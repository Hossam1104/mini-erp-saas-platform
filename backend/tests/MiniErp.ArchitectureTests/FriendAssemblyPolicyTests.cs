using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;
using MiniErp.App.BuildingBlocks.Work;
using Xunit;

namespace MiniErp.ArchitectureTests;

/// <summary>
/// H92-06 focused review correction. Making the durable-work ledger types
/// <c>internal</c> did not close the shipping boundary while
/// <c>MiniErp.App</c> granted <c>[InternalsVisibleTo("MiniErp.Api")]</c>: a
/// friend assembly sees another assembly's internals exactly as if they were
/// public. These tests assert the friend-assembly declaration itself (the
/// only thing item 3.5 of the C# access-modifier spec that a shipping
/// assembly can exploit) and then prove, by full compilation, that source
/// compiled as the shipping <c>MiniErp.Api</c> assembly can no longer resolve
/// the mutable ledger surface at all -- compiler-enforced, not a source-scan
/// convention.
/// </summary>
public sealed class FriendAssemblyPolicyTests
{
    [Fact]
    public void MiniErp_App_does_not_declare_InternalsVisibleTo_MiniErp_Api()
    {
        var friends = GetFriendAssemblyNames(typeof(DurableWorkLocalRuntime).Assembly);
        Assert.DoesNotContain("MiniErp.Api", friends, StringComparer.Ordinal);
    }

    [Fact]
    public void MiniErp_App_grants_friend_access_only_to_the_architecture_test_assembly()
    {
        var friends = GetFriendAssemblyNames(typeof(DurableWorkLocalRuntime).Assembly);
        Assert.Equal(["MiniErp.ArchitectureTests"], friends);
    }

    [Fact]
    public void No_non_test_assembly_receives_friend_access_to_MiniErp_App_internals()
    {
        var friends = GetFriendAssemblyNames(typeof(DurableWorkLocalRuntime).Assembly);
        Assert.All(friends, friend => Assert.Contains("Test", friend, StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Source_compiled_as_the_shipping_Api_assembly_cannot_access_the_internal_effect_guard_or_executor_types()
    {
        const string source = """
            using MiniErp.App.BuildingBlocks.Work;

            namespace MiniErp.Api.Attack;

            internal static class LedgerAccessAttempt
            {
                internal static object ConstructGuard() => new InMemoryDurableWorkEffectGuard();

                internal static object ConstructExecutor(IDurableWorkEffectGuard guard) => new DurableWorkEffectExecutor(guard);

                internal static void MutateLedger(IDurableWorkEffectGuard guard, DurableWorkEffectKey key)
                {
                    guard.TryReserve(key);
                    guard.Release(key);
                    guard.RecordOutcomeUnknown(key, "attack");
                    guard.GetOutcomeUnknownReason(key);
                }
            }
            """;

        var result = CompileAs("MiniErp.Api", source);

        Assert.False(result.Success, "Source compiled as MiniErp.Api must not be able to see MiniErp.App's internal ledger surface.");
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "CS0122");
    }

    [Fact]
    public void Source_compiled_as_the_approved_architecture_test_assembly_can_still_access_the_internal_ledger_for_testing()
    {
        const string source = """
            using MiniErp.App.BuildingBlocks.Work;

            namespace MiniErp.ArchitectureTests.Probe;

            internal static class LedgerAccessProbe
            {
                internal static object ConstructGuard() => new InMemoryDurableWorkEffectGuard();
            }
            """;

        var result = CompileAs("MiniErp.ArchitectureTests", source);

        Assert.True(
            result.Success,
            "Positive control failed -- " + string.Join(
                "; ",
                result.Diagnostics.Where(d => d.Severity == DiagnosticSeverity.Error).Select(d => d.ToString())));
    }

    private static IReadOnlyList<string> GetFriendAssemblyNames(Assembly assembly) =>
        assembly.GetCustomAttributes<InternalsVisibleToAttribute>()
            .Select(attribute => attribute.AssemblyName.Split(',')[0].Trim())
            .OrderBy(name => name, StringComparer.Ordinal)
            .ToArray();

    private static EmitResult CompileAs(string assemblyName, string source)
    {
        var syntaxTree = CSharpSyntaxTree.ParseText(source);
        var references = new[]
            {
                typeof(object).Assembly.Location,
                typeof(DurableWorkLocalRuntime).Assembly.Location,
            }
            .Concat(AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") is string trusted
                ? trusted.Split(Path.PathSeparator)
                : [])
            .Distinct()
            .Select(path => (MetadataReference)MetadataReference.CreateFromFile(path));

        var compilation = CSharpCompilation.Create(
            assemblyName,
            [syntaxTree],
            references,
            new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary));

        using var stream = new MemoryStream();
        return compilation.Emit(stream);
    }
}
