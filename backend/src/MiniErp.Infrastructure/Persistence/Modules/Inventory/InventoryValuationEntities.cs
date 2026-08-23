#pragma warning disable CS1591

using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.Contracts.Modules.Inventory;

namespace MiniErp.Infrastructure.Persistence.Modules.Inventory;

internal sealed class InventoryValuationPolicyEntity : ITenantOwned
{
    private InventoryValuationPolicyEntity() { }
    internal InventoryValuationPolicyEntity(TenantId tenantId, Guid id, InventoryValuationPolicyRequest request, Guid actorId, DateTimeOffset at)
    {
        Id = id; TenantId = tenantId; CompanyId = request.CompanyId; FunctionalCurrencyId = request.FunctionalCurrencyId; FunctionalCurrencyCode = request.FunctionalCurrencyCode.Trim().ToUpperInvariant(); ScopeMode = request.ScopeMode; EffectiveFrom = request.EffectiveFrom; EffectiveTo = request.EffectiveTo; VersionNumber = 1; UnitCostScale = request.UnitCostScale; AmountScale = request.AmountScale; RoundingMode = request.RoundingMode; GoodsReceiptCostBasis = request.GoodsReceiptCostBasis; PositiveAdjustmentCostBasis = request.PositiveAdjustmentCostBasis; SupplierReturnCostBasis = request.SupplierReturnCostBasis; IsActive = true; ActorId = actorId; CreatedAt = at; UpdatedAt = at; TouchVersion();
    }
    internal Guid Id { get; private set; }
    public TenantId TenantId { get; private set; }
    internal Guid CompanyId { get; private set; }
    internal Guid FunctionalCurrencyId { get; private set; }
    internal string FunctionalCurrencyCode { get; private set; } = string.Empty;
    internal InventoryValuationScopeMode ScopeMode { get; private set; }
    internal DateOnly EffectiveFrom { get; private set; }
    internal DateOnly? EffectiveTo { get; private set; }
    internal int VersionNumber { get; private set; }
    internal int UnitCostScale { get; private set; }
    internal int AmountScale { get; private set; }
    internal InventoryValuationRoundingMode RoundingMode { get; private set; }
    internal string GoodsReceiptCostBasis { get; private set; } = string.Empty;
    internal string PositiveAdjustmentCostBasis { get; private set; } = string.Empty;
    internal string SupplierReturnCostBasis { get; private set; } = string.Empty;
    internal bool IsActive { get; private set; }
    internal Guid ActorId { get; private set; }
    internal DateTimeOffset CreatedAt { get; private set; }
    internal DateTimeOffset UpdatedAt { get; private set; }
    internal byte[] Version { get; private set; } = [];
    internal void TouchVersion() => Version = Guid.NewGuid().ToByteArray();
}

internal sealed class InventoryValuationScopeAnchorEntity : ITenantOwned
{
    private InventoryValuationScopeAnchorEntity() { }
    internal InventoryValuationScopeAnchorEntity(TenantId tenantId, Guid companyId, Guid? branchId, Guid warehouseId, Guid productId, Guid unitOfMeasureId, string? trackingIdentity, Guid policyId)
    {
        Id = Guid.NewGuid(); TenantId = tenantId; CompanyId = companyId; BranchId = branchId; WarehouseId = warehouseId; ProductId = productId; UnitOfMeasureId = unitOfMeasureId; TrackingIdentity = trackingIdentity ?? string.Empty; PolicyId = policyId; TouchVersion();
    }
    internal Guid Id { get; private set; }
    public TenantId TenantId { get; private set; }
    internal Guid CompanyId { get; private set; }
    internal Guid? BranchId { get; private set; }
    internal Guid WarehouseId { get; private set; }
    internal Guid ProductId { get; private set; }
    internal Guid UnitOfMeasureId { get; private set; }
    internal string TrackingIdentity { get; private set; } = string.Empty;
    internal Guid PolicyId { get; private set; }
    internal byte[] Version { get; private set; } = [];
    internal void TouchVersion() => Version = Guid.NewGuid().ToByteArray();
}

