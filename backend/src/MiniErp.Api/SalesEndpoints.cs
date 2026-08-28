#pragma warning disable CS1591

using System.Text.Json;
using Microsoft.AspNetCore.Antiforgery;
using MiniErp.App.BuildingBlocks.Rest;
using MiniErp.App.Modules.Procurement;
using MiniErp.App.Modules.Sales;
using MiniErp.Contracts.Modules.Foundation;
using MiniErp.Contracts.Modules.Sales;

namespace MiniErp.Api;

public static class SalesEndpoints
{
    public static void MapSalesEndpoints(this IEndpointRouteBuilder app)
    {
        app.MapGet("/api/v1/sales/quotations", async (HttpContext http, ITrustedRequestContextResolver resolver, ProcurementTenantContextResolver tenantResolver, SalesAuthorizationService authorization, SalesService service, Guid? companyId, SalesQuotationStatus? status) =>
        {
            var (context, denied) = await Resolve(http, resolver, tenantResolver, "sales.quotation.list");
            if (denied is not null || context is null) return denied!;
            if (!AuthorizeListScope(context, authorization, "sales.quotation.list", companyId)) return Problem(403, "permission_denied", "sales.quotation.list");
            return Results.Ok(await service.ListQuotationsAsync(context, companyId, status, http.RequestAborted));
        }).WithName("sales.quotation.list").WithMetadata(Metadata("sales.quotation.list"));

        app.MapGet("/api/v1/sales/quotations/{quotationId:guid}", async (Guid quotationId, HttpContext http, ITrustedRequestContextResolver resolver, ProcurementTenantContextResolver tenantResolver, SalesAuthorizationService authorization, SalesService service) =>
        {
            var (context, denied) = await Resolve(http, resolver, tenantResolver, "sales.quotation.read");
            if (denied is not null || context is null) return denied!;
            var value = await service.GetQuotationAsync(context, quotationId, http.RequestAborted);
            if (value is null) return Problem(404, "quotation_not_found", "sales.quotation.read");
            var resourceDenied = AuthorizeResource(context, authorization, "sales.quotation.read", new SalesScope(value.TenantId, value.CompanyId, value.BranchId), "quotation_not_found");
            if (resourceDenied is not null) return resourceDenied;
            return Results.Ok(value);
        }).WithName("sales.quotation.read").WithMetadata(Metadata("sales.quotation.read"));

        app.MapPost("/api/v1/sales/quotations", async (SalesQuotationCreateRequest request, HttpContext http, ITrustedRequestContextResolver resolver, ProcurementTenantContextResolver tenantResolver, SalesService service) =>
            await ExecuteMutationAsync(http, resolver, tenantResolver, "sales.quotation.create", null, async (context, key, _) => await service.CreateQuotationAsync(context, request, key, http.RequestAborted)))
            .WithName("sales.quotation.create").WithMetadata(Metadata("sales.quotation.create"));

        app.MapPost("/api/v1/sales/quotations/{quotationId:guid}/edit", async (Guid quotationId, SalesQuotationEditRequest request, HttpContext http, ITrustedRequestContextResolver resolver, ProcurementTenantContextResolver tenantResolver, SalesService service) =>
            await ExecuteMutationAsync(http, resolver, tenantResolver, "sales.quotation.edit", quotationId, async (context, key, version) => await service.EditQuotationAsync(context, quotationId, request, version!, key, http.RequestAborted)))
            .WithName("sales.quotation.edit").WithMetadata(Metadata("sales.quotation.edit"));

        app.MapPost("/api/v1/sales/quotations/{quotationId:guid}/submit", async (Guid quotationId, SalesActionRequest? request, HttpContext http, ITrustedRequestContextResolver resolver, ProcurementTenantContextResolver tenantResolver, SalesService service) =>
            await ExecuteMutationAsync(http, resolver, tenantResolver, "sales.quotation.submit", quotationId, async (context, key, version) => await service.TransitionQuotationAsync(context, quotationId, SalesQuotationStatus.PendingApproval, request?.Reason, version!, key, http.RequestAborted)))
            .WithName("sales.quotation.submit").WithMetadata(Metadata("sales.quotation.submit"));

        app.MapPost("/api/v1/sales/quotations/{quotationId:guid}/approve", async (Guid quotationId, SalesActionRequest? request, HttpContext http, ITrustedRequestContextResolver resolver, ProcurementTenantContextResolver tenantResolver, SalesService service) =>
            await ExecuteMutationAsync(http, resolver, tenantResolver, "sales.quotation.approve", quotationId, async (context, key, version) => await service.TransitionQuotationAsync(context, quotationId, SalesQuotationStatus.Approved, request?.Reason, version!, key, http.RequestAborted)))
            .WithName("sales.quotation.approve").WithMetadata(Metadata("sales.quotation.approve"));

        app.MapPost("/api/v1/sales/quotations/{quotationId:guid}/reject", async (Guid quotationId, SalesActionRequest? request, HttpContext http, ITrustedRequestContextResolver resolver, ProcurementTenantContextResolver tenantResolver, SalesService service) =>
            await ExecuteMutationAsync(http, resolver, tenantResolver, "sales.quotation.reject", quotationId, async (context, key, version) => await service.TransitionQuotationAsync(context, quotationId, SalesQuotationStatus.Rejected, request?.Reason, version!, key, http.RequestAborted)))
            .WithName("sales.quotation.reject").WithMetadata(Metadata("sales.quotation.reject"));

        app.MapPost("/api/v1/sales/quotations/{quotationId:guid}/return", async (Guid quotationId, SalesActionRequest? request, HttpContext http, ITrustedRequestContextResolver resolver, ProcurementTenantContextResolver tenantResolver, SalesService service) =>
            await ExecuteMutationAsync(http, resolver, tenantResolver, "sales.quotation.return", quotationId, async (context, key, version) => await service.TransitionQuotationAsync(context, quotationId, SalesQuotationStatus.ReturnedForChange, request?.Reason, version!, key, http.RequestAborted)))
            .WithName("sales.quotation.return").WithMetadata(Metadata("sales.quotation.return"));

        app.MapPost("/api/v1/sales/quotations/{quotationId:guid}/send", async (Guid quotationId, SalesActionRequest? request, HttpContext http, ITrustedRequestContextResolver resolver, ProcurementTenantContextResolver tenantResolver, SalesService service) =>
            await ExecuteMutationAsync(http, resolver, tenantResolver, "sales.quotation.send", quotationId, async (context, key, version) => await service.TransitionQuotationAsync(context, quotationId, SalesQuotationStatus.Sent, request?.Reason, version!, key, http.RequestAborted)))
            .WithName("sales.quotation.send").WithMetadata(Metadata("sales.quotation.send"));

        app.MapPost("/api/v1/sales/quotations/{quotationId:guid}/withdraw", async (Guid quotationId, SalesActionRequest? request, HttpContext http, ITrustedRequestContextResolver resolver, ProcurementTenantContextResolver tenantResolver, SalesService service) =>
            await ExecuteMutationAsync(http, resolver, tenantResolver, "sales.quotation.withdraw", quotationId, async (context, key, version) => await service.TransitionQuotationAsync(context, quotationId, SalesQuotationStatus.Withdrawn, request?.Reason, version!, key, http.RequestAborted)))
            .WithName("sales.quotation.withdraw").WithMetadata(Metadata("sales.quotation.withdraw"));

        app.MapPost("/api/v1/sales/quotations/{quotationId:guid}/cancel", async (Guid quotationId, SalesActionRequest? request, HttpContext http, ITrustedRequestContextResolver resolver, ProcurementTenantContextResolver tenantResolver, SalesService service) =>
            await ExecuteMutationAsync(http, resolver, tenantResolver, "sales.quotation.cancel", quotationId, async (context, key, version) => await service.TransitionQuotationAsync(context, quotationId, SalesQuotationStatus.Cancelled, request?.Reason, version!, key, http.RequestAborted)))
            .WithName("sales.quotation.cancel").WithMetadata(Metadata("sales.quotation.cancel"));

        app.MapPost("/api/v1/sales/quotations/{quotationId:guid}/convert", async (Guid quotationId, SalesActionRequest? request, HttpContext http, ITrustedRequestContextResolver resolver, ProcurementTenantContextResolver tenantResolver, SalesService service) =>
            await ExecuteMutationAsync(http, resolver, tenantResolver, "sales.quotation.convert", quotationId, async (context, key, version) => await service.ConvertQuotationAsync(context, quotationId, version!, key, http.RequestAborted)))
            .WithName("sales.quotation.convert").WithMetadata(Metadata("sales.quotation.convert"));

        app.MapGet("/api/v1/sales/quotations/{quotationId:guid}/revisions", async (Guid quotationId, HttpContext http, ITrustedRequestContextResolver resolver, ProcurementTenantContextResolver tenantResolver, SalesAuthorizationService authorization, SalesService service) =>
            await ReadHistory(http, resolver, tenantResolver, authorization, service, "sales.quotation.revisions.read", "quotation", quotationId, true))
            .WithName("sales.quotation.revisions.read").WithMetadata(Metadata("sales.quotation.revisions.read"));
        app.MapGet("/api/v1/sales/quotations/{quotationId:guid}/history", async (Guid quotationId, HttpContext http, ITrustedRequestContextResolver resolver, ProcurementTenantContextResolver tenantResolver, SalesAuthorizationService authorization, SalesService service) =>
            await ReadHistory(http, resolver, tenantResolver, authorization, service, "sales.quotation.history.read", "quotation", quotationId, false))
            .WithName("sales.quotation.history.read").WithMetadata(Metadata("sales.quotation.history.read"));
        app.MapGet("/api/v1/sales/quotations/{quotationId:guid}/audit", async (Guid quotationId, HttpContext http, ITrustedRequestContextResolver resolver, ProcurementTenantContextResolver tenantResolver, SalesAuthorizationService authorization, SalesService service) =>
            await ReadAudit(http, resolver, tenantResolver, authorization, service, "sales.quotation.audit.read", "quotation", quotationId))
            .WithName("sales.quotation.audit.read").WithMetadata(Metadata("sales.quotation.audit.read"));

        app.MapGet("/api/v1/sales/orders", async (HttpContext http, ITrustedRequestContextResolver resolver, ProcurementTenantContextResolver tenantResolver, SalesAuthorizationService authorization, SalesService service, Guid? companyId, SalesOrderStatus? status) =>
        {
            var (context, denied) = await Resolve(http, resolver, tenantResolver, "sales.order.list");
            if (denied is not null || context is null) return denied!;
            if (!AuthorizeListScope(context, authorization, "sales.order.list", companyId)) return Problem(403, "permission_denied", "sales.order.list");
            return Results.Ok(await service.ListOrdersAsync(context, companyId, status, http.RequestAborted));
        }).WithName("sales.order.list").WithMetadata(Metadata("sales.order.list"));

        app.MapGet("/api/v1/sales/orders/{orderId:guid}", async (Guid orderId, HttpContext http, ITrustedRequestContextResolver resolver, ProcurementTenantContextResolver tenantResolver, SalesAuthorizationService authorization, SalesService service) =>
        {
            var (context, denied) = await Resolve(http, resolver, tenantResolver, "sales.order.read");
            if (denied is not null || context is null) return denied!;
            var value = await service.GetOrderAsync(context, orderId, http.RequestAborted);
            if (value is null) return Problem(404, "order_not_found", "sales.order.read");
            var resourceDenied = AuthorizeResource(context, authorization, "sales.order.read", new SalesScope(value.TenantId, value.CompanyId, value.BranchId), "order_not_found");
            if (resourceDenied is not null) return resourceDenied;
            return Results.Ok(value);
        }).WithName("sales.order.read").WithMetadata(Metadata("sales.order.read"));

        app.MapPost("/api/v1/sales/orders/{orderId:guid}/edit", async (Guid orderId, SalesOrderEditRequest request, HttpContext http, ITrustedRequestContextResolver resolver, ProcurementTenantContextResolver tenantResolver, SalesService service) =>
            await ExecuteMutationAsync(http, resolver, tenantResolver, "sales.order.edit", orderId, async (context, key, version) => await service.EditOrderAsync(context, orderId, request, version!, key, http.RequestAborted)))
            .WithName("sales.order.edit").WithMetadata(Metadata("sales.order.edit"));

        app.MapPost("/api/v1/sales/orders/{orderId:guid}/submit", async (Guid orderId, SalesActionRequest? request, HttpContext http, ITrustedRequestContextResolver resolver, ProcurementTenantContextResolver tenantResolver, SalesService service) =>
            await ExecuteMutationAsync(http, resolver, tenantResolver, "sales.order.submit", orderId, async (context, key, version) => await service.TransitionOrderAsync(context, orderId, SalesOrderStatus.PendingApproval, request?.Reason, version!, key, http.RequestAborted)))
            .WithName("sales.order.submit").WithMetadata(Metadata("sales.order.submit"));

        app.MapPost("/api/v1/sales/orders/{orderId:guid}/approve", async (Guid orderId, SalesActionRequest? request, HttpContext http, ITrustedRequestContextResolver resolver, ProcurementTenantContextResolver tenantResolver, SalesService service) =>
            await ExecuteMutationAsync(http, resolver, tenantResolver, "sales.order.approve", orderId, async (context, key, version) => await service.TransitionOrderAsync(context, orderId, SalesOrderStatus.Approved, request?.Reason, version!, key, http.RequestAborted)))
            .WithName("sales.order.approve").WithMetadata(Metadata("sales.order.approve"));

        app.MapPost("/api/v1/sales/orders/{orderId:guid}/reject", async (Guid orderId, SalesActionRequest? request, HttpContext http, ITrustedRequestContextResolver resolver, ProcurementTenantContextResolver tenantResolver, SalesService service) =>
            await ExecuteMutationAsync(http, resolver, tenantResolver, "sales.order.reject", orderId, async (context, key, version) => await service.TransitionOrderAsync(context, orderId, SalesOrderStatus.Rejected, request?.Reason, version!, key, http.RequestAborted)))
            .WithName("sales.order.reject").WithMetadata(Metadata("sales.order.reject"));

        app.MapPost("/api/v1/sales/orders/{orderId:guid}/return", async (Guid orderId, SalesActionRequest? request, HttpContext http, ITrustedRequestContextResolver resolver, ProcurementTenantContextResolver tenantResolver, SalesService service) =>
            await ExecuteMutationAsync(http, resolver, tenantResolver, "sales.order.return", orderId, async (context, key, version) => await service.TransitionOrderAsync(context, orderId, SalesOrderStatus.ReturnedForChange, request?.Reason, version!, key, http.RequestAborted)))
            .WithName("sales.order.return").WithMetadata(Metadata("sales.order.return"));

        app.MapPost("/api/v1/sales/orders/{orderId:guid}/confirm", async (Guid orderId, SalesActionRequest? request, HttpContext http, ITrustedRequestContextResolver resolver, ProcurementTenantContextResolver tenantResolver, SalesService service) =>
            await ExecuteMutationAsync(http, resolver, tenantResolver, "sales.order.confirm", orderId, async (context, key, version) => await service.TransitionOrderAsync(context, orderId, SalesOrderStatus.Confirmed, request?.Reason, version!, key, http.RequestAborted)))
            .WithName("sales.order.confirm").WithMetadata(Metadata("sales.order.confirm"));

        app.MapPost("/api/v1/sales/orders/{orderId:guid}/cancel", async (Guid orderId, SalesActionRequest? request, HttpContext http, ITrustedRequestContextResolver resolver, ProcurementTenantContextResolver tenantResolver, SalesService service) =>
            await ExecuteMutationAsync(http, resolver, tenantResolver, "sales.order.cancel", orderId, async (context, key, version) => await service.TransitionOrderAsync(context, orderId, SalesOrderStatus.Cancelled, request?.Reason, version!, key, http.RequestAborted)))
            .WithName("sales.order.cancel").WithMetadata(Metadata("sales.order.cancel"));

        app.MapGet("/api/v1/sales/orders/{orderId:guid}/credit", async (Guid orderId, HttpContext http, ITrustedRequestContextResolver resolver, ProcurementTenantContextResolver tenantResolver, SalesAuthorizationService authorization, SalesService service) =>
        {
            var (context, denied) = await Resolve(http, resolver, tenantResolver, "sales.order.credit.read");
            if (denied is not null || context is null) return denied!;
            var order = await service.GetOrderAsync(context, orderId, http.RequestAborted);
            if (order is null) return Problem(404, "order_not_found", "sales.order.credit.read");
            var resourceDenied = AuthorizeResource(context, authorization, "sales.order.credit.read", new SalesScope(order.TenantId, order.CompanyId, order.BranchId), "order_not_found");
            if (resourceDenied is not null) return resourceDenied;
            return Results.Ok(await service.GetOrderCreditAsync(context, orderId, http.RequestAborted));
        }).WithName("sales.order.credit.read").WithMetadata(Metadata("sales.order.credit.read"));

        app.MapPost("/api/v1/sales/orders/{orderId:guid}/credit/override", async (Guid orderId, SalesCreditOverrideRequest request, HttpContext http, ITrustedRequestContextResolver resolver, ProcurementTenantContextResolver tenantResolver, SalesService service) =>
            await ExecuteMutationAsync(http, resolver, tenantResolver, "sales.order.credit.override", orderId, async (context, key, version) => await service.OverrideCreditAsync(context, orderId, request, version!, key, http.RequestAborted)))
            .WithName("sales.order.credit.override").WithMetadata(Metadata("sales.order.credit.override"));

        app.MapGet("/api/v1/sales/orders/{orderId:guid}/history", async (Guid orderId, HttpContext http, ITrustedRequestContextResolver resolver, ProcurementTenantContextResolver tenantResolver, SalesAuthorizationService authorization, SalesService service) =>
            await ReadHistory(http, resolver, tenantResolver, authorization, service, "sales.order.history.read", "order", orderId, false))
            .WithName("sales.order.history.read").WithMetadata(Metadata("sales.order.history.read"));
        app.MapGet("/api/v1/sales/orders/{orderId:guid}/audit", async (Guid orderId, HttpContext http, ITrustedRequestContextResolver resolver, ProcurementTenantContextResolver tenantResolver, SalesAuthorizationService authorization, SalesService service) =>
            await ReadAudit(http, resolver, tenantResolver, authorization, service, "sales.order.audit.read", "order", orderId))
            .WithName("sales.order.audit.read").WithMetadata(Metadata("sales.order.audit.read"));
    }

