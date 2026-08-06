using System.Reflection;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.BuildingBlocks.Work;
using Xunit;

namespace MiniErp.ArchitectureTests;

/// <summary>
/// H92-05/M92-05 focused review correction. H92-05: the mutable effect ledger
/// (<see cref="IDurableWorkEffectGuard"/>) and its executor must never be
/// reachable from <see cref="DurableWorkLocalRuntime"/>'s public surface, so a
/// shipping caller cannot reserve, release, complete or mark an effect
/// uncertain outside the approved <see cref="DurableWorkEffectExecutor"/>.
/// M92-05: no public method may resolve uncertain-effect reason or state
/// evidence from a raw <see cref="DurableWorkEffectKey"/> alone; the only
/// publicly reachable uncertain-effect evidence path remains
/// <see cref="IDurableWorkStore.ReadUncertainEffectsAsync"/>, which requires a
/// <see cref="VerifiedDurableWorkReconciliationAuthorization"/>.
/// </summary>
public sealed class DurableWorkEffectLedgerSurfaceTests
{
    private static readonly Type[] InternalLedgerTypes =
    [
        typeof(IDurableWorkEffectGuard),
        typeof(IDurableWorkEffectExecutor),
        typeof(InMemoryDurableWorkEffectGuard),
        typeof(DurableWorkEffectExecutor)
    ];

    // ---------------------------------------------------------------------
    // H92-05: no public mutable ledger access
    // ---------------------------------------------------------------------

    [Fact]
    public void Runtime_exposes_no_public_EffectGuard_property()
    {
        Assert.Null(typeof(DurableWorkLocalRuntime).GetProperty("EffectGuard", BindingFlags.Public | BindingFlags.Instance));
    }

    [Fact]
    public void Runtime_exposes_no_public_EffectExecutor_property()
    {
        Assert.Null(typeof(DurableWorkLocalRuntime).GetProperty("EffectExecutor", BindingFlags.Public | BindingFlags.Instance));
    }

    [Fact]
    public void Runtime_public_instance_surface_is_limited_to_Store_and_Dispatcher()
    {
        var publicMemberNames = typeof(DurableWorkLocalRuntime)
            .GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly)
            .Select(member => member.Name)
            .ToArray();

        var allowed = new HashSet<string>(StringComparer.Ordinal)
        {
            nameof(DurableWorkLocalRuntime.Store),
            nameof(DurableWorkLocalRuntime.Dispatcher),
            "get_" + nameof(DurableWorkLocalRuntime.Store),
            "get_" + nameof(DurableWorkLocalRuntime.Dispatcher)
        };