internal sealed class InventoryValuationStateEntity : ITenantOwned
{
    private InventoryValuationStateEntity() { }
    internal InventoryValuationStateEntity(TenantId tenantId, Guid companyId, Guid? branchId, Guid warehouseId, Guid productId, Guid unitOfMeasureId, string? trackingIdentity, InventoryValuationPolicyEntity policy, DateTimeOffset at)
    {
        Id = Guid.NewGuid(); TenantId = tenantId; CompanyId = companyId; BranchId = branchId; WarehouseId = warehouseId; ProductId = productId; UnitOfMeasureId = unitOfMeasureId; TrackingIdentity = trackingIdentity ?? string.Empty; PolicyId = policy.Id; PolicyVersionNumber = policy.VersionNumber; FunctionalCurrencyCode = policy.FunctionalCurrencyCode; Quantity = 0m; Value = 0m; AverageUnitCost = 0m; LastAppliedLedgerSequence = 0; UpdatedAt = at; TouchVersion();
    }
    internal Guid Id { get; private set; }
    public TenantId TenantId { get; private set; }
    internal Guid CompanyId { get; private set; }
    internal Guid? BranchId { get; private set; }
    internal Guid WarehouseId { get; private set; }
    internal Guid ProductId { get; private set; }
    internal Guid UnitOfMeasureId { get; private set; }
    internal string TrackingIdentity { get; private set; } = string.Empty;
    internal Guid PolicyId { get; private set; }
    internal int PolicyVersionNumber { get; private set; }
    internal string FunctionalCurrencyCode { get; private set; } = string.Empty;
    internal decimal Quantity { get; private set; }
    internal decimal Value { get; private set; }
    internal decimal AverageUnitCost { get; private set; }
    internal long LastAppliedLedgerSequence { get; private set; }
    internal DateTimeOffset UpdatedAt { get; private set; }
    internal byte[] Version { get; private set; } = [];
    internal void Apply(decimal quantity, decimal value, decimal average, long sequence, DateTimeOffset at)
    {
        Quantity = quantity; Value = value; AverageUnitCost = average; LastAppliedLedgerSequence = sequence; UpdatedAt = at; TouchVersion();
    }
    internal void TouchVersion() => Version = Guid.NewGuid().ToByteArray();
}

