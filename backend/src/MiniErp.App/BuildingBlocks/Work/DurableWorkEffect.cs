#pragma warning disable CS1591

using MiniErp.App.BuildingBlocks.Tenancy;

namespace MiniErp.App.BuildingBlocks.Work;

/// <summary>
/// Server-owned category of one protected durable-work effect. Two effects
/// with an identical Tenant, work item and operation but a different purpose
/// are independent authorities and must never suppress each other.
/// </summary>
public enum DurableWorkEffectPurpose
{
    Handler = 1,
    Outbox = 2
}

/// <summary>
/// Server-owned identity for one protected durable-work effect. The purpose
/// namespaces the key so a handler effect and an outbox effect for the same
/// work item can never collide; an outbox effect additionally carries the
/// immutable <see cref="EventId"/> so redelivery of the same event resolves
/// to the same key while two different events remain independent.
/// </summary>
public sealed class DurableWorkEffectKey : IEquatable<DurableWorkEffectKey>
{
    private DurableWorkEffectKey(
        TenantId tenantId,
        DurableWorkEffectPurpose purpose,
        Guid workItemId,
        string operationId,
        Guid? eventId)
    {
        if (workItemId == Guid.Empty)
        {
            throw new ArgumentException("Work item identifier must not be empty.", nameof(workItemId));
        }

        if (string.IsNullOrWhiteSpace(operationId) || operationId.Trim().Length > 128)
        {
            throw new ArgumentException("Operation identifier is required and bounded.", nameof(operationId));
        }

        if (purpose == DurableWorkEffectPurpose.Outbox && (eventId is null || eventId.Value == Guid.Empty))
        {
            throw new ArgumentException(
                "An outbox effect key requires a non-empty immutable outbox event identifier.",
                nameof(eventId));
        }

        if (purpose == DurableWorkEffectPurpose.Handler && eventId is not null)
        {
            throw new ArgumentException(
                "A handler effect key must not carry an outbox event identifier.",
                nameof(eventId));
        }

        TenantId = tenantId;
        Purpose = purpose;
        WorkItemId = workItemId;
        OperationId = operationId.Trim();
        EventId = eventId;
    }

    public TenantId TenantId { get; }

    public DurableWorkEffectPurpose Purpose { get; }

    public Guid WorkItemId { get; }

    public string OperationId { get; }

    /// <summary>Immutable outbox event identifier. Always null for a handler-purpose key.</summary>
    public Guid? EventId { get; }

    /// <summary>Stable handler-effect identity: WorkItemId + OperationId.</summary>
    public static DurableWorkEffectKey ForHandlerEffect(TenantId tenantId, Guid workItemId, string operationId) =>
        new(tenantId, DurableWorkEffectPurpose.Handler, workItemId, operationId, eventId: null);

    /// <summary>Stable outbox-effect identity: the immutable EventId, scoped to the work item and operation.</summary>
    public static DurableWorkEffectKey ForOutboxEffect(
        TenantId tenantId,
        Guid workItemId,
        string operationId,
        Guid eventId) =>
        new(tenantId, DurableWorkEffectPurpose.Outbox, workItemId, operationId, eventId);

    public bool Equals(DurableWorkEffectKey? other) =>
        other is not null
        && TenantId == other.TenantId
        && Purpose == other.Purpose
        && WorkItemId == other.WorkItemId
        && string.Equals(OperationId, other.OperationId, StringComparison.Ordinal)
        && EventId == other.EventId;

    public override bool Equals(object? obj) => Equals(obj as DurableWorkEffectKey);

    public override int GetHashCode() => HashCode.Combine(TenantId, Purpose, WorkItemId, OperationId, EventId);
}

/// <summary>Required states for one protected durable-work effect.</summary>
public enum DurableWorkEffectState
{
    NotStarted = 1,
    Reserved = 2,
    Completed = 3,
    OutcomeUnknown = 4
}

public enum DurableWorkEffectReservationKind
{
    ReservedNow = 1,
    AlreadyCompleted = 2,
    OutcomeUnknown = 3,
    InFlight = 4
}

