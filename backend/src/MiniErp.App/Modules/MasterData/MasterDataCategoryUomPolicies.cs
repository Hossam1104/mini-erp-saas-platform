#pragma warning disable CS1591

using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.Contracts.Modules.MasterData;

namespace MiniErp.App.Modules.MasterData;

/// <summary>
/// The production-owned business-scope policy for the Category/UOM slice.
/// The policy is explicit: a record is reusable throughout its owning Tenant,
/// while the caller still needs a trusted Tenant authorization path and the
/// operation-specific Permission.
/// </summary>
public sealed class CategoryUomScopePolicy : IMasterDataScopePolicy
{
    public const string PolicyId = "master-data.category-uom.scope";
    public const int PolicyVersion = 1;

    public MasterDataScopeDecision Evaluate(
        MasterDataRequestContext context,
        MasterDataResourceReference resource)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(resource);

        if (resource.ResourceKind is not (
            MasterDataResourceKind.ProductCategory
            or MasterDataResourceKind.UnitOfMeasure))
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

        return MasterDataScopeDecision.Success("category_uom_scope_allowed");
    }

    public static BusinessScope CreateScope(TenantId tenantId) => new(
        new TenantOwnership(tenantId.Value),
        organizationAnchor: null,
        new ScopePolicyReference(PolicyId, PolicyVersion));
}

/// <summary>
/// Resource policy that is intentionally limited to the two resources in
/// M95-SL-02. Later Master Data resources need their own policy.
/// </summary>
public sealed class CategoryUomResourcePolicy : IMasterDataResourcePolicy
{
    public MasterDataPolicyDecision Evaluate(MasterDataPolicyInput input)
    {
        ArgumentNullException.ThrowIfNull(input);

        return input.Resource.ResourceKind is
            MasterDataResourceKind.ProductCategory or MasterDataResourceKind.UnitOfMeasure
            ? MasterDataPolicyDecision.Allowed("category_uom_resource_allowed")
            : MasterDataPolicyDecision.Denied("resource_policy_not_configured");
    }
}

/// <summary>
/// M99's affected-slice approval disposition. Routine Category/UOM lifecycle
/// operations do not require a separate approver; all other resource/policy
/// combinations retain the generic fail-closed behavior.
/// </summary>
public sealed class CategoryUomApprovalPolicy : IMasterDataApprovalPolicy
{
    private readonly DefaultMasterDataApprovalPolicy fallback = new();

    public MasterDataApprovalPolicyResult Evaluate(
        MasterDataRequestContext context,
        MasterDataResourceReference resource,
        MasterDataOperation operation)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(resource);

        if (resource.ResourceKind is (
            MasterDataResourceKind.ProductCategory
            or MasterDataResourceKind.UnitOfMeasure)
            && operation is (
                MasterDataOperation.View
                or MasterDataOperation.ViewAuditHistory
                or MasterDataOperation.Create
                or MasterDataOperation.Edit
                or MasterDataOperation.Activate
                or MasterDataOperation.Deactivate
                or MasterDataOperation.Reactivate))
        {
            return new MasterDataApprovalPolicyResult(MasterDataApprovalPolicyStatus.NotApplicable);
        }

        return fallback.Evaluate(context, resource, operation);
    }
}

public sealed record MasterDataHierarchyValidationResult(bool Valid, string Code)
{
    public static MasterDataHierarchyValidationResult Success() => new(true, "valid");

    public static MasterDataHierarchyValidationResult Denied(string code) => new(false, code);
}

/// <summary>
/// Configuration-led Category hierarchy validation. The default M99 policy is
/// three levels; changing the policy does not change the persistence shape.
/// </summary>
public sealed class MasterDataCategoryHierarchyPolicy
{
    public MasterDataCategoryHierarchyPolicy(int maximumDepth = 3)
    {
        if (maximumDepth < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(maximumDepth));
        }

        MaximumDepth = maximumDepth;
    }

    public int MaximumDepth { get; }

    public MasterDataHierarchyValidationResult Validate(
        Guid categoryId,
        Guid? parentCategoryId,
        IReadOnlyDictionary<Guid, Guid?> parentByCategoryId)
    {
        ArgumentNullException.ThrowIfNull(parentByCategoryId);

        if (categoryId == Guid.Empty)
        {
            return MasterDataHierarchyValidationResult.Denied("category_id_invalid");
        }

        if (parentCategoryId is null)
        {
            return MasterDataHierarchyValidationResult.Success();
        }

        if (parentCategoryId == Guid.Empty)
        {
            return MasterDataHierarchyValidationResult.Denied("parent_category_invalid");
        }

        var visited = new HashSet<Guid>();
        var current = parentCategoryId;
        var depth = 1;
        while (current is { } currentId)
        {
            if (currentId == categoryId || !visited.Add(currentId))
            {
                return MasterDataHierarchyValidationResult.Denied("category_parent_cycle");
            }

            if (!parentByCategoryId.TryGetValue(currentId, out current))
            {
                return MasterDataHierarchyValidationResult.Denied("parent_category_not_found");
            }

            depth++;
            if (depth > MaximumDepth)
            {
                return MasterDataHierarchyValidationResult.Denied("category_depth_exceeded");
            }
        }

        return MasterDataHierarchyValidationResult.Success();
    }
}

public static class MasterDataCategoryUomValuePolicy
{
    public const int QuantityPrecision = 6;
    public const int ConversionPrecision = 8;

    public static string NormalizeCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("A business code is required.", nameof(code));
        }

        var normalized = code.Trim();
        if (normalized.Length > 128 || normalized.Any(char.IsControl))
        {
            throw new ArgumentException("The business code is outside the approved bound.", nameof(code));
        }

        return normalized;
    }

    public static string NameKey(LocalizedName name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return (name.English ?? name.Arabic ?? string.Empty).ToUpperInvariant();
    }

    public static void ValidateConversionFactor(decimal factor)
    {
        if (factor <= 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(factor), "A conversion factor must be positive and non-zero.");
        }

        if (GetScale(factor) > ConversionPrecision)
        {
            throw new ArgumentException("The conversion factor exceeds the approved precision.", nameof(factor));
        }
    }

    public static void ValidateQuantity(decimal quantity)
    {
        if (GetScale(quantity) > QuantityPrecision)
        {
            throw new ArgumentException("The quantity exceeds the approved precision.", nameof(quantity));
        }
    }

    public static decimal Calculate(decimal quantity, decimal factor)
    {
        ValidateQuantity(quantity);
        ValidateConversionFactor(factor);
        return Math.Round(
            quantity * factor,
            QuantityPrecision,
            MidpointRounding.AwayFromZero);
    }

    private static int GetScale(decimal value) =>
        (decimal.GetBits(value)[3] >> 16) & 0x7F;
}

#pragma warning restore CS1591
