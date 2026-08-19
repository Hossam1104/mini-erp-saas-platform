#pragma warning disable CS1591

using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.Modules.Procurement;
using MiniErp.Contracts.Modules.Procurement;

namespace MiniErp.Infrastructure.Persistence.Modules.Procurement;

internal sealed class PurchaseInvoiceHandoffEntity : ITenantOwned
{
    private PurchaseInvoiceHandoffEntity()
    {
        SupplierCode = string.Empty;
        SupplierName = string.Empty;
        CurrencyCode = string.Empty;
    }

    internal PurchaseInvoiceHandoffEntity(PurchaseInvoiceHandoffCreateCommand command, TenantId tenantId, Guid supplierId, string supplierCode, string supplierName, string currencyCode)
    {
        Id = command.Id;
        TenantId = tenantId;
        PurchaseOrderId = command.PurchaseOrderId;
        CompanyId = command.Scope.CompanyId;
        BranchId = command.Scope.BranchId;
        CreatedByActorId = command.CreatedByActorId;
        SupplierId = supplierId;
        SupplierCode = supplierCode;
        SupplierName = supplierName;
        CurrencyCode = currencyCode;
        Status = PurchaseInvoiceHandoffStatus.Recorded;
        SupplierInvoiceReference = command.SupplierInvoiceReference;
        SupplierInvoiceDate = command.SupplierInvoiceDate;
        Notes = command.Notes;
        CreatedAt = command.OccurredAt;
        UpdatedAt = command.OccurredAt;
    }

    internal Guid Id { get; private set; }
    public TenantId TenantId { get; private set; }
    internal Guid PurchaseOrderId { get; private set; }
    internal Guid CompanyId { get; private set; }
    internal Guid? BranchId { get; private set; }
    internal Guid CreatedByActorId { get; private set; }
    internal PurchaseInvoiceHandoffStatus Status { get; private set; }
    internal Guid SupplierId { get; private set; }
    internal string SupplierCode { get; private set; } = string.Empty;
    internal string SupplierName { get; private set; } = string.Empty;
    internal string CurrencyCode { get; private set; } = string.Empty;
    internal string? SupplierInvoiceReference { get; private set; }
    internal DateOnly? SupplierInvoiceDate { get; private set; }
    internal string? Notes { get; private set; }
    internal DateTimeOffset CreatedAt { get; private set; }
    internal DateTimeOffset UpdatedAt { get; private set; }
    internal DateTimeOffset? CancelledAt { get; private set; }
    internal string? CancellationReason { get; private set; }
    internal byte[] Version { get; private set; } = [];

    internal List<PurchaseInvoiceHandoffLineEntity> Lines { get; } = [];
    internal List<PurchaseInvoiceHandoffSourceEntity> Sources { get; } = [];

    internal void Cancel(string reason, DateTimeOffset occurredAt)
    {
        Status = PurchaseInvoiceHandoffStatus.Cancelled;
        CancellationReason = reason;
        CancelledAt = occurredAt;
        UpdatedAt = occurredAt;
    }

    internal void TouchVersion() => Version = Guid.NewGuid().ToByteArray();
}

internal sealed class PurchaseInvoiceHandoffLineEntity : ITenantOwned
{
    private PurchaseInvoiceHandoffLineEntity()
    {
        ProductSku = string.Empty;
        ProductName = string.Empty;
        UnitOfMeasureCode = string.Empty;
    }

    internal PurchaseInvoiceHandoffLineEntity(
        TenantId tenantId,
        Guid purchaseInvoiceHandoffId,
        Guid id,
        Guid purchaseOrderLineId,
        Guid productId,
        string productSku,
        string productName,
        string unitOfMeasureCode,
        decimal handoffQuantity,
        decimal unitPrice,
        decimal? taxRatePercentage,
        decimal? taxAmount,
        decimal lineAmount)
    {
        Id = id;
        TenantId = tenantId;
        PurchaseInvoiceHandoffId = purchaseInvoiceHandoffId;
        PurchaseOrderLineId = purchaseOrderLineId;
        ProductId = productId;
        ProductSku = productSku;
        ProductName = productName;
        UnitOfMeasureCode = unitOfMeasureCode;
        HandoffQuantity = handoffQuantity;
        UnitPrice = unitPrice;
        TaxRatePercentage = taxRatePercentage;
        TaxAmount = taxAmount;
        LineAmount = lineAmount;
    }

