#pragma warning disable CS1591

using Microsoft.AspNetCore.Antiforgery;
using MiniErp.App.BuildingBlocks.Rest;
using MiniErp.App.Modules.Audit;
using MiniErp.App.Modules.MasterData;
using MiniErp.Contracts.Modules.Audit;
using MiniErp.Contracts.Modules.Foundation;
using MiniErp.Contracts.Modules.MasterData;

namespace MiniErp.Api;

public static class ProductIdentityEndpoints
{
    public static IEndpointRouteBuilder MapProductIdentityEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapGet(
            "/api/v1/master-data/products",
            async (
                HttpContext httpContext,
                ITrustedRequestContextResolver resolver,
                MasterDataTenantContextResolver tenantResolver,
                ProductIdentityService service) =>
                await ExecuteReadAsync(
                    httpContext,
                    resolver,
                    tenantResolver,
                    FoundationOperationCatalog.GetRequired("master-data.product.list"),
                    context => service.ListProductsAsync(context, httpContext.RequestAborted),
                    records => records.Select(ToResponse).ToArray()))
            .WithName("master-data.product.list")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("master-data.product.list")));

        endpoints.MapGet(
            "/api/v1/master-data/products/{productId:guid}",
            async (
                Guid productId,
                HttpContext httpContext,
                ITrustedRequestContextResolver resolver,
                MasterDataTenantContextResolver tenantResolver,
                ProductIdentityService service) =>
                await ExecuteReadAsync(
                    httpContext,
                    resolver,
                    tenantResolver,
                    FoundationOperationCatalog.GetRequired("master-data.product.read"),
                    context => service.GetProductAsync(context, productId, httpContext.RequestAborted),
                    ToResponse))
            .WithName("master-data.product.read")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("master-data.product.read")));

        endpoints.MapPost(
            "/api/v1/master-data/products",
            async (
                ProductIdentityWriteRequest? request,
                HttpContext httpContext,
                ITrustedRequestContextResolver resolver,
                MasterDataTenantContextResolver tenantResolver,
                FoundationAuditCoordinator auditCoordinator,
                ProductIdentityService service) =>
            {
                if (!TryCreateCommand(request, out var command))
                {
                    return await WriteProblemAsync(
                        httpContext,
                        StatusCodes.Status400BadRequest,
                        "validation_failed",
                        "Validation failed",
                        "The request is invalid.",
                        "master-data.product.create");
                }

                return await ExecuteMutationAsync(
                    httpContext,
                    resolver,
                    tenantResolver,
                    auditCoordinator,
                    FoundationOperationCatalog.GetRequired("master-data.product.create"),
                    context => service.CreateProductAsync(context, command!, httpContext.RequestAborted),
                    ToResponse,
                    setEtag: true);
            })
            .WithName("master-data.product.create")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("master-data.product.create")));

        endpoints.MapPost(
            "/api/v1/master-data/products/{productId:guid}/edit",
            async (
                Guid productId,
                ProductIdentityWriteRequest? request,
                HttpContext httpContext,
                ITrustedRequestContextResolver resolver,
                MasterDataTenantContextResolver tenantResolver,
                FoundationAuditCoordinator auditCoordinator,
                ProductIdentityService service) =>
            {
                if (!TryReadExpectedVersion(httpContext, out var expectedVersion)
                    || !TryCreateEditCommand(productId, request, expectedVersion, out var command))
                {
                    return await WriteProblemAsync(
                        httpContext,
                        StatusCodes.Status400BadRequest,
                        "validation_failed",
                        "Validation failed",
                        "A valid If-Match version and request are required.",
                        "master-data.product.edit");
                }

                return await ExecuteMutationAsync(
                    httpContext,
                    resolver,
                    tenantResolver,
                    auditCoordinator,
                    FoundationOperationCatalog.GetRequired("master-data.product.edit"),
                    context => service.EditProductAsync(context, command!, httpContext.RequestAborted),
                    ToResponse,
                    setEtag: true);
            })
            .WithName("master-data.product.edit")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("master-data.product.edit")));

        endpoints.MapPost(
            "/api/v1/master-data/products/{productId:guid}/deactivate",
            async (
                Guid productId,
                ProductLifecycleRequest? request,
                HttpContext httpContext,
                ITrustedRequestContextResolver resolver,
                MasterDataTenantContextResolver tenantResolver,
                FoundationAuditCoordinator auditCoordinator,
                ProductIdentityService service) =>
            {
                if (!TryReadExpectedVersion(httpContext, out var expectedVersion))
                {
                    return await WriteProblemAsync(
                        httpContext,
                        StatusCodes.Status400BadRequest,
                        "validation_failed",
                        "Validation failed",
                        "A valid If-Match version is required.",
                        "master-data.product.deactivate");
                }

                return await ExecuteMutationAsync(
                    httpContext,
                    resolver,
                    tenantResolver,
                    auditCoordinator,
                    FoundationOperationCatalog.GetRequired("master-data.product.deactivate"),
                    context => service.DeactivateProductAsync(
                        context,
                        productId,
                        expectedVersion,
                        request?.Reason,
                        httpContext.RequestAborted),
                    ToResponse,
                    setEtag: true);
            })
            .WithName("master-data.product.deactivate")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("master-data.product.deactivate")));

        endpoints.MapPost(
            "/api/v1/master-data/products/{productId:guid}/reactivate",
            async (
                Guid productId,
                HttpContext httpContext,
                ITrustedRequestContextResolver resolver,
                MasterDataTenantContextResolver tenantResolver,
                FoundationAuditCoordinator auditCoordinator,
                ProductIdentityService service) =>
                await ExecuteMutationAsync(
                    httpContext,
                    resolver,
                    tenantResolver,
                    auditCoordinator,
                    FoundationOperationCatalog.GetRequired("master-data.product.reactivate"),
                    context => service.ReactivateProductAsync(
                        context,
                        productId,
                        ReadExpectedVersionOrEmpty(httpContext),
                        httpContext.RequestAborted),
                    ToResponse,
                    setEtag: true,
                    requireExpectedVersion: true))
            .WithName("master-data.product.reactivate")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("master-data.product.reactivate")));

        endpoints.MapGet(
            "/api/v1/master-data/products/{productId:guid}/audit",
            async (
                Guid productId,
                HttpContext httpContext,
                ITrustedRequestContextResolver resolver,
                MasterDataTenantContextResolver tenantResolver,
                ProductIdentityService service) =>
                await ExecuteReadAsync(
                    httpContext,
                    resolver,
                    tenantResolver,
                    FoundationOperationCatalog.GetRequired("master-data.product.audit.read"),
                    context => service.ReadAuditHistoryAsync(context, productId, httpContext.RequestAborted),
                    records => records.Select(ToResponse).ToArray()))
            .WithName("master-data.product.audit.read")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("master-data.product.audit.read")));

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
        var context = await resolver.ResolveAsync(httpContext, httpContext.RequestAborted);
        var resolution = tenantResolver.Resolve(context);
        if (!resolution.Allowed || resolution.Context is null)
        {
            return await WriteProblemAsync(
                httpContext,
                context.SecurityProfile == FoundationSecurityProfile.Anonymous
                    ? StatusCodes.Status401Unauthorized
                    : StatusCodes.Status403Forbidden,
                resolution.Code,
                context.SecurityProfile == FoundationSecurityProfile.Anonymous
                    ? "Authentication required"
                    : "Access denied",
                "The operation is not available for this security context.",
                descriptor.OperationId);
        }

        var result = await operation(resolution.Context);
        return ToResult(httpContext, result, descriptor.OperationId, map, StatusCodes.Status200OK, setEtag: false);
    }

    private static async Task<IResult> ExecuteMutationAsync<T>(
        HttpContext httpContext,
        ITrustedRequestContextResolver resolver,
        MasterDataTenantContextResolver tenantResolver,
        FoundationAuditCoordinator auditCoordinator,
        FoundationOperationDescriptor descriptor,
        Func<MasterDataRequestContext, Task<MasterDataOperationResult<T>>> operation,
        Func<T, object?> map,
        bool setEtag,
        bool requireExpectedVersion = false)
    {
        if (!await EnsureAntiforgeryAsync(httpContext))
        {
            return await WriteProblemAsync(
                httpContext,
                StatusCodes.Status403Forbidden,
                "antiforgery_failed",
                "Antiforgery validation failed",
                "The request could not be validated.",
                descriptor.OperationId);
        }

        if (requireExpectedVersion && !TryReadExpectedVersion(httpContext, out _))
        {
            return await WriteProblemAsync(
                httpContext,
                StatusCodes.Status400BadRequest,
                "validation_failed",
                "Validation failed",
                "A valid If-Match version is required.",
                descriptor.OperationId);
        }

        var context = await resolver.ResolveAsync(httpContext, httpContext.RequestAborted);
        var resolution = tenantResolver.Resolve(context);
        if (!resolution.Allowed || resolution.Context is null)
        {
            return await WriteProblemAsync(
                httpContext,
                context.SecurityProfile == FoundationSecurityProfile.Anonymous
                    ? StatusCodes.Status401Unauthorized
                    : StatusCodes.Status403Forbidden,
                resolution.Code,
                context.SecurityProfile == FoundationSecurityProfile.Anonymous
                    ? "Authentication required"
                    : "Access denied",
                "The operation is not available for this security context.",
                descriptor.OperationId);
        }

        var execution = await auditCoordinator.ExecuteProtectedAsync(
            context,
            descriptor.OperationId,
            GetCorrelation(httpContext),
            FoundationAuditReason.Allowed,
            () => operation(resolution.Context),
            cancellationToken: httpContext.RequestAborted);
        if (!execution.Succeeded || execution.Value is null)
        {
            return await WriteProblemAsync(
                httpContext,
                StatusCodes.Status503ServiceUnavailable,
                execution.Code,
                "Operation unavailable",
                "The operation could not be completed.",
                descriptor.OperationId);
        }

        return ToResult(
            httpContext,
            execution.Value,
            descriptor.OperationId,
            map,
            StatusCodes.Status200OK,
            setEtag);
    }

    private static IResult ToResult<T>(
        HttpContext httpContext,
        MasterDataOperationResult<T> result,
        string operationId,
        Func<T, object?> map,
        int successStatus,
        bool setEtag)
    {
        if (result.Succeeded && result.Value is not null)
        {
            if (setEtag && result.Value is ProductIdentityRecord product)
            {
                httpContext.Response.Headers.ETag = $"\"{Convert.ToBase64String(product.Version)}\"";
            }

            return Results.Json(map(result.Value), statusCode: successStatus);
        }

        var code = result.Code;
        var status = code switch
        {
            "permission_denied"
                or "resource_scope_denied"
                or "cross_tenant_target_denied"
                or "tenant_context_failed"
                or "authorization_denied"
                or "approval_required"
                or "approval_pending"
                or "approval_rejected"
                or "approval_policy_not_configured"
                or "approval_identity_missing"
                or "approval_policy_invalid"
                or "resource_policy_not_configured"
                or "self_approval_denied" => StatusCodes.Status403Forbidden,
            "permission_unavailable"
                or "scope_policy_unavailable"
                or "approval_policy_unavailable"
                or "resource_policy_unavailable"
                or "authorization_operation_unmapped" => StatusCodes.Status503ServiceUnavailable,
            "product_not_found" => StatusCodes.Status404NotFound,
            "concurrency_conflict"
                or "product_duplicate"
                or "product_barcode_duplicate"
                or "product_lifecycle_no_change" => StatusCodes.Status409Conflict,
            "persistence_unavailable"
                or "reference_persistence_unavailable"
                or "audit_unavailable"
                or "audit_evidence_invalid"
                or "audit_evidence_unavailable"
                or "audit_context_mismatch"
                or "base_uom_integrity_unavailable" => StatusCodes.Status503ServiceUnavailable,
            _ => StatusCodes.Status400BadRequest
        };
        return Results.Problem(
            statusCode: status,
            title: status == StatusCodes.Status403Forbidden ? "Access denied" : "Product operation failed",
            detail: "The Product operation could not be completed.",
            type: $"https://api.minierp.local/problems/{code}",
            extensions: new Dictionary<string, object?>
            {
                ["code"] = code,
                ["correlationId"] = GetCorrelation(httpContext),
                ["operationId"] = operationId
            });
    }

    private static bool TryCreateCommand(
        ProductIdentityWriteRequest? request,
        out CreateProductIdentityCommand? command)
    {
        command = null;
        if (request is null)
        {
            return false;
        }

        try
        {
            command = new CreateProductIdentityCommand(
                request.Sku ?? string.Empty,
                new LocalizedName(request.EnglishName, request.ArabicName),
                request.Description,
                request.CategoryId,
                request.BaseUnitOfMeasureId,
                request.Barcodes,
                request.TrackingEnabledOverride,
                request.IsSellable,
                request.IsPurchasable,
                request.IsInventoryRelevant);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static bool TryCreateEditCommand(
        Guid productId,
        ProductIdentityWriteRequest? request,
        byte[] expectedVersion,
        out EditProductIdentityCommand? command)
    {
        command = null;
        if (request is null)
        {
            return false;
        }

        try
        {
            command = new EditProductIdentityCommand(
                productId,
                request.Sku ?? string.Empty,
                new LocalizedName(request.EnglishName, request.ArabicName),
                request.Description,
                request.CategoryId,
                request.BaseUnitOfMeasureId,
                request.Barcodes,
                request.TrackingEnabledOverride,
                request.IsSellable,
                request.IsPurchasable,
                request.IsInventoryRelevant,
                expectedVersion);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

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

    private static byte[] ReadExpectedVersionOrEmpty(HttpContext httpContext) =>
        TryReadExpectedVersion(httpContext, out var version) ? version : [];

    private static ProductIdentityResponse ToResponse(ProductIdentityRecord record) => new(
        record.Id,
        record.TenantId.Value,
        record.Sku,
        record.Name.English,
        record.Name.Arabic,
        record.Description,
        record.CategoryId,
        record.BaseUnitOfMeasureId,
        record.TrackingDefaultEnabled,
        record.TrackingEnabledOverride,
        record.TrackingEnabled,
        record.IsSellable,
        record.IsPurchasable,
        record.IsInventoryRelevant,
        record.LifecycleState.ToString(),
        record.Version,
        record.Barcodes.Select(barcode => new ProductBarcodeResponse(
            barcode.Id,
            barcode.ProductId,
            barcode.Value,
            barcode.Version)).ToArray());

    private static ProductAuditResponse ToResponse(MasterDataAuditRecord record) => new(
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
            await httpContext.RequestServices.GetRequiredService<IAntiforgery>()
                .ValidateRequestAsync(httpContext);
            return true;
        }
        catch (AntiforgeryValidationException)
        {
            return false;
        }
    }

    private static string GetCorrelation(HttpContext httpContext) =>
        httpContext.Items.TryGetValue("MiniErp.Foundation.CorrelationId", out var value)
            && value is string correlationId
            ? correlationId
            : FoundationCorrelation.Resolve(httpContext.Request);

    private static Task<IResult> WriteProblemAsync(
        HttpContext httpContext,
        int statusCode,
        string code,
        string title,
        string detail,
        string operationId) => Task.FromResult<IResult>(Results.Problem(
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
