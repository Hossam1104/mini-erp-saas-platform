#pragma warning disable CS1591

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

public sealed class TaxTests
{
    private static readonly Guid TenantA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid TenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid Actor = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid Session = Guid.Parse("44444444-4444-4444-4444-444444444444");

    [Fact]
    public async Task Tax_is_tenant_owned_effective_dated_reproducible_and_audited()
    {
        await using var fixture = await Fixture.CreateAsync();

        var created = await fixture.Service.CreateTaxAsync(
            fixture.ContextA,
            Tax("VAT-STD", "VAT", 15m, new DateOnly(2026, 1, 1)));

        Assert.True(created.Succeeded, created.Code);
        var first = Assert.IsType<MasterDataTaxRecord>(created.Value);
        Assert.Equal(TenantA, first.TenantId.Value);
        Assert.Equal(MasterDataLifecycleState.Active, first.LifecycleState);
        Assert.Single(first.RateVersions);

        var edited = await fixture.Service.EditTaxAsync(
            fixture.ContextA,
            new EditMasterDataTaxCommand(
                first.Id,
                "VAT-STD",
                "VAT",
                new LocalizedName("Value Added Tax", "\u0636\u0631\u064a\u0628\u0629 \u0627\u0644\u0642\u064a\u0645\u0629 \u0627\u0644\u0645\u0636\u0627\u0641\u0629"),
                new LocalizedName("Standard VAT", "\u0636\u0631\u064a\u0628\u0629 \u0642\u064a\u0645\u0629"),
                TaxDirection.Both,
                new MasterDataTaxRateVersion(new DateOnly(2027, 1, 1), null, 20m),
                first.Version));

        Assert.True(edited.Succeeded, edited.Code);
        var versioned = Assert.IsType<MasterDataTaxRecord>(edited.Value);
        Assert.Equal(2, versioned.CurrentVersionNumber);
        Assert.Equal(new DateOnly(2026, 12, 31), versioned.RateVersions[0].EffectiveTo);
        Assert.Equal(20m, versioned.RateVersions[1].RatePercentage);
        Assert.Equal("Standard VAT", versioned.Name.English);

        var historical = await fixture.Service.GetTaxReferenceAsync(
            fixture.ContextA,
            versioned.Id,
            new DateOnly(2026, 6, 15));
        Assert.True(historical.Succeeded, historical.Code);
        Assert.Equal(1, historical.Value!.VersionNumber);
        Assert.Equal("VAT-STD;v1", historical.Value.Snapshot.AppliedValue);

        var current = await fixture.Service.GetTaxReferenceAsync(
            fixture.ContextA,
            versioned.Id,
            new DateOnly(2027, 2, 1));
        Assert.True(current.Succeeded, current.Code);
        Assert.Equal(2, current.Value!.VersionNumber);

        var calculation = await fixture.Service.CalculateTaxAsync(
            fixture.ContextA,
            versioned.Id,
            new TaxCalculationRequest(
                new DateOnly(2026, 6, 15),
                TaxDirection.Sales,
                100m,
                " sar ",
                2,
                TaxRoundingMode.AwayFromZero,
                "sales-line-001"));
        Assert.True(calculation.Succeeded, calculation.Code);
        Assert.Equal(1, calculation.Value!.RateVersionNumber);
        Assert.Equal(15m, calculation.Value.RatePercentage);
        Assert.Equal(15m, calculation.Value.TaxAmount);
        Assert.Equal("SAR", calculation.Value.CurrencyCode);
        Assert.Equal("sales-line-001", calculation.Value.SourceLineage);

        var tenantBList = await fixture.Service.ListTaxesAsync(fixture.ContextB);
        Assert.True(tenantBList.Succeeded, tenantBList.Code);
        Assert.Empty(tenantBList.Value!);
        var foreignRead = await fixture.Service.GetTaxAsync(fixture.ContextB, versioned.Id);
        Assert.False(foreignRead.Succeeded);
        Assert.Equal("tax_not_found", foreignRead.Code);

        var audit = await fixture.Persistence.ReadAuditHistoryAsync(fixture.TenantContextA, versioned.Id);
        Assert.Equal(2, audit.Count);
        Assert.All(audit, entry =>
        {
            Assert.Equal(MasterDataResourceKind.Tax, entry.ResourceKind);
            Assert.Equal(TaxScopePolicy.PolicyId, entry.Scope!.Policy.PolicyId);
        });
    }

