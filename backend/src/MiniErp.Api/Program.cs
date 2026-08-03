using MiniErp.App.BuildingBlocks.Rest;
using MiniErp.App.Modules.Audit;
using MiniErp.App.Modules.Platform;
using MiniErp.Contracts.Modules.Foundation;
using MiniErp.Contracts.Modules.Platform;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<IPlatformAdministrationModule>(_ => PlatformModuleRegistration.Create());
builder.Services.AddSingleton<ITrustedRequestContextResolver, DefaultTrustedRequestContextResolver>();
builder.Services.AddSingleton<LocalFoundationIdempotencyStore>();
builder.Services.AddSingleton<LocalFoundationProbeStore>();
builder.Services.AddSingleton<IFoundationTargetDirectory, EmptyFoundationTargetDirectory>();
builder.Services.AddSingleton<LocalImmutableAuditEvidenceStore>();
builder.Services.AddSingleton<IFoundationAuditEvidenceSink>(services =>
    services.GetRequiredService<LocalImmutableAuditEvidenceStore>());
builder.Services.AddSingleton<IFoundationAuditEvidenceReader>(services =>
    services.GetRequiredService<LocalImmutableAuditEvidenceStore>());
builder.Services.AddSingleton<LocalFoundationAuditTelemetrySink>();
builder.Services.AddSingleton<IFoundationAuditTelemetrySink>(services =>
    services.GetRequiredService<LocalFoundationAuditTelemetrySink>());
builder.Services.AddSingleton<LocalFoundationAuditOperationalSignalSink>();
builder.Services.AddSingleton<IFoundationAuditOperationalSignalSink>(services =>
    services.GetRequiredService<LocalFoundationAuditOperationalSignalSink>());
builder.Services.AddSingleton<FoundationAuditCoordinator>();
builder.Services.AddSingleton<FoundationRestApplication>();
builder.Services.AddOpenApi("v1");

var app = builder.Build();

app.Use(async (httpContext, next) =>
{
    var correlationId = FoundationCorrelation.Resolve(httpContext.Request);
    httpContext.Items[FoundationApiKeys.CorrelationItem] = correlationId;
    httpContext.Response.OnStarting(() =>
    {
        httpContext.Response.Headers[FoundationCorrelation.HeaderName] = correlationId;
        return Task.CompletedTask;
    });

    try
    {
        await next();
    }
    catch (Exception)
    {
        if (!httpContext.Response.HasStarted)
        {
            await WriteProblemAsync(
                httpContext,
                StatusCodes.Status500InternalServerError,
                "internal_error",
                "Internal error",
                "The operation could not be completed.",
                "platform.internal");
        }
    }
});

app.MapGet("/health", () => Results.Ok(new { status = "ok" }))
    .WithName("platform.health")
    .WithMetadata(new FoundationOperationMetadata("platform.health", FoundationSecurityProfile.Anonymous));

app.MapGet("/api/v1/module-registration", (IPlatformAdministrationModule platformModule) =>
    Results.Ok(new
    {
        module = platformModule.Descriptor.Key,
        name = platformModule.Descriptor.Name,
        boundary = platformModule.Descriptor.Boundary,
        registered = platformModule.RegistrationEvidence.IsRegistered
    }))
    .WithName("platform.module-registration")
    .WithMetadata(new FoundationOperationMetadata("platform.module-registration", FoundationSecurityProfile.Anonymous));

app.MapGet("/api/v1/foundation/tenant-context", async (
        HttpContext httpContext,
        ITrustedRequestContextResolver resolver,
        FoundationRestApplication application) =>
    await ExecuteAsync(
        httpContext,
        resolver,
        async () => application.ReadTenantContext(
            await GetContext(httpContext, resolver),
            GetCorrelation(httpContext)),
        StatusCodes.Status200OK))
    .WithName("foundation.tenant-context.read")
    .WithMetadata(new FoundationOperationMetadata(
        "foundation.tenant-context.read",
        FoundationSecurityProfile.OrdinaryMembership));

app.MapGet("/api/v1/foundation/support-context", async (
        HttpContext httpContext,
        ITrustedRequestContextResolver resolver,
        FoundationRestApplication application) =>
    await ExecuteAsync(
        httpContext,
        resolver,
        async () => application.ReadSupportContext(
            await GetContext(httpContext, resolver),
            GetCorrelation(httpContext)),
        StatusCodes.Status200OK))
    .WithName("foundation.support-context.read")
    .WithMetadata(new FoundationOperationMetadata(
        "foundation.support-context.read",
        FoundationSecurityProfile.SupportGrant));

app.MapGet("/api/v1/foundation/platform-context", async (
        HttpContext httpContext,
        ITrustedRequestContextResolver resolver,
        FoundationRestApplication application) =>
    await ExecuteAsync(
        httpContext,
        resolver,
        async () => application.ReadPlatformContext(
            await GetContext(httpContext, resolver),
            GetCorrelation(httpContext)),
        StatusCodes.Status200OK))
    .WithName("foundation.platform-context.read")
    .WithMetadata(new FoundationOperationMetadata(
        "foundation.platform-context.read",
        FoundationSecurityProfile.PlatformGovernanceContext));

