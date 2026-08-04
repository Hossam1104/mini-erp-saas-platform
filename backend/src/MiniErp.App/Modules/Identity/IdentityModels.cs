using MiniErp.App.BuildingBlocks.Tenancy;

namespace MiniErp.App.Modules.Identity;

internal enum GlobalUserStatus
{
    Active = 1,
    Suspended = 2,
    Offboarded = 3
}

internal enum MembershipStatus
{
    Active = 1,
    Suspended = 2,
    Revoked = 3,
    PendingInvitation = 4
}

internal enum ScopeKind
{
    Tenant = 1,
    Company = 2,
    Branch = 3,
    Warehouse = 4
}

internal readonly record struct UserId
{
    internal UserId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("User identifier must not be empty.", nameof(value));
        }

        Value = value;
    }

    internal Guid Value { get; }
}

internal readonly record struct SessionId
{
    internal SessionId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Session identifier must not be empty.", nameof(value));
        }

        Value = value;
    }

    internal Guid Value { get; }
}

internal readonly record struct MembershipId
{
    internal MembershipId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Membership identifier must not be empty.", nameof(value));
        }

        Value = value;
    }

    internal Guid Value { get; }
}

internal readonly record struct RoleId
{
    internal RoleId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Role identifier must not be empty.", nameof(value));
        }

        Value = value;
    }

    internal Guid Value { get; }
}

internal readonly record struct ScopeGrantId
{
    internal ScopeGrantId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Scope grant identifier must not be empty.", nameof(value));
        }

        Value = value;
    }

    internal Guid Value { get; }
}

internal readonly record struct SupportCaseId
{
    internal SupportCaseId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Support case identifier must not be empty.", nameof(value));
        }

        Value = value;
    }

    internal Guid Value { get; }
}

internal readonly record struct SupportGrantId
{
    internal SupportGrantId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Support grant identifier must not be empty.", nameof(value));
        }

        Value = value;
    }

    internal Guid Value { get; }
}

internal readonly record struct InvitationId
{
    internal InvitationId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Invitation identifier must not be empty.", nameof(value));
        }

        Value = value;
    }

    internal Guid Value { get; }
}

internal readonly record struct RecoveryId
{
    internal RecoveryId(Guid value)
    {
        if (value == Guid.Empty)
        {
            throw new ArgumentException("Recovery identifier must not be empty.", nameof(value));
        }

        Value = value;
    }

    internal Guid Value { get; }
}

internal readonly record struct PermissionCode
{
    internal PermissionCode(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Permission code must not be empty.", nameof(value));
        }

        Value = value.Trim().ToLowerInvariant();
    }

    internal string Value { get; }
}

internal static class IdentityPermissions
{
    internal static readonly PermissionCode Read = new("tenant.business.read");
    internal static readonly PermissionCode Export = new("tenant.business.export");
    // Technical permissions are neutral, configuration-led permissions owned
    // by the Foundation operation catalogue rather than by a role name.
    internal static readonly PermissionCode ContextRead = new("tenant.foundation.context.read");
    internal static readonly PermissionCode TargetRead = new("tenant.foundation.target.read");
    internal static readonly PermissionCode ProbeWrite = new("tenant.foundation.probe.write");
    internal static readonly PermissionCode ContextSwitch = new("foundation.context.switch");
    internal static readonly PermissionCode PlatformMetadataRead = new("platform.metadata.read");
    internal static readonly PermissionCode SuspendGlobalUser = new("platform.user.suspend");
    internal static readonly PermissionCode ReactivateGlobalUser = new("platform.user.reactivate");
    internal static readonly PermissionCode OffboardGlobalUser = new("platform.user.offboard");
    internal static readonly PermissionCode SuspendMembership = new("tenant.membership.suspend");
    internal static readonly PermissionCode ReactivateMembership = new("tenant.membership.reactivate");
    internal static readonly PermissionCode RevokeMembership = new("tenant.membership.revoke");
    internal static readonly PermissionCode SupportRead = new("support.tenant.read");
    // Configuration-led Platform authority; this is not a customer-specific role permission.
    internal static readonly PermissionCode AssignPlatformPermission = new("platform.authority.assign");
    // Configuration-led Tenant authority permissions used by the approved seam.
    internal static readonly PermissionCode AssignRole = new("tenant.role.assign");
    internal static readonly PermissionCode AssignScopeGrant = new("tenant.scope.grant");
    internal static readonly PermissionCode ApproveSupportGrant = new("support.grant.approve");

