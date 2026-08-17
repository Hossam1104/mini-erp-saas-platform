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

public sealed class PurchaseOrderTests
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

    [Fact]
    public async Task Creates_from_current_source_decision_and_preserves_lineage_idempotently()
    {
        await using var fixture = await Fixture.CreateAsync();
        var sources = await fixture.Service.ListSourceOptionsAsync(fixture.Context(Requester, "tenant.procurement.purchase-order.view"));
        Assert.True(sources.Succeeded, sources.Code);
        var source = Assert.Single(sources.Value!);

        var created = await fixture.Service.CreateAsync(
            fixture.Context(Requester, "tenant.procurement.purchase-order.create"),
            new PurchaseOrderCreateRequest(source.Source.SourceDecisionId),
            "po-create-1",
            "fp-po-create-1");
        Assert.True(created.Succeeded, created.Code);
        Assert.Equal(PurchaseOrderStatus.Draft, created.Value!.Status);
        Assert.Equal(source.Source.SourceDecisionId, created.Value.Source.SourceDecisionId);
        Assert.Equal(source.Source.Supplier.Id, created.Value.Source.Supplier.Id);
        Assert.Equal(source.Lines.Single().SourceQuotationLineId, created.Value.Lines.Single().SourceQuotationLineId);

        var replay = await fixture.Service.CreateAsync(
            fixture.Context(Requester, "tenant.procurement.purchase-order.create"),
            new PurchaseOrderCreateRequest(source.Source.SourceDecisionId),
            "po-create-1",
            "fp-po-create-1");
        Assert.True(replay.Succeeded, replay.Code);
        Assert.Equal(created.Value.Id, replay.Value!.Id);

        var history = await fixture.Service.ReadHistoryAsync(fixture.Context(Requester, "tenant.procurement.purchase-order.history"), created.Value.Id);
        Assert.True(history.Succeeded, history.Code);
        Assert.Contains(history.Value!, item => item.Action == PurchaseOrderHistoryAction.Created);
        var audit = await fixture.Service.ReadAuditAsync(fixture.Context(Requester, "tenant.procurement.purchase-order.audit"), created.Value.Id);
        Assert.True(audit.Succeeded, audit.Code);
        Assert.Contains(audit.Value!, item => item.IdempotencyKey == "po-create-1");
    }

    [Fact]
    public async Task Enforces_approval_separation_issue_concurrency_and_cross_tenant_reads()
    {
        await using var fixture = await Fixture.CreateAsync();
        var source = Assert.Single((await fixture.Service.ListSourceOptionsAsync(fixture.Context(Requester, "tenant.procurement.purchase-order.view"))).Value!);
        var created = await fixture.Service.CreateAsync(fixture.Context(Requester, "tenant.procurement.purchase-order.create"), new PurchaseOrderCreateRequest(source.Source.SourceDecisionId), "po-flow-create", "fp-po-flow-create");
        Assert.True(created.Succeeded, created.Code);

        var edited = await fixture.Service.EditAsync(
            fixture.Context(Requester, "tenant.procurement.purchase-order.edit"),
            created.Value!.Id,
            new PurchaseOrderEditRequest("Reviewed before approval", [new PurchaseOrderLineEditRequest(created.Value.Lines.Single().Id, 2m, 12.5m, null, "Reviewed")]),
            created.Value.Version,
            "po-edit-1",
            "fp-po-edit-1");
        Assert.True(edited.Succeeded, edited.Code);
        var stale = await fixture.Service.EditAsync(fixture.Context(Requester, "tenant.procurement.purchase-order.edit"), created.Value.Id, new PurchaseOrderEditRequest("stale", [new PurchaseOrderLineEditRequest(created.Value.Lines.Single().Id, 3m, 12.5m, null, null)]), created.Value.Version, "po-edit-stale", "fp-po-edit-stale");
        Assert.False(stale.Succeeded);
        Assert.Equal("concurrency_conflict", stale.Code);

        var submitted = await fixture.Service.SubmitAsync(fixture.Context(Requester, "tenant.procurement.purchase-order.submit"), edited.Value!.Id, edited.Value.Version, "po-submit-1", "fp-po-submit-1");
        Assert.True(submitted.Succeeded, submitted.Code);
        var selfApproval = await fixture.Service.ApproveAsync(fixture.Context(Requester, "tenant.procurement.purchase-order.approve"), submitted.Value!.Id, submitted.Value.Version, "po-self-approval", "fp-po-self-approval");
        Assert.False(selfApproval.Succeeded);
        Assert.Equal("self_approval_denied", selfApproval.Code);

        var approved = await fixture.Service.ApproveAsync(fixture.Context(Approver, "tenant.procurement.purchase-order.approve"), submitted.Value.Id, submitted.Value.Version, "po-approve-1", "fp-po-approve-1");
        Assert.True(approved.Succeeded, approved.Code);
        var issued = await fixture.Service.IssueAsync(fixture.Context(Approver, "tenant.procurement.purchase-order.issue"), approved.Value!.Id, approved.Value.Version, "po-issue-1", "fp-po-issue-1");
        Assert.True(issued.Succeeded, issued.Code);

        var foreign = await fixture.Service.GetAsync(fixture.Context(Approver, "tenant.procurement.purchase-order.view", TenantB), issued.Value!.Id);
        Assert.False(foreign.Succeeded);
        Assert.Equal("purchase_order_not_found", foreign.Code);
    }

    [Fact]
    public async Task Records_full_partial_rejected_and_supplier_change_reapproval_without_silent_mutation()
    {
        await using var fixture = await Fixture.CreateAsync();
        var source = Assert.Single((await fixture.Service.ListSourceOptionsAsync(fixture.Context(Requester, "tenant.procurement.purchase-order.view"))).Value!);
        var created = await fixture.Service.CreateAsync(fixture.Context(Requester, "tenant.procurement.purchase-order.create"), new PurchaseOrderCreateRequest(source.Source.SourceDecisionId), "po-confirm-create", "fp-po-confirm-create");
        var submitted = await fixture.Service.SubmitAsync(fixture.Context(Requester, "tenant.procurement.purchase-order.submit"), created.Value!.Id, created.Value.Version, "po-confirm-submit", "fp-po-confirm-submit");
        var approved = await fixture.Service.ApproveAsync(fixture.Context(Approver, "tenant.procurement.purchase-order.approve"), submitted.Value!.Id, submitted.Value.Version, "po-confirm-approve", "fp-po-confirm-approve");
        var issued = await fixture.Service.IssueAsync(fixture.Context(Approver, "tenant.procurement.purchase-order.issue"), approved.Value!.Id, approved.Value.Version, "po-confirm-issue", "fp-po-confirm-issue");
        var line = issued.Value!.Lines.Single();

        var partial = await fixture.Service.RecordConfirmationAsync(
            fixture.Context(Requester, "tenant.procurement.purchase-order.confirmation.capture"),
            issued.Value.Id,
            new PurchaseOrderConfirmationRequest(PurchaseOrderConfirmationStatus.PartiallyConfirmed, DateOnly.FromDateTime(DateTime.UtcNow.Date), "SUP-RESP-1", "supplier@test", null, "One unit confirmed", [new PurchaseOrderConfirmationLineRequest(line.Id, 1m, DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(8)), null, null, null, null)], []),
            issued.Value.Version,
            "po-partial-1",
            "fp-po-partial-1");
        Assert.True(partial.Succeeded, partial.Code);
        Assert.Equal(PurchaseOrderStatus.PartiallyConfirmed, partial.Value!.Status);
        Assert.Equal(1m, partial.Value.Lines.Single().RemainingQuantity);

        var changed = await fixture.Service.RecordConfirmationAsync(
            fixture.Context(Requester, "tenant.procurement.purchase-order.confirmation.capture"),
            partial.Value.Id,
            new PurchaseOrderConfirmationRequest(PurchaseOrderConfirmationStatus.PartiallyConfirmed, DateOnly.FromDateTime(DateTime.UtcNow.Date), "SUP-RESP-2", "supplier@test", null, null, [new PurchaseOrderConfirmationLineRequest(line.Id, 1m, null, 1m, 15m, DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(10)), "Supplier requested a revised price and date.")], []),
            partial.Value.Version,
            "po-change-1",
            "fp-po-change-1");
        Assert.True(changed.Succeeded, changed.Code);
        Assert.Equal(PurchaseOrderStatus.ChangedPendingApproval, changed.Value!.Status);
        Assert.Equal(12.5m, changed.Value.Lines.Single().UnitPrice);
        Assert.Single(changed.Value.PendingChanges);
        Assert.Equal(PurchaseOrderSupplierChangeStatus.PendingApproval, changed.Value.PendingChanges.Single().Status);

        var reapproved = await fixture.Service.ApproveSupplierChangeAsync(fixture.Context(Approver, "tenant.procurement.purchase-order.supplier-change.approve"), changed.Value.Id, changed.Value.Version, "po-change-approve", "fp-po-change-approve");
        Assert.True(reapproved.Succeeded, reapproved.Code);
        Assert.Equal(15m, reapproved.Value!.Lines.Single().UnitPrice);
        var confirmationHistory = await fixture.Service.ReadConfirmationsAsync(fixture.Context(Requester, "tenant.procurement.purchase-order.confirmation.view"), reapproved.Value.Id);
        Assert.True(confirmationHistory.Succeeded, confirmationHistory.Code);
        Assert.Equal(PurchaseOrderSupplierChangeStatus.Approved, confirmationHistory.Value!.Last().Changes.Single().Status);

        var rejected = await fixture.Service.RecordConfirmationAsync(
            fixture.Context(Requester, "tenant.procurement.purchase-order.confirmation.capture"),
            reapproved.Value.Id,
            new PurchaseOrderConfirmationRequest(PurchaseOrderConfirmationStatus.Rejected, DateOnly.FromDateTime(DateTime.UtcNow.Date), "SUP-RESP-3", "supplier@test", "Supplier declined the order.", null, [], []),
            reapproved.Value.Version,
            "po-rejected-1",
            "fp-po-rejected-1");
        Assert.True(rejected.Succeeded, rejected.Code);
        Assert.Equal(PurchaseOrderStatus.Rejected, rejected.Value!.Status);
    }

    [Fact]
    public async Task Distinguishes_identical_retry_replay_from_cross_target_and_same_target_fingerprint_conflicts()
    {
        await using var fixture = await Fixture.CreateAsync();
        var source = Assert.Single((await fixture.Service.ListSourceOptionsAsync(fixture.Context(Requester, "tenant.procurement.purchase-order.view"))).Value!);

        var poA = await fixture.Service.CreateAsync(fixture.Context(Requester, "tenant.procurement.purchase-order.create"), new PurchaseOrderCreateRequest(source.Source.SourceDecisionId), "po-idem-create-a", "fp-idem-create-a");
        Assert.True(poA.Succeeded, poA.Code);
        var poB = await fixture.Service.CreateAsync(fixture.Context(Requester, "tenant.procurement.purchase-order.create"), new PurchaseOrderCreateRequest(source.Source.SourceDecisionId), "po-idem-create-b", "fp-idem-create-b");
        Assert.True(poB.Succeeded, poB.Code);
        Assert.NotEqual(poA.Value!.Id, poB.Value!.Id);

        const string sharedKey = "po-idem-shared-edit-key";
        var editA = await fixture.Service.EditAsync(
            fixture.Context(Requester, "tenant.procurement.purchase-order.edit"),
            poA.Value.Id,
            new PurchaseOrderEditRequest("First edit on A", [new PurchaseOrderLineEditRequest(poA.Value.Lines.Single().Id, 2m, 12.5m, null, "First edit on A")]),
            poA.Value.Version,
            sharedKey,
            "fp-edit-a-payload-1");
        Assert.True(editA.Succeeded, editA.Code);

        // Identical retry: same actor/operation/target/key/fingerprint must deterministically replay
        // the original result rather than mutate again, even though the entity has since moved on.
        var editAReplay = await fixture.Service.EditAsync(
            fixture.Context(Requester, "tenant.procurement.purchase-order.edit"),
            poA.Value.Id,
            new PurchaseOrderEditRequest("First edit on A", [new PurchaseOrderLineEditRequest(poA.Value.Lines.Single().Id, 2m, 12.5m, null, "First edit on A")]),
            poA.Value.Version,
            sharedKey,
            "fp-edit-a-payload-1");
        Assert.True(editAReplay.Succeeded, editAReplay.Code);
        Assert.Equal(editA.Value!.Version, editAReplay.Value!.Version);
        Assert.Equal(editA.Value.Notes, editAReplay.Value.Notes);

        // Same key, same target, different semantic payload: must be rejected as a conflict rather
        // than silently replaying or silently re-mutating using the stale expected version.
        var editASamePayloadDifferentFingerprint = await fixture.Service.EditAsync(
            fixture.Context(Requester, "tenant.procurement.purchase-order.edit"),
            poA.Value.Id,
            new PurchaseOrderEditRequest("Different edit on A", [new PurchaseOrderLineEditRequest(poA.Value.Lines.Single().Id, 3m, 13.5m, null, "Different edit on A")]),
            editA.Value.Version,
            sharedKey,
            "fp-edit-a-payload-2");
        Assert.False(editASamePayloadDifferentFingerprint.Succeeded);
        Assert.Equal("idempotency_conflict", editASamePayloadDifferentFingerprint.Code);

        // Same key, different target: must never replay an unrelated purchase order's result.
        var editBCrossTarget = await fixture.Service.EditAsync(
            fixture.Context(Requester, "tenant.procurement.purchase-order.edit"),
            poB.Value.Id,
            new PurchaseOrderEditRequest("Edit on B reusing A's key", [new PurchaseOrderLineEditRequest(poB.Value.Lines.Single().Id, 2m, 12.5m, null, "Edit on B reusing A's key")]),
            poB.Value.Version,
            sharedKey,
            "fp-edit-b-payload-1");
        Assert.False(editBCrossTarget.Succeeded);
        Assert.Equal("idempotency_conflict", editBCrossTarget.Code);

        // Neither conflict attempt mutated anything: B is still at its pristine created version.
        var untouchedB = await fixture.Service.GetAsync(fixture.Context(Requester, "tenant.procurement.purchase-order.view"), poB.Value.Id);
        Assert.True(untouchedB.Succeeded, untouchedB.Code);
        Assert.Equal(poB.Value.Version, untouchedB.Value!.Version);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection;
        private readonly DbContextOptions options;

        private Fixture(SqliteConnection connection, DbContextOptions options, PurchaseOrderService service)
        {
            this.connection = connection;
            this.options = options;
            Service = service;
        }

        public PurchaseOrderService Service { get; }

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

            var service = new PurchaseOrderService(
                new PurchaseRequestAuthorizationService(),
                new PurchaseOrderPersistence(options),
                new PurchaseRequestPersistence(options),
                new SupplierQuotationPersistence(options),
                new DefaultPurchaseRequestApprovalPolicyProvider(),
                new NoPurchaseRequestApprovalDelegationProvider());
            return new Fixture(connection, options, service);
        }

        public ProcurementRequestContext Context(Guid actor, string operation, Guid tenantId = default)
        {
            tenantId = tenantId == Guid.Empty ? TenantA : tenantId;
            var foundation = FoundationRequestContext.ForTenant(actor, Guid.NewGuid(), CreateTenantContext(tenantId, actor), operation);
            var resolved = new ProcurementTenantContextResolver().Resolve(foundation);
            return Assert.IsType<ProcurementRequestContext>(resolved.Context);
        }

        private static async Task SeedAsync(ProcurementDbContext db)
        {
            var now = DateTimeOffset.UtcNow;
            var scope = new PurchaseRequestScope(TenantA, CompanyA, BranchA);
            var lineId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");
            var quotationId = Guid.Parse("aaaaaaaa-aaaa-1111-1111-111111111111");
            var decisionId = Guid.Parse("aaaaaaaa-aaaa-2222-2222-222222222222");
            var policy = new PurchaseRequestApprovalPolicyDefinition("procurement.purchase-order.test", 1, [new PurchaseRequestApprovalStageDefinition("manager", 1, 1, [], false)], true, now.AddDays(-1));
            var request = new PurchaseRequestEntity(Guid.Parse("aaaaaaaa-aaaa-3333-3333-333333333333"), new TenantId(TenantA), CompanyA, BranchA, Requester, "Approved demand", now);
            request.Lines.Add(new PurchaseRequestLineEntity(lineId, new TenantId(TenantA), request.Id, new PurchaseRequestLineSnapshot(lineId, Product, "SKU-001", "Test Product", Unit, "EA", 2m, DateOnly.FromDateTime(now.UtcDateTime.Date.AddDays(7)), "Approved demand line")));
            request.Submit(policy, JsonSerializer.Serialize(policy), now);
            request.RecordApproval(PurchaseRequestStatus.Approved, 0, 0, "[]", now);
            request.TouchVersion();

            var quotationLine = new SupplierQuotationLineSnapshot(Guid.Parse("aaaaaaaa-aaaa-4444-4444-444444444444"), lineId, Product, "SKU-001", "Test Product", Unit, "EA", 2m, 2m, 12.5m, null, null, null, null, null, null, null, null, DateOnly.FromDateTime(now.UtcDateTime.Date.AddDays(7)), DateOnly.FromDateTime(now.UtcDateTime.Date.AddDays(9)), "9 days", "Quoted line");
            var supplier = new SupplierQuotationSupplierSnapshot(Supplier, "SUP-A", "Alpha Supplier");
            var currency = new SupplierQuotationCurrencySnapshot(Currency, "USD", "US Dollar");
            var quotationCommand = new SupplierQuotationCreateCommand(quotationId, request.Id, scope, Requester, supplier, "Q-PO-1", DateOnly.FromDateTime(now.UtcDateTime.Date), DateOnly.FromDateTime(now.UtcDateTime.Date.AddDays(14)), currency, null, "Delivered", DateOnly.FromDateTime(now.UtcDateTime.Date.AddDays(9)), "9 days", "Seed quote", [quotationLine], [], now, "seed");
            var quotation = new SupplierQuotationEntity(quotationCommand, new TenantId(TenantA));
            quotation.Lines.Add(new SupplierQuotationLineEntity(new TenantId(TenantA), quotationId, request.Id, quotationLine));
            quotation.SetStatus(SupplierQuotationStatus.Submitted, now);
            quotation.TouchVersion();
            var quotationRecord = new SupplierQuotationRecord(quotationId, TenantA, request.Id, scope, Requester, supplier, SupplierQuotationStatus.Submitted, "Q-PO-1", quotationCommand.OfferDate, quotationCommand.ValidUntil, currency, null, quotationCommand.DeliveryTerms, quotationCommand.OfferedDeliveryDate, quotationCommand.OfferedDeliveryLeadTime, quotationCommand.Notes, [quotationLine], [], now, now, now, quotation.Version);
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
