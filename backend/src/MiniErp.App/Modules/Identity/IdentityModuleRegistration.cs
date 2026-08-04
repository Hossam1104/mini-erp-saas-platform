#pragma warning disable CS1591

using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

namespace MiniErp.App.Modules.Identity;

public static class IdentityModuleRegistration
{
    public static IServiceCollection AddIdentityAuthorization(this IServiceCollection services)
    {
        ArgumentNullException.ThrowIfNull(services);
        services.AddSingleton<IPasswordHasher<GlobalUser>, PasswordHasher<GlobalUser>>();
        services.AddSingleton<IdentityStore>();
        services.AddSingleton<IdentityAuthorizationService>(serviceProvider =>
            new IdentityAuthorizationService(
                serviceProvider.GetRequiredService<IdentityStore>(),
                timeProvider: serviceProvider.GetRequiredService<TimeProvider>(),
                passwordHasher: serviceProvider.GetRequiredService<IPasswordHasher<GlobalUser>>(),
                assuranceEvidenceSource: serviceProvider.GetRequiredService<IAuthenticationAssuranceEvidenceSource>()));
        services.AddSingleton<IAuthenticationAssuranceEvidenceSource, UnavailableAuthenticationAssuranceEvidenceSource>();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IFoundationIdentityHost>(serviceProvider =>
            new FoundationIdentityHost(serviceProvider.GetRequiredService<IdentityAuthorizationService>()));
        return services;
    }
}

#pragma warning restore CS1591
