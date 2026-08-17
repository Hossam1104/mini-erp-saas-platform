#pragma warning disable CS1591

namespace MiniErp.Contracts.Modules.Procurement;

public enum PurchaseOrderStatus
{
    Draft = 1,
    PendingApproval = 2,
    Approved = 3,
    Issued = 4,
    Confirmed = 5,
    PartiallyConfirmed = 6,
    ChangedPendingApproval = 7,
    Rejected = 8,
    NoResponse = 9,
    ReturnedForChange = 10,
    Cancelled = 11
}

public enum PurchaseOrderHistoryAction
{
    Created = 1,
    Edited = 2,
    Submitted = 3,
    ApprovalRecorded = 4,
    ReturnedForChange = 5,
    Rejected = 6,
    Issued = 7,
    ConfirmationRecorded = 8,
    PartiallyConfirmed = 9,
    SupplierRejected = 10,
    NoResponse = 11,
    SupplierChangeProposed = 12,
    SupplierChangeApproved = 13,
    SupplierChangeRejected = 14,
    Cancelled = 15
}

public enum PurchaseOrderConfirmationStatus
{
    Confirmed = 1,
    PartiallyConfirmed = 2,
    Rejected = 3,
    NoResponse = 4
}

public enum PurchaseOrderSupplierChangeStatus
{
    PendingApproval = 1,
    Approved = 2,
    Rejected = 3
}

public sealed record PurchaseOrderCreateRequest(Guid SourceDecisionId);

public sealed record PurchaseOrderLineEditRequest(
    Guid PurchaseOrderLineId,
    decimal OrderedQuantity,
    decimal UnitPrice,
    DateOnly? DeliveryDate,
    string? Notes);

public sealed record PurchaseOrderEditRequest(
    string? Notes,
    IReadOnlyList<PurchaseOrderLineEditRequest> Lines);

public sealed record PurchaseOrderActionRequest(string? Reason);

public sealed record PurchaseOrderEvidenceReferenceWriteRequest(
    string? ReferenceId,
    string? FileName,
    string? ContentType,
    string? Description,
    string? Source,
    string? ExternalReference);

public sealed record PurchaseOrderConfirmationLineRequest(
    Guid PurchaseOrderLineId,
    decimal ConfirmedQuantity,
    DateOnly? ExpectedDeliveryDate,
    decimal? ProposedQuantity,
    decimal? ProposedUnitPrice,
    DateOnly? ProposedDeliveryDate,
    string? ChangeReason);

public sealed record PurchaseOrderConfirmationRequest(
    PurchaseOrderConfirmationStatus Status,
    DateOnly ResponseDate,
    string? SupplierReference,
    string? SupplierContact,
    string? Reason,
    string? Notes,
    IReadOnlyList<PurchaseOrderConfirmationLineRequest> Lines,
    IReadOnlyList<PurchaseOrderEvidenceReferenceWriteRequest> Evidence);

public sealed record PurchaseOrderSupplierResponse(
    Guid Id,
    string Code,
    string Name);

public sealed record PurchaseOrderCurrencyResponse(
    Guid Id,
    string Code,
    string Name);

public sealed record PurchaseOrderPaymentTermResponse(
    Guid Id,
    string Code,
    string Name,
    int Version);

public sealed record PurchaseOrderSourceResponse(
    Guid PurchaseRequestId,
    Guid SupplierQuotationId,
    Guid SourceDecisionId,
    string PurchaseRequestReference,
    string? PurchaseRequestPurpose,
    string SupplierQuotationReference,
    PurchaseOrderSupplierResponse Supplier,
    PurchaseOrderCurrencyResponse Currency,
    PurchaseOrderPaymentTermResponse? PaymentTerm,
    string SourceDecisionRationale,
    DateTimeOffset SelectedAt);

public sealed record PurchaseOrderSourceLineResponse(
    Guid SupplierQuotationLineId,
    Guid PurchaseRequestLineId,
    string ProductSku,
    string ProductName,
    string UnitOfMeasureCode,
    decimal RequestedQuantity,
    decimal SelectedQuantity,
    decimal UnitPrice,
    decimal? DiscountAmount,
    decimal? DiscountPercentage,
    string? TaxCode,
    string? TaxName,
    decimal? TaxRatePercentage,
    decimal? TaxAmount,
    DateOnly RequestedNeedByDate,
    DateOnly? OfferedDeliveryDate);

public sealed record PurchaseOrderApprovalRecord(
    string PolicyId,
    int PolicyVersion,
    int StageIndex,
    string StageKey,
    int RequiredApprovals,
    int RecordedApprovals,
    bool AllowDelegation,
    bool AllowRequesterCancellationWhilePending,
    bool IsReapproval);

