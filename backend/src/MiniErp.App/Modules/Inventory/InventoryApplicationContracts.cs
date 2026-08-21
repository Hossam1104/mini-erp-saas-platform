#pragma warning disable CS1591

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MiniErp.App.BuildingBlocks.Rest;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.Modules.MasterData;
using MiniErp.Contracts.Modules.Foundation;
using MiniErp.Contracts.Modules.Inventory;

namespace MiniErp.App.Modules.Inventory;

public sealed record InventoryOperationResult<T>(bool Succeeded, string Code, T? Value)
{
    public static InventoryOperationResult<T> Success(T value) => new(true, "succeeded", value);

    public static InventoryOperationResult<T> Failure(string code) => new(false, code, default);
}

public enum InventoryPersistenceOutcome
{
    Succeeded = 1,
    NotFound = 2,
    Conflict = 3,
    InvalidState = 4,
    Duplicate = 5,
    Failure = 6
}

public sealed record InventoryPersistenceResult<T>(InventoryPersistenceOutcome Outcome, string Code, T? Value)
{
    public bool Succeeded => Outcome == InventoryPersistenceOutcome.Succeeded;

    public static InventoryPersistenceResult<T> Success(T value) =>
        new(InventoryPersistenceOutcome.Succeeded, "persisted", value);

    public static InventoryPersistenceResult<T> Denied(InventoryPersistenceOutcome outcome, string code) =>
        new(outcome, code, default);
}

public sealed class InventoryRequestContext
{
    private InventoryRequestContext(
        TenantId tenantId,
        TenantAuthorizationPath authorizationPath,
        ScopeReference? trustedScope,
        Guid actorId,
        Guid sessionId,
        CorrelationId? correlationId,
        FoundationRequestContext foundationContext)
    {
        TenantId = tenantId;
        AuthorizationPath = authorizationPath;
        TrustedScope = trustedScope;
        ActorId = actorId;
        SessionId = sessionId;
        CorrelationId = correlationId;
        FoundationContext = foundationContext;
    }

    public TenantId TenantId { get; }
    public TenantAuthorizationPath AuthorizationPath { get; }
    public ScopeReference? TrustedScope { get; }
    public Guid ActorId { get; }
    public Guid SessionId { get; }
    public CorrelationId? CorrelationId { get; }
    internal FoundationRequestContext FoundationContext { get; }
    public TenantContext TenantContext => FoundationContext.TenantContext
        ?? throw new InvalidOperationException("An Inventory context must carry a trusted Tenant context.");

    internal static InventoryRequestContext FromFoundationContext(FoundationRequestContext foundationContext)
    {
        ArgumentNullException.ThrowIfNull(foundationContext);
        if (foundationContext.SecurityProfile is not (
                FoundationSecurityProfile.OrdinaryMembership or FoundationSecurityProfile.SupportGrant)
            || foundationContext.TenantContext is null
            || foundationContext.PlatformGovernanceContext is not null
            || foundationContext.ActorId is not { } actorId
            || foundationContext.SessionId is not { } sessionId
            || actorId == Guid.Empty
            || sessionId == Guid.Empty)
        {
            throw new ArgumentException("A Tenant-bound Foundation context is required.", nameof(foundationContext));
        }

        var tenant = foundationContext.TenantContext;
        var expectedProfile = tenant.AuthorizationPath switch
        {
            TenantAuthorizationPath.OrdinaryMembership => FoundationSecurityProfile.OrdinaryMembership,
            TenantAuthorizationPath.SupportGrant => FoundationSecurityProfile.SupportGrant,
            _ => throw new ArgumentException("The Tenant authorization path is not supported.", nameof(foundationContext))
        };

        if (foundationContext.SecurityProfile != expectedProfile
            || tenant.ActorId is { } tenantActorId && tenantActorId != actorId)
        {
            throw new ArgumentException("The Foundation context contains inconsistent authorization facts.", nameof(foundationContext));
        }

        return new InventoryRequestContext(
            tenant.TenantId,
            tenant.AuthorizationPath,
            tenant.Scope,
            actorId,
            sessionId,
            tenant.CorrelationId,
            foundationContext);
    }
}