        var unexpected = publicMemberNames.Where(name => !allowed.Contains(name)).ToArray();
        Assert.True(unexpected.Length == 0, "Unexpected public runtime member(s): " + string.Join(", ", unexpected));
    }

    [Fact]
    public void IDurableWorkEffectGuard_is_not_a_public_type()
    {
        Assert.False(typeof(IDurableWorkEffectGuard).IsPublic);
    }

    [Fact]
    public void IDurableWorkEffectExecutor_is_not_a_public_type()
    {
        Assert.False(typeof(IDurableWorkEffectExecutor).IsPublic);
    }

    [Fact]
    public void InMemoryDurableWorkEffectGuard_is_not_a_public_type()
    {
        Assert.False(typeof(InMemoryDurableWorkEffectGuard).IsPublic);
    }

    [Fact]
    public void DurableWorkEffectExecutor_is_not_a_public_type()
    {
        Assert.False(typeof(DurableWorkEffectExecutor).IsPublic);
    }

    [Fact]
    public void No_public_type_in_the_App_assembly_exposes_an_internal_ledger_type()
    {
        var violations = new List<string>();
        foreach (var type in typeof(DurableWorkLocalRuntime).Assembly.GetTypes().Where(t => t.IsPublic))
        {
            foreach (var property in type.GetProperties(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static))
            {
                if (InternalLedgerTypes.Contains(property.PropertyType))
                {
                    violations.Add($"{type.FullName}.{property.Name} (property)");
                }
            }

            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                if (method.IsSpecialName)
                {
                    continue;
                }

                if (InternalLedgerTypes.Contains(method.ReturnType)
                    || method.GetParameters().Any(parameter => InternalLedgerTypes.Contains(parameter.ParameterType)))
                {
                    violations.Add($"{type.FullName}.{method.Name} (method)");
                }
            }
        }

        Assert.True(violations.Count == 0, "Public exposure of an internal ledger type: " + string.Join(", ", violations));
    }

    [Fact]
    public void Store_and_dispatcher_still_share_the_identical_internal_guard()
    {
        var operationCatalogue = CreateCatalogue();
        var payloadRegistry = CreatePayloadRegistry();
        var runtime = DurableWorkLocalRuntime.Create(operationCatalogue, payloadRegistry);

        var storeExecutor = (IDurableWorkEffectExecutor)GetPrivateField(runtime.Store, "effectExecutor");
        var dispatcherExecutor = (IDurableWorkEffectExecutor)GetPrivateField(runtime.Dispatcher, "effectExecutor");
        var executorGuard = GetPrivateField(storeExecutor, "guard");

        Assert.Same(runtime.EffectGuard, executorGuard);
        Assert.Same(storeExecutor, dispatcherExecutor);
        Assert.Same(runtime.EffectExecutor, storeExecutor);
    }

    // ---------------------------------------------------------------------
    // M92-05: no raw-key public evidence path
    // ---------------------------------------------------------------------

    [Fact]
    public void No_public_method_in_the_App_assembly_resolves_uncertain_effect_evidence_from_a_raw_key_alone()
    {
        var violations = new List<string>();
        foreach (var type in typeof(DurableWorkLocalRuntime).Assembly.GetTypes().Where(t => t.IsPublic))
        {
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
            {
                if (method.IsSpecialName)
                {
                    continue;
                }

                var parameters = method.GetParameters();
                var onlyRawKey = parameters.Length == 1 && parameters[0].ParameterType == typeof(DurableWorkEffectKey);
                if (!onlyRawKey)
                {
                    continue;
                }

                var returnsReasonOrState = method.ReturnType == typeof(string) || method.ReturnType.IsEnum;
                if (returnsReasonOrState)
                {
                    violations.Add($"{type.FullName}.{method.Name}");
                }
            }
        }

        Assert.True(violations.Count == 0, "A public method accepts only a raw DurableWorkEffectKey and returns reason/state evidence: " + string.Join(", ", violations));
    }

    [Fact]
    public void GetOutcomeUnknownReason_is_not_declared_by_any_public_type()
    {
        var declaringTypes = typeof(DurableWorkLocalRuntime).Assembly.GetTypes()
            .Where(t => t.IsPublic)
            .Where(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly)
                .Any(m => m.Name == "GetOutcomeUnknownReason"))
            .ToArray();

        Assert.Empty(declaringTypes);
    }

    [Fact]
    public void ReadUncertainEffectsAsync_still_requires_verified_reconciliation_authorization_and_has_one_overload()
    {
        var methods = typeof(IDurableWorkStore).GetMethods(BindingFlags.Public | BindingFlags.Instance)
            .Where(m => m.Name == nameof(IDurableWorkStore.ReadUncertainEffectsAsync))
            .ToArray();

        var method = Assert.Single(methods);
        var firstParameter = method.GetParameters()[0];
        Assert.Equal(typeof(VerifiedDurableWorkReconciliationAuthorization), firstParameter.ParameterType);
    }

    [Fact]
    public async Task Internal_reason_is_preserved_and_inspectable_only_through_the_internal_test_only_seam()
    {
        var guard = new InMemoryDurableWorkEffectGuard();
        var key = DurableWorkEffectKey.ForHandlerEffect(new TenantId(Guid.NewGuid()), Guid.NewGuid(), "foundation.ledger-surface");
        guard.TryReserve(key);
        guard.RecordOutcomeUnknown(key, "reason_preserved_for_reconciliation");

        // The internal/test-only seam (IDurableWorkEffectGuard.GetOutcomeUnknownReason)
        // is reachable here only because this test project has InternalsVisibleTo
        // access to MiniErp.App; no public type exposes this method (see
        // GetOutcomeUnknownReason_is_not_declared_by_any_public_type above).
        Assert.Equal("reason_preserved_for_reconciliation", guard.GetOutcomeUnknownReason(key));
        await Task.CompletedTask;
    }

    // ---------------------------------------------------------------------
    // Attack-regression: a shipping caller cannot obtain the runtime guard
    // and release an in-flight reservation to force a second execution.
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Shipping_caller_cannot_reach_the_guard_to_release_an_in_flight_reservation_and_effect_still_runs_once()
    {
        var operationCatalogue = CreateCatalogue();
        var payloadRegistry = CreatePayloadRegistry();
        var runtime = DurableWorkLocalRuntime.Create(operationCatalogue, payloadRegistry);
        var handler = new BlockingHandler();
        runtime.Dispatcher.Register(handler);

        var context = TenantContext.ForOrdinaryMembership(
            new TenantId(Guid.NewGuid()),
            new MembershipReference(Guid.NewGuid()),
            new ScopeReference("tenant"),
            new CorrelationId($"corr-{Guid.NewGuid():N}"),
            Guid.NewGuid());
        var identity = DurableWorkIdentity.Create(
            Guid.NewGuid(),
            operationCatalogue,
            "foundation.ledger-surface",
            context.CorrelationId!.Value,
            "ledger-surface-attack");
        var work = DurableWorkItem.Create(
            context,
            DurableWorkTestSupport.TenantWideScope(context),
            identity,
            new DemoPayload("payload"),
            payloadRegistry,
            Guid.NewGuid(),
            3,
            DateTimeOffset.UtcNow);
        Assert.True(await runtime.Store.SubmitAsync(work));

        var authority = DurableWorkTestSupport.Approve(work, context);
        var executionContext = new DurableWorkExecutionContext(work, authority.Authorization!);

        // Start the dispatch; the effect reservation is now held (Reserved)
        // while the handler is deliberately blocked inside the executor
        // boundary, mirroring the window in the documented H92-05 attack.
        var dispatchTask = runtime.Dispatcher.DispatchAsync(work, executionContext);
        await handler.EntryReached.Task;

        // The previously-possible attack was `runtime.EffectGuard.Release(key)`.
        // That member no longer exists on the public type -- a caller limited
        // to DurableWorkLocalRuntime's public reflection surface (exactly what
        // an external assembly without InternalsVisibleTo would see) cannot
        // obtain the guard at all, so it has no way to release the
        // in-flight reservation early.
        var publiclyVisibleGuardProperty = typeof(DurableWorkLocalRuntime)
            .GetProperty("EffectGuard", BindingFlags.Public | BindingFlags.Instance);
        Assert.Null(publiclyVisibleGuardProperty);

        var publicMembersOfRuntimeInstance = runtime.GetType().GetMembers(BindingFlags.Public | BindingFlags.Instance);
        Assert.DoesNotContain(publicMembersOfRuntimeInstance, member =>
            member is PropertyInfo property && property.PropertyType == typeof(IDurableWorkEffectGuard));

        handler.AllowCompletion.SetResult(true);
        var firstResult = await dispatchTask;
        Assert.True(firstResult.Success);
        Assert.Equal(1, handler.Calls);

        // A duplicate dispatch after completion must still replay the single
        // recorded result rather than executing the effect again -- proving
        // the single-effect guarantee held throughout the in-flight window.
        var duplicate = await runtime.Dispatcher.DispatchAsync(work, executionContext);
        Assert.True(duplicate.Success);
        Assert.Equal(1, handler.Calls);
    }

    private static DurableWorkOperationCatalogue CreateCatalogue() =>
        new([
            new DurableWorkOperationDescriptor(
                "foundation.ledger-surface",
                "ledger-surface-demo",
                "tenant.business.read",
                [TenantAuthorizationPath.OrdinaryMembership])
        ]);

    private static DurableWorkPayloadRegistry CreatePayloadRegistry()
    {
        var registry = new DurableWorkPayloadRegistry();
        registry.Register(new DurableWorkPayloadTypeId("test.ledger-surface-payload"), new JsonDurableWorkPayloadCodec<DemoPayload>());
        return registry;
    }

    private static object GetPrivateField(object instance, string fieldName)
    {
        var field = instance.GetType().GetField(fieldName, BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException($"Field {fieldName} was not found on {instance.GetType()}.");
        return field.GetValue(instance)!;
    }

    private sealed record DemoPayload(string Value) : IWorkPayload;

    private sealed class BlockingHandler : IDurableWorkHandler<DemoPayload>
    {
        internal TaskCompletionSource<bool> EntryReached { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal TaskCompletionSource<bool> AllowCompletion { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

        internal int Calls { get; private set; }

        public DurableWorkOperationDescriptor Operation { get; } = new(
            "foundation.ledger-surface",
            "ledger-surface-demo",
            "tenant.business.read",
            [TenantAuthorizationPath.OrdinaryMembership]);

        public async ValueTask<DurableWorkProtectedEffectResult> ExecuteAsync(
            DemoPayload payload,
            DurableWorkExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            EntryReached.SetResult(true);
            await AllowCompletion.Task;
            return DurableWorkProtectedEffectResult.Applied(DurableWorkHandlerResult.Succeeded());
        }
    }
}
