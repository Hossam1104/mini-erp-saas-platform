#pragma warning disable CS1591

using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.Contracts.Modules.Inventory;

namespace MiniErp.App.Modules.Inventory;

public sealed record InventoryValuationPolicyCommand(
    Guid Id,
    InventoryValuationPolicyRequest Request,
    Guid ActorId,
    DateTimeOffset OccurredAt,
    string CorrelationId,
    string? IdempotencyKey,
    string RequestFingerprint);

public sealed record InventoryValuationProcessCommand(
    Guid CompanyId,
    Guid? BranchId,
    Guid? WarehouseId,
    Guid? ProductId,
    Guid? UnitOfMeasureId,
    string? TrackingIdentity,
    DateOnly? AsOfDate,
    Guid ActorId,
    DateTimeOffset OccurredAt,
    string CorrelationId,
    string? IdempotencyKey,
    string RequestFingerprint);

public sealed record InventoryValuationCorrectionCommand(
    Guid OriginalValuationEventId,
    Guid AuthoritativeSourceRevisionId,
    string Reason,
    Guid ActorId,
    DateTimeOffset OccurredAt,
    string CorrelationId,
    string? IdempotencyKey,
    string RequestFingerprint);

public sealed record InventoryValuationQuery(
    Guid CompanyId,
    Guid? BranchId = null,
    Guid? WarehouseId = null,
    Guid? ProductId = null,
    Guid? UnitOfMeasureId = null,
    string? TrackingIdentity = null,
    InventoryValuationEventStatus? Status = null,
    long? FromLedgerSequence = null,
    long? ToLedgerSequence = null,
    DateOnly? EffectiveFrom = null,
    DateOnly? EffectiveTo = null,
    InventoryMovementSourceType? SourceType = null,
    Guid? PolicyId = null,
    string? FunctionalCurrencyCode = null);

