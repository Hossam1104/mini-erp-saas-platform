#pragma warning disable CS1591

using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.Contracts.Modules.Inventory;

namespace MiniErp.Infrastructure.Persistence.Modules.Inventory;

internal sealed class InventoryReasonCodeEntity : ITenantOwned
{
    private InventoryReasonCodeEntity() { }
    internal InventoryReasonCodeEntity(TenantId tenantId, Guid id, string code, string englishName, string arabicName, InventoryReasonCategory category, Guid actorId, DateTimeOffset at)
    {
        Id = id; TenantId = tenantId; Code = code; EnglishName = englishName; ArabicName = arabicName; Category = category; IsActive = true;
        CreatedByActorId = actorId; CreatedAt = at; UpdatedAt = at; TouchVersion();
    }

    internal Guid Id { get; private set; }
    public TenantId TenantId { get; private set; }
    internal string Code { get; private set; } = string.Empty;
    internal string EnglishName { get; private set; } = string.Empty;
    internal string ArabicName { get; private set; } = string.Empty;
    internal InventoryReasonCategory Category { get; private set; }
    internal bool IsActive { get; private set; }
    internal Guid CreatedByActorId { get; private set; }
    internal DateTimeOffset CreatedAt { get; private set; }
    internal DateTimeOffset UpdatedAt { get; private set; }
    internal byte[] Version { get; private set; } = [];
    internal void Update(string englishName, string arabicName, InventoryReasonCategory category, bool isActive, DateTimeOffset at)
    { EnglishName = englishName; ArabicName = arabicName; Category = category; IsActive = isActive; UpdatedAt = at; TouchVersion(); }
    internal void TouchVersion() => Version = Guid.NewGuid().ToByteArray();
}

internal sealed class InventoryAdjustmentEntity : ITenantOwned
{
    private InventoryAdjustmentEntity() { }
    internal InventoryAdjustmentEntity(TenantId tenantId, Guid id, Guid companyId, Guid? branchId, Guid warehouseId, string warehouseCode, string warehouseName, string? evidenceReference, Guid requesterId, DateTimeOffset at)
    {
        Id = id; TenantId = tenantId; CompanyId = companyId; BranchId = branchId; WarehouseId = warehouseId; WarehouseCode = warehouseCode; WarehouseName = warehouseName;
        EvidenceReference = evidenceReference; RequesterId = requesterId; Status = InventoryControlDocumentStatus.Draft; CreatedAt = at; UpdatedAt = at; TouchVersion();
    }
    internal Guid Id { get; private set; }
    public TenantId TenantId { get; private set; }
    internal Guid CompanyId { get; private set; }
    internal Guid? BranchId { get; private set; }
    internal Guid WarehouseId { get; private set; }
    internal string WarehouseCode { get; private set; } = string.Empty;
    internal string WarehouseName { get; private set; } = string.Empty;
    internal Guid RequesterId { get; private set; }
    internal InventoryControlDocumentStatus Status { get; private set; }
    internal string? EvidenceReference { get; private set; }
    internal string? ApprovalPolicySnapshotJson { get; private set; }
    internal string? CurrentStageApproverIdsJson { get; private set; }
    internal int CurrentApprovalStageIndex { get; private set; }
    internal int CurrentStageApprovalCount { get; private set; }
    internal Guid? LastApproverId { get; private set; }
    internal Guid? LastDelegatedFromActorId { get; private set; }
    internal DateTimeOffset CreatedAt { get; private set; }
    internal DateTimeOffset UpdatedAt { get; private set; }
    internal DateTimeOffset? SubmittedAt { get; private set; }
    internal DateTimeOffset? ApprovedAt { get; private set; }
    internal DateTimeOffset? PostedAt { get; private set; }
    internal byte[] Version { get; private set; } = [];
    internal List<InventoryAdjustmentLineEntity> Lines { get; } = [];
    internal void SetStatus(InventoryControlDocumentStatus status, DateTimeOffset at) { Status = status; UpdatedAt = at; TouchVersion(); }
    internal void Submit(bool requiresApproval, string? policyJson, DateTimeOffset at) { ApprovalPolicySnapshotJson = policyJson; CurrentStageApproverIdsJson = "[]"; CurrentApprovalStageIndex = 0; CurrentStageApprovalCount = 0; Status = requiresApproval ? InventoryControlDocumentStatus.PendingApproval : InventoryControlDocumentStatus.Approved; SubmittedAt = at; UpdatedAt = at; TouchVersion(); }
    internal void RecordApproval(Guid actorId, Guid? delegatedFrom, int stageIndex, IReadOnlyCollection<Guid> approvers, bool finalStage, DateTimeOffset at) { LastApproverId = actorId; LastDelegatedFromActorId = delegatedFrom; CurrentApprovalStageIndex = stageIndex; CurrentStageApprovalCount = approvers.Count; CurrentStageApproverIdsJson = finalStage ? "[]" : System.Text.Json.JsonSerializer.Serialize(approvers); if (finalStage) ApprovedAt = at; UpdatedAt = at; TouchVersion(); }
    internal void MarkPosted(DateTimeOffset at) { Status = InventoryControlDocumentStatus.Posted; PostedAt = at; UpdatedAt = at; TouchVersion(); }
    internal void TouchVersion() => Version = Guid.NewGuid().ToByteArray();
}

