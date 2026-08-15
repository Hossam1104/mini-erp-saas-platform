#pragma warning disable CS1591

using Microsoft.Extensions.Configuration;
using MiniErp.App.BuildingBlocks.Tenancy;

namespace MiniErp.App.Modules.Identity;

/// <summary>
/// Resolves human Tenant labels from server-owned configuration. No client
/// supplied label participates in authorization or context selection.
/// </summary>
internal interface ITenantDisplayNameProvider
{
    string GetDisplayName(TenantId tenantId);
}

internal sealed class ConfiguredTenantDisplayNameProvider : ITenantDisplayNameProvider
{
    private readonly IReadOnlyDictionary<Guid, string> configuredNames;

    public ConfiguredTenantDisplayNameProvider(IConfiguration configuration)
    {
        var names = new Dictionary<Guid, string>();
        foreach (var entry in configuration.GetSection("MESP_TENANT_DISPLAY_NAMES").GetChildren())
        {
            if (Guid.TryParse(entry.Key, out var tenantId)
                && !string.IsNullOrWhiteSpace(entry.Value))
            {
                names[tenantId] = entry.Value.Trim();
            }
        }

        var developmentName = configuration["MESP_DEV_TENANT_DISPLAY_NAME"];
        if (!string.IsNullOrWhiteSpace(developmentName))
        {
            names[DevelopmentBootstrap.DevTenantId.Value] = developmentName.Trim();
        }

        configuredNames = names;
    }

    public string GetDisplayName(TenantId tenantId) =>
        configuredNames.TryGetValue(tenantId.Value, out var displayName)
            ? displayName
            : $"Tenant {tenantId.Value:D}";
}

internal sealed class DefaultTenantDisplayNameProvider : ITenantDisplayNameProvider
{
    public string GetDisplayName(TenantId tenantId) => $"Tenant {tenantId.Value:D}";
}

#pragma warning restore CS1591
