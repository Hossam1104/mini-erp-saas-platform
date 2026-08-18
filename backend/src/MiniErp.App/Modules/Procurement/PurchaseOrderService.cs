#pragma warning disable CS1591

using System.Security.Cryptography;
using System.Text;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.Contracts.Modules.MasterData;
using MiniErp.Contracts.Modules.Procurement;

namespace MiniErp.App.Modules.Procurement;

public sealed class PurchaseOrderService
{
    private const string CreateOperationId = "procurement.purchase-order.create";
    private readonly PurchaseRequestAuthorizationService authorization;
    private readonly IPurchaseOrderPersistence persistence;
    private readonly IPurchaseRequestPersistence purchaseRequests;
    private readonly ISupplierQuotationPersistence quotations;
    private readonly IPurchaseRequestApprovalPolicyProvider policyProvider;
    private readonly IPurchaseRequestApprovalDelegationProvider delegationProvider;

    public PurchaseOrderService(
        PurchaseRequestAuthorizationService authorization,
        IPurchaseOrderPersistence persistence,
        IPurchaseRequestPersistence purchaseRequests,
        ISupplierQuotationPersistence quotations,
        IPurchaseRequestApprovalPolicyProvider policyProvider,
        IPurchaseRequestApprovalDelegationProvider delegationProvider)
    {
        this.authorization = authorization ?? throw new ArgumentNullException(nameof(authorization));
        this.persistence = persistence ?? throw new ArgumentNullException(nameof(persistence));
        this.purchaseRequests = purchaseRequests ?? throw new ArgumentNullException(nameof(purchaseRequests));
        this.quotations = quotations ?? throw new ArgumentNullException(nameof(quotations));
        this.policyProvider = policyProvider ?? throw new ArgumentNullException(nameof(policyProvider));
        this.delegationProvider = delegationProvider ?? throw new ArgumentNullException(nameof(delegationProvider));
    }

    public async Task<PurchaseOrderOperationResult<IReadOnlyList<PurchaseOrderListRecord>>> ListAsync(
        ProcurementRequestContext context,
        PurchaseOrderStatus? status,
        CancellationToken cancellationToken = default)
    {
        var authorized = authorization.Authorize(context, "procurement.purchase-order.list");
        if (!authorized.Allowed)
        {
            return PurchaseOrderOperationResult<IReadOnlyList<PurchaseOrderListRecord>>.Failure(authorized.Code);
        }

        try
        {
            return PurchaseOrderOperationResult<IReadOnlyList<PurchaseOrderListRecord>>.Success(
                await persistence.ListAsync(context.TenantContext, status, cancellationToken));
        }
        catch
        {
            return PurchaseOrderOperationResult<IReadOnlyList<PurchaseOrderListRecord>>.Failure("persistence_unavailable");
        }
    }

    public async Task<PurchaseOrderOperationResult<IReadOnlyList<PurchaseOrderSourceOptionRecord>>> ListSourceOptionsAsync(
        ProcurementRequestContext context,
        CancellationToken cancellationToken = default)
    {
        var authorized = authorization.Authorize(context, "procurement.purchase-order.source.list");
        if (!authorized.Allowed)
        {
            return PurchaseOrderOperationResult<IReadOnlyList<PurchaseOrderSourceOptionRecord>>.Failure(authorized.Code);
        }

        try
        {
            var requests = await purchaseRequests.ListAsync(context.TenantContext, PurchaseRequestStatus.Approved, cancellationToken);
            var options = new List<PurchaseOrderSourceOptionRecord>();
            foreach (var requestSummary in requests)
            {
                var request = await purchaseRequests.FindAsync(context.TenantContext, requestSummary.Id, cancellationToken);
                if (request is null)
                {
                    continue;
                }

                var sourceDecision = await quotations.FindSourceDecisionAsync(context.TenantContext, request.Id, cancellationToken);
                if (sourceDecision is null || !authorization.Authorize(context, "procurement.purchase-order.source.list", sourceDecision.Scope).Allowed)
                {
                    continue;
                }

                var quotation = await quotations.FindAsync(context.TenantContext, sourceDecision.SelectedQuotationId, cancellationToken);
                if (quotation is null || quotation.Status != SupplierQuotationStatus.Submitted)
                {
                    continue;
                }

                var sourceLines = BuildSourceLines(request, quotation);
                if (sourceLines.Count == 0)
                {
                    continue;
                }

                options.Add(new PurchaseOrderSourceOptionRecord(
                    ToSourceResponse(request, sourceDecision, quotation),
                    request.Scope,
                    sourceLines,
                    request.Version));
            }

            return PurchaseOrderOperationResult<IReadOnlyList<PurchaseOrderSourceOptionRecord>>.Success(
                options.OrderBy(item => item.Source.Supplier.Name, StringComparer.Ordinal)
                    .ThenBy(item => item.Source.PurchaseRequestReference, StringComparer.Ordinal)
                    .ThenBy(item => item.Source.SupplierQuotationReference, StringComparer.Ordinal)
                    .ToArray());
        }
        catch
        {
            return PurchaseOrderOperationResult<IReadOnlyList<PurchaseOrderSourceOptionRecord>>.Failure("persistence_unavailable");
        }
    }

    public async Task<PurchaseOrderOperationResult<PurchaseOrderRecord>> GetAsync(
        ProcurementRequestContext context,
        Guid purchaseOrderId,
        CancellationToken cancellationToken = default)
    {
        var current = await FindAsync(context, purchaseOrderId, cancellationToken);
        if (!current.Succeeded || current.Value is null)
        {
            return current;
        }

        var authorized = authorization.Authorize(context, "procurement.purchase-order.read", current.Value.Scope);
        return authorized.Allowed
            ? current
            : PurchaseOrderOperationResult<PurchaseOrderRecord>.Failure(authorized.Code);
    }

