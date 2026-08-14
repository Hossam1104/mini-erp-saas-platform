#pragma warning disable CS1591

using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.Contracts.Modules.Audit;
using MiniErp.Contracts.Modules.MasterData;

namespace MiniErp.App.Modules.MasterData;

public sealed record CreateMasterDataPriceListCommand(
    string Code,
    LocalizedName Name,
    Guid CurrencyId,
    Guid? CustomerId,
    OrganizationScopeKind? OrganizationScopeKind,
    Guid? OrganizationScopeId,
    int Priority);

public sealed record EditMasterDataPriceListCommand(
    Guid PriceListId,
    string Code,
    LocalizedName Name,
    Guid CurrencyId,
    Guid? CustomerId,
    OrganizationScopeKind? OrganizationScopeKind,
    Guid? OrganizationScopeId,
    int Priority,
    byte[] ExpectedVersion);

public sealed record AppendMasterDataPriceCommand(
    Guid PriceListId,
    Guid ProductId,
    Guid UnitOfMeasureId,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    decimal Price,
    int PriceScale,
    PriceListProvenance Provenance,
    string? SourceReference,
    byte[] ExpectedVersion);

public sealed record ResolveMasterDataPriceQuery(
    Guid? PriceListId,
    Guid ProductId,
    Guid UnitOfMeasureId,
    Guid CurrencyId,
    Guid? CustomerId,
    OrganizationScopeKind? OrganizationScopeKind,
    Guid? OrganizationScopeId,
    DateOnly EffectiveOn);

