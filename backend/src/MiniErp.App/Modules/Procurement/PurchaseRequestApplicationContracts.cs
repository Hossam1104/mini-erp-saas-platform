#pragma warning disable CS1591

using MiniErp.App.BuildingBlocks.Rest;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.Modules.MasterData;
using MiniErp.Contracts.Modules.Foundation;
using MiniErp.Contracts.Modules.MasterData;
using MiniErp.Contracts.Modules.Procurement;

namespace MiniErp.App.Modules.Procurement;

public sealed record PurchaseRequestOperationResult<T>(
    bool Succeeded,
    string Code,
    T? Value)
{
    public static PurchaseRequestOperationResult<T> Success(T value) =>
        new(true, "succeeded", value);

    public static PurchaseRequestOperationResult<T> Failure(string code) =>
        new(false, code, default);
}

public enum PurchaseRequestPersistenceOutcome
{
    Succeeded = 1,
    NotFound = 2,
    Conflict = 3,
    InvalidState = 4,
    Duplicate = 5,
    Failure = 6
}

public sealed record PurchaseRequestPersistenceResult<T>(
    PurchaseRequestPersistenceOutcome Outcome,
    string Code,
    T? Value)
{
    public bool Succeeded => Outcome == PurchaseRequestPersistenceOutcome.Succeeded;

    public static PurchaseRequestPersistenceResult<T> Success(T value) =>
        new(PurchaseRequestPersistenceOutcome.Succeeded, "persisted", value);

    public static PurchaseRequestPersistenceResult<T> Denied(
        PurchaseRequestPersistenceOutcome outcome,
        string code) => new(outcome, code, default);
}

public sealed class ProcurementRequestContext
{
    private ProcurementRequestContext(
        TenantId tenantId,
        TenantAuthorizationPath authorizationPath,
        ScopeReference? trustedScope,
        Guid actorId,
        Guid sessionId,
        CorrelationId? correlationId,
        FoundationRequestContext foundationContext)
    {
        TenantId = tenantId;
        AuthorizationPath = authorizationPath;
        TrustedScope = trustedScope;
        ActorId = actorId;
        SessionId = sessionId;
        CorrelationId = correlationId;
        FoundationContext = foundationContext;
    }

    public TenantId TenantId { get; }

    public TenantAuthorizationPath AuthorizationPath { get; }

    public ScopeReference? TrustedScope { get; }

    public Guid ActorId { get; }

    public Guid SessionId { get; }

    public CorrelationId? CorrelationId { get; }

    internal FoundationRequestContext FoundationContext { get; }

    public TenantContext TenantContext => FoundationContext.TenantContext
        ?? throw new InvalidOperationException("A Procurement context must carry a trusted Tenant context.");

    internal static ProcurementRequestContext FromFoundationContext(
        FoundationRequestContext foundationContext)
    {
        ArgumentNullException.ThrowIfNull(foundationContext);

        if (foundationContext.SecurityProfile is not (
            FoundationSecurityProfile.OrdinaryMembership
            or FoundationSecurityProfile.SupportGrant)
            || foundationContext.TenantContext is null
            || foundationContext.PlatformGovernanceContext is not null
            || foundationContext.ActorId is not { } actorId
            || foundationContext.SessionId is not { } sessionId
            || actorId == Guid.Empty
            || sessionId == Guid.Empty)
        {
            throw new ArgumentException("A Tenant-bound Foundation context is required.", nameof(foundationContext));
        }

        var tenant = foundationContext.TenantContext;
        var expectedProfile = tenant.AuthorizationPath switch
        {
            TenantAuthorizationPath.OrdinaryMembership => FoundationSecurityProfile.OrdinaryMembership,
            TenantAuthorizationPath.SupportGrant => FoundationSecurityProfile.SupportGrant,
            _ => throw new ArgumentException("The Tenant authorization path is not supported.", nameof(foundationContext))
        };

        if (foundationContext.SecurityProfile != expectedProfile
            || tenant.ActorId is { } tenantActorId && tenantActorId != actorId)
        {
            throw new ArgumentException("The Foundation context contains inconsistent authorization facts.", nameof(foundationContext));
        }

        return new ProcurementRequestContext(
            tenant.TenantId,
            tenant.AuthorizationPath,
            tenant.Scope,
            actorId,
            sessionId,
            tenant.CorrelationId,
            foundationContext);
    }
}

