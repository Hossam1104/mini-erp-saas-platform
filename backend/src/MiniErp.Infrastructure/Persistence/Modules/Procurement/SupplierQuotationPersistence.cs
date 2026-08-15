#pragma warning disable CS1591

using Microsoft.EntityFrameworkCore;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.Modules.Procurement;
using MiniErp.Contracts.Modules.Procurement;

namespace MiniErp.Infrastructure.Persistence.Modules.Procurement;

public sealed class SupplierQuotationPersistence : ISupplierQuotationPersistence
{
    private readonly DbContextOptions options;

    internal SupplierQuotationPersistence(DbContextOptions options)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<IReadOnlyList<SupplierQuotationRecord>> ListAsync(
        TenantContext tenantContext,
        Guid purchaseRequestId,
        CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(tenantContext);
        var quotations = await ApplyTrustedScope(
            db.SupplierQuotations.AsNoTracking(),
            tenantContext.Scope)
            .Include(item => item.Lines)
            .Include(item => item.Evidence)
            .Where(item => item.PurchaseRequestId == purchaseRequestId)
            .ToListAsync(cancellationToken);
        return quotations
            .OrderByDescending(item => item.CreatedAt)
            .ThenByDescending(item => item.Id)
            .Select(ToRecord)
            .ToArray();
    }

    public async Task<SupplierQuotationRecord?> FindAsync(
        TenantContext tenantContext,
        Guid quotationId,
        CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(tenantContext);
        var quotation = await db.SupplierQuotations
            .AsNoTracking()
            .Include(item => item.Lines)
            .Include(item => item.Evidence)
            .SingleOrDefaultAsync(item => item.Id == quotationId, cancellationToken);
        return quotation is null ? null : ToRecord(quotation);
    }

