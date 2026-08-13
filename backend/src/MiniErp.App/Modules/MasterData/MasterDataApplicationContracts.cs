#pragma warning disable CS1591

using MiniErp.App.BuildingBlocks.Rest;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.Modules.Audit;
using MiniErp.Contracts.Modules.Audit;
using MiniErp.Contracts.Modules.Foundation;
using MiniErp.Contracts.Modules.MasterData;

namespace MiniErp.App.Modules.MasterData;

public sealed record MasterDataContextResolution(
    bool Allowed,
    string Code,
    MasterDataRequestContext? Context)
{
    public static MasterDataContextResolution Success(MasterDataRequestContext context) =>
        new(true, "resolved", context);

    public static MasterDataContextResolution Denied(string code) =>
        new(false, code, null);
}

public sealed class MasterDataRequestContext
{
    private MasterDataRequestContext(
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
        ?? throw new InvalidOperationException("A Master Data context must carry a trusted Tenant context.");

    internal static MasterDataRequestContext FromFoundationContext(
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

        return new MasterDataRequestContext(
            tenant.TenantId,
            tenant.AuthorizationPath,
            tenant.Scope,
            actorId,
            sessionId,
            tenant.CorrelationId,
            foundationContext);
    }
}

public sealed class MasterDataTenantContextResolver
{
    public MasterDataContextResolution Resolve(
        FoundationRequestContext trustedContext,
        MasterDataScopeSelection? selection = null)
    {
        ArgumentNullException.ThrowIfNull(trustedContext);

        MasterDataRequestContext context;
        try
        {
            context = MasterDataRequestContext.FromFoundationContext(trustedContext);
        }
        catch (ArgumentException)
        {
            return MasterDataContextResolution.Denied("tenant_context_failed");
        }

        if (selection?.RequestedTenantId is { } requestedTenantId
            && requestedTenantId != context.TenantId.Value)
        {
            return MasterDataContextResolution.Denied("cross_tenant_target_denied");
        }

        if (selection?.RequestedScope is { } requestedScope)
        {
            if (requestedScope.Tenant.TenantId != context.TenantId.Value)
            {
                return MasterDataContextResolution.Denied("cross_tenant_target_denied");
            }

            if (requestedScope.OrganizationAnchor is not { } requestedAnchor
                || context.TrustedScope is not { } trustedScope
                || !string.Equals(
                    requestedAnchor.CanonicalValue,
                    trustedScope.Value,
                    StringComparison.Ordinal))
            {
                return MasterDataContextResolution.Denied("resource_scope_denied");
            }
        }

        // A supplied selection is only an optional target hint. The context
        // returned below remains the trusted server-derived authority, so an
        // empty or tenant-only hint cannot broaden or replace its scope.
        return MasterDataContextResolution.Success(context);
    }
}

public sealed class MasterDataPolicyInput
{
    internal MasterDataPolicyInput(
        MasterDataRequestContext context,
        MasterDataResourceReference resource,
        MasterDataOperation operation,
        MasterDataCapability capability,
        MasterDataApprovalPolicyResult approval)
    {
        Context = context;
        Tenant = new TenantOwnership(context.TenantId.Value);
        AuthorizationPath = context.AuthorizationPath;
        TrustedScope = context.TrustedScope;
        ActorId = context.ActorId;
        SessionId = context.SessionId;
        CorrelationId = context.CorrelationId;
        Resource = resource;
        Operation = operation;
        Capability = capability;
        Approval = approval;
    }

    internal MasterDataRequestContext Context { get; }

    public TenantOwnership Tenant { get; }

    public TenantAuthorizationPath AuthorizationPath { get; }

    public ScopeReference? TrustedScope { get; }

    public Guid ActorId { get; }

    public Guid SessionId { get; }

    public CorrelationId? CorrelationId { get; }

    public MasterDataResourceReference Resource { get; }

    public MasterDataOperation Operation { get; }

    public MasterDataCapability Capability { get; }

    public MasterDataApprovalPolicyResult Approval { get; }
}

public interface IMasterDataCapabilityResolver
{
    IReadOnlySet<MasterDataCapability> Resolve(MasterDataRequestContext context);
}

public sealed class DenyAllMasterDataCapabilityResolver : IMasterDataCapabilityResolver
{
    public IReadOnlySet<MasterDataCapability> Resolve(MasterDataRequestContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return new HashSet<MasterDataCapability>();
    }
}

public sealed class GrantingMasterDataCapabilityResolver : IMasterDataCapabilityResolver
{
    private static readonly HashSet<MasterDataCapability> AllCapabilities = System.Enum.GetValues<MasterDataCapability>().ToHashSet();

