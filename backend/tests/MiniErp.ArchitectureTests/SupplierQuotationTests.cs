using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MiniErp.App.BuildingBlocks.Rest;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.Modules.BusinessParties;
using MiniErp.App.Modules.MasterData;
using MiniErp.App.Modules.Procurement;
using MiniErp.Contracts.Modules.Audit;
using MiniErp.Contracts.Modules.Foundation;
using MiniErp.Contracts.Modules.MasterData;
using MiniErp.Contracts.Modules.Procurement;
using MiniErp.Infrastructure.Persistence.Modules.Procurement;
using Xunit;

namespace MiniErp.ArchitectureTests;

public sealed class SupplierQuotationTests
{
    private static readonly Guid TenantA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid TenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid CompanyA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid BranchA = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid Requester = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid SupplierA = Guid.Parse("aaaaaaaa-1111-1111-1111-111111111111");
    private static readonly Guid SupplierB = Guid.Parse("bbbbbbbb-2222-2222-2222-222222222222");
    private static readonly Guid Usd = Guid.Parse("aaaaaaaa-3333-3333-3333-333333333333");
    private static readonly Guid Eur = Guid.Parse("bbbbbbbb-4444-4444-4444-444444444444");
    private static readonly Guid ProductId = Guid.Parse("77777777-7777-7777-7777-777777777777");
    private static readonly Guid UnitId = Guid.Parse("88888888-8888-8888-8888-888888888888");

    [Fact]
    public async Task Approved_request_captures_idempotent_quotations_and_preserves_snapshots()
    {
        await using var fixture = await Fixture.CreateAsync();
        var request = await fixture.CreateApprovedRequestAsync();
        var body = fixture.Quotation(SupplierA, Usd, "Q-USD-1");

        var created = await fixture.Service.CreateAsync(
            fixture.Context(Requester, "tenant.procurement.quotation.create"),
            request.Id,
            body,
            "quotation-create-1");
        Assert.True(created.Succeeded, created.Code);
        Assert.Equal(SupplierA, created.Value!.Supplier.Id);
        Assert.Equal(ProductId, created.Value.Lines.Single().ProductId);
        Assert.Equal(request.Lines.Single().Id, created.Value.Lines.Single().PurchaseRequestLineId);
        Assert.NotEmpty(created.Value.Version);

        var replay = await fixture.Service.CreateAsync(
            fixture.Context(Requester, "tenant.procurement.quotation.create"),
            request.Id,
            body,
            "quotation-create-1");
        Assert.True(replay.Succeeded, replay.Code);
        Assert.Equal(created.Value.Id, replay.Value!.Id);

        var history = await fixture.Service.ReadHistoryAsync(
            fixture.Context(Requester, "tenant.procurement.quotation.history"),
            created.Value.Id);
        Assert.True(history.Succeeded, history.Code);
        Assert.Equal(SupplierQuotationHistoryAction.Created, history.Value!.Single().Action);

        var audit = await fixture.Service.ReadAuditAsync(
            fixture.Context(Requester, "tenant.procurement.quotation.audit"),
            created.Value.Id);
        Assert.True(audit.Succeeded, audit.Code);
        Assert.Single(audit.Value!);
        Assert.Equal("quotation-create-1", audit.Value!.Single().IdempotencyKey);
    }

