using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MiniErp.App.BuildingBlocks.Rest;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.Modules.Finance;
using MiniErp.App.Modules.Inventory;
using MiniErp.App.Modules.MasterData;
using MiniErp.Contracts.Modules.Finance;
using MiniErp.Contracts.Modules.Foundation;
using MiniErp.Infrastructure.Persistence;
using MiniErp.Infrastructure.Persistence.Modules.Finance;
using Xunit;

namespace MiniErp.ArchitectureTests;

public sealed class FinanceMesp135Tests
{
    private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid ActorId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    [Fact]
    public void MESP135_public_operations_are_scoped_and_use_mutation_controls()
    {
        var operationIds = new[]
        {
            "finance.close.readiness", "finance.close.runs", "finance.close.history", "finance.close.period", "finance.close.reopen",
            "finance.year-end.list", "finance.year-end.calculate", "finance.year-end.post", "finance.year-end.reverse",
            "finance.correction.create", "finance.reconciliation.close", "finance.report.trial-balance", "finance.report.trial-balance.export",
            "finance.report.general-ledger", "finance.report.general-ledger.export", "finance.report.ap-aging", "finance.report.ap-aging.export",
            "finance.report.ar-aging", "finance.report.ar-aging.export", "finance.report.profit-loss", "finance.report.balance-sheet"
        };

        foreach (var operationId in operationIds)
        {
            var operation = FoundationOperationCatalog.GetRequired(operationId);
            Assert.Equal(FoundationOperationVisibility.Public, operation.Visibility);
            Assert.Equal(FoundationScopePolicy.Tenant, operation.ScopePolicy);
            Assert.False(string.IsNullOrWhiteSpace(operation.ExactPermissionCode));
        }

        foreach (var operationId in new[]
                 {
                     "finance.close.period", "finance.close.reopen", "finance.year-end.post",
                     "finance.year-end.reverse", "finance.correction.create"
                 })
        {
            var operation = FoundationOperationCatalog.GetRequired(operationId);
            Assert.Equal(FoundationConcurrencyPolicy.IfMatch, operation.Concurrency);
            Assert.Equal(FoundationIdempotencyPolicy.Required, operation.Idempotency);
            Assert.True(operation.RequiresAntiforgery);
            Assert.True(operation.RequiresMandatoryAudit);
            Assert.True(operation.IsUnsafe);
        }
    }

    [Fact]
    public async Task Period_close_persists_readiness_evidence_history_and_reopen_is_versioned()
    {
        await using var fixture = await SqliteFixture.CreateAsync();
        var period = await fixture.CreateOpenPeriodAsync();

        var readiness = await fixture.Persistence.EvaluateCloseReadinessAsync(
            fixture.Context("tenant.finance.period.close"),
            new FinanceCloseReadinessQuery(fixture.CompanyId, period.Id));

        Assert.True(readiness.Succeeded, readiness.Code);
        Assert.Equal(FinanceCloseCheckStatus.Ready, readiness.Value!.Status);
        Assert.Contains(readiness.Value.Checks, check => check.Code == "gl_balanced");

        var close = await fixture.Persistence.ClosePeriodAsync(
            fixture.Context("tenant.finance.period.close"),
            new FinancePeriodCloseCommand(fixture.CompanyId, period.Id, period.Version, "MESP-135 close", Guid.NewGuid(), "close-1", "close-1"));

        Assert.True(close.Succeeded, close.Code);
        Assert.Equal(FinanceCloseRunStatus.Closed, close.Value!.Status);

        await using (var db = new FinanceDbContext(fixture.Options, fixture.TenantContext))
        {
            Assert.Equal(1, await db.PeriodCloseEvidence.CountAsync(item => item.PeriodId == period.Id));
            Assert.Equal(1, await db.PeriodCloseRuns.CountAsync(item => item.PeriodId == period.Id));
            Assert.Equal(1, await db.PeriodHistory.CountAsync(item => item.PeriodId == period.Id));
        }

        var stale = await fixture.Persistence.ReopenPeriodAsync(
            fixture.Context("tenant.finance.period.reopen"),
            new FinancePeriodReopenCommand(fixture.CompanyId, period.Id, period.Version, "stale reopen", Guid.NewGuid(), "reopen-stale", "reopen-stale"));
        Assert.False(stale.Succeeded);
        Assert.Equal("concurrency_conflict", stale.Code);

        var currentVersion = await fixture.CurrentPeriodVersionAsync(period.Id);
        var reopened = await fixture.Persistence.ReopenPeriodAsync(
            fixture.Context("tenant.finance.period.reopen"),
            new FinancePeriodReopenCommand(fixture.CompanyId, period.Id, currentVersion, "MESP-135 reopen", Guid.NewGuid(), "reopen-1", "reopen-1"));

        Assert.True(reopened.Succeeded, reopened.Code);
        Assert.Equal(FinanceCloseRunStatus.Reopened, reopened.Value!.Status);
        var history = await fixture.Persistence.ListPeriodHistoryAsync(fixture.Context("tenant.finance.period.view"), fixture.CompanyId, period.Id);
        Assert.Equal([FinancePeriodHistoryAction.Reopened, FinancePeriodHistoryAction.Closed], history.Select(item => item.Action).ToArray());
    }

