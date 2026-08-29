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
    StockIssue = 12,
    SalesDelivery = 13
}

public enum InventoryValuationStatus
{
    Known = 1,
    Pending = 2
}

public enum InventoryValuationEventStatus
{
    Pending = 1,
    Applied = 2,
    Blocked = 3
}

public enum InventoryValuationScopeMode
{
    WarehouseProductUom = 1,
    WarehouseProductUomTracking = 2
}

public enum InventoryValuationRoundingMode
{
    ToEven = 1,
    AwayFromZero = 2
}

public enum InventoryValuationReconciliationStatus
{
    Reconciled = 1,
    PendingValuation = 2,
    Blocked = 3,
    QuantityMismatch = 4,
    ValuationMismatch = 5,
    FinanceHandoffPending = 6
}

public enum InventoryFinanceValuationHandoffStatus
{
    NotConfigured = 1,
    Pending = 2,
    ReadyForFinance = 3
}

public enum InventoryInTransitValuationStatus
{
    Ready = 1,
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
    Released = 2,
    Fulfilled = 3
}

public enum InventoryReservationAction
{
    Created = 1,
    Reduced = 2,
    Released = 3,
    Consumed = 4,
    Allocated = 5
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
    string? TrackingIdentity = null,
    Guid? SourceDocumentId = null,
    Guid? SourceLineId = null,
    int? SourceRevision = null,
    decimal? SourceQuantityLimit = null);

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
    string? SourceReference = null,
    long LedgerSequence = 0);

public sealed record InventoryValuationPolicyRequest(
    Guid CompanyId,
    Guid FunctionalCurrencyId,
    string FunctionalCurrencyCode,
    InventoryValuationScopeMode ScopeMode,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    int UnitCostScale,
    int AmountScale,
    InventoryValuationRoundingMode RoundingMode,
    string GoodsReceiptCostBasis,
    string PositiveAdjustmentCostBasis,
    string SupplierReturnCostBasis);

public sealed record InventoryValuationProcessRequest(
    Guid CompanyId,
    Guid? BranchId = null,
    Guid? WarehouseId = null,
    Guid? ProductId = null,
    Guid? UnitOfMeasureId = null);

public sealed record InventoryValuationCorrectionRequest(
    Guid AuthoritativeSourceRevisionId,
    string Reason);

public sealed record InventoryValuationPolicyRecord(
    Guid Id,
    Guid TenantId,
    Guid CompanyId,
    Guid FunctionalCurrencyId,
    string FunctionalCurrencyCode,
    InventoryValuationScopeMode ScopeMode,
    DateOnly EffectiveFrom,
    DateOnly? EffectiveTo,
    int VersionNumber,
    int UnitCostScale,
    int AmountScale,
    InventoryValuationRoundingMode RoundingMode,
    string GoodsReceiptCostBasis,
    string PositiveAdjustmentCostBasis,
    string SupplierReturnCostBasis,
    bool IsActive,
    byte[] Version,
    Guid? SupersedesPolicyId = null);

public sealed record InventoryValuationStateRecord(
    Guid TenantId,
    Guid CompanyId,
    Guid? BranchId,
    Guid WarehouseId,
    Guid ProductId,
    Guid UnitOfMeasureId,
    string? TrackingIdentity,
    Guid? CurrentPolicyId,
    int? CurrentPolicyVersionNumber,
    string FunctionalCurrencyCode,
    decimal Quantity,
    decimal Value,
    decimal AverageUnitCost,
    long LastAppliedLedgerSequence,
    DateTimeOffset UpdatedAt,
    byte[] Version);

public sealed record InventoryMovementValuationEventRecord(
    Guid Id,
    Guid TenantId,
    Guid MovementId,
    InventoryMovementSourceType SourceType,
    Guid SourceDocumentId,
    Guid SourceLineId,
    Guid? CorrectionOfMovementId,
    Guid? GoodsReceiptId,
    Guid? GoodsReceiptLineId,
    Guid? SupplierReturnId,
    Guid? SupplierReturnLineId,
    Guid? PurchaseOrderId,
    Guid? PurchaseOrderLineId,
    Guid? TransferId,
    Guid? TransferLineId,
    string? SourceReference,
    long LedgerSequence,
    InventoryValuationEventStatus Status,
    string StatusCode,
    Guid CompanyId,
    Guid? BranchId,
    Guid WarehouseId,
    Guid ProductId,
    Guid UnitOfMeasureId,
    string? TrackingIdentity,
    Guid? PolicyId,
    int? PolicyVersionNumber,
    string? FunctionalCurrencyCode,
    decimal Quantity,
    InventoryMovementDirection Direction,
    decimal? TransactionUnitCost,
    string? TransactionCurrencyCode,
    Guid? ExchangeRateId,
    Guid? ExchangeRateVersionId,
    int? ExchangeRateVersionNumber,
    decimal? ExchangeRate,
    int? ExchangeRateScale,
    string? ExchangeRateProvenance,
    DateOnly EffectiveOn,
    decimal? BaseUnitCost,
    decimal PriorQuantity,
    decimal PriorValue,
    decimal NewQuantity,
    decimal NewValue,
    decimal? MovementValue,
    decimal? FormulaMovementValue,
    decimal? RoundingAdjustmentAmount,
    int? UnitCostScale,
    int? AmountScale,
    InventoryValuationRoundingMode? RoundingMode,
    Guid? CorrectionOfValuationEventId,
    Guid? SourceRevisionId,
    bool IsBackdated,
    string? PendingReason,
    string CorrelationId,
    Guid ActorId,
    DateTimeOffset OccurredAt,
    byte[] Version);

