using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MiniErp.App.BuildingBlocks.Rest;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.Modules.Procurement;
using MiniErp.Contracts.Modules.Foundation;
using MiniErp.Contracts.Modules.MasterData;
using MiniErp.Contracts.Modules.Procurement;
using MiniErp.Infrastructure.Persistence.Modules.MasterData;
using MiniErp.Infrastructure.Persistence.Modules.Procurement;
using Xunit;

namespace MiniErp.ArchitectureTests;

public sealed class PurchaseRequestTests
{
    private static readonly Guid TenantA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid TenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid CompanyA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid CompanyB = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid BranchA = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid Requester = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid Approver = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly Guid Delegatee = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private static readonly Guid ProductId = Guid.Parse("77777777-7777-7777-7777-777777777777");
    private static readonly Guid UnitId = Guid.Parse("88888888-8888-8888-8888-888888888888");
    private static readonly Guid CategoryId = Guid.Parse("99999999-9999-9999-9999-999999999999");

    [Fact]
    public async Task Draft_submit_approve_preserves_immutable_history_and_audit_and_denies_self_approval()
    {
        await using var fixture = await Fixture.CreateAsync();
        var request = fixture.NewRequest(CompanyA, BranchA);

        var draft = await fixture.Service.CreateAsync(
            fixture.Context(Requester, "tenant.procurement.purchase-request.create"),
            request,
            "create-1");
        Assert.True(draft.Succeeded, draft.Code);
        Assert.Equal(PurchaseRequestStatus.Draft, draft.Value!.Status);
        Assert.Single(draft.Value.Lines);

        var submitted = await fixture.Service.SubmitAsync(
            fixture.Context(Requester, "tenant.procurement.purchase-request.submit"),
            draft.Value.Id,
            draft.Value.Version,
            "submit-1");
        Assert.True(submitted.Succeeded, submitted.Code);
        Assert.Equal(PurchaseRequestStatus.PendingApproval, submitted.Value!.Status);
        Assert.Equal("procurement.purchase-request.default", submitted.Value.ApprovalPolicy!.PolicyId);

        var selfApproval = await fixture.Service.ApproveAsync(
            fixture.Context(Requester, "tenant.procurement.purchase-request.approve"),
            submitted.Value.Id,
            submitted.Value.Version,
            "approve-self");
        Assert.False(selfApproval.Succeeded);
        Assert.Equal("self_approval_denied", selfApproval.Code);

        var approved = await fixture.Service.ApproveAsync(
            fixture.Context(Approver, "tenant.procurement.purchase-request.approve"),
            submitted.Value.Id,
            submitted.Value.Version,
            "approve-1");
        Assert.True(approved.Succeeded, approved.Code);
        Assert.Equal(PurchaseRequestStatus.Approved, approved.Value!.Status);

        var history = await fixture.Service.ReadHistoryAsync(
            fixture.Context(Approver, "tenant.procurement.purchase-request.history"),
            approved.Value.Id);
        Assert.True(history.Succeeded, history.Code);
        Assert.Equal(
            [
                PurchaseRequestHistoryAction.Created,
                PurchaseRequestHistoryAction.Submitted,
                PurchaseRequestHistoryAction.ApprovalRecorded
            ],
            history.Value!.Select(item => item.Action).ToArray());
        Assert.All(history.Value!, item => Assert.Equal(TenantA, approved.Value.TenantId));

        var audit = await fixture.Service.ReadAuditAsync(
            fixture.Context(Approver, "tenant.procurement.purchase-request.audit"),
            approved.Value.Id);
        Assert.True(audit.Succeeded, audit.Code);
        Assert.Equal(3, audit.Value!.Count);
        Assert.Contains(audit.Value, item => item.OperationId == "procurement.purchase-request.submit");
        Assert.Contains(audit.Value, item => item.AfterStatus == PurchaseRequestStatus.Approved);
        Assert.All(audit.Value, item => Assert.Equal(TenantA, item.TenantId));
    }