    [Fact]
    public async Task Correction_is_exactly_inverse_and_reports_are_deterministic()
    {
        await using var fixture = await SqliteFixture.CreateAsync();
        var period = await fixture.CreateOpenPeriodAsync();
        var original = await fixture.CreatePostedJournalAsync(period);

        var correction = await fixture.Persistence.CorrectJournalAsync(
            fixture.Context("tenant.finance.journal.correct"),
            new FinanceCorrectionCommand(fixture.CompanyId, original.Id, new DateOnly(2026, 1, 20), original.Version, "MESP-135 correction", Guid.NewGuid(), "correction-1", "correction-1"));

        Assert.True(correction.Succeeded, correction.Code);
        Assert.Equal(original.Id, correction.Value!.ReversalOfJournalId);
        Assert.Equal(original.Lines.Count, correction.Value.Lines.Count);
        for (var index = 0; index < original.Lines.Count; index++)
        {
            Assert.Equal(original.Lines[index].Debit, correction.Value.Lines[index].Credit);
            Assert.Equal(original.Lines[index].Credit, correction.Value.Lines[index].Debit);
            Assert.Equal(original.Lines[index].FunctionalDebit, correction.Value.Lines[index].FunctionalCredit);
            Assert.Equal(original.Lines[index].FunctionalCredit, correction.Value.Lines[index].FunctionalDebit);
        }

        var trialBalance = await fixture.Persistence.QueryTrialBalanceAsync(
            fixture.Context("tenant.finance.report.trial-balance"),
            new FinanceTrialBalanceQuery(fixture.CompanyId, period.EndDate, period.Id));
        var generalLedger = await fixture.Persistence.QueryGeneralLedgerAsync(
            fixture.Context("tenant.finance.report.general-ledger"),
            new FinanceGeneralLedgerQuery(fixture.CompanyId, FiscalPeriodId: period.Id));

        Assert.Equal(trialBalance.TotalDebit, trialBalance.TotalCredit);
        Assert.Equal(0m, trialBalance.TotalClosingBalance);
        Assert.Equal(4, generalLedger.Count);
        Assert.Equal(0m, generalLedger.Sum(item => item.FunctionalDebit - item.FunctionalCredit));

        await using var db = new FinanceDbContext(fixture.Options, fixture.TenantContext);
        var storedOriginal = await db.Journals.SingleAsync(item => item.Id == original.Id);
        Assert.Equal(FinanceJournalStatus.Reversed, storedOriginal.Status);
        Assert.Equal(correction.Value.Id, storedOriginal.ReversalJournalId);
    }

    private sealed class SqliteFixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection;
        private readonly ConfiguredFinanceCompanyProvider companies;
        private readonly FinancePersistence setup;

        private SqliteFixture(
            SqliteConnection connection,
            DbContextOptions options,
            TenantContext tenantContext,
            FinanceRequestContext context,
            ConfiguredFinanceCompanyProvider companies,
            FinancePersistence setup,
            FinanceMesp135Persistence persistence,
            Guid companyId)
        {
            this.connection = connection;
            Options = options;
            TenantContext = tenantContext;
            ContextValue = context;
            this.companies = companies;
            this.setup = setup;
            Persistence = persistence;
            CompanyId = companyId;
        }

