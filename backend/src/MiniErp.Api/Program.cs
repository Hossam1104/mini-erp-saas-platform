using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http.HttpResults;
using MiniErp.App.BuildingBlocks.Rest;
using MiniErp.App.Modules.Audit;
using MiniErp.App.Modules.Identity;
using MiniErp.App.Modules.Platform;
using MiniErp.Contracts.Modules.Audit;
using MiniErp.Contracts.Modules.Foundation;
using MiniErp.Contracts.Modules.Platform;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<IPlatformAdministrationModule>(_ => PlatformModuleRegistration.Create());
builder.Services.AddIdentityAuthorization();
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = FirstPartyCookieConfiguration.Scheme;
    options.DefaultSignInScheme = FirstPartyCookieConfiguration.Scheme;
    options.DefaultChallengeScheme = FirstPartyCookieConfiguration.Scheme;
})
.AddCookie(FirstPartyCookieConfiguration.Scheme, options =>
{
    var approved = FirstPartyCookieConfiguration.CreateForHost();
    options.Cookie = approved.Cookie;
    options.ExpireTimeSpan = approved.ExpireTimeSpan;
    options.SlidingExpiration = false;
    options.LoginPath = approved.LoginPath;
    options.AccessDeniedPath = approved.AccessDeniedPath;
    options.Events = new CookieAuthenticationEvents
    {
        OnValidatePrincipal = context =>
        {
            var host = context.HttpContext.RequestServices.GetRequiredService<IFoundationIdentityHost>();
            if (context.Principal is null || !host.ValidatePrincipal(context.Principal))
            {
                context.RejectPrincipal();
            }

            return Task.CompletedTask;
        }
    };
});
builder.Services.AddAuthorization();
builder.Services.AddAntiforgery(options =>
{
    options.HeaderName = "X-CSRF-TOKEN";
    options.Cookie.Name = "__Host-MiniErp.AntiForgery";
    options.Cookie.HttpOnly = false;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.Path = "/";
});
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

app.UseAuthentication();
app.UseAuthorization();
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
            var problem = await WriteProblemAsync(
                httpContext,
                StatusCodes.Status500InternalServerError,
                "internal_error",
                "Internal error",
                "The operation could not be completed.",
                "platform.internal");
            await problem.ExecuteAsync(httpContext);
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

app.MapGet("/api/v1/auth/antiforgery", async (HttpContext httpContext, IAntiforgery antiforgery) =>
{
    var tokens = antiforgery.GetAndStoreTokens(httpContext);
    var requestToken = tokens.RequestToken ?? string.Empty;
    httpContext.Response.Headers["X-CSRF-TOKEN"] = requestToken;
    httpContext.Response.Headers.CacheControl = "no-store";
    await httpContext.Response.WriteAsJsonAsync(new { status = "issued" });
})
    .WithName("auth.antiforgery.read")
    .WithMetadata(new FoundationOperationMetadata("auth.antiforgery.read", FoundationSecurityProfile.Anonymous));

app.MapPost("/api/v1/auth/sign-in", async (
    FoundationSignInRequest? request,
    HttpContext httpContext,
    IFoundationIdentityHost identityHost) =>
{
    var result = identityHost.SignIn(request?.Login ?? string.Empty, request?.Password ?? string.Empty);
    if (!result.Succeeded || result.Principal is null)
    {
        return await WriteProblemAsync(
            httpContext,
            StatusCodes.Status401Unauthorized,
            "authentication_failed",
            "Authentication failed",
            "The supplied credentials could not be authenticated.",
            "auth.sign-in");
    }

    await httpContext.SignInAsync(FirstPartyCookieConfiguration.Scheme, result.Principal);
    return Results.Json(ToSessionResponse(result.State!), statusCode: StatusCodes.Status200OK);
})
    .WithName("auth.sign-in")
    .WithMetadata(new FoundationOperationMetadata("auth.sign-in", FoundationSecurityProfile.Anonymous, RequiresMandatoryAudit: false, IsUnsafe: true));

