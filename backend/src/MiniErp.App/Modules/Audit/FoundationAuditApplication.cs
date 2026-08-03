#pragma warning disable CS1591

using MiniErp.App.BuildingBlocks.Rest;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.Contracts.Modules.Audit;
using MiniErp.Contracts.Modules.Foundation;

namespace MiniErp.App.Modules.Audit;

/// <summary>
/// Safe text rules shared by evidence construction and telemetry projection.
/// </summary>
internal static class FoundationAuditText
{
    public const int MaximumLength = 128;

    public static string Required(string? value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("The evidence value is required.", name);
        }

        return Validated(value, name);
    }

    public static string? Optional(string? value, string name)
    {
        return value is null ? null : Validated(value, name);
    }

    private static string Validated(string value, string name)
    {
        var normalized = value.Trim();
        if (normalized.Length == 0 || normalized.Length > MaximumLength)
        {
            throw new ArgumentException("The evidence value is outside the approved bound.", name);
        }

        if (normalized.Any(char.IsControl))
        {
            throw new ArgumentException("The evidence value contains a control character.", name);
        }

        return normalized;
    }
}

/// <summary>
/// Creates evidence from an immutable, server-derived Foundation request
/// context. No caller-supplied Tenant or target identifier is accepted.
/// </summary>
public static class FoundationAuditEvidenceFactory
{
    public static FoundationAuditEvidence Create(
        FoundationRequestContext context,
        string operationId,
        string correlationId,
        FoundationAuditDecision decision,
        FoundationAuditReason reason,
        string? idempotencyKey = null,
        string? operationVersion = null,
        string? supportPurpose = null,
        DateTimeOffset? supportGrantExpiresAt = null,
        Guid? retryOfEvidenceId = null,
        int attempt = 1,
        DateTimeOffset? occurredAt = null)
    {
        ArgumentNullException.ThrowIfNull(context);

        var operation = FoundationAuditText.Required(operationId, nameof(operationId));
        var correlation = FoundationAuditText.Required(correlationId, nameof(correlationId));
        if (!FoundationCorrelation.IsValid(correlation))
        {
            throw new ArgumentException("The correlation identifier is invalid.", nameof(correlationId));
        }

        if (!Enum.IsDefined(decision))
        {
            throw new ArgumentOutOfRangeException(nameof(decision));
        }

        if (!Enum.IsDefined(reason))
        {
            throw new ArgumentOutOfRangeException(nameof(reason));
        }

        if (attempt < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(attempt));
        }

        if (decision == FoundationAuditDecision.Retry
            && (retryOfEvidenceId is null || attempt < 2))
        {
            throw new ArgumentException("A retry must identify its prior evidence.", nameof(retryOfEvidenceId));
        }

        if (decision != FoundationAuditDecision.Retry && retryOfEvidenceId is not null)
        {
            throw new ArgumentException("Only retry evidence may reference prior evidence.", nameof(retryOfEvidenceId));
        }

        var idempotency = FoundationAuditText.Optional(idempotencyKey, nameof(idempotencyKey));
        if (idempotency is not null && !FoundationCorrelation.IsValid(idempotency))
        {
            throw new ArgumentException("The idempotency key is invalid.", nameof(idempotencyKey));
        }

        var version = FoundationAuditText.Optional(operationVersion, nameof(operationVersion));
        var actorId = context.ActorId
            ?? throw new ArgumentException("A protected context requires a server actor.", nameof(context));
        var sessionId = context.SessionId
            ?? throw new ArgumentException("A protected context requires a server session.", nameof(context));

        if (context.SecurityProfile == FoundationSecurityProfile.OrdinaryMembership
            && (supportPurpose is not null || supportGrantExpiresAt is not null))
        {
            throw new ArgumentException("Ordinary evidence cannot contain support facts.", nameof(supportPurpose));
        }

