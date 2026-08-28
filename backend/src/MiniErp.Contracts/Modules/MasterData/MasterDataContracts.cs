#pragma warning disable CS1591

using MiniErp.Contracts.Modules.Audit;

namespace MiniErp.Contracts.Modules.MasterData;

public enum MasterDataResourceKind
{
    Product = 1,
    ProductCategory = 2,
    UnitOfMeasure = 3,
    Supplier = 4,
    BusinessCustomer = 5,
    PriceList = 6,
    Tax = 7,
    PaymentTerm = 8,
    Currency = 9,
    ExchangeRate = 10,
    Import = 11
}

public enum BusinessPartyRole
{
    Supplier = 1,
    BusinessCustomer = 2
}

public enum MasterDataOperation
{
    View = 1,
    Create = 2,
    Edit = 3,
    Activate = 4,
    Deactivate = 5,
    Approve = 6,
    Import = 7,
    ViewAuditHistory = 8,
    Reactivate = 9
}

public enum MasterDataLifecycleState
{
    Active = 1,
    Inactive = 2
}

public enum MasterDataCapability
{
    View = 1,
    Create = 2,
    Edit = 3,
    Activate = 4,
    Deactivate = 5,
    Approve = 6,
    MaintainPriceLists = 7,
    MaintainTaxes = 8,
    MaintainExchangeRates = 9,
    ViewAuditHistory = 10,
    ImportMigrate = 11
}

public enum OrganizationScopeKind
{
    Company = 1,
    Branch = 2,
    Warehouse = 3
}

public enum MasterDataApprovalPolicyStatus
{
    NotApplicable = 1,
    NotConfigured = 2,
    RequiresApproval = 3,
    Pending = 4,
    Approved = 5,
    Rejected = 6
}

public enum MasterDataPolicyOutcome
{
    Allowed = 1,
    Denied = 2,
    ApprovalRequired = 3,
    ApprovalPending = 4,
    Approved = 5,
    Rejected = 6,
    NotConfigured = 7
}

public readonly record struct TenantOwnership
{
    public TenantOwnership(Guid tenantId)
    {
        if (tenantId == Guid.Empty)
        {
            throw new ArgumentException("Tenant ownership requires a non-empty identifier.", nameof(tenantId));
        }

        TenantId = tenantId;
    }

    public Guid TenantId { get; }
}

public readonly record struct OrganizationReference
{
    public OrganizationReference(TenantOwnership tenant, OrganizationScopeKind kind, Guid id)
    {
        if (tenant.TenantId == Guid.Empty)
        {
            throw new ArgumentException("The organization reference requires Tenant ownership.", nameof(tenant));
        }

        if (!Enum.IsDefined(kind))
        {
            throw new ArgumentOutOfRangeException(nameof(kind));
        }

        if (id == Guid.Empty)
        {
            throw new ArgumentException("The organization reference requires a non-empty identifier.", nameof(id));
        }

        Tenant = tenant;
        Kind = kind;
        Id = id;
    }

    public TenantOwnership Tenant { get; }

    public OrganizationScopeKind Kind { get; }

    public Guid Id { get; }

    public string CanonicalValue => $"{Kind}:{Id:D}";
}

public readonly record struct ScopePolicyReference
{
    public ScopePolicyReference(string policyId, int version)
    {
        if (string.IsNullOrWhiteSpace(policyId))
        {
            throw new ArgumentException("The scope policy requires an identifier.", nameof(policyId));
        }

        var normalized = policyId.Trim();
        if (normalized.Length > 64 || normalized.Any(char.IsControl))
        {
            throw new ArgumentException("The scope policy identifier is outside the approved bound.", nameof(policyId));
        }

        if (version < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(version));
        }

        PolicyId = normalized;
        Version = version;
    }

    public string PolicyId { get; }

    public int Version { get; }
}

