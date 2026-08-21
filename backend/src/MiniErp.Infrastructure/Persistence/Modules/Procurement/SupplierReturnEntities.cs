#pragma warning disable CS1591

using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.Modules.Procurement;
using MiniErp.Contracts.Modules.Procurement;

namespace MiniErp.Infrastructure.Persistence.Modules.Procurement;

internal sealed class SupplierReturnEntity : ITenantOwned
{
    private SupplierReturnEntity()
    {
        SupplierCode = string.Empty;
        SupplierName = string.Empty;
        CurrencyCode = string.Empty;
    }

    internal SupplierReturnEntity(
        SupplierReturnCreateCommand command,
        TenantId tenantId,
        Guid purchaseOrderId,
        Guid? supplierConfirmationId,
        Guid warehouseId,
        Guid supplierId,
        string supplierCode,
        string supplierName,
        string currencyCode)
    {
        Id = command.Id;
        TenantId = tenantId;
        GoodsReceiptId = command.GoodsReceiptId;
        PurchaseOrderId = purchaseOrderId;
        SupplierConfirmationId = supplierConfirmationId;
        CompanyId = command.Scope.CompanyId;
        BranchId = command.Scope.BranchId;
        WarehouseId = warehouseId;
        SupplierId = supplierId;
        SupplierCode = supplierCode;
        SupplierName = supplierName;
        CurrencyCode = currencyCode;
        Status = SupplierReturnStatus.Draft;
        ReasonCode = command.ReasonCode;
        Condition = command.Condition;
        CommercialOutcome = command.CommercialOutcome;
        ReasonDetail = command.ReasonDetail;
        Notes = command.Notes;
        ReturnDate = command.ReturnDate;
        CorrectionOfId = command.CorrectionOfId;
        CreatedByActorId = command.ActorId;
        CreatedAt = command.OccurredAt;
        UpdatedAt = command.OccurredAt;
    }

    internal Guid Id { get; private set; }
    public TenantId TenantId { get; private set; }
    internal Guid GoodsReceiptId { get; private set; }
    internal Guid PurchaseOrderId { get; private set; }
    internal Guid? SupplierConfirmationId { get; private set; }
    internal Guid CompanyId { get; private set; }
    internal Guid? BranchId { get; private set; }
    internal Guid WarehouseId { get; private set; }
    internal Guid SupplierId { get; private set; }
    internal string SupplierCode { get; private set; } = string.Empty;
    internal string SupplierName { get; private set; } = string.Empty;
    internal string CurrencyCode { get; private set; } = string.Empty;
    internal SupplierReturnStatus Status { get; private set; }
    internal SupplierReturnReasonCode ReasonCode { get; private set; }
    internal SupplierReturnCondition Condition { get; private set; }
    internal SupplierReturnCommercialOutcome CommercialOutcome { get; private set; }
    internal string? ReasonDetail { get; private set; }
    internal string? Notes { get; private set; }
    internal DateOnly ReturnDate { get; private set; }
    internal Guid CreatedByActorId { get; private set; }
    internal DateTimeOffset CreatedAt { get; private set; }
    internal DateTimeOffset UpdatedAt { get; private set; }
    internal DateTimeOffset? CancelledAt { get; private set; }
    internal DateTimeOffset? ReversedAt { get; private set; }
    internal Guid? CorrectionOfId { get; private set; }
    internal Guid? InventoryHandoffEvidenceId { get; private set; }
    internal string? InventoryHandoffReference { get; private set; }
    internal DateTimeOffset? InventoryHandoffRecordedAt { get; private set; }
    internal string? FinanceReference { get; private set; }
    internal string? FinanceCurrencyCode { get; private set; }
    internal decimal? FinanceAmount { get; private set; }
    internal DateTimeOffset? FinanceReferenceRecordedAt { get; private set; }
    internal byte[] Version { get; private set; } = [];

    internal List<SupplierReturnLineEntity> Lines { get; } = [];
    internal List<SupplierReturnEvidenceEntity> Evidence { get; } = [];

