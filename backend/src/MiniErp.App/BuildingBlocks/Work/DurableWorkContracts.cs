#pragma warning disable CS1591

using MiniErp.App.BuildingBlocks.Tenancy;

namespace MiniErp.App.BuildingBlocks.Work;

/// <summary>Lifecycle states for a Tenant-owned durable work item.</summary>
public enum DurableWorkLifecycle
{
    Pending = 1,
    Claimed = 2,
    Completed = 3,
    RetryScheduled = 4,
    DeadLetter = 5
}

/// <summary>Safe, provider-neutral failure categories.</summary>
public enum DurableWorkFailureCategory
{
    None = 0,
    ValidationFailed = 1,
    TenantMismatch = 2,
    AuthorizationDenied = 3,
    ConcurrencyConflict = 4,
    HandlerFailed = 5,
    ProviderUnavailable = 6,
    Unknown = 7
}

/// <summary>Safe notification delivery states.</summary>
public enum NotificationDeliveryState
{
    Pending = 1,
    Delivered = 2,
    RetryScheduled = 3,
    DeadLetter = 4,
    Duplicate = 5
}

/// <summary>Safe file metadata dispositions. No physical purge is implied.</summary>
public enum PrivateFileDisposition
{
    Available = 1,
    Expired = 2,
    Disposed = 3,
    ChecksumFailed = 4
}

/// <summary>Safe file-access outcomes that do not reveal foreign targets.</summary>
public enum PrivateFileAccessOutcome
{
    Allowed = 1,
    NotFound = 2,
    TenantDenied = 3,
    AnonymousDenied = 4,
    Expired = 5,
    ChecksumFailed = 6,
    ConcurrencyConflict = 7
}

/// <summary>Marker for a handler-specific, typed durable-work payload.</summary>
public interface IWorkPayload
{
}

/// <summary>Validated organization scope derived by trusted server code.</summary>
public sealed class TenantWorkScope
{
    private TenantWorkScope(
        TenantId tenantId,
        Guid? companyId,
        Guid? branchId,
        Guid? warehouseId)
    {
        TenantId = tenantId;
        CompanyId = companyId;
        BranchId = branchId;
        WarehouseId = warehouseId;
    }

    public TenantId TenantId { get; }

    public Guid? CompanyId { get; }

    public Guid? BranchId { get; }

    public Guid? WarehouseId { get; }

    /// <summary>Creates a scope only from a trusted TenantContext.</summary>
    public static TenantWorkScope ForServerContext(
        TenantContext tenantContext,
        Guid? companyId = null,
        Guid? branchId = null,
        Guid? warehouseId = null)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        ValidateIdentifiers(companyId, branchId, warehouseId);
        if (branchId.HasValue && !companyId.HasValue)
        {
            throw new ArgumentException("A branch scope requires its company scope.", nameof(companyId));
        }

        if (warehouseId.HasValue && !branchId.HasValue)
        {
            throw new ArgumentException("A warehouse scope requires its branch scope.", nameof(branchId));
        }

        return new TenantWorkScope(tenantContext.TenantId, companyId, branchId, warehouseId);
    }

    private static void ValidateIdentifiers(Guid? companyId, Guid? branchId, Guid? warehouseId)
    {
        if (companyId == Guid.Empty || branchId == Guid.Empty || warehouseId == Guid.Empty)
        {
            throw new ArgumentException("Organization scope identifiers must not be empty.");
        }
    }
}

/// <summary>Immutable initiating authorization facts for durable work.</summary>
public sealed class DurableWorkInitiator
{
    private DurableWorkInitiator(
        TenantId tenantId,
        TenantAuthorizationPath authorizationPath,
        MembershipReference? membership,
        SupportGrantReference? supportGrant,
        ScopeReference? scope,
        Guid? actorId,
        Guid? sessionId,
        CorrelationId correlationId)
    {
        TenantId = tenantId;
        AuthorizationPath = authorizationPath;
        Membership = membership;
        SupportGrant = supportGrant;
        Scope = scope;
        ActorId = actorId;
        SessionId = sessionId;
        CorrelationId = correlationId;
    }

