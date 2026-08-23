#pragma warning disable CS1591

namespace MiniErp.Contracts.Modules.Inventory;

public enum InventoryMovementSourceType
{
    OpeningBalance = 1,
    Correction = 2,
    GoodsReceipt = 3,
    WarehouseTransferShipment = 4,
    WarehouseTransferReceipt = 5,
    SupplierReturn = 6,
    CustomerReturn = 7,
    WarehouseTransferLoss = 8,
    WarehouseTransferReturn = 9,
    StockAdjustment = 10,
    InventoryCountVariance = 11,
    StockIssue = 12
}

public enum InventoryValuationStatus
{
    Known = 1,
    Pending = 2
}

public enum InventoryMovementDirection
{
    Inbound = 1,
    Outbound = 2
}

public enum InventoryOpeningBalanceStatus
{
    Draft = 1,
    Validated = 2,
    Posted = 3,
    Corrected = 4
}

public enum InventoryOpeningRowStatus
{
    Pending = 1,
    Valid = 2,
    Quarantined = 3,
    Posted = 4,
    Corrected = 5
}

public enum InventoryReservationStatus
{
    Active = 1,
    Released = 2
}

public enum InventoryReservationAction
{
    Created = 1,
    Reduced = 2,
    Released = 3
}

public enum InventoryTransferMode
{
    Direct = 1,
    InTransit = 2
}

public enum InventoryTransferStatus
{
    Draft = 1,
    Shipped = 2,
    PartiallyReceived = 3,
    Completed = 4,
    LossResolved = 5,
    Cancelled = 6
}

public enum InventoryTransferEventType
{
    Created = 1,
    DirectCompleted = 2,
    Shipped = 3,
    Received = 4,
    ShortageResolved = 5,
    Cancelled = 6
}

public sealed record InventoryWarehouseOption(
    Guid TenantId,
    Guid CompanyId,
    Guid? BranchId,
    Guid WarehouseId,
    string Code,
    string Name,
    bool IsActive = true)
{
    public string DisplayName => $"{Code} - {Name}";
}

public sealed record InventoryProductReference(
    Guid TenantId,
    Guid ProductId,
    string Sku,
    string Name,
    Guid BaseUnitOfMeasureId,
    string BaseUnitOfMeasureCode,
    bool IsActive,
    bool IsInventoryRelevant,
    bool TrackingEnabled);

public sealed record InventoryOpeningBalanceRowRequest(
    Guid ProductId,
    Guid UnitOfMeasureId,
    decimal Quantity,
    decimal UnitCost,
    string CurrencyCode,
    string? TrackingIdentity = null,
    string? SourceLineReference = null);

public sealed record InventoryOpeningBalanceCreateRequest(
    Guid CompanyId,
    Guid? BranchId,
    Guid WarehouseId,
    DateOnly AsOfDate,
    string SourceOwner,
    string SourceSystem,
    DateTimeOffset ExtractedAt,
    string? SourceReference,
    IReadOnlyList<InventoryOpeningBalanceRowRequest> Rows);

public sealed record InventoryOpeningBalanceActionRequest(string? Reason = null);

public sealed record InventoryReservationCreateRequest(
    Guid CompanyId,
    Guid? BranchId,
    Guid WarehouseId,
    Guid ProductId,
    Guid UnitOfMeasureId,
    decimal RequestedQuantity,
    string SourceType,
    string SourceReference,
    bool AllowPartialAllocation,
    string? TrackingIdentity = null);

public sealed record InventoryReservationActionRequest(
    decimal? Quantity = null,
    string? Reason = null);

public sealed record InventoryOpeningBalanceRowRecord(
    Guid Id,
    Guid ProductId,
    string ProductSku,
    string ProductName,
    Guid UnitOfMeasureId,
    string UnitOfMeasureCode,
    decimal Quantity,
    decimal UnitCost,
    string CurrencyCode,
    string? TrackingIdentity,
    string? SourceLineReference,
    InventoryOpeningRowStatus Status,
    string? ValidationCode,
    DateTimeOffset? PostedAt,
    byte[] Version,
    string SourceFingerprint = "");

