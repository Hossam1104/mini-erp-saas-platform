#pragma warning disable CS1591

namespace MiniErp.Contracts.Modules.Procurement;

public enum PurchaseRequestStatus
{
    Draft = 1,
    PendingApproval = 2,
    Approved = 3,
    Rejected = 4,
    ReturnedForChange = 5,
    Cancelled = 6
}

public enum PurchaseRequestHistoryAction
{
    Created = 1,
    Edited = 2,
    Submitted = 3,
    ApprovalRecorded = 4,
    Rejected = 5,
    ReturnedForChange = 6,
    Cancelled = 7
}

public sealed record PurchaseRequestLineWriteRequest(
    Guid ProductId,
    Guid UnitOfMeasureId,
    decimal Quantity,
    DateOnly NeedByDate,
    string? Purpose);

public sealed record PurchaseRequestWriteRequest(
    Guid CompanyId,
    Guid? BranchId,
    string? Purpose,
    IReadOnlyList<PurchaseRequestLineWriteRequest>? Lines);

public sealed record PurchaseRequestActionRequest(string? Reason = null);

public sealed record PurchaseRequestLineResponse(
    Guid Id,
    Guid ProductId,
    string ProductSku,
    string ProductName,
    Guid UnitOfMeasureId,
    string UnitOfMeasureCode,
    decimal Quantity,
    DateOnly NeedByDate,
    string Purpose,
    byte[] Version);

public sealed record PurchaseRequestApprovalResponse(
    string PolicyId,
    int PolicyVersion,
    int StageIndex,
    string StageKey,
    int RequiredApprovals,
    int RecordedApprovals,
    bool AllowDelegation,
    bool AllowRequesterCancellationWhilePending);

public sealed record PurchaseRequestResponse(
    Guid Id,
    Guid TenantId,
    Guid CompanyId,
    Guid? BranchId,
    Guid RequesterId,
    string Status,
    string? Purpose,
    IReadOnlyList<PurchaseRequestLineResponse> Lines,
    PurchaseRequestApprovalResponse? Approval,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? SubmittedAt,
    DateTimeOffset? ApprovedAt,
    DateTimeOffset? CancelledAt,
    byte[] Version,
    bool CanEdit,
    bool CanSubmit,
    bool CanApprove,
    bool CanReject,
    bool CanReturnForChange,
    bool CanCancel);

public sealed record PurchaseRequestListItemResponse(
    Guid Id,
    Guid CompanyId,
    Guid? BranchId,
    Guid RequesterId,
    string Status,
    string? Purpose,
    int LineCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    byte[] Version);

public sealed record PurchaseRequestHistoryResponse(
    Guid EvidenceId,
    Guid PurchaseRequestId,
    DateTimeOffset OccurredAt,
    string FromStatus,
    string ToStatus,
    string Action,
    Guid ActorId,
    string? Reason,
    string CorrelationId,
    string? PolicyId,
    int? PolicyVersion,
    string? StageKey,
    Guid? DelegatedFromActorId);

public sealed record PurchaseRequestAuditResponse(
    Guid EvidenceId,
    Guid PurchaseRequestId,
    DateTimeOffset OccurredAt,
    string OperationId,
    string CorrelationId,
    Guid TenantId,
    Guid ActorId,
    Guid SessionId,
    string AuthorizationPath,
    string Decision,
    string? Reason,
    string? BeforeStatus,
    string? AfterStatus,
    Guid CompanyId,
    Guid? BranchId,
    string? BeforeSummary,
    string? AfterSummary,
    string? IdempotencyKey);

public sealed record PurchaseRequestApprovalStageDefinition(
    string StageKey,
    int Sequence,
    int RequiredApprovals,
    IReadOnlyList<Guid>? EligibleApproverIds = null,
    bool AllowDelegation = true);

public sealed record PurchaseRequestApprovalPolicyDefinition(
    string PolicyId,
    int Version,
    IReadOnlyList<PurchaseRequestApprovalStageDefinition> Stages,
    bool AllowRequesterCancellationWhilePending,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo = null);

#pragma warning restore CS1591
