#pragma warning disable CS1591

using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.Modules.Procurement;
using MiniErp.Contracts.Modules.Procurement;

namespace MiniErp.Infrastructure.Persistence.Modules.Procurement;

internal sealed class GoodsReceiptEntity : ITenantOwned
{
    private GoodsReceiptEntity()
    {
        SupplierCode = string.Empty;
        SupplierName = string.Empty;
    }

    internal GoodsReceiptEntity(GoodsReceiptCreateCommand command, TenantId tenantId, Guid supplierId, string supplierCode, string supplierName)
    {
        Id = command.Id;
        TenantId = tenantId;
        PurchaseOrderId = command.PurchaseOrderId;
        WarehouseId = command.WarehouseId;
        CompanyId = command.Scope.CompanyId;
        BranchId = command.Scope.BranchId;
        ReceivedByActorId = command.ReceivedByActorId;
        SupplierId = supplierId;
        SupplierCode = supplierCode;
        SupplierName = supplierName;
        Status = GoodsReceiptStatus.Recorded;
        ReceivedDate = command.ReceivedDate;
        ReferenceNote = command.ReferenceNote;
        Notes = command.Notes;
        CreatedAt = command.OccurredAt;
        UpdatedAt = command.OccurredAt;
    }

    internal Guid Id { get; private set; }
    public TenantId TenantId { get; private set; }
    internal Guid PurchaseOrderId { get; private set; }
    internal Guid WarehouseId { get; private set; }
    internal Guid CompanyId { get; private set; }
    internal Guid? BranchId { get; private set; }
    internal Guid ReceivedByActorId { get; private set; }
    internal GoodsReceiptStatus Status { get; private set; }
    internal Guid SupplierId { get; private set; }
    internal string SupplierCode { get; private set; } = string.Empty;
    internal string SupplierName { get; private set; } = string.Empty;
    internal DateOnly ReceivedDate { get; private set; }
    internal string? ReferenceNote { get; private set; }
    internal string? Notes { get; private set; }
    internal DateTimeOffset CreatedAt { get; private set; }
    internal DateTimeOffset UpdatedAt { get; private set; }
    internal DateTimeOffset? CancelledAt { get; private set; }
    internal string? CancellationReason { get; private set; }
    internal byte[] Version { get; private set; } = [];

    internal List<GoodsReceiptLineEntity> Lines { get; } = [];

    internal void Cancel(string reason, DateTimeOffset occurredAt)
    {
        Status = GoodsReceiptStatus.Cancelled;
        CancellationReason = reason;
        CancelledAt = occurredAt;
        UpdatedAt = occurredAt;
    }

    internal void TouchVersion() => Version = Guid.NewGuid().ToByteArray();
}

internal sealed class GoodsReceiptLineEntity : ITenantOwned
{
    private GoodsReceiptLineEntity()
    {
        ProductSku = string.Empty;
        ProductName = string.Empty;
        UnitOfMeasureCode = string.Empty;
    }

    internal GoodsReceiptLineEntity(
        TenantId tenantId,
        Guid goodsReceiptId,
        Guid id,
        GoodsReceiptLineCreateCommand command,
        Guid productId,
        string productSku,
        string productName,
        string unitOfMeasureCode,
        decimal orderedQuantityAtReceipt,
        decimal remainingReceivableQuantityAfter)
    {
        Id = id;
        TenantId = tenantId;
        GoodsReceiptId = goodsReceiptId;
        PurchaseOrderLineId = command.PurchaseOrderLineId;
        ProductId = productId;
        ProductSku = productSku;
        ProductName = productName;
        UnitOfMeasureCode = unitOfMeasureCode;
        OrderedQuantityAtReceipt = orderedQuantityAtReceipt;
        ReceivedQuantity = command.ReceivedQuantity;
        AcceptedQuantity = command.AcceptedQuantity;
        RejectedQuantity = command.RejectedQuantity;
        DamagedQuantity = command.DamagedQuantity;
        DamageNotes = command.DamageNotes;
        RemainingReceivableQuantityAfter = remainingReceivableQuantityAfter;
        Notes = command.Notes;
    }

    internal Guid Id { get; private set; }
    public TenantId TenantId { get; private set; }
    internal Guid GoodsReceiptId { get; private set; }
    internal Guid PurchaseOrderLineId { get; private set; }
    internal Guid ProductId { get; private set; }
    internal string ProductSku { get; private set; }
    internal string ProductName { get; private set; }
    internal string UnitOfMeasureCode { get; private set; }
    internal decimal OrderedQuantityAtReceipt { get; private set; }
    internal decimal ReceivedQuantity { get; private set; }
    internal decimal AcceptedQuantity { get; private set; }
    internal decimal RejectedQuantity { get; private set; }
    internal decimal? DamagedQuantity { get; private set; }
    internal string? DamageNotes { get; private set; }
    internal decimal RemainingReceivableQuantityAfter { get; private set; }
    internal string? Notes { get; private set; }
    internal byte[] Version { get; private set; } = [];
}

internal sealed class GoodsReceiptHistoryEntity : ITenantOwned
{
    private GoodsReceiptHistoryEntity() => CorrelationId = string.Empty;

    internal GoodsReceiptHistoryEntity(Guid id, TenantId tenantId, Guid goodsReceiptId, GoodsReceiptStatus fromStatus, GoodsReceiptStatus toStatus, GoodsReceiptHistoryAction action, Guid actorId, string? reason, string correlationId, DateTimeOffset occurredAt)
    {
        Id = id;
        TenantId = tenantId;
        GoodsReceiptId = goodsReceiptId;
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
    internal Guid GoodsReceiptId { get; private set; }
    internal GoodsReceiptStatus FromStatus { get; private set; }
    internal GoodsReceiptStatus ToStatus { get; private set; }
    internal GoodsReceiptHistoryAction Action { get; private set; }
    internal Guid ActorId { get; private set; }
    internal string? Reason { get; private set; }
    internal string CorrelationId { get; private set; }
    internal DateTimeOffset OccurredAt { get; private set; }
    internal byte[] Version { get; private set; } = [];
}

internal sealed class GoodsReceiptAuditEntity : ITenantOwned
{
    private GoodsReceiptAuditEntity()
    {
        OperationId = string.Empty;
        CorrelationId = string.Empty;
        AuthorizationPath = string.Empty;
        Decision = string.Empty;
    }

    internal GoodsReceiptAuditEntity(GoodsReceiptAuditEvidence evidence)
    {
        Id = evidence.EvidenceId;
        TenantId = new TenantId(evidence.TenantId);
        GoodsReceiptId = evidence.GoodsReceiptId;
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
    internal Guid GoodsReceiptId { get; private set; }
    internal DateTimeOffset OccurredAt { get; private set; }
    internal string OperationId { get; private set; }
    internal string CorrelationId { get; private set; }
    internal Guid ActorId { get; private set; }
    internal Guid SessionId { get; private set; }
    internal string AuthorizationPath { get; private set; }
    internal string Decision { get; private set; }
    internal string? Reason { get; private set; }
    internal GoodsReceiptStatus? BeforeStatus { get; private set; }
    internal GoodsReceiptStatus? AfterStatus { get; private set; }
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
