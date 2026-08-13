#pragma warning disable CS1591

using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Mvc;
using MiniErp.App.BuildingBlocks.Rest;
using MiniErp.App.Modules.Audit;
using MiniErp.App.Modules.MasterData;
using MiniErp.Contracts.Modules.Audit;
using MiniErp.Contracts.Modules.Foundation;
using MiniErp.Contracts.Modules.MasterData;

namespace MiniErp.Api;

/// <summary>REST composition for MESP-121 Phase A Price Lists and pricing reference.</summary>
public static class PriceListEndpoints
{
    public static IEndpointRouteBuilder MapPriceListEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapGet(
            "/api/v1/master-data/price-lists",
            async ([FromQuery(Name = "q")] string? search, HttpContext httpContext, ITrustedRequestContextResolver resolver, MasterDataTenantContextResolver tenantResolver, MasterDataPriceListService service) =>
                await ExecuteReadAsync(httpContext, resolver, tenantResolver, FoundationOperationCatalog.GetRequired("master-data.price-list.list"), context => service.ListPriceListsAsync(context, search, httpContext.RequestAborted), records => records.Select(ToResponse).ToArray()))
            .WithName("master-data.price-list.list")
            .WithTags("Master Data / Price Lists")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("master-data.price-list.list")));

        endpoints.MapGet(
            "/api/v1/master-data/price-lists/{priceListId:guid}",
            async (Guid priceListId, HttpContext httpContext, ITrustedRequestContextResolver resolver, MasterDataTenantContextResolver tenantResolver, MasterDataPriceListService service) =>
                await ExecuteReadAsync(httpContext, resolver, tenantResolver, FoundationOperationCatalog.GetRequired("master-data.price-list.read"), context => service.GetPriceListAsync(context, priceListId, httpContext.RequestAborted), ToResponse))
            .WithName("master-data.price-list.read")
            .WithTags("Master Data / Price Lists")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("master-data.price-list.read")));

        endpoints.MapGet(
            "/api/v1/master-data/price-lists/{priceListId:guid}/history",
            async (Guid priceListId, HttpContext httpContext, ITrustedRequestContextResolver resolver, MasterDataTenantContextResolver tenantResolver, MasterDataPriceListService service) =>
                await ExecuteReadAsync(httpContext, resolver, tenantResolver, FoundationOperationCatalog.GetRequired("master-data.price-list.history.read"), context => service.GetPriceListHistoryAsync(context, priceListId, httpContext.RequestAborted), record => record.Prices.Select(ToResponse).ToArray()))
            .WithName("master-data.price-list.history.read")
            .WithTags("Master Data / Price Lists")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("master-data.price-list.history.read")));

        endpoints.MapGet(
            "/api/v1/master-data/price-lists/reference",
            async (
                [FromQuery] Guid? priceListId,
                [FromQuery] Guid? productId,
                [FromQuery] Guid? unitOfMeasureId,
                [FromQuery] Guid? currencyId,
                [FromQuery] Guid? customerId,
                [FromQuery] OrganizationScopeKind? organizationScopeKind,
                [FromQuery] Guid? organizationScopeId,
                [FromQuery(Name = "effectiveOn")] string? effectiveOn,
                HttpContext httpContext,
                ITrustedRequestContextResolver resolver,
                MasterDataTenantContextResolver tenantResolver,
                MasterDataPriceListService service) =>
            {
                if (!TryReadDate(effectiveOn, out var parsedEffectiveOn)
                    || productId is not { } parsedProductId
                    || unitOfMeasureId is not { } parsedUnitOfMeasureId
                    || currencyId is not { } parsedCurrencyId)
                {
                    return await WriteProblemAsync(httpContext, 400, "validation_failed", "Validation failed", "Product, UOM, Currency, and an ISO effectiveOn date are required.", "master-data.price-list.reference.read");
                }

                var query = new ResolveMasterDataPriceQuery(
                    priceListId,
                    parsedProductId,
                    parsedUnitOfMeasureId,
                    parsedCurrencyId,
                    customerId,
                    organizationScopeKind,
                    organizationScopeId,
                    parsedEffectiveOn);
                return await ExecuteReadAsync(httpContext, resolver, tenantResolver, FoundationOperationCatalog.GetRequired("master-data.price-list.reference.read"), context => service.ResolvePriceAsync(context, query, httpContext.RequestAborted), ToReferenceResponse);
            })
            .WithName("master-data.price-list.reference.read")
            .WithTags("Master Data / Price Lists")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("master-data.price-list.reference.read")));

        endpoints.MapPost(
            "/api/v1/master-data/price-lists",
            async (PriceListWriteRequest? request, HttpContext httpContext, ITrustedRequestContextResolver resolver, MasterDataTenantContextResolver tenantResolver, FoundationAuditCoordinator auditCoordinator, LocalMasterDataIdempotencyStore idempotencyStore, MasterDataPriceListService service) =>
            {
                if (!TryCreateCommand(request, out var command))
                {
                    return await WriteProblemAsync(httpContext, 400, "validation_failed", "Validation failed", "The Price List request is invalid.", "master-data.price-list.create");
                }

                return await ExecuteMutationAsync(httpContext, resolver, tenantResolver, auditCoordinator, idempotencyStore, FoundationOperationCatalog.GetRequired("master-data.price-list.create"), Fingerprint(request), context => service.CreatePriceListAsync(context, command!, httpContext.RequestAborted), ToResponse, setEtag: true);
            })
            .WithName("master-data.price-list.create")
            .WithTags("Master Data / Price Lists")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("master-data.price-list.create")));

        endpoints.MapPost(
            "/api/v1/master-data/price-lists/{priceListId:guid}/edit",
            async (Guid priceListId, PriceListWriteRequest? request, HttpContext httpContext, ITrustedRequestContextResolver resolver, MasterDataTenantContextResolver tenantResolver, FoundationAuditCoordinator auditCoordinator, LocalMasterDataIdempotencyStore idempotencyStore, MasterDataPriceListService service) =>
            {
                if (!TryReadExpectedVersion(httpContext, out var expectedVersion) || !TryCreateEditCommand(priceListId, request, expectedVersion, out var command))
                {
                    return await WriteProblemAsync(httpContext, 400, "validation_failed", "Validation failed", "A valid If-Match version and Price List request are required.", "master-data.price-list.edit");
                }

                return await ExecuteMutationAsync(httpContext, resolver, tenantResolver, auditCoordinator, idempotencyStore, FoundationOperationCatalog.GetRequired("master-data.price-list.edit"), Fingerprint(request) + VersionFingerprint(expectedVersion), context => service.EditPriceListAsync(context, command!, httpContext.RequestAborted), ToResponse, setEtag: true, requireExpectedVersion: true);
            })
            .WithName("master-data.price-list.edit")
            .WithTags("Master Data / Price Lists")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("master-data.price-list.edit")));

        endpoints.MapPost(
            "/api/v1/master-data/price-lists/{priceListId:guid}/prices",
            async (Guid priceListId, PriceListPriceWriteRequest? request, HttpContext httpContext, ITrustedRequestContextResolver resolver, MasterDataTenantContextResolver tenantResolver, FoundationAuditCoordinator auditCoordinator, LocalMasterDataIdempotencyStore idempotencyStore, MasterDataPriceListService service) =>
            {
                if (!TryReadExpectedVersion(httpContext, out var expectedVersion) || !TryCreatePriceCommand(priceListId, request, expectedVersion, out var command))
                {
                    return await WriteProblemAsync(httpContext, 400, "validation_failed", "Validation failed", "A valid If-Match version and Price List price request are required.", "master-data.price-list.price.append");
                }

                return await ExecuteMutationAsync(httpContext, resolver, tenantResolver, auditCoordinator, idempotencyStore, FoundationOperationCatalog.GetRequired("master-data.price-list.price.append"), Fingerprint(request) + VersionFingerprint(expectedVersion), context => service.AppendPriceAsync(context, command!, httpContext.RequestAborted), ToResponse, setEtag: true, requireExpectedVersion: true);
            })
            .WithName("master-data.price-list.price.append")
            .WithTags("Master Data / Price Lists")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("master-data.price-list.price.append")));

        endpoints.MapPost(
            "/api/v1/master-data/price-lists/{priceListId:guid}/deactivate",
            async (Guid priceListId, HttpContext httpContext, ITrustedRequestContextResolver resolver, MasterDataTenantContextResolver tenantResolver, FoundationAuditCoordinator auditCoordinator, LocalMasterDataIdempotencyStore idempotencyStore, MasterDataPriceListService service) =>
            {
                if (!TryReadExpectedVersion(httpContext, out var expectedVersion))
                {
                    return await WriteProblemAsync(httpContext, 400, "validation_failed", "Validation failed", "A valid If-Match version is required.", "master-data.price-list.deactivate");
                }

                return await ExecuteMutationAsync(httpContext, resolver, tenantResolver, auditCoordinator, idempotencyStore, FoundationOperationCatalog.GetRequired("master-data.price-list.deactivate"), VersionFingerprint(expectedVersion), context => service.DeactivatePriceListAsync(context, priceListId, expectedVersion, httpContext.RequestAborted), ToResponse, setEtag: true, requireExpectedVersion: true);
            })
            .WithName("master-data.price-list.deactivate")
            .WithTags("Master Data / Price Lists")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("master-data.price-list.deactivate")));

        endpoints.MapPost(
            "/api/v1/master-data/price-lists/{priceListId:guid}/reactivate",
            async (Guid priceListId, HttpContext httpContext, ITrustedRequestContextResolver resolver, MasterDataTenantContextResolver tenantResolver, FoundationAuditCoordinator auditCoordinator, LocalMasterDataIdempotencyStore idempotencyStore, MasterDataPriceListService service) =>
            {
                if (!TryReadExpectedVersion(httpContext, out var expectedVersion))
                {
                    return await WriteProblemAsync(httpContext, 400, "validation_failed", "Validation failed", "A valid If-Match version is required.", "master-data.price-list.reactivate");
                }

                return await ExecuteMutationAsync(httpContext, resolver, tenantResolver, auditCoordinator, idempotencyStore, FoundationOperationCatalog.GetRequired("master-data.price-list.reactivate"), VersionFingerprint(expectedVersion), context => service.ReactivatePriceListAsync(context, priceListId, expectedVersion, httpContext.RequestAborted), ToResponse, setEtag: true, requireExpectedVersion: true);
            })
            .WithName("master-data.price-list.reactivate")
            .WithTags("Master Data / Price Lists")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("master-data.price-list.reactivate")));

        endpoints.MapGet(
            "/api/v1/master-data/price-lists/{priceListId:guid}/audit",
            async (Guid priceListId, HttpContext httpContext, ITrustedRequestContextResolver resolver, MasterDataTenantContextResolver tenantResolver, MasterDataPriceListService service) =>
                await ExecuteReadAsync(httpContext, resolver, tenantResolver, FoundationOperationCatalog.GetRequired("master-data.price-list.audit.read"), context => service.ReadAuditHistoryAsync(context, priceListId, httpContext.RequestAborted), records => records.Select(ToAuditResponse).ToArray()))
            .WithName("master-data.price-list.audit.read")
            .WithTags("Master Data / Price Lists")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("master-data.price-list.audit.read")));

        return endpoints;
    }

    private static async Task<IResult> ExecuteReadAsync<T>(
        HttpContext httpContext,
        ITrustedRequestContextResolver resolver,
        MasterDataTenantContextResolver tenantResolver,
        FoundationOperationDescriptor descriptor,
        Func<MasterDataRequestContext, Task<MasterDataOperationResult<T>>> operation,
        Func<T, object?> map)
    {
        var foundationContext = await resolver.ResolveAsync(httpContext, httpContext.RequestAborted);
        var resolution = tenantResolver.Resolve(foundationContext);
        if (!resolution.Allowed || resolution.Context is null)
        {
            return await WriteProblemAsync(httpContext, foundationContext.SecurityProfile == FoundationSecurityProfile.Anonymous ? 401 : 403, resolution.Code, foundationContext.SecurityProfile == FoundationSecurityProfile.Anonymous ? "Authentication required" : "Access denied", "The operation is not available for this security context.", descriptor.OperationId);
        }

        return ToResult(httpContext, await operation(resolution.Context), descriptor.OperationId, map, setEtag: false);
    }

    private static async Task<IResult> ExecuteMutationAsync<T>(
        HttpContext httpContext,
        ITrustedRequestContextResolver resolver,
        MasterDataTenantContextResolver tenantResolver,
        FoundationAuditCoordinator auditCoordinator,
        LocalMasterDataIdempotencyStore idempotencyStore,
        FoundationOperationDescriptor descriptor,
        string fingerprint,
        Func<MasterDataRequestContext, Task<MasterDataOperationResult<T>>> operation,
        Func<T, object?> map,
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
            return ToResult(httpContext, MasterDataOperationResult<T>.Success(replay), descriptor.OperationId, map, setEtag);
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
            if (setEtag && result.Value is MasterDataPriceListRecord priceList)
            {
                httpContext.Response.Headers.ETag = $"\"{Convert.ToBase64String(priceList.Version)}\"";
            }

            return Results.Json(map(result.Value));
        }

        var code = result.Code;
        var status = code switch
        {
            "permission_denied" or "resource_scope_denied" or "organization_scope_denied" or "cross_tenant_target_denied" or "tenant_context_failed" or "authorization_denied" or "approval_required" or "approval_policy_not_configured" or "resource_policy_not_configured" => 403,
            "permission_unavailable" or "scope_policy_unavailable" or "approval_policy_unavailable" or "resource_policy_unavailable" or "authorization_operation_unmapped" or "persistence_unavailable" or "audit_unavailable" or "audit_context_mismatch" or "customer_reference_unavailable" => 503,
            "price_list_not_found" or "price_list_price_not_found" or "price_list_product_not_found" or "price_list_uom_not_found" or "price_list_currency_not_found" or "price_list_customer_not_found" => 404,
            "concurrency_conflict" or "price_list_effective_overlap" or "price_list_precedence_conflict" or "price_list_duplicate" or "price_list_lifecycle_no_change" or "price_list_inactive" or "price_list_customer_inactive" or "idempotency_conflict" => 409,
            _ => 400
        };

        return Results.Problem(statusCode: status, title: status == 403 ? "Access denied" : "Master Data operation failed", detail: "The Price List operation could not be completed.", type: $"https://api.minierp.local/problems/{code}", extensions: new Dictionary<string, object?>
        {
            ["code"] = code,
            ["correlationId"] = GetCorrelation(httpContext),
            ["operationId"] = operationId
        });
    }

    private static bool TryCreateCommand(PriceListWriteRequest? request, out CreateMasterDataPriceListCommand? command)
    {
        command = null;
        if (request is null)
        {
            return false;
        }

        try
        {
            command = new CreateMasterDataPriceListCommand(
                request.Code,
                new LocalizedName(request.EnglishName, request.ArabicName),
                request.CurrencyId,
                request.CustomerId,
                request.OrganizationScopeKind,
                request.OrganizationScopeId,
                request.Priority);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static bool TryCreateEditCommand(Guid id, PriceListWriteRequest? request, byte[] expectedVersion, out EditMasterDataPriceListCommand? command)
    {
        command = null;
        if (request is null)
        {
            return false;
        }

        try
        {
            command = new EditMasterDataPriceListCommand(
                id,
                request.Code,
                new LocalizedName(request.EnglishName, request.ArabicName),
                request.CurrencyId,
                request.CustomerId,
                request.OrganizationScopeKind,
                request.OrganizationScopeId,
                request.Priority,
                expectedVersion);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static bool TryCreatePriceCommand(Guid id, PriceListPriceWriteRequest? request, byte[] expectedVersion, out AppendMasterDataPriceCommand? command)
    {
        command = null;
        if (request is null)
        {
            return false;
        }

        command = new AppendMasterDataPriceCommand(id, request.ProductId, request.UnitOfMeasureId, request.EffectiveFrom, request.EffectiveTo, request.Price, request.PriceScale, request.Provenance, request.SourceReference, expectedVersion);
        return true;
    }

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

    private static bool TryReadDate(string? value, out DateOnly date) =>
        DateOnly.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out date);

    private static string Fingerprint(object? request) => JsonSerializer.Serialize(request);

    private static string VersionFingerprint(byte[] version) => $"|version:{Convert.ToBase64String(version)}";

    private static PriceListResponse ToResponse(MasterDataPriceListRecord record) => new(
        record.Id,
        record.TenantId.Value,
        record.Code,
        record.Name.English ?? string.Empty,
        record.Name.Arabic,
        record.CurrencyId,
        record.CurrencyCode,
        record.CustomerId,
        record.OrganizationScopeKind,
        record.OrganizationScopeId,
        record.Priority,
        record.LifecycleState.ToString(),
        record.CurrentVersionNumber,
        record.Prices.Select(ToResponse).ToArray(),
        record.Version);

    private static PriceListPriceVersionResponse ToResponse(MasterDataPriceListPriceRecord record) => new(
        record.Id,
        record.VersionNumber,
        record.ProductId,
        record.ProductSku,
        record.UnitOfMeasureId,
        record.UnitOfMeasureCode,
        record.CurrencyId,
        record.CurrencyCode,
        record.CustomerId,
        record.OrganizationScopeKind,
        record.OrganizationScopeId,
        record.Priority,
        record.EffectiveFrom,
        record.EffectiveTo,
        record.Price,
        record.PriceScale,
        record.Provenance,
        record.SourceReference,
        record.Version);

    private static PriceListReferenceResponse ToReferenceResponse(MasterDataPriceListReferenceRecord record) => new(
        record.PriceListId,
        record.TenantId.Value,
        record.PriceListCode,
        record.Price.Id,
        record.Price.VersionNumber,
        record.Price.ProductId,
        record.Price.ProductSku,
        record.Price.UnitOfMeasureId,
        record.Price.UnitOfMeasureCode,
        record.CurrentConfiguration.CurrencyId,
        record.CurrentConfiguration.CurrencyCode,
        record.CurrentConfiguration.CustomerId,
        record.CurrentConfiguration.OrganizationScopeKind,
        record.CurrentConfiguration.OrganizationScopeId,
        record.CurrentConfiguration.Priority,
        record.EffectiveOn,
        record.Price.EffectiveFrom,
        record.Price.EffectiveTo,
        record.Price.Price,
        record.Price.PriceScale,
        record.Price.Provenance,
        record.Price.SourceReference,
        record.Snapshot.AppliedValue,
        record.Price.Version,
        record.MasterVersion);

    private static CategoryAuditResponse ToAuditResponse(MasterDataAuditRecord record) => new(
        record.EvidenceId,
        record.OccurredAt,
        record.OperationId,
        record.CorrelationId,
        record.TenantId.Value,
        record.ActorId,
        record.SessionId,
        record.AuthorizationPath.ToString(),
        record.Operation.ToString(),
        record.PolicyOutcome.ToString(),
        record.Decision.ToString(),
        record.Reason.ToString(),
        record.BeforeSummary,
        record.AfterSummary,
        record.ApproverId);

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
        httpContext.Items.TryGetValue(FoundationApiKeys.CorrelationItem, out var value) && value is string correlationId
            ? correlationId
            : FoundationCorrelation.Resolve(httpContext.Request);

    private static Task<IResult> WriteProblemAsync(HttpContext httpContext, int statusCode, string code, string title, string detail, string operationId) =>
        Task.FromResult<IResult>(Results.Problem(statusCode: statusCode, title: title, detail: detail, type: $"https://api.minierp.local/problems/{code}", extensions: new Dictionary<string, object?>
        {
            ["code"] = code,
            ["correlationId"] = GetCorrelation(httpContext),
            ["operationId"] = operationId
        }));
}

#pragma warning restore CS1591
