namespace MiniErp.App.BuildingBlocks.Tenancy;

/// <summary>
/// Marker contract for records that are owned by exactly one Tenant.
/// </summary>
public interface ITenantOwned
{
    /// <summary>The immutable owner of the record.</summary>
    TenantId TenantId { get; }
}

/// <summary>
/// Representative reusable persistence record used to prove the foundation.
/// It is not a business aggregate or a later module workflow.
/// </summary>
public sealed class TenantOwnedRecord : ITenantOwned
{
    private TenantOwnedRecord()
    {
        BusinessKey = string.Empty;
    }

    /// <summary>Creates a validated representative Tenant-owned persistence record.</summary>
    public TenantOwnedRecord(
        TenantId tenantId,
        string businessKey,
        TenantRelationshipKind relationshipKind = TenantRelationshipKind.None,
        TenantId? relatedTenantId = null)
    {
        if (tenantId == default)
        {
            throw new ArgumentException("Tenant-owned records require a TenantId.", nameof(tenantId));
        }

        if (!Enum.IsDefined(relationshipKind))
        {
            throw new ArgumentOutOfRangeException(nameof(relationshipKind));
        }

        if (string.IsNullOrWhiteSpace(businessKey))
        {
            throw new ArgumentException("BusinessKey must not be empty.", nameof(businessKey));
        }

        if (relationshipKind == TenantRelationshipKind.None && relatedTenantId.HasValue)
        {
            throw new ArgumentException("A related Tenant requires a relationship kind.", nameof(relatedTenantId));
        }

        if (relationshipKind != TenantRelationshipKind.None && !relatedTenantId.HasValue)
        {
            throw new ArgumentException("A relationship kind requires a related Tenant.", nameof(relatedTenantId));
        }

        Id = Guid.NewGuid();
        TenantId = tenantId;
        BusinessKey = businessKey.Trim();
        RelationshipKind = relationshipKind;
        RelatedTenantId = relatedTenantId;
    }

    /// <summary>The record identifier.</summary>
    public Guid Id { get; private set; }

    /// <summary>The immutable Tenant owner.</summary>
    public TenantId TenantId { get; private set; }

    /// <summary>The value constrained by the Tenant-aware unique index.</summary>
    public string BusinessKey { get; private set; }

    /// <summary>The representative same-Tenant relationship category.</summary>
    public TenantRelationshipKind RelationshipKind { get; private set; }

    /// <summary>The related record owner used by the common relationship guard.</summary>
    public TenantId? RelatedTenantId { get; private set; }

    /// <summary>The optimistic-concurrency token.</summary>
    public byte[] Version { get; private set; } = [];
}

/// <summary>
/// Relationship categories covered by the common same-Tenant guard.
/// </summary>
public enum TenantRelationshipKind
{
    /// <summary>No relationship is represented.</summary>
    None = 0,
    /// <summary>Company to Branch.</summary>
    CompanyBranch = 1,
    /// <summary>Branch to Warehouse.</summary>
    BranchWarehouse = 2,
    /// <summary>Company to Department.</summary>
    CompanyDepartment = 3,
    /// <summary>Membership to Tenant.</summary>
    MembershipTenant = 4,
    /// <summary>Role Assignment to Tenant.</summary>
    RoleAssignmentTenant = 5,
    /// <summary>Access Scope Grant to Role Assignment.</summary>
    AccessScopeGrantRoleAssignment = 6,
    /// <summary>Support Case to Support Grant.</summary>
    SupportCaseGrant = 7,
    /// <summary>Tenant-owned file relationship.</summary>
    TenantFile = 8,
    /// <summary>Tenant-owned export relationship.</summary>
    TenantExport = 9,
    /// <summary>Tenant-owned durable-work relationship.</summary>
    DurableWork = 10
}

/// <summary>
/// Minimum initiating metadata carried into future durable work.
/// </summary>
public sealed class TenantWorkMetadata
{
    /// <summary>Creates immutable initiating metadata for later durable work.</summary>
    public TenantWorkMetadata(
        TenantId tenantId,
        TenantAuthorizationPath authorizationPath,
        CorrelationId correlationId,
        ScopeReference? scope = null,
        Guid? actorId = null)
    {
        if (!Enum.IsDefined(authorizationPath))
        {
            throw new ArgumentOutOfRangeException(nameof(authorizationPath));
        }

        if (tenantId == default)
        {
            throw new ArgumentException("Durable work metadata requires a TenantId.", nameof(tenantId));
        }

        if (string.IsNullOrWhiteSpace(correlationId.Value))
        {
            throw new ArgumentException("Durable work metadata requires a correlation identifier.", nameof(correlationId));
        }

        if (scope.HasValue && string.IsNullOrWhiteSpace(scope.Value.Value))
        {
            throw new ArgumentException("Scope reference must contain a value when supplied.", nameof(scope));
        }

        TenantId = tenantId;
        AuthorizationPath = authorizationPath;
        CorrelationId = correlationId;
        Scope = scope;
        ActorId = actorId;
    }

    /// <summary>The initiating Tenant.</summary>
    public TenantId TenantId { get; }

    /// <summary>The one recorded authorization path.</summary>
    public TenantAuthorizationPath AuthorizationPath { get; }

    /// <summary>The optional downward scope.</summary>
    public ScopeReference? Scope { get; }

    /// <summary>The initiating correlation identifier.</summary>
    public CorrelationId CorrelationId { get; }

    /// <summary>The optional initiating actor.</summary>
    public Guid? ActorId { get; }
}

/// <summary>
/// Narrow explicit factory for a tenant-bound persistence session.
/// </summary>
public interface ITenantPersistenceSessionFactory
{
    /// <summary>Creates a session bound to the supplied trusted context.</summary>
    ITenantPersistenceSession Create(TenantContext tenantContext);
}

/// <summary>
/// Tenant-bound persistence session; no unscoped session is exposed.
/// </summary>
public interface ITenantPersistenceSession : IAsyncDisposable
{
    /// <summary>The narrow repository surface for this Tenant session.</summary>
    ITenantRecordRepository Records { get; }

    /// <summary>Persists changes after the ownership guard has run.</summary>
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Explicit, tenant-bound record operations without an optional Tenant filter.
/// </summary>
public interface ITenantRecordRepository
{
    /// <summary>Adds a record to this session's Tenant-bound unit of work.</summary>
    void Add(TenantOwnedRecord record);

    /// <summary>Lists records constrained to the session's trusted Tenant.</summary>
    Task<IReadOnlyList<TenantOwnedRecord>> ListAsync(CancellationToken cancellationToken = default);

    /// <summary>Finds a record without revealing another Tenant's target existence.</summary>
    Task<TenantOwnedRecord?> FindAsync(Guid recordId, CancellationToken cancellationToken = default);
}
