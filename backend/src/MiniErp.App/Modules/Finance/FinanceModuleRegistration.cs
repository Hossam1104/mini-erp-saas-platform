#pragma warning disable CS1591

using Microsoft.Extensions.DependencyInjection;

namespace MiniErp.App.Modules.Finance;

public static class FinanceModuleRegistration
{
    public static IServiceCollection AddFinanceApplication(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<IFinanceCompanyProvider, NoFinanceCompanyProvider>();
        services.AddSingleton<IFinancePersistence, UnavailableFinancePersistence>();
        services.AddSingleton<FinanceAuthorizationService>();
        return services;
    }
}

#pragma warning restore CS1591