    public async Task<SupplierQuotationPersistenceResult<SupplierQuotationRecord>> CreateAsync(
        TenantContext tenantContext,
        SupplierQuotationCreateCommand command,
        SupplierQuotationAuditEvidence evidence,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(evidence);
        await using var db = CreateContext(tenantContext);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var replay = await FindReplayAsync(db, evidence, cancellationToken);
            if (replay is not null)
            {
                await transaction.CommitAsync(cancellationToken);
                return SupplierQuotationPersistenceResult<SupplierQuotationRecord>.Success(replay);
            }

            if (command.Scope.TenantId != tenantContext.TenantId.Value || command.Id == Guid.Empty)
            {
                return Denied<SupplierQuotationRecord>(SupplierQuotationPersistenceOutcome.Duplicate, "quotation_duplicate");
            }

            if (await db.SupplierQuotations.AnyAsync(item => item.Id == command.Id, cancellationToken))
            {
                return Denied<SupplierQuotationRecord>(SupplierQuotationPersistenceOutcome.Duplicate, "quotation_duplicate");
            }

            var source = await db.PurchaseRequests
                .SingleOrDefaultAsync(item => item.Id == command.PurchaseRequestId, cancellationToken);
            if (source is null)
            {
                return Denied<SupplierQuotationRecord>(SupplierQuotationPersistenceOutcome.NotFound, "purchase_request_not_found");
            }

            if (source.Status != PurchaseRequestStatus.Approved)
            {
                return Denied<SupplierQuotationRecord>(SupplierQuotationPersistenceOutcome.InvalidState, "purchase_request_not_approved");
            }

            var entity = new SupplierQuotationEntity(command, tenantContext.TenantId);
            entity.TouchVersion();
            foreach (var line in command.Lines)
            {
                entity.Lines.Add(new SupplierQuotationLineEntity(
                    tenantContext.TenantId,
                    entity.Id,
                    entity.PurchaseRequestId,
                    line));
            }

            foreach (var item in command.Evidence)
            {
                entity.Evidence.Add(new SupplierQuotationEvidenceEntity(
                    tenantContext.TenantId,
                    entity.Id,
                    item));
            }

            db.SupplierQuotations.Add(entity);
            db.SupplierQuotationHistory.Add(new SupplierQuotationHistoryEntity(
                evidence.EvidenceId,
                tenantContext.TenantId,
                entity.Id,
                SupplierQuotationStatus.Draft,
                SupplierQuotationStatus.Draft,
                SupplierQuotationHistoryAction.Created,
                command.CreatedByActorId,
                null,
                evidence.CorrelationId,
                null,
                null,
                null,
                null,
                command.OccurredAt));
            db.SupplierQuotationAudit.Add(new SupplierQuotationAuditEntity(evidence));
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return SupplierQuotationPersistenceResult<SupplierQuotationRecord>.Success(ToRecord(entity));
        }
        catch (DbUpdateConcurrencyException)
        {
            return Denied<SupplierQuotationRecord>(SupplierQuotationPersistenceOutcome.Conflict, "concurrency_conflict");
        }
        catch (DbUpdateException)
        {
            return Denied<SupplierQuotationRecord>(SupplierQuotationPersistenceOutcome.Failure, "persistence_unavailable");
        }
        catch
        {
            return Denied<SupplierQuotationRecord>(SupplierQuotationPersistenceOutcome.Failure, "persistence_unavailable");
        }
    }

    public Task<SupplierQuotationPersistenceResult<SupplierQuotationRecord>> EditAsync(
        TenantContext tenantContext,
        SupplierQuotationEditCommand command,
        SupplierQuotationAuditEvidence evidence,
        CancellationToken cancellationToken = default) =>
        MutateAsync(
            tenantContext,
            command.Id,
            command.ExpectedVersion,
            evidence,
            async (db, entity, fromStatus) =>
            {
                if (fromStatus != SupplierQuotationStatus.Draft)
                {
                    return MutationDecision.Failure("edit_not_allowed");
                }

                var sourceApproved = await db.PurchaseRequests
                    .AnyAsync(item => item.Id == entity.PurchaseRequestId && item.Status == PurchaseRequestStatus.Approved, cancellationToken);
                if (!sourceApproved)
                {
                    return MutationDecision.Failure("purchase_request_not_approved");
                }

                db.SupplierQuotationLines.RemoveRange(entity.Lines.ToArray());
                entity.Lines.Clear();
                foreach (var line in command.Lines)
                {
                    entity.Lines.Add(new SupplierQuotationLineEntity(
                        tenantContext.TenantId,
                        entity.Id,
                        entity.PurchaseRequestId,
                        line));
                }

                entity.ReplaceDraft(command);
                entity.TouchVersion();
                foreach (var item in command.Evidence)
                {
                    entity.Evidence.Add(new SupplierQuotationEvidenceEntity(
                        tenantContext.TenantId,
                        entity.Id,
                        item));
                }

                db.SupplierQuotationHistory.Add(new SupplierQuotationHistoryEntity(
                    Guid.NewGuid(),
                    tenantContext.TenantId,
                    entity.Id,
                    fromStatus,
                    fromStatus,
                    SupplierQuotationHistoryAction.Edited,
                    evidence.ActorId,
                    null,
                    evidence.CorrelationId,
                    null,
                    null,
                    null,
                    null,
                    command.OccurredAt));
                return MutationDecision.Success();
            },
            cancellationToken);

    public Task<SupplierQuotationPersistenceResult<SupplierQuotationRecord>> SubmitAsync(
        TenantContext tenantContext,
        SupplierQuotationActionCommand command,
        SupplierQuotationAuditEvidence evidence,
        CancellationToken cancellationToken = default) =>
        TransitionAsync(
            tenantContext,
            command,
            evidence,
            SupplierQuotationStatus.Submitted,
            SupplierQuotationHistoryAction.Submitted,
            SupplierQuotationStatus.Draft,
            cancellationToken);

    public Task<SupplierQuotationPersistenceResult<SupplierQuotationRecord>> WithdrawAsync(
        TenantContext tenantContext,
        SupplierQuotationActionCommand command,
        SupplierQuotationAuditEvidence evidence,
        CancellationToken cancellationToken = default) =>
        TransitionAsync(
            tenantContext,
            command,
            evidence,
            SupplierQuotationStatus.Withdrawn,
            SupplierQuotationHistoryAction.Withdrawn,
            SupplierQuotationStatus.Submitted,
            cancellationToken);

    public Task<SupplierQuotationPersistenceResult<SupplierQuotationRecord>> DisqualifyAsync(
        TenantContext tenantContext,
        SupplierQuotationActionCommand command,
        SupplierQuotationAuditEvidence evidence,
        CancellationToken cancellationToken = default) =>
        TransitionAsync(
            tenantContext,
            command,
            evidence,
            SupplierQuotationStatus.Disqualified,
            SupplierQuotationHistoryAction.Disqualified,
            SupplierQuotationStatus.Submitted,
            cancellationToken);

    public async Task<IReadOnlyList<SupplierQuotationHistoryRecord>> ReadHistoryAsync(
        TenantContext tenantContext,
        Guid quotationId,
        CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(tenantContext);
        var records = await db.SupplierQuotationHistory
            .AsNoTracking()
            .Where(item => item.SupplierQuotationId == quotationId)
            .Select(item => new SupplierQuotationHistoryRecord(
                item.Id,
                item.SupplierQuotationId,
                item.OccurredAt,
                item.FromStatus,
                item.ToStatus,
                item.Action,
                item.ActorId,
                item.Reason,
                item.CorrelationId,
                item.PolicyId,
                item.PolicyVersion,
                item.StageKey,
                item.DelegatedFromActorId))
            .ToListAsync(cancellationToken);
        return records
            .OrderBy(item => item.OccurredAt)
            .ThenBy(item => item.EvidenceId)
            .ToArray();
    }

    public async Task<IReadOnlyList<SupplierQuotationAuditRecord>> ReadAuditAsync(
        TenantContext tenantContext,
        Guid quotationId,
        CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(tenantContext);
        var records = await db.SupplierQuotationAudit
            .AsNoTracking()
            .Where(item => item.SupplierQuotationId == quotationId)
            .Select(item => new SupplierQuotationAuditRecord(
                item.Id,
                item.SupplierQuotationId,
                item.PurchaseRequestId,
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
        return records
            .OrderBy(item => item.OccurredAt)
            .ThenBy(item => item.EvidenceId)
            .ToArray();
    }

    public async Task<SupplierSourceDecisionRecord?> FindSourceDecisionAsync(
        TenantContext tenantContext,
        Guid purchaseRequestId,
        CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(tenantContext);
        var decision = await db.SupplierSourceDecisions
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.PurchaseRequestId == purchaseRequestId, cancellationToken);
        return decision is null ? null : ToRecord(decision);
    }

    public async Task<SupplierQuotationPersistenceResult<SupplierSourceDecisionRecord>> RecordSourceDecisionAsync(
        TenantContext tenantContext,
        SupplierSourceDecisionCommand command,
        SupplierQuotationAuditEvidence evidence,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(evidence);
        await using var db = CreateContext(tenantContext);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var replay = await FindSourceDecisionReplayAsync(db, evidence, cancellationToken);
            if (replay is not null)
            {
                await transaction.CommitAsync(cancellationToken);
                return SupplierQuotationPersistenceResult<SupplierSourceDecisionRecord>.Success(replay);
            }

            if (command.Scope.TenantId != tenantContext.TenantId.Value
                || command.Id == Guid.Empty
                || command.ExpectedVersion is null
                || command.ExpectedVersion.Length == 0)
            {
                return Denied<SupplierSourceDecisionRecord>(SupplierQuotationPersistenceOutcome.Conflict, "concurrency_conflict");
            }

            var source = await db.PurchaseRequests
                .SingleOrDefaultAsync(item => item.Id == command.PurchaseRequestId, cancellationToken);
            if (source is null)
            {
                return Denied<SupplierSourceDecisionRecord>(SupplierQuotationPersistenceOutcome.NotFound, "purchase_request_not_found");
            }

            if (source.Status != PurchaseRequestStatus.Approved)
            {
                return Denied<SupplierSourceDecisionRecord>(SupplierQuotationPersistenceOutcome.InvalidState, "purchase_request_not_approved");
            }

            if (command.Scope.CompanyId != source.CompanyId || command.Scope.BranchId != source.BranchId)
            {
                return Denied<SupplierSourceDecisionRecord>(SupplierQuotationPersistenceOutcome.InvalidState, "resource_scope_denied");
            }

            var current = await db.SupplierSourceDecisions
                .SingleOrDefaultAsync(item => item.PurchaseRequestId == command.PurchaseRequestId, cancellationToken);
            var expectedVersion = current is null ? source.Version : current.Version;
            if (!VersionMatches(expectedVersion, command.ExpectedVersion))
            {
                return Denied<SupplierSourceDecisionRecord>(SupplierQuotationPersistenceOutcome.Conflict, "concurrency_conflict");
            }

            var selected = await db.SupplierQuotations
                .Include(item => item.Lines)
                .Include(item => item.Evidence)
                .SingleOrDefaultAsync(item => item.Id == command.SelectedQuotationId, cancellationToken);
            if (selected is null || selected.PurchaseRequestId != command.PurchaseRequestId)
            {
                return Denied<SupplierSourceDecisionRecord>(SupplierQuotationPersistenceOutcome.NotFound, "quotation_not_found");
            }

            if (selected.Status != SupplierQuotationStatus.Submitted)
            {
                return Denied<SupplierSourceDecisionRecord>(SupplierQuotationPersistenceOutcome.InvalidState, "source_decision_not_allowed");
            }

            var previousSelectedQuotationId = current?.SelectedQuotationId;
            if (previousSelectedQuotationId is { } previousId && previousId != selected.Id)
            {
                var previous = await db.SupplierQuotations
                    .Include(item => item.Lines)
                    .Include(item => item.Evidence)
                    .SingleOrDefaultAsync(item => item.Id == previousId, cancellationToken);
                if (previous is not null && previous.Status == SupplierQuotationStatus.Submitted)
                {
                    previous.SetStatus(SupplierQuotationStatus.Superseded, command.SelectedAt);
                    previous.TouchVersion();
                    db.SupplierQuotationHistory.Add(new SupplierQuotationHistoryEntity(
                        Guid.NewGuid(),
                        tenantContext.TenantId,
                        previous.Id,
                        SupplierQuotationStatus.Submitted,
                        SupplierQuotationStatus.Superseded,
                        SupplierQuotationHistoryAction.Superseded,
                        command.ActorId,
                        "superseded_by_source_decision",
                        evidence.CorrelationId,
                        command.PolicyId,
                        command.PolicyVersion,
                        command.StageKey,
                        null,
                        command.SelectedAt));
                    db.SupplierQuotationAudit.Add(new SupplierQuotationAuditEntity(evidence with
                    {
                        EvidenceId = Guid.NewGuid(),
                        SupplierQuotationId = previous.Id,
                        BeforeStatus = SupplierQuotationStatus.Submitted,
                        AfterStatus = SupplierQuotationStatus.Superseded,
                        Reason = "superseded_by_source_decision",
                        BeforeSummary = SupplierQuotationStatus.Submitted.ToString(),
                        AfterSummary = SupplierQuotationStatus.Superseded.ToString()
                    }));
                }
            }

            if (current is null)
            {
                current = new SupplierSourceDecisionEntity(command, tenantContext.TenantId, ToRecord(selected));
                current.TouchVersion();
                db.SupplierSourceDecisions.Add(current);
            }
            else
            {
                current.Replace(command, ToRecord(selected));
                current.TouchVersion();
            }

            db.SupplierSourceDecisionHistory.Add(new SupplierSourceDecisionHistoryEntity(
                Guid.NewGuid(),
                tenantContext.TenantId,
                current.Id,
                command.PurchaseRequestId,
                previousSelectedQuotationId,
                command));
            db.SupplierQuotationAudit.Add(new SupplierQuotationAuditEntity(evidence));
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return SupplierQuotationPersistenceResult<SupplierSourceDecisionRecord>.Success(ToRecord(current));
        }
        catch (DbUpdateConcurrencyException)
        {
            return Denied<SupplierSourceDecisionRecord>(SupplierQuotationPersistenceOutcome.Conflict, "concurrency_conflict");
        }
        catch (DbUpdateException)
        {
            return Denied<SupplierSourceDecisionRecord>(SupplierQuotationPersistenceOutcome.Failure, "persistence_unavailable");
        }
        catch
        {
            return Denied<SupplierSourceDecisionRecord>(SupplierQuotationPersistenceOutcome.Failure, "persistence_unavailable");
        }
    }

    private async Task<SupplierQuotationPersistenceResult<SupplierQuotationRecord>> TransitionAsync(
        TenantContext tenantContext,
        SupplierQuotationActionCommand command,
        SupplierQuotationAuditEvidence evidence,
        SupplierQuotationStatus targetStatus,
        SupplierQuotationHistoryAction historyAction,
        SupplierQuotationStatus expectedStatus,
        CancellationToken cancellationToken) =>
        await MutateAsync(
            tenantContext,
            command.Id,
            command.ExpectedVersion,
            evidence,
            (db, entity, fromStatus) =>
            {
                if (fromStatus != expectedStatus)
                {
                    return Task.FromResult(MutationDecision.Failure("action_not_allowed"));
                }

                entity.SetStatus(targetStatus, command.OccurredAt);
                entity.TouchVersion();
                db.SupplierQuotationHistory.Add(new SupplierQuotationHistoryEntity(
                    Guid.NewGuid(),
                    tenantContext.TenantId,
                    entity.Id,
                    fromStatus,
                    targetStatus,
                    historyAction,
                    command.ActorId,
                    command.Reason,
                    evidence.CorrelationId,
                    null,
                    null,
                    null,
                    null,
                    command.OccurredAt));
                return Task.FromResult(MutationDecision.Success());
            },
            cancellationToken);

    private async Task<SupplierQuotationPersistenceResult<SupplierQuotationRecord>> MutateAsync(
        TenantContext tenantContext,
        Guid quotationId,
        byte[] expectedVersion,
        SupplierQuotationAuditEvidence evidence,
        Func<ProcurementDbContext, SupplierQuotationEntity, SupplierQuotationStatus, Task<MutationDecision>> mutation,
        CancellationToken cancellationToken)
    {
        if (expectedVersion is null || expectedVersion.Length == 0)
        {
            return Denied<SupplierQuotationRecord>(SupplierQuotationPersistenceOutcome.Conflict, "concurrency_conflict");
        }

        await using var db = CreateContext(tenantContext);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var entity = await db.SupplierQuotations
                .Include(item => item.Lines)
                .Include(item => item.Evidence)
                .SingleOrDefaultAsync(item => item.Id == quotationId, cancellationToken);
            if (entity is null)
            {
                return Denied<SupplierQuotationRecord>(SupplierQuotationPersistenceOutcome.NotFound, "quotation_not_found");
            }

            var replay = await FindReplayAsync(db, evidence, cancellationToken);
            if (replay is not null)
            {
                await transaction.CommitAsync(cancellationToken);
                return SupplierQuotationPersistenceResult<SupplierQuotationRecord>.Success(replay);
            }

            if (!VersionMatches(entity.Version, expectedVersion))
            {
                return Denied<SupplierQuotationRecord>(SupplierQuotationPersistenceOutcome.Conflict, "concurrency_conflict");
            }

            var fromStatus = entity.Status;
            var decision = await mutation(db, entity, fromStatus);
            if (!decision.Succeeded)
            {
                return Denied<SupplierQuotationRecord>(SupplierQuotationPersistenceOutcome.InvalidState, decision.Code);
            }

            db.SupplierQuotationAudit.Add(new SupplierQuotationAuditEntity(evidence with
            {
                BeforeStatus = evidence.BeforeStatus ?? fromStatus,
                AfterStatus = evidence.AfterStatus ?? entity.Status,
                AfterSummary = evidence.AfterSummary ?? entity.Status.ToString()
            }));
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return SupplierQuotationPersistenceResult<SupplierQuotationRecord>.Success(ToRecord(entity));
        }
        catch (DbUpdateConcurrencyException)
        {
            return Denied<SupplierQuotationRecord>(SupplierQuotationPersistenceOutcome.Conflict, "concurrency_conflict");
        }
        catch (DbUpdateException)
        {
            return Denied<SupplierQuotationRecord>(SupplierQuotationPersistenceOutcome.Failure, "persistence_unavailable");
        }
        catch
        {
            return Denied<SupplierQuotationRecord>(SupplierQuotationPersistenceOutcome.Failure, "persistence_unavailable");
        }
    }

    private async Task<SupplierQuotationRecord?> FindReplayAsync(
        ProcurementDbContext db,
        SupplierQuotationAuditEvidence evidence,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(evidence.IdempotencyKey))
        {
            return null;
        }

        var replayIds = await db.SupplierQuotationAudit
            .Where(item => item.ActorId == evidence.ActorId
                && item.OperationId == evidence.OperationId
                && item.IdempotencyKey == evidence.IdempotencyKey)
            .Select(item => new { item.SupplierQuotationId, item.OccurredAt })
            .ToListAsync(cancellationToken);
        var quotationId = replayIds
            .OrderByDescending(item => item.OccurredAt)
            .Select(item => item.SupplierQuotationId)
            .FirstOrDefault();
        if (quotationId == Guid.Empty)
        {
            return null;
        }

        var quotation = await db.SupplierQuotations
            .Include(item => item.Lines)
            .Include(item => item.Evidence)
            .SingleOrDefaultAsync(item => item.Id == quotationId, cancellationToken);
        return quotation is null ? null : ToRecord(quotation);
    }

    private async Task<SupplierSourceDecisionRecord?> FindSourceDecisionReplayAsync(
        ProcurementDbContext db,
        SupplierQuotationAuditEvidence evidence,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(evidence.IdempotencyKey))
        {
            return null;
        }

        var hasReplay = await db.SupplierQuotationAudit
            .AnyAsync(item => item.ActorId == evidence.ActorId
                && item.OperationId == evidence.OperationId
                && item.IdempotencyKey == evidence.IdempotencyKey,
                cancellationToken);
        if (!hasReplay)
        {
            return null;
        }

        var decision = await db.SupplierSourceDecisions
            .SingleOrDefaultAsync(item => item.PurchaseRequestId == evidence.PurchaseRequestId, cancellationToken);
        return decision is null ? null : ToRecord(decision);
    }

    private ProcurementDbContext CreateContext(TenantContext tenantContext) =>
        new(options, tenantContext);

    private static IQueryable<SupplierQuotationEntity> ApplyTrustedScope(
        IQueryable<SupplierQuotationEntity> query,
        ScopeReference? scope)
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

    private static bool VersionMatches(byte[] actual, byte[] expected) =>
        actual.SequenceEqual(expected);

    private static SupplierQuotationRecord ToRecord(SupplierQuotationEntity entity) =>
        new(
            entity.Id,
            entity.TenantId.Value,
            entity.PurchaseRequestId,
            new PurchaseRequestScope(entity.TenantId.Value, entity.CompanyId, entity.BranchId),
            entity.CreatedByActorId,
            new SupplierQuotationSupplierSnapshot(entity.SupplierId, entity.SupplierCode, entity.SupplierName),
            entity.Status,
            entity.SupplierQuotationReference,
            entity.OfferDate,
            entity.ValidUntil,
            new SupplierQuotationCurrencySnapshot(entity.CurrencyId, entity.CurrencyCode, entity.CurrencyName),
            entity.PaymentTermId is { } paymentTermId
                ? new SupplierQuotationPaymentTermSnapshot(
                    paymentTermId,
                    entity.PaymentTermCode ?? string.Empty,
                    entity.PaymentTermName ?? entity.PaymentTermCode ?? string.Empty,
                    entity.PaymentTermVersion ?? 0)
                : null,
            entity.DeliveryTerms,
            entity.OfferedDeliveryDate,
            entity.OfferedDeliveryLeadTime,
            entity.Notes,
            entity.Lines
                .OrderBy(item => item.PurchaseRequestLineId)
                .ThenBy(item => item.Id)
                .Select(item => new SupplierQuotationLineSnapshot(
                    item.Id,
                    item.PurchaseRequestLineId,
                    item.ProductId,
                    item.ProductSku,
                    item.ProductName,
                    item.UnitOfMeasureId,
                    item.UnitOfMeasureCode,
                    item.RequestedQuantity,
                    item.QuotedQuantity,
                    item.UnitPrice,
                    item.DiscountAmount,
                    item.DiscountPercentage,
                    item.TaxId,
                    item.TaxCode,
                    item.TaxName,
                    item.TaxRatePercentage,
                    item.TaxAmount,
                    item.TaxReference,
                    item.RequestedNeedByDate,
                    item.OfferedDeliveryDate,
                    item.OfferedDeliveryLeadTime,
                    item.Notes,
                    item.Version.ToArray()))
                .ToArray(),
            entity.Evidence
                .OrderBy(item => item.RecordedAt)
                .ThenBy(item => item.Id)
                .Select(item => new SupplierQuotationEvidenceReference(
                    item.Id,
                    item.ReferenceId,
                    item.FileName,
                    item.ContentType,
                    item.Description,
                    item.Source,
                    item.ExternalReference,
                    item.RecordedByActorId,
                    item.RecordedAt,
                    item.Version.ToArray()))
                .ToArray(),
            entity.CreatedAt,
            entity.UpdatedAt,
            entity.SubmittedAt,
            entity.Version.ToArray());

    private static SupplierSourceDecisionRecord ToRecord(SupplierSourceDecisionEntity entity) =>
        new(
            entity.Id,
            entity.TenantId.Value,
            entity.PurchaseRequestId,
            new PurchaseRequestScope(entity.TenantId.Value, entity.CompanyId, entity.BranchId),
            entity.SelectedQuotationId,
            new SupplierQuotationSupplierSnapshot(entity.SupplierId, entity.SupplierCode, entity.SupplierName),
            entity.SupplierQuotationReference,
            entity.ActorId,
            entity.SelectedAt,
            entity.Rationale,
            entity.PolicyId,
            entity.PolicyVersion,
            entity.StageKey,
            entity.ComparisonSnapshotReference,
            entity.ComparisonSnapshotJson,
            entity.Version.ToArray());

    private static SupplierQuotationPersistenceResult<T> Denied<T>(
        SupplierQuotationPersistenceOutcome outcome,
        string code) =>
        SupplierQuotationPersistenceResult<T>.Denied(outcome, code);

    private sealed record MutationDecision(bool Succeeded, string Code)
    {
        internal static MutationDecision Success() => new(true, "succeeded");

        internal static MutationDecision Failure(string code) => new(false, code);
    }
}

#pragma warning restore CS1591
