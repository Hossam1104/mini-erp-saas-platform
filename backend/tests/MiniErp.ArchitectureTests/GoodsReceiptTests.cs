using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MiniErp.App.BuildingBlocks.Rest;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.Modules.Procurement;
using MiniErp.Contracts.Modules.Foundation;
using MiniErp.Contracts.Modules.Procurement;
using MiniErp.Infrastructure.Persistence.Modules.Procurement;
using Xunit;

namespace MiniErp.ArchitectureTests;

public sealed class GoodsReceiptTests
{
    private static readonly Guid TenantA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid TenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid CompanyA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid BranchA = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid Requester = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid Approver = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly Guid Supplier = Guid.Parse("aaaaaaaa-1111-1111-1111-111111111111");
    private static readonly Guid Currency = Guid.Parse("aaaaaaaa-3333-3333-3333-333333333333");
    private static readonly Guid Product = Guid.Parse("77777777-7777-7777-7777-777777777777");
    private static readonly Guid Unit = Guid.Parse("88888888-8888-8888-8888-888888888888");
    private static readonly Guid WarehouseA = Guid.Parse("cccccccc-1111-1111-1111-111111111111");

    [Fact]
    public async Task Denies_receipt_against_a_purchase_order_that_has_not_reached_a_confirmed_stage()
    {
        await using var fixture = await Fixture.CreateAsync();
        var issued = await fixture.IssuedOrderAsync("gr-wrong-stage");

        var denied = await fixture.GoodsReceiptService.CreateAsync(
            fixture.Context(Requester, "tenant.procurement.goods-receipt.create"),
            new GoodsReceiptCreateRequest(issued.Id, WarehouseA, DateOnly.FromDateTime(DateTime.UtcNow.Date), null, null, [new GoodsReceiptLineCreateRequest(issued.Lines.Single().Id, 2m, 2m, 0m, null, null, null)]),
            "gr-wrong-stage-create",
            "fp-gr-wrong-stage-create");
        Assert.False(denied.Succeeded);
        Assert.Equal("goods_receipt_source_not_eligible", denied.Code);
    }

    [Fact]
    public async Task Lists_eligible_sources_only_while_remaining_receivable_quantity_is_positive()
    {
        await using var fixture = await Fixture.CreateAsync();
        var confirmed = await fixture.ConfirmedOrderAsync("gr-eligibility");

        var before = await fixture.GoodsReceiptService.ListEligibleSourcesAsync(fixture.Context(Requester, "tenant.procurement.goods-receipt.view"));
        Assert.True(before.Succeeded, before.Code);
        var source = Assert.Single(before.Value!);
        Assert.Equal(confirmed.Id, source.PurchaseOrderId);
        var eligibleLine = Assert.Single(source.Lines);
        Assert.Equal(2m, eligibleLine.RemainingReceivableQuantity);

        var full = await fixture.GoodsReceiptService.CreateAsync(
            fixture.Context(Requester, "tenant.procurement.goods-receipt.create"),
            new GoodsReceiptCreateRequest(confirmed.Id, WarehouseA, DateOnly.FromDateTime(DateTime.UtcNow.Date), "GRN-1", null, [new GoodsReceiptLineCreateRequest(confirmed.Lines.Single().Id, 2m, 2m, 0m, null, null, null)]),
            "gr-eligibility-full",
            "fp-gr-eligibility-full");
        Assert.True(full.Succeeded, full.Code);

        var after = await fixture.GoodsReceiptService.ListEligibleSourcesAsync(fixture.Context(Requester, "tenant.procurement.goods-receipt.view"));
        Assert.True(after.Succeeded, after.Code);
        Assert.Empty(after.Value!);
    }

