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

    /// <summary>
    /// Development composition-root adapter for SQLite persistence.
    /// Creates the database schema via EnsureCreated during registration.
    /// </summary>
    public static IServiceCollection AddBusinessPartiesSqlitePersistence(
        this IServiceCollection services,
        string connectionString)
    {
        ArgumentNullException.ThrowIfNull(services);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new ArgumentException("A SQLite connection string is required.", nameof(connectionString));
        }

        // EnsureCreated needs a TenantContext to construct the DbContext.
        // We use a throwaway context solely to create the schema.
        var schemaOptions = new DbContextOptionsBuilder().UseSqlite(connectionString).Options;
        var bootstrapTenant = MiniErp.App.BuildingBlocks.Tenancy.TenantContext.ForOrdinaryMembership(
            MiniErp.App.Modules.Identity.DevelopmentBootstrap.DevTenantId,
            new MiniErp.App.BuildingBlocks.Tenancy.MembershipReference(Guid.NewGuid()),
            null,
            null,
            Guid.NewGuid());
        using (var db = new BusinessPartiesDbContext(schemaOptions, bootstrapTenant))
        {
            // Multiple DbContexts may share base tables (e.g. AuditEvents).
            // When they target the same file-based SQLite database, the second
            // EnsureCreated call would fail with "table already exists".
            try { db.Database.EnsureCreated(); }
            catch (Microsoft.Data.Sqlite.SqliteException ex) when (ex.SqliteErrorCode == 1) { /* table already exists */ }
        }

        return services.AddBusinessPartiesPersistence(options => options.UseSqlite(connectionString));
    }
}

#pragma warning restore CS1591
