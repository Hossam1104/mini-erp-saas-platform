#pragma warning disable CS1591

using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.Modules.Procurement;
using MiniErp.Contracts.Modules.Procurement;

namespace MiniErp.Infrastructure.Persistence.Modules.Procurement;

internal sealed class PurchaseOrderEntity : ITenantOwned
{
    private PurchaseOrderEntity()
    {
        SupplierCode = string.Empty;
        SupplierName = string.Empty;
        SupplierQuotationReference = string.Empty;
        CurrencyCode = string.Empty;
        CurrencyName = string.Empty;
        SourcePurchaseRequestReference = string.Empty;
        SourceDecisionRationale = string.Empty;
        ApprovalPolicySnapshotJson = string.Empty;
        CurrentStageApproverIdsJson = "[]";
    }

    internal PurchaseOrderEntity(
        PurchaseOrderCreateCommand command,
        TenantId tenantId)
    {
        Id = command.Id;
        TenantId = tenantId;
        PurchaseRequestId = command.Source.PurchaseRequestId;
        SupplierQuotationId = command.Source.SupplierQuotationId;
        SourceDecisionId = command.Source.SourceDecisionId;
        CompanyId = command.Scope.CompanyId;
        BranchId = command.Scope.BranchId;
        CreatedByActorId = command.CreatedByActorId;
        SupplierId = command.Source.Supplier.Id;
        SupplierCode = command.Source.Supplier.Code;
        SupplierName = command.Source.Supplier.Name;
        SupplierQuotationReference = command.Source.SupplierQuotationReference;
        CurrencyId = command.Source.Currency.Id;
        CurrencyCode = command.Source.Currency.Code;
        CurrencyName = command.Source.Currency.Name;
        PaymentTermId = command.Source.PaymentTerm?.Id;
        PaymentTermCode = command.Source.PaymentTerm?.Code;
        PaymentTermName = command.Source.PaymentTerm?.Name;
        PaymentTermVersion = command.Source.PaymentTerm?.Version;
        SourcePurchaseRequestReference = command.Source.PurchaseRequestReference;
        SourcePurchaseRequestPurpose = command.Source.PurchaseRequestPurpose;
        SourceDecisionRationale = command.Source.SourceDecisionRationale;
        SourceSelectedAt = command.Source.SelectedAt;
        Status = PurchaseOrderStatus.Draft;
        CreatedAt = command.OccurredAt;
        UpdatedAt = command.OccurredAt;
    }

    internal Guid Id { get; private set; }

    public TenantId TenantId { get; private set; }

    internal Guid PurchaseRequestId { get; private set; }
    internal Guid SupplierQuotationId { get; private set; }
    internal Guid SourceDecisionId { get; private set; }
    internal Guid CompanyId { get; private set; }
    internal Guid? BranchId { get; private set; }
    internal Guid CreatedByActorId { get; private set; }
    internal Guid SupplierId { get; private set; }
    internal string SupplierCode { get; private set; } = string.Empty;
    internal string SupplierName { get; private set; } = string.Empty;
    internal string SupplierQuotationReference { get; private set; } = string.Empty;
    internal Guid CurrencyId { get; private set; }
    internal string CurrencyCode { get; private set; } = string.Empty;
    internal string CurrencyName { get; private set; } = string.Empty;
    internal Guid? PaymentTermId { get; private set; }
    internal string? PaymentTermCode { get; private set; }
    internal string? PaymentTermName { get; private set; }
    internal int? PaymentTermVersion { get; private set; }
    internal string SourcePurchaseRequestReference { get; private set; } = string.Empty;
    internal string? SourcePurchaseRequestPurpose { get; private set; }
    internal string SourceDecisionRationale { get; private set; } = string.Empty;
    internal DateTimeOffset SourceSelectedAt { get; private set; }
    internal PurchaseOrderStatus Status { get; private set; }
    internal string? Notes { get; private set; }
    internal DateTimeOffset CreatedAt { get; private set; }
    internal DateTimeOffset UpdatedAt { get; private set; }
    internal DateTimeOffset? SubmittedAt { get; private set; }
    internal DateTimeOffset? ApprovedAt { get; private set; }
    internal DateTimeOffset? IssuedAt { get; private set; }
    internal DateTimeOffset? CancelledAt { get; private set; }
    internal Guid? LatestConfirmationId { get; private set; }
    internal PurchaseOrderConfirmationStatus? LatestConfirmationStatus { get; private set; }
    internal PurchaseOrderStatus? StatusBeforeSupplierChange { get; private set; }
    internal string ApprovalPolicySnapshotJson { get; private set; } = string.Empty;
    internal int CurrentApprovalStageIndex { get; private set; }
    internal int CurrentStageApprovalCount { get; private set; }
    internal string CurrentStageApproverIdsJson { get; private set; } = "[]";
    internal bool IsReapproval { get; private set; }
    internal byte[] Version { get; private set; } = [];

