#pragma warning disable CS1591

namespace MiniErp.Contracts.Modules.MasterData;

/// <summary>
/// The bounded provenance choices for the internal Exchange Rate master.
/// External provider feeds are deliberately not represented by this contract.
/// </summary>
public enum ExchangeRateProvenance
{
    Manual = 1,
    Configured = 2
}

public sealed record ExchangeRateWriteRequest(
    Guid SourceCurrencyId,
    Guid TargetCurrencyId,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    decimal Rate,
    int RateScale,
    ExchangeRateProvenance Provenance,
    string? SourceNotes);

public sealed record ExchangeRateVersionResponse(
    Guid Id,
    int VersionNumber,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    decimal Rate,
    int RateScale,
    ExchangeRateProvenance Provenance,
    string? SourceNotes,
    string SourceCurrencyCode,
    string TargetCurrencyCode);

public sealed record ExchangeRateResponse(
    Guid Id,
    Guid TenantId,
    Guid SourceCurrencyId,
    Guid TargetCurrencyId,
    string SourceCurrencyCode,
    string TargetCurrencyCode,
    string LifecycleState,
    int CurrentVersionNumber,
    IReadOnlyList<ExchangeRateVersionResponse> Versions,
    byte[] Version);

public sealed record ExchangeRateReferenceResponse(
    Guid Id,
    Guid TenantId,
    Guid SourceCurrencyId,
    Guid TargetCurrencyId,
    string SourceCurrencyCode,
    string TargetCurrencyCode,
    string LifecycleState,
    int VersionNumber,
    Guid VersionId,
    DateOnly EffectiveOn,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    decimal Rate,
    int RateScale,
    ExchangeRateProvenance Provenance,
    string? SourceNotes,
    string ReferenceValue,
    byte[] Version);

#pragma warning restore CS1591
