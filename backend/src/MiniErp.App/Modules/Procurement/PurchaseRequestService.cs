#pragma warning disable CS1591

using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.Modules.MasterData;
using MiniErp.Contracts.Modules.Foundation;
using MiniErp.Contracts.Modules.MasterData;
using MiniErp.Contracts.Modules.Procurement;

namespace MiniErp.App.Modules.Procurement;

public sealed class PurchaseRequestService
{
    private readonly PurchaseRequestAuthorizationService authorization;
    private readonly IPurchaseRequestPersistence persistence;
    private readonly IProductIdentityPersistence products;
    private readonly IMasterDataCatalogPersistence catalog;
    private readonly IPurchaseRequestApprovalPolicyProvider policyProvider;
    private readonly IPurchaseRequestApprovalDelegationProvider delegationProvider;
    private readonly IProcurementOrganizationScopeProvider organizationScopeProvider;

    public PurchaseRequestService(
        PurchaseRequestAuthorizationService authorization,
        IPurchaseRequestPersistence persistence,
        IProductIdentityPersistence products,
        IMasterDataCatalogPersistence catalog,
        IPurchaseRequestApprovalPolicyProvider policyProvider,
        IPurchaseRequestApprovalDelegationProvider delegationProvider,
        IProcurementOrganizationScopeProvider organizationScopeProvider)
    {
        this.authorization = authorization ?? throw new ArgumentNullException(nameof(authorization));
        this.persistence = persistence ?? throw new ArgumentNullException(nameof(persistence));
        this.products = products ?? throw new ArgumentNullException(nameof(products));
        this.catalog = catalog ?? throw new ArgumentNullException(nameof(catalog));
        this.policyProvider = policyProvider ?? throw new ArgumentNullException(nameof(policyProvider));
        this.delegationProvider = delegationProvider ?? throw new ArgumentNullException(nameof(delegationProvider));
        this.organizationScopeProvider = organizationScopeProvider ?? throw new ArgumentNullException(nameof(organizationScopeProvider));
    }

    public async Task<PurchaseRequestOperationResult<IReadOnlyList<ProcurementOrganizationScopeOption>>> ListOrganizationScopesAsync(
        ProcurementRequestContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        var authorized = authorization.Authorize(context, "procurement.organization-scope.list");
        if (!authorized.Allowed)
        {
            return PurchaseRequestOperationResult<IReadOnlyList<ProcurementOrganizationScopeOption>>.Failure(authorized.Code);
        }

        try
        {
            return PurchaseRequestOperationResult<IReadOnlyList<ProcurementOrganizationScopeOption>>.Success(
                await organizationScopeProvider.ListAsync(context, cancellationToken));
        }
        catch
        {
            return PurchaseRequestOperationResult<IReadOnlyList<ProcurementOrganizationScopeOption>>.Failure("persistence_unavailable");
        }
    }

    public async Task<PurchaseRequestOperationResult<IReadOnlyList<PurchaseRequestListRecord>>> ListAsync(
        ProcurementRequestContext context,
        PurchaseRequestStatus? status,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        var authorized = authorization.Authorize(context, "procurement.purchase-request.list");
        if (!authorized.Allowed)
        {
            return PurchaseRequestOperationResult<IReadOnlyList<PurchaseRequestListRecord>>.Failure(authorized.Code);
        }

        try
        {
            return PurchaseRequestOperationResult<IReadOnlyList<PurchaseRequestListRecord>>.Success(
                await persistence.ListAsync(context.TenantContext, status, cancellationToken));
        }
        catch
        {
            return PurchaseRequestOperationResult<IReadOnlyList<PurchaseRequestListRecord>>.Failure("persistence_unavailable");
        }
    }

    public async Task<PurchaseRequestOperationResult<PurchaseRequestRecord>> GetAsync(
        ProcurementRequestContext context,
        Guid purchaseRequestId,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        var record = await FindAsync(context, purchaseRequestId, cancellationToken);
        if (!record.Succeeded || record.Value is null)
        {
            return record;
        }

        var authorized = authorization.Authorize(
            context,
            "procurement.purchase-request.read",
            record.Value.Scope);
        return authorized.Allowed
            ? record
            : PurchaseRequestOperationResult<PurchaseRequestRecord>.Failure(authorized.Code);
    }

