#pragma warning disable CS1591

using MiniErp.App.BuildingBlocks.Tenancy;

namespace MiniErp.App.BuildingBlocks.Work;

/// <summary>
/// Deterministic local, in-memory adapter for the durable-work seam. It is
/// intentionally bounded and is never a relational, SQL-backed, process-crash
/// durable, production-ready or distributed exactly-once store or provider.
/// The constructor is internal and always requires an explicit effect
/// executor: shipping code obtains the one approved instance, sharing the
/// exact same executor as its paired <see cref="DurableWorkDispatcher"/>,
/// only through <see cref="DurableWorkLocalRuntime"/>. Tests use this
/// constructor directly through InternalsVisibleTo.
/// </summary>
public sealed class InMemoryDurableWorkStore : IDurableWorkStore
{
    private readonly object syncRoot = new();
    private readonly Dictionary<Guid, DurableWorkItem> workItems = [];
    private readonly Dictionary<(TenantId TenantId, string Key), Guid> idempotency = [];
    private readonly Dictionary<Guid, TenantOutboxMessage> outbox = [];
    private readonly List<DurableWorkAuditRecord> audit = [];
    private readonly IDurableWorkEffectExecutor effectExecutor;

    internal InMemoryDurableWorkStore(IDurableWorkEffectExecutor effectExecutor)
    {
        this.effectExecutor = effectExecutor ?? throw new ArgumentNullException(nameof(effectExecutor));
    }

    /// <summary>Submits work and its outbox event as one local transaction.</summary>
    public ValueTask<bool> SubmitAsync(
        DurableWorkItem workItem,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(workItem);
        cancellationToken.ThrowIfCancellationRequested();
        lock (syncRoot)
        {
            var key = (workItem.TenantId, workItem.Identity.IdempotencyKey);
            if (idempotency.TryGetValue(key, out var existingId))
            {
                return ValueTask.FromResult(existingId == workItem.Identity.WorkItemId);
            }

            if (workItems.ContainsKey(workItem.Identity.WorkItemId))
            {
                return ValueTask.FromResult(false);
            }

            workItems.Add(workItem.Identity.WorkItemId, workItem);
            idempotency.Add(key, workItem.Identity.WorkItemId);
            var message = new TenantOutboxMessage(Guid.NewGuid(), workItem, workItem.CreatedAt);
            outbox.Add(message.EventId, message);
            AddAudit(workItem, "work.submitted", workItem.Lifecycle, DurableWorkFailureCategory.None);
            return ValueTask.FromResult(true);
        }
    }

    /// <summary>Returns only a record owned by the trusted Tenant context.</summary>
    public ValueTask<DurableWorkItem?> FindAsync(
        TenantContext tenantContext,
        Guid workItemId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        cancellationToken.ThrowIfCancellationRequested();
        lock (syncRoot)
        {
            if (!workItems.TryGetValue(workItemId, out var item)
                || item.TenantId != tenantContext.TenantId
                || item.Initiator.AuthorizationPath != tenantContext.AuthorizationPath)
            {
                return ValueTask.FromResult<DurableWorkItem?>(null);
            }

            return ValueTask.FromResult<DurableWorkItem?>(item);
        }
    }

    /// <summary>Claims one eligible record for the exact trusted Tenant.</summary>
    public ValueTask<DurableWorkLease?> TryClaimAsync(
        TenantContext tenantContext,
        Guid workerId,
        DateTimeOffset now,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        if (workerId == Guid.Empty)
        {
            throw new ArgumentException("Worker identifier must not be empty.", nameof(workerId));
        }

        if (leaseDuration <= TimeSpan.Zero || leaseDuration > TimeSpan.FromHours(1))
        {
            throw new ArgumentOutOfRangeException(nameof(leaseDuration));
        }

        cancellationToken.ThrowIfCancellationRequested();
        lock (syncRoot)
        {
            foreach (var workItem in workItems.Values
                         .OrderBy(item => item.CreatedAt)
                         .Where(item => item.IsEligible(tenantContext, now)))
            {
                var lease = workItem.Claim(workerId, now.Add(leaseDuration));
                AddAudit(workItem, "work.claimed", workItem.Lifecycle, DurableWorkFailureCategory.None);
                return ValueTask.FromResult<DurableWorkLease?>(lease);
            }

            return ValueTask.FromResult<DurableWorkLease?>(null);
        }
    }