public sealed class BusinessScope
{
    public BusinessScope(
        TenantOwnership tenant,
        OrganizationReference? organizationAnchor,
        ScopePolicyReference policy)
    {
        if (tenant.TenantId == Guid.Empty)
        {
            throw new ArgumentException("Business scope requires Tenant ownership.", nameof(tenant));
        }

        if (organizationAnchor is { } anchor && anchor.Tenant != tenant)
        {
            throw new ArgumentException("The organization anchor must belong to the same Tenant.", nameof(organizationAnchor));
        }

        if (string.IsNullOrWhiteSpace(policy.PolicyId) || policy.Version < 1)
        {
            throw new ArgumentException("A valid scope policy reference is required.", nameof(policy));
        }

        Tenant = tenant;
        OrganizationAnchor = organizationAnchor;
        Policy = policy;
    }

    public TenantOwnership Tenant { get; }

    public OrganizationReference? OrganizationAnchor { get; }

    public ScopePolicyReference Policy { get; }
}

public sealed class MasterDataScopeSelection
{
    public MasterDataScopeSelection(Guid? requestedTenantId = null, BusinessScope? requestedScope = null)
    {
        if (requestedTenantId == Guid.Empty)
        {
            throw new ArgumentException("The requested Tenant identifier must be non-empty when supplied.", nameof(requestedTenantId));
        }

        RequestedTenantId = requestedTenantId;
        RequestedScope = requestedScope;
    }

    public Guid? RequestedTenantId { get; }

    public BusinessScope? RequestedScope { get; }
}

public sealed class MasterDataResourceReference
{
    public MasterDataResourceReference(
        MasterDataResourceKind resourceKind,
        TenantOwnership tenant,
        Guid? stableId = null,
        string? businessCode = null,
        BusinessScope? scope = null)
    {
        if (!Enum.IsDefined(resourceKind))
        {
            throw new ArgumentOutOfRangeException(nameof(resourceKind));
        }

        if (tenant.TenantId == Guid.Empty)
        {
            throw new ArgumentException("The resource reference requires Tenant ownership.", nameof(tenant));
        }

        if (stableId == Guid.Empty)
        {
            throw new ArgumentException("The stable identifier must be non-empty when supplied.", nameof(stableId));
        }

        var normalizedCode = businessCode?.Trim();
        if (normalizedCode is { Length: > 128 } || normalizedCode?.Any(char.IsControl) == true)
        {
            throw new ArgumentException("The business code is outside the approved bound.", nameof(businessCode));
        }

        if (stableId is null && string.IsNullOrWhiteSpace(normalizedCode))
        {
            throw new ArgumentException("A stable identifier or opaque business code is required.", nameof(stableId));
        }

        if (scope is not null && scope.Tenant != tenant)
        {
            throw new ArgumentException("The resource scope must belong to the same Tenant.", nameof(scope));
        }

        ResourceKind = resourceKind;
        Tenant = tenant;
        StableId = stableId;
        BusinessCode = string.IsNullOrWhiteSpace(normalizedCode) ? null : normalizedCode;
        Scope = scope;
    }

    public MasterDataResourceKind ResourceKind { get; }

    public TenantOwnership Tenant { get; }

    public Guid? StableId { get; }

    public string? BusinessCode { get; }

    public BusinessScope? Scope { get; }
}

public sealed class ReferenceSnapshot
{
    public ReferenceSnapshot(
        MasterDataResourceKind resourceKind,
        Guid stableId,
        TenantOwnership tenant,
        long version,
        string appliedValue,
        DateOnly? effectiveOn = null)
    {
        if (!Enum.IsDefined(resourceKind))
        {
            throw new ArgumentOutOfRangeException(nameof(resourceKind));
        }

        if (tenant.TenantId == Guid.Empty)
        {
            throw new ArgumentException("The reference snapshot requires Tenant ownership.", nameof(tenant));
        }

        if (stableId == Guid.Empty)
        {
            throw new ArgumentException("The stable identifier must be non-empty.", nameof(stableId));
        }

        if (version < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(version));
        }