app.MapPost("/api/v1/auth/sign-out", async (
    HttpContext httpContext,
    ITrustedRequestContextResolver resolver,
    IFoundationIdentityHost identityHost,
    FoundationAuditCoordinator auditCoordinator) =>
{
    if (!await EnsureAntiforgeryAsync(httpContext))
    {
        return await WriteProblemAsync(httpContext, StatusCodes.Status403Forbidden, "antiforgery_failed", "Antiforgery validation failed", "The request could not be validated.", "auth.sign-out");
    }

    var context = await GetContext(httpContext, resolver);
    var principal = httpContext.User;
    if (identityHost.GetSession(principal) is not { Authenticated: true } state)
    {
        await httpContext.SignOutAsync(FirstPartyCookieConfiguration.Scheme);
        return await WriteProblemAsync(httpContext, StatusCodes.Status401Unauthorized, "authentication_failed", "Authentication required", "Authentication is required.", "auth.sign-out");
    }

    if (context.SecurityProfile is FoundationSecurityProfile.OrdinaryMembership
        or FoundationSecurityProfile.SupportGrant
        or FoundationSecurityProfile.PlatformGovernanceContext)
    {
        var execution = await auditCoordinator.ExecuteProtectedAsync(
            context,
            "auth.sign-out",
            GetCorrelation(httpContext),
            FoundationAuditReason.Allowed,
            () => Task.FromResult(identityHost.Revoke(principal, "sign-out")));
        if (!execution.Succeeded)
        {
            return await WriteProblemAsync(httpContext, StatusCodes.Status503ServiceUnavailable, "audit_unavailable", "Operation unavailable", "The operation could not be completed.", "auth.sign-out");
        }
    }
    else
    {
        // A session without a selected Tenant/platform path is still safely
        // revocable; it has no protected business effect to evidence.
        identityHost.Revoke(principal, "sign-out");
    }

    await httpContext.SignOutAsync(FirstPartyCookieConfiguration.Scheme);
    return Results.NoContent();
})
    .WithName("auth.sign-out")
    .WithMetadata(new FoundationOperationMetadata("auth.sign-out", FoundationSecurityProfile.AuthenticatedSession, RequiresMandatoryAudit: true, IsUnsafe: true));

app.MapGet("/api/v1/auth/session", (
    HttpContext httpContext,
    IFoundationIdentityHost identityHost) =>
{
    var state = identityHost.GetSession(httpContext.User);
    return state.Authenticated
        ? Results.Json(ToSessionResponse(state))
        : Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Authentication required", detail: "Authentication is required.", type: "https://api.minierp.local/problems/authentication_failed");
})
    .WithName("auth.session.read")
    .WithMetadata(new FoundationOperationMetadata("auth.session.read", FoundationSecurityProfile.AuthenticatedSession));

app.MapGet("/api/v1/auth/contexts", (
    HttpContext httpContext,
    IFoundationIdentityHost identityHost) =>
{
    var state = identityHost.GetSession(httpContext.User);
    if (!state.Authenticated)
    {
        return Results.Problem(statusCode: StatusCodes.Status401Unauthorized, title: "Authentication required", detail: "Authentication is required.", type: "https://api.minierp.local/problems/authentication_failed");
    }

    return Results.Json(new FoundationContextsResponse(
        identityHost.ListContexts(httpContext.User).Select(ToContextResponse).ToArray()));
})
    .WithName("auth.contexts.read")
    .WithMetadata(new FoundationOperationMetadata("auth.contexts.read", FoundationSecurityProfile.AuthenticatedSession));