public sealed record DurableWorkEffectReservation(
    DurableWorkEffectReservationKind Kind,
    DurableWorkHandlerResult? CompletedResult)
{
    internal static DurableWorkEffectReservation ReservedNow() => new(DurableWorkEffectReservationKind.ReservedNow, null);

    internal static DurableWorkEffectReservation AlreadyCompleted(DurableWorkHandlerResult result) =>
        new(DurableWorkEffectReservationKind.AlreadyCompleted, result);

    internal static DurableWorkEffectReservation Unknown() => new(DurableWorkEffectReservationKind.OutcomeUnknown, null);

    internal static DurableWorkEffectReservation InFlight() => new(DurableWorkEffectReservationKind.InFlight, null);
}

/// <summary>
/// Narrow port guarding one protected durable-work effect. Reservation is the
/// single non-reversible boundary: once reserved, only <see cref="RecordCompleted"/>,
/// <see cref="RecordOutcomeUnknown"/> or <see cref="Release"/> may resolve it.
/// One shared guard instance is the single authoritative ledger for every
/// purpose-qualified effect key across the store and the dispatcher.
/// </summary>
public interface IDurableWorkEffectGuard
{
    DurableWorkEffectState GetState(DurableWorkEffectKey key);

    DurableWorkEffectReservation TryReserve(DurableWorkEffectKey key);

    void RecordCompleted(DurableWorkEffectKey key, DurableWorkHandlerResult safeResult);

    void RecordOutcomeUnknown(DurableWorkEffectKey key, string safeReason);

    void Release(DurableWorkEffectKey key);
}

/// <summary>Deterministic in-memory effect guard. A local Foundation seam only; not crash-durable.</summary>
public sealed class InMemoryDurableWorkEffectGuard : IDurableWorkEffectGuard
{
    private readonly object syncRoot = new();
    private readonly Dictionary<DurableWorkEffectKey, EffectRecord> records = [];

    public DurableWorkEffectState GetState(DurableWorkEffectKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        lock (syncRoot)
        {
            return records.TryGetValue(key, out var existing) ? existing.State : DurableWorkEffectState.NotStarted;
        }
    }

    public DurableWorkEffectReservation TryReserve(DurableWorkEffectKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        lock (syncRoot)
        {
            if (records.TryGetValue(key, out var existing))
            {
                return existing.State switch
                {
                    DurableWorkEffectState.Completed => DurableWorkEffectReservation.AlreadyCompleted(existing.Result!),
                    DurableWorkEffectState.OutcomeUnknown => DurableWorkEffectReservation.Unknown(),
                    _ => DurableWorkEffectReservation.InFlight()
                };
            }

            records[key] = new EffectRecord(DurableWorkEffectState.Reserved, null);
            return DurableWorkEffectReservation.ReservedNow();
        }
    }

    public void RecordCompleted(DurableWorkEffectKey key, DurableWorkHandlerResult safeResult)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(safeResult);
        lock (syncRoot)
        {
            if (!records.TryGetValue(key, out var existing) || existing.State != DurableWorkEffectState.Reserved)
            {
                throw new InvalidOperationException("An effect can be completed only from a reserved state.");
            }

            records[key] = new EffectRecord(DurableWorkEffectState.Completed, safeResult);
        }
    }

    public void RecordOutcomeUnknown(DurableWorkEffectKey key, string safeReason)
    {
        ArgumentNullException.ThrowIfNull(key);
        lock (syncRoot)
        {
            if (!records.TryGetValue(key, out var existing) || existing.State != DurableWorkEffectState.Reserved)
            {
                throw new InvalidOperationException("Outcome-unknown can be recorded only from a reserved state.");
            }

            records[key] = new EffectRecord(DurableWorkEffectState.OutcomeUnknown, null);
        }
    }

    public void Release(DurableWorkEffectKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        lock (syncRoot)
        {
            if (records.TryGetValue(key, out var existing) && existing.State == DurableWorkEffectState.Reserved)
            {
                records.Remove(key);
            }
        }
    }

    private sealed record EffectRecord(DurableWorkEffectState State, DurableWorkHandlerResult? Result);
}

/// <summary>Explicit outcome of one protected-effect attempt made after the reservation boundary.</summary>
public enum DurableWorkProtectedEffectOutcome
{
    Applied = 1,
    NotAppliedRetryable = 2,
    OutcomeUnknown = 3,
    TerminalNotApplied = 4
}

