#pragma warning disable CS1591

namespace MiniErp.Contracts.Modules.MasterData;

/// <summary>
/// The configured business direction in which a Tenant tax may be selected.
/// The value is an internal ERP applicability fact, not a statutory
/// classification.
/// </summary>
public enum TaxDirection
{
    Purchase = 1,
    Sales = 2,
    Both = 3
}

/// <summary>
/// Rounding is an explicit input to the deterministic engine contract. The
/// Tax master does not silently choose a Tenant's accounting or document
/// rounding policy.
/// </summary>
public enum TaxRoundingMode
{
    ToEven = 1,
    AwayFromZero = 2
}

public sealed record TaxRateVersionWriteRequest(
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    decimal RatePercentage);

public sealed record TaxWriteRequest(
    string? Code,
    string? CategoryCode,
    string? CategoryEnglishName,
    string? CategoryArabicName,
    string? EnglishName,
    string? ArabicName,
    TaxDirection Applicability,
    TaxRateVersionWriteRequest? RateVersion);

public sealed record TaxCalculationRequest(
    DateOnly EffectiveOn,
    TaxDirection TransactionDirection,
    decimal TaxableBase,
    string? CurrencyCode,
    int RoundingScale,
    TaxRoundingMode RoundingMode,
    string? SourceLineage);

public sealed record TaxRateVersionResponse(
    Guid Id,
    int VersionNumber,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    decimal RatePercentage);

public sealed record TaxResponse(
    Guid Id,
    Guid TenantId,
    string Code,
    string CategoryCode,
    string? CategoryEnglishName,
    string? CategoryArabicName,
    string? EnglishName,
    string? ArabicName,
    TaxDirection Applicability,
    string LifecycleState,
    int CurrentVersionNumber,
    IReadOnlyList<TaxRateVersionResponse> RateVersions,
    byte[] Version);

public sealed record TaxReferenceResponse(
    Guid Id,
    Guid TenantId,
    string Code,
    string CategoryCode,
    string? CategoryEnglishName,
    string? CategoryArabicName,
    string? EnglishName,
    string? ArabicName,
    TaxDirection Applicability,
    string LifecycleState,
    int VersionNumber,
    Guid VersionId,
    DateOnly EffectiveOn,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    decimal RatePercentage,
    string ReferenceValue,
    byte[] Version);

/// <summary>
/// A reproducible, side-effect-free engine result. A downstream owning
/// document must persist this evidence when it applies tax; the Tax master
/// does not create accounting entries or rewrite posted evidence.
/// </summary>
public sealed record TaxCalculationResponse(
    Guid TaxId,
    Guid TenantId,
    string Code,
    string CategoryCode,
    TaxDirection Applicability,
    TaxDirection TransactionDirection,
    Guid RateVersionId,
    int RateVersionNumber,
    DateOnly EffectiveOn,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    decimal RatePercentage,
    decimal TaxableBase,
    decimal TaxAmount,
    string CurrencyCode,
    int RoundingScale,
    TaxRoundingMode RoundingMode,
    string SourceLineage,
    string ReferenceValue);

#pragma warning restore CS1591
