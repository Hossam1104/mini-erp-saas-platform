namespace MiniErp.Contracts.Modules.Foundation;

/// <summary>
/// The public security profiles used by the Foundation REST contract.
/// Each public operation selects exactly one profile.
/// </summary>
public enum FoundationSecurityProfile
{
    /// <summary>Anonymous operation with no authorization context.</summary>
    Anonymous = 1,
    /// <summary>Authenticated session without a Tenant or platform path.</summary>
    AuthenticatedSession = 2,
    /// <summary>Active ordinary Tenant membership.</summary>
    OrdinaryMembership = 3,
    /// <summary>Case-bound support access for one Tenant.</summary>
    SupportGrant = 4,
    /// <summary>Purpose-bound Platform governance context.</summary>
    PlatformGovernanceContext = 5,
    /// <summary>High-risk operation requiring additional assurance.</summary>
    HighRisk = 6
}

/// <summary>
/// Visibility of an operation in the public contract catalogue.
/// </summary>
public enum FoundationOperationVisibility
{
    /// <summary>Included in the public REST contract.</summary>
    Public = 1,
    /// <summary>Internal application operation, never mapped publicly.</summary>
    Internal = 2
}

/// <summary>
/// Stable mapping between one versioned endpoint and one application operation.
/// </summary>
public sealed record FoundationOperationDescriptor(
    string OperationId,
    string Route,
    string HttpMethod,
    FoundationSecurityProfile SecurityProfile,
    FoundationOperationVisibility Visibility,
    bool RequiresMandatoryAudit = false,
    bool IsUnsafe = false);

/// <summary>
/// Metadata attached to every public endpoint.
/// </summary>
public sealed record FoundationOperationMetadata(
    string OperationId,
    FoundationSecurityProfile SecurityProfile,
    FoundationOperationVisibility Visibility = FoundationOperationVisibility.Public,
    bool RequiresMandatoryAudit = false,
    bool IsUnsafe = false);

/// <summary>Safe first-party authentication request.</summary>
public sealed record FoundationSignInRequest(string? Login, string? Password);

/// <summary>Safe session summary returned to the first-party shell.</summary>
public sealed record FoundationSessionResponse(
    bool Authenticated,
    Guid? ActorId,
    Guid? SessionId,
    string LifecycleState,
    DateTimeOffset? AbsoluteExpiresAt,
    string? SelectedPath,
    Guid? SelectedTenantId,
    Guid? SelectedContextId,
    long ContextVersion);

/// <summary>One safe server-derived context candidate.</summary>
public sealed record FoundationContextCandidateResponse(
    Guid ContextId,
    string Kind,
    Guid? TenantId,
    string DisplayName,
    long Version);

/// <summary>Authorized-context list response.</summary>
public sealed record FoundationContextsResponse(
    IReadOnlyList<FoundationContextCandidateResponse> Contexts);

/// <summary>Server-confirmed context switch request.</summary>
public sealed record FoundationContextSwitchRequest(Guid ContextId, long ExpectedVersion = 0);

/// <summary>
/// Stable, safe response for the representative Foundation context operation.
/// </summary>
public sealed record FoundationContextResponse(
    string OperationId,
    string CorrelationId,
    string AuthorizationPath,
    string LifecycleState,
    string Permission,
    string? TenantId,
    string? Scope,
    bool PlatformGovernance);

/// <summary>
/// Stable, safe response for the representative Foundation write operation.
/// </summary>
public sealed record FoundationWriteResponse(
    string OperationId,
    string CorrelationId,
    string Result,
    string Version,
    bool Replayed);

/// <summary>
/// The allow-listed request body for the non-business Foundation write probe.
/// </summary>
public sealed record FoundationWriteRequest(
    string? Value,
    Guid? TargetId = null);

/// <summary>
/// Stable, safe response for the target-existence demonstration.
/// </summary>
public sealed record FoundationTargetResponse(
    string OperationId,
    string CorrelationId,
    string Result,
    string TargetId);

/// <summary>
/// The single public operation catalogue for the Foundation seam.
/// </summary>
public static class FoundationOperationCatalog
{
    /// <summary>The complete public operation catalogue.</summary>
    public static IReadOnlyList<FoundationOperationDescriptor> PublicOperations { get; } =
    [
        new("platform.health", "/health", "GET", FoundationSecurityProfile.Anonymous, FoundationOperationVisibility.Public),
        new("platform.openapi", "/openapi/v1.json", "GET", FoundationSecurityProfile.Anonymous, FoundationOperationVisibility.Public),
        new("platform.module-registration", "/api/v1/module-registration", "GET", FoundationSecurityProfile.Anonymous, FoundationOperationVisibility.Public),
        new("foundation.tenant-context.read", "/api/v1/foundation/tenant-context", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public),
        new("foundation.support-context.read", "/api/v1/foundation/support-context", "GET", FoundationSecurityProfile.SupportGrant, FoundationOperationVisibility.Public),
        new("foundation.platform-context.read", "/api/v1/foundation/platform-context", "GET", FoundationSecurityProfile.PlatformGovernanceContext, FoundationOperationVisibility.Public),
        new("foundation.target.read", "/api/v1/foundation/targets/{targetId}", "GET", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public),
        new("foundation.probe.write", "/api/v1/foundation/probe", "POST", FoundationSecurityProfile.OrdinaryMembership, FoundationOperationVisibility.Public, RequiresMandatoryAudit: true, IsUnsafe: true),
        new("auth.antiforgery.read", "/api/v1/auth/antiforgery", "GET", FoundationSecurityProfile.Anonymous, FoundationOperationVisibility.Public),
        new("auth.sign-in", "/api/v1/auth/sign-in", "POST", FoundationSecurityProfile.Anonymous, FoundationOperationVisibility.Public, IsUnsafe: true),
        new("auth.sign-out", "/api/v1/auth/sign-out", "POST", FoundationSecurityProfile.AuthenticatedSession, FoundationOperationVisibility.Public, RequiresMandatoryAudit: true, IsUnsafe: true),
        new("auth.session.read", "/api/v1/auth/session", "GET", FoundationSecurityProfile.AuthenticatedSession, FoundationOperationVisibility.Public),
        new("auth.contexts.read", "/api/v1/auth/contexts", "GET", FoundationSecurityProfile.AuthenticatedSession, FoundationOperationVisibility.Public),
        new("auth.context-switch", "/api/v1/auth/context-switch", "POST", FoundationSecurityProfile.AuthenticatedSession, FoundationOperationVisibility.Public, RequiresMandatoryAudit: true, IsUnsafe: true)
    ];

    /// <summary>Internal operations deliberately excluded from public routing.</summary>
    public static IReadOnlyList<FoundationOperationDescriptor> InternalOperations { get; } =
    [
        new("identity.invitation.issue", "", "INTERNAL", FoundationSecurityProfile.HighRisk, FoundationOperationVisibility.Internal),
        new("identity.recovery.consume", "", "INTERNAL", FoundationSecurityProfile.HighRisk, FoundationOperationVisibility.Internal)
    ];
}
