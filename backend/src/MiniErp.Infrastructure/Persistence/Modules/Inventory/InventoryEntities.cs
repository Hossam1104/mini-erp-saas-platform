#pragma warning disable CS1591

using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.Contracts.Modules.Inventory;

namespace MiniErp.Infrastructure.Persistence.Modules.Inventory;

internal sealed class InventoryOpeningBalanceEntity : ITenantOwned
{
    private InventoryOpeningBalanceEntity() { }

    internal InventoryOpeningBalanceEntity(
        TenantId tenantId, Guid id, Guid companyId, Guid? branchId, Guid warehouseId, string warehouseCode, string warehouseName, DateOnly asOfDate,
        string sourceOwner, string sourceSystem, DateTimeOffset extractedAt, string? sourceReference, Guid actorId, DateTimeOffset occurredAt)
    {
        Id = id; TenantId = tenantId; CompanyId = companyId; BranchId = branchId; WarehouseId = warehouseId;
        WarehouseCode = warehouseCode; WarehouseName = warehouseName; AsOfDate = asOfDate; SourceOwner = sourceOwner; SourceSystem = sourceSystem; ExtractedAt = extractedAt;
        SourceReference = sourceReference; Status = InventoryOpeningBalanceStatus.Draft; CreatedAt = occurredAt; UpdatedAt = occurredAt; CreatedByActorId = actorId;
        TouchVersion();
    }

    internal Guid Id { get; private set; }
    public TenantId TenantId { get; private set; }
    internal Guid CompanyId { get; private set; }
    internal Guid? BranchId { get; private set; }
    internal Guid WarehouseId { get; private set; }
    internal string WarehouseCode { get; private set; } = string.Empty;
    internal string WarehouseName { get; private set; } = string.Empty;
    internal DateOnly AsOfDate { get; private set; }
    internal string SourceOwner { get; private set; } = string.Empty;
    internal string SourceSystem { get; private set; } = string.Empty;
    internal DateTimeOffset ExtractedAt { get; private set; }
    internal string? SourceReference { get; private set; }
    internal InventoryOpeningBalanceStatus Status { get; private set; }
    internal Guid CreatedByActorId { get; private set; }
    internal DateTimeOffset CreatedAt { get; private set; }
    internal DateTimeOffset UpdatedAt { get; private set; }
    internal byte[] Version { get; private set; } = [];
    internal List<InventoryOpeningBalanceRowEntity> Rows { get; } = [];
    internal void SetStatus(InventoryOpeningBalanceStatus status, DateTimeOffset at) { Status = status; UpdatedAt = at; }
    internal void TouchVersion() => Version = Guid.NewGuid().ToByteArray();
}

internal sealed class InventoryOpeningBalanceRowEntity : ITenantOwned
{
    private InventoryOpeningBalanceRowEntity() { }

    internal InventoryOpeningBalanceRowEntity(TenantId tenantId, Guid id, Guid batchId, Guid productId, string sku, string productName, Guid uomId, string uomCode, decimal quantity, decimal unitCost, string currencyCode, string? trackingIdentity, string? sourceLineReference, InventoryOpeningRowStatus status, string? validationCode)
    {
        Id = id; TenantId = tenantId; OpeningBalanceId = batchId; ProductId = productId; ProductSku = sku; ProductName = productName; UnitOfMeasureId = uomId; UnitOfMeasureCode = uomCode; Quantity = quantity; UnitCost = unitCost; CurrencyCode = currencyCode; TrackingIdentity = trackingIdentity; SourceLineReference = sourceLineReference; Status = status; ValidationCode = validationCode;
    }

    internal Guid Id { get; private set; }
    public TenantId TenantId { get; private set; }
    internal Guid OpeningBalanceId { get; private set; }
    internal Guid ProductId { get; private set; }
    internal string ProductSku { get; private set; } = string.Empty;
    internal string ProductName { get; private set; } = string.Empty;
    internal Guid UnitOfMeasureId { get; private set; }
    internal string UnitOfMeasureCode { get; private set; } = string.Empty;
    internal decimal Quantity { get; private set; }
    internal decimal UnitCost { get; private set; }
    internal string CurrencyCode { get; private set; } = string.Empty;
    internal string? TrackingIdentity { get; private set; }
    internal string? SourceLineReference { get; private set; }
    internal InventoryOpeningRowStatus Status { get; private set; }
    internal string? ValidationCode { get; private set; }
    internal DateTimeOffset? PostedAt { get; private set; }
    internal byte[] Version { get; private set; } = [];
    internal void Validate(InventoryOpeningRowStatus status, string? code) { Status = status; ValidationCode = code; }
    internal void MarkPosted(DateTimeOffset at) { Status = InventoryOpeningRowStatus.Posted; PostedAt = at; ValidationCode = null; }
    internal void MarkCorrected() => Status = InventoryOpeningRowStatus.Corrected;
}

