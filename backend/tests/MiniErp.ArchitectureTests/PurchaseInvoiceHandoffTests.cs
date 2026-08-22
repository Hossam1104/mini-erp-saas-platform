using System.Text.Json;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using MiniErp.App.BuildingBlocks.Rest;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.Modules.MasterData;
using MiniErp.App.Modules.Procurement;
using MiniErp.Contracts.Modules.MasterData;
using MiniErp.Contracts.Modules.Foundation;
using MiniErp.Contracts.Modules.Procurement;
using MiniErp.Infrastructure.Persistence.Modules.Procurement;
using Xunit;

namespace MiniErp.ArchitectureTests;

public sealed class PurchaseInvoiceHandoffTests
{
    private static readonly Guid TenantA = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid TenantB = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
    private static readonly Guid CompanyA = Guid.Parse("11111111-1111-1111-1111-111111111111");
    private static readonly Guid BranchA = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid Requester = Guid.Parse("44444444-4444-4444-4444-444444444444");
    private static readonly Guid Approver = Guid.Parse("55555555-5555-5555-5555-555555555555");
    private static readonly Guid Supplier = Guid.Parse("aaaaaaaa-1111-1111-1111-111111111111");
    private static readonly Guid Currency = Guid.Parse("aaaaaaaa-3333-3333-3333-333333333333");
    private static readonly Guid Product = Guid.Parse("77777777-7777-7777-7777-777777777777");
    private static readonly Guid Unit = Guid.Parse("88888888-8888-8888-8888-888888888888");
    private static readonly Guid WarehouseA = Guid.Parse("cccccccc-1111-1111-1111-111111111111");

    [Fact]
    public async Task Lists_eligible_accepted_lines_only_while_remaining_handoff_quantity_is_positive()
    {
        await using var fixture = await Fixture.CreateAsync();
        var receipt = await fixture.RecordedReceiptAsync("pih-eligibility");
        var receiptLine = receipt.Lines.Single();

        var before = await fixture.InvoiceHandoffService.ListEligibleSourcesAsync(
            fixture.Context(Requester, "tenant.procurement.invoice-handoff.view"));
        Assert.True(before.Succeeded, before.Code);
        var source = Assert.Single(before.Value!);
        Assert.Equal(receipt.PurchaseOrderId, source.PurchaseOrderId);
        var eligibleLine = Assert.Single(source.Lines);
        Assert.Equal(2m, eligibleLine.AcceptedQuantity);
        Assert.Equal(0m, eligibleLine.AlreadyHandedOffQuantity);
        Assert.Equal(2m, eligibleLine.RemainingHandoffQuantity);

        var fullHandoff = await fixture.InvoiceHandoffService.CreateAsync(
            fixture.Context(Requester, "tenant.procurement.invoice-handoff.create"),
            new PurchaseInvoiceHandoffCreateRequest(
                receipt.PurchaseOrderId,
                "INV-FULL-001",
                DateOnly.FromDateTime(DateTime.UtcNow.Date),
                "Full handoff notes",
                [new PurchaseInvoiceHandoffSourceRequest(receipt.Id, receiptLine.Id, 2m)]),
            "pih-full-key",
            "fp-pih-full");
        Assert.True(fullHandoff.Succeeded, fullHandoff.Code);

        var after = await fixture.InvoiceHandoffService.ListEligibleSourcesAsync(
            fixture.Context(Requester, "tenant.procurement.invoice-handoff.view"));
        Assert.True(after.Succeeded, after.Code);
        Assert.Empty(after.Value!);
    }

    [Fact]
    public async Task Legacy_handoff_without_independent_invoice_evidence_is_not_match_ready()
    {
        await using var fixture = await Fixture.CreateAsync();
        var receipt = await fixture.RecordedReceiptAsync("pih-legacy-match");
        var receiptLine = receipt.Lines.Single();
        var handoff = await fixture.InvoiceHandoffService.CreateAsync(
            fixture.Context(Requester, "tenant.procurement.invoice-handoff.create"),
            new PurchaseInvoiceHandoffCreateRequest(
                receipt.PurchaseOrderId,
                "INV-LEGACY-MATCH",
                DateOnly.FromDateTime(DateTime.UtcNow.Date),
                null,
                [new PurchaseInvoiceHandoffSourceRequest(receipt.Id, receiptLine.Id, 1m)]),
            "pih-legacy-match-key",
            "fp-pih-legacy-match");
        Assert.True(handoff.Succeeded, handoff.Code);

        var evaluation = await fixture.MatchService.EvaluateAsync(
            fixture.Context(Requester, "tenant.procurement.matching.evaluate"),
            handoff.Value!.Id,
            handoff.Value.Version,
            new PurchaseInvoiceMatchEvaluateRequest(),
            "pih-legacy-match-evaluate-key",
            "fp-pih-legacy-match-evaluate");

        Assert.True(evaluation.Succeeded, evaluation.Code);
        Assert.Equal(PurchaseInvoiceMatchResult.NotMatchReady, evaluation.Value!.Result);
        Assert.Contains(evaluation.Value.Variances, item => item.Classification == "InvoiceEvidenceMissing");
    }

    [Fact]
    public async Task Quantity_matching_uses_the_current_partial_handoff_not_the_entire_purchase_order()
    {
        await using var fixture = await Fixture.CreateAsync();
        var receipt = await fixture.RecordedReceiptWithQuantityAsync(100m, "pih-quantity-partial");
        var handoff = await CreateEvidenceHandoffAsync(fixture, receipt, 40m, 40m, 40m, "pih-quantity-partial");

        var evaluation = await fixture.MatchService.EvaluateAsync(
            fixture.Context(Requester, "tenant.procurement.matching.evaluate"),
            handoff.Id,
            handoff.Version,
            new PurchaseInvoiceMatchEvaluateRequest(),
            "pih-quantity-partial-evaluate",
            "fp-pih-quantity-partial-evaluate");

        Assert.True(evaluation.Succeeded, evaluation.Code);
        Assert.True(evaluation.Value!.Result == PurchaseInvoiceMatchResult.ExactMatch, string.Join(" | ", evaluation.Value.Variances.Select(item => $"{item.Classification}:{item.ExpectedValue}->{item.ActualValue}:{item.Variance}")));
        Assert.DoesNotContain(evaluation.Value.Variances, item => item.Classification == "QuantityVariance");
    }

    [Theory]
    [InlineData(20, 20, 20, 20, "ExactMatch", 0)]
    [InlineData(15, 20, 15, 20, "ExceptionHold", -5)]
    [InlineData(25, 20, 25, 15, "ExceptionHold", 5)]
    public async Task Multiple_declared_lines_for_one_po_line_are_quantity_aggregated(
        decimal firstDeclared,
        decimal secondDeclared,
        decimal firstAllocation,
        decimal secondAllocation,
        string expectedResult,
        decimal expectedVariance)
    {
        await using var fixture = await Fixture.CreateAsync();
        var receipt = await fixture.RecordedReceiptWithQuantityAsync(100m, $"pih-split-{expectedResult}-{firstDeclared}");
        var handoff = await CreateSplitEvidenceHandoffAsync(
            fixture,
            [receipt],
            40m,
            [firstDeclared, secondDeclared],
            [firstAllocation, secondAllocation],
            [0, 0],
            $"pih-split-{expectedResult}-{firstDeclared}");

        var evaluation = await fixture.MatchService.EvaluateAsync(
            fixture.Context(Requester, "tenant.procurement.matching.evaluate"),
            handoff.Id,
            handoff.Version,
            new PurchaseInvoiceMatchEvaluateRequest(),
            $"pih-split-{expectedResult}-{firstDeclared}-evaluate",
            $"fp-pih-split-{expectedResult}-{firstDeclared}-evaluate");

        Assert.True(evaluation.Succeeded, evaluation.Code);
        Assert.Equal(expectedResult, evaluation.Value!.Result.ToString());
        if (expectedVariance == 0m)
        {
            Assert.DoesNotContain(evaluation.Value.Variances, item => item.Classification == "QuantityVariance");
        }
        else
        {
            var variance = Assert.Single(evaluation.Value.Variances, item => item.Classification == "QuantityVariance");
            Assert.Equal(expectedVariance, variance.Variance);
            Assert.Equal(40m, variance.ExpectedValue);
            Assert.Equal(firstDeclared + secondDeclared, variance.ActualValue);
        }
    }

    [Fact]
    public async Task Split_quantity_exactly_on_configured_tolerance_boundary_is_within_tolerance()
    {
        var configured = new ConfigurationPurchaseInvoiceMatchingTolerancePolicyProvider(
            new StaticOptionsMonitor<PurchaseInvoiceMatchingPolicyOptions>(new()
            {
                TolerancePolicies =
                [
                    new PurchaseInvoiceMatchingTolerancePolicyOptions
                    {
                        TenantId = TenantA,
                        CompanyId = CompanyA,
                        BranchId = BranchA,
                        PolicyId = "split-boundary",
                        Version = 1,
                        QuantityPercentageTolerance = 2.5m,
                        EffectiveFrom = DateTimeOffset.MinValue
                    }
                ]
            }));
        await using var fixture = await Fixture.CreateAsync(configured);
        var receipt = await fixture.RecordedReceiptWithQuantityAsync(100m, "pih-split-boundary");
        var handoff = await CreateSplitEvidenceHandoffAsync(fixture, [receipt], 40m, [21m, 20m], [20m, 20m], [0, 0], "pih-split-boundary");

        var evaluation = await fixture.MatchService.EvaluateAsync(
            fixture.Context(Requester, "tenant.procurement.matching.evaluate"),
            handoff.Id,
            handoff.Version,
            new PurchaseInvoiceMatchEvaluateRequest(),
            "pih-split-boundary-evaluate",
            "fp-pih-split-boundary-evaluate");

        Assert.True(evaluation.Succeeded, evaluation.Code);
        Assert.Equal(PurchaseInvoiceMatchResult.WithinTolerance, evaluation.Value!.Result);
        Assert.Equal(1m, evaluation.Value.Variances.Single(item => item.Classification == "QuantityVariance").AllowedTolerance);
    }

    [Fact]
    public async Task Duplicate_allocations_to_one_receipt_line_are_aggregated_and_blocked()
    {
        await using var fixture = await Fixture.CreateAsync();
        var receipt = await fixture.RecordedReceiptWithQuantityAsync(100m, "pih-duplicate-receipt-allocation");
        var handoff = await CreateSplitEvidenceHandoffAsync(fixture, [receipt], 20m, [20m, 20m], [20m, 20m], [0, 0], "pih-duplicate-receipt-allocation");

        var evaluation = await fixture.MatchService.EvaluateAsync(
            fixture.Context(Requester, "tenant.procurement.matching.evaluate"),
            handoff.Id,
            handoff.Version,
            new PurchaseInvoiceMatchEvaluateRequest(),
            "pih-duplicate-receipt-allocation-evaluate",
            "fp-pih-duplicate-receipt-allocation-evaluate");

        Assert.True(evaluation.Succeeded, evaluation.Code);
        Assert.Equal(PurchaseInvoiceMatchResult.ExceptionHold, evaluation.Value!.Result);
        var variance = Assert.Single(evaluation.Value.Variances, item => item.Classification == "InvoiceAllocationExceedsSupportedQuantity");
        Assert.Equal(20m, variance.ExpectedValue);
        Assert.Equal(40m, variance.ActualValue);
    }

