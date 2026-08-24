using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MiniErp.App.BuildingBlocks.Rest;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.Modules.Finance;
using MiniErp.App.Modules.Inventory;
using MiniErp.App.Modules.MasterData;
using MiniErp.Contracts.Modules.Audit;
using MiniErp.Contracts.Modules.Finance;
using MiniErp.Contracts.Modules.MasterData;
using MiniErp.Infrastructure.Persistence.Modules.Finance;
using Xunit;

namespace MiniErp.ArchitectureTests;

public sealed class FinanceFoundationTests
{
    private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid CompanyId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid OtherCompanyId = Guid.Parse("22222222-2222-2222-2222-222222222222");
    private static readonly Guid ActorId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid ApproverId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private static readonly Guid SessionId = Guid.Parse("55555555-5555-5555-5555-555555555555");

    [Fact]
    public async Task Company_books_support_period_control_balanced_posting_reversal_and_gl_facts()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder().UseSqlite(connection).Options;
        await EnsureCreatedAsync(options);
        var persistence = CreatePersistence(options);
        var context = Context("tenant.finance.journal.create");
        var debitAccount = await CreateAccountAsync(persistence, context, "1000", FinanceAccountType.Asset, true);
        var creditAccount = await CreateAccountAsync(persistence, context, "4000", FinanceAccountType.Revenue, true);
        var calendar = await persistence.CreateCalendarAsync(context, new FinanceFiscalCalendarCommand(CompanyId, "Configured FY", Guid.NewGuid(), "calendar-1", "calendar-1"));
        Assert.True(calendar.Succeeded, calendar.Code);
        var year = await persistence.CreateYearAsync(context, new FinanceFiscalYearCommand(calendar.Value!.Id, 2026, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), Guid.NewGuid(), "year-1", "year-1"));
        Assert.True(year.Succeeded, year.Code);
        var period = await persistence.CreatePeriodAsync(context, new FinanceFiscalPeriodCommand(year.Value!.Id, 1, "2026-01", "January", "يناير", new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), Guid.NewGuid(), "period-1", "period-1"));
        Assert.True(period.Succeeded, period.Code);
        var opened = await persistence.SetPeriodStateAsync(context, new FinancePeriodStateCommand(period.Value!.Id, FinanceFiscalPeriodState.Open, null, period.Value.Version, "period-open", "period-open"));
        Assert.True(opened.Succeeded, opened.Code);

        var journalCommand = Journal(debitAccount.Value!.Id, creditAccount.Value!.Id, "journal-1", debit: 125m);
        var created = await persistence.CreateJournalAsync(context, journalCommand);
        Assert.True(created.Succeeded, created.Code);
        var replay = await persistence.CreateJournalAsync(context, journalCommand);
        Assert.True(replay.Succeeded, replay.Code);
        Assert.Equal(created.Value!.Id, replay.Value!.Id);

        var submitted = await persistence.TransitionJournalAsync(context, Action(created.Value, "submit-1"), FinanceJournalStatus.Submitted);
        Assert.True(submitted.Succeeded, submitted.Code);
        var approved = await persistence.TransitionJournalAsync(Context("tenant.finance.journal.approve", ApproverId), Action(submitted.Value!, "approve-1"), FinanceJournalStatus.Approved);
        Assert.True(approved.Succeeded, approved.Code);
        var posted = await persistence.PostJournalAsync(context, Action(approved.Value!, "post-1"));
        Assert.True(posted.Succeeded, posted.Code);
        Assert.Equal(FinanceJournalStatus.Posted, posted.Value!.Status);
        Assert.Equal(period.Value.Id, posted.Value.FiscalPeriodId);
        Assert.Equal(125m, posted.Value.Lines.Sum(item => item.FunctionalDebit));
        Assert.Equal(125m, posted.Value.Lines.Sum(item => item.FunctionalCredit));

        var reversal = await persistence.ReverseJournalAsync(context, new FinanceReversalCommand(posted.Value.Id, new DateOnly(2026, 1, 20), "Controlled correction", Guid.NewGuid(), "reverse-1", "reverse-1"));
        Assert.True(reversal.Succeeded, reversal.Code);
        Assert.Equal(FinanceJournalStatus.Posted, reversal.Value!.Status);
        Assert.Equal(posted.Value.Id, reversal.Value.ReversalOfJournalId);
        Assert.All(reversal.Value.Lines, line => Assert.True(line.TransactionAmount is null || line.TransactionAmount > 0m));
        Assert.Equal(posted.Value.Lines.Sum(item => item.Debit), reversal.Value.Lines.Sum(item => item.Credit));
        Assert.Equal(FinanceJournalStatus.Reversed, (await persistence.ListJournalsAsync(context, CompanyId)).Single(item => item.Id == posted.Value.Id).Status);

        var gl = await persistence.QueryGlAsync(context, new FinanceGlQuery(CompanyId));
        Assert.Equal(4, gl.Count);
        Assert.Equal(0m, gl.Sum(item => item.FunctionalDebit - item.FunctionalCredit));
    }

    [Fact]
    public async Task Unbalanced_and_closed_period_journals_fail_only_at_posting()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder().UseSqlite(connection).Options;
        await EnsureCreatedAsync(options);
        var persistence = CreatePersistence(options);
        var context = Context("tenant.finance.journal.create");
        var debitAccount = await CreateAccountAsync(persistence, context, "1100", FinanceAccountType.Asset, true);
        var creditAccount = await CreateAccountAsync(persistence, context, "4100", FinanceAccountType.Revenue, true);
        var period = await OpenPeriodAsync(persistence, context);

        var unbalanced = await persistence.CreateJournalAsync(context, Journal(debitAccount.Value!.Id, creditAccount.Value!.Id, "unbalanced", 100m, 90m));
        Assert.True(unbalanced.Succeeded, unbalanced.Code);
        var submitted = await persistence.TransitionJournalAsync(context, Action(unbalanced.Value!, "unbalanced-submit"), FinanceJournalStatus.Submitted);
        var approved = await persistence.TransitionJournalAsync(Context("tenant.finance.journal.approve", ApproverId), Action(submitted.Value!, "unbalanced-approve"), FinanceJournalStatus.Approved);
        var blocked = await persistence.PostJournalAsync(context, Action(approved.Value!, "unbalanced-post"));
        Assert.False(blocked.Succeeded);
        Assert.Equal("journal_not_balanced", blocked.Code);

        var closed = await persistence.SetPeriodStateAsync(context, new FinancePeriodStateCommand(period.Id, FinanceFiscalPeriodState.Closed, "Close for control test", period.Version, "period-close", "period-close"));
        Assert.True(closed.Succeeded, closed.Code);
        var closedJournal = await persistence.CreateJournalAsync(context, Journal(debitAccount.Value.Id, creditAccount.Value.Id, "closed", 50m));
        var closedSubmitted = await persistence.TransitionJournalAsync(context, Action(closedJournal.Value!, "closed-submit"), FinanceJournalStatus.Submitted);
        var closedApproved = await persistence.TransitionJournalAsync(Context("tenant.finance.journal.approve", ApproverId), Action(closedSubmitted.Value!, "closed-approve"), FinanceJournalStatus.Approved);
        var closedPost = await persistence.PostJournalAsync(context, Action(closedApproved.Value!, "closed-post"));
        Assert.False(closedPost.Succeeded);
        Assert.Equal("period_closed", closedPost.Code);
    }

    [Fact]
    public async Task Foreign_currency_requires_exact_active_master_data_rate_evidence()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder().UseSqlite(connection).Options;
        await EnsureCreatedAsync(options);
        var rates = new TestExchangeRatePersistence();
        var persistence = new FinancePersistence(options, Companies(), new UnavailableInventoryValuationPersistence(), rates);
        var context = Context("tenant.finance.journal.create");
        var debitAccount = await CreateAccountAsync(persistence, context, "1200", FinanceAccountType.Asset, true);
        var creditAccount = await CreateAccountAsync(persistence, context, "4200", FinanceAccountType.Revenue, true);
        var rateId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        rates.Record = new MasterDataExchangeRateRecord(
            rateId,
            new TenantId(TenantId),
            Guid.NewGuid(),
            Guid.NewGuid(),
            "USD",
            "SAR",
            MasterDataLifecycleState.Active,
            1,
            [new MasterDataExchangeRateVersionRecord(versionId, 1, new DateOnly(2026, 1, 1), null, 3.75m, 4, ExchangeRateProvenance.Configured, "test", "USD", "SAR")],
            [1]);

        var mismatch = await persistence.CreateJournalAsync(context, new FinanceJournalCommand(CompanyId, new DateOnly(2026, 1, 15), new DateOnly(2026, 1, 15), "USD", 4m, rateId, versionId, 1, "manual-journal.v1", "manual", null, null, null, "FX mismatch", [new FinanceJournalLineCommand(debitAccount.Value!.Id, 10m, 0m, 10m, "USD", null, null), new FinanceJournalLineCommand(creditAccount.Value!.Id, 0m, 10m, 10m, "USD", null, null)], Guid.NewGuid(), "fx-1", "fx-1"));
        Assert.False(mismatch.Succeeded);
        Assert.Equal("exchange_rate_evidence_mismatch", mismatch.Code);

        var exact = await persistence.CreateJournalAsync(context, new FinanceJournalCommand(CompanyId, new DateOnly(2026, 1, 15), new DateOnly(2026, 1, 15), "USD", 3.75m, rateId, versionId, 1, "manual-journal.v1", "manual", null, null, null, "FX exact", [new FinanceJournalLineCommand(debitAccount.Value.Id, 10m, 0m, 10m, "USD", null, null), new FinanceJournalLineCommand(creditAccount.Value.Id, 0m, 10m, 10m, "USD", null, null)], Guid.NewGuid(), "fx-2", "fx-2"));
        Assert.True(exact.Succeeded, exact.Code);
    }

    [Fact]
    public async Task Posting_rules_and_cost_centers_fail_closed_when_dimension_evidence_is_missing_or_invalid()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder().UseSqlite(connection).Options;
        await EnsureCreatedAsync(options);
        var persistence = CreatePersistence(options);
        var context = Context("tenant.finance.journal.create");
        var debitAccount = await CreateAccountAsync(persistence, context, "1300", FinanceAccountType.Asset, true);
        var creditAccount = await CreateAccountAsync(persistence, context, "4300", FinanceAccountType.Revenue, true);
        var period = await OpenPeriodAsync(persistence, context);
        var center = await persistence.CreateCostCenterAsync(context, new FinanceCostCenterCommand(CompanyId, "OPS", "Operations", null, new DateOnly(2026, 1, 1), null, Guid.NewGuid(), "center", "center"));
        Assert.True(center.Succeeded, center.Code);
        var rule = await persistence.CreatePostingRuleAsync(context, new FinancePostingRuleCommand(CompanyId, "inventory-valuation-finance.v1", "Inbound", debitAccount.Value!.Id, creditAccount.Value!.Id, true, new DateOnly(2026, 1, 1), null, Guid.NewGuid(), "rule", "rule"));
        Assert.True(rule.Succeeded, rule.Code);

        var invalidDimension = await persistence.CreateJournalAsync(context, new FinanceJournalCommand(CompanyId, new DateOnly(2026, 1, 15), new DateOnly(2026, 1, 15), null, 1m, null, null, null, "inventory-valuation-finance.v1", "Inbound", null, null, rule.Value!.Id, "Invalid dimension", [new FinanceJournalLineCommand(debitAccount.Value.Id, 25m, 0m, 25m, "SAR", Guid.NewGuid(), null), new FinanceJournalLineCommand(creditAccount.Value.Id, 0m, 25m, 25m, "SAR", null, null)], Guid.NewGuid(), "invalid-center", "invalid-center"));
        Assert.False(invalidDimension.Succeeded);
        Assert.Equal("dimension_invalid", invalidDimension.Code);

        var missingDimension = await persistence.CreateJournalAsync(context, new FinanceJournalCommand(CompanyId, new DateOnly(2026, 1, 15), new DateOnly(2026, 1, 15), null, 1m, null, null, null, "inventory-valuation-finance.v1", "Inbound", null, null, rule.Value.Id, "Missing dimension", [new FinanceJournalLineCommand(debitAccount.Value.Id, 25m, 0m, 25m, "SAR", null, null), new FinanceJournalLineCommand(creditAccount.Value.Id, 0m, 25m, 25m, "SAR", null, null)], Guid.NewGuid(), "missing-center", "missing-center"));
        Assert.True(missingDimension.Succeeded, missingDimension.Code);
        var submitted = await persistence.TransitionJournalAsync(context, Action(missingDimension.Value!, "missing-center-submit"), FinanceJournalStatus.Submitted);
        var approved = await persistence.TransitionJournalAsync(Context("tenant.finance.journal.approve", ApproverId), Action(submitted.Value!, "missing-center-approve"), FinanceJournalStatus.Approved);
        var blocked = await persistence.PostJournalAsync(context, Action(approved.Value!, "missing-center-post"));
        Assert.False(blocked.Succeeded);
        Assert.Equal("dimension_required", blocked.Code);

        var disabled = await persistence.SetPostingRuleLifecycleAsync(context, rule.Value.Id, CompanyId, FinancePostingRuleLifecycle.Disabled, rule.Value.Version, "rule-disable", "rule-disable");
        Assert.True(disabled.Succeeded, disabled.Code);
        Assert.Equal(FinancePostingRuleLifecycle.Disabled, disabled.Value!.Lifecycle);
        Assert.NotEqual(period.Id, Guid.Empty);
    }

    [Fact]
    public void Finance_authorization_requires_exact_operation_permission_and_company_scope()
    {
        var authorization = new FinanceAuthorizationService(Companies());
        var context = Context("tenant.finance.journal.post");
        Assert.True(authorization.Authorize(context, "finance.journal.post", CompanyId).Allowed);
        Assert.Equal("permission_denied", authorization.Authorize(context, "finance.journal.create", CompanyId).Code);
        Assert.Equal("company_scope_denied", authorization.Authorize(context, "finance.journal.post", Guid.NewGuid()).Code);
    }

    [Fact]
    public void Company_provider_preserves_multiple_branches_without_single_row_assumptions()
    {
        var branchOne = Guid.Parse("aaaaaaaa-1111-1111-1111-111111111111");
        var branchTwo = Guid.Parse("bbbbbbbb-2222-2222-2222-222222222222");
        var provider = new ConfiguredFinanceCompanyProvider([
            new FinanceCompanyOption(TenantId, CompanyId, "Test Company", "SAR", branchOne),
            new FinanceCompanyOption(TenantId, CompanyId, "Test Company", "SAR", branchTwo)]);

        var options = provider.List(new TenantId(TenantId));

        Assert.Equal(2, options.Count);
        Assert.Contains(options, item => item.BranchId == branchOne);
        Assert.Contains(options, item => item.BranchId == branchTwo);
        Assert.True(new FinanceAuthorizationService(provider).Authorize(Context("tenant.finance.journal.post"), "finance.journal.post", CompanyId).Allowed);
    }

    [Fact]
    public async Task Journal_approval_blocks_creator_and_submitter_but_allows_a_separate_authorized_actor()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder().UseSqlite(connection).Options;
        await EnsureCreatedAsync(options);
        var persistence = CreatePersistence(options);
        var creator = Context("tenant.finance.journal.create", ActorId);
        var debit = await CreateAccountAsync(persistence, creator, "1400", FinanceAccountType.Asset, true);
        var credit = await CreateAccountAsync(persistence, creator, "4400", FinanceAccountType.Revenue, true);
        await OpenPeriodAsync(persistence, creator);
        var created = await persistence.CreateJournalAsync(creator, Journal(debit.Value!.Id, credit.Value!.Id, "sod-journal", 10m));
        var submitted = await persistence.TransitionJournalAsync(creator, Action(created.Value!, "sod-submit"), FinanceJournalStatus.Submitted);

        var creatorApproval = await persistence.TransitionJournalAsync(Context("tenant.finance.journal.approve", ActorId), Action(submitted.Value!, "sod-creator-approve"), FinanceJournalStatus.Approved);
        Assert.False(creatorApproval.Succeeded);
        Assert.Equal("self_approval_forbidden", creatorApproval.Code);

        var separateApproval = await persistence.TransitionJournalAsync(Context("tenant.finance.journal.approve", ApproverId), Action(submitted.Value!, "sod-separate-approve"), FinanceJournalStatus.Approved);
        Assert.True(separateApproval.Succeeded, separateApproval.Code);
        Assert.Equal(ApproverId, separateApproval.Value!.ApprovedBy);
    }

    [Fact]
    public async Task Company_scope_is_resolved_from_resource_and_foreign_years_are_not_read()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder().UseSqlite(connection).Options;
        await EnsureCreatedAsync(options);
        var persistence = CreatePersistence(options, CompaniesWithOtherCompany());
        var unscoped = Context("tenant.finance.calendar.create");
        var calendar = await persistence.CreateCalendarAsync(unscoped, new FinanceFiscalCalendarCommand(OtherCompanyId, "Other Company FY", Guid.NewGuid(), "other-calendar", "other-calendar"));
        Assert.True(calendar.Succeeded, calendar.Code);
        var year = await persistence.CreateYearAsync(unscoped, new FinanceFiscalYearCommand(calendar.Value!.Id, 2026, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), Guid.NewGuid(), "other-year", "other-year"));
        Assert.True(year.Succeeded, year.Code);

        var companyActor = Context("tenant.finance.calendar.view", ActorId, CompanyId);
        var foreignYears = await persistence.ListYearsAsync(companyActor, calendar.Value.Id);
        Assert.Empty(foreignYears);
        Assert.Equal("company_scope_denied", new FinanceAuthorizationService(CompaniesWithOtherCompany()).Authorize(companyActor, "finance.year.list", OtherCompanyId).Code);
    }

    [Fact]
    public async Task Manual_amounts_are_derived_and_foreign_functional_only_accounts_are_revalidated_at_post()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder().UseSqlite(connection).Options;
        await EnsureCreatedAsync(options);
        var rates = new TestExchangeRatePersistence();
        var rateId = Guid.NewGuid();
        var versionId = Guid.NewGuid();
        rates.Record = new MasterDataExchangeRateRecord(rateId, new TenantId(TenantId), Guid.NewGuid(), Guid.NewGuid(), "USD", "SAR", MasterDataLifecycleState.Active, 1, [new MasterDataExchangeRateVersionRecord(versionId, 1, new DateOnly(2026, 1, 1), null, 3.75m, 4, ExchangeRateProvenance.Configured, "test", "USD", "SAR")], [1]);
        var persistence = new FinancePersistence(options, Companies(), new UnavailableInventoryValuationPersistence(), rates);
        var context = Context("tenant.finance.journal.create");
        var debit = await CreateAccountAsync(persistence, context, "1500", FinanceAccountType.Asset, true);
        var credit = await CreateAccountAsync(persistence, context, "4500", FinanceAccountType.Revenue, true);
        await OpenPeriodAsync(persistence, context);
        var mismatched = await persistence.CreateJournalAsync(context, new FinanceJournalCommand(CompanyId, new DateOnly(2026, 1, 15), new DateOnly(2026, 1, 15), "USD", 3.75m, rateId, versionId, 1, "manual-journal.v1", "manual", null, null, null, "bad amount", [new FinanceJournalLineCommand(debit.Value!.Id, 10m, 0m, 11m, "USD", null, null), new FinanceJournalLineCommand(credit.Value!.Id, 0m, 10m, 10m, "USD", null, null)], Guid.NewGuid(), "amount-mismatch", "amount-mismatch"));
        Assert.False(mismatched.Succeeded);
        Assert.Equal("transaction_amount_mismatch", mismatched.Code);

        var account = await CreateAccountAsync(persistence, context, "1600", FinanceAccountType.Asset, true);
        var journal = await persistence.CreateJournalAsync(context, new FinanceJournalCommand(CompanyId, new DateOnly(2026, 1, 15), new DateOnly(2026, 1, 15), "USD", 3.75m, rateId, versionId, 1, "manual-journal.v1", "manual", null, null, null, "revalidate currency", [new FinanceJournalLineCommand(account.Value!.Id, 10m, 0m, null, "USD", null, null), new FinanceJournalLineCommand(credit.Value.Id, 0m, 10m, null, "USD", null, null)], Guid.NewGuid(), "currency-revalidate", "currency-revalidate"));
        Assert.True(journal.Succeeded, journal.Code);
        Assert.Equal(10m, journal.Value!.Lines[0].TransactionAmount);
        Assert.Equal(37.50m, journal.Value.Lines[0].FunctionalDebit);
        var edited = await persistence.EditAccountAsync(context, new FinanceAccountCommand(CompanyId, "1600", "1600", null, null, FinanceAccountType.Asset, true, FinanceCurrencyBehavior.FunctionalOnly, new DateOnly(2026, 1, 1), null, account.Value.Id, account.Value.Version, "currency-change", "currency-change"));
        Assert.True(edited.Succeeded, edited.Code);
        var submitted = await persistence.TransitionJournalAsync(context, Action(journal.Value, "currency-submit"), FinanceJournalStatus.Submitted);
        var approved = await persistence.TransitionJournalAsync(Context("tenant.finance.journal.approve", ApproverId), Action(submitted.Value!, "currency-approve"), FinanceJournalStatus.Approved);
        var blocked = await persistence.PostJournalAsync(context, Action(approved.Value!, "currency-post"));
        Assert.False(blocked.Succeeded);
        Assert.Equal("account_currency_behavior_invalid", blocked.Code);
    }

    private static async Task EnsureCreatedAsync(DbContextOptions options)
    {
        await using var db = new FinanceDbContext(options, TenantContext.ForOrdinaryMembership(
            new TenantId(TenantId),
            new MembershipReference(Guid.NewGuid()),
            correlationId: new CorrelationId("finance-test")));
        await db.Database.EnsureCreatedAsync();
    }

    private static FinancePersistence CreatePersistence(DbContextOptions options, IFinanceCompanyProvider? companyProvider = null) =>
        new(options, companyProvider ?? Companies(), new UnavailableInventoryValuationPersistence(), new UnavailableMasterDataExchangeRatePersistence());

    private static ConfiguredFinanceCompanyProvider Companies() =>
        new([new FinanceCompanyOption(TenantId, CompanyId, "Test Company", "SAR")]);

    private static ConfiguredFinanceCompanyProvider CompaniesWithOtherCompany() =>
        new([new FinanceCompanyOption(TenantId, CompanyId, "Test Company", "SAR"), new FinanceCompanyOption(TenantId, OtherCompanyId, "Other Company", "SAR")]);

    private static FinanceRequestContext Context(string permission, Guid actorId = default, Guid scopedCompanyId = default)
    {
        actorId = actorId == Guid.Empty ? ActorId : actorId;
        var tenantContext = TenantContext.ForOrdinaryMembership(
            new TenantId(TenantId),
            new MembershipReference(Guid.NewGuid()),
            scopedCompanyId == Guid.Empty ? null : new ScopeReference($"Company:{scopedCompanyId:D}"),
            correlationId: new CorrelationId("finance-test"));
        var foundation = FoundationRequestContext.ForTenant(actorId, SessionId, tenantContext, permission);
        Assert.True(FinanceRequestContext.TryCreate(foundation, out var context));
        return context!;
    }

    private static async Task<FinanceOperationResult<FinanceAccountRecord>> CreateAccountAsync(IFinancePersistence persistence, FinanceRequestContext context, string code, FinanceAccountType type, bool posting) =>
        await persistence.CreateAccountAsync(context, new FinanceAccountCommand(CompanyId, code, code, null, null, type, posting, FinanceCurrencyBehavior.TransactionCurrencyAllowed, new DateOnly(2026, 1, 1), null, Guid.NewGuid(), null, $"account-{code}", $"account-{code}"));

    private static async Task<FinanceFiscalPeriodRecord> OpenPeriodAsync(IFinancePersistence persistence, FinanceRequestContext context)
    {
        var calendar = await persistence.CreateCalendarAsync(context, new FinanceFiscalCalendarCommand(CompanyId, "Configured FY", Guid.NewGuid(), "calendar", "calendar"));
        var year = await persistence.CreateYearAsync(context, new FinanceFiscalYearCommand(calendar.Value!.Id, 2026, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), Guid.NewGuid(), "year", "year"));
        var period = await persistence.CreatePeriodAsync(context, new FinanceFiscalPeriodCommand(year.Value!.Id, 1, "2026-01", "January", null, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), Guid.NewGuid(), "period", "period"));
        var opened = await persistence.SetPeriodStateAsync(context, new FinancePeriodStateCommand(period.Value!.Id, FinanceFiscalPeriodState.Open, null, period.Value.Version, "open", "open"));
        Assert.True(opened.Succeeded, opened.Code);
        return opened.Value!;
    }

    private static FinanceJournalCommand Journal(Guid debitAccountId, Guid creditAccountId, string key, decimal debit, decimal? credit = null) =>
        new(CompanyId, new DateOnly(2026, 1, 15), new DateOnly(2026, 1, 15), null, 1m, null, null, null, "manual-journal.v1", "manual", null, null, null, "Test journal", [new FinanceJournalLineCommand(debitAccountId, debit, 0m, debit, "SAR", null, null), new FinanceJournalLineCommand(creditAccountId, 0m, credit ?? debit, credit ?? debit, "SAR", null, null)], Guid.NewGuid(), key, key);

    private static FinanceJournalActionCommand Action(FinanceJournalRecord journal, string key) =>
        new(journal.Id, journal.Version, key, key, key);

    private sealed class TestExchangeRatePersistence : IMasterDataExchangeRatePersistence
    {
        private readonly UnavailableMasterDataExchangeRatePersistence fallback = new();
        public MasterDataExchangeRateRecord? Record { get; set; }
        public Task<IReadOnlyList<MasterDataExchangeRateRecord>> ListExchangeRatesAsync(TenantContext tenantContext, CancellationToken cancellationToken = default) => fallback.ListExchangeRatesAsync(tenantContext, cancellationToken);
        public Task<MasterDataExchangeRateRecord?> FindExchangeRateAsync(TenantContext tenantContext, Guid exchangeRateId, CancellationToken cancellationToken = default) => Task.FromResult(Record?.Id == exchangeRateId ? Record : null);
        public Task<MasterDataPersistenceResult<MasterDataExchangeRateRecord>> CreateExchangeRateAsync(TenantContext tenantContext, Guid exchangeRateId, CreateMasterDataExchangeRateCommand command, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => fallback.CreateExchangeRateAsync(tenantContext, exchangeRateId, command, evidence, cancellationToken);
        public Task<MasterDataPersistenceResult<MasterDataExchangeRateRecord>> EditExchangeRateAsync(TenantContext tenantContext, EditMasterDataExchangeRateCommand command, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => fallback.EditExchangeRateAsync(tenantContext, command, evidence, cancellationToken);
        public Task<MasterDataPersistenceResult<MasterDataExchangeRateRecord>> SetExchangeRateLifecycleAsync(TenantContext tenantContext, Guid exchangeRateId, MasterDataLifecycleState lifecycleState, byte[] expectedVersion, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => fallback.SetExchangeRateLifecycleAsync(tenantContext, exchangeRateId, lifecycleState, expectedVersion, evidence, cancellationToken);
        public Task<MasterDataPersistenceResult<MasterDataAuditRecord>> AppendAuditAsync(TenantContext tenantContext, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => fallback.AppendAuditAsync(tenantContext, evidence, cancellationToken);
        public Task<IReadOnlyList<MasterDataAuditRecord>> ReadAuditHistoryAsync(TenantContext tenantContext, Guid? exchangeRateId = null, CancellationToken cancellationToken = default) => fallback.ReadAuditHistoryAsync(tenantContext, exchangeRateId, cancellationToken);
    }
}