    public async Task<PurchaseRequestOperationResult<PurchaseRequestRecord>> CreateAsync(
        ProcurementRequestContext context,
        PurchaseRequestWriteRequest request,
        string? idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(request);
        if (!PurchaseRequestValuePolicy.TryNormalize(
                request.CompanyId,
                request.BranchId,
                request.Purpose,
                request.Lines,
                out var purpose,
                out var lines))
        {
            return PurchaseRequestOperationResult<PurchaseRequestRecord>.Failure("validation_failed");
        }

        var scope = new PurchaseRequestScope(context.TenantId.Value, request.CompanyId, request.BranchId);
        var authorized = authorization.Authorize(context, "procurement.purchase-request.create", scope);
        if (!authorized.Allowed)
        {
            return PurchaseRequestOperationResult<PurchaseRequestRecord>.Failure(authorized.Code);
        }

        var snapshots = await ResolveLinesAsync(context.TenantContext, lines, cancellationToken);
        if (!snapshots.Succeeded || snapshots.Value is null)
        {
            return PurchaseRequestOperationResult<PurchaseRequestRecord>.Failure(snapshots.Code);
        }

        var id = Guid.NewGuid();
        var occurredAt = DateTimeOffset.UtcNow;
        var command = new PurchaseRequestCreateCommand(
            id,
            scope,
            context.ActorId,
            purpose,
            snapshots.Value,
            occurredAt,
            idempotencyKey);
        var evidence = CreateEvidence(
            context,
            id,
            scope,
            "procurement.purchase-request.create",
            PurchaseRequestStatus.Draft,
            beforeStatus: null,
            reason: null,
            idempotencyKey,
            beforeSummary: null,
            afterSummary: "Draft");
        return ToOperationResult(await persistence.CreateAsync(
            context.TenantContext,
            command,
            evidence,
            cancellationToken));
    }

    public async Task<PurchaseRequestOperationResult<PurchaseRequestRecord>> EditAsync(
        ProcurementRequestContext context,
        Guid purchaseRequestId,
        PurchaseRequestWriteRequest request,
        byte[] expectedVersion,
        string? idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(request);
        if (expectedVersion is null || expectedVersion.Length == 0)
        {
            return PurchaseRequestOperationResult<PurchaseRequestRecord>.Failure("validation_failed");
        }

        var current = await FindAsync(context, purchaseRequestId, cancellationToken);
        if (!current.Succeeded || current.Value is null)
        {
            return current;
        }

        var existing = current.Value;
        var existingAuthorization = authorization.Authorize(
            context,
            "procurement.purchase-request.edit",
            existing.Scope);
        if (!existingAuthorization.Allowed)
        {
            return PurchaseRequestOperationResult<PurchaseRequestRecord>.Failure(existingAuthorization.Code);
        }

        if (existing.RequesterId != context.ActorId)
        {
            return PurchaseRequestOperationResult<PurchaseRequestRecord>.Failure("requester_only");
        }

        if (existing.Status is not (PurchaseRequestStatus.Draft or PurchaseRequestStatus.ReturnedForChange))
        {
            return PurchaseRequestOperationResult<PurchaseRequestRecord>.Failure("edit_not_allowed");
        }

        if (!PurchaseRequestValuePolicy.TryNormalize(
                request.CompanyId,
                request.BranchId,
                request.Purpose,
                request.Lines,
                out var purpose,
                out var lines))
        {
            return PurchaseRequestOperationResult<PurchaseRequestRecord>.Failure("validation_failed");
        }

        var scope = new PurchaseRequestScope(context.TenantId.Value, request.CompanyId, request.BranchId);
        var targetAuthorization = authorization.Authorize(context, "procurement.purchase-request.edit", scope);
        if (!targetAuthorization.Allowed)
        {
            return PurchaseRequestOperationResult<PurchaseRequestRecord>.Failure(targetAuthorization.Code);
        }

        var snapshots = await ResolveLinesAsync(context.TenantContext, lines, cancellationToken);
        if (!snapshots.Succeeded || snapshots.Value is null)
        {
            return PurchaseRequestOperationResult<PurchaseRequestRecord>.Failure(snapshots.Code);
        }

        var command = new PurchaseRequestEditCommand(
            purchaseRequestId,
            scope,
            context.ActorId,
            purpose,
            snapshots.Value,
            expectedVersion,
            DateTimeOffset.UtcNow,
            idempotencyKey);
        var evidence = CreateEvidence(
            context,
            purchaseRequestId,
            scope,
            "procurement.purchase-request.edit",
            existing.Status,
            existing.Status,
            reason: null,
            idempotencyKey,
            $"{existing.Status};lines={existing.Lines.Count}",
            $"{existing.Status};lines={snapshots.Value.Count}");
        return ToOperationResult(await persistence.EditAsync(
            context.TenantContext,
            command,
            evidence,
            cancellationToken));
    }

