#pragma warning disable CS1591

using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.Contracts.Modules.MasterData;

namespace MiniErp.App.Modules.MasterData;

public sealed class PriceListScopePolicy : IMasterDataScopePolicy
{
    public const string PolicyId = "master-data.price-list.scope";
    public const int PolicyVersion = 1;

    public MasterDataScopeDecision Evaluate(
        MasterDataRequestContext context,
        MasterDataResourceReference resource)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(resource);

        if (resource.ResourceKind != MasterDataResourceKind.PriceList
            || resource.Tenant.TenantId != context.TenantId.Value
            || resource.Scope is not { } scope
            || !string.Equals(scope.Policy.PolicyId, PolicyId, StringComparison.Ordinal)
            || scope.Policy.Version != PolicyVersion)
        {
            return MasterDataScopeDecision.Denied("resource_scope_denied");
        }

        if (scope.OrganizationAnchor is { } anchor)
        {
            if (anchor.Kind is not (OrganizationScopeKind.Company or OrganizationScopeKind.Branch))
            {
                return MasterDataScopeDecision.Denied("organization_scope_denied");
            }

            if (context.TrustedScope is not { } trusted
                || !string.Equals(
                    trusted.Value,
                    $"{anchor.Kind}:{anchor.Id:D}",
                    StringComparison.OrdinalIgnoreCase))
            {
                return MasterDataScopeDecision.Denied("organization_scope_denied");
            }
        }

        return MasterDataScopeDecision.Success("price_list_scope_allowed");
    }

    public static BusinessScope CreateScope(
        TenantId tenantId,
        OrganizationScopeKind? organizationScopeKind = null,
        Guid? organizationScopeId = null)
    {
        OrganizationReference? anchor = null;
        if (organizationScopeKind is not null || organizationScopeId is not null)
        {
            if (organizationScopeKind is not { } kind
                || kind is not (OrganizationScopeKind.Company or OrganizationScopeKind.Branch)
                || organizationScopeId is not { } id
                || id == Guid.Empty)
            {
                throw new ArgumentException("A Price List organization scope must be a valid Company or Branch reference.");
            }

            anchor = new OrganizationReference(new TenantOwnership(tenantId.Value), kind, id);
        }

        return new BusinessScope(
            new TenantOwnership(tenantId.Value),
            anchor,
            new ScopePolicyReference(PolicyId, PolicyVersion));
    }
}

public sealed class PriceListResourcePolicy : IMasterDataResourcePolicy
{
    public MasterDataPolicyDecision Evaluate(MasterDataPolicyInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        return input.Resource.ResourceKind == MasterDataResourceKind.PriceList
            ? MasterDataPolicyDecision.Allowed("price_list_resource_allowed")
            : MasterDataPolicyDecision.Denied("resource_policy_not_configured");
    }
}

public sealed class PriceListApprovalPolicy : IMasterDataApprovalPolicy
{
    private readonly DefaultMasterDataApprovalPolicy fallback = new();

    public MasterDataApprovalPolicyResult Evaluate(
        MasterDataRequestContext context,
        MasterDataResourceReference resource,
        MasterDataOperation operation)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(resource);

        if (resource.ResourceKind == MasterDataResourceKind.PriceList
            && operation is (MasterDataOperation.View
                or MasterDataOperation.ViewAuditHistory
                or MasterDataOperation.Create
                or MasterDataOperation.Edit
                or MasterDataOperation.Activate
                or MasterDataOperation.Deactivate
                or MasterDataOperation.Reactivate))
        {
            // Routine Price List authority follows the approved generic
            // Master Data permission catalogue. No generic approver or Draft
            // state is invented here; a future explicitly high-risk catalogue
            // entry remains a separate gate.
            return new MasterDataApprovalPolicyResult(MasterDataApprovalPolicyStatus.NotApplicable);
        }

        return fallback.Evaluate(context, resource, operation);
    }
}

public static class MasterDataPriceListValuePolicy
{
    public static string NormalizeCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("A Price List code is required.", nameof(code));
        }

        var normalized = code.Trim();
        if (normalized.Length > 128 || normalized.Any(char.IsControl))
        {
            throw new ArgumentException("The Price List code is outside the approved bound.", nameof(code));
        }

        return normalized;
    }

    public static LocalizedName NormalizeName(string englishName, string? arabicName) =>
        new(englishName, arabicName);

    public static void ValidateApplicability(
        Guid currencyId,
        Guid? customerId,
        OrganizationScopeKind? organizationScopeKind,
        Guid? organizationScopeId,
        int priority)
    {
        if (currencyId == Guid.Empty)
        {
            throw new ArgumentException("A Price List requires an existing Currency identity.", nameof(currencyId));
        }

        if (customerId == Guid.Empty)
        {
            throw new ArgumentException("The Customer reference must be non-empty when supplied.", nameof(customerId));
        }

        if (organizationScopeKind.HasValue != organizationScopeId.HasValue
            || organizationScopeId == Guid.Empty
            || organizationScopeKind is OrganizationScopeKind.Warehouse)
        {
            throw new ArgumentException("Price List applicability may target a Company or Branch only.");
        }

        if (priority is < 0 or > 1_000_000)
        {
            throw new ArgumentException("Price List priority is outside the approved bound.", nameof(priority));
        }
    }

    public static void ValidatePrice(
        Guid productId,
        Guid unitOfMeasureId,
        DateOnly effectiveFrom,
        DateOnly? effectiveTo,
        decimal price,
        int priceScale,
        PriceListProvenance provenance,
        string? sourceReference)
    {
        if (productId == Guid.Empty || unitOfMeasureId == Guid.Empty)
        {
            throw new ArgumentException("A Price List price requires Product and UOM identities.");
        }

        if (effectiveFrom == default || effectiveTo is { } end && end < effectiveFrom)
        {
            throw new ArgumentException("The Price List price effective window is invalid.");
        }

        if (price < 0m || priceScale is < 0 or > 12 || decimal.Round(price, priceScale) != price)
        {
            throw new ArgumentException("The Price List price must be non-negative and match its declared precision scale.");
        }

        if (!Enum.IsDefined(provenance))
        {
            throw new ArgumentException("The Price List price provenance is invalid.", nameof(provenance));
        }

        if (sourceReference is not null
            && (sourceReference.Trim().Length > 1024 || sourceReference.Any(char.IsControl)))
        {
            throw new ArgumentException("The Price List source reference is outside the approved bound.", nameof(sourceReference));
        }
    }

    public static void ValidateVersion(byte[] expectedVersion)
    {
        if (expectedVersion is null || expectedVersion.Length == 0 || expectedVersion.Length > 64)
        {
            throw new ArgumentException("An optimistic-concurrency version is required.", nameof(expectedVersion));
        }
    }

    public static string NormalizeSourceReference(string? sourceReference) =>
        string.IsNullOrWhiteSpace(sourceReference) ? string.Empty : sourceReference.Trim();
}

#pragma warning restore CS1591
