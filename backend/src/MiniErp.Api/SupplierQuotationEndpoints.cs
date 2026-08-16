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

public static class SupplierQuotationEndpoints
{
    public static IEndpointRouteBuilder MapSupplierQuotationEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapGet(
                "/api/v1/procurement/purchase-requests/{purchaseRequestId:guid}/quotations",
                async (Guid purchaseRequestId, HttpContext httpContext, ITrustedRequestContextResolver resolver, ProcurementTenantContextResolver tenantResolver, SupplierQuotationService service) =>
                    await ExecuteReadAsync(
                        httpContext,
                        resolver,
                        tenantResolver,
                        FoundationOperationCatalog.GetRequired("procurement.quotation.list"),
                        context => service.ListAsync(context, purchaseRequestId, httpContext.RequestAborted),
                        (_, records) => records.Select(ToListResponse).ToArray()))
            .WithName("procurement.quotation.list")
            .WithTags("Procurement / Supplier Quotations")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("procurement.quotation.list")));

        endpoints.MapGet(
                "/api/v1/procurement/quotations/{quotationId:guid}",
                async (Guid quotationId, HttpContext httpContext, ITrustedRequestContextResolver resolver, ProcurementTenantContextResolver tenantResolver, SupplierQuotationService service) =>
                    await ExecuteReadAsync(
                        httpContext,
                        resolver,
                        tenantResolver,
                        FoundationOperationCatalog.GetRequired("procurement.quotation.read"),
                        context => service.GetAsync(context, quotationId, httpContext.RequestAborted),
                        (context, record) => ToResponse(record, context),
                        setEtag: true))
            .WithName("procurement.quotation.read")
            .WithTags("Procurement / Supplier Quotations")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("procurement.quotation.read")));

        endpoints.MapPost(
                "/api/v1/procurement/purchase-requests/{purchaseRequestId:guid}/quotations",
                async (Guid purchaseRequestId, SupplierQuotationWriteRequest? request, HttpContext httpContext, ITrustedRequestContextResolver resolver, ProcurementTenantContextResolver tenantResolver, FoundationAuditCoordinator auditCoordinator, LocalMasterDataIdempotencyStore idempotencyStore, SupplierQuotationService service) =>
                {
                    if (request is null)
                    {
                        return await WriteProblemAsync(httpContext, 400, "validation_failed", "Validation failed", "A Supplier Quotation body is required.", "procurement.quotation.create");
                    }

                    return await ExecuteMutationAsync(
                        httpContext,
                        resolver,
                        tenantResolver,
                        auditCoordinator,
                        idempotencyStore,
                        FoundationOperationCatalog.GetRequired("procurement.quotation.create"),
                        Fingerprint(request),
                        context => service.CreateAsync(context, purchaseRequestId, request, GetIdempotencyKey(httpContext), httpContext.RequestAborted),
                        (context, record) => ToResponse(record, context),
                        setEtag: true);
                })
            .WithName("procurement.quotation.create")
            .WithTags("Procurement / Supplier Quotations")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("procurement.quotation.create")));

        endpoints.MapPost(
                "/api/v1/procurement/quotations/{quotationId:guid}/edit",
                async (Guid quotationId, SupplierQuotationWriteRequest? request, HttpContext httpContext, ITrustedRequestContextResolver resolver, ProcurementTenantContextResolver tenantResolver, FoundationAuditCoordinator auditCoordinator, LocalMasterDataIdempotencyStore idempotencyStore, SupplierQuotationService service) =>
                {
                    if (request is null || !TryReadExpectedVersion(httpContext, out var expectedVersion))
                    {
                        return await WriteProblemAsync(httpContext, 400, "validation_failed", "Validation failed", "A valid If-Match version and Supplier Quotation body are required.", "procurement.quotation.edit");
                    }

                    return await ExecuteMutationAsync(
                        httpContext,
                        resolver,
                        tenantResolver,
                        auditCoordinator,
                        idempotencyStore,
                        FoundationOperationCatalog.GetRequired("procurement.quotation.edit"),
                        Fingerprint(request) + VersionFingerprint(expectedVersion),
                        context => service.EditAsync(context, quotationId, request, expectedVersion, GetIdempotencyKey(httpContext), httpContext.RequestAborted),
                        (context, record) => ToResponse(record, context),
                        setEtag: true,
                        requireExpectedVersion: true);
                })
            .WithName("procurement.quotation.edit")
            .WithTags("Procurement / Supplier Quotations")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("procurement.quotation.edit")));

        endpoints.MapPost(
                "/api/v1/procurement/quotations/{quotationId:guid}/submit",
                async (Guid quotationId, HttpContext httpContext, ITrustedRequestContextResolver resolver, ProcurementTenantContextResolver tenantResolver, FoundationAuditCoordinator auditCoordinator, LocalMasterDataIdempotencyStore idempotencyStore, SupplierQuotationService service) =>
                    await ExecuteMutationAsyncAction(
                        quotationId,
                        httpContext,
                        resolver,
                        tenantResolver,
                        auditCoordinator,
                        idempotencyStore,
                        service,
                        FoundationOperationCatalog.GetRequired("procurement.quotation.submit"),
                        (context, version, key, _) => service.SubmitAsync(context, quotationId, version, key, httpContext.RequestAborted)))
            .WithName("procurement.quotation.submit")
            .WithTags("Procurement / Supplier Quotations")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("procurement.quotation.submit")));

        endpoints.MapPost(
                "/api/v1/procurement/quotations/{quotationId:guid}/withdraw",
                async (Guid quotationId, SupplierQuotationActionRequest? request, HttpContext httpContext, ITrustedRequestContextResolver resolver, ProcurementTenantContextResolver tenantResolver, FoundationAuditCoordinator auditCoordinator, LocalMasterDataIdempotencyStore idempotencyStore, SupplierQuotationService service) =>
                    await ExecuteMutationAsyncAction(
                        quotationId,
                        httpContext,
                        resolver,
                        tenantResolver,
                        auditCoordinator,
                        idempotencyStore,
                        service,
                        FoundationOperationCatalog.GetRequired("procurement.quotation.withdraw"),
                        (context, version, key, body) => service.WithdrawAsync(context, quotationId, version, body?.Reason, key, httpContext.RequestAborted),
                        request))
            .WithName("procurement.quotation.withdraw")
            .WithTags("Procurement / Supplier Quotations")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("procurement.quotation.withdraw")));

        endpoints.MapPost(
                "/api/v1/procurement/quotations/{quotationId:guid}/disqualify",
                async (Guid quotationId, SupplierQuotationActionRequest? request, HttpContext httpContext, ITrustedRequestContextResolver resolver, ProcurementTenantContextResolver tenantResolver, FoundationAuditCoordinator auditCoordinator, LocalMasterDataIdempotencyStore idempotencyStore, SupplierQuotationService service) =>
                    await ExecuteMutationAsyncAction(
                        quotationId,
                        httpContext,
                        resolver,
                        tenantResolver,
                        auditCoordinator,
                        idempotencyStore,
                        service,
                        FoundationOperationCatalog.GetRequired("procurement.quotation.disqualify"),
                        (context, version, key, body) => service.DisqualifyAsync(context, quotationId, version, body?.Reason, key, httpContext.RequestAborted),
                        request))
            .WithName("procurement.quotation.disqualify")
            .WithTags("Procurement / Supplier Quotations")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("procurement.quotation.disqualify")));

        endpoints.MapGet(
                "/api/v1/procurement/purchase-requests/{purchaseRequestId:guid}/quotation-comparison",
                async (Guid purchaseRequestId, HttpContext httpContext, ITrustedRequestContextResolver resolver, ProcurementTenantContextResolver tenantResolver, SupplierQuotationService service) =>
                    await ExecuteReadAsync(
                        httpContext,
                        resolver,
                        tenantResolver,
                        FoundationOperationCatalog.GetRequired("procurement.quotation.compare"),
                        context => service.CompareAsync(context, purchaseRequestId, httpContext.RequestAborted),
                        (_, comparison) => ToComparisonResponse(comparison)))
            .WithName("procurement.quotation.compare")
            .WithTags("Procurement / Supplier Quotations")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("procurement.quotation.compare")));

        endpoints.MapGet(
                "/api/v1/procurement/purchase-requests/{purchaseRequestId:guid}/source-decision",
                async (Guid purchaseRequestId, HttpContext httpContext, ITrustedRequestContextResolver resolver, ProcurementTenantContextResolver tenantResolver, SupplierQuotationService service) =>
                    await ExecuteReadAsync(
                        httpContext,
                        resolver,
                        tenantResolver,
                        FoundationOperationCatalog.GetRequired("procurement.source-decision.read"),
                        context => service.ReadSourceDecisionAsync(context, purchaseRequestId, httpContext.RequestAborted),
                        (_, decision) => ToSourceDecisionResponse(decision),
                        setEtag: true))
            .WithName("procurement.source-decision.read")
            .WithTags("Procurement / Supplier Quotations")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("procurement.source-decision.read")));

        endpoints.MapGet(
                "/api/v1/procurement/purchase-requests/{purchaseRequestId:guid}/source-decision/history",
                async (Guid purchaseRequestId, HttpContext httpContext, ITrustedRequestContextResolver resolver, ProcurementTenantContextResolver tenantResolver, SupplierQuotationService service) =>
                    await ExecuteReadAsync(
                        httpContext,
                        resolver,
                        tenantResolver,
                        FoundationOperationCatalog.GetRequired("procurement.source-decision.history.read"),
                        context => service.ReadSourceDecisionHistoryAsync(context, purchaseRequestId, httpContext.RequestAborted),
                        (_, records) => records.Select(ToSourceDecisionHistoryResponse).ToArray()))
            .WithName("procurement.source-decision.history.read")
            .WithTags("Procurement / Supplier Quotations")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("procurement.source-decision.history.read")));

        endpoints.MapPost(
                "/api/v1/procurement/purchase-requests/{purchaseRequestId:guid}/source-decision",
                async (Guid purchaseRequestId, SupplierSourceDecisionWriteRequest? request, HttpContext httpContext, ITrustedRequestContextResolver resolver, ProcurementTenantContextResolver tenantResolver, FoundationAuditCoordinator auditCoordinator, LocalMasterDataIdempotencyStore idempotencyStore, SupplierQuotationService service) =>
                {
                    if (request is null || !TryReadExpectedVersion(httpContext, out var expectedVersion))
                    {
                        return await WriteProblemAsync(httpContext, 400, "validation_failed", "Validation failed", "A valid If-Match version and source-decision body are required.", "procurement.source-decision.record");
                    }

                    return await ExecuteMutationAsync(
                        httpContext,
                        resolver,
                        tenantResolver,
                        auditCoordinator,
                        idempotencyStore,
                        FoundationOperationCatalog.GetRequired("procurement.source-decision.record"),
                        Fingerprint(request) + VersionFingerprint(expectedVersion),
                        context => service.RecordSourceDecisionAsync(context, purchaseRequestId, request, expectedVersion, GetIdempotencyKey(httpContext), httpContext.RequestAborted),
                        (_, decision) => ToSourceDecisionResponse(decision),
                        setEtag: true,
                        requireExpectedVersion: true);
                })
            .WithName("procurement.source-decision.record")
            .WithTags("Procurement / Supplier Quotations")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("procurement.source-decision.record")));

        endpoints.MapGet(
                "/api/v1/procurement/quotations/{quotationId:guid}/history",
                async (Guid quotationId, HttpContext httpContext, ITrustedRequestContextResolver resolver, ProcurementTenantContextResolver tenantResolver, SupplierQuotationService service) =>
                    await ExecuteReadAsync(
                        httpContext,
                        resolver,
                        tenantResolver,
                        FoundationOperationCatalog.GetRequired("procurement.quotation.history.read"),
                        context => service.ReadHistoryAsync(context, quotationId, httpContext.RequestAborted),
                        (_, records) => records.Select(ToHistoryResponse).ToArray()))
            .WithName("procurement.quotation.history.read")
            .WithTags("Procurement / Supplier Quotations")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("procurement.quotation.history.read")));

        endpoints.MapGet(
                "/api/v1/procurement/quotations/{quotationId:guid}/audit",
                async (Guid quotationId, HttpContext httpContext, ITrustedRequestContextResolver resolver, ProcurementTenantContextResolver tenantResolver, SupplierQuotationService service) =>
                    await ExecuteReadAsync(
                        httpContext,
                        resolver,
                        tenantResolver,
                        FoundationOperationCatalog.GetRequired("procurement.quotation.audit.read"),
                        context => service.ReadAuditAsync(context, quotationId, httpContext.RequestAborted),
                        (_, records) => records.Select(ToAuditResponse).ToArray()))
            .WithName("procurement.quotation.audit.read")
            .WithTags("Procurement / Supplier Quotations")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("procurement.quotation.audit.read")));

        return endpoints;
    }

    private static async Task<IResult> ExecuteMutationAsyncAction(
        Guid quotationId,
        HttpContext httpContext,
        ITrustedRequestContextResolver resolver,
        ProcurementTenantContextResolver tenantResolver,
        FoundationAuditCoordinator auditCoordinator,
        LocalMasterDataIdempotencyStore idempotencyStore,
        SupplierQuotationService service,
        FoundationOperationDescriptor descriptor,
        Func<ProcurementRequestContext, byte[], string, SupplierQuotationActionRequest?, Task<SupplierQuotationOperationResult<SupplierQuotationRecord>>> operation,
        SupplierQuotationActionRequest? request = null)
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
            context => operation(context, expectedVersion, GetIdempotencyKey(httpContext)!, request),
            (context, record) => ToResponse(record, context),
            setEtag: true,
            requireExpectedVersion: true);
    }

    private static async Task<IResult> ExecuteReadAsync<T>(
        HttpContext httpContext,
        ITrustedRequestContextResolver resolver,
        ProcurementTenantContextResolver tenantResolver,
        FoundationOperationDescriptor descriptor,
        Func<ProcurementRequestContext, Task<SupplierQuotationOperationResult<T>>> operation,
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
        Func<ProcurementRequestContext, Task<SupplierQuotationOperationResult<T>>> operation,
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
            return ToResult(httpContext, SupplierQuotationOperationResult<T>.Success(replay), descriptor.OperationId, context, map, setEtag);
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
                operationVersion: "procurement.supplier-quotation.v1",
                cancellationToken: httpContext.RequestAborted);
            if (!execution.Succeeded || execution.Value is null)
            {
                return await WriteProblemAsync(httpContext, 503, execution.Code, "Operation unavailable", "The Supplier Quotation operation could not be completed.", descriptor.OperationId);
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
        SupplierQuotationOperationResult<T> result,
        string operationId,
        ProcurementRequestContext context,
        Func<ProcurementRequestContext, T, object?> map,
        bool setEtag)
    {
        if (result.Succeeded && result.Value is not null)
        {
            if (setEtag && result.Value is SupplierQuotationRecord quotation)
            {
                httpContext.Response.Headers.ETag = $"\"{Convert.ToBase64String(quotation.Version)}\"";
            }
            else if (setEtag && result.Value is SupplierSourceDecisionRecord decision)
            {
                httpContext.Response.Headers.ETag = $"\"{Convert.ToBase64String(decision.Version)}\"";
            }

            return Results.Json(map(context, result.Value));
        }

        var code = result.Code;
        var status = code switch
        {
            "permission_denied" or "resource_scope_denied" or "cross_tenant_target_denied" or "tenant_context_failed" or "authorization_profile_denied" => 403,
            "persistence_unavailable" or "reference_persistence_unavailable" or "authorization_operation_unmapped" => 503,
            "quotation_not_found" or "purchase_request_not_found" or "source_decision_not_found" => 404,
            "concurrency_conflict" or "quotation_duplicate" or "edit_not_allowed" or "submit_not_allowed" or "action_not_allowed" or "source_decision_not_allowed" or "purchase_request_not_approved" or "supplier_inactive" or "currency_inactive" or "payment_term_inactive" or "tax_inactive" => 409,
            _ => 400
        };

        return Results.Problem(
            statusCode: status,
            title: status == 403 ? "Access denied" : "Supplier Quotation operation failed",
            detail: "The Supplier Quotation operation could not be completed.",
            type: $"https://api.minierp.local/problems/{code}",
            extensions: new Dictionary<string, object?>
            {
                ["code"] = code,
                ["correlationId"] = GetCorrelation(httpContext),
                ["operationId"] = operationId
            });
    }

    private static SupplierQuotationResponse ToResponse(
        SupplierQuotationRecord record,
        ProcurementRequestContext context)
    {
        var canEdit = context.ActorId == record.CreatedByActorId
            && record.Status == SupplierQuotationStatus.Draft;
        var canSubmit = canEdit;
        var canWithdraw = record.Status == SupplierQuotationStatus.Submitted;
        var canDisqualify = canWithdraw;
        return new SupplierQuotationResponse(
            record.Id,
            record.TenantId,
            record.PurchaseRequestId,
            record.Scope.CompanyId,
            record.Scope.BranchId,
            record.CreatedByActorId,
            new SupplierQuotationSupplierResponse(record.Supplier.Id, record.Supplier.Code, record.Supplier.Name),
            record.Status.ToString(),
            record.SupplierQuotationReference,
            record.OfferDate,
            record.ValidUntil,
            new SupplierQuotationCurrencyResponse(record.Currency.Id, record.Currency.Code, record.Currency.Name),
            record.PaymentTerm is null
                ? null
                : new SupplierQuotationPaymentTermResponse(record.PaymentTerm.Id, record.PaymentTerm.Code, record.PaymentTerm.Name, record.PaymentTerm.Version),
            record.DeliveryTerms,
            record.OfferedDeliveryDate,
            record.OfferedDeliveryLeadTime,
            record.Notes,
            record.Lines.Select(ToLineResponse).ToArray(),
            record.Evidence.Select(ToEvidenceResponse).ToArray(),
            record.CreatedAt,
            record.UpdatedAt,
            record.SubmittedAt,
            record.IsSelected,
            record.Version,
            canEdit,
            canSubmit,
            canWithdraw,
            canDisqualify);
    }

    private static SupplierQuotationListItemResponse ToListResponse(SupplierQuotationRecord record) => new(
        record.Id,
        record.PurchaseRequestId,
        new SupplierQuotationSupplierResponse(record.Supplier.Id, record.Supplier.Code, record.Supplier.Name),
        record.Status.ToString(),
        record.SupplierQuotationReference,
        record.OfferDate,
        record.ValidUntil,
        new SupplierQuotationCurrencyResponse(record.Currency.Id, record.Currency.Code, record.Currency.Name),
        SupplierQuotationValuePolicy.CommercialTotal(record),
        record.Lines.Count,
        record.Lines.Count,
        record.Evidence.Count > 0,
        record.Version);

    private static SupplierQuotationLineResponse ToLineResponse(SupplierQuotationLineSnapshot line) => new(
        line.Id,
        line.PurchaseRequestLineId,
        line.ProductId,
        line.ProductSku,
        line.ProductName,
        line.UnitOfMeasureId,
        line.UnitOfMeasureCode,
        line.RequestedQuantity,
        line.QuotedQuantity,
        line.UnitPrice,
        line.DiscountAmount,
        line.DiscountPercentage,
        line.TaxId,
        line.TaxCode,
        line.TaxName,
        line.TaxRatePercentage,
        line.TaxAmount,
        line.TaxReference,
        line.RequestedNeedByDate,
        line.OfferedDeliveryDate,
        line.OfferedDeliveryLeadTime,
        line.Notes,
        line.Version ?? []);

    private static SupplierQuotationEvidenceReferenceResponse ToEvidenceResponse(SupplierQuotationEvidenceReference evidence) => new(
        evidence.Id,
        evidence.ReferenceId,
        evidence.FileName,
        evidence.ContentType,
        evidence.Description,
        evidence.Source,
        evidence.ExternalReference,
        evidence.RecordedByActorId,
        evidence.RecordedAt);

    private static SupplierQuotationHistoryResponse ToHistoryResponse(SupplierQuotationHistoryRecord record) => new(
        record.EvidenceId,
        record.SupplierQuotationId,
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

    private static SupplierSourceDecisionHistoryResponse ToSourceDecisionHistoryResponse(SupplierSourceDecisionHistoryRecord record) => new(
        record.Id,
        record.TenantId,
        record.SourceDecisionId,
        record.PurchaseRequestId,
        record.PreviousSelectedQuotationId,
        record.SelectedQuotationId,
        record.ActorId,
        record.SelectedAt,
        record.Rationale,
        record.PolicyId,
        record.PolicyVersion,
        record.StageKey,
        record.ComparisonSnapshotReference);

    private static SupplierQuotationAuditResponse ToAuditResponse(SupplierQuotationAuditRecord record) => new(
        record.EvidenceId,
        record.SupplierQuotationId,
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

    private static SupplierQuotationComparisonResponse ToComparisonResponse(SupplierQuotationComparisonModel model) => new(
        model.PurchaseRequestId,
        model.HasMixedCurrencies,
        model.DirectCurrencyComparisonAvailable,
        model.ComparisonBasis,
        model.CurrencyGroups.Select(group => new SupplierQuotationCurrencyComparisonGroupResponse(
            group.CurrencyId,
            group.CurrencyCode,
            group.SupplierQuotationIds,
            group.DirectlyComparableWithinGroup)).ToArray(),
        model.Quotations.Select(item => new SupplierQuotationComparisonItemResponse(
            item.SupplierQuotationId,
            new SupplierQuotationSupplierResponse(item.Supplier.Id, item.Supplier.Code, item.Supplier.Name),
            item.Status.ToString(),
            item.SupplierQuotationReference,
            item.OfferDate,
            item.ValidUntil,
            new SupplierQuotationCurrencyResponse(item.Currency.Id, item.Currency.Code, item.Currency.Name),
            item.CommercialTotal,
            item.CoveredLineCount,
            item.RequestedLineCount,
            item.HasEvidence,
            item.IsDirectlyComparableToAll,
            item.PaymentTermCode,
            item.DeliveryTerms,
            item.OfferedDeliveryDate,
            item.OfferedDeliveryLeadTime,
            item.Lines.Select(line => new SupplierQuotationComparisonLineResponse(
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
                line.IsCovered,
                line.QualificationIssue)).ToArray(),
            item.QualificationIssues)).ToArray(),
        model.CurrentSourceDecision is null ? null : ToSourceDecisionResponse(model.CurrentSourceDecision));

    private static SupplierSourceDecisionResponse ToSourceDecisionResponse(SupplierSourceDecisionRecord decision) => new(
        decision.Id,
        decision.TenantId,
        decision.PurchaseRequestId,
        decision.SelectedQuotationId,
        new SupplierQuotationSupplierResponse(decision.Supplier.Id, decision.Supplier.Code, decision.Supplier.Name),
        decision.SupplierQuotationReference,
        decision.ActorId,
        decision.SelectedAt,
        decision.Rationale,
        decision.PolicyId,
        decision.PolicyVersion,
        decision.StageKey,
        decision.ComparisonSnapshotReference,
        decision.Version);

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