app.MapGet("/api/v1/foundation/targets/{targetId}", async (
        string targetId,
        HttpContext httpContext,
        ITrustedRequestContextResolver resolver,
        FoundationRestApplication application) =>
    {
        if (!Guid.TryParse(targetId, out var parsedTargetId))
        {
            return await WriteProblemAsync(
                httpContext,
                StatusCodes.Status400BadRequest,
                "validation_failed",
                "Validation failed",
                "The request is invalid.",
                "foundation.target.read");
        }

        return await ExecuteAsync(
            httpContext,
            resolver,
            async () => application.ReadTarget(
                await GetContext(httpContext, resolver),
                parsedTargetId,
                GetCorrelation(httpContext)),
            StatusCodes.Status200OK);
    })
    .WithName("foundation.target.read")
    .WithMetadata(new FoundationOperationMetadata(
        "foundation.target.read",
        FoundationSecurityProfile.OrdinaryMembership));

app.MapPost("/api/v1/foundation/probe", async (
        FoundationWriteRequest request,
        HttpContext httpContext,
        ITrustedRequestContextResolver resolver,
        FoundationRestApplication application) =>
    await ExecuteAsync(
        httpContext,
        resolver,
        async () => application.WriteProbe(
            await GetContext(httpContext, resolver),
            request,
            httpContext.Request.Headers["Idempotency-Key"].FirstOrDefault(),
            httpContext.Request.Headers["If-Match"].FirstOrDefault(),
            IsAntiforgeryValid(httpContext),
            GetCorrelation(httpContext)),
        StatusCodes.Status200OK))
    .WithName("foundation.probe.write")
    .WithMetadata(new FoundationOperationMetadata(
        "foundation.probe.write",
        FoundationSecurityProfile.OrdinaryMembership));

app.MapOpenApi("/openapi/v1.json")
    .WithName("platform.openapi")
    .WithMetadata(new FoundationOperationMetadata("platform.openapi", FoundationSecurityProfile.Anonymous));

app.Run();

static string GetCorrelation(HttpContext httpContext) =>
    httpContext.Items.TryGetValue(FoundationApiKeys.CorrelationItem, out var value) && value is string correlationId
        ? correlationId
        : FoundationCorrelation.Resolve(httpContext.Request);

static async Task<FoundationRequestContext> GetContext(
    HttpContext httpContext,
    ITrustedRequestContextResolver resolver) =>
    await resolver.ResolveAsync(httpContext, httpContext.RequestAborted);

static bool IsAntiforgeryValid(HttpContext httpContext)
{
    var token = httpContext.Request.Headers["X-CSRF-TOKEN"].FirstOrDefault();
    return FoundationCorrelation.IsValid(token) && string.Equals(token, "foundation-test-token", StringComparison.Ordinal);
}

static async Task<IResult> ExecuteAsync<T>(
    HttpContext httpContext,
    ITrustedRequestContextResolver resolver,
    Func<Task<FoundationOperationResult<T>>> operation,
    int successStatus)
{
    var result = await operation();
    return ToResult(httpContext, result, successStatus);
}

static IResult ToResult<T>(
    HttpContext httpContext,
    FoundationOperationResult<T> result,
    int successStatus)
{
    if (result.Succeeded)
    {
        if (result.Value is FoundationWriteResponse write)
        {
            httpContext.Response.Headers["ETag"] = $"\"{write.Version}\"";
        }
        return Results.Json(result.Value, statusCode: successStatus);
    }

    return Results.Problem(
        statusCode: result.StatusCode,
        title: result.Title,
        detail: result.Detail,
        type: $"https://api.minierp.local/problems/{result.Code}",
        extensions: new Dictionary<string, object?>
        {
            ["code"] = result.Code,
            ["correlationId"] = result.CorrelationId,
            ["operationId"] = result.OperationId
        });
}

static Task<IResult> WriteProblemAsync(
    HttpContext httpContext,
    int statusCode,
    string code,
    string title,
    string detail,
    string operationId)
{
    var correlationId = GetCorrelation(httpContext);
    return Task.FromResult<IResult>(Results.Problem(
        statusCode: statusCode,
        title: title,
        detail: detail,
        type: $"https://api.minierp.local/problems/{code}",
        extensions: new Dictionary<string, object?>
        {
            ["code"] = code,
            ["correlationId"] = correlationId,
            ["operationId"] = operationId
        }));
}

/// <summary>Entry point exposed for API integration tests.</summary>
public partial class Program;

internal static class FoundationApiKeys
{
    internal const string CorrelationItem = "MiniErp.Foundation.CorrelationId";
}
