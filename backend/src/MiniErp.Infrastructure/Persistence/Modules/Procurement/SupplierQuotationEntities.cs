#pragma warning disable CS1591

using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.Modules.Procurement;
using MiniErp.Contracts.Modules.Procurement;

namespace MiniErp.Infrastructure.Persistence.Modules.Procurement;

internal sealed class SupplierQuotationEntity : ITenantOwned
{
    private SupplierQuotationEntity()
    {
        SupplierCode = string.Empty;
        SupplierName = string.Empty;
        SupplierQuotationReference = string.Empty;
        CurrencyCode = string.Empty;
        CurrencyName = string.Empty;
        Lines = [];
        Evidence = [];
    }

    internal SupplierQuotationEntity(
        SupplierQuotationCreateCommand command,
        TenantId tenantId)
    {
        Id = command.Id;
        TenantId = tenantId;
        PurchaseRequestId = command.PurchaseRequestId;
        CompanyId = command.Scope.CompanyId;
        BranchId = command.Scope.BranchId;
        CreatedByActorId = command.CreatedByActorId;
        SupplierId = command.Supplier.Id;
        SupplierCode = command.Supplier.Code;
        SupplierName = command.Supplier.Name;
        SupplierQuotationReference = command.SupplierQuotationReference;
        OfferDate = command.OfferDate;
        ValidUntil = command.ValidUntil;
        CurrencyId = command.Currency.Id;
        CurrencyCode = command.Currency.Code;
        CurrencyName = command.Currency.Name;
        PaymentTermId = command.PaymentTerm?.Id;
        PaymentTermCode = command.PaymentTerm?.Code;
        PaymentTermName = command.PaymentTerm?.Name;
        PaymentTermVersion = command.PaymentTerm?.Version;
        DeliveryTerms = command.DeliveryTerms;
        OfferedDeliveryDate = command.OfferedDeliveryDate;
        OfferedDeliveryLeadTime = command.OfferedDeliveryLeadTime;
        Notes = command.Notes;
        Status = SupplierQuotationStatus.Draft;
        CreatedAt = command.OccurredAt;
        UpdatedAt = command.OccurredAt;
        Lines = [];
        Evidence = [];
    }

    internal Guid Id { get; private set; }

    public TenantId TenantId { get; private set; }

    internal Guid PurchaseRequestId { get; private set; }

    internal Guid CompanyId { get; private set; }

    internal Guid? BranchId { get; private set; }

    internal Guid CreatedByActorId { get; private set; }

    internal Guid SupplierId { get; private set; }

    internal string SupplierCode { get; private set; }

    internal string SupplierName { get; private set; }

    internal SupplierQuotationStatus Status { get; private set; }

    internal string SupplierQuotationReference { get; private set; }

    internal DateOnly OfferDate { get; private set; }

    internal DateOnly? ValidUntil { get; private set; }

    internal Guid CurrencyId { get; private set; }

    internal string CurrencyCode { get; private set; }

    internal string CurrencyName { get; private set; }

    internal Guid? PaymentTermId { get; private set; }

    internal string? PaymentTermCode { get; private set; }

    internal string? PaymentTermName { get; private set; }

    internal int? PaymentTermVersion { get; private set; }

    internal string? DeliveryTerms { get; private set; }

    internal DateOnly? OfferedDeliveryDate { get; private set; }

    internal string? OfferedDeliveryLeadTime { get; private set; }

    internal string? Notes { get; private set; }

    internal DateTimeOffset CreatedAt { get; private set; }

    internal DateTimeOffset UpdatedAt { get; private set; }

    internal DateTimeOffset? SubmittedAt { get; private set; }

    internal byte[] Version { get; private set; } = [];

    internal ICollection<SupplierQuotationLineEntity> Lines { get; private set; }

    internal ICollection<SupplierQuotationEvidenceEntity> Evidence { get; private set; }

    internal void ReplaceDraft(
        SupplierQuotationEditCommand command)
    {
        SupplierId = command.Supplier.Id;
        SupplierCode = command.Supplier.Code;
        SupplierName = command.Supplier.Name;
        SupplierQuotationReference = command.SupplierQuotationReference;
        OfferDate = command.OfferDate;
        ValidUntil = command.ValidUntil;
        CurrencyId = command.Currency.Id;
        CurrencyCode = command.Currency.Code;
        CurrencyName = command.Currency.Name;
        PaymentTermId = command.PaymentTerm?.Id;
        PaymentTermCode = command.PaymentTerm?.Code;
        PaymentTermName = command.PaymentTerm?.Name;
        PaymentTermVersion = command.PaymentTerm?.Version;
        DeliveryTerms = command.DeliveryTerms;
        OfferedDeliveryDate = command.OfferedDeliveryDate;
        OfferedDeliveryLeadTime = command.OfferedDeliveryLeadTime;
        Notes = command.Notes;
        UpdatedAt = command.OccurredAt;
    }

