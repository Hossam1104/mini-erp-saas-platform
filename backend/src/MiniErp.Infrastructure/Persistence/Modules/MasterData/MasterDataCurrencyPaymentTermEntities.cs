#pragma warning disable CS1591

using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.Contracts.Modules.MasterData;

namespace MiniErp.Infrastructure.Persistence.Modules.MasterData;

internal sealed class MasterDataCurrencyEntity : ITenantOwned
{
    private MasterDataCurrencyEntity()
    {
        Code = string.Empty;
        CodeKey = string.Empty;
        EnglishName = string.Empty;
        NameKey = string.Empty;
    }

    internal MasterDataCurrencyEntity(
        Guid id,
        TenantId tenantId,
        string code,
        LocalizedName name)
    {
        Id = id;
        TenantId = tenantId;
        Code = code;
        CodeKey = code.ToUpperInvariant();
        EnglishName = name.English ?? string.Empty;
        ArabicName = name.Arabic;
        NameKey = (name.English ?? name.Arabic ?? string.Empty).ToUpperInvariant();
        LifecycleState = MasterDataLifecycleState.Active;
        Revision = 1;
    }

    internal Guid Id { get; private set; }
    public TenantId TenantId { get; private set; }
    internal string Code { get; private set; }
    internal string CodeKey { get; private set; }
    internal string EnglishName { get; private set; }
    internal string? ArabicName { get; private set; }
    internal string NameKey { get; private set; }
    internal MasterDataLifecycleState LifecycleState { get; private set; }
    internal int Revision { get; private set; }
    internal byte[] Version { get; private set; } = Guid.NewGuid().ToByteArray();

    internal LocalizedName Name => new(
        string.IsNullOrWhiteSpace(EnglishName) ? null : EnglishName,
        ArabicName);

    internal void Edit(string code, LocalizedName name)
    {
        Code = code;
        CodeKey = code.ToUpperInvariant();
        EnglishName = name.English ?? string.Empty;
        ArabicName = name.Arabic;
        NameKey = (name.English ?? name.Arabic ?? string.Empty).ToUpperInvariant();
        Revision++;
        TouchVersion();
    }

    internal void SetLifecycle(MasterDataLifecycleState lifecycleState)
    {
        LifecycleState = lifecycleState;
        Revision++;
        TouchVersion();
    }

    internal void TouchVersion() => Version = Guid.NewGuid().ToByteArray();
}

internal sealed class MasterDataPaymentTermEntity : ITenantOwned
{
    private MasterDataPaymentTermEntity()
    {
        Code = string.Empty;
        CodeKey = string.Empty;
        EnglishName = string.Empty;
        NameKey = string.Empty;
    }

    internal MasterDataPaymentTermEntity(
        Guid id,
        TenantId tenantId,
        string code,
        LocalizedName name)
    {
        Id = id;
        TenantId = tenantId;
        Code = code;
        CodeKey = code.ToUpperInvariant();
        EnglishName = name.English ?? string.Empty;
        ArabicName = name.Arabic;
        NameKey = (name.English ?? name.Arabic ?? string.Empty).ToUpperInvariant();
        LifecycleState = MasterDataLifecycleState.Active;
        CurrentVersionNumber = 1;
    }

    internal Guid Id { get; private set; }
    public TenantId TenantId { get; private set; }
    internal string Code { get; private set; }
    internal string CodeKey { get; private set; }
    internal string EnglishName { get; private set; }
    internal string? ArabicName { get; private set; }
    internal string NameKey { get; private set; }
    internal MasterDataLifecycleState LifecycleState { get; private set; }
    internal int CurrentVersionNumber { get; private set; }
    internal byte[] Version { get; private set; } = Guid.NewGuid().ToByteArray();
    internal ICollection<MasterDataPaymentTermVersionEntity> Versions { get; private set; } = new List<MasterDataPaymentTermVersionEntity>();

    internal LocalizedName Name => new(
        string.IsNullOrWhiteSpace(EnglishName) ? null : EnglishName,
        ArabicName);

