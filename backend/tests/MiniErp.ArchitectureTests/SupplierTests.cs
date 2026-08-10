using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MiniErp.App.BuildingBlocks.Rest;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.Modules.BusinessParties;
using MiniErp.App.Modules.MasterData;
using MiniErp.Contracts.Modules.Audit;
using MiniErp.Contracts.Modules.BusinessParties;
using MiniErp.Contracts.Modules.Foundation;
using MiniErp.Contracts.Modules.MasterData;
using MiniErp.Infrastructure.Persistence.Modules.BusinessParties;
using Xunit;

namespace MiniErp.ArchitectureTests;

public sealed class SupplierTests
{
    private static readonly Guid TenantA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid TenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid ActorA = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid SessionA = Guid.Parse("44444444-4444-4444-4444-444444444444");

    [Fact]
    public async Task Supplier_create_is_active_tenant_wide_localized_and_contact_bearing()
    {
        await using var fixture = await Fixture.CreateAsync();

        var result = await fixture.SupplierService.CreateSupplierAsync(
            fixture.ContextA,
            Supplier("SUP-001", "Acme Plumbing", "مرحباً أكمي", "REG-001"));

        Assert.True(result.Succeeded, result.Code);
        var supplier = Assert.IsType<SupplierRecord>(result.Value);
        Assert.Equal(TenantA, supplier.TenantId.Value);
        Assert.Equal("SUP-001", supplier.Code);
        Assert.Equal("Acme Plumbing", supplier.LegalName.English);
        Assert.Equal("مرحباً أكمي", supplier.LegalName.Arabic);
        Assert.Equal(MasterDataLifecycleState.Active, supplier.LifecycleState);
        var contact = Assert.Single(supplier.Contacts);
        Assert.Equal("External buyer", contact.Name);
        Assert.Equal("buyer@example.test", contact.Email);

        var audit = await fixture.Persistence.ReadAuditHistoryAsync(
            fixture.TenantContextA,
            supplier.Id);
        var entry = Assert.Single(audit);
        Assert.Equal(MasterDataResourceKind.Supplier, entry.ResourceKind);
        Assert.Equal(MasterDataOperation.Create, entry.Operation);
        Assert.Equal(SupplierScopePolicy.PolicyId, entry.Scope!.Policy.PolicyId);
        Assert.Null(entry.Scope.OrganizationAnchor);
        Assert.Contains("contacts=1", entry.AfterSummary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Same_tenant_exact_supplier_duplicates_fail_but_other_tenant_can_reuse_identity()
    {
        await using var fixture = await Fixture.CreateAsync();
        var first = await fixture.SupplierService.CreateSupplierAsync(
            fixture.ContextA,
            Supplier("same-code", "Same Legal Name", null, "same-registration"));
        Assert.True(first.Succeeded, first.Code);

        var duplicateCode = await fixture.SupplierService.CreateSupplierAsync(
            fixture.ContextA,
            Supplier(" SAME-CODE ", "Different Legal Name", null, "different-registration"));
        Assert.False(duplicateCode.Succeeded);
        Assert.Equal("supplier_code_duplicate", duplicateCode.Code);
        Assert.Equal(FoundationAuditReason.ValidationFailed, duplicateCode.Evidence!.Reason);

        var duplicateRegistration = await fixture.SupplierService.CreateSupplierAsync(
            fixture.ContextA,
            Supplier("different-code", "Another Legal Name", null, " SAME-REGISTRATION "));
        Assert.False(duplicateRegistration.Succeeded);
        Assert.Equal("supplier_registration_duplicate", duplicateRegistration.Code);
        Assert.Equal(FoundationAuditReason.ValidationFailed, duplicateRegistration.Evidence!.Reason);

        var duplicateName = await fixture.SupplierService.CreateSupplierAsync(
            fixture.ContextA,
            Supplier("another-code", " SAME LEGAL NAME ", null, "another-registration"));
        Assert.False(duplicateName.Succeeded);
        Assert.Equal("supplier_duplicate", duplicateName.Code);
        Assert.Equal(FoundationAuditReason.ValidationFailed, duplicateName.Evidence!.Reason);

        var otherTenant = await fixture.SupplierService.CreateSupplierAsync(
            fixture.ContextB,
            Supplier("same-code", "Same Legal Name", null, "same-registration"));
        Assert.True(otherTenant.Succeeded, otherTenant.Code);
        Assert.Equal(TenantB, otherTenant.Value!.TenantId.Value);

        var tenantAList = await fixture.SupplierService.ListSuppliersAsync(fixture.ContextA);
        var tenantBList = await fixture.SupplierService.ListSuppliersAsync(fixture.ContextB);
        Assert.True(tenantAList.Succeeded, tenantAList.Code);
        Assert.True(tenantBList.Succeeded, tenantBList.Code);
        Assert.Single(tenantAList.Value!);
        Assert.Single(tenantBList.Value!);
        var foreignRead = await fixture.SupplierService.GetSupplierAsync(
            fixture.ContextB,
            first.Value!.Id);
        Assert.False(foreignRead.Succeeded);
        Assert.Equal("supplier_not_found", foreignRead.Code);
    }

    [Fact]
    public async Task Cross_role_review_is_non_blocking_and_remains_evidenced_without_a_unified_party()
    {
        await using var fixture = await Fixture.CreateAsync(
            new ReviewingCrossRoleMatchPolicy());

        var result = await fixture.SupplierService.CreateSupplierAsync(
            fixture.ContextA,
            Supplier("cross-role", "Customer-like Name", null, "cross-role-reference"));

        Assert.True(result.Succeeded, result.Code);
        var audit = await fixture.Persistence.ReadAuditHistoryAsync(
            fixture.TenantContextA,
            result.Value!.Id);
        var entry = Assert.Single(audit);
        Assert.Contains("cross-role-review=review_required", entry.AfterSummary, StringComparison.Ordinal);
        Assert.Contains("review-count=1", entry.AfterSummary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Supplier_lifecycle_requires_reason_preserves_identity_and_rejects_stale_writes()
    {
        await using var fixture = await Fixture.CreateAsync();
        var created = await fixture.SupplierService.CreateSupplierAsync(
            fixture.ContextA,
            Supplier("lifecycle", "Lifecycle Supplier", null, null));
        Assert.True(created.Succeeded, created.Code);
        var supplier = created.Value!;

        var noReason = await fixture.SupplierService.DeactivateSupplierAsync(
            fixture.ContextA,
            supplier.Id,
            supplier.Version,
            null);
        Assert.False(noReason.Succeeded);
        Assert.Equal("deactivation_reason_required", noReason.Code);

        var deactivated = await fixture.SupplierService.DeactivateSupplierAsync(
            fixture.ContextA,
            supplier.Id,
            supplier.Version,
            "supplier no longer used");
        Assert.True(deactivated.Succeeded, deactivated.Code);
        Assert.Equal(MasterDataLifecycleState.Inactive, deactivated.Value!.LifecycleState);
        Assert.Equal(supplier.Id, deactivated.Value.Id);

        var stale = await fixture.SupplierService.ReactivateSupplierAsync(
            fixture.ContextA,
            supplier.Id,
            supplier.Version);
        Assert.False(stale.Succeeded);
        Assert.Equal("concurrency_conflict", stale.Code);

        var reactivated = await fixture.SupplierService.ReactivateSupplierAsync(
            fixture.ContextA,
            supplier.Id,
            deactivated.Value.Version);
        Assert.True(reactivated.Succeeded, reactivated.Code);
        Assert.Equal(MasterDataLifecycleState.Active, reactivated.Value!.LifecycleState);
    }

    [Fact]
    public async Task Supplier_create_has_no_effect_when_module_audit_persistence_fails()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.DropAuditEventsAsync();

        var result = await fixture.SupplierService.CreateSupplierAsync(
            fixture.ContextA,
            Supplier("audit-failure", "Audit Failure Supplier", null, null));

        Assert.False(result.Succeeded);
        Assert.Equal("audit_unavailable", result.Code);
        var suppliers = await fixture.SupplierService.ListSuppliersAsync(fixture.ContextA);
        Assert.True(suppliers.Succeeded, suppliers.Code);
        Assert.Empty(suppliers.Value!);
    }

    [Fact]
    public async Task Supplier_authorization_dependency_outages_are_internal_failures_and_have_no_effect()
    {
        await using var fixture = await Fixture.CreateAsync();
        var cases = new[]
        {
            (
                ExpectedCode: "permission_unavailable",
                Authorization: SupplierAuthorization(
                    new ThrowingCapabilityResolver(),
                    new SupplierResourcePolicy(),
                    new SupplierApprovalPolicy(),
                    new SupplierScopePolicy())),
            (
                ExpectedCode: "scope_policy_unavailable",
                Authorization: SupplierAuthorization(
                    new GrantingCapabilityResolver(),
                    new SupplierResourcePolicy(),
                    new SupplierApprovalPolicy(),
                    new ThrowingScopePolicy())),
            (
                ExpectedCode: "approval_policy_unavailable",
                Authorization: SupplierAuthorization(
                    new GrantingCapabilityResolver(),
                    new SupplierResourcePolicy(),
                    new ThrowingApprovalPolicy(),
                    new SupplierScopePolicy())),
            (
                ExpectedCode: "resource_policy_unavailable",
                Authorization: SupplierAuthorization(
                    new GrantingCapabilityResolver(),
                    new ThrowingResourcePolicy(),
                    new SupplierApprovalPolicy(),
                    new SupplierScopePolicy()))
        };

        foreach (var (expectedCode, authorization) in cases)
        {
            var result = await fixture.CreateService(authorization).CreateSupplierAsync(
                fixture.ContextA,
                Supplier("auth-" + expectedCode, "Authorization " + expectedCode, null, null));

            Assert.False(result.Succeeded);
            Assert.Equal(expectedCode, result.Code);
            Assert.Equal(FoundationAuditReason.InternalFailure, result.Evidence!.Reason);
            Assert.Null(result.Value);
        }

        var suppliers = await fixture.SupplierService.ListSuppliersAsync(fixture.ContextA);
        Assert.True(suppliers.Succeeded, suppliers.Code);
        Assert.Empty(suppliers.Value!);
    }

    [Fact]
    public async Task Supplier_genuine_permission_denial_remains_denial_and_has_no_effect()
    {
        await using var fixture = await Fixture.CreateAsync();
        var authorization = SupplierAuthorization(
            new DenyAllMasterDataCapabilityResolver(),
            new SupplierResourcePolicy(),
            new SupplierApprovalPolicy(),
            new SupplierScopePolicy());

        var result = await fixture.CreateService(authorization).CreateSupplierAsync(
            fixture.ContextA,
            Supplier("permission-denied", "Permission Denied Supplier", null, null));

        Assert.False(result.Succeeded);
        Assert.Equal("permission_denied", result.Code);
        Assert.Equal(FoundationAuditReason.PermissionDenied, result.Evidence!.Reason);
        Assert.Null(result.Value);
        Assert.Empty((await fixture.SupplierService.ListSuppliersAsync(fixture.ContextA)).Value!);
    }

    [Fact]
    public void Supplier_authorization_requires_supplier_tenant_scope_and_rejects_foreign_or_company_scope()
    {
        var context = ResolveContext(TenantA, "supplier-policy", "tenant.master-data.supplier.create");
        var authorization = new SupplierResourceAuthorizationService(
            new GrantingCapabilityResolver(),
            new SupplierResourcePolicy(),
            new SupplierApprovalPolicy(),
            new SupplierScopePolicy());

        var resource = new MasterDataResourceReference(
            MasterDataResourceKind.Supplier,
            new TenantOwnership(TenantA),
            Guid.NewGuid(),
            "SUPPLIER",
            SupplierScopePolicy.CreateScope(context.TenantId));
        var allowed = authorization.Authorize(context, resource, MasterDataOperation.Create);
        Assert.True(allowed.Allowed, allowed.Code);
        Assert.Equal("allowed", allowed.Code);

        var companyScope = new BusinessScope(
            new TenantOwnership(TenantA),
            new OrganizationReference(
                new TenantOwnership(TenantA),
                OrganizationScopeKind.Company,
                Guid.Parse("11111111-1111-1111-1111-111111111111")),
            new ScopePolicyReference(SupplierScopePolicy.PolicyId, SupplierScopePolicy.PolicyVersion));
        Assert.False(authorization.Authorize(
            context,
            new MasterDataResourceReference(
                MasterDataResourceKind.Supplier,
                new TenantOwnership(TenantA),
                Guid.NewGuid(),
                "SUPPLIER",
                companyScope),
            MasterDataOperation.Create).Allowed);

        Assert.False(authorization.Authorize(
            context,
            new MasterDataResourceReference(
                MasterDataResourceKind.Supplier,
                new TenantOwnership(TenantB),
                Guid.NewGuid(),
                "SUPPLIER",
                SupplierScopePolicy.CreateScope(new TenantId(TenantB))),
            MasterDataOperation.Create).Allowed);

        Assert.False(authorization.Authorize(
            context,
            new MasterDataResourceReference(
                MasterDataResourceKind.Product,
                new TenantOwnership(TenantA),
                Guid.NewGuid(),
                "SUPPLIER",
                SupplierScopePolicy.CreateScope(context.TenantId)),
            MasterDataOperation.Create).Allowed);
    }

    [Fact]
    public void Supplier_public_contract_and_operation_catalog_have_no_credential_or_client_tenant_authority()
    {
        var requestProperties = typeof(SupplierWriteRequest).GetProperties()
            .Select(property => property.Name)
            .ToHashSet(StringComparer.Ordinal);
        Assert.DoesNotContain("TenantId", requestProperties);
        Assert.DoesNotContain("CompanyId", requestProperties);
        Assert.DoesNotContain("BranchId", requestProperties);
        Assert.DoesNotContain("Scope", requestProperties);
        Assert.DoesNotContain("UserId", requestProperties);
        Assert.DoesNotContain("Password", requestProperties);
        Assert.DoesNotContain("Credential", requestProperties);
        Assert.All(
            new[]
            {
                "master-data.supplier.list",
                "master-data.supplier.read",
                "master-data.supplier.create",
                "master-data.supplier.edit",
                "master-data.supplier.deactivate",
                "master-data.supplier.reactivate",
                "master-data.supplier.audit.read"
            },
            operationId => Assert.NotNull(FoundationOperationCatalog.GetRequired(operationId)));
    }

    private static CreateSupplierCommand Supplier(
        string code,
        string legalName,
        string? arabicLegalName,
        string? registrationReference) => new(
        code,
        new LocalizedName(legalName, arabicLegalName),
        new LocalizedName("Trading " + legalName),
        registrationReference,
        [new SupplierContactCommand("External buyer", "buyer@example.test", "+966500000000")]);

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

    private sealed class GrantingCapabilityResolver : IMasterDataCapabilityResolver
    {
        private readonly IReadOnlySet<MasterDataCapability> capabilities =
            Enum.GetValues<MasterDataCapability>().ToHashSet();

        public IReadOnlySet<MasterDataCapability> Resolve(MasterDataRequestContext context) => capabilities;
    }

    private sealed class ThrowingCapabilityResolver : IMasterDataCapabilityResolver
    {
        public IReadOnlySet<MasterDataCapability> Resolve(MasterDataRequestContext context) =>
            throw new InvalidOperationException("capability dependency unavailable");
    }

    private sealed class ThrowingScopePolicy : IMasterDataScopePolicy
    {
        public MasterDataScopeDecision Evaluate(
            MasterDataRequestContext context,
            MasterDataResourceReference resource) =>
            throw new InvalidOperationException("scope dependency unavailable");
    }

    private sealed class ThrowingApprovalPolicy : IMasterDataApprovalPolicy
    {
        public MasterDataApprovalPolicyResult Evaluate(
            MasterDataRequestContext context,
            MasterDataResourceReference resource,
            MasterDataOperation operation) =>
            throw new InvalidOperationException("approval dependency unavailable");
    }

    private sealed class ThrowingResourcePolicy : IMasterDataResourcePolicy
    {
        public MasterDataPolicyDecision Evaluate(MasterDataPolicyInput input) =>
            throw new InvalidOperationException("resource dependency unavailable");
    }

    private static SupplierResourceAuthorizationService SupplierAuthorization(
        IMasterDataCapabilityResolver capabilityResolver,
        IMasterDataResourcePolicy resourcePolicy,
        IMasterDataApprovalPolicy approvalPolicy,
        IMasterDataScopePolicy scopePolicy) => new(
        capabilityResolver,
        resourcePolicy,
        approvalPolicy,
        scopePolicy);

    private sealed class ReviewingCrossRoleMatchPolicy : ISupplierCrossRoleMatchPolicy
    {
        public SupplierCrossRoleMatchReview Evaluate(
            MasterDataRequestContext context,
            string legalNameKey,
            string? registrationKey) =>
            new(true, "review_required", 1);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection;
        private readonly DbContextOptions options;

        private Fixture(
            SqliteConnection connection,
            DbContextOptions options,
            BusinessPartiesSupplierPersistence persistence,
            TenantContext tenantContextA,
            TenantContext tenantContextB,
            MasterDataRequestContext contextA,
            MasterDataRequestContext contextB,
            SupplierService supplierService)
        {
            this.connection = connection;
            this.options = options;
            Persistence = persistence;
            TenantContextA = tenantContextA;
            TenantContextB = tenantContextB;
            ContextA = contextA;
            ContextB = contextB;
            SupplierService = supplierService;
        }

        public BusinessPartiesSupplierPersistence Persistence { get; }

        public TenantContext TenantContextA { get; }

        public TenantContext TenantContextB { get; }

        public MasterDataRequestContext ContextA { get; }

        public MasterDataRequestContext ContextB { get; }

        public SupplierService SupplierService { get; }

        public SupplierService CreateService(SupplierResourceAuthorizationService authorization) =>
            new(authorization, Persistence);

        public static Task<Fixture> CreateAsync(
            ISupplierCrossRoleMatchPolicy? crossRoleMatchPolicy = null) =>
            CreateCoreAsync(crossRoleMatchPolicy);

        private static async Task<Fixture> CreateCoreAsync(
            ISupplierCrossRoleMatchPolicy? crossRoleMatchPolicy)
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
                new CorrelationId("corr-supplier-a"),
                ActorA);
            var tenantContextB = TenantContext.ForOrdinaryMembership(
                new TenantId(TenantB),
                new MembershipReference(Guid.NewGuid()),
                new ScopeReference("Company:22222222-2222-2222-2222-222222222222"),
                new CorrelationId("corr-supplier-b"),
                ActorA);
            await using (var db = new BusinessPartiesDbContext(options, tenantContextA))
            {
                await db.Database.EnsureCreatedAsync();
            }

            var persistence = new BusinessPartiesSupplierPersistence(options);
            var authorization = new SupplierResourceAuthorizationService(
                new GrantingCapabilityResolver(),
                new SupplierResourcePolicy(),
                new SupplierApprovalPolicy(),
                new SupplierScopePolicy());
            return new Fixture(
                connection,
                options,
                persistence,
                tenantContextA,
                tenantContextB,
                ResolveContext(tenantContextA),
                ResolveContext(tenantContextB),
                new SupplierService(authorization, persistence, crossRoleMatchPolicy));
        }

        public async Task DropAuditEventsAsync()
        {
            await using var db = new BusinessPartiesDbContext(options, TenantContextA);
            await db.Database.ExecuteSqlRawAsync("DROP TABLE IF EXISTS \"AuditEvents\"");
        }

        private static MasterDataRequestContext ResolveContext(TenantContext tenantContext) =>
            ResolveContext(
                tenantContext.TenantId.Value,
                tenantContext.CorrelationId?.Value ?? "corr-supplier",
                "tenant.master-data.supplier");

        private static MasterDataRequestContext ResolveContext(
            Guid tenantId,
            string correlation,
            string permission) =>
            SupplierTests.ResolveContext(tenantId, correlation, permission);

        public async ValueTask DisposeAsync() => await connection.DisposeAsync();
    }
}
