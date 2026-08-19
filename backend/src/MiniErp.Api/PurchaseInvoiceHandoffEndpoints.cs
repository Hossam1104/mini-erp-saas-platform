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

public static class PurchaseInvoiceHandoffEndpoints
{
    public static IEndpointRouteBuilder MapPurchaseInvoiceHandoffEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapGet(
                "/api/v1/procurement/purchase-invoice-handoff-sources",
                async (HttpContext httpContext, ITrustedRequestContextResolver resolver, ProcurementTenantContextResolver tenantResolver, PurchaseInvoiceHandoffService service) =>
                    await ExecuteReadAsync(
                        httpContext,
                        resolver,
                        tenantResolver,
                        FoundationOperationCatalog.GetRequired("procurement.invoice-handoff.eligible-source.list"),
                        context => service.ListEligibleSourcesAsync(context, httpContext.RequestAborted),
                        (_, records) => records.Select(ToEligibleSourceResponse).ToArray()))
            .WithName("procurement.invoice-handoff.eligible-source.list")
            .WithTags("Procurement / Purchase Invoice Handoffs")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("procurement.invoice-handoff.eligible-source.list")));

        endpoints.MapGet(
                "/api/v1/procurement/purchase-invoice-handoffs",
                async (HttpContext httpContext, ITrustedRequestContextResolver resolver, ProcurementTenantContextResolver tenantResolver, PurchaseInvoiceHandoffService service) =>
                {
                    PurchaseInvoiceHandoffStatus? status = null;
                    var parsed = default(PurchaseInvoiceHandoffStatus);
                    var rawStatus = httpContext.Request.Query["status"].FirstOrDefault();
                    if (!string.IsNullOrWhiteSpace(rawStatus) && !Enum.TryParse(rawStatus, true, out parsed))
                    {
                        return await WriteProblemAsync(httpContext, 400, "validation_failed", "Validation failed", "The Purchase Invoice Handoff status filter is invalid.", "procurement.invoice-handoff.list");
                    }

                    if (!string.IsNullOrWhiteSpace(rawStatus))
                    {
                        status = parsed;
                    }

                    return await ExecuteReadAsync(
                        httpContext,
                        resolver,
                        tenantResolver,
                        FoundationOperationCatalog.GetRequired("procurement.invoice-handoff.list"),
                        context => service.ListAsync(context, status, httpContext.RequestAborted),
                        (_, records) => records.Select(ToListResponse).ToArray());
                })
            .WithName("procurement.invoice-handoff.list")
            .WithTags("Procurement / Purchase Invoice Handoffs")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("procurement.invoice-handoff.list")));

        endpoints.MapGet(
                "/api/v1/procurement/purchase-invoice-handoffs/{purchaseInvoiceHandoffId:guid}",
                async (Guid purchaseInvoiceHandoffId, HttpContext httpContext, ITrustedRequestContextResolver resolver, ProcurementTenantContextResolver tenantResolver, PurchaseInvoiceHandoffService service) =>
                    await ExecuteReadAsync(
                        httpContext,
                        resolver,
                        tenantResolver,
                        FoundationOperationCatalog.GetRequired("procurement.invoice-handoff.read"),
                        context => service.GetAsync(context, purchaseInvoiceHandoffId, httpContext.RequestAborted),
                        (context, record) => ToResponse(record, context),
                        setEtag: true))
            .WithName("procurement.invoice-handoff.read")
            .WithTags("Procurement / Purchase Invoice Handoffs")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("procurement.invoice-handoff.read")));

        endpoints.MapPost(
                "/api/v1/procurement/purchase-invoice-handoffs",
                async (PurchaseInvoiceHandoffCreateRequest? request, HttpContext httpContext, ITrustedRequestContextResolver resolver, ProcurementTenantContextResolver tenantResolver, FoundationAuditCoordinator auditCoordinator, LocalMasterDataIdempotencyStore idempotencyStore, PurchaseInvoiceHandoffService service) =>
                {
                    if (request is null)
                    {
                        return await WriteProblemAsync(httpContext, 400, "validation_failed", "Validation failed", "A Purchase Invoice Handoff body is required.", "procurement.invoice-handoff.create");
                    }

                    var fingerprint = Fingerprint(request);
                    return await ExecuteMutationAsync(
                        httpContext,
                        resolver,
                        tenantResolver,
                        auditCoordinator,
                        idempotencyStore,
                        FoundationOperationCatalog.GetRequired("procurement.invoice-handoff.create"),
                        fingerprint,
                        context => service.CreateAsync(context, request, GetIdempotencyKey(httpContext), fingerprint, httpContext.RequestAborted),
                        (context, record) => ToResponse(record, context),
                        setEtag: true);
                })
            .WithName("procurement.invoice-handoff.create")
            .WithTags("Procurement / Purchase Invoice Handoffs")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("procurement.invoice-handoff.create")));

        endpoints.MapPost(
                "/api/v1/procurement/purchase-invoice-handoffs/{purchaseInvoiceHandoffId:guid}/cancel",
                async (Guid purchaseInvoiceHandoffId, PurchaseInvoiceHandoffActionRequest? request, HttpContext httpContext, ITrustedRequestContextResolver resolver, ProcurementTenantContextResolver tenantResolver, FoundationAuditCoordinator auditCoordinator, LocalMasterDataIdempotencyStore idempotencyStore, PurchaseInvoiceHandoffService service) =>
                {
                    if (!TryReadExpectedVersion(httpContext, out var expectedVersion))
                    {
                        return await WriteProblemAsync(httpContext, 400, "validation_failed", "Validation failed", "A valid If-Match version is required.", "procurement.invoice-handoff.cancel");
                    }

                    var fingerprint = Fingerprint(request) + VersionFingerprint(expectedVersion) + TargetFingerprint(purchaseInvoiceHandoffId);
                    return await ExecuteMutationAsync(
                        httpContext,
                        resolver,
                        tenantResolver,
                        auditCoordinator,
                        idempotencyStore,
                        FoundationOperationCatalog.GetRequired("procurement.invoice-handoff.cancel"),
                        fingerprint,
                        context => service.CancelAsync(context, purchaseInvoiceHandoffId, expectedVersion, request?.Reason, GetIdempotencyKey(httpContext), fingerprint, httpContext.RequestAborted),
                        (context, record) => ToResponse(record, context),
                        setEtag: true,
                        requireExpectedVersion: true);
                })
            .WithName("procurement.invoice-handoff.cancel")
            .WithTags("Procurement / Purchase Invoice Handoffs")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("procurement.invoice-handoff.cancel")));

        endpoints.MapGet(
                "/api/v1/procurement/purchase-invoice-handoffs/{purchaseInvoiceHandoffId:guid}/history",
                async (Guid purchaseInvoiceHandoffId, HttpContext httpContext, ITrustedRequestContextResolver resolver, ProcurementTenantContextResolver tenantResolver, PurchaseInvoiceHandoffService service) =>
                    await ExecuteReadAsync(
                        httpContext,
                        resolver,
                        tenantResolver,
                        FoundationOperationCatalog.GetRequired("procurement.invoice-handoff.history.read"),
                        context => service.ReadHistoryAsync(context, purchaseInvoiceHandoffId, httpContext.RequestAborted),
                        (_, records) => records.Select(ToHistoryResponse).ToArray()))
            .WithName("procurement.invoice-handoff.history.read")
            .WithTags("Procurement / Purchase Invoice Handoffs")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("procurement.invoice-handoff.history.read")));

        endpoints.MapGet(
                "/api/v1/procurement/purchase-invoice-handoffs/{purchaseInvoiceHandoffId:guid}/audit",
                async (Guid purchaseInvoiceHandoffId, HttpContext httpContext, ITrustedRequestContextResolver resolver, ProcurementTenantContextResolver tenantResolver, PurchaseInvoiceHandoffService service) =>
                    await ExecuteReadAsync(
                        httpContext,
                        resolver,
                        tenantResolver,
                        FoundationOperationCatalog.GetRequired("procurement.invoice-handoff.audit.read"),
                        context => service.ReadAuditAsync(context, purchaseInvoiceHandoffId, httpContext.RequestAborted),
                        (_, records) => records.Select(ToAuditResponse).ToArray()))
            .WithName("procurement.invoice-handoff.audit.read")
            .WithTags("Procurement / Purchase Invoice Handoffs")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("procurement.invoice-handoff.audit.read")));

        return endpoints;
    }

    private static async Task<IResult> ExecuteReadAsync<T>(
        HttpContext httpContext,
        ITrustedRequestContextResolver resolver,
        ProcurementTenantContextResolver tenantResolver,
        FoundationOperationDescriptor descriptor,
        Func<ProcurementRequestContext, Task<PurchaseInvoiceHandoffOperationResult<T>>> operation,
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
        Func<ProcurementRequestContext, Task<PurchaseInvoiceHandoffOperationResult<T>>> operation,
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
            return ToResult(httpContext, PurchaseInvoiceHandoffOperationResult<T>.Success(replay), descriptor.OperationId, context, map, setEtag);
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
                operationVersion: "procurement.invoice-handoff.v1",
                cancellationToken: httpContext.RequestAborted);
            if (!execution.Succeeded || execution.Value is null)
            {
                return await WriteProblemAsync(httpContext, 503, execution.Code, "Operation unavailable", "The Purchase Invoice Handoff operation could not be completed.", descriptor.OperationId);
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

    private static IResult ToResult<T>(HttpContext httpContext, PurchaseInvoiceHandoffOperationResult<T> result, string operationId, ProcurementRequestContext context, Func<ProcurementRequestContext, T, object?> map, bool setEtag)
    {
        if (result.Succeeded && result.Value is not null)
        {
            if (setEtag && result.Value is PurchaseInvoiceHandoffRecord record)
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
            "invoice_handoff_not_found" => 404,
            "concurrency_conflict" or "idempotency_conflict" or "invoice_handoff_duplicate" or "invoice_handoff_source_not_eligible" or "invoice_handoff_line_not_eligible" or "over_handoff_not_allowed" or "cancel_not_allowed" or "reason_required" => 409,
            _ => 400
        };

        return Results.Problem(statusCode: status, title: status == 403 ? "Access denied" : "Purchase Invoice Handoff operation failed", detail: "The Purchase Invoice Handoff operation could not be completed.", type: $"https://api.minierp.local/problems/{code}", extensions: new Dictionary<string, object?> { ["code"] = code, ["correlationId"] = GetCorrelation(httpContext), ["operationId"] = operationId });
    }

    private static PurchaseInvoiceHandoffEligibleSourceResponse ToEligibleSourceResponse(PurchaseInvoiceHandoffEligibleSourceRecord record) => new(
        record.PurchaseOrderId,
        record.Scope.CompanyId,
        record.Scope.BranchId,
        record.SupplierId,
        record.SupplierCode,
        record.SupplierName,
        record.CurrencyCode,
        record.Lines.Select(line => new PurchaseInvoiceHandoffEligibleLineResponse(
            line.GoodsReceiptId,
            line.GoodsReceiptLineId,
            line.PurchaseOrderLineId,
            line.ProductId,
            line.ProductSku,
            line.ProductName,
            line.UnitOfMeasureCode,
            line.ReceivedDate,
            line.AcceptedQuantity,
            line.AlreadyHandedOffQuantity,
            line.RemainingHandoffQuantity,
            line.UnitPrice)).ToArray());

    private static PurchaseInvoiceHandoffListItemResponse ToListResponse(PurchaseInvoiceHandoffListRecord record) => new(record.Id, record.TenantId, record.Scope.CompanyId, record.Scope.BranchId, record.PurchaseOrderId, record.Status.ToString(), record.SupplierCode, record.SupplierName, record.CurrencyCode, record.Total, record.LineCount, record.CreatedAt, Version(record.Version));

    private static PurchaseInvoiceHandoffResponse ToResponse(PurchaseInvoiceHandoffRecord record, ProcurementRequestContext context) => new(
        record.Id,
        record.TenantId,
        record.Scope.CompanyId,
        record.Scope.BranchId,
        record.PurchaseOrderId,
        record.CreatedByActorId,
        record.Status.ToString(),
        record.SupplierId,
        record.SupplierCode,
        record.SupplierName,
        record.CurrencyCode,
        record.SupplierInvoiceReference,
        record.SupplierInvoiceDate,
        record.Notes,
        record.Lines.Sum(line => line.LineAmount),
        record.CreatedAt,
        record.UpdatedAt,
        record.CancelledAt,
        record.CancellationReason,
        record.Lines.Select(ToLineResponse).ToArray(),
        record.Sources.Select(ToSourceResponse).ToArray(),
        Version(record.Version),
        record.Status == PurchaseInvoiceHandoffStatus.Recorded);

    private static PurchaseInvoiceHandoffLineResponse ToLineResponse(PurchaseInvoiceHandoffLineRecord line) => new(line.Id, line.PurchaseOrderLineId, line.ProductId, line.ProductSku, line.ProductName, line.UnitOfMeasureCode, line.HandoffQuantity, line.UnitPrice, line.TaxRatePercentage, line.TaxAmount, line.LineAmount);

    private static PurchaseInvoiceHandoffSourceResponse ToSourceResponse(PurchaseInvoiceHandoffSourceRecord source) => new(source.Id, source.GoodsReceiptId, source.GoodsReceiptLineId, source.PurchaseOrderLineId, source.Quantity);

    private static PurchaseInvoiceHandoffHistoryResponse ToHistoryResponse(PurchaseInvoiceHandoffHistoryRecord record) => new(record.EvidenceId, record.PurchaseInvoiceHandoffId, record.OccurredAt, record.FromStatus.ToString(), record.ToStatus.ToString(), record.Action.ToString(), record.ActorId, record.Reason, record.CorrelationId);

    private static PurchaseInvoiceHandoffAuditResponse ToAuditResponse(PurchaseInvoiceHandoffAuditRecord record) => new(record.EvidenceId, record.PurchaseInvoiceHandoffId, record.OccurredAt, record.OperationId, record.CorrelationId, record.TenantId, record.ActorId, record.SessionId, record.AuthorizationPath, record.Decision, record.Reason, record.BeforeStatus?.ToString(), record.AfterStatus?.ToString(), record.CompanyId, record.BranchId, record.BeforeSummary, record.AfterSummary, record.IdempotencyKey);

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
    private static string TargetFingerprint(Guid purchaseInvoiceHandoffId) => $"|target:{purchaseInvoiceHandoffId:D}";
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
