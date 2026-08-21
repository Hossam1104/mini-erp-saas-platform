using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.Modules.Procurement;
using MiniErp.Contracts.Modules.Procurement;

namespace MiniErp.Infrastructure.Persistence.Modules.Procurement;

internal sealed class PurchaseInvoiceMatchEvaluationEntity : ITenantOwned
{
    private PurchaseInvoiceMatchEvaluationEntity()
    {
        SourceFingerprint = string.Empty;
        PolicySnapshotJson = string.Empty;
        VariancesJson = "[]";
        SourceSnapshotJson = string.Empty;
        CorrelationId = string.Empty;
    }

    internal PurchaseInvoiceMatchEvaluationEntity(
        TenantId tenantId,
        PurchaseInvoiceMatchEvaluateCommand command,
        Guid id,
        Guid purchaseOrderId,
        PurchaseRequestScope scope,
        PurchaseInvoiceMatchResult result,
        string sourceFingerprint,
        byte[] purchaseOrderVersion,
        byte[] handoffVersion,
        Guid? declaredEvidenceId,
        int? declaredEvidenceVersion,
        string policySnapshotJson,
        string? exchangeRateSnapshotJson,
        string variancesJson,
        string sourceSnapshotJson)
    {
        Id = id;
        TenantId = tenantId;
        PurchaseInvoiceHandoffId = command.PurchaseInvoiceHandoffId;
        PurchaseOrderId = purchaseOrderId;
        CompanyId = scope.CompanyId;
        BranchId = scope.BranchId;
        Lifecycle = PurchaseInvoiceMatchLifecycle.Current;
        Result = result;
        EvaluatedAt = command.OccurredAt;
        EvaluatedByActorId = command.ActorId;
        SourceFingerprint = sourceFingerprint;
        PurchaseOrderVersion = purchaseOrderVersion;
        HandoffVersion = handoffVersion;
        DeclaredEvidenceId = declaredEvidenceId;
        DeclaredEvidenceVersion = declaredEvidenceVersion;
        PolicySnapshotJson = policySnapshotJson;
        ExchangeRateSnapshotJson = exchangeRateSnapshotJson;
        VariancesJson = variancesJson;
        SourceSnapshotJson = sourceSnapshotJson;
        CorrelationId = command.CorrelationId;
    }

    internal Guid Id { get; private set; }
    public TenantId TenantId { get; private set; }
    internal Guid PurchaseInvoiceHandoffId { get; private set; }
    internal Guid PurchaseOrderId { get; private set; }
    internal Guid CompanyId { get; private set; }
    internal Guid? BranchId { get; private set; }
    internal PurchaseInvoiceMatchLifecycle Lifecycle { get; private set; }
    internal PurchaseInvoiceMatchResult Result { get; private set; }
    internal DateTimeOffset EvaluatedAt { get; private set; }
    internal Guid EvaluatedByActorId { get; private set; }
    internal Guid? ResolvedByActorId { get; private set; }
    internal DateTimeOffset? ResolvedAt { get; private set; }
    internal string? ResolutionReason { get; private set; }
    internal string SourceFingerprint { get; private set; }
    internal byte[] PurchaseOrderVersion { get; private set; } = [];
    internal byte[] HandoffVersion { get; private set; } = [];
    internal Guid? DeclaredEvidenceId { get; private set; }
    internal int? DeclaredEvidenceVersion { get; private set; }
    internal string PolicySnapshotJson { get; private set; }
    internal string? ResolutionPolicySnapshotJson { get; private set; }
    internal string? ExchangeRateSnapshotJson { get; private set; }
    internal string VariancesJson { get; private set; }
    internal string SourceSnapshotJson { get; private set; }
    internal string CorrelationId { get; private set; }
    internal byte[] Version { get; private set; } = [];
    internal int? ReplayResponseSchemaVersion { get; private set; }
    internal string? ReplayResponseSnapshotJson { get; private set; }

    internal void Supersede() => Lifecycle = PurchaseInvoiceMatchLifecycle.Superseded;