internal sealed class InventoryAdjustmentLineEntity : ITenantOwned
{
    private InventoryAdjustmentLineEntity() { }
    internal InventoryAdjustmentLineEntity(TenantId tenantId, Guid id, Guid adjustmentId, Guid productId, string sku, string name, Guid uomId, string uomCode, InventoryAdjustmentDirection direction, decimal quantity, string trackingIdentity, Guid reasonId, string reasonCode, string reasonEnglish, string reasonArabic, string? evidenceReference)
    {
        Id = id; TenantId = tenantId; AdjustmentId = adjustmentId; ProductId = productId; ProductSku = sku; ProductName = name; UnitOfMeasureId = uomId; UnitOfMeasureCode = uomCode; Direction = direction; Quantity = quantity; TrackingIdentity = trackingIdentity; ReasonCodeId = reasonId; ReasonCode = reasonCode; ReasonEnglishName = reasonEnglish; ReasonArabicName = reasonArabic; EvidenceReference = evidenceReference; TouchVersion();
    }
    internal Guid Id { get; private set; }
    public TenantId TenantId { get; private set; }
    internal Guid AdjustmentId { get; private set; }
    internal InventoryAdjustmentEntity Adjustment { get; private set; } = null!;
    internal Guid ProductId { get; private set; }
    internal string ProductSku { get; private set; } = string.Empty;
    internal string ProductName { get; private set; } = string.Empty;
    internal Guid UnitOfMeasureId { get; private set; }
    internal string UnitOfMeasureCode { get; private set; } = string.Empty;
    internal InventoryAdjustmentDirection Direction { get; private set; }
    internal decimal Quantity { get; private set; }
    internal string TrackingIdentity { get; private set; } = string.Empty;
    internal Guid ReasonCodeId { get; private set; }
    internal string ReasonCode { get; private set; } = string.Empty;
    internal string ReasonEnglishName { get; private set; } = string.Empty;
    internal string ReasonArabicName { get; private set; } = string.Empty;
    internal string? EvidenceReference { get; private set; }
    internal Guid? MovementId { get; private set; }
    internal byte[] Version { get; private set; } = [];
    internal void MarkPosted(Guid movementId) { MovementId = movementId; TouchVersion(); }
    internal void TouchVersion() => Version = Guid.NewGuid().ToByteArray();
}

