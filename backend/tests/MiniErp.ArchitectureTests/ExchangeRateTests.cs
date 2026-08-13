using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MiniErp.App.BuildingBlocks.Rest;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.Modules.MasterData;
using MiniErp.Contracts.Modules.Audit;
using MiniErp.Contracts.Modules.MasterData;
using MiniErp.Infrastructure.Persistence.Modules.MasterData;
using Xunit;

namespace MiniErp.ArchitectureTests;

public sealed class ExchangeRateTests
{
    private static readonly Guid TenantA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid TenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid Actor = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid Session = Guid.Parse("44444444-4444-4444-4444-444444444444");

    [Fact]
    public async Task Exchange_rates_are_directional_tenant_owned_versioned_and_referenceable()
    {
        await using var fixture = await Fixture.CreateAsync();

        var usd = await fixture.CurrencyService.CreateCurrencyAsync(
            fixture.ContextA,
            new CreateMasterDataCurrencyCommand("USD", new LocalizedName("US Dollar")));
        var sar = await fixture.CurrencyService.CreateCurrencyAsync(
            fixture.ContextA,
            new CreateMasterDataCurrencyCommand("SAR", new LocalizedName("Saudi Riyal")));
        Assert.True(usd.Succeeded, usd.Code);
        Assert.True(sar.Succeeded, sar.Code);

        var created = await fixture.ExchangeService.CreateExchangeRateAsync(
            fixture.ContextA,
            new CreateMasterDataExchangeRateCommand(
                usd.Value!.Id,
                sar.Value!.Id,
                new DateOnly(2026, 1, 1),
                null,
                3.75m,
                2,
                ExchangeRateProvenance.Manual,
                "Treasury import"));
        Assert.True(created.Succeeded, created.Code);
        Assert.Equal(MasterDataLifecycleState.Active, created.Value!.LifecycleState);
        Assert.Equal(1, created.Value.CurrentVersionNumber);
        Assert.Equal("USD", created.Value.SourceCurrencyCode);
        Assert.Equal("SAR", created.Value.TargetCurrencyCode);
        Assert.Single(created.Value.Versions);

        var edited = await fixture.ExchangeService.EditExchangeRateAsync(
            fixture.ContextA,
            new EditMasterDataExchangeRateCommand(
                created.Value.Id,
                usd.Value.Id,
                sar.Value.Id,
                new DateOnly(2027, 1, 1),
                null,
                3.8m,
                2,
                ExchangeRateProvenance.Configured,
                "Approved internal configuration",
                created.Value.Version));
        Assert.True(edited.Succeeded, edited.Code);
        Assert.Equal(2, edited.Value!.CurrentVersionNumber);
        Assert.Equal(new DateOnly(2026, 12, 31), edited.Value.Versions[0].EffectiveTo);
        Assert.Equal(ExchangeRateProvenance.Configured, edited.Value.Versions[1].Provenance);
        Assert.Equal("USD", edited.Value.Versions[0].SourceCurrencyCode);
        Assert.Equal("SAR", edited.Value.Versions[1].TargetCurrencyCode);

        var historical = await fixture.ExchangeService.GetExchangeRateReferenceAsync(
            fixture.ContextA,
            edited.Value.Id,
            new DateOnly(2026, 6, 15));
        Assert.True(historical.Succeeded, historical.Code);
        Assert.Equal(1, historical.Value!.VersionNumber);
        Assert.Equal(3.75m, historical.Value.Version.Rate);
        Assert.Equal("USD->SAR;v1", historical.Value.Snapshot.AppliedValue);
        Assert.Equal(new DateOnly(2026, 6, 15), historical.Value.Snapshot.EffectiveOn);

        var current = await fixture.ExchangeService.GetExchangeRateReferenceAsync(
            fixture.ContextA,
            edited.Value.Id,
            new DateOnly(2027, 2, 1));
        Assert.True(current.Succeeded, current.Code);
        Assert.Equal(2, current.Value!.VersionNumber);
        Assert.Equal(3.8m, current.Value.Version.Rate);
        Assert.Equal("Approved internal configuration", current.Value.Version.SourceNotes);

        var stale = await fixture.ExchangeService.EditExchangeRateAsync(
            fixture.ContextA,
            new EditMasterDataExchangeRateCommand(
                edited.Value.Id,
                usd.Value.Id,
                sar.Value.Id,
                new DateOnly(2028, 1, 1),
                null,
                3.9m,
                2,
                ExchangeRateProvenance.Manual,
                null,
                created.Value.Version));
        Assert.False(stale.Succeeded);
        Assert.Equal("concurrency_conflict", stale.Code);

        var pairChange = await fixture.ExchangeService.EditExchangeRateAsync(
            fixture.ContextA,
            new EditMasterDataExchangeRateCommand(
                edited.Value.Id,
                sar.Value.Id,
                usd.Value.Id,
                new DateOnly(2028, 1, 1),
                null,
                0.27m,
                2,
                ExchangeRateProvenance.Manual,
                null,
                edited.Value.Version));
        Assert.False(pairChange.Succeeded);
        Assert.Equal("exchange_rate_pair_immutable", pairChange.Code);

        var tenantBList = await fixture.ExchangeService.ListExchangeRatesAsync(fixture.ContextB);
        Assert.True(tenantBList.Succeeded, tenantBList.Code);
        Assert.Empty(tenantBList.Value!);
        var hidden = await fixture.ExchangeService.GetExchangeRateAsync(fixture.ContextB, edited.Value.Id);
        Assert.False(hidden.Succeeded);
        Assert.Equal("exchange_rate_not_found", hidden.Code);

        var inactive = await fixture.ExchangeService.DeactivateExchangeRateAsync(
            fixture.ContextA,
            edited.Value.Id,
            edited.Value.Version);
        Assert.True(inactive.Succeeded, inactive.Code);
        Assert.Equal(MasterDataLifecycleState.Inactive, inactive.Value!.LifecycleState);
        var inactiveReference = await fixture.ExchangeService.GetExchangeRateReferenceAsync(
            fixture.ContextA,
            edited.Value.Id,
            new DateOnly(2027, 2, 1));
        Assert.False(inactiveReference.Succeeded);
        Assert.Equal("exchange_rate_inactive", inactiveReference.Code);

        var reactivated = await fixture.ExchangeService.ReactivateExchangeRateAsync(
            fixture.ContextA,
            edited.Value.Id,
            inactive.Value.Version);
        Assert.True(reactivated.Succeeded, reactivated.Code);

        var audit = await fixture.ExchangeService.ReadAuditHistoryAsync(fixture.ContextA, edited.Value.Id);
        Assert.True(audit.Succeeded, audit.Code);
        Assert.NotEmpty(audit.Value!);
        Assert.All(audit.Value!, entry =>
        {
            Assert.Equal(TenantA, entry.TenantId.Value);
            Assert.Equal(MasterDataResourceKind.ExchangeRate, entry.ResourceKind);
            Assert.Equal(ExchangeRateScopePolicy.PolicyId, entry.Scope!.Policy.PolicyId);
            Assert.Null(entry.Scope.OrganizationAnchor);
        });
    }