    [Fact]
    public async Task Records_partial_receipts_sequentially_until_the_remainder_is_exhausted()
    {
        await using var fixture = await Fixture.CreateAsync();
        var confirmed = await fixture.ConfirmedOrderAsync("gr-partial");
        var line = confirmed.Lines.Single();

        var firstPartial = await fixture.GoodsReceiptService.CreateAsync(
            fixture.Context(Requester, "tenant.procurement.goods-receipt.create"),
            new GoodsReceiptCreateRequest(confirmed.Id, WarehouseA, DateOnly.FromDateTime(DateTime.UtcNow.Date), "GRN-P1", null, [new GoodsReceiptLineCreateRequest(line.Id, 1m, 1m, 0m, null, null, null)]),
            "gr-partial-1",
            "fp-gr-partial-1");
        Assert.True(firstPartial.Succeeded, firstPartial.Code);
        Assert.Equal(1m, firstPartial.Value!.Lines.Single().RemainingReceivableQuantityAfter);

        var midway = await fixture.GoodsReceiptService.ListEligibleSourcesAsync(fixture.Context(Requester, "tenant.procurement.goods-receipt.view"));
        Assert.True(midway.Succeeded, midway.Code);
        Assert.Equal(1m, Assert.Single(midway.Value!).Lines.Single().RemainingReceivableQuantity);

        var overReceipt = await fixture.GoodsReceiptService.CreateAsync(
            fixture.Context(Requester, "tenant.procurement.goods-receipt.create"),
            new GoodsReceiptCreateRequest(confirmed.Id, WarehouseA, DateOnly.FromDateTime(DateTime.UtcNow.Date), "GRN-OVER", null, [new GoodsReceiptLineCreateRequest(line.Id, 2m, 2m, 0m, null, null, null)]),
            "gr-over-receipt",
            "fp-gr-over-receipt");
        Assert.False(overReceipt.Succeeded);
        Assert.Equal("over_receipt_not_allowed", overReceipt.Code);

        var secondPartial = await fixture.GoodsReceiptService.CreateAsync(
            fixture.Context(Requester, "tenant.procurement.goods-receipt.create"),
            new GoodsReceiptCreateRequest(confirmed.Id, WarehouseA, DateOnly.FromDateTime(DateTime.UtcNow.Date), "GRN-P2", null, [new GoodsReceiptLineCreateRequest(line.Id, 1m, 1m, 0m, null, null, null)]),
            "gr-partial-2",
            "fp-gr-partial-2");
        Assert.True(secondPartial.Succeeded, secondPartial.Code);
        Assert.Equal(0m, secondPartial.Value!.Lines.Single().RemainingReceivableQuantityAfter);

        var exhausted = await fixture.GoodsReceiptService.ListEligibleSourcesAsync(fixture.Context(Requester, "tenant.procurement.goods-receipt.view"));
        Assert.True(exhausted.Succeeded, exhausted.Code);
        Assert.Empty(exhausted.Value!);

        var list = await fixture.GoodsReceiptService.ListAsync(fixture.Context(Requester, "tenant.procurement.goods-receipt.view"), null, confirmed.Id);
        Assert.True(list.Succeeded, list.Code);
        Assert.Equal(2, list.Value!.Count);
    }

