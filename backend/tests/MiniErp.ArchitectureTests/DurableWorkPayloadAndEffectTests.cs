using System.Reflection;
using System.Text.Json;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.BuildingBlocks.Work;
using Xunit;

namespace MiniErp.ArchitectureTests;

/// <summary>
/// MESP-92: immutable payload envelopes (H-5), single-effect protection (H-6),
/// genuine concurrency evidence (M-2) and the renamed local store (L-1).
/// </summary>
public sealed class DurableWorkPayloadAndEffectTests
{
    private static readonly DateTimeOffset Clock = new(2026, 8, 6, 0, 0, 0, TimeSpan.Zero);
    private static readonly DurableWorkOperationCatalogue OperationCatalogue =
        new([
            new DurableWorkOperationDescriptor(
                "foundation.effect-work",
                "effect-demo",
                "tenant.business.read",
                [TenantAuthorizationPath.OrdinaryMembership])
        ]);
    private static readonly DurableWorkPayloadRegistry PayloadRegistry = CreatePayloadRegistry();

    private static DurableWorkPayloadRegistry CreatePayloadRegistry()
    {
        var registry = new DurableWorkPayloadRegistry();
        registry.Register(new DurableWorkPayloadTypeId("test.simple-payload"), new JsonDurableWorkPayloadCodec<SimplePayload>());
        registry.Register(new DurableWorkPayloadTypeId("test.mutable-string-payload"), new JsonDurableWorkPayloadCodec<MutableStringPayload>());
        registry.Register(new DurableWorkPayloadTypeId("test.byte-array-payload"), new JsonDurableWorkPayloadCodec<ByteArrayPayload>());
        registry.Register(new DurableWorkPayloadTypeId("test.list-payload"), new JsonDurableWorkPayloadCodec<ListPayload>());
        registry.Register(new DurableWorkPayloadTypeId("test.nested-mutable-payload"), new JsonDurableWorkPayloadCodec<NestedMutablePayload>());
        return registry;
    }

    // ---------------------------------------------------------------------
    // Architecture enforcement: a normal registered handler cannot bypass the
    // effect guard while still being treated as a protected durable-work
    // handler.
    // ---------------------------------------------------------------------

    [Fact]
    public void Handler_invocation_is_reachable_only_through_the_effect_executor()
    {
        var sourceRoot = Path.GetFullPath(Path.Combine(
            AppContext.BaseDirectory, "..", "..", "..", "..", "..", "src", "MiniErp.App"));
        var dispatcherPath = Path.GetFullPath(Path.Combine(
            sourceRoot, "BuildingBlocks", "Work", "InMemoryDurableWorkStore.cs"));

        var handlerExecuteSites = new List<(string Path, bool GuardedByExecutor)>();
        foreach (var path in Directory.GetFiles(sourceRoot, "*.cs", SearchOption.AllDirectories))
        {
            var tree = CSharpSyntaxTree.ParseText(File.ReadAllText(path), path: path);
            var root = tree.GetRoot();
            foreach (var invocation in root.DescendantNodes().OfType<InvocationExpressionSyntax>())
            {
                if (invocation.Expression is not MemberAccessExpressionSyntax member
                    || member.Name.Identifier.ValueText != "ExecuteAsync"
                    || member.Expression is not IdentifierNameSyntax identifier
                    || identifier.Identifier.ValueText != "handler")
                {
                    continue;
                }

                var isInsideExecutorLambda = invocation.Ancestors()
                    .OfType<ArgumentSyntax>()
                    .Any(argument => argument.Parent is ArgumentListSyntax { Parent: InvocationExpressionSyntax executorInvocation }
                        && executorInvocation.Expression is MemberAccessExpressionSyntax executorMember
                        && executorMember.Name.Identifier.ValueText == nameof(IDurableWorkEffectExecutor.ExecuteHandlerEffectAsync));

                handlerExecuteSites.Add((path, isInsideExecutorLambda));
            }
        }

        Assert.NotEmpty(handlerExecuteSites);
        Assert.All(handlerExecuteSites, site =>
            Assert.Equal(dispatcherPath, Path.GetFullPath(site.Path), StringComparer.OrdinalIgnoreCase));
        Assert.All(handlerExecuteSites, site => Assert.True(
            site.GuardedByExecutor,
            $"handler.ExecuteAsync at {site.Path} must be called only inside IDurableWorkEffectExecutor.ExecuteHandlerEffectAsync."));
    }

    // ---------------------------------------------------------------------
    // Payload immutability (H-5)
    // ---------------------------------------------------------------------

    [Fact]
    public void Original_mutable_payload_changed_after_submission_does_not_affect_stored_envelope()
    {
        var payload = new MutableStringPayload { Value = "original" };
        var envelope = PayloadRegistry.Capture(payload);
        payload.Value = "mutated-after-capture";

        var decoded = PayloadRegistry.Decode<MutableStringPayload>(envelope);
        Assert.Equal("original", decoded.Value);
    }

    [Fact]
    public void Original_byte_array_changed_after_submission_does_not_affect_stored_envelope()
    {
        var payload = new ByteArrayPayload { Data = [1, 2, 3] };
        var envelope = PayloadRegistry.Capture(payload);
        payload.Data[0] = 99;

        var decoded = PayloadRegistry.Decode<ByteArrayPayload>(envelope);
        Assert.Equal(1, decoded.Data[0]);
    }

    [Fact]
    public void Original_list_changed_after_submission_does_not_affect_stored_envelope()
    {
        var payload = new ListPayload { Items = ["a", "b"] };
        var envelope = PayloadRegistry.Capture(payload);
        payload.Items.Add("c");

        var decoded = PayloadRegistry.Decode<ListPayload>(envelope);
        Assert.Equal(["a", "b"], decoded.Items);
    }

    [Fact]
    public void Nested_mutable_object_changed_after_submission_does_not_affect_stored_envelope()
    {
        var payload = new NestedMutablePayload { Nested = new NestedMutable { Detail = "original" } };
        var envelope = PayloadRegistry.Capture(payload);
        payload.Nested.Detail = "mutated";

        var decoded = PayloadRegistry.Decode<NestedMutablePayload>(envelope);
        Assert.Equal("original", decoded.Nested.Detail);
    }

    [Fact]
    public void Returned_envelope_bytes_mutated_does_not_affect_stored_envelope()
    {
        var payload = new SimplePayload("stable");
        var envelope = PayloadRegistry.Capture(payload);
        var copy = envelope.GetBytesCopy();
        copy[0] ^= 0xFF;

        var decoded = PayloadRegistry.Decode<SimplePayload>(envelope);
        Assert.Equal("stable", decoded.Value);
        var secondCopy = envelope.GetBytesCopy();
        Assert.NotEqual(copy, secondCopy);
    }

    [Fact]
    public void Handler_decoded_payload_mutated_does_not_affect_stored_envelope()
    {
        var payload = new MutableStringPayload { Value = "decoded" };
        var envelope = PayloadRegistry.Capture(payload);
        var firstDecode = PayloadRegistry.Decode<MutableStringPayload>(envelope);
        firstDecode.Value = "mutated-after-decode";

        var secondDecode = PayloadRegistry.Decode<MutableStringPayload>(envelope);
        Assert.Equal("decoded", secondDecode.Value);
    }

    [Fact]
    public void Repeated_decode_returns_independent_instances()
    {
        var payload = new ListPayload { Items = ["x"] };
        var envelope = PayloadRegistry.Capture(payload);
        var first = PayloadRegistry.Decode<ListPayload>(envelope);
        var second = PayloadRegistry.Decode<ListPayload>(envelope);

        Assert.NotSame(first, second);
        Assert.NotSame(first.Items, second.Items);
        first.Items.Add("y");
        Assert.Single(second.Items);
    }