    public TenantId TenantId { get; }

    public TenantAuthorizationPath AuthorizationPath { get; }

    public MembershipReference? Membership { get; }

    public SupportGrantReference? SupportGrant { get; }

    public ScopeReference? Scope { get; }

    public Guid? ActorId { get; }

    public Guid? SessionId { get; }

    public CorrelationId CorrelationId { get; }

    /// <summary>Captures only server-derived authorization facts.</summary>
    public static DurableWorkInitiator FromServerContext(
        TenantContext tenantContext,
        Guid? sessionId = null)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        if (sessionId == Guid.Empty)
        {
            throw new ArgumentException("Session identifier must not be empty.", nameof(sessionId));
        }

        return new DurableWorkInitiator(
            tenantContext.TenantId,
            tenantContext.AuthorizationPath,
            tenantContext.Membership,
            tenantContext.SupportGrant,
            tenantContext.Scope,
            tenantContext.ActorId,
            sessionId,
            tenantContext.CorrelationId
                ?? throw new ArgumentException("Durable work requires a trusted correlation identifier.", nameof(tenantContext)));
    }
}

/// <summary>Stable identity for one durable work item.</summary>
public sealed class DurableWorkIdentity
{
    public DurableWorkIdentity(
        Guid workItemId,
        string operationId,
        CorrelationId correlationId,
        string idempotencyKey,
        string workType)
    {
        if (workItemId == Guid.Empty)
        {
            throw new ArgumentException("Work item identifier must not be empty.", nameof(workItemId));
        }

        WorkItemId = workItemId;
        OperationId = Required(operationId, nameof(operationId));
        CorrelationId = correlationId.Value is { Length: > 0 }
            ? correlationId
            : throw new ArgumentException("Correlation identifier is required.", nameof(correlationId));
        IdempotencyKey = Required(idempotencyKey, nameof(idempotencyKey));
        WorkType = Required(workType, nameof(workType));
    }

    public Guid WorkItemId { get; }

    public string OperationId { get; }

    public CorrelationId CorrelationId { get; }

    public string IdempotencyKey { get; }

    public string WorkType { get; }

    private static string Required(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > 128)
        {
            throw new ArgumentException("The work identity value is required and bounded.", name);
        }

        var normalized = value.Trim();
        if (normalized.Any(char.IsControl))
        {
            throw new ArgumentException("The work identity value contains a control character.", name);
        }

        return normalized;
    }
}

/// <summary>Tenant-owned work item with immutable ownership and typed payload.</summary>
public sealed class DurableWorkItem : ITenantOwned
{
    private DurableWorkItem(
        DurableWorkIdentity identity,
        DurableWorkInitiator initiator,
        TenantWorkScope scope,
        IWorkPayload payload,
        int maximumAttempts,
        DateTimeOffset createdAt)
    {
        Identity = identity;
        Initiator = initiator;
        Scope = scope;
        Payload = payload;
        MaximumAttempts = maximumAttempts;
        CreatedAt = createdAt;
        UpdatedAt = createdAt;
        NextAttemptAt = createdAt;
        Lifecycle = DurableWorkLifecycle.Pending;
        ConcurrencyVersion = 1;
    }

    public DurableWorkIdentity Identity { get; }

    public TenantId TenantId => Initiator.TenantId;

    public DurableWorkInitiator Initiator { get; }

    public TenantWorkScope Scope { get; }

    public IWorkPayload Payload { get; }

    public int MaximumAttempts { get; }

    public int AttemptCount { get; private set; }

    public DateTimeOffset NextAttemptAt { get; private set; }

    public Guid? LeaseOwnerId { get; private set; }

    public DateTimeOffset? LeaseExpiresAt { get; private set; }

    public DateTimeOffset CreatedAt { get; }

    public DateTimeOffset UpdatedAt { get; private set; }

