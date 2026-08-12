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

/// <summary>REST composition for the bounded MESP-119 Tax/VAT capability.</summary>
public static class TaxEndpoints
{
    public static IEndpointRouteBuilder MapTaxEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapGet(
            "/api/v1/master-data/taxes",
            async (HttpContext httpContext, ITrustedRequestContextResolver resolver, MasterDataTenantContextResolver tenantResolver, MasterDataTaxService service) =>
                await ExecuteReadAsync(httpContext, resolver, tenantResolver, FoundationOperationCatalog.GetRequired("master-data.tax.list"), context => service.ListTaxesAsync(context, httpContext.RequestAborted), records => records.Select(ToResponse).ToArray()))
            .WithName("master-data.tax.list")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("master-data.tax.list")));

        endpoints.MapGet(
            "/api/v1/master-data/taxes/{taxId:guid}",
            async (Guid taxId, HttpContext httpContext, ITrustedRequestContextResolver resolver, MasterDataTenantContextResolver tenantResolver, MasterDataTaxService service) =>
                await ExecuteReadAsync(httpContext, resolver, tenantResolver, FoundationOperationCatalog.GetRequired("master-data.tax.read"), context => service.GetTaxAsync(context, taxId, httpContext.RequestAborted), ToResponse))
            .WithName("master-data.tax.read")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("master-data.tax.read")));

        endpoints.MapGet(
            "/api/v1/master-data/taxes/{taxId:guid}/history",
            async (Guid taxId, HttpContext httpContext, ITrustedRequestContextResolver resolver, MasterDataTenantContextResolver tenantResolver, MasterDataTaxService service) =>
                await ExecuteReadAsync(httpContext, resolver, tenantResolver, FoundationOperationCatalog.GetRequired("master-data.tax.history.read"), context => service.GetTaxHistoryAsync(context, taxId, httpContext.RequestAborted), record => record.RateVersions.Select(ToResponse).ToArray()))
            .WithName("master-data.tax.history.read")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("master-data.tax.history.read")));

        endpoints.MapGet(
            "/api/v1/master-data/taxes/{taxId:guid}/reference",
            async (Guid taxId, HttpContext httpContext, ITrustedRequestContextResolver resolver, MasterDataTenantContextResolver tenantResolver, MasterDataTaxService service) =>
            {
                if (!TryReadDate(httpContext.Request.Query["effectiveOn"], out var effectiveOn))
                {
                    return await WriteProblemAsync(httpContext, 400, "validation_failed", "Validation failed", "An ISO effectiveOn date is required.", "master-data.tax.reference.read");
                }

                return await ExecuteReadAsync(httpContext, resolver, tenantResolver, FoundationOperationCatalog.GetRequired("master-data.tax.reference.read"), context => service.GetTaxReferenceAsync(context, taxId, effectiveOn, httpContext.RequestAborted), ToReferenceResponse);
            })
            .WithName("master-data.tax.reference.read")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("master-data.tax.reference.read")));

        endpoints.MapPost(
            "/api/v1/master-data/taxes/{taxId:guid}/calculate",
            async (Guid taxId, TaxCalculationRequest? request, HttpContext httpContext, ITrustedRequestContextResolver resolver, MasterDataTenantContextResolver tenantResolver, MasterDataTaxService service) =>
            {
                if (request is null)
                {
                    return await WriteProblemAsync(httpContext, 400, "validation_failed", "Validation failed", "An explicit Tax calculation request is required.", "master-data.tax.calculate");
                }

                return await ExecuteReadAsync(httpContext, resolver, tenantResolver, FoundationOperationCatalog.GetRequired("master-data.tax.calculate"), context => service.CalculateTaxAsync(context, taxId, request, httpContext.RequestAborted), ToCalculationResponse);
            })
            .WithName("master-data.tax.calculate")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("master-data.tax.calculate")));

        endpoints.MapPost(
            "/api/v1/master-data/taxes",
            async (TaxWriteRequest? request, HttpContext httpContext, ITrustedRequestContextResolver resolver, MasterDataTenantContextResolver tenantResolver, FoundationAuditCoordinator auditCoordinator, LocalMasterDataIdempotencyStore idempotencyStore, MasterDataTaxService service) =>
            {
                if (!TryCreateTaxCommand(request, out var command))
                {
                    return await WriteProblemAsync(httpContext, 400, "validation_failed", "Validation failed", "The Tax request is invalid.", "master-data.tax.create");
                }

                return await ExecuteMutationAsync(httpContext, resolver, tenantResolver, auditCoordinator, idempotencyStore, FoundationOperationCatalog.GetRequired("master-data.tax.create"), Fingerprint(request), context => service.CreateTaxAsync(context, command!, httpContext.RequestAborted), ToResponse, setEtag: true);
            })
            .WithName("master-data.tax.create")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("master-data.tax.create")));

        endpoints.MapPost(
            "/api/v1/master-data/taxes/{taxId:guid}/edit",
            async (Guid taxId, TaxWriteRequest? request, HttpContext httpContext, ITrustedRequestContextResolver resolver, MasterDataTenantContextResolver tenantResolver, FoundationAuditCoordinator auditCoordinator, LocalMasterDataIdempotencyStore idempotencyStore, MasterDataTaxService service) =>
            {
                if (!TryReadExpectedVersion(httpContext, out var expectedVersion) || !TryCreateTaxEditCommand(taxId, request, expectedVersion, out var command))
                {
                    return await WriteProblemAsync(httpContext, 400, "validation_failed", "Validation failed", "A valid If-Match version and Tax request are required.", "master-data.tax.edit");
                }

                return await ExecuteMutationAsync(httpContext, resolver, tenantResolver, auditCoordinator, idempotencyStore, FoundationOperationCatalog.GetRequired("master-data.tax.edit"), Fingerprint(request) + VersionFingerprint(expectedVersion), context => service.EditTaxAsync(context, command!, httpContext.RequestAborted), ToResponse, setEtag: true, requireExpectedVersion: true);
            })
            .WithName("master-data.tax.edit")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("master-data.tax.edit")));

        endpoints.MapPost(
            "/api/v1/master-data/taxes/{taxId:guid}/deactivate",
            async (Guid taxId, MasterDataLifecycleRequest? _, HttpContext httpContext, ITrustedRequestContextResolver resolver, MasterDataTenantContextResolver tenantResolver, FoundationAuditCoordinator auditCoordinator, LocalMasterDataIdempotencyStore idempotencyStore, MasterDataTaxService service) =>
            {
                if (!TryReadExpectedVersion(httpContext, out var expectedVersion))
                {
                    return await WriteProblemAsync(httpContext, 400, "validation_failed", "Validation failed", "A valid If-Match version is required.", "master-data.tax.deactivate");
                }

                return await ExecuteMutationAsync(httpContext, resolver, tenantResolver, auditCoordinator, idempotencyStore, FoundationOperationCatalog.GetRequired("master-data.tax.deactivate"), VersionFingerprint(expectedVersion), context => service.DeactivateTaxAsync(context, taxId, expectedVersion, httpContext.RequestAborted), ToResponse, setEtag: true, requireExpectedVersion: true);
            })
            .WithName("master-data.tax.deactivate")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("master-data.tax.deactivate")));

        endpoints.MapPost(
            "/api/v1/master-data/taxes/{taxId:guid}/reactivate",
            async (Guid taxId, HttpContext httpContext, ITrustedRequestContextResolver resolver, MasterDataTenantContextResolver tenantResolver, FoundationAuditCoordinator auditCoordinator, LocalMasterDataIdempotencyStore idempotencyStore, MasterDataTaxService service) =>
            {
                if (!TryReadExpectedVersion(httpContext, out var expectedVersion))
                {
                    return await WriteProblemAsync(httpContext, 400, "validation_failed", "Validation failed", "A valid If-Match version is required.", "master-data.tax.reactivate");
                }

                return await ExecuteMutationAsync(httpContext, resolver, tenantResolver, auditCoordinator, idempotencyStore, FoundationOperationCatalog.GetRequired("master-data.tax.reactivate"), VersionFingerprint(expectedVersion), context => service.ReactivateTaxAsync(context, taxId, expectedVersion, httpContext.RequestAborted), ToResponse, setEtag: true, requireExpectedVersion: true);
            })
            .WithName("master-data.tax.reactivate")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("master-data.tax.reactivate")));

        endpoints.MapGet(
            "/api/v1/master-data/taxes/{taxId:guid}/audit",
            async (Guid taxId, HttpContext httpContext, ITrustedRequestContextResolver resolver, MasterDataTenantContextResolver tenantResolver, MasterDataTaxService service) =>
                await ExecuteReadAsync(httpContext, resolver, tenantResolver, FoundationOperationCatalog.GetRequired("master-data.tax.audit.read"), context => service.ReadAuditHistoryAsync(context, taxId, httpContext.RequestAborted), records => records.Select(ToAuditResponse).ToArray()))
            .WithName("master-data.tax.audit.read")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("master-data.tax.audit.read")));

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

    private static IResult ToResult<T>(
        HttpContext httpContext,
        MasterDataOperationResult<T> result,
        string operationId,
        Func<T, object?> map,
        bool setEtag)
    {
        if (result.Succeeded && result.Value is not null)
        {
            if (setEtag && result.Value is MasterDataTaxRecord tax)
            {
                httpContext.Response.Headers.ETag = $"\"{Convert.ToBase64String(tax.Version)}\"";
            }

            return Results.Json(map(result.Value));
        }

        var code = result.Code;
        var status = code switch
        {
            "permission_denied" or "resource_scope_denied" or "cross_tenant_target_denied" or "tenant_context_failed" or "authorization_denied" or "approval_required" or "approval_policy_not_configured" or "resource_policy_not_configured" => 403,
            "permission_unavailable" or "scope_policy_unavailable" or "approval_policy_unavailable" or "resource_policy_unavailable" or "authorization_operation_unmapped" or "persistence_unavailable" or "audit_unavailable" or "audit_context_mismatch" => 503,
            "tax_not_found" or "tax_version_not_found" => 404,
            "concurrency_conflict" or "tax_effective_overlap" or "tax_duplicate" or "tax_lifecycle_no_change" or "idempotency_conflict" => 409,
            _ => 400
        };

        return Results.Problem(
            statusCode: status,
            title: status == 403 ? "Access denied" : "Master Data operation failed",
            detail: "The Tax operation could not be completed.",
            type: $"https://api.minierp.local/problems/{code}",
            extensions: new Dictionary<string, object?>
            {
                ["code"] = code,
                ["correlationId"] = GetCorrelation(httpContext),
                ["operationId"] = operationId
            });
    }

    private static bool TryCreateTaxCommand(TaxWriteRequest? request, out CreateMasterDataTaxCommand? command)
    {
        command = null;
        if (request is null || request.RateVersion is null)
        {
            return false;
        }

        try
        {
            command = new CreateMasterDataTaxCommand(
                request.Code ?? string.Empty,
                request.CategoryCode ?? string.Empty,
                new LocalizedName(request.CategoryEnglishName, request.CategoryArabicName),
                new LocalizedName(request.EnglishName, request.ArabicName),
                request.Applicability,
                new MasterDataTaxRateVersion(request.RateVersion.EffectiveFrom, request.RateVersion.EffectiveTo, request.RateVersion.RatePercentage));
            return true;
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static bool TryCreateTaxEditCommand(Guid id, TaxWriteRequest? request, byte[] expectedVersion, out EditMasterDataTaxCommand? command)
    {
        command = null;
        if (request is null || request.RateVersion is null)
        {
            return false;
        }

        try
        {
            command = new EditMasterDataTaxCommand(
                id,
                request.Code ?? string.Empty,
                request.CategoryCode ?? string.Empty,
                new LocalizedName(request.CategoryEnglishName, request.CategoryArabicName),
                new LocalizedName(request.EnglishName, request.ArabicName),
                request.Applicability,
                new MasterDataTaxRateVersion(request.RateVersion.EffectiveFrom, request.RateVersion.EffectiveTo, request.RateVersion.RatePercentage),
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
        if (string.IsNullOrWhiteSpace(value)) return false;
        var normalized = value.Trim();
        if (normalized.StartsWith("W/", StringComparison.OrdinalIgnoreCase)) return false;
        if (normalized.Length >= 2 && normalized[0] == '"' && normalized[^1] == '"') normalized = normalized[1..^1];
        try { version = Convert.FromBase64String(normalized); return version.Length is > 0 and <= 64; }
        catch (FormatException) { version = []; return false; }
    }

    private static bool TryReadDate(string? value, out DateOnly date) =>
        DateOnly.TryParseExact(value, "yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out date);

    private static string Fingerprint(object? request) => JsonSerializer.Serialize(request);

    private static string VersionFingerprint(byte[] version) => $"|version:{Convert.ToBase64String(version)}";

    private static TaxResponse ToResponse(MasterDataTaxRecord record) => new(
        record.Id,
        record.TenantId.Value,
        record.Code,
        record.CategoryCode,
        record.CategoryName.English,
        record.CategoryName.Arabic,
        record.Name.English,
        record.Name.Arabic,
        record.Applicability,
        record.LifecycleState.ToString(),
        record.CurrentVersionNumber,
        record.RateVersions.Select(ToResponse).ToArray(),
        record.Version);

    private static TaxRateVersionResponse ToResponse(MasterDataTaxRateVersionRecord record) => new(
        record.Id,
        record.VersionNumber,
        record.EffectiveFrom,
        record.EffectiveTo,
        record.RatePercentage);

    private static TaxReferenceResponse ToReferenceResponse(MasterDataTaxReferenceRecord record) => new(
        record.Id,
        record.TenantId.Value,
        record.Code,
        record.CategoryCode,
        record.CategoryName.English,
        record.CategoryName.Arabic,
        record.Name.English,
        record.Name.Arabic,
        record.Applicability,
        record.LifecycleState.ToString(),
        record.VersionNumber,
        record.VersionId,
        record.EffectiveOn,
        record.RateVersion.EffectiveFrom,
        record.RateVersion.EffectiveTo,
        record.RateVersion.RatePercentage,
        record.Snapshot.AppliedValue,
        record.MasterVersion);

    private static TaxCalculationResponse ToCalculationResponse(MasterDataTaxCalculation record) => new(
        record.TaxId,
        record.TenantId.Value,
        record.Code,
        record.CategoryCode,
        record.Applicability,
        record.TransactionDirection,
        record.RateVersionId,
        record.RateVersionNumber,
        record.EffectiveOn,
        record.EffectiveFrom,
        record.EffectiveTo,
        record.RatePercentage,
        record.TaxableBase,
        record.TaxAmount,
        record.CurrencyCode,
        record.RoundingScale,
        record.RoundingMode,
        record.SourceLineage,
        record.Snapshot.AppliedValue);

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
