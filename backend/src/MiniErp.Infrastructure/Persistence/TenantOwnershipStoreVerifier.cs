using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using MiniErp.App.BuildingBlocks.Tenancy;

namespace MiniErp.Infrastructure.Persistence;

/// <summary>
/// The only intentionally unscoped ownership lookup. It projects only the
/// stored TenantId for an EF metadata primary key and is not exposed through
/// a repository or dependency-injection contract.
/// </summary>
internal static class TenantOwnershipStoreVerifier
{
    internal static TenantId? ReadStoredTenantId(
        TenantPersistenceDbContext dbContext,
        EntityEntry entry)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(entry);

        var primaryKey = entry.Metadata.FindPrimaryKey();
        if (primaryKey is null || primaryKey.Properties.Count != 1)
        {
            return null;
        }

        var keyProperty = primaryKey.Properties[0];
        if (entry.Property(keyProperty.Name).CurrentValue is not Guid recordId
            || recordId == Guid.Empty
            || entry.Entity is not TenantOwnedRecord)
        {
            return null;
        }

        // Projection avoids tracked values and payload retrieval. This is the
        // narrow allow-listed ownership check, not an ordinary read path.
        var storedTenantIds = UnscopedTenantOwnedRecords(dbContext)
            .Where(record => record.Id == recordId)
            .Select(record => record.TenantId)
            .ToList();

        return storedTenantIds.Count == 0 ? null : storedTenantIds[0];
    }

    internal static async Task<TenantId?> ReadStoredTenantIdAsync(
        TenantPersistenceDbContext dbContext,
        EntityEntry entry,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(dbContext);
        ArgumentNullException.ThrowIfNull(entry);

        var primaryKey = entry.Metadata.FindPrimaryKey();
        if (primaryKey is null || primaryKey.Properties.Count != 1)
        {
            return null;
        }

        var keyProperty = primaryKey.Properties[0];
        if (entry.Property(keyProperty.Name).CurrentValue is not Guid recordId
            || recordId == Guid.Empty
            || entry.Entity is not TenantOwnedRecord)
        {
            return null;
        }

        // Keep the asynchronous check equally narrow and store-backed.
        var storedTenantIds = await UnscopedTenantOwnedRecords(dbContext)
            .Where(record => record.Id == recordId)
            .Select(record => record.TenantId)
            .ToListAsync(cancellationToken);

        return storedTenantIds.Count == 0 ? null : storedTenantIds[0];
    }

    private static IQueryable<TenantOwnedRecord> UnscopedTenantOwnedRecords(
        TenantPersistenceDbContext dbContext)
    {
        // This is the single syntax-level unscoped EF operation in the
        // repository. Both sync and async verification share this path.
        return dbContext.TenantOwnedRecords.IgnoreQueryFilters();
    }
}
