#pragma warning disable CS1591

namespace MiniErp.Contracts.Modules.Finance;

public enum FinancePaymentMethodDirection
{
    Payment = 1,
    Receipt = 2,
    Both = 3
}

public enum FinancePaymentMethodLifecycle
{
    Active = 1,
    Inactive = 2
}

public enum FinanceCashAccountKind
{
    Cash = 1,
    Bank = 2
}

public enum FinanceSettlementDocumentStatus
{
    Draft = 1,
    Submitted = 2,
    Approved = 3,
    Rejected = 4,
    Cancelled = 5,
    Posted = 6,
    Reversed = 7
}

public enum FinanceOpenItemKind
{
    Payable = 1,
    Receivable = 2
}

public enum FinanceOpenItemRecognitionState
{
    PendingPosting = 1,
    Recognized = 2,
    Blocked = 3
}

public enum FinanceOpenItemStatus
{
    Open = 1,
    PartiallySettled = 2,
    Settled = 3,
    OnHold = 4,
    Reversed = 5
}

public enum FinanceAllocationStatus
{
    Active = 1,
    Reversed = 2
}

public enum FinanceReconciliationStatus
{
    Reconciled = 1,
    PendingPosting = 2,
    PendingMapping = 3,
    PendingApproval = 4,
    PendingFxRecognition = 5,
    AmountMismatch = 6,
    Unreconciled = 7
}

public sealed record FinancePaymentMethodRecord(
    Guid Id,
    Guid TenantId,
    Guid CompanyId,
    string Code,
    string EnglishName,
    string? ArabicName,
    FinancePaymentMethodDirection Direction,
    FinancePaymentMethodLifecycle Lifecycle,
    bool IsManual,
    bool RequiresReference,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    byte[] Version);

public sealed record FinanceCashAccountRecord(
    Guid Id,
    Guid TenantId,
    Guid CompanyId,
    string Code,
    string EnglishName,
    string? ArabicName,
    FinanceCashAccountKind Kind,
    string CurrencyCode,
    Guid LinkedAccountId,
    string LinkedAccountCode,
    string? BankReference,
    FinancePaymentMethodLifecycle Lifecycle,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    byte[] Version);

public sealed record FinancePaymentTermSnapshotRecord(
    Guid Id,
    string Code,
    string? EnglishName,
    string? ArabicName,
    int VersionNumber,
    Guid VersionId,
    DateOnly EffectiveOn,
    DateOnly? DueDate);

public sealed record FinanceOpenItemRecord(
    Guid Id,
    Guid TenantId,
    Guid CompanyId,
    FinanceOpenItemKind Kind,
    Guid? SupplierId,
    Guid? CustomerId,
    string SourceContract,
    Guid SourceDocumentId,
    int SourceDocumentVersion,
    Guid SourceEvidenceId,
    int SourceEvidenceVersion,
    string? Reference,
    DateOnly DocumentDate,
    DateOnly DueDate,
    string CurrencyCode,
    decimal OriginalAmount,
    string FunctionalCurrencyCode,
    decimal OriginalFunctionalAmount,
    decimal? ExchangeRate,
    Guid? ExchangeRateId,
    Guid? ExchangeRateVersionId,
    int? ExchangeRateVersionNumber,
    FinancePaymentTermSnapshotRecord? PaymentTerm,
    Guid? MatchEvidenceId,
    int? MatchEvidenceVersion,
    FinanceOpenItemRecognitionState RecognitionState,
    Guid? RecognitionJournalId,
    decimal AllocatedAmount,
    decimal OutstandingAmount,
    FinanceOpenItemStatus Status,
    byte[] Version);

public sealed record FinanceSettlementDocumentRecord(
    Guid Id,
    Guid TenantId,
    Guid CompanyId,
    FinanceSettlementDocumentStatus Status,
    FinancePaymentMethodDirection Direction,
    Guid? SupplierId,
    Guid? CustomerId,
    Guid CashAccountId,
    Guid PaymentMethodId,
    DateOnly DocumentDate,
    string CurrencyCode,
    decimal Amount,
    string FunctionalCurrencyCode,
    decimal FunctionalAmount,
    decimal? ExchangeRate,
    Guid? ExchangeRateId,
    Guid? ExchangeRateVersionId,
    int? ExchangeRateVersionNumber,
    string? ExternalReference,
    string? Description,
    Guid CreatedBy,
    Guid? SubmittedBy,
    Guid? ApprovedBy,
    Guid? PostedBy,
    Guid? ReversedBy,
    Guid? PostedJournalId,
    Guid? ReversalJournalId,
    decimal AllocatedAmount,
    decimal UnallocatedAmount,
    byte[] Version);

public sealed record FinanceAllocationRecord(
    Guid Id,
    Guid TenantId,
    Guid CompanyId,
    Guid SettlementDocumentId,
    Guid OpenItemId,
    decimal Amount,
    string CurrencyCode,
    decimal FunctionalAmount,
    DateOnly AllocationDate,
    FinanceAllocationStatus Status,
    Guid? ReversalOfAllocationId,
    Guid? JournalId,
    Guid CreatedBy,
    string? Reason,
    byte[] Version);

public sealed record FinanceAgingRecord(
    Guid OpenItemId,
    FinanceOpenItemKind Kind,
    Guid? SupplierId,
    Guid? CustomerId,
    string? Reference,
    DateOnly DocumentDate,
    DateOnly DueDate,
    DateOnly AsOfDate,
    int DaysOverdue,
    string CurrencyCode,
    decimal OriginalAmount,
    decimal AllocatedAmount,
    decimal OutstandingAmount,
    FinanceOpenItemStatus Status);

public sealed record FinanceCustomerExposureRecord(
    Guid CompanyId,
    Guid CustomerId,
    string CurrencyCode,
    decimal OpenReceivables,
    decimal OverdueReceivables,
    decimal UnappliedCredits,
    decimal NetReceivableExposure,
    DateOnly AsOfDate,
    bool CreditHold,
    string? HoldReason);

public sealed record FinanceReconciliationRecord(
    Guid CompanyId,
    FinanceOpenItemKind? Kind,
    string Scope,
    decimal SubledgerAmount,
    decimal PostedJournalAmount,
    decimal Difference,
    FinanceReconciliationStatus Status,
    DateOnly AsOfDate);

#pragma warning restore CS1591