internal sealed class InventoryMovementValuationEventEntity : ITenantOwned
{
    private InventoryMovementValuationEventEntity() { }
    internal InventoryMovementValuationEventEntity(TenantId tenantId, Guid id, Guid movementId, long ledgerSequence, InventoryValuationEventStatus status, string statusCode, InventoryStockMovementEntity movement, InventoryValuationPolicyEntity policy, decimal priorQuantity, decimal priorValue, decimal newQuantity, decimal newValue, decimal? movementValue, decimal? baseUnitCost, Guid? exchangeRateId, Guid? exchangeRateVersionId, int? exchangeRateVersionNumber, decimal? exchangeRate, int? exchangeRateScale, string? exchangeRateProvenance, Guid? correctionOfValuationEventId, Guid? sourceRevisionId, bool isBackdated, string? pendingReason, Guid actorId, string correlationId, DateTimeOffset at, decimal? transactionUnitCostOverride = null, string? transactionCurrencyCodeOverride = null)
    {
        Id = id; TenantId = tenantId; MovementId = movementId; SourceType = movement.SourceType; SourceDocumentId = movement.SourceDocumentId; SourceLineId = movement.SourceLineId; CorrectionOfMovementId = movement.CorrectionOfMovementId; GoodsReceiptId = movement.GoodsReceiptId; GoodsReceiptLineId = movement.GoodsReceiptLineId; SupplierReturnId = movement.SupplierReturnId; SupplierReturnLineId = movement.SupplierReturnLineId; PurchaseOrderId = movement.PurchaseOrderId; PurchaseOrderLineId = movement.PurchaseOrderLineId; TransferId = movement.TransferId; TransferLineId = movement.TransferLineId; SourceReference = movement.SourceReference; LedgerSequence = ledgerSequence; Status = status; StatusCode = statusCode; CompanyId = movement.CompanyId; BranchId = movement.BranchId; WarehouseId = movement.WarehouseId; ProductId = movement.ProductId; UnitOfMeasureId = movement.UnitOfMeasureId; TrackingIdentity = movement.TrackingIdentity ?? string.Empty; PolicyId = policy.Id; PolicyVersionNumber = policy.VersionNumber; FunctionalCurrencyCode = policy.FunctionalCurrencyCode; Quantity = movement.Quantity; Direction = movement.Direction; TransactionUnitCost = transactionUnitCostOverride ?? movement.UnitCost; TransactionCurrencyCode = transactionCurrencyCodeOverride ?? movement.CurrencyCode; ExchangeRateId = exchangeRateId; ExchangeRateVersionId = exchangeRateVersionId; ExchangeRateVersionNumber = exchangeRateVersionNumber; ExchangeRate = exchangeRate; ExchangeRateScale = exchangeRateScale; ExchangeRateProvenance = exchangeRateProvenance; EffectiveOn = movement.EffectiveDate; BaseUnitCost = baseUnitCost; PriorQuantity = priorQuantity; PriorValue = priorValue; NewQuantity = newQuantity; NewValue = newValue; MovementValue = movementValue; UnitCostScale = policy.UnitCostScale; AmountScale = policy.AmountScale; RoundingMode = policy.RoundingMode; CorrectionOfValuationEventId = correctionOfValuationEventId; SourceRevisionId = sourceRevisionId; IsBackdated = isBackdated; PendingReason = pendingReason; CorrelationId = correlationId; ActorId = actorId; OccurredAt = at; TouchVersion();
    }
    internal Guid Id { get; private set; }
    public TenantId TenantId { get; private set; }
    internal Guid MovementId { get; private set; }
    internal InventoryMovementSourceType SourceType { get; private set; }
    internal Guid SourceDocumentId { get; private set; }
    internal Guid SourceLineId { get; private set; }
    internal Guid? CorrectionOfMovementId { get; private set; }
    internal Guid? GoodsReceiptId { get; private set; }
    internal Guid? GoodsReceiptLineId { get; private set; }
    internal Guid? SupplierReturnId { get; private set; }
    internal Guid? SupplierReturnLineId { get; private set; }
    internal Guid? PurchaseOrderId { get; private set; }
    internal Guid? PurchaseOrderLineId { get; private set; }
    internal Guid? TransferId { get; private set; }
    internal Guid? TransferLineId { get; private set; }
    internal string? SourceReference { get; private set; }
    internal long LedgerSequence { get; private set; }
    internal InventoryValuationEventStatus Status { get; private set; }
    internal string StatusCode { get; private set; } = string.Empty;
    internal Guid CompanyId { get; private set; }
    internal Guid? BranchId { get; private set; }
    internal Guid WarehouseId { get; private set; }
    internal Guid ProductId { get; private set; }
    internal Guid UnitOfMeasureId { get; private set; }
    internal string TrackingIdentity { get; private set; } = string.Empty;
    internal Guid PolicyId { get; private set; }
    internal int PolicyVersionNumber { get; private set; }
    internal string FunctionalCurrencyCode { get; private set; } = string.Empty;
    internal decimal Quantity { get; private set; }
    internal InventoryMovementDirection Direction { get; private set; }
    internal decimal? TransactionUnitCost { get; private set; }
    internal string? TransactionCurrencyCode { get; private set; }
    internal Guid? ExchangeRateId { get; private set; }
    internal Guid? ExchangeRateVersionId { get; private set; }
    internal int? ExchangeRateVersionNumber { get; private set; }
    internal decimal? ExchangeRate { get; private set; }
    internal int? ExchangeRateScale { get; private set; }
    internal string? ExchangeRateProvenance { get; private set; }
    internal DateOnly EffectiveOn { get; private set; }
    internal decimal? BaseUnitCost { get; private set; }
    internal decimal PriorQuantity { get; private set; }
    internal decimal PriorValue { get; private set; }
    internal decimal NewQuantity { get; private set; }
    internal decimal NewValue { get; private set; }
    internal decimal? MovementValue { get; private set; }
    internal int UnitCostScale { get; private set; }
    internal int AmountScale { get; private set; }
    internal InventoryValuationRoundingMode RoundingMode { get; private set; }
    internal Guid? CorrectionOfValuationEventId { get; private set; }
    internal Guid? SourceRevisionId { get; private set; }
    internal bool IsBackdated { get; private set; }
    internal string? PendingReason { get; private set; }
    internal string CorrelationId { get; private set; } = string.Empty;
    internal Guid ActorId { get; private set; }
    internal DateTimeOffset OccurredAt { get; private set; }
    internal byte[] Version { get; private set; } = [];
    internal void TouchVersion() => Version = Guid.NewGuid().ToByteArray();
}

internal sealed class InventoryValuationRunEntity : ITenantOwned
{
    private InventoryValuationRunEntity() { }
    internal InventoryValuationRunEntity(TenantId tenantId, Guid actorId, string key, string fingerprint, string resultJson, DateTimeOffset at)
    { Id = Guid.NewGuid(); TenantId = tenantId; ActorId = actorId; IdempotencyKey = key; RequestFingerprint = fingerprint; ResultJson = resultJson; CreatedAt = at; TouchVersion(); }
    internal Guid Id { get; private set; }
    public TenantId TenantId { get; private set; }
    internal Guid ActorId { get; private set; }
    internal string IdempotencyKey { get; private set; } = string.Empty;
    internal string RequestFingerprint { get; private set; } = string.Empty;
    internal string ResultJson { get; private set; } = string.Empty;
    internal DateTimeOffset CreatedAt { get; private set; }
    internal byte[] Version { get; private set; } = [];
    internal void TouchVersion() => Version = Guid.NewGuid().ToByteArray();
}

