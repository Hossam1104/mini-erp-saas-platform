#pragma warning disable CS1591

using MiniErp.App.BuildingBlocks.Composition;
using MiniErp.Contracts.Modules;
using MiniErp.Contracts.Modules.MasterData;

namespace MiniErp.App.Modules.MasterData;

public static class MasterDataModuleRegistration
{
    public static IMasterDataCatalogModule Create()
    {
        return ModuleRegistration.Create<IMasterDataCatalogModule>(
            static () => new Internal.MasterDataCatalogModule());
    }
}

#pragma warning restore CS1591