    public IReadOnlySet<MasterDataCapability> Resolve(MasterDataRequestContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        return context.AuthorizationPath == TenantAuthorizationPath.OrdinaryMembership
            ? AllCapabilities
            : new HashSet<MasterDataCapability>();
    }
}

/// <summary>
/// Production adapter from the trusted Foundation operation permission to the
/// single Master Data capability required by that operation. It deliberately
/// does not inspect client headers, roles, query values, or request bodies.
/// </summary>
public sealed class FoundationPermissionMasterDataCapabilityResolver : IMasterDataCapabilityResolver
{
    public IReadOnlySet<MasterDataCapability> Resolve(MasterDataRequestContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        return MasterDataOperationCatalog.TryGetCapabilityForPermission(
            context.FoundationContext.Permission,
            out var capability)
            ? new HashSet<MasterDataCapability> { capability }
            : new HashSet<MasterDataCapability>();
    }
}

public interface IMasterDataApprovalPolicy
{
    MasterDataApprovalPolicyResult Evaluate(
        MasterDataRequestContext context,
        MasterDataResourceReference resource,
        MasterDataOperation operation);
}

public sealed class DefaultMasterDataApprovalPolicy : IMasterDataApprovalPolicy
{
    public MasterDataApprovalPolicyResult Evaluate(
        MasterDataRequestContext context,
        MasterDataResourceReference resource,
        MasterDataOperation operation)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(resource);
        return operation is MasterDataOperation.View or MasterDataOperation.ViewAuditHistory
            ? new MasterDataApprovalPolicyResult(MasterDataApprovalPolicyStatus.NotApplicable)
            : new MasterDataApprovalPolicyResult(MasterDataApprovalPolicyStatus.NotConfigured);
    }
}

public interface IMasterDataResourcePolicy
{
    MasterDataPolicyDecision Evaluate(MasterDataPolicyInput input);
}

public sealed record MasterDataScopeDecision(bool Allowed, string Code)
{
    public static MasterDataScopeDecision Denied(string code = "scope_policy_not_configured") =>
        new(false, code);

    public static MasterDataScopeDecision Success(string code = "scope_allowed") =>
        new(true, code);
}

public interface IMasterDataScopePolicy
{
    MasterDataScopeDecision Evaluate(
        MasterDataRequestContext context,
        MasterDataResourceReference resource);
}

public sealed class DenyAllMasterDataScopePolicy : IMasterDataScopePolicy
{
    public MasterDataScopeDecision Evaluate(
        MasterDataRequestContext context,
        MasterDataResourceReference resource)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(resource);
        return MasterDataScopeDecision.Denied();
    }
}

public sealed class DenyAllMasterDataResourcePolicy : IMasterDataResourcePolicy
{
    public MasterDataPolicyDecision Evaluate(MasterDataPolicyInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        return MasterDataPolicyDecision.Denied("resource_policy_not_configured");
    }
}

public sealed record MasterDataAuthorizationResult(
    bool Allowed,
    string Code,
    MasterDataPolicyDecision Decision,
    MasterDataApprovalPolicyResult Approval)
{
    public static MasterDataAuthorizationResult Denied(
        string code,
        MasterDataApprovalPolicyResult? approval = null,
        MasterDataPolicyDecision? decision = null) => new(
        false,
        code,
        decision ?? MasterDataPolicyDecision.Denied(code),
        approval ?? new MasterDataApprovalPolicyResult(MasterDataApprovalPolicyStatus.NotApplicable));

    public static MasterDataAuthorizationResult Success(
        MasterDataPolicyDecision decision,
        MasterDataApprovalPolicyResult approval) => new(
        true,
        "allowed",
        decision,
        approval);
}

public sealed class MasterDataResourceAuthorizationService
{
    private readonly IMasterDataCapabilityResolver capabilityResolver;
    private readonly IMasterDataResourcePolicy resourcePolicy;
    private readonly IMasterDataApprovalPolicy approvalPolicy;
    private readonly IMasterDataScopePolicy scopePolicy;

    public MasterDataResourceAuthorizationService(
        IMasterDataCapabilityResolver capabilityResolver,
        IMasterDataResourcePolicy resourcePolicy,
        IMasterDataApprovalPolicy approvalPolicy,
        IMasterDataScopePolicy scopePolicy)
    {
        this.capabilityResolver = capabilityResolver ?? throw new ArgumentNullException(nameof(capabilityResolver));
        this.resourcePolicy = resourcePolicy ?? throw new ArgumentNullException(nameof(resourcePolicy));
        this.approvalPolicy = approvalPolicy ?? throw new ArgumentNullException(nameof(approvalPolicy));
        this.scopePolicy = scopePolicy ?? throw new ArgumentNullException(nameof(scopePolicy));
    }