    internal void SetStatus(
        SupplierQuotationStatus status,
        DateTimeOffset occurredAt)
    {
        Status = status;
        if (status == SupplierQuotationStatus.Submitted)
        {
            SubmittedAt = occurredAt;
        }

        UpdatedAt = occurredAt;
    }

    internal void TouchVersion() => Version = Guid.NewGuid().ToByteArray();
}

internal sealed class SupplierQuotationLineEntity : ITenantOwned
{
    private SupplierQuotationLineEntity()
    {
        ProductSku = string.Empty;
        ProductName = string.Empty;
        UnitOfMeasureCode = string.Empty;
    }

    internal SupplierQuotationLineEntity(
        TenantId tenantId,
        Guid quotationId,
        Guid purchaseRequestId,
        SupplierQuotationLineSnapshot snapshot)
    {
        Id = snapshot.Id;
        TenantId = tenantId;
        SupplierQuotationId = quotationId;
        PurchaseRequestId = purchaseRequestId;
        PurchaseRequestLineId = snapshot.PurchaseRequestLineId;
        ProductId = snapshot.ProductId;
        ProductSku = snapshot.ProductSku;
        ProductName = snapshot.ProductName;
        UnitOfMeasureId = snapshot.UnitOfMeasureId;
        UnitOfMeasureCode = snapshot.UnitOfMeasureCode;
        RequestedQuantity = snapshot.RequestedQuantity;
        QuotedQuantity = snapshot.QuotedQuantity;
        UnitPrice = snapshot.UnitPrice;
        DiscountAmount = snapshot.DiscountAmount;
        DiscountPercentage = snapshot.DiscountPercentage;
        TaxId = snapshot.TaxId;
        TaxCode = snapshot.TaxCode;
        TaxName = snapshot.TaxName;
        TaxRatePercentage = snapshot.TaxRatePercentage;
        TaxAmount = snapshot.TaxAmount;
        TaxReference = snapshot.TaxReference;
        RequestedNeedByDate = snapshot.RequestedNeedByDate;
        OfferedDeliveryDate = snapshot.OfferedDeliveryDate;
        OfferedDeliveryLeadTime = snapshot.OfferedDeliveryLeadTime;
        Notes = snapshot.Notes;
    }

    internal Guid Id { get; private set; }

    public TenantId TenantId { get; private set; }

    internal Guid SupplierQuotationId { get; private set; }

    internal Guid PurchaseRequestId { get; private set; }

    internal Guid PurchaseRequestLineId { get; private set; }

    internal Guid ProductId { get; private set; }

    internal string ProductSku { get; private set; }

    internal string ProductName { get; private set; }

    internal Guid UnitOfMeasureId { get; private set; }

    internal string UnitOfMeasureCode { get; private set; }

    internal decimal RequestedQuantity { get; private set; }

    internal decimal QuotedQuantity { get; private set; }

    internal decimal UnitPrice { get; private set; }

    internal decimal? DiscountAmount { get; private set; }

    internal decimal? DiscountPercentage { get; private set; }

    internal Guid? TaxId { get; private set; }

    internal string? TaxCode { get; private set; }

    internal string? TaxName { get; private set; }

    internal decimal? TaxRatePercentage { get; private set; }

    internal decimal? TaxAmount { get; private set; }

    internal string? TaxReference { get; private set; }

    internal DateOnly RequestedNeedByDate { get; private set; }

    internal DateOnly? OfferedDeliveryDate { get; private set; }

    internal string? OfferedDeliveryLeadTime { get; private set; }

    internal string? Notes { get; private set; }

    internal byte[] Version { get; private set; } = [];
}

internal sealed class SupplierQuotationEvidenceEntity : ITenantOwned
{
    private SupplierQuotationEvidenceEntity()
    {
        ReferenceId = string.Empty;
        Source = string.Empty;
    }

