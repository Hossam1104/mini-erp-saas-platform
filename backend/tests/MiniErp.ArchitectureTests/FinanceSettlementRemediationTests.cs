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
using MiniErp.Contracts.Modules.Procurement;
using MiniErp.App.Modules.Procurement;
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
    public void Payable_settlement_above_historical_carrying_value_is_realized_loss()
    {
        var result = FinanceSettlementPersistence.ResolveRealizedFx(FinanceOpenItemKind.Payable, 100m, 110m);

        Assert.Equal(10m, result.Difference);
        Assert.Equal("Loss", result.Direction);
    }

    [Fact]
    public void Payable_settlement_below_historical_carrying_value_is_realized_gain()
    {
        var result = FinanceSettlementPersistence.ResolveRealizedFx(FinanceOpenItemKind.Payable, 100m, 90m);

        Assert.Equal(-10m, result.Difference);
        Assert.Equal("Gain", result.Direction);
    }

    [Fact]
    public void Receivable_receipt_above_historical_carrying_value_is_realized_gain()
    {
        var result = FinanceSettlementPersistence.ResolveRealizedFx(FinanceOpenItemKind.Receivable, 100m, 110m);

        Assert.Equal(10m, result.Difference);
        Assert.Equal("Gain", result.Direction);
    }

    [Fact]
    public void Receivable_receipt_below_historical_carrying_value_is_realized_loss()
    {
        var result = FinanceSettlementPersistence.ResolveRealizedFx(FinanceOpenItemKind.Receivable, 100m, 90m);

        Assert.Equal(-10m, result.Difference);
        Assert.Equal("Loss", result.Direction);
    }

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
    public async Task Sales_invoice_posts_gross_ar_with_immutable_tax_evidence_and_configured_tax_liability()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder().UseSqlite(connection).Options;
        await EnsureCreatedAsync(options);
        var context = Context("tenant.finance.sales-invoice");
        await using (var policyDb = new FinanceDbContext(options, context.TenantContext))
        {
            policyDb.MonetaryPolicies.Add(new FinanceMonetaryPolicyEntity(context.TenantId, new FinanceMonetaryPolicyCommand(CompanyId, null, 2, "ToEven", false, new DateOnly(2026, 1, 1), null, Guid.NewGuid(), "sales-policy", "sales-policy"), "SAR", null, 1));
            await policyDb.SaveChangesAsync();
        }
        var ar = await CreateAccountAsync(options, context, "SALES-INVOICE-AR");
        var revenue = await CreateAccountAsync(options, context, "SALES-INVOICE-REVENUE");
        var taxLiability = await CreateAccountAsync(options, context, "SALES-INVOICE-TAX");
        await OpenFullYearAndRulesAsync(options, context,
        [
            ("sales-invoice.v1", "recognition", ar.Id, revenue.Id, new DateOnly(2026, 1, 1), (DateOnly?)null),
            ("finance-tax.v1", "output", revenue.Id, taxLiability.Id, new DateOnly(2026, 1, 1), (DateOnly?)null)
        ]);

        var paymentTerm = new FinancePaymentTermSnapshotRecord(Guid.NewGuid(), "NET30", "Net 30", null, 4, Guid.NewGuid(), new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31));
        var taxId = Guid.NewGuid();
        var secondTaxId = Guid.NewGuid();
        var line = new FinanceSalesInvoiceLine(Guid.NewGuid(), 1m, 100m, 15m, 115m, taxId, "VAT15", Guid.NewGuid(), 3, new DateOnly(2026, 1, 1), null, 15m, 100m, "VAT15;v3");
        var secondLine = new FinanceSalesInvoiceLine(Guid.NewGuid(), 2m, 200m, 20m, 220m, secondTaxId, "VAT10", Guid.NewGuid(), 1, new DateOnly(2026, 1, 1), null, 10m, 200m, "VAT10;v1");
        var command = new FinanceSalesInvoiceCommand(CompanyId, CustomerId, Guid.NewGuid(), 1, Guid.NewGuid(), new DateOnly(2026, 1, 15), paymentTerm.Id, "SAR", 335m, null, null, null, null, "immutable-sales-source", "SI-137", "sales-invoice-137", "sales-invoice-137", 300m, 35m, [line, secondLine], paymentTerm);
        var persistence = CreatePersistence(options, customers: new ActiveCustomerReader());

        var result = await persistence.CreateSalesInvoiceAsync(context, command);

        Assert.True(result.Succeeded, result.Code);
        Assert.Equal(335m, result.Value!.OriginalAmount);
        Assert.Equal(paymentTerm.Id, result.Value.PaymentTerm!.Id);
        await using var db = new FinanceDbContext(options, context.TenantContext);
        var effects = await db.TaxAccountingEffects.Where(item => item.OpenItemId == result.Value.Id).ToListAsync();
        Assert.Equal(2, effects.Count);
        Assert.Contains(effects, item => item.TaxId == taxId && item.TaxAmount == 15m);
        Assert.Contains(effects, item => item.TaxId == secondTaxId && item.TaxAmount == 20m);
        Assert.Contains(await db.AuditEvents.ToListAsync(), audit => audit.OperationId == "finance.tax-accounting.post" && audit.ResourceId == command.InvoiceRequestId && audit.Result == "Succeeded");
        var journals = await db.Journals.Include(item => item.Lines).Where(item => item.SourceEvidenceId == command.InvoiceRequestId || item.SourceContract == "finance-tax.v1").ToListAsync();
        Assert.Equal(3, journals.Count);
        var taxJournals = journals.Where(item => item.SourceContract == "finance-tax.v1").ToArray();
        Assert.Equal(2, taxJournals.Length);
        Assert.Contains(taxJournals, journal => journal.Lines.Any(item => item.AccountId == revenue.Id && item.FunctionalDebit == 15m) && journal.Lines.Any(item => item.AccountId == taxLiability.Id && item.FunctionalCredit == 15m));
        Assert.Contains(taxJournals, journal => journal.Lines.Any(item => item.AccountId == revenue.Id && item.FunctionalDebit == 20m) && journal.Lines.Any(item => item.AccountId == taxLiability.Id && item.FunctionalCredit == 20m));
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
        var aging = await persistence.GetAgingAsync(context, new FinanceAgingQuery(CompanyId, DateOnly.FromDateTime(DateTime.UtcNow), FinanceOpenItemKind.Payable));
        Assert.Equal(100m, Assert.Single(aging).OutstandingAmount);

        await SeedRecognizedItemAndJournalAsync(options, context, FinanceOpenItemKind.Receivable, Guid.NewGuid(), CustomerId, arControl.Id, revenue.Id, "hold3-ar");
        var bothDirections = await persistence.GetReconciliationAsync(context, CompanyId);
        Assert.Equal(FinanceReconciliationStatus.Reconciled, Assert.Single(bothDirections, row => row.Kind == FinanceOpenItemKind.Payable).Status);
        Assert.Equal(FinanceReconciliationStatus.Reconciled, Assert.Single(bothDirections, row => row.Kind == FinanceOpenItemKind.Receivable).Status);
        Assert.DoesNotContain(bothDirections, row => row.Status == FinanceReconciliationStatus.PendingMapping);
    }

    [Fact]
    public async Task GetReconciliationAsync_asOf_reflects_ap_allocation_and_reversal_history_without_rewriting_the_past()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder().UseSqlite(connection).Options;
        await EnsureCreatedAsync(options);
        var context = Context("tenant.finance.reconciliation");
        var expense = await CreateAccountAsync(options, context, "ASOF-AP-EXPENSE");
        var apControl = await CreateAccountAsync(options, context, "ASOF-AP-CONTROL");
        var cashAccount = await CreateAccountAsync(options, context, "ASOF-AP-CASH");
        await OpenFullYearAndRulesAsync(options, context, [
            ("procurement-supplier-invoice.v1", "recognition", expense.Id, apControl.Id, new DateOnly(2026, 1, 1), null),
            ("supplier-payment.v1", "on-account", apControl.Id, cashAccount.Id, new DateOnly(2026, 1, 1), null),
            ("supplier-payment.v1", "allocation", apControl.Id, cashAccount.Id, new DateOnly(2026, 1, 1), null),
        ]);
        var source = SourceForTest();
        var persistence = CreatePersistence(options, new RequiredApprovalPolicy(), suppliers: new ActiveSupplierReader(SupplierId), sourceProvider: new StaticSupplierInvoiceSourceProvider(source));

        var recognized = await persistence.RecognizeSupplierInvoiceAsync(context, new FinanceSupplierInvoiceRecognitionCommand(source.SourceEvidenceId, "asof-ap-recognize", "asof-ap-recognize"));
        Assert.True(recognized.Succeeded, recognized.Code);
        var method = await persistence.CreatePaymentMethodAsync(context, PaymentMethodCommand("ASOF-AP-METHOD", Guid.NewGuid(), true, FinancePaymentMethodDirection.Payment));
        var cash = await persistence.CreateCashAccountAsync(context, CashAccountCommand("ASOF-AP-CASH", cashAccount.Id, Guid.NewGuid()));
        Assert.True(method.Succeeded, method.Code);
        Assert.True(cash.Succeeded, cash.Code);
        var created = await persistence.CreateSettlementDocumentAsync(context, new FinanceSettlementDocumentCommand(FinancePaymentMethodDirection.Payment, CompanyId, SupplierId, null, cash.Value!.Id, method.Value!.Id, new DateOnly(2026, 1, 15), "SAR", 100m, null, null, null, null, null, "ASOF-AP-PAY", "asof ap payment", Guid.NewGuid(), "asof-ap-payment-create", "asof-ap-payment-create"));
        Assert.True(created.Succeeded, created.Code);
        var submitted = await persistence.TransitionSettlementDocumentAsync(context, new FinanceSettlementActionCommand(created.Value!.Id, created.Value.Version, null, "asof-ap-payment-submit", "asof-ap-payment-submit", FinancePaymentMethodDirection.Payment), FinanceSettlementDocumentStatus.Submitted);
        Assert.True(submitted.Succeeded, submitted.Code);
        var approved = await persistence.TransitionSettlementDocumentAsync(Context("tenant.finance.settlement.approve", ApproverId), new FinanceSettlementActionCommand(submitted.Value!.Id, submitted.Value.Version, null, "asof-ap-payment-approve", "asof-ap-payment-approve", FinancePaymentMethodDirection.Payment), FinanceSettlementDocumentStatus.Approved);
        Assert.True(approved.Succeeded, approved.Code);
        var posted = await persistence.PostSettlementDocumentAsync(Context("tenant.finance.settlement.post", ApproverId), new FinanceSettlementActionCommand(approved.Value!.Id, approved.Value.Version, null, "asof-ap-payment-post", "asof-ap-payment-post", FinancePaymentMethodDirection.Payment));
        Assert.True(posted.Succeeded, posted.Code);

        var allocated = await persistence.CreateAllocationAsync(context, new FinanceAllocationCommand(posted.Value!.Id, recognized.Value!.Id, 40m, new DateOnly(2026, 2, 1), "asof allocation", Guid.NewGuid(), "asof-ap-allocation-create", "asof-ap-allocation-create"));
        Assert.True(allocated.Succeeded, allocated.Code);

        var beforeAllocation = await persistence.GetReconciliationAsync(context, CompanyId, new DateOnly(2026, 1, 20));
        var beforeRow = Assert.Single(beforeAllocation, row => row.Kind == FinanceOpenItemKind.Payable);
        Assert.Equal(100m, beforeRow.SubledgerAmount);
        Assert.Equal(100m, beforeRow.PostedJournalAmount);
        Assert.Equal(FinanceReconciliationStatus.Reconciled, beforeRow.Status);

        var afterAllocation = await persistence.GetReconciliationAsync(context, CompanyId, new DateOnly(2026, 2, 5));
        var afterRow = Assert.Single(afterAllocation, row => row.Kind == FinanceOpenItemKind.Payable);
        Assert.Equal(60m, afterRow.SubledgerAmount);
        Assert.Equal(60m, afterRow.PostedJournalAmount);
        Assert.Equal(FinanceReconciliationStatus.Reconciled, afterRow.Status);

        var reversed = await persistence.ReverseAllocationAsync(context, new FinanceAllocationReversalCommand(allocated.Value!.Id, allocated.Value.Version, "restore AP outstanding for asOf proof", Guid.NewGuid(), "asof-ap-allocation-reverse", "asof-ap-allocation-reverse"));
        Assert.True(reversed.Succeeded, reversed.Code);

        var stillBeforeReversal = await persistence.GetReconciliationAsync(context, CompanyId, new DateOnly(2026, 2, 5));
        var stillBeforeRow = Assert.Single(stillBeforeReversal, row => row.Kind == FinanceOpenItemKind.Payable);
        Assert.Equal(60m, stillBeforeRow.SubledgerAmount);
        Assert.Equal(60m, stillBeforeRow.PostedJournalAmount);
        Assert.Equal(FinanceReconciliationStatus.Reconciled, stillBeforeRow.Status);

        var afterReversal = await persistence.GetReconciliationAsync(context, CompanyId, DateOnly.FromDateTime(DateTime.UtcNow));
        var afterReversalRow = Assert.Single(afterReversal, row => row.Kind == FinanceOpenItemKind.Payable);
        Assert.Equal(100m, afterReversalRow.SubledgerAmount);
        Assert.Equal(100m, afterReversalRow.PostedJournalAmount);
        Assert.Equal(FinanceReconciliationStatus.Reconciled, afterReversalRow.Status);
    }

    [Fact]
    public async Task GetReconciliationAsync_asOf_reflects_ar_allocation_and_reversal_history_without_rewriting_the_past()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder().UseSqlite(connection).Options;
        await EnsureCreatedAsync(options);
        var context = Context("tenant.finance.reconciliation");
        var arControl = await CreateAccountAsync(options, context, "ASOF-AR-CONTROL");
        var revenue = await CreateAccountAsync(options, context, "ASOF-AR-REVENUE");
        var cashAccount = await CreateAccountAsync(options, context, "ASOF-AR-CASH");
        await OpenFullYearAndRulesAsync(options, context, [
            ("customer-receipt.v1", "on-account", cashAccount.Id, arControl.Id, new DateOnly(2026, 1, 1), null),
            ("customer-receipt.v1", "allocation", cashAccount.Id, arControl.Id, new DateOnly(2026, 1, 1), null),
        ]);
        var itemId = Guid.NewGuid();
        await SeedRecognizedItemAndJournalAsync(options, context, FinanceOpenItemKind.Receivable, itemId, CustomerId, arControl.Id, revenue.Id, "ASOF-AR-1");
        var persistence = CreatePersistence(options, new RequiredApprovalPolicy(), customers: new ActiveCustomerReader());

        var method = await persistence.CreatePaymentMethodAsync(context, PaymentMethodCommand("ASOF-AR-METHOD", Guid.NewGuid(), true, FinancePaymentMethodDirection.Receipt));
        var cash = await persistence.CreateCashAccountAsync(context, CashAccountCommand("ASOF-AR-CASH", cashAccount.Id, Guid.NewGuid()));
        Assert.True(method.Succeeded, method.Code);
        Assert.True(cash.Succeeded, cash.Code);
        var created = await persistence.CreateSettlementDocumentAsync(context, new FinanceSettlementDocumentCommand(FinancePaymentMethodDirection.Receipt, CompanyId, null, CustomerId, cash.Value!.Id, method.Value!.Id, new DateOnly(2026, 1, 15), "SAR", 100m, null, null, null, null, null, "ASOF-AR-REC", "asof ar receipt", Guid.NewGuid(), "asof-ar-receipt-create", "asof-ar-receipt-create"));
        Assert.True(created.Succeeded, created.Code);
        var submitted = await persistence.TransitionSettlementDocumentAsync(context, new FinanceSettlementActionCommand(created.Value!.Id, created.Value.Version, null, "asof-ar-receipt-submit", "asof-ar-receipt-submit", FinancePaymentMethodDirection.Receipt), FinanceSettlementDocumentStatus.Submitted);
        Assert.True(submitted.Succeeded, submitted.Code);
        var approved = await persistence.TransitionSettlementDocumentAsync(Context("tenant.finance.settlement.approve", ApproverId), new FinanceSettlementActionCommand(submitted.Value!.Id, submitted.Value.Version, null, "asof-ar-receipt-approve", "asof-ar-receipt-approve", FinancePaymentMethodDirection.Receipt), FinanceSettlementDocumentStatus.Approved);
        Assert.True(approved.Succeeded, approved.Code);
        var posted = await persistence.PostSettlementDocumentAsync(Context("tenant.finance.settlement.post", ApproverId), new FinanceSettlementActionCommand(approved.Value!.Id, approved.Value.Version, null, "asof-ar-receipt-post", "asof-ar-receipt-post", FinancePaymentMethodDirection.Receipt));
        Assert.True(posted.Succeeded, posted.Code);

        var allocated = await persistence.CreateAllocationAsync(context, new FinanceAllocationCommand(posted.Value!.Id, itemId, 40m, new DateOnly(2026, 2, 1), "asof ar allocation", Guid.NewGuid(), "asof-ar-allocation-create", "asof-ar-allocation-create"));
        Assert.True(allocated.Succeeded, allocated.Code);

        var beforeAllocation = await persistence.GetReconciliationAsync(context, CompanyId, new DateOnly(2026, 1, 20));
        var beforeRow = Assert.Single(beforeAllocation, row => row.Kind == FinanceOpenItemKind.Receivable);
        Assert.Equal(100m, beforeRow.SubledgerAmount);
        Assert.Equal(100m, beforeRow.PostedJournalAmount);
        Assert.Equal(FinanceReconciliationStatus.Reconciled, beforeRow.Status);

        var afterAllocation = await persistence.GetReconciliationAsync(context, CompanyId, new DateOnly(2026, 2, 5));
        var afterRow = Assert.Single(afterAllocation, row => row.Kind == FinanceOpenItemKind.Receivable);
        Assert.Equal(60m, afterRow.SubledgerAmount);
        Assert.Equal(60m, afterRow.PostedJournalAmount);
        Assert.Equal(FinanceReconciliationStatus.Reconciled, afterRow.Status);

        var reversed = await persistence.ReverseAllocationAsync(context, new FinanceAllocationReversalCommand(allocated.Value!.Id, allocated.Value.Version, "restore AR outstanding for asOf proof", Guid.NewGuid(), "asof-ar-allocation-reverse", "asof-ar-allocation-reverse"));
        Assert.True(reversed.Succeeded, reversed.Code);

        var stillBeforeReversal = await persistence.GetReconciliationAsync(context, CompanyId, new DateOnly(2026, 2, 5));
        var stillBeforeRow = Assert.Single(stillBeforeReversal, row => row.Kind == FinanceOpenItemKind.Receivable);
        Assert.Equal(60m, stillBeforeRow.SubledgerAmount);
        Assert.Equal(60m, stillBeforeRow.PostedJournalAmount);
        Assert.Equal(FinanceReconciliationStatus.Reconciled, stillBeforeRow.Status);

        var afterReversal = await persistence.GetReconciliationAsync(context, CompanyId, DateOnly.FromDateTime(DateTime.UtcNow));
        var afterReversalRow = Assert.Single(afterReversal, row => row.Kind == FinanceOpenItemKind.Receivable);
        Assert.Equal(100m, afterReversalRow.SubledgerAmount);
        Assert.Equal(100m, afterReversalRow.PostedJournalAmount);
        Assert.Equal(FinanceReconciliationStatus.Reconciled, afterReversalRow.Status);
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

    [Fact]
    public async Task Procurement_source_ready_returns_active_supplier_with_trusted_invoice_date()
    {
        var fixture = ProcurementProviderFixture.Create();

        var source = await fixture.Provider.FindAsync(fixture.Context, fixture.MatchId);
        Assert.NotNull(source);
        Assert.Equal(fixture.SupplierId, source!.SupplierId);
        Assert.Equal(fixture.CompanyId, source.CompanyId);
        Assert.Equal(fixture.InvoiceDate, source.DocumentDate);
        Assert.Equal(fixture.TermVersion, source.PaymentTerm!.VersionNumber);
        Assert.Equal(fixture.DueDate, source.DueDate);
        Assert.Equal(fixture.MatchId, source.MatchEvidenceId);
        Assert.Equal(fixture.MatchId, source.SourceEvidenceId);

        var listed = await fixture.Provider.ListAsync(fixture.Context);
        var listedSource = Assert.Single(listed);
        Assert.Equal(fixture.MatchId, listedSource.MatchEvidenceId);
        Assert.Equal(fixture.SupplierId, listedSource.SupplierId);
    }

    [Fact]
    public async Task Procurement_source_ready_excludes_missing_inactive_and_cross_tenant_suppliers()
    {
        var fixtures = new[]
        {
            ProcurementProviderFixture.Create(includeSupplier: false),
            ProcurementProviderFixture.Create(supplier: ProcurementProviderFixture.Supplier(lifecycle: MasterDataLifecycleState.Inactive)),
            ProcurementProviderFixture.Create(supplier: ProcurementProviderFixture.Supplier(tenantId: Guid.NewGuid()))
        };

        foreach (var fixture in fixtures)
        {
            Assert.Null(await fixture.Provider.FindAsync(fixture.Context, fixture.MatchId));
            Assert.Empty(await fixture.Provider.ListAsync(fixture.Context));
        }
    }

    [Fact]
    public async Task Procurement_source_ready_never_uses_handoff_created_at_as_invoice_date()
    {
        var fixture = ProcurementProviderFixture.Create(includeInvoiceDate: false);

        Assert.Null(await fixture.Provider.FindAsync(fixture.Context, fixture.MatchId));
        Assert.Empty(await fixture.Provider.ListAsync(fixture.Context));
    }

    [Fact]
    public async Task Procurement_source_ready_fails_closed_for_unsupported_payment_term_base_date()
    {
        var fixture = ProcurementProviderFixture.Create(baseDateRule: PaymentTermBaseDateRule.ReceiptDate);

        Assert.Null(await fixture.Provider.FindAsync(fixture.Context, fixture.MatchId));
        Assert.Empty(await fixture.Provider.ListAsync(fixture.Context));
    }

    [Fact]
    public async Task Historical_recognition_uses_rule_effective_on_document_date_without_reinterpreting_prior_item()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder().UseSqlite(connection).Options;
        await EnsureCreatedAsync(options);
        var context = Context("tenant.finance.recognition.rule-history");
        var expense = await CreateAccountAsync(options, context, "HISTORY-EXPENSE");
        var controlA = await CreateAccountAsync(options, context, "HISTORY-AP-A");
        var controlB = await CreateAccountAsync(options, context, "HISTORY-AP-B");

        await OpenFullYearAndRulesAsync(options, context, [
            ("procurement-supplier-invoice.v1", "recognition", expense.Id, controlA.Id, new DateOnly(2026, 1, 1), new DateOnly(2026, 3, 31)),
            ("procurement-supplier-invoice.v1", "recognition", expense.Id, controlB.Id, new DateOnly(2026, 4, 1), null)
        ]);

        var sourceA = SourceForTest() with
        {
            SourceDocumentId = Guid.NewGuid(),
            SourceEvidenceId = Guid.NewGuid(),
            MatchEvidenceId = Guid.NewGuid(),
            Reference = "HISTORY-AP-A-ITEM",
            DocumentDate = new DateOnly(2026, 2, 15),
            DueDate = new DateOnly(2026, 3, 17)
        };
        var sourceB = SourceForTest() with
        {
            SourceDocumentId = Guid.NewGuid(),
            SourceEvidenceId = Guid.NewGuid(),
            MatchEvidenceId = Guid.NewGuid(),
            Reference = "HISTORY-AP-B-ITEM",
            DocumentDate = new DateOnly(2026, 5, 15),
            DueDate = new DateOnly(2026, 6, 14)
        };
        var persistence = CreatePersistence(
            options,
            suppliers: new ActiveSupplierReader(SupplierId),
            sourceProvider: new StaticSupplierInvoiceSourceProvider([sourceA, sourceB]));

        var recognizedA = await persistence.RecognizeSupplierInvoiceAsync(
            context,
            new FinanceSupplierInvoiceRecognitionCommand(sourceA.SourceEvidenceId, "history-recognize-a", "history-recognize-a"));
        Assert.True(recognizedA.Succeeded, recognizedA.Code);
        Assert.NotNull(recognizedA.Value!.RecognitionJournalId);

        var recognizedB = await persistence.RecognizeSupplierInvoiceAsync(
            context,
            new FinanceSupplierInvoiceRecognitionCommand(sourceB.SourceEvidenceId, "history-recognize-b", "history-recognize-b"));
        Assert.True(recognizedB.Succeeded, recognizedB.Code);
        Assert.NotNull(recognizedB.Value!.RecognitionJournalId);

        await using (var db = new FinanceDbContext(options, context.TenantContext))
        {
            var openA = await db.OpenItems.SingleAsync(item => item.SourceEvidenceId == sourceA.SourceEvidenceId);
            var openB = await db.OpenItems.SingleAsync(item => item.SourceEvidenceId == sourceB.SourceEvidenceId);
            Assert.Equal(recognizedA.Value.RecognitionJournalId, openA.RecognitionJournalId);
            Assert.Equal(recognizedB.Value.RecognitionJournalId, openB.RecognitionJournalId);

            var journalA = await db.Journals.Include(item => item.Lines).SingleAsync(item => item.Id == openA.RecognitionJournalId);
            var journalB = await db.Journals.Include(item => item.Lines).SingleAsync(item => item.Id == openB.RecognitionJournalId);
            var controlLineA = Assert.Single(journalA.Lines, line => line.AccountId == controlA.Id);
            var controlLineB = Assert.Single(journalB.Lines, line => line.AccountId == controlB.Id);
            Assert.Equal(100m, controlLineA.Credit);
            Assert.Equal(100m, controlLineB.Credit);
            Assert.DoesNotContain(journalA.Lines, line => line.AccountId == controlB.Id);
            Assert.DoesNotContain(journalB.Lines, line => line.AccountId == controlA.Id);
        }

        var reconciliation = await persistence.GetReconciliationAsync(context, CompanyId);
        var payable = Assert.Single(reconciliation, row => row.Kind == FinanceOpenItemKind.Payable);
        Assert.Equal(FinanceReconciliationStatus.Reconciled, payable.Status);
        Assert.DoesNotContain(reconciliation, row => row.Status == FinanceReconciliationStatus.PendingMapping);
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

    private sealed class StaticSupplierInvoiceSourceProvider(IReadOnlyList<FinanceSupplierInvoiceSourceRecord> sources) : IFinanceSupplierInvoiceSourceProvider
    {
        public Task<FinanceSupplierInvoiceSourceRecord?> FindAsync(FinanceRequestContext context, Guid sourceEvidenceId, CancellationToken cancellationToken = default) =>
            Task.FromResult(sources.SingleOrDefault(source => source.SourceEvidenceId == sourceEvidenceId));

        public Task<IReadOnlyList<FinanceSupplierInvoiceSourceRecord>> ListAsync(FinanceRequestContext context, Guid? companyId = null, CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<FinanceSupplierInvoiceSourceRecord>>(sources.Where(source => companyId is null || source.CompanyId == companyId).ToArray());

        public StaticSupplierInvoiceSourceProvider(FinanceSupplierInvoiceSourceRecord source) : this([source]) { }
    }

    private sealed class ProcurementProviderFixture
    {
        public Guid SupplierId { get; } = Guid.Parse("99999999-9999-9999-9999-999999999999");
        public Guid CompanyId { get; } = FinanceSettlementRemediationTests.CompanyId;
        public Guid MatchId { get; }
        public DateOnly InvoiceDate { get; }
        public DateOnly DueDate { get; }
        public int TermVersion { get; } = 7;
        public FinanceRequestContext Context { get; }
        public ProcurementFinanceSupplierInvoiceSourceProvider Provider { get; }

        private ProcurementProviderFixture(
            SupplierRecord? supplier,
            DateOnly? invoiceDate,
            PaymentTermBaseDateRule baseDateRule)
        {
            var tenantContext = TenantContext.ForOrdinaryMembership(new TenantId(TenantId), new MembershipReference(Guid.NewGuid()), correlationId: new CorrelationId("provider-test"));
            var foundation = FoundationRequestContext.ForTenant(ActorId, Guid.NewGuid(), tenantContext, "tenant.finance.ap.source");
            Assert.True(FinanceRequestContext.TryCreate(foundation, out var context));
            Context = context!;
            InvoiceDate = invoiceDate ?? new DateOnly(2026, 2, 15);
            DueDate = InvoiceDate.AddDays(30);
            MatchId = Guid.NewGuid();
            var handoffId = Guid.NewGuid();
            var purchaseOrderId = Guid.NewGuid();
            var termId = Guid.NewGuid();
            var scope = new PurchaseRequestScope(TenantId, CompanyId, null);
            var now = new DateTimeOffset(2026, 1, 1, 12, 0, 0, TimeSpan.Zero);
            var evidence = new PurchaseInvoiceDeclaredEvidenceRecord(
                Guid.NewGuid(),
                3,
                "SUPPLIER-INV-7",
                invoiceDate,
                "SAR",
                100m,
                null,
                null,
                100m,
                now,
                ActorId,
                []);
            var handoff = new PurchaseInvoiceHandoffRecord(
                handoffId,
                TenantId,
                scope,
                purchaseOrderId,
                ActorId,
                PurchaseInvoiceHandoffStatus.Recorded,
                SupplierId,
                "SUP-7",
                "Authoritative Supplier",
                "SAR",
                "SUPPLIER-INV-7",
                invoiceDate,
                "provider fixture",
                now,
                now,
                null,
                null,
                [new PurchaseInvoiceHandoffLineRecord(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "SKU-7", "Item 7", "EA", 1m, 100m, null, null, 100m)],
                [],
                [1],
                evidence);
            var purchaseOrder = new PurchaseOrderRecord(
                purchaseOrderId,
                TenantId,
                ActorId,
                scope,
                PurchaseOrderStatus.Issued,
                new PurchaseOrderSourceResponse(
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    Guid.NewGuid(),
                    "PR-7",
                    "provider fixture",
                    "QUOT-7",
                    new PurchaseOrderSupplierResponse(SupplierId, "SUP-7", "Authoritative Supplier"),
                    new PurchaseOrderCurrencyResponse(Guid.NewGuid(), "SAR", "Saudi Riyal"),
                    new PurchaseOrderPaymentTermResponse(termId, "NET7", "Net 7", TermVersion),
                    "selected",
                    now),
                null,
                now,
                now,
                now,
                now,
                now,
                null,
                null,
                null,
                null,
                null,
                [],
                [],
                [1]);
            var match = new PurchaseInvoiceMatchRecord(
                MatchId,
                TenantId,
                scope,
                handoffId,
                purchaseOrderId,
                PurchaseInvoiceMatchLifecycle.Current,
                PurchaseInvoiceMatchResult.ExactMatch,
                now,
                ActorId,
                null,
                null,
                null,
                "provider-match",
                [1],
                [1],
                evidence.Id,
                evidence.VersionNumber,
                PurchaseInvoiceMatchingToleranceDefinition.ExactSafe(now),
                null,
                null,
                [],
                "provider snapshot",
                [1]);
            var term = new MasterDataPaymentTermRecord(
                termId,
                new TenantId(TenantId),
                "NET7",
                new LocalizedName("Net 7"),
                MasterDataLifecycleState.Active,
                TermVersion,
                [new MasterDataPaymentTermVersionRecord(
                    Guid.NewGuid(),
                    TermVersion,
                    new DateOnly(2026, 1, 1),
                    null,
                    baseDateRule,
                    PaymentTermScheduleMode.SingleDueDate,
                    new MasterDataPaymentTermOffset(30, 0),
                    [],
                    MasterDataEarlySettlementDiscount.Disabled(),
                    "NET7",
                    new LocalizedName("Net 7"))],
                [1]);
            Provider = new ProcurementFinanceSupplierInvoiceSourceProvider(
                new ProviderHandoffPersistence(handoff),
                new ProviderMatchPersistence(match),
                new ConfiguredFinanceCompanyProvider([new FinanceCompanyOption(TenantId, CompanyId, "Provider Company", "SAR")]),
                new ProviderPurchaseOrderPersistence(purchaseOrder),
                new ProviderPaymentTermPersistence(term),
                new ProviderSupplierPersistence(supplier));
        }

        public static ProcurementProviderFixture Create(
            SupplierRecord? supplier = null,
            DateOnly? invoiceDate = null,
            PaymentTermBaseDateRule baseDateRule = PaymentTermBaseDateRule.InvoiceDate,
            bool includeSupplier = true,
            bool includeInvoiceDate = true) =>
            new(includeSupplier ? supplier ?? Supplier() : null, includeInvoiceDate ? invoiceDate ?? new DateOnly(2026, 2, 15) : null, baseDateRule);

        public static SupplierRecord Supplier(
            Guid? tenantId = null,
            MasterDataLifecycleState lifecycle = MasterDataLifecycleState.Active) =>
            new(Guid.Parse("99999999-9999-9999-9999-999999999999"), new TenantId(tenantId ?? TenantId), "SUP-7", new LocalizedName("Authoritative Supplier"), null, null, lifecycle, [1], []);

        private sealed class ProviderHandoffPersistence(PurchaseInvoiceHandoffRecord record) : IPurchaseInvoiceHandoffPersistence
        {
            public Task<IReadOnlyList<PurchaseInvoiceHandoffListRecord>> ListAsync(TenantContext tenantContext, PurchaseInvoiceHandoffStatus? status, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<PurchaseInvoiceHandoffListRecord>>([]);
            public Task<IReadOnlyList<PurchaseInvoiceHandoffEligibleSourceRecord>> ListEligibleSourcesAsync(TenantContext tenantContext, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<PurchaseInvoiceHandoffEligibleSourceRecord>>([]);
            public Task<PurchaseInvoiceHandoffEligibleSourceRecord?> FindEligibleSourceAsync(TenantContext tenantContext, Guid purchaseOrderId, CancellationToken cancellationToken = default) => Task.FromResult<PurchaseInvoiceHandoffEligibleSourceRecord?>(null);
            public Task<PurchaseInvoiceHandoffReplayProbe> ProbeReplayAsync(TenantContext tenantContext, PurchaseInvoiceHandoffReplayQuery query, CancellationToken cancellationToken = default) => Task.FromResult(PurchaseInvoiceHandoffReplayProbe.NotFound);
            public Task<PurchaseInvoiceHandoffRecord?> FindAsync(TenantContext tenantContext, Guid purchaseInvoiceHandoffId, CancellationToken cancellationToken = default) => Task.FromResult<PurchaseInvoiceHandoffRecord?>(purchaseInvoiceHandoffId == record.Id ? record : null);
            public Task<PurchaseInvoiceHandoffPersistenceResult<PurchaseInvoiceHandoffRecord>> CreateAsync(TenantContext tenantContext, PurchaseInvoiceHandoffCreateCommand command, PurchaseInvoiceHandoffAuditEvidence evidence, CancellationToken cancellationToken = default) => Unavailable<PurchaseInvoiceHandoffPersistenceResult<PurchaseInvoiceHandoffRecord>>();
            public Task<PurchaseInvoiceHandoffPersistenceResult<PurchaseInvoiceHandoffRecord>> CaptureDeclaredEvidenceAsync(TenantContext tenantContext, PurchaseInvoiceDeclaredEvidenceCaptureCommand command, PurchaseInvoiceHandoffAuditEvidence evidence, CancellationToken cancellationToken = default) => Unavailable<PurchaseInvoiceHandoffPersistenceResult<PurchaseInvoiceHandoffRecord>>();
            public Task<PurchaseInvoiceHandoffPersistenceResult<PurchaseInvoiceHandoffRecord>> CancelAsync(TenantContext tenantContext, PurchaseInvoiceHandoffActionCommand command, PurchaseInvoiceHandoffAuditEvidence evidence, CancellationToken cancellationToken = default) => Unavailable<PurchaseInvoiceHandoffPersistenceResult<PurchaseInvoiceHandoffRecord>>();
            public Task<IReadOnlyList<PurchaseInvoiceHandoffHistoryRecord>> ReadHistoryAsync(TenantContext tenantContext, Guid purchaseInvoiceHandoffId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<PurchaseInvoiceHandoffHistoryRecord>>([]);
            public Task<IReadOnlyList<PurchaseInvoiceHandoffAuditRecord>> ReadAuditAsync(TenantContext tenantContext, Guid purchaseInvoiceHandoffId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<PurchaseInvoiceHandoffAuditRecord>>([]);
        }

        private sealed class ProviderMatchPersistence(PurchaseInvoiceMatchRecord record) : IPurchaseInvoiceMatchPersistence
        {
            public Task<IReadOnlyList<PurchaseInvoiceMatchListRecord>> ListAsync(TenantContext tenantContext, Guid? handoffId, PurchaseInvoiceMatchResult? result, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<PurchaseInvoiceMatchListRecord>>([new(record.Id, record.Scope, record.PurchaseInvoiceHandoffId, record.PurchaseOrderId, record.Lifecycle, record.Result, record.EvaluatedAt, record.ResolvedByActorId, record.Variances.Count, record.Version)]);
            public Task<PurchaseInvoiceMatchRecord?> FindAsync(TenantContext tenantContext, Guid matchEvaluationId, CancellationToken cancellationToken = default) => Task.FromResult<PurchaseInvoiceMatchRecord?>(matchEvaluationId == record.Id ? record : null);
            public Task<PurchaseInvoiceMatchRecord?> FindCurrentForHandoffAsync(TenantContext tenantContext, Guid handoffId, CancellationToken cancellationToken = default) => Task.FromResult<PurchaseInvoiceMatchRecord?>(handoffId == record.PurchaseInvoiceHandoffId ? record : null);
            public Task<PurchaseInvoiceMatchPersistenceResult<PurchaseInvoiceMatchRecord>> EvaluateAsync(TenantContext tenantContext, PurchaseInvoiceMatchEvaluateCommand command, PurchaseInvoiceMatchAuditEvidence evidence, CancellationToken cancellationToken = default) => Unavailable<PurchaseInvoiceMatchPersistenceResult<PurchaseInvoiceMatchRecord>>();
            public Task<PurchaseInvoiceMatchPersistenceResult<PurchaseInvoiceMatchRecord>> ResolveAsync(TenantContext tenantContext, PurchaseInvoiceMatchResolveCommand command, PurchaseInvoiceMatchAuditEvidence evidence, CancellationToken cancellationToken = default) => Unavailable<PurchaseInvoiceMatchPersistenceResult<PurchaseInvoiceMatchRecord>>();
            public Task<IReadOnlyList<PurchaseInvoiceMatchHistoryRecord>> ReadHistoryAsync(TenantContext tenantContext, Guid matchEvaluationId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<PurchaseInvoiceMatchHistoryRecord>>([]);
            public Task<IReadOnlyList<PurchaseInvoiceMatchAuditRecord>> ReadAuditAsync(TenantContext tenantContext, Guid matchEvaluationId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<PurchaseInvoiceMatchAuditRecord>>([]);
        }

        private sealed class ProviderPurchaseOrderPersistence(PurchaseOrderRecord record) : IPurchaseOrderPersistence
        {
            public Task<IReadOnlyList<PurchaseOrderListRecord>> ListAsync(TenantContext tenantContext, PurchaseOrderStatus? status, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<PurchaseOrderListRecord>>([]);
            public Task<bool> SourceDecisionConsumedAsync(TenantContext tenantContext, Guid sourceDecisionId, CancellationToken cancellationToken = default) => Task.FromResult(false);
            public Task<PurchaseOrderReplayProbe> ProbeReplayAsync(TenantContext tenantContext, PurchaseOrderReplayQuery query, CancellationToken cancellationToken = default) => Task.FromResult(PurchaseOrderReplayProbe.NotFound);
            public Task<PurchaseOrderRecord?> FindAsync(TenantContext tenantContext, Guid purchaseOrderId, CancellationToken cancellationToken = default) => Task.FromResult<PurchaseOrderRecord?>(purchaseOrderId == record.Id ? record : null);
            public Task<PurchaseOrderPersistenceResult<PurchaseOrderRecord>> CreateAsync(TenantContext tenantContext, PurchaseOrderCreateCommand command, PurchaseOrderAuditEvidence evidence, CancellationToken cancellationToken = default) => Unavailable<PurchaseOrderPersistenceResult<PurchaseOrderRecord>>();
            public Task<PurchaseOrderPersistenceResult<PurchaseOrderRecord>> EditAsync(TenantContext tenantContext, PurchaseOrderEditCommand command, PurchaseOrderAuditEvidence evidence, CancellationToken cancellationToken = default) => Unavailable<PurchaseOrderPersistenceResult<PurchaseOrderRecord>>();
            public Task<PurchaseOrderPersistenceResult<PurchaseOrderRecord>> SubmitAsync(TenantContext tenantContext, PurchaseOrderSubmitCommand command, PurchaseOrderAuditEvidence evidence, CancellationToken cancellationToken = default) => Unavailable<PurchaseOrderPersistenceResult<PurchaseOrderRecord>>();
            public Task<PurchaseOrderPersistenceResult<PurchaseOrderRecord>> ApproveAsync(TenantContext tenantContext, PurchaseOrderApprovalCommand command, PurchaseOrderAuditEvidence evidence, CancellationToken cancellationToken = default) => Unavailable<PurchaseOrderPersistenceResult<PurchaseOrderRecord>>();
            public Task<PurchaseOrderPersistenceResult<PurchaseOrderRecord>> RejectAsync(TenantContext tenantContext, PurchaseOrderActionCommand command, PurchaseOrderAuditEvidence evidence, CancellationToken cancellationToken = default) => Unavailable<PurchaseOrderPersistenceResult<PurchaseOrderRecord>>();
            public Task<PurchaseOrderPersistenceResult<PurchaseOrderRecord>> ReturnForChangeAsync(TenantContext tenantContext, PurchaseOrderActionCommand command, PurchaseOrderAuditEvidence evidence, CancellationToken cancellationToken = default) => Unavailable<PurchaseOrderPersistenceResult<PurchaseOrderRecord>>();
            public Task<PurchaseOrderPersistenceResult<PurchaseOrderRecord>> IssueAsync(TenantContext tenantContext, PurchaseOrderActionCommand command, PurchaseOrderAuditEvidence evidence, CancellationToken cancellationToken = default) => Unavailable<PurchaseOrderPersistenceResult<PurchaseOrderRecord>>();
            public Task<PurchaseOrderPersistenceResult<PurchaseOrderRecord>> CancelAsync(TenantContext tenantContext, PurchaseOrderActionCommand command, PurchaseOrderAuditEvidence evidence, CancellationToken cancellationToken = default) => Unavailable<PurchaseOrderPersistenceResult<PurchaseOrderRecord>>();
            public Task<PurchaseOrderPersistenceResult<PurchaseOrderRecord>> RecordConfirmationAsync(TenantContext tenantContext, PurchaseOrderConfirmationCommand command, PurchaseOrderAuditEvidence evidence, CancellationToken cancellationToken = default) => Unavailable<PurchaseOrderPersistenceResult<PurchaseOrderRecord>>();
            public Task<PurchaseOrderPersistenceResult<PurchaseOrderRecord>> ApproveSupplierChangeAsync(TenantContext tenantContext, PurchaseOrderApprovalCommand command, PurchaseOrderAuditEvidence evidence, CancellationToken cancellationToken = default) => Unavailable<PurchaseOrderPersistenceResult<PurchaseOrderRecord>>();
            public Task<PurchaseOrderPersistenceResult<PurchaseOrderRecord>> RejectSupplierChangeAsync(TenantContext tenantContext, PurchaseOrderActionCommand command, PurchaseOrderAuditEvidence evidence, CancellationToken cancellationToken = default) => Unavailable<PurchaseOrderPersistenceResult<PurchaseOrderRecord>>();
            public Task<IReadOnlyList<PurchaseOrderConfirmationRecord>> ReadConfirmationsAsync(TenantContext tenantContext, Guid purchaseOrderId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<PurchaseOrderConfirmationRecord>>([]);
            public Task<IReadOnlyList<PurchaseOrderHistoryRecord>> ReadHistoryAsync(TenantContext tenantContext, Guid purchaseOrderId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<PurchaseOrderHistoryRecord>>([]);
            public Task<IReadOnlyList<PurchaseOrderAuditRecord>> ReadAuditAsync(TenantContext tenantContext, Guid purchaseOrderId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<PurchaseOrderAuditRecord>>([]);
        }

        private sealed class ProviderPaymentTermPersistence(MasterDataPaymentTermRecord record) : IMasterDataCurrencyPaymentTermPersistence
        {
            public Task<IReadOnlyList<MasterDataCurrencyRecord>> ListCurrenciesAsync(TenantContext tenantContext, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<MasterDataCurrencyRecord>>([]);
            public Task<MasterDataCurrencyRecord?> FindCurrencyAsync(TenantContext tenantContext, Guid currencyId, CancellationToken cancellationToken = default) => Task.FromResult<MasterDataCurrencyRecord?>(null);
            public Task<MasterDataPersistenceResult<MasterDataCurrencyRecord>> CreateCurrencyAsync(TenantContext tenantContext, Guid currencyId, CreateMasterDataCurrencyCommand command, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => Unavailable<MasterDataPersistenceResult<MasterDataCurrencyRecord>>();
            public Task<MasterDataPersistenceResult<MasterDataCurrencyRecord>> EditCurrencyAsync(TenantContext tenantContext, EditMasterDataCurrencyCommand command, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => Unavailable<MasterDataPersistenceResult<MasterDataCurrencyRecord>>();
            public Task<MasterDataPersistenceResult<MasterDataCurrencyRecord>> SetCurrencyLifecycleAsync(TenantContext tenantContext, Guid currencyId, MasterDataLifecycleState lifecycleState, byte[] expectedVersion, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => Unavailable<MasterDataPersistenceResult<MasterDataCurrencyRecord>>();
            public Task<IReadOnlyList<MasterDataPaymentTermRecord>> ListPaymentTermsAsync(TenantContext tenantContext, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<MasterDataPaymentTermRecord>>([record]);
            public Task<MasterDataPaymentTermRecord?> FindPaymentTermAsync(TenantContext tenantContext, Guid paymentTermId, CancellationToken cancellationToken = default) => Task.FromResult<MasterDataPaymentTermRecord?>(paymentTermId == record.Id ? record : null);
            public Task<MasterDataPersistenceResult<MasterDataPaymentTermRecord>> CreatePaymentTermAsync(TenantContext tenantContext, Guid paymentTermId, CreateMasterDataPaymentTermCommand command, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => Unavailable<MasterDataPersistenceResult<MasterDataPaymentTermRecord>>();
            public Task<MasterDataPersistenceResult<MasterDataPaymentTermRecord>> EditPaymentTermAsync(TenantContext tenantContext, EditMasterDataPaymentTermCommand command, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => Unavailable<MasterDataPersistenceResult<MasterDataPaymentTermRecord>>();
            public Task<MasterDataPersistenceResult<MasterDataPaymentTermRecord>> SetPaymentTermLifecycleAsync(TenantContext tenantContext, Guid paymentTermId, MasterDataLifecycleState lifecycleState, byte[] expectedVersion, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => Unavailable<MasterDataPersistenceResult<MasterDataPaymentTermRecord>>();
            public Task<MasterDataPersistenceResult<MasterDataAuditRecord>> AppendAuditAsync(TenantContext tenantContext, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => Unavailable<MasterDataPersistenceResult<MasterDataAuditRecord>>();
            public Task<IReadOnlyList<MasterDataAuditRecord>> ReadAuditHistoryAsync(TenantContext tenantContext, MasterDataResourceKind resourceKind, Guid? resourceId = null, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<MasterDataAuditRecord>>([]);
        }

        private sealed class ProviderSupplierPersistence(SupplierRecord? record) : ISupplierPersistence
        {
            public Task<IReadOnlyList<SupplierRecord>> ListSuppliersAsync(TenantContext tenantContext, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<SupplierRecord>>(record is null ? [] : [record]);
            public Task<SupplierRecord?> FindSupplierAsync(TenantContext tenantContext, Guid supplierId, CancellationToken cancellationToken = default) => Task.FromResult<SupplierRecord?>(record?.Id == supplierId ? record : null);
            public Task<MasterDataPersistenceResult<SupplierRecord>> CreateSupplierAsync(TenantContext tenantContext, Guid supplierId, CreateSupplierCommand command, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => Unavailable<MasterDataPersistenceResult<SupplierRecord>>();
            public Task<MasterDataPersistenceResult<SupplierRecord>> EditSupplierAsync(TenantContext tenantContext, EditSupplierCommand command, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => Unavailable<MasterDataPersistenceResult<SupplierRecord>>();
            public Task<MasterDataPersistenceResult<SupplierRecord>> SetSupplierLifecycleAsync(TenantContext tenantContext, Guid supplierId, MasterDataLifecycleState lifecycleState, byte[] expectedVersion, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => Unavailable<MasterDataPersistenceResult<SupplierRecord>>();
            public Task<MasterDataPersistenceResult<MasterDataAuditRecord>> AppendAuditAsync(TenantContext tenantContext, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => Unavailable<MasterDataPersistenceResult<MasterDataAuditRecord>>();
            public Task<IReadOnlyList<MasterDataAuditRecord>> ReadAuditHistoryAsync(TenantContext tenantContext, Guid supplierId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<MasterDataAuditRecord>>([]);
        }

        private static Task<T> Unavailable<T>() => Task.FromException<T>(new InvalidOperationException("provider fixture operation unavailable"));
    }

}