    public async Task<PurchaseRequestOperationResult<PurchaseRequestRecord>> SubmitAsync(
        ProcurementRequestContext context,
        Guid purchaseRequestId,
        byte[] expectedVersion,
        string? idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        var current = await GetAuthorizedAsync(context, purchaseRequestId, "procurement.purchase-request.submit", cancellationToken);
        if (!current.Succeeded || current.Value is null)
        {
            return current;
        }

        var record = current.Value;
        if (record.RequesterId != context.ActorId)
        {
            return PurchaseRequestOperationResult<PurchaseRequestRecord>.Failure("requester_only");
        }

        if (record.Status is not (PurchaseRequestStatus.Draft or PurchaseRequestStatus.ReturnedForChange))
        {
            return PurchaseRequestOperationResult<PurchaseRequestRecord>.Failure("submit_not_allowed");
        }

        var policy = await policyProvider.ResolveAsync(
            context,
            record.Scope,
            DateTimeOffset.UtcNow,
            cancellationToken);
        if (!PurchaseRequestValuePolicy.IsValidPolicy(policy))
        {
            return PurchaseRequestOperationResult<PurchaseRequestRecord>.Failure("approval_policy_not_configured");
        }

        var command = new PurchaseRequestSubmitCommand(
            purchaseRequestId,
            expectedVersion,
            policy!,
            DateTimeOffset.UtcNow,
            idempotencyKey);
        var evidence = CreateEvidence(
            context,
            purchaseRequestId,
            record.Scope,
            "procurement.purchase-request.submit",
            PurchaseRequestStatus.PendingApproval,
            record.Status,
            reason: null,
            idempotencyKey,
            record.Status.ToString(),
            "PendingApproval");
        return ToOperationResult(await persistence.SubmitAsync(
            context.TenantContext,
            command,
            evidence,
            cancellationToken));
    }

    public Task<PurchaseRequestOperationResult<PurchaseRequestRecord>> ApproveAsync(
        ProcurementRequestContext context,
        Guid purchaseRequestId,
        byte[] expectedVersion,
        string? idempotencyKey,
        CancellationToken cancellationToken = default) =>
        ApproveOrDecisionAsync(
            context,
            purchaseRequestId,
            expectedVersion,
            idempotencyKey,
            operationId: "procurement.purchase-request.approve",
            action: PurchaseRequestHistoryAction.ApprovalRecorded,
            reason: null,
            cancellationToken);

    public Task<PurchaseRequestOperationResult<PurchaseRequestRecord>> RejectAsync(
        ProcurementRequestContext context,
        Guid purchaseRequestId,
        byte[] expectedVersion,
        string? reason,
        string? idempotencyKey,
        CancellationToken cancellationToken = default) =>
        ApproveOrDecisionAsync(
            context,
            purchaseRequestId,
            expectedVersion,
            idempotencyKey,
            operationId: "procurement.purchase-request.reject",
            action: PurchaseRequestHistoryAction.Rejected,
            reason,
            cancellationToken);

    public Task<PurchaseRequestOperationResult<PurchaseRequestRecord>> ReturnForChangeAsync(
        ProcurementRequestContext context,
        Guid purchaseRequestId,
        byte[] expectedVersion,
        string? reason,
        string? idempotencyKey,
        CancellationToken cancellationToken = default) =>
        ApproveOrDecisionAsync(
            context,
            purchaseRequestId,
            expectedVersion,
            idempotencyKey,
            operationId: "procurement.purchase-request.return-for-change",
            action: PurchaseRequestHistoryAction.ReturnedForChange,
            reason,
            cancellationToken);

