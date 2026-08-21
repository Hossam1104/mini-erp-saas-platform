#pragma warning disable CS1591

using System.Security.Cryptography;
using System.Text;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.Contracts.Modules.Procurement;

namespace MiniErp.App.Modules.Procurement;

/// <summary>
/// Records a non-posted commercial handoff from one or more Goods Receipts to Finance/AP for a given
/// Purchase Order, preserving full Goods-Receipt-line lineage for the future three-way-matching engine
/// (MESP-126). This service never creates AP/supplier liability, GL entries, tax posting, or a Posted
/// Purchase Invoice — it is bookkeeping-adjacent evidence only.
/// </summary>
public sealed class PurchaseInvoiceHandoffService
{
    private const string CreateOperationId = "procurement.invoice-handoff.create";
    private const string CancelOperationId = "procurement.invoice-handoff.cancel";
    private const string EvidenceCaptureOperationId = "procurement.invoice-handoff.evidence.capture";
    private readonly PurchaseRequestAuthorizationService authorization;
    private readonly IPurchaseInvoiceHandoffPersistence persistence;

    public PurchaseInvoiceHandoffService(PurchaseRequestAuthorizationService authorization, IPurchaseInvoiceHandoffPersistence persistence)
    {
        this.authorization = authorization ?? throw new ArgumentNullException(nameof(authorization));
        this.persistence = persistence ?? throw new ArgumentNullException(nameof(persistence));
    }

    public async Task<PurchaseInvoiceHandoffOperationResult<IReadOnlyList<PurchaseInvoiceHandoffListRecord>>> ListAsync(
        ProcurementRequestContext context,
        PurchaseInvoiceHandoffStatus? status,
        CancellationToken cancellationToken = default)
    {
        var authorized = authorization.Authorize(context, "procurement.invoice-handoff.list");
        if (!authorized.Allowed)
        {
            return PurchaseInvoiceHandoffOperationResult<IReadOnlyList<PurchaseInvoiceHandoffListRecord>>.Failure(authorized.Code);
        }

        try
        {
            return PurchaseInvoiceHandoffOperationResult<IReadOnlyList<PurchaseInvoiceHandoffListRecord>>.Success(
                await persistence.ListAsync(context.TenantContext, status, cancellationToken));
        }
        catch
        {
            return PurchaseInvoiceHandoffOperationResult<IReadOnlyList<PurchaseInvoiceHandoffListRecord>>.Failure("persistence_unavailable");
        }
    }

    public async Task<PurchaseInvoiceHandoffOperationResult<IReadOnlyList<PurchaseInvoiceHandoffEligibleSourceRecord>>> ListEligibleSourcesAsync(
        ProcurementRequestContext context,
        CancellationToken cancellationToken = default)
    {
        const string operationId = "procurement.invoice-handoff.eligible-source.list";
        var authorized = authorization.Authorize(context, operationId);
        if (!authorized.Allowed)
        {
            return PurchaseInvoiceHandoffOperationResult<IReadOnlyList<PurchaseInvoiceHandoffEligibleSourceRecord>>.Failure(authorized.Code);
        }

        try
        {
            var sources = await persistence.ListEligibleSourcesAsync(context.TenantContext, cancellationToken);
            var permitted = sources
                .Where(item => authorization.Authorize(context, operationId, item.Scope).Allowed && item.Lines.Any(line => line.RemainingHandoffQuantity > 0))
                .OrderBy(item => item.SupplierName, StringComparer.Ordinal)
                .ToArray();
            return PurchaseInvoiceHandoffOperationResult<IReadOnlyList<PurchaseInvoiceHandoffEligibleSourceRecord>>.Success(permitted);
        }
        catch
        {
            return PurchaseInvoiceHandoffOperationResult<IReadOnlyList<PurchaseInvoiceHandoffEligibleSourceRecord>>.Failure("persistence_unavailable");
        }
    }

    public async Task<PurchaseInvoiceHandoffOperationResult<PurchaseInvoiceHandoffRecord>> GetAsync(
        ProcurementRequestContext context,
        Guid purchaseInvoiceHandoffId,
        CancellationToken cancellationToken = default) =>
        await GetAuthorizedAsync(context, purchaseInvoiceHandoffId, "procurement.invoice-handoff.read", cancellationToken);