    /// <summary>Completes a lease only when its stored owner and version match.</summary>
    public ValueTask<DurableWorkCompletion> CompleteAsync(
        TenantContext tenantContext,
        DurableWorkLease lease,
        DurableWorkHandlerResult result,
        DateTimeOffset now,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(lease);
        ArgumentNullException.ThrowIfNull(result);
        cancellationToken.ThrowIfCancellationRequested();
        lock (syncRoot)
        {
            if (!workItems.TryGetValue(lease.WorkItemId, out var item)
                || item.TenantId != tenantContext.TenantId
                || lease.TenantId != tenantContext.TenantId)
            {
                return ValueTask.FromResult(DurableWorkCompletion.CreateDenied(DurableWorkFailureCategory.TenantMismatch));
            }

            var completion = item.Complete(lease, result, now);
            if (completion.Accepted)
            {
                var eventName = item.Lifecycle switch
                {
                    DurableWorkLifecycle.Completed => "work.completed",
                    DurableWorkLifecycle.RetryScheduled => "work.retry",
                    DurableWorkLifecycle.DeadLetter => "work.dead-letter",
                    DurableWorkLifecycle.OutcomeUnknown => "work.outcome-unknown",
                    _ => "work.transition"
                };
                AddAudit(item, eventName, item.Lifecycle, completion.FailureCategory);
            }

            return ValueTask.FromResult(completion);
        }
    }

