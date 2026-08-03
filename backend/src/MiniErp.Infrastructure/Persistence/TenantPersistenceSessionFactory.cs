using Microsoft.EntityFrameworkCore;
using MiniErp.App.BuildingBlocks.Tenancy;

namespace MiniErp.Infrastructure.Persistence;

/// <summary>
/// Creates explicit Tenant-bound EF Core sessions from immutable trusted context.
/// </summary>
public sealed class TenantPersistenceSessionFactory : ITenantPersistenceSessionFactory
{
    private readonly DbContextOptions _options;

    /// <summary>Creates a factory over immutable provider options.</summary>
    public TenantPersistenceSessionFactory(DbContextOptions options)
    {
        _options = options ?? throw new ArgumentNullException(nameof(options));
    }

    /// <summary>Creates one explicitly Tenant-bound persistence session.</summary>
    public ITenantPersistenceSession Create(TenantContext tenantContext)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        return new TenantPersistenceSession(new TenantPersistenceDbContext(_options, tenantContext));
    }

    internal static void EnsureCreatedForTests(DbContextOptions options, TenantContext tenantContext)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(tenantContext);

        using var context = new TenantPersistenceDbContext(options, tenantContext);
        context.Database.EnsureCreated();
    }
}
