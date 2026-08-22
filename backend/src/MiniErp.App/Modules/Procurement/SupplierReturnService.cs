#pragma warning disable CS1591

using System.Security.Cryptography;
using System.Text;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.Contracts.Modules.Procurement;

namespace MiniErp.App.Modules.Procurement;

/// <summary>
/// Coordinates the Procurement-owned supplier-return journey. This service records commercial
/// intent and downstream handoff evidence only; Inventory remains authoritative for stock movement
/// and Finance remains authoritative for credit, AP, tax, and posting consequences.
/// </summary>
public sealed class SupplierReturnService
{
    private const string CreateOperationId = "procurement.supplier-return.create";
    private readonly PurchaseRequestAuthorizationService authorization;
    private readonly ISupplierReturnPersistence persistence;
    private readonly ISupplierReturnInventoryEffectReader inventoryEffectReader;
    private readonly ISupplierReturnPhysicalEffectGate physicalEffectGate;

    public SupplierReturnService(
        PurchaseRequestAuthorizationService authorization,
        ISupplierReturnPersistence persistence,
        ISupplierReturnInventoryEffectReader? inventoryEffectReader = null,
        ISupplierReturnPhysicalEffectGate? physicalEffectGate = null)
    {
        this.authorization = authorization ?? throw new ArgumentNullException(nameof(authorization));
        this.persistence = persistence ?? throw new ArgumentNullException(nameof(persistence));
        this.inventoryEffectReader = inventoryEffectReader ?? new UnavailableSupplierReturnInventoryEffectReader();
        this.physicalEffectGate = physicalEffectGate ?? new SupplierReturnPhysicalEffectGate();
    }

    public async Task<SupplierReturnOperationResult<IReadOnlyList<SupplierReturnListRecord>>> ListAsync(
        ProcurementRequestContext context,
        SupplierReturnStatus? status,
        Guid? supplierId,
        CancellationToken cancellationToken = default)
    {
        var authorized = authorization.Authorize(context, "procurement.supplier-return.list");
        if (!authorized.Allowed)
        {
            return SupplierReturnOperationResult<IReadOnlyList<SupplierReturnListRecord>>.Failure(authorized.Code);
        }

        try
        {
            var records = await persistence.ListAsync(context.TenantContext, status, supplierId, cancellationToken);
            return SupplierReturnOperationResult<IReadOnlyList<SupplierReturnListRecord>>.Success(
                records.Where(item => authorization.Authorize(context, "procurement.supplier-return.list", item.Scope).Allowed).ToArray());
        }
        catch
        {
            return SupplierReturnOperationResult<IReadOnlyList<SupplierReturnListRecord>>.Failure("persistence_unavailable");
        }
    }

    public async Task<SupplierReturnOperationResult<IReadOnlyList<SupplierReturnEligibleSourceRecord>>> ListEligibleSourcesAsync(
        ProcurementRequestContext context,
        CancellationToken cancellationToken = default)
    {
        var authorized = authorization.Authorize(context, "procurement.supplier-return.eligible-source.list");
        if (!authorized.Allowed)
        {
            return SupplierReturnOperationResult<IReadOnlyList<SupplierReturnEligibleSourceRecord>>.Failure(authorized.Code);
        }

        try
        {
            var sources = await persistence.ListEligibleSourcesAsync(context.TenantContext, cancellationToken);
            return SupplierReturnOperationResult<IReadOnlyList<SupplierReturnEligibleSourceRecord>>.Success(
                sources
                    .Where(item => authorization.Authorize(context, "procurement.supplier-return.eligible-source.list", item.Scope).Allowed)
                    .Where(item => item.Lines.Any(line => line.EligibleReturnQuantity > 0m))
                    .OrderBy(item => item.SupplierName, StringComparer.Ordinal)
                    .ToArray());
        }
        catch
        {
            return SupplierReturnOperationResult<IReadOnlyList<SupplierReturnEligibleSourceRecord>>.Failure("persistence_unavailable");
        }
    }

