using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MiniErp.App.BuildingBlocks.Rest;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.Modules.Inventory;
using MiniErp.App.Modules.Procurement;
using MiniErp.Contracts.Modules.Foundation;
using MiniErp.Contracts.Modules.Inventory;
using MiniErp.Contracts.Modules.Procurement;
using MiniErp.Infrastructure.Persistence.Modules.Inventory;
using Xunit;

namespace MiniErp.ArchitectureTests;

public sealed class InventoryStockControlTests
{
    private static readonly Guid TenantA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid TenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid CompanyA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid WarehouseA = Guid.Parse("cccccccc-1111-1111-1111-111111111111");
    private static readonly Guid ProductA = Guid.Parse("77777777-7777-7777-7777-777777777777");
    private static readonly Guid UnitA = Guid.Parse("88888888-8888-8888-8888-888888888888");
    private static readonly Guid ProductB = Guid.Parse("77777777-7777-7777-7777-777777777778");
    private static readonly Guid UnitB = Guid.Parse("88888888-8888-8888-8888-888888888889");
    private static readonly Guid Actor = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid Actor2 = Guid.Parse("44444444-4444-4444-4444-444444444445");
    private static readonly Guid Actor3 = Guid.Parse("44444444-4444-4444-4444-444444444446");
    private static readonly Guid Actor4 = Guid.Parse("44444444-4444-4444-4444-444444444447");

    [Fact]
    public async Task Adjustment_posting_is_tenant_scoped_immutable_and_durablely_replayable()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder().UseSqlite(connection).Options;
        var context = Context(TenantA);
        await EnsureCreatedAsync(options, context);
        var persistence = new InventoryPersistence(options);
        var at = DateTimeOffset.UtcNow;
        var reasonId = Guid.NewGuid();
        var reasonCommand = new InventoryReasonCodeCommand(
            reasonId, "DAMAGE", "Damage", "تلف", InventoryReasonCategory.Adjustment,
            Actor, at, "reason-correlation", "reason-key", "reason-fingerprint");

        var reason = Assert.IsType<InventoryReasonCodeRecord>(await persistence.CreateReasonCodeAsync(context, reasonCommand));
        var reasonReplay = Assert.IsType<InventoryReasonCodeRecord>(await persistence.CreateReasonCodeAsync(context, reasonCommand));
        Assert.Equal(reason.Id, reasonReplay.Id);

        var scope = new InventoryScope(TenantA, CompanyA, null, WarehouseA);
        var create = new InventoryAdjustmentCreateCommand(
            Guid.NewGuid(), scope, "WH-A", "Warehouse A", "cycle correction",
            [new InventoryAdjustmentLineCommand(
                Guid.NewGuid(), ProductA, UnitA, InventoryAdjustmentDirection.Increase, 3m, "", "evidence-1", Product(), reason)],
            Actor, at, "adjustment-correlation", "adjustment-key", "adjustment-fingerprint");
        var draft = Assert.IsType<InventoryAdjustmentRecord>(await persistence.CreateAdjustmentAsync(context, create));
        var submitted = Assert.IsType<InventoryAdjustmentRecord>(await persistence.SubmitAdjustmentAsync(
            context,
            Action(draft.Id, draft.Version, "submit-key", "submit-fingerprint", at),
            requiresApproval: false,
            policyJson: null));
        var posted = Assert.IsType<InventoryAdjustmentRecord>(await persistence.PostAdjustmentAsync(
            context,
            Action(submitted.Id, submitted.Version, "post-key", "post-fingerprint", at)));
        var replayed = Assert.IsType<InventoryAdjustmentRecord>(await persistence.PostAdjustmentAsync(
            context,
            Action(submitted.Id, submitted.Version, "post-key", "post-fingerprint", at)));

        Assert.Equal(InventoryControlDocumentStatus.Posted, posted.Status);
        Assert.Equal(posted.Id, replayed.Id);
        var movement = Assert.Single(await persistence.ListMovementsAsync(context, scope));
        Assert.Equal(InventoryMovementSourceType.StockAdjustment, movement.SourceType);
        Assert.Equal(InventoryValuationStatus.Pending, movement.ValuationStatus);
        Assert.Equal(posted.Id, movement.SourceDocumentId);

