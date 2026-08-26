#pragma warning disable CS1591

using System.Text.Json;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.Contracts.Modules.Finance;

namespace MiniErp.Infrastructure.Persistence.Modules.Finance;

internal sealed class FinancePeriodCloseEvidenceEntity : FinanceEntity
{
    private FinancePeriodCloseEvidenceEntity() { SnapshotFingerprint = ChecksJson = PeriodVersionJson = string.Empty; }

    internal FinancePeriodCloseEvidenceEntity(
        TenantId tenantId,
        Guid id,
        Guid companyId,
        Guid fiscalYearId,
        Guid periodId,
        FinanceCloseCheckStatus status,
        IReadOnlyList<FinanceCloseCheckRecord> checks,
        string snapshotFingerprint,
        DateTimeOffset evaluatedAt,
        byte[] periodVersion)
        : base(tenantId, id)
    {
        CompanyId = companyId;
        FiscalYearId = fiscalYearId;
        PeriodId = periodId;
        Status = status;
        ChecksJson = JsonSerializer.Serialize(checks);
        SnapshotFingerprint = snapshotFingerprint;
        EvaluatedAt = evaluatedAt;
        PeriodVersionJson = Convert.ToBase64String(periodVersion);
    }

    internal Guid CompanyId { get; private set; }
    internal Guid FiscalYearId { get; private set; }
    internal Guid PeriodId { get; private set; }
    internal FinanceCloseCheckStatus Status { get; private set; }
    internal string ChecksJson { get; private set; }
    internal string SnapshotFingerprint { get; private set; }
    internal DateTimeOffset EvaluatedAt { get; private set; }
    internal string PeriodVersionJson { get; private set; }
}

internal sealed class FinancePeriodCloseRunEntity : FinanceEntity
{
    private FinancePeriodCloseRunEntity() { SnapshotFingerprint = ChecksJson = Reason = CorrelationId = string.Empty; }

    internal FinancePeriodCloseRunEntity(
        TenantId tenantId,
        Guid id,
        Guid companyId,
        Guid fiscalYearId,
        Guid periodId,
        int sequence,
        FinanceCloseCheckStatus readinessStatus,
        IReadOnlyList<FinanceCloseCheckRecord> checks,
        string snapshotFingerprint,
        string reason,
        Guid actorId,
        Guid sessionId,
        string correlationId,
        DateTimeOffset createdAt)
        : base(tenantId, id)
    {
        CompanyId = companyId;
        FiscalYearId = fiscalYearId;
        PeriodId = periodId;
        Sequence = sequence;
        Status = FinanceCloseRunStatus.Closed;
        ReadinessStatus = readinessStatus;
        SnapshotFingerprint = snapshotFingerprint;
        ChecksJson = JsonSerializer.Serialize(checks);
        Reason = reason;
        ActorId = actorId;
        SessionId = sessionId;
        CorrelationId = correlationId;
        CreatedAt = createdAt;
    }

    internal Guid CompanyId { get; private set; }
    internal Guid FiscalYearId { get; private set; }
    internal Guid PeriodId { get; private set; }
    internal int Sequence { get; private set; }
    internal FinanceCloseRunStatus Status { get; private set; }
    internal FinanceCloseCheckStatus ReadinessStatus { get; private set; }
    internal string SnapshotFingerprint { get; private set; }
    internal string ChecksJson { get; private set; }
    internal string Reason { get; private set; }
    internal Guid ActorId { get; private set; }
    internal Guid SessionId { get; private set; }
    internal string CorrelationId { get; private set; }
    internal DateTimeOffset CreatedAt { get; private set; }
    internal DateTimeOffset? ReopenedAt { get; private set; }
    internal Guid? ReopenedBy { get; private set; }

    internal void MarkReopened(Guid actorId, DateTimeOffset at)
    {
        Status = FinanceCloseRunStatus.Reopened;
        ReopenedBy = actorId;
        ReopenedAt = at;
        TouchVersion();
    }
}

internal sealed class FinancePeriodHistoryEntity : FinanceEntity
{
    private FinancePeriodHistoryEntity() { Reason = CorrelationId = string.Empty; }

    internal FinancePeriodHistoryEntity(
        TenantId tenantId,
        Guid id,
        Guid companyId,
        Guid fiscalYearId,
        Guid periodId,
        FinancePeriodHistoryAction action,
        FinanceFiscalPeriodState fromState,
        FinanceFiscalPeriodState toState,
        Guid? closeRunId,
        Guid actorId,
        Guid sessionId,
        string correlationId,
        string reason,
        DateTimeOffset occurredAt)
        : base(tenantId, id)
    {
        CompanyId = companyId;
        FiscalYearId = fiscalYearId;
        PeriodId = periodId;
        Action = action;
        FromState = fromState;
        ToState = toState;
        CloseRunId = closeRunId;
        ActorId = actorId;
        SessionId = sessionId;
        CorrelationId = correlationId;
        Reason = reason;
        OccurredAt = occurredAt;
    }

