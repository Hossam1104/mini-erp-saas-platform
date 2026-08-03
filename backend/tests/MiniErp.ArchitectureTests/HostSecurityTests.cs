using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using MiniErp.App.BuildingBlocks.Rest;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.Modules.Audit;
using MiniErp.App.Modules.Identity;
using MiniErp.Contracts.Modules.Foundation;
using Xunit;

namespace MiniErp.ArchitectureTests;

/// <summary>
/// Direct host security tests use the production Program graph. The only
/// substitution is a bounded in-memory identity/assurance provider so the
/// host adapter is exercised without a database or an external MFA service.
/// The trusted resolver itself is never replaced.
/// </summary>
public sealed class HostSecurityTests
{
    [Fact]
    public async Task Sign_in_uses_secure_http_only_cookie_and_never_serializes_secrets()
    {
        using var factory = new HostFactory();
        using var client = factory.CreateClient();
        factory.SeedCore();

        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/auth/session")).StatusCode);
        var response = await SignInAsync(client, "owner@example.com", factory.Password);
        var body = await ReadJsonAsync(response);
        var raw = body.GetRawText();

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var setCookie = Assert.Single(response.Headers.GetValues("Set-Cookie"));
        Assert.Contains("__Host-MiniErp.Auth=", setCookie, StringComparison.Ordinal);
        Assert.Contains("Secure", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("HttpOnly", setCookie, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain(FoundationIdentityClaims.SessionToken, raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("cookieValue", raw, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", raw, StringComparison.OrdinalIgnoreCase);

        var session = await client.GetAsync("/api/v1/auth/session");
        Assert.Equal(HttpStatusCode.OK, session.StatusCode);
    }

    [Fact]
    public async Task Wrong_password_is_generic_and_fifth_failure_locks_the_account()
    {
        using var factory = new HostFactory();
        using var client = factory.CreateClient();
        factory.SeedCore();

        for (var attempt = 0; attempt < 5; attempt++)
        {
            var response = await SignInAsync(client, "owner@example.com", "wrong-password");
            var body = await ReadJsonAsync(response);
            Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
            Assert.Equal("authentication_failed", body.GetProperty("code").GetString());
        }

        var locked = await SignInAsync(client, "owner@example.com", factory.Password);
        Assert.Equal(HttpStatusCode.Unauthorized, locked.StatusCode);
        Assert.Equal("authentication_failed", (await ReadJsonAsync(locked)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task Contexts_are_server_derived_and_foreign_context_or_spoofed_headers_have_no_authority()
    {
        using var factory = new HostFactory();
        using var client = factory.CreateClient();
        factory.SeedCore();
        var signIn = await SignInAsync(client, "owner@example.com", factory.Password);
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);

        var contextsResponse = await client.GetAsync("/api/v1/auth/contexts");
        var contexts = await ReadJsonAsync(contextsResponse);
        Assert.Equal(HttpStatusCode.OK, contextsResponse.StatusCode);
        var candidates = contexts.GetProperty("contexts").EnumerateArray().ToArray();
        var candidate = Assert.Single(candidates);
        Assert.Equal(factory.TenantA.Value, Guid.Parse(candidate.GetProperty("tenantId").GetString()!));
        Assert.DoesNotContain(candidates, item => item.GetProperty("tenantId").GetString() == factory.TenantB.Value.ToString("D"));

        using var spoofed = new HttpRequestMessage(HttpMethod.Get, "/api/v1/foundation/tenant-context");
        spoofed.Headers.TryAddWithoutValidation("X-Tenant-Id", factory.TenantA.Value.ToString("D"));
        spoofed.Headers.TryAddWithoutValidation("X-User-Id", factory.Owner.Value.ToString("D"));
        spoofed.Headers.TryAddWithoutValidation("X-Role", "tenant-admin");
        spoofed.Headers.TryAddWithoutValidation("X-Session-Id", Guid.NewGuid().ToString("D"));
        var spoofedResponse = await client.SendAsync(spoofed);
        Assert.Equal(HttpStatusCode.Forbidden, spoofedResponse.StatusCode);

        var antiToken = await GetAntiforgeryTokenAsync(client);
        var foreign = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/context-switch")
        {
            Content = JsonContent.Create(new FoundationContextSwitchRequest(factory.MembershipB.Value, 0))
        };
        foreign.Headers.TryAddWithoutValidation("X-CSRF-TOKEN", antiToken);
        foreign.Headers.TryAddWithoutValidation("Idempotency-Key", "context-foreign-001");
        var foreignResponse = await client.SendAsync(foreign);
        Assert.Equal(HttpStatusCode.Forbidden, foreignResponse.StatusCode);
        Assert.Equal("access_denied", (await ReadJsonAsync(foreignResponse)).GetProperty("code").GetString());
    }

    [Fact]
    public async Task Framework_antiforgery_protects_context_switch_and_probe_with_idempotency_and_version_checks()
    {
        using var factory = new HostFactory();
        using var client = factory.CreateClient();
        factory.SeedCore();
        Assert.Equal(HttpStatusCode.OK, (await SignInAsync(client, "owner@example.com", factory.Password)).StatusCode);

        var antiResponse = await client.GetAsync("/api/v1/auth/antiforgery");
        var antiToken = antiResponse.Headers.GetValues("X-CSRF-TOKEN").Single();
        Assert.DoesNotContain(antiToken, await antiResponse.Content.ReadAsStringAsync(), StringComparison.Ordinal);

        var missingAnti = await SwitchAsync(client, factory.MembershipA.Value, 0, "context-no-anti-001", token: null);
        Assert.Equal(HttpStatusCode.Forbidden, missingAnti.StatusCode);
        Assert.Equal("antiforgery_failed", (await ReadJsonAsync(missingAnti)).GetProperty("code").GetString());

        var wrongAnti = await SwitchAsync(client, factory.MembershipA.Value, 0, "context-wrong-anti-001", "fixed-token");
        Assert.Equal(HttpStatusCode.Forbidden, wrongAnti.StatusCode);

        var switched = await SwitchAsync(client, factory.MembershipA.Value, 0, "context-switch-001", antiToken);
        Assert.Equal(HttpStatusCode.OK, switched.StatusCode);
        var switchedBody = await ReadJsonAsync(switched);
        Assert.Equal(factory.TenantA.Value, Guid.Parse(switchedBody.GetProperty("selectedTenantId").GetString()!));

        var replay = await SwitchAsync(client, factory.MembershipA.Value, 0, "context-switch-001", antiToken);
        Assert.Equal(HttpStatusCode.OK, replay.StatusCode);
        Assert.Equal(1, (await ReadJsonAsync(replay)).GetProperty("contextVersion").GetInt64());

        var stale = await SwitchAsync(client, factory.MembershipA.Value, 0, "context-stale-001", antiToken);
        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        Assert.Equal("context_version_conflict", (await ReadJsonAsync(stale)).GetProperty("code").GetString());

        var tenantContext = await client.GetAsync("/api/v1/foundation/tenant-context");
        Assert.Equal(HttpStatusCode.OK, tenantContext.StatusCode);

        var missingProbeAnti = new HttpRequestMessage(HttpMethod.Post, "/api/v1/foundation/probe")
        {
            Content = JsonContent.Create(new FoundationWriteRequest("probe"))
        };
        missingProbeAnti.Headers.TryAddWithoutValidation("Idempotency-Key", "probe-no-anti-001");
        missingProbeAnti.Headers.TryAddWithoutValidation("If-Match", "\"1\"");
        var missingProbeResponse = await client.SendAsync(missingProbeAnti);
        Assert.Equal(HttpStatusCode.Forbidden, missingProbeResponse.StatusCode);

        using var probe = new HttpRequestMessage(HttpMethod.Post, "/api/v1/foundation/probe")
        {
            Content = JsonContent.Create(new FoundationWriteRequest("probe"))
        };
        probe.Headers.TryAddWithoutValidation("X-CSRF-TOKEN", antiToken);
        probe.Headers.TryAddWithoutValidation("Idempotency-Key", "probe-001");
        probe.Headers.TryAddWithoutValidation("If-Match", "\"1\"");
        var probeResponse = await client.SendAsync(probe);
        Assert.True(probeResponse.StatusCode == HttpStatusCode.OK, await probeResponse.Content.ReadAsStringAsync());
        Assert.Equal("accepted", (await ReadJsonAsync(probeResponse)).GetProperty("result").GetString());
    }

    [Fact]
    public async Task Sign_out_and_server_revocation_invalidate_the_cookie_session()
    {
        using var factory = new HostFactory();
        using var client = factory.CreateClient();
        factory.SeedCore();
        var signIn = await SignInAsync(client, "owner@example.com", factory.Password);
        Assert.Equal(HttpStatusCode.OK, signIn.StatusCode);

        var token = await GetAntiforgeryTokenAsync(client);
        var selected = await SwitchAsync(client, factory.MembershipA.Value, 0, "signout-context-001", token);
        Assert.Equal(HttpStatusCode.OK, selected.StatusCode);
        using var signOut = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/sign-out");
        signOut.Headers.TryAddWithoutValidation("X-CSRF-TOKEN", token);
        var signOutResponse = await client.SendAsync(signOut);
        Assert.Equal(HttpStatusCode.NoContent, signOutResponse.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/auth/session")).StatusCode);
        Assert.True(factory.Services.GetRequiredService<LocalImmutableAuditEvidenceStore>().Count >= 1);

        var secondSignIn = await SignInAsync(client, "owner@example.com", factory.Password);
        Assert.Equal(HttpStatusCode.OK, secondSignIn.StatusCode);
        var secondBody = await ReadJsonAsync(secondSignIn);
        var secondSessionId = Guid.Parse(secondBody.GetProperty("sessionId").GetString()!);
        factory.Identity.RevokeSession(new SessionId(secondSessionId), "test-revocation");
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/v1/auth/session")).StatusCode);
    }

    [Fact]
    public void Production_resolver_requires_a_server_session_and_ignores_claims_or_headers()
    {
        using var factory = new HostFactory();
        using var client = factory.CreateClient();
        factory.SeedCore();
        var host = factory.IdentityHost;
        var resolver = new DefaultTrustedRequestContextResolver(host);
        var signIn = host.SignIn("owner@example.com", factory.Password);
        Assert.True(signIn.Succeeded);
        Assert.NotNull(signIn.Principal);

        var initial = Resolve(resolver, signIn.Principal!, factory.TenantA.Value.ToString("D"));
        Assert.Equal(FoundationSecurityProfile.AuthenticatedSession, initial.SecurityProfile);

        var selected = host.SwitchContext(signIn.Principal!, factory.MembershipA.Value, 0);
        Assert.True(selected.Succeeded);
        var ordinary = Resolve(resolver, signIn.Principal!, factory.TenantB.Value.ToString("D"));
        Assert.Equal(FoundationSecurityProfile.OrdinaryMembership, ordinary.SecurityProfile);
        Assert.Equal(factory.TenantA, ordinary.TenantContext!.TenantId);

        var spoofedIdentity = new ClaimsIdentity(FirstPartyCookieConfiguration.Scheme);
        spoofedIdentity.AddClaim(new Claim(ClaimTypes.NameIdentifier, factory.Owner.Value.ToString("D")));
        spoofedIdentity.AddClaim(new Claim(FoundationIdentityClaims.SessionId, Guid.NewGuid().ToString("D")));
        spoofedIdentity.AddClaim(new Claim(FoundationIdentityClaims.SessionToken, "client-chosen-token"));
        var spoofed = Resolve(resolver, new ClaimsPrincipal(spoofedIdentity), factory.TenantB.Value.ToString("D"));
        Assert.Equal(FoundationSecurityProfile.Anonymous, spoofed.SecurityProfile);

        factory.Identity.RevokeSession(new SessionId(signIn.SessionId!.Value), "resolver-revocation");
        var revoked = Resolve(resolver, signIn.Principal!, factory.TenantA.Value.ToString("D"));
        Assert.Equal(FoundationSecurityProfile.Anonymous, revoked.SecurityProfile);
    }

    [Fact]
    public void Support_and_platform_contexts_require_assurance_and_resolve_through_the_same_host_seam()
    {
        using var factory = new HostFactory();
        using var client = factory.CreateClient();
        factory.SeedAll();
        var host = factory.IdentityHost;

        var support = host.SignIn("support@example.com", factory.Password);
        Assert.True(support.Succeeded);
        Assert.DoesNotContain(host.ListContexts(support.Principal!), item => item.Kind == FoundationHostContextKind.SupportGrant);
        var supportToken = RawToken(support.Principal!);
        var supportSession = new SessionId(support.SessionId!.Value);
        Assert.True(factory.Assurance.IssueMfaAndAccept(factory.Identity, supportToken, factory.SupportUser, supportSession, factory.Clock.GetUtcNow()));
        var supportOperation = $"support:{factory.SupportGrant.Value}:{IdentityPermissions.SupportRead.Value}";
        Assert.True(factory.Assurance.IssueFreshAndAccept(factory.Identity, supportToken, factory.SupportUser, supportSession, supportOperation, factory.Clock.GetUtcNow()));
        Assert.Contains(host.ListContexts(support.Principal!), item => item.ContextId == factory.SupportGrant.Value && item.Kind == FoundationHostContextKind.SupportGrant);
        Assert.True(host.SwitchContext(support.Principal!, factory.SupportGrant.Value, 0).Succeeded);
        Assert.Equal(FoundationSecurityProfile.SupportGrant, host.ResolveContext(support.Principal!, "support-correlation").SecurityProfile);

        var platform = host.SignIn("platform@example.com", factory.Password);
        Assert.True(platform.Succeeded);
        var platformToken = RawToken(platform.Principal!);
        var platformSession = new SessionId(platform.SessionId!.Value);
        Assert.True(factory.Assurance.IssueMfaAndAccept(factory.Identity, platformToken, factory.PlatformUser, platformSession, factory.Clock.GetUtcNow()));
        Assert.True(factory.Assurance.IssueFreshAndAccept(factory.Identity, platformToken, factory.PlatformUser, platformSession, "platform.context.read", factory.Clock.GetUtcNow()));
        Assert.Contains(host.ListContexts(platform.Principal!), item => item.Kind == FoundationHostContextKind.PlatformGovernanceContext);
        var platformContext = host.ListContexts(platform.Principal!).Single(item => item.Kind == FoundationHostContextKind.PlatformGovernanceContext);
        Assert.True(host.SwitchContext(platform.Principal!, platformContext.ContextId, 0).Succeeded);
        Assert.Equal(FoundationSecurityProfile.PlatformGovernanceContext, host.ResolveContext(platform.Principal!, "platform-correlation").SecurityProfile);
    }

    private static FoundationRequestContext Resolve(DefaultTrustedRequestContextResolver resolver, ClaimsPrincipal principal, string spoofedTenant)
    {
        var httpContext = new DefaultHttpContext { User = principal };
        httpContext.Request.Headers["X-Tenant-Id"] = spoofedTenant;
        return resolver.ResolveAsync(httpContext).Result;
    }

    private static async Task<HttpResponseMessage> SignInAsync(HttpClient client, string login, string password) =>
        await client.PostAsJsonAsync("/api/v1/auth/sign-in", new FoundationSignInRequest(login, password));

    private static async Task<HttpResponseMessage> SwitchAsync(HttpClient client, Guid contextId, long expectedVersion, string idempotencyKey, string? token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/context-switch")
        {
            Content = JsonContent.Create(new FoundationContextSwitchRequest(contextId, expectedVersion))
        };
        request.Headers.TryAddWithoutValidation("Idempotency-Key", idempotencyKey);
        if (token is not null)
        {
            request.Headers.TryAddWithoutValidation("X-CSRF-TOKEN", token);
        }

        return await client.SendAsync(request);
    }

    private static async Task<string> GetAntiforgeryTokenAsync(HttpClient client)
    {
        var response = await client.GetAsync("/api/v1/auth/antiforgery");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return response.Headers.GetValues("X-CSRF-TOKEN").Single();
    }

    private static string RawToken(ClaimsPrincipal principal) =>
        principal.FindFirstValue(FoundationIdentityClaims.SessionToken)!;

    private static async Task<JsonElement> ReadJsonAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.Clone();
    }

    internal sealed class HostFactory : WebApplicationFactory<Program>
    {
        internal readonly ManualTimeProvider Clock = new();
        internal readonly TestAuthenticationAssuranceEvidenceSource Assurance = new();
        internal readonly TenantId TenantA = new(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        internal readonly TenantId TenantB = new(Guid.Parse("22222222-2222-2222-2222-222222222222"));
        internal readonly string Password = "Correct-horse-battery-1!";
        internal IdentityAuthorizationService Identity { get; private set; } = null!;
        internal IFoundationIdentityHost IdentityHost { get; private set; } = null!;
        internal UserId Owner { get; private set; }
        internal UserId Approver { get; private set; }
        internal UserId SupportUser { get; private set; }
        internal UserId PlatformUser { get; private set; }
        internal MembershipId MembershipA { get; private set; }
        internal MembershipId MembershipB { get; private set; }
        internal SupportGrantId SupportGrant { get; private set; }

        private bool seeded;

        internal void SeedCore()
        {
            if (seeded)
            {
                return;
            }

            Identity = Services.GetRequiredService<IdentityAuthorizationService>();
            Approver = Identity.CreateUser("approver@example.com", Password);
            Owner = Identity.CreateUser("owner@example.com", Password);
            var foreign = Identity.CreateUser("foreign@example.com", Password);
            MembershipA = Identity.AddMembership(Owner, TenantA);
            MembershipB = Identity.AddMembership(Owner, TenantB);
            Identity.AddMembership(foreign, TenantB);

            var role = Identity.CreateRole("tenant-admin", TenantA, false, [IdentityPermissions.Read]);
            Identity.Store.RoleAssignments[MembershipA].Add(new RoleAssignment(role, MembershipA, Owner, TenantA, Approver));
            var scope = new ScopeGrantId(Guid.NewGuid());
            Identity.Store.ScopeGrants.Add(scope, new AccessScopeGrant(scope, MembershipA, Owner, OrganizationScope.ForTenant(TenantA), Approver));
            Identity.Store.ScopeGrantsByMembership[MembershipA].Add(scope);
            IdentityHost = Services.GetRequiredService<IFoundationIdentityHost>();
            seeded = true;
        }

        internal void SeedAll()
        {
            SeedCore();
            if (SupportUser != default)
            {
                return;
            }

            SupportUser = Identity.CreateUser("support@example.com", Password);
            var supportCase = Identity.AddSupportCase(TenantA);
            var supportGrant = new SupportGrant(
                new SupportGrantId(Guid.NewGuid()),
                supportCase,
                SupportUser,
                Approver,
                TenantA,
                "incident diagnosis",
                OrganizationScope.ForTenant(TenantA),
                [IdentityPermissions.SupportRead],
                Clock.GetUtcNow().AddHours(1));
            SupportGrant = supportGrant.Id;
            Identity.Store.SupportGrants.Add(SupportGrant, supportGrant);

            PlatformUser = Identity.CreateUser("platform@example.com", Password);
            Identity.Store.PlatformPermissions[PlatformUser] =
            [new PlatformPermissionAssignment(PlatformUser, IdentityPermissions.AssignPlatformPermission, Approver, "test seed")];
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.ConfigureTestServices(services =>
            {
                services.RemoveAll<IdentityAuthorizationService>();
                services.RemoveAll<IdentityStore>();
                services.RemoveAll<IFoundationIdentityHost>();
                services.RemoveAll<IAuthenticationAssuranceEvidenceSource>();
                services.RemoveAll<TimeProvider>();
                services.AddSingleton<TimeProvider>(Clock);
                services.AddSingleton<IAuthenticationAssuranceEvidenceSource>(Assurance);
                services.AddSingleton<IdentityAuthorizationService>(_ =>
                    new IdentityAuthorizationService(timeProvider: Clock, assuranceEvidenceSource: Assurance));
                services.AddSingleton<IFoundationIdentityHost>(serviceProvider =>
                    new FoundationIdentityHost(serviceProvider.GetRequiredService<IdentityAuthorizationService>()));
            });
        }

        internal new HttpClient CreateClient() => base.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = true,
            AllowAutoRedirect = false
        });

    }

    internal sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset current = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => current;

        internal void Advance(TimeSpan duration) => current = current.Add(duration);
    }

    internal sealed class TestAuthenticationAssuranceEvidenceSource : IAuthenticationAssuranceEvidenceSource
    {
        private readonly Dictionary<string, AuthenticationAssuranceEvidence> evidenceByToken = new(StringComparer.Ordinal);

        internal bool IssueMfaAndAccept(IdentityAuthorizationService service, string sessionToken, UserId userId, SessionId sessionId, DateTimeOffset now)
        {
            var token = Issue(new AuthenticationAssuranceEvidence(userId, sessionId, AuthenticationAssuranceType.Mfa, null, now, now.AddMinutes(5), SourceId, true));
            return service.AcceptMfaEvidence(sessionToken, token);
        }

        internal bool IssueFreshAndAccept(IdentityAuthorizationService service, string sessionToken, UserId userId, SessionId sessionId, string operation, DateTimeOffset now)
        {
            var token = Issue(new AuthenticationAssuranceEvidence(userId, sessionId, AuthenticationAssuranceType.FreshAuthentication, operation, now, now.AddMinutes(5), SourceId, true));
            return service.AcceptFreshAuthenticationEvidence(sessionToken, operation, token);
        }

        public string SourceId => "test-assurance";

        public bool TryValidateMfaEvidence(string opaqueEvidence, UserId expectedUserId, SessionId expectedSessionId, DateTimeOffset now, out AuthenticationAssuranceEvidence evidence) =>
            TryTake(opaqueEvidence, expectedUserId, expectedSessionId, AuthenticationAssuranceType.Mfa, null, now, out evidence);

        public bool TryValidateFreshAuthenticationEvidence(string opaqueEvidence, UserId expectedUserId, SessionId expectedSessionId, string operation, DateTimeOffset now, out AuthenticationAssuranceEvidence evidence) =>
            TryTake(opaqueEvidence, expectedUserId, expectedSessionId, AuthenticationAssuranceType.FreshAuthentication, operation, now, out evidence);

        private string Issue(AuthenticationAssuranceEvidence evidence)
        {
            var token = $"test-evidence-{Guid.NewGuid():N}";
            evidenceByToken.Add(token, evidence);
            return token;
        }

        private bool TryTake(string token, UserId userId, SessionId sessionId, AuthenticationAssuranceType type, string? operation, DateTimeOffset now, out AuthenticationAssuranceEvidence evidence)
        {
            if (!evidenceByToken.TryGetValue(token, out evidence!)
                || evidence.UserId != userId
                || evidence.SessionId != sessionId
                || evidence.Type != type
                || !string.Equals(evidence.Operation, operation, StringComparison.Ordinal)
                || evidence.IssuedAt > now
                || evidence.ExpiresAt <= now)
            {
                evidence = null!;
                return false;
            }

            evidenceByToken.Remove(token);
            return true;
        }
    }
}