    [Fact]
    public void Checksum_mismatch_fails_closed()
    {
        var envelope = PayloadRegistry.Capture(new SimplePayload("checksum"));
        CorruptStoredBytesForTest(envelope);

        Assert.Throws<DurableWorkPayloadException>(() => PayloadRegistry.Decode<SimplePayload>(envelope));
    }

    [Fact]
    public void Unknown_payload_type_fails_closed()
    {
        var isolatedRegistry = new DurableWorkPayloadRegistry();
        isolatedRegistry.Register(new DurableWorkPayloadTypeId("test.isolated-only"), new JsonDurableWorkPayloadCodec<SimplePayload>());
        var envelope = isolatedRegistry.Capture(new SimplePayload("isolated"));

        var otherRegistry = new DurableWorkPayloadRegistry();
        Assert.Throws<DurableWorkPayloadException>(() => otherRegistry.Decode<SimplePayload>(envelope));
    }

    [Fact]
    public void Payload_handler_type_mismatch_fails_closed()
    {
        var envelope = PayloadRegistry.Capture(new SimplePayload("mismatch"));
        Assert.Throws<DurableWorkPayloadException>(() => PayloadRegistry.Decode<MutableStringPayload>(envelope));
    }

    [Fact]
    public void Oversized_payload_fails_closed()
    {
        var oversized = new MutableStringPayload { Value = new string('x', DurableWorkPayloadEnvelope.MaxPayloadSizeBytes) };
        Assert.Throws<DurableWorkPayloadException>(() => PayloadRegistry.Capture(oversized));
    }

