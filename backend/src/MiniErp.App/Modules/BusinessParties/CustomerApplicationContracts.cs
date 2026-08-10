#pragma warning disable CS1591

using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.Modules.MasterData;
using MiniErp.Contracts.Modules.Audit;
using MiniErp.Contracts.Modules.MasterData;

namespace MiniErp.App.Modules.BusinessParties;

public sealed record CustomerContactCommand(
    string Name,
    string? Email,
    string? Phone);

public sealed record CreateCustomerCommand(
    string Code,
    LocalizedName LegalName,
    LocalizedName? TradingName,
    IReadOnlyList<CustomerContactCommand> Contacts);

public sealed record EditCustomerCommand(
    Guid CustomerId,
    string Code,
    LocalizedName LegalName,
    LocalizedName? TradingName,
    IReadOnlyList<CustomerContactCommand> Contacts,
    byte[] ExpectedVersion);

public sealed record CustomerContactRecord(
    Guid Id,
    TenantId TenantId,
    Guid CustomerId,
    string Name,
    string? Email,
    string? Phone,
    byte[] Version);

public sealed record CustomerRecord(
    Guid Id,
    TenantId TenantId,
    string Code,
    LocalizedName LegalName,
    LocalizedName? TradingName,
    MasterDataLifecycleState LifecycleState,
    byte[] Version,
    IReadOnlyList<CustomerContactRecord> Contacts);

public interface ICustomerPersistence
{
    Task<IReadOnlyList<CustomerRecord>> ListCustomersAsync(
        TenantContext tenantContext,
        CancellationToken cancellationToken = default);

    Task<CustomerRecord?> FindCustomerAsync(
        TenantContext tenantContext,
        Guid customerId,
        CancellationToken cancellationToken = default);

    Task<MasterDataPersistenceResult<CustomerRecord>> CreateCustomerAsync(
        TenantContext tenantContext,
        Guid customerId,
        CreateCustomerCommand command,
        MasterDataAuditEvidence evidence,
        CancellationToken cancellationToken = default);

    Task<MasterDataPersistenceResult<CustomerRecord>> EditCustomerAsync(
        TenantContext tenantContext,
        EditCustomerCommand command,
        MasterDataAuditEvidence evidence,
        CancellationToken cancellationToken = default);

    Task<MasterDataPersistenceResult<CustomerRecord>> SetCustomerLifecycleAsync(
        TenantContext tenantContext,
        Guid customerId,
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
        Guid customerId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Safe composition fallback. It does not create an in-memory Customer store.
/// </summary>
public sealed class UnavailableCustomerPersistence : ICustomerPersistence
{
    public Task<IReadOnlyList<CustomerRecord>> ListCustomersAsync(
        TenantContext tenantContext,
        CancellationToken cancellationToken = default) => Unavailable<IReadOnlyList<CustomerRecord>>();

    public Task<CustomerRecord?> FindCustomerAsync(
        TenantContext tenantContext,
        Guid customerId,
        CancellationToken cancellationToken = default) => Unavailable<CustomerRecord?>();

    public Task<MasterDataPersistenceResult<CustomerRecord>> CreateCustomerAsync(
        TenantContext tenantContext,
        Guid customerId,
        CreateCustomerCommand command,
        MasterDataAuditEvidence evidence,
        CancellationToken cancellationToken = default) => Unavailable<MasterDataPersistenceResult<CustomerRecord>>();

    public Task<MasterDataPersistenceResult<CustomerRecord>> EditCustomerAsync(
        TenantContext tenantContext,
        EditCustomerCommand command,
        MasterDataAuditEvidence evidence,
        CancellationToken cancellationToken = default) => Unavailable<MasterDataPersistenceResult<CustomerRecord>>();

    public Task<MasterDataPersistenceResult<CustomerRecord>> SetCustomerLifecycleAsync(
        TenantContext tenantContext,
        Guid customerId,
        MasterDataLifecycleState lifecycleState,
        byte[] expectedVersion,
        MasterDataAuditEvidence evidence,
        CancellationToken cancellationToken = default) => Unavailable<MasterDataPersistenceResult<CustomerRecord>>();

    public Task<MasterDataPersistenceResult<MasterDataAuditRecord>> AppendAuditAsync(
        TenantContext tenantContext,
        MasterDataAuditEvidence evidence,
        CancellationToken cancellationToken = default) => Unavailable<MasterDataPersistenceResult<MasterDataAuditRecord>>();

    public Task<IReadOnlyList<MasterDataAuditRecord>> ReadAuditHistoryAsync(
        TenantContext tenantContext,
        Guid customerId,
        CancellationToken cancellationToken = default) => Unavailable<IReadOnlyList<MasterDataAuditRecord>>();

    private static Task<T> Unavailable<T>() =>
        Task.FromException<T>(new InvalidOperationException("Business Customer persistence is not configured."));
}

#pragma warning restore CS1591