    [Fact]
    public async Task Enforces_accepted_plus_rejected_equals_received_while_treating_damaged_as_independent()
    {
        await using var fixture = await Fixture.CreateAsync();
        var confirmed = await fixture.ConfirmedOrderAsync("gr-quantity-integrity");
        var line = confirmed.Lines.Single();

        var unbalanced = await fixture.GoodsReceiptService.CreateAsync(
            fixture.Context(Requester, "tenant.procurement.goods-receipt.create"),
            new GoodsReceiptCreateRequest(confirmed.Id, WarehouseA, DateOnly.FromDateTime(DateTime.UtcNow.Date), null, null, [new GoodsReceiptLineCreateRequest(line.Id, 2m, 2m, 1m, null, null, null)]),
            "gr-unbalanced",
            "fp-gr-unbalanced");
        Assert.False(unbalanced.Succeeded);
        Assert.Equal("validation_failed", unbalanced.Code);

        var damagedExceedsReceived = await fixture.GoodsReceiptService.CreateAsync(
            fixture.Context(Requester, "tenant.procurement.goods-receipt.create"),
            new GoodsReceiptCreateRequest(confirmed.Id, WarehouseA, DateOnly.FromDateTime(DateTime.UtcNow.Date), null, null, [new GoodsReceiptLineCreateRequest(line.Id, 2m, 1m, 1m, 3m, "Crushed box", null)]),
            "gr-damaged-exceeds",
            "fp-gr-damaged-exceeds");
        Assert.False(damagedExceedsReceived.Succeeded);
        Assert.Equal("validation_failed", damagedExceedsReceived.Code);

        // Received = Accepted + Rejected (two-way). Damaged is an independent, non-additive descriptive
        // bucket bounded only by <= Received, never summed into the Received equation as a third term.
        var recorded = await fixture.GoodsReceiptService.CreateAsync(
            fixture.Context(Requester, "tenant.procurement.goods-receipt.create"),
            new GoodsReceiptCreateRequest(confirmed.Id, WarehouseA, DateOnly.FromDateTime(DateTime.UtcNow.Date), null, null, [new GoodsReceiptLineCreateRequest(line.Id, 2m, 1m, 1m, 1m, "One accepted unit was also damaged", null)]),
            "gr-balanced-damaged",
            "fp-gr-balanced-damaged");
        Assert.True(recorded.Succeeded, recorded.Code);
        var recordedLine = recorded.Value!.Lines.Single();
        Assert.Equal(2m, recordedLine.ReceivedQuantity);
        Assert.Equal(1m, recordedLine.AcceptedQuantity);
        Assert.Equal(1m, recordedLine.RejectedQuantity);
        Assert.Equal(1m, recordedLine.DamagedQuantity);
        Assert.Equal(1m, recordedLine.RemainingReceivableQuantityAfter);

        // A rejected quantity is never silently treated as accepted stock or as closing the remaining
        // supplier obligation: the remainder still reflects only the accepted portion.
        var eligible = await fixture.GoodsReceiptService.ListEligibleSourcesAsync(fixture.Context(Requester, "tenant.procurement.goods-receipt.view"));
        Assert.True(eligible.Succeeded, eligible.Code);
        Assert.Equal(1m, Assert.Single(eligible.Value!).Lines.Single().RemainingReceivableQuantity);
    }

    [Fact]
    public async Task Denies_cross_tenant_reads_and_authorizes_within_the_recording_tenant()
    {
        await using var fixture = await Fixture.CreateAsync();
        var confirmed = await fixture.ConfirmedOrderAsync("gr-cross-tenant");
        var created = await fixture.GoodsReceiptService.CreateAsync(
            fixture.Context(Requester, "tenant.procurement.goods-receipt.create"),
            new GoodsReceiptCreateRequest(confirmed.Id, WarehouseA, DateOnly.FromDateTime(DateTime.UtcNow.Date), null, null, [new GoodsReceiptLineCreateRequest(confirmed.Lines.Single().Id, 2m, 2m, 0m, null, null, null)]),
            "gr-cross-tenant-create",
            "fp-gr-cross-tenant-create");
        Assert.True(created.Succeeded, created.Code);

        var foreign = await fixture.GoodsReceiptService.GetAsync(fixture.Context(Requester, "tenant.procurement.goods-receipt.view", TenantB), created.Value!.Id);
        Assert.False(foreign.Succeeded);
        Assert.Equal("goods_receipt_not_found", foreign.Code);

        var owned = await fixture.GoodsReceiptService.GetAsync(fixture.Context(Requester, "tenant.procurement.goods-receipt.view"), created.Value.Id);
        Assert.True(owned.Succeeded, owned.Code);
        Assert.Equal(created.Value.Id, owned.Value!.Id);
    }

