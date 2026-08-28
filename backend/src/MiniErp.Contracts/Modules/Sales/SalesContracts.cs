#pragma warning disable CS1591

namespace MiniErp.Contracts.Modules.Sales;

public enum SalesQuotationStatus
{
    Draft = 1,
    PendingApproval = 2,
    Approved = 3,
    Sent = 4,
    Expired = 5,
    Converted = 6,
    Withdrawn = 7,
    Rejected = 8,
    ReturnedForChange = 9,
    Cancelled = 10
}

public enum SalesOrderStatus
{
    Draft = 1,
    PendingApproval = 2,
    Approved = 3,
    CreditHold = 4,
    Confirmed = 5,
    Rejected = 6,
    ReturnedForChange = 7,
    Cancelled = 8
}

public enum SalesCreditOutcome
{
    Eligible = 1,
    Warning = 2,
    Blocked = 3,
    Pending = 4,
    Unknown = 5,
    Overridden = 6
}

public enum SalesHistoryAction
{
    Created = 1,
    Edited = 2,
    RevisionCreated = 3,
    Submitted = 4,
    Approved = 5,
    Rejected = 6,
    ReturnedForChange = 7,
    Sent = 8,
    Withdrawn = 9,
    Expired = 10,
    Converted = 11,
    Confirmed = 12,
    Cancelled = 13,
    CreditEvaluated = 14,
    CreditOverridden = 15
}

public sealed record SalesQuotationLineRequest(
    Guid ProductId,
    Guid UnitOfMeasureId,
    decimal Quantity,
    decimal? UnitPriceOverride = null,
    decimal DiscountPercent = 0m,
    string? Notes = null,
    Guid? TaxId = null);

public sealed record SalesExchangeRateReferenceRequest(Guid ExchangeRateId);

public sealed record SalesExchangeRateEvidence(
    Guid ExchangeRateId,
    Guid ExchangeRateVersionId,
    int VersionNumber,
    string SourceCurrencyCode,
    string TargetCurrencyCode,
    decimal Rate,
    int RateScale,
    string Provenance,
    string? SourceNotes,
    DateOnly EffectiveOn,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    string ReferenceValue);

public sealed record SalesQuotationCreateRequest(
    Guid CompanyId,
    Guid? BranchId,
    Guid CustomerId,
    DateOnly QuotationDate,
    DateOnly ValidUntil,
    Guid CurrencyId,
    Guid? PriceListId,
    string? CustomerContactId,
    string? Notes,
    string? CustomerReference,
    IReadOnlyList<SalesQuotationLineRequest> Lines,
    Guid? ExchangeRateId = null);

public sealed record SalesQuotationEditRequest(
    Guid CompanyId,
    Guid? BranchId,
    DateOnly ValidUntil,
    Guid CurrencyId,
    Guid? PriceListId,
    string? CustomerContactId,
    string? Notes,
    string? CustomerReference,
    IReadOnlyList<SalesQuotationLineRequest> Lines,
    Guid? ExchangeRateId = null);

public sealed record SalesOrderEditRequest(
    Guid CurrencyId,
    Guid? PriceListId,
    IReadOnlyList<SalesQuotationLineRequest> Lines,
    Guid? ExchangeRateId = null);

public sealed record SalesActionRequest(string? Reason);

public sealed record SalesCreditOverrideRequest(
    string Reason,
    DateTimeOffset ExpiresAt,
    string? Scope,
    string? SourceReference);

public sealed record SalesApprovalDecisionResponse(
    string StageKey,
    Guid ActorId,
    Guid? DelegatedFromActorId,
    DateTimeOffset DecidedAt,
    string PolicyId,
    int PolicyVersion,
    int RevisionNumber,
    byte[] DocumentVersion);

public sealed record SalesApprovalStateResponse(
    string PolicyId,
    int PolicyVersion,
    int CurrentStageIndex,
    string? CurrentStageKey,
    int CurrentStageRequiredApprovals,
    int CurrentStageApprovalCount,
    IReadOnlyList<Guid> CurrentStageApproverIds,
    IReadOnlyList<SalesApprovalDecisionResponse> Decisions);

public sealed record SalesQuotationSummaryResponse(
    Guid Id,
    string Number,
    Guid CompanyId,
    Guid? BranchId,
    Guid CustomerId,
    string CustomerCode,
    string CustomerName,
    Guid CreatedByActorId,
    DateOnly QuotationDate,
    DateOnly ValidUntil,
    Guid CurrencyId,
    string CurrencyCode,
    decimal Subtotal,
    decimal DiscountAmount,
    decimal TaxAmount,
    decimal Total,
    SalesQuotationStatus Status,
    int RevisionNumber,
    byte[] Version,
    DateTimeOffset UpdatedAt);

