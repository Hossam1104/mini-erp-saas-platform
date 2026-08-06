using System.Text.Json;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.BuildingBlocks.Work;
using MiniErp.App.Modules.Identity;
using Xunit;

namespace MiniErp.ArchitectureTests;

public sealed class DurableWorkAuthorityRevalidationTests
{
    private static readonly TenantId TenantA = new(Guid.Parse("11111111-1111-1111-1111-111111111111"));
    private static readonly TenantId TenantB = new(Guid.Parse("22222222-2222-2222-2222-222222222222"));
    private static readonly Guid CompanyA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid CompanyB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid BranchA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa01");
    private static readonly Guid BranchB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbb01");
    private static readonly Guid WarehouseA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaa02");
    private static readonly Guid WarehouseB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbb02");
    private static readonly DurableWorkOperationDescriptor ReadOperation =
        new(
            "durable-work.test",
            "authority-test",
            "tenant.business.read",
            [TenantAuthorizationPath.OrdinaryMembership]);
    private static readonly DurableWorkOperationDescriptor SupportOperation =
        new(
            "durable-work.support-test",
            "authority-test",
            "support.tenant.read",
            [TenantAuthorizationPath.SupportGrant]);
    private static readonly DurableWorkOperationDescriptor ExportOperation =
        new(
            "durable-work.export-test",
            "authority-test",
            "tenant.business.export",
            [TenantAuthorizationPath.OrdinaryMembership]);
    private static readonly DurableWorkOperationCatalogue OperationCatalogue =
        new([ReadOperation, SupportOperation, ExportOperation]);

    [Fact]
    public void Foreign_tenant_company_is_rejected_by_the_real_resolver()
    {
        var fixture = CreateOrdinaryFixture();
        var result = fixture.Service.Resolve(fixture.Context, TenantWorkScopeRequest.ForCompany(CompanyB));

        Assert.False(result.Allowed);
        Assert.Null(result.Scope);
    }

    [Fact]
    public void Company_outside_the_authorized_scope_is_rejected()
    {
        var fixture = CreateOrdinaryFixture(OrganizationScope.ForCompany(TenantA, CompanyA));
        var result = fixture.Service.Resolve(fixture.Context, TenantWorkScopeRequest.ForCompany(CompanyB));

        Assert.False(result.Allowed);
    }

    [Fact]
    public void Branch_declared_under_the_wrong_company_is_rejected()
    {
        var fixture = CreateOrdinaryFixture();
        var result = fixture.Service.Resolve(
            fixture.Context,
            TenantWorkScopeRequest.ForBranch(CompanyB, BranchA));

        Assert.False(result.Allowed);
    }

    [Fact]
    public void Warehouse_declared_under_the_wrong_branch_is_rejected()
    {
        var fixture = CreateOrdinaryFixture();
        var result = fixture.Service.Resolve(
            fixture.Context,
            TenantWorkScopeRequest.ForWarehouse(CompanyA, BranchB, WarehouseA));

        Assert.False(result.Allowed);
    }

    [Fact]
    public void Sibling_company_is_rejected()
    {
        var fixture = CreateOrdinaryFixture(OrganizationScope.ForCompany(TenantA, CompanyA));
        var result = fixture.Service.Resolve(fixture.Context, TenantWorkScopeRequest.ForCompany(CompanyB));

        Assert.False(result.Allowed);
    }

    [Fact]
    public void Sibling_branch_is_rejected()
    {
        var fixture = CreateOrdinaryFixture(OrganizationScope.ForBranch(TenantA, BranchA));
        var result = fixture.Service.Resolve(
            fixture.Context,
            TenantWorkScopeRequest.ForBranch(CompanyA, BranchB));

        Assert.False(result.Allowed);
    }

    [Fact]
    public void Sibling_warehouse_is_rejected()
    {
        var fixture = CreateOrdinaryFixture(OrganizationScope.ForWarehouse(TenantA, WarehouseA));
        var result = fixture.Service.Resolve(
            fixture.Context,
            TenantWorkScopeRequest.ForWarehouse(CompanyA, BranchA, WarehouseB));

        Assert.False(result.Allowed);
    }