public sealed record ProcurementContextResolution(
    bool Allowed,
    string Code,
    ProcurementRequestContext? Context)
{
    public static ProcurementContextResolution Success(ProcurementRequestContext context) =>
        new(true, "resolved", context);

    public static ProcurementContextResolution Denied(string code) =>
        new(false, code, null);
}

public sealed class ProcurementTenantContextResolver
{
    public ProcurementContextResolution Resolve(FoundationRequestContext trustedContext)
    {
        ArgumentNullException.ThrowIfNull(trustedContext);
        try
        {
            return ProcurementContextResolution.Success(
                ProcurementRequestContext.FromFoundationContext(trustedContext));
        }
        catch (ArgumentException)
        {
            return ProcurementContextResolution.Denied("tenant_context_failed");
        }
    }
}

public sealed record PurchaseRequestScope(
    Guid TenantId,
    Guid CompanyId,
    Guid? BranchId)
{
    public string CanonicalReference => BranchId is { } branchId
        ? $"Branch:{branchId:D}"
        : $"Company:{CompanyId:D}";

    public bool Matches(PurchaseRequestScope other) =>
        other is not null
        && TenantId == other.TenantId
        && CompanyId == other.CompanyId
        && BranchId == other.BranchId;
}

public sealed record PurchaseRequestLineSnapshot(
    Guid Id,
    Guid ProductId,
    string ProductSku,
    string ProductName,
    Guid UnitOfMeasureId,
    string UnitOfMeasureCode,
    decimal Quantity,
    DateOnly NeedByDate,
    string Purpose,
    byte[]? Version = null);

public sealed record PurchaseRequestCreateCommand(
    Guid Id,
    PurchaseRequestScope Scope,
    Guid RequesterId,
    string? Purpose,
    IReadOnlyList<PurchaseRequestLineSnapshot> Lines,
    DateTimeOffset OccurredAt,
    string? IdempotencyKey);

public sealed record PurchaseRequestEditCommand(
    Guid Id,
    PurchaseRequestScope Scope,
    Guid RequesterId,
    string? Purpose,
    IReadOnlyList<PurchaseRequestLineSnapshot> Lines,
    byte[] ExpectedVersion,
    DateTimeOffset OccurredAt,
    string? IdempotencyKey);

public sealed record PurchaseRequestSubmitCommand(
    Guid Id,
    byte[] ExpectedVersion,
    PurchaseRequestApprovalPolicyDefinition Policy,
    DateTimeOffset OccurredAt,
    string? IdempotencyKey);

public sealed record PurchaseRequestApprovalCommand(
    Guid Id,
    byte[] ExpectedVersion,
    Guid ActorId,
    Guid? DelegatedFromActorId,
    DateTimeOffset OccurredAt,
    string? IdempotencyKey);

public sealed record PurchaseRequestActionCommand(
    Guid Id,
    byte[] ExpectedVersion,
    Guid ActorId,
    string? Reason,
    DateTimeOffset OccurredAt,
    string? IdempotencyKey);

public sealed record PurchaseRequestRecord(
    Guid Id,
    Guid TenantId,
    PurchaseRequestScope Scope,
    Guid RequesterId,
    PurchaseRequestStatus Status,
    string? Purpose,
    IReadOnlyList<PurchaseRequestLineSnapshot> Lines,
    PurchaseRequestApprovalPolicyDefinition? ApprovalPolicy,
    int CurrentApprovalStageIndex,
    int CurrentStageApprovalCount,
    IReadOnlyList<Guid> CurrentStageApproverIds,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? SubmittedAt,
    DateTimeOffset? ApprovedAt,
    DateTimeOffset? CancelledAt,
    byte[] Version);

public sealed record PurchaseRequestListRecord(
    Guid Id,
    Guid TenantId,
    PurchaseRequestScope Scope,
    Guid RequesterId,
    PurchaseRequestStatus Status,
    string? Purpose,
    int LineCount,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    byte[] Version);

public sealed record PurchaseRequestHistoryRecord(
    Guid EvidenceId,
    Guid PurchaseRequestId,
    DateTimeOffset OccurredAt,
    PurchaseRequestStatus FromStatus,
    PurchaseRequestStatus ToStatus,
    PurchaseRequestHistoryAction Action,
    Guid ActorId,
    string? Reason,
    string CorrelationId,
    string? PolicyId,
    int? PolicyVersion,
    string? StageKey,
    Guid? DelegatedFromActorId);

