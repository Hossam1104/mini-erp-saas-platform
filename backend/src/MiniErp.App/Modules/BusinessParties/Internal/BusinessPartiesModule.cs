using MiniErp.Contracts.Modules;
using MiniErp.Contracts.Modules.BusinessParties;

namespace MiniErp.App.Modules.BusinessParties.Internal;

internal sealed class BusinessPartiesModule : IBusinessPartiesModule
{
    public ModuleDescriptor Descriptor { get; } = new(
        "business-parties",
        "Business Parties",
        "BusinessParties");

    public ModuleRegistrationEvidence RegistrationEvidence { get; } = new(
        "business-parties",
        true);
}
