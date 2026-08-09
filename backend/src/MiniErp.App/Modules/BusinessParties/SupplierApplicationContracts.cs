#pragma warning disable CS1591

using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.Modules.MasterData;
using MiniErp.Contracts.Modules.Audit;
using MiniErp.Contracts.Modules.MasterData;

namespace MiniErp.App.Modules.BusinessParties;

public sealed record SupplierContactCommand(
    string Name,
    string? Email,
    string? Phone);

public sealed record CreateSupplierCommand(
    string Code,
    LocalizedName LegalName,
    LocalizedName? TradingName,
    string? RegistrationReference,
    IReadOnlyList<SupplierContactCommand> Contacts);

public sealed record EditSupplierCommand(
    Guid SupplierId,
    string Code,
    LocalizedName LegalName,
    LocalizedName? TradingName,
    string? RegistrationReference,
    IReadOnlyList<SupplierContactCommand> Contacts,
    byte[] ExpectedVersion);

public sealed record SupplierContactRecord(
    Guid Id,
    TenantId TenantId,
    Guid SupplierId,
    string Name,
    string? Email,
    string? Phone,
    byte[] Version);

public sealed record SupplierRecord(
    Guid Id,
    TenantId TenantId,
    string Code,
    LocalizedName LegalName,
    LocalizedName? TradingName,
    string? RegistrationReference,
    MasterDataLifecycleState LifecycleState,
    byte[] Version,
    IReadOnlyList<SupplierContactRecord> Contacts);

/// <summary>
/// Non-blocking seam for a future Business Customer matching implementation.
/// A match may be surfaced for review, but it must never become a cross-role
/// duplicate rejection or a unified Party identity.
/// </summary>
public sealed record SupplierCrossRoleMatchReview(
    bool ReviewRequired,
    string Code,
    int EvidenceCount)
{
    public static SupplierCrossRoleMatchReview None() => new(false, "not_evaluated", 0);

    public static SupplierCrossRoleMatchReview Unavailable() =>
        new(false, "review_unavailable", 0);
}

public interface ISupplierCrossRoleMatchPolicy
{
    SupplierCrossRoleMatchReview Evaluate(
        MasterDataRequestContext context,
        string legalNameKey,
        string? registrationKey);
}

/// <summary>
/// Safe Release-1 fallback while the Business Customer slice is not present.
/// It intentionally returns no match and never rejects Supplier creation.
/// </summary>
public sealed class SupplierCrossRoleMatchPolicyUnavailable : ISupplierCrossRoleMatchPolicy
{
    public SupplierCrossRoleMatchReview Evaluate(
        MasterDataRequestContext context,
        string legalNameKey,
        string? registrationKey)
    {
        ArgumentNullException.ThrowIfNull(context);
        return SupplierCrossRoleMatchReview.Unavailable();
    }
}

public interface ISupplierPersistence
{
    Task<IReadOnlyList<SupplierRecord>> ListSuppliersAsync(
        TenantContext tenantContext,
        CancellationToken cancellationToken = default);

    Task<SupplierRecord?> FindSupplierAsync(
        TenantContext tenantContext,
        Guid supplierId,
        CancellationToken cancellationToken = default);

    Task<MasterDataPersistenceResult<SupplierRecord>> CreateSupplierAsync(
        TenantContext tenantContext,
        Guid supplierId,
        CreateSupplierCommand command,
        MasterDataAuditEvidence evidence,
        CancellationToken cancellationToken = default);

    Task<MasterDataPersistenceResult<SupplierRecord>> EditSupplierAsync(
        TenantContext tenantContext,
        EditSupplierCommand command,
        MasterDataAuditEvidence evidence,
        CancellationToken cancellationToken = default);

    Task<MasterDataPersistenceResult<SupplierRecord>> SetSupplierLifecycleAsync(
        TenantContext tenantContext,
        Guid supplierId,
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
        Guid supplierId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Safe composition fallback. It does not create an in-memory Supplier store.
/// </summary>
public sealed class UnavailableSupplierPersistence : ISupplierPersistence
{
    public Task<IReadOnlyList<SupplierRecord>> ListSuppliersAsync(
        TenantContext tenantContext,
        CancellationToken cancellationToken = default) => Unavailable<IReadOnlyList<SupplierRecord>>();

    public Task<SupplierRecord?> FindSupplierAsync(
        TenantContext tenantContext,
        Guid supplierId,
        CancellationToken cancellationToken = default) => Unavailable<SupplierRecord?>();

    public Task<MasterDataPersistenceResult<SupplierRecord>> CreateSupplierAsync(
        TenantContext tenantContext,
        Guid supplierId,
        CreateSupplierCommand command,
        MasterDataAuditEvidence evidence,
        CancellationToken cancellationToken = default) => Unavailable<MasterDataPersistenceResult<SupplierRecord>>();

    public Task<MasterDataPersistenceResult<SupplierRecord>> EditSupplierAsync(
        TenantContext tenantContext,
        EditSupplierCommand command,
        MasterDataAuditEvidence evidence,
        CancellationToken cancellationToken = default) => Unavailable<MasterDataPersistenceResult<SupplierRecord>>();

    public Task<MasterDataPersistenceResult<SupplierRecord>> SetSupplierLifecycleAsync(
        TenantContext tenantContext,
        Guid supplierId,
        MasterDataLifecycleState lifecycleState,
        byte[] expectedVersion,
        MasterDataAuditEvidence evidence,
        CancellationToken cancellationToken = default) => Unavailable<MasterDataPersistenceResult<SupplierRecord>>();

    public Task<MasterDataPersistenceResult<MasterDataAuditRecord>> AppendAuditAsync(
        TenantContext tenantContext,
        MasterDataAuditEvidence evidence,
        CancellationToken cancellationToken = default) => Unavailable<MasterDataPersistenceResult<MasterDataAuditRecord>>();

    public Task<IReadOnlyList<MasterDataAuditRecord>> ReadAuditHistoryAsync(
        TenantContext tenantContext,
        Guid supplierId,
        CancellationToken cancellationToken = default) => Unavailable<IReadOnlyList<MasterDataAuditRecord>>();

    private static Task<T> Unavailable<T>() =>
        Task.FromException<T>(new InvalidOperationException("Supplier persistence is not configured."));
}

#pragma warning restore CS1591
