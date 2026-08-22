using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MiniErp.App.BuildingBlocks.Rest;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.Modules.MasterData;
using MiniErp.App.Modules.Inventory;
using MiniErp.App.Modules.Procurement;
using MiniErp.Contracts.Modules.Foundation;
using MiniErp.Contracts.Modules.Inventory;
using MiniErp.Contracts.Modules.MasterData;
using MiniErp.Contracts.Modules.Procurement;
using MiniErp.Infrastructure.Persistence.Modules.Inventory;
using MiniErp.Infrastructure.Persistence.Modules.MasterData;
using Xunit;

namespace MiniErp.ArchitectureTests;

public sealed class InventoryLedgerTests
{
    private static readonly Guid TenantA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid TenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid CompanyA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid WarehouseA = Guid.Parse("cccccccc-1111-1111-1111-111111111111");
    private static readonly Guid WarehouseB = Guid.Parse("cccccccc-2222-2222-2222-222222222222");
    private static readonly Guid ProductA = Guid.Parse("77777777-7777-7777-7777-777777777777");
    private static readonly Guid UnitA = Guid.Parse("88888888-8888-8888-8888-888888888888");
    private static readonly Guid Actor = Guid.Parse("44444444-4444-4444-4444-444444444444");

    [Fact]
    public void Inventory_authorization_is_Tenant_and_server_scope_bound()
    {
        var context = Context(TenantA, new ScopeReference($"Warehouse:{WarehouseA:D}"));
        var authorization = new InventoryResourceAuthorizationService();

        Assert.True(authorization.IsAllowed(context, "inventory.ledger.read", new InventoryScope(TenantA, CompanyA, null, WarehouseA)));
        Assert.False(authorization.IsAllowed(context, "inventory.ledger.read", new InventoryScope(TenantA, CompanyA, null, WarehouseB)));
        Assert.False(authorization.IsAllowed(context, "inventory.ledger.read", new InventoryScope(TenantB, CompanyA, null, WarehouseA)));
        Assert.False(authorization.IsAllowed(context, "inventory.opening.create", new InventoryScope(TenantA, CompanyA, null, WarehouseA)));
    }

    [Fact]
    public void Inventory_public_operations_declare_safe_mutation_metadata()
    {
        var inventory = FoundationOperationCatalog.PublicOperations
            .Where(item => item.OperationId.StartsWith("inventory.", StringComparison.Ordinal))
            .ToArray();

        Assert.NotEmpty(inventory);
        Assert.All(inventory, operation =>
        {
            Assert.Equal(FoundationSecurityProfile.OrdinaryMembership, operation.SecurityProfile);
            Assert.Equal(FoundationScopePolicy.Tenant, operation.ScopePolicy);
            Assert.False(string.IsNullOrWhiteSpace(operation.ExactPermissionCode));
        });

        var mutations = inventory.Where(item => item.IsUnsafe).ToArray();
        Assert.NotEmpty(mutations);
        Assert.All(mutations, operation =>
        {
            Assert.True(operation.RequiresAntiforgery);
            Assert.True(operation.RequiresMandatoryAudit);
            Assert.Equal(FoundationIdempotencyPolicy.Required, operation.Idempotency);
        });
        Assert.All(mutations.Where(item => !item.OperationId.EndsWith(".create", StringComparison.Ordinal)), operation =>
            Assert.Equal(FoundationConcurrencyPolicy.IfMatch, operation.Concurrency));
    }

    [Fact]
    public async Task Inventory_ledger_query_filter_keeps_Tenants_isolated()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder<InventoryDbContext>().UseSqlite(connection).Options;
        var movement = new InventoryStockMovementEntity(
            new TenantId(TenantA),
            Guid.NewGuid(),
            CompanyA,
            null,
            WarehouseA,
            "WH-A",
            "Warehouse A",
            ProductA,
            "SKU-A",
            "Product A",
            UnitA,
            "EA",
            InventoryMovementDirection.Inbound,
            5m,
            10m,
            "SAR",
            null,
            InventoryMovementSourceType.OpeningBalance,
            Guid.NewGuid(),
            Guid.NewGuid(),
            null,
            DateOnly.FromDateTime(DateTime.UtcNow),
            Actor,
            "inventory-test",
            DateTimeOffset.UtcNow);

        await using (var tenantA = new InventoryDbContext(options, TenantContext.ForOrdinaryMembership(new TenantId(TenantA), new MembershipReference(Guid.NewGuid()), actorId: Actor)))
        {
            await tenantA.Database.EnsureCreatedAsync();
            tenantA.StockMovements.Add(movement);
            await tenantA.SaveChangesAsync();
        }

        await using (var tenantA = new InventoryDbContext(options, TenantContext.ForOrdinaryMembership(new TenantId(TenantA), new MembershipReference(Guid.NewGuid()), actorId: Actor)))
        {
            Assert.Single(await tenantA.StockMovements.AsNoTracking().ToListAsync());
        }

