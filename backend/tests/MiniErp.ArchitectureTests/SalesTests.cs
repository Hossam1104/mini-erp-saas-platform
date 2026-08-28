using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MiniErp.App.BuildingBlocks.Rest;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.Modules.BusinessParties;
using MiniErp.App.Modules.Finance;
using MiniErp.App.Modules.MasterData;
using MiniErp.App.Modules.Procurement;
using MiniErp.App.Modules.Sales;
using MiniErp.Contracts.Modules.Audit;
using MiniErp.Contracts.Modules.Finance;
using MiniErp.Contracts.Modules.Foundation;
using MiniErp.Contracts.Modules.MasterData;
using MiniErp.Contracts.Modules.Sales;
using MiniErp.Infrastructure.Persistence.Modules.Sales;
using Xunit;

namespace MiniErp.ArchitectureTests;

public sealed class SalesTests
{
    private static readonly Guid TenantA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid TenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid CompanyA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid CompanyB = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid BranchA = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid CustomerA = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid ProductA = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly Guid UomA = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private static readonly Guid CurrencyA = Guid.Parse("77777777-7777-7777-7777-777777777777");
    private static readonly Guid PriceListA = Guid.Parse("88888888-8888-8888-8888-888888888888");
    private static readonly Guid TaxA = Guid.Parse("aaaaaaaa-1111-1111-1111-111111111111");
    private static readonly Guid ExchangeRateA = Guid.Parse("bbbbbbbb-1111-1111-1111-111111111111");
    private static readonly Guid Creator = Guid.Parse("99999999-9999-9999-9999-999999999999");
    private static readonly Guid Approver = Guid.Parse("12121212-1212-1212-1212-121212121212");

    [Fact]
    public async Task Quotation_revision_history_is_immutable_and_stale_edits_fail()
    {
        await using var fixture = await Fixture.CreateAsync();
        var created = await fixture.Persistence.CreateQuotationAsync(fixture.Context(Creator), fixture.Model(), "create-1", "fingerprint-1", fixture.Policy());
        Assert.True(created.Succeeded, created.Code);

        var editedModel = fixture.Model(created.Value!.Id) with { CustomerReference = "revised-reference", Reason = "commercial correction" };
        var edited = await fixture.Persistence.EditQuotationAsync(fixture.Context(Creator), created.Value.Id, editedModel, created.Value.Version, "edit-1", "fingerprint-2");
        Assert.True(edited.Succeeded, edited.Code);
        Assert.Equal(2, edited.Value!.RevisionNumber);
        Assert.Equal("revised-reference", edited.Value.CustomerReference);

        var stale = await fixture.Persistence.EditQuotationAsync(fixture.Context(Creator), created.Value.Id, editedModel, created.Value.Version, "edit-2", "fingerprint-3");
        Assert.False(stale.Succeeded);
        Assert.Equal("concurrency_conflict", stale.Code);

        var revisions = await fixture.Persistence.ListQuotationRevisionsAsync(fixture.Context(Creator), created.Value.Id);
        Assert.Equal(2, revisions.Count);
        Assert.Contains(revisions, item => item.RevisionNumber == 1 && item.Snapshot.CustomerReference is null);
        Assert.Contains(revisions, item => item.RevisionNumber == 2 && item.Snapshot.CustomerReference == "revised-reference");
        Assert.NotEqual(revisions[0].SnapshotHash, revisions[1].SnapshotHash);
    }