    [Fact]
    public async Task Return_for_change_allows_edit_and_resubmit_reject_is_terminal_and_cancel_is_eligible_only_before_terminal()
    {
        await using var fixture = await Fixture.CreateAsync();
        var draft = await fixture.CreateDraftAsync();
        var submitted = await fixture.Service.SubmitAsync(
            fixture.Context(Requester, "tenant.procurement.purchase-request.submit"),
            draft.Id,
            draft.Version,
            "submit-1");
        Assert.True(submitted.Succeeded, submitted.Code);

        var returned = await fixture.Service.ReturnForChangeAsync(
            fixture.Context(Approver, "tenant.procurement.purchase-request.return"),
            submitted.Value!.Id,
            submitted.Value.Version,
            "Need a clearer purpose.",
            "return-1");
        Assert.True(returned.Succeeded, returned.Code);
        Assert.Equal(PurchaseRequestStatus.ReturnedForChange, returned.Value!.Status);

        var edited = await fixture.Service.EditAsync(
            fixture.Context(Requester, "tenant.procurement.purchase-request.edit"),
            returned.Value.Id,
            fixture.NewRequest(CompanyA, BranchA) with { Purpose = "Updated demand" },
            returned.Value.Version,
            "edit-1");
        Assert.True(edited.Succeeded, edited.Code);
        Assert.Equal(PurchaseRequestStatus.ReturnedForChange, edited.Value!.Status);

        var resubmitted = await fixture.Service.SubmitAsync(
            fixture.Context(Requester, "tenant.procurement.purchase-request.submit"),
            edited.Value.Id,
            edited.Value.Version,
            "submit-2");
        Assert.True(resubmitted.Succeeded, resubmitted.Code);

        var rejected = await fixture.Service.RejectAsync(
            fixture.Context(Approver, "tenant.procurement.purchase-request.reject"),
            resubmitted.Value!.Id,
            resubmitted.Value.Version,
            "No longer required.",
            "reject-1");
        Assert.True(rejected.Succeeded, rejected.Code);
        Assert.Equal(PurchaseRequestStatus.Rejected, rejected.Value!.Status);

        var terminalCancel = await fixture.Service.CancelAsync(
            fixture.Context(Requester, "tenant.procurement.purchase-request.cancel"),
            rejected.Value.Id,
            rejected.Value.Version,
            "Cancel after rejection",
            "cancel-terminal");
        Assert.False(terminalCancel.Succeeded);
        Assert.Equal("cancel_not_allowed", terminalCancel.Code);

        var draftForCancel = await fixture.CreateDraftAsync();
        var cancelled = await fixture.Service.CancelAsync(
            fixture.Context(Requester, "tenant.procurement.purchase-request.cancel"),
            draftForCancel.Id,
            draftForCancel.Version,
            "No longer needed.",
            "cancel-draft");
        Assert.True(cancelled.Succeeded, cancelled.Code);
        Assert.Equal(PurchaseRequestStatus.Cancelled, cancelled.Value!.Status);
    }

    [Fact]
    public async Task Tenant_and_organization_scope_are_enforced_and_stale_versions_conflict()
    {
        await using var fixture = await Fixture.CreateAsync();
        var draft = await fixture.CreateDraftAsync();

        var foreignTenant = await fixture.Service.GetAsync(
            fixture.Context(Requester, "tenant.procurement.purchase-request.view", TenantB, CompanyB),
            draft.Id);
        Assert.False(foreignTenant.Succeeded);
        Assert.Equal("purchase_request_not_found", foreignTenant.Code);

        var foreignCompany = await fixture.Service.GetAsync(
            fixture.Context(Requester, "tenant.procurement.purchase-request.view", TenantA, CompanyB),
            draft.Id);
        Assert.False(foreignCompany.Succeeded);
        Assert.Equal("resource_scope_denied", foreignCompany.Code);

        var edited = await fixture.Service.EditAsync(
            fixture.Context(Requester, "tenant.procurement.purchase-request.edit"),
            draft.Id,
            fixture.NewRequest(CompanyA, BranchA) with { Purpose = "First edit" },
            draft.Version,
            "edit-1");
        Assert.True(edited.Succeeded, edited.Code);

        var stale = await fixture.Service.EditAsync(
            fixture.Context(Requester, "tenant.procurement.purchase-request.edit"),
            draft.Id,
            fixture.NewRequest(CompanyA, BranchA) with { Purpose = "Stale edit" },
            draft.Version,
            "edit-stale");
        Assert.False(stale.Succeeded);
        Assert.Equal("concurrency_conflict", stale.Code);
    }

    [Fact]
    public async Task Configured_policy_and_expiring_delegation_are_scoped_and_recorded()
    {
        await using var fixture = await Fixture.CreateAsync(
            configured: true);
        var draft = await fixture.CreateDraftAsync();
        var submitted = await fixture.Service.SubmitAsync(
            fixture.Context(Requester, "tenant.procurement.purchase-request.submit"),
            draft.Id,
            draft.Version,
            "submit-configured");
        Assert.True(submitted.Succeeded, submitted.Code);

        var approvedByDelegate = await fixture.Service.ApproveAsync(
            fixture.Context(Delegatee, "tenant.procurement.purchase-request.approve"),
            submitted.Value!.Id,
            submitted.Value.Version,
            "approve-delegated");
        Assert.True(approvedByDelegate.Succeeded, approvedByDelegate.Code);
        Assert.Equal(PurchaseRequestStatus.Approved, approvedByDelegate.Value!.Status);

        var history = await fixture.Service.ReadHistoryAsync(
            fixture.Context(Delegatee, "tenant.procurement.purchase-request.history"),
            approvedByDelegate.Value.Id);
        Assert.Contains(history.Value!, item => item.DelegatedFromActorId == Approver);
    }

