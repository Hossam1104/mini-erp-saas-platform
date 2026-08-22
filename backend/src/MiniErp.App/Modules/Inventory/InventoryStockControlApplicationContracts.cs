#pragma warning disable CS1591

using System.Text.Json;
using MiniErp.App.Modules.Procurement;
using MiniErp.Contracts.Modules.Inventory;
using MiniErp.Contracts.Modules.Procurement;

namespace MiniErp.App.Modules.Inventory;

public sealed record InventoryReasonCodeCommand(
    Guid Id,
    string Code,
    string EnglishName,
    string ArabicName,
    InventoryReasonCategory Category,
    Guid ActorId,
    DateTimeOffset OccurredAt,
    string CorrelationId,
    string? IdempotencyKey,
    string RequestFingerprint);

public sealed record InventoryReasonCodeUpdateCommand(
    Guid Id,
    byte[] ExpectedVersion,
    string EnglishName,
    string ArabicName,
    InventoryReasonCategory Category,
    bool IsActive,
    Guid ActorId,
    DateTimeOffset OccurredAt,
    string CorrelationId,
    string? IdempotencyKey,
    string RequestFingerprint);

public sealed record InventoryApprovalPolicyBinding(
    InventoryScope Scope,
    string DocumentType,
    PurchaseRequestApprovalPolicyDefinition Definition);

public interface IInventoryApprovalPolicyProvider
{
    Task<PurchaseRequestApprovalPolicyDefinition?> ResolveAsync(
        InventoryRequestContext context,
        InventoryScope scope,
        string documentType,
        DateTimeOffset at,
        CancellationToken cancellationToken = default);
}

public sealed class NoInventoryApprovalPolicyProvider : IInventoryApprovalPolicyProvider
{
    public Task<PurchaseRequestApprovalPolicyDefinition?> ResolveAsync(
        InventoryRequestContext context,
        InventoryScope scope,
        string documentType,
        DateTimeOffset at,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<PurchaseRequestApprovalPolicyDefinition?>(null);
}

public sealed class ConfiguredInventoryApprovalPolicyProvider : IInventoryApprovalPolicyProvider
{
    private readonly IReadOnlyList<InventoryApprovalPolicyBinding> bindings;

    public ConfiguredInventoryApprovalPolicyProvider(IEnumerable<InventoryApprovalPolicyBinding> bindings)
    {
        ArgumentNullException.ThrowIfNull(bindings);
        this.bindings = bindings.ToArray();
    }

    public Task<PurchaseRequestApprovalPolicyDefinition?> ResolveAsync(
        InventoryRequestContext context,
        InventoryScope scope,
        string documentType,
        DateTimeOffset at,
        CancellationToken cancellationToken = default)
    {
        var selected = bindings
            .Where(item => item.Scope.TenantId == scope.TenantId
                && item.Scope.CompanyId == scope.CompanyId
                && item.Scope.BranchId == scope.BranchId
                && (item.Scope.WarehouseId == Guid.Empty || item.Scope.WarehouseId == scope.WarehouseId)
                && string.Equals(item.DocumentType, documentType, StringComparison.Ordinal)
                && item.Definition.EffectiveFrom <= at
                && (item.Definition.EffectiveTo is null || item.Definition.EffectiveTo > at))
            .OrderByDescending(item => item.Definition.Version)
            .ThenBy(item => item.Definition.PolicyId, StringComparer.Ordinal)
            .FirstOrDefault();
        return Task.FromResult(selected?.Definition);
    }
}

public interface IInventoryApprovalDelegationProvider
{
    Task<PurchaseRequestApprovalDelegation?> ResolveAsync(
        InventoryRequestContext context,
        InventoryScope scope,
        PurchaseRequestApprovalStageDefinition stage,
        Guid actorId,
        DateTimeOffset at,
        CancellationToken cancellationToken = default);
}

public sealed class NoInventoryApprovalDelegationProvider : IInventoryApprovalDelegationProvider
{
    public Task<PurchaseRequestApprovalDelegation?> ResolveAsync(
        InventoryRequestContext context,
        InventoryScope scope,
        PurchaseRequestApprovalStageDefinition stage,
        Guid actorId,
        DateTimeOffset at,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<PurchaseRequestApprovalDelegation?>(null);
}

public sealed class ConfiguredInventoryApprovalDelegationProvider : IInventoryApprovalDelegationProvider
{
    private readonly IReadOnlyList<PurchaseRequestApprovalDelegation> delegations;

