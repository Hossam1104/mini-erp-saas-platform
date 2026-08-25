#pragma warning disable CS1591

using System.Text.Json;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.Contracts.Modules.Finance;

namespace MiniErp.Infrastructure.Persistence.Modules.Finance;

internal sealed class FinanceMonetaryPolicyEntity : FinanceEntity
{
    private FinanceMonetaryPolicyEntity() { FunctionalCurrencyCode = RoundingMode = string.Empty; }
    internal FinanceMonetaryPolicyEntity(TenantId tenantId, FinanceMonetaryPolicyCommand command, string functionalCurrencyCode, string? reportingCurrencyCode, int versionNumber)
        : base(tenantId, command.Id)
    {
        CompanyId = command.CompanyId;
        FunctionalCurrencyCode = functionalCurrencyCode;
        ReportingCurrencyId = command.ReportingCurrencyId;
        ReportingCurrencyCode = reportingCurrencyCode;
        RoundingScale = command.RoundingScale;
        RoundingMode = command.RoundingMode;
        RevaluationEnabled = command.RevaluationEnabled;
        EffectiveFrom = command.EffectiveFrom;
        EffectiveTo = command.EffectiveTo;
        VersionNumber = versionNumber;
    }

    internal Guid CompanyId { get; private set; }
    internal string FunctionalCurrencyCode { get; private set; }
    internal Guid? ReportingCurrencyId { get; private set; }
    internal string? ReportingCurrencyCode { get; private set; }
    internal int RoundingScale { get; private set; }
    internal string RoundingMode { get; private set; }
    internal bool RevaluationEnabled { get; private set; }
    internal DateOnly EffectiveFrom { get; private set; }
    internal DateOnly? EffectiveTo { get; private set; }
    internal int VersionNumber { get; private set; }
    internal void TouchRevision() { VersionNumber++; TouchVersion(); }
}

