using System.Text.Json;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.BuildingBlocks.Work;
using MiniErp.App.Modules.Identity;
using Xunit;

namespace MiniErp.ArchitectureTests;

/// <summary>
/// H92-04 focused review correction: reconciliation reads must be authorized
/// by exact scope, not by raw TenantId alone. M92-03 focused review
/// correction: uncertain-effect records must carry an exact, safe effect
/// identity sufficient to filter and reconcile them.
/// </summary>
public sealed class DurableWorkReconciliationAuthorizationTests
{
    private static readonly DateTimeOffset Clock = new(2026, 8, 6, 0, 0, 0, TimeSpan.Zero);
    private static readonly TenantId TenantA = new(Guid.Parse("31111111-1111-1111-1111-111111111111"));
    private static readonly TenantId TenantB = new(Guid.Parse("32222222-2222-2222-2222-222222222222"));
    private static readonly Guid CompanyA1 = Guid.Parse("31aaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa1");
    private static readonly Guid CompanyA2 = Guid.Parse("31aaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaa2");
    private static readonly Guid BranchA1a = Guid.Parse("31bbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbb01");
    private static readonly Guid BranchA1b = Guid.Parse("31bbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbb02");
    private static readonly Guid WarehouseA1a1 = Guid.Parse("31cccccc-cccc-cccc-cccc-cccccccccc01");
    private static readonly Guid WarehouseA1a2 = Guid.Parse("31cccccc-cccc-cccc-cccc-cccccccccc02");
    private static readonly DurableWorkOperationDescriptor Operation =
        new(
            "foundation.reconciliation-demo",
            "reconciliation-demo",
            "tenant.business.read",
            [TenantAuthorizationPath.OrdinaryMembership, TenantAuthorizationPath.SupportGrant]);
    private static readonly DurableWorkOperationCatalogue OperationCatalogue = new([Operation]);
    private static readonly DurableWorkPayloadRegistry PayloadRegistry = CreatePayloadRegistry();

    private static DurableWorkPayloadRegistry CreatePayloadRegistry()
    {
        var registry = new DurableWorkPayloadRegistry();
        registry.Register(new DurableWorkPayloadTypeId("test.reconciliation-payload"), new JsonDurableWorkPayloadCodec<DemoPayload>());
        return registry;
    }

    // ---------------------------------------------------------------------
    // H92-04: exact-scope authorization
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Tenant_a_cannot_read_tenant_b_uncertainty()
    {
        var fixtureA = CreateOrdinaryFixture(TenantA, OrganizationScope.ForTenant(TenantA));
        var fixtureB = CreateOrdinaryFixture(TenantB, OrganizationScope.ForTenant(TenantB));
        var store = NewStore();
        await CreateOutboxUncertainRecordAsync(store, fixtureB.Context, TenantWorkScopeRequest.TenantWide(), "cross-tenant");

        var authorizationA = await AuthorizeAsync(fixtureA, TenantWorkScopeRequest.TenantWide());
        Assert.True(authorizationA.Allowed);
        var recordsA = await store.ReadUncertainEffectsAsync(authorizationA.Authorization!);

        Assert.Empty(recordsA);
    }

    [Fact]
    public async Task Company_a_cannot_read_company_b_uncertainty()
    {
        var fixture = CreateOrdinaryFixture(TenantA, OrganizationScope.ForCompany(TenantA, CompanyA1));
        var store = NewStore();
        await CreateOutboxUncertainRecordAsync(store, fixture.Context, TenantWorkScopeRequest.ForCompany(CompanyA1), "company-a1");
        var otherFixture = CreateOrdinaryFixture(TenantA, OrganizationScope.ForCompany(TenantA, CompanyA2));
        await CreateOutboxUncertainRecordAsync(store, otherFixture.Context, TenantWorkScopeRequest.ForCompany(CompanyA2), "company-a2");

        var authorization = await AuthorizeAsync(fixture, TenantWorkScopeRequest.ForCompany(CompanyA1));
        Assert.True(authorization.Allowed);
        var records = await store.ReadUncertainEffectsAsync(authorization.Authorization!);

        var record = Assert.Single(records);
        Assert.Equal(CompanyA1, record.Scope.CompanyId);
    }

