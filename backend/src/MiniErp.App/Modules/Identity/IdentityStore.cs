using MiniErp.App.BuildingBlocks.Tenancy;

namespace MiniErp.App.Modules.Identity;

internal sealed class IdentityStore
{
    internal object SyncRoot { get; } = new();

    internal Dictionary<string, GlobalUser> UsersByEmail { get; } = new(StringComparer.Ordinal);

    internal Dictionary<UserId, GlobalUser> Users { get; } = [];

    internal Dictionary<SessionId, UserSession> Sessions { get; } = [];

    internal Dictionary<MembershipId, TenantMembership> Memberships { get; } = [];

    internal Dictionary<RoleId, IdentityRole> Roles { get; } = [];

    internal Dictionary<MembershipId, List<RoleAssignment>> RoleAssignments { get; } = [];

    internal Dictionary<ScopeGrantId, AccessScopeGrant> ScopeGrants { get; } = [];

    internal Dictionary<MembershipId, List<ScopeGrantId>> ScopeGrantsByMembership { get; } = [];

    internal Dictionary<SupportCaseId, SupportCase> SupportCases { get; } = [];

    internal Dictionary<SupportGrantId, SupportGrant> SupportGrants { get; } = [];

    internal Dictionary<string, Invitation> InvitationsByTokenHash { get; } = new(StringComparer.Ordinal);

    internal Dictionary<string, RecoveryArtifact> RecoveriesByTokenHash { get; } = new(StringComparer.Ordinal);

    internal Dictionary<(TenantId TenantId, ScopeKind Kind, Guid TargetId), OrganizationScope?> ParentScopes { get; } = [];

    internal Dictionary<UserId, HashSet<PermissionCode>> PlatformPermissions { get; } = [];

    internal Dictionary<string, string> LifecycleIdempotency { get; } = new(StringComparer.Ordinal);

    internal List<SafeSecurityEvidence> Evidence { get; } = [];

    internal HashSet<PermissionCode> ApprovedPermissions { get; } =
    [
        IdentityPermissions.Read,
        IdentityPermissions.Export,
        IdentityPermissions.SuspendGlobalUser,
        IdentityPermissions.ReactivateGlobalUser,
        IdentityPermissions.OffboardGlobalUser,
        IdentityPermissions.SuspendMembership,
        IdentityPermissions.ReactivateMembership,
        IdentityPermissions.RevokeMembership,
        IdentityPermissions.SupportRead
    ];
}
