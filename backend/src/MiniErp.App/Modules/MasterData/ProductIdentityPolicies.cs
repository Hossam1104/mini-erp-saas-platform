#pragma warning disable CS1591

using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.Contracts.Modules.MasterData;

namespace MiniErp.App.Modules.MasterData;

/// <summary>
/// Product's independently owned Tenant-wide resource scope. This policy is
/// deliberately not shared with Category/UOM.
/// </summary>
public sealed class ProductScopePolicy : IMasterDataScopePolicy
{
    public const string PolicyId = "master-data.product.scope";
    public const int PolicyVersion = 1;

    public MasterDataScopeDecision Evaluate(
        MasterDataRequestContext context,
        MasterDataResourceReference resource)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(resource);

        if (resource.ResourceKind != MasterDataResourceKind.Product)
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

        return MasterDataScopeDecision.Success("product_scope_allowed");
    }

    public static BusinessScope CreateScope(TenantId tenantId) => new(
        new TenantOwnership(tenantId.Value),
        organizationAnchor: null,
        new ScopePolicyReference(PolicyId, PolicyVersion));
}

public sealed class ProductResourcePolicy : IMasterDataResourcePolicy
{
    public MasterDataPolicyDecision Evaluate(MasterDataPolicyInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        return input.Resource.ResourceKind == MasterDataResourceKind.Product
            ? MasterDataPolicyDecision.Allowed("product_resource_allowed")
            : MasterDataPolicyDecision.Denied("resource_policy_not_configured");
    }
}

public sealed class ProductApprovalPolicy : IMasterDataApprovalPolicy
{
    private readonly DefaultMasterDataApprovalPolicy fallback = new();

    public MasterDataApprovalPolicyResult Evaluate(
        MasterDataRequestContext context,
        MasterDataResourceReference resource,
        MasterDataOperation operation)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(resource);

        if (resource.ResourceKind == MasterDataResourceKind.Product
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

public sealed class ProductResourceAuthorizationService
{
    private readonly IMasterDataCapabilityResolver capabilityResolver;
    private readonly IMasterDataResourcePolicy resourcePolicy;
    private readonly IMasterDataApprovalPolicy approvalPolicy;
    private readonly IMasterDataScopePolicy scopePolicy;

    public ProductResourceAuthorizationService(
        IMasterDataCapabilityResolver capabilityResolver,
        ProductResourcePolicy resourcePolicy,
        ProductApprovalPolicy approvalPolicy,
        ProductScopePolicy scopePolicy)
        : this(
            capabilityResolver,
            (IMasterDataResourcePolicy)resourcePolicy,
            (IMasterDataApprovalPolicy)approvalPolicy,
            (IMasterDataScopePolicy)scopePolicy)
    {
    }

    internal ProductResourceAuthorizationService(
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

public static class ProductIdentityValuePolicy
{
    public const int MaximumIdentifierLength = 128;
    public const int MaximumDescriptionLength = 2048;

    public static string NormalizeSku(string sku)
    {
        var normalized = NormalizeIdentifier(sku, nameof(sku));
        return normalized;
    }

    public static string NormalizeBarcode(string barcode)
    {
        var normalized = NormalizeIdentifier(barcode, nameof(barcode));
        return normalized;
    }

    public static string ComparisonKey(string value) =>
        NormalizeIdentifier(value, nameof(value)).ToUpperInvariant();

    public static IReadOnlyList<string> NormalizeBarcodes(IReadOnlyList<string>? barcodes)
    {
        ArgumentNullException.ThrowIfNull(barcodes);
        var normalized = barcodes.Select(NormalizeBarcode).ToArray();
        var duplicate = normalized
            .GroupBy(ComparisonKey, StringComparer.Ordinal)
            .Any(group => group.Count() > 1);
        if (duplicate)
        {
            throw new ArgumentException("Barcode values must be unique within one Product.", nameof(barcodes));
        }

        return normalized;
    }

    public static string? NormalizeDescription(string? description)
    {
        if (description is null)
        {
            return null;
        }

        var normalized = description.Trim();
        if (normalized.Length == 0)
        {
            return null;
        }

        if (normalized.Length > MaximumDescriptionLength || normalized.Any(char.IsControl))
        {
            throw new ArgumentException("The description is outside the approved bound.", nameof(description));
        }

        return normalized;
    }

    public static void ValidateName(LocalizedName name)
    {
        ArgumentNullException.ThrowIfNull(name);
        if (string.IsNullOrWhiteSpace(name.English))
        {
            throw new ArgumentException("Product English name is required.", nameof(name));
        }
    }

    private static string NormalizeIdentifier(string value, string parameterName)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A non-empty identifier is required.", parameterName);
        }

        var normalized = value.Trim();
        if (normalized.Length > MaximumIdentifierLength || normalized.Any(char.IsControl))
        {
            throw new ArgumentException("The identifier is outside the approved bound.", parameterName);
        }

        return normalized;
    }
}

#pragma warning restore CS1591