app.MapPost("/api/v1/auth/context-switch", async (
    FoundationContextSwitchRequest? request,
    HttpContext httpContext,
    ITrustedRequestContextResolver resolver,
    IFoundationIdentityHost identityHost,
    LocalFoundationIdempotencyStore idempotencyStore,
    FoundationAuditCoordinator auditCoordinator) =>
{
    const string operationId = "auth.context-switch";
    if (!await EnsureAntiforgeryAsync(httpContext))
    {
        return await WriteProblemAsync(httpContext, StatusCodes.Status403Forbidden, "antiforgery_failed", "Antiforgery validation failed", "The request could not be validated.", operationId);
    }

    var state = identityHost.GetSession(httpContext.User);
    if (!state.Authenticated || state.ActorId is null || state.SessionId is null)
    {
        return await WriteProblemAsync(httpContext, StatusCodes.Status401Unauthorized, "authentication_failed", "Authentication required", "Authentication is required.", operationId);
    }

    if (request is null || request.ContextId == Guid.Empty || request.ExpectedVersion < 0)
    {
        return await WriteProblemAsync(httpContext, StatusCodes.Status400BadRequest, "validation_failed", "Validation failed", "The request is invalid.", operationId);
    }

    var candidate = identityHost.ListContexts(httpContext.User).SingleOrDefault(item => item.ContextId == request.ContextId);
    if (candidate is null)
    {
        return await WriteProblemAsync(httpContext, StatusCodes.Status403Forbidden, "access_denied", "Access denied", "The requested context is not available.", operationId);
    }

    var binding = new FoundationIdempotencyBinding(
        state.ActorId.Value,
        candidate.TenantId,
        candidate.Kind switch
        {
            FoundationHostContextKind.OrdinaryMembership => FoundationSecurityProfile.OrdinaryMembership,
            FoundationHostContextKind.SupportGrant => FoundationSecurityProfile.SupportGrant,
            _ => FoundationSecurityProfile.PlatformGovernanceContext
        },
        operationId);
    var key = httpContext.Request.Headers["Idempotency-Key"].FirstOrDefault();
    if (!FoundationCorrelation.IsValid(key))
    {
        return await WriteProblemAsync(httpContext, StatusCodes.Status400BadRequest, "idempotency_key_invalid", "Invalid idempotency key", "The request is invalid.", operationId);
    }

    var fingerprint = $"{request.ContextId:N}|v{request.ExpectedVersion}";
    var check = idempotencyStore.Begin(key!, binding, fingerprint, DateTimeOffset.UtcNow, TimeSpan.FromMinutes(10));
    if (check.Decision == FoundationIdempotencyDecision.Replay && check.Response is not null)
    {
        return Results.Json(ToSessionResponse(identityHost.GetSession(httpContext.User), replayed: true));
    }

    if (check.Decision is FoundationIdempotencyDecision.RequestConflict or FoundationIdempotencyDecision.InProgress)
    {
        return await WriteProblemAsync(httpContext, StatusCodes.Status409Conflict, "idempotency_conflict", "Idempotency conflict", "The request cannot be replayed with different or incomplete input.", operationId);
    }

    var candidateContext = identityHost.ResolveCandidateContext(httpContext.User, request.ContextId, GetCorrelation(httpContext));
    if (candidateContext is null)
    {
        idempotencyStore.Release(key!, binding);
        return await WriteProblemAsync(httpContext, StatusCodes.Status403Forbidden, "access_denied", "Access denied", "The requested context is not available.", operationId);
    }

    var execution = await auditCoordinator.ExecuteProtectedAsync(
        candidateContext,
        operationId,
        GetCorrelation(httpContext),
        FoundationAuditReason.Allowed,
        () => Task.FromResult(identityHost.SwitchContext(httpContext.User, request.ContextId, request.ExpectedVersion)),
        key,
        request.ExpectedVersion.ToString(System.Globalization.CultureInfo.InvariantCulture));
    if (!execution.Succeeded || execution.Value is not { Succeeded: true })
    {
        idempotencyStore.Release(key!, binding);
        return await WriteProblemAsync(httpContext, StatusCodes.Status409Conflict, "context_version_conflict", "Context selection conflict", "The context selection is stale or unavailable.", operationId);
    }

    var switched = execution.Value.State ?? identityHost.GetSession(httpContext.User);
    idempotencyStore.Commit(key!, binding, new FoundationWriteResponse(operationId, GetCorrelation(httpContext), "context_selected", switched.SelectedContext?.Version.ToString() ?? "0", false));
    return Results.Json(ToSessionResponse(switched));
})
    .WithName("auth.context-switch")
    .WithMetadata(new FoundationOperationMetadata("auth.context-switch", FoundationSecurityProfile.AuthenticatedSession, RequiresMandatoryAudit: true, IsUnsafe: true));