    public async Task<SupplierReturnOperationResult<SupplierReturnRecord>> GetAsync(
        ProcurementRequestContext context,
        Guid supplierReturnId,
        CancellationToken cancellationToken = default) =>
        await GetAuthorizedAsync(context, supplierReturnId, "procurement.supplier-return.read", cancellationToken);

    public async Task<SupplierReturnOperationResult<SupplierReturnReportRecord>> ReportAsync(
        ProcurementRequestContext context,
        CancellationToken cancellationToken = default)
    {
        var reportAuthorization = authorization.Authorize(context, "procurement.supplier-return.report.read");
        if (!reportAuthorization.Allowed)
        {
            return SupplierReturnOperationResult<SupplierReturnReportRecord>.Failure(reportAuthorization.Code);
        }

        var result = await ListAsync(context, null, null, cancellationToken);
        if (!result.Succeeded || result.Value is null)
        {
            return SupplierReturnOperationResult<SupplierReturnReportRecord>.Failure(result.Code);
        }

        var records = result.Value;
        var open = records.Where(item => item.Status is not (SupplierReturnStatus.Cancelled or SupplierReturnStatus.Rejected or SupplierReturnStatus.Reversed or SupplierReturnStatus.CorrectionLinked or SupplierReturnStatus.Completed)).ToArray();
        return SupplierReturnOperationResult<SupplierReturnReportRecord>.Success(new SupplierReturnReportRecord(
            records.Count,
            records.Sum(item => item.TotalReturnQuantity),
            open.Length,
            open.Sum(item => item.TotalReturnQuantity),
            open.Count(item => item.Status is SupplierReturnStatus.Approved or SupplierReturnStatus.AwaitingInventory),
            open.Count(item => item.Status is SupplierReturnStatus.InventoryHandoffRecorded or SupplierReturnStatus.AwaitingFinance),
            records));
    }

    public async Task<SupplierReturnOperationResult<SupplierReturnRecord>> CreateAsync(
        ProcurementRequestContext context,
        SupplierReturnCreateRequest request,
        string? idempotencyKey,
        string? requestFingerprint,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!SupplierReturnValuePolicy.TryNormalizeCreate(request, out var returnDate, out var reasonDetail, out var notes, out var lines, out var evidence))
        {
            return SupplierReturnOperationResult<SupplierReturnRecord>.Failure("validation_failed");
        }

        var replay = await ProbeReplayAsync(context, CreateOperationId, null, idempotencyKey, requestFingerprint, cancellationToken);
        if (replay.Outcome == SupplierReturnReplayOutcome.Conflict)
        {
            return SupplierReturnOperationResult<SupplierReturnRecord>.Failure("idempotency_conflict");
        }

        if (replay is { Outcome: SupplierReturnReplayOutcome.Replay, Record: { } replayed })
        {
            var replayAuthorized = authorization.Authorize(context, CreateOperationId, replayed.Scope);
            return replayAuthorized.Allowed
                ? SupplierReturnOperationResult<SupplierReturnRecord>.Success(replayed)
                : SupplierReturnOperationResult<SupplierReturnRecord>.Failure(replayAuthorized.Code);
        }

        SupplierReturnEligibleSourceRecord? source;
        try
        {
            source = await persistence.FindEligibleSourceAsync(context.TenantContext, request.GoodsReceiptId, cancellationToken);
        }
        catch
        {
            return SupplierReturnOperationResult<SupplierReturnRecord>.Failure("persistence_unavailable");
        }

        if (source is null)
        {
            return SupplierReturnOperationResult<SupplierReturnRecord>.Failure("supplier_return_source_not_eligible");
        }

        var authorized = authorization.Authorize(context, CreateOperationId, source.Scope);
        if (!authorized.Allowed)
        {
            return SupplierReturnOperationResult<SupplierReturnRecord>.Failure(authorized.Code);
        }