    [Fact]
    public async Task Exchange_rate_validation_and_currency_references_are_safe()
    {
        await using var fixture = await Fixture.CreateAsync();
        var usd = await fixture.CurrencyService.CreateCurrencyAsync(
            fixture.ContextA,
            new CreateMasterDataCurrencyCommand("USD", new LocalizedName("US Dollar")));
        var sar = await fixture.CurrencyService.CreateCurrencyAsync(
            fixture.ContextA,
            new CreateMasterDataCurrencyCommand("SAR", new LocalizedName("Saudi Riyal")));
        Assert.True(usd.Succeeded, usd.Code);
        Assert.True(sar.Succeeded, sar.Code);

        var badPrecision = await fixture.ExchangeService.CreateExchangeRateAsync(
            fixture.ContextA,
            new CreateMasterDataExchangeRateCommand(
                usd.Value!.Id,
                sar.Value!.Id,
                new DateOnly(2026, 1, 1),
                null,
                3.751m,
                2,
                ExchangeRateProvenance.Manual,
                null));
        Assert.False(badPrecision.Succeeded);
        Assert.Equal("validation_failed", badPrecision.Code);

        var foreignCurrency = await fixture.ExchangeService.CreateExchangeRateAsync(
            fixture.ContextB,
            new CreateMasterDataExchangeRateCommand(
                usd.Value.Id,
                sar.Value.Id,
                new DateOnly(2026, 1, 1),
                null,
                3.75m,
                2,
                ExchangeRateProvenance.Manual,
                null));
        Assert.False(foreignCurrency.Succeeded);
        Assert.Equal("exchange_rate_currency_not_found", foreignCurrency.Code);
        var list = await fixture.ExchangeService.ListExchangeRatesAsync(fixture.ContextA);
        Assert.True(list.Succeeded, list.Code);
        Assert.Empty(list.Value!);
    }

