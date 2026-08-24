#pragma warning disable CS1591

namespace MiniErp.Contracts.Modules.Finance;

public enum FinanceAccountType
{
    Asset = 1,
    Liability = 2,
    Equity = 3,
    Revenue = 4,
    Expense = 5
}

public enum FinanceAccountLifecycle
{
    Active = 1,
    Inactive = 2
}

public enum FinanceCurrencyBehavior
{
    FunctionalOnly = 1,
    TransactionCurrencyAllowed = 2
}

public enum FinanceCalendarLifecycle
{
    Active = 1,
    Inactive = 2
}

public enum FinanceFiscalYearState
{
    Open = 1,
    Closed = 2
}

public enum FinanceFiscalPeriodState
{
    Draft = 1,
    Open = 2,
    SoftClosed = 3,
    Closed = 4
}

public enum FinanceJournalStatus
{
    Draft = 1,
    Submitted = 2,
    Approved = 3,
    Rejected = 4,
    Cancelled = 5,
    Posted = 6,
    Reversed = 7
}

public enum FinancePostingRuleLifecycle
{
    Enabled = 1,
    Disabled = 2
}

public enum FinanceSourceHandoffStatus
{
    Ready = 1,
    PendingMapping = 2,
    Posted = 3,
    Blocked = 4
}

public sealed record FinanceCompanyOption(
    Guid TenantId,
    Guid CompanyId,
    string CompanyName,
    string FunctionalCurrencyCode,
    Guid? BranchId = null,
    bool IsActive = true);

public sealed record FinanceAccountRecord(
    Guid Id,
    Guid TenantId,
    Guid CompanyId,
    string Code,
    string EnglishName,
    string? ArabicName,
    Guid? ParentAccountId,
    FinanceAccountType AccountType,
    bool IsPostingAccount,
    FinanceAccountLifecycle Lifecycle,
    FinanceCurrencyBehavior CurrencyBehavior,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    byte[] Version);

public sealed record FinanceFiscalCalendarRecord(
    Guid Id,
    Guid TenantId,
    Guid CompanyId,
    string Name,
    string FunctionalCurrencyCode,
    FinanceCalendarLifecycle Lifecycle,
    byte[] Version);

public sealed record FinanceFiscalYearRecord(
    Guid Id,
    Guid CalendarId,
    Guid TenantId,
    Guid CompanyId,
    int YearNumber,
    DateOnly StartDate,
    DateOnly EndDate,
    FinanceFiscalYearState State,
    byte[] Version);

public sealed record FinanceFiscalPeriodRecord(
    Guid Id,
    Guid FiscalYearId,
    Guid TenantId,
    Guid CompanyId,
    int Sequence,
    string Code,
    string? EnglishName,
    string? ArabicName,
    DateOnly StartDate,
    DateOnly EndDate,
    FinanceFiscalPeriodState State,
    byte[] Version);

public sealed record FinanceCostCenterRecord(
    Guid Id,
    Guid TenantId,
    Guid CompanyId,
    string Code,
    string EnglishName,
    string? ArabicName,
    FinanceAccountLifecycle Lifecycle,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    byte[] Version);

public sealed record FinancePostingRuleRecord(
    Guid Id,
    Guid TenantId,
    Guid CompanyId,
    string SourceContract,
    string SourceEvent,
    int VersionNumber,
    Guid DebitAccountId,
    string DebitAccountCode,
    Guid CreditAccountId,
    string CreditAccountCode,
    bool CostCenterRequired,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    FinancePostingRuleLifecycle Lifecycle,
    byte[] Version);

public sealed record FinanceJournalLineRecord(
    Guid Id,
    int LineNumber,
    Guid AccountId,
    string AccountCode,
    string AccountName,
    decimal Debit,
    decimal Credit,
    decimal FunctionalDebit,
    decimal FunctionalCredit,
    decimal? TransactionAmount,
    string? TransactionCurrencyCode,
    Guid? CostCenterId,
    string? CostCenterCode,
    string? Description);

public sealed record FinanceJournalRecord(
    Guid Id,
    Guid TenantId,
    Guid CompanyId,
    long JournalSequence,
    string JournalNumber,
    DateOnly JournalDate,
    DateOnly PostingDate,
    Guid? FiscalYearId,
    Guid? FiscalPeriodId,
    string FunctionalCurrencyCode,
    string? TransactionCurrencyCode,
    decimal? ExchangeRate,
    Guid? ExchangeRateId,
    Guid? ExchangeRateVersionId,
    int? ExchangeRateVersionNumber,
    string SourceContract,
    string SourceEvent,
    Guid? SourceEvidenceId,
    int? SourceEvidenceVersion,
    Guid? PostingRuleId,
    int? PostingRuleVersionNumber,
    string Description,
    FinanceJournalStatus Status,
    Guid CreatedBy,
    Guid? SubmittedBy,
    Guid? ApprovedBy,
    Guid? PostedBy,
    Guid? ReversedBy,
    Guid? ReversalOfJournalId,
    Guid? ReversalJournalId,
    string CorrelationId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? PostedAt,
    IReadOnlyList<FinanceJournalLineRecord> Lines,
    byte[] Version);

public sealed record FinanceHandoffRecord(
    Guid Id,
    Guid TenantId,
    Guid CompanyId,
    Guid MovementId,
    long LedgerSequence,
    string SourceType,
    Guid SourceDocumentId,
    Guid SourceLineId,
    Guid ValuationEvidenceId,
    int ValuationEvidenceVersion,
    decimal Quantity,
    string Direction,
    decimal BaseUnitCost,
    decimal BaseAmount,
    decimal SignedBaseAmount,
    decimal RoundingAdjustmentAmount,
    string FunctionalCurrencyCode,
    string? TransactionCurrencyCode,
    Guid? ExchangeRateId,
    Guid? ExchangeRateVersionId,
    int? ExchangeRateVersionNumber,
    Guid PolicyId,
    int PolicyVersionNumber,
    Guid? CorrectionOfMovementId,
    FinanceSourceHandoffStatus Status,
    Guid? JournalId,
    string ContractVersion,
    string CorrelationId,
    DateTimeOffset AsOf,
    byte[] Version);

public sealed record FinanceGlLineRecord(
    Guid JournalId,
    string JournalNumber,
    DateOnly PostingDate,
    string FunctionalCurrencyCode,
    Guid AccountId,
    string AccountCode,
    string AccountName,
    Guid? FiscalPeriodId,
    Guid? CostCenterId,
    string? CostCenterCode,
    decimal Debit,
    decimal Credit,
    decimal FunctionalDebit,
    decimal FunctionalCredit,
    string SourceContract,
    Guid? SourceEvidenceId,
    bool IsReversal);

public sealed record FinanceOperationResult<T>(bool Succeeded, string Code, T? Value)
{
    public static FinanceOperationResult<T> Success(T value) => new(true, "succeeded", value);
    public static FinanceOperationResult<T> Failure(string code) => new(false, code, default);
}

#pragma warning restore CS1591