    [Fact]
    public async Task Branch_a_cannot_read_sibling_branch_b_uncertainty()
    {
        var fixture = CreateOrdinaryFixture(TenantA, OrganizationScope.ForBranch(TenantA, BranchA1a));
        var store = NewStore();
        await CreateOutboxUncertainRecordAsync(store, fixture.Context, TenantWorkScopeRequest.ForBranch(CompanyA1, BranchA1a), "branch-a1a");
        var otherFixture = CreateOrdinaryFixture(TenantA, OrganizationScope.ForBranch(TenantA, BranchA1b));
        await CreateOutboxUncertainRecordAsync(store, otherFixture.Context, TenantWorkScopeRequest.ForBranch(CompanyA1, BranchA1b), "branch-a1b");

        var authorization = await AuthorizeAsync(fixture, TenantWorkScopeRequest.ForBranch(CompanyA1, BranchA1a));
        Assert.True(authorization.Allowed);
        var records = await store.ReadUncertainEffectsAsync(authorization.Authorization!);

        var record = Assert.Single(records);
        Assert.Equal(BranchA1a, record.Scope.BranchId);
    }

    [Fact]
    public async Task Warehouse_a_cannot_read_sibling_warehouse_b_uncertainty()
    {
        var fixture = CreateOrdinaryFixture(TenantA, OrganizationScope.ForWarehouse(TenantA, WarehouseA1a1));
        var store = NewStore();
        await CreateOutboxUncertainRecordAsync(
            store, fixture.Context, TenantWorkScopeRequest.ForWarehouse(CompanyA1, BranchA1a, WarehouseA1a1), "warehouse-a1a1");
        var otherFixture = CreateOrdinaryFixture(TenantA, OrganizationScope.ForWarehouse(TenantA, WarehouseA1a2));
        await CreateOutboxUncertainRecordAsync(
            store, otherFixture.Context, TenantWorkScopeRequest.ForWarehouse(CompanyA1, BranchA1a, WarehouseA1a2), "warehouse-a1a2");

        var authorization = await AuthorizeAsync(fixture, TenantWorkScopeRequest.ForWarehouse(CompanyA1, BranchA1a, WarehouseA1a1));
        Assert.True(authorization.Allowed);
        var records = await store.ReadUncertainEffectsAsync(authorization.Authorization!);

        var record = Assert.Single(records);
        Assert.Equal(WarehouseA1a1, record.Scope.WarehouseId);
    }

    [Fact]
    public async Task Tenant_wide_authorized_scope_can_read_valid_descendants()
    {
        var fixture = CreateOrdinaryFixture(TenantA, OrganizationScope.ForTenant(TenantA));
        var store = NewStore();
        await CreateOutboxUncertainRecordAsync(store, fixture.Context, TenantWorkScopeRequest.TenantWide(), "tenant-wide");
        await CreateOutboxUncertainRecordAsync(store, fixture.Context, TenantWorkScopeRequest.ForCompany(CompanyA1), "descendant-company");
        await CreateOutboxUncertainRecordAsync(
            store, fixture.Context, TenantWorkScopeRequest.ForWarehouse(CompanyA1, BranchA1a, WarehouseA1a1), "descendant-warehouse");

        var authorization = await AuthorizeAsync(fixture, TenantWorkScopeRequest.TenantWide());
        Assert.True(authorization.Allowed);
        var records = await store.ReadUncertainEffectsAsync(authorization.Authorization!);

        Assert.Equal(3, records.Count);
    }

