using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using MiniErp.App.BuildingBlocks.Tenancy;

namespace MiniErp.Infrastructure.Persistence.Modules.MasterData;

/// <summary>
/// Module-owned stored-owner registrations. The query remains Tenant-filtered;
/// a foreign or absent target is deliberately equivalent to an unavailable
/// stored owner to the ordinary write path.
/// </summary>
internal static class MasterDataTenantOwnershipVerifier
{
    internal static TenantOwnershipVerifierRegistration For<TEntity>()
        where TEntity : class, ITenantOwned => new(
            typeof(TEntity),
            static (context, entry) => Read<TEntity>(context, entry),
            static (context, entry, cancellationToken) =>
                ReadAsync<TEntity>(context, entry, cancellationToken));

    private static TenantId? Read<TEntity>(
        TenantPersistenceDbContext context,
        EntityEntry entry)
        where TEntity : class, ITenantOwned
    {
        if (context is not MasterDataDbContext masterDataContext
            || entry.Entity is not TEntity entity
            || entry.Property("Id").CurrentValue is not Guid id
            || id == Guid.Empty)
        {
            return null;
        }

        var stored = masterDataContext.Set<TEntity>()
            .Where(candidate => EF.Property<Guid>(candidate, "Id") == id)
            .Select(candidate => candidate.TenantId)
            .SingleOrDefault();
        return stored == default ? null : stored;
    }

    private static async Task<TenantId?> ReadAsync<TEntity>(
        TenantPersistenceDbContext context,
        EntityEntry entry,
        CancellationToken cancellationToken)
        where TEntity : class, ITenantOwned
    {
        if (context is not MasterDataDbContext masterDataContext
            || entry.Entity is not TEntity
            || entry.Property("Id").CurrentValue is not Guid id
            || id == Guid.Empty)
        {
            return null;
        }

        var stored = await masterDataContext.Set<TEntity>()
            .Where(candidate => EF.Property<Guid>(candidate, "Id") == id)
            .Select(candidate => candidate.TenantId)
            .SingleOrDefaultAsync(cancellationToken);
        return stored == default ? null : stored;
    }
}