        internal DbContextOptions Options { get; }
        internal TenantContext TenantContext { get; }
        internal FinanceMesp135Persistence Persistence { get; }
        internal Guid CompanyId { get; }
        private FinanceRequestContext ContextValue { get; }

        internal FinanceRequestContext Context(string permission, Guid actorId = default)
        {
            actorId = actorId == Guid.Empty ? ActorId : actorId;
            var foundation = FoundationRequestContext.ForTenant(actorId, Guid.NewGuid(), TenantContext, permission);
            Assert.True(FinanceRequestContext.TryCreate(foundation, out var context));
            return context!;
        }

        internal static async Task<SqliteFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder().UseSqlite(connection).Options;
            var tenantContext = TenantContext.ForOrdinaryMembership(new TenantId(TenantId), new MembershipReference(Guid.NewGuid()), correlationId: new CorrelationId("mesp135-test"), actorId: ActorId);
            await using (var db = new FinanceDbContext(options, tenantContext)) await db.Database.EnsureCreatedAsync();
            var companyId = Guid.Parse("11111111-1111-1111-1111-111111111111");
            var companies = new ConfiguredFinanceCompanyProvider([new FinanceCompanyOption(TenantId, companyId, "MESP-135 Test Company", "SAR")]);
            var setup = new FinancePersistence(options, companies, new UnavailableInventoryValuationPersistence(), new UnavailableMasterDataExchangeRatePersistence());
            var persistence = new FinanceMesp135Persistence(options, companies, new UnavailableFinanceSettlementPersistence(), new UnavailableFinanceMesp134Persistence(), new UnavailableMasterDataExchangeRatePersistence());
            var foundation = FoundationRequestContext.ForTenant(ActorId, Guid.NewGuid(), tenantContext, "tenant.finance.period.close");
            Assert.True(FinanceRequestContext.TryCreate(foundation, out var context));
            return new SqliteFixture(connection, options, tenantContext, context!, companies, setup, persistence, companyId);
        }

        internal async Task<FinanceFiscalPeriodRecord> CreateOpenPeriodAsync()
        {
            var context = Context("tenant.finance.calendar.create");
            var calendar = await setup.CreateCalendarAsync(context, new FinanceFiscalCalendarCommand(CompanyId, "MESP-135 FY", Guid.NewGuid(), "m135-calendar", "m135-calendar"));
            Assert.True(calendar.Succeeded, calendar.Code);
            var year = await setup.CreateYearAsync(context, new FinanceFiscalYearCommand(calendar.Value!.Id, 2026, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), Guid.NewGuid(), "m135-year", "m135-year"));
            Assert.True(year.Succeeded, year.Code);
            var period = await setup.CreatePeriodAsync(context, new FinanceFiscalPeriodCommand(year.Value!.Id, 1, "MESP-135", "January", null, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), Guid.NewGuid(), "m135-period", "m135-period"));
            Assert.True(period.Succeeded, period.Code);
            var opened = await setup.SetPeriodStateAsync(context, new FinancePeriodStateCommand(period.Value!.Id, FinanceFiscalPeriodState.Open, null, period.Value.Version, Guid.NewGuid().ToString("N"), Guid.NewGuid().ToString("N")));
            Assert.True(opened.Succeeded, opened.Code);
            return opened.Value!;
        }

        internal async Task<FinanceJournalRecord> CreatePostedJournalAsync(FinanceFiscalPeriodRecord period)
        {
            var createContext = Context("tenant.finance.journal.create");
            var debit = await setup.CreateAccountAsync(createContext, Account("M135-ASSET", FinanceAccountType.Asset));
            var credit = await setup.CreateAccountAsync(createContext, Account("M135-REVENUE", FinanceAccountType.Revenue));
            Assert.True(debit.Succeeded && credit.Succeeded);
            var key = Guid.NewGuid().ToString("N");
            var command = new FinanceJournalCommand(CompanyId, new DateOnly(2026, 1, 15), new DateOnly(2026, 1, 15), null, 1m, null, null, null, "manual-journal.v1", "manual", null, null, null, "MESP-135 test journal", [new FinanceJournalLineCommand(debit.Value!.Id, 100m, 0m, 100m, "SAR", null, "Asset"), new FinanceJournalLineCommand(credit.Value!.Id, 0m, 100m, 100m, "SAR", null, "Revenue")], Guid.NewGuid(), key, key);
            var created = await setup.CreateJournalAsync(createContext, command);
            Assert.True(created.Succeeded, created.Code);
            var submitted = await setup.TransitionJournalAsync(createContext, new FinanceJournalActionCommand(created.Value!.Id, created.Value.Version, "submit", key + "-submit", key + "-submit"), FinanceJournalStatus.Submitted);
            Assert.True(submitted.Succeeded, submitted.Code);
            var approved = await setup.TransitionJournalAsync(Context("tenant.finance.journal.approve", Guid.Parse("66666666-6666-6666-6666-666666666666")), new FinanceJournalActionCommand(submitted.Value!.Id, submitted.Value.Version, "approve", key + "-approve", key + "-approve"), FinanceJournalStatus.Approved);
            Assert.True(approved.Succeeded, approved.Code);
            var posted = await setup.PostJournalAsync(createContext, new FinanceJournalActionCommand(approved.Value!.Id, approved.Value.Version, "post", key + "-post", key + "-post"));
            Assert.True(posted.Succeeded, posted.Code);
            return posted.Value!;
        }

        internal async Task<byte[]> CurrentPeriodVersionAsync(Guid periodId)
        {
            await using var db = new FinanceDbContext(Options, TenantContext);
            return await db.FiscalPeriods.Where(item => item.Id == periodId).Select(item => item.Version).SingleAsync();
        }

        private FinanceAccountCommand Account(string code, FinanceAccountType type) => new(CompanyId, code, code, null, null, type, true, FinanceCurrencyBehavior.TransactionCurrencyAllowed, new DateOnly(2026, 1, 1), null, Guid.NewGuid(), null, code + "-create", code + "-create");

        public async ValueTask DisposeAsync() => await connection.DisposeAsync();
    }
}

