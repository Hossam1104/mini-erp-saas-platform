using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http.HttpResults;
using Scalar.AspNetCore;
using MiniErp.App.BuildingBlocks.Rest;
using MiniErp.App.Modules.Audit;
using MiniErp.App.Modules.BusinessParties;
using MiniErp.App.Modules.Identity;
using MiniErp.App.Modules.MasterData;
using MiniErp.App.Modules.Platform;
using MiniErp.App.Modules.Procurement;
using MiniErp.Contracts.Modules.Audit;
using MiniErp.Contracts.Modules.BusinessParties;
using MiniErp.Contracts.Modules.Foundation;
using MiniErp.Contracts.Modules.Platform;
using MiniErp.Contracts.Modules.MasterData;
using MiniErp.Infrastructure.Persistence.Modules.MasterData;
using MiniErp.Infrastructure.Persistence.Modules.BusinessParties;
using MiniErp.Infrastructure.Persistence.Modules.Procurement;
using MiniErp.Api;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddSingleton<IPlatformAdministrationModule>(_ => PlatformModuleRegistration.Create());
builder.Services.AddSingleton<IMasterDataCatalogModule>(_ => MasterDataModuleRegistration.Create());
builder.Services.AddSingleton<IBusinessPartiesModule>(_ => BusinessPartiesModuleRegistration.Create());
builder.Services.AddMasterDataAuthorization();
builder.Services.AddProductIdentity();
builder.Services.AddSupplierIdentity();
builder.Services.AddCustomerIdentity();
builder.Services.AddMasterDataImport();
builder.Services.AddPurchaseRequestApprovalFoundation();
string? developmentMasterDataSqliteConnectionString = null;
string? developmentBusinessPartiesSqliteConnectionString = null;
string? developmentProcurementSqliteConnectionString = null;
var sqlServerConnectionString = builder.Configuration["MESP_SQLSERVER_CONNECTION_STRING"];
if (!string.IsNullOrWhiteSpace(sqlServerConnectionString))
{
    builder.Services.AddMasterDataSqlServerPersistence(sqlServerConnectionString);
    builder.Services.AddBusinessPartiesSqlServerPersistence(sqlServerConnectionString);
    builder.Services.AddProcurementSqlServerPersistence(sqlServerConnectionString);
}
else if (builder.Environment.IsDevelopment())
{
    var configuredMasterDataConnectionString = builder.Configuration["MESP_DEV_MASTERDATA_SQLITE_CONNECTION_STRING"];
    var configuredBusinessPartiesConnectionString = builder.Configuration["MESP_DEV_BUSINESS_PARTIES_SQLITE_CONNECTION_STRING"];
    var configuredProcurementConnectionString = builder.Configuration["MESP_DEV_PROCUREMENT_SQLITE_CONNECTION_STRING"];
    var configuredSqliteDirectory = builder.Configuration["MESP_DEV_SQLITE_DIRECTORY"];
    var localApplicationData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
    var defaultSqliteDirectory = string.IsNullOrWhiteSpace(configuredSqliteDirectory)
        ? Path.Combine(
            string.IsNullOrWhiteSpace(localApplicationData) ? Path.GetTempPath() : localApplicationData,
            "MiniErp",
            "Development")
        : configuredSqliteDirectory;

    if (string.IsNullOrWhiteSpace(configuredMasterDataConnectionString)
        || string.IsNullOrWhiteSpace(configuredBusinessPartiesConnectionString)
        || string.IsNullOrWhiteSpace(configuredProcurementConnectionString))
    {
        Directory.CreateDirectory(defaultSqliteDirectory);
    }

    developmentMasterDataSqliteConnectionString = string.IsNullOrWhiteSpace(configuredMasterDataConnectionString)
        ? $"Data Source={Path.Combine(defaultSqliteDirectory, "masterdata.db")}"
        : configuredMasterDataConnectionString;
    developmentBusinessPartiesSqliteConnectionString = string.IsNullOrWhiteSpace(configuredBusinessPartiesConnectionString)
        ? $"Data Source={Path.Combine(defaultSqliteDirectory, "business-parties.db")}"
        : configuredBusinessPartiesConnectionString;
    developmentProcurementSqliteConnectionString = string.IsNullOrWhiteSpace(configuredProcurementConnectionString)
        ? $"Data Source={Path.Combine(defaultSqliteDirectory, "procurement.db")}"
        : configuredProcurementConnectionString;

    builder.Services.AddMasterDataSqlitePersistence(developmentMasterDataSqliteConnectionString);
    builder.Services.AddBusinessPartiesSqlitePersistence(developmentBusinessPartiesSqliteConnectionString);
    builder.Services.AddProcurementSqlitePersistence(developmentProcurementSqliteConnectionString);
}

