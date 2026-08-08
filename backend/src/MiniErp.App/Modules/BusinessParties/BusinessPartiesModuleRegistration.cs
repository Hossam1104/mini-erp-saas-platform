#pragma warning disable CS1591

using MiniErp.App.BuildingBlocks.Composition;
using MiniErp.Contracts.Modules;
using MiniErp.Contracts.Modules.BusinessParties;

namespace MiniErp.App.Modules.BusinessParties;

public static class BusinessPartiesModuleRegistration
{
    public static IBusinessPartiesModule Create()
    {
        return ModuleRegistration.Create<IBusinessPartiesModule>(
            static () => new Internal.BusinessPartiesModule());
    }
}

#pragma warning restore CS1591