    [Fact]
    public async Task Draft_edit_and_submit_require_current_version_and_active_references()
    {
        await using var fixture = await Fixture.CreateAsync();
        var request = await fixture.CreateApprovedRequestAsync();
        var created = await fixture.Service.CreateAsync(
            fixture.Context(Requester, "tenant.procurement.quotation.create"),
            request.Id,
            fixture.Quotation(SupplierA, Usd, "Q-USD-2"),
            "quotation-create-2");
        Assert.True(created.Succeeded, created.Code);

        var edited = await fixture.Service.EditAsync(
            fixture.Context(Requester, "tenant.procurement.quotation.edit"),
            created.Value!.Id,
            fixture.Quotation(SupplierA, Usd, "Q-USD-2-EDITED"),
            created.Value.Version,
            "quotation-edit-1");
        Assert.True(edited.Succeeded, edited.Code);
        Assert.Equal("Q-USD-2-EDITED", edited.Value!.SupplierQuotationReference);

        var stale = await fixture.Service.EditAsync(
            fixture.Context(Requester, "tenant.procurement.quotation.edit"),
            created.Value.Id,
            fixture.Quotation(SupplierA, Usd, "Q-USD-STALE"),
            created.Value.Version,
            "quotation-edit-stale");
        Assert.False(stale.Succeeded);
        Assert.Equal("concurrency_conflict", stale.Code);

        var submitted = await fixture.Service.SubmitAsync(
            fixture.Context(Requester, "tenant.procurement.quotation.submit"),
            edited.Value.Id,
            edited.Value.Version,
            "quotation-submit-1");
        Assert.True(submitted.Succeeded, submitted.Code);
        Assert.Equal(SupplierQuotationStatus.Submitted, submitted.Value!.Status);

        fixture.Suppliers.SetLifecycle(SupplierA, MasterDataLifecycleState.Inactive);
        var inactive = await fixture.Service.CreateAsync(
            fixture.Context(Requester, "tenant.procurement.quotation.create"),
            request.Id,
            fixture.Quotation(SupplierA, Usd, "Q-INACTIVE"),
            "quotation-inactive");
        Assert.False(inactive.Succeeded);
        Assert.Equal("supplier_inactive", inactive.Code);
    }

    [Fact]
    public async Task Comparison_groups_mixed_currencies_without_fx_or_hidden_winner()
    {
        await using var fixture = await Fixture.CreateAsync();
        var request = await fixture.CreateApprovedRequestAsync();
        var first = await fixture.CreateSubmittedAsync(request.Id, SupplierA, Usd, "Q-USD-3", "quotation-usd-3");
        var second = await fixture.CreateSubmittedAsync(request.Id, SupplierB, Eur, "Q-EUR-3", "quotation-eur-3");

        var comparison = await fixture.Service.CompareAsync(
            fixture.Context(Requester, "tenant.procurement.quotation.compare"),
            request.Id);
        Assert.True(comparison.Succeeded, comparison.Code);
        Assert.True(comparison.Value!.HasMixedCurrencies);
        Assert.False(comparison.Value.DirectCurrencyComparisonAvailable);
        Assert.Equal(2, comparison.Value.CurrencyGroups.Count);
        Assert.Equal(
            new[] { first.Id, second.Id }.OrderBy(item => item),
            comparison.Value.Quotations.Select(item => item.SupplierQuotationId).OrderBy(item => item));
        Assert.All(comparison.Value.Quotations, item =>
            Assert.Contains("mixed_currency_no_fx_basis", item.QualificationIssues));
    }

