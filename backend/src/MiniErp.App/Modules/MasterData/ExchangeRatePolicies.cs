#pragma warning disable CS1591

using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.Contracts.Modules.MasterData;

namespace MiniErp.App.Modules.MasterData;

public sealed class ExchangeRateScopePolicy : IMasterDataScopePolicy
{
    public const string PolicyId = "master-data.exchange-rate.scope";
    public const int PolicyVersion = 1;

    public MasterDataScopeDecision Evaluate(
        MasterDataRequestContext context,
        MasterDataResourceReference resource)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(resource);

        if (resource.ResourceKind != MasterDataResourceKind.ExchangeRate)
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

        return MasterDataScopeDecision.Success("exchange_rate_scope_allowed");
    }

    public static BusinessScope CreateScope(TenantId tenantId) => new(
        new TenantOwnership(tenantId.Value),
        organizationAnchor: null,
        new ScopePolicyReference(PolicyId, PolicyVersion));
}

public sealed class ExchangeRateResourcePolicy : IMasterDataResourcePolicy
{
    public MasterDataPolicyDecision Evaluate(MasterDataPolicyInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        return input.Resource.ResourceKind == MasterDataResourceKind.ExchangeRate
            ? MasterDataPolicyDecision.Allowed("exchange_rate_resource_allowed")
            : MasterDataPolicyDecision.Denied("resource_policy_not_configured");
    }
}

public sealed class ExchangeRateApprovalPolicy : IMasterDataApprovalPolicy
{
    private readonly DefaultMasterDataApprovalPolicy fallback = new();

    public MasterDataApprovalPolicyResult Evaluate(
        MasterDataRequestContext context,
        MasterDataResourceReference resource,
        MasterDataOperation operation)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(resource);

        if (resource.ResourceKind == MasterDataResourceKind.ExchangeRate
            && operation is (MasterDataOperation.View
                or MasterDataOperation.ViewAuditHistory
                or MasterDataOperation.Create
                or MasterDataOperation.Edit
                or MasterDataOperation.Activate
                or MasterDataOperation.Deactivate
                or MasterDataOperation.Reactivate))
        {
            // Routine authority follows the generic Master Data catalogue;
            // this capability does not invent an Exchange-Rate approver.
            return new MasterDataApprovalPolicyResult(MasterDataApprovalPolicyStatus.NotApplicable);
        }

        return fallback.Evaluate(context, resource, operation);
    }
}

public static class MasterDataExchangeRateValuePolicy
{
    public static void Validate(
        Guid sourceCurrencyId,
        Guid targetCurrencyId,
        DateOnly effectiveFrom,
        DateOnly? effectiveTo,
        decimal rate,
        int rateScale,
        ExchangeRateProvenance provenance,
        string? sourceNotes)
    {
        if (sourceCurrencyId == Guid.Empty || targetCurrencyId == Guid.Empty || sourceCurrencyId == targetCurrencyId)
        {
            throw new ArgumentException("Exchange Rates require two distinct existing Currency identities.");
        }

        if (effectiveFrom == default || effectiveTo is { } end && end < effectiveFrom)
        {
            throw new ArgumentException("The Exchange Rate effective window is invalid.");
        }

        if (rate <= 0m || rateScale is < 0 or > 12 || decimal.Round(rate, rateScale) != rate)
        {
            throw new ArgumentException("The Exchange Rate must be positive and match its declared precision scale.");
        }

        if (!Enum.IsDefined(provenance))
        {
            throw new ArgumentException("The Exchange Rate provenance is invalid.");
        }

        if (sourceNotes is not null && (sourceNotes.Trim().Length > 1024 || sourceNotes.Any(char.IsControl)))
        {
            throw new ArgumentException("Exchange Rate source notes are outside the approved bound.");
        }
    }

    public static string NormalizeSourceNotes(string? sourceNotes) =>
        string.IsNullOrWhiteSpace(sourceNotes) ? string.Empty : sourceNotes.Trim();

    public static void ValidateVersion(byte[] expectedVersion)
    {
        if (expectedVersion is null || expectedVersion.Length == 0 || expectedVersion.Length > 64)
        {
            throw new ArgumentException("An optimistic-concurrency version is required.");
        }
    }
}

#pragma warning restore CS1591