    [Fact]
    public async Task Enforces_optimistic_concurrency_and_durable_idempotent_replay_on_cancel()
    {
        await using var fixture = await Fixture.CreateAsync();
        var confirmed = await fixture.ConfirmedOrderAsync("gr-concurrency");
        var created = await fixture.GoodsReceiptService.CreateAsync(
            fixture.Context(Requester, "tenant.procurement.goods-receipt.create"),
            new GoodsReceiptCreateRequest(confirmed.Id, WarehouseA, DateOnly.FromDateTime(DateTime.UtcNow.Date), null, null, [new GoodsReceiptLineCreateRequest(confirmed.Lines.Single().Id, 2m, 2m, 0m, null, null, null)]),
            "gr-concurrency-create",
            "fp-gr-concurrency-create");
        Assert.True(created.Succeeded, created.Code);

        var staleVersion = new byte[] { 1, 2, 3, 4, 5, 6, 7, 8 };
        var staleCancel = await fixture.GoodsReceiptService.CancelAsync(
            fixture.Context(Requester, "tenant.procurement.goods-receipt.cancel"),
            created.Value!.Id,
            staleVersion,
            "Stale attempt",
            "gr-concurrency-stale",
            "fp-gr-concurrency-stale");
        Assert.False(staleCancel.Succeeded);
        Assert.Equal("concurrency_conflict", staleCancel.Code);

        const string sharedKey = "gr-concurrency-shared-cancel";
        var firstCancel = await fixture.GoodsReceiptService.CancelAsync(
            fixture.Context(Requester, "tenant.procurement.goods-receipt.cancel"),
            created.Value.Id,
            created.Value.Version,
            "No longer needed",
            sharedKey,
            "fp-gr-concurrency-cancel-payload");
        Assert.True(firstCancel.Succeeded, firstCancel.Code);
        Assert.Equal(GoodsReceiptStatus.Cancelled, firstCancel.Value!.Status);

        // Identical retry replays the original cancellation result rather than re-mutating.
        var replay = await fixture.GoodsReceiptService.CancelAsync(
            fixture.Context(Requester, "tenant.procurement.goods-receipt.cancel"),
            created.Value.Id,
            created.Value.Version,
            "No longer needed",
            sharedKey,
            "fp-gr-concurrency-cancel-payload");
        Assert.True(replay.Succeeded, replay.Code);
        Assert.Equal(firstCancel.Value.Version, replay.Value!.Version);

        // Same key, different payload fingerprint: rejected as a conflict rather than replayed or re-mutated.
        var conflicting = await fixture.GoodsReceiptService.CancelAsync(
            fixture.Context(Requester, "tenant.procurement.goods-receipt.cancel"),
            created.Value.Id,
            created.Value.Version,
            "Different reason",
            sharedKey,
            "fp-gr-concurrency-cancel-different-payload");
        Assert.False(conflicting.Succeeded);
        Assert.Equal("idempotency_conflict", conflicting.Code);

        var history = await fixture.GoodsReceiptService.ReadHistoryAsync(fixture.Context(Requester, "tenant.procurement.goods-receipt.history"), created.Value.Id);
        Assert.True(history.Succeeded, history.Code);
        Assert.Equal(1, history.Value!.Count(item => item.Action == GoodsReceiptHistoryAction.Cancelled));
        var audit = await fixture.GoodsReceiptService.ReadAuditAsync(fixture.Context(Requester, "tenant.procurement.goods-receipt.audit"), created.Value.Id);
        Assert.True(audit.Succeeded, audit.Code);
        Assert.Equal(1, audit.Value!.Count(item => item.IdempotencyKey == sharedKey));
    }

