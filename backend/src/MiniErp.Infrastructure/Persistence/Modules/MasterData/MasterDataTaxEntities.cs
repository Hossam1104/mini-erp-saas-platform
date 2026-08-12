#pragma warning disable CS1591

using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.Contracts.Modules.MasterData;

namespace MiniErp.Infrastructure.Persistence.Modules.MasterData;

internal sealed class MasterDataTaxEntity : ITenantOwned
{
    private MasterDataTaxEntity()
    {
        Code = string.Empty;
        CodeKey = string.Empty;
        CategoryCode = string.Empty;
        CategoryCodeKey = string.Empty;
        CategoryEnglishName = string.Empty;
        EnglishName = string.Empty;
        NameKey = string.Empty;
    }

    internal MasterDataTaxEntity(
        Guid id,
        TenantId tenantId,
        string code,
        string categoryCode,
        LocalizedName categoryName,
        LocalizedName name,
        TaxDirection applicability)
    {
        Id = id;
        TenantId = tenantId;
        Code = code;
        CodeKey = code.ToUpperInvariant();
        CategoryCode = categoryCode;
        CategoryCodeKey = categoryCode.ToUpperInvariant();
        CategoryEnglishName = categoryName.English ?? string.Empty;
        CategoryArabicName = categoryName.Arabic;
        EnglishName = name.English ?? string.Empty;
        ArabicName = name.Arabic;
        NameKey = (name.English ?? name.Arabic ?? string.Empty).ToUpperInvariant();
        Applicability = applicability;
        LifecycleState = MasterDataLifecycleState.Active;
        CurrentVersionNumber = 1;
    }

    internal Guid Id { get; private set; }
    public TenantId TenantId { get; private set; }
    internal string Code { get; private set; }
    internal string CodeKey { get; private set; }
    internal string CategoryCode { get; private set; }
    internal string CategoryCodeKey { get; private set; }
    internal string CategoryEnglishName { get; private set; }
    internal string? CategoryArabicName { get; private set; }
    internal string EnglishName { get; private set; }
    internal string? ArabicName { get; private set; }
    internal string NameKey { get; private set; }
    internal TaxDirection Applicability { get; private set; }
    internal MasterDataLifecycleState LifecycleState { get; private set; }
    internal int CurrentVersionNumber { get; private set; }
    internal byte[] Version { get; private set; } = Guid.NewGuid().ToByteArray();
    internal ICollection<MasterDataTaxRateVersionEntity> RateVersions { get; private set; } = new List<MasterDataTaxRateVersionEntity>();

    internal LocalizedName CategoryName => new(
        string.IsNullOrWhiteSpace(CategoryEnglishName) ? null : CategoryEnglishName,
        CategoryArabicName);

    internal LocalizedName Name => new(
        string.IsNullOrWhiteSpace(EnglishName) ? null : EnglishName,
        ArabicName);

    internal void EditIdentity(
        string code,
        string categoryCode,
        LocalizedName categoryName,
        LocalizedName name,
        TaxDirection applicability,
        int currentVersionNumber)
    {
        Code = code;
        CodeKey = code.ToUpperInvariant();
        CategoryCode = categoryCode;
        CategoryCodeKey = categoryCode.ToUpperInvariant();
        CategoryEnglishName = categoryName.English ?? string.Empty;
        CategoryArabicName = categoryName.Arabic;
        EnglishName = name.English ?? string.Empty;
        ArabicName = name.Arabic;
        NameKey = (name.English ?? name.Arabic ?? string.Empty).ToUpperInvariant();
        Applicability = applicability;
        CurrentVersionNumber = currentVersionNumber;
        TouchVersion();
    }

    internal void SetLifecycle(MasterDataLifecycleState lifecycleState)
    {
        LifecycleState = lifecycleState;
        TouchVersion();
    }

    internal void TouchVersion() => Version = Guid.NewGuid().ToByteArray();
}

internal sealed class MasterDataTaxRateVersionEntity : ITenantOwned
{
    private MasterDataTaxRateVersionEntity()
    {
    }

    internal MasterDataTaxRateVersionEntity(
        Guid id,
        TenantId tenantId,
        Guid taxId,
        int versionNumber,
        DateOnly effectiveFrom,
        DateOnly? effectiveTo,
        decimal ratePercentage)
    {
        Id = id;
        TenantId = tenantId;
        TaxId = taxId;
        VersionNumber = versionNumber;
        EffectiveFrom = effectiveFrom;
        EffectiveTo = effectiveTo;
        RatePercentage = ratePercentage;
    }

    internal Guid Id { get; private set; }
    public TenantId TenantId { get; private set; }
    internal Guid TaxId { get; private set; }
    internal int VersionNumber { get; private set; }
    internal DateOnly EffectiveFrom { get; private set; }
    internal DateOnly? EffectiveTo { get; private set; }
    internal decimal RatePercentage { get; private set; }
    internal byte[] Version { get; private set; } = Guid.NewGuid().ToByteArray();

    internal void CloseAt(DateOnly effectiveTo)
    {
        EffectiveTo = effectiveTo;
        TouchVersion();
    }

    internal void TouchVersion() => Version = Guid.NewGuid().ToByteArray();
}

#pragma warning restore CS1591
