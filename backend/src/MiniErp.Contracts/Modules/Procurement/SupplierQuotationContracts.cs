#pragma warning disable CS1591

namespace MiniErp.Contracts.Modules.Procurement;

public enum SupplierQuotationStatus
{
    Draft = 1,
    Submitted = 2,
    Withdrawn = 3,
    Disqualified = 4,
    Superseded = 5
}

public enum SupplierQuotationHistoryAction
{
    Created = 1,
    Edited = 2,
    Submitted = 3,
    Withdrawn = 4,
    Disqualified = 5,
    Superseded = 6
}

public sealed record SupplierQuotationLineWriteRequest(
    Guid PurchaseRequestLineId,
    decimal QuotedQuantity,
    decimal UnitPrice,
    decimal? DiscountAmount = null,
    decimal? DiscountPercentage = null,
    Guid? TaxId = null,
    string? TaxReference = null,
    decimal? TaxRatePercentage = null,
    decimal? TaxAmount = null,
    DateOnly? OfferedDeliveryDate = null,
    string? OfferedDeliveryLeadTime = null,
    string? Notes = null);

public sealed record SupplierQuotationEvidenceReferenceWriteRequest(
    string? ReferenceId,
    string? FileName,
    string? ContentType,
    string? Description,
    string? Source,
    string? ExternalReference);

public sealed record SupplierQuotationWriteRequest(
    Guid SupplierId,
    string? SupplierQuotationReference,
    DateOnly OfferDate,
    DateOnly? ValidUntil,
    Guid CurrencyId,
    Guid? PaymentTermId,
    string? DeliveryTerms,
    DateOnly? OfferedDeliveryDate,
    string? OfferedDeliveryLeadTime,
    string? Notes,
    IReadOnlyList<SupplierQuotationLineWriteRequest>? Lines,
    IReadOnlyList<SupplierQuotationEvidenceReferenceWriteRequest>? Evidence);

public sealed record SupplierQuotationActionRequest(string? Reason = null);

public sealed record SupplierSourceDecisionWriteRequest(
    Guid SelectedQuotationId,
    string? Rationale);

public sealed record SupplierQuotationSupplierResponse(
    Guid Id,
    string Code,
    string Name);

public sealed record SupplierQuotationCurrencyResponse(
    Guid Id,
    string Code,
    string Name);

public sealed record SupplierQuotationPaymentTermResponse(
    Guid Id,
    string Code,
    string Name,
    int Version);

public sealed record SupplierQuotationLineResponse(
    Guid Id,
    Guid PurchaseRequestLineId,
    Guid ProductId,
    string ProductSku,
    string ProductName,
    Guid UnitOfMeasureId,
    string UnitOfMeasureCode,
    decimal RequestedQuantity,
    decimal QuotedQuantity,
    decimal UnitPrice,
    decimal? DiscountAmount,
    decimal? DiscountPercentage,
    Guid? TaxId,
    string? TaxCode,
    string? TaxName,
    decimal? TaxRatePercentage,
    decimal? TaxAmount,
    string? TaxReference,
    DateOnly RequestedNeedByDate,
    DateOnly? OfferedDeliveryDate,
    string? OfferedDeliveryLeadTime,
    string? Notes,
    byte[] Version);

public sealed record SupplierQuotationEvidenceReferenceResponse(
    Guid Id,
    string ReferenceId,
    string? FileName,
    string? ContentType,
    string? Description,
    string Source,
    string? ExternalReference,
    Guid RecordedByActorId,
    DateTimeOffset RecordedAt);