    [Fact]
    public async Task Quote_to_order_preserves_source_revision_and_idempotent_retry_creates_one_order()
    {
        await using var fixture = await Fixture.CreateAsync();
        var created = await fixture.Persistence.CreateQuotationAsync(fixture.Context(Creator), fixture.Model(), "create-2", "fingerprint-1", fixture.Policy());
        Assert.True(created.Succeeded, created.Code);
        var submitted = await fixture.Persistence.TransitionQuotationAsync(fixture.Context(Creator), created.Value!.Id, SalesQuotationStatus.PendingApproval, null, created.Value.Version, "submit-2", "fingerprint-2", fixture.Policy());
        Assert.True(submitted.Succeeded, submitted.Code);
        var approved = await fixture.Persistence.TransitionQuotationAsync(fixture.Context(Approver), created.Value.Id, SalesQuotationStatus.Approved, "approved", submitted.Value!.Version, "approve-2", "fingerprint-3", fixture.Policy());
        Assert.True(approved.Succeeded, approved.Code);

        var converted = await fixture.Persistence.ConvertQuotationAsync(fixture.Context(Approver), created.Value.Id, approved.Value!.Version, "convert-2", "fingerprint-4", fixture.Policy());
        Assert.True(converted.Succeeded, converted.Code);
        Assert.Equal(created.Value.Id, converted.Value!.SourceQuotationId);
        Assert.Equal(1, converted.Value.SourceQuotationRevision);
        Assert.Equal(approved.Value.Lines.Single().PriceSourceReference, converted.Value.Lines.Single().PriceSourceReference);

        var replay = await fixture.Persistence.ConvertQuotationAsync(fixture.Context(Approver), created.Value.Id, approved.Value.Version, "convert-2", "fingerprint-4", fixture.Policy());
        Assert.True(replay.Succeeded, replay.Code);
        Assert.Equal(converted.Value.Id, replay.Value!.Id);
        Assert.Single(await fixture.Persistence.ListOrdersAsync(fixture.Context(Approver), CompanyA, null));

        var competing = await fixture.Persistence.ConvertQuotationAsync(fixture.Context(Approver), created.Value.Id, approved.Value.Version, "convert-3", "fingerprint-5", fixture.Policy());
        Assert.False(competing.Succeeded);
        Assert.Equal("concurrency_conflict", competing.Code);
    }

    [Fact]
    public async Task Expired_quote_cannot_convert_even_when_approved()
    {
        await using var fixture = await Fixture.CreateAsync();
        var expiredModel = fixture.Model() with
        {
            QuotationDate = new DateOnly(2025, 1, 1),
            ValidUntil = new DateOnly(2025, 1, 31)
        };
        var created = await fixture.Persistence.CreateQuotationAsync(fixture.Context(Creator), expiredModel, "create-3", "fingerprint-1", fixture.Policy());
        Assert.True(created.Succeeded, created.Code);
        var submitted = await fixture.Persistence.TransitionQuotationAsync(fixture.Context(Creator), created.Value!.Id, SalesQuotationStatus.PendingApproval, null, created.Value.Version, "submit-3", "fingerprint-2", fixture.Policy());
        Assert.True(submitted.Succeeded, submitted.Code);
        var approved = await fixture.Persistence.TransitionQuotationAsync(fixture.Context(Approver), created.Value.Id, SalesQuotationStatus.Approved, null, submitted.Value!.Version, "approve-3", "fingerprint-3", fixture.Policy());
        Assert.True(approved.Succeeded, approved.Code);

        var converted = await fixture.Persistence.ConvertQuotationAsync(fixture.Context(Approver), created.Value.Id, approved.Value!.Version, "convert-3", "fingerprint-4", fixture.Policy());
        Assert.False(converted.Succeeded);
        Assert.Equal("quotation_expired", converted.Code);
        Assert.Empty(await fixture.Persistence.ListOrdersAsync(fixture.Context(Approver), CompanyA, null));
    }

    [Fact]
    public async Task Tenant_query_filters_prevent_cross_tenant_quote_and_order_visibility()
    {
        await using var fixture = await Fixture.CreateAsync();
        var created = await fixture.Persistence.CreateQuotationAsync(fixture.Context(Creator), fixture.Model(), "create-4", "fingerprint-1", fixture.Policy());
        Assert.True(created.Succeeded, created.Code);

        var foreignContext = fixture.Context(Creator, TenantB, CompanyB);
        Assert.Null(await fixture.Persistence.GetQuotationAsync(foreignContext, created.Value!.Id));
        Assert.Empty(await fixture.Persistence.ListQuotationsAsync(foreignContext, null, null));
        Assert.Null(await fixture.Persistence.GetOrderAsync(foreignContext, created.Value.Id));
    }

    [Fact]
    public async Task Trusted_company_scope_filters_same_tenant_quote_visibility()
    {
        await using var fixture = await Fixture.CreateAsync();
        var created = await fixture.Persistence.CreateQuotationAsync(fixture.Context(Creator), fixture.Model(), "create-company-scope", "fingerprint-1", fixture.Policy());
        Assert.True(created.Succeeded, created.Code);

        var otherCompanyContext = fixture.Context(Creator, TenantA, CompanyB);

        Assert.Null(await fixture.Persistence.GetQuotationAsync(otherCompanyContext, created.Value!.Id));
        Assert.Empty(await fixture.Persistence.ListQuotationsAsync(otherCompanyContext, null, null));
    }

