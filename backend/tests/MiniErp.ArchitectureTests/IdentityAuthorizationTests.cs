using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.Modules.Identity;
using Xunit;

namespace MiniErp.ArchitectureTests;

public sealed class IdentityAuthorizationTests
{
    [Fact]
    public void SuccessfulAuthenticationCreatesOneServerSideSession()
    {
        var fixture = CreateFixture();

        var result = fixture.Service.Authenticate("owner@example.com", fixture.Password);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.SessionId);
        Assert.NotNull(result.CookieValue);
        Assert.Equal(1, fixture.Service.CountActiveSessions(fixture.Owner));
    }

    [Fact]
    public void CookieConfigurationIsHttpOnlySecureAndBounded()
    {
        var options = FirstPartyCookieConfiguration.Create(IdentitySecurityOptions.Default);

        Assert.True(options.Cookie.HttpOnly);
        Assert.Equal(CookieSecurePolicy.Always, options.Cookie.SecurePolicy);
        Assert.Equal(SameSiteMode.Lax, options.Cookie.SameSite);
        Assert.False(options.SlidingExpiration);
        Assert.Equal(TimeSpan.FromHours(8), options.ExpireTimeSpan);
    }

    [Fact]
    public void CookieValueRequiresServerSideSessionState()
    {
        var fixture = CreateFixture();
        var result = fixture.Service.Authenticate("owner@example.com", fixture.Password);

        Assert.False(fixture.Service.ValidateSession(result.CookieValue! + "-tampered").Valid);
        Assert.False(fixture.Service.ValidateSession(string.Empty).Valid);
    }

    [Fact]
    public void SessionIsValidBeforeInactivityTimeout()
    {
        var fixture = CreateFixture();
        var session = Authenticate(fixture);

        fixture.Clock.Advance(TimeSpan.FromMinutes(29));

        Assert.True(fixture.Service.ValidateSession(session.CookieValue).Valid);
    }

    [Fact]
    public void SessionFailsAfterThirtyMinutesOfInactivity()
    {
        var fixture = CreateFixture();
        var session = Authenticate(fixture);

        fixture.Clock.Advance(TimeSpan.FromMinutes(30));

        Assert.False(fixture.Service.ValidateSession(session.CookieValue).Valid);
    }

    [Fact]
    public void SessionFailsAtEightHourAbsoluteMaximum()
    {
        var fixture = CreateFixture();
        var session = Authenticate(fixture);

        fixture.Clock.Advance(TimeSpan.FromHours(8));

        Assert.False(fixture.Service.ValidateSession(session.CookieValue).Valid);
    }

    [Fact]
    public void NormalActivityDoesNotExtendAbsoluteMaximum()
    {
        var fixture = CreateFixture();
        var session = Authenticate(fixture);

        fixture.Clock.Advance(TimeSpan.FromMinutes(29));
        Assert.True(fixture.Service.ValidateSession(session.CookieValue).Valid);
        fixture.Clock.Advance(TimeSpan.FromHours(7) + TimeSpan.FromMinutes(31));

        Assert.False(fixture.Service.ValidateSession(session.CookieValue).Valid);
    }

    [Fact]
    public void RevokedSessionFailsImmediately()
    {
        var fixture = CreateFixture();
        var session = Authenticate(fixture);

        fixture.Service.RevokeSession(session.Id, "test-revocation");

        Assert.False(fixture.Service.ValidateSession(session.CookieValue).Valid);
    }

    [Fact]
    public void SuspendedGlobalUserFailsImmediately()
    {
        var fixture = CreateFixture();
        var targetSession = Authenticate(fixture);
        var platform = CreatePlatformActor(fixture, IdentityPermissions.SuspendGlobalUser, "platform.user.suspend");

        var result = fixture.Service.SuspendUser(
            platform.Governance,
            platform.Session.CookieValue,
            fixture.Owner,
            "approved security action",
            fixture.Service.GetUserVersion(fixture.Owner),
            "suspend-owner-1");

        Assert.True(result.Succeeded);
        Assert.False(fixture.Service.ValidateSession(targetSession.CookieValue).Valid);
    }

    [Fact]
    public void LockedUserFailsSafely()
    {
        var fixture = CreateFixture();
        for (var attempt = 0; attempt < 5; attempt++)
        {
            Assert.Equal("authentication_failed", fixture.Service.Authenticate("owner@example.com", "wrong-password").PublicCode);
        }

        var locked = fixture.Service.Authenticate("owner@example.com", fixture.Password);

        Assert.False(locked.Succeeded);
        Assert.Equal("authentication_failed", locked.PublicCode);
    }

    [Fact]
    public void AuthenticationResultDoesNotExposePasswordHashOrSecretFields()
    {
        var publicProperties = typeof(AuthenticationResult).GetProperties();

        Assert.DoesNotContain(publicProperties, property => property.Name.Contains("Password", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(publicProperties, property => property.Name.Contains("Hash", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void FirstFourFailedAttemptsDoNotLockPrematurely()
    {
        var fixture = CreateFixture();

        for (var attempt = 0; attempt < 4; attempt++)
        {
            Assert.False(fixture.Service.Authenticate("owner@example.com", "wrong-password").Succeeded);
        }

        Assert.True(fixture.Service.Authenticate("owner@example.com", fixture.Password).Succeeded);
    }

    [Fact]
    public void FifthFailedAttemptLocksForFifteenMinutes()
    {
        var fixture = CreateFixture();
        for (var attempt = 0; attempt < 5; attempt++)
        {
            fixture.Service.Authenticate("owner@example.com", "wrong-password");
        }

        fixture.Clock.Advance(TimeSpan.FromMinutes(14));
        Assert.False(fixture.Service.Authenticate("owner@example.com", fixture.Password).Succeeded);
        fixture.Clock.Advance(TimeSpan.FromMinutes(1));
        Assert.True(fixture.Service.Authenticate("owner@example.com", fixture.Password).Succeeded);
    }

    [Fact]
    public void AuthenticationDuringLockoutFailsSafely()
    {
        var fixture = CreateFixture();
        for (var attempt = 0; attempt < 5; attempt++)
        {
            fixture.Service.Authenticate("owner@example.com", "wrong-password");
        }

        var result = fixture.Service.Authenticate("owner@example.com", fixture.Password);

        Assert.False(result.Succeeded);
        Assert.Equal("authentication_failed", result.PublicCode);
    }

    [Fact]
    public void AuthenticationAfterLockoutExpiryFollowsResetBehavior()
    {
        var fixture = CreateFixture();
        for (var attempt = 0; attempt < 5; attempt++)
        {
            fixture.Service.Authenticate("owner@example.com", "wrong-password");
        }

        fixture.Clock.Advance(TimeSpan.FromMinutes(15));

        Assert.True(fixture.Service.Authenticate("owner@example.com", fixture.Password).Succeeded);
    }

    [Fact]
    public void UnknownAndKnownUserFailuresAreExternallyNonDisclosing()
    {
        var fixture = CreateFixture();

        var known = fixture.Service.Authenticate("owner@example.com", "wrong-password");
        var unknown = fixture.Service.Authenticate("unknown@example.com", "wrong-password");

        Assert.Equal(known.PublicCode, unknown.PublicCode);
        Assert.Equal(known.Succeeded, unknown.Succeeded);
    }

    [Fact]
    public void ActiveOrdinaryMembershipRolePermissionAndScopeSucceed()
    {
        var fixture = CreateFixture();
        var session = Authenticate(fixture);

        var decision = fixture.Service.AuthorizeOrdinary(
            session.CookieValue,
            fixture.TenantA,
            IdentityPermissions.Read,
            OrganizationScope.ForTenant(fixture.TenantA),
            fixture.Correlation);

        Assert.True(decision.Allowed);
        Assert.Equal(TenantAuthorizationPath.OrdinaryMembership, decision.TenantContext!.AuthorizationPath);
    }

    [Fact]
    public void MissingMembershipDenies()
    {
        var fixture = CreateFixture();
        var user = fixture.Service.CreateUser("nomembership@example.com", fixture.Password);
        var session = Authenticate(fixture, user, "nomembership@example.com");

        Assert.False(fixture.Service.AuthorizeOrdinary(session.CookieValue, fixture.TenantA, IdentityPermissions.Read, OrganizationScope.ForTenant(fixture.TenantA), fixture.Correlation).Allowed);
    }

    [Fact]
    public void InactiveMembershipDenies()
    {
        var fixture = CreateFixture();
        var membership = fixture.MembershipA;
        fixture.Service.Store.Memberships[membership].Status = MembershipStatus.Suspended;

        Assert.False(fixture.Service.AuthorizeOrdinary(Authenticate(fixture).CookieValue, fixture.TenantA, IdentityPermissions.Read, OrganizationScope.ForTenant(fixture.TenantA), fixture.Correlation).Allowed);
    }

    [Fact]
    public void MissingRoleAssignmentDenies()
    {
        var fixture = CreateFixture(withRole: false);

        Assert.False(fixture.Service.AuthorizeOrdinary(Authenticate(fixture).CookieValue, fixture.TenantA, IdentityPermissions.Read, OrganizationScope.ForTenant(fixture.TenantA), fixture.Correlation).Allowed);
    }

    [Fact]
    public void MissingPermissionDenies()
    {
        var fixture = CreateFixture(permission: IdentityPermissions.SuspendMembership);

        Assert.False(fixture.Service.AuthorizeOrdinary(Authenticate(fixture).CookieValue, fixture.TenantA, IdentityPermissions.Read, OrganizationScope.ForTenant(fixture.TenantA), fixture.Correlation).Allowed);
    }

    [Fact]
    public void MissingScopeDenies()
    {
        var fixture = CreateFixture(withScope: false);

        Assert.False(fixture.Service.AuthorizeOrdinary(Authenticate(fixture).CookieValue, fixture.TenantA, IdentityPermissions.Read, OrganizationScope.ForTenant(fixture.TenantA), fixture.Correlation).Allowed);
    }

    [Fact]
    public void WrongTenantDenies()
    {
        var fixture = CreateFixture();
        var tenantB = new TenantId(Guid.Parse("22222222-2222-2222-2222-222222222222"));

        Assert.False(fixture.Service.AuthorizeOrdinary(Authenticate(fixture).CookieValue, tenantB, IdentityPermissions.Read, OrganizationScope.ForTenant(tenantB), fixture.Correlation).Allowed);
    }

    [Fact]
    public void WrongBranchScopeDenies()
    {
        var fixture = CreateFixture(withScope: false);
        var branchA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        var branchB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        fixture = fixture with { Scope = OrganizationScope.ForBranch(fixture.TenantA, branchA) };
        fixture.Service.AddScopeGrant(fixture.MembershipA, fixture.Scope, fixture.Approver);

        var decision = fixture.Service.AuthorizeOrdinary(Authenticate(fixture).CookieValue, fixture.TenantA, IdentityPermissions.Read, OrganizationScope.ForBranch(fixture.TenantA, branchB), fixture.Correlation);

        Assert.False(decision.Allowed);
    }

    [Fact]
    public void DownwardInheritanceAllowsBranchGrantToDescendantWarehouseOnly()
    {
        var fixture = CreateFixture(withScope: false);
        var branch = OrganizationScope.ForBranch(fixture.TenantA, Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));
        var warehouse = OrganizationScope.ForWarehouse(fixture.TenantA, Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));
        fixture.Service.AddScopeGrant(fixture.MembershipA, branch, fixture.Approver);
        fixture.Service.SetOrganizationParent(warehouse, branch);

        Assert.True(fixture.Service.AuthorizeOrdinary(Authenticate(fixture).CookieValue, fixture.TenantA, IdentityPermissions.Read, warehouse, fixture.Correlation).Allowed);
    }

    [Fact]
    public void ClientTenantHintCannotExpandAuthority()
    {
        var fixture = CreateFixture();
        var tenantB = new TenantId(Guid.Parse("22222222-2222-2222-2222-222222222222"));

        var decision = fixture.Service.AuthorizeOrdinary(Authenticate(fixture).CookieValue, tenantB, IdentityPermissions.Read, OrganizationScope.ForTenant(tenantB), fixture.Correlation);

        Assert.False(decision.Allowed);
        Assert.Null(decision.TenantContext);
    }

    [Fact]
    public void ValidNamedTemporarySupportGrantSucceeds()
    {
        var fixture = CreateSupportFixture();
        var grant = fixture.SupportGrant;
        var operation = $"support:{grant.Value}:{IdentityPermissions.SupportRead.Value}";
        fixture.Service.CompleteMfa(fixture.SupportSession.Id);
        fixture.Service.MarkFreshAuthentication(fixture.SupportSession.Id, operation);

        var decision = fixture.Service.AuthorizeSupport(fixture.SupportSession.CookieValue, fixture.TenantA, grant, IdentityPermissions.SupportRead, OrganizationScope.ForTenant(fixture.TenantA), "incident diagnosis", fixture.Correlation);

        Assert.True(decision.Allowed);
        Assert.Equal(TenantAuthorizationPath.SupportGrant, decision.TenantContext!.AuthorizationPath);
    }

    [Fact]
    public void ExpiredSupportGrantDenies()
    {
        var fixture = CreateSupportFixture(lifetime: TimeSpan.FromMinutes(1));
        PrepareSupport(fixture);
        fixture.Clock.Advance(TimeSpan.FromMinutes(1));

        Assert.False(fixture.Service.AuthorizeSupport(fixture.SupportSession.CookieValue, fixture.TenantA, fixture.SupportGrant, IdentityPermissions.SupportRead, OrganizationScope.ForTenant(fixture.TenantA), "incident diagnosis", fixture.Correlation).Allowed);
    }

    [Fact]
    public void RevokedSupportGrantDeniesImmediately()
    {
        var fixture = CreateSupportFixture();
        PrepareSupport(fixture);
        fixture.Service.RevokeSupportGrant(fixture.SupportGrant);

        Assert.False(fixture.Service.AuthorizeSupport(fixture.SupportSession.CookieValue, fixture.TenantA, fixture.SupportGrant, IdentityPermissions.SupportRead, OrganizationScope.ForTenant(fixture.TenantA), "incident diagnosis", fixture.Correlation).Allowed);
    }

    [Fact]
    public void SupportGrantWrongTenantDenies()
    {
        var fixture = CreateSupportFixture();
        PrepareSupport(fixture);
        var otherTenant = new TenantId(Guid.Parse("22222222-2222-2222-2222-222222222222"));

        Assert.False(fixture.Service.AuthorizeSupport(fixture.SupportSession.CookieValue, otherTenant, fixture.SupportGrant, IdentityPermissions.SupportRead, OrganizationScope.ForTenant(otherTenant), "incident diagnosis", fixture.Correlation).Allowed);
    }

    [Fact]
    public void SupportGrantWrongScopeDenies()
    {
        var fixture = CreateSupportFixture();
        PrepareSupport(fixture);
        var warehouse = OrganizationScope.ForWarehouse(fixture.TenantA, Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));

        Assert.False(fixture.Service.AuthorizeSupport(fixture.SupportSession.CookieValue, fixture.TenantA, fixture.SupportGrant, IdentityPermissions.SupportRead, warehouse, "incident diagnosis", fixture.Correlation).Allowed);
    }

    [Fact]
    public void SupportGrantWrongPurposeDenies()
    {
        var fixture = CreateSupportFixture();
        PrepareSupport(fixture);

        Assert.False(fixture.Service.AuthorizeSupport(fixture.SupportSession.CookieValue, fixture.TenantA, fixture.SupportGrant, IdentityPermissions.SupportRead, OrganizationScope.ForTenant(fixture.TenantA), "export", fixture.Correlation).Allowed);
    }

    [Fact]
    public void SupportGrantRequiresMfa()
    {
        var fixture = CreateSupportFixture();
        var operation = $"support:{fixture.SupportGrant.Value}:{IdentityPermissions.SupportRead.Value}";
        fixture.Service.MarkFreshAuthentication(fixture.SupportSession.Id, operation);

        Assert.False(fixture.Service.AuthorizeSupport(fixture.SupportSession.CookieValue, fixture.TenantA, fixture.SupportGrant, IdentityPermissions.SupportRead, OrganizationScope.ForTenant(fixture.TenantA), "incident diagnosis", fixture.Correlation).Allowed);
    }

    [Fact]
    public void SupportGrantRequiresFreshAuthentication()
    {
        var fixture = CreateSupportFixture();
        fixture.Service.CompleteMfa(fixture.SupportSession.Id);

        Assert.False(fixture.Service.AuthorizeSupport(fixture.SupportSession.CookieValue, fixture.TenantA, fixture.SupportGrant, IdentityPermissions.SupportRead, OrganizationScope.ForTenant(fixture.TenantA), "incident diagnosis", fixture.Correlation).Allowed);
    }

    [Fact]
    public void OrdinaryMembershipAndSupportGrantTogetherAreRejected()
    {
        var fixture = CreateSupportFixture();
        var membership = fixture.Service.AddMembership(fixture.SupportUser, fixture.TenantA);
        var role = fixture.Service.CreateRole("support-ordinary", fixture.TenantA, false, [IdentityPermissions.SupportRead]);
        fixture.Service.AssignRole(membership, role, fixture.Approver);
        fixture.Service.AddScopeGrant(membership, OrganizationScope.ForTenant(fixture.TenantA), fixture.Approver);
        PrepareSupport(fixture);

        var ordinary = fixture.Service.AuthorizeOrdinary(fixture.SupportSession.CookieValue, fixture.TenantA, IdentityPermissions.SupportRead, OrganizationScope.ForTenant(fixture.TenantA), fixture.Correlation);
        var support = fixture.Service.AuthorizeSupport(fixture.SupportSession.CookieValue, fixture.TenantA, fixture.SupportGrant, IdentityPermissions.SupportRead, OrganizationScope.ForTenant(fixture.TenantA), "incident diagnosis", fixture.Correlation);

        Assert.False(ordinary.Allowed);
        Assert.False(support.Allowed);
    }

    [Fact]
    public void SupportGrantCreatesNoMembershipOrRoleAssignment()
    {
        var fixture = CreateSupportFixture();

        Assert.Equal(0, fixture.Service.CountMemberships(fixture.SupportUser, fixture.TenantA));
        Assert.Equal(0, fixture.Service.CountRoleAssignments(fixture.MembershipA));
    }

    [Fact]
    public void SupportGrantCannotGrantExportAuthority()
    {
        var fixture = CreateSupportFixture();

        Assert.Throws<InvalidOperationException>(() => fixture.Service.AddSupportGrant(fixture.SupportUser, fixture.SupportCase, fixture.Approver, fixture.TenantA, "export", OrganizationScope.ForTenant(fixture.TenantA), [IdentityPermissions.Export], TimeSpan.FromHours(1)));
    }

    [Fact]
    public void SupportGrantCannotAuthorizeGlobalUserLifecycle()
    {
        var fixture = CreateSupportFixture();
        PrepareSupport(fixture);

        var governance = new PlatformGovernanceContext(fixture.SupportUser.Value, PlatformGovernancePurpose.SecurityEvidence, fixture.Correlation);
        var result = fixture.Service.SuspendUser(governance, fixture.SupportSession.CookieValue, fixture.Owner, "support action", fixture.Service.GetUserVersion(fixture.Owner), "support-global-1");

        Assert.False(result.Succeeded);
    }

    [Fact]
    public void PlatformGovernancePurposeIsSeparateFromTenantAuthorization()
    {
        var fixture = CreateFixture();
        var platform = CreatePlatformActor(fixture, IdentityPermissions.SuspendGlobalUser, "platform.user.suspend");
        var governance = new PlatformGovernanceContext(platform.User.Value, PlatformGovernancePurpose.TenantLifecycleMetadata, fixture.Correlation);

        var result = fixture.Service.SuspendUser(governance, platform.Session.CookieValue, fixture.Owner, "wrong purpose", fixture.Service.GetUserVersion(fixture.Owner), "wrong-purpose-1");

        Assert.False(result.Succeeded);
    }

    [Fact]
    public void UnauthorizedPlatformActorCannotSuspendUser()
    {
        var fixture = CreateFixture();
        var actor = Authenticate(fixture);
        var governance = new PlatformGovernanceContext(fixture.Owner.Value, PlatformGovernancePurpose.SecurityEvidence, fixture.Correlation);

        Assert.False(fixture.Service.SuspendUser(governance, actor.CookieValue, fixture.OtherUser, "not authorized", fixture.Service.GetUserVersion(fixture.OtherUser), "unauthorized-1").Succeeded);
    }

    [Fact]
    public void AuthorizedPlatformLifecycleRequiresSpecificPermission()
    {
        var fixture = CreateFixture();
        var platform = CreatePlatformActor(fixture, IdentityPermissions.ReactivateGlobalUser, "platform.user.reactivate");

        Assert.False(fixture.Service.SuspendUser(platform.Governance, platform.Session.CookieValue, fixture.Owner, "missing permission", fixture.Service.GetUserVersion(fixture.Owner), "permission-1").Succeeded);
    }

    [Fact]
    public void AuthorizedPlatformLifecycleRequiresMfaAndFreshAuthentication()
    {
        var fixture = CreateFixture();
        var platformUser = fixture.Service.CreateUser("platform-no-evidence@example.com", fixture.Password);
        fixture.Service.GrantPlatformPermission(platformUser, IdentityPermissions.SuspendGlobalUser);
        var session = Authenticate(fixture, platformUser, "platform-no-evidence@example.com");
        var governance = new PlatformGovernanceContext(platformUser.Value, PlatformGovernancePurpose.SecurityEvidence, fixture.Correlation);

        Assert.False(fixture.Service.SuspendUser(governance, session.CookieValue, fixture.Owner, "missing evidence", fixture.Service.GetUserVersion(fixture.Owner), "evidence-1").Succeeded);
    }

    [Fact]
    public void GlobalSuspensionRevokesSessionsAcrossTenants()
    {
        var fixture = CreateFixture();
        var tenantB = new TenantId(Guid.Parse("22222222-2222-2222-2222-222222222222"));
        fixture.Service.AddMembership(fixture.Owner, tenantB);
        var targetSession = Authenticate(fixture);
        var platform = CreatePlatformActor(fixture, IdentityPermissions.SuspendGlobalUser, "platform.user.suspend");

        Assert.True(fixture.Service.SuspendUser(platform.Governance, platform.Session.CookieValue, fixture.Owner, "global suspension", fixture.Service.GetUserVersion(fixture.Owner), "global-suspend-1").Succeeded);
        Assert.False(fixture.Service.ValidateSession(targetSession.CookieValue).Valid);
        Assert.Equal(GlobalUserStatus.Suspended, fixture.Service.GetUserStatus(fixture.Owner));
    }

    [Fact]
    public void ReactivationDoesNotRestorePriorMembershipRoleScopeOrSupportAuthority()
    {
        var fixture = CreateFixture();
        var platform = CreatePlatformActor(fixture, IdentityPermissions.SuspendGlobalUser, "platform.user.suspend");
        Assert.True(fixture.Service.SuspendUser(platform.Governance, platform.Session.CookieValue, fixture.Owner, "suspend", fixture.Service.GetUserVersion(fixture.Owner), "reactivation-suspend-1").Succeeded);
        fixture.Service.GrantPlatformPermission(platform.User, IdentityPermissions.ReactivateGlobalUser);
        fixture.Service.MarkFreshAuthentication(platform.Session.Id, "platform.user.reactivate");

        Assert.True(fixture.Service.ReactivateUser(platform.Governance, platform.Session.CookieValue, fixture.Owner, "reactivate", fixture.Service.GetUserVersion(fixture.Owner), "reactivation-1").Succeeded);
        var newSession = Authenticate(fixture);
        var decision = fixture.Service.AuthorizeOrdinary(newSession.CookieValue, fixture.TenantA, IdentityPermissions.Read, OrganizationScope.ForTenant(fixture.TenantA), fixture.Correlation);

        Assert.False(decision.Allowed);
        Assert.Equal(0, fixture.Service.CountRoleAssignments(fixture.MembershipA));
        Assert.Equal(0, fixture.Service.CountActiveScopeGrants(fixture.MembershipA));
        Assert.Equal(MembershipStatus.Suspended, fixture.Service.GetMembershipStatus(fixture.MembershipA));
    }

    [Fact]
    public void OffboardingPreventsNewAuthentication()
    {
        var fixture = CreateFixture();
        var platform = CreatePlatformActor(fixture, IdentityPermissions.OffboardGlobalUser, "platform.user.offboard");

        Assert.True(fixture.Service.OffboardUser(platform.Governance, platform.Session.CookieValue, fixture.Owner, "offboard", fixture.Service.GetUserVersion(fixture.Owner), "offboard-1").Succeeded);
        Assert.False(fixture.Service.Authenticate("owner@example.com", fixture.Password).Succeeded);
    }

    [Fact]
    public void TenantAdministratorCanSuspendOwnTenantMembership()
    {
        var fixture = CreateFixture();
        var session = Authenticate(fixture);

        var result = fixture.Service.SuspendMembership(session.CookieValue, fixture.TenantA, fixture.MembershipA, "membership suspension", fixture.Service.Store.Memberships[fixture.MembershipA].Version, "tenant-suspend-1", fixture.Correlation);

        Assert.True(result.Succeeded);
        Assert.Equal(MembershipStatus.Suspended, fixture.Service.GetMembershipStatus(fixture.MembershipA));
        Assert.Equal(GlobalUserStatus.Active, fixture.Service.GetUserStatus(fixture.Owner));
    }

    [Fact]
    public void TenantAdministratorCannotModifyGlobalUserState()
    {
        var fixture = CreateFixture();
        var session = Authenticate(fixture);
        var governance = new PlatformGovernanceContext(fixture.Owner.Value, PlatformGovernancePurpose.SecurityEvidence, fixture.Correlation);

        Assert.False(fixture.Service.SuspendUser(governance, session.CookieValue, fixture.OtherUser, "tenant cannot global", fixture.Service.GetUserVersion(fixture.OtherUser), "tenant-global-1").Succeeded);
    }

    [Fact]
    public void TenantAdministratorCannotModifyAnotherTenantMembership()
    {
        var fixture = CreateFixture();
        var tenantB = new TenantId(Guid.Parse("22222222-2222-2222-2222-222222222222"));
        var membershipB = fixture.Service.AddMembership(fixture.OtherUser, tenantB);
        var session = Authenticate(fixture);

        var result = fixture.Service.SuspendMembership(session.CookieValue, fixture.TenantA, membershipB, "cross tenant", 1, "cross-tenant-membership-1", fixture.Correlation);

        Assert.False(result.Succeeded);
        Assert.Equal(MembershipStatus.Active, fixture.Service.GetMembershipStatus(membershipB));
    }

    [Fact]
    public void TenantScopedRevocationDoesNotRevokeUnrelatedTenantAuthority()
    {
        var fixture = CreateFixture();
        var tenantB = new TenantId(Guid.Parse("22222222-2222-2222-2222-222222222222"));
        var membershipB = fixture.Service.AddMembership(fixture.Owner, tenantB);
        var roleB = fixture.Service.CreateRole("tenant-b-reader", tenantB, false, [IdentityPermissions.Read]);
        fixture.Service.AssignRole(membershipB, roleB, fixture.Approver);
        fixture.Service.AddScopeGrant(membershipB, OrganizationScope.ForTenant(tenantB), fixture.Approver);
        var session = Authenticate(fixture);
        Assert.True(fixture.Service.AuthorizeOrdinary(session.CookieValue, fixture.TenantA, IdentityPermissions.Read, OrganizationScope.ForTenant(fixture.TenantA), fixture.Correlation).Allowed);
        Assert.True(fixture.Service.AuthorizeOrdinary(session.CookieValue, tenantB, IdentityPermissions.Read, OrganizationScope.ForTenant(tenantB), fixture.Correlation).Allowed);

        Assert.True(fixture.Service.SuspendMembership(session.CookieValue, fixture.TenantA, fixture.MembershipA, "only A", fixture.Service.Store.Memberships[fixture.MembershipA].Version, "tenant-a-only-1", fixture.Correlation).Succeeded);
        var bDecision = fixture.Service.AuthorizeOrdinary(session.CookieValue, tenantB, IdentityPermissions.Read, OrganizationScope.ForTenant(tenantB), fixture.Correlation);

        Assert.True(bDecision.Allowed);
    }

    [Fact]
    public void MembershipReactivationDoesNotResurrectRevokedRolesOrScopes()
    {
        var fixture = CreateFixture();
        var manager = AddTenantManager(fixture, "manager@example.com");
        Assert.True(fixture.Service.SuspendMembership(manager.CookieValue, fixture.TenantA, fixture.MembershipA, "suspend", fixture.Service.Store.Memberships[fixture.MembershipA].Version, "membership-reactivation-suspend-1", fixture.Correlation).Succeeded);

        Assert.True(fixture.Service.ReactivateMembership(manager.CookieValue, fixture.TenantA, fixture.MembershipA, "reactivate", fixture.Service.Store.Memberships[fixture.MembershipA].Version, "membership-reactivation-1", fixture.Correlation).Succeeded);
        var ownerSession = Authenticate(fixture);
        var decision = fixture.Service.AuthorizeOrdinary(ownerSession.CookieValue, fixture.TenantA, IdentityPermissions.Read, OrganizationScope.ForTenant(fixture.TenantA), fixture.Correlation);

        Assert.False(decision.Allowed);
        Assert.Equal(0, fixture.Service.CountRoleAssignments(fixture.MembershipA));
        Assert.Equal(0, fixture.Service.CountActiveScopeGrants(fixture.MembershipA));
    }

    [Fact]
    public void InvitationIsSingleUse()
    {
        var fixture = CreateFixture();
        fixture.Service.Store.Memberships[fixture.MembershipA].Status = MembershipStatus.Suspended;
        var invitation = fixture.Service.IssueInvitation(fixture.Owner, fixture.TenantA, fixture.MembershipA, "owner@example.com");

        Assert.True(fixture.Service.AcceptInvitation(invitation.TokenValue, fixture.Owner, fixture.TenantA));
        Assert.False(fixture.Service.AcceptInvitation(invitation.TokenValue, fixture.Owner, fixture.TenantA));
    }

    [Fact]
    public void ExpiredInvitationDenies()
    {
        var fixture = CreateFixture();
        fixture.Service.Store.Memberships[fixture.MembershipA].Status = MembershipStatus.Suspended;
        var invitation = fixture.Service.IssueInvitation(fixture.Owner, fixture.TenantA, fixture.MembershipA, "owner@example.com");
        fixture.Clock.Advance(TimeSpan.FromDays(7));

        Assert.False(fixture.Service.AcceptInvitation(invitation.TokenValue, fixture.Owner, fixture.TenantA));
    }

    [Fact]
    public void InvitationCannotCrossTenants()
    {
        var fixture = CreateFixture();
        var invitation = fixture.Service.IssueInvitation(fixture.Owner, fixture.TenantA, fixture.MembershipA, "owner@example.com");
        var tenantB = new TenantId(Guid.Parse("22222222-2222-2222-2222-222222222222"));

        Assert.False(fixture.Service.AcceptInvitation(invitation.TokenValue, fixture.Owner, tenantB));
    }

    [Fact]
    public void RecoveryArtifactIsSingleUse()
    {
        var fixture = CreateFixture();
        var recovery = fixture.Service.IssueRecovery(fixture.Owner);

        Assert.True(fixture.Service.UseRecovery(recovery.TokenValue, "New-password-1!"));
        Assert.False(fixture.Service.UseRecovery(recovery.TokenValue, "New-password-2!"));
    }

    [Fact]
    public void RecoveryUseRevokesApplicableSessions()
    {
        var fixture = CreateFixture();
        var session = Authenticate(fixture);
        var recovery = fixture.Service.IssueRecovery(fixture.Owner);

        Assert.True(fixture.Service.UseRecovery(recovery.TokenValue, "New-password-1!"));
        Assert.False(fixture.Service.ValidateSession(session.CookieValue).Valid);
    }

    [Fact]
    public void ConsumedRecoveryTokenCannotBeReplayed()
    {
        var fixture = CreateFixture();
        var recovery = fixture.Service.IssueRecovery(fixture.Owner);
        Assert.True(fixture.Service.UseRecovery(recovery.TokenValue, "New-password-1!"));

        Assert.False(fixture.Service.UseRecovery(recovery.TokenValue, "New-password-1!"));
    }

    [Fact]
    public void RepeatedSessionRevocationHasOneSafeEffect()
    {
        var fixture = CreateFixture();
        var session = Authenticate(fixture);

        fixture.Service.RevokeSession(session.Id, "first");
        fixture.Service.RevokeSession(session.Id, "second");

        Assert.Equal(0, fixture.Service.CountActiveSessions(fixture.Owner));
        Assert.False(fixture.Service.ValidateSession(session.CookieValue).Valid);
    }

    [Fact]
    public void RepeatedGlobalSuspensionDoesNotRestoreOrDuplicateState()
    {
        var fixture = CreateFixture();
        var platform = CreatePlatformActor(fixture, IdentityPermissions.SuspendGlobalUser, "platform.user.suspend");
        var expected = fixture.Service.GetUserVersion(fixture.Owner);

        Assert.True(fixture.Service.SuspendUser(platform.Governance, platform.Session.CookieValue, fixture.Owner, "suspend", expected, "concurrent-suspend-1").Succeeded);
        var repeated = fixture.Service.SuspendUser(platform.Governance, platform.Session.CookieValue, fixture.Owner, "suspend retry", expected, "concurrent-suspend-2");

        Assert.True(repeated.Succeeded);
        Assert.Equal(GlobalUserStatus.Suspended, fixture.Service.GetUserStatus(fixture.Owner));
        Assert.Equal(0, fixture.Service.CountRoleAssignments(fixture.MembershipA));
    }

    [Fact]
    public void RepeatedMembershipRevocationDoesNotRecreateGrants()
    {
        var fixture = CreateFixture();
        var manager = AddTenantManager(fixture, "manager-revoke@example.com");
        var expected = fixture.Service.Store.Memberships[fixture.MembershipA].Version;

        Assert.True(fixture.Service.RevokeMembership(manager.CookieValue, fixture.TenantA, fixture.MembershipA, "revoke", expected, "membership-revoke-1", fixture.Correlation).Succeeded);
        var repeated = fixture.Service.RevokeMembership(manager.CookieValue, fixture.TenantA, fixture.MembershipA, "revoke retry", expected, "membership-revoke-2", fixture.Correlation);

        Assert.True(repeated.Succeeded);
        Assert.Equal(0, fixture.Service.CountRoleAssignments(fixture.MembershipA));
        Assert.Equal(0, fixture.Service.CountActiveScopeGrants(fixture.MembershipA));
    }

    [Fact]
    public void RepeatedSupportGrantRevocationRemainsDenied()
    {
        var fixture = CreateSupportFixture();
        fixture.Service.RevokeSupportGrant(fixture.SupportGrant);
        fixture.Service.RevokeSupportGrant(fixture.SupportGrant);

        Assert.False(fixture.Service.AuthorizeSupport(fixture.SupportSession.CookieValue, fixture.TenantA, fixture.SupportGrant, IdentityPermissions.SupportRead, OrganizationScope.ForTenant(fixture.TenantA), "incident diagnosis", fixture.Correlation).Allowed);
    }

    [Fact]
    public void StaleConcurrencyStateFailsSafely()
    {
        var fixture = CreateFixture();
        var session = Authenticate(fixture);
        var staleVersion = fixture.Service.Store.Memberships[fixture.MembershipA].Version;
        fixture.Service.Store.Memberships[fixture.MembershipA].Version++;

        var result = fixture.Service.SuspendMembership(session.CookieValue, fixture.TenantA, fixture.MembershipA, "stale", staleVersion, "stale-membership-1", fixture.Correlation);

        Assert.False(result.Succeeded);
        Assert.Equal("access_denied", result.PublicCode);
    }

    [Fact]
    public void ForgedCookieCannotSelectAnotherSession()
    {
        var fixture = CreateFixture();
        var session = Authenticate(fixture);
        var forged = Convert.ToBase64String(Guid.NewGuid().ToByteArray());

        Assert.False(fixture.Service.ValidateSession(forged).Valid);
        Assert.True(fixture.Service.ValidateSession(session.CookieValue).Valid);
    }

    [Fact]
    public void ClosedSupportCaseDeniesImmediately()
    {
        var fixture = CreateSupportFixture();
        PrepareSupport(fixture);
        fixture.Service.CloseSupportCase(fixture.SupportCase);

        Assert.False(fixture.Service.AuthorizeSupport(fixture.SupportSession.CookieValue, fixture.TenantA, fixture.SupportGrant, IdentityPermissions.SupportRead, OrganizationScope.ForTenant(fixture.TenantA), "incident diagnosis", fixture.Correlation).Allowed);
    }

    [Fact]
    public void RoleAssignmentRejectsSelfApproval()
    {
        var fixture = CreateFixture(withRole: false);
        var role = fixture.Service.CreateRole("self-approved", fixture.TenantA, false, [IdentityPermissions.Read]);

        Assert.Throws<InvalidOperationException>(() => fixture.Service.AssignRole(fixture.MembershipA, role, fixture.Owner));
    }

    [Fact]
    public void ScopeGrantRejectsSelfApproval()
    {
        var fixture = CreateFixture(withScope: false);

        Assert.Throws<InvalidOperationException>(() => fixture.Service.AddScopeGrant(fixture.MembershipA, OrganizationScope.ForTenant(fixture.TenantA), fixture.Owner));
    }

    [Fact]
    public void TenantOwnedRoleCannotCrossTenantBoundary()
    {
        var fixture = CreateFixture(withRole: false);
        var tenantB = new TenantId(Guid.Parse("22222222-2222-2222-2222-222222222222"));
        var roleB = fixture.Service.CreateRole("tenant-b-role", tenantB, false, [IdentityPermissions.Read]);

        Assert.Throws<InvalidOperationException>(() => fixture.Service.AssignRole(fixture.MembershipA, roleB, fixture.Approver));
    }

    [Fact]
    public void BranchScopeDoesNotAuthorizeSidewaysWarehouse()
    {
        var fixture = CreateFixture(withScope: false);
        var branchA = OrganizationScope.ForBranch(fixture.TenantA, Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));
        var warehouseB = OrganizationScope.ForWarehouse(fixture.TenantA, Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));
        fixture.Service.AddScopeGrant(fixture.MembershipA, branchA, fixture.Approver);

        Assert.False(fixture.Service.AuthorizeOrdinary(Authenticate(fixture).CookieValue, fixture.TenantA, IdentityPermissions.Read, warehouseB, fixture.Correlation).Allowed);
    }

    [Fact]
    public void GovernanceContextCannotBeUsedAsTenantBusinessAuthorization()
    {
        var fixture = CreateFixture();
        var platformUser = fixture.Service.CreateUser("governance-only@example.com", fixture.Password);
        fixture.Service.GrantPlatformPermission(platformUser, IdentityPermissions.SuspendGlobalUser);
        var session = Authenticate(fixture, platformUser, "governance-only@example.com");
        var governance = new PlatformGovernanceContext(platformUser.Value, PlatformGovernancePurpose.SecurityEvidence, fixture.Correlation);

        var result = fixture.Service.SuspendMembership(session.CookieValue, fixture.TenantA, fixture.MembershipA, "governance cannot tenant", fixture.Service.Store.Memberships[fixture.MembershipA].Version, "governance-tenant-1", fixture.Correlation);

        Assert.False(result.Succeeded);
        Assert.Equal(PlatformGovernancePurpose.SecurityEvidence, governance.Purpose);
    }

    private static Fixture CreateFixture(
        bool withRole = true,
        bool withScope = true,
        PermissionCode? permission = null,
        OrganizationScope? scope = null)
    {
        var clock = new ManualTimeProvider();
        var service = new IdentityAuthorizationService(timeProvider: clock);
        var password = "Correct-horse-battery-1!";
        var tenantA = new TenantId(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        var owner = service.CreateUser("owner@example.com", password);
        var approver = service.CreateUser("approver@example.com", password);
        var other = service.CreateUser("other@example.com", password);
        var membership = service.AddMembership(owner, tenantA);
        var selectedPermission = permission ?? IdentityPermissions.Read;
        if (withRole)
        {
            var role = service.CreateRole("tenant-admin", tenantA, false, [selectedPermission, IdentityPermissions.SuspendMembership, IdentityPermissions.ReactivateMembership, IdentityPermissions.RevokeMembership]);
            service.AssignRole(membership, role, approver);
        }

        var selectedScope = scope ?? OrganizationScope.ForTenant(tenantA);
        if (withScope)
        {
            service.AddScopeGrant(membership, selectedScope, approver);
        }

        return new Fixture(service, clock, tenantA, owner, other, approver, membership, password, new CorrelationId("test-correlation"), selectedScope);
    }

    private static SupportFixture CreateSupportFixture(TimeSpan? lifetime = null)
    {
        var baseFixture = CreateFixture(withRole: false, withScope: false);
        var supportUser = baseFixture.Service.CreateUser("support@example.com", baseFixture.Password);
        var supportSession = Authenticate(baseFixture, supportUser, "support@example.com");
        var supportCase = baseFixture.Service.AddSupportCase(baseFixture.TenantA);
        var supportGrant = baseFixture.Service.AddSupportGrant(
            supportUser,
            supportCase,
            baseFixture.Approver,
            baseFixture.TenantA,
            "incident diagnosis",
            OrganizationScope.ForTenant(baseFixture.TenantA),
            [IdentityPermissions.SupportRead],
            lifetime ?? TimeSpan.FromHours(1));
        return new SupportFixture(baseFixture.Service, baseFixture.Clock, baseFixture.TenantA, baseFixture.Owner, baseFixture.OtherUser, baseFixture.Approver, baseFixture.MembershipA, baseFixture.Password, baseFixture.Correlation, baseFixture.Scope, supportUser, supportSession, supportCase, supportGrant);
    }

    private static SessionHandle Authenticate(Fixture fixture) =>
        Authenticate(fixture, fixture.Owner, "owner@example.com");

    private static SessionHandle Authenticate(Fixture fixture, UserId userId, string email)
    {
        var result = fixture.Service.Authenticate(email, fixture.Password);
        Assert.True(result.Succeeded);
        return new SessionHandle(result.SessionId!.Value, userId, result.CookieValue!);
    }

    private static void PrepareSupport(SupportFixture fixture)
    {
        fixture.Service.CompleteMfa(fixture.SupportSession.Id);
        fixture.Service.MarkFreshAuthentication(fixture.SupportSession.Id, $"support:{fixture.SupportGrant.Value}:{IdentityPermissions.SupportRead.Value}");
    }

    private static SessionHandle AddTenantManager(Fixture fixture, string email)
    {
        var manager = fixture.Service.CreateUser(email, fixture.Password);
        var membership = fixture.Service.AddMembership(manager, fixture.TenantA);
        var role = fixture.Service.CreateRole($"manager-{email}", fixture.TenantA, false, [IdentityPermissions.Read, IdentityPermissions.SuspendMembership, IdentityPermissions.ReactivateMembership, IdentityPermissions.RevokeMembership]);
        fixture.Service.AssignRole(membership, role, fixture.Approver);
        fixture.Service.AddScopeGrant(membership, OrganizationScope.ForTenant(fixture.TenantA), fixture.Approver);
        return Authenticate(fixture, manager, email);
    }

    private static PlatformFixture CreatePlatformActor(Fixture fixture, PermissionCode permission, string operation)
    {
        var platformUser = fixture.Service.CreateUser("platform@example.com", fixture.Password);
        fixture.Service.GrantPlatformPermission(platformUser, permission);
        var session = Authenticate(fixture, platformUser, "platform@example.com");
        fixture.Service.CompleteMfa(session.Id);
        fixture.Service.MarkFreshAuthentication(session.Id, operation);
        var governance = new PlatformGovernanceContext(platformUser.Value, PlatformGovernancePurpose.SecurityEvidence, fixture.Correlation);
        return new PlatformFixture(platformUser, session, governance);
    }

    private sealed record Fixture(
        IdentityAuthorizationService Service,
        ManualTimeProvider Clock,
        TenantId TenantA,
        UserId Owner,
        UserId OtherUser,
        UserId Approver,
        MembershipId MembershipA,
        string Password,
        CorrelationId Correlation,
        OrganizationScope Scope);

    private sealed record SupportFixture(
        IdentityAuthorizationService Service,
        ManualTimeProvider Clock,
        TenantId TenantA,
        UserId Owner,
        UserId OtherUser,
        UserId Approver,
        MembershipId MembershipA,
        string Password,
        CorrelationId Correlation,
        OrganizationScope Scope,
        UserId SupportUser,
        SessionHandle SupportSession,
        SupportCaseId SupportCase,
        SupportGrantId SupportGrant);

    private sealed record PlatformFixture(UserId User, SessionHandle Session, PlatformGovernanceContext Governance);

    private sealed class ManualTimeProvider : TimeProvider
    {
        private DateTimeOffset current = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

        public override DateTimeOffset GetUtcNow() => current;

        internal void Advance(TimeSpan duration) => current = current.Add(duration);
    }
}