public sealed record MasterDataPriceListPriceRecord(
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

public sealed record MasterDataPriceListRecord(
    Guid Id,
    TenantId TenantId,
    string Code,
    LocalizedName Name,
    Guid CurrencyId,
    string CurrencyCode,
    Guid? CustomerId,
    OrganizationScopeKind? OrganizationScopeKind,
    Guid? OrganizationScopeId,
    int Priority,
    MasterDataLifecycleState LifecycleState,
    int CurrentVersionNumber,
    IReadOnlyList<MasterDataPriceListPriceRecord> Prices,
    byte[] Version);

/// <summary>
/// The current parent configuration used to decide whether a historical Price
/// List row is applicable. The row snapshot remains immutable evidence and is
/// intentionally exposed separately from this configuration.
/// </summary>
public sealed record MasterDataPriceListCurrentConfiguration(
    Guid CurrencyId,
    string CurrencyCode,
    Guid? CustomerId,
    OrganizationScopeKind? OrganizationScopeKind,
    Guid? OrganizationScopeId,
    int Priority,
    MasterDataLifecycleState LifecycleState);

public sealed record MasterDataPriceListReferenceRecord(
    Guid PriceListId,
    TenantId TenantId,
    string PriceListCode,
    MasterDataPriceListPriceRecord Price,
    MasterDataPriceListCurrentConfiguration CurrentConfiguration,
    DateOnly EffectiveOn,
    ReferenceSnapshot Snapshot,
    byte[] MasterVersion);

public interface IMasterDataPriceListPersistence
{
    Task<IReadOnlyList<MasterDataPriceListRecord>> ListPriceListsAsync(
        TenantContext tenantContext,
        string? search,
        CancellationToken cancellationToken = default);

    Task<MasterDataPriceListRecord?> FindPriceListAsync(
        TenantContext tenantContext,
        Guid priceListId,
        CancellationToken cancellationToken = default);

    Task<MasterDataPersistenceResult<MasterDataPriceListRecord>> CreatePriceListAsync(
        TenantContext tenantContext,
        Guid priceListId,
        CreateMasterDataPriceListCommand command,
        MasterDataAuditEvidence evidence,
        CancellationToken cancellationToken = default);

    Task<MasterDataPersistenceResult<MasterDataPriceListRecord>> EditPriceListAsync(
        TenantContext tenantContext,
        EditMasterDataPriceListCommand command,
        MasterDataAuditEvidence evidence,
        CancellationToken cancellationToken = default);

    Task<MasterDataPersistenceResult<MasterDataPriceListRecord>> AppendPriceAsync(
        TenantContext tenantContext,
        AppendMasterDataPriceCommand command,
        MasterDataAuditEvidence evidence,
        CancellationToken cancellationToken = default);

    Task<MasterDataPersistenceResult<MasterDataPriceListRecord>> SetPriceListLifecycleAsync(
        TenantContext tenantContext,
        Guid priceListId,
        MasterDataLifecycleState lifecycleState,
        byte[] expectedVersion,
        MasterDataAuditEvidence evidence,
        CancellationToken cancellationToken = default);

    Task<MasterDataPersistenceResult<MasterDataPriceListReferenceRecord>> ResolvePriceAsync(
        TenantContext tenantContext,
        ResolveMasterDataPriceQuery query,
        MasterDataAuditEvidence evidence,
        CancellationToken cancellationToken = default);

    Task<MasterDataPersistenceResult<MasterDataAuditRecord>> AppendAuditAsync(
        TenantContext tenantContext,
        MasterDataAuditEvidence evidence,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MasterDataAuditRecord>> ReadAuditHistoryAsync(
        TenantContext tenantContext,
        Guid? priceListId = null,
        CancellationToken cancellationToken = default);
}

public sealed class UnavailableMasterDataPriceListPersistence : IMasterDataPriceListPersistence
{
    private static Task<T> Unavailable<T>() =>
        Task.FromException<T>(new InvalidOperationException("Price List persistence is unavailable."));

    public Task<IReadOnlyList<MasterDataPriceListRecord>> ListPriceListsAsync(TenantContext tenantContext, string? search, CancellationToken cancellationToken = default) => Unavailable<IReadOnlyList<MasterDataPriceListRecord>>();
    public Task<MasterDataPriceListRecord?> FindPriceListAsync(TenantContext tenantContext, Guid priceListId, CancellationToken cancellationToken = default) => Unavailable<MasterDataPriceListRecord?>();
    public Task<MasterDataPersistenceResult<MasterDataPriceListRecord>> CreatePriceListAsync(TenantContext tenantContext, Guid priceListId, CreateMasterDataPriceListCommand command, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => Unavailable<MasterDataPersistenceResult<MasterDataPriceListRecord>>();
    public Task<MasterDataPersistenceResult<MasterDataPriceListRecord>> EditPriceListAsync(TenantContext tenantContext, EditMasterDataPriceListCommand command, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => Unavailable<MasterDataPersistenceResult<MasterDataPriceListRecord>>();
    public Task<MasterDataPersistenceResult<MasterDataPriceListRecord>> AppendPriceAsync(TenantContext tenantContext, AppendMasterDataPriceCommand command, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => Unavailable<MasterDataPersistenceResult<MasterDataPriceListRecord>>();
    public Task<MasterDataPersistenceResult<MasterDataPriceListRecord>> SetPriceListLifecycleAsync(TenantContext tenantContext, Guid priceListId, MasterDataLifecycleState lifecycleState, byte[] expectedVersion, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => Unavailable<MasterDataPersistenceResult<MasterDataPriceListRecord>>();
    public Task<MasterDataPersistenceResult<MasterDataPriceListReferenceRecord>> ResolvePriceAsync(TenantContext tenantContext, ResolveMasterDataPriceQuery query, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => Unavailable<MasterDataPersistenceResult<MasterDataPriceListReferenceRecord>>();
    public Task<MasterDataPersistenceResult<MasterDataAuditRecord>> AppendAuditAsync(TenantContext tenantContext, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => Unavailable<MasterDataPersistenceResult<MasterDataAuditRecord>>();
    public Task<IReadOnlyList<MasterDataAuditRecord>> ReadAuditHistoryAsync(TenantContext tenantContext, Guid? priceListId = null, CancellationToken cancellationToken = default) => Unavailable<IReadOnlyList<MasterDataAuditRecord>>();
}

#pragma warning restore CS1591
