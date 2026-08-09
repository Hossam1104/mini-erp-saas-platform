#pragma warning disable CS1591

using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.Modules.MasterData;
using MiniErp.Contracts.Modules.MasterData;

namespace MiniErp.App.Modules.BusinessParties;

/// <summary>
/// Supplier's independently owned Tenant-wide resource scope. Supplier
/// records are reusable by the owning Tenant's Companies and Branches; no
/// Company/Branch anchor is accepted as authority for this slice.
/// </summary>
public sealed class SupplierScopePolicy : IMasterDataScopePolicy
{
    public const string PolicyId = "business-parties.supplier.scope";
    public const int PolicyVersion = 1;

    public MasterDataScopeDecision Evaluate(
        MasterDataRequestContext context,
        MasterDataResourceReference resource)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(resource);

        if (resource.ResourceKind != MasterDataResourceKind.Supplier)
        {
            return MasterDataScopeDecision.Denied("resource_scope_denied");
        }

        if (resource.Tenant.TenantId != context.TenantId.Value)
        {
            return MasterDataScopeDecision.Denied("cross_tenant_target_denied");
        }

        if (resource.Scope is not { } scope
            || scope.OrganizationAnchor is not null
            || !string.Equals(scope.Policy.PolicyId, PolicyId, StringComparison.Ordinal)
            || scope.Policy.Version != PolicyVersion)
        {
            return MasterDataScopeDecision.Denied("resource_scope_denied");
        }

        return MasterDataScopeDecision.Success("supplier_scope_allowed");
    }

    public static BusinessScope CreateScope(TenantId tenantId) => new(
        new TenantOwnership(tenantId.Value),
        organizationAnchor: null,
        new ScopePolicyReference(PolicyId, PolicyVersion));
}

public sealed class SupplierResourcePolicy : IMasterDataResourcePolicy
{
    public MasterDataPolicyDecision Evaluate(MasterDataPolicyInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        return input.Resource.ResourceKind == MasterDataResourceKind.Supplier
            ? MasterDataPolicyDecision.Allowed("supplier_resource_allowed")
            : MasterDataPolicyDecision.Denied("resource_policy_not_configured");
    }
}

public sealed class SupplierApprovalPolicy : IMasterDataApprovalPolicy
{
    private readonly DefaultMasterDataApprovalPolicy fallback = new();

    public MasterDataApprovalPolicyResult Evaluate(
        MasterDataRequestContext context,
        MasterDataResourceReference resource,
        MasterDataOperation operation)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(resource);

        if (resource.ResourceKind == MasterDataResourceKind.Supplier
            && operation is MasterDataOperation.View
                or MasterDataOperation.ViewAuditHistory
                or MasterDataOperation.Create
                or MasterDataOperation.Edit
                or MasterDataOperation.Activate
                or MasterDataOperation.Deactivate
                or MasterDataOperation.Reactivate)
        {
            return new MasterDataApprovalPolicyResult(MasterDataApprovalPolicyStatus.NotApplicable);
        }

        return fallback.Evaluate(context, resource, operation);
    }
}

/// <summary>
/// Supplier-owned authorization composition. It is intentionally separate
/// from Product and Category/UOM policy instances so resource/scope decisions
/// cannot be silently borrowed across module slices.
/// </summary>
public sealed class SupplierResourceAuthorizationService
{
    private readonly IMasterDataCapabilityResolver capabilityResolver;
    private readonly IMasterDataResourcePolicy resourcePolicy;
    private readonly IMasterDataApprovalPolicy approvalPolicy;
    private readonly IMasterDataScopePolicy scopePolicy;

    public SupplierResourceAuthorizationService(
        IMasterDataCapabilityResolver capabilityResolver,
        SupplierResourcePolicy resourcePolicy,
        SupplierApprovalPolicy approvalPolicy,
        SupplierScopePolicy scopePolicy)
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

        var approvalFailure = approval.Status switch
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
}

public static class SupplierValuePolicy
{
    public const int MaximumIdentifierLength = 128;
    public const int MaximumContactNameLength = 256;
    public const int MaximumContactValueLength = 256;

    public static string NormalizeCode(string code) =>
        NormalizeRequired(code, MaximumIdentifierLength, nameof(code));

    public static string ComparisonKey(string value) =>
        NormalizeCode(value).ToUpperInvariant();

    public static string? NormalizeRegistrationReference(string? value) =>
        NormalizeOptional(value, MaximumIdentifierLength, nameof(value));

    public static void ValidateName(LocalizedName name, string parameterName)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (string.IsNullOrWhiteSpace(name.English))
        {
            throw new ArgumentException("Supplier English legal/trading name is required.", parameterName);
        }
    }

    public static LocalizedName? NormalizeTradingName(LocalizedName? name)
    {
        if (name is null)
        {
            return null;
        }

        ValidateName(name, nameof(name));
        return name;
    }

    public static IReadOnlyList<SupplierContactCommand> NormalizeContacts(
        IReadOnlyList<SupplierContactCommand>? contacts)
    {
        ArgumentNullException.ThrowIfNull(contacts);
        if (contacts.Count > 50)
        {
            throw new ArgumentException("A Supplier cannot have more than 50 contacts.", nameof(contacts));
        }

        return contacts.Select(contact =>
            {
                ArgumentNullException.ThrowIfNull(contact);
                return new SupplierContactCommand(
                    NormalizeRequired(contact.Name, MaximumContactNameLength, nameof(contact.Name)),
                    NormalizeOptional(contact.Email, MaximumContactValueLength, nameof(contact.Email)),
                    NormalizeOptional(contact.Phone, MaximumContactValueLength, nameof(contact.Phone)));
            })
            .ToArray();
    }

    public static string? NameKey(string? value) =>
        NormalizeOptional(value, 256, nameof(value))?.ToUpperInvariant();

    private static string NormalizeRequired(string value, int maximumLength, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A non-empty value is required.", parameterName);
        }

        var normalized = value.Trim();
        if (normalized.Length > maximumLength || normalized.Any(char.IsControl))
        {
            throw new ArgumentException("The value is outside the approved bound.", parameterName);
        }

        return normalized;
    }

    private static string? NormalizeOptional(string? value, int maximumLength, string parameterName)
    {
        if (value is null)
        {
            return null;
        }

        var normalized = value.Trim();
        if (normalized.Length == 0)
        {
            return null;
        }

        if (normalized.Length > maximumLength || normalized.Any(char.IsControl))
        {
            throw new ArgumentException("The value is outside the approved bound.", parameterName);
        }

        return normalized;
    }
}

#pragma warning restore CS1591