    public async Task<PurchaseInvoiceHandoffOperationResult<PurchaseInvoiceHandoffRecord>> CreateAsync(
        ProcurementRequestContext context,
        PurchaseInvoiceHandoffCreateRequest request,
        string? idempotencyKey,
        string? requestFingerprint,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!PurchaseInvoiceHandoffValuePolicy.TryNormalizeCreate(request, out var supplierInvoiceReference, out var notes, out var sources)
            || !PurchaseInvoiceHandoffValuePolicy.TryNormalizeDeclaredEvidence(request.DeclaredEvidence, out var declaredEvidence))
        {
            return PurchaseInvoiceHandoffOperationResult<PurchaseInvoiceHandoffRecord>.Failure("validation_failed");
        }

        // Creation has no pre-existing target to authorize against, so the durable replay is authorized
        // against the scope of the Purchase Invoice Handoff the original request actually created,
        // mirroring the established Purchase Order/Goods Receipt create-replay pattern.
        var replayProbe = await ProbeReplayAsync(context, CreateOperationId, null, idempotencyKey, requestFingerprint, cancellationToken);
        if (replayProbe.Outcome == PurchaseInvoiceHandoffReplayOutcome.Conflict)
        {
            return PurchaseInvoiceHandoffOperationResult<PurchaseInvoiceHandoffRecord>.Failure("idempotency_conflict");
        }

        if (replayProbe is { Outcome: PurchaseInvoiceHandoffReplayOutcome.Replay, Record: { } replayed })
        {
            var replayAuthorized = authorization.Authorize(context, CreateOperationId, replayed.Scope);
            return replayAuthorized.Allowed
                ? PurchaseInvoiceHandoffOperationResult<PurchaseInvoiceHandoffRecord>.Success(replayed)
                : PurchaseInvoiceHandoffOperationResult<PurchaseInvoiceHandoffRecord>.Failure(replayAuthorized.Code);
        }

        PurchaseInvoiceHandoffEligibleSourceRecord? source;
        try
        {
            source = await persistence.FindEligibleSourceAsync(context.TenantContext, request.PurchaseOrderId, cancellationToken);
        }
        catch
        {
            return PurchaseInvoiceHandoffOperationResult<PurchaseInvoiceHandoffRecord>.Failure("persistence_unavailable");
        }

        if (source is null)
        {
            return PurchaseInvoiceHandoffOperationResult<PurchaseInvoiceHandoffRecord>.Failure("invoice_handoff_source_not_eligible");
        }

        var authorized = authorization.Authorize(context, CreateOperationId, source.Scope);
        if (!authorized.Allowed)
        {
            return PurchaseInvoiceHandoffOperationResult<PurchaseInvoiceHandoffRecord>.Failure(authorized.Code);
        }

        var eligibleLines = source.Lines.ToDictionary(item => (item.GoodsReceiptId, item.GoodsReceiptLineId));
        var commandSources = new List<PurchaseInvoiceHandoffSourceCommand>(sources.Count);
        foreach (var requested in sources)
        {
            if (!eligibleLines.TryGetValue((requested.GoodsReceiptId, requested.GoodsReceiptLineId), out var eligibleLine))
            {
                return PurchaseInvoiceHandoffOperationResult<PurchaseInvoiceHandoffRecord>.Failure("invoice_handoff_line_not_eligible");
            }

            if (requested.Quantity > eligibleLine.RemainingHandoffQuantity)
            {
                return PurchaseInvoiceHandoffOperationResult<PurchaseInvoiceHandoffRecord>.Failure("over_handoff_not_allowed");
            }

            commandSources.Add(new PurchaseInvoiceHandoffSourceCommand(requested.GoodsReceiptId, requested.GoodsReceiptLineId, requested.Quantity));
        }

