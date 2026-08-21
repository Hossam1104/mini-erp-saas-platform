#pragma warning disable CS1591

namespace MiniErp.Contracts.Modules.Procurement;

public enum SupplierReturnStatus
{
    Draft = 1,
    Submitted = 2,
    Approved = 3,
    Rejected = 4,
    Cancelled = 5,
    AwaitingInventory = 6,
    InventoryHandoffRecorded = 7,
    AwaitingFinance = 8,
    FinanceReferenceRecorded = 9,
    Completed = 10,
    Reversed = 11,
    CorrectionLinked = 12
}

public enum SupplierReturnHistoryAction
{
    Created = 1,
    Submitted = 2,
    Approved = 3,
    Rejected = 4,
    Cancelled = 5,
    InventoryHandoffRecorded = 6,
    FinanceReferenceRecorded = 7,
    Completed = 8,
    Reversed = 9,
    CorrectionLinked = 10
}

public enum SupplierReturnReasonCode
{
    Damaged = 1,
    Defective = 2,
    WrongItem = 3,
    ExcessQuantity = 4,
    SupplierNonConformance = 5,
    Other = 6
}

public enum SupplierReturnCondition
{
    Unusable = 1,
    Reworkable = 2,
    Sealed = 3,
    Other = 4
}

public enum SupplierReturnCommercialOutcome
{
    CreditExpected = 1,
    ReplacementExpected = 2,
    NoCreditExpected = 3,
    OtherCorrectionExpected = 4
}

public sealed record SupplierReturnEvidenceReferenceWriteRequest(
    string? ReferenceId,
    string? FileName,
    string? ContentType,
    string? Description,
    string? Source);

public sealed record SupplierReturnLineCreateRequest(
    Guid GoodsReceiptLineId,
    decimal ReturnQuantity,
    string? Notes);

public sealed record SupplierReturnCreateRequest(
    Guid GoodsReceiptId,
    DateOnly ReturnDate,
    SupplierReturnReasonCode ReasonCode,
    SupplierReturnCondition Condition,
    SupplierReturnCommercialOutcome CommercialOutcome,
    string? ReasonDetail,
    string? Notes,
    IReadOnlyList<SupplierReturnLineCreateRequest> Lines,
    IReadOnlyList<SupplierReturnEvidenceReferenceWriteRequest>? Evidence);

public sealed record SupplierReturnActionRequest(string? Reason);

public sealed record SupplierReturnInventoryHandoffRequest(
    string? HandoffReference,
    string? Notes);

public sealed record SupplierReturnFinanceReferenceRequest(
    string? FinanceReference,
    string? CurrencyCode,
    decimal? Amount,
    string? Notes);

public sealed record SupplierReturnEligibleLineResponse(
    Guid GoodsReceiptId,
    Guid GoodsReceiptLineId,
    Guid PurchaseOrderId,
    Guid PurchaseOrderLineId,
    Guid WarehouseId,
    Guid ProductId,
    string ProductSku,
    string ProductName,
    string UnitOfMeasureCode,
    decimal AcceptedQuantity,
    decimal AlreadyReturnedQuantity,
    decimal EligibleReturnQuantity,
    DateOnly ReceivedDate);

public sealed record SupplierReturnEligibleSourceResponse(
    Guid GoodsReceiptId,
    Guid PurchaseOrderId,
    Guid? SupplierConfirmationId,
    Guid CompanyId,
    Guid? BranchId,
    Guid WarehouseId,
    Guid SupplierId,
    string SupplierCode,
    string SupplierName,
    string CurrencyCode,
    IReadOnlyList<SupplierReturnEligibleLineResponse> Lines);

public sealed record SupplierReturnEvidenceReferenceResponse(
    Guid Id,
    string ReferenceId,
    string? FileName,
    string? ContentType,
    string? Description,
    string Source,
    DateTimeOffset RecordedAt);

public sealed record SupplierReturnLineResponse(
    Guid Id,
    Guid GoodsReceiptLineId,
    Guid PurchaseOrderLineId,
    Guid ProductId,
    string ProductSku,
    string ProductName,
    string UnitOfMeasureCode,
    decimal AcceptedQuantityAtReturn,
    decimal ReturnQuantity,
    decimal? EligibleQuantityAfter,
    string? Notes);

public sealed record SupplierReturnListItemResponse(
    Guid Id,
    Guid TenantId,
    Guid CompanyId,
    Guid? BranchId,
    Guid GoodsReceiptId,
    Guid PurchaseOrderId,
    Guid WarehouseId,
    string SupplierCode,
    string SupplierName,
    string Status,
    string ReasonCode,
    string CommercialOutcome,
    decimal TotalReturnQuantity,
    DateOnly ReturnDate,
    DateTimeOffset CreatedAt,
    string Version);

public sealed record SupplierReturnResponse(
    Guid Id,
    Guid TenantId,
    Guid CompanyId,
    Guid? BranchId,
    Guid GoodsReceiptId,
    Guid PurchaseOrderId,
    Guid? SupplierConfirmationId,
    Guid WarehouseId,
    Guid SupplierId,
    string SupplierCode,
    string SupplierName,
    string CurrencyCode,
    string Status,
    string ReasonCode,
    string Condition,
    string CommercialOutcome,
    string? ReasonDetail,
    string? Notes,
    DateOnly ReturnDate,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? CancelledAt,
    DateTimeOffset? ReversedAt,
    Guid? CorrectionOfId,
    Guid? InventoryHandoffId,
    string? InventoryHandoffReference,
    string? FinanceReference,
    string? FinanceCurrencyCode,
    decimal? FinanceAmount,
    IReadOnlyList<SupplierReturnLineResponse> Lines,
    IReadOnlyList<SupplierReturnEvidenceReferenceResponse> Evidence,
    string Version,
    bool CanSubmit,
    bool CanApprove,
    bool CanCancel,
    bool CanReverse,
    bool CanCorrect);

public sealed record SupplierReturnHistoryResponse(
    Guid EvidenceId,
    Guid SupplierReturnId,
    DateTimeOffset OccurredAt,
    string FromStatus,
    string ToStatus,
    string Action,
    Guid ActorId,
    string? Reason,
    string CorrelationId);

public sealed record SupplierReturnAuditResponse(
    Guid EvidenceId,
    Guid SupplierReturnId,
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

public sealed record SupplierReturnReportResponse(
    int ReturnCount,
    decimal TotalReturnQuantity,
    int OpenReturnCount,
    decimal OpenReturnQuantity,
    int PendingInventoryCount,
    int PendingFinanceCount,
    IReadOnlyList<SupplierReturnListItemResponse> Returns);

#pragma warning restore CS1591
