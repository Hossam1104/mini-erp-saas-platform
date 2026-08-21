#pragma warning disable CS1591

using System.Data;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.Modules.Inventory;
using MiniErp.Contracts.Modules.Inventory;

namespace MiniErp.Infrastructure.Persistence.Modules.Inventory;

internal sealed class InventoryPersistence(DbContextOptions options) : IInventoryPersistence
{
    private InventoryDbContext CreateContext(InventoryRequestContext context) => new(options, context.TenantContext);

    public async Task<IReadOnlyList<InventoryMovementRecord>> ListMovementsAsync(InventoryRequestContext context, InventoryScope? scope = null, Guid? productId = null, CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(context);
        var query = db.StockMovements.AsNoTracking().AsQueryable();
        if (scope is not null) query = query.Where(item => item.CompanyId == scope.CompanyId && item.BranchId == scope.BranchId && item.WarehouseId == scope.WarehouseId);
        if (productId.HasValue) query = query.Where(item => item.ProductId == productId.Value);
        var values = await query.ToListAsync(cancellationToken);
        return values.OrderByDescending(item => item.PostedAt).Select(ToMovement.Compile()).ToArray();
    }

    public async Task<InventoryMovementRecord?> FindMovementAsync(InventoryRequestContext context, Guid movementId, CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(context);
        return await db.StockMovements.AsNoTracking().Where(item => item.Id == movementId).Select(ToMovement).SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<InventoryOpeningBalanceRecord>> ListOpeningBalancesAsync(InventoryRequestContext context, InventoryScope? scope = null, CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(context);
        var query = db.OpeningBalances.AsNoTracking().Include(item => item.Rows).AsQueryable();
        if (scope is not null) query = query.Where(item => item.CompanyId == scope.CompanyId && item.BranchId == scope.BranchId && item.WarehouseId == scope.WarehouseId);
        var values = await query.ToListAsync(cancellationToken);
        values = values.OrderByDescending(item => item.CreatedAt).ToList();
        return values.Select(ToOpening).ToArray();
    }

    public async Task<InventoryOpeningBalanceRecord?> FindOpeningBalanceAsync(InventoryRequestContext context, Guid id, CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(context);
        var value = await db.OpeningBalances.AsNoTracking().Include(item => item.Rows).SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        return value is null ? null : ToOpening(value);
    }

    public async Task<InventoryOpeningBalanceRecord?> CreateOpeningBalanceAsync(InventoryRequestContext context, InventoryOpeningBalanceCommand command, CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(context);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var replay = await ReadReplayAsync<InventoryOpeningBalanceRecord>(db, context, "inventory.opening.create", command.IdempotencyKey, command.RequestFingerprint, cancellationToken);
            if (replay.Handled) return replay.Value;
            var batch = new InventoryOpeningBalanceEntity(context.TenantId, command.Id, command.Scope.CompanyId, command.Scope.BranchId, command.Scope.WarehouseId, command.WarehouseCode, command.WarehouseName, command.AsOfDate, command.SourceOwner, command.SourceSystem, command.ExtractedAt, command.SourceReference, command.ActorId, command.OccurredAt);
            db.OpeningBalances.Add(batch);
            foreach (var row in command.Rows)
            {
                var status = row.ValidationCode is null ? InventoryOpeningRowStatus.Valid : InventoryOpeningRowStatus.Quarantined;
                batch.Rows.Add(new InventoryOpeningBalanceRowEntity(context.TenantId, row.Id, batch.Id, row.ProductId, row.Product?.Sku ?? string.Empty, row.Product?.Name ?? string.Empty, row.UnitOfMeasureId, row.Product?.BaseUnitOfMeasureCode ?? string.Empty, row.Quantity, row.UnitCost, row.CurrencyCode, row.TrackingIdentity, row.SourceLineReference, status, row.ValidationCode));
            }
            db.OpeningBalanceHistory.Add(new InventoryOpeningBalanceHistoryEntity(context.TenantId, Guid.NewGuid(), batch.Id, InventoryOpeningBalanceStatus.Draft, InventoryOpeningBalanceStatus.Draft, "created", command.ActorId, null, command.CorrelationId, command.OccurredAt));
            AddAudit(db, context, "opening-balance", batch.Id, "inventory.opening.create", command.ActorId, "Succeeded", null, command.CorrelationId, command.IdempotencyKey, command.RequestFingerprint, null, "draft-created", command.OccurredAt);
            await db.SaveChangesAsync(cancellationToken);
            var result = ToOpening(await db.OpeningBalances.AsNoTracking().Include(item => item.Rows).SingleAsync(item => item.Id == batch.Id, cancellationToken));
            AddReplay(db, context, "inventory.opening.create", command.IdempotencyKey, command.RequestFingerprint, "opening-balance", result.Id, result, command.OccurredAt);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch (DbUpdateConcurrencyException) { return null; }
        catch (DbUpdateException) { return null; }
    }

    public Task<InventoryOpeningBalanceRecord?> ValidateOpeningBalanceAsync(InventoryRequestContext context, Guid id, byte[] expectedVersion, Guid actorId, string? reason, string correlationId, string? idempotencyKey, string fingerprint, CancellationToken cancellationToken = default) =>
        ActOpeningAsync(context, id, expectedVersion, actorId, reason, correlationId, idempotencyKey, fingerprint, "inventory.opening.validate", (batch, now) =>
        {
            var valid = batch.Rows.Where(item => item.Status is InventoryOpeningRowStatus.Valid or InventoryOpeningRowStatus.Pending).ToArray();
            foreach (var row in valid) row.Validate(InventoryOpeningRowStatus.Valid, null);
            foreach (var row in batch.Rows.Where(item => item.Status == InventoryOpeningRowStatus.Quarantined)) row.Validate(InventoryOpeningRowStatus.Quarantined, row.ValidationCode);
            return valid.Length > 0 ? InventoryOpeningBalanceStatus.Validated : InventoryOpeningBalanceStatus.Draft;
        }, cancellationToken);

    public Task<InventoryOpeningBalanceRecord?> PostOpeningBalanceAsync(InventoryRequestContext context, Guid id, byte[] expectedVersion, Guid actorId, string? reason, string correlationId, string? idempotencyKey, string fingerprint, CancellationToken cancellationToken = default) =>
        PostOpeningAsync(context, id, expectedVersion, actorId, reason, correlationId, idempotencyKey, fingerprint, cancellationToken);

    public Task<InventoryOpeningBalanceRecord?> CorrectOpeningBalanceAsync(InventoryRequestContext context, Guid id, byte[] expectedVersion, Guid actorId, string? reason, string correlationId, string? idempotencyKey, string fingerprint, CancellationToken cancellationToken = default) =>
        CorrectOpeningAsync(context, id, expectedVersion, actorId, reason, correlationId, idempotencyKey, fingerprint, cancellationToken);

    private async Task<InventoryOpeningBalanceRecord?> ActOpeningAsync(InventoryRequestContext context, Guid id, byte[] expectedVersion, Guid actorId, string? reason, string correlationId, string? idempotencyKey, string fingerprint, string operationId, Func<InventoryOpeningBalanceEntity, DateTimeOffset, InventoryOpeningBalanceStatus> transition, CancellationToken cancellationToken)
    {
        await using var db = CreateContext(context);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var replay = await ReadReplayAsync<InventoryOpeningBalanceRecord>(db, context, operationId, idempotencyKey, fingerprint, cancellationToken);
            if (replay.Handled) return replay.Value;
            var batch = await db.OpeningBalances.Include(item => item.Rows).SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
            if (batch is null || !batch.Version.SequenceEqual(expectedVersion)) return null;
            var from = batch.Status;
            var to = transition(batch, DateTimeOffset.UtcNow);
            if (to == from) return null;
            batch.SetStatus(to, DateTimeOffset.UtcNow);
            batch.TouchVersion();
            db.OpeningBalanceHistory.Add(new InventoryOpeningBalanceHistoryEntity(context.TenantId, Guid.NewGuid(), id, from, to, operationId[(operationId.LastIndexOf('.') + 1)..], actorId, reason, correlationId, DateTimeOffset.UtcNow));
            AddAudit(db, context, "opening-balance", id, operationId, actorId, "Succeeded", reason, correlationId, idempotencyKey, fingerprint, from.ToString(), to.ToString(), DateTimeOffset.UtcNow);
            await db.SaveChangesAsync(cancellationToken);
            var result = ToOpening(await db.OpeningBalances.AsNoTracking().Include(item => item.Rows).SingleAsync(item => item.Id == id, cancellationToken));
            AddReplay(db, context, operationId, idempotencyKey, fingerprint, "opening-balance", result.Id, result, DateTimeOffset.UtcNow);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch (DbUpdateConcurrencyException) { return null; }
        catch (DbUpdateException) { return null; }
    }

    private async Task<InventoryOpeningBalanceRecord?> PostOpeningAsync(InventoryRequestContext context, Guid id, byte[] expectedVersion, Guid actorId, string? reason, string correlationId, string? idempotencyKey, string fingerprint, CancellationToken cancellationToken)
    {
        await using var db = CreateContext(context);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var replay = await ReadReplayAsync<InventoryOpeningBalanceRecord>(db, context, "inventory.opening.post", idempotencyKey, fingerprint, cancellationToken);
            if (replay.Handled) return replay.Value;
            var batch = await db.OpeningBalances.Include(item => item.Rows).SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
            if (batch is null || !batch.Version.SequenceEqual(expectedVersion) || batch.Status != InventoryOpeningBalanceStatus.Validated) return null;
            var rows = batch.Rows.Where(item => item.Status == InventoryOpeningRowStatus.Valid).ToArray();
            if (rows.Length == 0) return null;
            var now = DateTimeOffset.UtcNow;
            foreach (var row in rows)
            {
                db.StockMovements.Add(new InventoryStockMovementEntity(context.TenantId, Guid.NewGuid(), batch.CompanyId, batch.BranchId, batch.WarehouseId, batch.WarehouseCode, batch.WarehouseName, row.ProductId, row.ProductSku, row.ProductName, row.UnitOfMeasureId, row.UnitOfMeasureCode, InventoryMovementDirection.Inbound, row.Quantity, row.UnitCost, row.CurrencyCode, row.TrackingIdentity, InventoryMovementSourceType.OpeningBalance, batch.Id, row.Id, null, batch.AsOfDate, actorId, correlationId, now));
                row.MarkPosted(now);
            }
            var from = batch.Status; batch.SetStatus(InventoryOpeningBalanceStatus.Posted, now); batch.TouchVersion();
            db.OpeningBalanceHistory.Add(new InventoryOpeningBalanceHistoryEntity(context.TenantId, Guid.NewGuid(), id, from, batch.Status, "posted", actorId, reason, correlationId, now));
            AddAudit(db, context, "opening-balance", id, "inventory.opening.post", actorId, "Succeeded", reason, correlationId, idempotencyKey, fingerprint, from.ToString(), "Posted", now);
            await db.SaveChangesAsync(cancellationToken);
            var result = ToOpening(await db.OpeningBalances.AsNoTracking().Include(item => item.Rows).SingleAsync(item => item.Id == id, cancellationToken));
            AddReplay(db, context, "inventory.opening.post", idempotencyKey, fingerprint, "opening-balance", id, result, now);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch (DbUpdateConcurrencyException) { return null; }
        catch (DbUpdateException) { return null; }
    }

    private async Task<InventoryOpeningBalanceRecord?> CorrectOpeningAsync(InventoryRequestContext context, Guid id, byte[] expectedVersion, Guid actorId, string? reason, string correlationId, string? idempotencyKey, string fingerprint, CancellationToken cancellationToken)
    {
        await using var db = CreateContext(context);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var replay = await ReadReplayAsync<InventoryOpeningBalanceRecord>(db, context, "inventory.opening.correct", idempotencyKey, fingerprint, cancellationToken);
            if (replay.Handled) return replay.Value;
            var batch = await db.OpeningBalances.Include(item => item.Rows).SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
            if (batch is null || !batch.Version.SequenceEqual(expectedVersion) || batch.Status != InventoryOpeningBalanceStatus.Posted) return null;
            var original = await db.StockMovements.Where(item => item.SourceType == InventoryMovementSourceType.OpeningBalance && item.SourceDocumentId == id).ToListAsync(cancellationToken);
            var now = DateTimeOffset.UtcNow;
            var plannedReversals = new Dictionary<(Guid ProductId, Guid UnitOfMeasureId, string? TrackingIdentity), decimal>();
            foreach (var movement in original)
            {
                var signedOnHand = await SignedQuantityAsync(db, movement.CompanyId, movement.BranchId, movement.WarehouseId, movement.ProductId, movement.UnitOfMeasureId, movement.TrackingIdentity, cancellationToken);
                var key = (movement.ProductId, movement.UnitOfMeasureId, movement.TrackingIdentity);
                plannedReversals.TryGetValue(key, out var planned);
                if (signedOnHand - planned < movement.Quantity) return null;
                plannedReversals[key] = planned + movement.Quantity;
                db.StockMovements.Add(new InventoryStockMovementEntity(context.TenantId, Guid.NewGuid(), batch.CompanyId, batch.BranchId, batch.WarehouseId, batch.WarehouseCode, batch.WarehouseName, movement.ProductId, movement.ProductSku, movement.ProductName, movement.UnitOfMeasureId, movement.UnitOfMeasureCode, InventoryMovementDirection.Outbound, movement.Quantity, movement.UnitCost, movement.CurrencyCode, movement.TrackingIdentity, InventoryMovementSourceType.Correction, id, Guid.NewGuid(), movement.Id, batch.AsOfDate, actorId, correlationId, now));
            }
            foreach (var row in batch.Rows.Where(item => item.Status == InventoryOpeningRowStatus.Posted)) row.MarkCorrected();
            var from = batch.Status; batch.SetStatus(InventoryOpeningBalanceStatus.Corrected, now); batch.TouchVersion();
            db.OpeningBalanceHistory.Add(new InventoryOpeningBalanceHistoryEntity(context.TenantId, Guid.NewGuid(), id, from, batch.Status, "corrected", actorId, reason, correlationId, now));
            AddAudit(db, context, "opening-balance", id, "inventory.opening.correct", actorId, "Succeeded", reason, correlationId, idempotencyKey, fingerprint, from.ToString(), "Corrected", now);
            await db.SaveChangesAsync(cancellationToken);
            var result = ToOpening(await db.OpeningBalances.AsNoTracking().Include(item => item.Rows).SingleAsync(item => item.Id == id, cancellationToken));
            AddReplay(db, context, "inventory.opening.correct", idempotencyKey, fingerprint, "opening-balance", id, result, now);
            await db.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); return result;
        }
        catch (DbUpdateConcurrencyException) { return null; }
        catch (DbUpdateException) { return null; }
    }

    public async Task<IReadOnlyList<InventoryOpeningBalanceHistoryRecord>> ReadOpeningHistoryAsync(InventoryRequestContext context, Guid id, CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(context);
        var values = await db.OpeningBalanceHistory.AsNoTracking().Where(item => item.OpeningBalanceId == id).ToListAsync(cancellationToken);
        return values.OrderBy(item => item.OccurredAt).Select(item => new InventoryOpeningBalanceHistoryRecord(item.Id, item.OpeningBalanceId, item.FromStatus, item.ToStatus, item.Action, item.ActorId, item.Reason, item.CorrelationId, item.OccurredAt, item.Version)).ToArray();
    }

    public async Task<IReadOnlyList<InventoryReservationRecord>> ListReservationsAsync(InventoryRequestContext context, InventoryScope? scope = null, Guid? productId = null, CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(context); var query = db.Reservations.AsNoTracking().AsQueryable();
        if (scope is not null) query = query.Where(item => item.CompanyId == scope.CompanyId && item.BranchId == scope.BranchId && item.WarehouseId == scope.WarehouseId);
        if (productId.HasValue) query = query.Where(item => item.ProductId == productId.Value);
        var values = await query.ToListAsync(cancellationToken);
        return values.OrderByDescending(item => item.CreatedAt).Select(ToReservation.Compile()).ToArray();
    }

    public async Task<InventoryReservationRecord?> FindReservationAsync(InventoryRequestContext context, Guid id, CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(context); return await db.Reservations.AsNoTracking().Where(item => item.Id == id).Select(ToReservation).SingleOrDefaultAsync(cancellationToken);
    }

    public async Task<InventoryReservationRecord?> CreateReservationAsync(InventoryRequestContext context, InventoryReservationCommand command, decimal availableQuantity, CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(context); await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var replay = await ReadReplayAsync<InventoryReservationRecord>(db, context, "inventory.reservation.create", command.IdempotencyKey, command.RequestFingerprint, cancellationToken); if (replay.Handled) return replay.Value;
            var trackingKey = command.TrackingIdentity ?? string.Empty;
            var anchor = await db.ConcurrencyAnchors.SingleOrDefaultAsync(item => item.CompanyId == command.Scope.CompanyId && item.BranchId == command.Scope.BranchId && item.WarehouseId == command.Scope.WarehouseId && item.ProductId == command.ProductId && item.UnitOfMeasureId == command.UnitOfMeasureId && item.TrackingKey == trackingKey, cancellationToken);
            if (anchor is null)
            {
                anchor = new InventoryConcurrencyAnchorEntity(context.TenantId, Guid.NewGuid(), command.Scope.CompanyId, command.Scope.BranchId, command.Scope.WarehouseId, command.ProductId, command.UnitOfMeasureId, trackingKey);
                anchor.TouchVersion();
                db.ConcurrencyAnchors.Add(anchor);
            }
            else
            {
                anchor.TouchVersion();
            }

            // Force the anchor write before calculating availability so concurrent
            // reservations for the same stock identity serialize on this row.
            await db.SaveChangesAsync(cancellationToken);
            var available = await CalculateAvailableAsync(db, command.Scope, command.ProductId, command.UnitOfMeasureId, command.TrackingIdentity, cancellationToken);
            if (command.RequestedQuantity > available && !command.AllowPartialAllocation) return null;
            var reserved = Math.Min(command.RequestedQuantity, Math.Max(0, available)); var unallocated = command.RequestedQuantity - reserved; var now = command.OccurredAt;
            var reservation = new InventoryReservationEntity(context.TenantId, command.Id, command.Scope.CompanyId, command.Scope.BranchId, command.Scope.WarehouseId, command.WarehouseCode, command.WarehouseName, command.ProductId, command.Product.Sku, command.Product.Name, command.UnitOfMeasureId, command.Product.BaseUnitOfMeasureCode, command.TrackingIdentity, command.SourceType, command.SourceReference, command.RequestedQuantity, reserved, unallocated, command.ActorId, now);
            db.Reservations.Add(reservation); db.ReservationHistory.Add(new InventoryReservationHistoryEntity(context.TenantId, Guid.NewGuid(), reservation.Id, InventoryReservationAction.Created, reserved, reserved, unallocated, command.ActorId, null, command.CorrelationId, now));
            AddAudit(db, context, "reservation", reservation.Id, "inventory.reservation.create", command.ActorId, "Succeeded", null, command.CorrelationId, command.IdempotencyKey, command.RequestFingerprint, null, $"reserved:{reserved}", now);
            await db.SaveChangesAsync(cancellationToken);
            var result = await db.Reservations.AsNoTracking().Where(item => item.Id == reservation.Id).Select(ToReservation).SingleAsync(cancellationToken);
            AddReplay(db, context, "inventory.reservation.create", command.IdempotencyKey, command.RequestFingerprint, "reservation", result.Id, result, now); await db.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); return result;
        }
        catch (DbUpdateConcurrencyException) { return null; }
        catch (DbUpdateException) { return null; }
    }

    public Task<InventoryReservationRecord?> ReduceReservationAsync(InventoryRequestContext context, Guid id, byte[] expectedVersion, decimal quantity, Guid actorId, string? reason, string correlationId, string? idempotencyKey, string fingerprint, CancellationToken cancellationToken = default) =>
        ActReservationAsync(context, id, expectedVersion, quantity, actorId, reason, correlationId, idempotencyKey, fingerprint, "inventory.reservation.reduce", (reservation, now) => { if (reservation.Status != InventoryReservationStatus.Active || quantity > reservation.ReservedQuantity) return false; reservation.Reduce(quantity, now); reservation.TouchVersion(); return true; }, cancellationToken);

    public Task<InventoryReservationRecord?> ReleaseReservationAsync(InventoryRequestContext context, Guid id, byte[] expectedVersion, Guid actorId, string? reason, string correlationId, string? idempotencyKey, string fingerprint, CancellationToken cancellationToken = default) =>
        ActReservationAsync(context, id, expectedVersion, 0, actorId, reason, correlationId, idempotencyKey, fingerprint, "inventory.reservation.release", (reservation, now) => { if (reservation.Status != InventoryReservationStatus.Active) return false; reservation.Release(now); reservation.TouchVersion(); return true; }, cancellationToken);

    private async Task<InventoryReservationRecord?> ActReservationAsync(InventoryRequestContext context, Guid id, byte[] expectedVersion, decimal quantity, Guid actorId, string? reason, string correlationId, string? idempotencyKey, string fingerprint, string operationId, Func<InventoryReservationEntity, DateTimeOffset, bool> mutate, CancellationToken cancellationToken)
    {
        await using var db = CreateContext(context); await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var replay = await ReadReplayAsync<InventoryReservationRecord>(db, context, operationId, idempotencyKey, fingerprint, cancellationToken); if (replay.Handled) return replay.Value;
            var reservation = await db.Reservations.SingleOrDefaultAsync(item => item.Id == id, cancellationToken); if (reservation is null || !reservation.Version.SequenceEqual(expectedVersion) || !mutate(reservation, DateTimeOffset.UtcNow)) return null;
            var now = DateTimeOffset.UtcNow; var action = operationId.EndsWith("release", StringComparison.Ordinal) ? InventoryReservationAction.Released : InventoryReservationAction.Reduced;
            db.ReservationHistory.Add(new InventoryReservationHistoryEntity(context.TenantId, Guid.NewGuid(), id, action, quantity == 0 ? reservation.RequestedQuantity : quantity, reservation.ReservedQuantity, reservation.UnallocatedQuantity, actorId, reason, correlationId, now));
            AddAudit(db, context, "reservation", id, operationId, actorId, "Succeeded", reason, correlationId, idempotencyKey, fingerprint, "active", reservation.Status.ToString(), now);
            await db.SaveChangesAsync(cancellationToken); var result = await db.Reservations.AsNoTracking().Where(item => item.Id == id).Select(ToReservation).SingleAsync(cancellationToken);
            AddReplay(db, context, operationId, idempotencyKey, fingerprint, "reservation", result.Id, result, now); await db.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); return result;
        }
        catch (DbUpdateConcurrencyException) { return null; }
        catch (DbUpdateException) { return null; }
    }

    public async Task<IReadOnlyList<InventoryReservationHistoryRecord>> ReadReservationHistoryAsync(InventoryRequestContext context, Guid id, CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(context); var values = await db.ReservationHistory.AsNoTracking().Where(item => item.ReservationId == id).ToListAsync(cancellationToken); return values.OrderBy(item => item.OccurredAt).Select(item => new InventoryReservationHistoryRecord(item.Id, item.ReservationId, item.Action, item.Quantity, item.ReservedQuantityAfter, item.UnallocatedQuantityAfter, item.ActorId, item.Reason, item.CorrelationId, item.OccurredAt, item.Version)).ToArray();
    }

    public async Task<InventoryAvailabilityRecord?> GetAvailabilityAsync(InventoryRequestContext context, InventoryScope scope, Guid productId, Guid unitOfMeasureId, string? trackingIdentity, InventoryProductReference product, InventoryWarehouseOption warehouse, CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(context); var onHand = await SignedQuantityAsync(db, scope.CompanyId, scope.BranchId, scope.WarehouseId, productId, unitOfMeasureId, trackingIdentity, cancellationToken); var reserved = await db.Reservations.Where(item => item.CompanyId == scope.CompanyId && item.BranchId == scope.BranchId && item.WarehouseId == scope.WarehouseId && item.ProductId == productId && item.UnitOfMeasureId == unitOfMeasureId && item.TrackingIdentity == trackingIdentity && item.Status == InventoryReservationStatus.Active).SumAsync(item => (decimal?)item.ReservedQuantity, cancellationToken) ?? 0;
        var anchor = await db.ConcurrencyAnchors.AsNoTracking().FirstOrDefaultAsync(item => item.CompanyId == scope.CompanyId && item.BranchId == scope.BranchId && item.WarehouseId == scope.WarehouseId && item.ProductId == productId && item.UnitOfMeasureId == unitOfMeasureId && item.TrackingKey == (trackingIdentity ?? string.Empty), cancellationToken);
        return new InventoryAvailabilityRecord(context.TenantId.Value, scope.CompanyId, scope.BranchId, scope.WarehouseId, warehouse.Code, warehouse.Name, productId, product.Sku, product.Name, unitOfMeasureId, product.BaseUnitOfMeasureCode, trackingIdentity, product.TrackingEnabled, onHand, reserved, Math.Max(0, onHand - reserved), 0, 0, 0, DateTimeOffset.UtcNow, anchor?.Version ?? []);
    }

    public async Task<IReadOnlyList<InventoryAuditRecord>> ReadAuditAsync(InventoryRequestContext context, string resourceType, Guid resourceId, CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(context); var values = await db.Audit.AsNoTracking().Where(item => item.ResourceType == resourceType && item.ResourceId == resourceId).ToListAsync(cancellationToken); return values.OrderBy(item => item.OccurredAt).Select(item => new InventoryAuditRecord(item.Id, item.TenantId.Value, item.ResourceType, item.ResourceId, item.OperationId, item.ActorId, item.SessionId, item.AuthorizationPath, item.Decision, item.Reason, item.CorrelationId, item.IdempotencyKey, item.RequestFingerprint, item.BeforeSummary, item.AfterSummary, item.OccurredAt, item.Version)).ToArray();
    }

    private static async Task<decimal> SignedQuantityAsync(InventoryDbContext db, Guid companyId, Guid? branchId, Guid warehouseId, Guid productId, Guid uomId, string? trackingIdentity, CancellationToken cancellationToken)
    {
        var inbound = await db.StockMovements.Where(item => item.CompanyId == companyId && item.BranchId == branchId && item.WarehouseId == warehouseId && item.ProductId == productId && item.UnitOfMeasureId == uomId && item.TrackingIdentity == trackingIdentity && item.Direction == InventoryMovementDirection.Inbound).SumAsync(item => (decimal?)item.Quantity, cancellationToken) ?? 0;
        var outbound = await db.StockMovements.Where(item => item.CompanyId == companyId && item.BranchId == branchId && item.WarehouseId == warehouseId && item.ProductId == productId && item.UnitOfMeasureId == uomId && item.TrackingIdentity == trackingIdentity && item.Direction == InventoryMovementDirection.Outbound).SumAsync(item => (decimal?)item.Quantity, cancellationToken) ?? 0;
        return inbound - outbound;
    }

    private static async Task<decimal> CalculateAvailableAsync(InventoryDbContext db, InventoryScope scope, Guid productId, Guid uomId, string? trackingIdentity, CancellationToken cancellationToken)
    {
        var onHand = await SignedQuantityAsync(db, scope.CompanyId, scope.BranchId, scope.WarehouseId, productId, uomId, trackingIdentity, cancellationToken);
        var reserved = await db.Reservations.Where(item => item.CompanyId == scope.CompanyId && item.BranchId == scope.BranchId && item.WarehouseId == scope.WarehouseId && item.ProductId == productId && item.UnitOfMeasureId == uomId && item.TrackingIdentity == trackingIdentity && item.Status == InventoryReservationStatus.Active).SumAsync(item => (decimal?)item.ReservedQuantity, cancellationToken) ?? 0;
        return Math.Max(0, onHand - reserved);
    }

    private static void AddAudit(InventoryDbContext db, InventoryRequestContext context, string resourceType, Guid resourceId, string operationId, Guid actorId, string decision, string? reason, string correlationId, string? idempotencyKey, string? fingerprint, string? before, string? after, DateTimeOffset at) => db.Audit.Add(new InventoryAuditEntity(context.TenantId, Guid.NewGuid(), resourceType, resourceId, operationId, actorId, context.SessionId, context.AuthorizationPath.ToString(), decision, reason, correlationId, idempotencyKey, fingerprint, before, after, at));

    private static void AddReplay<T>(InventoryDbContext db, InventoryRequestContext context, string operationId, string? key, string fingerprint, string resourceType, Guid resourceId, T snapshot, DateTimeOffset at)
    { if (!string.IsNullOrWhiteSpace(key)) db.Idempotency.Add(new InventoryIdempotencyEntity(context.TenantId, Guid.NewGuid(), context.ActorId, operationId, key!, fingerprint, resourceType, resourceId, JsonSerializer.Serialize(snapshot), at)); }

    private static async Task<(bool Handled, T? Value)> ReadReplayAsync<T>(InventoryDbContext db, InventoryRequestContext context, string operationId, string? key, string fingerprint, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(key)) return (false, default);
        var entry = await db.Idempotency.AsNoTracking().SingleOrDefaultAsync(item => item.ActorId == context.ActorId && item.OperationId == operationId && item.Key == key, cancellationToken);
        if (entry is null) return (false, default);
        return entry.Fingerprint == fingerprint ? (true, JsonSerializer.Deserialize<T>(entry.SnapshotJson)) : (true, default);
    }

    private static readonly System.Linq.Expressions.Expression<Func<InventoryStockMovementEntity, InventoryMovementRecord>> ToMovement = item => new InventoryMovementRecord(item.Id, item.TenantId.Value, item.CompanyId, item.BranchId, item.WarehouseId, item.WarehouseCode, item.WarehouseName, item.ProductId, item.ProductSku, item.ProductName, item.UnitOfMeasureId, item.UnitOfMeasureCode, item.Direction, item.Quantity, item.UnitCost, item.CurrencyCode, item.TrackingIdentity, item.SourceType, item.SourceDocumentId, item.SourceLineId, item.CorrectionOfMovementId, item.EffectiveDate, item.ActorId, item.CorrelationId, item.PostedAt, item.Version);
    private static readonly System.Linq.Expressions.Expression<Func<InventoryReservationEntity, InventoryReservationRecord>> ToReservation = item => new InventoryReservationRecord(item.Id, item.TenantId.Value, item.CompanyId, item.BranchId, item.WarehouseId, item.WarehouseCode, item.WarehouseName, item.ProductId, item.ProductSku, item.ProductName, item.UnitOfMeasureId, item.UnitOfMeasureCode, item.TrackingIdentity, item.SourceType, item.SourceReference, item.RequestedQuantity, item.ReservedQuantity, item.UnallocatedQuantity, item.Status, item.ActorId, item.CreatedAt, item.UpdatedAt, item.Version);

    private static InventoryOpeningBalanceRecord ToOpening(InventoryOpeningBalanceEntity item) => new(item.Id, item.TenantId.Value, item.CompanyId, item.BranchId, item.WarehouseId, item.WarehouseCode, item.WarehouseName, item.AsOfDate, item.SourceOwner, item.SourceSystem, item.ExtractedAt, item.SourceReference, item.Status, item.Rows.Count, item.Rows.Count(row => row.Status is InventoryOpeningRowStatus.Valid or InventoryOpeningRowStatus.Posted), item.Rows.Count(row => row.Status == InventoryOpeningRowStatus.Quarantined), item.Rows.Where(row => row.Status is InventoryOpeningRowStatus.Valid or InventoryOpeningRowStatus.Posted).Sum(row => row.Quantity), item.CreatedAt, item.UpdatedAt, item.Rows.OrderBy(row => row.Id).Select(row => new InventoryOpeningBalanceRowRecord(row.Id, row.ProductId, row.ProductSku, row.ProductName, row.UnitOfMeasureId, row.UnitOfMeasureCode, row.Quantity, row.UnitCost, row.CurrencyCode, row.TrackingIdentity, row.SourceLineReference, row.Status, row.ValidationCode, row.PostedAt, row.Version)).ToArray(), item.Version);
}

#pragma warning restore CS1591