    internal List<PurchaseOrderLineEntity> Lines { get; } = [];

    internal void Submit(PurchaseRequestApprovalPolicyDefinition policy, string policyJson, bool isReapproval, DateTimeOffset occurredAt)
    {
        Status = PurchaseOrderStatus.PendingApproval;
        ApprovalPolicySnapshotJson = policyJson;
        CurrentApprovalStageIndex = 0;
        CurrentStageApprovalCount = 0;
        CurrentStageApproverIdsJson = "[]";
        IsReapproval = isReapproval;
        SubmittedAt = occurredAt;
        UpdatedAt = occurredAt;
    }

    internal void RecordApproval(
        PurchaseOrderStatus resultingStatus,
        int stageIndex,
        int approvalCount,
        string approverIdsJson,
        DateTimeOffset occurredAt)
    {
        Status = resultingStatus;
        CurrentApprovalStageIndex = stageIndex;
        CurrentStageApprovalCount = approvalCount;
        CurrentStageApproverIdsJson = approverIdsJson;
        UpdatedAt = occurredAt;
        if (resultingStatus == PurchaseOrderStatus.Approved)
        {
            ApprovedAt = occurredAt;
        }
    }

    internal void ReplaceDraft(string? notes, DateTimeOffset occurredAt)
    {
        Notes = notes;
        UpdatedAt = occurredAt;
    }

    internal void SetStatus(PurchaseOrderStatus status, DateTimeOffset occurredAt)
    {
        Status = status;
        UpdatedAt = occurredAt;
        if (status == PurchaseOrderStatus.Issued)
        {
            IssuedAt = occurredAt;
        }

        if (status == PurchaseOrderStatus.Cancelled)
        {
            CancelledAt = occurredAt;
        }
    }

    internal void BeginSupplierChangeApproval(PurchaseOrderStatus priorStatus, DateTimeOffset occurredAt)
    {
        StatusBeforeSupplierChange = priorStatus;
        Status = PurchaseOrderStatus.ChangedPendingApproval;
        UpdatedAt = occurredAt;
    }

    internal void SetReapprovalPolicy(PurchaseRequestApprovalPolicyDefinition policy, string policyJson)
    {
        ApprovalPolicySnapshotJson = policyJson;
        CurrentApprovalStageIndex = 0;
        CurrentStageApprovalCount = 0;
        CurrentStageApproverIdsJson = "[]";
        IsReapproval = true;
    }

    internal void ResetApprovalState()
    {
        CurrentStageApprovalCount = 0;
        CurrentStageApproverIdsJson = "[]";
    }

    internal void CompleteSupplierChangeApproval(PurchaseOrderStatus resultingStatus, DateTimeOffset occurredAt)
    {
        StatusBeforeSupplierChange = null;
        Status = resultingStatus;
        UpdatedAt = occurredAt;
        if (resultingStatus == PurchaseOrderStatus.Approved)
        {
            ApprovedAt = occurredAt;
        }
    }

    internal void RejectSupplierChange(DateTimeOffset occurredAt)
    {
        Status = StatusBeforeSupplierChange ?? PurchaseOrderStatus.Issued;
        StatusBeforeSupplierChange = null;
        UpdatedAt = occurredAt;
    }

