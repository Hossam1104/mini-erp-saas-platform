using MiniErp.Contracts.Modules;
using MiniErp.Contracts.Modules.Platform;

namespace MiniErp.App.Modules.Platform.Internal;

internal sealed class PlatformAdministrationModule : IPlatformAdministrationModule
{
    private static readonly ModuleDescriptor Module = new(
        Key: "platform-administration",
        Name: "Platform Administration",
        Boundary: "Platform");

    public ModuleDescriptor Descriptor => Module;

    public ModuleRegistrationEvidence RegistrationEvidence => new(Module.Key, IsRegistered: true);
}
