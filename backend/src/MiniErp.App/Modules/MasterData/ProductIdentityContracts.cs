#pragma warning disable CS1591

using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.Contracts.Modules.Audit;
using MiniErp.Contracts.Modules.MasterData;

namespace MiniErp.App.Modules.MasterData;

public sealed record CreateProductIdentityCommand(
    string Sku,
    LocalizedName Name,
    string? Description,
    Guid CategoryId,
    Guid BaseUnitOfMeasureId,
    IReadOnlyList<string>? Barcodes,
    bool? TrackingEnabledOverride,
    bool IsSellable,
    bool IsPurchasable,
    bool IsInventoryRelevant);

public sealed record EditProductIdentityCommand(
    Guid ProductId,
    string Sku,
    LocalizedName Name,
    string? Description,
    Guid CategoryId,
    Guid BaseUnitOfMeasureId,
    IReadOnlyList<string>? Barcodes,
    bool? TrackingEnabledOverride,
    bool IsSellable,
    bool IsPurchasable,
    bool IsInventoryRelevant,
    byte[] ExpectedVersion);

public sealed record ProductIdentityRecord(
    Guid Id,
    TenantId TenantId,
    string Sku,
    LocalizedName Name,
    string? Description,
    Guid CategoryId,
    Guid BaseUnitOfMeasureId,
    bool TrackingDefaultEnabled,
    bool? TrackingEnabledOverride,
    bool TrackingEnabled,
    bool IsSellable,
    bool IsPurchasable,
    bool IsInventoryRelevant,
    MasterDataLifecycleState LifecycleState,
    byte[] Version,
    IReadOnlyList<ProductBarcodeRecord> Barcodes);

public sealed record ProductBarcodeRecord(
    Guid Id,
    TenantId TenantId,
    Guid ProductId,
    string Value,
    byte[] Version);

public sealed record ProductReferenceValidation(
    bool Available,
    bool CategoryActive,
    bool BaseUnitOfMeasureActive,
    bool TrackingDefaultEnabled)
{
    public bool IsValid => Available && CategoryActive && BaseUnitOfMeasureActive;

    public string Code => !Available
        ? "reference_persistence_unavailable"
        : !CategoryActive || !BaseUnitOfMeasureActive
            ? "product_reference_invalid"
            : "references_valid";

    public static ProductReferenceValidation Unavailable() => new(
        Available: false,
        CategoryActive: false,
        BaseUnitOfMeasureActive: false,
        TrackingDefaultEnabled: false);

    public static ProductReferenceValidation Invalid(
        bool categoryActive = false,
        bool baseUnitOfMeasureActive = false) => new(
            Available: true,
            CategoryActive: categoryActive,
            BaseUnitOfMeasureActive: baseUnitOfMeasureActive,
            TrackingDefaultEnabled: false);
}

public interface IProductIdentityPersistence
{
    Task<IReadOnlyList<ProductIdentityRecord>> ListProductsAsync(
        TenantContext tenantContext,
        CancellationToken cancellationToken = default);

    Task<ProductIdentityRecord?> FindProductAsync(
        TenantContext tenantContext,
        Guid productId,
        CancellationToken cancellationToken = default);

    Task<ProductReferenceValidation> ValidateReferencesAsync(
        TenantContext tenantContext,
        Guid categoryId,
        Guid baseUnitOfMeasureId,
        CancellationToken cancellationToken = default);

    Task<MasterDataPersistenceResult<ProductIdentityRecord>> CreateProductAsync(
        TenantContext tenantContext,
        Guid productId,
        CreateProductIdentityCommand command,
        ProductReferenceValidation references,
        MasterDataAuditEvidence evidence,
        CancellationToken cancellationToken = default);

    Task<MasterDataPersistenceResult<ProductIdentityRecord>> EditProductAsync(
        TenantContext tenantContext,
        EditProductIdentityCommand command,
        ProductReferenceValidation references,
        MasterDataAuditEvidence evidence,
        CancellationToken cancellationToken = default);

    Task<MasterDataPersistenceResult<ProductIdentityRecord>> SetProductLifecycleAsync(
        TenantContext tenantContext,
        Guid productId,
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
        Guid productId,
        CancellationToken cancellationToken = default);
}