if (builder.Environment.IsDevelopment()
    && string.Equals(builder.Configuration["MESP_DEV_BOOTSTRAP_ENABLED"], "true", StringComparison.OrdinalIgnoreCase))
{
    // Development-only default organization scope so the Purchase Request UI has at least one
    // selectable Company/Branch option before a real Company/Branch module exists. This is a
    // configuration-led fixture, not production policy: it is fully replaced by supplying
    // MESP_DEV_ORG_SCOPE_* environment variables, and a future Company/Branch module can replace
    // this provider registration entirely without touching Purchase Request code or the API contract.
    var configuredOrgScopeTenantId = builder.Configuration["MESP_DEV_ORG_SCOPE_TENANT_ID"];
    var configuredOrgScopeCompanyId = builder.Configuration["MESP_DEV_ORG_SCOPE_COMPANY_ID"];
    var configuredOrgScopeBranchId = builder.Configuration["MESP_DEV_ORG_SCOPE_BRANCH_ID"];
    var configuredOrgScopeCompanyName = builder.Configuration["MESP_DEV_ORG_SCOPE_COMPANY_NAME"];
    var configuredOrgScopeBranchName = builder.Configuration["MESP_DEV_ORG_SCOPE_BRANCH_NAME"];

    var developmentOrgScopeTenantId = Guid.TryParse(configuredOrgScopeTenantId, out var parsedOrgScopeTenantId)
        ? parsedOrgScopeTenantId
        : DevelopmentBootstrap.DevTenantId.Value;
    var developmentOrgScopeCompanyId = Guid.TryParse(configuredOrgScopeCompanyId, out var parsedOrgScopeCompanyId)
        ? parsedOrgScopeCompanyId
        : Guid.Parse("99999999-9999-9999-9999-999999999999");
    Guid? developmentOrgScopeBranchId = Guid.TryParse(configuredOrgScopeBranchId, out var parsedOrgScopeBranchId)
        ? parsedOrgScopeBranchId
        : null;
    var developmentOrgScopeCompanyName = string.IsNullOrWhiteSpace(configuredOrgScopeCompanyName)
        ? "Development Organization"
        : configuredOrgScopeCompanyName;
    var developmentOrgScopeBranchName = string.IsNullOrWhiteSpace(configuredOrgScopeBranchName)
        ? null
        : configuredOrgScopeBranchName;

    builder.Services.AddSingleton<IProcurementOrganizationScopeProvider>(_ =>
        new ConfiguredProcurementOrganizationScopeProvider(
        [
            new ProcurementOrganizationScopeOption(
                developmentOrgScopeTenantId,
                developmentOrgScopeCompanyId,
                developmentOrgScopeBranchId,
                developmentOrgScopeCompanyName,
                developmentOrgScopeBranchName)
        ]));
}

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
    if (builder.Environment.IsDevelopment())
    {
        // HTTP localhost cannot store a __Host- or Secure cookie. This is an
        // explicit Development-only compatibility boundary; production keeps
        // the approved __Host-/Secure configuration from the Identity module.
        options.Cookie.Name = "MiniErp.Auth";
        options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
    }
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
    options.Cookie.Name = builder.Environment.IsDevelopment() ? "MiniErp.AntiForgery" : "__Host-MiniErp.AntiForgery";
    options.Cookie.HttpOnly = false;
    options.Cookie.SecurePolicy = builder.Environment.IsDevelopment() ? CookieSecurePolicy.SameAsRequest : CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.Path = "/";
});
builder.Services.AddSingleton<ITrustedRequestContextResolver, DefaultTrustedRequestContextResolver>();
builder.Services.AddSingleton<LocalFoundationIdempotencyStore>();
builder.Services.AddSingleton<LocalMasterDataIdempotencyStore>();
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
builder.Services.AddOpenApi("v1", options =>
{
    options.AddDocumentTransformer<MiniErpOpenApiDocumentTransformer>();
    options.AddOperationTransformer<MiniErpOpenApiOperationTransformer>();
});

