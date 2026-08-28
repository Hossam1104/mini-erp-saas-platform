#pragma warning disable CS1591

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.Modules.BusinessParties;
using MiniErp.App.Modules.Finance;
using MiniErp.App.Modules.MasterData;
using MiniErp.App.Modules.Procurement;
using MiniErp.Contracts.Modules.Finance;
using MiniErp.Contracts.Modules.Foundation;
using MiniErp.Contracts.Modules.MasterData;
using MiniErp.Contracts.Modules.Sales;

namespace MiniErp.App.Modules.Sales;

public sealed record SalesOperationResult<T>(bool Succeeded, string Code, T? Value)
{
    public static SalesOperationResult<T> Success(T value) => new(true, "succeeded", value);
    public static SalesOperationResult<T> Failure(string code) => new(false, code, default);
}

public sealed record SalesScope(Guid TenantId, Guid CompanyId, Guid? BranchId)
{
    public PurchaseRequestScope ToProcurementScope() => new(TenantId, CompanyId, BranchId);
}

public sealed record SalesTaxEvidence(
    Guid TaxId,
    string TaxCode,
    Guid RateVersionId,
    int RateVersionNumber,
    DateOnly EffectiveOn,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    decimal RatePercentage,
    decimal TaxableBase,
    decimal TaxAmount,
    string CurrencyCode,
    string ReferenceValue);

public sealed record SalesTaxResolution(bool Succeeded, string Code, SalesTaxEvidence? Value)
{
    public static SalesTaxResolution Success(SalesTaxEvidence value) => new(true, "resolved", value);
    public static SalesTaxResolution Failure(string code) => new(false, code, null);
}

public interface ISalesTaxReferenceProvider
{
    Task<SalesTaxResolution> ResolveAsync(
        ProcurementRequestContext context,
        Guid taxId,
        DateOnly effectiveOn,
        decimal taxableBase,
        string currencyCode,
        string sourceLineage,
        CancellationToken cancellationToken = default);
}

public sealed class MasterDataSalesTaxReferenceProvider(IMasterDataTaxPersistence persistence) : ISalesTaxReferenceProvider
{
    public async Task<SalesTaxResolution> ResolveAsync(
        ProcurementRequestContext context,
        Guid taxId,
        DateOnly effectiveOn,
        decimal taxableBase,
        string currencyCode,
        string sourceLineage,
        CancellationToken cancellationToken = default)
    {
        if (taxId == Guid.Empty || effectiveOn == default || taxableBase < 0m || string.IsNullOrWhiteSpace(currencyCode))
        {
            return SalesTaxResolution.Failure("tax_reference_invalid");
        }

        try
        {
            var normalizedCurrency = MasterDataTaxValuePolicy.NormalizeCurrencyCode(currencyCode);
            _ = MasterDataTaxValuePolicy.NormalizeLineage(sourceLineage);
            var tax = await persistence.FindTaxAsync(context.TenantContext, taxId, cancellationToken);
            if (tax is null || tax.TenantId.Value != context.TenantId.Value)
            {
                return SalesTaxResolution.Failure("tax_not_found");
            }

            if (tax.LifecycleState != MasterDataLifecycleState.Active)
            {
                return SalesTaxResolution.Failure("tax_inactive");
            }

            if (tax.Applicability is not (TaxDirection.Both or TaxDirection.Sales))
            {
                return SalesTaxResolution.Failure("tax_direction_not_applicable");
            }

            var version = tax.RateVersions.SingleOrDefault(item =>
                item.EffectiveFrom <= effectiveOn
                && (item.EffectiveTo is null || effectiveOn <= item.EffectiveTo.Value));
            if (version is null)
            {
                return SalesTaxResolution.Failure("tax_version_not_found");
            }

            var taxAmount = MasterDataTaxValuePolicy.CalculateTaxAmount(taxableBase, version.RatePercentage, 2, TaxRoundingMode.AwayFromZero);
            return SalesTaxResolution.Success(new SalesTaxEvidence(
                tax.Id,
                tax.Code,
                version.Id,
                version.VersionNumber,
                effectiveOn,
                version.EffectiveFrom,
                version.EffectiveTo,
                version.RatePercentage,
                taxableBase,
                taxAmount,
                normalizedCurrency,
                $"{tax.Code};v{version.VersionNumber}"));
        }
        catch
        {
            return SalesTaxResolution.Failure("tax_reference_unavailable");
        }
    }
}

public sealed class UnavailableSalesTaxReferenceProvider : ISalesTaxReferenceProvider
{
    public Task<SalesTaxResolution> ResolveAsync(ProcurementRequestContext context, Guid taxId, DateOnly effectiveOn, decimal taxableBase, string currencyCode, string sourceLineage, CancellationToken cancellationToken = default) =>
        Task.FromResult(SalesTaxResolution.Failure("tax_reference_unavailable"));
}

public sealed record SalesExchangeRateResolution(bool Succeeded, string Code, SalesExchangeRateEvidence? Value)
{
    public static SalesExchangeRateResolution Success(SalesExchangeRateEvidence value) => new(true, "resolved", value);
    public static SalesExchangeRateResolution Failure(string code) => new(false, code, null);
}

public interface ISalesExchangeRateReferenceProvider
{
    Task<SalesExchangeRateResolution> ResolveAsync(
        TenantContext tenantContext,
        Guid exchangeRateId,
        string sourceCurrencyCode,
        string targetCurrencyCode,
        DateOnly effectiveOn,
        CancellationToken cancellationToken = default);
}

public sealed class MasterDataSalesExchangeRateReferenceProvider(IMasterDataExchangeRatePersistence persistence) : ISalesExchangeRateReferenceProvider
{
    public async Task<SalesExchangeRateResolution> ResolveAsync(
        TenantContext tenantContext,
        Guid exchangeRateId,
        string sourceCurrencyCode,
        string targetCurrencyCode,
        DateOnly effectiveOn,
        CancellationToken cancellationToken = default)
    {
        if (tenantContext is null || exchangeRateId == Guid.Empty || effectiveOn == default
            || string.IsNullOrWhiteSpace(sourceCurrencyCode) || string.IsNullOrWhiteSpace(targetCurrencyCode))
        {
            return SalesExchangeRateResolution.Failure("exchange_rate_reference_invalid");
        }

        try
        {
            var source = sourceCurrencyCode.Trim();
            var target = targetCurrencyCode.Trim();
            var record = await persistence.FindExchangeRateAsync(tenantContext, exchangeRateId, cancellationToken);
            if (record is null
                || record.TenantId.Value != tenantContext.TenantId.Value
                || record.LifecycleState != MasterDataLifecycleState.Active
                || !string.Equals(record.SourceCurrencyCode, source, StringComparison.OrdinalIgnoreCase)
                || !string.Equals(record.TargetCurrencyCode, target, StringComparison.OrdinalIgnoreCase))
            {
                return SalesExchangeRateResolution.Failure("exchange_rate_reference_invalid");
            }

            var version = record.Versions
                .Where(item => item.EffectiveFrom <= effectiveOn && (item.EffectiveTo is null || effectiveOn <= item.EffectiveTo.Value))
                .OrderByDescending(item => item.VersionNumber)
                .FirstOrDefault();
            if (version is null || version.Rate <= 0m || version.RateScale <= 0)
            {
                return SalesExchangeRateResolution.Failure("exchange_rate_reference_invalid");
            }

            return SalesExchangeRateResolution.Success(new SalesExchangeRateEvidence(
                record.Id,
                version.Id,
                version.VersionNumber,
                version.SourceCurrencyCode,
                version.TargetCurrencyCode,
                version.Rate,
                version.RateScale,
                version.Provenance.ToString(),
                version.SourceNotes,
                effectiveOn,
                version.EffectiveFrom,
                version.EffectiveTo,
                $"{version.SourceCurrencyCode}->{version.TargetCurrencyCode};v{version.VersionNumber}"));
        }
        catch
        {
            return SalesExchangeRateResolution.Failure("exchange_rate_reference_unavailable");
        }
    }
}

public sealed class UnavailableSalesExchangeRateReferenceProvider : ISalesExchangeRateReferenceProvider
{
    public Task<SalesExchangeRateResolution> ResolveAsync(TenantContext tenantContext, Guid exchangeRateId, string sourceCurrencyCode, string targetCurrencyCode, DateOnly effectiveOn, CancellationToken cancellationToken = default) =>
        Task.FromResult(SalesExchangeRateResolution.Failure("exchange_rate_reference_unavailable"));
}

public sealed record SalesLineWriteModel(
    Guid Id,
    Guid ProductId,
    string ProductSku,
    string ProductName,
    Guid UnitOfMeasureId,
    string UnitOfMeasureCode,
    decimal Quantity,
    decimal UnitPrice,
    decimal ResolvedUnitPrice,
    decimal DiscountPercent,
    decimal DiscountAmount,
    decimal TaxAmount,
    decimal LineTotal,
    Guid? PriceListId,
    int? PriceVersionNumber,
    DateOnly? PriceEffectiveFrom,
    string PriceProvenance,
    string? PriceSourceReference,
    bool ManualPriceApplied,
    string? CommercialAuthorityPolicyId,
    Guid? CommercialAuthorityActorId,
    string? CommercialAuthorityEvidence,
    string? Notes,
    SalesTaxEvidence? TaxEvidence = null);