    [Fact]
    public async Task Source_decision_is_single_current_selection_with_rationale_history_and_selection_flag()
    {
        await using var fixture = await Fixture.CreateAsync();
        var request = await fixture.CreateApprovedRequestAsync();
        var first = await fixture.CreateSubmittedAsync(request.Id, SupplierA, Usd, "Q-USD-4", "quotation-usd-4");
        var second = await fixture.CreateSubmittedAsync(request.Id, SupplierB, Usd, "Q-USD-5", "quotation-usd-5");

        var decision = await fixture.Service.RecordSourceDecisionAsync(
            fixture.Context(Requester, "tenant.procurement.source-decision.record"),
            request.Id,
            new SupplierSourceDecisionWriteRequest(first.Id, "Selected for the documented commercial and delivery rationale."),
            request.Version,
            "source-decision-1");
        Assert.True(decision.Succeeded, decision.Code);
        Assert.Equal(first.Id, decision.Value!.SelectedQuotationId);
        Assert.StartsWith("sha256:", decision.Value.ComparisonSnapshotReference, StringComparison.Ordinal);
        Assert.False(string.IsNullOrWhiteSpace(decision.Value.ComparisonSnapshotJson));

        var listed = await fixture.Service.ListAsync(
            fixture.Context(Requester, "tenant.procurement.quotation.view"),
            request.Id);
        Assert.True(listed.Succeeded, listed.Code);
        Assert.True(listed.Value!.Single(item => item.Id == first.Id).IsSelected);
        Assert.False(listed.Value!.Single(item => item.Id == second.Id).IsSelected);

        var changed = await fixture.Service.RecordSourceDecisionAsync(
            fixture.Context(Requester, "tenant.procurement.source-decision.record"),
            request.Id,
            new SupplierSourceDecisionWriteRequest(second.Id, "Changed after the second documented comparison review."),
            decision.Value.Version,
            "source-decision-2");
        Assert.True(changed.Succeeded, changed.Code);
        Assert.Equal(second.Id, changed.Value!.SelectedQuotationId);

        var firstHistory = await fixture.Service.ReadHistoryAsync(
            fixture.Context(Requester, "tenant.procurement.quotation.history"),
            first.Id);
        Assert.Contains(firstHistory.Value!, item => item.Action == SupplierQuotationHistoryAction.Superseded);
        var firstAudit = await fixture.Service.ReadAuditAsync(
            fixture.Context(Requester, "tenant.procurement.quotation.audit"),
            first.Id);
        Assert.Contains(firstAudit.Value!, item => item.AfterStatus == SupplierQuotationStatus.Superseded);
        var secondCurrent = await fixture.Service.GetAsync(
            fixture.Context(Requester, "tenant.procurement.quotation.view"),
            second.Id);
        Assert.True(secondCurrent.Succeeded, secondCurrent.Code);
        Assert.True(secondCurrent.Value!.IsSelected);
    }

