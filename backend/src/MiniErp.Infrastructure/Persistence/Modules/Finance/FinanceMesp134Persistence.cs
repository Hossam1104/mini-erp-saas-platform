#pragma warning disable CS1591

using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using MiniErp.App.Modules.Finance;
using MiniErp.App.Modules.MasterData;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.Contracts.Modules.Finance;
using MiniErp.Contracts.Modules.MasterData;

namespace MiniErp.Infrastructure.Persistence.Modules.Finance;

/// <summary>
/// MESP-134 integration boundary. It stores Finance-owned evidence and effects
/// while consuming the existing Tax, Currency, Exchange Rate, journal, and
/// AP/AR persistence authorities.
/// </summary>
public sealed class FinanceMesp134Persistence : IFinanceMesp134Persistence
{
    private readonly DbContextOptions options;
    private readonly IFinanceCompanyProvider companies;
    private readonly IMasterDataCurrencyPaymentTermPersistence currencies;
    private readonly IMasterDataExchangeRatePersistence exchangeRates;
    private readonly MasterDataTaxService taxService;
    private readonly IFinanceSupplierInvoiceSourceProvider supplierInvoiceSources;

    public FinanceMesp134Persistence(
        DbContextOptions options,
        IFinanceCompanyProvider companies,
        IMasterDataCurrencyPaymentTermPersistence currencies,
        IMasterDataExchangeRatePersistence exchangeRates,
        MasterDataTaxService taxService,
        IFinanceSupplierInvoiceSourceProvider supplierInvoiceSources)
    { this.options = options; this.companies = companies; this.currencies = currencies; this.exchangeRates = exchangeRates; this.taxService = taxService; this.supplierInvoiceSources = supplierInvoiceSources; }

    private FinanceDbContext Create(FinanceRequestContext context) => new(options, context.TenantContext);
    private static FinanceOperationResult<T> Failure<T>(string code) => FinanceOperationResult<T>.Failure(code);
    private FinanceCompanyOption? Company(FinanceRequestContext context, Guid companyId)
    {
        var match = companies.List(context.TenantId).Where(item => item.CompanyId == companyId && item.IsActive).ToArray();
        if (match.Length != 1) return null;
        if (context.TenantContext.Scope is { } scope && scope.Value.StartsWith("Company:", StringComparison.OrdinalIgnoreCase) && (!Guid.TryParse(scope.Value["Company:".Length..], out var scoped) || scoped != companyId)) return null;
        return match[0] with { FunctionalCurrencyCode = match[0].FunctionalCurrencyCode.Trim().ToUpperInvariant() };
    }

    public async Task<IReadOnlyList<FinanceMonetaryPolicyRecord>> ListMonetaryPoliciesAsync(FinanceRequestContext context, Guid companyId, CancellationToken cancellationToken = default)
    { if (Company(context, companyId) is null) return []; await using var db = Create(context); var values = await db.MonetaryPolicies.AsNoTracking().Where(item => item.CompanyId == companyId).OrderByDescending(item => item.EffectiveFrom).ToListAsync(cancellationToken); return values.Select(ToPolicy).ToArray(); }

    public async Task<FinanceOperationResult<FinanceMonetaryPolicyRecord>> CreateMonetaryPolicyAsync(FinanceRequestContext context, FinanceMonetaryPolicyCommand command, CancellationToken cancellationToken = default)
    {
        var company = Company(context, command.CompanyId); if (company is null) return Failure<FinanceMonetaryPolicyRecord>("company_scope_denied");
        if (command.EffectiveFrom == default || command.EffectiveTo is { } end && end < command.EffectiveFrom || command.RoundingScale is < 0 or > 8 || !string.Equals(command.RoundingMode, "ToEven", StringComparison.OrdinalIgnoreCase) && !string.Equals(command.RoundingMode, "AwayFromZero", StringComparison.OrdinalIgnoreCase)) return Failure<FinanceMonetaryPolicyRecord>("validation_failed");
        string? reportingCode = null;
        if (command.ReportingCurrencyId is { } reportingId)
        {
            var currency = await currencies.FindCurrencyAsync(context.TenantContext, reportingId, cancellationToken);
            if (currency is null || currency.LifecycleState != MasterDataLifecycleState.Active) return Failure<FinanceMonetaryPolicyRecord>("reporting_currency_not_configured");
            reportingCode = currency.Code.Trim().ToUpperInvariant();
        }
        if (string.Equals(reportingCode, company.FunctionalCurrencyCode, StringComparison.OrdinalIgnoreCase)) reportingCode = company.FunctionalCurrencyCode;
        await using var db = Create(context); await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var replay = await ReadReplayAsync<FinanceMonetaryPolicyRecord>(db, context, "finance.monetary-policy.create", command.IdempotencyKey, command.RequestFingerprint, cancellationToken); if (replay is not null) return replay;
        if (await db.MonetaryPolicies.AnyAsync(item => item.CompanyId == command.CompanyId && item.EffectiveFrom <= (command.EffectiveTo ?? DateOnly.MaxValue) && (item.EffectiveTo ?? DateOnly.MaxValue) >= command.EffectiveFrom, cancellationToken)) return Failure<FinanceMonetaryPolicyRecord>("monetary_policy_overlaps");
        var versionNumber = (await db.MonetaryPolicies.Where(item => item.CompanyId == command.CompanyId).Select(item => (int?)item.VersionNumber).MaxAsync(cancellationToken) ?? 0) + 1; var entity = new FinanceMonetaryPolicyEntity(context.TenantId, command, company.FunctionalCurrencyCode, reportingCode, versionNumber); db.MonetaryPolicies.Add(entity); var now = DateTimeOffset.UtcNow; AddAudit(db, context, "finance.monetary-policy.create", "monetary-policy", entity.Id, "Succeeded", null, command.IdempotencyKey, now); await db.SaveChangesAsync(cancellationToken); var result = FinanceOperationResult<FinanceMonetaryPolicyRecord>.Success(ToPolicy(entity)); AddReplay(db, context, "finance.monetary-policy.create", command.IdempotencyKey, command.RequestFingerprint, "monetary-policy", entity.Id, result.Value!, now); await db.SaveChangesAsync(cancellationToken); await tx.CommitAsync(cancellationToken); return result;
    }

    public async Task<IReadOnlyList<FinanceTaxAccountingEffectRecord>> ListTaxEffectsAsync(FinanceRequestContext context, Guid companyId, CancellationToken cancellationToken = default)
    { if (Company(context, companyId) is null) return []; await using var db = Create(context); var values = await db.TaxAccountingEffects.AsNoTracking().Where(item => item.CompanyId == companyId).OrderByDescending(item => item.CreatedAt).ToListAsync(cancellationToken); return values.Select(ToTaxEffect).ToArray(); }

    public async Task<FinanceTaxAccountingEffectRecord?> PreviewTaxAsync(FinanceRequestContext context, FinanceTaxAccountingCommand command, CancellationToken cancellationToken = default)
    {
        await using var db = Create(context); var item = await db.OpenItems.AsNoTracking().SingleOrDefaultAsync(value => value.Id == command.OpenItemId && value.CompanyId == command.CompanyId, cancellationToken); if (item is null || Company(context, command.CompanyId) is null) return null;
        var policy = await PolicyAtAsync(db, command.CompanyId, item.DocumentDate, cancellationToken); if (policy is null) return null;
        var calculation = await CalculateTaxAsync(context, policy, command.TaxId, item, command.TaxableBase, command.SourceLineage, cancellationToken); if (calculation is null) return null;
        if (await ValidateSupplierDeclaredTaxAsync(context, item, calculation, command.TaxableBase, cancellationToken) is not null) return null;
        var evidence = await BuildEvidenceAsync(context, policy, item, calculation.TaxAmount, item.DocumentDate, cancellationToken); if (evidence is null) return null;
        var recognition = item.RecognitionJournalId is { } journalId ? await db.Journals.AsNoTracking().Include(value => value.Lines).SingleOrDefaultAsync(value => value.Id == journalId, cancellationToken) : null;
        var rule = await FindRuleAsync(db, command.CompanyId, "finance-tax.v1", item.Kind == FinanceOpenItemKind.Payable ? "input" : "output", item.DocumentDate, cancellationToken);
        var sourceAccount = ResolveSourceAccount(item.Kind, recognition);
        if (rule is null || sourceAccount is null) return null;
        return new FinanceTaxAccountingEffectRecord(Guid.Empty, context.TenantId.Value, command.CompanyId, item.Id, item.Kind, calculation.TaxId, calculation.Code, calculation.RateVersionId, calculation.RateVersionNumber, calculation.EffectiveOn, calculation.RatePercentage, calculation.TaxableBase, calculation.TaxAmount, item.CurrencyCode, evidence.FunctionalAmount, item.FunctionalCurrencyCode, Guid.Empty, null, rule.Id, rule.VersionNumber, evidence, FinanceEvidenceStatus.Captured, DateTimeOffset.UtcNow, context.ActorId, []);
    }