internal sealed class InventoryCountEntity : ITenantOwned
{
    private InventoryCountEntity() { }
    internal InventoryCountEntity(TenantId tenantId, Guid id, Guid companyId, Guid? branchId, Guid warehouseId, string warehouseCode, string warehouseName, InventoryCountType countType, Guid assignedCounterId, Guid? reviewerId, DateTimeOffset cutoff, long? snapshotWarehouseMovementCount, Guid actorId, DateTimeOffset at, string? approvalPolicyJson = null)
    {
        Id = id; TenantId = tenantId; CompanyId = companyId; BranchId = branchId; WarehouseId = warehouseId; WarehouseCode = warehouseCode; WarehouseName = warehouseName; CountType = countType; AssignedCounterId = assignedCounterId; ReviewerId = reviewerId; CurrentRoundGeneration = 1; SnapshotCutoff = cutoff; SnapshotWarehouseMovementCount = snapshotWarehouseMovementCount; Status = InventoryControlDocumentStatus.Draft; ApprovalPolicySnapshotJson = approvalPolicyJson; CurrentStageApproverIdsJson = "[]"; CreatedByActorId = actorId; CreatedAt = at; UpdatedAt = at; TouchVersion();
    }
    internal Guid Id { get; private set; }
    public TenantId TenantId { get; private set; }
    internal Guid CompanyId { get; private set; }
    internal Guid? BranchId { get; private set; }
    internal Guid WarehouseId { get; private set; }
    internal string WarehouseCode { get; private set; } = string.Empty;
    internal string WarehouseName { get; private set; } = string.Empty;
    internal InventoryCountType CountType { get; private set; }
    internal Guid AssignedCounterId { get; private set; }
    internal Guid? ReviewerId { get; private set; }
    internal Guid? ApproverId { get; private set; }
    internal Guid? PosterId { get; private set; }
    internal InventoryControlDocumentStatus Status { get; private set; }
    internal string? ApprovalPolicySnapshotJson { get; private set; }
    internal string? CurrentStageApproverIdsJson { get; private set; }
    internal int CurrentApprovalStageIndex { get; private set; }
    internal int CurrentStageApprovalCount { get; private set; }
    internal Guid? LastApproverId { get; private set; }
    internal Guid? LastDelegatedFromActorId { get; private set; }
    internal int CurrentRoundGeneration { get; private set; }
    internal DateTimeOffset SnapshotCutoff { get; private set; }
    internal long? SnapshotWarehouseMovementCount { get; private set; }
    internal Guid CreatedByActorId { get; private set; }
    internal DateTimeOffset CreatedAt { get; private set; }
    internal DateTimeOffset UpdatedAt { get; private set; }
    internal DateTimeOffset? SubmittedAt { get; private set; }
    internal DateTimeOffset? ApprovedAt { get; private set; }
    internal DateTimeOffset? PostedAt { get; private set; }
    internal byte[] Version { get; private set; } = [];
    internal List<InventoryCountLineEntity> Lines { get; } = [];
    internal List<InventoryCountSnapshotEntity> Snapshots { get; } = [];
    internal void SetStatus(InventoryControlDocumentStatus status, DateTimeOffset at) { Status = status; UpdatedAt = at; TouchVersion(); }
    internal void MarkSubmitted(InventoryControlDocumentStatus status, DateTimeOffset at) { Status = status; SubmittedAt = at; UpdatedAt = at; TouchVersion(); }
    internal void MarkApproved(Guid actorId, DateTimeOffset at) { ApproverId = actorId; LastApproverId = actorId; ApprovedAt = at; Status = InventoryControlDocumentStatus.Approved; UpdatedAt = at; TouchVersion(); }
    internal void MarkPosted(Guid actorId, DateTimeOffset at) { PosterId = actorId; PostedAt = at; Status = InventoryControlDocumentStatus.Posted; UpdatedAt = at; TouchVersion(); }
    internal void RecordApproval(Guid actorId, Guid? delegatedFrom, int stageIndex, IReadOnlyCollection<Guid> approvers, bool finalStage, DateTimeOffset at) { LastApproverId = actorId; LastDelegatedFromActorId = delegatedFrom; CurrentApprovalStageIndex = stageIndex; CurrentStageApprovalCount = approvers.Count; CurrentStageApproverIdsJson = finalStage ? "[]" : System.Text.Json.JsonSerializer.Serialize(approvers); if (finalStage) { ApproverId = actorId; ApprovedAt = at; Status = InventoryControlDocumentStatus.Approved; } UpdatedAt = at; TouchVersion(); }
    internal void BeginNewRound(DateTimeOffset cutoff, long? snapshotWarehouseMovementCount, DateTimeOffset at) { CurrentRoundGeneration++; SnapshotCutoff = cutoff; SnapshotWarehouseMovementCount = snapshotWarehouseMovementCount; Status = InventoryControlDocumentStatus.Draft; ApproverId = null; ApprovedAt = null; LastApproverId = null; LastDelegatedFromActorId = null; CurrentApprovalStageIndex = 0; CurrentStageApprovalCount = 0; CurrentStageApproverIdsJson = "[]"; UpdatedAt = at; TouchVersion(); }
    internal void TouchVersion() => Version = Guid.NewGuid().ToByteArray();
}