    public async Task<PurchaseRequestOperationResult<PurchaseRequestRecord>> CancelAsync(
        ProcurementRequestContext context,
        Guid purchaseRequestId,
        byte[] expectedVersion,
        string? reason,
        string? idempotencyKey,
        CancellationToken cancellationToken = default)
    {
        var current = await GetAuthorizedAsync(context, purchaseRequestId, "procurement.purchase-request.cancel", cancellationToken);
        if (!current.Succeeded || current.Value is null)
        {
            return current;
        }

        var record = current.Value;
        if (record.RequesterId != context.ActorId)
        {
            return PurchaseRequestOperationResult<PurchaseRequestRecord>.Failure("requester_only");
        }

        var allowed = record.Status is PurchaseRequestStatus.Draft or PurchaseRequestStatus.ReturnedForChange
            || record.Status == PurchaseRequestStatus.PendingApproval
                && record.ApprovalPolicy?.AllowRequesterCancellationWhilePending == true;
        if (!allowed)
        {
            return PurchaseRequestOperationResult<PurchaseRequestRecord>.Failure("cancel_not_allowed");
        }

        if (!PurchaseRequestValuePolicy.TryText(reason, 2048, allowEmpty: true, out var normalizedReason))
        {
            return PurchaseRequestOperationResult<PurchaseRequestRecord>.Failure("validation_failed");
        }

        var command = new PurchaseRequestActionCommand(
            purchaseRequestId,
            expectedVersion,
            context.ActorId,
            normalizedReason,
            DateTimeOffset.UtcNow,
            idempotencyKey);
        var evidence = CreateEvidence(
            context,
            purchaseRequestId,
            record.Scope,
            "procurement.purchase-request.cancel",
            PurchaseRequestStatus.Cancelled,
            record.Status,
            normalizedReason,
            idempotencyKey,
            record.Status.ToString(),
            "Cancelled");
        return ToOperationResult(await persistence.CancelAsync(
            context.TenantContext,
            command,
            evidence,
            cancellationToken));
    }

    public async Task<PurchaseRequestOperationResult<IReadOnlyList<PurchaseRequestHistoryRecord>>> ReadHistoryAsync(
        ProcurementRequestContext context,
        Guid purchaseRequestId,
        CancellationToken cancellationToken = default)
    {
        var current = await GetAuthorizedAsync(context, purchaseRequestId, "procurement.purchase-request.history.read", cancellationToken);
        if (!current.Succeeded || current.Value is null)
        {
            return PurchaseRequestOperationResult<IReadOnlyList<PurchaseRequestHistoryRecord>>.Failure(current.Code);
        }

        try
        {
            return PurchaseRequestOperationResult<IReadOnlyList<PurchaseRequestHistoryRecord>>.Success(
                await persistence.ReadHistoryAsync(context.TenantContext, purchaseRequestId, cancellationToken));
        }
        catch
        {
            return PurchaseRequestOperationResult<IReadOnlyList<PurchaseRequestHistoryRecord>>.Failure("persistence_unavailable");
        }
    }

    public async Task<PurchaseRequestOperationResult<IReadOnlyList<PurchaseRequestAuditRecord>>> ReadAuditAsync(
        ProcurementRequestContext context,
        Guid purchaseRequestId,
        CancellationToken cancellationToken = default)
    {
        var current = await GetAuthorizedAsync(context, purchaseRequestId, "procurement.purchase-request.audit.read", cancellationToken);
        if (!current.Succeeded || current.Value is null)
        {
            return PurchaseRequestOperationResult<IReadOnlyList<PurchaseRequestAuditRecord>>.Failure(current.Code);
        }

        try
        {
            return PurchaseRequestOperationResult<IReadOnlyList<PurchaseRequestAuditRecord>>.Success(
                await persistence.ReadAuditAsync(context.TenantContext, purchaseRequestId, cancellationToken));
        }
        catch
        {
            return PurchaseRequestOperationResult<IReadOnlyList<PurchaseRequestAuditRecord>>.Failure("persistence_unavailable");
        }
    }

