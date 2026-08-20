#pragma warning disable CS1591

namespace MiniErp.Contracts.Modules.Procurement;

public enum PurchaseInvoiceMatchResult
{
    NotMatchReady = 1,
    ExactMatch = 2,
    WithinTolerance = 3,
    ExceptionHold = 4,
    ResolvedException = 5
}

public enum PurchaseInvoiceMatchLifecycle
{
    Current = 1,
    Superseded = 2
}

public sealed record PurchaseInvoiceExchangeRateReferenceRequest(
    Guid ExchangeRateId,
    DateOnly? EffectiveOn = null);

public sealed record PurchaseInvoiceMatchEvaluateRequest(
    PurchaseInvoiceExchangeRateReferenceRequest? ExchangeRateReference = null);

public sealed record PurchaseInvoiceMatchResolveRequest(string? Reason);

public sealed record PurchaseInvoiceMatchVarianceResponse(
    string Classification,
    Guid? PurchaseOrderLineId,
    Guid? GoodsReceiptLineId,
    decimal? ExpectedValue,
    decimal? ActualValue,
    decimal? Variance,
    decimal AllowedTolerance,
    string? CurrencyCode,
    string? Details);

public sealed record PurchaseInvoiceMatchPolicyResponse(
    string PolicyId,
    int Version,
    decimal QuantityAbsoluteTolerance,
    decimal QuantityPercentageTolerance,
    decimal PriceAbsoluteTolerance,
    decimal PricePercentageTolerance,
    decimal AmountAbsoluteTolerance,
    decimal AmountPercentageTolerance,
    decimal TaxAbsoluteTolerance,
    decimal TaxPercentageTolerance,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo);

public sealed record PurchaseInvoiceMatchResolutionPolicyResponse(
    string PolicyId,
    int Version,
    bool AllowResolution,
    bool RequireDifferentActor,
    bool RequireReason,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo);

public sealed record PurchaseInvoiceMatchExchangeRateResponse(
    Guid ExchangeRateId,
    Guid ExchangeRateVersionId,
    int VersionNumber,
    string SourceCurrencyCode,
    string TargetCurrencyCode,
    decimal Rate,
    int Scale,
    string? Provenance,
    string? Source,
    DateOnly EffectiveOn,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo);

public sealed record PurchaseInvoiceMatchResponse(
    Guid Id,
    Guid TenantId,
    Guid CompanyId,
    Guid? BranchId,
    Guid PurchaseInvoiceHandoffId,
    Guid PurchaseOrderId,
    PurchaseInvoiceMatchLifecycle Lifecycle,
    PurchaseInvoiceMatchResult Result,
    DateTimeOffset EvaluatedAt,
    Guid EvaluatedByActorId,
    Guid? ResolvedByActorId,
    DateTimeOffset? ResolvedAt,
    string? ResolutionReason,
    string SourceFingerprint,
    string PurchaseOrderVersion,
    string HandoffVersion,
    string? DeclaredEvidenceId,
    int? DeclaredEvidenceVersion,
    PurchaseInvoiceMatchPolicyResponse Policy,
    PurchaseInvoiceMatchResolutionPolicyResponse? ResolutionPolicy,
    PurchaseInvoiceMatchExchangeRateResponse? AppliedExchangeRate,
    IReadOnlyList<PurchaseInvoiceMatchVarianceResponse> Variances,
    string? SourceSnapshot,
    string Version);

public sealed record PurchaseInvoiceMatchListItemResponse(
    Guid Id,
    Guid PurchaseInvoiceHandoffId,
    Guid PurchaseOrderId,
    PurchaseInvoiceMatchLifecycle Lifecycle,
    PurchaseInvoiceMatchResult Result,
    DateTimeOffset EvaluatedAt,
    Guid? ResolvedByActorId,
    int VarianceCount,
    string Version);

public sealed record PurchaseInvoiceMatchHistoryResponse(
    Guid Id,
    Guid MatchEvaluationId,
    Guid PurchaseInvoiceHandoffId,
    PurchaseInvoiceMatchResult Result,
    string Action,
    Guid ActorId,
    string? Reason,
    DateTimeOffset OccurredAt,
    string CorrelationId);

public sealed record PurchaseInvoiceMatchAuditResponse(
    Guid Id,
    Guid MatchEvaluationId,
    Guid PurchaseInvoiceHandoffId,
    string OperationId,
    Guid TenantId,
    Guid ActorId,
    string Decision,
    string? Reason,
    DateTimeOffset OccurredAt,
    string? IdempotencyKey,
    string? RequestFingerprint);

#pragma warning restore CS1591
