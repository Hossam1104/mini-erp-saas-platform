#pragma warning disable CS1591

using Microsoft.Extensions.DependencyInjection;

namespace MiniErp.App.Modules.Inventory;

public static class InventoryModuleRegistration
{
    public static IServiceCollection AddInventoryApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<InventoryTenantContextResolver>();
        services.AddSingleton<InventoryResourceAuthorizationService>();
        services.AddSingleton<InventoryService>();
        services.AddSingleton<IInventoryWarehouseProvider, NoInventoryWarehouseProvider>();
        services.AddSingleton<IInventoryProductProvider, NoInventoryProductProvider>();
        services.AddSingleton<IInventoryPersistence, UnavailableInventoryPersistence>();
        return services;
    }
}

#pragma warning restore CS1591