        var normalizedValue = appliedValue?.Trim();
        if (string.IsNullOrWhiteSpace(normalizedValue)
            || normalizedValue.Length > 128
            || normalizedValue.Any(char.IsControl))
        {
            throw new ArgumentException("The reference value is outside the approved bound.", nameof(appliedValue));
        }

        ResourceKind = resourceKind;
        StableId = stableId;
        Tenant = tenant;
        Version = version;
        AppliedValue = normalizedValue;
        EffectiveOn = effectiveOn;
    }

    public MasterDataResourceKind ResourceKind { get; }

    public Guid StableId { get; }

    public TenantOwnership Tenant { get; }

    public long Version { get; }

    public string AppliedValue { get; }

    public DateOnly? EffectiveOn { get; }
}

public sealed class LocalizedName
{
    public LocalizedName(string? english = null, string? arabic = null)
    {
        English = Normalize(english, nameof(english));
        Arabic = Normalize(arabic, nameof(arabic));
        if (English is null && Arabic is null)
        {
            throw new ArgumentException("At least one localized value is required.");
        }
    }

    public string? English { get; }

    public string? Arabic { get; }

    private static string? Normalize(string? value, string name)
    {
        if (value is null)
        {
            return null;
        }

        var normalized = value.Trim();
        if (normalized.Length == 0 || normalized.Length > 256 || normalized.Any(char.IsControl))
        {
            throw new ArgumentException("The localized value is outside the approved bound.", name);
        }

        return normalized;
    }
}

public sealed class MasterDataApprovalPolicyResult
{
    public MasterDataApprovalPolicyResult(
        MasterDataApprovalPolicyStatus status,
        Guid? approvalId = null,
        Guid? approverId = null)
    {
        if (!Enum.IsDefined(status))
        {
            throw new ArgumentOutOfRangeException(nameof(status));
        }

        if (approvalId == Guid.Empty)
        {
            throw new ArgumentException("The approval identifier must be non-empty when supplied.", nameof(approvalId));
        }

        if (approverId == Guid.Empty)
        {
            throw new ArgumentException("The approver identifier must be non-empty when supplied.", nameof(approverId));
        }

        Status = status;
        ApprovalId = approvalId;
        ApproverId = approverId;
    }

    public MasterDataApprovalPolicyStatus Status { get; }

    public Guid? ApprovalId { get; }

    public Guid? ApproverId { get; }
}

public sealed class MasterDataPolicyDecision
{
    public MasterDataPolicyDecision(MasterDataPolicyOutcome outcome, string code)
    {
        if (!Enum.IsDefined(outcome))
        {
            throw new ArgumentOutOfRangeException(nameof(outcome));
        }

        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("A policy decision code is required.", nameof(code));
        }

        var normalizedCode = code.Trim();
        if (normalizedCode.Length > 128 || normalizedCode.Any(char.IsControl))
        {
            throw new ArgumentException("The policy decision code is outside the approved bound.", nameof(code));
        }

        Outcome = outcome;
        Code = normalizedCode;
    }

    public MasterDataPolicyOutcome Outcome { get; }

    public string Code { get; }

    public bool IsAllowed => Outcome is MasterDataPolicyOutcome.Allowed or MasterDataPolicyOutcome.Approved;

    public static MasterDataPolicyDecision Allowed(string code = "allowed") =>
        new(MasterDataPolicyOutcome.Allowed, code);

    public static MasterDataPolicyDecision Denied(string code = "authorization_denied") =>
        new(MasterDataPolicyOutcome.Denied, code);

    public static MasterDataPolicyDecision ApprovalRequired(string code = "approval_required") =>
        new(MasterDataPolicyOutcome.ApprovalRequired, code);

    public static MasterDataPolicyDecision ApprovalPending(string code = "approval_pending") =>
        new(MasterDataPolicyOutcome.ApprovalPending, code);

    public static MasterDataPolicyDecision Approved(string code = "approved") =>
        new(MasterDataPolicyOutcome.Approved, code);

    public static MasterDataPolicyDecision Rejected(string code = "rejected") =>
        new(MasterDataPolicyOutcome.Rejected, code);

    public static MasterDataPolicyDecision NotConfigured(string code = "policy_not_configured") =>
        new(MasterDataPolicyOutcome.NotConfigured, code);
}

