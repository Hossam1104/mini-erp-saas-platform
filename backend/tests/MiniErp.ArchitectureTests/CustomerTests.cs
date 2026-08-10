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

public sealed class CustomerTests
{
    private static readonly Guid TenantA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid TenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid ActorA = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid SessionA = Guid.Parse("44444444-4444-4444-4444-444444444444");

    [Fact]
    public async Task Customer_create_is_active_tenant_wide_localized_and_contact_bearing()
    {
        await using var fixture = await Fixture.CreateAsync();

        var result = await fixture.CustomerService.CreateCustomerAsync(
            fixture.ContextA,
            Customer("CUS-001", "Acme Trading", "مرحبا أكمي"));

        Assert.True(result.Succeeded, result.Code);
        var customer = Assert.IsType<CustomerRecord>(result.Value);
        Assert.Equal(TenantA, customer.TenantId.Value);
        Assert.Equal("CUS-001", customer.Code);
        Assert.Equal("Acme Trading", customer.LegalName.English);
        Assert.Equal("مرحبا أكمي", customer.LegalName.Arabic);
        Assert.Equal(MasterDataLifecycleState.Active, customer.LifecycleState);
        var contact = Assert.Single(customer.Contacts);
        Assert.Equal("Accounts receivable", contact.Name);
        Assert.Equal("ar@example.test", contact.Email);

        var audit = await fixture.Persistence.ReadAuditHistoryAsync(
            fixture.TenantContextA,
            customer.Id);
        var entry = Assert.Single(audit);
        Assert.Equal(MasterDataResourceKind.BusinessCustomer, entry.ResourceKind);
        Assert.Equal(MasterDataOperation.Create, entry.Operation);
        Assert.Equal(CustomerScopePolicy.PolicyId, entry.Scope!.Policy.PolicyId);
        Assert.Null(entry.Scope.OrganizationAnchor);
        Assert.Contains("state=Active", entry.AfterSummary, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Same_tenant_customer_code_and_name_duplicates_fail_but_other_tenant_can_reuse_identity()
    {
        await using var fixture = await Fixture.CreateAsync();
        var first = await fixture.CustomerService.CreateCustomerAsync(
            fixture.ContextA,
            Customer("same-code", "Same Customer"));
        Assert.True(first.Succeeded, first.Code);

        var duplicateCode = await fixture.CustomerService.CreateCustomerAsync(
            fixture.ContextA,
            Customer(" SAME-CODE ", "Different Customer"));
        Assert.False(duplicateCode.Succeeded);
        Assert.Equal("customer_code_duplicate", duplicateCode.Code);
        Assert.Equal(FoundationAuditReason.ValidationFailed, duplicateCode.Evidence!.Reason);

        var duplicateName = await fixture.CustomerService.CreateCustomerAsync(
            fixture.ContextA,
            Customer("different-code", " SAME CUSTOMER "));
        Assert.False(duplicateName.Succeeded);
        Assert.Equal("customer_duplicate", duplicateName.Code);
        Assert.Equal(FoundationAuditReason.ValidationFailed, duplicateName.Evidence!.Reason);

        var otherTenant = await fixture.CustomerService.CreateCustomerAsync(
            fixture.ContextB,
            Customer("same-code", "Same Customer"));
        Assert.True(otherTenant.Succeeded, otherTenant.Code);
        Assert.Equal(TenantB, otherTenant.Value!.TenantId.Value);

        var tenantAList = await fixture.CustomerService.ListCustomersAsync(fixture.ContextA);
        var tenantBList = await fixture.CustomerService.ListCustomersAsync(fixture.ContextB);
        Assert.True(tenantAList.Succeeded, tenantAList.Code);
        Assert.True(tenantBList.Succeeded, tenantBList.Code);
        Assert.Single(tenantAList.Value!);
        Assert.Single(tenantBList.Value!);
    }

    [Fact]
    public async Task Cross_tenant_customer_read_and_write_do_not_leak_or_change_the_record()
    {
        await using var fixture = await Fixture.CreateAsync();
        var created = await fixture.CustomerService.CreateCustomerAsync(
            fixture.ContextA,
            Customer("cross-tenant", "Tenant A Customer"));
        Assert.True(created.Succeeded, created.Code);

        var foreignRead = await fixture.CustomerService.GetCustomerAsync(
            fixture.ContextB,
            created.Value!.Id);
        Assert.False(foreignRead.Succeeded);
        Assert.Equal("customer_not_found", foreignRead.Code);

        var foreignWrite = await fixture.CustomerService.EditCustomerAsync(
            fixture.ContextB,
            new EditCustomerCommand(
                created.Value.Id,
                "changed-by-b",
                new LocalizedName("Changed by B"),
                null,
                [],
                created.Value.Version));
        Assert.False(foreignWrite.Succeeded);
        Assert.Equal("customer_not_found", foreignWrite.Code);

        var original = await fixture.CustomerService.GetCustomerAsync(
            fixture.ContextA,
            created.Value.Id);
        Assert.True(original.Succeeded, original.Code);
        Assert.Equal("cross-tenant", original.Value!.Code);
        Assert.Empty((await fixture.CustomerService.ListCustomersAsync(fixture.ContextB)).Value!);
    }

    [Fact]
    public void Customer_scope_is_tenant_wide_and_rejects_client_scope_expansion()
    {
        var context = ResolveContext(TenantA, "customer-policy", "tenant.master-data.customer.create");
        var authorization = new CustomerResourceAuthorizationService(
            new GrantingCapabilityResolver(),
            new CustomerResourcePolicy(),
            new CustomerApprovalPolicy(),
            new CustomerScopePolicy());

        var resource = new MasterDataResourceReference(
            MasterDataResourceKind.BusinessCustomer,
            new TenantOwnership(TenantA),
            Guid.NewGuid(),
            "CUSTOMER",
            CustomerScopePolicy.CreateScope(context.TenantId));
        var allowed = authorization.Authorize(context, resource, MasterDataOperation.Create);
        Assert.True(allowed.Allowed, allowed.Code);

        var companyScope = new BusinessScope(
            new TenantOwnership(TenantA),
            new OrganizationReference(
                new TenantOwnership(TenantA),
                OrganizationScopeKind.Company,
                Guid.Parse("11111111-1111-1111-1111-111111111111")),
            new ScopePolicyReference(CustomerScopePolicy.PolicyId, CustomerScopePolicy.PolicyVersion));
        Assert.False(authorization.Authorize(
            context,
            new MasterDataResourceReference(
                MasterDataResourceKind.BusinessCustomer,
                new TenantOwnership(TenantA),
                Guid.NewGuid(),
                "CUSTOMER",
                companyScope),
            MasterDataOperation.Create).Allowed);

        Assert.False(authorization.Authorize(
            context,
            new MasterDataResourceReference(
                MasterDataResourceKind.BusinessCustomer,
                new TenantOwnership(TenantB),
                Guid.NewGuid(),
                "CUSTOMER",
                CustomerScopePolicy.CreateScope(new TenantId(TenantB))),
            MasterDataOperation.Create).Allowed);

        var foundation = FoundationRequestContext.ForTenant(
            ActorA,
            SessionA,
            TenantContext.ForOrdinaryMembership(
                new TenantId(TenantA),
                new MembershipReference(Guid.NewGuid()),
                new ScopeReference("Company:11111111-1111-1111-1111-111111111111"),
                new CorrelationId("customer-client-scope"),
                ActorA),
            "tenant.master-data.customer.create");
        var resolver = new MasterDataTenantContextResolver();
        Assert.Equal(
            "cross_tenant_target_denied",
            resolver.Resolve(foundation, new MasterDataScopeSelection(TenantB)).Code);
        Assert.Equal(
            "resource_scope_denied",
            resolver.Resolve(
                foundation,
                new MasterDataScopeSelection(
                    requestedScope: new BusinessScope(
                        new TenantOwnership(TenantA),
                        new OrganizationReference(
                            new TenantOwnership(TenantA),
                            OrganizationScopeKind.Company,
                            Guid.Parse("22222222-2222-2222-2222-222222222222")),
                        new ScopePolicyReference(CustomerScopePolicy.PolicyId, CustomerScopePolicy.PolicyVersion)))).Code);
    }

    [Fact]
    public async Task Supplier_cross_role_review_is_non_blocking_and_does_not_create_a_unified_party()
    {
        await using var fixture = await Fixture.CreateAsync(new ReviewingCrossRoleMatchPolicy());

        var result = await fixture.CustomerService.CreateCustomerAsync(
            fixture.ContextA,
            Customer("cross-role", "Supplier-like Name"));

        Assert.True(result.Succeeded, result.Code);
        var audit = await fixture.Persistence.ReadAuditHistoryAsync(
            fixture.TenantContextA,
            result.Value!.Id);
        var entry = Assert.Single(audit);
        Assert.Contains("cross-role-review=review_required", entry.AfterSummary, StringComparison.Ordinal);
        Assert.Contains("review-count=1", entry.AfterSummary, StringComparison.Ordinal);
        Assert.DoesNotContain(
            typeof(CustomerRecord).Assembly.GetTypes(),
            type => type.Name.Contains("UnifiedParty", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Customer_lifecycle_requires_reason_preserves_identity_and_rejects_stale_writes()
    {
        await using var fixture = await Fixture.CreateAsync();
        var created = await fixture.CustomerService.CreateCustomerAsync(
            fixture.ContextA,
            Customer("lifecycle", "Lifecycle Customer"));
        Assert.True(created.Succeeded, created.Code);
        var customer = created.Value!;

        var noReason = await fixture.CustomerService.DeactivateCustomerAsync(
            fixture.ContextA,
            customer.Id,
            customer.Version,
            null);
        Assert.False(noReason.Succeeded);
        Assert.Equal("deactivation_reason_required", noReason.Code);

        var deactivated = await fixture.CustomerService.DeactivateCustomerAsync(
            fixture.ContextA,
            customer.Id,
            customer.Version,
            "customer no longer used");
        Assert.True(deactivated.Succeeded, deactivated.Code);
        Assert.Equal(MasterDataLifecycleState.Inactive, deactivated.Value!.LifecycleState);
        Assert.Equal(customer.Id, deactivated.Value.Id);

        var stale = await fixture.CustomerService.ReactivateCustomerAsync(
            fixture.ContextA,
            customer.Id,
            customer.Version);
        Assert.False(stale.Succeeded);
        Assert.Equal("concurrency_conflict", stale.Code);

        var reactivated = await fixture.CustomerService.ReactivateCustomerAsync(
            fixture.ContextA,
            customer.Id,
            deactivated.Value.Version);
        Assert.True(reactivated.Succeeded, reactivated.Code);
        Assert.Equal(MasterDataLifecycleState.Active, reactivated.Value!.LifecycleState);

        var history = await fixture.Persistence.ReadAuditHistoryAsync(
            fixture.TenantContextA,
            customer.Id);
        Assert.True(history.Count >= 4);
        Assert.Contains(history, item => item.Operation == MasterDataOperation.Deactivate);
        Assert.Contains(history, item => item.Operation == MasterDataOperation.Reactivate);
    }

    [Fact]
    public async Task Inactive_customer_state_is_preserved_for_active_only_consumers()
    {
        await using var fixture = await Fixture.CreateAsync();
        var created = await fixture.CustomerService.CreateCustomerAsync(
            fixture.ContextA,
            Customer("inactive", "Inactive Customer"));
        Assert.True(created.Succeeded, created.Code);

        var deactivated = await fixture.CustomerService.DeactivateCustomerAsync(
            fixture.ContextA,
            created.Value!.Id,
            created.Value.Version,
            "temporarily inactive");
        Assert.True(deactivated.Succeeded, deactivated.Code);

        var read = await fixture.CustomerService.GetCustomerAsync(
            fixture.ContextA,
            created.Value.Id);
        Assert.True(read.Succeeded, read.Code);
        Assert.Equal(MasterDataLifecycleState.Inactive, read.Value!.LifecycleState);
        Assert.Equal(created.Value.Id, read.Value.Id);
        Assert.Equal("inactive", read.Value.Code);
    }

    [Fact]
    public async Task Authorization_infrastructure_outage_fails_closed_and_is_not_caller_denial()
    {
        await using var fixture = await Fixture.CreateAsync();
        var service = fixture.CreateService(new ThrowingCapabilityResolver());

        var result = await service.CreateCustomerAsync(
            fixture.ContextA,
            Customer("auth-outage", "Authorization Outage"));

        Assert.False(result.Succeeded);
        Assert.Equal("permission_unavailable", result.Code);
        Assert.Equal(FoundationAuditReason.InternalFailure, result.Evidence!.Reason);
        Assert.Empty((await fixture.CustomerService.ListCustomersAsync(fixture.ContextA)).Value!);
    }

    [Fact]
    public async Task Genuine_permission_denial_remains_denial_and_has_no_customer_effect()
    {
        await using var fixture = await Fixture.CreateAsync();
        var service = fixture.CreateService(new DenyAllMasterDataCapabilityResolver());

        var result = await service.CreateCustomerAsync(
            fixture.ContextA,
            Customer("permission-denied", "Permission Denied"));

        Assert.False(result.Succeeded);
        Assert.Equal("permission_denied", result.Code);
        Assert.Equal(FoundationAuditReason.PermissionDenied, result.Evidence!.Reason);
        Assert.Empty((await fixture.CustomerService.ListCustomersAsync(fixture.ContextA)).Value!);
    }

    [Fact]
    public async Task Duplicate_conflict_is_validation_audited_and_has_no_customer_effect()
    {
        await using var fixture = await Fixture.CreateAsync();
        var first = await fixture.CustomerService.CreateCustomerAsync(
            fixture.ContextA,
            Customer("duplicate", "Duplicate Customer"));
        Assert.True(first.Succeeded, first.Code);

        var duplicate = await fixture.CustomerService.CreateCustomerAsync(
            fixture.ContextA,
            Customer("duplicate", "Another Customer"));
        Assert.False(duplicate.Succeeded);
        Assert.Equal("customer_code_duplicate", duplicate.Code);
        Assert.Equal(FoundationAuditReason.ValidationFailed, duplicate.Evidence!.Reason);
        Assert.NotEqual(FoundationAuditReason.InternalFailure, duplicate.Evidence.Reason);
        Assert.Single((await fixture.CustomerService.ListCustomersAsync(fixture.ContextA)).Value!);
    }

    [Fact]
    public async Task Audit_persistence_failure_leaves_create_without_effect()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.DropAuditEventsAsync();

        var result = await fixture.CustomerService.CreateCustomerAsync(
            fixture.ContextA,
            Customer("audit-failure", "Audit Failure Customer"));

        Assert.False(result.Succeeded);
        Assert.Equal("audit_unavailable", result.Code);
        Assert.Empty((await fixture.CustomerService.ListCustomersAsync(fixture.ContextA)).Value!);
    }

    [Fact]
    public async Task Business_persistence_failure_does_not_return_false_success()
    {
        await using var fixture = await Fixture.CreateAsync();
        var service = new CustomerService(
            fixture.Authorization,
            new FailingCreatePersistence(fixture.Persistence));

        var result = await service.CreateCustomerAsync(
            fixture.ContextA,
            Customer("persistence-failure", "Persistence Failure Customer"));

        Assert.False(result.Succeeded);
        Assert.Equal("persistence_unavailable", result.Code);
        Assert.NotNull(result.Evidence);
    }

    [Fact]
    public void Customer_contract_has_no_authentication_or_statutory_authority()
    {
        var requestProperties = typeof(CustomerWriteRequest).GetProperties()
            .Select(property => property.Name)
            .ToHashSet(StringComparer.Ordinal);
        Assert.DoesNotContain("TenantId", requestProperties);
        Assert.DoesNotContain("CompanyId", requestProperties);
        Assert.DoesNotContain("BranchId", requestProperties);
        Assert.DoesNotContain("Scope", requestProperties);
        Assert.DoesNotContain("UserId", requestProperties);
        Assert.DoesNotContain("Password", requestProperties);
        Assert.DoesNotContain("Credential", requestProperties);
        Assert.DoesNotContain("Portal", requestProperties);
        Assert.DoesNotContain("RegistrationReference", requestProperties);
        Assert.DoesNotContain("VatNumber", requestProperties);
        Assert.DoesNotContain("TaxNumber", requestProperties);

        Assert.All(
            new[]
            {
                "master-data.customer.list",
                "master-data.customer.read",
                "master-data.customer.create",
                "master-data.customer.edit",
                "master-data.customer.deactivate",
                "master-data.customer.reactivate",
                "master-data.customer.audit.read"
            },
            operationId => Assert.NotNull(FoundationOperationCatalog.GetRequired(operationId)));
    }

    [Fact]
    public void Customer_source_keeps_module_boundaries_and_excludes_downstream_behavior()
    {
        var appSource = File.ReadAllText(Path.Combine(
            AppContext.BaseDirectory,
            "..", "..", "..", "..", "..", "src", "MiniErp.App", "Modules", "BusinessParties", "CustomerService.cs"));
        Assert.DoesNotContain("BusinessPartiesSupplierEntity", appSource, StringComparison.Ordinal);
        Assert.DoesNotContain("BusinessPartiesCustomerDbContext", appSource, StringComparison.Ordinal);
        Assert.DoesNotContain("SalesOrder", appSource, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("CreditLimit", appSource, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("UnifiedParty", appSource, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Password", appSource, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DbContext", appSource, StringComparison.Ordinal);
    }

    private static CreateCustomerCommand Customer(
        string code,
        string legalName,
        string? arabicLegalName = null) => new(
        code,
        new LocalizedName(legalName, arabicLegalName),
        new LocalizedName("Trading " + legalName),
        [new CustomerContactCommand("Accounts receivable", "ar@example.test", "+966500000000")]);

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

    private sealed class ReviewingCrossRoleMatchPolicy : ISupplierCrossRoleMatchPolicy
    {
        public SupplierCrossRoleMatchReview Evaluate(
            MasterDataRequestContext context,
            string legalNameKey,
            string? registrationKey)
        {
            Assert.Null(registrationKey);
            return new SupplierCrossRoleMatchReview(true, "review_required", 1);
        }
    }

    private sealed class FailingCreatePersistence : ICustomerPersistence
    {
        private readonly ICustomerPersistence inner;

        public FailingCreatePersistence(ICustomerPersistence inner) => this.inner = inner;

        public Task<IReadOnlyList<CustomerRecord>> ListCustomersAsync(
            TenantContext tenantContext,
            CancellationToken cancellationToken = default) => inner.ListCustomersAsync(tenantContext, cancellationToken);

        public Task<CustomerRecord?> FindCustomerAsync(
            TenantContext tenantContext,
            Guid customerId,
            CancellationToken cancellationToken = default) => inner.FindCustomerAsync(tenantContext, customerId, cancellationToken);

        public Task<MasterDataPersistenceResult<CustomerRecord>> CreateCustomerAsync(
            TenantContext tenantContext,
            Guid customerId,
            CreateCustomerCommand command,
            MasterDataAuditEvidence evidence,
            CancellationToken cancellationToken = default) =>
            Task.FromException<MasterDataPersistenceResult<CustomerRecord>>(
                new InvalidOperationException("customer persistence unavailable"));

        public Task<MasterDataPersistenceResult<CustomerRecord>> EditCustomerAsync(
            TenantContext tenantContext,
            EditCustomerCommand command,
            MasterDataAuditEvidence evidence,
            CancellationToken cancellationToken = default) => inner.EditCustomerAsync(tenantContext, command, evidence, cancellationToken);

        public Task<MasterDataPersistenceResult<CustomerRecord>> SetCustomerLifecycleAsync(
            TenantContext tenantContext,
            Guid customerId,
            MasterDataLifecycleState lifecycleState,
            byte[] expectedVersion,
            MasterDataAuditEvidence evidence,
            CancellationToken cancellationToken = default) => inner.SetCustomerLifecycleAsync(tenantContext, customerId, lifecycleState, expectedVersion, evidence, cancellationToken);

        public Task<MasterDataPersistenceResult<MasterDataAuditRecord>> AppendAuditAsync(
            TenantContext tenantContext,
            MasterDataAuditEvidence evidence,
            CancellationToken cancellationToken = default) => inner.AppendAuditAsync(tenantContext, evidence, cancellationToken);

        public Task<IReadOnlyList<MasterDataAuditRecord>> ReadAuditHistoryAsync(
            TenantContext tenantContext,
            Guid customerId,
            CancellationToken cancellationToken = default) => inner.ReadAuditHistoryAsync(tenantContext, customerId, cancellationToken);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection;
        private readonly DbContextOptions options;

        private Fixture(
            SqliteConnection connection,
            DbContextOptions options,
            BusinessPartiesCustomerPersistence persistence,
            TenantContext tenantContextA,
            TenantContext tenantContextB,
            MasterDataRequestContext contextA,
            MasterDataRequestContext contextB,
            CustomerResourceAuthorizationService authorization,
            CustomerService customerService)
        {
            this.connection = connection;
            this.options = options;
            Persistence = persistence;
            TenantContextA = tenantContextA;
            TenantContextB = tenantContextB;
            ContextA = contextA;
            ContextB = contextB;
            Authorization = authorization;
            CustomerService = customerService;
        }

        public BusinessPartiesCustomerPersistence Persistence { get; }

        public TenantContext TenantContextA { get; }

        public TenantContext TenantContextB { get; }

        public MasterDataRequestContext ContextA { get; }

        public MasterDataRequestContext ContextB { get; }

        public CustomerResourceAuthorizationService Authorization { get; }

        public CustomerService CustomerService { get; }

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
                new CorrelationId("corr-customer-a"),
                ActorA);
            var tenantContextB = TenantContext.ForOrdinaryMembership(
                new TenantId(TenantB),
                new MembershipReference(Guid.NewGuid()),
                new ScopeReference("Company:22222222-2222-2222-2222-222222222222"),
                new CorrelationId("corr-customer-b"),
                ActorA);
            await using (var db = new BusinessPartiesDbContext(options, tenantContextA))
            {
                await db.Database.EnsureCreatedAsync();
            }

            var persistence = new BusinessPartiesCustomerPersistence(options);
            var authorization = new CustomerResourceAuthorizationService(
                new GrantingCapabilityResolver(),
                new CustomerResourcePolicy(),
                new CustomerApprovalPolicy(),
                new CustomerScopePolicy());
            return new Fixture(
                connection,
                options,
                persistence,
                tenantContextA,
                tenantContextB,
                ResolveContext(tenantContextA),
                ResolveContext(tenantContextB),
                authorization,
                new CustomerService(authorization, persistence, crossRoleMatchPolicy));
        }

        public CustomerService CreateService(IMasterDataCapabilityResolver capabilityResolver) =>
            new(
                new CustomerResourceAuthorizationService(
                    capabilityResolver,
                    new CustomerResourcePolicy(),
                    new CustomerApprovalPolicy(),
                    new CustomerScopePolicy()),
                Persistence);

        public async Task DropAuditEventsAsync()
        {
            await using var db = new BusinessPartiesDbContext(options, TenantContextA);
            await db.Database.ExecuteSqlRawAsync("DROP TABLE IF EXISTS \"AuditEvents\"");
        }

        private static MasterDataRequestContext ResolveContext(TenantContext tenantContext) =>
            ResolveContext(
                tenantContext.TenantId.Value,
                tenantContext.CorrelationId?.Value ?? "corr-customer",
                "tenant.master-data.customer");

        private static MasterDataRequestContext ResolveContext(
            Guid tenantId,
            string correlation,
            string permission) =>
            CustomerTests.ResolveContext(tenantId, correlation, permission);

        public async ValueTask DisposeAsync() => await connection.DisposeAsync();
    }
}