    [Fact]
    public async Task History_and_audit_capture_approval_conversion_and_credit_evidence()
    {
        await using var fixture = await Fixture.CreateAsync();
        var created = await fixture.Persistence.CreateQuotationAsync(fixture.Context(Creator), fixture.Model(), "create-5", "fingerprint-1", fixture.Policy());
        Assert.True(created.Succeeded, created.Code);
        var submitted = await fixture.Persistence.TransitionQuotationAsync(fixture.Context(Creator), created.Value!.Id, SalesQuotationStatus.PendingApproval, null, created.Value.Version, "submit-5", "fingerprint-2", fixture.Policy());
        Assert.True(submitted.Succeeded, submitted.Code);
        var approved = await fixture.Persistence.TransitionQuotationAsync(fixture.Context(Approver), created.Value.Id, SalesQuotationStatus.Approved, "approved", submitted.Value!.Version, "approve-5", "fingerprint-3", fixture.Policy());
        Assert.True(approved.Succeeded, approved.Code);
        var converted = await fixture.Persistence.ConvertQuotationAsync(fixture.Context(Approver), created.Value.Id, approved.Value!.Version, "convert-5", "fingerprint-4", fixture.Policy());
        Assert.True(converted.Succeeded, converted.Code);

        var quotationHistory = await fixture.Persistence.ListHistoryAsync(fixture.Context(Approver), "quotation", created.Value.Id);
        Assert.Contains(quotationHistory, item => item.Action == nameof(SalesHistoryAction.Created));
        Assert.Contains(quotationHistory, item => item.Action == nameof(SalesHistoryAction.Submitted) && item.PolicyId == fixture.Policy().PolicyId && item.PolicyVersion == fixture.Policy().Version);
        Assert.Contains(quotationHistory, item => item.Action == nameof(SalesHistoryAction.Approved));
        Assert.Contains(quotationHistory, item => item.Action == nameof(SalesHistoryAction.Converted));

        var orderAudit = await fixture.Persistence.ListAuditAsync(fixture.Context(Approver), "order", converted.Value!.Id);
        Assert.Contains(orderAudit, item => item.OperationId == "sales.quotation.convert" && item.Decision == "Allowed");
    }

    [Fact]
    public async Task Configured_commercial_authority_delegation_and_credit_are_scope_and_time_bounded()
    {
        var now = DateTimeOffset.UtcNow;
        var scope = new SalesScope(TenantA, CompanyA, BranchA);
        var authority = new SalesCommercialAuthority(TenantA, CompanyA, BranchA, "quotation", 15m, true, "sales-authority-1", 3, [Approver], now.AddMinutes(-1), now.AddMinutes(10));
        var authorityProvider = new ConfiguredSalesCommercialAuthorityProvider([authority]);
        var context = Context(Approver, TenantA, CompanyA);
        Assert.Equal(authority, await authorityProvider.ResolveAsync(context, scope, "quotation", Approver, now));
        Assert.Null(await authorityProvider.ResolveAsync(context, scope, "quotation", Creator, now));
        Assert.Null(await authorityProvider.ResolveAsync(context, scope, "quotation", Approver, now.AddDays(1)));

        var stage = new SalesApprovalStageDefinition("commercial", 1, 1, [Creator], true);
        var delegation = new SalesApprovalDelegation(TenantA, CompanyA, BranchA, "quotation", "commercial", Creator, Approver, now.AddMinutes(-5), now.AddMinutes(5));
        var delegationProvider = new ConfiguredSalesApprovalDelegationProvider([delegation]);
        Assert.Equal(delegation, await delegationProvider.ResolveAsync(context, scope, "quotation", stage, Approver, now));
        Assert.Null(await delegationProvider.ResolveAsync(context, scope, "quotation", stage, Approver, now.AddHours(1)));
        Assert.Null(await delegationProvider.ResolveAsync(context, new SalesScope(TenantB, CompanyA, BranchA), "quotation", stage, Approver, now));

        var limits = new ConfiguredSalesCreditLimitProvider([
            new SalesCreditLimit(TenantA, CompanyA, CustomerA, "SAR", 100m, new DateOnly(2026, 1, 1), null),
            new SalesCreditLimit(TenantA, CompanyA, CustomerA, "SAR", 250m, new DateOnly(2026, 8, 1), null)
        ]);
        Assert.Equal(250m, await limits.ResolveLimitAsync(context, CompanyA, CustomerA, "sar", new DateOnly(2026, 8, 28)));
        Assert.Null(await limits.ResolveLimitAsync(context, CompanyB, CustomerA, "SAR", new DateOnly(2026, 8, 28)));
    }

