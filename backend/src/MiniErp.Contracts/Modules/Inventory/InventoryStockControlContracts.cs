#pragma warning disable CS1591

namespace MiniErp.Contracts.Modules.Inventory;

public enum InventoryReasonCategory
{
    Adjustment = 1,
    CountVariance = 2,
    StockIssue = 3
}

public enum InventoryAdjustmentDirection
{
    Increase = 1,
    Decrease = 2
}

public enum InventoryControlDocumentStatus
{
    Draft = 1,
    Submitted = 2,
    PendingApproval = 3,
    Approved = 4,
    Rejected = 5,
    ReturnedForChange = 6,
    Posted = 7,
    Corrected = 8,
    RecountRequired = 9,
    ResnapshotRequired = 10,
    Blocked = 11
}

public enum InventoryCountType
{
    Full = 1,
    Cycle = 2
}

public enum InventoryControlHistoryAction
{
    Created = 1,
    Submitted = 2,
    Approved = 3,
    Rejected = 4,
    ReturnedForChange = 5,
    Posted = 6,
    PostBlocked = 7,
    Corrected = 8,
    Snapshot = 9,
    CountSubmitted = 10,
    RecountRequested = 11,
    Resnapshot = 12,
    VarianceReasonRecorded = 13,
    AuthorizationDenied = 14,
    TrackingRejected = 15,
    IdempotencyConflict = 16,
    ConcurrencyConflict = 17
}

public sealed record InventoryReasonCodeCreateRequest(
    string Code,
    string EnglishName,
    string ArabicName,
    InventoryReasonCategory Category);

public sealed record InventoryReasonCodeUpdateRequest(
    string EnglishName,
    string ArabicName,
    InventoryReasonCategory Category,
    bool IsActive);

public sealed record InventoryReasonCodeRecord(
    Guid Id,
    Guid TenantId,
    string Code,
    string EnglishName,
    string ArabicName,
    InventoryReasonCategory Category,
    bool IsActive,
    Guid CreatedByActorId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    byte[] Version);

public sealed record InventoryAdjustmentLineRequest(
    Guid ProductId,
    Guid UnitOfMeasureId,
    InventoryAdjustmentDirection Direction,
    decimal Quantity,
    string ReasonCode,
    string? TrackingIdentity = null,
    string? EvidenceReference = null);

public sealed record InventoryAdjustmentCreateRequest(
    Guid CompanyId,
    Guid? BranchId,
    Guid WarehouseId,
    string? EvidenceReference,
    IReadOnlyList<InventoryAdjustmentLineRequest> Lines);

public sealed record InventoryAdjustmentLineRecord(
    Guid Id,
    Guid ProductId,
    string ProductSku,
    string ProductName,
    Guid UnitOfMeasureId,
    string UnitOfMeasureCode,
    InventoryAdjustmentDirection Direction,
    decimal Quantity,
    string TrackingIdentity,
    Guid ReasonCodeId,
    string ReasonCode,
    string ReasonEnglishName,
    string ReasonArabicName,
    string? EvidenceReference,
    Guid? MovementId,
    byte[] Version);

public sealed record InventoryApprovalRecord(
    string PolicyId,
    int PolicyVersion,
    int StageIndex,
    string StageKey,
    int RequiredApprovals,
    int RecordedApprovals,
    bool AllowDelegation,
    Guid? LastApproverId,
    Guid? DelegatedFromActorId);

public sealed record InventoryAdjustmentRecord(
    Guid Id,
    Guid TenantId,
    Guid CompanyId,
    Guid? BranchId,
    Guid WarehouseId,
    string WarehouseCode,
    string WarehouseName,
    Guid RequesterId,
    InventoryControlDocumentStatus Status,
    string? EvidenceReference,
    IReadOnlyList<InventoryAdjustmentLineRecord> Lines,
    InventoryApprovalRecord? Approval,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? SubmittedAt,
    DateTimeOffset? ApprovedAt,
    DateTimeOffset? PostedAt,
    byte[] Version);

public sealed record InventoryCountLineRequest(
    Guid ProductId,
    Guid UnitOfMeasureId,
    string? TrackingIdentity = null);

