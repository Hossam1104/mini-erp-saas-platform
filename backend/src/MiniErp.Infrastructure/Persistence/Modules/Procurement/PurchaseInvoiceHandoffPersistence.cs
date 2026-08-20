#pragma warning disable CS1591

using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.Modules.Procurement;
using MiniErp.Contracts.Modules.Procurement;

namespace MiniErp.Infrastructure.Persistence.Modules.Procurement;

public sealed class PurchaseInvoiceHandoffPersistence : IPurchaseInvoiceHandoffPersistence
{
    private const string CreateOperationId = "procurement.invoice-handoff.create";
    private const int ReplayResponseSchemaVersion = 1;
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly DbContextOptions options;

    internal PurchaseInvoiceHandoffPersistence(DbContextOptions options)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<IReadOnlyList<PurchaseInvoiceHandoffListRecord>> ListAsync(
        TenantContext tenantContext,
        PurchaseInvoiceHandoffStatus? status,
        CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(tenantContext);
        var query = ApplyTrustedScope(db.PurchaseInvoiceHandoffs.AsNoTracking(), tenantContext.Scope);
        if (status is { } selectedStatus)
        {
            query = query.Where(item => item.Status == selectedStatus);
        }

        var entities = await query.Include(item => item.Lines).ToListAsync(cancellationToken);
        var records = entities
            .Select(item => new PurchaseInvoiceHandoffListRecord(
                item.Id,
                item.TenantId.Value,
                new PurchaseRequestScope(item.TenantId.Value, item.CompanyId, item.BranchId),
                item.PurchaseOrderId,
                item.Status,
                item.SupplierCode,
                item.SupplierName,
                item.CurrencyCode,
                item.Lines.Sum(line => line.LineAmount),
                item.Lines.Count,
                item.CreatedAt,
                item.Version.ToArray()))
            .ToArray();
        return records.OrderByDescending(item => item.CreatedAt).ThenByDescending(item => item.Id).ToArray();
    }

    public async Task<IReadOnlyList<PurchaseInvoiceHandoffEligibleSourceRecord>> ListEligibleSourcesAsync(
        TenantContext tenantContext,
        CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(tenantContext);
        var orderIds = await db.GoodsReceipts
            .Where(item => item.Status == GoodsReceiptStatus.Recorded)
            .Select(item => item.PurchaseOrderId)
            .Distinct()
            .ToListAsync(cancellationToken);
        if (orderIds.Count == 0)
        {
            return [];
        }

        var orders = await db.PurchaseOrders
            .AsNoTracking()
            .Include(item => item.Lines)
            .Where(item => orderIds.Contains(item.Id))
            .ToListAsync(cancellationToken);
        var records = new List<PurchaseInvoiceHandoffEligibleSourceRecord>(orders.Count);
        foreach (var order in orders)
        {
            var record = await ToEligibleSourceRecordAsync(db, order, cancellationToken);
            if (record.Lines.Count > 0)
            {
                records.Add(record);
            }
        }

        return records;
    }

    public async Task<PurchaseInvoiceHandoffEligibleSourceRecord?> FindEligibleSourceAsync(
        TenantContext tenantContext,
        Guid purchaseOrderId,
        CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(tenantContext);
        var order = await db.PurchaseOrders
            .AsNoTracking()
            .Include(item => item.Lines)
            .SingleOrDefaultAsync(item => item.Id == purchaseOrderId, cancellationToken);
        return order is null ? null : await ToEligibleSourceRecordAsync(db, order, cancellationToken);
    }