    internal SupplierQuotationEvidenceEntity(
        TenantId tenantId,
        Guid quotationId,
        SupplierQuotationEvidenceReference reference)
    {
        Id = reference.Id;
        TenantId = tenantId;
        SupplierQuotationId = quotationId;
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

    internal Guid SupplierQuotationId { get; private set; }

    internal string ReferenceId { get; private set; }

    internal string? FileName { get; private set; }

    internal string? ContentType { get; private set; }

    internal string? Description { get; private set; }

    internal string Source { get; private set; }

    internal string? ExternalReference { get; private set; }

    internal Guid RecordedByActorId { get; private set; }

    internal DateTimeOffset RecordedAt { get; private set; }

    internal byte[] Version { get; private set; } = [];
}

internal sealed class SupplierQuotationHistoryEntity : ITenantOwned
{
    private SupplierQuotationHistoryEntity()
    {
        CorrelationId = string.Empty;
    }

    internal SupplierQuotationHistoryEntity(
        Guid evidenceId,
        TenantId tenantId,
        Guid quotationId,
        SupplierQuotationStatus fromStatus,
        SupplierQuotationStatus toStatus,
        SupplierQuotationHistoryAction action,
        Guid actorId,
        string? reason,
        string correlationId,
        string? policyId,
        int? policyVersion,
        string? stageKey,
        Guid? delegatedFromActorId,
        DateTimeOffset occurredAt)
    {
        Id = evidenceId;
        TenantId = tenantId;
        SupplierQuotationId = quotationId;
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

    internal Guid SupplierQuotationId { get; private set; }

    internal SupplierQuotationStatus FromStatus { get; private set; }

    internal SupplierQuotationStatus ToStatus { get; private set; }

    internal SupplierQuotationHistoryAction Action { get; private set; }

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

internal sealed class SupplierQuotationAuditEntity : ITenantOwned
{
    private SupplierQuotationAuditEntity()
    {
        OperationId = string.Empty;
        CorrelationId = string.Empty;
        AuthorizationPath = string.Empty;
        Decision = string.Empty;
    }

    internal SupplierQuotationAuditEntity(SupplierQuotationAuditEvidence evidence)
    {
        Id = evidence.EvidenceId;
        TenantId = new TenantId(evidence.TenantId);
        SupplierQuotationId = evidence.SupplierQuotationId;
        PurchaseRequestId = evidence.PurchaseRequestId;
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
    }

    internal Guid Id { get; private set; }

    public TenantId TenantId { get; private set; }

    internal Guid SupplierQuotationId { get; private set; }

    internal Guid PurchaseRequestId { get; private set; }

    internal DateTimeOffset OccurredAt { get; private set; }

    internal string OperationId { get; private set; }

    internal string CorrelationId { get; private set; }

    internal Guid ActorId { get; private set; }

    internal Guid SessionId { get; private set; }

    internal string AuthorizationPath { get; private set; }

    internal string Decision { get; private set; }

    internal string? Reason { get; private set; }

    internal SupplierQuotationStatus? BeforeStatus { get; private set; }

    internal SupplierQuotationStatus? AfterStatus { get; private set; }

    internal Guid CompanyId { get; private set; }

    internal Guid? BranchId { get; private set; }

    internal string? BeforeSummary { get; private set; }

    internal string? AfterSummary { get; private set; }

    internal string? IdempotencyKey { get; private set; }

    internal byte[] Version { get; private set; } = [];
}

internal sealed class SupplierSourceDecisionEntity : ITenantOwned
{
    private SupplierSourceDecisionEntity()
    {
        SupplierCode = string.Empty;
        SupplierName = string.Empty;
        SupplierQuotationReference = string.Empty;
        Rationale = string.Empty;
        ComparisonSnapshotReference = string.Empty;
        ComparisonSnapshotJson = string.Empty;
    }

    internal SupplierSourceDecisionEntity(
        SupplierSourceDecisionCommand command,
        TenantId tenantId,
        SupplierQuotationRecord selectedQuotation)
    {
        Id = command.Id;
        TenantId = tenantId;
        PurchaseRequestId = command.PurchaseRequestId;
        CompanyId = command.Scope.CompanyId;
        BranchId = command.Scope.BranchId;
        SelectedQuotationId = command.SelectedQuotationId;
        SupplierId = selectedQuotation.Supplier.Id;
        SupplierCode = selectedQuotation.Supplier.Code;
        SupplierName = selectedQuotation.Supplier.Name;
        SupplierQuotationReference = selectedQuotation.SupplierQuotationReference;
        ActorId = command.ActorId;
        SelectedAt = command.SelectedAt;
        Rationale = command.Rationale;
        PolicyId = command.PolicyId;
        PolicyVersion = command.PolicyVersion;
        StageKey = command.StageKey;
        ComparisonSnapshotReference = command.ComparisonSnapshotReference;
        ComparisonSnapshotJson = command.ComparisonSnapshotJson;
    }

    internal Guid Id { get; private set; }

    public TenantId TenantId { get; private set; }

    internal Guid PurchaseRequestId { get; private set; }

    internal Guid CompanyId { get; private set; }

    internal Guid? BranchId { get; private set; }

    internal Guid SelectedQuotationId { get; private set; }

    internal Guid SupplierId { get; private set; }

    internal string SupplierCode { get; private set; }

    internal string SupplierName { get; private set; }

    internal string SupplierQuotationReference { get; private set; }

    internal Guid ActorId { get; private set; }

    internal DateTimeOffset SelectedAt { get; private set; }

    internal string Rationale { get; private set; }

    internal string? PolicyId { get; private set; }

    internal int? PolicyVersion { get; private set; }

    internal string? StageKey { get; private set; }

    internal string ComparisonSnapshotReference { get; private set; }

    internal string ComparisonSnapshotJson { get; private set; }

    internal byte[] Version { get; private set; } = [];

    internal void Replace(
        SupplierSourceDecisionCommand command,
        SupplierQuotationRecord selectedQuotation)
    {
        SelectedQuotationId = command.SelectedQuotationId;
        SupplierId = selectedQuotation.Supplier.Id;
        SupplierCode = selectedQuotation.Supplier.Code;
        SupplierName = selectedQuotation.Supplier.Name;
        SupplierQuotationReference = selectedQuotation.SupplierQuotationReference;
        ActorId = command.ActorId;
        SelectedAt = command.SelectedAt;
        Rationale = command.Rationale;
        PolicyId = command.PolicyId;
        PolicyVersion = command.PolicyVersion;
        StageKey = command.StageKey;
        ComparisonSnapshotReference = command.ComparisonSnapshotReference;
        ComparisonSnapshotJson = command.ComparisonSnapshotJson;
    }

    internal void TouchVersion() => Version = Guid.NewGuid().ToByteArray();
}

internal sealed class SupplierSourceDecisionHistoryEntity : ITenantOwned
{
    private SupplierSourceDecisionHistoryEntity()
    {
        Rationale = string.Empty;
        ComparisonSnapshotReference = string.Empty;
        ComparisonSnapshotJson = string.Empty;
    }

    internal SupplierSourceDecisionHistoryEntity(
        Guid id,
        TenantId tenantId,
        Guid sourceDecisionId,
        Guid purchaseRequestId,
        Guid? previousSelectedQuotationId,
        SupplierSourceDecisionCommand command)
    {
        Id = id;
        TenantId = tenantId;
        SourceDecisionId = sourceDecisionId;
        PurchaseRequestId = purchaseRequestId;
        PreviousSelectedQuotationId = previousSelectedQuotationId;
        SelectedQuotationId = command.SelectedQuotationId;
        ActorId = command.ActorId;
        SelectedAt = command.SelectedAt;
        Rationale = command.Rationale;
        PolicyId = command.PolicyId;
        PolicyVersion = command.PolicyVersion;
        StageKey = command.StageKey;
        ComparisonSnapshotReference = command.ComparisonSnapshotReference;
        ComparisonSnapshotJson = command.ComparisonSnapshotJson;
    }

    internal Guid Id { get; private set; }

    public TenantId TenantId { get; private set; }

    internal Guid SourceDecisionId { get; private set; }

    internal Guid PurchaseRequestId { get; private set; }

    internal Guid? PreviousSelectedQuotationId { get; private set; }

    internal Guid SelectedQuotationId { get; private set; }

    internal Guid ActorId { get; private set; }

    internal DateTimeOffset SelectedAt { get; private set; }

    internal string Rationale { get; private set; }

    internal string? PolicyId { get; private set; }

    internal int? PolicyVersion { get; private set; }

    internal string? StageKey { get; private set; }

    internal string ComparisonSnapshotReference { get; private set; }

    internal string ComparisonSnapshotJson { get; private set; }

    internal byte[] Version { get; private set; } = [];
}

#pragma warning restore CS1591