public sealed record InventoryContextResolution(bool Allowed, string Code, InventoryRequestContext? Context)
{
    public static InventoryContextResolution Success(InventoryRequestContext context) => new(true, "resolved", context);
    public static InventoryContextResolution Denied(string code) => new(false, code, null);
}

public sealed class InventoryTenantContextResolver
{
    public InventoryContextResolution Resolve(FoundationRequestContext trustedContext)
    {
        ArgumentNullException.ThrowIfNull(trustedContext);
        try
        {
            return InventoryContextResolution.Success(InventoryRequestContext.FromFoundationContext(trustedContext));
        }
        catch (ArgumentException)
        {
            return InventoryContextResolution.Denied("tenant_context_failed");
        }
    }
}

public sealed record InventoryScope(Guid TenantId, Guid CompanyId, Guid? BranchId, Guid WarehouseId)
{
    public string CanonicalReference => $"Warehouse:{WarehouseId:D}";
}

public sealed class InventoryResourceAuthorizationService
{
    public bool IsAllowed(InventoryRequestContext context, string operationId, InventoryScope scope)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(operationId);
        ArgumentNullException.ThrowIfNull(scope);

        if (scope.TenantId != context.TenantId.Value
            || !FoundationOperationCatalog.TryGet(operationId, out var descriptor)
            || descriptor.SecurityProfile is not FoundationSecurityProfile.OrdinaryMembership
                and not FoundationSecurityProfile.SupportGrant
            || descriptor.ExactPermissionCode is null
            || context.AuthorizationPath != (descriptor.SecurityProfile == FoundationSecurityProfile.OrdinaryMembership
                ? TenantAuthorizationPath.OrdinaryMembership
                : TenantAuthorizationPath.SupportGrant)
            || !string.Equals(context.FoundationContext.Permission, descriptor.ExactPermissionCode, StringComparison.Ordinal))
        {
            return false;
        }

