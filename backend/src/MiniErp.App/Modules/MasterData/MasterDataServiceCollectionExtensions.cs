using Microsoft.Extensions.DependencyInjection;

namespace MiniErp.App.Modules.MasterData;

/// <summary>Composition helpers for the bounded Master Data authorization seam.</summary>
public static class MasterDataServiceCollectionExtensions
{
    /// <summary>
    /// Registers the bounded Category/UOM authorization policy composition.
    /// Capability resolution remains deny-by-default until the Identity
    /// permission provider is connected by the host.
    /// </summary>
    public static IServiceCollection AddMasterDataAuthorization(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<IMasterDataCapabilityResolver, DenyAllMasterDataCapabilityResolver>();
        services.AddSingleton<IMasterDataScopePolicy, CategoryUomScopePolicy>();
        services.AddSingleton<IMasterDataResourcePolicy, CategoryUomResourcePolicy>();
        services.AddSingleton<IMasterDataApprovalPolicy, CategoryUomApprovalPolicy>();
        services.AddSingleton<MasterDataResourceAuthorizationService>();
        services.AddSingleton<MasterDataCategoryHierarchyPolicy>();
        return services;
    }

    /// <summary>
    /// Registers Product's independently owned authorization and application
    /// seam. Persistence remains fail-closed until the host supplies the
    /// module-owned provider adapter.
    /// </summary>
    public static IServiceCollection AddProductIdentity(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<ProductScopePolicy>();
        services.AddSingleton<ProductResourcePolicy>();
        services.AddSingleton<ProductApprovalPolicy>();
        services.AddSingleton<ProductResourceAuthorizationService>();
        services.AddSingleton<MasterDataTenantContextResolver>();
        services.AddSingleton<IProductIdentityPersistence, UnavailableProductIdentityPersistence>();
        services.AddSingleton<IProductBaseUomChangePolicy, ProductBaseUomChangePolicyUnavailable>();
        services.AddSingleton<ProductIdentityService>();
        return services;
    }
}