    private static async Task<IResult> ReadHistory(HttpContext http, ITrustedRequestContextResolver resolver, ProcurementTenantContextResolver tenantResolver, SalesAuthorizationService authorization, SalesService service, string operation, string type, Guid id, bool revisions)
    {
        var (context, denied) = await Resolve(http, resolver, tenantResolver, operation);
        if (denied is not null || context is null) return denied!;
        object? document = type == "quotation" ? await service.GetQuotationAsync(context, id, http.RequestAborted) : await service.GetOrderAsync(context, id, http.RequestAborted);
        if (document is null) return Problem(404, "document_not_found", operation);
        var scope = type == "quotation" ? new SalesScope(((SalesQuotationResponse)document).TenantId, ((SalesQuotationResponse)document).CompanyId, ((SalesQuotationResponse)document).BranchId) : new SalesScope(((SalesOrderResponse)document).TenantId, ((SalesOrderResponse)document).CompanyId, ((SalesOrderResponse)document).BranchId);
        var resourceDenied = AuthorizeResource(context, authorization, operation, scope, "document_not_found");
        if (resourceDenied is not null) return resourceDenied;
        return revisions ? Results.Ok(await service.ListQuotationRevisionsAsync(context, id, http.RequestAborted)) : Results.Ok(await service.ListHistoryAsync(context, type, id, http.RequestAborted));
    }

