#pragma warning disable CS1591

using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.Contracts.Modules.MasterData;

namespace MiniErp.App.Modules.MasterData;

public sealed class TaxScopePolicy : IMasterDataScopePolicy
{
    public const string PolicyId = "master-data.tax.scope";
    public const int PolicyVersion = 1;

    public MasterDataScopeDecision Evaluate(
        MasterDataRequestContext context,
        MasterDataResourceReference resource)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(resource);

        if (resource.ResourceKind != MasterDataResourceKind.Tax)
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

        return MasterDataScopeDecision.Success("tax_scope_allowed");
    }

    public static BusinessScope CreateScope(TenantId tenantId) => new(
        new TenantOwnership(tenantId.Value),
        organizationAnchor: null,
        new ScopePolicyReference(PolicyId, PolicyVersion));
}

public sealed class TaxResourcePolicy : IMasterDataResourcePolicy
{
    public MasterDataPolicyDecision Evaluate(MasterDataPolicyInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        return input.Resource.ResourceKind == MasterDataResourceKind.Tax
            ? MasterDataPolicyDecision.Allowed("tax_resource_allowed")
            : MasterDataPolicyDecision.Denied("resource_policy_not_configured");
    }
}

public sealed class TaxApprovalPolicy : IMasterDataApprovalPolicy
{
    private readonly DefaultMasterDataApprovalPolicy fallback = new();

    public MasterDataApprovalPolicyResult Evaluate(
        MasterDataRequestContext context,
        MasterDataResourceReference resource,
        MasterDataOperation operation)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(resource);

        if (resource.ResourceKind == MasterDataResourceKind.Tax
            && operation is (MasterDataOperation.View
                or MasterDataOperation.ViewAuditHistory
                or MasterDataOperation.Create
                or MasterDataOperation.Edit
                or MasterDataOperation.Activate
                or MasterDataOperation.Deactivate
                or MasterDataOperation.Reactivate))
        {
            // PD-035 approves server-derived permission/audit authority for
            // routine lifecycle work. It does not invent a separate tax
            // approver catalogue.
            return new MasterDataApprovalPolicyResult(MasterDataApprovalPolicyStatus.NotApplicable);
        }

        return fallback.Evaluate(context, resource, operation);
    }
}

public static class MasterDataTaxValuePolicy
{
    public static string NormalizeCode(string code, string fieldName = "code") => NormalizeOpaqueCode(code, 32, fieldName);

    public static string NormalizeCategoryCode(string code) => NormalizeOpaqueCode(code, 64, "categoryCode");

    public static void ValidateRateVersion(MasterDataTaxRateVersion version)
    {
        ArgumentNullException.ThrowIfNull(version);
        if (version.EffectiveFrom == default
            || version.EffectiveTo is { } end && end < version.EffectiveFrom)
        {
            throw new ArgumentException("The Tax effective window is invalid.");
        }

        if (version.RatePercentage is < 0m or > 100m)
        {
            throw new ArgumentException("The Tax rate must be between zero and one hundred percent.");
        }
    }

    public static void ValidateCalculation(TaxCalculationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (request.EffectiveOn == default
            || request.TransactionDirection is TaxDirection.Both
            || !Enum.IsDefined(request.TransactionDirection)
            || request.TaxableBase < 0m
            || request.RoundingScale is < 0 or > 6
            || !Enum.IsDefined(request.RoundingMode))
        {
            throw new ArgumentException("The Tax calculation inputs are invalid.");
        }

        _ = NormalizeCurrencyCode(request.CurrencyCode);
        NormalizeLineage(request.SourceLineage);
    }

    public static string NormalizeCurrencyCode(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("A calculation currency is required.");
        }

        var normalized = value.Trim().ToUpperInvariant();
        if (normalized.Length is < 3 or > 16
            || normalized.Any(character => !char.IsLetterOrDigit(character) && character is not '-' and not '_'))
        {
            throw new ArgumentException("The calculation currency is outside the approved bound.");
        }

        return normalized;
    }

    public static string NormalizeLineage(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new ArgumentException("Source lineage is required.");
        }

        var normalized = value.Trim();
        if (normalized.Length > 128 || normalized.Any(char.IsControl))
        {
            throw new ArgumentException("Source lineage is outside the approved bound.");
        }

        return normalized;
    }

    public static string NameKey(LocalizedName name)
    {
        ArgumentNullException.ThrowIfNull(name);
        return (name.English ?? name.Arabic ?? string.Empty).ToUpperInvariant();
    }

    private static string NormalizeOpaqueCode(string code, int maxLength, string fieldName)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("A business code is required.", fieldName);
        }

        var normalized = code.Trim().ToUpperInvariant();
        if (normalized.Length < 2 || normalized.Length > maxLength
            || normalized.Any(character => !char.IsLetterOrDigit(character) && character is not '-' and not '_' and not '.'))
        {
            throw new ArgumentException("The business code is outside the approved bound.", fieldName);
        }

        return normalized;
    }
}

#pragma warning restore CS1591
