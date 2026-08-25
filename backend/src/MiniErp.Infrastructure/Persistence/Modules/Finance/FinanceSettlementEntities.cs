#pragma warning disable CS1591

using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.Modules.Finance;
using MiniErp.Contracts.Modules.Finance;

namespace MiniErp.Infrastructure.Persistence.Modules.Finance;

internal sealed class FinancePaymentMethodEntity : FinanceEntity
{
    private FinancePaymentMethodEntity() { Code = EnglishName = string.Empty; }
    internal FinancePaymentMethodEntity(TenantId tenantId, FinancePaymentMethodCommand command) : base(tenantId, command.Id)
    {
        CompanyId = command.CompanyId; Code = command.Code; EnglishName = command.EnglishName; ArabicName = command.ArabicName;
        Direction = command.Direction; IsManual = command.IsManual; RequiresReference = command.RequiresReference;
        EffectiveFrom = command.EffectiveFrom; EffectiveTo = command.EffectiveTo; Lifecycle = FinancePaymentMethodLifecycle.Active;
    }
    internal Guid CompanyId { get; private set; }
    internal string Code { get; private set; }
    internal string EnglishName { get; private set; }
    internal string? ArabicName { get; private set; }
    internal FinancePaymentMethodDirection Direction { get; private set; }
    internal FinancePaymentMethodLifecycle Lifecycle { get; private set; }
    internal bool IsManual { get; private set; }
    internal bool RequiresReference { get; private set; }
    internal DateOnly EffectiveFrom { get; private set; }
    internal DateOnly? EffectiveTo { get; private set; }
    internal void Edit(FinancePaymentMethodCommand command)
    {
        Code = command.Code; EnglishName = command.EnglishName; ArabicName = command.ArabicName; Direction = command.Direction;
        IsManual = command.IsManual; RequiresReference = command.RequiresReference; EffectiveFrom = command.EffectiveFrom; EffectiveTo = command.EffectiveTo; TouchVersion();
    }
    internal void SetLifecycle(FinancePaymentMethodLifecycle lifecycle) { Lifecycle = lifecycle; TouchVersion(); }
}

internal sealed class FinanceCashAccountEntity : FinanceEntity
{
    private FinanceCashAccountEntity() { Code = EnglishName = CurrencyCode = string.Empty; }
    internal FinanceCashAccountEntity(TenantId tenantId, FinanceCashAccountCommand command, string currencyCode) : base(tenantId, command.Id)
    {
        CompanyId = command.CompanyId; Code = command.Code; EnglishName = command.EnglishName; ArabicName = command.ArabicName;
        Kind = command.Kind; CurrencyCode = currencyCode; LinkedAccountId = command.LinkedAccountId; BankReference = command.BankReference;
        EffectiveFrom = command.EffectiveFrom; EffectiveTo = command.EffectiveTo; Lifecycle = FinancePaymentMethodLifecycle.Active;
    }
    internal Guid CompanyId { get; private set; }
    internal string Code { get; private set; }
    internal string EnglishName { get; private set; }
    internal string? ArabicName { get; private set; }
    internal FinanceCashAccountKind Kind { get; private set; }
    internal string CurrencyCode { get; private set; }
    internal Guid LinkedAccountId { get; private set; }
    internal string LinkedAccountCode { get; private set; } = string.Empty;
    internal string? BankReference { get; private set; }
    internal FinancePaymentMethodLifecycle Lifecycle { get; private set; }
    internal DateOnly EffectiveFrom { get; private set; }
    internal DateOnly? EffectiveTo { get; private set; }
    internal void Edit(FinanceCashAccountCommand command, string currencyCode, string linkedAccountCode)
    {
        Code = command.Code; EnglishName = command.EnglishName; ArabicName = command.ArabicName; Kind = command.Kind; CurrencyCode = currencyCode;
        LinkedAccountId = command.LinkedAccountId; LinkedAccountCode = linkedAccountCode; BankReference = command.BankReference;
        EffectiveFrom = command.EffectiveFrom; EffectiveTo = command.EffectiveTo; TouchVersion();
    }
    internal void SetLinkedAccountCode(string code) => LinkedAccountCode = code;
    internal void SetLifecycle(FinancePaymentMethodLifecycle lifecycle) { Lifecycle = lifecycle; TouchVersion(); }
}