    [Fact]
    public async Task Blocks_cancellation_while_referenced_by_an_active_purchase_invoice_handoff_and_releases_after_it_is_cancelled()
    {
        await using var fixture = await Fixture.CreateAsync();
        var confirmed = await fixture.ConfirmedOrderAsync("gr-handoff-block");
        var created = await fixture.GoodsReceiptService.CreateAsync(
            fixture.Context(Requester, "tenant.procurement.goods-receipt.create"),
            new GoodsReceiptCreateRequest(confirmed.Id, WarehouseA, DateOnly.FromDateTime(DateTime.UtcNow.Date), null, null, [new GoodsReceiptLineCreateRequest(confirmed.Lines.Single().Id, 2m, 2m, 0m, null, null, null)]),
            "gr-handoff-block-create",
            "fp-gr-handoff-block-create");
        Assert.True(created.Succeeded, created.Code);
        var receiptLine = created.Value!.Lines.Single();

        var eligibleHandoffSources = await fixture.InvoiceHandoffService.ListEligibleSourcesAsync(fixture.Context(Requester, "tenant.procurement.invoice-handoff.view"));
        Assert.True(eligibleHandoffSources.Succeeded, eligibleHandoffSources.Code);
        var handoffSource = Assert.Single(eligibleHandoffSources.Value!);
        var handoffLine = Assert.Single(handoffSource.Lines, item => item.GoodsReceiptId == created.Value.Id && item.GoodsReceiptLineId == receiptLine.Id);

        var handoff = await fixture.InvoiceHandoffService.CreateAsync(
            fixture.Context(Requester, "tenant.procurement.invoice-handoff.create"),
            new PurchaseInvoiceHandoffCreateRequest(confirmed.Id, "SUP-INV-1", DateOnly.FromDateTime(DateTime.UtcNow.Date), null, [new PurchaseInvoiceHandoffSourceRequest(handoffLine.GoodsReceiptId, handoffLine.GoodsReceiptLineId, 2m)]),
            "gr-handoff-block-handoff-create",
            "fp-gr-handoff-block-handoff-create");
        Assert.True(handoff.Succeeded, handoff.Code);

        var blockedCancel = await fixture.GoodsReceiptService.CancelAsync(
            fixture.Context(Requester, "tenant.procurement.goods-receipt.cancel"),
            created.Value.Id,
            created.Value.Version,
            "Attempting cancel while referenced",
            "gr-handoff-block-cancel-attempt",
            "fp-gr-handoff-block-cancel-attempt");
        Assert.False(blockedCancel.Succeeded);
        Assert.Equal("goods_receipt_referenced_by_active_invoice_handoff", blockedCancel.Code);

        var handoffCancel = await fixture.InvoiceHandoffService.CancelAsync(
            fixture.Context(Requester, "tenant.procurement.invoice-handoff.cancel"),
            handoff.Value!.Id,
            handoff.Value.Version,
            "Reverting handoff to unblock the receipt cancel test",
            "gr-handoff-block-handoff-cancel",
            "fp-gr-handoff-block-handoff-cancel");
        Assert.True(handoffCancel.Succeeded, handoffCancel.Code);

        // Cancelling the handoff never blocks or reverses the source Goods Receipt itself.
        var untouchedReceipt = await fixture.GoodsReceiptService.GetAsync(fixture.Context(Requester, "tenant.procurement.goods-receipt.view"), created.Value.Id);
        Assert.True(untouchedReceipt.Succeeded, untouchedReceipt.Code);
        Assert.Equal(GoodsReceiptStatus.Recorded, untouchedReceipt.Value!.Status);

        var releasedCancel = await fixture.GoodsReceiptService.CancelAsync(
            fixture.Context(Requester, "tenant.procurement.goods-receipt.cancel"),
            created.Value.Id,
            created.Value.Version,
            "No longer referenced by an active handoff",
            "gr-handoff-block-cancel-released",
            "fp-gr-handoff-block-cancel-released");
        Assert.True(releasedCancel.Succeeded, releasedCancel.Code);
        Assert.Equal(GoodsReceiptStatus.Cancelled, releasedCancel.Value!.Status);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection;
        private readonly DbContextOptions options;

        private Fixture(SqliteConnection connection, DbContextOptions options, PurchaseOrderService purchaseOrderService, GoodsReceiptService goodsReceiptService, PurchaseInvoiceHandoffService invoiceHandoffService)
        {
            this.connection = connection;
            this.options = options;
            PurchaseOrderService = purchaseOrderService;
            GoodsReceiptService = goodsReceiptService;
            InvoiceHandoffService = invoiceHandoffService;
        }

        public PurchaseOrderService PurchaseOrderService { get; }
        public GoodsReceiptService GoodsReceiptService { get; }
        public PurchaseInvoiceHandoffService InvoiceHandoffService { get; }

        public static async Task<Fixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder().UseSqlite(connection).Options;
            await using (var db = new ProcurementDbContext(options, CreateTenantContext(TenantA, Requester)))
            {
                await db.Database.EnsureCreatedAsync();
                await SeedAsync(db);
            }

            var authorization = new PurchaseRequestAuthorizationService();
            var purchaseOrderService = new PurchaseOrderService(
                authorization,
                new PurchaseOrderPersistence(options),
                new PurchaseRequestPersistence(options),
                new SupplierQuotationPersistence(options),
                new DefaultPurchaseRequestApprovalPolicyProvider(),
                new NoPurchaseRequestApprovalDelegationProvider());
            var goodsReceiptService = new GoodsReceiptService(authorization, new GoodsReceiptPersistence(options));
            var invoiceHandoffService = new PurchaseInvoiceHandoffService(authorization, new PurchaseInvoiceHandoffPersistence(options));
            return new Fixture(connection, options, purchaseOrderService, goodsReceiptService, invoiceHandoffService);
        }