        var tenantBReasons = await persistence.ListReasonCodesAsync(Context(TenantB), InventoryReasonCategory.Adjustment);
        var tenantBAdjustments = await persistence.ListAdjustmentsAsync(Context(TenantB));
        Assert.Empty(tenantBReasons);
        Assert.Empty(tenantBAdjustments);
    }

    [Fact]
    public async Task Stock_issue_blocks_negative_available_stock_and_correction_is_limited_to_control_movements()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder().UseSqlite(connection).Options;
        var context = Context(TenantA);
        var scope = new InventoryScope(TenantA, CompanyA, null, WarehouseA);
        await EnsureCreatedAsync(options, context);
        await SeedStockAsync(options, context, 2m);
        var persistence = new InventoryPersistence(options);
        var at = DateTimeOffset.UtcNow;
        var reason = Assert.IsType<InventoryReasonCodeRecord>(await persistence.CreateReasonCodeAsync(
            context,
            new InventoryReasonCodeCommand(Guid.NewGuid(), "ISSUE", "Internal issue", "صرف داخلي", InventoryReasonCategory.StockIssue, Actor, at, "issue-reason", "issue-reason-key", "issue-reason-fp")));

        var blockedCreate = await persistence.CreateStockIssueAsync(context, IssueCreate(Guid.NewGuid(), 3m, reason, at, "blocked"));
        var blockedDraft = Assert.IsType<InventoryStockIssueRecord>(blockedCreate);
        var blockedSubmitted = Assert.IsType<InventoryStockIssueRecord>(await persistence.SubmitStockIssueAsync(
            context, Action(blockedDraft.Id, blockedDraft.Version, "blocked-submit", "blocked-submit-fp", at), false, null));
        Assert.Null(await persistence.PostStockIssueAsync(
            context, Action(blockedSubmitted.Id, blockedSubmitted.Version, "blocked-post", "blocked-post-fp", at)));

        var create = Assert.IsType<InventoryStockIssueRecord>(await persistence.CreateStockIssueAsync(context, IssueCreate(Guid.NewGuid(), 1m, reason, at, "production use")));
        var submitted = Assert.IsType<InventoryStockIssueRecord>(await persistence.SubmitStockIssueAsync(
            context, Action(create.Id, create.Version, "issue-submit", "issue-submit-fp", at), false, null));
        var posted = Assert.IsType<InventoryStockIssueRecord>(await persistence.PostStockIssueAsync(
            context, Action(submitted.Id, submitted.Version, "issue-post", "issue-post-fp", at)));
        var issueMovement = Assert.Single(await persistence.ListMovementsAsync(context, scope), item => item.SourceType == InventoryMovementSourceType.StockIssue);

        var correction = Assert.IsType<InventoryMovementRecord>(await persistence.CorrectMovementAsync(
            context,
            new InventoryMovementCorrectionCommand(
                issueMovement.Id, issueMovement.Version, Actor, reason.Id, reason.Code, reason.EnglishName, reason.ArabicName,
                "corrected issue", "correction-correlation", "correction-key", "correction-fingerprint", at)));
        var correctionReplay = Assert.IsType<InventoryMovementRecord>(await persistence.CorrectMovementAsync(
            context,
            new InventoryMovementCorrectionCommand(
                issueMovement.Id, issueMovement.Version, Actor, reason.Id, reason.Code, reason.EnglishName, reason.ArabicName,
                "corrected issue", "correction-correlation", "correction-key", "correction-fingerprint", at)));

        Assert.Equal(InventoryMovementSourceType.Correction, correction.SourceType);
        Assert.Equal(issueMovement.Id, correction.CorrectionOfMovementId);
        Assert.Equal(correction.Id, correctionReplay.Id);
        Assert.Equal(3, (await persistence.ListMovementsAsync(context, scope)).Count);
        Assert.Null(await persistence.CorrectMovementAsync(
            context,
            new InventoryMovementCorrectionCommand(
                issueMovement.Id, issueMovement.Version, Actor2, reason.Id, reason.Code, reason.EnglishName, reason.ArabicName,
                "different request must not create a second correction", "correction-race-correlation", "correction-race-key", "correction-race-fp", at)));
        Assert.Equal(3, (await persistence.ListMovementsAsync(context, scope)).Count);
        Assert.Null(await persistence.CorrectMovementAsync(
            context,
            new InventoryMovementCorrectionCommand(
                (await persistence.ListMovementsAsync(context, scope)).Single(item => item.SourceType == InventoryMovementSourceType.OpeningBalance).Id,
                (await persistence.ListMovementsAsync(context, scope)).Single(item => item.SourceType == InventoryMovementSourceType.OpeningBalance).Version,
                Actor, reason.Id, reason.Code, reason.EnglishName, reason.ArabicName, "opening must remain immutable",
                "opening-correction-correlation", "opening-correction-key", "opening-correction-fp", at)));
        Assert.Equal(InventoryControlDocumentStatus.Posted, posted.Status);
    }

    [Fact]
    public async Task Count_snapshot_is_server_authoritative_blind_for_counter_and_posts_variance_as_pending_valuation()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder().UseSqlite(connection).Options;
        var context = Context(TenantA);
        var scope = new InventoryScope(TenantA, CompanyA, null, WarehouseA);
        await EnsureCreatedAsync(options, context);
        await SeedStockAsync(options, context, 5m);
        var persistence = new InventoryPersistence(options);
        var at = DateTimeOffset.UtcNow;
        var reviewer = Guid.NewGuid();
        var reason = Assert.IsType<InventoryReasonCodeRecord>(await persistence.CreateReasonCodeAsync(
            context,
            new InventoryReasonCodeCommand(Guid.NewGuid(), "COUNT-VAR", "Count variance", "فرق جرد", InventoryReasonCategory.CountVariance, Actor, at, "count-reason", "count-reason-key", "count-reason-fp")));
        var countId = Guid.NewGuid();
        var created = Assert.IsType<InventoryCountRecord>(await persistence.CreateCountAsync(
            context,
            new InventoryCountCreateCommand(
                countId, scope, "WH-A", "Warehouse A", InventoryCountType.Cycle, Actor, reviewer,
                [new InventoryCountLineCommand(Guid.NewGuid(), null, 1, ProductA, UnitA, "", 999m, Product())],
                at, Actor, at, "count-correlation", "count-key", "count-fingerprint")));
        Assert.Equal(5m, Assert.Single(created.Lines).ExpectedQuantity);
        Assert.Null(Assert.Single((await persistence.FindCountAsync(context, created.Id, includeExpected: false))!.Lines).ExpectedQuantity);

        var line = Assert.Single(created.Lines);
        var submitted = Assert.IsType<InventoryCountRecord>(await persistence.SubmitCountAsync(
            context,
            new InventoryCountSubmitCommand(
                created.Id, created.Version,
                [new InventoryCountObservationRequest(line.Id, 4m)], Actor, "count-submit-key", "count-submit-fp", "count-submit-correlation", at)));
        Assert.Equal(InventoryControlDocumentStatus.PendingApproval, submitted.Status);
        var reasoned = Assert.IsType<InventoryCountRecord>(await persistence.RecordCountVarianceReasonAsync(
            context,
            new InventoryCountVarianceReasonCommand(submitted.Id, submitted.Version, line.Id, reason.Code, reviewer, "count-reason-key-2", "count-reason-fp-2", "count-reason-correlation-2", at)));
        var approved = Assert.IsType<InventoryCountRecord>(await persistence.ApproveCountAsync(
            context, Action(reasoned.Id, reasoned.Version, "count-approve-key", "count-approve-fp", at, reviewer)));
        var posted = Assert.IsType<InventoryCountRecord>(await persistence.PostCountAsync(
            context, Action(approved.Id, approved.Version, "count-post-key", "count-post-fp", at)));
        var movement = Assert.Single(await persistence.ListMovementsAsync(context, scope), item => item.SourceType == InventoryMovementSourceType.InventoryCountVariance);

        Assert.Equal(InventoryControlDocumentStatus.Posted, posted.Status);
        Assert.Equal(InventoryMovementDirection.Outbound, movement.Direction);
        Assert.Equal(InventoryValuationStatus.Pending, movement.ValuationStatus);
        Assert.Equal(1m, movement.Quantity);
    }

    [Fact]
    public async Task Adjustment_and_issue_approval_persist_distinct_multi_stage_state()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder().UseSqlite(connection).Options;
        var context = Context(TenantA);
        var scope = new InventoryScope(TenantA, CompanyA, null, WarehouseA);
        await EnsureCreatedAsync(options, context);
        var persistence = new InventoryPersistence(options);
        var at = DateTimeOffset.UtcNow;
        var reason = Assert.IsType<InventoryReasonCodeRecord>(await persistence.CreateReasonCodeAsync(context,
            new InventoryReasonCodeCommand(Guid.NewGuid(), "DAMAGE", "Damage", "Ù„Ù„", InventoryReasonCategory.Adjustment, Actor, at, "approval-reason", "approval-reason-key", "approval-reason-fp")));
        var policy = JsonSerializer.Serialize(new PurchaseRequestApprovalPolicyDefinition(
            "inventory-two-stage", 1,
            [new PurchaseRequestApprovalStageDefinition("stage-1", 1, 1), new PurchaseRequestApprovalStageDefinition("stage-2", 2, 1)],
            false, at));

        var adjustment = Assert.IsType<InventoryAdjustmentRecord>(await persistence.CreateAdjustmentAsync(context,
            new InventoryAdjustmentCreateCommand(Guid.NewGuid(), scope, "WH-A", "Warehouse A", "two-stage",
                [new InventoryAdjustmentLineCommand(Guid.NewGuid(), ProductA, UnitA, InventoryAdjustmentDirection.Increase, 1m, "", null, Product(), reason)], Actor, at, "approval-adjustment", "approval-adjustment-key", "approval-adjustment-fp")));
        var pending = Assert.IsType<InventoryAdjustmentRecord>(await persistence.SubmitAdjustmentAsync(context, Action(adjustment.Id, adjustment.Version, "approval-submit", "approval-submit-fp", at), true, policy));
        var stageOne = Assert.IsType<InventoryAdjustmentRecord>(await persistence.ApproveAdjustmentAsync(context, Action(pending.Id, pending.Version, "approval-stage-one", "approval-stage-one-fp", at, Actor2)));
        Assert.Equal(InventoryControlDocumentStatus.PendingApproval, stageOne.Status);
        Assert.Equal(1, stageOne.Approval!.StageIndex);
        var approved = Assert.IsType<InventoryAdjustmentRecord>(await persistence.ApproveAdjustmentAsync(context, Action(stageOne.Id, stageOne.Version, "approval-stage-two", "approval-stage-two-fp", at, Guid.NewGuid())));
        Assert.Equal(InventoryControlDocumentStatus.Approved, approved.Status);

        var issueReason = Assert.IsType<InventoryReasonCodeRecord>(await persistence.CreateReasonCodeAsync(context,
            new InventoryReasonCodeCommand(Guid.NewGuid(), "USE", "Use", "Ø§Ø³ØªØ®Ø¯Ø§Ù…", InventoryReasonCategory.StockIssue, Actor, at, "issue-approval-reason", "issue-approval-reason-key", "issue-approval-reason-fp")));
        var issue = Assert.IsType<InventoryStockIssueRecord>(await persistence.CreateStockIssueAsync(context, IssueCreate(Guid.NewGuid(), 1m, issueReason, at, "two-stage issue")));
        var issuePending = Assert.IsType<InventoryStockIssueRecord>(await persistence.SubmitStockIssueAsync(context, Action(issue.Id, issue.Version, "issue-approval-submit", "issue-approval-submit-fp", at), true, policy));
        var issueStageOne = Assert.IsType<InventoryStockIssueRecord>(await persistence.ApproveStockIssueAsync(context, Action(issuePending.Id, issuePending.Version, "issue-stage-one", "issue-stage-one-fp", at, Actor2)));
        Assert.Equal(InventoryControlDocumentStatus.PendingApproval, issueStageOne.Status);
        var issueApproved = Assert.IsType<InventoryStockIssueRecord>(await persistence.ApproveStockIssueAsync(context, Action(issueStageOne.Id, issueStageOne.Version, "issue-stage-two", "issue-stage-two-fp", at, Guid.NewGuid())));
        Assert.Equal(InventoryControlDocumentStatus.Approved, issueApproved.Status);
    }

    [Fact]
    public async Task Count_zero_variance_requires_a_clean_cutoff_before_posting()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder().UseSqlite(connection).Options;
        var context = Context(TenantA);
        var scope = new InventoryScope(TenantA, CompanyA, null, WarehouseA);
        await EnsureCreatedAsync(options, context);
        await SeedStockAsync(options, context, 5m);
        var persistence = new InventoryPersistence(options);
        var at = DateTimeOffset.UtcNow;
        var created = Assert.IsType<InventoryCountRecord>(await persistence.CreateCountAsync(context,
            new InventoryCountCreateCommand(Guid.NewGuid(), scope, "WH-A", "Warehouse A", InventoryCountType.Cycle, Actor, null,
                [new InventoryCountLineCommand(Guid.NewGuid(), null, 1, ProductA, UnitA, "", 0m, Product())], at, Actor, at, "zero-count", "zero-count-key", "zero-count-fp")));
        var line = Assert.Single(created.Lines);
        var submitted = Assert.IsType<InventoryCountRecord>(await persistence.SubmitCountAsync(context,
            new InventoryCountSubmitCommand(created.Id, created.Version, [new InventoryCountObservationRequest(line.Id, 5m)], Actor, "zero-submit", "zero-submit-fp", "zero-submit-correlation", at)));
        Assert.Equal(InventoryControlDocumentStatus.Submitted, submitted.Status);
        var posted = Assert.IsType<InventoryCountRecord>(await persistence.PostCountAsync(context, Action(submitted.Id, submitted.Version, "zero-post", "zero-post-fp", DateTimeOffset.UtcNow)));
        Assert.Equal(InventoryControlDocumentStatus.Posted, posted.Status);
        Assert.DoesNotContain(await persistence.ListMovementsAsync(context, scope), item => item.SourceType == InventoryMovementSourceType.InventoryCountVariance);
    }

    [Fact]
    public async Task Full_count_uses_the_warehouse_ledger_fence_when_movement_posted_at_is_earlier_than_cutoff()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder().UseSqlite(connection).Options;
        var context = Context(TenantA);
        var scope = new InventoryScope(TenantA, CompanyA, null, WarehouseA);
        await EnsureCreatedAsync(options, context);
        await SeedStockAsync(options, context, 5m);
        var persistence = new InventoryPersistence(options);

        var created = Assert.IsType<InventoryCountRecord>(await persistence.CreateCountAsync(
            context,
            new InventoryCountCreateCommand(Guid.NewGuid(), scope, "WH-A", "Warehouse A", InventoryCountType.Full, Actor, null,
                [new InventoryCountLineCommand(Guid.NewGuid(), null, 1, ProductA, UnitA, "", 0m, Product())],
                DateTimeOffset.UtcNow, Actor, DateTimeOffset.UtcNow, "full-fence-time-correlation", "full-fence-time-key", "full-fence-time-fp")));

        await SeedMovementAsync(options, context, ProductA, UnitA, "SKU-A", "Product A", 1m, "full-fence-time-movement", created.SnapshotCutoff.AddTicks(-1));
        var submitted = Assert.IsType<InventoryCountRecord>(await persistence.SubmitCountAsync(
            context,
            new InventoryCountSubmitCommand(created.Id, created.Version,
                [new InventoryCountObservationRequest(Assert.Single(created.Lines).Id, 5m)], Actor,
                "full-fence-time-submit", "full-fence-time-submit-fp", "full-fence-time-submit-correlation", DateTimeOffset.UtcNow)));
        var blocked = Assert.IsType<InventoryCountRecord>(await persistence.PostCountAsync(
            context, Action(submitted.Id, submitted.Version, "full-fence-time-post", "full-fence-time-post-fp", DateTimeOffset.UtcNow)));

        Assert.Equal(InventoryControlDocumentStatus.ResnapshotRequired, blocked.Status);
        Assert.DoesNotContain(await persistence.ListMovementsAsync(context, scope), item => item.SourceType == InventoryMovementSourceType.InventoryCountVariance);
    }

    [Fact]
    public async Task Cycle_count_uses_the_selected_identity_fence_when_movement_posted_at_is_earlier_than_cutoff()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder().UseSqlite(connection).Options;
        var context = Context(TenantA);
        var scope = new InventoryScope(TenantA, CompanyA, null, WarehouseA);
        await EnsureCreatedAsync(options, context);
        await SeedStockAsync(options, context, 5m);
        var persistence = new InventoryPersistence(options);

        var created = Assert.IsType<InventoryCountRecord>(await persistence.CreateCountAsync(
            context,
            new InventoryCountCreateCommand(Guid.NewGuid(), scope, "WH-A", "Warehouse A", InventoryCountType.Cycle, Actor, null,
                [new InventoryCountLineCommand(Guid.NewGuid(), null, 1, ProductA, UnitA, "", 0m, Product())],
                DateTimeOffset.UtcNow, Actor, DateTimeOffset.UtcNow, "cycle-fence-time-correlation", "cycle-fence-time-key", "cycle-fence-time-fp")));

        await SeedMovementAsync(options, context, ProductA, UnitA, "SKU-A", "Product A", 1m, "cycle-fence-time-movement", created.SnapshotCutoff.AddTicks(-1));
        var line = Assert.Single(created.Lines);
        var submitted = Assert.IsType<InventoryCountRecord>(await persistence.SubmitCountAsync(
            context,
            new InventoryCountSubmitCommand(created.Id, created.Version,
                [new InventoryCountObservationRequest(line.Id, 5m)], Actor,
                "cycle-fence-time-submit", "cycle-fence-time-submit-fp", "cycle-fence-time-submit-correlation", DateTimeOffset.UtcNow)));
        var blocked = Assert.IsType<InventoryCountRecord>(await persistence.PostCountAsync(
            context, Action(submitted.Id, submitted.Version, "cycle-fence-time-post", "cycle-fence-time-post-fp", DateTimeOffset.UtcNow)));

        Assert.Equal(InventoryControlDocumentStatus.ResnapshotRequired, blocked.Status);
    }

    [Fact]
    public async Task Full_count_resnapshot_adds_new_warehouse_identity_and_preserves_prior_round()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder().UseSqlite(connection).Options;
        var context = Context(TenantA);
        var scope = new InventoryScope(TenantA, CompanyA, null, WarehouseA);
        await EnsureCreatedAsync(options, context);
        await SeedStockAsync(options, context, 5m);
        var persistence = new InventoryPersistence(options);
        var at = DateTimeOffset.UtcNow;
        var created = Assert.IsType<InventoryCountRecord>(await persistence.CreateCountAsync(context,
            new InventoryCountCreateCommand(Guid.NewGuid(), scope, "WH-A", "Warehouse A", InventoryCountType.Full, Actor, null,
                [new InventoryCountLineCommand(Guid.NewGuid(), null, 1, ProductA, UnitA, "", 0m, Product())], at, Actor, at, "full-count", "full-count-key", "full-count-fp")));
        await SeedMovementAsync(options, context, ProductB, UnitB, "SKU-B", "Product B", 2m, "full-new-identity");
        var submitted = Assert.IsType<InventoryCountRecord>(await persistence.SubmitCountAsync(context,
            new InventoryCountSubmitCommand(created.Id, created.Version, [new InventoryCountObservationRequest(created.Lines.Single().Id, 5m)], Actor, "full-submit", "full-submit-fp", "full-submit-correlation", DateTimeOffset.UtcNow)));
        var stale = Assert.IsType<InventoryCountRecord>(await persistence.PostCountAsync(context, Action(submitted.Id, submitted.Version, "full-post", "full-post-fp", DateTimeOffset.UtcNow)));
        Assert.Equal(InventoryControlDocumentStatus.ResnapshotRequired, stale.Status);
        var resnapshot = Assert.IsType<InventoryCountRecord>(await persistence.ResnapshotCountAsync(context, Action(stale.Id, stale.Version, "full-resnapshot", "full-resnapshot-fp", DateTimeOffset.UtcNow)));
        Assert.True(resnapshot.CurrentRoundGeneration > created.CurrentRoundGeneration);
        Assert.Contains(resnapshot.Lines, item => item.IsCurrentRound && item.ProductId == ProductB && item.ExpectedQuantity == 2m);
        Assert.Contains(resnapshot.Lines, item => !item.IsCurrentRound && item.ProductId == ProductA && item.CountedQuantity == 5m);

        await using var verifyDb = new InventoryDbContext(options, context.TenantContext);
        var snapshots = await verifyDb.CountSnapshots.AsNoTracking().Where(item => item.CountId == created.Id).OrderBy(item => item.RoundGeneration).ToListAsync();
        Assert.Equal(2, snapshots.Count);
        Assert.Equal(1L, snapshots[0].SnapshotWarehouseMovementCount!.Value);
        Assert.Equal(2L, snapshots[1].SnapshotWarehouseMovementCount!.Value);
    }

    [Fact]
    public async Task Full_count_creation_discovers_existing_ledger_identities_inside_the_persistence_snapshot()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder().UseSqlite(connection).Options;
        var context = Context(TenantA);
        var scope = new InventoryScope(TenantA, CompanyA, null, WarehouseA);
        await EnsureCreatedAsync(options, context);
        await SeedStockAsync(options, context, 5m);
        await SeedMovementAsync(options, context, ProductB, UnitB, "SKU-B", "Product B", 2m, "full-discovery");
        var persistence = new InventoryPersistence(options);

        var created = Assert.IsType<InventoryCountRecord>(await persistence.CreateCountAsync(
            context,
            new InventoryCountCreateCommand(
                Guid.NewGuid(), scope, "WH-A", "Warehouse A", InventoryCountType.Full, Actor, null,
                [new InventoryCountLineCommand(Guid.NewGuid(), null, 1, ProductA, UnitA, "", 0m, Product())],
                DateTimeOffset.UtcNow, Actor, DateTimeOffset.UtcNow, "full-discovery-correlation", "full-discovery-key", "full-discovery-fp")));

        Assert.Equal(2, created.Lines.Count);
        var discovered = Assert.Single(created.Lines, line => line.ProductId == ProductB);
        Assert.Equal("SKU-B", discovered.ProductSku);
        Assert.Equal("Product B", discovered.ProductName);
        Assert.Equal(UnitB, discovered.UnitOfMeasureId);
        Assert.Equal("EA", discovered.UnitOfMeasureCode);
        Assert.Equal(2m, discovered.ExpectedQuantity);
    }

    [Fact]
    public async Task Cycle_count_only_invalidates_for_selected_identity_movements()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder().UseSqlite(connection).Options;
        var context = Context(TenantA);
        var scope = new InventoryScope(TenantA, CompanyA, null, WarehouseA);
        await EnsureCreatedAsync(options, context);
        await SeedStockAsync(options, context, 5m);
        var persistence = new InventoryPersistence(options);

        var unrelatedCount = Assert.IsType<InventoryCountRecord>(await persistence.CreateCountAsync(
            context,
            new InventoryCountCreateCommand(
                Guid.NewGuid(), scope, "WH-A", "Warehouse A", InventoryCountType.Cycle, Actor, null,
                [new InventoryCountLineCommand(Guid.NewGuid(), null, 1, ProductA, UnitA, "", 0m, Product())],
                DateTimeOffset.UtcNow, Actor, DateTimeOffset.UtcNow, "cycle-unrelated-correlation", "cycle-unrelated-key", "cycle-unrelated-fp")));
        await SeedMovementAsync(options, context, ProductB, UnitB, "SKU-B", "Product B", 2m, "cycle-unrelated-movement");
        var unrelatedSubmitted = Assert.IsType<InventoryCountRecord>(await persistence.SubmitCountAsync(
            context,
            new InventoryCountSubmitCommand(
                unrelatedCount.Id, unrelatedCount.Version,
                [new InventoryCountObservationRequest(Assert.Single(unrelatedCount.Lines).Id, 5m)],
                Actor, "cycle-unrelated-submit", "cycle-unrelated-submit-fp", "cycle-unrelated-submit-correlation", DateTimeOffset.UtcNow)));
        var unrelatedPosted = Assert.IsType<InventoryCountRecord>(await persistence.PostCountAsync(
            context, Action(unrelatedSubmitted.Id, unrelatedSubmitted.Version, "cycle-unrelated-post", "cycle-unrelated-post-fp", DateTimeOffset.UtcNow)));
        Assert.Equal(InventoryControlDocumentStatus.Posted, unrelatedPosted.Status);

        var selectedCount = Assert.IsType<InventoryCountRecord>(await persistence.CreateCountAsync(
            context,
            new InventoryCountCreateCommand(
                Guid.NewGuid(), scope, "WH-A", "Warehouse A", InventoryCountType.Cycle, Actor, null,
                [new InventoryCountLineCommand(Guid.NewGuid(), null, 1, ProductA, UnitA, "", 0m, Product())],
                DateTimeOffset.UtcNow, Actor, DateTimeOffset.UtcNow, "cycle-selected-correlation", "cycle-selected-key", "cycle-selected-fp")));
        await SeedMovementAsync(options, context, ProductA, UnitA, "SKU-A", "Product A", 1m, "cycle-selected-movement");
        var selectedSubmitted = Assert.IsType<InventoryCountRecord>(await persistence.SubmitCountAsync(
            context,
            new InventoryCountSubmitCommand(
                selectedCount.Id, selectedCount.Version,
                [new InventoryCountObservationRequest(Assert.Single(selectedCount.Lines).Id, 5m)],
                Actor, "cycle-selected-submit", "cycle-selected-submit-fp", "cycle-selected-submit-correlation", DateTimeOffset.UtcNow)));
        var selectedPost = Assert.IsType<InventoryCountRecord>(await persistence.PostCountAsync(
            context, Action(selectedSubmitted.Id, selectedSubmitted.Version, "cycle-selected-post", "cycle-selected-post-fp", DateTimeOffset.UtcNow)));
        Assert.Equal(InventoryControlDocumentStatus.ResnapshotRequired, selectedPost.Status);
    }

    [Fact]
    public async Task Adjustment_approval_requires_two_distinct_eligible_actors_and_records_valid_delegation()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder().UseSqlite(connection).Options;
        var persistence = new InventoryPersistence(options);
        var context = Context(TenantA);
        var scope = new InventoryScope(TenantA, CompanyA, null, WarehouseA);
        await EnsureCreatedAsync(options, context);
        var at = DateTimeOffset.UtcNow;
        var reason = Assert.IsType<InventoryReasonCodeRecord>(await persistence.CreateReasonCodeAsync(
            context,
            new InventoryReasonCodeCommand(Guid.NewGuid(), "DAMAGE", "Damage", "تلف", InventoryReasonCategory.Adjustment, Actor, at, "approval-delta-reason", "approval-delta-reason-key", "approval-delta-reason-fp")));
        var deniedPolicy = ApprovalPolicy("adjustment-denied", allowDelegation: false);
        var deniedService = CreateService(options, [new InventoryApprovalPolicyBinding(scope, "stock-adjustment", deniedPolicy)]);
        var deniedDraft = AssertSucceeded(await deniedService.CreateAdjustmentAsync(
            Context(TenantA, Actor, "tenant.inventory.adjustment.create"),
            new InventoryAdjustmentCreateRequest(CompanyA, null, WarehouseA, "denied delegation",
                [new InventoryAdjustmentLineRequest(ProductA, UnitA, InventoryAdjustmentDirection.Increase, 1m, reason.Code)]),
            "approval-denied-create")).Value!;
        var deniedSubmitted = AssertSucceeded(await deniedService.SubmitAdjustmentAsync(
            Context(TenantA, Actor, "tenant.inventory.adjustment.submit"), deniedDraft.Id, deniedDraft.Version, null, "approval-denied-submit")).Value!;
        var denied = await deniedService.ApproveAdjustmentAsync(
            Context(TenantA, Actor4, "tenant.inventory.adjustment.approve"), deniedSubmitted.Id, deniedSubmitted.Version, null, "approval-denied-delegate");
        Assert.False(denied.Succeeded);
        Assert.Equal("approver_not_eligible", denied.Code);

        var delegationPolicy = ApprovalPolicy("adjustment-delegation", allowDelegation: true);
        var delegationService = CreateService(
            options,
            [new InventoryApprovalPolicyBinding(scope, "stock-adjustment", delegationPolicy)],
            [new PurchaseRequestApprovalDelegation(TenantA, CompanyA, null, "approval-stage", Actor3, Actor4, at.AddMinutes(-1), at.AddHours(1), "temporary controller delegation")]);
        var draft = AssertSucceeded(await delegationService.CreateAdjustmentAsync(
            Context(TenantA, Actor, "tenant.inventory.adjustment.create"),
            new InventoryAdjustmentCreateRequest(CompanyA, null, WarehouseA, "delegated approval",
                [new InventoryAdjustmentLineRequest(ProductA, UnitA, InventoryAdjustmentDirection.Increase, 1m, reason.Code)]),
            "approval-delta-create")).Value!;
        var submitted = AssertSucceeded(await delegationService.SubmitAdjustmentAsync(
            Context(TenantA, Actor, "tenant.inventory.adjustment.submit"), draft.Id, draft.Version, null, "approval-delta-submit")).Value!;

        var first = AssertSucceeded(await delegationService.ApproveAdjustmentAsync(
            Context(TenantA, Actor2, "tenant.inventory.adjustment.approve"), submitted.Id, submitted.Version, null, "approval-delta-first")).Value!;
        Assert.Equal(InventoryControlDocumentStatus.PendingApproval, first.Status);
        Assert.Equal(1, first.Approval!.RecordedApprovals);
        Assert.Equal(Actor2, first.Approval.LastApproverId);

        var duplicate = await delegationService.ApproveAdjustmentAsync(
            Context(TenantA, Actor2, "tenant.inventory.adjustment.approve"), first.Id, first.Version, null, "approval-delta-first-retry-different-key");
        Assert.False(duplicate.Succeeded);
        Assert.Equal("conflict", duplicate.Code);

        var completed = AssertSucceeded(await delegationService.ApproveAdjustmentAsync(
            Context(TenantA, Actor4, "tenant.inventory.adjustment.approve"), first.Id, first.Version, null, "approval-delta-delegated-second")).Value!;
        Assert.Equal(InventoryControlDocumentStatus.Approved, completed.Status);
        Assert.Equal(Actor4, completed.Approval!.LastApproverId);
        Assert.Equal(Actor3, completed.Approval.DelegatedFromActorId);
        var persisted = Assert.IsType<InventoryAdjustmentRecord>(await persistence.FindAdjustmentAsync(context, completed.Id));
        Assert.Equal(Actor4, persisted.Approval!.LastApproverId);
        Assert.Equal(Actor3, persisted.Approval.DelegatedFromActorId);
    }

    [Fact]
    public async Task Stock_issue_uses_the_same_two_distinct_approval_semantics()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder().UseSqlite(connection).Options;
        var persistence = new InventoryPersistence(options);
        var context = Context(TenantA);
        var scope = new InventoryScope(TenantA, CompanyA, null, WarehouseA);
        await EnsureCreatedAsync(options, context);
        var at = DateTimeOffset.UtcNow;
        var reason = Assert.IsType<InventoryReasonCodeRecord>(await persistence.CreateReasonCodeAsync(
            context,
            new InventoryReasonCodeCommand(Guid.NewGuid(), "USE", "Internal use", "استخدام داخلي", InventoryReasonCategory.StockIssue, Actor, at, "issue-approval-delta-reason", "issue-approval-delta-reason-key", "issue-approval-delta-reason-fp")));
        var service = CreateService(options, [new InventoryApprovalPolicyBinding(scope, "stock-issue", ApprovalPolicy("issue-two-approvers", allowDelegation: false))]);
        var draft = AssertSucceeded(await service.CreateStockIssueAsync(
            Context(TenantA, Actor, "tenant.inventory.issue.create"),
            new InventoryStockIssueCreateRequest(CompanyA, null, WarehouseA, "internal use",
                [new InventoryIssueLineRequest(ProductA, UnitA, 1m, reason.Code)]),
            "issue-approval-delta-create")).Value!;
        var submitted = AssertSucceeded(await service.SubmitStockIssueAsync(
            Context(TenantA, Actor, "tenant.inventory.issue.submit"), draft.Id, draft.Version, null, "issue-approval-delta-submit")).Value!;
        var first = AssertSucceeded(await service.ApproveStockIssueAsync(
            Context(TenantA, Actor2, "tenant.inventory.issue.approve"), submitted.Id, submitted.Version, null, "issue-approval-delta-first")).Value!;
        Assert.Equal(InventoryControlDocumentStatus.PendingApproval, first.Status);
        Assert.Equal(1, first.Approval!.RecordedApprovals);
        var completed = AssertSucceeded(await service.ApproveStockIssueAsync(
            Context(TenantA, Actor3, "tenant.inventory.issue.approve"), first.Id, first.Version, null, "issue-approval-delta-second")).Value!;
        Assert.Equal(InventoryControlDocumentStatus.Approved, completed.Status);
        Assert.Equal(2, completed.Approval!.RequiredApprovals);
    }

    private static InventoryControlActionCommand Action(Guid id, byte[] version, string key, string fingerprint, DateTimeOffset at, Guid actorId = default) =>
        new(id, version, actorId == Guid.Empty ? Actor : actorId, "test action", null, $"correlation-{key}", key, fingerprint, at);

    private static InventoryStockIssueCreateCommand IssueCreate(Guid id, decimal quantity, InventoryReasonCodeRecord reason, DateTimeOffset at, string destination) =>
        new(id, new InventoryScope(TenantA, CompanyA, null, WarehouseA), "WH-A", "Warehouse A", destination,
            [new InventoryStockIssueLineCommand(Guid.NewGuid(), ProductA, UnitA, quantity, "", null, Product(), reason)],
            Actor, at, $"correlation-{id:N}", $"issue-{id:N}", $"issue-fingerprint-{id:N}");

    private static async Task EnsureCreatedAsync(DbContextOptions options, InventoryRequestContext context)
    {
        await using var db = new InventoryDbContext(options, context.TenantContext);
        await db.Database.EnsureCreatedAsync();
    }

    private static async Task SeedStockAsync(DbContextOptions options, InventoryRequestContext context, decimal quantity)
    {
        await using var db = new InventoryDbContext(options, context.TenantContext);
        db.StockMovements.Add(new InventoryStockMovementEntity(
            new TenantId(TenantA), Guid.NewGuid(), CompanyA, null, WarehouseA, "WH-A", "Warehouse A", ProductA,
            "SKU-A", "Product A", UnitA, "EA", InventoryMovementDirection.Inbound, quantity, 10m, "SAR", null,
            InventoryMovementSourceType.OpeningBalance, Guid.NewGuid(), Guid.NewGuid(), null,
            DateOnly.FromDateTime(DateTime.UtcNow), Actor, "seed-stock", DateTimeOffset.UtcNow));
        await db.SaveChangesAsync();
    }

    private static async Task SeedMovementAsync(DbContextOptions options, InventoryRequestContext context, Guid productId, Guid unitId, string sku, string name, decimal quantity, string correlationId, DateTimeOffset? postedAt = null)
    {
        await using var db = new InventoryDbContext(options, context.TenantContext);
        db.StockMovements.Add(new InventoryStockMovementEntity(
            new TenantId(TenantA), Guid.NewGuid(), CompanyA, null, WarehouseA, "WH-A", "Warehouse A", productId,
            sku, name, unitId, "EA", InventoryMovementDirection.Inbound, quantity, null, null, InventoryValuationStatus.Pending, null,
            InventoryMovementSourceType.StockAdjustment, Guid.NewGuid(), Guid.NewGuid(), null,
            DateOnly.FromDateTime((postedAt ?? DateTimeOffset.UtcNow).UtcDateTime), Actor, correlationId, postedAt ?? DateTimeOffset.UtcNow));
        await db.SaveChangesAsync();
    }

    private static InventoryProductReference Product() => new(TenantA, ProductA, "SKU-A", "Product A", UnitA, "EA", true, true, false);

    private static InventoryOperationResult<T> AssertSucceeded<T>(InventoryOperationResult<T> result)
    {
        Assert.True(result.Succeeded, result.Code);
        return result;
    }

    private static PurchaseRequestApprovalPolicyDefinition ApprovalPolicy(string policyId, bool allowDelegation) =>
        new(policyId, 1, [new PurchaseRequestApprovalStageDefinition("approval-stage", 1, 2, [Actor2, Actor3], allowDelegation)], false, DateTimeOffset.UnixEpoch);

    private static InventoryService CreateService(
        DbContextOptions options,
        IReadOnlyList<InventoryApprovalPolicyBinding> policyBindings,
        IReadOnlyList<PurchaseRequestApprovalDelegation>? delegations = null) =>
        new(
            new InventoryPersistence(options),
            new InventoryResourceAuthorizationService(),
            new ConfiguredInventoryWarehouseProvider([new InventoryWarehouseOption(TenantA, CompanyA, null, WarehouseA, "WH-A", "Warehouse A")]),
            new StaticInventoryProductProvider(Product()),
            approvalPolicies: new ConfiguredInventoryApprovalPolicyProvider(policyBindings),
            approvalDelegation: new ConfiguredInventoryApprovalDelegationProvider(delegations ?? []));

    private static InventoryRequestContext Context(Guid tenantId, Guid actorId = default, string permission = "tenant.inventory.ledger.view") =>
        new InventoryTenantContextResolver().Resolve(
            FoundationRequestContext.ForTenant(
                actorId == Guid.Empty ? Actor : actorId,
                Guid.Parse("55555555-5555-5555-5555-555555555555"),
                TenantContext.ForOrdinaryMembership(new TenantId(tenantId), new MembershipReference(Guid.NewGuid()), new ScopeReference($"Warehouse:{WarehouseA:D}"), actorId: actorId == Guid.Empty ? Actor : actorId),
                permission)).Context!;

    private sealed class StaticInventoryProductProvider(InventoryProductReference product) : IInventoryProductProvider
    {
        public Task<InventoryProductReference?> FindAsync(InventoryRequestContext context, Guid productId, CancellationToken cancellationToken = default) =>
            Task.FromResult<InventoryProductReference?>(product.TenantId == context.TenantId.Value && product.ProductId == productId ? product : null);
    }
}