    [Fact]
    public async Task Valid_allocations_to_multiple_receipt_lines_are_aggregated_without_double_consumption()
    {
        await using var fixture = await Fixture.CreateAsync();
        var receipts = await fixture.RecordedReceiptPairAsync("pih-multiple-valid-receipt-lines");
        var handoff = await CreateSplitEvidenceHandoffAsync(
            fixture,
            [receipts.First, receipts.Second],
            1m,
            [1m, 1m],
            [1m, 1m],
            [0, 1],
            "pih-multiple-valid-receipt-lines");

        var evaluation = await fixture.MatchService.EvaluateAsync(
            fixture.Context(Requester, "tenant.procurement.matching.evaluate"),
            handoff.Id,
            handoff.Version,
            new PurchaseInvoiceMatchEvaluateRequest(),
            "pih-multiple-valid-receipt-lines-evaluate",
            "fp-pih-multiple-valid-receipt-lines-evaluate");

        Assert.True(evaluation.Succeeded, evaluation.Code);
        Assert.True(evaluation.Value!.Result == PurchaseInvoiceMatchResult.ExactMatch, string.Join(" | ", evaluation.Value.Variances.Select(item => $"{item.Classification}:{item.ExpectedValue}->{item.ActualValue}:{item.Variance}")));
        Assert.DoesNotContain(evaluation.Value.Variances, item => item.Classification == "InvoiceAllocationExceedsSupportedQuantity");
    }

    [Theory]
    [InlineData(39, "under")]
    [InlineData(41, "over")]
    public async Task Zero_quantity_tolerance_holds_both_supplier_under_and_over_declarations(decimal declaredQuantity, string direction)
    {
        await using var fixture = await Fixture.CreateAsync();
        var receipt = await fixture.RecordedReceiptWithQuantityAsync(100m, $"pih-quantity-{direction}");
        var handoff = await CreateEvidenceHandoffAsync(fixture, receipt, 40m, declaredQuantity, Math.Min(40m, declaredQuantity), $"pih-quantity-{direction}");

        var evaluation = await fixture.MatchService.EvaluateAsync(
            fixture.Context(Requester, "tenant.procurement.matching.evaluate"),
            handoff.Id,
            handoff.Version,
            new PurchaseInvoiceMatchEvaluateRequest(),
            $"pih-quantity-{direction}-evaluate",
            $"fp-pih-quantity-{direction}-evaluate");

        Assert.True(evaluation.Succeeded, evaluation.Code);
        Assert.Equal(PurchaseInvoiceMatchResult.ExceptionHold, evaluation.Value!.Result);
        Assert.Contains(evaluation.Value.Variances, item => item.Classification == "QuantityVariance" && item.Variance == declaredQuantity - 40m);
    }

    [Fact]
    public async Task Supplier_over_declaration_is_recordable_without_fabricating_receipt_allocation()
    {
        await using var fixture = await Fixture.CreateAsync();
        var receipt = await fixture.RecordedReceiptWithQuantityAsync(100m, "pih-quantity-over-evidence");
        var handoff = await CreateEvidenceHandoffAsync(fixture, receipt, 40m, 41m, 40m, "pih-quantity-over-evidence");

        Assert.Equal(41m, handoff.DeclaredEvidence!.Lines.Single().Quantity);
        Assert.Equal(40m, handoff.DeclaredEvidence.Lines.Single().Allocations.Single().Quantity);

        var evaluation = await fixture.MatchService.EvaluateAsync(
            fixture.Context(Requester, "tenant.procurement.matching.evaluate"),
            handoff.Id,
            handoff.Version,
            new PurchaseInvoiceMatchEvaluateRequest(),
            "pih-quantity-over-evidence-evaluate",
            "fp-pih-quantity-over-evidence-evaluate");

        Assert.True(evaluation.Succeeded, evaluation.Code);
        Assert.Equal(PurchaseInvoiceMatchResult.ExceptionHold, evaluation.Value!.Result);
        Assert.Contains(evaluation.Value.Variances, item => item.Classification == "QuantityVariance");
        Assert.DoesNotContain(evaluation.Value.Variances, item => item.Classification == "InvoiceAllocationNotEligible");
    }

    [Fact]
    public async Task Configured_runtime_quantity_tolerance_is_used_and_exact_safe_fallback_remains_zero()
    {
        var configured = new ConfigurationPurchaseInvoiceMatchingTolerancePolicyProvider(
            new StaticOptionsMonitor<PurchaseInvoiceMatchingPolicyOptions>(new PurchaseInvoiceMatchingPolicyOptions
            {
                TolerancePolicies =
                [
                    new PurchaseInvoiceMatchingTolerancePolicyOptions
                    {
                        TenantId = TenantA,
                        CompanyId = CompanyA,
                        BranchId = BranchA,
                        PolicyId = "tenant-a-quantity-policy",
                        Version = 7,
                        QuantityPercentageTolerance = 2.5m,
                        EffectiveFrom = DateTimeOffset.MinValue
                    }
                ]
            }));
        var fallback = new ConfigurationPurchaseInvoiceMatchingTolerancePolicyProvider(
            new StaticOptionsMonitor<PurchaseInvoiceMatchingPolicyOptions>(new()));

        var selected = await configured.ResolveAsync(new PurchaseRequestScope(TenantA, CompanyA, BranchA), DateTimeOffset.UtcNow);
        var exactSafe = await fallback.ResolveAsync(new PurchaseRequestScope(TenantA, CompanyA, BranchA), DateTimeOffset.UtcNow);
        Assert.Equal("tenant-a-quantity-policy", selected.PolicyId);
        Assert.Equal(2.5m, selected.QuantityPercentageTolerance);
        Assert.Equal(0m, exactSafe.QuantityAbsoluteTolerance);
        Assert.Equal(0m, exactSafe.QuantityPercentageTolerance);

        await using var fixture = await Fixture.CreateAsync(configured);
        var receipt = await fixture.RecordedReceiptWithQuantityAsync(100m, "pih-quantity-tolerance");
        var handoff = await CreateEvidenceHandoffAsync(fixture, receipt, 40m, 41m, 40m, "pih-quantity-tolerance");
        var evaluation = await fixture.MatchService.EvaluateAsync(
            fixture.Context(Requester, "tenant.procurement.matching.evaluate"),
            handoff.Id,
            handoff.Version,
            new PurchaseInvoiceMatchEvaluateRequest(),
            "pih-quantity-tolerance-evaluate",
            "fp-pih-quantity-tolerance-evaluate");

        Assert.True(evaluation.Succeeded, evaluation.Code);
        Assert.True(evaluation.Value!.Result == PurchaseInvoiceMatchResult.WithinTolerance, string.Join(" | ", evaluation.Value.Variances.Select(item => $"{item.Classification}:{item.ExpectedValue}->{item.ActualValue}:{item.Variance}:allowed={item.AllowedTolerance}")));
        Assert.Equal("tenant-a-quantity-policy", evaluation.Value.Policy.PolicyId);
        Assert.Equal(1m, evaluation.Value.Variances.Single(item => item.Classification == "QuantityVariance").AllowedTolerance);

        var outsideHandoff = await CreateEvidenceHandoffAsync(fixture, receipt, 40m, 42m, 40m, "pih-quantity-tolerance-outside");
        var outsideEvaluation = await fixture.MatchService.EvaluateAsync(
            fixture.Context(Requester, "tenant.procurement.matching.evaluate"),
            outsideHandoff.Id,
            outsideHandoff.Version,
            new PurchaseInvoiceMatchEvaluateRequest(),
            "pih-quantity-tolerance-outside-evaluate",
            "fp-pih-quantity-tolerance-outside-evaluate");
        Assert.True(outsideEvaluation.Succeeded, outsideEvaluation.Code);
        Assert.Equal(PurchaseInvoiceMatchResult.ExceptionHold, outsideEvaluation.Value!.Result);
        Assert.Equal(2m, outsideEvaluation.Value.Variances.Single(item => item.Classification == "QuantityVariance").Variance);
    }

    [Fact]
    public async Task Cumulative_active_declared_quantity_exceeding_accepted_quantity_is_a_blocking_exception()
    {
        await using var fixture = await Fixture.CreateAsync();
        var receipt = await fixture.RecordedReceiptWithQuantityAsync(100m, "pih-quantity-cumulative");
        var first = await CreateEvidenceHandoffAsync(fixture, receipt, 40m, 60m, 40m, "pih-quantity-cumulative-1");
        var second = await CreateEvidenceHandoffAsync(fixture, receipt, 40m, 60m, 40m, "pih-quantity-cumulative-2");

        var evaluation = await fixture.MatchService.EvaluateAsync(
            fixture.Context(Requester, "tenant.procurement.matching.evaluate"),
            second.Id,
            second.Version,
            new PurchaseInvoiceMatchEvaluateRequest(),
            "pih-quantity-cumulative-evaluate",
            "fp-pih-quantity-cumulative-evaluate");

        Assert.True(evaluation.Succeeded, evaluation.Code);
        Assert.Equal(PurchaseInvoiceMatchResult.ExceptionHold, evaluation.Value!.Result);
        Assert.Contains(evaluation.Value.Variances, item => item.Classification == "CumulativeQuantityLimitExceeded" && item.ExpectedValue == 100m && item.ActualValue == 120m);

        var firstEvaluation = await fixture.MatchService.EvaluateAsync(
            fixture.Context(Requester, "tenant.procurement.matching.evaluate"),
            first.Id,
            first.Version,
            new PurchaseInvoiceMatchEvaluateRequest(),
            "pih-quantity-cumulative-first-evaluate",
            "fp-pih-quantity-cumulative-first-evaluate");
        Assert.True(firstEvaluation.Succeeded, firstEvaluation.Code);
        Assert.Equal(PurchaseInvoiceMatchResult.ExceptionHold, firstEvaluation.Value!.Result);
    }

    [Fact]
    public async Task Rejected_receipt_quantity_does_not_expand_matching_eligibility()
    {
        await using var fixture = await Fixture.CreateAsync();
        var receipt = await fixture.RecordedReceiptAsync("pih-rejected-not-eligible", acceptedQuantity: 1m, rejectedQuantity: 1m);
        Assert.Equal(1m, receipt.Lines.Single().AcceptedQuantity);
        Assert.Equal(1m, receipt.Lines.Single().RejectedQuantity);
        var handoff = await CreateEvidenceHandoffAsync(fixture, receipt, 1m, 1m, 1m, "pih-rejected-not-eligible");

        var evaluation = await fixture.MatchService.EvaluateAsync(
            fixture.Context(Requester, "tenant.procurement.matching.evaluate"),
            handoff.Id,
            handoff.Version,
            new PurchaseInvoiceMatchEvaluateRequest(),
            "pih-rejected-not-eligible-evaluate",
            "fp-pih-rejected-not-eligible-evaluate");

        Assert.True(evaluation.Succeeded, evaluation.Code);
        Assert.Equal(PurchaseInvoiceMatchResult.ExceptionHold, evaluation.Value!.Result);
        Assert.DoesNotContain(evaluation.Value.Variances, item => item.Classification == "QuantityVariance");
    }

