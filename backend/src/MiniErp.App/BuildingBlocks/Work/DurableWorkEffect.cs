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

/// <summary>
/// Required states for one protected durable-work effect. Internal: only the
/// internal effect guard and executor observe ledger state directly (H92-05);
/// the only publicly reachable evidence about an uncertain effect is the
/// scope-authorized <see cref="DurableWorkUncertainEffectRecord"/> returned by
/// <see cref="IDurableWorkStore.ReadUncertainEffectsAsync"/>.
/// </summary>
internal enum DurableWorkEffectState
{
    NotStarted = 1,
    Reserved = 2,
    Completed = 3,
    OutcomeUnknown = 4
}

internal enum DurableWorkEffectReservationKind
{
    ReservedNow = 1,
    AlreadyCompleted = 2,
    OutcomeUnknown = 3,
    InFlight = 4
}

internal sealed record DurableWorkEffectReservation(
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
/// Internal to <c>MiniErp.App</c> (H92-05): no shipping caller outside
/// <see cref="DurableWorkEffectExecutor"/> may reserve, release, complete or
/// mark an effect uncertain, and <see cref="GetOutcomeUnknownReason"/> is not
/// a public raw-key evidence path. Tests reach this port only through
/// <c>InternalsVisibleTo</c>.
/// </summary>
internal interface IDurableWorkEffectGuard
{
    DurableWorkEffectState GetState(DurableWorkEffectKey key);

    DurableWorkEffectReservation TryReserve(DurableWorkEffectKey key);

    void RecordCompleted(DurableWorkEffectKey key, DurableWorkHandlerResult safeResult);

    void RecordOutcomeUnknown(DurableWorkEffectKey key, string safeReason);

    void Release(DurableWorkEffectKey key);

    /// <summary>
    /// Returns the exact safe reason preserved when <paramref name="key"/> was
    /// recorded as <see cref="DurableWorkEffectState.OutcomeUnknown"/>, or null
    /// when the key does not currently hold that state. Read-only: it exposes
    /// no mutation surface (O92-01).
    /// </summary>
    string? GetOutcomeUnknownReason(DurableWorkEffectKey key);
}

/// <summary>
/// Deterministic in-memory effect guard. A local Foundation seam only; not
/// crash-durable. The type and its constructor are internal (H92-05):
/// shipping code obtains the one approved instance only through
/// <see cref="DurableWorkLocalRuntime"/>, so a second, independent production
/// effect ledger cannot be created and no shipping caller outside this
/// assembly's approved effect executor can reach the ledger directly. Tests
/// use this constructor directly through InternalsVisibleTo.
/// </summary>
internal sealed class InMemoryDurableWorkEffectGuard : IDurableWorkEffectGuard
{
    private readonly object syncRoot = new();
    private readonly Dictionary<DurableWorkEffectKey, EffectRecord> records = [];

    internal InMemoryDurableWorkEffectGuard()
    {
    }

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

            records[key] = new EffectRecord(DurableWorkEffectState.Reserved, null, null);
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

            records[key] = new EffectRecord(DurableWorkEffectState.Completed, safeResult, null);
        }
    }

    public void RecordOutcomeUnknown(DurableWorkEffectKey key, string safeReason)
    {
        ArgumentNullException.ThrowIfNull(key);
        var sanitizedReason = SanitizeSafeReason(safeReason);
        lock (syncRoot)
        {
            if (!records.TryGetValue(key, out var existing) || existing.State != DurableWorkEffectState.Reserved)
            {
                throw new InvalidOperationException("Outcome-unknown can be recorded only from a reserved state.");
            }

            // The reserved -> OutcomeUnknown transition is one-way: once this
            // key holds an OutcomeUnknown record its state is no longer
            // Reserved, so a later call always fails the guard above instead
            // of replacing the original preserved reason.
            records[key] = new EffectRecord(DurableWorkEffectState.OutcomeUnknown, null, sanitizedReason);
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

    public string? GetOutcomeUnknownReason(DurableWorkEffectKey key)
    {
        ArgumentNullException.ThrowIfNull(key);
        lock (syncRoot)
        {
            return records.TryGetValue(key, out var existing) && existing.State == DurableWorkEffectState.OutcomeUnknown
                ? existing.SafeReason
                : null;
        }
    }

    private static string SanitizeSafeReason(string safeReason)
    {
        if (string.IsNullOrWhiteSpace(safeReason) || safeReason.Trim().Length > 128 || safeReason.Any(char.IsControl))
        {
            throw new ArgumentException("Outcome-unknown safe reason must be safe and bounded.", nameof(safeReason));
        }

        return safeReason.Trim();
    }

    private sealed record EffectRecord(DurableWorkEffectState State, DurableWorkHandlerResult? Result, string? SafeReason);
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
internal enum DurableWorkEffectExecutionKind
{
    Executed = 1,
    Replayed = 2,
    InFlight = 3,
    OutcomeUnknown = 4
}

/// <summary>Safe outcome returned by the effect executor.</summary>
internal sealed record DurableWorkEffectExecution(
    DurableWorkEffectExecutionKind Kind,
    DurableWorkHandlerResult Result);

/// <summary>
/// Orchestrates one protected durable-work effect through the effect guard.
/// A caught post-boundary exception, a caught cancellation, or a
/// completion-recording failure observed by the running process, after the
/// reservation boundary, never causes automatic effect repetition -- it
/// records OutcomeUnknown instead. The protected callback must return an
/// explicit <see cref="DurableWorkProtectedEffectResult"/>; a bare generic
/// retry is not a representable outcome once the boundary has been crossed.
/// An actual process crash is a distinct, unhandled failure mode: it loses
/// this in-memory guard's state entirely rather than recording
/// OutcomeUnknown. Production durable crash recovery remains deferred.
/// Internal to <c>MiniErp.App</c> (H92-05): every registered handler
/// invocation and outbox effect is routed through this executor only from
/// inside <see cref="InMemoryDurableWorkStore"/> and <see cref="DurableWorkDispatcher"/>;
/// no shipping caller outside this assembly can invoke a protected effect
/// directly.
/// </summary>
internal interface IDurableWorkEffectExecutor
{
    ValueTask<DurableWorkEffectExecution> ExecuteHandlerEffectAsync(
        DurableWorkEffectKey key,
        Func<CancellationToken, ValueTask<DurableWorkProtectedEffectResult>> protectedEffect,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Default effect executor bound to one <see cref="IDurableWorkEffectGuard"/>.
/// The type and its constructor are internal (H92-05): shipping code obtains
/// the one approved instance only through <see cref="DurableWorkLocalRuntime"/>.
/// Tests use this constructor directly through InternalsVisibleTo.
/// </summary>
internal sealed class DurableWorkEffectExecutor : IDurableWorkEffectExecutor
{
    private readonly IDurableWorkEffectGuard guard;

    internal DurableWorkEffectExecutor(IDurableWorkEffectGuard guard)
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
