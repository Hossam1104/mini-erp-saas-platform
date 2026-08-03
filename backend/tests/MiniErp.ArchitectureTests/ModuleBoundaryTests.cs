using System.Collections;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using MiniErp.App.BuildingBlocks.Tenancy;
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
    public void Infrastructure_public_surface_has_no_unscoped_ef_shapes()
    {
        foreach (var type in InfrastructureAssembly.GetExportedTypes())
        {
            foreach (var constructor in type.GetConstructors(BindingFlags.Public | BindingFlags.Instance))
            {
                Assert.DoesNotContain(constructor.GetParameters(), parameter =>
                    IsUnscopedEfShape(parameter.ParameterType)
                    || IsTenantOverride(parameter)
                    || IsUnrestrictedPersistenceDelegate(parameter));
            }

            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
            {
                Assert.False(
                    IsUnscopedEfShape(method.ReturnType),
                    $"{type.FullName}.{method.Name} exposes an unscoped EF return type.");
                Assert.DoesNotContain(method.GetParameters(), parameter =>
                    IsUnscopedEfShape(parameter.ParameterType)
                    || IsTenantOverride(parameter)
                    || IsUnrestrictedPersistenceDelegate(parameter));
            }
        }
    }

    [Fact]
    public void Tenant_persistence_registration_owns_the_options_builder()
    {
        var method = typeof(TenantPersistenceServiceCollectionExtensions)
            .GetMethod(nameof(TenantPersistenceServiceCollectionExtensions.AddTenantPersistence));

        Assert.NotNull(method);
        var parameters = method!.GetParameters();
        Assert.DoesNotContain(parameters, parameter => parameter.ParameterType == typeof(DbContextOptions));
        Assert.Contains(parameters, parameter =>
            parameter.ParameterType == typeof(Action<DbContextOptionsBuilder>));
        Assert.DoesNotContain(
            typeof(TenantPersistenceSessionFactory).GetConstructors(BindingFlags.Public | BindingFlags.Instance),
            constructor => constructor.GetParameters().Any(parameter =>
                parameter.ParameterType == typeof(DbContextOptions)));
    }

    [Fact]
    public void Infrastructure_unscoped_ef_operations_are_allow_listed_to_stored_owner_verifier()
    {
        var repositoryRoot = FindRepositoryRoot();
        var sourceRoot = Path.Combine(
            repositoryRoot,
            "backend",
            "src",
            "MiniErp.Infrastructure");
        var forbiddenTokens = new[]
        {
            "IgnoreQueryFilters(",
            "ExecuteUpdate(",
            "ExecuteUpdateAsync(",
            "ExecuteDelete(",
            "ExecuteDeleteAsync(",
            "FromSqlRaw(",
            "FromSqlInterpolated("
        };
        var callSites = Directory.GetFiles(sourceRoot, "*.cs", SearchOption.AllDirectories)
            .Where(path => !path.Contains("\\bin\\", StringComparison.OrdinalIgnoreCase)
                && !path.Contains("\\obj\\", StringComparison.OrdinalIgnoreCase))
            .SelectMany(path => forbiddenTokens
                .Where(token => File.ReadAllText(path).Contains(token, StringComparison.Ordinal))
                .Select(token => (File: Path.GetFileName(path), Token: token)))
            .ToArray();

        Assert.Contains(callSites, callSite =>
            callSite.File == "TenantOwnershipStoreVerifier.cs"
            && callSite.Token == "IgnoreQueryFilters(");
        Assert.All(callSites, callSite =>
            Assert.Equal("TenantOwnershipStoreVerifier.cs", callSite.File));
    }

    private static bool IsUnscopedEfShape(Type type)
    {
        if (type.IsByRef || type.IsPointer || type.IsArray)
        {
            return IsUnscopedEfShape(type.GetElementType()!);
        }

        if (type == typeof(DbContext)
            || type == typeof(IQueryable))
        {
            return true;
        }

        if (type.IsGenericType)
        {
            var definition = type.GetGenericTypeDefinition();
            if (definition == typeof(DbSet<>) || definition == typeof(IQueryable<>))
            {
                return true;
            }

            return type.GetGenericArguments().Any(IsUnscopedEfShape);
        }

        return false;
    }

    private static bool IsTenantOverride(ParameterInfo parameter)
    {
        return ContainsTenantIdType(parameter.ParameterType)
            || (parameter.ParameterType == typeof(bool)
                && (parameter.Name?.Contains("ignore", StringComparison.OrdinalIgnoreCase) == true
                    || parameter.Name?.Contains("filter", StringComparison.OrdinalIgnoreCase) == true
                    || parameter.Name?.Contains("unscoped", StringComparison.OrdinalIgnoreCase) == true
                    || parameter.Name?.Contains("bypass", StringComparison.OrdinalIgnoreCase) == true));
    }

    private static bool ContainsTenantIdType(Type type)
    {
        if (type.IsByRef || type.IsPointer || type.IsArray)
        {
            return ContainsTenantIdType(type.GetElementType()!);
        }

        if (type == typeof(TenantId) || Nullable.GetUnderlyingType(type) == typeof(TenantId))
        {
            return true;
        }

        return type.IsGenericType && type.GetGenericArguments().Any(ContainsTenantIdType);
    }

    private static bool IsUnrestrictedPersistenceDelegate(ParameterInfo parameter)
    {
        if (!typeof(Delegate).IsAssignableFrom(parameter.ParameterType))
        {
            return false;
        }

        return parameter.ParameterType != typeof(Action<DbContextOptionsBuilder>);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (File.Exists(Path.Combine(
                directory.FullName,
                "backend",
                "src",
                "MiniErp.Infrastructure",
                "MiniErp.Infrastructure.csproj")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Repository root could not be located for architecture source checks.");
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
