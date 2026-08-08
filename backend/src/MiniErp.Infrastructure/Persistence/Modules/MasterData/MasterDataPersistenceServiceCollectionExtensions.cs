#pragma warning disable CS1591

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using MiniErp.App.Modules.MasterData;

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
        services.AddSingleton<IMasterDataCatalogPersistence>(
            new MasterDataCatalogPersistence(optionsBuilder.Options));
        services.AddSingleton<MasterDataCategoryUomService>();
        return services;
    }
}

#pragma warning restore CS1591
