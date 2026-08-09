#pragma warning disable CS1591

using Microsoft.AspNetCore.Antiforgery;
using MiniErp.App.BuildingBlocks.Rest;
using MiniErp.App.Modules.Audit;
using MiniErp.App.Modules.BusinessParties;
using MiniErp.App.Modules.MasterData;
using MiniErp.Contracts.Modules.Audit;
using MiniErp.Contracts.Modules.BusinessParties;
using MiniErp.Contracts.Modules.Foundation;
using MiniErp.Contracts.Modules.MasterData;

namespace MiniErp.Api;

public static class SupplierEndpoints
{
    public static IEndpointRouteBuilder MapSupplierEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapGet(
            "/api/v1/master-data/suppliers",
            async (
                HttpContext httpContext,
                ITrustedRequestContextResolver resolver,
                MasterDataTenantContextResolver tenantResolver,
                SupplierService service) =>
                await ExecuteReadAsync(
                    httpContext,
                    resolver,
                    tenantResolver,
                    FoundationOperationCatalog.GetRequired("master-data.supplier.list"),
                    context => service.ListSuppliersAsync(context, httpContext.RequestAborted),
                    records => records.Select(ToResponse).ToArray()))
            .WithName("master-data.supplier.list")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("master-data.supplier.list")));

        endpoints.MapGet(
            "/api/v1/master-data/suppliers/{supplierId:guid}",
            async (
                Guid supplierId,
                HttpContext httpContext,
                ITrustedRequestContextResolver resolver,
                MasterDataTenantContextResolver tenantResolver,
                SupplierService service) =>
                await ExecuteReadAsync(
                    httpContext,
                    resolver,
                    tenantResolver,
                    FoundationOperationCatalog.GetRequired("master-data.supplier.read"),
                    context => service.GetSupplierAsync(context, supplierId, httpContext.RequestAborted),
                    ToResponse))
            .WithName("master-data.supplier.read")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("master-data.supplier.read")));

        endpoints.MapPost(
            "/api/v1/master-data/suppliers",
            async (
                SupplierWriteRequest? request,
                HttpContext httpContext,
                ITrustedRequestContextResolver resolver,
                MasterDataTenantContextResolver tenantResolver,
                FoundationAuditCoordinator auditCoordinator,
                SupplierService service) =>
            {
                if (!TryCreateCommand(request, out var command))
                {
                    return await WriteProblemAsync(
                        httpContext,
                        StatusCodes.Status400BadRequest,
                        "validation_failed",
                        "Validation failed",
                        "The request is invalid.",
                        "master-data.supplier.create");
                }

                return await ExecuteMutationAsync(
                    httpContext,
                    resolver,
                    tenantResolver,
                    auditCoordinator,
                    FoundationOperationCatalog.GetRequired("master-data.supplier.create"),
                    context => service.CreateSupplierAsync(context, command!, httpContext.RequestAborted),
                    ToResponse,
                    setEtag: true);
            })
            .WithName("master-data.supplier.create")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("master-data.supplier.create")));

        endpoints.MapPost(
            "/api/v1/master-data/suppliers/{supplierId:guid}/edit",
            async (
                Guid supplierId,
                SupplierWriteRequest? request,
                HttpContext httpContext,
                ITrustedRequestContextResolver resolver,
                MasterDataTenantContextResolver tenantResolver,
                FoundationAuditCoordinator auditCoordinator,
                SupplierService service) =>
            {
                if (!TryReadExpectedVersion(httpContext, out var expectedVersion)
                    || !TryCreateEditCommand(supplierId, request, expectedVersion, out var command))
                {
                    return await WriteProblemAsync(
                        httpContext,
                        StatusCodes.Status400BadRequest,
                        "validation_failed",
                        "Validation failed",
                        "A valid If-Match version and request are required.",
                        "master-data.supplier.edit");
                }

                return await ExecuteMutationAsync(
                    httpContext,
                    resolver,
                    tenantResolver,
                    auditCoordinator,
                    FoundationOperationCatalog.GetRequired("master-data.supplier.edit"),
                    context => service.EditSupplierAsync(context, command!, httpContext.RequestAborted),
                    ToResponse,
                    setEtag: true);
            })
            .WithName("master-data.supplier.edit")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("master-data.supplier.edit")));

        endpoints.MapPost(
            "/api/v1/master-data/suppliers/{supplierId:guid}/deactivate",
            async (
                Guid supplierId,
                SupplierLifecycleRequest? request,
                HttpContext httpContext,
                ITrustedRequestContextResolver resolver,
                MasterDataTenantContextResolver tenantResolver,
                FoundationAuditCoordinator auditCoordinator,
                SupplierService service) =>
            {
                if (!TryReadExpectedVersion(httpContext, out var expectedVersion))
                {
                    return await WriteProblemAsync(
                        httpContext,
                        StatusCodes.Status400BadRequest,
                        "validation_failed",
                        "Validation failed",
                        "A valid If-Match version is required.",
                        "master-data.supplier.deactivate");
                }

                return await ExecuteMutationAsync(
                    httpContext,
                    resolver,
                    tenantResolver,
                    auditCoordinator,
                    FoundationOperationCatalog.GetRequired("master-data.supplier.deactivate"),
                    context => service.DeactivateSupplierAsync(
                        context,
                        supplierId,
                        expectedVersion,
                        request?.Reason,
                        httpContext.RequestAborted),
                    ToResponse,
                    setEtag: true);
            })
            .WithName("master-data.supplier.deactivate")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("master-data.supplier.deactivate")));

        endpoints.MapPost(
            "/api/v1/master-data/suppliers/{supplierId:guid}/reactivate",
            async (
                Guid supplierId,
                HttpContext httpContext,
                ITrustedRequestContextResolver resolver,
                MasterDataTenantContextResolver tenantResolver,
                FoundationAuditCoordinator auditCoordinator,
                SupplierService service) =>
                await ExecuteMutationAsync(
                    httpContext,
                    resolver,
                    tenantResolver,
                    auditCoordinator,
                    FoundationOperationCatalog.GetRequired("master-data.supplier.reactivate"),
                    context => service.ReactivateSupplierAsync(
                        context,
                        supplierId,
                        ReadExpectedVersionOrEmpty(httpContext),
                        httpContext.RequestAborted),
                    ToResponse,
                    setEtag: true,
                    requireExpectedVersion: true))
            .WithName("master-data.supplier.reactivate")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("master-data.supplier.reactivate")));

        endpoints.MapGet(
            "/api/v1/master-data/suppliers/{supplierId:guid}/audit",
            async (
                Guid supplierId,
                HttpContext httpContext,
                ITrustedRequestContextResolver resolver,
                MasterDataTenantContextResolver tenantResolver,
                SupplierService service) =>
                await ExecuteReadAsync(
                    httpContext,
                    resolver,
                    tenantResolver,
                    FoundationOperationCatalog.GetRequired("master-data.supplier.audit.read"),
                    context => service.ReadAuditHistoryAsync(context, supplierId, httpContext.RequestAborted),
                    records => records.Select(ToResponse).ToArray()))
            .WithName("master-data.supplier.audit.read")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("master-data.supplier.audit.read")));

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
            if (setEtag && result.Value is SupplierRecord supplier)
            {
                httpContext.Response.Headers.ETag = $"\"{Convert.ToBase64String(supplier.Version)}\"";
            }

            return Results.Json(map(result.Value), statusCode: successStatus);
        }

        var code = result.Code;
        var status = code switch
        {
            "permission_denied"
                or "permission_unavailable"
                or "resource_scope_denied"
                or "cross_tenant_target_denied"
                or "tenant_context_failed" => StatusCodes.Status403Forbidden,
            "supplier_not_found" => StatusCodes.Status404NotFound,
            "concurrency_conflict"
                or "supplier_duplicate"
                or "supplier_code_duplicate"
                or "supplier_registration_duplicate"
                or "supplier_persistence_conflict"
                or "supplier_lifecycle_no_change" => StatusCodes.Status409Conflict,
            "persistence_unavailable"
                or "audit_unavailable"
                or "audit_evidence_invalid"
                or "audit_evidence_unavailable" => StatusCodes.Status503ServiceUnavailable,
            _ => StatusCodes.Status400BadRequest
        };
        return Results.Problem(
            statusCode: status,
            title: status == StatusCodes.Status403Forbidden ? "Access denied" : "Supplier operation failed",
            detail: "The Supplier operation could not be completed.",
            type: $"https://api.minierp.local/problems/{code}",
            extensions: new Dictionary<string, object?>
            {
                ["code"] = code,
                ["correlationId"] = GetCorrelation(httpContext),
                ["operationId"] = operationId
            });
    }

    private static bool TryCreateCommand(
        SupplierWriteRequest? request,
        out CreateSupplierCommand? command)
    {
        command = null;
        if (request is null)
        {
            return false;
        }

        try
        {
            command = new CreateSupplierCommand(
                request.Code ?? string.Empty,
                new LocalizedName(request.EnglishLegalName, request.ArabicLegalName),
                CreateOptionalLocalizedName(request.EnglishTradingName, request.ArabicTradingName),
                request.RegistrationReference,
                request.Contacts?.Select(ToCommand).ToArray() ?? []);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static bool TryCreateEditCommand(
        Guid supplierId,
        SupplierWriteRequest? request,
        byte[] expectedVersion,
        out EditSupplierCommand? command)
    {
        command = null;
        if (request is null)
        {
            return false;
        }

        try
        {
            command = new EditSupplierCommand(
                supplierId,
                request.Code ?? string.Empty,
                new LocalizedName(request.EnglishLegalName, request.ArabicLegalName),
                CreateOptionalLocalizedName(request.EnglishTradingName, request.ArabicTradingName),
                request.RegistrationReference,
                request.Contacts?.Select(ToCommand).ToArray() ?? [],
                expectedVersion);
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static SupplierContactCommand ToCommand(SupplierContactWriteRequest? request)
    {
        ArgumentNullException.ThrowIfNull(request);
        return new SupplierContactCommand(request.Name ?? string.Empty, request.Email, request.Phone);
    }

    private static LocalizedName? CreateOptionalLocalizedName(string? english, string? arabic) =>
        english is null && arabic is null ? null : new LocalizedName(english, arabic);

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

    private static SupplierResponse ToResponse(SupplierRecord record) => new(
        record.Id,
        record.TenantId.Value,
        record.Code,
        record.LegalName.English,
        record.LegalName.Arabic,
        record.TradingName?.English,
        record.TradingName?.Arabic,
        record.RegistrationReference,
        record.LifecycleState.ToString(),
        record.Version,
        record.Contacts.Select(contact => new SupplierContactResponse(
            contact.Id,
            contact.SupplierId,
            contact.Name,
            contact.Email,
            contact.Phone,
            contact.Version)).ToArray());

    private static SupplierAuditResponse ToResponse(MasterDataAuditRecord record) => new(
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
