using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MiniErp.App.BuildingBlocks.Rest;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.Modules.Inventory;
using MiniErp.Contracts.Modules.Inventory;
using MiniErp.Infrastructure.Persistence.Modules.Inventory;
using Xunit;

namespace MiniErp.ArchitectureTests;

public sealed class InventoryValuationTests
{
    private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid CompanyId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid WarehouseId = Guid.Parse("cccccccc-1111-1111-1111-111111111111");
    private static readonly Guid ProductId = Guid.Parse("77777777-7777-7777-7777-777777777777");
    private static readonly Guid UnitId = Guid.Parse("88888888-8888-8888-8888-888888888888");
    private static readonly Guid ActorId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    [Fact]
    public void Moving_weighted_average_uses_decimal_inbound_and_outbound_formulas()
    {
        Assert.True(MovingWeightedAverageCalculator.TryApply(
            new MovingWeightedAverageInput(InventoryMovementDirection.Inbound, 10m, 10m, 0m, 0m, 8, 8, InventoryValuationRoundingMode.ToEven),
            out var first,
            out var firstError), firstError);
        Assert.Equal(10m, first.NewQuantity);
        Assert.Equal(100m, first.NewValue);
        Assert.Equal(10m, first.AverageUnitCost);

        Assert.True(MovingWeightedAverageCalculator.TryApply(
            new MovingWeightedAverageInput(InventoryMovementDirection.Inbound, 5m, 20m, first.NewQuantity, first.NewValue, 8, 8, InventoryValuationRoundingMode.ToEven),
            out var second,
            out var secondError), secondError);
        Assert.Equal(15m, second.NewQuantity);
        Assert.Equal(200m, second.NewValue);
        Assert.Equal(13.33333333m, second.AverageUnitCost);

        Assert.True(MovingWeightedAverageCalculator.TryApply(
            new MovingWeightedAverageInput(InventoryMovementDirection.Outbound, 5m, 0m, second.NewQuantity, second.NewValue, 8, 8, InventoryValuationRoundingMode.ToEven),
            out var issue,
            out var issueError), issueError);
        Assert.Equal(10m, issue.NewQuantity);
        Assert.Equal(133.33333333m, issue.NewValue);
        Assert.Equal(13.33333333m, issue.AverageUnitCost);
    }

    [Fact]
    public void Moving_weighted_average_rejects_negative_state_and_over_issue()
    {
        Assert.False(MovingWeightedAverageCalculator.TryApply(
            new MovingWeightedAverageInput(InventoryMovementDirection.Outbound, 11m, 0m, 10m, 100m, 8, 8, InventoryValuationRoundingMode.ToEven),
            out _,
            out var error));
        Assert.Equal("valuation_would_make_quantity_negative", error);

        Assert.False(MovingWeightedAverageCalculator.TryApply(
            new MovingWeightedAverageInput(InventoryMovementDirection.Inbound, 1m, -1m, 0m, 0m, 8, 8, InventoryValuationRoundingMode.ToEven),
            out _,
            out error));
        Assert.Equal("invalid_non_negative_valuation_input", error);
    }

    [Fact]
    public async Task Valuation_correction_reverses_original_evidence_append_only()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<InventoryDbContext>().UseSqlite(connection).Options;
        var context = Context();
        var originalMovementId = Guid.NewGuid();
        var correctionMovementId = Guid.NewGuid();
        await using (var db = new InventoryDbContext(options, context.TenantContext))
        {
            await db.Database.EnsureCreatedAsync();
            db.StockMovements.Add(Movement(originalMovementId, new DateOnly(2026, 8, 23), DateTimeOffset.UtcNow, 10m));
            db.StockMovements.Add(new InventoryStockMovementEntity(
                new TenantId(TenantId),
                correctionMovementId,
                CompanyId,
                null,
                WarehouseId,
                "WH-A",
                "Warehouse A",
                ProductId,
                "SKU-A",
                "Product A",
                UnitId,
                "EA",
                InventoryMovementDirection.Outbound,
                5m,
                null,
                null,
                null,
                InventoryMovementSourceType.Correction,
                Guid.NewGuid(),
                Guid.NewGuid(),
                originalMovementId,
                new DateOnly(2026, 8, 23),
                ActorId,
                "valuation-correction",
                DateTimeOffset.UtcNow.AddSeconds(1)));
            await db.SaveChangesAsync();
        }

        var persistence = new InventoryValuationPersistence(options, null, null, null);
        var policy = await persistence.CreatePolicyAsync(
            context,
            new InventoryValuationPolicyCommand(
                Guid.NewGuid(),
                new InventoryValuationPolicyRequest(
                    CompanyId,
                    Guid.NewGuid(),
                    "SAR",
                    InventoryValuationScopeMode.WarehouseProductUom,
                    new DateOnly(2026, 1, 1)),
                ActorId,
                DateTimeOffset.UtcNow,
                "valuation-correction-policy",
                "valuation-correction-policy-key",
                "valuation-correction-policy-fingerprint"));
        Assert.True(policy.Succeeded, policy.Code);

