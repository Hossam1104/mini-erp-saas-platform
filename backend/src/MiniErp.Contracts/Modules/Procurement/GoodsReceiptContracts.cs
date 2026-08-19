#pragma warning disable CS1591

namespace MiniErp.Contracts.Modules.Procurement;

public enum GoodsReceiptStatus
{
    Recorded = 1,
    Cancelled = 2
}

public enum GoodsReceiptHistoryAction
{
    Recorded = 1,
    Cancelled = 2
}

public sealed record GoodsReceiptLineCreateRequest(
    Guid PurchaseOrderLineId,
    decimal ReceivedQuantity,
    decimal AcceptedQuantity,
    decimal RejectedQuantity,
    decimal? DamagedQuantity,
    string? DamageNotes,
    string? Notes);

public sealed record GoodsReceiptCreateRequest(
    Guid PurchaseOrderId,
    Guid WarehouseId,
    DateOnly ReceivedDate,
    string? ReferenceNote,
    string? Notes,
    IReadOnlyList<GoodsReceiptLineCreateRequest> Lines);

public sealed record GoodsReceiptActionRequest(string? Reason);

public sealed record GoodsReceiptEligibleLineResponse(
    Guid PurchaseOrderId,
    Guid PurchaseOrderLineId,
    Guid ProductId,
    string ProductSku,
    string ProductName,
    string UnitOfMeasureCode,
    decimal ConfirmedQuantity,
    decimal AlreadyReceivedAcceptedQuantity,
    decimal RemainingReceivableQuantity);

public sealed record GoodsReceiptEligibleSourceResponse(
    Guid PurchaseOrderId,
    Guid CompanyId,
    Guid? BranchId,
    string Status,
    Guid SupplierId,
    string SupplierCode,
    string SupplierName,
    string CurrencyCode,
    IReadOnlyList<GoodsReceiptEligibleLineResponse> Lines);

public sealed record GoodsReceiptLineResponse(
    Guid Id,
    Guid PurchaseOrderLineId,
    Guid ProductId,
    string ProductSku,
    string ProductName,
    string UnitOfMeasureCode,
    decimal OrderedQuantityAtReceipt,
    decimal ReceivedQuantity,
    decimal AcceptedQuantity,
    decimal RejectedQuantity,
    decimal? DamagedQuantity,
    string? DamageNotes,
    decimal RemainingReceivableQuantityAfter,
    string? Notes);

public sealed record GoodsReceiptListItemResponse(
    Guid Id,
    Guid TenantId,
    Guid CompanyId,
    Guid? BranchId,
    Guid PurchaseOrderId,
    Guid WarehouseId,
    string Status,
    string SupplierCode,
    string SupplierName,
    DateOnly ReceivedDate,
    int LineCount,
    decimal TotalAcceptedQuantity,
    DateTimeOffset CreatedAt,
    string Version);

public sealed record GoodsReceiptResponse(
    Guid Id,
    Guid TenantId,
    Guid CompanyId,
    Guid? BranchId,
    Guid PurchaseOrderId,
    Guid WarehouseId,
    Guid ReceivedByActorId,
    string Status,
    Guid SupplierId,
    string SupplierCode,
    string SupplierName,
    DateOnly ReceivedDate,
    string? ReferenceNote,
    string? Notes,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? CancelledAt,
    string? CancellationReason,
    IReadOnlyList<GoodsReceiptLineResponse> Lines,
    string Version,
    bool CanCancel);

public sealed record GoodsReceiptHistoryResponse(
    Guid EvidenceId,
    Guid GoodsReceiptId,
    DateTimeOffset OccurredAt,
    string FromStatus,
    string ToStatus,
    string Action,
    Guid ActorId,
    string? Reason,
    string CorrelationId);

public sealed record GoodsReceiptAuditResponse(
    Guid EvidenceId,
    Guid GoodsReceiptId,
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

#pragma warning restore CS1591
