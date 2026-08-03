using System.Reflection;
using MiniErp.App.Modules.Platform;
using MiniErp.Contracts.Modules.Platform;
using MiniErp.Infrastructure.Persistence;
using Xunit;

namespace MiniErp.ArchitectureTests;

public sealed class ModuleBoundaryTests
{
    private static readonly Assembly ContractsAssembly = typeof(IPlatformAdministrationModule).Assembly;
    private static readonly Assembly ApplicationAssembly = typeof(PlatformModuleRegistration).Assembly;
    private static readonly Assembly ApiAssembly = Assembly.Load("MiniErp.Api");
    private static readonly Assembly InfrastructureAssembly = typeof(TenantPersistenceSessionFactory).Assembly;

    [Fact]
    public void Contracts_do_not_reference_application()
    {
        Assert.DoesNotContain(
            ContractsAssembly.GetReferencedAssemblies(),
            reference => string.Equals(reference.Name, ApplicationAssembly.GetName().Name, StringComparison.Ordinal));
    }

    [Fact]
    public void Application_does_not_reference_api()
    {
        Assert.DoesNotContain(
            ApplicationAssembly.GetReferencedAssemblies(),
            reference => string.Equals(reference.Name, ApiAssembly.GetName().Name, StringComparison.Ordinal));
    }

    [Fact]
    public void Application_does_not_reference_infrastructure()
    {
        Assert.DoesNotContain(
            ApplicationAssembly.GetReferencedAssemblies(),
            reference => string.Equals(reference.Name, InfrastructureAssembly.GetName().Name, StringComparison.Ordinal));
    }

    [Fact]
    public void Contracts_do_not_reference_infrastructure()
    {
        Assert.DoesNotContain(
            ContractsAssembly.GetReferencedAssemblies(),
            reference => string.Equals(reference.Name, InfrastructureAssembly.GetName().Name, StringComparison.Ordinal));
    }

    [Fact]
    public void Infrastructure_does_not_reference_api()
    {
        Assert.DoesNotContain(
            InfrastructureAssembly.GetReferencedAssemblies(),
            reference => string.Equals(reference.Name, ApiAssembly.GetName().Name, StringComparison.Ordinal));
    }

    [Fact]
    public void Privileged_boundary_is_not_resolvable_from_public_infrastructure_surface()
    {
        Assert.DoesNotContain(
            InfrastructureAssembly.GetExportedTypes(),
            type => type.Name.Contains("PrivilegedPersistenceBoundary", StringComparison.Ordinal));
    }

    [Fact]
    public void Platform_internal_implementation_is_not_public()
    {
        var implementationType = ApplicationAssembly.GetType(
            "MiniErp.App.Modules.Platform.Internal.PlatformAdministrationModule");

        Assert.NotNull(implementationType);
        Assert.False(implementationType!.IsPublic);
    }

    [Fact]
    public void Api_surface_does_not_expose_platform_internal_types()
    {
        var exposedInternalTypes = ApiAssembly
            .GetExportedTypes()
            .Where(type => type.Namespace?.StartsWith(
                "MiniErp.App.Modules.Platform.Internal",
                StringComparison.Ordinal) == true)
            .ToArray();

        Assert.Empty(exposedInternalTypes);
    }

    [Fact]
    public void Composition_seam_returns_only_public_contract()
    {
        var module = PlatformModuleRegistration.Create();

        Assert.IsAssignableFrom<IPlatformAdministrationModule>(module);
        Assert.Equal("platform-administration", module.Descriptor.Key);
        Assert.True(module.RegistrationEvidence.IsRegistered);
    }

    [Fact]
    public void Known_project_dependency_graph_has_no_cycle()
    {
        var assemblies = new[] { ContractsAssembly, ApplicationAssembly, InfrastructureAssembly, ApiAssembly };
        var names = assemblies.ToDictionary(
            assembly => assembly.GetName().Name!,
            assembly => assembly);
        var graph = assemblies.ToDictionary(
            assembly => assembly.GetName().Name!,
            assembly => assembly
                .GetReferencedAssemblies()
                .Select(reference => reference.Name)
                .Where(referenceName => referenceName is not null && names.ContainsKey(referenceName))
                .Cast<string>()
                .ToArray());

        var visited = new HashSet<string>(StringComparer.Ordinal);
        var active = new HashSet<string>(StringComparer.Ordinal);

        foreach (var name in names.Keys)
        {
            Assert.False(HasCycle(name, graph, visited, active));
        }
    }

    private static bool HasCycle(
        string name,
        IReadOnlyDictionary<string, string[]> graph,
        ISet<string> visited,
        ISet<string> active)
    {
        if (active.Contains(name))
        {
            return true;
        }

        if (!visited.Add(name))
        {
            return false;
        }

        active.Add(name);
        foreach (var dependency in graph[name])
        {
            if (HasCycle(dependency, graph, visited, active))
            {
                return true;
            }
        }

        active.Remove(name);
        return false;
    }
}