    [Fact]
    public async Task Missing_permission_is_denied()
    {
        var fixture = CreateOrdinaryFixture(TenantA, OrganizationScope.ForTenant(TenantA), grantReconciliationPermission: false);

        var authorization = await AuthorizeAsync(fixture, TenantWorkScopeRequest.TenantWide());

        Assert.False(authorization.Allowed);
    }

    [Fact]
    public async Task Wrong_operation_permission_does_not_substitute()
    {
        // A membership holding only the unrelated Export permission must not
        // satisfy the dedicated reconciliation-read permission check.
        var fixture = CreateOrdinaryFixture(
            TenantA,
            OrganizationScope.ForTenant(TenantA),
            grantReconciliationPermission: false,
            additionalPermission: IdentityPermissions.Export);

        var authorization = await AuthorizeAsync(fixture, TenantWorkScopeRequest.TenantWide());

        Assert.False(authorization.Allowed);
    }

    [Fact]
    public async Task Expired_session_is_denied()
    {
        var fixture = CreateOrdinaryFixture(TenantA, OrganizationScope.ForTenant(TenantA));
        fixture.Clock.Advance(TimeSpan.FromHours(8));

        var authorization = await AuthorizeAsync(fixture, TenantWorkScopeRequest.TenantWide());

        Assert.False(authorization.Allowed);
    }

    [Fact]
    public async Task Suspended_membership_is_denied()
    {
        var fixture = CreateOrdinaryFixture(TenantA, OrganizationScope.ForTenant(TenantA));
        fixture.Service.Store.Memberships[fixture.Membership!.Value].Status = MembershipStatus.Suspended;

        var authorization = await AuthorizeAsync(fixture, TenantWorkScopeRequest.TenantWide());

        Assert.False(authorization.Allowed);
    }

    [Fact]
    public async Task Expired_support_grant_is_denied()
    {
        var fixture = CreateSupportFixture(TenantA, OrganizationScope.ForTenant(TenantA), TimeSpan.FromMinutes(5));
        fixture.Clock.Advance(TimeSpan.FromMinutes(6));

        var authorization = await AuthorizeAsync(fixture, TenantWorkScopeRequest.TenantWide());

        Assert.False(authorization.Allowed);
    }

    [Fact]
    public async Task Revoked_support_grant_is_denied()
    {
        var fixture = CreateSupportFixture(TenantA, OrganizationScope.ForTenant(TenantA));
        fixture.SupportGrant!.RevokedAt = fixture.Clock.GetUtcNow();

        var authorization = await AuthorizeAsync(fixture, TenantWorkScopeRequest.TenantWide());

        Assert.False(authorization.Allowed);
    }

    [Fact]
    public async Task Support_grant_ordinary_membership_fallback_is_prohibited()
    {
        // An expired SupportGrant path must not silently fall back to an
        // ordinary-membership evaluation: there is no ordinary Membership on
        // this actor at all, so any fallback would itself be a bug.
        var fixture = CreateSupportFixture(TenantA, OrganizationScope.ForTenant(TenantA), TimeSpan.FromMinutes(5));
        fixture.Clock.Advance(TimeSpan.FromMinutes(6));

        var authorization = await AuthorizeAsync(fixture, TenantWorkScopeRequest.TenantWide());

        Assert.False(authorization.Allowed);
        Assert.Null(fixture.Membership);
    }

    [Fact]
    public void Platform_governance_is_never_a_reconciliation_authority_path()
    {
        var authorizeMethod = typeof(IDurableWorkReconciliationAuthorizer).GetMethod(nameof(IDurableWorkReconciliationAuthorizer.AuthorizeAsync))!;

        Assert.DoesNotContain(
            authorizeMethod.GetParameters(),
            parameter => parameter.ParameterType == typeof(PlatformGovernanceContext));
        Assert.Equal(typeof(TenantContext), authorizeMethod.GetParameters()[0].ParameterType);
    }