internal sealed class InventoryCountSnapshotEntity : ITenantOwned
{
    private InventoryCountSnapshotEntity() { }
    internal InventoryCountSnapshotEntity(TenantId tenantId, Guid id, Guid countId, int roundGeneration, DateTimeOffset snapshotCutoff, long? snapshotWarehouseMovementCount, DateTimeOffset at)
    {
        Id = id; TenantId = tenantId; CountId = countId; RoundGeneration = roundGeneration; SnapshotCutoff = snapshotCutoff; SnapshotWarehouseMovementCount = snapshotWarehouseMovementCount; CreatedAt = at; TouchVersion();
    }

    internal Guid Id { get; private set; }
    public TenantId TenantId { get; private set; }
    internal Guid CountId { get; private set; }
    internal InventoryCountEntity Count { get; private set; } = null!;
    internal int RoundGeneration { get; private set; }
    internal DateTimeOffset SnapshotCutoff { get; private set; }
    internal long? SnapshotWarehouseMovementCount { get; private set; }
    internal DateTimeOffset CreatedAt { get; private set; }
    internal byte[] Version { get; private set; } = [];
    internal void TouchVersion() => Version = Guid.NewGuid().ToByteArray();
}

internal sealed class InventoryCountLineEntity : ITenantOwned
{
    private InventoryCountLineEntity() { }
    internal InventoryCountLineEntity(TenantId tenantId, Guid id, Guid countId, Guid? priorLineId, int roundGeneration, Guid productId, string sku, string name, Guid uomId, string uomCode, string trackingIdentity, decimal expectedQuantity, long snapshotIdentityMovementCount)
    {
        Id = id; TenantId = tenantId; CountId = countId; PriorLineId = priorLineId; RoundGeneration = roundGeneration; ProductId = productId; ProductSku = sku; ProductName = name; UnitOfMeasureId = uomId; UnitOfMeasureCode = uomCode; TrackingIdentity = trackingIdentity; ExpectedQuantity = expectedQuantity; SnapshotIdentityMovementCount = snapshotIdentityMovementCount; TouchVersion();
    }
    internal Guid Id { get; private set; }
    public TenantId TenantId { get; private set; }
    internal Guid CountId { get; private set; }
    internal InventoryCountEntity Count { get; private set; } = null!;
    internal Guid? PriorLineId { get; private set; }
    internal int RoundGeneration { get; private set; }
    internal Guid ProductId { get; private set; }
    internal string ProductSku { get; private set; } = string.Empty;
    internal string ProductName { get; private set; } = string.Empty;
    internal Guid UnitOfMeasureId { get; private set; }
    internal string UnitOfMeasureCode { get; private set; } = string.Empty;
    internal string TrackingIdentity { get; private set; } = string.Empty;
    internal decimal ExpectedQuantity { get; private set; }
    internal long SnapshotIdentityMovementCount { get; private set; }
    internal decimal? CountedQuantity { get; private set; }
    internal decimal? Variance { get; private set; }
    internal Guid? VarianceReasonCodeId { get; private set; }
    internal string? VarianceReasonCode { get; private set; }
    internal string? VarianceReasonEnglishName { get; private set; }
    internal string? VarianceReasonArabicName { get; private set; }
    internal DateTimeOffset? CountedAt { get; private set; }
    internal Guid? MovementId { get; private set; }
    internal byte[] Version { get; private set; } = [];
    internal void SetObservation(decimal counted, DateTimeOffset at)
    { CountedQuantity = counted; Variance = counted - ExpectedQuantity; VarianceReasonCodeId = null; VarianceReasonCode = null; VarianceReasonEnglishName = null; VarianceReasonArabicName = null; CountedAt = at; TouchVersion(); }
    internal void SetVarianceReason(InventoryReasonCodeRecord reason) { VarianceReasonCodeId = reason.Id; VarianceReasonCode = reason.Code; VarianceReasonEnglishName = reason.EnglishName; VarianceReasonArabicName = reason.ArabicName; TouchVersion(); }
    internal void MarkPosted(Guid movementId) { MovementId = movementId; TouchVersion(); }
    internal void TouchVersion() => Version = Guid.NewGuid().ToByteArray();
}