    [Fact]
    public async Task Tenant_scoped_actor_receives_configured_organization_scopes_across_companies_in_the_same_tenant()
    {
        var provider = new ConfiguredProcurementOrganizationScopeProvider(
        [
            new ProcurementOrganizationScopeOption(TenantA, CompanyA, BranchA, "Company A", "Branch A"),
            new ProcurementOrganizationScopeOption(TenantA, CompanyB, null, "Company B", null)
        ]);
        await using var fixture = await Fixture.CreateAsync(organizationScopeProvider: provider);

        var result = await fixture.Service.ListOrganizationScopesAsync(
            fixture.TenantScopeContext(Requester, "tenant.procurement.organization-scope.view", TenantA));

        Assert.True(result.Succeeded, result.Code);
        Assert.Equal(2, result.Value!.Count);
        Assert.Contains(result.Value, option => option.CompanyId == CompanyA);
        Assert.Contains(result.Value, option => option.CompanyId == CompanyB);
    }

    [Fact]
    public async Task Company_scoped_actor_cannot_see_another_companys_organization_scope_option()
    {
        var provider = new ConfiguredProcurementOrganizationScopeProvider(
        [
            new ProcurementOrganizationScopeOption(TenantA, CompanyA, BranchA, "Company A", "Branch A"),
            new ProcurementOrganizationScopeOption(TenantA, CompanyB, null, "Company B", null)
        ]);
        await using var fixture = await Fixture.CreateAsync(organizationScopeProvider: provider);

        var result = await fixture.Service.ListOrganizationScopesAsync(
            fixture.Context(Requester, "tenant.procurement.organization-scope.view", TenantA, CompanyA));

        Assert.True(result.Succeeded, result.Code);
        Assert.Single(result.Value!);
        Assert.Equal(CompanyA, result.Value!.Single().CompanyId);
    }

