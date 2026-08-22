#pragma warning disable CS1591

using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MiniErp.App.BuildingBlocks.Rest;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.Modules.Procurement;
using MiniErp.Contracts.Modules.Procurement;
using MiniErp.Infrastructure.Persistence.Modules.Procurement;
using Xunit;

namespace MiniErp.ArchitectureTests;

/// <summary>
/// MESP-127 P1 regression coverage: a Supplier Return must keep consuming its accepted Goods Receipt
/// quantity through every status where the commercial return has not been undone, including
/// <see cref="SupplierReturnStatus.Completed"/>. These are real persistence/service integration tests
/// (Sqlite-backed <see cref="ProcurementDbContext"/>), not enum/array inspection, following the pattern
/// established in <c>GoodsReceiptTests</c>.
/// </summary>
public sealed class SupplierReturnPersistenceTests
{
    private static readonly Guid TenantA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
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
    public async Task Completed_return_via_finance_reference_still_consumes_quantity()
    {
        await using var fixture = await Fixture.CreateAsync();
        var receipt = await fixture.RecordedReceiptAsync(10m, "sr-fin");
        var line = receipt.Lines.Single();

        var created = await fixture.CreateReturnAsync(receipt.Id, line.Id, 4m, SupplierReturnCommercialOutcome.CreditExpected, "sr-fin-create");
        var submitted = await fixture.SubmitAsync(created, "sr-fin-submit");
        var approved = await fixture.ApproveAsync(submitted, "sr-fin-approve");
        var handoff = await fixture.RecordInventoryHandoffAsync(approved, "sr-fin-handoff");
        Assert.Equal(SupplierReturnStatus.AwaitingFinance, handoff.Status);
        var completed = await fixture.RecordFinanceReferenceAsync(handoff, "sr-fin-finance");
        Assert.Equal(SupplierReturnStatus.Completed, completed.Status);

        var eligible = await fixture.EligibleLineAsync(receipt.Id);
        Assert.Equal(4m, eligible.AlreadyReturnedQuantity);
        Assert.Equal(6m, eligible.EligibleReturnQuantity);

        var overReturn = await fixture.SupplierReturnService.CreateAsync(
            fixture.Context(Requester, "tenant.procurement.supplier-return.create"),
            new SupplierReturnCreateRequest(receipt.Id, DateOnly.FromDateTime(DateTime.UtcNow.Date), SupplierReturnReasonCode.Damaged, SupplierReturnCondition.Unusable, SupplierReturnCommercialOutcome.CreditExpected, null, null, [new SupplierReturnLineCreateRequest(line.Id, 7m, null)], []),
            "sr-fin-over-create",
            "fp-sr-fin-over-create");
        Assert.False(overReturn.Succeeded);
        Assert.Equal("over_return_not_allowed", overReturn.Code);
    }

    [Fact]
    public async Task Completed_no_credit_expected_return_still_consumes_quantity()
    {
        await using var fixture = await Fixture.CreateAsync();
        var receipt = await fixture.RecordedReceiptAsync(10m, "sr-nocredit");
        var line = receipt.Lines.Single();

        var created = await fixture.CreateReturnAsync(receipt.Id, line.Id, 4m, SupplierReturnCommercialOutcome.NoCreditExpected, "sr-nocredit-create");
        var submitted = await fixture.SubmitAsync(created, "sr-nocredit-submit");
        var approved = await fixture.ApproveAsync(submitted, "sr-nocredit-approve");
        var handoff = await fixture.RecordInventoryHandoffAsync(approved, "sr-nocredit-handoff");

        // NoCreditExpected completes on inventory handoff alone; it must not restore the returned units.
        Assert.Equal(SupplierReturnStatus.Completed, handoff.Status);

        var eligible = await fixture.EligibleLineAsync(receipt.Id);
        Assert.Equal(4m, eligible.AlreadyReturnedQuantity);
        Assert.Equal(6m, eligible.EligibleReturnQuantity);
    }

