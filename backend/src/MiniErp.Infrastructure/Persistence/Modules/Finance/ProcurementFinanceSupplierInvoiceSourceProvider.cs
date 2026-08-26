#pragma warning disable CS1591

using System.Text.Json;
using MiniErp.App.Modules.Finance;
using MiniErp.App.Modules.MasterData;
using MiniErp.App.Modules.Procurement;
using MiniErp.App.Modules.BusinessParties;
using MiniErp.Contracts.Modules.Finance;
using MiniErp.Contracts.Modules.Procurement;
using MiniErp.Contracts.Modules.MasterData;

namespace MiniErp.Infrastructure.Persistence.Modules.Finance;

internal sealed class ProcurementFinanceSupplierInvoiceSourceProvider(
    IPurchaseInvoiceHandoffPersistence handoffs,
    IPurchaseInvoiceMatchPersistence matches,
    IFinanceCompanyProvider companies,
    IPurchaseOrderPersistence purchaseOrders,
    IMasterDataCurrencyPaymentTermPersistence paymentTerms,
    ISupplierPersistence suppliers) : IFinanceSupplierInvoiceSourceProvider
{
    public async Task<FinanceSupplierInvoiceSourceRecord?> FindAsync(FinanceRequestContext context, Guid sourceEvidenceId, CancellationToken cancellationToken = default)
    {
        var match = await matches.FindAsync(context.TenantContext, sourceEvidenceId, cancellationToken);
        if (match is null || match.TenantId != context.TenantId.Value || match.Lifecycle != PurchaseInvoiceMatchLifecycle.Current || match.Result is not (PurchaseInvoiceMatchResult.ExactMatch or PurchaseInvoiceMatchResult.WithinTolerance or PurchaseInvoiceMatchResult.ResolvedException)) return null;
        var handoff = await handoffs.FindAsync(context.TenantContext, match.PurchaseInvoiceHandoffId, cancellationToken);
        if (handoff is null || handoff.TenantId != context.TenantId.Value || handoff.Status == PurchaseInvoiceHandoffStatus.Cancelled) return null;
        var company = companies.List(context.TenantId).SingleOrDefault(item => item.CompanyId == handoff.Scope.CompanyId && item.IsActive);
        if (company is null) return null;
        var purchaseOrder = await purchaseOrders.FindAsync(context.TenantContext, handoff.PurchaseOrderId, cancellationToken);
        var purchaseOrderTerm = purchaseOrder?.Source.PaymentTerm;
        if (purchaseOrder is null
            || purchaseOrder.TenantId != context.TenantId.Value
            || purchaseOrder.Scope.CompanyId != handoff.Scope.CompanyId
            || purchaseOrder.Status == PurchaseOrderStatus.Cancelled
            || purchaseOrder.Source.Supplier.Id != handoff.SupplierId
            || purchaseOrderTerm is null
            || purchaseOrderTerm.Id == Guid.Empty
            || purchaseOrderTerm.Version <= 0)
        {
            return null;
        }

        // The handoff's supplier code/name are historical invoice evidence.
        // Current source readiness is owned by the Supplier master, and the
        // master is resolved through its Tenant-authorized reference seam.
        SupplierRecord? supplier;
        try
        {
            supplier = await suppliers.FindSupplierAsync(context.TenantContext, handoff.SupplierId, cancellationToken);
        }
        catch
        {
            return null;
        }

        if (supplier is null
            || supplier.TenantId.Value != context.TenantId.Value
            || supplier.LifecycleState != MasterDataLifecycleState.Active)
        {
            return null;
        }

        // The PO snapshot owns the historical version number. We deliberately
        // resolve that exact version rather than the current master-data version;
        // a later term edit must never change an already-issued PO's AP schedule.
        var term = await paymentTerms.FindPaymentTermAsync(context.TenantContext, purchaseOrderTerm.Id, cancellationToken);
        var termVersion = term?.Versions.SingleOrDefault(item => item.VersionNumber == purchaseOrderTerm.Version);
        if (term is null
            || term.TenantId.Value != context.TenantId.Value
            || term.LifecycleState != MasterDataLifecycleState.Active
            || termVersion is null
            || !string.Equals(termVersion.Code, purchaseOrderTerm.Code, StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }
        var evidence = handoff.DeclaredEvidence;
        var declaredTaxes = evidence?.Lines
            .Where(line => line.TaxCode is not null || line.TaxRatePercentage is not null || line.TaxAmount is not null || line.NetAmount is not null)
            .Select(line => new FinanceSupplierDeclaredTaxRecord(line.TaxCode, line.TaxRatePercentage, line.TaxAmount, line.NetAmount))
            .ToArray();
        var amount = evidence?.GrossAmount ?? evidence?.Lines.Sum(line => line.GrossAmount ?? line.NetAmount ?? 0m) ?? handoff.Lines.Sum(line => line.LineAmount);
        if (amount <= 0m) return null;
        var currency = evidence?.CurrencyCode ?? handoff.CurrencyCode;
        var applied = match.AppliedExchangeRate;
        var functionalCurrency = company.FunctionalCurrencyCode.Trim().ToUpperInvariant();
        var functionalAmount = string.Equals(currency, functionalCurrency, StringComparison.OrdinalIgnoreCase) ? amount : applied is null ? 0m : amount * applied.Rate;
        if (functionalAmount <= 0m) return null;
        // CreatedAt is persistence metadata, never commercial evidence. The
        // MESP-126 upstream contract currently exposes SupplierInvoiceDate as
        // its only trusted commercial document date, so both InvoiceDate and
        // DocumentDate terms use that value and fail closed when it is absent.
        var invoiceDate = evidence?.SupplierInvoiceDate ?? handoff.SupplierInvoiceDate;
        if (invoiceDate is null) return null;
        var trustedInvoiceDate = invoiceDate.Value;
        var baseDate = termVersion.BaseDateRule switch
        {
            PaymentTermBaseDateRule.InvoiceDate or PaymentTermBaseDateRule.DocumentDate => trustedInvoiceDate,
            // MESP-125/126 do not expose a trusted receipt/delivery date on the
            // Finance source contract. Do not guess one from the current clock.
            _ => (DateOnly?)null
        };
        if (baseDate is null) return null;
        var dueDate = termVersion.ScheduleMode == PaymentTermScheduleMode.SingleDueDate
            ? AddOffset(baseDate.Value, termVersion.DueOffset)
            : termVersion.Installments.OrderBy(item => item.Sequence).Select(item => AddOffset(baseDate.Value, item.Offset)).LastOrDefault(baseDate.Value);
        var paymentTerm = new FinancePaymentTermSnapshotRecord(
            purchaseOrderTerm.Id,
            purchaseOrderTerm.Code,
            purchaseOrderTerm.Name,
            null,
            purchaseOrderTerm.Version,
            termVersion.Id,
            termVersion.EffectiveFrom,
            dueDate);
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
            trustedInvoiceDate,
            currency,
            amount,
            functionalCurrency,
            functionalAmount,
            string.Equals(currency, functionalCurrency, StringComparison.OrdinalIgnoreCase) ? 1m : applied?.Rate,
            string.Equals(currency, functionalCurrency, StringComparison.OrdinalIgnoreCase) ? null : applied?.ExchangeRateId,
            string.Equals(currency, functionalCurrency, StringComparison.OrdinalIgnoreCase) ? null : applied?.ExchangeRateVersionId,
            string.Equals(currency, functionalCurrency, StringComparison.OrdinalIgnoreCase) ? null : applied?.VersionNumber,
            paymentTerm,
            dueDate,
            match.Id,
            1,
            JsonSerializer.Serialize(new { Handoff = handoff, Match = match, PurchaseOrder = purchaseOrder.Id, PaymentTerm = paymentTerm, PaymentTermVersion = termVersion }),
            match.Id.ToString("N"),
            handoff.SupplierCode,
            handoff.SupplierName,
            match.Result,
            evidence?.CurrencyCode,
            evidence?.SupplierInvoiceDate,
            evidence?.TaxAmount,
            declaredTaxes);
    }

    public async Task<IReadOnlyList<FinanceSupplierInvoiceSourceRecord>> ListAsync(
        FinanceRequestContext context,
        Guid? companyId = null,
        CancellationToken cancellationToken = default)
    {
        var matchRecords = await matches.ListAsync(context.TenantContext, null, null, cancellationToken);
        var result = new List<FinanceSupplierInvoiceSourceRecord>();
        foreach (var match in matchRecords
            .Where(item => item.Lifecycle == PurchaseInvoiceMatchLifecycle.Current
                && item.Result is (PurchaseInvoiceMatchResult.ExactMatch
                    or PurchaseInvoiceMatchResult.WithinTolerance
                    or PurchaseInvoiceMatchResult.ResolvedException))
            .OrderByDescending(item => item.EvaluatedAt)
            .Take(1000))
        {
            var source = await FindAsync(context, match.Id, cancellationToken);
            if (source is not null && (companyId is null || source.CompanyId == companyId))
            {
                result.Add(source);
            }
        }

        return result;
    }

    private static DateOnly AddOffset(DateOnly date, MasterDataPaymentTermOffset offset) => date.AddMonths(offset.Months).AddDays(offset.Days);
}

#pragma warning restore CS1591