public sealed record SalesQuotationLineResponse(
    Guid Id,
    Guid ProductId,
    string ProductSku,
    string ProductName,
    Guid UnitOfMeasureId,
    string UnitOfMeasureCode,
    decimal Quantity,
    decimal UnitPrice,
    decimal ResolvedUnitPrice,
    decimal DiscountPercent,
    decimal DiscountAmount,
    decimal TaxAmount,
    decimal LineTotal,
    Guid? PriceListId,
    int? PriceVersionNumber,
    DateOnly? PriceEffectiveFrom,
    string PriceProvenance,
    string? PriceSourceReference,
    bool ManualPriceApplied,
    string? CommercialAuthorityPolicyId,
    Guid? CommercialAuthorityActorId,
    string? CommercialAuthorityEvidence,
    string? Notes,
    Guid? TaxId = null,
    string? TaxCode = null,
    Guid? TaxRateVersionId = null,
    int? TaxRateVersionNumber = null,
    DateOnly? TaxEffectiveFrom = null,
    DateOnly? TaxEffectiveTo = null,
    decimal? TaxRatePercentage = null,
    decimal? TaxableBase = null,
    string? TaxReferenceValue = null);

public sealed record SalesQuotationResponse(
    Guid Id,
    string Number,
    Guid TenantId,
    Guid CompanyId,
    Guid? BranchId,
    Guid CustomerId,
    string CustomerCode,
    string CustomerName,
    Guid CreatedByActorId,
    DateOnly QuotationDate,
    DateOnly ValidUntil,
    Guid CurrencyId,
    string CurrencyCode,
    string? CustomerContactId,
    string? Notes,
    string? CustomerReference,
    decimal Subtotal,
    decimal DiscountAmount,
    decimal TaxAmount,
    decimal Total,
    SalesQuotationStatus Status,
    int RevisionNumber,
    IReadOnlyList<SalesQuotationLineResponse> Lines,
    byte[] Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    SalesExchangeRateEvidence? ExchangeRateEvidence = null,
    SalesApprovalStateResponse? ApprovalState = null);

public sealed record SalesQuotationRevisionResponse(
    Guid Id,
    Guid QuotationId,
    int RevisionNumber,
    SalesQuotationStatus Status,
    string SnapshotHash,
    Guid ActorId,
    DateTimeOffset OccurredAt,
    string? Reason,
    SalesQuotationResponse Snapshot);

public sealed record SalesOrderSummaryResponse(
    Guid Id,
    string Number,
    Guid CompanyId,
    Guid? BranchId,
    Guid CustomerId,
    string CustomerCode,
    string CustomerName,
    Guid CreatedByActorId,
    Guid SourceQuotationId,
    string SourceQuotationNumber,
    int SourceQuotationRevision,
    Guid CurrencyId,
    string CurrencyCode,
    decimal Total,
    SalesOrderStatus Status,
    SalesCreditOutcome CreditOutcome,
    byte[] Version,
    DateTimeOffset UpdatedAt,
    int RevisionNumber = 1);

public sealed record SalesOrderResponse(
    Guid Id,
    string Number,
    Guid TenantId,
    Guid CompanyId,
    Guid? BranchId,
    Guid CustomerId,
    string CustomerCode,
    string CustomerName,
    Guid CreatedByActorId,
    Guid SourceQuotationId,
    string SourceQuotationNumber,
    int SourceQuotationRevision,
    Guid CurrencyId,
    string CurrencyCode,
    decimal Subtotal,
    decimal DiscountAmount,
    decimal TaxAmount,
    decimal Total,
    SalesOrderStatus Status,
    SalesCreditOutcome CreditOutcome,
    string? CreditReason,
    DateTimeOffset? CreditEvaluatedAt,
    DateTimeOffset? CreditOverrideExpiresAt,
    IReadOnlyList<SalesQuotationLineResponse> Lines,
    byte[] Version,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    SalesExchangeRateEvidence? ExchangeRateEvidence = null,
    int RevisionNumber = 1,
    SalesApprovalStateResponse? ApprovalState = null);

public sealed record SalesHistoryResponse(
    Guid Id,
    string DocumentType,
    Guid DocumentId,
    string Action,
    string? FromStatus,
    string? ToStatus,
    Guid ActorId,
    DateTimeOffset OccurredAt,
    string? Reason,
    string? PolicyId,
    int? PolicyVersion,
    string? CreditOutcome,
    string? SnapshotHash,
    string? SnapshotJson = null);

public sealed record SalesAuditResponse(
    Guid Id,
    string OperationId,
    string DocumentType,
    Guid DocumentId,
    Guid ActorId,
    DateTimeOffset OccurredAt,
    string Decision,
    string? Reason,
    string? BeforeSummary,
    string? AfterSummary,
    string? IdempotencyKey,
    string CorrelationId);

public sealed record SalesCreditResponse(
    Guid DocumentId,
    Guid CustomerId,
    Guid CompanyId,
    string? CurrencyCode,
    decimal? OpenReceivables,
    decimal? OverdueReceivables,
    decimal? NetReceivableExposure,
    decimal? ProposedExposure,
    decimal? CreditLimit,
    SalesCreditOutcome Outcome,
    string? Reason,
    DateOnly AsOfDate,
    DateTimeOffset EvaluatedAt,
    DateTimeOffset? OverrideExpiresAt,
    string? TransactionCurrencyCode = null,
    decimal? TransactionAmount = null,
    decimal? ConvertedOrderCommitment = null,
    SalesExchangeRateEvidence? ExchangeRateEvidence = null,
    int? OrderRevisionNumber = null);

#pragma warning restore CS1591