    private async Task<PurchaseRequestOperationResult<PurchaseRequestRecord>> ApproveOrDecisionAsync(
        ProcurementRequestContext context,
        Guid purchaseRequestId,
        byte[] expectedVersion,
        string? idempotencyKey,
        string operationId,
        PurchaseRequestHistoryAction action,
        string? reason,
        CancellationToken cancellationToken)
    {
        var current = await GetAuthorizedAsync(context, purchaseRequestId, operationId, cancellationToken);
        if (!current.Succeeded || current.Value is null)
        {
            return current;
        }

        var record = current.Value;
        if (record.Status != PurchaseRequestStatus.PendingApproval)
        {
            return PurchaseRequestOperationResult<PurchaseRequestRecord>.Failure("decision_not_allowed");
        }

        if (record.RequesterId == context.ActorId)
        {
            return PurchaseRequestOperationResult<PurchaseRequestRecord>.Failure("self_approval_denied");
        }

        if (action is PurchaseRequestHistoryAction.Rejected or PurchaseRequestHistoryAction.ReturnedForChange
            && !PurchaseRequestValuePolicy.TryText(reason, 2048, allowEmpty: false, out reason))
        {
            return PurchaseRequestOperationResult<PurchaseRequestRecord>.Failure("reason_required");
        }

        var stage = CurrentStage(record);
        if (stage is null)
        {
            return PurchaseRequestOperationResult<PurchaseRequestRecord>.Failure("approval_policy_invalid");
        }

        var (eligible, delegatedFrom) = await ResolveApprovalActorAsync(
            context,
            record.Scope,
            stage,
            record.RequesterId,
            cancellationToken);
        if (!eligible)
        {
            return PurchaseRequestOperationResult<PurchaseRequestRecord>.Failure("approval_not_eligible");
        }

        var normalizedReason = reason?.Trim();
        var evidence = CreateEvidence(
            context,
            purchaseRequestId,
            record.Scope,
            operationId,
            action == PurchaseRequestHistoryAction.ApprovalRecorded
                ? null
                : action == PurchaseRequestHistoryAction.Rejected
                    ? PurchaseRequestStatus.Rejected
                    : PurchaseRequestStatus.ReturnedForChange,
            record.Status,
            normalizedReason,
            idempotencyKey,
            record.Status.ToString(),
            action == PurchaseRequestHistoryAction.ApprovalRecorded ? "ApprovalRecorded" : action.ToString());

        if (action == PurchaseRequestHistoryAction.ApprovalRecorded)
        {
            var approval = new PurchaseRequestApprovalCommand(
                purchaseRequestId,
                expectedVersion,
                context.ActorId,
                delegatedFrom,
                DateTimeOffset.UtcNow,
                idempotencyKey);
            return ToOperationResult(await persistence.ApproveAsync(
                context.TenantContext,
                approval,
                evidence,
                cancellationToken));
        }

        var decision = new PurchaseRequestActionCommand(
            purchaseRequestId,
            expectedVersion,
            context.ActorId,
            normalizedReason,
            DateTimeOffset.UtcNow,
            idempotencyKey);
        var result = action == PurchaseRequestHistoryAction.Rejected
            ? await persistence.RejectAsync(context.TenantContext, decision, evidence, cancellationToken)
            : await persistence.ReturnForChangeAsync(context.TenantContext, decision, evidence, cancellationToken);
        return ToOperationResult(result);
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

        var delegation = await delegationProvider.ResolveAsync(
            context,
            scope,
            stage,
            context.ActorId,
            DateTimeOffset.UtcNow,
            cancellationToken);
        return delegation is { } value
            && value.DelegatorId != requesterId
            && value.DelegateeId == context.ActorId
            ? (true, value.DelegatorId)
            : (false, null);
    }

    private async Task<PurchaseRequestOperationResult<PurchaseRequestRecord>> GetAuthorizedAsync(
        ProcurementRequestContext context,
        Guid purchaseRequestId,
        string operationId,
        CancellationToken cancellationToken)
    {
        var current = await FindAsync(context, purchaseRequestId, cancellationToken);
        if (!current.Succeeded || current.Value is null)
        {
            return current;
        }

        var authorized = authorization.Authorize(context, operationId, current.Value.Scope);
        return authorized.Allowed
            ? current
            : PurchaseRequestOperationResult<PurchaseRequestRecord>.Failure(authorized.Code);
    }

