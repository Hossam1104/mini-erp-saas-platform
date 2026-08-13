#pragma warning disable CS1591

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MiniErp.App.Modules.BusinessParties;

namespace MiniErp.Infrastructure.Persistence.Modules.BusinessParties;

/// <summary>Composition helpers for the module-owned Business Parties adapters.</summary>
public static class BusinessPartiesPersistenceServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Supplier and Business Customer persistence adapters with
    /// caller-supplied provider options. It never creates a database or
    /// executes migrations.
    /// </summary>
    public static IServiceCollection AddBusinessPartiesPersistence(
        this IServiceCollection services,
        Action<DbContextOptionsBuilder> configureOptions)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureOptions);

        var optionsBuilder = new DbContextOptionsBuilder();
        configureOptions(optionsBuilder);
        services.AddSingleton<ISupplierPersistence>(
            new BusinessPartiesSupplierPersistence(optionsBuilder.Options));
        var customerPersistence = new BusinessPartiesCustomerPersistence(optionsBuilder.Options);
        services.AddSingleton<ICustomerPersistence>(customerPersistence);
        services.AddSingleton<IBusinessCustomerReferenceReader>(customerPersistence);
        return services;
    }

    /// <summary>
    /// Composition-root adapter for an explicitly supplied SQL Server
    /// connection. No database is opened or migrated during registration.
    /// </summary>
    public static IServiceCollection AddBusinessPartiesSqlServerPersistence(
        this IServiceCollection services,
        string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("A SQL Server connection string is required.", nameof(connectionString));
        }

        return services.AddBusinessPartiesPersistence(options => options.UseSqlServer(connectionString));
    }
}

#pragma warning restore CS1591