        await using (var tenantB = new InventoryDbContext(options, TenantContext.ForOrdinaryMembership(new TenantId(TenantB), new MembershipReference(Guid.NewGuid()), actorId: Actor)))
        {
            Assert.Empty(await tenantB.StockMovements.AsNoTracking().ToListAsync());
        }
    }

    [Fact]
    public async Task Opening_posting_and_correction_preserve_immutable_ledger_effects()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder().UseSqlite(connection).Options;
        var context = Context(TenantA, new ScopeReference($"Warehouse:{WarehouseA:D}"));
        var persistence = new InventoryPersistence(options);
        var scope = new InventoryScope(TenantA, CompanyA, null, WarehouseA);
        var product = Product();
        var warehouse = Warehouse();
        var row = new InventoryOpeningBalanceRowCommand(Guid.NewGuid(), ProductA, UnitA, 5m, 10m, "SAR", null, "line-1", product, null);
        var command = new InventoryOpeningBalanceCommand(
            Guid.NewGuid(), scope, warehouse.Code, warehouse.Name, DateOnly.FromDateTime(DateTime.UtcNow),
            "Inventory Operations", "Inventory Import", DateTimeOffset.UtcNow, "opening-1", [row], Actor,
            DateTimeOffset.UtcNow, "opening-correlation", "opening-key", "opening-fingerprint");

        await using (var db = new InventoryDbContext(options, context.TenantContext))
        {
            await db.Database.EnsureCreatedAsync();
        }

        var created = Assert.IsType<InventoryOpeningBalanceRecord>(await persistence.CreateOpeningBalanceAsync(context, command));
        Assert.NotEmpty(created.Version);
        var validated = Assert.IsType<InventoryOpeningBalanceRecord>(await persistence.ValidateOpeningBalanceAsync(context, created.Id, created.Version, Actor, "validated", "validate-correlation", "validate-key", "validate-fingerprint"));
        var posted = Assert.IsType<InventoryOpeningBalanceRecord>(await persistence.PostOpeningBalanceAsync(context, validated.Id, validated.Version, Actor, "posted", "post-correlation", "post-key", "post-fingerprint"));

        var afterPosting = await persistence.ListMovementsAsync(context, scope);
        var openingMovement = Assert.Single(afterPosting);
        Assert.Equal(InventoryMovementSourceType.OpeningBalance, openingMovement.SourceType);
        Assert.Equal(5m, openingMovement.Quantity);
        Assert.Equal("EA", openingMovement.UnitOfMeasureCode);
        Assert.Null(openingMovement.CorrectionOfMovementId);

        var availability = Assert.IsType<InventoryAvailabilityRecord>(await persistence.GetAvailabilityAsync(context, scope, ProductA, UnitA, null, product, warehouse));
        Assert.Equal(5m, availability.OnHandQuantity);
        Assert.Equal(5m, availability.AvailableQuantity);
        Assert.Equal(0m, availability.ReservedQuantity);
        Assert.Equal(0m, availability.ExpectedQuantity);
        Assert.Equal(0m, availability.DamagedQuantity);
        Assert.Equal(0m, availability.InTransitQuantity);

        var corrected = Assert.IsType<InventoryOpeningBalanceRecord>(await persistence.CorrectOpeningBalanceAsync(context, posted.Id, posted.Version, Actor, "corrected", "correct-correlation", "correct-key", "correct-fingerprint"));
        Assert.Equal(InventoryOpeningBalanceStatus.Corrected, corrected.Status);
        var afterCorrection = await persistence.ListMovementsAsync(context, scope);
        Assert.Equal(2, afterCorrection.Count);
        var correction = Assert.Single(afterCorrection, item => item.SourceType == InventoryMovementSourceType.Correction);
        Assert.Equal(InventoryMovementDirection.Outbound, correction.Direction);
        Assert.Equal(openingMovement.Id, correction.CorrectionOfMovementId);
        var correctedAvailability = Assert.IsType<InventoryAvailabilityRecord>(await persistence.GetAvailabilityAsync(context, scope, ProductA, UnitA, null, product, warehouse));
        Assert.Equal(0m, correctedAvailability.OnHandQuantity);
        Assert.Equal(0m, correctedAvailability.AvailableQuantity);
    }

    [Fact]
    public async Task Reservations_support_partial_allocation_release_and_durable_replay()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder().UseSqlite(connection).Options;
        var context = Context(TenantA, new ScopeReference($"Warehouse:{WarehouseA:D}"));
        var persistence = new InventoryPersistence(options);
        var scope = new InventoryScope(TenantA, CompanyA, null, WarehouseA);
        var product = Product();
        var warehouse = Warehouse();
        await using (var db = new InventoryDbContext(options, context.TenantContext))
        {
            await db.Database.EnsureCreatedAsync();
            db.StockMovements.Add(new InventoryStockMovementEntity(
                new TenantId(TenantA), Guid.NewGuid(), CompanyA, null, WarehouseA, warehouse.Code, warehouse.Name,
                ProductA, product.Sku, product.Name, UnitA, product.BaseUnitOfMeasureCode, InventoryMovementDirection.Inbound,
                5m, 10m, "SAR", null, InventoryMovementSourceType.OpeningBalance, Guid.NewGuid(), Guid.NewGuid(), null,
                DateOnly.FromDateTime(DateTime.UtcNow), Actor, "reservation-stock", DateTimeOffset.UtcNow));
            await db.SaveChangesAsync();
        }

        var command = new InventoryReservationCommand(
            Guid.NewGuid(), scope, ProductA, UnitA, 7m, "Demand", "DEMAND-1", true, null, product,
            warehouse.Code, warehouse.Name, Actor, DateTimeOffset.UtcNow, "reservation-correlation", "reservation-key", "reservation-fingerprint");
        var created = Assert.IsType<InventoryReservationRecord>(await persistence.CreateReservationAsync(context, command, 5m));
        Assert.NotEmpty(created.Version);
        Assert.Equal(5m, created.ReservedQuantity);
        Assert.Equal(2m, created.UnallocatedQuantity);

        var replay = Assert.IsType<InventoryReservationRecord>(await persistence.CreateReservationAsync(context, command with { Id = Guid.NewGuid() }, 5m));
        Assert.Equal(created.Id, replay.Id);
        Assert.Equal("EA", replay.UnitOfMeasureCode);
        Assert.Single(await persistence.ListReservationsAsync(context, scope));

        var conflictingReplay = await new InventoryPersistence(options).CreateReservationAsync(
            context,
            command with { Id = Guid.NewGuid(), RequestFingerprint = "different-request" },
            5m);
        Assert.Null(conflictingReplay);

        var reservedAvailability = Assert.IsType<InventoryAvailabilityRecord>(await persistence.GetAvailabilityAsync(context, scope, ProductA, UnitA, null, product, warehouse));
        Assert.Equal(5m, reservedAvailability.OnHandQuantity);
        Assert.Equal(5m, reservedAvailability.ReservedQuantity);
        Assert.Equal(0m, reservedAvailability.AvailableQuantity);

        var released = Assert.IsType<InventoryReservationRecord>(await persistence.ReleaseReservationAsync(context, created.Id, created.Version, Actor, "released", "release-correlation", "release-key", "release-fingerprint"));
        Assert.Equal(InventoryReservationStatus.Released, released.Status);
        var restoredAvailability = Assert.IsType<InventoryAvailabilityRecord>(await persistence.GetAvailabilityAsync(context, scope, ProductA, UnitA, null, product, warehouse));
        Assert.Equal(5m, restoredAvailability.OnHandQuantity);
        Assert.Equal(0m, restoredAvailability.ReservedQuantity);
        Assert.Equal(5m, restoredAvailability.AvailableQuantity);
    }

    [Fact]
    public async Task Opening_duplicate_source_rows_are_quarantined_and_posting_is_atomic()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder().UseSqlite(connection).Options;
        var context = Context(TenantA, new ScopeReference($"Warehouse:{WarehouseA:D}"));
        var persistence = new InventoryPersistence(options);
        await EnsureInventoryCreatedAsync(options, context);

        var command = OpeningCommand(
            "opening-duplicate",
            [OpeningRow(5m, "same-line"), OpeningRow(3m, "same-line")],
            idempotencyKey: "opening-duplicate-key");

        var created = Assert.IsType<InventoryOpeningBalanceRecord>(await persistence.CreateOpeningBalanceAsync(context, command));
        Assert.Single(created.Rows.Select(item => item.SourceFingerprint).Distinct(StringComparer.Ordinal));
        Assert.Single(created.Rows, item => item.Status == InventoryOpeningRowStatus.Valid);
        var quarantined = Assert.Single(created.Rows, item => item.Status == InventoryOpeningRowStatus.Quarantined);
        Assert.Equal("duplicate_source_row", quarantined.ValidationCode);

        var validated = Assert.IsType<InventoryOpeningBalanceRecord>(await persistence.ValidateOpeningBalanceAsync(
            context, created.Id, created.Version, Actor, "validate duplicate", "duplicate-validate", "duplicate-validate-key", "duplicate-validate-fingerprint"));
        Assert.Equal(InventoryOpeningBalanceStatus.Validated, validated.Status);

        var posted = await persistence.PostOpeningBalanceAsync(
            context, validated.Id, validated.Version, Actor, "post duplicate", "duplicate-post", "duplicate-post-key", "duplicate-post-fingerprint");
        Assert.Null(posted);
        Assert.Empty(await persistence.ListMovementsAsync(context, new InventoryScope(TenantA, CompanyA, null, WarehouseA)));

        var history = await persistence.ReadOpeningHistoryAsync(context, created.Id);
        Assert.Contains(history, item => item.Action == "post-blocked" && item.Reason == "opening_quarantined_rows");
        var audit = await persistence.ReadAuditAsync(context, "opening-balance", created.Id);
        Assert.Contains(audit, item => item.OperationId == "inventory.opening.post" && item.Decision == "Failed");
    }

    [Fact]
    public async Task Opening_partial_quarantine_fails_closed_without_partial_movement()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder().UseSqlite(connection).Options;
        var context = Context(TenantA, new ScopeReference($"Warehouse:{WarehouseA:D}"));
        var persistence = new InventoryPersistence(options);
        await EnsureInventoryCreatedAsync(options, context);

        var created = Assert.IsType<InventoryOpeningBalanceRecord>(await persistence.CreateOpeningBalanceAsync(
            context,
            OpeningCommand(
                "opening-partial",
                [OpeningRow(5m, "valid-line"), OpeningRow(3m, "invalid-line", validationCode: "invalid_quantity")],
                idempotencyKey: "opening-partial-key")));
        Assert.Equal(1, created.ValidRowCount);
        Assert.Equal(1, created.QuarantinedRowCount);

        var validated = Assert.IsType<InventoryOpeningBalanceRecord>(await persistence.ValidateOpeningBalanceAsync(
            context, created.Id, created.Version, Actor, "validate partial", "partial-validate", "partial-validate-key", "partial-validate-fingerprint"));
        Assert.Equal(InventoryOpeningBalanceStatus.Validated, validated.Status);
        Assert.Contains(validated.Rows, item => item.ValidationCode == "invalid_quantity");

        Assert.Null(await persistence.PostOpeningBalanceAsync(
            context, validated.Id, validated.Version, Actor, "post partial", "partial-post", "partial-post-key", "partial-post-fingerprint"));
        var availability = Assert.IsType<InventoryAvailabilityRecord>(await persistence.GetAvailabilityAsync(
            context,
            new InventoryScope(TenantA, CompanyA, null, WarehouseA),
            ProductA,
            UnitA,
            null,
            Product(),
            Warehouse()));
        Assert.Equal(0m, availability.OnHandQuantity);
        Assert.Empty(await persistence.ListMovementsAsync(context, new InventoryScope(TenantA, CompanyA, null, WarehouseA)));
    }

    [Fact]
    public async Task Opening_source_identity_is_durable_per_Tenant_and_distinct_rows_remain_valid()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder().UseSqlite(connection).Options;
        var tenantAContext = Context(TenantA, new ScopeReference($"Warehouse:{WarehouseA:D}"));
        var tenantBContext = Context(TenantB, new ScopeReference($"Warehouse:{WarehouseB:D}"));
        var persistence = new InventoryPersistence(options);
        await EnsureInventoryCreatedAsync(options, tenantAContext);

        var first = await CreateAndPostOpeningAsync(
            persistence,
            tenantAContext,
            OpeningCommand("same-source", [OpeningRow(5m, "line-1")], idempotencyKey: "source-a-1"));
        var secondTenant = await CreateAndPostOpeningAsync(
            persistence,
            tenantBContext,
            OpeningCommand("same-source", [OpeningRow(5m, "line-1", product: Product(TenantB), tenantId: TenantB)], tenantId: TenantB, warehouseId: WarehouseB, idempotencyKey: "source-b-1"));

        Assert.NotEqual(first.Id, secondTenant.Id);
        Assert.Single(await persistence.ListMovementsAsync(tenantAContext, new InventoryScope(TenantA, CompanyA, null, WarehouseA)));
        Assert.Single(await persistence.ListMovementsAsync(tenantBContext, new InventoryScope(TenantB, CompanyA, null, WarehouseB)));

        var duplicateLater = Assert.IsType<InventoryOpeningBalanceRecord>(await persistence.CreateOpeningBalanceAsync(
            tenantAContext,
            OpeningCommand("same-source", [OpeningRow(7m, "line-1")], idempotencyKey: "source-a-2")));
        Assert.Equal(InventoryOpeningRowStatus.Quarantined, Assert.Single(duplicateLater.Rows).Status);
        Assert.Equal("duplicate_source_row", duplicateLater.Rows[0].ValidationCode);
        Assert.Null(await persistence.PostOpeningBalanceAsync(
            tenantAContext,
            duplicateLater.Id,
            duplicateLater.Version,
            Actor,
            "duplicate later post",
            "source-a-2-post",
            "source-a-2-post-key",
            "source-a-2-post-fingerprint"));
    }

    [Fact]
    public async Task Opening_batches_with_identical_business_provenance_and_different_extraction_times_are_duplicates()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder().UseSqlite(connection).Options;
        var context = Context(TenantA, new ScopeReference($"Warehouse:{WarehouseA:D}"));
        var persistence = new InventoryPersistence(options);
        var scope = new InventoryScope(TenantA, CompanyA, null, WarehouseA);
        await EnsureInventoryCreatedAsync(options, context);

        var first = await CreateAndPostOpeningAsync(
            persistence,
            context,
            OpeningCommand(
                "opening-stable-source",
                [OpeningRow(100m, "OPEN-2026-08-LINE-0042")],
                idempotencyKey: "opening-stable-source-a",
                extractedAt: new DateTimeOffset(2026, 8, 21, 10, 0, 0, TimeSpan.Zero)));

        var duplicate = Assert.IsType<InventoryOpeningBalanceRecord>(await persistence.CreateOpeningBalanceAsync(
            context,
            OpeningCommand(
                "opening-stable-source",
                [OpeningRow(100m, "OPEN-2026-08-LINE-0042")],
                idempotencyKey: "opening-stable-source-b",
                extractedAt: new DateTimeOffset(2026, 8, 21, 10, 0, 1, TimeSpan.Zero))));

        Assert.Equal(first.Rows[0].SourceFingerprint, duplicate.Rows[0].SourceFingerprint);
        Assert.Equal(InventoryOpeningRowStatus.Quarantined, duplicate.Rows[0].Status);
        Assert.Equal("duplicate_source_row", duplicate.Rows[0].ValidationCode);

        var validated = Assert.IsType<InventoryOpeningBalanceRecord>(await persistence.ValidateOpeningBalanceAsync(
            context,
            duplicate.Id,
            duplicate.Version,
            Actor,
            "validate duplicate extraction replay",
            "opening-stable-source-b-validate",
            "opening-stable-source-b-validate-key",
            "opening-stable-source-b-validate-fingerprint"));
        Assert.Equal(InventoryOpeningBalanceStatus.Draft, validated.Status);
        Assert.Null(await persistence.PostOpeningBalanceAsync(
            context,
            duplicate.Id,
            validated.Version,
            Actor,
            "block duplicate extraction replay",
            "opening-stable-source-b-post",
            "opening-stable-source-b-post-key",
            "opening-stable-source-b-post-fingerprint"));

        var availability = Assert.IsType<InventoryAvailabilityRecord>(await persistence.GetAvailabilityAsync(
            context, scope, ProductA, UnitA, null, Product(), Warehouse()));
        Assert.Equal(100m, availability.OnHandQuantity);
        Assert.Single(await persistence.ListMovementsAsync(context, scope));
    }

    [Fact]
    public async Task Opening_distinct_source_lines_for_same_stock_identity_post_independently()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder().UseSqlite(connection).Options;
        var context = Context(TenantA, new ScopeReference($"Warehouse:{WarehouseA:D}"));
        var persistence = new InventoryPersistence(options);
        await EnsureInventoryCreatedAsync(options, context);

        var posted = await CreateAndPostOpeningAsync(
            persistence,
            context,
            OpeningCommand(
                "opening-distinct-lines",
                [OpeningRow(4m, "line-1"), OpeningRow(6m, "line-2")],
                idempotencyKey: "opening-distinct-lines-key"));

        Assert.Equal(InventoryOpeningBalanceStatus.Posted, posted.Status);
        Assert.All(posted.Rows, item => Assert.Equal(InventoryOpeningRowStatus.Posted, item.Status));
        Assert.All(posted.Rows, item => Assert.Equal("EA", item.UnitOfMeasureCode));
        var movements = await persistence.ListMovementsAsync(context, new InventoryScope(TenantA, CompanyA, null, WarehouseA));
        Assert.Equal(2, movements.Count);
        Assert.Equal(10m, movements.Sum(item => item.Quantity));
    }

    [Fact]
    public async Task Opening_correction_blocks_when_active_reservations_would_become_unsupported()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder().UseSqlite(connection).Options;
        var context = Context(TenantA, new ScopeReference($"Warehouse:{WarehouseA:D}"));
        var persistence = new InventoryPersistence(options);
        var scope = new InventoryScope(TenantA, CompanyA, null, WarehouseA);
        await EnsureInventoryCreatedAsync(options, context);

        var posted = await CreateAndPostOpeningAsync(
            persistence,
            context,
            OpeningCommand("opening-reserved-correction", [OpeningRow(10m, "line-1")], idempotencyKey: "reserved-opening-key"));
        var reservation = Assert.IsType<InventoryReservationRecord>(await persistence.CreateReservationAsync(
            context,
            ReservationCommand(scope, 7m, "reserved-correction", "reserved-correction-key"),
            10m));

        var correction = await persistence.CorrectOpeningBalanceAsync(
            context,
            posted.Id,
            posted.Version,
            Actor,
            "must preserve reservation",
            "reserved-correction-action",
            "reserved-correction-action-key",
            "reserved-correction-action-fingerprint");
        Assert.Null(correction);
        var availability = Assert.IsType<InventoryAvailabilityRecord>(await persistence.GetAvailabilityAsync(
            context, scope, ProductA, UnitA, null, Product(), Warehouse()));
        Assert.Equal(10m, availability.OnHandQuantity);
        Assert.Equal(7m, availability.ReservedQuantity);
        Assert.Equal(3m, availability.AvailableQuantity);
        Assert.Single(await persistence.ListMovementsAsync(context, scope));
        Assert.Contains(await persistence.ReadOpeningHistoryAsync(context, posted.Id), item => item.Action == "correction-blocked");
        Assert.Contains(await persistence.ReadAuditAsync(context, "opening-balance", posted.Id), item => item.Decision == "Failed");
        Assert.Equal(InventoryReservationStatus.Active, reservation.Status);

        var released = Assert.IsType<InventoryReservationRecord>(await persistence.ReleaseReservationAsync(
            context,
            reservation.Id,
            reservation.Version,
            Actor,
            "release before correction retry",
            "reserved-release",
            "reserved-release-key",
            "reserved-release-fingerprint"));
        Assert.Equal(InventoryReservationStatus.Released, released.Status);

        var corrected = Assert.IsType<InventoryOpeningBalanceRecord>(await persistence.CorrectOpeningBalanceAsync(
            context,
            posted.Id,
            posted.Version,
            Actor,
            "retry after reservation release",
            "reserved-correction-retry",
            "reserved-correction-retry-key",
            "reserved-correction-retry-fingerprint"));
        Assert.Equal(InventoryOpeningBalanceStatus.Corrected, corrected.Status);
        var restored = Assert.IsType<InventoryAvailabilityRecord>(await persistence.GetAvailabilityAsync(
            context, scope, ProductA, UnitA, null, Product(), Warehouse()));
        Assert.Equal(0m, restored.OnHandQuantity);
        Assert.Equal(0m, restored.ReservedQuantity);
        Assert.Equal(0m, restored.AvailableQuantity);
        Assert.Equal(2, (await persistence.ListMovementsAsync(context, scope)).Count);
    }

    [Fact]
    public async Task Opening_correction_reverses_all_rows_cumulatively_for_one_stock_identity()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder().UseSqlite(connection).Options;
        var context = Context(TenantA, new ScopeReference($"Warehouse:{WarehouseA:D}"));
        var persistence = new InventoryPersistence(options);
        var scope = new InventoryScope(TenantA, CompanyA, null, WarehouseA);
        await EnsureInventoryCreatedAsync(options, context);

        var posted = await CreateAndPostOpeningAsync(
            persistence,
            context,
            OpeningCommand(
                "opening-cumulative-correction",
                [OpeningRow(4m, "line-1"), OpeningRow(6m, "line-2")],
                idempotencyKey: "cumulative-opening-key"));
        var corrected = Assert.IsType<InventoryOpeningBalanceRecord>(await persistence.CorrectOpeningBalanceAsync(
            context,
            posted.Id,
            posted.Version,
            Actor,
            "reverse cumulative opening",
            "cumulative-correction",
            "cumulative-correction-key",
            "cumulative-correction-fingerprint"));

        Assert.Equal(InventoryOpeningBalanceStatus.Corrected, corrected.Status);
        var movements = await persistence.ListMovementsAsync(context, scope);
        Assert.Equal(4, movements.Count);
        Assert.Equal(10m, movements.Where(item => item.Direction == InventoryMovementDirection.Inbound).Sum(item => item.Quantity));
        Assert.Equal(10m, movements.Where(item => item.Direction == InventoryMovementDirection.Outbound).Sum(item => item.Quantity));
        Assert.Equal(2, movements.Count(item => item.SourceType == InventoryMovementSourceType.Correction && item.CorrectionOfMovementId.HasValue));
        var availability = Assert.IsType<InventoryAvailabilityRecord>(await persistence.GetAvailabilityAsync(
            context, scope, ProductA, UnitA, null, Product(), Warehouse()));
        Assert.Equal(0m, availability.OnHandQuantity);
        Assert.Equal(0m, availability.ReservedQuantity);
        Assert.Equal(0m, availability.AvailableQuantity);
    }

    [Fact]
    public async Task Concurrent_reservations_use_independent_contexts_without_over_allocation()
    {
        var databasePath = Path.Combine(Path.GetTempPath(), $"mesp-inventory-{Guid.NewGuid():N}.db");
        try
        {
            var connectionString = $"Data Source={databasePath};Cache=Shared;Pooling=False;Default Timeout=30";
            var options = new DbContextOptionsBuilder().UseSqlite(connectionString).Options;
            var context = Context(TenantA, new ScopeReference($"Warehouse:{WarehouseA:D}"));
            var scope = new InventoryScope(TenantA, CompanyA, null, WarehouseA);
            await EnsureInventoryCreatedAsync(options, context);
            await using (var db = new InventoryDbContext(options, context.TenantContext))
            {
                db.StockMovements.Add(new InventoryStockMovementEntity(
                    new TenantId(TenantA), Guid.NewGuid(), CompanyA, null, WarehouseA, "WH-A", "Warehouse A",
                    ProductA, "SKU-A", "Product A", UnitA, "EA", InventoryMovementDirection.Inbound, 10m, 1m, "SAR", null,
                    InventoryMovementSourceType.OpeningBalance, Guid.NewGuid(), Guid.NewGuid(), null,
                    DateOnly.FromDateTime(DateTime.UtcNow), Actor, "concurrent-seed", DateTimeOffset.UtcNow));
                await db.SaveChangesAsync();
            }

            using var barrier = new Barrier(2);
            var firstPersistence = new InventoryPersistence(options);
            var secondPersistence = new InventoryPersistence(options);
            var firstTask = Task.Run(async () =>
            {
                barrier.SignalAndWait();
                return await firstPersistence.CreateReservationAsync(
                    context,
                    ReservationCommand(scope, 7m, "concurrent-1", "concurrent-key-1"),
                    10m);
            });
            var secondTask = Task.Run(async () =>
            {
                barrier.SignalAndWait();
                return await secondPersistence.CreateReservationAsync(
                    context,
                    ReservationCommand(scope, 7m, "concurrent-2", "concurrent-key-2"),
                    10m);
            });

            InventoryReservationRecord?[] results;
            try
            {
                results = await Task.WhenAll(firstTask, secondTask);
            }
            catch (Exception exception)
            {
                Assert.Fail($"Concurrent reservation persistence must fail closed, not throw: {exception}");
                return;
            }

            Assert.Contains(results, item => item is not null);
            var reservations = await firstPersistence.ListReservationsAsync(context, scope);
            Assert.True(reservations.Sum(item => item.ReservedQuantity) <= 10m);
            var availability = Assert.IsType<InventoryAvailabilityRecord>(await firstPersistence.GetAvailabilityAsync(
                context, scope, ProductA, UnitA, null, Product(), Warehouse()));
            Assert.True(availability.ReservedQuantity <= availability.OnHandQuantity);
            Assert.True(availability.AvailableQuantity >= 0m);
        }
        finally
        {
            if (File.Exists(databasePath))
            {
                File.Delete(databasePath);
            }
        }
    }

    [Fact]
    public async Task Opening_source_fingerprint_index_rejects_two_consumed_rows_in_one_database_write()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder().UseSqlite(connection).Options;
        var context = Context(TenantA, new ScopeReference($"Warehouse:{WarehouseA:D}"));
        var persistence = new InventoryPersistence(options);
        await EnsureInventoryCreatedAsync(options, context);

        var first = Assert.IsType<InventoryOpeningBalanceRecord>(await persistence.CreateOpeningBalanceAsync(
            context,
            OpeningCommand("race-source", [OpeningRow(2m, "line-1")], idempotencyKey: "race-source-1")));
        var second = Assert.IsType<InventoryOpeningBalanceRecord>(await persistence.CreateOpeningBalanceAsync(
            context,
            OpeningCommand("race-source", [OpeningRow(3m, "line-1")], idempotencyKey: "race-source-2")));
        Assert.Equal(first.Rows[0].SourceFingerprint, second.Rows[0].SourceFingerprint);

        await using var db = new InventoryDbContext(options, context.TenantContext);
        var rows = await db.OpeningBalanceRows
            .Where(item => item.SourceFingerprint == first.Rows[0].SourceFingerprint)
            .ToListAsync();
        Assert.Equal(2, rows.Count);
        foreach (var row in rows)
        {
            row.MarkPosted(DateTimeOffset.UtcNow);
        }

        await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
    }

    [Fact]
    public async Task Existing_concurrency_anchor_touch_persists_a_real_mutable_field_on_sqlite()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder().UseSqlite(connection).Options;
        var context = Context(TenantA, new ScopeReference($"Warehouse:{WarehouseA:D}"));
        await EnsureInventoryCreatedAsync(options, context);

        var anchorId = Guid.NewGuid();
        await using (var createDb = new InventoryDbContext(options, context.TenantContext))
        {
            var anchor = new InventoryConcurrencyAnchorEntity(
                new TenantId(TenantA),
                anchorId,
                CompanyA,
                null,
                WarehouseA,
                ProductA,
                UnitA,
                string.Empty);
            anchor.Touch();
            createDb.ConcurrencyAnchors.Add(anchor);
            await createDb.SaveChangesAsync();
        }

        long beforeSequence;
        byte[] beforeVersion;
        await using (var readDb = new InventoryDbContext(options, context.TenantContext))
        {
            var anchor = await readDb.ConcurrencyAnchors.SingleAsync(item => item.Id == anchorId);
            beforeSequence = anchor.TouchSequence;
            beforeVersion = anchor.Version.ToArray();
            anchor.Touch();
            await readDb.SaveChangesAsync();
        }

        await using var verifyDb = new InventoryDbContext(options, context.TenantContext);
        var persisted = await verifyDb.ConcurrencyAnchors.AsNoTracking().SingleAsync(item => item.Id == anchorId);
        Assert.Equal(beforeSequence + 1, persisted.TouchSequence);
        Assert.NotEqual(beforeVersion, persisted.Version);
    }

    [Fact]
    public async Task Master_data_product_provider_persists_authoritative_active_uom_code()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder().UseSqlite(connection).Options;
        var tenantContext = TenantContext.ForOrdinaryMembership(new TenantId(TenantA), new MembershipReference(Guid.NewGuid()), actorId: Actor);
        var categoryId = Guid.NewGuid();
        await using (var db = new MasterDataDbContext(options, tenantContext))
        {
            await db.Database.EnsureCreatedAsync();
            db.Categories.Add(new MasterDataCategoryEntity(categoryId, new TenantId(TenantA), "GENERAL", new LocalizedName("General", null), null));
            db.UnitsOfMeasure.Add(new MasterDataUnitOfMeasureEntity(UnitA, new TenantId(TenantA), "EA", new LocalizedName("Each", null)));
            db.Products.Add(new MasterDataProductEntity(
                ProductA,
                new TenantId(TenantA),
                "SKU-A",
                new LocalizedName("Product A", null),
                null,
                categoryId,
                UnitA,
                null,
                true,
                true,
                true));
            await db.SaveChangesAsync();
        }

        var provider = new MasterDataInventoryProductProvider(
            new MasterDataCatalogPersistence(options),
            new MasterDataCatalogPersistence(options));
        var context = Context(TenantA, null);
        var product = await provider.FindAsync(context, ProductA);

        Assert.NotNull(product);
        Assert.Equal("EA", product.BaseUnitOfMeasureCode);
        Assert.Equal(UnitA, product.BaseUnitOfMeasureId);
        Assert.True(product.IsInventoryRelevant);
    }

    [Fact]
    public async Task Inventory_service_tracking_boolean_fails_closed_for_both_modes()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder().UseSqlite(connection).Options;
        var warehouseProvider = new ConfiguredInventoryWarehouseProvider([Warehouse()]);
        var context = Context(TenantA, new ScopeReference($"Warehouse:{WarehouseA:D}"), "tenant.inventory.reservation.create");
        var request = new InventoryReservationCreateRequest(
            CompanyA,
            null,
            WarehouseA,
            ProductA,
            UnitA,
            1m,
            "Demand",
            "TRACKING-TEST",
            true);

        var trackingEnabledService = new InventoryService(
            new InventoryPersistence(options),
            new InventoryResourceAuthorizationService(),
            warehouseProvider,
            new StaticInventoryProductProvider(Product() with { TrackingEnabled = true }));
        var required = await trackingEnabledService.CreateReservationAsync(context, request, "tracking-required-key");
        Assert.False(required.Succeeded);
        Assert.Equal("tracking_identity_required", required.Code);

        var trackingDisabledService = new InventoryService(
            new InventoryPersistence(options),
            new InventoryResourceAuthorizationService(),
            warehouseProvider,
            new StaticInventoryProductProvider(Product() with { TrackingEnabled = false }));
        var rejected = await trackingDisabledService.CreateReservationAsync(
            context,
            request with { TrackingIdentity = "SERIAL-1" },
            "tracking-disabled-key");
        Assert.False(rejected.Succeeded);
        Assert.Equal("tracking_not_enabled", rejected.Code);
    }

    [Fact]
    public async Task Goods_receipt_posts_accepted_quantity_once_with_pending_valuation()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder().UseSqlite(connection).Options;
        var context = Context(TenantA, new ScopeReference($"Warehouse:{WarehouseA:D}"));
        var persistence = new InventoryPersistence(options);
        var scope = new PurchaseRequestScope(TenantA, CompanyA, null);
        var receiptId = Guid.NewGuid();
        var receiptLineId = Guid.NewGuid();
        var purchaseOrderId = Guid.NewGuid();
        var purchaseOrderLineId = Guid.NewGuid();
        var line = new GoodsReceiptLineRecord(receiptLineId, purchaseOrderLineId, ProductA, "SKU-A", "Product A", "EA", 10m, 10m, 8m, 2m, null, null, 0m, null);
        var receipt = new GoodsReceiptRecord(receiptId, TenantA, scope, purchaseOrderId, WarehouseA, Actor, GoodsReceiptStatus.Recorded, Guid.NewGuid(), "SUP-001", "Supplier One", new DateOnly(2026, 8, 22), "GR-129", null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null, null, [line], [1]);
        var source = new InventoryGoodsReceiptSourceRecord(receipt, line, Product(), Warehouse(), UnitA);
        await EnsureInventoryCreatedAsync(options, context);

        var first = Assert.IsType<InventoryGoodsReceiptPostingRecord>(await persistence.PostGoodsReceiptAsync(
            context,
            new InventoryGoodsReceiptPostCommand(Guid.NewGuid(), source, Actor, DateTimeOffset.UtcNow, "goods-receipt-post", "goods-receipt-key-1", "goods-receipt-fingerprint-1")));
        var replay = Assert.IsType<InventoryGoodsReceiptPostingRecord>(await persistence.PostGoodsReceiptAsync(
            context,
            new InventoryGoodsReceiptPostCommand(Guid.NewGuid(), source, Actor, DateTimeOffset.UtcNow, "goods-receipt-replay", "goods-receipt-key-2", "goods-receipt-fingerprint-2")));

        Assert.Equal(8m, first.Quantity);
        Assert.Equal(InventoryValuationStatus.Pending, first.ValuationStatus);
        Assert.True(replay.WasExisting);
        Assert.Equal(first.MovementId, replay.MovementId);
        Assert.True(await persistence.HasActiveGoodsReceiptEffectAsync(context.TenantContext, receiptId));
        var movements = await persistence.ListMovementsAsync(context, new InventoryScope(TenantA, CompanyA, null, WarehouseA));
        var movement = Assert.Single(movements);
        Assert.Equal(8m, movement.Quantity);
        Assert.Equal(InventoryMovementSourceType.GoodsReceipt, movement.SourceType);
        Assert.Null(movement.UnitCost);
        Assert.Null(movement.CurrencyCode);
    }

    [Fact]
    public async Task Supplier_return_posts_outbound_once_with_full_source_lineage()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder().UseSqlite(connection).Options;
        var context = Context(TenantA, new ScopeReference($"Warehouse:{WarehouseA:D}"));
        var persistence = new InventoryPersistence(options);
        var scope = new PurchaseRequestScope(TenantA, CompanyA, null);
        var receiptId = Guid.NewGuid();
        var receiptLineId = Guid.NewGuid();
        var purchaseOrderId = Guid.NewGuid();
        var purchaseOrderLineId = Guid.NewGuid();
        var receiptLine = new GoodsReceiptLineRecord(receiptLineId, purchaseOrderLineId, ProductA, "SKU-A", "Product A", "EA", 10m, 10m, 10m, 0m, null, null, 0m, null);
        var receipt = new GoodsReceiptRecord(receiptId, TenantA, scope, purchaseOrderId, WarehouseA, Actor, GoodsReceiptStatus.Recorded, Guid.NewGuid(), "SUP-001", "Supplier One", new DateOnly(2026, 8, 22), "GR-129-SR", null, DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null, null, [receiptLine], [1]);
        var receiptSource = new InventoryGoodsReceiptSourceRecord(receipt, receiptLine, Product(), Warehouse(), UnitA);
        await EnsureInventoryCreatedAsync(options, context);
        await persistence.PostGoodsReceiptAsync(context, new InventoryGoodsReceiptPostCommand(Guid.NewGuid(), receiptSource, Actor, DateTimeOffset.UtcNow, "supplier-return-seed", "supplier-return-seed-key", "supplier-return-seed-fingerprint"));

        var supplierReturnId = Guid.NewGuid();
        var supplierReturnLineId = Guid.NewGuid();
        var returnLine = new SupplierReturnLineRecord(supplierReturnLineId, receiptLineId, purchaseOrderLineId, ProductA, "SKU-A", "Product A", "EA", 4m, 4m, 0m, null);
        var supplierReturn = new SupplierReturnRecord(supplierReturnId, TenantA, scope, receiptId, purchaseOrderId, null, WarehouseA, Guid.NewGuid(), "SUP-001", "Supplier One", "SAR", SupplierReturnStatus.AwaitingInventory, SupplierReturnReasonCode.Damaged, SupplierReturnCondition.Unusable, SupplierReturnCommercialOutcome.CreditExpected, "Damaged", null, new DateOnly(2026, 8, 22), DateTimeOffset.UtcNow, DateTimeOffset.UtcNow, null, null, null, null, null, null, null, null, [returnLine], [], [1]);
        var source = new InventorySupplierReturnSourceRecord(supplierReturn, receipt, Warehouse(), [new InventorySupplierReturnLineSourceRecord(returnLine, receiptLine, Product(), UnitA)]);

        var first = Assert.IsType<InventorySupplierReturnPostingRecord>(await persistence.PostSupplierReturnAsync(
            context,
            new InventorySupplierReturnPostCommand(Guid.NewGuid(), source, Actor, DateTimeOffset.UtcNow, "supplier-return-post", "supplier-return-key-1", "supplier-return-fingerprint-1")));
        var replay = Assert.IsType<InventorySupplierReturnPostingRecord>(await persistence.PostSupplierReturnAsync(
            context,
            new InventorySupplierReturnPostCommand(Guid.NewGuid(), source, Actor, DateTimeOffset.UtcNow, "supplier-return-replay", "supplier-return-key-2", "supplier-return-fingerprint-2")));

        Assert.Equal(4m, first.Quantity);
        Assert.Equal(InventoryValuationStatus.Pending, first.ValuationStatus);
        Assert.False(first.WasExisting);
        Assert.True(replay.WasExisting);
        Assert.Equal(first.MovementIds, replay.MovementIds);
        var movements = await persistence.ListMovementsAsync(context, new InventoryScope(TenantA, CompanyA, null, WarehouseA));
        var outbound = Assert.Single(movements, item => item.SourceType == InventoryMovementSourceType.SupplierReturn);
        Assert.Equal(4m, outbound.Quantity);
        Assert.Equal(supplierReturnId, outbound.SupplierReturnId);
        Assert.Equal(supplierReturnLineId, outbound.SupplierReturnLineId);
        Assert.Equal(receiptId, outbound.GoodsReceiptId);
        Assert.Equal(receiptLineId, outbound.GoodsReceiptLineId);
        Assert.Equal(purchaseOrderId, outbound.PurchaseOrderId);
        Assert.Equal(purchaseOrderLineId, outbound.PurchaseOrderLineId);
        Assert.Null(outbound.UnitCost);
        Assert.Null(outbound.CurrencyCode);
    }

    [Fact]
    public async Task Direct_transfer_posts_two_balanced_immutable_movements_with_pending_valuation()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder().UseSqlite(connection).Options;
        var context = Context(TenantA, new ScopeReference($"Warehouse:{WarehouseA:D}"));
        var persistence = new InventoryPersistence(options);
        await EnsureInventoryCreatedAsync(options, context);
        await SeedStockAsync(options, context, WarehouseA, 10m);

        var source = Warehouse(TenantA, WarehouseA);
        var destination = Warehouse(TenantA, WarehouseB);
        var created = Assert.IsType<InventoryTransferRecord>(await persistence.CreateTransferAsync(
            context,
            TransferCreateCommand(Guid.NewGuid(), source, destination, InventoryTransferMode.Direct, 10m, "direct-key")));

        var completed = Assert.IsType<InventoryTransferRecord>(await persistence.PostDirectTransferAsync(
            context,
            TransferActionCommand(created.Id, created.Version, "direct-action-key")));
        Assert.Equal(InventoryTransferStatus.Completed, completed.Status);
        Assert.Equal(10m, completed.ShippedQuantity);
        Assert.Equal(10m, completed.ReceivedQuantity);
        Assert.Equal(0m, completed.InTransitQuantity);

        var movements = await persistence.ListMovementsAsync(context, null);
        Assert.Equal(3, movements.Count);
        var outbound = Assert.Single(movements, item => item.SourceType == InventoryMovementSourceType.WarehouseTransferShipment);
        var inbound = Assert.Single(movements, item => item.SourceType == InventoryMovementSourceType.WarehouseTransferReceipt);
        Assert.Equal(10m, outbound.Quantity);
        Assert.Equal(10m, inbound.Quantity);
        Assert.Equal(InventoryValuationStatus.Pending, outbound.ValuationStatus);
        Assert.Equal(InventoryValuationStatus.Pending, inbound.ValuationStatus);
        Assert.Null(outbound.UnitCost);
        Assert.Equal(completed.Id, outbound.TransferId);
        Assert.Equal(completed.Id, inbound.TransferId);

        var sourceAvailability = Assert.IsType<InventoryAvailabilityRecord>(await persistence.GetAvailabilityAsync(context, new InventoryScope(TenantA, CompanyA, null, WarehouseA), ProductA, UnitA, null, Product(), source));
        var destinationAvailability = Assert.IsType<InventoryAvailabilityRecord>(await persistence.GetAvailabilityAsync(context, new InventoryScope(TenantA, CompanyA, null, WarehouseB), ProductA, UnitA, null, Product(), destination));
        Assert.Equal(0m, sourceAvailability.OnHandQuantity);
        Assert.Equal(10m, destinationAvailability.OnHandQuantity);
    }

    [Fact]
    public async Task In_transit_transfer_supports_partial_receipt_shortage_and_rejects_overage()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder().UseSqlite(connection).Options;
        var context = Context(TenantA, new ScopeReference($"Warehouse:{WarehouseA:D}"));
        var persistence = new InventoryPersistence(options);
        await EnsureInventoryCreatedAsync(options, context);
        await SeedStockAsync(options, context, WarehouseA, 10m);

        var created = Assert.IsType<InventoryTransferRecord>(await persistence.CreateTransferAsync(
            context,
            TransferCreateCommand(Guid.NewGuid(), Warehouse(TenantA, WarehouseA), Warehouse(TenantA, WarehouseB), InventoryTransferMode.InTransit, 10m, "transit-key")));
        var shipped = Assert.IsType<InventoryTransferRecord>(await persistence.ShipTransferAsync(
            context,
            TransferActionCommand(created.Id, created.Version, "ship-key")));
        Assert.Equal(InventoryTransferStatus.Shipped, shipped.Status);

        var received = Assert.IsType<InventoryTransferRecord>(await persistence.ReceiveTransferAsync(
            context,
            TransferActionCommand(shipped.Id, shipped.Version, "receive-key", 4m, "receipt-1")));
        Assert.Equal(InventoryTransferStatus.PartiallyReceived, received.Status);
        Assert.Equal(6m, received.InTransitQuantity);
        Assert.Null(await persistence.ReceiveTransferAsync(
            context,
            TransferActionCommand(received.Id, received.Version, "overage-key", 7m, "receipt-over")));

        var secondReceipt = Assert.IsType<InventoryTransferRecord>(await persistence.ReceiveTransferAsync(
            context,
            TransferActionCommand(received.Id, received.Version, "receive-key-2", 3m, "receipt-2")));
        Assert.Equal(InventoryTransferStatus.PartiallyReceived, secondReceipt.Status);
        Assert.Equal(3m, secondReceipt.InTransitQuantity);

        var destination = Assert.IsType<InventoryAvailabilityRecord>(await persistence.GetAvailabilityAsync(context, new InventoryScope(TenantA, CompanyA, null, WarehouseB), ProductA, UnitA, null, Product(), Warehouse(TenantA, WarehouseB)));
        Assert.Equal(7m, destination.OnHandQuantity);
        Assert.Equal(3m, destination.InTransitQuantity);

        var lossResolved = Assert.IsType<InventoryTransferRecord>(await persistence.ResolveTransferShortageAsync(
            context,
            TransferActionCommand(secondReceipt.Id, secondReceipt.Version, "loss-key", null, "loss-1")));
        Assert.Equal(InventoryTransferStatus.LossResolved, lossResolved.Status);
        Assert.Equal(3m, lossResolved.LostQuantity);
        Assert.Equal(0m, lossResolved.InTransitQuantity);

        var movements = await persistence.ListMovementsAsync(context, null);
        Assert.Equal(4, movements.Count);
        Assert.Single(movements, item => item.Direction == InventoryMovementDirection.Outbound && item.Quantity == 10m);
        Assert.Equal(2, movements.Count(item => item.SourceType == InventoryMovementSourceType.WarehouseTransferReceipt));
        Assert.Equal(7m, movements.Where(item => item.SourceType == InventoryMovementSourceType.WarehouseTransferReceipt).Sum(item => item.Quantity));
        var history = await persistence.ReadTransferHistoryAsync(context, created.Id);
        Assert.Contains(history, item => item.EventType == InventoryTransferEventType.Shipped);
        Assert.Contains(history, item => item.EventType == InventoryTransferEventType.Received && item.Quantity == 4m);
        Assert.Contains(history, item => item.EventType == InventoryTransferEventType.Received && item.Quantity == 3m);
        Assert.Contains(history, item => item.EventType == InventoryTransferEventType.ShortageResolved && item.Quantity == 3m);
    }

    [Fact]
    public async Task Draft_transfer_can_be_cancelled_but_shipped_transfer_cannot()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder().UseSqlite(connection).Options;
        var context = Context(TenantA, new ScopeReference($"Warehouse:{WarehouseA:D}"));
        var persistence = new InventoryPersistence(options);
        await EnsureInventoryCreatedAsync(options, context);
        await SeedStockAsync(options, context, WarehouseA, 5m);

        var draft = Assert.IsType<InventoryTransferRecord>(await persistence.CreateTransferAsync(
            context,
            TransferCreateCommand(Guid.NewGuid(), Warehouse(TenantA, WarehouseA), Warehouse(TenantA, WarehouseB), InventoryTransferMode.InTransit, 5m, "cancel-key")));
        var cancelled = Assert.IsType<InventoryTransferRecord>(await persistence.CancelTransferAsync(
            context,
            TransferActionCommand(draft.Id, draft.Version, "cancel-action-key", null, "cancel-before-shipment")));
        Assert.Equal(InventoryTransferStatus.Cancelled, cancelled.Status);
        Assert.Null(await persistence.ShipTransferAsync(context, TransferActionCommand(cancelled.Id, cancelled.Version, "ship-cancelled-key")));

        var movements = await persistence.ListMovementsAsync(context, null);
        Assert.Single(movements);
        Assert.Equal(5m, movements[0].Quantity);
    }

    private static async Task EnsureInventoryCreatedAsync(DbContextOptions options, InventoryRequestContext context)
    {
        await using var db = new InventoryDbContext(options, context.TenantContext);
        await db.Database.EnsureCreatedAsync();
    }

    private static async Task SeedStockAsync(DbContextOptions options, InventoryRequestContext context, Guid warehouseId, decimal quantity)
    {
        var warehouse = Warehouse(TenantA, warehouseId);
        await using var db = new InventoryDbContext(options, context.TenantContext);
        db.StockMovements.Add(new InventoryStockMovementEntity(
            new TenantId(TenantA), Guid.NewGuid(), CompanyA, null, warehouseId, warehouse.Code, warehouse.Name,
            ProductA, Product().Sku, Product().Name, UnitA, "EA", InventoryMovementDirection.Inbound,
            quantity, 10m, "SAR", null, InventoryMovementSourceType.OpeningBalance, Guid.NewGuid(), Guid.NewGuid(), null,
            new DateOnly(2026, 8, 21), Actor, "seed-stock", DateTimeOffset.UtcNow));
        await db.SaveChangesAsync();
    }

    private static InventoryTransferCreateCommand TransferCreateCommand(
        Guid id,
        InventoryWarehouseOption source,
        InventoryWarehouseOption destination,
        InventoryTransferMode mode,
        decimal quantity,
        string key) => new(
        id,
        new InventoryScope(TenantA, CompanyA, null, source.WarehouseId),
        source,
        destination,
        ProductA,
        UnitA,
        Product(),
        quantity,
        mode,
        null,
        "test transfer",
        Actor,
        DateTimeOffset.UtcNow,
        $"transfer-{key}",
        key,
        $"fingerprint-{key}");

    private static InventoryTransferActionCommand TransferActionCommand(
        Guid transferId,
        byte[] expectedVersion,
        string key,
        decimal? quantity = null,
        string? reference = null) => new(
        transferId,
        expectedVersion,
        quantity,
        reference,
        "test action",
        Actor,
        DateTimeOffset.UtcNow,
        $"transfer-action-{key}",
        key,
        $"fingerprint-{key}");

    private static async Task<InventoryOpeningBalanceRecord> CreateAndPostOpeningAsync(
        InventoryPersistence persistence,
        InventoryRequestContext context,
        InventoryOpeningBalanceCommand command)
    {
        var created = Assert.IsType<InventoryOpeningBalanceRecord>(await persistence.CreateOpeningBalanceAsync(context, command));
        var validated = Assert.IsType<InventoryOpeningBalanceRecord>(await persistence.ValidateOpeningBalanceAsync(
            context, created.Id, created.Version, Actor, "validate", $"validate-{command.Id:N}", $"validate-{command.Id:N}", $"validate-{command.Id:N}"));
        return Assert.IsType<InventoryOpeningBalanceRecord>(await persistence.PostOpeningBalanceAsync(
            context, validated.Id, validated.Version, Actor, "post", $"post-{command.Id:N}", $"post-{command.Id:N}", $"post-{command.Id:N}"));
    }

    private static InventoryOpeningBalanceCommand OpeningCommand(
        string sourceReference,
        IReadOnlyList<InventoryOpeningBalanceRowCommand> rows,
        Guid tenantId = default,
        Guid warehouseId = default,
        Guid? id = null,
        string? idempotencyKey = null,
        DateTimeOffset? extractedAt = null)
    {
        tenantId = tenantId == Guid.Empty ? TenantA : tenantId;
        warehouseId = warehouseId == Guid.Empty ? WarehouseA : warehouseId;
        var warehouse = Warehouse(tenantId, warehouseId);
        return new InventoryOpeningBalanceCommand(
            id ?? Guid.NewGuid(),
            new InventoryScope(tenantId, CompanyA, null, warehouseId),
            warehouse.Code,
            warehouse.Name,
            new DateOnly(2026, 8, 21),
            "Inventory Operations",
            "Inventory Import",
            extractedAt ?? new DateTimeOffset(2026, 8, 21, 8, 0, 0, TimeSpan.Zero),
            sourceReference,
            rows,
            Actor,
            new DateTimeOffset(2026, 8, 21, 8, 5, 0, TimeSpan.Zero),
            $"opening-{Guid.NewGuid():N}",
            idempotencyKey,
            $"request-{Guid.NewGuid():N}");
    }

    private static InventoryOpeningBalanceRowCommand OpeningRow(
        decimal quantity,
        string? sourceLineReference,
        Guid tenantId = default,
        Guid productId = default,
        Guid unitOfMeasureId = default,
        InventoryProductReference? product = null,
        string? validationCode = null) => new(
        Guid.NewGuid(),
        productId == Guid.Empty ? ProductA : productId,
        unitOfMeasureId == Guid.Empty ? UnitA : unitOfMeasureId,
        quantity,
        10m,
        "SAR",
        null,
        sourceLineReference,
        product ?? Product(tenantId == Guid.Empty ? TenantA : tenantId, productId == Guid.Empty ? ProductA : productId, unitOfMeasureId == Guid.Empty ? UnitA : unitOfMeasureId),
        validationCode);

    private static InventoryReservationCommand ReservationCommand(
        InventoryScope scope,
        decimal requestedQuantity,
        string sourceReference,
        string idempotencyKey,
        string? trackingIdentity = null) => new(
        Guid.NewGuid(),
        scope,
        ProductA,
        UnitA,
        requestedQuantity,
        "Demand",
        sourceReference,
        true,
        trackingIdentity,
        Product(scope.TenantId),
        Warehouse(scope.TenantId, scope.WarehouseId).Code,
        Warehouse(scope.TenantId, scope.WarehouseId).Name,
        Actor,
        DateTimeOffset.UtcNow,
        $"reservation-{Guid.NewGuid():N}",
        idempotencyKey,
        $"fingerprint-{idempotencyKey}");

    private static InventoryProductReference Product(
        Guid tenantId = default,
        Guid productId = default,
        Guid unitOfMeasureId = default) => new(
        tenantId == Guid.Empty ? TenantA : tenantId,
        productId == Guid.Empty ? ProductA : productId,
        "SKU-A",
        "Product A",
        unitOfMeasureId == Guid.Empty ? UnitA : unitOfMeasureId,
        "EA",
        true,
        true,
        false);

    private static InventoryWarehouseOption Warehouse(Guid tenantId = default, Guid warehouseId = default) => new(
        tenantId == Guid.Empty ? TenantA : tenantId,
        CompanyA,
        null,
        warehouseId == Guid.Empty ? WarehouseA : warehouseId,
        warehouseId == WarehouseB ? "WH-B" : "WH-A",
        warehouseId == WarehouseB ? "Warehouse B" : "Warehouse A");

    private static InventoryRequestContext Context(Guid tenantId, ScopeReference? scope, string permission = "tenant.inventory.ledger.view") =>
        new InventoryTenantContextResolver().Resolve(
            FoundationRequestContext.ForTenant(
                Actor,
                Guid.Parse("55555555-5555-5555-5555-555555555555"),
                TenantContext.ForOrdinaryMembership(new TenantId(tenantId), new MembershipReference(Guid.NewGuid()), scope, actorId: Actor),
                permission)).Context!;

    private sealed class StaticInventoryProductProvider(InventoryProductReference product) : IInventoryProductProvider
    {
        public Task<InventoryProductReference?> FindAsync(InventoryRequestContext context, Guid productId, CancellationToken cancellationToken = default) =>
            Task.FromResult<InventoryProductReference?>(product.TenantId == context.TenantId.Value && product.ProductId == productId ? product : null);
    }
}
