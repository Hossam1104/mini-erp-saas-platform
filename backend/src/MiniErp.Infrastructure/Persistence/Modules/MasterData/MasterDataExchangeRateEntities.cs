#pragma warning disable CS1591

using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.Contracts.Modules.MasterData;

namespace MiniErp.Infrastructure.Persistence.Modules.MasterData;

internal sealed class MasterDataExchangeRateEntity : ITenantOwned
{
    private MasterDataExchangeRateEntity() { }

    internal MasterDataExchangeRateEntity(Guid id, TenantId tenantId, Guid sourceCurrencyId, Guid targetCurrencyId)
    {
        Id = id;
        TenantId = tenantId;
        SourceCurrencyId = sourceCurrencyId;
        TargetCurrencyId = targetCurrencyId;
        LifecycleState = MasterDataLifecycleState.Active;
        CurrentVersionNumber = 1;
    }

    internal Guid Id { get; private set; }
    public TenantId TenantId { get; private set; }
    internal Guid SourceCurrencyId { get; private set; }
    internal Guid TargetCurrencyId { get; private set; }
    internal MasterDataLifecycleState LifecycleState { get; private set; }
    internal int CurrentVersionNumber { get; private set; }
    internal byte[] Version { get; private set; } = Guid.NewGuid().ToByteArray();
    internal ICollection<MasterDataExchangeRateVersionEntity> Versions { get; private set; } = new List<MasterDataExchangeRateVersionEntity>();

    internal void AppendVersion(int versionNumber)
    {
        CurrentVersionNumber = versionNumber;
        TouchVersion();
    }

    internal void SetLifecycle(MasterDataLifecycleState lifecycleState)
    {
        LifecycleState = lifecycleState;
        TouchVersion();
    }

    internal void TouchVersion() => Version = Guid.NewGuid().ToByteArray();
}

internal sealed class MasterDataExchangeRateVersionEntity : ITenantOwned
{
    private MasterDataExchangeRateVersionEntity()
    {
        SourceCurrencyCode = string.Empty;
        TargetCurrencyCode = string.Empty;
    }

    internal MasterDataExchangeRateVersionEntity(
        Guid id,
        TenantId tenantId,
        Guid exchangeRateId,
        int versionNumber,
        DateOnly effectiveFrom,
        DateOnly? effectiveTo,
        decimal rate,
        int rateScale,
        ExchangeRateProvenance provenance,
        string? sourceNotes,
        string sourceCurrencyCode,
        string targetCurrencyCode)
    {
        Id = id;
        TenantId = tenantId;
        ExchangeRateId = exchangeRateId;
        VersionNumber = versionNumber;
        EffectiveFrom = effectiveFrom;
        EffectiveTo = effectiveTo;
        Rate = rate;
        RateScale = rateScale;
        Provenance = provenance;
        SourceNotes = sourceNotes;
        SourceCurrencyCode = sourceCurrencyCode;
        TargetCurrencyCode = targetCurrencyCode;
    }

    internal Guid Id { get; private set; }
    public TenantId TenantId { get; private set; }
    internal Guid ExchangeRateId { get; private set; }
    internal int VersionNumber { get; private set; }
    internal DateOnly EffectiveFrom { get; private set; }
    internal DateOnly? EffectiveTo { get; private set; }
    internal decimal Rate { get; private set; }
    internal int RateScale { get; private set; }
    internal ExchangeRateProvenance Provenance { get; private set; }
    internal string? SourceNotes { get; private set; }
    internal string SourceCurrencyCode { get; private set; }
    internal string TargetCurrencyCode { get; private set; }
    internal byte[] Version { get; private set; } = Guid.NewGuid().ToByteArray();

    internal void CloseAt(DateOnly effectiveTo)
    {
        EffectiveTo = effectiveTo;
        TouchVersion();
    }

    internal void TouchVersion() => Version = Guid.NewGuid().ToByteArray();
}

#pragma warning restore CS1591