[Collection(SqlServerSafetyCollection.Name)]
public sealed class FinanceMesp135SqlServerSafetyTests
{
    private readonly SqlServerSafetyFixture fixture;

    public FinanceMesp135SqlServerSafetyTests(SqlServerSafetyFixture fixture) => this.fixture = fixture;

    [Fact]
    public async Task Close01_Concurrent_period_close_has_one_committed_winner()
    {
        var scenario = await CreateScenarioAsync();
        var first = Safe(() => scenario.First.ClosePeriodAsync(scenario.FirstContext, CloseCommand(scenario, "close01-a")));
        var second = Safe(() => scenario.Second.ClosePeriodAsync(scenario.SecondContext, CloseCommand(scenario, "close01-b")));
        var results = await Task.WhenAll(first, second);

        Assert.Single(results, item => item.Succeeded);
        Assert.Contains(results, item => !item.Succeeded);
        Assert.Contains(results, item => item.Code is "concurrency_conflict" or "period_already_closed");
    }

    [Fact]
    public async Task Close02_Concurrent_reopen_has_one_committed_winner()
    {
        var scenario = await CreateScenarioAsync();
        var closed = await scenario.First.ClosePeriodAsync(scenario.FirstContext, CloseCommand(scenario, "close02-close"));
        Assert.True(closed.Succeeded, closed.Code);
        scenario = scenario with { Period = await CurrentPeriodAsync(scenario) };

        var first = Safe(() => scenario.First.ReopenPeriodAsync(scenario.FirstContext, ReopenCommand(scenario, "reopen02-a")));
        var second = Safe(() => scenario.Second.ReopenPeriodAsync(scenario.SecondContext, ReopenCommand(scenario, "reopen02-b")));
        var results = await Task.WhenAll(first, second);

        Assert.Single(results, item => item.Succeeded);
        Assert.Contains(results, item => !item.Succeeded);
        Assert.Contains(results, item => item.Code is "concurrency_conflict" or "period_not_closed");
    }