    [Fact]
    public void Tenant_wide_authority_can_select_a_verified_descendant()
    {
        var fixture = CreateOrdinaryFixture();
        var result = fixture.Service.Resolve(
            fixture.Context,
            TenantWorkScopeRequest.ForWarehouse(CompanyA, BranchA, WarehouseA));

        Assert.True(result.Allowed);
        Assert.Equal(WarehouseA, result.Scope!.WarehouseId);
    }

    [Fact]
    public async Task Approved_revalidation_returns_exact_server_issued_execution_scope()
    {
        var fixture = CreateOrdinaryFixture();
        var work = CreateWork(
            fixture,
            TenantWorkScopeRequest.ForWarehouse(CompanyA, BranchA, WarehouseA));

        var result = await fixture.Service.RevalidateAsync(
            work,
            fixture.Context,
            fixture.Clock.GetUtcNow());

        Assert.True(result.Allowed);
        Assert.NotNull(result.Authorization);
        Assert.Equal(work.Identity.WorkItemId, result.Authorization!.WorkItemId);
        Assert.Equal(work.Identity.OperationId, result.Authorization.Operation.OperationId);
        Assert.Equal(WarehouseA, result.Authorization.Scope.WarehouseId);
        Assert.Equal(
            $"Warehouse:{WarehouseA}",
            result.Authorization.ExecutionTenantContext.Scope!.Value.Value);
        Assert.Equal(fixture.Actor.Value, result.Authorization.ActorId);
    }

    [Fact]
    public void Verified_scope_cannot_be_rebound_to_another_authorization_context()
    {
        var fixture = CreateOrdinaryFixture();
        var resolution = fixture.Service.Resolve(
            fixture.Context,
            TenantWorkScopeRequest.ForWarehouse(CompanyA, BranchA, WarehouseA));
        var otherContext = TenantContext.ForOrdinaryMembership(
            TenantA,
            new MembershipReference(Guid.NewGuid()),
            fixture.Context.Scope,
            fixture.Correlation,
            Guid.NewGuid());
        var identity = DurableWorkIdentity.Create(
            Guid.NewGuid(),
            fixture.Service.OperationCatalogue,
            "durable-work.test",
            fixture.Correlation,
            $"rebind-{Guid.NewGuid():N}");

        Assert.True(resolution.Allowed);
        Assert.Throws<ArgumentException>(() => DurableWorkItem.Create(
            otherContext,
            resolution.Scope!,
            identity,
            new AuthorityPayload("safe-payload"),
            Guid.NewGuid()));
    }

    [Fact]
    public void Company_scope_can_select_only_descendants()
    {
        var fixture = CreateOrdinaryFixture(OrganizationScope.ForCompany(TenantA, CompanyA));
        var descendant = fixture.Service.Resolve(
            fixture.Context,
            TenantWorkScopeRequest.ForWarehouse(CompanyA, BranchA, WarehouseA));
        var upward = fixture.Service.Resolve(fixture.Context, TenantWorkScopeRequest.TenantWide());

        Assert.True(descendant.Allowed);
        Assert.False(upward.Allowed);
    }

    [Fact]
    public void Missing_organization_ownership_fails_closed()
    {
        var fixture = CreateOrdinaryFixture();
        var missingCompany = Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc");
        var result = fixture.Service.Resolve(fixture.Context, TenantWorkScopeRequest.ForCompany(missingCompany));

        Assert.False(result.Allowed);
        Assert.Equal("scope_not_authorized", result.SafeReason);
    }

    [Fact]
    public async Task Inactive_user_prevents_dispatch()
    {
        var fixture = CreateOrdinaryFixture();
        var work = CreateWork(fixture, TenantWorkScopeRequest.TenantWide());
        fixture.Service.Store.Users[fixture.Actor].Status = GlobalUserStatus.Suspended;

        var outcome = await ProcessAsync(fixture, work);

        AssertDenied(outcome);
    }

