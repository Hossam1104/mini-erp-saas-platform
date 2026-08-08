using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MiniErp.App.BuildingBlocks.Rest;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.Modules.MasterData;
using MiniErp.Contracts.Modules.Audit;
using MiniErp.Contracts.Modules.MasterData;
using MiniErp.Infrastructure.Persistence.Modules.MasterData;
using Xunit;

namespace MiniErp.ArchitectureTests;

public sealed class MasterDataCategoryUomTests
{
    private static readonly Guid TenantA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid TenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid ActorA = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid SessionA = Guid.Parse("44444444-4444-4444-4444-444444444444");

    [Fact]
    public async Task Category_is_created_active_tenant_wide_and_full_audit_is_persistent()
    {
        await using var fixture = await Fixture.CreateAsync();
        var context = fixture.ContextA;

        var result = await fixture.Service.CreateCategoryAsync(
            context,
            new CreateMasterDataCategoryCommand(
                " PLUMBING ",
                new LocalizedName("Plumbing", "سباكة")));

        Assert.True(result.Succeeded, result.Code);
        Assert.NotNull(result.Value);
        var category = result.Value!;
        Assert.Equal("PLUMBING", category.Code);
        Assert.Equal(MasterDataLifecycleState.Active, category.LifecycleState);
        Assert.NotEmpty(category.Version);

        var audit = await fixture.Persistence.ReadAuditHistoryAsync(
            fixture.TenantContextA,
            MasterDataResourceKind.ProductCategory,
            category.Id);
        var entry = Assert.Single(audit);
        Assert.Equal(TenantA, entry.TenantId.Value);
        Assert.Equal(ActorA, entry.ActorId);
        Assert.Equal(SessionA, entry.SessionId);
        Assert.Equal(category.Id, entry.ResourceId);
        Assert.Equal("PLUMBING", entry.BusinessCode);
        Assert.Equal(MasterDataOperation.Create, entry.Operation);
        Assert.Equal(MasterDataPolicyOutcome.Allowed, entry.PolicyOutcome);
        Assert.Equal(FoundationAuditDecision.Allowed, entry.Decision);
        Assert.Equal(FoundationAuditReason.Allowed, entry.Reason);
        Assert.Equal("master-data.category-uom.scope", entry.Scope!.Policy.PolicyId);
        Assert.Equal(1, entry.Scope.Policy.Version);
        Assert.Null(entry.Scope.OrganizationAnchor);
        Assert.Null(entry.ApproverId);
        Assert.Equal("corr-category-uom-a", entry.CorrelationId);
        Assert.NotEqual(default, entry.EvidenceId);
        Assert.Contains("PLUMBING", entry.AfterSummary, StringComparison.Ordinal);
        Assert.Null(entry.BeforeSummary);
    }

    [Fact]
    public async Task Category_parent_policy_enforces_three_levels_and_cycles()
    {
        await using var fixture = await Fixture.CreateAsync();
        var root = await fixture.CreateCategoryAsync("root", "Root");
        var child = await fixture.CreateCategoryAsync("child", "Child", root.Id);
        var grandchild = await fixture.CreateCategoryAsync("grandchild", "Grandchild", child.Id);

        var tooDeep = await fixture.Service.CreateCategoryAsync(
            fixture.ContextA,
            new CreateMasterDataCategoryCommand("too-deep", new LocalizedName("Too deep"), grandchild.Id));
        Assert.False(tooDeep.Succeeded);
        Assert.Equal("category_depth_exceeded", tooDeep.Code);

        var cycle = await fixture.Service.EditCategoryAsync(
            fixture.ContextA,
            new EditMasterDataCategoryCommand(
                child.Id,
                "child",
                new LocalizedName("Child"),
                child.Id,
                child.Version));
        Assert.False(cycle.Succeeded);
        Assert.Equal("category_parent_cycle", cycle.Code);
    }

