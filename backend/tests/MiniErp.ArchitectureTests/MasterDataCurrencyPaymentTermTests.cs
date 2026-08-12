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

public sealed class MasterDataCurrencyPaymentTermTests
{
    private static readonly Guid TenantA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid TenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid Actor = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid Session = Guid.Parse("44444444-4444-4444-4444-444444444444");

    [Fact]
    public async Task Currency_and_payment_term_are_reusable_tenant_owned_and_audited()
    {
        await using var fixture = await Fixture.CreateAsync();

        var currency = await fixture.Service.CreateCurrencyAsync(
            fixture.ContextA,
            new CreateMasterDataCurrencyCommand(" sar ", new LocalizedName("Saudi Riyal", "ريال سعودي")));
        Assert.True(currency.Succeeded, currency.Code);
        Assert.Equal("SAR", currency.Value!.Code);
        Assert.Equal(MasterDataLifecycleState.Active, currency.Value.LifecycleState);

        var term = await fixture.Service.CreatePaymentTermAsync(
            fixture.ContextA,
            new CreateMasterDataPaymentTermCommand(
                "NET-30",
                new LocalizedName("Net 30"),
                new DateOnly(2026, 1, 1),
                null,
                PaymentTermBaseDateRule.InvoiceDate,
                PaymentTermScheduleMode.SingleDueDate,
                new MasterDataPaymentTermOffset(30, 0),
                [],
                new MasterDataEarlySettlementDiscount(true, 2m, new MasterDataPaymentTermOffset(10, 0))));
        Assert.True(term.Succeeded, term.Code);
        Assert.Equal(1, term.Value!.CurrentVersionNumber);
        Assert.Single(term.Value.Versions);

        var tenantBList = await fixture.Service.ListCurrenciesAsync(fixture.ContextB);
        Assert.True(tenantBList.Succeeded, tenantBList.Code);
        Assert.Empty(tenantBList.Value!);
        var hidden = await fixture.Service.GetPaymentTermAsync(fixture.ContextB, term.Value.Id);
        Assert.False(hidden.Succeeded);
        Assert.Equal("payment_term_not_found", hidden.Code);

        var audit = await fixture.Persistence.ReadAuditHistoryAsync(
            fixture.TenantContextA,
            MasterDataResourceKind.PaymentTerm,
            term.Value.Id);
        var entry = Assert.Single(audit);
        Assert.Equal(TenantA, entry.TenantId.Value);
        Assert.Equal(MasterDataOperation.Create, entry.Operation);
        Assert.Equal("master-data.currency-payment-terms.scope", entry.Scope!.Policy.PolicyId);
        Assert.Null(entry.Scope.OrganizationAnchor);
    }

    [Fact]
    public async Task Payment_term_versions_preserve_history_and_select_deterministic_references()
    {
        await using var fixture = await Fixture.CreateAsync();
        var created = await fixture.Service.CreatePaymentTermAsync(
            fixture.ContextA,
            new CreateMasterDataPaymentTermCommand(
                "MILESTONE",
                new LocalizedName("Milestone"),
                new DateOnly(2026, 1, 1),
                null,
                PaymentTermBaseDateRule.DocumentDate,
                PaymentTermScheduleMode.SingleDueDate,
                new MasterDataPaymentTermOffset(0, 1),
                [],
                MasterDataEarlySettlementDiscount.Disabled()));
        Assert.True(created.Succeeded, created.Code);

        var edited = await fixture.Service.EditPaymentTermAsync(
            fixture.ContextA,
            new EditMasterDataPaymentTermCommand(
                created.Value!.Id,
                "MILESTONE",
                new LocalizedName("Milestone revised"),
                new DateOnly(2027, 1, 1),
                null,
                PaymentTermBaseDateRule.InvoiceDate,
                PaymentTermScheduleMode.Installments,
                new MasterDataPaymentTermOffset(0, 0),
                [
                    new MasterDataPaymentTermInstallment(1, 40m, new MasterDataPaymentTermOffset(0, 0)),
                    new MasterDataPaymentTermInstallment(2, 60m, new MasterDataPaymentTermOffset(30, 0))
                ],
                new MasterDataEarlySettlementDiscount(true, 1.5m, new MasterDataPaymentTermOffset(10, 0)),
                created.Value.Version));
        Assert.True(edited.Succeeded, edited.Code);
        Assert.Collection(edited.Value!.Versions, _ => { }, _ => { });

        var first = edited.Value.Versions[0];
        var second = edited.Value.Versions[1];
        Assert.Equal(PaymentTermScheduleMode.SingleDueDate, first.ScheduleMode);
        Assert.Equal(new DateOnly(2026, 12, 31), first.EffectiveTo);
        Assert.Equal(2, second.VersionNumber);
        Assert.Equal(100m, second.Installments.Sum(item => item.Percentage));
        Assert.Equal("Milestone revised", edited.Value.Name.English);

        var historical = await fixture.Service.GetPaymentTermReferenceAsync(
            fixture.ContextA,
            edited.Value.Id,
            new DateOnly(2026, 6, 15));
        Assert.True(historical.Succeeded, historical.Code);
        Assert.Equal(1, historical.Value!.VersionNumber);
        Assert.Equal("MILESTONE;v1", historical.Value.Snapshot.AppliedValue);

        var current = await fixture.Service.GetPaymentTermReferenceAsync(
            fixture.ContextA,
            edited.Value.Id,
            new DateOnly(2027, 2, 1));
        Assert.True(current.Succeeded, current.Code);
        Assert.Equal(2, current.Value!.VersionNumber);
        Assert.Equal("InvoiceDate", current.Value.Version.BaseDateRule.ToString());

        var preview = await fixture.Service.PreviewPaymentTermAsync(
            fixture.ContextA,
            edited.Value.Id,
            new DateOnly(2027, 2, 1),
            new DateOnly(2027, 2, 15));
        Assert.True(preview.Succeeded, preview.Code);
        Assert.Equal(new DateOnly(2027, 2, 15), preview.Value!.DueDates[0].DueDate);
        Assert.Equal(new DateOnly(2027, 3, 17), preview.Value.DueDates[1].DueDate);
        Assert.Equal(new DateOnly(2027, 2, 25), preview.Value.EarlySettlementDiscountDate);
    }