        return context.SecurityProfile switch
        {
            FoundationSecurityProfile.OrdinaryMembership => CreateTenantEvidence(
                context,
                operation,
                correlation,
                actorId,
                sessionId,
                FoundationAuditAuthorizationPath.OrdinaryMembership,
                decision,
                reason,
                idempotency,
                version,
                supportPurpose: null,
                supportGrantExpiresAt: null,
                retryOfEvidenceId,
                attempt,
                occurredAt),
            FoundationSecurityProfile.SupportGrant => CreateTenantEvidence(
                context,
                operation,
                correlation,
                actorId,
                sessionId,
                FoundationAuditAuthorizationPath.SupportGrant,
                decision,
                reason,
                idempotency,
                version,
                FoundationAuditText.Required(supportPurpose, nameof(supportPurpose)),
                supportGrantExpiresAt,
                retryOfEvidenceId,
                attempt,
                occurredAt),
            FoundationSecurityProfile.PlatformGovernanceContext => CreatePlatformEvidence(
                context,
                operation,
                correlation,
                actorId,
                sessionId,
                decision,
                reason,
                idempotency,
                version,
                retryOfEvidenceId,
                attempt,
                occurredAt),
            _ => throw new ArgumentException(
                "Evidence requires one explicit Tenant or platform authorization path.",
                nameof(context))
        };
    }

    private static FoundationAuditEvidence CreateTenantEvidence(
        FoundationRequestContext context,
        string operation,
        string correlation,
        Guid actorId,
        Guid sessionId,
        FoundationAuditAuthorizationPath path,
        FoundationAuditDecision decision,
        FoundationAuditReason reason,
        string? idempotency,
        string? version,
        string? supportPurpose,
        DateTimeOffset? supportGrantExpiresAt,
        Guid? retryOfEvidenceId,
        int attempt,
        DateTimeOffset? occurredAt)
    {
        var tenant = context.TenantContext
            ?? throw new ArgumentException("Tenant evidence requires a trusted Tenant context.", nameof(context));

        var expectedPath = path == FoundationAuditAuthorizationPath.OrdinaryMembership
            ? TenantAuthorizationPath.OrdinaryMembership
            : TenantAuthorizationPath.SupportGrant;
        if (tenant.AuthorizationPath != expectedPath || context.PlatformGovernanceContext is not null)
        {
            throw new ArgumentException("The evidence path does not match the trusted Tenant context.", nameof(context));
        }

        Guid? supportUserId = path == FoundationAuditAuthorizationPath.SupportGrant ? actorId : null;
        var supportGrantId = tenant.SupportGrant?.GrantId;
        var supportCaseId = tenant.SupportGrant?.CaseId;
        if (path == FoundationAuditAuthorizationPath.SupportGrant
            && (supportGrantId is null || supportCaseId is null))
        {
            throw new ArgumentException("Support evidence requires a trusted grant and case.", nameof(context));
        }

        if (path == FoundationAuditAuthorizationPath.OrdinaryMembership
            && (supportPurpose is not null || supportGrantExpiresAt is not null))
        {
            throw new ArgumentException("Ordinary evidence cannot contain support facts.", nameof(supportPurpose));
        }

        return new FoundationAuditEvidence(
            Guid.NewGuid(),
            occurredAt ?? DateTimeOffset.UtcNow,
            operation,
            correlation,
            actorId,
            sessionId,
            path,
            tenant.TenantId.Value,
            tenant.Scope?.Value,
            supportPurpose,
            supportUserId,
            supportGrantId,
            supportCaseId,
            supportGrantExpiresAt,
            decision,
            reason,
            idempotency,
            version,
            retryOfEvidenceId,
            attempt);
    }

    private static FoundationAuditEvidence CreatePlatformEvidence(
        FoundationRequestContext context,
        string operation,
        string correlation,
        Guid actorId,
        Guid sessionId,
        FoundationAuditDecision decision,
        FoundationAuditReason reason,
        string? idempotency,
        string? version,
        Guid? retryOfEvidenceId,
        int attempt,
        DateTimeOffset? occurredAt)
    {
        var platform = context.PlatformGovernanceContext
            ?? throw new ArgumentException("Platform evidence requires a trusted governance context.", nameof(context));
        if (context.TenantContext is not null)
        {
            throw new ArgumentException("Platform evidence cannot contain a Tenant context.", nameof(context));
        }

        return new FoundationAuditEvidence(
            Guid.NewGuid(),
            occurredAt ?? DateTimeOffset.UtcNow,
            operation,
            correlation,
            actorId,
            sessionId,
            FoundationAuditAuthorizationPath.PlatformGovernanceContext,
            tenantId: null,
            organizationScope: null,
            platform.Purpose.ToString(),
            supportUserId: null,
            supportGrantId: null,
            supportCaseId: null,
            supportGrantExpiresAt: null,
            decision,
            reason,
            idempotency,
            version,
            retryOfEvidenceId,
            attempt);
    }
}