internal sealed class FinanceTaxAccountingEffectEntity : FinanceEntity
{
    private FinanceTaxAccountingEffectEntity() { TaxCode = TransactionCurrencyCode = FunctionalCurrencyCode = RoundingMode = string.Empty; }
    internal FinanceTaxAccountingEffectEntity(
        TenantId tenantId, Guid id, Guid companyId, Guid openItemId, FinanceOpenItemKind kind,
        Guid taxId, string taxCode, Guid taxRateVersionId, int taxRateVersionNumber, DateOnly taxEffectiveOn,
        decimal taxRatePercentage, decimal taxableBase, decimal taxAmount, string transactionCurrencyCode,
        decimal functionalAmount, string functionalCurrencyCode, Guid journalId, Guid postingRuleId,
        int postingRuleVersionNumber, FinanceMonetaryEvidence evidence, Guid actorId, DateTimeOffset at)
        : base(tenantId, id)
    {
        CompanyId = companyId; OpenItemId = openItemId; Kind = kind; TaxId = taxId; TaxCode = taxCode;
        TaxRateVersionId = taxRateVersionId; TaxRateVersionNumber = taxRateVersionNumber; TaxEffectiveOn = taxEffectiveOn;
        TaxRatePercentage = taxRatePercentage; TaxableBase = taxableBase; TaxAmount = taxAmount;
        TransactionCurrencyCode = transactionCurrencyCode; FunctionalAmount = functionalAmount; FunctionalCurrencyCode = functionalCurrencyCode;
        JournalId = journalId; PostingRuleId = postingRuleId; PostingRuleVersionNumber = postingRuleVersionNumber;
        ReportingCurrencyCode = evidence.ReportingCurrencyCode; ReportingAmount = evidence.ReportingAmount;
        TransactionToFunctionalRateId = evidence.TransactionToFunctionalRate?.ExchangeRateId;
        TransactionToFunctionalRateVersionId = evidence.TransactionToFunctionalRate?.ExchangeRateVersionId;
        TransactionToFunctionalRateVersionNumber = evidence.TransactionToFunctionalRate?.VersionNumber;
        FunctionalToReportingRateId = evidence.FunctionalToReportingRate?.ExchangeRateId;
        FunctionalToReportingRateVersionId = evidence.FunctionalToReportingRate?.ExchangeRateVersionId;
        FunctionalToReportingRateVersionNumber = evidence.FunctionalToReportingRate?.VersionNumber;
        SourceUnroundedFunctionalAmount = evidence.SourceUnroundedFunctionalAmount;
        SourceUnroundedReportingAmount = evidence.SourceUnroundedReportingAmount;
        RoundingScale = evidence.RoundingScale; RoundingMode = evidence.RoundingMode;
        FunctionalRoundingDifference = evidence.FunctionalRoundingDifference;
        ReportingRoundingDifference = evidence.ReportingRoundingDifference;
        ReportingEvidenceStatus = evidence.ReportingEvidenceStatus; MonetaryEvidenceJson = JsonSerializer.Serialize(evidence); CreatedBy = actorId; CreatedAt = at;
        Status = FinanceEvidenceStatus.Captured;
    }
    internal Guid CompanyId { get; private set; }
    internal Guid OpenItemId { get; private set; }
    internal FinanceOpenItemKind Kind { get; private set; }
    internal Guid TaxId { get; private set; }
    internal string TaxCode { get; private set; }
    internal Guid TaxRateVersionId { get; private set; }
    internal int TaxRateVersionNumber { get; private set; }
    internal DateOnly TaxEffectiveOn { get; private set; }
    internal decimal TaxRatePercentage { get; private set; }
    internal decimal TaxableBase { get; private set; }
    internal decimal TaxAmount { get; private set; }
    internal string TransactionCurrencyCode { get; private set; }
    internal decimal FunctionalAmount { get; private set; }
    internal string FunctionalCurrencyCode { get; private set; }
    internal string? ReportingCurrencyCode { get; private set; }
    internal decimal? ReportingAmount { get; private set; }
    internal Guid? TransactionToFunctionalRateId { get; private set; }
    internal Guid? TransactionToFunctionalRateVersionId { get; private set; }
    internal int? TransactionToFunctionalRateVersionNumber { get; private set; }
    internal Guid? FunctionalToReportingRateId { get; private set; }
    internal Guid? FunctionalToReportingRateVersionId { get; private set; }
    internal int? FunctionalToReportingRateVersionNumber { get; private set; }
    internal decimal SourceUnroundedFunctionalAmount { get; private set; }
    internal decimal? SourceUnroundedReportingAmount { get; private set; }
    internal int RoundingScale { get; private set; }
    internal string RoundingMode { get; private set; }
    internal decimal FunctionalRoundingDifference { get; private set; }
    internal decimal? ReportingRoundingDifference { get; private set; }
    internal FinanceEvidenceStatus ReportingEvidenceStatus { get; private set; }
    internal string MonetaryEvidenceJson { get; private set; } = string.Empty;
    internal Guid JournalId { get; private set; }
    internal Guid? ReversalJournalId { get; private set; }
    internal Guid PostingRuleId { get; private set; }
    internal int PostingRuleVersionNumber { get; private set; }
    internal FinanceEvidenceStatus Status { get; private set; }
    internal Guid CreatedBy { get; private set; }
    internal DateTimeOffset CreatedAt { get; private set; }
    internal void SetReversal(Guid journalId) { ReversalJournalId = journalId; Status = FinanceEvidenceStatus.Reversed; TouchVersion(); }
}

internal sealed class FinanceRevaluationBatchEntity : FinanceEntity
{
    private FinanceRevaluationBatchEntity() { Scope = string.Empty; }
    internal FinanceRevaluationBatchEntity(TenantId tenantId, FinanceRevaluationBatchCommand command, Guid actorId, DateTimeOffset at)
        : base(tenantId, command.Id)
    { CompanyId = command.CompanyId; AsOfDate = command.AsOfDate; Scope = command.Scope; Status = FinanceRevaluationBatchStatus.Draft; CreatedBy = actorId; CreatedAt = at; }
    internal Guid CompanyId { get; private set; }
    internal DateOnly AsOfDate { get; private set; }
    internal string Scope { get; private set; }
    internal FinanceRevaluationBatchStatus Status { get; private set; }
    internal Guid CreatedBy { get; private set; }
    internal DateTimeOffset CreatedAt { get; private set; }
    internal Guid? PostedBy { get; private set; }
    internal DateTimeOffset? PostedAt { get; private set; }
    internal Guid? ReversedBy { get; private set; }
    internal DateTimeOffset? ReversedAt { get; private set; }
    internal ICollection<FinanceRevaluationLineEntity> Lines { get; private set; } = new List<FinanceRevaluationLineEntity>();
    internal void SetStatus(FinanceRevaluationBatchStatus status, Guid actorId, DateTimeOffset at)
    { Status = status; if (status == FinanceRevaluationBatchStatus.Posted) { PostedBy = actorId; PostedAt = at; } if (status == FinanceRevaluationBatchStatus.Reversed) { ReversedBy = actorId; ReversedAt = at; } TouchVersion(); }
}

