using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MiniErp.App.BuildingBlocks.Rest;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.Modules.MasterData;
using MiniErp.Contracts.Modules.Audit;
using MiniErp.Contracts.Modules.Foundation;
using MiniErp.Contracts.Modules.MasterData;
using MiniErp.Infrastructure.Persistence.Modules.MasterData;
using Xunit;

namespace MiniErp.ArchitectureTests;

public sealed class ProductIdentityTests
{
    private static readonly Guid TenantA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid TenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid ActorA = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid SessionA = Guid.Parse("44444444-4444-4444-4444-444444444444");

    [Fact]
    public async Task Product_create_is_active_tenant_unique_and_uses_product_scope_with_tracking_default()
    {
        await using var fixture = await Fixture.CreateAsync();
        var category = await fixture.CreateCategoryAsync(
            fixture.ContextA,
            "PLUMBING",
            trackingDefaultEnabled: true);
        var unit = await fixture.CreateUnitAsync(fixture.ContextA, "EA");

        var result = await fixture.ProductService.CreateProductAsync(
            fixture.ContextA,
            Product("sku-001", category.Id, unit.Id, ["bar-001"]));

        Assert.True(result.Succeeded, result.Code);
        var product = Assert.IsType<ProductIdentityRecord>(result.Value);
        Assert.Equal("sku-001", product.Sku);
        Assert.Equal(MasterDataLifecycleState.Active, product.LifecycleState);
        Assert.True(product.TrackingDefaultEnabled);
        Assert.True(product.TrackingEnabled);
        Assert.Null(product.TrackingEnabledOverride);
        Assert.Single(product.Barcodes);

        var audit = await fixture.Persistence.ReadAuditHistoryAsync(
            fixture.TenantContextA,
            MasterDataResourceKind.Product,
            product.Id);
        var entry = Assert.Single(audit);
        Assert.Equal("master-data.product.scope", entry.Scope!.Policy.PolicyId);
        Assert.Equal(1, entry.Scope.Policy.Version);
        Assert.Equal(MasterDataResourceKind.Product, entry.ResourceKind);
        Assert.Equal(MasterDataOperation.Create, entry.Operation);
        Assert.Contains("tracking-default=True", entry.AfterSummary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Product_override_is_distinguishable_and_category_default_does_not_mutate_inventory_behavior()
    {
        await using var fixture = await Fixture.CreateAsync();
        var category = await fixture.CreateCategoryAsync(
            fixture.ContextA,
            "TRACKED",
            trackingDefaultEnabled: true);
        var unit = await fixture.CreateUnitAsync(fixture.ContextA, "EA");

        var result = await fixture.ProductService.CreateProductAsync(
            fixture.ContextA,
            Product("sku-override", category.Id, unit.Id, [], trackingOverride: false));

        Assert.True(result.Succeeded, result.Code);
        var product = result.Value!;
        Assert.True(product.TrackingDefaultEnabled);
        Assert.False(product.TrackingEnabled);
        Assert.False(product.TrackingEnabledOverride);
    }

    [Fact]
    public async Task Same_tenant_sku_and_barcode_duplicates_fail_but_other_tenant_can_reuse_them()
    {
        await using var fixture = await Fixture.CreateAsync();
        var categoryA = await fixture.CreateCategoryAsync(fixture.ContextA, "A");
        var unitA = await fixture.CreateUnitAsync(fixture.ContextA, "EA");
        var first = await fixture.ProductService.CreateProductAsync(
            fixture.ContextA,
            Product("same-sku", categoryA.Id, unitA.Id, ["same-barcode"]));
        Assert.True(first.Succeeded, first.Code);

        var duplicate = await fixture.ProductService.CreateProductAsync(
            fixture.ContextA,
            Product(" SAME-SKU ", categoryA.Id, unitA.Id, ["SAME-BARCODE"]));
        Assert.False(duplicate.Succeeded);
        Assert.Equal("product_duplicate", duplicate.Code);

        var categoryB = await fixture.CreateCategoryAsync(fixture.ContextB, "B");
        var unitB = await fixture.CreateUnitAsync(fixture.ContextB, "EA");
        var otherTenant = await fixture.ProductService.CreateProductAsync(
            fixture.ContextB,
            Product("same-sku", categoryB.Id, unitB.Id, ["same-barcode"]));
        Assert.True(otherTenant.Succeeded, otherTenant.Code);
        Assert.Equal(TenantB, otherTenant.Value!.TenantId.Value);
    }

    [Fact]
    public async Task Foreign_or_inactive_references_fail_closed_without_creating_a_product()
    {
        await using var fixture = await Fixture.CreateAsync();
        var categoryA = await fixture.CreateCategoryAsync(fixture.ContextA, "A");
        var unitA = await fixture.CreateUnitAsync(fixture.ContextA, "EA");
        var categoryB = await fixture.CreateCategoryAsync(fixture.ContextB, "B");
        var unitB = await fixture.CreateUnitAsync(fixture.ContextB, "EA");

        var foreign = await fixture.ProductService.CreateProductAsync(
            fixture.ContextA,
            Product("foreign-ref", categoryB.Id, unitB.Id, []));
        Assert.False(foreign.Succeeded);
        Assert.Equal("product_reference_invalid", foreign.Code);

        var deactivated = await fixture.CategoryUomService.DeactivateCategoryAsync(
            fixture.ContextA,
            categoryA.Id,
            categoryA.Version);
        Assert.True(deactivated.Succeeded, deactivated.Code);

        var inactive = await fixture.ProductService.CreateProductAsync(
            fixture.ContextA,
            Product("inactive-ref", categoryA.Id, unitA.Id, []));
        Assert.False(inactive.Succeeded);
        Assert.Equal("product_reference_invalid", inactive.Code);
        var products = await fixture.ProductService.ListProductsAsync(fixture.ContextA);
        Assert.True(products.Succeeded, products.Code);
        Assert.Empty(products.Value!);
    }

    [Fact]
    public async Task Product_lifecycle_requires_reason_and_optimistic_concurrency()
    {
        await using var fixture = await Fixture.CreateAsync();
        var category = await fixture.CreateCategoryAsync(fixture.ContextA, "A");
        var unit = await fixture.CreateUnitAsync(fixture.ContextA, "EA");
        var created = await fixture.ProductService.CreateProductAsync(
            fixture.ContextA,
            Product("lifecycle", category.Id, unit.Id, []));
        var product = created.Value!;

        var noReason = await fixture.ProductService.DeactivateProductAsync(
            fixture.ContextA,
            product.Id,
            product.Version,
            null);
        Assert.False(noReason.Succeeded);
        Assert.Equal("deactivation_reason_required", noReason.Code);

        var deactivated = await fixture.ProductService.DeactivateProductAsync(
            fixture.ContextA,
            product.Id,
            product.Version,
            "catalogue cleanup");
        Assert.True(deactivated.Succeeded, deactivated.Code);
        Assert.Equal(MasterDataLifecycleState.Inactive, deactivated.Value!.LifecycleState);

        var stale = await fixture.ProductService.ReactivateProductAsync(
            fixture.ContextA,
            product.Id,
            product.Version);
        Assert.False(stale.Succeeded);
        Assert.Equal("concurrency_conflict", stale.Code);

        var reactivated = await fixture.ProductService.ReactivateProductAsync(
            fixture.ContextA,
            product.Id,
            deactivated.Value!.Version);
        Assert.True(reactivated.Succeeded, reactivated.Code);
        Assert.Equal(MasterDataLifecycleState.Active, reactivated.Value!.LifecycleState);
    }

    [Fact]
    public async Task Product_edit_fails_closed_when_base_uom_integrity_policy_is_unavailable()
    {
        await using var fixture = await Fixture.CreateAsync();
        var category = await fixture.CreateCategoryAsync(fixture.ContextA, "A");
        var unit = await fixture.CreateUnitAsync(fixture.ContextA, "EA");
        var otherUnit = await fixture.CreateUnitAsync(fixture.ContextA, "BOX");
        var created = await fixture.ProductService.CreateProductAsync(
            fixture.ContextA,
            Product("uom-guard", category.Id, unit.Id, []));

        var edit = await fixture.ProductService.EditProductAsync(
            fixture.ContextA,
            new EditProductIdentityCommand(
                created.Value!.Id,
                "uom-guard",
                new LocalizedName("UOM guard"),
                null,
                category.Id,
                otherUnit.Id,
                [],
                null,
                true,
                false,
                true,
                created.Value.Version));

        Assert.False(edit.Succeeded);
        Assert.Equal("base_uom_integrity_unavailable", edit.Code);
        Assert.Equal(unit.Id, (await fixture.ProductService.GetProductAsync(
            fixture.ContextA,
            created.Value.Id)).Value!.BaseUnitOfMeasureId);
    }

    [Fact]
    public async Task Product_create_does_not_leave_an_effect_when_audit_persistence_fails()
    {
        await using var fixture = await Fixture.CreateAsync();
        var category = await fixture.CreateCategoryAsync(fixture.ContextA, "AUDIT");
        var unit = await fixture.CreateUnitAsync(fixture.ContextA, "EA");
        await fixture.DropAuditEventsAsync();

        var result = await fixture.ProductService.CreateProductAsync(
            fixture.ContextA,
            Product("audit-failure", category.Id, unit.Id, []));

        Assert.False(result.Succeeded);
        Assert.Equal("audit_unavailable", result.Code);
        var products = await fixture.ProductService.ListProductsAsync(fixture.ContextA);
        Assert.True(products.Succeeded, products.Code);
        Assert.Empty(products.Value!);
    }

    private static MasterDataRequestContext ResolveContext(
        Guid tenantId,
        string correlation,
        string permission)
    {
        var tenantContext = TenantContext.ForOrdinaryMembership(
            new TenantId(tenantId),
            new MembershipReference(Guid.NewGuid()),
            new ScopeReference("Company:11111111-1111-1111-1111-111111111111"),
            new CorrelationId(correlation),
            ActorA);
        var foundation = FoundationRequestContext.ForTenant(
            ActorA,
            SessionA,
            tenantContext,
            permission);
        var resolution = new MasterDataTenantContextResolver().Resolve(foundation);
        return Assert.IsType<MasterDataRequestContext>(resolution.Context);
    }

    [Fact]
    public void Product_authorization_owns_its_scope_and_rejects_category_uom_scope_reuse()
    {
        var context = ResolveContext(
            TenantA,
            "corr-product-policy",
            "tenant.master-data.product.create");
        var authorization = new ProductResourceAuthorizationService(
            new GrantingCapabilityResolver(),
            new ProductResourcePolicy(),
            new ProductApprovalPolicy(),
            new ProductScopePolicy());

        var productResource = new MasterDataResourceReference(
            MasterDataResourceKind.Product,
            new TenantOwnership(TenantA),
            Guid.NewGuid(),
            "SKU",
            ProductScopePolicy.CreateScope(context.TenantId));
        var allowed = authorization.Authorize(context, productResource, MasterDataOperation.Create);
        Assert.True(allowed.Allowed, allowed.Code);

        var categoryScopeResource = new MasterDataResourceReference(
            MasterDataResourceKind.Product,
            new TenantOwnership(TenantA),
            Guid.NewGuid(),
            "SKU",
            CategoryUomScopePolicy.CreateScope(context.TenantId));
        Assert.False(authorization.Authorize(
            context,
            categoryScopeResource,
            MasterDataOperation.Create).Allowed);

        var categoryResource = new MasterDataResourceReference(
            MasterDataResourceKind.ProductCategory,
            new TenantOwnership(TenantA),
            Guid.NewGuid(),
            "CATEGORY",
            ProductScopePolicy.CreateScope(context.TenantId));
        Assert.False(authorization.Authorize(
            context,
            categoryResource,
            MasterDataOperation.Create).Allowed);
    }

    private static CreateProductIdentityCommand Product(
        string sku,
        Guid categoryId,
        Guid unitId,
        IReadOnlyList<string> barcodes,
        bool? trackingOverride = null) => new(
        sku,
        new LocalizedName("Product " + sku),
        "Description",
        categoryId,
        unitId,
        barcodes,
        trackingOverride,
        IsSellable: true,
        IsPurchasable: true,
        IsInventoryRelevant: true);

    private sealed class GrantingCapabilityResolver : IMasterDataCapabilityResolver
    {
        private readonly IReadOnlySet<MasterDataCapability> capabilities =
            Enum.GetValues<MasterDataCapability>().ToHashSet();

        public IReadOnlySet<MasterDataCapability> Resolve(MasterDataRequestContext context) => capabilities;
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection;
        private readonly DbContextOptions options;

        private Fixture(
            SqliteConnection connection,
            DbContextOptions options,
            MasterDataCatalogPersistence persistence,
            TenantContext tenantContextA,
            TenantContext tenantContextB,
            MasterDataRequestContext contextA,
            MasterDataRequestContext contextB,
            MasterDataCategoryUomService categoryUomService,
            ProductIdentityService productService)
        {
            this.connection = connection;
            this.options = options;
            Persistence = persistence;
            TenantContextA = tenantContextA;
            TenantContextB = tenantContextB;
            ContextA = contextA;
            ContextB = contextB;
            CategoryUomService = categoryUomService;
            ProductService = productService;
        }

        public MasterDataCatalogPersistence Persistence { get; }

        public TenantContext TenantContextA { get; }

        public TenantContext TenantContextB { get; }

        public MasterDataRequestContext ContextA { get; }

        public MasterDataRequestContext ContextB { get; }

        public MasterDataCategoryUomService CategoryUomService { get; }

        public ProductIdentityService ProductService { get; }

        public static async Task<Fixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder()
                .UseSqlite(connection)
                .Options;
            var tenantContextA = TenantContext.ForOrdinaryMembership(
                new TenantId(TenantA),
                new MembershipReference(Guid.NewGuid()),
                new ScopeReference("Company:11111111-1111-1111-1111-111111111111"),
                new CorrelationId("corr-product-a"),
                ActorA);
            var tenantContextB = TenantContext.ForOrdinaryMembership(
                new TenantId(TenantB),
                new MembershipReference(Guid.NewGuid()),
                new ScopeReference("Company:22222222-2222-2222-2222-222222222222"),
                new CorrelationId("corr-product-b"),
                ActorA);
            await using (var db = new MasterDataDbContext(options, tenantContextA))
            {
                await db.Database.EnsureCreatedAsync();
            }

            var persistence = new MasterDataCatalogPersistence(options);
            var categoryUomAuthorization = new MasterDataResourceAuthorizationService(
                new GrantingCapabilityResolver(),
                new CategoryUomResourcePolicy(),
                new CategoryUomApprovalPolicy(),
                new CategoryUomScopePolicy());
            var productAuthorization = new ProductResourceAuthorizationService(
                new GrantingCapabilityResolver(),
                new ProductResourcePolicy(),
                new ProductApprovalPolicy(),
                new ProductScopePolicy());
            return new Fixture(
                connection,
                options,
                persistence,
                tenantContextA,
                tenantContextB,
                ResolveContext(tenantContextA),
                ResolveContext(tenantContextB),
                new MasterDataCategoryUomService(categoryUomAuthorization, persistence),
                new ProductIdentityService(productAuthorization, persistence));
        }

        public async Task DropAuditEventsAsync()
        {
            await using var db = new MasterDataDbContext(options, TenantContextA);
            await db.Database.ExecuteSqlRawAsync("DROP TABLE IF EXISTS \"AuditEvents\"");
        }

        public async Task<MasterDataCategoryRecord> CreateCategoryAsync(
            MasterDataRequestContext context,
            string code,
            bool trackingDefaultEnabled = false)
        {
            var result = await CategoryUomService.CreateCategoryAsync(
                context,
                new CreateMasterDataCategoryCommand(
                    code,
                    new LocalizedName(code),
                    TrackingDefaultEnabled: trackingDefaultEnabled));
            Assert.True(result.Succeeded, result.Code);
            return result.Value!;
        }

        public async Task<MasterDataUnitOfMeasureRecord> CreateUnitAsync(
            MasterDataRequestContext context,
            string code)
        {
            var result = await CategoryUomService.CreateUnitOfMeasureAsync(
                context,
                new CreateMasterDataUnitOfMeasureCommand(code, new LocalizedName(code)));
            Assert.True(result.Succeeded, result.Code);
            return result.Value!;
        }

        private static MasterDataRequestContext ResolveContext(TenantContext tenantContext)
        {
            return ResolveContext(
                tenantContext.TenantId.Value,
                tenantContext.CorrelationId?.Value ?? "corr-product",
                "master-data.product");
        }

        private static MasterDataRequestContext ResolveContext(
            Guid tenantId,
            string correlation,
            string permission)
        {
            var tenantContext = TenantContext.ForOrdinaryMembership(
                new TenantId(tenantId),
                new MembershipReference(Guid.NewGuid()),
                new ScopeReference("Company:11111111-1111-1111-1111-111111111111"),
                new CorrelationId(correlation),
                ActorA);
            var foundation = FoundationRequestContext.ForTenant(
                ActorA,
                SessionA,
                tenantContext,
                permission);
            var resolution = new MasterDataTenantContextResolver().Resolve(foundation);
            return Assert.IsType<MasterDataRequestContext>(resolution.Context);
        }

        public async ValueTask DisposeAsync() => await connection.DisposeAsync();
    }
}
