using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace MiniErp.App.Modules.Identity;

internal static class IdentityModuleRegistration
{
    internal static IServiceCollection AddIdentityAuthorization(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<IPasswordHasher<GlobalUser>, PasswordHasher<GlobalUser>>();
        services.AddSingleton<IdentityStore>();
        services.AddSingleton<IdentityAuthorizationService>();
        services.AddSingleton<IAuthenticationAssuranceEvidenceSource, UnavailableAuthenticationAssuranceEvidenceSource>();
        services.AddSingleton(TimeProvider.System);
        return services;
    }
}