    [Fact]
    public async Task Cancelled_handoff_is_excluded_from_cumulative_current_quantity()
    {
        await using var fixture = await Fixture.CreateAsync();
        var receipt = await fixture.RecordedReceiptWithQuantityAsync(100m, "pih-cancelled-handoff-quantity");
        var cancelled = await CreateEvidenceHandoffAsync(fixture, receipt, 40m, 60m, 40m, "pih-cancelled-handoff-quantity-cancelled");
        var cancellation = await fixture.InvoiceHandoffService.CancelAsync(
            fixture.Context(Requester, "tenant.procurement.invoice-handoff.cancel"),
            cancelled.Id,
            cancelled.Version,
            "Cancelled supplier invoice handoff.",
            "pih-cancelled-handoff-quantity-cancel",
            "fp-pih-cancelled-handoff-quantity-cancel");
        Assert.True(cancellation.Succeeded, cancellation.Code);

        var current = await CreateEvidenceHandoffAsync(fixture, receipt, 40m, 40m, 40m, "pih-cancelled-handoff-quantity-current");
        var evaluation = await fixture.MatchService.EvaluateAsync(
            fixture.Context(Requester, "tenant.procurement.matching.evaluate"),
            current.Id,
            current.Version,
            new PurchaseInvoiceMatchEvaluateRequest(),
            "pih-cancelled-handoff-quantity-evaluate",
            "fp-pih-cancelled-handoff-quantity-evaluate");

        Assert.True(evaluation.Succeeded, evaluation.Code);
        Assert.Equal(PurchaseInvoiceMatchResult.ExactMatch, evaluation.Value!.Result);
        Assert.DoesNotContain(evaluation.Value.Variances, item => item.Classification == "CumulativeQuantityLimitExceeded");
    }

    [Fact]
    public async Task Cancelled_receipt_is_excluded_from_current_accepted_quantity()
    {
        await using var fixture = await Fixture.CreateAsync();
        var receipts = await fixture.RecordedReceiptPairAsync("pih-cancelled-receipt-quantity");
        var cancelled = await CreateEvidenceHandoffAsync(fixture, receipts.First, 1m, 1m, 1m, "pih-cancelled-receipt-quantity-cancelled");
        var current = await CreateEvidenceHandoffAsync(fixture, receipts.Second, 1m, 2m, 1m, "pih-cancelled-receipt-quantity-current");

        var handoffCancellation = await fixture.InvoiceHandoffService.CancelAsync(
            fixture.Context(Requester, "tenant.procurement.invoice-handoff.cancel"),
            cancelled.Id,
            cancelled.Version,
            "Cancelled before cancelling the referenced receipt.",
            "pih-cancelled-receipt-handoff-cancel",
            "fp-pih-cancelled-receipt-handoff-cancel");
        Assert.True(handoffCancellation.Succeeded, handoffCancellation.Code);
        var currentReceipt = await fixture.GoodsReceiptService.GetAsync(
            fixture.Context(Requester, "tenant.procurement.goods-receipt.view"),
            receipts.First.Id);
        Assert.True(currentReceipt.Succeeded, currentReceipt.Code);
        var receiptCancellation = await fixture.GoodsReceiptService.CancelAsync(
            fixture.Context(Requester, "tenant.procurement.goods-receipt.cancel"),
            receipts.First.Id,
            currentReceipt.Value!.Version,
            "Cancelled receipt is no longer current evidence.",
            "pih-cancelled-receipt-cancel",
            "fp-pih-cancelled-receipt-cancel");
        Assert.True(receiptCancellation.Succeeded, receiptCancellation.Code);

        var evaluation = await fixture.MatchService.EvaluateAsync(
            fixture.Context(Requester, "tenant.procurement.matching.evaluate"),
            current.Id,
            current.Version,
            new PurchaseInvoiceMatchEvaluateRequest(),
            "pih-cancelled-receipt-quantity-evaluate",
            "fp-pih-cancelled-receipt-quantity-evaluate");

        Assert.True(evaluation.Succeeded, evaluation.Code);
        Assert.Equal(PurchaseInvoiceMatchResult.ExceptionHold, evaluation.Value!.Result);
        Assert.Contains(evaluation.Value.Variances, item => item.Classification == "CumulativeQuantityLimitExceeded" && item.ExpectedValue == 1m && item.ActualValue == 2m);
        Assert.DoesNotContain(receipts.First.Id.ToString("D"), evaluation.Value.SourceSnapshot);
    }

    [Fact]
    public async Task Default_resolution_policy_requires_permission_and_reason_without_inventing_different_actor_sod()
    {
        await using var fixture = await Fixture.CreateAsync();
        var receipt = await fixture.RecordedReceiptAsync("pih-sod-default");
        var handoff = await CreateEvidenceHandoffAsync(fixture, receipt, 1m, 1m, 1m, "pih-sod-default", 11.5m);
        var evaluation = await fixture.MatchService.EvaluateAsync(
            fixture.Context(Requester, "tenant.procurement.matching.evaluate"),
            handoff.Id,
            handoff.Version,
            new PurchaseInvoiceMatchEvaluateRequest(),
            "pih-sod-default-evaluate",
            "fp-pih-sod-default-evaluate");
        Assert.True(evaluation.Succeeded, evaluation.Code);
        Assert.Equal(PurchaseInvoiceMatchResult.ExceptionHold, evaluation.Value!.Result);

        var resolved = await fixture.MatchService.ResolveAsync(
            fixture.Context(Requester, "tenant.procurement.matching.resolve"),
            evaluation.Value.Id,
            evaluation.Value.Version,
            new PurchaseInvoiceMatchResolveRequest("Reviewed by the authorized resolver."),
            "pih-sod-default-resolve",
            "fp-pih-sod-default-resolve");

        Assert.True(resolved.Succeeded, resolved.Code);
        Assert.Equal(PurchaseInvoiceMatchResult.ResolvedException, resolved.Value!.Result);
        Assert.False(resolved.Value.ResolutionPolicy!.RequireDifferentActor);
    }

    [Fact]
    public async Task Configured_different_actor_policy_denies_same_actor_and_allows_authorized_other_actor()
    {
        var policy = new ConfiguredPurchaseInvoiceMatchingResolutionPolicyProvider(
        [
            new PurchaseInvoiceMatchingResolutionPolicyBinding(
                new PurchaseRequestScope(TenantA, CompanyA, BranchA),
                new PurchaseInvoiceMatchingResolutionPolicyDefinition(
                    "tenant-a-separation-of-duties",
                    4,
                    true,
                    true,
                    true,
                    DateTimeOffset.MinValue,
                    null))
        ]);
        await using var fixture = await Fixture.CreateAsync(resolutionPolicies: policy);
        var receipt = await fixture.RecordedReceiptAsync("pih-sod-configured");
        var handoff = await CreateEvidenceHandoffAsync(fixture, receipt, 1m, 1m, 1m, "pih-sod-configured", 11.5m);
        var evaluation = await fixture.MatchService.EvaluateAsync(
            fixture.Context(Requester, "tenant.procurement.matching.evaluate"),
            handoff.Id,
            handoff.Version,
            new PurchaseInvoiceMatchEvaluateRequest(),
            "pih-sod-configured-evaluate",
            "fp-pih-sod-configured-evaluate");
        Assert.True(evaluation.Succeeded, evaluation.Code);

        var unauthorized = await fixture.MatchService.ResolveAsync(
            fixture.Context(Requester, "tenant.procurement.matching.view"),
            evaluation.Value!.Id,
            evaluation.Value.Version,
            new PurchaseInvoiceMatchResolveRequest("A read-only permission cannot resolve an exception."),
            "pih-sod-configured-unauthorized",
            "fp-pih-sod-configured-unauthorized");
        Assert.False(unauthorized.Succeeded);
        Assert.Equal("permission_denied", unauthorized.Code);

        var sameActor = await fixture.MatchService.ResolveAsync(
            fixture.Context(Requester, "tenant.procurement.matching.resolve"),
            evaluation.Value!.Id,
            evaluation.Value.Version,
            new PurchaseInvoiceMatchResolveRequest("Same actor should be denied by configured policy."),
            "pih-sod-configured-same",
            "fp-pih-sod-configured-same");
        Assert.False(sameActor.Succeeded);
        Assert.Equal("sod_violation", sameActor.Code);

        var differentActor = await fixture.MatchService.ResolveAsync(
            fixture.Context(Approver, "tenant.procurement.matching.resolve"),
            evaluation.Value.Id,
            evaluation.Value.Version,
            new PurchaseInvoiceMatchResolveRequest("Independent authorized review completed."),
            "pih-sod-configured-other",
            "fp-pih-sod-configured-other");
        Assert.True(differentActor.Succeeded, differentActor.Code);
        Assert.True(differentActor.Value!.ResolutionPolicy!.RequireDifferentActor);
    }

    [Fact]
    public async Task Independent_declared_invoice_evidence_supports_exact_partial_match_and_preserves_its_price()
    {
        await using var fixture = await Fixture.CreateAsync();
        var receipt = await fixture.RecordedReceiptAsync("pih-independent-match");
        var receiptLine = receipt.Lines.Single();
        var declaredEvidence = new PurchaseInvoiceDeclaredEvidenceRequest(
            "INV-INDEPENDENT-MATCH",
            DateOnly.FromDateTime(DateTime.UtcNow.Date),
            "USD",
            12.5m,
            null,
            1.88m,
            14.38m,
            [new PurchaseInvoiceDeclaredEvidenceLineRequest(
                receiptLine.PurchaseOrderLineId,
                1m,
                12.5m,
                null,
                15m,
                "VAT15",
                1.88m,
                12.5m,
                14.38m,
                "Supplier declared line",
                [new PurchaseInvoiceDeclaredEvidenceAllocationRequest(receipt.Id, receiptLine.Id, 1m)])]);
        var handoff = await fixture.InvoiceHandoffService.CreateAsync(
            fixture.Context(Requester, "tenant.procurement.invoice-handoff.create"),
            new PurchaseInvoiceHandoffCreateRequest(
                receipt.PurchaseOrderId,
                "INV-INDEPENDENT-MATCH",
                DateOnly.FromDateTime(DateTime.UtcNow.Date),
                null,
                [new PurchaseInvoiceHandoffSourceRequest(receipt.Id, receiptLine.Id, 1m)],
                declaredEvidence),
            "pih-independent-match-key",
            "fp-pih-independent-match");
        Assert.True(handoff.Succeeded, handoff.Code);
        Assert.Equal(12.5m, handoff.Value!.DeclaredEvidence!.Lines.Single().UnitPrice);
        Assert.Equal("USD", handoff.Value.DeclaredEvidence.CurrencyCode);
        Assert.Equal(1m, handoff.Value.DeclaredEvidence.Lines.Single().Quantity);

        var evaluation = await fixture.MatchService.EvaluateAsync(
            fixture.Context(Requester, "tenant.procurement.matching.evaluate"),
            handoff.Value.Id,
            handoff.Value.Version,
            new PurchaseInvoiceMatchEvaluateRequest(),
            "pih-independent-match-evaluate-key",
            "fp-pih-independent-match-evaluate");

        Assert.True(evaluation.Succeeded, evaluation.Code);
        Assert.Equal(PurchaseInvoiceMatchResult.ExactMatch, evaluation.Value!.Result);
        Assert.Empty(evaluation.Value.Variances);
    }

