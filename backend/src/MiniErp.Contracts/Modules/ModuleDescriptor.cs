namespace MiniErp.Contracts.Modules;

/// <summary>
/// Stable identity for a module exposed at the composition boundary.
/// </summary>
public sealed record ModuleDescriptor(string Key, string Name, string Boundary);