    internal static bool TryResolve(string? value, out PermissionCode permission)
    {
        permission = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        var normalized = value.Trim().ToLowerInvariant();
        permission = normalized switch
        {
            "tenant.business.read" => Read,
            "tenant.business.export" => Export,
            "tenant.foundation.context.read" => ContextRead,
            "tenant.foundation.target.read" => TargetRead,
            "tenant.foundation.probe.write" => ProbeWrite,
            "foundation.context.switch" => ContextSwitch,
            "platform.metadata.read" => PlatformMetadataRead,
            "platform.user.suspend" => SuspendGlobalUser,
            "platform.user.reactivate" => ReactivateGlobalUser,
            "platform.user.offboard" => OffboardGlobalUser,
            "tenant.membership.suspend" => SuspendMembership,
            "tenant.membership.reactivate" => ReactivateMembership,
            "tenant.membership.revoke" => RevokeMembership,
            "support.tenant.read" => SupportRead,
            "platform.authority.assign" => AssignPlatformPermission,
            "tenant.role.assign" => AssignRole,
            "tenant.scope.grant" => AssignScopeGrant,
            "support.grant.approve" => ApproveSupportGrant,
            _ => default
        };

        return !permission.Equals(default(PermissionCode));
    }
}

internal readonly record struct OrganizationScope
{
    internal OrganizationScope(TenantId tenantId, ScopeKind kind, Guid targetId)
    {
        if (tenantId == default)
        {
            throw new ArgumentException("Organization scope requires a TenantId.", nameof(tenantId));
        }

        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }

        if (targetId == Guid.Empty)
        {
            throw new ArgumentException("Organization scope target must not be empty.", nameof(targetId));
        }

        if (kind == ScopeKind.Tenant && targetId != tenantId.Value)
        {
            throw new ArgumentException("A Tenant scope target must equal its Tenant identifier.", nameof(targetId));
        }

        TenantId = tenantId;
        Kind = kind;
        TargetId = targetId;
    }

    internal TenantId TenantId { get; }

    internal ScopeKind Kind { get; }

    internal Guid TargetId { get; }

    internal static OrganizationScope ForTenant(TenantId tenantId) =>
        new(tenantId, ScopeKind.Tenant, tenantId.Value);

    internal static OrganizationScope ForCompany(TenantId tenantId, Guid companyId) =>
        new(tenantId, ScopeKind.Company, companyId);

    internal static OrganizationScope ForBranch(TenantId tenantId, Guid branchId) =>
        new(tenantId, ScopeKind.Branch, branchId);

    internal static OrganizationScope ForWarehouse(TenantId tenantId, Guid warehouseId) =>
        new(tenantId, ScopeKind.Warehouse, warehouseId);
}

internal sealed class GlobalUser
{
    internal GlobalUser(UserId id, string normalizedEmail, string passwordHash, bool mfaEnabled)
    {
        if (string.IsNullOrWhiteSpace(normalizedEmail))
        {
            throw new ArgumentException("Normalized email must not be empty.", nameof(normalizedEmail));
        }

        if (string.IsNullOrWhiteSpace(passwordHash))
        {
            throw new ArgumentException("Password hash must not be empty.", nameof(passwordHash));
        }

        Id = id;
        NormalizedEmail = normalizedEmail;
        PasswordHash = passwordHash;
        MfaEnabled = mfaEnabled;
        Status = GlobalUserStatus.Active;
        Version = 1;
    }

    internal UserId Id { get; }

    internal string NormalizedEmail { get; }

    internal string PasswordHash { get; set; }

    internal GlobalUserStatus Status { get; set; }

    internal bool MfaEnabled { get; set; }

    internal int AccessFailedCount { get; set; }

    internal DateTimeOffset? LockoutEnd { get; set; }

    internal long Version { get; set; }
}

internal sealed class UserSession
{
    internal UserSession(
        SessionId id,
        UserId userId,
        string cookieTokenHash,
        DateTimeOffset issuedAt,
        DateTimeOffset absoluteExpiresAt)
    {
        Id = id;
        UserId = userId;
        CookieTokenHash = cookieTokenHash;
        IssuedAt = issuedAt;
        LastActivityAt = issuedAt;
        AbsoluteExpiresAt = absoluteExpiresAt;
        Version = 1;
    }

    internal SessionId Id { get; }

    internal UserId UserId { get; }

    internal string CookieTokenHash { get; }

    internal DateTimeOffset IssuedAt { get; }

    internal DateTimeOffset LastActivityAt { get; set; }

