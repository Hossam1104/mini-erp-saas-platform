#pragma warning disable CS1591

namespace MiniErp.Contracts.Modules.MasterData;

/// <summary>
/// Product write payload. Tenant, scope, capability and approval facts are
/// intentionally absent; those values come from the trusted server context.
/// </summary>
public sealed record ProductIdentityWriteRequest(
    string? Sku,
    string? EnglishName,
    string? ArabicName,
    string? Description,
    Guid CategoryId,
    Guid BaseUnitOfMeasureId,
    IReadOnlyList<string>? Barcodes,
    bool? TrackingEnabledOverride,
    bool IsSellable,
    bool IsPurchasable,
    bool IsInventoryRelevant);

public sealed record ProductLifecycleRequest(string? Reason = null);

public sealed record ProductIdentityResponse(
    Guid Id,
    Guid TenantId,
    string Sku,
    string? EnglishName,
    string? ArabicName,
    string? Description,
    Guid CategoryId,
    Guid BaseUnitOfMeasureId,
    bool TrackingDefaultEnabled,
    bool? TrackingEnabledOverride,
    bool TrackingEnabled,
    bool IsSellable,
    bool IsPurchasable,
    bool IsInventoryRelevant,
    string LifecycleState,
    byte[] Version,
    IReadOnlyList<ProductBarcodeResponse> Barcodes);

public sealed record ProductBarcodeResponse(
    Guid Id,
    Guid ProductId,
    string Value,
    byte[] Version);

public sealed record ProductAuditResponse(
    Guid EvidenceId,
    DateTimeOffset OccurredAt,
    string OperationId,
    string CorrelationId,
    Guid TenantId,
    Guid ActorId,
    Guid SessionId,
    string AuthorizationPath,
    string Operation,
    string PolicyOutcome,
    string Decision,
    string Reason,
    string? BeforeSummary,
    string? AfterSummary,
    Guid? ApproverId);

#pragma warning restore CS1591
