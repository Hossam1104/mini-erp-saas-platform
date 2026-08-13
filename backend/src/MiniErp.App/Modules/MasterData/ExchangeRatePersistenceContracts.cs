#pragma warning disable CS1591

using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.Contracts.Modules.Audit;
using MiniErp.Contracts.Modules.MasterData;

namespace MiniErp.App.Modules.MasterData;

public sealed record CreateMasterDataExchangeRateCommand(
    Guid SourceCurrencyId,
    Guid TargetCurrencyId,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    decimal Rate,
    int RateScale,
    ExchangeRateProvenance Provenance,
    string? SourceNotes);

public sealed record EditMasterDataExchangeRateCommand(
    Guid ExchangeRateId,
    Guid SourceCurrencyId,
    Guid TargetCurrencyId,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    decimal Rate,
    int RateScale,
    ExchangeRateProvenance Provenance,
    string? SourceNotes,
    byte[] ExpectedVersion);

public sealed record MasterDataExchangeRateVersionRecord(
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

public sealed record MasterDataExchangeRateRecord(
    Guid Id,
    TenantId TenantId,
    Guid SourceCurrencyId,
    Guid TargetCurrencyId,
    string SourceCurrencyCode,
    string TargetCurrencyCode,
    MasterDataLifecycleState LifecycleState,
    int CurrentVersionNumber,
    IReadOnlyList<MasterDataExchangeRateVersionRecord> Versions,
    byte[] Version);

public sealed record MasterDataExchangeRateReferenceRecord(
    Guid Id,
    TenantId TenantId,
    Guid SourceCurrencyId,
    Guid TargetCurrencyId,
    string SourceCurrencyCode,
    string TargetCurrencyCode,
    MasterDataLifecycleState LifecycleState,
    int VersionNumber,
    Guid VersionId,
    DateOnly EffectiveOn,
    MasterDataExchangeRateVersionRecord Version,
    ReferenceSnapshot Snapshot,
    byte[] MasterVersion);

public interface IMasterDataExchangeRatePersistence
{
    Task<IReadOnlyList<MasterDataExchangeRateRecord>> ListExchangeRatesAsync(
        TenantContext tenantContext,
        CancellationToken cancellationToken = default);

    Task<MasterDataExchangeRateRecord?> FindExchangeRateAsync(
        TenantContext tenantContext,
        Guid exchangeRateId,
        CancellationToken cancellationToken = default);

    Task<MasterDataPersistenceResult<MasterDataExchangeRateRecord>> CreateExchangeRateAsync(
        TenantContext tenantContext,
        Guid exchangeRateId,
        CreateMasterDataExchangeRateCommand command,
        MasterDataAuditEvidence evidence,
        CancellationToken cancellationToken = default);

    Task<MasterDataPersistenceResult<MasterDataExchangeRateRecord>> EditExchangeRateAsync(
        TenantContext tenantContext,
        EditMasterDataExchangeRateCommand command,
        MasterDataAuditEvidence evidence,
        CancellationToken cancellationToken = default);

    Task<MasterDataPersistenceResult<MasterDataExchangeRateRecord>> SetExchangeRateLifecycleAsync(
        TenantContext tenantContext,
        Guid exchangeRateId,
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
        Guid? exchangeRateId = null,
        CancellationToken cancellationToken = default);
}

public sealed class UnavailableMasterDataExchangeRatePersistence : IMasterDataExchangeRatePersistence
{
    private static Task<T> Unavailable<T>() =>
        Task.FromException<T>(new InvalidOperationException("Exchange Rate persistence is unavailable."));

    public Task<IReadOnlyList<MasterDataExchangeRateRecord>> ListExchangeRatesAsync(TenantContext tenantContext, CancellationToken cancellationToken = default) => Unavailable<IReadOnlyList<MasterDataExchangeRateRecord>>();
    public Task<MasterDataExchangeRateRecord?> FindExchangeRateAsync(TenantContext tenantContext, Guid exchangeRateId, CancellationToken cancellationToken = default) => Unavailable<MasterDataExchangeRateRecord?>();
    public Task<MasterDataPersistenceResult<MasterDataExchangeRateRecord>> CreateExchangeRateAsync(TenantContext tenantContext, Guid exchangeRateId, CreateMasterDataExchangeRateCommand command, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => Unavailable<MasterDataPersistenceResult<MasterDataExchangeRateRecord>>();
    public Task<MasterDataPersistenceResult<MasterDataExchangeRateRecord>> EditExchangeRateAsync(TenantContext tenantContext, EditMasterDataExchangeRateCommand command, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => Unavailable<MasterDataPersistenceResult<MasterDataExchangeRateRecord>>();
    public Task<MasterDataPersistenceResult<MasterDataExchangeRateRecord>> SetExchangeRateLifecycleAsync(TenantContext tenantContext, Guid exchangeRateId, MasterDataLifecycleState lifecycleState, byte[] expectedVersion, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => Unavailable<MasterDataPersistenceResult<MasterDataExchangeRateRecord>>();
    public Task<MasterDataPersistenceResult<MasterDataAuditRecord>> AppendAuditAsync(TenantContext tenantContext, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => Unavailable<MasterDataPersistenceResult<MasterDataAuditRecord>>();
    public Task<IReadOnlyList<MasterDataAuditRecord>> ReadAuditHistoryAsync(TenantContext tenantContext, Guid? exchangeRateId = null, CancellationToken cancellationToken = default) => Unavailable<IReadOnlyList<MasterDataAuditRecord>>();
}

#pragma warning restore CS1591
