#pragma warning disable CS1591

using System.Text.Json;
using Microsoft.AspNetCore.Antiforgery;
using MiniErp.App.BuildingBlocks.Rest;
using MiniErp.App.Modules.Audit;
using MiniErp.App.Modules.MasterData;
using MiniErp.Contracts.Modules.Audit;
using MiniErp.Contracts.Modules.Foundation;
using MiniErp.Contracts.Modules.MasterData;

namespace MiniErp.Api;

/// <summary>REST composition for the bounded MESP-118 capability.</summary>
public static class CurrencyPaymentTermEndpoints
{
    public static IEndpointRouteBuilder MapCurrencyPaymentTermEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapGet(
            "/api/v1/master-data/currencies",
            async (HttpContext httpContext, ITrustedRequestContextResolver resolver, MasterDataTenantContextResolver tenantResolver, MasterDataCurrencyPaymentTermService service) =>
                await ExecuteReadAsync(httpContext, resolver, tenantResolver, FoundationOperationCatalog.GetRequired("master-data.currency.list"), context => service.ListCurrenciesAsync(context, httpContext.RequestAborted), records => records.Select(ToResponse).ToArray()))
            .WithName("master-data.currency.list")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("master-data.currency.list")));

        endpoints.MapGet(
            "/api/v1/master-data/currencies/{currencyId:guid}",
            async (Guid currencyId, HttpContext httpContext, ITrustedRequestContextResolver resolver, MasterDataTenantContextResolver tenantResolver, MasterDataCurrencyPaymentTermService service) =>
                await ExecuteReadAsync(httpContext, resolver, tenantResolver, FoundationOperationCatalog.GetRequired("master-data.currency.read"), context => service.GetCurrencyAsync(context, currencyId, httpContext.RequestAborted), ToResponse))
            .WithName("master-data.currency.read")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("master-data.currency.read")));

        endpoints.MapGet(
            "/api/v1/master-data/currencies/{currencyId:guid}/reference",
            async (Guid currencyId, HttpContext httpContext, ITrustedRequestContextResolver resolver, MasterDataTenantContextResolver tenantResolver, MasterDataCurrencyPaymentTermService service) =>
                await ExecuteReadAsync(httpContext, resolver, tenantResolver, FoundationOperationCatalog.GetRequired("master-data.currency.reference.read"), context => service.GetCurrencyReferenceAsync(context, currencyId, httpContext.RequestAborted), ToReferenceResponse))
            .WithName("master-data.currency.reference.read")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("master-data.currency.reference.read")));

        endpoints.MapPost(
            "/api/v1/master-data/currencies",
            async (CurrencyWriteRequest? request, HttpContext httpContext, ITrustedRequestContextResolver resolver, MasterDataTenantContextResolver tenantResolver, FoundationAuditCoordinator auditCoordinator, LocalMasterDataIdempotencyStore idempotencyStore, MasterDataCurrencyPaymentTermService service) =>
            {
                if (!TryCreateCurrencyCommand(request, out var command))
                {
                    return await WriteProblemAsync(httpContext, 400, "validation_failed", "Validation failed", "The Currency request is invalid.", "master-data.currency.create");
                }

                return await ExecuteMutationAsync(httpContext, resolver, tenantResolver, auditCoordinator, idempotencyStore, FoundationOperationCatalog.GetRequired("master-data.currency.create"), Fingerprint(request), context => service.CreateCurrencyAsync(context, command!, httpContext.RequestAborted), ToResponse, setEtag: true);
            })
            .WithName("master-data.currency.create")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("master-data.currency.create")));

        endpoints.MapPost(
            "/api/v1/master-data/currencies/{currencyId:guid}/edit",
            async (Guid currencyId, CurrencyWriteRequest? request, HttpContext httpContext, ITrustedRequestContextResolver resolver, MasterDataTenantContextResolver tenantResolver, FoundationAuditCoordinator auditCoordinator, LocalMasterDataIdempotencyStore idempotencyStore, MasterDataCurrencyPaymentTermService service) =>
            {
                if (!TryReadExpectedVersion(httpContext, out var expectedVersion) || !TryCreateCurrencyEditCommand(currencyId, request, expectedVersion, out var command))
                {
                    return await WriteProblemAsync(httpContext, 400, "validation_failed", "Validation failed", "A valid If-Match version and Currency request are required.", "master-data.currency.edit");
                }

                return await ExecuteMutationAsync(httpContext, resolver, tenantResolver, auditCoordinator, idempotencyStore, FoundationOperationCatalog.GetRequired("master-data.currency.edit"), Fingerprint(request) + VersionFingerprint(expectedVersion), context => service.EditCurrencyAsync(context, command!, httpContext.RequestAborted), ToResponse, setEtag: true, requireExpectedVersion: true);
            })
            .WithName("master-data.currency.edit")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("master-data.currency.edit")));

        endpoints.MapPost(
            "/api/v1/master-data/currencies/{currencyId:guid}/deactivate",
            async (Guid currencyId, MasterDataLifecycleRequest? _, HttpContext httpContext, ITrustedRequestContextResolver resolver, MasterDataTenantContextResolver tenantResolver, FoundationAuditCoordinator auditCoordinator, LocalMasterDataIdempotencyStore idempotencyStore, MasterDataCurrencyPaymentTermService service) =>
            {
                if (!TryReadExpectedVersion(httpContext, out var expectedVersion))
                {
                    return await WriteProblemAsync(httpContext, 400, "validation_failed", "Validation failed", "A valid If-Match version is required.", "master-data.currency.deactivate");
                }

                return await ExecuteMutationAsync(httpContext, resolver, tenantResolver, auditCoordinator, idempotencyStore, FoundationOperationCatalog.GetRequired("master-data.currency.deactivate"), VersionFingerprint(expectedVersion), context => service.DeactivateCurrencyAsync(context, currencyId, expectedVersion, httpContext.RequestAborted), ToResponse, setEtag: true, requireExpectedVersion: true);
            })
            .WithName("master-data.currency.deactivate")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("master-data.currency.deactivate")));

        endpoints.MapPost(
            "/api/v1/master-data/currencies/{currencyId:guid}/reactivate",
            async (Guid currencyId, HttpContext httpContext, ITrustedRequestContextResolver resolver, MasterDataTenantContextResolver tenantResolver, FoundationAuditCoordinator auditCoordinator, LocalMasterDataIdempotencyStore idempotencyStore, MasterDataCurrencyPaymentTermService service) =>
            {
                if (!TryReadExpectedVersion(httpContext, out var expectedVersion))
                {
                    return await WriteProblemAsync(httpContext, 400, "validation_failed", "Validation failed", "A valid If-Match version is required.", "master-data.currency.reactivate");
                }

                return await ExecuteMutationAsync(httpContext, resolver, tenantResolver, auditCoordinator, idempotencyStore, FoundationOperationCatalog.GetRequired("master-data.currency.reactivate"), VersionFingerprint(expectedVersion), context => service.ReactivateCurrencyAsync(context, currencyId, expectedVersion, httpContext.RequestAborted), ToResponse, setEtag: true, requireExpectedVersion: true);
            })
            .WithName("master-data.currency.reactivate")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("master-data.currency.reactivate")));

        endpoints.MapGet(
            "/api/v1/master-data/currencies/{currencyId:guid}/audit",
            async (Guid currencyId, HttpContext httpContext, ITrustedRequestContextResolver resolver, MasterDataTenantContextResolver tenantResolver, MasterDataCurrencyPaymentTermService service) =>
                await ExecuteReadAsync(httpContext, resolver, tenantResolver, FoundationOperationCatalog.GetRequired("master-data.currency.audit.read"), context => service.ReadAuditHistoryAsync(context, MasterDataResourceKind.Currency, currencyId, httpContext.RequestAborted), records => records.Select(ToAuditResponse).ToArray()))
            .WithName("master-data.currency.audit.read")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("master-data.currency.audit.read")));

        endpoints.MapGet(
            "/api/v1/master-data/payment-terms",
            async (HttpContext httpContext, ITrustedRequestContextResolver resolver, MasterDataTenantContextResolver tenantResolver, MasterDataCurrencyPaymentTermService service) =>
                await ExecuteReadAsync(httpContext, resolver, tenantResolver, FoundationOperationCatalog.GetRequired("master-data.payment-term.list"), context => service.ListPaymentTermsAsync(context, httpContext.RequestAborted), records => records.Select(ToResponse).ToArray()))
            .WithName("master-data.payment-term.list")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("master-data.payment-term.list")));

        endpoints.MapGet(
            "/api/v1/master-data/payment-terms/{paymentTermId:guid}",
            async (Guid paymentTermId, HttpContext httpContext, ITrustedRequestContextResolver resolver, MasterDataTenantContextResolver tenantResolver, MasterDataCurrencyPaymentTermService service) =>
                await ExecuteReadAsync(httpContext, resolver, tenantResolver, FoundationOperationCatalog.GetRequired("master-data.payment-term.read"), context => service.GetPaymentTermAsync(context, paymentTermId, httpContext.RequestAborted), ToResponse))
            .WithName("master-data.payment-term.read")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("master-data.payment-term.read")));

        endpoints.MapGet(
            "/api/v1/master-data/payment-terms/{paymentTermId:guid}/history",
            async (Guid paymentTermId, HttpContext httpContext, ITrustedRequestContextResolver resolver, MasterDataTenantContextResolver tenantResolver, MasterDataCurrencyPaymentTermService service) =>
                await ExecuteReadAsync(httpContext, resolver, tenantResolver, FoundationOperationCatalog.GetRequired("master-data.payment-term.history.read"), context => service.GetPaymentTermAsync(context, paymentTermId, httpContext.RequestAborted), record => record.Versions.Select(ToResponse).ToArray()))
            .WithName("master-data.payment-term.history.read")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("master-data.payment-term.history.read")));

        endpoints.MapGet(
            "/api/v1/master-data/payment-terms/{paymentTermId:guid}/reference",
            async (Guid paymentTermId, HttpContext httpContext, ITrustedRequestContextResolver resolver, MasterDataTenantContextResolver tenantResolver, MasterDataCurrencyPaymentTermService service) =>
            {
                if (!TryReadDate(httpContext.Request.Query["effectiveOn"], out var effectiveOn))
                {
                    return await WriteProblemAsync(httpContext, 400, "validation_failed", "Validation failed", "An ISO effectiveOn date is required.", "master-data.payment-term.reference.read");
                }

                return await ExecuteReadAsync(httpContext, resolver, tenantResolver, FoundationOperationCatalog.GetRequired("master-data.payment-term.reference.read"), context => service.GetPaymentTermReferenceAsync(context, paymentTermId, effectiveOn, httpContext.RequestAborted), ToReferenceResponse);
            })
            .WithName("master-data.payment-term.reference.read")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("master-data.payment-term.reference.read")));

        endpoints.MapGet(
            "/api/v1/master-data/payment-terms/{paymentTermId:guid}/due-date-preview",
            async (Guid paymentTermId, HttpContext httpContext, ITrustedRequestContextResolver resolver, MasterDataTenantContextResolver tenantResolver, MasterDataCurrencyPaymentTermService service) =>
            {
                if (!TryReadDate(httpContext.Request.Query["effectiveOn"], out var effectiveOn)
                    || !TryReadDate(httpContext.Request.Query["baseDate"], out var baseDate))
                {
                    return await WriteProblemAsync(httpContext, 400, "validation_failed", "Validation failed", "ISO effectiveOn and baseDate dates are required.", "master-data.payment-term.preview.read");
                }

                return await ExecuteReadAsync(httpContext, resolver, tenantResolver, FoundationOperationCatalog.GetRequired("master-data.payment-term.preview.read"), context => service.PreviewPaymentTermAsync(context, paymentTermId, effectiveOn, baseDate, httpContext.RequestAborted), ToResponse);
            })
            .WithName("master-data.payment-term.preview.read")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("master-data.payment-term.preview.read")));

        endpoints.MapPost(
            "/api/v1/master-data/payment-terms",
            async (PaymentTermWriteRequest? request, HttpContext httpContext, ITrustedRequestContextResolver resolver, MasterDataTenantContextResolver tenantResolver, FoundationAuditCoordinator auditCoordinator, LocalMasterDataIdempotencyStore idempotencyStore, MasterDataCurrencyPaymentTermService service) =>
            {
                if (!TryCreatePaymentTermCommand(request, out var command))
                {
                    return await WriteProblemAsync(httpContext, 400, "validation_failed", "Validation failed", "The Payment Term request is invalid.", "master-data.payment-term.create");
                }

                return await ExecuteMutationAsync(httpContext, resolver, tenantResolver, auditCoordinator, idempotencyStore, FoundationOperationCatalog.GetRequired("master-data.payment-term.create"), Fingerprint(request), context => service.CreatePaymentTermAsync(context, command!, httpContext.RequestAborted), ToResponse, setEtag: true);
            })
            .WithName("master-data.payment-term.create")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("master-data.payment-term.create")));

        endpoints.MapPost(
            "/api/v1/master-data/payment-terms/{paymentTermId:guid}/edit",
            async (Guid paymentTermId, PaymentTermWriteRequest? request, HttpContext httpContext, ITrustedRequestContextResolver resolver, MasterDataTenantContextResolver tenantResolver, FoundationAuditCoordinator auditCoordinator, LocalMasterDataIdempotencyStore idempotencyStore, MasterDataCurrencyPaymentTermService service) =>
            {
                if (!TryReadExpectedVersion(httpContext, out var expectedVersion) || !TryCreatePaymentTermEditCommand(paymentTermId, request, expectedVersion, out var command))
                {
                    return await WriteProblemAsync(httpContext, 400, "validation_failed", "Validation failed", "A valid If-Match version and Payment Term request are required.", "master-data.payment-term.edit");
                }

                return await ExecuteMutationAsync(httpContext, resolver, tenantResolver, auditCoordinator, idempotencyStore, FoundationOperationCatalog.GetRequired("master-data.payment-term.edit"), Fingerprint(request) + VersionFingerprint(expectedVersion), context => service.EditPaymentTermAsync(context, command!, httpContext.RequestAborted), ToResponse, setEtag: true, requireExpectedVersion: true);
            })
            .WithName("master-data.payment-term.edit")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("master-data.payment-term.edit")));

        endpoints.MapPost(
            "/api/v1/master-data/payment-terms/{paymentTermId:guid}/deactivate",
            async (Guid paymentTermId, MasterDataLifecycleRequest? _, HttpContext httpContext, ITrustedRequestContextResolver resolver, MasterDataTenantContextResolver tenantResolver, FoundationAuditCoordinator auditCoordinator, LocalMasterDataIdempotencyStore idempotencyStore, MasterDataCurrencyPaymentTermService service) =>
            {
                if (!TryReadExpectedVersion(httpContext, out var expectedVersion))
                {
                    return await WriteProblemAsync(httpContext, 400, "validation_failed", "Validation failed", "A valid If-Match version is required.", "master-data.payment-term.deactivate");
                }

                return await ExecuteMutationAsync(httpContext, resolver, tenantResolver, auditCoordinator, idempotencyStore, FoundationOperationCatalog.GetRequired("master-data.payment-term.deactivate"), VersionFingerprint(expectedVersion), context => service.DeactivatePaymentTermAsync(context, paymentTermId, expectedVersion, httpContext.RequestAborted), ToResponse, setEtag: true, requireExpectedVersion: true);
            })
            .WithName("master-data.payment-term.deactivate")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("master-data.payment-term.deactivate")));

        endpoints.MapPost(
            "/api/v1/master-data/payment-terms/{paymentTermId:guid}/reactivate",
            async (Guid paymentTermId, HttpContext httpContext, ITrustedRequestContextResolver resolver, MasterDataTenantContextResolver tenantResolver, FoundationAuditCoordinator auditCoordinator, LocalMasterDataIdempotencyStore idempotencyStore, MasterDataCurrencyPaymentTermService service) =>
            {
                if (!TryReadExpectedVersion(httpContext, out var expectedVersion))
                {
                    return await WriteProblemAsync(httpContext, 400, "validation_failed", "Validation failed", "A valid If-Match version is required.", "master-data.payment-term.reactivate");
                }

                return await ExecuteMutationAsync(httpContext, resolver, tenantResolver, auditCoordinator, idempotencyStore, FoundationOperationCatalog.GetRequired("master-data.payment-term.reactivate"), VersionFingerprint(expectedVersion), context => service.ReactivatePaymentTermAsync(context, paymentTermId, expectedVersion, httpContext.RequestAborted), ToResponse, setEtag: true, requireExpectedVersion: true);
            })
            .WithName("master-data.payment-term.reactivate")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("master-data.payment-term.reactivate")));

        endpoints.MapGet(
            "/api/v1/master-data/payment-terms/{paymentTermId:guid}/audit",
            async (Guid paymentTermId, HttpContext httpContext, ITrustedRequestContextResolver resolver, MasterDataTenantContextResolver tenantResolver, MasterDataCurrencyPaymentTermService service) =>
                await ExecuteReadAsync(httpContext, resolver, tenantResolver, FoundationOperationCatalog.GetRequired("master-data.payment-term.audit.read"), context => service.ReadAuditHistoryAsync(context, MasterDataResourceKind.PaymentTerm, paymentTermId, httpContext.RequestAborted), records => records.Select(ToAuditResponse).ToArray()))
            .WithName("master-data.payment-term.audit.read")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("master-data.payment-term.audit.read")));

        return endpoints;
    }

    private static async Task<IResult> ExecuteReadAsync<T>(HttpContext httpContext, ITrustedRequestContextResolver resolver, MasterDataTenantContextResolver tenantResolver, FoundationOperationDescriptor descriptor, Func<MasterDataRequestContext, Task<MasterDataOperationResult<T>>> operation, Func<T, object?> map)
    {
        var foundationContext = await resolver.ResolveAsync(httpContext, httpContext.RequestAborted);
        var resolution = tenantResolver.Resolve(foundationContext);
        if (!resolution.Allowed || resolution.Context is null)
        {
            return await WriteProblemAsync(httpContext, foundationContext.SecurityProfile == FoundationSecurityProfile.Anonymous ? 401 : 403, resolution.Code, foundationContext.SecurityProfile == FoundationSecurityProfile.Anonymous ? "Authentication required" : "Access denied", "The operation is not available for this security context.", descriptor.OperationId);
        }

        return ToResult(httpContext, await operation(resolution.Context), descriptor.OperationId, map, setEtag: false);
    }

    private static async Task<IResult> ExecuteMutationAsync<T>(HttpContext httpContext, ITrustedRequestContextResolver resolver, MasterDataTenantContextResolver tenantResolver, FoundationAuditCoordinator auditCoordinator, LocalMasterDataIdempotencyStore idempotencyStore, FoundationOperationDescriptor descriptor, string fingerprint, Func<MasterDataRequestContext, Task<MasterDataOperationResult<T>>> operation, Func<T, object?> map, bool setEtag, bool requireExpectedVersion = false)
    {
        if (!await EnsureAntiforgeryAsync(httpContext))
        {
            return await WriteProblemAsync(httpContext, 403, "antiforgery_failed", "Antiforgery validation failed", "The request could not be validated.", descriptor.OperationId);
        }

        if (requireExpectedVersion && !TryReadExpectedVersion(httpContext, out _))
        {
            return await WriteProblemAsync(httpContext, 400, "validation_failed", "Validation failed", "A valid If-Match version is required.", descriptor.OperationId);
        }

        var key = httpContext.Request.Headers["Idempotency-Key"].FirstOrDefault();
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
            return ToResult(httpContext, MasterDataOperationResult<T>.Success(replay), descriptor.OperationId, map, setEtag: setEtag);
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
                cancellationToken: httpContext.RequestAborted);
            if (!execution.Succeeded || execution.Value is null)
            {
                return await WriteProblemAsync(httpContext, 503, execution.Code, "Operation unavailable", "The operation could not be completed.", descriptor.OperationId);
            }

            var result = execution.Value;
            if (result.Succeeded && result.Value is not null)
            {
                idempotencyStore.Commit(key!, binding, result.Value);
                committed = true;
            }

            return ToResult(httpContext, result, descriptor.OperationId, map, setEtag);
        }
        finally
        {
            if (!committed)
            {
                idempotencyStore.Release(key!, binding);
            }
        }
    }

    private static IResult ToResult<T>(HttpContext httpContext, MasterDataOperationResult<T> result, string operationId, Func<T, object?> map, bool setEtag)
    {
        if (result.Succeeded && result.Value is not null)
        {
            if (setEtag && result.Value is MasterDataCurrencyRecord currency)
            {
                httpContext.Response.Headers.ETag = $"\"{Convert.ToBase64String(currency.Version)}\"";
            }
            else if (setEtag && result.Value is MasterDataPaymentTermRecord paymentTerm)
            {
                httpContext.Response.Headers.ETag = $"\"{Convert.ToBase64String(paymentTerm.Version)}\"";
            }

            return Results.Json(map(result.Value));
        }

        var code = result.Code;
        var status = code switch
        {
            "permission_denied" or "resource_scope_denied" or "cross_tenant_target_denied" or "tenant_context_failed" or "authorization_denied" or "approval_required" or "approval_policy_not_configured" or "resource_policy_not_configured" => 403,
            "permission_unavailable" or "scope_policy_unavailable" or "approval_policy_unavailable" or "resource_policy_unavailable" or "authorization_operation_unmapped" or "persistence_unavailable" or "audit_unavailable" or "audit_context_mismatch" => 503,
            "currency_not_found" or "payment_term_not_found" or "payment_term_version_not_found" => 404,
            "concurrency_conflict" or "payment_term_effective_overlap" or "currency_duplicate" or "payment_term_duplicate" or "currency_lifecycle_no_change" or "payment_term_lifecycle_no_change" => 409,
            _ => 400
        };

        return Results.Problem(statusCode: status, title: status == 403 ? "Access denied" : "Master Data operation failed", detail: "The Currency or Payment Term operation could not be completed.", type: $"https://api.minierp.local/problems/{code}", extensions: new Dictionary<string, object?> { ["code"] = code, ["correlationId"] = GetCorrelation(httpContext), ["operationId"] = operationId });
    }

    private static bool TryCreateCurrencyCommand(CurrencyWriteRequest? request, out CreateMasterDataCurrencyCommand? command)
    {
        command = null;
        if (request is null) return false;
        try { command = new CreateMasterDataCurrencyCommand(request.Code ?? string.Empty, new LocalizedName(request.EnglishName, request.ArabicName)); return true; }
        catch (ArgumentException) { return false; }
    }

    private static bool TryCreateCurrencyEditCommand(Guid id, CurrencyWriteRequest? request, byte[] expectedVersion, out EditMasterDataCurrencyCommand? command)
    {
        command = null;
        if (request is null) return false;
        try { command = new EditMasterDataCurrencyCommand(id, request.Code ?? string.Empty, new LocalizedName(request.EnglishName, request.ArabicName), expectedVersion); return true; }
        catch (ArgumentException) { return false; }
    }

    private static bool TryCreatePaymentTermCommand(PaymentTermWriteRequest? request, out CreateMasterDataPaymentTermCommand? command)
    {
        command = null;
        if (request is null) return false;
        try
        {
            command = new CreateMasterDataPaymentTermCommand(request.Code ?? string.Empty, new LocalizedName(request.EnglishName, request.ArabicName), request.EffectiveFrom, request.EffectiveTo, request.BaseDateRule, request.ScheduleMode, ToOffset(request.DueOffset), ToInstallments(request.Installments), ToDiscount(request.EarlySettlementDiscount));
            return true;
        }
        catch (ArgumentException) { return false; }
    }

    private static bool TryCreatePaymentTermEditCommand(Guid id, PaymentTermWriteRequest? request, byte[] expectedVersion, out EditMasterDataPaymentTermCommand? command)
    {
        command = null;
        if (request is null) return false;
        try
        {
            command = new EditMasterDataPaymentTermCommand(id, request.Code ?? string.Empty, new LocalizedName(request.EnglishName, request.ArabicName), request.EffectiveFrom, request.EffectiveTo, request.BaseDateRule, request.ScheduleMode, ToOffset(request.DueOffset), ToInstallments(request.Installments), ToDiscount(request.EarlySettlementDiscount), expectedVersion);
            return true;
        }
        catch (ArgumentException) { return false; }
    }

    private static MasterDataPaymentTermOffset ToOffset(PaymentTermOffsetRequest? offset) => new(offset?.Days ?? 0, offset?.Months ?? 0);
    private static IReadOnlyList<MasterDataPaymentTermInstallment> ToInstallments(IReadOnlyList<PaymentTermInstallmentRequest>? installments) => (installments ?? []).Select(item => new MasterDataPaymentTermInstallment(item.Sequence, item.Percentage, new MasterDataPaymentTermOffset(item.Days, item.Months))).ToArray();
    private static MasterDataEarlySettlementDiscount ToDiscount(EarlySettlementDiscountRequest? discount) => discount is null ? MasterDataEarlySettlementDiscount.Disabled() : new(discount.Enabled, discount.Percentage, new MasterDataPaymentTermOffset(discount.Days, discount.Months));

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

    private static bool TryReadDate(string? value, out DateOnly date) => DateOnly.TryParseExact(value, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out date);
    private static string Fingerprint(object? request) => JsonSerializer.Serialize(request);
    private static string VersionFingerprint(byte[] version) => $"|version:{Convert.ToBase64String(version)}";

    private static CurrencyResponse ToResponse(MasterDataCurrencyRecord record) => new(record.Id, record.TenantId.Value, record.Code, record.Name.English, record.Name.Arabic, record.LifecycleState.ToString(), record.Revision, record.Version);
    private static CurrencyReferenceResponse ToReferenceResponse(MasterDataCurrencyReferenceRecord record) => new(record.Id, record.TenantId.Value, record.Code, record.Name.English, record.Name.Arabic, record.LifecycleState.ToString(), record.Revision, record.Snapshot.AppliedValue, record.Snapshot.EffectiveOn?.ToString("yyyy-MM-dd") ?? string.Empty, record.Version);
    private static PaymentTermResponse ToResponse(MasterDataPaymentTermRecord record) => new(record.Id, record.TenantId.Value, record.Code, record.Name.English, record.Name.Arabic, record.LifecycleState.ToString(), record.CurrentVersionNumber, record.Versions.Select(ToResponse).ToArray(), record.Version);
    private static PaymentTermVersionResponse ToResponse(MasterDataPaymentTermVersionRecord record) => new(record.Id, record.VersionNumber, record.EffectiveFrom, record.EffectiveTo, record.BaseDateRule, record.ScheduleMode, record.DueOffset.Days, record.DueOffset.Months, record.Installments.Select(item => new PaymentTermInstallmentResponse(item.Sequence, item.Percentage, item.Offset.Days, item.Offset.Months)).ToArray(), record.EarlySettlementDiscount.Enabled, record.EarlySettlementDiscount.Percentage, record.EarlySettlementDiscount.Offset.Days, record.EarlySettlementDiscount.Offset.Months, record.Code, record.Name.English, record.Name.Arabic);
    private static PaymentTermReferenceResponse ToReferenceResponse(MasterDataPaymentTermReferenceRecord record) => new(record.Id, record.TenantId.Value, record.Code, record.Name.English, record.Name.Arabic, record.LifecycleState.ToString(), record.VersionNumber, record.VersionId, record.EffectiveOn, record.Version.EffectiveFrom, record.Version.EffectiveTo, record.Version.BaseDateRule, record.Version.ScheduleMode, record.Version.DueOffset.Days, record.Version.DueOffset.Months, record.Version.Installments.Select(item => new PaymentTermInstallmentResponse(item.Sequence, item.Percentage, item.Offset.Days, item.Offset.Months)).ToArray(), record.Version.EarlySettlementDiscount.Enabled, record.Version.EarlySettlementDiscount.Percentage, record.Version.EarlySettlementDiscount.Offset.Days, record.Version.EarlySettlementDiscount.Offset.Months, record.Snapshot.AppliedValue, record.MasterVersion);
    private static PaymentTermDueDatePreviewResponse ToResponse(MasterDataPaymentTermDueDatePreview record) => new(record.Id, record.TenantId.Value, record.Code, record.VersionNumber, record.VersionId, record.EffectiveOn, record.BaseDate, record.BaseDateRule, record.ScheduleMode, record.DueDates.Select(item => new PaymentTermDueDateResponse(item.Sequence, item.Percentage, item.DueDate)).ToArray(), record.EarlySettlementDiscountDate, record.EarlySettlementDiscountPercentage, record.Snapshot.AppliedValue);
    private static CategoryAuditResponse ToAuditResponse(MasterDataAuditRecord record) => new(record.EvidenceId, record.OccurredAt, record.OperationId, record.CorrelationId, record.TenantId.Value, record.ActorId, record.SessionId, record.AuthorizationPath.ToString(), record.Operation.ToString(), record.PolicyOutcome.ToString(), record.Decision.ToString(), record.Reason.ToString(), record.BeforeSummary, record.AfterSummary, record.ApproverId);

    private static async Task<bool> EnsureAntiforgeryAsync(HttpContext httpContext)
    {
        try { await httpContext.RequestServices.GetRequiredService<IAntiforgery>().ValidateRequestAsync(httpContext); return true; }
        catch (AntiforgeryValidationException) { return false; }
    }

    private static string GetCorrelation(HttpContext httpContext) => httpContext.Items.TryGetValue(FoundationApiKeys.CorrelationItem, out var value) && value is string correlationId ? correlationId : FoundationCorrelation.Resolve(httpContext.Request);
    private static Task<IResult> WriteProblemAsync(HttpContext httpContext, int statusCode, string code, string title, string detail, string operationId) => Task.FromResult<IResult>(Results.Problem(statusCode: statusCode, title: title, detail: detail, type: $"https://api.minierp.local/problems/{code}", extensions: new Dictionary<string, object?> { ["code"] = code, ["correlationId"] = GetCorrelation(httpContext), ["operationId"] = operationId }));
}