internal sealed class InventoryFinanceValuationHandoffEntity : ITenantOwned
{
    private InventoryFinanceValuationHandoffEntity() { }
    internal InventoryFinanceValuationHandoffEntity(TenantId tenantId, InventoryStockMovementEntity movement, InventoryMovementValuationEventEntity evidence, InventoryValuationPolicyEntity policy, decimal baseUnitCost, decimal baseAmount, decimal? transactionUnitCost, string? transactionCurrencyCode, Guid? exchangeRateId, Guid? exchangeRateVersionId, int? exchangeRateVersionNumber, decimal? exchangeRate, int? exchangeRateScale, string? exchangeRateProvenance, InventoryFinanceValuationHandoffStatus status, string correlationId, DateTimeOffset asOf)
    { Id = Guid.NewGuid(); TenantId = tenantId; CompanyId = movement.CompanyId; BranchId = movement.BranchId; WarehouseId = movement.WarehouseId; MovementId = movement.Id; LedgerSequence = movement.LedgerSequence; SourceType = movement.SourceType; SourceDocumentId = movement.SourceDocumentId; SourceLineId = movement.SourceLineId; ValuationEvidenceId = evidence.Id; ValuationEvidenceVersion = 1; Quantity = movement.Quantity; BaseUnitCost = baseUnitCost; BaseAmount = baseAmount; PolicyId = policy.Id; PolicyVersionNumber = policy.VersionNumber; FunctionalCurrencyCode = policy.FunctionalCurrencyCode; TransactionUnitCost = transactionUnitCost; TransactionCurrencyCode = transactionCurrencyCode; ExchangeRateId = exchangeRateId; ExchangeRateVersionId = exchangeRateVersionId; ExchangeRateVersionNumber = exchangeRateVersionNumber; ExchangeRate = exchangeRate; ExchangeRateScale = exchangeRateScale; ExchangeRateProvenance = exchangeRateProvenance; ProductId = movement.ProductId; UnitOfMeasureId = movement.UnitOfMeasureId; TrackingIdentity = movement.TrackingIdentity; CorrectionOfMovementId = movement.CorrectionOfMovementId; Status = status; ContractVersion = "inventory-valuation-finance.v1"; CorrelationId = correlationId; AsOf = asOf; CreatedAt = asOf; TouchVersion(); }
    internal Guid Id { get; private set; }
    public TenantId TenantId { get; private set; }
    internal Guid CompanyId { get; private set; }
    internal Guid? BranchId { get; private set; }
    internal Guid WarehouseId { get; private set; }
    internal Guid MovementId { get; private set; }
    internal long LedgerSequence { get; private set; }
    internal InventoryMovementSourceType SourceType { get; private set; }
    internal Guid SourceDocumentId { get; private set; }
    internal Guid SourceLineId { get; private set; }
    internal Guid ValuationEvidenceId { get; private set; }
    internal int ValuationEvidenceVersion { get; private set; }
    internal decimal Quantity { get; private set; }
    internal decimal BaseUnitCost { get; private set; }
    internal decimal BaseAmount { get; private set; }
    internal Guid PolicyId { get; private set; }
    internal int PolicyVersionNumber { get; private set; }
    internal string FunctionalCurrencyCode { get; private set; } = string.Empty;
    internal decimal? TransactionUnitCost { get; private set; }
    internal string? TransactionCurrencyCode { get; private set; }
    internal Guid? ExchangeRateId { get; private set; }
    internal Guid? ExchangeRateVersionId { get; private set; }
    internal int? ExchangeRateVersionNumber { get; private set; }
    internal decimal? ExchangeRate { get; private set; }
    internal int? ExchangeRateScale { get; private set; }
    internal string? ExchangeRateProvenance { get; private set; }
    internal Guid ProductId { get; private set; }
    internal Guid UnitOfMeasureId { get; private set; }
    internal string? TrackingIdentity { get; private set; }
    internal Guid? CorrectionOfMovementId { get; private set; }
    internal InventoryFinanceValuationHandoffStatus Status { get; private set; }
    internal string ContractVersion { get; private set; } = string.Empty;
    internal string CorrelationId { get; private set; } = string.Empty;
    internal DateTimeOffset AsOf { get; private set; }
    internal DateTimeOffset CreatedAt { get; private set; }
    internal byte[] Version { get; private set; } = [];
    internal void TouchVersion() => Version = Guid.NewGuid().ToByteArray();
}

#pragma warning restore CS1591
