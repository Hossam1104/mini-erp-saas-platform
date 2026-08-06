using System.Reflection;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.BuildingBlocks.Work;
using Xunit;

namespace MiniErp.ArchitectureTests;

/// <summary>
/// H92-03 focused review correction: exactly one shipping effect ledger must
/// be structurally reachable. <see cref="DurableWorkLocalRuntime"/> is the
/// only approved composition entry point for the guard, executor, store and
/// dispatcher, and no other shipping construction site may exist.
/// </summary>
public sealed class DurableWorkLocalRuntimeCompositionTests
{
    private static readonly DateTimeOffset Clock = new(2026, 8, 6, 0, 0, 0, TimeSpan.Zero);
    private static readonly DurableWorkOperationCatalogue OperationCatalogue =
        new([
            new DurableWorkOperationDescriptor(
                "foundation.runtime-composition",
                "runtime-composition-demo",
                "tenant.business.read",
                [TenantAuthorizationPath.OrdinaryMembership])
        ]);
    private static readonly DurableWorkPayloadRegistry PayloadRegistry = CreatePayloadRegistry();

    private static DurableWorkPayloadRegistry CreatePayloadRegistry()
    {
        var registry = new DurableWorkPayloadRegistry();
        registry.Register(new DurableWorkPayloadTypeId("test.runtime-composition-payload"), new JsonDurableWorkPayloadCodec<DemoPayload>());
        return registry;
    }

    // ---------------------------------------------------------------------
    // Structural enforcement
    // ---------------------------------------------------------------------

    [Fact]
    public void Runtime_store_and_dispatcher_reference_the_identical_executor_authority()
    {
        var runtime = DurableWorkLocalRuntime.Create(OperationCatalogue, PayloadRegistry);

        var storeExecutor = GetPrivateField(runtime.Store, "effectExecutor");
        var dispatcherExecutor = GetPrivateField(runtime.Dispatcher, "effectExecutor");

        Assert.Same(runtime.EffectExecutor, storeExecutor);
        Assert.Same(runtime.EffectExecutor, dispatcherExecutor);
    }

    [Fact]
    public void Each_composition_call_creates_one_independent_ledger()
    {
        var first = DurableWorkLocalRuntime.Create(OperationCatalogue, PayloadRegistry);
        var second = DurableWorkLocalRuntime.Create(OperationCatalogue, PayloadRegistry);

        Assert.NotSame(first.EffectGuard, second.EffectGuard);
        Assert.NotSame(first.EffectExecutor, second.EffectExecutor);
        Assert.NotSame(first.Store, second.Store);
        Assert.NotSame(first.Dispatcher, second.Dispatcher);
    }

    [Fact]
    public void No_public_constructor_permits_an_independent_store_effect_executor()
    {
        var publicConstructors = typeof(InMemoryDurableWorkStore).GetConstructors(BindingFlags.Public | BindingFlags.Instance);
        Assert.Empty(publicConstructors);
    }

    [Fact]
    public void No_public_constructor_permits_an_independent_dispatcher_effect_executor()
    {
        var publicConstructors = typeof(DurableWorkDispatcher).GetConstructors(BindingFlags.Public | BindingFlags.Instance);
        Assert.Empty(publicConstructors);
    }