    [Fact]
    public async Task Cross_tenant_organization_scope_options_fail_closed()
    {
        var provider = new ConfiguredProcurementOrganizationScopeProvider(
        [
            new ProcurementOrganizationScopeOption(TenantA, CompanyA, BranchA, "Company A", "Branch A"),
            new ProcurementOrganizationScopeOption(TenantB, CompanyB, null, "Company B", null)
        ]);
        await using var fixture = await Fixture.CreateAsync(organizationScopeProvider: provider);

        var result = await fixture.Service.ListOrganizationScopesAsync(
            fixture.TenantScopeContext(Requester, "tenant.procurement.organization-scope.view", TenantA));

        Assert.True(result.Succeeded, result.Code);
        Assert.Single(result.Value!);
        Assert.Equal(CompanyA, result.Value!.Single().CompanyId);
        Assert.DoesNotContain(result.Value!, option => option.CompanyId == CompanyB);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly SqliteConnection masterDataConnection;
        private readonly SqliteConnection procurementConnection;
        private readonly DbContextOptions masterDataOptions;
        private readonly DbContextOptions procurementOptions;

        private Fixture(
            SqliteConnection masterDataConnection,
            SqliteConnection procurementConnection,
            DbContextOptions masterDataOptions,
            DbContextOptions procurementOptions,
            PurchaseRequestService service)
        {
            this.masterDataConnection = masterDataConnection;
            this.procurementConnection = procurementConnection;
            this.masterDataOptions = masterDataOptions;
            this.procurementOptions = procurementOptions;
            Service = service;
        }

        public PurchaseRequestService Service { get; }

        public static async Task<Fixture> CreateAsync(
            bool configured = false,
            IProcurementOrganizationScopeProvider? organizationScopeProvider = null)
        {
            var masterDataConnection = new SqliteConnection("Data Source=:memory:");
            await masterDataConnection.OpenAsync();
            var procurementConnection = new SqliteConnection("Data Source=:memory:");
            await procurementConnection.OpenAsync();
            var masterDataOptions = new DbContextOptionsBuilder()
                .UseSqlite(masterDataConnection)
                .Options;
            var procurementOptions = new DbContextOptionsBuilder()
                .UseSqlite(procurementConnection)
                .Options;
            var tenantContext = TenantContext(TenantA, Requester, new ScopeReference($"Company:{CompanyA:D}"));

            await using (var db = new MasterDataDbContext(masterDataOptions, tenantContext))
            {
                await db.Database.EnsureCreatedAsync();
                db.Categories.Add(new MasterDataCategoryEntity(
                    CategoryId,
                    new TenantId(TenantA),
                    "GENERAL",
                    new LocalizedName("General"),
                    parentCategoryId: null,
                    trackingDefaultEnabled: false));
                db.UnitsOfMeasure.Add(new MasterDataUnitOfMeasureEntity(
                    UnitId,
                    new TenantId(TenantA),
                    "EA",
                    new LocalizedName("Each")));
                db.Products.Add(new MasterDataProductEntity(
                    ProductId,
                    new TenantId(TenantA),
                    "SKU-001",
                    new LocalizedName("Test Product"),
                    null,
                    CategoryId,
                    UnitId,
                    trackingEnabledOverride: false,
                    isSellable: false,
                    isPurchasable: true,
                    isInventoryRelevant: false));
                await db.SaveChangesAsync();
            }

            await using (var db = new ProcurementDbContext(procurementOptions, tenantContext))
            {
                await db.Database.EnsureCreatedAsync();
            }

            var masterData = new MasterDataCatalogPersistence(masterDataOptions);
            IPurchaseRequestApprovalPolicyProvider policyProvider = configured
                ? new ConfiguredPurchaseRequestApprovalPolicyProvider(
                    [new PurchaseRequestApprovalPolicyBinding(
                        new PurchaseRequestScope(TenantA, CompanyA, BranchA),
                        new PurchaseRequestApprovalPolicyDefinition(
                            "procurement.purchase-request.configured",
                            3,
                            [new PurchaseRequestApprovalStageDefinition("manager", 1, 1, [Approver], true)],
                            true,
                            DateTimeOffset.MinValue))])
                : new DefaultPurchaseRequestApprovalPolicyProvider();
            IPurchaseRequestApprovalDelegationProvider delegationProvider = configured
                ? new ConfiguredPurchaseRequestApprovalDelegationProvider(
                    [new PurchaseRequestApprovalDelegation(
                        TenantA,
                        CompanyA,
                        BranchA,
                        "manager",
                        Approver,
                        Delegatee,
                        DateTimeOffset.UtcNow.AddMinutes(-1),
                        DateTimeOffset.UtcNow.AddMinutes(10),
                        "Approved leave coverage")])
                : new NoPurchaseRequestApprovalDelegationProvider();
            var service = new PurchaseRequestService(
                new PurchaseRequestAuthorizationService(),
                new PurchaseRequestPersistence(procurementOptions),
                masterData,
                masterData,
                policyProvider,
                delegationProvider,
                organizationScopeProvider ?? new NoProcurementOrganizationScopeProvider());
            return new Fixture(
                masterDataConnection,
                procurementConnection,
                masterDataOptions,
                procurementOptions,
                service);
        }

        public PurchaseRequestWriteRequest NewRequest(Guid companyId, Guid? branchId) =>
            new(
                companyId,
                branchId,
                "Office supplies",
                [new PurchaseRequestLineWriteRequest(
                    ProductId,
                    UnitId,
                    2,
                    DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(7)),
                    "Replace damaged stockroom items")]);

        public async Task<PurchaseRequestRecord> CreateDraftAsync()
        {
            var result = await Service.CreateAsync(
                Context(Requester, "tenant.procurement.purchase-request.create"),
                NewRequest(CompanyA, BranchA),
                Guid.NewGuid().ToString("N"));
            Assert.True(result.Succeeded, result.Code);
            return result.Value!;
        }

        public ProcurementRequestContext Context(
            Guid actor,
            string permission,
            Guid? tenantId = null,
            Guid? companyId = null) =>
            ResolveContext(
                tenantId ?? TenantA,
                actor,
                new ScopeReference($"Company:{companyId ?? CompanyA:D}"),
                permission);

        public ProcurementRequestContext TenantScopeContext(
            Guid actor,
            string permission,
            Guid tenantId) =>
            ResolveContext(
                tenantId,
                actor,
                new ScopeReference($"Tenant:{tenantId:D}"),
                permission);

        private static ProcurementRequestContext ResolveContext(
            Guid tenantId,
            Guid actor,
            ScopeReference scope,
            string permission)
        {
            var tenantContext = TenantContext(tenantId, actor, scope);
            var foundation = FoundationRequestContext.ForTenant(
                actor,
                Guid.NewGuid(),
                tenantContext,
                permission);
            var resolution = new ProcurementTenantContextResolver().Resolve(foundation);
            return Assert.IsType<ProcurementRequestContext>(resolution.Context);
        }

        private static TenantContext TenantContext(Guid tenantId, Guid actor, ScopeReference scope) =>
            MiniErp.App.BuildingBlocks.Tenancy.TenantContext.ForOrdinaryMembership(
                new TenantId(tenantId),
                new MembershipReference(Guid.NewGuid()),
                scope,
                new CorrelationId($"corr-{Guid.NewGuid():N}"),
                actor);

        public async ValueTask DisposeAsync()
        {
            await masterDataConnection.DisposeAsync();
            await procurementConnection.DisposeAsync();
        }
    }
}