    private static MasterDataRequestContext ResolveContext(Guid tenantId, string correlation)
    {
        var foundation = FoundationRequestContext.ForTenant(
            Actor,
            Session,
            TenantContext.ForOrdinaryMembership(
                new TenantId(tenantId),
                new MembershipReference(Guid.NewGuid()),
                new ScopeReference($"Company:{tenantId:D}"),
                new CorrelationId(correlation),
                Actor),
            "master-data.exchange-rate");
        var resolution = new MasterDataTenantContextResolver().Resolve(foundation);
        return Assert.IsType<MasterDataRequestContext>(resolution.Context);
    }

    private sealed class GrantingCapabilityResolver : IMasterDataCapabilityResolver
    {
        private readonly IReadOnlySet<MasterDataCapability> capabilities = Enum.GetValues<MasterDataCapability>().ToHashSet();
        public IReadOnlySet<MasterDataCapability> Resolve(MasterDataRequestContext context) => capabilities;
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection;

        private Fixture(
            SqliteConnection connection,
            TenantContext tenantContextA,
            TenantContext tenantContextB,
            MasterDataRequestContext contextA,
            MasterDataRequestContext contextB,
            MasterDataCurrencyPaymentTermService currencyService,
            MasterDataExchangeRateService exchangeService)
        {
            this.connection = connection;
            TenantContextA = tenantContextA;
            TenantContextB = tenantContextB;
            ContextA = contextA;
            ContextB = contextB;
            CurrencyService = currencyService;
            ExchangeService = exchangeService;
        }

        public TenantContext TenantContextA { get; }
        public TenantContext TenantContextB { get; }
        public MasterDataRequestContext ContextA { get; }
        public MasterDataRequestContext ContextB { get; }
        public MasterDataCurrencyPaymentTermService CurrencyService { get; }
        public MasterDataExchangeRateService ExchangeService { get; }

        public static async Task<Fixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder().UseSqlite(connection).Options;
            var tenantContextA = TenantContext.ForOrdinaryMembership(new TenantId(TenantA), new MembershipReference(Guid.NewGuid()), new ScopeReference($"Company:{TenantA:D}"), new CorrelationId("corr-exchange-a"), Actor);
            var tenantContextB = TenantContext.ForOrdinaryMembership(new TenantId(TenantB), new MembershipReference(Guid.NewGuid()), new ScopeReference($"Company:{TenantB:D}"), new CorrelationId("corr-exchange-b"), Actor);
            await using (var db = new MasterDataDbContext(options, tenantContextA)) await db.Database.EnsureCreatedAsync();

            var resolver = new GrantingCapabilityResolver();
            var currencyPersistence = new MasterDataCurrencyPaymentTermPersistence(options);
            var currencyAuthorization = new MasterDataResourceAuthorizationService(resolver, new CurrencyPaymentTermResourcePolicy(), new CurrencyPaymentTermApprovalPolicy(), new CurrencyPaymentTermScopePolicy());
            var currencyService = new MasterDataCurrencyPaymentTermService(currencyAuthorization, currencyPersistence);
            var exchangePersistence = new MasterDataExchangeRatePersistence(options);
            var exchangeAuthorization = new MasterDataResourceAuthorizationService(resolver, new ExchangeRateResourcePolicy(), new ExchangeRateApprovalPolicy(), new ExchangeRateScopePolicy());
            var exchangeService = new MasterDataExchangeRateService(exchangeAuthorization, exchangePersistence);

            return new Fixture(connection, tenantContextA, tenantContextB, ResolveContext(TenantA, "corr-exchange-a"), ResolveContext(TenantB, "corr-exchange-b"), currencyService, exchangeService);
        }

        public ValueTask DisposeAsync() => connection.DisposeAsync();
    }
}
