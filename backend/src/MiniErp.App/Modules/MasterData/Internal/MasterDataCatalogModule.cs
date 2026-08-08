using MiniErp.Contracts.Modules;
using MiniErp.Contracts.Modules.MasterData;

namespace MiniErp.App.Modules.MasterData.Internal;

internal sealed class MasterDataCatalogModule : IMasterDataCatalogModule
{
    public ModuleDescriptor Descriptor { get; } = new(
        "master-data-catalog",
        "Master Data and Catalog",
        "MasterDataAndCatalog");

    public ModuleRegistrationEvidence RegistrationEvidence { get; } = new(
        "master-data-catalog",
        true);
}
