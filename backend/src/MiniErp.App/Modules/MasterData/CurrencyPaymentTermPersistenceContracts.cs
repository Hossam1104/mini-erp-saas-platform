#pragma warning disable CS1591

using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.Contracts.Modules.Audit;
using MiniErp.Contracts.Modules.MasterData;

namespace MiniErp.App.Modules.MasterData;

public sealed record MasterDataPaymentTermOffset(
    int Days,
    int Months);

public sealed record MasterDataPaymentTermInstallment(
    int Sequence,
    decimal Percentage,
    MasterDataPaymentTermOffset Offset);

public sealed record MasterDataEarlySettlementDiscount(
    bool Enabled,
    decimal? Percentage,
    MasterDataPaymentTermOffset Offset)
{
    public static MasterDataEarlySettlementDiscount Disabled() =>
        new(false, null, new MasterDataPaymentTermOffset(0, 0));
}

public sealed record CreateMasterDataCurrencyCommand(
    string Code,
    LocalizedName Name);

public sealed record EditMasterDataCurrencyCommand(
    Guid CurrencyId,
    string Code,
    LocalizedName Name,
    byte[] ExpectedVersion);

public sealed record CreateMasterDataPaymentTermCommand(
    string Code,
    LocalizedName Name,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    PaymentTermBaseDateRule BaseDateRule,
    PaymentTermScheduleMode ScheduleMode,
    MasterDataPaymentTermOffset DueOffset,
    IReadOnlyList<MasterDataPaymentTermInstallment> Installments,
    MasterDataEarlySettlementDiscount EarlySettlementDiscount);

public sealed record EditMasterDataPaymentTermCommand(
    Guid PaymentTermId,
    string Code,
    LocalizedName Name,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    PaymentTermBaseDateRule BaseDateRule,
    PaymentTermScheduleMode ScheduleMode,
    MasterDataPaymentTermOffset DueOffset,
    IReadOnlyList<MasterDataPaymentTermInstallment> Installments,
    MasterDataEarlySettlementDiscount EarlySettlementDiscount,
    byte[] ExpectedVersion);

public sealed record MasterDataCurrencyRecord(
    Guid Id,
    TenantId TenantId,
    string Code,
    LocalizedName Name,
    MasterDataLifecycleState LifecycleState,
    int Revision,
    byte[] Version);

public sealed record MasterDataPaymentTermVersionRecord(
    Guid Id,
    int VersionNumber,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    PaymentTermBaseDateRule BaseDateRule,
    PaymentTermScheduleMode ScheduleMode,
    MasterDataPaymentTermOffset DueOffset,
    IReadOnlyList<MasterDataPaymentTermInstallment> Installments,
    MasterDataEarlySettlementDiscount EarlySettlementDiscount,
    string Code,
    LocalizedName Name);

public sealed record MasterDataPaymentTermRecord(
    Guid Id,
    TenantId TenantId,
    string Code,
    LocalizedName Name,
    MasterDataLifecycleState LifecycleState,
    int CurrentVersionNumber,
    IReadOnlyList<MasterDataPaymentTermVersionRecord> Versions,
    byte[] Version);

public sealed record MasterDataCurrencyReferenceRecord(
    Guid Id,
    TenantId TenantId,
    string Code,
    LocalizedName Name,
    MasterDataLifecycleState LifecycleState,
    int Revision,
    ReferenceSnapshot Snapshot,
    byte[] Version);

public sealed record MasterDataPaymentTermReferenceRecord(
    Guid Id,
    TenantId TenantId,
    string Code,
    LocalizedName Name,
    MasterDataLifecycleState LifecycleState,
    int VersionNumber,
    Guid VersionId,
    DateOnly EffectiveOn,
    MasterDataPaymentTermVersionRecord Version,
    ReferenceSnapshot Snapshot,
    byte[] MasterVersion);

public sealed record MasterDataPaymentTermDueDate(
    int Sequence,
    decimal Percentage,
    DateOnly DueDate);

public sealed record MasterDataPaymentTermDueDatePreview(
    Guid Id,
    TenantId TenantId,
    string Code,
    int VersionNumber,
    Guid VersionId,
    DateOnly EffectiveOn,
    DateOnly BaseDate,
    PaymentTermBaseDateRule BaseDateRule,
    PaymentTermScheduleMode ScheduleMode,
    IReadOnlyList<MasterDataPaymentTermDueDate> DueDates,
    DateOnly? EarlySettlementDiscountDate,
    decimal? EarlySettlementDiscountPercentage,
    ReferenceSnapshot Snapshot);

public interface IMasterDataCurrencyPaymentTermPersistence
{
    Task<IReadOnlyList<MasterDataCurrencyRecord>> ListCurrenciesAsync(
        TenantContext tenantContext,
        CancellationToken cancellationToken = default);

    Task<MasterDataCurrencyRecord?> FindCurrencyAsync(
        TenantContext tenantContext,
        Guid currencyId,
        CancellationToken cancellationToken = default);

    Task<MasterDataPersistenceResult<MasterDataCurrencyRecord>> CreateCurrencyAsync(
        TenantContext tenantContext,
        Guid currencyId,
        CreateMasterDataCurrencyCommand command,
        MasterDataAuditEvidence evidence,
        CancellationToken cancellationToken = default);

    Task<MasterDataPersistenceResult<MasterDataCurrencyRecord>> EditCurrencyAsync(
        TenantContext tenantContext,
        EditMasterDataCurrencyCommand command,
        MasterDataAuditEvidence evidence,
        CancellationToken cancellationToken = default);