    [Fact]
    public async Task Category_is_not_draft_and_lifecycle_is_permission_and_version_bound()
    {
        await using var fixture = await Fixture.CreateAsync();
        var category = await fixture.CreateCategoryAsync("active", "Active");

        var inactive = await fixture.Service.DeactivateCategoryAsync(
            fixture.ContextA,
            category.Id,
            category.Version);
        Assert.True(inactive.Succeeded, inactive.Code);
        Assert.Equal(MasterDataLifecycleState.Inactive, inactive.Value!.LifecycleState);

        var stale = await fixture.Service.ReactivateCategoryAsync(
            fixture.ContextA,
            category.Id,
            category.Version);
        Assert.False(stale.Succeeded);
        Assert.Equal("concurrency_conflict", stale.Code);

        var active = await fixture.Service.ReactivateCategoryAsync(
            fixture.ContextA,
            category.Id,
            inactive.Value.Version);
        Assert.True(active.Succeeded, active.Code);
        Assert.Equal(MasterDataLifecycleState.Active, active.Value!.LifecycleState);
    }

    [Fact]
    public async Task Uom_conversion_rejects_over_precision_and_rounds_away_from_zero()
    {
        await using var fixture = await Fixture.CreateAsync();
        var piece = await fixture.CreateUnitAsync("piece", "Piece");
        var box = await fixture.CreateUnitAsync("box", "Box");

        var factorTooPrecise = await fixture.Service.CreateConversionAsync(
            fixture.ContextA,
            new CreateMasterDataConversionCommand(piece.Id, box.Id, 12.123456789m));
        Assert.False(factorTooPrecise.Succeeded);
        Assert.Equal("validation_failed", factorTooPrecise.Code);

        var conversion = await fixture.Service.CreateConversionAsync(
            fixture.ContextA,
            new CreateMasterDataConversionCommand(piece.Id, box.Id, 2.5m));
        Assert.True(conversion.Succeeded, conversion.Code);

        var calculated = await fixture.Service.ConvertQuantityAsync(
            fixture.ContextA,
            conversion.Value!.Id,
            1.234567m);
        Assert.True(calculated.Succeeded, calculated.Code);
        Assert.Equal(3.086418m, calculated.Value);

        var quantityTooPrecise = await fixture.Service.ConvertQuantityAsync(
            fixture.ContextA,
            conversion.Value!.Id,
            1.2345678m);
        Assert.False(quantityTooPrecise.Succeeded);
        Assert.Equal("precision_invalid", quantityTooPrecise.Code);
    }

    [Fact]
    public async Task Uom_cannot_deactivate_while_a_same_tenant_conversion_references_it()
    {
        await using var fixture = await Fixture.CreateAsync();
        var piece = await fixture.CreateUnitAsync("piece", "Piece");
        var box = await fixture.CreateUnitAsync("box", "Box");
        var conversion = await fixture.Service.CreateConversionAsync(
            fixture.ContextA,
            new CreateMasterDataConversionCommand(piece.Id, box.Id, 2m));
        Assert.True(conversion.Succeeded, conversion.Code);

        var result = await fixture.Service.DeactivateUnitOfMeasureAsync(
            fixture.ContextA,
            piece.Id,
            piece.Version);
        Assert.False(result.Succeeded);
        Assert.Equal("uom_in_use", result.Code);
    }

    [Fact]
    public async Task Tenant_isolation_allows_same_code_but_hides_the_other_tenant()
    {
        await using var fixture = await Fixture.CreateAsync();
        var tenantACategory = await fixture.CreateCategoryAsync("same-code", "Tenant A");
        var tenantBResult = await fixture.Service.CreateCategoryAsync(
            fixture.ContextB,
            new CreateMasterDataCategoryCommand("same-code", new LocalizedName("Tenant B")));

        Assert.True(tenantBResult.Succeeded, tenantBResult.Code);
        Assert.NotEqual(tenantACategory.Id, tenantBResult.Value!.Id);

        var tenantAList = await fixture.Service.ListCategoriesAsync(fixture.ContextA);
        Assert.True(tenantAList.Succeeded, tenantAList.Code);
        Assert.Single(tenantAList.Value!);
        Assert.Equal(tenantACategory.Id, tenantAList.Value![0].Id);

        var hidden = await fixture.Service.GetCategoryAsync(fixture.ContextA, tenantBResult.Value!.Id);
        Assert.False(hidden.Succeeded);
        Assert.Equal("category_not_found", hidden.Code);
    }