    [Fact]
    public void No_public_constructor_permits_an_independent_guard_or_executor()
    {
        Assert.Empty(typeof(InMemoryDurableWorkEffectGuard).GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        Assert.Empty(typeof(DurableWorkEffectExecutor).GetConstructors(BindingFlags.Public | BindingFlags.Instance));
    }

    [Fact]
    public void Shipping_construction_of_the_ledger_types_occurs_only_inside_the_approved_composition()
    {
        var sourceRoot = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src"));
        var approvedPath = Path.GetFullPath(Path.Combine(
            sourceRoot, "MiniErp.App", "BuildingBlocks", "Work", "DurableWorkLocalRuntime.cs"));
        var restrictedTypeNames = new HashSet<string>(StringComparer.Ordinal)
        {
            nameof(InMemoryDurableWorkEffectGuard),
            nameof(DurableWorkEffectExecutor),
            nameof(InMemoryDurableWorkStore),
            nameof(DurableWorkDispatcher)
        };

        var violations = new List<string>();
        foreach (var path in Directory.GetFiles(sourceRoot, "*.cs", SearchOption.AllDirectories))
        {
            var fullPath = Path.GetFullPath(path);
            if (string.Equals(fullPath, approvedPath, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(path), path: path);
            var root = tree.GetRoot();
            foreach (var creation in root.DescendantNodes().OfType<ObjectCreationExpressionSyntax>())
            {
                var typeName = creation.Type switch
                {
                    IdentifierNameSyntax identifier => identifier.Identifier.ValueText,
                    QualifiedNameSyntax qualified => qualified.Right.Identifier.ValueText,
                    _ => null
                };

                if (typeName is not null && restrictedTypeNames.Contains(typeName))
                {
                    violations.Add($"{fullPath}: new {typeName}(...)");
                }
            }
        }

        Assert.True(
            violations.Count == 0,
            "Found shipping construction of a restricted durable-work ledger type outside DurableWorkLocalRuntime.cs: "
                + string.Join("; ", violations));
    }

    // ---------------------------------------------------------------------
    // One shared ledger across the composed store and dispatcher
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Handler_duplicate_across_runtime_calls_executes_once()
    {
        var runtime = DurableWorkLocalRuntime.Create(OperationCatalogue, PayloadRegistry);
        var handler = new CountingHandler();
        runtime.Dispatcher.Register(handler);
        var context = CreateContext();
        var work = Work(context, "runtime-handler-duplicate");
        Assert.True(await runtime.Store.SubmitAsync(work));

        var worker = new TenantDurableWorkWorker(runtime.Store, runtime.Dispatcher, new TestAuthorityRevalidator());
        var first = await worker.ProcessOneAsync(context, Guid.NewGuid(), Clock, TimeSpan.FromMinutes(5));
        Assert.Equal(DurableWorkLifecycle.Completed, first!.Lifecycle);

        var authority = await new TestAuthorityRevalidator().RevalidateAsync(work, context, Clock);
        var executionContext = new DurableWorkExecutionContext(work, authority.Authorization!);
        var duplicate = await runtime.Dispatcher.DispatchAsync(work, executionContext);

        Assert.True(duplicate.Success);
        Assert.Equal(1, handler.Calls);
    }

    [Fact]
    public async Task Outbox_duplicate_across_runtime_calls_executes_once()
    {
        var runtime = DurableWorkLocalRuntime.Create(OperationCatalogue, PayloadRegistry);
        var context = CreateContext();
        var work = Work(context, "runtime-outbox-duplicate");
        Assert.True(await runtime.Store.SubmitAsync(work));
        var effects = 0;
        Func<TenantOutboxMessage, VerifiedDurableWorkAuthorization, CancellationToken, ValueTask<DurableWorkProtectedEffectResult>> effect =
            (_, _, _) =>
            {
                effects++;
                return ValueTask.FromResult(DurableWorkProtectedEffectResult.Applied(DurableWorkHandlerResult.Succeeded()));
            };

        var first = await runtime.Store.DispatchOutboxAsync(context, new TestAuthorityRevalidator(), Clock, effect);
        Assert.True(first.Delivered);
        var replayed = await runtime.Store.DispatchOutboxAsync(context, new TestAuthorityRevalidator(), Clock, effect);

        Assert.False(replayed.Delivered);
        Assert.Equal(1, effects);
    }

    [Fact]
    public async Task Handler_and_outbox_effects_remain_independent_by_purpose_on_one_runtime()
    {
        var runtime = DurableWorkLocalRuntime.Create(OperationCatalogue, PayloadRegistry);
        var handler = new CountingHandler();
        runtime.Dispatcher.Register(handler);
        var context = CreateContext();
        var work = Work(context, "runtime-cross-purpose");
        Assert.True(await runtime.Store.SubmitAsync(work));
        var worker = new TenantDurableWorkWorker(runtime.Store, runtime.Dispatcher, new TestAuthorityRevalidator());

        var handlerCompletion = await worker.ProcessOneAsync(context, Guid.NewGuid(), Clock, TimeSpan.FromMinutes(5));
        Assert.Equal(DurableWorkLifecycle.Completed, handlerCompletion!.Lifecycle);
        Assert.Equal(1, handler.Calls);

        var outboxEffects = 0;
        var outboxResult = await runtime.Store.DispatchOutboxAsync(context, new TestAuthorityRevalidator(), Clock, (_, _, _) =>
        {
            outboxEffects++;
            return ValueTask.FromResult(DurableWorkProtectedEffectResult.Applied(DurableWorkHandlerResult.Succeeded()));
        });

        Assert.True(outboxResult.Delivered);
        Assert.False(outboxResult.Duplicate);
        Assert.Equal(1, outboxEffects);
    }

    [Fact]
    public async Task Same_event_id_redelivery_on_one_runtime_executes_once()
    {
        var runtime = DurableWorkLocalRuntime.Create(OperationCatalogue, PayloadRegistry);
        var context = CreateContext();
        var work = Work(context, "runtime-same-event-redelivery");
        Assert.True(await runtime.Store.SubmitAsync(work));
        var effects = 0;
        Func<TenantOutboxMessage, VerifiedDurableWorkAuthorization, CancellationToken, ValueTask<DurableWorkProtectedEffectResult>> effect =
            (_, _, _) =>
            {
                effects++;
                return ValueTask.FromResult(DurableWorkProtectedEffectResult.Applied(DurableWorkHandlerResult.Succeeded()));
            };

        await runtime.Store.DispatchOutboxAsync(context, new TestAuthorityRevalidator(), Clock, effect);
        Assert.True(runtime.Store.ReplayOutboxForValidation(work.Identity.WorkItemId, Clock));
        var replay = await runtime.Store.DispatchOutboxAsync(context, new TestAuthorityRevalidator(), Clock, effect);

        Assert.True(replay.Delivered);
        Assert.True(replay.Duplicate);
        Assert.Equal(1, effects);
    }

    [Fact]
    public async Task Different_event_ids_on_one_runtime_remain_independent()
    {
        var runtime = DurableWorkLocalRuntime.Create(OperationCatalogue, PayloadRegistry);
        var context = CreateContext();
        var firstWork = Work(context, "runtime-event-a");
        var secondWork = Work(context, "runtime-event-b");
        Assert.True(await runtime.Store.SubmitAsync(firstWork));
        Assert.True(await runtime.Store.SubmitAsync(secondWork));
        var effects = 0;
        Func<TenantOutboxMessage, VerifiedDurableWorkAuthorization, CancellationToken, ValueTask<DurableWorkProtectedEffectResult>> effect =
            (_, _, _) =>
            {
                effects++;
                return ValueTask.FromResult(DurableWorkProtectedEffectResult.Applied(DurableWorkHandlerResult.Succeeded()));
            };

        var firstResult = await runtime.Store.DispatchOutboxAsync(context, new TestAuthorityRevalidator(), Clock, effect);
        var secondResult = await runtime.Store.DispatchOutboxAsync(context, new TestAuthorityRevalidator(), Clock, effect);

        Assert.True(firstResult.Delivered);
        Assert.True(secondResult.Delivered);
        Assert.False(secondResult.Duplicate);
        Assert.Equal(2, effects);
    }

    // ---------------------------------------------------------------------
    // Test infrastructure
    // ---------------------------------------------------------------------

    private static object GetPrivateField(object instance, string fieldName)
    {
        var field = instance.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException($"Field {fieldName} was not found on {instance.GetType()}.");
        return field.GetValue(instance)!;
    }

    private static DurableWorkItem Work(TenantContext context, string key) =>
        DurableWorkItem.Create(
            context,
            DurableWorkTestSupport.TenantWideScope(context),
            Identity(context, key),
            new DemoPayload("payload"),
            PayloadRegistry,
            Guid.NewGuid(),
            3,
            Clock);

    private static DurableWorkIdentity Identity(TenantContext context, string key) =>
        DurableWorkIdentity.Create(
            Guid.NewGuid(),
            OperationCatalogue,
            "foundation.runtime-composition",
            context.CorrelationId!.Value,
            key);

    private static TenantContext CreateContext() =>
        TenantContext.ForOrdinaryMembership(
            new TenantId(Guid.NewGuid()),
            new MembershipReference(Guid.NewGuid()),
            new ScopeReference("tenant"),
            new CorrelationId($"corr-{Guid.NewGuid():N}"),
            Guid.NewGuid());

    private sealed record DemoPayload(string Value) : IWorkPayload;

    private sealed class CountingHandler : IDurableWorkHandler<DemoPayload>
    {
        internal int Calls { get; private set; }

        public DurableWorkOperationDescriptor Operation => OperationCatalogue.TryGet("foundation.runtime-composition", out var operation)
            ? operation
            : throw new InvalidOperationException("The test operation is not registered.");

        public ValueTask<DurableWorkProtectedEffectResult> ExecuteAsync(
            DemoPayload payload,
            DurableWorkExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            return ValueTask.FromResult(DurableWorkProtectedEffectResult.Applied(DurableWorkHandlerResult.Succeeded()));
        }
    }

    private sealed class TestAuthorityRevalidator : IDurableWorkAuthorityRevalidator
    {
        public ValueTask<DurableWorkAuthorityValidationResult> RevalidateAsync(
            DurableWorkItem workItem,
            TenantContext currentTenantContext,
            DateTimeOffset now,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(DurableWorkTestSupport.Approve(workItem, currentTenantContext));
    }
}
