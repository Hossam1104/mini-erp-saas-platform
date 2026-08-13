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

/// <summary>REST composition for the bounded MESP-120 Exchange Rate capability.</summary>
public static class ExchangeRateEndpoints
{
    public static IEndpointRouteBuilder MapExchangeRateEndpoints(this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapGet(
            "/api/v1/master-data/exchange-rates",
            async (HttpContext httpContext, ITrustedRequestContextResolver resolver, MasterDataTenantContextResolver tenantResolver, MasterDataExchangeRateService service) =>
                await ExecuteReadAsync(httpContext, resolver, tenantResolver, FoundationOperationCatalog.GetRequired("master-data.exchange-rate.list"), context => service.ListExchangeRatesAsync(context, httpContext.RequestAborted), records => records.Select(ToResponse).ToArray()))
            .WithName("master-data.exchange-rate.list")
            .WithTags("Master Data / Exchange Rates")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("master-data.exchange-rate.list")));

        endpoints.MapGet(
            "/api/v1/master-data/exchange-rates/{exchangeRateId:guid}",
            async (Guid exchangeRateId, HttpContext httpContext, ITrustedRequestContextResolver resolver, MasterDataTenantContextResolver tenantResolver, MasterDataExchangeRateService service) =>
                await ExecuteReadAsync(httpContext, resolver, tenantResolver, FoundationOperationCatalog.GetRequired("master-data.exchange-rate.read"), context => service.GetExchangeRateAsync(context, exchangeRateId, httpContext.RequestAborted), ToResponse))
            .WithName("master-data.exchange-rate.read")
            .WithTags("Master Data / Exchange Rates")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("master-data.exchange-rate.read")));

        endpoints.MapGet(
            "/api/v1/master-data/exchange-rates/{exchangeRateId:guid}/history",
            async (Guid exchangeRateId, HttpContext httpContext, ITrustedRequestContextResolver resolver, MasterDataTenantContextResolver tenantResolver, MasterDataExchangeRateService service) =>
                await ExecuteReadAsync(httpContext, resolver, tenantResolver, FoundationOperationCatalog.GetRequired("master-data.exchange-rate.history.read"), context => service.GetExchangeRateHistoryAsync(context, exchangeRateId, httpContext.RequestAborted), record => record.Versions.Select(ToResponse).ToArray()))
            .WithName("master-data.exchange-rate.history.read")
            .WithTags("Master Data / Exchange Rates")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("master-data.exchange-rate.history.read")));

        endpoints.MapGet(
            "/api/v1/master-data/exchange-rates/{exchangeRateId:guid}/reference",
            async (Guid exchangeRateId, [FromQuery(Name = "effectiveOn")] string? effectiveOn, HttpContext httpContext, ITrustedRequestContextResolver resolver, MasterDataTenantContextResolver tenantResolver, MasterDataExchangeRateService service) =>
            {
                if (!TryReadDate(effectiveOn, out var parsedEffectiveOn))
                {
                    return await WriteProblemAsync(httpContext, 400, "validation_failed", "Validation failed", "An ISO effectiveOn date is required.", "master-data.exchange-rate.reference.read");
                }

                return await ExecuteReadAsync(httpContext, resolver, tenantResolver, FoundationOperationCatalog.GetRequired("master-data.exchange-rate.reference.read"), context => service.GetExchangeRateReferenceAsync(context, exchangeRateId, parsedEffectiveOn, httpContext.RequestAborted), ToReferenceResponse);
            })
            .WithName("master-data.exchange-rate.reference.read")
            .WithTags("Master Data / Exchange Rates")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("master-data.exchange-rate.reference.read")));

        endpoints.MapPost(
            "/api/v1/master-data/exchange-rates",
            async (ExchangeRateWriteRequest? request, HttpContext httpContext, ITrustedRequestContextResolver resolver, MasterDataTenantContextResolver tenantResolver, FoundationAuditCoordinator auditCoordinator, LocalMasterDataIdempotencyStore idempotencyStore, MasterDataExchangeRateService service) =>
            {
                if (!TryCreateCommand(request, out var command))
                {
                    return await WriteProblemAsync(httpContext, 400, "validation_failed", "Validation failed", "The Exchange Rate request is invalid.", "master-data.exchange-rate.create");
                }

                return await ExecuteMutationAsync(httpContext, resolver, tenantResolver, auditCoordinator, idempotencyStore, FoundationOperationCatalog.GetRequired("master-data.exchange-rate.create"), Fingerprint(request), context => service.CreateExchangeRateAsync(context, command!, httpContext.RequestAborted), ToResponse, setEtag: true);
            })
            .WithName("master-data.exchange-rate.create")
            .WithTags("Master Data / Exchange Rates")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("master-data.exchange-rate.create")));

        endpoints.MapPost(
            "/api/v1/master-data/exchange-rates/{exchangeRateId:guid}/edit",
            async (Guid exchangeRateId, ExchangeRateWriteRequest? request, HttpContext httpContext, ITrustedRequestContextResolver resolver, MasterDataTenantContextResolver tenantResolver, FoundationAuditCoordinator auditCoordinator, LocalMasterDataIdempotencyStore idempotencyStore, MasterDataExchangeRateService service) =>
            {
                if (!TryReadExpectedVersion(httpContext, out var expectedVersion) || !TryCreateEditCommand(exchangeRateId, request, expectedVersion, out var command))
                {
                    return await WriteProblemAsync(httpContext, 400, "validation_failed", "Validation failed", "A valid If-Match version and Exchange Rate request are required.", "master-data.exchange-rate.edit");
                }

                return await ExecuteMutationAsync(httpContext, resolver, tenantResolver, auditCoordinator, idempotencyStore, FoundationOperationCatalog.GetRequired("master-data.exchange-rate.edit"), Fingerprint(request) + VersionFingerprint(expectedVersion), context => service.EditExchangeRateAsync(context, command!, httpContext.RequestAborted), ToResponse, setEtag: true, requireExpectedVersion: true);
            })
            .WithName("master-data.exchange-rate.edit")
            .WithTags("Master Data / Exchange Rates")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("master-data.exchange-rate.edit")));

        endpoints.MapPost(
            "/api/v1/master-data/exchange-rates/{exchangeRateId:guid}/deactivate",
            async (Guid exchangeRateId, MasterDataLifecycleRequest? _, HttpContext httpContext, ITrustedRequestContextResolver resolver, MasterDataTenantContextResolver tenantResolver, FoundationAuditCoordinator auditCoordinator, LocalMasterDataIdempotencyStore idempotencyStore, MasterDataExchangeRateService service) =>
            {
                if (!TryReadExpectedVersion(httpContext, out var expectedVersion))
                {
                    return await WriteProblemAsync(httpContext, 400, "validation_failed", "Validation failed", "A valid If-Match version is required.", "master-data.exchange-rate.deactivate");
                }

                return await ExecuteMutationAsync(httpContext, resolver, tenantResolver, auditCoordinator, idempotencyStore, FoundationOperationCatalog.GetRequired("master-data.exchange-rate.deactivate"), VersionFingerprint(expectedVersion), context => service.DeactivateExchangeRateAsync(context, exchangeRateId, expectedVersion, httpContext.RequestAborted), ToResponse, setEtag: true, requireExpectedVersion: true);
            })
            .WithName("master-data.exchange-rate.deactivate")
            .WithTags("Master Data / Exchange Rates")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("master-data.exchange-rate.deactivate")));

        endpoints.MapPost(
            "/api/v1/master-data/exchange-rates/{exchangeRateId:guid}/reactivate",
            async (Guid exchangeRateId, HttpContext httpContext, ITrustedRequestContextResolver resolver, MasterDataTenantContextResolver tenantResolver, FoundationAuditCoordinator auditCoordinator, LocalMasterDataIdempotencyStore idempotencyStore, MasterDataExchangeRateService service) =>
            {
                if (!TryReadExpectedVersion(httpContext, out var expectedVersion))
                {
                    return await WriteProblemAsync(httpContext, 400, "validation_failed", "Validation failed", "A valid If-Match version is required.", "master-data.exchange-rate.reactivate");
                }

                return await ExecuteMutationAsync(httpContext, resolver, tenantResolver, auditCoordinator, idempotencyStore, FoundationOperationCatalog.GetRequired("master-data.exchange-rate.reactivate"), VersionFingerprint(expectedVersion), context => service.ReactivateExchangeRateAsync(context, exchangeRateId, expectedVersion, httpContext.RequestAborted), ToResponse, setEtag: true, requireExpectedVersion: true);
            })
            .WithName("master-data.exchange-rate.reactivate")
            .WithTags("Master Data / Exchange Rates")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("master-data.exchange-rate.reactivate")));

        endpoints.MapGet(
            "/api/v1/master-data/exchange-rates/{exchangeRateId:guid}/audit",
            async (Guid exchangeRateId, HttpContext httpContext, ITrustedRequestContextResolver resolver, MasterDataTenantContextResolver tenantResolver, MasterDataExchangeRateService service) =>
                await ExecuteReadAsync(httpContext, resolver, tenantResolver, FoundationOperationCatalog.GetRequired("master-data.exchange-rate.audit.read"), context => service.ReadAuditHistoryAsync(context, exchangeRateId, httpContext.RequestAborted), records => records.Select(ToAuditResponse).ToArray()))
            .WithName("master-data.exchange-rate.audit.read")
            .WithTags("Master Data / Exchange Rates")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("master-data.exchange-rate.audit.read")));

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
            if (setEtag && result.Value is MasterDataExchangeRateRecord exchangeRate)
            {
                httpContext.Response.Headers.ETag = $"\"{Convert.ToBase64String(exchangeRate.Version)}\"";
            }

            return Results.Json(map(result.Value));
        }

        var code = result.Code;
        var status = code switch
        {
            "permission_denied" or "resource_scope_denied" or "cross_tenant_target_denied" or "tenant_context_failed" or "authorization_denied" or "approval_required" or "approval_policy_not_configured" or "resource_policy_not_configured" => 403,
            "permission_unavailable" or "scope_policy_unavailable" or "approval_policy_unavailable" or "resource_policy_unavailable" or "authorization_operation_unmapped" or "persistence_unavailable" or "audit_unavailable" or "audit_context_mismatch" => 503,
            "exchange_rate_not_found" or "exchange_rate_version_not_found" or "exchange_rate_currency_not_found" => 404,
            "concurrency_conflict" or "exchange_rate_effective_overlap" or "exchange_rate_pair_immutable" or "exchange_rate_duplicate" or "exchange_rate_lifecycle_no_change" or "idempotency_conflict" => 409,
            _ => 400
        };

        return Results.Problem(
            statusCode: status,
            title: status == 403 ? "Access denied" : "Master Data operation failed",
            detail: "The Exchange Rate operation could not be completed.",
            type: $"https://api.minierp.local/problems/{code}",
            extensions: new Dictionary<string, object?>
            {
                ["code"] = code,
                ["correlationId"] = GetCorrelation(httpContext),
                ["operationId"] = operationId
            });
    }

    private static bool TryCreateCommand(ExchangeRateWriteRequest? request, out CreateMasterDataExchangeRateCommand? command)
    {
        command = null;
        if (request is null)
        {
            return false;
        }

        command = new CreateMasterDataExchangeRateCommand(
            request.SourceCurrencyId,
            request.TargetCurrencyId,
            request.EffectiveFrom,
            request.EffectiveTo,
            request.Rate,
            request.RateScale,
            request.Provenance,
            request.SourceNotes);
        return true;
    }

    private static bool TryCreateEditCommand(Guid id, ExchangeRateWriteRequest? request, byte[] expectedVersion, out EditMasterDataExchangeRateCommand? command)
    {
        command = null;
        if (request is null)
        {
            return false;
        }

        command = new EditMasterDataExchangeRateCommand(
            id,
            request.SourceCurrencyId,
            request.TargetCurrencyId,
            request.EffectiveFrom,
            request.EffectiveTo,
            request.Rate,
            request.RateScale,
            request.Provenance,
            request.SourceNotes,
            expectedVersion);
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

    private static ExchangeRateResponse ToResponse(MasterDataExchangeRateRecord record) => new(
        record.Id,
        record.TenantId.Value,
        record.SourceCurrencyId,
        record.TargetCurrencyId,
        record.SourceCurrencyCode,
        record.TargetCurrencyCode,
        record.LifecycleState.ToString(),
        record.CurrentVersionNumber,
        record.Versions.Select(ToResponse).ToArray(),
        record.Version);

    private static ExchangeRateVersionResponse ToResponse(MasterDataExchangeRateVersionRecord record) => new(
        record.Id,
        record.VersionNumber,
        record.EffectiveFrom,
        record.EffectiveTo,
        record.Rate,
        record.RateScale,
        record.Provenance,
        record.SourceNotes,
        record.SourceCurrencyCode,
        record.TargetCurrencyCode);

    private static ExchangeRateReferenceResponse ToReferenceResponse(MasterDataExchangeRateReferenceRecord record) => new(
        record.Id,
        record.TenantId.Value,
        record.SourceCurrencyId,
        record.TargetCurrencyId,
        record.SourceCurrencyCode,
        record.TargetCurrencyCode,
        record.LifecycleState.ToString(),
        record.VersionNumber,
        record.VersionId,
        record.EffectiveOn,
        record.Version.EffectiveFrom,
        record.Version.EffectiveTo,
        record.Version.Rate,
        record.Version.RateScale,
        record.Version.Provenance,
        record.Version.SourceNotes,
        record.Snapshot.AppliedValue,
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