/// <summary>
/// Explicit, provider-reported result of one protected-effect attempt. A
/// generic <see cref="DurableWorkHandlerResult"/> retry is never proof that an
/// effect was not applied; only this contract, returned from inside the
/// effect executor, may resolve the reservation. <see cref="Applied"/> and
/// <see cref="TerminalNotApplied"/> record a safe terminal outcome and never
/// automatically repeat. <see cref="NotAppliedRetryable"/> is the only
/// outcome that releases the reservation for a bounded retry, and it must be
/// used only when the provider/handler positively confirms no protected
/// effect occurred. <see cref="OutcomeUnknown"/> never automatically repeats
/// and requires reconciliation.
/// </summary>
public sealed class DurableWorkProtectedEffectResult
{
    private DurableWorkProtectedEffectResult(
        DurableWorkProtectedEffectOutcome outcome,
        DurableWorkHandlerResult safeResult)
    {
        Outcome = outcome;
        SafeResult = safeResult;
    }

    public DurableWorkProtectedEffectOutcome Outcome { get; }

    public DurableWorkHandlerResult SafeResult { get; }

    public static DurableWorkProtectedEffectResult Applied(DurableWorkHandlerResult safeResult)
    {
        ArgumentNullException.ThrowIfNull(safeResult);
        if (!safeResult.Success)
        {
            throw new ArgumentException("An Applied protected effect must carry a successful safe result.", nameof(safeResult));
        }

        return new(DurableWorkProtectedEffectOutcome.Applied, safeResult);
    }

    public static DurableWorkProtectedEffectResult NotAppliedRetryable(
        DurableWorkFailureCategory category,
        TimeSpan retryAfter,
        string safeReason) =>
        new(
            DurableWorkProtectedEffectOutcome.NotAppliedRetryable,
            DurableWorkHandlerResult.Retry(category, retryAfter, safeReason));

    public static DurableWorkProtectedEffectResult OutcomeUnknown(string safeReason) =>
        new(
            DurableWorkProtectedEffectOutcome.OutcomeUnknown,
            DurableWorkHandlerResult.OutcomeUnknown(safeReason));

    public static DurableWorkProtectedEffectResult TerminalNotApplied(
        DurableWorkFailureCategory category,
        string safeReason) =>
        new(
            DurableWorkProtectedEffectOutcome.TerminalNotApplied,
            DurableWorkHandlerResult.DeadLettered(category, safeReason));
}

/// <summary>Outcome of one attempted protected-effect execution.</summary>
public enum DurableWorkEffectExecutionKind
{
    Executed = 1,
    Replayed = 2,
    InFlight = 3,
    OutcomeUnknown = 4
}

/// <summary>Safe outcome returned by the effect executor.</summary>
public sealed record DurableWorkEffectExecution(
    DurableWorkEffectExecutionKind Kind,
    DurableWorkHandlerResult Result);

/// <summary>
/// Orchestrates one protected durable-work effect through the effect guard.
/// A crash, cancellation or state-recording failure after the reservation
/// boundary never causes automatic effect repetition. The protected callback
/// must return an explicit <see cref="DurableWorkProtectedEffectResult"/>; a
/// bare generic retry is not a representable outcome once the boundary has
/// been crossed.
/// </summary>
public interface IDurableWorkEffectExecutor
{
    ValueTask<DurableWorkEffectExecution> ExecuteHandlerEffectAsync(
        DurableWorkEffectKey key,
        Func<CancellationToken, ValueTask<DurableWorkProtectedEffectResult>> protectedEffect,
        CancellationToken cancellationToken = default);
}

/// <summary>Default effect executor bound to one <see cref="IDurableWorkEffectGuard"/>.</summary>
public sealed class DurableWorkEffectExecutor : IDurableWorkEffectExecutor
{
    private readonly IDurableWorkEffectGuard guard;

    public DurableWorkEffectExecutor(IDurableWorkEffectGuard guard)
    {
        this.guard = guard ?? throw new ArgumentNullException(nameof(guard));
    }