    [Fact]
    public async Task Cancelled_return_restores_eligible_quantity()
    {
        await using var fixture = await Fixture.CreateAsync();
        var receipt = await fixture.RecordedReceiptAsync(10m, "sr-cancel");
        var line = receipt.Lines.Single();

        var created = await fixture.CreateReturnAsync(receipt.Id, line.Id, 4m, SupplierReturnCommercialOutcome.CreditExpected, "sr-cancel-create");
        var midway = await fixture.EligibleLineAsync(receipt.Id);
        Assert.Equal(4m, midway.AlreadyReturnedQuantity);
        Assert.Equal(6m, midway.EligibleReturnQuantity);

        var cancelled = await fixture.CancelAsync(created, "sr-cancel-cancel");
        Assert.Equal(SupplierReturnStatus.Cancelled, cancelled.Status);

        var restored = await fixture.EligibleLineAsync(receipt.Id);
        Assert.Equal(0m, restored.AlreadyReturnedQuantity);
        Assert.Equal(10m, restored.EligibleReturnQuantity);
    }

    [Fact]
    public async Task Reversed_return_restores_eligible_quantity()
    {
        await using var fixture = await Fixture.CreateAsync();
        var receipt = await fixture.RecordedReceiptAsync(10m, "sr-reverse");
        var line = receipt.Lines.Single();

        var created = await fixture.CreateReturnAsync(receipt.Id, line.Id, 4m, SupplierReturnCommercialOutcome.CreditExpected, "sr-reverse-create");
        var submitted = await fixture.SubmitAsync(created, "sr-reverse-submit");
        var approved = await fixture.ApproveAsync(submitted, "sr-reverse-approve");

        var reversed = await fixture.ReverseAsync(approved, "sr-reverse-reverse");
        Assert.Equal(SupplierReturnStatus.Reversed, reversed.Status);

        var restored = await fixture.EligibleLineAsync(receipt.Id);
        Assert.Equal(0m, restored.AlreadyReturnedQuantity);
        Assert.Equal(10m, restored.EligibleReturnQuantity);
    }

    [Fact]
    public async Task Correction_successor_replaces_original_consumption_without_double_counting()
    {
        await using var fixture = await Fixture.CreateAsync();
        var receipt = await fixture.RecordedReceiptAsync(10m, "sr-correct");
        var line = receipt.Lines.Single();

        var original = await fixture.CreateReturnAsync(receipt.Id, line.Id, 4m, SupplierReturnCommercialOutcome.CreditExpected, "sr-correct-original");
        var afterOriginal = await fixture.EligibleLineAsync(receipt.Id);
        Assert.Equal(4m, afterOriginal.AlreadyReturnedQuantity);
        Assert.Equal(6m, afterOriginal.EligibleReturnQuantity);

        var correctionRequest = new SupplierReturnCreateRequest(
            receipt.Id,
            DateOnly.FromDateTime(DateTime.UtcNow.Date),
            SupplierReturnReasonCode.Damaged,
            SupplierReturnCondition.Unusable,
            SupplierReturnCommercialOutcome.CreditExpected,
            null,
            null,
            [new SupplierReturnLineCreateRequest(line.Id, 3m, null)],
            []);
        var corrected = await fixture.SupplierReturnService.CorrectAsync(
            fixture.Context(Requester, "tenant.procurement.supplier-return.correct"),
            original.Id,
            original.Version,
            correctionRequest,
            "sr-correct-correct",
            "fp-sr-correct-correct");
        Assert.True(corrected.Succeeded, corrected.Code);
        var successor = corrected.Value!;
        Assert.Equal(original.Id, successor.CorrectionOfId);
        Assert.Equal(3m, successor.Lines.Single().ReturnQuantity);

        var originalAfterCorrection = await fixture.SupplierReturnService.GetAsync(fixture.Context(Requester, "tenant.procurement.supplier-return.view"), original.Id);
        Assert.True(originalAfterCorrection.Succeeded, originalAfterCorrection.Code);
        Assert.Equal(SupplierReturnStatus.CorrectionLinked, originalAfterCorrection.Value!.Status);

        // Neither 6 (stale original-consumption view), nor 3 (successor only, ignoring the fresh
        // remainder), nor 10 (as if nothing was ever returned) — exactly 7: the original's 4 units are
        // released and the successor's 3 units are the only quantity still consumed.
        var afterCorrection = await fixture.EligibleLineAsync(receipt.Id);
        Assert.Equal(3m, afterCorrection.AlreadyReturnedQuantity);
        Assert.Equal(7m, afterCorrection.EligibleReturnQuantity);
    }

