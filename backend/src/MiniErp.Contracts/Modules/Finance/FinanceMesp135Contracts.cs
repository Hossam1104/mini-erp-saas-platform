#pragma warning disable CS1591

namespace MiniErp.Contracts.Modules.Finance;

public enum FinanceCloseCheckStatus
{
    Ready = 1,
    Warning = 2,
    Blocked = 3
}

public enum FinanceCloseRunStatus
{
    Closed = 1,
    Reopened = 2
}

public enum FinancePeriodHistoryAction
{
    Opened = 1,
    Closed = 2,
    Reopened = 3,
    Reclosed = 4
}

public enum FinanceYearEndRunStatus
{
    Calculated = 1,
    Posted = 2,
    Reversed = 3
}

public enum FinanceReconciliationViewStatus
{
    Reconciled = 1,
    Pending = 2,
    Blocked = 3,
    Mismatch = 4,
    LegacyWithoutEvidence = 5
}

public enum FinanceStatementKind
{
    ProfitAndLoss = 1,
    BalanceSheet = 2
}

public sealed record FinanceCloseCheckRecord(
    string Code,
    FinanceCloseCheckStatus Status,
    string Message,
    decimal? ExpectedAmount = null,
    decimal? ActualAmount = null);

public sealed record FinanceCloseReadinessRecord(
    Guid PeriodId,
    Guid FiscalYearId,
    Guid TenantId,
    Guid CompanyId,
    FinanceCloseCheckStatus Status,
    IReadOnlyList<FinanceCloseCheckRecord> Checks,
    string SnapshotFingerprint,
    DateTimeOffset EvaluatedAt,
    byte[] PeriodVersion);

public sealed record FinancePeriodCloseRunRecord(
    Guid Id,
    Guid TenantId,
    Guid CompanyId,
    Guid FiscalYearId,
    Guid PeriodId,
    int Sequence,
    FinanceCloseRunStatus Status,
    FinanceCloseCheckStatus ReadinessStatus,
    string SnapshotFingerprint,
    IReadOnlyList<FinanceCloseCheckRecord> Checks,
    string Reason,
    Guid ActorId,
    Guid SessionId,
    string CorrelationId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? ReopenedAt,
    Guid? ReopenedBy,
    byte[] Version);

public sealed record FinancePeriodHistoryRecord(
    Guid Id,
    Guid TenantId,
    Guid CompanyId,
    Guid FiscalYearId,
    Guid PeriodId,
    FinancePeriodHistoryAction Action,
    FinanceFiscalPeriodState FromState,
    FinanceFiscalPeriodState ToState,
    Guid? CloseRunId,
    Guid ActorId,
    Guid SessionId,
    string CorrelationId,
    string Reason,
    DateTimeOffset OccurredAt);

public sealed record FinanceYearEndLineRecord(
    Guid Id,
    Guid RunId,
    Guid AccountId,
    string AccountCode,
    string AccountName,
    string? AccountNameArabic,
    FinanceAccountType AccountType,
    decimal Debit,
    decimal Credit,
    decimal NetBalance,
    Guid? ClosingJournalLineId);

public sealed record FinanceYearEndRunRecord(
    Guid Id,
    Guid TenantId,
    Guid CompanyId,
    Guid FiscalYearId,
    DateOnly AsOfDate,
    FinanceYearEndRunStatus Status,
    string SnapshotFingerprint,
    IReadOnlyList<FinanceYearEndLineRecord> Lines,
    Guid? ClosingJournalId,
    Guid? ReversalJournalId,
    Guid? RetainedEarningsAccountId,
    string? RetainedEarningsAccountCode,
    Guid? PostingRuleId,
    int? PostingRuleVersionNumber,
    string? PostingRuleSourceContract,
    string? PostingRuleSourceEvent,
    string Reason,
    Guid ActorId,
    Guid SessionId,
    string CorrelationId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? PostedAt,
    DateTimeOffset? ReversedAt,
    byte[] Version);

public sealed record FinanceTrialBalanceQuery(
    Guid CompanyId,
    DateOnly AsOfDate,
    Guid? FiscalPeriodId = null,
    Guid? AccountId = null,
    Guid? CostCenterId = null,
    string? AccountFrom = null,
    string? AccountTo = null,
    string? PresentationCurrencyCode = null);

