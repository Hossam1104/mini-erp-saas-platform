#pragma warning disable CS1591

using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.Modules.Procurement;
using MiniErp.Contracts.Modules.Procurement;

namespace MiniErp.Infrastructure.Persistence.Modules.Procurement;

internal sealed class PurchaseRequestEntity : ITenantOwned
{
    private PurchaseRequestEntity()
    {
        Purpose = string.Empty;
        ApprovalPolicySnapshotJson = string.Empty;
        CurrentStageApproverIdsJson = "[]";
        Lines = [];
    }

    internal PurchaseRequestEntity(
        Guid id,
        TenantId tenantId,
        Guid companyId,
        Guid? branchId,
        Guid requesterId,
        string? purpose,
        DateTimeOffset occurredAt)
    {
        Id = id;
        TenantId = tenantId;
        CompanyId = companyId;
        BranchId = branchId;
        RequesterId = requesterId;
        Purpose = purpose;
        Status = PurchaseRequestStatus.Draft;
        CreatedAt = occurredAt;
        UpdatedAt = occurredAt;
        ApprovalPolicySnapshotJson = string.Empty;
        CurrentStageApproverIdsJson = "[]";
        Lines = [];
    }

    internal Guid Id { get; private set; }

    public TenantId TenantId { get; private set; }

    internal Guid CompanyId { get; private set; }

    internal Guid? BranchId { get; private set; }

    internal Guid RequesterId { get; private set; }

    internal string? Purpose { get; private set; }

    internal PurchaseRequestStatus Status { get; private set; }

    internal DateTimeOffset CreatedAt { get; private set; }

    internal DateTimeOffset UpdatedAt { get; private set; }

    internal DateTimeOffset? SubmittedAt { get; private set; }

    internal DateTimeOffset? ApprovedAt { get; private set; }

    internal DateTimeOffset? CancelledAt { get; private set; }

    internal string ApprovalPolicySnapshotJson { get; private set; }

    internal int CurrentApprovalStageIndex { get; private set; }

    internal int CurrentStageApprovalCount { get; private set; }

    internal string CurrentStageApproverIdsJson { get; private set; }

    internal byte[] Version { get; private set; } = [];

    internal ICollection<PurchaseRequestLineEntity> Lines { get; private set; }

    internal void ReplaceDraft(
        Guid companyId,
        Guid? branchId,
        string? purpose,
        DateTimeOffset occurredAt)
    {
        CompanyId = companyId;
        BranchId = branchId;
        Purpose = purpose;
        UpdatedAt = occurredAt;
    }

    internal void Submit(
        PurchaseRequestApprovalPolicyDefinition policy,
        string policyJson,
        DateTimeOffset occurredAt)
    {
        ApprovalPolicySnapshotJson = policyJson;
        CurrentApprovalStageIndex = 0;
        CurrentStageApprovalCount = 0;
        CurrentStageApproverIdsJson = "[]";
        Status = PurchaseRequestStatus.PendingApproval;
        SubmittedAt = occurredAt;
        UpdatedAt = occurredAt;
    }

    internal void RecordApproval(
        PurchaseRequestStatus resultingStatus,
        int stageIndex,
        int stageApprovalCount,
        string approverIdsJson,
        DateTimeOffset occurredAt)
    {
        Status = resultingStatus;
        CurrentApprovalStageIndex = stageIndex;
        CurrentStageApprovalCount = stageApprovalCount;
        CurrentStageApproverIdsJson = approverIdsJson;
        if (resultingStatus == PurchaseRequestStatus.Approved)
        {
            ApprovedAt = occurredAt;
        }

        UpdatedAt = occurredAt;
    }

    internal void SetDecision(PurchaseRequestStatus status, DateTimeOffset occurredAt)
    {
        Status = status;
        if (status == PurchaseRequestStatus.Cancelled)
        {
            CancelledAt = occurredAt;
        }

        UpdatedAt = occurredAt;
    }

    internal void TouchVersion() => Version = Guid.NewGuid().ToByteArray();
}

internal sealed class PurchaseRequestLineEntity : ITenantOwned
{
    private PurchaseRequestLineEntity()
    {
        ProductSku = string.Empty;
        ProductName = string.Empty;
        UnitOfMeasureCode = string.Empty;
        Purpose = string.Empty;
    }

    internal PurchaseRequestLineEntity(
        Guid id,
        TenantId tenantId,
        Guid purchaseRequestId,
        PurchaseRequestLineSnapshot snapshot)
    {
        Id = id;
        TenantId = tenantId;
        PurchaseRequestId = purchaseRequestId;
        ProductId = snapshot.ProductId;
        ProductSku = snapshot.ProductSku;
        ProductName = snapshot.ProductName;
        UnitOfMeasureId = snapshot.UnitOfMeasureId;
        UnitOfMeasureCode = snapshot.UnitOfMeasureCode;
        Quantity = snapshot.Quantity;
        NeedByDate = snapshot.NeedByDate;
        Purpose = snapshot.Purpose;
    }

