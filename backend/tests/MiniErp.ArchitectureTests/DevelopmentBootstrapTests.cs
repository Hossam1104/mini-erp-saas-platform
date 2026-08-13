using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MiniErp.Api;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.Modules.Identity;
using MiniErp.App.Modules.MasterData;
using MiniErp.Contracts.Modules.Foundation;
using MiniErp.Infrastructure.Persistence.Modules.MasterData;
using Xunit;

namespace MiniErp.ArchitectureTests;

public sealed class DevelopmentBootstrapTests
{
    [Fact]
    public void Bootstrap_DisabledByDefault_DoesNotSeedUser()
    {
        using var factory = new CustomTestWebApplicationFactory(new Dictionary<string, string?>());
        using var scope = factory.Services.CreateScope();
        var identity = scope.ServiceProvider.GetRequiredService<IdentityAuthorizationService>();

        var result = identity.Authenticate("admin@minierp.local", "SomePassword123!");
        Assert.False(result.Succeeded);
        Assert.Equal("authentication_failed", result.PublicCode);
    }

    [Fact]
    public void Bootstrap_EnabledWithPassword_SeedsUserAndPermissionsIdempotently()
    {
        var settings = new Dictionary<string, string?>
        {
            ["MESP_DEV_BOOTSTRAP_ENABLED"] = "true",
            ["MESP_DEV_ADMIN_LOGIN"] = "admin@minierp.local",
            ["MESP_DEV_ADMIN_PASSWORD"] = "LocalDevSecret123!"
        };

        using var factory = new CustomTestWebApplicationFactory(settings);
        using var scope = factory.Services.CreateScope();

        var host = scope.ServiceProvider.GetRequiredService<IFoundationIdentityHost>();

        var signIn = host.SignIn("admin@minierp.local", "LocalDevSecret123!");
        Assert.True(signIn.Succeeded);
        Assert.NotNull(signIn.Principal);
        Assert.NotNull(signIn.SessionId);

        var contexts = host.ListContexts(signIn.Principal);
        Assert.Single(contexts);
        Assert.Equal(DevelopmentBootstrap.DevTenantId.Value, contexts[0].TenantId);
    }

    [Fact]
    public void Bootstrap_WrongPassword_Fails()
    {
        var settings = new Dictionary<string, string?>
        {
            ["MESP_DEV_BOOTSTRAP_ENABLED"] = "true",
            ["MESP_DEV_ADMIN_LOGIN"] = "admin@minierp.local",
            ["MESP_DEV_ADMIN_PASSWORD"] = "LocalDevSecret123!"
        };

        using var factory = new CustomTestWebApplicationFactory(settings);
        using var scope = factory.Services.CreateScope();
        var identity = scope.ServiceProvider.GetRequiredService<IdentityAuthorizationService>();

        var result = identity.Authenticate("admin@minierp.local", "WrongPassword!");
        Assert.False(result.Succeeded);
        Assert.Equal("authentication_failed", result.PublicCode);
    }

    [Fact]
    public async Task Bootstrap_FullSignInSessionContextFlow_Succeeds()
    {
        var settings = new Dictionary<string, string?>
        {
            ["MESP_DEV_BOOTSTRAP_ENABLED"] = "true",
            ["MESP_DEV_ADMIN_LOGIN"] = "admin@minierp.local",
            ["MESP_DEV_ADMIN_PASSWORD"] = "LocalDevSecret123!"
        };

        using var factory = new CustomTestWebApplicationFactory(settings);
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost"),
            HandleCookies = true,
            AllowAutoRedirect = false
        });

        // 1. Sign in
        var signInResponse = await client.PostAsJsonAsync("/api/v1/auth/sign-in", new FoundationSignInRequest("admin@minierp.local", "LocalDevSecret123!"));
        Assert.Equal(HttpStatusCode.OK, signInResponse.StatusCode);

        // 2. Fetch Antiforgery token
        var antiforgeryMessage = await client.GetAsync("/api/v1/auth/antiforgery");
        Assert.Equal(HttpStatusCode.OK, antiforgeryMessage.StatusCode);
        var csrfToken = antiforgeryMessage.Headers.GetValues("X-CSRF-TOKEN").Single();

        // 3. Read session
        var sessionResponse = await client.GetFromJsonAsync<FoundationSessionResponse>("/api/v1/auth/session");
        Assert.NotNull(sessionResponse);
        Assert.True(sessionResponse.Authenticated);

        // 4. List contexts
        var contextsResponse = await client.GetFromJsonAsync<FoundationContextsResponse>("/api/v1/auth/contexts");
        Assert.NotNull(contextsResponse);
        Assert.Single(contextsResponse.Contexts);

        var targetContext = contextsResponse.Contexts[0];
        Assert.Equal(DevelopmentBootstrap.DevTenantId.Value, targetContext.TenantId);

        // 5. Switch context with CSRF and Idempotency headers
        using var switchRequest = new HttpRequestMessage(HttpMethod.Post, "/api/v1/auth/context-switch")
        {
            Content = JsonContent.Create(new FoundationContextSwitchRequest(
                targetContext.ContextId,
                sessionResponse.SelectionVersion,
                targetContext.EligibilityVersion))
        };
        switchRequest.Headers.TryAddWithoutValidation("X-CSRF-TOKEN", csrfToken);
        switchRequest.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.NewGuid().ToString("N"));

        var switchResponse = await client.SendAsync(switchRequest);
        Assert.Equal(HttpStatusCode.OK, switchResponse.StatusCode);

        // 6. Access Price List list endpoint as authenticated Tenant user
        var priceListResponse = await client.GetAsync("/api/v1/master-data/price-lists");
        Assert.Equal(HttpStatusCode.OK, priceListResponse.StatusCode);
    }

    private sealed class CustomTestWebApplicationFactory : WebApplicationFactory<Program>
    {
        private readonly Dictionary<string, string?> customSettings;
        private SqliteConnection? connection;

        public CustomTestWebApplicationFactory(Dictionary<string, string?> customSettings)
        {
            this.customSettings = customSettings;
        }

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, config) =>
            {
                config.AddInMemoryCollection(customSettings);
            });
            builder.ConfigureTestServices(services =>
            {
                connection = new SqliteConnection("Data Source=:memory:");
                connection.Open();
                var options = new DbContextOptionsBuilder().UseSqlite(connection).Options;
                using (var db = new MasterDataDbContext(options, TenantContext.ForOrdinaryMembership(DevelopmentBootstrap.DevTenantId, new MembershipReference(Guid.NewGuid()), null, new CorrelationId("bootstrap-init"), Guid.NewGuid())))
                {
                    db.Database.EnsureCreated();
                }

                var persistence = new MasterDataPriceListPersistence(options);
                services.AddSingleton<IMasterDataPriceListPersistence>(persistence);
            });
        }

        protected override void Dispose(bool disposing)
        {
            base.Dispose(disposing);
            if (disposing)
            {
                connection?.Dispose();
            }
        }
    }
}
