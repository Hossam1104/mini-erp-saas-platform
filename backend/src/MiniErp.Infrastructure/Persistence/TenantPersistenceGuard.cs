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
            ValidateEntry(entry);
        }
    }

    public async Task ValidateAsync(
        ChangeTracker changeTracker,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(changeTracker);

        foreach (var entry in changeTracker.Entries())
        {
            await ValidateEntryAsync(entry, cancellationToken);
        }
    }

    private void ValidateEntry(EntityEntry entry)
    {
        if (entry.Entity is not ITenantOwned tenantOwned)
        {
            return;
        }

        var currentTenant = tenantOwned.TenantId;
        if (currentTenant == default)
        {
            throw Deny(
                TenantPersistenceViolationCode.MissingTenantId,
                OperationFor(entry.State));
        }

        if (entry.State is EntityState.Modified or EntityState.Deleted)
        {
            var storedTenant = TenantOwnershipStoreVerifier.ReadStoredTenantId(
                (TenantPersistenceDbContext)entry.Context,
                entry);
            ValidateStoredOwnership(entry, currentTenant, storedTenant);
        }
        else if (currentTenant != _tenantContext.TenantId)
        {
            throw Deny(
                TenantPersistenceViolationCode.TenantContextMismatch,
                OperationFor(entry.State));
        }

        ValidateRelationship(entry.Entity);
    }

    private async Task ValidateEntryAsync(EntityEntry entry, CancellationToken cancellationToken)
    {
        if (entry.Entity is not ITenantOwned tenantOwned)
        {
            return;
        }

        var currentTenant = tenantOwned.TenantId;
        if (currentTenant == default)
        {
            throw Deny(
                TenantPersistenceViolationCode.MissingTenantId,
                OperationFor(entry.State));
        }

        if (entry.State is EntityState.Modified or EntityState.Deleted)
        {
            var storedTenant = await TenantOwnershipStoreVerifier.ReadStoredTenantIdAsync(
                (TenantPersistenceDbContext)entry.Context,
                entry,
                cancellationToken);
            ValidateStoredOwnership(entry, currentTenant, storedTenant);
        }
        else if (currentTenant != _tenantContext.TenantId)
        {
            throw Deny(
                TenantPersistenceViolationCode.TenantContextMismatch,
                OperationFor(entry.State));
        }

        ValidateRelationship(entry.Entity);
    }

    private void ValidateStoredOwnership(
        EntityEntry entry,
        TenantId currentTenant,
        TenantId? storedTenant)
    {
        if (!storedTenant.HasValue)
        {
            throw Deny(
                TenantPersistenceViolationCode.StoredOwnerMissing,
                OperationFor(entry.State));
        }

        if (storedTenant.Value == default)
        {
            throw Deny(
                TenantPersistenceViolationCode.StoredOwnerInvalid,
                OperationFor(entry.State));
        }

        if (storedTenant.Value != _tenantContext.TenantId)
        {
            throw Deny(
                TenantPersistenceViolationCode.StoredOwnerMismatch,
                OperationFor(entry.State));
        }

        if (currentTenant != storedTenant.Value)
        {
            throw Deny(
                TenantPersistenceViolationCode.TenantOwnershipMutation,
                OperationFor(entry.State));
        }
    }

    private void ValidateRelationship(object entity)
    {
        if (entity is not TenantOwnedRecord record)
        {
            return;
        }

        if (!Enum.IsDefined(record.RelationshipKind))
        {
            throw Deny(
                TenantPersistenceViolationCode.InvalidRelationship,
                TenantPersistenceOperation.Relationship);
        }

        if (record.RelationshipKind == TenantRelationshipKind.None && record.RelatedTenantId.HasValue)
        {
            throw Deny(
                TenantPersistenceViolationCode.RelationshipMismatch,
                TenantPersistenceOperation.Relationship);
        }

        if (record.RelationshipKind != TenantRelationshipKind.None
            && (!record.RelatedTenantId.HasValue || record.RelatedTenantId.Value != _tenantContext.TenantId))
        {
            throw Deny(
                TenantPersistenceViolationCode.RelationshipMismatch,
                TenantPersistenceOperation.Relationship);
        }
    }

    private TenantPersistenceViolationException Deny(
        TenantPersistenceViolationCode code,
        TenantPersistenceOperation operation)
    {
        return TenantPersistenceViolationException.Deny(code, _tenantContext, operation);
    }

    private static TenantPersistenceOperation OperationFor(EntityState state) => state switch
    {
        EntityState.Added => TenantPersistenceOperation.Add,
        EntityState.Modified => TenantPersistenceOperation.Modify,
        EntityState.Deleted => TenantPersistenceOperation.Delete,
        _ => TenantPersistenceOperation.SaveChanges
    };
}