/// <summary>
/// Validates the immutable evidence invariants before local persistence.
/// </summary>
internal static class FoundationAuditEvidenceMapping
{
    public static void Validate(FoundationAuditEvidence evidence)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        if (evidence.EvidenceId == Guid.Empty
            || evidence.ActorId == Guid.Empty
            || evidence.SessionId == Guid.Empty
            || evidence.Attempt < 1
            || !Enum.IsDefined(evidence.AuthorizationPath)
            || !Enum.IsDefined(evidence.Decision)
            || !Enum.IsDefined(evidence.Reason))
        {
            throw new FoundationAuditAppendException("evidence_invalid");
        }

        _ = FoundationAuditText.Required(evidence.OperationId, nameof(evidence.OperationId));
        _ = FoundationAuditText.Required(evidence.CorrelationId, nameof(evidence.CorrelationId));
        if (!FoundationCorrelation.IsValid(evidence.CorrelationId))
        {
            throw new FoundationAuditAppendException("correlation_invalid");
        }

        var tenantPath = evidence.AuthorizationPath is
            FoundationAuditAuthorizationPath.OrdinaryMembership or FoundationAuditAuthorizationPath.SupportGrant;
        if (tenantPath != (evidence.TenantId.HasValue && evidence.TenantId.Value != Guid.Empty))
        {
            throw new FoundationAuditAppendException("tenant_path_invalid");
        }

        if (evidence.AuthorizationPath == FoundationAuditAuthorizationPath.PlatformGovernanceContext
            && string.IsNullOrWhiteSpace(evidence.Purpose))
        {
            throw new FoundationAuditAppendException("platform_purpose_missing");
        }

        if (evidence.AuthorizationPath == FoundationAuditAuthorizationPath.OrdinaryMembership
            && (evidence.Purpose is not null
                || evidence.SupportUserId is not null
                || evidence.SupportGrantId is not null
                || evidence.SupportCaseId is not null
                || evidence.SupportGrantExpiresAt is not null))
        {
            throw new FoundationAuditAppendException("ordinary_support_facts_invalid");
        }

        if (evidence.AuthorizationPath == FoundationAuditAuthorizationPath.SupportGrant
            && (evidence.SupportUserId != evidence.ActorId
                || evidence.SupportGrantId is null
                || evidence.SupportCaseId is null
                || string.IsNullOrWhiteSpace(evidence.Purpose)))
        {
            throw new FoundationAuditAppendException("support_facts_missing");
        }

        if (evidence.Decision == FoundationAuditDecision.Retry
            ? evidence.RetryOfEvidenceId is null || evidence.Attempt < 2
            : evidence.RetryOfEvidenceId is not null)
        {
            throw new FoundationAuditAppendException("retry_relationship_invalid");
        }

        if (evidence.IdempotencyKey is not null && !FoundationCorrelation.IsValid(evidence.IdempotencyKey))
        {
            throw new FoundationAuditAppendException("idempotency_invalid");
        }

        _ = FoundationAuditText.Optional(evidence.OrganizationScope, nameof(evidence.OrganizationScope));
        _ = FoundationAuditText.Optional(evidence.Purpose, nameof(evidence.Purpose));
        _ = FoundationAuditText.Optional(evidence.OperationVersion, nameof(evidence.OperationVersion));
    }
}

/// <summary>
/// Safe append-only evidence sink. Implementations must not expose mutation or
/// deletion operations.
/// </summary>
public interface IFoundationAuditEvidenceSink
{
    ValueTask AppendAsync(FoundationAuditEvidence evidence, CancellationToken cancellationToken = default);
}

/// <summary>
/// Explicit Tenant-bound read surface. There is intentionally no unscoped
/// query method.
/// </summary>
public interface IFoundationAuditEvidenceReader
{
    ValueTask<IReadOnlyList<FoundationAuditEvidence>> ReadForTenantAsync(
        TenantContext tenantContext,
        CancellationToken cancellationToken = default);

