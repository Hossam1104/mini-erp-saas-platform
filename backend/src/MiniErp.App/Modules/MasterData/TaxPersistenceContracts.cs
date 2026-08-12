#pragma warning disable CS1591

using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.Contracts.Modules.Audit;
using MiniErp.Contracts.Modules.MasterData;

namespace MiniErp.App.Modules.MasterData;

public sealed record MasterDataTaxRateVersion(
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    decimal RatePercentage);

public sealed record CreateMasterDataTaxCommand(
    string Code,
    string CategoryCode,
    LocalizedName CategoryName,
    LocalizedName Name,
    TaxDirection Applicability,
    MasterDataTaxRateVersion RateVersion);

public sealed record EditMasterDataTaxCommand(
    Guid TaxId,
    string Code,
    string CategoryCode,
    LocalizedName CategoryName,
    LocalizedName Name,
    TaxDirection Applicability,
    MasterDataTaxRateVersion RateVersion,
    byte[] ExpectedVersion);

public sealed record MasterDataTaxRateVersionRecord(
    Guid Id,
    int VersionNumber,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    decimal RatePercentage);

public sealed record MasterDataTaxRecord(
    Guid Id,
    TenantId TenantId,
    string Code,
    string CategoryCode,
    LocalizedName CategoryName,
    LocalizedName Name,
    TaxDirection Applicability,
    MasterDataLifecycleState LifecycleState,
    int CurrentVersionNumber,
    IReadOnlyList<MasterDataTaxRateVersionRecord> RateVersions,
    byte[] Version);

public sealed record MasterDataTaxReferenceRecord(
    Guid Id,
    TenantId TenantId,
    string Code,
    string CategoryCode,
    LocalizedName CategoryName,
    LocalizedName Name,
    TaxDirection Applicability,
    MasterDataLifecycleState LifecycleState,
    int VersionNumber,
    Guid VersionId,
    DateOnly EffectiveOn,
    MasterDataTaxRateVersionRecord RateVersion,
    ReferenceSnapshot Snapshot,
    byte[] MasterVersion);

public sealed record MasterDataTaxCalculation(
    Guid TaxId,
    TenantId TenantId,
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
    ReferenceSnapshot Snapshot);

public interface IMasterDataTaxPersistence
{
    Task<IReadOnlyList<MasterDataTaxRecord>> ListTaxesAsync(
        TenantContext tenantContext,
        CancellationToken cancellationToken = default);

    Task<MasterDataTaxRecord?> FindTaxAsync(
        TenantContext tenantContext,
        Guid taxId,
        CancellationToken cancellationToken = default);

    Task<MasterDataPersistenceResult<MasterDataTaxRecord>> CreateTaxAsync(
        TenantContext tenantContext,
        Guid taxId,
        CreateMasterDataTaxCommand command,
        MasterDataAuditEvidence evidence,
        CancellationToken cancellationToken = default);

    Task<MasterDataPersistenceResult<MasterDataTaxRecord>> EditTaxAsync(
        TenantContext tenantContext,
        EditMasterDataTaxCommand command,
        MasterDataAuditEvidence evidence,
        CancellationToken cancellationToken = default);

    Task<MasterDataPersistenceResult<MasterDataTaxRecord>> SetTaxLifecycleAsync(
        TenantContext tenantContext,
        Guid taxId,
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
        Guid? taxId = null,
        CancellationToken cancellationToken = default);
}

public sealed class UnavailableMasterDataTaxPersistence : IMasterDataTaxPersistence
{
    private static Task<T> Unavailable<T>() =>
        Task.FromException<T>(new InvalidOperationException("Tax persistence is unavailable."));

    public Task<IReadOnlyList<MasterDataTaxRecord>> ListTaxesAsync(TenantContext tenantContext, CancellationToken cancellationToken = default) => Unavailable<IReadOnlyList<MasterDataTaxRecord>>();
    public Task<MasterDataTaxRecord?> FindTaxAsync(TenantContext tenantContext, Guid taxId, CancellationToken cancellationToken = default) => Unavailable<MasterDataTaxRecord?>();
    public Task<MasterDataPersistenceResult<MasterDataTaxRecord>> CreateTaxAsync(TenantContext tenantContext, Guid taxId, CreateMasterDataTaxCommand command, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => Unavailable<MasterDataPersistenceResult<MasterDataTaxRecord>>();
    public Task<MasterDataPersistenceResult<MasterDataTaxRecord>> EditTaxAsync(TenantContext tenantContext, EditMasterDataTaxCommand command, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => Unavailable<MasterDataPersistenceResult<MasterDataTaxRecord>>();
    public Task<MasterDataPersistenceResult<MasterDataTaxRecord>> SetTaxLifecycleAsync(TenantContext tenantContext, Guid taxId, MasterDataLifecycleState lifecycleState, byte[] expectedVersion, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => Unavailable<MasterDataPersistenceResult<MasterDataTaxRecord>>();
    public Task<MasterDataPersistenceResult<MasterDataAuditRecord>> AppendAuditAsync(TenantContext tenantContext, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => Unavailable<MasterDataPersistenceResult<MasterDataAuditRecord>>();
    public Task<IReadOnlyList<MasterDataAuditRecord>> ReadAuditHistoryAsync(TenantContext tenantContext, Guid? taxId = null, CancellationToken cancellationToken = default) => Unavailable<IReadOnlyList<MasterDataAuditRecord>>();
}

#pragma warning restore CS1591