    public async Task<PurchaseOrderOperationResult<PurchaseOrderRecord>> CreateAsync(
        ProcurementRequestContext context,
        PurchaseOrderCreateRequest request,
        string? idempotencyKey,
        string? requestFingerprint,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.SourceDecisionId == Guid.Empty)
        {
            return PurchaseOrderOperationResult<PurchaseOrderRecord>.Failure("validation_failed");
        }

        // Creation has no pre-existing target to authorize against, so the durable replay is authorized
        // against the scope of the Purchase Order the original request actually created. That keeps an
        // identical retry replayable after the mutable sourcing state has moved on, without weakening
        // source validation for a genuinely new create.
        var replayProbe = await ProbeReplayAsync(context, CreateOperationId, null, idempotencyKey, requestFingerprint, cancellationToken);
        if (replayProbe.Outcome == PurchaseOrderReplayOutcome.Conflict)
        {
            return PurchaseOrderOperationResult<PurchaseOrderRecord>.Failure("idempotency_conflict");
        }

        if (replayProbe is { Outcome: PurchaseOrderReplayOutcome.Replay, Record: { } replayed })
        {
            var replayAuthorized = authorization.Authorize(context, CreateOperationId, replayed.Scope);
            return replayAuthorized.Allowed
                ? PurchaseOrderOperationResult<PurchaseOrderRecord>.Success(replayed)
                : PurchaseOrderOperationResult<PurchaseOrderRecord>.Failure(replayAuthorized.Code);
        }

        var source = await FindSourceOptionAsync(context, request.SourceDecisionId, cancellationToken);
        if (!source.Succeeded || source.Value is null)
        {
            return PurchaseOrderOperationResult<PurchaseOrderRecord>.Failure(source.Code);
        }

        var authorized = authorization.Authorize(context, CreateOperationId, source.Value.Scope);
        if (!authorized.Allowed)
        {
            return PurchaseOrderOperationResult<PurchaseOrderRecord>.Failure(authorized.Code);
        }

