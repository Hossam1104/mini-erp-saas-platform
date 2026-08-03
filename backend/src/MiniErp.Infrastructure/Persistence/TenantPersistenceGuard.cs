using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using MiniErp.App.BuildingBlocks.Tenancy;

namespace MiniErp.Infrastructure.Persistence;

internal sealed class TenantPersistenceGuard
{
    private readonly TenantContext _tenantContext;

    public TenantPersistenceGuard(TenantContext tenantContext)
    {
        _tenantContext = tenantContext ?? throw new ArgumentNullException(nameof(tenantContext));
    }

    public void Validate(ChangeTracker changeTracker)
    {
        ArgumentNullException.ThrowIfNull(changeTracker);

        foreach (var entry in changeTracker.Entries())
        {
            if (entry.Entity is not ITenantOwned tenantOwned)
            {
                continue;
            }

            if (tenantOwned.TenantId == default)
            {
                throw new TenantPersistenceViolationException("Tenant-owned persistence requires a TenantId.");
            }

            if (tenantOwned.TenantId != _tenantContext.TenantId)
            {
                throw new TenantPersistenceViolationException("Tenant ownership does not match the trusted context.");
            }

            if (entry.Metadata.FindProperty(nameof(TenantOwnedRecord.TenantId)) is { } tenantProperty)
            {
                var currentTenant = entry.Property(tenantProperty.Name).CurrentValue;
                if (currentTenant is not TenantId currentTenantId || currentTenantId == default)
                {
                    throw new TenantPersistenceViolationException("Tenant-owned persistence requires a valid TenantId.");
                }

                if (entry.State is EntityState.Modified or EntityState.Deleted)
                {
                    var originalTenant = entry.Property(tenantProperty.Name).OriginalValue;
                    if (originalTenant is not TenantId originalTenantId || originalTenantId != currentTenantId)
                    {
                        throw new TenantPersistenceViolationException("Tenant ownership is immutable after establishment.");
                    }
                }
            }

            if (entry.Entity is TenantOwnedRecord record)
            {
                if (record.RelationshipKind == TenantRelationshipKind.None && record.RelatedTenantId.HasValue)
                {
                    throw new TenantPersistenceViolationException("A related Tenant requires a relationship kind.");
                }

                if (record.RelationshipKind != TenantRelationshipKind.None
                    && (!record.RelatedTenantId.HasValue || record.RelatedTenantId.Value != _tenantContext.TenantId))
                {
                    throw new TenantPersistenceViolationException("Tenant relationships must remain within the trusted Tenant.");
                }
            }
        }
    }
}