        var id = Guid.NewGuid();
        var occurredAt = DateTimeOffset.UtcNow;
        var command = new PurchaseInvoiceHandoffCreateCommand(
            id,
            source.Scope,
            request.PurchaseOrderId,
            context.ActorId,
            supplierInvoiceReference,
            request.SupplierInvoiceDate,
            notes,
            commandSources,
            occurredAt,
            idempotencyKey,
            declaredEvidence);
        var evidence = CreateEvidence(context, id, source.Scope, CreateOperationId, null, PurchaseInvoiceHandoffStatus.Recorded, null, idempotencyKey, requestFingerprint, null, "Recorded");
        return ToOperationResult(await persistence.CreateAsync(context.TenantContext, command, evidence, cancellationToken));
    }

    public async Task<PurchaseInvoiceHandoffOperationResult<PurchaseInvoiceHandoffRecord>> CaptureDeclaredEvidenceAsync(
        ProcurementRequestContext context,
        Guid purchaseInvoiceHandoffId,
        byte[] expectedVersion,
        PurchaseInvoiceDeclaredEvidenceCaptureRequest request,
        string? idempotencyKey,
        string? requestFingerprint,
        CancellationToken cancellationToken = default)
    {
        var current = await GetAuthorizedAsync(context, purchaseInvoiceHandoffId, EvidenceCaptureOperationId, cancellationToken);
        if (!current.Succeeded || current.Value is null) return current;
        if (expectedVersion is null || expectedVersion.Length == 0) return PurchaseInvoiceHandoffOperationResult<PurchaseInvoiceHandoffRecord>.Failure("validation_failed");
        if (request is null || !PurchaseInvoiceHandoffValuePolicy.TryNormalizeDeclaredEvidence(request.Evidence, out var normalized) || normalized is null)
        {
            return PurchaseInvoiceHandoffOperationResult<PurchaseInvoiceHandoffRecord>.Failure("invoice_evidence_invalid");
        }

        var replay = await TryDurableReplayAsync(context, EvidenceCaptureOperationId, purchaseInvoiceHandoffId, idempotencyKey, requestFingerprint, cancellationToken);
        if (replay is not null) return replay;
        if (current.Value.Status != PurchaseInvoiceHandoffStatus.Recorded) return PurchaseInvoiceHandoffOperationResult<PurchaseInvoiceHandoffRecord>.Failure("invoice_handoff_not_active");
        if (current.Value.DeclaredEvidence is not null && !PurchaseInvoiceHandoffValuePolicy.TryReason(request.Reason, out var reason))
        {
            return PurchaseInvoiceHandoffOperationResult<PurchaseInvoiceHandoffRecord>.Failure("evidence_correction_reason_required");
        }

        var command = new PurchaseInvoiceDeclaredEvidenceCaptureCommand(
            purchaseInvoiceHandoffId,
            expectedVersion,
            context.ActorId,
            normalized,
            string.IsNullOrWhiteSpace(request.Reason) ? null : request.Reason.Trim(),
            DateTimeOffset.UtcNow,
            idempotencyKey);
        var evidence = CreateEvidence(context, purchaseInvoiceHandoffId, current.Value.Scope, EvidenceCaptureOperationId, current.Value.Status, current.Value.Status, command.Reason, idempotencyKey, requestFingerprint, $"declared-evidence:{current.Value.DeclaredEvidence?.VersionNumber.ToString() ?? "none"}", "declared-evidence:captured");
        return ToOperationResult(await persistence.CaptureDeclaredEvidenceAsync(context.TenantContext, command, evidence, cancellationToken));
    }

    public async Task<PurchaseInvoiceHandoffOperationResult<PurchaseInvoiceHandoffRecord>> CancelAsync(
        ProcurementRequestContext context,
        Guid purchaseInvoiceHandoffId,
        byte[] expectedVersion,
        string? reason,
        string? idempotencyKey,
        string? requestFingerprint,
        CancellationToken cancellationToken = default)
    {
        var current = await GetAuthorizedAsync(context, purchaseInvoiceHandoffId, CancelOperationId, cancellationToken);
        if (!current.Succeeded || current.Value is null)
        {
            return current;
        }

        if (expectedVersion is null || expectedVersion.Length == 0)
        {
            return PurchaseInvoiceHandoffOperationResult<PurchaseInvoiceHandoffRecord>.Failure("validation_failed");
        }

        if (!PurchaseInvoiceHandoffValuePolicy.TryReason(reason, out var normalizedReason))
        {
            return PurchaseInvoiceHandoffOperationResult<PurchaseInvoiceHandoffRecord>.Failure("reason_required");
        }

        var replay = await TryDurableReplayAsync(context, CancelOperationId, purchaseInvoiceHandoffId, idempotencyKey, requestFingerprint, cancellationToken);
        if (replay is not null)
        {
            return replay;
        }

        if (current.Value.Status != PurchaseInvoiceHandoffStatus.Recorded)
        {
            return PurchaseInvoiceHandoffOperationResult<PurchaseInvoiceHandoffRecord>.Failure("cancel_not_allowed");
        }

        var command = new PurchaseInvoiceHandoffActionCommand(purchaseInvoiceHandoffId, expectedVersion, context.ActorId, normalizedReason, DateTimeOffset.UtcNow, idempotencyKey);
        var evidence = CreateEvidence(context, purchaseInvoiceHandoffId, current.Value.Scope, CancelOperationId, current.Value.Status, PurchaseInvoiceHandoffStatus.Cancelled, normalizedReason, idempotencyKey, requestFingerprint, current.Value.Status.ToString(), "Cancelled");
        return ToOperationResult(await persistence.CancelAsync(context.TenantContext, command, evidence, cancellationToken));
    }

    public async Task<PurchaseInvoiceHandoffOperationResult<IReadOnlyList<PurchaseInvoiceHandoffHistoryRecord>>> ReadHistoryAsync(ProcurementRequestContext context, Guid purchaseInvoiceHandoffId, CancellationToken cancellationToken = default)
    {
        var current = await GetAuthorizedAsync(context, purchaseInvoiceHandoffId, "procurement.invoice-handoff.history.read", cancellationToken);
        if (!current.Succeeded || current.Value is null)
        {
            return PurchaseInvoiceHandoffOperationResult<IReadOnlyList<PurchaseInvoiceHandoffHistoryRecord>>.Failure(current.Code);
        }

        try
        {
            return PurchaseInvoiceHandoffOperationResult<IReadOnlyList<PurchaseInvoiceHandoffHistoryRecord>>.Success(await persistence.ReadHistoryAsync(context.TenantContext, purchaseInvoiceHandoffId, cancellationToken));
        }
        catch
        {
            return PurchaseInvoiceHandoffOperationResult<IReadOnlyList<PurchaseInvoiceHandoffHistoryRecord>>.Failure("persistence_unavailable");
        }
    }

    public async Task<PurchaseInvoiceHandoffOperationResult<IReadOnlyList<PurchaseInvoiceHandoffAuditRecord>>> ReadAuditAsync(ProcurementRequestContext context, Guid purchaseInvoiceHandoffId, CancellationToken cancellationToken = default)
    {
        var current = await GetAuthorizedAsync(context, purchaseInvoiceHandoffId, "procurement.invoice-handoff.audit.read", cancellationToken);
        if (!current.Succeeded || current.Value is null)
        {
            return PurchaseInvoiceHandoffOperationResult<IReadOnlyList<PurchaseInvoiceHandoffAuditRecord>>.Failure(current.Code);
        }

        try
        {
            return PurchaseInvoiceHandoffOperationResult<IReadOnlyList<PurchaseInvoiceHandoffAuditRecord>>.Success(await persistence.ReadAuditAsync(context.TenantContext, purchaseInvoiceHandoffId, cancellationToken));
        }
        catch
        {
            return PurchaseInvoiceHandoffOperationResult<IReadOnlyList<PurchaseInvoiceHandoffAuditRecord>>.Failure("persistence_unavailable");
        }
    }

    private async Task<PurchaseInvoiceHandoffOperationResult<PurchaseInvoiceHandoffRecord>> GetAuthorizedAsync(ProcurementRequestContext context, Guid purchaseInvoiceHandoffId, string operationId, CancellationToken cancellationToken)
    {
        var current = await FindAsync(context, purchaseInvoiceHandoffId, cancellationToken);
        if (!current.Succeeded || current.Value is null)
        {
            return current;
        }

        var authorized = authorization.Authorize(context, operationId, current.Value.Scope);
        return authorized.Allowed ? current : PurchaseInvoiceHandoffOperationResult<PurchaseInvoiceHandoffRecord>.Failure(authorized.Code);
    }

    private async Task<PurchaseInvoiceHandoffOperationResult<PurchaseInvoiceHandoffRecord>> FindAsync(ProcurementRequestContext context, Guid purchaseInvoiceHandoffId, CancellationToken cancellationToken)
    {
        if (purchaseInvoiceHandoffId == Guid.Empty)
        {
            return PurchaseInvoiceHandoffOperationResult<PurchaseInvoiceHandoffRecord>.Failure("validation_failed");
        }

        try
        {
            var record = await persistence.FindAsync(context.TenantContext, purchaseInvoiceHandoffId, cancellationToken);
            return record is null
                ? PurchaseInvoiceHandoffOperationResult<PurchaseInvoiceHandoffRecord>.Failure("invoice_handoff_not_found")
                : PurchaseInvoiceHandoffOperationResult<PurchaseInvoiceHandoffRecord>.Success(record);
        }
        catch
        {
            return PurchaseInvoiceHandoffOperationResult<PurchaseInvoiceHandoffRecord>.Failure("persistence_unavailable");
        }
    }

    private static PurchaseInvoiceHandoffAuditEvidence CreateEvidence(ProcurementRequestContext context, Guid purchaseInvoiceHandoffId, PurchaseRequestScope scope, string operationId, PurchaseInvoiceHandoffStatus? beforeStatus, PurchaseInvoiceHandoffStatus? afterStatus, string? reason, string? idempotencyKey, string? requestFingerprint, string? beforeSummary, string? afterSummary) => new(
        Guid.NewGuid(),
        purchaseInvoiceHandoffId,
        DateTimeOffset.UtcNow,
        operationId,
        context.CorrelationId?.Value ?? Guid.NewGuid().ToString("N"),
        context.TenantId.Value,
        context.ActorId,
        context.SessionId,
        context.AuthorizationPath.ToString(),
        "Allowed",
        reason,
        beforeStatus,
        afterStatus,
        scope.CompanyId,
        scope.BranchId,
        beforeSummary,
        afterSummary,
        idempotencyKey,
        ComputeRequestFingerprint(operationId, purchaseInvoiceHandoffId, requestFingerprint));

    private static string? ComputeRequestFingerprint(string operationId, Guid purchaseInvoiceHandoffId, string? rawFingerprint)
    {
        if (string.IsNullOrWhiteSpace(rawFingerprint))
        {
            return null;
        }

        var targetDiscriminator = operationId == CreateOperationId ? "new" : purchaseInvoiceHandoffId.ToString("D");
        var canonical = $"{operationId}|{targetDiscriminator}|{rawFingerprint}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    /// <summary>
    /// Consults durable persisted idempotency evidence after the trusted Tenant context, the current
    /// target, and the caller's authority over that target have already been established, but before any
    /// lifecycle/state check that the original successful request itself would have changed.
    /// </summary>
    private async Task<PurchaseInvoiceHandoffOperationResult<PurchaseInvoiceHandoffRecord>?> TryDurableReplayAsync(
        ProcurementRequestContext context,
        string operationId,
        Guid purchaseInvoiceHandoffId,
        string? idempotencyKey,
        string? requestFingerprint,
        CancellationToken cancellationToken)
    {
        var probe = await ProbeReplayAsync(context, operationId, purchaseInvoiceHandoffId, idempotencyKey, requestFingerprint, cancellationToken);
        return probe.Outcome switch
        {
            PurchaseInvoiceHandoffReplayOutcome.Replay when probe.Record is not null => PurchaseInvoiceHandoffOperationResult<PurchaseInvoiceHandoffRecord>.Success(probe.Record),
            PurchaseInvoiceHandoffReplayOutcome.Conflict => PurchaseInvoiceHandoffOperationResult<PurchaseInvoiceHandoffRecord>.Failure("idempotency_conflict"),
            _ => null
        };
    }

    private async Task<PurchaseInvoiceHandoffReplayProbe> ProbeReplayAsync(
        ProcurementRequestContext context,
        string operationId,
        Guid? purchaseInvoiceHandoffId,
        string? idempotencyKey,
        string? requestFingerprint,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return PurchaseInvoiceHandoffReplayProbe.NotFound;
        }

        var query = new PurchaseInvoiceHandoffReplayQuery(
            context.ActorId,
            operationId,
            idempotencyKey,
            purchaseInvoiceHandoffId,
            ComputeRequestFingerprint(operationId, purchaseInvoiceHandoffId ?? Guid.Empty, requestFingerprint));
        try
        {
            return await persistence.ProbeReplayAsync(context.TenantContext, query, cancellationToken);
        }
        catch
        {
            // A probe failure must never approve a mutation on its own; fall through to the normal path,
            // where the persistence-side replay check still runs inside the mutation transaction.
            return PurchaseInvoiceHandoffReplayProbe.NotFound;
        }
    }

    private static PurchaseInvoiceHandoffOperationResult<T> ToOperationResult<T>(PurchaseInvoiceHandoffPersistenceResult<T> result) =>
        result.Succeeded && result.Value is not null
            ? PurchaseInvoiceHandoffOperationResult<T>.Success(result.Value)
            : PurchaseInvoiceHandoffOperationResult<T>.Failure(result.Code);
}

#pragma warning restore CS1591