    [Fact]
    public async Task Source_decision_concurrency_enforces_caller_version_on_first_decision_and_reselection()
    {
        await using var fixture = await Fixture.CreateAsync();
        var request = await fixture.CreateApprovedRequestAsync();
        var first = await fixture.CreateSubmittedAsync(request.Id, SupplierA, Usd, "Q-USD-C1", "quotation-usd-c1");
        var second = await fixture.CreateSubmittedAsync(request.Id, SupplierB, Usd, "Q-USD-C2", "quotation-usd-c2");

        // 1. First decision with stale/wrong PR version fails with concurrency_conflict
        var wrongFirstDecision = await fixture.Service.RecordSourceDecisionAsync(
            fixture.Context(Requester, "tenant.procurement.source-decision.record"),
            request.Id,
            new SupplierSourceDecisionWriteRequest(first.Id, "Stale token first decision."),
            new byte[] { 9, 9, 9, 9 },
            "source-decision-wrong-1");
        Assert.False(wrongFirstDecision.Succeeded);
        Assert.Equal("concurrency_conflict", wrongFirstDecision.Code);

        // 2. First decision with valid PR version succeeds
        var firstDecision = await fixture.Service.RecordSourceDecisionAsync(
            fixture.Context(Requester, "tenant.procurement.source-decision.record"),
            request.Id,
            new SupplierSourceDecisionWriteRequest(first.Id, "First selection rationale."),
            request.Version,
            "source-decision-valid-1");
        Assert.True(firstDecision.Succeeded, firstDecision.Code);
        Assert.Equal(first.Id, firstDecision.Value!.SelectedQuotationId);

        // 3. Reselection with stale PR version fails with concurrency_conflict
        var stalePrReselection = await fixture.Service.RecordSourceDecisionAsync(
            fixture.Context(Requester, "tenant.procurement.source-decision.record"),
            request.Id,
            new SupplierSourceDecisionWriteRequest(second.Id, "Reselection with PR token."),
            request.Version,
            "source-decision-stale-pr");
        Assert.False(stalePrReselection.Succeeded);
        Assert.Equal("concurrency_conflict", stalePrReselection.Code);

        // 4. Reselection with garbage/wrong version fails with concurrency_conflict
        var garbageReselection = await fixture.Service.RecordSourceDecisionAsync(
            fixture.Context(Requester, "tenant.procurement.source-decision.record"),
            request.Id,
            new SupplierSourceDecisionWriteRequest(second.Id, "Reselection with garbage token."),
            new byte[] { 0, 0, 0, 0, 0, 0, 0, 0 },
            "source-decision-garbage");
        Assert.False(garbageReselection.Succeeded);
        Assert.Equal("concurrency_conflict", garbageReselection.Code);

        // 5. Failed reselections did not alter current decision or mutate history
        var currentDecisionAfterFailures = await fixture.Service.ReadSourceDecisionAsync(
            fixture.Context(Requester, "tenant.procurement.source-decision.view"),
            request.Id);
        Assert.True(currentDecisionAfterFailures.Succeeded, currentDecisionAfterFailures.Code);
        Assert.Equal(first.Id, currentDecisionAfterFailures.Value!.SelectedQuotationId);

        var historyAfterFailures = await fixture.Service.ReadSourceDecisionHistoryAsync(
            fixture.Context(Requester, "tenant.procurement.source-decision.view"),
            request.Id);
        Assert.True(historyAfterFailures.Succeeded, historyAfterFailures.Code);
        Assert.Single(historyAfterFailures.Value!);

        // 6. Reselection with valid current source-decision version succeeds
        var validReselection = await fixture.Service.RecordSourceDecisionAsync(
            fixture.Context(Requester, "tenant.procurement.source-decision.record"),
            request.Id,
            new SupplierSourceDecisionWriteRequest(second.Id, "Reselection with current decision version."),
            firstDecision.Value.Version,
            "source-decision-valid-reselection");
        Assert.True(validReselection.Succeeded, validReselection.Code);
        Assert.Equal(second.Id, validReselection.Value!.SelectedQuotationId);

        // 7. Source decision history now records both selections
        var historyAfterSuccess = await fixture.Service.ReadSourceDecisionHistoryAsync(
            fixture.Context(Requester, "tenant.procurement.source-decision.view"),
            request.Id);
        Assert.True(historyAfterSuccess.Succeeded, historyAfterSuccess.Code);
        Assert.Equal(2, historyAfterSuccess.Value!.Count);
        Assert.Equal(first.Id, historyAfterSuccess.Value![0].SelectedQuotationId);
        Assert.Equal(second.Id, historyAfterSuccess.Value![1].SelectedQuotationId);
        Assert.Equal(first.Id, historyAfterSuccess.Value![1].PreviousSelectedQuotationId);
    }

