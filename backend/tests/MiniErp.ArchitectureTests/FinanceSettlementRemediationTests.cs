using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MiniErp.App.BuildingBlocks.Rest;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.Modules.BusinessParties;
using MiniErp.App.Modules.Finance;
using MiniErp.App.Modules.Inventory;
using MiniErp.App.Modules.MasterData;
using MiniErp.Contracts.Modules.Finance;
using MiniErp.Contracts.Modules.MasterData;
using MiniErp.Infrastructure.Persistence.Modules.Finance;
using Xunit;

namespace MiniErp.ArchitectureTests;

/// <summary>
/// Focused behavioral coverage for the MESP-133 Sol acceptance remediation.
/// These tests deliberately use the real Finance persistence implementation
/// and a disposable SQLite database; the SQL Server contention cases remain in
/// <see cref="SqlServerSafetyTests"/>.
/// </summary>
public sealed class FinanceSettlementRemediationTests
{
    private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid CompanyId = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid ActorId = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid ApproverId = Guid.Parse("66666666-6666-6666-6666-666666666666");
    private static readonly Guid CustomerId = Guid.Parse("77777777-7777-7777-7777-777777777777");

    [Fact]
    public async Task Settlement_configuration_is_manual_only_and_preserves_manual_identity_on_edit()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder().UseSqlite(connection).Options;
        await EnsureCreatedAsync(options);
        var persistence = CreatePersistence(options);
        var context = Context("tenant.finance.settlement.configure");

        var unsupported = await persistence.CreatePaymentMethodAsync(
            context,
            PaymentMethodCommand("NON-MANUAL", Guid.NewGuid(), isManual: false));

        Assert.False(unsupported.Succeeded);
        Assert.Equal("payment_method_not_supported", unsupported.Code);

        var created = await persistence.CreatePaymentMethodAsync(
            context,
            PaymentMethodCommand("MANUAL", Guid.NewGuid(), isManual: true));
        Assert.True(created.Succeeded, created.Code);
        Assert.True(created.Value!.IsManual);

        var forgedEdit = await persistence.EditPaymentMethodAsync(
            context,
            PaymentMethodCommand("MANUAL-FORGED", created.Value.Id, isManual: false) with
            {
                ExpectedVersion = created.Value.Version
            });