internal sealed class FinanceOpenItemEntity : FinanceEntity
{
    private FinanceOpenItemEntity() { SourceContract = Reference = CurrencyCode = FunctionalCurrencyCode = string.Empty; }
    internal FinanceOpenItemEntity(TenantId tenantId, Guid id, FinanceOpenItemKind kind, Guid companyId, Guid? supplierId, Guid? customerId,
        string sourceContract, Guid sourceDocumentId, int sourceDocumentVersion, Guid sourceEvidenceId, int sourceEvidenceVersion,
        string? reference, DateOnly documentDate, DateOnly dueDate, string currencyCode, decimal amount, string functionalCurrencyCode,
        decimal functionalAmount, decimal? exchangeRate, Guid? exchangeRateId, Guid? exchangeRateVersionId, int? exchangeRateVersionNumber,
        FinancePaymentTermSnapshotRecord? paymentTerm, Guid? matchEvidenceId, int? matchEvidenceVersion, string? sourceSnapshot) : base(tenantId, id)
    {
        Kind = kind; CompanyId = companyId; SupplierId = supplierId; CustomerId = customerId; SourceContract = sourceContract;
        SourceDocumentId = sourceDocumentId; SourceDocumentVersion = sourceDocumentVersion; SourceEvidenceId = sourceEvidenceId; SourceEvidenceVersion = sourceEvidenceVersion;
        Reference = reference; DocumentDate = documentDate; DueDate = dueDate; CurrencyCode = currencyCode; OriginalAmount = amount;
        FunctionalCurrencyCode = functionalCurrencyCode; OriginalFunctionalAmount = functionalAmount; ExchangeRate = exchangeRate;
        ExchangeRateId = exchangeRateId; ExchangeRateVersionId = exchangeRateVersionId; ExchangeRateVersionNumber = exchangeRateVersionNumber;
        PaymentTermId = paymentTerm?.Id; PaymentTermCode = paymentTerm?.Code; PaymentTermEnglishName = paymentTerm?.EnglishName;
        PaymentTermArabicName = paymentTerm?.ArabicName; PaymentTermVersionNumber = paymentTerm?.VersionNumber; PaymentTermVersionId = paymentTerm?.VersionId;
        PaymentTermEffectiveOn = paymentTerm?.EffectiveOn; MatchEvidenceId = matchEvidenceId; MatchEvidenceVersion = matchEvidenceVersion;
        SourceSnapshot = sourceSnapshot; RecognitionState = FinanceOpenItemRecognitionState.PendingPosting;
    }
    internal Guid CompanyId { get; private set; }
    internal FinanceOpenItemKind Kind { get; private set; }
    internal Guid? SupplierId { get; private set; }
    internal Guid? CustomerId { get; private set; }
    internal string SourceContract { get; private set; }
    internal Guid SourceDocumentId { get; private set; }
    internal int SourceDocumentVersion { get; private set; }
    internal Guid SourceEvidenceId { get; private set; }
    internal int SourceEvidenceVersion { get; private set; }
    internal string? Reference { get; private set; }
    internal DateOnly DocumentDate { get; private set; }
    internal DateOnly DueDate { get; private set; }
    internal string CurrencyCode { get; private set; }
    internal decimal OriginalAmount { get; private set; }
    internal string FunctionalCurrencyCode { get; private set; }
    internal decimal OriginalFunctionalAmount { get; private set; }
    internal decimal? ExchangeRate { get; private set; }
    internal Guid? ExchangeRateId { get; private set; }
    internal Guid? ExchangeRateVersionId { get; private set; }
    internal int? ExchangeRateVersionNumber { get; private set; }
    internal Guid? PaymentTermId { get; private set; }
    internal string? PaymentTermCode { get; private set; }
    internal string? PaymentTermEnglishName { get; private set; }
    internal string? PaymentTermArabicName { get; private set; }
    internal int? PaymentTermVersionNumber { get; private set; }
    internal Guid? PaymentTermVersionId { get; private set; }
    internal DateOnly? PaymentTermEffectiveOn { get; private set; }
    internal Guid? MatchEvidenceId { get; private set; }
    internal int? MatchEvidenceVersion { get; private set; }
    internal FinanceOpenItemRecognitionState RecognitionState { get; private set; }
    internal Guid? RecognitionJournalId { get; private set; }
    internal string? SourceSnapshot { get; private set; }
    internal void SetRecognition(FinanceOpenItemRecognitionState state, Guid? journalId) { RecognitionState = state; RecognitionJournalId = journalId; TouchVersion(); }
}