    [Fact]
    public async Task Audit_history_is_permission_bound_and_denials_are_evidenced()
    {
        await using var fixture = await Fixture.CreateAsync();
        var category = await fixture.CreateCategoryAsync("audited", "Audited");
        var deniedService = new MasterDataCategoryUomService(
            new MasterDataResourceAuthorizationService(
                new EmptyCapabilityResolver(),
                new CategoryUomResourcePolicy(),
                new CategoryUomApprovalPolicy(),
                new CategoryUomScopePolicy()),
            fixture.Persistence);

        var result = await deniedService.ReadAuditHistoryAsync(
            fixture.ContextA,
            MasterDataResourceKind.ProductCategory,
            category.Id);

        Assert.False(result.Succeeded);
        Assert.Equal("permission_denied", result.Code);

        var history = await fixture.Persistence.ReadAuditHistoryAsync(
            fixture.TenantContextA,
            MasterDataResourceKind.ProductCategory,
            category.Id);
        Assert.Equal(2, history.Count);
        Assert.Contains(history, entry =>
            entry.Operation == MasterDataOperation.ViewAuditHistory
            && entry.PolicyOutcome == MasterDataPolicyOutcome.Denied
            && entry.Decision == FoundationAuditDecision.Denied);
    }

    [Fact]
    public void Master_data_audit_type_has_no_constructible_public_or_internal_constructor()
    {
        Assert.Empty(typeof(MasterDataAuditEvidence).GetConstructors());
        Assert.DoesNotContain(
            typeof(MasterDataAuditEvidence).GetConstructors(
                System.Reflection.BindingFlags.Public
                | System.Reflection.BindingFlags.NonPublic
                | System.Reflection.BindingFlags.Instance),
            constructor => constructor.IsAssembly || constructor.IsFamily || constructor.IsFamilyOrAssembly);
    }

    [Fact]
    public void Category_uom_scope_policy_rejects_generic_or_client_organization_scope()
    {
        var context = ResolveContext(TenantA, "corr-scope");
        var policy = new CategoryUomScopePolicy();
        var valid = new MasterDataResourceReference(
            MasterDataResourceKind.ProductCategory,
            new TenantOwnership(TenantA),
            Guid.NewGuid(),
            "category",
            CategoryUomScopePolicy.CreateScope(new TenantId(TenantA)));
        Assert.True(policy.Evaluate(context, valid).Allowed);

        var organizationScope = new BusinessScope(
            new TenantOwnership(TenantA),
            new OrganizationReference(
                new TenantOwnership(TenantA),
                OrganizationScopeKind.Company,
                Guid.NewGuid()),
            new ScopePolicyReference("organization.exact", 1));
        var clientScope = new MasterDataResourceReference(
            MasterDataResourceKind.ProductCategory,
            new TenantOwnership(TenantA),
            Guid.NewGuid(),
            "category",
            organizationScope);
        Assert.False(policy.Evaluate(context, clientScope).Allowed);
    }

    private static MasterDataRequestContext ResolveContext(Guid tenantId, string correlation)
    {
        var foundation = FoundationRequestContext.ForTenant(
            ActorA,
            SessionA,
            TenantContext.ForOrdinaryMembership(
                new TenantId(tenantId),
                new MembershipReference(Guid.NewGuid()),
                new ScopeReference("Company:11111111-1111-1111-1111-111111111111"),
                new CorrelationId(correlation),
                ActorA),
            "master-data.category-uom");
        var resolution = new MasterDataTenantContextResolver().Resolve(foundation);
        return Assert.IsType<MasterDataRequestContext>(resolution.Context);
    }

