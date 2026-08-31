#pragma warning disable CS1591

using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.Modules.Inventory;
using MiniErp.App.Modules.Sales;
using MiniErp.Contracts.Modules.Inventory;

namespace MiniErp.Infrastructure.Persistence.Modules.Inventory;

public sealed class CustomerReturnInventoryPersistence(
    DbContextOptions options,
    ISalesCustomerReturnSourceProvider salesReturns) : IInventoryCustomerReturnPersistence
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);
    private InventoryDbContext Create(InventoryRequestContext context) => new(options, context.TenantContext);

    public async Task<InventoryCustomerReturnResponse?> GetAsync(InventoryRequestContext context, Guid salesCustomerReturnId, CancellationToken cancellationToken = default)
    {
        await using var db = Create(context);
        var item = await db.CustomerReturns.AsNoTracking().Include(value => value.Lines).SingleOrDefaultAsync(value => value.SalesCustomerReturnId == salesCustomerReturnId, cancellationToken);
        return item is null || !InScope(context, item.CompanyId, item.BranchId) ? null : ToResponse(item);
    }

    public async Task<InventoryOperationResult<InventoryCustomerReturnResponse>> ReceiveAsync(InventoryRequestContext context, Guid salesCustomerReturnId, byte[] expectedVersion, InventoryCustomerReturnReceiptRequest request, string? idempotencyKey, string fingerprint, CancellationToken cancellationToken = default)
    {
        await using var db = Create(context);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var replay = await ReplayAsync(db, context, "inventory.customer-return.receive", idempotencyKey, fingerprint, cancellationToken);
        if (replay is not null) return replay;
        var source = await salesReturns.GetCustomerReturnSourceAsync(context.TenantContext, salesCustomerReturnId, cancellationToken);
        if (source is null) return Failure("sales_return_not_found");
        if (source.Version is null || !source.Version.SequenceEqual(expectedVersion)) return Failure("concurrency_conflict");
        var existing = await db.CustomerReturns.Include(item => item.Lines).SingleOrDefaultAsync(item => item.SalesCustomerReturnId == salesCustomerReturnId, cancellationToken);
        if (existing is not null) return Failure("inventory_customer_return_duplicate");
        var entity = new InventoryCustomerReturnEntity(context.TenantId, Guid.NewGuid(), salesCustomerReturnId, source.CompanyId, source.BranchId, source.WarehouseId, request, context.ActorId, DateTimeOffset.UtcNow);
        foreach (var line in request.Lines)
        {
            var sourceLine = source.Lines.SingleOrDefault(item => item.OrderLineId == line.OrderLineId);
            if (sourceLine is null || line.Quantity > sourceLine.ReturnQuantity) return Failure("return_quantity_conflict");
            entity.Lines.Add(new InventoryCustomerReturnLineEntity(context.TenantId, Guid.NewGuid(), entity.Id, line.OrderLineId, line.Quantity));
        }
        db.CustomerReturns.Add(entity);
        AddAudit(db, context, "inventory.customer-return.receive", entity.Id, "Succeeded", null, null, "physical receipt recorded", idempotencyKey, fingerprint);
        await db.SaveChangesAsync(cancellationToken);
        return await SaveResultAsync(db, transaction, context, "inventory.customer-return.receive", idempotencyKey, fingerprint, ToResponse(entity), cancellationToken);
    }

    public async Task<InventoryOperationResult<InventoryCustomerReturnResponse>> InspectAsync(InventoryRequestContext context, Guid salesCustomerReturnId, byte[] expectedVersion, InventoryCustomerReturnInspectionRequest request, string? idempotencyKey, string fingerprint, CancellationToken cancellationToken = default)
    {
        await using var db = Create(context);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var replay = await ReplayAsync(db, context, "inventory.customer-return.inspect", idempotencyKey, fingerprint, cancellationToken);
        if (replay is not null) return replay;
        var entity = await db.CustomerReturns.Include(item => item.Lines).SingleOrDefaultAsync(item => item.SalesCustomerReturnId == salesCustomerReturnId, cancellationToken);
        if (entity is null) return Failure("inventory_customer_return_not_found");
        if (!entity.Version.SequenceEqual(expectedVersion)) return Failure("concurrency_conflict");
        if (entity.Status is not (InventoryCustomerReturnStatus.Received or InventoryCustomerReturnStatus.ReconciliationRequired)) return Failure("inventory_customer_return_transition_invalid");
        var source = await salesReturns.GetCustomerReturnSourceAsync(context.TenantContext, salesCustomerReturnId, cancellationToken);
        if (source is null) return Failure("sales_return_not_found");
        var requested = request.Lines.ToDictionary(item => item.OrderLineId);
        var resolved = new List<(InventoryCustomerReturnLineEntity Line, InventoryCustomerReturnInspectionLineRequest Request, Guid DeliveryMovementId, decimal UnitCost)>();
        foreach (var item in entity.Lines)
        {
            if (!requested.TryGetValue(item.OrderLineId, out var inspection)) continue;
            if (inspection.Quantity > item.ReceivedQuantity - item.DispositionedQuantity) return Failure("disposition_quantity_conflict");
            var sourceLine = source.Lines.SingleOrDefault(value => value.OrderLineId == item.OrderLineId);
            if (sourceLine is null) return Failure("return_source_mismatch");
            if (inspection.Disposition == InventoryCustomerReturnDisposition.Restockable)
            {
                var deliveryMovement = await db.StockMovements.AsNoTracking().Where(value => value.SourceType == InventoryMovementSourceType.SalesDelivery && value.Direction == InventoryMovementDirection.Outbound && value.SourceDocumentId == source.DeliveryId && value.SourceLineId == item.OrderLineId && value.ProductId == sourceLine.ProductId && value.UnitOfMeasureId == sourceLine.UnitOfMeasureId && value.WarehouseId == entity.WarehouseId && value.TenantId == context.TenantId).OrderByDescending(value => value.LedgerSequence).FirstOrDefaultAsync(cancellationToken);
                if (deliveryMovement is null || deliveryMovement.ValuationStatus != InventoryValuationStatus.Known || deliveryMovement.UnitCost is null || string.IsNullOrWhiteSpace(deliveryMovement.CurrencyCode))
                {
                    entity.SetInspected(request.InspectionEvidenceReference, InventoryCustomerReturnStatus.ReconciliationRequired, DateTimeOffset.UtcNow);
                    AddAudit(db, context, "inventory.customer-return.inspect", entity.Id, "Blocked", "delivery_cost_evidence_missing", entity.Status.ToString(), "ReconciliationRequired", idempotencyKey, fingerprint);
                    await db.SaveChangesAsync(cancellationToken);
                    await transaction.CommitAsync(cancellationToken);
                    return Failure("delivery_cost_evidence_missing");
                }
                resolved.Add((item, inspection, deliveryMovement.Id, deliveryMovement.UnitCost.Value));
            }
        }
        var now = DateTimeOffset.UtcNow;
        foreach (var value in resolved)
        {
            var sourceLine = source.Lines.Single(item => item.OrderLineId == value.Line.OrderLineId);
            if (value.Line.MovementId is not null && value.Line.MovementId != Guid.Empty) continue;
            var deliveryMovement = await db.StockMovements.AsNoTracking().SingleAsync(item => item.Id == value.DeliveryMovementId, cancellationToken);
            var movement = new InventoryStockMovementEntity(context.TenantId, Guid.NewGuid(), entity.CompanyId, entity.BranchId, entity.WarehouseId, deliveryMovement.WarehouseCode, deliveryMovement.WarehouseName, sourceLine.ProductId, sourceLine.ProductSku, sourceLine.ProductName, sourceLine.UnitOfMeasureId, sourceLine.UnitOfMeasureCode, InventoryMovementDirection.Inbound, value.Request.Quantity, value.UnitCost, deliveryMovement.CurrencyCode, InventoryValuationStatus.Known, deliveryMovement.TrackingIdentity, InventoryMovementSourceType.CustomerReturn, entity.SalesCustomerReturnId, value.Line.Id, null, entity.ReceiptDate ?? DateOnly.FromDateTime(now.UtcDateTime), context.ActorId, context.CorrelationId?.Value ?? "inventory", now, sourceReference: $"sales-delivery:{source.DeliveryId:D};movement:{deliveryMovement.Id:D}");
            db.StockMovements.Add(movement);
            value.Line.Dispose(value.Request.Quantity, value.Request.Disposition, value.Request.Notes, movement.Id, deliveryMovement.Id, value.UnitCost);
        }
        foreach (var item in entity.Lines)
        {
            if (requested.TryGetValue(item.OrderLineId, out var inspection) && item.MovementId is null) item.Dispose(inspection.Quantity, inspection.Disposition, inspection.Notes, null, null, null);
        }
        var complete = entity.Lines.All(item => item.DispositionedQuantity == item.ReceivedQuantity);
        entity.SetInspected(request.InspectionEvidenceReference, complete ? InventoryCustomerReturnStatus.Posted : InventoryCustomerReturnStatus.Inspected, now);
        AddAudit(db, context, "inventory.customer-return.inspect", entity.Id, "Succeeded", null, "Received", entity.Status.ToString(), idempotencyKey, fingerprint);
        await db.SaveChangesAsync(cancellationToken);
        return await SaveResultAsync(db, transaction, context, "inventory.customer-return.inspect", idempotencyKey, fingerprint, ToResponse(entity), cancellationToken);
    }

    private static InventoryCustomerReturnResponse ToResponse(InventoryCustomerReturnEntity item) => new(item.Id, item.TenantId.Value, item.SalesCustomerReturnId, item.CompanyId, item.BranchId, item.WarehouseId, item.Status, item.PhysicalEvidenceReference, item.InspectionEvidenceReference, item.HandoffState, item.ReceiptDate, item.PostedAt, item.Lines.Select(line => new InventoryCustomerReturnLineResponse(line.Id, line.OrderLineId, line.ReceivedQuantity, line.DispositionedQuantity, line.Disposition, line.MovementId, line.DeliveryMovementId, line.DeliveryUnitCost, line.Notes)).ToArray(), item.Version);
    private static bool InScope(InventoryRequestContext context, Guid companyId, Guid? branchId) => context.TenantContext.Scope is not { } scope || ScopeMatches(scope.Value, companyId, branchId);
    private static bool ScopeMatches(string value, Guid companyId, Guid? branchId) { var parts = value.Split(':', 2); return parts.Length == 2 && Guid.TryParse(parts[1], out var id) && (parts[0] switch { "Tenant" => true, "Company" => companyId == id, "Branch" => branchId == id, _ => false }); }
    private static InventoryOperationResult<InventoryCustomerReturnResponse> Failure(string code) => InventoryOperationResult<InventoryCustomerReturnResponse>.Failure(code);
    private static void AddAudit(InventoryDbContext db, InventoryRequestContext context, string operation, Guid id, string decision, string? reason, string? before, string? after, string? key, string? fingerprint) => db.Audit.Add(new InventoryAuditEntity(context.TenantId, Guid.NewGuid(), "customer-return", id, operation, context.ActorId, context.SessionId, context.AuthorizationPath.ToString(), decision, reason, context.CorrelationId?.Value ?? string.Empty, key, fingerprint, before, after, DateTimeOffset.UtcNow));
    private static async Task<InventoryOperationResult<InventoryCustomerReturnResponse>?> ReplayAsync(InventoryDbContext db, InventoryRequestContext context, string operation, string? key, string fingerprint, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;
        var row = await db.Idempotency.AsNoTracking().SingleOrDefaultAsync(item => item.ActorId == context.ActorId && item.OperationId == operation && item.Key == key, cancellationToken);
        if (row is null) return null;
        return row.Fingerprint == fingerprint ? InventoryOperationResult<InventoryCustomerReturnResponse>.Success(JsonSerializer.Deserialize<InventoryCustomerReturnResponse>(row.SnapshotJson, Json)!) : Failure("idempotency_conflict");
    }
    private static async Task<InventoryOperationResult<InventoryCustomerReturnResponse>> SaveResultAsync(InventoryDbContext db, IDbContextTransaction transaction, InventoryRequestContext context, string operation, string? key, string fingerprint, InventoryCustomerReturnResponse response, CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(key)) db.Idempotency.Add(new InventoryIdempotencyEntity(context.TenantId, Guid.NewGuid(), context.ActorId, operation, key!, fingerprint, "customer-return", response.SalesCustomerReturnId, JsonSerializer.Serialize(response, Json), DateTimeOffset.UtcNow));
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return InventoryOperationResult<InventoryCustomerReturnResponse>.Success(response);
    }
}

#pragma warning restore CS1591