internal sealed class FinanceSettlementDocumentEntity : FinanceEntity
{
    private FinanceSettlementDocumentEntity() { CurrencyCode = FunctionalCurrencyCode = string.Empty; }
    internal FinanceSettlementDocumentEntity(TenantId tenantId, FinanceSettlementDocumentCommand command, string currencyCode, string functionalCurrencyCode, decimal functionalAmount, Guid actorId, DateTimeOffset at) : base(tenantId, command.Id)
    {
        CompanyId = command.CompanyId; Direction = command.Direction; SupplierId = command.SupplierId; CustomerId = command.CustomerId;
        CashAccountId = command.CashAccountId; PaymentMethodId = command.PaymentMethodId; DocumentDate = command.DocumentDate;
        CurrencyCode = currencyCode; Amount = command.Amount; FunctionalCurrencyCode = functionalCurrencyCode; FunctionalAmount = functionalAmount;
        ExchangeRate = command.ExchangeRate; ExchangeRateId = command.ExchangeRateId; ExchangeRateVersionId = command.ExchangeRateVersionId; ExchangeRateVersionNumber = command.ExchangeRateVersionNumber;
        ExternalReference = command.ExternalReference; Description = command.Description; Status = FinanceSettlementDocumentStatus.Draft; CreatedBy = actorId; CreatedAt = at;
    }
    internal Guid CompanyId { get; private set; }
    internal FinanceSettlementDocumentStatus Status { get; private set; }
    internal FinancePaymentMethodDirection Direction { get; private set; }
    internal Guid? SupplierId { get; private set; }
    internal Guid? CustomerId { get; private set; }
    internal Guid CashAccountId { get; private set; }
    internal Guid PaymentMethodId { get; private set; }
    internal DateOnly DocumentDate { get; private set; }
    internal string CurrencyCode { get; private set; }
    internal decimal Amount { get; private set; }
    internal string FunctionalCurrencyCode { get; private set; }
    internal decimal FunctionalAmount { get; private set; }
    internal decimal? ExchangeRate { get; private set; }
    internal Guid? ExchangeRateId { get; private set; }
    internal Guid? ExchangeRateVersionId { get; private set; }
    internal int? ExchangeRateVersionNumber { get; private set; }
    internal string? ExternalReference { get; private set; }
    internal string? Description { get; private set; }
    internal Guid CreatedBy { get; private set; }
    internal Guid? SubmittedBy { get; private set; }
    internal Guid? ApprovedBy { get; private set; }
    internal Guid? PostedBy { get; private set; }
    internal Guid? ReversedBy { get; private set; }
    internal Guid? PostedJournalId { get; private set; }
    internal Guid? ReversalJournalId { get; private set; }
    internal DateTimeOffset CreatedAt { get; private set; }
    internal DateTimeOffset? PostedAt { get; private set; }
    internal void Edit(FinanceSettlementDocumentCommand command, string currencyCode, decimal functionalAmount)
    {
        CashAccountId = command.CashAccountId; PaymentMethodId = command.PaymentMethodId; DocumentDate = command.DocumentDate; CurrencyCode = currencyCode;
        Amount = command.Amount; FunctionalAmount = functionalAmount; ExchangeRate = command.ExchangeRate; ExchangeRateId = command.ExchangeRateId; ExchangeRateVersionId = command.ExchangeRateVersionId; ExchangeRateVersionNumber = command.ExchangeRateVersionNumber;
        ExternalReference = command.ExternalReference; Description = command.Description; TouchVersion();
    }
    internal void SetStatus(FinanceSettlementDocumentStatus status, Guid actorId, DateTimeOffset at)
    {
        Status = status; if (status == FinanceSettlementDocumentStatus.Submitted) SubmittedBy = actorId; if (status == FinanceSettlementDocumentStatus.Approved) ApprovedBy = actorId;
        if (status == FinanceSettlementDocumentStatus.Posted) { PostedBy = actorId; PostedAt = at; } if (status == FinanceSettlementDocumentStatus.Reversed) ReversedBy = actorId; TouchVersion();
    }
    internal void ReturnToDraft(Guid actorId, DateTimeOffset at) => SetStatus(FinanceSettlementDocumentStatus.Draft, actorId, at);
    internal void SetPostedJournal(Guid journalId) { PostedJournalId = journalId; TouchVersion(); }
    internal void SetReversal(Guid journalId) { ReversalJournalId = journalId; TouchVersion(); }
}

