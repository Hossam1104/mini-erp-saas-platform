#pragma warning disable CS1591

using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.Modules.Procurement;
using MiniErp.Contracts.Modules.Procurement;

namespace MiniErp.Infrastructure.Persistence.Modules.Procurement;

public sealed class SupplierReturnPersistence : ISupplierReturnPersistence
{
    private const string CreateOperationId = "procurement.supplier-return.create";
    private const int ReplayResponseSchemaVersion = 1;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    // A Supplier Return still consumes its accepted Goods Receipt quantity in every status where the
    // commercial return has not been undone. Completed permanently consumes quantity: the return has
    // concluded (via Finance reference or a NoCreditExpected inventory handoff) and nothing restores it
    // short of a linked reversal/correction. Rejected never became an effective return. Cancelled and
    // Reversed restore quantity only for their currently supported pre-downstream-consequence paths.
    // CorrectionLinked is superseded by its correction successor, which alone owns the current
    // consumption, so the original must be excluded to avoid double counting.
    private static readonly SupplierReturnStatus[] QuantityConsumingStatuses =
    [
        SupplierReturnStatus.Draft,
        SupplierReturnStatus.Submitted,
        SupplierReturnStatus.Approved,
        SupplierReturnStatus.AwaitingInventory,
        SupplierReturnStatus.InventoryHandoffRecorded,
        SupplierReturnStatus.AwaitingFinance,
        SupplierReturnStatus.FinanceReferenceRecorded,
        SupplierReturnStatus.Completed
    ];
    private readonly DbContextOptions options;

    internal SupplierReturnPersistence(DbContextOptions options)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<IReadOnlyList<SupplierReturnListRecord>> ListAsync(TenantContext tenantContext, SupplierReturnStatus? status, Guid? supplierId, CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(tenantContext);
        var query = ApplyTrustedScope(db.SupplierReturns.AsNoTracking(), tenantContext.Scope);
        if (status is { } selectedStatus)
        {
            query = query.Where(item => item.Status == selectedStatus);
        }

        if (supplierId is { } selectedSupplierId)
        {
            query = query.Where(item => item.SupplierId == selectedSupplierId);
        }

        var entities = await query.Include(item => item.Lines).ToListAsync(cancellationToken);
        return entities
            .Select(item => new SupplierReturnListRecord(
                item.Id,
                item.TenantId.Value,
                new PurchaseRequestScope(item.TenantId.Value, item.CompanyId, item.BranchId),
                item.GoodsReceiptId,
                item.PurchaseOrderId,
                item.WarehouseId,
                item.SupplierCode,
                item.SupplierName,
                item.Status,
                item.ReasonCode,
                item.CommercialOutcome,
                item.Lines.Sum(line => line.ReturnQuantity),
                item.ReturnDate,
                item.CreatedAt,
                item.Version.ToArray()))
            .OrderByDescending(item => item.CreatedAt)
            .ThenByDescending(item => item.Id)
            .ToArray();
    }

    public async Task<IReadOnlyList<SupplierReturnEligibleSourceRecord>> ListEligibleSourcesAsync(TenantContext tenantContext, CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(tenantContext);
        var receipts = await db.GoodsReceipts
            .AsNoTracking()
            .Include(item => item.Lines)
            .Where(item => item.Status == GoodsReceiptStatus.Recorded)
            .OrderBy(item => item.ReceivedDate)
            .ToListAsync(cancellationToken);
        return await ToEligibleSourcesAsync(db, receipts, cancellationToken);
    }

    public async Task<SupplierReturnEligibleSourceRecord?> FindEligibleSourceAsync(TenantContext tenantContext, Guid goodsReceiptId, CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(tenantContext);
        var receipt = await db.GoodsReceipts
            .AsNoTracking()
            .Include(item => item.Lines)
            .SingleOrDefaultAsync(item => item.Id == goodsReceiptId && item.Status == GoodsReceiptStatus.Recorded, cancellationToken);
        if (receipt is null)
        {
            return null;
        }

        var sources = await ToEligibleSourcesAsync(db, [receipt], cancellationToken);
        return sources.SingleOrDefault();
    }

    public async Task<SupplierReturnReplayProbe> ProbeReplayAsync(TenantContext tenantContext, SupplierReturnReplayQuery query, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        await using var db = CreateContext(tenantContext);
        var lookup = await FindReplayAsync(db, query, cancellationToken);
        return lookup.Outcome switch
        {
            ReplayOutcome.Replay when lookup.Record is not null => SupplierReturnReplayProbe.ForReplay(lookup.Record),
            ReplayOutcome.Conflict => SupplierReturnReplayProbe.Conflict,
            _ => SupplierReturnReplayProbe.NotFound
        };
    }