public sealed class MasterDataAuditEvidence
{
    private MasterDataAuditEvidence(
        Guid evidenceId,
        DateTimeOffset occurredAt,
        string operationId,
        string correlationId,
        Guid actorId,
        Guid sessionId,
        FoundationAuditAuthorizationPath authorizationPath,
        TenantOwnership tenant,
        BusinessScope? scope,
        MasterDataOperation operation,
        MasterDataResourceKind resourceKind,
        Guid? resourceId,
        string? businessCode,
        MasterDataPolicyOutcome policyOutcome,
        FoundationAuditDecision decision,
        FoundationAuditReason reason,
        string? beforeSummary,
        string? afterSummary,
        Guid? approverId)
    {
        if (evidenceId == Guid.Empty || actorId == Guid.Empty || sessionId == Guid.Empty)
        {
            throw new ArgumentException("Audit evidence requires non-empty identity values.");
        }

        if (occurredAt == default)
        {
            throw new ArgumentException("Audit evidence requires an occurrence timestamp.", nameof(occurredAt));
        }

        if (tenant.TenantId == Guid.Empty)
        {
            throw new ArgumentException("Audit evidence requires Tenant ownership.", nameof(tenant));
        }

        if (!Enum.IsDefined(authorizationPath)
            || authorizationPath is FoundationAuditAuthorizationPath.PlatformGovernanceContext
            || !Enum.IsDefined(operation)
            || !Enum.IsDefined(resourceKind)
            || !Enum.IsDefined(policyOutcome)
            || !Enum.IsDefined(decision)
            || !Enum.IsDefined(reason))
        {
            throw new ArgumentException("Audit evidence contains an unsupported value.");
        }

        if (string.IsNullOrWhiteSpace(operationId)
            || string.IsNullOrWhiteSpace(correlationId)
            || operationId.Trim().Length > 128
            || correlationId.Trim().Length > 128
            || operationId.Any(char.IsControl)
            || correlationId.Any(char.IsControl))
        {
            throw new ArgumentException("Audit evidence text is outside the approved bound.");
        }

        if (resourceId == Guid.Empty || approverId == Guid.Empty)
        {
            throw new ArgumentException("Audit evidence identifiers must be non-empty when supplied.");
        }

        if (decision == FoundationAuditDecision.Allowed
            && policyOutcome is not (MasterDataPolicyOutcome.Allowed or MasterDataPolicyOutcome.Approved))
        {
            throw new ArgumentException("An allowed audit decision requires an allowed policy outcome.", nameof(decision));
        }

        if (decision != FoundationAuditDecision.Allowed
            && policyOutcome is (MasterDataPolicyOutcome.Allowed or MasterDataPolicyOutcome.Approved))
        {
            throw new ArgumentException("A denied audit decision cannot carry an allowed policy outcome.", nameof(decision));
        }

        if (scope is not null && scope.Tenant != tenant)
        {
            throw new ArgumentException("Audit evidence scope must belong to the evidence Tenant.");
        }

        BusinessCode = NormalizeOptional(businessCode);
        BeforeSummary = NormalizeOptional(beforeSummary);
        AfterSummary = NormalizeOptional(afterSummary);

        EvidenceId = evidenceId;
        OccurredAt = occurredAt;
        OperationId = operationId.Trim();
        CorrelationId = correlationId.Trim();
        ActorId = actorId;
        SessionId = sessionId;
        AuthorizationPath = authorizationPath;
        Tenant = tenant;
        Scope = scope;
        Operation = operation;
        ResourceKind = resourceKind;
        ResourceId = resourceId;
        PolicyOutcome = policyOutcome;
        Decision = decision;
        Reason = reason;
        ApproverId = approverId;
    }