    [Fact]
    public async Task Close03_Concurrent_close_and_post_reject_closed_period_journal()
    {
        var scenario = await CreateScenarioAsync(postJournal: true);
        var close = Safe(() => scenario.First.ClosePeriodAsync(scenario.FirstContext, CloseCommand(scenario, "close03-close")));
        var post = Safe(() => scenario.Setup.PostJournalAsync(scenario.FirstContext, new FinanceJournalActionCommand(scenario.ApprovedJournal!.Id, scenario.ApprovedJournal.Version, "post during close", "close03-post", "close03-post")));
        await Task.WhenAll(close, post);
        var closeResult = await close;
        var postResult = await post;

        Assert.True(closeResult.Succeeded, closeResult.Code);
        if (postResult.Succeeded)
        {
            Assert.True(postResult.Value!.PostedAt <= closeResult.Value!.CreatedAt);
        }
        else
        {
            Assert.Contains(postResult.Code, new[] { "period_closed", "concurrency_conflict" });
        }
        await using var db = new FinanceDbContext(scenario.Options, fixture.TenantA);
        Assert.Equal(FinanceFiscalPeriodState.Closed, await db.FiscalPeriods.Where(item => item.Id == scenario.Period.Id).Select(item => item.State).SingleAsync());
    }

    [Fact]
    public async Task Year01_Concurrent_year_end_calculation_has_one_durable_snapshot()
    {
        var scenario = await CreateScenarioAsync(postJournal: true, yearEndRule: true, closePeriod: true);
        var first = Safe(() => scenario.First.CalculateYearEndAsync(scenario.FirstContext, YearEndCommand(scenario, "year01-a")));
        var second = Safe(() => scenario.Second.CalculateYearEndAsync(scenario.SecondContext, YearEndCommand(scenario, "year01-b")));
        var results = await Task.WhenAll(first, second);

        Assert.Single(results, item => item.Succeeded);
        Assert.Contains(results, item => !item.Succeeded);
        await using var db = new FinanceDbContext(scenario.Options, fixture.TenantA);
        Assert.Equal(1, await db.YearEndRuns.CountAsync(item => item.CompanyId == scenario.CompanyId));
    }

    [Fact]
    public async Task Year02_Concurrent_year_end_post_has_one_committed_journal()
    {
        var scenario = await CreateScenarioAsync(postJournal: true, yearEndRule: true, closePeriod: true);
        var calculated = await scenario.First.CalculateYearEndAsync(scenario.FirstContext, YearEndCommand(scenario, "year02-calculate"));
        Assert.True(calculated.Succeeded, calculated.Code);
        scenario = scenario with { YearEnd = calculated.Value };

        var first = Safe(() => scenario.First.PostYearEndAsync(scenario.FirstContext, YearEndActionCommand(scenario, "year02-a")));
        var second = Safe(() => scenario.Second.PostYearEndAsync(scenario.SecondContext, YearEndActionCommand(scenario, "year02-b")));
        var results = await Task.WhenAll(first, second);

        Assert.Single(results, item => item.Succeeded);
        Assert.Contains(results, item => !item.Succeeded);
        await using var db = new FinanceDbContext(scenario.Options, fixture.TenantA);
        Assert.Equal(1, await db.Journals.CountAsync(item => item.CompanyId == scenario.CompanyId && item.SourceContract == "finance-year-end.v1"));
    }

    [Fact]
    public async Task Corr01_Concurrent_correction_has_one_committed_reversal()
    {
        var scenario = await CreateScenarioAsync(postJournal: true);
        var first = Safe(() => scenario.First.CorrectJournalAsync(scenario.FirstContext, CorrectionCommand(scenario, "corr01-a")));
        var second = Safe(() => scenario.Second.CorrectJournalAsync(scenario.SecondContext, CorrectionCommand(scenario, "corr01-b")));
        var results = await Task.WhenAll(first, second);

        Assert.Single(results, item => item.Succeeded);
        Assert.Contains(results, item => !item.Succeeded);
        await using var db = new FinanceDbContext(scenario.Options, fixture.TenantA);
        Assert.Equal(1, await db.Journals.CountAsync(item => item.CompanyId == scenario.CompanyId && item.SourceContract == "finance-correction.v1"));
    }