    [Fact]
    public async Task Cross_tenant_reads_fail_closed_and_unapproved_requests_cannot_capture_quotes()
    {
        await using var fixture = await Fixture.CreateAsync();
        var request = await fixture.CreateApprovedRequestAsync();
        var quote = await fixture.Service.CreateAsync(
            fixture.Context(Requester, "tenant.procurement.quotation.create"),
            request.Id,
            fixture.Quotation(SupplierA, Usd, "Q-TENANT"),
            "quotation-tenant");
        Assert.True(quote.Succeeded, quote.Code);

        var foreign = await fixture.Service.GetAsync(
            fixture.Context(Requester, "tenant.procurement.quotation.read", TenantB),
            quote.Value!.Id);
        Assert.False(foreign.Succeeded);
        Assert.Equal("quotation_not_found", foreign.Code);

        var draft = await fixture.CreateDraftRequestAsync();
        var denied = await fixture.Service.CreateAsync(
            fixture.Context(Requester, "tenant.procurement.quotation.create"),
            draft.Id,
            fixture.Quotation(SupplierA, Usd, "Q-DRAFT"),
            "quotation-draft");
        Assert.False(denied.Succeeded);
        Assert.Equal("purchase_request_not_approved", denied.Code);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection;
        private readonly DbContextOptions options;

        private Fixture(
            SqliteConnection connection,
            DbContextOptions options,
            SupplierQuotationService service,
            TestSupplierPersistence suppliers,
            TestCurrencyPaymentTermPersistence currencies)
        {
            this.connection = connection;
            this.options = options;
            Service = service;
            Suppliers = suppliers;
            Currencies = currencies;
        }

        public SupplierQuotationService Service { get; }

        public TestSupplierPersistence Suppliers { get; }

        public TestCurrencyPaymentTermPersistence Currencies { get; }

        public static async Task<Fixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder()
                .UseSqlite(connection)
                .Options;
            var tenantContext = TenantContext(TenantA, Requester);
            await using (var db = new ProcurementDbContext(options, tenantContext))
            {
                await db.Database.EnsureCreatedAsync();
            }

            var suppliers = new TestSupplierPersistence(
            [
                Supplier(SupplierA, "SUP-A", "Alpha Supplier", TenantA),
                Supplier(SupplierB, "SUP-B", "Beta Supplier", TenantA)
            ]);
            var currencies = new TestCurrencyPaymentTermPersistence(
            [
                Currency(Usd, "USD", "US Dollar", TenantA),
                Currency(Eur, "EUR", "Euro", TenantA)
            ]);
            var service = new SupplierQuotationService(
                new PurchaseRequestAuthorizationService(),
                new PurchaseRequestPersistence(options),
                new SupplierQuotationPersistence(options),
                suppliers,
                currencies,
                new TestTaxPersistence());
            return new Fixture(connection, options, service, suppliers, currencies);
        }

        public async Task<PurchaseRequestRecord> CreateApprovedRequestAsync()
        {
            var request = await CreateRequestEntityAsync(PurchaseRequestStatus.Approved);
            await using var db = new ProcurementDbContext(options, TenantContext(TenantA, Requester));
            var found = await db.PurchaseRequests
                .Include(item => item.Lines)
                .SingleAsync(item => item.Id == request);
            return new PurchaseRequestPersistence(options) is { } persistence
                ? (await persistence.FindAsync(TenantContext(TenantA, Requester), request))!
                : throw new InvalidOperationException();
        }

        public async Task<PurchaseRequestRecord> CreateDraftRequestAsync()
        {
            var request = await CreateRequestEntityAsync(PurchaseRequestStatus.Draft);
            return (await new PurchaseRequestPersistence(options).FindAsync(
                TenantContext(TenantA, Requester),
                request))!;
        }

        public async Task<SupplierQuotationRecord> CreateSubmittedAsync(
            Guid requestId,
            Guid supplierId,
            Guid currencyId,
            string quotationReference,
            string idempotencyKey)
        {
            var created = await Service.CreateAsync(
                Context(Requester, "tenant.procurement.quotation.create"),
                requestId,
                Quotation(supplierId, currencyId, quotationReference),
                idempotencyKey + "-create");
            Assert.True(created.Succeeded, created.Code);
            var submitted = await Service.SubmitAsync(
                Context(Requester, "tenant.procurement.quotation.submit"),
                created.Value!.Id,
                created.Value.Version,
                idempotencyKey + "-submit");
            Assert.True(submitted.Succeeded, submitted.Code);
            return submitted.Value!;
        }