public sealed record ProductIntegrityDecision(bool Allowed, string Code)
{
    public static ProductIntegrityDecision Success() => new(true, "integrity_allowed");

    public static ProductIntegrityDecision Denied(string code) => new(false, code);
}

/// <summary>
/// Downstream owners may replace this seam when an approved stock-reference
/// integrity rule exists. Product does not inspect or mutate downstream data.
/// </summary>
public interface IProductBaseUomChangePolicy
{
    ProductIntegrityDecision Evaluate(
        MasterDataRequestContext context,
        ProductIdentityRecord current,
        Guid requestedBaseUnitOfMeasureId);
}

public sealed class ProductBaseUomChangePolicyUnavailable : IProductBaseUomChangePolicy
{
    public ProductIntegrityDecision Evaluate(
        MasterDataRequestContext context,
        ProductIdentityRecord current,
        Guid requestedBaseUnitOfMeasureId)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(current);

        return current.BaseUnitOfMeasureId == requestedBaseUnitOfMeasureId
            ? ProductIntegrityDecision.Success()
            : ProductIntegrityDecision.Denied("base_uom_integrity_unavailable");
    }
}

/// <summary>
/// Safe composition fallback used when no provider-backed Product persistence
/// has been configured. It makes the host fail closed without creating a
/// database, schema, migration, or in-memory business store.
/// </summary>
public sealed class UnavailableProductIdentityPersistence : IProductIdentityPersistence
{
    public Task<IReadOnlyList<ProductIdentityRecord>> ListProductsAsync(
        TenantContext tenantContext,
        CancellationToken cancellationToken = default) => Unavailable<IReadOnlyList<ProductIdentityRecord>>();

    public Task<ProductIdentityRecord?> FindProductAsync(
        TenantContext tenantContext,
        Guid productId,
        CancellationToken cancellationToken = default) => Unavailable<ProductIdentityRecord?>();

    public Task<ProductReferenceValidation> ValidateReferencesAsync(
        TenantContext tenantContext,
        Guid categoryId,
        Guid baseUnitOfMeasureId,
        CancellationToken cancellationToken = default) => Unavailable<ProductReferenceValidation>();

    public Task<MasterDataPersistenceResult<ProductIdentityRecord>> CreateProductAsync(
        TenantContext tenantContext,
        Guid productId,
        CreateProductIdentityCommand command,
        ProductReferenceValidation references,
        MasterDataAuditEvidence evidence,
        CancellationToken cancellationToken = default) => Unavailable<MasterDataPersistenceResult<ProductIdentityRecord>>();

    public Task<MasterDataPersistenceResult<ProductIdentityRecord>> EditProductAsync(
        TenantContext tenantContext,
        EditProductIdentityCommand command,
        ProductReferenceValidation references,
        MasterDataAuditEvidence evidence,
        CancellationToken cancellationToken = default) => Unavailable<MasterDataPersistenceResult<ProductIdentityRecord>>();

    public Task<MasterDataPersistenceResult<ProductIdentityRecord>> SetProductLifecycleAsync(
        TenantContext tenantContext,
        Guid productId,
        MasterDataLifecycleState lifecycleState,
        byte[] expectedVersion,
        MasterDataAuditEvidence evidence,
        CancellationToken cancellationToken = default) => Unavailable<MasterDataPersistenceResult<ProductIdentityRecord>>();

    public Task<MasterDataPersistenceResult<MasterDataAuditRecord>> AppendAuditAsync(
        TenantContext tenantContext,
        MasterDataAuditEvidence evidence,
        CancellationToken cancellationToken = default) => Unavailable<MasterDataPersistenceResult<MasterDataAuditRecord>>();

    public Task<IReadOnlyList<MasterDataAuditRecord>> ReadAuditHistoryAsync(
        TenantContext tenantContext,
        Guid productId,
        CancellationToken cancellationToken = default) => Unavailable<IReadOnlyList<MasterDataAuditRecord>>();

    private static Task<T> Unavailable<T>() =>
        Task.FromException<T>(new InvalidOperationException("Product persistence is not configured."));
}

#pragma warning restore CS1591
