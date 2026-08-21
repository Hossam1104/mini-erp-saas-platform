#pragma warning disable CS1591

namespace MiniErp.Contracts.Modules.Inventory;

public enum InventoryMovementSourceType
{
    OpeningBalance = 1,
    Correction = 2
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
    byte[] Version);

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
    decimal UnitCost,
    string CurrencyCode,
    string? TrackingIdentity,
    InventoryMovementSourceType SourceType,
    Guid SourceDocumentId,
    Guid SourceLineId,
    Guid? CorrectionOfMovementId,
    DateOnly EffectiveDate,
    Guid ActorId,
    string CorrelationId,
    DateTimeOffset PostedAt,
    byte[] Version);

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

#pragma warning restore CS1591