    [Fact]
    public async Task Suspended_membership_prevents_dispatch()
    {
        var fixture = CreateOrdinaryFixture();
        var work = CreateWork(fixture, TenantWorkScopeRequest.TenantWide());
        fixture.Service.Store.Memberships[fixture.Membership!.Value].Status = MembershipStatus.Suspended;

        var outcome = await ProcessAsync(fixture, work);

        AssertDenied(outcome);
    }

    [Fact]
    public async Task Revoked_session_prevents_dispatch()
    {
        var fixture = CreateOrdinaryFixture();
        var work = CreateWork(fixture, TenantWorkScopeRequest.TenantWide());
        fixture.Service.RevokeSession(fixture.Session.Id, "durable-work-test");

        var outcome = await ProcessAsync(fixture, work);

        AssertDenied(outcome);
    }

    [Fact]
    public async Task Expired_session_prevents_dispatch()
    {
        var fixture = CreateOrdinaryFixture();
        var work = CreateWork(fixture, TenantWorkScopeRequest.TenantWide());
        fixture.Clock.Advance(TimeSpan.FromHours(8));

        var outcome = await ProcessAsync(fixture, work);

        AssertDenied(outcome);
    }

    [Fact]
    public async Task Missing_exact_permission_prevents_dispatch()
    {
        var fixture = CreateOrdinaryFixture();
        var work = CreateWork(
            fixture,
            TenantWorkScopeRequest.TenantWide(),
            "durable-work.export-test");

        var outcome = await ProcessAsync(fixture, work);

        AssertDenied(outcome);
    }

    [Fact]
    public async Task Reduced_current_scope_prevents_dispatch()
    {
        var fixture = CreateOrdinaryFixture();
        var work = CreateWork(fixture, TenantWorkScopeRequest.TenantWide());
        fixture.Service.Store.ScopeGrants.Values.Single().IsActive = false;

        var outcome = await ProcessAsync(fixture, work);

        AssertDenied(outcome);
    }

    [Fact]
    public async Task Mixed_authorization_path_prevents_dispatch()
    {
        var ordinary = CreateOrdinaryFixture();
        var work = CreateWork(ordinary, TenantWorkScopeRequest.TenantWide());
        var mixedPath = TenantContext.ForSupportGrant(
            TenantA,
            new SupportGrantReference(Guid.NewGuid(), Guid.NewGuid()),
            new ScopeReference($"Tenant:{TenantA.Value}"),
            ordinary.Correlation,
            ordinary.Actor.Value);

        var result = await ordinary.Service.RevalidateAsync(work, mixedPath, ordinary.Clock.GetUtcNow());

        Assert.False(result.Allowed);
        Assert.Equal(DurableWorkFailureCategory.AuthorizationDenied, result.FailureCategory);
    }

    [Fact]
    public async Task Handler_effect_is_never_called_after_failed_revalidation()
    {
        var fixture = CreateOrdinaryFixture();
        var work = CreateWork(fixture, TenantWorkScopeRequest.TenantWide());
        fixture.Service.Store.Users[fixture.Actor].Status = GlobalUserStatus.Offboarded;

        var outcome = await ProcessAsync(fixture, work);

        Assert.Equal(0, outcome.HandlerCalls);
        Assert.Equal(DurableWorkLifecycle.DeadLetter, outcome.Completion.Lifecycle);
    }

    [Fact]
    public async Task Outbox_effect_is_never_called_after_failed_revalidation()
    {
        var fixture = CreateOrdinaryFixture();
        var work = CreateWork(fixture, TenantWorkScopeRequest.TenantWide());
        fixture.Service.Store.Users[fixture.Actor].Status = GlobalUserStatus.Suspended;
        var store = new InMemoryRelationalDurableWorkStore();
        Assert.True(await store.SubmitAsync(work));
        var effectCalls = 0;

        var result = await store.DispatchOutboxAsync(
            fixture.Context,
            fixture.Service,
            fixture.Clock.GetUtcNow(),
            (_, _, _) =>
            {
                effectCalls++;
                return ValueTask.CompletedTask;
            });

        Assert.True(result.DeadLettered);
        Assert.Equal(DurableWorkFailureCategory.AuthorizationDenied, result.FailureCategory);
        Assert.Equal(0, effectCalls);
        var audit = await store.ReadAuditAsync(fixture.Context);
        Assert.Contains(audit, item => item.EventType == "outbox.dead-letter");
    }

