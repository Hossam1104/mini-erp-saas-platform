#pragma warning disable CS1591

using System.Data;
using System.Globalization;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.Modules.Inventory;
using MiniErp.App.Modules.MasterData;
using MiniErp.App.Modules.Procurement;
using MiniErp.Contracts.Modules.Inventory;
using MiniErp.Contracts.Modules.MasterData;

namespace MiniErp.Infrastructure.Persistence.Modules.Inventory;

internal sealed class InventoryValuationPersistence(
    DbContextOptions options,
    IGoodsReceiptPersistence? goodsReceipts,
    IPurchaseOrderPersistence? purchaseOrders,
    IMasterDataExchangeRatePersistence? exchangeRates) : IInventoryValuationPersistence
{
    private InventoryDbContext CreateContext(InventoryRequestContext context) => new(options, context.TenantContext);

    public async Task<InventoryPersistenceResult<InventoryValuationPolicyRecord>> CreatePolicyAsync(InventoryRequestContext context, InventoryValuationPolicyCommand command, CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(context);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var overlaps = await db.ValuationPolicies.AnyAsync(item => item.CompanyId == command.Request.CompanyId && item.IsActive && item.EffectiveFrom <= (command.Request.EffectiveTo ?? DateOnly.MaxValue) && (item.EffectiveTo == null || item.EffectiveTo >= command.Request.EffectiveFrom), cancellationToken);
        if (overlaps) return InventoryPersistenceResult<InventoryValuationPolicyRecord>.Denied(InventoryPersistenceOutcome.Conflict, "valuation_policy_overlap");
        var policy = new InventoryValuationPolicyEntity(context.TenantId, command.Id, command.Request, command.ActorId, command.OccurredAt);
        db.ValuationPolicies.Add(policy);
        db.Audit.Add(new InventoryAuditEntity(context.TenantId, Guid.NewGuid(), "valuation-policy", policy.Id, "inventory.valuation.policy.create", command.ActorId, context.SessionId, context.AuthorizationPath.ToString(), "Succeeded", null, command.CorrelationId, command.IdempotencyKey, command.RequestFingerprint, null, $"policy:{policy.Id}", command.OccurredAt));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return InventoryPersistenceResult<InventoryValuationPolicyRecord>.Success(ToPolicy(policy));
    }

    public async Task<IReadOnlyList<InventoryValuationPolicyRecord>> ListPoliciesAsync(InventoryRequestContext context, Guid companyId, CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(context);
        var values = await db.ValuationPolicies.AsNoTracking().Where(item => item.CompanyId == companyId).OrderByDescending(item => item.EffectiveFrom).ThenByDescending(item => item.VersionNumber).ToListAsync(cancellationToken);
        return values.Select(ToPolicy).ToArray();
    }

    public async Task<InventoryValuationProcessResult> ProcessAsync(InventoryRequestContext context, InventoryValuationProcessCommand command, CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(context);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        if (!string.IsNullOrWhiteSpace(command.IdempotencyKey))
        {
            var replay = await db.ValuationRuns.AsNoTracking().SingleOrDefaultAsync(item => item.ActorId == context.ActorId && item.IdempotencyKey == command.IdempotencyKey, cancellationToken);
            if (replay is not null)
            {
                if (!string.Equals(replay.RequestFingerprint, command.RequestFingerprint, StringComparison.Ordinal))
                    return new InventoryValuationProcessResult(command.CompanyId, command.BranchId, command.WarehouseId, command.ProductId, 0, 0, 0, null, command.OccurredAt, string.Empty, null, "idempotency_conflict");
                return JsonSerializer.Deserialize<InventoryValuationProcessResult>(replay.ResultJson)
                    ?? new InventoryValuationProcessResult(command.CompanyId, command.BranchId, command.WarehouseId, command.ProductId, 0, 0, 0, null, command.OccurredAt, string.Empty, null, "replay_unavailable");
            }
        }

        var movementsQuery = db.StockMovements.AsNoTracking().Where(item => item.CompanyId == command.CompanyId);
        if (command.BranchId.HasValue) movementsQuery = movementsQuery.Where(item => item.BranchId == command.BranchId);
        if (command.WarehouseId.HasValue) movementsQuery = movementsQuery.Where(item => item.WarehouseId == command.WarehouseId);
        if (command.ProductId.HasValue) movementsQuery = movementsQuery.Where(item => item.ProductId == command.ProductId);
        if (command.UnitOfMeasureId.HasValue) movementsQuery = movementsQuery.Where(item => item.UnitOfMeasureId == command.UnitOfMeasureId);
        if (!string.IsNullOrWhiteSpace(command.TrackingIdentity)) movementsQuery = movementsQuery.Where(item => item.TrackingIdentity == command.TrackingIdentity);
        var movements = await movementsQuery.OrderBy(item => item.LedgerSequence).ThenBy(item => item.Id).ToListAsync(cancellationToken);
        if (command.AsOfDate.HasValue) movements = movements.Where(item => item.EffectiveDate <= command.AsOfDate.Value).ToList();

        var applied = 0; var pending = 0; var blocked = 0; long? latest = movements.Count == 0 ? null : movements.Max(item => item.LedgerSequence); string functionalCurrency = string.Empty; Guid? lastPolicyId = null;
        var stoppedScopes = new HashSet<ValuationScopeKey>();
        var stateCache = new Dictionary<(Guid PolicyId, ValuationScopeKey Scope), InventoryValuationStateEntity>();
        foreach (var movement in movements)
        {
            var policy = await db.ValuationPolicies.AsNoTracking().Where(item => item.CompanyId == movement.CompanyId && item.IsActive && item.EffectiveFrom <= movement.EffectiveDate && (item.EffectiveTo == null || item.EffectiveTo >= movement.EffectiveDate)).OrderByDescending(item => item.EffectiveFrom).ThenByDescending(item => item.VersionNumber).FirstOrDefaultAsync(cancellationToken);
            if (policy is null) { pending++; continue; }
            functionalCurrency = policy.FunctionalCurrencyCode; lastPolicyId = policy.Id;
            var scopeKey = ValuationScopeKey.From(movement, policy.ScopeMode);
            if (await db.MovementValuationEvents.AnyAsync(item => item.MovementId == movement.Id && item.Status == InventoryValuationEventStatus.Applied, cancellationToken)) continue;

            if (!stateCache.TryGetValue((policy.Id, scopeKey), out var state))
            {
                state = await db.ValuationStates.SingleOrDefaultAsync(item => item.PolicyId == policy.Id && item.CompanyId == scopeKey.CompanyId && item.BranchId == scopeKey.BranchId && item.WarehouseId == scopeKey.WarehouseId && item.ProductId == scopeKey.ProductId && item.UnitOfMeasureId == scopeKey.UnitOfMeasureId && item.TrackingIdentity == scopeKey.TrackingIdentity, cancellationToken);
                var anchor = await db.ValuationScopeAnchors.SingleOrDefaultAsync(item => item.PolicyId == policy.Id && item.CompanyId == scopeKey.CompanyId && item.BranchId == scopeKey.BranchId && item.WarehouseId == scopeKey.WarehouseId && item.ProductId == scopeKey.ProductId && item.UnitOfMeasureId == scopeKey.UnitOfMeasureId && item.TrackingIdentity == scopeKey.TrackingIdentity, cancellationToken);
                if (state is null)
                {
                    state = new InventoryValuationStateEntity(context.TenantId, scopeKey.CompanyId, scopeKey.BranchId, scopeKey.WarehouseId, scopeKey.ProductId, scopeKey.UnitOfMeasureId, scopeKey.TrackingIdentity, policy, command.OccurredAt);
                    db.ValuationStates.Add(state);
                }

                // The anchor is the durable serialization point for this valuation
                // scope. Touching its provider-independent token makes concurrent
                // processors fail at SaveChanges instead of forking the MWA state.
                if (anchor is null)
                {
                    db.ValuationScopeAnchors.Add(new InventoryValuationScopeAnchorEntity(context.TenantId, scopeKey.CompanyId, scopeKey.BranchId, scopeKey.WarehouseId, scopeKey.ProductId, scopeKey.UnitOfMeasureId, scopeKey.TrackingIdentity, policy.Id));
                }
                else
                {
                    anchor.TouchVersion();
                }
            }
            stateCache[(policy.Id, scopeKey)] = state;
            if (movement.LedgerSequence <= state.LastAppliedLedgerSequence) continue;

            if (stoppedScopes.Contains(scopeKey))
            {
                db.MovementValuationEvents.Add(new InventoryMovementValuationEventEntity(context.TenantId, Guid.NewGuid(), movement.Id, movement.LedgerSequence, InventoryValuationEventStatus.Pending, "pending_predecessor", movement, policy, state.Quantity, state.Value, state.Quantity, state.Value, null, null, null, null, null, null, null, null, null, null, false, "pending_predecessor", command.ActorId, command.CorrelationId, command.OccurredAt));
                pending++;
                continue;
            }

            var correction = movement.CorrectionOfMovementId.HasValue
                ? await ResolveCorrectionAsync(context, db, movement, policy, cancellationToken)
                : null;
            if (correction is not null && !correction.Succeeded)
            {
                var correctionStatus = correction.IsBlocked ? InventoryValuationEventStatus.Blocked : InventoryValuationEventStatus.Pending;
                db.MovementValuationEvents.Add(new InventoryMovementValuationEventEntity(context.TenantId, Guid.NewGuid(), movement.Id, movement.LedgerSequence, correctionStatus, correction.Code, movement, policy, state.Quantity, state.Value, state.Quantity, state.Value, null, correction.Cost?.BaseUnitCost, correction.Cost?.ExchangeRateId, correction.Cost?.ExchangeRateVersionId, correction.Cost?.ExchangeRateVersionNumber, correction.Cost?.ExchangeRate, correction.Cost?.ExchangeRateScale, correction.Cost?.ExchangeRateProvenance, correction.OriginalValuationEventId, null, false, correction.Reason, command.ActorId, command.CorrelationId, command.OccurredAt, correction.Cost?.TransactionUnitCost, correction.Cost?.TransactionCurrencyCode));
                stoppedScopes.Add(scopeKey);
                if (correction.IsBlocked) blocked++; else pending++;
                continue;
            }

            var cost = correction?.Cost ?? await ResolveCostAsync(context, db, movement, policy, state, cancellationToken);
            if (!cost.Succeeded)
            {
                db.MovementValuationEvents.Add(new InventoryMovementValuationEventEntity(context.TenantId, Guid.NewGuid(), movement.Id, movement.LedgerSequence, InventoryValuationEventStatus.Pending, cost.Code, movement, policy, state.Quantity, state.Value, state.Quantity, state.Value, null, null, cost.ExchangeRateId, cost.ExchangeRateVersionId, cost.ExchangeRateVersionNumber, cost.ExchangeRate, cost.ExchangeRateScale, cost.ExchangeRateProvenance, null, null, false, cost.Reason, command.ActorId, command.CorrelationId, command.OccurredAt, cost.TransactionUnitCost, cost.TransactionCurrencyCode));
                stoppedScopes.Add(scopeKey); pending++; continue;
            }

            var baseCost = movement.Direction == InventoryMovementDirection.Outbound ? state.AverageUnitCost : cost.BaseUnitCost ?? 0m;
            MovingWeightedAverageOutput calculation;
            string calculationError;
            var calculationSucceeded = correction is not null
                ? MovingWeightedAverageCalculator.TryApplyCorrection(new MovingWeightedAverageCorrectionInput(movement.Direction, movement.Quantity, correction.ReversalValue, state.Quantity, state.Value, policy.UnitCostScale, policy.AmountScale, policy.RoundingMode), out calculation, out calculationError)
                : MovingWeightedAverageCalculator.TryApply(new MovingWeightedAverageInput(movement.Direction, movement.Quantity, baseCost, state.Quantity, state.Value, policy.UnitCostScale, policy.AmountScale, policy.RoundingMode), out calculation, out calculationError);
            if (!calculationSucceeded)
            {
                db.MovementValuationEvents.Add(new InventoryMovementValuationEventEntity(context.TenantId, Guid.NewGuid(), movement.Id, movement.LedgerSequence, InventoryValuationEventStatus.Blocked, calculationError, movement, policy, state.Quantity, state.Value, state.Quantity, state.Value, null, baseCost, cost.ExchangeRateId, cost.ExchangeRateVersionId, cost.ExchangeRateVersionNumber, cost.ExchangeRate, cost.ExchangeRateScale, cost.ExchangeRateProvenance, correction?.OriginalValuationEventId, null, false, calculationError, command.ActorId, command.CorrelationId, command.OccurredAt, cost.TransactionUnitCost, cost.TransactionCurrencyCode));
                stoppedScopes.Add(scopeKey); blocked++; continue;
            }

            var isBackdated = await db.MovementValuationEvents.AnyAsync(item => item.PolicyId == policy.Id && item.CompanyId == scopeKey.CompanyId && item.BranchId == scopeKey.BranchId && item.WarehouseId == scopeKey.WarehouseId && item.ProductId == scopeKey.ProductId && item.UnitOfMeasureId == scopeKey.UnitOfMeasureId && item.TrackingIdentity == scopeKey.TrackingIdentity && item.Status == InventoryValuationEventStatus.Applied && item.EffectiveOn > movement.EffectiveDate, cancellationToken);
            var eventEntity = new InventoryMovementValuationEventEntity(context.TenantId, Guid.NewGuid(), movement.Id, movement.LedgerSequence, InventoryValuationEventStatus.Applied, isBackdated ? "backdated_applied" : "applied", movement, policy, state.Quantity, state.Value, calculation.NewQuantity, calculation.NewValue, calculation.MovementValue, baseCost, cost.ExchangeRateId, cost.ExchangeRateVersionId, cost.ExchangeRateVersionNumber, cost.ExchangeRate, cost.ExchangeRateScale, cost.ExchangeRateProvenance, correction?.OriginalValuationEventId, null, isBackdated, null, command.ActorId, command.CorrelationId, command.OccurredAt, cost.TransactionUnitCost, cost.TransactionCurrencyCode);
            db.MovementValuationEvents.Add(eventEntity);
            state.Apply(calculation.NewQuantity, calculation.NewValue, calculation.AverageUnitCost, movement.LedgerSequence, command.OccurredAt);
            db.FinanceValuationHandoffs.Add(new InventoryFinanceValuationHandoffEntity(context.TenantId, movement, eventEntity, policy, baseCost, calculation.MovementValue, cost.TransactionUnitCost, cost.TransactionCurrencyCode, cost.ExchangeRateId, cost.ExchangeRateVersionId, cost.ExchangeRateVersionNumber, cost.ExchangeRate, cost.ExchangeRateScale, cost.ExchangeRateProvenance, InventoryFinanceValuationHandoffStatus.ReadyForFinance, command.CorrelationId, command.OccurredAt));
            applied++;
        }

        var result = new InventoryValuationProcessResult(command.CompanyId, command.BranchId, command.WarehouseId, command.ProductId, applied, pending, blocked, latest, command.OccurredAt, functionalCurrency, lastPolicyId?.ToString("D"), movements.Count == 0 ? "no_movements" : null);
        if (!string.IsNullOrWhiteSpace(command.IdempotencyKey)) db.ValuationRuns.Add(new InventoryValuationRunEntity(context.TenantId, context.ActorId, command.IdempotencyKey!, command.RequestFingerprint, JsonSerializer.Serialize(result), command.OccurredAt));
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
        }
        catch (DbUpdateConcurrencyException exception)
        {
            throw new InvalidOperationException("valuation_concurrency_conflict", exception);
        }
        return result;
    }

    public async Task<IReadOnlyList<InventoryValuationStateRecord>> ListStatesAsync(InventoryRequestContext context, InventoryValuationQuery query, CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(context); var values = await ApplyScope(db.ValuationStates.AsNoTracking(), query).OrderBy(item => item.WarehouseId).ThenBy(item => item.ProductId).ThenBy(item => item.TrackingIdentity).ToListAsync(cancellationToken); return values.Select(ToState).ToArray();
    }

    public async Task<IReadOnlyList<InventoryMovementValuationEventRecord>> ListEventsAsync(InventoryRequestContext context, InventoryValuationQuery query, CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(context); var values = await ApplyScope(db.MovementValuationEvents.AsNoTracking(), query).OrderByDescending(item => item.LedgerSequence).ThenByDescending(item => item.Id).ToListAsync(cancellationToken); return values.Select(ToEvent).ToArray();
    }

    public async Task<IReadOnlyList<InventoryValuationReconciliationRecord>> ReconcileAsync(InventoryRequestContext context, InventoryValuationQuery query, CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(context);
        var movements = await ApplyMovementScope(db.StockMovements.AsNoTracking(), query).OrderBy(item => item.LedgerSequence).ToListAsync(cancellationToken);
        var events = await ApplyScope(db.MovementValuationEvents.AsNoTracking(), query).ToListAsync(cancellationToken);
        var states = await ApplyScope(db.ValuationStates.AsNoTracking(), query).ToListAsync(cancellationToken);
        var policies = await db.ValuationPolicies.AsNoTracking().Where(item => item.CompanyId == query.CompanyId && item.IsActive).ToListAsync(cancellationToken);
        var handoffs = await ApplyHandoffScope(db.FinanceValuationHandoffs.AsNoTracking(), query).ToListAsync(cancellationToken);
        var rows = new List<InventoryValuationReconciliationRecord>();
        var groupedMovements = movements.GroupBy(item =>
        {
            var movementPolicy = policies
                .Where(policy => IsEffective(policy, item.EffectiveDate))
                .OrderByDescending(policy => policy.EffectiveFrom)
                .ThenByDescending(policy => policy.VersionNumber)
                .FirstOrDefault();
            var trackingIdentity = movementPolicy?.ScopeMode == InventoryValuationScopeMode.WarehouseProductUomTracking
                ? item.TrackingIdentity ?? string.Empty
                : string.Empty;
            return new PhysicalKey(item.CompanyId, item.BranchId, item.WarehouseId, item.ProductId, item.UnitOfMeasureId, trackingIdentity, movementPolicy?.Id);
        });
        foreach (var group in groupedMovements)
        {
            var policy = group.Key.PolicyId.HasValue
                ? policies.SingleOrDefault(item => item.Id == group.Key.PolicyId.Value)
                : null;
            var state = states.Where(item => item.CompanyId == group.Key.CompanyId && item.BranchId == group.Key.BranchId && item.WarehouseId == group.Key.WarehouseId && item.ProductId == group.Key.ProductId && item.UnitOfMeasureId == group.Key.UnitOfMeasureId && item.TrackingIdentity == group.Key.TrackingIdentity && (!group.Key.PolicyId.HasValue || item.PolicyId == group.Key.PolicyId.Value)).OrderByDescending(item => item.UpdatedAt).FirstOrDefault();
            var movementIds = group.Select(item => item.Id).ToHashSet(); var groupEvents = events.Where(item => movementIds.Contains(item.MovementId)).ToArray();
            var physicalQuantity = group.Sum(item => item.Direction == InventoryMovementDirection.Inbound ? item.Quantity : -item.Quantity);
            var movementStatuses = groupEvents.GroupBy(item => item.MovementId).Select(item => new
            {
                MovementId = item.Key,
                HasApplied = item.Any(value => value.Status == InventoryValuationEventStatus.Applied),
                HasPending = item.Any(value => value.Status == InventoryValuationEventStatus.Pending),
                HasBlocked = item.Any(value => value.Status == InventoryValuationEventStatus.Blocked),
                OldestPendingSequence = item.Where(value => value.Status is InventoryValuationEventStatus.Pending or InventoryValuationEventStatus.Blocked).Select(value => (long?)value.LedgerSequence).Min()
            }).ToArray();
            var appliedMovementIds = movementStatuses.Where(item => item.HasApplied).Select(item => item.MovementId).ToHashSet();
            var pendingMovementStatuses = movementStatuses.Where(item => !item.HasApplied && item.HasPending).ToArray();
            var blockedMovementStatuses = movementStatuses.Where(item => !item.HasApplied && item.HasBlocked).ToArray();
            var valuedQuantity = state?.Quantity ?? 0m; var quantityDifference = MovingWeightedAverageCalculator.Round(physicalQuantity - valuedQuantity, policy?.AmountScale ?? 8, policy?.RoundingMode ?? InventoryValuationRoundingMode.ToEven);
            var appliedHandoffCount = handoffs.Count(item => appliedMovementIds.Contains(item.MovementId) && item.Status == InventoryFinanceValuationHandoffStatus.ReadyForFinance);
            var financeHandoffStatus = appliedMovementIds.Count == 0
                ? InventoryFinanceValuationHandoffStatus.Pending
                : appliedHandoffCount == appliedMovementIds.Count
                    ? InventoryFinanceValuationHandoffStatus.ReadyForFinance
                    : InventoryFinanceValuationHandoffStatus.Pending;
            var status = policy is null ? InventoryValuationReconciliationStatus.PendingValuation : blockedMovementStatuses.Length > 0 ? InventoryValuationReconciliationStatus.Blocked : pendingMovementStatuses.Length > 0 ? InventoryValuationReconciliationStatus.PendingValuation : quantityDifference != 0m ? InventoryValuationReconciliationStatus.QuantityMismatch : financeHandoffStatus != InventoryFinanceValuationHandoffStatus.ReadyForFinance ? InventoryValuationReconciliationStatus.FinanceHandoffPending : InventoryValuationReconciliationStatus.Reconciled;
            var inTransit = await ReadInTransitAsync(db, group.Key, events, cancellationToken);
            rows.Add(new InventoryValuationReconciliationRecord(context.TenantId.Value, group.Key.CompanyId, group.Key.BranchId, group.Key.WarehouseId, group.Key.ProductId, group.Key.UnitOfMeasureId, string.IsNullOrEmpty(group.Key.TrackingIdentity) ? null : group.Key.TrackingIdentity, policy?.FunctionalCurrencyCode ?? string.Empty, policy?.Id, status, physicalQuantity, valuedQuantity, quantityDifference, state?.Value ?? 0m, state?.AverageUnitCost ?? 0m, group.Max(item => (long?)item.LedgerSequence), state?.LastAppliedLedgerSequence ?? 0L, group.Count(), appliedMovementIds.Count, pendingMovementStatuses.Length, blockedMovementStatuses.Length, movementStatuses.Where(item => !item.HasApplied).Select(item => item.OldestPendingSequence).Where(item => item.HasValue).Min(), inTransit.Quantity, inTransit.Value, financeHandoffStatus, query.EffectiveTo.HasValue ? query.EffectiveTo.Value.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc) : DateTime.UtcNow, DateTimeOffset.UtcNow, status == InventoryValuationReconciliationStatus.QuantityMismatch ? "physical_quantity_differs_from_valued_state" : status == InventoryValuationReconciliationStatus.FinanceHandoffPending ? "finance_handoff_evidence_pending" : null));
        }
        return rows;
    }

    public async Task<IReadOnlyList<InventoryFinanceValuationHandoffRecord>> ListFinanceHandoffsAsync(InventoryRequestContext context, InventoryValuationQuery query, CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(context); var values = await ApplyHandoffScope(db.FinanceValuationHandoffs.AsNoTracking(), query).OrderByDescending(item => item.LedgerSequence).ToListAsync(cancellationToken); return values.Select(ToHandoff).ToArray();
    }

    public async Task<InventoryValuationExportRecord> ExportAsync(InventoryRequestContext context, InventoryValuationQuery query, CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(context);
        var asOf = DateTimeOffset.UtcNow;
        var events = await ApplyScope(db.MovementValuationEvents.AsNoTracking(), query)
            .OrderBy(item => item.LedgerSequence)
            .ThenBy(item => item.Id)
            .Take(10_000)
            .ToListAsync(cancellationToken);
        var policy = query.PolicyId.HasValue
            ? await db.ValuationPolicies.AsNoTracking().SingleOrDefaultAsync(item => item.Id == query.PolicyId.Value, cancellationToken)
            : events.Select(item => item.PolicyId).Distinct().Count() == 1
                ? await db.ValuationPolicies.AsNoTracking().SingleOrDefaultAsync(item => item.Id == events.Select(value => value.PolicyId).First(), cancellationToken)
                : await db.ValuationPolicies.AsNoTracking().Where(item => item.CompanyId == query.CompanyId && item.IsActive).OrderByDescending(item => item.EffectiveFrom).ThenByDescending(item => item.VersionNumber).FirstOrDefaultAsync(cancellationToken);
        var functionalCurrency = policy?.FunctionalCurrencyCode ?? events.Select(item => item.FunctionalCurrencyCode).FirstOrDefault() ?? string.Empty;
        var requestFingerprint = JsonSerializer.Serialize(query);
        var freshAsOf = DateTimeOffset.UtcNow;
        var content = new StringBuilder()
            .AppendLine("# export=inventory-valuation")
            .AppendLine($"# tenantId={Csv(context.TenantId.Value)}")
            .AppendLine($"# filters={Csv(requestFingerprint)}")
            .AppendLine($"# asOf={Csv(asOf.ToString("O", CultureInfo.InvariantCulture))}")
            .AppendLine($"# freshness={Csv(freshAsOf.ToString("O", CultureInfo.InvariantCulture))}")
            .AppendLine($"# functionalCurrency={Csv(functionalCurrency)}")
            .AppendLine($"# policyId={Csv(policy?.Id.ToString("D"))}")
            .AppendLine($"# policyVersion={Csv(policy?.VersionNumber.ToString(CultureInfo.InvariantCulture))}")
            .AppendLine($"# generatedActor={Csv(context.ActorId.ToString("D"))}")
            .AppendLine($"# generatedCorrelation={Csv(context.CorrelationId?.Value)}")
            .AppendLine("LedgerSequence,MovementId,SourceType,SourceDocumentId,SourceLineId,Status,StatusCode,CompanyId,BranchId,WarehouseId,ProductId,UnitOfMeasureId,TrackingIdentity,PolicyId,PolicyVersion,FunctionalCurrency,Quantity,Direction,TransactionUnitCost,TransactionCurrency,ExchangeRate,BaseUnitCost,PriorQuantity,PriorValue,NewQuantity,NewValue,MovementValue,EffectiveOn,CorrectionOfValuationEventId,SourceRevisionId,PendingReason,CorrelationId,ActorId,OccurredAt");
        foreach (var item in events)
        {
            content.AppendLine(string.Join(',',
                Csv(item.LedgerSequence.ToString(CultureInfo.InvariantCulture)), Csv(item.MovementId.ToString("D")), Csv(item.SourceType), Csv(item.SourceDocumentId.ToString("D")), Csv(item.SourceLineId.ToString("D")), Csv(item.Status), Csv(item.StatusCode), Csv(item.CompanyId.ToString("D")), Csv(item.BranchId?.ToString("D")), Csv(item.WarehouseId.ToString("D")), Csv(item.ProductId.ToString("D")), Csv(item.UnitOfMeasureId.ToString("D")), Csv(string.IsNullOrEmpty(item.TrackingIdentity) ? null : item.TrackingIdentity), Csv(item.PolicyId.ToString("D")), Csv(item.PolicyVersionNumber.ToString(CultureInfo.InvariantCulture)), Csv(item.FunctionalCurrencyCode), Csv(item.Quantity.ToString(CultureInfo.InvariantCulture)), Csv(item.Direction), Csv(item.TransactionUnitCost?.ToString(CultureInfo.InvariantCulture)), Csv(item.TransactionCurrencyCode), Csv(item.ExchangeRate?.ToString(CultureInfo.InvariantCulture)), Csv(item.BaseUnitCost?.ToString(CultureInfo.InvariantCulture)), Csv(item.PriorQuantity.ToString(CultureInfo.InvariantCulture)), Csv(item.PriorValue.ToString(CultureInfo.InvariantCulture)), Csv(item.NewQuantity.ToString(CultureInfo.InvariantCulture)), Csv(item.NewValue.ToString(CultureInfo.InvariantCulture)), Csv(item.MovementValue?.ToString(CultureInfo.InvariantCulture)), Csv(item.EffectiveOn.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)), Csv(item.CorrectionOfValuationEventId?.ToString("D")), Csv(item.SourceRevisionId?.ToString("D")), Csv(item.PendingReason), Csv(item.CorrelationId), Csv(item.ActorId.ToString("D")), Csv(item.OccurredAt.ToString("O", CultureInfo.InvariantCulture))));
        }

        var exportId = Guid.NewGuid();
        db.Audit.Add(new InventoryAuditEntity(context.TenantId, exportId, "valuation-export", exportId, "inventory.valuation.export", context.ActorId, context.SessionId, context.AuthorizationPath.ToString(), "Succeeded", null, context.CorrelationId?.Value ?? string.Empty, null, requestFingerprint, null, $"rows:{events.Count};asOf:{asOf:O};functionalCurrency:{functionalCurrency};policy:{policy?.Id:D};policyVersion:{policy?.VersionNumber}", asOf));
        await db.SaveChangesAsync(cancellationToken);
        return new InventoryValuationExportRecord($"inventory-valuation-{asOf:yyyyMMddHHmmss}.csv", "text/csv; charset=utf-8", content.ToString(), context.TenantId.Value, query.CompanyId, functionalCurrency, policy?.Id, policy?.VersionNumber, asOf, freshAsOf, context.ActorId, context.CorrelationId?.Value ?? string.Empty);
    }

    public Task<InventoryPersistenceResult<InventoryMovementValuationEventRecord>> CorrectAsync(InventoryRequestContext context, InventoryValuationCorrectionCommand command, CancellationToken cancellationToken = default) =>
        Task.FromResult(InventoryPersistenceResult<InventoryMovementValuationEventRecord>.Denied(InventoryPersistenceOutcome.Conflict, "authoritative_source_revision_provider_required"));

    private async Task<CostResolution> ResolveCostAsync(InventoryRequestContext context, InventoryDbContext db, InventoryStockMovementEntity movement, InventoryValuationPolicyEntity policy, InventoryValuationStateEntity state, CancellationToken cancellationToken)
    {
        decimal? transactionCost = movement.UnitCost; string? transactionCurrency = movement.CurrencyCode;
        if (movement.SourceType == InventoryMovementSourceType.GoodsReceipt && movement.PurchaseOrderId.HasValue && movement.PurchaseOrderLineId.HasValue && goodsReceipts is not null && purchaseOrders is not null)
        {
            var purchaseOrder = await purchaseOrders.FindAsync(context.TenantContext, movement.PurchaseOrderId.Value, cancellationToken);
            var line = purchaseOrder?.Lines.FirstOrDefault(item => item.Id == movement.PurchaseOrderLineId.Value);
            transactionCost = line?.UnitPrice; transactionCurrency = purchaseOrder?.Source.Currency.Code;
        }
        else if ((movement.SourceType is InventoryMovementSourceType.WarehouseTransferReceipt or InventoryMovementSourceType.WarehouseTransferLoss or InventoryMovementSourceType.WarehouseTransferReturn) && movement.TransferId.HasValue)
        {
            var shipment = await db.StockMovements.AsNoTracking().Where(item => item.TransferId == movement.TransferId && item.SourceType == InventoryMovementSourceType.WarehouseTransferShipment).OrderBy(item => item.LedgerSequence).FirstOrDefaultAsync(cancellationToken);
            if (shipment is not null)
            {
                var shipmentEvent = await db.MovementValuationEvents.AsNoTracking().Where(item => item.MovementId == shipment.Id && item.Status == InventoryValuationEventStatus.Applied).OrderByDescending(item => item.LedgerSequence).FirstOrDefaultAsync(cancellationToken);
                transactionCost = shipmentEvent?.BaseUnitCost; transactionCurrency = policy.FunctionalCurrencyCode;
                if (shipmentEvent is null) return CostResolution.Pending("transfer_shipment_valuation_pending");
                return CostResolution.Success(shipmentEvent.BaseUnitCost ?? 0m, null, null, null, null, null, null, "transfer_inherited", shipmentEvent.TransactionUnitCost ?? shipmentEvent.BaseUnitCost, shipmentEvent.FunctionalCurrencyCode);
            }
            return CostResolution.Pending("transfer_shipment_not_found");
        }
        else if (movement.Direction == InventoryMovementDirection.Outbound && movement.SourceType == InventoryMovementSourceType.SupplierReturn && policy.SupplierReturnCostBasis == "LinkedReceiptValuation")
        {
            var receipt = await db.StockMovements.AsNoTracking().Where(item => item.GoodsReceiptLineId == movement.GoodsReceiptLineId && item.SourceType == InventoryMovementSourceType.GoodsReceipt).FirstOrDefaultAsync(cancellationToken);
            var receiptEvent = receipt is null ? null : await db.MovementValuationEvents.AsNoTracking().Where(item => item.MovementId == receipt.Id && item.Status == InventoryValuationEventStatus.Applied).FirstOrDefaultAsync(cancellationToken);
            if (receiptEvent is null) return CostResolution.Pending("linked_receipt_valuation_pending");
            return CostResolution.Success(receiptEvent.BaseUnitCost ?? 0m, null, null, null, null, null, null, "linked_receipt", receiptEvent.TransactionUnitCost ?? receiptEvent.BaseUnitCost, receiptEvent.FunctionalCurrencyCode);
        }

        if (movement.Direction == InventoryMovementDirection.Outbound || (movement.Direction == InventoryMovementDirection.Inbound && !transactionCost.HasValue))
        {
            if (movement.SourceType == InventoryMovementSourceType.CustomerReturn && movement.Direction == InventoryMovementDirection.Inbound && !transactionCost.HasValue)
                return CostResolution.Pending("customer_return_original_delivery_valuation_required");
            if (movement.Direction == InventoryMovementDirection.Inbound && movement.SourceType is (InventoryMovementSourceType.StockAdjustment or InventoryMovementSourceType.InventoryCountVariance or InventoryMovementSourceType.CustomerReturn) && policy.PositiveAdjustmentCostBasis != "CurrentMovingAverage") return CostResolution.Pending("positive_movement_cost_basis_not_configured", movement.UnitCost, movement.CurrencyCode);
            if (state.Quantity <= 0m && movement.Direction == InventoryMovementDirection.Outbound) return CostResolution.Pending("current_moving_average_unavailable");
            return CostResolution.Success(state.AverageUnitCost, null, null, null, null, null, null, "current_moving_average", movement.UnitCost, movement.CurrencyCode);
        }
        if (!transactionCost.HasValue || string.IsNullOrWhiteSpace(transactionCurrency)) return CostResolution.Pending("transaction_cost_or_currency_missing", transactionCost, transactionCurrency);
        var fx = await ResolveExchangeRateAsync(context, transactionCurrency!, policy.FunctionalCurrencyCode, movement.EffectiveDate, cancellationToken);
        if (!fx.Succeeded) return fx with { TransactionUnitCost = transactionCost, TransactionCurrencyCode = transactionCurrency };
        var baseUnitCost = MovingWeightedAverageCalculator.Round(transactionCost.Value * fx.ExchangeRate!.Value, policy.UnitCostScale, policy.RoundingMode);
        return CostResolution.Success(baseUnitCost, fx.ExchangeRateId, fx.ExchangeRateVersionId, fx.ExchangeRateVersionNumber, fx.ExchangeRate, fx.ExchangeRateScale, fx.ExchangeRateProvenance, "source_cost", transactionCost, transactionCurrency);
    }

    private static async Task<CorrectionResolution> ResolveCorrectionAsync(InventoryRequestContext context, InventoryDbContext db, InventoryStockMovementEntity movement, InventoryValuationPolicyEntity policy, CancellationToken cancellationToken)
    {
        var originalMovement = await db.StockMovements.AsNoTracking().SingleOrDefaultAsync(item => item.Id == movement.CorrectionOfMovementId, cancellationToken);
        if (originalMovement is null)
            return CorrectionResolution.Blocked("correction_source_movement_missing");

        var originalEvent = db.MovementValuationEvents.Local
            .Where(item => item.MovementId == originalMovement.Id && item.Status == InventoryValuationEventStatus.Applied)
            .OrderByDescending(item => item.LedgerSequence)
            .FirstOrDefault();
        originalEvent ??= await db.MovementValuationEvents.AsNoTracking()
            .Where(item => item.MovementId == originalMovement.Id && item.Status == InventoryValuationEventStatus.Applied)
            .OrderByDescending(item => item.LedgerSequence)
            .FirstOrDefaultAsync(cancellationToken);
        if (originalEvent is null)
            return CorrectionResolution.Pending("correction_source_valuation_pending");
        if (originalEvent.Direction == movement.Direction)
            return CorrectionResolution.Blocked("correction_direction_must_reverse");
        if (originalEvent.MovementValue is null || originalEvent.Quantity <= 0m)
            return CorrectionResolution.Pending("correction_source_value_missing");
        if (movement.Quantity > originalEvent.Quantity)
            return CorrectionResolution.Blocked("correction_quantity_exceeds_original");

        var reversalValue = movement.Quantity == originalEvent.Quantity
            ? originalEvent.MovementValue.Value
            : MovingWeightedAverageCalculator.Round(originalEvent.MovementValue.Value * movement.Quantity / originalEvent.Quantity, policy.AmountScale, policy.RoundingMode);
        var baseUnitCost = originalEvent.BaseUnitCost
            ?? MovingWeightedAverageCalculator.Round(originalEvent.MovementValue.Value / originalEvent.Quantity, policy.UnitCostScale, policy.RoundingMode);
        var cost = CostResolution.Success(baseUnitCost, originalEvent.ExchangeRateId, originalEvent.ExchangeRateVersionId, originalEvent.ExchangeRateVersionNumber, originalEvent.ExchangeRate, originalEvent.ExchangeRateScale, originalEvent.ExchangeRateProvenance, "correction_reversal", originalEvent.TransactionUnitCost, originalEvent.TransactionCurrencyCode);
        return CorrectionResolution.Applied(originalEvent.Id, reversalValue, cost);
    }

    private async Task<CostResolution> ResolveExchangeRateAsync(InventoryRequestContext context, string sourceCurrency, string targetCurrency, DateOnly effectiveOn, CancellationToken cancellationToken)
    {
        if (string.Equals(sourceCurrency.Trim(), targetCurrency.Trim(), StringComparison.OrdinalIgnoreCase)) return CostResolution.Success(1m, null, null, null, 1m, 0, "same_currency", "same_currency");
        if (exchangeRates is null) return CostResolution.Pending("exchange_rate_persistence_unavailable");
        IReadOnlyList<MasterDataExchangeRateRecord> rates;
        try { rates = await exchangeRates.ListExchangeRatesAsync(context.TenantContext, cancellationToken); }
        catch (InvalidOperationException) { return CostResolution.Pending("exchange_rate_persistence_unavailable"); }
        var candidates = rates.Where(rate => rate.LifecycleState == MasterDataLifecycleState.Active && string.Equals(rate.SourceCurrencyCode, sourceCurrency.Trim(), StringComparison.OrdinalIgnoreCase) && string.Equals(rate.TargetCurrencyCode, targetCurrency.Trim(), StringComparison.OrdinalIgnoreCase)).SelectMany(rate => rate.Versions.Where(version => version.EffectiveFrom <= effectiveOn && (version.EffectiveTo is null || version.EffectiveTo >= effectiveOn)).Select(version => (rate, version))).ToArray();
        if (candidates.Length != 1) return CostResolution.Pending(candidates.Length == 0 ? "exchange_rate_missing" : "exchange_rate_ambiguous");
        var selected = candidates[0]; return CostResolution.Success(selected.version.Rate, selected.rate.Id, selected.version.Id, selected.version.VersionNumber, selected.version.Rate, selected.version.RateScale, selected.version.Provenance.ToString(), "exchange_rate");
    }

    private static IQueryable<InventoryValuationStateEntity> ApplyScope(IQueryable<InventoryValuationStateEntity> query, InventoryValuationQuery filter)
    {
        query = query.Where(item => item.CompanyId == filter.CompanyId); if (filter.BranchId.HasValue) query = query.Where(item => item.BranchId == filter.BranchId); if (filter.WarehouseId.HasValue) query = query.Where(item => item.WarehouseId == filter.WarehouseId); if (filter.ProductId.HasValue) query = query.Where(item => item.ProductId == filter.ProductId); if (filter.UnitOfMeasureId.HasValue) query = query.Where(item => item.UnitOfMeasureId == filter.UnitOfMeasureId); if (!string.IsNullOrWhiteSpace(filter.TrackingIdentity)) query = query.Where(item => item.TrackingIdentity == filter.TrackingIdentity); if (filter.PolicyId.HasValue) query = query.Where(item => item.PolicyId == filter.PolicyId); if (!string.IsNullOrWhiteSpace(filter.FunctionalCurrencyCode)) query = query.Where(item => item.FunctionalCurrencyCode == filter.FunctionalCurrencyCode); return query;
    }

    private static IQueryable<InventoryMovementValuationEventEntity> ApplyScope(IQueryable<InventoryMovementValuationEventEntity> query, InventoryValuationQuery filter)
    {
        query = query.Where(item => item.CompanyId == filter.CompanyId); if (filter.BranchId.HasValue) query = query.Where(item => item.BranchId == filter.BranchId); if (filter.WarehouseId.HasValue) query = query.Where(item => item.WarehouseId == filter.WarehouseId); if (filter.ProductId.HasValue) query = query.Where(item => item.ProductId == filter.ProductId); if (filter.UnitOfMeasureId.HasValue) query = query.Where(item => item.UnitOfMeasureId == filter.UnitOfMeasureId); if (!string.IsNullOrWhiteSpace(filter.TrackingIdentity)) query = query.Where(item => item.TrackingIdentity == filter.TrackingIdentity); if (filter.SourceType.HasValue) query = query.Where(item => item.SourceType == filter.SourceType); if (filter.PolicyId.HasValue) query = query.Where(item => item.PolicyId == filter.PolicyId); if (!string.IsNullOrWhiteSpace(filter.FunctionalCurrencyCode)) query = query.Where(item => item.FunctionalCurrencyCode == filter.FunctionalCurrencyCode); if (filter.Status.HasValue) query = query.Where(item => item.Status == filter.Status); if (filter.FromLedgerSequence.HasValue) query = query.Where(item => item.LedgerSequence >= filter.FromLedgerSequence); if (filter.ToLedgerSequence.HasValue) query = query.Where(item => item.LedgerSequence <= filter.ToLedgerSequence); if (filter.EffectiveFrom.HasValue) query = query.Where(item => item.EffectiveOn >= filter.EffectiveFrom); if (filter.EffectiveTo.HasValue) query = query.Where(item => item.EffectiveOn <= filter.EffectiveTo); return query;
    }

    private static IQueryable<InventoryStockMovementEntity> ApplyMovementScope(IQueryable<InventoryStockMovementEntity> query, InventoryValuationQuery filter)
    {
        query = query.Where(item => item.CompanyId == filter.CompanyId); if (filter.BranchId.HasValue) query = query.Where(item => item.BranchId == filter.BranchId); if (filter.WarehouseId.HasValue) query = query.Where(item => item.WarehouseId == filter.WarehouseId); if (filter.ProductId.HasValue) query = query.Where(item => item.ProductId == filter.ProductId); if (filter.UnitOfMeasureId.HasValue) query = query.Where(item => item.UnitOfMeasureId == filter.UnitOfMeasureId); if (!string.IsNullOrWhiteSpace(filter.TrackingIdentity)) query = query.Where(item => item.TrackingIdentity == filter.TrackingIdentity); if (filter.SourceType.HasValue) query = query.Where(item => item.SourceType == filter.SourceType); if (filter.FromLedgerSequence.HasValue) query = query.Where(item => item.LedgerSequence >= filter.FromLedgerSequence); if (filter.ToLedgerSequence.HasValue) query = query.Where(item => item.LedgerSequence <= filter.ToLedgerSequence); if (filter.EffectiveFrom.HasValue) query = query.Where(item => item.EffectiveDate >= filter.EffectiveFrom); if (filter.EffectiveTo.HasValue) query = query.Where(item => item.EffectiveDate <= filter.EffectiveTo); return query;
    }

    private static IQueryable<InventoryFinanceValuationHandoffEntity> ApplyHandoffScope(IQueryable<InventoryFinanceValuationHandoffEntity> query, InventoryValuationQuery filter)
    {
        query = query.Where(item => item.CompanyId == filter.CompanyId); if (filter.BranchId.HasValue) query = query.Where(item => item.BranchId == filter.BranchId); if (filter.WarehouseId.HasValue) query = query.Where(item => item.WarehouseId == filter.WarehouseId); if (filter.ProductId.HasValue) query = query.Where(item => item.ProductId == filter.ProductId); if (filter.UnitOfMeasureId.HasValue) query = query.Where(item => item.UnitOfMeasureId == filter.UnitOfMeasureId); if (!string.IsNullOrWhiteSpace(filter.TrackingIdentity)) query = query.Where(item => item.TrackingIdentity == filter.TrackingIdentity); if (filter.SourceType.HasValue) query = query.Where(item => item.SourceType == filter.SourceType); if (filter.PolicyId.HasValue) query = query.Where(item => item.PolicyId == filter.PolicyId); if (!string.IsNullOrWhiteSpace(filter.FunctionalCurrencyCode)) query = query.Where(item => item.FunctionalCurrencyCode == filter.FunctionalCurrencyCode); if (filter.FromLedgerSequence.HasValue) query = query.Where(item => item.LedgerSequence >= filter.FromLedgerSequence); if (filter.ToLedgerSequence.HasValue) query = query.Where(item => item.LedgerSequence <= filter.ToLedgerSequence); return query;
    }

    private static bool IsEffective(InventoryValuationPolicyEntity policy, DateOnly date) => policy.EffectiveFrom <= date && (policy.EffectiveTo is null || policy.EffectiveTo >= date);

    private static string Csv(object? value) => $"\"{(value?.ToString() ?? string.Empty).Replace("\"", "\"\"", StringComparison.Ordinal)}\"";

    private static async Task<(decimal Quantity, decimal Value)> ReadInTransitAsync(InventoryDbContext db, PhysicalKey key, IReadOnlyList<InventoryMovementValuationEventEntity> events, CancellationToken cancellationToken)
    {
        var transfers = await db.Transfers.AsNoTracking().Where(item => item.CompanyId == key.CompanyId && item.SourceWarehouseId == key.WarehouseId && item.ProductId == key.ProductId && item.UnitOfMeasureId == key.UnitOfMeasureId).ToListAsync(cancellationToken);
        decimal quantity = 0m, value = 0m;
        foreach (var transfer in transfers)
        {
            var shipment = await db.StockMovements.AsNoTracking().Where(item => item.TransferId == transfer.Id && item.SourceType == InventoryMovementSourceType.WarehouseTransferShipment).FirstOrDefaultAsync(cancellationToken);
            if (shipment is null) continue;
            var received = await db.StockMovements.AsNoTracking().Where(item => item.TransferId == transfer.Id && item.SourceType == InventoryMovementSourceType.WarehouseTransferReceipt).SumAsync(item => (decimal?)item.Quantity, cancellationToken) ?? 0m;
            var shipped = shipment.Quantity; if (shipped <= received) continue;
            var shipmentEvent = events.FirstOrDefault(item => item.MovementId == shipment.Id && item.Status == InventoryValuationEventStatus.Applied); quantity += shipped - received; value += shipmentEvent?.MovementValue ?? 0m;
        }
        return (quantity, value);
    }

    private static InventoryValuationPolicyRecord ToPolicy(InventoryValuationPolicyEntity item) => new(item.Id, item.TenantId.Value, item.CompanyId, item.FunctionalCurrencyId, item.FunctionalCurrencyCode, item.ScopeMode, item.EffectiveFrom, item.EffectiveTo, item.VersionNumber, item.UnitCostScale, item.AmountScale, item.RoundingMode, item.GoodsReceiptCostBasis, item.PositiveAdjustmentCostBasis, item.SupplierReturnCostBasis, item.IsActive, item.Version);
    private static InventoryValuationStateRecord ToState(InventoryValuationStateEntity item) => new(item.TenantId.Value, item.CompanyId, item.BranchId, item.WarehouseId, item.ProductId, item.UnitOfMeasureId, string.IsNullOrEmpty(item.TrackingIdentity) ? null : item.TrackingIdentity, item.PolicyId, item.PolicyVersionNumber, item.FunctionalCurrencyCode, item.Quantity, item.Value, item.AverageUnitCost, item.LastAppliedLedgerSequence, item.UpdatedAt, item.Version);
    private static InventoryMovementValuationEventRecord ToEvent(InventoryMovementValuationEventEntity item) => new(item.Id, item.TenantId.Value, item.MovementId, item.SourceType, item.SourceDocumentId, item.SourceLineId, item.CorrectionOfMovementId, item.GoodsReceiptId, item.GoodsReceiptLineId, item.SupplierReturnId, item.SupplierReturnLineId, item.PurchaseOrderId, item.PurchaseOrderLineId, item.TransferId, item.TransferLineId, item.SourceReference, item.LedgerSequence, item.Status, item.StatusCode, item.CompanyId, item.BranchId, item.WarehouseId, item.ProductId, item.UnitOfMeasureId, string.IsNullOrEmpty(item.TrackingIdentity) ? null : item.TrackingIdentity, item.PolicyId, item.PolicyVersionNumber, item.FunctionalCurrencyCode, item.Quantity, item.Direction, item.TransactionUnitCost, item.TransactionCurrencyCode, item.ExchangeRateId, item.ExchangeRateVersionId, item.ExchangeRateVersionNumber, item.ExchangeRate, item.ExchangeRateScale, item.ExchangeRateProvenance, item.EffectiveOn, item.BaseUnitCost, item.PriorQuantity, item.PriorValue, item.NewQuantity, item.NewValue, item.MovementValue, item.UnitCostScale, item.AmountScale, item.RoundingMode, item.CorrectionOfValuationEventId, item.SourceRevisionId, item.IsBackdated, item.PendingReason, item.CorrelationId, item.ActorId, item.OccurredAt, item.Version);
    private static InventoryFinanceValuationHandoffRecord ToHandoff(InventoryFinanceValuationHandoffEntity item) => new(item.Id, item.TenantId.Value, item.CompanyId, item.BranchId, item.WarehouseId, item.MovementId, item.LedgerSequence, item.SourceType, item.SourceDocumentId, item.SourceLineId, item.ValuationEvidenceId, item.ValuationEvidenceVersion, item.Quantity, item.BaseUnitCost, item.BaseAmount, item.PolicyId, item.PolicyVersionNumber, item.FunctionalCurrencyCode, item.TransactionUnitCost, item.TransactionCurrencyCode, item.ExchangeRateId, item.ExchangeRateVersionId, item.ExchangeRateVersionNumber, item.ExchangeRate, item.ExchangeRateScale, item.ExchangeRateProvenance, item.ProductId, item.UnitOfMeasureId, string.IsNullOrEmpty(item.TrackingIdentity) ? null : item.TrackingIdentity, item.CorrectionOfMovementId, item.Status, item.ContractVersion, item.CorrelationId, item.AsOf, item.CreatedAt, item.Version);

    private readonly record struct ValuationScopeKey(Guid CompanyId, Guid? BranchId, Guid WarehouseId, Guid ProductId, Guid UnitOfMeasureId, string TrackingIdentity)
    {
        internal static ValuationScopeKey From(InventoryStockMovementEntity movement, InventoryValuationScopeMode mode) => new(movement.CompanyId, movement.BranchId, movement.WarehouseId, movement.ProductId, movement.UnitOfMeasureId, mode == InventoryValuationScopeMode.WarehouseProductUomTracking ? movement.TrackingIdentity ?? string.Empty : string.Empty);
    }
    private readonly record struct PhysicalKey(Guid CompanyId, Guid? BranchId, Guid WarehouseId, Guid ProductId, Guid UnitOfMeasureId, string TrackingIdentity, Guid? PolicyId);
    private sealed record CostResolution(bool Succeeded, string Code, string? Reason, decimal? BaseUnitCost, Guid? ExchangeRateId, Guid? ExchangeRateVersionId, int? ExchangeRateVersionNumber, decimal? ExchangeRate, int? ExchangeRateScale, string? ExchangeRateProvenance, decimal? TransactionUnitCost, string? TransactionCurrencyCode)
    {
        internal static CostResolution Success(decimal baseUnitCost, Guid? id, Guid? versionId, int? versionNumber, decimal? rate, int? scale, string? provenance, string code, decimal? transactionUnitCost = null, string? transactionCurrencyCode = null) => new(true, code, null, baseUnitCost, id, versionId, versionNumber, rate, scale, provenance, transactionUnitCost, transactionCurrencyCode);
        internal static CostResolution Pending(string reason, decimal? transactionUnitCost = null, string? transactionCurrencyCode = null) => new(false, "valuation_pending", reason, null, null, null, null, null, null, null, transactionUnitCost, transactionCurrencyCode);
    }

    private sealed record CorrectionResolution(bool Succeeded, bool IsBlocked, string Code, string? Reason, Guid? OriginalValuationEventId, decimal ReversalValue, CostResolution? Cost)
    {
        internal static CorrectionResolution Pending(string reason) => new(false, false, "correction_pending", reason, null, 0m, null);
        internal static CorrectionResolution Blocked(string reason) => new(false, true, "correction_blocked", reason, null, 0m, null);
        internal static CorrectionResolution Applied(Guid originalValuationEventId, decimal reversalValue, CostResolution cost) => new(true, false, "correction_reversal", null, originalValuationEventId, reversalValue, cost);
    }
}

#pragma warning restore CS1591