    internal DateTimeOffset AbsoluteExpiresAt { get; }

    internal DateTimeOffset? RevokedAt { get; set; }

    internal string? RevocationReason { get; set; }

    internal DateTimeOffset? MfaSatisfiedAt { get; set; }

    internal Dictionary<string, DateTimeOffset> FreshAuthenticationByOperation { get; } = new(StringComparer.Ordinal);

    internal HashSet<MembershipId> MembershipReferences { get; } = [];

    internal HashSet<SupportGrantId> SupportGrantReferences { get; } = [];

    internal long Version { get; set; }
}

internal sealed class TenantMembership
{
    internal TenantMembership(MembershipId id, UserId userId, TenantId tenantId)
    {
        Id = id;
        UserId = userId;
        TenantId = tenantId;
        Status = MembershipStatus.Active;
        Version = 1;
    }

    internal MembershipId Id { get; }

    internal UserId UserId { get; }

    internal TenantId TenantId { get; }

    internal MembershipStatus Status { get; set; }

    internal long Version { get; set; }
}

internal sealed class IdentityRole
{
    internal IdentityRole(RoleId id, string name, TenantId? tenantId, bool platformOwned, IEnumerable<PermissionCode> permissions)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new ArgumentException("Role name must not be empty.", nameof(name));
        }

        if (platformOwned == tenantId.HasValue)
        {
            throw new ArgumentException("A role must be either platform-owned or Tenant-owned, never both.", nameof(tenantId));
        }

        Id = id;
        Name = name.Trim();
        TenantId = tenantId;
        PlatformOwned = platformOwned;
        Permissions = permissions.ToHashSet();
    }

    internal RoleId Id { get; }

    internal string Name { get; }

    internal TenantId? TenantId { get; }

    internal bool PlatformOwned { get; }

    internal HashSet<PermissionCode> Permissions { get; }
}

internal sealed class RoleAssignment
{
    internal RoleAssignment(RoleId roleId, MembershipId membershipId, UserId userId, TenantId tenantId, UserId approverId)
    {
        if (approverId == userId)
        {
            throw new ArgumentException("Role assignment requires a separate named approver.", nameof(approverId));
        }

        RoleId = roleId;
        MembershipId = membershipId;
        UserId = userId;
        TenantId = tenantId;
        ApproverId = approverId;
        IsActive = true;
        Version = 1;
    }

    internal RoleId RoleId { get; }

    internal MembershipId MembershipId { get; }

    internal UserId UserId { get; }

    internal TenantId TenantId { get; }

    internal UserId ApproverId { get; }

    internal bool IsActive { get; set; }

    internal long Version { get; set; }
}

internal sealed class AccessScopeGrant
{
    internal AccessScopeGrant(ScopeGrantId id, MembershipId membershipId, UserId userId, OrganizationScope scope, UserId approverId)
    {
        if (approverId == userId)
        {
            throw new ArgumentException("Scope grant requires a separate named approver.", nameof(approverId));
        }

        Id = id;
        MembershipId = membershipId;
        UserId = userId;
        Scope = scope;
        ApproverId = approverId;
        IsActive = true;
        Version = 1;
    }

    internal ScopeGrantId Id { get; }

    internal MembershipId MembershipId { get; }

    internal UserId UserId { get; }

    internal OrganizationScope Scope { get; }

    internal UserId ApproverId { get; }

    internal bool IsActive { get; set; }

    internal long Version { get; set; }
}

internal sealed class SupportCase
{
    internal SupportCase(SupportCaseId id, TenantId tenantId)
    {
        Id = id;
        TenantId = tenantId;
        IsActive = true;
        Version = 1;
    }

    internal SupportCaseId Id { get; }

    internal TenantId TenantId { get; }

    internal bool IsActive { get; set; }

    internal long Version { get; set; }
}

internal sealed class SupportGrant
{
    internal SupportGrant(
        SupportGrantId id,
        SupportCaseId caseId,
        UserId userId,
        UserId tenantApproverId,
        TenantId tenantId,
        string purpose,
        OrganizationScope scope,
        IEnumerable<PermissionCode> permissions,
        DateTimeOffset expiresAt)
    {
        if (string.IsNullOrWhiteSpace(purpose))
        {
            throw new ArgumentException("Support grant purpose must not be empty.", nameof(purpose));
        }

        if (scope.TenantId != tenantId)
        {
            throw new ArgumentException("Support grant scope must belong to the target Tenant.", nameof(scope));
        }

        Id = id;
        CaseId = caseId;
        UserId = userId;
        TenantApproverId = tenantApproverId;
        TenantId = tenantId;
        Purpose = purpose.Trim();
        Scope = scope;
        Permissions = permissions.ToHashSet();
        ExpiresAt = expiresAt;
        Version = 1;
    }