        public SupplierQuotationWriteRequest Quotation(Guid supplierId, Guid currencyId, string reference) => new(
            supplierId,
            reference,
            DateOnly.FromDateTime(DateTime.UtcNow.Date),
            DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(14)),
            currencyId,
            null,
            "Delivered to the requested branch",
            DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(7)),
            "7 days",
            "Buyer-recorded quote",
            [new SupplierQuotationLineWriteRequest(
                Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee"),
                2,
                12.5m,
                DiscountPercentage: 5m,
                TaxReference: "quoted-tax-fact")],
            [new SupplierQuotationEvidenceReferenceWriteRequest(
                "offer-ref-" + reference,
                "offer.pdf",
                "application/pdf",
                "Supplier offer reference",
                "buyer-recorded",
                null)]);

        public ProcurementRequestContext Context(
            Guid actor,
            string permission,
            Guid tenantId = default)
        {
            tenantId = tenantId == Guid.Empty ? TenantA : tenantId;
            var tenantContext = TenantContext(tenantId, actor);
            var foundation = FoundationRequestContext.ForTenant(
                actor,
                Guid.NewGuid(),
                tenantContext,
                permission);
            var resolved = new ProcurementTenantContextResolver().Resolve(foundation);
            return Assert.IsType<ProcurementRequestContext>(resolved.Context);
        }

        private async Task<Guid> CreateRequestEntityAsync(PurchaseRequestStatus status)
        {
            var requestId = Guid.NewGuid();
            var lineId = status == PurchaseRequestStatus.Draft
                ? Guid.NewGuid()
                : Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");
            var now = DateTimeOffset.UtcNow;
            var policy = new PurchaseRequestApprovalPolicyDefinition(
                "procurement.purchase-request.test",
                1,
                [new PurchaseRequestApprovalStageDefinition("manager", 1, 1, [], false)],
                true,
                now.AddMinutes(-1));
            var entity = new PurchaseRequestEntity(
                requestId,
                new TenantId(TenantA),
                CompanyA,
                BranchA,
                Requester,
                "Approved office demand",
                now);
            entity.Lines.Add(new PurchaseRequestLineEntity(
                lineId,
                new TenantId(TenantA),
                requestId,
                new PurchaseRequestLineSnapshot(
                    lineId,
                    ProductId,
                    "SKU-001",
                    "Test Product",
                    UnitId,
                    "EA",
                    2,
                    DateOnly.FromDateTime(now.UtcDateTime.Date.AddDays(7)),
                    "Replace damaged stockroom items")));
            if (status == PurchaseRequestStatus.Approved)
            {
                entity.Submit(policy, JsonSerializer.Serialize(policy), now);
                entity.RecordApproval(PurchaseRequestStatus.Approved, 0, 0, "[]", now);
            }

            entity.TouchVersion();
            await using var db = new ProcurementDbContext(options, TenantContext(TenantA, Requester));
            db.PurchaseRequests.Add(entity);
            await db.SaveChangesAsync();
            return requestId;
        }

        public async ValueTask DisposeAsync() => await connection.DisposeAsync();

        private static TenantContext TenantContext(Guid tenantId, Guid actor) =>
            MiniErp.App.BuildingBlocks.Tenancy.TenantContext.ForOrdinaryMembership(
                new TenantId(tenantId),
                new MembershipReference(Guid.NewGuid()),
                new ScopeReference($"Company:{CompanyA:D}"),
                new CorrelationId($"corr-{Guid.NewGuid():N}"),
                actor);

        private static SupplierRecord Supplier(Guid id, string code, string name, Guid tenantId) => new(
            id,
            new TenantId(tenantId),
            code,
            new LocalizedName(name),
            null,
            null,
            MasterDataLifecycleState.Active,
            Guid.NewGuid().ToByteArray(),
            []);

        private static MasterDataCurrencyRecord Currency(Guid id, string code, string name, Guid tenantId) => new(
            id,
            new TenantId(tenantId),
            code,
            new LocalizedName(name),
            MasterDataLifecycleState.Active,
            1,
            Guid.NewGuid().ToByteArray());
    }

    private sealed class TestSupplierPersistence : ISupplierPersistence
    {
        private readonly Dictionary<Guid, SupplierRecord> records;

        public TestSupplierPersistence(IEnumerable<SupplierRecord> records) =>
            this.records = records.ToDictionary(item => item.Id);

        public void SetLifecycle(Guid id, MasterDataLifecycleState lifecycleState) =>
            records[id] = records[id] with { LifecycleState = lifecycleState };

        public Task<SupplierRecord?> FindSupplierAsync(TenantContext tenantContext, Guid supplierId, CancellationToken cancellationToken = default) =>
            Task.FromResult(records.TryGetValue(supplierId, out var record) && record.TenantId.Value == tenantContext.TenantId.Value ? record : null);

        public Task<IReadOnlyList<SupplierRecord>> ListSuppliersAsync(TenantContext tenantContext, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<SupplierRecord>>(records.Values.Where(item => item.TenantId.Value == tenantContext.TenantId.Value).ToArray());

        public Task<MasterDataPersistenceResult<SupplierRecord>> CreateSupplierAsync(TenantContext tenantContext, Guid supplierId, CreateSupplierCommand command, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => Unavailable<MasterDataPersistenceResult<SupplierRecord>>();
        public Task<MasterDataPersistenceResult<SupplierRecord>> EditSupplierAsync(TenantContext tenantContext, EditSupplierCommand command, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => Unavailable<MasterDataPersistenceResult<SupplierRecord>>();
        public Task<MasterDataPersistenceResult<SupplierRecord>> SetSupplierLifecycleAsync(TenantContext tenantContext, Guid supplierId, MasterDataLifecycleState lifecycleState, byte[] expectedVersion, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => Unavailable<MasterDataPersistenceResult<SupplierRecord>>();
        public Task<MasterDataPersistenceResult<MasterDataAuditRecord>> AppendAuditAsync(TenantContext tenantContext, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => Unavailable<MasterDataPersistenceResult<MasterDataAuditRecord>>();
        public Task<IReadOnlyList<MasterDataAuditRecord>> ReadAuditHistoryAsync(TenantContext tenantContext, Guid supplierId, CancellationToken cancellationToken = default) => Unavailable<IReadOnlyList<MasterDataAuditRecord>>();

        private static Task<T> Unavailable<T>() => Task.FromException<T>(new InvalidOperationException("not used"));
    }

    private sealed class TestCurrencyPaymentTermPersistence : IMasterDataCurrencyPaymentTermPersistence
    {
        private readonly Dictionary<Guid, MasterDataCurrencyRecord> currencies;

        public TestCurrencyPaymentTermPersistence(IEnumerable<MasterDataCurrencyRecord> currencies) =>
            this.currencies = currencies.ToDictionary(item => item.Id);

        public Task<MasterDataCurrencyRecord?> FindCurrencyAsync(TenantContext tenantContext, Guid currencyId, CancellationToken cancellationToken = default) =>
            Task.FromResult(currencies.TryGetValue(currencyId, out var record) && record.TenantId.Value == tenantContext.TenantId.Value ? record : null);

        public Task<IReadOnlyList<MasterDataCurrencyRecord>> ListCurrenciesAsync(TenantContext tenantContext, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<MasterDataCurrencyRecord>>(currencies.Values.Where(item => item.TenantId.Value == tenantContext.TenantId.Value).ToArray());

        public Task<MasterDataPaymentTermRecord?> FindPaymentTermAsync(TenantContext tenantContext, Guid paymentTermId, CancellationToken cancellationToken = default) => Task.FromResult<MasterDataPaymentTermRecord?>(null);
        public Task<IReadOnlyList<MasterDataPaymentTermRecord>> ListPaymentTermsAsync(TenantContext tenantContext, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<MasterDataPaymentTermRecord>>([]);
        public Task<MasterDataPersistenceResult<MasterDataCurrencyRecord>> CreateCurrencyAsync(TenantContext tenantContext, Guid currencyId, CreateMasterDataCurrencyCommand command, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => Unavailable<MasterDataPersistenceResult<MasterDataCurrencyRecord>>();
        public Task<MasterDataPersistenceResult<MasterDataCurrencyRecord>> EditCurrencyAsync(TenantContext tenantContext, EditMasterDataCurrencyCommand command, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => Unavailable<MasterDataPersistenceResult<MasterDataCurrencyRecord>>();
        public Task<MasterDataPersistenceResult<MasterDataCurrencyRecord>> SetCurrencyLifecycleAsync(TenantContext tenantContext, Guid currencyId, MasterDataLifecycleState lifecycleState, byte[] expectedVersion, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => Unavailable<MasterDataPersistenceResult<MasterDataCurrencyRecord>>();
        public Task<MasterDataPersistenceResult<MasterDataPaymentTermRecord>> CreatePaymentTermAsync(TenantContext tenantContext, Guid paymentTermId, CreateMasterDataPaymentTermCommand command, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => Unavailable<MasterDataPersistenceResult<MasterDataPaymentTermRecord>>();
        public Task<MasterDataPersistenceResult<MasterDataPaymentTermRecord>> EditPaymentTermAsync(TenantContext tenantContext, EditMasterDataPaymentTermCommand command, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => Unavailable<MasterDataPersistenceResult<MasterDataPaymentTermRecord>>();
        public Task<MasterDataPersistenceResult<MasterDataPaymentTermRecord>> SetPaymentTermLifecycleAsync(TenantContext tenantContext, Guid paymentTermId, MasterDataLifecycleState lifecycleState, byte[] expectedVersion, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => Unavailable<MasterDataPersistenceResult<MasterDataPaymentTermRecord>>();
        public Task<MasterDataPersistenceResult<MasterDataAuditRecord>> AppendAuditAsync(TenantContext tenantContext, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => Unavailable<MasterDataPersistenceResult<MasterDataAuditRecord>>();
        public Task<IReadOnlyList<MasterDataAuditRecord>> ReadAuditHistoryAsync(TenantContext tenantContext, MasterDataResourceKind resourceKind, Guid? resourceId = null, CancellationToken cancellationToken = default) => Unavailable<IReadOnlyList<MasterDataAuditRecord>>();

        private static Task<T> Unavailable<T>() => Task.FromException<T>(new InvalidOperationException("not used"));
    }

    private sealed class TestTaxPersistence : IMasterDataTaxPersistence
    {
        public Task<MasterDataTaxRecord?> FindTaxAsync(TenantContext tenantContext, Guid taxId, CancellationToken cancellationToken = default) => Task.FromResult<MasterDataTaxRecord?>(null);
        public Task<IReadOnlyList<MasterDataTaxRecord>> ListTaxesAsync(TenantContext tenantContext, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<MasterDataTaxRecord>>([]);
        public Task<MasterDataPersistenceResult<MasterDataTaxRecord>> CreateTaxAsync(TenantContext tenantContext, Guid taxId, CreateMasterDataTaxCommand command, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => Unavailable<MasterDataPersistenceResult<MasterDataTaxRecord>>();
        public Task<MasterDataPersistenceResult<MasterDataTaxRecord>> EditTaxAsync(TenantContext tenantContext, EditMasterDataTaxCommand command, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => Unavailable<MasterDataPersistenceResult<MasterDataTaxRecord>>();
        public Task<MasterDataPersistenceResult<MasterDataTaxRecord>> SetTaxLifecycleAsync(TenantContext tenantContext, Guid taxId, MasterDataLifecycleState lifecycleState, byte[] expectedVersion, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => Unavailable<MasterDataPersistenceResult<MasterDataTaxRecord>>();
        public Task<MasterDataPersistenceResult<MasterDataAuditRecord>> AppendAuditAsync(TenantContext tenantContext, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => Unavailable<MasterDataPersistenceResult<MasterDataAuditRecord>>();
        public Task<IReadOnlyList<MasterDataAuditRecord>> ReadAuditHistoryAsync(TenantContext tenantContext, Guid? taxId = null, CancellationToken cancellationToken = default) => Unavailable<IReadOnlyList<MasterDataAuditRecord>>();

        private static Task<T> Unavailable<T>() => Task.FromException<T>(new InvalidOperationException("not used"));
    }
}