internal sealed class FinanceRevaluationLineEntity : FinanceEntity
{
    private FinanceRevaluationLineEntity() { SourceType = TransactionCurrencyCode = ExchangeSourceCurrencyCode = ExchangeTargetCurrencyCode = ExchangeProvenance = string.Empty; }
    internal FinanceRevaluationLineEntity(TenantId tenantId, Guid id, FinanceRevaluationBatchEntity batch, Guid sourceId, string sourceType, string currency, decimal outstanding, decimal historical, decimal revalued, decimal difference, FinanceFxDirection direction, FinanceExchangeRateEvidence rate)
        : base(tenantId, id)
    { BatchId = batch.Id; CompanyId = batch.CompanyId; SourceId = sourceId; SourceType = sourceType; AsOfDate = batch.AsOfDate; TransactionCurrencyCode = currency; OutstandingTransactionAmount = outstanding; HistoricalFunctionalAmount = historical; RevaluedFunctionalAmount = revalued; Difference = difference; Direction = direction; ExchangeRateId = rate.ExchangeRateId; ExchangeRateVersionId = rate.ExchangeRateVersionId; ExchangeRateVersionNumber = rate.VersionNumber; ExchangeSourceCurrencyCode = rate.SourceCurrencyCode; ExchangeTargetCurrencyCode = rate.TargetCurrencyCode; ExchangeEffectiveOn = rate.EffectiveOn; ExchangeEffectiveFrom = rate.EffectiveFrom; ExchangeEffectiveTo = rate.EffectiveTo; ExchangeRate = rate.Rate; ExchangeRateScale = rate.RateScale; ExchangeProvenance = rate.Provenance; ExchangeSourceNotes = rate.SourceNotes; }
    internal Guid BatchId { get; private set; }
    internal Guid CompanyId { get; private set; }
    internal Guid SourceId { get; private set; }
    internal string SourceType { get; private set; }
    internal DateOnly AsOfDate { get; private set; }
    internal string TransactionCurrencyCode { get; private set; }
    internal decimal OutstandingTransactionAmount { get; private set; }
    internal decimal HistoricalFunctionalAmount { get; private set; }
    internal decimal RevaluedFunctionalAmount { get; private set; }
    internal decimal Difference { get; private set; }
    internal FinanceFxDirection Direction { get; private set; }
    internal Guid ExchangeRateId { get; private set; }
    internal Guid ExchangeRateVersionId { get; private set; }
    internal int ExchangeRateVersionNumber { get; private set; }
    internal string ExchangeSourceCurrencyCode { get; private set; }
    internal string ExchangeTargetCurrencyCode { get; private set; }
    internal DateOnly ExchangeEffectiveOn { get; private set; }
    internal DateOnly? ExchangeEffectiveFrom { get; private set; }
    internal DateOnly? ExchangeEffectiveTo { get; private set; }
    internal decimal ExchangeRate { get; private set; }
    internal int ExchangeRateScale { get; private set; }
    internal string ExchangeProvenance { get; private set; }
    internal string? ExchangeSourceNotes { get; private set; }
    internal Guid? JournalId { get; private set; }
    internal Guid? ReversalJournalId { get; private set; }
    internal FinanceEvidenceStatus Status { get; private set; } = FinanceEvidenceStatus.Captured;
    internal void SetJournal(Guid journalId) { JournalId = journalId; TouchVersion(); }
    internal void SetReversal(Guid journalId) { ReversalJournalId = journalId; Status = FinanceEvidenceStatus.Reversed; TouchVersion(); }
}

#pragma warning restore CS1591
