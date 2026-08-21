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

public static class PurchaseInvoiceMatchingEndpoints
{
    public static IEndpointRouteBuilder MapPurchaseInvoiceMatchingEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapGet("/api/v1/procurement/purchase-invoice-matches", async (HttpContext httpContext, ITrustedRequestContextResolver resolver, ProcurementTenantContextResolver tenantResolver, PurchaseInvoiceMatchService service) =>
        {
            Guid? handoffId = Guid.TryParse(httpContext.Request.Query["handoffId"].FirstOrDefault(), out var parsedHandoff) ? parsedHandoff : null;
            PurchaseInvoiceMatchResult? result = null;
            var rawResult = httpContext.Request.Query["result"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(rawResult) && !Enum.TryParse(rawResult, true, out PurchaseInvoiceMatchResult parsedResult))
            {
                return await WriteProblemAsync(httpContext, 400, "validation_failed", "Validation failed", "The matching result filter is invalid.", "procurement.matching.list");
            }
            if (!string.IsNullOrWhiteSpace(rawResult)) result = Enum.Parse<PurchaseInvoiceMatchResult>(rawResult, true);
            return await ExecuteReadAsync(httpContext, resolver, tenantResolver, FoundationOperationCatalog.GetRequired("procurement.matching.list"), context => service.ListAsync(context, handoffId, result, httpContext.RequestAborted), (_, records) => records.Select(ToListResponse).ToArray());
        }).WithName("procurement.matching.list").WithTags("Procurement / Three-way Matching").WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("procurement.matching.list")));

        endpoints.MapGet("/api/v1/procurement/purchase-invoice-matches/{matchEvaluationId:guid}", async (Guid matchEvaluationId, HttpContext httpContext, ITrustedRequestContextResolver resolver, ProcurementTenantContextResolver tenantResolver, PurchaseInvoiceMatchService service) =>
            await ExecuteReadAsync(httpContext, resolver, tenantResolver, FoundationOperationCatalog.GetRequired("procurement.matching.read"), context => service.GetAsync(context, matchEvaluationId, httpContext.RequestAborted), (_, record) => ToResponse(record), setEtag: true))
            .WithName("procurement.matching.read").WithTags("Procurement / Three-way Matching").WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("procurement.matching.read")));

        endpoints.MapPost("/api/v1/procurement/purchase-invoice-handoffs/{purchaseInvoiceHandoffId:guid}/evaluate-match", async (Guid purchaseInvoiceHandoffId, PurchaseInvoiceMatchEvaluateRequest? request, HttpContext httpContext, ITrustedRequestContextResolver resolver, ProcurementTenantContextResolver tenantResolver, FoundationAuditCoordinator auditCoordinator, LocalMasterDataIdempotencyStore idempotencyStore, PurchaseInvoiceMatchService service) =>
        {
            if (!TryReadExpectedVersion(httpContext, out var expectedVersion)) return await WriteProblemAsync(httpContext, 400, "validation_failed", "Validation failed", "A valid If-Match version is required.", "procurement.matching.evaluate");
            var body = request ?? new PurchaseInvoiceMatchEvaluateRequest();
            var fingerprint = Fingerprint(body) + VersionFingerprint(expectedVersion) + TargetFingerprint(purchaseInvoiceHandoffId);
            return await ExecuteMutationAsync(httpContext, resolver, tenantResolver, auditCoordinator, idempotencyStore, FoundationOperationCatalog.GetRequired("procurement.matching.evaluate"), fingerprint,
                context => service.EvaluateAsync(context, purchaseInvoiceHandoffId, expectedVersion, body, GetIdempotencyKey(httpContext), fingerprint, httpContext.RequestAborted), (_, record) => ToResponse(record), setEtag: false, requireExpectedVersion: true);
        }).WithName("procurement.matching.evaluate").WithTags("Procurement / Three-way Matching").WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("procurement.matching.evaluate")));

        endpoints.MapPost("/api/v1/procurement/purchase-invoice-matches/{matchEvaluationId:guid}/resolve-exception", async (Guid matchEvaluationId, PurchaseInvoiceMatchResolveRequest? request, HttpContext httpContext, ITrustedRequestContextResolver resolver, ProcurementTenantContextResolver tenantResolver, FoundationAuditCoordinator auditCoordinator, LocalMasterDataIdempotencyStore idempotencyStore, PurchaseInvoiceMatchService service) =>
        {
            if (!TryReadExpectedVersion(httpContext, out var expectedVersion)) return await WriteProblemAsync(httpContext, 400, "validation_failed", "Validation failed", "A valid If-Match version is required.", "procurement.matching.resolve-exception");
            var fingerprint = Fingerprint(request) + VersionFingerprint(expectedVersion) + TargetFingerprint(matchEvaluationId);
            return await ExecuteMutationAsync(httpContext, resolver, tenantResolver, auditCoordinator, idempotencyStore, FoundationOperationCatalog.GetRequired("procurement.matching.resolve-exception"), fingerprint,
                context => service.ResolveAsync(context, matchEvaluationId, expectedVersion, request!, GetIdempotencyKey(httpContext), fingerprint, httpContext.RequestAborted), (_, record) => ToResponse(record), setEtag: true, requireExpectedVersion: true);
        }).WithName("procurement.matching.resolve-exception").WithTags("Procurement / Three-way Matching").WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("procurement.matching.resolve-exception")));

        endpoints.MapGet("/api/v1/procurement/purchase-invoice-matches/{matchEvaluationId:guid}/history", async (Guid matchEvaluationId, HttpContext httpContext, ITrustedRequestContextResolver resolver, ProcurementTenantContextResolver tenantResolver, PurchaseInvoiceMatchService service) =>
            await ExecuteReadAsync(httpContext, resolver, tenantResolver, FoundationOperationCatalog.GetRequired("procurement.matching.history.read"), context => service.ReadHistoryAsync(context, matchEvaluationId, httpContext.RequestAborted), (_, records) => records.Select(item => new PurchaseInvoiceMatchHistoryResponse(item.Id, item.MatchEvaluationId, item.PurchaseInvoiceHandoffId, item.Result, item.Action, item.ActorId, item.Reason, item.OccurredAt, item.CorrelationId)).ToArray()))
            .WithName("procurement.matching.history.read").WithTags("Procurement / Three-way Matching").WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("procurement.matching.history.read")));

        endpoints.MapGet("/api/v1/procurement/purchase-invoice-matches/{matchEvaluationId:guid}/audit", async (Guid matchEvaluationId, HttpContext httpContext, ITrustedRequestContextResolver resolver, ProcurementTenantContextResolver tenantResolver, PurchaseInvoiceMatchService service) =>
            await ExecuteReadAsync(httpContext, resolver, tenantResolver, FoundationOperationCatalog.GetRequired("procurement.matching.audit.read"), context => service.ReadAuditAsync(context, matchEvaluationId, httpContext.RequestAborted), (_, records) => records.Select(item => new PurchaseInvoiceMatchAuditResponse(item.Id, item.MatchEvaluationId, item.PurchaseInvoiceHandoffId, item.OperationId, item.TenantId, item.ActorId, item.Decision, item.Reason, item.OccurredAt, item.IdempotencyKey, item.RequestFingerprint)).ToArray()))
            .WithName("procurement.matching.audit.read").WithTags("Procurement / Three-way Matching").WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("procurement.matching.audit.read")));

        return endpoints;
    }

    private static async Task<IResult> ExecuteReadAsync<T>(HttpContext httpContext, ITrustedRequestContextResolver resolver, ProcurementTenantContextResolver tenantResolver, FoundationOperationDescriptor descriptor, Func<ProcurementRequestContext, Task<PurchaseInvoiceMatchOperationResult<T>>> operation, Func<ProcurementRequestContext, T, object?> map, bool setEtag = false)
    {
        var foundationContext = await resolver.ResolveAsync(httpContext, httpContext.RequestAborted);
        var resolution = tenantResolver.Resolve(foundationContext);
        if (!resolution.Allowed || resolution.Context is null) return await WriteProblemAsync(httpContext, foundationContext.SecurityProfile == FoundationSecurityProfile.Anonymous ? 401 : 403, resolution.Code, foundationContext.SecurityProfile == FoundationSecurityProfile.Anonymous ? "Authentication required" : "Access denied", "The operation is not available for this security context.", descriptor.OperationId);
        var context = resolution.Context;
        var result = await operation(context);
        return ToResult(httpContext, result, descriptor.OperationId, context, map, setEtag);
    }

    private static async Task<IResult> ExecuteMutationAsync<T>(HttpContext httpContext, ITrustedRequestContextResolver resolver, ProcurementTenantContextResolver tenantResolver, FoundationAuditCoordinator auditCoordinator, LocalMasterDataIdempotencyStore idempotencyStore, FoundationOperationDescriptor descriptor, string fingerprint, Func<ProcurementRequestContext, Task<PurchaseInvoiceMatchOperationResult<T>>> operation, Func<ProcurementRequestContext, T, object?> map, bool setEtag, bool requireExpectedVersion)
    {
        if (!await EnsureAntiforgeryAsync(httpContext)) return await WriteProblemAsync(httpContext, 403, "antiforgery_failed", "Antiforgery validation failed", "The request could not be validated.", descriptor.OperationId);
        if (requireExpectedVersion && !TryReadExpectedVersion(httpContext, out _)) return await WriteProblemAsync(httpContext, 400, "validation_failed", "Validation failed", "A valid If-Match version is required.", descriptor.OperationId);
        var key = GetIdempotencyKey(httpContext);
        if (!FoundationCorrelation.IsValid(key)) return await WriteProblemAsync(httpContext, 400, "idempotency_key_invalid", "Invalid idempotency key", "A valid Idempotency-Key is required for this mutation.", descriptor.OperationId);
        var foundationContext = await resolver.ResolveAsync(httpContext, httpContext.RequestAborted);
        var resolution = tenantResolver.Resolve(foundationContext);
        if (!resolution.Allowed || resolution.Context is null) return await WriteProblemAsync(httpContext, foundationContext.SecurityProfile == FoundationSecurityProfile.Anonymous ? 401 : 403, resolution.Code, foundationContext.SecurityProfile == FoundationSecurityProfile.Anonymous ? "Authentication required" : "Access denied", "The operation is not available for this security context.", descriptor.OperationId);
        var context = resolution.Context;
        var binding = new FoundationIdempotencyBinding(context.ActorId, context.TenantId.Value, descriptor.SecurityProfile, descriptor.OperationId);
        var check = idempotencyStore.Begin(key!, binding, fingerprint, DateTimeOffset.UtcNow, TimeSpan.FromMinutes(10));
        if (check.Decision == LocalMasterDataIdempotencyDecision.Replay && check.Response is T replay)
        {
            httpContext.Response.Headers["X-Idempotent-Replay"] = "true";
            return ToResult(httpContext, PurchaseInvoiceMatchOperationResult<T>.Success(replay), descriptor.OperationId, context, map, setEtag);
        }
        if (check.Decision is not LocalMasterDataIdempotencyDecision.New) return await WriteProblemAsync(httpContext, 409, "idempotency_conflict", "Idempotency conflict", "The request cannot be replayed with different or incomplete input.", descriptor.OperationId);
        var committed = false;
        try
        {
            var execution = await auditCoordinator.ExecuteProtectedAsync(foundationContext, descriptor.OperationId, GetCorrelation(httpContext), FoundationAuditReason.Allowed, () => operation(context), idempotencyKey: key, operationVersion: "procurement.matching.v1", cancellationToken: httpContext.RequestAborted);
            if (!execution.Succeeded || execution.Value is null) return await WriteProblemAsync(httpContext, 503, execution.Code, "Operation unavailable", "The three-way matching operation could not be completed.", descriptor.OperationId);
            var result = execution.Value;
            if (result.Succeeded && result.Value is not null) { idempotencyStore.Commit(key!, binding, result.Value); committed = true; }
            return ToResult(httpContext, result, descriptor.OperationId, context, map, setEtag);
        }
        finally { if (!committed) idempotencyStore.Release(key!, binding); }
    }

    private static IResult ToResult<T>(HttpContext httpContext, PurchaseInvoiceMatchOperationResult<T> result, string operationId, ProcurementRequestContext context, Func<ProcurementRequestContext, T, object?> map, bool setEtag)
    {
        if (result.Succeeded && result.Value is not null)
        {
            if (setEtag && result.Value is PurchaseInvoiceMatchRecord record) httpContext.Response.Headers.ETag = $"\"{Convert.ToBase64String(record.Version)}\"";
            return Results.Json(map(context, result.Value));
        }
        var code = result.Code;
        var status = code switch
        {
            "permission_denied" or "resource_scope_denied" or "cross_tenant_target_denied" or "tenant_context_failed" or "authorization_profile_denied" or "resolution_policy_denied" or "sod_violation" => 403,
            "persistence_unavailable" or "authorization_operation_unmapped" => 503,
            "invoice_handoff_not_found" or "match_evaluation_not_found" or "purchase_order_not_found" => 404,
            "concurrency_conflict" or "idempotency_conflict" or "reason_required" or "match_resolution_not_allowed" or "stale_evaluation" or "invoice_handoff_not_active" => 409,
            _ => 400
        };
        return Results.Problem(statusCode: status, title: status == 403 ? "Access denied" : "Three-way matching operation failed", detail: "The three-way matching operation could not be completed.", type: $"https://api.minierp.local/problems/{code}", extensions: new Dictionary<string, object?> { ["code"] = code, ["correlationId"] = GetCorrelation(httpContext), ["operationId"] = operationId });
    }

    private static PurchaseInvoiceMatchListItemResponse ToListResponse(PurchaseInvoiceMatchListRecord item) => new(item.Id, item.PurchaseInvoiceHandoffId, item.PurchaseOrderId, item.Lifecycle, item.Result, item.EvaluatedAt, item.ResolvedByActorId, item.VarianceCount, Version(item.Version));
    private static PurchaseInvoiceMatchResponse ToResponse(PurchaseInvoiceMatchRecord item) => new(item.Id, item.TenantId, item.Scope.CompanyId, item.Scope.BranchId, item.PurchaseInvoiceHandoffId, item.PurchaseOrderId, item.Lifecycle, item.Result, item.EvaluatedAt, item.EvaluatedByActorId, item.ResolvedByActorId, item.ResolvedAt, item.ResolutionReason, item.SourceFingerprint, Version(item.PurchaseOrderVersion), Version(item.HandoffVersion), item.DeclaredEvidenceId?.ToString("D"), item.DeclaredEvidenceVersion, item.Policy.ToResponse(), item.ResolutionPolicy?.ToResponse(), item.AppliedExchangeRate?.ToResponse(), item.Variances.Select(variance => variance.ToResponse()).ToArray(), item.SourceSnapshot, Version(item.Version));

    private static bool TryReadExpectedVersion(HttpContext httpContext, out byte[] version)
    {
        version = [];
        var value = httpContext.Request.Headers.IfMatch.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(value)) return false;
        var normalized = value.Trim();
        if (normalized.StartsWith("W/", StringComparison.OrdinalIgnoreCase)) return false;
        if (normalized.Length >= 2 && normalized[0] == '"' && normalized[^1] == '"') normalized = normalized[1..^1];
        try { version = Convert.FromBase64String(normalized); return version.Length is > 0 and <= 64; } catch (FormatException) { version = []; return false; }
    }

    private static string? GetIdempotencyKey(HttpContext httpContext) => httpContext.Request.Headers["Idempotency-Key"].FirstOrDefault();
    private static string Fingerprint(object? value) => JsonSerializer.Serialize(value);
    private static string VersionFingerprint(byte[] version) => $"|version:{Convert.ToBase64String(version)}";
    private static string TargetFingerprint(Guid id) => $"|target:{id:D}";
    private static string Version(byte[] version) => Convert.ToBase64String(version);
    private static string GetCorrelation(HttpContext httpContext) => httpContext.Items.TryGetValue(FoundationApiKeys.CorrelationItem, out var value) && value is string correlationId ? correlationId : FoundationCorrelation.Resolve(httpContext.Request);
    private static async Task<bool> EnsureAntiforgeryAsync(HttpContext httpContext) { try { await httpContext.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(httpContext); return true; } catch (AntiforgeryValidationException) { return false; } }
    private static Task<IResult> WriteProblemAsync(HttpContext httpContext, int statusCode, string code, string title, string detail, string operationId) => Task.FromResult<IResult>(Results.Problem(statusCode: statusCode, title: title, detail: detail, type: $"https://api.minierp.local/problems/{code}", extensions: new Dictionary<string, object?> { ["code"] = code, ["correlationId"] = GetCorrelation(httpContext), ["operationId"] = operationId }));
}

#pragma warning restore CS1591