    Task<MasterDataPersistenceResult<MasterDataCurrencyRecord>> SetCurrencyLifecycleAsync(
        TenantContext tenantContext,
        Guid currencyId,
        MasterDataLifecycleState lifecycleState,
        byte[] expectedVersion,
        MasterDataAuditEvidence evidence,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MasterDataPaymentTermRecord>> ListPaymentTermsAsync(
        TenantContext tenantContext,
        CancellationToken cancellationToken = default);

    Task<MasterDataPaymentTermRecord?> FindPaymentTermAsync(
        TenantContext tenantContext,
        Guid paymentTermId,
        CancellationToken cancellationToken = default);

    Task<MasterDataPersistenceResult<MasterDataPaymentTermRecord>> CreatePaymentTermAsync(
        TenantContext tenantContext,
        Guid paymentTermId,
        CreateMasterDataPaymentTermCommand command,
        MasterDataAuditEvidence evidence,
        CancellationToken cancellationToken = default);

    Task<MasterDataPersistenceResult<MasterDataPaymentTermRecord>> EditPaymentTermAsync(
        TenantContext tenantContext,
        EditMasterDataPaymentTermCommand command,
        MasterDataAuditEvidence evidence,
        CancellationToken cancellationToken = default);

    Task<MasterDataPersistenceResult<MasterDataPaymentTermRecord>> SetPaymentTermLifecycleAsync(
        TenantContext tenantContext,
        Guid paymentTermId,
        MasterDataLifecycleState lifecycleState,
        byte[] expectedVersion,
        MasterDataAuditEvidence evidence,
        CancellationToken cancellationToken = default);

    Task<MasterDataPersistenceResult<MasterDataAuditRecord>> AppendAuditAsync(
        TenantContext tenantContext,
        MasterDataAuditEvidence evidence,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MasterDataAuditRecord>> ReadAuditHistoryAsync(
        TenantContext tenantContext,
        MasterDataResourceKind resourceKind,
        Guid? resourceId = null,
        CancellationToken cancellationToken = default);
}

public sealed class UnavailableMasterDataCurrencyPaymentTermPersistence : IMasterDataCurrencyPaymentTermPersistence
{
    private static Task<T> Unavailable<T>() =>
        Task.FromException<T>(new InvalidOperationException("Currency and Payment Terms persistence is unavailable."));

    public Task<IReadOnlyList<MasterDataCurrencyRecord>> ListCurrenciesAsync(TenantContext tenantContext, CancellationToken cancellationToken = default) => Unavailable<IReadOnlyList<MasterDataCurrencyRecord>>();
    public Task<MasterDataCurrencyRecord?> FindCurrencyAsync(TenantContext tenantContext, Guid currencyId, CancellationToken cancellationToken = default) => Unavailable<MasterDataCurrencyRecord?>();
    public Task<MasterDataPersistenceResult<MasterDataCurrencyRecord>> CreateCurrencyAsync(TenantContext tenantContext, Guid currencyId, CreateMasterDataCurrencyCommand command, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => Unavailable<MasterDataPersistenceResult<MasterDataCurrencyRecord>>();
    public Task<MasterDataPersistenceResult<MasterDataCurrencyRecord>> EditCurrencyAsync(TenantContext tenantContext, EditMasterDataCurrencyCommand command, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => Unavailable<MasterDataPersistenceResult<MasterDataCurrencyRecord>>();
    public Task<MasterDataPersistenceResult<MasterDataCurrencyRecord>> SetCurrencyLifecycleAsync(TenantContext tenantContext, Guid currencyId, MasterDataLifecycleState lifecycleState, byte[] expectedVersion, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => Unavailable<MasterDataPersistenceResult<MasterDataCurrencyRecord>>();
    public Task<IReadOnlyList<MasterDataPaymentTermRecord>> ListPaymentTermsAsync(TenantContext tenantContext, CancellationToken cancellationToken = default) => Unavailable<IReadOnlyList<MasterDataPaymentTermRecord>>();
    public Task<MasterDataPaymentTermRecord?> FindPaymentTermAsync(TenantContext tenantContext, Guid paymentTermId, CancellationToken cancellationToken = default) => Unavailable<MasterDataPaymentTermRecord?>();
    public Task<MasterDataPersistenceResult<MasterDataPaymentTermRecord>> CreatePaymentTermAsync(TenantContext tenantContext, Guid paymentTermId, CreateMasterDataPaymentTermCommand command, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => Unavailable<MasterDataPersistenceResult<MasterDataPaymentTermRecord>>();
    public Task<MasterDataPersistenceResult<MasterDataPaymentTermRecord>> EditPaymentTermAsync(TenantContext tenantContext, EditMasterDataPaymentTermCommand command, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => Unavailable<MasterDataPersistenceResult<MasterDataPaymentTermRecord>>();
    public Task<MasterDataPersistenceResult<MasterDataPaymentTermRecord>> SetPaymentTermLifecycleAsync(TenantContext tenantContext, Guid paymentTermId, MasterDataLifecycleState lifecycleState, byte[] expectedVersion, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => Unavailable<MasterDataPersistenceResult<MasterDataPaymentTermRecord>>();
    public Task<MasterDataPersistenceResult<MasterDataAuditRecord>> AppendAuditAsync(TenantContext tenantContext, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => Unavailable<MasterDataPersistenceResult<MasterDataAuditRecord>>();
    public Task<IReadOnlyList<MasterDataAuditRecord>> ReadAuditHistoryAsync(TenantContext tenantContext, MasterDataResourceKind resourceKind, Guid? resourceId = null, CancellationToken cancellationToken = default) => Unavailable<IReadOnlyList<MasterDataAuditRecord>>();
}

#pragma warning restore CS1591
