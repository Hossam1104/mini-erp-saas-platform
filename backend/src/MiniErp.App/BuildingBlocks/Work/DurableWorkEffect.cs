#pragma warning disable CS1591

using MiniErp.App.BuildingBlocks.Tenancy;

namespace MiniErp.App.BuildingBlocks.Work;

/// <summary>Server-owned identity for one protected durable-work effect.</summary>
public sealed class DurableWorkEffectKey : IEquatable<DurableWorkEffectKey>
{
    public DurableWorkEffectKey(TenantId tenantId, Guid workItemId, string operationId)
    {
        if (workItemId == Guid.Empty)
        {
            throw new ArgumentException("Work item identifier must not be empty.", nameof(workItemId));
        }

        if (string.IsNullOrWhiteSpace(operationId) || operationId.Trim().Length > 128)
        {
            throw new ArgumentException("Operation identifier is required and bounded.", nameof(operationId));
        }

        TenantId = tenantId;
        WorkItemId = workItemId;
        OperationId = operationId.Trim();
    }

    public TenantId TenantId { get; }

    public Guid WorkItemId { get; }

    public string OperationId { get; }

    public bool Equals(DurableWorkEffectKey? other) =>
        other is not null
        && TenantId == other.TenantId
        && WorkItemId == other.WorkItemId
        && string.Equals(OperationId, other.OperationId, StringComparison.Ordinal);

    public override bool Equals(object? obj) => Equals(obj as DurableWorkEffectKey);

    public override int GetHashCode() => HashCode.Combine(TenantId, WorkItemId, OperationId);
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
/// boundary never causes automatic effect repetition.
/// </summary>
public interface IDurableWorkEffectExecutor
{
    ValueTask<DurableWorkEffectExecution> ExecuteHandlerEffectAsync(
        DurableWorkEffectKey key,
        Func<CancellationToken, ValueTask<DurableWorkHandlerResult>> protectedEffect,
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
        Func<CancellationToken, ValueTask<DurableWorkHandlerResult>> protectedEffect,
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
                    DurableWorkHandlerResult.DeadLettered(
                        DurableWorkFailureCategory.Unknown,
                        "outcome_unknown_requires_reconciliation"));
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
        DurableWorkHandlerResult result;
        try
        {
            result = await protectedEffect(cancellationToken);
        }
        catch (Exception)
        {
            SafeRecordOutcomeUnknown(key, "effect_boundary_interrupted");
            return new DurableWorkEffectExecution(
                DurableWorkEffectExecutionKind.OutcomeUnknown,
                DurableWorkHandlerResult.DeadLettered(DurableWorkFailureCategory.Unknown, "effect_outcome_unknown"));
        }

        if (result.Success || result.DeadLetter)
        {
            try
            {
                guard.RecordCompleted(key, result);
            }
            catch (Exception)
            {
                SafeRecordOutcomeUnknown(key, "completion_recording_failed");
                return new DurableWorkEffectExecution(
                    DurableWorkEffectExecutionKind.OutcomeUnknown,
                    DurableWorkHandlerResult.DeadLettered(DurableWorkFailureCategory.Unknown, "completion_recording_failed"));
            }
        }
        else
        {
            // A deliberate bounded-retry verdict releases the reservation so a
            // future legitimate attempt may run the effect again.
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