public sealed record SalesQuotationWriteModel(
    Guid Id,
    Guid CompanyId,
    Guid? BranchId,
    Guid CustomerId,
    string CustomerCode,
    string CustomerName,
    DateOnly QuotationDate,
    DateOnly ValidUntil,
    Guid CurrencyId,
    string CurrencyCode,
    string? CustomerContactId,
    string? Notes,
    string? CustomerReference,
    IReadOnlyList<SalesLineWriteModel> Lines,
    decimal Subtotal,
    decimal DiscountAmount,
    decimal TaxAmount,
    decimal Total,
    string? Reason = null,
    SalesExchangeRateEvidence? ExchangeRateEvidence = null);

public sealed record SalesApprovalStageDefinition(
    string StageKey,
    int Sequence,
    int RequiredApprovals,
    IReadOnlyList<Guid> EligibleApproverIds,
    bool AllowDelegation);

public sealed record SalesApprovalPolicyDefinition(
    string PolicyId,
    int Version,
    IReadOnlyList<SalesApprovalStageDefinition> Stages,
    bool AllowRequesterCancellationWhilePending,
    bool DirectOrderCreationAllowed,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo,
    decimal? MinimumTotal = null,
    decimal? MaximumTotal = null,
    string? CurrencyCode = null,
    bool EnforceSeparationOfDuties = true)
{
    public bool Matches(decimal total, string? currencyCode, DateTimeOffset at) =>
        EffectiveFrom <= at
        && (EffectiveTo is null || EffectiveTo > at)
        && (MinimumTotal is null || total >= MinimumTotal)
        && (MaximumTotal is null || total <= MaximumTotal)
        && (string.IsNullOrWhiteSpace(CurrencyCode) || string.Equals(CurrencyCode, currencyCode, StringComparison.OrdinalIgnoreCase));
}

public interface ISalesApprovalPolicyProvider
{
    Task<SalesApprovalPolicyDefinition?> ResolveAsync(
        ProcurementRequestContext context,
        SalesScope scope,
        string documentType,
        decimal total,
        DateTimeOffset at,
        CancellationToken cancellationToken = default,
        string? currencyCode = null);
}

public sealed class DefaultSalesApprovalPolicyProvider : ISalesApprovalPolicyProvider
{
    public Task<SalesApprovalPolicyDefinition?> ResolveAsync(
        ProcurementRequestContext context,
        SalesScope scope,
        string documentType,
        decimal total,
        DateTimeOffset at,
        CancellationToken cancellationToken = default,
        string? currencyCode = null) =>
        Task.FromResult<SalesApprovalPolicyDefinition?>(new(
            "sales.commercial.default",
            1,
            [new SalesApprovalStageDefinition("commercial-approver", 1, 1, [], true)],
            AllowRequesterCancellationWhilePending: true,
            DirectOrderCreationAllowed: false,
            DateTimeOffset.MinValue,
            null));
}

public sealed record SalesApprovalPolicyBinding(SalesScope Scope, string DocumentType, SalesApprovalPolicyDefinition Definition);

public sealed class ConfiguredSalesApprovalPolicyProvider(IEnumerable<SalesApprovalPolicyBinding> bindings) : ISalesApprovalPolicyProvider
{
    private readonly IReadOnlyList<SalesApprovalPolicyBinding> bindings = bindings.ToArray();

    public Task<SalesApprovalPolicyDefinition?> ResolveAsync(
        ProcurementRequestContext context,
        SalesScope scope,
        string documentType,
        decimal total,
        DateTimeOffset at,
        CancellationToken cancellationToken = default,
        string? currencyCode = null) =>
        Task.FromResult(bindings
            .Where(item => item.Scope == scope && string.Equals(item.DocumentType, documentType, StringComparison.Ordinal))
            .Where(item => item.Definition.Matches(total, currencyCode, at))
            .OrderByDescending(item => item.Definition.Version)
            .ThenBy(item => item.Definition.PolicyId, StringComparer.Ordinal)
            .Select(item => item.Definition)
            .FirstOrDefault());
}

public sealed class SalesPolicyOptions
{
    public List<SalesApprovalPolicyOptions> ApprovalPolicies { get; set; } = [];
    public List<SalesCommercialAuthorityOptions> CommercialAuthorities { get; set; } = [];
    public List<SalesApprovalDelegationOptions> ApprovalDelegations { get; set; } = [];
    public List<SalesCreditLimitOptions> CreditLimits { get; set; } = [];
}

public sealed class SalesApprovalPolicyOptions
{
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid? BranchId { get; set; }
    public string DocumentType { get; set; } = string.Empty;
    public string PolicyId { get; set; } = string.Empty;
    public int Version { get; set; }
    public List<SalesApprovalStageOptions> Stages { get; set; } = [];
    public bool AllowRequesterCancellationWhilePending { get; set; }
    public bool DirectOrderCreationAllowed { get; set; }
    public DateTimeOffset EffectiveFrom { get; set; }
    public DateTimeOffset? EffectiveTo { get; set; }
    public decimal? MinimumTotal { get; set; }
    public decimal? MaximumTotal { get; set; }
    public string? CurrencyCode { get; set; }
    public bool EnforceSeparationOfDuties { get; set; } = true;

    public SalesApprovalPolicyBinding ToBinding() => new(
        new SalesScope(TenantId, CompanyId, BranchId),
        DocumentType,
        new SalesApprovalPolicyDefinition(PolicyId, Version, Stages.Select(item => item.ToDefinition()).ToArray(), AllowRequesterCancellationWhilePending, DirectOrderCreationAllowed, EffectiveFrom, EffectiveTo, MinimumTotal, MaximumTotal, CurrencyCode, EnforceSeparationOfDuties));
}

public sealed class SalesApprovalStageOptions
{
    public string StageKey { get; set; } = string.Empty;
    public int Sequence { get; set; }
    public int RequiredApprovals { get; set; }
    public List<Guid> EligibleApproverIds { get; set; } = [];
    public bool AllowDelegation { get; set; }
    public SalesApprovalStageDefinition ToDefinition() => new(StageKey, Sequence, RequiredApprovals, EligibleApproverIds, AllowDelegation);
}

public sealed class SalesCommercialAuthorityOptions
{
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid? BranchId { get; set; }
    public string DocumentType { get; set; } = string.Empty;
    public decimal MaximumDiscountPercent { get; set; }
    public bool AllowManualPrice { get; set; }
    public string PolicyId { get; set; } = string.Empty;
    public int Version { get; set; }
    public List<Guid> AuthorizedActorIds { get; set; } = [];
    public DateTimeOffset EffectiveFrom { get; set; }
    public DateTimeOffset? EffectiveTo { get; set; }

    public SalesCommercialAuthority ToAuthority() => new(TenantId, CompanyId, BranchId, DocumentType, MaximumDiscountPercent, AllowManualPrice, PolicyId, Version, AuthorizedActorIds, EffectiveFrom, EffectiveTo);
}

public sealed class SalesApprovalDelegationOptions
{
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid? BranchId { get; set; }
    public string DocumentType { get; set; } = string.Empty;
    public string StageKey { get; set; } = string.Empty;
    public Guid DelegatorId { get; set; }
    public Guid DelegateeId { get; set; }
    public DateTimeOffset StartsAt { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }

    public SalesApprovalDelegation ToDelegation() => new(TenantId, CompanyId, BranchId, DocumentType, StageKey, DelegatorId, DelegateeId, StartsAt, ExpiresAt);
}

public sealed class SalesCreditLimitOptions
{
    public Guid TenantId { get; set; }
    public Guid CompanyId { get; set; }
    public Guid CustomerId { get; set; }
    public string CurrencyCode { get; set; } = string.Empty;
    public decimal Limit { get; set; }
    public DateOnly EffectiveFrom { get; set; }
    public DateOnly? EffectiveTo { get; set; }

    public SalesCreditLimit ToLimit() => new(TenantId, CompanyId, CustomerId, CurrencyCode, Limit, EffectiveFrom, EffectiveTo);
}

public sealed class ConfigurationSalesApprovalPolicyProvider(IOptionsMonitor<SalesPolicyOptions> options) : ISalesApprovalPolicyProvider
{
    public Task<SalesApprovalPolicyDefinition?> ResolveAsync(ProcurementRequestContext context, SalesScope scope, string documentType, decimal total, DateTimeOffset at, CancellationToken cancellationToken = default, string? currencyCode = null)
    {
        var selected = options.CurrentValue.ApprovalPolicies
            .Where(item => item.TenantId == scope.TenantId && item.CompanyId == scope.CompanyId && item.BranchId == scope.BranchId && string.Equals(item.DocumentType, documentType, StringComparison.Ordinal))
            .Select(item => item.ToBinding().Definition)
            .Where(item => item.Stages.Count > 0 && item.Stages.All(stage => stage.RequiredApprovals > 0 && !string.IsNullOrWhiteSpace(stage.StageKey)) && item.Matches(total, currencyCode, at))
            .OrderByDescending(item => item.Version)
            .ThenBy(item => item.PolicyId, StringComparer.Ordinal)
            .FirstOrDefault();
        return Task.FromResult<SalesApprovalPolicyDefinition?>(selected);
    }
}

