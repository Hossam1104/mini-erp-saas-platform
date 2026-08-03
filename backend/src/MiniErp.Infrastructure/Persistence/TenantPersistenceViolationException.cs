using MiniErp.App.BuildingBlocks.Tenancy;

namespace MiniErp.Infrastructure.Persistence;

/// <summary>Stable safe decision attached to a denied persistence operation.</summary>
public enum TenantPersistenceDecision
{
    /// <summary>The persistence decision was denied.</summary>
    Denied = 1
}

/// <summary>Operation category used by the safe persistence denial contract.</summary>
public enum TenantPersistenceOperation
{
    /// <summary>Adding a new record.</summary>
    Add = 1,
    /// <summary>Modifying an existing record.</summary>
    Modify = 2,
    /// <summary>Deleting an existing record.</summary>
    Delete = 3,
    /// <summary>Committing the unit of work.</summary>
    SaveChanges = 4,
    /// <summary>Validating a same-Tenant relationship.</summary>
    Relationship = 5,
    /// <summary>Reading a Tenant-owned record.</summary>
    Read = 6
}

/// <summary>Non-disclosing, machine-readable persistence violation categories.</summary>
public enum TenantPersistenceViolationCode
{
    /// <summary>The entity has no valid Tenant identifier.</summary>
    MissingTenantId = 1,
    /// <summary>The entity Tenant differs from trusted context on a new record.</summary>
    TenantContextMismatch = 2,
    /// <summary>
    /// The stored owner could not be safely established. This deliberately
    /// covers missing, invalid and foreign ownership with one public code.
    /// </summary>
    StoredOwnerUnavailable = 3,
    /// <summary>The caller attempted to mutate stored Tenant ownership.</summary>
    TenantOwnershipMutation = 6,
    /// <summary>A relationship did not remain within the trusted Tenant.</summary>
    RelationshipMismatch = 7,
    /// <summary>The relationship category is not valid.</summary>
    InvalidRelationship = 8,
    /// <summary>The provider reported a persistence conflict.</summary>
    PersistenceConflict = 9
}

/// <summary>
/// Safe denial raised when a Tenant persistence invariant is violated.
/// Provider details, foreign identifiers, payloads, and secrets are deliberately
/// excluded so the exception can cross an application boundary safely.
/// </summary>
public sealed class TenantPersistenceViolationException : InvalidOperationException
{
    private const string SafeMessage = "Tenant persistence operation was denied.";

    private TenantPersistenceViolationException(
        TenantPersistenceViolationCode code,
        TenantContext tenantContext,
        TenantPersistenceOperation operation,
        TenantPersistenceInternalReason? internalReason)
        : base(SafeMessage)
    {
        Code = code;
        TrustedTenantId = tenantContext.TenantId;
        AuthorizationPath = tenantContext.AuthorizationPath;
        CorrelationId = tenantContext.CorrelationId;
        Operation = operation;
        Decision = TenantPersistenceDecision.Denied;
        InternalReason = internalReason;
    }

    /// <summary>Stable violation code safe for later evidence emission.</summary>
    public TenantPersistenceViolationCode Code { get; }

    /// <summary>The trusted Tenant used for the authorization decision.</summary>
    public TenantId TrustedTenantId { get; }

    /// <summary>The server-derived authorization path in force.</summary>
    public TenantAuthorizationPath AuthorizationPath { get; }

    /// <summary>The optional server-derived correlation identifier.</summary>
    public CorrelationId? CorrelationId { get; }

    /// <summary>The persistence operation category that was denied.</summary>
    public TenantPersistenceOperation Operation { get; }

    /// <summary>The safe decision/result represented by this exception.</summary>
    public TenantPersistenceDecision Decision { get; }

    // Deliberately internal: precise storage observations are useful for
    // security diagnostics but must never become a client-facing oracle.
    internal TenantPersistenceInternalReason? InternalReason { get; }

    internal static TenantPersistenceViolationException Deny(
        TenantPersistenceViolationCode code,
        TenantContext tenantContext,
        TenantPersistenceOperation operation,
        TenantPersistenceInternalReason? internalReason = null)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        return new TenantPersistenceViolationException(code, tenantContext, operation, internalReason);
    }
}

/// <summary>Internal reason retained for diagnostics but never exported.</summary>
internal enum TenantPersistenceInternalReason
{
    StoredOwnerMissing = 1,
    StoredOwnerInvalid = 2,
    StoredOwnerMismatch = 3,
    VerifierUnavailable = 4
}
