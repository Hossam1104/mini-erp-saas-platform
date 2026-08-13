#pragma warning disable CS1591

using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.Contracts.Modules.MasterData;

namespace MiniErp.Infrastructure.Persistence.Modules.MasterData;

internal sealed class MasterDataPriceListEntity : ITenantOwned
{
    private MasterDataPriceListEntity()
    {
        Code = string.Empty;
        CodeKey = string.Empty;
        EnglishName = string.Empty;
        CurrencyCode = string.Empty;
    }

    internal MasterDataPriceListEntity(
        Guid id,
        TenantId tenantId,
        string code,
        LocalizedName name,
        Guid currencyId,
        string currencyCode,
        Guid? customerId,
        OrganizationScopeKind? organizationScopeKind,
        Guid? organizationScopeId,
        int priority)
    {
        Id = id;
        TenantId = tenantId;
        Code = code;
        CodeKey = code.ToUpperInvariant();
        EnglishName = name.English ?? string.Empty;
        ArabicName = name.Arabic;
        CurrencyId = currencyId;
        CurrencyCode = currencyCode;
        CustomerId = customerId;
        OrganizationScopeKind = organizationScopeKind;
        OrganizationScopeId = organizationScopeId;
        Priority = priority;
        LifecycleState = MasterDataLifecycleState.Active;
    }

    internal Guid Id { get; private set; }
    public TenantId TenantId { get; private set; }
    internal string Code { get; private set; }
    internal string CodeKey { get; private set; }
    internal string EnglishName { get; private set; }
    internal string? ArabicName { get; private set; }
    internal Guid CurrencyId { get; private set; }
    internal string CurrencyCode { get; private set; }
    internal Guid? CustomerId { get; private set; }
    internal OrganizationScopeKind? OrganizationScopeKind { get; private set; }
    internal Guid? OrganizationScopeId { get; private set; }
    internal int Priority { get; private set; }
    internal MasterDataLifecycleState LifecycleState { get; private set; }
    internal int CurrentVersionNumber { get; private set; }
    internal byte[] Version { get; private set; } = Guid.NewGuid().ToByteArray();
    internal ICollection<MasterDataPriceListPriceEntity> Prices { get; private set; } = new List<MasterDataPriceListPriceEntity>();

    internal LocalizedName Name => new(EnglishName, ArabicName);

    internal void Edit(
        string code,
        LocalizedName name,
        Guid currencyId,
        string currencyCode,
        Guid? customerId,
        OrganizationScopeKind? organizationScopeKind,
        Guid? organizationScopeId,
        int priority)
    {
        Code = code;
        CodeKey = code.ToUpperInvariant();
        SetName(name);
        CurrencyId = currencyId;
        CurrencyCode = currencyCode;
        CustomerId = customerId;
        OrganizationScopeKind = organizationScopeKind;
        OrganizationScopeId = organizationScopeId;
        Priority = priority;
        TouchVersion();
    }

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

    private void SetName(LocalizedName name)
    {
        EnglishName = name.English ?? string.Empty;
        ArabicName = name.Arabic;
    }
}

internal sealed class MasterDataPriceListPriceEntity : ITenantOwned
{
    private MasterDataPriceListPriceEntity()
    {
        ProductSku = string.Empty;
        UnitOfMeasureCode = string.Empty;
        CurrencyCode = string.Empty;
        SourceReference = string.Empty;
    }

    internal MasterDataPriceListPriceEntity(
        Guid id,
        TenantId tenantId,
        Guid priceListId,
        int versionNumber,
        Guid productId,
        string productSku,
        Guid unitOfMeasureId,
        string unitOfMeasureCode,
        Guid currencyId,
        string currencyCode,
        Guid? customerId,
        OrganizationScopeKind? organizationScopeKind,
        Guid? organizationScopeId,
        int priority,
        DateOnly effectiveFrom,
        DateOnly? effectiveTo,
        decimal price,
        int priceScale,
        PriceListProvenance provenance,
        string? sourceReference)
    {
        Id = id;
        TenantId = tenantId;
        PriceListId = priceListId;
        VersionNumber = versionNumber;
        ProductId = productId;
        ProductSku = productSku;
        UnitOfMeasureId = unitOfMeasureId;
        UnitOfMeasureCode = unitOfMeasureCode;
        CurrencyId = currencyId;
        CurrencyCode = currencyCode;
        CustomerId = customerId;
        OrganizationScopeKind = organizationScopeKind;
        OrganizationScopeId = organizationScopeId;
        Priority = priority;
        EffectiveFrom = effectiveFrom;
        EffectiveTo = effectiveTo;
        Price = price;
        PriceScale = priceScale;
        Provenance = provenance;
        SourceReference = sourceReference;
    }

    internal Guid Id { get; private set; }
    public TenantId TenantId { get; private set; }
    internal Guid PriceListId { get; private set; }
    internal int VersionNumber { get; private set; }
    internal Guid ProductId { get; private set; }
    internal string ProductSku { get; private set; }
    internal Guid UnitOfMeasureId { get; private set; }
    internal string UnitOfMeasureCode { get; private set; }
    internal Guid CurrencyId { get; private set; }
    internal string CurrencyCode { get; private set; }
    internal Guid? CustomerId { get; private set; }
    internal OrganizationScopeKind? OrganizationScopeKind { get; private set; }
    internal Guid? OrganizationScopeId { get; private set; }
    internal int Priority { get; private set; }
    internal DateOnly EffectiveFrom { get; private set; }
    internal DateOnly? EffectiveTo { get; private set; }
    internal decimal Price { get; private set; }
    internal int PriceScale { get; private set; }
    internal PriceListProvenance Provenance { get; private set; }
    internal string? SourceReference { get; private set; }
    internal byte[] Version { get; private set; } = Guid.NewGuid().ToByteArray();
}

#pragma warning restore CS1591