    [Fact]
    public void Sales_authorization_reuses_foundation_catalogue_and_scope_rules()
    {
        var authorization = new SalesAuthorizationService(new PurchaseRequestAuthorizationService());
        var context = Context(Creator, TenantA, CompanyA, "tenant.sales.quotation.create");
        Assert.True(authorization.Authorize(context, "sales.quotation.create", new SalesScope(TenantA, CompanyA, BranchA)));
        Assert.False(authorization.Authorize(context, "sales.quotation.create", new SalesScope(TenantB, CompanyA, BranchA)));
        Assert.False(authorization.Authorize(Context(Creator, TenantA, CompanyA, "tenant.sales.order.confirm"), "sales.quotation.create", new SalesScope(TenantA, CompanyA, BranchA)));
    }

    [Fact]
    public async Task Sales_service_rebuilds_server_totals_and_denies_manual_price_without_authority()
    {
        var persistence = new CapturingSalesPersistence();
        var service = new SalesService(
            persistence,
            new SalesAuthorizationService(new PurchaseRequestAuthorizationService()),
            new DefaultSalesApprovalPolicyProvider(),
            new NoSalesCommercialAuthorityProvider(),
            new NoSalesApprovalDelegationProvider(),
            new NoSalesCreditLimitProvider(),
            new UnavailableFinanceSettlementPersistence(),
            new ConfiguredFinanceCompanyProvider([new FinanceCompanyOption(TenantA, CompanyA, "Company A", "SAR", BranchA)]),
            new CustomerReferenceFake(),
            new ProductReferenceFake(),
            new PriceReferenceFake(),
            new UnavailableSalesTaxReferenceProvider(),
            new UnavailableSalesExchangeRateReferenceProvider());

        var request = new SalesQuotationCreateRequest(
            CompanyA,
            BranchA,
            CustomerA,
            new DateOnly(2026, 8, 28),
            new DateOnly(2026, 9, 30),
            CurrencyA,
            PriceListA,
            null,
            null,
            null,
            [new SalesQuotationLineRequest(ProductA, UomA, 2m)]);

        var created = await service.CreateQuotationAsync(Context(Creator), request, "service-create-1");

        Assert.True(created.Succeeded, created.Code);
        Assert.NotNull(persistence.Captured);
        Assert.Equal(100m, persistence.Captured!.Total);
        Assert.Equal(50m, persistence.Captured.Lines.Single().UnitPrice);
        Assert.Equal(100m, persistence.Captured.Lines.Single().LineTotal);
        Assert.False(persistence.Captured.Lines.Single().ManualPriceApplied);

        var manualPrice = request with
        {
            Lines = [request.Lines[0] with { UnitPriceOverride = 40m }]
        };
        var rejected = await service.CreateQuotationAsync(Context(Creator), manualPrice, "service-create-2");

        Assert.False(rejected.Succeeded);
        Assert.Equal("commercial_reference_invalid", rejected.Code);
    }

    [Fact]
    public async Task Sales_service_snapshots_tax_and_exchange_evidence_from_existing_contracts()
    {
        var persistence = new CapturingSalesPersistence();
        var service = new SalesService(
            persistence,
            new SalesAuthorizationService(new PurchaseRequestAuthorizationService()),
            new DefaultSalesApprovalPolicyProvider(),
            new NoSalesCommercialAuthorityProvider(),
            new NoSalesApprovalDelegationProvider(),
            new NoSalesCreditLimitProvider(),
            new UnavailableFinanceSettlementPersistence(),
            new ConfiguredFinanceCompanyProvider([new FinanceCompanyOption(TenantA, CompanyA, "Company A", "SAR", BranchA)]),
            new CustomerReferenceFake(),
            new ProductReferenceFake(),
            new PriceReferenceFake("USD"),
            new TaxReferenceFake(),
            new ExchangeRateReferenceFake());

        var request = new SalesQuotationCreateRequest(
            CompanyA,
            BranchA,
            CustomerA,
            new DateOnly(2026, 8, 28),
            new DateOnly(2026, 9, 30),
            CurrencyA,
            PriceListA,
            null,
            null,
            null,
            [new SalesQuotationLineRequest(ProductA, UomA, 2m, null, 0m, null, TaxA)],
            ExchangeRateA);

        var created = await service.CreateQuotationAsync(Context(Creator), request, "service-tax-fx-1");

        Assert.True(created.Succeeded, created.Code);
        Assert.NotNull(persistence.Captured);
        Assert.Equal(100m, persistence.Captured!.Subtotal);
        Assert.Equal(15m, persistence.Captured.TaxAmount);
        Assert.Equal(115m, persistence.Captured.Total);
        Assert.Equal(TaxA, persistence.Captured.Lines.Single().TaxEvidence!.TaxId);
        Assert.Equal(ExchangeRateA, persistence.Captured.ExchangeRateEvidence!.ExchangeRateId);
        Assert.Equal("USD", persistence.Captured.ExchangeRateEvidence.SourceCurrencyCode);
        Assert.Equal("SAR", persistence.Captured.ExchangeRateEvidence.TargetCurrencyCode);
    }