var app = builder.Build();

if (developmentMasterDataSqliteConnectionString is not null
    && developmentBusinessPartiesSqliteConnectionString is not null
    && developmentProcurementSqliteConnectionString is not null)
{
    MasterDataPersistenceServiceCollectionExtensions.EnsureDevelopmentSqliteDatabase(
        developmentMasterDataSqliteConnectionString);
    BusinessPartiesPersistenceServiceCollectionExtensions.EnsureDevelopmentSqliteDatabase(
        developmentBusinessPartiesSqliteConnectionString);
    ProcurementPersistenceServiceCollectionExtensions.EnsureDevelopmentSqliteDatabase(
        developmentProcurementSqliteConnectionString);
}

app.SeedDevelopmentBootstrap();

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
    catch (Exception exception)
    {
        app.Logger.LogError(
            exception,
            "Unhandled request failure for {RequestMethod} {RequestPath}",
            httpContext.Request.Method,
            httpContext.Request.Path);
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
    .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("platform.health")));

app.MapGet("/api/v1/module-registration", (
    IPlatformAdministrationModule platformModule,
    IMasterDataCatalogModule masterDataModule,
    IBusinessPartiesModule businessPartiesModule) =>
    Results.Ok(new
    {
        module = platformModule.Descriptor.Key,
        name = platformModule.Descriptor.Name,
        boundary = platformModule.Descriptor.Boundary,
        registered = platformModule.RegistrationEvidence.IsRegistered,
        masterData = new
        {
            module = masterDataModule.Descriptor.Key,
            name = masterDataModule.Descriptor.Name,
            boundary = masterDataModule.Descriptor.Boundary,
            registered = masterDataModule.RegistrationEvidence.IsRegistered
        },
        businessParties = new
        {
            module = businessPartiesModule.Descriptor.Key,
            name = businessPartiesModule.Descriptor.Name,
            boundary = businessPartiesModule.Descriptor.Boundary,
            registered = businessPartiesModule.RegistrationEvidence.IsRegistered
        }
    }))
    .WithName("platform.module-registration")
    .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("platform.module-registration")));