public enum LocalMasterDataIdempotencyDecision
{
    New = 1,
    Replay = 2,
    Conflict = 3,
    InProgress = 4
}

public sealed record LocalMasterDataIdempotencyCheck(LocalMasterDataIdempotencyDecision Decision, object? Response = null);

/// <summary>Bounded in-memory request replay seam for local REST validation.</summary>
public sealed class LocalMasterDataIdempotencyStore
{
    private sealed record Entry(string Fingerprint, DateTimeOffset ExpiresAt, object? Response);
    private readonly object syncRoot = new();
    private readonly Dictionary<(FoundationIdempotencyBinding Binding, string Key), Entry> entries = [];

    public LocalMasterDataIdempotencyCheck Begin(string key, FoundationIdempotencyBinding binding, string fingerprint, DateTimeOffset now, TimeSpan validity)
    {
        if (!FoundationCorrelation.IsValid(key)) return new(LocalMasterDataIdempotencyDecision.Conflict);
        lock (syncRoot)
        {
            foreach (var expired in entries.Where(item => item.Value.ExpiresAt <= now).Select(item => item.Key).ToArray()) entries.Remove(expired);
            var composite = (binding, key.Trim());
            if (!entries.TryGetValue(composite, out var existing))
            {
                entries[composite] = new Entry(fingerprint, now.Add(validity), null);
                return new(LocalMasterDataIdempotencyDecision.New);
            }

            if (!string.Equals(existing.Fingerprint, fingerprint, StringComparison.Ordinal)) return new(LocalMasterDataIdempotencyDecision.Conflict);
            return existing.Response is null ? new(LocalMasterDataIdempotencyDecision.InProgress) : new(LocalMasterDataIdempotencyDecision.Replay, existing.Response);
        }
    }

    public void Commit(string key, FoundationIdempotencyBinding binding, object response)
    {
        lock (syncRoot)
        {
            var composite = (binding, key.Trim());
            if (entries.TryGetValue(composite, out var existing)) entries[composite] = existing with { Response = response };
        }
    }

    public void Release(string key, FoundationIdempotencyBinding binding)
    {
        lock (syncRoot)
        {
            var composite = (binding, key.Trim());
            if (entries.TryGetValue(composite, out var existing) && existing.Response is null) entries.Remove(composite);
        }
    }
}

#pragma warning restore CS1591