        public ProcurementRequestContext Context(Guid actor, string operation, Guid tenantId = default)
        {
            tenantId = tenantId == Guid.Empty ? TenantA : tenantId;
            var foundation = FoundationRequestContext.ForTenant(actor, Guid.NewGuid(), CreateTenantContext(tenantId, actor), operation);
            var resolved = new ProcurementTenantContextResolver().Resolve(foundation);
            return Assert.IsType<ProcurementRequestContext>(resolved.Context);
        }

        /// <summary>
        /// Drives the seeded source decision through create, submit, approve, and issue so a Goods Receipt
        /// fixture can either confirm it (eligible source) or exercise it while still Issued (wrong stage).
        /// </summary>
        public async Task<PurchaseOrderRecord> IssuedOrderAsync(string keyPrefix)
        {
            var source = Assert.Single((await PurchaseOrderService.ListSourceOptionsAsync(Context(Requester, "tenant.procurement.purchase-order.view"))).Value!);
            var created = await PurchaseOrderService.CreateAsync(Context(Requester, "tenant.procurement.purchase-order.create"), new PurchaseOrderCreateRequest(source.Source.SourceDecisionId), $"{keyPrefix}-create", $"fp-{keyPrefix}-create");
            Assert.True(created.Succeeded, created.Code);
            var submitted = await PurchaseOrderService.SubmitAsync(Context(Requester, "tenant.procurement.purchase-order.submit"), created.Value!.Id, created.Value.Version, $"{keyPrefix}-submit", $"fp-{keyPrefix}-submit");
            Assert.True(submitted.Succeeded, submitted.Code);
            var approved = await PurchaseOrderService.ApproveAsync(Context(Approver, "tenant.procurement.purchase-order.approve"), submitted.Value!.Id, submitted.Value.Version, $"{keyPrefix}-approve", $"fp-{keyPrefix}-approve");
            Assert.True(approved.Succeeded, approved.Code);
            var issued = await PurchaseOrderService.IssueAsync(Context(Approver, "tenant.procurement.purchase-order.issue"), approved.Value!.Id, approved.Value.Version, $"{keyPrefix}-issue", $"fp-{keyPrefix}-issue");
            Assert.True(issued.Succeeded, issued.Code);
            return issued.Value!;
        }