    internal void SetLatestConfirmation(Guid confirmationId, PurchaseOrderConfirmationStatus status, DateTimeOffset occurredAt)
    {
        LatestConfirmationId = confirmationId;
        LatestConfirmationStatus = status;
        Status = status switch
        {
            PurchaseOrderConfirmationStatus.Confirmed => PurchaseOrderStatus.Confirmed,
            PurchaseOrderConfirmationStatus.PartiallyConfirmed => PurchaseOrderStatus.PartiallyConfirmed,
            PurchaseOrderConfirmationStatus.Rejected => PurchaseOrderStatus.Rejected,
            PurchaseOrderConfirmationStatus.NoResponse => PurchaseOrderStatus.NoResponse,
            _ => Status
        };
        UpdatedAt = occurredAt;
    }

    internal void TouchVersion() => Version = Guid.NewGuid().ToByteArray();
}

internal sealed class PurchaseOrderLineEntity : ITenantOwned
{
    private PurchaseOrderLineEntity()
    {
        ProductSku = string.Empty;
        ProductName = string.Empty;
        UnitOfMeasureCode = string.Empty;
    }

    internal PurchaseOrderLineEntity(TenantId tenantId, Guid purchaseOrderId, Guid id, PurchaseOrderSourceLineSnapshot source)
    {
        Id = id;
        TenantId = tenantId;
        PurchaseOrderId = purchaseOrderId;
        SourceQuotationLineId = source.SourceQuotationLineId;
        PurchaseRequestLineId = source.PurchaseRequestLineId;
        ProductId = source.ProductId;
        ProductSku = source.ProductSku;
        ProductName = source.ProductName;
        UnitOfMeasureId = source.UnitOfMeasureId;
        UnitOfMeasureCode = source.UnitOfMeasureCode;
        RequestedQuantity = source.RequestedQuantity;
        OrderedQuantity = source.SelectedQuantity;
        ConfirmedQuantity = 0m;
        RemainingQuantity = source.SelectedQuantity;
        UnitPrice = source.UnitPrice;
        DiscountAmount = source.DiscountAmount;
        DiscountPercentage = source.DiscountPercentage;
        TaxId = source.TaxId;
        TaxCode = source.TaxCode;
        TaxName = source.TaxName;
        TaxRatePercentage = source.TaxRatePercentage;
        TaxAmount = source.TaxAmount;
        RequestedNeedByDate = source.RequestedNeedByDate;
        DeliveryDate = source.OfferedDeliveryDate;
    }

    internal Guid Id { get; private set; }
    public TenantId TenantId { get; private set; }
    internal Guid PurchaseOrderId { get; private set; }
    internal Guid SourceQuotationLineId { get; private set; }
    internal Guid PurchaseRequestLineId { get; private set; }
    internal Guid ProductId { get; private set; }
    internal string ProductSku { get; private set; }
    internal string ProductName { get; private set; }
    internal Guid UnitOfMeasureId { get; private set; }
    internal string UnitOfMeasureCode { get; private set; }
    internal decimal RequestedQuantity { get; private set; }
    internal decimal OrderedQuantity { get; private set; }
    internal decimal ConfirmedQuantity { get; private set; }
    internal decimal RemainingQuantity { get; private set; }
    internal decimal UnitPrice { get; private set; }
    internal decimal? DiscountAmount { get; private set; }
    internal decimal? DiscountPercentage { get; private set; }
    internal Guid? TaxId { get; private set; }
    internal string? TaxCode { get; private set; }
    internal string? TaxName { get; private set; }
    internal decimal? TaxRatePercentage { get; private set; }
    internal decimal? TaxAmount { get; private set; }
    internal DateOnly RequestedNeedByDate { get; private set; }
    internal DateOnly? DeliveryDate { get; private set; }
    internal string? Notes { get; private set; }
    internal byte[] Version { get; private set; } = [];

