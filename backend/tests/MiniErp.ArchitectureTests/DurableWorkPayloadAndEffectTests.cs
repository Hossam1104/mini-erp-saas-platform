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
        envelope.TamperForValidation();

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
            return ValueTask.FromResult(DurableWorkHandlerResult.Succeeded());
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
        Func<CancellationToken, ValueTask<DurableWorkHandlerResult>> effect = ct =>
        {
            Interlocked.Increment(ref invocations);
            return ValueTask.FromResult(DurableWorkHandlerResult.Succeeded());
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
                return ValueTask.FromResult(DurableWorkHandlerResult.Succeeded());
            },
            preCancelled.Token).AsTask());

        Assert.Equal(DurableWorkEffectState.NotStarted, guard.GetState(key));
        Assert.Equal(0, invocations);

        var retry = await executor.ExecuteHandlerEffectAsync(key, ct =>
        {
            Interlocked.Increment(ref invocations);
            return ValueTask.FromResult(DurableWorkHandlerResult.Succeeded());
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
            return ValueTask.FromResult(DurableWorkHandlerResult.Succeeded());
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
        Func<CancellationToken, ValueTask<DurableWorkHandlerResult>> effect = ct =>
        {
            Interlocked.Increment(ref invocations);
            return ValueTask.FromResult(DurableWorkHandlerResult.Succeeded());
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
        Func<CancellationToken, ValueTask<DurableWorkHandlerResult>> effect = ct =>
        {
            Interlocked.Increment(ref invocations);
            return ValueTask.FromResult(DurableWorkHandlerResult.Succeeded());
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
        await executor.ExecuteHandlerEffectAsync(key, _ => ValueTask.FromResult(DurableWorkHandlerResult.Succeeded()));
        Assert.Equal(DurableWorkEffectState.OutcomeUnknown, guard.GetState(key));

        var invoked = false;
        var blocked = await executor.ExecuteHandlerEffectAsync(key, _ =>
        {
            invoked = true;
            return ValueTask.FromResult(DurableWorkHandlerResult.Succeeded());
        });

        Assert.False(invoked);
        Assert.Equal(DurableWorkEffectExecutionKind.OutcomeUnknown, blocked.Kind);
        Assert.Equal(DurableWorkFailureCategory.Unknown, blocked.Result.FailureCategory);
    }

    [Fact]
    public async Task Duplicate_dispatch_replays_the_exact_safe_result()
    {
        var executor = NewExecutor();
        var key = NewKey();
        var first = await executor.ExecuteHandlerEffectAsync(key, _ =>
            ValueTask.FromResult(DurableWorkHandlerResult.DeadLettered(DurableWorkFailureCategory.HandlerFailed, "safe_terminal_reason")));

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
        var keyA = new DurableWorkEffectKey(new TenantId(Guid.NewGuid()), workItemId, "foundation.effect-work");
        var keyB = new DurableWorkEffectKey(new TenantId(Guid.NewGuid()), workItemId, "foundation.effect-work");
        var invocations = 0;
        Func<CancellationToken, ValueTask<DurableWorkHandlerResult>> effect = ct =>
        {
            Interlocked.Increment(ref invocations);
            return ValueTask.FromResult(DurableWorkHandlerResult.Succeeded());
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
        var keyA = new DurableWorkEffectKey(tenantId, workItemId, "foundation.effect-work");
        var keyB = new DurableWorkEffectKey(tenantId, workItemId, "foundation.other-effect-work");
        var invocations = 0;
        Func<CancellationToken, ValueTask<DurableWorkHandlerResult>> effect = ct =>
        {
            Interlocked.Increment(ref invocations);
            return ValueTask.FromResult(DurableWorkHandlerResult.Succeeded());
        };

        var resultA = await executor.ExecuteHandlerEffectAsync(keyA, effect);
        var resultB = await executor.ExecuteHandlerEffectAsync(keyB, effect);

        Assert.Equal(DurableWorkEffectExecutionKind.Executed, resultA.Kind);
        Assert.Equal(DurableWorkEffectExecutionKind.Executed, resultB.Kind);
        Assert.Equal(2, invocations);
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
                return DurableWorkHandlerResult.Succeeded();
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
            return ValueTask.CompletedTask;
        });

        Assert.True(dispatchResult.Delivered);
        Assert.Equal(1, effects);
    }

    // ---------------------------------------------------------------------
    // Outbox explicit Applied / NotAppliedRetryable / OutcomeUnknown outcomes
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
            return ValueTask.CompletedTask;
        });

        Assert.True(result.Delivered);
        Assert.False(result.OutcomeUnknown);
        Assert.False(result.RetryScheduled);
        Assert.Equal(1, effects);

        var next = await store.DispatchOutboxAsync(context, new TestAuthorityRevalidator(), Clock, (_, _, _) =>
        {
            effects++;
            return ValueTask.CompletedTask;
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
                return ValueTask.CompletedTask;
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
                return ValueTask.CompletedTask;
            });

        Assert.True(succeeded.Delivered);
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
            return ValueTask.CompletedTask;
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
        Func<TenantOutboxMessage, VerifiedDurableWorkAuthorization, CancellationToken, ValueTask> effect = (_, _, _) =>
        {
            effects++;
            return ValueTask.CompletedTask;
        };

        await store.DispatchOutboxAsync(context, new TestAuthorityRevalidator(), Clock, effect);
        Assert.True(store.ReplayOutboxForValidation(work.Identity.WorkItemId, Clock));
        var replay = await store.DispatchOutboxAsync(context, new TestAuthorityRevalidator(), Clock, effect);

        Assert.True(replay.Delivered);
        Assert.True(replay.Duplicate);
        Assert.Equal(1, effects);
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
    // Test infrastructure
    // ---------------------------------------------------------------------

    private static IDurableWorkEffectExecutor NewExecutor() =>
        new DurableWorkEffectExecutor(new InMemoryDurableWorkEffectGuard());

    private static DurableWorkEffectKey NewKey() =>
        new(new TenantId(Guid.NewGuid()), Guid.NewGuid(), "foundation.effect-work");

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