    public long ConcurrencyVersion { get; private set; }

    public DurableWorkLifecycle Lifecycle { get; private set; }

    public DurableWorkFailureCategory FailureCategory { get; private set; }

    public string? SafeFailureReason { get; private set; }

    public bool IsDeadLettered => Lifecycle == DurableWorkLifecycle.DeadLetter;

    public static DurableWorkItem Create<TPayload>(
        TenantContext trustedTenantContext,
        TenantWorkScope scope,
        DurableWorkIdentity identity,
        TPayload payload,
        int maximumAttempts = 3,
        DateTimeOffset? createdAt = null)
        where TPayload : IWorkPayload
    {
        ArgumentNullException.ThrowIfNull(trustedTenantContext);
        ArgumentNullException.ThrowIfNull(scope);
        ArgumentNullException.ThrowIfNull(identity);
        ArgumentNullException.ThrowIfNull(payload);
        if (scope.TenantId != trustedTenantContext.TenantId)
        {
            throw new ArgumentException("Organization scope must belong to the trusted Tenant.", nameof(scope));
        }

        if (maximumAttempts is < 1 or > 20)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumAttempts));
        }

        var initiator = DurableWorkInitiator.FromServerContext(trustedTenantContext);
        if (!string.Equals(identity.CorrelationId.Value, initiator.CorrelationId.Value, StringComparison.Ordinal))
        {
            throw new ArgumentException("Work correlation must match the trusted context.", nameof(identity));
        }

        return new DurableWorkItem(identity, initiator, scope, payload, maximumAttempts, createdAt ?? DateTimeOffset.UtcNow);
    }

    internal bool IsEligible(TenantContext context, DateTimeOffset now)
    {
        return Lifecycle is DurableWorkLifecycle.Pending or DurableWorkLifecycle.RetryScheduled
            && NextAttemptAt <= now
            && (LeaseExpiresAt is null || LeaseExpiresAt <= now)
            && context.TenantId == TenantId
            && context.AuthorizationPath == Initiator.AuthorizationPath;
    }

    internal DurableWorkLease Claim(Guid workerId, DateTimeOffset leaseExpiresAt)
    {
        if (workerId == Guid.Empty)
        {
            throw new ArgumentException("Worker identifier must not be empty.", nameof(workerId));
        }

        Lifecycle = DurableWorkLifecycle.Claimed;
        LeaseOwnerId = workerId;
        LeaseExpiresAt = leaseExpiresAt;
        UpdatedAt = DateTimeOffset.UtcNow;
        ConcurrencyVersion++;
        return new DurableWorkLease(Identity.WorkItemId, TenantId, workerId, leaseExpiresAt, ConcurrencyVersion);
    }

    internal DurableWorkCompletion Complete(
        DurableWorkLease lease,
        DurableWorkHandlerResult result,
        DateTimeOffset now)
    {
        if (Lifecycle != DurableWorkLifecycle.Claimed
            || LeaseOwnerId != lease.WorkerId
            || lease.ConcurrencyVersion != ConcurrencyVersion
            || LeaseExpiresAt is null
            || LeaseExpiresAt < now)
        {
            return DurableWorkCompletion.CreateDenied(DurableWorkFailureCategory.ConcurrencyConflict);
        }

        AttemptCount++;
        FailureCategory = result.FailureCategory;
        SafeFailureReason = result.SafeReason;
        LeaseOwnerId = null;
        LeaseExpiresAt = null;
        if (result.Success)
        {
            Lifecycle = DurableWorkLifecycle.Completed;
        }
        else if (result.DeadLetter || AttemptCount >= MaximumAttempts)
        {
            Lifecycle = DurableWorkLifecycle.DeadLetter;
        }
        else
        {
            Lifecycle = DurableWorkLifecycle.RetryScheduled;
            NextAttemptAt = now.Add(result.RetryAfter);
        }

        UpdatedAt = now;
        ConcurrencyVersion++;
        return DurableWorkCompletion.CreateAccepted(Lifecycle, AttemptCount, FailureCategory);
    }

    internal void MarkExpiredLease(DateTimeOffset now)
    {
        if (Lifecycle == DurableWorkLifecycle.Claimed && LeaseExpiresAt <= now)
        {
            Lifecycle = DurableWorkLifecycle.RetryScheduled;
            LeaseOwnerId = null;
            LeaseExpiresAt = null;
            NextAttemptAt = now;
            UpdatedAt = now;
            ConcurrencyVersion++;
        }
    }
}