    [Fact]
    public async Task Payload_is_absent_from_audit_evidence()
    {
        var context = CreateContext();
        var store = new InMemoryDurableWorkStore();
        var secretMarker = $"payload-secret-{Guid.NewGuid():N}";
        await store.SubmitAsync(Work(context, "payload-audit", secretMarker));

        var auditJson = JsonSerializer.Serialize(await store.ReadAuditAsync(context));
        Assert.DoesNotContain(secretMarker, auditJson, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------
    // Effect single-execution semantics (H-6)
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Normal_effect_executes_exactly_once()
    {
        var executor = NewExecutor();
        var key = NewKey();
        var invocations = 0;

        var result = await executor.ExecuteHandlerEffectAsync(key, ct =>
        {
            Interlocked.Increment(ref invocations);
            return ValueTask.FromResult(DurableWorkProtectedEffectResult.Applied(DurableWorkHandlerResult.Succeeded()));
        });

        Assert.Equal(DurableWorkEffectExecutionKind.Executed, result.Kind);
        Assert.True(result.Result.Success);
        Assert.Equal(1, invocations);
    }

    [Fact]
    public async Task Completed_duplicate_does_not_repeat_the_effect()
    {
        var executor = NewExecutor();
        var key = NewKey();
        var invocations = 0;
        Func<CancellationToken, ValueTask<DurableWorkProtectedEffectResult>> effect = ct =>
        {
            Interlocked.Increment(ref invocations);
            return ValueTask.FromResult(DurableWorkProtectedEffectResult.Applied(DurableWorkHandlerResult.Succeeded()));
        };

        var first = await executor.ExecuteHandlerEffectAsync(key, effect);
        var second = await executor.ExecuteHandlerEffectAsync(key, effect);

        Assert.Equal(DurableWorkEffectExecutionKind.Executed, first.Kind);
        Assert.Equal(DurableWorkEffectExecutionKind.Replayed, second.Kind);
        Assert.Equal(1, invocations);
    }

    [Fact]
    public async Task Cancellation_before_effect_permits_retry()
    {
        var guard = new InMemoryDurableWorkEffectGuard();
        var executor = new DurableWorkEffectExecutor(guard);
        var key = NewKey();
        var invocations = 0;
        using var preCancelled = new CancellationTokenSource();
        preCancelled.Cancel();

        await Assert.ThrowsAsync<OperationCanceledException>(() => executor.ExecuteHandlerEffectAsync(
            key,
            ct =>
            {
                Interlocked.Increment(ref invocations);
                return ValueTask.FromResult(DurableWorkProtectedEffectResult.Applied(DurableWorkHandlerResult.Succeeded()));
            },
            preCancelled.Token).AsTask());

        Assert.Equal(DurableWorkEffectState.NotStarted, guard.GetState(key));
        Assert.Equal(0, invocations);

        var retry = await executor.ExecuteHandlerEffectAsync(key, ct =>
        {
            Interlocked.Increment(ref invocations);
            return ValueTask.FromResult(DurableWorkProtectedEffectResult.Applied(DurableWorkHandlerResult.Succeeded()));
        });

        Assert.Equal(DurableWorkEffectExecutionKind.Executed, retry.Kind);
        Assert.Equal(1, invocations);
    }

    [Fact]
    public async Task Cancellation_after_the_effect_boundary_does_not_repeat()
    {
        var executor = NewExecutor();
        var key = NewKey();
        var invocations = 0;

        var first = await executor.ExecuteHandlerEffectAsync(key, async ct =>
        {
            Interlocked.Increment(ref invocations);
            await Task.Yield();
            throw new OperationCanceledException("observed mid-effect, after the reservation boundary");
        });

        Assert.Equal(DurableWorkEffectExecutionKind.OutcomeUnknown, first.Kind);

        var second = await executor.ExecuteHandlerEffectAsync(key, ct =>
        {
            Interlocked.Increment(ref invocations);
            return ValueTask.FromResult(DurableWorkProtectedEffectResult.Applied(DurableWorkHandlerResult.Succeeded()));
        });

        Assert.Equal(DurableWorkEffectExecutionKind.OutcomeUnknown, second.Kind);
        Assert.Equal(1, invocations);
    }

    [Fact]
    public async Task Completion_recording_failure_yields_outcome_unknown_and_blocks_repeat()
    {
        var guard = new FaultInjectingEffectGuard(new InMemoryDurableWorkEffectGuard(), failOnRecordCompleted: true);
        var executor = new DurableWorkEffectExecutor(guard);
        var key = NewKey();
        var invocations = 0;
        Func<CancellationToken, ValueTask<DurableWorkProtectedEffectResult>> effect = ct =>
        {
            Interlocked.Increment(ref invocations);
            return ValueTask.FromResult(DurableWorkProtectedEffectResult.Applied(DurableWorkHandlerResult.Succeeded()));
        };

        var first = await executor.ExecuteHandlerEffectAsync(key, effect);
        Assert.Equal(DurableWorkEffectExecutionKind.OutcomeUnknown, first.Kind);

        var second = await executor.ExecuteHandlerEffectAsync(key, effect);
        Assert.Equal(DurableWorkEffectExecutionKind.OutcomeUnknown, second.Kind);
        Assert.Equal(1, invocations);
        Assert.Equal(DurableWorkEffectState.OutcomeUnknown, guard.GetState(key));
    }

    [Fact]
    public async Task Lease_recovery_after_a_completed_effect_does_not_repeat()
    {
        var executor = NewExecutor();
        var key = NewKey();
        var invocations = 0;
        Func<CancellationToken, ValueTask<DurableWorkProtectedEffectResult>> effect = ct =>
        {
            Interlocked.Increment(ref invocations);
            return ValueTask.FromResult(DurableWorkProtectedEffectResult.Applied(DurableWorkHandlerResult.Succeeded()));
        };

        var beforeReclaim = await executor.ExecuteHandlerEffectAsync(key, effect);
        // Simulate a worker reclaiming the lease after the item was already
        // marked Completed and re-attempting dispatch for the same effect key.
        var afterReclaim = await executor.ExecuteHandlerEffectAsync(key, effect);

        Assert.Equal(DurableWorkEffectExecutionKind.Executed, beforeReclaim.Kind);
        Assert.Equal(DurableWorkEffectExecutionKind.Replayed, afterReclaim.Kind);
        Assert.Equal(1, invocations);
    }

    [Fact]
    public async Task Outcome_unknown_is_never_automatically_executed()
    {
        var guard = new FaultInjectingEffectGuard(new InMemoryDurableWorkEffectGuard(), failOnRecordCompleted: true);
        var executor = new DurableWorkEffectExecutor(guard);
        var key = NewKey();
        await executor.ExecuteHandlerEffectAsync(key, _ => ValueTask.FromResult(DurableWorkProtectedEffectResult.Applied(DurableWorkHandlerResult.Succeeded())));
        Assert.Equal(DurableWorkEffectState.OutcomeUnknown, guard.GetState(key));

        var invoked = false;
        var blocked = await executor.ExecuteHandlerEffectAsync(key, _ =>
        {
            invoked = true;
            return ValueTask.FromResult(DurableWorkProtectedEffectResult.Applied(DurableWorkHandlerResult.Succeeded()));
        });

        Assert.False(invoked);
        Assert.Equal(DurableWorkEffectExecutionKind.OutcomeUnknown, blocked.Kind);
        Assert.Equal(DurableWorkFailureCategory.Unknown, blocked.Result.FailureCategory);
        Assert.True(blocked.Result.IsOutcomeUnknown);
    }

    [Fact]
    public async Task Duplicate_dispatch_replays_the_exact_safe_result()
    {
        var executor = NewExecutor();
        var key = NewKey();
        var first = await executor.ExecuteHandlerEffectAsync(key, _ =>
            ValueTask.FromResult(DurableWorkProtectedEffectResult.TerminalNotApplied(DurableWorkFailureCategory.HandlerFailed, "safe_terminal_reason")));

        var second = await executor.ExecuteHandlerEffectAsync(key, _ =>
            throw new InvalidOperationException("must not be invoked on replay"));

        Assert.Equal(DurableWorkEffectExecutionKind.Replayed, second.Kind);
        Assert.Equal(first.Result.Success, second.Result.Success);
        Assert.Equal(first.Result.DeadLetter, second.Result.DeadLetter);
        Assert.Equal(first.Result.FailureCategory, second.Result.FailureCategory);
        Assert.Equal(first.Result.SafeReason, second.Result.SafeReason);
    }

    [Fact]
    public async Task Cross_tenant_effect_keys_are_isolated()
    {
        var executor = NewExecutor();
        var workItemId = Guid.NewGuid();
        var keyA = DurableWorkEffectKey.ForHandlerEffect(new TenantId(Guid.NewGuid()), workItemId, "foundation.effect-work");
        var keyB = DurableWorkEffectKey.ForHandlerEffect(new TenantId(Guid.NewGuid()), workItemId, "foundation.effect-work");
        var invocations = 0;
        Func<CancellationToken, ValueTask<DurableWorkProtectedEffectResult>> effect = ct =>
        {
            Interlocked.Increment(ref invocations);
            return ValueTask.FromResult(DurableWorkProtectedEffectResult.Applied(DurableWorkHandlerResult.Succeeded()));
        };

        var resultA = await executor.ExecuteHandlerEffectAsync(keyA, effect);
        var resultB = await executor.ExecuteHandlerEffectAsync(keyB, effect);

        Assert.Equal(DurableWorkEffectExecutionKind.Executed, resultA.Kind);
        Assert.Equal(DurableWorkEffectExecutionKind.Executed, resultB.Kind);
        Assert.Equal(2, invocations);
    }

    [Fact]
    public async Task Cross_operation_effect_keys_are_isolated()
    {
        var executor = NewExecutor();
        var tenantId = new TenantId(Guid.NewGuid());
        var workItemId = Guid.NewGuid();
        var keyA = DurableWorkEffectKey.ForHandlerEffect(tenantId, workItemId, "foundation.effect-work");
        var keyB = DurableWorkEffectKey.ForHandlerEffect(tenantId, workItemId, "foundation.other-effect-work");
        var invocations = 0;
        Func<CancellationToken, ValueTask<DurableWorkProtectedEffectResult>> effect = ct =>
        {
            Interlocked.Increment(ref invocations);
            return ValueTask.FromResult(DurableWorkProtectedEffectResult.Applied(DurableWorkHandlerResult.Succeeded()));
        };

        var resultA = await executor.ExecuteHandlerEffectAsync(keyA, effect);
        var resultB = await executor.ExecuteHandlerEffectAsync(keyB, effect);

        Assert.Equal(DurableWorkEffectExecutionKind.Executed, resultA.Kind);
        Assert.Equal(DurableWorkEffectExecutionKind.Executed, resultB.Kind);
        Assert.Equal(2, invocations);
    }

    [Fact]
    public async Task Cross_purpose_effect_keys_are_isolated_on_one_shared_guard()
    {
        // A single shared guard is the application's one authoritative effect
        // ledger (H92-01). Handler and outbox purposes for the identical
        // Tenant, work item and operation must remain independent within it;
        // if the purpose were dropped from the key, the second call would
        // incorrectly observe AlreadyCompleted/Replayed instead of Executed.
        var guard = new InMemoryDurableWorkEffectGuard();
        var executor = new DurableWorkEffectExecutor(guard);
        var tenantId = new TenantId(Guid.NewGuid());
        var workItemId = Guid.NewGuid();
        const string operationId = "foundation.effect-work";
        var handlerKey = DurableWorkEffectKey.ForHandlerEffect(tenantId, workItemId, operationId);
        var outboxKey = DurableWorkEffectKey.ForOutboxEffect(tenantId, workItemId, operationId, Guid.NewGuid());
        var invocations = 0;
        Func<CancellationToken, ValueTask<DurableWorkProtectedEffectResult>> effect = ct =>
        {
            Interlocked.Increment(ref invocations);
            return ValueTask.FromResult(DurableWorkProtectedEffectResult.Applied(DurableWorkHandlerResult.Succeeded()));
        };

        var handlerResult = await executor.ExecuteHandlerEffectAsync(handlerKey, effect);
        var outboxResult = await executor.ExecuteHandlerEffectAsync(outboxKey, effect);

        Assert.Equal(DurableWorkEffectExecutionKind.Executed, handlerResult.Kind);
        Assert.Equal(DurableWorkEffectExecutionKind.Executed, outboxResult.Kind);
        Assert.Equal(2, invocations);
        Assert.Equal(DurableWorkEffectState.Completed, guard.GetState(handlerKey));
        Assert.Equal(DurableWorkEffectState.Completed, guard.GetState(outboxKey));
    }

    [Fact]
    public void Outbox_effect_key_requires_a_non_empty_event_identifier()
    {
        Assert.Throws<ArgumentException>(() => DurableWorkEffectKey.ForOutboxEffect(
            new TenantId(Guid.NewGuid()),
            Guid.NewGuid(),
            "foundation.effect-work",
            Guid.Empty));
    }

    [Fact]
    public void Different_outbox_event_ids_for_the_same_work_item_are_independent_keys()
    {
        var tenantId = new TenantId(Guid.NewGuid());
        var workItemId = Guid.NewGuid();
        var keyA = DurableWorkEffectKey.ForOutboxEffect(tenantId, workItemId, "foundation.effect-work", Guid.NewGuid());
        var keyB = DurableWorkEffectKey.ForOutboxEffect(tenantId, workItemId, "foundation.effect-work", Guid.NewGuid());

        Assert.NotEqual(keyA, keyB);
    }

    [Fact]
    public void Same_outbox_event_id_redelivery_resolves_to_the_same_key()
    {
        var tenantId = new TenantId(Guid.NewGuid());
        var workItemId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var keyA = DurableWorkEffectKey.ForOutboxEffect(tenantId, workItemId, "foundation.effect-work", eventId);
        var keyB = DurableWorkEffectKey.ForOutboxEffect(tenantId, workItemId, "foundation.effect-work", eventId);

        Assert.Equal(keyA, keyB);
    }

    // ---------------------------------------------------------------------
    // Genuine concurrency evidence (M-2)
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Two_real_concurrent_claimers_produce_one_winner()
    {
        var context = CreateContext();
        var store = new InMemoryDurableWorkStore();
        await store.SubmitAsync(Work(context, "concurrent-claim"));

        var results = await RunWithBarrierAsync(8, () => store.TryClaimAsync(context, Guid.NewGuid(), Clock, TimeSpan.FromMinutes(5)).AsTask());

        Assert.Equal(1, results.Count(lease => lease is not null));
    }

    [Fact]
    public async Task Two_concurrent_dispatchers_reserve_exactly_one_effect()
    {
        var executor = NewExecutor();
        var key = NewKey();
        var invocations = 0;

        var results = await RunWithBarrierAsync(8, async () =>
        {
            var execution = await executor.ExecuteHandlerEffectAsync(key, async ct =>
            {
                Interlocked.Increment(ref invocations);
                await Task.Delay(15, ct);
                return DurableWorkProtectedEffectResult.Applied(DurableWorkHandlerResult.Succeeded());
            });
            return execution.Kind;
        });

        Assert.Equal(1, invocations);
        Assert.Equal(1, results.Count(kind => kind == DurableWorkEffectExecutionKind.Executed));
    }

    [Fact]
    public async Task Active_lease_blocks_every_concurrent_contender()
    {
        var context = CreateContext();
        var store = new InMemoryDurableWorkStore();
        await store.SubmitAsync(Work(context, "active-lease-contention"));
        var first = await store.TryClaimAsync(context, Guid.NewGuid(), Clock, TimeSpan.FromMinutes(5));
        Assert.NotNull(first);

        var results = await RunWithBarrierAsync(8, () => store.TryClaimAsync(context, Guid.NewGuid(), Clock, TimeSpan.FromMinutes(5)).AsTask());

        Assert.All(results, Assert.Null);
    }

    [Fact]
    public async Task Expired_lease_reclaim_has_exactly_one_winner()
    {
        var context = CreateContext();
        var store = new InMemoryDurableWorkStore();
        await store.SubmitAsync(Work(context, "expired-lease-reclaim"));
        await store.TryClaimAsync(context, Guid.NewGuid(), Clock, TimeSpan.FromMinutes(1));
        await store.ExpireLeasesAsync(Clock.AddMinutes(2));

        var results = await RunWithBarrierAsync(
            8,
            () => store.TryClaimAsync(context, Guid.NewGuid(), Clock.AddMinutes(2), TimeSpan.FromMinutes(1)).AsTask());

        Assert.Equal(1, results.Count(lease => lease is not null));
    }

    [Fact]
    public async Task Stale_completion_after_reclaim_is_rejected()
    {
        var context = CreateContext();
        var store = new InMemoryDurableWorkStore();
        await store.SubmitAsync(Work(context, "stale-completion"));
        var staleLease = await store.TryClaimAsync(context, Guid.NewGuid(), Clock, TimeSpan.FromMinutes(1));
        Assert.NotNull(staleLease);
        await store.ExpireLeasesAsync(Clock.AddMinutes(2));
        var freshLease = await store.TryClaimAsync(context, Guid.NewGuid(), Clock.AddMinutes(2), TimeSpan.FromMinutes(1));
        Assert.NotNull(freshLease);

        var staleResult = await store.CompleteAsync(context, staleLease!, DurableWorkHandlerResult.Succeeded(), Clock.AddMinutes(2));

        Assert.False(staleResult.Accepted);
        Assert.Equal(DurableWorkFailureCategory.ConcurrencyConflict, staleResult.FailureCategory);
    }

    [Fact]
    public async Task Concurrent_duplicate_submissions_produce_one_effect()
    {
        var context = CreateContext();
        var store = new InMemoryDurableWorkStore();
        var identity = Identity(context, "concurrent-duplicate-submit");
        var items = Enumerable.Range(0, 8)
            .Select(_ => DurableWorkItem.Create(
                context,
                DurableWorkTestSupport.TenantWideScope(context),
                identity,
                new SimplePayload("dup"),
                PayloadRegistry,
                Guid.NewGuid(),
                3,
                Clock))
            .ToArray();

        var submitResults = await RunWithBarrierAsync(items.Length, index => store.SubmitAsync(items[index]).AsTask());
        Assert.All(submitResults, Assert.True);

        var submittedAuditCount = (await store.ReadAuditAsync(context)).Count(record => record.EventType == "work.submitted");
        Assert.Equal(1, submittedAuditCount);

        var effects = 0;
        var dispatchResult = await store.DispatchOutboxAsync(context, new TestAuthorityRevalidator(), Clock, (_, _, _) =>
        {
            Interlocked.Increment(ref effects);
            return ValueTask.FromResult(DurableWorkProtectedEffectResult.Applied(DurableWorkHandlerResult.Succeeded()));
        });

        Assert.True(dispatchResult.Delivered);
        Assert.Equal(1, effects);
    }

    // ---------------------------------------------------------------------
    // Outbox explicit Applied / NotAppliedRetryable / OutcomeUnknown / TerminalNotApplied outcomes
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Applied_outbox_delivery_is_never_repeated()
    {
        var context = CreateContext();
        var store = new InMemoryDurableWorkStore();
        var work = Work(context, "outbox-applied-once");
        await store.SubmitAsync(work);
        var effects = 0;

        var result = await store.DispatchOutboxAsync(context, new TestAuthorityRevalidator(), Clock, (_, _, _) =>
        {
            effects++;
            return ValueTask.FromResult(DurableWorkProtectedEffectResult.Applied(DurableWorkHandlerResult.Succeeded()));
        });

        Assert.True(result.Delivered);
        Assert.False(result.OutcomeUnknown);
        Assert.False(result.RetryScheduled);
        Assert.Equal(1, effects);

        var next = await store.DispatchOutboxAsync(context, new TestAuthorityRevalidator(), Clock, (_, _, _) =>
        {
            effects++;
            return ValueTask.FromResult(DurableWorkProtectedEffectResult.Applied(DurableWorkHandlerResult.Succeeded()));
        });

        Assert.False(next.Delivered);
        Assert.Equal(1, effects);
    }

    [Fact]
    public async Task Proven_not_applied_outcome_may_retry_and_later_succeed()
    {
        var context = CreateContext();
        var store = new InMemoryDurableWorkStore();
        var work = Work(context, "outbox-not-applied-retry");
        await store.SubmitAsync(work);
        var effects = 0;

        var failed = await store.DispatchOutboxAsync(
            context,
            new ControlledAuthorityRevalidator(AuthorityBehavior.ProviderUnavailable),
            Clock,
            (_, _, _) =>
            {
                effects++;
                return ValueTask.FromResult(DurableWorkProtectedEffectResult.Applied(DurableWorkHandlerResult.Succeeded()));
            });

        Assert.True(failed.RetryScheduled);
        Assert.False(failed.OutcomeUnknown);
        Assert.Equal(0, effects);

        var succeeded = await store.DispatchOutboxAsync(
            context,
            new TestAuthorityRevalidator(),
            Clock.AddMinutes(1),
            (_, _, _) =>
            {
                effects++;
                return ValueTask.FromResult(DurableWorkProtectedEffectResult.Applied(DurableWorkHandlerResult.Succeeded()));
            });

        Assert.True(succeeded.Delivered);
        Assert.Equal(1, effects);
    }

    [Fact]
    public async Task Outbox_provider_explicit_not_applied_retryable_releases_and_later_succeeds()
    {
        var context = CreateContext();
        var store = new InMemoryDurableWorkStore();
        var work = Work(context, "outbox-provider-not-applied-retry");
        await store.SubmitAsync(work);
        var effects = 0;

        var failed = await store.DispatchOutboxAsync(context, new TestAuthorityRevalidator(), Clock, (_, _, _) =>
        {
            effects++;
            return ValueTask.FromResult(DurableWorkProtectedEffectResult.NotAppliedRetryable(
                DurableWorkFailureCategory.ProviderUnavailable,
                TimeSpan.FromSeconds(1),
                "provider_reported_not_applied"));
        });

        Assert.True(failed.RetryScheduled);
        Assert.False(failed.OutcomeUnknown);
        Assert.False(failed.DeadLettered);
        Assert.Equal(1, effects);

        var succeeded = await store.DispatchOutboxAsync(context, new TestAuthorityRevalidator(), Clock.AddSeconds(2), (_, _, _) =>
        {
            effects++;
            return ValueTask.FromResult(DurableWorkProtectedEffectResult.Applied(DurableWorkHandlerResult.Succeeded()));
        });

        Assert.True(succeeded.Delivered);
        Assert.Equal(2, effects);
    }

    [Fact]
    public async Task Outbox_provider_explicit_terminal_not_applied_dead_letters_immediately()
    {
        var context = CreateContext();
        var store = new InMemoryDurableWorkStore();
        var work = Work(context, "outbox-provider-terminal-not-applied");
        await store.SubmitAsync(work);
        var effects = 0;

        var result = await store.DispatchOutboxAsync(context, new TestAuthorityRevalidator(), Clock, (_, _, _) =>
        {
            effects++;
            return ValueTask.FromResult(DurableWorkProtectedEffectResult.TerminalNotApplied(
                DurableWorkFailureCategory.ValidationFailed,
                "provider_reported_terminal_not_applied"));
        });

        Assert.True(result.DeadLettered);
        Assert.False(result.RetryScheduled);
        Assert.False(result.OutcomeUnknown);
        Assert.Equal(1, effects);

        // A terminal outcome is completed in the guard and must never repeat,
        // even though the attempt-count-based retry budget was never spent.
        var next = await store.DispatchOutboxAsync(context, new TestAuthorityRevalidator(), Clock.AddMinutes(1), (_, _, _) =>
        {
            effects++;
            return ValueTask.FromResult(DurableWorkProtectedEffectResult.Applied(DurableWorkHandlerResult.Succeeded()));
        });

        Assert.False(next.Delivered);
        Assert.Equal(1, effects);
    }

    [Fact]
    public async Task Outbox_provider_explicit_outcome_unknown_is_never_automatically_repeated()
    {
        var context = CreateContext();
        var store = new InMemoryDurableWorkStore();
        var work = Work(context, "outbox-provider-outcome-unknown");
        await store.SubmitAsync(work);
        var effects = 0;

        var result = await store.DispatchOutboxAsync(context, new TestAuthorityRevalidator(), Clock, (_, _, _) =>
        {
            effects++;
            return ValueTask.FromResult(DurableWorkProtectedEffectResult.OutcomeUnknown("provider_reported_outcome_unknown"));
        });

        Assert.True(result.OutcomeUnknown);
        Assert.False(result.Delivered);
        Assert.False(result.RetryScheduled);
        Assert.False(result.DeadLettered);
        Assert.Equal(1, effects);

        var next = await store.DispatchOutboxAsync(context, new TestAuthorityRevalidator(), Clock.AddMinutes(1), (_, _, _) =>
        {
            effects++;
            return ValueTask.FromResult(DurableWorkProtectedEffectResult.Applied(DurableWorkHandlerResult.Succeeded()));
        });

        Assert.False(next.Delivered);
        Assert.Equal(1, effects);
    }

    [Fact]
    public async Task Unknown_outbox_outcome_is_never_automatically_repeated()
    {
        var context = CreateContext();
        var store = new InMemoryDurableWorkStore();
        var work = Work(context, "outbox-outcome-unknown");
        await store.SubmitAsync(work);
        var effects = 0;

        var result = await store.DispatchOutboxAsync(context, new TestAuthorityRevalidator(), Clock, (_, _, _) =>
        {
            effects++;
            throw new InvalidOperationException("interrupted after the effect boundary");
        });

        Assert.True(result.OutcomeUnknown);
        Assert.False(result.Delivered);
        Assert.False(result.RetryScheduled);
        Assert.Equal(1, effects);

        // No other message is pending, and this one is never revisited by the
        // normal poll: a second dispatch attempt finds nothing to deliver.
        var next = await store.DispatchOutboxAsync(context, new TestAuthorityRevalidator(), Clock.AddMinutes(1), (_, _, _) =>
        {
            effects++;
            return ValueTask.FromResult(DurableWorkProtectedEffectResult.Applied(DurableWorkHandlerResult.Succeeded()));
        });

        Assert.False(next.Delivered);
        Assert.Equal(1, effects);
    }

    [Fact]
    public async Task Completed_duplicate_outbox_delivery_replays_the_safe_result()
    {
        var context = CreateContext();
        var store = new InMemoryDurableWorkStore();
        var work = Work(context, "outbox-duplicate-replay");
        await store.SubmitAsync(work);
        var effects = 0;
        Func<TenantOutboxMessage, VerifiedDurableWorkAuthorization, CancellationToken, ValueTask<DurableWorkProtectedEffectResult>> effect = (_, _, _) =>
        {
            effects++;
            return ValueTask.FromResult(DurableWorkProtectedEffectResult.Applied(DurableWorkHandlerResult.Succeeded()));
        };

        await store.DispatchOutboxAsync(context, new TestAuthorityRevalidator(), Clock, effect);
        Assert.True(store.ReplayOutboxForValidation(work.Identity.WorkItemId, Clock));
        var replay = await store.DispatchOutboxAsync(context, new TestAuthorityRevalidator(), Clock, effect);

        Assert.True(replay.Delivered);
        Assert.True(replay.Duplicate);
        Assert.Equal(1, effects);
    }

    [Fact]
    public async Task Generic_replay_cannot_restart_an_outcome_unknown_outbox_message()
    {
        var context = CreateContext();
        var store = new InMemoryDurableWorkStore();
        var work = Work(context, "outbox-uncertain-no-generic-replay");
        await store.SubmitAsync(work);

        await store.DispatchOutboxAsync(context, new TestAuthorityRevalidator(), Clock, (_, _, _) =>
            ValueTask.FromResult(DurableWorkProtectedEffectResult.OutcomeUnknown("boundary_interrupted")));

        // The generic redelivery/replay path must refuse to restart a message
        // in the explicit uncertain-reconciliation state; only explicit
        // reconciliation may act on it.
        Assert.False(store.ReplayOutboxForValidation(work.Identity.WorkItemId, Clock));
    }

    [Fact]
    public async Task Provider_exception_details_are_redacted_from_evidence()
    {
        var context = CreateContext();
        var store = new InMemoryDurableWorkStore();
        var work = Work(context, "outbox-redacted-exception");
        await store.SubmitAsync(work);
        const string secretDetail = "connection string password=hunter2";

        await store.DispatchOutboxAsync(context, new TestAuthorityRevalidator(), Clock, (_, _, _) =>
            throw new InvalidOperationException(secretDetail));

        var auditJson = JsonSerializer.Serialize(await store.ReadAuditAsync(context));
        Assert.DoesNotContain(secretDetail, auditJson, StringComparison.Ordinal);
        Assert.DoesNotContain("hunter2", auditJson, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------
    // One shared authoritative effect guard across store and dispatcher (H92-01)
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Shared_composition_executor_guards_handler_and_outbox_effects_independently()
    {
        var sharedExecutor = DurableWorkEffectComposition.CreateSharedExecutor();
        var context = CreateContext();
        var store = new InMemoryDurableWorkStore(sharedExecutor);
        var dispatcher = new DurableWorkDispatcher(OperationCatalogue, PayloadRegistry, sharedExecutor);
        var handler = new CountingEffectHandler();
        dispatcher.Register(handler);
        var worker = new TenantDurableWorkWorker(store, dispatcher, new TestAuthorityRevalidator());
        var work = Work(context, "shared-executor-cross-purpose");
        await store.SubmitAsync(work);

        // The handler effect and the outbox effect share the identical Tenant,
        // work item and operation, but only differ by purpose. Both must
        // execute exactly once on the one shared guard.
        var handlerCompletion = await worker.ProcessOneAsync(context, Guid.NewGuid(), Clock, TimeSpan.FromMinutes(5));
        Assert.Equal(DurableWorkLifecycle.Completed, handlerCompletion!.Lifecycle);
        Assert.Equal(1, handler.Calls);

        var outboxEffects = 0;
        var outboxResult = await store.DispatchOutboxAsync(context, new TestAuthorityRevalidator(), Clock, (_, _, _) =>
        {
            outboxEffects++;
            return ValueTask.FromResult(DurableWorkProtectedEffectResult.Applied(DurableWorkHandlerResult.Succeeded()));
        });

        Assert.True(outboxResult.Delivered);
        Assert.False(outboxResult.Duplicate);
        Assert.Equal(1, outboxEffects);

        // A duplicate handler dispatch for the identical effect key replays
        // the completed result instead of invoking the handler again.
        var authority = await new TestAuthorityRevalidator().RevalidateAsync(work, context, Clock);
        var executionContext = new DurableWorkExecutionContext(work, authority.Authorization!);
        var duplicateHandlerResult = await dispatcher.DispatchAsync(work, executionContext, CancellationToken.None);
        Assert.True(duplicateHandlerResult.Success);
        Assert.Equal(1, handler.Calls);
    }

    // ---------------------------------------------------------------------
    // Explicit protected-effect outcome contract (H92-02)
    // ---------------------------------------------------------------------

    [Fact]
    public void Protected_effect_boundary_cannot_return_a_bare_generic_retry()
    {
        var executorMethod = typeof(IDurableWorkEffectExecutor).GetMethod(nameof(IDurableWorkEffectExecutor.ExecuteHandlerEffectAsync))!;
        var protectedEffectParameter = executorMethod.GetParameters()
            .Single(p => p.ParameterType.IsGenericType && p.ParameterType.Name.StartsWith("Func`", StringComparison.Ordinal));
        var delegateReturnType = protectedEffectParameter.ParameterType.GetGenericArguments()[1];
        Assert.Equal(typeof(ValueTask<DurableWorkProtectedEffectResult>), delegateReturnType);

        var handlerMethod = typeof(IDurableWorkHandler<>).GetMethod("ExecuteAsync")!;
        Assert.Equal(typeof(ValueTask<DurableWorkProtectedEffectResult>), handlerMethod.ReturnType);
    }

    [Fact]
    public void Applied_protected_effect_result_requires_a_successful_safe_result()
    {
        Assert.Throws<ArgumentException>(() => DurableWorkProtectedEffectResult.Applied(
            DurableWorkHandlerResult.DeadLettered(DurableWorkFailureCategory.HandlerFailed, "not_success")));
    }

    [Fact]
    public async Task Applied_effect_replay_never_invokes_a_second_attempt()
    {
        var executor = NewExecutor();
        var key = NewKey();
        var first = await executor.ExecuteHandlerEffectAsync(key, _ =>
            ValueTask.FromResult(DurableWorkProtectedEffectResult.Applied(DurableWorkHandlerResult.Succeeded())));

        var second = await executor.ExecuteHandlerEffectAsync(key, _ =>
            throw new InvalidOperationException("must not be invoked: the effect already applied"));

        Assert.Equal(DurableWorkEffectExecutionKind.Executed, first.Kind);
        Assert.Equal(DurableWorkEffectExecutionKind.Replayed, second.Kind);
        Assert.True(second.Result.Success);
    }

    [Fact]
    public async Task Explicit_applied_records_completed_state()
    {
        var guard = new InMemoryDurableWorkEffectGuard();
        var executor = new DurableWorkEffectExecutor(guard);
        var key = NewKey();

        await executor.ExecuteHandlerEffectAsync(key, _ =>
            ValueTask.FromResult(DurableWorkProtectedEffectResult.Applied(DurableWorkHandlerResult.Succeeded())));

        Assert.Equal(DurableWorkEffectState.Completed, guard.GetState(key));
    }

    [Fact]
    public async Task Explicit_not_applied_retryable_releases_the_reservation()
    {
        var guard = new InMemoryDurableWorkEffectGuard();
        var executor = new DurableWorkEffectExecutor(guard);
        var key = NewKey();

        var result = await executor.ExecuteHandlerEffectAsync(key, _ =>
            ValueTask.FromResult(DurableWorkProtectedEffectResult.NotAppliedRetryable(
                DurableWorkFailureCategory.ProviderUnavailable, TimeSpan.FromSeconds(1), "not_applied")));

        Assert.Equal(DurableWorkEffectExecutionKind.Executed, result.Kind);
        Assert.False(result.Result.Success);
        Assert.Equal(DurableWorkEffectState.NotStarted, guard.GetState(key));
    }

    [Fact]
    public async Task Explicit_outcome_unknown_prevents_automatic_retry()
    {
        var guard = new InMemoryDurableWorkEffectGuard();
        var executor = new DurableWorkEffectExecutor(guard);
        var key = NewKey();
        var invocations = 0;

        var first = await executor.ExecuteHandlerEffectAsync(key, _ =>
        {
            invocations++;
            return ValueTask.FromResult(DurableWorkProtectedEffectResult.OutcomeUnknown("explicit_uncertain"));
        });

        Assert.Equal(DurableWorkEffectExecutionKind.OutcomeUnknown, first.Kind);
        Assert.Equal(DurableWorkEffectState.OutcomeUnknown, guard.GetState(key));

        var second = await executor.ExecuteHandlerEffectAsync(key, _ =>
        {
            invocations++;
            return ValueTask.FromResult(DurableWorkProtectedEffectResult.Applied(DurableWorkHandlerResult.Succeeded()));
        });

        Assert.Equal(DurableWorkEffectExecutionKind.OutcomeUnknown, second.Kind);
        Assert.Equal(1, invocations);
    }

    [Fact]
    public async Task Exception_after_reservation_becomes_outcome_unknown()
    {
        var executor = NewExecutor();
        var key = NewKey();

        var result = await executor.ExecuteHandlerEffectAsync(key, _ =>
            throw new InvalidOperationException("boundary interrupted by an unexpected exception"));

        Assert.Equal(DurableWorkEffectExecutionKind.OutcomeUnknown, result.Kind);
        Assert.True(result.Result.IsOutcomeUnknown);
    }

    [Fact]
    public async Task Pre_reservation_authority_failure_retries_without_touching_the_guard()
    {
        var guard = new InMemoryDurableWorkEffectGuard();
        var executor = new DurableWorkEffectExecutor(guard);
        var context = CreateContext();
        var store = new InMemoryDurableWorkStore(executor);
        var dispatcher = new DurableWorkDispatcher(OperationCatalogue, PayloadRegistry, executor);
        var handler = new CountingEffectHandler();
        dispatcher.Register(handler);
        var worker = new TenantDurableWorkWorker(
            store,
            dispatcher,
            new ControlledAuthorityRevalidator(AuthorityBehavior.ProviderUnavailable));
        var work = Work(context, "pre-reservation-retry");
        await store.SubmitAsync(work);
        var effectKey = DurableWorkEffectKey.ForHandlerEffect(context.TenantId, work.Identity.WorkItemId, work.Identity.OperationId);

        var completion = await worker.ProcessOneAsync(context, Guid.NewGuid(), Clock, TimeSpan.FromMinutes(5));

        Assert.Equal(DurableWorkLifecycle.RetryScheduled, completion!.Lifecycle);
        Assert.Equal(0, handler.Calls);
        Assert.Equal(DurableWorkEffectState.NotStarted, guard.GetState(effectKey));
    }

    // ---------------------------------------------------------------------
    // Explicit uncertain reconciliation state (M92-01)
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Handler_outcome_unknown_enters_explicit_uncertain_state_and_is_excluded_from_normal_poll()
    {
        var sharedExecutor = DurableWorkEffectComposition.CreateSharedExecutor();
        var context = CreateContext();
        var store = new InMemoryDurableWorkStore(sharedExecutor);
        var dispatcher = new DurableWorkDispatcher(OperationCatalogue, PayloadRegistry, sharedExecutor);
        dispatcher.Register(new OutcomeUnknownHandler());
        var worker = new TenantDurableWorkWorker(store, dispatcher, new TestAuthorityRevalidator());
        var work = Work(context, "handler-outcome-unknown");
        await store.SubmitAsync(work);

        var completion = await worker.ProcessOneAsync(context, Guid.NewGuid(), Clock, TimeSpan.FromMinutes(5));

        Assert.Equal(DurableWorkLifecycle.OutcomeUnknown, completion!.Lifecycle);
        Assert.Contains(await store.ReadAuditAsync(context), item => item.EventType == "work.outcome-unknown");

        // Normal polling never selects an OutcomeUnknown item; ordinary
        // dead-letter/retry handling never applies to it either.
        var nextClaim = await store.TryClaimAsync(context, Guid.NewGuid(), Clock.AddMinutes(1), TimeSpan.FromMinutes(5));
        Assert.Null(nextClaim);

        var uncertain = await store.ReadUncertainEffectsAsync(context);
        var record = Assert.Single(uncertain);
        Assert.Equal(DurableWorkEffectPurpose.Handler, record.Purpose);
        Assert.Equal(work.Identity.WorkItemId, record.WorkItemId);
    }

    [Fact]
    public async Task Outbox_outcome_unknown_appears_in_the_tenant_scoped_reconciliation_read_port()
    {
        var context = CreateContext();
        var store = new InMemoryDurableWorkStore();
        var work = Work(context, "outbox-reconciliation-evidence");
        await store.SubmitAsync(work);

        await store.DispatchOutboxAsync(context, new TestAuthorityRevalidator(), Clock, (_, _, _) =>
            ValueTask.FromResult(DurableWorkProtectedEffectResult.OutcomeUnknown("boundary_interrupted")));

        var uncertain = await store.ReadUncertainEffectsAsync(context);
        var record = Assert.Single(uncertain);
        Assert.Equal(DurableWorkEffectPurpose.Outbox, record.Purpose);
        Assert.Equal(work.Identity.WorkItemId, record.WorkItemId);
    }

    [Fact]
    public async Task Tenant_a_cannot_read_tenant_b_uncertain_effects()
    {
        var tenantA = CreateContext();
        var tenantB = CreateContext();
        var store = new InMemoryDurableWorkStore();
        var work = Work(tenantB, "uncertain-cross-tenant");
        await store.SubmitAsync(work);

        await store.DispatchOutboxAsync(tenantB, new TestAuthorityRevalidator(), Clock, (_, _, _) =>
            ValueTask.FromResult(DurableWorkProtectedEffectResult.OutcomeUnknown("boundary_interrupted")));

        Assert.Empty(await store.ReadUncertainEffectsAsync(tenantA));
        Assert.Single(await store.ReadUncertainEffectsAsync(tenantB));
    }

    [Fact]
    public async Task Uncertain_effect_evidence_excludes_payload_and_provider_exception_text()
    {
        var context = CreateContext();
        var store = new InMemoryDurableWorkStore();
        var secretMarker = $"provider-secret-{Guid.NewGuid():N}";
        var work = Work(context, "uncertain-evidence-redacted", secretMarker);
        await store.SubmitAsync(work);

        await store.DispatchOutboxAsync(context, new TestAuthorityRevalidator(), Clock, (_, _, _) =>
            throw new InvalidOperationException(secretMarker));

        var evidenceJson = JsonSerializer.Serialize(await store.ReadUncertainEffectsAsync(context));
        Assert.DoesNotContain(secretMarker, evidenceJson, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------
    // Payload codec exception sanitization (M92-02)
    // ---------------------------------------------------------------------

    [Fact]
    public void Custom_codec_encode_exception_is_sanitized_and_secret_is_not_exposed()
    {
        var registry = new DurableWorkPayloadRegistry();
        var secretMarker = $"codec-secret-{Guid.NewGuid():N}";
        registry.Register(new DurableWorkPayloadTypeId("test.throwing-codec"), new ThrowingCodec(secretMarker));

        var thrown = Assert.Throws<DurableWorkPayloadException>(() => registry.Capture(new ThrowingPayload("value")));
        Assert.DoesNotContain(secretMarker, thrown.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(secretMarker, thrown.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public void Custom_codec_decode_exception_is_sanitized_and_secret_is_not_exposed()
    {
        var registry = new DurableWorkPayloadRegistry();
        var secretMarker = $"codec-secret-{Guid.NewGuid():N}";
        registry.Register(new DurableWorkPayloadTypeId("test.throwing-decode-codec"), new ThrowingDecodeCodec(secretMarker));
        var envelope = DurableWorkPayloadEnvelope.Capture(new DurableWorkPayloadTypeId("test.throwing-decode-codec"), "ok"u8.ToArray());

        var thrown = Assert.Throws<DurableWorkPayloadException>(() => registry.Decode<ThrowingPayload>(envelope));
        Assert.DoesNotContain(secretMarker, thrown.Message, StringComparison.Ordinal);
        Assert.DoesNotContain(secretMarker, thrown.ToString(), StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------------
    // Test infrastructure
    // ---------------------------------------------------------------------

    /// <summary>
    /// Test-only checksum-corruption seam (M92-02): production code exposes no
    /// method that mutates stored payload bytes, so the test project uses
    /// bounded reflection over the private backing field instead.
    /// </summary>
    private static void CorruptStoredBytesForTest(DurableWorkPayloadEnvelope envelope)
    {
        var field = typeof(DurableWorkPayloadEnvelope).GetField("bytes", BindingFlags.NonPublic | BindingFlags.Instance)
            ?? throw new InvalidOperationException("The private payload bytes field was not found.");
        var bytes = (byte[])field.GetValue(envelope)!;
        if (bytes.Length > 0)
        {
            bytes[0] ^= 0xFF;
        }
    }

    private static IDurableWorkEffectExecutor NewExecutor() =>
        new DurableWorkEffectExecutor(new InMemoryDurableWorkEffectGuard());

    private static DurableWorkEffectKey NewKey() =>
        DurableWorkEffectKey.ForHandlerEffect(new TenantId(Guid.NewGuid()), Guid.NewGuid(), "foundation.effect-work");

    private static async Task<TResult[]> RunWithBarrierAsync<TResult>(int contenders, Func<Task<TResult>> action)
    {
        using var barrier = new Barrier(contenders);
        var tasks = Enumerable.Range(0, contenders).Select(_ => Task.Run(async () =>
        {
            barrier.SignalAndWait();
            return await action();
        })).ToArray();
        return await Task.WhenAll(tasks);
    }

    private static async Task<TResult[]> RunWithBarrierAsync<TResult>(int contenders, Func<int, Task<TResult>> action)
    {
        using var barrier = new Barrier(contenders);
        var tasks = Enumerable.Range(0, contenders).Select(index => Task.Run(async () =>
        {
            barrier.SignalAndWait();
            return await action(index);
        })).ToArray();
        return await Task.WhenAll(tasks);
    }

    private static DurableWorkItem Work(TenantContext context, string key, string payload = "payload") =>
        DurableWorkItem.Create(
            context,
            DurableWorkTestSupport.TenantWideScope(context),
            Identity(context, key),
            new SimplePayload(payload),
            PayloadRegistry,
            Guid.NewGuid(),
            3,
            Clock);

    private static DurableWorkIdentity Identity(TenantContext context, string key) =>
        DurableWorkIdentity.Create(
            Guid.NewGuid(),
            OperationCatalogue,
            "foundation.effect-work",
            context.CorrelationId!.Value,
            key);

    private static TenantContext CreateContext() =>
        TenantContext.ForOrdinaryMembership(
            new TenantId(Guid.NewGuid()),
            new MembershipReference(Guid.NewGuid()),
            new ScopeReference("tenant"),
            new CorrelationId($"corr-{Guid.NewGuid():N}"),
            Guid.NewGuid());

    private sealed record SimplePayload(string Value) : IWorkPayload;

    private sealed class MutableStringPayload : IWorkPayload
    {
        public string Value { get; set; } = string.Empty;
    }

    private sealed class ByteArrayPayload : IWorkPayload
    {
        public byte[] Data { get; set; } = [];
    }

    private sealed class ListPayload : IWorkPayload
    {
        public List<string> Items { get; set; } = [];
    }

    private sealed class NestedMutable
    {
        public string Detail { get; set; } = string.Empty;
    }

    private sealed class NestedMutablePayload : IWorkPayload
    {
        public NestedMutable Nested { get; set; } = new();
    }

    private sealed record ThrowingPayload(string Value) : IWorkPayload;

    private sealed class ThrowingCodec : IDurableWorkPayloadCodec<ThrowingPayload>
    {
        private readonly string secretMarker;

        internal ThrowingCodec(string secretMarker)
        {
            this.secretMarker = secretMarker;
        }

        public byte[] Encode(ThrowingPayload payload) =>
            throw new InvalidOperationException($"codec failure containing secret: {secretMarker}");

        public ThrowingPayload Decode(ReadOnlySpan<byte> bytes) =>
            throw new InvalidOperationException($"codec failure containing secret: {secretMarker}");
    }

    private sealed class ThrowingDecodeCodec : IDurableWorkPayloadCodec<ThrowingPayload>
    {
        private readonly string secretMarker;

        internal ThrowingDecodeCodec(string secretMarker)
        {
            this.secretMarker = secretMarker;
        }

        public byte[] Encode(ThrowingPayload payload) => "ok"u8.ToArray();

        public ThrowingPayload Decode(ReadOnlySpan<byte> bytes) =>
            throw new InvalidOperationException($"codec failure containing secret: {secretMarker}");
    }

    private sealed class CountingEffectHandler : IDurableWorkHandler<SimplePayload>
    {
        internal int Calls { get; private set; }

        public DurableWorkOperationDescriptor Operation => OperationCatalogue.TryGet("foundation.effect-work", out var operation)
            ? operation
            : throw new InvalidOperationException("The test operation is not registered.");

        public ValueTask<DurableWorkProtectedEffectResult> ExecuteAsync(
            SimplePayload payload,
            DurableWorkExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            return ValueTask.FromResult(DurableWorkProtectedEffectResult.Applied(DurableWorkHandlerResult.Succeeded()));
        }
    }

    private sealed class OutcomeUnknownHandler : IDurableWorkHandler<SimplePayload>
    {
        public DurableWorkOperationDescriptor Operation => OperationCatalogue.TryGet("foundation.effect-work", out var operation)
            ? operation
            : throw new InvalidOperationException("The test operation is not registered.");

        public ValueTask<DurableWorkProtectedEffectResult> ExecuteAsync(
            SimplePayload payload,
            DurableWorkExecutionContext context,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(DurableWorkProtectedEffectResult.OutcomeUnknown("handler_reported_uncertain"));
    }

    private sealed class FaultInjectingEffectGuard : IDurableWorkEffectGuard
    {
        private readonly IDurableWorkEffectGuard inner;
        private readonly bool failOnRecordCompleted;

        internal FaultInjectingEffectGuard(IDurableWorkEffectGuard inner, bool failOnRecordCompleted)
        {
            this.inner = inner;
            this.failOnRecordCompleted = failOnRecordCompleted;
        }

        public DurableWorkEffectState GetState(DurableWorkEffectKey key) => inner.GetState(key);

        public DurableWorkEffectReservation TryReserve(DurableWorkEffectKey key) => inner.TryReserve(key);

        public void RecordCompleted(DurableWorkEffectKey key, DurableWorkHandlerResult safeResult)
        {
            if (failOnRecordCompleted)
            {
                throw new InvalidOperationException("simulated completion-recording failure");
            }

            inner.RecordCompleted(key, safeResult);
        }

        public void RecordOutcomeUnknown(DurableWorkEffectKey key, string safeReason) =>
            inner.RecordOutcomeUnknown(key, safeReason);

        public void Release(DurableWorkEffectKey key) => inner.Release(key);
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

    private enum AuthorityBehavior
    {
        ProviderUnavailable
    }

    private sealed class ControlledAuthorityRevalidator : IDurableWorkAuthorityRevalidator
    {
        private readonly AuthorityBehavior behavior;

        internal ControlledAuthorityRevalidator(AuthorityBehavior behavior)
        {
            this.behavior = behavior;
        }

        public ValueTask<DurableWorkAuthorityValidationResult> RevalidateAsync(
            DurableWorkItem workItem,
            TenantContext currentTenantContext,
            DateTimeOffset now,
            CancellationToken cancellationToken = default) =>
            behavior == AuthorityBehavior.ProviderUnavailable
                ? throw new InvalidOperationException("simulated authority provider failure")
                : throw new NotSupportedException();
    }
}