        var eligible = source.Lines.ToDictionary(item => item.GoodsReceiptLineId);
        var commandLines = new List<SupplierReturnLineCreateCommand>(lines.Count);
        foreach (var line in lines)
        {
            if (!eligible.TryGetValue(line.GoodsReceiptLineId, out var eligibleLine))
            {
                return SupplierReturnOperationResult<SupplierReturnRecord>.Failure("supplier_return_line_not_eligible");
            }

            if (line.ReturnQuantity > eligibleLine.EligibleReturnQuantity)
            {
                return SupplierReturnOperationResult<SupplierReturnRecord>.Failure("over_return_not_allowed");
            }

            commandLines.Add(new SupplierReturnLineCreateCommand(Guid.NewGuid(), line.GoodsReceiptLineId, line.ReturnQuantity, line.Notes));
        }

        var command = new SupplierReturnCreateCommand(
            Guid.NewGuid(),
            source.Scope,
            request.GoodsReceiptId,
            returnDate,
            request.ReasonCode,
            request.Condition,
            request.CommercialOutcome,
            reasonDetail,
            notes,
            commandLines,
            evidence.Select(item => new SupplierReturnEvidenceCreateCommand(
                Guid.NewGuid(),
                item.ReferenceId!,
                item.FileName,
                item.ContentType,
                item.Description,
                item.Source ?? "private-file-reference")).ToArray(),
            context.ActorId,
            DateTimeOffset.UtcNow,
            idempotencyKey);
        var audit = CreateEvidence(context, command.Id, source.Scope, CreateOperationId, null, SupplierReturnStatus.Draft, null, idempotencyKey, requestFingerprint, null, "Draft");
        return ToOperationResult(await persistence.CreateAsync(context.TenantContext, command, audit, cancellationToken));
    }

    public Task<SupplierReturnOperationResult<SupplierReturnRecord>> SubmitAsync(ProcurementRequestContext context, Guid id, byte[] version, string? reason, string? key, string? fingerprint, CancellationToken cancellationToken = default) => ActionAsync(context, id, version, SupplierReturnMutationAction.Submit, reason, key, fingerprint, null, null, null, null, null, cancellationToken);
    public Task<SupplierReturnOperationResult<SupplierReturnRecord>> ApproveAsync(ProcurementRequestContext context, Guid id, byte[] version, string? reason, string? key, string? fingerprint, CancellationToken cancellationToken = default) => ActionAsync(context, id, version, SupplierReturnMutationAction.Approve, reason, key, fingerprint, null, null, null, null, null, cancellationToken);
    public Task<SupplierReturnOperationResult<SupplierReturnRecord>> RejectAsync(ProcurementRequestContext context, Guid id, byte[] version, string? reason, string? key, string? fingerprint, CancellationToken cancellationToken = default) => ActionAsync(context, id, version, SupplierReturnMutationAction.Reject, reason, key, fingerprint, null, null, null, null, null, cancellationToken);
    public Task<SupplierReturnOperationResult<SupplierReturnRecord>> CancelAsync(ProcurementRequestContext context, Guid id, byte[] version, string? reason, string? key, string? fingerprint, CancellationToken cancellationToken = default) => ActionAsync(context, id, version, SupplierReturnMutationAction.Cancel, reason, key, fingerprint, null, null, null, null, null, cancellationToken);
    public Task<SupplierReturnOperationResult<SupplierReturnRecord>> ReverseAsync(ProcurementRequestContext context, Guid id, byte[] version, string? reason, string? key, string? fingerprint, CancellationToken cancellationToken = default) => ActionAsync(context, id, version, SupplierReturnMutationAction.Reverse, reason, key, fingerprint, null, null, null, null, null, cancellationToken);

    public async Task<SupplierReturnOperationResult<SupplierReturnRecord>> RecordInventoryHandoffAsync(ProcurementRequestContext context, Guid id, byte[] version, SupplierReturnInventoryHandoffRequest request, string? key, string? fingerprint, CancellationToken cancellationToken = default)
    {
        if (!SupplierReturnValuePolicy.TryNormalizeHandoff(request, out var reference, out var notes))
        {
            return SupplierReturnOperationResult<SupplierReturnRecord>.Failure("validation_failed");
        }

        return await ActionAsync(context, id, version, SupplierReturnMutationAction.RecordInventoryHandoff, notes, key, fingerprint, reference, null, null, null, notes, cancellationToken);
    }

    public async Task<SupplierReturnOperationResult<SupplierReturnRecord>> RecordFinanceReferenceAsync(ProcurementRequestContext context, Guid id, byte[] version, SupplierReturnFinanceReferenceRequest request, string? key, string? fingerprint, CancellationToken cancellationToken = default)
    {
        if (!SupplierReturnValuePolicy.TryNormalizeFinance(request, out var reference, out var currency, out var amount, out var notes))
        {
            return SupplierReturnOperationResult<SupplierReturnRecord>.Failure("validation_failed");
        }

        return await ActionAsync(context, id, version, SupplierReturnMutationAction.RecordFinanceReference, notes, key, fingerprint, null, reference, currency, amount, notes, cancellationToken);
    }

    public async Task<SupplierReturnOperationResult<SupplierReturnRecord>> CorrectAsync(ProcurementRequestContext context, Guid id, byte[] expectedVersion, SupplierReturnCreateRequest request, string? key, string? fingerprint, CancellationToken cancellationToken = default)
    {
        var current = await GetAuthorizedAsync(context, id, "procurement.supplier-return.correct", cancellationToken);
        if (!current.Succeeded || current.Value is null)
        {
            return current;
        }

        if (expectedVersion is null || expectedVersion.Length == 0)
        {
            return SupplierReturnOperationResult<SupplierReturnRecord>.Failure("concurrency_conflict");
        }

        if (!SupplierReturnValuePolicy.TryNormalizeCreate(request, out var returnDate, out var reasonDetail, out var notes, out var lines, out var evidence))
        {
            return SupplierReturnOperationResult<SupplierReturnRecord>.Failure("validation_failed");
        }

        var replay = await TryDurableReplayAsync(context, "procurement.supplier-return.correct", id, key, fingerprint, cancellationToken);
        if (replay is not null)
        {
            return replay;
        }

        if (request.GoodsReceiptId != current.Value.GoodsReceiptId)
        {
            return SupplierReturnOperationResult<SupplierReturnRecord>.Failure("correction_source_mismatch");
        }

        var command = new SupplierReturnCreateCommand(
            Guid.NewGuid(),
            current.Value.Scope,
            request.GoodsReceiptId,
            returnDate,
            request.ReasonCode,
            request.Condition,
            request.CommercialOutcome,
            reasonDetail,
            notes,
            lines.Select(item => new SupplierReturnLineCreateCommand(Guid.NewGuid(), item.GoodsReceiptLineId, item.ReturnQuantity, item.Notes)).ToArray(),
            evidence.Select(item => new SupplierReturnEvidenceCreateCommand(Guid.NewGuid(), item.ReferenceId!, item.FileName, item.ContentType, item.Description, item.Source ?? "private-file-reference")).ToArray(),
            context.ActorId,
            DateTimeOffset.UtcNow,
            key,
            id);
        var audit = CreateEvidence(context, id, current.Value.Scope, "procurement.supplier-return.correct", current.Value.Status, SupplierReturnStatus.CorrectionLinked, "Correction linked", key, fingerprint, current.Value.Status.ToString(), "CorrectionLinked");
        if (RequiresInventoryEffectVerification(current.Value.Status))
        {
            using var lease = await physicalEffectGate.AcquireAsync(id, cancellationToken);
            var verification = await VerifyInventoryEffectAsync(context, id, cancellationToken);
            if (verification is not null)
            {
                return verification;
            }

            return ToOperationResult(await persistence.CorrectAsync(context.TenantContext, command, expectedVersion, audit, cancellationToken));
        }

        return ToOperationResult(await persistence.CorrectAsync(context.TenantContext, command, expectedVersion, audit, cancellationToken));
    }

    public async Task<SupplierReturnOperationResult<IReadOnlyList<SupplierReturnHistoryRecord>>> ReadHistoryAsync(ProcurementRequestContext context, Guid id, CancellationToken cancellationToken = default)
    {
        var current = await GetAuthorizedAsync(context, id, "procurement.supplier-return.history.read", cancellationToken);
        if (!current.Succeeded || current.Value is null)
        {
            return SupplierReturnOperationResult<IReadOnlyList<SupplierReturnHistoryRecord>>.Failure(current.Code);
        }

        try
        {
            return SupplierReturnOperationResult<IReadOnlyList<SupplierReturnHistoryRecord>>.Success(await persistence.ReadHistoryAsync(context.TenantContext, id, cancellationToken));
        }
        catch
        {
            return SupplierReturnOperationResult<IReadOnlyList<SupplierReturnHistoryRecord>>.Failure("persistence_unavailable");
        }
    }

    public async Task<SupplierReturnOperationResult<IReadOnlyList<SupplierReturnAuditEvidence>>> ReadAuditAsync(ProcurementRequestContext context, Guid id, CancellationToken cancellationToken = default)
    {
        var current = await GetAuthorizedAsync(context, id, "procurement.supplier-return.audit.read", cancellationToken);
        if (!current.Succeeded || current.Value is null)
        {
            return SupplierReturnOperationResult<IReadOnlyList<SupplierReturnAuditEvidence>>.Failure(current.Code);
        }

        try
        {
            return SupplierReturnOperationResult<IReadOnlyList<SupplierReturnAuditEvidence>>.Success(await persistence.ReadAuditAsync(context.TenantContext, id, cancellationToken));
        }
        catch
        {
            return SupplierReturnOperationResult<IReadOnlyList<SupplierReturnAuditEvidence>>.Failure("persistence_unavailable");
        }
    }

    private async Task<SupplierReturnOperationResult<SupplierReturnRecord>> ActionAsync(
        ProcurementRequestContext context,
        Guid id,
        byte[] expectedVersion,
        SupplierReturnMutationAction action,
        string? reason,
        string? idempotencyKey,
        string? requestFingerprint,
        string? handoffReference,
        string? financeReference,
        string? financeCurrency,
        decimal? financeAmount,
        string? notes,
        CancellationToken cancellationToken)
    {
        var operationId = OperationId(action);
        var current = await GetAuthorizedAsync(context, id, operationId, cancellationToken);
        if (!current.Succeeded || current.Value is null)
        {
            return current;
        }

        if (expectedVersion is null || expectedVersion.Length == 0)
        {
            return SupplierReturnOperationResult<SupplierReturnRecord>.Failure("concurrency_conflict");
        }

        if (action is SupplierReturnMutationAction.Reject or SupplierReturnMutationAction.Cancel or SupplierReturnMutationAction.Reverse)
        {
            if (!SupplierReturnValuePolicy.TryReason(reason, out var normalizedReason))
            {
                return SupplierReturnOperationResult<SupplierReturnRecord>.Failure("reason_required");
            }

            reason = normalizedReason;
        }

        var replay = await TryDurableReplayAsync(context, operationId, id, idempotencyKey, requestFingerprint, cancellationToken);
        if (replay is not null)
        {
            return replay;
        }

        var afterStatus = action switch
        {
            SupplierReturnMutationAction.Submit => SupplierReturnStatus.Submitted,
            SupplierReturnMutationAction.Approve => SupplierReturnStatus.AwaitingInventory,
            SupplierReturnMutationAction.Reject => SupplierReturnStatus.Rejected,
            SupplierReturnMutationAction.Cancel => SupplierReturnStatus.Cancelled,
            SupplierReturnMutationAction.RecordInventoryHandoff => SupplierReturnStatus.AwaitingFinance,
            SupplierReturnMutationAction.RecordFinanceReference => SupplierReturnStatus.Completed,
            SupplierReturnMutationAction.Reverse => SupplierReturnStatus.Reversed,
            _ => current.Value.Status
        };
        var command = new SupplierReturnActionCommand(id, expectedVersion, action, context.ActorId, reason, DateTimeOffset.UtcNow, idempotencyKey, handoffReference, financeReference, financeCurrency, financeAmount, notes);
        var audit = CreateEvidence(context, id, current.Value.Scope, operationId, current.Value.Status, afterStatus, reason, idempotencyKey, requestFingerprint, current.Value.Status.ToString(), afterStatus.ToString());
        if (RequiresInventoryEffectVerification(action, current.Value.Status))
        {
            using var lease = await physicalEffectGate.AcquireAsync(id, cancellationToken);
            var verification = await VerifyInventoryEffectAsync(context, id, cancellationToken);
            if (verification is not null)
            {
                return verification;
            }

            return ToOperationResult(await persistence.ActionAsync(context.TenantContext, command, audit, cancellationToken));
        }

        return ToOperationResult(await persistence.ActionAsync(context.TenantContext, command, audit, cancellationToken));
    }

    private async Task<SupplierReturnOperationResult<SupplierReturnRecord>?> VerifyInventoryEffectAsync(
        ProcurementRequestContext context,
        Guid supplierReturnId,
        CancellationToken cancellationToken)
    {
        SupplierReturnInventoryEffectVerification verification;
        try
        {
            verification = await inventoryEffectReader.VerifyAsync(context.TenantContext, supplierReturnId, cancellationToken);
        }
        catch
        {
            return SupplierReturnOperationResult<SupplierReturnRecord>.Failure("inventory_effect_verification_unavailable");
        }

        return verification switch
        {
            SupplierReturnInventoryEffectVerification.ActiveEffectExists => SupplierReturnOperationResult<SupplierReturnRecord>.Failure("inventory_effect_exists"),
            SupplierReturnInventoryEffectVerification.NoActiveEffect => null,
            _ => SupplierReturnOperationResult<SupplierReturnRecord>.Failure("inventory_effect_verification_unavailable")
        };
    }

    private static bool RequiresInventoryEffectVerification(SupplierReturnMutationAction action, SupplierReturnStatus status) =>
        action is SupplierReturnMutationAction.Cancel or SupplierReturnMutationAction.Reverse
        && RequiresInventoryEffectVerification(status);

    private static bool RequiresInventoryEffectVerification(SupplierReturnStatus status) =>
        status is SupplierReturnStatus.Approved or SupplierReturnStatus.AwaitingInventory;

    private async Task<SupplierReturnOperationResult<SupplierReturnRecord>> GetAuthorizedAsync(ProcurementRequestContext context, Guid id, string operationId, CancellationToken cancellationToken)
    {
        if (id == Guid.Empty)
        {
            return SupplierReturnOperationResult<SupplierReturnRecord>.Failure("validation_failed");
        }

        SupplierReturnRecord? record;
        try
        {
            record = await persistence.FindAsync(context.TenantContext, id, cancellationToken);
        }
        catch
        {
            return SupplierReturnOperationResult<SupplierReturnRecord>.Failure("persistence_unavailable");
        }

        if (record is null)
        {
            return SupplierReturnOperationResult<SupplierReturnRecord>.Failure("supplier_return_not_found");
        }

        var authorized = authorization.Authorize(context, operationId, record.Scope);
        return authorized.Allowed
            ? SupplierReturnOperationResult<SupplierReturnRecord>.Success(record)
            : SupplierReturnOperationResult<SupplierReturnRecord>.Failure(authorized.Code);
    }

    private async Task<SupplierReturnReplayProbe> ProbeReplayAsync(ProcurementRequestContext context, string operationId, Guid? id, string? key, string? fingerprint, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return SupplierReturnReplayProbe.NotFound;
        }

        try
        {
            return await persistence.ProbeReplayAsync(context.TenantContext, new SupplierReturnReplayQuery(context.ActorId, operationId, key, id, ComputeRequestFingerprint(operationId, id ?? Guid.Empty, fingerprint)), cancellationToken);
        }
        catch
        {
            return SupplierReturnReplayProbe.NotFound;
        }
    }

    private async Task<SupplierReturnOperationResult<SupplierReturnRecord>?> TryDurableReplayAsync(ProcurementRequestContext context, string operationId, Guid id, string? key, string? fingerprint, CancellationToken cancellationToken)
    {
        var replay = await ProbeReplayAsync(context, operationId, id, key, fingerprint, cancellationToken);
        return replay.Outcome switch
        {
            SupplierReturnReplayOutcome.Replay when replay.Record is not null => SupplierReturnOperationResult<SupplierReturnRecord>.Success(replay.Record),
            SupplierReturnReplayOutcome.Conflict => SupplierReturnOperationResult<SupplierReturnRecord>.Failure("idempotency_conflict"),
            _ => null
        };
    }

    private static string OperationId(SupplierReturnMutationAction action) => action switch
    {
        SupplierReturnMutationAction.Submit => "procurement.supplier-return.submit",
        SupplierReturnMutationAction.Approve => "procurement.supplier-return.approve",
        SupplierReturnMutationAction.Reject => "procurement.supplier-return.reject",
        SupplierReturnMutationAction.Cancel => "procurement.supplier-return.cancel",
        SupplierReturnMutationAction.RecordInventoryHandoff => "procurement.supplier-return.inventory-handoff.record",
        SupplierReturnMutationAction.RecordFinanceReference => "procurement.supplier-return.finance-reference.record",
        SupplierReturnMutationAction.Reverse => "procurement.supplier-return.reverse",
        _ => throw new ArgumentOutOfRangeException(nameof(action))
    };

    private static SupplierReturnAuditEvidence CreateEvidence(ProcurementRequestContext context, Guid id, PurchaseRequestScope scope, string operationId, SupplierReturnStatus? before, SupplierReturnStatus? after, string? reason, string? key, string? rawFingerprint, string? beforeSummary, string? afterSummary) => new(
        Guid.NewGuid(),
        id,
        DateTimeOffset.UtcNow,
        operationId,
        context.CorrelationId?.Value ?? Guid.NewGuid().ToString("N"),
        context.TenantId.Value,
        context.ActorId,
        context.SessionId,
        context.AuthorizationPath.ToString(),
        "Allowed",
        reason,
        before,
        after,
        scope.CompanyId,
        scope.BranchId,
        beforeSummary,
        afterSummary,
        key,
        ComputeRequestFingerprint(operationId, id, rawFingerprint));

    private static string? ComputeRequestFingerprint(string operationId, Guid id, string? rawFingerprint)
    {
        if (string.IsNullOrWhiteSpace(rawFingerprint))
        {
            return null;
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes($"{operationId}|{id:D}|{rawFingerprint}")));
    }

    private static SupplierReturnOperationResult<T> ToOperationResult<T>(SupplierReturnPersistenceResult<T> result) =>
        result.Succeeded && result.Value is not null
            ? SupplierReturnOperationResult<T>.Success(result.Value)
            : SupplierReturnOperationResult<T>.Failure(result.Code);
}

public sealed record SupplierReturnReportRecord(
    int ReturnCount,
    decimal TotalReturnQuantity,
    int OpenReturnCount,
    decimal OpenReturnQuantity,
    int PendingInventoryCount,
    int PendingFinanceCount,
    IReadOnlyList<SupplierReturnListRecord> Returns);

#pragma warning restore CS1591
