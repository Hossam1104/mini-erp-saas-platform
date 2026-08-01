using MiniErp.Contracts.Modules;

namespace MiniErp.Contracts.Modules.Platform;

/// <summary>
/// The public composition seam for Platform Administration.
/// </summary>
public interface IPlatformAdministrationModule
{
    /// <summary>
    /// Stable identity of the registered module.
    /// </summary>
    ModuleDescriptor Descriptor { get; }

    /// <summary>
    /// Composition evidence returned by the module seam.
    /// </summary>
    ModuleRegistrationEvidence RegistrationEvidence { get; }
}

/// <summary>
/// Non-business evidence that the host registered the module through its seam.
/// </summary>
public sealed record ModuleRegistrationEvidence(string ModuleKey, bool IsRegistered);