    [Fact]
    public async Task Failed_revalidation_creates_safe_dead_letter_evidence()
    {
        var fixture = CreateOrdinaryFixture();
        var work = CreateWork(fixture, TenantWorkScopeRequest.TenantWide());
        fixture.Service.Store.Users[fixture.Actor].Status = GlobalUserStatus.Suspended;

        var outcome = await ProcessAsync(fixture, work);
        var evidenceJson = JsonSerializer.Serialize(outcome.Evidence);
        var auditJson = JsonSerializer.Serialize(outcome.Audit);

        AssertDenied(outcome);
        Assert.Contains(outcome.Evidence, item => item.Operation == "durable-work.dispatch" && item.Result == "denied");
        Assert.DoesNotContain("password", evidenceJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("password", auditJson, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("private-bytes", auditJson, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Expired_support_grant_prevents_dispatch()
    {
        var fixture = CreateSupportFixture(TimeSpan.FromMinutes(5));
        var work = CreateWork(fixture, TenantWorkScopeRequest.TenantWide(), "durable-work.support-test");
        fixture.Clock.Advance(TimeSpan.FromMinutes(6));

        var outcome = await ProcessAsync(fixture, work);

        AssertDenied(outcome);
    }

    [Fact]
    public async Task Revoked_support_grant_prevents_dispatch()
    {
        var fixture = CreateSupportFixture();
        var work = CreateWork(fixture, TenantWorkScopeRequest.TenantWide(), "durable-work.support-test");
        fixture.SupportGrant!.RevokedAt = fixture.Clock.GetUtcNow();

        var outcome = await ProcessAsync(fixture, work);

        AssertDenied(outcome);
    }

    [Fact]
    public async Task Inactive_support_case_prevents_dispatch()
    {
        var fixture = CreateSupportFixture();
        var work = CreateWork(fixture, TenantWorkScopeRequest.TenantWide(), "durable-work.support-test");
        fixture.Service.CloseSupportCase(fixture.SupportCase!.Value);

        var outcome = await ProcessAsync(fixture, work);

        AssertDenied(outcome);
    }

    [Fact]
    public void Platform_governance_has_no_tenant_work_scope_or_authority_path()
    {
        Assert.DoesNotContain(
            typeof(DurableWorkItem).GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static),
            method => method.GetParameters().Any(parameter => parameter.ParameterType == typeof(PlatformGovernanceContext)));
        Assert.DoesNotContain(
            typeof(TenantDurableWorkWorker).GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance),
            method => method.GetParameters().Any(parameter => parameter.ParameterType == typeof(PlatformGovernanceContext)));
    }

    private static AuthorityFixture CreateOrdinaryFixture(OrganizationScope? grantedScope = null)
    {
        var clock = new ManualTimeProvider();
        var service = new IdentityAuthorizationService(
            timeProvider: clock,
            operationCatalogue: OperationCatalogue);
        var password = "Correct-horse-battery-1!";
        var actor = service.CreateUser("durable-owner@example.com", password);
        var approver = service.CreateUser("durable-approver@example.com", password);
        var membership = service.AddMembership(actor, TenantA);
        SeedOrganizationGraph(service, TenantA);
        SeedOrganizationGraph(service, TenantB);
        var role = service.CreateRole("durable-reader", TenantA, false, [IdentityPermissions.Read]);
        service.Store.RoleAssignments[membership].Add(
            new RoleAssignment(role, membership, actor, TenantA, approver));
        var selectedScope = grantedScope ?? OrganizationScope.ForTenant(TenantA);
        AddScopeGrant(service, membership, selectedScope, approver);

        var authentication = service.Authenticate("durable-owner@example.com", password);
        Assert.True(authentication.Succeeded);
        var correlation = new CorrelationId($"durable-{Guid.NewGuid():N}");
        var decision = service.AuthorizeOrdinary(
            authentication.CookieValue!,
            TenantA,
            IdentityPermissions.Read,
            selectedScope,
            correlation);
        Assert.True(decision.Allowed);
        Assert.NotNull(decision.TenantContext);

        return new AuthorityFixture(
            service,
            clock,
            TenantA,
            actor,
            membership,
            new SessionSnapshot(authentication.SessionId!.Value, authentication.CookieValue!),
            decision.TenantContext!,
            correlation,
            null,
            null);
    }

    private static AuthorityFixture CreateSupportFixture(TimeSpan? grantLifetime = null)
    {
        var clock = new ManualTimeProvider();
        var service = new IdentityAuthorizationService(
            timeProvider: clock,
            operationCatalogue: OperationCatalogue);
        var password = "Correct-horse-battery-1!";
        var supportUser = service.CreateUser("durable-support@example.com", password);
        var approver = service.CreateUser("durable-support-approver@example.com", password);
        var supportCase = service.AddSupportCase(TenantA);
        var grant = new SupportGrant(
            new SupportGrantId(Guid.NewGuid()),
            supportCase,
            supportUser,
            approver,
            TenantA,
            "durable-work-test",
            OrganizationScope.ForTenant(TenantA),
            [IdentityPermissions.SupportRead],
            clock.GetUtcNow().Add(grantLifetime ?? TimeSpan.FromHours(1)));
        service.Store.SupportGrants.Add(grant.Id, grant);

        var authentication = service.Authenticate("durable-support@example.com", password);
        Assert.True(authentication.Succeeded);
        var session = new SessionSnapshot(authentication.SessionId!.Value, authentication.CookieValue!);
        service.Store.Sessions[session.Id].SupportGrantReferences.Add(grant.Id);
        var correlation = new CorrelationId($"durable-support-{Guid.NewGuid():N}");
        var context = TenantContext.ForSupportGrant(
            TenantA,
            new SupportGrantReference(grant.Id.Value, supportCase.Value),
            new ScopeReference($"Tenant:{TenantA.Value}"),
            correlation,
            supportUser.Value);

        return new AuthorityFixture(
            service,
            clock,
            TenantA,
            supportUser,
            null,
            session,
            context,
            correlation,
            grant,
            supportCase);
    }

    private static DurableWorkItem CreateWork(
        AuthorityFixture fixture,
        TenantWorkScopeRequest requestedScope,
        string operationId = "durable-work.test")
    {
        var resolution = fixture.Service.Resolve(fixture.Context, requestedScope);
        Assert.True(resolution.Allowed, resolution.SafeReason);
        var identity = DurableWorkIdentity.Create(
            Guid.NewGuid(),
            fixture.Service.OperationCatalogue,
            operationId,
            fixture.Correlation,
            $"work-{Guid.NewGuid():N}");
        return DurableWorkItem.Create(
            fixture.Context,
            resolution.Scope!,
            identity,
            new AuthorityPayload("safe-payload"),
            fixture.Session.Id.Value,
            3,
            fixture.Clock.GetUtcNow());
    }

    private static async Task<ProcessOutcome> ProcessAsync(
        AuthorityFixture fixture,
        DurableWorkItem work)
    {
        var store = new InMemoryRelationalDurableWorkStore();
        Assert.True(await store.SubmitAsync(work));
        var handler = new CountingHandler(work.Identity.Operation);
        var dispatcher = new DurableWorkDispatcher(fixture.Service.OperationCatalogue);
        dispatcher.Register(handler);
        var worker = new TenantDurableWorkWorker(store, dispatcher, fixture.Service);
        var completion = await worker.ProcessOneAsync(
            fixture.Context,
            Guid.NewGuid(),
            fixture.Clock.GetUtcNow(),
            TimeSpan.FromMinutes(5));
        Assert.NotNull(completion);
        return new ProcessOutcome(
            completion!,
            handler.Calls,
            await store.ReadAuditAsync(fixture.Context),
            fixture.Service.GetEvidence());
    }

    private static void AssertDenied(ProcessOutcome outcome)
    {
        Assert.Equal(DurableWorkLifecycle.DeadLetter, outcome.Completion.Lifecycle);
        Assert.Equal(DurableWorkFailureCategory.AuthorizationDenied, outcome.Completion.FailureCategory);
        Assert.Equal(0, outcome.HandlerCalls);
        Assert.Contains(outcome.Audit, item => item.EventType == "work.dead-letter");
    }

    private static void SeedOrganizationGraph(IdentityAuthorizationService service, TenantId tenantId)
    {
        var company = tenantId == TenantA ? CompanyA : CompanyB;
        var branch = tenantId == TenantA ? BranchA : BranchB;
        var warehouse = tenantId == TenantA ? WarehouseA : WarehouseB;
        service.SetOrganizationParent(
            OrganizationScope.ForCompany(tenantId, company),
            OrganizationScope.ForTenant(tenantId));
        service.SetOrganizationParent(
            OrganizationScope.ForBranch(tenantId, branch),
            OrganizationScope.ForCompany(tenantId, company));
        service.SetOrganizationParent(
            OrganizationScope.ForWarehouse(tenantId, warehouse),
            OrganizationScope.ForBranch(tenantId, branch));
    }

    private static void AddScopeGrant(
        IdentityAuthorizationService service,
        MembershipId membership,
        OrganizationScope scope,
        UserId approver)
    {
        var target = service.Store.Memberships[membership];
        var grantId = new ScopeGrantId(Guid.NewGuid());
        service.Store.ScopeGrants.Add(
            grantId,
            new AccessScopeGrant(grantId, membership, target.UserId, scope, approver));
        service.Store.ScopeGrantsByMembership[membership].Add(grantId);
    }

    private sealed class AuthorityFixture
    {
        internal AuthorityFixture(
            IdentityAuthorizationService service,
            ManualTimeProvider clock,
            TenantId tenant,
            UserId actor,
            MembershipId? membership,
            SessionSnapshot session,
            TenantContext context,
            CorrelationId correlation,
            SupportGrant? supportGrant,
            SupportCaseId? supportCase)
        {
            Service = service;
            Clock = clock;
            Tenant = tenant;
            Actor = actor;
            Membership = membership;
            Session = session;
            Context = context;
            Correlation = correlation;
            SupportGrant = supportGrant;
            SupportCase = supportCase;
        }

        internal IdentityAuthorizationService Service { get; }
        internal ManualTimeProvider Clock { get; }
        internal TenantId Tenant { get; }
        internal UserId Actor { get; }
        internal MembershipId? Membership { get; }
        internal SessionSnapshot Session { get; }
        internal TenantContext Context { get; }
        internal CorrelationId Correlation { get; }
        internal SupportGrant? SupportGrant { get; }
        internal SupportCaseId? SupportCase { get; }
    }

    private sealed record SessionSnapshot(SessionId Id, string CookieValue);

    private sealed record ProcessOutcome(
        DurableWorkCompletion Completion,
        int HandlerCalls,
        IReadOnlyList<DurableWorkAuditRecord> Audit,
        IReadOnlyList<SafeSecurityEvidence> Evidence);

    private sealed record AuthorityPayload(string Value) : IWorkPayload;

    private sealed class CountingHandler : IDurableWorkHandler<AuthorityPayload>
    {
        private readonly DurableWorkOperationDescriptor operation;

        internal CountingHandler(DurableWorkOperationDescriptor operation)
        {
            this.operation = operation;
        }

        internal int Calls { get; private set; }

        public DurableWorkOperationDescriptor Operation => operation;

        public ValueTask<DurableWorkHandlerResult> ExecuteAsync(
            AuthorityPayload payload,
            DurableWorkExecutionContext context,
            CancellationToken cancellationToken = default)
        {
            Calls++;
            return ValueTask.FromResult(DurableWorkHandlerResult.Succeeded());
        }
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset current = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => current;

        internal void Advance(TimeSpan duration) => current = current.Add(duration);
    }
}