public sealed record FinanceTrialBalanceRow(
    Guid AccountId,
    string AccountCode,
    string AccountName,
    string? AccountNameArabic,
    FinanceAccountType AccountType,
    decimal OpeningBalance,
    decimal PeriodDebit,
    decimal PeriodCredit,
    decimal ClosingBalance,
    string FunctionalCurrencyCode,
    string? ReportingCurrencyCode,
    decimal? ReportingOpeningBalance,
    decimal? ReportingDebit,
    decimal? ReportingCredit,
    decimal? ReportingClosingBalance,
    FinanceEvidenceStatus ReportingEvidenceStatus);

public sealed record FinanceTrialBalanceReport(
    Guid CompanyId,
    DateOnly AsOfDate,
    DateOnly? FromDate,
    DateOnly? ToDate,
    IReadOnlyList<FinanceTrialBalanceRow> Rows,
    decimal TotalDebit,
    decimal TotalCredit,
    decimal TotalClosingBalance,
    string FunctionalCurrencyCode,
    string? ReportingCurrencyCode,
    FinanceEvidenceStatus ReportingEvidenceStatus);

public sealed record FinanceGeneralLedgerQuery(
    Guid CompanyId,
    Guid? AccountId = null,
    Guid? FiscalPeriodId = null,
    DateOnly? FromDate = null,
    DateOnly? ToDate = null,
    Guid? CostCenterId = null,
    string? SourceContract = null,
    string? PresentationCurrencyCode = null);

public sealed record FinanceGeneralLedgerLineRecord(
    Guid JournalId,
    string JournalNumber,
    long JournalSequence,
    DateOnly PostingDate,
    Guid AccountId,
    string AccountCode,
    string AccountName,
    string? AccountNameArabic,
    Guid? FiscalPeriodId,
    Guid? CostCenterId,
    string? CostCenterCode,
    string SourceContract,
    string SourceEvent,
    Guid? SourceEvidenceId,
    int LineNumber,
    decimal Debit,
    decimal Credit,
    decimal FunctionalDebit,
    decimal FunctionalCredit,
    decimal RunningBalance,
    string FunctionalCurrencyCode,
    string? TransactionCurrencyCode,
    decimal? TransactionAmount,
    decimal? ReportingAmount,
    FinanceEvidenceStatus ReportingEvidenceStatus,
    bool IsReversal);

public sealed record FinanceAgingReportQuery(
    Guid CompanyId,
    DateOnly AsOfDate,
    FinanceOpenItemKind Kind,
    Guid? PartyId = null,
    string? CurrencyCode = null);

public sealed record FinanceAgingReportRow(
    Guid OpenItemId,
    FinanceOpenItemKind Kind,
    Guid? SupplierId,
    Guid? CustomerId,
    string? SourceReference,
    DateOnly DocumentDate,
    DateOnly DueDate,
    DateOnly AsOfDate,
    int DaysOverdue,
    string AgingBucket,
    string CurrencyCode,
    decimal OriginalAmount,
    decimal AllocatedAmount,
    decimal OutstandingAmount,
    string FunctionalCurrencyCode,
    decimal OriginalFunctionalAmount,
    decimal OutstandingFunctionalAmount,
    FinanceOpenItemStatus Status);

public sealed record FinanceReconciliationViewRecord(
    Guid CompanyId,
    DateOnly AsOfDate,
    string Scope,
    FinanceReconciliationViewStatus Status,
    decimal? ExpectedAmount,
    decimal? ActualAmount,
    decimal? Difference,
    string? SourceReference,
    string? Detail,
    bool HasDurableEvidence);

public sealed record FinanceCloseReconciliationRecord(
    Guid CompanyId,
    Guid? PeriodId,
    DateOnly AsOfDate,
    FinanceReconciliationViewStatus OverallStatus,
    IReadOnlyList<FinanceReconciliationViewRecord> Items,
    IReadOnlyList<FinancePeriodCloseRunRecord> CloseHistory,
    IReadOnlyList<FinanceYearEndRunRecord> YearEndRuns);

public sealed record FinanceStatementRow(
    Guid AccountId,
    string AccountCode,
    string AccountName,
    string? AccountNameArabic,
    FinanceAccountType AccountType,
    decimal OpeningBalance,
    decimal Debit,
    decimal Credit,
    decimal ClosingBalance,
    string FunctionalCurrencyCode);

public sealed record FinanceStatementReport(
    FinanceStatementKind Kind,
    Guid CompanyId,
    DateOnly FromDate,
    DateOnly ToDate,
    IReadOnlyList<FinanceStatementRow> Rows,
    decimal TotalDebit,
    decimal TotalCredit,
    decimal TotalClosingBalance,
    string FunctionalCurrencyCode,
    string? Finding);

#pragma warning restore CS1591