    [Fact]
    public async Task Missing_selected_scope_is_denied()
    {
        var fixture = CreateOrdinaryFixture(TenantA, OrganizationScope.ForTenant(TenantA));
        var missingScopeContext = TenantContext.ForOrdinaryMembership(
            TenantA,
            new MembershipReference(fixture.Membership!.Value.Value),
            scope: null,
            fixture.Correlation,
            fixture.Actor.Value);

        var authorization = await fixture.Service.AuthorizeAsync(
            missingScopeContext,
            fixture.Session.Id.Value,
            TenantWorkScopeRequest.TenantWide(),
            fixture.Clock.GetUtcNow());

        Assert.False(authorization.Allowed);
    }

    [Fact]
    public async Task Safe_denial_reveals_no_foreign_record_identity()
    {
        var fixtureA = CreateOrdinaryFixture(TenantA, OrganizationScope.ForCompany(TenantA, CompanyA1));
        var otherFixture = CreateOrdinaryFixture(TenantA, OrganizationScope.ForCompany(TenantA, CompanyA2));
        var store = NewStore();
        var foreignSecret = $"foreign-record-{Guid.NewGuid():N}";
        await CreateOutboxUncertainRecordAsync(store, otherFixture.Context, TenantWorkScopeRequest.ForCompany(CompanyA2), foreignSecret);

        var authorization = await AuthorizeAsync(fixtureA, TenantWorkScopeRequest.ForCompany(CompanyA2));

        Assert.False(authorization.Allowed);
        var denialJson = JsonSerializer.Serialize(new { authorization.SafeReason });
        Assert.DoesNotContain(foreignSecret, denialJson, StringComparison.Ordinal);
        Assert.DoesNotContain(CompanyA2.ToString(), denialJson, StringComparison.OrdinalIgnoreCase);
    }

    // ---------------------------------------------------------------------
    // M92-03: exact uncertain-effect identity
    // ---------------------------------------------------------------------

    [Fact]
    public async Task Handler_uncertain_record_contains_exact_operation_id_and_no_event_id()
    {
        var context = CreateContext();
        var store = NewStore();
        var dispatcher = new DurableWorkDispatcher(OperationCatalogue, PayloadRegistry, StandaloneExecutor());
        dispatcher.Register(new OutcomeUnknownHandler());
        var worker = new TenantDurableWorkWorker(store, dispatcher, new FakeApprovingRevalidator());
        var work = Work(context, "handler-identity");
        await store.SubmitAsync(work);
        await worker.ProcessOneAsync(context, Guid.NewGuid(), Clock, TimeSpan.FromMinutes(5));

        var records = await store.ReadUncertainEffectsAsync(DurableWorkTestSupport.ApproveReconciliation(context));

        var record = Assert.Single(records);
        Assert.Equal(DurableWorkEffectPurpose.Handler, record.Purpose);
        Assert.Equal(work.Identity.OperationId, record.OperationId);
        Assert.Null(record.EventId);
    }

    [Fact]
    public async Task Outbox_uncertain_record_contains_the_exact_event_id()
    {
        var context = CreateContext();
        var store = NewStore();
        var work = Work(context, "outbox-identity");
        await store.SubmitAsync(work);
        await store.DispatchOutboxAsync(context, new FakeApprovingRevalidator(), Clock, (_, _, _) =>
            ValueTask.FromResult(DurableWorkProtectedEffectResult.OutcomeUnknown("provider_reported_uncertain")));

        var records = await store.ReadUncertainEffectsAsync(DurableWorkTestSupport.ApproveReconciliation(context));

        var record = Assert.Single(records);
        Assert.Equal(DurableWorkEffectPurpose.Outbox, record.Purpose);
        Assert.NotNull(record.EventId);
        Assert.NotEqual(Guid.Empty, record.EventId!.Value);
    }