    private static async Task<IResult> ReadAudit(HttpContext http, ITrustedRequestContextResolver resolver, ProcurementTenantContextResolver tenantResolver, SalesAuthorizationService authorization, SalesService service, string operation, string type, Guid id)
    {
        var (context, denied) = await Resolve(http, resolver, tenantResolver, operation);
        if (denied is not null || context is null) return denied!;
        object? document = type == "quotation" ? await service.GetQuotationAsync(context, id, http.RequestAborted) : await service.GetOrderAsync(context, id, http.RequestAborted);
        if (document is null) return Problem(404, "document_not_found", operation);
        var scope = type == "quotation" ? new SalesScope(((SalesQuotationResponse)document).TenantId, ((SalesQuotationResponse)document).CompanyId, ((SalesQuotationResponse)document).BranchId) : new SalesScope(((SalesOrderResponse)document).TenantId, ((SalesOrderResponse)document).CompanyId, ((SalesOrderResponse)document).BranchId);
        var resourceDenied = AuthorizeResource(context, authorization, operation, scope, "document_not_found");
        if (resourceDenied is not null) return resourceDenied;
        return Results.Ok(await service.ListAuditAsync(context, type, id, http.RequestAborted));
    }

    private static async Task<(ProcurementRequestContext? Context, IResult? Denied)> Resolve(HttpContext http, ITrustedRequestContextResolver resolver, ProcurementTenantContextResolver tenantResolver, string operation)
    {
        var foundation = await resolver.ResolveAsync(http, http.RequestAborted);
        var resolution = tenantResolver.Resolve(foundation);
        if (!resolution.Allowed || resolution.Context is null) return (null, Problem(foundation.SecurityProfile == FoundationSecurityProfile.Anonymous ? 401 : 403, resolution.Code, operation));
        return (resolution.Context, null);
    }

