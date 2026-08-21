using MiniErp.App.Modules.Procurement;
using MiniErp.Contracts.Modules.Foundation;
using MiniErp.Contracts.Modules.Procurement;
using Xunit;

namespace MiniErp.ArchitectureTests;

public sealed class SupplierReturnTests
{
    [Fact]
    public void Normalizes_return_lines_and_preserves_private_evidence_references()
    {
        var request = new SupplierReturnCreateRequest(
            Guid.NewGuid(),
            new DateOnly(2026, 8, 21),
            SupplierReturnReasonCode.Damaged,
            SupplierReturnCondition.Unusable,
            SupplierReturnCommercialOutcome.CreditExpected,
            "  Broken seal  ",
            "  Dock note  ",
            [new SupplierReturnLineCreateRequest(Guid.NewGuid(), 2m, "  Carton  ")],
            [new SupplierReturnEvidenceReferenceWriteRequest("private-object-1", "photo.png", "image/png", "  Receiving photo  ", null)]);

        var valid = SupplierReturnValuePolicy.TryNormalizeCreate(request, out _, out var reason, out var notes, out var lines, out var evidence);

        Assert.True(valid);
        Assert.Equal("Broken seal", reason);
        Assert.Equal("Dock note", notes);
        Assert.Equal("Carton", lines.Single().Notes);
        Assert.Equal("private-object-1", evidence.Single().ReferenceId);
        Assert.Equal("private-file-reference", evidence.Single().Source);
    }

    [Fact]
    public void Rejects_duplicate_lines_and_non_positive_return_quantity()
    {
        var lineId = Guid.NewGuid();
        var request = new SupplierReturnCreateRequest(
            Guid.NewGuid(),
            new DateOnly(2026, 8, 21),
            SupplierReturnReasonCode.Defective,
            SupplierReturnCondition.Reworkable,
            SupplierReturnCommercialOutcome.ReplacementExpected,
            null,
            null,
            [new SupplierReturnLineCreateRequest(lineId, 1m, null), new SupplierReturnLineCreateRequest(lineId, 0m, null)],
            []);

        Assert.False(SupplierReturnValuePolicy.TryNormalizeCreate(request, out _, out _, out _, out _, out _));
    }

    [Fact]
    public void Publishes_the_full_supplier_return_boundary_in_the_foundation_operation_catalogue()
    {
        var required = new[]
        {
            "procurement.supplier-return.eligible-source.list",
            "procurement.supplier-return.create",
            "procurement.supplier-return.submit",
            "procurement.supplier-return.approve",
            "procurement.supplier-return.inventory-handoff.record",
            "procurement.supplier-return.finance-reference.record",
            "procurement.supplier-return.correct",
            "procurement.supplier-return.reverse",
            "procurement.supplier-return.history.read",
            "procurement.supplier-return.audit.read",
            "procurement.supplier-return.report.read"
        };

        foreach (var operationId in required)
        {
            var descriptor = FoundationOperationCatalog.GetRequired(operationId);
            Assert.StartsWith("/api/v1/procurement/supplier-return", descriptor.Route, StringComparison.Ordinal);
            Assert.Equal(FoundationSecurityProfile.OrdinaryMembership, descriptor.SecurityProfile);
            Assert.Equal(FoundationScopePolicy.Tenant, descriptor.ScopePolicy);
        }
    }
}
