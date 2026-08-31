#pragma warning disable CS1591

using System.Text.Json;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.Modules.Sales;
using MiniErp.Contracts.Modules.Sales;

namespace MiniErp.Infrastructure.Persistence.Modules.Sales;

internal sealed class SalesCustomerReturnEntity : ITenantOwned
{
    private SalesCustomerReturnEntity()
    {
        CurrencyCode = string.Empty;
        EvidenceJson = "[]";
        HandoffJson = "{}";
    }

    internal SalesCustomerReturnEntity(TenantId tenantId, Guid id, SalesCustomerReturnCreateRequest request, SalesCustomerReturnSourceRecord source, Guid actorId, DateTimeOffset at)
    {
        Id = id;
        TenantId = tenantId;
        DeliveryId = source.DeliveryId;
        OrderId = source.OrderId;
        OrderRevisionNumber = source.OrderRevisionNumber;
        CompanyId = source.CompanyId;
        BranchId = source.BranchId;
        CustomerId = source.CustomerId;
        WarehouseId = source.WarehouseId;
        InvoiceId = request.InvoiceId ?? source.RecognizedInvoiceId;
        FinanceOpenItemId = source.FinanceOpenItemId;
        CurrencyCode = source.CurrencyCode;
        Status = SalesCustomerReturnStatus.Draft;
        Consequence = request.Consequence;
        ReturnDate = request.ReturnDate;
        Reason = request.Reason;
        EvidenceJson = JsonSerializer.Serialize(request.Evidence ?? []);
        HandoffJson = JsonSerializer.Serialize(new { State = "NotCommitted", Reconciliation = "Pending", RequestFingerprint = string.Empty });
        CreatedByActorId = actorId;
        CreatedAt = at;
        UpdatedAt = at;
        Version = Guid.NewGuid().ToByteArray();
    }

    internal Guid Id { get; private set; }
    public TenantId TenantId { get; private set; }
    internal Guid DeliveryId { get; private set; }
    internal Guid OrderId { get; private set; }
    internal int OrderRevisionNumber { get; private set; }
    internal Guid CompanyId { get; private set; }
    internal Guid? BranchId { get; private set; }
    internal Guid CustomerId { get; private set; }
    internal Guid WarehouseId { get; private set; }
    internal Guid? InvoiceId { get; private set; }
    internal Guid? FinanceOpenItemId { get; private set; }
    internal string CurrencyCode { get; private set; } = string.Empty;
    internal SalesCustomerReturnStatus Status { get; private set; }
    internal SalesCustomerReturnConsequence Consequence { get; private set; }
    internal DateOnly ReturnDate { get; private set; }
    internal string? Reason { get; private set; }
    internal string EvidenceJson { get; private set; } = "[]";
    internal string HandoffJson { get; private set; } = "{}";
    internal Guid CreatedByActorId { get; private set; }
    internal DateTimeOffset CreatedAt { get; private set; }
    internal DateTimeOffset UpdatedAt { get; private set; }
    internal byte[] Version { get; private set; } = [];
    internal List<SalesCustomerReturnLineEntity> Lines { get; } = [];

    internal void SetStatus(SalesCustomerReturnStatus status, DateTimeOffset at)
    {
        Status = status;
        UpdatedAt = at;
        Version = Guid.NewGuid().ToByteArray();
    }
}

internal sealed class SalesCustomerReturnLineEntity : ITenantOwned
{
    private SalesCustomerReturnLineEntity()
    {
        Reason = null;
    }

    internal SalesCustomerReturnLineEntity(TenantId tenantId, Guid id, Guid returnId, Guid deliveryId, SalesCustomerReturnLineRequest request, SalesCustomerReturnSourceLineRecord source)
    {
        Id = id;
        TenantId = tenantId;
        CustomerReturnId = returnId;
        DeliveryId = deliveryId;
        OrderLineId = request.OrderLineId;
        DeliveredQuantity = source.DeliveredQuantity;
        PreviouslyReturnedQuantity = source.AlreadyReturnedQuantity;
        ReturnQuantity = request.Quantity;
        Reason = request.Reason;
        Version = Guid.NewGuid().ToByteArray();
    }

    internal Guid Id { get; private set; }
    public TenantId TenantId { get; private set; }
    internal Guid CustomerReturnId { get; private set; }
    internal Guid DeliveryId { get; private set; }
    internal Guid OrderLineId { get; private set; }
    internal decimal DeliveredQuantity { get; private set; }
    internal decimal PreviouslyReturnedQuantity { get; private set; }
    internal decimal ReturnQuantity { get; private set; }
    internal string? Reason { get; private set; }
    internal byte[] Version { get; private set; } = [];

}

#pragma warning restore CS1591