    internal SupportGrantId Id { get; }

    internal SupportCaseId CaseId { get; }

    internal UserId UserId { get; }

    internal UserId TenantApproverId { get; }

    internal TenantId TenantId { get; }

    internal string Purpose { get; }

    internal OrganizationScope Scope { get; }

    internal HashSet<PermissionCode> Permissions { get; }

    internal DateTimeOffset ExpiresAt { get; }

    internal DateTimeOffset? RevokedAt { get; set; }

    internal long Version { get; set; }
}

internal sealed class Invitation
{
    internal Invitation(InvitationId id, string tokenHash, string normalizedEmail, UserId userId, long userVersionAtIssue, TenantId tenantId, MembershipId membershipId, long membershipVersionAtIssue, DateTimeOffset expiresAt)
    {
        Id = id;
        TokenHash = tokenHash;
        NormalizedEmail = normalizedEmail;
        UserId = userId;
        UserVersionAtIssue = userVersionAtIssue;
        TenantId = tenantId;
        MembershipId = membershipId;
        MembershipVersionAtIssue = membershipVersionAtIssue;
        ExpiresAt = expiresAt;
    }

    internal InvitationId Id { get; }

    internal string TokenHash { get; }

    internal string NormalizedEmail { get; }

    internal UserId UserId { get; }

    internal long UserVersionAtIssue { get; }

    internal TenantId TenantId { get; }

    internal MembershipId MembershipId { get; }

    internal long MembershipVersionAtIssue { get; }

    internal DateTimeOffset ExpiresAt { get; }

    internal bool Consumed { get; set; }
}

internal sealed class RecoveryArtifact
{
    internal RecoveryArtifact(RecoveryId id, string tokenHash, UserId userId, long userVersionAtIssue, DateTimeOffset expiresAt)
    {
        Id = id;
        TokenHash = tokenHash;
        UserId = userId;
        UserVersionAtIssue = userVersionAtIssue;
        ExpiresAt = expiresAt;
    }

    internal RecoveryId Id { get; }

    internal string TokenHash { get; }

    internal UserId UserId { get; }

    internal long UserVersionAtIssue { get; }

    internal DateTimeOffset ExpiresAt { get; }

    internal bool Consumed { get; set; }
}

internal sealed record AuthenticationResult(
    bool Succeeded,
    SessionId? SessionId,
    string? CookieValue,
    string PublicCode);

internal sealed record SessionValidation(
    bool Valid,
    UserId? UserId,
    SessionId? SessionId,
    string PublicCode);

internal sealed record AuthorizationDecision(
    bool Allowed,
    string PublicCode,
    string ReasonCategory,
    TenantContext? TenantContext,
    SafeSecurityEvidence Evidence);

internal sealed record SafeSecurityEvidence(
    Guid ActorId,
    string Operation,
    string AuthorizationPath,
    Guid? TenantId,
    string Result,
    string ReasonCategory,
    string CorrelationId,
    Guid? SessionId,
    string? Purpose,
    string? Scope);

internal sealed record LifecycleResult(bool Succeeded, string PublicCode, long Version, SafeSecurityEvidence Evidence);

internal sealed record AuthorityMutationResult(
    bool Succeeded,
    string PublicCode,
    long Version,
    Guid? ResourceId,
    SafeSecurityEvidence Evidence);

internal sealed class PlatformPermissionAssignment
{
    internal PlatformPermissionAssignment(
        UserId userId,
        PermissionCode permission,
        UserId approverId,
        string reason)
    {
        if (approverId == userId)
        {
            throw new ArgumentException("Platform permission assignment requires a separate approver.", nameof(approverId));
        }

        UserId = userId;
        Permission = permission;
        ApproverId = approverId;
        Reason = reason;
        IsActive = true;
        Version = 1;
    }

    internal UserId UserId { get; }

    internal PermissionCode Permission { get; }

    internal UserId ApproverId { get; }

    internal string Reason { get; }

    internal bool IsActive { get; set; }

    internal long Version { get; set; }
}

internal sealed record InvitationHandle(InvitationId Id, string TokenValue);

internal sealed record RecoveryHandle(RecoveryId Id, string TokenValue);

internal sealed record SessionHandle(SessionId Id, UserId UserId, string CookieValue);