    internal Guid Id { get; private set; }
    public TenantId TenantId { get; private set; }
    internal Guid PurchaseInvoiceHandoffId { get; private set; }
    internal Guid PurchaseOrderLineId { get; private set; }
    internal Guid ProductId { get; private set; }
    internal string ProductSku { get; private set; }
    internal string ProductName { get; private set; }
    internal string UnitOfMeasureCode { get; private set; }
    internal decimal HandoffQuantity { get; private set; }
    internal decimal UnitPrice { get; private set; }
    internal decimal? TaxRatePercentage { get; private set; }
    internal decimal? TaxAmount { get; private set; }
    internal decimal LineAmount { get; private set; }
    internal byte[] Version { get; private set; } = [];
}

internal sealed class PurchaseInvoiceHandoffSourceEntity : ITenantOwned
{
    private PurchaseInvoiceHandoffSourceEntity()
    {
    }

    internal PurchaseInvoiceHandoffSourceEntity(TenantId tenantId, Guid purchaseInvoiceHandoffId, Guid id, Guid goodsReceiptId, Guid goodsReceiptLineId, Guid purchaseOrderLineId, decimal quantity)
    {
        Id = id;
        TenantId = tenantId;
        PurchaseInvoiceHandoffId = purchaseInvoiceHandoffId;
        GoodsReceiptId = goodsReceiptId;
        GoodsReceiptLineId = goodsReceiptLineId;
        PurchaseOrderLineId = purchaseOrderLineId;
        Quantity = quantity;
    }

    internal Guid Id { get; private set; }
    public TenantId TenantId { get; private set; }
    internal Guid PurchaseInvoiceHandoffId { get; private set; }
    internal Guid GoodsReceiptId { get; private set; }
    internal Guid GoodsReceiptLineId { get; private set; }
    internal Guid PurchaseOrderLineId { get; private set; }
    internal decimal Quantity { get; private set; }
    internal byte[] Version { get; private set; } = [];
}

internal sealed class PurchaseInvoiceHandoffHistoryEntity : ITenantOwned
{
    private PurchaseInvoiceHandoffHistoryEntity() => CorrelationId = string.Empty;

    internal PurchaseInvoiceHandoffHistoryEntity(Guid id, TenantId tenantId, Guid purchaseInvoiceHandoffId, PurchaseInvoiceHandoffStatus fromStatus, PurchaseInvoiceHandoffStatus toStatus, PurchaseInvoiceHandoffHistoryAction action, Guid actorId, string? reason, string correlationId, DateTimeOffset occurredAt)
    {
        Id = id;
        TenantId = tenantId;
        PurchaseInvoiceHandoffId = purchaseInvoiceHandoffId;
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
    internal Guid PurchaseInvoiceHandoffId { get; private set; }
    internal PurchaseInvoiceHandoffStatus FromStatus { get; private set; }
    internal PurchaseInvoiceHandoffStatus ToStatus { get; private set; }
    internal PurchaseInvoiceHandoffHistoryAction Action { get; private set; }
    internal Guid ActorId { get; private set; }
    internal string? Reason { get; private set; }
    internal string CorrelationId { get; private set; }
    internal DateTimeOffset OccurredAt { get; private set; }
    internal byte[] Version { get; private set; } = [];
}

internal sealed class PurchaseInvoiceHandoffAuditEntity : ITenantOwned
{
    private PurchaseInvoiceHandoffAuditEntity()
    {
        OperationId = string.Empty;
        CorrelationId = string.Empty;
        AuthorizationPath = string.Empty;
        Decision = string.Empty;
    }

    internal PurchaseInvoiceHandoffAuditEntity(PurchaseInvoiceHandoffAuditEvidence evidence)
    {
        Id = evidence.EvidenceId;
        TenantId = new TenantId(evidence.TenantId);
        PurchaseInvoiceHandoffId = evidence.PurchaseInvoiceHandoffId;
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
    internal Guid PurchaseInvoiceHandoffId { get; private set; }
    internal DateTimeOffset OccurredAt { get; private set; }
    internal string OperationId { get; private set; }
    internal string CorrelationId { get; private set; }
    internal Guid ActorId { get; private set; }
    internal Guid SessionId { get; private set; }
    internal string AuthorizationPath { get; private set; }
    internal string Decision { get; private set; }
    internal string? Reason { get; private set; }
    internal PurchaseInvoiceHandoffStatus? BeforeStatus { get; private set; }
    internal PurchaseInvoiceHandoffStatus? AfterStatus { get; private set; }
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
