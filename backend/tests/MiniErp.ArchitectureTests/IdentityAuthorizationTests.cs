using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
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
        SeedScopeGrant(fixture, fixture.MembershipA, fixture.Scope, fixture.Approver);

        var decision = fixture.Service.AuthorizeOrdinary(Authenticate(fixture).CookieValue, fixture.TenantA, IdentityPermissions.Read, OrganizationScope.ForBranch(fixture.TenantA, branchB), fixture.Correlation);

        Assert.False(decision.Allowed);
    }

    [Fact]
    public void DownwardInheritanceAllowsBranchGrantToDescendantWarehouseOnly()
    {
        var fixture = CreateFixture(withScope: false);
        var branch = OrganizationScope.ForBranch(fixture.TenantA, Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));
        var warehouse = OrganizationScope.ForWarehouse(fixture.TenantA, Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));
        SeedScopeGrant(fixture, fixture.MembershipA, branch, fixture.Approver);
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
        PrepareSupport(fixture);

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
        var source = fixture.Source;
        Assert.True(fixture.Service.AcceptFreshAuthenticationEvidence(fixture.SupportSession.CookieValue, operation, source.IssueFresh(fixture.SupportUser, fixture.SupportSession.Id, operation, fixture.Clock.GetUtcNow())));

        Assert.False(fixture.Service.AuthorizeSupport(fixture.SupportSession.CookieValue, fixture.TenantA, fixture.SupportGrant, IdentityPermissions.SupportRead, OrganizationScope.ForTenant(fixture.TenantA), "incident diagnosis", fixture.Correlation).Allowed);
    }

    [Fact]
    public void SupportGrantRequiresFreshAuthentication()
    {
        var fixture = CreateSupportFixture();
        Assert.True(fixture.Service.AcceptMfaEvidence(fixture.SupportSession.CookieValue, fixture.Source.IssueMfa(fixture.SupportUser, fixture.SupportSession.Id, fixture.Clock.GetUtcNow())));

        Assert.False(fixture.Service.AuthorizeSupport(fixture.SupportSession.CookieValue, fixture.TenantA, fixture.SupportGrant, IdentityPermissions.SupportRead, OrganizationScope.ForTenant(fixture.TenantA), "incident diagnosis", fixture.Correlation).Allowed);
    }

    [Fact]
    public void OrdinaryMembershipAndSupportGrantTogetherAreRejected()
    {
        var fixture = CreateSupportFixture();
        var membership = fixture.Service.AddMembership(fixture.SupportUser, fixture.TenantA);
        var role = fixture.Service.CreateRole("support-ordinary", fixture.TenantA, false, [IdentityPermissions.SupportRead]);
        SeedRoleAssignment(fixture, membership, role, fixture.Approver);
        SeedScopeGrant(fixture, membership, OrganizationScope.ForTenant(fixture.TenantA), fixture.Approver);
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

        var result = fixture.Service.AddSupportGrant(Authenticate(fixture).CookieValue, fixture.TenantA, fixture.SupportUser, fixture.SupportCase, "export", OrganizationScope.ForTenant(fixture.TenantA), [IdentityPermissions.Export], TimeSpan.FromHours(1), "approved", fixture.Service.Store.SupportCases[fixture.SupportCase].Version, "export-support-1", fixture.Correlation);
        Assert.False(result.Succeeded);
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
        SeedPlatformPermission(fixture, platformUser, IdentityPermissions.SuspendGlobalUser, fixture.Approver);
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
        SeedPlatformPermission(fixture, platform.User, IdentityPermissions.ReactivateGlobalUser, fixture.Approver);
        Assert.True(fixture.Service.AcceptFreshAuthenticationEvidence(platform.Session.CookieValue, "platform.user.reactivate", fixture.Source.IssueFresh(platform.User, platform.Session.Id, "platform.user.reactivate", fixture.Clock.GetUtcNow())));

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
        SeedRoleAssignment(fixture, membershipB, roleB, fixture.Approver);
        SeedScopeGrant(fixture, membershipB, OrganizationScope.ForTenant(tenantB), fixture.Approver);
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
        fixture.Service.Store.Memberships[fixture.MembershipA].Status = MembershipStatus.PendingInvitation;
        var invitation = fixture.Service.IssueInvitation(fixture.Owner, fixture.TenantA, fixture.MembershipA, "owner@example.com");

        Assert.True(fixture.Service.AcceptInvitation(invitation.TokenValue, fixture.Owner, fixture.TenantA));
        Assert.False(fixture.Service.AcceptInvitation(invitation.TokenValue, fixture.Owner, fixture.TenantA));
    }

    [Fact]
    public void ExpiredInvitationDenies()
    {
        var fixture = CreateFixture();
        fixture.Service.Store.Memberships[fixture.MembershipA].Status = MembershipStatus.PendingInvitation;
        var invitation = fixture.Service.IssueInvitation(fixture.Owner, fixture.TenantA, fixture.MembershipA, "owner@example.com");
        fixture.Clock.Advance(TimeSpan.FromDays(7));

        Assert.False(fixture.Service.AcceptInvitation(invitation.TokenValue, fixture.Owner, fixture.TenantA));
    }

    [Fact]
    public void InvitationCannotCrossTenants()
    {
        var fixture = CreateFixture();
        fixture.Service.Store.Memberships[fixture.MembershipA].Status = MembershipStatus.PendingInvitation;
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

        var result = fixture.Service.AssignRole(Authenticate(fixture).CookieValue, fixture.TenantA, fixture.MembershipA, role, OrganizationScope.ForTenant(fixture.TenantA), "self", fixture.Service.Store.Memberships[fixture.MembershipA].Version, "self-role-1", fixture.Correlation);
        Assert.False(result.Succeeded);
    }

    [Fact]
    public void ScopeGrantRejectsSelfApproval()
    {
        var fixture = CreateFixture(withScope: false);

        var result = fixture.Service.AddScopeGrant(Authenticate(fixture).CookieValue, fixture.TenantA, fixture.MembershipA, OrganizationScope.ForTenant(fixture.TenantA), "self", fixture.Service.Store.Memberships[fixture.MembershipA].Version, "self-scope-1", fixture.Correlation);
        Assert.False(result.Succeeded);
    }

    [Fact]
    public void TenantOwnedRoleCannotCrossTenantBoundary()
    {
        var fixture = CreateFixture(withRole: false);
        var tenantB = new TenantId(Guid.Parse("22222222-2222-2222-2222-222222222222"));
        var roleB = fixture.Service.CreateRole("tenant-b-role", tenantB, false, [IdentityPermissions.Read]);

        var result = fixture.Service.AssignRole(Authenticate(fixture).CookieValue, fixture.TenantA, fixture.MembershipA, roleB, OrganizationScope.ForTenant(fixture.TenantA), "cross tenant", fixture.Service.Store.Memberships[fixture.MembershipA].Version, "cross-role-1", fixture.Correlation);
        Assert.False(result.Succeeded);
    }

    [Fact]
    public void BranchScopeDoesNotAuthorizeSidewaysWarehouse()
    {
        var fixture = CreateFixture(withScope: false);
        var branchA = OrganizationScope.ForBranch(fixture.TenantA, Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));
        var warehouseB = OrganizationScope.ForWarehouse(fixture.TenantA, Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"));
        SeedScopeGrant(fixture, fixture.MembershipA, branchA, fixture.Approver);

        Assert.False(fixture.Service.AuthorizeOrdinary(Authenticate(fixture).CookieValue, fixture.TenantA, IdentityPermissions.Read, warehouseB, fixture.Correlation).Allowed);
    }

    [Fact]
    public void GovernanceContextCannotBeUsedAsTenantBusinessAuthorization()
    {
        var fixture = CreateFixture();
        var platformUser = fixture.Service.CreateUser("governance-only@example.com", fixture.Password);
        SeedPlatformPermission(fixture, platformUser, IdentityPermissions.SuspendGlobalUser, fixture.Approver);
        var session = Authenticate(fixture, platformUser, "governance-only@example.com");
        var governance = new PlatformGovernanceContext(platformUser.Value, PlatformGovernancePurpose.SecurityEvidence, fixture.Correlation);

        var result = fixture.Service.SuspendMembership(session.CookieValue, fixture.TenantA, fixture.MembershipA, "governance cannot tenant", fixture.Service.Store.Memberships[fixture.MembershipA].Version, "governance-tenant-1", fixture.Correlation);

        Assert.False(result.Succeeded);
        Assert.Equal(PlatformGovernancePurpose.SecurityEvidence, governance.Purpose);
    }

    [Fact]
    public void RuntimeServiceHasNoUnauthenticatedAuthorityIssuanceMethods()
    {
        var methods = typeof(IdentityAuthorizationService).GetMethods(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);

        Assert.DoesNotContain(methods, method => method.Name == "GrantPlatformPermission");
        Assert.DoesNotContain(methods, method => method.Name == "CompleteMfa");
        Assert.DoesNotContain(methods, method => method.Name == "MarkFreshAuthentication");
        Assert.Contains(methods, method => method.Name == "AssignPlatformPermission");
        Assert.Contains(methods, method => method.Name == "AssignRole");
        Assert.Contains(methods, method => method.Name == "AddScopeGrant");
        Assert.Contains(methods, method => method.Name == "AddSupportGrant");
    }

    [Fact]
    public void ProductionIdentityRegistrationUsesUnavailableAssuranceSource()
    {
        var services = new ServiceCollection();
        services.AddIdentityAuthorization();
        using var provider = services.BuildServiceProvider();

        Assert.IsType<UnavailableAuthenticationAssuranceEvidenceSource>(provider.GetRequiredService<IAuthenticationAssuranceEvidenceSource>());
        Assert.DoesNotContain(provider.GetServices<IAuthenticationAssuranceEvidenceSource>(), source => source.GetType().Name.Contains("Test", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void UnauthenticatedCallerCannotAssignPlatformPermission()
    {
        var fixture = CreateFixture();
        var platform = CreatePlatformActor(fixture, IdentityPermissions.AssignPlatformPermission, "platform.authority.assign");
        var result = fixture.Service.AssignPlatformPermission(platform.Governance, "invalid-cookie", fixture.OtherUser, IdentityPermissions.Read, "approved", fixture.Service.GetUserVersion(fixture.OtherUser), "platform-unauth-1");

        Assert.False(result.Succeeded);
        Assert.False(fixture.Service.Store.PlatformPermissions.TryGetValue(fixture.OtherUser, out var assignments) && assignments.Any(assignment => assignment.IsActive));
    }

    [Fact]
    public void OrdinaryTenantAdministratorCannotAssignPlatformPermission()
    {
        var fixture = CreateFixture();
        var session = Authenticate(fixture);
        var governance = new PlatformGovernanceContext(fixture.Owner.Value, PlatformGovernancePurpose.SecurityEvidence, fixture.Correlation);
        var result = fixture.Service.AssignPlatformPermission(governance, session.CookieValue, fixture.OtherUser, IdentityPermissions.Read, "ordinary cannot platform", fixture.Service.GetUserVersion(fixture.OtherUser), "platform-ordinary-1");

        Assert.False(result.Succeeded);
    }

    [Fact]
    public void AuthorizedPlatformAssignmentIsIdempotentAndDoesNotDuplicate()
    {
        var fixture = CreateFixture();
        var platform = CreatePlatformActor(fixture, IdentityPermissions.AssignPlatformPermission, "platform.authority.assign");
        var expected = fixture.Service.GetUserVersion(fixture.OtherUser);
        var first = fixture.Service.AssignPlatformPermission(platform.Governance, platform.Session.CookieValue, fixture.OtherUser, IdentityPermissions.Read, "approved", expected, "platform-idempotent-1");
        ReassertPlatformEvidence(fixture, platform, "platform.authority.assign");
        var second = fixture.Service.AssignPlatformPermission(platform.Governance, platform.Session.CookieValue, fixture.OtherUser, IdentityPermissions.Read, "retry", expected, "platform-idempotent-1");

        Assert.True(first.Succeeded);
        Assert.True(second.Succeeded);
        Assert.Single(fixture.Service.Store.PlatformPermissions[fixture.OtherUser], assignment => assignment.IsActive && assignment.Permission == IdentityPermissions.Read);
    }

    [Fact]
    public void RevokedPlatformPermissionCannotBeResurrected()
    {
        var fixture = CreateFixture();
        var platform = CreatePlatformActor(fixture, IdentityPermissions.AssignPlatformPermission, "platform.authority.assign");
        SeedPlatformPermission(fixture, fixture.OtherUser, IdentityPermissions.Read, fixture.Approver);
        fixture.Service.Store.PlatformPermissions[fixture.OtherUser][0].IsActive = false;
        var result = fixture.Service.AssignPlatformPermission(platform.Governance, platform.Session.CookieValue, fixture.OtherUser, IdentityPermissions.Read, "restore", fixture.Service.GetUserVersion(fixture.OtherUser), "platform-resurrect-1");

        Assert.False(result.Succeeded);
        Assert.DoesNotContain(fixture.Service.Store.PlatformPermissions[fixture.OtherUser], assignment => assignment.IsActive && assignment.Permission == IdentityPermissions.Read);
    }

    [Fact]
    public void AuthorizedRoleAssignmentUsesAuthenticatedActorAndExactTenant()
    {
        var fixture = CreateFixture();
        var targetMembership = fixture.Service.AddMembership(fixture.OtherUser, fixture.TenantA);
        var role = fixture.Service.CreateRole("target-reader", fixture.TenantA, false, [IdentityPermissions.Read]);
        var result = fixture.Service.AssignRole(Authenticate(fixture).CookieValue, fixture.TenantA, targetMembership, role, OrganizationScope.ForTenant(fixture.TenantA), "approved role", fixture.Service.Store.Memberships[targetMembership].Version, "role-authorized-1", fixture.Correlation);

        Assert.True(result.Succeeded);
        Assert.Equal(fixture.Owner, fixture.Service.Store.RoleAssignments[targetMembership].Single().ApproverId);
    }

    [Fact]
    public void BranchAdministratorCannotGrantCompanyScope()
    {
        var branch = OrganizationScope.ForBranch(new TenantId(Guid.Parse("11111111-1111-1111-1111-111111111111")), Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));
        var fixture = CreateFixture(scope: branch);
        var targetMembership = fixture.Service.AddMembership(fixture.OtherUser, fixture.TenantA);
        var result = fixture.Service.AddScopeGrant(Authenticate(fixture).CookieValue, fixture.TenantA, targetMembership, OrganizationScope.ForCompany(fixture.TenantA, Guid.Parse("cccccccc-cccc-cccc-cccc-cccccccccccc")), "upward scope", fixture.Service.Store.Memberships[targetMembership].Version, "scope-upward-1", fixture.Correlation);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public void AuthorizedContainedScopeGrantSucceeds()
    {
        var fixture = CreateFixture();
        var targetMembership = fixture.Service.AddMembership(fixture.OtherUser, fixture.TenantA);
        var branch = OrganizationScope.ForBranch(fixture.TenantA, Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa"));
        var result = fixture.Service.AddScopeGrant(Authenticate(fixture).CookieValue, fixture.TenantA, targetMembership, branch, "contained scope", fixture.Service.Store.Memberships[targetMembership].Version, "scope-authorized-1", fixture.Correlation);

        Assert.True(result.Succeeded);
    }

    [Fact]
    public void AuthorizedExactTenantSupportApprovalSucceeds()
    {
        var fixture = CreateFixture();
        var supportUser = fixture.Service.CreateUser("approved-support@example.com", fixture.Password);
        var supportCase = fixture.Service.AddSupportCase(fixture.TenantA);
        var result = fixture.Service.AddSupportGrant(Authenticate(fixture).CookieValue, fixture.TenantA, supportUser, supportCase, "incident diagnosis", OrganizationScope.ForTenant(fixture.TenantA), [IdentityPermissions.SupportRead], TimeSpan.FromHours(1), "approved support", fixture.Service.Store.SupportCases[supportCase].Version, "support-authorized-1", fixture.Correlation);

        Assert.True(result.Succeeded);
        Assert.NotNull(result.ResourceId);
        Assert.Equal(fixture.Owner, fixture.Service.Store.SupportGrants[new SupportGrantId(result.ResourceId!.Value)].TenantApproverId);
    }

    [Fact]
    public void WrongTenantSupportCaseCannotBeApproved()
    {
        var fixture = CreateFixture();
        var tenantB = new TenantId(Guid.Parse("22222222-2222-2222-2222-222222222222"));
        var supportUser = fixture.Service.CreateUser("wrong-tenant-support@example.com", fixture.Password);
        var supportCase = fixture.Service.AddSupportCase(tenantB);
        var result = fixture.Service.AddSupportGrant(Authenticate(fixture).CookieValue, fixture.TenantA, supportUser, supportCase, "wrong tenant", OrganizationScope.ForTenant(fixture.TenantA), [IdentityPermissions.SupportRead], TimeSpan.FromHours(1), "wrong tenant", fixture.Service.Store.SupportCases[supportCase].Version, "support-cross-tenant-1", fixture.Correlation);

        Assert.False(result.Succeeded);
    }

    [Fact]
    public void AssuranceEvidenceIsBoundToUserSessionOperationExpiryAndReplay()
    {
        var fixture = CreateFixture();
        var ownerSession = Authenticate(fixture);
        var other = fixture.Service.CreateUser("evidence-other@example.com", fixture.Password);
        var wrongUserToken = fixture.Source.IssueMfa(other, ownerSession.Id, fixture.Clock.GetUtcNow());
        Assert.False(fixture.Service.AcceptMfaEvidence(ownerSession.CookieValue, wrongUserToken));

        var secondSession = Authenticate(fixture);
        var wrongSessionToken = fixture.Source.IssueMfa(fixture.Owner, secondSession.Id, fixture.Clock.GetUtcNow());
        Assert.False(fixture.Service.AcceptMfaEvidence(ownerSession.CookieValue, wrongSessionToken));

        var operation = "platform.authority.assign";
        var wrongOperationToken = fixture.Source.IssueFresh(fixture.Owner, ownerSession.Id, "other.operation", fixture.Clock.GetUtcNow());
        Assert.False(fixture.Service.AcceptFreshAuthenticationEvidence(ownerSession.CookieValue, operation, wrongOperationToken));

        var expired = fixture.Source.IssueMfa(fixture.Owner, ownerSession.Id, fixture.Clock.GetUtcNow());
        fixture.Clock.Advance(TimeSpan.FromMinutes(6));
        Assert.False(fixture.Service.AcceptMfaEvidence(ownerSession.CookieValue, expired));

        var valid = fixture.Source.IssueMfa(fixture.Owner, ownerSession.Id, fixture.Clock.GetUtcNow());
        Assert.True(fixture.Service.AcceptMfaEvidence(ownerSession.CookieValue, valid));
        Assert.False(fixture.Service.AcceptMfaEvidence(ownerSession.CookieValue, valid));
    }

    [Fact]
    public void ActiveSuspendedAndRevokedMembershipsCannotIssueInvitations()
    {
        var fixture = CreateFixture();
        Assert.Throws<InvalidOperationException>(() => fixture.Service.IssueInvitation(fixture.Owner, fixture.TenantA, fixture.MembershipA, "owner@example.com"));
        fixture.Service.Store.Memberships[fixture.MembershipA].Status = MembershipStatus.Suspended;
        Assert.Throws<InvalidOperationException>(() => fixture.Service.IssueInvitation(fixture.Owner, fixture.TenantA, fixture.MembershipA, "owner@example.com"));
        fixture.Service.Store.Memberships[fixture.MembershipA].Status = MembershipStatus.Revoked;
        Assert.Throws<InvalidOperationException>(() => fixture.Service.IssueInvitation(fixture.Owner, fixture.TenantA, fixture.MembershipA, "owner@example.com"));
    }

    [Fact]
    public void MembershipAndUserVersionChangesInvalidateInvitation()
    {
        var fixture = CreateFixture();
        fixture.Service.Store.Memberships[fixture.MembershipA].Status = MembershipStatus.PendingInvitation;
        var invitation = fixture.Service.IssueInvitation(fixture.Owner, fixture.TenantA, fixture.MembershipA, "owner@example.com");
        fixture.Service.Store.Memberships[fixture.MembershipA].Version++;
        Assert.False(fixture.Service.AcceptInvitation(invitation.TokenValue, fixture.Owner, fixture.TenantA));

        fixture.Service.Store.Memberships[fixture.MembershipA].Status = MembershipStatus.PendingInvitation;
        fixture.Service.Store.Memberships[fixture.MembershipA].Version++;
        var second = fixture.Service.IssueInvitation(fixture.Owner, fixture.TenantA, fixture.MembershipA, "owner@example.com");
        fixture.Service.Store.Users[fixture.Owner].Version++;
        Assert.False(fixture.Service.AcceptInvitation(second.TokenValue, fixture.Owner, fixture.TenantA));
    }

    [Fact]
    public void InvitationAcceptanceDoesNotRestoreRevokedAuthority()
    {
        var fixture = CreateFixture();
        foreach (var assignment in fixture.Service.Store.RoleAssignments[fixture.MembershipA])
        {
            assignment.IsActive = false;
        }

        foreach (var id in fixture.Service.Store.ScopeGrantsByMembership[fixture.MembershipA])
        {
            fixture.Service.Store.ScopeGrants[id].IsActive = false;
        }

        fixture.Service.Store.Memberships[fixture.MembershipA].Status = MembershipStatus.PendingInvitation;
        var invitation = fixture.Service.IssueInvitation(fixture.Owner, fixture.TenantA, fixture.MembershipA, "owner@example.com");
        Assert.True(fixture.Service.AcceptInvitation(invitation.TokenValue, fixture.Owner, fixture.TenantA));
        Assert.Equal(0, fixture.Service.CountRoleAssignments(fixture.MembershipA));
        Assert.Equal(0, fixture.Service.CountActiveScopeGrants(fixture.MembershipA));
    }

    [Fact]
    public void IneligibleSupportGrantDoesNotBlockOrdinaryAuthorization()
    {
        var fixture = CreateSupportFixture();
        var membership = fixture.Service.AddMembership(fixture.SupportUser, fixture.TenantA);
        var role = fixture.Service.CreateRole("support-ordinary-ineligible", fixture.TenantA, false, [IdentityPermissions.SupportRead]);
        SeedRoleAssignment(fixture, membership, role, fixture.Approver);
        SeedScopeGrant(fixture, membership, OrganizationScope.ForTenant(fixture.TenantA), fixture.Approver);

        var ordinary = fixture.Service.AuthorizeOrdinary(fixture.SupportSession.CookieValue, fixture.TenantA, IdentityPermissions.SupportRead, OrganizationScope.ForTenant(fixture.TenantA), fixture.Correlation);

        Assert.True(ordinary.Allowed);
    }

    [Fact]
    public void OrdinaryAuthorizationRenewsInactivityWithoutExtendingAbsoluteExpiry()
    {
        var fixture = CreateFixture();
        var session = Authenticate(fixture);
        var absoluteExpiry = fixture.Service.Store.Sessions[session.Id].AbsoluteExpiresAt;
        fixture.Clock.Advance(TimeSpan.FromMinutes(29));
        Assert.True(fixture.Service.AuthorizeOrdinary(session.CookieValue, fixture.TenantA, IdentityPermissions.Read, OrganizationScope.ForTenant(fixture.TenantA), fixture.Correlation).Allowed);
        fixture.Clock.Advance(TimeSpan.FromMinutes(29));
        Assert.True(fixture.Service.ValidateSession(session.CookieValue).Valid);
        Assert.Equal(absoluteExpiry, fixture.Service.Store.Sessions[session.Id].AbsoluteExpiresAt);
        fixture.Clock.Advance(TimeSpan.FromHours(7));
        Assert.False(fixture.Service.ValidateSession(session.CookieValue).Valid);
    }

    [Fact]
    public void InvalidOperationCannotReviveAnIdleSession()
    {
        var fixture = CreateFixture();
        var session = Authenticate(fixture);
        fixture.Clock.Advance(TimeSpan.FromMinutes(30));
        Assert.False(fixture.Service.AuthorizeOrdinary(session.CookieValue, fixture.TenantA, IdentityPermissions.Read, OrganizationScope.ForTenant(fixture.TenantA), fixture.Correlation).Allowed);
        Assert.False(fixture.Service.ValidateSession(session.CookieValue).Valid);
    }

    [Fact]
    public void SupportAuthorizationRenewsInactivityAfterTrustedEvidence()
    {
        var fixture = CreateSupportFixture();
        PrepareSupport(fixture);
        fixture.Clock.Advance(TimeSpan.FromMinutes(29));
        PrepareSupport(fixture);
        Assert.True(fixture.Service.AuthorizeSupport(fixture.SupportSession.CookieValue, fixture.TenantA, fixture.SupportGrant, IdentityPermissions.SupportRead, OrganizationScope.ForTenant(fixture.TenantA), "incident diagnosis", fixture.Correlation).Allowed);
        fixture.Clock.Advance(TimeSpan.FromMinutes(29));

        Assert.True(fixture.Service.ValidateSession(fixture.SupportSession.CookieValue).Valid);
    }

    [Fact]
    public void PlatformLifecycleAuthorizationRenewsInactivity()
    {
        var fixture = CreateFixture();
        var platform = CreatePlatformActor(fixture, IdentityPermissions.SuspendGlobalUser, "platform.user.suspend");
        fixture.Clock.Advance(TimeSpan.FromMinutes(29));
        ReassertPlatformEvidence(fixture, platform, "platform.user.suspend");
        var result = fixture.Service.SuspendUser(platform.Governance, platform.Session.CookieValue, fixture.OtherUser, "approved lifecycle", fixture.Service.GetUserVersion(fixture.OtherUser), "platform-renew-1");
        fixture.Clock.Advance(TimeSpan.FromMinutes(29));

        Assert.True(result.Succeeded);
        Assert.True(fixture.Service.ValidateSession(platform.Session.CookieValue).Valid);
    }

    [Fact]
    public void TenantLifecycleAuthorizationRenewsInactivity()
    {
        var fixture = CreateFixture();
        var manager = AddTenantManager(fixture, "manager-renew@example.com");
        fixture.Clock.Advance(TimeSpan.FromMinutes(29));
        var result = fixture.Service.SuspendMembership(manager.CookieValue, fixture.TenantA, fixture.MembershipA, "approved lifecycle", fixture.Service.Store.Memberships[fixture.MembershipA].Version, "tenant-renew-1", fixture.Correlation);
        fixture.Clock.Advance(TimeSpan.FromMinutes(29));

        Assert.True(result.Succeeded);
        Assert.True(fixture.Service.ValidateSession(manager.CookieValue).Valid);
    }

    private static Fixture CreateFixture(
        bool withRole = true,
        bool withScope = true,
        PermissionCode? permission = null,
        OrganizationScope? scope = null)
    {
        var clock = new ManualTimeProvider();
        var source = new TestAuthenticationAssuranceEvidenceSource();
        var service = new IdentityAuthorizationService(timeProvider: clock, assuranceEvidenceSource: source);
        var password = "Correct-horse-battery-1!";
        var tenantA = new TenantId(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        var owner = service.CreateUser("owner@example.com", password);
        var approver = service.CreateUser("approver@example.com", password);
        var other = service.CreateUser("other@example.com", password);
        var membership = service.AddMembership(owner, tenantA);
        var selectedPermission = permission ?? IdentityPermissions.Read;
        if (withRole)
        {
            var role = service.CreateRole("tenant-admin", tenantA, false, [selectedPermission, IdentityPermissions.SuspendMembership, IdentityPermissions.ReactivateMembership, IdentityPermissions.RevokeMembership, IdentityPermissions.AssignRole, IdentityPermissions.AssignScopeGrant, IdentityPermissions.ApproveSupportGrant]);
            SeedRoleAssignment(service, membership, role, approver);
        }

        var selectedScope = scope ?? OrganizationScope.ForTenant(tenantA);
        if (withScope)
        {
            SeedScopeGrant(service, membership, selectedScope, approver);
        }

        return new Fixture(service, clock, source, tenantA, owner, other, approver, membership, password, new CorrelationId("test-correlation"), selectedScope);
    }

    private static SupportFixture CreateSupportFixture(TimeSpan? lifetime = null)
    {
        var baseFixture = CreateFixture(withRole: false, withScope: false);
        var supportUser = baseFixture.Service.CreateUser("support@example.com", baseFixture.Password);
        var supportSession = Authenticate(baseFixture, supportUser, "support@example.com");
        var supportCase = baseFixture.Service.AddSupportCase(baseFixture.TenantA);
        var supportGrant = new SupportGrant(new SupportGrantId(Guid.NewGuid()), supportCase, supportUser, baseFixture.Approver, baseFixture.TenantA, "incident diagnosis", OrganizationScope.ForTenant(baseFixture.TenantA), [IdentityPermissions.SupportRead], baseFixture.Clock.GetUtcNow().Add(lifetime ?? TimeSpan.FromHours(1)));
        baseFixture.Service.Store.SupportGrants.Add(supportGrant.Id, supportGrant);
        return new SupportFixture(baseFixture.Service, baseFixture.Clock, baseFixture.Source, baseFixture.TenantA, baseFixture.Owner, baseFixture.OtherUser, baseFixture.Approver, baseFixture.MembershipA, baseFixture.Password, baseFixture.Correlation, baseFixture.Scope, supportUser, supportSession, supportCase, supportGrant.Id);
    }

    private static SessionHandle Authenticate(Fixture fixture) =>
        Authenticate(fixture, fixture.Owner, "owner@example.com");

    private static SessionHandle Authenticate(SupportFixture fixture)
    {
        var result = fixture.Service.Authenticate("owner@example.com", fixture.Password);
        Assert.True(result.Succeeded);
        return new SessionHandle(result.SessionId!.Value, fixture.Owner, result.CookieValue!);
    }

    private static SessionHandle Authenticate(Fixture fixture, UserId userId, string email)
    {
        var result = fixture.Service.Authenticate(email, fixture.Password);
        Assert.True(result.Succeeded);
        return new SessionHandle(result.SessionId!.Value, userId, result.CookieValue!);
    }

    private static void PrepareSupport(SupportFixture fixture)
    {
        Assert.True(fixture.Service.AcceptMfaEvidence(fixture.SupportSession.CookieValue, fixture.Source.IssueMfa(fixture.SupportUser, fixture.SupportSession.Id, fixture.Clock.GetUtcNow())));
        var operation = $"support:{fixture.SupportGrant.Value}:{IdentityPermissions.SupportRead.Value}";
        Assert.True(fixture.Service.AcceptFreshAuthenticationEvidence(fixture.SupportSession.CookieValue, operation, fixture.Source.IssueFresh(fixture.SupportUser, fixture.SupportSession.Id, operation, fixture.Clock.GetUtcNow())));
    }

    private static SessionHandle AddTenantManager(Fixture fixture, string email)
    {
        var manager = fixture.Service.CreateUser(email, fixture.Password);
        var membership = fixture.Service.AddMembership(manager, fixture.TenantA);
        var role = fixture.Service.CreateRole($"manager-{email}", fixture.TenantA, false, [IdentityPermissions.Read, IdentityPermissions.SuspendMembership, IdentityPermissions.ReactivateMembership, IdentityPermissions.RevokeMembership]);
        SeedRoleAssignment(fixture, membership, role, fixture.Approver);
        SeedScopeGrant(fixture, membership, OrganizationScope.ForTenant(fixture.TenantA), fixture.Approver);
        return Authenticate(fixture, manager, email);
    }

    private static PlatformFixture CreatePlatformActor(Fixture fixture, PermissionCode permission, string operation)
    {
        var platformUser = fixture.Service.CreateUser("platform@example.com", fixture.Password);
        SeedPlatformPermission(fixture, platformUser, permission, fixture.Approver);
        var session = Authenticate(fixture, platformUser, "platform@example.com");
        Assert.True(fixture.Service.AcceptMfaEvidence(session.CookieValue, fixture.Source.IssueMfa(platformUser, session.Id, fixture.Clock.GetUtcNow())));
        Assert.True(fixture.Service.AcceptFreshAuthenticationEvidence(session.CookieValue, operation, fixture.Source.IssueFresh(platformUser, session.Id, operation, fixture.Clock.GetUtcNow())));
        var governance = new PlatformGovernanceContext(platformUser.Value, PlatformGovernancePurpose.SecurityEvidence, fixture.Correlation);
        return new PlatformFixture(platformUser, session, governance);
    }

    private static void ReassertPlatformEvidence(Fixture fixture, PlatformFixture platform, string operation)
    {
        Assert.True(fixture.Service.AcceptMfaEvidence(platform.Session.CookieValue, fixture.Source.IssueMfa(platform.User, platform.Session.Id, fixture.Clock.GetUtcNow())));
        Assert.True(fixture.Service.AcceptFreshAuthenticationEvidence(platform.Session.CookieValue, operation, fixture.Source.IssueFresh(platform.User, platform.Session.Id, operation, fixture.Clock.GetUtcNow())));
    }

    private sealed record Fixture(
        IdentityAuthorizationService Service,
        ManualTimeProvider Clock,
        TestAuthenticationAssuranceEvidenceSource Source,
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
        TestAuthenticationAssuranceEvidenceSource Source,
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

    private static void SeedRoleAssignment(Fixture fixture, MembershipId membershipId, RoleId roleId, UserId approverId) =>
        SeedRoleAssignment(fixture.Service, membershipId, roleId, approverId);

    private static void SeedRoleAssignment(SupportFixture fixture, MembershipId membershipId, RoleId roleId, UserId approverId) =>
        SeedRoleAssignment(fixture.Service, membershipId, roleId, approverId);

    private static void SeedRoleAssignment(IdentityAuthorizationService service, MembershipId membershipId, RoleId roleId, UserId approverId)
    {
        var membership = service.Store.Memberships[membershipId];
        service.Store.RoleAssignments[membershipId].Add(new RoleAssignment(roleId, membershipId, membership.UserId, membership.TenantId, approverId));
    }

    private static void SeedScopeGrant(Fixture fixture, MembershipId membershipId, OrganizationScope scope, UserId approverId) =>
        SeedScopeGrant(fixture.Service, membershipId, scope, approverId);

    private static void SeedScopeGrant(SupportFixture fixture, MembershipId membershipId, OrganizationScope scope, UserId approverId) =>
        SeedScopeGrant(fixture.Service, membershipId, scope, approverId);

    private static void SeedScopeGrant(IdentityAuthorizationService service, MembershipId membershipId, OrganizationScope scope, UserId approverId)
    {
        var membership = service.Store.Memberships[membershipId];
        var id = new ScopeGrantId(Guid.NewGuid());
        service.Store.ScopeGrants.Add(id, new AccessScopeGrant(id, membershipId, membership.UserId, scope, approverId));
        service.Store.ScopeGrantsByMembership[membershipId].Add(id);
    }

    private static void SeedPlatformPermission(Fixture fixture, UserId userId, PermissionCode permission, UserId approverId)
    {
        if (!fixture.Service.Store.PlatformPermissions.TryGetValue(userId, out var assignments))
        {
            assignments = [];
            fixture.Service.Store.PlatformPermissions.Add(userId, assignments);
        }

        assignments.Add(new PlatformPermissionAssignment(userId, permission, approverId, "test seed"));
    }

    private sealed class TestAuthenticationAssuranceEvidenceSource : IAuthenticationAssuranceEvidenceSource
    {
        private readonly Dictionary<string, AuthenticationAssuranceEvidence> evidenceByToken = new(StringComparer.Ordinal);

        internal string IssueMfa(UserId userId, SessionId sessionId, DateTimeOffset now) =>
            Issue(new AuthenticationAssuranceEvidence(userId, sessionId, AuthenticationAssuranceType.Mfa, null, now, now.AddMinutes(5), SourceId, true));

        internal string IssueFresh(UserId userId, SessionId sessionId, string operation, DateTimeOffset now) =>
            Issue(new AuthenticationAssuranceEvidence(userId, sessionId, AuthenticationAssuranceType.FreshAuthentication, operation, now, now.AddMinutes(5), SourceId, true));

        public string SourceId => "test-assurance";

        public bool TryValidateMfaEvidence(string opaqueEvidence, UserId expectedUserId, SessionId expectedSessionId, DateTimeOffset now, out AuthenticationAssuranceEvidence evidence) =>
            TryTake(opaqueEvidence, expectedUserId, expectedSessionId, AuthenticationAssuranceType.Mfa, null, now, out evidence);

        public bool TryValidateFreshAuthenticationEvidence(string opaqueEvidence, UserId expectedUserId, SessionId expectedSessionId, string operation, DateTimeOffset now, out AuthenticationAssuranceEvidence evidence) =>
            TryTake(opaqueEvidence, expectedUserId, expectedSessionId, AuthenticationAssuranceType.FreshAuthentication, operation, now, out evidence);

        private string Issue(AuthenticationAssuranceEvidence evidence)
        {
            var token = $"test-evidence-{Guid.NewGuid():N}";
            evidenceByToken.Add(token, evidence);
            return token;
        }

        private bool TryTake(string token, UserId userId, SessionId sessionId, AuthenticationAssuranceType type, string? operation, DateTimeOffset now, out AuthenticationAssuranceEvidence evidence)
        {
            if (!evidenceByToken.TryGetValue(token, out evidence!)
                || evidence.UserId != userId
                || evidence.SessionId != sessionId
                || evidence.Type != type
                || !string.Equals(evidence.Operation, operation, StringComparison.Ordinal)
                || evidence.IssuedAt > now
                || evidence.ExpiresAt <= now)
            {
                evidence = null!;
                return false;
            }

            evidenceByToken.Remove(token);
            return true;
        }
    }
}