    [Fact]
    public async Task Payment_term_rejects_non_exact_installment_totals_before_persistence()
    {
        await using var fixture = await Fixture.CreateAsync();
        var result = await fixture.Service.CreatePaymentTermAsync(
            fixture.ContextA,
            new CreateMasterDataPaymentTermCommand(
                "BAD-SPLIT",
                new LocalizedName("Bad split"),
                new DateOnly(2026, 1, 1),
                null,
                PaymentTermBaseDateRule.DocumentDate,
                PaymentTermScheduleMode.Installments,
                new MasterDataPaymentTermOffset(0, 0),
                [
                    new MasterDataPaymentTermInstallment(1, 40m, new MasterDataPaymentTermOffset(0, 0)),
                    new MasterDataPaymentTermInstallment(2, 50m, new MasterDataPaymentTermOffset(30, 0))
                ],
                MasterDataEarlySettlementDiscount.Disabled()));

        Assert.False(result.Succeeded);
        Assert.Equal("validation_failed", result.Code);
        var list = await fixture.Service.ListPaymentTermsAsync(fixture.ContextA);
        Assert.True(list.Succeeded, list.Code);
        Assert.Empty(list.Value!);
    }

    [Fact]
    public async Task Currency_and_term_mutations_are_optimistic_concurrency_and_lifecycle_bound()
    {
        await using var fixture = await Fixture.CreateAsync();
        var currency = await fixture.Service.CreateCurrencyAsync(
            fixture.ContextA,
            new CreateMasterDataCurrencyCommand("USD", new LocalizedName("US Dollar")));
        Assert.True(currency.Succeeded, currency.Code);

        var edited = await fixture.Service.EditCurrencyAsync(
            fixture.ContextA,
            new EditMasterDataCurrencyCommand(currency.Value!.Id, "USD", new LocalizedName("US Dollar revised"), currency.Value.Version));
        Assert.True(edited.Succeeded, edited.Code);
        var stale = await fixture.Service.EditCurrencyAsync(
            fixture.ContextA,
            new EditMasterDataCurrencyCommand(currency.Value.Id, "USD", new LocalizedName("Stale"), currency.Value.Version));
        Assert.False(stale.Succeeded);
        Assert.Equal("concurrency_conflict", stale.Code);

        var inactive = await fixture.Service.DeactivateCurrencyAsync(fixture.ContextA, currency.Value.Id, edited.Value!.Version);
        Assert.True(inactive.Succeeded, inactive.Code);
        Assert.Equal(MasterDataLifecycleState.Inactive, inactive.Value!.LifecycleState);
        var reference = await fixture.Service.GetCurrencyReferenceAsync(fixture.ContextA, currency.Value.Id);
        Assert.True(reference.Succeeded, reference.Code);
        Assert.Equal(3, reference.Value!.Revision);
        Assert.Equal("USD", reference.Value.Snapshot.AppliedValue);
    }

    private static MasterDataRequestContext ResolveContext(Guid tenantId, string correlation)
    {
        var foundation = FoundationRequestContext.ForTenant(
            Actor,
            Session,
            TenantContext.ForOrdinaryMembership(
                new TenantId(tenantId),
                new MembershipReference(Guid.NewGuid()),
                new ScopeReference("Company:11111111-1111-1111-1111-111111111111"),
                new CorrelationId(correlation),
                Actor),
            "master-data.currency-payment-terms");
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

        private Fixture(SqliteConnection connection, TenantContext tenantContextA, TenantContext tenantContextB, MasterDataRequestContext contextA, MasterDataRequestContext contextB, MasterDataCurrencyPaymentTermPersistence persistence, MasterDataCurrencyPaymentTermService service)
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
        public MasterDataCurrencyPaymentTermPersistence Persistence { get; }
        public MasterDataCurrencyPaymentTermService Service { get; }

        public static async Task<Fixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder().UseSqlite(connection).Options;
            var tenantContextA = TenantContext.ForOrdinaryMembership(new TenantId(TenantA), new MembershipReference(Guid.NewGuid()), new ScopeReference("Company:11111111-1111-1111-1111-111111111111"), new CorrelationId("corr-currency-a"), Actor);
            var tenantContextB = TenantContext.ForOrdinaryMembership(new TenantId(TenantB), new MembershipReference(Guid.NewGuid()), new ScopeReference("Company:22222222-2222-2222-2222-222222222222"), new CorrelationId("corr-currency-b"), Actor);
            await using (var db = new MasterDataDbContext(options, tenantContextA)) await db.Database.EnsureCreatedAsync();
            var persistence = new MasterDataCurrencyPaymentTermPersistence(options);
            var authorization = new MasterDataResourceAuthorizationService(new GrantingCapabilityResolver(), new CurrencyPaymentTermResourcePolicy(), new CurrencyPaymentTermApprovalPolicy(), new CurrencyPaymentTermScopePolicy());
            return new Fixture(connection, tenantContextA, tenantContextB, ResolveContext(TenantA, "corr-currency-a"), ResolveContext(TenantB, "corr-currency-b"), persistence, new MasterDataCurrencyPaymentTermService(authorization, persistence));
        }

        public ValueTask DisposeAsync() => connection.DisposeAsync();
    }
}