    internal void Edit(decimal orderedQuantity, decimal unitPrice, DateOnly? deliveryDate, string? notes)
    {
        OrderedQuantity = orderedQuantity;
        RemainingQuantity = Math.Max(0m, orderedQuantity - ConfirmedQuantity);
        UnitPrice = unitPrice;
        DeliveryDate = deliveryDate;
        Notes = notes;
    }

    internal void RecordConfirmation(decimal confirmedQuantity, DateOnly? expectedDeliveryDate)
    {
        ConfirmedQuantity = confirmedQuantity;
        RemainingQuantity = Math.Max(0m, OrderedQuantity - confirmedQuantity);
        if (expectedDeliveryDate is not null)
        {
            DeliveryDate = expectedDeliveryDate;
        }
    }

    internal void ApplySupplierChange(decimal? proposedQuantity, decimal? proposedUnitPrice, DateOnly? proposedDeliveryDate)
    {
        if (proposedQuantity is { } quantity)
        {
            OrderedQuantity = quantity;
        }

        if (proposedUnitPrice is { } price)
        {
            UnitPrice = price;
        }

        if (proposedDeliveryDate is { } date)
        {
            DeliveryDate = date;
        }

        RemainingQuantity = Math.Max(0m, OrderedQuantity - ConfirmedQuantity);
    }

    internal void TouchVersion() => Version = Guid.NewGuid().ToByteArray();
}

internal sealed class PurchaseOrderConfirmationEntity : ITenantOwned
{
    private PurchaseOrderConfirmationEntity()
    {
        SupplierReference = null;
        SupplierContact = null;
        Reason = null;
        Notes = null;
    }

    internal PurchaseOrderConfirmationEntity(
        TenantId tenantId,
        PurchaseOrderConfirmationCommand command)
    {
        Id = command.Id;
        TenantId = tenantId;
        PurchaseOrderId = command.PurchaseOrderId;
        Status = command.Status;
        ResponseDate = command.ResponseDate;
        SupplierReference = command.SupplierReference;
        SupplierContact = command.SupplierContact;
        Reason = command.Reason;
        Notes = command.Notes;
        RecordedByActorId = command.ActorId;
        RecordedAt = command.OccurredAt;
        PurchaseOrderVersion = command.PurchaseOrderVersion;
    }

    internal Guid Id { get; private set; }
    public TenantId TenantId { get; private set; }
    internal Guid PurchaseOrderId { get; private set; }
    internal PurchaseOrderConfirmationStatus Status { get; private set; }
    internal DateOnly ResponseDate { get; private set; }
    internal string? SupplierReference { get; private set; }
    internal string? SupplierContact { get; private set; }
    internal string? Reason { get; private set; }
    internal string? Notes { get; private set; }
    internal Guid RecordedByActorId { get; private set; }
    internal DateTimeOffset RecordedAt { get; private set; }
    internal byte[] PurchaseOrderVersion { get; private set; } = [];
    internal byte[] Version { get; private set; } = [];
    internal List<PurchaseOrderConfirmationLineEntity> Lines { get; } = [];
    internal List<PurchaseOrderEvidenceEntity> Evidence { get; } = [];
    internal List<PurchaseOrderSupplierChangeEntity> Changes { get; } = [];

    internal void TouchVersion() => Version = Guid.NewGuid().ToByteArray();
}

internal sealed class PurchaseOrderConfirmationLineEntity : ITenantOwned
{
    private PurchaseOrderConfirmationLineEntity()
    {
        ChangeReason = null;
    }

    internal PurchaseOrderConfirmationLineEntity(TenantId tenantId, Guid confirmationId, PurchaseOrderConfirmationLineCommand command)
    {
        Id = command.Id;
        TenantId = tenantId;
        PurchaseOrderConfirmationId = confirmationId;
        PurchaseOrderLineId = command.PurchaseOrderLineId;
        OrderedQuantityAtResponse = command.OrderedQuantityAtResponse;
        ConfirmedQuantity = command.ConfirmedQuantity;
        RemainingQuantity = command.RemainingQuantity;
        ExpectedDeliveryDate = command.ExpectedDeliveryDate;
        ProposedQuantity = command.ProposedQuantity;
        ProposedUnitPrice = command.ProposedUnitPrice;
        ProposedDeliveryDate = command.ProposedDeliveryDate;
        ChangeReason = command.ChangeReason;
    }