app.MapGet("/api/v1/foundation/tenant-context", async (
        HttpContext httpContext,
        ITrustedRequestContextResolver resolver,
        FoundationRestApplication application) =>
    await ExecuteAsync(
        httpContext,
        resolver,
        context => Task.FromResult(application.ReadTenantContext(context, GetCorrelation(httpContext))),
        StatusCodes.Status200OK))
    .WithName("foundation.tenant-context.read")
    .WithMetadata(new FoundationOperationMetadata("foundation.tenant-context.read", FoundationSecurityProfile.OrdinaryMembership));

app.MapGet("/api/v1/foundation/support-context", async (
        HttpContext httpContext,
        ITrustedRequestContextResolver resolver,
        FoundationRestApplication application) =>
    await ExecuteAsync(
        httpContext,
        resolver,
        context => Task.FromResult(application.ReadSupportContext(context, GetCorrelation(httpContext))),
        StatusCodes.Status200OK))
    .WithName("foundation.support-context.read")
    .WithMetadata(new FoundationOperationMetadata("foundation.support-context.read", FoundationSecurityProfile.SupportGrant));

app.MapGet("/api/v1/foundation/platform-context", async (
        HttpContext httpContext,
        ITrustedRequestContextResolver resolver,
        FoundationRestApplication application) =>
    await ExecuteAsync(
        httpContext,
        resolver,
        context => Task.FromResult(application.ReadPlatformContext(context, GetCorrelation(httpContext))),
        StatusCodes.Status200OK))
    .WithName("foundation.platform-context.read")
    .WithMetadata(new FoundationOperationMetadata("foundation.platform-context.read", FoundationSecurityProfile.PlatformGovernanceContext));

app.MapGet("/api/v1/foundation/targets/{targetId}", async (
        string targetId,
        HttpContext httpContext,
        ITrustedRequestContextResolver resolver,
        FoundationRestApplication application) =>
    {
        if (!Guid.TryParse(targetId, out var parsedTargetId))
        {
            return await WriteProblemAsync(httpContext, StatusCodes.Status400BadRequest, "validation_failed", "Validation failed", "The request is invalid.", "foundation.target.read");
        }

        return await ExecuteAsync(
            httpContext,
            resolver,
            context => Task.FromResult(application.ReadTarget(context, parsedTargetId, GetCorrelation(httpContext))),
            StatusCodes.Status200OK);
    })
    .WithName("foundation.target.read")
    .WithMetadata(new FoundationOperationMetadata("foundation.target.read", FoundationSecurityProfile.OrdinaryMembership));

app.MapPost("/api/v1/foundation/probe", async (
        FoundationWriteRequest request,
        HttpContext httpContext,
        ITrustedRequestContextResolver resolver,
        FoundationRestApplication application) =>
    {
        if (!await EnsureAntiforgeryAsync(httpContext))
        {
            return (IResult)await WriteProblemAsync(httpContext, StatusCodes.Status403Forbidden, "antiforgery_failed", "Antiforgery validation failed", "The request could not be validated.", "foundation.probe.write");
        }

        return (IResult)await ExecuteAsync<FoundationWriteResponse>(
            httpContext,
            resolver,
            context => application.WriteProbeAsync(
                context,
                request,
                httpContext.Request.Headers["Idempotency-Key"].FirstOrDefault(),
                httpContext.Request.Headers["If-Match"].FirstOrDefault(),
                antiforgeryValidated: true,
                GetCorrelation(httpContext)),
            StatusCodes.Status200OK);
    })
    .WithName("foundation.probe.write")
    .WithMetadata(new FoundationOperationMetadata("foundation.probe.write", FoundationSecurityProfile.OrdinaryMembership, RequiresMandatoryAudit: true, IsUnsafe: true));

app.MapOpenApi("/openapi/v1.json")
    .WithName("platform.openapi")
    .WithMetadata(new FoundationOperationMetadata("platform.openapi", FoundationSecurityProfile.Anonymous));

app.Run();

static string GetCorrelation(HttpContext httpContext) =>
    httpContext.Items.TryGetValue(FoundationApiKeys.CorrelationItem, out var value) && value is string correlationId
        ? correlationId
        : FoundationCorrelation.Resolve(httpContext.Request);

