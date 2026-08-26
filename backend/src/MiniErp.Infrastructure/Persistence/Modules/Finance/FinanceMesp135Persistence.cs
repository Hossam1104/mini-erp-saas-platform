#pragma warning disable CS1591

using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.Modules.Finance;
using MiniErp.App.Modules.MasterData;
using MiniErp.Contracts.Modules.Finance;

namespace MiniErp.Infrastructure.Persistence.Modules.Finance;

internal sealed class FinanceMesp135Persistence(
    DbContextOptions options,
    IFinanceCompanyProvider companies,
    IFinanceSettlementPersistence settlements,
    IFinanceMesp134Persistence mesp134,
    IMasterDataExchangeRatePersistence exchangeRates) : IFinanceMesp135Persistence
{
    private const string YearEndContract = "finance-year-end.v1";
    private const string CorrectionContract = "finance-correction.v1";
    private const string ManualContract = "manual-journal.v1";
    private readonly DbContextOptions options = options;
    private readonly IFinanceCompanyProvider companies = companies;
    private readonly IFinanceSettlementPersistence settlements = settlements;
    private readonly IFinanceMesp134Persistence mesp134 = mesp134;
    private readonly IMasterDataExchangeRatePersistence exchangeRates = exchangeRates;

    public async Task<FinanceOperationResult<FinanceCloseReadinessRecord>> EvaluateCloseReadinessAsync(FinanceRequestContext context, FinanceCloseReadinessQuery query, CancellationToken cancellationToken = default)
    {
        if (Company(context, query.CompanyId) is null) return Failure<FinanceCloseReadinessRecord>("company_scope_denied");
        await using var db = CreateContext(context);
        var evaluation = await EvaluateReadinessAsync(db, context, query.CompanyId, query.PeriodId, cancellationToken);
        if (!evaluation.Succeeded || evaluation.Value is null) return Failure<FinanceCloseReadinessRecord>(evaluation.Code);
        if (!await db.PeriodCloseEvidence.AnyAsync(item => item.PeriodId == query.PeriodId && item.SnapshotFingerprint == evaluation.Value.SnapshotFingerprint, cancellationToken))
        {
            db.PeriodCloseEvidence.Add(new FinancePeriodCloseEvidenceEntity(context.TenantId, Guid.NewGuid(), query.CompanyId, evaluation.Value.FiscalYearId, query.PeriodId, evaluation.Value.Status, evaluation.Value.Checks, evaluation.Value.SnapshotFingerprint, evaluation.Value.EvaluatedAt, evaluation.Value.PeriodVersion));
            await db.SaveChangesAsync(cancellationToken);
        }
        return evaluation;
    }

    public async Task<FinanceOperationResult<FinancePeriodCloseRunRecord>> ClosePeriodAsync(FinanceRequestContext context, FinancePeriodCloseCommand command, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.Reason)) return Failure<FinancePeriodCloseRunRecord>("reason_required");
        await using var db = CreateContext(context);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var replay = await ReadReplayAsync<FinancePeriodCloseRunRecord>(db, context, "finance.period.close", command.IdempotencyKey, command.RequestFingerprint, cancellationToken);
        if (replay is not null) return replay;
        var period = await db.FiscalPeriods.SingleOrDefaultAsync(item => item.Id == command.PeriodId && item.CompanyId == command.CompanyId, cancellationToken);
        var year = period is null ? null : await db.FiscalYears.SingleOrDefaultAsync(item => item.Id == period.FiscalYearId && item.CompanyId == command.CompanyId, cancellationToken);
        if (period is null || year is null || Company(context, command.CompanyId) is null) return Failure<FinancePeriodCloseRunRecord>("company_scope_denied");
        if (!period.Version.SequenceEqual(command.ExpectedVersion)) return Failure<FinancePeriodCloseRunRecord>("concurrency_conflict");
        if (period.State == FinanceFiscalPeriodState.Closed) return Failure<FinancePeriodCloseRunRecord>("period_already_closed");
        if (period.State != FinanceFiscalPeriodState.Open && period.State != FinanceFiscalPeriodState.SoftClosed) return Failure<FinancePeriodCloseRunRecord>("period_not_closable");
        var evaluation = await EvaluateReadinessAsync(db, context, command.CompanyId, command.PeriodId, cancellationToken);
        if (!evaluation.Succeeded || evaluation.Value is null) return Failure<FinancePeriodCloseRunRecord>(evaluation.Code);
        if (evaluation.Value.Status == FinanceCloseCheckStatus.Blocked) return Failure<FinancePeriodCloseRunRecord>("period_close_blocked");
        var now = DateTimeOffset.UtcNow;
        var fromState = period.State;
        var sequence = (await db.PeriodCloseRuns.Where(item => item.PeriodId == period.Id).Select(item => (int?)item.Sequence).MaxAsync(cancellationToken) ?? 0) + 1;
        var run = new FinancePeriodCloseRunEntity(context.TenantId, command.Id, command.CompanyId, year.Id, period.Id, sequence, evaluation.Value.Status, evaluation.Value.Checks, evaluation.Value.SnapshotFingerprint, command.Reason.Trim(), context.ActorId, context.SessionId, context.CorrelationId, now);
        if (!await db.PeriodCloseEvidence.AnyAsync(item => item.PeriodId == period.Id && item.SnapshotFingerprint == evaluation.Value.SnapshotFingerprint, cancellationToken))
            db.PeriodCloseEvidence.Add(new FinancePeriodCloseEvidenceEntity(context.TenantId, Guid.NewGuid(), command.CompanyId, year.Id, period.Id, evaluation.Value.Status, evaluation.Value.Checks, evaluation.Value.SnapshotFingerprint, now, period.Version));
        period.SetState(FinanceFiscalPeriodState.Closed);
        db.PeriodCloseRuns.Add(run);
        db.PeriodHistory.Add(new FinancePeriodHistoryEntity(context.TenantId, Guid.NewGuid(), command.CompanyId, year.Id, period.Id, sequence == 1 ? FinancePeriodHistoryAction.Closed : FinancePeriodHistoryAction.Reclosed, fromState, FinanceFiscalPeriodState.Closed, run.Id, context.ActorId, context.SessionId, context.CorrelationId, command.Reason.Trim(), now));
        AddAudit(db, context, "finance.period.close", "period-close-run", run.Id, "Succeeded", command.Reason, command.IdempotencyKey, now);
        await db.SaveChangesAsync(cancellationToken);
        var result = FinanceOperationResult<FinancePeriodCloseRunRecord>.Success(ToCloseRun(run));
        AddReplay(db, context, "finance.period.close", command.IdempotencyKey, command.RequestFingerprint, "period-close-run", run.Id, result.Value!, now);
        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return result;
    }

    public async Task<FinanceOperationResult<FinancePeriodCloseRunRecord>> ReopenPeriodAsync(FinanceRequestContext context, FinancePeriodReopenCommand command, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.Reason)) return Failure<FinancePeriodCloseRunRecord>("reason_required");
        await using var db = CreateContext(context);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var replay = await ReadReplayAsync<FinancePeriodCloseRunRecord>(db, context, "finance.period.reopen", command.IdempotencyKey, command.RequestFingerprint, cancellationToken);
        if (replay is not null) return replay;
        var period = await db.FiscalPeriods.SingleOrDefaultAsync(item => item.Id == command.PeriodId && item.CompanyId == command.CompanyId, cancellationToken);
        var year = period is null ? null : await db.FiscalYears.SingleOrDefaultAsync(item => item.Id == period.FiscalYearId && item.CompanyId == command.CompanyId, cancellationToken);
        if (period is null || year is null || Company(context, command.CompanyId) is null) return Failure<FinancePeriodCloseRunRecord>("company_scope_denied");
        if (!period.Version.SequenceEqual(command.ExpectedVersion)) return Failure<FinancePeriodCloseRunRecord>("concurrency_conflict");
        if (period.State != FinanceFiscalPeriodState.Closed) return Failure<FinancePeriodCloseRunRecord>("period_not_closed");
        if (await db.YearEndRuns.AnyAsync(item => item.FiscalYearId == year.Id && item.Status == FinanceYearEndRunStatus.Posted, cancellationToken)) return Failure<FinancePeriodCloseRunRecord>("year_end_reversal_required");
        var run = await db.PeriodCloseRuns.Where(item => item.PeriodId == period.Id && item.Status == FinanceCloseRunStatus.Closed).OrderByDescending(item => item.Sequence).FirstOrDefaultAsync(cancellationToken);
        if (run is null) return Failure<FinancePeriodCloseRunRecord>("close_history_missing");
        var now = DateTimeOffset.UtcNow;
        run.MarkReopened(context.ActorId, now);
        period.SetState(FinanceFiscalPeriodState.Open);
        db.PeriodHistory.Add(new FinancePeriodHistoryEntity(context.TenantId, Guid.NewGuid(), command.CompanyId, year.Id, period.Id, FinancePeriodHistoryAction.Reopened, FinanceFiscalPeriodState.Closed, FinanceFiscalPeriodState.Open, run.Id, context.ActorId, context.SessionId, context.CorrelationId, command.Reason.Trim(), now));
        AddAudit(db, context, "finance.period.reopen", "period-close-run", run.Id, "Succeeded", command.Reason, command.IdempotencyKey, now);
        await db.SaveChangesAsync(cancellationToken);
        var result = FinanceOperationResult<FinancePeriodCloseRunRecord>.Success(ToCloseRun(run));
        AddReplay(db, context, "finance.period.reopen", command.IdempotencyKey, command.RequestFingerprint, "period-close-run", run.Id, result.Value!, now);
        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return result;
    }

    public async Task<IReadOnlyList<FinancePeriodCloseRunRecord>> ListPeriodCloseRunsAsync(FinanceRequestContext context, Guid companyId, Guid? periodId = null, CancellationToken cancellationToken = default)
    {
        if (Company(context, companyId) is null) return [];
        await using var db = CreateContext(context);
        var query = db.PeriodCloseRuns.AsNoTracking().Where(item => item.CompanyId == companyId);
        if (periodId is { } id) query = query.Where(item => item.PeriodId == id);
        return (await query.ToListAsync(cancellationToken)).OrderByDescending(item => item.CreatedAt).Select(ToCloseRun).ToArray();
    }

    public async Task<IReadOnlyList<FinancePeriodHistoryRecord>> ListPeriodHistoryAsync(FinanceRequestContext context, Guid companyId, Guid? periodId = null, CancellationToken cancellationToken = default)
    {
        if (Company(context, companyId) is null) return [];
        await using var db = CreateContext(context);
        var query = db.PeriodHistory.AsNoTracking().Where(item => item.CompanyId == companyId);
        if (periodId is { } id) query = query.Where(item => item.PeriodId == id);
        return (await query.ToListAsync(cancellationToken)).OrderByDescending(item => item.OccurredAt).Select(ToHistory).ToArray();
    }

    public async Task<FinanceOperationResult<FinanceYearEndRunRecord>> CalculateYearEndAsync(FinanceRequestContext context, FinanceYearEndCommand command, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.Reason)) return Failure<FinanceYearEndRunRecord>("reason_required");
        await using var db = CreateContext(context);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var replay = await ReadReplayAsync<FinanceYearEndRunRecord>(db, context, "finance.year-end.calculate", command.IdempotencyKey, command.RequestFingerprint, cancellationToken);
        if (replay is not null) return replay;
        var year = await db.FiscalYears.SingleOrDefaultAsync(item => item.Id == command.FiscalYearId && item.CompanyId == command.CompanyId, cancellationToken);
        if (year is null || Company(context, command.CompanyId) is null) return Failure<FinanceYearEndRunRecord>("company_scope_denied");
        if (command.AsOfDate != year.EndDate) return Failure<FinanceYearEndRunRecord>("year_end_date_mismatch");
        var periods = await db.FiscalPeriods.Where(item => item.FiscalYearId == year.Id).OrderBy(item => item.Sequence).ToListAsync(cancellationToken);
        if (periods.Count == 0 || periods.Any(item => item.State != FinanceFiscalPeriodState.Closed)) return Failure<FinanceYearEndRunRecord>("year_periods_not_closed");
        var rule = await FindYearEndRuleAsync(db, command.CompanyId, command.AsOfDate, cancellationToken);
        if (rule is null) return Failure<FinanceYearEndRunRecord>("year_end_posting_rule_not_configured");
        var retained = await db.Accounts.SingleOrDefaultAsync(item => item.Id == rule.CreditAccountId && item.CompanyId == command.CompanyId, cancellationToken);
        if (retained is null || retained.AccountType != FinanceAccountType.Equity || !retained.IsPostingAccount || retained.Lifecycle != FinanceAccountLifecycle.Active) return Failure<FinanceYearEndRunRecord>("retained_earnings_account_invalid");
        var active = (await db.YearEndRuns.Include(item => item.Lines).Where(item => item.CompanyId == command.CompanyId && item.FiscalYearId == year.Id && item.Status != FinanceYearEndRunStatus.Reversed).ToListAsync(cancellationToken)).OrderByDescending(item => item.CreatedAt).FirstOrDefault();
        if (active is not null) return Failure<FinanceYearEndRunRecord>("year_end_already_exists");
        var lines = await CalculateYearEndLinesAsync(db, command.CompanyId, year, retained, cancellationToken);
        var fingerprint = Fingerprint(new { YearId = year.Id, YearVersion = year.Version, RuleId = rule.Id, RuleVersion = rule.VersionNumber, RetainedEarningsAccountId = retained.Id, Lines = lines.Select(item => new { AccountId = item.Account.Id, item.Debit, item.Credit, item.NetBalance }) });
        var now = DateTimeOffset.UtcNow;
        var run = new FinanceYearEndRunEntity(context.TenantId, command.Id, command.CompanyId, year.Id, command.AsOfDate, command.Reason.Trim(), context.ActorId, context.SessionId, context.CorrelationId, now);
        run.SetSnapshot(fingerprint, rule, retained);
        foreach (var line in lines) run.Lines.Add(new FinanceYearEndLineEntity(context.TenantId, Guid.NewGuid(), run.Id, line.Account, line.Debit, line.Credit, line.NetBalance));
        db.YearEndRuns.Add(run);
        AddAudit(db, context, "finance.year-end.calculate", "year-end-run", run.Id, "Succeeded", command.Reason, command.IdempotencyKey, now);
        await db.SaveChangesAsync(cancellationToken);
        var result = FinanceOperationResult<FinanceYearEndRunRecord>.Success(ToYearEnd(run));
        AddReplay(db, context, "finance.year-end.calculate", command.IdempotencyKey, command.RequestFingerprint, "year-end-run", run.Id, result.Value!, now);
        await db.SaveChangesAsync(cancellationToken);
        await tx.CommitAsync(cancellationToken);
        return result;
    }

    public async Task<FinanceOperationResult<FinanceYearEndRunRecord>> PostYearEndAsync(FinanceRequestContext context, FinanceYearEndActionCommand command, CancellationToken cancellationToken = default)
    {
        return await ActYearEndAsync(context, command, false, cancellationToken);
    }

    public async Task<FinanceOperationResult<FinanceYearEndRunRecord>> ReverseYearEndAsync(FinanceRequestContext context, FinanceYearEndActionCommand command, CancellationToken cancellationToken = default)
    {
        return await ActYearEndAsync(context, command, true, cancellationToken);
    }

    public async Task<IReadOnlyList<FinanceYearEndRunRecord>> ListYearEndRunsAsync(FinanceRequestContext context, Guid companyId, Guid? fiscalYearId = null, CancellationToken cancellationToken = default)
    {
        if (Company(context, companyId) is null) return [];
        await using var db = CreateContext(context);
        var query = db.YearEndRuns.AsNoTracking().Include(item => item.Lines).Where(item => item.CompanyId == companyId);
        if (fiscalYearId is { } id) query = query.Where(item => item.FiscalYearId == id);
        return (await query.ToListAsync(cancellationToken)).OrderByDescending(item => item.CreatedAt).Select(ToYearEnd).ToArray();
    }

    public async Task<FinanceOperationResult<FinanceJournalRecord>> CorrectJournalAsync(FinanceRequestContext context, FinanceCorrectionCommand command, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.Reason)) return Failure<FinanceJournalRecord>("reason_required");
        await using var db = CreateContext(context);
        await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var replay = await ReadReplayAsync<FinanceJournalRecord>(db, context, "finance.journal.correct", command.IdempotencyKey, command.RequestFingerprint, cancellationToken);
        if (replay is not null) return replay;
        var original = await db.Journals.Include(item => item.Lines).SingleOrDefaultAsync(item => item.Id == command.OriginalJournalId, cancellationToken);
        if (original is null || Company(context, original.CompanyId) is null || original.CompanyId != command.CompanyId) return Failure<FinanceJournalRecord>("company_scope_denied");
        if (!original.Version.SequenceEqual(command.ExpectedVersion)) return Failure<FinanceJournalRecord>("concurrency_conflict");
        if (original.Status != FinanceJournalStatus.Posted) return Failure<FinanceJournalRecord>("journal_not_posted");
        if (original.ReversalJournalId is not null) return Failure<FinanceJournalRecord>("journal_already_corrected");
        var period = await db.FiscalPeriods.SingleOrDefaultAsync(item => item.CompanyId == original.CompanyId && item.StartDate <= command.PostingDate && item.EndDate >= command.PostingDate, cancellationToken);
        if (period is null) return Failure<FinanceJournalRecord>("period_not_configured");
        if (period.State != FinanceFiscalPeriodState.Open) return Failure<FinanceJournalRecord>(period.State == FinanceFiscalPeriodState.SoftClosed ? "period_soft_closed" : "period_closed");
        var company = Company(context, original.CompanyId)!;
        var reversalId = command.Id;
        var reversalCommand = new FinanceJournalCommand(original.CompanyId, original.JournalDate, command.PostingDate, original.TransactionCurrencyCode, original.ExchangeRate, original.ExchangeRateId, original.ExchangeRateVersionId, original.ExchangeRateVersionNumber, CorrectionContract, "correct", null, null, original.PostingRuleId, command.Reason.Trim(), original.Lines.OrderBy(item => item.LineNumber).Select(item => new FinanceJournalLineCommand(item.AccountId, item.Credit, item.Debit, item.TransactionAmount, item.TransactionCurrencyCode, item.CostCenterId, item.Description)).ToArray(), reversalId, command.IdempotencyKey, command.RequestFingerprint, original.AmountAuthority, FinanceApprovalRequirement.NotRequired);
        var reversal = new FinanceJournalEntity(context.TenantId, reversalId, reversalCommand, (await db.Journals.Where(item => item.CompanyId == original.CompanyId).Select(item => (long?)item.JournalSequence).MaxAsync(cancellationToken) ?? 0L) + 1L, company.FunctionalCurrencyCode, context.ActorId, DateTimeOffset.UtcNow);
        reversal.SetCorrelation(context.CorrelationId); reversal.LinkOriginal(original.Id); reversal.SetPeriod(period.FiscalYearId, period.Id);
        foreach (var sourceLine in original.Lines.OrderBy(item => item.LineNumber))
        {
            var account = await db.Accounts.SingleOrDefaultAsync(item => item.Id == sourceLine.AccountId && item.CompanyId == original.CompanyId, cancellationToken);
            if (account is null) return Failure<FinanceJournalRecord>("posting_lineage_missing");
            var center = sourceLine.CostCenterId is { } centerId ? await db.CostCenters.SingleOrDefaultAsync(item => item.Id == centerId && item.CompanyId == original.CompanyId, cancellationToken) : null;
            reversal.Lines.Add(new FinanceJournalLineEntity(context.TenantId, Guid.NewGuid(), reversal.Id, sourceLine.LineNumber, account, new FinanceJournalLineCommand(sourceLine.AccountId, sourceLine.Credit, sourceLine.Debit, sourceLine.TransactionAmount, sourceLine.TransactionCurrencyCode, sourceLine.CostCenterId, command.Reason.Trim()), center, sourceLine.FunctionalCredit, sourceLine.FunctionalDebit, original.AmountAuthority));
        }
        reversal.SetStatus(FinanceJournalStatus.Posted, context.ActorId, DateTimeOffset.UtcNow);
        db.Journals.Add(reversal);
        await CopyEvidenceAsync(db, context, original, reversal.Id, cancellationToken);
        original.LinkReversal(reversal.Id); original.SetStatus(FinanceJournalStatus.Reversed, context.ActorId, DateTimeOffset.UtcNow);
        var now = DateTimeOffset.UtcNow; AddAudit(db, context, "finance.journal.correct", "journal", original.Id, "Succeeded", command.Reason, command.IdempotencyKey, now);
        await db.SaveChangesAsync(cancellationToken);
        var result = FinanceOperationResult<FinanceJournalRecord>.Success(ToJournal(reversal));
        AddReplay(db, context, "finance.journal.correct", command.IdempotencyKey, command.RequestFingerprint, "journal", reversal.Id, result.Value!, now);
        await db.SaveChangesAsync(cancellationToken); await tx.CommitAsync(cancellationToken); return result;
    }

    public async Task<FinanceTrialBalanceReport> QueryTrialBalanceAsync(FinanceRequestContext context, FinanceTrialBalanceQuery query, CancellationToken cancellationToken = default)
    {
        if (Company(context, query.CompanyId) is not { } company) return EmptyTrialBalance(query);
        await using var db = CreateContext(context);
        var (from, to) = await ResolveRangeAsync(db, query.CompanyId, query.FiscalPeriodId, query.AsOfDate, cancellationToken);
        var facts = await JournalFactsAsync(db, query.CompanyId, null, to, cancellationToken);
        var accounts = await db.Accounts.AsNoTracking().Where(item => item.CompanyId == query.CompanyId).ToDictionaryAsync(item => item.Id, cancellationToken);
        var grouped = facts.Where(item => (!query.AccountId.HasValue || item.Line.AccountId == query.AccountId) && (!query.CostCenterId.HasValue || item.Line.CostCenterId == query.CostCenterId) && (query.AccountFrom == null || string.CompareOrdinal(item.Line.AccountCode, query.AccountFrom) >= 0) && (query.AccountTo == null || string.CompareOrdinal(item.Line.AccountCode, query.AccountTo) <= 0)).GroupBy(item => item.Line.AccountId);
        var rows = new List<FinanceTrialBalanceRow>();
        foreach (var group in grouped.OrderBy(item => accounts.TryGetValue(item.Key, out var account) ? account.Code : string.Empty))
        {
            if (!accounts.TryGetValue(group.Key, out var account)) continue;
            var opening = facts.Where(item => item.Line.AccountId == group.Key && item.Journal.PostingDate < from).Sum(item => item.Line.FunctionalDebit - item.Line.FunctionalCredit);
            var debit = group.Sum(item => item.Journal.PostingDate >= from ? item.Line.FunctionalDebit : 0m);
            var credit = group.Sum(item => item.Journal.PostingDate >= from ? item.Line.FunctionalCredit : 0m);
            var report = ReportingAmounts(group, query.PresentationCurrencyCode);
            rows.Add(new FinanceTrialBalanceRow(account.Id, account.Code, account.EnglishName, account.ArabicName, account.AccountType, opening, debit, credit, opening + debit - credit, company.FunctionalCurrencyCode, query.PresentationCurrencyCode, report.Opening, report.Debit, report.Credit, report.Closing, report.Status));
        }
        var reportingStatus = rows.Count == 0 ? FinanceEvidenceStatus.NotCaptured : rows.Select(item => item.ReportingEvidenceStatus).Aggregate(WorstEvidence);
        return new FinanceTrialBalanceReport(query.CompanyId, query.AsOfDate, from, to, rows, rows.Sum(item => item.PeriodDebit), rows.Sum(item => item.PeriodCredit), rows.Sum(item => item.ClosingBalance), company.FunctionalCurrencyCode, query.PresentationCurrencyCode, reportingStatus);
    }

    public async Task<IReadOnlyList<FinanceGeneralLedgerLineRecord>> QueryGeneralLedgerAsync(FinanceRequestContext context, FinanceGeneralLedgerQuery query, CancellationToken cancellationToken = default)
    {
        if (Company(context, query.CompanyId) is not { } company) return [];
        await using var db = CreateContext(context);
        var facts = await JournalFactsAsync(db, query.CompanyId, query.FromDate, query.ToDate, cancellationToken);
        var running = new Dictionary<Guid, decimal>();
        var result = new List<FinanceGeneralLedgerLineRecord>();
        foreach (var fact in facts.Where(item => (!query.AccountId.HasValue || item.Line.AccountId == query.AccountId) && (!query.FiscalPeriodId.HasValue || item.Journal.FiscalPeriodId == query.FiscalPeriodId) && (!query.CostCenterId.HasValue || item.Line.CostCenterId == query.CostCenterId) && (query.SourceContract == null || item.Journal.SourceContract == query.SourceContract)))
        {
            var balance = running.TryGetValue(fact.Line.AccountId, out var existing) ? existing : 0m;
            balance += fact.Line.FunctionalDebit - fact.Line.FunctionalCredit; running[fact.Line.AccountId] = balance;
            var evidence = fact.Evidence;
            var report = ReportLineAmount(fact, query.PresentationCurrencyCode);
            result.Add(new FinanceGeneralLedgerLineRecord(fact.Journal.Id, fact.Journal.JournalNumber, fact.Journal.JournalSequence, fact.Journal.PostingDate, fact.Line.AccountId, fact.Line.AccountCode, fact.Line.AccountName, null, fact.Journal.FiscalPeriodId, fact.Line.CostCenterId, fact.Line.CostCenterCode, fact.Journal.SourceContract, fact.Journal.SourceEvent, fact.Journal.SourceEvidenceId, fact.Line.LineNumber, fact.Line.Debit, fact.Line.Credit, fact.Line.FunctionalDebit, fact.Line.FunctionalCredit, balance, company.FunctionalCurrencyCode, fact.Line.TransactionCurrencyCode, fact.Line.TransactionAmount, report.Amount, report.Status, fact.Journal.ReversalOfJournalId is not null));
        }
        return result;
    }

    public async Task<IReadOnlyList<FinanceAgingReportRow>> QueryAgingAsync(FinanceRequestContext context, FinanceAgingReportQuery query, CancellationToken cancellationToken = default)
    {
        if (Company(context, query.CompanyId) is null) return [];
        await using var db = CreateContext(context);
        var items = await db.OpenItems.AsNoTracking().Where(item => item.CompanyId == query.CompanyId && item.Kind == query.Kind && item.DocumentDate <= query.AsOfDate && (!query.PartyId.HasValue || (query.Kind == FinanceOpenItemKind.Payable ? item.SupplierId == query.PartyId : item.CustomerId == query.PartyId)) && (query.CurrencyCode == null || item.CurrencyCode == query.CurrencyCode)).ToListAsync(cancellationToken);
        var allocations = await db.Allocations.AsNoTracking().Where(item => item.CompanyId == query.CompanyId && item.AllocationDate <= query.AsOfDate).ToListAsync(cancellationToken);
        return items.Select(item =>
        {
            var allocated = allocations.Where(value => value.OpenItemId == item.Id && value.Status == FinanceAllocationStatus.Active && !allocations.Any(reverse => reverse.ReversalOfAllocationId == value.Id && reverse.Status == FinanceAllocationStatus.Reversed)).Sum(value => value.Amount);
            var functionalAllocated = allocations.Where(value => value.OpenItemId == item.Id && value.Status == FinanceAllocationStatus.Active && !allocations.Any(reverse => reverse.ReversalOfAllocationId == value.Id && reverse.Status == FinanceAllocationStatus.Reversed)).Sum(value => value.FunctionalAmount);
            var outstanding = Math.Max(0m, item.OriginalAmount - allocated); var functionalOutstanding = Math.Max(0m, item.OriginalFunctionalAmount - functionalAllocated); var days = Math.Max(0, query.AsOfDate.DayNumber - item.DueDate.DayNumber);
            var status = outstanding == 0m ? FinanceOpenItemStatus.Settled : allocated == 0m ? FinanceOpenItemStatus.Open : FinanceOpenItemStatus.PartiallySettled;
            return new FinanceAgingReportRow(item.Id, item.Kind, item.SupplierId, item.CustomerId, item.Reference, item.DocumentDate, item.DueDate, query.AsOfDate, days, days == 0 ? "Current" : days <= 30 ? "1-30" : days <= 60 ? "31-60" : days <= 90 ? "61-90" : "90+", item.CurrencyCode, item.OriginalAmount, allocated, outstanding, item.FunctionalCurrencyCode, item.OriginalFunctionalAmount, functionalOutstanding, status);
        }).Where(item => item.OutstandingAmount != 0m).OrderBy(item => item.DueDate).ToArray();
    }

    public async Task<FinanceCloseReconciliationRecord> QueryReconciliationAsync(FinanceRequestContext context, Guid companyId, DateOnly asOfDate, Guid? periodId = null, CancellationToken cancellationToken = default)
    {
        var items = new List<FinanceReconciliationViewRecord>();
        if (Company(context, companyId) is null) return new FinanceCloseReconciliationRecord(companyId, periodId, asOfDate, FinanceReconciliationViewStatus.Blocked, items, [], []);
        foreach (var item in await settlements.GetReconciliationAsync(context, companyId, cancellationToken)) items.Add(new FinanceReconciliationViewRecord(companyId, asOfDate, item.Scope, MapStatus(item.Status), item.SubledgerAmount, item.PostedJournalAmount, item.Difference, null, $"{item.Status}", true));
        foreach (var item in await mesp134.ReconcileTaxAsync(context, companyId, cancellationToken)) items.Add(new FinanceReconciliationViewRecord(companyId, asOfDate, "Tax", MapStatus(item.Status), item.TaxAmount, item.PostedTaxAmount, item.TaxAmount - item.PostedTaxAmount, item.JournalId.ToString("D"), $"Tax effect {item.EffectId}", true));
        foreach (var item in await mesp134.ReconcileFxAsync(context, companyId, cancellationToken)) items.Add(new FinanceReconciliationViewRecord(companyId, asOfDate, "Realized FX", MapStatus(item.Status), item.RealizedDifference, item.PostedDifference, item.RealizedDifference - item.PostedDifference, item.JournalId?.ToString("D"), item.StatusReason, true));
        await using var db = CreateContext(context);
        var runs = (await db.PeriodCloseRuns.AsNoTracking().Where(item => item.CompanyId == companyId && (!periodId.HasValue || item.PeriodId == periodId)).ToListAsync(cancellationToken)).OrderByDescending(item => item.CreatedAt).ToArray();
        var yearRuns = (await db.YearEndRuns.AsNoTracking().Include(item => item.Lines).Where(item => item.CompanyId == companyId).ToListAsync(cancellationToken)).OrderByDescending(item => item.CreatedAt).ToArray();
        var status = items.Any(item => item.Status is FinanceReconciliationViewStatus.Blocked or FinanceReconciliationViewStatus.Mismatch) ? FinanceReconciliationViewStatus.Blocked : items.Any(item => item.Status == FinanceReconciliationViewStatus.Pending) ? FinanceReconciliationViewStatus.Pending : items.Count == 0 ? FinanceReconciliationViewStatus.LegacyWithoutEvidence : FinanceReconciliationViewStatus.Reconciled;
        return new FinanceCloseReconciliationRecord(companyId, periodId, asOfDate, status, items, runs.Select(ToCloseRun).ToArray(), yearRuns.Select(ToYearEnd).ToArray());
    }

    public async Task<FinanceStatementReport> QueryStatementAsync(FinanceRequestContext context, Guid companyId, FinanceStatementKind kind, DateOnly fromDate, DateOnly toDate, CancellationToken cancellationToken = default)
    {
        if (Company(context, companyId) is not { } company) return new FinanceStatementReport(kind, companyId, fromDate, toDate, [], 0m, 0m, 0m, "", "company_scope_denied");
        await using var db = CreateContext(context);
        var facts = await JournalFactsAsync(db, companyId, fromDate, toDate, cancellationToken);
        var before = await JournalFactsAsync(db, companyId, null, fromDate.AddDays(-1), cancellationToken);
        var accounts = await db.Accounts.AsNoTracking().Where(item => item.CompanyId == companyId).ToDictionaryAsync(item => item.Id, cancellationToken);
        var allowed = kind == FinanceStatementKind.ProfitAndLoss ? new[] { FinanceAccountType.Revenue, FinanceAccountType.Expense } : new[] { FinanceAccountType.Asset, FinanceAccountType.Liability, FinanceAccountType.Equity };
        var rows = facts.Where(item => accounts.TryGetValue(item.Line.AccountId, out var account) && allowed.Contains(account.AccountType)).GroupBy(item => item.Line.AccountId).Select(group =>
        {
            var account = accounts[group.Key]; var opening = before.Where(item => item.Line.AccountId == group.Key).Sum(item => item.Line.FunctionalDebit - item.Line.FunctionalCredit); var debit = group.Sum(item => item.Line.FunctionalDebit); var credit = group.Sum(item => item.Line.FunctionalCredit); return new FinanceStatementRow(account.Id, account.Code, account.EnglishName, account.ArabicName, account.AccountType, opening, debit, credit, opening + debit - credit, company.FunctionalCurrencyCode);
        }).OrderBy(item => item.AccountCode).ToArray();
        return new FinanceStatementReport(kind, companyId, fromDate, toDate, rows, rows.Sum(item => item.Debit), rows.Sum(item => item.Credit), rows.Sum(item => item.ClosingBalance), company.FunctionalCurrencyCode, null);
    }

    private async Task<FinanceOperationResult<FinanceCloseReadinessRecord>> EvaluateReadinessAsync(FinanceDbContext db, FinanceRequestContext context, Guid companyId, Guid periodId, CancellationToken cancellationToken)
    {
        var period = await db.FiscalPeriods.SingleOrDefaultAsync(item => item.Id == periodId && item.CompanyId == companyId, cancellationToken);
        var year = period is null ? null : await db.FiscalYears.SingleOrDefaultAsync(item => item.Id == period.FiscalYearId && item.CompanyId == companyId, cancellationToken);
        if (period is null || year is null) return Failure<FinanceCloseReadinessRecord>("period_not_found");
        var checks = new List<FinanceCloseCheckRecord>();
        void Check(string code, FinanceCloseCheckStatus status, string message, decimal? expected = null, decimal? actual = null) => checks.Add(new FinanceCloseCheckRecord(code, status, message, expected, actual));
        var journals = await db.Journals.AsNoTracking().Include(item => item.Lines).Where(item => item.CompanyId == companyId && (item.Status == FinanceJournalStatus.Posted || item.Status == FinanceJournalStatus.Reversed) && item.PostingDate >= period.StartDate && item.PostingDate <= period.EndDate).ToListAsync(cancellationToken);
        var debit = journals.Sum(item => item.Lines.Sum(line => line.FunctionalDebit)); var credit = journals.Sum(item => item.Lines.Sum(line => line.FunctionalCredit));
        Check("gl_balanced", debit == credit ? FinanceCloseCheckStatus.Ready : FinanceCloseCheckStatus.Blocked, debit == credit ? "Posted journal functional totals are balanced." : "Posted journal functional totals are not balanced.", debit, credit);
        Check("journal_period_assignment", journals.All(item => item.FiscalYearId == year.Id && item.FiscalPeriodId == period.Id) ? FinanceCloseCheckStatus.Ready : FinanceCloseCheckStatus.Blocked, "Every posted journal in the date range must identify this fiscal year and period.");
        Check("posting_lineage", journals.All(item => !string.IsNullOrWhiteSpace(item.SourceContract) && (item.SourceContract == ManualContract || item.SourceEvidenceId is not null || item.PostingRuleId is not null)) ? FinanceCloseCheckStatus.Ready : FinanceCloseCheckStatus.Blocked, "Every posted journal has a source contract and retained lineage.");
        var prior = await db.FiscalPeriods.AsNoTracking().Where(item => item.FiscalYearId == year.Id && item.Sequence < period.Sequence).ToListAsync(cancellationToken);
        Check("prior_periods_closed", prior.All(item => item.State == FinanceFiscalPeriodState.Closed) ? FinanceCloseCheckStatus.Ready : FinanceCloseCheckStatus.Blocked, "All prior periods must be closed before this period.");
        Check("period_state", period.State == FinanceFiscalPeriodState.Closed ? FinanceCloseCheckStatus.Warning : FinanceCloseCheckStatus.Ready, period.State == FinanceFiscalPeriodState.Closed ? "The period is already closed." : "The period is eligible for close processing.");
        var settlement = await settlements.GetReconciliationAsync(context, companyId, cancellationToken);
        var settlementBlocked = settlement.Any(item => item.Status is FinanceReconciliationStatus.AmountMismatch or FinanceReconciliationStatus.Unreconciled);
        var settlementPending = settlement.Any(item => item.Status is not FinanceReconciliationStatus.Reconciled);
        Check("subledger_reconciliation", settlementBlocked ? FinanceCloseCheckStatus.Blocked : settlementPending ? FinanceCloseCheckStatus.Warning : FinanceCloseCheckStatus.Ready, settlementBlocked ? "AP/AR subledgers contain a mismatch." : settlementPending ? "AP/AR subledger evidence is pending." : "AP/AR subledgers reconcile to posted journals.");
        var tax = await mesp134.ReconcileTaxAsync(context, companyId, cancellationToken); var fx = await mesp134.ReconcileFxAsync(context, companyId, cancellationToken); var unrealized = await mesp134.ReconcileUnrealizedFxAsync(context, companyId, cancellationToken); var reporting = await mesp134.ReconcileReportingCurrencyAsync(context, companyId, cancellationToken);
        CheckEvidence("tax_reconciliation", tax.Select(item => item.Status), checks, "Tax accounting evidence is reconciled."); CheckEvidence("realized_fx_reconciliation", fx.Select(item => item.Status), checks, "Realized FX evidence is reconciled."); CheckEvidence("unrealized_fx_reconciliation", unrealized.Select(item => item.Status), checks, "Unrealized FX evidence is reconciled."); CheckEvidence("reporting_currency_reconciliation", reporting.Select(item => item.Status), checks, "Reporting-currency evidence is reconciled.");
        var policy = await db.MonetaryPolicies.AsNoTracking().Where(item => item.CompanyId == companyId && item.EffectiveFrom <= period.EndDate && (item.EffectiveTo == null || item.EffectiveTo >= period.EndDate)).OrderByDescending(item => item.VersionNumber).FirstOrDefaultAsync(cancellationToken);
        var foreign = await db.OpenItems.AsNoTracking().AnyAsync(item => item.CompanyId == companyId && item.DocumentDate <= period.EndDate && item.CurrencyCode != item.FunctionalCurrencyCode, cancellationToken);
        var revaluation = await db.RevaluationBatches.AsNoTracking().AnyAsync(item => item.CompanyId == companyId && item.AsOfDate == period.EndDate && item.Status == FinanceRevaluationBatchStatus.Posted, cancellationToken);
        Check("revaluation_policy", policy?.RevaluationEnabled != true || !foreign || revaluation ? FinanceCloseCheckStatus.Ready : FinanceCloseCheckStatus.Blocked, policy?.RevaluationEnabled == true && foreign && !revaluation ? "A configured revaluation policy requires a posted period-end revaluation." : "Revaluation policy is satisfied or not applicable.");
        var status = checks.Any(item => item.Status == FinanceCloseCheckStatus.Blocked) ? FinanceCloseCheckStatus.Blocked : checks.Any(item => item.Status == FinanceCloseCheckStatus.Warning) ? FinanceCloseCheckStatus.Warning : FinanceCloseCheckStatus.Ready;
        var evaluatedAt = DateTimeOffset.UtcNow; var fingerprint = Fingerprint(new { PeriodId = period.Id, PeriodVersion = period.Version, FiscalYearId = year.Id, FiscalYearVersion = year.Version, Checks = checks });
        return FinanceOperationResult<FinanceCloseReadinessRecord>.Success(new FinanceCloseReadinessRecord(period.Id, year.Id, context.TenantId.Value, companyId, status, checks, fingerprint, evaluatedAt, period.Version));
    }

    private async Task<FinanceOperationResult<FinanceYearEndRunRecord>> ActYearEndAsync(FinanceRequestContext context, FinanceYearEndActionCommand command, bool reverse, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(command.Reason)) return Failure<FinanceYearEndRunRecord>("reason_required");
        var operation = reverse ? "finance.year-end.reverse" : "finance.year-end.post";
        await using var db = CreateContext(context); await using var tx = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var replay = await ReadReplayAsync<FinanceYearEndRunRecord>(db, context, operation, command.IdempotencyKey, command.RequestFingerprint, cancellationToken); if (replay is not null) return replay;
        var run = await db.YearEndRuns.Include(item => item.Lines).SingleOrDefaultAsync(item => item.Id == command.RunId, cancellationToken);
        if (run is null || Company(context, run.CompanyId) is null || !run.Version.SequenceEqual(command.ExpectedVersion)) return Failure<FinanceYearEndRunRecord>(run is null ? "year_end_not_found" : "concurrency_conflict");
        var year = await db.FiscalYears.SingleAsync(item => item.Id == run.FiscalYearId && item.CompanyId == run.CompanyId, cancellationToken); var company = Company(context, run.CompanyId)!;
        if (reverse)
        {
            if (run.Status != FinanceYearEndRunStatus.Posted || run.ClosingJournalId is null) return Failure<FinanceYearEndRunRecord>("year_end_not_posted");
            var original = await db.Journals.Include(item => item.Lines).SingleOrDefaultAsync(item => item.Id == run.ClosingJournalId, cancellationToken); if (original is null) return Failure<FinanceYearEndRunRecord>("posting_lineage_missing");
            var reversal = await CreateExactJournalReversalAsync(db, context, original, year.EndDate, command.Reason.Trim(), command.Id, YearEndContract, "reverse", cancellationToken); if (!reversal.Succeeded || reversal.Value is null) return Failure<FinanceYearEndRunRecord>(reversal.Code);
            run.MarkReversed(reversal.Value.Id, DateTimeOffset.UtcNow); year.SetState(FinanceFiscalYearState.Open); AddAudit(db, context, operation, "year-end-run", run.Id, "Succeeded", command.Reason, command.IdempotencyKey, DateTimeOffset.UtcNow); await db.SaveChangesAsync(cancellationToken); var result = FinanceOperationResult<FinanceYearEndRunRecord>.Success(ToYearEnd(run)); AddReplay(db, context, operation, command.IdempotencyKey, command.RequestFingerprint, "year-end-run", run.Id, result.Value!, DateTimeOffset.UtcNow); await db.SaveChangesAsync(cancellationToken); await tx.CommitAsync(cancellationToken); return result;
        }
        if (run.Status != FinanceYearEndRunStatus.Calculated) return Failure<FinanceYearEndRunRecord>("year_end_not_calculated");
        var periods = await db.FiscalPeriods.Where(item => item.FiscalYearId == year.Id).ToListAsync(cancellationToken); if (periods.Count == 0 || periods.Any(item => item.State != FinanceFiscalPeriodState.Closed)) return Failure<FinanceYearEndRunRecord>("year_periods_not_closed");
        var rule = await FindYearEndRuleAsync(db, run.CompanyId, run.AsOfDate, cancellationToken); var retained = rule is null ? null : await db.Accounts.SingleOrDefaultAsync(item => item.Id == rule.CreditAccountId && item.CompanyId == run.CompanyId, cancellationToken); if (rule is null || retained is null || run.RetainedEarningsAccountId != retained.Id || run.PostingRuleVersionNumber != rule.VersionNumber) return Failure<FinanceYearEndRunRecord>("year_end_configuration_changed");
        var currentLines = await CalculateYearEndLinesAsync(db, run.CompanyId, year, retained, cancellationToken); var currentFingerprint = Fingerprint(new { YearId = year.Id, YearVersion = year.Version, RuleId = rule.Id, RuleVersion = rule.VersionNumber, RetainedEarningsAccountId = retained.Id, Lines = currentLines.Select(item => new { AccountId = item.Account.Id, item.Debit, item.Credit, item.NetBalance }) }); if (!string.Equals(currentFingerprint, run.SnapshotFingerprint, StringComparison.Ordinal)) return Failure<FinanceYearEndRunRecord>("year_end_source_changed");
        var period = periods.OrderByDescending(item => item.Sequence).First(); var journal = await CreateYearEndJournalAsync(db, context, run, period, rule, company.FunctionalCurrencyCode, cancellationToken); if (!journal.Succeeded || journal.Value is null) return Failure<FinanceYearEndRunRecord>(journal.Code);
        run.MarkPosted(journal.Value.Id, DateTimeOffset.UtcNow); year.SetState(FinanceFiscalYearState.Closed); AddAudit(db, context, operation, "year-end-run", run.Id, "Succeeded", command.Reason, command.IdempotencyKey, DateTimeOffset.UtcNow); await db.SaveChangesAsync(cancellationToken); var posted = FinanceOperationResult<FinanceYearEndRunRecord>.Success(ToYearEnd(run)); AddReplay(db, context, operation, command.IdempotencyKey, command.RequestFingerprint, "year-end-run", run.Id, posted.Value!, DateTimeOffset.UtcNow); await db.SaveChangesAsync(cancellationToken); await tx.CommitAsync(cancellationToken); return posted;
    }

    private async Task<(bool Succeeded, string Code, FinanceJournalRecord? Value)> CreateYearEndJournalAsync(FinanceDbContext db, FinanceRequestContext context, FinanceYearEndRunEntity run, FinanceFiscalPeriodEntity period, FinancePostingRuleEntity rule, string functionalCurrency, CancellationToken cancellationToken)
    {
        var accounts = await db.Accounts.Where(item => item.CompanyId == run.CompanyId && run.Lines.Select(line => line.AccountId).Contains(item.Id)).ToDictionaryAsync(item => item.Id, cancellationToken); if (accounts.Count != run.Lines.Select(item => item.AccountId).Distinct().Count()) return (false, "posting_lineage_missing", null);
        var values = run.Lines.Where(item => item.Debit != 0m || item.Credit != 0m).OrderBy(item => item.AccountCode).ToArray(); var command = new FinanceJournalCommand(run.CompanyId, period.EndDate, period.EndDate, functionalCurrency, null, null, null, null, YearEndContract, "close", run.Id, 1, rule.Id, "Year-end retained earnings close", values.Select(item => new FinanceJournalLineCommand(item.AccountId, item.Debit, item.Credit, Math.Max(item.Debit, item.Credit), functionalCurrency, null, "Year-end close")).ToArray(), Guid.NewGuid(), Guid.NewGuid().ToString("N"), Guid.NewGuid().ToString("N"), FinanceJournalAmountAuthority.ManualTransactionCurrency, FinanceApprovalRequirement.NotRequired);
        var journal = new FinanceJournalEntity(context.TenantId, command.Id, command, (await db.Journals.Where(item => item.CompanyId == run.CompanyId).Select(item => (long?)item.JournalSequence).MaxAsync(cancellationToken) ?? 0L) + 1L, functionalCurrency, context.ActorId, DateTimeOffset.UtcNow); journal.SetCorrelation(context.CorrelationId); journal.SetPeriod(period.FiscalYearId, period.Id); journal.SetRule(rule.Id, rule.VersionNumber);
        var number = 1; foreach (var item in values) journal.Lines.Add(new FinanceJournalLineEntity(context.TenantId, Guid.NewGuid(), journal.Id, number++, accounts[item.AccountId], command.Lines[number - 2], null, item.Debit, item.Credit, FinanceJournalAmountAuthority.ManualTransactionCurrency)); journal.SetStatus(FinanceJournalStatus.Posted, context.ActorId, DateTimeOffset.UtcNow); db.Journals.Add(journal);
        var evidence = await FinanceJournalMonetaryEvidenceFactory.BuildAsync(db, context.TenantContext, exchangeRates, run.CompanyId, period.EndDate, functionalCurrency, values.Sum(item => item.Debit), functionalCurrency, values.Sum(item => item.Debit), null, null, null, null, cancellationToken); if (!evidence.Succeeded) return (false, evidence.Code, null); if (evidence.Evidence is not null) db.JournalMonetaryEvidence.Add(new FinanceJournalMonetaryEvidenceEntity(context.TenantId, Guid.NewGuid(), journal.Id, run.CompanyId, run.Id, evidence.Evidence, DateTimeOffset.UtcNow));
        return (true, "succeeded", ToJournal(journal));
    }

    private async Task<FinanceOperationResult<FinanceJournalRecord>> CreateExactJournalReversalAsync(FinanceDbContext db, FinanceRequestContext context, FinanceJournalEntity original, DateOnly date, string reason, Guid id, string sourceContract, string sourceEvent, CancellationToken cancellationToken)
    {
        var period = await db.FiscalPeriods.SingleOrDefaultAsync(item => item.CompanyId == original.CompanyId && item.StartDate <= date && item.EndDate >= date, cancellationToken); if (period is null || period.State != FinanceFiscalPeriodState.Closed) return Failure<FinanceJournalRecord>("period_not_closed");
        var company = Company(context, original.CompanyId)!; var command = new FinanceJournalCommand(original.CompanyId, original.JournalDate, date, original.TransactionCurrencyCode, original.ExchangeRate, original.ExchangeRateId, original.ExchangeRateVersionId, original.ExchangeRateVersionNumber, sourceContract, sourceEvent, null, null, original.PostingRuleId, reason, original.Lines.OrderBy(item => item.LineNumber).Select(item => new FinanceJournalLineCommand(item.AccountId, item.Credit, item.Debit, item.TransactionAmount, item.TransactionCurrencyCode, item.CostCenterId, reason)).ToArray(), id, Guid.NewGuid().ToString("N"), Guid.NewGuid().ToString("N"), original.AmountAuthority, FinanceApprovalRequirement.NotRequired);
        var reversal = new FinanceJournalEntity(context.TenantId, Guid.NewGuid(), command, (await db.Journals.Where(item => item.CompanyId == original.CompanyId).Select(item => (long?)item.JournalSequence).MaxAsync(cancellationToken) ?? 0L) + 1L, company.FunctionalCurrencyCode, context.ActorId, DateTimeOffset.UtcNow); reversal.SetCorrelation(context.CorrelationId); reversal.LinkOriginal(original.Id); reversal.SetPeriod(period.FiscalYearId, period.Id); foreach (var line in original.Lines.OrderBy(item => item.LineNumber)) { var account = await db.Accounts.SingleAsync(item => item.Id == line.AccountId && item.CompanyId == original.CompanyId, cancellationToken); reversal.Lines.Add(new FinanceJournalLineEntity(context.TenantId, Guid.NewGuid(), reversal.Id, line.LineNumber, account, new FinanceJournalLineCommand(line.AccountId, line.Credit, line.Debit, line.TransactionAmount, line.TransactionCurrencyCode, line.CostCenterId, reason), null, line.FunctionalCredit, line.FunctionalDebit, original.AmountAuthority)); } reversal.SetStatus(FinanceJournalStatus.Posted, context.ActorId, DateTimeOffset.UtcNow); db.Journals.Add(reversal); await CopyEvidenceAsync(db, context, original, reversal.Id, cancellationToken); original.LinkReversal(reversal.Id); original.SetStatus(FinanceJournalStatus.Reversed, context.ActorId, DateTimeOffset.UtcNow); return FinanceOperationResult<FinanceJournalRecord>.Success(ToJournal(reversal));
    }

    private async Task<FinancePostingRuleEntity?> FindYearEndRuleAsync(FinanceDbContext db, Guid companyId, DateOnly date, CancellationToken cancellationToken) => await db.PostingRules.Where(item => item.CompanyId == companyId && item.SourceContract == YearEndContract && item.SourceEvent == "close" && item.Lifecycle == FinancePostingRuleLifecycle.Enabled && item.EffectiveFrom <= date && (item.EffectiveTo == null || item.EffectiveTo >= date)).OrderByDescending(item => item.VersionNumber).SingleOrDefaultAsync(cancellationToken);

    private static async Task<List<(FinanceAccountEntity Account, decimal Debit, decimal Credit, decimal NetBalance)>> CalculateYearEndLinesAsync(FinanceDbContext db, Guid companyId, FinanceFiscalYearEntity year, FinanceAccountEntity retained, CancellationToken cancellationToken)
    {
        var journals = await db.Journals.AsNoTracking().Include(item => item.Lines).Where(item => item.CompanyId == companyId && (item.Status == FinanceJournalStatus.Posted || item.Status == FinanceJournalStatus.Reversed) && item.PostingDate >= year.StartDate && item.PostingDate <= year.EndDate && item.SourceContract != YearEndContract).ToListAsync(cancellationToken); var accounts = await db.Accounts.Where(item => item.CompanyId == companyId).ToDictionaryAsync(item => item.Id, cancellationToken); var balances = journals.SelectMany(item => item.Lines).GroupBy(item => item.AccountId).Select(group => (Account: accounts.GetValueOrDefault(group.Key), Debit: group.Sum(item => item.FunctionalCredit), Credit: group.Sum(item => item.FunctionalDebit))).Where(item => item.Account is not null && item.Account.AccountType is FinanceAccountType.Revenue or FinanceAccountType.Expense && item.Debit != item.Credit).Select(item => (item.Account!, item.Debit, item.Credit, item.Debit - item.Credit)).ToList(); var net = balances.Sum(item => item.Debit - item.Credit); if (net > 0m) balances.Add((retained, 0m, net, -net)); else if (net < 0m) balances.Add((retained, -net, 0m, -net)); return balances;
    }

    private async Task CopyEvidenceAsync(FinanceDbContext db, FinanceRequestContext context, FinanceJournalEntity original, Guid reversalId, CancellationToken cancellationToken)
    {
        var source = await db.JournalMonetaryEvidence.AsNoTracking().SingleOrDefaultAsync(item => item.JournalId == original.Id, cancellationToken); if (source is null) return; var evidence = JsonSerializer.Deserialize<FinanceMonetaryEvidence>(source.MonetaryEvidenceJson); if (evidence is null) return; db.JournalMonetaryEvidence.Add(new FinanceJournalMonetaryEvidenceEntity(context.TenantId, Guid.NewGuid(), reversalId, original.CompanyId, null, FinanceJournalMonetaryEvidenceFactory.Negate(evidence), DateTimeOffset.UtcNow));
    }

    private async Task<List<JournalFact>> JournalFactsAsync(FinanceDbContext db, Guid companyId, DateOnly? from, DateOnly? to, CancellationToken cancellationToken)
    {
        var query = db.Journals.AsNoTracking().Include(item => item.Lines).Where(item => item.CompanyId == companyId && (item.Status == FinanceJournalStatus.Posted || item.Status == FinanceJournalStatus.Reversed)); if (from is { } start) query = query.Where(item => item.PostingDate >= start); if (to is { } end) query = query.Where(item => item.PostingDate <= end); var journals = await query.OrderBy(item => item.PostingDate).ThenBy(item => item.JournalSequence).ToListAsync(cancellationToken); var evidence = await db.JournalMonetaryEvidence.AsNoTracking().Where(item => journals.Select(value => value.Id).Contains(item.JournalId)).ToDictionaryAsync(item => item.JournalId, cancellationToken); return journals.SelectMany(journal => journal.Lines.OrderBy(item => item.LineNumber).Select(line => new JournalFact(journal, line, evidence.GetValueOrDefault(journal.Id)))).ToList();
    }

    private static (decimal? Opening, decimal? Debit, decimal? Credit, decimal? Closing, FinanceEvidenceStatus Status) ReportingAmounts(IEnumerable<JournalFact> facts, string? currency)
    {
        if (currency is null) return (null, null, null, null, FinanceEvidenceStatus.NotCaptured); var values = facts.ToArray(); if (values.Length == 0) return (null, null, null, null, FinanceEvidenceStatus.NotCaptured); if (values.Any(item => item.Evidence is null || item.Evidence.ReportingCurrencyCode != currency || item.Evidence.ReportingAmount is null || item.Evidence.ReportingEvidenceStatus is not (FinanceEvidenceStatus.Captured or FinanceEvidenceStatus.Reconciled))) return (null, null, null, null, FinanceEvidenceStatus.LegacyWithoutReportingEvidence); var debit = values.Sum(ReportDebit); var credit = values.Sum(ReportCredit); return (0m, debit, credit, debit - credit, FinanceEvidenceStatus.Reconciled);
        decimal ReportDebit(JournalFact item) => item.Evidence!.ReportingAmount!.Value * (item.Line.FunctionalDebit / Math.Max(0.00000001m, item.Journal.Lines.Sum(line => line.FunctionalDebit + line.FunctionalCredit)));
        decimal ReportCredit(JournalFact item) => item.Evidence!.ReportingAmount!.Value * (item.Line.FunctionalCredit / Math.Max(0.00000001m, item.Journal.Lines.Sum(line => line.FunctionalDebit + line.FunctionalCredit)));
    }

    private static (decimal? Amount, FinanceEvidenceStatus Status) ReportLineAmount(JournalFact fact, string? currency) => currency is null ? (null, FinanceEvidenceStatus.NotCaptured) : fact.Evidence is { ReportingCurrencyCode: var code, ReportingAmount: not null } evidence && string.Equals(code, currency, StringComparison.OrdinalIgnoreCase) && evidence.ReportingEvidenceStatus is FinanceEvidenceStatus.Captured or FinanceEvidenceStatus.Reconciled ? (evidence.ReportingAmount.Value * ((fact.Line.FunctionalDebit - fact.Line.FunctionalCredit) / Math.Max(0.00000001m, fact.Journal.Lines.Sum(line => line.FunctionalDebit + line.FunctionalCredit))), FinanceEvidenceStatus.Reconciled) : (null, FinanceEvidenceStatus.LegacyWithoutReportingEvidence);

    private static async Task<(DateOnly From, DateOnly To)> ResolveRangeAsync(FinanceDbContext db, Guid companyId, Guid? periodId, DateOnly asOf, CancellationToken cancellationToken) { if (periodId is { } id) { var period = await db.FiscalPeriods.AsNoTracking().SingleOrDefaultAsync(item => item.Id == id && item.CompanyId == companyId, cancellationToken); if (period is not null) return (period.StartDate, asOf < period.EndDate ? asOf : period.EndDate); } return (DateOnly.MinValue, asOf); }
    private static FinanceEvidenceStatus WorstEvidence(FinanceEvidenceStatus left, FinanceEvidenceStatus right) => left == FinanceEvidenceStatus.PendingMapping || right == FinanceEvidenceStatus.PendingMapping ? FinanceEvidenceStatus.PendingMapping : left == FinanceEvidenceStatus.LegacyWithoutReportingEvidence || right == FinanceEvidenceStatus.LegacyWithoutReportingEvidence ? FinanceEvidenceStatus.LegacyWithoutReportingEvidence : left == FinanceEvidenceStatus.NotCaptured || right == FinanceEvidenceStatus.NotCaptured ? FinanceEvidenceStatus.NotCaptured : FinanceEvidenceStatus.Reconciled;
    private static FinanceReconciliationViewStatus MapStatus(FinanceReconciliationStatus status) => status switch { FinanceReconciliationStatus.Reconciled => FinanceReconciliationViewStatus.Reconciled, FinanceReconciliationStatus.PendingPosting or FinanceReconciliationStatus.PendingApproval or FinanceReconciliationStatus.PendingMapping or FinanceReconciliationStatus.PendingFxRecognition => FinanceReconciliationViewStatus.Pending, FinanceReconciliationStatus.AmountMismatch or FinanceReconciliationStatus.Unreconciled => FinanceReconciliationViewStatus.Mismatch, _ => FinanceReconciliationViewStatus.Blocked };
    private static FinanceReconciliationViewStatus MapStatus(FinanceEvidenceStatus status) => status switch { FinanceEvidenceStatus.Reconciled or FinanceEvidenceStatus.Captured => FinanceReconciliationViewStatus.Reconciled, FinanceEvidenceStatus.LegacyWithoutReportingEvidence or FinanceEvidenceStatus.NotCaptured => FinanceReconciliationViewStatus.LegacyWithoutEvidence, FinanceEvidenceStatus.PendingMapping or FinanceEvidenceStatus.MissingRate or FinanceEvidenceStatus.AmbiguousMapping => FinanceReconciliationViewStatus.Pending, _ => FinanceReconciliationViewStatus.Blocked };
    private static void CheckEvidence(string code, IEnumerable<FinanceEvidenceStatus> values, ICollection<FinanceCloseCheckRecord> checks, string message) { var statuses = values.ToArray(); var status = statuses.Any(item => item is FinanceEvidenceStatus.PendingMapping or FinanceEvidenceStatus.AmbiguousMapping or FinanceEvidenceStatus.MissingRate) ? FinanceCloseCheckStatus.Blocked : statuses.Any(item => item is FinanceEvidenceStatus.NotCaptured or FinanceEvidenceStatus.LegacyWithoutReportingEvidence) ? FinanceCloseCheckStatus.Warning : FinanceCloseCheckStatus.Ready; checks.Add(new FinanceCloseCheckRecord(code, status, message)); }
    private FinanceCompanyOption? Company(FinanceRequestContext context, Guid companyId) { var matches = companies.List(context.TenantId).Where(item => item.CompanyId == companyId && item.IsActive).ToArray(); if (matches.Length == 0 || matches.Select(item => item.FunctionalCurrencyCode).Distinct(StringComparer.OrdinalIgnoreCase).Count() != 1) return null; if (context.TenantContext.Scope is { } scope) { var value = scope.Value; if (value.StartsWith("Company:", StringComparison.OrdinalIgnoreCase) && (!Guid.TryParse(value["Company:".Length..], out var scoped) || scoped != companyId)) return null; if (value.StartsWith("Branch:", StringComparison.OrdinalIgnoreCase) && (!Guid.TryParse(value["Branch:".Length..], out var branch) || !matches.Any(item => item.BranchId == branch))) return null; } return matches.OrderBy(item => item.BranchId.HasValue).ThenBy(item => item.BranchId).First(); }
    private FinanceDbContext CreateContext(FinanceRequestContext context) => new(options, context.TenantContext);
    private static FinanceOperationResult<T> Failure<T>(string code) => FinanceOperationResult<T>.Failure(code);
    private static string Fingerprint(object value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value))));
    private static FinanceTrialBalanceReport EmptyTrialBalance(FinanceTrialBalanceQuery query) => new(query.CompanyId, query.AsOfDate, null, null, [], 0m, 0m, 0m, string.Empty, query.PresentationCurrencyCode, FinanceEvidenceStatus.NotCaptured);
    private static void AddAudit(FinanceDbContext db, FinanceRequestContext context, string operation, string resource, Guid id, string result, string? reason, string? key, DateTimeOffset at) => db.AuditEvents.Add(new FinanceAuditEntity(context.TenantId, Guid.NewGuid(), operation, resource, id, context.ActorId, context.SessionId, result, reason, context.CorrelationId, key, at));
    private static void AddReplay<T>(FinanceDbContext db, FinanceRequestContext context, string operation, string key, string fingerprint, string resource, Guid id, T value, DateTimeOffset at) { if (!string.IsNullOrWhiteSpace(key)) db.Idempotency.Add(new FinanceIdempotencyEntity(context.TenantId, Guid.NewGuid(), context.ActorId, operation, key, fingerprint, resource, id, JsonSerializer.Serialize(value), at)); }
    private static async Task<FinanceOperationResult<T>?> ReadReplayAsync<T>(FinanceDbContext db, FinanceRequestContext context, string operation, string key, string fingerprint, CancellationToken cancellationToken) { if (string.IsNullOrWhiteSpace(key)) return null; var item = await db.Idempotency.AsNoTracking().SingleOrDefaultAsync(value => value.ActorId == context.ActorId && value.OperationId == operation && value.Key == key, cancellationToken); if (item is null) return null; if (!string.Equals(item.Fingerprint, fingerprint, StringComparison.Ordinal)) return Failure<T>("idempotency_conflict"); var value = JsonSerializer.Deserialize<T>(item.SnapshotJson); return value is null ? Failure<T>("idempotency_snapshot_invalid") : FinanceOperationResult<T>.Success(value); }

    private static FinancePeriodCloseRunRecord ToCloseRun(FinancePeriodCloseRunEntity item) => new(item.Id, item.TenantId.Value, item.CompanyId, item.FiscalYearId, item.PeriodId, item.Sequence, item.Status, item.ReadinessStatus, item.SnapshotFingerprint, JsonSerializer.Deserialize<IReadOnlyList<FinanceCloseCheckRecord>>(item.ChecksJson) ?? [], item.Reason, item.ActorId, item.SessionId, item.CorrelationId, item.CreatedAt, item.ReopenedAt, item.ReopenedBy, item.Version);
    private static FinancePeriodHistoryRecord ToHistory(FinancePeriodHistoryEntity item) => new(item.Id, item.TenantId.Value, item.CompanyId, item.FiscalYearId, item.PeriodId, item.Action, item.FromState, item.ToState, item.CloseRunId, item.ActorId, item.SessionId, item.CorrelationId, item.Reason, item.OccurredAt);
    private static FinanceYearEndRunRecord ToYearEnd(FinanceYearEndRunEntity item) => new(item.Id, item.TenantId.Value, item.CompanyId, item.FiscalYearId, item.AsOfDate, item.Status, item.SnapshotFingerprint, item.Lines.OrderBy(line => line.AccountCode).Select(line => new FinanceYearEndLineRecord(line.Id, line.RunId, line.AccountId, line.AccountCode, line.AccountName, line.AccountNameArabic, line.AccountType, line.Debit, line.Credit, line.NetBalance, line.ClosingJournalLineId)).ToArray(), item.ClosingJournalId, item.ReversalJournalId, item.RetainedEarningsAccountId, item.RetainedEarningsAccountCode, item.PostingRuleId, item.PostingRuleVersionNumber, item.PostingRuleSourceContract, item.PostingRuleSourceEvent, item.Reason, item.ActorId, item.SessionId, item.CorrelationId, item.CreatedAt, item.PostedAt, item.ReversedAt, item.Version);
    private static FinanceJournalRecord ToJournal(FinanceJournalEntity item) => new(item.Id, item.TenantId.Value, item.CompanyId, item.JournalSequence, item.JournalNumber, item.JournalDate, item.PostingDate, item.FiscalYearId, item.FiscalPeriodId, item.FunctionalCurrencyCode, item.TransactionCurrencyCode, item.ExchangeRate, item.ExchangeRateId, item.ExchangeRateVersionId, item.ExchangeRateVersionNumber, item.SourceContract, item.SourceEvent, item.SourceEvidenceId, item.SourceEvidenceVersion, item.PostingRuleId, item.PostingRuleVersionNumber, item.Description, item.Status, item.CreatedBy, item.SubmittedBy, item.ApprovedBy, item.PostedBy, item.ReversedBy, item.ReversalOfJournalId, item.ReversalJournalId, item.CorrelationId, item.CreatedAt, item.PostedAt, item.Lines.OrderBy(line => line.LineNumber).Select(line => new FinanceJournalLineRecord(line.Id, line.LineNumber, line.AccountId, line.AccountCode, line.AccountName, line.Debit, line.Credit, line.FunctionalDebit, line.FunctionalCredit, line.TransactionAmount, line.TransactionCurrencyCode, line.CostCenterId, line.CostCenterCode, line.Description)).ToArray(), item.Version, item.AmountAuthority, item.ApprovalRequirement);
    private sealed record JournalFact(FinanceJournalEntity Journal, FinanceJournalLineEntity Line, FinanceJournalMonetaryEvidenceEntity? Evidence);
}

#pragma warning restore CS1591