        /// <summary>
        /// Drives the seeded source decision to a plain Confirmed Purchase Order (no supplier-proposed
        /// change on any of the three Proposed* fields, so confirmation lands directly on Confirmed rather
        /// than detouring through ChangedPendingApproval) so it is a genuinely eligible Goods Receipt source.
        /// </summary>
        public async Task<PurchaseOrderRecord> ConfirmedOrderAsync(string keyPrefix)
        {
            var issued = await IssuedOrderAsync(keyPrefix);
            var line = issued.Lines.Single();
            var confirmed = await PurchaseOrderService.RecordConfirmationAsync(
                Context(Requester, "tenant.procurement.purchase-order.confirmation.capture"),
                issued.Id,
                new PurchaseOrderConfirmationRequest(
                    PurchaseOrderConfirmationStatus.Confirmed,
                    DateOnly.FromDateTime(DateTime.UtcNow.Date),
                    $"SUP-{keyPrefix}",
                    "supplier@test",
                    null,
                    null,
                    [new PurchaseOrderConfirmationLineRequest(line.Id, line.OrderedQuantity, DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(7)), null, null, null, null)],
                    []),
                issued.Version,
                $"{keyPrefix}-confirm",
                $"fp-{keyPrefix}-confirm");
            Assert.True(confirmed.Succeeded, confirmed.Code);
            Assert.Equal(PurchaseOrderStatus.Confirmed, confirmed.Value!.Status);
            return confirmed.Value!;
        }

        private static async Task SeedAsync(ProcurementDbContext db)
        {
            var now = DateTimeOffset.UtcNow;
            var scope = new PurchaseRequestScope(TenantA, CompanyA, BranchA);
            var lineId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");
            var quotationId = Guid.Parse("aaaaaaaa-aaaa-1111-1111-111111111111");
            var decisionId = Guid.Parse("aaaaaaaa-aaaa-2222-2222-222222222222");
            var policy = new PurchaseRequestApprovalPolicyDefinition("procurement.goods-receipt.test", 1, [new PurchaseRequestApprovalStageDefinition("manager", 1, 1, [], false)], true, now.AddDays(-1));
            var request = new PurchaseRequestEntity(Guid.Parse("aaaaaaaa-aaaa-3333-3333-333333333333"), new TenantId(TenantA), CompanyA, BranchA, Requester, "Approved demand", now);
            request.Lines.Add(new PurchaseRequestLineEntity(lineId, new TenantId(TenantA), request.Id, new PurchaseRequestLineSnapshot(lineId, Product, "SKU-001", "Test Product", Unit, "EA", 2m, DateOnly.FromDateTime(now.UtcDateTime.Date.AddDays(7)), "Approved demand line")));
            request.Submit(policy, JsonSerializer.Serialize(policy), now);
            request.RecordApproval(PurchaseRequestStatus.Approved, 0, 0, "[]", now);
            request.TouchVersion();

            var quotationLine = new SupplierQuotationLineSnapshot(Guid.Parse("aaaaaaaa-aaaa-4444-4444-444444444444"), lineId, Product, "SKU-001", "Test Product", Unit, "EA", 2m, 2m, 12.5m, null, null, null, null, null, null, null, null, DateOnly.FromDateTime(now.UtcDateTime.Date.AddDays(7)), DateOnly.FromDateTime(now.UtcDateTime.Date.AddDays(9)), "9 days", "Quoted line");
            var supplier = new SupplierQuotationSupplierSnapshot(Supplier, "SUP-A", "Alpha Supplier");
            var currency = new SupplierQuotationCurrencySnapshot(Currency, "USD", "US Dollar");
            var quotationCommand = new SupplierQuotationCreateCommand(quotationId, request.Id, scope, Requester, supplier, "Q-GR-1", DateOnly.FromDateTime(now.UtcDateTime.Date), DateOnly.FromDateTime(now.UtcDateTime.Date.AddDays(14)), currency, null, "Delivered", DateOnly.FromDateTime(now.UtcDateTime.Date.AddDays(9)), "9 days", "Seed quote", [quotationLine], [], now, "seed");
            var quotation = new SupplierQuotationEntity(quotationCommand, new TenantId(TenantA));
            quotation.Lines.Add(new SupplierQuotationLineEntity(new TenantId(TenantA), quotationId, request.Id, quotationLine));
            quotation.SetStatus(SupplierQuotationStatus.Submitted, now);
            quotation.TouchVersion();
            var quotationRecord = new SupplierQuotationRecord(quotationId, TenantA, request.Id, scope, Requester, supplier, SupplierQuotationStatus.Submitted, "Q-GR-1", quotationCommand.OfferDate, quotationCommand.ValidUntil, currency, null, quotationCommand.DeliveryTerms, quotationCommand.OfferedDeliveryDate, quotationCommand.OfferedDeliveryLeadTime, quotationCommand.Notes, [quotationLine], [], now, now, now, quotation.Version);
            var decisionCommand = new SupplierSourceDecisionCommand(decisionId, request.Id, scope, quotationId, Requester, now, "Selected for test", null, null, null, "sha256:test", "{}", request.Version, "seed-decision");
            var decision = new SupplierSourceDecisionEntity(decisionCommand, new TenantId(TenantA), quotationRecord);
            decision.TouchVersion();
            db.PurchaseRequests.Add(request);
            db.SupplierQuotations.Add(quotation);
            db.SupplierSourceDecisions.Add(decision);
            await db.SaveChangesAsync();
        }

        public async ValueTask DisposeAsync() => await connection.DisposeAsync();
    }

    private static TenantContext CreateTenantContext(Guid tenantId, Guid actor) => TenantContext.ForOrdinaryMembership(new TenantId(tenantId), new MembershipReference(Guid.NewGuid()), new ScopeReference($"Company:{CompanyA:D}"), new CorrelationId($"corr-{Guid.NewGuid():N}"), actor);
}
