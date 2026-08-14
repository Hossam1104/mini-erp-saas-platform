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

/// <summary>REST adapter for the Phase-A structured Master Data import seam.</summary>
public static class MasterDataImportEndpoints
{
    public static IEndpointRouteBuilder MapMasterDataImportEndpoints(
        this IEndpointRouteBuilder endpoints)
    {
        ArgumentNullException.ThrowIfNull(endpoints);

        endpoints.MapPost(
            "/api/v1/master-data/imports",
            async (MasterDataImportBatchRequest? request,
                HttpContext httpContext,
                ITrustedRequestContextResolver resolver,
                MasterDataTenantContextResolver tenantResolver,
                FoundationAuditCoordinator auditCoordinator,
                LocalMasterDataIdempotencyStore idempotencyStore,
                MasterDataImportService service) =>
            {
                if (request is null)
                {
                    return await WriteProblemAsync(httpContext, 400, "validation_failed", "Validation failed", "An import batch request is required.", "master-data.import.create");
                }

                return await ExecuteMutationAsync(
                    httpContext,
                    resolver,
                    tenantResolver,
                    auditCoordinator,
                    idempotencyStore,
                    FoundationOperationCatalog.GetRequired("master-data.import.create"),
                    JsonSerializer.Serialize(request),
                    context => service.CreateBatchAsync(context, request, IdempotencyKey(httpContext), httpContext.RequestAborted),
                    ToBatchResponse,
                    requireExpectedVersion: false);
            })
            .WithName("master-data.import.create")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("master-data.import.create")));

        endpoints.MapPost(
            "/api/v1/master-data/imports/{batchId:guid}/simulate",
            async (Guid batchId,
                HttpContext httpContext,
                ITrustedRequestContextResolver resolver,
                MasterDataTenantContextResolver tenantResolver,
                FoundationAuditCoordinator auditCoordinator,
                LocalMasterDataIdempotencyStore idempotencyStore,
                MasterDataImportService service) => await ExecuteMutationAsync(
                    httpContext,
                    resolver,
                    tenantResolver,
                    auditCoordinator,
                    idempotencyStore,
                    FoundationOperationCatalog.GetRequired("master-data.import.simulate"),
                    $"batch:{batchId:D}",
                    context => service.SimulateAsync(context, batchId, httpContext.RequestAborted),
                    ToBatchResponse,
                    requireExpectedVersion: false))
            .WithName("master-data.import.simulate")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("master-data.import.simulate")));

        endpoints.MapPost(
            "/api/v1/master-data/imports/{batchId:guid}/execute",
            async (Guid batchId,
                HttpContext httpContext,
                ITrustedRequestContextResolver resolver,
                MasterDataTenantContextResolver tenantResolver,
                FoundationAuditCoordinator auditCoordinator,
                LocalMasterDataIdempotencyStore idempotencyStore,
                MasterDataImportService service) =>
            {
                if (!TryReadExpectedVersion(httpContext, out var expectedVersion))
                {
                    return await WriteProblemAsync(httpContext, 400, "validation_failed", "Validation failed", "A valid If-Match version is required.", "master-data.import.execute");
                }

                return await ExecuteMutationAsync(
                    httpContext,
                    resolver,
                    tenantResolver,
                    auditCoordinator,
                    idempotencyStore,
                    FoundationOperationCatalog.GetRequired("master-data.import.execute"),
                    $"batch:{batchId:D}|version:{Convert.ToBase64String(expectedVersion)}",
                    context => service.ExecuteAsync(context, batchId, expectedVersion, httpContext.RequestAborted),
                    ToBatchResponse,
                    requireExpectedVersion: true);
            })
            .WithName("master-data.import.execute")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("master-data.import.execute")));

        endpoints.MapGet(
            "/api/v1/master-data/imports",
            async (HttpContext httpContext,
                ITrustedRequestContextResolver resolver,
                MasterDataTenantContextResolver tenantResolver,
                MasterDataImportService service) => await ExecuteReadAsync(
                    httpContext,
                    resolver,
                    tenantResolver,
                    FoundationOperationCatalog.GetRequired("master-data.import.list"),
                    context => service.ListBatchesAsync(context, httpContext.RequestAborted),
                    items => items.Select(ToBatchResponse).ToArray()))
            .WithName("master-data.import.list")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("master-data.import.list")));

        endpoints.MapGet(
            "/api/v1/master-data/imports/{batchId:guid}",
            async (Guid batchId,
                HttpContext httpContext,
                ITrustedRequestContextResolver resolver,
                MasterDataTenantContextResolver tenantResolver,
                MasterDataImportService service) => await ExecuteReadAsync(
                    httpContext,
                    resolver,
                    tenantResolver,
                    FoundationOperationCatalog.GetRequired("master-data.import.read"),
                    context => service.ReadBatchAsync(context, batchId, httpContext.RequestAborted),
                    ToBatchResponse))
            .WithName("master-data.import.read")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("master-data.import.read")));

        endpoints.MapGet(
            "/api/v1/master-data/imports/{batchId:guid}/status",
            async (Guid batchId,
                HttpContext httpContext,
                ITrustedRequestContextResolver resolver,
                MasterDataTenantContextResolver tenantResolver,
                MasterDataImportService service) => await ExecuteReadAsync(
                    httpContext,
                    resolver,
                    tenantResolver,
                    FoundationOperationCatalog.GetRequired("master-data.import.status.read"),
                    context => service.ReadBatchAsync(context, batchId, httpContext.RequestAborted),
                    batch => new { batch.Id, batch.Status, batch.CorrelationId, batch.Version }))
            .WithName("master-data.import.status.read")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("master-data.import.status.read")));

        endpoints.MapGet(
            "/api/v1/master-data/imports/{batchId:guid}/rows",
            async (Guid batchId,
                HttpContext httpContext,
                ITrustedRequestContextResolver resolver,
                MasterDataTenantContextResolver tenantResolver,
                MasterDataImportService service) => await ExecuteReadAsync(
                    httpContext,
                    resolver,
                    tenantResolver,
                    FoundationOperationCatalog.GetRequired("master-data.import.rows.read"),
                    context => service.ReadRowsAsync(context, batchId, httpContext.RequestAborted),
                    items => items.Select(ToRowResponse).ToArray()))
            .WithName("master-data.import.rows.read")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("master-data.import.rows.read")));

        endpoints.MapGet(
            "/api/v1/master-data/imports/{batchId:guid}/reconciliation",
            async (Guid batchId,
                HttpContext httpContext,
                ITrustedRequestContextResolver resolver,
                MasterDataTenantContextResolver tenantResolver,
                MasterDataImportService service) => await ExecuteReadAsync(
                    httpContext,
                    resolver,
                    tenantResolver,
                    FoundationOperationCatalog.GetRequired("master-data.import.reconciliation.read"),
                    context => service.ReadReconciliationAsync(context, batchId, httpContext.RequestAborted),
                    ToReconciliationResponse))
            .WithName("master-data.import.reconciliation.read")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("master-data.import.reconciliation.read")));

        endpoints.MapGet(
            "/api/v1/master-data/imports/{batchId:guid}/audit",
            async (Guid batchId,
                HttpContext httpContext,
                ITrustedRequestContextResolver resolver,
                MasterDataTenantContextResolver tenantResolver,
                MasterDataImportService service) => await ExecuteReadAsync(
                    httpContext,
                    resolver,
                    tenantResolver,
                    FoundationOperationCatalog.GetRequired("master-data.import.audit.read"),
                    context => service.ReadAuditAsync(context, batchId, httpContext.RequestAborted),
                    items => items.Select(ToAuditResponse).ToArray()))
            .WithName("master-data.import.audit.read")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("master-data.import.audit.read")));

        endpoints.MapGet(
            "/api/v1/master-data/imports/{batchId:guid}/evidence",
            async (Guid batchId,
                HttpContext httpContext,
                ITrustedRequestContextResolver resolver,
                MasterDataTenantContextResolver tenantResolver,
                MasterDataImportService service) => await ExecuteReadAsync(
                    httpContext,
                    resolver,
                    tenantResolver,
                    FoundationOperationCatalog.GetRequired("master-data.import.evidence.read"),
                    context => service.ReadEvidenceAsync(context, batchId, httpContext.RequestAborted),
                    evidence => new
                    {
                        Batch = ToBatchResponse(evidence.Batch),
                        Rows = evidence.Rows.Select(ToRowResponse).ToArray(),
                        Audit = evidence.Audit.Select(ToAuditResponse).ToArray(),
                        Reconciliation = ToReconciliationResponse(evidence.Reconciliation)
                    }))
            .WithName("master-data.import.evidence.read")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("master-data.import.evidence.read")));

        endpoints.MapPost(
            "/api/v1/master-data/imports/{batchId:guid}/rows/{rowId:guid}/replay",
            async (Guid batchId,
                Guid rowId,
                MasterDataImportReplayRequest? request,
                HttpContext httpContext,
                ITrustedRequestContextResolver resolver,
                MasterDataTenantContextResolver tenantResolver,
                FoundationAuditCoordinator auditCoordinator,
                LocalMasterDataIdempotencyStore idempotencyStore,
                MasterDataImportService service) =>
            {
                if (request is null)
                {
                    return await WriteProblemAsync(httpContext, 400, "validation_failed", "Validation failed", "Corrected replay fields are required.", "master-data.import.replay");
                }

                if (!TryReadExpectedVersion(httpContext, out var expectedVersion))
                {
                    return await WriteProblemAsync(httpContext, 400, "validation_failed", "Validation failed", "A valid If-Match version is required.", "master-data.import.replay");
                }

                return await ExecuteMutationAsync(
                    httpContext,
                    resolver,
                    tenantResolver,
                    auditCoordinator,
                    idempotencyStore,
                    FoundationOperationCatalog.GetRequired("master-data.import.replay"),
                    JsonSerializer.Serialize(request) + $"|batch:{batchId:D}|row:{rowId:D}|version:{Convert.ToBase64String(expectedVersion)}",
                    context => service.ReplayQuarantinedRowAsync(context, batchId, rowId, request, IdempotencyKey(httpContext), expectedVersion, httpContext.RequestAborted),
                    ToBatchResponse,
                    requireExpectedVersion: true);
            })
            .WithName("master-data.import.replay")
            .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("master-data.import.replay")));

        return endpoints;
    }

    private static async Task<IResult> ExecuteReadAsync<TDomain, TResponse>(
        HttpContext httpContext,
        ITrustedRequestContextResolver resolver,
        MasterDataTenantContextResolver tenantResolver,
        FoundationOperationDescriptor descriptor,
        Func<MasterDataRequestContext, Task<MasterDataImportOperationResult<TDomain>>> operation,
        Func<TDomain, TResponse> map)
    {
        var foundationContext = await resolver.ResolveAsync(httpContext, httpContext.RequestAborted);
        var resolution = tenantResolver.Resolve(foundationContext);
        if (!resolution.Allowed || resolution.Context is null)
        {
            return await WriteProblemAsync(
                httpContext,
                foundationContext.SecurityProfile == FoundationSecurityProfile.Anonymous ? 401 : 403,
                resolution.Code,
                foundationContext.SecurityProfile == FoundationSecurityProfile.Anonymous ? "Authentication required" : "Access denied",
                "The operation is not available for this security context.",
                descriptor.OperationId);
        }

        var result = await operation(resolution.Context);
        return result.Succeeded && result.Value is not null
            ? Results.Json(map(result.Value), statusCode: 200)
            : await WriteProblemAsync(httpContext, result.StatusCode, result.Code, result.StatusCode == 403 ? "Access denied" : "Import operation failed", "The import operation could not be completed.", descriptor.OperationId);
    }

    private static async Task<IResult> ExecuteMutationAsync<TDomain, TResponse>(
        HttpContext httpContext,
        ITrustedRequestContextResolver resolver,
        MasterDataTenantContextResolver tenantResolver,
        FoundationAuditCoordinator auditCoordinator,
        LocalMasterDataIdempotencyStore idempotencyStore,
        FoundationOperationDescriptor descriptor,
        string fingerprint,
        Func<MasterDataRequestContext, Task<MasterDataImportOperationResult<TDomain>>> operation,
        Func<TDomain, TResponse> map,
        bool requireExpectedVersion)
    {
        if (!await EnsureAntiforgeryAsync(httpContext))
        {
            return await WriteProblemAsync(httpContext, 403, "antiforgery_failed", "Antiforgery validation failed", "The request could not be validated.", descriptor.OperationId);
        }

        if (requireExpectedVersion && !TryReadExpectedVersion(httpContext, out _))
        {
            return await WriteProblemAsync(httpContext, 400, "validation_failed", "Validation failed", "A valid If-Match version is required.", descriptor.OperationId);
        }

        var key = IdempotencyKey(httpContext);
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
        if (check.Decision == LocalMasterDataIdempotencyDecision.Replay && check.Response is TResponse replay)
        {
            httpContext.Response.Headers["X-Idempotent-Replay"] = "true";
            return Results.Json(replay, statusCode: 200);
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
                operationVersion: httpContext.Request.Headers.IfMatch.FirstOrDefault(),
                cancellationToken: httpContext.RequestAborted);
            if (!execution.Succeeded || execution.Value is null)
            {
                return await WriteProblemAsync(httpContext, 503, execution.Code, "Operation unavailable", "The import operation could not be completed.", descriptor.OperationId);
            }

            var result = execution.Value;
            if (!result.Succeeded || result.Value is null)
            {
                return await WriteProblemAsync(httpContext, result.StatusCode, result.Code, result.StatusCode == 403 ? "Access denied" : "Import operation failed", "The import operation could not be completed.", descriptor.OperationId);
            }

            var response = map(result.Value);
            idempotencyStore.Commit(key!, binding, response!);
            committed = true;
            if (result.Value is MasterDataImportBatchRecord batch)
            {
                httpContext.Response.Headers.ETag = $"\"{Convert.ToBase64String(batch.Version)}\"";
            }

            return Results.Json(response, statusCode: 200);
        }
        finally
        {
            if (!committed)
            {
                idempotencyStore.Release(key!, binding);
            }
        }
    }

    private static MasterDataImportBatchResponse ToBatchResponse(MasterDataImportBatchRecord batch)
    {
        var reconciliation = batch.Reconciliation;
        return new(
            batch.Id,
            batch.TenantId.Value,
            batch.ResourceKind,
            new MasterDataImportSourceRequest(batch.Source.SourceSystemCategory, batch.Source.SourceFileReference, batch.Source.BatchReference),
            batch.DuplicatePolicy,
            batch.Mode,
            batch.Status,
            batch.SubmittedActorId,
            batch.CreatedAt,
            batch.StartedAt,
            batch.CompletedAt,
            batch.CorrelationId,
            batch.TotalRows,
            batch.AcceptedCount,
            batch.RejectedCount,
            batch.QuarantinedCount,
            batch.CommittedCount,
            batch.SkippedCount,
            batch.FailedCount,
            batch.IdempotencyKey,
            batch.Version,
            ToReconciliationResponse(reconciliation));
    }

    private static MasterDataImportRowResponse ToRowResponse(MasterDataImportRowRecord row) => new(
        row.Id,
        row.BatchId,
        row.OriginalRowNumber,
        row.ReplaySequence,
        row.IsCurrent,
        row.ResourceKind,
        row.SourceFields,
        row.NormalizedFields,
        row.Outcome,
        row.Diagnostics.Select(item => new MasterDataImportDiagnosticResponse(item.Code, item.Message, item.Field, item.Severity)).ToArray(),
        row.HighestSeverity,
        row.MutationDisposition,
        row.ResultingResourceId,
        row.ResultingResourceCode,
        row.ReplayOfRowId,
        row.OriginalRowId,
        row.ProcessedAt,
        row.Version);

    private static MasterDataImportReconciliationResponse ToReconciliationResponse(MasterDataImportReconciliation reconciliation) => new(
        reconciliation.TotalRows,
        reconciliation.Accepted,
        reconciliation.Rejected,
        reconciliation.Quarantined,
        reconciliation.Committed,
        reconciliation.Skipped,
        reconciliation.Failed,
        reconciliation.IsConsistent,
        MasterDataImportReconciliation.Formula);

    private static MasterDataImportAuditResponse ToAuditResponse(MasterDataImportAuditRecord audit) => new(
        audit.EvidenceId,
        audit.OccurredAt,
        audit.OperationId,
        audit.CorrelationId,
        audit.TenantId.Value,
        audit.ActorId,
        audit.BatchId,
        audit.RowId,
        audit.OriginalRowNumber,
        audit.ResourceKind,
        audit.Outcome,
        audit.SourceReference,
        audit.Detail);

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

    private static string? IdempotencyKey(HttpContext httpContext) =>
        httpContext.Request.Headers["Idempotency-Key"].FirstOrDefault();

    private static string GetCorrelation(HttpContext httpContext) =>
        httpContext.Items.TryGetValue(FoundationApiKeys.CorrelationItem, out var value) && value is string correlationId
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
