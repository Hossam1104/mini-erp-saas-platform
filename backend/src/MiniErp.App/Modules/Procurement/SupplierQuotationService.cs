#pragma warning disable CS1591

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.Modules.BusinessParties;
using MiniErp.App.Modules.MasterData;
using MiniErp.Contracts.Modules.MasterData;
using MiniErp.Contracts.Modules.Procurement;

namespace MiniErp.App.Modules.Procurement;

public sealed class SupplierQuotationService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly PurchaseRequestAuthorizationService authorization;
    private readonly IPurchaseRequestPersistence purchaseRequests;
    private readonly ISupplierQuotationPersistence persistence;
    private readonly ISupplierPersistence suppliers;
    private readonly IMasterDataCurrencyPaymentTermPersistence currenciesAndPaymentTerms;
    private readonly IMasterDataTaxPersistence taxes;

    public SupplierQuotationService(
        PurchaseRequestAuthorizationService authorization,
        IPurchaseRequestPersistence purchaseRequests,
        ISupplierQuotationPersistence persistence,
        ISupplierPersistence suppliers,
        IMasterDataCurrencyPaymentTermPersistence currenciesAndPaymentTerms,
        IMasterDataTaxPersistence taxes)
    {
        this.authorization = authorization ?? throw new ArgumentNullException(nameof(authorization));
        this.purchaseRequests = purchaseRequests ?? throw new ArgumentNullException(nameof(purchaseRequests));
        this.persistence = persistence ?? throw new ArgumentNullException(nameof(persistence));
        this.suppliers = suppliers ?? throw new ArgumentNullException(nameof(suppliers));
        this.currenciesAndPaymentTerms = currenciesAndPaymentTerms ?? throw new ArgumentNullException(nameof(currenciesAndPaymentTerms));
        this.taxes = taxes ?? throw new ArgumentNullException(nameof(taxes));
    }

    public async Task<SupplierQuotationOperationResult<IReadOnlyList<SupplierQuotationRecord>>> ListAsync(
        ProcurementRequestContext context,
        Guid purchaseRequestId,
        CancellationToken cancellationToken = default)
    {
        var request = await GetPurchaseRequestAsync(
            context,
            purchaseRequestId,
            "procurement.quotation.list",
            cancellationToken);
        if (!request.Succeeded || request.Value is null)
        {
            return SupplierQuotationOperationResult<IReadOnlyList<SupplierQuotationRecord>>.Failure(request.Code);
        }

        try
        {
            var records = await persistence.ListAsync(context.TenantContext, purchaseRequestId, cancellationToken);
            var decision = await persistence.FindSourceDecisionAsync(
                context.TenantContext,
                purchaseRequestId,
                cancellationToken);
            return SupplierQuotationOperationResult<IReadOnlyList<SupplierQuotationRecord>>.Success(
                records.Select(item => item with
                {
                    IsSelected = decision?.SelectedQuotationId == item.Id
                }).ToArray());
        }
        catch
        {
            return SupplierQuotationOperationResult<IReadOnlyList<SupplierQuotationRecord>>.Failure("persistence_unavailable");
        }
    }

    public async Task<SupplierQuotationOperationResult<SupplierQuotationRecord>> GetAsync(
        ProcurementRequestContext context,
        Guid quotationId,
        CancellationToken cancellationToken = default)
    {
        var quotation = await FindQuotationAsync(context, quotationId, cancellationToken);
        if (!quotation.Succeeded || quotation.Value is null)
        {
            return quotation;
        }

        var authorized = authorization.Authorize(
            context,
            "procurement.quotation.read",
            quotation.Value.Scope);
        return authorized.Allowed
            ? quotation
            : SupplierQuotationOperationResult<SupplierQuotationRecord>.Failure(authorized.Code);
    }

    public async Task<SupplierQuotationOperationResult<SupplierQuotationRecord>> CreateAsync(
        ProcurementRequestContext context,
        Guid purchaseRequestId,
        SupplierQuotationWriteRequest request,
        string? idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(request);

        var source = await GetApprovedPurchaseRequestAsync(
            context,
            purchaseRequestId,
            "procurement.quotation.create",
            cancellationToken);
        if (!source.Succeeded || source.Value is null)
        {
            return SupplierQuotationOperationResult<SupplierQuotationRecord>.Failure(source.Code);
        }

        if (!SupplierQuotationValuePolicy.TryNormalize(
                request,
                out var quotationReference,
                out var deliveryTerms,
                out var offeredDeliveryLeadTime,
                out var notes,
                out var lines,
                out var evidence))
        {
            return SupplierQuotationOperationResult<SupplierQuotationRecord>.Failure("validation_failed");
        }

        var supplierAuthorization = authorization.Authorize(
            context,
            "procurement.quotation.create",
            source.Value.Scope);
        if (!supplierAuthorization.Allowed)
        {
            return SupplierQuotationOperationResult<SupplierQuotationRecord>.Failure(supplierAuthorization.Code);
        }

        var references = await ResolveReferencesAsync(
            context,
            request,
            lines,
            source.Value,
            cancellationToken);
        if (!references.Succeeded || references.Value is null)
        {
            return SupplierQuotationOperationResult<SupplierQuotationRecord>.Failure(references.Code);
        }

        var quotationId = Guid.NewGuid();
        var occurredAt = DateTimeOffset.UtcNow;
        var command = new SupplierQuotationCreateCommand(
            quotationId,
            purchaseRequestId,
            source.Value.Scope,
            context.ActorId,
            references.Value.Supplier,
            quotationReference!,
            request.OfferDate,
            request.ValidUntil,
            references.Value.Currency,
            references.Value.PaymentTerm,
            deliveryTerms,
            request.OfferedDeliveryDate,
            offeredDeliveryLeadTime,
            notes,
            references.Value.Lines,
            ToEvidenceReferences(evidence, context.ActorId, occurredAt),
            occurredAt,
            idempotencyKey);
        var audit = CreateAuditEvidence(
            context,
            command,
            "procurement.quotation.create",
            beforeStatus: null,
            afterStatus: SupplierQuotationStatus.Draft,
            reason: null,
            idempotencyKey,
            beforeSummary: null,
            afterSummary: "Draft");
        return ToOperationResult(await persistence.CreateAsync(
            context.TenantContext,
            command,
            audit,
            cancellationToken));
    }

    public async Task<SupplierQuotationOperationResult<SupplierQuotationRecord>> EditAsync(
        ProcurementRequestContext context,
        Guid quotationId,
        SupplierQuotationWriteRequest request,
        byte[] expectedVersion,
        string? idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(request);
        if (expectedVersion is null || expectedVersion.Length == 0)
        {
            return SupplierQuotationOperationResult<SupplierQuotationRecord>.Failure("validation_failed");
        }

        var current = await FindQuotationAsync(context, quotationId, cancellationToken);
        if (!current.Succeeded || current.Value is null)
        {
            return current;
        }

        var editAuthorization = authorization.Authorize(
            context,
            "procurement.quotation.edit",
            current.Value.Scope);
        if (!editAuthorization.Allowed)
        {
            return SupplierQuotationOperationResult<SupplierQuotationRecord>.Failure(editAuthorization.Code);
        }

        var source = await GetApprovedPurchaseRequestAsync(
            context,
            current.Value.PurchaseRequestId,
            "procurement.quotation.edit",
            cancellationToken);
        if (!source.Succeeded || source.Value is null)
        {
            return SupplierQuotationOperationResult<SupplierQuotationRecord>.Failure(source.Code);
        }

        if (current.Value.Status != SupplierQuotationStatus.Draft)
        {
            return SupplierQuotationOperationResult<SupplierQuotationRecord>.Failure("edit_not_allowed");
        }

        if (!SupplierQuotationValuePolicy.TryNormalize(
                request,
                out var quotationReference,
                out var deliveryTerms,
                out var offeredDeliveryLeadTime,
                out var notes,
                out var lines,
                out var evidence))
        {
            return SupplierQuotationOperationResult<SupplierQuotationRecord>.Failure("validation_failed");
        }

        var references = await ResolveReferencesAsync(
            context,
            request,
            lines,
            source.Value,
            cancellationToken);
        if (!references.Succeeded || references.Value is null)
        {
            return SupplierQuotationOperationResult<SupplierQuotationRecord>.Failure(references.Code);
        }

        var occurredAt = DateTimeOffset.UtcNow;
        var command = new SupplierQuotationEditCommand(
            quotationId,
            source.Value.Id,
            source.Value.Scope,
            references.Value.Supplier,
            quotationReference!,
            request.OfferDate,
            request.ValidUntil,
            references.Value.Currency,
            references.Value.PaymentTerm,
            deliveryTerms,
            request.OfferedDeliveryDate,
            offeredDeliveryLeadTime,
            notes,
            references.Value.Lines,
            ToEvidenceReferences(evidence, context.ActorId, occurredAt),
            expectedVersion,
            occurredAt,
            idempotencyKey);
        var audit = CreateAuditEvidence(
            context,
            command,
            "procurement.quotation.edit",
            current.Value.Status,
            current.Value.Status,
            reason: null,
            idempotencyKey,
            $"{current.Value.Status};lines={current.Value.Lines.Count}",
            $"{current.Value.Status};lines={references.Value.Lines.Count}");
        return ToOperationResult(await persistence.EditAsync(
            context.TenantContext,
            command,
            audit,
            cancellationToken));
    }

    public async Task<SupplierQuotationOperationResult<SupplierQuotationRecord>> SubmitAsync(
        ProcurementRequestContext context,
        Guid quotationId,
        byte[] expectedVersion,
        string? idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        var current = await FindQuotationAsync(context, quotationId, cancellationToken);
        if (!current.Succeeded || current.Value is null)
        {
            return current;
        }

        var submitAuthorization = authorization.Authorize(
            context,
            "procurement.quotation.submit",
            current.Value.Scope);
        if (!submitAuthorization.Allowed)
        {
            return SupplierQuotationOperationResult<SupplierQuotationRecord>.Failure(submitAuthorization.Code);
        }

        var source = await GetApprovedPurchaseRequestAsync(
            context,
            current.Value.PurchaseRequestId,
            "procurement.quotation.submit",
            cancellationToken);
        if (!source.Succeeded || source.Value is null)
        {
            return SupplierQuotationOperationResult<SupplierQuotationRecord>.Failure(source.Code);
        }

        if (current.Value.Status != SupplierQuotationStatus.Draft)
        {
            return SupplierQuotationOperationResult<SupplierQuotationRecord>.Failure("submit_not_allowed");
        }

        var activeReferences = await ValidateActiveReferencesAsync(
            context,
            current.Value,
            cancellationToken);
        if (!activeReferences.Succeeded)
        {
            return SupplierQuotationOperationResult<SupplierQuotationRecord>.Failure(activeReferences.Code);
        }

        var command = new SupplierQuotationActionCommand(
            quotationId,
            expectedVersion,
            context.ActorId,
            null,
            DateTimeOffset.UtcNow,
            idempotencyKey);
        var audit = CreateAuditEvidence(
            context,
            current.Value,
            "procurement.quotation.submit",
            current.Value.Status,
            SupplierQuotationStatus.Submitted,
            reason: null,
            idempotencyKey,
            current.Value.Status.ToString(),
            SupplierQuotationStatus.Submitted.ToString());
        return ToOperationResult(await persistence.SubmitAsync(
            context.TenantContext,
            command,
            audit,
            cancellationToken));
    }

    public Task<SupplierQuotationOperationResult<SupplierQuotationRecord>> WithdrawAsync(
        ProcurementRequestContext context,
        Guid quotationId,
        byte[] expectedVersion,
        string? reason,
        string? idempotencyKey,
        CancellationToken cancellationToken = default) =>
        ApplyActionAsync(
            context,
            quotationId,
            expectedVersion,
            reason,
            idempotencyKey,
            "procurement.quotation.withdraw",
            SupplierQuotationStatus.Withdrawn,
            cancellationToken);

    public Task<SupplierQuotationOperationResult<SupplierQuotationRecord>> DisqualifyAsync(
        ProcurementRequestContext context,
        Guid quotationId,
        byte[] expectedVersion,
        string? reason,
        string? idempotencyKey,
        CancellationToken cancellationToken = default) =>
        ApplyActionAsync(
            context,
            quotationId,
            expectedVersion,
            reason,
            idempotencyKey,
            "procurement.quotation.disqualify",
            SupplierQuotationStatus.Disqualified,
            cancellationToken);

    public async Task<SupplierQuotationOperationResult<IReadOnlyList<SupplierQuotationHistoryRecord>>> ReadHistoryAsync(
        ProcurementRequestContext context,
        Guid quotationId,
        CancellationToken cancellationToken = default)
    {
        var current = await FindQuotationAsync(context, quotationId, cancellationToken);
        if (!current.Succeeded || current.Value is null)
        {
            return SupplierQuotationOperationResult<IReadOnlyList<SupplierQuotationHistoryRecord>>.Failure(current.Code);
        }

        var historyAuthorization = authorization.Authorize(
            context,
            "procurement.quotation.history.read",
            current.Value.Scope);
        if (!historyAuthorization.Allowed)
        {
            return SupplierQuotationOperationResult<IReadOnlyList<SupplierQuotationHistoryRecord>>.Failure(historyAuthorization.Code);
        }

        try
        {
            return SupplierQuotationOperationResult<IReadOnlyList<SupplierQuotationHistoryRecord>>.Success(
                await persistence.ReadHistoryAsync(context.TenantContext, quotationId, cancellationToken));
        }
        catch
        {
            return SupplierQuotationOperationResult<IReadOnlyList<SupplierQuotationHistoryRecord>>.Failure("persistence_unavailable");
        }
    }

    public async Task<SupplierQuotationOperationResult<IReadOnlyList<SupplierQuotationAuditRecord>>> ReadAuditAsync(
        ProcurementRequestContext context,
        Guid quotationId,
        CancellationToken cancellationToken = default)
    {
        var current = await FindQuotationAsync(context, quotationId, cancellationToken);
        if (!current.Succeeded || current.Value is null)
        {
            return SupplierQuotationOperationResult<IReadOnlyList<SupplierQuotationAuditRecord>>.Failure(current.Code);
        }

        var auditAuthorization = authorization.Authorize(
            context,
            "procurement.quotation.audit.read",
            current.Value.Scope);
        if (!auditAuthorization.Allowed)
        {
            return SupplierQuotationOperationResult<IReadOnlyList<SupplierQuotationAuditRecord>>.Failure(auditAuthorization.Code);
        }

        try
        {
            return SupplierQuotationOperationResult<IReadOnlyList<SupplierQuotationAuditRecord>>.Success(
                await persistence.ReadAuditAsync(context.TenantContext, quotationId, cancellationToken));
        }
        catch
        {
            return SupplierQuotationOperationResult<IReadOnlyList<SupplierQuotationAuditRecord>>.Failure("persistence_unavailable");
        }
    }

    public async Task<SupplierQuotationOperationResult<SupplierQuotationComparisonModel>> CompareAsync(
        ProcurementRequestContext context,
        Guid purchaseRequestId,
        CancellationToken cancellationToken = default)
    {
        var source = await GetPurchaseRequestAsync(
            context,
            purchaseRequestId,
            "procurement.quotation.compare",
            cancellationToken);
        if (!source.Succeeded || source.Value is null)
        {
            return SupplierQuotationOperationResult<SupplierQuotationComparisonModel>.Failure(source.Code);
        }

        IReadOnlyList<SupplierQuotationRecord> quotations;
        SupplierSourceDecisionRecord? decision;
        try
        {
            quotations = await persistence.ListAsync(context.TenantContext, purchaseRequestId, cancellationToken);
            decision = await persistence.FindSourceDecisionAsync(context.TenantContext, purchaseRequestId, cancellationToken);
        }
        catch
        {
            return SupplierQuotationOperationResult<SupplierQuotationComparisonModel>.Failure("persistence_unavailable");
        }

        return SupplierQuotationOperationResult<SupplierQuotationComparisonModel>.Success(
            BuildComparison(source.Value, quotations, decision));
    }

    public async Task<SupplierQuotationOperationResult<SupplierSourceDecisionRecord>> ReadSourceDecisionAsync(
        ProcurementRequestContext context,
        Guid purchaseRequestId,
        CancellationToken cancellationToken = default)
    {
        var source = await GetPurchaseRequestAsync(
            context,
            purchaseRequestId,
            "procurement.source-decision.read",
            cancellationToken);
        if (!source.Succeeded || source.Value is null)
        {
            return SupplierQuotationOperationResult<SupplierSourceDecisionRecord>.Failure(source.Code);
        }

        try
        {
            var decision = await persistence.FindSourceDecisionAsync(
                context.TenantContext,
                purchaseRequestId,
                cancellationToken);
            return decision is null
                ? SupplierQuotationOperationResult<SupplierSourceDecisionRecord>.Failure("source_decision_not_found")
                : SupplierQuotationOperationResult<SupplierSourceDecisionRecord>.Success(decision);
        }
        catch
        {
            return SupplierQuotationOperationResult<SupplierSourceDecisionRecord>.Failure("persistence_unavailable");
        }
    }

    public async Task<SupplierQuotationOperationResult<SupplierSourceDecisionRecord>> RecordSourceDecisionAsync(
        ProcurementRequestContext context,
        Guid purchaseRequestId,
        SupplierSourceDecisionWriteRequest request,
        byte[] expectedPurchaseRequestVersion,
        string? idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(request);
        if (expectedPurchaseRequestVersion is null || expectedPurchaseRequestVersion.Length == 0)
        {
            return SupplierQuotationOperationResult<SupplierSourceDecisionRecord>.Failure("validation_failed");
        }
        if (request.SelectedQuotationId == Guid.Empty
            || !SupplierQuotationValuePolicy.TryRationale(request.Rationale, out var rationale))
        {
            return SupplierQuotationOperationResult<SupplierSourceDecisionRecord>.Failure("rationale_required");
        }

        var source = await GetApprovedPurchaseRequestAsync(
            context,
            purchaseRequestId,
            "procurement.source-decision.record",
            cancellationToken);
        if (!source.Succeeded || source.Value is null)
        {
            return SupplierQuotationOperationResult<SupplierSourceDecisionRecord>.Failure(source.Code);
        }

        SupplierQuotationRecord? selected;
        SupplierSourceDecisionRecord? existingDecision;
        IReadOnlyList<SupplierQuotationRecord> quotations;
        try
        {
            selected = await persistence.FindAsync(context.TenantContext, request.SelectedQuotationId, cancellationToken);
            quotations = await persistence.ListAsync(context.TenantContext, purchaseRequestId, cancellationToken);
            existingDecision = await persistence.FindSourceDecisionAsync(context.TenantContext, purchaseRequestId, cancellationToken);
        }
        catch
        {
            return SupplierQuotationOperationResult<SupplierSourceDecisionRecord>.Failure("persistence_unavailable");
        }

        if (selected is null || selected.PurchaseRequestId != purchaseRequestId)
        {
            return SupplierQuotationOperationResult<SupplierSourceDecisionRecord>.Failure("quotation_not_found");
        }

        var quotationAuthorization = authorization.Authorize(
            context,
            "procurement.source-decision.record",
            selected.Scope);
        if (!quotationAuthorization.Allowed)
        {
            return SupplierQuotationOperationResult<SupplierSourceDecisionRecord>.Failure(quotationAuthorization.Code);
        }

        if (selected.Status != SupplierQuotationStatus.Submitted)
        {
            return SupplierQuotationOperationResult<SupplierSourceDecisionRecord>.Failure("source_decision_not_allowed");
        }

        var activeReferences = await ValidateActiveReferencesAsync(context, selected, cancellationToken);
        if (!activeReferences.Succeeded)
        {
            return SupplierQuotationOperationResult<SupplierSourceDecisionRecord>.Failure(activeReferences.Code);
        }

        var comparison = BuildComparison(source.Value, quotations, decision: null);
        var snapshotJson = JsonSerializer.Serialize(comparison, JsonOptions);
        var snapshotReference = "sha256:" + Convert.ToHexString(
            SHA256.HashData(Encoding.UTF8.GetBytes(snapshotJson))).ToLowerInvariant();
        var policy = source.Value.ApprovalPolicy;
        var lastPolicyStage = policy?.Stages.OrderBy(item => item.Sequence).LastOrDefault();
        var command = new SupplierSourceDecisionCommand(
            Guid.NewGuid(),
            purchaseRequestId,
            source.Value.Scope,
            selected.Id,
            context.ActorId,
            DateTimeOffset.UtcNow,
            rationale,
            policy?.PolicyId,
            policy?.Version,
            lastPolicyStage?.StageKey,
            snapshotReference,
            snapshotJson,
            existingDecision?.Version ?? expectedPurchaseRequestVersion,
            idempotencyKey);
        var audit = CreateAuditEvidence(
            context,
            selected,
            "procurement.source-decision.record",
            selected.Status,
            selected.Status,
            rationale,
            idempotencyKey,
            existingDecision is null ? "source:none" : $"source:{existingDecision.SelectedQuotationId:D}",
            $"source:{selected.Id:D}");
        return ToOperationResult(await persistence.RecordSourceDecisionAsync(
            context.TenantContext,
            command,
            audit,
            cancellationToken));
    }

    private async Task<SupplierQuotationOperationResult<SupplierQuotationRecord>> ApplyActionAsync(
        ProcurementRequestContext context,
        Guid quotationId,
        byte[] expectedVersion,
        string? reason,
        string? idempotencyKey,
        string operationId,
        SupplierQuotationStatus targetStatus,
        CancellationToken cancellationToken)
    {
        if (expectedVersion is null || expectedVersion.Length == 0)
        {
            return SupplierQuotationOperationResult<SupplierQuotationRecord>.Failure("validation_failed");
        }

        var current = await FindQuotationAsync(context, quotationId, cancellationToken);
        if (!current.Succeeded || current.Value is null)
        {
            return current;
        }

        var actionAuthorization = authorization.Authorize(
            context,
            operationId,
            current.Value.Scope);
        if (!actionAuthorization.Allowed)
        {
            return SupplierQuotationOperationResult<SupplierQuotationRecord>.Failure(actionAuthorization.Code);
        }

        var source = await GetApprovedPurchaseRequestAsync(
            context,
            current.Value.PurchaseRequestId,
            operationId,
            cancellationToken);
        if (!source.Succeeded || source.Value is null)
        {
            return SupplierQuotationOperationResult<SupplierQuotationRecord>.Failure(source.Code);
        }

        if (current.Value.Status != SupplierQuotationStatus.Submitted)
        {
            return SupplierQuotationOperationResult<SupplierQuotationRecord>.Failure("action_not_allowed");
        }

        try
        {
            var sourceDecision = await persistence.FindSourceDecisionAsync(
                context.TenantContext,
                current.Value.PurchaseRequestId,
                cancellationToken);
            if (sourceDecision?.SelectedQuotationId == current.Value.Id)
            {
                return SupplierQuotationOperationResult<SupplierQuotationRecord>.Failure("action_not_allowed");
            }
        }
        catch
        {
            return SupplierQuotationOperationResult<SupplierQuotationRecord>.Failure("persistence_unavailable");
        }

        if (!SupplierQuotationValuePolicy.TryRationale(reason, out var rationale))
        {
            return SupplierQuotationOperationResult<SupplierQuotationRecord>.Failure("reason_required");
        }

        var command = new SupplierQuotationActionCommand(
            quotationId,
            expectedVersion,
            context.ActorId,
            rationale,
            DateTimeOffset.UtcNow,
            idempotencyKey);
        var audit = CreateAuditEvidence(
            context,
            current.Value,
            operationId,
            current.Value.Status,
            targetStatus,
            rationale,
            idempotencyKey,
            current.Value.Status.ToString(),
            targetStatus.ToString());
        var result = targetStatus == SupplierQuotationStatus.Withdrawn
            ? await persistence.WithdrawAsync(context.TenantContext, command, audit, cancellationToken)
            : await persistence.DisqualifyAsync(context.TenantContext, command, audit, cancellationToken);
        return ToOperationResult(result);
    }

    private async Task<SupplierQuotationOperationResult<PurchaseRequestRecord>> GetApprovedPurchaseRequestAsync(
        ProcurementRequestContext context,
        Guid purchaseRequestId,
        string operationId,
        CancellationToken cancellationToken)
    {
        var request = await GetPurchaseRequestAsync(context, purchaseRequestId, operationId, cancellationToken);
        if (!request.Succeeded || request.Value is null)
        {
            return request;
        }

        return request.Value.Status == PurchaseRequestStatus.Approved
            ? request
            : SupplierQuotationOperationResult<PurchaseRequestRecord>.Failure("purchase_request_not_approved");
    }

    private async Task<SupplierQuotationOperationResult<PurchaseRequestRecord>> GetPurchaseRequestAsync(
        ProcurementRequestContext context,
        Guid purchaseRequestId,
        string operationId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (purchaseRequestId == Guid.Empty)
        {
            return SupplierQuotationOperationResult<PurchaseRequestRecord>.Failure("validation_failed");
        }

        PurchaseRequestRecord? record;
        try
        {
            record = await purchaseRequests.FindAsync(
                context.TenantContext,
                purchaseRequestId,
                cancellationToken);
        }
        catch
        {
            return SupplierQuotationOperationResult<PurchaseRequestRecord>.Failure("persistence_unavailable");
        }

        if (record is null)
        {
            return SupplierQuotationOperationResult<PurchaseRequestRecord>.Failure("purchase_request_not_found");
        }

        var authorized = authorization.Authorize(context, operationId, record.Scope);
        return authorized.Allowed
            ? SupplierQuotationOperationResult<PurchaseRequestRecord>.Success(record)
            : SupplierQuotationOperationResult<PurchaseRequestRecord>.Failure(authorized.Code);
    }

    private async Task<SupplierQuotationOperationResult<SupplierQuotationRecord>> FindQuotationAsync(
        ProcurementRequestContext context,
        Guid quotationId,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(context);
        if (quotationId == Guid.Empty)
        {
            return SupplierQuotationOperationResult<SupplierQuotationRecord>.Failure("validation_failed");
        }

        try
        {
            var record = await persistence.FindAsync(context.TenantContext, quotationId, cancellationToken);
            if (record is not null)
            {
                var decision = await persistence.FindSourceDecisionAsync(
                    context.TenantContext,
                    record.PurchaseRequestId,
                    cancellationToken);
                record = record with
                {
                    IsSelected = decision?.SelectedQuotationId == record.Id
                };
            }
            return record is null
                ? SupplierQuotationOperationResult<SupplierQuotationRecord>.Failure("quotation_not_found")
                : SupplierQuotationOperationResult<SupplierQuotationRecord>.Success(record);
        }
        catch
        {
            return SupplierQuotationOperationResult<SupplierQuotationRecord>.Failure("persistence_unavailable");
        }
    }

    private async Task<SupplierQuotationOperationResult<ResolvedQuotationReferences>> ResolveReferencesAsync(
        ProcurementRequestContext context,
        SupplierQuotationWriteRequest request,
        IReadOnlyList<SupplierQuotationLineWriteRequest> lines,
        PurchaseRequestRecord source,
        CancellationToken cancellationToken)
    {
        try
        {
            var supplier = await suppliers.FindSupplierAsync(
                context.TenantContext,
                request.SupplierId,
                cancellationToken);
            if (supplier is null)
            {
                return SupplierQuotationOperationResult<ResolvedQuotationReferences>.Failure("supplier_not_found");
            }

            if (supplier.TenantId.Value != context.TenantId.Value)
            {
                return SupplierQuotationOperationResult<ResolvedQuotationReferences>.Failure("supplier_not_found");
            }

            if (supplier.LifecycleState != MasterDataLifecycleState.Active)
            {
                return SupplierQuotationOperationResult<ResolvedQuotationReferences>.Failure("supplier_inactive");
            }

            var currency = await currenciesAndPaymentTerms.FindCurrencyAsync(
                context.TenantContext,
                request.CurrencyId,
                cancellationToken);
            if (currency is null || currency.TenantId.Value != context.TenantId.Value)
            {
                return SupplierQuotationOperationResult<ResolvedQuotationReferences>.Failure("currency_not_found");
            }

            if (currency.LifecycleState != MasterDataLifecycleState.Active)
            {
                return SupplierQuotationOperationResult<ResolvedQuotationReferences>.Failure("currency_inactive");
            }

            SupplierQuotationPaymentTermSnapshot? paymentTerm = null;
            if (request.PaymentTermId is { } paymentTermId)
            {
                var payment = await currenciesAndPaymentTerms.FindPaymentTermAsync(
                    context.TenantContext,
                    paymentTermId,
                    cancellationToken);
                if (payment is null || payment.TenantId.Value != context.TenantId.Value)
                {
                    return SupplierQuotationOperationResult<ResolvedQuotationReferences>.Failure("payment_term_not_found");
                }

                if (payment.LifecycleState != MasterDataLifecycleState.Active)
                {
                    return SupplierQuotationOperationResult<ResolvedQuotationReferences>.Failure("payment_term_inactive");
                }

                paymentTerm = new SupplierQuotationPaymentTermSnapshot(
                    payment.Id,
                    payment.Code,
                    payment.Name.English ?? payment.Name.Arabic ?? payment.Code,
                    payment.CurrentVersionNumber);
            }

            var purchaseRequestLines = source.Lines.ToDictionary(item => item.Id);
            var snapshots = new List<SupplierQuotationLineSnapshot>(lines.Count);
            foreach (var line in lines)
            {
                if (!purchaseRequestLines.TryGetValue(line.PurchaseRequestLineId, out var sourceLine))
                {
                    return SupplierQuotationOperationResult<ResolvedQuotationReferences>.Failure("purchase_request_line_not_found");
                }

                var gross = line.QuotedQuantity * line.UnitPrice;
                if (line.DiscountAmount is { } discountAmount && discountAmount > gross
                    || line.DiscountPercentage is { } discountPercentage && gross * discountPercentage / 100m > gross)
                {
                    return SupplierQuotationOperationResult<ResolvedQuotationReferences>.Failure("discount_exceeds_line_value");
                }

                Guid? taxId = null;
                string? taxCode = null;
                string? taxName = null;
                decimal? taxRate = line.TaxRatePercentage;
                if (line.TaxId is { } requestedTaxId)
                {
                    var tax = await taxes.FindTaxAsync(
                        context.TenantContext,
                        requestedTaxId,
                        cancellationToken);
                    if (tax is null || tax.TenantId.Value != context.TenantId.Value)
                    {
                        return SupplierQuotationOperationResult<ResolvedQuotationReferences>.Failure("tax_not_found");
                    }

                    if (tax.LifecycleState != MasterDataLifecycleState.Active)
                    {
                        return SupplierQuotationOperationResult<ResolvedQuotationReferences>.Failure("tax_inactive");
                    }

                    var currentRate = tax.RateVersions
                        .OrderByDescending(item => item.VersionNumber)
                        .FirstOrDefault();
                    taxId = tax.Id;
                    taxCode = tax.Code;
                    taxName = tax.Name.English ?? tax.Name.Arabic ?? tax.Code;
                    taxRate ??= currentRate?.RatePercentage;
                }

                snapshots.Add(new SupplierQuotationLineSnapshot(
                    Guid.NewGuid(),
                    sourceLine.Id,
                    sourceLine.ProductId,
                    sourceLine.ProductSku,
                    sourceLine.ProductName,
                    sourceLine.UnitOfMeasureId,
                    sourceLine.UnitOfMeasureCode,
                    sourceLine.Quantity,
                    line.QuotedQuantity,
                    line.UnitPrice,
                    line.DiscountAmount,
                    line.DiscountPercentage,
                    taxId,
                    taxCode,
                    taxName,
                    taxRate,
                    line.TaxAmount,
                    line.TaxReference,
                    sourceLine.NeedByDate,
                    line.OfferedDeliveryDate,
                    line.OfferedDeliveryLeadTime,
                    line.Notes));
            }

            return SupplierQuotationOperationResult<ResolvedQuotationReferences>.Success(
                new ResolvedQuotationReferences(
                    new SupplierQuotationSupplierSnapshot(
                        supplier.Id,
                        supplier.Code,
                        supplier.LegalName.English ?? supplier.LegalName.Arabic ?? supplier.Code),
                    new SupplierQuotationCurrencySnapshot(
                        currency.Id,
                        currency.Code,
                        currency.Name.English ?? currency.Name.Arabic ?? currency.Code),
                    paymentTerm,
                    snapshots));
        }
        catch
        {
            return SupplierQuotationOperationResult<ResolvedQuotationReferences>.Failure("reference_persistence_unavailable");
        }
    }

    private async Task<SupplierQuotationOperationResult<bool>> ValidateActiveReferencesAsync(
        ProcurementRequestContext context,
        SupplierQuotationRecord quotation,
        CancellationToken cancellationToken)
    {
        try
        {
            var supplier = await suppliers.FindSupplierAsync(
                context.TenantContext,
                quotation.Supplier.Id,
                cancellationToken);
            if (supplier is null)
            {
                return SupplierQuotationOperationResult<bool>.Failure("supplier_not_found");
            }

            if (supplier.LifecycleState != MasterDataLifecycleState.Active)
            {
                return SupplierQuotationOperationResult<bool>.Failure("supplier_inactive");
            }

            var currency = await currenciesAndPaymentTerms.FindCurrencyAsync(
                context.TenantContext,
                quotation.Currency.Id,
                cancellationToken);
            if (currency is null)
            {
                return SupplierQuotationOperationResult<bool>.Failure("currency_not_found");
            }

            if (currency.LifecycleState != MasterDataLifecycleState.Active)
            {
                return SupplierQuotationOperationResult<bool>.Failure("currency_inactive");
            }

            if (quotation.PaymentTerm is { } paymentTerm)
            {
                var payment = await currenciesAndPaymentTerms.FindPaymentTermAsync(
                    context.TenantContext,
                    paymentTerm.Id,
                    cancellationToken);
                if (payment is null)
                {
                    return SupplierQuotationOperationResult<bool>.Failure("payment_term_not_found");
                }

                if (payment.LifecycleState != MasterDataLifecycleState.Active)
                {
                    return SupplierQuotationOperationResult<bool>.Failure("payment_term_inactive");
                }
            }

            return SupplierQuotationOperationResult<bool>.Success(true);
        }
        catch
        {
            return SupplierQuotationOperationResult<bool>.Failure("reference_persistence_unavailable");
        }
    }

    private static SupplierQuotationComparisonModel BuildComparison(
        PurchaseRequestRecord source,
        IReadOnlyList<SupplierQuotationRecord> quotations,
        SupplierSourceDecisionRecord? decision)
    {
        var orderedQuotations = quotations
            .OrderBy(item => item.Supplier.Name, StringComparer.Ordinal)
            .ThenBy(item => item.Supplier.Code, StringComparer.Ordinal)
            .ThenBy(item => item.SupplierQuotationReference, StringComparer.Ordinal)
            .ThenBy(item => item.Id)
            .ToArray();
        var currencyGroups = orderedQuotations
            .GroupBy(item => new { item.Currency.Id, item.Currency.Code })
            .OrderBy(group => group.Key.Code, StringComparer.Ordinal)
            .ThenBy(group => group.Key.Id)
            .Select(group => new SupplierQuotationCurrencyComparisonGroupModel(
                group.Key.Id,
                group.Key.Code,
                group.Select(item => item.Id).OrderBy(item => item).ToArray(),
                DirectlyComparableWithinGroup: true))
            .ToArray();
        var hasMixedCurrencies = currencyGroups.Length > 1;
        var today = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var items = orderedQuotations
            .Select(quotation => BuildComparisonItem(source, quotation, hasMixedCurrencies, today))
            .ToArray();

        return new SupplierQuotationComparisonModel(
            source.Id,
            hasMixedCurrencies,
            !hasMixedCurrencies && currencyGroups.Length > 0,
            "Same-currency commercial totals only; no FX conversion or cross-currency ranking.",
            currencyGroups,
            items,
            decision);
    }

    private static SupplierQuotationComparisonItemModel BuildComparisonItem(
        PurchaseRequestRecord source,
        SupplierQuotationRecord quotation,
        bool hasMixedCurrencies,
        DateOnly today)
    {
        var byLine = quotation.Lines.ToDictionary(item => item.PurchaseRequestLineId);
        var lines = source.Lines
            .OrderBy(item => item.Id)
            .Select(sourceLine =>
            {
                if (!byLine.TryGetValue(sourceLine.Id, out var line))
                {
                    return new SupplierQuotationComparisonLineModel(
                        sourceLine.Id,
                        sourceLine.ProductSku,
                        sourceLine.ProductName,
                        sourceLine.Quantity,
                        null,
                        null,
                        null,
                        null,
                        null,
                        null,
                        sourceLine.NeedByDate,
                        null,
                        false,
                        "missing_line");
                }

                var issue = line.QuotedQuantity < sourceLine.Quantity
                    ? "partial_quantity"
                    : null;
                return new SupplierQuotationComparisonLineModel(
                    line.PurchaseRequestLineId,
                    line.ProductSku,
                    line.ProductName,
                    line.RequestedQuantity,
                    line.QuotedQuantity,
                    line.UnitPrice,
                    line.DiscountAmount,
                    line.DiscountPercentage,
                    line.TaxRatePercentage,
                    line.TaxAmount,
                    line.RequestedNeedByDate,
                    line.OfferedDeliveryDate,
                    true,
                    issue);
            })
            .ToArray();
        var covered = lines.Count(item => item.IsCovered);
        var issues = new SortedSet<string>(StringComparer.Ordinal);
        if (quotation.Status != SupplierQuotationStatus.Submitted)
        {
            issues.Add("status_not_submitted");
        }

        if (quotation.ValidUntil is { } validUntil && validUntil < today)
        {
            issues.Add("expired");
        }

        if (covered != source.Lines.Count)
        {
            issues.Add("partial_line_coverage");
        }

        foreach (var line in lines)
        {
            if (line.QualificationIssue is { } lineIssue)
            {
                issues.Add(lineIssue);
            }
        }

        if (hasMixedCurrencies)
        {
            issues.Add("mixed_currency_no_fx_basis");
        }

        return new SupplierQuotationComparisonItemModel(
            quotation.Id,
            quotation.Supplier,
            quotation.Status,
            quotation.SupplierQuotationReference,
            quotation.OfferDate,
            quotation.ValidUntil,
            quotation.Currency,
            SupplierQuotationValuePolicy.CommercialTotal(quotation),
            covered,
            source.Lines.Count,
            quotation.Evidence.Count > 0,
            !hasMixedCurrencies,
            quotation.PaymentTerm?.Code,
            quotation.DeliveryTerms,
            quotation.OfferedDeliveryDate,
            quotation.OfferedDeliveryLeadTime,
            lines,
            issues.ToArray());
    }

    private static IReadOnlyList<SupplierQuotationEvidenceReference> ToEvidenceReferences(
        IReadOnlyList<SupplierQuotationEvidenceReferenceWriteRequest> evidence,
        Guid actorId,
        DateTimeOffset recordedAt) => evidence
        .Select(item => new SupplierQuotationEvidenceReference(
            Guid.NewGuid(),
            item.ReferenceId!,
            item.FileName,
            item.ContentType,
            item.Description,
            item.Source ?? "buyer-recorded",
            item.ExternalReference,
            actorId,
            recordedAt))
        .ToArray();

    private static SupplierQuotationAuditEvidence CreateAuditEvidence(
        ProcurementRequestContext context,
        SupplierQuotationCreateCommand command,
        string operationId,
        SupplierQuotationStatus? beforeStatus,
        SupplierQuotationStatus? afterStatus,
        string? reason,
        string? idempotencyKey,
        string? beforeSummary,
        string? afterSummary) => new(
        Guid.NewGuid(),
        command.Id,
        command.PurchaseRequestId,
        command.OccurredAt,
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
        command.Scope.CompanyId,
        command.Scope.BranchId,
        beforeSummary,
        afterSummary,
        idempotencyKey);

    private static SupplierQuotationAuditEvidence CreateAuditEvidence(
        ProcurementRequestContext context,
        SupplierQuotationEditCommand command,
        string operationId,
        SupplierQuotationStatus? beforeStatus,
        SupplierQuotationStatus? afterStatus,
        string? reason,
        string? idempotencyKey,
        string? beforeSummary,
        string? afterSummary) => new(
        Guid.NewGuid(),
        command.Id,
        command.PurchaseRequestId,
        command.OccurredAt,
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
        command.Scope.CompanyId,
        command.Scope.BranchId,
        beforeSummary,
        afterSummary,
        idempotencyKey);

    private static SupplierQuotationAuditEvidence CreateAuditEvidence(
        ProcurementRequestContext context,
        SupplierQuotationRecord quotation,
        string operationId,
        SupplierQuotationStatus? beforeStatus,
        SupplierQuotationStatus? afterStatus,
        string? reason,
        string? idempotencyKey,
        string? beforeSummary,
        string? afterSummary) => new(
        Guid.NewGuid(),
        quotation.Id,
        quotation.PurchaseRequestId,
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
        quotation.Scope.CompanyId,
        quotation.Scope.BranchId,
        beforeSummary,
        afterSummary,
        idempotencyKey);

    private static SupplierQuotationOperationResult<T> ToOperationResult<T>(
        SupplierQuotationPersistenceResult<T> result) =>
        result.Succeeded && result.Value is not null
            ? SupplierQuotationOperationResult<T>.Success(result.Value)
            : SupplierQuotationOperationResult<T>.Failure(result.Code);

    private sealed record ResolvedQuotationReferences(
        SupplierQuotationSupplierSnapshot Supplier,
        SupplierQuotationCurrencySnapshot Currency,
        SupplierQuotationPaymentTermSnapshot? PaymentTerm,
        IReadOnlyList<SupplierQuotationLineSnapshot> Lines);
}

#pragma warning restore CS1591
