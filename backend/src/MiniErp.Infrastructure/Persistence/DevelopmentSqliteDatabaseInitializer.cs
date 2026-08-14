using Microsoft.EntityFrameworkCore;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.Modules.Identity;

namespace MiniErp.Infrastructure.Persistence;

/// <summary>
/// Shared Development-only schema initialization seam for module-owned SQLite
/// databases. Each module supplies its own context factory and database file.
/// </summary>
internal static class DevelopmentSqliteDatabaseInitializer
{
    public static void EnsureCreated(
        string connectionString,
        Func<DbContextOptions, TenantContext, DbContext> contextFactory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        ArgumentNullException.ThrowIfNull(contextFactory);

        var options = new DbContextOptionsBuilder()
            .UseSqlite(connectionString)
            .Options;
        var schemaTenant = TenantContext.ForOrdinaryMembership(
            DevelopmentBootstrap.DevTenantId,
            new MembershipReference(Guid.NewGuid()),
            null,
            null,
            Guid.NewGuid());

        using var db = contextFactory(options, schemaTenant);
        db.Database.EnsureCreated();
    }
}