    [Fact]
    public async Task Corr02_correction_and_reversal_are_exact_and_linked()
    {
        var scenario = await CreateScenarioAsync(postJournal: true);
        var corrected = await scenario.First.CorrectJournalAsync(scenario.FirstContext, CorrectionCommand(scenario, "corr02"));
        Assert.True(corrected.Succeeded, corrected.Code);
        Assert.Equal(scenario.Journal!.Id, corrected.Value!.ReversalOfJournalId);

        for (var index = 0; index < scenario.Journal.Lines.Count; index++)
        {
            var original = scenario.Journal.Lines[index];
            var reversal = corrected.Value.Lines[index];
            Assert.Equal(original.Debit, reversal.Credit);
            Assert.Equal(original.Credit, reversal.Debit);
            Assert.Equal(original.FunctionalDebit, reversal.FunctionalCredit);
            Assert.Equal(original.FunctionalCredit, reversal.FunctionalDebit);
        }

        await using var db = new FinanceDbContext(scenario.Options, fixture.TenantA);
        Assert.Equal(corrected.Value.Id, await db.Journals.Where(item => item.Id == scenario.Journal.Id).Select(item => item.ReversalJournalId).SingleAsync());
    }

    private async Task<SqlScenario> CreateScenarioAsync(bool postJournal = false, bool yearEndRule = false, bool closePeriod = false)
    {
        await using var connection = await fixture.OpenConnectionAsync();
        var options = SqlServerMigrationConfiguration.Configure(connection.ConnectionString, SqlServerMigrationConfiguration.FinanceHistoryTable);
        var companyId = Guid.NewGuid();
        var provider = new ConfiguredFinanceCompanyProvider([new FinanceCompanyOption(fixture.TenantA.TenantId.Value, companyId, "MESP-135 SQL Company", "SAR")]);
        var setup = new FinancePersistence(options, provider, new UnavailableInventoryValuationPersistence(), new UnavailableMasterDataExchangeRatePersistence());
        var first = new FinanceMesp135Persistence(options, provider, new UnavailableFinanceSettlementPersistence(), new UnavailableFinanceMesp134Persistence(), new UnavailableMasterDataExchangeRatePersistence());
        var second = new FinanceMesp135Persistence(options, provider, new UnavailableFinanceSettlementPersistence(), new UnavailableFinanceMesp134Persistence(), new UnavailableMasterDataExchangeRatePersistence());
        var firstContext = Context("tenant.finance.period.close", Guid.NewGuid());
        var secondContext = Context("tenant.finance.period.close", Guid.NewGuid());
        var calendar = await setup.CreateCalendarAsync(firstContext, new FinanceFiscalCalendarCommand(companyId, "MESP-135 SQL FY", Guid.NewGuid(), Guid.NewGuid().ToString("N"), "calendar"));
        Assert.True(calendar.Succeeded, calendar.Code);
        var year = await setup.CreateYearAsync(firstContext, new FinanceFiscalYearCommand(calendar.Value!.Id, 2026, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), Guid.NewGuid(), Guid.NewGuid().ToString("N"), "year"));
        Assert.True(year.Succeeded, year.Code);
        var period = await setup.CreatePeriodAsync(firstContext, new FinanceFiscalPeriodCommand(year.Value!.Id, 1, Guid.NewGuid().ToString("N"), "January", null, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), Guid.NewGuid(), Guid.NewGuid().ToString("N"), "period"));
        Assert.True(period.Succeeded, period.Code);
        var opened = await setup.SetPeriodStateAsync(firstContext, new FinancePeriodStateCommand(period.Value!.Id, FinanceFiscalPeriodState.Open, null, period.Value.Version, Guid.NewGuid().ToString("N"), Guid.NewGuid().ToString("N")));
        Assert.True(opened.Succeeded, opened.Code);
        var accounts = await CreateAccountsAsync(setup, firstContext, companyId);
        FinanceJournalRecord? approvedJournal = null;
        FinanceJournalRecord? postedJournal = null;
        if (postJournal)
        {
            var journals = await CreatePostedJournalAsync(setup, firstContext, secondContext, companyId, accounts);
            approvedJournal = journals.Approved;
            postedJournal = journals.Posted;
        }
        if (yearEndRule)
        {
            var rule = await setup.CreatePostingRuleAsync(firstContext, new FinancePostingRuleCommand(companyId, "finance-year-end.v1", "close", accounts.Revenue.Id, accounts.Equity.Id, false, new DateOnly(2026, 1, 1), null, Guid.NewGuid(), Guid.NewGuid().ToString("N"), "year-end"));
            Assert.True(rule.Succeeded, rule.Code);
        }
        var currentPeriod = opened.Value!;
        if (closePeriod)
        {
            var closed = await setup.SetPeriodStateAsync(firstContext, new FinancePeriodStateCommand(currentPeriod.Id, FinanceFiscalPeriodState.Closed, "seed close", currentPeriod.Version, Guid.NewGuid().ToString("N"), Guid.NewGuid().ToString("N")));
            Assert.True(closed.Succeeded, closed.Code);
            currentPeriod = closed.Value!;
        }
        return new SqlScenario(options, companyId, provider, setup, first, second, firstContext, secondContext, year.Value!, currentPeriod, accounts, approvedJournal, postedJournal, null);
    }

    private async Task<(FinanceAccountRecord Debit, FinanceAccountRecord Revenue, FinanceAccountRecord Equity)> CreateAccountsAsync(FinancePersistence setup, FinanceRequestContext context, Guid companyId)
    {
        var debit = await setup.CreateAccountAsync(context, Account(companyId, "M135-SQL-ASSET", FinanceAccountType.Asset));
        var revenue = await setup.CreateAccountAsync(context, Account(companyId, "M135-SQL-REVENUE", FinanceAccountType.Revenue));
        var equity = await setup.CreateAccountAsync(context, Account(companyId, "M135-SQL-EQUITY", FinanceAccountType.Equity));
        Assert.True(debit.Succeeded, debit.Code);
        Assert.True(revenue.Succeeded, revenue.Code);
        Assert.True(equity.Succeeded, equity.Code);
        return (debit.Value!, revenue.Value!, equity.Value!);
    }

    private static async Task<(FinanceJournalRecord Approved, FinanceJournalRecord Posted)> CreatePostedJournalAsync(FinancePersistence setup, FinanceRequestContext creator, FinanceRequestContext approver, Guid companyId, (FinanceAccountRecord Debit, FinanceAccountRecord Revenue, FinanceAccountRecord Equity) accounts)
    {
        var key = Guid.NewGuid().ToString("N");
        var command = new FinanceJournalCommand(companyId, new DateOnly(2026, 1, 15), new DateOnly(2026, 1, 15), null, 1m, null, null, null, "manual-journal.v1", "manual", null, null, null, "MESP-135 SQL journal", [new FinanceJournalLineCommand(accounts.Debit.Id, 100m, 0m, 100m, "SAR", null, "Asset"), new FinanceJournalLineCommand(accounts.Revenue.Id, 0m, 100m, 100m, "SAR", null, "Revenue")], Guid.NewGuid(), key, key);
        var created = await setup.CreateJournalAsync(creator, command);
        Assert.True(created.Succeeded, created.Code);
        var submitted = await setup.TransitionJournalAsync(creator, new FinanceJournalActionCommand(created.Value!.Id, created.Value.Version, "submit", key + "-submit", key + "-submit"), FinanceJournalStatus.Submitted);
        Assert.True(submitted.Succeeded, submitted.Code);
        var approved = await setup.TransitionJournalAsync(approver, new FinanceJournalActionCommand(submitted.Value!.Id, submitted.Value.Version, "approve", key + "-approve", key + "-approve"), FinanceJournalStatus.Approved);
        Assert.True(approved.Succeeded, approved.Code);
        var posted = await setup.PostJournalAsync(creator, new FinanceJournalActionCommand(approved.Value!.Id, approved.Value.Version, "post", key + "-post", key + "-post"));
        Assert.True(posted.Succeeded, posted.Code);
        return (approved.Value!, posted.Value!);
    }

    private FinanceRequestContext Context(string permission, Guid actorId)
    {
        var foundation = FoundationRequestContext.ForTenant(actorId, Guid.NewGuid(), fixture.TenantA, permission);
        Assert.True(FinanceRequestContext.TryCreate(foundation, out var context));
        return context!;
    }

    private static FinanceAccountCommand Account(Guid companyId, string code, FinanceAccountType type) => new(companyId, code, code, null, null, type, true, FinanceCurrencyBehavior.TransactionCurrencyAllowed, new DateOnly(2026, 1, 1), null, Guid.NewGuid(), null, code + "-create", code + "-create");

    private static FinancePeriodCloseCommand CloseCommand(SqlScenario scenario, string key) => new(scenario.CompanyId, scenario.Period.Id, scenario.Period.Version, "MESP-135 SQL close", Guid.NewGuid(), key, key);
    private static FinancePeriodReopenCommand ReopenCommand(SqlScenario scenario, string key) => new(scenario.CompanyId, scenario.Period.Id, scenario.Period.Version, "MESP-135 SQL reopen", Guid.NewGuid(), key, key);
    private static FinanceYearEndCommand YearEndCommand(SqlScenario scenario, string key) => new(scenario.CompanyId, scenario.Year.Id, scenario.Year.EndDate, "MESP-135 SQL year end", Guid.NewGuid(), key, key);
    private static FinanceYearEndActionCommand YearEndActionCommand(SqlScenario scenario, string key) => new(scenario.CompanyId, scenario.YearEnd!.Id, scenario.YearEnd.Version, "MESP-135 SQL year end post", Guid.NewGuid(), key, key);
    private static FinanceCorrectionCommand CorrectionCommand(SqlScenario scenario, string key) => new(scenario.CompanyId, scenario.Journal!.Id, new DateOnly(2026, 1, 20), scenario.Journal.Version, "MESP-135 SQL correction", Guid.NewGuid(), key, key);

    private async Task<FinanceFiscalPeriodRecord> CurrentPeriodAsync(SqlScenario scenario)
    {
        await using var db = new FinanceDbContext(scenario.Options, fixture.TenantA);
        return await db.FiscalPeriods.Where(item => item.Id == scenario.Period.Id).Select(item => new FinanceFiscalPeriodRecord(item.Id, item.FiscalYearId, item.TenantId.Value, item.CompanyId, item.Sequence, item.Code, item.EnglishName, item.ArabicName, item.StartDate, item.EndDate, item.State, item.Version)).SingleAsync();
    }

    private static async Task<FinanceOperationResult<T>> Safe<T>(Func<Task<FinanceOperationResult<T>>> operation)
    {
        try
        {
            return await operation();
        }
        catch (DbUpdateConcurrencyException)
        {
            return FinanceOperationResult<T>.Failure("concurrency_conflict");
        }
        catch (DbUpdateException exception) when (exception.InnerException is SqlException sql && IsExpectedContention(sql.Number))
        {
            return FinanceOperationResult<T>.Failure("concurrency_conflict");
        }
        catch (SqlException exception) when (IsExpectedContention(exception.Number))
        {
            return FinanceOperationResult<T>.Failure("concurrency_conflict");
        }
        catch (InvalidOperationException exception) when (ContainsExpectedContention(exception))
        {
            return FinanceOperationResult<T>.Failure("concurrency_conflict");
        }
    }

    private static bool IsExpectedContention(int number) => number is 1205 or 1222 or 2601 or 2627 or 3960;

    private static bool ContainsExpectedContention(Exception exception)
    {
        for (var current = exception; current is not null; current = current.InnerException)
        {
            if (current is SqlException sql && IsExpectedContention(sql.Number)) return true;
        }
        return false;
    }

    private sealed record SqlScenario(
        DbContextOptions Options,
        Guid CompanyId,
        ConfiguredFinanceCompanyProvider Provider,
        FinancePersistence Setup,
        FinanceMesp135Persistence First,
        FinanceMesp135Persistence Second,
        FinanceRequestContext FirstContext,
        FinanceRequestContext SecondContext,
        FinanceFiscalYearRecord Year,
        FinanceFiscalPeriodRecord Period,
        (FinanceAccountRecord Debit, FinanceAccountRecord Revenue, FinanceAccountRecord Equity) Accounts,
        FinanceJournalRecord? ApprovedJournal,
        FinanceJournalRecord? Journal,
        FinanceYearEndRunRecord? YearEnd);
}
