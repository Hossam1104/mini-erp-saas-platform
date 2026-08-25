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
    private static readonly Guid SupplierId = Guid.Parse("88888888-8888-8888-8888-888888888888");

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
        Assert.Null(await persistence.GetSettlementDocumentAsync(context, documentId, FinancePaymentMethodDirection.Payment));
        Assert.NotNull(await persistence.GetSettlementDocumentAsync(context, documentId, FinancePaymentMethodDirection.Receipt));

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
    public async Task Manual_ar_requires_a_server_payment_term_even_when_client_supplies_a_due_date()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder().UseSqlite(connection).Options;
        await EnsureCreatedAsync(options);
        var persistence = CreatePersistence(options, customers: new ActiveCustomerReader());

        var result = await persistence.CreateManualReceivableAsync(
            Context("tenant.finance.ar.create"),
            new FinanceManualReceivableCommand(
                CompanyId,
                CustomerId,
                new DateOnly(2026, 1, 15),
                new DateOnly(2026, 2, 15),
                null,
                "SAR",
                100m,
                null,
                null,
                null,
                null,
                null,
                "AR-TERM-REQUIRED",
                "manual AR term assertion",
                Guid.NewGuid(),
                "manual-ar-term-required",
                "manual-ar-term-required"));

        Assert.False(result.Succeeded);
        Assert.Equal("payment_term_not_configured", result.Code);
    }

    [Fact]
    public async Task Receipt_exposure_uses_posted_and_reversal_journal_dates_for_as_of_truth()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder().UseSqlite(connection).Options;
        await EnsureCreatedAsync(options);
        var context = Context("tenant.finance.settlement.create");
        var cashAccount = await CreateAccountAsync(options, context, "RECEIPT-ASOF-CASH");
        var controlAccount = await CreateAccountAsync(options, context, "RECEIPT-ASOF-CONTROL");
        await OpenPeriodAndRuleAsync(options, context, cashAccount.Id, controlAccount.Id, "customer-receipt.v1", "on-account");
        var persistence = CreatePersistence(options, new RequiredApprovalPolicy(), new ActiveCustomerReader());
        var method = await persistence.CreatePaymentMethodAsync(context, PaymentMethodCommand("ASOF-RECEIPT-METHOD", Guid.NewGuid(), true, FinancePaymentMethodDirection.Receipt));
        var cash = await persistence.CreateCashAccountAsync(context, CashAccountCommand("ASOF-RECEIPT-CASH", cashAccount.Id, Guid.NewGuid()));
        Assert.True(method.Succeeded, method.Code);
        Assert.True(cash.Succeeded, cash.Code);
        var created = await persistence.CreateSettlementDocumentAsync(context, new FinanceSettlementDocumentCommand(FinancePaymentMethodDirection.Receipt, CompanyId, null, CustomerId, cash.Value!.Id, method.Value!.Id, new DateOnly(2026, 1, 10), "SAR", 100m, null, null, null, null, null, "ASOF-RECEIPT", "as-of receipt", Guid.NewGuid(), "asof-receipt-create", "asof-receipt-create"));
        Assert.True(created.Succeeded, created.Code);
        var submitted = await persistence.TransitionSettlementDocumentAsync(context, new FinanceSettlementActionCommand(created.Value!.Id, created.Value.Version, null, "asof-receipt-submit", "asof-receipt-submit", FinancePaymentMethodDirection.Receipt), FinanceSettlementDocumentStatus.Submitted);
        Assert.True(submitted.Succeeded, submitted.Code);
        var approved = await persistence.TransitionSettlementDocumentAsync(Context("tenant.finance.settlement.approve", ApproverId), new FinanceSettlementActionCommand(submitted.Value!.Id, submitted.Value.Version, null, "asof-receipt-approve", "asof-receipt-approve", FinancePaymentMethodDirection.Receipt), FinanceSettlementDocumentStatus.Approved);
        Assert.True(approved.Succeeded, approved.Code);
        var posted = await persistence.PostSettlementDocumentAsync(Context("tenant.finance.settlement.post", ApproverId), new FinanceSettlementActionCommand(approved.Value!.Id, approved.Value.Version, null, "asof-receipt-post", "asof-receipt-post", FinancePaymentMethodDirection.Receipt));
        Assert.True(posted.Succeeded, posted.Code);

        var beforePosting = await persistence.GetExposureAsync(context, new FinanceExposureQuery(CompanyId, CustomerId, new DateOnly(2026, 1, 5)));
        var beforeReversal = await persistence.GetExposureAsync(context, new FinanceExposureQuery(CompanyId, CustomerId, new DateOnly(2026, 1, 15)));
        Assert.Equal(0m, beforePosting!.UnappliedCredits);
        Assert.Equal(100m, beforeReversal!.UnappliedCredits);

        var reversed = await persistence.ReverseSettlementDocumentAsync(Context("tenant.finance.settlement.reverse", ApproverId), new FinanceSettlementReversalCommand(posted.Value!.Id, new DateOnly(2026, 1, 20), "as-of receipt reversal", Guid.NewGuid(), "asof-receipt-reverse", "asof-receipt-reverse", FinancePaymentMethodDirection.Receipt));
        Assert.True(reversed.Succeeded, reversed.Code);
        var afterReversal = await persistence.GetExposureAsync(context, new FinanceExposureQuery(CompanyId, CustomerId, new DateOnly(2026, 1, 20)));
        Assert.Equal(0m, afterReversal!.UnappliedCredits);
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

    [Fact]
    public async Task Historical_reconciliation_preserves_ap_and_ar_lineage_after_rule_change_and_allocation_reversal()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder().UseSqlite(connection).Options;
        await EnsureCreatedAsync(options);
        var context = Context("tenant.finance.reconciliation");
        var expense = await CreateAccountAsync(options, context, "HOLD3-EXPENSE");
        var apControl = await CreateAccountAsync(options, context, "HOLD3-AP-CONTROL");
        var changedControl = await CreateAccountAsync(options, context, "HOLD3-CHANGED-CONTROL");
        var cashAccount = await CreateAccountAsync(options, context, "HOLD3-CASH");
        var arControl = await CreateAccountAsync(options, context, "HOLD3-AR-CONTROL");
        var revenue = await CreateAccountAsync(options, context, "HOLD3-REVENUE");
        await OpenFullYearAndRulesAsync(options, context, [
            ("procurement-supplier-invoice.v1", "recognition", expense.Id, apControl.Id, new DateOnly(2026, 1, 1), null),
            ("supplier-payment.v1", "on-account", apControl.Id, cashAccount.Id, new DateOnly(2026, 1, 1), null),
            ("supplier-payment.v1", "allocation", changedControl.Id, cashAccount.Id, new DateOnly(2026, 1, 1), new DateOnly(2026, 3, 31)),
            ("supplier-payment.v1", "allocation", apControl.Id, cashAccount.Id, new DateOnly(2026, 4, 1), null),
            ("manual-ar.v1", "recognition", arControl.Id, revenue.Id, new DateOnly(2026, 1, 1), null),
        ]);

        var source = new FinanceSupplierInvoiceSourceRecord(
            TenantId, CompanyId, SupplierId, "procurement-supplier-invoice.v1", Guid.NewGuid(), 1,
            Guid.NewGuid(), 1, "HOLD3-AP-1", new DateOnly(2026, 1, 15), "SAR", 100m, "SAR", 100m,
            1m, null, null, null,
            new FinancePaymentTermSnapshotRecord(Guid.NewGuid(), "NET30", "Net 30", null, 1, Guid.NewGuid(), new DateOnly(2026, 1, 1), new DateOnly(2026, 2, 14)),
            new DateOnly(2026, 2, 14), Guid.NewGuid(), 1, "hold3-source", "hold3-source");
        var persistence = CreatePersistence(options, new RequiredApprovalPolicy(), suppliers: new ActiveSupplierReader(SupplierId), sourceProvider: new StaticSupplierInvoiceSourceProvider(source));

        var recognized = await persistence.RecognizeSupplierInvoiceAsync(context, new FinanceSupplierInvoiceRecognitionCommand(source.SourceEvidenceId, "hold3-recognize", "hold3-recognize"));
        Assert.True(recognized.Succeeded, recognized.Code);
        var method = await persistence.CreatePaymentMethodAsync(context, PaymentMethodCommand("HOLD3-METHOD", Guid.NewGuid(), true, FinancePaymentMethodDirection.Payment));
        var cash = await persistence.CreateCashAccountAsync(context, CashAccountCommand("HOLD3-CASH", cashAccount.Id, Guid.NewGuid()));
        Assert.True(method.Succeeded, method.Code);
        Assert.True(cash.Succeeded, cash.Code);
        var created = await persistence.CreateSettlementDocumentAsync(context, new FinanceSettlementDocumentCommand(FinancePaymentMethodDirection.Payment, CompanyId, SupplierId, null, cash.Value!.Id, method.Value!.Id, new DateOnly(2026, 1, 15), "SAR", 100m, null, null, null, null, null, "HOLD3-PAY", "hold3 payment", Guid.NewGuid(), "hold3-payment-create", "hold3-payment-create"));
        Assert.True(created.Succeeded, created.Code);
        await using (var ruleDb = new FinanceDbContext(options, context.TenantContext))
        {
            Assert.Single(await ruleDb.PostingRules.Where(item => item.SourceContract == "supplier-payment.v1" && item.SourceEvent == "on-account").ToListAsync());
        }
        var submitted = await persistence.TransitionSettlementDocumentAsync(context, new FinanceSettlementActionCommand(created.Value!.Id, created.Value.Version, null, "hold3-payment-submit", "hold3-payment-submit", FinancePaymentMethodDirection.Payment), FinanceSettlementDocumentStatus.Submitted);
        Assert.True(submitted.Succeeded, submitted.Code);
        var approved = await persistence.TransitionSettlementDocumentAsync(Context("tenant.finance.settlement.approve", ApproverId), new FinanceSettlementActionCommand(submitted.Value!.Id, submitted.Value.Version, null, "hold3-payment-approve", "hold3-payment-approve", FinancePaymentMethodDirection.Payment), FinanceSettlementDocumentStatus.Approved);
        Assert.True(approved.Succeeded, approved.Code);
        var posted = await persistence.PostSettlementDocumentAsync(Context("tenant.finance.settlement.post", ApproverId), new FinanceSettlementActionCommand(approved.Value!.Id, approved.Value.Version, null, "hold3-payment-post", "hold3-payment-post", FinancePaymentMethodDirection.Payment));
        Assert.True(posted.Succeeded, posted.Code);

        var blocked = await persistence.CreateAllocationAsync(context, new FinanceAllocationCommand(posted.Value!.Id, recognized.Value!.Id, 40m, new DateOnly(2026, 1, 15), "old mapping must not clear historical AP", Guid.NewGuid(), "hold3-allocation-blocked", "hold3-allocation-blocked"));
        Assert.False(blocked.Succeeded);
        Assert.Equal("posting_rule_control_account_mismatch", blocked.Code);
        var allocated = await persistence.CreateAllocationAsync(context, new FinanceAllocationCommand(posted.Value.Id, recognized.Value.Id, 40m, new DateOnly(2026, 4, 15), "changed rule clears historical AP", Guid.NewGuid(), "hold3-allocation-create", "hold3-allocation-create"));
        Assert.True(allocated.Succeeded, allocated.Code);
        var afterAllocation = await persistence.GetReconciliationAsync(context, CompanyId);
        Assert.Equal(FinanceReconciliationStatus.Reconciled, Assert.Single(afterAllocation, row => row.Kind == FinanceOpenItemKind.Payable).Status);

        var reversed = await persistence.ReverseAllocationAsync(context, new FinanceAllocationReversalCommand(allocated.Value!.Id, allocated.Value.Version, "restore AP outstanding", Guid.NewGuid(), "hold3-allocation-reverse", "hold3-allocation-reverse"));
        Assert.True(reversed.Succeeded, reversed.Code);
        var afterReversal = await persistence.GetReconciliationAsync(context, CompanyId);
        Assert.Equal(FinanceReconciliationStatus.Reconciled, Assert.Single(afterReversal, row => row.Kind == FinanceOpenItemKind.Payable).Status);
        var aging = await persistence.GetAgingAsync(context, new FinanceAgingQuery(CompanyId, new DateOnly(2026, 8, 25), FinanceOpenItemKind.Payable));
        Assert.Equal(100m, Assert.Single(aging).OutstandingAmount);

        await SeedRecognizedItemAndJournalAsync(options, context, FinanceOpenItemKind.Receivable, Guid.NewGuid(), CustomerId, arControl.Id, revenue.Id, "hold3-ar");
        var bothDirections = await persistence.GetReconciliationAsync(context, CompanyId);
        Assert.Equal(FinanceReconciliationStatus.Reconciled, Assert.Single(bothDirections, row => row.Kind == FinanceOpenItemKind.Payable).Status);
        Assert.Equal(FinanceReconciliationStatus.Reconciled, Assert.Single(bothDirections, row => row.Kind == FinanceOpenItemKind.Receivable).Status);
        Assert.DoesNotContain(bothDirections, row => row.Status == FinanceReconciliationStatus.PendingMapping);
    }

    [Fact]
    public async Task Supplier_invoice_recognition_fails_closed_when_authoritative_supplier_is_missing_or_inactive()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder().UseSqlite(connection).Options;
        await EnsureCreatedAsync(options);
        var source = SourceForTest();
        var missing = CreatePersistence(options, suppliers: new ActiveSupplierReader(Guid.NewGuid()), sourceProvider: new StaticSupplierInvoiceSourceProvider(source));
        var missingResult = await missing.RecognizeSupplierInvoiceAsync(Context("tenant.finance.ap.recognize"), new FinanceSupplierInvoiceRecognitionCommand(source.SourceEvidenceId, "hold3-supplier-missing", "hold3-supplier-missing"));
        Assert.False(missingResult.Succeeded);
        Assert.Equal("party_scope_denied", missingResult.Code);

        var inactive = CreatePersistence(options, suppliers: new ActiveSupplierReader(SupplierId, MasterDataLifecycleState.Inactive), sourceProvider: new StaticSupplierInvoiceSourceProvider(source));
        var inactiveResult = await inactive.RecognizeSupplierInvoiceAsync(Context("tenant.finance.ap.recognize"), new FinanceSupplierInvoiceRecognitionCommand(source.SourceEvidenceId, "hold3-supplier-inactive", "hold3-supplier-inactive"));
        Assert.False(inactiveResult.Succeeded);
        Assert.Equal("party_scope_denied", inactiveResult.Code);
    }

    [Fact]
    public async Task Supplier_invoice_recognition_rejects_a_cross_tenant_source_before_finance_posting()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder().UseSqlite(connection).Options;
        await EnsureCreatedAsync(options);
        var source = SourceForTest() with { TenantId = Guid.NewGuid() };
        var persistence = CreatePersistence(options, suppliers: new ActiveSupplierReader(SupplierId), sourceProvider: new StaticSupplierInvoiceSourceProvider(source));
        var result = await persistence.RecognizeSupplierInvoiceAsync(Context("tenant.finance.ap.recognize"), new FinanceSupplierInvoiceRecognitionCommand(source.SourceEvidenceId, "hold3-source-tenant", "hold3-source-tenant"));
        Assert.False(result.Succeeded);
        Assert.Equal("source_not_ready", result.Code);
    }

    [Fact]
    public async Task Supplier_invoice_recognition_fails_closed_for_an_inactive_authoritative_supplier()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder().UseSqlite(connection).Options;
        await EnsureCreatedAsync(options);
        var source = SourceForTest();
        var persistence = CreatePersistence(options, suppliers: new ActiveSupplierReader(SupplierId, MasterDataLifecycleState.Inactive), sourceProvider: new StaticSupplierInvoiceSourceProvider(source));
        var result = await persistence.RecognizeSupplierInvoiceAsync(Context("tenant.finance.ap.recognize"), new FinanceSupplierInvoiceRecognitionCommand(source.SourceEvidenceId, "hold3-supplier-inactive-only", "hold3-supplier-inactive-only"));
        Assert.False(result.Succeeded);
        Assert.Equal("party_scope_denied", result.Code);
    }

    private static FinanceSettlementPersistence CreatePersistence(
        DbContextOptions options,
        IFinanceSourceApprovalPolicy? policy = null,
        IBusinessCustomerReferenceReader? customers = null,
        IMasterDataCurrencyPaymentTermPersistence? paymentTerms = null,
        ISupplierPersistence? suppliers = null,
        IFinanceSupplierInvoiceSourceProvider? sourceProvider = null) =>
        new(
            options,
            Companies(),
            new UnavailableMasterDataExchangeRatePersistence(),
            customers ?? new UnavailableCustomerPersistence(),
            suppliers ?? new UnavailableSupplierPersistence(),
            paymentTerms ?? new UnavailableMasterDataCurrencyPaymentTermPersistence(),
            sourceProvider ?? new UnavailableFinanceSupplierInvoiceSourceProvider(),
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

    private static async Task OpenFullYearAndRulesAsync(
        DbContextOptions options,
        FinanceRequestContext context,
        IReadOnlyList<(string Contract, string Event, Guid Debit, Guid Credit, DateOnly From, DateOnly? To)> rules)
    {
        var persistence = new FinancePersistence(options, Companies(), new UnavailableInventoryValuationPersistence(), new UnavailableMasterDataExchangeRatePersistence());
        var calendar = await persistence.CreateCalendarAsync(context, new FinanceFiscalCalendarCommand(CompanyId, "HOLD3-FY", Guid.NewGuid(), "hold3-calendar", "hold3-calendar"));
        var year = await persistence.CreateYearAsync(context, new FinanceFiscalYearCommand(calendar.Value!.Id, 2026, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), Guid.NewGuid(), "hold3-year", "hold3-year"));
        var period = await persistence.CreatePeriodAsync(context, new FinanceFiscalPeriodCommand(year.Value!.Id, 1, "2026", "2026", null, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), Guid.NewGuid(), "hold3-period", "hold3-period"));
        var opened = await persistence.SetPeriodStateAsync(context, new FinancePeriodStateCommand(period.Value!.Id, FinanceFiscalPeriodState.Open, null, period.Value.Version, "hold3-period-open", "hold3-period-open"));
        Assert.True(opened.Succeeded, opened.Code);
        foreach (var definition in rules)
        {
            var key = $"hold3-rule-{definition.Contract}-{definition.Event}-{definition.From:yyyyMMdd}";
            var rule = await persistence.CreatePostingRuleAsync(context, new FinancePostingRuleCommand(CompanyId, definition.Contract, definition.Event, definition.Debit, definition.Credit, false, definition.From, definition.To, Guid.NewGuid(), key, key));
            Assert.True(rule.Succeeded, rule.Code);
        }
    }

    private static FinanceSupplierInvoiceSourceRecord SourceForTest() => new(
        TenantId, CompanyId, SupplierId, "procurement-supplier-invoice.v1", Guid.NewGuid(), 1,
        Guid.NewGuid(), 1, "HOLD3-SOURCE", new DateOnly(2026, 1, 15), "SAR", 100m, "SAR", 100m,
        1m, null, null, null,
        new FinancePaymentTermSnapshotRecord(Guid.NewGuid(), "NET30", "Net 30", null, 1, Guid.NewGuid(), new DateOnly(2026, 1, 1), new DateOnly(2026, 2, 14)),
        new DateOnly(2026, 2, 14), Guid.NewGuid(), 1, "hold3-source", "hold3-source");

    private static async Task SeedRecognizedItemAndJournalAsync(
        DbContextOptions options,
        FinanceRequestContext context,
        FinanceOpenItemKind kind,
        Guid itemId,
        Guid partyId,
        Guid controlAccountId,
        Guid balancingAccountId,
        string reference)
    {
        await using var db = new FinanceDbContext(options, context.TenantContext);
        var control = await db.Accounts.SingleAsync(item => item.Id == controlAccountId);
        var balancing = await db.Accounts.SingleAsync(item => item.Id == balancingAccountId);
        var period = await db.FiscalPeriods.SingleAsync(item => item.CompanyId == CompanyId);
        var sequence = (await db.Journals.Select(item => (long?)item.JournalSequence).MaxAsync() ?? 0L) + 1L;
        var journalId = Guid.NewGuid();
        var controlIsDebit = kind == FinanceOpenItemKind.Receivable;
        var command = new FinanceJournalCommand(CompanyId, new DateOnly(2026, 1, 15), new DateOnly(2026, 1, 15), "SAR", 1m, null, null, null, "manual-ar.v1", "recognition", itemId, 1, null, reference, [
            new FinanceJournalLineCommand(control.Id, controlIsDebit ? 100m : 0m, controlIsDebit ? 0m : 100m, 100m, "SAR", null, reference),
            new FinanceJournalLineCommand(balancing.Id, controlIsDebit ? 0m : 100m, controlIsDebit ? 100m : 0m, 100m, "SAR", null, reference),
        ], journalId, reference, reference, FinanceJournalAmountAuthority.ManualTransactionCurrency, FinanceApprovalRequirement.NotRequired);
        var journal = new FinanceJournalEntity(context.TenantId, journalId, command, sequence, "SAR", context.ActorId, DateTimeOffset.UtcNow);
        journal.SetPeriod(period.FiscalYearId, period.Id);
        journal.SetStatus(FinanceJournalStatus.Posted, context.ActorId, DateTimeOffset.UtcNow);
        journal.Lines.Add(new FinanceJournalLineEntity(context.TenantId, Guid.NewGuid(), journal.Id, 1, control, command.Lines[0], null, controlIsDebit ? 100m : 0m, controlIsDebit ? 0m : 100m, FinanceJournalAmountAuthority.ManualTransactionCurrency));
        journal.Lines.Add(new FinanceJournalLineEntity(context.TenantId, Guid.NewGuid(), journal.Id, 2, balancing, command.Lines[1], null, controlIsDebit ? 0m : 100m, controlIsDebit ? 100m : 0m, FinanceJournalAmountAuthority.ManualTransactionCurrency));
        var item = new FinanceOpenItemEntity(context.TenantId, itemId, kind, CompanyId, kind == FinanceOpenItemKind.Payable ? partyId : null, kind == FinanceOpenItemKind.Receivable ? partyId : null, "manual-ar.v1", itemId, 1, itemId, 1, reference, new DateOnly(2026, 1, 15), new DateOnly(2026, 2, 15), "SAR", 100m, "SAR", 100m, 1m, null, null, null, null, null, null, reference);
        item.SetRecognition(FinanceOpenItemRecognitionState.Recognized, journal.Id);
        db.Journals.Add(journal);
        db.OpenItems.Add(item);
        await db.SaveChangesAsync();
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

    private sealed class ActiveSupplierReader(Guid supplierId, MasterDataLifecycleState lifecycleState = MasterDataLifecycleState.Active) : ISupplierPersistence
    {
        public Task<IReadOnlyList<SupplierRecord>> ListSuppliersAsync(TenantContext tenantContext, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<SupplierRecord>>([Reference(tenantContext)]);
        public Task<SupplierRecord?> FindSupplierAsync(TenantContext tenantContext, Guid id, CancellationToken cancellationToken = default) => Task.FromResult<SupplierRecord?>(id == supplierId ? Reference(tenantContext) : null);
        public Task<MasterDataPersistenceResult<SupplierRecord>> CreateSupplierAsync(TenantContext tenantContext, Guid id, CreateSupplierCommand command, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => Unavailable<MasterDataPersistenceResult<SupplierRecord>>();
        public Task<MasterDataPersistenceResult<SupplierRecord>> EditSupplierAsync(TenantContext tenantContext, EditSupplierCommand command, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => Unavailable<MasterDataPersistenceResult<SupplierRecord>>();
        public Task<MasterDataPersistenceResult<SupplierRecord>> SetSupplierLifecycleAsync(TenantContext tenantContext, Guid id, MasterDataLifecycleState lifecycleState, byte[] expectedVersion, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => Unavailable<MasterDataPersistenceResult<SupplierRecord>>();
        public Task<MasterDataPersistenceResult<MasterDataAuditRecord>> AppendAuditAsync(TenantContext tenantContext, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => Unavailable<MasterDataPersistenceResult<MasterDataAuditRecord>>();
        public Task<IReadOnlyList<MasterDataAuditRecord>> ReadAuditHistoryAsync(TenantContext tenantContext, Guid id, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<MasterDataAuditRecord>>([]);
        private SupplierRecord Reference(TenantContext context) => new(supplierId, context.TenantId, "SUP-1", new LocalizedName("Supplier"), null, null, lifecycleState, [1], []);
        private static Task<T> Unavailable<T>() => Task.FromException<T>(new InvalidOperationException("test persistence unavailable"));
    }

    private sealed class StaticSupplierInvoiceSourceProvider(FinanceSupplierInvoiceSourceRecord source) : IFinanceSupplierInvoiceSourceProvider
    {
        public Task<FinanceSupplierInvoiceSourceRecord?> FindAsync(FinanceRequestContext context, Guid sourceEvidenceId, CancellationToken cancellationToken = default) => Task.FromResult<FinanceSupplierInvoiceSourceRecord?>(source.SourceEvidenceId == sourceEvidenceId ? source : null);
        public Task<IReadOnlyList<FinanceSupplierInvoiceSourceRecord>> ListAsync(FinanceRequestContext context, Guid? companyId = null, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<FinanceSupplierInvoiceSourceRecord>>(companyId is null || companyId == source.CompanyId ? [source] : []);
    }
}
