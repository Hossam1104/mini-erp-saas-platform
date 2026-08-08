#pragma warning disable CS1591

using MiniErp.Contracts.Modules;

namespace MiniErp.Contracts.Modules.BusinessParties;

public interface IBusinessPartiesModule
{
    ModuleDescriptor Descriptor { get; }

    ModuleRegistrationEvidence RegistrationEvidence { get; }
}

public sealed record ModuleRegistrationEvidence(string ModuleKey, bool IsRegistered);

#pragma warning restore CS1591
