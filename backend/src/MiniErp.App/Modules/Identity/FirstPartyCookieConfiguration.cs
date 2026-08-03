#pragma warning disable CS1591

using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;

namespace MiniErp.App.Modules.Identity;

public static class FirstPartyCookieConfiguration
{
    public const string Scheme = "MiniErp.Identity";

    /// <summary>Creates the approved first-party host cookie options.</summary>
    public static CookieAuthenticationOptions CreateForHost() => Create(IdentitySecurityOptions.Default);

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

#pragma warning restore CS1591
