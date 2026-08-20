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

public sealed class PurchaseInvoiceHandoffTests
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
    public async Task Lists_eligible_accepted_lines_only_while_remaining_handoff_quantity_is_positive()
    {
        await using var fixture = await Fixture.CreateAsync();
        var receipt = await fixture.RecordedReceiptAsync("pih-eligibility");
        var receiptLine = receipt.Lines.Single();

        var before = await fixture.InvoiceHandoffService.ListEligibleSourcesAsync(
            fixture.Context(Requester, "tenant.procurement.invoice-handoff.view"));
        Assert.True(before.Succeeded, before.Code);
        var source = Assert.Single(before.Value!);
        Assert.Equal(receipt.PurchaseOrderId, source.PurchaseOrderId);
        var eligibleLine = Assert.Single(source.Lines);
        Assert.Equal(2m, eligibleLine.AcceptedQuantity);
        Assert.Equal(0m, eligibleLine.AlreadyHandedOffQuantity);
        Assert.Equal(2m, eligibleLine.RemainingHandoffQuantity);

        var fullHandoff = await fixture.InvoiceHandoffService.CreateAsync(
            fixture.Context(Requester, "tenant.procurement.invoice-handoff.create"),
            new PurchaseInvoiceHandoffCreateRequest(
                receipt.PurchaseOrderId,
                "INV-FULL-001",
                DateOnly.FromDateTime(DateTime.UtcNow.Date),
                "Full handoff notes",
                [new PurchaseInvoiceHandoffSourceRequest(receipt.Id, receiptLine.Id, 2m)]),
            "pih-full-key",
            "fp-pih-full");
        Assert.True(fullHandoff.Succeeded, fullHandoff.Code);

        var after = await fixture.InvoiceHandoffService.ListEligibleSourcesAsync(
            fixture.Context(Requester, "tenant.procurement.invoice-handoff.view"));
        Assert.True(after.Succeeded, after.Code);
        Assert.Empty(after.Value!);
    }

    [Fact]
    public async Task Creates_invoice_handoff_with_exact_prorata_tax_recalculation()
    {
        await using var fixture = await Fixture.CreateAsync();
        var receipt = await fixture.RecordedReceiptAsync("pih-tax");
        var receiptLine = receipt.Lines.Single();

        // 1 unit at 12.50 unit price with 15% tax:
        // subtotal = 12.50, tax = 12.50 * 0.15 = 1.875 -> 1.88, line total = 14.38
        var partialHandoff = await fixture.InvoiceHandoffService.CreateAsync(
            fixture.Context(Requester, "tenant.procurement.invoice-handoff.create"),
            new PurchaseInvoiceHandoffCreateRequest(
                receipt.PurchaseOrderId,
                "INV-TAX-001",
                DateOnly.FromDateTime(DateTime.UtcNow.Date),
                "Partial pro-rata tax verification",
                [new PurchaseInvoiceHandoffSourceRequest(receipt.Id, receiptLine.Id, 1m)]),
            "pih-tax-key",
            "fp-pih-tax");
        Assert.True(partialHandoff.Succeeded, partialHandoff.Code);
        var record = partialHandoff.Value!;
        Assert.Equal(PurchaseInvoiceHandoffStatus.Recorded, record.Status);
        Assert.Equal("INV-TAX-001", record.SupplierInvoiceReference);

        var line = Assert.Single(record.Lines);
        Assert.Equal(1m, line.HandoffQuantity);
        Assert.Equal(12.5m, line.UnitPrice);
        Assert.Equal(15m, line.TaxRatePercentage);
        Assert.Equal(1.88m, line.TaxAmount);
        Assert.Equal(14.38m, line.LineAmount);

        var src = Assert.Single(record.Sources);
        Assert.Equal(receipt.Id, src.GoodsReceiptId);
        Assert.Equal(receiptLine.Id, src.GoodsReceiptLineId);
        Assert.Equal(1m, src.Quantity);
    }

    [Fact]
    public async Task Records_partial_handoffs_sequentially_until_the_remainder_is_exhausted()
    {
        await using var fixture = await Fixture.CreateAsync();
        var receipt = await fixture.RecordedReceiptAsync("pih-partial");
        var receiptLine = receipt.Lines.Single();

        var handoff1 = await fixture.InvoiceHandoffService.CreateAsync(
            fixture.Context(Requester, "tenant.procurement.invoice-handoff.create"),
            new PurchaseInvoiceHandoffCreateRequest(
                receipt.PurchaseOrderId,
                "INV-P1",
                DateOnly.FromDateTime(DateTime.UtcNow.Date),
                null,
                [new PurchaseInvoiceHandoffSourceRequest(receipt.Id, receiptLine.Id, 1m)]),
            "pih-p1-key",
            "fp-pih-p1");
        Assert.True(handoff1.Succeeded, handoff1.Code);

        var midway = await fixture.InvoiceHandoffService.ListEligibleSourcesAsync(
            fixture.Context(Requester, "tenant.procurement.invoice-handoff.view"));
        Assert.True(midway.Succeeded, midway.Code);
        var midwaySource = Assert.Single(midway.Value!);
        var midwayLine = Assert.Single(midwaySource.Lines);
        Assert.Equal(1m, midwayLine.AlreadyHandedOffQuantity);
        Assert.Equal(1m, midwayLine.RemainingHandoffQuantity);

        // Attempting to hand off 2 units when only 1 unit remains must fail
        var overHandoff = await fixture.InvoiceHandoffService.CreateAsync(
            fixture.Context(Requester, "tenant.procurement.invoice-handoff.create"),
            new PurchaseInvoiceHandoffCreateRequest(
                receipt.PurchaseOrderId,
                "INV-OVER",
                DateOnly.FromDateTime(DateTime.UtcNow.Date),
                null,
                [new PurchaseInvoiceHandoffSourceRequest(receipt.Id, receiptLine.Id, 2m)]),
            "pih-over-key",
            "fp-pih-over");
        Assert.False(overHandoff.Succeeded);
        Assert.Equal("over_handoff_not_allowed", overHandoff.Code);

        // Hand off the remaining 1 unit
        var handoff2 = await fixture.InvoiceHandoffService.CreateAsync(
            fixture.Context(Requester, "tenant.procurement.invoice-handoff.create"),
            new PurchaseInvoiceHandoffCreateRequest(
                receipt.PurchaseOrderId,
                "INV-P2",
                DateOnly.FromDateTime(DateTime.UtcNow.Date),
                null,
                [new PurchaseInvoiceHandoffSourceRequest(receipt.Id, receiptLine.Id, 1m)]),
            "pih-p2-key",
            "fp-pih-p2");
        Assert.True(handoff2.Succeeded, handoff2.Code);

        var list = await fixture.InvoiceHandoffService.ListAsync(
            fixture.Context(Requester, "tenant.procurement.invoice-handoff.view"),
            null);
        Assert.True(list.Succeeded, list.Code);
        Assert.Equal(2, list.Value!.Count);
    }

    [Fact]
    public async Task Denies_cross_tenant_reads_and_authorizes_within_the_recording_tenant()
    {
        await using var fixture = await Fixture.CreateAsync();
        var receipt = await fixture.RecordedReceiptAsync("pih-cross-tenant");
        var receiptLine = receipt.Lines.Single();

        var created = await fixture.InvoiceHandoffService.CreateAsync(
            fixture.Context(Requester, "tenant.procurement.invoice-handoff.create"),
            new PurchaseInvoiceHandoffCreateRequest(
                receipt.PurchaseOrderId,
                "INV-CROSS-1",
                DateOnly.FromDateTime(DateTime.UtcNow.Date),
                null,
                [new PurchaseInvoiceHandoffSourceRequest(receipt.Id, receiptLine.Id, 2m)]),
            "pih-cross-key",
            "fp-pih-cross");
        Assert.True(created.Succeeded, created.Code);

        var foreign = await fixture.InvoiceHandoffService.GetAsync(
            fixture.Context(Requester, "tenant.procurement.invoice-handoff.view", TenantB),
            created.Value!.Id);
        Assert.False(foreign.Succeeded);
        Assert.Equal("invoice_handoff_not_found", foreign.Code);

        var owned = await fixture.InvoiceHandoffService.GetAsync(
            fixture.Context(Requester, "tenant.procurement.invoice-handoff.view"),
            created.Value.Id);
        Assert.True(owned.Succeeded, owned.Code);
        Assert.Equal(created.Value.Id, owned.Value!.Id);
    }

    [Fact]
    public async Task Enforces_optimistic_concurrency_and_durable_idempotent_replay_on_cancel()
    {
        await using var fixture = await Fixture.CreateAsync();
        var receipt = await fixture.RecordedReceiptAsync("pih-concurrency");
        var receiptLine = receipt.Lines.Single();

        var created = await fixture.InvoiceHandoffService.CreateAsync(
            fixture.Context(Requester, "tenant.procurement.invoice-handoff.create"),
            new PurchaseInvoiceHandoffCreateRequest(
                receipt.PurchaseOrderId,
                "INV-CONCUR-1",
                DateOnly.FromDateTime(DateTime.UtcNow.Date),
                null,
                [new PurchaseInvoiceHandoffSourceRequest(receipt.Id, receiptLine.Id, 2m)]),
            "pih-concur-create",
            "fp-pih-concur-create");
        Assert.True(created.Succeeded, created.Code);

        var staleVersion = new byte[] { 9, 8, 7, 6, 5, 4, 3, 2 };
        var staleCancel = await fixture.InvoiceHandoffService.CancelAsync(
            fixture.Context(Requester, "tenant.procurement.invoice-handoff.cancel"),
            created.Value!.Id,
            staleVersion,
            "Stale attempt",
            "pih-concur-stale",
            "fp-pih-concur-stale");
        Assert.False(staleCancel.Succeeded);
        Assert.Equal("concurrency_conflict", staleCancel.Code);

        const string sharedKey = "pih-shared-cancel-key";
        var firstCancel = await fixture.InvoiceHandoffService.CancelAsync(
            fixture.Context(Requester, "tenant.procurement.invoice-handoff.cancel"),
            created.Value.Id,
            created.Value.Version,
            "Cancellation reason",
            sharedKey,
            "fp-pih-cancel-payload");
        Assert.True(firstCancel.Succeeded, firstCancel.Code);
        Assert.Equal(PurchaseInvoiceHandoffStatus.Cancelled, firstCancel.Value!.Status);

        // Identical replay
        var replay = await fixture.InvoiceHandoffService.CancelAsync(
            fixture.Context(Requester, "tenant.procurement.invoice-handoff.cancel"),
            created.Value.Id,
            created.Value.Version,
            "Cancellation reason",
            sharedKey,
            "fp-pih-cancel-payload");
        Assert.True(replay.Succeeded, replay.Code);
        Assert.Equal(firstCancel.Value.Version, replay.Value!.Version);

        // Conflicting payload with same idempotency key
        var conflicting = await fixture.InvoiceHandoffService.CancelAsync(
            fixture.Context(Requester, "tenant.procurement.invoice-handoff.cancel"),
            created.Value.Id,
            created.Value.Version,
            "Different reason",
            sharedKey,
            "fp-pih-cancel-different-payload");
        Assert.False(conflicting.Succeeded);
        Assert.Equal("idempotency_conflict", conflicting.Code);

        var history = await fixture.InvoiceHandoffService.ReadHistoryAsync(
            fixture.Context(Requester, "tenant.procurement.invoice-handoff.history"),
            created.Value.Id);
        Assert.True(history.Succeeded, history.Code);
        Assert.Equal(1, history.Value!.Count(item => item.Action == PurchaseInvoiceHandoffHistoryAction.Cancelled));

        var audit = await fixture.InvoiceHandoffService.ReadAuditAsync(
            fixture.Context(Requester, "tenant.procurement.invoice-handoff.audit"),
            created.Value.Id);
        Assert.True(audit.Succeeded, audit.Code);
        Assert.Equal(1, audit.Value!.Count(item => item.IdempotencyKey == sharedKey));
    }

    [Fact]
    public async Task Cancelling_invoice_handoff_releases_remaining_handoff_quantity_and_never_affects_goods_receipt()
    {
        await using var fixture = await Fixture.CreateAsync();
        var receipt = await fixture.RecordedReceiptAsync("pih-release");
        var receiptLine = receipt.Lines.Single();

        var handoff = await fixture.InvoiceHandoffService.CreateAsync(
            fixture.Context(Requester, "tenant.procurement.invoice-handoff.create"),
            new PurchaseInvoiceHandoffCreateRequest(
                receipt.PurchaseOrderId,
                "INV-REL-1",
                DateOnly.FromDateTime(DateTime.UtcNow.Date),
                null,
                [new PurchaseInvoiceHandoffSourceRequest(receipt.Id, receiptLine.Id, 2m)]),
            "pih-rel-create",
            "fp-pih-rel-create");
        Assert.True(handoff.Succeeded, handoff.Code);

        var cancel = await fixture.InvoiceHandoffService.CancelAsync(
            fixture.Context(Requester, "tenant.procurement.invoice-handoff.cancel"),
            handoff.Value!.Id,
            handoff.Value.Version,
            "Mistake in reference",
            "pih-rel-cancel",
            "fp-pih-rel-cancel");
        Assert.True(cancel.Succeeded, cancel.Code);

        // Source Goods Receipt remains in Recorded status
        var receiptCheck = await fixture.GoodsReceiptService.GetAsync(
            fixture.Context(Requester, "tenant.procurement.goods-receipt.view"),
            receipt.Id);
        Assert.True(receiptCheck.Succeeded, receiptCheck.Code);
        Assert.Equal(GoodsReceiptStatus.Recorded, receiptCheck.Value!.Status);

        // Released handoff quantity is available again
        var eligible = await fixture.InvoiceHandoffService.ListEligibleSourcesAsync(
            fixture.Context(Requester, "tenant.procurement.invoice-handoff.view"));
        Assert.True(eligible.Succeeded, eligible.Code);
        var eligibleLine = Assert.Single(Assert.Single(eligible.Value!).Lines);
        Assert.Equal(2m, eligibleLine.RemainingHandoffQuantity);
    }

    [Fact]
    public async Task Concurrent_invoice_handoff_requests_prevent_atomic_over_handoff()
    {
        await using var fixture = await Fixture.CreateAsync();
        var receipt = await fixture.RecordedReceiptAsync("pih-concur-race");
        var receiptLine = receipt.Lines.Single();

        var task1 = fixture.InvoiceHandoffService.CreateAsync(
            fixture.Context(Requester, "tenant.procurement.invoice-handoff.create"),
            new PurchaseInvoiceHandoffCreateRequest(
                receipt.PurchaseOrderId,
                "INV-RACE-1",
                DateOnly.FromDateTime(DateTime.UtcNow.Date),
                null,
                [new PurchaseInvoiceHandoffSourceRequest(receipt.Id, receiptLine.Id, 2m)]),
            "pih-race-key-1",
            "fp-pih-race-1");

        var task2 = fixture.InvoiceHandoffService.CreateAsync(
            fixture.Context(Requester, "tenant.procurement.invoice-handoff.create"),
            new PurchaseInvoiceHandoffCreateRequest(
                receipt.PurchaseOrderId,
                "INV-RACE-2",
                DateOnly.FromDateTime(DateTime.UtcNow.Date),
                null,
                [new PurchaseInvoiceHandoffSourceRequest(receipt.Id, receiptLine.Id, 2m)]),
            "pih-race-key-2",
            "fp-pih-race-2");

        var results = await Task.WhenAll(task1, task2);
        var successCount = results.Count(r => r.Succeeded);
        var failureCount = results.Count(r => !r.Succeeded);

        Assert.Equal(1, successCount);
        Assert.Equal(1, failureCount);
        Assert.Contains(results.First(r => !r.Succeeded).Code, new[] { "over_handoff_not_allowed", "concurrency_conflict" });

        var list = await fixture.InvoiceHandoffService.ListAsync(
            fixture.Context(Requester, "tenant.procurement.invoice-handoff.view"),
            PurchaseInvoiceHandoffStatus.Recorded);
        Assert.True(list.Succeeded, list.Code);
        Assert.Single(list.Value!);
    }

    [Fact]
    public async Task Concurrent_invoice_handoff_requests_for_seven_units_against_remainder_of_ten_prevent_atomic_over_handoff()
    {
        await using var fixture = await Fixture.CreateAsync();
        var receipt = await fixture.RecordedReceiptWithQuantityAsync(10m, "pih-race-10-7-7");
        var receiptLine = receipt.Lines.Single();

        var task1 = fixture.InvoiceHandoffService.CreateAsync(
            fixture.Context(Requester, "tenant.procurement.invoice-handoff.create"),
            new PurchaseInvoiceHandoffCreateRequest(
                receipt.PurchaseOrderId,
                "INV-RACE-7A",
                DateOnly.FromDateTime(DateTime.UtcNow.Date),
                null,
                [new PurchaseInvoiceHandoffSourceRequest(receipt.Id, receiptLine.Id, 7m)]),
            "pih-race-7a-key",
            "fp-pih-race-7a");

        var task2 = fixture.InvoiceHandoffService.CreateAsync(
            fixture.Context(Requester, "tenant.procurement.invoice-handoff.create"),
            new PurchaseInvoiceHandoffCreateRequest(
                receipt.PurchaseOrderId,
                "INV-RACE-7B",
                DateOnly.FromDateTime(DateTime.UtcNow.Date),
                null,
                [new PurchaseInvoiceHandoffSourceRequest(receipt.Id, receiptLine.Id, 7m)]),
            "pih-race-7b-key",
            "fp-pih-race-7b");

        var results = await Task.WhenAll(task1, task2);
        var successCount = results.Count(r => r.Succeeded);
        var failureCount = results.Count(r => !r.Succeeded);

        Assert.Equal(1, successCount);
        Assert.Equal(1, failureCount);
        Assert.Contains(results.First(r => !r.Succeeded).Code, new[] { "over_handoff_not_allowed", "concurrency_conflict" });

        var list = await fixture.InvoiceHandoffService.ListAsync(
            fixture.Context(Requester, "tenant.procurement.invoice-handoff.view"),
            PurchaseInvoiceHandoffStatus.Recorded);
        Assert.True(list.Succeeded, list.Code);
        var totalHandedOff = list.Value!.Sum(item => item.LineCount);
        Assert.Equal(1, totalHandedOff);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection;
        private readonly DbContextOptions options;

        private Fixture(
            SqliteConnection connection,
            DbContextOptions options,
            PurchaseOrderService purchaseOrderService,
            GoodsReceiptService goodsReceiptService,
            PurchaseInvoiceHandoffService invoiceHandoffService)
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
            var warehouseProvider = new ConfiguredProcurementWarehouseProvider(
            [
                new ProcurementWarehouseOption(TenantA, CompanyA, BranchA, WarehouseA, "WH-A", "Warehouse A", IsActive: true)
            ]);
            var goodsReceiptService = new GoodsReceiptService(authorization, new GoodsReceiptPersistence(options), warehouseProvider);
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

        public async Task<GoodsReceiptRecord> RecordedReceiptWithQuantityAsync(decimal quantity, string keyPrefix)
        {
            var now = DateTimeOffset.UtcNow;
            var scope = new PurchaseRequestScope(TenantA, CompanyA, BranchA);
            var reqId = Guid.NewGuid();
            var lineId = Guid.NewGuid();
            var quoteId = Guid.NewGuid();
            var quoteLineId = Guid.NewGuid();
            var decisionId = Guid.NewGuid();
            var policy = new PurchaseRequestApprovalPolicyDefinition("procurement.invoice-handoff.dynamic", 1, [new PurchaseRequestApprovalStageDefinition("manager", 1, 1, [], false)], true, now.AddDays(-1));

            await using (var db = new ProcurementDbContext(options, CreateTenantContext(TenantA, Requester)))
            {
                var request = new PurchaseRequestEntity(reqId, new TenantId(TenantA), CompanyA, BranchA, Requester, $"Dynamic demand {keyPrefix}", now);
                request.Lines.Add(new PurchaseRequestLineEntity(lineId, new TenantId(TenantA), request.Id, new PurchaseRequestLineSnapshot(lineId, Product, "SKU-001", "Test Product", Unit, "EA", quantity, DateOnly.FromDateTime(now.UtcDateTime.Date.AddDays(7)), "Dynamic line")));
                request.Submit(policy, JsonSerializer.Serialize(policy), now);
                request.RecordApproval(PurchaseRequestStatus.Approved, 0, 0, "[]", now);
                request.TouchVersion();

                var quoteLine = new SupplierQuotationLineSnapshot(quoteLineId, lineId, Product, "SKU-001", "Test Product", Unit, "EA", quantity, quantity, 12.5m, null, null, null, null, null, null, null, null, DateOnly.FromDateTime(now.UtcDateTime.Date.AddDays(7)), DateOnly.FromDateTime(now.UtcDateTime.Date.AddDays(9)), "9 days", "Dynamic quote line");
                var supplier = new SupplierQuotationSupplierSnapshot(Supplier, "SUP-A", "Alpha Supplier");
                var currency = new SupplierQuotationCurrencySnapshot(Currency, "USD", "US Dollar");
                var quoteCommand = new SupplierQuotationCreateCommand(quoteId, request.Id, scope, Requester, supplier, $"Q-{keyPrefix}", DateOnly.FromDateTime(now.UtcDateTime.Date), DateOnly.FromDateTime(now.UtcDateTime.Date.AddDays(14)), currency, null, "Delivered", DateOnly.FromDateTime(now.UtcDateTime.Date.AddDays(9)), "9 days", "Dynamic quote", [quoteLine], [], now, "seed");
                var quotation = new SupplierQuotationEntity(quoteCommand, new TenantId(TenantA));
                quotation.Lines.Add(new SupplierQuotationLineEntity(new TenantId(TenantA), quoteId, request.Id, quoteLine));
                quotation.SetStatus(SupplierQuotationStatus.Submitted, now);
                quotation.TouchVersion();
                var quotationRecord = new SupplierQuotationRecord(quoteId, TenantA, request.Id, scope, Requester, supplier, SupplierQuotationStatus.Submitted, $"Q-{keyPrefix}", quoteCommand.OfferDate, quoteCommand.ValidUntil, currency, null, quoteCommand.DeliveryTerms, quoteCommand.OfferedDeliveryDate, quoteCommand.OfferedDeliveryLeadTime, quoteCommand.Notes, [quoteLine], [], now, now, now, quotation.Version);
                var decisionCommand = new SupplierSourceDecisionCommand(decisionId, request.Id, scope, quoteId, Requester, now, "Selected dynamic", null, null, null, "sha256:dynamic", "{}", request.Version, $"dyn-decision-{keyPrefix}");
                var decision = new SupplierSourceDecisionEntity(decisionCommand, new TenantId(TenantA), quotationRecord);
                decision.TouchVersion();
                db.PurchaseRequests.Add(request);
                db.SupplierQuotations.Add(quotation);
                db.SupplierSourceDecisions.Add(decision);
                await db.SaveChangesAsync();
            }

            var created = await PurchaseOrderService.CreateAsync(Context(Requester, "tenant.procurement.purchase-order.create"), new PurchaseOrderCreateRequest(decisionId), $"{keyPrefix}-dyn-po-create", $"fp-{keyPrefix}-dyn-po-create");
            Assert.True(created.Succeeded, created.Code);
            var submitted = await PurchaseOrderService.SubmitAsync(Context(Requester, "tenant.procurement.purchase-order.submit"), created.Value!.Id, created.Value.Version, $"{keyPrefix}-dyn-po-submit", $"fp-{keyPrefix}-dyn-po-submit");
            Assert.True(submitted.Succeeded, submitted.Code);
            var approved = await PurchaseOrderService.ApproveAsync(Context(Approver, "tenant.procurement.purchase-order.approve"), submitted.Value!.Id, submitted.Value.Version, $"{keyPrefix}-dyn-po-approve", $"fp-{keyPrefix}-dyn-po-approve");
            Assert.True(approved.Succeeded, approved.Code);
            var issued = await PurchaseOrderService.IssueAsync(Context(Approver, "tenant.procurement.purchase-order.issue"), approved.Value!.Id, approved.Value.Version, $"{keyPrefix}-dyn-po-issue", $"fp-{keyPrefix}-dyn-po-issue");
            Assert.True(issued.Succeeded, issued.Code);

            var poLine = issued.Value!.Lines.Single();
            var confirmed = await PurchaseOrderService.RecordConfirmationAsync(
                Context(Requester, "tenant.procurement.purchase-order.confirmation.capture"),
                issued.Value.Id,
                new PurchaseOrderConfirmationRequest(
                    PurchaseOrderConfirmationStatus.Confirmed,
                    DateOnly.FromDateTime(DateTime.UtcNow.Date),
                    $"SUP-{keyPrefix}",
                    "supplier@test",
                    null,
                    null,
                    [new PurchaseOrderConfirmationLineRequest(poLine.Id, quantity, DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(7)), null, null, null, null)],
                    []),
                issued.Value.Version,
                $"{keyPrefix}-dyn-confirm",
                $"fp-{keyPrefix}-dyn-confirm");
            Assert.True(confirmed.Succeeded, confirmed.Code);

            var receipt = await GoodsReceiptService.CreateAsync(
                Context(Requester, "tenant.procurement.goods-receipt.create"),
                new GoodsReceiptCreateRequest(
                    confirmed.Value!.Id,
                    WarehouseA,
                    DateOnly.FromDateTime(DateTime.UtcNow.Date),
                    $"GRN-{keyPrefix}",
                    null,
                    [new GoodsReceiptLineCreateRequest(poLine.Id, quantity, quantity, 0m, null, null, null)]),
                $"{keyPrefix}-dyn-gr-create",
                $"fp-{keyPrefix}-dyn-gr-create");
            Assert.True(receipt.Succeeded, receipt.Code);
            return receipt.Value!;
        }

        public async Task<GoodsReceiptRecord> RecordedReceiptAsync(string keyPrefix)
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

            var receipt = await GoodsReceiptService.CreateAsync(
                Context(Requester, "tenant.procurement.goods-receipt.create"),
                new GoodsReceiptCreateRequest(
                    confirmed.Value!.Id,
                    WarehouseA,
                    DateOnly.FromDateTime(DateTime.UtcNow.Date),
                    $"GRN-{keyPrefix}",
                    null,
                    [new GoodsReceiptLineCreateRequest(line.Id, 2m, 2m, 0m, null, null, null)]),
                $"{keyPrefix}-gr-create",
                $"fp-{keyPrefix}-gr-create");
            Assert.True(receipt.Succeeded, receipt.Code);
            return receipt.Value!;
        }

        private async Task<PurchaseOrderRecord> IssuedOrderAsync(string keyPrefix)
        {
            var source = Assert.Single((await PurchaseOrderService.ListSourceOptionsAsync(Context(Requester, "tenant.procurement.purchase-order.view"))).Value!);
            var created = await PurchaseOrderService.CreateAsync(Context(Requester, "tenant.procurement.purchase-order.create"), new PurchaseOrderCreateRequest(source.Source.SourceDecisionId), $"{keyPrefix}-po-create", $"fp-{keyPrefix}-po-create");
            Assert.True(created.Succeeded, created.Code);
            var submitted = await PurchaseOrderService.SubmitAsync(Context(Requester, "tenant.procurement.purchase-order.submit"), created.Value!.Id, created.Value.Version, $"{keyPrefix}-po-submit", $"fp-{keyPrefix}-po-submit");
            Assert.True(submitted.Succeeded, submitted.Code);
            var approved = await PurchaseOrderService.ApproveAsync(Context(Approver, "tenant.procurement.purchase-order.approve"), submitted.Value!.Id, submitted.Value.Version, $"{keyPrefix}-po-approve", $"fp-{keyPrefix}-po-approve");
            Assert.True(approved.Succeeded, approved.Code);
            var issued = await PurchaseOrderService.IssueAsync(Context(Approver, "tenant.procurement.purchase-order.issue"), approved.Value!.Id, approved.Value.Version, $"{keyPrefix}-po-issue", $"fp-{keyPrefix}-po-issue");
            Assert.True(issued.Succeeded, issued.Code);
            return issued.Value!;
        }

        private static async Task SeedAsync(ProcurementDbContext db)
        {
            var now = DateTimeOffset.UtcNow;
            var scope = new PurchaseRequestScope(TenantA, CompanyA, BranchA);
            var lineId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");
            var quotationId = Guid.Parse("aaaaaaaa-aaaa-1111-1111-111111111111");
            var decisionId = Guid.Parse("aaaaaaaa-aaaa-2222-2222-222222222222");
            var policy = new PurchaseRequestApprovalPolicyDefinition("procurement.invoice-handoff.test", 1, [new PurchaseRequestApprovalStageDefinition("manager", 1, 1, [], false)], true, now.AddDays(-1));
            var request = new PurchaseRequestEntity(Guid.Parse("aaaaaaaa-aaaa-3333-3333-333333333333"), new TenantId(TenantA), CompanyA, BranchA, Requester, "Approved demand", now);
            request.Lines.Add(new PurchaseRequestLineEntity(lineId, new TenantId(TenantA), request.Id, new PurchaseRequestLineSnapshot(lineId, Product, "SKU-001", "Test Product", Unit, "EA", 2m, DateOnly.FromDateTime(now.UtcDateTime.Date.AddDays(7)), "Approved demand line")));
            request.Submit(policy, JsonSerializer.Serialize(policy), now);
            request.RecordApproval(PurchaseRequestStatus.Approved, 0, 0, "[]", now);
            request.TouchVersion();

            // Unit price 12.50, 15% tax rate percentage
            var quotationLine = new SupplierQuotationLineSnapshot(
                Guid.Parse("aaaaaaaa-aaaa-4444-4444-444444444444"),
                lineId,
                Product,
                "SKU-001",
                "Test Product",
                Unit,
                "EA",
                2m,
                2m,
                12.5m,
                null,
                null,
                Guid.Parse("aaaaaaaa-5555-5555-5555-555555555555"),
                "VAT15",
                "VAT 15%",
                15m,
                3.75m,
                null,
                DateOnly.FromDateTime(now.UtcDateTime.Date.AddDays(7)),
                DateOnly.FromDateTime(now.UtcDateTime.Date.AddDays(9)),
                "9 days",
                "Quoted line");
            var supplier = new SupplierQuotationSupplierSnapshot(Supplier, "SUP-A", "Alpha Supplier");
            var currency = new SupplierQuotationCurrencySnapshot(Currency, "USD", "US Dollar");
            var quotationCommand = new SupplierQuotationCreateCommand(quotationId, request.Id, scope, Requester, supplier, "Q-PIH-1", DateOnly.FromDateTime(now.UtcDateTime.Date), DateOnly.FromDateTime(now.UtcDateTime.Date.AddDays(14)), currency, null, "Delivered", DateOnly.FromDateTime(now.UtcDateTime.Date.AddDays(9)), "9 days", "Seed quote", [quotationLine], [], now, "seed");
            var quotation = new SupplierQuotationEntity(quotationCommand, new TenantId(TenantA));
            quotation.Lines.Add(new SupplierQuotationLineEntity(new TenantId(TenantA), quotationId, request.Id, quotationLine));
            quotation.SetStatus(SupplierQuotationStatus.Submitted, now);
            quotation.TouchVersion();
            var quotationRecord = new SupplierQuotationRecord(quotationId, TenantA, request.Id, scope, Requester, supplier, SupplierQuotationStatus.Submitted, "Q-PIH-1", quotationCommand.OfferDate, quotationCommand.ValidUntil, currency, null, quotationCommand.DeliveryTerms, quotationCommand.OfferedDeliveryDate, quotationCommand.OfferedDeliveryLeadTime, quotationCommand.Notes, [quotationLine], [], now, now, now, quotation.Version);
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