    private async Task<PurchaseRequestOperationResult<PurchaseRequestRecord>> FindAsync(
        ProcurementRequestContext context,
        Guid purchaseRequestId,
        CancellationToken cancellationToken)
    {
        if (purchaseRequestId == Guid.Empty)
        {
            return PurchaseRequestOperationResult<PurchaseRequestRecord>.Failure("validation_failed");
        }

        try
        {
            var record = await persistence.FindAsync(context.TenantContext, purchaseRequestId, cancellationToken);
            return record is null
                ? PurchaseRequestOperationResult<PurchaseRequestRecord>.Failure("purchase_request_not_found")
                : PurchaseRequestOperationResult<PurchaseRequestRecord>.Success(record);
        }
        catch
        {
            return PurchaseRequestOperationResult<PurchaseRequestRecord>.Failure("persistence_unavailable");
        }
    }

    private async Task<PurchaseRequestOperationResult<IReadOnlyList<PurchaseRequestLineSnapshot>>> ResolveLinesAsync(
        TenantContext tenantContext,
        IReadOnlyList<PurchaseRequestLineWriteRequest> lines,
        CancellationToken cancellationToken)
    {
        var snapshots = new List<PurchaseRequestLineSnapshot>(lines.Count);
        foreach (var line in lines)
        {
            ProductIdentityRecord? product;
            MasterDataUnitOfMeasureRecord? unit;
            try
            {
                product = await products.FindProductAsync(tenantContext, line.ProductId, cancellationToken);
                unit = await catalog.FindUnitOfMeasureAsync(tenantContext, line.UnitOfMeasureId, cancellationToken);
            }
            catch
            {
                return PurchaseRequestOperationResult<IReadOnlyList<PurchaseRequestLineSnapshot>>.Failure("reference_persistence_unavailable");
            }

            if (product is null)
            {
                return PurchaseRequestOperationResult<IReadOnlyList<PurchaseRequestLineSnapshot>>.Failure("product_not_found");
            }

            if (product.LifecycleState != MasterDataLifecycleState.Active)
            {
                return PurchaseRequestOperationResult<IReadOnlyList<PurchaseRequestLineSnapshot>>.Failure("product_inactive");
            }

            if (!product.IsPurchasable)
            {
                return PurchaseRequestOperationResult<IReadOnlyList<PurchaseRequestLineSnapshot>>.Failure("product_not_purchasable");
            }

            if (unit is null)
            {
                return PurchaseRequestOperationResult<IReadOnlyList<PurchaseRequestLineSnapshot>>.Failure("uom_not_found");
            }

            if (unit.LifecycleState != MasterDataLifecycleState.Active)
            {
                return PurchaseRequestOperationResult<IReadOnlyList<PurchaseRequestLineSnapshot>>.Failure("uom_inactive");
            }

            snapshots.Add(new PurchaseRequestLineSnapshot(
                Guid.NewGuid(),
                product.Id,
                product.Sku,
                product.Name.English ?? product.Name.Arabic ?? product.Sku,
                unit.Id,
                unit.Code,
                line.Quantity,
                line.NeedByDate,
                line.Purpose!.Trim()));
        }

        return PurchaseRequestOperationResult<IReadOnlyList<PurchaseRequestLineSnapshot>>.Success(snapshots);
    }

    private static PurchaseRequestApprovalStageDefinition? CurrentStage(PurchaseRequestRecord record)
    {
        if (record.ApprovalPolicy is null)
        {
            return null;
        }

        return record.ApprovalPolicy.Stages
            .OrderBy(item => item.Sequence)
            .ElementAtOrDefault(record.CurrentApprovalStageIndex);
    }

    private static PurchaseRequestAuditEvidence CreateEvidence(
        ProcurementRequestContext context,
        Guid requestId,
        PurchaseRequestScope scope,
        string operationId,
        PurchaseRequestStatus? afterStatus,
        PurchaseRequestStatus? beforeStatus,
        string? reason,
        string? idempotencyKey,
        string? beforeSummary,
        string? afterSummary) => new(
        Guid.NewGuid(),
        requestId,
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
        idempotencyKey);

    private static PurchaseRequestOperationResult<PurchaseRequestRecord> ToOperationResult(
        PurchaseRequestPersistenceResult<PurchaseRequestRecord> result) =>
        result.Succeeded && result.Value is not null
            ? PurchaseRequestOperationResult<PurchaseRequestRecord>.Success(result.Value)
            : PurchaseRequestOperationResult<PurchaseRequestRecord>.Failure(result.Code);
}

#pragma warning restore CS1591