/// <summary>Lease proof used to complete exactly one claimed item.</summary>
public sealed record DurableWorkLease(
    Guid WorkItemId,
    TenantId TenantId,
    Guid WorkerId,
    DateTimeOffset LeaseExpiresAt,
    long ConcurrencyVersion);

/// <summary>Typed execution context reconstructed from stored server facts.</summary>
public sealed class DurableWorkExecutionContext
{
    internal DurableWorkExecutionContext(DurableWorkItem workItem, TenantContext tenantContext)
    {
        if (workItem.TenantId != tenantContext.TenantId
            || workItem.Initiator.AuthorizationPath != tenantContext.AuthorizationPath)
        {
            throw new ArgumentException("Execution context does not match stored work ownership.", nameof(tenantContext));
        }

        WorkItemId = workItem.Identity.WorkItemId;
        TenantContext = tenantContext;
        Scope = workItem.Scope;
        OperationId = workItem.Identity.OperationId;
        CorrelationId = workItem.Identity.CorrelationId;
    }

    public Guid WorkItemId { get; }

    public TenantContext TenantContext { get; }

    public TenantWorkScope Scope { get; }

    public string OperationId { get; }

    public CorrelationId CorrelationId { get; }
}

/// <summary>Safe result returned by a typed work handler.</summary>
public sealed class DurableWorkHandlerResult
{
    private DurableWorkHandlerResult(
        bool success,
        bool deadLetter,
        TimeSpan retryAfter,
        DurableWorkFailureCategory failureCategory,
        string? safeReason)
    {
        Success = success;
        DeadLetter = deadLetter;
        RetryAfter = retryAfter;
        FailureCategory = failureCategory;
        SafeReason = safeReason;
    }

    public bool Success { get; }

    public bool DeadLetter { get; }

    public TimeSpan RetryAfter { get; }

    public DurableWorkFailureCategory FailureCategory { get; }

    public string? SafeReason { get; }

    public static DurableWorkHandlerResult Succeeded() =>
        new(true, false, TimeSpan.Zero, DurableWorkFailureCategory.None, null);

    public static DurableWorkHandlerResult Retry(
        DurableWorkFailureCategory category,
        TimeSpan retryAfter,
        string safeReason)
    {
        if (category == DurableWorkFailureCategory.None || retryAfter < TimeSpan.Zero)
        {
            throw new ArgumentException("Retry requires a bounded failure category and delay.");
        }

        return new(false, false, retryAfter > TimeSpan.FromHours(1) ? TimeSpan.FromHours(1) : retryAfter, category, SanitizeReason(safeReason));
    }

    public static DurableWorkHandlerResult DeadLettered(
        DurableWorkFailureCategory category,
        string safeReason) =>
        new(false, true, TimeSpan.Zero, category == DurableWorkFailureCategory.None ? DurableWorkFailureCategory.Unknown : category, SanitizeReason(safeReason));

    private static string SanitizeReason(string value)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Trim().Length > 128 || value.Any(char.IsControl))
        {
            throw new ArgumentException("Failure reason must be safe and bounded.", nameof(value));
        }

        return value.Trim();
    }
}