public sealed record PurchaseRequestAuditEvidence(
    Guid EvidenceId,
    Guid PurchaseRequestId,
    DateTimeOffset OccurredAt,
    string OperationId,
    string CorrelationId,
    Guid TenantId,
    Guid ActorId,
    Guid SessionId,
    string AuthorizationPath,
    string Decision,
    string? Reason,
    PurchaseRequestStatus? BeforeStatus,
    PurchaseRequestStatus? AfterStatus,
    Guid CompanyId,
    Guid? BranchId,
    string? BeforeSummary,
    string? AfterSummary,
    string? IdempotencyKey);

public sealed record PurchaseRequestAuditRecord(
    Guid EvidenceId,
    Guid PurchaseRequestId,
    DateTimeOffset OccurredAt,
    string OperationId,
    string CorrelationId,
    Guid TenantId,
    Guid ActorId,
    Guid SessionId,
    string AuthorizationPath,
    string Decision,
    string? Reason,
    PurchaseRequestStatus? BeforeStatus,
    PurchaseRequestStatus? AfterStatus,
    Guid CompanyId,
    Guid? BranchId,
    string? BeforeSummary,
    string? AfterSummary,
    string? IdempotencyKey);

public interface IPurchaseRequestPersistence
{
    Task<IReadOnlyList<PurchaseRequestListRecord>> ListAsync(
        TenantContext tenantContext,
        PurchaseRequestStatus? status,
        CancellationToken cancellationToken = default);

    Task<PurchaseRequestRecord?> FindAsync(
        TenantContext tenantContext,
        Guid purchaseRequestId,
        CancellationToken cancellationToken = default);

    Task<PurchaseRequestPersistenceResult<PurchaseRequestRecord>> CreateAsync(
        TenantContext tenantContext,
        PurchaseRequestCreateCommand command,
        PurchaseRequestAuditEvidence evidence,
        CancellationToken cancellationToken = default);

    Task<PurchaseRequestPersistenceResult<PurchaseRequestRecord>> EditAsync(
        TenantContext tenantContext,
        PurchaseRequestEditCommand command,
        PurchaseRequestAuditEvidence evidence,
        CancellationToken cancellationToken = default);

    Task<PurchaseRequestPersistenceResult<PurchaseRequestRecord>> SubmitAsync(
        TenantContext tenantContext,
        PurchaseRequestSubmitCommand command,
        PurchaseRequestAuditEvidence evidence,
        CancellationToken cancellationToken = default);

    Task<PurchaseRequestPersistenceResult<PurchaseRequestRecord>> ApproveAsync(
        TenantContext tenantContext,
        PurchaseRequestApprovalCommand command,
        PurchaseRequestAuditEvidence evidence,
        CancellationToken cancellationToken = default);

    Task<PurchaseRequestPersistenceResult<PurchaseRequestRecord>> RejectAsync(
        TenantContext tenantContext,
        PurchaseRequestActionCommand command,
        PurchaseRequestAuditEvidence evidence,
        CancellationToken cancellationToken = default);

    Task<PurchaseRequestPersistenceResult<PurchaseRequestRecord>> ReturnForChangeAsync(
        TenantContext tenantContext,
        PurchaseRequestActionCommand command,
        PurchaseRequestAuditEvidence evidence,
        CancellationToken cancellationToken = default);