    internal Guid CompanyId { get; private set; }
    internal Guid FiscalYearId { get; private set; }
    internal Guid PeriodId { get; private set; }
    internal FinancePeriodHistoryAction Action { get; private set; }
    internal FinanceFiscalPeriodState FromState { get; private set; }
    internal FinanceFiscalPeriodState ToState { get; private set; }
    internal Guid? CloseRunId { get; private set; }
    internal Guid ActorId { get; private set; }
    internal Guid SessionId { get; private set; }
    internal string CorrelationId { get; private set; }
    internal string Reason { get; private set; }
    internal DateTimeOffset OccurredAt { get; private set; }
}

internal sealed class FinanceYearEndRunEntity : FinanceEntity
{
    private FinanceYearEndRunEntity() { SnapshotFingerprint = Reason = CorrelationId = string.Empty; Lines = []; }

    internal FinanceYearEndRunEntity(
        TenantId tenantId,
        Guid id,
        Guid companyId,
        Guid fiscalYearId,
        DateOnly asOfDate,
        string reason,
        Guid actorId,
        Guid sessionId,
        string correlationId,
        DateTimeOffset createdAt)
        : base(tenantId, id)
    {
        CompanyId = companyId;
        FiscalYearId = fiscalYearId;
        AsOfDate = asOfDate;
        Status = FinanceYearEndRunStatus.Calculated;
        Reason = reason;
        ActorId = actorId;
        SessionId = sessionId;
        CorrelationId = correlationId;
        CreatedAt = createdAt;
        Lines = [];
    }

    internal Guid CompanyId { get; private set; }
    internal Guid FiscalYearId { get; private set; }
    internal DateOnly AsOfDate { get; private set; }
    internal FinanceYearEndRunStatus Status { get; private set; }
    internal string SnapshotFingerprint { get; private set; } = string.Empty;
    internal Guid? ClosingJournalId { get; private set; }
    internal Guid? ReversalJournalId { get; private set; }
    internal Guid? RetainedEarningsAccountId { get; private set; }
    internal string? RetainedEarningsAccountCode { get; private set; }
    internal Guid? PostingRuleId { get; private set; }
    internal int? PostingRuleVersionNumber { get; private set; }
    internal string? PostingRuleSourceContract { get; private set; }
    internal string? PostingRuleSourceEvent { get; private set; }
    internal string Reason { get; private set; }
    internal Guid ActorId { get; private set; }
    internal Guid SessionId { get; private set; }
    internal string CorrelationId { get; private set; }
    internal DateTimeOffset CreatedAt { get; private set; }
    internal DateTimeOffset? PostedAt { get; private set; }
    internal DateTimeOffset? ReversedAt { get; private set; }
    internal ICollection<FinanceYearEndLineEntity> Lines { get; private set; }

    internal void SetSnapshot(string fingerprint, FinancePostingRuleEntity rule, FinanceAccountEntity retainedEarnings)
    {
        SnapshotFingerprint = fingerprint;
        PostingRuleId = rule.Id;
        PostingRuleVersionNumber = rule.VersionNumber;
        PostingRuleSourceContract = rule.SourceContract;
        PostingRuleSourceEvent = rule.SourceEvent;
        RetainedEarningsAccountId = retainedEarnings.Id;
        RetainedEarningsAccountCode = retainedEarnings.Code;
        TouchVersion();
    }

    internal void MarkPosted(Guid journalId, DateTimeOffset at)
    {
        Status = FinanceYearEndRunStatus.Posted;
        ClosingJournalId = journalId;
        PostedAt = at;
        TouchVersion();
    }

    internal void MarkReversed(Guid journalId, DateTimeOffset at)
    {
        Status = FinanceYearEndRunStatus.Reversed;
        ReversalJournalId = journalId;
        ReversedAt = at;
        TouchVersion();
    }
}

internal sealed class FinanceYearEndLineEntity : FinanceEntity
{
    private FinanceYearEndLineEntity() { AccountCode = AccountName = string.Empty; }

    internal FinanceYearEndLineEntity(
        TenantId tenantId,
        Guid id,
        Guid runId,
        FinanceAccountEntity account,
        decimal debit,
        decimal credit,
        decimal netBalance)
        : base(tenantId, id)
    {
        RunId = runId;
        AccountId = account.Id;
        AccountCode = account.Code;
        AccountName = account.EnglishName;
        AccountNameArabic = account.ArabicName;
        AccountType = account.AccountType;
        Debit = debit;
        Credit = credit;
        NetBalance = netBalance;
    }

    internal Guid RunId { get; private set; }
    internal Guid AccountId { get; private set; }
    internal string AccountCode { get; private set; }
    internal string AccountName { get; private set; }
    internal string? AccountNameArabic { get; private set; }
    internal FinanceAccountType AccountType { get; private set; }
    internal decimal Debit { get; private set; }
    internal decimal Credit { get; private set; }
    internal decimal NetBalance { get; private set; }
    internal Guid? ClosingJournalLineId { get; private set; }

    internal void SetClosingJournalLine(Guid lineId)
    {
        ClosingJournalLineId = lineId;
        TouchVersion();
    }
}

#pragma warning restore CS1591