    [Fact]
    public async Task Exact_safe_matching_holds_both_favorable_and_unfavorable_price_variances()
    {
        await using var fixture = await Fixture.CreateAsync();
        var receipt = await fixture.RecordedReceiptAsync("pih-price-variance");
        var receiptLine = receipt.Lines.Single();
        var declaredEvidence = new PurchaseInvoiceDeclaredEvidenceRequest(
            "INV-PRICE-VARIANCE",
            DateOnly.FromDateTime(DateTime.UtcNow.Date),
            "USD",
            11.5m,
            null,
            1.73m,
            13.23m,
            [new PurchaseInvoiceDeclaredEvidenceLineRequest(
                receiptLine.PurchaseOrderLineId,
                1m,
                11.5m,
                null,
                15m,
                "VAT15",
                1.73m,
                11.5m,
                13.23m,
                null,
                [new PurchaseInvoiceDeclaredEvidenceAllocationRequest(receipt.Id, receiptLine.Id, 1m)])]);
        var handoff = await fixture.InvoiceHandoffService.CreateAsync(
            fixture.Context(Requester, "tenant.procurement.invoice-handoff.create"),
            new PurchaseInvoiceHandoffCreateRequest(
                receipt.PurchaseOrderId,
                "INV-PRICE-VARIANCE",
                DateOnly.FromDateTime(DateTime.UtcNow.Date),
                null,
                [new PurchaseInvoiceHandoffSourceRequest(receipt.Id, receiptLine.Id, 1m)],
                declaredEvidence),
            "pih-price-variance-key",
            "fp-pih-price-variance");
        Assert.True(handoff.Succeeded, handoff.Code);

        var evaluation = await fixture.MatchService.EvaluateAsync(
            fixture.Context(Requester, "tenant.procurement.matching.evaluate"),
            handoff.Value!.Id,
            handoff.Value.Version,
            new PurchaseInvoiceMatchEvaluateRequest(),
            "pih-price-variance-evaluate-key",
            "fp-pih-price-variance-evaluate");

        Assert.True(evaluation.Succeeded, evaluation.Code);
        Assert.Equal(PurchaseInvoiceMatchResult.ExceptionHold, evaluation.Value!.Result);
        Assert.Contains(evaluation.Value.Variances, item => item.Classification == "PriceVariance" && item.Variance == -1m);
    }

    [Fact]
    public async Task Different_currency_without_an_applied_rate_fails_closed_as_not_match_ready()
    {
        await using var fixture = await Fixture.CreateAsync();
        var receipt = await fixture.RecordedReceiptAsync("pih-currency-hold");
        var receiptLine = receipt.Lines.Single();
        var declaredEvidence = new PurchaseInvoiceDeclaredEvidenceRequest(
            "INV-CURRENCY-HOLD",
            DateOnly.FromDateTime(DateTime.UtcNow.Date),
            "EUR",
            12.5m,
            null,
            1.88m,
            14.38m,
            [new PurchaseInvoiceDeclaredEvidenceLineRequest(
                receiptLine.PurchaseOrderLineId,
                1m,
                12.5m,
                null,
                15m,
                "VAT15",
                1.88m,
                12.5m,
                14.38m,
                null,
                [new PurchaseInvoiceDeclaredEvidenceAllocationRequest(receipt.Id, receiptLine.Id, 1m)])]);
        var handoff = await fixture.InvoiceHandoffService.CreateAsync(
            fixture.Context(Requester, "tenant.procurement.invoice-handoff.create"),
            new PurchaseInvoiceHandoffCreateRequest(
                receipt.PurchaseOrderId,
                "INV-CURRENCY-HOLD",
                DateOnly.FromDateTime(DateTime.UtcNow.Date),
                null,
                [new PurchaseInvoiceHandoffSourceRequest(receipt.Id, receiptLine.Id, 1m)],
                declaredEvidence),
            "pih-currency-hold-key",
            "fp-pih-currency-hold");
        Assert.True(handoff.Succeeded, handoff.Code);

        var evaluation = await fixture.MatchService.EvaluateAsync(
            fixture.Context(Requester, "tenant.procurement.matching.evaluate"),
            handoff.Value!.Id,
            handoff.Value.Version,
            new PurchaseInvoiceMatchEvaluateRequest(),
            "pih-currency-hold-evaluate-key",
            "fp-pih-currency-hold-evaluate");

        Assert.True(evaluation.Succeeded, evaluation.Code);
        Assert.Equal(PurchaseInvoiceMatchResult.NotMatchReady, evaluation.Value!.Result);
        Assert.Contains(evaluation.Value.Variances, item => item.Classification == "CurrencyNotComparable");
    }

    [Fact]
    public async Task Valid_tenant_owned_exchange_rate_reference_is_resolved_and_snapshotted_by_matching()
    {
        var exchangeRateId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaa4101");
        var exchangeRateVersionId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaa4102");
        var effectiveOn = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var serverSnapshot = new PurchaseInvoiceMatchExchangeRateRecord(
            exchangeRateId,
            exchangeRateVersionId,
            3,
            "EUR",
            "USD",
            1.25m,
            1,
            "Configured",
            "MESP-120-master-data",
            effectiveOn,
            effectiveOn.AddDays(-1),
            null);
        var provider = new StaticExchangeRateReferenceProvider(serverSnapshot);
        await using var fixture = await Fixture.CreateAsync(exchangeRates: provider);
        var receipt = await fixture.RecordedReceiptWithQuantityAsync(100m, "pih-currency-server-owned");
        var handoff = await CreateEvidenceHandoffAsync(
            fixture,
            receipt,
            1m,
            1m,
            1m,
            "pih-currency-server-owned",
            declaredUnitPrice: 10m,
            currencyCode: "EUR");

        var evaluation = await fixture.MatchService.EvaluateAsync(
            fixture.Context(Requester, "tenant.procurement.matching.evaluate"),
            handoff.Id,
            handoff.Version,
            new PurchaseInvoiceMatchEvaluateRequest(
                new PurchaseInvoiceExchangeRateReferenceRequest(exchangeRateId)),
            "pih-currency-server-owned-evaluate",
            "fp-pih-currency-server-owned-evaluate");

        Assert.True(evaluation.Succeeded, evaluation.Code);
        Assert.Equal(PurchaseInvoiceMatchResult.ExactMatch, evaluation.Value!.Result);
        Assert.Equal(serverSnapshot, evaluation.Value.AppliedExchangeRate);
        Assert.Empty(evaluation.Value.Variances);

        provider.Snapshot = serverSnapshot with { VersionNumber = 4, Rate = 2m, EffectiveOn = effectiveOn };
        var reread = await fixture.MatchService.GetAsync(
            fixture.Context(Requester, "tenant.procurement.matching.view"),
            evaluation.Value.Id);
        Assert.True(reread.Succeeded, reread.Code);
        Assert.Equal(serverSnapshot, reread.Value!.AppliedExchangeRate);
    }

    [Fact]
    public async Task Exception_resolution_replays_the_original_response_after_the_state_transition()
    {
        await using var fixture = await Fixture.CreateAsync();
        var receipt = await fixture.RecordedReceiptAsync("pih-resolution-replay");
        var receiptLine = receipt.Lines.Single();
        var declaredEvidence = new PurchaseInvoiceDeclaredEvidenceRequest(
            "INV-RESOLUTION-REPLAY",
            DateOnly.FromDateTime(DateTime.UtcNow.Date),
            "USD",
            11.5m,
            null,
            1.73m,
            13.23m,
            [new PurchaseInvoiceDeclaredEvidenceLineRequest(
                receiptLine.PurchaseOrderLineId,
                1m,
                11.5m,
                null,
                15m,
                "VAT15",
                1.73m,
                11.5m,
                13.23m,
                null,
                [new PurchaseInvoiceDeclaredEvidenceAllocationRequest(receipt.Id, receiptLine.Id, 1m)])]);
        var handoff = await fixture.InvoiceHandoffService.CreateAsync(
            fixture.Context(Requester, "tenant.procurement.invoice-handoff.create"),
            new PurchaseInvoiceHandoffCreateRequest(
                receipt.PurchaseOrderId,
                "INV-RESOLUTION-REPLAY",
                DateOnly.FromDateTime(DateTime.UtcNow.Date),
                null,
                [new PurchaseInvoiceHandoffSourceRequest(receipt.Id, receiptLine.Id, 1m)],
                declaredEvidence),
            "pih-resolution-replay-key",
            "fp-pih-resolution-replay");
        Assert.True(handoff.Succeeded, handoff.Code);
        var evaluation = await fixture.MatchService.EvaluateAsync(
            fixture.Context(Requester, "tenant.procurement.matching.evaluate"),
            handoff.Value!.Id,
            handoff.Value.Version,
            new PurchaseInvoiceMatchEvaluateRequest(),
            "pih-resolution-replay-evaluate-key",
            "fp-pih-resolution-replay-evaluate");
        Assert.True(evaluation.Succeeded, evaluation.Code);

        var resolverContext = fixture.Context(Approver, "tenant.procurement.matching.resolve");
        var first = await fixture.MatchService.ResolveAsync(
            resolverContext,
            evaluation.Value!.Id,
            evaluation.Value.Version,
            new PurchaseInvoiceMatchResolveRequest("Reviewed supplier evidence"),
            "pih-resolution-replay-resolve-key",
            "fp-pih-resolution-replay-resolve");
        var replay = await fixture.MatchService.ResolveAsync(
            resolverContext,
            evaluation.Value.Id,
            evaluation.Value.Version,
            new PurchaseInvoiceMatchResolveRequest("Reviewed supplier evidence"),
            "pih-resolution-replay-resolve-key",
            "fp-pih-resolution-replay-resolve");

        Assert.True(first.Succeeded, first.Code);
        Assert.True(replay.Succeeded, replay.Code);
        Assert.Equal(PurchaseInvoiceMatchResult.ResolvedException, first.Value!.Result);
        Assert.Equal(first.Value.Id, replay.Value!.Id);
        Assert.Equal(first.Value.Version, replay.Value.Version);
        Assert.Equal(first.Value.ResolutionPolicy!.PolicyId, replay.Value.ResolutionPolicy!.PolicyId);
    }