app.MapGet("/api/v1/auth/antiforgery", async (HttpContext httpContext, IAntiforgery antiforgery) =>
{
    var tokens = antiforgery.GetAndStoreTokens(httpContext);
    var requestToken = tokens.RequestToken ?? string.Empty;
    httpContext.Response.Headers["X-CSRF-TOKEN"] = requestToken;
    httpContext.Response.Headers.CacheControl = "no-store";
    await httpContext.Response.WriteAsJsonAsync(new { status = "issued" });
})
    .WithName("auth.antiforgery.read")
    .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("auth.antiforgery.read")));

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
    .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("auth.sign-in")));

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
    .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("auth.sign-out")));

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
    .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("auth.session.read")));

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
    .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("auth.contexts.read")));

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

    if (request is null || request.ContextId == Guid.Empty
        || request.ExpectedSelectionVersion < 0
        || request.ExpectedEligibilityVersion < 0)
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

    var fingerprint = $"{request.ContextId:N}|selection:{request.ExpectedSelectionVersion}|eligibility:{request.ExpectedEligibilityVersion}";
    var check = idempotencyStore.Begin(key!, binding, fingerprint, DateTimeOffset.UtcNow, TimeSpan.FromMinutes(10));
    switch (check.Decision)
    {
        case FoundationIdempotencyDecision.Replay when check.Response?.SessionResponse is { } original:
            return Results.Json(original with { Replayed = true });
        case FoundationIdempotencyDecision.Replay:
        case FoundationIdempotencyDecision.RequestConflict:
        case FoundationIdempotencyDecision.InProgress:
            return await WriteProblemAsync(httpContext, StatusCodes.Status409Conflict, "idempotency_conflict", "Idempotency conflict", "The request cannot be replayed with different or incomplete input.", operationId);
        case FoundationIdempotencyDecision.New:
            break;
        default:
            idempotencyStore.Release(key!, binding);
            return await WriteProblemAsync(httpContext, StatusCodes.Status409Conflict, "idempotency_conflict", "Idempotency conflict", "The idempotency decision is not recognized.", operationId);
    }

    var descriptor = FoundationOperationCatalog.GetRequired(operationId);
    var committed = false;
    try
    {
        var candidateContext = identityHost.ResolveCandidateContext(httpContext.User, request.ContextId, descriptor, GetCorrelation(httpContext));
        if (candidateContext is null)
        {
            return await WriteProblemAsync(httpContext, StatusCodes.Status403Forbidden, "access_denied", "Access denied", "The requested context is not available.", operationId);
        }

        var execution = await auditCoordinator.ExecuteProtectedAsync(
            candidateContext,
            operationId,
            GetCorrelation(httpContext),
            FoundationAuditReason.Allowed,
            () => Task.FromResult(identityHost.SwitchContext(httpContext.User, request.ContextId, request.ExpectedSelectionVersion, request.ExpectedEligibilityVersion)),
            key,
            request.ExpectedSelectionVersion.ToString(System.Globalization.CultureInfo.InvariantCulture));
        if (!execution.Succeeded || execution.Value is not { Succeeded: true })
        {
            return await WriteProblemAsync(httpContext, StatusCodes.Status409Conflict, "context_version_conflict", "Context selection conflict", "The context selection is stale or unavailable.", operationId);
        }

        var switched = execution.Value.State ?? identityHost.GetSession(httpContext.User);
        var sessionResponse = ToSessionResponse(switched);
        idempotencyStore.Commit(key!, binding, FoundationIdempotencyResponse.ForSession(sessionResponse));
        committed = true;
        return Results.Json(sessionResponse);
    }
    finally
    {
        if (!committed)
        {
            idempotencyStore.Release(key!, binding);
        }
    }
})
    .WithName("auth.context-switch")
    .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("auth.context-switch")));

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
    .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("foundation.tenant-context.read")));

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
    .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("foundation.support-context.read")));

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
    .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("foundation.platform-context.read")));

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
    .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("foundation.target.read")));

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
    .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("foundation.probe.write")));

app.MapCategoryUomEndpoints();
app.MapProductIdentityEndpoints();
app.MapSupplierEndpoints();
app.MapCustomerEndpoints();
app.MapCurrencyPaymentTermEndpoints();
app.MapTaxEndpoints();
app.MapExchangeRateEndpoints();
app.MapPriceListEndpoints();
app.MapMasterDataImportEndpoints();
app.MapPurchaseRequestEndpoints();
app.MapSupplierQuotationEndpoints();

app.MapOpenApi("/openapi/v1.json")
    .WithName("platform.openapi")
    .WithMetadata(new FoundationOperationMetadata(FoundationOperationCatalog.GetRequired("platform.openapi")));

if ((app.Environment.IsDevelopment()
        || string.Equals(app.Environment.EnvironmentName, "QA", StringComparison.OrdinalIgnoreCase))
    && app.Configuration.GetValue("Scalar:Enabled", true))
{
    app.MapScalarApiReference(
        "/scalar",
        options => options
            .AddDocument("v1")
            .DisableAgent()
            .WithTitle("Mini ERP SaaS Platform API"));
}

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
    if (metadata is null
        || !FoundationOperationCatalog.TryGet(metadata.OperationId, out var descriptor)
        || descriptor.Visibility != FoundationOperationVisibility.Public
        || metadata.Descriptor != descriptor
        || !MatchesSecurityProfile(metadata.SecurityProfile, context.SecurityProfile))
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
        state.SelectionVersion,
        replayed);

static FoundationContextCandidateResponse ToContextResponse(FoundationHostContextCandidate candidate) =>
    new(candidate.ContextId, candidate.Kind.ToString(), candidate.TenantId, candidate.DisplayName, candidate.EligibilityVersion);

/// <summary>Entry point exposed for API integration tests.</summary>
public partial class Program;

internal static class FoundationApiKeys
{
    internal const string CorrelationItem = "MiniErp.Foundation.CorrelationId";
}