    public async Task<FinanceOperationResult<FinanceTaxAccountingEffectRecord>> PostTaxAsync(FinanceRequestContext context, FinanceTaxAccountingCommand command, CancellationToken cancellationToken = default)
    {
        await using var db = Create(context); await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var replay = await ReadReplayAsync<FinanceTaxAccountingEffectRecord>(db, context, "finance.tax-accounting.post", command.IdempotencyKey, command.RequestFingerprint, cancellationToken); if (replay is not null) return replay;
        var item = await db.OpenItems.SingleOrDefaultAsync(value => value.Id == command.OpenItemId && value.CompanyId == command.CompanyId, cancellationToken); if (item is null || Company(context, command.CompanyId) is null) return Failure<FinanceTaxAccountingEffectRecord>("company_scope_denied");
        if (await db.TaxAccountingEffects.AnyAsync(value => value.OpenItemId == item.Id && value.ReversalJournalId == null, cancellationToken)) return Failure<FinanceTaxAccountingEffectRecord>("tax_already_posted");
        var policy = await PolicyAtAsync(db, command.CompanyId, item.DocumentDate, cancellationToken); if (policy is null) return Failure<FinanceTaxAccountingEffectRecord>("monetary_policy_not_configured");
        var calculation = await CalculateTaxAsync(context, policy, command.TaxId, item, command.TaxableBase, command.SourceLineage, cancellationToken); if (calculation is null) return Failure<FinanceTaxAccountingEffectRecord>("tax_evidence_not_authoritative");
        var supplierTaxError = await ValidateSupplierDeclaredTaxAsync(context, item, calculation, command.TaxableBase, cancellationToken); if (supplierTaxError is not null) return Failure<FinanceTaxAccountingEffectRecord>(supplierTaxError);
        var evidence = await BuildEvidenceAsync(context, policy, item, calculation.TaxAmount, item.DocumentDate, cancellationToken); if (evidence is null) return Failure<FinanceTaxAccountingEffectRecord>(await ReportingEvidenceFailureCodeAsync(context, policy, item, item.DocumentDate, cancellationToken));
        var recognition = item.RecognitionJournalId is { } recognitionId ? await db.Journals.Include(value => value.Lines).SingleOrDefaultAsync(value => value.Id == recognitionId, cancellationToken) : null; var sourceAccount = ResolveSourceAccount(item.Kind, recognition); if (sourceAccount is null) return Failure<FinanceTaxAccountingEffectRecord>("tax_source_account_mismatch");
        var rule = await FindRuleAsync(db, command.CompanyId, "finance-tax.v1", item.Kind == FinanceOpenItemKind.Payable ? "input" : "output", item.DocumentDate, cancellationToken); if (rule is null) return Failure<FinanceTaxAccountingEffectRecord>("tax_posting_rule_not_configured");
        var expectedSource = item.Kind == FinanceOpenItemKind.Payable ? rule.CreditAccountId : rule.DebitAccountId; if (expectedSource != sourceAccount.Value) return Failure<FinanceTaxAccountingEffectRecord>("tax_source_account_mismatch");
        var taxAccount = item.Kind == FinanceOpenItemKind.Payable ? rule.DebitAccountId : rule.CreditAccountId; var lines = item.Kind == FinanceOpenItemKind.Payable ? new[] { new JournalLine(taxAccount, calculation.TaxAmount, 0m, evidence.FunctionalAmount, "Tax Input"), new JournalLine(sourceAccount.Value, 0m, calculation.TaxAmount, evidence.FunctionalAmount, "Expense reclassification") } : new[] { new JournalLine(sourceAccount.Value, calculation.TaxAmount, 0m, evidence.FunctionalAmount, "Revenue reclassification"), new JournalLine(taxAccount, 0m, calculation.TaxAmount, evidence.FunctionalAmount, "Tax Output") };
        var journal = await CreatePostedJournalAsync(db, context, command.CompanyId, item.DocumentDate, item.CurrencyCode, calculation.TaxAmount, item.ExchangeRate, item.ExchangeRateId, item.ExchangeRateVersionId, item.ExchangeRateVersionNumber, "finance-tax.v1", item.Kind == FinanceOpenItemKind.Payable ? "input" : "output", command.Id, 1, rule, lines, "Finance Tax accounting reclassification", evidence, cancellationToken); if (!journal.Succeeded || journal.Value is null) return Failure<FinanceTaxAccountingEffectRecord>(journal.Code);
        var effect = new FinanceTaxAccountingEffectEntity(context.TenantId, command.Id, command.CompanyId, item.Id, item.Kind, calculation.TaxId, calculation.Code, calculation.RateVersionId, calculation.RateVersionNumber, calculation.EffectiveOn, calculation.RatePercentage, calculation.TaxableBase, calculation.TaxAmount, item.CurrencyCode, evidence.FunctionalAmount, item.FunctionalCurrencyCode, journal.Value.Id, rule.Id, rule.VersionNumber, evidence, context.ActorId, DateTimeOffset.UtcNow); db.TaxAccountingEffects.Add(effect); var now = DateTimeOffset.UtcNow; AddAudit(db, context, "finance.tax-accounting.post", "tax-accounting-effect", effect.Id, "Succeeded", null, command.IdempotencyKey, now); await db.SaveChangesAsync(cancellationToken); var result = FinanceOperationResult<FinanceTaxAccountingEffectRecord>.Success(ToTaxEffect(effect)); AddReplay(db, context, "finance.tax-accounting.post", command.IdempotencyKey, command.RequestFingerprint, "tax-accounting-effect", effect.Id, result.Value!, now); await db.SaveChangesAsync(cancellationToken); await tx.CommitAsync(cancellationToken); return result;
    }