    [Fact]
    public async Task Creates_invoice_handoff_with_exact_prorata_tax_recalculation()
    {
        await using var fixture = await Fixture.CreateAsync();
        var receipt = await fixture.RecordedReceiptAsync("pih-tax");
        var receiptLine = receipt.Lines.Single();

        // 1 unit at 12.50 unit price with 15% tax:
        // subtotal = 12.50, tax = 12.50 * 0.15 = 1.875 -> 1.88, line total = 14.38
        var partialHandoff = await fixture.InvoiceHandoffService.CreateAsync(
            fixture.Context(Requester, "tenant.procurement.invoice-handoff.create"),
            new PurchaseInvoiceHandoffCreateRequest(
                receipt.PurchaseOrderId,
                "INV-TAX-001",
                DateOnly.FromDateTime(DateTime.UtcNow.Date),
                "Partial pro-rata tax verification",
                [new PurchaseInvoiceHandoffSourceRequest(receipt.Id, receiptLine.Id, 1m)]),
            "pih-tax-key",
            "fp-pih-tax");
        Assert.True(partialHandoff.Succeeded, partialHandoff.Code);
        var record = partialHandoff.Value!;
        Assert.Equal(PurchaseInvoiceHandoffStatus.Recorded, record.Status);
        Assert.Equal("INV-TAX-001", record.SupplierInvoiceReference);

        var line = Assert.Single(record.Lines);
        Assert.Equal(1m, line.HandoffQuantity);
        Assert.Equal(12.5m, line.UnitPrice);
        Assert.Equal(15m, line.TaxRatePercentage);
        Assert.Equal(1.88m, line.TaxAmount);
        Assert.Equal(14.38m, line.LineAmount);

        var src = Assert.Single(record.Sources);
        Assert.Equal(receipt.Id, src.GoodsReceiptId);
        Assert.Equal(receiptLine.Id, src.GoodsReceiptLineId);
        Assert.Equal(1m, src.Quantity);
    }

    [Fact]
    public async Task Records_partial_handoffs_sequentially_until_the_remainder_is_exhausted()
    {
        await using var fixture = await Fixture.CreateAsync();
        var receipt = await fixture.RecordedReceiptAsync("pih-partial");
        var receiptLine = receipt.Lines.Single();

        var handoff1 = await fixture.InvoiceHandoffService.CreateAsync(
            fixture.Context(Requester, "tenant.procurement.invoice-handoff.create"),
            new PurchaseInvoiceHandoffCreateRequest(
                receipt.PurchaseOrderId,
                "INV-P1",
                DateOnly.FromDateTime(DateTime.UtcNow.Date),
                null,
                [new PurchaseInvoiceHandoffSourceRequest(receipt.Id, receiptLine.Id, 1m)]),
            "pih-p1-key",
            "fp-pih-p1");
        Assert.True(handoff1.Succeeded, handoff1.Code);

        var midway = await fixture.InvoiceHandoffService.ListEligibleSourcesAsync(
            fixture.Context(Requester, "tenant.procurement.invoice-handoff.view"));
        Assert.True(midway.Succeeded, midway.Code);
        var midwaySource = Assert.Single(midway.Value!);
        var midwayLine = Assert.Single(midwaySource.Lines);
        Assert.Equal(1m, midwayLine.AlreadyHandedOffQuantity);
        Assert.Equal(1m, midwayLine.RemainingHandoffQuantity);

        // Attempting to hand off 2 units when only 1 unit remains must fail
        var overHandoff = await fixture.InvoiceHandoffService.CreateAsync(
            fixture.Context(Requester, "tenant.procurement.invoice-handoff.create"),
            new PurchaseInvoiceHandoffCreateRequest(
                receipt.PurchaseOrderId,
                "INV-OVER",
                DateOnly.FromDateTime(DateTime.UtcNow.Date),
                null,
                [new PurchaseInvoiceHandoffSourceRequest(receipt.Id, receiptLine.Id, 2m)]),
            "pih-over-key",
            "fp-pih-over");
        Assert.False(overHandoff.Succeeded);
        Assert.Equal("over_handoff_not_allowed", overHandoff.Code);

        // Hand off the remaining 1 unit
        var handoff2 = await fixture.InvoiceHandoffService.CreateAsync(
            fixture.Context(Requester, "tenant.procurement.invoice-handoff.create"),
            new PurchaseInvoiceHandoffCreateRequest(
                receipt.PurchaseOrderId,
                "INV-P2",
                DateOnly.FromDateTime(DateTime.UtcNow.Date),
                null,
                [new PurchaseInvoiceHandoffSourceRequest(receipt.Id, receiptLine.Id, 1m)]),
            "pih-p2-key",
            "fp-pih-p2");
        Assert.True(handoff2.Succeeded, handoff2.Code);

        var list = await fixture.InvoiceHandoffService.ListAsync(
            fixture.Context(Requester, "tenant.procurement.invoice-handoff.view"),
            null);
        Assert.True(list.Succeeded, list.Code);
        Assert.Equal(2, list.Value!.Count);
    }

    [Fact]
    public async Task Denies_cross_tenant_reads_and_authorizes_within_the_recording_tenant()
    {
        await using var fixture = await Fixture.CreateAsync();
        var receipt = await fixture.RecordedReceiptAsync("pih-cross-tenant");
        var receiptLine = receipt.Lines.Single();

        var created = await fixture.InvoiceHandoffService.CreateAsync(
            fixture.Context(Requester, "tenant.procurement.invoice-handoff.create"),
            new PurchaseInvoiceHandoffCreateRequest(
                receipt.PurchaseOrderId,
                "INV-CROSS-1",
                DateOnly.FromDateTime(DateTime.UtcNow.Date),
                null,
                [new PurchaseInvoiceHandoffSourceRequest(receipt.Id, receiptLine.Id, 2m)]),
            "pih-cross-key",
            "fp-pih-cross");
        Assert.True(created.Succeeded, created.Code);

        var foreign = await fixture.InvoiceHandoffService.GetAsync(
            fixture.Context(Requester, "tenant.procurement.invoice-handoff.view", TenantB),
            created.Value!.Id);
        Assert.False(foreign.Succeeded);
        Assert.Equal("invoice_handoff_not_found", foreign.Code);

        var owned = await fixture.InvoiceHandoffService.GetAsync(
            fixture.Context(Requester, "tenant.procurement.invoice-handoff.view"),
            created.Value.Id);
        Assert.True(owned.Succeeded, owned.Code);
        Assert.Equal(created.Value.Id, owned.Value!.Id);
    }

    [Fact]
    public async Task Enforces_optimistic_concurrency_and_durable_idempotent_replay_on_cancel()
    {
        await using var fixture = await Fixture.CreateAsync();
        var receipt = await fixture.RecordedReceiptAsync("pih-concurrency");
        var receiptLine = receipt.Lines.Single();

        var created = await fixture.InvoiceHandoffService.CreateAsync(
            fixture.Context(Requester, "tenant.procurement.invoice-handoff.create"),
            new PurchaseInvoiceHandoffCreateRequest(
                receipt.PurchaseOrderId,
                "INV-CONCUR-1",
                DateOnly.FromDateTime(DateTime.UtcNow.Date),
                null,
                [new PurchaseInvoiceHandoffSourceRequest(receipt.Id, receiptLine.Id, 2m)]),
            "pih-concur-create",
            "fp-pih-concur-create");
        Assert.True(created.Succeeded, created.Code);

        var staleVersion = new byte[] { 9, 8, 7, 6, 5, 4, 3, 2 };
        var staleCancel = await fixture.InvoiceHandoffService.CancelAsync(
            fixture.Context(Requester, "tenant.procurement.invoice-handoff.cancel"),
            created.Value!.Id,
            staleVersion,
            "Stale attempt",
            "pih-concur-stale",
            "fp-pih-concur-stale");
        Assert.False(staleCancel.Succeeded);
        Assert.Equal("concurrency_conflict", staleCancel.Code);

        const string sharedKey = "pih-shared-cancel-key";
        var firstCancel = await fixture.InvoiceHandoffService.CancelAsync(
            fixture.Context(Requester, "tenant.procurement.invoice-handoff.cancel"),
            created.Value.Id,
            created.Value.Version,
            "Cancellation reason",
            sharedKey,
            "fp-pih-cancel-payload");
        Assert.True(firstCancel.Succeeded, firstCancel.Code);
        Assert.Equal(PurchaseInvoiceHandoffStatus.Cancelled, firstCancel.Value!.Status);

        // Identical replay
        var replay = await fixture.InvoiceHandoffService.CancelAsync(
            fixture.Context(Requester, "tenant.procurement.invoice-handoff.cancel"),
            created.Value.Id,
            created.Value.Version,
            "Cancellation reason",
            sharedKey,
            "fp-pih-cancel-payload");
        Assert.True(replay.Succeeded, replay.Code);
        Assert.Equal(firstCancel.Value.Version, replay.Value!.Version);

        // Conflicting payload with same idempotency key
        var conflicting = await fixture.InvoiceHandoffService.CancelAsync(
            fixture.Context(Requester, "tenant.procurement.invoice-handoff.cancel"),
            created.Value.Id,
            created.Value.Version,
            "Different reason",
            sharedKey,
            "fp-pih-cancel-different-payload");
        Assert.False(conflicting.Succeeded);
        Assert.Equal("idempotency_conflict", conflicting.Code);

        var history = await fixture.InvoiceHandoffService.ReadHistoryAsync(
            fixture.Context(Requester, "tenant.procurement.invoice-handoff.history"),
            created.Value.Id);
        Assert.True(history.Succeeded, history.Code);
        Assert.Equal(1, history.Value!.Count(item => item.Action == PurchaseInvoiceHandoffHistoryAction.Cancelled));

        var audit = await fixture.InvoiceHandoffService.ReadAuditAsync(
            fixture.Context(Requester, "tenant.procurement.invoice-handoff.audit"),
            created.Value.Id);
        Assert.True(audit.Succeeded, audit.Code);
        Assert.Equal(1, audit.Value!.Count(item => item.IdempotencyKey == sharedKey));
    }

