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

public static class GoodsReceiptEndpoints
{
    public static IEndpointRouteBuilder MapGoodsReceiptEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapGet(
                "/api/v1/procurement/goods-receipt-sources",
                async (HttpContext httpContext, ITrustedRequestContextResolver resolver, ProcurementTenantContextResolver tenantResolver, GoodsReceiptService service) =>
                    await ExecuteReadAsync(
                        httpContext,
                        resolver,
                        tenantResolver,
                        FoundationOperationCatalog.GetRequired("procurement.goods-receipt.eligible-source.list"),
                        context => service.ListEligibleSourcesAsync(context, httpContext.RequestAborted),
                        (_, records) => records.Select(ToEligibleSourceResponse).ToArray()))
            .WithName("procurement.goods-receipt.eligible-source.list")
            .WithTags("Procurement / Goods Receipts")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("procurement.goods-receipt.eligible-source.list")));

        endpoints.MapGet(
                "/api/v1/procurement/warehouses",
                async (HttpContext httpContext, ITrustedRequestContextResolver resolver, ProcurementTenantContextResolver tenantResolver, GoodsReceiptService service) =>
                {
                    Guid? companyId = null;
                    var rawCompanyId = httpContext.Request.Query["companyId"].FirstOrDefault();
                    if (!string.IsNullOrWhiteSpace(rawCompanyId))
                    {
                        if (!Guid.TryParse(rawCompanyId, out var parsedCompanyId))
                        {
                            return await WriteProblemAsync(httpContext, 400, "validation_failed", "Validation failed", "The company filter is invalid.", "procurement.warehouse.list");
                        }

                        companyId = parsedCompanyId;
                    }

                    Guid? branchId = null;
                    var rawBranchId = httpContext.Request.Query["branchId"].FirstOrDefault();
                    if (!string.IsNullOrWhiteSpace(rawBranchId))
                    {
                        if (!Guid.TryParse(rawBranchId, out var parsedBranchId))
                        {
                            return await WriteProblemAsync(httpContext, 400, "validation_failed", "Validation failed", "The branch filter is invalid.", "procurement.warehouse.list");
                        }

                        branchId = parsedBranchId;
                    }

                    return await ExecuteReadAsync(
                        httpContext,
                        resolver,
                        tenantResolver,
                        FoundationOperationCatalog.GetRequired("procurement.warehouse.list"),
                        context => service.ListWarehousesAsync(context, companyId, branchId, httpContext.RequestAborted),
                        (_, records) => records.Select(ToWarehouseResponse).ToArray());
                })
            .WithName("procurement.warehouse.list")
            .WithTags("Procurement / Goods Receipts")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("procurement.warehouse.list")));

        endpoints.MapGet(
                "/api/v1/procurement/goods-receipts",
                async (HttpContext httpContext, ITrustedRequestContextResolver resolver, ProcurementTenantContextResolver tenantResolver, GoodsReceiptService service) =>
                {
                    GoodsReceiptStatus? status = null;
                    var parsed = default(GoodsReceiptStatus);
                    var rawStatus = httpContext.Request.Query["status"].FirstOrDefault();
                    if (!string.IsNullOrWhiteSpace(rawStatus) && !Enum.TryParse(rawStatus, true, out parsed))
                    {
                        return await WriteProblemAsync(httpContext, 400, "validation_failed", "Validation failed", "The Goods Receipt status filter is invalid.", "procurement.goods-receipt.list");
                    }

                    if (!string.IsNullOrWhiteSpace(rawStatus))
                    {
                        status = parsed;
                    }

                    Guid? purchaseOrderId = null;
                    var rawPurchaseOrderId = httpContext.Request.Query["purchaseOrderId"].FirstOrDefault();
                    if (!string.IsNullOrWhiteSpace(rawPurchaseOrderId))
                    {
                        if (!Guid.TryParse(rawPurchaseOrderId, out var parsedPurchaseOrderId))
                        {
                            return await WriteProblemAsync(httpContext, 400, "validation_failed", "Validation failed", "The Purchase Order filter is invalid.", "procurement.goods-receipt.list");
                        }

                        purchaseOrderId = parsedPurchaseOrderId;
                    }

                    return await ExecuteReadAsync(
                        httpContext,
                        resolver,
                        tenantResolver,
                        FoundationOperationCatalog.GetRequired("procurement.goods-receipt.list"),
                        context => service.ListAsync(context, status, purchaseOrderId, httpContext.RequestAborted),
                        (_, records) => records.Select(ToListResponse).ToArray());
                })
            .WithName("procurement.goods-receipt.list")
            .WithTags("Procurement / Goods Receipts")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("procurement.goods-receipt.list")));

        endpoints.MapGet(
                "/api/v1/procurement/goods-receipts/{goodsReceiptId:guid}",
                async (Guid goodsReceiptId, HttpContext httpContext, ITrustedRequestContextResolver resolver, ProcurementTenantContextResolver tenantResolver, GoodsReceiptService service) =>
                    await ExecuteReadAsync(
                        httpContext,
                        resolver,
                        tenantResolver,
                        FoundationOperationCatalog.GetRequired("procurement.goods-receipt.read"),
                        context => service.GetAsync(context, goodsReceiptId, httpContext.RequestAborted),
                        (context, record) => ToResponse(record, context),
                        setEtag: true))
            .WithName("procurement.goods-receipt.read")
            .WithTags("Procurement / Goods Receipts")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("procurement.goods-receipt.read")));

        endpoints.MapPost(
                "/api/v1/procurement/goods-receipts",
                async (GoodsReceiptCreateRequest? request, HttpContext httpContext, ITrustedRequestContextResolver resolver, ProcurementTenantContextResolver tenantResolver, FoundationAuditCoordinator auditCoordinator, LocalMasterDataIdempotencyStore idempotencyStore, GoodsReceiptService service) =>
                {
                    if (request is null)
                    {
                        return await WriteProblemAsync(httpContext, 400, "validation_failed", "Validation failed", "A Goods Receipt body is required.", "procurement.goods-receipt.create");
                    }

                    var fingerprint = Fingerprint(request);
                    return await ExecuteMutationAsync(
                        httpContext,
                        resolver,
                        tenantResolver,
                        auditCoordinator,
                        idempotencyStore,
                        FoundationOperationCatalog.GetRequired("procurement.goods-receipt.create"),
                        fingerprint,
                        context => service.CreateAsync(context, request, GetIdempotencyKey(httpContext), fingerprint, httpContext.RequestAborted),
                        (context, record) => ToResponse(record, context),
                        setEtag: true);
                })
            .WithName("procurement.goods-receipt.create")
            .WithTags("Procurement / Goods Receipts")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("procurement.goods-receipt.create")));

        endpoints.MapPost(
                "/api/v1/procurement/goods-receipts/{goodsReceiptId:guid}/cancel",
                async (Guid goodsReceiptId, GoodsReceiptActionRequest? request, HttpContext httpContext, ITrustedRequestContextResolver resolver, ProcurementTenantContextResolver tenantResolver, FoundationAuditCoordinator auditCoordinator, LocalMasterDataIdempotencyStore idempotencyStore, GoodsReceiptService service) =>
                {
                    if (!TryReadExpectedVersion(httpContext, out var expectedVersion))
                    {
                        return await WriteProblemAsync(httpContext, 400, "validation_failed", "Validation failed", "A valid If-Match version is required.", "procurement.goods-receipt.cancel");
                    }

                    var fingerprint = Fingerprint(request) + VersionFingerprint(expectedVersion) + TargetFingerprint(goodsReceiptId);
                    return await ExecuteMutationAsync(
                        httpContext,
                        resolver,
                        tenantResolver,
                        auditCoordinator,
                        idempotencyStore,
                        FoundationOperationCatalog.GetRequired("procurement.goods-receipt.cancel"),
                        fingerprint,
                        context => service.CancelAsync(context, goodsReceiptId, expectedVersion, request?.Reason, GetIdempotencyKey(httpContext), fingerprint, httpContext.RequestAborted),
                        (context, record) => ToResponse(record, context),
                        setEtag: true,
                        requireExpectedVersion: true);
                })
            .WithName("procurement.goods-receipt.cancel")
            .WithTags("Procurement / Goods Receipts")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("procurement.goods-receipt.cancel")));

        endpoints.MapGet(
                "/api/v1/procurement/goods-receipts/{goodsReceiptId:guid}/history",
                async (Guid goodsReceiptId, HttpContext httpContext, ITrustedRequestContextResolver resolver, ProcurementTenantContextResolver tenantResolver, GoodsReceiptService service) =>
                    await ExecuteReadAsync(
                        httpContext,
                        resolver,
                        tenantResolver,
                        FoundationOperationCatalog.GetRequired("procurement.goods-receipt.history.read"),
                        context => service.ReadHistoryAsync(context, goodsReceiptId, httpContext.RequestAborted),
                        (_, records) => records.Select(ToHistoryResponse).ToArray()))
            .WithName("procurement.goods-receipt.history.read")
            .WithTags("Procurement / Goods Receipts")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("procurement.goods-receipt.history.read")));

        endpoints.MapGet(
                "/api/v1/procurement/goods-receipts/{goodsReceiptId:guid}/audit",
                async (Guid goodsReceiptId, HttpContext httpContext, ITrustedRequestContextResolver resolver, ProcurementTenantContextResolver tenantResolver, GoodsReceiptService service) =>
                    await ExecuteReadAsync(
                        httpContext,
                        resolver,
                        tenantResolver,
                        FoundationOperationCatalog.GetRequired("procurement.goods-receipt.audit.read"),
                        context => service.ReadAuditAsync(context, goodsReceiptId, httpContext.RequestAborted),
                        (_, records) => records.Select(ToAuditResponse).ToArray()))
            .WithName("procurement.goods-receipt.audit.read")
            .WithTags("Procurement / Goods Receipts")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("procurement.goods-receipt.audit.read")));

        return endpoints;
    }

    private static async Task<IResult> ExecuteReadAsync<T>(
        HttpContext httpContext,
        ITrustedRequestContextResolver resolver,
        ProcurementTenantContextResolver tenantResolver,
        FoundationOperationDescriptor descriptor,
        Func<ProcurementRequestContext, Task<GoodsReceiptOperationResult<T>>> operation,
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
        Func<ProcurementRequestContext, Task<GoodsReceiptOperationResult<T>>> operation,
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
            return ToResult(httpContext, GoodsReceiptOperationResult<T>.Success(replay), descriptor.OperationId, context, map, setEtag);
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
                operationVersion: "procurement.goods-receipt.v1",
                cancellationToken: httpContext.RequestAborted);
            if (!execution.Succeeded || execution.Value is null)
            {
                return await WriteProblemAsync(httpContext, 503, execution.Code, "Operation unavailable", "The Goods Receipt operation could not be completed.", descriptor.OperationId);
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

    private static IResult ToResult<T>(HttpContext httpContext, GoodsReceiptOperationResult<T> result, string operationId, ProcurementRequestContext context, Func<ProcurementRequestContext, T, object?> map, bool setEtag)
    {
        if (result.Succeeded && result.Value is not null)
        {
            if (setEtag && result.Value is GoodsReceiptRecord record)
            {
                httpContext.Response.Headers.ETag = $"\"{Convert.ToBase64String(record.Version)}\"";
            }

            return Results.Json(map(context, result.Value));
        }

        var code = result.Code;
        var status = code switch
        {
            "permission_denied" or "resource_scope_denied" or "cross_tenant_target_denied" or "tenant_context_failed" or "authorization_profile_denied" or "warehouse_not_authorized" or "warehouse_scope_denied" => 403,
            "persistence_unavailable" or "authorization_operation_unmapped" => 503,
            "goods_receipt_not_found" => 404,
            "concurrency_conflict" or "idempotency_conflict" or "goods_receipt_duplicate" or "goods_receipt_source_not_eligible" or "goods_receipt_line_not_eligible" or "over_receipt_not_allowed" or "cancel_not_allowed" or "goods_receipt_referenced_by_active_invoice_handoff" or "reason_required" or "warehouse_inactive" => 409,
            _ => 400
        };

        return Results.Problem(statusCode: status, title: status == 403 ? "Access denied" : "Goods Receipt operation failed", detail: "The Goods Receipt operation could not be completed.", type: $"https://api.minierp.local/problems/{code}", extensions: new Dictionary<string, object?> { ["code"] = code, ["correlationId"] = GetCorrelation(httpContext), ["operationId"] = operationId });
    }

    private static ProcurementWarehouseOptionResponse ToWarehouseResponse(ProcurementWarehouseOption option) => new(
        option.TenantId,
        option.CompanyId,
        option.BranchId,
        option.WarehouseId,
        option.Code,
        option.Name,
        option.DisplayName);

    private static GoodsReceiptEligibleSourceResponse ToEligibleSourceResponse(GoodsReceiptEligibleSourceRecord record) => new(
        record.PurchaseOrderId,
        record.Scope.CompanyId,
        record.Scope.BranchId,
        record.Status.ToString(),
        record.SupplierId,
        record.SupplierCode,
        record.SupplierName,
        record.CurrencyCode,
        record.Lines.Select(line => new GoodsReceiptEligibleLineResponse(
            record.PurchaseOrderId,
            line.PurchaseOrderLineId,
            line.ProductId,
            line.ProductSku,
            line.ProductName,
            line.UnitOfMeasureCode,
            line.ConfirmedQuantity,
            line.AlreadyReceivedAcceptedQuantity,
            line.RemainingReceivableQuantity)).ToArray());

    private static GoodsReceiptListItemResponse ToListResponse(GoodsReceiptListRecord record) => new(record.Id, record.TenantId, record.Scope.CompanyId, record.Scope.BranchId, record.PurchaseOrderId, record.WarehouseId, record.Status.ToString(), record.SupplierCode, record.SupplierName, record.ReceivedDate, record.LineCount, record.TotalAcceptedQuantity, record.CreatedAt, Version(record.Version));

    private static GoodsReceiptResponse ToResponse(GoodsReceiptRecord record, ProcurementRequestContext context) => new(
        record.Id,
        record.TenantId,
        record.Scope.CompanyId,
        record.Scope.BranchId,
        record.PurchaseOrderId,
        record.WarehouseId,
        record.ReceivedByActorId,
        record.Status.ToString(),
        record.SupplierId,
        record.SupplierCode,
        record.SupplierName,
        record.ReceivedDate,
        record.ReferenceNote,
        record.Notes,
        record.CreatedAt,
        record.UpdatedAt,
        record.CancelledAt,
        record.CancellationReason,
        record.Lines.Select(ToLineResponse).ToArray(),
        Version(record.Version),
        record.Status == GoodsReceiptStatus.Recorded);

    private static GoodsReceiptLineResponse ToLineResponse(GoodsReceiptLineRecord line) => new(line.Id, line.PurchaseOrderLineId, line.ProductId, line.ProductSku, line.ProductName, line.UnitOfMeasureCode, line.OrderedQuantityAtReceipt, line.ReceivedQuantity, line.AcceptedQuantity, line.RejectedQuantity, line.DamagedQuantity, line.DamageNotes, line.RemainingReceivableQuantityAfter, line.Notes);

    private static GoodsReceiptHistoryResponse ToHistoryResponse(GoodsReceiptHistoryRecord record) => new(record.EvidenceId, record.GoodsReceiptId, record.OccurredAt, record.FromStatus.ToString(), record.ToStatus.ToString(), record.Action.ToString(), record.ActorId, record.Reason, record.CorrelationId);

    private static GoodsReceiptAuditResponse ToAuditResponse(GoodsReceiptAuditRecord record) => new(record.EvidenceId, record.GoodsReceiptId, record.OccurredAt, record.OperationId, record.CorrelationId, record.TenantId, record.ActorId, record.SessionId, record.AuthorizationPath, record.Decision, record.Reason, record.BeforeStatus?.ToString(), record.AfterStatus?.ToString(), record.CompanyId, record.BranchId, record.BeforeSummary, record.AfterSummary, record.IdempotencyKey);

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
    private static string TargetFingerprint(Guid goodsReceiptId) => $"|target:{goodsReceiptId:D}";
    private static string Version(byte[] version) => Convert.ToBase64String(version);

    private static async Task<bool> EnsureAntiforgeryAsync(HttpContext httpContext)
    {
        try { await httpContext.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(httpContext); return true; }
        catch (AntiforgeryValidationException) { return false; }
    }

    private static string GetCorrelation(HttpContext httpContext) => httpContext.Items.TryGetValue(FoundationApiKeys.CorrelationItem, out var value) && value is string correlationId ? correlationId : FoundationCorrelation.Resolve(httpContext.Request);

    private static Task<IResult> WriteProblemAsync(HttpContext httpContext, int statusCode, string code, string title, string detail, string operationId) => Task.FromResult<IResult>(Results.Problem(statusCode: statusCode, title: title, detail: detail, type: $"https://api.minierp.local/problems/{code}", extensions: new Dictionary<string, object?> { ["code"] = code, ["correlationId"] = GetCorrelation(httpContext), ["operationId"] = operationId }));
}

#pragma warning restore CS1591
