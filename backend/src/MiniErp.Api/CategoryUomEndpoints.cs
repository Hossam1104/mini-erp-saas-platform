#pragma warning disable CS1591

using Microsoft.AspNetCore.Antiforgery;
using MiniErp.App.BuildingBlocks.Rest;
using MiniErp.App.Modules.Audit;
using MiniErp.App.Modules.MasterData;
using MiniErp.Contracts.Modules.Audit;
using MiniErp.Contracts.Modules.Foundation;
using MiniErp.Contracts.Modules.MasterData;

namespace MiniErp.Api;

public static class CategoryUomEndpoints
{
    public static IEndpointRouteBuilder MapCategoryUomEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapGet(
            "/api/v1/master-data/categories",
            async (
                HttpContext httpContext,
                ITrustedRequestContextResolver resolver,
                MasterDataTenantContextResolver tenantResolver,
                MasterDataCategoryUomService service) =>
                await ExecuteReadAsync(
                    httpContext,
                    resolver,
                    tenantResolver,
                    FoundationOperationCatalog.GetRequired("master-data.category.list"),
                    context => service.ListCategoriesAsync(context, httpContext.RequestAborted),
                    records => records.Select(ToResponse).ToArray()))
            .WithName("master-data.category.list")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("master-data.category.list")));

        endpoints.MapGet(
            "/api/v1/master-data/categories/{categoryId:guid}",
            async (
                Guid categoryId,
                HttpContext httpContext,
                ITrustedRequestContextResolver resolver,
                MasterDataTenantContextResolver tenantResolver,
                MasterDataCategoryUomService service) =>
                await ExecuteReadAsync(
                    httpContext,
                    resolver,
                    tenantResolver,
                    FoundationOperationCatalog.GetRequired("master-data.category.read"),
                    context => service.GetCategoryAsync(context, categoryId, httpContext.RequestAborted),
                    ToResponse))
            .WithName("master-data.category.read")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("master-data.category.read")));

        endpoints.MapPost(
            "/api/v1/master-data/categories",
            async (
                CategoryWriteRequest? request,
                HttpContext httpContext,
                ITrustedRequestContextResolver resolver,
                MasterDataTenantContextResolver tenantResolver,
                FoundationAuditCoordinator auditCoordinator,
                MasterDataCategoryUomService service) =>
            {
                if (!TryCreateCategoryCommand(request, out var command))
                {
                    return await WriteProblemAsync(
                        httpContext,
                        StatusCodes.Status400BadRequest,
                        "validation_failed",
                        "Validation failed",
                        "The request is invalid.",
                        "master-data.category.create");
                }

                return await ExecuteMutationAsync(
                    httpContext,
                    resolver,
                    tenantResolver,
                    auditCoordinator,
                    FoundationOperationCatalog.GetRequired("master-data.category.create"),
                    context => service.CreateCategoryAsync(context, command!, httpContext.RequestAborted),
                    ToResponse,
                    setEtag: true);
            })
            .WithName("master-data.category.create")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("master-data.category.create")));

        endpoints.MapPost(
            "/api/v1/master-data/categories/{categoryId:guid}/edit",
            async (
                Guid categoryId,
                CategoryWriteRequest? request,
                HttpContext httpContext,
                ITrustedRequestContextResolver resolver,
                MasterDataTenantContextResolver tenantResolver,
                FoundationAuditCoordinator auditCoordinator,
                MasterDataCategoryUomService service) =>
            {
                if (!TryReadExpectedVersion(httpContext, out var expectedVersion)
                    || !TryCreateCategoryEditCommand(categoryId, request, expectedVersion, out var command))
                {
                    return await WriteProblemAsync(
                        httpContext,
                        StatusCodes.Status400BadRequest,
                        "validation_failed",
                        "Validation failed",
                        "A valid If-Match version and request are required.",
                        "master-data.category.edit");
                }

                return await ExecuteMutationAsync(
                    httpContext,
                    resolver,
                    tenantResolver,
                    auditCoordinator,
                    FoundationOperationCatalog.GetRequired("master-data.category.edit"),
                    context => service.EditCategoryAsync(context, command!, httpContext.RequestAborted),
                    ToResponse,
                    setEtag: true);
            })
            .WithName("master-data.category.edit")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("master-data.category.edit")));

        endpoints.MapPost(
            "/api/v1/master-data/categories/{categoryId:guid}/deactivate",
            async (
                Guid categoryId,
                MasterDataLifecycleRequest? _,
                HttpContext httpContext,
                ITrustedRequestContextResolver resolver,
                MasterDataTenantContextResolver tenantResolver,
                FoundationAuditCoordinator auditCoordinator,
                MasterDataCategoryUomService service) =>
            {
                if (!TryReadExpectedVersion(httpContext, out var expectedVersion))
                {
                    return await WriteProblemAsync(
                        httpContext,
                        StatusCodes.Status400BadRequest,
                        "validation_failed",
                        "Validation failed",
                        "A valid If-Match version is required.",
                        "master-data.category.deactivate");
                }

                return await ExecuteMutationAsync(
                    httpContext,
                    resolver,
                    tenantResolver,
                    auditCoordinator,
                    FoundationOperationCatalog.GetRequired("master-data.category.deactivate"),
                    context => service.DeactivateCategoryAsync(context, categoryId, expectedVersion, httpContext.RequestAborted),
                    ToResponse,
                    setEtag: true);
            })
            .WithName("master-data.category.deactivate")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("master-data.category.deactivate")));

        endpoints.MapPost(
            "/api/v1/master-data/categories/{categoryId:guid}/reactivate",
            async (
                Guid categoryId,
                HttpContext httpContext,
                ITrustedRequestContextResolver resolver,
                MasterDataTenantContextResolver tenantResolver,
                FoundationAuditCoordinator auditCoordinator,
                MasterDataCategoryUomService service) =>
                await ExecuteMutationAsync(
                    httpContext,
                    resolver,
                    tenantResolver,
                    auditCoordinator,
                    FoundationOperationCatalog.GetRequired("master-data.category.reactivate"),
                    context => service.ReactivateCategoryAsync(
                        context,
                        categoryId,
                        ReadExpectedVersionOrEmpty(httpContext),
                        httpContext.RequestAborted),
                    ToResponse,
                    setEtag: true,
                    requireExpectedVersion: true))
            .WithName("master-data.category.reactivate")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("master-data.category.reactivate")));

        endpoints.MapGet(
            "/api/v1/master-data/categories/{categoryId:guid}/audit",
            async (
                Guid categoryId,
                HttpContext httpContext,
                ITrustedRequestContextResolver resolver,
                MasterDataTenantContextResolver tenantResolver,
                MasterDataCategoryUomService service) =>
                await ExecuteReadAsync(
                    httpContext,
                    resolver,
                    tenantResolver,
                    FoundationOperationCatalog.GetRequired("master-data.category.audit.read"),
                    context => service.ReadAuditHistoryAsync(
                        context,
                        MasterDataResourceKind.ProductCategory,
                        categoryId,
                        httpContext.RequestAborted),
                    records => records.Select(ToAuditResponse).ToArray()))
            .WithName("master-data.category.audit.read")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("master-data.category.audit.read")));

        endpoints.MapGet(
            "/api/v1/master-data/units-of-measure",
            async (
                HttpContext httpContext,
                ITrustedRequestContextResolver resolver,
                MasterDataTenantContextResolver tenantResolver,
                MasterDataCategoryUomService service) =>
                await ExecuteReadAsync(
                    httpContext,
                    resolver,
                    tenantResolver,
                    FoundationOperationCatalog.GetRequired("master-data.unit.list"),
                    context => service.ListUnitsOfMeasureAsync(context, httpContext.RequestAborted),
                    records => records.Select(ToResponse).ToArray()))
            .WithName("master-data.unit.list")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("master-data.unit.list")));

        endpoints.MapGet(
            "/api/v1/master-data/units-of-measure/{unitId:guid}",
            async (
                Guid unitId,
                HttpContext httpContext,
                ITrustedRequestContextResolver resolver,
                MasterDataTenantContextResolver tenantResolver,
                MasterDataCategoryUomService service) =>
                await ExecuteReadAsync(
                    httpContext,
                    resolver,
                    tenantResolver,
                    FoundationOperationCatalog.GetRequired("master-data.unit.read"),
                    context => service.GetUnitOfMeasureAsync(context, unitId, httpContext.RequestAborted),
                    ToResponse))
            .WithName("master-data.unit.read")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("master-data.unit.read")));

        endpoints.MapPost(
            "/api/v1/master-data/units-of-measure",
            async (
                UnitOfMeasureWriteRequest? request,
                HttpContext httpContext,
                ITrustedRequestContextResolver resolver,
                MasterDataTenantContextResolver tenantResolver,
                FoundationAuditCoordinator auditCoordinator,
                MasterDataCategoryUomService service) =>
            {
                if (!TryCreateUnitCommand(request, out var command))
                {
                    return await WriteProblemAsync(
                        httpContext,
                        StatusCodes.Status400BadRequest,
                        "validation_failed",
                        "Validation failed",
                        "The request is invalid.",
                        "master-data.unit.create");
                }

                return await ExecuteMutationAsync(
                    httpContext,
                    resolver,
                    tenantResolver,
                    auditCoordinator,
                    FoundationOperationCatalog.GetRequired("master-data.unit.create"),
                    context => service.CreateUnitOfMeasureAsync(context, command!, httpContext.RequestAborted),
                    ToResponse,
                    setEtag: true);
            })
            .WithName("master-data.unit.create")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("master-data.unit.create")));

        endpoints.MapPost(
            "/api/v1/master-data/units-of-measure/{unitId:guid}/edit",
            async (
                Guid unitId,
                UnitOfMeasureWriteRequest? request,
                HttpContext httpContext,
                ITrustedRequestContextResolver resolver,
                MasterDataTenantContextResolver tenantResolver,
                FoundationAuditCoordinator auditCoordinator,
                MasterDataCategoryUomService service) =>
            {
                if (!TryReadExpectedVersion(httpContext, out var expectedVersion)
                    || !TryCreateUnitEditCommand(unitId, request, expectedVersion, out var command))
                {
                    return await WriteProblemAsync(
                        httpContext,
                        StatusCodes.Status400BadRequest,
                        "validation_failed",
                        "Validation failed",
                        "A valid If-Match version and request are required.",
                        "master-data.unit.edit");
                }

                return await ExecuteMutationAsync(
                    httpContext,
                    resolver,
                    tenantResolver,
                    auditCoordinator,
                    FoundationOperationCatalog.GetRequired("master-data.unit.edit"),
                    context => service.EditUnitOfMeasureAsync(context, command!, httpContext.RequestAborted),
                    ToResponse,
                    setEtag: true);
            })
            .WithName("master-data.unit.edit")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("master-data.unit.edit")));

        endpoints.MapPost(
            "/api/v1/master-data/units-of-measure/{unitId:guid}/deactivate",
            async (
                Guid unitId,
                MasterDataLifecycleRequest? _,
                HttpContext httpContext,
                ITrustedRequestContextResolver resolver,
                MasterDataTenantContextResolver tenantResolver,
                FoundationAuditCoordinator auditCoordinator,
                MasterDataCategoryUomService service) =>
            {
                if (!TryReadExpectedVersion(httpContext, out var expectedVersion))
                {
                    return await WriteProblemAsync(
                        httpContext,
                        StatusCodes.Status400BadRequest,
                        "validation_failed",
                        "Validation failed",
                        "A valid If-Match version is required.",
                        "master-data.unit.deactivate");
                }

                return await ExecuteMutationAsync(
                    httpContext,
                    resolver,
                    tenantResolver,
                    auditCoordinator,
                    FoundationOperationCatalog.GetRequired("master-data.unit.deactivate"),
                    context => service.DeactivateUnitOfMeasureAsync(context, unitId, expectedVersion, httpContext.RequestAborted),
                    ToResponse,
                    setEtag: true);
            })
            .WithName("master-data.unit.deactivate")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("master-data.unit.deactivate")));

        endpoints.MapPost(
            "/api/v1/master-data/units-of-measure/{unitId:guid}/reactivate",
            async (
                Guid unitId,
                HttpContext httpContext,
                ITrustedRequestContextResolver resolver,
                MasterDataTenantContextResolver tenantResolver,
                FoundationAuditCoordinator auditCoordinator,
                MasterDataCategoryUomService service) =>
                await ExecuteMutationAsync(
                    httpContext,
                    resolver,
                    tenantResolver,
                    auditCoordinator,
                    FoundationOperationCatalog.GetRequired("master-data.unit.reactivate"),
                    context => service.ReactivateUnitOfMeasureAsync(
                        context,
                        unitId,
                        ReadExpectedVersionOrEmpty(httpContext),
                        httpContext.RequestAborted),
                    ToResponse,
                    setEtag: true,
                    requireExpectedVersion: true))
            .WithName("master-data.unit.reactivate")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("master-data.unit.reactivate")));

        endpoints.MapGet(
            "/api/v1/master-data/units-of-measure/{unitId:guid}/audit",
            async (
                Guid unitId,
                HttpContext httpContext,
                ITrustedRequestContextResolver resolver,
                MasterDataTenantContextResolver tenantResolver,
                MasterDataCategoryUomService service) =>
                await ExecuteReadAsync(
                    httpContext,
                    resolver,
                    tenantResolver,
                    FoundationOperationCatalog.GetRequired("master-data.unit.audit.read"),
                    context => service.ReadAuditHistoryAsync(
                        context,
                        MasterDataResourceKind.UnitOfMeasure,
                        unitId,
                        httpContext.RequestAborted),
                    records => records.Select(ToAuditResponse).ToArray()))
            .WithName("master-data.unit.audit.read")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("master-data.unit.audit.read")));

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

        return ToResult(httpContext, execution.Value, descriptor.OperationId, map, StatusCodes.Status200OK, setEtag);
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
            if (setEtag && result.Value is MasterDataCategoryRecord category)
            {
                httpContext.Response.Headers.ETag = $"\"{Convert.ToBase64String(category.Version)}\"";
            }
            else if (setEtag && result.Value is MasterDataUnitOfMeasureRecord unit)
            {
                httpContext.Response.Headers.ETag = $"\"{Convert.ToBase64String(unit.Version)}\"";
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
            "category_not_found"
                or "uom_not_found" => StatusCodes.Status404NotFound,
            "concurrency_conflict"
                or "category_duplicate"
                or "category_code_duplicate"
                or "category_name_duplicate"
                or "category_lifecycle_no_change"
                or "uom_duplicate"
                or "uom_code_duplicate"
                or "uom_name_duplicate"
                or "uom_lifecycle_no_change" => StatusCodes.Status409Conflict,
            "persistence_unavailable"
                or "audit_unavailable"
                or "audit_evidence_invalid"
                or "audit_evidence_unavailable" => StatusCodes.Status503ServiceUnavailable,
            _ => StatusCodes.Status400BadRequest
        };

        return Results.Problem(
            statusCode: status,
            title: status == StatusCodes.Status403Forbidden ? "Access denied" : "Master Data operation failed",
            detail: "The Category or Unit of Measure operation could not be completed.",
            type: $"https://api.minierp.local/problems/{code}",
            extensions: new Dictionary<string, object?>
            {
                ["code"] = code,
                ["correlationId"] = GetCorrelation(httpContext),
                ["operationId"] = operationId
            });
    }

    private static bool TryCreateCategoryCommand(
        CategoryWriteRequest? request,
        out CreateMasterDataCategoryCommand? command)
    {
        command = null;
        if (request is null)
        {
            return false;
        }

        try
        {
            command = new CreateMasterDataCategoryCommand(
                request.Code ?? string.Empty,
                new LocalizedName(request.EnglishName, request.ArabicName),
                request.ParentCategoryId,
                request.TrackingDefaultEnabled);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static bool TryCreateCategoryEditCommand(
        Guid categoryId,
        CategoryWriteRequest? request,
        byte[] expectedVersion,
        out EditMasterDataCategoryCommand? command)
    {
        command = null;
        if (request is null)
        {
            return false;
        }

        try
        {
            command = new EditMasterDataCategoryCommand(
                categoryId,
                request.Code ?? string.Empty,
                new LocalizedName(request.EnglishName, request.ArabicName),
                request.ParentCategoryId,
                expectedVersion,
                request.TrackingDefaultEnabled);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static bool TryCreateUnitCommand(
        UnitOfMeasureWriteRequest? request,
        out CreateMasterDataUnitOfMeasureCommand? command)
    {
        command = null;
        if (request is null)
        {
            return false;
        }

        try
        {
            command = new CreateMasterDataUnitOfMeasureCommand(
                request.Code ?? string.Empty,
                new LocalizedName(request.EnglishName, request.ArabicName));
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static bool TryCreateUnitEditCommand(
        Guid unitId,
        UnitOfMeasureWriteRequest? request,
        byte[] expectedVersion,
        out EditMasterDataUnitOfMeasureCommand? command)
    {
        command = null;
        if (request is null)
        {
            return false;
        }

        try
        {
            command = new EditMasterDataUnitOfMeasureCommand(
                unitId,
                request.Code ?? string.Empty,
                new LocalizedName(request.EnglishName, request.ArabicName),
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

    private static CategoryResponse ToResponse(MasterDataCategoryRecord record) => new(
        record.Id,
        record.TenantId.Value,
        record.Code,
        record.Name.English,
        record.Name.Arabic,
        record.ParentCategoryId,
        record.LifecycleState.ToString(),
        record.Version,
        record.TrackingDefaultEnabled);

    private static UnitOfMeasureResponse ToResponse(MasterDataUnitOfMeasureRecord record) => new(
        record.Id,
        record.TenantId.Value,
        record.Code,
        record.Name.English,
        record.Name.Arabic,
        record.LifecycleState.ToString(),
        record.Version);

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