    ValueTask<IReadOnlyList<FoundationAuditEvidence>> ReadForPlatformAsync(
        PlatformGovernanceContext governanceContext,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// A bounded local append-only store used for isolated validation only. It is
/// not a production retention, purge or database provider.
/// </summary>
public sealed class LocalImmutableAuditEvidenceStore : IFoundationAuditEvidenceSink, IFoundationAuditEvidenceReader
{
    private readonly object syncRoot = new();
    private readonly List<FoundationAuditEvidence> evidence = [];
    private readonly int capacity;

    public LocalImmutableAuditEvidenceStore(int capacity = 2048)
    {
        if (capacity < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity));
        }

        this.capacity = capacity;
    }

    public ValueTask AppendAsync(
        FoundationAuditEvidence item,
        CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        FoundationAuditEvidenceMapping.Validate(item);

        lock (syncRoot)
        {
            if (evidence.Count >= capacity)
            {
                throw new FoundationAuditAppendException("evidence_capacity_reached");
            }

            if (evidence.Any(existing => existing.EvidenceId == item.EvidenceId))
            {
                throw new FoundationAuditAppendException("evidence_id_reuse");
            }

            evidence.Add(item);
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask<IReadOnlyList<FoundationAuditEvidence>> ReadForTenantAsync(
        TenantContext tenantContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        cancellationToken.ThrowIfCancellationRequested();
        var tenantId = tenantContext.TenantId.Value;
        var expectedPath = tenantContext.AuthorizationPath switch
        {
            TenantAuthorizationPath.OrdinaryMembership => FoundationAuditAuthorizationPath.OrdinaryMembership,
            TenantAuthorizationPath.SupportGrant => FoundationAuditAuthorizationPath.SupportGrant,
            _ => throw new ArgumentOutOfRangeException(nameof(tenantContext))
        };
        lock (syncRoot)
        {
            IReadOnlyList<FoundationAuditEvidence> result = evidence
                .Where(item => item.TenantId == tenantId
                    && item.AuthorizationPath == expectedPath)
                .ToArray();
            return ValueTask.FromResult(result);
        }
    }

    public ValueTask<IReadOnlyList<FoundationAuditEvidence>> ReadForPlatformAsync(
        PlatformGovernanceContext governanceContext,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(governanceContext);
        cancellationToken.ThrowIfCancellationRequested();
        var purpose = governanceContext.Purpose.ToString();
        lock (syncRoot)
        {
            IReadOnlyList<FoundationAuditEvidence> result = evidence
                .Where(item => item.AuthorizationPath == FoundationAuditAuthorizationPath.PlatformGovernanceContext
                    && item.TenantId is null
                    && string.Equals(item.Purpose, purpose, StringComparison.Ordinal))
                .ToArray();
            return ValueTask.FromResult(result);
        }
    }

    /// <summary>Test-only reset for a new isolated local host.</summary>
    internal void Clear()
    {
        lock (syncRoot)
        {
            evidence.Clear();
        }
    }

    internal int Count
    {
        get
        {
            lock (syncRoot)
            {
                return evidence.Count;
            }
        }
    }
}

/// <summary>
/// Safe append failure with an allow-listed code and no provider detail.
/// </summary>
public sealed class FoundationAuditAppendException : InvalidOperationException
{
    public FoundationAuditAppendException(string code)
        : base("Mandatory audit evidence could not be appended.")
    {
        Code = FoundationAuditText.Required(code, nameof(code));
    }

    public string Code { get; }
}

/// <summary>
/// Provides an in-process structured log, metric and trace hook. No exporter
/// or endpoint is configured by the Foundation slice.
/// </summary>
public interface IFoundationAuditTelemetrySink
{
    void Emit(FoundationAuditTelemetryEvent telemetryEvent);
}

public sealed class LocalFoundationAuditTelemetrySink : IFoundationAuditTelemetrySink
{
    private readonly object syncRoot = new();
    private readonly List<FoundationAuditTelemetryEvent> events = [];
    private readonly int capacity;

    public LocalFoundationAuditTelemetrySink(int capacity = 2048)
    {
        if (capacity < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity));
        }

        this.capacity = capacity;
    }

    public IReadOnlyList<FoundationAuditTelemetryEvent> Events
    {
        get
        {
            lock (syncRoot)
            {
                return events.ToArray();
            }
        }
    }