        return ScopeAllows(context.TrustedScope, scope);
    }

    public bool IsAllowed(InventoryRequestContext context, string operationId, Guid tenantId, Guid companyId, Guid? branchId, Guid warehouseId) =>
        IsAllowed(context, operationId, new InventoryScope(tenantId, companyId, branchId, warehouseId));

    private static bool ScopeAllows(ScopeReference? trustedScope, InventoryScope target)
    {
        if (trustedScope is null)
        {
            return true;
        }

        var value = trustedScope.Value.Value;
        if (value.Equals($"Tenant:{target.TenantId:D}", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (value.StartsWith("Company:", StringComparison.OrdinalIgnoreCase)
            && Guid.TryParse(value["Company:".Length..], out var companyId))
        {
            return companyId == target.CompanyId;
        }

        if (value.StartsWith("Branch:", StringComparison.OrdinalIgnoreCase)
            && Guid.TryParse(value["Branch:".Length..], out var branchId))
        {
            return target.BranchId == branchId;
        }

        if (value.StartsWith("Warehouse:", StringComparison.OrdinalIgnoreCase)
            && Guid.TryParse(value["Warehouse:".Length..], out var warehouseId))
        {
            return target.WarehouseId == warehouseId;
        }

        return false;
    }
}

public interface IInventoryWarehouseProvider
{
    Task<IReadOnlyList<InventoryWarehouseOption>> ListAsync(
        InventoryRequestContext context,
        Guid? companyId = null,
        Guid? branchId = null,
        CancellationToken cancellationToken = default);

    Task<InventoryWarehouseOption?> FindAsync(
        InventoryRequestContext context,
        Guid warehouseId,
        CancellationToken cancellationToken = default);
}

public sealed class NoInventoryWarehouseProvider : IInventoryWarehouseProvider
{
    public Task<IReadOnlyList<InventoryWarehouseOption>> ListAsync(InventoryRequestContext context, Guid? companyId = null, Guid? branchId = null, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<InventoryWarehouseOption>>([]);

    public Task<InventoryWarehouseOption?> FindAsync(InventoryRequestContext context, Guid warehouseId, CancellationToken cancellationToken = default) =>
        Task.FromResult<InventoryWarehouseOption?>(null);
}

public sealed class ConfiguredInventoryWarehouseProvider : IInventoryWarehouseProvider
{
    private readonly IReadOnlyList<InventoryWarehouseOption> options;

    public ConfiguredInventoryWarehouseProvider(IEnumerable<InventoryWarehouseOption> options) =>
        this.options = options?.ToArray() ?? throw new ArgumentNullException(nameof(options));

    public Task<IReadOnlyList<InventoryWarehouseOption>> ListAsync(InventoryRequestContext context, Guid? companyId = null, Guid? branchId = null, CancellationToken cancellationToken = default)
    {
        var result = options.Where(item => item.TenantId == context.TenantId.Value && item.IsActive);
        if (companyId.HasValue) result = result.Where(item => item.CompanyId == companyId.Value);
        if (branchId.HasValue) result = result.Where(item => item.BranchId == branchId.Value);
        return Task.FromResult<IReadOnlyList<InventoryWarehouseOption>>(result.OrderBy(item => item.Code).ThenBy(item => item.Name).ToArray());
    }

    public Task<InventoryWarehouseOption?> FindAsync(InventoryRequestContext context, Guid warehouseId, CancellationToken cancellationToken = default) =>
        Task.FromResult(options.FirstOrDefault(item => item.TenantId == context.TenantId.Value && item.WarehouseId == warehouseId));
}

public interface IInventoryProductProvider
{
    Task<InventoryProductReference?> FindAsync(InventoryRequestContext context, Guid productId, CancellationToken cancellationToken = default);
}

public sealed class NoInventoryProductProvider : IInventoryProductProvider
{
    public Task<InventoryProductReference?> FindAsync(InventoryRequestContext context, Guid productId, CancellationToken cancellationToken = default) =>
        Task.FromResult<InventoryProductReference?>(null);
}

public sealed class MasterDataInventoryProductProvider(IProductIdentityPersistence products) : IInventoryProductProvider
{
    public async Task<InventoryProductReference?> FindAsync(InventoryRequestContext context, Guid productId, CancellationToken cancellationToken = default)
    {
        var product = await products.FindProductAsync(context.TenantContext, productId, cancellationToken);
        if (product is null)
        {
            return null;
        }

        return new InventoryProductReference(
            product.TenantId.Value,
            product.Id,
            product.Sku,
            product.Name.English ?? product.Name.Arabic ?? product.Sku,
            product.BaseUnitOfMeasureId,
            string.Empty,
            product.LifecycleState == Contracts.Modules.MasterData.MasterDataLifecycleState.Active,
            product.IsInventoryRelevant,
            product.TrackingEnabled);
    }
}

public sealed record InventoryOpeningBalanceCommand(
    Guid Id,
    InventoryScope Scope,
    string WarehouseCode,
    string WarehouseName,
    DateOnly AsOfDate,
    string SourceOwner,
    string SourceSystem,
    DateTimeOffset ExtractedAt,
    string? SourceReference,
    IReadOnlyList<InventoryOpeningBalanceRowCommand> Rows,
    Guid ActorId,
    DateTimeOffset OccurredAt,
    string CorrelationId,
    string? IdempotencyKey,
    string RequestFingerprint);

public sealed record InventoryOpeningBalanceRowCommand(
    Guid Id,
    Guid ProductId,
    Guid UnitOfMeasureId,
    decimal Quantity,
    decimal UnitCost,
    string CurrencyCode,
    string? TrackingIdentity,
    string? SourceLineReference,
    InventoryProductReference? Product,
    string? ValidationCode);

public sealed record InventoryReservationCommand(
    Guid Id,
    InventoryScope Scope,
    Guid ProductId,
    Guid UnitOfMeasureId,
    decimal RequestedQuantity,
    string SourceType,
    string SourceReference,
    bool AllowPartialAllocation,
    string? TrackingIdentity,
    InventoryProductReference Product,
    string WarehouseCode,
    string WarehouseName,
    Guid ActorId,
    DateTimeOffset OccurredAt,
    string CorrelationId,
    string? IdempotencyKey,
    string RequestFingerprint);

public sealed record InventoryReplayRecord(string ResourceType, Guid ResourceId, string Fingerprint, string SnapshotJson);

public interface IInventoryPersistence
{
    Task<IReadOnlyList<InventoryMovementRecord>> ListMovementsAsync(InventoryRequestContext context, InventoryScope? scope = null, Guid? productId = null, CancellationToken cancellationToken = default);
    Task<InventoryMovementRecord?> FindMovementAsync(InventoryRequestContext context, Guid movementId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InventoryOpeningBalanceRecord>> ListOpeningBalancesAsync(InventoryRequestContext context, InventoryScope? scope = null, CancellationToken cancellationToken = default);
    Task<InventoryOpeningBalanceRecord?> FindOpeningBalanceAsync(InventoryRequestContext context, Guid id, CancellationToken cancellationToken = default);
    Task<InventoryOpeningBalanceRecord?> CreateOpeningBalanceAsync(InventoryRequestContext context, InventoryOpeningBalanceCommand command, CancellationToken cancellationToken = default);
    Task<InventoryOpeningBalanceRecord?> ValidateOpeningBalanceAsync(InventoryRequestContext context, Guid id, byte[] expectedVersion, Guid actorId, string? reason, string correlationId, string? idempotencyKey, string fingerprint, CancellationToken cancellationToken = default);
    Task<InventoryOpeningBalanceRecord?> PostOpeningBalanceAsync(InventoryRequestContext context, Guid id, byte[] expectedVersion, Guid actorId, string? reason, string correlationId, string? idempotencyKey, string fingerprint, CancellationToken cancellationToken = default);
    Task<InventoryOpeningBalanceRecord?> CorrectOpeningBalanceAsync(InventoryRequestContext context, Guid id, byte[] expectedVersion, Guid actorId, string? reason, string correlationId, string? idempotencyKey, string fingerprint, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InventoryOpeningBalanceHistoryRecord>> ReadOpeningHistoryAsync(InventoryRequestContext context, Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InventoryReservationRecord>> ListReservationsAsync(InventoryRequestContext context, InventoryScope? scope = null, Guid? productId = null, CancellationToken cancellationToken = default);
    Task<InventoryReservationRecord?> FindReservationAsync(InventoryRequestContext context, Guid id, CancellationToken cancellationToken = default);
    Task<InventoryReservationRecord?> CreateReservationAsync(InventoryRequestContext context, InventoryReservationCommand command, decimal availableQuantity, CancellationToken cancellationToken = default);
    Task<InventoryReservationRecord?> ReduceReservationAsync(InventoryRequestContext context, Guid id, byte[] expectedVersion, decimal quantity, Guid actorId, string? reason, string correlationId, string? idempotencyKey, string fingerprint, CancellationToken cancellationToken = default);
    Task<InventoryReservationRecord?> ReleaseReservationAsync(InventoryRequestContext context, Guid id, byte[] expectedVersion, Guid actorId, string? reason, string correlationId, string? idempotencyKey, string fingerprint, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InventoryReservationHistoryRecord>> ReadReservationHistoryAsync(InventoryRequestContext context, Guid id, CancellationToken cancellationToken = default);
    Task<InventoryAvailabilityRecord?> GetAvailabilityAsync(InventoryRequestContext context, InventoryScope scope, Guid productId, Guid unitOfMeasureId, string? trackingIdentity, InventoryProductReference product, InventoryWarehouseOption warehouse, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InventoryAuditRecord>> ReadAuditAsync(InventoryRequestContext context, string resourceType, Guid resourceId, CancellationToken cancellationToken = default);
}

public sealed class UnavailableInventoryPersistence : IInventoryPersistence
{
    private static Task<T> Unavailable<T>() => Task.FromException<T>(new InvalidOperationException("Inventory persistence is unavailable."));
    public Task<IReadOnlyList<InventoryMovementRecord>> ListMovementsAsync(InventoryRequestContext context, InventoryScope? scope = null, Guid? productId = null, CancellationToken cancellationToken = default) => Unavailable<IReadOnlyList<InventoryMovementRecord>>();
    public Task<InventoryMovementRecord?> FindMovementAsync(InventoryRequestContext context, Guid movementId, CancellationToken cancellationToken = default) => Unavailable<InventoryMovementRecord?>();
    public Task<IReadOnlyList<InventoryOpeningBalanceRecord>> ListOpeningBalancesAsync(InventoryRequestContext context, InventoryScope? scope = null, CancellationToken cancellationToken = default) => Unavailable<IReadOnlyList<InventoryOpeningBalanceRecord>>();
    public Task<InventoryOpeningBalanceRecord?> FindOpeningBalanceAsync(InventoryRequestContext context, Guid id, CancellationToken cancellationToken = default) => Unavailable<InventoryOpeningBalanceRecord?>();
    public Task<InventoryOpeningBalanceRecord?> CreateOpeningBalanceAsync(InventoryRequestContext context, InventoryOpeningBalanceCommand command, CancellationToken cancellationToken = default) => Unavailable<InventoryOpeningBalanceRecord?>();
    public Task<InventoryOpeningBalanceRecord?> ValidateOpeningBalanceAsync(InventoryRequestContext context, Guid id, byte[] expectedVersion, Guid actorId, string? reason, string correlationId, string? idempotencyKey, string fingerprint, CancellationToken cancellationToken = default) => Unavailable<InventoryOpeningBalanceRecord?>();
    public Task<InventoryOpeningBalanceRecord?> PostOpeningBalanceAsync(InventoryRequestContext context, Guid id, byte[] expectedVersion, Guid actorId, string? reason, string correlationId, string? idempotencyKey, string fingerprint, CancellationToken cancellationToken = default) => Unavailable<InventoryOpeningBalanceRecord?>();
    public Task<InventoryOpeningBalanceRecord?> CorrectOpeningBalanceAsync(InventoryRequestContext context, Guid id, byte[] expectedVersion, Guid actorId, string? reason, string correlationId, string? idempotencyKey, string fingerprint, CancellationToken cancellationToken = default) => Unavailable<InventoryOpeningBalanceRecord?>();
    public Task<IReadOnlyList<InventoryOpeningBalanceHistoryRecord>> ReadOpeningHistoryAsync(InventoryRequestContext context, Guid id, CancellationToken cancellationToken = default) => Unavailable<IReadOnlyList<InventoryOpeningBalanceHistoryRecord>>();
    public Task<IReadOnlyList<InventoryReservationRecord>> ListReservationsAsync(InventoryRequestContext context, InventoryScope? scope = null, Guid? productId = null, CancellationToken cancellationToken = default) => Unavailable<IReadOnlyList<InventoryReservationRecord>>();
    public Task<InventoryReservationRecord?> FindReservationAsync(InventoryRequestContext context, Guid id, CancellationToken cancellationToken = default) => Unavailable<InventoryReservationRecord?>();
    public Task<InventoryReservationRecord?> CreateReservationAsync(InventoryRequestContext context, InventoryReservationCommand command, decimal availableQuantity, CancellationToken cancellationToken = default) => Unavailable<InventoryReservationRecord?>();
    public Task<InventoryReservationRecord?> ReduceReservationAsync(InventoryRequestContext context, Guid id, byte[] expectedVersion, decimal quantity, Guid actorId, string? reason, string correlationId, string? idempotencyKey, string fingerprint, CancellationToken cancellationToken = default) => Unavailable<InventoryReservationRecord?>();
    public Task<InventoryReservationRecord?> ReleaseReservationAsync(InventoryRequestContext context, Guid id, byte[] expectedVersion, Guid actorId, string? reason, string correlationId, string? idempotencyKey, string fingerprint, CancellationToken cancellationToken = default) => Unavailable<InventoryReservationRecord?>();
    public Task<IReadOnlyList<InventoryReservationHistoryRecord>> ReadReservationHistoryAsync(InventoryRequestContext context, Guid id, CancellationToken cancellationToken = default) => Unavailable<IReadOnlyList<InventoryReservationHistoryRecord>>();
    public Task<InventoryAvailabilityRecord?> GetAvailabilityAsync(InventoryRequestContext context, InventoryScope scope, Guid productId, Guid unitOfMeasureId, string? trackingIdentity, InventoryProductReference product, InventoryWarehouseOption warehouse, CancellationToken cancellationToken = default) => Unavailable<InventoryAvailabilityRecord?>();
    public Task<IReadOnlyList<InventoryAuditRecord>> ReadAuditAsync(InventoryRequestContext context, string resourceType, Guid resourceId, CancellationToken cancellationToken = default) => Unavailable<IReadOnlyList<InventoryAuditRecord>>();
}

internal static class InventoryFingerprints
{
    internal static string Create<T>(T value)
    {
        var json = JsonSerializer.Serialize(value, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase });
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(json)));
    }
}

#pragma warning restore CS1591
