#pragma warning disable CS1591

using System.Text.Json;
using MiniErp.App.Modules.Finance;
using MiniErp.App.Modules.Procurement;
using MiniErp.Contracts.Modules.Procurement;

namespace MiniErp.Infrastructure.Persistence.Modules.Finance;

internal sealed class ProcurementFinanceSupplierInvoiceSourceProvider(
    IPurchaseInvoiceHandoffPersistence handoffs,
    IPurchaseInvoiceMatchPersistence matches,
    IFinanceCompanyProvider companies) : IFinanceSupplierInvoiceSourceProvider
{
    public async Task<FinanceSupplierInvoiceSourceRecord?> FindAsync(FinanceRequestContext context, Guid sourceEvidenceId, CancellationToken cancellationToken = default)
    {
        var match = await matches.FindAsync(context.TenantContext, sourceEvidenceId, cancellationToken);
        if (match is null || match.TenantId != context.TenantId.Value || match.Lifecycle != PurchaseInvoiceMatchLifecycle.Current || match.Result is not (PurchaseInvoiceMatchResult.ExactMatch or PurchaseInvoiceMatchResult.WithinTolerance or PurchaseInvoiceMatchResult.ResolvedException)) return null;
        var handoff = await handoffs.FindAsync(context.TenantContext, match.PurchaseInvoiceHandoffId, cancellationToken);
        if (handoff is null || handoff.TenantId != context.TenantId.Value || handoff.Status == PurchaseInvoiceHandoffStatus.Cancelled) return null;
        var company = companies.List(context.TenantId).SingleOrDefault(item => item.CompanyId == handoff.Scope.CompanyId && item.IsActive);
        if (company is null) return null;
        var evidence = handoff.DeclaredEvidence;
        var amount = evidence?.GrossAmount ?? evidence?.Lines.Sum(line => line.GrossAmount ?? line.NetAmount ?? 0m) ?? handoff.Lines.Sum(line => line.LineAmount);
        if (amount <= 0m) return null;
        var currency = evidence?.CurrencyCode ?? handoff.CurrencyCode;
        var applied = match.AppliedExchangeRate;
        var functionalCurrency = company.FunctionalCurrencyCode.Trim().ToUpperInvariant();
        var functionalAmount = string.Equals(currency, functionalCurrency, StringComparison.OrdinalIgnoreCase) ? amount : applied is null ? 0m : amount * applied.Rate;
        if (functionalAmount <= 0m) return null;
        var invoiceDate = evidence?.SupplierInvoiceDate ?? handoff.SupplierInvoiceDate ?? DateOnly.FromDateTime(handoff.CreatedAt.UtcDateTime);
        return new FinanceSupplierInvoiceSourceRecord(
            context.TenantId.Value,
            handoff.Scope.CompanyId,
            handoff.SupplierId,
            "procurement-supplier-invoice.v1",
            handoff.Id,
            1,
            match.Id,
            1,
            evidence?.SupplierInvoiceReference ?? handoff.SupplierInvoiceReference,
            invoiceDate,
            currency,
            amount,
            functionalCurrency,
            functionalAmount,
            string.Equals(currency, functionalCurrency, StringComparison.OrdinalIgnoreCase) ? 1m : applied?.Rate,
            string.Equals(currency, functionalCurrency, StringComparison.OrdinalIgnoreCase) ? null : applied?.ExchangeRateId,
            string.Equals(currency, functionalCurrency, StringComparison.OrdinalIgnoreCase) ? null : applied?.ExchangeRateVersionId,
            string.Equals(currency, functionalCurrency, StringComparison.OrdinalIgnoreCase) ? null : applied?.VersionNumber,
            null,
            null,
            match.Id,
            1,
            JsonSerializer.Serialize(new { Handoff = handoff, Match = match }),
            match.Id.ToString("N"));
    }
}

#pragma warning restore CS1591
