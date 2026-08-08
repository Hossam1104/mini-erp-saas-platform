#pragma warning disable CS1591

using System.Collections.Frozen;
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

    /// <summary>
    /// Resolves the required capability for a defined operation. Unknown or
    /// unmapped values return false so authorization can fail closed.
    /// </summary>
    public static bool TryGetRequiredCapability(
        MasterDataOperation operation,
        out MasterDataCapability capability) =>
        RequiredCapabilities.TryGetValue(operation, out capability);
}

#pragma warning restore CS1591