    private static ProcurementRequestContext Context(Guid actor, Guid tenantId = default, Guid companyId = default, string permission = "tenant.sales.quotation.create")
    {
        tenantId = tenantId == Guid.Empty ? TenantA : tenantId;
        companyId = companyId == Guid.Empty ? CompanyA : companyId;
        var tenantContext = TenantContext.ForOrdinaryMembership(new TenantId(tenantId), new MembershipReference(Guid.NewGuid()), new ScopeReference($"Company:{companyId:D}"), new CorrelationId($"sales-{Guid.NewGuid():N}"), actor);
        var foundation = FoundationRequestContext.ForTenant(actor, Guid.NewGuid(), tenantContext, permission);
        var resolution = new ProcurementTenantContextResolver().Resolve(foundation);
        return Assert.IsType<ProcurementRequestContext>(resolution.Context);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection;
        private readonly DbContextOptions options;

        private Fixture(SqliteConnection connection, DbContextOptions options)
        {
            this.connection = connection;
            this.options = options;
            Persistence = new SalesPersistence(options);
        }

        public SalesPersistence Persistence { get; }

        public static async Task<Fixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder().UseSqlite(connection).Options;
            await using (var db = new SalesDbContext(options, TenantContext(TenantA, Creator, CompanyA))) await db.Database.EnsureCreatedAsync();
            return new Fixture(connection, options);
        }

        public ProcurementRequestContext Context(Guid actor, Guid tenantId = default, Guid companyId = default) => SalesTests.Context(actor, tenantId == Guid.Empty ? TenantA : tenantId, companyId == Guid.Empty ? CompanyA : companyId);

        public SalesApprovalPolicyDefinition Policy() => new("sales.test.policy", 7, [new SalesApprovalStageDefinition("commercial", 1, 1, [Approver], false)], true, false, DateTimeOffset.MinValue, null);

        public SalesQuotationWriteModel Model(Guid? id = null) => new(
            id ?? Guid.NewGuid(), CompanyA, BranchA, CustomerA, "CUST-001", "Customer A", new DateOnly(2026, 8, 28), new DateOnly(2026, 9, 30), CurrencyA, "SAR", "contact-1", "Commercial note", null,
            [new SalesLineWriteModel(Guid.NewGuid(), ProductA, "SKU-001", "Product A", UomA, "EA", 3m, 50m, 50m, 0m, 0m, 0m, 150m, PriceListA, 4, new DateOnly(2026, 8, 1), "PriceList", "price-source-4", false, null, null, null, "line note")],
            150m, 0m, 0m, 150m);

        private static TenantContext TenantContext(Guid tenantId, Guid actor, Guid companyId) => MiniErp.App.BuildingBlocks.Tenancy.TenantContext.ForOrdinaryMembership(new TenantId(tenantId), new MembershipReference(Guid.NewGuid()), new ScopeReference($"Company:{companyId:D}"), new CorrelationId($"sales-fixture-{Guid.NewGuid():N}"), actor);

