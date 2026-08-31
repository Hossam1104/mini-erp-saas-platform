#pragma warning disable CS1591

using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.Contracts.Modules.Inventory;

namespace MiniErp.Infrastructure.Persistence.Modules.Inventory;

internal sealed class InventoryCustomerReturnEntity : ITenantOwned
{
    private InventoryCustomerReturnEntity() { PhysicalEvidenceReference = string.Empty; InspectionEvidenceReference = string.Empty; HandoffState = string.Empty; }
    internal InventoryCustomerReturnEntity(TenantId tenantId, Guid id, Guid salesCustomerReturnId, Guid companyId, Guid? branchId, Guid warehouseId, InventoryCustomerReturnReceiptRequest request, Guid actorId, DateTimeOffset at)
    { Id = id; TenantId = tenantId; SalesCustomerReturnId = salesCustomerReturnId; CompanyId = companyId; BranchId = branchId; WarehouseId = warehouseId; Status = InventoryCustomerReturnStatus.Received; PhysicalEvidenceReference = request.PhysicalEvidenceReference.Trim(); InspectionEvidenceReference = string.Empty; HandoffState = "PhysicalReceiptRecorded"; ReceiptDate = request.ReceiptDate; ActorId = actorId; CreatedAt = at; UpdatedAt = at; Version = Guid.NewGuid().ToByteArray(); }
    internal Guid Id { get; private set; }
    public TenantId TenantId { get; private set; }
    internal Guid SalesCustomerReturnId { get; private set; }
    internal Guid CompanyId { get; private set; }
    internal Guid? BranchId { get; private set; }
    internal Guid WarehouseId { get; private set; }
    internal InventoryCustomerReturnStatus Status { get; private set; }
    internal string PhysicalEvidenceReference { get; private set; } = string.Empty;
    internal string InspectionEvidenceReference { get; private set; } = string.Empty;
    internal string HandoffState { get; private set; } = string.Empty;
    internal DateOnly? ReceiptDate { get; private set; }
    internal DateTimeOffset? PostedAt { get; private set; }
    internal Guid ActorId { get; private set; }
    internal DateTimeOffset CreatedAt { get; private set; }
    internal DateTimeOffset UpdatedAt { get; private set; }
    internal byte[] Version { get; private set; } = [];
    internal List<InventoryCustomerReturnLineEntity> Lines { get; } = [];
    internal void SetInspected(string evidence, InventoryCustomerReturnStatus status, DateTimeOffset at) { InspectionEvidenceReference = evidence.Trim(); Status = status; HandoffState = status == InventoryCustomerReturnStatus.Posted ? "Committed" : "ReconciliationRequired"; PostedAt = status == InventoryCustomerReturnStatus.Posted ? at : null; UpdatedAt = at; Version = Guid.NewGuid().ToByteArray(); }
}

internal sealed class InventoryCustomerReturnLineEntity : ITenantOwned
{
    private InventoryCustomerReturnLineEntity() { Notes = null; }
    internal InventoryCustomerReturnLineEntity(TenantId tenantId, Guid id, Guid returnId, Guid orderLineId, decimal receivedQuantity)
    { Id = id; TenantId = tenantId; InventoryCustomerReturnId = returnId; OrderLineId = orderLineId; ReceivedQuantity = receivedQuantity; DispositionedQuantity = 0m; Disposition = InventoryCustomerReturnDisposition.PendingInspection; Version = Guid.NewGuid().ToByteArray(); }
    internal Guid Id { get; private set; }
    public TenantId TenantId { get; private set; }
    internal Guid InventoryCustomerReturnId { get; private set; }
    internal Guid OrderLineId { get; private set; }
    internal decimal ReceivedQuantity { get; private set; }
    internal decimal DispositionedQuantity { get; private set; }
    internal InventoryCustomerReturnDisposition Disposition { get; private set; }
    internal Guid? MovementId { get; private set; }
    internal Guid? DeliveryMovementId { get; private set; }
    internal decimal? DeliveryUnitCost { get; private set; }
    internal string? Notes { get; private set; }
    internal byte[] Version { get; private set; } = [];
    internal void Dispose(decimal quantity, InventoryCustomerReturnDisposition disposition, string? notes, Guid? movementId, Guid? deliveryMovementId, decimal? deliveryUnitCost) { DispositionedQuantity += quantity; Disposition = disposition; Notes = notes; MovementId = movementId; DeliveryMovementId = deliveryMovementId; DeliveryUnitCost = deliveryUnitCost; Version = Guid.NewGuid().ToByteArray(); }
}

#pragma warning restore CS1591