internal sealed class InventoryOpeningBalanceHistoryEntity : ITenantOwned
{
    private InventoryOpeningBalanceHistoryEntity() { }
    internal InventoryOpeningBalanceHistoryEntity(TenantId tenantId, Guid id, Guid batchId, InventoryOpeningBalanceStatus fromStatus, InventoryOpeningBalanceStatus toStatus, string action, Guid actorId, string? reason, string correlationId, DateTimeOffset occurredAt)
    { Id = id; TenantId = tenantId; OpeningBalanceId = batchId; FromStatus = fromStatus; ToStatus = toStatus; Action = action; ActorId = actorId; Reason = reason; CorrelationId = correlationId; OccurredAt = occurredAt; }
    internal Guid Id { get; private set; }
    public TenantId TenantId { get; private set; }
    internal Guid OpeningBalanceId { get; private set; }
    internal InventoryOpeningBalanceStatus FromStatus { get; private set; }
    internal InventoryOpeningBalanceStatus ToStatus { get; private set; }
    internal string Action { get; private set; } = string.Empty;
    internal Guid ActorId { get; private set; }
    internal string? Reason { get; private set; }
    internal string CorrelationId { get; private set; } = string.Empty;
    internal DateTimeOffset OccurredAt { get; private set; }
    internal byte[] Version { get; private set; } = [];
}

internal sealed class InventoryStockMovementEntity : ITenantOwned
{
    private InventoryStockMovementEntity() { }
    internal InventoryStockMovementEntity(TenantId tenantId, Guid id, Guid companyId, Guid? branchId, Guid warehouseId, string warehouseCode, string warehouseName, Guid productId, string sku, string productName, Guid uomId, string uomCode, InventoryMovementDirection direction, decimal quantity, decimal unitCost, string currencyCode, string? trackingIdentity, InventoryMovementSourceType sourceType, Guid sourceDocumentId, Guid sourceLineId, Guid? correctionOfMovementId, DateOnly effectiveDate, Guid actorId, string correlationId, DateTimeOffset postedAt)
    { Id = id; TenantId = tenantId; CompanyId = companyId; BranchId = branchId; WarehouseId = warehouseId; WarehouseCode = warehouseCode; WarehouseName = warehouseName; ProductId = productId; ProductSku = sku; ProductName = productName; UnitOfMeasureId = uomId; UnitOfMeasureCode = uomCode; Direction = direction; Quantity = quantity; UnitCost = unitCost; CurrencyCode = currencyCode; TrackingIdentity = trackingIdentity; SourceType = sourceType; SourceDocumentId = sourceDocumentId; SourceLineId = sourceLineId; CorrectionOfMovementId = correctionOfMovementId; EffectiveDate = effectiveDate; ActorId = actorId; CorrelationId = correlationId; PostedAt = postedAt; }
    internal Guid Id { get; private set; }
    public TenantId TenantId { get; private set; }
    internal Guid CompanyId { get; private set; }
    internal Guid? BranchId { get; private set; }
    internal Guid WarehouseId { get; private set; }
    internal string WarehouseCode { get; private set; } = string.Empty;
    internal string WarehouseName { get; private set; } = string.Empty;
    internal Guid ProductId { get; private set; }
    internal string ProductSku { get; private set; } = string.Empty;
    internal string ProductName { get; private set; } = string.Empty;
    internal Guid UnitOfMeasureId { get; private set; }
    internal string UnitOfMeasureCode { get; private set; } = string.Empty;
    internal InventoryMovementDirection Direction { get; private set; }
    internal decimal Quantity { get; private set; }
    internal decimal UnitCost { get; private set; }
    internal string CurrencyCode { get; private set; } = string.Empty;
    internal string? TrackingIdentity { get; private set; }
    internal InventoryMovementSourceType SourceType { get; private set; }
    internal Guid SourceDocumentId { get; private set; }
    internal Guid SourceLineId { get; private set; }
    internal Guid? CorrectionOfMovementId { get; private set; }
    internal DateOnly EffectiveDate { get; private set; }
    internal Guid ActorId { get; private set; }
    internal string CorrelationId { get; private set; } = string.Empty;
    internal DateTimeOffset PostedAt { get; private set; }
    internal byte[] Version { get; private set; } = [];
}