    internal Guid Id { get; private set; }
    public TenantId TenantId { get; private set; }
    internal Guid PurchaseOrderConfirmationId { get; private set; }
    internal Guid PurchaseOrderLineId { get; private set; }
    internal decimal OrderedQuantityAtResponse { get; private set; }
    internal decimal ConfirmedQuantity { get; private set; }
    internal decimal RemainingQuantity { get; private set; }
    internal DateOnly? ExpectedDeliveryDate { get; private set; }
    internal decimal? ProposedQuantity { get; private set; }
    internal decimal? ProposedUnitPrice { get; private set; }
    internal DateOnly? ProposedDeliveryDate { get; private set; }
    internal string? ChangeReason { get; private set; }
    internal byte[] Version { get; private set; } = [];
}

internal sealed class PurchaseOrderEvidenceEntity : ITenantOwned
{
    private PurchaseOrderEvidenceEntity() => Source = string.Empty;

    internal PurchaseOrderEvidenceEntity(TenantId tenantId, Guid confirmationId, PurchaseOrderEvidenceReference reference)
    {
        Id = reference.Id;
        TenantId = tenantId;
        PurchaseOrderConfirmationId = confirmationId;
        ReferenceId = reference.ReferenceId;
        FileName = reference.FileName;
        ContentType = reference.ContentType;
        Description = reference.Description;
        Source = reference.Source;
        ExternalReference = reference.ExternalReference;
        RecordedByActorId = reference.RecordedByActorId;
        RecordedAt = reference.RecordedAt;
    }

    internal Guid Id { get; private set; }
    public TenantId TenantId { get; private set; }
    internal Guid PurchaseOrderConfirmationId { get; private set; }
    internal string ReferenceId { get; private set; } = string.Empty;
    internal string? FileName { get; private set; }
    internal string? ContentType { get; private set; }
    internal string? Description { get; private set; }
    internal string Source { get; private set; }
    internal string? ExternalReference { get; private set; }
    internal Guid RecordedByActorId { get; private set; }
    internal DateTimeOffset RecordedAt { get; private set; }
    internal byte[] Version { get; private set; } = [];
}

internal sealed class PurchaseOrderSupplierChangeEntity : ITenantOwned
{
    private PurchaseOrderSupplierChangeEntity() => Reason = string.Empty;

    internal PurchaseOrderSupplierChangeEntity(TenantId tenantId, Guid confirmationId, PurchaseOrderLineEntity line, PurchaseOrderConfirmationLineCommand command, Guid actorId, DateTimeOffset occurredAt)
    {
        Id = Guid.NewGuid();
        TenantId = tenantId;
        PurchaseOrderConfirmationId = confirmationId;
        PurchaseOrderLineId = line.Id;
        PreviousOrderedQuantity = line.OrderedQuantity;
        ProposedQuantity = command.ProposedQuantity;
        PreviousUnitPrice = line.UnitPrice;
        ProposedUnitPrice = command.ProposedUnitPrice;
        PreviousDeliveryDate = line.DeliveryDate;
        ProposedDeliveryDate = command.ProposedDeliveryDate;
        Status = PurchaseOrderSupplierChangeStatus.PendingApproval;
        Reason = command.ChangeReason ?? string.Empty;
        ActorId = actorId;
        ProposedAt = occurredAt;
    }

    internal Guid Id { get; private set; }
    public TenantId TenantId { get; private set; }
    internal Guid PurchaseOrderConfirmationId { get; private set; }
    internal Guid PurchaseOrderLineId { get; private set; }
    internal decimal PreviousOrderedQuantity { get; private set; }
    internal decimal? ProposedQuantity { get; private set; }
    internal decimal PreviousUnitPrice { get; private set; }
    internal decimal? ProposedUnitPrice { get; private set; }
    internal DateOnly? PreviousDeliveryDate { get; private set; }
    internal DateOnly? ProposedDeliveryDate { get; private set; }
    internal PurchaseOrderSupplierChangeStatus Status { get; private set; }
    internal string Reason { get; private set; }
    internal Guid ActorId { get; private set; }
    internal DateTimeOffset ProposedAt { get; private set; }
    internal DateTimeOffset? DecidedAt { get; private set; }
    internal string? DecisionReason { get; private set; }
    internal byte[] Version { get; private set; } = [];