    [Fact]
    public async Task Cancelling_invoice_handoff_releases_remaining_handoff_quantity_and_never_affects_goods_receipt()
    {
        await using var fixture = await Fixture.CreateAsync();
        var receipt = await fixture.RecordedReceiptAsync("pih-release");
        var receiptLine = receipt.Lines.Single();

        var handoff = await fixture.InvoiceHandoffService.CreateAsync(
            fixture.Context(Requester, "tenant.procurement.invoice-handoff.create"),
            new PurchaseInvoiceHandoffCreateRequest(
                receipt.PurchaseOrderId,
                "INV-REL-1",
                DateOnly.FromDateTime(DateTime.UtcNow.Date),
                null,
                [new PurchaseInvoiceHandoffSourceRequest(receipt.Id, receiptLine.Id, 2m)]),
            "pih-rel-create",
            "fp-pih-rel-create");
        Assert.True(handoff.Succeeded, handoff.Code);

        var cancel = await fixture.InvoiceHandoffService.CancelAsync(
            fixture.Context(Requester, "tenant.procurement.invoice-handoff.cancel"),
            handoff.Value!.Id,
            handoff.Value.Version,
            "Mistake in reference",
            "pih-rel-cancel",
            "fp-pih-rel-cancel");
        Assert.True(cancel.Succeeded, cancel.Code);

        // Source Goods Receipt remains in Recorded status
        var receiptCheck = await fixture.GoodsReceiptService.GetAsync(
            fixture.Context(Requester, "tenant.procurement.goods-receipt.view"),
            receipt.Id);
        Assert.True(receiptCheck.Succeeded, receiptCheck.Code);
        Assert.Equal(GoodsReceiptStatus.Recorded, receiptCheck.Value!.Status);

        // Released handoff quantity is available again
        var eligible = await fixture.InvoiceHandoffService.ListEligibleSourcesAsync(
            fixture.Context(Requester, "tenant.procurement.invoice-handoff.view"));
        Assert.True(eligible.Succeeded, eligible.Code);
        var eligibleLine = Assert.Single(Assert.Single(eligible.Value!).Lines);
        Assert.Equal(2m, eligibleLine.RemainingHandoffQuantity);
    }

    [Fact]
    public async Task Concurrent_invoice_handoff_requests_prevent_atomic_over_handoff()
    {
        await using var fixture = await Fixture.CreateAsync();
        var receipt = await fixture.RecordedReceiptAsync("pih-concur-race");
        var receiptLine = receipt.Lines.Single();

        var task1 = fixture.InvoiceHandoffService.CreateAsync(
            fixture.Context(Requester, "tenant.procurement.invoice-handoff.create"),
            new PurchaseInvoiceHandoffCreateRequest(
                receipt.PurchaseOrderId,
                "INV-RACE-1",
                DateOnly.FromDateTime(DateTime.UtcNow.Date),
                null,
                [new PurchaseInvoiceHandoffSourceRequest(receipt.Id, receiptLine.Id, 2m)]),
            "pih-race-key-1",
            "fp-pih-race-1");

        var task2 = fixture.InvoiceHandoffService.CreateAsync(
            fixture.Context(Requester, "tenant.procurement.invoice-handoff.create"),
            new PurchaseInvoiceHandoffCreateRequest(
                receipt.PurchaseOrderId,
                "INV-RACE-2",
                DateOnly.FromDateTime(DateTime.UtcNow.Date),
                null,
                [new PurchaseInvoiceHandoffSourceRequest(receipt.Id, receiptLine.Id, 2m)]),
            "pih-race-key-2",
            "fp-pih-race-2");

        var results = await Task.WhenAll(task1, task2);
        var successCount = results.Count(r => r.Succeeded);
        var failureCount = results.Count(r => !r.Succeeded);

        Assert.Equal(1, successCount);
        Assert.Equal(1, failureCount);
        Assert.Contains(results.First(r => !r.Succeeded).Code, new[] { "over_handoff_not_allowed", "concurrency_conflict" });

        var list = await fixture.InvoiceHandoffService.ListAsync(
            fixture.Context(Requester, "tenant.procurement.invoice-handoff.view"),
            PurchaseInvoiceHandoffStatus.Recorded);
        Assert.True(list.Succeeded, list.Code);
        Assert.Single(list.Value!);
    }

    [Fact]
    public async Task Concurrent_invoice_handoff_requests_for_seven_units_against_remainder_of_ten_prevent_atomic_over_handoff()
    {
        await using var fixture = await Fixture.CreateAsync();
        var receipt = await fixture.RecordedReceiptWithQuantityAsync(10m, "pih-race-10-7-7");
        var receiptLine = receipt.Lines.Single();

        var task1 = fixture.InvoiceHandoffService.CreateAsync(
            fixture.Context(Requester, "tenant.procurement.invoice-handoff.create"),
            new PurchaseInvoiceHandoffCreateRequest(
                receipt.PurchaseOrderId,
                "INV-RACE-7A",
                DateOnly.FromDateTime(DateTime.UtcNow.Date),
                null,
                [new PurchaseInvoiceHandoffSourceRequest(receipt.Id, receiptLine.Id, 7m)]),
            "pih-race-7a-key",
            "fp-pih-race-7a");

        var task2 = fixture.InvoiceHandoffService.CreateAsync(
            fixture.Context(Requester, "tenant.procurement.invoice-handoff.create"),
            new PurchaseInvoiceHandoffCreateRequest(
                receipt.PurchaseOrderId,
                "INV-RACE-7B",
                DateOnly.FromDateTime(DateTime.UtcNow.Date),
                null,
                [new PurchaseInvoiceHandoffSourceRequest(receipt.Id, receiptLine.Id, 7m)]),
            "pih-race-7b-key",
            "fp-pih-race-7b");

        var results = await Task.WhenAll(task1, task2);
        var successCount = results.Count(r => r.Succeeded);
        var failureCount = results.Count(r => !r.Succeeded);

        Assert.Equal(1, successCount);
        Assert.Equal(1, failureCount);
        Assert.Contains(results.First(r => !r.Succeeded).Code, new[] { "over_handoff_not_allowed", "concurrency_conflict" });

        var list = await fixture.InvoiceHandoffService.ListAsync(
            fixture.Context(Requester, "tenant.procurement.invoice-handoff.view"),
            PurchaseInvoiceHandoffStatus.Recorded);
        Assert.True(list.Succeeded, list.Code);
        var totalHandedOff = list.Value!.Sum(item => item.LineCount);
        Assert.Equal(1, totalHandedOff);
    }

    private static async Task<PurchaseInvoiceHandoffRecord> CreateEvidenceHandoffAsync(
        Fixture fixture,
        GoodsReceiptRecord receipt,
        decimal handoffQuantity,
        decimal declaredQuantity,
        decimal allocationQuantity,
        string keyPrefix,
        decimal declaredUnitPrice = 12.5m,
        string currencyCode = "USD")
    {
        var receiptLine = receipt.Lines.Single();
        var subtotal = declaredQuantity * declaredUnitPrice;
        var tax = 0m;
        var evidence = new PurchaseInvoiceDeclaredEvidenceRequest(
            $"INV-{keyPrefix}",
            DateOnly.FromDateTime(DateTime.UtcNow.Date),
            currencyCode,
            subtotal,
            null,
            tax,
            subtotal + tax,
            [new PurchaseInvoiceDeclaredEvidenceLineRequest(
                receiptLine.PurchaseOrderLineId,
                declaredQuantity,
                declaredUnitPrice,
                null,
                null,
                null,
                tax,
                subtotal,
                subtotal + tax,
                "Supplier declared quantity evidence",
                [new PurchaseInvoiceDeclaredEvidenceAllocationRequest(receipt.Id, receiptLine.Id, allocationQuantity)])]);
        var handoff = await fixture.InvoiceHandoffService.CreateAsync(
            fixture.Context(Requester, "tenant.procurement.invoice-handoff.create"),
            new PurchaseInvoiceHandoffCreateRequest(
                receipt.PurchaseOrderId,
                $"INV-{keyPrefix}",
                DateOnly.FromDateTime(DateTime.UtcNow.Date),
                null,
                [new PurchaseInvoiceHandoffSourceRequest(receipt.Id, receiptLine.Id, handoffQuantity)],
                evidence),
            $"{keyPrefix}-create",
            $"fp-{keyPrefix}-create");
        Assert.True(handoff.Succeeded, handoff.Code);
        return handoff.Value!;
    }

    private static async Task<PurchaseInvoiceHandoffRecord> CreateSplitEvidenceHandoffAsync(
        Fixture fixture,
        IReadOnlyList<GoodsReceiptRecord> receipts,
        decimal handoffQuantityPerReceipt,
        IReadOnlyList<decimal> declaredQuantities,
        IReadOnlyList<decimal> allocationQuantities,
        IReadOnlyList<int> allocationReceiptIndexes,
        string keyPrefix,
        decimal declaredUnitPrice = 12.5m,
        string currencyCode = "USD")
    {
        Assert.NotEmpty(receipts);
        Assert.Equal(declaredQuantities.Count, allocationQuantities.Count);
        Assert.Equal(declaredQuantities.Count, allocationReceiptIndexes.Count);
        var purchaseOrderId = receipts[0].PurchaseOrderId;
        var purchaseOrderLineId = receipts[0].Lines.Single().PurchaseOrderLineId;
        var purchaseOrder = await fixture.PurchaseOrderService.GetAsync(
            fixture.Context(Requester, "tenant.procurement.purchase-order.view"),
            purchaseOrderId);
        Assert.True(purchaseOrder.Succeeded, purchaseOrder.Code);
        var purchaseOrderLine = Assert.Single(purchaseOrder.Value!.Lines, item => item.Id == purchaseOrderLineId);
        var taxRate = purchaseOrderLine.TaxRatePercentage;
        var invoiceDate = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var subtotal = declaredQuantities.Sum(quantity => quantity * declaredUnitPrice);
        var taxTotal = taxRate is { } rate ? declaredQuantities.Sum(quantity => Math.Round(quantity * declaredUnitPrice * rate / 100m, 2, MidpointRounding.AwayFromZero)) : 0m;
        var evidenceLines = declaredQuantities.Select((quantity, index) =>
        {
            var receipt = receipts[allocationReceiptIndexes[index]];
            var net = quantity * declaredUnitPrice;
            var tax = taxRate is { } rate ? Math.Round(net * rate / 100m, 2, MidpointRounding.AwayFromZero) : 0m;
            return new PurchaseInvoiceDeclaredEvidenceLineRequest(
                purchaseOrderLineId,
                quantity,
                declaredUnitPrice,
                null,
                taxRate,
                purchaseOrderLine.TaxCode,
                tax,
                net,
                net + tax,
                $"Supplier split line {index + 1}",
                [new PurchaseInvoiceDeclaredEvidenceAllocationRequest(receipt.Id, receipt.Lines.Single().Id, allocationQuantities[index])]);
        }).ToArray();
        var evidence = new PurchaseInvoiceDeclaredEvidenceRequest(
            $"INV-{keyPrefix}",
            invoiceDate,
            currencyCode,
            subtotal,
            null,
            taxTotal,
            subtotal + taxTotal,
            evidenceLines);
        var sources = receipts.Select(receipt =>
            new PurchaseInvoiceHandoffSourceRequest(receipt.Id, receipt.Lines.Single().Id, handoffQuantityPerReceipt)).ToArray();
        var handoff = await fixture.InvoiceHandoffService.CreateAsync(
            fixture.Context(Requester, "tenant.procurement.invoice-handoff.create"),
            new PurchaseInvoiceHandoffCreateRequest(purchaseOrderId, $"INV-{keyPrefix}", invoiceDate, null, sources, evidence),
            $"{keyPrefix}-create",
            $"fp-{keyPrefix}-create");
        Assert.True(handoff.Succeeded, handoff.Code);
        return handoff.Value!;
    }