static async Task<FoundationRequestContext> GetContext(HttpContext httpContext, ITrustedRequestContextResolver resolver) =>
    await resolver.ResolveAsync(httpContext, httpContext.RequestAborted);

static async Task<bool> EnsureAntiforgeryAsync(HttpContext httpContext)
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

static async Task<IResult> ExecuteAsync<T>(HttpContext httpContext, ITrustedRequestContextResolver resolver, Func<FoundationRequestContext, Task<FoundationOperationResult<T>>> operation, int successStatus)
{
    var context = await resolver.ResolveAsync(httpContext, httpContext.RequestAborted);
    var metadata = httpContext.GetEndpoint()?.Metadata.GetMetadata<FoundationOperationMetadata>();
    if (metadata is null || !MatchesSecurityProfile(metadata.SecurityProfile, context.SecurityProfile))
    {
        var code = context.SecurityProfile == FoundationSecurityProfile.Anonymous
            ? "authentication_failed"
            : "access_denied";
        var title = context.SecurityProfile == FoundationSecurityProfile.Anonymous
            ? "Authentication required"
            : "Access denied";
        return await WriteProblemAsync(httpContext, context.SecurityProfile == FoundationSecurityProfile.Anonymous ? StatusCodes.Status401Unauthorized : StatusCodes.Status403Forbidden, code, title, "The operation is not available for this security context.", metadata?.OperationId ?? "platform.internal");
    }

    var result = await operation(context);
    return ToResult(httpContext, result, successStatus);
}

static bool MatchesSecurityProfile(FoundationSecurityProfile declared, FoundationSecurityProfile enforced) =>
    declared switch
    {
        FoundationSecurityProfile.Anonymous => enforced == FoundationSecurityProfile.Anonymous,
        FoundationSecurityProfile.AuthenticatedSession => enforced is FoundationSecurityProfile.AuthenticatedSession
            or FoundationSecurityProfile.OrdinaryMembership
            or FoundationSecurityProfile.SupportGrant
            or FoundationSecurityProfile.PlatformGovernanceContext,
        _ => declared == enforced
    };

static IResult ToResult<T>(HttpContext httpContext, FoundationOperationResult<T> result, int successStatus)
{
    if (result.Succeeded)
    {
        if (result.Value is FoundationWriteResponse write)
        {
            httpContext.Response.Headers.ETag = $"\"{write.Version}\"";
        }

        return Results.Json(result.Value, statusCode: successStatus);
    }

    return Results.Problem(statusCode: result.StatusCode, title: result.Title, detail: result.Detail, type: $"https://api.minierp.local/problems/{result.Code}", extensions: new Dictionary<string, object?>
    {
        ["code"] = result.Code,
        ["correlationId"] = result.CorrelationId,
        ["operationId"] = result.OperationId
    });
}

static Task<IResult> WriteProblemAsync(HttpContext httpContext, int statusCode, string code, string title, string detail, string operationId)
{
    var correlationId = GetCorrelation(httpContext);
    return Task.FromResult<IResult>(Results.Problem(statusCode: statusCode, title: title, detail: detail, type: $"https://api.minierp.local/problems/{code}", extensions: new Dictionary<string, object?>
    {
        ["code"] = code,
        ["correlationId"] = correlationId,
        ["operationId"] = operationId
    }));
}

static FoundationSessionResponse ToSessionResponse(FoundationHostSessionState state, bool replayed = false) =>
    new(
        state.Authenticated,
        state.ActorId,
        state.SessionId,
        state.LifecycleState,
        state.AbsoluteExpiresAt,
        state.SelectedContext?.Kind.ToString(),
        state.SelectedContext?.TenantId,
        state.SelectedContext?.ContextId,
        state.SelectedContext?.Version ?? 0);

static FoundationContextCandidateResponse ToContextResponse(FoundationHostContextCandidate candidate) =>
    new(candidate.ContextId, candidate.Kind.ToString(), candidate.TenantId, candidate.DisplayName, candidate.Version);

/// <summary>Entry point exposed for API integration tests.</summary>
public partial class Program;

internal static class FoundationApiKeys
{
    internal const string CorrelationItem = "MiniErp.Foundation.CorrelationId";
}