public sealed record InventoryOpeningBalanceRecord(
    Guid Id,
    Guid TenantId,
    Guid CompanyId,
    Guid? BranchId,
    Guid WarehouseId,
    string WarehouseCode,
    string WarehouseName,
    DateOnly AsOfDate,
    string SourceOwner,
    string SourceSystem,
    DateTimeOffset ExtractedAt,
    string? SourceReference,
    InventoryOpeningBalanceStatus Status,
    int RowCount,
    int ValidRowCount,
    int QuarantinedRowCount,
    decimal ValidQuantityTotal,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<InventoryOpeningBalanceRowRecord> Rows,
    byte[] Version);

public sealed record InventoryOpeningBalanceHistoryRecord(
    Guid Id,
    Guid OpeningBalanceId,
    InventoryOpeningBalanceStatus FromStatus,
    InventoryOpeningBalanceStatus ToStatus,
    string Action,
    Guid ActorId,
    string? Reason,
    string CorrelationId,
    DateTimeOffset OccurredAt,
    byte[] Version);

public sealed record InventoryMovementRecord(
    Guid Id,
    Guid TenantId,
    Guid CompanyId,
    Guid? BranchId,
    Guid WarehouseId,
    string WarehouseCode,
    string WarehouseName,
    Guid ProductId,
    string ProductSku,
    string ProductName,
    Guid UnitOfMeasureId,
    string UnitOfMeasureCode,
    InventoryMovementDirection Direction,
    decimal Quantity,
    decimal? UnitCost,
    string? CurrencyCode,
    string? TrackingIdentity,
    InventoryMovementSourceType SourceType,
    Guid SourceDocumentId,
    Guid SourceLineId,
    Guid? CorrectionOfMovementId,
    DateOnly EffectiveDate,
    Guid ActorId,
    string CorrelationId,
    DateTimeOffset PostedAt,
    byte[] Version,
    InventoryValuationStatus ValuationStatus = InventoryValuationStatus.Pending,
    Guid? GoodsReceiptId = null,
    Guid? GoodsReceiptLineId = null,
    Guid? SupplierReturnId = null,
    Guid? SupplierReturnLineId = null,
    Guid? PurchaseOrderId = null,
    Guid? PurchaseOrderLineId = null,
    Guid? TransferId = null,
    Guid? TransferLineId = null,
    string? SourceReference = null);

public sealed record InventoryAvailabilityRecord(
    Guid TenantId,
    Guid CompanyId,
    Guid? BranchId,
    Guid WarehouseId,
    string WarehouseCode,
    string WarehouseName,
    Guid ProductId,
    string ProductSku,
    string ProductName,
    Guid UnitOfMeasureId,
    string UnitOfMeasureCode,
    string? TrackingIdentity,
    bool TrackingEnabled,
    decimal OnHandQuantity,
    decimal ReservedQuantity,
    decimal AvailableQuantity,
    decimal ExpectedQuantity,
    decimal DamagedQuantity,
    decimal InTransitQuantity,
    DateTimeOffset CalculatedAt,
    byte[] Version);

public sealed record InventoryReservationRecord(
    Guid Id,
    Guid TenantId,
    Guid CompanyId,
    Guid? BranchId,
    Guid WarehouseId,
    string WarehouseCode,
    string WarehouseName,
    Guid ProductId,
    string ProductSku,
    string ProductName,
    Guid UnitOfMeasureId,
    string UnitOfMeasureCode,
    string? TrackingIdentity,
    string SourceType,
    string SourceReference,
    decimal RequestedQuantity,
    decimal ReservedQuantity,
    decimal UnallocatedQuantity,
    InventoryReservationStatus Status,
    Guid ActorId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    byte[] Version);

public sealed record InventoryReservationHistoryRecord(
    Guid Id,
    Guid ReservationId,
    InventoryReservationAction Action,
    decimal Quantity,
    decimal ReservedQuantityAfter,
    decimal UnallocatedQuantityAfter,
    Guid ActorId,
    string? Reason,
    string CorrelationId,
    DateTimeOffset OccurredAt,
    byte[] Version);