    public void Emit(FoundationAuditTelemetryEvent telemetryEvent)
    {
        lock (syncRoot)
        {
            if (events.Count < capacity)
            {
                events.Add(telemetryEvent);
            }
        }
    }

    internal void Clear()
    {
        lock (syncRoot)
        {
            events.Clear();
        }
    }
}

/// <summary>
/// Bounded safe operational signal hook for evidence failures.
/// </summary>
public interface IFoundationAuditOperationalSignalSink
{
    void Emit(FoundationAuditOperationalSignal signal);
}

public sealed class LocalFoundationAuditOperationalSignalSink : IFoundationAuditOperationalSignalSink
{
    private readonly object syncRoot = new();
    private readonly List<FoundationAuditOperationalSignal> signals = [];
    private readonly int capacity;

    public LocalFoundationAuditOperationalSignalSink(int capacity = 256)
    {
        if (capacity < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(capacity));
        }

        this.capacity = capacity;
    }

    public IReadOnlyList<FoundationAuditOperationalSignal> Signals
    {
        get
        {
            lock (syncRoot)
            {
                return signals.ToArray();
            }
        }
    }

    public void Emit(FoundationAuditOperationalSignal signal)
    {
        lock (syncRoot)
        {
            if (signals.Count < capacity)
            {
                signals.Add(signal);
            }
        }
    }

    internal void Clear()
    {
        lock (syncRoot)
        {
            signals.Clear();
        }
    }
}

/// <summary>
/// Projects evidence through an explicit allow-list. The optional input bag
/// is intentionally ignored; arbitrary request objects are never serialized.
/// </summary>
public static class FoundationAuditRedactor
{
    public static FoundationAuditTelemetryEvent ToTelemetry(
        FoundationAuditEvidence evidence,
        FoundationAuditTelemetryKind kind,
        IReadOnlyDictionary<string, string?>? ignoredInput = null)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }

        _ = ignoredInput;
        return new FoundationAuditTelemetryEvent(
            Guid.NewGuid(),
            evidence.OccurredAt,
            kind,
            evidence.EvidenceId,
            evidence.OperationId,
            evidence.CorrelationId,
            evidence.AuthorizationPath,
            evidence.TenantId,
            evidence.Decision,
            evidence.Reason,
            evidence.Decision == FoundationAuditDecision.Retry);
    }
}

/// <summary>
/// Result of an evidence append attempt. A failed result contains no business
/// value and is safe to expose to an API boundary.
/// </summary>
public sealed record FoundationAuditRecordResult(
    bool Succeeded,
    FoundationAuditEvidence? Evidence,
    string Code)
{
    public static FoundationAuditRecordResult Success(FoundationAuditEvidence evidence) =>
        new(true, evidence, "recorded");

    public static FoundationAuditRecordResult Failure(string code) =>
        new(false, null, code);
}

/// <summary>
/// Result for a protected effect that requires mandatory evidence first.
/// </summary>
public sealed record FoundationAuditExecutionResult<T>(
    bool Succeeded,
    T? Value,
    FoundationAuditEvidence? Evidence,
    string Code)
{
    public static FoundationAuditExecutionResult<T> Success(T value, FoundationAuditEvidence evidence) =>
        new(true, value, evidence, "success");

    public static FoundationAuditExecutionResult<T> Failure(string code) =>
        new(false, default, null, code);
}

/// <summary>
/// Coordinates mandatory append-before-effect behavior and safe telemetry.
/// </summary>
public sealed class FoundationAuditCoordinator
{
    private readonly IFoundationAuditEvidenceSink evidenceSink;
    private readonly IFoundationAuditTelemetrySink telemetrySink;
    private readonly IFoundationAuditOperationalSignalSink signalSink;

    public FoundationAuditCoordinator(
        IFoundationAuditEvidenceSink evidenceSink,
        IFoundationAuditTelemetrySink telemetrySink,
        IFoundationAuditOperationalSignalSink signalSink)
    {
        this.evidenceSink = evidenceSink ?? throw new ArgumentNullException(nameof(evidenceSink));
        this.telemetrySink = telemetrySink ?? throw new ArgumentNullException(nameof(telemetrySink));
        this.signalSink = signalSink ?? throw new ArgumentNullException(nameof(signalSink));
    }

