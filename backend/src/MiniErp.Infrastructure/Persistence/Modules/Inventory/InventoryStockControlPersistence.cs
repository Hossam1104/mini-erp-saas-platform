#pragma warning disable CS1591

using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.Modules.Inventory;
using MiniErp.App.Modules.Procurement;
using MiniErp.Contracts.Modules.Inventory;
using MiniErp.Contracts.Modules.Procurement;

namespace MiniErp.Infrastructure.Persistence.Modules.Inventory;

internal sealed partial class InventoryPersistence
{
    public async Task<IReadOnlyList<InventoryReasonCodeRecord>> ListReasonCodesAsync(InventoryRequestContext context, InventoryReasonCategory? category = null, bool includeInactive = false, CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(context);
        var query = db.ReasonCodes.AsNoTracking().AsQueryable();
        if (category.HasValue) query = query.Where(item => item.Category == category.Value);
        if (!includeInactive) query = query.Where(item => item.IsActive);
        return (await query.OrderBy(item => item.Code).ToListAsync(cancellationToken)).Select(ToReason).ToArray();
    }

    public async Task<InventoryReasonCodeRecord?> FindReasonCodeAsync(InventoryRequestContext context, Guid id, CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(context);
        var value = await db.ReasonCodes.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        return value is null ? null : ToReason(value);
    }