    internal void EditIdentity(string code, LocalizedName name, int versionNumber)
    {
        Code = code;
        CodeKey = code.ToUpperInvariant();
        EnglishName = name.English ?? string.Empty;
        ArabicName = name.Arabic;
        NameKey = (name.English ?? name.Arabic ?? string.Empty).ToUpperInvariant();
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

internal sealed class MasterDataPaymentTermVersionEntity : ITenantOwned
{
    private MasterDataPaymentTermVersionEntity()
    {
        Code = string.Empty;
        EnglishName = string.Empty;
    }

    internal MasterDataPaymentTermVersionEntity(
        Guid id,
        TenantId tenantId,
        Guid paymentTermId,
        int versionNumber,
        string code,
        LocalizedName name,
        DateOnly effectiveFrom,
        DateOnly? effectiveTo,
        PaymentTermBaseDateRule baseDateRule,
        PaymentTermScheduleMode scheduleMode,
        int dueOffsetDays,
        int dueOffsetMonths,
        bool earlySettlementDiscountEnabled,
        decimal? earlySettlementDiscountPercentage,
        int earlySettlementDiscountDays,
        int earlySettlementDiscountMonths)
    {
        Id = id;
        TenantId = tenantId;
        PaymentTermId = paymentTermId;
        VersionNumber = versionNumber;
        Code = code;
        EnglishName = name.English ?? string.Empty;
        ArabicName = name.Arabic;
        EffectiveFrom = effectiveFrom;
        EffectiveTo = effectiveTo;
        BaseDateRule = baseDateRule;
        ScheduleMode = scheduleMode;
        DueOffsetDays = dueOffsetDays;
        DueOffsetMonths = dueOffsetMonths;
        EarlySettlementDiscountEnabled = earlySettlementDiscountEnabled;
        EarlySettlementDiscountPercentage = earlySettlementDiscountPercentage;
        EarlySettlementDiscountDays = earlySettlementDiscountDays;
        EarlySettlementDiscountMonths = earlySettlementDiscountMonths;
    }

    internal Guid Id { get; private set; }
    public TenantId TenantId { get; private set; }
    internal Guid PaymentTermId { get; private set; }
    internal int VersionNumber { get; private set; }
    internal string Code { get; private set; }
    internal string EnglishName { get; private set; }
    internal string? ArabicName { get; private set; }
    internal DateOnly EffectiveFrom { get; private set; }
    internal DateOnly? EffectiveTo { get; private set; }
    internal PaymentTermBaseDateRule BaseDateRule { get; private set; }
    internal PaymentTermScheduleMode ScheduleMode { get; private set; }
    internal int DueOffsetDays { get; private set; }
    internal int DueOffsetMonths { get; private set; }
    internal bool EarlySettlementDiscountEnabled { get; private set; }
    internal decimal? EarlySettlementDiscountPercentage { get; private set; }
    internal int EarlySettlementDiscountDays { get; private set; }
    internal int EarlySettlementDiscountMonths { get; private set; }
    internal byte[] Version { get; private set; } = Guid.NewGuid().ToByteArray();
    internal ICollection<MasterDataPaymentTermInstallmentEntity> Installments { get; private set; } = new List<MasterDataPaymentTermInstallmentEntity>();

    internal LocalizedName Name => new(
        string.IsNullOrWhiteSpace(EnglishName) ? null : EnglishName,
        ArabicName);

    internal void CloseAt(DateOnly effectiveTo)
    {
        EffectiveTo = effectiveTo;
        TouchVersion();
    }

    internal void TouchVersion() => Version = Guid.NewGuid().ToByteArray();
}

internal sealed class MasterDataPaymentTermInstallmentEntity : ITenantOwned
{
    private MasterDataPaymentTermInstallmentEntity()
    {
    }

    internal MasterDataPaymentTermInstallmentEntity(
        Guid id,
        TenantId tenantId,
        Guid paymentTermVersionId,
        int sequence,
        decimal percentage,
        int offsetDays,
        int offsetMonths)
    {
        Id = id;
        TenantId = tenantId;
        PaymentTermVersionId = paymentTermVersionId;
        Sequence = sequence;
        Percentage = percentage;
        OffsetDays = offsetDays;
        OffsetMonths = offsetMonths;
    }

    internal Guid Id { get; private set; }
    public TenantId TenantId { get; private set; }
    internal Guid PaymentTermVersionId { get; private set; }
    internal int Sequence { get; private set; }
    internal decimal Percentage { get; private set; }
    internal int OffsetDays { get; private set; }
    internal int OffsetMonths { get; private set; }
}

#pragma warning restore CS1591
