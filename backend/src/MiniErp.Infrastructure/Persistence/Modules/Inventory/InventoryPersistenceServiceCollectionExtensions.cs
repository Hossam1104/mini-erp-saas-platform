#pragma warning disable CS1591

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MiniErp.App.Modules.Inventory;

namespace MiniErp.Infrastructure.Persistence.Modules.Inventory;

public static class InventoryPersistenceServiceCollectionExtensions
{
    public static IServiceCollection AddInventoryPersistence(this IServiceCollection services, Action<DbContextOptionsBuilder> configureOptions)
    {
        ArgumentNullException.ThrowIfNull(services); ArgumentNullException.ThrowIfNull(configureOptions);
        var optionsBuilder = new DbContextOptionsBuilder(); configureOptions(optionsBuilder);
        services.AddSingleton<IInventoryPersistence>(new InventoryPersistence(optionsBuilder.Options));
        return services;
    }

    public static IServiceCollection AddInventorySqlServerPersistence(this IServiceCollection services, string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        return services.AddInventoryPersistence(options => options.UseSqlServer(connectionString, sql => sql.MigrationsHistoryTable(SqlServerMigrationConfiguration.InventoryHistoryTable, SqlServerMigrationConfiguration.HistorySchema)));
    }

    public static IServiceCollection AddInventorySqlitePersistence(this IServiceCollection services, string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        return services.AddInventoryPersistence(options => options.UseSqlite(connectionString));
    }

    public static void EnsureDevelopmentSqliteDatabase(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        DevelopmentSqliteDatabaseInitializer.EnsureCreated(connectionString, (options, tenantContext) => new InventoryDbContext(options, tenantContext));
    }
}

#pragma warning restore CS1591