    /// <summary>
    /// Dispatches one Tenant-owned outbox message with live authority
    /// revalidation and single-effect protection immediately before the
    /// protected effect. Explicit outcomes: Applied (never repeats),
    /// NotAppliedRetryable (bounded retry) or OutcomeUnknown (never
    /// automatically repeats; requires reconciliation).
    /// </summary>
    public async ValueTask<OutboxDispatchResult> DispatchOutboxAsync(
        TenantContext tenantContext,
        IDurableWorkAuthorityRevalidator authorityRevalidator,
        DateTimeOffset now,
        Func<TenantOutboxMessage, VerifiedDurableWorkAuthorization, CancellationToken, ValueTask<DurableWorkProtectedEffectResult>> effect,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(authorityRevalidator);
        ArgumentNullException.ThrowIfNull(effect);
        cancellationToken.ThrowIfCancellationRequested();
        TenantOutboxMessage? message;
        DurableWorkItem? workItem;
        lock (syncRoot)
        {
            message = outbox.Values
                .Where(candidate => candidate.TenantId == tenantContext.TenantId
                    && candidate.DeliveryState is DurableWorkLifecycle.Pending or DurableWorkLifecycle.RetryScheduled
                    && candidate.NextAttemptAt <= now)
                .OrderBy(candidate => candidate.OccurredAt)
                .FirstOrDefault();
            if (message is null)
            {
                return OutboxDispatchResult.NoMessage();
            }

            // Claim before leaving the lock so a concurrent dispatcher cannot
            // run the authority check or protected effect twice.
            message.DeliveryState = DurableWorkLifecycle.Claimed;
            message.AttemptCount++;
            workItems.TryGetValue(message.WorkItemId, out workItem);
        }

        DurableWorkAuthorityValidationResult authority;
        try
        {
            authority = workItem is null
                ? DurableWorkAuthorityValidationResult.Denied("stored_owner_missing")
                : await authorityRevalidator.RevalidateAsync(
                    workItem,
                    tenantContext,
                    now,
                    cancellationToken);
        }
        catch (OperationCanceledException)
        {
            authority = DurableWorkAuthorityValidationResult.Cancelled("authority_validation_cancelled");
        }
        catch (Exception)
        {
            authority = DurableWorkAuthorityValidationResult.TemporarilyUnavailable("authority_validation_unavailable");
        }

        if (authority.Outcome == DurableWorkAuthorityValidationOutcome.Denied)
        {
            lock (syncRoot)
            {
                message.DeliveryState = DurableWorkLifecycle.DeadLetter;
                message.FailureCategory = DurableWorkFailureCategory.AuthorizationDenied;
                AddAuditForMessage(message, "outbox.dead-letter", DurableWorkFailureCategory.AuthorizationDenied);
            }

            return OutboxDispatchResult.AsDeadLettered(DurableWorkFailureCategory.AuthorizationDenied);
        }

        if (!authority.Allowed || authority.Authorization is null)
        {
            lock (syncRoot)
            {
                var terminal = message.AttemptCount >= 3;
                message.DeliveryState = terminal
                    ? DurableWorkLifecycle.DeadLetter
                    : DurableWorkLifecycle.RetryScheduled;
                message.FailureCategory = authority.FailureCategory;
                message.NextAttemptAt = now.Add(BoundedBackoff(message.AttemptCount));
                AddAuditForMessage(
                    message,
                    terminal ? "outbox.dead-letter" : "outbox.retry",
                    authority.FailureCategory);
                return terminal
                    ? OutboxDispatchResult.AsDeadLettered(authority.FailureCategory)
                    : OutboxDispatchResult.NotAppliedRetryable(authority.FailureCategory);
            }
        }

        var effectKey = DurableWorkEffectKey.ForOutboxEffect(
            tenantContext.TenantId,
            message.WorkItemId,
            message.EventType,
            message.EventId);
        var execution = await effectExecutor.ExecuteHandlerEffectAsync(
            effectKey,
            ct => effect(message, authority.Authorization, ct),
            cancellationToken);

        lock (syncRoot)
        {
            var handlerResult = execution.Result;
            if (handlerResult.Success)
            {
                var duplicate = execution.Kind == DurableWorkEffectExecutionKind.Replayed;
                message.DeliveryState = DurableWorkLifecycle.Completed;
                AddAuditForMessage(message, duplicate ? "outbox.duplicate" : "outbox.delivered", DurableWorkFailureCategory.None);
                return OutboxDispatchResult.Applied(duplicate);
            }

            if (execution.Kind == DurableWorkEffectExecutionKind.OutcomeUnknown)
            {
                // The effect boundary was reached but its completion could not
                // be proven. DeliveryState moves to the explicit OutcomeUnknown
                // reconciliation state so the Pending/RetryScheduled poll
                // filter never automatically revisits it and ordinary
                // dead-letter/replay handling cannot restart it. The actual
                // transition time and the provider/executor-reported safe
                // reason are preserved for reconciliation evidence; NextAttemptAt
                // is never reused as the occurrence time.
                message.DeliveryState = DurableWorkLifecycle.OutcomeUnknown;
                message.FailureCategory = DurableWorkFailureCategory.Unknown;
                message.OutcomeUnknownAt = now;
                message.SafeFailureReason = handlerResult.SafeReason ?? "effect_outcome_unknown";
                AddAuditForMessage(message, "outbox.outcome-unknown", DurableWorkFailureCategory.Unknown);
                return OutboxDispatchResult.OutcomeUnknownResult();
            }

            if (handlerResult.DeadLetter)
            {
                // An explicit TerminalNotApplied outcome (or its replay): a
                // safe terminal result that must never be retried, regardless
                // of the remaining attempt budget.
                message.DeliveryState = DurableWorkLifecycle.DeadLetter;
                message.FailureCategory = handlerResult.FailureCategory;
                AddAuditForMessage(message, "outbox.dead-letter", handlerResult.FailureCategory);
                return OutboxDispatchResult.AsDeadLettered(handlerResult.FailureCategory);
            }

            // Only an explicit NotAppliedRetryable outcome (or in-flight
            // contention) reaches this branch: bounded retry is safe.
            var terminalRetry = message.AttemptCount >= 3;
            message.DeliveryState = terminalRetry ? DurableWorkLifecycle.DeadLetter : DurableWorkLifecycle.RetryScheduled;
            message.FailureCategory = handlerResult.FailureCategory;
            message.NextAttemptAt = now.Add(BoundedBackoff(message.AttemptCount));
            AddAuditForMessage(message, terminalRetry ? "outbox.dead-letter" : "outbox.retry", handlerResult.FailureCategory);
            return terminalRetry
                ? OutboxDispatchResult.AsDeadLettered(handlerResult.FailureCategory)
                : OutboxDispatchResult.NotAppliedRetryable(handlerResult.FailureCategory);
        }
    }

    /// <summary>Reads safe, Tenant-scoped audit records only.</summary>
    public ValueTask<IReadOnlyList<DurableWorkAuditRecord>> ReadAuditAsync(
        TenantContext tenantContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        cancellationToken.ThrowIfCancellationRequested();
        lock (syncRoot)
        {
            return ValueTask.FromResult<IReadOnlyList<DurableWorkAuditRecord>>(
                audit.Where(item => item.TenantId == tenantContext.TenantId).ToArray());
        }
    }

