using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.Infrastructure.Persistence;

namespace MiniErp.Infrastructure.Persistence.Modules.Sales;

internal static class SalesTenantOwnershipVerifier
{
    internal static TenantOwnershipVerifierRegistration For<TEntity>() where TEntity : class, ITenantOwned => new(
        typeof(TEntity),
        static (context, entry) => Read<TEntity>(context, entry),
        static (context, entry, cancellationToken) => ReadAsync<TEntity>(context, entry, cancellationToken));

    private static TenantId? Read<TEntity>(TenantPersistenceDbContext context, EntityEntry entry) where TEntity : class, ITenantOwned
    {
        if (context is not SalesDbContext sales || entry.Entity is not TEntity || entry.Property("Id").CurrentValue is not Guid id || id == Guid.Empty) return null;
        var stored = sales.Set<TEntity>().Where(item => EF.Property<Guid>(item, "Id") == id).Select(item => item.TenantId).SingleOrDefault();
        return stored == default ? null : stored;
    }

    private static async Task<TenantId?> ReadAsync<TEntity>(TenantPersistenceDbContext context, EntityEntry entry, CancellationToken cancellationToken) where TEntity : class, ITenantOwned
    {
        if (context is not SalesDbContext sales || entry.Entity is not TEntity || entry.Property("Id").CurrentValue is not Guid id || id == Guid.Empty) return null;
        var stored = await sales.Set<TEntity>().Where(item => EF.Property<Guid>(item, "Id") == id).Select(item => item.TenantId).SingleOrDefaultAsync(cancellationToken);
        return stored == default ? null : stored;
    }
}
