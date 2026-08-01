using MiniErp.App.BuildingBlocks.Composition;
using MiniErp.Contracts.Modules.Platform;

namespace MiniErp.App.Modules.Platform;

/// <summary>
/// Composition entry point for the Platform Administration module.
/// </summary>
public static class PlatformModuleRegistration
{
    /// <summary>
    /// Creates the module through its public composition seam.
    /// </summary>
    public static IPlatformAdministrationModule Create()
    {
        return ModuleRegistration.Create<IPlatformAdministrationModule>(
            static () => new Internal.PlatformAdministrationModule());
    }
}