    [Fact]
    public async Task Tax_calculation_is_explicit_side_effect_free_and_lifecycle_bound()
    {
        await using var fixture = await Fixture.CreateAsync();
        var created = await fixture.Service.CreateTaxAsync(
            fixture.ContextA,
            Tax("VAT-SALES", "VAT", 15m, new DateOnly(2026, 1, 1)));
        Assert.True(created.Succeeded, created.Code);

        var beforeAudit = await fixture.Persistence.ReadAuditHistoryAsync(fixture.TenantContextA, created.Value!.Id);
        var result = await fixture.Service.CalculateTaxAsync(
            fixture.ContextA,
            created.Value.Id,
            new TaxCalculationRequest(
                new DateOnly(2026, 1, 1),
                TaxDirection.Sales,
                100.005m,
                "SAR",
                2,
                TaxRoundingMode.AwayFromZero,
                "document:preview"));
        Assert.True(result.Succeeded, result.Code);
        Assert.Equal(15.00m, result.Value!.TaxAmount);
        Assert.Equal(beforeAudit.Count, (await fixture.Persistence.ReadAuditHistoryAsync(fixture.TenantContextA, created.Value.Id)).Count);

        var inactive = await fixture.Service.DeactivateTaxAsync(
            fixture.ContextA,
            created.Value.Id,
            created.Value.Version);
        Assert.True(inactive.Succeeded, inactive.Code);

        var blocked = await fixture.Service.CalculateTaxAsync(
            fixture.ContextA,
            created.Value.Id,
            new TaxCalculationRequest(
                new DateOnly(2026, 1, 1),
                TaxDirection.Sales,
                100m,
                "SAR",
                2,
                TaxRoundingMode.ToEven,
                "document:inactive"));
        Assert.False(blocked.Succeeded);
        Assert.Equal("tax_inactive", blocked.Code);

        var reactivated = await fixture.Service.ReactivateTaxAsync(
            fixture.ContextA,
            created.Value.Id,
            inactive.Value!.Version);
        Assert.True(reactivated.Succeeded, reactivated.Code);
        Assert.Equal(MasterDataLifecycleState.Active, reactivated.Value!.LifecycleState);

        var stale = await fixture.Service.EditTaxAsync(
            fixture.ContextA,
            new EditMasterDataTaxCommand(
                created.Value.Id,
                "VAT-SALES",
                "VAT",
                new LocalizedName("Value Added Tax"),
                new LocalizedName("Sales VAT"),
                TaxDirection.Sales,
                new MasterDataTaxRateVersion(new DateOnly(2027, 1, 1), null, 5m),
                created.Value.Version));
        Assert.False(stale.Succeeded);
        Assert.Equal("concurrency_conflict", stale.Code);
    }

    [Fact]
    public void Tax_policy_rejects_undecided_or_ambiguous_engine_inputs()
    {
        Assert.Throws<ArgumentException>(() => MasterDataTaxValuePolicy.ValidateRateVersion(
            new MasterDataTaxRateVersion(new DateOnly(2027, 1, 1), new DateOnly(2026, 12, 31), 15m)));

        Assert.Throws<ArgumentException>(() => MasterDataTaxValuePolicy.ValidateCalculation(
            new TaxCalculationRequest(
                new DateOnly(2026, 1, 1),
                TaxDirection.Both,
                100m,
                "SAR",
                2,
                TaxRoundingMode.ToEven,
                "ambiguous")));

        Assert.Equal("SAR", MasterDataTaxValuePolicy.NormalizeCurrencyCode(" sar "));
        Assert.Equal("source:line", MasterDataTaxValuePolicy.NormalizeLineage(" source:line "));
    }

    private static CreateMasterDataTaxCommand Tax(
        string code,
        string categoryCode,
        decimal rate,
        DateOnly effectiveFrom) =>
        new(
            code,
            categoryCode,
            new LocalizedName("Value Added Tax", "\u0636\u0631\u064a\u0628\u0629 \u0627\u0644\u0642\u064a\u0645\u0629"),
            new LocalizedName("Standard VAT", "\u0636\u0631\u064a\u0628\u0629 \u0642\u064a\u0645\u0629"),
            TaxDirection.Both,
            new MasterDataTaxRateVersion(effectiveFrom, null, rate));

    private static MasterDataRequestContext ResolveContext(Guid tenantId, string correlation)
    {
        var foundation = FoundationRequestContext.ForTenant(
            Actor,
            Session,
            TenantContext.ForOrdinaryMembership(
                new TenantId(tenantId),
                new MembershipReference(Guid.NewGuid()),
                new ScopeReference($"Company:{tenantId:N}"),
                new CorrelationId(correlation),
                Actor),
            "tenant.master-data.tax.view");
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
            MasterDataTaxPersistence persistence,
            MasterDataTaxService service)
        {
            this.connection = connection;
            TenantContextA = tenantContextA;
            TenantContextB = tenantContextB;
            ContextA = contextA;
            ContextB = contextB;
            Persistence = persistence;
            Service = service;
        }

        public TenantContext TenantContextA { get; }
        public TenantContext TenantContextB { get; }
        public MasterDataRequestContext ContextA { get; }
        public MasterDataRequestContext ContextB { get; }
        public MasterDataTaxPersistence Persistence { get; }
        public MasterDataTaxService Service { get; }

        public static async Task<Fixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder().UseSqlite(connection).Options;
            var tenantContextA = TenantContext.ForOrdinaryMembership(
                new TenantId(TenantA),
                new MembershipReference(Guid.NewGuid()),
                new ScopeReference("Company:aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa"),
                new CorrelationId("corr-tax-a"),
                Actor);
            var tenantContextB = TenantContext.ForOrdinaryMembership(
                new TenantId(TenantB),
                new MembershipReference(Guid.NewGuid()),
                new ScopeReference("Company:bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb"),
                new CorrelationId("corr-tax-b"),
                Actor);
            await using (var db = new MasterDataDbContext(options, tenantContextA))
            {
                await db.Database.EnsureCreatedAsync();
            }

            var persistence = new MasterDataTaxPersistence(options);
            var authorization = new MasterDataResourceAuthorizationService(
                new GrantingCapabilityResolver(),
                new TaxResourcePolicy(),
                new TaxApprovalPolicy(),
                new TaxScopePolicy());
            return new Fixture(
                connection,
                tenantContextA,
                tenantContextB,
                ResolveContext(TenantA, "corr-tax-a"),
                ResolveContext(TenantB, "corr-tax-b"),
                persistence,
                new MasterDataTaxService(authorization, persistence));
        }

        public ValueTask DisposeAsync() => connection.DisposeAsync();
    }
}

#pragma warning restore CS1591
