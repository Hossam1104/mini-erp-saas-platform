#pragma warning disable CS1591

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MiniErp.App.Modules.BusinessParties;
using MiniErp.App.Modules.MasterData;
using MiniErp.Infrastructure.Persistence;

namespace MiniErp.Infrastructure.Persistence.Modules.MasterData;

/// <summary>Composition helper for the module-owned persistence adapter.</summary>
public static class MasterDataPersistenceServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Master Data application persistence adapter. The host
    /// supplies provider options; this method does not create a database or
    /// execute migrations.
    /// </summary>
    public static IServiceCollection AddMasterDataPersistence(
        this IServiceCollection services,
        Action<DbContextOptionsBuilder> configureOptions)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureOptions);

        var optionsBuilder = new DbContextOptionsBuilder();
        configureOptions(optionsBuilder);
        var persistence = new MasterDataCatalogPersistence(optionsBuilder.Options);
        var currencyPaymentTermPersistence = new MasterDataCurrencyPaymentTermPersistence(optionsBuilder.Options);
        var exchangeRatePersistence = new MasterDataExchangeRatePersistence(optionsBuilder.Options);
        var taxPersistence = new MasterDataTaxPersistence(optionsBuilder.Options);
        var importPersistence = new MasterDataImportPersistence(optionsBuilder.Options);
        services.AddSingleton<IMasterDataCatalogPersistence>(persistence);
        services.AddSingleton<IMasterDataCurrencyPaymentTermPersistence>(currencyPaymentTermPersistence);
        services.AddSingleton<IMasterDataExchangeRatePersistence>(exchangeRatePersistence);
        services.AddSingleton<IMasterDataTaxPersistence>(taxPersistence);
        services.AddSingleton<IMasterDataImportPersistence>(importPersistence);
        services.AddSingleton<IMasterDataPriceListPersistence>(servicesProvider =>
            new MasterDataPriceListPersistence(
                optionsBuilder.Options,
                servicesProvider.GetService<IBusinessCustomerReferenceReader>()));
        services.AddSingleton<IProductIdentityPersistence>(persistence);
        services.AddSingleton<MasterDataCategoryUomService>();
        return services;
    }

    /// <summary>
    /// Composition-root adapter for an explicitly supplied SQL Server
    /// connection. The API project does not reference EF Core directly and
    /// this method never creates a database or executes migrations.
    /// </summary>
    public static IServiceCollection AddMasterDataSqlServerPersistence(
        this IServiceCollection services,
        string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("A SQL Server connection string is required.", nameof(connectionString));
        }

        return services.AddMasterDataPersistence(options =>
            options.UseSqlServer(
                connectionString,
                sql => sql.MigrationsHistoryTable(
                    SqlServerMigrationConfiguration.MasterDataHistoryTable,
                    SqlServerMigrationConfiguration.HistorySchema)));
    }

    /// <summary>
    /// Development composition-root adapter for SQLite persistence.
    /// Creates the database schema via EnsureCreated during registration.
    /// </summary>
    public static IServiceCollection AddMasterDataSqlitePersistence(
        this IServiceCollection services,
        string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("A SQLite connection string is required.", nameof(connectionString));
        }

        return services.AddMasterDataPersistence(options => options.UseSqlite(connectionString));
    }

    /// <summary>
    /// Initializes the explicitly selected Development-only Master Data SQLite
    /// database. Initialization is separate from service registration so a
    /// failed schema operation cannot be hidden during composition.
    /// </summary>
    public static void EnsureDevelopmentSqliteDatabase(string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);
        DevelopmentSqliteDatabaseInitializer.EnsureCreated(
            connectionString,
            (options, tenantContext) => new MasterDataDbContext(options, tenantContext));
    }
}

#pragma warning restore CS1591
