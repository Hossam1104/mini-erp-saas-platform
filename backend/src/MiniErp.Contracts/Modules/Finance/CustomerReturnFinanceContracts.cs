#pragma warning disable CS1591

namespace MiniErp.Contracts.Modules.Finance;

public enum FinanceCreditNoteStatus
{
    Draft = 1,
    Submitted = 2,
    Approved = 3,
    Posted = 4,
    Rejected = 5,
    Cancelled = 6,
    Reversed = 7,
    Unknown = 8,
    ReconciliationRequired = 9
}

public enum FinanceCustomerCreditStatus
{
    Available = 1,
    PartiallyApplied = 2,
    FullyApplied = 3,
    Reversed = 4,
    Unknown = 5
}

public sealed record FinanceCreditNoteCreateRequest(
    Guid SalesCustomerReturnId,
    DateOnly CreditNoteDate,
    string? Reason,
    Guid? InvoiceId = null,
    IReadOnlyList<FinanceCreditNoteLineRequest>? Lines = null);

public sealed record FinanceCreditNoteLineRequest(Guid SourceAllocationId, decimal Quantity);

public sealed record FinanceCreditNoteActionRequest(string? Reason);

public sealed record FinanceCreditNoteLineResponse(
    Guid Id,
    Guid OrderLineId,
    decimal Quantity,
    decimal NetAmount,
    decimal TaxAmount,
    decimal GrossAmount,
    string CurrencyCode,
    Guid? TaxId,
    Guid? TaxRateVersionId);

public sealed record FinanceCreditNoteResponse(
    Guid Id,
    Guid TenantId,
    Guid SalesCustomerReturnId,
    Guid DeliveryId,
    Guid? InvoiceId,
    Guid? FinanceOpenItemId,
    Guid CompanyId,
    Guid CustomerId,
    FinanceCreditNoteStatus Status,
    string CurrencyCode,
    string FunctionalCurrencyCode,
    decimal NetAmount,
    decimal TaxAmount,
    decimal GrossAmount,
    decimal FunctionalAmount,
    FinanceCustomerCreditStatus? CustomerCreditStatus,
    Guid? CustomerCreditId,
    string SourceEvidence,
    string HandoffState,
    DateOnly CreditNoteDate,
    DateTimeOffset? PostedAt,
    IReadOnlyList<FinanceCreditNoteLineResponse> Lines,
    byte[] Version,
    Guid? PostingJournalId = null,
    Guid? ReversalJournalId = null);

public sealed record FinanceCustomerCreditResponse(
    Guid Id,
    Guid CompanyId,
    Guid CustomerId,
    Guid CreditNoteId,
    Guid? AppliedToOpenItemId,
    string CurrencyCode,
    decimal OriginalAmount,
    decimal AppliedAmount,
    decimal OutstandingAmount,
    FinanceCustomerCreditStatus Status,
    byte[] Version);

#pragma warning restore CS1591