    public MasterDataAuthorizationResult Authorize(
        MasterDataRequestContext context,
        MasterDataResourceReference resource,
        MasterDataOperation operation)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(resource);

        if (!MasterDataOperationCatalog.TryGetRequiredCapability(operation, out var requiredCapability))
        {
            return MasterDataAuthorizationResult.Denied("authorization_operation_unmapped");
        }

        if (resource.Tenant.TenantId != context.TenantId.Value)
        {
            return MasterDataAuthorizationResult.Denied("cross_tenant_target_denied");
        }

        MasterDataScopeDecision? scopeDecision;
        try
        {
            scopeDecision = scopePolicy.Evaluate(context, resource);
        }
        catch
        {
            return MasterDataAuthorizationResult.Denied("scope_policy_unavailable");
        }

        if (scopeDecision is null || !scopeDecision.Allowed)
        {
            return MasterDataAuthorizationResult.Denied(scopeDecision?.Code ?? "scope_policy_unavailable");
        }

        IReadOnlySet<MasterDataCapability>? capabilities;
        try
        {
            capabilities = capabilityResolver.Resolve(context);
        }
        catch
        {
            return MasterDataAuthorizationResult.Denied("permission_unavailable");
        }

        if (capabilities is null || !capabilities.Contains(requiredCapability))
        {
            return MasterDataAuthorizationResult.Denied("permission_denied");
        }

        MasterDataApprovalPolicyResult? approval;
        try
        {
            approval = approvalPolicy.Evaluate(context, resource, operation);
        }
        catch
        {
            return MasterDataAuthorizationResult.Denied("approval_policy_unavailable");
        }

        if (approval is null)
        {
            return MasterDataAuthorizationResult.Denied("approval_policy_unavailable");
        }

        var approvalFailure = ValidateApproval(context, approval);
        if (approvalFailure is not null)
        {
            return MasterDataAuthorizationResult.Denied(approvalFailure, approval);
        }

        MasterDataPolicyDecision? decision;
        try
        {
            decision = resourcePolicy.Evaluate(new MasterDataPolicyInput(
                context,
                resource,
                operation,
                requiredCapability,
                approval));
        }
        catch
        {
            return MasterDataAuthorizationResult.Denied("resource_policy_unavailable", approval);
        }

        if (decision is null)
        {
            return MasterDataAuthorizationResult.Denied("resource_policy_unavailable", approval);
        }

        return decision.IsAllowed
            ? MasterDataAuthorizationResult.Success(decision, approval)
            : MasterDataAuthorizationResult.Denied(decision.Code, approval, decision);
    }

    private static string? ValidateApproval(
        MasterDataRequestContext context,
        MasterDataApprovalPolicyResult approval) => approval.Status switch
        {
            MasterDataApprovalPolicyStatus.NotApplicable => null,
            MasterDataApprovalPolicyStatus.NotConfigured => "approval_policy_not_configured",
            MasterDataApprovalPolicyStatus.RequiresApproval => "approval_required",
            MasterDataApprovalPolicyStatus.Pending => "approval_pending",
            MasterDataApprovalPolicyStatus.Rejected => "approval_rejected",
            MasterDataApprovalPolicyStatus.Approved when approval.ApproverId == context.ActorId => "self_approval_denied",
            MasterDataApprovalPolicyStatus.Approved when approval.ApproverId is null => "approval_identity_missing",
            MasterDataApprovalPolicyStatus.Approved => null,
            _ => "approval_policy_invalid"
        };
}

public static class MasterDataAuditEvidenceFactory
{
    public static MasterDataAuditEvidence Create(
        MasterDataRequestContext context,
        MasterDataResourceReference resource,
        MasterDataOperation operation,
        MasterDataPolicyDecision decision,
        FoundationAuditReason reason,
        string? beforeSummary = null,
        string? afterSummary = null,
        Guid? approverId = null,
        DateTimeOffset? occurredAt = null)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(resource);
        ArgumentNullException.ThrowIfNull(decision);

        if (!Enum.IsDefined(operation) || !Enum.IsDefined(reason))
        {
            throw new ArgumentOutOfRangeException(nameof(operation));
        }

        if (resource.Tenant.TenantId != context.TenantId.Value)
        {
            throw new ArgumentException("Audit evidence cannot identify a foreign Tenant target.", nameof(resource));
        }

