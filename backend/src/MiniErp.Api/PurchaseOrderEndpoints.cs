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

public static class PurchaseOrderEndpoints
{
    public static IEndpointRouteBuilder MapPurchaseOrderEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapGet(
                "/api/v1/procurement/purchase-order-sources",
                async (HttpContext httpContext, ITrustedRequestContextResolver resolver, ProcurementTenantContextResolver tenantResolver, PurchaseOrderService service) =>
                    await ExecuteReadAsync(
                        httpContext,
                        resolver,
                        tenantResolver,
                        FoundationOperationCatalog.GetRequired("procurement.purchase-order.source.list"),
                        context => service.ListSourceOptionsAsync(context, httpContext.RequestAborted),
                        (_, records) => records.Select(ToSourceOptionResponse).ToArray()))
            .WithName("procurement.purchase-order.source.list")
            .WithTags("Procurement / Purchase Orders")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("procurement.purchase-order.source.list")));

        endpoints.MapGet(
                "/api/v1/procurement/purchase-orders",
                async (HttpContext httpContext, ITrustedRequestContextResolver resolver, ProcurementTenantContextResolver tenantResolver, PurchaseOrderService service) =>
                {
                    PurchaseOrderStatus? status = null;
                    var parsed = default(PurchaseOrderStatus);
                    var rawStatus = httpContext.Request.Query["status"].FirstOrDefault();
                    if (!string.IsNullOrWhiteSpace(rawStatus) && !Enum.TryParse(rawStatus, true, out parsed))
                    {
                        return await WriteProblemAsync(httpContext, 400, "validation_failed", "Validation failed", "The Purchase Order status filter is invalid.", "procurement.purchase-order.list");
                    }

                    if (!string.IsNullOrWhiteSpace(rawStatus))
                    {
                        status = parsed;
                    }

                    return await ExecuteReadAsync(
                        httpContext,
                        resolver,
                        tenantResolver,
                        FoundationOperationCatalog.GetRequired("procurement.purchase-order.list"),
                        context => service.ListAsync(context, status, httpContext.RequestAborted),
                        (_, records) => records.Select(ToListResponse).ToArray());
                })
            .WithName("procurement.purchase-order.list")
            .WithTags("Procurement / Purchase Orders")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("procurement.purchase-order.list")));

        endpoints.MapGet(
                "/api/v1/procurement/purchase-orders/{purchaseOrderId:guid}",
                async (Guid purchaseOrderId, HttpContext httpContext, ITrustedRequestContextResolver resolver, ProcurementTenantContextResolver tenantResolver, PurchaseOrderService service) =>
                    await ExecuteReadAsync(
                        httpContext,
                        resolver,
                        tenantResolver,
                        FoundationOperationCatalog.GetRequired("procurement.purchase-order.read"),
                        context => service.GetAsync(context, purchaseOrderId, httpContext.RequestAborted),
                        (context, record) => ToResponse(record, context),
                        setEtag: true))
            .WithName("procurement.purchase-order.read")
            .WithTags("Procurement / Purchase Orders")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("procurement.purchase-order.read")));

        endpoints.MapPost(
                "/api/v1/procurement/purchase-orders",
                async (PurchaseOrderCreateRequest? request, HttpContext httpContext, ITrustedRequestContextResolver resolver, ProcurementTenantContextResolver tenantResolver, FoundationAuditCoordinator auditCoordinator, LocalMasterDataIdempotencyStore idempotencyStore, PurchaseOrderService service) =>
                {
                    if (request is null)
                    {
                        return await WriteProblemAsync(httpContext, 400, "validation_failed", "Validation failed", "A Purchase Order source body is required.", "procurement.purchase-order.create");
                    }

                    return await ExecuteMutationAsync(
                        httpContext,
                        resolver,
                        tenantResolver,
                        auditCoordinator,
                        idempotencyStore,
                        FoundationOperationCatalog.GetRequired("procurement.purchase-order.create"),
                        Fingerprint(request),
                        context => service.CreateAsync(context, request, GetIdempotencyKey(httpContext), httpContext.RequestAborted),
                        (context, record) => ToResponse(record, context),
                        setEtag: true);
                })
            .WithName("procurement.purchase-order.create")
            .WithTags("Procurement / Purchase Orders")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("procurement.purchase-order.create")));

        endpoints.MapPost(
                "/api/v1/procurement/purchase-orders/{purchaseOrderId:guid}/edit",
                async (Guid purchaseOrderId, PurchaseOrderEditRequest? request, HttpContext httpContext, ITrustedRequestContextResolver resolver, ProcurementTenantContextResolver tenantResolver, FoundationAuditCoordinator auditCoordinator, LocalMasterDataIdempotencyStore idempotencyStore, PurchaseOrderService service) =>
                {
                    if (request is null || !TryReadExpectedVersion(httpContext, out var expectedVersion))
                    {
                        return await WriteProblemAsync(httpContext, 400, "validation_failed", "Validation failed", "A valid If-Match version and Purchase Order body are required.", "procurement.purchase-order.edit");
                    }

                    return await ExecuteMutationAsync(
                        httpContext,
                        resolver,
                        tenantResolver,
                        auditCoordinator,
                        idempotencyStore,
                        FoundationOperationCatalog.GetRequired("procurement.purchase-order.edit"),
                        Fingerprint(request) + VersionFingerprint(expectedVersion),
                        context => service.EditAsync(context, purchaseOrderId, request, expectedVersion, GetIdempotencyKey(httpContext), httpContext.RequestAborted),
                        (context, record) => ToResponse(record, context),
                        setEtag: true,
                        requireExpectedVersion: true);
                })
            .WithName("procurement.purchase-order.edit")
            .WithTags("Procurement / Purchase Orders")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("procurement.purchase-order.edit")));

        MapLifecycleActions(endpoints);

        endpoints.MapGet(
                "/api/v1/procurement/purchase-orders/{purchaseOrderId:guid}/confirmations",
                async (Guid purchaseOrderId, HttpContext httpContext, ITrustedRequestContextResolver resolver, ProcurementTenantContextResolver tenantResolver, PurchaseOrderService service) =>
                    await ExecuteReadAsync(
                        httpContext,
                        resolver,
                        tenantResolver,
                        FoundationOperationCatalog.GetRequired("procurement.purchase-order.confirmation.read"),
                        context => service.ReadConfirmationsAsync(context, purchaseOrderId, httpContext.RequestAborted),
                        (_, records) => records.Select(ToConfirmationResponse).ToArray()))
            .WithName("procurement.purchase-order.confirmation.read")
            .WithTags("Procurement / Purchase Orders")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("procurement.purchase-order.confirmation.read")));

        endpoints.MapPost(
                "/api/v1/procurement/purchase-orders/{purchaseOrderId:guid}/confirmations",
                async (Guid purchaseOrderId, PurchaseOrderConfirmationRequest? request, HttpContext httpContext, ITrustedRequestContextResolver resolver, ProcurementTenantContextResolver tenantResolver, FoundationAuditCoordinator auditCoordinator, LocalMasterDataIdempotencyStore idempotencyStore, PurchaseOrderService service) =>
                {
                    if (request is null || !TryReadExpectedVersion(httpContext, out var expectedVersion))
                    {
                        return await WriteProblemAsync(httpContext, 400, "validation_failed", "Validation failed", "A valid If-Match version and confirmation body are required.", "procurement.purchase-order.confirmation.capture");
                    }

                    return await ExecuteMutationAsync(
                        httpContext,
                        resolver,
                        tenantResolver,
                        auditCoordinator,
                        idempotencyStore,
                        FoundationOperationCatalog.GetRequired("procurement.purchase-order.confirmation.capture"),
                        Fingerprint(request) + VersionFingerprint(expectedVersion),
                        context => service.RecordConfirmationAsync(context, purchaseOrderId, request, expectedVersion, GetIdempotencyKey(httpContext), httpContext.RequestAborted),
                        (context, record) => ToResponse(record, context),
                        setEtag: true,
                        requireExpectedVersion: true);
                })
            .WithName("procurement.purchase-order.confirmation.capture")
            .WithTags("Procurement / Purchase Orders")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("procurement.purchase-order.confirmation.capture")));

        MapSupplierChangeActions(endpoints);

        endpoints.MapGet(
                "/api/v1/procurement/purchase-orders/{purchaseOrderId:guid}/history",
                async (Guid purchaseOrderId, HttpContext httpContext, ITrustedRequestContextResolver resolver, ProcurementTenantContextResolver tenantResolver, PurchaseOrderService service) =>
                    await ExecuteReadAsync(
                        httpContext,
                        resolver,
                        tenantResolver,
                        FoundationOperationCatalog.GetRequired("procurement.purchase-order.history.read"),
                        context => service.ReadHistoryAsync(context, purchaseOrderId, httpContext.RequestAborted),
                        (_, records) => records.Select(ToHistoryResponse).ToArray()))
            .WithName("procurement.purchase-order.history.read")
            .WithTags("Procurement / Purchase Orders")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("procurement.purchase-order.history.read")));

        endpoints.MapGet(
                "/api/v1/procurement/purchase-orders/{purchaseOrderId:guid}/audit",
                async (Guid purchaseOrderId, HttpContext httpContext, ITrustedRequestContextResolver resolver, ProcurementTenantContextResolver tenantResolver, PurchaseOrderService service) =>
                    await ExecuteReadAsync(
                        httpContext,
                        resolver,
                        tenantResolver,
                        FoundationOperationCatalog.GetRequired("procurement.purchase-order.audit.read"),
                        context => service.ReadAuditAsync(context, purchaseOrderId, httpContext.RequestAborted),
                        (_, records) => records.Select(ToAuditResponse).ToArray()))
            .WithName("procurement.purchase-order.audit.read")
            .WithTags("Procurement / Purchase Orders")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("procurement.purchase-order.audit.read")));

        return endpoints;
    }

    private static void MapLifecycleActions(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(
                "/api/v1/procurement/purchase-orders/{purchaseOrderId:guid}/submit",
                async (Guid purchaseOrderId, PurchaseOrderActionRequest? request, HttpContext httpContext, ITrustedRequestContextResolver resolver, ProcurementTenantContextResolver tenantResolver, FoundationAuditCoordinator auditCoordinator, LocalMasterDataIdempotencyStore idempotencyStore, PurchaseOrderService service) =>
                    await ExecuteMutationAsync(
                        purchaseOrderId,
                        request,
                        httpContext,
                        resolver,
                        tenantResolver,
                        auditCoordinator,
                        idempotencyStore,
                        service,
                        FoundationOperationCatalog.GetRequired("procurement.purchase-order.submit"),
                        (currentService, context, id, version, key, _) => currentService.SubmitAsync(context, id, version, key)))
            .WithName("procurement.purchase-order.submit")
            .WithTags("Procurement / Purchase Orders")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("procurement.purchase-order.submit")));

        endpoints.MapPost(
                "/api/v1/procurement/purchase-orders/{purchaseOrderId:guid}/approve",
                async (Guid purchaseOrderId, PurchaseOrderActionRequest? request, HttpContext httpContext, ITrustedRequestContextResolver resolver, ProcurementTenantContextResolver tenantResolver, FoundationAuditCoordinator auditCoordinator, LocalMasterDataIdempotencyStore idempotencyStore, PurchaseOrderService service) =>
                    await ExecuteMutationAsync(
                        purchaseOrderId,
                        request,
                        httpContext,
                        resolver,
                        tenantResolver,
                        auditCoordinator,
                        idempotencyStore,
                        service,
                        FoundationOperationCatalog.GetRequired("procurement.purchase-order.approve"),
                        (currentService, context, id, version, key, _) => currentService.ApproveAsync(context, id, version, key)))
            .WithName("procurement.purchase-order.approve")
            .WithTags("Procurement / Purchase Orders")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("procurement.purchase-order.approve")));

        endpoints.MapPost(
                "/api/v1/procurement/purchase-orders/{purchaseOrderId:guid}/reject",
                async (Guid purchaseOrderId, PurchaseOrderActionRequest? request, HttpContext httpContext, ITrustedRequestContextResolver resolver, ProcurementTenantContextResolver tenantResolver, FoundationAuditCoordinator auditCoordinator, LocalMasterDataIdempotencyStore idempotencyStore, PurchaseOrderService service) =>
                    await ExecuteMutationAsync(
                        purchaseOrderId,
                        request,
                        httpContext,
                        resolver,
                        tenantResolver,
                        auditCoordinator,
                        idempotencyStore,
                        service,
                        FoundationOperationCatalog.GetRequired("procurement.purchase-order.reject"),
                        (currentService, context, id, version, key, body) => currentService.RejectAsync(context, id, version, body?.Reason, key)))
            .WithName("procurement.purchase-order.reject")
            .WithTags("Procurement / Purchase Orders")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("procurement.purchase-order.reject")));

        endpoints.MapPost(
                "/api/v1/procurement/purchase-orders/{purchaseOrderId:guid}/return-for-change",
                async (Guid purchaseOrderId, PurchaseOrderActionRequest? request, HttpContext httpContext, ITrustedRequestContextResolver resolver, ProcurementTenantContextResolver tenantResolver, FoundationAuditCoordinator auditCoordinator, LocalMasterDataIdempotencyStore idempotencyStore, PurchaseOrderService service) =>
                    await ExecuteMutationAsync(
                        purchaseOrderId,
                        request,
                        httpContext,
                        resolver,
                        tenantResolver,
                        auditCoordinator,
                        idempotencyStore,
                        service,
                        FoundationOperationCatalog.GetRequired("procurement.purchase-order.return-for-change"),
                        (currentService, context, id, version, key, body) => currentService.ReturnForChangeAsync(context, id, version, body?.Reason, key)))
            .WithName("procurement.purchase-order.return-for-change")
            .WithTags("Procurement / Purchase Orders")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("procurement.purchase-order.return-for-change")));

        endpoints.MapPost(
                "/api/v1/procurement/purchase-orders/{purchaseOrderId:guid}/issue",
                async (Guid purchaseOrderId, PurchaseOrderActionRequest? request, HttpContext httpContext, ITrustedRequestContextResolver resolver, ProcurementTenantContextResolver tenantResolver, FoundationAuditCoordinator auditCoordinator, LocalMasterDataIdempotencyStore idempotencyStore, PurchaseOrderService service) =>
                    await ExecuteMutationAsync(
                        purchaseOrderId,
                        request,
                        httpContext,
                        resolver,
                        tenantResolver,
                        auditCoordinator,
                        idempotencyStore,
                        service,
                        FoundationOperationCatalog.GetRequired("procurement.purchase-order.issue"),
                        (currentService, context, id, version, key, _) => currentService.IssueAsync(context, id, version, key)))
            .WithName("procurement.purchase-order.issue")
            .WithTags("Procurement / Purchase Orders")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("procurement.purchase-order.issue")));

        endpoints.MapPost(
                "/api/v1/procurement/purchase-orders/{purchaseOrderId:guid}/cancel",
                async (Guid purchaseOrderId, PurchaseOrderActionRequest? request, HttpContext httpContext, ITrustedRequestContextResolver resolver, ProcurementTenantContextResolver tenantResolver, FoundationAuditCoordinator auditCoordinator, LocalMasterDataIdempotencyStore idempotencyStore, PurchaseOrderService service) =>
                    await ExecuteMutationAsync(
                        purchaseOrderId,
                        request,
                        httpContext,
                        resolver,
                        tenantResolver,
                        auditCoordinator,
                        idempotencyStore,
                        service,
                        FoundationOperationCatalog.GetRequired("procurement.purchase-order.cancel"),
                        (currentService, context, id, version, key, body) => currentService.CancelAsync(context, id, version, body?.Reason, key)))
            .WithName("procurement.purchase-order.cancel")
            .WithTags("Procurement / Purchase Orders")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("procurement.purchase-order.cancel")));
    }

    private static void MapSupplierChangeActions(IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost(
                "/api/v1/procurement/purchase-orders/{purchaseOrderId:guid}/supplier-change/approve",
                async (Guid purchaseOrderId, PurchaseOrderActionRequest? request, HttpContext httpContext, ITrustedRequestContextResolver resolver, ProcurementTenantContextResolver tenantResolver, FoundationAuditCoordinator auditCoordinator, LocalMasterDataIdempotencyStore idempotencyStore, PurchaseOrderService service) =>
                    await ExecuteMutationAsync(
                        purchaseOrderId,
                        request,
                        httpContext,
                        resolver,
                        tenantResolver,
                        auditCoordinator,
                        idempotencyStore,
                        service,
                        FoundationOperationCatalog.GetRequired("procurement.purchase-order.supplier-change.approve"),
                        (currentService, context, id, version, key, _) => currentService.ApproveSupplierChangeAsync(context, id, version, key)))
            .WithName("procurement.purchase-order.supplier-change.approve")
            .WithTags("Procurement / Purchase Orders")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("procurement.purchase-order.supplier-change.approve")));

        endpoints.MapPost(
                "/api/v1/procurement/purchase-orders/{purchaseOrderId:guid}/supplier-change/reject",
                async (Guid purchaseOrderId, PurchaseOrderActionRequest? request, HttpContext httpContext, ITrustedRequestContextResolver resolver, ProcurementTenantContextResolver tenantResolver, FoundationAuditCoordinator auditCoordinator, LocalMasterDataIdempotencyStore idempotencyStore, PurchaseOrderService service) =>
                    await ExecuteMutationAsync(
                        purchaseOrderId,
                        request,
                        httpContext,
                        resolver,
                        tenantResolver,
                        auditCoordinator,
                        idempotencyStore,
                        service,
                        FoundationOperationCatalog.GetRequired("procurement.purchase-order.supplier-change.reject"),
                        (currentService, context, id, version, key, body) => currentService.RejectSupplierChangeAsync(context, id, version, body?.Reason, key)))
            .WithName("procurement.purchase-order.supplier-change.reject")
            .WithTags("Procurement / Purchase Orders")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("procurement.purchase-order.supplier-change.reject")));
    }

    private static async Task<IResult> ExecuteMutationAsync(
        Guid purchaseOrderId,
        PurchaseOrderActionRequest? request,
        HttpContext httpContext,
        ITrustedRequestContextResolver resolver,
        ProcurementTenantContextResolver tenantResolver,
        FoundationAuditCoordinator auditCoordinator,
        LocalMasterDataIdempotencyStore idempotencyStore,
        PurchaseOrderService service,
        FoundationOperationDescriptor descriptor,
        Func<PurchaseOrderService, ProcurementRequestContext, Guid, byte[], string, PurchaseOrderActionRequest?, Task<PurchaseOrderOperationResult<PurchaseOrderRecord>>> operation)
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
            Fingerprint(request) + VersionFingerprint(expectedVersion),
            context => operation(service, context, purchaseOrderId, expectedVersion, GetIdempotencyKey(httpContext)!, request),
            (context, record) => ToResponse(record, context),
            setEtag: true,
            requireExpectedVersion: true);
    }

    private static async Task<IResult> ExecuteReadAsync<T>(
        HttpContext httpContext,
        ITrustedRequestContextResolver resolver,
        ProcurementTenantContextResolver tenantResolver,
        FoundationOperationDescriptor descriptor,
        Func<ProcurementRequestContext, Task<PurchaseOrderOperationResult<T>>> operation,
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

    private static async Task<IResult> ExecuteMutationAsync<T>(
        HttpContext httpContext,
        ITrustedRequestContextResolver resolver,
        ProcurementTenantContextResolver tenantResolver,
        FoundationAuditCoordinator auditCoordinator,
        LocalMasterDataIdempotencyStore idempotencyStore,
        FoundationOperationDescriptor descriptor,
        string fingerprint,
        Func<ProcurementRequestContext, Task<PurchaseOrderOperationResult<T>>> operation,
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
            return ToResult(httpContext, PurchaseOrderOperationResult<T>.Success(replay), descriptor.OperationId, context, map, setEtag);
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
                operationVersion: "procurement.purchase-order.v1",
                cancellationToken: httpContext.RequestAborted);
            if (!execution.Succeeded || execution.Value is null)
            {
                return await WriteProblemAsync(httpContext, 503, execution.Code, "Operation unavailable", "The Purchase Order operation could not be completed.", descriptor.OperationId);
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

    private static IResult ToResult<T>(HttpContext httpContext, PurchaseOrderOperationResult<T> result, string operationId, ProcurementRequestContext context, Func<ProcurementRequestContext, T, object?> map, bool setEtag)
    {
        if (result.Succeeded && result.Value is not null)
        {
            if (setEtag && result.Value is PurchaseOrderRecord record)
            {
                httpContext.Response.Headers.ETag = $"\"{Convert.ToBase64String(record.Version)}\"";
            }

            return Results.Json(map(context, result.Value));
        }

        var code = result.Code;
        var status = code switch
        {
            "permission_denied" or "resource_scope_denied" or "cross_tenant_target_denied" or "tenant_context_failed" or "authorization_profile_denied" => 403,
            "persistence_unavailable" or "authorization_operation_unmapped" => 503,
            "purchase_order_not_found" or "source_decision_not_found" or "source_not_found" => 404,
            "concurrency_conflict" or "purchase_order_duplicate" or "edit_not_allowed" or "submit_not_allowed" or "decision_not_allowed" or "issue_not_allowed" or "confirmation_not_allowed" or "supplier_change_approval_not_allowed" or "supplier_change_rejection_not_allowed" or "cancel_not_allowed" or "source_quotation_not_eligible" or "purchase_request_not_approved" => 409,
            _ => 400
        };

        return Results.Problem(statusCode: status, title: status == 403 ? "Access denied" : "Purchase Order operation failed", detail: "The Purchase Order operation could not be completed.", type: $"https://api.minierp.local/problems/{code}", extensions: new Dictionary<string, object?> { ["code"] = code, ["correlationId"] = GetCorrelation(httpContext), ["operationId"] = operationId });
    }

    private static PurchaseOrderSourceOptionResponse ToSourceOptionResponse(PurchaseOrderSourceOptionRecord option) => new(
        option.Source,
        option.Scope.CompanyId,
        option.Scope.BranchId,
        option.Lines.Select(item => new PurchaseOrderSourceLineResponse(
            item.SourceQuotationLineId,
            item.PurchaseRequestLineId,
            item.ProductSku,
            item.ProductName,
            item.UnitOfMeasureCode,
            item.RequestedQuantity,
            item.SelectedQuantity,
            item.UnitPrice,
            item.DiscountAmount,
            item.DiscountPercentage,
            item.TaxCode,
            item.TaxName,
            item.TaxRatePercentage,
            item.TaxAmount,
            item.RequestedNeedByDate,
            item.OfferedDeliveryDate)).ToArray(),
        Version(option.PurchaseRequestVersion));

    private static PurchaseOrderListItemResponse ToListResponse(PurchaseOrderListRecord record) => new(record.Id, record.TenantId, record.Scope.CompanyId, record.Scope.BranchId, record.Status.ToString(), record.SupplierCode, record.SupplierName, record.SupplierQuotationReference, record.CurrencyCode, record.Total, record.LineCount, record.CreatedAt, record.UpdatedAt, Version(record.Version));

    private static PurchaseOrderResponse ToResponse(PurchaseOrderRecord record, ProcurementRequestContext context)
    {
        var canApprove = record.Status == PurchaseOrderStatus.PendingApproval && record.CreatedByActorId != context.ActorId;
        var canChangeApproval = record.Status == PurchaseOrderStatus.ChangedPendingApproval && record.CreatedByActorId != context.ActorId;
        var canEdit = record.CreatedByActorId == context.ActorId && (record.Status is PurchaseOrderStatus.Draft or PurchaseOrderStatus.ReturnedForChange);
        var canCancel = record.CreatedByActorId == context.ActorId && (record.Status is PurchaseOrderStatus.Draft or PurchaseOrderStatus.ReturnedForChange or PurchaseOrderStatus.Approved or PurchaseOrderStatus.Issued or PurchaseOrderStatus.PartiallyConfirmed or PurchaseOrderStatus.NoResponse);
        return new PurchaseOrderResponse(
            record.Id,
            record.TenantId,
            record.Scope.CompanyId,
            record.Scope.BranchId,
            record.CreatedByActorId,
            record.Status.ToString(),
            record.Source,
            record.Notes,
            record.CreatedAt,
            record.UpdatedAt,
            record.SubmittedAt,
            record.ApprovedAt,
            record.IssuedAt,
            record.CancelledAt,
            record.LatestConfirmationId,
            record.LatestConfirmationStatus?.ToString(),
            record.Approval is null ? null : record.Approval with { },
            record.Lines.Select(ToLineResponse).ToArray(),
            record.PendingChanges.Select(ToSupplierChangeResponse).ToArray(),
            Version(record.Version),
            canEdit,
            canEdit,
            canApprove,
            canApprove,
            canApprove,
            record.Status == PurchaseOrderStatus.Approved,
            canCancel,
            record.Status is PurchaseOrderStatus.Issued or PurchaseOrderStatus.Confirmed or PurchaseOrderStatus.PartiallyConfirmed or PurchaseOrderStatus.NoResponse,
            canChangeApproval,
            canChangeApproval);
    }

    private static PurchaseOrderLineResponse ToLineResponse(PurchaseOrderLineRecord line) => new(line.Id, line.SourceQuotationLineId, line.PurchaseRequestLineId, line.ProductSku, line.ProductName, line.UnitOfMeasureCode, line.OrderedQuantity, line.ConfirmedQuantity, line.RemainingQuantity, line.UnitPrice, line.DiscountAmount, line.DiscountPercentage, line.TaxCode, line.TaxName, line.TaxRatePercentage, line.TaxAmount, line.RequestedNeedByDate, line.DeliveryDate, line.Notes, Version(line.Version));

    private static PurchaseOrderSupplierChangeResponse ToSupplierChangeResponse(PurchaseOrderSupplierChangeRecord change) => new(change.Id, change.ConfirmationId, change.PurchaseOrderLineId, change.PreviousOrderedQuantity, change.ProposedQuantity, change.PreviousUnitPrice, change.ProposedUnitPrice, change.PreviousDeliveryDate, change.ProposedDeliveryDate, change.Status, change.Reason, change.ActorId, change.ProposedAt, change.DecidedAt, change.DecisionReason, change.Version is null ? null : Version(change.Version));

    private static PurchaseOrderConfirmationResponse ToConfirmationResponse(PurchaseOrderConfirmationRecord record) => new(record.Id, record.PurchaseOrderId, record.Status.ToString(), record.ResponseDate, record.SupplierReference, record.SupplierContact, record.Reason, record.Notes, record.RecordedByActorId, record.RecordedAt, Version(record.PurchaseOrderVersion), record.Lines.Select(item => new PurchaseOrderConfirmationLineResponse(item.Id, item.PurchaseOrderLineId, item.OrderedQuantityAtResponse, item.ConfirmedQuantity, item.RemainingQuantity, item.ExpectedDeliveryDate, item.ProposedQuantity, item.ProposedUnitPrice, item.ProposedDeliveryDate, item.ChangeReason, Version(item.Version ?? []))).ToArray(), record.Evidence.Select(item => new PurchaseOrderEvidenceReferenceResponse(item.Id, item.ReferenceId, item.FileName, item.ContentType, item.Description, item.Source, item.ExternalReference, item.RecordedByActorId, item.RecordedAt, Version(item.Version ?? []))).ToArray(), record.Changes.Select(ToSupplierChangeResponse).ToArray(), Version(record.Version));

    private static PurchaseOrderHistoryResponse ToHistoryResponse(PurchaseOrderHistoryRecord record) => new(record.EvidenceId, record.PurchaseOrderId, record.OccurredAt, record.FromStatus.ToString(), record.ToStatus.ToString(), record.Action.ToString(), record.ActorId, record.Reason, record.CorrelationId, record.PolicyId, record.PolicyVersion, record.StageKey, record.DelegatedFromActorId);

    private static PurchaseOrderAuditResponse ToAuditResponse(PurchaseOrderAuditRecord record) => new(record.EvidenceId, record.PurchaseOrderId, record.OccurredAt, record.OperationId, record.CorrelationId, record.TenantId, record.ActorId, record.SessionId, record.AuthorizationPath, record.Decision, record.Reason, record.BeforeStatus?.ToString(), record.AfterStatus?.ToString(), record.CompanyId, record.BranchId, record.BeforeSummary, record.AfterSummary, record.IdempotencyKey);

    private static bool TryReadExpectedVersion(HttpContext httpContext, out byte[] version)
    {
        version = [];
        var value = httpContext.Request.Headers.IfMatch.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(value)) return false;
        var normalized = value.Trim();
        if (normalized.StartsWith("W/", StringComparison.OrdinalIgnoreCase)) return false;
        if (normalized.Length >= 2 && normalized[0] == '"' && normalized[^1] == '"') normalized = normalized[1..^1];
        try { version = Convert.FromBase64String(normalized); return version.Length is > 0 and <= 64; }
        catch (FormatException) { version = []; return false; }
    }

    private static string? GetIdempotencyKey(HttpContext httpContext) => httpContext.Request.Headers["Idempotency-Key"].FirstOrDefault();
    private static string Fingerprint(object? value) => JsonSerializer.Serialize(value);
    private static string VersionFingerprint(byte[] version) => $"|version:{Convert.ToBase64String(version)}";
    private static string Version(byte[] version) => Convert.ToBase64String(version);

    private static async Task<bool> EnsureAntiforgeryAsync(HttpContext httpContext)
    {
        try { await httpContext.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(httpContext); return true; }
        catch (AntiforgeryValidationException) { return false; }
    }

    private static string GetCorrelation(HttpContext httpContext) => httpContext.Items.TryGetValue(FoundationApiKeys.CorrelationItem, out var value) && value is string correlationId ? correlationId : FoundationCorrelation.Resolve(httpContext.Request);

    private static Task<IResult> WriteProblemAsync(HttpContext httpContext, int statusCode, string code, string title, string detail, string operationId) => Task.FromResult<IResult>(Results.Problem(statusCode: statusCode, title: title, detail: detail, type: $"https://api.minierp.local/problems/{code}", extensions: new Dictionary<string, object?> { ["code"] = code, ["correlationId"] = GetCorrelation(httpContext), ["operationId"] = operationId }));
}

public sealed record PurchaseOrderSourceOptionResponse(
    PurchaseOrderSourceResponse Source,
    Guid CompanyId,
    Guid? BranchId,
    IReadOnlyList<PurchaseOrderSourceLineResponse> Lines,
    string PurchaseRequestVersion);

#pragma warning restore CS1591
