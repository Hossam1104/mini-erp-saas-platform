#pragma warning disable CS1591

using System.Collections.Frozen;
using MiniErp.Contracts.Modules.Foundation;
using MiniErp.Contracts.Modules.MasterData;

namespace MiniErp.App.Modules.MasterData;

/// <summary>
/// Server-owned binding between a Master Data operation and the capability that
/// is required to authorize it. The immutable catalog prevents callers from
/// supplying an unrelated capability alongside an operation.
/// </summary>
public static class MasterDataOperationCatalog
{
    private static readonly FrozenDictionary<MasterDataOperation, MasterDataCapability> RequiredCapabilities =
        new Dictionary<MasterDataOperation, MasterDataCapability>
        {
            [MasterDataOperation.View] = MasterDataCapability.View,
            [MasterDataOperation.Create] = MasterDataCapability.Create,
            [MasterDataOperation.Edit] = MasterDataCapability.Edit,
            [MasterDataOperation.Activate] = MasterDataCapability.Activate,
            [MasterDataOperation.Deactivate] = MasterDataCapability.Deactivate,
            [MasterDataOperation.Approve] = MasterDataCapability.Approve,
            [MasterDataOperation.Import] = MasterDataCapability.ImportMigrate,
            [MasterDataOperation.ViewAuditHistory] = MasterDataCapability.ViewAuditHistory,
            // Reactivation is the lifecycle counterpart of activation and
            // therefore deliberately uses the existing Activate permission.
            [MasterDataOperation.Reactivate] = MasterDataCapability.Activate
        }.ToFrozenDictionary();

    private static readonly FrozenDictionary<string, MasterDataCapability> PermissionCapabilities =
        BuildPermissionCapabilities();

    /// <summary>
    /// Resolves the required capability for a defined operation. Unknown or
    /// unmapped values return false so authorization can fail closed.
    /// </summary>
    public static bool TryGetRequiredCapability(
        MasterDataOperation operation,
        out MasterDataCapability capability) =>
        RequiredCapabilities.TryGetValue(operation, out capability);

    /// <summary>
    /// Maps an exact server-owned Foundation permission to the one generic
    /// Master Data capability represented by that public operation. The
    /// Foundation catalogue remains the source of truth for permission names;
    /// unknown or unrelated permissions fail closed.
    /// </summary>
    public static bool TryGetCapabilityForPermission(
        string? permission,
        out MasterDataCapability capability)
    {
        if (string.IsNullOrWhiteSpace(permission))
        {
            capability = default;
            return false;
        }

        return PermissionCapabilities.TryGetValue(permission.Trim(), out capability);
    }

    private static FrozenDictionary<string, MasterDataCapability> BuildPermissionCapabilities()
    {
        var mappings = FoundationOperationCatalog.PublicOperations
            .Where(operation => operation.OperationId.StartsWith("master-data.", StringComparison.Ordinal)
                && !string.IsNullOrWhiteSpace(operation.ExactPermissionCode))
            .Select(operation =>
                (Permission: operation.ExactPermissionCode!,
                    Capability: CapabilityForOperation(operation.OperationId)))
            .GroupBy(item => item.Permission, StringComparer.Ordinal)
            .ToDictionary(
                group => group.Key,
                group => group.Select(item => item.Capability).Distinct().Single(),
                StringComparer.Ordinal);

        return mappings.ToFrozenDictionary(StringComparer.Ordinal);
    }

    private static MasterDataCapability CapabilityForOperation(string operationId)
    {
        if (operationId.EndsWith(".audit.read", StringComparison.Ordinal))
        {
            return MasterDataCapability.ViewAuditHistory;
        }

        if (operationId.EndsWith(".create", StringComparison.Ordinal))
        {
            return MasterDataCapability.Create;
        }

        if (operationId.EndsWith(".edit", StringComparison.Ordinal)
            || operationId.EndsWith(".price.append", StringComparison.Ordinal))
        {
            return MasterDataCapability.Edit;
        }

        if (operationId.EndsWith(".deactivate", StringComparison.Ordinal))
        {
            return MasterDataCapability.Deactivate;
        }

        if (operationId.EndsWith(".reactivate", StringComparison.Ordinal))
        {
            return MasterDataCapability.Activate;
        }

        if (operationId.EndsWith(".list", StringComparison.Ordinal)
            || operationId.EndsWith(".read", StringComparison.Ordinal)
            || operationId.EndsWith(".history.read", StringComparison.Ordinal)
            || operationId.EndsWith(".reference.read", StringComparison.Ordinal)
            || operationId.EndsWith(".preview.read", StringComparison.Ordinal)
            || operationId.EndsWith(".calculate", StringComparison.Ordinal))
        {
            return MasterDataCapability.View;
        }

        throw new InvalidOperationException($"Unmapped Master Data Foundation operation: {operationId}");
    }
}

#pragma warning restore CS1591
