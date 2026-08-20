#pragma warning disable CS1591

namespace MiniErp.Contracts.Modules.Procurement;

public enum PurchaseInvoiceHandoffStatus
{
    Recorded = 1,
    Cancelled = 2
}

public enum PurchaseInvoiceHandoffHistoryAction
{
    Recorded = 1,
    Cancelled = 2,
    DeclaredEvidenceCaptured = 3
}

public sealed record PurchaseInvoiceHandoffSourceRequest(
    Guid GoodsReceiptId,
    Guid GoodsReceiptLineId,
    decimal Quantity);

public sealed record PurchaseInvoiceDeclaredEvidenceAllocationRequest(
    Guid GoodsReceiptId,
    Guid GoodsReceiptLineId,
    decimal Quantity);

public sealed record PurchaseInvoiceDeclaredEvidenceLineRequest(
    Guid PurchaseOrderLineId,
    decimal Quantity,
    decimal UnitPrice,
    decimal? DiscountAmount,
    decimal? TaxRatePercentage,
    string? TaxCode,
    decimal? TaxAmount,
    decimal? NetAmount,
    decimal? GrossAmount,
    string? Description,
    IReadOnlyList<PurchaseInvoiceDeclaredEvidenceAllocationRequest> Allocations);

public sealed record PurchaseInvoiceDeclaredEvidenceRequest(
    string? SupplierInvoiceReference,
    DateOnly? SupplierInvoiceDate,
    string? CurrencyCode,
    decimal? SubtotalAmount,
    decimal? DiscountAmount,
    decimal? TaxAmount,
    decimal? GrossAmount,
    IReadOnlyList<PurchaseInvoiceDeclaredEvidenceLineRequest> Lines);

public sealed record PurchaseInvoiceHandoffCreateRequest(
    Guid PurchaseOrderId,
    string? SupplierInvoiceReference,
    DateOnly? SupplierInvoiceDate,
    string? Notes,
    IReadOnlyList<PurchaseInvoiceHandoffSourceRequest> Sources,
    PurchaseInvoiceDeclaredEvidenceRequest? DeclaredEvidence = null);

public sealed record PurchaseInvoiceDeclaredEvidenceCaptureRequest(
    PurchaseInvoiceDeclaredEvidenceRequest Evidence,
    string? Reason);

public sealed record PurchaseInvoiceHandoffActionRequest(string? Reason);

public sealed record PurchaseInvoiceHandoffEligibleLineResponse(
    Guid GoodsReceiptId,
    Guid GoodsReceiptLineId,
    Guid PurchaseOrderLineId,
    Guid ProductId,
    string ProductSku,
    string ProductName,
    string UnitOfMeasureCode,
    DateOnly ReceivedDate,
    decimal AcceptedQuantity,
    decimal AlreadyHandedOffQuantity,
    decimal RemainingHandoffQuantity,
    decimal UnitPrice);

public sealed record PurchaseInvoiceHandoffEligibleSourceResponse(
    Guid PurchaseOrderId,
    Guid CompanyId,
    Guid? BranchId,
    Guid SupplierId,
    string SupplierCode,
    string SupplierName,
    string CurrencyCode,
    IReadOnlyList<PurchaseInvoiceHandoffEligibleLineResponse> Lines);

public sealed record PurchaseInvoiceHandoffSourceResponse(
    Guid Id,
    Guid GoodsReceiptId,
    Guid GoodsReceiptLineId,
    Guid PurchaseOrderLineId,
    decimal Quantity);

public sealed record PurchaseInvoiceHandoffLineResponse(
    Guid Id,
    Guid PurchaseOrderLineId,
    Guid ProductId,
    string ProductSku,
    string ProductName,
    string UnitOfMeasureCode,
    decimal HandoffQuantity,
    decimal UnitPrice,
    decimal? TaxRatePercentage,
    decimal? TaxAmount,
    decimal LineAmount);

public sealed record PurchaseInvoiceHandoffListItemResponse(
    Guid Id,
    Guid TenantId,
    Guid CompanyId,
    Guid? BranchId,
    Guid PurchaseOrderId,
    string Status,
    string SupplierCode,
    string SupplierName,
    string CurrencyCode,
    decimal Total,
    int LineCount,
    DateTimeOffset CreatedAt,
    string Version);

public sealed record PurchaseInvoiceHandoffResponse(
    Guid Id,
    Guid TenantId,
    Guid CompanyId,
    Guid? BranchId,
    Guid PurchaseOrderId,
    Guid CreatedByActorId,
    string Status,
    Guid SupplierId,
    string SupplierCode,
    string SupplierName,
    string CurrencyCode,
    string? SupplierInvoiceReference,
    DateOnly? SupplierInvoiceDate,
    string? Notes,
    decimal Total,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? CancelledAt,
    string? CancellationReason,
    IReadOnlyList<PurchaseInvoiceHandoffLineResponse> Lines,
    IReadOnlyList<PurchaseInvoiceHandoffSourceResponse> Sources,
    string Version,
    bool CanCancel,
    PurchaseInvoiceDeclaredEvidenceResponse? DeclaredEvidence = null);

public sealed record PurchaseInvoiceDeclaredEvidenceAllocationResponse(
    Guid GoodsReceiptId,
    Guid GoodsReceiptLineId,
    decimal Quantity);

public sealed record PurchaseInvoiceDeclaredEvidenceLineResponse(
    Guid Id,
    Guid PurchaseOrderLineId,
    decimal Quantity,
    decimal UnitPrice,
    decimal? DiscountAmount,
    decimal? TaxRatePercentage,
    string? TaxCode,
    decimal? TaxAmount,
    decimal? NetAmount,
    decimal? GrossAmount,
    string? Description,
    IReadOnlyList<PurchaseInvoiceDeclaredEvidenceAllocationResponse> Allocations);

public sealed record PurchaseInvoiceDeclaredEvidenceResponse(
    Guid Id,
    int VersionNumber,
    string? SupplierInvoiceReference,
    DateOnly? SupplierInvoiceDate,
    string CurrencyCode,
    decimal? SubtotalAmount,
    decimal? DiscountAmount,
    decimal? TaxAmount,
    decimal? GrossAmount,
    DateTimeOffset RecordedAt,
    Guid RecordedByActorId,
    IReadOnlyList<PurchaseInvoiceDeclaredEvidenceLineResponse> Lines);

public sealed record PurchaseInvoiceHandoffHistoryResponse(
    Guid EvidenceId,
    Guid PurchaseInvoiceHandoffId,
    DateTimeOffset OccurredAt,
    string FromStatus,
    string ToStatus,
    string Action,
    Guid ActorId,
    string? Reason,
    string CorrelationId);

public sealed record PurchaseInvoiceHandoffAuditResponse(
    Guid EvidenceId,
    Guid PurchaseInvoiceHandoffId,
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