    public ConfiguredInventoryApprovalDelegationProvider(IEnumerable<PurchaseRequestApprovalDelegation> delegations)
    {
        ArgumentNullException.ThrowIfNull(delegations);
        this.delegations = delegations.ToArray();
    }

    public Task<PurchaseRequestApprovalDelegation?> ResolveAsync(
        InventoryRequestContext context,
        InventoryScope scope,
        PurchaseRequestApprovalStageDefinition stage,
        Guid actorId,
        DateTimeOffset at,
        CancellationToken cancellationToken = default)
    {
        var eligible = (stage.EligibleApproverIds ?? []).ToHashSet();
        var selected = delegations
            .Where(item => item.TenantId == scope.TenantId
                && item.CompanyId == scope.CompanyId
                && item.BranchId == scope.BranchId
                && string.Equals(item.StageKey, stage.StageKey, StringComparison.Ordinal)
                && item.DelegateeId == actorId
                && item.DelegatorId != actorId
                && item.StartsAt <= at
                && item.ExpiresAt > at
                && (eligible.Count == 0 || eligible.Contains(item.DelegatorId)))
            .OrderBy(item => item.ExpiresAt)
            .ThenBy(item => item.DelegatorId)
            .FirstOrDefault();
        return Task.FromResult(selected);
    }
}

public sealed record InventoryAdjustmentLineCommand(
    Guid Id,
    Guid ProductId,
    Guid UnitOfMeasureId,
    InventoryAdjustmentDirection Direction,
    decimal Quantity,
    string TrackingIdentity,
    string? EvidenceReference,
    InventoryProductReference Product,
    InventoryReasonCodeRecord Reason);

public sealed record InventoryAdjustmentCreateCommand(
    Guid Id,
    InventoryScope Scope,
    string WarehouseCode,
    string WarehouseName,
    string? EvidenceReference,
    IReadOnlyList<InventoryAdjustmentLineCommand> Lines,
    Guid ActorId,
    DateTimeOffset OccurredAt,
    string CorrelationId,
    string? IdempotencyKey,
    string RequestFingerprint);

public sealed record InventoryControlActionCommand(
    Guid Id,
    byte[] ExpectedVersion,
    Guid ActorId,
    string? Reason,
    Guid? DelegatedFromActorId,
    string CorrelationId,
    string? IdempotencyKey,
    string RequestFingerprint,
    DateTimeOffset OccurredAt);

public sealed record InventoryCountLineCommand(
    Guid Id,
    Guid? PriorLineId,
    int RoundGeneration,
    Guid ProductId,
    Guid UnitOfMeasureId,
    string TrackingIdentity,
    decimal ExpectedQuantity,
    InventoryProductReference Product);

public sealed record InventoryCountCreateCommand(
    Guid Id,
    InventoryScope Scope,
    string WarehouseCode,
    string WarehouseName,
    InventoryCountType CountType,
    Guid AssignedCounterId,
    Guid? ReviewerId,
    IReadOnlyList<InventoryCountLineCommand> Lines,
    DateTimeOffset SnapshotCutoff,
    Guid ActorId,
    DateTimeOffset OccurredAt,
    string CorrelationId,
    string? IdempotencyKey,
    string RequestFingerprint);

public sealed record InventoryCountSubmitCommand(
    Guid Id,
    byte[] ExpectedVersion,
    IReadOnlyList<InventoryCountObservationRequest> Observations,
    Guid ActorId,
    string? IdempotencyKey,
    string RequestFingerprint,
    string CorrelationId,
    DateTimeOffset OccurredAt);

public sealed record InventoryStockIssueLineCommand(
    Guid Id,
    Guid ProductId,
    Guid UnitOfMeasureId,
    decimal Quantity,
    string TrackingIdentity,
    string? EvidenceReference,
    InventoryProductReference Product,
    InventoryReasonCodeRecord Reason);

public sealed record InventoryStockIssueCreateCommand(
    Guid Id,
    InventoryScope Scope,
    string WarehouseCode,
    string WarehouseName,
    string DestinationUseDescription,
    IReadOnlyList<InventoryStockIssueLineCommand> Lines,
    Guid ActorId,
    DateTimeOffset OccurredAt,
    string CorrelationId,
    string? IdempotencyKey,
    string RequestFingerprint);

public sealed record InventoryMovementCorrectionCommand(
    Guid MovementId,
    byte[] ExpectedVersion,
    Guid ActorId,
    Guid ReasonCodeId,
    string ReasonCode,
    string ReasonEnglishName,
    string ReasonArabicName,
    string? Reason,
    string CorrelationId,
    string? IdempotencyKey,
    string RequestFingerprint,
    DateTimeOffset OccurredAt);

public partial interface IInventoryPersistence
{
    Task<IReadOnlyList<InventoryReasonCodeRecord>> ListReasonCodesAsync(InventoryRequestContext context, InventoryReasonCategory? category = null, bool includeInactive = false, CancellationToken cancellationToken = default);
    Task<InventoryReasonCodeRecord?> FindReasonCodeAsync(InventoryRequestContext context, Guid id, CancellationToken cancellationToken = default);
    Task<InventoryReasonCodeRecord?> CreateReasonCodeAsync(InventoryRequestContext context, InventoryReasonCodeCommand command, CancellationToken cancellationToken = default);
    Task<InventoryReasonCodeRecord?> UpdateReasonCodeAsync(InventoryRequestContext context, InventoryReasonCodeUpdateCommand command, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InventoryAdjustmentRecord>> ListAdjustmentsAsync(InventoryRequestContext context, InventoryScope? scope = null, CancellationToken cancellationToken = default);
    Task<InventoryAdjustmentRecord?> FindAdjustmentAsync(InventoryRequestContext context, Guid id, CancellationToken cancellationToken = default);
    Task<InventoryAdjustmentRecord?> CreateAdjustmentAsync(InventoryRequestContext context, InventoryAdjustmentCreateCommand command, CancellationToken cancellationToken = default);
    Task<InventoryAdjustmentRecord?> SubmitAdjustmentAsync(InventoryRequestContext context, InventoryControlActionCommand command, bool requiresApproval, string? policyJson, CancellationToken cancellationToken = default);
    Task<InventoryAdjustmentRecord?> ApproveAdjustmentAsync(InventoryRequestContext context, InventoryControlActionCommand command, CancellationToken cancellationToken = default);
    Task<InventoryAdjustmentRecord?> RejectAdjustmentAsync(InventoryRequestContext context, InventoryControlActionCommand command, bool returnForChange, CancellationToken cancellationToken = default);
    Task<InventoryAdjustmentRecord?> PostAdjustmentAsync(InventoryRequestContext context, InventoryControlActionCommand command, CancellationToken cancellationToken = default);
    Task<InventoryCountRecord?> CreateCountAsync(InventoryRequestContext context, InventoryCountCreateCommand command, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InventoryCountRecord>> ListCountsAsync(InventoryRequestContext context, InventoryScope? scope = null, CancellationToken cancellationToken = default);
    Task<InventoryCountRecord?> FindCountAsync(InventoryRequestContext context, Guid id, bool includeExpected, CancellationToken cancellationToken = default);
    Task<InventoryCountRecord?> SubmitCountAsync(InventoryRequestContext context, InventoryCountSubmitCommand command, CancellationToken cancellationToken = default);
    Task<InventoryCountRecord?> ApproveCountAsync(InventoryRequestContext context, InventoryControlActionCommand command, CancellationToken cancellationToken = default);
    Task<InventoryCountRecord?> RejectCountAsync(InventoryRequestContext context, InventoryControlActionCommand command, bool returnForChange, CancellationToken cancellationToken = default);
    Task<InventoryCountRecord?> RequestCountRecountAsync(InventoryRequestContext context, InventoryControlActionCommand command, CancellationToken cancellationToken = default);
    Task<InventoryCountRecord?> ResnapshotCountAsync(InventoryRequestContext context, InventoryControlActionCommand command, CancellationToken cancellationToken = default);
    Task<InventoryCountRecord?> PostCountAsync(InventoryRequestContext context, InventoryControlActionCommand command, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InventoryControlHistoryRecord>> ReadControlHistoryAsync(InventoryRequestContext context, string resourceType, Guid resourceId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<InventoryStockIssueRecord>> ListStockIssuesAsync(InventoryRequestContext context, InventoryScope? scope = null, CancellationToken cancellationToken = default);
    Task<InventoryStockIssueRecord?> FindStockIssueAsync(InventoryRequestContext context, Guid id, CancellationToken cancellationToken = default);
    Task<InventoryStockIssueRecord?> CreateStockIssueAsync(InventoryRequestContext context, InventoryStockIssueCreateCommand command, CancellationToken cancellationToken = default);
    Task<InventoryStockIssueRecord?> SubmitStockIssueAsync(InventoryRequestContext context, InventoryControlActionCommand command, bool requiresApproval, string? policyJson, CancellationToken cancellationToken = default);
    Task<InventoryStockIssueRecord?> ApproveStockIssueAsync(InventoryRequestContext context, InventoryControlActionCommand command, CancellationToken cancellationToken = default);
    Task<InventoryStockIssueRecord?> RejectStockIssueAsync(InventoryRequestContext context, InventoryControlActionCommand command, bool returnForChange, CancellationToken cancellationToken = default);
    Task<InventoryStockIssueRecord?> PostStockIssueAsync(InventoryRequestContext context, InventoryControlActionCommand command, CancellationToken cancellationToken = default);
    Task<InventoryMovementRecord?> CorrectMovementAsync(InventoryRequestContext context, InventoryMovementCorrectionCommand command, CancellationToken cancellationToken = default);
}

public sealed partial class UnavailableInventoryPersistence
{
    private static Task<T> ControlUnavailable<T>() => Task.FromException<T>(new InvalidOperationException("Inventory persistence is unavailable."));
    public Task<IReadOnlyList<InventoryReasonCodeRecord>> ListReasonCodesAsync(InventoryRequestContext context, InventoryReasonCategory? category = null, bool includeInactive = false, CancellationToken cancellationToken = default) => ControlUnavailable<IReadOnlyList<InventoryReasonCodeRecord>>();
    public Task<InventoryReasonCodeRecord?> FindReasonCodeAsync(InventoryRequestContext context, Guid id, CancellationToken cancellationToken = default) => ControlUnavailable<InventoryReasonCodeRecord?>();
    public Task<InventoryReasonCodeRecord?> CreateReasonCodeAsync(InventoryRequestContext context, InventoryReasonCodeCommand command, CancellationToken cancellationToken = default) => ControlUnavailable<InventoryReasonCodeRecord?>();
    public Task<InventoryReasonCodeRecord?> UpdateReasonCodeAsync(InventoryRequestContext context, InventoryReasonCodeUpdateCommand command, CancellationToken cancellationToken = default) => ControlUnavailable<InventoryReasonCodeRecord?>();
    public Task<IReadOnlyList<InventoryAdjustmentRecord>> ListAdjustmentsAsync(InventoryRequestContext context, InventoryScope? scope = null, CancellationToken cancellationToken = default) => ControlUnavailable<IReadOnlyList<InventoryAdjustmentRecord>>();
    public Task<InventoryAdjustmentRecord?> FindAdjustmentAsync(InventoryRequestContext context, Guid id, CancellationToken cancellationToken = default) => ControlUnavailable<InventoryAdjustmentRecord?>();
    public Task<InventoryAdjustmentRecord?> CreateAdjustmentAsync(InventoryRequestContext context, InventoryAdjustmentCreateCommand command, CancellationToken cancellationToken = default) => ControlUnavailable<InventoryAdjustmentRecord?>();
    public Task<InventoryAdjustmentRecord?> SubmitAdjustmentAsync(InventoryRequestContext context, InventoryControlActionCommand command, bool requiresApproval, string? policyJson, CancellationToken cancellationToken = default) => ControlUnavailable<InventoryAdjustmentRecord?>();
    public Task<InventoryAdjustmentRecord?> ApproveAdjustmentAsync(InventoryRequestContext context, InventoryControlActionCommand command, CancellationToken cancellationToken = default) => ControlUnavailable<InventoryAdjustmentRecord?>();
    public Task<InventoryAdjustmentRecord?> RejectAdjustmentAsync(InventoryRequestContext context, InventoryControlActionCommand command, bool returnForChange, CancellationToken cancellationToken = default) => ControlUnavailable<InventoryAdjustmentRecord?>();
    public Task<InventoryAdjustmentRecord?> PostAdjustmentAsync(InventoryRequestContext context, InventoryControlActionCommand command, CancellationToken cancellationToken = default) => ControlUnavailable<InventoryAdjustmentRecord?>();
    public Task<InventoryCountRecord?> CreateCountAsync(InventoryRequestContext context, InventoryCountCreateCommand command, CancellationToken cancellationToken = default) => ControlUnavailable<InventoryCountRecord?>();
    public Task<IReadOnlyList<InventoryCountRecord>> ListCountsAsync(InventoryRequestContext context, InventoryScope? scope = null, CancellationToken cancellationToken = default) => ControlUnavailable<IReadOnlyList<InventoryCountRecord>>();
    public Task<InventoryCountRecord?> FindCountAsync(InventoryRequestContext context, Guid id, bool includeExpected, CancellationToken cancellationToken = default) => ControlUnavailable<InventoryCountRecord?>();
    public Task<InventoryCountRecord?> SubmitCountAsync(InventoryRequestContext context, InventoryCountSubmitCommand command, CancellationToken cancellationToken = default) => ControlUnavailable<InventoryCountRecord?>();
    public Task<InventoryCountRecord?> ApproveCountAsync(InventoryRequestContext context, InventoryControlActionCommand command, CancellationToken cancellationToken = default) => ControlUnavailable<InventoryCountRecord?>();
    public Task<InventoryCountRecord?> RejectCountAsync(InventoryRequestContext context, InventoryControlActionCommand command, bool returnForChange, CancellationToken cancellationToken = default) => ControlUnavailable<InventoryCountRecord?>();
    public Task<InventoryCountRecord?> RequestCountRecountAsync(InventoryRequestContext context, InventoryControlActionCommand command, CancellationToken cancellationToken = default) => ControlUnavailable<InventoryCountRecord?>();
    public Task<InventoryCountRecord?> ResnapshotCountAsync(InventoryRequestContext context, InventoryControlActionCommand command, CancellationToken cancellationToken = default) => ControlUnavailable<InventoryCountRecord?>();
    public Task<InventoryCountRecord?> PostCountAsync(InventoryRequestContext context, InventoryControlActionCommand command, CancellationToken cancellationToken = default) => ControlUnavailable<InventoryCountRecord?>();
    public Task<IReadOnlyList<InventoryControlHistoryRecord>> ReadControlHistoryAsync(InventoryRequestContext context, string resourceType, Guid resourceId, CancellationToken cancellationToken = default) => ControlUnavailable<IReadOnlyList<InventoryControlHistoryRecord>>();
    public Task<IReadOnlyList<InventoryStockIssueRecord>> ListStockIssuesAsync(InventoryRequestContext context, InventoryScope? scope = null, CancellationToken cancellationToken = default) => ControlUnavailable<IReadOnlyList<InventoryStockIssueRecord>>();
    public Task<InventoryStockIssueRecord?> FindStockIssueAsync(InventoryRequestContext context, Guid id, CancellationToken cancellationToken = default) => ControlUnavailable<InventoryStockIssueRecord?>();
    public Task<InventoryStockIssueRecord?> CreateStockIssueAsync(InventoryRequestContext context, InventoryStockIssueCreateCommand command, CancellationToken cancellationToken = default) => ControlUnavailable<InventoryStockIssueRecord?>();
    public Task<InventoryStockIssueRecord?> SubmitStockIssueAsync(InventoryRequestContext context, InventoryControlActionCommand command, bool requiresApproval, string? policyJson, CancellationToken cancellationToken = default) => ControlUnavailable<InventoryStockIssueRecord?>();
    public Task<InventoryStockIssueRecord?> ApproveStockIssueAsync(InventoryRequestContext context, InventoryControlActionCommand command, CancellationToken cancellationToken = default) => ControlUnavailable<InventoryStockIssueRecord?>();
    public Task<InventoryStockIssueRecord?> RejectStockIssueAsync(InventoryRequestContext context, InventoryControlActionCommand command, bool returnForChange, CancellationToken cancellationToken = default) => ControlUnavailable<InventoryStockIssueRecord?>();
    public Task<InventoryStockIssueRecord?> PostStockIssueAsync(InventoryRequestContext context, InventoryControlActionCommand command, CancellationToken cancellationToken = default) => ControlUnavailable<InventoryStockIssueRecord?>();
    public Task<InventoryMovementRecord?> CorrectMovementAsync(InventoryRequestContext context, InventoryMovementCorrectionCommand command, CancellationToken cancellationToken = default) => ControlUnavailable<InventoryMovementRecord?>();
}

public static class InventoryControlJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web);
}

#pragma warning restore CS1591