public sealed class ConfigurationSalesCommercialAuthorityProvider(IOptionsMonitor<SalesPolicyOptions> options) : ISalesCommercialAuthorityProvider
{
    public Task<SalesCommercialAuthority?> ResolveAsync(ProcurementRequestContext context, SalesScope scope, string documentType, Guid actorId, DateTimeOffset at, CancellationToken cancellationToken = default) =>
        Task.FromResult(options.CurrentValue.CommercialAuthorities
            .Select(item => item.ToAuthority())
            .Where(item => item.TenantId == scope.TenantId && item.CompanyId == scope.CompanyId && item.BranchId == scope.BranchId && string.Equals(item.DocumentType, documentType, StringComparison.Ordinal))
            .Where(item => item.EffectiveFrom <= at && (item.EffectiveTo is null || item.EffectiveTo > at))
            .Where(item => item.AuthorizedActorIds.Count == 0 || item.AuthorizedActorIds.Contains(actorId))
            .OrderByDescending(item => item.Version)
            .ThenBy(item => item.PolicyId, StringComparer.Ordinal)
            .FirstOrDefault());
}

public sealed class ConfigurationSalesApprovalDelegationProvider(IOptionsMonitor<SalesPolicyOptions> options) : ISalesApprovalDelegationProvider
{
    public Task<SalesApprovalDelegation?> ResolveAsync(ProcurementRequestContext context, SalesScope scope, string documentType, SalesApprovalStageDefinition stage, Guid actorId, DateTimeOffset at, CancellationToken cancellationToken = default) =>
        Task.FromResult(options.CurrentValue.ApprovalDelegations
            .Select(item => item.ToDelegation())
            .Where(item => item.TenantId == scope.TenantId && item.CompanyId == scope.CompanyId && item.BranchId == scope.BranchId && string.Equals(item.DocumentType, documentType, StringComparison.Ordinal) && string.Equals(item.StageKey, stage.StageKey, StringComparison.Ordinal))
            .Where(item => item.DelegateeId == actorId && item.DelegatorId != actorId && item.StartsAt <= at && item.ExpiresAt > at)
            .Where(item => stage.AllowDelegation && (stage.EligibleApproverIds.Count == 0 || stage.EligibleApproverIds.Contains(item.DelegatorId)))
            .OrderBy(item => item.ExpiresAt)
            .ThenBy(item => item.DelegatorId)
            .FirstOrDefault());
}

public sealed class ConfigurationSalesCreditLimitProvider(IOptionsMonitor<SalesPolicyOptions> options) : ISalesCreditLimitProvider
{
    public Task<decimal?> ResolveLimitAsync(ProcurementRequestContext context, Guid companyId, Guid customerId, string currencyCode, DateOnly asOfDate, CancellationToken cancellationToken = default) =>
        Task.FromResult(options.CurrentValue.CreditLimits
            .Select(item => item.ToLimit())
            .Where(item => item.TenantId == context.TenantId.Value && item.CompanyId == companyId && item.CustomerId == customerId && string.Equals(item.CurrencyCode, currencyCode, StringComparison.OrdinalIgnoreCase))
            .Where(item => item.EffectiveFrom <= asOfDate && (item.EffectiveTo is null || item.EffectiveTo >= asOfDate))
            .OrderByDescending(item => item.EffectiveFrom)
            .Select(item => (decimal?)item.Limit)
            .FirstOrDefault());
}

public sealed record SalesCommercialAuthority(
    Guid TenantId,
    Guid CompanyId,
    Guid? BranchId,
    string DocumentType,
    decimal MaximumDiscountPercent,
    bool AllowManualPrice,
    string PolicyId,
    int Version,
    IReadOnlyList<Guid> AuthorizedActorIds,
    DateTimeOffset EffectiveFrom,
    DateTimeOffset? EffectiveTo);

public interface ISalesCommercialAuthorityProvider
{
    Task<SalesCommercialAuthority?> ResolveAsync(
        ProcurementRequestContext context,
        SalesScope scope,
        string documentType,
        Guid actorId,
        DateTimeOffset at,
        CancellationToken cancellationToken = default);
}

public sealed class NoSalesCommercialAuthorityProvider : ISalesCommercialAuthorityProvider
{
    public Task<SalesCommercialAuthority?> ResolveAsync(ProcurementRequestContext context, SalesScope scope, string documentType, Guid actorId, DateTimeOffset at, CancellationToken cancellationToken = default) =>
        Task.FromResult<SalesCommercialAuthority?>(null);
}

public sealed class ConfiguredSalesCommercialAuthorityProvider(IEnumerable<SalesCommercialAuthority> authorities) : ISalesCommercialAuthorityProvider
{
    private readonly IReadOnlyList<SalesCommercialAuthority> authorities = authorities.ToArray();

    public Task<SalesCommercialAuthority?> ResolveAsync(ProcurementRequestContext context, SalesScope scope, string documentType, Guid actorId, DateTimeOffset at, CancellationToken cancellationToken = default) =>
        Task.FromResult(authorities
            .Where(item => item.TenantId == scope.TenantId && item.CompanyId == scope.CompanyId && item.BranchId == scope.BranchId)
            .Where(item => string.Equals(item.DocumentType, documentType, StringComparison.Ordinal))
            .Where(item => item.EffectiveFrom <= at && (item.EffectiveTo is null || item.EffectiveTo > at))
            .Where(item => item.AuthorizedActorIds.Count == 0 || item.AuthorizedActorIds.Contains(actorId))
            .OrderByDescending(item => item.Version)
            .ThenBy(item => item.PolicyId, StringComparer.Ordinal)
            .FirstOrDefault());
}

public sealed record SalesApprovalDelegation(
    Guid TenantId,
    Guid CompanyId,
    Guid? BranchId,
    string DocumentType,
    string StageKey,
    Guid DelegatorId,
    Guid DelegateeId,
    DateTimeOffset StartsAt,
    DateTimeOffset ExpiresAt);

public interface ISalesApprovalDelegationProvider
{
    Task<SalesApprovalDelegation?> ResolveAsync(
        ProcurementRequestContext context,
        SalesScope scope,
        string documentType,
        SalesApprovalStageDefinition stage,
        Guid actorId,
        DateTimeOffset at,
        CancellationToken cancellationToken = default);
}

public sealed class NoSalesApprovalDelegationProvider : ISalesApprovalDelegationProvider
{
    public Task<SalesApprovalDelegation?> ResolveAsync(ProcurementRequestContext context, SalesScope scope, string documentType, SalesApprovalStageDefinition stage, Guid actorId, DateTimeOffset at, CancellationToken cancellationToken = default) =>
        Task.FromResult<SalesApprovalDelegation?>(null);
}

public sealed class ConfiguredSalesApprovalDelegationProvider(IEnumerable<SalesApprovalDelegation> delegations) : ISalesApprovalDelegationProvider
{
    private readonly IReadOnlyList<SalesApprovalDelegation> delegations = delegations.ToArray();

    public Task<SalesApprovalDelegation?> ResolveAsync(ProcurementRequestContext context, SalesScope scope, string documentType, SalesApprovalStageDefinition stage, Guid actorId, DateTimeOffset at, CancellationToken cancellationToken = default) =>
        Task.FromResult(delegations
            .Where(item => item.TenantId == scope.TenantId && item.CompanyId == scope.CompanyId && item.BranchId == scope.BranchId)
            .Where(item => string.Equals(item.DocumentType, documentType, StringComparison.Ordinal) && string.Equals(item.StageKey, stage.StageKey, StringComparison.Ordinal))
            .Where(item => item.DelegateeId == actorId && item.DelegatorId != actorId && item.StartsAt <= at && item.ExpiresAt > at)
            .Where(item => stage.EligibleApproverIds.Count == 0 || stage.EligibleApproverIds.Contains(item.DelegatorId))
            .OrderBy(item => item.ExpiresAt)
            .ThenBy(item => item.DelegatorId)
            .FirstOrDefault());
}

public sealed record SalesCreditLimit(Guid TenantId, Guid CompanyId, Guid CustomerId, string CurrencyCode, decimal Limit, DateOnly EffectiveFrom, DateOnly? EffectiveTo);

public interface ISalesCreditLimitProvider
{
    Task<decimal?> ResolveLimitAsync(ProcurementRequestContext context, Guid companyId, Guid customerId, string currencyCode, DateOnly asOfDate, CancellationToken cancellationToken = default);
}

public sealed class NoSalesCreditLimitProvider : ISalesCreditLimitProvider
{
    public Task<decimal?> ResolveLimitAsync(ProcurementRequestContext context, Guid companyId, Guid customerId, string currencyCode, DateOnly asOfDate, CancellationToken cancellationToken = default) =>
        Task.FromResult<decimal?>(null);
}

public sealed class ConfiguredSalesCreditLimitProvider(IEnumerable<SalesCreditLimit> limits) : ISalesCreditLimitProvider
{
    private readonly IReadOnlyList<SalesCreditLimit> limits = limits.ToArray();

    public Task<decimal?> ResolveLimitAsync(ProcurementRequestContext context, Guid companyId, Guid customerId, string currencyCode, DateOnly asOfDate, CancellationToken cancellationToken = default) =>
        Task.FromResult(limits
            .Where(item => item.TenantId == context.TenantId.Value && item.CompanyId == companyId && item.CustomerId == customerId && string.Equals(item.CurrencyCode, currencyCode, StringComparison.OrdinalIgnoreCase))
            .Where(item => item.EffectiveFrom <= asOfDate && (item.EffectiveTo is null || item.EffectiveTo >= asOfDate))
            .OrderByDescending(item => item.EffectiveFrom)
            .Select(item => (decimal?)item.Limit)
            .FirstOrDefault());
}