internal sealed class InventoryReservationEntity : ITenantOwned
{
    private InventoryReservationEntity() { }
    internal InventoryReservationEntity(TenantId tenantId, Guid id, Guid companyId, Guid? branchId, Guid warehouseId, string warehouseCode, string warehouseName, Guid productId, string sku, string productName, Guid uomId, string uomCode, string? trackingIdentity, string sourceType, string sourceReference, decimal requested, decimal reserved, decimal unallocated, Guid actorId, DateTimeOffset at)
    { Id = id; TenantId = tenantId; CompanyId = companyId; BranchId = branchId; WarehouseId = warehouseId; WarehouseCode = warehouseCode; WarehouseName = warehouseName; ProductId = productId; ProductSku = sku; ProductName = productName; UnitOfMeasureId = uomId; UnitOfMeasureCode = uomCode; TrackingIdentity = trackingIdentity; SourceType = sourceType; SourceReference = sourceReference; RequestedQuantity = requested; ReservedQuantity = reserved; UnallocatedQuantity = unallocated; Status = InventoryReservationStatus.Active; ActorId = actorId; CreatedAt = at; UpdatedAt = at; TouchVersion(); }
    internal Guid Id { get; private set; }
    public TenantId TenantId { get; private set; }
    internal Guid CompanyId { get; private set; }
    internal Guid? BranchId { get; private set; }
    internal Guid WarehouseId { get; private set; }
    internal string WarehouseCode { get; private set; } = string.Empty;
    internal string WarehouseName { get; private set; } = string.Empty;
    internal Guid ProductId { get; private set; }
    internal string ProductSku { get; private set; } = string.Empty;
    internal string ProductName { get; private set; } = string.Empty;
    internal Guid UnitOfMeasureId { get; private set; }
    internal string UnitOfMeasureCode { get; private set; } = string.Empty;
    internal string? TrackingIdentity { get; private set; }
    internal string SourceType { get; private set; } = string.Empty;
    internal string SourceReference { get; private set; } = string.Empty;
    internal decimal RequestedQuantity { get; private set; }
    internal decimal ReservedQuantity { get; private set; }
    internal decimal UnallocatedQuantity { get; private set; }
    internal InventoryReservationStatus Status { get; private set; }
    internal Guid ActorId { get; private set; }
    internal DateTimeOffset CreatedAt { get; private set; }
    internal DateTimeOffset UpdatedAt { get; private set; }
    internal byte[] Version { get; private set; } = [];
    internal void Reduce(decimal quantity, DateTimeOffset at) { ReservedQuantity -= quantity; UnallocatedQuantity += quantity; UpdatedAt = at; }
    internal void Release(DateTimeOffset at) { UnallocatedQuantity += ReservedQuantity; ReservedQuantity = 0; Status = InventoryReservationStatus.Released; UpdatedAt = at; }
    internal void TouchVersion() => Version = Guid.NewGuid().ToByteArray();
}

internal sealed class InventoryReservationHistoryEntity : ITenantOwned
{
    private InventoryReservationHistoryEntity() { }
    internal InventoryReservationHistoryEntity(TenantId tenantId, Guid id, Guid reservationId, InventoryReservationAction action, decimal quantity, decimal reservedAfter, decimal unallocatedAfter, Guid actorId, string? reason, string correlationId, DateTimeOffset occurredAt)
    { Id = id; TenantId = tenantId; ReservationId = reservationId; Action = action; Quantity = quantity; ReservedQuantityAfter = reservedAfter; UnallocatedQuantityAfter = unallocatedAfter; ActorId = actorId; Reason = reason; CorrelationId = correlationId; OccurredAt = occurredAt; }
    internal Guid Id { get; private set; }
    public TenantId TenantId { get; private set; }
    internal Guid ReservationId { get; private set; }
    internal InventoryReservationAction Action { get; private set; }
    internal decimal Quantity { get; private set; }
    internal decimal ReservedQuantityAfter { get; private set; }
    internal decimal UnallocatedQuantityAfter { get; private set; }
    internal Guid ActorId { get; private set; }
    internal string? Reason { get; private set; }
    internal string CorrelationId { get; private set; } = string.Empty;
    internal DateTimeOffset OccurredAt { get; private set; }
    internal byte[] Version { get; private set; } = [];
}