    internal static MasterDataAuditEvidence CreateValidated(
        Guid evidenceId,
        DateTimeOffset occurredAt,
        string operationId,
        string correlationId,
        Guid actorId,
        Guid sessionId,
        FoundationAuditAuthorizationPath authorizationPath,
        TenantOwnership tenant,
        BusinessScope? scope,
        MasterDataOperation operation,
        MasterDataResourceKind resourceKind,
        Guid? resourceId,
        string? businessCode,
        MasterDataPolicyOutcome policyOutcome,
        FoundationAuditDecision decision,
        FoundationAuditReason reason,
        string? beforeSummary,
        string? afterSummary,
        Guid? approverId) => new(
            evidenceId,
            occurredAt,
            operationId,
            correlationId,
            actorId,
            sessionId,
            authorizationPath,
            tenant,
            scope,
            operation,
            resourceKind,
            resourceId,
            businessCode,
            policyOutcome,
            decision,
            reason,
            beforeSummary,
            afterSummary,
            approverId);

    /// <summary>
    /// Creates immutable evidence for a trusted bounded-module reference read.
    /// The caller must already have completed the owning module's authorization;
    /// this factory only preserves the actor, Tenant, and operation facts for
    /// the Master Data reference port.
    /// </summary>
    public static MasterDataAuditEvidence CreateTrustedModuleReference(
        string operationId,
        string correlationId,
        Guid actorId,
        Guid sessionId,
        Guid tenantId,
        Guid? organizationId = null,
        OrganizationScopeKind? organizationKind = null,
        string? afterSummary = null) =>
        CreateValidated(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            operationId,
            correlationId,
            actorId,
            sessionId,
            FoundationAuditAuthorizationPath.OrdinaryMembership,
            new TenantOwnership(tenantId),
            organizationId is { } id && organizationKind is { } kind
                ? new BusinessScope(
                    new TenantOwnership(tenantId),
                    new OrganizationReference(new TenantOwnership(tenantId), kind, id),
                    new ScopePolicyReference("trusted-module-reference.v1", 1))
                : null,
            MasterDataOperation.View,
            MasterDataResourceKind.PriceList,
            null,
            null,
            MasterDataPolicyOutcome.Allowed,
            FoundationAuditDecision.Allowed,
            FoundationAuditReason.Allowed,
            null,
            afterSummary,
            null);

    public Guid EvidenceId { get; }

    public DateTimeOffset OccurredAt { get; }

    public string OperationId { get; }

    public string CorrelationId { get; }

    public Guid ActorId { get; }

    public Guid SessionId { get; }

    public FoundationAuditAuthorizationPath AuthorizationPath { get; }

    public TenantOwnership Tenant { get; }

    public BusinessScope? Scope { get; }

    public MasterDataOperation Operation { get; }

    public MasterDataResourceKind ResourceKind { get; }

    public Guid? ResourceId { get; }

    public string? BusinessCode { get; }

    public MasterDataPolicyOutcome PolicyOutcome { get; }

    public FoundationAuditDecision Decision { get; }

    public FoundationAuditReason Reason { get; }

    public string? BeforeSummary { get; }

    public string? AfterSummary { get; }

    public Guid? ApproverId { get; }

    private static string? NormalizeOptional(string? value)
    {
        if (value is null)
        {
            return null;
        }

        var normalized = value.Trim();
        if (normalized.Length == 0 || normalized.Length > 2048 || normalized.Any(char.IsControl))
        {
            throw new ArgumentException("Audit evidence text is outside the approved bound.");
        }

        return normalized;
    }
}

public interface IMasterDataAuditEvidenceSink
{
    ValueTask<MasterDataAuditAppendResult> AppendAsync(
        MasterDataAuditEvidence evidence,
        CancellationToken cancellationToken = default);
}

public sealed record MasterDataAuditAppendResult(
    bool Appended,
    Guid EvidenceId,
    string Code)
{
    public static MasterDataAuditAppendResult Success(Guid evidenceId) =>
        new(true, evidenceId, "recorded");

    public static MasterDataAuditAppendResult Failure(Guid evidenceId, string code) =>
        new(false, evidenceId, code);
}

#pragma warning restore CS1591
