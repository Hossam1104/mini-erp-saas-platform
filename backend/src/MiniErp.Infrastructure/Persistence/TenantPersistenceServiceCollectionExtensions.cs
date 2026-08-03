using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MiniErp.App.BuildingBlocks.Tenancy;

namespace MiniErp.Infrastructure.Persistence;

/// <summary>
/// Registers the stateless Tenant persistence session factory at the composition root.
/// </summary>
public static class TenantPersistenceServiceCollectionExtensions
{
    /// <summary>Registers a stateless session factory; no context is ambient or singleton.</summary>
    public static IServiceCollection AddTenantPersistence(
        this IServiceCollection services,
        Action<DbContextOptionsBuilder> configureOptions)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configureOptions);

        var optionsBuilder = new DbContextOptionsBuilder();
        configureOptions(optionsBuilder);

        services.AddSingleton<ITenantPersistenceSessionFactory>(
            new TenantPersistenceSessionFactory(optionsBuilder.Options));
        return services;
    }
}