        public async ValueTask DisposeAsync() => await connection.DisposeAsync();
    }

    private sealed class CapturingSalesPersistence : ISalesPersistence
    {
        public SalesQuotationWriteModel? Captured { get; private set; }

        public Task<IReadOnlyList<SalesQuotationSummaryResponse>> ListQuotationsAsync(ProcurementRequestContext context, Guid? companyId, SalesQuotationStatus? status, CancellationToken cancellationToken = default) => EmptyList<SalesQuotationSummaryResponse>();
        public Task<SalesQuotationResponse?> GetQuotationAsync(ProcurementRequestContext context, Guid id, CancellationToken cancellationToken = default) => Empty<SalesQuotationResponse?>();
        public Task<SalesOperationResult<SalesQuotationResponse>> CreateQuotationAsync(ProcurementRequestContext context, SalesQuotationWriteModel model, string idempotencyKey, string requestFingerprint, SalesApprovalPolicyDefinition? policy, CancellationToken cancellationToken = default)
        {
            Captured = model;
            return Task.FromResult(SalesOperationResult<SalesQuotationResponse>.Success(null!));
        }
        public Task<SalesOperationResult<SalesQuotationResponse>> EditQuotationAsync(ProcurementRequestContext context, Guid id, SalesQuotationWriteModel model, byte[] expectedVersion, string idempotencyKey, string requestFingerprint, CancellationToken cancellationToken = default) => Failure<SalesQuotationResponse>();
        public Task<SalesOperationResult<SalesQuotationResponse>> TransitionQuotationAsync(ProcurementRequestContext context, Guid id, SalesQuotationStatus target, string? reason, byte[] expectedVersion, string idempotencyKey, string requestFingerprint, SalesApprovalPolicyDefinition? policy, Guid? delegatedFromActorId = null, CancellationToken cancellationToken = default) => Failure<SalesQuotationResponse>();
        public Task<IReadOnlyList<SalesQuotationRevisionResponse>> ListQuotationRevisionsAsync(ProcurementRequestContext context, Guid id, CancellationToken cancellationToken = default) => EmptyList<SalesQuotationRevisionResponse>();
        public Task<IReadOnlyList<SalesHistoryResponse>> ListHistoryAsync(ProcurementRequestContext context, string documentType, Guid id, CancellationToken cancellationToken = default) => EmptyList<SalesHistoryResponse>();
        public Task<IReadOnlyList<SalesAuditResponse>> ListAuditAsync(ProcurementRequestContext context, string documentType, Guid id, CancellationToken cancellationToken = default) => EmptyList<SalesAuditResponse>();
        public Task<IReadOnlyList<SalesOrderSummaryResponse>> ListOrdersAsync(ProcurementRequestContext context, Guid? companyId, SalesOrderStatus? status, CancellationToken cancellationToken = default) => EmptyList<SalesOrderSummaryResponse>();
        public Task<SalesOrderResponse?> GetOrderAsync(ProcurementRequestContext context, Guid id, CancellationToken cancellationToken = default) => Empty<SalesOrderResponse?>();
        public Task<SalesOperationResult<SalesOrderResponse>> ConvertQuotationAsync(ProcurementRequestContext context, Guid quotationId, byte[] expectedVersion, string idempotencyKey, string requestFingerprint, SalesApprovalPolicyDefinition? policy, CancellationToken cancellationToken = default) => Failure<SalesOrderResponse>();
        public Task<SalesOperationResult<SalesOrderResponse>> TransitionOrderAsync(ProcurementRequestContext context, Guid id, SalesOrderStatus target, string? reason, byte[] expectedVersion, string idempotencyKey, string requestFingerprint, SalesCreditEvaluation? credit, SalesApprovalPolicyDefinition? policy, Guid? delegatedFromActorId = null, CancellationToken cancellationToken = default) => Failure<SalesOrderResponse>();
        public Task<SalesOperationResult<SalesOrderResponse>> OverrideOrderCreditAsync(ProcurementRequestContext context, Guid id, string reason, DateTimeOffset expiresAt, string? scope, string? sourceReference, byte[] expectedVersion, string idempotencyKey, string requestFingerprint, SalesCreditEvaluation credit, CancellationToken cancellationToken = default) => Failure<SalesOrderResponse>();
        public Task<SalesCreditResponse?> GetOrderCreditAsync(ProcurementRequestContext context, Guid id, CancellationToken cancellationToken = default) => Empty<SalesCreditResponse?>();

        private static Task<T> Empty<T>() => Task.FromResult<T>(default!);
        private static Task<IReadOnlyList<T>> EmptyList<T>() => Task.FromResult<IReadOnlyList<T>>([]);
        private static Task<SalesOperationResult<T>> Failure<T>() => Task.FromResult(SalesOperationResult<T>.Failure("not-called"));
    }

    private sealed class CustomerReferenceFake : ICustomerPersistence
    {
        private static readonly CustomerRecord Record = new(CustomerA, new TenantId(TenantA), "CUST-001", new LocalizedName("Customer A"), null, MasterDataLifecycleState.Active, [1], []);

        public Task<IReadOnlyList<CustomerRecord>> ListCustomersAsync(TenantContext tenantContext, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<CustomerRecord>>([]);
        public Task<CustomerRecord?> FindCustomerAsync(TenantContext tenantContext, Guid customerId, CancellationToken cancellationToken = default) => Task.FromResult<CustomerRecord?>(customerId == CustomerA ? Record : null);
        public Task<MasterDataPersistenceResult<CustomerRecord>> CreateCustomerAsync(TenantContext tenantContext, Guid customerId, CreateCustomerCommand command, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => Failure<CustomerRecord>();
        public Task<MasterDataPersistenceResult<CustomerRecord>> EditCustomerAsync(TenantContext tenantContext, EditCustomerCommand command, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => Failure<CustomerRecord>();
        public Task<MasterDataPersistenceResult<CustomerRecord>> SetCustomerLifecycleAsync(TenantContext tenantContext, Guid customerId, MasterDataLifecycleState lifecycleState, byte[] expectedVersion, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => Failure<CustomerRecord>();
        public Task<MasterDataPersistenceResult<MasterDataAuditRecord>> AppendAuditAsync(TenantContext tenantContext, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => Failure<MasterDataAuditRecord>();
        public Task<IReadOnlyList<MasterDataAuditRecord>> ReadAuditHistoryAsync(TenantContext tenantContext, Guid customerId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<MasterDataAuditRecord>>([]);

        private static Task<MasterDataPersistenceResult<T>> Failure<T>() => Task.FromResult(MasterDataPersistenceResult<T>.Denied(MasterDataPersistenceOutcome.Failure, "not-called"));
    }

    private sealed class ProductReferenceFake : IProductIdentityPersistence
    {
        private static readonly ProductIdentityRecord Record = new(ProductA, new TenantId(TenantA), "SKU-001", new LocalizedName("Product A"), null, Guid.NewGuid(), UomA, false, null, false, true, false, false, MasterDataLifecycleState.Active, [1], []);

        public Task<IReadOnlyList<ProductIdentityRecord>> ListProductsAsync(TenantContext tenantContext, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<ProductIdentityRecord>>([]);
        public Task<ProductIdentityRecord?> FindProductAsync(TenantContext tenantContext, Guid productId, CancellationToken cancellationToken = default) => Task.FromResult<ProductIdentityRecord?>(productId == ProductA ? Record : null);
        public Task<ProductReferenceValidation> ValidateReferencesAsync(TenantContext tenantContext, Guid categoryId, Guid baseUnitOfMeasureId, CancellationToken cancellationToken = default) => Task.FromResult(ProductReferenceValidation.Invalid());
        public Task<MasterDataPersistenceResult<ProductIdentityRecord>> CreateProductAsync(TenantContext tenantContext, Guid productId, CreateProductIdentityCommand command, ProductReferenceValidation references, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => Failure<ProductIdentityRecord>();
        public Task<MasterDataPersistenceResult<ProductIdentityRecord>> EditProductAsync(TenantContext tenantContext, EditProductIdentityCommand command, ProductReferenceValidation references, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => Failure<ProductIdentityRecord>();
        public Task<MasterDataPersistenceResult<ProductIdentityRecord>> SetProductLifecycleAsync(TenantContext tenantContext, Guid productId, MasterDataLifecycleState lifecycleState, byte[] expectedVersion, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => Failure<ProductIdentityRecord>();
        public Task<MasterDataPersistenceResult<MasterDataAuditRecord>> AppendAuditAsync(TenantContext tenantContext, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => Failure<MasterDataAuditRecord>();
        public Task<IReadOnlyList<MasterDataAuditRecord>> ReadAuditHistoryAsync(TenantContext tenantContext, Guid productId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<MasterDataAuditRecord>>([]);

        private static Task<MasterDataPersistenceResult<T>> Failure<T>() => Task.FromResult(MasterDataPersistenceResult<T>.Denied(MasterDataPersistenceOutcome.Failure, "not-called"));
    }

    private sealed class TaxReferenceFake : ISalesTaxReferenceProvider
    {
        public Task<SalesTaxResolution> ResolveAsync(ProcurementRequestContext context, Guid taxId, DateOnly effectiveOn, decimal taxableBase, string currencyCode, string sourceLineage, CancellationToken cancellationToken = default) =>
            Task.FromResult(taxId == TaxA
                ? SalesTaxResolution.Success(new SalesTaxEvidence(TaxA, "VAT-15", Guid.Parse("cccccccc-1111-1111-1111-111111111111"), 2, effectiveOn, effectiveOn.AddDays(-10), null, 15m, taxableBase, decimal.Round(taxableBase * .15m, 2), currencyCode, "VAT-15;v2"))
                : SalesTaxResolution.Failure("tax_not_found"));
    }

    private sealed class ExchangeRateReferenceFake : ISalesExchangeRateReferenceProvider
    {
        public Task<SalesExchangeRateResolution> ResolveAsync(TenantContext tenantContext, Guid exchangeRateId, string sourceCurrencyCode, string targetCurrencyCode, DateOnly effectiveOn, CancellationToken cancellationToken = default) =>
            Task.FromResult(exchangeRateId == ExchangeRateA
                ? SalesExchangeRateResolution.Success(new SalesExchangeRateEvidence(ExchangeRateA, Guid.Parse("dddddddd-1111-1111-1111-111111111111"), 3, sourceCurrencyCode, targetCurrencyCode, 3.75m, 1, "Configured", "USD/SAR", effectiveOn, effectiveOn.AddDays(-30), null, $"{sourceCurrencyCode}->{targetCurrencyCode};v3"))
                : SalesExchangeRateResolution.Failure("exchange_rate_not_found"));
    }

    private sealed class PriceReferenceFake : IMasterDataPriceListPersistence
    {
        private readonly string currencyCode;

        public PriceReferenceFake(string currencyCode = "SAR") => this.currencyCode = currencyCode;

        public Task<IReadOnlyList<MasterDataPriceListRecord>> ListPriceListsAsync(TenantContext tenantContext, string? search, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<MasterDataPriceListRecord>>([]);
        public Task<MasterDataPriceListRecord?> FindPriceListAsync(TenantContext tenantContext, Guid priceListId, CancellationToken cancellationToken = default) => Empty<MasterDataPriceListRecord?>();
        public Task<MasterDataPersistenceResult<MasterDataPriceListRecord>> CreatePriceListAsync(TenantContext tenantContext, Guid priceListId, CreateMasterDataPriceListCommand command, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => Failure<MasterDataPriceListRecord>();
        public Task<MasterDataPersistenceResult<MasterDataPriceListRecord>> EditPriceListAsync(TenantContext tenantContext, EditMasterDataPriceListCommand command, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => Failure<MasterDataPriceListRecord>();
        public Task<MasterDataPersistenceResult<MasterDataPriceListRecord>> AppendPriceAsync(TenantContext tenantContext, AppendMasterDataPriceCommand command, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => Failure<MasterDataPriceListRecord>();
        public Task<MasterDataPersistenceResult<MasterDataPriceListRecord>> SetPriceListLifecycleAsync(TenantContext tenantContext, Guid priceListId, MasterDataLifecycleState lifecycleState, byte[] expectedVersion, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => Failure<MasterDataPriceListRecord>();
        public Task<MasterDataPersistenceResult<MasterDataPriceListReferenceRecord>> ResolvePriceAsync(TenantContext tenantContext, ResolveMasterDataPriceQuery query, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => Task.FromResult(MasterDataPersistenceResult<MasterDataPriceListReferenceRecord>.Success(CreateRecord()));
        public Task<MasterDataPersistenceResult<MasterDataAuditRecord>> AppendAuditAsync(TenantContext tenantContext, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => Failure<MasterDataAuditRecord>();
        public Task<IReadOnlyList<MasterDataAuditRecord>> ReadAuditHistoryAsync(TenantContext tenantContext, Guid? priceListId = null, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<MasterDataAuditRecord>>([]);

        private MasterDataPriceListReferenceRecord CreateRecord()
        {
            var effective = new DateOnly(2026, 8, 1);
            var price = new MasterDataPriceListPriceRecord(Guid.NewGuid(), 4, ProductA, "SKU-001", UomA, "EA", CurrencyA, currencyCode, CustomerA, OrganizationScopeKind.Branch, BranchA, 1, effective, null, 50m, 2, PriceListProvenance.Configured, "price-row-4", [1]);
            var configuration = new MasterDataPriceListCurrentConfiguration(CurrencyA, currencyCode, CustomerA, OrganizationScopeKind.Branch, BranchA, 1, MasterDataLifecycleState.Active);
            var snapshot = new ReferenceSnapshot(MasterDataResourceKind.PriceList, PriceListA, new TenantOwnership(TenantA), 4, "price-row-4", effective);
            return new MasterDataPriceListReferenceRecord(PriceListA, new TenantId(TenantA), "STANDARD", price, configuration, new DateOnly(2026, 8, 28), snapshot, [1]);
        }

        private static Task<T> Empty<T>() => Task.FromResult<T>(default!);
        private static Task<MasterDataPersistenceResult<T>> Failure<T>() => Task.FromResult(MasterDataPersistenceResult<T>.Denied(MasterDataPersistenceOutcome.Failure, "not-called"));
    }
}
