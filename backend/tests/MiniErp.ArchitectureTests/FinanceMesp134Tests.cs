using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MiniErp.App.BuildingBlocks.Rest;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.Modules.BusinessParties;
using MiniErp.App.Modules.Finance;
using MiniErp.App.Modules.Inventory;
using MiniErp.App.Modules.MasterData;
using MiniErp.Contracts.Modules.Audit;
using MiniErp.Contracts.Modules.Finance;
using MiniErp.Contracts.Modules.MasterData;
using MiniErp.Infrastructure.Persistence.Modules.Finance;
using MiniErp.Infrastructure.Persistence.Modules.MasterData;
using Xunit;

namespace MiniErp.ArchitectureTests;

/// <summary>
/// Direct MESP-134 persistence coverage. These tests exercise the production
/// FinanceMesp134Persistence against disposable SQLite module stores; the
/// provider-realistic contention matrix remains in SqlServerSafetyTests.
/// </summary>
public sealed class FinanceMesp134Tests
{
    [Fact]
    public async Task No_reporting_currency_persists_null_reporting_amount_and_not_captured_evidence()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.CreatePolicyAsync(null, 2, "AwayFromZero");

        var posted = await fixture.PostTaxAsync(10m);

        Assert.True(posted.Succeeded, posted.Code);
        Assert.Null(posted.Value!.MonetaryEvidence.ReportingCurrencyCode);
        Assert.Null(posted.Value.MonetaryEvidence.ReportingAmount);
        Assert.Equal(FinanceEvidenceStatus.NotCaptured, posted.Value.MonetaryEvidence.ReportingEvidenceStatus);
        var reconciliation = await fixture.Persistence.ReconcileReportingCurrencyAsync(fixture.Context, Fixture.CompanyId);
        Assert.Contains(reconciliation, item => item.JournalId == posted.Value.JournalId && item.Status == FinanceEvidenceStatus.NotCaptured);
    }

    [Fact]
    public async Task Reporting_equal_to_functional_is_captured_without_an_exchange_rate()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.CreatePolicyAsync(Fixture.SarCurrencyId, 2, "AwayFromZero");

        var posted = await fixture.PostTaxAsync(10m);

        Assert.True(posted.Succeeded, posted.Code);
        Assert.Equal("SAR", posted.Value!.MonetaryEvidence.ReportingCurrencyCode);
        Assert.Equal(posted.Value.FunctionalAmount, posted.Value.MonetaryEvidence.ReportingAmount);
        Assert.Null(posted.Value.MonetaryEvidence.FunctionalToReportingRate);
        Assert.Equal(FinanceEvidenceStatus.Captured, posted.Value.MonetaryEvidence.ReportingEvidenceStatus);
    }

    [Fact]
    public async Task Reporting_currency_different_from_functional_fails_closed_without_exact_rate()
    {
        await using var fixture = await Fixture.CreateAsync(includeReportingRate: false);
        await fixture.CreatePolicyAsync(Fixture.UsdCurrencyId, 2, "AwayFromZero");

        var posted = await fixture.PostTaxAsync(10m);

        Assert.False(posted.Succeeded);
        Assert.Equal("reporting_exchange_rate_required", posted.Code);
    }

    [Fact]
    public async Task Tax_preview_and_post_use_the_same_company_rounding_policy()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.CreatePolicyAsync(null, 3, "ToEven");

        var preview = await fixture.Persistence.PreviewTaxAsync(fixture.Context, fixture.TaxCommand(0.005m));
        var posted = await fixture.PostTaxAsync(0.005m);

        Assert.NotNull(preview);
        Assert.True(posted.Succeeded, posted.Code);
        Assert.Equal(0.002m, preview!.TaxAmount);
        Assert.Equal(preview.TaxAmount, posted.Value!.TaxAmount);
        Assert.Equal(3, posted.Value.MonetaryEvidence.RoundingScale);
        Assert.Equal("ToEven", posted.Value.MonetaryEvidence.RoundingMode);
    }

    [Fact]
    public async Task Overlapping_monetary_policy_versions_are_rejected()
    {
        await using var fixture = await Fixture.CreateAsync();
        var first = await fixture.CreatePolicyAsync(null, 2, "AwayFromZero");
        var second = await fixture.CreatePolicyAsync(null, 4, "ToEven", idempotencyKey: "policy-overlap");

        Assert.True(first.Succeeded, first.Code);
        Assert.False(second.Succeeded);
        Assert.Equal("monetary_policy_overlaps", second.Code);
    }

    [Fact]
    public async Task Tax_reversal_requires_reason_and_preserves_exact_reporting_snapshot()
    {
        await using var fixture = await Fixture.CreateAsync(includeReportingRate: true);
        await fixture.CreatePolicyAsync(Fixture.UsdCurrencyId, 2, "AwayFromZero");
        var posted = await fixture.PostTaxAsync(10m);
        Assert.True(posted.Succeeded, posted.Code);

        var missingReason = await fixture.Persistence.ReverseTaxAsync(
            fixture.Context,
            new FinanceTaxAccountingReversalCommand(posted.Value!.Id, posted.Value.Version, " ", Guid.NewGuid(), "reverse-missing-reason", "reverse-missing-reason"));
        Assert.False(missingReason.Succeeded);
        Assert.Equal("reason_required", missingReason.Code);

        var reversed = await fixture.Persistence.ReverseTaxAsync(
            fixture.Context,
            new FinanceTaxAccountingReversalCommand(posted.Value.Id, posted.Value.Version, "Owner-approved correction", Guid.NewGuid(), "reverse-tax", "reverse-tax"));
        Assert.True(reversed.Succeeded, reversed.Code);
        Assert.NotNull(reversed.Value!.ReversalJournalId);
        var taxReconciliation = await fixture.Persistence.ReconcileTaxAsync(fixture.Context, Fixture.CompanyId);
        Assert.Equal(FinanceEvidenceStatus.Reversed, Assert.Single(taxReconciliation).Status);
        var reporting = await fixture.Persistence.ReconcileReportingCurrencyAsync(fixture.Context, Fixture.CompanyId);
        Assert.Contains(reporting, item => item.JournalId == posted.Value.JournalId && item.Status == FinanceEvidenceStatus.Reconciled);
        Assert.Contains(reporting, item => item.JournalId == reversed.Value.ReversalJournalId && item.Status == FinanceEvidenceStatus.Reconciled);
    }

    [Fact]
    public async Task Reporting_reconciliation_uses_persisted_rate_evidence_after_rate_change()
    {
        await using var fixture = await Fixture.CreateAsync(includeReportingRate: true);
        await fixture.CreatePolicyAsync(Fixture.UsdCurrencyId, 2, "AwayFromZero");
        var posted = await fixture.PostTaxAsync(10m);
        Assert.True(posted.Succeeded, posted.Code);
        var original = posted.Value!.MonetaryEvidence.ReportingAmount;

        fixture.ReportingRate = fixture.ReportingRate with
        {
            Versions = [fixture.ReportingRate.Versions[0] with { Rate = 9m }]
        };

        var reconciliation = await fixture.Persistence.ReconcileReportingCurrencyAsync(fixture.Context, Fixture.CompanyId);
        var row = Assert.Single(reconciliation, item => item.JournalId == posted.Value.JournalId);
        Assert.Equal(original, row.ReportingAmount);
        Assert.Equal(original, row.ExpectedReportingAmount);
        Assert.Equal(FinanceEvidenceStatus.Reconciled, row.Status);
    }

    [Fact]
    public async Task Revaluation_scope_is_explicit_and_free_text_is_rejected()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.CreatePolicyAsync(null, 2, "AwayFromZero", revaluationEnabled: true);

        var rejected = await fixture.Persistence.CreateRevaluationBatchAsync(
            fixture.Context,
            new FinanceRevaluationBatchCommand(Fixture.CompanyId, new DateOnly(2026, 1, 15), "AP", Guid.NewGuid(), "revaluation-scope", "revaluation-scope"));
        Assert.False(rejected.Succeeded);
        Assert.Equal("unsupported_revaluation_scope", rejected.Code);
    }

    [Fact]
    public async Task Procurement_declared_tax_exact_match_is_persisted_with_authoritative_version_evidence()
    {
        await using var fixture = await Fixture.CreateAsync();
        var source = fixture.SupplierSource([new FinanceSupplierDeclaredTaxRecord("VAT50", 50m, 50m, 100m)]);
        var openItemId = await fixture.AddProcurementInvoiceAsync(source);
        await fixture.CreatePolicyAsync(null, 2, "AwayFromZero");

        var preview = await fixture.Persistence.PreviewTaxAsync(fixture.Context, fixture.TaxCommand(openItemId, 100m));
        var posted = await fixture.Persistence.PostTaxAsync(fixture.Context, fixture.TaxCommand(openItemId, 100m));

        Assert.NotNull(preview);
        Assert.True(posted.Succeeded, posted.Code);
        Assert.Equal("VAT50", posted.Value!.TaxCode);
        Assert.Equal(50m, posted.Value.TaxRatePercentage);
        Assert.Equal(50m, posted.Value.TaxAmount);
        Assert.Equal(100m, posted.Value.TaxableBase);
        Assert.Equal(posted.Value.TaxRateVersionId, preview!.TaxRateVersionId);
        Assert.Equal(posted.Value.TaxRateVersionNumber, preview.TaxRateVersionNumber);
    }

    [Fact]
    public async Task Procurement_declared_tax_code_mismatch_fails_with_tax_evidence_mismatch()
    {
        await using var fixture = await Fixture.CreateAsync();
        var source = fixture.SupplierSource([new FinanceSupplierDeclaredTaxRecord("VAT15", 50m, 50m, 100m)]);
        var openItemId = await fixture.AddProcurementInvoiceAsync(source);
        await fixture.CreatePolicyAsync(null, 2, "AwayFromZero");

        var result = await fixture.Persistence.PostTaxAsync(fixture.Context, fixture.TaxCommand(openItemId, 100m));

        Assert.False(result.Succeeded);
        Assert.Equal("tax_evidence_mismatch", result.Code);
    }

    [Fact]
    public async Task Procurement_declared_tax_rate_or_amount_mismatch_fails_with_tax_evidence_mismatch()
    {
        await using var fixture = await Fixture.CreateAsync();
        var source = fixture.SupplierSource([new FinanceSupplierDeclaredTaxRecord("VAT50", 15m, 15m, 100m)]);
        var openItemId = await fixture.AddProcurementInvoiceAsync(source);
        await fixture.CreatePolicyAsync(null, 2, "AwayFromZero");

        var result = await fixture.Persistence.PostTaxAsync(fixture.Context, fixture.TaxCommand(openItemId, 100m));

        Assert.False(result.Succeeded);
        Assert.Equal("tax_evidence_mismatch", result.Code);
    }

    [Fact]
    public async Task Procurement_declared_tax_ambiguous_evidence_fails_closed()
    {
        await using var fixture = await Fixture.CreateAsync();
        var source = fixture.SupplierSource([
            new FinanceSupplierDeclaredTaxRecord("VAT50", 50m, 50m, 100m),
            new FinanceSupplierDeclaredTaxRecord("VAT50", 50m, 50m, 100m)]);
        var openItemId = await fixture.AddProcurementInvoiceAsync(source);
        await fixture.CreatePolicyAsync(null, 2, "AwayFromZero");

        var result = await fixture.Persistence.PostTaxAsync(fixture.Context, fixture.TaxCommand(openItemId, 100m));

        Assert.False(result.Succeeded);
        Assert.Equal("tax_evidence_ambiguous", result.Code);
    }

    [Fact]
    public async Task Procurement_declared_tax_insufficient_evidence_fails_closed()
    {
        await using var fixture = await Fixture.CreateAsync();
        var source = fixture.SupplierSource([new FinanceSupplierDeclaredTaxRecord("VAT50", 50m, 50m, null)]);
        var openItemId = await fixture.AddProcurementInvoiceAsync(source);
        await fixture.CreatePolicyAsync(null, 2, "AwayFromZero");

        var result = await fixture.Persistence.PostTaxAsync(fixture.Context, fixture.TaxCommand(openItemId, 100m));

        Assert.False(result.Succeeded);
        Assert.Equal("tax_evidence_not_authoritative", result.Code);
    }

    [Fact]
    public async Task Procurement_declared_tax_date_or_currency_mismatch_fails_closed()
    {
        await using var fixture = await Fixture.CreateAsync();
        var source = fixture.SupplierSource([new FinanceSupplierDeclaredTaxRecord("VAT50", 50m, 50m, 100m)], declaredDate: new DateOnly(2026, 1, 16), declaredCurrency: "USD");
        var openItemId = await fixture.AddProcurementInvoiceAsync(source);
        await fixture.CreatePolicyAsync(null, 2, "AwayFromZero");

        var result = await fixture.Persistence.PostTaxAsync(fixture.Context, fixture.TaxCommand(openItemId, 100m));

        Assert.False(result.Succeeded);
        Assert.Equal("tax_evidence_mismatch", result.Code);
    }

    [Fact]
    public async Task Historical_fx_exact_identity_pair_and_rate_are_required_but_later_version_does_not_reinterpret_source()
    {
        await using var fixture = await Fixture.CreateAsync();
        var source = await fixture.AddForeignReceivableAsync();
        await fixture.CreatePolicyAsync(null, 2, "AwayFromZero");

        var result = await fixture.Persistence.PostTaxAsync(fixture.Context, fixture.TaxCommand(source.OpenItemId, 100m));

        Assert.True(result.Succeeded, result.Code);
        var evidence = result.Value!.MonetaryEvidence.TransactionToFunctionalRate;
        Assert.NotNull(evidence);
        Assert.Equal(source.RateId, evidence!.ExchangeRateId);
        Assert.Equal(source.HistoricalVersionId, evidence.ExchangeRateVersionId);
        Assert.Equal(1, evidence.VersionNumber);
        Assert.Equal("USD", evidence.SourceCurrencyCode);
        Assert.Equal("SAR", evidence.TargetCurrencyCode);
        Assert.Equal(3.5m, evidence.Rate);
    }

    [Fact]
    public async Task Historical_fx_missing_exchange_rate_id_fails_closed()
    {
        await using var fixture = await Fixture.CreateAsync();
        var source = await fixture.AddForeignReceivableAsync(missingIdentity: true);
        await fixture.CreatePolicyAsync(null, 2, "AwayFromZero");

        var result = await fixture.Persistence.PostTaxAsync(fixture.Context, fixture.TaxCommand(source.OpenItemId, 100m));

        Assert.False(result.Succeeded);
        Assert.Equal("exact_exchange_rate_evidence_required", result.Code);
    }

    [Fact]
    public async Task Historical_fx_wrong_version_identity_fails_closed()
    {
        await using var fixture = await Fixture.CreateAsync();
        var source = await fixture.AddForeignReceivableAsync(wrongVersion: true);
        await fixture.CreatePolicyAsync(null, 2, "AwayFromZero");

        var result = await fixture.Persistence.PostTaxAsync(fixture.Context, fixture.TaxCommand(source.OpenItemId, 100m));

        Assert.False(result.Succeeded);
        Assert.Equal("exact_exchange_rate_evidence_required", result.Code);
    }

    [Fact]
    public async Task Historical_fx_wrong_version_number_fails_closed()
    {
        await using var fixture = await Fixture.CreateAsync();
        var source = await fixture.AddForeignReceivableAsync(wrongVersionNumber: true);
        await fixture.CreatePolicyAsync(null, 2, "AwayFromZero");

        var result = await fixture.Persistence.PostTaxAsync(fixture.Context, fixture.TaxCommand(source.OpenItemId, 100m));

        Assert.False(result.Succeeded);
        Assert.Equal("exact_exchange_rate_evidence_required", result.Code);
    }

    [Fact]
    public async Task Historical_fx_wrong_pair_fails_closed()
    {
        await using var fixture = await Fixture.CreateAsync();
        var source = await fixture.AddForeignReceivableAsync(wrongPair: true);
        await fixture.CreatePolicyAsync(null, 2, "AwayFromZero");

        var result = await fixture.Persistence.PostTaxAsync(fixture.Context, fixture.TaxCommand(source.OpenItemId, 100m));

        Assert.False(result.Succeeded);
        Assert.Equal("exact_exchange_rate_evidence_required", result.Code);
    }

    [Fact]
    public async Task Historical_fx_wrong_stored_rate_fails_closed()
    {
        await using var fixture = await Fixture.CreateAsync();
        var source = await fixture.AddForeignReceivableAsync(storedRate: 3.5m, historicalRate: 3.75m);
        await fixture.CreatePolicyAsync(null, 2, "AwayFromZero");

        var result = await fixture.Persistence.PostTaxAsync(fixture.Context, fixture.TaxCommand(source.OpenItemId, 100m));

        Assert.False(result.Succeeded);
        Assert.Equal("exact_exchange_rate_evidence_required", result.Code);
    }

    [Fact]
    public async Task Realized_fx_gain_persistence_uses_one_sided_balanced_evidence_and_exact_reversal()
    {
        await using var fixture = await Fixture.CreateAsync(includeReportingRate: true);
        var source = await fixture.AddForeignReceivableAsync(currentRate: 3.75m, settlementCompatible: true);
        await fixture.CreatePolicyAsync(Fixture.UsdCurrencyId, 2, "AwayFromZero");

        var allocation = await fixture.CreatePostedReceiptAndAllocateAsync(source, 25m);
        await fixture.AssertRealizedAllocationAsync(allocation, FinanceFxDirection.Gain, 6.25m);
        var reversed = await fixture.ReverseAllocationAsync(allocation);

        var fx = await fixture.Persistence.ReconcileFxAsync(fixture.Context, Fixture.CompanyId);
        Assert.Equal(FinanceEvidenceStatus.Reversed, Assert.Single(fx).Status);
        await fixture.AssertExactAllocationEvidenceAsync(allocation.JournalId!.Value, reversed.JournalId!.Value);
    }

    [Fact]
    public async Task Realized_fx_loss_persistence_uses_one_sided_balanced_evidence_and_exact_reversal()
    {
        await using var fixture = await Fixture.CreateAsync(includeReportingRate: true);
        var source = await fixture.AddForeignReceivableAsync(currentRate: 3.25m, settlementCompatible: true);
        await fixture.CreatePolicyAsync(Fixture.UsdCurrencyId, 2, "AwayFromZero");

        var allocation = await fixture.CreatePostedReceiptAndAllocateAsync(source, 25m);
        await fixture.AssertRealizedAllocationAsync(allocation, FinanceFxDirection.Loss, 6.25m);
        var reversed = await fixture.ReverseAllocationAsync(allocation);

        var fx = await fixture.Persistence.ReconcileFxAsync(fixture.Context, Fixture.CompanyId);
        Assert.Equal(FinanceEvidenceStatus.Reversed, Assert.Single(fx).Status);
        await fixture.AssertExactAllocationEvidenceAsync(allocation.JournalId!.Value, reversed.JournalId!.Value);
    }

    [Fact]
    public async Task Revaluation_calculate_post_and_reverse_persist_functional_snapshot_and_reconciliation()
    {
        await using var fixture = await Fixture.CreateAsync();
        var source = await fixture.AddForeignReceivableAsync(settlementCompatible: true);
        await fixture.CreatePolicyAsync(null, 2, "AwayFromZero", revaluationEnabled: true);

        var batch = await fixture.CreateRevaluationAsync(source.OpenItemId);
        var calculated = await fixture.Persistence.CalculateRevaluationBatchAsync(fixture.Context, new FinanceRevaluationActionCommand(batch.Id, batch.Version, null, Guid.NewGuid(), "direct-revaluation-calculate", "direct-revaluation-calculate"));
        Assert.True(calculated.Succeeded, calculated.Code);
        var line = Assert.Single(calculated.Value!.Lines);
        Assert.Equal(100m, line.OutstandingTransactionAmount);
        Assert.Equal(350m, line.HistoricalFunctionalAmount);
        Assert.Equal(375m, line.RevaluedFunctionalAmount);
        Assert.Equal(25m, line.Difference);
        Assert.Equal(FinanceFxDirection.Gain, line.Direction);
        Assert.NotNull(line.SourceSnapshotFingerprint);

        var posted = await fixture.Persistence.PostRevaluationBatchAsync(fixture.Context, new FinanceRevaluationActionCommand(calculated.Value.Id, calculated.Value.Version, null, Guid.NewGuid(), "direct-revaluation-post", "direct-revaluation-post"));
        Assert.True(posted.Succeeded, posted.Code);
        var unrealized = await fixture.Persistence.ReconcileUnrealizedFxAsync(fixture.Context, Fixture.CompanyId);
        Assert.Equal(FinanceEvidenceStatus.Reconciled, Assert.Single(unrealized).Status);
        Assert.NotNull(posted.Value!.Lines.Single().PostingRuleId);
        var reversed = await fixture.Persistence.ReverseRevaluationBatchAsync(fixture.Context, new FinanceRevaluationActionCommand(posted.Value.Id, posted.Value.Version, "direct revaluation correction", Guid.NewGuid(), "direct-revaluation-reverse", "direct-revaluation-reverse"));
        Assert.True(reversed.Succeeded, reversed.Code);
        var reversedUnrealized = await fixture.Persistence.ReconcileUnrealizedFxAsync(fixture.Context, Fixture.CompanyId);
        Assert.Equal(FinanceEvidenceStatus.Reversed, Assert.Single(reversedUnrealized).Status);
        await fixture.AssertExactRevaluationEvidenceAsync(posted.Value!.Lines.Single().JournalId!.Value, reversed.Value!.Lines.Single().ReversalJournalId!.Value);
    }

    [Fact]
    public async Task Revaluation_post_rejects_a_real_allocation_source_change_after_calculation()
    {
        await using var fixture = await Fixture.CreateAsync();
        var source = await fixture.AddForeignReceivableAsync(settlementCompatible: true);
        await fixture.CreatePolicyAsync(null, 2, "AwayFromZero", revaluationEnabled: true);
        var batch = await fixture.CreateRevaluationAsync(source.OpenItemId);
        var calculated = await fixture.Persistence.CalculateRevaluationBatchAsync(fixture.Context, new FinanceRevaluationActionCommand(batch.Id, batch.Version, null, Guid.NewGuid(), "direct-source-change-calculate", "direct-source-change-calculate"));
        Assert.True(calculated.Succeeded, calculated.Code);

        var allocation = await fixture.CreatePostedReceiptAndAllocateAsync(source, 25m);
        var posted = await fixture.Persistence.PostRevaluationBatchAsync(fixture.Context, new FinanceRevaluationActionCommand(calculated.Value!.Id, calculated.Value.Version, null, Guid.NewGuid(), "direct-source-change-post", "direct-source-change-post"));

        Assert.False(posted.Succeeded);
        Assert.Equal("revaluation_source_changed", posted.Code);
        Assert.NotNull(allocation.JournalId);
        await using var db = new FinanceDbContext(fixture.Options, fixture.Context.TenantContext);
        Assert.Equal(FinanceRevaluationBatchStatus.Calculated, await db.RevaluationBatches.Where(item => item.Id == batch.Id).Select(item => item.Status).SingleAsync());
        Assert.Equal(0, await db.Journals.CountAsync(item => item.SourceContract == "finance-revaluation.v1"));
    }

    [Fact]
    public async Task ReconcileTax_scopes_by_the_effect_durable_tax_effective_date_not_journal_existence()
    {
        await using var fixture = await Fixture.CreateAsync();
        await fixture.CreatePolicyAsync(null, 2, "AwayFromZero");
        var posted = await fixture.PostTaxAsync(10m);
        Assert.True(posted.Succeeded, posted.Code);
        var effectiveOn = posted.Value!.TaxEffectiveOn;

        var before = await fixture.Persistence.ReconcileTaxAsync(fixture.Context, Fixture.CompanyId, effectiveOn.AddDays(-1));
        Assert.Empty(before);

        var onOrAfter = await fixture.Persistence.ReconcileTaxAsync(fixture.Context, Fixture.CompanyId, effectiveOn);
        Assert.Equal(FinanceEvidenceStatus.Reconciled, Assert.Single(onOrAfter).Status);
    }

    [Fact]
    public async Task ReconcileFx_scopes_realized_allocations_by_the_durable_allocation_date_not_journal_existence()
    {
        await using var fixture = await Fixture.CreateAsync(includeReportingRate: true);
        var source = await fixture.AddForeignReceivableAsync(currentRate: 3.75m, settlementCompatible: true);
        await fixture.CreatePolicyAsync(Fixture.UsdCurrencyId, 2, "AwayFromZero");
        var allocation = await fixture.CreatePostedReceiptAndAllocateAsync(source, 25m);
        var allocationDate = new DateOnly(2026, 1, 15);

        var before = await fixture.Persistence.ReconcileFxAsync(fixture.Context, Fixture.CompanyId, allocationDate.AddDays(-1));
        Assert.Empty(before);

        var onOrAfter = await fixture.Persistence.ReconcileFxAsync(fixture.Context, Fixture.CompanyId, allocationDate);
        Assert.Equal(FinanceEvidenceStatus.Reconciled, Assert.Single(onOrAfter).Status);
        Assert.Equal(allocation.Id, Assert.Single(onOrAfter).AllocationId);
    }

    [Fact]
    public async Task ReconcileUnrealizedFx_scopes_revaluation_lines_by_the_durable_asOf_date_not_journal_existence()
    {
        await using var fixture = await Fixture.CreateAsync();
        var source = await fixture.AddForeignReceivableAsync(settlementCompatible: true);
        await fixture.CreatePolicyAsync(null, 2, "AwayFromZero", revaluationEnabled: true);
        var batch = await fixture.CreateRevaluationAsync(source.OpenItemId);
        var calculated = await fixture.Persistence.CalculateRevaluationBatchAsync(fixture.Context, new FinanceRevaluationActionCommand(batch.Id, batch.Version, null, Guid.NewGuid(), "asof-revaluation-calculate", "asof-revaluation-calculate"));
        Assert.True(calculated.Succeeded, calculated.Code);
        var posted = await fixture.Persistence.PostRevaluationBatchAsync(fixture.Context, new FinanceRevaluationActionCommand(calculated.Value!.Id, calculated.Value.Version, null, Guid.NewGuid(), "asof-revaluation-post", "asof-revaluation-post"));
        Assert.True(posted.Succeeded, posted.Code);
        var asOfDate = batch.AsOfDate;

        var before = await fixture.Persistence.ReconcileUnrealizedFxAsync(fixture.Context, Fixture.CompanyId, asOfDate.AddDays(-1));
        Assert.Empty(before);

        var onOrAfter = await fixture.Persistence.ReconcileUnrealizedFxAsync(fixture.Context, Fixture.CompanyId, asOfDate);
        Assert.Equal(FinanceEvidenceStatus.Reconciled, Assert.Single(onOrAfter).Status);
    }

    private sealed class Fixture : IAsyncDisposable
    {
        internal static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
        internal static readonly Guid CompanyId = Guid.Parse("11111111-1111-1111-1111-111111111111");
        internal static readonly Guid ActorId = Guid.Parse("44444444-4444-4444-4444-444444444444");
        internal static readonly Guid SarCurrencyId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaa0001");
        internal static readonly Guid UsdCurrencyId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaa0002");

        private readonly SqliteConnection connection;
        private readonly SqliteConnection masterDataConnection;
        private readonly DbContextOptions options;
        private readonly ConfiguredFinanceCompanyProvider companies;
        private readonly MasterDataTaxService taxService;
        private readonly Guid arControlId;
        private readonly Guid revenueAccountId;
        private readonly MutableSupplierInvoiceSourceProvider supplierSources;

        private Fixture(
            SqliteConnection connection,
            SqliteConnection masterDataConnection,
            DbContextOptions options,
            FinanceRequestContext context,
            ConfiguredFinanceCompanyProvider companies,
            MasterDataTaxService taxService,
            FinanceMesp134Persistence persistence,
            Guid taxId,
            Guid openItemId,
            Guid arControlId,
            Guid revenueAccountId,
            TestExchangeRatePersistence exchangeRates,
            MutableSupplierInvoiceSourceProvider supplierSources)
        {
            this.connection = connection;
            this.masterDataConnection = masterDataConnection;
            this.options = options;
            Context = context;
            this.companies = companies;
            this.taxService = taxService;
            Persistence = persistence;
            TaxId = taxId;
            OpenItemId = openItemId;
            this.arControlId = arControlId;
            this.revenueAccountId = revenueAccountId;
            ExchangeRates = exchangeRates;
            this.supplierSources = supplierSources;
        }

        internal FinanceRequestContext Context { get; }
        internal FinanceMesp134Persistence Persistence { get; }
        internal DbContextOptions Options => options;
        internal Guid TaxId { get; }
        internal Guid OpenItemId { get; }
        internal TestExchangeRatePersistence ExchangeRates { get; }
        internal MasterDataExchangeRateRecord ReportingRate
        {
            get => ExchangeRates.Records.Single(item => item.Id == ReportingRateId);
            set => ExchangeRates.Replace(value);
        }
        private static readonly Guid ReportingRateId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaa0101");

        internal static async Task<Fixture> CreateAsync(bool includeReportingRate = false)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder().UseSqlite(connection).Options;
            var masterDataConnection = new SqliteConnection("Data Source=:memory:");
            await masterDataConnection.OpenAsync();
            var masterDataOptions = new DbContextOptionsBuilder().UseSqlite(masterDataConnection).Options;
            var tenantContext = TenantContext.ForOrdinaryMembership(
                new TenantId(TenantId),
                new MembershipReference(Guid.NewGuid()),
                correlationId: new CorrelationId("mesp134-test"),
                actorId: ActorId);
            await using (var masterData = new MasterDataDbContext(masterDataOptions, tenantContext)) await masterData.Database.EnsureCreatedAsync();
            await using (var finance = new FinanceDbContext(options, tenantContext)) await finance.Database.EnsureCreatedAsync();

            var foundation = FoundationRequestContext.ForTenant(ActorId, Guid.NewGuid(), tenantContext, "tenant.finance.tax.post");
            Assert.True(FinanceRequestContext.TryCreate(foundation, out var context));
            var companies = new ConfiguredFinanceCompanyProvider([new FinanceCompanyOption(TenantId, CompanyId, "Test Company", "SAR")]);
            var taxPersistence = new MasterDataTaxPersistence(masterDataOptions);
            var authorization = new MasterDataResourceAuthorizationService(
                new GrantingCapabilityResolver(), new TaxResourcePolicy(), new TaxApprovalPolicy(), new TaxScopePolicy());
            var taxService = new MasterDataTaxService(authorization, taxPersistence);
            var taxContext = MasterDataRequestContext.FromFoundationContext(foundation);
            var createdTax = await taxService.CreateTaxAsync(taxContext, new CreateMasterDataTaxCommand(
                "VAT50", "VAT", new LocalizedName("VAT", "ضريبة"), new LocalizedName("VAT", "ضريبة"),
                TaxDirection.Both, new MasterDataTaxRateVersion(new DateOnly(2026, 1, 1), null, 50m)));
            Assert.True(createdTax.Succeeded, createdTax.Code);

            var rates = new TestExchangeRatePersistence();
            if (includeReportingRate) rates.Add(ExchangeRate(ReportingRateId, SarCurrencyId, UsdCurrencyId, "SAR", "USD", 2m));
            var currency = new TestCurrencyPaymentTermPersistence();
            var financePersistence = new FinancePersistence(options, companies, new UnavailableInventoryValuationPersistence(), rates);
            var accountA = await financePersistence.CreateAccountAsync(context!, Account("AR-CONTROL", FinanceAccountType.Asset));
            var accountB = await financePersistence.CreateAccountAsync(context!, Account("REVENUE", FinanceAccountType.Revenue));
            var taxAccount = await financePersistence.CreateAccountAsync(context!, Account("TAX-OUTPUT", FinanceAccountType.Liability));
            Assert.True(accountA.Succeeded && accountB.Succeeded && taxAccount.Succeeded);
            var calendar = await financePersistence.CreateCalendarAsync(context!, new FinanceFiscalCalendarCommand(CompanyId, "FY", Guid.NewGuid(), "calendar", "calendar"));
            var year = await financePersistence.CreateYearAsync(context!, new FinanceFiscalYearCommand(calendar.Value!.Id, 2026, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), Guid.NewGuid(), "year", "year"));
            var period = await financePersistence.CreatePeriodAsync(context!, new FinanceFiscalPeriodCommand(year.Value!.Id, 1, "2026", "2026", null, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), Guid.NewGuid(), "period", "period"));
            var opened = await financePersistence.SetPeriodStateAsync(context!, new FinancePeriodStateCommand(period.Value!.Id, FinanceFiscalPeriodState.Open, null, period.Value.Version, "open", "open"));
            Assert.True(opened.Succeeded, opened.Code);
            var rule = await financePersistence.CreatePostingRuleAsync(context!, new FinancePostingRuleCommand(CompanyId, "finance-tax.v1", "output", accountA.Value!.Id, taxAccount.Value!.Id, false, new DateOnly(2026, 1, 1), null, Guid.NewGuid(), "tax-rule", "tax-rule"));
            Assert.True(rule.Succeeded, rule.Code);
            var inputRule = await financePersistence.CreatePostingRuleAsync(context!, new FinancePostingRuleCommand(CompanyId, "finance-tax.v1", "input", taxAccount.Value!.Id, accountB.Value!.Id, false, new DateOnly(2026, 1, 1), null, Guid.NewGuid(), "tax-input-rule", "tax-input-rule"));
            Assert.True(inputRule.Succeeded, inputRule.Code);
            var fxLoss = await financePersistence.CreateAccountAsync(context!, Account("FX-LOSS", FinanceAccountType.Expense));
            var fxGain = await financePersistence.CreateAccountAsync(context!, Account("FX-GAIN", FinanceAccountType.Revenue));
            Assert.True(fxLoss.Succeeded && fxGain.Succeeded);
            var realizedRule = await financePersistence.CreatePostingRuleAsync(context!, new FinancePostingRuleCommand(CompanyId, "finance-fx.v1", "realized", fxLoss.Value!.Id, fxGain.Value!.Id, false, new DateOnly(2026, 1, 1), null, Guid.NewGuid(), "realized-fx-rule", "realized-fx-rule"));
            Assert.True(realizedRule.Succeeded, realizedRule.Code);
            var unrealizedRule = await financePersistence.CreatePostingRuleAsync(context!, new FinancePostingRuleCommand(CompanyId, "finance-fx.v1", "unrealized", fxLoss.Value.Id, fxGain.Value.Id, false, new DateOnly(2026, 1, 1), null, Guid.NewGuid(), "unrealized-fx-rule", "unrealized-fx-rule"));
            Assert.True(unrealizedRule.Succeeded, unrealizedRule.Code);
            var receiptRule = await financePersistence.CreatePostingRuleAsync(context!, new FinancePostingRuleCommand(CompanyId, "customer-receipt.v1", "on-account", accountB.Value.Id, accountA.Value.Id, false, new DateOnly(2026, 1, 1), null, Guid.NewGuid(), "receipt-on-account-rule", "receipt-on-account-rule"));
            Assert.True(receiptRule.Succeeded, receiptRule.Code);
            var receiptAllocationRule = await financePersistence.CreatePostingRuleAsync(context!, new FinancePostingRuleCommand(CompanyId, "customer-receipt.v1", "allocation", accountB.Value.Id, accountA.Value.Id, false, new DateOnly(2026, 1, 1), null, Guid.NewGuid(), "receipt-allocation-rule", "receipt-allocation-rule"));
            Assert.True(receiptAllocationRule.Succeeded, receiptAllocationRule.Code);

            var openItemId = Guid.NewGuid();
            var journalId = Guid.NewGuid();
            await using (var db = new FinanceDbContext(options, tenantContext))
            {
                var ar = await db.Accounts.SingleAsync(item => item.Id == accountA.Value.Id);
                var revenue = await db.Accounts.SingleAsync(item => item.Id == accountB.Value!.Id);
                var journal = new FinanceJournalEntity(tenantContext.TenantId, journalId, new FinanceJournalCommand(CompanyId, new DateOnly(2026, 1, 15), new DateOnly(2026, 1, 15), "SAR", 1m, null, null, null, "manual-ar.v1", "recognition", openItemId, 1, null, "AR recognition", [new FinanceJournalLineCommand(revenue.Id, 100m, 0m, 100m, "SAR", null, "Revenue"), new FinanceJournalLineCommand(ar.Id, 0m, 100m, 100m, "SAR", null, "AR")], journalId, "recognition", "recognition", FinanceJournalAmountAuthority.ManualTransactionCurrency, FinanceApprovalRequirement.NotRequired), 1, "SAR", ActorId, DateTimeOffset.UtcNow);
                journal.SetPeriod(year.Value.Id, period.Value.Id);
                journal.SetStatus(FinanceJournalStatus.Posted, ActorId, DateTimeOffset.UtcNow);
                journal.Lines.Add(new FinanceJournalLineEntity(tenantContext.TenantId, Guid.NewGuid(), journal.Id, 1, revenue, new FinanceJournalLineCommand(revenue.Id, 100m, 0m, 100m, "SAR", null, "Revenue"), null, 100m, 0m, FinanceJournalAmountAuthority.ManualTransactionCurrency));
                journal.Lines.Add(new FinanceJournalLineEntity(tenantContext.TenantId, Guid.NewGuid(), journal.Id, 2, ar, new FinanceJournalLineCommand(ar.Id, 0m, 100m, 100m, "SAR", null, "AR"), null, 0m, 100m, FinanceJournalAmountAuthority.ManualTransactionCurrency));
                var item = new FinanceOpenItemEntity(tenantContext.TenantId, openItemId, FinanceOpenItemKind.Receivable, CompanyId, null, Guid.NewGuid(), "manual-ar.v1", openItemId, 1, openItemId, 1, "AR-1", new DateOnly(2026, 1, 15), new DateOnly(2026, 2, 15), "SAR", 100m, "SAR", 100m, 1m, null, null, null, null, null, null, "manual AR");
                item.SetRecognition(FinanceOpenItemRecognitionState.Recognized, journal.Id);
                db.Journals.Add(journal);
                db.OpenItems.Add(item);
                await db.SaveChangesAsync();
            }

            var supplierSources = new MutableSupplierInvoiceSourceProvider();
            var persistence = new FinanceMesp134Persistence(options, companies, currency, rates, taxService, supplierSources);
            return new Fixture(connection, masterDataConnection, options, context!, companies, taxService, persistence, createdTax.Value!.Id, openItemId, accountA.Value.Id, accountB.Value.Id, rates, supplierSources);
        }

        internal async Task<FinanceOperationResult<FinanceMonetaryPolicyRecord>> CreatePolicyAsync(Guid? reportingCurrencyId, int scale, string mode, bool revaluationEnabled = false, string? idempotencyKey = null) =>
            await Persistence.CreateMonetaryPolicyAsync(Context, new FinanceMonetaryPolicyCommand(CompanyId, reportingCurrencyId, scale, mode, revaluationEnabled, new DateOnly(2026, 1, 1), null, Guid.NewGuid(), idempotencyKey ?? $"policy-{Guid.NewGuid():N}", idempotencyKey ?? $"policy-{Guid.NewGuid():N}"));

        internal FinanceTaxAccountingCommand TaxCommand(decimal taxableBase) => new(CompanyId, OpenItemId, TaxId, taxableBase, "mesp134-test", Guid.NewGuid(), $"tax-{Guid.NewGuid():N}", $"tax-{Guid.NewGuid():N}");
        internal FinanceTaxAccountingCommand TaxCommand(Guid openItemId, decimal taxableBase) => new(CompanyId, openItemId, TaxId, taxableBase, "mesp134-test", Guid.NewGuid(), $"tax-{Guid.NewGuid():N}", $"tax-{Guid.NewGuid():N}");
        internal Task<FinanceOperationResult<FinanceTaxAccountingEffectRecord>> PostTaxAsync(decimal taxableBase) => Persistence.PostTaxAsync(Context, TaxCommand(taxableBase));

        internal FinanceSupplierInvoiceSourceRecord SupplierSource(
            IReadOnlyList<FinanceSupplierDeclaredTaxRecord> declaredTaxes,
            DateOnly? declaredDate = null,
            string? declaredCurrency = null)
        {
            var date = new DateOnly(2026, 1, 15);
            return new FinanceSupplierInvoiceSourceRecord(
                TenantId,
                CompanyId,
                Guid.Parse("88888888-8888-8888-8888-888888888888"),
                "procurement-supplier-invoice.v1",
                Guid.NewGuid(),
                1,
                Guid.NewGuid(),
                1,
                "SUPPLIER-INVOICE",
                date,
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
                Guid.NewGuid(),
                1,
                "supplier source",
                "supplier-test",
                DeclaredCurrencyCode: declaredCurrency ?? "SAR",
                DeclaredInvoiceDate: declaredDate ?? date,
                DeclaredTaxAmount: 50m,
                DeclaredTaxes: declaredTaxes);
        }

        internal async Task<Guid> AddProcurementInvoiceAsync(FinanceSupplierInvoiceSourceRecord source)
        {
            supplierSources.Add(source);
            var openItemId = Guid.NewGuid();
            var journalId = Guid.NewGuid();
            await using var db = new FinanceDbContext(options, Context.TenantContext);
            var expense = await db.Accounts.SingleAsync(item => item.Id == revenueAccountId);
            var control = await db.Accounts.SingleAsync(item => item.Id == arControlId);
            var period = await db.FiscalPeriods.SingleAsync(item => item.CompanyId == CompanyId);
            var command = new FinanceJournalCommand(CompanyId, source.DocumentDate, source.DocumentDate, "SAR", 1m, null, null, null, "procurement-supplier-invoice.v1", "recognition", openItemId, 1, null, "supplier invoice recognition", [
                new FinanceJournalLineCommand(expense.Id, 100m, 0m, 100m, "SAR", null, "Expense"),
                new FinanceJournalLineCommand(control.Id, 0m, 100m, 100m, "SAR", null, "AP")
            ], journalId, "supplier-recognition", "supplier-recognition", FinanceJournalAmountAuthority.ManualTransactionCurrency, FinanceApprovalRequirement.NotRequired);
            var journal = new FinanceJournalEntity(Context.TenantId, journalId, command, (await db.Journals.Select(item => (long?)item.JournalSequence).MaxAsync() ?? 0L) + 1L, "SAR", Context.ActorId, DateTimeOffset.UtcNow);
            journal.SetPeriod(period.FiscalYearId, period.Id);
            journal.SetStatus(FinanceJournalStatus.Posted, Context.ActorId, DateTimeOffset.UtcNow);
            journal.Lines.Add(new FinanceJournalLineEntity(Context.TenantId, Guid.NewGuid(), journal.Id, 1, expense, command.Lines[0], null, 100m, 0m, FinanceJournalAmountAuthority.ManualTransactionCurrency));
            journal.Lines.Add(new FinanceJournalLineEntity(Context.TenantId, Guid.NewGuid(), journal.Id, 2, control, command.Lines[1], null, 0m, 100m, FinanceJournalAmountAuthority.ManualTransactionCurrency));
            var item = new FinanceOpenItemEntity(Context.TenantId, openItemId, FinanceOpenItemKind.Payable, CompanyId, source.SupplierId, null, source.SourceContract, source.SourceDocumentId, source.SourceDocumentVersion, source.SourceEvidenceId, source.SourceEvidenceVersion, source.Reference, source.DocumentDate, source.DueDate ?? source.DocumentDate, source.CurrencyCode, source.Amount, source.FunctionalCurrencyCode, source.FunctionalAmount, source.ExchangeRate, source.ExchangeRateId, source.ExchangeRateVersionId, source.ExchangeRateVersionNumber, source.PaymentTerm, source.MatchEvidenceId, source.MatchEvidenceVersion, source.SourceSnapshot);
            item.SetRecognition(FinanceOpenItemRecognitionState.Recognized, journal.Id);
            db.Journals.Add(journal);
            db.OpenItems.Add(item);
            await db.SaveChangesAsync();
            return openItemId;
        }

        internal sealed record ForeignReceivable(Guid OpenItemId, Guid RateId, Guid HistoricalVersionId, Guid CurrentVersionId, decimal CurrentRate);

        internal async Task<ForeignReceivable> AddForeignReceivableAsync(
            decimal? storedRate = 3.5m,
            decimal historicalRate = 3.5m,
            decimal currentRate = 3.75m,
            bool missingIdentity = false,
            bool wrongVersion = false,
            bool wrongVersionNumber = false,
            bool wrongPair = false,
            bool settlementCompatible = false)
        {
            var rateId = Guid.NewGuid();
            var historicalVersionId = Guid.NewGuid();
            var currentVersionId = Guid.NewGuid();
            var sourceCurrency = wrongPair ? "EUR" : "USD";
            var record = new MasterDataExchangeRateRecord(
                rateId,
                new TenantId(TenantId),
                Guid.NewGuid(),
                Guid.NewGuid(),
                sourceCurrency,
                "SAR",
                MasterDataLifecycleState.Active,
                2,
                [
                    new MasterDataExchangeRateVersionRecord(historicalVersionId, 1, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 14), historicalRate, 6, ExchangeRateProvenance.Configured, "historical source rate", sourceCurrency, "SAR"),
                    new MasterDataExchangeRateVersionRecord(currentVersionId, 2, new DateOnly(2026, 1, 15), null, currentRate, 6, ExchangeRateProvenance.Configured, "later current rate", sourceCurrency, "SAR")
                ],
                [1, 2]);
            ExchangeRates.Add(record);
            var openItemId = Guid.NewGuid();
            var journalId = Guid.NewGuid();
            await using var db = new FinanceDbContext(options, Context.TenantContext);
            var ar = await db.Accounts.SingleAsync(item => item.Id == arControlId);
            var revenue = await db.Accounts.SingleAsync(item => item.Id == revenueAccountId);
            var period = await db.FiscalPeriods.SingleAsync(item => item.CompanyId == CompanyId);
            var command = new FinanceJournalCommand(CompanyId, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 1), "USD", historicalRate, rateId, historicalVersionId, 1, "manual-ar.v1", "recognition", openItemId, 1, null, "foreign AR recognition", [
                new FinanceJournalLineCommand(ar.Id, settlementCompatible ? 100m : 0m, settlementCompatible ? 0m : 100m, 350m, "USD", null, "AR"),
                new FinanceJournalLineCommand(revenue.Id, settlementCompatible ? 0m : 100m, settlementCompatible ? 100m : 0m, 350m, "USD", null, "Revenue")
            ], journalId, "foreign-recognition", "foreign-recognition", FinanceJournalAmountAuthority.ManualTransactionCurrency, FinanceApprovalRequirement.NotRequired);
            var journal = new FinanceJournalEntity(Context.TenantId, journalId, command, (await db.Journals.Select(item => (long?)item.JournalSequence).MaxAsync() ?? 0L) + 1L, "SAR", Context.ActorId, DateTimeOffset.UtcNow);
            journal.SetPeriod(period.FiscalYearId, period.Id);
            journal.SetStatus(FinanceJournalStatus.Posted, Context.ActorId, DateTimeOffset.UtcNow);
            journal.Lines.Add(new FinanceJournalLineEntity(Context.TenantId, Guid.NewGuid(), journal.Id, 1, ar, command.Lines[0], null, settlementCompatible ? 350m : 0m, settlementCompatible ? 0m : 350m, FinanceJournalAmountAuthority.ManualTransactionCurrency));
            journal.Lines.Add(new FinanceJournalLineEntity(Context.TenantId, Guid.NewGuid(), journal.Id, 2, revenue, command.Lines[1], null, settlementCompatible ? 0m : 350m, settlementCompatible ? 350m : 0m, FinanceJournalAmountAuthority.ManualTransactionCurrency));
            var item = new FinanceOpenItemEntity(Context.TenantId, openItemId, FinanceOpenItemKind.Receivable, CompanyId, null, Guid.Parse("77777777-7777-7777-7777-777777777777"), "manual-ar.v1", openItemId, 1, openItemId, 1, "FOREIGN-AR", new DateOnly(2026, 1, 1), new DateOnly(2026, 2, 1), "USD", 100m, "SAR", 350m, missingIdentity ? null : storedRate, missingIdentity ? null : rateId, missingIdentity ? null : wrongVersion ? Guid.NewGuid() : historicalVersionId, missingIdentity ? null : wrongVersionNumber ? 2 : 1, null, null, null, "foreign source");
            item.SetRecognition(FinanceOpenItemRecognitionState.Recognized, journal.Id);
            db.Journals.Add(journal);
            db.OpenItems.Add(item);
            await db.SaveChangesAsync();
            return new ForeignReceivable(openItemId, rateId, historicalVersionId, currentVersionId, currentRate);
        }

        internal FinanceRevaluationBatchRecord CreateBatchRecord(FinanceOperationResult<FinanceRevaluationBatchRecord> result) => result.Value!;

        internal async Task<FinanceRevaluationBatchRecord> CreateRevaluationAsync(Guid sourceId)
        {
            var created = await Persistence.CreateRevaluationBatchAsync(Context, new FinanceRevaluationBatchCommand(CompanyId, new DateOnly(2026, 1, 15), FinanceRevaluationScopes.ApArAndUnallocatedSettlements, Guid.NewGuid(), "revaluation-create", "revaluation-create"));
            Assert.True(created.Succeeded, created.Code);
            return created.Value!;
        }

        internal async Task<FinanceAllocationRecord> CreatePostedReceiptAndAllocateAsync(ForeignReceivable source, decimal amount)
        {
            var settlement = new FinanceSettlementPersistence(options, companies, ExchangeRates, new ActiveCustomerReader(), new UnavailableSupplierPersistence(), new TestCurrencyPaymentTermPersistence(), supplierSources, new NoApprovalPolicy());
            var method = await settlement.CreatePaymentMethodAsync(Context, new FinancePaymentMethodCommand(CompanyId, "DIRECT-RECEIPT", "Direct receipt", null, FinancePaymentMethodDirection.Receipt, true, false, new DateOnly(2026, 1, 1), null, Guid.NewGuid(), null, "direct-receipt-method", "direct-receipt-method"));
            Assert.True(method.Succeeded, method.Code);
            var cash = await settlement.CreateCashAccountAsync(Context, new FinanceCashAccountCommand(CompanyId, "DIRECT-CASH", "Direct cash", null, FinanceCashAccountKind.Bank, "USD", revenueAccountId, null, new DateOnly(2026, 1, 1), null, Guid.NewGuid(), null, "direct-cash", "direct-cash"));
            Assert.True(cash.Succeeded, cash.Code);
            var rate = ExchangeRates.Records.Single(item => item.Id == source.RateId).Versions.Single(item => item.Id == source.CurrentVersionId);
            var created = await settlement.CreateSettlementDocumentAsync(Context, new FinanceSettlementDocumentCommand(FinancePaymentMethodDirection.Receipt, CompanyId, null, Guid.Parse("77777777-7777-7777-7777-777777777777"), cash.Value!.Id, method.Value!.Id, new DateOnly(2026, 1, 15), "USD", amount, null, rate.Rate, source.RateId, source.CurrentVersionId, rate.VersionNumber, "DIRECT-RECEIPT", "Direct receipt", Guid.NewGuid(), "direct-settlement", "direct-settlement"));
            Assert.True(created.Succeeded, created.Code);
            var submitted = await settlement.TransitionSettlementDocumentAsync(Context, new FinanceSettlementActionCommand(created.Value!.Id, created.Value.Version, null, "direct-submit", "direct-submit", FinancePaymentMethodDirection.Receipt), FinanceSettlementDocumentStatus.Submitted);
            Assert.True(submitted.Succeeded, submitted.Code);
            var posted = await settlement.PostSettlementDocumentAsync(Context, new FinanceSettlementActionCommand(submitted.Value!.Id, submitted.Value.Version, null, "direct-post", "direct-post", FinancePaymentMethodDirection.Receipt));
            Assert.True(posted.Succeeded, posted.Code);
            var allocation = await settlement.CreateAllocationAsync(Context, new FinanceAllocationCommand(posted.Value!.Id, source.OpenItemId, amount, new DateOnly(2026, 1, 15), "Direct realized FX", Guid.NewGuid(), "direct-allocation", "direct-allocation"));
            Assert.True(allocation.Succeeded, allocation.Code);
            return allocation.Value!;
        }

        internal async Task<FinanceAllocationRecord> ReverseAllocationAsync(FinanceAllocationRecord allocation)
        {
            var settlement = new FinanceSettlementPersistence(options, companies, ExchangeRates, new ActiveCustomerReader(), new UnavailableSupplierPersistence(), new TestCurrencyPaymentTermPersistence(), supplierSources, new NoApprovalPolicy());
            var result = await settlement.ReverseAllocationAsync(Context, new FinanceAllocationReversalCommand(allocation.Id, allocation.Version, "exact direct allocation reversal", Guid.NewGuid(), "direct-allocation-reverse", "direct-allocation-reverse"));
            Assert.True(result.Succeeded, result.Code);
            return result.Value!;
        }

        internal async Task AssertRealizedAllocationAsync(FinanceAllocationRecord allocation, FinanceFxDirection direction, decimal difference)
        {
            Assert.Equal(direction == FinanceFxDirection.Gain ? "Gain" : "Loss", allocation.RealizedFxDirection);
            Assert.Equal(difference, allocation.RealizedFxAmount);
            Assert.NotNull(allocation.RealizedFxJournalId);
            await using var db = new FinanceDbContext(options, Context.TenantContext);
            var journal = await db.Journals.Include(item => item.Lines).SingleAsync(item => item.Id == allocation.RealizedFxJournalId);
            Assert.Equal(journal.Lines.Sum(item => item.Debit), journal.Lines.Sum(item => item.Credit));
            Assert.Equal(journal.Lines.Sum(item => item.FunctionalDebit), journal.Lines.Sum(item => item.FunctionalCredit));
            var evidenceEntity = await db.JournalMonetaryEvidence.SingleAsync(item => item.JournalId == journal.Id);
            var evidence = JsonSerializer.Deserialize<FinanceMonetaryEvidence>(evidenceEntity.MonetaryEvidenceJson)!;
            Assert.Equal(evidence.FunctionalAmount, evidence.TransactionAmount);
            var oneSidedBalancedAmount = Math.Max(allocation.HistoricalFunctionalAmount, allocation.SettlementFunctionalAmount);
            Assert.Equal(oneSidedBalancedAmount, evidence.FunctionalAmount);
            Assert.Equal(oneSidedBalancedAmount * 2m, evidence.ReportingAmount);
            Assert.Equal(FinanceEvidenceStatus.Captured, evidence.ReportingEvidenceStatus);
            var reporting = await Persistence.ReconcileReportingCurrencyAsync(Context, CompanyId);
            Assert.Equal(FinanceEvidenceStatus.Reconciled, Assert.Single(reporting, item => item.JournalId == journal.Id).Status);
        }

        internal async Task AssertExactAllocationEvidenceAsync(Guid originalJournalId, Guid reversalJournalId)
        {
            await using var db = new FinanceDbContext(options, Context.TenantContext);
            var original = JsonSerializer.Deserialize<FinanceMonetaryEvidence>((await db.JournalMonetaryEvidence.SingleAsync(item => item.JournalId == originalJournalId)).MonetaryEvidenceJson)!;
            var reversal = JsonSerializer.Deserialize<FinanceMonetaryEvidence>((await db.JournalMonetaryEvidence.SingleAsync(item => item.JournalId == reversalJournalId)).MonetaryEvidenceJson)!;
            Assert.Equal(-original.TransactionAmount, reversal.TransactionAmount);
            Assert.Equal(-original.FunctionalAmount, reversal.FunctionalAmount);
            Assert.Equal(-original.ReportingAmount, reversal.ReportingAmount);
        }

        internal async Task AssertExactRevaluationEvidenceAsync(Guid originalJournalId, Guid reversalJournalId)
        {
            await using var db = new FinanceDbContext(options, Context.TenantContext);
            var original = JsonSerializer.Deserialize<FinanceMonetaryEvidence>((await db.JournalMonetaryEvidence.SingleAsync(item => item.JournalId == originalJournalId)).MonetaryEvidenceJson)!;
            var reversal = JsonSerializer.Deserialize<FinanceMonetaryEvidence>((await db.JournalMonetaryEvidence.SingleAsync(item => item.JournalId == reversalJournalId)).MonetaryEvidenceJson)!;
            Assert.Equal(-original.FunctionalAmount, reversal.FunctionalAmount);
            Assert.Equal(-original.TransactionAmount, reversal.TransactionAmount);
        }
        public async ValueTask DisposeAsync()
        {
            await connection.DisposeAsync();
            await masterDataConnection.DisposeAsync();
        }

        private static FinanceAccountCommand Account(string code, FinanceAccountType type) => new(CompanyId, code, code, null, null, type, true, FinanceCurrencyBehavior.TransactionCurrencyAllowed, new DateOnly(2026, 1, 1), null, Guid.NewGuid(), null, code, code);
        private static MasterDataExchangeRateRecord ExchangeRate(Guid id, Guid sourceId, Guid targetId, string source, string target, decimal rate) => new(id, new TenantId(TenantId), sourceId, targetId, source, target, MasterDataLifecycleState.Active, 1, [new MasterDataExchangeRateVersionRecord(Guid.NewGuid(), 1, new DateOnly(2026, 1, 1), null, rate, 6, ExchangeRateProvenance.Configured, "test", source, target)], [1]);

        private sealed class GrantingCapabilityResolver : IMasterDataCapabilityResolver
        {
            public IReadOnlySet<MasterDataCapability> Resolve(MasterDataRequestContext context) => Enum.GetValues<MasterDataCapability>().ToHashSet();
        }

        private sealed class EmptySupplierInvoiceSourceProvider : IFinanceSupplierInvoiceSourceProvider
        {
            public Task<FinanceSupplierInvoiceSourceRecord?> FindAsync(FinanceRequestContext context, Guid sourceEvidenceId, CancellationToken cancellationToken = default) => Task.FromResult<FinanceSupplierInvoiceSourceRecord?>(null);
        }

        private sealed class MutableSupplierInvoiceSourceProvider : IFinanceSupplierInvoiceSourceProvider
        {
            private readonly List<FinanceSupplierInvoiceSourceRecord> sources = [];
            internal void Add(FinanceSupplierInvoiceSourceRecord source) => sources.Add(source);
            public Task<FinanceSupplierInvoiceSourceRecord?> FindAsync(FinanceRequestContext context, Guid sourceEvidenceId, CancellationToken cancellationToken = default) => Task.FromResult(sources.SingleOrDefault(item => item.SourceEvidenceId == sourceEvidenceId));
            public Task<IReadOnlyList<FinanceSupplierInvoiceSourceRecord>> ListAsync(FinanceRequestContext context, Guid? companyId = null, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<FinanceSupplierInvoiceSourceRecord>>(sources.Where(item => companyId is null || item.CompanyId == companyId).ToArray());
        }

        private sealed class ActiveCustomerReader : IBusinessCustomerReferenceReader
        {
            public Task<BusinessCustomerReference?> FindCustomerReferenceAsync(TenantContext tenantContext, Guid customerId, CancellationToken cancellationToken = default) =>
                Task.FromResult<BusinessCustomerReference?>(customerId == Guid.Parse("77777777-7777-7777-7777-777777777777")
                    ? new BusinessCustomerReference(customerId, tenantContext.TenantId, "CUSTOMER-1", MasterDataLifecycleState.Active)
                    : null);
        }

        private sealed class NoApprovalPolicy : IFinanceSourceApprovalPolicy
        {
            public FinanceApprovalRequirement Resolve(string sourceContract, string sourceEvent) =>
                sourceContract is "customer-receipt.v1" ? FinanceApprovalRequirement.NotRequired : FinanceApprovalRequirement.NotConfigured;
        }
    }

    private sealed class TestCurrencyPaymentTermPersistence : IMasterDataCurrencyPaymentTermPersistence
    {
        private readonly UnavailableMasterDataCurrencyPaymentTermPersistence fallback = new();
        private static readonly MasterDataCurrencyRecord Sar = new(Fixture.SarCurrencyId, new TenantId(Fixture.TenantId), "SAR", new LocalizedName("Saudi Riyal"), MasterDataLifecycleState.Active, 1, [1]);
        private static readonly MasterDataCurrencyRecord Usd = new(Fixture.UsdCurrencyId, new TenantId(Fixture.TenantId), "USD", new LocalizedName("US Dollar"), MasterDataLifecycleState.Active, 1, [1]);
        public Task<IReadOnlyList<MasterDataCurrencyRecord>> ListCurrenciesAsync(TenantContext tenantContext, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<MasterDataCurrencyRecord>>([Sar, Usd]);
        public Task<MasterDataCurrencyRecord?> FindCurrencyAsync(TenantContext tenantContext, Guid currencyId, CancellationToken cancellationToken = default) => Task.FromResult<MasterDataCurrencyRecord?>(currencyId == Sar.Id ? Sar : currencyId == Usd.Id ? Usd : null);
        public Task<MasterDataPersistenceResult<MasterDataCurrencyRecord>> CreateCurrencyAsync(TenantContext tenantContext, Guid currencyId, CreateMasterDataCurrencyCommand command, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => fallback.CreateCurrencyAsync(tenantContext, currencyId, command, evidence, cancellationToken);
        public Task<MasterDataPersistenceResult<MasterDataCurrencyRecord>> EditCurrencyAsync(TenantContext tenantContext, EditMasterDataCurrencyCommand command, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => fallback.EditCurrencyAsync(tenantContext, command, evidence, cancellationToken);
        public Task<MasterDataPersistenceResult<MasterDataCurrencyRecord>> SetCurrencyLifecycleAsync(TenantContext tenantContext, Guid currencyId, MasterDataLifecycleState lifecycleState, byte[] expectedVersion, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => fallback.SetCurrencyLifecycleAsync(tenantContext, currencyId, lifecycleState, expectedVersion, evidence, cancellationToken);
        public Task<IReadOnlyList<MasterDataPaymentTermRecord>> ListPaymentTermsAsync(TenantContext tenantContext, CancellationToken cancellationToken = default) => fallback.ListPaymentTermsAsync(tenantContext, cancellationToken);
        public Task<MasterDataPaymentTermRecord?> FindPaymentTermAsync(TenantContext tenantContext, Guid paymentTermId, CancellationToken cancellationToken = default) => fallback.FindPaymentTermAsync(tenantContext, paymentTermId, cancellationToken);
        public Task<MasterDataPersistenceResult<MasterDataPaymentTermRecord>> CreatePaymentTermAsync(TenantContext tenantContext, Guid paymentTermId, CreateMasterDataPaymentTermCommand command, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => fallback.CreatePaymentTermAsync(tenantContext, paymentTermId, command, evidence, cancellationToken);
        public Task<MasterDataPersistenceResult<MasterDataPaymentTermRecord>> EditPaymentTermAsync(TenantContext tenantContext, EditMasterDataPaymentTermCommand command, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => fallback.EditPaymentTermAsync(tenantContext, command, evidence, cancellationToken);
        public Task<MasterDataPersistenceResult<MasterDataPaymentTermRecord>> SetPaymentTermLifecycleAsync(TenantContext tenantContext, Guid paymentTermId, MasterDataLifecycleState lifecycleState, byte[] expectedVersion, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => fallback.SetPaymentTermLifecycleAsync(tenantContext, paymentTermId, lifecycleState, expectedVersion, evidence, cancellationToken);
        public Task<MasterDataPersistenceResult<MasterDataAuditRecord>> AppendAuditAsync(TenantContext tenantContext, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => fallback.AppendAuditAsync(tenantContext, evidence, cancellationToken);
        public Task<IReadOnlyList<MasterDataAuditRecord>> ReadAuditHistoryAsync(TenantContext tenantContext, MasterDataResourceKind resourceKind, Guid? resourceId = null, CancellationToken cancellationToken = default) => fallback.ReadAuditHistoryAsync(tenantContext, resourceKind, resourceId, cancellationToken);
    }

    private sealed class TestExchangeRatePersistence : IMasterDataExchangeRatePersistence
    {
        internal List<MasterDataExchangeRateRecord> Records { get; } = [];
        internal void Add(MasterDataExchangeRateRecord record) => Records.Add(record);
        internal void Replace(MasterDataExchangeRateRecord record) { Records.RemoveAll(item => item.Id == record.Id); Records.Add(record); }
        public Task<IReadOnlyList<MasterDataExchangeRateRecord>> ListExchangeRatesAsync(TenantContext tenantContext, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<MasterDataExchangeRateRecord>>(Records);
        public Task<MasterDataExchangeRateRecord?> FindExchangeRateAsync(TenantContext tenantContext, Guid exchangeRateId, CancellationToken cancellationToken = default) => Task.FromResult<MasterDataExchangeRateRecord?>(Records.SingleOrDefault(item => item.Id == exchangeRateId));
        public Task<MasterDataPersistenceResult<MasterDataExchangeRateRecord>> CreateExchangeRateAsync(TenantContext tenantContext, Guid exchangeRateId, CreateMasterDataExchangeRateCommand command, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<MasterDataPersistenceResult<MasterDataExchangeRateRecord>> EditExchangeRateAsync(TenantContext tenantContext, EditMasterDataExchangeRateCommand command, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<MasterDataPersistenceResult<MasterDataExchangeRateRecord>> SetExchangeRateLifecycleAsync(TenantContext tenantContext, Guid exchangeRateId, MasterDataLifecycleState lifecycleState, byte[] expectedVersion, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<MasterDataPersistenceResult<MasterDataAuditRecord>> AppendAuditAsync(TenantContext tenantContext, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => throw new NotSupportedException();
        public Task<IReadOnlyList<MasterDataAuditRecord>> ReadAuditHistoryAsync(TenantContext tenantContext, Guid? exchangeRateId = null, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<MasterDataAuditRecord>>([]);
    }
}