public sealed record SalesCreditEvaluation(
    SalesCreditOutcome Outcome,
    string? Reason,
    decimal? OpenReceivables,
    decimal? OverdueReceivables,
    decimal? NetReceivableExposure,
    decimal? ProposedExposure,
    decimal? CreditLimit,
    DateOnly AsOfDate,
    DateTimeOffset EvaluatedAt,
    DateTimeOffset? OverrideExpiresAt = null,
    string? CurrencyCode = null,
    string? TransactionCurrencyCode = null,
    decimal? TransactionAmount = null,
    decimal? ConvertedOrderCommitment = null,
    SalesExchangeRateEvidence? ExchangeRateEvidence = null,
    int? OrderRevisionNumber = null)
{
    public static SalesCreditEvaluation Unknown(DateOnly asOfDate, string reason) => new(SalesCreditOutcome.Unknown, reason, null, null, null, null, null, asOfDate, DateTimeOffset.UtcNow);
}

public interface ISalesPersistence
{
    Task<IReadOnlyList<SalesQuotationSummaryResponse>> ListQuotationsAsync(ProcurementRequestContext context, Guid? companyId, SalesQuotationStatus? status, CancellationToken cancellationToken = default);
    Task<SalesQuotationResponse?> GetQuotationAsync(ProcurementRequestContext context, Guid id, CancellationToken cancellationToken = default);
    Task<SalesOperationResult<SalesQuotationResponse>> CreateQuotationAsync(ProcurementRequestContext context, SalesQuotationWriteModel model, string idempotencyKey, string requestFingerprint, SalesApprovalPolicyDefinition? policy, CancellationToken cancellationToken = default);
    Task<SalesOperationResult<SalesQuotationResponse>> EditQuotationAsync(ProcurementRequestContext context, Guid id, SalesQuotationWriteModel model, byte[] expectedVersion, string idempotencyKey, string requestFingerprint, CancellationToken cancellationToken = default, SalesApprovalPolicyDefinition? policy = null);
    Task<SalesOperationResult<SalesOrderResponse>> EditOrderAsync(ProcurementRequestContext context, Guid id, SalesQuotationWriteModel model, byte[] expectedVersion, string idempotencyKey, string requestFingerprint, SalesApprovalPolicyDefinition? policy, CancellationToken cancellationToken = default);
    Task<SalesOperationResult<SalesQuotationResponse>> TransitionQuotationAsync(ProcurementRequestContext context, Guid id, SalesQuotationStatus target, string? reason, byte[] expectedVersion, string idempotencyKey, string requestFingerprint, SalesApprovalPolicyDefinition? policy, Guid? delegatedFromActorId = null, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SalesQuotationRevisionResponse>> ListQuotationRevisionsAsync(ProcurementRequestContext context, Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SalesHistoryResponse>> ListHistoryAsync(ProcurementRequestContext context, string documentType, Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SalesAuditResponse>> ListAuditAsync(ProcurementRequestContext context, string documentType, Guid id, CancellationToken cancellationToken = default);
    Task<SalesApprovalPolicyDefinition?> GetApprovalPolicyAsync(ProcurementRequestContext context, string documentType, Guid id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SalesOrderSummaryResponse>> ListOrdersAsync(ProcurementRequestContext context, Guid? companyId, SalesOrderStatus? status, CancellationToken cancellationToken = default);
    Task<SalesOrderResponse?> GetOrderAsync(ProcurementRequestContext context, Guid id, CancellationToken cancellationToken = default);
    Task<SalesOperationResult<SalesOrderResponse>> ConvertQuotationAsync(ProcurementRequestContext context, Guid quotationId, byte[] expectedVersion, string idempotencyKey, string requestFingerprint, SalesApprovalPolicyDefinition? policy, CancellationToken cancellationToken = default);
    Task<SalesOperationResult<SalesOrderResponse>> TransitionOrderAsync(ProcurementRequestContext context, Guid id, SalesOrderStatus target, string? reason, byte[] expectedVersion, string idempotencyKey, string requestFingerprint, SalesCreditEvaluation? credit, SalesApprovalPolicyDefinition? policy, Guid? delegatedFromActorId = null, CancellationToken cancellationToken = default);
    Task<SalesOperationResult<SalesOrderResponse>> OverrideOrderCreditAsync(ProcurementRequestContext context, Guid id, string reason, DateTimeOffset expiresAt, string? scope, string? sourceReference, byte[] expectedVersion, string idempotencyKey, string requestFingerprint, SalesCreditEvaluation credit, CancellationToken cancellationToken = default);
    Task<SalesCreditResponse?> GetOrderCreditAsync(ProcurementRequestContext context, Guid id, CancellationToken cancellationToken = default);
}

public sealed class UnavailableSalesPersistence : ISalesPersistence
{
    private static Task<T> Empty<T>() => Task.FromResult<T>(default!);
    private static Task<IReadOnlyList<T>> EmptyList<T>() => Task.FromResult<IReadOnlyList<T>>([]);
    private static SalesOperationResult<T> Failure<T>() => SalesOperationResult<T>.Failure("sales_persistence_unavailable");
    public Task<IReadOnlyList<SalesQuotationSummaryResponse>> ListQuotationsAsync(ProcurementRequestContext c, Guid? companyId, SalesQuotationStatus? status, CancellationToken x = default) => EmptyList<SalesQuotationSummaryResponse>();
    public Task<SalesQuotationResponse?> GetQuotationAsync(ProcurementRequestContext c, Guid id, CancellationToken x = default) => Empty<SalesQuotationResponse?>();
    public Task<SalesOperationResult<SalesQuotationResponse>> CreateQuotationAsync(ProcurementRequestContext c, SalesQuotationWriteModel m, string k, string f, SalesApprovalPolicyDefinition? p, CancellationToken x = default) => Task.FromResult(Failure<SalesQuotationResponse>());
    public Task<SalesOperationResult<SalesQuotationResponse>> EditQuotationAsync(ProcurementRequestContext c, Guid id, SalesQuotationWriteModel m, byte[] v, string k, string f, CancellationToken x = default, SalesApprovalPolicyDefinition? p = null) => Task.FromResult(Failure<SalesQuotationResponse>());
    public Task<SalesOperationResult<SalesOrderResponse>> EditOrderAsync(ProcurementRequestContext c, Guid id, SalesQuotationWriteModel m, byte[] v, string k, string f, SalesApprovalPolicyDefinition? p, CancellationToken x = default) => Task.FromResult(Failure<SalesOrderResponse>());
    public Task<SalesOperationResult<SalesQuotationResponse>> TransitionQuotationAsync(ProcurementRequestContext c, Guid id, SalesQuotationStatus t, string? r, byte[] v, string k, string f, SalesApprovalPolicyDefinition? p, Guid? d = null, CancellationToken x = default) => Task.FromResult(Failure<SalesQuotationResponse>());
    public Task<IReadOnlyList<SalesQuotationRevisionResponse>> ListQuotationRevisionsAsync(ProcurementRequestContext c, Guid id, CancellationToken x = default) => EmptyList<SalesQuotationRevisionResponse>();
    public Task<IReadOnlyList<SalesHistoryResponse>> ListHistoryAsync(ProcurementRequestContext c, string t, Guid id, CancellationToken x = default) => EmptyList<SalesHistoryResponse>();
    public Task<IReadOnlyList<SalesAuditResponse>> ListAuditAsync(ProcurementRequestContext c, string t, Guid id, CancellationToken x = default) => EmptyList<SalesAuditResponse>();
    public Task<SalesApprovalPolicyDefinition?> GetApprovalPolicyAsync(ProcurementRequestContext c, string t, Guid id, CancellationToken x = default) => Empty<SalesApprovalPolicyDefinition?>();
    public Task<IReadOnlyList<SalesOrderSummaryResponse>> ListOrdersAsync(ProcurementRequestContext c, Guid? companyId, SalesOrderStatus? status, CancellationToken x = default) => EmptyList<SalesOrderSummaryResponse>();
    public Task<SalesOrderResponse?> GetOrderAsync(ProcurementRequestContext c, Guid id, CancellationToken x = default) => Empty<SalesOrderResponse?>();
    public Task<SalesOperationResult<SalesOrderResponse>> ConvertQuotationAsync(ProcurementRequestContext c, Guid q, byte[] v, string k, string f, SalesApprovalPolicyDefinition? p, CancellationToken x = default) => Task.FromResult(Failure<SalesOrderResponse>());
    public Task<SalesOperationResult<SalesOrderResponse>> TransitionOrderAsync(ProcurementRequestContext c, Guid id, SalesOrderStatus t, string? r, byte[] v, string k, string f, SalesCreditEvaluation? credit, SalesApprovalPolicyDefinition? p, Guid? d = null, CancellationToken x = default) => Task.FromResult(Failure<SalesOrderResponse>());
    public Task<SalesOperationResult<SalesOrderResponse>> OverrideOrderCreditAsync(ProcurementRequestContext c, Guid id, string r, DateTimeOffset e, string? s, string? sr, byte[] v, string k, string f, SalesCreditEvaluation credit, CancellationToken x = default) => Task.FromResult(Failure<SalesOrderResponse>());
    public Task<SalesCreditResponse?> GetOrderCreditAsync(ProcurementRequestContext c, Guid id, CancellationToken x = default) => Empty<SalesCreditResponse?>();
}

public sealed class SalesAuthorizationService(PurchaseRequestAuthorizationService authorization)
{
    public PurchaseRequestAuthorizationResult Decide(ProcurementRequestContext context, string operationId, SalesScope? scope = null) =>
        authorization.Authorize(context, operationId, scope?.ToProcurementScope());

    public bool Authorize(ProcurementRequestContext context, string operationId, SalesScope? scope = null) =>
        Decide(context, operationId, scope).Allowed;
}

public sealed class SalesService(
    ISalesPersistence persistence,
    SalesAuthorizationService authorization,
    ISalesApprovalPolicyProvider approvalPolicies,
    ISalesCommercialAuthorityProvider commercialAuthorities,
    ISalesApprovalDelegationProvider delegations,
    ISalesCreditLimitProvider creditLimits,
    IFinanceSettlementPersistence finance,
    IFinanceCompanyProvider companies,
    ICustomerPersistence customers,
    IProductIdentityPersistence products,
    IMasterDataPriceListPersistence prices,
    ISalesTaxReferenceProvider taxes,
    ISalesExchangeRateReferenceProvider exchangeRates)
{
    private readonly ISalesPersistence persistence = persistence;
    private readonly SalesAuthorizationService authorization = authorization;
    private readonly ISalesApprovalPolicyProvider approvalPolicies = approvalPolicies;
    private readonly ISalesCommercialAuthorityProvider commercialAuthorities = commercialAuthorities;
    private readonly ISalesApprovalDelegationProvider delegations = delegations;
    private readonly ISalesCreditLimitProvider creditLimits = creditLimits;
    private readonly IFinanceSettlementPersistence finance = finance;
    private readonly IFinanceCompanyProvider companies = companies;
    private readonly ICustomerPersistence customers = customers;
    private readonly IProductIdentityPersistence products = products;
    private readonly IMasterDataPriceListPersistence prices = prices;
    private readonly ISalesTaxReferenceProvider taxes = taxes;
    private readonly ISalesExchangeRateReferenceProvider exchangeRates = exchangeRates;

    public Task<IReadOnlyList<SalesQuotationSummaryResponse>> ListQuotationsAsync(ProcurementRequestContext c, Guid? companyId, SalesQuotationStatus? status, CancellationToken x = default) => persistence.ListQuotationsAsync(c, companyId, status, x);
    public Task<SalesQuotationResponse?> GetQuotationAsync(ProcurementRequestContext c, Guid id, CancellationToken x = default) => persistence.GetQuotationAsync(c, id, x);
    public Task<IReadOnlyList<SalesOrderSummaryResponse>> ListOrdersAsync(ProcurementRequestContext c, Guid? companyId, SalesOrderStatus? status, CancellationToken x = default) => persistence.ListOrdersAsync(c, companyId, status, x);
    public Task<SalesOrderResponse?> GetOrderAsync(ProcurementRequestContext c, Guid id, CancellationToken x = default) => persistence.GetOrderAsync(c, id, x);
    public Task<IReadOnlyList<SalesQuotationRevisionResponse>> ListQuotationRevisionsAsync(ProcurementRequestContext c, Guid id, CancellationToken x = default) => persistence.ListQuotationRevisionsAsync(c, id, x);
    public Task<IReadOnlyList<SalesHistoryResponse>> ListHistoryAsync(ProcurementRequestContext c, string type, Guid id, CancellationToken x = default) => persistence.ListHistoryAsync(c, type, id, x);
    public Task<IReadOnlyList<SalesAuditResponse>> ListAuditAsync(ProcurementRequestContext c, string type, Guid id, CancellationToken x = default) => persistence.ListAuditAsync(c, type, id, x);
    public Task<SalesApprovalPolicyDefinition?> GetApprovalPolicyAsync(ProcurementRequestContext c, string type, Guid id, CancellationToken x = default) => persistence.GetApprovalPolicyAsync(c, type, id, x);

    public async Task<SalesOperationResult<SalesQuotationResponse>> CreateQuotationAsync(ProcurementRequestContext context, SalesQuotationCreateRequest request, string key, CancellationToken cancellationToken = default)
    {
        var requestedScope = new SalesScope(context.TenantId.Value, request.CompanyId, request.BranchId);
        var authorizationDecision = authorization.Decide(context, "sales.quotation.create", requestedScope);
        if (!authorizationDecision.Allowed) return SalesOperationResult<SalesQuotationResponse>.Failure(ScopeFailure(authorizationDecision.Code, "quotation_not_found"));
        var model = await BuildModelAsync(context, Guid.NewGuid(), request.CompanyId, request.BranchId, request.CustomerId, request.QuotationDate, request.ValidUntil, request.CurrencyId, request.PriceListId, request.ExchangeRateId, request.CustomerContactId, request.Notes, request.CustomerReference, request.Lines, cancellationToken);
        if (model is null) return SalesOperationResult<SalesQuotationResponse>.Failure("commercial_reference_invalid");
        var policy = await approvalPolicies.ResolveAsync(context, Scope(context, model.CompanyId, model.BranchId), "quotation", model.Total, DateTimeOffset.UtcNow, cancellationToken, model.CurrencyCode);
        return await persistence.CreateQuotationAsync(context, model, key, Fingerprint(request), policy, cancellationToken);
    }

    public async Task<SalesOperationResult<SalesQuotationResponse>> EditQuotationAsync(ProcurementRequestContext context, Guid id, SalesQuotationEditRequest request, byte[] expectedVersion, string key, CancellationToken cancellationToken = default)
    {
        var existing = await persistence.GetQuotationAsync(context, id, cancellationToken);
        if (existing is null) return SalesOperationResult<SalesQuotationResponse>.Failure("quotation_not_found");
        var authorizationDecision = authorization.Decide(context, "sales.quotation.edit", Scope(context, existing.CompanyId, existing.BranchId));
        if (!authorizationDecision.Allowed) return SalesOperationResult<SalesQuotationResponse>.Failure(ScopeFailure(authorizationDecision.Code, "quotation_not_found"));
        if (request.CompanyId != existing.CompanyId || request.BranchId != existing.BranchId)
            return SalesOperationResult<SalesQuotationResponse>.Failure("quotation_scope_immutable");
        var model = await BuildModelAsync(context, id, existing.CompanyId, existing.BranchId, existing.CustomerId, existing.QuotationDate, request.ValidUntil, request.CurrencyId, request.PriceListId, request.ExchangeRateId, request.CustomerContactId, request.Notes, request.CustomerReference, request.Lines, cancellationToken);
        if (model is null) return SalesOperationResult<SalesQuotationResponse>.Failure("commercial_reference_invalid");
        var policy = await approvalPolicies.ResolveAsync(context, Scope(context, existing.CompanyId, existing.BranchId), "quotation", model.Total, DateTimeOffset.UtcNow, cancellationToken, model.CurrencyCode);
        return await persistence.EditQuotationAsync(context, id, model, expectedVersion, key, Fingerprint(request), cancellationToken, policy);
    }

    public async Task<SalesOperationResult<SalesOrderResponse>> EditOrderAsync(ProcurementRequestContext context, Guid id, SalesOrderEditRequest request, byte[] expectedVersion, string key, CancellationToken cancellationToken = default)
    {
        var existing = await persistence.GetOrderAsync(context, id, cancellationToken);
        if (existing is null) return SalesOperationResult<SalesOrderResponse>.Failure("order_not_found");
        var authorizationDecision = authorization.Decide(context, "sales.order.edit", Scope(context, existing.CompanyId, existing.BranchId));
        if (!authorizationDecision.Allowed) return SalesOperationResult<SalesOrderResponse>.Failure(ScopeFailure(authorizationDecision.Code, "order_not_found"));
        if (existing.Status is not (SalesOrderStatus.Draft or SalesOrderStatus.ReturnedForChange)) return SalesOperationResult<SalesOrderResponse>.Failure("order_edit_locked");
        var date = DateOnly.FromDateTime(DateTime.UtcNow);
        var model = await BuildModelAsync(context, id, existing.CompanyId, existing.BranchId, existing.CustomerId, date, date, request.CurrencyId, request.PriceListId, request.ExchangeRateId, null, null, null, request.Lines, cancellationToken, "order");
        if (model is null) return SalesOperationResult<SalesOrderResponse>.Failure("commercial_reference_invalid");
        var policy = await approvalPolicies.ResolveAsync(context, Scope(context, existing.CompanyId, existing.BranchId), "order", model.Total, DateTimeOffset.UtcNow, cancellationToken, model.CurrencyCode);
        return await persistence.EditOrderAsync(context, id, model, expectedVersion, key, Fingerprint(request), policy, cancellationToken);
    }

    public async Task<SalesOperationResult<SalesQuotationResponse>> TransitionQuotationAsync(ProcurementRequestContext context, Guid id, SalesQuotationStatus target, string? reason, byte[] version, string key, CancellationToken cancellationToken = default)
    {
        var existing = await persistence.GetQuotationAsync(context, id, cancellationToken);
        if (existing is null) return SalesOperationResult<SalesQuotationResponse>.Failure("quotation_not_found");
        var operation = target switch { SalesQuotationStatus.PendingApproval => "sales.quotation.submit", SalesQuotationStatus.Approved => "sales.quotation.approve", SalesQuotationStatus.Rejected => "sales.quotation.reject", SalesQuotationStatus.ReturnedForChange => "sales.quotation.return", SalesQuotationStatus.Sent => "sales.quotation.send", SalesQuotationStatus.Withdrawn => "sales.quotation.withdraw", SalesQuotationStatus.Cancelled => "sales.quotation.cancel", _ => "sales.quotation.edit" };
        var authorizationDecision = authorization.Decide(context, operation, Scope(context, existing.CompanyId, existing.BranchId));
        if (!authorizationDecision.Allowed) return SalesOperationResult<SalesQuotationResponse>.Failure(ScopeFailure(authorizationDecision.Code, "quotation_not_found"));
        var policy = await ResolveTransitionPolicyAsync(context, id, existing.CompanyId, existing.BranchId, "quotation", existing.Status, target, existing.Total, existing.CurrencyCode, cancellationToken);
        if (target == SalesQuotationStatus.PendingApproval && policy is null) return SalesOperationResult<SalesQuotationResponse>.Failure("approval_policy_missing");
        if (target == SalesQuotationStatus.Cancelled && existing.Status == SalesQuotationStatus.PendingApproval
            && (policy is null || !policy.AllowRequesterCancellationWhilePending || existing.CreatedByActorId != context.ActorId))
            return SalesOperationResult<SalesQuotationResponse>.Failure("cancellation_not_allowed");
        Guid? delegatedFrom = null;
        if (target == SalesQuotationStatus.Approved)
        {
            if (existing.CreatedByActorId == context.ActorId) return SalesOperationResult<SalesQuotationResponse>.Failure("self_approval_denied");
            if (policy is null) return SalesOperationResult<SalesQuotationResponse>.Failure("approval_policy_missing");
            var stageIndex = existing.ApprovalState?.CurrentStageIndex ?? 0;
            var stage = policy.Stages.OrderBy(item => item.Sequence).ElementAtOrDefault(stageIndex);
            if (stage is null) return SalesOperationResult<SalesQuotationResponse>.Failure("approval_policy_missing");
            if (stage.EligibleApproverIds.Count > 0 && !stage.EligibleApproverIds.Contains(context.ActorId))
            {
                var delegation = await delegations.ResolveAsync(context, Scope(context, existing.CompanyId, existing.BranchId), "quotation", stage, context.ActorId, DateTimeOffset.UtcNow, cancellationToken);
                if (delegation is null) return SalesOperationResult<SalesQuotationResponse>.Failure("approver_not_eligible");
                delegatedFrom = delegation.DelegatorId;
            }
        }
        if (target is SalesQuotationStatus.Sent or SalesQuotationStatus.Approved
            && DateOnly.FromDateTime(DateTime.UtcNow) > existing.ValidUntil)
            return SalesOperationResult<SalesQuotationResponse>.Failure("quotation_expired");
        return await persistence.TransitionQuotationAsync(context, id, target, reason, version, key, Fingerprint(new { id, target, reason, version }), policy, delegatedFrom, cancellationToken);
    }

    public async Task<SalesOperationResult<SalesOrderResponse>> ConvertQuotationAsync(ProcurementRequestContext context, Guid quotationId, byte[] version, string key, CancellationToken cancellationToken = default)
    {
        var quote = await persistence.GetQuotationAsync(context, quotationId, cancellationToken);
        if (quote is null) return SalesOperationResult<SalesOrderResponse>.Failure("quotation_not_found");
        var authorizationDecision = authorization.Decide(context, "sales.quotation.convert", Scope(context, quote.CompanyId, quote.BranchId));
        if (!authorizationDecision.Allowed) return SalesOperationResult<SalesOrderResponse>.Failure(ScopeFailure(authorizationDecision.Code, "quotation_not_found"));
        var policy = await approvalPolicies.ResolveAsync(context, Scope(context, quote.CompanyId, quote.BranchId), "order", quote.Total, DateTimeOffset.UtcNow, cancellationToken, quote.CurrencyCode);
        return await persistence.ConvertQuotationAsync(context, quotationId, version, key, Fingerprint(new { quotationId, version }), policy, cancellationToken);
    }

    public async Task<SalesOperationResult<SalesOrderResponse>> TransitionOrderAsync(ProcurementRequestContext context, Guid id, SalesOrderStatus target, string? reason, byte[] version, string key, CancellationToken cancellationToken = default)
    {
        var existing = await persistence.GetOrderAsync(context, id, cancellationToken);
        if (existing is null) return SalesOperationResult<SalesOrderResponse>.Failure("order_not_found");
        var operation = target switch { SalesOrderStatus.PendingApproval => "sales.order.submit", SalesOrderStatus.Approved => "sales.order.approve", SalesOrderStatus.Rejected => "sales.order.reject", SalesOrderStatus.ReturnedForChange => "sales.order.return", SalesOrderStatus.Confirmed => "sales.order.confirm", SalesOrderStatus.Cancelled => "sales.order.cancel", _ => "sales.order.edit" };
        var authorizationDecision = authorization.Decide(context, operation, Scope(context, existing.CompanyId, existing.BranchId));
        if (!authorizationDecision.Allowed) return SalesOperationResult<SalesOrderResponse>.Failure(ScopeFailure(authorizationDecision.Code, "order_not_found"));
        var policy = await ResolveTransitionPolicyAsync(context, id, existing.CompanyId, existing.BranchId, "order", existing.Status, target, existing.Total, existing.CurrencyCode, cancellationToken);
        if (target == SalesOrderStatus.PendingApproval && policy is null) return SalesOperationResult<SalesOrderResponse>.Failure("approval_policy_missing");
        if (target == SalesOrderStatus.Cancelled && existing.Status == SalesOrderStatus.PendingApproval
            && (policy is null || !policy.AllowRequesterCancellationWhilePending || existing.CreatedByActorId != context.ActorId))
            return SalesOperationResult<SalesOrderResponse>.Failure("cancellation_not_allowed");
        Guid? delegatedFrom = null;
        if (target is SalesOrderStatus.Approved or SalesOrderStatus.Confirmed)
        {
            if (existing.CreatedByActorId == context.ActorId) return SalesOperationResult<SalesOrderResponse>.Failure("self_approval_denied");
            if (policy is null && target == SalesOrderStatus.Approved) return SalesOperationResult<SalesOrderResponse>.Failure("approval_policy_missing");
            var stageIndex = existing.ApprovalState?.CurrentStageIndex ?? 0;
            var stage = policy?.Stages.OrderBy(item => item.Sequence).ElementAtOrDefault(stageIndex);
            if (target == SalesOrderStatus.Approved && stage is null) return SalesOperationResult<SalesOrderResponse>.Failure("approval_policy_missing");
            if (stage is not null && stage.EligibleApproverIds.Count > 0 && !stage.EligibleApproverIds.Contains(context.ActorId))
            {
                var delegation = await delegations.ResolveAsync(context, Scope(context, existing.CompanyId, existing.BranchId), "order", stage, context.ActorId, DateTimeOffset.UtcNow, cancellationToken);
                if (delegation is null) return SalesOperationResult<SalesOrderResponse>.Failure("approver_not_eligible");
                delegatedFrom = delegation.DelegatorId;
            }
        }
        SalesCreditEvaluation? credit = null;
        if (target == SalesOrderStatus.Confirmed)
        {
            credit = await EvaluateCreditAsync(context, existing, cancellationToken);
            if (credit.Outcome is SalesCreditOutcome.Blocked or SalesCreditOutcome.Pending or SalesCreditOutcome.Unknown)
            {
                var prior = await persistence.GetOrderCreditAsync(context, id, cancellationToken);
                if (!CanReuseCreditOverride(existing, prior, credit))
                {
                    return await persistence.TransitionOrderAsync(context, id, SalesOrderStatus.CreditHold, credit.Reason, version, key, Fingerprint(new { id, target, reason, version }), credit, policy, delegatedFrom, cancellationToken);
                }

                credit = credit with
                {
                    Outcome = SalesCreditOutcome.Overridden,
                    Reason = prior!.Reason,
                    OverrideExpiresAt = existing.CreditOverrideExpiresAt
                };
            }
        }
        return await persistence.TransitionOrderAsync(context, id, target, reason, version, key, Fingerprint(new { id, target, reason, version }), credit, policy, delegatedFrom, cancellationToken);
    }

    public async Task<SalesOperationResult<SalesOrderResponse>> OverrideCreditAsync(ProcurementRequestContext context, Guid id, SalesCreditOverrideRequest request, byte[] version, string key, CancellationToken cancellationToken = default)
    {
        var order = await persistence.GetOrderAsync(context, id, cancellationToken);
        if (order is null) return SalesOperationResult<SalesOrderResponse>.Failure("order_not_found");
        var authorizationDecision = authorization.Decide(context, "sales.order.credit.override", Scope(context, order.CompanyId, order.BranchId));
        if (!authorizationDecision.Allowed) return SalesOperationResult<SalesOrderResponse>.Failure(ScopeFailure(authorizationDecision.Code, "order_not_found"));
        if (order.Status != SalesOrderStatus.CreditHold) return SalesOperationResult<SalesOrderResponse>.Failure("credit_override_not_allowed");
        if (order.CreatedByActorId == context.ActorId) return SalesOperationResult<SalesOrderResponse>.Failure("self_approval_denied");
        if (string.IsNullOrWhiteSpace(request.Reason) || request.Reason.Length > 2048 || request.ExpiresAt <= DateTimeOffset.UtcNow) return SalesOperationResult<SalesOrderResponse>.Failure("credit_override_invalid");
        var credit = await EvaluateCreditAsync(context, order, cancellationToken);
        if (credit.Outcome == SalesCreditOutcome.Unknown) return SalesOperationResult<SalesOrderResponse>.Failure("credit_truth_unavailable");
        credit = credit with { Outcome = SalesCreditOutcome.Overridden, Reason = request.Reason, OverrideExpiresAt = request.ExpiresAt };
        return await persistence.OverrideOrderCreditAsync(context, id, request.Reason, request.ExpiresAt, request.Scope, request.SourceReference, version, key, Fingerprint(request), credit, cancellationToken);
    }

    public Task<SalesCreditResponse?> GetOrderCreditAsync(ProcurementRequestContext c, Guid id, CancellationToken x = default) => persistence.GetOrderCreditAsync(c, id, x);

    private async Task<SalesApprovalPolicyDefinition?> ResolveTransitionPolicyAsync(
        ProcurementRequestContext context,
        Guid id,
        Guid companyId,
        Guid? branchId,
        string documentType,
        Enum currentStatus,
        Enum target,
        decimal total,
        string currencyCode,
        CancellationToken cancellationToken)
    {
        if (target is SalesQuotationStatus.PendingApproval || target is SalesOrderStatus.PendingApproval)
        {
            return await approvalPolicies.ResolveAsync(
                context,
                Scope(context, companyId, branchId),
                documentType,
                total,
                DateTimeOffset.UtcNow,
                cancellationToken,
                currencyCode);
        }

        var isDraft = currentStatus is SalesQuotationStatus.Draft or SalesQuotationStatus.ReturnedForChange
            || currentStatus is SalesOrderStatus.Draft or SalesOrderStatus.ReturnedForChange;
        return isDraft
            ? null
            : await persistence.GetApprovalPolicyAsync(context, documentType, id, cancellationToken);
    }

    private async Task<SalesCreditEvaluation> EvaluateCreditAsync(ProcurementRequestContext context, SalesOrderResponse order, CancellationToken cancellationToken)
    {
        var at = DateOnly.FromDateTime(DateTime.UtcNow);
        var transactionCurrency = NormalizeCurrencyCode(order.CurrencyCode);
        if (transactionCurrency is null || order.Total < 0m || order.RevisionNumber <= 0)
            return UnknownCredit(order, at, "sales_order_currency_invalid");

        if (!FinanceRequestContext.TryCreate(context.FoundationContext, out var financeContext) || financeContext is null)
            return UnknownCredit(order, at, "finance_context_unavailable");

        var exposure = await finance.GetExposureAsync(financeContext, new FinanceExposureQuery(order.CompanyId, order.CustomerId, at), cancellationToken);
        if (exposure is null)
            return UnknownCredit(order, at, "credit_truth_unavailable");
        if (exposure.CompanyId != order.CompanyId || exposure.CustomerId != order.CustomerId || exposure.AsOfDate != at)
            return UnknownCredit(order, at, "finance_exposure_invalid");

        var evaluationCurrency = NormalizeCurrencyCode(exposure.CurrencyCode);
        if (evaluationCurrency is null)
            return UnknownCredit(order, at, "finance_exposure_currency_invalid");

        var conversion = ResolveCreditCommitment(order, transactionCurrency, evaluationCurrency);
        if (!conversion.Succeeded)
            return UnknownCredit(order, at, conversion.Code, evaluationCurrency, conversion.ExchangeRateEvidence, conversion.ConvertedOrderCommitment);

        var limit = await creditLimits.ResolveLimitAsync(context, order.CompanyId, order.CustomerId, evaluationCurrency, at, cancellationToken);
        if (limit is null || limit < 0m)
            return UnknownCredit(order, at, "credit_limit_unavailable", evaluationCurrency, conversion.ExchangeRateEvidence, conversion.ConvertedOrderCommitment);

        var proposed = decimal.Round(exposure.NetReceivableExposure + conversion.ConvertedOrderCommitment!.Value, 8, MidpointRounding.ToEven);
        var outcome = exposure.CreditHold ? SalesCreditOutcome.Blocked : proposed <= limit.Value ? SalesCreditOutcome.Eligible : SalesCreditOutcome.Blocked;
        if (outcome == SalesCreditOutcome.Eligible && exposure.OverdueReceivables > 0m) outcome = SalesCreditOutcome.Warning;
        return new(
            outcome,
            outcome == SalesCreditOutcome.Blocked ? (exposure.CreditHold ? "finance_credit_hold" : "credit_limit_exceeded") : exposure.OverdueReceivables > 0m ? "overdue_receivables" : null,
            exposure.OpenReceivables,
            exposure.OverdueReceivables,
            exposure.NetReceivableExposure,
            proposed,
            limit,
            at,
            DateTimeOffset.UtcNow,
            CurrencyCode: evaluationCurrency,
            TransactionCurrencyCode: transactionCurrency,
            TransactionAmount: order.Total,
            ConvertedOrderCommitment: conversion.ConvertedOrderCommitment,
            ExchangeRateEvidence: conversion.ExchangeRateEvidence,
            OrderRevisionNumber: order.RevisionNumber);
    }

    private static SalesCreditEvaluation UnknownCredit(
        SalesOrderResponse order,
        DateOnly asOfDate,
        string reason,
        string? evaluationCurrency = null,
        SalesExchangeRateEvidence? exchangeRateEvidence = null,
        decimal? convertedOrderCommitment = null) =>
        new(
            SalesCreditOutcome.Unknown,
            reason,
            null,
            null,
            null,
            null,
            null,
            asOfDate,
            DateTimeOffset.UtcNow,
            CurrencyCode: evaluationCurrency,
            TransactionCurrencyCode: NormalizeCurrencyCode(order.CurrencyCode) ?? order.CurrencyCode,
            TransactionAmount: order.Total,
            ConvertedOrderCommitment: convertedOrderCommitment,
            ExchangeRateEvidence: exchangeRateEvidence,
            OrderRevisionNumber: order.RevisionNumber);

    private static (bool Succeeded, string Code, decimal? ConvertedOrderCommitment, SalesExchangeRateEvidence? ExchangeRateEvidence) ResolveCreditCommitment(
        SalesOrderResponse order,
        string transactionCurrency,
        string evaluationCurrency)
    {
        if (string.Equals(transactionCurrency, evaluationCurrency, StringComparison.OrdinalIgnoreCase))
            return (true, "resolved", order.Total, null);

        var evidence = order.ExchangeRateEvidence;
        if (evidence is null)
            return (false, "credit_fx_evidence_missing", null, null);
        if (!string.Equals(NormalizeCurrencyCode(evidence.SourceCurrencyCode), transactionCurrency, StringComparison.OrdinalIgnoreCase))
            return (false, "credit_fx_source_currency_mismatch", null, evidence);
        if (!string.Equals(NormalizeCurrencyCode(evidence.TargetCurrencyCode), evaluationCurrency, StringComparison.OrdinalIgnoreCase))
            return (false, "credit_fx_target_currency_mismatch", null, evidence);
        if (evidence.ExchangeRateId == Guid.Empty
            || evidence.ExchangeRateVersionId == Guid.Empty
            || evidence.VersionNumber <= 0
            || evidence.Rate <= 0m
            || evidence.RateScale is <= 0 or > 12
            || decimal.Round(evidence.Rate, evidence.RateScale, MidpointRounding.ToEven) != evidence.Rate)
            return (false, "credit_fx_rate_invalid", null, evidence);
        if (evidence.EffectiveOn == default
            || evidence.EffectiveFrom == default
            || evidence.EffectiveFrom > evidence.EffectiveOn
            || evidence.EffectiveTo is { } effectiveTo && evidence.EffectiveOn > effectiveTo
            || string.IsNullOrWhiteSpace(evidence.ReferenceValue))
            return (false, "credit_fx_evidence_invalid", null, evidence);

        // Finance's established transaction-to-functional convention is amount * rate,
        // with functional monetary precision rounded to 8 places using ToEven.
        var converted = decimal.Round(order.Total * evidence.Rate, 8, MidpointRounding.ToEven);
        return (true, "resolved", converted, evidence);
    }

    private static string? NormalizeCurrencyCode(string? value)
    {
        try
        {
            return string.IsNullOrWhiteSpace(value) ? null : MasterDataCurrencyPaymentTermValuePolicy.NormalizeCode(value);
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private async Task<SalesQuotationWriteModel?> BuildModelAsync(ProcurementRequestContext context, Guid id, Guid companyId, Guid? branchId, Guid customerId, DateOnly quotationDate, DateOnly validUntil, Guid currencyId, Guid? priceListId, Guid? exchangeRateId, string? contactId, string? notes, string? reference, IReadOnlyList<SalesQuotationLineRequest> lineRequests, CancellationToken cancellationToken, string documentType = "quotation")
    {
        if (companyId == Guid.Empty || customerId == Guid.Empty || currencyId == Guid.Empty || validUntil < quotationDate || lineRequests is null || lineRequests.Count == 0 || lineRequests.Count > 500) return null;
        var company = companies.List(context.TenantId).FirstOrDefault(item => item.CompanyId == companyId && item.BranchId == branchId);
        if (company is null) return null;
        var customer = await customers.FindCustomerAsync(context.TenantContext, customerId, cancellationToken);
        if (customer is null || customer.TenantId.Value != context.TenantId.Value || customer.LifecycleState != MasterDataLifecycleState.Active) return null;
        var lines = new List<SalesLineWriteModel>();
        var currencyCode = string.Empty;
        foreach (var line in lineRequests)
        {
            if (line.ProductId == Guid.Empty || line.UnitOfMeasureId == Guid.Empty || line.Quantity <= 0m || line.Quantity > 1_000_000m || line.DiscountPercent < 0m || line.DiscountPercent > 100m) return null;
            var product = await products.FindProductAsync(context.TenantContext, line.ProductId, cancellationToken);
            if (product is null || product.TenantId.Value != context.TenantId.Value || product.LifecycleState != MasterDataLifecycleState.Active || !product.IsSellable) return null;
            var query = new ResolveMasterDataPriceQuery(priceListId, line.ProductId, line.UnitOfMeasureId, currencyId, customerId, branchId is null ? OrganizationScopeKind.Company : OrganizationScopeKind.Branch, branchId ?? companyId, quotationDate);
            var evidence = MasterDataAuditEvidence.CreateTrustedModuleReference("sales.quotation.price.resolve", context.CorrelationId?.Value ?? "sales", context.ActorId, context.SessionId, context.TenantId.Value, branchId ?? companyId, branchId is null ? OrganizationScopeKind.Company : OrganizationScopeKind.Branch, $"product={line.ProductId:N};effective={quotationDate:yyyy-MM-dd}");
            var price = await prices.ResolvePriceAsync(context.TenantContext, query, evidence, cancellationToken);
            if (!price.Succeeded || price.Value is null) return null;
            var source = price.Value;
            if (source.TenantId.Value != context.TenantId.Value
                || source.Price.ProductId != line.ProductId
                || source.Price.UnitOfMeasureId != line.UnitOfMeasureId
                || source.Price.CurrencyId != currencyId
                || source.EffectiveOn != quotationDate
                || priceListId is { } requestedPriceListId && source.PriceListId != requestedPriceListId
                || source.Price.CustomerId is { } pricedCustomerId && pricedCustomerId != customerId
                || source.Price.OrganizationScopeKind == OrganizationScopeKind.Company && source.Price.OrganizationScopeId != companyId
                || source.Price.OrganizationScopeKind == OrganizationScopeKind.Branch && source.Price.OrganizationScopeId != branchId) return null;
            if (string.IsNullOrWhiteSpace(currencyCode)) currencyCode = source.Price.CurrencyCode;
            if (!string.Equals(currencyCode, source.Price.CurrencyCode, StringComparison.OrdinalIgnoreCase)) return null;
            var requiresAuthority = line.UnitPriceOverride is not null || line.DiscountPercent != 0m;
            SalesCommercialAuthority? authority = null;
            if (requiresAuthority)
            {
                authority = await commercialAuthorities.ResolveAsync(context, Scope(context, companyId, branchId), documentType, context.ActorId, DateTimeOffset.UtcNow, cancellationToken);
                if (authority is null || line.UnitPriceOverride is not null && !authority.AllowManualPrice || line.DiscountPercent > authority.MaximumDiscountPercent) return null;
            }
            var appliedUnitPrice = decimal.Round(line.UnitPriceOverride ?? source.Price.Price, 8, MidpointRounding.AwayFromZero);
            var gross = decimal.Round(appliedUnitPrice * line.Quantity, 2, MidpointRounding.AwayFromZero);
            var discountAmount = decimal.Round(gross * line.DiscountPercent / 100m, 2, MidpointRounding.AwayFromZero);
            var lineTotal = gross - discountAmount;
            SalesTaxEvidence? taxEvidence = null;
            if (line.TaxId is { } taxId)
            {
                var tax = await taxes.ResolveAsync(context, taxId, quotationDate, lineTotal, currencyCode, $"sales.quotation:{id:N}:line:{line.ProductId:N}", cancellationToken);
                if (!tax.Succeeded || tax.Value is null) return null;
                taxEvidence = tax.Value;
            }
            var authorityEvidence = authority is null ? null : $"policy={authority.PolicyId};v{authority.Version};actor={context.ActorId:N};resolved={source.Price.Price.ToString(System.Globalization.CultureInfo.InvariantCulture)};applied={appliedUnitPrice.ToString(System.Globalization.CultureInfo.InvariantCulture)};discount={line.DiscountPercent.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
            lines.Add(new SalesLineWriteModel(Guid.NewGuid(), product.Id, product.Sku, product.Name.English ?? product.Name.Arabic ?? product.Sku, line.UnitOfMeasureId, source.Price.UnitOfMeasureCode, line.Quantity, appliedUnitPrice, source.Price.Price, line.DiscountPercent, discountAmount, taxEvidence?.TaxAmount ?? 0m, lineTotal, source.PriceListId, source.Price.VersionNumber, source.Price.EffectiveFrom, source.Price.Provenance.ToString(), source.Price.SourceReference, requiresAuthority, authority?.PolicyId, requiresAuthority ? context.ActorId : null, authorityEvidence, line.Notes, taxEvidence));
        }
        var isFunctionalCurrency = company.FunctionalCurrencyCode.Equals(currencyCode, StringComparison.OrdinalIgnoreCase);
        SalesExchangeRateEvidence? exchangeRate = null;
        if (!isFunctionalCurrency)
        {
            exchangeRate = await ResolveExchangeRateAsync(
                context,
                exchangeRateId,
                currencyCode,
                company.FunctionalCurrencyCode,
                quotationDate,
                cancellationToken);
        }
        if (!isFunctionalCurrency && exchangeRate is null) return null;
        if (isFunctionalCurrency && exchangeRateId is not null) return null;
        var subtotal = decimal.Round(lines.Sum(item => item.UnitPrice * item.Quantity), 2, MidpointRounding.AwayFromZero);
        var discount = decimal.Round(lines.Sum(item => item.DiscountAmount), 2, MidpointRounding.AwayFromZero);
        var taxAmount = decimal.Round(lines.Sum(item => item.TaxAmount), 2, MidpointRounding.AwayFromZero);
        var total = decimal.Round(subtotal - discount + taxAmount, 2, MidpointRounding.AwayFromZero);
        return new(id, companyId, branchId, customerId, customer.Code, customer.TradingName?.English ?? customer.TradingName?.Arabic ?? customer.LegalName.English ?? customer.LegalName.Arabic ?? customer.Code, quotationDate, validUntil, currencyId, currencyCode, contactId, notes, reference, lines, subtotal, discount, taxAmount, total, null, exchangeRate);
    }

    private async Task<SalesExchangeRateEvidence?> ResolveExchangeRateAsync(ProcurementRequestContext context, Guid? exchangeRateId, string sourceCurrencyCode, string targetCurrencyCode, DateOnly effectiveOn, CancellationToken cancellationToken)
    {
        if (exchangeRateId is not { } id || id == Guid.Empty) return null;
        var result = await exchangeRates.ResolveAsync(context.TenantContext, id, sourceCurrencyCode, targetCurrencyCode, effectiveOn, cancellationToken);
        return result.Succeeded ? result.Value : null;
    }

    private static SalesScope Scope(ProcurementRequestContext context, Guid companyId, Guid? branchId) => new(context.TenantId.Value, companyId, branchId);
    private static string ScopeFailure(string code, string notFoundCode) => code is "resource_scope_denied" or "cross_tenant_target_denied" ? notFoundCode : "permission_denied";
    private static bool CanReuseCreditOverride(SalesOrderResponse order, SalesCreditResponse? prior, SalesCreditEvaluation current) =>
        order.CreditOutcome == SalesCreditOutcome.Overridden
        && order.CreditOverrideExpiresAt is { } expiry
        && expiry > DateTimeOffset.UtcNow
        && prior is not null
        && prior.Outcome == SalesCreditOutcome.Overridden
        && prior.OpenReceivables == current.OpenReceivables
        && prior.OverdueReceivables == current.OverdueReceivables
        && prior.NetReceivableExposure == current.NetReceivableExposure
        && prior.ProposedExposure == current.ProposedExposure
        && prior.CreditLimit == current.CreditLimit
        && string.Equals(prior.CurrencyCode, current.CurrencyCode, StringComparison.OrdinalIgnoreCase)
        && string.Equals(prior.TransactionCurrencyCode, current.TransactionCurrencyCode, StringComparison.OrdinalIgnoreCase)
        && prior.TransactionAmount == current.TransactionAmount
        && prior.ConvertedOrderCommitment == current.ConvertedOrderCommitment
        && prior.ExchangeRateEvidence == current.ExchangeRateEvidence
        && prior.OrderRevisionNumber == current.OrderRevisionNumber;
    private static string Fingerprint<T>(T value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value))));
}

#pragma warning restore CS1591