    private sealed class StaticOptionsMonitor<T>(T value) : IOptionsMonitor<T>
    {
        public T CurrentValue => value;
        public T Get(string? name) => value;
        public IDisposable OnChange(Action<T, string?> listener) => NoopDisposable.Instance;

        private sealed class NoopDisposable : IDisposable
        {
            internal static readonly NoopDisposable Instance = new();
            public void Dispose() { }
        }
    }

    private sealed class StaticExchangeRateReferenceProvider(PurchaseInvoiceMatchExchangeRateRecord snapshot) : IPurchaseInvoiceMatchingExchangeRateReferenceProvider
    {
        public PurchaseInvoiceMatchExchangeRateRecord Snapshot { get; set; } = snapshot;

        public Task<PurchaseInvoiceMatchExchangeRateResolution> ResolveAsync(
            TenantContext tenantContext,
            Guid exchangeRateId,
            string sourceCurrencyCode,
            string targetCurrencyCode,
            DateOnly? supplierInvoiceDate,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(
            Snapshot.ExchangeRateId == exchangeRateId
                && string.Equals(Snapshot.SourceCurrencyCode, sourceCurrencyCode, StringComparison.OrdinalIgnoreCase)
                && string.Equals(Snapshot.TargetCurrencyCode, targetCurrencyCode, StringComparison.OrdinalIgnoreCase)
                    ? PurchaseInvoiceMatchExchangeRateResolution.Success(Snapshot)
                    : PurchaseInvoiceMatchExchangeRateResolution.Failure("currency_not_comparable"));
    }

    private sealed class Fixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection;
        private readonly DbContextOptions options;

        private Fixture(
            SqliteConnection connection,
            DbContextOptions options,
            PurchaseOrderService purchaseOrderService,
            GoodsReceiptService goodsReceiptService,
            PurchaseInvoiceHandoffService invoiceHandoffService,
            PurchaseInvoiceMatchService matchService)
        {
            this.connection = connection;
            this.options = options;
            PurchaseOrderService = purchaseOrderService;
            GoodsReceiptService = goodsReceiptService;
            InvoiceHandoffService = invoiceHandoffService;
            MatchService = matchService;
        }

        public PurchaseOrderService PurchaseOrderService { get; }
        public GoodsReceiptService GoodsReceiptService { get; }
        public PurchaseInvoiceHandoffService InvoiceHandoffService { get; }
        public PurchaseInvoiceMatchService MatchService { get; }

        public static async Task<Fixture> CreateAsync(
            IPurchaseInvoiceMatchingTolerancePolicyProvider? tolerancePolicies = null,
            IPurchaseInvoiceMatchingResolutionPolicyProvider? resolutionPolicies = null,
            IPurchaseInvoiceMatchingExchangeRateReferenceProvider? exchangeRates = null)
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder().UseSqlite(connection).Options;
            await using (var db = new ProcurementDbContext(options, CreateTenantContext(TenantA, Requester)))
            {
                await db.Database.EnsureCreatedAsync();
                await SeedAsync(db);
            }

            var authorization = new PurchaseRequestAuthorizationService();
            var purchaseOrderService = new PurchaseOrderService(
                authorization,
                new PurchaseOrderPersistence(options),
                new PurchaseRequestPersistence(options),
                new SupplierQuotationPersistence(options),
                new DefaultPurchaseRequestApprovalPolicyProvider(),
                new NoPurchaseRequestApprovalDelegationProvider());
            var warehouseProvider = new ConfiguredProcurementWarehouseProvider(
            [
                new ProcurementWarehouseOption(TenantA, CompanyA, BranchA, WarehouseA, "WH-A", "Warehouse A", IsActive: true)
            ]);
            var goodsReceiptService = new GoodsReceiptService(authorization, new GoodsReceiptPersistence(options), warehouseProvider, new NoActiveGoodsReceiptInventoryEffectReader());
            var invoiceHandoffService = new PurchaseInvoiceHandoffService(authorization, new PurchaseInvoiceHandoffPersistence(options));
            var matchService = new PurchaseInvoiceMatchService(
                authorization,
                new PurchaseInvoiceHandoffPersistence(options),
                new PurchaseInvoiceMatchPersistence(options),
                tolerancePolicies ?? new ExactSafePurchaseInvoiceMatchingTolerancePolicyProvider(),
                resolutionPolicies ?? new DefaultPurchaseInvoiceMatchingResolutionPolicyProvider(),
                exchangeRates ?? new UnavailablePurchaseInvoiceMatchingExchangeRateReferenceProvider());
            return new Fixture(connection, options, purchaseOrderService, goodsReceiptService, invoiceHandoffService, matchService);
        }

        public ProcurementRequestContext Context(Guid actor, string operation, Guid tenantId = default)
        {
            tenantId = tenantId == Guid.Empty ? TenantA : tenantId;
            var foundation = FoundationRequestContext.ForTenant(actor, Guid.NewGuid(), CreateTenantContext(tenantId, actor), operation);
            var resolved = new ProcurementTenantContextResolver().Resolve(foundation);
            return Assert.IsType<ProcurementRequestContext>(resolved.Context);
        }

        public async Task<GoodsReceiptRecord> RecordedReceiptWithQuantityAsync(decimal quantity, string keyPrefix)
        {
            var now = DateTimeOffset.UtcNow;
            var scope = new PurchaseRequestScope(TenantA, CompanyA, BranchA);
            var reqId = Guid.NewGuid();
            var lineId = Guid.NewGuid();
            var quoteId = Guid.NewGuid();
            var quoteLineId = Guid.NewGuid();
            var decisionId = Guid.NewGuid();
            var policy = new PurchaseRequestApprovalPolicyDefinition("procurement.invoice-handoff.dynamic", 1, [new PurchaseRequestApprovalStageDefinition("manager", 1, 1, [], false)], true, now.AddDays(-1));

            await using (var db = new ProcurementDbContext(options, CreateTenantContext(TenantA, Requester)))
            {
                var request = new PurchaseRequestEntity(reqId, new TenantId(TenantA), CompanyA, BranchA, Requester, $"Dynamic demand {keyPrefix}", now);
                request.Lines.Add(new PurchaseRequestLineEntity(lineId, new TenantId(TenantA), request.Id, new PurchaseRequestLineSnapshot(lineId, Product, "SKU-001", "Test Product", Unit, "EA", quantity, DateOnly.FromDateTime(now.UtcDateTime.Date.AddDays(7)), "Dynamic line")));
                request.Submit(policy, JsonSerializer.Serialize(policy), now);
                request.RecordApproval(PurchaseRequestStatus.Approved, 0, 0, "[]", now);
                request.TouchVersion();

                var quoteLine = new SupplierQuotationLineSnapshot(quoteLineId, lineId, Product, "SKU-001", "Test Product", Unit, "EA", quantity, quantity, 12.5m, null, null, null, null, null, null, null, null, DateOnly.FromDateTime(now.UtcDateTime.Date.AddDays(7)), DateOnly.FromDateTime(now.UtcDateTime.Date.AddDays(9)), "9 days", "Dynamic quote line");
                var supplier = new SupplierQuotationSupplierSnapshot(Supplier, "SUP-A", "Alpha Supplier");
                var currency = new SupplierQuotationCurrencySnapshot(Currency, "USD", "US Dollar");
                var quoteCommand = new SupplierQuotationCreateCommand(quoteId, request.Id, scope, Requester, supplier, $"Q-{keyPrefix}", DateOnly.FromDateTime(now.UtcDateTime.Date), DateOnly.FromDateTime(now.UtcDateTime.Date.AddDays(14)), currency, null, "Delivered", DateOnly.FromDateTime(now.UtcDateTime.Date.AddDays(9)), "9 days", "Dynamic quote", [quoteLine], [], now, "seed");
                var quotation = new SupplierQuotationEntity(quoteCommand, new TenantId(TenantA));
                quotation.Lines.Add(new SupplierQuotationLineEntity(new TenantId(TenantA), quoteId, request.Id, quoteLine));
                quotation.SetStatus(SupplierQuotationStatus.Submitted, now);
                quotation.TouchVersion();
                var quotationRecord = new SupplierQuotationRecord(quoteId, TenantA, request.Id, scope, Requester, supplier, SupplierQuotationStatus.Submitted, $"Q-{keyPrefix}", quoteCommand.OfferDate, quoteCommand.ValidUntil, currency, null, quoteCommand.DeliveryTerms, quoteCommand.OfferedDeliveryDate, quoteCommand.OfferedDeliveryLeadTime, quoteCommand.Notes, [quoteLine], [], now, now, now, quotation.Version);
                var decisionCommand = new SupplierSourceDecisionCommand(decisionId, request.Id, scope, quoteId, Requester, now, "Selected dynamic", null, null, null, "sha256:dynamic", "{}", request.Version, $"dyn-decision-{keyPrefix}");
                var decision = new SupplierSourceDecisionEntity(decisionCommand, new TenantId(TenantA), quotationRecord);
                decision.TouchVersion();
                db.PurchaseRequests.Add(request);
                db.SupplierQuotations.Add(quotation);
                db.SupplierSourceDecisions.Add(decision);
                await db.SaveChangesAsync();
            }

            var created = await PurchaseOrderService.CreateAsync(Context(Requester, "tenant.procurement.purchase-order.create"), new PurchaseOrderCreateRequest(decisionId), $"{keyPrefix}-dyn-po-create", $"fp-{keyPrefix}-dyn-po-create");
            Assert.True(created.Succeeded, created.Code);
            var submitted = await PurchaseOrderService.SubmitAsync(Context(Requester, "tenant.procurement.purchase-order.submit"), created.Value!.Id, created.Value.Version, $"{keyPrefix}-dyn-po-submit", $"fp-{keyPrefix}-dyn-po-submit");
            Assert.True(submitted.Succeeded, submitted.Code);
            var approved = await PurchaseOrderService.ApproveAsync(Context(Approver, "tenant.procurement.purchase-order.approve"), submitted.Value!.Id, submitted.Value.Version, $"{keyPrefix}-dyn-po-approve", $"fp-{keyPrefix}-dyn-po-approve");
            Assert.True(approved.Succeeded, approved.Code);
            var issued = await PurchaseOrderService.IssueAsync(Context(Approver, "tenant.procurement.purchase-order.issue"), approved.Value!.Id, approved.Value.Version, $"{keyPrefix}-dyn-po-issue", $"fp-{keyPrefix}-dyn-po-issue");
            Assert.True(issued.Succeeded, issued.Code);

            var poLine = issued.Value!.Lines.Single();
            var confirmed = await PurchaseOrderService.RecordConfirmationAsync(
                Context(Requester, "tenant.procurement.purchase-order.confirmation.capture"),
                issued.Value.Id,
                new PurchaseOrderConfirmationRequest(
                    PurchaseOrderConfirmationStatus.Confirmed,
                    DateOnly.FromDateTime(DateTime.UtcNow.Date),
                    $"SUP-{keyPrefix}",
                    "supplier@test",
                    null,
                    null,
                    [new PurchaseOrderConfirmationLineRequest(poLine.Id, quantity, DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(7)), null, null, null, null)],
                    []),
                issued.Value.Version,
                $"{keyPrefix}-dyn-confirm",
                $"fp-{keyPrefix}-dyn-confirm");
            Assert.True(confirmed.Succeeded, confirmed.Code);