    internal void Decide(PurchaseOrderSupplierChangeStatus status, DateTimeOffset at, string? reason)
    {
        Status = status;
        DecidedAt = at;
        DecisionReason = reason;
    }

    internal void TouchVersion() => Version = Guid.NewGuid().ToByteArray();
}

internal sealed class PurchaseOrderHistoryEntity : ITenantOwned
{
    private PurchaseOrderHistoryEntity() => CorrelationId = string.Empty;

    internal PurchaseOrderHistoryEntity(Guid id, TenantId tenantId, Guid purchaseOrderId, PurchaseOrderStatus fromStatus, PurchaseOrderStatus toStatus, PurchaseOrderHistoryAction action, Guid actorId, string? reason, string correlationId, string? policyId, int? policyVersion, string? stageKey, Guid? delegatedFromActorId, DateTimeOffset occurredAt)
    {
        Id = id;
        TenantId = tenantId;
        PurchaseOrderId = purchaseOrderId;
        FromStatus = fromStatus;
        ToStatus = toStatus;
        Action = action;
        ActorId = actorId;
        Reason = reason;
        CorrelationId = correlationId;
        PolicyId = policyId;
        PolicyVersion = policyVersion;
        StageKey = stageKey;
        DelegatedFromActorId = delegatedFromActorId;
        OccurredAt = occurredAt;
    }

    internal Guid Id { get; private set; }
    public TenantId TenantId { get; private set; }
    internal Guid PurchaseOrderId { get; private set; }
    internal PurchaseOrderStatus FromStatus { get; private set; }
    internal PurchaseOrderStatus ToStatus { get; private set; }
    internal PurchaseOrderHistoryAction Action { get; private set; }
    internal Guid ActorId { get; private set; }
    internal string? Reason { get; private set; }
    internal string CorrelationId { get; private set; }
    internal string? PolicyId { get; private set; }
    internal int? PolicyVersion { get; private set; }
    internal string? StageKey { get; private set; }
    internal Guid? DelegatedFromActorId { get; private set; }
    internal DateTimeOffset OccurredAt { get; private set; }
    internal byte[] Version { get; private set; } = [];
}

internal sealed class PurchaseOrderAuditEntity : ITenantOwned
{
    private PurchaseOrderAuditEntity()
    {
        OperationId = string.Empty;
        CorrelationId = string.Empty;
        AuthorizationPath = string.Empty;
        Decision = string.Empty;
    }

    internal PurchaseOrderAuditEntity(PurchaseOrderAuditEvidence evidence)
    {
        Id = evidence.EvidenceId;
        TenantId = new TenantId(evidence.TenantId);
        PurchaseOrderId = evidence.PurchaseOrderId;
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
    internal Guid PurchaseOrderId { get; private set; }
    internal DateTimeOffset OccurredAt { get; private set; }
    internal string OperationId { get; private set; }
    internal string CorrelationId { get; private set; }
    internal Guid ActorId { get; private set; }
    internal Guid SessionId { get; private set; }
    internal string AuthorizationPath { get; private set; }
    internal string Decision { get; private set; }
    internal string? Reason { get; private set; }
    internal PurchaseOrderStatus? BeforeStatus { get; private set; }
    internal PurchaseOrderStatus? AfterStatus { get; private set; }
    internal Guid CompanyId { get; private set; }
    internal Guid? BranchId { get; private set; }
    internal string? BeforeSummary { get; private set; }
    internal string? AfterSummary { get; private set; }
    internal string? IdempotencyKey { get; private set; }
    internal string? RequestFingerprint { get; private set; }
    internal byte[] Version { get; private set; } = [];
}

#pragma warning restore CS1591
