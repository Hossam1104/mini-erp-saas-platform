#pragma warning disable CS1591

using MiniErp.App.BuildingBlocks.Tenancy;

namespace MiniErp.App.BuildingBlocks.Work;

/// <summary>
/// Deterministic local adapter for the relational durable-work seam. It is
/// intentionally bounded and is never a production database or provider.
/// </summary>
public sealed class InMemoryRelationalDurableWorkStore : IRelationalDurableWorkStore
{
    private readonly object syncRoot = new();
    private readonly Dictionary<Guid, DurableWorkItem> workItems = [];
    private readonly Dictionary<(TenantId TenantId, string Key), Guid> idempotency = [];
    private readonly Dictionary<Guid, TenantOutboxMessage> outbox = [];
    private readonly HashSet<(TenantId TenantId, Guid EventId)> inbox = [];
    private readonly List<DurableWorkAuditRecord> audit = [];

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
                    _ => "work.transition"
                };
                AddAudit(item, eventName, item.Lifecycle, completion.FailureCategory);
            }

            return ValueTask.FromResult(completion);
        }
    }

    /// <summary>Dispatches one Tenant-owned outbox message with inbox deduplication.</summary>
    public async ValueTask<OutboxDispatchResult> DispatchOutboxAsync(
        TenantContext tenantContext,
        DateTimeOffset now,
        Func<TenantOutboxMessage, CancellationToken, ValueTask> effect,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(effect);
        cancellationToken.ThrowIfCancellationRequested();
        TenantOutboxMessage? message;
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
                return new OutboxDispatchResult(false, false, false, false, DurableWorkFailureCategory.None);
            }

            if (!inbox.Add((tenantContext.TenantId, message.EventId)))
            {
                message.DeliveryState = DurableWorkLifecycle.Completed;
                AddAuditForMessage(message, "outbox.duplicate", DurableWorkFailureCategory.None);
                return new OutboxDispatchResult(true, true, false, false, DurableWorkFailureCategory.None);
            }

            message.DeliveryState = DurableWorkLifecycle.Claimed;
            message.AttemptCount++;
            AddAuditForMessage(message, "outbox.dispatch", DurableWorkFailureCategory.None);
        }

        try
        {
            await effect(message, cancellationToken);
            lock (syncRoot)
            {
                message.DeliveryState = DurableWorkLifecycle.Completed;
                AddAuditForMessage(message, "outbox.delivered", DurableWorkFailureCategory.None);
            }

            return new OutboxDispatchResult(true, false, false, false, DurableWorkFailureCategory.None);
        }
        catch (OperationCanceledException)
        {
            lock (syncRoot)
            {
                inbox.Remove((tenantContext.TenantId, message.EventId));
                message.DeliveryState = DurableWorkLifecycle.RetryScheduled;
                message.NextAttemptAt = now.Add(BoundedBackoff(message.AttemptCount));
                AddAuditForMessage(message, "outbox.retry", DurableWorkFailureCategory.ProviderUnavailable);
            }

            throw;
        }
        catch (Exception)
        {
            lock (syncRoot)
            {
                inbox.Remove((tenantContext.TenantId, message.EventId));
                var terminal = message.AttemptCount >= 3;
                message.DeliveryState = terminal ? DurableWorkLifecycle.DeadLetter : DurableWorkLifecycle.RetryScheduled;
                message.NextAttemptAt = now.Add(BoundedBackoff(message.AttemptCount));
                AddAuditForMessage(message, terminal ? "outbox.dead-letter" : "outbox.retry", DurableWorkFailureCategory.ProviderUnavailable);
                return new OutboxDispatchResult(false, false, !terminal, terminal, DurableWorkFailureCategory.ProviderUnavailable);
            }
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
    /// Test-only replay hook. It models a redelivered outbox row while retaining
    /// the inbox marker; production callers cannot access the local adapter's
    /// internal validation hooks.
    /// </summary>
    internal bool ReplayOutboxForValidation(Guid workItemId, DateTimeOffset nextAttemptAt)
    {
        lock (syncRoot)
        {
            var message = outbox.Values.FirstOrDefault(item => item.WorkItemId == workItemId);
            if (message is null)
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

/// <summary>One typed handler invocation; no global Tenant scan is available.</summary>
public sealed class DurableWorkDispatcher
{
    private readonly Dictionary<string, Func<DurableWorkItem, DurableWorkExecutionContext, CancellationToken, ValueTask<DurableWorkHandlerResult>>> handlers =
        new(StringComparer.Ordinal);

    public void Register<TPayload>(IDurableWorkHandler<TPayload> handler)
        where TPayload : IWorkPayload
    {
        ArgumentNullException.ThrowIfNull(handler);
        if (string.IsNullOrWhiteSpace(handler.WorkType))
        {
            throw new ArgumentException("A typed handler requires a work type.", nameof(handler));
        }

        if (!handlers.TryAdd(handler.WorkType.Trim(), Invoke))
        {
            throw new InvalidOperationException("A durable-work handler is already registered for this type.");
        }

        async ValueTask<DurableWorkHandlerResult> Invoke(
            DurableWorkItem item,
            DurableWorkExecutionContext context,
            CancellationToken cancellationToken)
        {
            if (item.Payload is not TPayload payload)
            {
                return DurableWorkHandlerResult.DeadLettered(
                    DurableWorkFailureCategory.ValidationFailed,
                    "typed_payload_mismatch");
            }

            return await handler.ExecuteAsync(payload, context, cancellationToken);
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

        if (!handlers.TryGetValue(item.Identity.WorkType, out var handler))
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
    private readonly IRelationalDurableWorkStore store;
    private readonly DurableWorkDispatcher dispatcher;

    public TenantDurableWorkWorker(
        IRelationalDurableWorkStore store,
        DurableWorkDispatcher dispatcher)
    {
        this.store = store ?? throw new ArgumentNullException(nameof(store));
        this.dispatcher = dispatcher ?? throw new ArgumentNullException(nameof(dispatcher));
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

        DurableWorkHandlerResult result;
        var completionCancellation = cancellationToken;
        try
        {
            var executionContext = new DurableWorkExecutionContext(item, tenantContext);
            result = await dispatcher.DispatchAsync(item, executionContext, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            // Persist the bounded retry even when the handler observed request
            // cancellation; otherwise the lease would remain active until it
            // expires and the work would have no durable outcome.
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

        return await store.CompleteAsync(tenantContext, lease, result, now, completionCancellation);
    }
}