    public async ValueTask<DurableWorkEffectExecution> ExecuteHandlerEffectAsync(
        DurableWorkEffectKey key,
        Func<CancellationToken, ValueTask<DurableWorkProtectedEffectResult>> protectedEffect,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(key);
        ArgumentNullException.ThrowIfNull(protectedEffect);

        // A pre-boundary cancellation never touches the guard; the caller may
        // safely retry a later attempt.
        cancellationToken.ThrowIfCancellationRequested();

        var reservation = guard.TryReserve(key);
        switch (reservation.Kind)
        {
            case DurableWorkEffectReservationKind.AlreadyCompleted:
                return new DurableWorkEffectExecution(
                    DurableWorkEffectExecutionKind.Replayed,
                    reservation.CompletedResult!);
            case DurableWorkEffectReservationKind.OutcomeUnknown:
                return new DurableWorkEffectExecution(
                    DurableWorkEffectExecutionKind.OutcomeUnknown,
                    DurableWorkHandlerResult.OutcomeUnknown("outcome_unknown_requires_reconciliation"));
            case DurableWorkEffectReservationKind.InFlight:
                return new DurableWorkEffectExecution(
                    DurableWorkEffectExecutionKind.InFlight,
                    DurableWorkHandlerResult.Retry(
                        DurableWorkFailureCategory.ConcurrencyConflict,
                        TimeSpan.FromSeconds(1),
                        "effect_in_flight"));
        }

        // The reservation is the single non-reversible boundary. From here,
        // any interruption is OutcomeUnknown; nothing may auto-retry.
        DurableWorkProtectedEffectResult protectedResult;
        try
        {
            protectedResult = await protectedEffect(cancellationToken);
        }
        catch (Exception)
        {
            SafeRecordOutcomeUnknown(key, "effect_boundary_interrupted");
            return new DurableWorkEffectExecution(
                DurableWorkEffectExecutionKind.OutcomeUnknown,
                DurableWorkHandlerResult.OutcomeUnknown("effect_outcome_unknown"));
        }

        if (protectedResult.Outcome == DurableWorkProtectedEffectOutcome.OutcomeUnknown)
        {
            SafeRecordOutcomeUnknown(key, protectedResult.SafeResult.SafeReason ?? "effect_outcome_unknown");
            return new DurableWorkEffectExecution(DurableWorkEffectExecutionKind.OutcomeUnknown, protectedResult.SafeResult);
        }

        var result = protectedResult.SafeResult;
        if (result.Success || result.DeadLetter)
        {
            // Applied or TerminalNotApplied: both are safe terminal outcomes
            // that must never automatically repeat.
            try
            {
                guard.RecordCompleted(key, result);
            }
            catch (Exception)
            {
                SafeRecordOutcomeUnknown(key, "completion_recording_failed");
                return new DurableWorkEffectExecution(
                    DurableWorkEffectExecutionKind.OutcomeUnknown,
                    DurableWorkHandlerResult.OutcomeUnknown("completion_recording_failed"));
            }
        }
        else
        {
            // Only an explicit NotAppliedRetryable outcome reaches this
            // branch; it is the sole outcome permitted to release the
            // reservation for a future bounded retry.
            guard.Release(key);
        }

        return new DurableWorkEffectExecution(DurableWorkEffectExecutionKind.Executed, result);
    }

    private void SafeRecordOutcomeUnknown(DurableWorkEffectKey key, string safeReason)
    {
        try
        {
            guard.RecordOutcomeUnknown(key, safeReason);
        }
        catch
        {
            // Best effort only: the still-Reserved record alone already blocks
            // automatic re-execution of this effect key.
        }
    }
}

/// <summary>
/// Single authoritative composition seam for the durable-work effect guard.
/// The store's outbox dispatch and the dispatcher's handler execution must
/// share one <see cref="IDurableWorkEffectExecutor"/> instance so every
/// protected effect for a Tenant is guarded by one ledger; the purpose and
/// EventId inside <see cref="DurableWorkEffectKey"/> keep handler and outbox
/// effects independent within that one shared authority.
/// </summary>
public static class DurableWorkEffectComposition
{
    public static IDurableWorkEffectExecutor CreateSharedExecutor() =>
        new DurableWorkEffectExecutor(new InMemoryDurableWorkEffectGuard());
}