    internal Guid Id { get; private set; }

    public TenantId TenantId { get; private set; }

    internal Guid PurchaseRequestId { get; private set; }

    internal Guid ProductId { get; private set; }

    internal string ProductSku { get; private set; }

    internal string ProductName { get; private set; }

    internal Guid UnitOfMeasureId { get; private set; }

    internal string UnitOfMeasureCode { get; private set; }

    internal decimal Quantity { get; private set; }

    internal DateOnly NeedByDate { get; private set; }

    internal string Purpose { get; private set; }

    internal byte[] Version { get; private set; } = [];
}

internal sealed class PurchaseRequestHistoryEntity : ITenantOwned
{
    private PurchaseRequestHistoryEntity()
    {
        CorrelationId = string.Empty;
    }

    internal PurchaseRequestHistoryEntity(
        Guid evidenceId,
        TenantId tenantId,
        Guid purchaseRequestId,
        PurchaseRequestStatus fromStatus,
        PurchaseRequestStatus toStatus,
        PurchaseRequestHistoryAction action,
        Guid actorId,
        string? reason,
        string correlationId,
        string? policyId,
        int? policyVersion,
        string? stageKey,
        Guid? delegatedFromActorId,
        DateTimeOffset occurredAt)
    {
        Id = evidenceId;
        TenantId = tenantId;
        PurchaseRequestId = purchaseRequestId;
        FromStatus = fromStatus;
        ToStatus = toStatus;
        Action = action;
        ActorId = actorId;
        Reason = reason;
        CorrelationId = correlationId;
        PolicyId = policyId;
        PolicyVersion = policyVersion;
        StageKey = stageKey;
        DelegatedFromActorId = delegatedFromActorId;
        OccurredAt = occurredAt;
    }

    internal Guid Id { get; private set; }

    public TenantId TenantId { get; private set; }

    internal Guid PurchaseRequestId { get; private set; }

    internal PurchaseRequestStatus FromStatus { get; private set; }

    internal PurchaseRequestStatus ToStatus { get; private set; }

    internal PurchaseRequestHistoryAction Action { get; private set; }

    internal Guid ActorId { get; private set; }

    internal string? Reason { get; private set; }

    internal string CorrelationId { get; private set; }

    internal string? PolicyId { get; private set; }

    internal int? PolicyVersion { get; private set; }

    internal string? StageKey { get; private set; }

    internal Guid? DelegatedFromActorId { get; private set; }

    internal DateTimeOffset OccurredAt { get; private set; }

    internal byte[] Version { get; private set; } = [];
}

internal sealed class PurchaseRequestAuditEntity : ITenantOwned
{
    private PurchaseRequestAuditEntity()
    {
        OperationId = string.Empty;
        CorrelationId = string.Empty;
        AuthorizationPath = string.Empty;
        Decision = string.Empty;
    }

    internal PurchaseRequestAuditEntity(
        PurchaseRequestAuditEvidence evidence)
    {
        Id = evidence.EvidenceId;
        TenantId = new TenantId(evidence.TenantId);
        PurchaseRequestId = evidence.PurchaseRequestId;
        OccurredAt = evidence.OccurredAt;
        OperationId = evidence.OperationId;
        CorrelationId = evidence.CorrelationId;
        ActorId = evidence.ActorId;
        SessionId = evidence.SessionId;
        AuthorizationPath = evidence.AuthorizationPath;
        Decision = evidence.Decision;
        Reason = evidence.Reason;
        BeforeStatus = evidence.BeforeStatus;
        AfterStatus = evidence.AfterStatus;
        CompanyId = evidence.CompanyId;
        BranchId = evidence.BranchId;
        BeforeSummary = evidence.BeforeSummary;
        AfterSummary = evidence.AfterSummary;
        IdempotencyKey = evidence.IdempotencyKey;
    }

    internal Guid Id { get; private set; }

    public TenantId TenantId { get; private set; }

    internal Guid PurchaseRequestId { get; private set; }

    internal DateTimeOffset OccurredAt { get; private set; }

    internal string OperationId { get; private set; }

    internal string CorrelationId { get; private set; }

    internal Guid ActorId { get; private set; }

    internal Guid SessionId { get; private set; }

    internal string AuthorizationPath { get; private set; }

    internal string Decision { get; private set; }

    internal string? Reason { get; private set; }

    internal PurchaseRequestStatus? BeforeStatus { get; private set; }

    internal PurchaseRequestStatus? AfterStatus { get; private set; }

    internal Guid CompanyId { get; private set; }

    internal Guid? BranchId { get; private set; }

    internal string? BeforeSummary { get; private set; }

    internal string? AfterSummary { get; private set; }

    internal string? IdempotencyKey { get; private set; }

    internal byte[] Version { get; private set; } = [];
}

#pragma warning restore CS1591