            var receipt = await GoodsReceiptService.CreateAsync(
                Context(Requester, "tenant.procurement.goods-receipt.create"),
                new GoodsReceiptCreateRequest(
                    confirmed.Value!.Id,
                    WarehouseA,
                    DateOnly.FromDateTime(DateTime.UtcNow.Date),
                    $"GRN-{keyPrefix}",
                    null,
                    [new GoodsReceiptLineCreateRequest(poLine.Id, quantity, quantity, 0m, null, null, null)]),
                $"{keyPrefix}-dyn-gr-create",
                $"fp-{keyPrefix}-dyn-gr-create");
            Assert.True(receipt.Succeeded, receipt.Code);
            return receipt.Value!;
        }

        public async Task<GoodsReceiptRecord> RecordedReceiptAsync(string keyPrefix, decimal acceptedQuantity = 2m, decimal rejectedQuantity = 0m)
        {
            var issued = await IssuedOrderAsync(keyPrefix);
            var line = issued.Lines.Single();
            var confirmed = await PurchaseOrderService.RecordConfirmationAsync(
                Context(Requester, "tenant.procurement.purchase-order.confirmation.capture"),
                issued.Id,
                new PurchaseOrderConfirmationRequest(
                    PurchaseOrderConfirmationStatus.Confirmed,
                    DateOnly.FromDateTime(DateTime.UtcNow.Date),
                    $"SUP-{keyPrefix}",
                    "supplier@test",
                    null,
                    null,
                    [new PurchaseOrderConfirmationLineRequest(line.Id, line.OrderedQuantity, DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(7)), null, null, null, null)],
                    []),
                issued.Version,
                $"{keyPrefix}-confirm",
                $"fp-{keyPrefix}-confirm");
            Assert.True(confirmed.Succeeded, confirmed.Code);

            var receipt = await GoodsReceiptService.CreateAsync(
                Context(Requester, "tenant.procurement.goods-receipt.create"),
                new GoodsReceiptCreateRequest(
                    confirmed.Value!.Id,
                    WarehouseA,
                    DateOnly.FromDateTime(DateTime.UtcNow.Date),
                    $"GRN-{keyPrefix}",
                    null,
                    [new GoodsReceiptLineCreateRequest(line.Id, acceptedQuantity + rejectedQuantity, acceptedQuantity, rejectedQuantity, null, null, null)]),
                $"{keyPrefix}-gr-create",
                $"fp-{keyPrefix}-gr-create");
            Assert.True(receipt.Succeeded, receipt.Code);
            return receipt.Value!;
        }

        public async Task<(GoodsReceiptRecord First, GoodsReceiptRecord Second)> RecordedReceiptPairAsync(string keyPrefix)
        {
            var issued = await IssuedOrderAsync(keyPrefix);
            var line = issued.Lines.Single();
            var confirmed = await PurchaseOrderService.RecordConfirmationAsync(
                Context(Requester, "tenant.procurement.purchase-order.confirmation.capture"),
                issued.Id,
                new PurchaseOrderConfirmationRequest(
                    PurchaseOrderConfirmationStatus.Confirmed,
                    DateOnly.FromDateTime(DateTime.UtcNow.Date),
                    $"SUP-{keyPrefix}",
                    "supplier@test",
                    null,
                    null,
                    [new PurchaseOrderConfirmationLineRequest(line.Id, line.OrderedQuantity, DateOnly.FromDateTime(DateTime.UtcNow.Date.AddDays(7)), null, null, null, null)],
                    []),
                issued.Version,
                $"{keyPrefix}-confirm",
                $"fp-{keyPrefix}-confirm");
            Assert.True(confirmed.Succeeded, confirmed.Code);

            async Task<GoodsReceiptRecord> CreateReceiptAsync(string suffix)
            {
                var receipt = await GoodsReceiptService.CreateAsync(
                    Context(Requester, "tenant.procurement.goods-receipt.create"),
                    new GoodsReceiptCreateRequest(
                        confirmed.Value!.Id,
                        WarehouseA,
                        DateOnly.FromDateTime(DateTime.UtcNow.Date),
                        $"GRN-{suffix}",
                        null,
                        [new GoodsReceiptLineCreateRequest(line.Id, 1m, 1m, 0m, null, null, null)]),
                    $"{suffix}-gr-create",
                    $"fp-{suffix}-gr-create");
                Assert.True(receipt.Succeeded, receipt.Code);
                return receipt.Value!;
            }

            return (await CreateReceiptAsync($"{keyPrefix}-first"), await CreateReceiptAsync($"{keyPrefix}-second"));
        }

        private async Task<PurchaseOrderRecord> IssuedOrderAsync(string keyPrefix)
        {
            var source = Assert.Single((await PurchaseOrderService.ListSourceOptionsAsync(Context(Requester, "tenant.procurement.purchase-order.view"))).Value!);
            var created = await PurchaseOrderService.CreateAsync(Context(Requester, "tenant.procurement.purchase-order.create"), new PurchaseOrderCreateRequest(source.Source.SourceDecisionId), $"{keyPrefix}-po-create", $"fp-{keyPrefix}-po-create");
            Assert.True(created.Succeeded, created.Code);
            var submitted = await PurchaseOrderService.SubmitAsync(Context(Requester, "tenant.procurement.purchase-order.submit"), created.Value!.Id, created.Value.Version, $"{keyPrefix}-po-submit", $"fp-{keyPrefix}-po-submit");
            Assert.True(submitted.Succeeded, submitted.Code);
            var approved = await PurchaseOrderService.ApproveAsync(Context(Approver, "tenant.procurement.purchase-order.approve"), submitted.Value!.Id, submitted.Value.Version, $"{keyPrefix}-po-approve", $"fp-{keyPrefix}-po-approve");
            Assert.True(approved.Succeeded, approved.Code);
            var issued = await PurchaseOrderService.IssueAsync(Context(Approver, "tenant.procurement.purchase-order.issue"), approved.Value!.Id, approved.Value.Version, $"{keyPrefix}-po-issue", $"fp-{keyPrefix}-po-issue");
            Assert.True(issued.Succeeded, issued.Code);
            return issued.Value!;
        }

        private static async Task SeedAsync(ProcurementDbContext db)
        {
            var now = DateTimeOffset.UtcNow;
            var scope = new PurchaseRequestScope(TenantA, CompanyA, BranchA);
            var lineId = Guid.Parse("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee");
            var quotationId = Guid.Parse("aaaaaaaa-aaaa-1111-1111-111111111111");
            var decisionId = Guid.Parse("aaaaaaaa-aaaa-2222-2222-222222222222");
            var policy = new PurchaseRequestApprovalPolicyDefinition("procurement.invoice-handoff.test", 1, [new PurchaseRequestApprovalStageDefinition("manager", 1, 1, [], false)], true, now.AddDays(-1));
            var request = new PurchaseRequestEntity(Guid.Parse("aaaaaaaa-aaaa-3333-3333-333333333333"), new TenantId(TenantA), CompanyA, BranchA, Requester, "Approved demand", now);
            request.Lines.Add(new PurchaseRequestLineEntity(lineId, new TenantId(TenantA), request.Id, new PurchaseRequestLineSnapshot(lineId, Product, "SKU-001", "Test Product", Unit, "EA", 2m, DateOnly.FromDateTime(now.UtcDateTime.Date.AddDays(7)), "Approved demand line")));
            request.Submit(policy, JsonSerializer.Serialize(policy), now);
            request.RecordApproval(PurchaseRequestStatus.Approved, 0, 0, "[]", now);
            request.TouchVersion();

            // Unit price 12.50, 15% tax rate percentage
            var quotationLine = new SupplierQuotationLineSnapshot(
                Guid.Parse("aaaaaaaa-aaaa-4444-4444-444444444444"),
                lineId,
                Product,
                "SKU-001",
                "Test Product",
                Unit,
                "EA",
                2m,
                2m,
                12.5m,
                null,
                null,
                Guid.Parse("aaaaaaaa-5555-5555-5555-555555555555"),
                "VAT15",
                "VAT 15%",
                15m,
                3.75m,
                null,
                DateOnly.FromDateTime(now.UtcDateTime.Date.AddDays(7)),
                DateOnly.FromDateTime(now.UtcDateTime.Date.AddDays(9)),
                "9 days",
                "Quoted line");
            var supplier = new SupplierQuotationSupplierSnapshot(Supplier, "SUP-A", "Alpha Supplier");
            var currency = new SupplierQuotationCurrencySnapshot(Currency, "USD", "US Dollar");
            var quotationCommand = new SupplierQuotationCreateCommand(quotationId, request.Id, scope, Requester, supplier, "Q-PIH-1", DateOnly.FromDateTime(now.UtcDateTime.Date), DateOnly.FromDateTime(now.UtcDateTime.Date.AddDays(14)), currency, null, "Delivered", DateOnly.FromDateTime(now.UtcDateTime.Date.AddDays(9)), "9 days", "Seed quote", [quotationLine], [], now, "seed");
            var quotation = new SupplierQuotationEntity(quotationCommand, new TenantId(TenantA));
            quotation.Lines.Add(new SupplierQuotationLineEntity(new TenantId(TenantA), quotationId, request.Id, quotationLine));
            quotation.SetStatus(SupplierQuotationStatus.Submitted, now);
            quotation.TouchVersion();
            var quotationRecord = new SupplierQuotationRecord(quotationId, TenantA, request.Id, scope, Requester, supplier, SupplierQuotationStatus.Submitted, "Q-PIH-1", quotationCommand.OfferDate, quotationCommand.ValidUntil, currency, null, quotationCommand.DeliveryTerms, quotationCommand.OfferedDeliveryDate, quotationCommand.OfferedDeliveryLeadTime, quotationCommand.Notes, [quotationLine], [], now, now, now, quotation.Version);
            var decisionCommand = new SupplierSourceDecisionCommand(decisionId, request.Id, scope, quotationId, Requester, now, "Selected for test", null, null, null, "sha256:test", "{}", request.Version, "seed-decision");
            var decision = new SupplierSourceDecisionEntity(decisionCommand, new TenantId(TenantA), quotationRecord);
            decision.TouchVersion();
            db.PurchaseRequests.Add(request);
            db.SupplierQuotations.Add(quotation);
            db.SupplierSourceDecisions.Add(decision);
            await db.SaveChangesAsync();
        }

        public async ValueTask DisposeAsync() => await connection.DisposeAsync();
    }

    private static TenantContext CreateTenantContext(Guid tenantId, Guid actor) => TenantContext.ForOrdinaryMembership(new TenantId(tenantId), new MembershipReference(Guid.NewGuid()), new ScopeReference($"Company:{CompanyA:D}"), new CorrelationId($"corr-{Guid.NewGuid():N}"), actor);
}