    /// <summary>
    /// Scope-authorized reconciliation read port. Returns handler work items
    /// and outbox messages currently in the explicit OutcomeUnknown state
    /// whose exact Tenant and organization boundary is contained by
    /// <paramref name="authorization"/>'s verified scope; a sibling Company,
    /// Branch or Warehouse and another Tenant's records are filtered out
    /// before any record is returned. No payload, exception or provider
    /// secret is exposed.
    /// </summary>
    public ValueTask<IReadOnlyList<DurableWorkUncertainEffectRecord>> ReadUncertainEffectsAsync(
        VerifiedDurableWorkReconciliationAuthorization authorization,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(authorization);
        cancellationToken.ThrowIfCancellationRequested();
        lock (syncRoot)
        {
            var handlerRecords = workItems.Values
                .Where(item => item.Lifecycle == DurableWorkLifecycle.OutcomeUnknown
                    && authorization.TenantId == item.TenantId
                    && authorization.Contains(item.Scope))
                .Select(item => new DurableWorkUncertainEffectRecord(
                    DurableWorkEffectKey.ForHandlerEffect(item.TenantId, item.Identity.WorkItemId, item.Identity.OperationId),
                    item.Scope,
                    item.Identity.CorrelationId,
                    item.UpdatedAt,
                    item.SafeFailureReason ?? "outcome_unknown",
                    item.ConcurrencyVersion));

            var outboxRecords = outbox.Values
                .Where(message => message.DeliveryState == DurableWorkLifecycle.OutcomeUnknown
                    && authorization.TenantId == message.TenantId
                    && authorization.Contains(message.Scope))
                .Select(message => new DurableWorkUncertainEffectRecord(
                    DurableWorkEffectKey.ForOutboxEffect(message.TenantId, message.WorkItemId, message.EventType, message.EventId),
                    message.Scope,
                    message.CorrelationId,
                    message.OutcomeUnknownAt ?? message.NextAttemptAt,
                    message.SafeFailureReason ?? "outcome_unknown",
                    message.AttemptCount));

            return ValueTask.FromResult<IReadOnlyList<DurableWorkUncertainEffectRecord>>(
                handlerRecords.Concat(outboxRecords).ToArray());
        }
    }

    /// <summary>Local test hook that expires a lease without changing ownership.</summary>
    internal async ValueTask ExpireLeasesAsync(DateTimeOffset now)
    {
        await Task.Yield();
        lock (syncRoot)
        {
            foreach (var item in workItems.Values)
            {
                item.MarkExpiredLease(now);
            }
        }
    }

    /// <summary>
    /// Test-only replay hook. It models a redelivered outbox row while the
    /// effect guard retains its own completion record; production callers
    /// cannot access the local adapter's internal validation hooks. A message
    /// in the explicit OutcomeUnknown reconciliation state is never restarted
    /// by this generic replay path: only explicit reconciliation applies.
    /// </summary>
    internal bool ReplayOutboxForValidation(Guid workItemId, DateTimeOffset nextAttemptAt)
    {
        lock (syncRoot)
        {
            var message = outbox.Values.FirstOrDefault(item => item.WorkItemId == workItemId);
            if (message is null || message.DeliveryState == DurableWorkLifecycle.OutcomeUnknown)
            {
                return false;
            }

            message.DeliveryState = DurableWorkLifecycle.RetryScheduled;
            message.NextAttemptAt = nextAttemptAt;
            return true;
        }
    }

    private void AddAudit(
        DurableWorkItem item,
        string eventType,
        DurableWorkLifecycle lifecycle,
        DurableWorkFailureCategory failureCategory)
    {
        audit.Add(new DurableWorkAuditRecord(
            DateTimeOffset.UtcNow,
            eventType,
            item.TenantId,
            item.Identity.WorkItemId,
            item.Identity.CorrelationId,
            lifecycle,
            failureCategory,
            item.AttemptCount));
    }

    private void AddAuditForMessage(
        TenantOutboxMessage message,
        string eventType,
        DurableWorkFailureCategory failureCategory)
    {
        audit.Add(new DurableWorkAuditRecord(
            DateTimeOffset.UtcNow,
            eventType,
            message.TenantId,
            message.WorkItemId,
            message.CorrelationId,
            message.DeliveryState,
            failureCategory,
            message.AttemptCount));
    }