    private sealed class GrantingCapabilityResolver : IMasterDataCapabilityResolver
    {
        private readonly IReadOnlySet<MasterDataCapability> capabilities =
            Enum.GetValues<MasterDataCapability>().ToHashSet();

        public IReadOnlySet<MasterDataCapability> Resolve(MasterDataRequestContext context) => capabilities;
    }

    private sealed class EmptyCapabilityResolver : IMasterDataCapabilityResolver
    {
        public IReadOnlySet<MasterDataCapability> Resolve(MasterDataRequestContext context) =>
            new HashSet<MasterDataCapability>();
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection;

        private Fixture(
            SqliteConnection connection,
            DbContextOptions options,
            TenantContext tenantContextA,
            TenantContext tenantContextB,
            MasterDataRequestContext contextA,
            MasterDataRequestContext contextB,
            MasterDataCatalogPersistence persistence,
            MasterDataCategoryUomService service)
        {
            this.connection = connection;
            Options = options;
            TenantContextA = tenantContextA;
            TenantContextB = tenantContextB;
            ContextA = contextA;
            ContextB = contextB;
            Persistence = persistence;
            Service = service;
        }

        public DbContextOptions Options { get; }

        public TenantContext TenantContextA { get; }

        public TenantContext TenantContextB { get; }

        public MasterDataRequestContext ContextA { get; }

        public MasterDataRequestContext ContextB { get; }

        public MasterDataCatalogPersistence Persistence { get; }

        public MasterDataCategoryUomService Service { get; }

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
                new CorrelationId("corr-category-uom-a"),
                ActorA);
            var tenantContextB = TenantContext.ForOrdinaryMembership(
                new TenantId(TenantB),
                new MembershipReference(Guid.NewGuid()),
                new ScopeReference("Company:22222222-2222-2222-2222-222222222222"),
                new CorrelationId("corr-category-uom-b"),
                ActorA);
            await using (var db = new MasterDataDbContext(options, tenantContextA))
            {
                await db.Database.EnsureCreatedAsync();
            }

            var persistence = new MasterDataCatalogPersistence(options);
            var authorization = new MasterDataResourceAuthorizationService(
                new GrantingCapabilityResolver(),
                new CategoryUomResourcePolicy(),
                new CategoryUomApprovalPolicy(),
                new CategoryUomScopePolicy());
            return new Fixture(
                connection,
                options,
                tenantContextA,
                tenantContextB,
                ResolveContext(TenantA, "corr-category-uom-a"),
                ResolveContext(TenantB, "corr-category-uom-b"),
                persistence,
                new MasterDataCategoryUomService(authorization, persistence));
        }

        public async Task<MasterDataCategoryRecord> CreateCategoryAsync(
            string code,
            string english,
            Guid? parentCategoryId = null)
        {
            var result = await Service.CreateCategoryAsync(
                ContextA,
                new CreateMasterDataCategoryCommand(
                    code,
                    new LocalizedName(english),
                    parentCategoryId));
            Assert.True(result.Succeeded, result.Code);
            Assert.NotNull(result.Value);
            return result.Value!;
        }

        public async Task<MasterDataUnitOfMeasureRecord> CreateUnitAsync(
            string code,
            string english)
        {
            var result = await Service.CreateUnitOfMeasureAsync(
                ContextA,
                new CreateMasterDataUnitOfMeasureCommand(code, new LocalizedName(english)));
            Assert.True(result.Succeeded, result.Code);
            Assert.NotNull(result.Value);
            return result.Value!;
        }

        public async ValueTask DisposeAsync() => await connection.DisposeAsync();
    }
}
