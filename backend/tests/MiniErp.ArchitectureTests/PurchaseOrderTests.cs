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
    private static readonly Guid Delegatee = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private static readonly Guid IneligibleApprover = Guid.Parse("99999999-9999-9999-9999-999999999999");
    private static readonly Guid StageASecondApprover = Guid.Parse("aaaaaaaa-5555-5555-5555-555555555555");
    private static readonly Guid StageBFirstApprover = Guid.Parse("bbbbbbbb-5555-5555-5555-555555555555");
    private static readonly Guid StageBSecondApprover = Guid.Parse("cccccccc-5555-5555-5555-555555555555");
    private static readonly Guid WrongDelegatee = Guid.Parse("dddddddd-5555-5555-5555-555555555555");
    private static readonly Guid Supplier = Guid.Parse("aaaaaaaa-1111-1111-1111-111111111111");
    private static readonly Guid Currency = Guid.Parse("aaaaaaaa-3333-3333-3333-333333333333");
    private static readonly Guid Product = Guid.Parse("77777777-7777-7777-7777-777777777777");
    private static readonly Guid Unit = Guid.Parse("88888888-8888-8888-8888-888888888888");

    [Fact]
    public async Task Defines_tenant_scoped_source_decision_uniqueness_as_a_database_invariant()
    {
        await using var fixture = await Fixture.CreateAsync();
        await using var db = new ProcurementDbContext(fixture.Options, CreateTenantContext(TenantA, Requester));

        var purchaseOrderType = db.Model.FindEntityType(typeof(PurchaseOrderEntity));
        var sourceDecisionIndex = Assert.Single(
            purchaseOrderType!.GetIndexes(),
            index => index.Properties.Select(property => property.Name).SequenceEqual(["TenantId", "SourceDecisionId"]));

        Assert.True(sourceDecisionIndex.IsUnique);
    }

    [Fact]
    public async Task Rejects_second_purchase_order_from_same_source_decision_with_only_one_durable_aggregate()
    {
        await using var fixture = await Fixture.CreateAsync();
        var source = Assert.Single((await fixture.Service.ListSourceOptionsAsync(fixture.Context(Requester, "tenant.procurement.purchase-order.view"))).Value!);

        var first = await fixture.Service.CreateAsync(
            fixture.Context(Requester, "tenant.procurement.purchase-order.create"),
            new PurchaseOrderCreateRequest(source.Source.SourceDecisionId),
            "po-source-unique-1",
            "fp-po-source-unique-1");
        Assert.True(first.Succeeded, first.Code);

        var duplicate = await fixture.Service.CreateAsync(
            fixture.Context(Requester, "tenant.procurement.purchase-order.create"),
            new PurchaseOrderCreateRequest(source.Source.SourceDecisionId),
            "po-source-unique-2",
            "fp-po-source-unique-2");
        Assert.False(duplicate.Succeeded);
        Assert.Equal("purchase_order_duplicate", duplicate.Code);

        var orders = await fixture.Service.ListAsync(fixture.Context(Requester, "tenant.procurement.purchase-order.view"), null);
        Assert.True(orders.Succeeded, orders.Code);
        Assert.NotNull(orders.Value);
        Assert.Single(orders.Value!);
        Assert.Equal(first.Value!.Id, orders.Value!.Single().Id);

        var history = await fixture.Service.ReadHistoryAsync(fixture.Context(Requester, "tenant.procurement.purchase-order.history"), first.Value.Id);
        Assert.True(history.Succeeded, history.Code);
        Assert.Equal(1, history.Value!.Count(item => item.Action == PurchaseOrderHistoryAction.Created));

        var audit = await fixture.Service.ReadAuditAsync(fixture.Context(Requester, "tenant.procurement.purchase-order.audit"), first.Value.Id);
        Assert.True(audit.Succeeded, audit.Code);
        Assert.Equal(1, audit.Value!.Count(item => item.OperationId == "procurement.purchase-order.create"));
    }

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

        var consumedSources = await fixture.Service.ListSourceOptionsAsync(fixture.Context(Requester, "tenant.procurement.purchase-order.view"));
        Assert.True(consumedSources.Succeeded, consumedSources.Code);
        Assert.Empty(consumedSources.Value!);

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
    public async Task Enforces_eligible_ineligible_delegated_and_invalid_delegated_approval_actors()
    {
        var policy = new PurchaseRequestApprovalPolicyDefinition(
            "procurement.purchase-order.approver-tests",
            2,
            [new PurchaseRequestApprovalStageDefinition("manager", 1, 1, [Approver], true)],
            true,
            DateTimeOffset.MinValue);

        await using (var eligibleFixture = await Fixture.CreateAsync(policy))
        {
            var pending = await eligibleFixture.PendingApprovalAsync("eligible");
            var approved = await eligibleFixture.Service.ApproveAsync(
                eligibleFixture.Context(Approver, "tenant.procurement.purchase-order.approve"),
                pending.Id,
                pending.Version,
                "po-eligible-approve",
                "fp-po-eligible-approve");
            Assert.True(approved.Succeeded, approved.Code);
            Assert.Equal(PurchaseOrderStatus.Approved, approved.Value!.Status);
        }

        await using (var ineligibleFixture = await Fixture.CreateAsync(policy))
        {
            var pending = await ineligibleFixture.PendingApprovalAsync("ineligible");
            var denied = await ineligibleFixture.Service.ApproveAsync(
                ineligibleFixture.Context(IneligibleApprover, "tenant.procurement.purchase-order.approve"),
                pending.Id,
                pending.Version,
                "po-ineligible-approve",
                "fp-po-ineligible-approve");
            Assert.False(denied.Succeeded);
            Assert.Equal("approval_not_eligible", denied.Code);
        }

        var validDelegation = new ConfiguredPurchaseRequestApprovalDelegationProvider(
            [new PurchaseRequestApprovalDelegation(
                TenantA,
                CompanyA,
                BranchA,
                "manager",
                Approver,
                Delegatee,
                DateTimeOffset.UtcNow.AddMinutes(-1),
                DateTimeOffset.UtcNow.AddMinutes(10),
                "Approved leave coverage")]);
        await using (var delegatedFixture = await Fixture.CreateAsync(policy, validDelegation))
        {
            var pending = await delegatedFixture.PendingApprovalAsync("delegated");
            var approved = await delegatedFixture.Service.ApproveAsync(
                delegatedFixture.Context(Delegatee, "tenant.procurement.purchase-order.approve"),
                pending.Id,
                pending.Version,
                "po-delegated-approve",
                "fp-po-delegated-approve");
            Assert.True(approved.Succeeded, approved.Code);
            Assert.Equal(PurchaseOrderStatus.Approved, approved.Value!.Status);
            var history = await delegatedFixture.Service.ReadHistoryAsync(
                delegatedFixture.Context(Requester, "tenant.procurement.purchase-order.history"),
                pending.Id);
            Assert.True(history.Succeeded, history.Code);
            Assert.Equal(Approver, Assert.Single(history.Value!, item => item.Action == PurchaseOrderHistoryAction.ApprovalRecorded).DelegatedFromActorId);
        }

        var invalidDelegation = new ConfiguredPurchaseRequestApprovalDelegationProvider(
            [new PurchaseRequestApprovalDelegation(
                TenantA,
                CompanyA,
                BranchA,
                "manager",
                IneligibleApprover,
                Delegatee,
                DateTimeOffset.UtcNow.AddMinutes(-1),
                DateTimeOffset.UtcNow.AddMinutes(10),
                "Invalid delegator is not an eligible approver")]);
        await using (var invalidDelegationFixture = await Fixture.CreateAsync(policy, invalidDelegation))
        {
            var pending = await invalidDelegationFixture.PendingApprovalAsync("invalid-delegation");
            var denied = await invalidDelegationFixture.Service.ApproveAsync(
                invalidDelegationFixture.Context(Delegatee, "tenant.procurement.purchase-order.approve"),
                pending.Id,
                pending.Version,
                "po-invalid-delegation-approve",
                "fp-po-invalid-delegation-approve");
            Assert.False(denied.Succeeded);
            Assert.Equal("approval_not_eligible", denied.Code);
        }
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
        Assert.Equal(PurchaseOrderConfirmationStatus.PartiallyConfirmed, changed.Value.LatestConfirmationStatus);
        Assert.Equal(1m, changed.Value.Lines.Single().ConfirmedQuantity);
        Assert.Equal(1m, changed.Value.Lines.Single().RemainingQuantity);
        Assert.Equal(12.5m, changed.Value.Lines.Single().UnitPrice);
        Assert.Single(changed.Value.PendingChanges);
        Assert.Equal(PurchaseOrderSupplierChangeStatus.PendingApproval, changed.Value.PendingChanges.Single().Status);

        var reapproved = await fixture.Service.ApproveSupplierChangeAsync(fixture.Context(Approver, "tenant.procurement.purchase-order.supplier-change.approve"), changed.Value.Id, changed.Value.Version, "po-change-approve", "fp-po-change-approve");
        Assert.True(reapproved.Succeeded, reapproved.Code);
        Assert.Equal(PurchaseOrderStatus.Confirmed, reapproved.Value!.Status);
        Assert.Equal(1m, reapproved.Value.Lines.Single().ConfirmedQuantity);
        Assert.Equal(0m, reapproved.Value.Lines.Single().RemainingQuantity);
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
    public async Task Resets_supplier_change_reapproval_stage_approvers_and_requires_genuine_approvals_per_stage()
    {
        var policy = new PurchaseRequestApprovalPolicyDefinition(
            "procurement.purchase-order.multi-stage-reapproval",
            2,
            [
                new PurchaseRequestApprovalStageDefinition("stage-a", 1, 2, [Approver, StageASecondApprover], true),
                new PurchaseRequestApprovalStageDefinition("stage-b", 2, 2, [StageBFirstApprover, StageBSecondApprover], true),
            ],
            true,
            DateTimeOffset.MinValue);

        await using var fixture = await Fixture.CreateAsync(policy);
        var source = Assert.Single((await fixture.Service.ListSourceOptionsAsync(fixture.Context(Requester, "tenant.procurement.purchase-order.view"))).Value!);
        var created = await fixture.Service.CreateAsync(
            fixture.Context(Requester, "tenant.procurement.purchase-order.create"),
            new PurchaseOrderCreateRequest(source.Source.SourceDecisionId),
            "po-multi-stage-create",
            "fp-po-multi-stage-create");
        Assert.True(created.Succeeded, created.Code);

        var submitted = await fixture.Service.SubmitAsync(
            fixture.Context(Requester, "tenant.procurement.purchase-order.submit"),
            created.Value!.Id,
            created.Value.Version,
            "po-multi-stage-submit",
            "fp-po-multi-stage-submit");
        Assert.True(submitted.Succeeded, submitted.Code);

        var stageAFirst = await fixture.Service.ApproveAsync(
            fixture.Context(Approver, "tenant.procurement.purchase-order.approve"),
            submitted.Value!.Id,
            submitted.Value.Version,
            "po-multi-stage-a1",
            "fp-po-multi-stage-a1");
        Assert.True(stageAFirst.Succeeded, stageAFirst.Code);
        Assert.Equal(0, stageAFirst.Value!.Approval!.StageIndex);
        Assert.Equal(1, stageAFirst.Value.Approval.RecordedApprovals);

        var stageASecond = await fixture.Service.ApproveAsync(
            fixture.Context(StageASecondApprover, "tenant.procurement.purchase-order.approve"),
            stageAFirst.Value.Id,
            stageAFirst.Value.Version,
            "po-multi-stage-a2",
            "fp-po-multi-stage-a2");
        Assert.True(stageASecond.Succeeded, stageASecond.Code);

        var stageBFirst = await fixture.Service.ApproveAsync(
            fixture.Context(StageBFirstApprover, "tenant.procurement.purchase-order.approve"),
            stageASecond.Value!.Id,
            stageASecond.Value.Version,
            "po-multi-stage-b1",
            "fp-po-multi-stage-b1");
        Assert.True(stageBFirst.Succeeded, stageBFirst.Code);
        var stageBSecond = await fixture.Service.ApproveAsync(
            fixture.Context(StageBSecondApprover, "tenant.procurement.purchase-order.approve"),
            stageBFirst.Value!.Id,
            stageBFirst.Value.Version,
            "po-multi-stage-b2",
            "fp-po-multi-stage-b2");
        Assert.True(stageBSecond.Succeeded, stageBSecond.Code);
        var issued = await fixture.Service.IssueAsync(
            fixture.Context(StageBSecondApprover, "tenant.procurement.purchase-order.issue"),
            stageBSecond.Value!.Id,
            stageBSecond.Value.Version,
            "po-multi-stage-issue",
            "fp-po-multi-stage-issue");
        Assert.True(issued.Succeeded, issued.Code);

        var line = issued.Value!.Lines.Single();
        var changed = await fixture.Service.RecordConfirmationAsync(
            fixture.Context(Requester, "tenant.procurement.purchase-order.confirmation.capture"),
            issued.Value.Id,
            new PurchaseOrderConfirmationRequest(
                PurchaseOrderConfirmationStatus.PartiallyConfirmed,
                DateOnly.FromDateTime(DateTime.UtcNow.Date),
                "SUP-MULTI-STAGE",
                "supplier@test",
                null,
                null,
                [new PurchaseOrderConfirmationLineRequest(line.Id, 1m, null, null, 15m, null, "Supplier proposed a revised price.")],
                []),
            issued.Value.Version,
            "po-multi-stage-change",
            "fp-po-multi-stage-change");
        Assert.True(changed.Succeeded, changed.Code);
        Assert.Equal(PurchaseOrderStatus.ChangedPendingApproval, changed.Value!.Status);
        Assert.Equal(0, changed.Value.Approval!.RecordedApprovals);
        Assert.Equal(0, changed.Value.Approval.StageIndex);
        Assert.Equal("stage-a", changed.Value.Approval.StageKey);

        var reapprovalAFirst = await fixture.Service.ApproveSupplierChangeAsync(
            fixture.Context(Approver, "tenant.procurement.purchase-order.supplier-change.approve"),
            changed.Value.Id,
            changed.Value.Version,
            "po-multi-stage-reapproval-a1",
            "fp-po-multi-stage-reapproval-a1");
        Assert.True(reapprovalAFirst.Succeeded, reapprovalAFirst.Code);
        Assert.Equal(0, reapprovalAFirst.Value!.Approval!.StageIndex);
        Assert.Equal(1, reapprovalAFirst.Value.Approval.RecordedApprovals);
        Assert.Equal(12.5m, reapprovalAFirst.Value.Lines.Single().UnitPrice);

        var reapprovalASecond = await fixture.Service.ApproveSupplierChangeAsync(
            fixture.Context(StageASecondApprover, "tenant.procurement.purchase-order.supplier-change.approve"),
            reapprovalAFirst.Value.Id,
            reapprovalAFirst.Value.Version,
            "po-multi-stage-reapproval-a2",
            "fp-po-multi-stage-reapproval-a2");
        Assert.True(reapprovalASecond.Succeeded, reapprovalASecond.Code);
        Assert.Equal(PurchaseOrderStatus.ChangedPendingApproval, reapprovalASecond.Value!.Status);
        Assert.Equal(1, reapprovalASecond.Value.Approval!.StageIndex);
        Assert.Equal("stage-b", reapprovalASecond.Value.Approval.StageKey);
        Assert.Equal(0, reapprovalASecond.Value.Approval.RecordedApprovals);
        Assert.Equal(12.5m, reapprovalASecond.Value.Lines.Single().UnitPrice);

        // Stage-A approvers are not stage-B approvals. They fail eligibility rather than being
        // treated as duplicate stage-B approvals, and the stage-B count remains zero.
        var formerStageA = await fixture.Service.ApproveSupplierChangeAsync(
            fixture.Context(Approver, "tenant.procurement.purchase-order.supplier-change.approve"),
            reapprovalASecond.Value.Id,
            reapprovalASecond.Value.Version,
            "po-multi-stage-former-a",
            "fp-po-multi-stage-former-a");
        Assert.False(formerStageA.Succeeded);
        Assert.Equal("approval_not_eligible", formerStageA.Code);

        var reapprovalBFirst = await fixture.Service.ApproveSupplierChangeAsync(
            fixture.Context(StageBFirstApprover, "tenant.procurement.purchase-order.supplier-change.approve"),
            reapprovalASecond.Value.Id,
            reapprovalASecond.Value.Version,
            "po-multi-stage-reapproval-b1",
            "fp-po-multi-stage-reapproval-b1");
        Assert.True(reapprovalBFirst.Succeeded, reapprovalBFirst.Code);
        Assert.Equal(1, reapprovalBFirst.Value!.Approval!.StageIndex);
        Assert.Equal(1, reapprovalBFirst.Value.Approval.RecordedApprovals);
        Assert.Equal(12.5m, reapprovalBFirst.Value.Lines.Single().UnitPrice);
        Assert.Equal(PurchaseOrderStatus.ChangedPendingApproval, reapprovalBFirst.Value.Status);

        var reapprovalBSecond = await fixture.Service.ApproveSupplierChangeAsync(
            fixture.Context(StageBSecondApprover, "tenant.procurement.purchase-order.supplier-change.approve"),
            reapprovalBFirst.Value.Id,
            reapprovalBFirst.Value.Version,
            "po-multi-stage-reapproval-b2",
            "fp-po-multi-stage-reapproval-b2");
        Assert.True(reapprovalBSecond.Succeeded, reapprovalBSecond.Code);
        Assert.Equal(PurchaseOrderStatus.PartiallyConfirmed, reapprovalBSecond.Value!.Status);
        Assert.Equal(15m, reapprovalBSecond.Value.Lines.Single().UnitPrice);
        Assert.Empty(reapprovalBSecond.Value.PendingChanges);

        var confirmations = await fixture.Service.ReadConfirmationsAsync(fixture.Context(Requester, "tenant.procurement.purchase-order.confirmation.view"), reapprovalBSecond.Value.Id);
        Assert.True(confirmations.Succeeded, confirmations.Code);
        Assert.Equal(PurchaseOrderSupplierChangeStatus.Approved, confirmations.Value!.Single().Changes.Single().Status);

        var history = await fixture.Service.ReadHistoryAsync(fixture.Context(Requester, "tenant.procurement.purchase-order.history"), reapprovalBSecond.Value.Id);
        Assert.True(history.Succeeded, history.Code);
        var reapprovalHistory = history.Value!
            .SkipWhile(item => item.Action != PurchaseOrderHistoryAction.SupplierChangeProposed)
            .Skip(1)
            .ToArray();
        var reapprovalApprovalHistory = reapprovalHistory
            .Where(item => item.Action == PurchaseOrderHistoryAction.ApprovalRecorded)
            .ToArray();
        Assert.Equal(["stage-a", "stage-a", "stage-b"], reapprovalApprovalHistory.Select(item => item.StageKey ?? string.Empty).ToArray());
        var finalApprovalHistory = Assert.Single(reapprovalHistory, item => item.Action == PurchaseOrderHistoryAction.SupplierChangeApproved);
        Assert.Equal("stage-b", finalApprovalHistory.StageKey);
        Assert.DoesNotContain(reapprovalHistory, item => item.ActorId == Approver && item.StageKey == "stage-b");
    }

    [Fact]
    public async Task Enforces_supplier_change_reapproval_eligibility_delegation_and_self_approval()
    {
        var policy = new PurchaseRequestApprovalPolicyDefinition(
            "procurement.purchase-order.reapproval-actor-tests",
            3,
            [new PurchaseRequestApprovalStageDefinition("manager", 1, 1, [Approver], true)],
            true,
            DateTimeOffset.MinValue);

        await using (var eligibleFixture = await Fixture.CreateAsync(policy))
        {
            var changed = await eligibleFixture.ChangedPendingApprovalAsync("reapproval-eligible");
            var self = await eligibleFixture.Service.ApproveSupplierChangeAsync(
                eligibleFixture.Context(Requester, "tenant.procurement.purchase-order.supplier-change.approve"),
                changed.Id,
                changed.Version,
                "po-reapproval-self",
                "fp-po-reapproval-self");
            Assert.False(self.Succeeded);
            Assert.Equal("self_approval_denied", self.Code);

            var ineligible = await eligibleFixture.Service.ApproveSupplierChangeAsync(
                eligibleFixture.Context(IneligibleApprover, "tenant.procurement.purchase-order.supplier-change.approve"),
                changed.Id,
                changed.Version,
                "po-reapproval-ineligible",
                "fp-po-reapproval-ineligible");
            Assert.False(ineligible.Succeeded);
            Assert.Equal("approval_not_eligible", ineligible.Code);

            var eligible = await eligibleFixture.Service.ApproveSupplierChangeAsync(
                eligibleFixture.Context(Approver, "tenant.procurement.purchase-order.supplier-change.approve"),
                changed.Id,
                changed.Version,
                "po-reapproval-eligible",
                "fp-po-reapproval-eligible");
            Assert.True(eligible.Succeeded, eligible.Code);
            Assert.Equal(PurchaseOrderStatus.Confirmed, eligible.Value!.Status);
        }

        var validDelegation = new ConfiguredPurchaseRequestApprovalDelegationProvider(
            [new PurchaseRequestApprovalDelegation(
                TenantA,
                CompanyA,
                BranchA,
                "manager",
                Approver,
                Delegatee,
                DateTimeOffset.UtcNow.AddMinutes(-1),
                DateTimeOffset.UtcNow.AddMinutes(10),
                "Approved leave coverage")]);
        await using (var delegatedFixture = await Fixture.CreateAsync(policy, validDelegation))
        {
            var changed = await delegatedFixture.ChangedPendingApprovalAsync("reapproval-delegated");
            var delegated = await delegatedFixture.Service.ApproveSupplierChangeAsync(
                delegatedFixture.Context(Delegatee, "tenant.procurement.purchase-order.supplier-change.approve"),
                changed.Id,
                changed.Version,
                "po-reapproval-delegated",
                "fp-po-reapproval-delegated");
            Assert.True(delegated.Succeeded, delegated.Code);
            Assert.Equal(PurchaseOrderStatus.Confirmed, delegated.Value!.Status);
            var history = await delegatedFixture.Service.ReadHistoryAsync(delegatedFixture.Context(Requester, "tenant.procurement.purchase-order.history"), changed.Id);
            Assert.True(history.Succeeded, history.Code);
            Assert.Equal(Approver, Assert.Single(history.Value!, item => item.Action == PurchaseOrderHistoryAction.SupplierChangeApproved).DelegatedFromActorId);
        }

        var invalidOrExpiredDelegations = new ConfiguredPurchaseRequestApprovalDelegationProvider(
            [
                new PurchaseRequestApprovalDelegation(TenantA, CompanyA, BranchA, "manager", IneligibleApprover, Delegatee, DateTimeOffset.UtcNow.AddMinutes(-1), DateTimeOffset.UtcNow.AddMinutes(10), "Invalid delegator"),
                new PurchaseRequestApprovalDelegation(TenantA, CompanyA, BranchA, "manager", Approver, Delegatee, DateTimeOffset.UtcNow.AddMinutes(-20), DateTimeOffset.UtcNow.AddMinutes(-10), "Expired delegation"),
            ]);
        await using (var invalidFixture = await Fixture.CreateAsync(policy, invalidOrExpiredDelegations))
        {
            var changed = await invalidFixture.ChangedPendingApprovalAsync("reapproval-invalid-delegation");
            var invalid = await invalidFixture.Service.ApproveSupplierChangeAsync(
                invalidFixture.Context(Delegatee, "tenant.procurement.purchase-order.supplier-change.approve"),
                changed.Id,
                changed.Version,
                "po-reapproval-invalid-delegation",
                "fp-po-reapproval-invalid-delegation");
            Assert.False(invalid.Succeeded);
            Assert.Equal("approval_not_eligible", invalid.Code);

            var wrongActor = await invalidFixture.Service.ApproveSupplierChangeAsync(
                invalidFixture.Context(WrongDelegatee, "tenant.procurement.purchase-order.supplier-change.approve"),
                changed.Id,
                changed.Version,
                "po-reapproval-wrong-actor",
                "fp-po-reapproval-wrong-actor");
            Assert.False(wrongActor.Succeeded);
            Assert.Equal("approval_not_eligible", wrongActor.Code);
        }
    }

    [Fact]
    public async Task Preserves_confirmation_facts_through_supplier_change_rejection_and_approval_and_rejects_impossible_quantity_without_effects()
    {
        await using var fixture = await Fixture.CreateAsync();
        var issued = await fixture.IssuedOrderAsync("commercial-integrity");
        var line = issued.Lines.Single();
        var responseDate = DateOnly.FromDateTime(DateTime.UtcNow.Date);

        var partialChange = await fixture.Service.RecordConfirmationAsync(
            fixture.Context(Requester, "tenant.procurement.purchase-order.confirmation.capture"),
            issued.Id,
            new PurchaseOrderConfirmationRequest(
                PurchaseOrderConfirmationStatus.PartiallyConfirmed,
                responseDate,
                "SUP-PARTIAL",
                "supplier@test",
                null,
                null,
                [new PurchaseOrderConfirmationLineRequest(line.Id, 1m, responseDate.AddDays(8), null, 15m, null, "Supplier proposed a revised unit price.")],
                []),
            issued.Version,
            "po-commercial-partial",
            "fp-po-commercial-partial");
        Assert.True(partialChange.Succeeded, partialChange.Code);
        Assert.Equal(PurchaseOrderStatus.ChangedPendingApproval, partialChange.Value!.Status);
        Assert.Equal(1m, partialChange.Value.Lines.Single().ConfirmedQuantity);
        Assert.Equal(1m, partialChange.Value.Lines.Single().RemainingQuantity);

        var partialRejected = await fixture.Service.RejectSupplierChangeAsync(
            fixture.Context(Approver, "tenant.procurement.purchase-order.supplier-change.reject"),
            partialChange.Value.Id,
            partialChange.Value.Version,
            "The revised price is not accepted.",
            "po-commercial-partial-reject",
            "fp-po-commercial-partial-reject");
        Assert.True(partialRejected.Succeeded, partialRejected.Code);
        Assert.Equal(PurchaseOrderStatus.PartiallyConfirmed, partialRejected.Value!.Status);
        Assert.Equal(1m, partialRejected.Value.Lines.Single().ConfirmedQuantity);
        Assert.Equal(1m, partialRejected.Value.Lines.Single().RemainingQuantity);
        Assert.Equal(12.5m, partialRejected.Value.Lines.Single().UnitPrice);

        var fullChange = await fixture.Service.RecordConfirmationAsync(
            fixture.Context(Requester, "tenant.procurement.purchase-order.confirmation.capture"),
            partialRejected.Value.Id,
            new PurchaseOrderConfirmationRequest(
                PurchaseOrderConfirmationStatus.Confirmed,
                responseDate,
                "SUP-FULL",
                "supplier@test",
                null,
                null,
                [new PurchaseOrderConfirmationLineRequest(line.Id, 2m, responseDate.AddDays(9), null, 16m, null, "Supplier proposed a final unit price.")],
                []),
            partialRejected.Value.Version,
            "po-commercial-full",
            "fp-po-commercial-full");
        Assert.True(fullChange.Succeeded, fullChange.Code);
        Assert.Equal(PurchaseOrderStatus.ChangedPendingApproval, fullChange.Value!.Status);
        Assert.Equal(PurchaseOrderConfirmationStatus.Confirmed, fullChange.Value.LatestConfirmationStatus);
        Assert.Equal(2m, fullChange.Value.Lines.Single().ConfirmedQuantity);
        Assert.Equal(0m, fullChange.Value.Lines.Single().RemainingQuantity);

        var fullApproved = await fixture.Service.ApproveSupplierChangeAsync(
            fixture.Context(Approver, "tenant.procurement.purchase-order.supplier-change.approve"),
            fullChange.Value.Id,
            fullChange.Value.Version,
            "po-commercial-full-approve",
            "fp-po-commercial-full-approve");
        Assert.True(fullApproved.Succeeded, fullApproved.Code);
        Assert.Equal(PurchaseOrderStatus.Confirmed, fullApproved.Value!.Status);
        Assert.Equal(2m, fullApproved.Value.Lines.Single().ConfirmedQuantity);
        Assert.Equal(0m, fullApproved.Value.Lines.Single().RemainingQuantity);
        Assert.Equal(16m, fullApproved.Value.Lines.Single().UnitPrice);

        var confirmationsBeforeImpossible = await fixture.Service.ReadConfirmationsAsync(fixture.Context(Requester, "tenant.procurement.purchase-order.confirmation.view"), fullApproved.Value.Id);
        var historyBeforeImpossible = await fixture.Service.ReadHistoryAsync(fixture.Context(Requester, "tenant.procurement.purchase-order.history"), fullApproved.Value.Id);
        var auditBeforeImpossible = await fixture.Service.ReadAuditAsync(fixture.Context(Requester, "tenant.procurement.purchase-order.audit"), fullApproved.Value.Id);

        var impossible = await fixture.Service.RecordConfirmationAsync(
            fixture.Context(Requester, "tenant.procurement.purchase-order.confirmation.capture"),
            fullApproved.Value.Id,
            new PurchaseOrderConfirmationRequest(
                PurchaseOrderConfirmationStatus.Confirmed,
                responseDate,
                "SUP-IMPOSSIBLE",
                "supplier@test",
                null,
                null,
                [new PurchaseOrderConfirmationLineRequest(line.Id, 2m, null, 1m, null, null, "Impossible reduction below confirmed quantity.")],
                []),
            fullApproved.Value.Version,
            "po-commercial-impossible",
            "fp-po-commercial-impossible");
        Assert.False(impossible.Succeeded);
        Assert.Equal("proposed_quantity_below_confirmed", impossible.Code);

        var current = await fixture.Service.GetAsync(fixture.Context(Requester, "tenant.procurement.purchase-order.view"), fullApproved.Value.Id);
        Assert.True(current.Succeeded, current.Code);
        Assert.Equal(fullApproved.Value.Version, current.Value!.Version);
        Assert.Equal(2m, current.Value.Lines.Single().ConfirmedQuantity);
        Assert.Equal(16m, current.Value.Lines.Single().UnitPrice);
        var confirmationsAfterImpossible = await fixture.Service.ReadConfirmationsAsync(fixture.Context(Requester, "tenant.procurement.purchase-order.confirmation.view"), fullApproved.Value.Id);
        var historyAfterImpossible = await fixture.Service.ReadHistoryAsync(fixture.Context(Requester, "tenant.procurement.purchase-order.history"), fullApproved.Value.Id);
        var auditAfterImpossible = await fixture.Service.ReadAuditAsync(fixture.Context(Requester, "tenant.procurement.purchase-order.audit"), fullApproved.Value.Id);
        Assert.Equal(confirmationsBeforeImpossible.Value!.Count, confirmationsAfterImpossible.Value!.Count);
        Assert.Equal(historyBeforeImpossible.Value!.Count, historyAfterImpossible.Value!.Count);
        Assert.Equal(auditBeforeImpossible.Value!.Count, auditAfterImpossible.Value!.Count);
    }

    [Fact]
    public async Task Distinguishes_identical_retry_replay_from_cross_target_and_same_target_fingerprint_conflicts()
    {
        await using var fixture = await Fixture.CreateAsync();
        var source = Assert.Single((await fixture.Service.ListSourceOptionsAsync(fixture.Context(Requester, "tenant.procurement.purchase-order.view"))).Value!);

        var poA = await fixture.Service.CreateAsync(fixture.Context(Requester, "tenant.procurement.purchase-order.create"), new PurchaseOrderCreateRequest(source.Source.SourceDecisionId), "po-idem-create-a", "fp-idem-create-a");
        Assert.True(poA.Succeeded, poA.Code);
        var secondSource = await fixture.AddSecondSourceAsync();
        var poB = await fixture.Service.CreateAsync(fixture.Context(Requester, "tenant.procurement.purchase-order.create"), new PurchaseOrderCreateRequest(secondSource.Source.SourceDecisionId), "po-idem-create-b", "fp-idem-create-b");
        Assert.True(poB.Succeeded, poB.Code);
        Assert.NotEqual(poA.Value!.Id, poB.Value!.Id);

        var duplicate = await fixture.Service.CreateAsync(fixture.Context(Requester, "tenant.procurement.purchase-order.create"), new PurchaseOrderCreateRequest(source.Source.SourceDecisionId), "po-idem-create-duplicate", "fp-idem-create-duplicate");
        Assert.False(duplicate.Succeeded);
        Assert.Equal("purchase_order_duplicate", duplicate.Code);

        var foreign = await fixture.Service.CreateAsync(fixture.Context(Requester, "tenant.procurement.purchase-order.create", TenantB), new PurchaseOrderCreateRequest(source.Source.SourceDecisionId), "po-idem-create-foreign", "fp-po-idem-create-foreign");
        Assert.False(foreign.Succeeded);
        Assert.Equal("source_decision_not_found", foreign.Code);

        const string sharedKey = "po-idem-shared-edit-key";
        var editA = await fixture.Service.EditAsync(
            fixture.Context(Requester, "tenant.procurement.purchase-order.edit"),
            poA.Value.Id,
            new PurchaseOrderEditRequest("First edit on A", [new PurchaseOrderLineEditRequest(poA.Value.Lines.Single().Id, 2m, 12.5m, null, "First edit on A")]),
            poA.Value.Version,
            sharedKey,
            "fp-edit-a-payload-1");
        Assert.True(editA.Succeeded, editA.Code);

        var editAAfterOriginal = await fixture.Service.EditAsync(
            fixture.Context(Requester, "tenant.procurement.purchase-order.edit"),
            poA.Value.Id,
            new PurchaseOrderEditRequest("Second edit on A", [new PurchaseOrderLineEditRequest(poA.Value.Lines.Single().Id, 3m, 13.5m, null, "Second edit on A")]),
            editA.Value!.Version,
            "po-idem-edit-a-second",
            "fp-edit-a-payload-second");
        Assert.True(editAAfterOriginal.Succeeded, editAAfterOriginal.Code);
        Assert.NotEqual(editA.Value.Version, editAAfterOriginal.Value!.Version);

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
        Assert.Equal(2m, editAReplay.Value.Lines.Single().OrderedQuantity);
        Assert.Equal(12.5m, editAReplay.Value.Lines.Single().UnitPrice);

        // Same key, same target, different semantic payload: must be rejected as a conflict rather
        // than silently replaying or silently re-mutating using the stale expected version.
        var editASamePayloadDifferentFingerprint = await fixture.Service.EditAsync(
            fixture.Context(Requester, "tenant.procurement.purchase-order.edit"),
            poA.Value.Id,
            new PurchaseOrderEditRequest("Different edit on A", [new PurchaseOrderLineEditRequest(poA.Value.Lines.Single().Id, 3m, 13.5m, null, "Different edit on A")]),
            editAAfterOriginal.Value.Version,
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

    [Fact]
    public async Task Replays_durable_submit_approve_and_issue_after_the_original_request_advanced_state()
    {
        await using var fixture = await Fixture.CreateAsync();
        var source = Assert.Single((await fixture.Service.ListSourceOptionsAsync(fixture.Context(Requester, "tenant.procurement.purchase-order.view"))).Value!);
        var created = await fixture.Service.CreateAsync(fixture.Context(Requester, "tenant.procurement.purchase-order.create"), new PurchaseOrderCreateRequest(source.Source.SourceDecisionId), "po-durable-create", "fp-durable-create");
        Assert.True(created.Succeeded, created.Code);

        // Submit succeeds and moves the order to PendingApproval, so the expected version carried by the
        // original request is now stale and Draft/ReturnedForChange no longer holds. An identical durable
        // retry must still replay rather than return submit_not_allowed.
        var submitted = await fixture.Service.SubmitAsync(fixture.Context(Requester, "tenant.procurement.purchase-order.submit"), created.Value!.Id, created.Value.Version, "po-durable-submit", "fp-durable-submit");
        Assert.True(submitted.Succeeded, submitted.Code);
        Assert.Equal(PurchaseOrderStatus.PendingApproval, submitted.Value!.Status);

        var approved = await fixture.Service.ApproveAsync(fixture.Context(Approver, "tenant.procurement.purchase-order.approve"), submitted.Value.Id, submitted.Value.Version, "po-durable-approve", "fp-durable-approve");
        Assert.True(approved.Succeeded, approved.Code);
        Assert.Equal(PurchaseOrderStatus.Approved, approved.Value!.Status);

        // The original submit response must remain replayable after approval has advanced the target.
        var submitReplay = await fixture.Service.SubmitAsync(fixture.Context(Requester, "tenant.procurement.purchase-order.submit"), created.Value.Id, created.Value.Version, "po-durable-submit", "fp-durable-submit");
        Assert.True(submitReplay.Succeeded, submitReplay.Code);
        Assert.Equal(PurchaseOrderStatus.PendingApproval, submitReplay.Value!.Status);
        Assert.Equal(submitted.Value.Version, submitReplay.Value.Version);

        var issued = await fixture.Service.IssueAsync(fixture.Context(Approver, "tenant.procurement.purchase-order.issue"), approved.Value.Id, approved.Value.Version, "po-durable-issue", "fp-durable-issue");
        Assert.True(issued.Succeeded, issued.Code);
        Assert.Equal(PurchaseOrderStatus.Issued, issued.Value!.Status);

        // The original approval response must remain replayable after issue has advanced the target.
        var approveReplay = await fixture.Service.ApproveAsync(fixture.Context(Approver, "tenant.procurement.purchase-order.approve"), submitted.Value.Id, submitted.Value.Version, "po-durable-approve", "fp-durable-approve");
        Assert.True(approveReplay.Succeeded, approveReplay.Code);
        Assert.Equal(PurchaseOrderStatus.Approved, approveReplay.Value!.Status);
        Assert.Equal(approved.Value.Version, approveReplay.Value.Version);

        var issuedLine = issued.Value.Lines.Single();
        var laterConfirmation = await fixture.Service.RecordConfirmationAsync(
            fixture.Context(Requester, "tenant.procurement.purchase-order.confirmation.capture"),
            issued.Value.Id,
            new PurchaseOrderConfirmationRequest(
                PurchaseOrderConfirmationStatus.PartiallyConfirmed,
                DateOnly.FromDateTime(DateTime.UtcNow.Date),
                "SUP-DURABLE-LATER",
                "supplier@test",
                null,
                "Later state mutation after issue.",
                [new PurchaseOrderConfirmationLineRequest(issuedLine.Id, 1m, null, null, null, null, null)],
                []),
            issued.Value.Version,
            "po-durable-later-confirmation",
            "fp-po-durable-later-confirmation");
        Assert.True(laterConfirmation.Succeeded, laterConfirmation.Code);
        Assert.Equal(PurchaseOrderStatus.PartiallyConfirmed, laterConfirmation.Value!.Status);

        // The original issue response must remain replayable after a later confirmation mutation.
        var issueReplay = await fixture.Service.IssueAsync(fixture.Context(Approver, "tenant.procurement.purchase-order.issue"), approved.Value.Id, approved.Value.Version, "po-durable-issue", "fp-durable-issue");
        Assert.True(issueReplay.Succeeded, issueReplay.Code);
        Assert.Equal(PurchaseOrderStatus.Issued, issueReplay.Value!.Status);
        Assert.Equal(issued.Value.Version, issueReplay.Value.Version);

        // Same key against a genuinely different payload must still be rejected rather than replayed.
        var mismatched = await fixture.Service.IssueAsync(fixture.Context(Approver, "tenant.procurement.purchase-order.issue"), approved.Value.Id, approved.Value.Version, "po-durable-issue", "fp-durable-issue-different");
        Assert.False(mismatched.Succeeded);
        Assert.Equal("idempotency_conflict", mismatched.Code);

        var history = (await fixture.Service.ReadHistoryAsync(fixture.Context(Requester, "tenant.procurement.purchase-order.history"), created.Value.Id)).Value!;
        Assert.Equal(1, CountAction(history, PurchaseOrderHistoryAction.Submitted));
        Assert.Equal(1, CountAction(history, PurchaseOrderHistoryAction.ApprovalRecorded));
        Assert.Equal(1, CountAction(history, PurchaseOrderHistoryAction.Issued));

        var audit = (await fixture.Service.ReadAuditAsync(fixture.Context(Requester, "tenant.procurement.purchase-order.audit"), created.Value.Id)).Value!;
        Assert.Equal(1, CountKey(audit, "po-durable-submit"));
        Assert.Equal(1, CountKey(audit, "po-durable-approve"));
        Assert.Equal(1, CountKey(audit, "po-durable-issue"));

        var current = await fixture.Service.GetAsync(fixture.Context(Requester, "tenant.procurement.purchase-order.view"), created.Value.Id);
        Assert.True(current.Succeeded, current.Code);
        Assert.NotEqual(issued.Value.Version, current.Value!.Version);
        Assert.Equal(PurchaseOrderStatus.PartiallyConfirmed, current.Value.Status);
    }

    [Fact]
    public async Task Replays_durable_confirmation_and_supplier_change_approval_after_the_order_left_the_eligible_state()
    {
        await using var fixture = await Fixture.CreateAsync();
        var issued = await fixture.IssuedOrderAsync("conf");
        var line = issued.Lines.Single();
        var responseDate = DateOnly.FromDateTime(DateTime.UtcNow.Date);

        var changeRequest = new PurchaseOrderConfirmationRequest(
            PurchaseOrderConfirmationStatus.PartiallyConfirmed,
            responseDate,
            "SUP-DURABLE-1",
            "supplier@test",
            null,
            null,
            [new PurchaseOrderConfirmationLineRequest(line.Id, 1m, null, 1m, 15m, responseDate.AddDays(10), "Supplier proposed a revised price and date.")],
            []);
        var changed = await fixture.Service.RecordConfirmationAsync(fixture.Context(Requester, "tenant.procurement.purchase-order.confirmation.capture"), issued.Id, changeRequest, issued.Version, "po-durable-change", "fp-durable-change");
        Assert.True(changed.Succeeded, changed.Code);
        Assert.Equal(PurchaseOrderStatus.ChangedPendingApproval, changed.Value!.Status);

        // ChangedPendingApproval is not an eligible state for a new confirmation, but the identical retry
        // must replay the original confirmation instead of failing with confirmation_not_allowed.
        var changeReplay = await fixture.Service.RecordConfirmationAsync(fixture.Context(Requester, "tenant.procurement.purchase-order.confirmation.capture"), issued.Id, changeRequest, issued.Version, "po-durable-change", "fp-durable-change");
        Assert.True(changeReplay.Succeeded, changeReplay.Code);
        Assert.Equal(PurchaseOrderStatus.ChangedPendingApproval, changeReplay.Value!.Status);
        Assert.Equal(changed.Value.Version, changeReplay.Value.Version);
        Assert.Single(changeReplay.Value.PendingChanges);

        var reapproved = await fixture.Service.ApproveSupplierChangeAsync(fixture.Context(Approver, "tenant.procurement.purchase-order.supplier-change.approve"), changed.Value.Id, changed.Value.Version, "po-durable-change-approve", "fp-durable-change-approve");
        Assert.True(reapproved.Succeeded, reapproved.Code);
        Assert.NotEqual(PurchaseOrderStatus.ChangedPendingApproval, reapproved.Value!.Status);
        Assert.Equal(15m, reapproved.Value.Lines.Single().UnitPrice);

        var rejectRequest = new PurchaseOrderConfirmationRequest(PurchaseOrderConfirmationStatus.Rejected, responseDate, "SUP-DURABLE-2", "supplier@test", "Supplier declined the order.", null, [], []);
        var rejected = await fixture.Service.RecordConfirmationAsync(fixture.Context(Requester, "tenant.procurement.purchase-order.confirmation.capture"), reapproved.Value.Id, rejectRequest, reapproved.Value.Version, "po-durable-reject", "fp-durable-reject");
        Assert.True(rejected.Succeeded, rejected.Code);
        Assert.Equal(PurchaseOrderStatus.Rejected, rejected.Value!.Status);

        // Rejected is terminal for confirmation capture; the identical retry must still replay.
        var rejectedReplay = await fixture.Service.RecordConfirmationAsync(fixture.Context(Requester, "tenant.procurement.purchase-order.confirmation.capture"), reapproved.Value.Id, rejectRequest, reapproved.Value.Version, "po-durable-reject", "fp-durable-reject");
        Assert.True(rejectedReplay.Succeeded, rejectedReplay.Code);
        Assert.Equal(PurchaseOrderStatus.Rejected, rejectedReplay.Value!.Status);
        Assert.Equal(rejected.Value.Version, rejectedReplay.Value.Version);

        // Both original successful responses must remain exact after later mutations: the confirmation
        // snapshot still contains its pending supplier change, and the approval snapshot still contains
        // the approved commercial values rather than the current rejected state.
        var changeReplayAfterMutation = await fixture.Service.RecordConfirmationAsync(fixture.Context(Requester, "tenant.procurement.purchase-order.confirmation.capture"), issued.Id, changeRequest, issued.Version, "po-durable-change", "fp-durable-change");
        Assert.True(changeReplayAfterMutation.Succeeded, changeReplayAfterMutation.Code);
        Assert.Equal(PurchaseOrderStatus.ChangedPendingApproval, changeReplayAfterMutation.Value!.Status);
        Assert.Single(changeReplayAfterMutation.Value.PendingChanges);
        Assert.Equal(15m, changeReplayAfterMutation.Value.PendingChanges.Single().ProposedUnitPrice);

        var reapprovalReplay = await fixture.Service.ApproveSupplierChangeAsync(fixture.Context(Approver, "tenant.procurement.purchase-order.supplier-change.approve"), changed.Value.Id, changed.Value.Version, "po-durable-change-approve", "fp-durable-change-approve");
        Assert.True(reapprovalReplay.Succeeded, reapprovalReplay.Code);
        Assert.Equal(reapproved.Value.Version, reapprovalReplay.Value!.Version);
        Assert.Equal(PurchaseOrderStatus.Confirmed, reapprovalReplay.Value.Status);
        Assert.Equal(15m, reapprovalReplay.Value.Lines.Single().UnitPrice);

        var confirmations = (await fixture.Service.ReadConfirmationsAsync(fixture.Context(Requester, "tenant.procurement.purchase-order.confirmation.view"), issued.Id)).Value!;
        Assert.Equal(2, confirmations.Count);
        Assert.Equal(1, confirmations.Sum(item => item.Changes.Count));

        var history = (await fixture.Service.ReadHistoryAsync(fixture.Context(Requester, "tenant.procurement.purchase-order.history"), issued.Id)).Value!;
        Assert.Equal(1, CountAction(history, PurchaseOrderHistoryAction.SupplierChangeProposed));
        Assert.Equal(1, CountAction(history, PurchaseOrderHistoryAction.SupplierChangeApproved));
        Assert.Equal(1, CountAction(history, PurchaseOrderHistoryAction.SupplierRejected));

        var audit = (await fixture.Service.ReadAuditAsync(fixture.Context(Requester, "tenant.procurement.purchase-order.audit"), issued.Id)).Value!;
        Assert.Equal(1, CountKey(audit, "po-durable-change"));
        Assert.Equal(1, CountKey(audit, "po-durable-change-approve"));
        Assert.Equal(1, CountKey(audit, "po-durable-reject"));
    }

    [Fact]
    public async Task Replays_durable_supplier_change_rejection_after_the_order_left_changed_pending_approval()
    {
        await using var fixture = await Fixture.CreateAsync();
        var issued = await fixture.IssuedOrderAsync("chg-reject");
        var line = issued.Lines.Single();
        var responseDate = DateOnly.FromDateTime(DateTime.UtcNow.Date);

        var changed = await fixture.Service.RecordConfirmationAsync(
            fixture.Context(Requester, "tenant.procurement.purchase-order.confirmation.capture"),
            issued.Id,
            new PurchaseOrderConfirmationRequest(PurchaseOrderConfirmationStatus.PartiallyConfirmed, responseDate, "SUP-DURABLE-3", "supplier@test", null, null, [new PurchaseOrderConfirmationLineRequest(line.Id, 1m, null, 1m, 15m, responseDate.AddDays(10), "Supplier proposed a revised price and date.")], []),
            issued.Version,
            "po-durable-change-2",
            "fp-durable-change-2");
        Assert.True(changed.Succeeded, changed.Code);
        Assert.Equal(PurchaseOrderStatus.ChangedPendingApproval, changed.Value!.Status);

        var rejectedChange = await fixture.Service.RejectSupplierChangeAsync(fixture.Context(Approver, "tenant.procurement.purchase-order.supplier-change.reject"), changed.Value.Id, changed.Value.Version, "The proposed price increase is not accepted.", "po-durable-change-reject", "fp-durable-change-reject");
        Assert.True(rejectedChange.Succeeded, rejectedChange.Code);
        Assert.NotEqual(PurchaseOrderStatus.ChangedPendingApproval, rejectedChange.Value!.Status);
        Assert.Equal(12.5m, rejectedChange.Value.Lines.Single().UnitPrice);

        var rejectionReplay = await fixture.Service.RejectSupplierChangeAsync(fixture.Context(Approver, "tenant.procurement.purchase-order.supplier-change.reject"), changed.Value.Id, changed.Value.Version, "The proposed price increase is not accepted.", "po-durable-change-reject", "fp-durable-change-reject");
        Assert.True(rejectionReplay.Succeeded, rejectionReplay.Code);
        Assert.Equal(rejectedChange.Value.Version, rejectionReplay.Value!.Version);
        Assert.Equal(12.5m, rejectionReplay.Value.Lines.Single().UnitPrice);

        var history = (await fixture.Service.ReadHistoryAsync(fixture.Context(Requester, "tenant.procurement.purchase-order.history"), issued.Id)).Value!;
        Assert.Equal(1, CountAction(history, PurchaseOrderHistoryAction.SupplierChangeRejected));
        var audit = (await fixture.Service.ReadAuditAsync(fixture.Context(Requester, "tenant.procurement.purchase-order.audit"), issued.Id)).Value!;
        Assert.Equal(1, CountKey(audit, "po-durable-change-reject"));
        var confirmations = (await fixture.Service.ReadConfirmationsAsync(fixture.Context(Requester, "tenant.procurement.purchase-order.confirmation.view"), issued.Id)).Value!;
        Assert.Equal(1, confirmations.Sum(item => item.Changes.Count));
        Assert.Equal(PurchaseOrderSupplierChangeStatus.Rejected, confirmations.Single().Changes.Single().Status);
    }

    [Fact]
    public async Task Replays_durable_create_after_source_state_drift_without_weakening_new_create_validation()
    {
        await using var fixture = await Fixture.CreateAsync();
        var source = Assert.Single((await fixture.Service.ListSourceOptionsAsync(fixture.Context(Requester, "tenant.procurement.purchase-order.view"))).Value!);
        var created = await fixture.Service.CreateAsync(fixture.Context(Requester, "tenant.procurement.purchase-order.create"), new PurchaseOrderCreateRequest(source.Source.SourceDecisionId), "po-durable-create-drift", "fp-durable-create-drift");
        Assert.True(created.Succeeded, created.Code);

        // The sourcing state moves on after the original create succeeded.
        await fixture.DisqualifySourceQuotationAsync();

        var replay = await fixture.Service.CreateAsync(fixture.Context(Requester, "tenant.procurement.purchase-order.create"), new PurchaseOrderCreateRequest(source.Source.SourceDecisionId), "po-durable-create-drift", "fp-durable-create-drift");
        Assert.True(replay.Succeeded, replay.Code);
        Assert.Equal(created.Value!.Id, replay.Value!.Id);
        Assert.Equal(created.Value.Version, replay.Value.Version);

        // Same key with a different payload fingerprint is a conflict, never a silent replay.
        var mismatched = await fixture.Service.CreateAsync(fixture.Context(Requester, "tenant.procurement.purchase-order.create"), new PurchaseOrderCreateRequest(source.Source.SourceDecisionId), "po-durable-create-drift", "fp-durable-create-drift-2");
        Assert.False(mismatched.Succeeded);
        Assert.Equal("idempotency_conflict", mismatched.Code);

        // A genuinely new create still runs full current source validation and fails closed.
        var freshCreate = await fixture.Service.CreateAsync(fixture.Context(Requester, "tenant.procurement.purchase-order.create"), new PurchaseOrderCreateRequest(source.Source.SourceDecisionId), "po-durable-create-fresh", "fp-durable-create-fresh");
        Assert.False(freshCreate.Succeeded);
        Assert.Equal("purchase_order_duplicate", freshCreate.Code);

        var history = (await fixture.Service.ReadHistoryAsync(fixture.Context(Requester, "tenant.procurement.purchase-order.history"), created.Value.Id)).Value!;
        Assert.Equal(1, CountAction(history, PurchaseOrderHistoryAction.Created));
        var audit = (await fixture.Service.ReadAuditAsync(fixture.Context(Requester, "tenant.procurement.purchase-order.audit"), created.Value.Id)).Value!;
        Assert.Equal(1, CountKey(audit, "po-durable-create-drift"));
        var all = (await fixture.Service.ListAsync(fixture.Context(Requester, "tenant.procurement.purchase-order.view"), null)).Value!;
        Assert.Single(all);
    }

    private static int CountAction(IReadOnlyList<PurchaseOrderHistoryRecord> history, PurchaseOrderHistoryAction action) =>
        history.Count(item => item.Action == action);

    private static int CountKey(IReadOnlyList<PurchaseOrderAuditRecord> audit, string idempotencyKey) =>
        audit.Count(item => item.IdempotencyKey == idempotencyKey);

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
        public DbContextOptions Options => options;

        public static async Task<Fixture> CreateAsync(
            PurchaseRequestApprovalPolicyDefinition? approvalPolicy = null,
            IPurchaseRequestApprovalDelegationProvider? approvalDelegationProvider = null)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder().UseSqlite(connection).Options;
            await using (var db = new ProcurementDbContext(options, CreateTenantContext(TenantA, Requester)))
            {
                await db.Database.EnsureCreatedAsync();
                await SeedAsync(db);
            }

            IPurchaseRequestApprovalPolicyProvider policyProvider = approvalPolicy is null
                ? new DefaultPurchaseRequestApprovalPolicyProvider()
                : new ConfiguredPurchaseRequestApprovalPolicyProvider(
                    [new PurchaseRequestApprovalPolicyBinding(
                        new PurchaseRequestScope(TenantA, CompanyA, BranchA),
                        approvalPolicy)]);
            var service = new PurchaseOrderService(
                new PurchaseRequestAuthorizationService(),
                new PurchaseOrderPersistence(options),
                new PurchaseRequestPersistence(options),
                new SupplierQuotationPersistence(options),
                policyProvider,
                approvalDelegationProvider ?? new NoPurchaseRequestApprovalDelegationProvider());
            return new Fixture(connection, options, service);
        }

        public ProcurementRequestContext Context(Guid actor, string operation, Guid tenantId = default)
        {
            tenantId = tenantId == Guid.Empty ? TenantA : tenantId;
            var foundation = FoundationRequestContext.ForTenant(actor, Guid.NewGuid(), CreateTenantContext(tenantId, actor), operation);
            var resolved = new ProcurementTenantContextResolver().Resolve(foundation);
            return Assert.IsType<ProcurementRequestContext>(resolved.Context);
        }

        public async Task<PurchaseOrderRecord> PendingApprovalAsync(string keyPrefix)
        {
            var source = Assert.Single((await Service.ListSourceOptionsAsync(Context(Requester, "tenant.procurement.purchase-order.view"))).Value!);
            var created = await Service.CreateAsync(
                Context(Requester, "tenant.procurement.purchase-order.create"),
                new PurchaseOrderCreateRequest(source.Source.SourceDecisionId),
                $"po-{keyPrefix}-create",
                $"fp-po-{keyPrefix}-create");
            Assert.True(created.Succeeded, created.Code);
            var submitted = await Service.SubmitAsync(
                Context(Requester, "tenant.procurement.purchase-order.submit"),
                created.Value!.Id,
                created.Value.Version,
                $"po-{keyPrefix}-submit",
                $"fp-po-{keyPrefix}-submit");
            Assert.True(submitted.Succeeded, submitted.Code);
            return submitted.Value!;
        }

        /// <summary>
        /// Drives the seeded source decision through create, submit, approve, and issue so a durable
        /// replay test can start from a genuinely Issued Purchase Order.
        /// </summary>
        public async Task<PurchaseOrderRecord> IssuedOrderAsync(string keyPrefix)
        {
            var source = Assert.Single((await Service.ListSourceOptionsAsync(Context(Requester, "tenant.procurement.purchase-order.view"))).Value!);
            var created = await Service.CreateAsync(Context(Requester, "tenant.procurement.purchase-order.create"), new PurchaseOrderCreateRequest(source.Source.SourceDecisionId), $"{keyPrefix}-create", $"fp-{keyPrefix}-create");
            Assert.True(created.Succeeded, created.Code);
            var submitted = await Service.SubmitAsync(Context(Requester, "tenant.procurement.purchase-order.submit"), created.Value!.Id, created.Value.Version, $"{keyPrefix}-submit", $"fp-{keyPrefix}-submit");
            Assert.True(submitted.Succeeded, submitted.Code);
            var approved = await Service.ApproveAsync(Context(Approver, "tenant.procurement.purchase-order.approve"), submitted.Value!.Id, submitted.Value.Version, $"{keyPrefix}-approve", $"fp-{keyPrefix}-approve");
            Assert.True(approved.Succeeded, approved.Code);
            var issued = await Service.IssueAsync(Context(Approver, "tenant.procurement.purchase-order.issue"), approved.Value!.Id, approved.Value.Version, $"{keyPrefix}-issue", $"fp-{keyPrefix}-issue");
            Assert.True(issued.Succeeded, issued.Code);
            return issued.Value!;
        }

        public async Task<PurchaseOrderRecord> ChangedPendingApprovalAsync(string keyPrefix)
        {
            var issued = await IssuedOrderAsync(keyPrefix);
            var line = issued.Lines.Single();
            var changed = await Service.RecordConfirmationAsync(
                Context(Requester, "tenant.procurement.purchase-order.confirmation.capture"),
                issued.Id,
                new PurchaseOrderConfirmationRequest(
                    PurchaseOrderConfirmationStatus.Confirmed,
                    DateOnly.FromDateTime(DateTime.UtcNow.Date),
                    $"SUP-{keyPrefix}",
                    "supplier@test",
                    null,
                    null,
                    [new PurchaseOrderConfirmationLineRequest(line.Id, line.OrderedQuantity, null, null, 15m, null, "Supplier proposed a revised price.")],
                    []),
                issued.Version,
                $"{keyPrefix}-change",
                $"fp-{keyPrefix}-change");
            Assert.True(changed.Succeeded, changed.Code);
            Assert.Equal(PurchaseOrderStatus.ChangedPendingApproval, changed.Value!.Status);
            return changed.Value!;
        }

        public async Task<PurchaseOrderSourceOptionRecord> AddSecondSourceAsync()
        {
            await using var db = new ProcurementDbContext(options, CreateTenantContext(TenantA, Requester));
            var now = DateTimeOffset.UtcNow;
            var scope = new PurchaseRequestScope(TenantA, CompanyA, BranchA);
            var requestId = Guid.NewGuid();
            var requestLineId = Guid.NewGuid();
            var quotationId = Guid.NewGuid();
            var quotationLineId = Guid.NewGuid();
            var decisionId = Guid.NewGuid();
            var policy = new PurchaseRequestApprovalPolicyDefinition("procurement.purchase-order.test", 1, [new PurchaseRequestApprovalStageDefinition("manager", 1, 1, [], false)], true, now.AddDays(-1));
            var request = new PurchaseRequestEntity(requestId, new TenantId(TenantA), CompanyA, BranchA, Requester, "Second approved demand", now);
            request.Lines.Add(new PurchaseRequestLineEntity(requestLineId, new TenantId(TenantA), request.Id, new PurchaseRequestLineSnapshot(requestLineId, Product, "SKU-001", "Test Product", Unit, "EA", 2m, DateOnly.FromDateTime(now.UtcDateTime.Date.AddDays(7)), "Second approved demand line")));
            request.Submit(policy, JsonSerializer.Serialize(policy), now);
            request.RecordApproval(PurchaseRequestStatus.Approved, 0, 0, "[]", now);
            request.TouchVersion();

            var quotationLine = new SupplierQuotationLineSnapshot(quotationLineId, requestLineId, Product, "SKU-001", "Test Product", Unit, "EA", 2m, 2m, 12.5m, null, null, null, null, null, null, null, null, DateOnly.FromDateTime(now.UtcDateTime.Date.AddDays(7)), DateOnly.FromDateTime(now.UtcDateTime.Date.AddDays(9)), "9 days", "Second quoted line");
            var supplier = new SupplierQuotationSupplierSnapshot(Supplier, "SUP-A", "Alpha Supplier");
            var currency = new SupplierQuotationCurrencySnapshot(Currency, "USD", "US Dollar");
            var quotationCommand = new SupplierQuotationCreateCommand(quotationId, request.Id, scope, Requester, supplier, "Q-PO-2", DateOnly.FromDateTime(now.UtcDateTime.Date), DateOnly.FromDateTime(now.UtcDateTime.Date.AddDays(14)), currency, null, "Delivered", DateOnly.FromDateTime(now.UtcDateTime.Date.AddDays(9)), "9 days", "Second seed quote", [quotationLine], [], now, "second-seed");
            var quotation = new SupplierQuotationEntity(quotationCommand, new TenantId(TenantA));
            quotation.Lines.Add(new SupplierQuotationLineEntity(new TenantId(TenantA), quotationId, request.Id, quotationLine));
            quotation.SetStatus(SupplierQuotationStatus.Submitted, now);
            quotation.TouchVersion();
            var quotationRecord = new SupplierQuotationRecord(quotationId, TenantA, request.Id, scope, Requester, supplier, SupplierQuotationStatus.Submitted, "Q-PO-2", quotationCommand.OfferDate, quotationCommand.ValidUntil, currency, null, quotationCommand.DeliveryTerms, quotationCommand.OfferedDeliveryDate, quotationCommand.OfferedDeliveryLeadTime, quotationCommand.Notes, [quotationLine], [], now, now, now, quotation.Version);
            var decisionCommand = new SupplierSourceDecisionCommand(decisionId, request.Id, scope, quotationId, Requester, now, "Selected for second test", null, null, null, "sha256:test-second", "{}", request.Version, "second-seed-decision");
            var decision = new SupplierSourceDecisionEntity(decisionCommand, new TenantId(TenantA), quotationRecord);
            decision.TouchVersion();

            db.PurchaseRequests.Add(request);
            db.SupplierQuotations.Add(quotation);
            db.SupplierSourceDecisions.Add(decision);
            await db.SaveChangesAsync();

            var sources = await Service.ListSourceOptionsAsync(Context(Requester, "tenant.procurement.purchase-order.view"));
            Assert.True(sources.Succeeded, sources.Code);
            return Assert.Single(sources.Value!, item => item.Source.SourceDecisionId == decisionId);
        }

        /// <summary>
        /// Moves the seeded sourcing state on after a Purchase Order was already created, so a durable
        /// replay can be distinguished from a genuinely new create that must still fail current validation.
        /// </summary>
        public async Task DisqualifySourceQuotationAsync()
        {
            await using var db = new ProcurementDbContext(options, CreateTenantContext(TenantA, Requester));
            var quotation = await db.SupplierQuotations.SingleAsync();
            quotation.SetStatus(SupplierQuotationStatus.Disqualified, DateTimeOffset.UtcNow);
            quotation.TouchVersion();
            await db.SaveChangesAsync();
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