    private static TimeSpan BoundedBackoff(int attempt) =>
        TimeSpan.FromSeconds(Math.Min(60, Math.Max(1, attempt * attempt)));
}

/// <summary>
/// One typed handler invocation; no global Tenant scan is available. The
/// constructor is internal: shipping code obtains the one approved instance,
/// sharing the exact same executor as its paired <see cref="InMemoryDurableWorkStore"/>,
/// only through <see cref="DurableWorkLocalRuntime"/>. Tests use this
/// constructor directly through InternalsVisibleTo.
/// </summary>
public sealed class DurableWorkDispatcher
{
    private readonly IDurableWorkOperationCatalogue operationCatalogue;
    private readonly IDurableWorkPayloadRegistry payloadRegistry;
    private readonly IDurableWorkEffectExecutor effectExecutor;
    private readonly Dictionary<string, Func<DurableWorkItem, DurableWorkExecutionContext, CancellationToken, ValueTask<DurableWorkHandlerResult>>> handlers =
        new(StringComparer.Ordinal);

    internal DurableWorkDispatcher(
        IDurableWorkOperationCatalogue operationCatalogue,
        IDurableWorkPayloadRegistry payloadRegistry,
        IDurableWorkEffectExecutor effectExecutor)
    {
        this.operationCatalogue = operationCatalogue ?? throw new ArgumentNullException(nameof(operationCatalogue));
        this.payloadRegistry = payloadRegistry ?? throw new ArgumentNullException(nameof(payloadRegistry));
        this.effectExecutor = effectExecutor ?? throw new ArgumentNullException(nameof(effectExecutor));
    }

    /// <summary>
    /// Registers one typed handler. Every dispatched call is routed through the
    /// approved effect executor using a stable (Tenant, WorkItemId, OperationId)
    /// effect key; a handler cannot bypass that guard while still being treated
    /// as a protected durable-work handler.
    /// </summary>
    public void Register<TPayload>(IDurableWorkHandler<TPayload> handler)
        where TPayload : IWorkPayload
    {
        ArgumentNullException.ThrowIfNull(handler);
        ArgumentNullException.ThrowIfNull(handler.Operation);
        if (!operationCatalogue.TryGet(handler.Operation.OperationId, out var registeredOperation)
            || !registeredOperation.ExactlyMatches(handler.Operation)
            || !handler.Operation.RequiresMandatorySecurityEvidence)
        {
            throw new ArgumentException("A typed handler must declare an exact registered operation descriptor.", nameof(handler));
        }

        if (!handlers.TryAdd(handler.Operation.OperationId, Invoke))
        {
            throw new InvalidOperationException("A durable-work handler is already registered for this operation.");
        }

        async ValueTask<DurableWorkHandlerResult> Invoke(
            DurableWorkItem item,
            DurableWorkExecutionContext context,
            CancellationToken cancellationToken)
        {
            TPayload payload;
            try
            {
                payload = payloadRegistry.Decode<TPayload>(item.PayloadEnvelope);
            }
            catch (DurableWorkPayloadException)
            {
                return DurableWorkHandlerResult.DeadLettered(
                    DurableWorkFailureCategory.ValidationFailed,
                    "typed_payload_mismatch");
            }

            var effectKey = DurableWorkEffectKey.ForHandlerEffect(item.TenantId, item.Identity.WorkItemId, item.Identity.OperationId);
            var execution = await effectExecutor.ExecuteHandlerEffectAsync(
                effectKey,
                ct => handler.ExecuteAsync(payload, context, ct),
                cancellationToken);
            return execution.Result;
        }
    }

    public ValueTask<DurableWorkHandlerResult> DispatchAsync(
        DurableWorkItem item,
        DurableWorkExecutionContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(item);
        ArgumentNullException.ThrowIfNull(context);
        if (item.TenantId != context.TenantContext.TenantId)
        {
            return ValueTask.FromResult(DurableWorkHandlerResult.DeadLettered(
                DurableWorkFailureCategory.TenantMismatch,
                "tenant_context_mismatch"));
        }

        if (!operationCatalogue.TryGet(item.Identity.OperationId, out var registeredOperation)
            || !registeredOperation.ExactlyMatches(item.Identity.Operation)
            || !registeredOperation.ExactlyMatches(context.Operation)
            || !registeredOperation.RequiresMandatorySecurityEvidence)
        {
            return ValueTask.FromResult(DurableWorkHandlerResult.DeadLettered(
                DurableWorkFailureCategory.ValidationFailed,
                "operation_descriptor_mismatch"));
        }

        if (!handlers.TryGetValue(item.Identity.OperationId, out var handler))
        {
            return ValueTask.FromResult(DurableWorkHandlerResult.DeadLettered(
                DurableWorkFailureCategory.ValidationFailed,
                "handler_not_registered"));
        }

        return handler(item, context, cancellationToken);
    }
}