public sealed record PurchaseOrderLineResponse(
    Guid Id,
    Guid SourceQuotationLineId,
    Guid PurchaseRequestLineId,
    string ProductSku,
    string ProductName,
    string UnitOfMeasureCode,
    decimal OrderedQuantity,
    decimal ConfirmedQuantity,
    decimal RemainingQuantity,
    decimal UnitPrice,
    decimal? DiscountAmount,
    decimal? DiscountPercentage,
    string? TaxCode,
    string? TaxName,
    decimal? TaxRatePercentage,
    decimal? TaxAmount,
    DateOnly RequestedNeedByDate,
    DateOnly? DeliveryDate,
    string? Notes,
    string Version);

public sealed record PurchaseOrderEvidenceReferenceResponse(
    Guid Id,
    string ReferenceId,
    string? FileName,
    string? ContentType,
    string? Description,
    string Source,
    string? ExternalReference,
    Guid RecordedByActorId,
    DateTimeOffset RecordedAt,
    string? Version = null);

public sealed record PurchaseOrderSupplierChangeResponse(
    Guid Id,
    Guid ConfirmationId,
    Guid PurchaseOrderLineId,
    decimal PreviousOrderedQuantity,
    decimal? ProposedQuantity,
    decimal PreviousUnitPrice,
    decimal? ProposedUnitPrice,
    DateOnly? PreviousDeliveryDate,
    DateOnly? ProposedDeliveryDate,
    PurchaseOrderSupplierChangeStatus Status,
    string Reason,
    Guid ActorId,
    DateTimeOffset ProposedAt,
    DateTimeOffset? DecidedAt,
    string? DecisionReason,
    string? Version = null);

public sealed record PurchaseOrderConfirmationLineResponse(
    Guid Id,
    Guid PurchaseOrderLineId,
    decimal OrderedQuantityAtResponse,
    decimal ConfirmedQuantity,
    decimal RemainingQuantity,
    DateOnly? ExpectedDeliveryDate,
    decimal? ProposedQuantity,
    decimal? ProposedUnitPrice,
    DateOnly? ProposedDeliveryDate,
    string? ChangeReason,
    string? Version = null);

public sealed record PurchaseOrderConfirmationResponse(
    Guid Id,
    Guid PurchaseOrderId,
    string Status,
    DateOnly ResponseDate,
    string? SupplierReference,
    string? SupplierContact,
    string? Reason,
    string? Notes,
    Guid RecordedByActorId,
    DateTimeOffset RecordedAt,
    string PurchaseOrderVersion,
    IReadOnlyList<PurchaseOrderConfirmationLineResponse> Lines,
    IReadOnlyList<PurchaseOrderEvidenceReferenceResponse> Evidence,
    IReadOnlyList<PurchaseOrderSupplierChangeResponse> Changes,
    string Version);

public sealed record PurchaseOrderListItemResponse(
    Guid Id,
    Guid TenantId,
    Guid CompanyId,
    Guid? BranchId,
    string Status,
    string SupplierCode,
    string SupplierName,
    string SupplierQuotationReference,
    string CurrencyCode,
    decimal Total,
    int LineCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    string Version);

public sealed record PurchaseOrderResponse(
    Guid Id,
    Guid TenantId,
    Guid CompanyId,
    Guid? BranchId,
    Guid CreatedByActorId,
    string Status,
    PurchaseOrderSourceResponse Source,
    string? Notes,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? SubmittedAt,
    DateTimeOffset? ApprovedAt,
    DateTimeOffset? IssuedAt,
    DateTimeOffset? CancelledAt,
    Guid? LatestConfirmationId,
    string? LatestConfirmationStatus,
    PurchaseOrderApprovalRecord? Approval,
    IReadOnlyList<PurchaseOrderLineResponse> Lines,
    IReadOnlyList<PurchaseOrderSupplierChangeResponse> PendingChanges,
    string Version,
    bool CanEdit,
    bool CanSubmit,
    bool CanApprove,
    bool CanReject,
    bool CanReturnForChange,
    bool CanIssue,
    bool CanCancel,
    bool CanCaptureConfirmation,
    bool CanApproveSupplierChange,
    bool CanRejectSupplierChange);

public sealed record PurchaseOrderHistoryResponse(
    Guid EvidenceId,
    Guid PurchaseOrderId,
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

public sealed record PurchaseOrderAuditResponse(
    Guid EvidenceId,
    Guid PurchaseOrderId,
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