    [Fact]
    public async Task Two_uncertain_outbox_event_ids_for_the_same_work_item_are_distinct_records()
    {
        var context = CreateContext();
        var store = NewStore();
        var work = Work(context, "outbox-two-events");
        await store.SubmitAsync(work);

        await store.DispatchOutboxAsync(context, new FakeApprovingRevalidator(), Clock, (_, _, _) =>
            ValueTask.FromResult(DurableWorkProtectedEffectResult.OutcomeUnknown("first_uncertain")));
        // Reuse the internal validation-only replay hook to simulate a second,
        // independent outbox event for the same work item.
        var replayed = store.ReplayOutboxForValidation(work.Identity.WorkItemId, Clock);
        Assert.False(replayed, "an OutcomeUnknown message must not be restarted by the generic replay path");

        var records = await store.ReadUncertainEffectsAsync(DurableWorkTestSupport.ApproveReconciliation(context));
        Assert.Single(records);
    }

    [Fact]
    public async Task Outcome_unknown_at_equals_the_actual_transition_time()
    {
        var context = CreateContext();
        var store = NewStore();
        var work = Work(context, "transition-time");
        await store.SubmitAsync(work);
        var transitionTime = Clock.AddMinutes(7);

        await store.DispatchOutboxAsync(context, new FakeApprovingRevalidator(), transitionTime, (_, _, _) =>
            ValueTask.FromResult(DurableWorkProtectedEffectResult.OutcomeUnknown("provider_reported_uncertain")));

        var records = await store.ReadUncertainEffectsAsync(DurableWorkTestSupport.ApproveReconciliation(context));
        var record = Assert.Single(records);
        Assert.Equal(transitionTime, record.OutcomeUnknownAt);
        Assert.NotEqual(work.NextAttemptAt, record.OutcomeUnknownAt);
    }

    [Fact]
    public async Task Safe_reason_is_preserved_for_outbox_records()
    {
        var context = CreateContext();
        var store = NewStore();
        var work = Work(context, "safe-reason-preserved");
        await store.SubmitAsync(work);

        await store.DispatchOutboxAsync(context, new FakeApprovingRevalidator(), Clock, (_, _, _) =>
            ValueTask.FromResult(DurableWorkProtectedEffectResult.OutcomeUnknown("provider_reported_specific_reason")));

        var records = await store.ReadUncertainEffectsAsync(DurableWorkTestSupport.ApproveReconciliation(context));
        var record = Assert.Single(records);
        Assert.Equal("provider_reported_specific_reason", record.SafeReason);
    }