/// <summary>Bounded worker seam that always starts from an explicit Tenant context.</summary>
public sealed class TenantDurableWorkWorker
{
    private readonly IDurableWorkStore store;
    private readonly DurableWorkDispatcher dispatcher;
    private readonly IDurableWorkAuthorityRevalidator authorityRevalidator;

    public TenantDurableWorkWorker(
        IDurableWorkStore store,
        DurableWorkDispatcher dispatcher,
        IDurableWorkAuthorityRevalidator authorityRevalidator)
    {
        this.store = store ?? throw new ArgumentNullException(nameof(store));
        this.dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
        this.authorityRevalidator = authorityRevalidator ?? throw new ArgumentNullException(nameof(authorityRevalidator));
    }

    /// <summary>Processes one item for the supplied trusted Tenant only.</summary>
    public async ValueTask<DurableWorkCompletion?> ProcessOneAsync(
        TenantContext tenantContext,
        Guid workerId,
        DateTimeOffset now,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        var lease = await store.TryClaimAsync(tenantContext, workerId, now, leaseDuration, cancellationToken);
        if (lease is null)
        {
            return null;
        }

        var item = await store.FindAsync(tenantContext, lease.WorkItemId, cancellationToken);
        if (item is null)
        {
            return await store.CompleteAsync(
                tenantContext,
                lease,
                DurableWorkHandlerResult.DeadLettered(DurableWorkFailureCategory.TenantMismatch, "stored_owner_missing"),
                now,
                cancellationToken);
        }

        var completionCancellation = cancellationToken;
        DurableWorkAuthorityValidationResult authority;
        try
        {
            authority = await authorityRevalidator.RevalidateAsync(
                item,
                tenantContext,
                now,
                cancellationToken);
        }
        catch (OperationCanceledException)
        {
            completionCancellation = CancellationToken.None;
            authority = DurableWorkAuthorityValidationResult.Cancelled("authority_validation_cancelled");
        }
        catch (Exception)
        {
            completionCancellation = CancellationToken.None;
            authority = DurableWorkAuthorityValidationResult.TemporarilyUnavailable("authority_validation_unavailable");
        }

        DurableWorkHandlerResult result;
        if (authority.Outcome == DurableWorkAuthorityValidationOutcome.Denied)
        {
            // A true live-authority denial is terminal. No handler or protected
            // effect is reachable after this branch.
            result = DurableWorkHandlerResult.DeadLettered(
                DurableWorkFailureCategory.AuthorizationDenied,
                authority.SafeReason);
        }
        else if (!authority.Allowed || authority.Authorization is null)
        {
            // Provider outages and cancellation are recoverable outcomes. The
            // completion token is intentionally detached so the claimed lease
            // is durably returned to the retry path.
            completionCancellation = CancellationToken.None;
            result = DurableWorkHandlerResult.Retry(
                authority.FailureCategory,
                TimeSpan.FromSeconds(1),
                authority.SafeReason);
        }
        else
        {
            try
            {
                var executionContext = new DurableWorkExecutionContext(item, authority.Authorization);
                result = await dispatcher.DispatchAsync(item, executionContext, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                // Persist the bounded retry even when the handler observed request
                // cancellation before the protected effect was reserved; otherwise
                // the lease would remain active until it expires and the work
                // would have no durable outcome.
                completionCancellation = CancellationToken.None;
                result = DurableWorkHandlerResult.Retry(
                    DurableWorkFailureCategory.ProviderUnavailable,
                    TimeSpan.FromSeconds(1),
                    "execution_cancelled");
            }
            catch (Exception)
            {
                result = DurableWorkHandlerResult.Retry(
                    DurableWorkFailureCategory.HandlerFailed,
                    TimeSpan.FromSeconds(1),
                    "handler_failed");
            }
        }

        return await store.CompleteAsync(tenantContext, lease, result, now, completionCancellation);
    }
}