        Assert.False(forgedEdit.Succeeded);
        Assert.Equal("payment_method_not_supported", forgedEdit.Code);
        Assert.Equal("MANUAL", (await persistence.ListPaymentMethodsAsync(context, CompanyId)).Single().Code);
    }

    [Fact]
    public async Task Open_item_detail_enforces_kind_and_settlement_actions_enforce_route_direction()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder().UseSqlite(connection).Options;
        await EnsureCreatedAsync(options);
        var persistence = CreatePersistence(options);
        var context = Context("tenant.finance.settlement.submit");
        var openItemId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var linkedAccount = await CreateAccountAsync(options, context, "ROUTE-CASH-LINK");
        var method = await persistence.CreatePaymentMethodAsync(context, PaymentMethodCommand("ROUTE-METHOD", Guid.NewGuid(), true, FinancePaymentMethodDirection.Receipt));
        var cash = await persistence.CreateCashAccountAsync(context, CashAccountCommand("ROUTE-CASH", linkedAccount.Id, Guid.NewGuid()));
        Assert.True(method.Succeeded, method.Code);
        Assert.True(cash.Succeeded, cash.Code);

        await using (var db = new FinanceDbContext(options, context.TenantContext))
        {
            var item = new FinanceOpenItemEntity(
                context.TenantId,
                openItemId,
                FinanceOpenItemKind.Receivable,
                CompanyId,
                null,
                CustomerId,
                "manual-ar.v1",
                Guid.NewGuid(),
                1,
                Guid.NewGuid(),
                1,
                "AR-1",
                new DateOnly(2026, 1, 15),
                new DateOnly(2026, 2, 15),
                "SAR",
                100m,
                "SAR",
                100m,
                1m,
                null,
                null,
                null,
                null,
                null,
                null,
                null);
            item.SetRecognition(FinanceOpenItemRecognitionState.Recognized, Guid.NewGuid());
            db.OpenItems.Add(item);

            var document = NewDocument(
                context,
                documentId,
                FinancePaymentMethodDirection.Receipt,
                cash.Value!.Id,
                method.Value!.Id,
                CustomerId);
            db.SettlementDocuments.Add(document);
            await db.SaveChangesAsync();
        }

        Assert.Null(await persistence.GetOpenItemAsync(context, openItemId, FinanceOpenItemKind.Payable));
        Assert.NotNull(await persistence.GetOpenItemAsync(context, openItemId, FinanceOpenItemKind.Receivable));

        await using var readDb = new FinanceDbContext(options, context.TenantContext);
        var documentVersion = await readDb.SettlementDocuments
            .Where(item => item.Id == documentId)
            .Select(item => item.Version)
            .SingleAsync();
        var wrongRoute = await persistence.TransitionSettlementDocumentAsync(
            context,
            new FinanceSettlementActionCommand(
                documentId,
                documentVersion,
                null,
                "wrong-route",
                "wrong-route",
                FinancePaymentMethodDirection.Payment),
            FinanceSettlementDocumentStatus.Submitted);

        Assert.False(wrongRoute.Succeeded);
        Assert.Equal("settlement_direction_mismatch", wrongRoute.Code);
    }

    [Fact]
    public async Task Rejected_settlement_edit_returns_to_draft_before_reapproval()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder().UseSqlite(connection).Options;
        await EnsureCreatedAsync(options);
        var persistence = CreatePersistence(options);
        var context = Context("tenant.finance.settlement.configure");
        var linkedAccount = await CreateAccountAsync(options, context, "SETTLEMENT-EDIT-CASH");
        var method = await persistence.CreatePaymentMethodAsync(
            context,
            PaymentMethodCommand("EDIT-METHOD", Guid.NewGuid(), isManual: true, direction: FinancePaymentMethodDirection.Receipt));
        Assert.True(method.Succeeded, method.Code);
        var cash = await persistence.CreateCashAccountAsync(
            context,
            CashAccountCommand("EDIT-CASH", linkedAccount.Id, Guid.NewGuid()));
        Assert.True(cash.Succeeded, cash.Code);

        var documentId = Guid.NewGuid();
        await using (var db = new FinanceDbContext(options, context.TenantContext))
        {
            var document = NewDocument(context, documentId, FinancePaymentMethodDirection.Receipt, cash.Value!.Id, method.Value!.Id, CustomerId);
            document.SetStatus(FinanceSettlementDocumentStatus.Submitted, ActorId, DateTimeOffset.UtcNow);
            document.SetStatus(FinanceSettlementDocumentStatus.Rejected, ApproverId, DateTimeOffset.UtcNow);
            db.SettlementDocuments.Add(document);
            await db.SaveChangesAsync();
        }

        await using var readDb = new FinanceDbContext(options, context.TenantContext);
        var version = await readDb.SettlementDocuments.Where(item => item.Id == documentId).Select(item => item.Version).SingleAsync();
        var edited = await persistence.EditSettlementDocumentAsync(
            context,
            new FinanceSettlementDocumentCommand(
                FinancePaymentMethodDirection.Receipt,
                CompanyId,
                null,
                CustomerId,
                cash.Value.Id,
                method.Value.Id,
                new DateOnly(2026, 1, 15),
                "SAR",
                125m,
                null,
                null,
                null,
                null,
                null,
                "edited",
                "edited rejected settlement",
                documentId,
                "rejected-edit",
                "rejected-edit"),
            version);

        Assert.True(edited.Succeeded, edited.Code);
        Assert.Equal(FinanceSettlementDocumentStatus.Draft, edited.Value!.Status);
    }

    [Fact]
    public async Task Shared_source_approval_policy_blocks_self_approval_and_cash_gl_mapping_is_authoritative()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder().UseSqlite(connection).Options;
        await EnsureCreatedAsync(options);
        var policy = new RequiredApprovalPolicy();
        var persistence = CreatePersistence(options, policy);
        var context = Context("tenant.finance.settlement.submit");

        var selfLinked = await CreateAccountAsync(options, context, "SELF-APPROVAL-CASH");
        var selfMethod = await persistence.CreatePaymentMethodAsync(context, PaymentMethodCommand("SELF-METHOD", Guid.NewGuid(), true, FinancePaymentMethodDirection.Receipt));
        var selfCash = await persistence.CreateCashAccountAsync(context, CashAccountCommand("SELF-CASH", selfLinked.Id, Guid.NewGuid()));
        Assert.True(selfMethod.Succeeded, selfMethod.Code);
        Assert.True(selfCash.Succeeded, selfCash.Code);
        var selfApprovalId = Guid.NewGuid();
        await using (var db = new FinanceDbContext(options, context.TenantContext))
        {
            var document = NewDocument(context, selfApprovalId, FinancePaymentMethodDirection.Receipt, selfCash.Value!.Id, selfMethod.Value!.Id, CustomerId);
            document.SetStatus(FinanceSettlementDocumentStatus.Submitted, ActorId, DateTimeOffset.UtcNow);
            db.SettlementDocuments.Add(document);
            await db.SaveChangesAsync();
        }
        await using var approvalReadDb = new FinanceDbContext(options, context.TenantContext);
        var submittedVersion = await approvalReadDb.SettlementDocuments.Where(item => item.Id == selfApprovalId).Select(item => item.Version).SingleAsync();
        var selfApproval = await persistence.TransitionSettlementDocumentAsync(
            Context("tenant.finance.settlement.approve", ActorId),
            new FinanceSettlementActionCommand(selfApprovalId, submittedVersion, null, "self-approval", "self-approval", FinancePaymentMethodDirection.Receipt),
            FinanceSettlementDocumentStatus.Approved);
        Assert.False(selfApproval.Succeeded);
        Assert.Equal("self_approval_forbidden", selfApproval.Code);

        var linked = await CreateAccountAsync(options, context, "CASH-LINKED");
        var wrong = await CreateAccountAsync(options, context, "WRONG-CASH-MAPPING");
        var credit = await CreateAccountAsync(options, context, "RECEIPT-CREDIT");
        await OpenPeriodAndRuleAsync(options, context, wrong.Id, credit.Id, "customer-receipt.v1", "on-account");
        var method = await persistence.CreatePaymentMethodAsync(context, PaymentMethodCommand("POST-METHOD", Guid.NewGuid(), true, FinancePaymentMethodDirection.Receipt));
        var cash = await persistence.CreateCashAccountAsync(context, CashAccountCommand("POST-CASH", linked.Id, Guid.NewGuid()));
        Assert.True(method.Succeeded, method.Code);
        Assert.True(cash.Succeeded, cash.Code);
        var postId = Guid.NewGuid();
        await using (var db = new FinanceDbContext(options, context.TenantContext))
        {
            var document = NewDocument(context, postId, FinancePaymentMethodDirection.Receipt, cash.Value!.Id, method.Value!.Id, CustomerId);
            document.SetStatus(FinanceSettlementDocumentStatus.Submitted, ActorId, DateTimeOffset.UtcNow);
            document.SetStatus(FinanceSettlementDocumentStatus.Approved, ApproverId, DateTimeOffset.UtcNow);
            db.SettlementDocuments.Add(document);
            await db.SaveChangesAsync();
        }
        await using var postReadDb = new FinanceDbContext(options, context.TenantContext);
        var approvedVersion = await postReadDb.SettlementDocuments.Where(item => item.Id == postId).Select(item => item.Version).SingleAsync();
        var blocked = await persistence.PostSettlementDocumentAsync(
            Context("tenant.finance.settlement.post", ApproverId),
            new FinanceSettlementActionCommand(postId, approvedVersion, null, "cash-map", "cash-map", FinancePaymentMethodDirection.Receipt));

        Assert.False(blocked.Succeeded);
        Assert.Equal("posting_rule_cash_account_mismatch", blocked.Code);
    }

    [Fact]
    public async Task Aging_uses_allocation_effective_as_of_date_instead_of_current_state()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder().UseSqlite(connection).Options;
        await EnsureCreatedAsync(options);
        var persistence = CreatePersistence(options);
        var context = Context("tenant.finance.ap.aging");
        var itemId = Guid.NewGuid();
        var documentId = Guid.NewGuid();
        var allocationId = Guid.NewGuid();
        var linkedAccount = await CreateAccountAsync(options, context, "ASOF-CASH-LINK");
        var method = await persistence.CreatePaymentMethodAsync(context, PaymentMethodCommand("ASOF-METHOD", Guid.NewGuid(), true, FinancePaymentMethodDirection.Payment));
        var cash = await persistence.CreateCashAccountAsync(context, CashAccountCommand("ASOF-CASH", linkedAccount.Id, Guid.NewGuid()));
        Assert.True(method.Succeeded, method.Code);
        Assert.True(cash.Succeeded, cash.Code);
        await using (var db = new FinanceDbContext(options, context.TenantContext))
        {
            var item = new FinanceOpenItemEntity(
                context.TenantId,
                itemId,
                FinanceOpenItemKind.Payable,
                CompanyId,
                Guid.NewGuid(),
                null,
                "procurement-supplier-invoice.v1",
                Guid.NewGuid(),
                1,
                Guid.NewGuid(),
                1,
                "AP-ASOF",
                new DateOnly(2026, 1, 1),
                new DateOnly(2026, 2, 1),
                "SAR",
                100m,
                "SAR",
                100m,
                1m,
                null,
                null,
                null,
                null,
                null,
                null,
                null);
            item.SetRecognition(FinanceOpenItemRecognitionState.Recognized, Guid.NewGuid());
            db.OpenItems.Add(item);

            var document = NewDocument(context, documentId, FinancePaymentMethodDirection.Payment, cash.Value!.Id, method.Value!.Id, null, item.SupplierId);
            document.SetStatus(FinanceSettlementDocumentStatus.Submitted, ActorId, DateTimeOffset.UtcNow);
            document.SetStatus(FinanceSettlementDocumentStatus.Approved, ApproverId, DateTimeOffset.UtcNow);
            document.SetStatus(FinanceSettlementDocumentStatus.Posted, ApproverId, DateTimeOffset.UtcNow);
            db.SettlementDocuments.Add(document);
            db.Allocations.Add(new FinanceAllocationEntity(
                context.TenantId,
                new FinanceAllocationCommand(documentId, itemId, 25m, new DateOnly(2026, 3, 15), "future allocation", allocationId, "asof-allocation", "asof-allocation"),
                CompanyId,
                "SAR",
                25m,
                ActorId));
            await db.SaveChangesAsync();
        }

        var beforeAllocation = await persistence.GetAgingAsync(
            context,
            new FinanceAgingQuery(CompanyId, new DateOnly(2026, 3, 1), FinanceOpenItemKind.Payable));
        var afterAllocation = await persistence.GetAgingAsync(
            context,
            new FinanceAgingQuery(CompanyId, new DateOnly(2026, 3, 20), FinanceOpenItemKind.Payable));

        var before = Assert.Single(beforeAllocation);
        var after = Assert.Single(afterAllocation);
        Assert.Equal(0m, before.AllocatedAmount);
        Assert.Equal(100m, before.OutstandingAmount);
        Assert.Equal(25m, after.AllocatedAmount);
        Assert.Equal(75m, after.OutstandingAmount);
    }

    private static FinanceSettlementPersistence CreatePersistence(
        DbContextOptions options,
        IFinanceSourceApprovalPolicy? policy = null,
        IBusinessCustomerReferenceReader? customers = null,
        IMasterDataCurrencyPaymentTermPersistence? paymentTerms = null) =>
        new(
            options,
            Companies(),
            new UnavailableMasterDataExchangeRatePersistence(),
            customers ?? new UnavailableCustomerPersistence(),
            new UnavailableSupplierPersistence(),
            paymentTerms ?? new UnavailableMasterDataCurrencyPaymentTermPersistence(),
            new UnavailableFinanceSupplierInvoiceSourceProvider(),
            policy);

    private static ConfiguredFinanceCompanyProvider Companies() =>
        new([new FinanceCompanyOption(TenantId, CompanyId, "Test Company", "SAR")]);

    private static FinancePaymentMethodCommand PaymentMethodCommand(
        string code,
        Guid id,
        bool isManual,
        FinancePaymentMethodDirection direction = FinancePaymentMethodDirection.Both) =>
        new(CompanyId, code, code, null, direction, isManual, false, new DateOnly(2026, 1, 1), null, id, null, $"method-{code}", $"method-{code}");

    private static FinanceCashAccountCommand CashAccountCommand(string code, Guid linkedAccountId, Guid id) =>
        new(CompanyId, code, code, null, FinanceCashAccountKind.Bank, "SAR", linkedAccountId, null, new DateOnly(2026, 1, 1), null, id, null, $"cash-{code}", $"cash-{code}");

    private static FinanceSettlementDocumentEntity NewDocument(
        FinanceRequestContext context,
        Guid id,
        FinancePaymentMethodDirection direction,
        Guid cashAccountId,
        Guid paymentMethodId,
        Guid? customerId,
        Guid? supplierId = null) =>
        new(
            context.TenantId,
            new FinanceSettlementDocumentCommand(
                direction,
                CompanyId,
                direction == FinancePaymentMethodDirection.Payment ? supplierId ?? Guid.NewGuid() : null,
                direction == FinancePaymentMethodDirection.Receipt ? customerId ?? CustomerId : null,
                cashAccountId,
                paymentMethodId,
                new DateOnly(2026, 1, 15),
                "SAR",
                100m,
                null,
                null,
                null,
                null,
                null,
                null,
                null,
                id,
                $"document-{id:N}",
                $"document-{id:N}"),
            "SAR",
            "SAR",
            100m,
            context.ActorId,
            DateTimeOffset.UtcNow);

    private static async Task<FinanceAccountRecord> CreateAccountAsync(DbContextOptions options, FinanceRequestContext context, string code)
    {
        var persistence = new FinancePersistence(options, Companies(), new UnavailableInventoryValuationPersistence(), new UnavailableMasterDataExchangeRatePersistence());
        var result = await persistence.CreateAccountAsync(
            context,
            new FinanceAccountCommand(CompanyId, code, code, null, null, FinanceAccountType.Asset, true, FinanceCurrencyBehavior.TransactionCurrencyAllowed, new DateOnly(2026, 1, 1), null, Guid.NewGuid(), null, $"account-{code}", $"account-{code}"));
        Assert.True(result.Succeeded, result.Code);
        return result.Value!;
    }

    private static async Task OpenPeriodAndRuleAsync(
        DbContextOptions options,
        FinanceRequestContext context,
        Guid debitAccountId,
        Guid creditAccountId,
        string contract,
        string eventName)
    {
        var persistence = new FinancePersistence(options, Companies(), new UnavailableInventoryValuationPersistence(), new UnavailableMasterDataExchangeRatePersistence());
        var calendar = await persistence.CreateCalendarAsync(context, new FinanceFiscalCalendarCommand(CompanyId, "FY", Guid.NewGuid(), "calendar", "calendar"));
        var year = await persistence.CreateYearAsync(context, new FinanceFiscalYearCommand(calendar.Value!.Id, 2026, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), Guid.NewGuid(), "year", "year"));
        var period = await persistence.CreatePeriodAsync(context, new FinanceFiscalPeriodCommand(year.Value!.Id, 1, "2026-01", "January", null, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), Guid.NewGuid(), "period", "period"));
        var opened = await persistence.SetPeriodStateAsync(context, new FinancePeriodStateCommand(period.Value!.Id, FinanceFiscalPeriodState.Open, null, period.Value.Version, "period-open", "period-open"));
        Assert.True(opened.Succeeded, opened.Code);
        var rule = await persistence.CreatePostingRuleAsync(context, new FinancePostingRuleCommand(CompanyId, contract, eventName, debitAccountId, creditAccountId, false, new DateOnly(2026, 1, 1), null, Guid.NewGuid(), "rule", "rule"));
        Assert.True(rule.Succeeded, rule.Code);
    }

    private static async Task EnsureCreatedAsync(DbContextOptions options)
    {
        await using var db = new FinanceDbContext(options, TenantContext.ForOrdinaryMembership(new TenantId(TenantId), new MembershipReference(Guid.NewGuid()), correlationId: new CorrelationId("finance-remediation")));
        await db.Database.EnsureCreatedAsync();
    }

    private static FinanceRequestContext Context(string permission, Guid actorId = default)
    {
        var foundation = FoundationRequestContext.ForTenant(actorId == Guid.Empty ? ActorId : actorId, Guid.NewGuid(), TenantContext.ForOrdinaryMembership(new TenantId(TenantId), new MembershipReference(Guid.NewGuid()), correlationId: new CorrelationId("finance-remediation")), permission);
        Assert.True(FinanceRequestContext.TryCreate(foundation, out var context));
        return context!;
    }

    private sealed class RequiredApprovalPolicy : IFinanceSourceApprovalPolicy
    {
        public FinanceApprovalRequirement Resolve(string sourceContract, string sourceEvent) =>
            sourceContract is "supplier-payment.v1" or "customer-receipt.v1"
                ? FinanceApprovalRequirement.Required
                : FinanceApprovalRequirement.NotConfigured;
    }

    private sealed class ActiveCustomerReader : IBusinessCustomerReferenceReader
    {
        public Task<BusinessCustomerReference?> FindCustomerReferenceAsync(TenantContext tenantContext, Guid customerId, CancellationToken cancellationToken = default) =>
            Task.FromResult<BusinessCustomerReference?>(customerId == CustomerId
                ? new BusinessCustomerReference(CustomerId, tenantContext.TenantId, "CUSTOMER-1", MasterDataLifecycleState.Active)
                : null);
    }
}