internal sealed class InventoryStockIssueEntity : ITenantOwned
{
    private InventoryStockIssueEntity() { }
    internal InventoryStockIssueEntity(TenantId tenantId, Guid id, Guid companyId, Guid? branchId, Guid warehouseId, string warehouseCode, string warehouseName, string destinationUseDescription, Guid requesterId, DateTimeOffset at)
    {
        Id = id; TenantId = tenantId; CompanyId = companyId; BranchId = branchId; WarehouseId = warehouseId; WarehouseCode = warehouseCode; WarehouseName = warehouseName; DestinationUseDescription = destinationUseDescription; RequesterId = requesterId; Status = InventoryControlDocumentStatus.Draft; CreatedAt = at; UpdatedAt = at; TouchVersion();
    }
    internal Guid Id { get; private set; }
    public TenantId TenantId { get; private set; }
    internal Guid CompanyId { get; private set; }
    internal Guid? BranchId { get; private set; }
    internal Guid WarehouseId { get; private set; }
    internal string WarehouseCode { get; private set; } = string.Empty;
    internal string WarehouseName { get; private set; } = string.Empty;
    internal string DestinationUseDescription { get; private set; } = string.Empty;
    internal Guid RequesterId { get; private set; }
    internal InventoryControlDocumentStatus Status { get; private set; }
    internal string? ApprovalPolicySnapshotJson { get; private set; }
    internal string? CurrentStageApproverIdsJson { get; private set; }
    internal int CurrentApprovalStageIndex { get; private set; }
    internal int CurrentStageApprovalCount { get; private set; }
    internal Guid? LastApproverId { get; private set; }
    internal Guid? LastDelegatedFromActorId { get; private set; }
    internal DateTimeOffset CreatedAt { get; private set; }
    internal DateTimeOffset UpdatedAt { get; private set; }
    internal DateTimeOffset? SubmittedAt { get; private set; }
    internal DateTimeOffset? ApprovedAt { get; private set; }
    internal DateTimeOffset? PostedAt { get; private set; }
    internal byte[] Version { get; private set; } = [];
    internal List<InventoryStockIssueLineEntity> Lines { get; } = [];
    internal void Submit(bool requiresApproval, string? policyJson, DateTimeOffset at) { ApprovalPolicySnapshotJson = policyJson; CurrentStageApproverIdsJson = "[]"; CurrentApprovalStageIndex = 0; CurrentStageApprovalCount = 0; Status = requiresApproval ? InventoryControlDocumentStatus.PendingApproval : InventoryControlDocumentStatus.Approved; SubmittedAt = at; UpdatedAt = at; TouchVersion(); }
    internal void RecordApproval(Guid actorId, Guid? delegatedFrom, int stageIndex, IReadOnlyCollection<Guid> approvers, bool finalStage, DateTimeOffset at) { LastApproverId = actorId; LastDelegatedFromActorId = delegatedFrom; CurrentApprovalStageIndex = stageIndex; CurrentStageApprovalCount = approvers.Count; CurrentStageApproverIdsJson = finalStage ? "[]" : System.Text.Json.JsonSerializer.Serialize(approvers); if (finalStage) ApprovedAt = at; UpdatedAt = at; TouchVersion(); }
    internal void SetStatus(InventoryControlDocumentStatus status, DateTimeOffset at) { Status = status; UpdatedAt = at; TouchVersion(); }
    internal void MarkPosted(DateTimeOffset at) { Status = InventoryControlDocumentStatus.Posted; PostedAt = at; UpdatedAt = at; TouchVersion(); }
    internal void TouchVersion() => Version = Guid.NewGuid().ToByteArray();
}