        if (approverId == context.ActorId)
        {
            throw new ArgumentException("Audit evidence cannot endorse self-approval.", nameof(approverId));
        }

        var correlationId = context.CorrelationId?.Value
            ?? throw new ArgumentException("Audit evidence requires a trusted correlation identifier.", nameof(context));
        var authorizationPath = context.AuthorizationPath switch
        {
            TenantAuthorizationPath.OrdinaryMembership => FoundationAuditAuthorizationPath.OrdinaryMembership,
            TenantAuthorizationPath.SupportGrant => FoundationAuditAuthorizationPath.SupportGrant,
            _ => throw new ArgumentException("Audit evidence requires a Tenant authorization path.", nameof(context))
        };

        return MasterDataAuditEvidence.CreateValidated(
            Guid.NewGuid(),
            occurredAt ?? DateTimeOffset.UtcNow,
            $"master-data.{resource.ResourceKind}.{operation}",
            correlationId,
            context.ActorId,
            context.SessionId,
            authorizationPath,
            new TenantOwnership(context.TenantId.Value),
            resource.Scope,
            operation,
            resource.ResourceKind,
            resource.StableId,
            resource.BusinessCode,
            decision.Outcome,
            decision.IsAllowed ? FoundationAuditDecision.Allowed : FoundationAuditDecision.Denied,
            reason,
            beforeSummary,
            afterSummary,
            approverId);
    }
}

public sealed class MasterDataAuditCoordinator
{
    private readonly IMasterDataAuditEvidenceSink evidenceSink;

    public MasterDataAuditCoordinator(IMasterDataAuditEvidenceSink evidenceSink)
    {
        this.evidenceSink = evidenceSink ?? throw new ArgumentNullException(nameof(evidenceSink));
    }

    public async ValueTask<MasterDataAuditAppendResult> RecordAsync(
        MasterDataRequestContext context,
        MasterDataResourceReference resource,
        MasterDataOperation operation,
        MasterDataPolicyDecision decision,
        FoundationAuditReason reason,
        string? beforeSummary = null,
        string? afterSummary = null,
        Guid? approverId = null,
        DateTimeOffset? occurredAt = null,
        CancellationToken cancellationToken = default)
    {
        MasterDataAuditEvidence evidence;
        try
        {
            evidence = MasterDataAuditEvidenceFactory.Create(
                context,
                resource,
                operation,
                decision,
                reason,
                beforeSummary,
                afterSummary,
                approverId,
                occurredAt);
        }
        catch
        {
            return MasterDataAuditAppendResult.Failure(Guid.Empty, "audit_evidence_invalid");
        }

        try
        {
            return await evidenceSink.AppendAsync(evidence, cancellationToken);
        }
        catch
        {
            return MasterDataAuditAppendResult.Failure(evidence.EvidenceId, "audit_evidence_unavailable");
        }
    }
}

public sealed class FoundationMasterDataAuditEvidenceSink : IMasterDataAuditEvidenceSink
{
    private readonly MasterDataRequestContext context;
    private readonly IFoundationAuditEvidenceSink foundationSink;

    public FoundationMasterDataAuditEvidenceSink(
        MasterDataRequestContext context,
        IFoundationAuditEvidenceSink foundationSink)
    {
        this.context = context ?? throw new ArgumentNullException(nameof(context));
        this.foundationSink = foundationSink ?? throw new ArgumentNullException(nameof(foundationSink));
    }

    public async ValueTask<MasterDataAuditAppendResult> AppendAsync(
        MasterDataAuditEvidence evidence,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(evidence);

        if (evidence.ActorId != context.ActorId
            || evidence.SessionId != context.SessionId
            || evidence.Tenant.TenantId != context.TenantId.Value)
        {
            return MasterDataAuditAppendResult.Failure(evidence.EvidenceId, "audit_context_mismatch");
        }

        try
        {
            var foundationEvidence = FoundationAuditEvidenceFactory.Create(
                context.FoundationContext,
                evidence.OperationId,
                evidence.CorrelationId,
                evidence.Decision,
                evidence.Reason,
                operationVersion: "master-data.contract.v1",
                supportPurpose: evidence.AuthorizationPath == FoundationAuditAuthorizationPath.SupportGrant
                    ? "master-data"
                    : null);
            await foundationSink.AppendAsync(foundationEvidence, cancellationToken);
            return MasterDataAuditAppendResult.Success(evidence.EvidenceId);
        }
        catch
        {
            return MasterDataAuditAppendResult.Failure(evidence.EvidenceId, "audit_evidence_unavailable");
        }
    }
}

#pragma warning restore CS1591
