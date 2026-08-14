#pragma warning disable CS1591

namespace MiniErp.Contracts.Modules.MasterData;

/// <summary>
/// Bounded provenance for internal B2B price configuration. External feeds,
/// promotions, campaigns, coupons, POS rules, and provider identities are not
/// represented by this Release 1 contract.
/// </summary>
public enum PriceListProvenance
{
    Manual = 1,
    Configured = 2
}

public sealed record PriceListWriteRequest(
    string Code,
    string EnglishName,
    string? ArabicName,
    Guid CurrencyId,
    Guid? CustomerId,
    OrganizationScopeKind? OrganizationScopeKind,
    Guid? OrganizationScopeId,
    int Priority);

public sealed record PriceListPriceWriteRequest(
    Guid ProductId,
    Guid UnitOfMeasureId,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    decimal Price,
    int PriceScale,
    PriceListProvenance Provenance,
    string? SourceReference);

public sealed record PriceListPriceVersionResponse(
    Guid Id,
    int VersionNumber,
    Guid ProductId,
    string ProductSku,
    Guid UnitOfMeasureId,
    string UnitOfMeasureCode,
    Guid CurrencyId,
    string CurrencyCode,
    Guid? CustomerId,
    OrganizationScopeKind? OrganizationScopeKind,
    Guid? OrganizationScopeId,
    int Priority,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    decimal Price,
    int PriceScale,
    PriceListProvenance Provenance,
    string? SourceReference,
    byte[] Version);

public sealed record PriceListResponse(
    Guid Id,
    Guid TenantId,
    string Code,
    string EnglishName,
    string? ArabicName,
    Guid CurrencyId,
    string CurrencyCode,
    Guid? CustomerId,
    OrganizationScopeKind? OrganizationScopeKind,
    Guid? OrganizationScopeId,
    int Priority,
    string LifecycleState,
    int CurrentVersionNumber,
    IReadOnlyList<PriceListPriceVersionResponse> Prices,
    byte[] Version);

/// <summary>
/// Immutable evidence returned by the deterministic pricing-reference seam.
/// Sales may later persist this evidence in its own document snapshot; this
/// Phase A operation does not create or edit Sales Orders.
/// </summary>
public sealed record PriceListReferenceResponse(
    Guid PriceListId,
    Guid TenantId,
    string PriceListCode,
    Guid PriceVersionId,
    int PriceVersionNumber,
    Guid ProductId,
    string ProductSku,
    Guid UnitOfMeasureId,
    string UnitOfMeasureCode,
    Guid CurrencyId,
    string CurrencyCode,
    Guid? CustomerId,
    OrganizationScopeKind? OrganizationScopeKind,
    Guid? OrganizationScopeId,
    int Priority,
    DateOnly EffectiveOn,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    decimal Price,
    int PriceScale,
    PriceListProvenance Provenance,
    string? SourceReference,
    string ReferenceValue,
    byte[] PriceVersion,
    byte[] MasterVersion);

#pragma warning restore CS1591