    [Fact]
    public async Task Concurrent_overlapping_returns_cannot_both_consume_the_same_remaining_quantity()
    {
        await using var fixture = await Fixture.CreateAsync();
        var receipt = await fixture.RecordedReceiptAsync(10m, "sr-race");
        var line = receipt.Lines.Single();

        var task1 = fixture.SupplierReturnService.CreateAsync(
            fixture.Context(Requester, "tenant.procurement.supplier-return.create"),
            new SupplierReturnCreateRequest(receipt.Id, DateOnly.FromDateTime(DateTime.UtcNow.Date), SupplierReturnReasonCode.Damaged, SupplierReturnCondition.Unusable, SupplierReturnCommercialOutcome.CreditExpected, null, null, [new SupplierReturnLineCreateRequest(line.Id, 6m, null)], []),
            "sr-race-1",
            "fp-sr-race-1");
        var task2 = fixture.SupplierReturnService.CreateAsync(
            fixture.Context(Requester, "tenant.procurement.supplier-return.create"),
            new SupplierReturnCreateRequest(receipt.Id, DateOnly.FromDateTime(DateTime.UtcNow.Date), SupplierReturnReasonCode.Damaged, SupplierReturnCondition.Unusable, SupplierReturnCommercialOutcome.CreditExpected, null, null, [new SupplierReturnLineCreateRequest(line.Id, 6m, null)], []),
            "sr-race-2",
            "fp-sr-race-2");

        var results = await Task.WhenAll(task1, task2);
        var successCount = results.Count(r => r.Succeeded);
        var failureCount = results.Count(r => !r.Succeeded);

        Assert.Equal(1, successCount);
        Assert.Equal(1, failureCount);
        Assert.Contains(results.First(r => !r.Succeeded).Code, new[] { "over_return_not_allowed", "concurrency_conflict" });

        var eligible = await fixture.EligibleLineAsync(receipt.Id);
        Assert.Equal(6m, eligible.AlreadyReturnedQuantity);
        Assert.Equal(4m, eligible.EligibleReturnQuantity);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection;
        private readonly DbContextOptions options;

        private Fixture(SqliteConnection connection, DbContextOptions options, PurchaseOrderService purchaseOrderService, GoodsReceiptService goodsReceiptService, SupplierReturnService supplierReturnService)
        {
            this.connection = connection;
            this.options = options;
            PurchaseOrderService = purchaseOrderService;
            GoodsReceiptService = goodsReceiptService;
            SupplierReturnService = supplierReturnService;
        }

        public PurchaseOrderService PurchaseOrderService { get; }
        public GoodsReceiptService GoodsReceiptService { get; }
        public SupplierReturnService SupplierReturnService { get; }

        public static async Task<Fixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder().UseSqlite(connection).Options;
            await using (var db = new ProcurementDbContext(options, CreateTenantContext(TenantA, Requester)))
            {
                await db.Database.EnsureCreatedAsync();
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
            var goodsReceiptService = new GoodsReceiptService(authorization, new GoodsReceiptPersistence(options), warehouseProvider, new NoActiveGoodsReceiptInventoryEffectReader());
            var supplierReturnService = new SupplierReturnService(authorization, new SupplierReturnPersistence(options));
            return new Fixture(connection, options, purchaseOrderService, goodsReceiptService, supplierReturnService);
        }

        public ProcurementRequestContext Context(Guid actor, string permission)
        {
            var foundation = FoundationRequestContext.ForTenant(actor, Guid.NewGuid(), CreateTenantContext(TenantA, actor), permission);
            var resolved = new ProcurementTenantContextResolver().Resolve(foundation);
            return Assert.IsType<ProcurementRequestContext>(resolved.Context);
        }

        /// <summary>
        /// Drives a dynamic Purchase Request through quotation, source decision, Purchase Order issue and
        /// confirmation, then records a full Goods Receipt (Received = Accepted = quantity, Rejected = 0)
        /// so it is a genuinely eligible Supplier Return source.
        /// </summary>
        public async Task<GoodsReceiptRecord> RecordedReceiptAsync(decimal quantity, string keyPrefix)
        {
            var confirmed = await ConfirmedOrderWithQuantityAsync(quantity, keyPrefix);
            var line = confirmed.Lines.Single();
            var recorded = await GoodsReceiptService.CreateAsync(
                Context(Requester, "tenant.procurement.goods-receipt.create"),
                new GoodsReceiptCreateRequest(confirmed.Id, WarehouseA, DateOnly.FromDateTime(DateTime.UtcNow.Date), $"GRN-{keyPrefix}", null, [new GoodsReceiptLineCreateRequest(line.Id, quantity, quantity, 0m, null, null, null)]),
                $"{keyPrefix}-gr-create",
                $"fp-{keyPrefix}-gr-create");
            Assert.True(recorded.Succeeded, recorded.Code);
            return recorded.Value!;
        }

        public async Task<SupplierReturnRecord> CreateReturnAsync(Guid goodsReceiptId, Guid goodsReceiptLineId, decimal quantity, SupplierReturnCommercialOutcome outcome, string keyPrefix)
        {
            var request = new SupplierReturnCreateRequest(
                goodsReceiptId,
                DateOnly.FromDateTime(DateTime.UtcNow.Date),
                SupplierReturnReasonCode.Damaged,
                SupplierReturnCondition.Unusable,
                outcome,
                "Reason detail",
                "Return notes",
                [new SupplierReturnLineCreateRequest(goodsReceiptLineId, quantity, null)],
                []);
            var created = await SupplierReturnService.CreateAsync(
                Context(Requester, "tenant.procurement.supplier-return.create"),
                request,
                $"{keyPrefix}-create",
                $"fp-{keyPrefix}-create");
            Assert.True(created.Succeeded, created.Code);
            return created.Value!;
        }

        public async Task<SupplierReturnRecord> SubmitAsync(SupplierReturnRecord current, string keyPrefix)
        {
            var result = await SupplierReturnService.SubmitAsync(Context(Requester, "tenant.procurement.supplier-return.submit"), current.Id, current.Version, null, $"{keyPrefix}", $"fp-{keyPrefix}");
            Assert.True(result.Succeeded, result.Code);
            return result.Value!;
        }

        public async Task<SupplierReturnRecord> ApproveAsync(SupplierReturnRecord current, string keyPrefix)
        {
            var result = await SupplierReturnService.ApproveAsync(Context(Requester, "tenant.procurement.supplier-return.approve"), current.Id, current.Version, null, $"{keyPrefix}", $"fp-{keyPrefix}");
            Assert.True(result.Succeeded, result.Code);
            return result.Value!;
        }

        public async Task<SupplierReturnRecord> RecordInventoryHandoffAsync(SupplierReturnRecord current, string keyPrefix)
        {
            var request = new SupplierReturnInventoryHandoffRequest($"WH-REF-{keyPrefix}", null);
            var result = await SupplierReturnService.RecordInventoryHandoffAsync(Context(Requester, "tenant.procurement.supplier-return.inventory-handoff"), current.Id, current.Version, request, $"{keyPrefix}", $"fp-{keyPrefix}");
            Assert.True(result.Succeeded, result.Code);
            return result.Value!;
        }

        public async Task<SupplierReturnRecord> RecordFinanceReferenceAsync(SupplierReturnRecord current, string keyPrefix)
        {
            var request = new SupplierReturnFinanceReferenceRequest($"FIN-REF-{keyPrefix}", "USD", 100m, null);
            var result = await SupplierReturnService.RecordFinanceReferenceAsync(Context(Requester, "tenant.procurement.supplier-return.finance-reference"), current.Id, current.Version, request, $"{keyPrefix}", $"fp-{keyPrefix}");
            Assert.True(result.Succeeded, result.Code);
            return result.Value!;
        }

        public async Task<SupplierReturnRecord> CancelAsync(SupplierReturnRecord current, string keyPrefix)
        {
            var result = await SupplierReturnService.CancelAsync(Context(Requester, "tenant.procurement.supplier-return.cancel"), current.Id, current.Version, "No longer needed", $"{keyPrefix}", $"fp-{keyPrefix}");
            Assert.True(result.Succeeded, result.Code);
            return result.Value!;
        }

        public async Task<SupplierReturnRecord> ReverseAsync(SupplierReturnRecord current, string keyPrefix)
        {
            var result = await SupplierReturnService.ReverseAsync(Context(Requester, "tenant.procurement.supplier-return.reverse"), current.Id, current.Version, "Reversing before downstream consequence", $"{keyPrefix}", $"fp-{keyPrefix}");
            Assert.True(result.Succeeded, result.Code);
            return result.Value!;
        }

        public async Task<SupplierReturnEligibleLineRecord> EligibleLineAsync(Guid goodsReceiptId)
        {
            var sources = await SupplierReturnService.ListEligibleSourcesAsync(Context(Requester, "tenant.procurement.supplier-return.view"));
            Assert.True(sources.Succeeded, sources.Code);
            var source = Assert.Single(sources.Value!, item => item.GoodsReceiptId == goodsReceiptId);
            return source.Lines.Single();
        }

        /// <summary>
        /// Creates a dynamic Purchase Order confirmed with the exact specified line quantity, mirroring
        /// GoodsReceiptTests.Fixture.ConfirmedOrderWithQuantityAsync.
        /// </summary>
        public async Task<PurchaseOrderRecord> ConfirmedOrderWithQuantityAsync(decimal quantity, string keyPrefix)
        {
            var now = DateTimeOffset.UtcNow;
            var scope = new PurchaseRequestScope(TenantA, CompanyA, BranchA);
            var reqId = Guid.NewGuid();
            var lineId = Guid.NewGuid();
            var quoteId = Guid.NewGuid();
            var quoteLineId = Guid.NewGuid();
            var decisionId = Guid.NewGuid();
            var policy = new PurchaseRequestApprovalPolicyDefinition("procurement.supplier-return.dynamic", 1, [new PurchaseRequestApprovalStageDefinition("manager", 1, 1, [], false)], true, now.AddDays(-1));

            await using (var db = new ProcurementDbContext(options, CreateTenantContext(TenantA, Requester)))
            {
                var request = new PurchaseRequestEntity(reqId, new TenantId(TenantA), CompanyA, BranchA, Requester, $"Dynamic demand {keyPrefix}", now);
                request.Lines.Add(new PurchaseRequestLineEntity(lineId, new TenantId(TenantA), request.Id, new PurchaseRequestLineSnapshot(lineId, Product, "SKU-001", "Test Product", Unit, "EA", quantity, DateOnly.FromDateTime(now.UtcDateTime.Date.AddDays(7)), "Dynamic line")));
                request.Submit(policy, JsonSerializer.Serialize(policy), now);
                request.RecordApproval(PurchaseRequestStatus.Approved, 0, 0, "[]", now);
                request.TouchVersion();

                var quoteLine = new SupplierQuotationLineSnapshot(quoteLineId, lineId, Product, "SKU-001", "Test Product", Unit, "EA", quantity, quantity, 10m, null, null, null, null, null, null, null, null, DateOnly.FromDateTime(now.UtcDateTime.Date.AddDays(7)), DateOnly.FromDateTime(now.UtcDateTime.Date.AddDays(9)), "9 days", "Dynamic quote line");
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
            return confirmed.Value!;
        }

        public async ValueTask DisposeAsync() => await connection.DisposeAsync();
    }

    private static TenantContext CreateTenantContext(Guid tenantId, Guid actor) => TenantContext.ForOrdinaryMembership(new TenantId(tenantId), new MembershipReference(Guid.NewGuid()), new ScopeReference($"Company:{CompanyA:D}"), new CorrelationId($"corr-{Guid.NewGuid():N}"), actor);
}