    private static async Task<IResult> ExecuteMutationAsync<T>(HttpContext http, ITrustedRequestContextResolver resolver, ProcurementTenantContextResolver tenantResolver, string operation, Guid? targetId, Func<ProcurementRequestContext, string, byte[]?, Task<SalesOperationResult<T>>> action)
    {
        if (!await Antiforgery(http)) return Problem(403, "antiforgery_failed", operation);
        var key = http.Request.Headers["Idempotency-Key"].FirstOrDefault();
        if (!FoundationCorrelation.IsValid(key)) return Problem(400, "idempotency_key_invalid", operation);
        var version = ReadVersion(http);
        if (targetId is not null && version is null) return Problem(400, "version_required", operation);
        var (context, denied) = await Resolve(http, resolver, tenantResolver, operation);
        if (denied is not null || context is null) return denied!;
        var result = await action(context, key!, version);
        if (!result.Succeeded || result.Value is null) return Problem(Status(result.Code), result.Code, operation);
        if (GetVersion(result.Value) is { Length: > 0 } responseVersion) http.Response.Headers.ETag = $"\"{Convert.ToBase64String(responseVersion)}\"";
        return Results.Ok(result.Value);
    }

    private static byte[]? ReadVersion(HttpContext http)
    {
        var value = http.Request.Headers.IfMatch.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(value) || value.StartsWith("W/", StringComparison.OrdinalIgnoreCase)) return null;
        value = value.Trim(); if (value.Length > 1 && value[0] == '"' && value[^1] == '"') value = value[1..^1];
        try { var bytes = Convert.FromBase64String(value); return bytes.Length is > 0 and <= 64 ? bytes : null; } catch (FormatException) { return null; }
    }

    private static byte[]? GetVersion(object value) => value switch { SalesQuotationResponse quote => quote.Version, SalesOrderResponse order => order.Version, _ => null };
    private static int Status(string code) => code switch { "permission_denied" or "self_approval_denied" or "approver_not_eligible" => 403, "quotation_not_found" or "order_not_found" => 404, "concurrency_conflict" or "idempotency_conflict" or "quotation_transition_invalid" or "order_transition_invalid" or "quotation_edit_locked" or "order_edit_locked" or "quotation_scope_immutable" or "order_scope_immutable" or "quotation_revision_already_converted" or "quotation_expired" or "credit_override_not_allowed" or "approval_state_missing" or "approval_already_recorded" or "approval_sod_violation" or "approval_already_complete" or "delegation_invalid" or "cancellation_not_allowed" => 409, "sales_persistence_unavailable" or "credit_truth_unavailable" => 503, _ => 400 };
    private static async Task<bool> Antiforgery(HttpContext http) { try { await http.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(http); return true; } catch (AntiforgeryValidationException) { return false; } }
    private static FoundationOperationMetadata Metadata(string operation) => new(FoundationOperationCatalog.GetRequired(operation));
    private static bool AuthorizeListScope(ProcurementRequestContext context, SalesAuthorizationService authorization, string operation, Guid? companyId) =>
        authorization.Decide(context, operation, companyId is null ? null : new SalesScope(context.TenantId.Value, companyId.Value, null)).Allowed;
    private static IResult? AuthorizeResource(ProcurementRequestContext context, SalesAuthorizationService authorization, string operation, SalesScope scope, string notFoundCode)
    {
        var decision = authorization.Decide(context, operation, scope);
        if (decision.Allowed) return null;
        var status = decision.Code is "resource_scope_denied" or "cross_tenant_target_denied" ? 404 : 403;
        return Problem(status, status == 404 ? notFoundCode : decision.Code, operation);
    }
    private static IResult Problem(int status, string code, string operation) => Results.Problem(statusCode: status, title: code, detail: "The Sales operation could not be completed.", extensions: new Dictionary<string, object?> { ["code"] = code, ["operationId"] = operation });
}

#pragma warning restore CS1591