    public async Task<SupplierReturnRecord?> FindAsync(TenantContext tenantContext, Guid supplierReturnId, CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(tenantContext);
        var entity = await db.SupplierReturns
            .AsNoTracking()
            .Include(item => item.Lines)
            .Include(item => item.Evidence)
            .SingleOrDefaultAsync(item => item.Id == supplierReturnId, cancellationToken);
        return entity is null ? null : ToRecord(entity);
    }

    public async Task<SupplierReturnPersistenceResult<SupplierReturnRecord>> CreateAsync(TenantContext tenantContext, SupplierReturnCreateCommand command, SupplierReturnAuditEvidence evidence, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(evidence);
        await using var db = CreateContext(tenantContext);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var replay = await FindReplayAsync(db, ToReplayQuery(evidence), cancellationToken);
            if (replay.Outcome == ReplayOutcome.Conflict)
            {
                return Denied(SupplierReturnPersistenceOutcome.Conflict, "idempotency_conflict");
            }

            if (replay.Outcome == ReplayOutcome.Replay)
            {
                await transaction.CommitAsync(cancellationToken);
                return SupplierReturnPersistenceResult<SupplierReturnRecord>.Success(replay.Record!);
            }

            if (command.Scope.TenantId != tenantContext.TenantId.Value || command.Id == Guid.Empty || await db.SupplierReturns.AnyAsync(item => item.Id == command.Id, cancellationToken))
            {
                return Denied(SupplierReturnPersistenceOutcome.Duplicate, "supplier_return_duplicate");
            }

            var receipt = await db.GoodsReceipts
                .Include(item => item.Lines)
                .SingleOrDefaultAsync(item => item.Id == command.GoodsReceiptId, cancellationToken);
            if (receipt is null)
            {
                return Denied(SupplierReturnPersistenceOutcome.NotFound, "goods_receipt_not_found");
            }

            if (receipt.Status != GoodsReceiptStatus.Recorded || receipt.CompanyId != command.Scope.CompanyId || receipt.BranchId != command.Scope.BranchId)
            {
                return Denied(SupplierReturnPersistenceOutcome.InvalidState, "supplier_return_source_not_eligible");
            }

            var order = await db.PurchaseOrders
                .Include(item => item.Lines)
                .SingleOrDefaultAsync(item => item.Id == receipt.PurchaseOrderId, cancellationToken);
            if (order is null || order.TenantId.Value != tenantContext.TenantId.Value)
            {
                return Denied(SupplierReturnPersistenceOutcome.NotFound, "purchase_order_not_found");
            }

            var receiptLineMap = receipt.Lines.ToDictionary(item => item.Id);
            var orderLineMap = order.Lines.ToDictionary(item => item.Id);
            var returned = await ActiveReturnedByReceiptLineAsync(db, receipt.Id, null, cancellationToken);
            var entity = new SupplierReturnEntity(command, tenantContext.TenantId, order.Id, order.LatestConfirmationId, receipt.WarehouseId, order.SupplierId, order.SupplierCode, order.SupplierName, order.CurrencyCode);
            foreach (var lineCommand in command.Lines)
            {
                if (!receiptLineMap.TryGetValue(lineCommand.GoodsReceiptLineId, out var receiptLine)
                    || !orderLineMap.TryGetValue(receiptLine.PurchaseOrderLineId, out var orderLine)
                    || receiptLine.AcceptedQuantity <= 0m)
                {
                    return Denied(SupplierReturnPersistenceOutcome.InvalidState, "supplier_return_line_not_eligible");
                }

                var already = returned.TryGetValue(receiptLine.Id, out var sum) ? sum : 0m;
                var remaining = Math.Max(0m, receiptLine.AcceptedQuantity - already);
                if (lineCommand.ReturnQuantity > remaining)
                {
                    return Denied(SupplierReturnPersistenceOutcome.InvalidState, "over_return_not_allowed");
                }

                entity.Lines.Add(new SupplierReturnLineEntity(
                    tenantContext.TenantId,
                    entity.Id,
                    lineCommand,
                    receiptLine.PurchaseOrderLineId,
                    receiptLine.ProductId,
                    receiptLine.ProductSku,
                    receiptLine.ProductName,
                    receiptLine.UnitOfMeasureCode,
                    receiptLine.AcceptedQuantity,
                    remaining - lineCommand.ReturnQuantity));
            }

            foreach (var evidenceReference in command.Evidence)
            {
                entity.Evidence.Add(new SupplierReturnEvidenceEntity(Guid.NewGuid(), tenantContext.TenantId, entity.Id, evidenceReference, command.OccurredAt));
            }

            receipt.TouchVersion();
            entity.TouchVersion();
            db.SupplierReturns.Add(entity);
            db.SupplierReturnHistory.Add(new SupplierReturnHistoryEntity(evidence.EvidenceId, tenantContext.TenantId, entity.Id, SupplierReturnStatus.Draft, SupplierReturnStatus.Draft, SupplierReturnHistoryAction.Created, command.ActorId, null, evidence.CorrelationId, command.OccurredAt));
            var audit = new SupplierReturnAuditEntity(evidence);
            db.SupplierReturnAudit.Add(audit);
            await db.SaveChangesAsync(cancellationToken);
            var response = ToRecord(entity);
            audit.SetReplayResponseSnapshot(ReplayResponseSchemaVersion, SerializeReplayResponse(response));
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return SupplierReturnPersistenceResult<SupplierReturnRecord>.Success(response);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Denied(SupplierReturnPersistenceOutcome.Conflict, "concurrency_conflict");
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            return Denied(SupplierReturnPersistenceOutcome.Duplicate, "supplier_return_duplicate");
        }
        catch (DbUpdateException)
        {
            return Denied(SupplierReturnPersistenceOutcome.Failure, "persistence_unavailable");
        }
        catch
        {
            return Denied(SupplierReturnPersistenceOutcome.Failure, "persistence_unavailable");
        }
    }

    public async Task<SupplierReturnPersistenceResult<SupplierReturnRecord>> ActionAsync(TenantContext tenantContext, SupplierReturnActionCommand command, SupplierReturnAuditEvidence evidence, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(evidence);
        if (command.ExpectedVersion is null || command.ExpectedVersion.Length == 0)
        {
            return Denied(SupplierReturnPersistenceOutcome.Conflict, "concurrency_conflict");
        }

        await using var db = CreateContext(tenantContext);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var replay = await FindReplayAsync(db, ToReplayQuery(evidence), cancellationToken);
            if (replay.Outcome == ReplayOutcome.Conflict)
            {
                return Denied(SupplierReturnPersistenceOutcome.Conflict, "idempotency_conflict");
            }

            if (replay.Outcome == ReplayOutcome.Replay)
            {
                await transaction.CommitAsync(cancellationToken);
                return SupplierReturnPersistenceResult<SupplierReturnRecord>.Success(replay.Record!);
            }

            var entity = await db.SupplierReturns
                .Include(item => item.Lines)
                .Include(item => item.Evidence)
                .SingleOrDefaultAsync(item => item.Id == command.Id, cancellationToken);
            if (entity is null)
            {
                return Denied(SupplierReturnPersistenceOutcome.NotFound, "supplier_return_not_found");
            }

            if (!VersionMatches(entity.Version, command.ExpectedVersion))
            {
                return Denied(SupplierReturnPersistenceOutcome.Conflict, "concurrency_conflict");
            }

            var receipt = await db.GoodsReceipts.SingleOrDefaultAsync(item => item.Id == entity.GoodsReceiptId, cancellationToken);
            if (receipt is null)
            {
                return Denied(SupplierReturnPersistenceOutcome.NotFound, "goods_receipt_not_found");
            }

            var from = entity.Status;
            var to = from;
            var historyAction = SupplierReturnHistoryAction.Created;
            var changesEligibility = false;
            switch (command.Action)
            {
                case SupplierReturnMutationAction.Submit when from == SupplierReturnStatus.Draft:
                    to = SupplierReturnStatus.Submitted;
                    historyAction = SupplierReturnHistoryAction.Submitted;
                    break;
                case SupplierReturnMutationAction.Approve when from == SupplierReturnStatus.Submitted:
                    to = SupplierReturnStatus.AwaitingInventory;
                    historyAction = SupplierReturnHistoryAction.Approved;
                    break;
                case SupplierReturnMutationAction.Reject when from is SupplierReturnStatus.Draft or SupplierReturnStatus.Submitted:
                    to = SupplierReturnStatus.Rejected;
                    historyAction = SupplierReturnHistoryAction.Rejected;
                    changesEligibility = true;
                    break;
                case SupplierReturnMutationAction.Cancel when from is SupplierReturnStatus.Draft or SupplierReturnStatus.Submitted or SupplierReturnStatus.Approved or SupplierReturnStatus.AwaitingInventory:
                    if (entity.InventoryHandoffEvidenceId is not null || entity.FinanceReference is not null)
                    {
                        return Denied(SupplierReturnPersistenceOutcome.InvalidState, "downstream_consequence_exists");
                    }

                    to = SupplierReturnStatus.Cancelled;
                    historyAction = SupplierReturnHistoryAction.Cancelled;
                    changesEligibility = true;
                    break;
                case SupplierReturnMutationAction.RecordInventoryHandoff when from is SupplierReturnStatus.Approved or SupplierReturnStatus.AwaitingInventory:
                    if (string.IsNullOrWhiteSpace(command.HandoffReference))
                    {
                        return Denied(SupplierReturnPersistenceOutcome.InvalidState, "handoff_reference_required");
                    }

                    entity.RecordInventoryHandoff(evidence.EvidenceId, command.HandoffReference!, command.OccurredAt);
                    to = entity.CommercialOutcome == SupplierReturnCommercialOutcome.NoCreditExpected ? SupplierReturnStatus.Completed : SupplierReturnStatus.AwaitingFinance;
                    historyAction = SupplierReturnHistoryAction.InventoryHandoffRecorded;
                    break;
                case SupplierReturnMutationAction.RecordFinanceReference when from is SupplierReturnStatus.InventoryHandoffRecorded or SupplierReturnStatus.AwaitingFinance:
                    if (string.IsNullOrWhiteSpace(command.FinanceReference))
                    {
                        return Denied(SupplierReturnPersistenceOutcome.InvalidState, "finance_reference_required");
                    }

                    entity.RecordFinanceReference(command.FinanceReference!, command.FinanceCurrencyCode, command.FinanceAmount, command.OccurredAt);
                    to = SupplierReturnStatus.Completed;
                    historyAction = SupplierReturnHistoryAction.FinanceReferenceRecorded;
                    break;
                case SupplierReturnMutationAction.Reverse when from is SupplierReturnStatus.Draft or SupplierReturnStatus.Submitted or SupplierReturnStatus.Approved or SupplierReturnStatus.AwaitingInventory:
                    if (entity.InventoryHandoffEvidenceId is not null || entity.FinanceReference is not null)
                    {
                        return Denied(SupplierReturnPersistenceOutcome.InvalidState, "downstream_consequence_exists");
                    }

                    to = SupplierReturnStatus.Reversed;
                    historyAction = SupplierReturnHistoryAction.Reversed;
                    changesEligibility = true;
                    break;
                default:
                    return Denied(SupplierReturnPersistenceOutcome.InvalidState, "supplier_return_action_not_allowed");
            }

            entity.SetStatus(to, command.OccurredAt);
            entity.TouchVersion();
            if (changesEligibility)
            {
                receipt.TouchVersion();
            }

            db.SupplierReturnHistory.Add(new SupplierReturnHistoryEntity(evidence.EvidenceId, tenantContext.TenantId, entity.Id, from, to, historyAction, command.ActorId, command.Reason, evidence.CorrelationId, command.OccurredAt));
            var audit = new SupplierReturnAuditEntity(evidence);
            db.SupplierReturnAudit.Add(audit);
            await db.SaveChangesAsync(cancellationToken);
            var response = ToRecord(entity);
            audit.SetReplayResponseSnapshot(ReplayResponseSchemaVersion, SerializeReplayResponse(response));
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return SupplierReturnPersistenceResult<SupplierReturnRecord>.Success(response);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Denied(SupplierReturnPersistenceOutcome.Conflict, "concurrency_conflict");
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            return Denied(SupplierReturnPersistenceOutcome.Duplicate, "supplier_return_duplicate");
        }
        catch (DbUpdateException)
        {
            return Denied(SupplierReturnPersistenceOutcome.Failure, "persistence_unavailable");
        }
        catch
        {
            return Denied(SupplierReturnPersistenceOutcome.Failure, "persistence_unavailable");
        }
    }

    public async Task<SupplierReturnPersistenceResult<SupplierReturnRecord>> CorrectAsync(TenantContext tenantContext, SupplierReturnCreateCommand command, byte[] expectedVersion, SupplierReturnAuditEvidence evidence, CancellationToken cancellationToken = default)
    {
        if (expectedVersion is null || expectedVersion.Length == 0)
        {
            return Denied(SupplierReturnPersistenceOutcome.Conflict, "concurrency_conflict");
        }

        await using var db = CreateContext(tenantContext);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var replay = await FindReplayAsync(db, ToReplayQuery(evidence), cancellationToken);
            if (replay.Outcome == ReplayOutcome.Conflict)
            {
                return Denied(SupplierReturnPersistenceOutcome.Conflict, "idempotency_conflict");
            }

            if (replay.Outcome == ReplayOutcome.Replay)
            {
                await transaction.CommitAsync(cancellationToken);
                return SupplierReturnPersistenceResult<SupplierReturnRecord>.Success(replay.Record!);
            }

            var original = await db.SupplierReturns
                .Include(item => item.Lines)
                .Include(item => item.Evidence)
                .SingleOrDefaultAsync(item => item.Id == command.CorrectionOfId, cancellationToken);
            if (original is null)
            {
                return Denied(SupplierReturnPersistenceOutcome.NotFound, "supplier_return_not_found");
            }

            if (!VersionMatches(original.Version, expectedVersion))
            {
                return Denied(SupplierReturnPersistenceOutcome.Conflict, "concurrency_conflict");
            }

            if (original.GoodsReceiptId != command.GoodsReceiptId || original.InventoryHandoffEvidenceId is not null || original.FinanceReference is not null || original.Status is SupplierReturnStatus.Cancelled or SupplierReturnStatus.Rejected or SupplierReturnStatus.Reversed or SupplierReturnStatus.CorrectionLinked)
            {
                return Denied(SupplierReturnPersistenceOutcome.InvalidState, "correction_not_allowed");
            }

            var receipt = await db.GoodsReceipts
                .Include(item => item.Lines)
                .SingleOrDefaultAsync(item => item.Id == command.GoodsReceiptId, cancellationToken);
            var order = receipt is null ? null : await db.PurchaseOrders.Include(item => item.Lines).SingleOrDefaultAsync(item => item.Id == receipt.PurchaseOrderId, cancellationToken);
            if (receipt is null || order is null || receipt.Status != GoodsReceiptStatus.Recorded)
            {
                return Denied(SupplierReturnPersistenceOutcome.InvalidState, "supplier_return_source_not_eligible");
            }

            var activeReturned = await ActiveReturnedByReceiptLineAsync(db, receipt.Id, original.Id, cancellationToken);
            var receiptLineMap = receipt.Lines.ToDictionary(item => item.Id);
            var orderLineMap = order.Lines.ToDictionary(item => item.Id);
            var corrected = new SupplierReturnEntity(command, tenantContext.TenantId, order.Id, order.LatestConfirmationId, receipt.WarehouseId, order.SupplierId, order.SupplierCode, order.SupplierName, order.CurrencyCode);
            foreach (var lineCommand in command.Lines)
            {
                if (!receiptLineMap.TryGetValue(lineCommand.GoodsReceiptLineId, out var receiptLine) || !orderLineMap.ContainsKey(receiptLine.PurchaseOrderLineId))
                {
                    return Denied(SupplierReturnPersistenceOutcome.InvalidState, "supplier_return_line_not_eligible");
                }

                var priorOnOriginal = original.Lines.Where(item => item.GoodsReceiptLineId == receiptLine.Id).Sum(item => item.ReturnQuantity);
                var already = activeReturned.TryGetValue(receiptLine.Id, out var sum) ? sum : 0m;
                var remaining = Math.Max(0m, receiptLine.AcceptedQuantity - already + priorOnOriginal);
                if (lineCommand.ReturnQuantity > remaining)
                {
                    return Denied(SupplierReturnPersistenceOutcome.InvalidState, "over_return_not_allowed");
                }

                corrected.Lines.Add(new SupplierReturnLineEntity(tenantContext.TenantId, corrected.Id, lineCommand, receiptLine.PurchaseOrderLineId, receiptLine.ProductId, receiptLine.ProductSku, receiptLine.ProductName, receiptLine.UnitOfMeasureCode, receiptLine.AcceptedQuantity, remaining - lineCommand.ReturnQuantity));
            }

            foreach (var evidenceReference in command.Evidence)
            {
                corrected.Evidence.Add(new SupplierReturnEvidenceEntity(Guid.NewGuid(), tenantContext.TenantId, corrected.Id, evidenceReference, command.OccurredAt));
            }

            var oldStatus = original.Status;
            original.SetStatus(SupplierReturnStatus.CorrectionLinked, command.OccurredAt);
            original.TouchVersion();
            corrected.TouchVersion();
            receipt.TouchVersion();
            db.SupplierReturns.Add(corrected);
            db.SupplierReturnHistory.Add(new SupplierReturnHistoryEntity(evidence.EvidenceId, tenantContext.TenantId, original.Id, oldStatus, SupplierReturnStatus.CorrectionLinked, SupplierReturnHistoryAction.CorrectionLinked, command.ActorId, evidence.Reason, evidence.CorrelationId, command.OccurredAt));
            db.SupplierReturnHistory.Add(new SupplierReturnHistoryEntity(Guid.NewGuid(), tenantContext.TenantId, corrected.Id, SupplierReturnStatus.Draft, SupplierReturnStatus.Draft, SupplierReturnHistoryAction.Created, command.ActorId, "Correction successor", evidence.CorrelationId, command.OccurredAt));
            var oldAudit = new SupplierReturnAuditEntity(evidence);
            db.SupplierReturnAudit.Add(oldAudit);
            await db.SaveChangesAsync(cancellationToken);
            var response = ToRecord(corrected);
            oldAudit.SetReplayResponseSnapshot(ReplayResponseSchemaVersion, SerializeReplayResponse(response));
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return SupplierReturnPersistenceResult<SupplierReturnRecord>.Success(response);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Denied(SupplierReturnPersistenceOutcome.Conflict, "concurrency_conflict");
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            return Denied(SupplierReturnPersistenceOutcome.Duplicate, "supplier_return_duplicate");
        }
        catch (DbUpdateException)
        {
            return Denied(SupplierReturnPersistenceOutcome.Failure, "persistence_unavailable");
        }
        catch
        {
            return Denied(SupplierReturnPersistenceOutcome.Failure, "persistence_unavailable");
        }
    }

    public async Task<IReadOnlyList<SupplierReturnHistoryRecord>> ReadHistoryAsync(TenantContext tenantContext, Guid supplierReturnId, CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(tenantContext);
        var records = await db.SupplierReturnHistory.AsNoTracking().Where(item => item.SupplierReturnId == supplierReturnId).Select(item => new SupplierReturnHistoryRecord(item.Id, item.SupplierReturnId, item.OccurredAt, item.FromStatus, item.ToStatus, item.Action, item.ActorId, item.Reason, item.CorrelationId)).ToListAsync(cancellationToken);
        return records.OrderBy(item => item.OccurredAt).ThenBy(item => item.EvidenceId).ToArray();
    }

    public async Task<IReadOnlyList<SupplierReturnAuditEvidence>> ReadAuditAsync(TenantContext tenantContext, Guid supplierReturnId, CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(tenantContext);
        var records = await db.SupplierReturnAudit.AsNoTracking().Where(item => item.SupplierReturnId == supplierReturnId).Select(item => new SupplierReturnAuditEvidence(item.Id, item.SupplierReturnId, item.OccurredAt, item.OperationId, item.CorrelationId, item.TenantId.Value, item.ActorId, item.SessionId, item.AuthorizationPath, item.Decision, item.Reason, item.BeforeStatus, item.AfterStatus, item.CompanyId, item.BranchId, item.BeforeSummary, item.AfterSummary, item.IdempotencyKey, item.RequestFingerprint)).ToListAsync(cancellationToken);
        return records.OrderBy(item => item.OccurredAt).ThenBy(item => item.EvidenceId).ToArray();
    }

    private async Task<IReadOnlyList<SupplierReturnEligibleSourceRecord>> ToEligibleSourcesAsync(ProcurementDbContext db, IReadOnlyList<GoodsReceiptEntity> receipts, CancellationToken cancellationToken)
    {
        if (receipts.Count == 0)
        {
            return [];
        }

        var receiptIds = receipts.Select(item => item.Id).ToArray();
        var returned = await (
            from line in db.SupplierReturnLines
            join parent in db.SupplierReturns on new { line.TenantId, SupplierReturnId = line.SupplierReturnId } equals new { parent.TenantId, SupplierReturnId = parent.Id }
            where receiptIds.Contains(parent.GoodsReceiptId) && QuantityConsumingStatuses.Contains(parent.Status)
            group line.ReturnQuantity by new { parent.GoodsReceiptId, line.GoodsReceiptLineId } into g
            select new { g.Key.GoodsReceiptId, g.Key.GoodsReceiptLineId, Quantity = g.Sum() }).ToListAsync(cancellationToken);
        var returnedLookup = returned.ToDictionary(item => (item.GoodsReceiptId, item.GoodsReceiptLineId), item => item.Quantity);
        var orders = await db.PurchaseOrders.AsNoTracking().Where(item => receipts.Select(receipt => receipt.PurchaseOrderId).Contains(item.Id)).ToDictionaryAsync(item => item.Id, cancellationToken);
        var result = new List<SupplierReturnEligibleSourceRecord>(receipts.Count);
        foreach (var receipt in receipts)
        {
            if (!orders.TryGetValue(receipt.PurchaseOrderId, out var order))
            {
                continue;
            }

            var lines = receipt.Lines
                .OrderBy(item => item.Id)
                .Select(line =>
                {
                    var already = returnedLookup.TryGetValue((receipt.Id, line.Id), out var sum) ? sum : 0m;
                    return new SupplierReturnEligibleLineRecord(receipt.Id, line.Id, receipt.PurchaseOrderId, line.PurchaseOrderLineId, receipt.WarehouseId, line.ProductId, line.ProductSku, line.ProductName, line.UnitOfMeasureCode, line.AcceptedQuantity, already, Math.Max(0m, line.AcceptedQuantity - already), receipt.ReceivedDate);
                })
                .ToArray();
            result.Add(new SupplierReturnEligibleSourceRecord(receipt.Id, receipt.PurchaseOrderId, order.LatestConfirmationId, new PurchaseRequestScope(receipt.TenantId.Value, receipt.CompanyId, receipt.BranchId), receipt.WarehouseId, receipt.SupplierId, receipt.SupplierCode, receipt.SupplierName, order.CurrencyCode, lines));
        }

        return result;
    }

    private static async Task<Dictionary<Guid, decimal>> ActiveReturnedByReceiptLineAsync(ProcurementDbContext db, Guid goodsReceiptId, Guid? excludedReturnId, CancellationToken cancellationToken)
    {
        var query = from line in db.SupplierReturnLines
                    join parent in db.SupplierReturns on new { line.TenantId, SupplierReturnId = line.SupplierReturnId } equals new { parent.TenantId, SupplierReturnId = parent.Id }
                    where parent.GoodsReceiptId == goodsReceiptId && QuantityConsumingStatuses.Contains(parent.Status)
                    select new { line.GoodsReceiptLineId, line.ReturnQuantity, parent.Id };
        if (excludedReturnId is { } excluded)
        {
            query = query.Where(item => item.Id != excluded);
        }

        var rows = await query.ToListAsync(cancellationToken);
        return rows.GroupBy(item => item.GoodsReceiptLineId).ToDictionary(group => group.Key, group => group.Sum(item => item.ReturnQuantity));
    }

    private Task<ReplayLookup> FindReplayAsync(ProcurementDbContext db, SupplierReturnAuditEvidence evidence, CancellationToken cancellationToken) => FindReplayAsync(db, ToReplayQuery(evidence), cancellationToken);
    private static SupplierReturnReplayQuery ToReplayQuery(SupplierReturnAuditEvidence evidence) => new(evidence.ActorId, evidence.OperationId, evidence.IdempotencyKey, evidence.OperationId == CreateOperationId ? null : evidence.SupplierReturnId, evidence.RequestFingerprint);

    private async Task<ReplayLookup> FindReplayAsync(ProcurementDbContext db, SupplierReturnReplayQuery query, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query.IdempotencyKey))
        {
            return ReplayLookup.NotFound;
        }

        var candidates = await db.SupplierReturnAudit.Where(item => item.ActorId == query.ActorId && item.OperationId == query.OperationId && item.IdempotencyKey == query.IdempotencyKey).Select(item => new { item.Id, item.SupplierReturnId, item.OccurredAt, item.RequestFingerprint, item.ReplayResponseSchemaVersion, item.ReplayResponseSnapshotJson }).ToListAsync(cancellationToken);
        if (candidates.Count == 0)
        {
            return ReplayLookup.NotFound;
        }

        if (candidates.Select(item => item.SupplierReturnId).Distinct().Count() != 1 || query.SupplierReturnId is { } target && candidates.Any(item => item.SupplierReturnId != target) || candidates.Any(item => !string.Equals(item.RequestFingerprint, query.RequestFingerprint, StringComparison.Ordinal)))
        {
            return ReplayLookup.Conflict;
        }

        var original = candidates.OrderBy(item => item.OccurredAt).ThenBy(item => item.Id).First();
        if (original.ReplayResponseSchemaVersion != ReplayResponseSchemaVersion || string.IsNullOrWhiteSpace(original.ReplayResponseSnapshotJson))
        {
            return ReplayLookup.Conflict;
        }

        try
        {
            var snapshot = JsonSerializer.Deserialize<ReplayResponseSnapshot>(original.ReplayResponseSnapshotJson, JsonOptions);
            return snapshot is { SchemaVersion: ReplayResponseSchemaVersion, Response: not null } && snapshot.Response.Id != Guid.Empty
                ? ReplayLookup.ForReplay(snapshot.Response)
                : ReplayLookup.Conflict;
        }
        catch (JsonException)
        {
            return ReplayLookup.Conflict;
        }
    }

    private static string SerializeReplayResponse(SupplierReturnRecord response) => JsonSerializer.Serialize(new ReplayResponseSnapshot(ReplayResponseSchemaVersion, response), JsonOptions);

    private static SupplierReturnRecord ToRecord(SupplierReturnEntity entity) => new(
        entity.Id,
        entity.TenantId.Value,
        new PurchaseRequestScope(entity.TenantId.Value, entity.CompanyId, entity.BranchId),
        entity.GoodsReceiptId,
        entity.PurchaseOrderId,
        entity.SupplierConfirmationId,
        entity.WarehouseId,
        entity.SupplierId,
        entity.SupplierCode,
        entity.SupplierName,
        entity.CurrencyCode,
        entity.Status,
        entity.ReasonCode,
        entity.Condition,
        entity.CommercialOutcome,
        entity.ReasonDetail,
        entity.Notes,
        entity.ReturnDate,
        entity.CreatedAt,
        entity.UpdatedAt,
        entity.CancelledAt,
        entity.ReversedAt,
        entity.CorrectionOfId,
        entity.InventoryHandoffEvidenceId,
        entity.InventoryHandoffReference,
        entity.FinanceReference,
        entity.FinanceCurrencyCode,
        entity.FinanceAmount,
        entity.Lines.OrderBy(item => item.Id).Select(item => new SupplierReturnLineRecord(item.Id, item.GoodsReceiptLineId, item.PurchaseOrderLineId, item.ProductId, item.ProductSku, item.ProductName, item.UnitOfMeasureCode, item.AcceptedQuantityAtReturn, item.ReturnQuantity, item.EligibleQuantityAfter, item.Notes)).ToArray(),
        entity.Evidence.OrderBy(item => item.RecordedAt).ThenBy(item => item.Id).Select(item => new SupplierReturnEvidenceRecord(item.Id, item.ReferenceId, item.FileName, item.ContentType, item.Description, item.Source, item.RecordedAt)).ToArray(),
        entity.Version.ToArray());

    private static SupplierReturnPersistenceResult<SupplierReturnRecord> Denied(SupplierReturnPersistenceOutcome outcome, string code) => SupplierReturnPersistenceResult<SupplierReturnRecord>.Denied(outcome, code);
    private ProcurementDbContext CreateContext(TenantContext tenantContext) => new(options, tenantContext);

    private static IQueryable<SupplierReturnEntity> ApplyTrustedScope(IQueryable<SupplierReturnEntity> query, ScopeReference? scope)
    {
        if (scope is not { } reference)
        {
            return query;
        }

        var parts = reference.Value.Split(':', 2, StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || !Guid.TryParse(parts[1], out var id))
        {
            return query.Where(_ => false);
        }

        return parts[0] switch
        {
            "Company" => query.Where(item => item.CompanyId == id),
            "Branch" => query.Where(item => item.BranchId == id),
            "Tenant" => query,
            _ => query.Where(_ => false)
        };
    }

    private static bool VersionMatches(byte[] actual, byte[] expected) => actual.SequenceEqual(expected);

    private static bool IsUniqueConstraintViolation(DbUpdateException exception)
    {
        for (var current = exception.InnerException; current is not null; current = current.InnerException)
        {
            if (current is SqlException { Number: 2601 or 2627 } || current is SqliteException { SqliteErrorCode: 19 })
            {
                return true;
            }
        }

        return false;
    }

    private sealed record ReplayResponseSnapshot(int SchemaVersion, SupplierReturnRecord Response);
    private sealed record ReplayLookup(ReplayOutcome Outcome, SupplierReturnRecord? Record)
    {
        internal static ReplayLookup NotFound => new(ReplayOutcome.NotFound, null);
        internal static ReplayLookup Conflict => new(ReplayOutcome.Conflict, null);
        internal static ReplayLookup ForReplay(SupplierReturnRecord record) => new(ReplayOutcome.Replay, record);
    }

    private enum ReplayOutcome
    {
        NotFound = 1,
        Replay = 2,
        Conflict = 3
    }
}

#pragma warning restore CS1591