public sealed record InventoryAuditRecord(
    Guid Id,
    Guid TenantId,
    string ResourceType,
    Guid ResourceId,
    string OperationId,
    Guid ActorId,
    Guid SessionId,
    string AuthorizationPath,
    string Decision,
    string? Reason,
    string CorrelationId,
    string? IdempotencyKey,
    string? RequestFingerprint,
    string? BeforeSummary,
    string? AfterSummary,
    DateTimeOffset OccurredAt,
    byte[] Version);

public sealed record InventoryGoodsReceiptPostRequest(Guid GoodsReceiptId, Guid GoodsReceiptLineId, byte[]? ExpectedVersion = null);

public sealed record InventorySupplierReturnPostRequest(Guid SupplierReturnId, byte[]? ExpectedVersion = null);

public sealed record InventoryTransferCreateRequest(
    Guid CompanyId,
    Guid? BranchId,
    Guid SourceWarehouseId,
    Guid DestinationWarehouseId,
    Guid ProductId,
    Guid UnitOfMeasureId,
    decimal Quantity,
    InventoryTransferMode Mode,
    string? TrackingIdentity = null,
    string? Reason = null);

public sealed record InventoryTransferActionRequest(
    decimal? Quantity = null,
    string? Reference = null,
    string? Reason = null);

public static class InventoryTransferReferencePolicy
{
    public static string? Normalize(string? reference)
    {
        if (string.IsNullOrWhiteSpace(reference))
        {
            return null;
        }

        var trimmed = reference.Trim();
        return trimmed[..Math.Min(trimmed.Length, 512)].ToUpperInvariant();
    }
}

public sealed record InventoryGoodsReceiptPostingRecord(
    Guid MovementId,
    Guid TenantId,
    Guid GoodsReceiptId,
    Guid GoodsReceiptLineId,
    Guid CompanyId,
    Guid? BranchId,
    Guid WarehouseId,
    string WarehouseCode,
    string WarehouseName,
    Guid ProductId,
    string ProductSku,
    string ProductName,
    Guid UnitOfMeasureId,
    string UnitOfMeasureCode,
    decimal Quantity,
    InventoryValuationStatus ValuationStatus,
    DateTimeOffset PostedAt,
    bool WasExisting = false);

public sealed record InventorySupplierReturnPostingRecord(
    Guid SupplierReturnId,
    IReadOnlyList<Guid> MovementIds,
    decimal Quantity,
    string HandoffReference,
    InventoryValuationStatus ValuationStatus,
    DateTimeOffset PostedAt,
    bool WasExisting = false,
    bool HandoffRecorded = false,
    Guid? CompanyId = null,
    Guid? BranchId = null,
    Guid? WarehouseId = null);

public sealed record InventoryTransferEventRecord(
    Guid Id,
    Guid TransferId,
    Guid TransferLineId,
    InventoryTransferEventType EventType,
    decimal Quantity,
    string? Reference,
    string? Reason,
    Guid ActorId,
    string CorrelationId,
    DateTimeOffset OccurredAt,
    Guid? SourceMovementId,
    Guid? DestinationMovementId,
    byte[] Version);

public sealed record InventoryTransferRecord(
    Guid Id,
    Guid TenantId,
    Guid CompanyId,
    Guid? BranchId,
    Guid SourceWarehouseId,
    string SourceWarehouseCode,
    string SourceWarehouseName,
    Guid DestinationWarehouseId,
    string DestinationWarehouseCode,
    string DestinationWarehouseName,
    Guid ProductId,
    string ProductSku,
    string ProductName,
    Guid UnitOfMeasureId,
    string UnitOfMeasureCode,
    decimal Quantity,
    InventoryTransferMode Mode,
    InventoryTransferStatus Status,
    string? TrackingIdentity,
    decimal ShippedQuantity,
    decimal ReceivedQuantity,
    decimal LostQuantity,
    decimal InTransitQuantity,
    decimal RemainingToShipQuantity,
    string? Reason,
    Guid ActorId,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    IReadOnlyList<InventoryTransferEventRecord> Events,
    byte[] Version);

public sealed record InventoryCustomerReturnBoundaryRecord(
    bool Available,
    string Status,
    string MessageKey,
    string? AuthoritativeSource);

#pragma warning restore CS1591