    internal void Resolve(Guid actorId, string reason, string resolutionPolicySnapshotJson, DateTimeOffset occurredAt)
    {
        Result = PurchaseInvoiceMatchResult.ResolvedException;
        ResolvedByActorId = actorId;
        ResolvedAt = occurredAt;
        ResolutionReason = reason;
        ResolutionPolicySnapshotJson = resolutionPolicySnapshotJson;
        TouchVersion();
    }

    internal void TouchVersion() => Version = Guid.NewGuid().ToByteArray();

    internal void SetReplayResponseSnapshot(int schemaVersion, string snapshotJson)
    {
        ReplayResponseSchemaVersion = schemaVersion;
        ReplayResponseSnapshotJson = snapshotJson;
    }
}

internal sealed class PurchaseInvoiceMatchHistoryEntity : ITenantOwned
{
    private PurchaseInvoiceMatchHistoryEntity() => CorrelationId = string.Empty;

    internal PurchaseInvoiceMatchHistoryEntity(
        TenantId tenantId,
        Guid id,
        Guid matchEvaluationId,
        Guid handoffId,
        PurchaseInvoiceMatchResult result,
        string action,
        Guid actorId,
        string? reason,
        DateTimeOffset occurredAt,
        string correlationId)
    {
        Id = id;
        TenantId = tenantId;
        PurchaseInvoiceMatchEvaluationId = matchEvaluationId;
        PurchaseInvoiceHandoffId = handoffId;
        Result = result;
        Action = action;
        ActorId = actorId;
        Reason = reason;
        OccurredAt = occurredAt;
        CorrelationId = correlationId;
    }

    internal Guid Id { get; private set; }
    public TenantId TenantId { get; private set; }
    internal Guid PurchaseInvoiceMatchEvaluationId { get; private set; }
    internal Guid PurchaseInvoiceHandoffId { get; private set; }
    internal PurchaseInvoiceMatchResult Result { get; private set; }
    internal string Action { get; private set; } = string.Empty;
    internal Guid ActorId { get; private set; }
    internal string? Reason { get; private set; }
    internal DateTimeOffset OccurredAt { get; private set; }
    internal string CorrelationId { get; private set; }
    internal byte[] Version { get; private set; } = [];
}

internal sealed class PurchaseInvoiceMatchAuditEntity : ITenantOwned
{
    private PurchaseInvoiceMatchAuditEntity()
    {
        OperationId = string.Empty;
        CorrelationId = string.Empty;
        Decision = string.Empty;
    }

    internal PurchaseInvoiceMatchAuditEntity(PurchaseInvoiceMatchAuditEvidence evidence)
    {
        Id = evidence.Id;
        TenantId = new TenantId(evidence.TenantId);
        PurchaseInvoiceMatchEvaluationId = evidence.MatchEvaluationId;
        PurchaseInvoiceHandoffId = evidence.PurchaseInvoiceHandoffId;
        OccurredAt = evidence.OccurredAt;
        OperationId = evidence.OperationId;
        CorrelationId = evidence.CorrelationId;
        ActorId = evidence.ActorId;
        Decision = evidence.Decision;
        Reason = evidence.Reason;
        IdempotencyKey = evidence.IdempotencyKey;
        RequestFingerprint = evidence.RequestFingerprint;
    }

    internal Guid Id { get; private set; }
    public TenantId TenantId { get; private set; }
    internal Guid PurchaseInvoiceMatchEvaluationId { get; private set; }
    internal Guid PurchaseInvoiceHandoffId { get; private set; }
    internal DateTimeOffset OccurredAt { get; private set; }
    internal string OperationId { get; private set; }
    internal string CorrelationId { get; private set; }
    internal Guid ActorId { get; private set; }
    internal string Decision { get; private set; }
    internal string? Reason { get; private set; }
    internal string? IdempotencyKey { get; private set; }
    internal string? RequestFingerprint { get; private set; }
    internal int? ReplayResponseSchemaVersion { get; private set; }
    internal string? ReplayResponseSnapshotJson { get; private set; }
    internal byte[] Version { get; private set; } = [];

    internal void SetReplayResponseSnapshot(int schemaVersion, string snapshotJson)
    {
        ReplayResponseSchemaVersion = schemaVersion;
        ReplayResponseSnapshotJson = snapshotJson;
    }
}