public sealed record InventoryValuationProcessResult(
    Guid CompanyId,
    Guid? BranchId,
    Guid? WarehouseId,
    Guid? ProductId,
    int AppliedCount,
    int PendingCount,
    int BlockedCount,
    long? LatestLedgerSequence,
    DateTimeOffset AsOf,
    string FunctionalCurrencyCode,
    string? PolicyId,
    string? Message);

public sealed record InventoryValuationReconciliationRecord(
    Guid TenantId,
    Guid CompanyId,
    Guid? BranchId,
    Guid WarehouseId,
    Guid ProductId,
    Guid UnitOfMeasureId,
    string? TrackingIdentity,
    string FunctionalCurrencyCode,
    Guid? PolicyId,
    InventoryValuationReconciliationStatus Status,
    decimal PhysicalOnHandQuantity,
    decimal ValuedQuantity,
    decimal QuantityDifference,
    decimal ValuedAmount,
    decimal AverageUnitCost,
    long? LatestLedgerSequence,
    long LastAppliedLedgerSequence,
    int EligibleMovementCount,
    int AppliedMovementCount,
    int PendingMovementCount,
    int BlockedMovementCount,
    long? OldestPendingLedgerSequence,
    decimal InTransitQuantity,
    decimal InTransitValue,
    InventoryInTransitValuationStatus InTransitValueStatus,
    InventoryFinanceValuationHandoffStatus FinanceHandoffStatus,
    DateTimeOffset AsOf,
    DateTimeOffset FreshAsOf,
    string? DifferenceReason);

public sealed record InventoryValuationSummaryRecord(
    Guid TenantId,
    Guid CompanyId,
    Guid? BranchId,
    Guid? WarehouseId,
    string FunctionalCurrencyCode,
    decimal PhysicalOnHandQuantity,
    decimal ValuedQuantity,
    decimal ValuedAmount,
    int PendingMovementCount,
    int BlockedMovementCount,
    decimal InTransitQuantity,
    decimal InTransitValue,
    InventoryInTransitValuationStatus InTransitValueStatus,
    InventoryValuationReconciliationStatus ReconciliationStatus,
    long? LatestLedgerSequence,
    long? LatestValuedLedgerSequence,
    bool IsComplete,
    bool IsPartial,
    DateTimeOffset AsOf,
    DateTimeOffset FreshAsOf);

public sealed record InventoryValuationExportRecord(
    string FileName,
    string ContentType,
    string Content,
    Guid TenantId,
    Guid CompanyId,
    string FunctionalCurrencyCode,
    Guid? PolicyId,
    int? PolicyVersionNumber,
    DateTimeOffset AsOf,
    DateTimeOffset FreshAsOf,
    Guid ActorId,
    string CorrelationId);

public sealed record InventoryFinanceValuationHandoffRecord(
    Guid Id,
    Guid TenantId,
    Guid CompanyId,
    Guid? BranchId,
    Guid WarehouseId,
    Guid MovementId,
    long LedgerSequence,
    InventoryMovementSourceType SourceType,
    Guid SourceDocumentId,
    Guid SourceLineId,
    Guid ValuationEvidenceId,
    int ValuationEvidenceVersion,
    decimal Quantity,
    InventoryMovementDirection Direction,
    decimal BaseUnitCost,
    decimal BaseAmount,
    decimal SignedBaseAmount,
    decimal RoundingAdjustmentAmount,
    Guid PolicyId,
    int PolicyVersionNumber,
    string FunctionalCurrencyCode,
    decimal? TransactionUnitCost,
    string? TransactionCurrencyCode,
    Guid? ExchangeRateId,
    Guid? ExchangeRateVersionId,
    int? ExchangeRateVersionNumber,
    decimal? ExchangeRate,
    int? ExchangeRateScale,
    string? ExchangeRateProvenance,
    Guid ProductId,
    Guid UnitOfMeasureId,
    string? TrackingIdentity,
    Guid? CorrectionOfMovementId,
    InventoryFinanceValuationHandoffStatus Status,
    string ContractVersion,
    string CorrelationId,
    DateTimeOffset AsOf,
    DateTimeOffset CreatedAt,
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
    byte[] Version,
    decimal FulfilledQuantity = 0m,
    Guid? SourceDocumentId = null,
    Guid? SourceLineId = null,
    int? SourceRevision = null);

public sealed record InventoryReservationHistoryRecord(
    Guid Id,
    Guid ReservationId,
    InventoryReservationAction Action,
    decimal Quantity,
    decimal ReservedQuantityAfter,
    decimal UnallocatedQuantityAfter,
    decimal FulfilledQuantityAfter,
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