    public async Task<FoundationAuditRecordResult> RecordAsync(
        FoundationRequestContext context,
        string operationId,
        string correlationId,
        FoundationAuditDecision decision,
        FoundationAuditReason reason,
        string? idempotencyKey = null,
        string? operationVersion = null,
        string? supportPurpose = null,
        DateTimeOffset? supportGrantExpiresAt = null,
        Guid? retryOfEvidenceId = null,
        int attempt = 1,
        CancellationToken cancellationToken = default)
    {
        FoundationAuditEvidence evidence;
        try
        {
            evidence = FoundationAuditEvidenceFactory.Create(
                context,
                operationId,
                correlationId,
                decision,
                reason,
                idempotencyKey,
                operationVersion,
                supportPurpose,
                supportGrantExpiresAt,
                retryOfEvidenceId,
                attempt);
        }
        catch
        {
            EmitFailureSignal(operationId, correlationId);
            return FoundationAuditRecordResult.Failure("audit_evidence_invalid");
        }

        try
        {
            await evidenceSink.AppendAsync(evidence, cancellationToken);
        }
        catch
        {
            EmitFailureSignal(evidence.OperationId, evidence.CorrelationId);
            return FoundationAuditRecordResult.Failure("audit_evidence_unavailable");
        }

        EmitTelemetry(evidence);
        return FoundationAuditRecordResult.Success(evidence);
    }

    public Task<FoundationAuditRecordResult> RecordRetryAsync(
        FoundationRequestContext context,
        string operationId,
        string correlationId,
        Guid priorEvidenceId,
        int attempt,
        string? idempotencyKey = null,
        string? operationVersion = null,
        CancellationToken cancellationToken = default) => RecordAsync(
            context,
            operationId,
            correlationId,
            FoundationAuditDecision.Retry,
            FoundationAuditReason.RetryAccepted,
            idempotencyKey,
            operationVersion,
            retryOfEvidenceId: priorEvidenceId,
            attempt: attempt,
            cancellationToken: cancellationToken);

    public async Task<FoundationAuditExecutionResult<T>> ExecuteProtectedAsync<T>(
        FoundationRequestContext context,
        string operationId,
        string correlationId,
        FoundationAuditReason reason,
        Func<Task<T>> protectedEffect,
        string? idempotencyKey = null,
        string? operationVersion = null,
        string? supportPurpose = null,
        DateTimeOffset? supportGrantExpiresAt = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(protectedEffect);
        var record = await RecordAsync(
            context,
            operationId,
            correlationId,
            FoundationAuditDecision.Allowed,
            reason,
            idempotencyKey,
            operationVersion,
            supportPurpose,
            supportGrantExpiresAt,
            cancellationToken: cancellationToken);
        if (!record.Succeeded || record.Evidence is null)
        {
            return FoundationAuditExecutionResult<T>.Failure(record.Code);
        }

        try
        {
            var value = await protectedEffect();
            return FoundationAuditExecutionResult<T>.Success(value, record.Evidence);
        }
        catch
        {
            return FoundationAuditExecutionResult<T>.Failure("protected_effect_failed");
        }
    }

    private void EmitTelemetry(FoundationAuditEvidence evidence)
    {
        foreach (var kind in Enum.GetValues<FoundationAuditTelemetryKind>())
        {
            try
            {
                telemetrySink.Emit(FoundationAuditRedactor.ToTelemetry(evidence, kind));
            }
            catch
            {
                EmitFailureSignal(evidence.OperationId, evidence.CorrelationId);
            }
        }
    }

    private void EmitFailureSignal(string operationId, string correlationId)
    {
        var safeOperation = SafeSignalText(operationId, "unknown.operation");
        var safeCorrelation = SafeSignalText(correlationId, Guid.NewGuid().ToString("N"));
        try
        {
            signalSink.Emit(new FoundationAuditOperationalSignal(
                Guid.NewGuid(),
                DateTimeOffset.UtcNow,
                "audit_evidence_failure",
                safeOperation,
                safeCorrelation,
                FoundationAuditDecision.EvidenceFailure));
        }
        catch
        {
            // The operational signal is deliberately best effort and bounded.
        }
    }

    private static string SafeSignalText(string value, string fallback)
    {
        try
        {
            return FoundationAuditText.Required(value, nameof(value));
        }
        catch
        {
            return fallback;
        }
    }
}