internal sealed class FinanceAllocationEntity : FinanceEntity
{
    private FinanceAllocationEntity() { CurrencyCode = string.Empty; }
    internal FinanceAllocationEntity(TenantId tenantId, FinanceAllocationCommand command, Guid companyId, string currencyCode, decimal functionalAmount, Guid actorId) : base(tenantId, command.Id)
    {
        CompanyId = companyId; SettlementDocumentId = command.SettlementDocumentId; OpenItemId = command.OpenItemId; Amount = command.Amount; CurrencyCode = currencyCode;
        FunctionalAmount = functionalAmount; AllocationDate = command.AllocationDate; Status = FinanceAllocationStatus.Active; CreatedBy = actorId; Reason = command.Reason;
    }
    internal FinanceAllocationEntity(TenantId tenantId, FinanceAllocationReversalCommand command, FinanceAllocationEntity original, Guid companyId, Guid actorId) : base(tenantId, command.Id)
    {
        CompanyId = companyId; SettlementDocumentId = original.SettlementDocumentId; OpenItemId = original.OpenItemId; Amount = original.Amount; CurrencyCode = original.CurrencyCode;
        FunctionalAmount = original.FunctionalAmount; AllocationDate = DateOnly.FromDateTime(DateTime.UtcNow); Status = FinanceAllocationStatus.Reversed; ReversalOfAllocationId = original.Id; CreatedBy = actorId; Reason = command.Reason;
    }
    internal Guid CompanyId { get; private set; }
    internal Guid SettlementDocumentId { get; private set; }
    internal Guid OpenItemId { get; private set; }
    internal decimal Amount { get; private set; }
    internal string CurrencyCode { get; private set; }
    internal decimal FunctionalAmount { get; private set; }
    internal DateOnly AllocationDate { get; private set; }
    internal FinanceAllocationStatus Status { get; private set; }
    internal Guid? ReversalOfAllocationId { get; private set; }
    internal Guid? JournalId { get; private set; }
    internal Guid CreatedBy { get; private set; }
    internal string? Reason { get; private set; }
    internal void SetJournal(Guid journalId) { JournalId = journalId; TouchVersion(); }
}

#pragma warning restore CS1591
