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

        var currentReceipt = await fixture.GoodsReceiptService.GetAsync(fixture.Context(Requester, "tenant.procurement.goods-receipt.view"), created.Value.Id);
        Assert.True(currentReceipt.Succeeded, currentReceipt.Code);

        var blockedCancel = await fixture.GoodsReceiptService.CancelAsync(
            fixture.Context(Requester, "tenant.procurement.goods-receipt.cancel"),
            created.Value.Id,
            currentReceipt.Value!.Version,
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
            untouchedReceipt.Value!.Version,
            "No longer referenced by an active handoff",
            "gr-handoff-block-cancel-released",
            "fp-gr-handoff-block-cancel-released");
        Assert.True(releasedCancel.Succeeded, releasedCancel.Code);
        Assert.Equal(GoodsReceiptStatus.Cancelled, releasedCancel.Value!.Status);
    }

    [Fact]
    public async Task Denies_receipt_with_unauthorized_inactive_or_mismatched_warehouse()
    {
        await using var fixture = await Fixture.CreateAsync();
        var confirmed = await fixture.ConfirmedOrderAsync("gr-wh-auth");
        var line = confirmed.Lines.Single();

        // 1. Unknown / foreign tenant warehouse is denied (no existence leakage)
        var unknownWarehouse = Guid.NewGuid();
        var unknownResult = await fixture.GoodsReceiptService.CreateAsync(
            fixture.Context(Requester, "tenant.procurement.goods-receipt.create"),
            new GoodsReceiptCreateRequest(confirmed.Id, unknownWarehouse, DateOnly.FromDateTime(DateTime.UtcNow.Date), null, null, [new GoodsReceiptLineCreateRequest(line.Id, 2m, 2m, 0m, null, null, null)]),
            "gr-wh-unknown",
            "fp-gr-wh-unknown");
        Assert.False(unknownResult.Succeeded);
        Assert.Equal("warehouse_not_authorized", unknownResult.Code);

        // 2. Inactive warehouse is denied
        var inactiveResult = await fixture.GoodsReceiptService.CreateAsync(
            fixture.Context(Requester, "tenant.procurement.goods-receipt.create"),
            new GoodsReceiptCreateRequest(confirmed.Id, Fixture.WarehouseInactive, DateOnly.FromDateTime(DateTime.UtcNow.Date), null, null, [new GoodsReceiptLineCreateRequest(line.Id, 2m, 2m, 0m, null, null, null)]),
            "gr-wh-inactive",
            "fp-gr-wh-inactive");
        Assert.False(inactiveResult.Succeeded);
        Assert.Equal("warehouse_inactive", inactiveResult.Code);

        // 3. Mismatched company / branch warehouse is denied
        var mismatchResult = await fixture.GoodsReceiptService.CreateAsync(
            fixture.Context(Requester, "tenant.procurement.goods-receipt.create"),
            new GoodsReceiptCreateRequest(confirmed.Id, Fixture.WarehouseForeignScope, DateOnly.FromDateTime(DateTime.UtcNow.Date), null, null, [new GoodsReceiptLineCreateRequest(line.Id, 2m, 2m, 0m, null, null, null)]),
            "gr-wh-mismatch",
            "fp-gr-wh-mismatch");
        Assert.False(mismatchResult.Succeeded);
        Assert.Equal("warehouse_scope_denied", mismatchResult.Code);

        // 4. Warehouse listing returns active warehouses for the Tenant (inactive excluded)
        var warehouseList = await fixture.GoodsReceiptService.ListWarehousesAsync(fixture.Context(Requester, "tenant.procurement.goods-receipt.view"));
        Assert.True(warehouseList.Succeeded, warehouseList.Code);
        Assert.Equal(2, warehouseList.Value!.Count);
        Assert.Contains(warehouseList.Value!, w => w.WarehouseId == WarehouseA && w.Code == "WH-A");
        Assert.DoesNotContain(warehouseList.Value!, w => w.WarehouseId == Fixture.WarehouseInactive);
    }

    [Fact]
    public async Task Concurrent_receipt_requests_exceeding_receivable_quantity_prevent_atomic_over_receipt()
    {
        await using var fixture = await Fixture.CreateAsync();
        var confirmed = await fixture.ConfirmedOrderAsync("gr-concur-race");
        var line = confirmed.Lines.Single();

        var task1 = fixture.GoodsReceiptService.CreateAsync(
            fixture.Context(Requester, "tenant.procurement.goods-receipt.create"),
            new GoodsReceiptCreateRequest(confirmed.Id, WarehouseA, DateOnly.FromDateTime(DateTime.UtcNow.Date), "GR-RACE-1", null, [new GoodsReceiptLineCreateRequest(line.Id, 2m, 2m, 0m, null, null, null)]),
            "gr-race-key-1",
            "fp-gr-race-1");

        var task2 = fixture.GoodsReceiptService.CreateAsync(
            fixture.Context(Requester, "tenant.procurement.goods-receipt.create"),
            new GoodsReceiptCreateRequest(confirmed.Id, WarehouseA, DateOnly.FromDateTime(DateTime.UtcNow.Date), "GR-RACE-2", null, [new GoodsReceiptLineCreateRequest(line.Id, 2m, 2m, 0m, null, null, null)]),
            "gr-race-key-2",
            "fp-gr-race-2");

        var results = await Task.WhenAll(task1, task2);
        var successCount = results.Count(r => r.Succeeded);
        var failureCount = results.Count(r => !r.Succeeded);

        Assert.Equal(1, successCount);
        Assert.Equal(1, failureCount);
        Assert.Contains(results.First(r => !r.Succeeded).Code, new[] { "over_receipt_not_allowed", "concurrency_conflict" });

        var list = await fixture.GoodsReceiptService.ListAsync(fixture.Context(Requester, "tenant.procurement.goods-receipt.view"), null, confirmed.Id);
        Assert.True(list.Succeeded, list.Code);
        var totalAccepted = list.Value!.Sum(item => item.TotalAcceptedQuantity);
        Assert.Equal(2m, totalAccepted);
    }

    [Fact]
    public async Task Proves_receipt_quantity_semantics_rules_A_through_I()
    {
        await using var fixture = await Fixture.CreateAsync();

        // Rule A: received 10, accepted 8, rejected 2 -> valid
        var orderA = await fixture.ConfirmedOrderWithQuantityAsync(10m, "gr-sem-a");
        var lineA = orderA.Lines.Single();
        var resultA = await fixture.GoodsReceiptService.CreateAsync(
            fixture.Context(Requester, "tenant.procurement.goods-receipt.create"),
            new GoodsReceiptCreateRequest(orderA.Id, WarehouseA, DateOnly.FromDateTime(DateTime.UtcNow.Date), "GR-A", null, [new GoodsReceiptLineCreateRequest(lineA.Id, 10m, 8m, 2m, null, null, null)]),
            "gr-sem-a-create",
            "fp-gr-sem-a-create");
        Assert.True(resultA.Succeeded, resultA.Code);
        Assert.Equal(10m, resultA.Value!.Lines.Single().ReceivedQuantity);
        Assert.Equal(8m, resultA.Value.Lines.Single().AcceptedQuantity);
        Assert.Equal(2m, resultA.Value.Lines.Single().RejectedQuantity);

        // Rule B: received 10, accepted 10, rejected 0 -> valid
        var orderB = await fixture.ConfirmedOrderWithQuantityAsync(10m, "gr-sem-b");
        var lineB = orderB.Lines.Single();
        var resultB = await fixture.GoodsReceiptService.CreateAsync(
            fixture.Context(Requester, "tenant.procurement.goods-receipt.create"),
            new GoodsReceiptCreateRequest(orderB.Id, WarehouseA, DateOnly.FromDateTime(DateTime.UtcNow.Date), "GR-B", null, [new GoodsReceiptLineCreateRequest(lineB.Id, 10m, 10m, 0m, null, null, null)]),
            "gr-sem-b-create",
            "fp-gr-sem-b-create");
        Assert.True(resultB.Succeeded, resultB.Code);
        Assert.Equal(10m, resultB.Value!.Lines.Single().ReceivedQuantity);
        Assert.Equal(10m, resultB.Value.Lines.Single().AcceptedQuantity);
        Assert.Equal(0m, resultB.Value.Lines.Single().RejectedQuantity);

        // Rule C: received 10, accepted 0, rejected 10 -> valid
        var orderC = await fixture.ConfirmedOrderWithQuantityAsync(10m, "gr-sem-c");
        var lineC = orderC.Lines.Single();
        var resultC = await fixture.GoodsReceiptService.CreateAsync(
            fixture.Context(Requester, "tenant.procurement.goods-receipt.create"),
            new GoodsReceiptCreateRequest(orderC.Id, WarehouseA, DateOnly.FromDateTime(DateTime.UtcNow.Date), "GR-C", null, [new GoodsReceiptLineCreateRequest(lineC.Id, 10m, 0m, 10m, null, null, null)]),
            "gr-sem-c-create",
            "fp-gr-sem-c-create");
        Assert.True(resultC.Succeeded, resultC.Code);
        Assert.Equal(10m, resultC.Value!.Lines.Single().ReceivedQuantity);
        Assert.Equal(0m, resultC.Value.Lines.Single().AcceptedQuantity);
        Assert.Equal(10m, resultC.Value.Lines.Single().RejectedQuantity);

        // Rule D: received 10, accepted 8, rejected 2 with damage condition evidence -> damage does not increase physical total
        var orderD = await fixture.ConfirmedOrderWithQuantityAsync(10m, "gr-sem-d");
        var lineD = orderD.Lines.Single();
        var resultD = await fixture.GoodsReceiptService.CreateAsync(
            fixture.Context(Requester, "tenant.procurement.goods-receipt.create"),
            new GoodsReceiptCreateRequest(orderD.Id, WarehouseA, DateOnly.FromDateTime(DateTime.UtcNow.Date), "GR-D", null, [new GoodsReceiptLineCreateRequest(lineD.Id, 10m, 8m, 2m, 2m, "2 rejected units were damaged", null)]),
            "gr-sem-d-create",
            "fp-gr-sem-d-create");
        Assert.True(resultD.Succeeded, resultD.Code);
        var recLineD = resultD.Value!.Lines.Single();
        Assert.Equal(10m, recLineD.ReceivedQuantity);
        Assert.Equal(8m, recLineD.AcceptedQuantity);
        Assert.Equal(2m, recLineD.RejectedQuantity);
        Assert.Equal(2m, recLineD.DamagedQuantity);
        Assert.Equal(10m, recLineD.AcceptedQuantity + recLineD.RejectedQuantity);

        // Rule E: accepted + rejected > received -> rejected
        var orderE = await fixture.ConfirmedOrderWithQuantityAsync(10m, "gr-sem-e");
        var lineE = orderE.Lines.Single();
        var resultE = await fixture.GoodsReceiptService.CreateAsync(
            fixture.Context(Requester, "tenant.procurement.goods-receipt.create"),
            new GoodsReceiptCreateRequest(orderE.Id, WarehouseA, DateOnly.FromDateTime(DateTime.UtcNow.Date), "GR-E", null, [new GoodsReceiptLineCreateRequest(lineE.Id, 10m, 8m, 3m, null, null, null)]),
            "gr-sem-e-create",
            "fp-gr-sem-e-create");
        Assert.False(resultE.Succeeded);
        Assert.Equal("validation_failed", resultE.Code);

        // Rule F: accepted + rejected < received -> rejected
        var orderF = await fixture.ConfirmedOrderWithQuantityAsync(10m, "gr-sem-f");
        var lineF = orderF.Lines.Single();
        var resultF = await fixture.GoodsReceiptService.CreateAsync(
            fixture.Context(Requester, "tenant.procurement.goods-receipt.create"),
            new GoodsReceiptCreateRequest(orderF.Id, WarehouseA, DateOnly.FromDateTime(DateTime.UtcNow.Date), "GR-F", null, [new GoodsReceiptLineCreateRequest(lineF.Id, 10m, 8m, 1m, null, null, null)]),
            "gr-sem-f-create",
            "fp-gr-sem-f-create");
        Assert.False(resultF.Succeeded);
        Assert.Equal("validation_failed", resultF.Code);

        // Rule G: damage quantity exceeds received -> rejected; negative damage -> rejected
        var orderG = await fixture.ConfirmedOrderWithQuantityAsync(10m, "gr-sem-g");
        var lineG = orderG.Lines.Single();
        var resultGExceeds = await fixture.GoodsReceiptService.CreateAsync(
            fixture.Context(Requester, "tenant.procurement.goods-receipt.create"),
            new GoodsReceiptCreateRequest(orderG.Id, WarehouseA, DateOnly.FromDateTime(DateTime.UtcNow.Date), "GR-G1", null, [new GoodsReceiptLineCreateRequest(lineG.Id, 10m, 8m, 2m, 11m, "Exceeds received", null)]),
            "gr-sem-g1-create",
            "fp-gr-sem-g1-create");
        Assert.False(resultGExceeds.Succeeded);
        Assert.Equal("validation_failed", resultGExceeds.Code);

        var resultGNegative = await fixture.GoodsReceiptService.CreateAsync(
            fixture.Context(Requester, "tenant.procurement.goods-receipt.create"),
            new GoodsReceiptCreateRequest(orderG.Id, WarehouseA, DateOnly.FromDateTime(DateTime.UtcNow.Date), "GR-G2", null, [new GoodsReceiptLineCreateRequest(lineG.Id, 10m, 8m, 2m, -1m, "Negative damage", null)]),
            "gr-sem-g2-create",
            "fp-gr-sem-g2-create");
        Assert.False(resultGNegative.Succeeded);
        Assert.Equal("validation_failed", resultGNegative.Code);

        // Rule H: rejected quantity does not reduce commercial remainder as accepted receipt
        // On order of 10, receiving 10 with 8 accepted and 2 rejected leaves remaining receivable as 2 (10 - 8 = 2)
        var orderH = await fixture.ConfirmedOrderWithQuantityAsync(10m, "gr-sem-h");
        var lineH = orderH.Lines.Single();
        var resultH = await fixture.GoodsReceiptService.CreateAsync(
            fixture.Context(Requester, "tenant.procurement.goods-receipt.create"),
            new GoodsReceiptCreateRequest(orderH.Id, WarehouseA, DateOnly.FromDateTime(DateTime.UtcNow.Date), "GR-H", null, [new GoodsReceiptLineCreateRequest(lineH.Id, 10m, 8m, 2m, null, null, null)]),
            "gr-sem-h-create",
            "fp-gr-sem-h-create");
        Assert.True(resultH.Succeeded, resultH.Code);
        Assert.Equal(2m, resultH.Value!.Lines.Single().RemainingReceivableQuantityAfter);

        var eligibleH = await fixture.GoodsReceiptService.ListEligibleSourcesAsync(fixture.Context(Requester, "tenant.procurement.goods-receipt.view"));
        Assert.True(eligibleH.Succeeded, eligibleH.Code);
        var sourceH = eligibleH.Value!.Single(s => s.PurchaseOrderId == orderH.Id);
        Assert.Equal(2m, sourceH.Lines.Single().RemainingReceivableQuantity);

        // Rule I: cancelled receipt releases its accepted quantity from active receipt totals
        var cancelH = await fixture.GoodsReceiptService.CancelAsync(
            fixture.Context(Requester, "tenant.procurement.goods-receipt.cancel"),
            resultH.Value.Id,
            resultH.Value.Version,
            "Releasing accepted quantity",
            "gr-sem-h-cancel",
            "fp-gr-sem-h-cancel");
        Assert.True(cancelH.Succeeded, cancelH.Code);

        var eligibleAfterCancel = await fixture.GoodsReceiptService.ListEligibleSourcesAsync(fixture.Context(Requester, "tenant.procurement.goods-receipt.view"));
        Assert.True(eligibleAfterCancel.Succeeded, eligibleAfterCancel.Code);
        var sourceAfterCancel = eligibleAfterCancel.Value!.Single(s => s.PurchaseOrderId == orderH.Id);
        Assert.Equal(10m, sourceAfterCancel.Lines.Single().RemainingReceivableQuantity);
    }

    [Fact]
    public async Task Concurrent_receipt_requests_for_seven_units_against_remainder_of_ten_prevent_atomic_over_receipt()
    {
        await using var fixture = await Fixture.CreateAsync();
        var confirmed = await fixture.ConfirmedOrderWithQuantityAsync(10m, "gr-race-10-7-7");
        var line = confirmed.Lines.Single();

        var task1 = fixture.GoodsReceiptService.CreateAsync(
            fixture.Context(Requester, "tenant.procurement.goods-receipt.create"),
            new GoodsReceiptCreateRequest(confirmed.Id, WarehouseA, DateOnly.FromDateTime(DateTime.UtcNow.Date), "GR-RACE-7A", null, [new GoodsReceiptLineCreateRequest(line.Id, 7m, 7m, 0m, null, null, null)]),
            "gr-race-7a-key",
            "fp-gr-race-7a");

        var task2 = fixture.GoodsReceiptService.CreateAsync(
            fixture.Context(Requester, "tenant.procurement.goods-receipt.create"),
            new GoodsReceiptCreateRequest(confirmed.Id, WarehouseA, DateOnly.FromDateTime(DateTime.UtcNow.Date), "GR-RACE-7B", null, [new GoodsReceiptLineCreateRequest(line.Id, 7m, 7m, 0m, null, null, null)]),
            "gr-race-7b-key",
            "fp-gr-race-7b");

        var results = await Task.WhenAll(task1, task2);
        var successCount = results.Count(r => r.Succeeded);
        var failureCount = results.Count(r => !r.Succeeded);

        Assert.Equal(1, successCount);
        Assert.Equal(1, failureCount);
        Assert.Contains(results.First(r => !r.Succeeded).Code, new[] { "over_receipt_not_allowed", "concurrency_conflict" });

        var list = await fixture.GoodsReceiptService.ListAsync(fixture.Context(Requester, "tenant.procurement.goods-receipt.view"), null, confirmed.Id);
        Assert.True(list.Succeeded, list.Code);
        var totalAccepted = list.Value!.Where(item => item.Status == GoodsReceiptStatus.Recorded).Sum(item => item.TotalAcceptedQuantity);
        Assert.Equal(7m, totalAccepted);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        public static readonly Guid WarehouseInactive = Guid.Parse("cccccccc-2222-2222-2222-222222222222");
        public static readonly Guid WarehouseForeignScope = Guid.Parse("cccccccc-3333-3333-3333-333333333333");
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
            var warehouseProvider = new ConfiguredProcurementWarehouseProvider(
            [
                new ProcurementWarehouseOption(TenantA, CompanyA, BranchA, WarehouseA, "WH-A", "Warehouse A", IsActive: true),
                new ProcurementWarehouseOption(TenantA, CompanyA, BranchA, WarehouseInactive, "WH-INACTIVE", "Inactive Warehouse", IsActive: false),
                new ProcurementWarehouseOption(TenantA, Guid.Parse("99999999-0000-0000-0000-000000000000"), null, WarehouseForeignScope, "WH-OTHER", "Other Company Warehouse", IsActive: true)
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

        /// <summary>
        /// Creates a dynamic Purchase Order confirmed with the exact specified line quantity.
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
            var policy = new PurchaseRequestApprovalPolicyDefinition("procurement.goods-receipt.dynamic", 1, [new PurchaseRequestApprovalStageDefinition("manager", 1, 1, [], false)], true, now.AddDays(-1));

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
