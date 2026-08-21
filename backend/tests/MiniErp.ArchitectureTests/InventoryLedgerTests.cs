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
        Assert.Single(await persistence.ListReservationsAsync(context, scope));

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

    private static InventoryProductReference Product() => new(TenantA, ProductA, "SKU-A", "Product A", UnitA, "EA", true, true, false);

    private static InventoryWarehouseOption Warehouse() => new(TenantA, CompanyA, null, WarehouseA, "WH-A", "Warehouse A");

    private static InventoryRequestContext Context(Guid tenantId, ScopeReference? scope) =>
        new InventoryTenantContextResolver().Resolve(
            FoundationRequestContext.ForTenant(
                Actor,
                Guid.Parse("55555555-5555-5555-5555-555555555555"),
                TenantContext.ForOrdinaryMembership(new TenantId(tenantId), new MembershipReference(Guid.NewGuid()), scope, actorId: Actor),
                "tenant.inventory.ledger.view")).Context!;
}