    internal void SetStatus(SupplierReturnStatus status, DateTimeOffset occurredAt)
    {
        Status = status;
        UpdatedAt = occurredAt;
        if (status == SupplierReturnStatus.Cancelled)
        {
            CancelledAt = occurredAt;
        }

        if (status == SupplierReturnStatus.Reversed)
        {
            ReversedAt = occurredAt;
        }
    }

    internal void RecordInventoryHandoff(Guid evidenceId, string reference, DateTimeOffset occurredAt)
    {
        InventoryHandoffEvidenceId = evidenceId;
        InventoryHandoffReference = reference;
        InventoryHandoffRecordedAt = occurredAt;
        UpdatedAt = occurredAt;
    }

    internal void RecordFinanceReference(string reference, string? currencyCode, decimal? amount, DateTimeOffset occurredAt)
    {
        FinanceReference = reference;
        FinanceCurrencyCode = currencyCode;
        FinanceAmount = amount;
        FinanceReferenceRecordedAt = occurredAt;
        UpdatedAt = occurredAt;
    }

    internal void TouchVersion() => Version = Guid.NewGuid().ToByteArray();
}

internal sealed class SupplierReturnLineEntity : ITenantOwned
{
    private SupplierReturnLineEntity()
    {
        ProductSku = string.Empty;
        ProductName = string.Empty;
        UnitOfMeasureCode = string.Empty;
    }

    internal SupplierReturnLineEntity(
        TenantId tenantId,
        Guid supplierReturnId,
        SupplierReturnLineCreateCommand command,
        Guid purchaseOrderLineId,
        Guid productId,
        string productSku,
        string productName,
        string unitOfMeasureCode,
        decimal acceptedQuantityAtReturn,
        decimal? eligibleQuantityAfter)
    {
        Id = command.Id;
        TenantId = tenantId;
        SupplierReturnId = supplierReturnId;
        GoodsReceiptLineId = command.GoodsReceiptLineId;
        PurchaseOrderLineId = purchaseOrderLineId;
        ProductId = productId;
        ProductSku = productSku;
        ProductName = productName;
        UnitOfMeasureCode = unitOfMeasureCode;
        AcceptedQuantityAtReturn = acceptedQuantityAtReturn;
        ReturnQuantity = command.ReturnQuantity;
        EligibleQuantityAfter = eligibleQuantityAfter;
        Notes = command.Notes;
    }

    internal Guid Id { get; private set; }
    public TenantId TenantId { get; private set; }
    internal Guid SupplierReturnId { get; private set; }
    internal Guid GoodsReceiptLineId { get; private set; }
    internal Guid PurchaseOrderLineId { get; private set; }
    internal Guid ProductId { get; private set; }
    internal string ProductSku { get; private set; } = string.Empty;
    internal string ProductName { get; private set; } = string.Empty;
    internal string UnitOfMeasureCode { get; private set; } = string.Empty;
    internal decimal AcceptedQuantityAtReturn { get; private set; }
    internal decimal ReturnQuantity { get; private set; }
    internal decimal? EligibleQuantityAfter { get; private set; }
    internal string? Notes { get; private set; }
    internal byte[] Version { get; private set; } = [];
}

internal sealed class SupplierReturnEvidenceEntity : ITenantOwned
{
    private SupplierReturnEvidenceEntity() => ReferenceId = string.Empty;

    internal SupplierReturnEvidenceEntity(Guid id, TenantId tenantId, Guid supplierReturnId, SupplierReturnEvidenceCreateCommand command, DateTimeOffset recordedAt)
    {
        Id = id;
        TenantId = tenantId;
        SupplierReturnId = supplierReturnId;
        ReferenceId = command.ReferenceId;
        FileName = command.FileName;
        ContentType = command.ContentType;
        Description = command.Description;
        Source = command.Source;
        RecordedAt = recordedAt;
    }

    internal Guid Id { get; private set; }
    public TenantId TenantId { get; private set; }
    internal Guid SupplierReturnId { get; private set; }
    internal string ReferenceId { get; private set; }
    internal string? FileName { get; private set; }
    internal string? ContentType { get; private set; }
    internal string? Description { get; private set; }
    internal string Source { get; private set; } = string.Empty;
    internal DateTimeOffset RecordedAt { get; private set; }
    internal byte[] Version { get; private set; } = [];
}