/// <summary>Result of a guarded state transition.</summary>
public sealed record DurableWorkCompletion(
    bool Accepted,
    DurableWorkLifecycle Lifecycle,
    int AttemptCount,
    DurableWorkFailureCategory FailureCategory)
{
    internal static DurableWorkCompletion CreateAccepted(
        DurableWorkLifecycle lifecycle,
        int attemptCount,
        DurableWorkFailureCategory failureCategory) =>
        new(true, lifecycle, attemptCount, failureCategory);

    internal static DurableWorkCompletion CreateDenied(DurableWorkFailureCategory failureCategory) =>
        new(false, DurableWorkLifecycle.Claimed, 0, failureCategory);
}

/// <summary>Handler-specific typed work contract.</summary>
public interface IDurableWorkHandler<TPayload>
    where TPayload : IWorkPayload
{
    string WorkType { get; }

    ValueTask<DurableWorkHandlerResult> ExecuteAsync(
        TPayload payload,
        DurableWorkExecutionContext context,
        CancellationToken cancellationToken = default);
}

/// <summary>Stable, Tenant-owned outbox envelope.</summary>
public sealed class TenantOutboxMessage : ITenantOwned
{
    internal TenantOutboxMessage(
        Guid eventId,
        DurableWorkItem workItem,
        DateTimeOffset occurredAt)
    {
        EventId = eventId;
        WorkItemId = workItem.Identity.WorkItemId;
        TenantId = workItem.TenantId;
        Scope = workItem.Scope;
        EventType = workItem.Identity.WorkType;
        CorrelationId = workItem.Identity.CorrelationId;
        OccurredAt = occurredAt;
        AttemptCount = 0;
        NextAttemptAt = occurredAt;
    }

    public Guid EventId { get; }

    public Guid WorkItemId { get; }

    public TenantId TenantId { get; }

    public TenantWorkScope Scope { get; }

    public string EventType { get; }

    public CorrelationId CorrelationId { get; }

    public DateTimeOffset OccurredAt { get; }

    public int AttemptCount { get; internal set; }

    public DateTimeOffset NextAttemptAt { get; internal set; }

    public DurableWorkLifecycle DeliveryState { get; internal set; } = DurableWorkLifecycle.Pending;

    public DurableWorkFailureCategory FailureCategory { get; internal set; }
}

/// <summary>Safe audit record emitted by local durable-work adapters.</summary>
public sealed record DurableWorkAuditRecord(
    DateTimeOffset OccurredAt,
    string EventType,
    TenantId TenantId,
    Guid WorkItemId,
    CorrelationId CorrelationId,
    DurableWorkLifecycle Lifecycle,
    DurableWorkFailureCategory FailureCategory,
    int AttemptCount);

/// <summary>Transactional, Tenant-bound durable-work seam.</summary>
public interface IRelationalDurableWorkStore
{
    ValueTask<bool> SubmitAsync(DurableWorkItem workItem, CancellationToken cancellationToken = default);

    ValueTask<DurableWorkItem?> FindAsync(
        TenantContext tenantContext,
        Guid workItemId,
        CancellationToken cancellationToken = default);

    ValueTask<DurableWorkLease?> TryClaimAsync(
        TenantContext tenantContext,
        Guid workerId,
        DateTimeOffset now,
        TimeSpan leaseDuration,
        CancellationToken cancellationToken = default);

    ValueTask<DurableWorkCompletion> CompleteAsync(
        TenantContext tenantContext,
        DurableWorkLease lease,
        DurableWorkHandlerResult result,
        DateTimeOffset now,
        CancellationToken cancellationToken = default);

    ValueTask<OutboxDispatchResult> DispatchOutboxAsync(
        TenantContext tenantContext,
        DateTimeOffset now,
        Func<TenantOutboxMessage, CancellationToken, ValueTask> effect,
        CancellationToken cancellationToken = default);

    ValueTask<IReadOnlyList<DurableWorkAuditRecord>> ReadAuditAsync(
        TenantContext tenantContext,
        CancellationToken cancellationToken = default);
}

/// <summary>Safe outbox delivery result.</summary>
public sealed record OutboxDispatchResult(
    bool Delivered,
    bool Duplicate,
    bool RetryScheduled,
    bool DeadLettered,
    DurableWorkFailureCategory FailureCategory);