        var result = await persistence.ProcessAsync(
            context,
            new InventoryValuationProcessCommand(
                CompanyId,
                null,
                WarehouseId,
                ProductId,
                UnitId,
                null,
                null,
                ActorId,
                DateTimeOffset.UtcNow,
                "valuation-correction-process",
                "valuation-correction-process-key",
                "valuation-correction-process-fingerprint"));

        Assert.Equal(2, result.AppliedCount);
        var events = await persistence.ListEventsAsync(context, new InventoryValuationQuery(CompanyId));
        Assert.Equal(2, events.Count);
        var original = Assert.Single(events, item => item.MovementId == originalMovementId);
        var correction = Assert.Single(events, item => item.MovementId == correctionMovementId);
        Assert.Equal(InventoryValuationEventStatus.Applied, correction.Status);
        Assert.Equal(original.Id, correction.CorrectionOfValuationEventId);
        Assert.Equal(5m, correction.NewQuantity);
        Assert.Equal(50m, correction.NewValue);
        Assert.Equal(-50m, correction.MovementValue);
        Assert.Equal(2L, correction.LedgerSequence);
    }

    [Fact]
    public async Task Company_ledger_sequence_is_durable_and_independent_of_timestamps()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<InventoryDbContext>().UseSqlite(connection).Options;
        var context = Context();
        await using (var db = new InventoryDbContext(options, context.TenantContext))
        {
            await db.Database.EnsureCreatedAsync();
            db.StockMovements.AddRange(
                Movement(Guid.NewGuid(), new DateOnly(2026, 8, 23), DateTimeOffset.UtcNow.AddMinutes(1), 10m),
                Movement(Guid.NewGuid(), new DateOnly(2026, 8, 1), DateTimeOffset.UtcNow.AddMinutes(-1), 20m));
            await db.SaveChangesAsync();
        }

        await using (var db = new InventoryDbContext(options, context.TenantContext))
        {
            var rows = await db.StockMovements.AsNoTracking().OrderBy(item => item.LedgerSequence).ToArrayAsync();
            Assert.Equal([1L, 2L], rows.Select(item => item.LedgerSequence).ToArray());
            var anchor = await db.CompanyLedgerSequenceAnchors.AsNoTracking().SingleAsync(item => item.CompanyId == CompanyId);
            Assert.Equal(3L, anchor.NextSequence);
        }
    }

    [Fact]
    public async Task Valuation_process_appends_explainable_evidence_and_truthful_finance_handoff()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<InventoryDbContext>().UseSqlite(connection).Options;
        var context = Context();
        var movementId = Guid.NewGuid();
        await using (var db = new InventoryDbContext(options, context.TenantContext))
        {
            await db.Database.EnsureCreatedAsync();
            db.StockMovements.Add(Movement(movementId, new DateOnly(2026, 8, 23), DateTimeOffset.UtcNow, 10m));
            await db.SaveChangesAsync();
        }

        var persistence = new InventoryValuationPersistence(options, null, null, null);
        var policy = await persistence.CreatePolicyAsync(
            context,
            new InventoryValuationPolicyCommand(
                Guid.NewGuid(),
                new InventoryValuationPolicyRequest(
                    CompanyId,
                    Guid.NewGuid(),
                    "SAR",
                    InventoryValuationScopeMode.WarehouseProductUom,
                    new DateOnly(2026, 1, 1)),
                ActorId,
                DateTimeOffset.UtcNow,
                "valuation-policy",
                "valuation-policy-key",
                "valuation-policy-fingerprint"));
        Assert.True(policy.Succeeded, policy.Code);

        var result = await persistence.ProcessAsync(
            context,
            new InventoryValuationProcessCommand(
                CompanyId,
                null,
                WarehouseId,
                ProductId,
                UnitId,
                null,
                null,
                ActorId,
                DateTimeOffset.UtcNow,
                "valuation-process",
                "valuation-process-key",
                "valuation-process-fingerprint"));

        Assert.Equal(1, result.AppliedCount);
        Assert.Equal(0, result.PendingCount);
        var events = await persistence.ListEventsAsync(context, new InventoryValuationQuery(CompanyId));
        var evidence = Assert.Single(events);
        Assert.Equal(InventoryValuationEventStatus.Applied, evidence.Status);
        Assert.Equal(movementId, evidence.MovementId);
        Assert.Equal(InventoryMovementSourceType.OpeningBalance, evidence.SourceType);
        Assert.Equal(10m, evidence.TransactionUnitCost);
        Assert.Equal("SAR", evidence.TransactionCurrencyCode);
        Assert.Equal(10m, evidence.BaseUnitCost);
        Assert.Equal(100m, evidence.MovementValue);
        Assert.Equal(100m, evidence.NewValue);

        var handoffs = await persistence.ListFinanceHandoffsAsync(context, new InventoryValuationQuery(CompanyId));
        var handoff = Assert.Single(handoffs);
        Assert.Equal(InventoryFinanceValuationHandoffStatus.ReadyForFinance, handoff.Status);
        Assert.Equal(evidence.Id, handoff.ValuationEvidenceId);
        Assert.Equal(InventoryMovementSourceType.OpeningBalance, handoff.SourceType);
        Assert.Equal("inventory-valuation-finance.v1", handoff.ContractVersion);
        Assert.DoesNotContain("Journal", handoff.ContractVersion, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Valuation_export_is_bounded_filtered_and_audited()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<InventoryDbContext>().UseSqlite(connection).Options;
        var context = Context();
        var movementId = Guid.NewGuid();
        await using (var db = new InventoryDbContext(options, context.TenantContext))
        {
            await db.Database.EnsureCreatedAsync();
            db.StockMovements.Add(Movement(movementId, new DateOnly(2026, 8, 23), DateTimeOffset.UtcNow, 10m));
            await db.SaveChangesAsync();
        }

        var persistence = new InventoryValuationPersistence(options, null, null, null);
        var policy = await persistence.CreatePolicyAsync(
            context,
            new InventoryValuationPolicyCommand(
                Guid.NewGuid(),
                new InventoryValuationPolicyRequest(CompanyId, Guid.NewGuid(), "SAR", InventoryValuationScopeMode.WarehouseProductUom, new DateOnly(2026, 1, 1)),
                ActorId,
                DateTimeOffset.UtcNow,
                "valuation-export-policy",
                "valuation-export-policy-key",
                "valuation-export-policy-fingerprint"));
        Assert.True(policy.Succeeded, policy.Code);

        await persistence.ProcessAsync(
            context,
            new InventoryValuationProcessCommand(CompanyId, null, WarehouseId, ProductId, UnitId, null, null, ActorId, DateTimeOffset.UtcNow, "valuation-export-process", "valuation-export-process-key", "valuation-export-process-fingerprint"));

        var exported = await persistence.ExportAsync(context, new InventoryValuationQuery(CompanyId, WarehouseId: WarehouseId, FromLedgerSequence: 1, ToLedgerSequence: 1));
        Assert.EndsWith(".csv", exported.FileName, StringComparison.Ordinal);
        Assert.Equal("text/csv; charset=utf-8", exported.ContentType);
        Assert.Contains("# export=inventory-valuation", exported.Content, StringComparison.Ordinal);
        Assert.Contains("# filters=", exported.Content, StringComparison.Ordinal);
        Assert.Contains("# freshness=", exported.Content, StringComparison.Ordinal);
        Assert.Contains(movementId.ToString("D"), exported.Content, StringComparison.Ordinal);

        await using var auditDb = new InventoryDbContext(options, context.TenantContext);
        Assert.Contains(await auditDb.Audit.AsNoTracking().ToArrayAsync(), item => item.OperationId == "inventory.valuation.export");
    }

    [Fact]
    public void Valuation_contract_keeps_correction_and_source_revision_as_append_only_seams()
    {
        var eventType = typeof(InventoryMovementValuationEventRecord);
        var correction = eventType.GetProperty(nameof(InventoryMovementValuationEventRecord.CorrectionOfValuationEventId));
        var sourceRevision = eventType.GetProperty(nameof(InventoryMovementValuationEventRecord.SourceRevisionId));
        Assert.NotNull(correction);
        Assert.NotNull(sourceRevision);
        Assert.Contains("SourceDocumentId", eventType.GetProperties().Select(item => item.Name));
        Assert.Contains("SourceLineId", eventType.GetProperties().Select(item => item.Name));
        Assert.Contains("ValuationEvidenceId", typeof(InventoryFinanceValuationHandoffRecord).GetProperties().Select(item => item.Name));
    }

    private static InventoryStockMovementEntity Movement(Guid id, DateOnly effectiveDate, DateTimeOffset postedAt, decimal cost) =>
        new(
            new TenantId(TenantId),
            id,
            CompanyId,
            null,
            WarehouseId,
            "WH-A",
            "Warehouse A",
            ProductId,
            "SKU-A",
            "Product A",
            UnitId,
            "EA",
            InventoryMovementDirection.Inbound,
            10m,
            cost,
            "SAR",
            null,
            InventoryMovementSourceType.OpeningBalance,
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            effectiveDate,
            ActorId,
            "valuation-test",
            postedAt);

    private static InventoryRequestContext Context() =>
        new InventoryTenantContextResolver().Resolve(
            FoundationRequestContext.ForTenant(
                ActorId,
                Guid.Parse("55555555-5555-5555-5555-555555555555"),
                TenantContext.ForOrdinaryMembership(
                    new TenantId(TenantId),
                    new MembershipReference(Guid.NewGuid()),
                    actorId: ActorId),
                "tenant.inventory.valuation.process")).Context!;
}
