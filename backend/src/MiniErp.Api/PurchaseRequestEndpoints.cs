#pragma warning disable CS1591

using System.Text.Json;
using Microsoft.AspNetCore.Antiforgery;
using MiniErp.App.BuildingBlocks.Rest;
using MiniErp.App.Modules.Audit;
using MiniErp.App.Modules.Procurement;
using MiniErp.Contracts.Modules.Audit;
using MiniErp.Contracts.Modules.Foundation;
using MiniErp.Contracts.Modules.Procurement;

namespace MiniErp.Api;

public static class PurchaseRequestEndpoints
{
    public static IEndpointRouteBuilder MapPurchaseRequestEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapGet(
                "/api/v1/procurement/purchase-requests",
                async (HttpContext httpContext, ITrustedRequestContextResolver resolver, ProcurementTenantContextResolver tenantResolver, PurchaseRequestService service) =>
                {
                    PurchaseRequestStatus? status = null;
                    var parsedStatus = default(PurchaseRequestStatus);
                    var rawStatus = httpContext.Request.Query["status"].FirstOrDefault();
                    if (!string.IsNullOrWhiteSpace(rawStatus)
                        && !Enum.TryParse(rawStatus, ignoreCase: true, out parsedStatus))
                    {
                        return await WriteProblemAsync(httpContext, 400, "validation_failed", "Validation failed", "The status filter is invalid.", "procurement.purchase-request.list");
                    }

                    if (!string.IsNullOrWhiteSpace(rawStatus))
                    {
                        status = parsedStatus;
                    }

                    return await ExecuteReadAsync(
                        httpContext,
                        resolver,
                        tenantResolver,
                        FoundationOperationCatalog.GetRequired("procurement.purchase-request.list"),
                        context => service.ListAsync(context, status, httpContext.RequestAborted),
                        (_, records) => records.Select(ToListResponse).ToArray());
                })
            .WithName("procurement.purchase-request.list")
            .WithTags("Procurement / Purchase Requests")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("procurement.purchase-request.list")));

        endpoints.MapGet(
                "/api/v1/procurement/purchase-requests/{purchaseRequestId:guid}",
                async (Guid purchaseRequestId, HttpContext httpContext, ITrustedRequestContextResolver resolver, ProcurementTenantContextResolver tenantResolver, PurchaseRequestService service) =>
                    await ExecuteReadAsync(
                        httpContext,
                        resolver,
                        tenantResolver,
                        FoundationOperationCatalog.GetRequired("procurement.purchase-request.read"),
                        context => service.GetAsync(context, purchaseRequestId, httpContext.RequestAborted),
                        (context, record) => ToResponse(record, context),
                        setEtag: true))
            .WithName("procurement.purchase-request.read")
            .WithTags("Procurement / Purchase Requests")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("procurement.purchase-request.read")));

        endpoints.MapPost(
                "/api/v1/procurement/purchase-requests",
                async (PurchaseRequestWriteRequest? request, HttpContext httpContext, ITrustedRequestContextResolver resolver, ProcurementTenantContextResolver tenantResolver, FoundationAuditCoordinator auditCoordinator, LocalMasterDataIdempotencyStore idempotencyStore, PurchaseRequestService service) =>
                {
                    if (request is null)
                    {
                        return await WriteProblemAsync(httpContext, 400, "validation_failed", "Validation failed", "A Purchase Request body is required.", "procurement.purchase-request.create");
                    }

                    return await ExecuteMutationAsync(
                        httpContext,
                        resolver,
                        tenantResolver,
                        auditCoordinator,
                        idempotencyStore,
                        FoundationOperationCatalog.GetRequired("procurement.purchase-request.create"),
                        Fingerprint(request),
                        context => service.CreateAsync(context, request, GetIdempotencyKey(httpContext), httpContext.RequestAborted),
                        (context, record) => ToResponse(record, context),
                        setEtag: true);
                })
            .WithName("procurement.purchase-request.create")
            .WithTags("Procurement / Purchase Requests")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("procurement.purchase-request.create")));

        endpoints.MapPost(
                "/api/v1/procurement/purchase-requests/{purchaseRequestId:guid}/edit",
                async (Guid purchaseRequestId, PurchaseRequestWriteRequest? request, HttpContext httpContext, ITrustedRequestContextResolver resolver, ProcurementTenantContextResolver tenantResolver, FoundationAuditCoordinator auditCoordinator, LocalMasterDataIdempotencyStore idempotencyStore, PurchaseRequestService service) =>
                {
                    if (request is null || !TryReadExpectedVersion(httpContext, out var expectedVersion))
                    {
                        return await WriteProblemAsync(httpContext, 400, "validation_failed", "Validation failed", "A valid If-Match version and Purchase Request body are required.", "procurement.purchase-request.edit");
                    }

                    return await ExecuteMutationAsync(
                        httpContext,
                        resolver,
                        tenantResolver,
                        auditCoordinator,
                        idempotencyStore,
                        FoundationOperationCatalog.GetRequired("procurement.purchase-request.edit"),
                        Fingerprint(request) + VersionFingerprint(expectedVersion),
                        context => service.EditAsync(context, purchaseRequestId, request, expectedVersion, GetIdempotencyKey(httpContext), httpContext.RequestAborted),
                        (context, record) => ToResponse(record, context),
                        setEtag: true,
                        requireExpectedVersion: true);
                })
            .WithName("procurement.purchase-request.edit")
            .WithTags("Procurement / Purchase Requests")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("procurement.purchase-request.edit")));

        endpoints.MapPost(
                "/api/v1/procurement/purchase-requests/{purchaseRequestId:guid}/submit",
                async (Guid purchaseRequestId, HttpContext httpContext, ITrustedRequestContextResolver resolver, ProcurementTenantContextResolver tenantResolver, FoundationAuditCoordinator auditCoordinator, LocalMasterDataIdempotencyStore idempotencyStore, PurchaseRequestService service) =>
                    await ExecuteMutationAsyncAction(
                        purchaseRequestId,
                        httpContext,
                        resolver,
                        tenantResolver,
                        auditCoordinator,
                        idempotencyStore,
                        service,
                        FoundationOperationCatalog.GetRequired("procurement.purchase-request.submit"),
                        (context, version, key) => service.SubmitAsync(context, purchaseRequestId, version, key, httpContext.RequestAborted)))
            .WithName("procurement.purchase-request.submit")
            .WithTags("Procurement / Purchase Requests")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("procurement.purchase-request.submit")));

        endpoints.MapPost(
                "/api/v1/procurement/purchase-requests/{purchaseRequestId:guid}/approve",
                async (Guid purchaseRequestId, HttpContext httpContext, ITrustedRequestContextResolver resolver, ProcurementTenantContextResolver tenantResolver, FoundationAuditCoordinator auditCoordinator, LocalMasterDataIdempotencyStore idempotencyStore, PurchaseRequestService service) =>
                    await ExecuteMutationAsyncAction(
                        purchaseRequestId,
                        httpContext,
                        resolver,
                        tenantResolver,
                        auditCoordinator,
                        idempotencyStore,
                        service,
                        FoundationOperationCatalog.GetRequired("procurement.purchase-request.approve"),
                        (context, version, key) => service.ApproveAsync(context, purchaseRequestId, version, key, httpContext.RequestAborted)))
            .WithName("procurement.purchase-request.approve")
            .WithTags("Procurement / Purchase Requests")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("procurement.purchase-request.approve")));

        endpoints.MapPost(
                "/api/v1/procurement/purchase-requests/{purchaseRequestId:guid}/reject",
                async (Guid purchaseRequestId, PurchaseRequestActionRequest? request, HttpContext httpContext, ITrustedRequestContextResolver resolver, ProcurementTenantContextResolver tenantResolver, FoundationAuditCoordinator auditCoordinator, LocalMasterDataIdempotencyStore idempotencyStore, PurchaseRequestService service) =>
                    await ExecuteMutationAsyncAction(
                        purchaseRequestId,
                        httpContext,
                        resolver,
                        tenantResolver,
                        auditCoordinator,
                        idempotencyStore,
                        service,
                        FoundationOperationCatalog.GetRequired("procurement.purchase-request.reject"),
                        (context, version, key) => service.RejectAsync(context, purchaseRequestId, version, request?.Reason, key, httpContext.RequestAborted),
                        Fingerprint(request)))
            .WithName("procurement.purchase-request.reject")
            .WithTags("Procurement / Purchase Requests")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("procurement.purchase-request.reject")));

        endpoints.MapPost(
                "/api/v1/procurement/purchase-requests/{purchaseRequestId:guid}/return-for-change",
                async (Guid purchaseRequestId, PurchaseRequestActionRequest? request, HttpContext httpContext, ITrustedRequestContextResolver resolver, ProcurementTenantContextResolver tenantResolver, FoundationAuditCoordinator auditCoordinator, LocalMasterDataIdempotencyStore idempotencyStore, PurchaseRequestService service) =>
                    await ExecuteMutationAsyncAction(
                        purchaseRequestId,
                        httpContext,
                        resolver,
                        tenantResolver,
                        auditCoordinator,
                        idempotencyStore,
                        service,
                        FoundationOperationCatalog.GetRequired("procurement.purchase-request.return-for-change"),
                        (context, version, key) => service.ReturnForChangeAsync(context, purchaseRequestId, version, request?.Reason, key, httpContext.RequestAborted),
                        Fingerprint(request)))
            .WithName("procurement.purchase-request.return-for-change")
            .WithTags("Procurement / Purchase Requests")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("procurement.purchase-request.return-for-change")));

        endpoints.MapPost(
                "/api/v1/procurement/purchase-requests/{purchaseRequestId:guid}/cancel",
                async (Guid purchaseRequestId, PurchaseRequestActionRequest? request, HttpContext httpContext, ITrustedRequestContextResolver resolver, ProcurementTenantContextResolver tenantResolver, FoundationAuditCoordinator auditCoordinator, LocalMasterDataIdempotencyStore idempotencyStore, PurchaseRequestService service) =>
                    await ExecuteMutationAsyncAction(
                        purchaseRequestId,
                        httpContext,
                        resolver,
                        tenantResolver,
                        auditCoordinator,
                        idempotencyStore,
                        service,
                        FoundationOperationCatalog.GetRequired("procurement.purchase-request.cancel"),
                        (context, version, key) => service.CancelAsync(context, purchaseRequestId, version, request?.Reason, key, httpContext.RequestAborted),
                        Fingerprint(request)))
            .WithName("procurement.purchase-request.cancel")
            .WithTags("Procurement / Purchase Requests")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("procurement.purchase-request.cancel")));

        endpoints.MapGet(
                "/api/v1/procurement/purchase-requests/{purchaseRequestId:guid}/history",
                async (Guid purchaseRequestId, HttpContext httpContext, ITrustedRequestContextResolver resolver, ProcurementTenantContextResolver tenantResolver, PurchaseRequestService service) =>
                    await ExecuteReadAsync(
                        httpContext,
                        resolver,
                        tenantResolver,
                        FoundationOperationCatalog.GetRequired("procurement.purchase-request.history.read"),
                        context => service.ReadHistoryAsync(context, purchaseRequestId, httpContext.RequestAborted),
                        (_, records) => records.Select(ToHistoryResponse).ToArray()))
            .WithName("procurement.purchase-request.history.read")
            .WithTags("Procurement / Purchase Requests")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("procurement.purchase-request.history.read")));

        endpoints.MapGet(
                "/api/v1/procurement/purchase-requests/{purchaseRequestId:guid}/audit",
                async (Guid purchaseRequestId, HttpContext httpContext, ITrustedRequestContextResolver resolver, ProcurementTenantContextResolver tenantResolver, PurchaseRequestService service) =>
                    await ExecuteReadAsync(
                        httpContext,
                        resolver,
                        tenantResolver,
                        FoundationOperationCatalog.GetRequired("procurement.purchase-request.audit.read"),
                        context => service.ReadAuditAsync(context, purchaseRequestId, httpContext.RequestAborted),
                        (_, records) => records.Select(ToAuditResponse).ToArray()))
            .WithName("procurement.purchase-request.audit.read")
            .WithTags("Procurement / Purchase Requests")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("procurement.purchase-request.audit.read")));

        return endpoints;
    }

    private static async Task<IResult> ExecuteReadAsync<T>(
        HttpContext httpContext,
        ITrustedRequestContextResolver resolver,
        ProcurementTenantContextResolver tenantResolver,
        FoundationOperationDescriptor descriptor,
        Func<ProcurementRequestContext, Task<PurchaseRequestOperationResult<T>>> operation,
        Func<ProcurementRequestContext, T, object?> map,
        bool setEtag = false)
    {
        var foundationContext = await resolver.ResolveAsync(httpContext, httpContext.RequestAborted);
        var resolution = tenantResolver.Resolve(foundationContext);
        if (!resolution.Allowed || resolution.Context is null)
        {
            return await WriteProblemAsync(httpContext, foundationContext.SecurityProfile == FoundationSecurityProfile.Anonymous ? 401 : 403, resolution.Code, foundationContext.SecurityProfile == FoundationSecurityProfile.Anonymous ? "Authentication required" : "Access denied", "The operation is not available for this security context.", descriptor.OperationId);
        }

        var context = resolution.Context;
        var result = await operation(context);
        return ToResult(httpContext, result, descriptor.OperationId, context, map, setEtag);
    }

    private static async Task<IResult> ExecuteMutationAsyncAction(
        Guid purchaseRequestId,
        HttpContext httpContext,
        ITrustedRequestContextResolver resolver,
        ProcurementTenantContextResolver tenantResolver,
        FoundationAuditCoordinator auditCoordinator,
        LocalMasterDataIdempotencyStore idempotencyStore,
        PurchaseRequestService service,
        FoundationOperationDescriptor descriptor,
        Func<ProcurementRequestContext, byte[], string, Task<PurchaseRequestOperationResult<PurchaseRequestRecord>>> operation,
        string requestFingerprint = "")
    {
        if (!TryReadExpectedVersion(httpContext, out var expectedVersion))
        {
            return await WriteProblemAsync(httpContext, 400, "validation_failed", "Validation failed", "A valid If-Match version is required.", descriptor.OperationId);
        }

        return await ExecuteMutationAsync(
            httpContext,
            resolver,
            tenantResolver,
            auditCoordinator,
            idempotencyStore,
            descriptor,
            requestFingerprint + VersionFingerprint(expectedVersion),
            (context) => operation(context, expectedVersion, GetIdempotencyKey(httpContext)!),
            (context, record) => ToResponse(record, context),
            setEtag: true,
            requireExpectedVersion: true);
    }

    private static async Task<IResult> ExecuteMutationAsync<T>(
        HttpContext httpContext,
        ITrustedRequestContextResolver resolver,
        ProcurementTenantContextResolver tenantResolver,
        FoundationAuditCoordinator auditCoordinator,
        LocalMasterDataIdempotencyStore idempotencyStore,
        FoundationOperationDescriptor descriptor,
        string fingerprint,
        Func<ProcurementRequestContext, Task<PurchaseRequestOperationResult<T>>> operation,
        Func<ProcurementRequestContext, T, object?> map,
        bool setEtag,
        bool requireExpectedVersion = false)
    {
        if (!await EnsureAntiforgeryAsync(httpContext))
        {
            return await WriteProblemAsync(httpContext, 403, "antiforgery_failed", "Antiforgery validation failed", "The request could not be validated.", descriptor.OperationId);
        }

        if (requireExpectedVersion && !TryReadExpectedVersion(httpContext, out _))
        {
            return await WriteProblemAsync(httpContext, 400, "validation_failed", "Validation failed", "A valid If-Match version is required.", descriptor.OperationId);
        }

        var key = GetIdempotencyKey(httpContext);
        if (!FoundationCorrelation.IsValid(key))
        {
            return await WriteProblemAsync(httpContext, 400, "idempotency_key_invalid", "Invalid idempotency key", "A valid Idempotency-Key is required for this mutation.", descriptor.OperationId);
        }

        var foundationContext = await resolver.ResolveAsync(httpContext, httpContext.RequestAborted);
        var resolution = tenantResolver.Resolve(foundationContext);
        if (!resolution.Allowed || resolution.Context is null)
        {
            return await WriteProblemAsync(httpContext, foundationContext.SecurityProfile == FoundationSecurityProfile.Anonymous ? 401 : 403, resolution.Code, foundationContext.SecurityProfile == FoundationSecurityProfile.Anonymous ? "Authentication required" : "Access denied", "The operation is not available for this security context.", descriptor.OperationId);
        }

        var context = resolution.Context;
        var binding = new FoundationIdempotencyBinding(context.ActorId, context.TenantId.Value, descriptor.SecurityProfile, descriptor.OperationId);
        var check = idempotencyStore.Begin(key!, binding, fingerprint, DateTimeOffset.UtcNow, TimeSpan.FromMinutes(10));
        if (check.Decision == LocalMasterDataIdempotencyDecision.Replay && check.Response is T replay)
        {
            httpContext.Response.Headers["X-Idempotent-Replay"] = "true";
            return ToResult(httpContext, PurchaseRequestOperationResult<T>.Success(replay), descriptor.OperationId, context, map, setEtag);
        }

        if (check.Decision is not LocalMasterDataIdempotencyDecision.New)
        {
            return await WriteProblemAsync(httpContext, 409, "idempotency_conflict", "Idempotency conflict", "The request cannot be replayed with different or incomplete input.", descriptor.OperationId);
        }

        var committed = false;
        try
        {
            var execution = await auditCoordinator.ExecuteProtectedAsync(
                foundationContext,
                descriptor.OperationId,
                GetCorrelation(httpContext),
                FoundationAuditReason.Allowed,
                () => operation(context),
                idempotencyKey: key,
                operationVersion: "procurement.purchase-request.v1",
                cancellationToken: httpContext.RequestAborted);
            if (!execution.Succeeded || execution.Value is null)
            {
                return await WriteProblemAsync(httpContext, 503, execution.Code, "Operation unavailable", "The Purchase Request operation could not be completed.", descriptor.OperationId);
            }

            var result = execution.Value;
            if (result.Succeeded && result.Value is not null)
            {
                idempotencyStore.Commit(key!, binding, result.Value);
                committed = true;
            }

            return ToResult(httpContext, result, descriptor.OperationId, context, map, setEtag);
        }
        finally
        {
            if (!committed)
            {
                idempotencyStore.Release(key!, binding);
            }
        }
    }

    private static IResult ToResult<T>(
        HttpContext httpContext,
        PurchaseRequestOperationResult<T> result,
        string operationId,
        ProcurementRequestContext context,
        Func<ProcurementRequestContext, T, object?> map,
        bool setEtag)
    {
        if (result.Succeeded && result.Value is not null)
        {
            if (setEtag && result.Value is PurchaseRequestRecord record)
            {
                httpContext.Response.Headers.ETag = $"\"{Convert.ToBase64String(record.Version)}\"";
            }

            return Results.Json(map(context, result.Value));
        }

        var code = result.Code;
        var status = code switch
        {
            "permission_denied" or "resource_scope_denied" or "cross_tenant_target_denied" or "tenant_context_failed" or "authorization_profile_denied" or "requester_only" or "self_approval_denied" => 403,
            "persistence_unavailable" or "reference_persistence_unavailable" or "authorization_operation_unmapped" or "approval_policy_not_configured" => 503,
            "purchase_request_not_found" => 404,
            "concurrency_conflict" or "purchase_request_duplicate" or "edit_not_allowed" or "submit_not_allowed" or "decision_not_allowed" or "approval_not_eligible" or "approval_duplicate" or "cancel_not_allowed" => 409,
            _ => 400
        };

        return Results.Problem(
            statusCode: status,
            title: status == 403 ? "Access denied" : "Purchase Request operation failed",
            detail: "The Purchase Request operation could not be completed.",
            type: $"https://api.minierp.local/problems/{code}",
            extensions: new Dictionary<string, object?>
            {
                ["code"] = code,
                ["correlationId"] = GetCorrelation(httpContext),
                ["operationId"] = operationId
            });
    }

    private static PurchaseRequestResponse ToResponse(
        PurchaseRequestRecord record,
        ProcurementRequestContext? context)
    {
        var actorId = context?.ActorId;
        var canEdit = actorId == record.RequesterId
            && record.Status is PurchaseRequestStatus.Draft or PurchaseRequestStatus.ReturnedForChange;
        var canSubmit = canEdit;
        var canApprove = actorId is { } actor
            && actor != record.RequesterId
            && record.Status == PurchaseRequestStatus.PendingApproval;
        var canReject = canApprove;
        var canReturn = canApprove;
        var canCancel = actorId == record.RequesterId
            && (record.Status is PurchaseRequestStatus.Draft or PurchaseRequestStatus.ReturnedForChange
                || record.Status == PurchaseRequestStatus.PendingApproval
                    && record.ApprovalPolicy?.AllowRequesterCancellationWhilePending == true);

        var approval = record.ApprovalPolicy is null
            ? null
            : new PurchaseRequestApprovalResponse(
                record.ApprovalPolicy.PolicyId,
                record.ApprovalPolicy.Version,
                record.CurrentApprovalStageIndex,
                record.ApprovalPolicy.Stages.OrderBy(item => item.Sequence).ElementAtOrDefault(record.CurrentApprovalStageIndex)?.StageKey ?? string.Empty,
                record.ApprovalPolicy.Stages.OrderBy(item => item.Sequence).ElementAtOrDefault(record.CurrentApprovalStageIndex)?.RequiredApprovals ?? 0,
                record.CurrentStageApprovalCount,
                record.ApprovalPolicy.Stages.OrderBy(item => item.Sequence).ElementAtOrDefault(record.CurrentApprovalStageIndex)?.AllowDelegation ?? false,
                record.ApprovalPolicy.AllowRequesterCancellationWhilePending);

        return new PurchaseRequestResponse(
            record.Id,
            record.TenantId,
            record.Scope.CompanyId,
            record.Scope.BranchId,
            record.RequesterId,
            record.Status.ToString(),
            record.Purpose,
            record.Lines.Select(ToLineResponse).ToArray(),
            approval,
            record.CreatedAt,
            record.UpdatedAt,
            record.SubmittedAt,
            record.ApprovedAt,
            record.CancelledAt,
            record.Version,
            canEdit,
            canSubmit,
            canApprove,
            canReject,
            canReturn,
            canCancel);
    }

    private static PurchaseRequestListItemResponse ToListResponse(PurchaseRequestListRecord record) => new(
        record.Id,
        record.Scope.CompanyId,
        record.Scope.BranchId,
        record.RequesterId,
        record.Status.ToString(),
        record.Purpose,
        record.LineCount,
        record.CreatedAt,
        record.UpdatedAt,
        record.Version);

    private static PurchaseRequestLineResponse ToLineResponse(PurchaseRequestLineSnapshot line) => new(
        line.Id,
        line.ProductId,
        line.ProductSku,
        line.ProductName,
        line.UnitOfMeasureId,
        line.UnitOfMeasureCode,
        line.Quantity,
        line.NeedByDate,
        line.Purpose,
        line.Version ?? []);

    private static PurchaseRequestHistoryResponse ToHistoryResponse(PurchaseRequestHistoryRecord record) => new(
        record.EvidenceId,
        record.PurchaseRequestId,
        record.OccurredAt,
        record.FromStatus.ToString(),
        record.ToStatus.ToString(),
        record.Action.ToString(),
        record.ActorId,
        record.Reason,
        record.CorrelationId,
        record.PolicyId,
        record.PolicyVersion,
        record.StageKey,
        record.DelegatedFromActorId);

    private static PurchaseRequestAuditResponse ToAuditResponse(PurchaseRequestAuditRecord record) => new(
        record.EvidenceId,
        record.PurchaseRequestId,
        record.OccurredAt,
        record.OperationId,
        record.CorrelationId,
        record.TenantId,
        record.ActorId,
        record.SessionId,
        record.AuthorizationPath,
        record.Decision,
        record.Reason,
        record.BeforeStatus?.ToString(),
        record.AfterStatus?.ToString(),
        record.CompanyId,
        record.BranchId,
        record.BeforeSummary,
        record.AfterSummary,
        record.IdempotencyKey);

    private static bool TryReadExpectedVersion(HttpContext httpContext, out byte[] version)
    {
        version = [];
        var value = httpContext.Request.Headers.IfMatch.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim();
        if (normalized.StartsWith("W/", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (normalized.Length >= 2 && normalized[0] == '"' && normalized[^1] == '"')
        {
            normalized = normalized[1..^1];
        }

        try
        {
            version = Convert.FromBase64String(normalized);
            return version.Length is > 0 and <= 64;
        }
        catch (FormatException)
        {
            version = [];
            return false;
        }
    }

    private static string? GetIdempotencyKey(HttpContext httpContext) =>
        httpContext.Request.Headers["Idempotency-Key"].FirstOrDefault();

    private static string Fingerprint(object? request) => JsonSerializer.Serialize(request);

    private static string VersionFingerprint(byte[] version) =>
        $"|version:{Convert.ToBase64String(version)}";

    private static async Task<bool> EnsureAntiforgeryAsync(HttpContext httpContext)
    {
        try
        {
            await httpContext.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(httpContext);
            return true;
        }
        catch (AntiforgeryValidationException)
        {
            return false;
        }
    }

    private static string GetCorrelation(HttpContext httpContext) =>
        httpContext.Items.TryGetValue(FoundationApiKeys.CorrelationItem, out var value)
            && value is string correlationId
            ? correlationId
            : FoundationCorrelation.Resolve(httpContext.Request);

    private static Task<IResult> WriteProblemAsync(
        HttpContext httpContext,
        int statusCode,
        string code,
        string title,
        string detail,
        string operationId) =>
        Task.FromResult<IResult>(Results.Problem(
            statusCode: statusCode,
            title: title,
            detail: detail,
            type: $"https://api.minierp.local/problems/{code}",
            extensions: new Dictionary<string, object?>
            {
                ["code"] = code,
                ["correlationId"] = GetCorrelation(httpContext),
                ["operationId"] = operationId
            }));
}

#pragma warning restore CS1591
