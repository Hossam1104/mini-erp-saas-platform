#pragma warning disable CS1591

using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.Modules.Procurement;
using MiniErp.Contracts.Modules.Procurement;

namespace MiniErp.Infrastructure.Persistence.Modules.Procurement;

public sealed class PurchaseRequestPersistence : IPurchaseRequestPersistence
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly DbContextOptions options;

    internal PurchaseRequestPersistence(DbContextOptions options)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<IReadOnlyList<PurchaseRequestListRecord>> ListAsync(
        TenantContext tenantContext,
        PurchaseRequestStatus? status,
        CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(tenantContext);
        var query = ApplyTrustedScope(db.PurchaseRequests.AsNoTracking(), tenantContext.Scope);
        if (status is { } selectedStatus)
        {
            query = query.Where(item => item.Status == selectedStatus);
        }

        var records = await query
            .Select(item => new PurchaseRequestListRecord(
                item.Id,
                item.TenantId.Value,
                new PurchaseRequestScope(item.TenantId.Value, item.CompanyId, item.BranchId),
                item.RequesterId,
                item.Status,
                item.Purpose,
                item.Lines.Count,
                item.CreatedAt,
                item.UpdatedAt,
                item.Version.ToArray()))
            .ToListAsync(cancellationToken);
        return records
            .OrderByDescending(item => item.CreatedAt)
            .ThenByDescending(item => item.Id)
            .ToArray();
    }

    public async Task<PurchaseRequestRecord?> FindAsync(
        TenantContext tenantContext,
        Guid purchaseRequestId,
        CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(tenantContext);
        var entity = await db.PurchaseRequests
            .AsNoTracking()
            .Include(item => item.Lines)
            .SingleOrDefaultAsync(item => item.Id == purchaseRequestId, cancellationToken);
        return entity is null ? null : ToRecord(entity);
    }

    public async Task<PurchaseRequestPersistenceResult<PurchaseRequestRecord>> CreateAsync(
        TenantContext tenantContext,
        PurchaseRequestCreateCommand command,
        PurchaseRequestAuditEvidence evidence,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(command);
        ArgumentNullException.ThrowIfNull(evidence);
        await using var db = CreateContext(tenantContext);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            if (command.Scope.TenantId != tenantContext.TenantId.Value
                || command.Id == Guid.Empty
                || await db.PurchaseRequests.AnyAsync(item => item.Id == command.Id, cancellationToken))
            {
                return PurchaseRequestPersistenceResult<PurchaseRequestRecord>.Denied(
                    PurchaseRequestPersistenceOutcome.Duplicate,
                    "purchase_request_duplicate");
            }

            var entity = new PurchaseRequestEntity(
                command.Id,
                tenantContext.TenantId,
                command.Scope.CompanyId,
                command.Scope.BranchId,
                command.RequesterId,
                command.Purpose,
                command.OccurredAt);
            foreach (var line in command.Lines)
            {
                entity.Lines.Add(new PurchaseRequestLineEntity(
                    line.Id,
                    tenantContext.TenantId,
                    command.Id,
                    line));
            }

            db.PurchaseRequests.Add(entity);
            db.PurchaseRequestHistory.Add(new PurchaseRequestHistoryEntity(
                evidence.EvidenceId,
                tenantContext.TenantId,
                command.Id,
                PurchaseRequestStatus.Draft,
                PurchaseRequestStatus.Draft,
                PurchaseRequestHistoryAction.Created,
                command.RequesterId,
                null,
                evidence.CorrelationId,
                null,
                null,
                null,
                null,
                command.OccurredAt));
            db.PurchaseRequestAudit.Add(new PurchaseRequestAuditEntity(evidence));
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return PurchaseRequestPersistenceResult<PurchaseRequestRecord>.Success(ToRecord(entity));
        }
        catch (DbUpdateConcurrencyException)
        {
            return PurchaseRequestPersistenceResult<PurchaseRequestRecord>.Denied(
                PurchaseRequestPersistenceOutcome.Conflict,
                "concurrency_conflict");
        }
        catch (DbUpdateException)
        {
            return PurchaseRequestPersistenceResult<PurchaseRequestRecord>.Denied(
                PurchaseRequestPersistenceOutcome.Duplicate,
                "purchase_request_duplicate");
        }
        catch
        {
            return PurchaseRequestPersistenceResult<PurchaseRequestRecord>.Denied(
                PurchaseRequestPersistenceOutcome.Failure,
                "persistence_unavailable");
        }
    }

    public Task<PurchaseRequestPersistenceResult<PurchaseRequestRecord>> EditAsync(
        TenantContext tenantContext,
        PurchaseRequestEditCommand command,
        PurchaseRequestAuditEvidence evidence,
        CancellationToken cancellationToken = default) =>
        MutateAsync(
            tenantContext,
            command.Id,
            command.ExpectedVersion,
            evidence,
            async (db, entity, fromStatus) =>
            {
                await Task.CompletedTask;
                if (entity.Status is not (PurchaseRequestStatus.Draft or PurchaseRequestStatus.ReturnedForChange))
                {
                    return MutationDecision.Failure("edit_not_allowed");
                }

                db.PurchaseRequestLines.RemoveRange(entity.Lines.ToArray());
                entity.Lines.Clear();
                foreach (var line in command.Lines)
                {
                    entity.Lines.Add(new PurchaseRequestLineEntity(
                        line.Id,
                        tenantContext.TenantId,
                        entity.Id,
                        line));
                }

                entity.ReplaceDraft(
                    command.Scope.CompanyId,
                    command.Scope.BranchId,
                    command.Purpose,
                    command.OccurredAt);
                entity.TouchVersion();
                db.PurchaseRequestHistory.Add(new PurchaseRequestHistoryEntity(
                    Guid.NewGuid(),
                    tenantContext.TenantId,
                    entity.Id,
                    fromStatus,
                    fromStatus,
                    PurchaseRequestHistoryAction.Edited,
                    command.RequesterId,
                    null,
                    evidence.CorrelationId,
                    entity.ApprovalPolicySnapshotJson.Length == 0 ? null : ReadPolicy(entity)?.PolicyId,
                    entity.ApprovalPolicySnapshotJson.Length == 0 ? null : ReadPolicy(entity)?.Version,
                    null,
                    null,
                    command.OccurredAt));
                return MutationDecision.Success();
            },
            cancellationToken);

    public Task<PurchaseRequestPersistenceResult<PurchaseRequestRecord>> SubmitAsync(
        TenantContext tenantContext,
        PurchaseRequestSubmitCommand command,
        PurchaseRequestAuditEvidence evidence,
        CancellationToken cancellationToken = default) =>
        MutateAsync(
            tenantContext,
            command.Id,
            command.ExpectedVersion,
            evidence,
            async (db, entity, fromStatus) =>
            {
                await Task.CompletedTask;
                if (entity.Status is not (PurchaseRequestStatus.Draft or PurchaseRequestStatus.ReturnedForChange))
                {
                    return MutationDecision.Failure("submit_not_allowed");
                }

                var policyJson = JsonSerializer.Serialize(command.Policy, JsonOptions);
                entity.Submit(command.Policy, policyJson, command.OccurredAt);
                entity.TouchVersion();
                db.PurchaseRequestHistory.Add(new PurchaseRequestHistoryEntity(
                    Guid.NewGuid(),
                    tenantContext.TenantId,
                    entity.Id,
                    fromStatus,
                    PurchaseRequestStatus.PendingApproval,
                    PurchaseRequestHistoryAction.Submitted,
                    evidence.ActorId,
                    null,
                    evidence.CorrelationId,
                    command.Policy.PolicyId,
                    command.Policy.Version,
                    command.Policy.Stages.OrderBy(item => item.Sequence).First().StageKey,
                    null,
                    command.OccurredAt));
                return MutationDecision.Success();
            },
            cancellationToken);

    public Task<PurchaseRequestPersistenceResult<PurchaseRequestRecord>> ApproveAsync(
        TenantContext tenantContext,
        PurchaseRequestApprovalCommand command,
        PurchaseRequestAuditEvidence evidence,
        CancellationToken cancellationToken = default) =>
        MutateAsync(
            tenantContext,
            command.Id,
            command.ExpectedVersion,
            evidence,
            async (db, entity, fromStatus) =>
            {
                await Task.CompletedTask;
                if (entity.Status != PurchaseRequestStatus.PendingApproval)
                {
                    return MutationDecision.Failure("decision_not_allowed");
                }

                if (entity.RequesterId == command.ActorId)
                {
                    return MutationDecision.Failure("self_approval_denied");
                }

                var policy = ReadPolicy(entity);
                if (!PurchaseRequestValuePolicy.IsValidPolicy(policy))
                {
                    return MutationDecision.Failure("approval_policy_invalid");
                }

                var stages = policy!.Stages.OrderBy(item => item.Sequence).ToArray();
                if (entity.CurrentApprovalStageIndex < 0 || entity.CurrentApprovalStageIndex >= stages.Length)
                {
                    return MutationDecision.Failure("approval_policy_invalid");
                }

                var stage = stages[entity.CurrentApprovalStageIndex];
                var approvers = ReadApprovers(entity).ToList();
                if (approvers.Contains(command.ActorId))
                {
                    return MutationDecision.Failure("approval_duplicate");
                }

                approvers.Add(command.ActorId);
                var stageComplete = approvers.Count >= stage.RequiredApprovals;
                var nextStageIndex = entity.CurrentApprovalStageIndex + (stageComplete ? 1 : 0);
                var resultingStatus = stageComplete && nextStageIndex >= stages.Length
                    ? PurchaseRequestStatus.Approved
                    : PurchaseRequestStatus.PendingApproval;
                var nextApprovers = stageComplete ? [] : approvers.ToArray();
                var nextCount = stageComplete ? 0 : approvers.Count;
                entity.RecordApproval(
                    resultingStatus,
                    nextStageIndex >= stages.Length ? entity.CurrentApprovalStageIndex : nextStageIndex,
                    nextCount,
                    JsonSerializer.Serialize(nextApprovers, JsonOptions),
                    command.OccurredAt);
                entity.TouchVersion();
                db.PurchaseRequestHistory.Add(new PurchaseRequestHistoryEntity(
                    Guid.NewGuid(),
                    tenantContext.TenantId,
                    entity.Id,
                    fromStatus,
                    resultingStatus,
                    PurchaseRequestHistoryAction.ApprovalRecorded,
                    command.ActorId,
                    null,
                    evidence.CorrelationId,
                    policy.PolicyId,
                    policy.Version,
                    stage.StageKey,
                    command.DelegatedFromActorId,
                    command.OccurredAt));
                return MutationDecision.Success();
            },
            cancellationToken);

    public Task<PurchaseRequestPersistenceResult<PurchaseRequestRecord>> RejectAsync(
        TenantContext tenantContext,
        PurchaseRequestActionCommand command,
        PurchaseRequestAuditEvidence evidence,
        CancellationToken cancellationToken = default) =>
        DecisionAsync(tenantContext, command, evidence, PurchaseRequestStatus.Rejected, PurchaseRequestHistoryAction.Rejected, cancellationToken);

    public Task<PurchaseRequestPersistenceResult<PurchaseRequestRecord>> ReturnForChangeAsync(
        TenantContext tenantContext,
        PurchaseRequestActionCommand command,
        PurchaseRequestAuditEvidence evidence,
        CancellationToken cancellationToken = default) =>
        DecisionAsync(tenantContext, command, evidence, PurchaseRequestStatus.ReturnedForChange, PurchaseRequestHistoryAction.ReturnedForChange, cancellationToken);

    public Task<PurchaseRequestPersistenceResult<PurchaseRequestRecord>> CancelAsync(
        TenantContext tenantContext,
        PurchaseRequestActionCommand command,
        PurchaseRequestAuditEvidence evidence,
        CancellationToken cancellationToken = default) =>
        MutateAsync(
            tenantContext,
            command.Id,
            command.ExpectedVersion,
            evidence,
            async (db, entity, fromStatus) =>
            {
                await Task.CompletedTask;
                if (entity.RequesterId != command.ActorId)
                {
                    return MutationDecision.Failure("requester_only");
                }

                var policy = ReadPolicy(entity);
                var allowed = fromStatus is PurchaseRequestStatus.Draft or PurchaseRequestStatus.ReturnedForChange
                    || fromStatus == PurchaseRequestStatus.PendingApproval
                        && policy?.AllowRequesterCancellationWhilePending == true;
                if (!allowed)
                {
                    return MutationDecision.Failure("cancel_not_allowed");
                }

                entity.SetDecision(PurchaseRequestStatus.Cancelled, command.OccurredAt);
                entity.TouchVersion();
                db.PurchaseRequestHistory.Add(new PurchaseRequestHistoryEntity(
                    Guid.NewGuid(),
                    tenantContext.TenantId,
                    entity.Id,
                    fromStatus,
                    PurchaseRequestStatus.Cancelled,
                    PurchaseRequestHistoryAction.Cancelled,
                    command.ActorId,
                    command.Reason,
                    evidence.CorrelationId,
                    policy?.PolicyId,
                    policy?.Version,
                    null,
                    null,
                    command.OccurredAt));
                return MutationDecision.Success();
            },
            cancellationToken);

    public async Task<IReadOnlyList<PurchaseRequestHistoryRecord>> ReadHistoryAsync(
        TenantContext tenantContext,
        Guid purchaseRequestId,
        CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(tenantContext);
        var records = await db.PurchaseRequestHistory
            .AsNoTracking()
            .Where(item => item.PurchaseRequestId == purchaseRequestId)
            .Select(item => new PurchaseRequestHistoryRecord(
                item.Id,
                item.PurchaseRequestId,
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

    public async Task<IReadOnlyList<PurchaseRequestAuditRecord>> ReadAuditAsync(
        TenantContext tenantContext,
        Guid purchaseRequestId,
        CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(tenantContext);
        var records = await db.PurchaseRequestAudit
            .AsNoTracking()
            .Where(item => item.PurchaseRequestId == purchaseRequestId)
            .Select(item => new PurchaseRequestAuditRecord(
                item.Id,
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

    private Task<PurchaseRequestPersistenceResult<PurchaseRequestRecord>> DecisionAsync(
        TenantContext tenantContext,
        PurchaseRequestActionCommand command,
        PurchaseRequestAuditEvidence evidence,
        PurchaseRequestStatus targetStatus,
        PurchaseRequestHistoryAction action,
        CancellationToken cancellationToken) =>
        MutateAsync(
            tenantContext,
            command.Id,
            command.ExpectedVersion,
            evidence,
            async (db, entity, fromStatus) =>
            {
                await Task.CompletedTask;
                if (fromStatus != PurchaseRequestStatus.PendingApproval)
                {
                    return MutationDecision.Failure("decision_not_allowed");
                }

                if (entity.RequesterId == command.ActorId)
                {
                    return MutationDecision.Failure("self_approval_denied");
                }

                var policy = ReadPolicy(entity);
                var stage = policy?.Stages
                    .OrderBy(item => item.Sequence)
                    .ElementAtOrDefault(entity.CurrentApprovalStageIndex);
                entity.SetDecision(targetStatus, command.OccurredAt);
                entity.TouchVersion();
                db.PurchaseRequestHistory.Add(new PurchaseRequestHistoryEntity(
                    Guid.NewGuid(),
                    tenantContext.TenantId,
                    entity.Id,
                    fromStatus,
                    targetStatus,
                    action,
                    command.ActorId,
                    command.Reason,
                    evidence.CorrelationId,
                    policy?.PolicyId,
                    policy?.Version,
                    stage?.StageKey,
                    null,
                    command.OccurredAt));
                return MutationDecision.Success();
            },
            cancellationToken);

    private async Task<PurchaseRequestPersistenceResult<PurchaseRequestRecord>> MutateAsync(
        TenantContext tenantContext,
        Guid purchaseRequestId,
        byte[] expectedVersion,
        PurchaseRequestAuditEvidence evidence,
        Func<ProcurementDbContext, PurchaseRequestEntity, PurchaseRequestStatus, Task<MutationDecision>> mutation,
        CancellationToken cancellationToken)
    {
        if (expectedVersion is null || expectedVersion.Length == 0)
        {
            return PurchaseRequestPersistenceResult<PurchaseRequestRecord>.Denied(
                PurchaseRequestPersistenceOutcome.Conflict,
                "concurrency_conflict");
        }

        await using var db = CreateContext(tenantContext);
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        try
        {
            var entity = await db.PurchaseRequests
                .Include(item => item.Lines)
                .SingleOrDefaultAsync(item => item.Id == purchaseRequestId, cancellationToken);
            if (entity is null)
            {
                return PurchaseRequestPersistenceResult<PurchaseRequestRecord>.Denied(
                    PurchaseRequestPersistenceOutcome.NotFound,
                    "purchase_request_not_found");
            }

            if (!VersionMatches(entity.Version, expectedVersion))
            {
                return PurchaseRequestPersistenceResult<PurchaseRequestRecord>.Denied(
                    PurchaseRequestPersistenceOutcome.Conflict,
                    "concurrency_conflict");
            }

            var fromStatus = entity.Status;
            var decision = await mutation(db, entity, fromStatus);
            if (!decision.Succeeded)
            {
                return PurchaseRequestPersistenceResult<PurchaseRequestRecord>.Denied(
                    PurchaseRequestPersistenceOutcome.InvalidState,
                    decision.Code);
            }

            var audit = evidence with
            {
                BeforeStatus = evidence.BeforeStatus ?? fromStatus,
                AfterStatus = evidence.AfterStatus ?? entity.Status,
                AfterSummary = evidence.AfterSummary ?? entity.Status.ToString()
            };
            db.PurchaseRequestAudit.Add(new PurchaseRequestAuditEntity(audit));
            await db.SaveChangesAsync(cancellationToken);
            await transaction.CommitAsync(cancellationToken);
            return PurchaseRequestPersistenceResult<PurchaseRequestRecord>.Success(ToRecord(entity));
        }
        catch (DbUpdateConcurrencyException)
        {
            return PurchaseRequestPersistenceResult<PurchaseRequestRecord>.Denied(
                PurchaseRequestPersistenceOutcome.Conflict,
                "concurrency_conflict");
        }
        catch (DbUpdateException)
        {
            return PurchaseRequestPersistenceResult<PurchaseRequestRecord>.Denied(
                PurchaseRequestPersistenceOutcome.Failure,
                "persistence_unavailable");
        }
        catch
        {
            return PurchaseRequestPersistenceResult<PurchaseRequestRecord>.Denied(
                PurchaseRequestPersistenceOutcome.Failure,
                "persistence_unavailable");
        }
    }

    private ProcurementDbContext CreateContext(TenantContext tenantContext) =>
        new(options, tenantContext);

    private static IQueryable<PurchaseRequestEntity> ApplyTrustedScope(
        IQueryable<PurchaseRequestEntity> query,
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
            _ => query.Where(_ => false)
        };
    }

    private static bool VersionMatches(byte[] actual, byte[] expected) =>
        actual.SequenceEqual(expected);

    private static PurchaseRequestApprovalPolicyDefinition? ReadPolicy(PurchaseRequestEntity entity)
    {
        if (string.IsNullOrWhiteSpace(entity.ApprovalPolicySnapshotJson))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<PurchaseRequestApprovalPolicyDefinition>(
                entity.ApprovalPolicySnapshotJson,
                JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static IReadOnlyList<Guid> ReadApprovers(PurchaseRequestEntity entity)
    {
        try
        {
            return JsonSerializer.Deserialize<Guid[]>(entity.CurrentStageApproverIdsJson, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            return [];
        }
    }

    private static PurchaseRequestRecord ToRecord(PurchaseRequestEntity entity)
    {
        var policy = ReadPolicy(entity);
        return new PurchaseRequestRecord(
            entity.Id,
            entity.TenantId.Value,
            new PurchaseRequestScope(entity.TenantId.Value, entity.CompanyId, entity.BranchId),
            entity.RequesterId,
            entity.Status,
            entity.Purpose,
            entity.Lines
                .OrderBy(item => item.Id)
                .Select(item => new PurchaseRequestLineSnapshot(
                    item.Id,
                    item.ProductId,
                    item.ProductSku,
                    item.ProductName,
                    item.UnitOfMeasureId,
                    item.UnitOfMeasureCode,
                    item.Quantity,
                    item.NeedByDate,
                    item.Purpose,
                    item.Version.ToArray()))
                .ToArray(),
            policy,
            entity.CurrentApprovalStageIndex,
            entity.CurrentStageApprovalCount,
            ReadApprovers(entity),
            entity.CreatedAt,
            entity.UpdatedAt,
            entity.SubmittedAt,
            entity.ApprovedAt,
            entity.CancelledAt,
            entity.Version.ToArray());
    }

    private sealed record MutationDecision(bool Succeeded, string Code)
    {
        internal static MutationDecision Success() => new(true, "succeeded");

        internal static MutationDecision Failure(string code) => new(false, code);
    }
}

#pragma warning restore CS1591
