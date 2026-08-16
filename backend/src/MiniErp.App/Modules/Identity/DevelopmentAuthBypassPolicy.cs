#pragma warning disable CS1591

using System.Net;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace MiniErp.App.Modules.Identity;

/// <summary>
/// Central policy for the local Development-only persistent session shortcut.
/// </summary>
public static class DevelopmentAuthBypassPolicy
{
    public const string SettingName = "MESP_DEV_AUTH_BYPASS";

    public static bool IsEnabled(IConfiguration configuration) =>
        string.Equals(configuration[SettingName], "true", StringComparison.OrdinalIgnoreCase);

    public static bool IsExactDevelopment(IHostEnvironment environment) =>
        string.Equals(environment.EnvironmentName, Environments.Development, StringComparison.Ordinal);

    public static bool IsAllowed(
        IHostEnvironment environment,
        IConfiguration configuration,
        IPAddress? remoteIpAddress) =>
        IsExactDevelopment(environment)
        && IsEnabled(configuration)
        && remoteIpAddress is not null
        && IPAddress.IsLoopback(remoteIpAddress);

    public static void ValidateStartup(IHostEnvironment environment, IConfiguration configuration)
    {
        if (IsEnabled(configuration) && !IsExactDevelopment(environment))
        {
            throw new InvalidOperationException(
                $"{SettingName}=true is permitted only when ASPNETCORE_ENVIRONMENT is exactly Development.");
        }
    }
}

#pragma warning restore CS1591
