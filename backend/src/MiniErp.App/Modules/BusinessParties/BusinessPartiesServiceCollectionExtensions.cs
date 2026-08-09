#pragma warning disable CS1591

using Microsoft.Extensions.DependencyInjection;

namespace MiniErp.App.Modules.BusinessParties;

/// <summary>Composition helpers for the bounded Business Parties Supplier seam.</summary>
public static class BusinessPartiesServiceCollectionExtensions
{
    /// <summary>
    /// Registers Supplier's independently owned authorization and application
    /// seam. Persistence remains fail-closed until the host supplies the
    /// Business Parties module-owned provider adapter.
    /// </summary>
    public static IServiceCollection AddSupplierIdentity(
        this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);

        services.AddSingleton<SupplierScopePolicy>();
        services.AddSingleton<SupplierResourcePolicy>();
        services.AddSingleton<SupplierApprovalPolicy>();
        services.AddSingleton<SupplierResourceAuthorizationService>();
        services.AddSingleton<ISupplierPersistence, UnavailableSupplierPersistence>();
        services.AddSingleton<ISupplierCrossRoleMatchPolicy, SupplierCrossRoleMatchPolicyUnavailable>();
        services.AddSingleton<SupplierService>();
        return services;
    }
}

#pragma warning restore CS1591