    public async Task<FinanceOperationResult<FinanceTaxAccountingEffectRecord>> ReverseTaxAsync(FinanceRequestContext context, FinanceTaxAccountingReversalCommand command, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.Reason)) return Failure<FinanceTaxAccountingEffectRecord>("reason_required"); await using var db = Create(context); await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken); var replay = await ReadReplayAsync<FinanceTaxAccountingEffectRecord>(db, context, "finance.tax-accounting.reverse", command.IdempotencyKey, command.RequestFingerprint, cancellationToken); if (replay is not null) return replay; var effect = await db.TaxAccountingEffects.SingleOrDefaultAsync(item => item.Id == command.EffectId, cancellationToken); if (effect is null || Company(context, effect.CompanyId) is null) return Failure<FinanceTaxAccountingEffectRecord>("company_scope_denied"); if (!effect.Version.SequenceEqual(command.ExpectedVersion)) return Failure<FinanceTaxAccountingEffectRecord>("concurrency_conflict"); if (effect.ReversalJournalId is not null) return Failure<FinanceTaxAccountingEffectRecord>("tax_already_reversed"); var original = await db.Journals.Include(item => item.Lines).SingleOrDefaultAsync(item => item.Id == effect.JournalId, cancellationToken); if (original is null) return Failure<FinanceTaxAccountingEffectRecord>("posting_lineage_missing"); var reversal = await CreateExactReversalAsync(db, context, original, DateOnly.FromDateTime(DateTime.UtcNow), command.Reason, cancellationToken); if (!reversal.Succeeded || reversal.Value is null) return Failure<FinanceTaxAccountingEffectRecord>(reversal.Code); effect.SetReversal(reversal.Value.Id); var now = DateTimeOffset.UtcNow; AddAudit(db, context, "finance.tax-accounting.reverse", "tax-accounting-effect", effect.Id, "Succeeded", command.Reason, command.IdempotencyKey, now); await db.SaveChangesAsync(cancellationToken); var result = FinanceOperationResult<FinanceTaxAccountingEffectRecord>.Success(ToTaxEffect(effect)); AddReplay(db, context, "finance.tax-accounting.reverse", command.IdempotencyKey, command.RequestFingerprint, "tax-accounting-effect", effect.Id, result.Value!, now); await db.SaveChangesAsync(cancellationToken); await tx.CommitAsync(cancellationToken); return result;
    }

    public async Task<IReadOnlyList<FinanceRevaluationBatchRecord>> ListRevaluationBatchesAsync(FinanceRequestContext context, Guid companyId, CancellationToken cancellationToken = default)
    { if (Company(context, companyId) is null) return []; await using var db = Create(context); var batches = await db.RevaluationBatches.AsNoTracking().Include(item => item.Lines).Where(item => item.CompanyId == companyId).OrderByDescending(item => item.AsOfDate).ToListAsync(cancellationToken); return batches.Select(ToBatch).ToArray(); }
    public async Task<FinanceRevaluationBatchRecord?> GetRevaluationBatchAsync(FinanceRequestContext context, Guid batchId, CancellationToken cancellationToken = default)
    { await using var db = Create(context); var batch = await db.RevaluationBatches.AsNoTracking().Include(item => item.Lines).SingleOrDefaultAsync(item => item.Id == batchId, cancellationToken); return batch is null || Company(context, batch.CompanyId) is null ? null : ToBatch(batch); }

    public async Task<FinanceOperationResult<FinanceRevaluationBatchRecord>> CreateRevaluationBatchAsync(FinanceRequestContext context, FinanceRevaluationBatchCommand command, CancellationToken cancellationToken = default)
    { if (Company(context, command.CompanyId) is null) return Failure<FinanceRevaluationBatchRecord>("company_scope_denied"); if (command.AsOfDate == default || !string.Equals(command.Scope, FinanceRevaluationScopes.ApArAndUnallocatedSettlements, StringComparison.Ordinal)) return Failure<FinanceRevaluationBatchRecord>("unsupported_revaluation_scope"); await using var db = Create(context); await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken); var replay = await ReadReplayAsync<FinanceRevaluationBatchRecord>(db, context, "finance.revaluation.create", command.IdempotencyKey, command.RequestFingerprint, cancellationToken); if (replay is not null) return replay; var policy = await PolicyAtAsync(db, command.CompanyId, command.AsOfDate, cancellationToken); if (policy is null || !policy.RevaluationEnabled) return Failure<FinanceRevaluationBatchRecord>("monetary_policy_not_configured"); var batch = new FinanceRevaluationBatchEntity(context.TenantId, command with { Scope = FinanceRevaluationScopes.ApArAndUnallocatedSettlements }, context.ActorId, DateTimeOffset.UtcNow); db.RevaluationBatches.Add(batch); var now = DateTimeOffset.UtcNow; AddAudit(db, context, "finance.revaluation.create", "revaluation-batch", batch.Id, "Succeeded", null, command.IdempotencyKey, now); await db.SaveChangesAsync(cancellationToken); var result = FinanceOperationResult<FinanceRevaluationBatchRecord>.Success(ToBatch(batch)); AddReplay(db, context, "finance.revaluation.create", command.IdempotencyKey, command.RequestFingerprint, "revaluation-batch", batch.Id, result.Value!, now); await db.SaveChangesAsync(cancellationToken); await tx.CommitAsync(cancellationToken); return result; }

    public async Task<FinanceOperationResult<FinanceRevaluationBatchRecord>> CalculateRevaluationBatchAsync(FinanceRequestContext context, FinanceRevaluationActionCommand command, CancellationToken cancellationToken = default)
    {
        await using var db = Create(context); await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken); var replay = await ReadReplayAsync<FinanceRevaluationBatchRecord>(db, context, "finance.revaluation.calculate", command.IdempotencyKey, command.RequestFingerprint, cancellationToken); if (replay is not null) return replay; var batch = await db.RevaluationBatches.Include(item => item.Lines).SingleOrDefaultAsync(item => item.Id == command.BatchId, cancellationToken); if (batch is null || Company(context, batch.CompanyId) is null) return Failure<FinanceRevaluationBatchRecord>("company_scope_denied"); if (!batch.Version.SequenceEqual(command.ExpectedVersion)) return Failure<FinanceRevaluationBatchRecord>("concurrency_conflict"); if (batch.Status != FinanceRevaluationBatchStatus.Draft) return Failure<FinanceRevaluationBatchRecord>("revaluation_already_calculated"); var policy = await PolicyAtAsync(db, batch.CompanyId, batch.AsOfDate, cancellationToken); if (policy is null || !policy.RevaluationEnabled) return Failure<FinanceRevaluationBatchRecord>("monetary_policy_not_configured");
        if (!string.Equals(batch.Scope, FinanceRevaluationScopes.ApArAndUnallocatedSettlements, StringComparison.Ordinal)) return Failure<FinanceRevaluationBatchRecord>("unsupported_revaluation_scope");
        var items = await db.OpenItems.Where(item => item.CompanyId == batch.CompanyId && item.RecognitionState == FinanceOpenItemRecognitionState.Recognized && item.DocumentDate <= batch.AsOfDate && item.CurrencyCode != item.FunctionalCurrencyCode).ToListAsync(cancellationToken); var allocations = await db.Allocations.Where(item => item.CompanyId == batch.CompanyId && item.AllocationDate <= batch.AsOfDate).ToListAsync(cancellationToken); var active = allocations.Where(item => item.Status == FinanceAllocationStatus.Active && item.ReversalOfAllocationId == null && !allocations.Any(reversal => reversal.ReversalOfAllocationId == item.Id && reversal.Status == FinanceAllocationStatus.Reversed && reversal.AllocationDate <= batch.AsOfDate)).ToArray();
        foreach (var item in items)
         { var allocated = active.Where(value => value.OpenItemId == item.Id).Sum(value => value.Amount); var outstanding = Math.Max(0m, item.OriginalAmount - allocated); if (outstanding == 0m) continue; var historical = outstanding * item.OriginalFunctionalAmount / item.OriginalAmount; var evidence = await ResolveRateEvidenceAsync(context, item.CurrencyCode, item.FunctionalCurrencyCode, batch.AsOfDate, cancellationToken); if (evidence is null) return Failure<FinanceRevaluationBatchRecord>("revaluation_rate_not_configured"); var revalued = decimal.Round(outstanding * evidence.Rate, policy.RoundingScale, Rounding(policy.RoundingMode)); var difference = revalued - historical; if (difference == 0m) continue; var direction = item.Kind == FinanceOpenItemKind.Payable ? (difference > 0m ? FinanceFxDirection.Loss : FinanceFxDirection.Gain) : (difference > 0m ? FinanceFxDirection.Gain : FinanceFxDirection.Loss); var snapshot = OpenItemSnapshot(item, active.Where(value => value.OpenItemId == item.Id), batch.AsOfDate); var monetary = await BuildFunctionalReportingEvidenceAsync(context, policy, revalued, batch.AsOfDate, cancellationToken); if (monetary is null) return Failure<FinanceRevaluationBatchRecord>("reporting_exchange_rate_required"); batch.Lines.Add(new FinanceRevaluationLineEntity(context.TenantId, Guid.NewGuid(), batch, item.Id, item.Kind == FinanceOpenItemKind.Payable ? "AP" : "AR", item.CurrencyCode, outstanding, historical, revalued, difference, direction, evidence, monetary, snapshot)); }
         var documents = await db.SettlementDocuments.Where(item => item.CompanyId == batch.CompanyId && item.Status == FinanceSettlementDocumentStatus.Posted && item.DocumentDate <= batch.AsOfDate && item.CurrencyCode != item.FunctionalCurrencyCode).ToListAsync(cancellationToken); foreach (var document in documents) { var allocated = active.Where(value => value.SettlementDocumentId == document.Id).Sum(value => value.Amount); var outstanding = Math.Max(0m, document.Amount - allocated); if (outstanding == 0m) continue; var historical = outstanding * document.FunctionalAmount / document.Amount; var evidence = await ResolveRateEvidenceAsync(context, document.CurrencyCode, document.FunctionalCurrencyCode, batch.AsOfDate, cancellationToken); if (evidence is null) return Failure<FinanceRevaluationBatchRecord>("revaluation_rate_not_configured"); var revalued = decimal.Round(outstanding * evidence.Rate, policy.RoundingScale, Rounding(policy.RoundingMode)); var difference = revalued - historical; if (difference == 0m) continue; var direction = document.Direction == FinancePaymentMethodDirection.Receipt ? (difference > 0m ? FinanceFxDirection.Gain : FinanceFxDirection.Loss) : (difference > 0m ? FinanceFxDirection.Loss : FinanceFxDirection.Gain); var snapshot = SettlementSnapshot(document, active.Where(value => value.SettlementDocumentId == document.Id), batch.AsOfDate); var monetary = await BuildFunctionalReportingEvidenceAsync(context, policy, revalued, batch.AsOfDate, cancellationToken); if (monetary is null) return Failure<FinanceRevaluationBatchRecord>("reporting_exchange_rate_required"); batch.Lines.Add(new FinanceRevaluationLineEntity(context.TenantId, Guid.NewGuid(), batch, document.Id, document.Direction == FinancePaymentMethodDirection.Receipt ? "Receipt" : "Payment", document.CurrencyCode, outstanding, historical, revalued, difference, direction, evidence, monetary, snapshot)); }
        batch.SetStatus(FinanceRevaluationBatchStatus.Calculated, context.ActorId, DateTimeOffset.UtcNow); var now = DateTimeOffset.UtcNow; AddAudit(db, context, "finance.revaluation.calculate", "revaluation-batch", batch.Id, "Succeeded", null, command.IdempotencyKey, now); await db.SaveChangesAsync(cancellationToken); var result = FinanceOperationResult<FinanceRevaluationBatchRecord>.Success(ToBatch(batch)); AddReplay(db, context, "finance.revaluation.calculate", command.IdempotencyKey, command.RequestFingerprint, "revaluation-batch", batch.Id, result.Value!, now); await db.SaveChangesAsync(cancellationToken); await tx.CommitAsync(cancellationToken); return result;
    }

    public async Task<FinanceOperationResult<FinanceRevaluationBatchRecord>> PostRevaluationBatchAsync(FinanceRequestContext context, FinanceRevaluationActionCommand command, CancellationToken cancellationToken = default)
    {
        await using var db = Create(context); await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken); var replay = await ReadReplayAsync<FinanceRevaluationBatchRecord>(db, context, "finance.revaluation.post", command.IdempotencyKey, command.RequestFingerprint, cancellationToken); if (replay is not null) return replay; var batch = await db.RevaluationBatches.Include(item => item.Lines).SingleOrDefaultAsync(item => item.Id == command.BatchId, cancellationToken); if (batch is null || Company(context, batch.CompanyId) is null) return Failure<FinanceRevaluationBatchRecord>("company_scope_denied"); if (!batch.Version.SequenceEqual(command.ExpectedVersion)) return Failure<FinanceRevaluationBatchRecord>("concurrency_conflict"); if (batch.Status != FinanceRevaluationBatchStatus.Calculated) return Failure<FinanceRevaluationBatchRecord>("revaluation_not_calculated"); if (batch.Lines.Count == 0) return Failure<FinanceRevaluationBatchRecord>("revaluation_has_no_effect");
        await LockRevaluationSourcesAsync(db, batch, cancellationToken);
        var policy = await PolicyAtAsync(db, batch.CompanyId, batch.AsOfDate, cancellationToken); if (policy is null) return Failure<FinanceRevaluationBatchRecord>("monetary_policy_not_configured");
        foreach (var line in batch.Lines) { if (await db.RevaluationLines.AnyAsync(value => value.SourceId == line.SourceId && value.Status != FinanceEvidenceStatus.Reversed && value.JournalId != null && value.BatchId != batch.Id && db.RevaluationBatches.Any(b => b.Id == value.BatchId && b.Status == FinanceRevaluationBatchStatus.Posted), cancellationToken)) return Failure<FinanceRevaluationBatchRecord>("active_revaluation_requires_reversal"); var currentSnapshot = await CurrentSourceSnapshotAsync(db, line, batch.AsOfDate, cancellationToken); if (currentSnapshot is null || !string.Equals(currentSnapshot, line.SourceSnapshotJson, StringComparison.Ordinal)) return Failure<FinanceRevaluationBatchRecord>("revaluation_source_changed"); var rule = await FindRuleAsync(db, batch.CompanyId, "finance-fx.v1", "unrealized", batch.AsOfDate, cancellationToken); if (rule is null) return Failure<FinanceRevaluationBatchRecord>("fx_posting_rule_not_configured"); var accounts = await ResolveRevaluationAccountsAsync(db, line, cancellationToken); if (accounts is null) return Failure<FinanceRevaluationBatchRecord>("posting_lineage_missing"); var lines = RevaluationJournalLines(line, rule, accounts.Value); var journal = await CreatePostedJournalAsync(db, context, batch.CompanyId, batch.AsOfDate, Company(context, batch.CompanyId)!.FunctionalCurrencyCode, Math.Abs(line.Difference), 1m, null, null, null, "finance-revaluation.v1", "unrealized", line.Id, 1, rule, lines, "Finance unrealized FX revaluation", DeserializeEvidence(line.MonetaryEvidenceJson), cancellationToken); if (!journal.Succeeded || journal.Value is null) return Failure<FinanceRevaluationBatchRecord>(journal.Code); line.SetPostingRule(rule.Id, rule.VersionNumber); line.SetJournal(journal.Value.Id); db.SourceEffects.Add(new FinanceSourceEffectEntity(context.TenantId, Guid.NewGuid(), batch.CompanyId, "finance-revaluation.v1", line.Id, 1, journal.Value.Id, DateTimeOffset.UtcNow)); }
        batch.SetStatus(FinanceRevaluationBatchStatus.Posted, context.ActorId, DateTimeOffset.UtcNow); var now = DateTimeOffset.UtcNow; AddAudit(db, context, "finance.revaluation.post", "revaluation-batch", batch.Id, "Succeeded", null, command.IdempotencyKey, now); await db.SaveChangesAsync(cancellationToken); var result = FinanceOperationResult<FinanceRevaluationBatchRecord>.Success(ToBatch(batch)); AddReplay(db, context, "finance.revaluation.post", command.IdempotencyKey, command.RequestFingerprint, "revaluation-batch", batch.Id, result.Value!, now); await db.SaveChangesAsync(cancellationToken); await tx.CommitAsync(cancellationToken); return result;
    }

    public async Task<FinanceOperationResult<FinanceRevaluationBatchRecord>> ReverseRevaluationBatchAsync(FinanceRequestContext context, FinanceRevaluationActionCommand command, CancellationToken cancellationToken = default)
    { if (string.IsNullOrWhiteSpace(command.Reason)) return Failure<FinanceRevaluationBatchRecord>("reason_required"); await using var db = Create(context); await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken); var replay = await ReadReplayAsync<FinanceRevaluationBatchRecord>(db, context, "finance.revaluation.reverse", command.IdempotencyKey, command.RequestFingerprint, cancellationToken); if (replay is not null) return replay; var batch = await db.RevaluationBatches.Include(item => item.Lines).SingleOrDefaultAsync(item => item.Id == command.BatchId, cancellationToken); if (batch is null || Company(context, batch.CompanyId) is null) return Failure<FinanceRevaluationBatchRecord>("company_scope_denied"); if (!batch.Version.SequenceEqual(command.ExpectedVersion)) return Failure<FinanceRevaluationBatchRecord>("concurrency_conflict"); if (batch.Status != FinanceRevaluationBatchStatus.Posted) return Failure<FinanceRevaluationBatchRecord>("revaluation_already_reversed"); foreach (var line in batch.Lines) { var original = line.JournalId is { } id ? await db.Journals.Include(value => value.Lines).SingleOrDefaultAsync(value => value.Id == id, cancellationToken) : null; if (original is null) return Failure<FinanceRevaluationBatchRecord>("posting_lineage_missing"); var reversal = await CreateExactReversalAsync(db, context, original, DateOnly.FromDateTime(DateTime.UtcNow), command.Reason, cancellationToken); if (!reversal.Succeeded || reversal.Value is null) return Failure<FinanceRevaluationBatchRecord>(reversal.Code); line.SetReversal(reversal.Value.Id); } batch.SetStatus(FinanceRevaluationBatchStatus.Reversed, context.ActorId, DateTimeOffset.UtcNow); var now = DateTimeOffset.UtcNow; AddAudit(db, context, "finance.revaluation.reverse", "revaluation-batch", batch.Id, "Succeeded", command.Reason, command.IdempotencyKey, now); await db.SaveChangesAsync(cancellationToken); var result = FinanceOperationResult<FinanceRevaluationBatchRecord>.Success(ToBatch(batch)); AddReplay(db, context, "finance.revaluation.reverse", command.IdempotencyKey, command.RequestFingerprint, "revaluation-batch", batch.Id, result.Value!, now); await db.SaveChangesAsync(cancellationToken); await tx.CommitAsync(cancellationToken); return result; }

    public async Task<IReadOnlyList<FinanceTaxAccountingReconciliationRecord>> ReconcileTaxAsync(FinanceRequestContext context, Guid companyId, CancellationToken cancellationToken = default) => await ReconcileTaxAsync(context, companyId, null, cancellationToken);
    public async Task<IReadOnlyList<FinanceTaxAccountingReconciliationRecord>> ReconcileTaxAsync(FinanceRequestContext context, Guid companyId, DateOnly? asOfDate, CancellationToken cancellationToken = default)
    {
        if (Company(context, companyId) is null) return [];
        await using var db = Create(context);
        var effects = await db.TaxAccountingEffects.AsNoTracking().Where(item => item.CompanyId == companyId).ToListAsync(cancellationToken);
        var journals = await db.Journals.AsNoTracking().Include(item => item.Lines).Where(item => item.CompanyId == companyId).ToDictionaryAsync(item => item.Id, cancellationToken);
        var rules = await db.PostingRules.AsNoTracking().Where(item => item.CompanyId == companyId).ToDictionaryAsync(item => item.Id, cancellationToken);
        return effects
            .Where(item => asOfDate is null || item.TaxEffectiveOn <= asOfDate)
            .Select(item =>
            {
                var journalFound = journals.TryGetValue(item.JournalId, out var journal);
                var reversalVisible = asOfDate is null || (item.ReversalJournalId is { } reversalId && journals.TryGetValue(reversalId, out var reversalJournal) && reversalJournal.PostingDate <= asOfDate);
                if (!journalFound)
                {
                    return new FinanceTaxAccountingReconciliationRecord(item.Id, item.CompanyId, item.OpenItemId, item.TaxId, item.TaxAmount, 0m, FinanceEvidenceStatus.PendingMapping, item.JournalId, null);
                }
                var posted = 0m;
                var status = FinanceEvidenceStatus.PendingMapping;
                if (rules.TryGetValue(item.PostingRuleId, out var rule))
                {
                    var taxAccount = item.Kind == FinanceOpenItemKind.Payable ? rule.DebitAccountId : rule.CreditAccountId;
                    posted = journal!.Lines.Where(line => line.AccountId == taxAccount).Sum(line => item.Kind == FinanceOpenItemKind.Payable ? line.FunctionalDebit : line.FunctionalCredit);
                    status = item.ReversalJournalId is not null && reversalVisible ? FinanceEvidenceStatus.Reversed : Math.Abs(posted - item.FunctionalAmount) <= 0.00000001m ? FinanceEvidenceStatus.Reconciled : FinanceEvidenceStatus.PendingMapping;
                }
                return new FinanceTaxAccountingReconciliationRecord(item.Id, item.CompanyId, item.OpenItemId, item.TaxId, item.TaxAmount, posted, status, item.JournalId, item.ReversalJournalId is not null && reversalVisible ? item.ReversalJournalId : null);
            })
            .ToArray();
    }
    public async Task<IReadOnlyList<FinanceFxReconciliationRecord>> ReconcileFxAsync(FinanceRequestContext context, Guid companyId, CancellationToken cancellationToken = default) => await ReconcileFxAsync(context, companyId, null, cancellationToken);
    public async Task<IReadOnlyList<FinanceFxReconciliationRecord>> ReconcileFxAsync(FinanceRequestContext context, Guid companyId, DateOnly? asOfDate, CancellationToken cancellationToken = default)
    {
        if (Company(context, companyId) is null) return [];
        await using var db = Create(context);
        var allocations = await db.Allocations.AsNoTracking().Where(item => item.CompanyId == companyId && item.RealizedFxAmount != 0m && item.ReversalOfAllocationId == null).ToListAsync(cancellationToken);
        var reversals = await db.Allocations.AsNoTracking().Where(item => item.CompanyId == companyId && item.ReversalOfAllocationId != null).ToListAsync(cancellationToken);
        var journals = await db.Journals.AsNoTracking().Include(item => item.Lines).Where(item => item.CompanyId == companyId).ToDictionaryAsync(item => item.Id, cancellationToken);
        var rules = await db.PostingRules.AsNoTracking().Where(item => item.CompanyId == companyId).ToDictionaryAsync(item => item.Id, cancellationToken);
        return allocations
            .Where(item => asOfDate is null || item.AllocationDate <= asOfDate)
            .Select(item =>
            {
                var direction = item.RealizedFxDirection == "Gain" ? FinanceFxDirection.Gain : FinanceFxDirection.Loss;
                var reversal = reversals.SingleOrDefault(value => value.ReversalOfAllocationId == item.Id);
                var reversalVisible = asOfDate is null || (reversal is not null && reversal.AllocationDate <= asOfDate);
                var expectedAccount = item.RealizedFxRuleId is { } ruleId && rules.TryGetValue(ruleId, out var rule) ? direction == FinanceFxDirection.Gain ? rule.CreditAccountId : rule.DebitAccountId : (Guid?)null;
                var journal = item.RealizedFxJournalId is { } journalId && journals.TryGetValue(journalId, out var found) ? found : null;
                var posted = journal is null || expectedAccount is null ? 0m : journal.Lines.Where(line => line.AccountId == expectedAccount).Sum(line => direction == FinanceFxDirection.Gain ? line.FunctionalCredit : line.FunctionalDebit);
                var valid = journal is not null && expectedAccount is not null && Math.Abs(posted - item.RealizedFxAmount) <= 0.00000001m;
                var reversalValid = reversalVisible && reversal?.JournalId is { } reversalJournalId && journal is not null && journals.TryGetValue(reversalJournalId, out var reverseJournal) && reverseJournal.ReversalOfJournalId == journal.Id && Math.Abs(reverseJournal.Lines.Where(line => line.AccountId == expectedAccount).Sum(line => direction == FinanceFxDirection.Gain ? line.FunctionalDebit : line.FunctionalCredit) - item.RealizedFxAmount) <= 0.00000001m;
                var status = reversal is not null && reversalVisible ? (reversalValid ? FinanceEvidenceStatus.Reversed : FinanceEvidenceStatus.PendingMapping) : valid ? FinanceEvidenceStatus.Reconciled : FinanceEvidenceStatus.PendingMapping;
                return new FinanceFxReconciliationRecord(item.Id, item.CompanyId, item.RealizedFxAmount, posted, direction, status, item.JournalId, item.OpenItemId, item.SettlementDocumentId, reversalVisible ? reversal?.JournalId : null, expectedAccount, item.RealizedFxRuleId, item.RealizedFxRuleVersionNumber, valid ? null : "realized_fx_line_mismatch");
            })
            .ToArray();
    }

    public async Task<IReadOnlyList<FinanceUnrealizedFxReconciliationRecord>> ReconcileUnrealizedFxAsync(FinanceRequestContext context, Guid companyId, CancellationToken cancellationToken = default) => await ReconcileUnrealizedFxAsync(context, companyId, null, cancellationToken);
    public async Task<IReadOnlyList<FinanceUnrealizedFxReconciliationRecord>> ReconcileUnrealizedFxAsync(FinanceRequestContext context, Guid companyId, DateOnly? asOfDate, CancellationToken cancellationToken = default)
    {
        if (Company(context, companyId) is null) return [];
        await using var db = Create(context);
        var lines = await db.RevaluationLines.AsNoTracking().Where(item => item.CompanyId == companyId).ToListAsync(cancellationToken);
        var journals = await db.Journals.AsNoTracking().Include(item => item.Lines).Where(item => item.CompanyId == companyId).ToDictionaryAsync(item => item.Id, cancellationToken);
        var rules = await db.PostingRules.AsNoTracking().Where(item => item.CompanyId == companyId).ToDictionaryAsync(item => item.Id, cancellationToken);
        return lines
            .Where(line => asOfDate is null || line.AsOfDate <= asOfDate)
            .Select(line => { var rule = line.PostingRuleId is { } ruleId && rules.TryGetValue(ruleId, out var foundRule) ? foundRule : null; var expected = rule is null ? (Guid?)null : line.Direction == FinanceFxDirection.Loss ? rule.DebitAccountId : rule.CreditAccountId; var journal = line.JournalId is { } journalId && journals.TryGetValue(journalId, out var foundJournal) ? foundJournal : null; var reversalVisible = asOfDate is null || (line.ReversalJournalId is { } visibleReversalId && journals.TryGetValue(visibleReversalId, out var visibleReversalJournal) && visibleReversalJournal.PostingDate <= asOfDate); var posted = journal is null || expected is null ? 0m : journal.Lines.Where(value => value.AccountId == expected).Sum(value => line.Direction == FinanceFxDirection.Loss ? value.FunctionalDebit : value.FunctionalCredit); var valid = journal is not null && expected is not null && Math.Abs(posted - Math.Abs(line.Difference)) <= 0.00000001m; var reversed = reversalVisible && line.ReversalJournalId is { } reversalId && journal is not null && journals.TryGetValue(reversalId, out var reversal) && reversal.ReversalOfJournalId == journal.Id && Math.Abs(reversal.Lines.Where(value => value.AccountId == expected).Sum(value => line.Direction == FinanceFxDirection.Loss ? value.FunctionalCredit : value.FunctionalDebit) - Math.Abs(line.Difference)) <= 0.00000001m; var status = line.ReversalJournalId is not null && reversalVisible ? (reversed ? FinanceEvidenceStatus.Reversed : FinanceEvidenceStatus.PendingMapping) : valid ? FinanceEvidenceStatus.Reconciled : FinanceEvidenceStatus.PendingMapping; return new FinanceUnrealizedFxReconciliationRecord(line.Id, line.BatchId, line.CompanyId, line.SourceId, line.SourceType, Math.Abs(line.Difference), posted, line.Direction, status, line.JournalId, reversalVisible ? line.ReversalJournalId : null, expected, line.PostingRuleId, line.PostingRuleVersionNumber, valid ? null : "unrealized_fx_line_mismatch"); }).ToArray(); }

    public async Task<IReadOnlyList<FinanceReportingCurrencyReconciliationRecord>> ReconcileReportingCurrencyAsync(FinanceRequestContext context, Guid companyId, CancellationToken cancellationToken = default) => await ReconcileReportingCurrencyAsync(context, companyId, null, cancellationToken);
    public async Task<IReadOnlyList<FinanceReportingCurrencyReconciliationRecord>> ReconcileReportingCurrencyAsync(FinanceRequestContext context, Guid companyId, DateOnly? asOfDate, CancellationToken cancellationToken = default)
    {
        if (Company(context, companyId) is null) return [];
        await using var db = Create(context);
        var journals = await db.Journals.AsNoTracking()
            .Where(item => item.CompanyId == companyId && (item.Status == FinanceJournalStatus.Posted || item.Status == FinanceJournalStatus.Reversed) && (asOfDate == null || item.PostingDate <= asOfDate))
            .ToListAsync(cancellationToken);
        var snapshots = await db.JournalMonetaryEvidence.AsNoTracking()
            .Where(item => item.CompanyId == companyId)
            .ToListAsync(cancellationToken);
        var effects = await db.TaxAccountingEffects.AsNoTracking()
            .Where(item => item.CompanyId == companyId)
            .ToDictionaryAsync(item => item.JournalId, cancellationToken);

        return journals.Select(journal =>
        {
            var snapshot = snapshots.SingleOrDefault(item => item.JournalId == journal.Id);
            if (snapshot is null)
            {
                return new FinanceReportingCurrencyReconciliationRecord(
                    journal.Id,
                    journal.CompanyId,
                    journal.FunctionalCurrencyCode,
                    journal.Lines.Sum(line => line.FunctionalDebit - line.FunctionalCredit),
                    null,
                    null,
                    null,
                    null,
                    null,
                    null,
                    FinanceEvidenceStatus.LegacyWithoutReportingEvidence,
                    effects.TryGetValue(journal.Id, out var effect) ? effect.Id : null,
                    "legacy_without_reporting_evidence");
            }

            var evidence = DeserializeEvidence(snapshot.MonetaryEvidenceJson);
            var evidenceMatchesSnapshot = evidence is not null
                && string.Equals(evidence.TransactionCurrencyCode, snapshot.TransactionCurrencyCode, StringComparison.Ordinal)
                && evidence.TransactionAmount == snapshot.TransactionAmount
                && string.Equals(evidence.FunctionalCurrencyCode, snapshot.FunctionalCurrencyCode, StringComparison.Ordinal)
                && evidence.FunctionalAmount == snapshot.FunctionalAmount
                && string.Equals(evidence.ReportingCurrencyCode, snapshot.ReportingCurrencyCode, StringComparison.Ordinal)
                && evidence.ReportingAmount == snapshot.ReportingAmount
                && evidence.TransactionToFunctionalRate?.ExchangeRateId == snapshot.TransactionToFunctionalRateId
                && evidence.TransactionToFunctionalRate?.ExchangeRateVersionId == snapshot.TransactionToFunctionalRateVersionId
                && evidence.TransactionToFunctionalRate?.VersionNumber == snapshot.TransactionToFunctionalRateVersionNumber
                && evidence.FunctionalToReportingRate?.ExchangeRateId == snapshot.FunctionalToReportingRateId
                && evidence.FunctionalToReportingRate?.ExchangeRateVersionId == snapshot.FunctionalToReportingRateVersionId
                && evidence.FunctionalToReportingRate?.VersionNumber == snapshot.FunctionalToReportingRateVersionNumber
                && evidence.SourceUnroundedFunctionalAmount == snapshot.SourceUnroundedFunctionalAmount
                && evidence.SourceUnroundedReportingAmount == snapshot.SourceUnroundedReportingAmount
                && evidence.RoundingScale == snapshot.RoundingScale
                && string.Equals(evidence.RoundingMode, snapshot.RoundingMode, StringComparison.Ordinal)
                && evidence.FunctionalRoundingDifference == snapshot.FunctionalRoundingDifference
                && evidence.ReportingRoundingDifference == snapshot.ReportingRoundingDifference
                && evidence.ReportingEvidenceStatus == snapshot.ReportingEvidenceStatus;
            var status = evidence is null || !evidenceMatchesSnapshot
                ? FinanceEvidenceStatus.PendingMapping
                : evidence.ReportingEvidenceStatus == FinanceEvidenceStatus.NotCaptured
                    ? FinanceEvidenceStatus.NotCaptured
                    : FinanceEvidenceStatus.Reconciled;
            return new FinanceReportingCurrencyReconciliationRecord(
                journal.Id,
                journal.CompanyId,
                snapshot.FunctionalCurrencyCode,
                snapshot.FunctionalAmount,
                snapshot.ReportingCurrencyCode,
                snapshot.ReportingAmount,
                snapshot.ReportingAmount,
                snapshot.FunctionalToReportingRateId,
                snapshot.FunctionalToReportingRateVersionId,
                snapshot.FunctionalToReportingRateVersionNumber,
                status,
                snapshot.EffectId,
                status == FinanceEvidenceStatus.PendingMapping ? "reporting_evidence_mismatch" : null);
        }).ToArray();
    }

    private async Task<MasterDataTaxCalculation?> CalculateTaxAsync(FinanceRequestContext context, FinanceMonetaryPolicyEntity policy, Guid taxId, FinanceOpenItemEntity item, decimal taxableBase, string? sourceLineage, CancellationToken cancellationToken)
    { var md = MasterDataRequestContext.FromFoundationContext(context.FoundationContext); var roundingMode = string.Equals(policy.RoundingMode, "ToEven", StringComparison.OrdinalIgnoreCase) ? TaxRoundingMode.ToEven : TaxRoundingMode.AwayFromZero; var result = await taxService.CalculateTaxAsync(md, taxId, new TaxCalculationRequest(item.DocumentDate, item.Kind == FinanceOpenItemKind.Payable ? TaxDirection.Purchase : TaxDirection.Sales, taxableBase, item.CurrencyCode, policy.RoundingScale, roundingMode, sourceLineage ?? $"finance-open-item:{item.Id}"), cancellationToken); return result.Succeeded ? result.Value : null; }
    private async Task<FinanceMonetaryEvidence?> BuildEvidenceAsync(FinanceRequestContext context, FinanceMonetaryPolicyEntity policy, FinanceOpenItemEntity item, decimal transactionAmount, DateOnly date, CancellationToken cancellationToken)
    {
        FinanceExchangeRateEvidence? txRate = null;
        var transactionIsFunctional = string.Equals(item.CurrencyCode, item.FunctionalCurrencyCode, StringComparison.OrdinalIgnoreCase);
        if (!transactionIsFunctional)
        {
            if (item.ExchangeRate is not > 0m
                || item.ExchangeRateId is null
                || item.ExchangeRateVersionId is null
                || item.ExchangeRateVersionNumber is not > 0)
            {
                return null;
            }

            txRate = await ResolveRateEvidenceAsync(context, item.CurrencyCode, item.FunctionalCurrencyCode, date, cancellationToken, item.ExchangeRateId, item.ExchangeRateVersionId, item.ExchangeRateVersionNumber, item.ExchangeRate);
            if (txRate is null) return null;
        }
        var sourceUnroundedFunctional = transactionIsFunctional ? transactionAmount : transactionAmount * txRate!.Rate;
        var functional = decimal.Round(sourceUnroundedFunctional, policy.RoundingScale, Rounding(policy.RoundingMode));
        string? reporting = policy.ReportingCurrencyCode;
        decimal? reportingAmount = null;
        decimal? sourceUnroundedReporting = null;
        FinanceExchangeRateEvidence? reportRate = null;
        FinanceEvidenceStatus reportingStatus;
        if (reporting is null)
        {
            reportingStatus = FinanceEvidenceStatus.NotCaptured;
        }
        else if (string.Equals(reporting, item.FunctionalCurrencyCode, StringComparison.OrdinalIgnoreCase))
        {
            reportingAmount = functional;
            sourceUnroundedReporting = sourceUnroundedFunctional;
            reportingStatus = FinanceEvidenceStatus.Captured;
        }
        else
        {
            reportRate = await ResolveRateEvidenceAsync(context, item.FunctionalCurrencyCode, reporting, date, cancellationToken);
            if (reportRate is null) return null;
            sourceUnroundedReporting = functional * reportRate.Rate;
            reportingAmount = decimal.Round(sourceUnroundedReporting.Value, policy.RoundingScale, Rounding(policy.RoundingMode));
            reportingStatus = FinanceEvidenceStatus.Captured;
        }
        return new FinanceMonetaryEvidence(item.CurrencyCode, transactionAmount, item.FunctionalCurrencyCode, functional, txRate, reporting, reportingAmount, reportRate, sourceUnroundedFunctional, sourceUnroundedReporting, policy.RoundingScale, policy.RoundingMode, functional - sourceUnroundedFunctional, reportingAmount is null || sourceUnroundedReporting is null ? null : reportingAmount - sourceUnroundedReporting, reportingStatus);
    }
    private async Task<FinanceMonetaryPolicyEntity?> PolicyAtAsync(FinanceDbContext db, Guid companyId, DateOnly date, CancellationToken cancellationToken) => await db.MonetaryPolicies.Where(item => item.CompanyId == companyId && item.EffectiveFrom <= date && (item.EffectiveTo == null || item.EffectiveTo >= date)).OrderByDescending(item => item.VersionNumber).SingleOrDefaultAsync(cancellationToken);
    private async Task<FinanceExchangeRateEvidence?> ResolveRateEvidenceAsync(FinanceRequestContext context, string source, string target, DateOnly date, CancellationToken cancellationToken, Guid? expectedId = null, Guid? expectedVersionId = null, int? expectedVersionNumber = null, decimal? expectedRate = null)
    { var records = await exchangeRates.ListExchangeRatesAsync(context.TenantContext, cancellationToken); var candidates = records.Where(item => (expectedId is not null || item.LifecycleState == MasterDataLifecycleState.Active) && string.Equals(item.SourceCurrencyCode, source, StringComparison.OrdinalIgnoreCase) && string.Equals(item.TargetCurrencyCode, target, StringComparison.OrdinalIgnoreCase)).SelectMany(item => item.Versions.Where(version => version.EffectiveFrom <= date && (version.EffectiveTo == null || version.EffectiveTo >= date)).Select(version => (item, version))).Where(value => (expectedId is null || value.item.Id == expectedId) && (expectedVersionId is null || value.version.Id == expectedVersionId) && (expectedVersionNumber is null || value.version.VersionNumber == expectedVersionNumber) && (expectedRate is null || value.version.Rate == expectedRate)).ToArray(); if (candidates.Length != 1) return null; var selected = candidates[0]; var v = selected.version; return new FinanceExchangeRateEvidence(selected.item.Id, v.Id, v.VersionNumber, source, target, date, v.Rate, v.RateScale, v.Provenance.ToString(), v.SourceNotes, $"{source}->{target};v{v.VersionNumber}@{date:yyyy-MM-dd}", v.EffectiveFrom, v.EffectiveTo); }

    private async Task<string?> ValidateSupplierDeclaredTaxAsync(FinanceRequestContext context, FinanceOpenItemEntity item, MasterDataTaxCalculation calculation, decimal taxableBase, CancellationToken cancellationToken)
    {
        if (!string.Equals(item.SourceContract, "procurement-supplier-invoice.v1", StringComparison.Ordinal)) return null;
        var source = await supplierInvoiceSources.FindAsync(context, item.SourceEvidenceId, cancellationToken);
        if (source is null || source.DeclaredTaxes is null || source.DeclaredTaxes.Count == 0) return "tax_evidence_not_authoritative";
        if (source.DeclaredInvoiceDate is null || source.DeclaredInvoiceDate.Value != item.DocumentDate || !string.Equals(source.DeclaredCurrencyCode ?? source.CurrencyCode, item.CurrencyCode, StringComparison.OrdinalIgnoreCase)) return "tax_evidence_mismatch";
        var taxes = source.DeclaredTaxes.Where(value => value.TaxCode is not null || value.TaxRatePercentage is not null || value.TaxAmount is not null).ToArray();
        if (taxes.Length != 1) return "tax_evidence_ambiguous";
        var declared = taxes[0];
        if (string.IsNullOrWhiteSpace(declared.TaxCode) || declared.TaxRatePercentage is null || declared.TaxAmount is null || declared.TaxableBase is null) return "tax_evidence_not_authoritative";
        if (!string.Equals(declared.TaxCode, calculation.Code, StringComparison.OrdinalIgnoreCase)) return "tax_evidence_mismatch";
        if (declared.TaxRatePercentage.Value != calculation.RatePercentage || Math.Abs(declared.TaxAmount.Value - calculation.TaxAmount) > 0.00000001m || Math.Abs(declared.TaxableBase.Value - taxableBase) > 0.00000001m) return "tax_evidence_mismatch";
        return null;
    }

    private async Task<string> ReportingEvidenceFailureCodeAsync(FinanceRequestContext context, FinanceMonetaryPolicyEntity policy, FinanceOpenItemEntity item, DateOnly date, CancellationToken cancellationToken)
    {
        if (!string.Equals(item.CurrencyCode, item.FunctionalCurrencyCode, StringComparison.OrdinalIgnoreCase) && await ResolveRateEvidenceAsync(context, item.CurrencyCode, item.FunctionalCurrencyCode, date, cancellationToken, item.ExchangeRateId, item.ExchangeRateVersionId, item.ExchangeRateVersionNumber, item.ExchangeRate) is null) return "exact_exchange_rate_evidence_required";
        return policy.ReportingCurrencyCode is not null && !string.Equals(policy.ReportingCurrencyCode, item.FunctionalCurrencyCode, StringComparison.OrdinalIgnoreCase) ? "reporting_exchange_rate_required" : "exact_exchange_rate_evidence_required";
    }

    private async Task<FinanceMonetaryEvidence?> BuildFunctionalReportingEvidenceAsync(FinanceRequestContext context, FinanceMonetaryPolicyEntity policy, decimal functionalAmount, DateOnly date, CancellationToken cancellationToken)
    {
        var reporting = policy.ReportingCurrencyCode;
        if (reporting is null) return new FinanceMonetaryEvidence(policy.FunctionalCurrencyCode, functionalAmount, policy.FunctionalCurrencyCode, functionalAmount, null, null, null, null, functionalAmount, null, policy.RoundingScale, policy.RoundingMode, 0m, null, FinanceEvidenceStatus.NotCaptured);
        if (string.Equals(reporting, policy.FunctionalCurrencyCode, StringComparison.OrdinalIgnoreCase)) return new FinanceMonetaryEvidence(policy.FunctionalCurrencyCode, functionalAmount, policy.FunctionalCurrencyCode, functionalAmount, null, policy.FunctionalCurrencyCode, functionalAmount, null, functionalAmount, functionalAmount, policy.RoundingScale, policy.RoundingMode, 0m, 0m, FinanceEvidenceStatus.Captured);
        var rate = await ResolveRateEvidenceAsync(context, policy.FunctionalCurrencyCode, reporting, date, cancellationToken);
        if (rate is null) return null;
        var unrounded = functionalAmount * rate.Rate;
        var rounded = decimal.Round(unrounded, policy.RoundingScale, Rounding(policy.RoundingMode));
        return new FinanceMonetaryEvidence(policy.FunctionalCurrencyCode, functionalAmount, policy.FunctionalCurrencyCode, functionalAmount, null, reporting, rounded, rate, functionalAmount, unrounded, policy.RoundingScale, policy.RoundingMode, 0m, rounded - unrounded, FinanceEvidenceStatus.Captured);
    }

    private static string OpenItemSnapshot(FinanceOpenItemEntity item, IEnumerable<FinanceAllocationEntity> allocations, DateOnly asOfDate) => JsonSerializer.Serialize(new { item.Id, item.Version, item.OriginalAmount, item.OriginalFunctionalAmount, item.RecognitionJournalId, item.DocumentDate, item.CurrencyCode, item.FunctionalCurrencyCode, AsOfDate = asOfDate, Allocations = allocations.OrderBy(value => value.Id).Select(value => new { value.Id, value.Status, value.ReversalOfAllocationId, value.Amount, value.FunctionalAmount, value.HistoricalFunctionalAmount, value.SettlementFunctionalAmount, value.RealizedFxAmount, value.RealizedFxDirection, value.JournalId, value.AllocationDate }) });
    private static string SettlementSnapshot(FinanceSettlementDocumentEntity document, IEnumerable<FinanceAllocationEntity> allocations, DateOnly asOfDate) => JsonSerializer.Serialize(new { document.Id, document.Version, document.Status, document.DocumentDate, document.Amount, document.FunctionalAmount, document.FunctionalCurrencyCode, document.CurrencyCode, document.PostedJournalId, document.ReversalJournalId, AsOfDate = asOfDate, Allocations = allocations.OrderBy(value => value.Id).Select(value => new { value.Id, value.Status, value.ReversalOfAllocationId, value.Amount, value.FunctionalAmount, value.HistoricalFunctionalAmount, value.SettlementFunctionalAmount, value.RealizedFxAmount, value.RealizedFxDirection, value.JournalId, value.AllocationDate }) });
    private async Task<string?> CurrentSourceSnapshotAsync(FinanceDbContext db, FinanceRevaluationLineEntity line, DateOnly asOfDate, CancellationToken cancellationToken)
    {
        var isOpenItem = line.SourceType is "AP" or "AR";
        var allocations = await db.Allocations.Where(value => value.CompanyId == line.CompanyId && value.AllocationDate <= asOfDate && (isOpenItem ? value.OpenItemId == line.SourceId : value.SettlementDocumentId == line.SourceId)).OrderBy(value => value.Id).ToArrayAsync(cancellationToken);
        if (line.SourceType is "AP" or "AR") { var item = await db.OpenItems.SingleOrDefaultAsync(value => value.Id == line.SourceId, cancellationToken); return item is null ? null : OpenItemSnapshot(item, allocations, asOfDate); }
        var document = await db.SettlementDocuments.SingleOrDefaultAsync(value => value.Id == line.SourceId, cancellationToken); return document is null ? null : SettlementSnapshot(document, allocations, asOfDate);
    }

    private static FinanceMonetaryEvidence? DeserializeEvidence(string? json)
    {
        if (string.IsNullOrWhiteSpace(json)) return null;
        try
        {
            return JsonSerializer.Deserialize<FinanceMonetaryEvidence>(json);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static async Task LockRevaluationSourcesAsync(FinanceDbContext db, FinanceRevaluationBatchEntity batch, CancellationToken cancellationToken)
    {
        if (db.Database.ProviderName != "Microsoft.EntityFrameworkCore.SqlServer") return;
        var ids = await db.RevaluationLines.Where(value => value.BatchId == batch.Id).Select(value => value.SourceId).Distinct().ToArrayAsync(cancellationToken);
        var connection = db.Database.GetDbConnection();
        if (connection.State != System.Data.ConnectionState.Open) await connection.OpenAsync(cancellationToken);
        await using var command = connection.CreateCommand(); command.Transaction = db.Database.CurrentTransaction?.GetDbTransaction();
        var parameters = ids.Select((id, index) => { var parameter = command.CreateParameter(); parameter.ParameterName = $"@p{index}"; parameter.Value = id; command.Parameters.Add(parameter); return parameter.ParameterName; }).ToArray();
        if (parameters.Length == 0) return;
        command.CommandText = $"SELECT Id FROM finance.OpenItems WITH (UPDLOCK,HOLDLOCK) WHERE TenantId = '{batch.TenantId.Value}' AND CompanyId = '{batch.CompanyId}' AND Id IN ({string.Join(",", parameters)}) UNION ALL SELECT Id FROM finance.SettlementDocuments WITH (UPDLOCK,HOLDLOCK) WHERE TenantId = '{batch.TenantId.Value}' AND CompanyId = '{batch.CompanyId}' AND Id IN ({string.Join(",", parameters)})";
        await using var reader = await command.ExecuteReaderAsync(cancellationToken); while (await reader.ReadAsync(cancellationToken)) { }
    }
    private static Guid? ResolveSourceAccount(FinanceOpenItemKind kind, FinanceJournalEntity? journal) { if (journal is null) return null; var lines = kind == FinanceOpenItemKind.Payable ? journal.Lines.Where(item => item.FunctionalDebit > 0m).ToArray() : journal.Lines.Where(item => item.FunctionalCredit > 0m).ToArray(); return lines.Length == 1 ? lines[0].AccountId : null; }
    private async Task<FinancePostingRuleEntity?> FindRuleAsync(FinanceDbContext db, Guid companyId, string contract, string eventName, DateOnly date, CancellationToken cancellationToken) { var rules = await db.PostingRules.Where(item => item.CompanyId == companyId && item.SourceContract == contract && item.SourceEvent == eventName && item.Lifecycle == FinancePostingRuleLifecycle.Enabled && item.EffectiveFrom <= date && (item.EffectiveTo == null || item.EffectiveTo >= date)).ToListAsync(cancellationToken); return rules.Count == 1 ? rules[0] : null; }
    private async Task<(Guid ControlId, Guid OpposingId)?> ResolveRevaluationAccountsAsync(FinanceDbContext db, FinanceRevaluationLineEntity line, CancellationToken cancellationToken)
    { if (line.SourceType is "AP" or "AR") { var item = await db.OpenItems.SingleOrDefaultAsync(value => value.Id == line.SourceId, cancellationToken); if (item?.RecognitionJournalId is not { } recognitionId) return null; var recognition = await db.Journals.Include(value => value.Lines).SingleOrDefaultAsync(value => value.Id == recognitionId, cancellationToken); if (recognition is null) return null; var control = item.Kind == FinanceOpenItemKind.Payable ? recognition.Lines.Where(value => value.FunctionalCredit > 0m) : recognition.Lines.Where(value => value.FunctionalDebit > 0m); var account = control.SingleOrDefault(); return account is null ? null : (account.AccountId, account.AccountId); } var document = await db.SettlementDocuments.SingleOrDefaultAsync(value => value.Id == line.SourceId, cancellationToken); if (document?.PostedJournalId is not { } journalId) return null; var journal = await db.Journals.Include(value => value.Lines).SingleOrDefaultAsync(value => value.Id == journalId, cancellationToken); var cash = document is null ? null : await db.CashAccounts.SingleOrDefaultAsync(value => value.Id == document.CashAccountId, cancellationToken); var posted = cash is null ? null : journal?.Lines.SingleOrDefault(value => value.AccountId == cash.LinkedAccountId); return posted is null ? null : (posted.AccountId, posted.AccountId); }
    private static IEnumerable<JournalLine> RevaluationJournalLines(FinanceRevaluationLineEntity line, FinancePostingRuleEntity rule, (Guid ControlId, Guid OpposingId) accounts) { var loss = line.Direction == FinanceFxDirection.Loss; var amount = Math.Abs(line.Difference); return loss ? [new JournalLine(rule.DebitAccountId, amount, 0m, amount, "Unrealized FX loss"), new JournalLine(accounts.ControlId, 0m, amount, amount, "Revaluation source")] : [new JournalLine(accounts.ControlId, amount, 0m, amount, "Revaluation source"), new JournalLine(rule.CreditAccountId, 0m, amount, amount, "Unrealized FX gain")]; }
    private sealed record JournalLine(Guid AccountId, decimal Debit, decimal Credit, decimal Functional, string Description);
    private async Task<(bool Succeeded, string Code, FinanceJournalRecord? Value)> CreatePostedJournalAsync(FinanceDbContext db, FinanceRequestContext context, Guid companyId, DateOnly date, string currency, decimal amount, decimal? rate, Guid? rateId, Guid? versionId, int? versionNumber, string sourceContract, string sourceEvent, Guid sourceEvidenceId, int sourceEvidenceVersion, FinancePostingRuleEntity rule, IEnumerable<JournalLine> lineValues, string description, FinanceMonetaryEvidence? monetaryEvidence, CancellationToken cancellationToken)
    { var company = Company(context, companyId); if (company is null) return (false, "company_scope_denied", null); var period = await db.FiscalPeriods.SingleOrDefaultAsync(item => item.CompanyId == companyId && item.StartDate <= date && (item.EndDate >= date), cancellationToken); if (period is null || period.State != FinanceFiscalPeriodState.Open) return (false, "period_not_open", null); var values = lineValues.ToArray(); var accounts = await db.Accounts.Where(item => item.CompanyId == companyId && values.Select(value => value.AccountId).Contains(item.Id)).ToDictionaryAsync(item => item.Id, cancellationToken); if (accounts.Count != values.Select(value => value.AccountId).Distinct().Count() || accounts.Values.Any(item => !item.IsPostingAccount || item.Lifecycle != FinanceAccountLifecycle.Active)) return (false, "account_not_postable", null); var sequence = (await db.Journals.Where(item => item.CompanyId == companyId).Select(item => (long?)item.JournalSequence).MaxAsync(cancellationToken) ?? 0) + 1; var id = Guid.NewGuid(); var command = new FinanceJournalCommand(companyId, date, date, currency, currency == company.FunctionalCurrencyCode ? null : rate, currency == company.FunctionalCurrencyCode ? null : rateId, currency == company.FunctionalCurrencyCode ? null : versionId, currency == company.FunctionalCurrencyCode ? null : versionNumber, sourceContract, sourceEvent, sourceEvidenceId, sourceEvidenceVersion, rule.Id, description, values.Select(value => new FinanceJournalLineCommand(value.AccountId, value.Debit, value.Credit, Math.Max(value.Debit, value.Credit), currency, null, value.Description)).ToArray(), id, Guid.NewGuid().ToString("N"), Guid.NewGuid().ToString("N"), FinanceJournalAmountAuthority.ManualTransactionCurrency, FinanceApprovalRequirement.NotRequired); var journal = new FinanceJournalEntity(context.TenantId, id, command, sequence, company.FunctionalCurrencyCode, context.ActorId, DateTimeOffset.UtcNow); journal.SetCorrelation(context.CorrelationId); journal.SetRule(rule.Id, rule.VersionNumber); journal.SetPeriod(period.FiscalYearId, period.Id); journal.SetStatus(FinanceJournalStatus.Posted, context.ActorId, DateTimeOffset.UtcNow); var number = 1; foreach (var value in values) journal.Lines.Add(new FinanceJournalLineEntity(context.TenantId, Guid.NewGuid(), id, number++, accounts[value.AccountId], new FinanceJournalLineCommand(value.AccountId, value.Debit, value.Credit, Math.Max(value.Debit, value.Credit), currency, null, value.Description), null, value.Debit > 0m ? value.Functional : 0m, value.Credit > 0m ? value.Functional : 0m, FinanceJournalAmountAuthority.ManualTransactionCurrency)); db.Journals.Add(journal); if (monetaryEvidence is not null) db.JournalMonetaryEvidence.Add(new FinanceJournalMonetaryEvidenceEntity(context.TenantId, Guid.NewGuid(), id, companyId, sourceContract == "finance-tax.v1" ? sourceEvidenceId : null, monetaryEvidence, DateTimeOffset.UtcNow)); return (true, "succeeded", ToJournal(journal)); }
    private async Task<FinanceOperationResult<FinanceJournalRecord>> CreateExactReversalAsync(FinanceDbContext db, FinanceRequestContext context, FinanceJournalEntity original, DateOnly date, string reason, CancellationToken cancellationToken)
    {
        var rule = original.PostingRuleId is { } id
            ? await db.PostingRules.SingleOrDefaultAsync(item => item.Id == id, cancellationToken)
            : null;
        if (rule is null) return Failure<FinanceJournalRecord>("posting_lineage_missing");
        var values = original.Lines.OrderBy(item => item.LineNumber)
            .Select(line => new JournalLine(line.AccountId, line.Credit, line.Debit, line.FunctionalCredit, reason));
        var storedEvidence = await db.JournalMonetaryEvidence.AsNoTracking()
            .SingleOrDefaultAsync(item => item.JournalId == original.Id, cancellationToken);
        var evidence = storedEvidence is null ? null : DeserializeEvidence(storedEvidence.MonetaryEvidenceJson);
        if (evidence is not null)
        {
            evidence = evidence with
            {
                TransactionAmount = -evidence.TransactionAmount,
                FunctionalAmount = -evidence.FunctionalAmount,
                ReportingAmount = evidence.ReportingAmount is null ? null : -evidence.ReportingAmount,
                SourceUnroundedFunctionalAmount = -evidence.SourceUnroundedFunctionalAmount,
                SourceUnroundedReportingAmount = evidence.SourceUnroundedReportingAmount is null ? null : -evidence.SourceUnroundedReportingAmount,
                FunctionalRoundingDifference = -evidence.FunctionalRoundingDifference,
                ReportingRoundingDifference = evidence.ReportingRoundingDifference is null ? null : -evidence.ReportingRoundingDifference
            };
        }
        var reversal = await CreatePostedJournalAsync(
            db,
            context,
            original.CompanyId,
            date,
            original.TransactionCurrencyCode ?? original.FunctionalCurrencyCode,
            original.Lines.Sum(line => Math.Max(line.Debit, line.Credit)),
            original.ExchangeRate,
            original.ExchangeRateId,
            original.ExchangeRateVersionId,
            original.ExchangeRateVersionNumber,
            "finance-reversal.v1",
            "exact",
            original.Id,
            1,
            rule,
            values,
            reason,
            evidence,
            cancellationToken);
        if (!reversal.Succeeded || reversal.Value is null) return Failure<FinanceJournalRecord>(reversal.Code);
        var reversalEntity = db.Journals.Local.Single(item => item.Id == reversal.Value.Id);
        reversalEntity.LinkOriginal(original.Id);
        original.LinkReversal(reversal.Value.Id);
        original.SetStatus(FinanceJournalStatus.Reversed, context.ActorId, DateTimeOffset.UtcNow);
        return FinanceOperationResult<FinanceJournalRecord>.Success(reversal.Value);
    }
    private static void AddAudit(FinanceDbContext db, FinanceRequestContext context, string operation, string resource, Guid id, string result, string? reason, string? key, DateTimeOffset at) => db.AuditEvents.Add(new FinanceAuditEntity(context.TenantId, Guid.NewGuid(), operation, resource, id, context.ActorId, context.SessionId, result, reason, context.CorrelationId, key, at));
    private static void AddReplay<T>(FinanceDbContext db, FinanceRequestContext context, string operation, string key, string fingerprint, string resource, Guid id, T value, DateTimeOffset at) { if (!string.IsNullOrWhiteSpace(key)) db.Idempotency.Add(new FinanceIdempotencyEntity(context.TenantId, Guid.NewGuid(), context.ActorId, operation, key, fingerprint, resource, id, JsonSerializer.Serialize(value), at)); }
    private static async Task<FinanceOperationResult<T>?> ReadReplayAsync<T>(FinanceDbContext db, FinanceRequestContext context, string operation, string key, string fingerprint, CancellationToken cancellationToken) { if (string.IsNullOrWhiteSpace(key)) return null; var item = await db.Idempotency.AsNoTracking().SingleOrDefaultAsync(value => value.ActorId == context.ActorId && value.OperationId == operation && value.Key == key, cancellationToken); if (item is null) return null; if (!string.Equals(item.Fingerprint, fingerprint, StringComparison.Ordinal)) return Failure<T>("idempotency_conflict"); var value = JsonSerializer.Deserialize<T>(item.SnapshotJson); return value is null ? Failure<T>("idempotency_snapshot_invalid") : FinanceOperationResult<T>.Success(value); }
    private static MidpointRounding Rounding(string mode) => string.Equals(mode, "ToEven", StringComparison.OrdinalIgnoreCase) ? MidpointRounding.ToEven : MidpointRounding.AwayFromZero;
    private static FinanceMonetaryPolicyRecord ToPolicy(FinanceMonetaryPolicyEntity item) => new(item.Id, item.TenantId.Value, item.CompanyId, item.FunctionalCurrencyCode, item.ReportingCurrencyId, item.ReportingCurrencyCode, item.RoundingScale, item.RoundingMode, item.RevaluationEnabled, item.EffectiveFrom, item.EffectiveTo, item.VersionNumber, item.Version);
    private static FinanceTaxAccountingEffectRecord ToTaxEffect(FinanceTaxAccountingEffectEntity item)
    {
        var evidence = JsonSerializer.Deserialize<FinanceMonetaryEvidence>(item.MonetaryEvidenceJson)
            ?? new FinanceMonetaryEvidence(item.TransactionCurrencyCode, item.TaxAmount, item.FunctionalCurrencyCode, item.FunctionalAmount, null, item.ReportingCurrencyCode, item.ReportingAmount, null, item.SourceUnroundedFunctionalAmount, item.SourceUnroundedReportingAmount, item.RoundingScale, item.RoundingMode, item.FunctionalRoundingDifference, item.ReportingRoundingDifference, item.ReportingEvidenceStatus);
        return new(item.Id, item.TenantId.Value, item.CompanyId, item.OpenItemId, item.Kind, item.TaxId, item.TaxCode, item.TaxRateVersionId, item.TaxRateVersionNumber, item.TaxEffectiveOn, item.TaxRatePercentage, item.TaxableBase, item.TaxAmount, item.TransactionCurrencyCode, item.FunctionalAmount, item.FunctionalCurrencyCode, item.JournalId, item.ReversalJournalId, item.PostingRuleId, item.PostingRuleVersionNumber, evidence, item.Status, item.CreatedAt, item.CreatedBy, item.Version);
    }
    private static FinanceRevaluationBatchRecord ToBatch(FinanceRevaluationBatchEntity item) => new(item.Id, item.TenantId.Value, item.CompanyId, item.AsOfDate, item.Scope, item.Status, item.Lines.OrderBy(value => value.SourceType).Select(ToLine).ToArray(), item.CreatedBy, item.CreatedAt, item.PostedBy, item.PostedAt, item.ReversedBy, item.ReversedAt, item.Version);
    private static FinanceRevaluationLineRecord ToLine(FinanceRevaluationLineEntity item) => new(item.Id, item.BatchId, item.CompanyId, item.SourceId, item.SourceType, item.AsOfDate, item.TransactionCurrencyCode, item.OutstandingTransactionAmount, item.HistoricalFunctionalAmount, item.RevaluedFunctionalAmount, item.Difference, item.Direction, new FinanceExchangeRateEvidence(item.ExchangeRateId, item.ExchangeRateVersionId, item.ExchangeRateVersionNumber, item.ExchangeSourceCurrencyCode, item.ExchangeTargetCurrencyCode, item.ExchangeEffectiveOn, item.ExchangeRate, item.ExchangeRateScale, item.ExchangeProvenance, item.ExchangeSourceNotes, $"{item.ExchangeSourceCurrencyCode}->{item.ExchangeTargetCurrencyCode};v{item.ExchangeRateVersionNumber}@{item.ExchangeEffectiveOn:yyyy-MM-dd}", item.ExchangeEffectiveFrom, item.ExchangeEffectiveTo), item.JournalId, item.ReversalJournalId, item.Status, item.Version, DeserializeEvidence(item.MonetaryEvidenceJson), string.IsNullOrWhiteSpace(item.SourceSnapshotJson) ? null : Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(item.SourceSnapshotJson))), item.PostingRuleId, item.PostingRuleVersionNumber, null, null, item.Status == FinanceEvidenceStatus.Reversed ? FinanceEvidenceStatus.Reversed : item.JournalId is null ? FinanceEvidenceStatus.PendingMapping : FinanceEvidenceStatus.Reconciled);
    private static FinanceJournalRecord ToJournal(FinanceJournalEntity item) => new(item.Id, item.TenantId.Value, item.CompanyId, item.JournalSequence, item.JournalNumber, item.JournalDate, item.PostingDate, item.FiscalYearId, item.FiscalPeriodId, item.FunctionalCurrencyCode, item.TransactionCurrencyCode, item.ExchangeRate, item.ExchangeRateId, item.ExchangeRateVersionId, item.ExchangeRateVersionNumber, item.SourceContract, item.SourceEvent, item.SourceEvidenceId, item.SourceEvidenceVersion, item.PostingRuleId, item.PostingRuleVersionNumber, item.Description, item.Status, item.CreatedBy, item.SubmittedBy, item.ApprovedBy, item.PostedBy, item.ReversedBy, item.ReversalOfJournalId, item.ReversalJournalId, item.CorrelationId, item.CreatedAt, item.PostedAt, item.Lines.OrderBy(line => line.LineNumber).Select(line => new FinanceJournalLineRecord(line.Id, line.LineNumber, line.AccountId, line.AccountCode, line.AccountName, line.Debit, line.Credit, line.FunctionalDebit, line.FunctionalCredit, line.TransactionAmount, line.TransactionCurrencyCode, line.CostCenterId, line.CostCenterCode, line.Description)).ToArray(), item.Version, item.AmountAuthority, item.ApprovalRequirement);
}

#pragma warning restore CS1591
