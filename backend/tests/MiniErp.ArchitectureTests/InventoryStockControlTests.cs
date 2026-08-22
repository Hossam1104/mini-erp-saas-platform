using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MiniErp.App.BuildingBlocks.Rest;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.Modules.Inventory;
using MiniErp.Contracts.Modules.Foundation;
using MiniErp.Contracts.Modules.Inventory;
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
    private static readonly Guid Actor = Guid.Parse("44444444-4444-4444-4444-444444444444");

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
                [new InventoryCountObservationRequest(line.Id, 4m, reason.Code)], Actor, "count-submit-key", "count-submit-fp", "count-submit-correlation", at)));
        Assert.Equal(InventoryControlDocumentStatus.PendingApproval, submitted.Status);
        var approved = Assert.IsType<InventoryCountRecord>(await persistence.ApproveCountAsync(
            context, Action(submitted.Id, submitted.Version, "count-approve-key", "count-approve-fp", at, reviewer)));
        var posted = Assert.IsType<InventoryCountRecord>(await persistence.PostCountAsync(
            context, Action(approved.Id, approved.Version, "count-post-key", "count-post-fp", at)));
        var movement = Assert.Single(await persistence.ListMovementsAsync(context, scope), item => item.SourceType == InventoryMovementSourceType.InventoryCountVariance);

        Assert.Equal(InventoryControlDocumentStatus.Posted, posted.Status);
        Assert.Equal(InventoryMovementDirection.Outbound, movement.Direction);
        Assert.Equal(InventoryValuationStatus.Pending, movement.ValuationStatus);
        Assert.Equal(1m, movement.Quantity);
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

    private static InventoryProductReference Product() => new(TenantA, ProductA, "SKU-A", "Product A", UnitA, "EA", true, true, false);

    private static InventoryRequestContext Context(Guid tenantId) =>
        new InventoryTenantContextResolver().Resolve(
            FoundationRequestContext.ForTenant(
                Actor,
                Guid.Parse("55555555-5555-5555-5555-555555555555"),
                TenantContext.ForOrdinaryMembership(new TenantId(tenantId), new MembershipReference(Guid.NewGuid()), new ScopeReference($"Warehouse:{WarehouseA:D}"), actorId: Actor),
                "tenant.inventory.ledger.view")).Context!;
}