public sealed record SupplierQuotationResponse(
    Guid Id,
    Guid TenantId,
    Guid PurchaseRequestId,
    Guid CompanyId,
    Guid? BranchId,
    Guid CreatedByActorId,
    SupplierQuotationSupplierResponse Supplier,
    string Status,
    string SupplierQuotationReference,
    DateOnly OfferDate,
    DateOnly? ValidUntil,
    SupplierQuotationCurrencyResponse Currency,
    SupplierQuotationPaymentTermResponse? PaymentTerm,
    string? DeliveryTerms,
    DateOnly? OfferedDeliveryDate,
    string? OfferedDeliveryLeadTime,
    string? Notes,
    IReadOnlyList<SupplierQuotationLineResponse> Lines,
    IReadOnlyList<SupplierQuotationEvidenceReferenceResponse> Evidence,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? SubmittedAt,
    bool IsSelected,
    byte[] Version,
    bool CanEdit,
    bool CanSubmit,
    bool CanWithdraw,
    bool CanDisqualify);

public sealed record SupplierQuotationListItemResponse(
    Guid Id,
    Guid PurchaseRequestId,
    SupplierQuotationSupplierResponse Supplier,
    string Status,
    string SupplierQuotationReference,
    DateOnly OfferDate,
    DateOnly? ValidUntil,
    SupplierQuotationCurrencyResponse Currency,
    decimal CommercialTotal,
    int CoveredLineCount,
    int RequestedLineCount,
    bool HasEvidence,
    byte[] Version);

public sealed record SupplierQuotationHistoryResponse(
    Guid EvidenceId,
    Guid SupplierQuotationId,
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

public sealed record SupplierQuotationAuditResponse(
    Guid EvidenceId,
    Guid SupplierQuotationId,
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

public sealed record SupplierQuotationComparisonLineResponse(
    Guid PurchaseRequestLineId,
    string ProductSku,
    string ProductName,
    decimal RequestedQuantity,
    decimal? QuotedQuantity,
    decimal? UnitPrice,
    decimal? DiscountAmount,
    decimal? DiscountPercentage,
    decimal? TaxRatePercentage,
    decimal? TaxAmount,
    DateOnly RequestedNeedByDate,
    DateOnly? OfferedDeliveryDate,
    bool IsCovered,
    string? QualificationIssue);

public sealed record SupplierQuotationComparisonItemResponse(
    Guid SupplierQuotationId,
    SupplierQuotationSupplierResponse Supplier,
    string Status,
    string SupplierQuotationReference,
    DateOnly OfferDate,
    DateOnly? ValidUntil,
    SupplierQuotationCurrencyResponse Currency,
    decimal CommercialTotal,
    int CoveredLineCount,
    int RequestedLineCount,
    bool HasEvidence,
    bool IsDirectlyComparableToAll,
    string? PaymentTermCode,
    string? DeliveryTerms,
    DateOnly? OfferedDeliveryDate,
    string? OfferedDeliveryLeadTime,
    IReadOnlyList<SupplierQuotationComparisonLineResponse> Lines,
    IReadOnlyList<string> QualificationIssues);

public sealed record SupplierQuotationCurrencyComparisonGroupResponse(
    Guid CurrencyId,
    string CurrencyCode,
    IReadOnlyList<Guid> SupplierQuotationIds,
    bool DirectlyComparableWithinGroup);

public sealed record SupplierSourceDecisionResponse(
    Guid Id,
    Guid TenantId,
    Guid PurchaseRequestId,
    Guid SelectedQuotationId,
    SupplierQuotationSupplierResponse Supplier,
    string SupplierQuotationReference,
    Guid ActorId,
    DateTimeOffset SelectedAt,
    string Rationale,
    string? PolicyId,
    int? PolicyVersion,
    string? StageKey,
    string ComparisonSnapshotReference,
    byte[] Version);

public sealed record SupplierQuotationComparisonResponse(
    Guid PurchaseRequestId,
    bool HasMixedCurrencies,
    bool DirectCurrencyComparisonAvailable,
    string ComparisonBasis,
    IReadOnlyList<SupplierQuotationCurrencyComparisonGroupResponse> CurrencyGroups,
    IReadOnlyList<SupplierQuotationComparisonItemResponse> Quotations,
    SupplierSourceDecisionResponse? CurrentSourceDecision);

#pragma warning restore CS1591
