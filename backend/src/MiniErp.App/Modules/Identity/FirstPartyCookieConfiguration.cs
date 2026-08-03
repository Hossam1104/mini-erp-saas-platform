using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;

namespace MiniErp.App.Modules.Identity;

internal static class FirstPartyCookieConfiguration
{
    internal const string Scheme = "MiniErp.Identity";

    internal static CookieAuthenticationOptions Create(IdentitySecurityOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        options.Validate();

        return new CookieAuthenticationOptions
        {
            Cookie = new CookieBuilder
            {
                Name = "__Host-MiniErp.Auth",
                HttpOnly = true,
                SecurePolicy = CookieSecurePolicy.Always,
                SameSite = SameSiteMode.Lax,
                IsEssential = true,
                Path = "/"
            },
            ExpireTimeSpan = options.AbsoluteSessionLifetime,
            SlidingExpiration = false,
            LoginPath = "/account/sign-in",
            AccessDeniedPath = "/account/access-denied"
        };
    }
}