    public async Task<InventoryReasonCodeRecord?> CreateReasonCodeAsync(InventoryRequestContext context, InventoryReasonCodeCommand command, CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(context);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var replay = await ProbeReplayAsync<InventoryReasonCodeRecord>(db, context, "inventory.reason.create", command.IdempotencyKey, command.RequestFingerprint, cancellationToken);
            if (replay.Outcome == InventoryReplayOutcome.Replay) return replay.Value;
            if (replay.Outcome == InventoryReplayOutcome.Conflict) return null;
            if (await db.ReasonCodes.AnyAsync(item => item.Code == command.Code, cancellationToken)) return null;
            var entity = new InventoryReasonCodeEntity(context.TenantId, command.Id, command.Code, command.EnglishName, command.ArabicName, command.Category, command.ActorId, command.OccurredAt);
            db.ReasonCodes.Add(entity);
            AddAudit(db, context, "reason-code", entity.Id, "inventory.reason.create", command.ActorId, "Succeeded", null, command.CorrelationId, command.IdempotencyKey, command.RequestFingerprint, null, entity.Code, command.OccurredAt);
            await db.SaveChangesAsync(cancellationToken);
            var result = ToReason(entity);
            AddReplay(db, context, "inventory.reason.create", command.IdempotencyKey, command.RequestFingerprint, "reason-code", entity.Id, result, command.OccurredAt);
            await db.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); return result;
        }
        catch (Exception exception) when (InventoryPersistenceExceptionClassifier.IsSqlServerContention(exception)) { return null; }
        catch (DbUpdateConcurrencyException) { return null; }
        catch (DbUpdateException exception) { throw InventoryPersistenceExceptionClassifier.Unavailable(exception); }
        catch (SqlException exception) { throw InventoryPersistenceExceptionClassifier.Unavailable(exception); }
    }

    public async Task<InventoryReasonCodeRecord?> UpdateReasonCodeAsync(InventoryRequestContext context, InventoryReasonCodeUpdateCommand command, CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(context);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var replay = await ProbeReplayAsync<InventoryReasonCodeRecord>(db, context, "inventory.reason.update", command.IdempotencyKey, command.RequestFingerprint, cancellationToken);
            if (replay.Outcome == InventoryReplayOutcome.Replay) return replay.Value;
            if (replay.Outcome == InventoryReplayOutcome.Conflict) return null;
            var entity = await db.ReasonCodes.SingleOrDefaultAsync(item => item.Id == command.Id, cancellationToken);
            if (entity is null || !entity.Version.SequenceEqual(command.ExpectedVersion)) return null;
            entity.Update(command.EnglishName, command.ArabicName, command.Category, command.IsActive, command.OccurredAt);
            AddAudit(db, context, "reason-code", entity.Id, "inventory.reason.update", command.ActorId, "Succeeded", null, command.CorrelationId, command.IdempotencyKey, command.RequestFingerprint, null, entity.IsActive ? "active" : "inactive", command.OccurredAt);
            await db.SaveChangesAsync(cancellationToken);
            var result = ToReason(entity);
            AddReplay(db, context, "inventory.reason.update", command.IdempotencyKey, command.RequestFingerprint, "reason-code", entity.Id, result, command.OccurredAt);
            await db.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); return result;
        }
        catch (Exception exception) when (InventoryPersistenceExceptionClassifier.IsSqlServerContention(exception)) { return null; }
        catch (DbUpdateConcurrencyException) { return null; }
        catch (DbUpdateException exception) { throw InventoryPersistenceExceptionClassifier.Unavailable(exception); }
        catch (SqlException exception) { throw InventoryPersistenceExceptionClassifier.Unavailable(exception); }
    }

    public async Task<IReadOnlyList<InventoryAdjustmentRecord>> ListAdjustmentsAsync(InventoryRequestContext context, InventoryScope? scope = null, CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(context); var query = db.Adjustments.AsNoTracking().Include(item => item.Lines).AsQueryable();
        if (scope is not null) query = query.Where(item => item.CompanyId == scope.CompanyId && item.BranchId == scope.BranchId && item.WarehouseId == scope.WarehouseId);
        var values = (await query.ToListAsync(cancellationToken)).OrderByDescending(item => item.CreatedAt).ToArray();
        return values.Select(ToAdjustment).ToArray();
    }

    public async Task<InventoryAdjustmentRecord?> FindAdjustmentAsync(InventoryRequestContext context, Guid id, CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(context); var value = await db.Adjustments.AsNoTracking().Include(item => item.Lines).SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        return value is null ? null : ToAdjustment(value);
    }

    public async Task<InventoryAdjustmentRecord?> CreateAdjustmentAsync(InventoryRequestContext context, InventoryAdjustmentCreateCommand command, CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(context); await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var replay = await ProbeReplayAsync<InventoryAdjustmentRecord>(db, context, "inventory.adjustment.create", command.IdempotencyKey, command.RequestFingerprint, cancellationToken);
            if (replay.Outcome == InventoryReplayOutcome.Replay) return replay.Value;
            if (replay.Outcome == InventoryReplayOutcome.Conflict || command.Lines.Count == 0) return null;
            var ids = command.Lines.Select(item => item.Reason.Id).Distinct().ToArray();
            var reasons = await db.ReasonCodes.Where(item => ids.Contains(item.Id) && item.IsActive && item.Category == InventoryReasonCategory.Adjustment).ToDictionaryAsync(item => item.Id, cancellationToken);
            if (reasons.Count != ids.Length) return null;
            var entity = new InventoryAdjustmentEntity(context.TenantId, command.Id, command.Scope.CompanyId, command.Scope.BranchId, command.Scope.WarehouseId, command.WarehouseCode, command.WarehouseName, command.EvidenceReference, command.ActorId, command.OccurredAt);
            foreach (var line in command.Lines)
            {
                var reason = reasons[line.Reason.Id];
                entity.Lines.Add(new InventoryAdjustmentLineEntity(context.TenantId, line.Id, entity.Id, line.ProductId, line.Product.Sku, line.Product.Name, line.UnitOfMeasureId, line.Product.BaseUnitOfMeasureCode, line.Direction, line.Quantity, line.TrackingIdentity, reason.Id, reason.Code, reason.EnglishName, reason.ArabicName, line.EvidenceReference));
            }
            db.Adjustments.Add(entity); AddControlHistory(db, context, "adjustment", entity.Id, null, InventoryControlHistoryAction.Created, entity.Status, entity.Status, command.ActorId, null, null, command.CorrelationId, 0, command.OccurredAt); AddAudit(db, context, "adjustment", entity.Id, "inventory.adjustment.create", command.ActorId, "Succeeded", null, command.CorrelationId, command.IdempotencyKey, command.RequestFingerprint, null, "Draft", command.OccurredAt);
            await db.SaveChangesAsync(cancellationToken); var result = ToAdjustment(entity); AddReplay(db, context, "inventory.adjustment.create", command.IdempotencyKey, command.RequestFingerprint, "adjustment", entity.Id, result, command.OccurredAt); await db.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); return result;
        }
        catch (Exception exception) when (InventoryPersistenceExceptionClassifier.IsSqlServerContention(exception)) { return null; }
        catch (DbUpdateConcurrencyException) { return null; }
        catch (DbUpdateException exception) { throw InventoryPersistenceExceptionClassifier.Unavailable(exception); }
        catch (SqlException exception) { throw InventoryPersistenceExceptionClassifier.Unavailable(exception); }
    }

    public Task<InventoryAdjustmentRecord?> SubmitAdjustmentAsync(InventoryRequestContext context, InventoryControlActionCommand command, bool requiresApproval, string? policyJson, CancellationToken cancellationToken = default) => MutateAdjustmentAsync(context, command, "inventory.adjustment.submit", (db, entity, now) =>
    {
        if (entity.Status != InventoryControlDocumentStatus.Draft) return false; entity.Submit(requiresApproval, policyJson, now); return true;
    }, InventoryControlHistoryAction.Submitted, cancellationToken);

    public Task<InventoryAdjustmentRecord?> ApproveAdjustmentAsync(InventoryRequestContext context, InventoryControlActionCommand command, CancellationToken cancellationToken = default) => MutateAdjustmentAsync(context, command, "inventory.adjustment.approve", (db, entity, now) =>
    {
        if (entity.Status != InventoryControlDocumentStatus.PendingApproval) return false;
        var policy = ReadApproval(entity.ApprovalPolicySnapshotJson); if (!PurchaseRequestValuePolicy.IsValidPolicy(policy)) return false;
        var stages = policy!.Stages.OrderBy(item => item.Sequence).ToArray(); var stage = stages.ElementAtOrDefault(entity.CurrentApprovalStageIndex); if (stage is null) return false;
        var approvers = ReadApproverIds(entity.CurrentStageApproverIdsJson); if (!approvers.Add(command.ActorId)) return false;
        if (approvers.Count < stage.RequiredApprovals) { entity.RecordApproval(command.ActorId, command.DelegatedFromActorId, entity.CurrentApprovalStageIndex, approvers, false, now); return true; }
        var finalStage = entity.CurrentApprovalStageIndex + 1 >= stages.Length; entity.RecordApproval(command.ActorId, command.DelegatedFromActorId, finalStage ? entity.CurrentApprovalStageIndex : entity.CurrentApprovalStageIndex + 1, [], finalStage, now); if (finalStage) entity.SetStatus(InventoryControlDocumentStatus.Approved, now); return true;
    }, InventoryControlHistoryAction.Approved, cancellationToken);

    public Task<InventoryAdjustmentRecord?> RejectAdjustmentAsync(InventoryRequestContext context, InventoryControlActionCommand command, bool returnForChange, CancellationToken cancellationToken = default) => MutateAdjustmentAsync(context, command, returnForChange ? "inventory.adjustment.return" : "inventory.adjustment.reject", (db, entity, now) => { if (entity.Status != InventoryControlDocumentStatus.PendingApproval) return false; entity.SetStatus(returnForChange ? InventoryControlDocumentStatus.ReturnedForChange : InventoryControlDocumentStatus.Rejected, now); return true; }, returnForChange ? InventoryControlHistoryAction.ReturnedForChange : InventoryControlHistoryAction.Rejected, cancellationToken);

    public async Task<InventoryAdjustmentRecord?> PostAdjustmentAsync(InventoryRequestContext context, InventoryControlActionCommand command, CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(context); await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var replay = await ProbeReplayAsync<InventoryAdjustmentRecord>(db, context, "inventory.adjustment.post", command.IdempotencyKey, command.RequestFingerprint, cancellationToken); if (replay.Outcome == InventoryReplayOutcome.Replay) return replay.Value; if (replay.Outcome == InventoryReplayOutcome.Conflict) return null;
            var entity = await db.Adjustments.Include(item => item.Lines).SingleOrDefaultAsync(item => item.Id == command.Id, cancellationToken); if (entity is null || !entity.Version.SequenceEqual(command.ExpectedVersion)) return null;
            if (entity.Status == InventoryControlDocumentStatus.Posted) return ToAdjustment(entity);
            if (entity.Status != InventoryControlDocumentStatus.Approved) return null;
            var reasonIds = entity.Lines.Select(item => item.ReasonCodeId).Distinct().ToArray(); if (await db.ReasonCodes.CountAsync(item => reasonIds.Contains(item.Id) && item.IsActive && item.Category == InventoryReasonCategory.Adjustment, cancellationToken) != reasonIds.Length) return null;
            var identities = entity.Lines.Select(StockIdentityKey.From).Distinct().ToArray(); await AcquireConcurrencyAnchorsAsync(db, context.TenantId, identities, cancellationToken);
            var availability = await ReadAvailabilityByIdentityAsync(db, identities, cancellationToken); var outbound = entity.Lines.Where(item => item.Direction == InventoryAdjustmentDirection.Decrease).GroupBy(StockIdentityKey.From).ToDictionary(group => group.Key, group => group.Sum(item => item.Quantity));
            if (outbound.Any(item => availability[item.Key].OnHand - item.Value < availability[item.Key].Reserved)) { AddControlHistory(db, context, "adjustment", entity.Id, null, InventoryControlHistoryAction.PostBlocked, entity.Status, entity.Status, command.ActorId, null, "negative_stock_or_reservation", command.CorrelationId, 0, command.OccurredAt); AddAudit(db, context, "adjustment", entity.Id, "inventory.adjustment.post", command.ActorId, "Failed", "negative_stock_or_reservation", command.CorrelationId, command.IdempotencyKey, command.RequestFingerprint, entity.Status.ToString(), "post-blocked", command.OccurredAt); await db.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); return null; }
            var now = command.OccurredAt; foreach (var line in entity.Lines)
            {
                var movement = NewControlMovement(context, entity.CompanyId, entity.BranchId, entity.WarehouseId, entity.WarehouseCode, entity.WarehouseName, line.ProductId, line.ProductSku, line.ProductName, line.UnitOfMeasureId, line.UnitOfMeasureCode, line.Direction == InventoryAdjustmentDirection.Increase ? InventoryMovementDirection.Inbound : InventoryMovementDirection.Outbound, line.Quantity, line.TrackingIdentity, InventoryMovementSourceType.StockAdjustment, entity.Id, line.Id, null, now, entity.EvidenceReference ?? line.EvidenceReference);
                db.StockMovements.Add(movement); line.MarkPosted(movement.Id);
            }
            entity.MarkPosted(now); AddControlHistory(db, context, "adjustment", entity.Id, null, InventoryControlHistoryAction.Posted, InventoryControlDocumentStatus.Approved, entity.Status, command.ActorId, null, command.Reason, command.CorrelationId, 0, now); AddAudit(db, context, "adjustment", entity.Id, "inventory.adjustment.post", command.ActorId, "Succeeded", command.Reason, command.CorrelationId, command.IdempotencyKey, command.RequestFingerprint, "Approved", "Posted", now);
            await db.SaveChangesAsync(cancellationToken); var result = ToAdjustment(entity); AddReplay(db, context, "inventory.adjustment.post", command.IdempotencyKey, command.RequestFingerprint, "adjustment", entity.Id, result, now); await db.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); return result;
        }
        catch (Exception exception) when (InventoryPersistenceExceptionClassifier.IsSqlServerContention(exception)) { return null; }
        catch (DbUpdateConcurrencyException) { return null; }
        catch (DbUpdateException exception) { throw InventoryPersistenceExceptionClassifier.Unavailable(exception); }
        catch (SqlException exception) { throw InventoryPersistenceExceptionClassifier.Unavailable(exception); }
    }

    private async Task<InventoryAdjustmentRecord?> MutateAdjustmentAsync(InventoryRequestContext context, InventoryControlActionCommand command, string operationId, Func<InventoryDbContext, InventoryAdjustmentEntity, DateTimeOffset, bool> mutate, InventoryControlHistoryAction action, CancellationToken cancellationToken)
    {
        await using var db = CreateContext(context); await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var replay = await ProbeReplayAsync<InventoryAdjustmentRecord>(db, context, operationId, command.IdempotencyKey, command.RequestFingerprint, cancellationToken); if (replay.Outcome == InventoryReplayOutcome.Replay) return replay.Value; if (replay.Outcome == InventoryReplayOutcome.Conflict) return null;
            var entity = await db.Adjustments.Include(item => item.Lines).SingleOrDefaultAsync(item => item.Id == command.Id, cancellationToken); if (entity is null || !entity.Version.SequenceEqual(command.ExpectedVersion)) return null; var fromStatus = entity.Status; if (!mutate(db, entity, command.OccurredAt)) return null;
            AddControlHistory(db, context, "adjustment", entity.Id, null, action, fromStatus, entity.Status, command.ActorId, command.DelegatedFromActorId, command.Reason, command.CorrelationId, 0, command.OccurredAt); AddAudit(db, context, "adjustment", entity.Id, operationId, command.ActorId, "Succeeded", command.Reason, command.CorrelationId, command.IdempotencyKey, command.RequestFingerprint, fromStatus.ToString(), entity.Status.ToString(), command.OccurredAt); await db.SaveChangesAsync(cancellationToken); var result = ToAdjustment(entity); AddReplay(db, context, operationId, command.IdempotencyKey, command.RequestFingerprint, "adjustment", entity.Id, result, command.OccurredAt); await db.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); return result;
        }
        catch (Exception exception) when (InventoryPersistenceExceptionClassifier.IsSqlServerContention(exception)) { return null; }
        catch (DbUpdateConcurrencyException) { return null; }
        catch (DbUpdateException exception) { throw InventoryPersistenceExceptionClassifier.Unavailable(exception); }
        catch (SqlException exception) { throw InventoryPersistenceExceptionClassifier.Unavailable(exception); }
    }

    public async Task<IReadOnlyList<InventoryStockIssueRecord>> ListStockIssuesAsync(InventoryRequestContext context, InventoryScope? scope = null, CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(context); var query = db.StockIssues.AsNoTracking().Include(item => item.Lines).AsQueryable(); if (scope is not null) query = query.Where(item => item.CompanyId == scope.CompanyId && item.BranchId == scope.BranchId && item.WarehouseId == scope.WarehouseId); return (await query.ToListAsync(cancellationToken)).OrderByDescending(item => item.CreatedAt).Select(ToIssue).ToArray();
    }

    public async Task<InventoryStockIssueRecord?> FindStockIssueAsync(InventoryRequestContext context, Guid id, CancellationToken cancellationToken = default)
    { await using var db = CreateContext(context); var value = await db.StockIssues.AsNoTracking().Include(item => item.Lines).SingleOrDefaultAsync(item => item.Id == id, cancellationToken); return value is null ? null : ToIssue(value); }

    public async Task<InventoryStockIssueRecord?> CreateStockIssueAsync(InventoryRequestContext context, InventoryStockIssueCreateCommand command, CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(context); await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var replay = await ProbeReplayAsync<InventoryStockIssueRecord>(db, context, "inventory.issue.create", command.IdempotencyKey, command.RequestFingerprint, cancellationToken); if (replay.Outcome == InventoryReplayOutcome.Replay) return replay.Value; if (replay.Outcome == InventoryReplayOutcome.Conflict || command.Lines.Count == 0) return null;
            var ids = command.Lines.Select(item => item.Reason.Id).Distinct().ToArray(); var reasons = await db.ReasonCodes.Where(item => ids.Contains(item.Id) && item.IsActive && item.Category == InventoryReasonCategory.StockIssue).ToDictionaryAsync(item => item.Id, cancellationToken); if (reasons.Count != ids.Length) return null;
            var entity = new InventoryStockIssueEntity(context.TenantId, command.Id, command.Scope.CompanyId, command.Scope.BranchId, command.Scope.WarehouseId, command.WarehouseCode, command.WarehouseName, command.DestinationUseDescription, command.ActorId, command.OccurredAt);
            foreach (var line in command.Lines) { var reason = reasons[line.Reason.Id]; entity.Lines.Add(new InventoryStockIssueLineEntity(context.TenantId, line.Id, entity.Id, line.ProductId, line.Product.Sku, line.Product.Name, line.UnitOfMeasureId, line.Product.BaseUnitOfMeasureCode, line.Quantity, line.TrackingIdentity, reason.Id, reason.Code, reason.EnglishName, reason.ArabicName, line.EvidenceReference)); }
            db.StockIssues.Add(entity); AddControlHistory(db, context, "stock-issue", entity.Id, null, InventoryControlHistoryAction.Created, entity.Status, entity.Status, command.ActorId, null, null, command.CorrelationId, 0, command.OccurredAt); AddAudit(db, context, "stock-issue", entity.Id, "inventory.issue.create", command.ActorId, "Succeeded", null, command.CorrelationId, command.IdempotencyKey, command.RequestFingerprint, null, "Draft", command.OccurredAt); await db.SaveChangesAsync(cancellationToken); var result = ToIssue(entity); AddReplay(db, context, "inventory.issue.create", command.IdempotencyKey, command.RequestFingerprint, "stock-issue", entity.Id, result, command.OccurredAt); await db.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); return result;
        }
        catch (Exception exception) when (InventoryPersistenceExceptionClassifier.IsSqlServerContention(exception)) { return null; }
        catch (DbUpdateConcurrencyException) { return null; }
        catch (DbUpdateException exception) { throw InventoryPersistenceExceptionClassifier.Unavailable(exception); }
        catch (SqlException exception) { throw InventoryPersistenceExceptionClassifier.Unavailable(exception); }
    }

    public Task<InventoryStockIssueRecord?> SubmitStockIssueAsync(InventoryRequestContext context, InventoryControlActionCommand command, bool requiresApproval, string? policyJson, CancellationToken cancellationToken = default) => MutateIssueAsync(context, command, "inventory.issue.submit", (entity, now) => { if (entity.Status != InventoryControlDocumentStatus.Draft) return false; entity.Submit(requiresApproval, policyJson, now); return true; }, InventoryControlHistoryAction.Submitted, cancellationToken);
    public Task<InventoryStockIssueRecord?> ApproveStockIssueAsync(InventoryRequestContext context, InventoryControlActionCommand command, CancellationToken cancellationToken = default) => MutateIssueAsync(context, command, "inventory.issue.approve", (entity, now) => { if (entity.Status != InventoryControlDocumentStatus.PendingApproval) return false; var policy = ReadApproval(entity.ApprovalPolicySnapshotJson); if (!PurchaseRequestValuePolicy.IsValidPolicy(policy)) return false; var stages = policy!.Stages.OrderBy(item => item.Sequence).ToArray(); var stage = stages.ElementAtOrDefault(entity.CurrentApprovalStageIndex); if (stage is null) return false; var approvers = ReadApproverIds(entity.CurrentStageApproverIdsJson); if (!approvers.Add(command.ActorId)) return false; if (approvers.Count < stage.RequiredApprovals) { entity.RecordApproval(command.ActorId, command.DelegatedFromActorId, entity.CurrentApprovalStageIndex, approvers, false, now); return true; } var finalStage = entity.CurrentApprovalStageIndex + 1 >= stages.Length; entity.RecordApproval(command.ActorId, command.DelegatedFromActorId, finalStage ? entity.CurrentApprovalStageIndex : entity.CurrentApprovalStageIndex + 1, [], finalStage, now); if (finalStage) entity.SetStatus(InventoryControlDocumentStatus.Approved, now); return true; }, InventoryControlHistoryAction.Approved, cancellationToken);
    public Task<InventoryStockIssueRecord?> RejectStockIssueAsync(InventoryRequestContext context, InventoryControlActionCommand command, bool returnForChange, CancellationToken cancellationToken = default) => MutateIssueAsync(context, command, returnForChange ? "inventory.issue.return" : "inventory.issue.reject", (entity, now) => { if (entity.Status != InventoryControlDocumentStatus.PendingApproval) return false; entity.SetStatus(returnForChange ? InventoryControlDocumentStatus.ReturnedForChange : InventoryControlDocumentStatus.Rejected, now); return true; }, returnForChange ? InventoryControlHistoryAction.ReturnedForChange : InventoryControlHistoryAction.Rejected, cancellationToken);

    public async Task<InventoryStockIssueRecord?> PostStockIssueAsync(InventoryRequestContext context, InventoryControlActionCommand command, CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(context); await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var replay = await ProbeReplayAsync<InventoryStockIssueRecord>(db, context, "inventory.issue.post", command.IdempotencyKey, command.RequestFingerprint, cancellationToken); if (replay.Outcome == InventoryReplayOutcome.Replay) return replay.Value; if (replay.Outcome == InventoryReplayOutcome.Conflict) return null;
            var entity = await db.StockIssues.Include(item => item.Lines).SingleOrDefaultAsync(item => item.Id == command.Id, cancellationToken); if (entity is null || !entity.Version.SequenceEqual(command.ExpectedVersion)) return null; if (entity.Status == InventoryControlDocumentStatus.Posted) return ToIssue(entity); if (entity.Status != InventoryControlDocumentStatus.Approved) return null;
            var reasonIds = entity.Lines.Select(item => item.ReasonCodeId).Distinct().ToArray(); if (await db.ReasonCodes.CountAsync(item => reasonIds.Contains(item.Id) && item.IsActive && item.Category == InventoryReasonCategory.StockIssue, cancellationToken) != reasonIds.Length) return null;
            var identities = entity.Lines.Select(StockIdentityKey.From).Distinct().ToArray(); await AcquireConcurrencyAnchorsAsync(db, context.TenantId, identities, cancellationToken); var availability = await ReadAvailabilityByIdentityAsync(db, identities, cancellationToken); var outbound = entity.Lines.GroupBy(StockIdentityKey.From).ToDictionary(group => group.Key, group => group.Sum(item => item.Quantity));
            if (outbound.Any(item => availability[item.Key].OnHand - item.Value < availability[item.Key].Reserved)) { AddControlHistory(db, context, "stock-issue", entity.Id, null, InventoryControlHistoryAction.PostBlocked, entity.Status, entity.Status, command.ActorId, null, "negative_stock_or_reservation", command.CorrelationId, 0, command.OccurredAt); AddAudit(db, context, "stock-issue", entity.Id, "inventory.issue.post", command.ActorId, "Failed", "negative_stock_or_reservation", command.CorrelationId, command.IdempotencyKey, command.RequestFingerprint, entity.Status.ToString(), "post-blocked", command.OccurredAt); await db.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); return null; }
            foreach (var line in entity.Lines) { var movement = NewControlMovement(context, entity.CompanyId, entity.BranchId, entity.WarehouseId, entity.WarehouseCode, entity.WarehouseName, line.ProductId, line.ProductSku, line.ProductName, line.UnitOfMeasureId, line.UnitOfMeasureCode, InventoryMovementDirection.Outbound, line.Quantity, line.TrackingIdentity, InventoryMovementSourceType.StockIssue, entity.Id, line.Id, null, command.OccurredAt, entity.DestinationUseDescription); db.StockMovements.Add(movement); line.MarkPosted(movement.Id); }
            entity.MarkPosted(command.OccurredAt); AddControlHistory(db, context, "stock-issue", entity.Id, null, InventoryControlHistoryAction.Posted, InventoryControlDocumentStatus.Approved, entity.Status, command.ActorId, null, command.Reason, command.CorrelationId, 0, command.OccurredAt); AddAudit(db, context, "stock-issue", entity.Id, "inventory.issue.post", command.ActorId, "Succeeded", command.Reason, command.CorrelationId, command.IdempotencyKey, command.RequestFingerprint, "Approved", "Posted", command.OccurredAt); await db.SaveChangesAsync(cancellationToken); var result = ToIssue(entity); AddReplay(db, context, "inventory.issue.post", command.IdempotencyKey, command.RequestFingerprint, "stock-issue", entity.Id, result, command.OccurredAt); await db.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); return result;
        }
        catch (Exception exception) when (InventoryPersistenceExceptionClassifier.IsSqlServerContention(exception)) { return null; }
        catch (DbUpdateConcurrencyException) { return null; }
        catch (DbUpdateException exception) { throw InventoryPersistenceExceptionClassifier.Unavailable(exception); }
        catch (SqlException exception) { throw InventoryPersistenceExceptionClassifier.Unavailable(exception); }
    }

    private async Task<InventoryStockIssueRecord?> MutateIssueAsync(InventoryRequestContext context, InventoryControlActionCommand command, string operationId, Func<InventoryStockIssueEntity, DateTimeOffset, bool> mutate, InventoryControlHistoryAction action, CancellationToken cancellationToken)
    {
        await using var db = CreateContext(context); await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var replay = await ProbeReplayAsync<InventoryStockIssueRecord>(db, context, operationId, command.IdempotencyKey, command.RequestFingerprint, cancellationToken); if (replay.Outcome == InventoryReplayOutcome.Replay) return replay.Value; if (replay.Outcome == InventoryReplayOutcome.Conflict) return null; var entity = await db.StockIssues.Include(item => item.Lines).SingleOrDefaultAsync(item => item.Id == command.Id, cancellationToken); if (entity is null || !entity.Version.SequenceEqual(command.ExpectedVersion)) return null; var fromStatus = entity.Status; if (!mutate(entity, command.OccurredAt)) return null; AddControlHistory(db, context, "stock-issue", entity.Id, null, action, fromStatus, entity.Status, command.ActorId, command.DelegatedFromActorId, command.Reason, command.CorrelationId, 0, command.OccurredAt); AddAudit(db, context, "stock-issue", entity.Id, operationId, command.ActorId, "Succeeded", command.Reason, command.CorrelationId, command.IdempotencyKey, command.RequestFingerprint, fromStatus.ToString(), entity.Status.ToString(), command.OccurredAt); await db.SaveChangesAsync(cancellationToken); var result = ToIssue(entity); AddReplay(db, context, operationId, command.IdempotencyKey, command.RequestFingerprint, "stock-issue", entity.Id, result, command.OccurredAt); await db.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); return result;
        }
        catch (Exception exception) when (InventoryPersistenceExceptionClassifier.IsSqlServerContention(exception)) { return null; }
        catch (DbUpdateConcurrencyException) { return null; }
        catch (DbUpdateException exception) { throw InventoryPersistenceExceptionClassifier.Unavailable(exception); }
        catch (SqlException exception) { throw InventoryPersistenceExceptionClassifier.Unavailable(exception); }
    }

    public async Task<InventoryCountRecord?> CreateCountAsync(InventoryRequestContext context, InventoryCountCreateCommand command, CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(context); await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var replay = await ProbeReplayAsync<InventoryCountRecord>(db, context, "inventory.count.create", command.IdempotencyKey, command.RequestFingerprint, cancellationToken); if (replay.Outcome == InventoryReplayOutcome.Replay) return replay.Value; if (replay.Outcome == InventoryReplayOutcome.Conflict || command.Lines.Count == 0) return null;
            var identities = command.Lines.Select(line => new StockIdentityKey(command.Scope.CompanyId, command.Scope.BranchId, command.Scope.WarehouseId, line.ProductId, line.UnitOfMeasureId, line.TrackingIdentity)).Distinct().ToArray();
            await AcquireConcurrencyAnchorsAsync(db, context.TenantId, identities, cancellationToken);
            var expectedByIdentity = await ReadAvailabilityByIdentityAsync(db, identities, cancellationToken);
            var cutoff = DateTimeOffset.UtcNow;
            var entity = new InventoryCountEntity(context.TenantId, command.Id, command.Scope.CompanyId, command.Scope.BranchId, command.Scope.WarehouseId, command.WarehouseCode, command.WarehouseName, command.CountType, command.AssignedCounterId, command.ReviewerId, cutoff, command.ActorId, command.OccurredAt, command.ApprovalPolicyJson);
            foreach (var line in command.Lines)
            {
                var identity = new StockIdentityKey(command.Scope.CompanyId, command.Scope.BranchId, command.Scope.WarehouseId, line.ProductId, line.UnitOfMeasureId, line.TrackingIdentity);
                entity.Lines.Add(new InventoryCountLineEntity(context.TenantId, line.Id, entity.Id, line.PriorLineId, line.RoundGeneration, line.ProductId, line.Product.Sku, line.Product.Name, line.UnitOfMeasureId, line.Product.BaseUnitOfMeasureCode, line.TrackingIdentity, expectedByIdentity[identity].OnHand));
            }
            db.Counts.Add(entity); AddControlHistory(db, context, "count", entity.Id, null, InventoryControlHistoryAction.Snapshot, entity.Status, entity.Status, command.ActorId, null, null, command.CorrelationId, entity.CurrentRoundGeneration, cutoff); AddAudit(db, context, "count", entity.Id, "inventory.count.create", command.ActorId, "Succeeded", null, command.CorrelationId, command.IdempotencyKey, command.RequestFingerprint, null, "snapshot", cutoff); await db.SaveChangesAsync(cancellationToken); var result = ToCount(entity, true); AddReplay(db, context, "inventory.count.create", command.IdempotencyKey, command.RequestFingerprint, "count", entity.Id, result, cutoff); await db.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); return result;
        }
        catch (Exception exception) when (InventoryPersistenceExceptionClassifier.IsSqlServerContention(exception)) { return null; }
        catch (DbUpdateConcurrencyException) { return null; }
        catch (DbUpdateException exception) { throw InventoryPersistenceExceptionClassifier.Unavailable(exception); }
        catch (SqlException exception) { throw InventoryPersistenceExceptionClassifier.Unavailable(exception); }
    }

    public async Task<IReadOnlyList<InventoryCountRecord>> ListCountsAsync(InventoryRequestContext context, InventoryScope? scope = null, CancellationToken cancellationToken = default)
    { await using var db = CreateContext(context); var query = db.Counts.AsNoTracking().Include(item => item.Lines).AsQueryable(); if (scope is not null) query = query.Where(item => item.CompanyId == scope.CompanyId && item.BranchId == scope.BranchId && item.WarehouseId == scope.WarehouseId); return (await query.ToListAsync(cancellationToken)).OrderByDescending(item => item.CreatedAt).Select(item => ToCount(item, true)).ToArray(); }

    public async Task<InventoryCountRecord?> FindCountAsync(InventoryRequestContext context, Guid id, bool includeExpected, CancellationToken cancellationToken = default)
    { await using var db = CreateContext(context); var value = await db.Counts.AsNoTracking().Include(item => item.Lines).SingleOrDefaultAsync(item => item.Id == id, cancellationToken); return value is null ? null : ToCount(value, includeExpected); }

    public async Task<InventoryCountRecord?> SubmitCountAsync(InventoryRequestContext context, InventoryCountSubmitCommand command, CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(context); await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var replay = await ProbeReplayAsync<InventoryCountRecord>(db, context, "inventory.count.submit", command.IdempotencyKey, command.RequestFingerprint, cancellationToken); if (replay.Outcome == InventoryReplayOutcome.Replay) return replay.Value; if (replay.Outcome == InventoryReplayOutcome.Conflict) return null; var entity = await db.Counts.Include(item => item.Lines).SingleOrDefaultAsync(item => item.Id == command.Id, cancellationToken); if (entity is null || !entity.Version.SequenceEqual(command.ExpectedVersion) || entity.Status != InventoryControlDocumentStatus.Draft || entity.AssignedCounterId != command.ActorId) return null;
            var current = entity.Lines.Where(item => item.RoundGeneration == entity.CurrentRoundGeneration).ToDictionary(item => item.Id); if (command.Observations.Count != current.Count || command.Observations.Any(item => !current.ContainsKey(item.CountLineId) || item.CountedQuantity < 0m)) return null;
            foreach (var observation in command.Observations) { var line = current[observation.CountLineId]; line.SetObservation(observation.CountedQuantity, command.OccurredAt); }
            var hasVariance = current.Values.Any(item => item.Variance is not null and not 0m); var from = entity.Status; entity.MarkSubmitted(hasVariance ? InventoryControlDocumentStatus.PendingApproval : InventoryControlDocumentStatus.Submitted, command.OccurredAt); AddControlHistory(db, context, "count", entity.Id, null, InventoryControlHistoryAction.CountSubmitted, from, entity.Status, command.ActorId, null, null, command.CorrelationId, entity.CurrentRoundGeneration, command.OccurredAt); AddAudit(db, context, "count", entity.Id, "inventory.count.submit", command.ActorId, "Succeeded", null, command.CorrelationId, command.IdempotencyKey, command.RequestFingerprint, from.ToString(), entity.Status.ToString(), command.OccurredAt); await db.SaveChangesAsync(cancellationToken); var result = ToCount(entity, true); AddReplay(db, context, "inventory.count.submit", command.IdempotencyKey, command.RequestFingerprint, "count", entity.Id, result, command.OccurredAt); await db.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); return result;
        }
        catch (Exception exception) when (InventoryPersistenceExceptionClassifier.IsSqlServerContention(exception)) { return null; }
        catch (DbUpdateConcurrencyException) { return null; }
        catch (DbUpdateException exception) { throw InventoryPersistenceExceptionClassifier.Unavailable(exception); }
        catch (SqlException exception) { throw InventoryPersistenceExceptionClassifier.Unavailable(exception); }
    }

    public Task<InventoryCountRecord?> ApproveCountAsync(InventoryRequestContext context, InventoryControlActionCommand command, CancellationToken cancellationToken = default) => MutateCountAsync(context, command, "inventory.count.approve", (db, entity, now) =>
    {
        var current = entity.Lines.Where(item => item.RoundGeneration == entity.CurrentRoundGeneration).ToArray();
        if (entity.Status != InventoryControlDocumentStatus.PendingApproval || entity.AssignedCounterId == command.ActorId || current.Any(item => item.CountedQuantity is null || item.Variance is not null and not 0m && item.VarianceReasonCodeId is null)) return false;
        var policy = ReadApproval(entity.ApprovalPolicySnapshotJson);
        if (entity.ApprovalPolicySnapshotJson is not null && !PurchaseRequestValuePolicy.IsValidPolicy(policy)) return false;
        if (policy is null) { entity.MarkApproved(command.ActorId, now); return true; }
        var stages = policy.Stages.OrderBy(item => item.Sequence).ToArray();
        var stage = stages.ElementAtOrDefault(entity.CurrentApprovalStageIndex);
        if (stage is null) return false;
        var approvers = ReadApproverIds(entity.CurrentStageApproverIdsJson);
        if (!approvers.Add(command.ActorId)) return false;
        if (approvers.Count < stage.RequiredApprovals)
        {
            entity.RecordApproval(command.ActorId, command.DelegatedFromActorId, entity.CurrentApprovalStageIndex, approvers, false, now);
            return true;
        }
        var finalStage = entity.CurrentApprovalStageIndex + 1 >= stages.Length;
        entity.RecordApproval(command.ActorId, command.DelegatedFromActorId, finalStage ? entity.CurrentApprovalStageIndex : entity.CurrentApprovalStageIndex + 1, [], finalStage, now);
        return true;
    }, InventoryControlHistoryAction.Approved, cancellationToken);

    public async Task<InventoryCountRecord?> RecordCountVarianceReasonAsync(InventoryRequestContext context, InventoryCountVarianceReasonCommand command, CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(context); await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var replay = await ProbeReplayAsync<InventoryCountRecord>(db, context, "inventory.count.variance-reason", command.IdempotencyKey, command.RequestFingerprint, cancellationToken); if (replay.Outcome == InventoryReplayOutcome.Replay) return replay.Value; if (replay.Outcome == InventoryReplayOutcome.Conflict) return null;
            var entity = await db.Counts.Include(item => item.Lines).SingleOrDefaultAsync(item => item.Id == command.Id, cancellationToken); if (entity is null || !entity.Version.SequenceEqual(command.ExpectedVersion) || entity.Status != InventoryControlDocumentStatus.PendingApproval) return null;
            var line = entity.Lines.SingleOrDefault(item => item.Id == command.CountLineId && item.RoundGeneration == entity.CurrentRoundGeneration); if (line is null || line.CountedQuantity is null || line.Variance is null or 0m) return null;
            var reasonEntity = await db.ReasonCodes.AsNoTracking().SingleOrDefaultAsync(item => item.Code == command.ReasonCode && item.IsActive && item.Category == InventoryReasonCategory.CountVariance, cancellationToken); if (reasonEntity is null) return null;
            line.SetVarianceReason(ToReason(reasonEntity)); AddControlHistory(db, context, "count", entity.Id, line.Id, InventoryControlHistoryAction.VarianceReasonRecorded, entity.Status, entity.Status, command.ActorId, null, reasonEntity.Code, command.CorrelationId, entity.CurrentRoundGeneration, command.OccurredAt); AddAudit(db, context, "count", entity.Id, "inventory.count.variance-reason", command.ActorId, "Succeeded", reasonEntity.Code, command.CorrelationId, command.IdempotencyKey, command.RequestFingerprint, null, $"line:{line.Id}", command.OccurredAt); await db.SaveChangesAsync(cancellationToken); var result = ToCount(entity, true); AddReplay(db, context, "inventory.count.variance-reason", command.IdempotencyKey, command.RequestFingerprint, "count", entity.Id, result, command.OccurredAt); await db.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); return result;
        }
        catch (Exception exception) when (InventoryPersistenceExceptionClassifier.IsSqlServerContention(exception)) { return null; }
        catch (DbUpdateConcurrencyException) { return null; }
        catch (DbUpdateException exception) { throw InventoryPersistenceExceptionClassifier.Unavailable(exception); }
        catch (SqlException exception) { throw InventoryPersistenceExceptionClassifier.Unavailable(exception); }
    }
    public Task<InventoryCountRecord?> RejectCountAsync(InventoryRequestContext context, InventoryControlActionCommand command, bool returnForChange, CancellationToken cancellationToken = default) => MutateCountAsync(context, command, returnForChange ? "inventory.count.return" : "inventory.count.reject", (db, entity, now) => { if (entity.Status != InventoryControlDocumentStatus.PendingApproval) return false; entity.SetStatus(returnForChange ? InventoryControlDocumentStatus.ReturnedForChange : InventoryControlDocumentStatus.Rejected, now); return true; }, returnForChange ? InventoryControlHistoryAction.ReturnedForChange : InventoryControlHistoryAction.Rejected, cancellationToken);

    public Task<InventoryCountRecord?> RequestCountRecountAsync(InventoryRequestContext context, InventoryControlActionCommand command, CancellationToken cancellationToken = default) => MutateCountAsync(context, command, "inventory.count.recount", (db, entity, now) => { if (entity.Status != InventoryControlDocumentStatus.PendingApproval && entity.Status != InventoryControlDocumentStatus.Approved) return false; var current = entity.Lines.Where(item => item.RoundGeneration == entity.CurrentRoundGeneration).ToArray(); entity.BeginNewRound(entity.SnapshotCutoff, now); foreach (var line in current) entity.Lines.Add(new InventoryCountLineEntity(context.TenantId, Guid.NewGuid(), entity.Id, line.Id, entity.CurrentRoundGeneration, line.ProductId, line.ProductSku, line.ProductName, line.UnitOfMeasureId, line.UnitOfMeasureCode, line.TrackingIdentity, line.ExpectedQuantity)); return true; }, InventoryControlHistoryAction.RecountRequested, cancellationToken);

    public async Task<InventoryCountRecord?> ResnapshotCountAsync(InventoryRequestContext context, InventoryControlActionCommand command, CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(context); await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var replay = await ProbeReplayAsync<InventoryCountRecord>(db, context, "inventory.count.resnapshot", command.IdempotencyKey, command.RequestFingerprint, cancellationToken);
            if (replay.Outcome == InventoryReplayOutcome.Replay) return replay.Value;
            if (replay.Outcome == InventoryReplayOutcome.Conflict) return null;

            var entity = await db.Counts.Include(item => item.Lines).SingleOrDefaultAsync(item => item.Id == command.Id, cancellationToken);
            if (entity is null || !entity.Version.SequenceEqual(command.ExpectedVersion) || entity.Status != InventoryControlDocumentStatus.ResnapshotRequired) return null;

            var previous = entity.Lines.Where(item => item.RoundGeneration == entity.CurrentRoundGeneration).ToArray();
            var warehouseMovementRows = entity.CountType == InventoryCountType.Full
                ? await db.StockMovements.AsNoTracking()
                    .Where(item => item.CompanyId == entity.CompanyId && item.BranchId == entity.BranchId && item.WarehouseId == entity.WarehouseId)
                    .Select(item => new
                    {
                        item.CompanyId,
                        item.BranchId,
                        item.WarehouseId,
                        item.ProductId,
                        item.ProductSku,
                        item.ProductName,
                        item.UnitOfMeasureId,
                        item.UnitOfMeasureCode,
                        TrackingIdentity = item.TrackingIdentity ?? string.Empty
                    })
                    .ToListAsync(cancellationToken)
                : [];
            var movementMetadata = warehouseMovementRows
                .GroupBy(item => new StockIdentityKey(item.CompanyId, item.BranchId, item.WarehouseId, item.ProductId, item.UnitOfMeasureId, item.TrackingIdentity))
                .ToDictionary(group => group.Key, group => group.First());
            var identities = warehouseMovementRows
                .Select(item => new StockIdentityKey(item.CompanyId, item.BranchId, item.WarehouseId, item.ProductId, item.UnitOfMeasureId, item.TrackingIdentity))
                .Concat(previous.Select(StockIdentityKey.From))
                .Distinct()
                .ToArray();

            await AcquireConcurrencyAnchorsAsync(db, context.TenantId, identities, cancellationToken);
            var expectedByIdentity = await ReadAvailabilityByIdentityAsync(db, identities, cancellationToken);
            var cutoff = DateTimeOffset.UtcNow;
            entity.BeginNewRound(cutoff, command.OccurredAt);
            foreach (var identity in identities)
            {
                var prior = previous.FirstOrDefault(item => StockIdentityKey.From(item).Equals(identity));
                movementMetadata.TryGetValue(identity, out var movement);
                entity.Lines.Add(new InventoryCountLineEntity(
                    context.TenantId,
                    Guid.NewGuid(),
                    entity.Id,
                    prior?.Id,
                    entity.CurrentRoundGeneration,
                    identity.ProductId,
                    prior?.ProductSku ?? movement?.ProductSku ?? string.Empty,
                    prior?.ProductName ?? movement?.ProductName ?? string.Empty,
                    identity.UnitOfMeasureId,
                    prior?.UnitOfMeasureCode ?? movement?.UnitOfMeasureCode ?? string.Empty,
                    identity.TrackingKey,
                    expectedByIdentity[identity].OnHand));
            }

            AddControlHistory(db, context, "count", entity.Id, null, InventoryControlHistoryAction.Resnapshot, InventoryControlDocumentStatus.ResnapshotRequired, entity.Status, command.ActorId, null, command.Reason, command.CorrelationId, entity.CurrentRoundGeneration, cutoff);
            AddAudit(db, context, "count", entity.Id, "inventory.count.resnapshot", command.ActorId, "Succeeded", command.Reason, command.CorrelationId, command.IdempotencyKey, command.RequestFingerprint, "ResnapshotRequired", "Draft", cutoff);
            await db.SaveChangesAsync(cancellationToken);
            var result = ToCount(entity, true);
            AddReplay(db, context, "inventory.count.resnapshot", command.IdempotencyKey, command.RequestFingerprint, "count", entity.Id, result, cutoff);
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return result;
        }
        catch (Exception exception) when (InventoryPersistenceExceptionClassifier.IsSqlServerContention(exception)) { return null; }
        catch (DbUpdateConcurrencyException) { return null; }
        catch (DbUpdateException exception) { throw InventoryPersistenceExceptionClassifier.Unavailable(exception); }
        catch (SqlException exception) { throw InventoryPersistenceExceptionClassifier.Unavailable(exception); }
    }

    public async Task<InventoryCountRecord?> PostCountAsync(InventoryRequestContext context, InventoryControlActionCommand command, CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(context); await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var replay = await ProbeReplayAsync<InventoryCountRecord>(db, context, "inventory.count.post", command.IdempotencyKey, command.RequestFingerprint, cancellationToken); if (replay.Outcome == InventoryReplayOutcome.Replay) return replay.Value; if (replay.Outcome == InventoryReplayOutcome.Conflict) return null; var entity = await db.Counts.Include(item => item.Lines).SingleOrDefaultAsync(item => item.Id == command.Id, cancellationToken); if (entity is null || !entity.Version.SequenceEqual(command.ExpectedVersion)) return null; if (entity.Status == InventoryControlDocumentStatus.Posted) return ToCount(entity, true); if (entity.Status is not (InventoryControlDocumentStatus.Submitted or InventoryControlDocumentStatus.Approved)) return null;
            var current = entity.Lines.Where(item => item.RoundGeneration == entity.CurrentRoundGeneration).ToArray();
            var identities = current.Select(StockIdentityKey.From).Distinct().ToArray();
            await AcquireConcurrencyAnchorsAsync(db, context.TenantId, identities, cancellationToken);
            var recentMovements = (await db.StockMovements.AsNoTracking().Where(item => item.CompanyId == entity.CompanyId && item.BranchId == entity.BranchId && item.WarehouseId == entity.WarehouseId).ToListAsync(cancellationToken))
                .Where(item => item.PostedAt > entity.SnapshotCutoff)
                .Select(item => new StockIdentityKey(item.CompanyId, item.BranchId, item.WarehouseId, item.ProductId, item.UnitOfMeasureId, item.TrackingIdentity ?? string.Empty))
                .ToList();
            if (recentMovements.Count > 0)
            {
                var fromStatus = entity.Status; entity.SetStatus(InventoryControlDocumentStatus.ResnapshotRequired, command.OccurredAt);
                AddControlHistory(db, context, "count", entity.Id, null, InventoryControlHistoryAction.PostBlocked, fromStatus, entity.Status, command.ActorId, null, "resnapshot_required", command.CorrelationId, entity.CurrentRoundGeneration, command.OccurredAt);
                AddAudit(db, context, "count", entity.Id, "inventory.count.post", command.ActorId, "Failed", "resnapshot_required", command.CorrelationId, command.IdempotencyKey, command.RequestFingerprint, fromStatus.ToString(), "ResnapshotRequired", command.OccurredAt);
                await db.SaveChangesAsync(cancellationToken);
                await transaction.CommitAsync(cancellationToken);
                return ToCount(entity, true);
            }
            if (entity.Status == InventoryControlDocumentStatus.Submitted && current.Any(item => item.Variance is not null and not 0m)) return null; if (current.Any(item => item.Variance is not null and not 0m && item.VarianceReasonCodeId is null)) return null;
            var negative = current.Where(item => item.Variance < 0m).GroupBy(StockIdentityKey.From).ToDictionary(group => group.Key, group => group.Sum(item => Math.Abs(item.Variance!.Value))); var availability = await ReadAvailabilityByIdentityAsync(db, identities, cancellationToken); if (negative.Any(item => availability[item.Key].OnHand - item.Value < availability[item.Key].Reserved)) { var fromStatus = entity.Status; entity.SetStatus(InventoryControlDocumentStatus.Blocked, command.OccurredAt); AddControlHistory(db, context, "count", entity.Id, null, InventoryControlHistoryAction.PostBlocked, fromStatus, entity.Status, command.ActorId, null, "reservation_reconciliation_required", command.CorrelationId, entity.CurrentRoundGeneration, command.OccurredAt); AddAudit(db, context, "count", entity.Id, "inventory.count.post", command.ActorId, "Failed", "reservation_reconciliation_required", command.CorrelationId, command.IdempotencyKey, command.RequestFingerprint, fromStatus.ToString(), "Blocked", command.OccurredAt); await db.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); return ToCount(entity, true); }
            foreach (var line in current.Where(item => item.Variance is not null and not 0m)) { var direction = line.Variance > 0m ? InventoryMovementDirection.Inbound : InventoryMovementDirection.Outbound; var movement = NewControlMovement(context, entity.CompanyId, entity.BranchId, entity.WarehouseId, entity.WarehouseCode, entity.WarehouseName, line.ProductId, line.ProductSku, line.ProductName, line.UnitOfMeasureId, line.UnitOfMeasureCode, direction, Math.Abs(line.Variance!.Value), line.TrackingIdentity, InventoryMovementSourceType.InventoryCountVariance, entity.Id, line.Id, null, command.OccurredAt, line.VarianceReasonCode); db.StockMovements.Add(movement); line.MarkPosted(movement.Id); }
            var postFromStatus = entity.Status; entity.MarkPosted(command.ActorId, command.OccurredAt); AddControlHistory(db, context, "count", entity.Id, null, InventoryControlHistoryAction.Posted, postFromStatus, entity.Status, command.ActorId, null, command.Reason, command.CorrelationId, entity.CurrentRoundGeneration, command.OccurredAt); AddAudit(db, context, "count", entity.Id, "inventory.count.post", command.ActorId, "Succeeded", command.Reason, command.CorrelationId, command.IdempotencyKey, command.RequestFingerprint, postFromStatus.ToString(), "Posted", command.OccurredAt); await db.SaveChangesAsync(cancellationToken); var result = ToCount(entity, true); AddReplay(db, context, "inventory.count.post", command.IdempotencyKey, command.RequestFingerprint, "count", entity.Id, result, command.OccurredAt); await db.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); return result;
        }
        catch (Exception exception) when (InventoryPersistenceExceptionClassifier.IsSqlServerContention(exception)) { return null; }
        catch (DbUpdateConcurrencyException) { return null; }
        catch (DbUpdateException exception) { throw InventoryPersistenceExceptionClassifier.Unavailable(exception); }
        catch (SqlException exception) { throw InventoryPersistenceExceptionClassifier.Unavailable(exception); }
    }

    private async Task<InventoryCountRecord?> MutateCountAsync(InventoryRequestContext context, InventoryControlActionCommand command, string operationId, Func<InventoryDbContext, InventoryCountEntity, DateTimeOffset, bool> mutate, InventoryControlHistoryAction action, CancellationToken cancellationToken)
    {
        await using var db = CreateContext(context); await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var replay = await ProbeReplayAsync<InventoryCountRecord>(db, context, operationId, command.IdempotencyKey, command.RequestFingerprint, cancellationToken); if (replay.Outcome == InventoryReplayOutcome.Replay) return replay.Value; if (replay.Outcome == InventoryReplayOutcome.Conflict) return null; var entity = await db.Counts.Include(item => item.Lines).SingleOrDefaultAsync(item => item.Id == command.Id, cancellationToken); if (entity is null || !entity.Version.SequenceEqual(command.ExpectedVersion)) return null; var fromStatus = entity.Status; if (!mutate(db, entity, command.OccurredAt)) return null; AddControlHistory(db, context, "count", entity.Id, null, action, fromStatus, entity.Status, command.ActorId, command.DelegatedFromActorId, command.Reason, command.CorrelationId, entity.CurrentRoundGeneration, command.OccurredAt); AddAudit(db, context, "count", entity.Id, operationId, command.ActorId, "Succeeded", command.Reason, command.CorrelationId, command.IdempotencyKey, command.RequestFingerprint, fromStatus.ToString(), entity.Status.ToString(), command.OccurredAt); await db.SaveChangesAsync(cancellationToken); var result = ToCount(entity, true); AddReplay(db, context, operationId, command.IdempotencyKey, command.RequestFingerprint, "count", entity.Id, result, command.OccurredAt); await db.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); return result;
        }
        catch (Exception exception) when (InventoryPersistenceExceptionClassifier.IsSqlServerContention(exception)) { return null; }
        catch (DbUpdateConcurrencyException) { return null; }
        catch (DbUpdateException exception) { throw InventoryPersistenceExceptionClassifier.Unavailable(exception); }
        catch (SqlException exception) { throw InventoryPersistenceExceptionClassifier.Unavailable(exception); }
    }

    public async Task<IReadOnlyList<InventoryControlHistoryRecord>> ReadControlHistoryAsync(InventoryRequestContext context, string resourceType, Guid resourceId, CancellationToken cancellationToken = default)
    { await using var db = CreateContext(context); return (await db.ControlHistory.AsNoTracking().Where(item => item.ResourceType == resourceType && item.ResourceId == resourceId).OrderBy(item => item.OccurredAt).ToListAsync(cancellationToken)).Select(ToHistory).ToArray(); }

    public async Task<InventoryMovementRecord?> CorrectMovementAsync(InventoryRequestContext context, InventoryMovementCorrectionCommand command, CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(context); await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        try
        {
            var replay = await ProbeReplayAsync<InventoryMovementRecord>(db, context, "inventory.movement.correct", command.IdempotencyKey, command.RequestFingerprint, cancellationToken); if (replay.Outcome == InventoryReplayOutcome.Replay) return replay.Value; if (replay.Outcome == InventoryReplayOutcome.Conflict) return null; var original = await db.StockMovements.SingleOrDefaultAsync(item => item.Id == command.MovementId, cancellationToken); if (original is null || !original.Version.SequenceEqual(command.ExpectedVersion) || original.SourceType is not (InventoryMovementSourceType.StockAdjustment or InventoryMovementSourceType.InventoryCountVariance or InventoryMovementSourceType.StockIssue)) return null; if (await db.StockMovements.AnyAsync(item => item.CorrectionOfMovementId == original.Id, cancellationToken)) return null;
            var identity = StockIdentityKey.From(original); await AcquireConcurrencyAnchorsAsync(db, context.TenantId, [identity], cancellationToken); if (original.Direction == InventoryMovementDirection.Inbound && await SignedQuantityAsync(db, identity, cancellationToken) - original.Quantity < await ActiveReservedQuantityAsync(db, identity, cancellationToken)) return null;
            var movement = NewControlMovement(context, original.CompanyId, original.BranchId, original.WarehouseId, original.WarehouseCode, original.WarehouseName, original.ProductId, original.ProductSku, original.ProductName, original.UnitOfMeasureId, original.UnitOfMeasureCode, original.Direction == InventoryMovementDirection.Inbound ? InventoryMovementDirection.Outbound : InventoryMovementDirection.Inbound, original.Quantity, original.TrackingIdentity ?? string.Empty, InventoryMovementSourceType.Correction, Guid.NewGuid(), Guid.NewGuid(), original.Id, command.OccurredAt, command.ReasonCode); db.StockMovements.Add(movement); AddAudit(db, context, "movement", original.Id, "inventory.movement.correct", command.ActorId, "Succeeded", command.Reason, command.CorrelationId, command.IdempotencyKey, command.RequestFingerprint, $"movement:{original.Id}", $"correction:{movement.Id}", command.OccurredAt); await db.SaveChangesAsync(cancellationToken); var result = ToMovement.Compile()(movement); AddReplay(db, context, "inventory.movement.correct", command.IdempotencyKey, command.RequestFingerprint, "movement", original.Id, result, command.OccurredAt); await db.SaveChangesAsync(cancellationToken); await transaction.CommitAsync(cancellationToken); return result;
        }
        catch (Exception exception) when (InventoryPersistenceExceptionClassifier.IsSqlServerContention(exception)) { return null; }
        catch (DbUpdateConcurrencyException) { return null; }
        catch (DbUpdateException exception) when (InventoryPersistenceExceptionClassifier.IsCorrectionUniqueViolation(exception)) { return null; }
        catch (DbUpdateException exception) { throw InventoryPersistenceExceptionClassifier.Unavailable(exception); }
        catch (SqlException exception) { throw InventoryPersistenceExceptionClassifier.Unavailable(exception); }
    }

    private static async Task<InventoryAdjustmentRecord?> ToAdjustmentAsync(InventoryDbContext db, Guid id, CancellationToken cancellationToken) { var entity = await db.Adjustments.AsNoTracking().Include(item => item.Lines).SingleAsync(item => item.Id == id, cancellationToken); return ToAdjustment(entity); }
    private static async Task<InventoryStockIssueRecord?> ToIssueAsync(InventoryDbContext db, Guid id, CancellationToken cancellationToken) { var entity = await db.StockIssues.AsNoTracking().Include(item => item.Lines).SingleAsync(item => item.Id == id, cancellationToken); return ToIssue(entity); }

    private static InventoryReasonCodeRecord ToReason(InventoryReasonCodeEntity item) => new(item.Id, item.TenantId.Value, item.Code, item.EnglishName, item.ArabicName, item.Category, item.IsActive, item.CreatedByActorId, item.CreatedAt, item.UpdatedAt, item.Version);
    private static PurchaseRequestApprovalPolicyDefinition? ReadApproval(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try { return JsonSerializer.Deserialize<PurchaseRequestApprovalPolicyDefinition>(json, InventoryControlJson.Options); }
        catch (JsonException) { return null; }
    }
    private static HashSet<Guid> ReadApproverIds(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return [];
        try { return (JsonSerializer.Deserialize<Guid[]>(json, InventoryControlJson.Options) ?? []).Where(item => item != Guid.Empty).ToHashSet(); }
        catch (JsonException) { return []; }
    }
    private static InventoryApprovalRecord? ReadApprovalRecord(string? json, int index, int count, Guid? actor, Guid? delegated)
    { var policy = ReadApproval(json); var stage = policy?.Stages.OrderBy(item => item.Sequence).ElementAtOrDefault(index); return policy is null || stage is null ? null : new InventoryApprovalRecord(policy.PolicyId, policy.Version, index, stage.StageKey, stage.RequiredApprovals, count, stage.AllowDelegation, actor, delegated); }
    private static InventoryAdjustmentRecord ToAdjustment(InventoryAdjustmentEntity item) => new(item.Id, item.TenantId.Value, item.CompanyId, item.BranchId, item.WarehouseId, item.WarehouseCode, item.WarehouseName, item.RequesterId, item.Status, item.EvidenceReference, item.Lines.OrderBy(line => line.Id).Select(line => new InventoryAdjustmentLineRecord(line.Id, line.ProductId, line.ProductSku, line.ProductName, line.UnitOfMeasureId, line.UnitOfMeasureCode, line.Direction, line.Quantity, line.TrackingIdentity, line.ReasonCodeId, line.ReasonCode, line.ReasonEnglishName, line.ReasonArabicName, line.EvidenceReference, line.MovementId, line.Version)).ToArray(), ReadApprovalRecord(item.ApprovalPolicySnapshotJson, item.CurrentApprovalStageIndex, item.CurrentStageApprovalCount, item.LastApproverId, item.LastDelegatedFromActorId), item.CreatedAt, item.UpdatedAt, item.SubmittedAt, item.ApprovedAt, item.PostedAt, item.Version);
    private static InventoryStockIssueRecord ToIssue(InventoryStockIssueEntity item) => new(item.Id, item.TenantId.Value, item.CompanyId, item.BranchId, item.WarehouseId, item.WarehouseCode, item.WarehouseName, item.RequesterId, item.DestinationUseDescription, item.Status, item.Lines.OrderBy(line => line.Id).Select(line => new InventoryStockIssueLineRecord(line.Id, line.ProductId, line.ProductSku, line.ProductName, line.UnitOfMeasureId, line.UnitOfMeasureCode, line.Quantity, line.TrackingIdentity, line.ReasonCodeId, line.ReasonCode, line.ReasonEnglishName, line.ReasonArabicName, line.EvidenceReference, line.MovementId, line.Version)).ToArray(), ReadApprovalRecord(item.ApprovalPolicySnapshotJson, item.CurrentApprovalStageIndex, item.CurrentStageApprovalCount, item.LastApproverId, item.LastDelegatedFromActorId), item.CreatedAt, item.UpdatedAt, item.SubmittedAt, item.ApprovedAt, item.PostedAt, item.Version);
    private static InventoryCountRecord ToCount(InventoryCountEntity item, bool includeExpected) => new(item.Id, item.TenantId.Value, item.CompanyId, item.BranchId, item.WarehouseId, item.WarehouseCode, item.WarehouseName, item.CountType, item.AssignedCounterId, item.ReviewerId, item.ApproverId, item.PosterId, item.Status, item.CurrentRoundGeneration, item.SnapshotCutoff, item.Lines.OrderBy(line => line.RoundGeneration).ThenBy(line => line.Id).Select(line => new InventoryCountLineRecord(line.Id, line.PriorLineId, line.RoundGeneration, line.ProductId, line.ProductSku, line.ProductName, line.UnitOfMeasureId, line.UnitOfMeasureCode, line.TrackingIdentity, includeExpected ? line.ExpectedQuantity : null, line.CountedQuantity, includeExpected ? line.Variance : line.CountedQuantity is null ? null : line.Variance, line.VarianceReasonCodeId, line.VarianceReasonCode, line.VarianceReasonEnglishName, line.VarianceReasonArabicName, line.RoundGeneration == item.CurrentRoundGeneration, line.CountedAt, line.Version)).ToArray(), ReadApprovalRecord(item.ApprovalPolicySnapshotJson, item.CurrentApprovalStageIndex, item.CurrentStageApprovalCount, item.LastApproverId, item.LastDelegatedFromActorId), item.CreatedAt, item.UpdatedAt, item.SubmittedAt, item.ApprovedAt, item.PostedAt, item.Version);
    private static InventoryControlHistoryRecord ToHistory(InventoryControlHistoryEntity item) => new(item.Id, item.ResourceType, item.ResourceId, item.LineId, item.Action, item.FromStatus, item.ToStatus, item.ActorId, item.DelegatedFromActorId, item.Reason, item.CorrelationId, item.RoundGeneration, item.OccurredAt, item.Version);
    private static void AddControlHistory(InventoryDbContext db, InventoryRequestContext context, string resourceType, Guid resourceId, Guid? lineId, InventoryControlHistoryAction action, InventoryControlDocumentStatus from, InventoryControlDocumentStatus to, Guid actorId, Guid? delegatedFrom, string? reason, string correlationId, int generation, DateTimeOffset at) => db.ControlHistory.Add(new InventoryControlHistoryEntity(context.TenantId, Guid.NewGuid(), resourceType, resourceId, lineId, action, from, to, actorId, delegatedFrom, reason, correlationId, generation, at));
    private static InventoryStockMovementEntity NewControlMovement(InventoryRequestContext context, Guid companyId, Guid? branchId, Guid warehouseId, string warehouseCode, string warehouseName, Guid productId, string sku, string name, Guid uomId, string uomCode, InventoryMovementDirection direction, decimal quantity, string trackingIdentity, InventoryMovementSourceType sourceType, Guid sourceDocumentId, Guid sourceLineId, Guid? correctionOfMovementId, DateTimeOffset at, string? sourceReference) => new(context.TenantId, Guid.NewGuid(), companyId, branchId, warehouseId, warehouseCode, warehouseName, productId, sku, name, uomId, uomCode, direction, quantity, null, null, InventoryValuationStatus.Pending, string.IsNullOrEmpty(trackingIdentity) ? null : trackingIdentity, sourceType, sourceDocumentId, sourceLineId, correctionOfMovementId, DateOnly.FromDateTime(at.UtcDateTime), context.ActorId, context.CorrelationId?.Value ?? Guid.NewGuid().ToString("N"), at, sourceReference: sourceReference);
    private static async Task<Dictionary<StockIdentityKey, (decimal OnHand, decimal Reserved)>> ReadAvailabilityByIdentityAsync(InventoryDbContext db, IEnumerable<StockIdentityKey> identities, CancellationToken cancellationToken) { var result = new Dictionary<StockIdentityKey, (decimal, decimal)>(); foreach (var identity in identities.Distinct()) result[identity] = (await SignedQuantityAsync(db, identity, cancellationToken), await ActiveReservedQuantityAsync(db, identity, cancellationToken)); return result; }
}

#pragma warning restore CS1591