internal sealed class InventoryStockIssueLineEntity : ITenantOwned
{
    private InventoryStockIssueLineEntity() { }
    internal InventoryStockIssueLineEntity(TenantId tenantId, Guid id, Guid issueId, Guid productId, string sku, string name, Guid uomId, string uomCode, decimal quantity, string trackingIdentity, Guid reasonId, string reasonCode, string reasonEnglish, string reasonArabic, string? evidenceReference)
    {
        Id = id; TenantId = tenantId; StockIssueId = issueId; ProductId = productId; ProductSku = sku; ProductName = name; UnitOfMeasureId = uomId; UnitOfMeasureCode = uomCode; Quantity = quantity; TrackingIdentity = trackingIdentity; ReasonCodeId = reasonId; ReasonCode = reasonCode; ReasonEnglishName = reasonEnglish; ReasonArabicName = reasonArabic; EvidenceReference = evidenceReference; TouchVersion();
    }
    internal Guid Id { get; private set; }
    public TenantId TenantId { get; private set; }
    internal Guid StockIssueId { get; private set; }
    internal InventoryStockIssueEntity StockIssue { get; private set; } = null!;
    internal Guid ProductId { get; private set; }
    internal string ProductSku { get; private set; } = string.Empty;
    internal string ProductName { get; private set; } = string.Empty;
    internal Guid UnitOfMeasureId { get; private set; }
    internal string UnitOfMeasureCode { get; private set; } = string.Empty;
    internal decimal Quantity { get; private set; }
    internal string TrackingIdentity { get; private set; } = string.Empty;
    internal Guid ReasonCodeId { get; private set; }
    internal string ReasonCode { get; private set; } = string.Empty;
    internal string ReasonEnglishName { get; private set; } = string.Empty;
    internal string ReasonArabicName { get; private set; } = string.Empty;
    internal string? EvidenceReference { get; private set; }
    internal Guid? MovementId { get; private set; }
    internal byte[] Version { get; private set; } = [];
    internal void MarkPosted(Guid movementId) { MovementId = movementId; TouchVersion(); }
    internal void TouchVersion() => Version = Guid.NewGuid().ToByteArray();
}

internal sealed class InventoryControlHistoryEntity : ITenantOwned
{
    private InventoryControlHistoryEntity() { }
    internal InventoryControlHistoryEntity(TenantId tenantId, Guid id, string resourceType, Guid resourceId, Guid? lineId, InventoryControlHistoryAction action, InventoryControlDocumentStatus fromStatus, InventoryControlDocumentStatus toStatus, Guid actorId, Guid? delegatedFromActorId, string? reason, string correlationId, int roundGeneration, DateTimeOffset at)
    { Id = id; TenantId = tenantId; ResourceType = resourceType; ResourceId = resourceId; LineId = lineId; Action = action; FromStatus = fromStatus; ToStatus = toStatus; ActorId = actorId; DelegatedFromActorId = delegatedFromActorId; Reason = reason; CorrelationId = correlationId; RoundGeneration = roundGeneration; OccurredAt = at; }
    internal Guid Id { get; private set; }
    public TenantId TenantId { get; private set; }
    internal string ResourceType { get; private set; } = string.Empty;
    internal Guid ResourceId { get; private set; }
    internal Guid? LineId { get; private set; }
    internal InventoryControlHistoryAction Action { get; private set; }
    internal InventoryControlDocumentStatus FromStatus { get; private set; }
    internal InventoryControlDocumentStatus ToStatus { get; private set; }
    internal Guid ActorId { get; private set; }
    internal Guid? DelegatedFromActorId { get; private set; }
    internal string? Reason { get; private set; }
    internal string CorrelationId { get; private set; } = string.Empty;
    internal int RoundGeneration { get; private set; }
    internal DateTimeOffset OccurredAt { get; private set; }
    internal byte[] Version { get; private set; } = [];
}

#pragma warning restore CS1591