    Task<PurchaseRequestPersistenceResult<PurchaseRequestRecord>> CancelAsync(
        TenantContext tenantContext,
        PurchaseRequestActionCommand command,
        PurchaseRequestAuditEvidence evidence,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PurchaseRequestHistoryRecord>> ReadHistoryAsync(
        TenantContext tenantContext,
        Guid purchaseRequestId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PurchaseRequestAuditRecord>> ReadAuditAsync(
        TenantContext tenantContext,
        Guid purchaseRequestId,
        CancellationToken cancellationToken = default);
}

public sealed class UnavailablePurchaseRequestPersistence : IPurchaseRequestPersistence
{
    private static Task<PurchaseRequestPersistenceResult<T>> Unavailable<T>() =>
        Task.FromResult(PurchaseRequestPersistenceResult<T>.Denied(
            PurchaseRequestPersistenceOutcome.Failure,
            "persistence_unavailable"));

    public Task<IReadOnlyList<PurchaseRequestListRecord>> ListAsync(TenantContext tenantContext, PurchaseRequestStatus? status, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<PurchaseRequestListRecord>>([]);

    public Task<PurchaseRequestRecord?> FindAsync(TenantContext tenantContext, Guid purchaseRequestId, CancellationToken cancellationToken = default) =>
        Task.FromResult<PurchaseRequestRecord?>(null);

    public Task<PurchaseRequestPersistenceResult<PurchaseRequestRecord>> CreateAsync(TenantContext tenantContext, PurchaseRequestCreateCommand command, PurchaseRequestAuditEvidence evidence, CancellationToken cancellationToken = default) => Unavailable<PurchaseRequestRecord>();
    public Task<PurchaseRequestPersistenceResult<PurchaseRequestRecord>> EditAsync(TenantContext tenantContext, PurchaseRequestEditCommand command, PurchaseRequestAuditEvidence evidence, CancellationToken cancellationToken = default) => Unavailable<PurchaseRequestRecord>();
    public Task<PurchaseRequestPersistenceResult<PurchaseRequestRecord>> SubmitAsync(TenantContext tenantContext, PurchaseRequestSubmitCommand command, PurchaseRequestAuditEvidence evidence, CancellationToken cancellationToken = default) => Unavailable<PurchaseRequestRecord>();
    public Task<PurchaseRequestPersistenceResult<PurchaseRequestRecord>> ApproveAsync(TenantContext tenantContext, PurchaseRequestApprovalCommand command, PurchaseRequestAuditEvidence evidence, CancellationToken cancellationToken = default) => Unavailable<PurchaseRequestRecord>();
    public Task<PurchaseRequestPersistenceResult<PurchaseRequestRecord>> RejectAsync(TenantContext tenantContext, PurchaseRequestActionCommand command, PurchaseRequestAuditEvidence evidence, CancellationToken cancellationToken = default) => Unavailable<PurchaseRequestRecord>();
    public Task<PurchaseRequestPersistenceResult<PurchaseRequestRecord>> ReturnForChangeAsync(TenantContext tenantContext, PurchaseRequestActionCommand command, PurchaseRequestAuditEvidence evidence, CancellationToken cancellationToken = default) => Unavailable<PurchaseRequestRecord>();
    public Task<PurchaseRequestPersistenceResult<PurchaseRequestRecord>> CancelAsync(TenantContext tenantContext, PurchaseRequestActionCommand command, PurchaseRequestAuditEvidence evidence, CancellationToken cancellationToken = default) => Unavailable<PurchaseRequestRecord>();

    public Task<IReadOnlyList<PurchaseRequestHistoryRecord>> ReadHistoryAsync(TenantContext tenantContext, Guid purchaseRequestId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<PurchaseRequestHistoryRecord>>([]);

    public Task<IReadOnlyList<PurchaseRequestAuditRecord>> ReadAuditAsync(TenantContext tenantContext, Guid purchaseRequestId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<PurchaseRequestAuditRecord>>([]);
}

public sealed record PurchaseRequestApprovalDelegation(
    Guid TenantId,
    Guid CompanyId,
    Guid? BranchId,
    string StageKey,
    Guid DelegatorId,
    Guid DelegateeId,
    DateTimeOffset StartsAt,
    DateTimeOffset ExpiresAt,
    string Reason);

public interface IPurchaseRequestApprovalPolicyProvider
{
    Task<PurchaseRequestApprovalPolicyDefinition?> ResolveAsync(
        ProcurementRequestContext context,
        PurchaseRequestScope scope,
        DateTimeOffset at,
        CancellationToken cancellationToken = default);
}

public interface IPurchaseRequestApprovalDelegationProvider
{
    Task<PurchaseRequestApprovalDelegation?> ResolveAsync(
        ProcurementRequestContext context,
        PurchaseRequestScope scope,
        PurchaseRequestApprovalStageDefinition stage,
        Guid actorId,
        DateTimeOffset at,
        CancellationToken cancellationToken = default);
}

public sealed record PurchaseRequestApprovalPolicyBinding(
    PurchaseRequestScope Scope,
    PurchaseRequestApprovalPolicyDefinition Definition);

public sealed class DefaultPurchaseRequestApprovalPolicyProvider : IPurchaseRequestApprovalPolicyProvider
{
    public Task<PurchaseRequestApprovalPolicyDefinition?> ResolveAsync(
        ProcurementRequestContext context,
        PurchaseRequestScope scope,
        DateTimeOffset at,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<PurchaseRequestApprovalPolicyDefinition?>(new(
            "procurement.purchase-request.default",
            1,
            [new PurchaseRequestApprovalStageDefinition("requester-manager", 1, 1, [], true)],
            AllowRequesterCancellationWhilePending: true,
            DateTimeOffset.MinValue,
            null));
}

public sealed class ConfiguredPurchaseRequestApprovalPolicyProvider : IPurchaseRequestApprovalPolicyProvider
{
    private readonly IReadOnlyList<PurchaseRequestApprovalPolicyBinding> bindings;

    public ConfiguredPurchaseRequestApprovalPolicyProvider(
        IEnumerable<PurchaseRequestApprovalPolicyBinding> bindings)
    {
        ArgumentNullException.ThrowIfNull(bindings);
        this.bindings = bindings.ToArray();
    }

    public Task<PurchaseRequestApprovalPolicyDefinition?> ResolveAsync(
        ProcurementRequestContext context,
        PurchaseRequestScope scope,
        DateTimeOffset at,
        CancellationToken cancellationToken = default)
    {
        var selected = bindings
            .Where(item => item.Scope.Matches(scope))
            .Where(item => item.Definition.EffectiveFrom <= at
                && (item.Definition.EffectiveTo is null || item.Definition.EffectiveTo > at))
            .OrderByDescending(item => item.Definition.Version)
            .ThenBy(item => item.Definition.PolicyId, StringComparer.Ordinal)
            .FirstOrDefault();
        return Task.FromResult(selected?.Definition);
    }
}

public sealed class NoPurchaseRequestApprovalDelegationProvider : IPurchaseRequestApprovalDelegationProvider
{
    public Task<PurchaseRequestApprovalDelegation?> ResolveAsync(
        ProcurementRequestContext context,
        PurchaseRequestScope scope,
        PurchaseRequestApprovalStageDefinition stage,
        Guid actorId,
        DateTimeOffset at,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<PurchaseRequestApprovalDelegation?>(null);
}

public sealed class ConfiguredPurchaseRequestApprovalDelegationProvider : IPurchaseRequestApprovalDelegationProvider
{
    private readonly IReadOnlyList<PurchaseRequestApprovalDelegation> delegations;

    public ConfiguredPurchaseRequestApprovalDelegationProvider(
        IEnumerable<PurchaseRequestApprovalDelegation> delegations)
    {
        ArgumentNullException.ThrowIfNull(delegations);
        this.delegations = delegations.ToArray();
    }

    public Task<PurchaseRequestApprovalDelegation?> ResolveAsync(
        ProcurementRequestContext context,
        PurchaseRequestScope scope,
        PurchaseRequestApprovalStageDefinition stage,
        Guid actorId,
        DateTimeOffset at,
        CancellationToken cancellationToken = default)
    {
        var eligible = (stage.EligibleApproverIds ?? []).ToHashSet();
        var selected = delegations
            .Where(item => item.TenantId == scope.TenantId
                && item.CompanyId == scope.CompanyId
                && item.BranchId == scope.BranchId
                && string.Equals(item.StageKey, stage.StageKey, StringComparison.Ordinal)
                && item.DelegateeId == actorId
                && item.DelegatorId != actorId
                && item.StartsAt <= at
                && item.ExpiresAt > at
                && (eligible.Count == 0 || eligible.Contains(item.DelegatorId)))
            .OrderBy(item => item.ExpiresAt)
            .ThenBy(item => item.DelegatorId)
            .FirstOrDefault();
        return Task.FromResult(selected);
    }
}

public sealed record PurchaseRequestAuthorizationResult(bool Allowed, string Code)
{
    public static PurchaseRequestAuthorizationResult Success() => new(true, "allowed");

    public static PurchaseRequestAuthorizationResult Denied(string code) => new(false, code);
}

public sealed class PurchaseRequestAuthorizationService
{
    public PurchaseRequestAuthorizationResult Authorize(
        ProcurementRequestContext context,
        string operationId,
        PurchaseRequestScope? targetScope = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentException.ThrowIfNullOrWhiteSpace(operationId);

        if (!FoundationOperationCatalog.TryGet(operationId, out var descriptor)
            || descriptor.Visibility != FoundationOperationVisibility.Public
            || descriptor.SecurityProfile != FoundationSecurityProfile.OrdinaryMembership)
        {
            return PurchaseRequestAuthorizationResult.Denied("authorization_operation_unmapped");
        }

        if (context.AuthorizationPath != TenantAuthorizationPath.OrdinaryMembership)
        {
            return PurchaseRequestAuthorizationResult.Denied("authorization_profile_denied");
        }

        if (!string.Equals(context.FoundationContext.Permission, descriptor.ExactPermissionCode, StringComparison.Ordinal))
        {
            return PurchaseRequestAuthorizationResult.Denied("permission_denied");
        }

        if (targetScope is not null)
        {
            if (targetScope.TenantId != context.TenantId.Value)
            {
                return PurchaseRequestAuthorizationResult.Denied("cross_tenant_target_denied");
            }

            if (!ScopeAllows(context.TrustedScope, targetScope))
            {
                return PurchaseRequestAuthorizationResult.Denied("resource_scope_denied");
            }
        }

        return PurchaseRequestAuthorizationResult.Success();
    }

    private static bool ScopeAllows(ScopeReference? trustedScope, PurchaseRequestScope target)
    {
        if (trustedScope is not { } scope)
        {
            return true;
        }

        var value = scope.Value;
        if (string.Equals(value, target.CanonicalReference, StringComparison.Ordinal))
        {
            return true;
        }

        if (string.Equals(value, $"Tenant:{target.TenantId:D}", StringComparison.Ordinal))
        {
            return true;
        }

        return target.BranchId is { }
            && string.Equals(value, $"Company:{target.CompanyId:D}", StringComparison.Ordinal);
    }
}

public static class PurchaseRequestValuePolicy
{
    public static bool TryNormalize(
        Guid companyId,
        Guid? branchId,
        string? purpose,
        IReadOnlyList<PurchaseRequestLineWriteRequest>? lines,
        out string? normalizedPurpose,
        out IReadOnlyList<PurchaseRequestLineWriteRequest> normalizedLines)
    {
        normalizedPurpose = null;
        normalizedLines = [];
        if (companyId == Guid.Empty || branchId == Guid.Empty || lines is null || lines.Count == 0)
        {
            return false;
        }

        if (!TryText(purpose, 2048, allowEmpty: true, out normalizedPurpose))
        {
            return false;
        }

        var output = new List<PurchaseRequestLineWriteRequest>(lines.Count);
        foreach (var line in lines)
        {
            if (line.ProductId == Guid.Empty
                || line.UnitOfMeasureId == Guid.Empty
                || line.Quantity <= 0
                || line.NeedByDate == DateOnly.MinValue
                || !TryText(line.Purpose, 2048, allowEmpty: false, out var linePurpose))
            {
                return false;
            }

            output.Add(line with { Purpose = linePurpose });
        }

        normalizedLines = output;
        return true;
    }

    public static bool TryText(string? value, int maximumLength, bool allowEmpty, out string? normalized)
    {
        normalized = value?.Trim();
        if (string.IsNullOrWhiteSpace(normalized))
        {
            normalized = null;
            return allowEmpty;
        }

        return normalized.Length <= maximumLength && !normalized.Any(char.IsControl);
    }

    public static bool IsValidPolicy(PurchaseRequestApprovalPolicyDefinition? policy)
    {
        if (policy is null
            || string.IsNullOrWhiteSpace(policy.PolicyId)
            || policy.Version < 1
            || policy.Stages is null
            || policy.Stages.Count == 0)
        {
            return false;
        }

        var ordered = policy.Stages.OrderBy(item => item.Sequence).ToArray();
        return ordered.Select(item => item.Sequence).Distinct().Count() == ordered.Length
            && ordered.All(item => !string.IsNullOrWhiteSpace(item.StageKey) && item.RequiredApprovals > 0)
            && !policy.Stages.Any(item => item.EligibleApproverIds?.Contains(Guid.Empty) == true);
    }
}

#pragma warning restore CS1591