internal sealed class SupplierReturnHistoryEntity : ITenantOwned
{
    private SupplierReturnHistoryEntity() => CorrelationId = string.Empty;

    internal SupplierReturnHistoryEntity(Guid id, TenantId tenantId, Guid supplierReturnId, SupplierReturnStatus fromStatus, SupplierReturnStatus toStatus, SupplierReturnHistoryAction action, Guid actorId, string? reason, string correlationId, DateTimeOffset occurredAt)
    {
        Id = id;
        TenantId = tenantId;
        SupplierReturnId = supplierReturnId;
        FromStatus = fromStatus;
        ToStatus = toStatus;
        Action = action;
        ActorId = actorId;
        Reason = reason;
        CorrelationId = correlationId;
        OccurredAt = occurredAt;
    }

    internal Guid Id { get; private set; }
    public TenantId TenantId { get; private set; }
    internal Guid SupplierReturnId { get; private set; }
    internal SupplierReturnStatus FromStatus { get; private set; }
    internal SupplierReturnStatus ToStatus { get; private set; }
    internal SupplierReturnHistoryAction Action { get; private set; }
    internal Guid ActorId { get; private set; }
    internal string? Reason { get; private set; }
    internal string CorrelationId { get; private set; }
    internal DateTimeOffset OccurredAt { get; private set; }
    internal byte[] Version { get; private set; } = [];
}

internal sealed class SupplierReturnAuditEntity : ITenantOwned
{
    private SupplierReturnAuditEntity()
    {
        OperationId = string.Empty;
        CorrelationId = string.Empty;
        AuthorizationPath = string.Empty;
        Decision = string.Empty;
    }

    internal SupplierReturnAuditEntity(SupplierReturnAuditEvidence evidence)
    {
        Id = evidence.EvidenceId;
        TenantId = new TenantId(evidence.TenantId);
        SupplierReturnId = evidence.SupplierReturnId;
        OccurredAt = evidence.OccurredAt;
        OperationId = evidence.OperationId;
        CorrelationId = evidence.CorrelationId;
        ActorId = evidence.ActorId;
        SessionId = evidence.SessionId;
        AuthorizationPath = evidence.AuthorizationPath;
        Decision = evidence.Decision;
        Reason = evidence.Reason;
        BeforeStatus = evidence.BeforeStatus;
        AfterStatus = evidence.AfterStatus;
        CompanyId = evidence.CompanyId;
        BranchId = evidence.BranchId;
        BeforeSummary = evidence.BeforeSummary;
        AfterSummary = evidence.AfterSummary;
        IdempotencyKey = evidence.IdempotencyKey;
        RequestFingerprint = evidence.RequestFingerprint;
    }

    internal Guid Id { get; private set; }
    public TenantId TenantId { get; private set; }
    internal Guid SupplierReturnId { get; private set; }
    internal DateTimeOffset OccurredAt { get; private set; }
    internal string OperationId { get; private set; }
    internal string CorrelationId { get; private set; }
    internal Guid ActorId { get; private set; }
    internal Guid SessionId { get; private set; }
    internal string AuthorizationPath { get; private set; }
    internal string Decision { get; private set; }
    internal string? Reason { get; private set; }
    internal SupplierReturnStatus? BeforeStatus { get; private set; }
    internal SupplierReturnStatus? AfterStatus { get; private set; }
    internal Guid CompanyId { get; private set; }
    internal Guid? BranchId { get; private set; }
    internal string? BeforeSummary { get; private set; }
    internal string? AfterSummary { get; private set; }
    internal string? IdempotencyKey { get; private set; }
    internal string? RequestFingerprint { get; private set; }
    internal int? ReplayResponseSchemaVersion { get; private set; }
    internal string? ReplayResponseSnapshotJson { get; private set; }
    internal byte[] Version { get; private set; } = [];

    internal void SetReplayResponseSnapshot(int schemaVersion, string snapshotJson)
    {
        ReplayResponseSchemaVersion = schemaVersion;
        ReplayResponseSnapshotJson = snapshotJson;
    }
}

#pragma warning restore CS1591
