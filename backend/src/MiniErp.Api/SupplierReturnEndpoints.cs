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

public static class SupplierReturnEndpoints
{
    public static IEndpointRouteBuilder MapSupplierReturnEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapGet("/api/v1/procurement/supplier-return-sources", async (HttpContext httpContext, ITrustedRequestContextResolver resolver, ProcurementTenantContextResolver tenantResolver, SupplierReturnService service) =>
            await ExecuteReadAsync(httpContext, resolver, tenantResolver, FoundationOperationCatalog.GetRequired("procurement.supplier-return.eligible-source.list"), context => service.ListEligibleSourcesAsync(context, httpContext.RequestAborted), (_, records) => records.Select(ToEligibleSourceResponse).ToArray()))
            .WithName("procurement.supplier-return.eligible-source.list").WithTags("Procurement / Supplier Returns").WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("procurement.supplier-return.eligible-source.list")));

        endpoints.MapGet("/api/v1/procurement/supplier-returns", async (HttpContext httpContext, ITrustedRequestContextResolver resolver, ProcurementTenantContextResolver tenantResolver, SupplierReturnService service) =>
        {
            SupplierReturnStatus? status = null;
            var rawStatus = httpContext.Request.Query["status"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(rawStatus))
            {
                if (!Enum.TryParse(rawStatus, true, out SupplierReturnStatus parsed))
                {
                    return await WriteProblemAsync(httpContext, 400, "validation_failed", "Validation failed", "The Supplier Return status filter is invalid.", "procurement.supplier-return.list");
                }

                status = parsed;
            }

            Guid? supplierId = null;
            var rawSupplierId = httpContext.Request.Query["supplierId"].FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(rawSupplierId))
            {
                if (!Guid.TryParse(rawSupplierId, out var parsedSupplierId))
                {
                    return await WriteProblemAsync(httpContext, 400, "validation_failed", "Validation failed", "The supplier filter is invalid.", "procurement.supplier-return.list");
                }

                supplierId = parsedSupplierId;
            }

            return await ExecuteReadAsync(httpContext, resolver, tenantResolver, FoundationOperationCatalog.GetRequired("procurement.supplier-return.list"), context => service.ListAsync(context, status, supplierId, httpContext.RequestAborted), (_, records) => records.Select(ToListResponse).ToArray());
        }).WithName("procurement.supplier-return.list").WithTags("Procurement / Supplier Returns").WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("procurement.supplier-return.list")));

        endpoints.MapGet("/api/v1/procurement/supplier-returns/report", async (HttpContext httpContext, ITrustedRequestContextResolver resolver, ProcurementTenantContextResolver tenantResolver, SupplierReturnService service) =>
            await ExecuteReadAsync(httpContext, resolver, tenantResolver, FoundationOperationCatalog.GetRequired("procurement.supplier-return.report.read"), context => service.ReportAsync(context, httpContext.RequestAborted), (_, summary) => new SupplierReturnReportResponse(summary.ReturnCount, summary.TotalReturnQuantity, summary.OpenReturnCount, summary.OpenReturnQuantity, summary.PendingInventoryCount, summary.PendingFinanceCount, summary.Returns.Select(ToListResponse).ToArray())))
            .WithName("procurement.supplier-return.report.read").WithTags("Procurement / Supplier Returns").WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("procurement.supplier-return.report.read")));

        endpoints.MapGet("/api/v1/procurement/supplier-returns/{supplierReturnId:guid}", async (Guid supplierReturnId, HttpContext httpContext, ITrustedRequestContextResolver resolver, ProcurementTenantContextResolver tenantResolver, SupplierReturnService service) =>
            await ExecuteReadAsync(httpContext, resolver, tenantResolver, FoundationOperationCatalog.GetRequired("procurement.supplier-return.read"), context => service.GetAsync(context, supplierReturnId, httpContext.RequestAborted), (_, record) => ToResponse(record), setEtag: true))
            .WithName("procurement.supplier-return.read").WithTags("Procurement / Supplier Returns").WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("procurement.supplier-return.read")));

        endpoints.MapPost("/api/v1/procurement/supplier-returns", async (SupplierReturnCreateRequest? request, HttpContext httpContext, ITrustedRequestContextResolver resolver, ProcurementTenantContextResolver tenantResolver, FoundationAuditCoordinator auditCoordinator, LocalMasterDataIdempotencyStore idempotencyStore, SupplierReturnService service) =>
        {
            if (request is null)
            {
                return await WriteProblemAsync(httpContext, 400, "validation_failed", "Validation failed", "A Supplier Return body is required.", "procurement.supplier-return.create");
            }

            var fingerprint = Fingerprint(request);
            return await ExecuteMutationAsync(httpContext, resolver, tenantResolver, auditCoordinator, idempotencyStore, FoundationOperationCatalog.GetRequired("procurement.supplier-return.create"), fingerprint, context => service.CreateAsync(context, request, GetIdempotencyKey(httpContext), fingerprint, httpContext.RequestAborted), (_, record) => ToResponse(record), setEtag: true, requireExpectedVersion: false);
        }).WithName("procurement.supplier-return.create").WithTags("Procurement / Supplier Returns").WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("procurement.supplier-return.create")));

        endpoints.MapPost("/api/v1/procurement/supplier-returns/{supplierReturnId:guid}/submit", async (Guid supplierReturnId, SupplierReturnActionRequest? request, HttpContext httpContext, ITrustedRequestContextResolver resolver, ProcurementTenantContextResolver tenantResolver, FoundationAuditCoordinator auditCoordinator, LocalMasterDataIdempotencyStore idempotencyStore, SupplierReturnService service) =>
            await ExecuteMutationAsync(httpContext, resolver, tenantResolver, auditCoordinator, idempotencyStore, FoundationOperationCatalog.GetRequired("procurement.supplier-return.submit"), supplierReturnId, request, (context, version, body, key, fingerprint) => service.SubmitAsync(context, supplierReturnId, version, body?.Reason, key, fingerprint, httpContext.RequestAborted)))
            .WithName("procurement.supplier-return.submit").WithTags("Procurement / Supplier Returns").WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("procurement.supplier-return.submit")));
        endpoints.MapPost("/api/v1/procurement/supplier-returns/{supplierReturnId:guid}/approve", async (Guid supplierReturnId, SupplierReturnActionRequest? request, HttpContext httpContext, ITrustedRequestContextResolver resolver, ProcurementTenantContextResolver tenantResolver, FoundationAuditCoordinator auditCoordinator, LocalMasterDataIdempotencyStore idempotencyStore, SupplierReturnService service) =>
            await ExecuteMutationAsync(httpContext, resolver, tenantResolver, auditCoordinator, idempotencyStore, FoundationOperationCatalog.GetRequired("procurement.supplier-return.approve"), supplierReturnId, request, (context, version, body, key, fingerprint) => service.ApproveAsync(context, supplierReturnId, version, body?.Reason, key, fingerprint, httpContext.RequestAborted)))
            .WithName("procurement.supplier-return.approve").WithTags("Procurement / Supplier Returns").WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("procurement.supplier-return.approve")));
        endpoints.MapPost("/api/v1/procurement/supplier-returns/{supplierReturnId:guid}/reject", async (Guid supplierReturnId, SupplierReturnActionRequest? request, HttpContext httpContext, ITrustedRequestContextResolver resolver, ProcurementTenantContextResolver tenantResolver, FoundationAuditCoordinator auditCoordinator, LocalMasterDataIdempotencyStore idempotencyStore, SupplierReturnService service) =>
            await ExecuteMutationAsync(httpContext, resolver, tenantResolver, auditCoordinator, idempotencyStore, FoundationOperationCatalog.GetRequired("procurement.supplier-return.reject"), supplierReturnId, request, (context, version, body, key, fingerprint) => service.RejectAsync(context, supplierReturnId, version, body?.Reason, key, fingerprint, httpContext.RequestAborted)))
            .WithName("procurement.supplier-return.reject").WithTags("Procurement / Supplier Returns").WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("procurement.supplier-return.reject")));
        endpoints.MapPost("/api/v1/procurement/supplier-returns/{supplierReturnId:guid}/cancel", async (Guid supplierReturnId, SupplierReturnActionRequest? request, HttpContext httpContext, ITrustedRequestContextResolver resolver, ProcurementTenantContextResolver tenantResolver, FoundationAuditCoordinator auditCoordinator, LocalMasterDataIdempotencyStore idempotencyStore, SupplierReturnService service) =>
            await ExecuteMutationAsync(httpContext, resolver, tenantResolver, auditCoordinator, idempotencyStore, FoundationOperationCatalog.GetRequired("procurement.supplier-return.cancel"), supplierReturnId, request, (context, version, body, key, fingerprint) => service.CancelAsync(context, supplierReturnId, version, body?.Reason, key, fingerprint, httpContext.RequestAborted)))
            .WithName("procurement.supplier-return.cancel").WithTags("Procurement / Supplier Returns").WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("procurement.supplier-return.cancel")));
        endpoints.MapPost("/api/v1/procurement/supplier-returns/{supplierReturnId:guid}/reverse", async (Guid supplierReturnId, SupplierReturnActionRequest? request, HttpContext httpContext, ITrustedRequestContextResolver resolver, ProcurementTenantContextResolver tenantResolver, FoundationAuditCoordinator auditCoordinator, LocalMasterDataIdempotencyStore idempotencyStore, SupplierReturnService service) =>
            await ExecuteMutationAsync(httpContext, resolver, tenantResolver, auditCoordinator, idempotencyStore, FoundationOperationCatalog.GetRequired("procurement.supplier-return.reverse"), supplierReturnId, request, (context, version, body, key, fingerprint) => service.ReverseAsync(context, supplierReturnId, version, body?.Reason, key, fingerprint, httpContext.RequestAborted)))
            .WithName("procurement.supplier-return.reverse").WithTags("Procurement / Supplier Returns").WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("procurement.supplier-return.reverse")));

        endpoints.MapPost("/api/v1/procurement/supplier-returns/{supplierReturnId:guid}/inventory-handoff", async (Guid supplierReturnId, SupplierReturnInventoryHandoffRequest? request, HttpContext httpContext, ITrustedRequestContextResolver resolver, ProcurementTenantContextResolver tenantResolver, FoundationAuditCoordinator auditCoordinator, LocalMasterDataIdempotencyStore idempotencyStore, SupplierReturnService service) =>
            await ExecuteMutationAsync(httpContext, resolver, tenantResolver, auditCoordinator, idempotencyStore, FoundationOperationCatalog.GetRequired("procurement.supplier-return.inventory-handoff.record"), supplierReturnId, request, (context, version, body, key, fingerprint) => service.RecordInventoryHandoffAsync(context, supplierReturnId, version, body!, key, fingerprint, httpContext.RequestAborted)))
            .WithName("procurement.supplier-return.inventory-handoff.record").WithTags("Procurement / Supplier Returns").WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("procurement.supplier-return.inventory-handoff.record")));

        endpoints.MapPost("/api/v1/procurement/supplier-returns/{supplierReturnId:guid}/finance-reference", async (Guid supplierReturnId, SupplierReturnFinanceReferenceRequest? request, HttpContext httpContext, ITrustedRequestContextResolver resolver, ProcurementTenantContextResolver tenantResolver, FoundationAuditCoordinator auditCoordinator, LocalMasterDataIdempotencyStore idempotencyStore, SupplierReturnService service) =>
            await ExecuteMutationAsync(httpContext, resolver, tenantResolver, auditCoordinator, idempotencyStore, FoundationOperationCatalog.GetRequired("procurement.supplier-return.finance-reference.record"), supplierReturnId, request, (context, version, body, key, fingerprint) => service.RecordFinanceReferenceAsync(context, supplierReturnId, version, body!, key, fingerprint, httpContext.RequestAborted)))
            .WithName("procurement.supplier-return.finance-reference.record").WithTags("Procurement / Supplier Returns").WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("procurement.supplier-return.finance-reference.record")));

        endpoints.MapPost("/api/v1/procurement/supplier-returns/{supplierReturnId:guid}/correct", async (Guid supplierReturnId, SupplierReturnCreateRequest? request, HttpContext httpContext, ITrustedRequestContextResolver resolver, ProcurementTenantContextResolver tenantResolver, FoundationAuditCoordinator auditCoordinator, LocalMasterDataIdempotencyStore idempotencyStore, SupplierReturnService service) =>
        {
            if (request is null || !TryReadExpectedVersion(httpContext, out var expectedVersion))
            {
                return await WriteProblemAsync(httpContext, 400, "validation_failed", "Validation failed", "A Supplier Return body and valid If-Match version are required.", "procurement.supplier-return.correct");
            }

            var fingerprint = Fingerprint(request) + VersionFingerprint(expectedVersion) + TargetFingerprint(supplierReturnId);
            return await ExecuteMutationAsync(httpContext, resolver, tenantResolver, auditCoordinator, idempotencyStore, FoundationOperationCatalog.GetRequired("procurement.supplier-return.correct"), fingerprint, context => service.CorrectAsync(context, supplierReturnId, expectedVersion, request, GetIdempotencyKey(httpContext), fingerprint, httpContext.RequestAborted), (_, record) => ToResponse(record), setEtag: true, requireExpectedVersion: true);
        }).WithName("procurement.supplier-return.correct").WithTags("Procurement / Supplier Returns").WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("procurement.supplier-return.correct")));

        endpoints.MapGet("/api/v1/procurement/supplier-returns/{supplierReturnId:guid}/history", async (Guid supplierReturnId, HttpContext httpContext, ITrustedRequestContextResolver resolver, ProcurementTenantContextResolver tenantResolver, SupplierReturnService service) =>
            await ExecuteReadAsync(httpContext, resolver, tenantResolver, FoundationOperationCatalog.GetRequired("procurement.supplier-return.history.read"), context => service.ReadHistoryAsync(context, supplierReturnId, httpContext.RequestAborted), (_, records) => records.Select(item => new SupplierReturnHistoryResponse(item.EvidenceId, item.SupplierReturnId, item.OccurredAt, item.FromStatus.ToString(), item.ToStatus.ToString(), item.Action.ToString(), item.ActorId, item.Reason, item.CorrelationId)).ToArray()))
            .WithName("procurement.supplier-return.history.read").WithTags("Procurement / Supplier Returns").WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("procurement.supplier-return.history.read")));

        endpoints.MapGet("/api/v1/procurement/supplier-returns/{supplierReturnId:guid}/audit", async (Guid supplierReturnId, HttpContext httpContext, ITrustedRequestContextResolver resolver, ProcurementTenantContextResolver tenantResolver, SupplierReturnService service) =>
            await ExecuteReadAsync(httpContext, resolver, tenantResolver, FoundationOperationCatalog.GetRequired("procurement.supplier-return.audit.read"), context => service.ReadAuditAsync(context, supplierReturnId, httpContext.RequestAborted), (_, records) => records.Select(item => new SupplierReturnAuditResponse(item.EvidenceId, item.SupplierReturnId, item.OccurredAt, item.OperationId, item.CorrelationId, item.TenantId, item.ActorId, item.SessionId, item.AuthorizationPath, item.Decision, item.Reason, item.BeforeStatus?.ToString(), item.AfterStatus?.ToString(), item.CompanyId, item.BranchId, item.BeforeSummary, item.AfterSummary, item.IdempotencyKey)).ToArray()))
            .WithName("procurement.supplier-return.audit.read").WithTags("Procurement / Supplier Returns").WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("procurement.supplier-return.audit.read")));

        return endpoints;
    }

    private static async Task<IResult> ExecuteMutationAsync(HttpContext httpContext, ITrustedRequestContextResolver resolver, ProcurementTenantContextResolver tenantResolver, FoundationAuditCoordinator auditCoordinator, LocalMasterDataIdempotencyStore idempotencyStore, FoundationOperationDescriptor descriptor, Guid id, SupplierReturnActionRequest? body, Func<ProcurementRequestContext, byte[], SupplierReturnActionRequest?, string?, string?, Task<SupplierReturnOperationResult<SupplierReturnRecord>>> operation)
    {
        if (!TryReadExpectedVersion(httpContext, out var version))
        {
            return await WriteProblemAsync(httpContext, 400, "validation_failed", "Validation failed", "A valid If-Match version is required.", descriptor.OperationId);
        }

        var fingerprint = Fingerprint(body) + VersionFingerprint(version) + TargetFingerprint(id);
        return await ExecuteMutationAsync(httpContext, resolver, tenantResolver, auditCoordinator, idempotencyStore, descriptor, fingerprint, context => operation(context, version, body, GetIdempotencyKey(httpContext), fingerprint), (_, record) => ToResponse(record), setEtag: true, requireExpectedVersion: true);
    }

    private static async Task<IResult> ExecuteMutationAsync<TBody>(HttpContext httpContext, ITrustedRequestContextResolver resolver, ProcurementTenantContextResolver tenantResolver, FoundationAuditCoordinator auditCoordinator, LocalMasterDataIdempotencyStore idempotencyStore, FoundationOperationDescriptor descriptor, Guid id, TBody? body, Func<ProcurementRequestContext, byte[], TBody?, string?, string?, Task<SupplierReturnOperationResult<SupplierReturnRecord>>> operation)
    {
        if (body is null || !TryReadExpectedVersion(httpContext, out var version))
        {
            return await WriteProblemAsync(httpContext, 400, "validation_failed", "Validation failed", "A body and valid If-Match version are required.", descriptor.OperationId);
        }

        var fingerprint = Fingerprint(body) + VersionFingerprint(version) + TargetFingerprint(id);
        return await ExecuteMutationAsync(httpContext, resolver, tenantResolver, auditCoordinator, idempotencyStore, descriptor, fingerprint, context => operation(context, version, body, GetIdempotencyKey(httpContext), fingerprint), (_, record) => ToResponse(record), setEtag: true, requireExpectedVersion: true);
    }

    private static async Task<IResult> ExecuteReadAsync<T>(HttpContext httpContext, ITrustedRequestContextResolver resolver, ProcurementTenantContextResolver tenantResolver, FoundationOperationDescriptor descriptor, Func<ProcurementRequestContext, Task<SupplierReturnOperationResult<T>>> operation, Func<ProcurementRequestContext, T, object?> map, bool setEtag = false)
    {
        var foundationContext = await resolver.ResolveAsync(httpContext, httpContext.RequestAborted);
        var resolution = tenantResolver.Resolve(foundationContext);
        if (!resolution.Allowed || resolution.Context is null)
        {
            return await WriteProblemAsync(httpContext, foundationContext.SecurityProfile == FoundationSecurityProfile.Anonymous ? 401 : 403, resolution.Code, "Access denied", "The operation is not available for this security context.", descriptor.OperationId);
        }

        return ToResult(httpContext, await operation(resolution.Context), descriptor.OperationId, resolution.Context, map, setEtag);
    }

    private static async Task<IResult> ExecuteMutationAsync<T>(HttpContext httpContext, ITrustedRequestContextResolver resolver, ProcurementTenantContextResolver tenantResolver, FoundationAuditCoordinator auditCoordinator, LocalMasterDataIdempotencyStore idempotencyStore, FoundationOperationDescriptor descriptor, string fingerprint, Func<ProcurementRequestContext, Task<SupplierReturnOperationResult<T>>> operation, Func<ProcurementRequestContext, T, object?> map, bool setEtag, bool requireExpectedVersion)
    {
        if (!await EnsureAntiforgeryAsync(httpContext)) return await WriteProblemAsync(httpContext, 403, "antiforgery_failed", "Antiforgery validation failed", "The request could not be validated.", descriptor.OperationId);
        if (requireExpectedVersion && !TryReadExpectedVersion(httpContext, out _)) return await WriteProblemAsync(httpContext, 400, "validation_failed", "Validation failed", "A valid If-Match version is required.", descriptor.OperationId);
        var key = GetIdempotencyKey(httpContext);
        if (!FoundationCorrelation.IsValid(key)) return await WriteProblemAsync(httpContext, 400, "idempotency_key_invalid", "Invalid idempotency key", "A valid Idempotency-Key is required for this mutation.", descriptor.OperationId);
        var foundationContext = await resolver.ResolveAsync(httpContext, httpContext.RequestAborted);
        var resolution = tenantResolver.Resolve(foundationContext);
        if (!resolution.Allowed || resolution.Context is null) return await WriteProblemAsync(httpContext, 403, resolution.Code, "Access denied", "The operation is not available for this security context.", descriptor.OperationId);
        var context = resolution.Context;
        var binding = new FoundationIdempotencyBinding(context.ActorId, context.TenantId.Value, descriptor.SecurityProfile, descriptor.OperationId);
        var check = idempotencyStore.Begin(key!, binding, fingerprint, DateTimeOffset.UtcNow, TimeSpan.FromMinutes(10));
        if (check.Decision == LocalMasterDataIdempotencyDecision.Replay && check.Response is T replay)
        {
            httpContext.Response.Headers["X-Idempotent-Replay"] = "true";
            return ToResult(httpContext, SupplierReturnOperationResult<T>.Success(replay), descriptor.OperationId, context, map, setEtag);
        }

        if (check.Decision is not LocalMasterDataIdempotencyDecision.New) return await WriteProblemAsync(httpContext, 409, "idempotency_conflict", "Idempotency conflict", "The request cannot be replayed with different or incomplete input.", descriptor.OperationId);
        var committed = false;
        try
        {
            var execution = await auditCoordinator.ExecuteProtectedAsync(foundationContext, descriptor.OperationId, GetCorrelation(httpContext), FoundationAuditReason.Allowed, () => operation(context), idempotencyKey: key, operationVersion: "procurement.supplier-return.v1", cancellationToken: httpContext.RequestAborted);
            if (!execution.Succeeded || execution.Value is null) return await WriteProblemAsync(httpContext, 503, execution.Code, "Operation unavailable", "The Supplier Return operation could not be completed.", descriptor.OperationId);
            var result = execution.Value;
            if (result.Succeeded && result.Value is not null) { idempotencyStore.Commit(key!, binding, result.Value); committed = true; }
            return ToResult(httpContext, result, descriptor.OperationId, context, map, setEtag);
        }
        finally { if (!committed) idempotencyStore.Release(key!, binding); }
    }

    private static IResult ToResult<T>(HttpContext httpContext, SupplierReturnOperationResult<T> result, string operationId, ProcurementRequestContext context, Func<ProcurementRequestContext, T, object?> map, bool setEtag)
    {
        if (result.Succeeded && result.Value is not null)
        {
            if (setEtag && result.Value is SupplierReturnRecord record) httpContext.Response.Headers.ETag = $"\"{Convert.ToBase64String(record.Version)}\"";
            return Results.Json(map(context, result.Value));
        }

        var code = result.Code;
        var status = code switch
        {
            "permission_denied" or "resource_scope_denied" or "cross_tenant_target_denied" or "tenant_context_failed" or "authorization_profile_denied" => 403,
            "persistence_unavailable" or "authorization_operation_unmapped" => 503,
            "supplier_return_not_found" or "goods_receipt_not_found" => 404,
            "concurrency_conflict" or "idempotency_conflict" or "reason_required" or "supplier_return_source_not_eligible" or "supplier_return_line_not_eligible" or "over_return_not_allowed" or "supplier_return_action_not_allowed" or "downstream_consequence_exists" or "correction_not_allowed" or "correction_source_mismatch" or "handoff_reference_required" or "finance_reference_required" => 409,
            _ => 400
        };
        return Results.Problem(statusCode: status, title: status == 403 ? "Access denied" : "Supplier Return operation failed", detail: "The Supplier Return operation could not be completed.", type: $"https://api.minierp.local/problems/{code}", extensions: new Dictionary<string, object?> { ["code"] = code, ["correlationId"] = GetCorrelation(httpContext), ["operationId"] = operationId });
    }

    private static SupplierReturnEligibleSourceResponse ToEligibleSourceResponse(SupplierReturnEligibleSourceRecord item) => new(item.GoodsReceiptId, item.PurchaseOrderId, item.SupplierConfirmationId, item.Scope.CompanyId, item.Scope.BranchId, item.WarehouseId, item.SupplierId, item.SupplierCode, item.SupplierName, item.CurrencyCode, item.Lines.Select(line => new SupplierReturnEligibleLineResponse(line.GoodsReceiptId, line.GoodsReceiptLineId, line.PurchaseOrderId, line.PurchaseOrderLineId, line.WarehouseId, line.ProductId, line.ProductSku, line.ProductName, line.UnitOfMeasureCode, line.AcceptedQuantity, line.AlreadyReturnedQuantity, line.EligibleReturnQuantity, line.ReceivedDate)).ToArray());
    private static SupplierReturnListItemResponse ToListResponse(SupplierReturnListRecord item) => new(item.Id, item.TenantId, item.Scope.CompanyId, item.Scope.BranchId, item.GoodsReceiptId, item.PurchaseOrderId, item.WarehouseId, item.SupplierCode, item.SupplierName, item.Status.ToString(), item.ReasonCode.ToString(), item.CommercialOutcome.ToString(), item.TotalReturnQuantity, item.ReturnDate, item.CreatedAt, Version(item.Version));
    private static SupplierReturnResponse ToResponse(SupplierReturnRecord item) => new(item.Id, item.TenantId, item.Scope.CompanyId, item.Scope.BranchId, item.GoodsReceiptId, item.PurchaseOrderId, item.SupplierConfirmationId, item.WarehouseId, item.SupplierId, item.SupplierCode, item.SupplierName, item.CurrencyCode, item.Status.ToString(), item.ReasonCode.ToString(), item.Condition.ToString(), item.CommercialOutcome.ToString(), item.ReasonDetail, item.Notes, item.ReturnDate, item.CreatedAt, item.UpdatedAt, item.CancelledAt, item.ReversedAt, item.CorrectionOfId, item.InventoryHandoffId, item.InventoryHandoffReference, item.FinanceReference, item.FinanceCurrencyCode, item.FinanceAmount, item.Lines.Select(line => new SupplierReturnLineResponse(line.Id, line.GoodsReceiptLineId, line.PurchaseOrderLineId, line.ProductId, line.ProductSku, line.ProductName, line.UnitOfMeasureCode, line.AcceptedQuantityAtReturn, line.ReturnQuantity, line.EligibleQuantityAfter, line.Notes)).ToArray(), item.Evidence.Select(file => new SupplierReturnEvidenceReferenceResponse(file.Id, file.ReferenceId, file.FileName, file.ContentType, file.Description, file.Source, file.RecordedAt)).ToArray(), Version(item.Version), item.Status == SupplierReturnStatus.Draft, item.Status == SupplierReturnStatus.Submitted, item.Status is SupplierReturnStatus.Draft or SupplierReturnStatus.Submitted or SupplierReturnStatus.Approved or SupplierReturnStatus.AwaitingInventory, item.Status is SupplierReturnStatus.Draft or SupplierReturnStatus.Submitted or SupplierReturnStatus.Approved or SupplierReturnStatus.AwaitingInventory, item.Status is SupplierReturnStatus.Approved or SupplierReturnStatus.AwaitingInventory);
    private static string Version(byte[] version) => Convert.ToBase64String(version);
    private static bool TryReadExpectedVersion(HttpContext httpContext, out byte[] version) { version = []; var value = httpContext.Request.Headers.IfMatch.FirstOrDefault(); if (string.IsNullOrWhiteSpace(value)) return false; var normalized = value.Trim(); if (normalized.StartsWith("W/", StringComparison.OrdinalIgnoreCase)) return false; if (normalized.Length >= 2 && normalized[0] == '"' && normalized[^1] == '"') normalized = normalized[1..^1]; try { version = Convert.FromBase64String(normalized); return version.Length is > 0 and <= 64; } catch (FormatException) { version = []; return false; } }
    private static string? GetIdempotencyKey(HttpContext httpContext) => httpContext.Request.Headers["Idempotency-Key"].FirstOrDefault();
    private static string Fingerprint(object? value) => JsonSerializer.Serialize(value);
    private static string VersionFingerprint(byte[] version) => $"|version:{Convert.ToBase64String(version)}";
    private static string TargetFingerprint(Guid id) => $"|target:{id:D}";
    private static string GetCorrelation(HttpContext httpContext) => httpContext.Items.TryGetValue(FoundationApiKeys.CorrelationItem, out var value) && value is string correlationId ? correlationId : FoundationCorrelation.Resolve(httpContext.Request);
    private static async Task<bool> EnsureAntiforgeryAsync(HttpContext httpContext) { try { await httpContext.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(httpContext); return true; } catch (AntiforgeryValidationException) { return false; } }
    private static Task<IResult> WriteProblemAsync(HttpContext httpContext, int statusCode, string code, string title, string detail, string operationId) => Task.FromResult<IResult>(Results.Problem(statusCode: statusCode, title: title, detail: detail, type: $"https://api.minierp.local/problems/{code}", extensions: new Dictionary<string, object?> { ["code"] = code, ["correlationId"] = GetCorrelation(httpContext), ["operationId"] = operationId }));
}

#pragma warning restore CS1591
