#pragma warning disable CS1591

using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.Contracts.Modules.MasterData;

namespace MiniErp.App.Modules.MasterData;

public sealed class CurrencyPaymentTermScopePolicy : IMasterDataScopePolicy
{
    public const string PolicyId = "master-data.currency-payment-terms.scope";
    public const int PolicyVersion = 1;

    public MasterDataScopeDecision Evaluate(
        MasterDataRequestContext context,
        MasterDataResourceReference resource)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(resource);

        if (resource.ResourceKind is not (
            MasterDataResourceKind.Currency
            or MasterDataResourceKind.PaymentTerm))
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

        return MasterDataScopeDecision.Success("currency_payment_terms_scope_allowed");
    }

    public static BusinessScope CreateScope(TenantId tenantId) => new(
        new TenantOwnership(tenantId.Value),
        organizationAnchor: null,
        new ScopePolicyReference(PolicyId, PolicyVersion));
}

public sealed class CurrencyPaymentTermResourcePolicy : IMasterDataResourcePolicy
{
    public MasterDataPolicyDecision Evaluate(MasterDataPolicyInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        return input.Resource.ResourceKind is
            MasterDataResourceKind.Currency or MasterDataResourceKind.PaymentTerm
            ? MasterDataPolicyDecision.Allowed("currency_payment_terms_resource_allowed")
            : MasterDataPolicyDecision.Denied("resource_policy_not_configured");
    }
}

public sealed class CurrencyPaymentTermApprovalPolicy : IMasterDataApprovalPolicy
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
            MasterDataResourceKind.Currency
            or MasterDataResourceKind.PaymentTerm)
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

public static class MasterDataCurrencyPaymentTermValuePolicy
{
    public static string NormalizeCode(string code)
    {
        if (string.IsNullOrWhiteSpace(code))
        {
            throw new ArgumentException("A business code is required.", nameof(code));
        }

        var normalized = code.Trim().ToUpperInvariant();
        if (normalized.Length is < 2 or > 16
            || normalized.Any(character => !char.IsLetterOrDigit(character) && character is not '-' and not '_' and not '.'))
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

    public static void ValidatePaymentTerm(
        DateOnly effectiveFrom,
        DateOnly? effectiveTo,
        PaymentTermBaseDateRule baseDateRule,
        PaymentTermScheduleMode scheduleMode,
        MasterDataPaymentTermOffset dueOffset,
        IReadOnlyList<MasterDataPaymentTermInstallment> installments,
        MasterDataEarlySettlementDiscount earlySettlementDiscount)
    {
        if (!Enum.IsDefined(baseDateRule) || !Enum.IsDefined(scheduleMode))
        {
            throw new ArgumentException("A supported payment-term rule and schedule are required.");
        }

        if (effectiveFrom == default
            || effectiveTo is { } end && end < effectiveFrom)
        {
            throw new ArgumentException("The payment-term effective window is invalid.");
        }

        ArgumentNullException.ThrowIfNull(dueOffset);
        ArgumentNullException.ThrowIfNull(installments);
        ArgumentNullException.ThrowIfNull(earlySettlementDiscount);
        // The due offset is used for SingleDueDate. Installment schedules keep
        // the field at zero and carry their own offsets per installment.
        ValidateOffset(dueOffset, allowZero: true);

        if (scheduleMode == PaymentTermScheduleMode.SingleDueDate)
        {
            if (installments.Count != 0)
            {
                throw new ArgumentException("A single due-date term cannot contain installments.");
            }
        }
        else
        {
            if (installments.Count == 0)
            {
                throw new ArgumentException("An installment term requires at least one installment.");
            }

            var expectedSequence = 1;
            var total = 0m;
            foreach (var installment in installments.OrderBy(item => item.Sequence))
            {
                if (installment.Sequence != expectedSequence++)
                {
                    throw new ArgumentException("Installment sequences must be contiguous and start at one.");
                }

                if (installment.Percentage <= 0m || installment.Percentage > 100m)
                {
                    throw new ArgumentException("Installment percentages must be greater than zero and no more than 100.");
                }

                ValidateOffset(installment.Offset, allowZero: true);
                total += installment.Percentage;
            }

            if (total != 100m)
            {
                throw new ArgumentException("Installment percentages must total exactly 100%.");
            }
        }

        if (earlySettlementDiscount.Enabled)
        {
            if (earlySettlementDiscount.Percentage is not { } percentage
                || percentage <= 0m
                || percentage > 100m)
            {
                throw new ArgumentException("An enabled early-settlement discount requires a percentage from zero to 100.");
            }

            ValidateOffset(earlySettlementDiscount.Offset, allowZero: false);
        }
        else if (earlySettlementDiscount.Percentage is not null
            || earlySettlementDiscount.Offset.Days != 0
            || earlySettlementDiscount.Offset.Months != 0)
        {
            throw new ArgumentException("A disabled early-settlement discount cannot carry configuration.");
        }
    }

    private static void ValidateOffset(MasterDataPaymentTermOffset offset, bool allowZero)
    {
        ArgumentNullException.ThrowIfNull(offset);
        if (offset.Days < 0 || offset.Months < 0
            || offset.Days > 0 && offset.Months > 0
            || !allowZero && offset.Days == 0 && offset.Months == 0)
        {
            throw new ArgumentException("An offset must use non-negative days or months, but not both.");
        }
    }
}

#pragma warning restore CS1591
