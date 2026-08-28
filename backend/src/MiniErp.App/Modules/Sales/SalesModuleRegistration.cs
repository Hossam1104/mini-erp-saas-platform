#pragma warning disable CS1591

using Microsoft.Extensions.DependencyInjection;
using MiniErp.App.Modules.Finance;

namespace MiniErp.App.Modules.Sales;

public static class SalesModuleRegistration
{
    public static IServiceCollection AddSalesApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<SalesAuthorizationService>();
        services.AddSingleton<ISalesApprovalPolicyProvider, DefaultSalesApprovalPolicyProvider>();
        services.AddSingleton<ISalesCommercialAuthorityProvider, NoSalesCommercialAuthorityProvider>();
        services.AddSingleton<ISalesApprovalDelegationProvider, NoSalesApprovalDelegationProvider>();
        services.AddSingleton<ISalesCreditLimitProvider, NoSalesCreditLimitProvider>();
        services.AddSingleton<ISalesTaxReferenceProvider, MasterDataSalesTaxReferenceProvider>();
        services.AddSingleton<ISalesExchangeRateReferenceProvider, MasterDataSalesExchangeRateReferenceProvider>();
        services.AddSingleton<ISalesPersistence, UnavailableSalesPersistence>();
        services.AddSingleton<SalesService>();
        return services;
    }
}

#pragma warning restore CS1591