    [Fact]
    public async Task Unsafe_provider_exception_is_not_preserved()
    {
        var context = CreateContext();
        var store = NewStore();
        var work = Work(context, "unsafe-exception-not-preserved");
        await store.SubmitAsync(work);
        var secretMarker = $"provider-secret-{Guid.NewGuid():N}";

        await store.DispatchOutboxAsync(context, new FakeApprovingRevalidator(), Clock, (_, _, _) =>
            throw new InvalidOperationException(secretMarker));

        var records = await store.ReadUncertainEffectsAsync(DurableWorkTestSupport.ApproveReconciliation(context));
        var record = Assert.Single(records);
        Assert.DoesNotContain(secretMarker, record.SafeReason, StringComparison.Ordinal);
        Assert.DoesNotContain(secretMarker, JsonSerializer.Serialize(record), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Records_remain_immutable()
    {
        var context = CreateContext();
        var store = NewStore();
        var work = Work(context, "immutable-record");
        await store.SubmitAsync(work);
        await store.DispatchOutboxAsync(context, new FakeApprovingRevalidator(), Clock, (_, _, _) =>
            ValueTask.FromResult(DurableWorkProtectedEffectResult.OutcomeUnknown("uncertain")));

        var records = await store.ReadUncertainEffectsAsync(DurableWorkTestSupport.ApproveReconciliation(context));
        Assert.Single(records);

        // Every property is either computed (no setter) or an init-only
        // record property; nothing is publicly mutable after construction.
        Assert.All(
            typeof(DurableWorkUncertainEffectRecord).GetProperties(),
            property => Assert.True(
                property.SetMethod is null
                    || property.SetMethod.ReturnParameter.GetRequiredCustomModifiers()
                        .Contains(typeof(System.Runtime.CompilerServices.IsExternalInit)),
                $"{property.Name} must be immutable (init-only or computed)."));
    }

    [Fact]
    public async Task Correlation_and_effect_key_remain_stable_across_reads()
    {
        var context = CreateContext();
        var store = NewStore();
        var work = Work(context, "stable-identity");
        await store.SubmitAsync(work);
        await store.DispatchOutboxAsync(context, new FakeApprovingRevalidator(), Clock, (_, _, _) =>
            ValueTask.FromResult(DurableWorkProtectedEffectResult.OutcomeUnknown("uncertain")));

        var first = Assert.Single(await store.ReadUncertainEffectsAsync(DurableWorkTestSupport.ApproveReconciliation(context)));
        var second = Assert.Single(await store.ReadUncertainEffectsAsync(DurableWorkTestSupport.ApproveReconciliation(context)));

        Assert.Equal(first.CorrelationId, second.CorrelationId);
        Assert.Equal(first.EffectKey, second.EffectKey);
        Assert.Equal(work.Identity.CorrelationId, first.CorrelationId);
    }

    // ---------------------------------------------------------------------
    // Test infrastructure
    // ---------------------------------------------------------------------

    private static InMemoryDurableWorkStore NewStore() => new(StandaloneExecutor());

    private static IDurableWorkEffectExecutor StandaloneExecutor() =>
        new DurableWorkEffectExecutor(new InMemoryDurableWorkEffectGuard());

    private static async Task<DurableWorkReconciliationAuthorizationResult> AuthorizeAsync(
        ReconciliationFixture fixture,
        TenantWorkScopeRequest requestedScope) =>
        await fixture.Service.AuthorizeAsync(
            fixture.Context,
            fixture.Session.Id.Value,
            requestedScope,
            fixture.Clock.GetUtcNow());

    private static async Task<DurableWorkItem> CreateOutboxUncertainRecordAsync(
        InMemoryDurableWorkStore store,
        TenantContext mintingContext,
        TenantWorkScopeRequest requestedScope,
        string key)
    {
        var scope = TenantWorkScope.IssueFromVerifiedAuthority(mintingContext, requestedScope);
        var identity = DurableWorkIdentity.Create(
            Guid.NewGuid(),
            OperationCatalogue,
            Operation.OperationId,
            mintingContext.CorrelationId!.Value,
            key);
        var work = DurableWorkItem.Create(
            mintingContext,
            scope,
            identity,
            new DemoPayload("payload"),
            PayloadRegistry,
            Guid.NewGuid(),
            3,
            Clock);
        await store.SubmitAsync(work);
        await store.DispatchOutboxAsync(mintingContext, new FakeApprovingRevalidator(), Clock, (_, _, _) =>
            ValueTask.FromResult(DurableWorkProtectedEffectResult.OutcomeUnknown("provider_reported_uncertain")));
        return work;
    }

    private static DurableWorkItem Work(TenantContext context, string key) =>
        DurableWorkItem.Create(
            context,
            DurableWorkTestSupport.TenantWideScope(context),
            DurableWorkIdentity.Create(Guid.NewGuid(), OperationCatalogue, Operation.OperationId, context.CorrelationId!.Value, key),
            new DemoPayload("payload"),
            PayloadRegistry,
            Guid.NewGuid(),
            3,
            Clock);

    private static TenantContext CreateContext() =>
        TenantContext.ForOrdinaryMembership(
            new TenantId(Guid.NewGuid()),
            new MembershipReference(Guid.NewGuid()),
            new ScopeReference("tenant"),
            new CorrelationId($"corr-{Guid.NewGuid():N}"),
            Guid.NewGuid());

    private static ReconciliationFixture CreateOrdinaryFixture(
        TenantId tenant,
        OrganizationScope grantedScope,
        bool grantReconciliationPermission = true,
        MiniErp.App.Modules.Identity.PermissionCode? additionalPermission = null)
    {
        var clock = new ManualTimeProvider();
        var service = new IdentityAuthorizationService(timeProvider: clock, operationCatalogue: OperationCatalogue);
        var password = "Correct-horse-battery-1!";
        var actor = service.CreateUser($"reconciliation-owner-{Guid.NewGuid():N}@example.com", password);
        var approver = service.CreateUser($"reconciliation-approver-{Guid.NewGuid():N}@example.com", password);
        var membership = service.AddMembership(actor, tenant);
        SeedOrganizationGraph(service, tenant);
        var permissions = new List<PermissionCode> { IdentityPermissions.Read };
        if (grantReconciliationPermission)
        {
            permissions.Add(IdentityPermissions.DurableWorkReconciliationRead);
        }

        if (additionalPermission.HasValue)
        {
            permissions.Add(additionalPermission.Value);
        }

        var role = service.CreateRole("reconciliation-reader", tenant, false, permissions);
        service.Store.RoleAssignments[membership].Add(new RoleAssignment(role, membership, actor, tenant, approver));
        AddScopeGrant(service, membership, grantedScope, approver);

        var email = service.Store.Users[actor].NormalizedEmail;
        var authentication = service.Authenticate(email, password);
        Assert.True(authentication.Succeeded);
        var correlation = new CorrelationId($"reconciliation-{Guid.NewGuid():N}");
        var decision = service.AuthorizeOrdinary(
            authentication.CookieValue!,
            tenant,
            IdentityPermissions.Read,
            grantedScope,
            correlation);
        Assert.True(decision.Allowed);

        return new ReconciliationFixture(
            service,
            clock,
            actor,
            membership,
            new SessionSnapshot(authentication.SessionId!.Value, authentication.CookieValue!),
            decision.TenantContext!,
            correlation,
            null);
    }

    private static ReconciliationFixture CreateSupportFixture(
        TenantId tenant,
        OrganizationScope grantedScope,
        TimeSpan? grantLifetime = null)
    {
        var clock = new ManualTimeProvider();
        var service = new IdentityAuthorizationService(timeProvider: clock, operationCatalogue: OperationCatalogue);
        var password = "Correct-horse-battery-1!";
        var supportUser = service.CreateUser($"reconciliation-support-{Guid.NewGuid():N}@example.com", password);
        var approver = service.CreateUser($"reconciliation-support-approver-{Guid.NewGuid():N}@example.com", password);
        SeedOrganizationGraph(service, tenant);
        var supportCase = service.AddSupportCase(tenant);
        var grant = new SupportGrant(
            new SupportGrantId(Guid.NewGuid()),
            supportCase,
            supportUser,
            approver,
            tenant,
            "reconciliation-support-test",
            grantedScope,
            [IdentityPermissions.DurableWorkReconciliationRead],
            clock.GetUtcNow().Add(grantLifetime ?? TimeSpan.FromHours(1)));
        service.Store.SupportGrants.Add(grant.Id, grant);

        var email = service.Store.Users[supportUser].NormalizedEmail;
        var authentication = service.Authenticate(email, password);
        Assert.True(authentication.Succeeded);
        var session = new SessionSnapshot(authentication.SessionId!.Value, authentication.CookieValue!);
        service.Store.Sessions[session.Id].SupportGrantReferences.Add(grant.Id);
        var correlation = new CorrelationId($"reconciliation-support-{Guid.NewGuid():N}");
        var scopeReference = new ScopeReference($"{grantedScope.Kind}:{grantedScope.TargetId}");
        var context = TenantContext.ForSupportGrant(
            tenant,
            new SupportGrantReference(grant.Id.Value, supportCase.Value),
            scopeReference,
            correlation,
            supportUser.Value);

        return new ReconciliationFixture(service, clock, supportUser, null, session, context, correlation, grant);
    }

    private static void SeedOrganizationGraph(IdentityAuthorizationService service, TenantId tenantId)
    {
        service.SetOrganizationParent(OrganizationScope.ForCompany(tenantId, CompanyA1), OrganizationScope.ForTenant(tenantId));
        service.SetOrganizationParent(OrganizationScope.ForCompany(tenantId, CompanyA2), OrganizationScope.ForTenant(tenantId));
        service.SetOrganizationParent(OrganizationScope.ForBranch(tenantId, BranchA1a), OrganizationScope.ForCompany(tenantId, CompanyA1));
        service.SetOrganizationParent(OrganizationScope.ForBranch(tenantId, BranchA1b), OrganizationScope.ForCompany(tenantId, CompanyA1));
        service.SetOrganizationParent(OrganizationScope.ForWarehouse(tenantId, WarehouseA1a1), OrganizationScope.ForBranch(tenantId, BranchA1a));
        service.SetOrganizationParent(OrganizationScope.ForWarehouse(tenantId, WarehouseA1a2), OrganizationScope.ForBranch(tenantId, BranchA1a));
    }

    private static void AddScopeGrant(
        IdentityAuthorizationService service,
        MembershipId membership,
        OrganizationScope scope,
        UserId approver)
    {
        var target = service.Store.Memberships[membership];
        var grantId = new ScopeGrantId(Guid.NewGuid());
        service.Store.ScopeGrants.Add(grantId, new AccessScopeGrant(grantId, membership, target.UserId, scope, approver));
        service.Store.ScopeGrantsByMembership[membership].Add(grantId);
    }

    private sealed record DemoPayload(string Value) : IWorkPayload;

    private sealed class OutcomeUnknownHandler : IDurableWorkHandler<DemoPayload>
    {
        public DurableWorkOperationDescriptor Operation => DurableWorkReconciliationAuthorizationTests.Operation;

        public ValueTask<DurableWorkProtectedEffectResult> ExecuteAsync(
            DemoPayload payload,
            DurableWorkExecutionContext context,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(DurableWorkProtectedEffectResult.OutcomeUnknown("handler_reported_uncertain"));
    }

    private sealed class FakeApprovingRevalidator : IDurableWorkAuthorityRevalidator
    {
        public ValueTask<DurableWorkAuthorityValidationResult> RevalidateAsync(
            DurableWorkItem workItem,
            TenantContext currentTenantContext,
            DateTimeOffset now,
            CancellationToken cancellationToken = default) =>
            ValueTask.FromResult(DurableWorkTestSupport.Approve(workItem, currentTenantContext));
    }

    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset current = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => current;

        internal void Advance(TimeSpan duration) => current = current.Add(duration);
    }

    private sealed record SessionSnapshot(SessionId Id, string CookieValue);

    private sealed class ReconciliationFixture
    {
        internal ReconciliationFixture(
            IdentityAuthorizationService service,
            ManualTimeProvider clock,
            UserId actor,
            MembershipId? membership,
            SessionSnapshot session,
            TenantContext context,
            CorrelationId correlation,
            SupportGrant? supportGrant)
        {
            Service = service;
            Clock = clock;
            Actor = actor;
            Membership = membership;
            Session = session;
            Context = context;
            Correlation = correlation;
            SupportGrant = supportGrant;
        }

        internal IdentityAuthorizationService Service { get; }
        internal ManualTimeProvider Clock { get; }
        internal UserId Actor { get; }
        internal MembershipId? Membership { get; }
        internal SessionSnapshot Session { get; }
        internal TenantContext Context { get; }
        internal CorrelationId Correlation { get; }
        internal SupportGrant? SupportGrant { get; }
    }
}