        var id = Guid.NewGuid();
        var occurredAt = DateTimeOffset.UtcNow;
        var command = new PurchaseOrderCreateCommand(
            id,
            source.Value.Scope,
            context.ActorId,
            source.Value.Source,
            source.Value.Lines,
            occurredAt,
            idempotencyKey);
        var evidence = CreateEvidence(context, id, source.Value.Scope, CreateOperationId, null, PurchaseOrderStatus.Draft, null, idempotencyKey, requestFingerprint, null, "Draft");
        return ToOperationResult(await persistence.CreateAsync(context.TenantContext, command, evidence, cancellationToken));
    }

    public async Task<PurchaseOrderOperationResult<PurchaseOrderRecord>> EditAsync(
        ProcurementRequestContext context,
        Guid purchaseOrderId,
        PurchaseOrderEditRequest request,
        byte[] expectedVersion,
        string? idempotencyKey,
        string? requestFingerprint,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (expectedVersion is null || expectedVersion.Length == 0
            || !PurchaseOrderValuePolicy.TryNormalizeEdit(request, out var notes, out var lines))
        {
            return PurchaseOrderOperationResult<PurchaseOrderRecord>.Failure("validation_failed");
        }

        var current = await GetAuthorizedAsync(context, purchaseOrderId, "procurement.purchase-order.edit", cancellationToken);
        if (!current.Succeeded || current.Value is null)
        {
            return current;
        }

        if (current.Value.CreatedByActorId != context.ActorId)
        {
            return PurchaseOrderOperationResult<PurchaseOrderRecord>.Failure("creator_only");
        }

        var replay = await TryDurableReplayAsync(context, "procurement.purchase-order.edit", purchaseOrderId, idempotencyKey, requestFingerprint, cancellationToken);
        if (replay is not null)
        {
            return replay;
        }

        var command = new PurchaseOrderEditCommand(
            purchaseOrderId,
            notes,
            lines,
            expectedVersion,
            DateTimeOffset.UtcNow,
            idempotencyKey);
        var evidence = CreateEvidence(context, purchaseOrderId, current.Value.Scope, "procurement.purchase-order.edit", current.Value.Status, current.Value.Status, null, idempotencyKey, requestFingerprint, $"{current.Value.Status};lines={current.Value.Lines.Count}", $"{current.Value.Status};lines={lines.Count}");
        return ToOperationResult(await persistence.EditAsync(context.TenantContext, command, evidence, cancellationToken));
    }

    public async Task<PurchaseOrderOperationResult<PurchaseOrderRecord>> SubmitAsync(
        ProcurementRequestContext context,
        Guid purchaseOrderId,
        byte[] expectedVersion,
        string? idempotencyKey,
        string? requestFingerprint,
        CancellationToken cancellationToken = default)
    {
        var current = await GetAuthorizedAsync(context, purchaseOrderId, "procurement.purchase-order.submit", cancellationToken);
        if (!current.Succeeded || current.Value is null)
        {
            return current;
        }

        if (expectedVersion is null || expectedVersion.Length == 0 || current.Value.CreatedByActorId != context.ActorId)
        {
            return PurchaseOrderOperationResult<PurchaseOrderRecord>.Failure(expectedVersion is null or { Length: 0 } ? "validation_failed" : "creator_only");
        }

        var replay = await TryDurableReplayAsync(context, "procurement.purchase-order.submit", purchaseOrderId, idempotencyKey, requestFingerprint, cancellationToken);
        if (replay is not null)
        {
            return replay;
        }

        if (current.Value.Status is not (PurchaseOrderStatus.Draft or PurchaseOrderStatus.ReturnedForChange))
        {
            return PurchaseOrderOperationResult<PurchaseOrderRecord>.Failure("submit_not_allowed");
        }

        var policy = await policyProvider.ResolveAsync(context, current.Value.Scope, DateTimeOffset.UtcNow, cancellationToken);
        if (!PurchaseRequestValuePolicy.IsValidPolicy(policy))
        {
            return PurchaseOrderOperationResult<PurchaseOrderRecord>.Failure("approval_policy_not_configured");
        }

        var command = new PurchaseOrderSubmitCommand(purchaseOrderId, expectedVersion, policy!, false, DateTimeOffset.UtcNow, idempotencyKey);
        var evidence = CreateEvidence(context, purchaseOrderId, current.Value.Scope, "procurement.purchase-order.submit", current.Value.Status, PurchaseOrderStatus.PendingApproval, null, idempotencyKey, requestFingerprint, current.Value.Status.ToString(), "PendingApproval");
        return ToOperationResult(await persistence.SubmitAsync(context.TenantContext, command, evidence, cancellationToken));
    }

    public Task<PurchaseOrderOperationResult<PurchaseOrderRecord>> ApproveAsync(ProcurementRequestContext context, Guid purchaseOrderId, byte[] expectedVersion, string? idempotencyKey, string? requestFingerprint, CancellationToken cancellationToken = default) =>
        ApproveOrDecisionAsync(context, purchaseOrderId, expectedVersion, idempotencyKey, requestFingerprint, "procurement.purchase-order.approve", PurchaseOrderHistoryAction.ApprovalRecorded, null, cancellationToken);

    public Task<PurchaseOrderOperationResult<PurchaseOrderRecord>> RejectAsync(ProcurementRequestContext context, Guid purchaseOrderId, byte[] expectedVersion, string? reason, string? idempotencyKey, string? requestFingerprint, CancellationToken cancellationToken = default) =>
        ApproveOrDecisionAsync(context, purchaseOrderId, expectedVersion, idempotencyKey, requestFingerprint, "procurement.purchase-order.reject", PurchaseOrderHistoryAction.Rejected, reason, cancellationToken);

    public Task<PurchaseOrderOperationResult<PurchaseOrderRecord>> ReturnForChangeAsync(ProcurementRequestContext context, Guid purchaseOrderId, byte[] expectedVersion, string? reason, string? idempotencyKey, string? requestFingerprint, CancellationToken cancellationToken = default) =>
        ApproveOrDecisionAsync(context, purchaseOrderId, expectedVersion, idempotencyKey, requestFingerprint, "procurement.purchase-order.return-for-change", PurchaseOrderHistoryAction.ReturnedForChange, reason, cancellationToken);

    public async Task<PurchaseOrderOperationResult<PurchaseOrderRecord>> IssueAsync(
        ProcurementRequestContext context,
        Guid purchaseOrderId,
        byte[] expectedVersion,
        string? idempotencyKey,
        string? requestFingerprint,
        CancellationToken cancellationToken = default)
    {
        var current = await GetAuthorizedAsync(context, purchaseOrderId, "procurement.purchase-order.issue", cancellationToken);
        if (!current.Succeeded || current.Value is null)
        {
            return current;
        }

        if (expectedVersion is null || expectedVersion.Length == 0)
        {
            return PurchaseOrderOperationResult<PurchaseOrderRecord>.Failure("validation_failed");
        }

        var replay = await TryDurableReplayAsync(context, "procurement.purchase-order.issue", purchaseOrderId, idempotencyKey, requestFingerprint, cancellationToken);
        if (replay is not null)
        {
            return replay;
        }

        if (current.Value.Status != PurchaseOrderStatus.Approved)
        {
            return PurchaseOrderOperationResult<PurchaseOrderRecord>.Failure("issue_not_allowed");
        }

        var command = new PurchaseOrderActionCommand(purchaseOrderId, expectedVersion, context.ActorId, null, DateTimeOffset.UtcNow, idempotencyKey);
        var evidence = CreateEvidence(context, purchaseOrderId, current.Value.Scope, "procurement.purchase-order.issue", current.Value.Status, PurchaseOrderStatus.Issued, null, idempotencyKey, requestFingerprint, "Approved", "Issued;no-stock-no-ap");
        return ToOperationResult(await persistence.IssueAsync(context.TenantContext, command, evidence, cancellationToken));
    }

    public async Task<PurchaseOrderOperationResult<PurchaseOrderRecord>> CancelAsync(
        ProcurementRequestContext context,
        Guid purchaseOrderId,
        byte[] expectedVersion,
        string? reason,
        string? idempotencyKey,
        string? requestFingerprint,
        CancellationToken cancellationToken = default)
    {
        var current = await GetAuthorizedAsync(context, purchaseOrderId, "procurement.purchase-order.cancel", cancellationToken);
        if (!current.Succeeded || current.Value is null)
        {
            return current;
        }

        if (expectedVersion is null || expectedVersion.Length == 0)
        {
            return PurchaseOrderOperationResult<PurchaseOrderRecord>.Failure("validation_failed");
        }

        if (current.Value.CreatedByActorId != context.ActorId)
        {
            return PurchaseOrderOperationResult<PurchaseOrderRecord>.Failure("creator_only");
        }

        if (!PurchaseOrderValuePolicy.TryReason(reason, out var normalizedReason))
        {
            return PurchaseOrderOperationResult<PurchaseOrderRecord>.Failure("reason_required");
        }

        var replay = await TryDurableReplayAsync(context, "procurement.purchase-order.cancel", purchaseOrderId, idempotencyKey, requestFingerprint, cancellationToken);
        if (replay is not null)
        {
            return replay;
        }

        var command = new PurchaseOrderActionCommand(purchaseOrderId, expectedVersion, context.ActorId, normalizedReason, DateTimeOffset.UtcNow, idempotencyKey);
        var evidence = CreateEvidence(context, purchaseOrderId, current.Value.Scope, "procurement.purchase-order.cancel", current.Value.Status, PurchaseOrderStatus.Cancelled, normalizedReason, idempotencyKey, requestFingerprint, current.Value.Status.ToString(), "Cancelled");
        return ToOperationResult(await persistence.CancelAsync(context.TenantContext, command, evidence, cancellationToken));
    }

    public async Task<PurchaseOrderOperationResult<PurchaseOrderRecord>> RecordConfirmationAsync(
        ProcurementRequestContext context,
        Guid purchaseOrderId,
        PurchaseOrderConfirmationRequest request,
        byte[] expectedVersion,
        string? idempotencyKey,
        string? requestFingerprint,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (expectedVersion is null || expectedVersion.Length == 0
            || !PurchaseOrderValuePolicy.TryNormalizeConfirmation(request, out var supplierReference, out var supplierContact, out var reason, out var notes, out var lines, out var evidenceWrites))
        {
            return PurchaseOrderOperationResult<PurchaseOrderRecord>.Failure("validation_failed");
        }

        var current = await GetAuthorizedAsync(context, purchaseOrderId, "procurement.purchase-order.confirmation.capture", cancellationToken);
        if (!current.Succeeded || current.Value is null)
        {
            return current;
        }

        var replay = await TryDurableReplayAsync(context, "procurement.purchase-order.confirmation.capture", purchaseOrderId, idempotencyKey, requestFingerprint, cancellationToken);
        if (replay is not null)
        {
            return replay;
        }

        var currentRecord = current.Value;
        if (currentRecord.Status is not (PurchaseOrderStatus.Issued or PurchaseOrderStatus.Confirmed or PurchaseOrderStatus.PartiallyConfirmed or PurchaseOrderStatus.NoResponse))
        {
            return PurchaseOrderOperationResult<PurchaseOrderRecord>.Failure("confirmation_not_allowed");
        }

        var requestedById = lines.ToDictionary(item => item.PurchaseOrderLineId);
        if (lines.Count == 0 && (request.Status is PurchaseOrderConfirmationStatus.Rejected or PurchaseOrderConfirmationStatus.NoResponse))
        {
            lines = currentRecord.Lines
                .Select(item => new PurchaseOrderConfirmationLineRequest(item.Id, 0m, null, null, null, null, null))
                .ToArray();
            requestedById = lines.ToDictionary(item => item.PurchaseOrderLineId);
        }

        if (lines.Count != currentRecord.Lines.Count || currentRecord.Lines.Any(item => !requestedById.ContainsKey(item.Id)))
        {
            return PurchaseOrderOperationResult<PurchaseOrderRecord>.Failure("line_set_mismatch");
        }

        var hasChanges = false;
        var normalizedLines = new List<PurchaseOrderConfirmationLineCommand>(lines.Count);
        foreach (var line in currentRecord.Lines)
        {
            var requested = requestedById[line.Id];
            if (requested.ConfirmedQuantity > line.OrderedQuantity || requested.ConfirmedQuantity < 0)
            {
                return PurchaseOrderOperationResult<PurchaseOrderRecord>.Failure("confirmed_quantity_exceeds_ordered");
            }

            var hasLineChange = requested.ProposedQuantity.HasValue || requested.ProposedUnitPrice.HasValue || requested.ProposedDeliveryDate.HasValue;
            hasChanges |= hasLineChange;
            normalizedLines.Add(new PurchaseOrderConfirmationLineCommand(
                Guid.NewGuid(),
                line.Id,
                line.OrderedQuantity,
                requested.ConfirmedQuantity,
                Math.Max(0m, line.OrderedQuantity - requested.ConfirmedQuantity),
                requested.ExpectedDeliveryDate,
                requested.ProposedQuantity,
                requested.ProposedUnitPrice,
                requested.ProposedDeliveryDate,
                requested.ChangeReason));
        }

        if (request.Status == PurchaseOrderConfirmationStatus.Confirmed && normalizedLines.Any(item => item.ConfirmedQuantity < item.OrderedQuantityAtResponse))
        {
            return PurchaseOrderOperationResult<PurchaseOrderRecord>.Failure("confirmation_status_mismatch");
        }

        if (request.Status == PurchaseOrderConfirmationStatus.PartiallyConfirmed
            && (normalizedLines.All(item => item.ConfirmedQuantity == 0m) || normalizedLines.All(item => item.ConfirmedQuantity >= item.OrderedQuantityAtResponse)))
        {
            return PurchaseOrderOperationResult<PurchaseOrderRecord>.Failure("confirmation_status_mismatch");
        }

        if (request.Status is PurchaseOrderConfirmationStatus.Rejected or PurchaseOrderConfirmationStatus.NoResponse
            && normalizedLines.Any(item => item.ConfirmedQuantity != 0m))
        {
            return PurchaseOrderOperationResult<PurchaseOrderRecord>.Failure("confirmation_status_mismatch");
        }

        PurchaseRequestApprovalPolicyDefinition? reapprovalPolicy = null;
        if (hasChanges)
        {
            reapprovalPolicy = await policyProvider.ResolveAsync(context, currentRecord.Scope, DateTimeOffset.UtcNow, cancellationToken);
            if (!PurchaseRequestValuePolicy.IsValidPolicy(reapprovalPolicy))
            {
                return PurchaseOrderOperationResult<PurchaseOrderRecord>.Failure("approval_policy_not_configured");
            }
        }

        var occurredAt = DateTimeOffset.UtcNow;
        var command = new PurchaseOrderConfirmationCommand(
            Guid.NewGuid(),
            purchaseOrderId,
            request.Status,
            request.ResponseDate,
            supplierReference,
            supplierContact,
            reason,
            notes,
            context.ActorId,
            expectedVersion,
            normalizedLines,
            evidenceWrites.Select(item => new PurchaseOrderEvidenceReference(
                Guid.NewGuid(),
                item.ReferenceId ?? item.ExternalReference ?? string.Empty,
                item.FileName,
                item.ContentType,
                item.Description,
                item.Source ?? "buyer-recorded",
                item.ExternalReference,
                context.ActorId,
                occurredAt)).ToArray(),
            reapprovalPolicy,
            occurredAt,
            idempotencyKey);
        var targetStatus = hasChanges ? PurchaseOrderStatus.ChangedPendingApproval : request.Status switch
        {
            PurchaseOrderConfirmationStatus.Confirmed => PurchaseOrderStatus.Confirmed,
            PurchaseOrderConfirmationStatus.PartiallyConfirmed => PurchaseOrderStatus.PartiallyConfirmed,
            PurchaseOrderConfirmationStatus.Rejected => PurchaseOrderStatus.Rejected,
            _ => PurchaseOrderStatus.NoResponse
        };
        var evidence = CreateEvidence(context, purchaseOrderId, currentRecord.Scope, "procurement.purchase-order.confirmation.capture", currentRecord.Status, targetStatus, reason, idempotencyKey, requestFingerprint, currentRecord.Status.ToString(), targetStatus.ToString());
        return ToOperationResult(await persistence.RecordConfirmationAsync(context.TenantContext, command, evidence, cancellationToken));
    }

    public Task<PurchaseOrderOperationResult<PurchaseOrderRecord>> ApproveSupplierChangeAsync(ProcurementRequestContext context, Guid purchaseOrderId, byte[] expectedVersion, string? idempotencyKey, string? requestFingerprint, CancellationToken cancellationToken = default) =>
        ApproveSupplierChangeOrRejectAsync(context, purchaseOrderId, expectedVersion, null, idempotencyKey, requestFingerprint, approve: true, cancellationToken);

    public Task<PurchaseOrderOperationResult<PurchaseOrderRecord>> RejectSupplierChangeAsync(ProcurementRequestContext context, Guid purchaseOrderId, byte[] expectedVersion, string? reason, string? idempotencyKey, string? requestFingerprint, CancellationToken cancellationToken = default) =>
        ApproveSupplierChangeOrRejectAsync(context, purchaseOrderId, expectedVersion, reason, idempotencyKey, requestFingerprint, approve: false, cancellationToken);

    public async Task<PurchaseOrderOperationResult<IReadOnlyList<PurchaseOrderConfirmationRecord>>> ReadConfirmationsAsync(ProcurementRequestContext context, Guid purchaseOrderId, CancellationToken cancellationToken = default)
    {
        var current = await GetAuthorizedAsync(context, purchaseOrderId, "procurement.purchase-order.confirmation.read", cancellationToken);
        if (!current.Succeeded || current.Value is null)
        {
            return PurchaseOrderOperationResult<IReadOnlyList<PurchaseOrderConfirmationRecord>>.Failure(current.Code);
        }

        try
        {
            return PurchaseOrderOperationResult<IReadOnlyList<PurchaseOrderConfirmationRecord>>.Success(await persistence.ReadConfirmationsAsync(context.TenantContext, purchaseOrderId, cancellationToken));
        }
        catch
        {
            return PurchaseOrderOperationResult<IReadOnlyList<PurchaseOrderConfirmationRecord>>.Failure("persistence_unavailable");
        }
    }

    public async Task<PurchaseOrderOperationResult<IReadOnlyList<PurchaseOrderHistoryRecord>>> ReadHistoryAsync(ProcurementRequestContext context, Guid purchaseOrderId, CancellationToken cancellationToken = default)
    {
        var current = await GetAuthorizedAsync(context, purchaseOrderId, "procurement.purchase-order.history.read", cancellationToken);
        if (!current.Succeeded || current.Value is null)
        {
            return PurchaseOrderOperationResult<IReadOnlyList<PurchaseOrderHistoryRecord>>.Failure(current.Code);
        }

        try
        {
            return PurchaseOrderOperationResult<IReadOnlyList<PurchaseOrderHistoryRecord>>.Success(await persistence.ReadHistoryAsync(context.TenantContext, purchaseOrderId, cancellationToken));
        }
        catch
        {
            return PurchaseOrderOperationResult<IReadOnlyList<PurchaseOrderHistoryRecord>>.Failure("persistence_unavailable");
        }
    }

    public async Task<PurchaseOrderOperationResult<IReadOnlyList<PurchaseOrderAuditRecord>>> ReadAuditAsync(ProcurementRequestContext context, Guid purchaseOrderId, CancellationToken cancellationToken = default)
    {
        var current = await GetAuthorizedAsync(context, purchaseOrderId, "procurement.purchase-order.audit.read", cancellationToken);
        if (!current.Succeeded || current.Value is null)
        {
            return PurchaseOrderOperationResult<IReadOnlyList<PurchaseOrderAuditRecord>>.Failure(current.Code);
        }

        try
        {
            return PurchaseOrderOperationResult<IReadOnlyList<PurchaseOrderAuditRecord>>.Success(await persistence.ReadAuditAsync(context.TenantContext, purchaseOrderId, cancellationToken));
        }
        catch
        {
            return PurchaseOrderOperationResult<IReadOnlyList<PurchaseOrderAuditRecord>>.Failure("persistence_unavailable");
        }
    }

    private async Task<PurchaseOrderOperationResult<PurchaseOrderRecord>> ApproveOrDecisionAsync(
        ProcurementRequestContext context,
        Guid purchaseOrderId,
        byte[] expectedVersion,
        string? idempotencyKey,
        string? requestFingerprint,
        string operationId,
        PurchaseOrderHistoryAction action,
        string? reason,
        CancellationToken cancellationToken)
    {
        var current = await GetAuthorizedAsync(context, purchaseOrderId, operationId, cancellationToken);
        if (!current.Succeeded || current.Value is null)
        {
            return current;
        }

        if (expectedVersion is null || expectedVersion.Length == 0)
        {
            return PurchaseOrderOperationResult<PurchaseOrderRecord>.Failure("validation_failed");
        }

        // A replay can never bypass separation of duties: the persisted evidence is matched on this exact
        // actor, so only an actor who already passed the SoD, stage, and delegation checks can replay.
        var replay = await TryDurableReplayAsync(context, operationId, purchaseOrderId, idempotencyKey, requestFingerprint, cancellationToken);
        if (replay is not null)
        {
            return replay;
        }

        if (current.Value.Status != PurchaseOrderStatus.PendingApproval)
        {
            return PurchaseOrderOperationResult<PurchaseOrderRecord>.Failure("decision_not_allowed");
        }

        if (current.Value.CreatedByActorId == context.ActorId)
        {
            return PurchaseOrderOperationResult<PurchaseOrderRecord>.Failure("self_approval_denied");
        }

        if (action is PurchaseOrderHistoryAction.Rejected or PurchaseOrderHistoryAction.ReturnedForChange
            && !PurchaseOrderValuePolicy.TryReason(reason, out reason!))
        {
            return PurchaseOrderOperationResult<PurchaseOrderRecord>.Failure("reason_required");
        }

        var stage = CurrentStage(current.Value);
        if (stage is null)
        {
            return PurchaseOrderOperationResult<PurchaseOrderRecord>.Failure("approval_policy_invalid");
        }

        var eligibility = await ResolveApprovalActorAsync(context, current.Value.Scope, stage, current.Value.CreatedByActorId, cancellationToken);
        if (!eligibility.Eligible)
        {
            return PurchaseOrderOperationResult<PurchaseOrderRecord>.Failure("approval_not_eligible");
        }

        var occurredAt = DateTimeOffset.UtcNow;
        var evidence = CreateEvidence(context, purchaseOrderId, current.Value.Scope, operationId, current.Value.Status, action == PurchaseOrderHistoryAction.ApprovalRecorded ? null : action == PurchaseOrderHistoryAction.Rejected ? PurchaseOrderStatus.Rejected : PurchaseOrderStatus.ReturnedForChange, reason, idempotencyKey, requestFingerprint, current.Value.Status.ToString(), action.ToString());
        if (action == PurchaseOrderHistoryAction.ApprovalRecorded)
        {
            return ToOperationResult(await persistence.ApproveAsync(context.TenantContext, new PurchaseOrderApprovalCommand(purchaseOrderId, expectedVersion, context.ActorId, eligibility.DelegatedFrom, occurredAt, idempotencyKey), evidence, cancellationToken));
        }

        var command = new PurchaseOrderActionCommand(purchaseOrderId, expectedVersion, context.ActorId, reason, occurredAt, idempotencyKey);
        var result = action == PurchaseOrderHistoryAction.Rejected
            ? await persistence.RejectAsync(context.TenantContext, command, evidence, cancellationToken)
            : await persistence.ReturnForChangeAsync(context.TenantContext, command, evidence, cancellationToken);
        return ToOperationResult(result);
    }

    private async Task<PurchaseOrderOperationResult<PurchaseOrderRecord>> ApproveSupplierChangeOrRejectAsync(
        ProcurementRequestContext context,
        Guid purchaseOrderId,
        byte[] expectedVersion,
        string? reason,
        string? idempotencyKey,
        string? requestFingerprint,
        bool approve,
        CancellationToken cancellationToken)
    {
        var operationId = approve ? "procurement.purchase-order.supplier-change.approve" : "procurement.purchase-order.supplier-change.reject";
        var current = await GetAuthorizedAsync(context, purchaseOrderId, operationId, cancellationToken);
        if (!current.Succeeded || current.Value is null)
        {
            return current;
        }

        if (expectedVersion is null || expectedVersion.Length == 0)
        {
            return PurchaseOrderOperationResult<PurchaseOrderRecord>.Failure("validation_failed");
        }

        var replay = await TryDurableReplayAsync(context, operationId, purchaseOrderId, idempotencyKey, requestFingerprint, cancellationToken);
        if (replay is not null)
        {
            return replay;
        }

        if (current.Value.Status != PurchaseOrderStatus.ChangedPendingApproval)
        {
            return PurchaseOrderOperationResult<PurchaseOrderRecord>.Failure("supplier_change_approval_not_allowed");
        }

        if (current.Value.CreatedByActorId == context.ActorId)
        {
            return PurchaseOrderOperationResult<PurchaseOrderRecord>.Failure("self_approval_denied");
        }

        if (!approve && !PurchaseOrderValuePolicy.TryReason(reason, out reason!))
        {
            return PurchaseOrderOperationResult<PurchaseOrderRecord>.Failure("reason_required");
        }

        var stage = CurrentStage(current.Value);
        if (stage is null)
        {
            return PurchaseOrderOperationResult<PurchaseOrderRecord>.Failure("approval_policy_invalid");
        }

        var eligibility = await ResolveApprovalActorAsync(context, current.Value.Scope, stage, current.Value.CreatedByActorId, cancellationToken);
        if (!eligibility.Eligible)
        {
            return PurchaseOrderOperationResult<PurchaseOrderRecord>.Failure("approval_not_eligible");
        }

        var occurredAt = DateTimeOffset.UtcNow;
        var evidence = CreateEvidence(context, purchaseOrderId, current.Value.Scope, operationId, current.Value.Status, null, reason, idempotencyKey, requestFingerprint, "ChangedPendingApproval", approve ? "SupplierChangeApproved" : "SupplierChangeRejected");
        if (approve)
        {
            return ToOperationResult(await persistence.ApproveSupplierChangeAsync(context.TenantContext, new PurchaseOrderApprovalCommand(purchaseOrderId, expectedVersion, context.ActorId, eligibility.DelegatedFrom, occurredAt, idempotencyKey), evidence, cancellationToken));
        }

        return ToOperationResult(await persistence.RejectSupplierChangeAsync(context.TenantContext, new PurchaseOrderActionCommand(purchaseOrderId, expectedVersion, context.ActorId, reason, occurredAt, idempotencyKey), evidence, cancellationToken));
    }

    private async Task<(bool Eligible, Guid? DelegatedFrom)> ResolveApprovalActorAsync(
        ProcurementRequestContext context,
        PurchaseRequestScope scope,
        PurchaseRequestApprovalStageDefinition stage,
        Guid requesterId,
        CancellationToken cancellationToken)
    {
        if (requesterId == context.ActorId)
        {
            return (false, null);
        }

        var eligibleIds = (stage.EligibleApproverIds ?? []).ToHashSet();
        if (eligibleIds.Count == 0 || eligibleIds.Contains(context.ActorId))
        {
            return (true, null);
        }

        if (!stage.AllowDelegation)
        {
            return (false, null);
        }

        var delegation = await delegationProvider.ResolveAsync(context, scope, stage, context.ActorId, DateTimeOffset.UtcNow, cancellationToken);
        return delegation is { } value && value.DelegatorId != requesterId && value.DelegateeId == context.ActorId
            ? (true, value.DelegatorId)
            : (false, null);
    }

    private static PurchaseRequestApprovalStageDefinition? CurrentStage(PurchaseOrderRecord record) =>
        record.ApprovalPolicy?.Stages
            .OrderBy(item => item.Sequence)
            .ElementAtOrDefault(record.Approval?.StageIndex ?? 0);

    private async Task<PurchaseOrderOperationResult<PurchaseOrderRecord>> GetAuthorizedAsync(ProcurementRequestContext context, Guid purchaseOrderId, string operationId, CancellationToken cancellationToken)
    {
        var current = await FindAsync(context, purchaseOrderId, cancellationToken);
        if (!current.Succeeded || current.Value is null)
        {
            return current;
        }

        var authorized = authorization.Authorize(context, operationId, current.Value.Scope);
        return authorized.Allowed ? current : PurchaseOrderOperationResult<PurchaseOrderRecord>.Failure(authorized.Code);
    }

    private async Task<PurchaseOrderOperationResult<PurchaseOrderRecord>> FindAsync(ProcurementRequestContext context, Guid purchaseOrderId, CancellationToken cancellationToken)
    {
        if (purchaseOrderId == Guid.Empty)
        {
            return PurchaseOrderOperationResult<PurchaseOrderRecord>.Failure("validation_failed");
        }

        try
        {
            var record = await persistence.FindAsync(context.TenantContext, purchaseOrderId, cancellationToken);
            return record is null
                ? PurchaseOrderOperationResult<PurchaseOrderRecord>.Failure("purchase_order_not_found")
                : PurchaseOrderOperationResult<PurchaseOrderRecord>.Success(record);
        }
        catch
        {
            return PurchaseOrderOperationResult<PurchaseOrderRecord>.Failure("persistence_unavailable");
        }
    }

    private async Task<PurchaseOrderOperationResult<PurchaseOrderSourceOptionRecord>> FindSourceOptionAsync(ProcurementRequestContext context, Guid sourceDecisionId, CancellationToken cancellationToken)
    {
        try
        {
            var requests = await purchaseRequests.ListAsync(context.TenantContext, PurchaseRequestStatus.Approved, cancellationToken);
            foreach (var summary in requests)
            {
                var request = await purchaseRequests.FindAsync(context.TenantContext, summary.Id, cancellationToken);
                if (request is null)
                {
                    continue;
                }

                var decision = await quotations.FindSourceDecisionAsync(context.TenantContext, request.Id, cancellationToken);
                if (decision?.Id != sourceDecisionId)
                {
                    continue;
                }

                var quotation = await quotations.FindAsync(context.TenantContext, decision.SelectedQuotationId, cancellationToken);
                if (quotation is null || quotation.Status != SupplierQuotationStatus.Submitted)
                {
                    return PurchaseOrderOperationResult<PurchaseOrderSourceOptionRecord>.Failure("source_quotation_not_eligible");
                }

                return PurchaseOrderOperationResult<PurchaseOrderSourceOptionRecord>.Success(new PurchaseOrderSourceOptionRecord(
                    ToSourceResponse(request, decision, quotation),
                    request.Scope,
                    BuildSourceLines(request, quotation),
                    request.Version));
            }

            return PurchaseOrderOperationResult<PurchaseOrderSourceOptionRecord>.Failure("source_decision_not_found");
        }
        catch
        {
            return PurchaseOrderOperationResult<PurchaseOrderSourceOptionRecord>.Failure("persistence_unavailable");
        }
    }

    private static IReadOnlyList<PurchaseOrderSourceLineSnapshot> BuildSourceLines(PurchaseRequestRecord request, SupplierQuotationRecord quotation)
    {
        var requestLines = request.Lines.ToDictionary(item => item.Id);
        return quotation.Lines
            .Where(item => requestLines.ContainsKey(item.PurchaseRequestLineId))
            .OrderBy(item => item.PurchaseRequestLineId)
            .Select(item =>
            {
                var sourceLine = requestLines[item.PurchaseRequestLineId];
                return new PurchaseOrderSourceLineSnapshot(
                    item.Id,
                    item.PurchaseRequestLineId,
                    item.ProductId,
                    item.ProductSku,
                    item.ProductName,
                    item.UnitOfMeasureId,
                    item.UnitOfMeasureCode,
                    sourceLine.Quantity,
                    item.QuotedQuantity,
                    item.UnitPrice,
                    item.DiscountAmount,
                    item.DiscountPercentage,
                    item.TaxId,
                    item.TaxCode,
                    item.TaxName,
                    item.TaxRatePercentage,
                    item.TaxAmount,
                    sourceLine.NeedByDate,
                    item.OfferedDeliveryDate);
            })
            .ToArray();
    }

    private static PurchaseOrderSourceResponse ToSourceResponse(PurchaseRequestRecord request, SupplierSourceDecisionRecord decision, SupplierQuotationRecord quotation) => new(
        request.Id,
        quotation.Id,
        decision.Id,
        $"PR-{request.Id.ToString("N")[..8].ToUpperInvariant()}",
        request.Purpose,
        quotation.SupplierQuotationReference,
        new PurchaseOrderSupplierResponse(quotation.Supplier.Id, quotation.Supplier.Code, quotation.Supplier.Name),
        new PurchaseOrderCurrencyResponse(quotation.Currency.Id, quotation.Currency.Code, quotation.Currency.Name),
        quotation.PaymentTerm is null ? null : new PurchaseOrderPaymentTermResponse(quotation.PaymentTerm.Id, quotation.PaymentTerm.Code, quotation.PaymentTerm.Name, quotation.PaymentTerm.Version),
        decision.Rationale,
        decision.SelectedAt);

    private static PurchaseOrderAuditEvidence CreateEvidence(ProcurementRequestContext context, Guid purchaseOrderId, PurchaseRequestScope scope, string operationId, PurchaseOrderStatus? beforeStatus, PurchaseOrderStatus? afterStatus, string? reason, string? idempotencyKey, string? requestFingerprint, string? beforeSummary, string? afterSummary) => new(
        Guid.NewGuid(),
        purchaseOrderId,
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
        ComputeRequestFingerprint(operationId, purchaseOrderId, requestFingerprint));

    private static string? ComputeRequestFingerprint(string operationId, Guid purchaseOrderId, string? rawFingerprint)
    {
        if (string.IsNullOrWhiteSpace(rawFingerprint))
        {
            return null;
        }

        var targetDiscriminator = operationId == CreateOperationId
            ? "new"
            : purchaseOrderId.ToString("D");
        var canonical = $"{operationId}|{targetDiscriminator}|{rawFingerprint}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    /// <summary>
    /// Consults durable persisted idempotency evidence for an existing Purchase Order after the trusted
    /// Tenant context, the current target, and the caller's authority over that target have already been
    /// established, but before any lifecycle, concurrency, approval-stage, delegation, or reapproval check
    /// that the original successful request itself changed. Returns <c>null</c> when the caller must
    /// continue through normal business validation and mutation.
    /// </summary>
    private async Task<PurchaseOrderOperationResult<PurchaseOrderRecord>?> TryDurableReplayAsync(
        ProcurementRequestContext context,
        string operationId,
        Guid purchaseOrderId,
        string? idempotencyKey,
        string? requestFingerprint,
        CancellationToken cancellationToken)
    {
        var probe = await ProbeReplayAsync(context, operationId, purchaseOrderId, idempotencyKey, requestFingerprint, cancellationToken);
        return probe.Outcome switch
        {
            PurchaseOrderReplayOutcome.Replay when probe.Record is not null => PurchaseOrderOperationResult<PurchaseOrderRecord>.Success(probe.Record),
            PurchaseOrderReplayOutcome.Conflict => PurchaseOrderOperationResult<PurchaseOrderRecord>.Failure("idempotency_conflict"),
            _ => null
        };
    }

    private async Task<PurchaseOrderReplayProbe> ProbeReplayAsync(
        ProcurementRequestContext context,
        string operationId,
        Guid? purchaseOrderId,
        string? idempotencyKey,
        string? requestFingerprint,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return PurchaseOrderReplayProbe.NotFound;
        }

        var query = new PurchaseOrderReplayQuery(
            context.ActorId,
            operationId,
            idempotencyKey,
            purchaseOrderId,
            ComputeRequestFingerprint(operationId, purchaseOrderId ?? Guid.Empty, requestFingerprint));
        try
        {
            return await persistence.ProbeReplayAsync(context.TenantContext, query, cancellationToken);
        }
        catch
        {
            // A probe failure must never approve a mutation on its own; fall through to the normal path,
            // where the persistence-side replay check still runs inside the mutation transaction.
            return PurchaseOrderReplayProbe.NotFound;
        }
    }

    private static PurchaseOrderOperationResult<T> ToOperationResult<T>(PurchaseOrderPersistenceResult<T> result) =>
        result.Succeeded && result.Value is not null
            ? PurchaseOrderOperationResult<T>.Success(result.Value)
            : PurchaseOrderOperationResult<T>.Failure(result.Code);
}

#pragma warning restore CS1591