internal sealed class InventoryAuditEntity : ITenantOwned
{
    private InventoryAuditEntity() { }
    internal InventoryAuditEntity(TenantId tenantId, Guid id, string resourceType, Guid resourceId, string operationId, Guid actorId, Guid sessionId, string authorizationPath, string decision, string? reason, string correlationId, string? idempotencyKey, string? requestFingerprint, string? beforeSummary, string? afterSummary, DateTimeOffset occurredAt)
    { Id = id; TenantId = tenantId; ResourceType = resourceType; ResourceId = resourceId; OperationId = operationId; ActorId = actorId; SessionId = sessionId; AuthorizationPath = authorizationPath; Decision = decision; Reason = reason; CorrelationId = correlationId; IdempotencyKey = idempotencyKey; RequestFingerprint = requestFingerprint; BeforeSummary = beforeSummary; AfterSummary = afterSummary; OccurredAt = occurredAt; }
    internal Guid Id { get; private set; }
    public TenantId TenantId { get; private set; }
    internal string ResourceType { get; private set; } = string.Empty;
    internal Guid ResourceId { get; private set; }
    internal string OperationId { get; private set; } = string.Empty;
    internal Guid ActorId { get; private set; }
    internal Guid SessionId { get; private set; }
    internal string AuthorizationPath { get; private set; } = string.Empty;
    internal string Decision { get; private set; } = string.Empty;
    internal string? Reason { get; private set; }
    internal string CorrelationId { get; private set; } = string.Empty;
    internal string? IdempotencyKey { get; private set; }
    internal string? RequestFingerprint { get; private set; }
    internal string? BeforeSummary { get; private set; }
    internal string? AfterSummary { get; private set; }
    internal DateTimeOffset OccurredAt { get; private set; }
    internal byte[] Version { get; private set; } = [];
}

internal sealed class InventoryIdempotencyEntity : ITenantOwned
{
    private InventoryIdempotencyEntity() { }
    internal InventoryIdempotencyEntity(TenantId tenantId, Guid id, Guid actorId, string operationId, string key, string fingerprint, string resourceType, Guid resourceId, string snapshotJson, DateTimeOffset at)
    { Id = id; TenantId = tenantId; ActorId = actorId; OperationId = operationId; Key = key; Fingerprint = fingerprint; ResourceType = resourceType; ResourceId = resourceId; SnapshotJson = snapshotJson; CreatedAt = at; }
    internal Guid Id { get; private set; }
    public TenantId TenantId { get; private set; }
    internal Guid ActorId { get; private set; }
    internal string OperationId { get; private set; } = string.Empty;
    internal string Key { get; private set; } = string.Empty;
    internal string Fingerprint { get; private set; } = string.Empty;
    internal string ResourceType { get; private set; } = string.Empty;
    internal Guid ResourceId { get; private set; }
    internal string SnapshotJson { get; private set; } = string.Empty;
    internal DateTimeOffset CreatedAt { get; private set; }
    internal byte[] Version { get; private set; } = [];
}

internal sealed class InventoryConcurrencyAnchorEntity : ITenantOwned
{
    private InventoryConcurrencyAnchorEntity() { }
    internal InventoryConcurrencyAnchorEntity(TenantId tenantId, Guid id, Guid companyId, Guid? branchId, Guid warehouseId, Guid productId, Guid unitOfMeasureId, string trackingKey)
    { Id = id; TenantId = tenantId; CompanyId = companyId; BranchId = branchId; WarehouseId = warehouseId; ProductId = productId; UnitOfMeasureId = unitOfMeasureId; TrackingKey = trackingKey; }
    internal Guid Id { get; private set; }
    public TenantId TenantId { get; private set; }
    internal Guid CompanyId { get; private set; }
    internal Guid? BranchId { get; private set; }
    internal Guid WarehouseId { get; private set; }
    internal Guid ProductId { get; private set; }
    internal Guid UnitOfMeasureId { get; private set; }
    internal string TrackingKey { get; private set; } = string.Empty;
    internal byte[] Version { get; private set; } = [];
    internal void TouchVersion() => Version = Guid.NewGuid().ToByteArray();
}

#pragma warning restore CS1591