public sealed record InventoryCountCreateRequest(
    Guid CompanyId,
    Guid? BranchId,
    Guid WarehouseId,
    InventoryCountType CountType,
    Guid AssignedCounterId,
    Guid? ReviewerId,
    IReadOnlyList<InventoryCountLineRequest>? Lines = null);

public sealed record InventoryCountObservationRequest(
    Guid CountLineId,
    decimal CountedQuantity);

public sealed record InventoryCountSubmitRequest(
    IReadOnlyList<InventoryCountObservationRequest> Observations);

public sealed record InventoryCountActionRequest(
    string? Reason = null,
    Guid? NewCounterId = null);

public sealed record InventoryCountVarianceReasonRequest(Guid CountLineId, string ReasonCode);

public sealed record InventoryControlActionRequest(string? Reason = null);

public sealed record InventoryCountLineRecord(
    Guid Id,
    Guid? PriorLineId,
    int RoundGeneration,
    Guid ProductId,
    string ProductSku,
    string ProductName,
    Guid UnitOfMeasureId,
    string UnitOfMeasureCode,
    string TrackingIdentity,
    decimal? ExpectedQuantity,
    decimal? CountedQuantity,
    decimal? Variance,
    Guid? VarianceReasonCodeId,
    string? VarianceReasonCode,
    string? VarianceReasonEnglishName,
    string? VarianceReasonArabicName,
    bool IsCurrentRound,
    DateTimeOffset? CountedAt,
    byte[] Version);

public sealed record InventoryCountRecord(
    Guid Id,
    Guid TenantId,
    Guid CompanyId,
    Guid? BranchId,
    Guid WarehouseId,
    string WarehouseCode,
    string WarehouseName,
    InventoryCountType CountType,
    Guid AssignedCounterId,
    Guid? ReviewerId,
    Guid? ApproverId,
    Guid? PosterId,
    InventoryControlDocumentStatus Status,
    int CurrentRoundGeneration,
    DateTimeOffset SnapshotCutoff,
    IReadOnlyList<InventoryCountLineRecord> Lines,
    InventoryApprovalRecord? Approval,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? SubmittedAt,
    DateTimeOffset? ApprovedAt,
    DateTimeOffset? PostedAt,
    byte[] Version);

public sealed record InventoryIssueLineRequest(
    Guid ProductId,
    Guid UnitOfMeasureId,
    decimal Quantity,
    string ReasonCode,
    string? TrackingIdentity = null,
    string? EvidenceReference = null);

public sealed record InventoryStockIssueCreateRequest(
    Guid CompanyId,
    Guid? BranchId,
    Guid WarehouseId,
    string DestinationUseDescription,
    IReadOnlyList<InventoryIssueLineRequest> Lines);

public sealed record InventoryStockIssueLineRecord(
    Guid Id,
    Guid ProductId,
    string ProductSku,
    string ProductName,
    Guid UnitOfMeasureId,
    string UnitOfMeasureCode,
    decimal Quantity,
    string TrackingIdentity,
    Guid ReasonCodeId,
    string ReasonCode,
    string ReasonEnglishName,
    string ReasonArabicName,
    string? EvidenceReference,
    Guid? MovementId,
    byte[] Version);

public sealed record InventoryStockIssueRecord(
    Guid Id,
    Guid TenantId,
    Guid CompanyId,
    Guid? BranchId,
    Guid WarehouseId,
    string WarehouseCode,
    string WarehouseName,
    Guid RequesterId,
    string DestinationUseDescription,
    InventoryControlDocumentStatus Status,
    IReadOnlyList<InventoryStockIssueLineRecord> Lines,
    InventoryApprovalRecord? Approval,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? SubmittedAt,
    DateTimeOffset? ApprovedAt,
    DateTimeOffset? PostedAt,
    byte[] Version);

public sealed record InventoryControlHistoryRecord(
    Guid Id,
    string ResourceType,
    Guid ResourceId,
    Guid? LineId,
    InventoryControlHistoryAction Action,
    InventoryControlDocumentStatus FromStatus,
    InventoryControlDocumentStatus ToStatus,
    Guid ActorId,
    Guid? DelegatedFromActorId,
    string? Reason,
    string CorrelationId,
    int RoundGeneration,
    DateTimeOffset OccurredAt,
    byte[] Version);

public sealed record InventoryCorrectionRequest(
    string ReasonCode,
    string? Reason = null);

#pragma warning restore CS1591
