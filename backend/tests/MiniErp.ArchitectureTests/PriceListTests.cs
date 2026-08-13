using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MiniErp.App.BuildingBlocks.Rest;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.Modules.MasterData;
using MiniErp.Contracts.Modules.Foundation;
using MiniErp.Contracts.Modules.MasterData;
using MiniErp.Infrastructure.Persistence.Modules.MasterData;
using Xunit;

namespace MiniErp.ArchitectureTests;

public sealed class PriceListTests
{
    private static readonly Guid TenantA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid TenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid Actor = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid Session = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid CompanyA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherCompany = Guid.Parse("99999999-9999-9999-9999-999999999999");

    [Fact]
    public async Task Price_list_schema_can_be_created()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder().UseSqlite(connection).Options;
        var tenant = TenantContext.ForOrdinaryMembership(
            new TenantId(TenantA),
            new MembershipReference(Guid.NewGuid()),
            new ScopeReference($"Company:{CompanyA:D}"),
            new CorrelationId("price-list-schema"),
            Actor);

        await using var db = new MasterDataDbContext(options, tenant);
        Assert.True(await db.Database.EnsureCreatedAsync());
    }

    [Fact]
    public async Task Price_list_resolution_is_deterministic_versioned_and_tenant_bound()
    {
        await using var fixture = await Fixture.CreateAsync();

        var standard = await fixture.Service.CreatePriceListAsync(
            fixture.ContextA,
            new CreateMasterDataPriceListCommand(
                "STANDARD",
                new LocalizedName("Standard"),
                fixture.CurrencyId,
                null,
                null,
                null,
                10));
        var fallback = await fixture.Service.CreatePriceListAsync(
            fixture.ContextA,
            new CreateMasterDataPriceListCommand(
                "FALLBACK",
                new LocalizedName("Fallback"),
                fixture.CurrencyId,
                null,
                null,
                null,
                20));
        Assert.True(standard.Succeeded, standard.Code);
        Assert.True(fallback.Succeeded, fallback.Code);
        Assert.Equal(0, standard.Value!.CurrentVersionNumber);

        var standardPrice = await fixture.Service.AppendPriceAsync(
            fixture.ContextA,
            Price(standard.Value.Id, fixture.ProductId, fixture.UnitId, 100m, standard.Value.Version));
        var fallbackPrice = await fixture.Service.AppendPriceAsync(
            fixture.ContextA,
            Price(fallback.Value!.Id, fixture.ProductId, fixture.UnitId, 90m, fallback.Value.Version));
        Assert.True(standardPrice.Succeeded, standardPrice.Code);
        Assert.True(fallbackPrice.Succeeded, fallbackPrice.Code);
        Assert.Equal(1, standardPrice.Value!.CurrentVersionNumber);
        Assert.Equal(1, standardPrice.Value.Prices.Single().VersionNumber);

        var manualSourceRequired = await fixture.Service.CreatePriceListAsync(
            fixture.ContextA,
            new CreateMasterDataPriceListCommand(
                "MANUAL-SOURCE-REQUIRED",
                new LocalizedName("Manual source required"),
                fixture.CurrencyId,
                null,
                null,
                null,
                30));
        Assert.True(manualSourceRequired.Succeeded, manualSourceRequired.Code);
        var missingManualSource = await fixture.Service.AppendPriceAsync(
            fixture.ContextA,
            Price(manualSourceRequired.Value!.Id, fixture.ProductId, fixture.UnitId, 50m, manualSourceRequired.Value.Version, sourceReference: null));
        Assert.False(missingManualSource.Succeeded);
        Assert.Equal("validation_failed", missingManualSource.Code);

        var resolutionQuery = new ResolveMasterDataPriceQuery(
                null,
                fixture.ProductId,
                fixture.UnitId,
                fixture.CurrencyId,
                null,
                null,
                null,
                new DateOnly(2026, 6, 15));
        var resolved = await fixture.Service.ResolvePriceAsync(fixture.ContextA, resolutionQuery);
        Assert.True(resolved.Succeeded, resolved.Code);
        Assert.Equal(standard.Value.Id, resolved.Value!.PriceListId);
        Assert.Equal(10, resolved.Value.Price.Priority);
        Assert.Equal(100m, resolved.Value.Price.Price);
        Assert.Equal(1, resolved.Value.Price.VersionNumber);
        Assert.Equal(new DateOnly(2026, 6, 15), resolved.Value.Snapshot.EffectiveOn);
        Assert.Contains($"pl={standard.Value.Id:N}", resolved.Value.Snapshot.AppliedValue, StringComparison.Ordinal);

        var overlap = await fixture.Service.AppendPriceAsync(
            fixture.ContextA,
            Price(standard.Value.Id, fixture.ProductId, fixture.UnitId, 101m, standardPrice.Value.Version));
        Assert.False(overlap.Succeeded);
        Assert.Equal("price_list_effective_overlap", overlap.Code);

        var stale = await fixture.Service.AppendPriceAsync(
            fixture.ContextA,
            Price(standard.Value.Id, fixture.ProductId, fixture.UnitId, 102m, standard.Value.Version));
        Assert.False(stale.Succeeded);
        Assert.Equal("concurrency_conflict", stale.Code);

        var equalPriority = await fixture.Service.CreatePriceListAsync(
            fixture.ContextA,
            new CreateMasterDataPriceListCommand(
                "EQUAL-PRIORITY",
                new LocalizedName("Equal priority"),
                fixture.CurrencyId,
                null,
                null,
                null,
                10));
        Assert.True(equalPriority.Succeeded, equalPriority.Code);
        var conflictingPrice = await fixture.Service.AppendPriceAsync(
            fixture.ContextA,
            Price(equalPriority.Value!.Id, fixture.ProductId, fixture.UnitId, 80m, equalPriority.Value.Version));
        Assert.False(conflictingPrice.Succeeded);
        Assert.Equal("price_list_precedence_conflict", conflictingPrice.Code);

        var tenantBList = await fixture.Service.ListPriceListsAsync(fixture.ContextB, null);
        Assert.True(tenantBList.Succeeded, tenantBList.Code);
        Assert.Empty(tenantBList.Value!);
        var hidden = await fixture.Service.GetPriceListAsync(fixture.ContextB, standard.Value.Id);
        Assert.False(hidden.Succeeded);
        Assert.Equal("price_list_not_found", hidden.Code);

        var inactive = await fixture.Service.DeactivatePriceListAsync(
            fixture.ContextA,
            standard.Value.Id,
            standardPrice.Value.Version);
        Assert.True(inactive.Succeeded, inactive.Code);
        var inactiveReference = await fixture.Service.ResolvePriceAsync(
            fixture.ContextA,
            new ResolveMasterDataPriceQuery(
                standard.Value.Id,
                fixture.ProductId,
                fixture.UnitId,
                fixture.CurrencyId,
                null,
                null,
                null,
                new DateOnly(2026, 6, 15)));
        Assert.False(inactiveReference.Succeeded);
        Assert.Equal("price_list_inactive", inactiveReference.Code);

        var audit = await fixture.Service.ReadAuditHistoryAsync(fixture.ContextA, standard.Value.Id);
        Assert.True(audit.Succeeded, audit.Code);
        Assert.NotEmpty(audit.Value!);
        Assert.All(audit.Value!, entry =>
        {
            Assert.Equal(TenantA, entry.TenantId.Value);
            Assert.Equal(MasterDataResourceKind.PriceList, entry.ResourceKind);
            Assert.Equal(PriceListScopePolicy.PolicyId, entry.Scope!.Policy.PolicyId);
        });
        var resolutionAudit = Assert.Single(
            audit.Value!,
            entry => entry.AfterSummary?.Contains("result=resolved", StringComparison.Ordinal) == true);
        Assert.Equal(standard.Value.Id, resolutionAudit.ResourceId);
        Assert.Contains($"selected-price-list={standard.Value.Id:N}", resolutionAudit.AfterSummary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Price_list_organization_applicability_is_server_scoped_and_not_cross_scope_visible()
    {
        await using var fixture = await Fixture.CreateAsync();
        var scoped = await fixture.Service.CreatePriceListAsync(
            fixture.ContextA,
            new CreateMasterDataPriceListCommand(
                "COMPANY-A",
                new LocalizedName("Company A"),
                fixture.CurrencyId,
                null,
                OrganizationScopeKind.Company,
                CompanyA,
                5));
        Assert.True(scoped.Succeeded, scoped.Code);

        var price = await fixture.Service.AppendPriceAsync(
            fixture.ContextA,
            Price(scoped.Value!.Id, fixture.ProductId, fixture.UnitId, 75m, scoped.Value.Version));
        Assert.True(price.Succeeded, price.Code);

        var visible = await fixture.Service.ListPriceListsAsync(fixture.ContextA, "COMPANY-A");
        Assert.True(visible.Succeeded, visible.Code);
        Assert.Contains(visible.Value!, item => item.Id == scoped.Value.Id);

        var otherScope = await fixture.Service.ListPriceListsAsync(fixture.ContextOtherScope, null);
        Assert.True(otherScope.Succeeded, otherScope.Code);
        Assert.DoesNotContain(otherScope.Value!, item => item.Id == scoped.Value.Id);

        var hidden = await fixture.Service.GetPriceListAsync(fixture.ContextOtherScope, scoped.Value.Id);
        Assert.False(hidden.Succeeded);
        Assert.Equal("price_list_not_found", hidden.Code);

        var unauthorizedLifecycle = await fixture.Service.DeactivatePriceListAsync(
            fixture.ContextOtherScope,
            scoped.Value.Id,
            price.Value!.Version);
        Assert.False(unauthorizedLifecycle.Succeeded);
        Assert.Equal("organization_scope_denied", unauthorizedLifecycle.Code);

        var invalidRequestedScope = await fixture.Service.ResolvePriceAsync(
            fixture.ContextOtherScope,
            new ResolveMasterDataPriceQuery(
                null,
                fixture.ProductId,
                fixture.UnitId,
                fixture.CurrencyId,
                null,
                OrganizationScopeKind.Company,
                CompanyA,
                new DateOnly(2026, 6, 15)));
        Assert.False(invalidRequestedScope.Succeeded);
        Assert.Equal("validation_failed", invalidRequestedScope.Code);

        var hiddenReference = await fixture.Service.ResolvePriceAsync(
            fixture.ContextOtherScope,
            new ResolveMasterDataPriceQuery(
                scoped.Value.Id,
                fixture.ProductId,
                fixture.UnitId,
                fixture.CurrencyId,
                null,
                null,
                null,
                new DateOnly(2026, 6, 15)));
        Assert.False(hiddenReference.Succeeded);
        Assert.Equal("price_list_not_found", hiddenReference.Code);

        var hiddenAudit = await fixture.Service.ReadAuditHistoryAsync(fixture.ContextOtherScope, scoped.Value.Id);
        Assert.True(hiddenAudit.Succeeded, hiddenAudit.Code);
        Assert.Empty(hiddenAudit.Value!);
    }

    private static AppendMasterDataPriceCommand Price(
        Guid priceListId,
        Guid productId,
        Guid unitOfMeasureId,
        decimal value,
        byte[] expectedVersion,
        string? sourceReference = "MESP-121 test fixture") =>
        new(
            priceListId,
            productId,
            unitOfMeasureId,
            new DateOnly(2026, 1, 1),
            null,
            value,
            2,
            PriceListProvenance.Manual,
            sourceReference,
            expectedVersion);

    private sealed class GrantingCapabilityResolver : IMasterDataCapabilityResolver
    {
        private readonly IReadOnlySet<MasterDataCapability> capabilities =
            Enum.GetValues<MasterDataCapability>().ToHashSet();

        public IReadOnlySet<MasterDataCapability> Resolve(MasterDataRequestContext context) => capabilities;
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection;

        private Fixture(
            SqliteConnection connection,
            TenantContext tenantContextA,
            TenantContext tenantContextB,
            TenantContext tenantContextOtherScope,
            MasterDataRequestContext contextA,
            MasterDataRequestContext contextB,
            MasterDataRequestContext contextOtherScope,
            MasterDataPriceListService service,
            Guid currencyId,
            Guid productId,
            Guid unitId)
        {
            this.connection = connection;
            TenantContextA = tenantContextA;
            TenantContextB = tenantContextB;
            TenantContextOtherScope = tenantContextOtherScope;
            ContextA = contextA;
            ContextB = contextB;
            ContextOtherScope = contextOtherScope;
            Service = service;
            CurrencyId = currencyId;
            ProductId = productId;
            UnitId = unitId;
        }

        public TenantContext TenantContextA { get; }
        public TenantContext TenantContextB { get; }
        public TenantContext TenantContextOtherScope { get; }
        public MasterDataRequestContext ContextA { get; }
        public MasterDataRequestContext ContextB { get; }
        public MasterDataRequestContext ContextOtherScope { get; }
        public MasterDataPriceListService Service { get; }
        public Guid CurrencyId { get; }
        public Guid ProductId { get; }
        public Guid UnitId { get; }

        public static async Task<Fixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder().UseSqlite(connection).Options;
            var tenantContextA = TenantContext.ForOrdinaryMembership(
                new TenantId(TenantA),
                new MembershipReference(Guid.NewGuid()),
                new ScopeReference($"Company:{CompanyA:D}"),
                new CorrelationId("price-list-a"),
                Actor);
            var tenantContextB = TenantContext.ForOrdinaryMembership(
                new TenantId(TenantB),
                new MembershipReference(Guid.NewGuid()),
                new ScopeReference($"Company:{Guid.Parse("22222222-2222-2222-2222-222222222222"):D}"),
                new CorrelationId("price-list-b"),
                Actor);
            var tenantContextOtherScope = TenantContext.ForOrdinaryMembership(
                new TenantId(TenantA),
                new MembershipReference(Guid.NewGuid()),
                new ScopeReference($"Company:{OtherCompany:D}"),
                new CorrelationId("price-list-other-scope"),
                Actor);

            await using (var db = new MasterDataDbContext(options, tenantContextA))
            {
                await db.Database.EnsureCreatedAsync();
                var currencyId = Guid.Parse("aaaaaaaa-1111-1111-1111-111111111111");
                var categoryId = Guid.Parse("aaaaaaaa-2222-2222-2222-222222222222");
                var unitId = Guid.Parse("aaaaaaaa-3333-3333-3333-333333333333");
                var productId = Guid.Parse("aaaaaaaa-4444-4444-4444-444444444444");
                db.Currencies.Add(new MasterDataCurrencyEntity(
                    currencyId,
                    tenantContextA.TenantId,
                    "USD",
                    new LocalizedName("US Dollar")));
                db.Categories.Add(new MasterDataCategoryEntity(
                    categoryId,
                    tenantContextA.TenantId,
                    "GENERAL",
                    new LocalizedName("General"),
                    null));
                db.UnitsOfMeasure.Add(new MasterDataUnitOfMeasureEntity(
                    unitId,
                    tenantContextA.TenantId,
                    "EA",
                    new LocalizedName("Each")));
                db.Products.Add(new MasterDataProductEntity(
                    productId,
                    tenantContextA.TenantId,
                    "SKU-001",
                    new LocalizedName("Test product"),
                    null,
                    categoryId,
                    unitId,
                    null,
                    true,
                    true,
                    true));
                await db.SaveChangesAsync();
            }

            var resolver = new GrantingCapabilityResolver();
            var authorization = new MasterDataResourceAuthorizationService(
                resolver,
                new PriceListResourcePolicy(),
                new PriceListApprovalPolicy(),
                new PriceListScopePolicy());
            var service = new MasterDataPriceListService(
                authorization,
                new MasterDataPriceListPersistence(options));

            return new Fixture(
                connection,
                tenantContextA,
                tenantContextB,
                tenantContextOtherScope,
                ResolveContext(tenantContextA, "master-data.price-list"),
                ResolveContext(tenantContextB, "master-data.price-list"),
                ResolveContext(tenantContextOtherScope, "master-data.price-list"),
                service,
                Guid.Parse("aaaaaaaa-1111-1111-1111-111111111111"),
                Guid.Parse("aaaaaaaa-4444-4444-4444-444444444444"),
                Guid.Parse("aaaaaaaa-3333-3333-3333-333333333333"));
        }

        private static MasterDataRequestContext ResolveContext(TenantContext tenantContext, string permission) =>
            Assert.IsType<MasterDataRequestContext>(
                new MasterDataTenantContextResolver().Resolve(
                    FoundationRequestContext.ForTenant(
                        Actor,
                        Session,
                        tenantContext,
                        permission)).Context);

        public ValueTask DisposeAsync() => connection.DisposeAsync();
    }
}