    public async Task<PurchaseInvoiceHandoffReplayProbe> ProbeReplayAsync(
        TenantContext tenantContext,
        PurchaseInvoiceHandoffReplayQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);
        await using var db = CreateContext(tenantContext);
        var lookup = await FindReplayAsync(db, query, cancellationToken);
        return lookup.Outcome switch
        {
            ReplayOutcome.Replay when lookup.Record is not null => PurchaseInvoiceHandoffReplayProbe.ForReplay(lookup.Record),
            ReplayOutcome.Conflict => PurchaseInvoiceHandoffReplayProbe.Conflict,
            _ => PurchaseInvoiceHandoffReplayProbe.NotFound
        };
    }

    public async Task<PurchaseInvoiceHandoffRecord?> FindAsync(
        TenantContext tenantContext,
        Guid purchaseInvoiceHandoffId,
        CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(tenantContext);
        var entity = await db.PurchaseInvoiceHandoffs
            .AsNoTracking()
            .Include(item => item.Lines)
            .Include(item => item.Sources)
            .SingleOrDefaultAsync(item => item.Id == purchaseInvoiceHandoffId, cancellationToken);
        return entity is null ? null : ToRecord(entity);
    }

    public async Task<PurchaseInvoiceHandoffPersistenceResult<PurchaseInvoiceHandoffRecord>> CreateAsync(
        TenantContext tenantContext,
        PurchaseInvoiceHandoffCreateCommand command,
        PurchaseInvoiceHandoffAuditEvidence evidence,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(evidence);
        await using var db = CreateContext(tenantContext);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var replay = await FindReplayAsync(db, evidence, cancellationToken);
            if (replay.Outcome == ReplayOutcome.Conflict)
            {
                return Denied(PurchaseInvoiceHandoffPersistenceOutcome.Conflict, "idempotency_conflict");
            }

            if (replay.Outcome == ReplayOutcome.Replay)
            {
                await transaction.CommitAsync(cancellationToken);
                return PurchaseInvoiceHandoffPersistenceResult<PurchaseInvoiceHandoffRecord>.Success(replay.Record!);
            }

            if (command.Scope.TenantId != tenantContext.TenantId.Value || command.Id == Guid.Empty)
            {
                return Denied(PurchaseInvoiceHandoffPersistenceOutcome.Duplicate, "invoice_handoff_duplicate");
            }

            if (await db.PurchaseInvoiceHandoffs.AnyAsync(item => item.Id == command.Id, cancellationToken))
            {
                return Denied(PurchaseInvoiceHandoffPersistenceOutcome.Duplicate, "invoice_handoff_duplicate");
            }

            var order = await db.PurchaseOrders
                .Include(item => item.Lines)
                .SingleOrDefaultAsync(item => item.Id == command.PurchaseOrderId, cancellationToken);
            if (order is null)
            {
                return Denied(PurchaseInvoiceHandoffPersistenceOutcome.NotFound, "purchase_order_not_found");
            }

            var source = await ToEligibleSourceRecordAsync(db, order, cancellationToken);
            var eligibleLines = source.Lines.ToDictionary(item => (item.GoodsReceiptId, item.GoodsReceiptLineId));
            var poLineMap = order.Lines.ToDictionary(item => item.Id);
            var entity = new PurchaseInvoiceHandoffEntity(command, tenantContext.TenantId, order.SupplierId, order.SupplierCode, order.SupplierName, order.CurrencyCode);

            var quantityByPurchaseOrderLineId = new Dictionary<Guid, decimal>();
            foreach (var requested in command.Sources)
            {
                if (!eligibleLines.TryGetValue((requested.GoodsReceiptId, requested.GoodsReceiptLineId), out var eligibleLine))
                {
                    return Denied(PurchaseInvoiceHandoffPersistenceOutcome.InvalidState, "invoice_handoff_line_not_eligible");
                }

                if (requested.Quantity > eligibleLine.RemainingHandoffQuantity)
                {
                    return Denied(PurchaseInvoiceHandoffPersistenceOutcome.InvalidState, "over_handoff_not_allowed");
                }

                entity.Sources.Add(new PurchaseInvoiceHandoffSourceEntity(
                    tenantContext.TenantId,
                    entity.Id,
                    Guid.NewGuid(),
                    requested.GoodsReceiptId,
                    requested.GoodsReceiptLineId,
                    eligibleLine.PurchaseOrderLineId,
                    requested.Quantity));
                quantityByPurchaseOrderLineId[eligibleLine.PurchaseOrderLineId] =
                    quantityByPurchaseOrderLineId.GetValueOrDefault(eligibleLine.PurchaseOrderLineId) + requested.Quantity;
            }

            foreach (var (purchaseOrderLineId, handoffQuantity) in quantityByPurchaseOrderLineId)
            {
                if (!poLineMap.TryGetValue(purchaseOrderLineId, out var poLine))
                {
                    return Denied(PurchaseInvoiceHandoffPersistenceOutcome.InvalidState, "invoice_handoff_line_not_eligible");
                }

                var (taxAmount, lineAmount) = ComputeLineAmount(handoffQuantity, poLine.UnitPrice, poLine.TaxRatePercentage);
                entity.Lines.Add(new PurchaseInvoiceHandoffLineEntity(
                    tenantContext.TenantId,
                    entity.Id,
                    Guid.NewGuid(),
                    purchaseOrderLineId,
                    poLine.ProductId,
                    poLine.ProductSku,
                    poLine.ProductName,
                    poLine.UnitOfMeasureCode,
                    handoffQuantity,
                    poLine.UnitPrice,
                    poLine.TaxRatePercentage,
                    taxAmount,
                    lineAmount));
            }

            var receiptIds = command.Sources.Select(s => s.GoodsReceiptId).Distinct().ToArray();
            var receipts = await db.GoodsReceipts.Where(r => receiptIds.Contains(r.Id)).ToListAsync(cancellationToken);
            foreach (var receipt in receipts)
            {
                receipt.TouchVersion();
            }

            order.TouchVersion();
            entity.TouchVersion();
            db.PurchaseInvoiceHandoffs.Add(entity);
            db.PurchaseInvoiceHandoffHistory.Add(new PurchaseInvoiceHandoffHistoryEntity(
                evidence.EvidenceId,
                tenantContext.TenantId,
                entity.Id,
                PurchaseInvoiceHandoffStatus.Recorded,
                PurchaseInvoiceHandoffStatus.Recorded,
                PurchaseInvoiceHandoffHistoryAction.Recorded,
                command.CreatedByActorId,
                null,
                evidence.CorrelationId,
                command.OccurredAt));
            var audit = new PurchaseInvoiceHandoffAuditEntity(evidence);
            db.PurchaseInvoiceHandoffAudit.Add(audit);
            await db.SaveChangesAsync(cancellationToken);
            var response = ToRecord(entity);
            audit.SetReplayResponseSnapshot(ReplayResponseSchemaVersion, SerializeReplayResponse(response));
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return PurchaseInvoiceHandoffPersistenceResult<PurchaseInvoiceHandoffRecord>.Success(response);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Denied(PurchaseInvoiceHandoffPersistenceOutcome.Conflict, "concurrency_conflict");
        }
        catch (DbUpdateException exception) when (IsUniqueConstraintViolation(exception))
        {
            return Denied(PurchaseInvoiceHandoffPersistenceOutcome.Duplicate, "invoice_handoff_duplicate");
        }
        catch (DbUpdateException)
        {
            return Denied(PurchaseInvoiceHandoffPersistenceOutcome.Failure, "persistence_unavailable");
        }
        catch
        {
            return Denied(PurchaseInvoiceHandoffPersistenceOutcome.Failure, "persistence_unavailable");
        }
    }

    public async Task<PurchaseInvoiceHandoffPersistenceResult<PurchaseInvoiceHandoffRecord>> CancelAsync(
        TenantContext tenantContext,
        PurchaseInvoiceHandoffActionCommand command,
        PurchaseInvoiceHandoffAuditEvidence evidence,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(evidence);
        if (command.ExpectedVersion is null || command.ExpectedVersion.Length == 0)
        {
            return Denied(PurchaseInvoiceHandoffPersistenceOutcome.Conflict, "concurrency_conflict");
        }

        await using var db = CreateContext(tenantContext);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var entity = await db.PurchaseInvoiceHandoffs
                .Include(item => item.Lines)
                .Include(item => item.Sources)
                .SingleOrDefaultAsync(item => item.Id == command.Id, cancellationToken);
            if (entity is null)
            {
                return Denied(PurchaseInvoiceHandoffPersistenceOutcome.NotFound, "invoice_handoff_not_found");
            }

            var replay = await FindReplayAsync(db, evidence, cancellationToken);
            if (replay.Outcome == ReplayOutcome.Conflict)
            {
                return Denied(PurchaseInvoiceHandoffPersistenceOutcome.Conflict, "idempotency_conflict");
            }

            if (replay.Outcome == ReplayOutcome.Replay)
            {
                await transaction.CommitAsync(cancellationToken);
                return PurchaseInvoiceHandoffPersistenceResult<PurchaseInvoiceHandoffRecord>.Success(replay.Record!);
            }

            if (!VersionMatches(entity.Version, command.ExpectedVersion))
            {
                return Denied(PurchaseInvoiceHandoffPersistenceOutcome.Conflict, "concurrency_conflict");
            }

            if (entity.Status != PurchaseInvoiceHandoffStatus.Recorded)
            {
                return Denied(PurchaseInvoiceHandoffPersistenceOutcome.InvalidState, "cancel_not_allowed");
            }

            var fromStatus = entity.Status;
            entity.Cancel(command.Reason ?? string.Empty, command.OccurredAt);
            entity.TouchVersion();
            db.PurchaseInvoiceHandoffHistory.Add(new PurchaseInvoiceHandoffHistoryEntity(
                Guid.NewGuid(),
                tenantContext.TenantId,
                entity.Id,
                fromStatus,
                PurchaseInvoiceHandoffStatus.Cancelled,
                PurchaseInvoiceHandoffHistoryAction.Cancelled,
                command.ActorId,
                command.Reason,
                evidence.CorrelationId,
                command.OccurredAt));
            var audit = new PurchaseInvoiceHandoffAuditEntity(evidence with
            {
                BeforeStatus = evidence.BeforeStatus ?? fromStatus,
                AfterStatus = evidence.AfterStatus ?? entity.Status,
                BeforeSummary = evidence.BeforeSummary ?? fromStatus.ToString(),
                AfterSummary = evidence.AfterSummary ?? entity.Status.ToString()
            });
            db.PurchaseInvoiceHandoffAudit.Add(audit);
            await db.SaveChangesAsync(cancellationToken);
            var response = ToRecord(entity);
            audit.SetReplayResponseSnapshot(ReplayResponseSchemaVersion, SerializeReplayResponse(response));
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return PurchaseInvoiceHandoffPersistenceResult<PurchaseInvoiceHandoffRecord>.Success(response);
        }
        catch (DbUpdateConcurrencyException)
        {
            return Denied(PurchaseInvoiceHandoffPersistenceOutcome.Conflict, "concurrency_conflict");
        }
        catch (DbUpdateException)
        {
            return Denied(PurchaseInvoiceHandoffPersistenceOutcome.Failure, "persistence_unavailable");
        }
        catch
        {
            return Denied(PurchaseInvoiceHandoffPersistenceOutcome.Failure, "persistence_unavailable");
        }
    }

    public async Task<IReadOnlyList<PurchaseInvoiceHandoffHistoryRecord>> ReadHistoryAsync(
        TenantContext tenantContext,
        Guid purchaseInvoiceHandoffId,
        CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(tenantContext);
        var records = await db.PurchaseInvoiceHandoffHistory
            .AsNoTracking()
            .Where(item => item.PurchaseInvoiceHandoffId == purchaseInvoiceHandoffId)
            .Select(item => new PurchaseInvoiceHandoffHistoryRecord(
                item.Id,
                item.PurchaseInvoiceHandoffId,
                item.OccurredAt,
                item.FromStatus,
                item.ToStatus,
                item.Action,
                item.ActorId,
                item.Reason,
                item.CorrelationId))
            .ToListAsync(cancellationToken);
        return records.OrderBy(item => item.OccurredAt).ThenBy(item => item.EvidenceId).ToArray();
    }

    public async Task<IReadOnlyList<PurchaseInvoiceHandoffAuditRecord>> ReadAuditAsync(
        TenantContext tenantContext,
        Guid purchaseInvoiceHandoffId,
        CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(tenantContext);
        var records = await db.PurchaseInvoiceHandoffAudit
            .AsNoTracking()
            .Where(item => item.PurchaseInvoiceHandoffId == purchaseInvoiceHandoffId)
            .Select(item => new PurchaseInvoiceHandoffAuditRecord(
                item.Id,
                item.PurchaseInvoiceHandoffId,
                item.OccurredAt,
                item.OperationId,
                item.CorrelationId,
                item.TenantId.Value,
                item.ActorId,
                item.SessionId,
                item.AuthorizationPath,
                item.Decision,
                item.Reason,
                item.BeforeStatus,
                item.AfterStatus,
                item.CompanyId,
                item.BranchId,
                item.BeforeSummary,
                item.AfterSummary,
                item.IdempotencyKey))
            .ToListAsync(cancellationToken);
        return records.OrderBy(item => item.OccurredAt).ThenBy(item => item.EvidenceId).ToArray();
    }

    // A Purchase Order line's stored `TaxAmount` is the total tax for its full ordered quantity, so it
    // cannot be reused for a partial handoff; tax is reprorated here from `TaxRatePercentage` against the
    // handoff-quantity subtotal.
    private static (decimal? TaxAmount, decimal LineAmount) ComputeLineAmount(decimal handoffQuantity, decimal unitPrice, decimal? taxRatePercentage)
    {
        var subtotal = handoffQuantity * unitPrice;
        var taxAmount = taxRatePercentage is { } rate
            ? Math.Round(subtotal * rate / 100m, 2, MidpointRounding.AwayFromZero)
            : (decimal?)null;
        return (taxAmount, subtotal + (taxAmount ?? 0m));
    }

    private async Task<PurchaseInvoiceHandoffEligibleSourceRecord> ToEligibleSourceRecordAsync(
        ProcurementDbContext db,
        PurchaseOrderEntity order,
        CancellationToken cancellationToken)
    {
        var receipts = await db.GoodsReceipts
            .AsNoTracking()
            .Include(item => item.Lines)
            .Where(item => item.PurchaseOrderId == order.Id && item.Status == GoodsReceiptStatus.Recorded)
            .ToListAsync(cancellationToken);
        var receiptLines = receipts
            .SelectMany(receipt => receipt.Lines
                .Where(line => line.AcceptedQuantity > 0)
                .Select(line => (Receipt: receipt, Line: line)))
            .ToArray();
        var alreadyHandedOff = await AlreadyHandedOffByLineAsync(db, receiptLines.Select(item => item.Line.Id).ToArray(), cancellationToken);
        var poLineMap = order.Lines.ToDictionary(item => item.Id);
        var lines = receiptLines
            .Where(item => poLineMap.ContainsKey(item.Line.PurchaseOrderLineId))
            .OrderBy(item => item.Receipt.ReceivedDate).ThenBy(item => item.Line.Id)
            .Select(item =>
            {
                var poLine = poLineMap[item.Line.PurchaseOrderLineId];
                var already = alreadyHandedOff.TryGetValue(item.Line.Id, out var sum) ? sum : 0m;
                var remaining = Math.Max(0m, item.Line.AcceptedQuantity - already);
                return new PurchaseInvoiceHandoffEligibleLineRecord(
                    item.Receipt.Id,
                    item.Line.Id,
                    item.Line.PurchaseOrderLineId,
                    item.Line.ProductId,
                    item.Line.ProductSku,
                    item.Line.ProductName,
                    poLine.UnitOfMeasureId,
                    item.Line.UnitOfMeasureCode,
                    item.Receipt.ReceivedDate,
                    item.Line.AcceptedQuantity,
                    already,
                    remaining,
                    poLine.UnitPrice,
                    poLine.TaxRatePercentage,
                    poLine.TaxAmount);
            })
            .ToArray();
        return new PurchaseInvoiceHandoffEligibleSourceRecord(
            order.Id,
            new PurchaseRequestScope(order.TenantId.Value, order.CompanyId, order.BranchId),
            order.SupplierId,
            order.SupplierCode,
            order.SupplierName,
            order.CurrencyId,
            order.CurrencyCode,
            order.CurrencyName,
            lines);
    }

    private static async Task<Dictionary<Guid, decimal>> AlreadyHandedOffByLineAsync(
        ProcurementDbContext db,
        IReadOnlyCollection<Guid> goodsReceiptLineIds,
        CancellationToken cancellationToken)
    {
        if (goodsReceiptLineIds.Count == 0)
        {
            return [];
        }

        var grouped = await (
            from source in db.PurchaseInvoiceHandoffSources
            join handoff in db.PurchaseInvoiceHandoffs
                on new { source.TenantId, source.PurchaseInvoiceHandoffId } equals new { handoff.TenantId, PurchaseInvoiceHandoffId = handoff.Id }
            where handoff.Status != PurchaseInvoiceHandoffStatus.Cancelled && goodsReceiptLineIds.Contains(source.GoodsReceiptLineId)
            group source.Quantity by source.GoodsReceiptLineId into g
            select new { GoodsReceiptLineId = g.Key, Handed = g.Sum() })
            .ToListAsync(cancellationToken);
        return grouped.ToDictionary(item => item.GoodsReceiptLineId, item => item.Handed);
    }

    private Task<ReplayLookup> FindReplayAsync(
        ProcurementDbContext db,
        PurchaseInvoiceHandoffAuditEvidence evidence,
        CancellationToken cancellationToken) =>
        FindReplayAsync(db, ToReplayQuery(evidence), cancellationToken);

    // Create has no pre-existing target: `evidence.PurchaseInvoiceHandoffId` is a freshly generated id for
    // this call and must not be compared against the previously created target's id.
    private static PurchaseInvoiceHandoffReplayQuery ToReplayQuery(PurchaseInvoiceHandoffAuditEvidence evidence) => new(
        evidence.ActorId,
        evidence.OperationId,
        evidence.IdempotencyKey,
        evidence.OperationId == CreateOperationId ? null : evidence.PurchaseInvoiceHandoffId,
        evidence.RequestFingerprint);

    private async Task<ReplayLookup> FindReplayAsync(
        ProcurementDbContext db,
        PurchaseInvoiceHandoffReplayQuery query,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query.IdempotencyKey))
        {
            return ReplayLookup.NotFound;
        }

        var replayCandidates = await db.PurchaseInvoiceHandoffAudit
            .Where(item => item.ActorId == query.ActorId
                && item.OperationId == query.OperationId
                && item.IdempotencyKey == query.IdempotencyKey)
            .Select(item => new
            {
                item.Id,
                item.PurchaseInvoiceHandoffId,
                item.OccurredAt,
                item.RequestFingerprint,
                item.ReplayResponseSchemaVersion,
                item.ReplayResponseSnapshotJson
            })
            .ToListAsync(cancellationToken);
        if (replayCandidates.Count == 0)
        {
            return ReplayLookup.NotFound;
        }

        if (replayCandidates.Select(item => item.PurchaseInvoiceHandoffId).Distinct().Count() != 1)
        {
            return ReplayLookup.Conflict;
        }

        if (query.PurchaseInvoiceHandoffId is { } targetId
            && replayCandidates.Any(item => item.PurchaseInvoiceHandoffId != targetId))
        {
            return ReplayLookup.Conflict;
        }

        if (replayCandidates.Any(item => !string.Equals(item.RequestFingerprint, query.RequestFingerprint, StringComparison.Ordinal)))
        {
            return ReplayLookup.Conflict;
        }

        var original = replayCandidates
            .OrderBy(item => item.OccurredAt)
            .ThenBy(item => item.Id)
            .First();
        if (original.ReplayResponseSchemaVersion != ReplayResponseSchemaVersion
            || string.IsNullOrWhiteSpace(original.ReplayResponseSnapshotJson))
        {
            return ReplayLookup.Conflict;
        }

        try
        {
            var snapshot = JsonSerializer.Deserialize<ReplayResponseSnapshot>(original.ReplayResponseSnapshotJson, JsonOptions);
            if (snapshot is null
                || snapshot.SchemaVersion != ReplayResponseSchemaVersion
                || snapshot.Response is null
                || snapshot.Response.Id != original.PurchaseInvoiceHandoffId)
            {
                return ReplayLookup.Conflict;
            }

            return ReplayLookup.ForReplay(snapshot.Response);
        }
        catch (JsonException)
        {
            return ReplayLookup.Conflict;
        }
    }

    private static string SerializeReplayResponse(PurchaseInvoiceHandoffRecord response) =>
        JsonSerializer.Serialize(new ReplayResponseSnapshot(ReplayResponseSchemaVersion, response), JsonOptions);

    private static PurchaseInvoiceHandoffRecord ToRecord(PurchaseInvoiceHandoffEntity entity) => new(
        entity.Id,
        entity.TenantId.Value,
        new PurchaseRequestScope(entity.TenantId.Value, entity.CompanyId, entity.BranchId),
        entity.PurchaseOrderId,
        entity.CreatedByActorId,
        entity.Status,
        entity.SupplierId,
        entity.SupplierCode,
        entity.SupplierName,
        entity.CurrencyCode,
        entity.SupplierInvoiceReference,
        entity.SupplierInvoiceDate,
        entity.Notes,
        entity.CreatedAt,
        entity.UpdatedAt,
        entity.CancelledAt,
        entity.CancellationReason,
        entity.Lines
            .OrderBy(item => item.Id)
            .Select(item => new PurchaseInvoiceHandoffLineRecord(
                item.Id,
                item.PurchaseOrderLineId,
                item.ProductId,
                item.ProductSku,
                item.ProductName,
                item.UnitOfMeasureCode,
                item.HandoffQuantity,
                item.UnitPrice,
                item.TaxRatePercentage,
                item.TaxAmount,
                item.LineAmount))
            .ToArray(),
        entity.Sources
            .OrderBy(item => item.Id)
            .Select(item => new PurchaseInvoiceHandoffSourceRecord(
                item.Id,
                item.GoodsReceiptId,
                item.GoodsReceiptLineId,
                item.PurchaseOrderLineId,
                item.Quantity))
            .ToArray(),
        entity.Version.ToArray());

    private ProcurementDbContext CreateContext(TenantContext tenantContext) => new(options, tenantContext);

    private static IQueryable<PurchaseInvoiceHandoffEntity> ApplyTrustedScope(IQueryable<PurchaseInvoiceHandoffEntity> query, ScopeReference? scope)
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
            if (current is SqlException { Number: 2601 or 2627 }
                || current is SqliteException { SqliteErrorCode: 19 })
            {
                return true;
            }
        }

        return false;
    }

    private static PurchaseInvoiceHandoffPersistenceResult<PurchaseInvoiceHandoffRecord> Denied(PurchaseInvoiceHandoffPersistenceOutcome outcome, string code) =>
        PurchaseInvoiceHandoffPersistenceResult<PurchaseInvoiceHandoffRecord>.Denied(outcome, code);

    private enum ReplayOutcome
    {
        NotFound,
        Replay,
        Conflict
    }

    private sealed record ReplayLookup(ReplayOutcome Outcome, PurchaseInvoiceHandoffRecord? Record)
    {
        internal static readonly ReplayLookup NotFound = new(ReplayOutcome.NotFound, null);
        internal static readonly ReplayLookup Conflict = new(ReplayOutcome.Conflict, null);
        internal static ReplayLookup ForReplay(PurchaseInvoiceHandoffRecord record) => new(ReplayOutcome.Replay, record);
    }

    private sealed record ReplayResponseSnapshot(int SchemaVersion, PurchaseInvoiceHandoffRecord Response);
}

#pragma warning restore CS1591