public interface IInventoryValuationPersistence
{
    Task<InventoryPersistenceResult<InventoryValuationPolicyRecord>> CreatePolicyAsync(
        InventoryRequestContext context,
        InventoryValuationPolicyCommand command,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<InventoryValuationPolicyRecord>> ListPoliciesAsync(
        InventoryRequestContext context,
        Guid companyId,
        CancellationToken cancellationToken = default);

    Task<InventoryValuationProcessResult> ProcessAsync(
        InventoryRequestContext context,
        InventoryValuationProcessCommand command,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<InventoryValuationStateRecord>> ListStatesAsync(
        InventoryRequestContext context,
        InventoryValuationQuery query,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<InventoryMovementValuationEventRecord>> ListEventsAsync(
        InventoryRequestContext context,
        InventoryValuationQuery query,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<InventoryValuationReconciliationRecord>> ReconcileAsync(
        InventoryRequestContext context,
        InventoryValuationQuery query,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<InventoryFinanceValuationHandoffRecord>> ListFinanceHandoffsAsync(
        InventoryRequestContext context,
        InventoryValuationQuery query,
        CancellationToken cancellationToken = default);

    Task<InventoryValuationExportRecord> ExportAsync(
        InventoryRequestContext context,
        InventoryValuationQuery query,
        CancellationToken cancellationToken = default);

    Task<InventoryPersistenceResult<InventoryMovementValuationEventRecord>> CorrectAsync(
        InventoryRequestContext context,
        InventoryValuationCorrectionCommand command,
        CancellationToken cancellationToken = default);
}

public sealed class UnavailableInventoryValuationPersistence : IInventoryValuationPersistence
{
    private static Task<T> Unavailable<T>() => Task.FromException<T>(new InvalidOperationException("Inventory valuation persistence is unavailable."));

    public Task<InventoryPersistenceResult<InventoryValuationPolicyRecord>> CreatePolicyAsync(InventoryRequestContext context, InventoryValuationPolicyCommand command, CancellationToken cancellationToken = default) => Unavailable<InventoryPersistenceResult<InventoryValuationPolicyRecord>>();
    public Task<IReadOnlyList<InventoryValuationPolicyRecord>> ListPoliciesAsync(InventoryRequestContext context, Guid companyId, CancellationToken cancellationToken = default) => Unavailable<IReadOnlyList<InventoryValuationPolicyRecord>>();
    public Task<InventoryValuationProcessResult> ProcessAsync(InventoryRequestContext context, InventoryValuationProcessCommand command, CancellationToken cancellationToken = default) => Unavailable<InventoryValuationProcessResult>();
    public Task<IReadOnlyList<InventoryValuationStateRecord>> ListStatesAsync(InventoryRequestContext context, InventoryValuationQuery query, CancellationToken cancellationToken = default) => Unavailable<IReadOnlyList<InventoryValuationStateRecord>>();
    public Task<IReadOnlyList<InventoryMovementValuationEventRecord>> ListEventsAsync(InventoryRequestContext context, InventoryValuationQuery query, CancellationToken cancellationToken = default) => Unavailable<IReadOnlyList<InventoryMovementValuationEventRecord>>();
    public Task<IReadOnlyList<InventoryValuationReconciliationRecord>> ReconcileAsync(InventoryRequestContext context, InventoryValuationQuery query, CancellationToken cancellationToken = default) => Unavailable<IReadOnlyList<InventoryValuationReconciliationRecord>>();
    public Task<IReadOnlyList<InventoryFinanceValuationHandoffRecord>> ListFinanceHandoffsAsync(InventoryRequestContext context, InventoryValuationQuery query, CancellationToken cancellationToken = default) => Unavailable<IReadOnlyList<InventoryFinanceValuationHandoffRecord>>();
    public Task<InventoryValuationExportRecord> ExportAsync(InventoryRequestContext context, InventoryValuationQuery query, CancellationToken cancellationToken = default) => Unavailable<InventoryValuationExportRecord>();
    public Task<InventoryPersistenceResult<InventoryMovementValuationEventRecord>> CorrectAsync(InventoryRequestContext context, InventoryValuationCorrectionCommand command, CancellationToken cancellationToken = default) => Unavailable<InventoryPersistenceResult<InventoryMovementValuationEventRecord>>();
}

public sealed record MovingWeightedAverageInput(
    InventoryMovementDirection Direction,
    decimal Quantity,
    decimal BaseUnitCost,
    decimal PriorQuantity,
    decimal PriorValue,
    int UnitCostScale,
    int AmountScale,
    InventoryValuationRoundingMode RoundingMode);

public sealed record MovingWeightedAverageOutput(
    decimal NewQuantity,
    decimal NewValue,
    decimal AverageUnitCost,
    decimal MovementValue);

public sealed record MovingWeightedAverageCorrectionInput(
    InventoryMovementDirection Direction,
    decimal Quantity,
    decimal ReversalValue,
    decimal PriorQuantity,
    decimal PriorValue,
    int UnitCostScale,
    int AmountScale,
    InventoryValuationRoundingMode RoundingMode);

public static class MovingWeightedAverageCalculator
{
    public static bool TryApply(MovingWeightedAverageInput input, out MovingWeightedAverageOutput output, out string error)
    {
        output = new(0m, 0m, 0m, 0m);
        error = string.Empty;
        if (input.Quantity <= 0m || input.PriorQuantity < 0m || input.PriorValue < 0m || input.BaseUnitCost < 0m)
        {
            error = "invalid_non_negative_valuation_input";
            return false;
        }

        var priorQuantity = Round(input.PriorQuantity, input.AmountScale, input.RoundingMode);
        var priorValue = Round(input.PriorValue, input.AmountScale, input.RoundingMode);
        var quantity = Round(input.Quantity, input.AmountScale, input.RoundingMode);
        var baseUnitCost = Round(input.BaseUnitCost, input.UnitCostScale, input.RoundingMode);
        decimal newQuantity;
        decimal movementValue;
        decimal newValue;

        if (input.Direction == InventoryMovementDirection.Inbound)
        {
            movementValue = Round(quantity * baseUnitCost, input.AmountScale, input.RoundingMode);
            newQuantity = Round(priorQuantity + quantity, input.AmountScale, input.RoundingMode);
            newValue = Round(priorValue + movementValue, input.AmountScale, input.RoundingMode);
        }
        else if (input.Direction == InventoryMovementDirection.Outbound)
        {
            if (quantity > priorQuantity)
            {
                error = "valuation_would_make_quantity_negative";
                return false;
            }

            movementValue = Round(quantity * (priorQuantity == 0m ? 0m : priorValue / priorQuantity), input.AmountScale, input.RoundingMode);
            newQuantity = Round(priorQuantity - quantity, input.AmountScale, input.RoundingMode);
            newValue = Round(priorValue - movementValue, input.AmountScale, input.RoundingMode);
        }
        else
        {
            error = "movement_direction_required";
            return false;
        }

        if (newQuantity < 0m || newValue < 0m)
        {
            error = "valuation_would_make_state_negative";
            return false;
        }

        var average = newQuantity == 0m
            ? 0m
            : Round(newValue / newQuantity, input.UnitCostScale, input.RoundingMode);
        output = new(newQuantity, newValue, average, movementValue);
        return true;
    }

    public static bool TryApplyCorrection(MovingWeightedAverageCorrectionInput input, out MovingWeightedAverageOutput output, out string error)
    {
        output = new(0m, 0m, 0m, 0m);
        error = string.Empty;
        if (input.Quantity <= 0m || input.ReversalValue < 0m || input.PriorQuantity < 0m || input.PriorValue < 0m)
        {
            error = "invalid_non_negative_correction_input";
            return false;
        }

        var priorQuantity = Round(input.PriorQuantity, input.AmountScale, input.RoundingMode);
        var priorValue = Round(input.PriorValue, input.AmountScale, input.RoundingMode);
        var quantity = Round(input.Quantity, input.AmountScale, input.RoundingMode);
        var reversalValue = Round(input.ReversalValue, input.AmountScale, input.RoundingMode);
        var newQuantity = input.Direction == InventoryMovementDirection.Inbound
            ? Round(priorQuantity + quantity, input.AmountScale, input.RoundingMode)
            : input.Direction == InventoryMovementDirection.Outbound
                ? Round(priorQuantity - quantity, input.AmountScale, input.RoundingMode)
                : -1m;
        var newValue = input.Direction == InventoryMovementDirection.Inbound
            ? Round(priorValue + reversalValue, input.AmountScale, input.RoundingMode)
            : input.Direction == InventoryMovementDirection.Outbound
                ? Round(priorValue - reversalValue, input.AmountScale, input.RoundingMode)
                : -1m;

        if (newQuantity < 0m || newValue < 0m)
        {
            error = newQuantity < 0m
                ? "correction_would_make_quantity_negative"
                : "correction_would_make_value_negative";
            return false;
        }

        var average = newQuantity == 0m
            ? 0m
            : Round(newValue / newQuantity, input.UnitCostScale, input.RoundingMode);
        // A correction is an explicit reversal event. Keep the physical state
        // effect directionally clear in immutable evidence: an outbound
        // reversal removes value, while an inbound reversal restores it.
        var signedMovementValue = input.Direction == InventoryMovementDirection.Outbound
            ? -reversalValue
            : reversalValue;
        output = new(newQuantity, newValue, average, signedMovementValue);
        return true;
    }

    public static decimal Round(decimal value, int scale, InventoryValuationRoundingMode mode)
    {
        if (scale is < 0 or > 28) throw new ArgumentOutOfRangeException(nameof(scale));
        return Math.Round(value, scale, mode == InventoryValuationRoundingMode.AwayFromZero
            ? MidpointRounding.AwayFromZero
            : MidpointRounding.ToEven);
    }
}

#pragma warning restore CS1591
