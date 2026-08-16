#pragma warning disable CS1591

using System.Globalization;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.Modules.BusinessParties;
using MiniErp.Contracts.Modules.Foundation;
using MiniErp.Contracts.Modules.Procurement;

namespace MiniErp.App.Modules.Procurement;

public sealed record SupplierQuotationOperationResult<T>(
    bool Succeeded,
    string Code,
    T? Value)
{
    public static SupplierQuotationOperationResult<T> Success(T value) =>
        new(true, "succeeded", value);

    public static SupplierQuotationOperationResult<T> Failure(string code) =>
        new(false, code, default);
}

public enum SupplierQuotationPersistenceOutcome
{
    Succeeded = 1,
    NotFound = 2,
    Conflict = 3,
    InvalidState = 4,
    Duplicate = 5,
    Failure = 6
}

public sealed record SupplierQuotationPersistenceResult<T>(
    SupplierQuotationPersistenceOutcome Outcome,
    string Code,
    T? Value)
{
    public bool Succeeded => Outcome == SupplierQuotationPersistenceOutcome.Succeeded;

    public static SupplierQuotationPersistenceResult<T> Success(T value) =>
        new(SupplierQuotationPersistenceOutcome.Succeeded, "persisted", value);

    public static SupplierQuotationPersistenceResult<T> Denied(
        SupplierQuotationPersistenceOutcome outcome,
        string code) => new(outcome, code, default);
}

public sealed record SupplierQuotationSupplierSnapshot(
    Guid Id,
    string Code,
    string Name);

public sealed record SupplierQuotationCurrencySnapshot(
    Guid Id,
    string Code,
    string Name);

public sealed record SupplierQuotationPaymentTermSnapshot(
    Guid Id,
    string Code,
    string Name,
    int Version);

public sealed record SupplierQuotationLineSnapshot(
    Guid Id,
    Guid PurchaseRequestLineId,
    Guid ProductId,
    string ProductSku,
    string ProductName,
    Guid UnitOfMeasureId,
    string UnitOfMeasureCode,
    decimal RequestedQuantity,
    decimal QuotedQuantity,
    decimal UnitPrice,
    decimal? DiscountAmount,
    decimal? DiscountPercentage,
    Guid? TaxId,
    string? TaxCode,
    string? TaxName,
    decimal? TaxRatePercentage,
    decimal? TaxAmount,
    string? TaxReference,
    DateOnly RequestedNeedByDate,
    DateOnly? OfferedDeliveryDate,
    string? OfferedDeliveryLeadTime,
    string? Notes,
    byte[]? Version = null);

public sealed record SupplierQuotationEvidenceReference(
    Guid Id,
    string ReferenceId,
    string? FileName,
    string? ContentType,
    string? Description,
    string Source,
    string? ExternalReference,
    Guid RecordedByActorId,
    DateTimeOffset RecordedAt,
    byte[]? Version = null);

public sealed record SupplierQuotationRecord(
    Guid Id,
    Guid TenantId,
    Guid PurchaseRequestId,
    PurchaseRequestScope Scope,
    Guid CreatedByActorId,
    SupplierQuotationSupplierSnapshot Supplier,
    SupplierQuotationStatus Status,
    string SupplierQuotationReference,
    DateOnly OfferDate,
    DateOnly? ValidUntil,
    SupplierQuotationCurrencySnapshot Currency,
    SupplierQuotationPaymentTermSnapshot? PaymentTerm,
    string? DeliveryTerms,
    DateOnly? OfferedDeliveryDate,
    string? OfferedDeliveryLeadTime,
    string? Notes,
    IReadOnlyList<SupplierQuotationLineSnapshot> Lines,
    IReadOnlyList<SupplierQuotationEvidenceReference> Evidence,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? SubmittedAt,
    byte[] Version,
    bool IsSelected = false);

public sealed record SupplierQuotationHistoryRecord(
    Guid EvidenceId,
    Guid SupplierQuotationId,
    DateTimeOffset OccurredAt,
    SupplierQuotationStatus FromStatus,
    SupplierQuotationStatus ToStatus,
    SupplierQuotationHistoryAction Action,
    Guid ActorId,
    string? Reason,
    string CorrelationId,
    string? PolicyId,
    int? PolicyVersion,
    string? StageKey,
    Guid? DelegatedFromActorId);

public sealed record SupplierQuotationAuditEvidence(
    Guid EvidenceId,
    Guid SupplierQuotationId,
    Guid PurchaseRequestId,
    DateTimeOffset OccurredAt,
    string OperationId,
    string CorrelationId,
    Guid TenantId,
    Guid ActorId,
    Guid SessionId,
    string AuthorizationPath,
    string Decision,
    string? Reason,
    SupplierQuotationStatus? BeforeStatus,
    SupplierQuotationStatus? AfterStatus,
    Guid CompanyId,
    Guid? BranchId,
    string? BeforeSummary,
    string? AfterSummary,
    string? IdempotencyKey);

public sealed record SupplierQuotationAuditRecord(
    Guid EvidenceId,
    Guid SupplierQuotationId,
    Guid PurchaseRequestId,
    DateTimeOffset OccurredAt,
    string OperationId,
    string CorrelationId,
    Guid TenantId,
    Guid ActorId,
    Guid SessionId,
    string AuthorizationPath,
    string Decision,
    string? Reason,
    SupplierQuotationStatus? BeforeStatus,
    SupplierQuotationStatus? AfterStatus,
    Guid CompanyId,
    Guid? BranchId,
    string? BeforeSummary,
    string? AfterSummary,
    string? IdempotencyKey);

public sealed record SupplierSourceDecisionRecord(
    Guid Id,
    Guid TenantId,
    Guid PurchaseRequestId,
    PurchaseRequestScope Scope,
    Guid SelectedQuotationId,
    SupplierQuotationSupplierSnapshot Supplier,
    string SupplierQuotationReference,
    Guid ActorId,
    DateTimeOffset SelectedAt,
    string Rationale,
    string? PolicyId,
    int? PolicyVersion,
    string? StageKey,
    string ComparisonSnapshotReference,
    string ComparisonSnapshotJson,
    byte[] Version);

public sealed record SupplierSourceDecisionHistoryRecord(
    Guid Id,
    Guid TenantId,
    Guid SourceDecisionId,
    Guid PurchaseRequestId,
    Guid? PreviousSelectedQuotationId,
    Guid SelectedQuotationId,
    Guid ActorId,
    DateTimeOffset SelectedAt,
    string Rationale,
    string? PolicyId,
    int? PolicyVersion,
    string? StageKey,
    string ComparisonSnapshotReference);

public sealed record SupplierQuotationCreateCommand(
    Guid Id,
    Guid PurchaseRequestId,
    PurchaseRequestScope Scope,
    Guid CreatedByActorId,
    SupplierQuotationSupplierSnapshot Supplier,
    string SupplierQuotationReference,
    DateOnly OfferDate,
    DateOnly? ValidUntil,
    SupplierQuotationCurrencySnapshot Currency,
    SupplierQuotationPaymentTermSnapshot? PaymentTerm,
    string? DeliveryTerms,
    DateOnly? OfferedDeliveryDate,
    string? OfferedDeliveryLeadTime,
    string? Notes,
    IReadOnlyList<SupplierQuotationLineSnapshot> Lines,
    IReadOnlyList<SupplierQuotationEvidenceReference> Evidence,
    DateTimeOffset OccurredAt,
    string? IdempotencyKey);

public sealed record SupplierQuotationEditCommand(
    Guid Id,
    Guid PurchaseRequestId,
    PurchaseRequestScope Scope,
    SupplierQuotationSupplierSnapshot Supplier,
    string SupplierQuotationReference,
    DateOnly OfferDate,
    DateOnly? ValidUntil,
    SupplierQuotationCurrencySnapshot Currency,
    SupplierQuotationPaymentTermSnapshot? PaymentTerm,
    string? DeliveryTerms,
    DateOnly? OfferedDeliveryDate,
    string? OfferedDeliveryLeadTime,
    string? Notes,
    IReadOnlyList<SupplierQuotationLineSnapshot> Lines,
    IReadOnlyList<SupplierQuotationEvidenceReference> Evidence,
    byte[] ExpectedVersion,
    DateTimeOffset OccurredAt,
    string? IdempotencyKey);

public sealed record SupplierQuotationActionCommand(
    Guid Id,
    byte[] ExpectedVersion,
    Guid ActorId,
    string? Reason,
    DateTimeOffset OccurredAt,
    string? IdempotencyKey);

public sealed record SupplierSourceDecisionCommand(
    Guid Id,
    Guid PurchaseRequestId,
    PurchaseRequestScope Scope,
    Guid SelectedQuotationId,
    Guid ActorId,
    DateTimeOffset SelectedAt,
    string Rationale,
    string? PolicyId,
    int? PolicyVersion,
    string? StageKey,
    string ComparisonSnapshotReference,
    string ComparisonSnapshotJson,
    byte[] ExpectedVersion,
    string? IdempotencyKey);

public interface ISupplierQuotationPersistence
{
    Task<IReadOnlyList<SupplierQuotationRecord>> ListAsync(
        TenantContext tenantContext,
        Guid purchaseRequestId,
        CancellationToken cancellationToken = default);

    Task<SupplierQuotationRecord?> FindAsync(
        TenantContext tenantContext,
        Guid quotationId,
        CancellationToken cancellationToken = default);

    Task<SupplierQuotationPersistenceResult<SupplierQuotationRecord>> CreateAsync(
        TenantContext tenantContext,
        SupplierQuotationCreateCommand command,
        SupplierQuotationAuditEvidence evidence,
        CancellationToken cancellationToken = default);

    Task<SupplierQuotationPersistenceResult<SupplierQuotationRecord>> EditAsync(
        TenantContext tenantContext,
        SupplierQuotationEditCommand command,
        SupplierQuotationAuditEvidence evidence,
        CancellationToken cancellationToken = default);

    Task<SupplierQuotationPersistenceResult<SupplierQuotationRecord>> SubmitAsync(
        TenantContext tenantContext,
        SupplierQuotationActionCommand command,
        SupplierQuotationAuditEvidence evidence,
        CancellationToken cancellationToken = default);

    Task<SupplierQuotationPersistenceResult<SupplierQuotationRecord>> WithdrawAsync(
        TenantContext tenantContext,
        SupplierQuotationActionCommand command,
        SupplierQuotationAuditEvidence evidence,
        CancellationToken cancellationToken = default);

    Task<SupplierQuotationPersistenceResult<SupplierQuotationRecord>> DisqualifyAsync(
        TenantContext tenantContext,
        SupplierQuotationActionCommand command,
        SupplierQuotationAuditEvidence evidence,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SupplierQuotationHistoryRecord>> ReadHistoryAsync(
        TenantContext tenantContext,
        Guid quotationId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SupplierQuotationAuditRecord>> ReadAuditAsync(
        TenantContext tenantContext,
        Guid quotationId,
        CancellationToken cancellationToken = default);

    Task<SupplierSourceDecisionRecord?> FindSourceDecisionAsync(
        TenantContext tenantContext,
        Guid purchaseRequestId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SupplierSourceDecisionHistoryRecord>> ReadSourceDecisionHistoryAsync(
        TenantContext tenantContext,
        Guid purchaseRequestId,
        CancellationToken cancellationToken = default);

    Task<SupplierQuotationPersistenceResult<SupplierSourceDecisionRecord>> RecordSourceDecisionAsync(
        TenantContext tenantContext,
        SupplierSourceDecisionCommand command,
        SupplierQuotationAuditEvidence evidence,
        CancellationToken cancellationToken = default);
}

public sealed class UnavailableSupplierQuotationPersistence : ISupplierQuotationPersistence
{
    private static Task<SupplierQuotationPersistenceResult<T>> Unavailable<T>() =>
        Task.FromResult(SupplierQuotationPersistenceResult<T>.Denied(
            SupplierQuotationPersistenceOutcome.Failure,
            "persistence_unavailable"));

    public Task<IReadOnlyList<SupplierQuotationRecord>> ListAsync(TenantContext tenantContext, Guid purchaseRequestId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<SupplierQuotationRecord>>([]);

    public Task<SupplierQuotationRecord?> FindAsync(TenantContext tenantContext, Guid quotationId, CancellationToken cancellationToken = default) =>
        Task.FromResult<SupplierQuotationRecord?>(null);

    public Task<SupplierQuotationPersistenceResult<SupplierQuotationRecord>> CreateAsync(TenantContext tenantContext, SupplierQuotationCreateCommand command, SupplierQuotationAuditEvidence evidence, CancellationToken cancellationToken = default) => Unavailable<SupplierQuotationRecord>();
    public Task<SupplierQuotationPersistenceResult<SupplierQuotationRecord>> EditAsync(TenantContext tenantContext, SupplierQuotationEditCommand command, SupplierQuotationAuditEvidence evidence, CancellationToken cancellationToken = default) => Unavailable<SupplierQuotationRecord>();
    public Task<SupplierQuotationPersistenceResult<SupplierQuotationRecord>> SubmitAsync(TenantContext tenantContext, SupplierQuotationActionCommand command, SupplierQuotationAuditEvidence evidence, CancellationToken cancellationToken = default) => Unavailable<SupplierQuotationRecord>();
    public Task<SupplierQuotationPersistenceResult<SupplierQuotationRecord>> WithdrawAsync(TenantContext tenantContext, SupplierQuotationActionCommand command, SupplierQuotationAuditEvidence evidence, CancellationToken cancellationToken = default) => Unavailable<SupplierQuotationRecord>();
    public Task<SupplierQuotationPersistenceResult<SupplierQuotationRecord>> DisqualifyAsync(TenantContext tenantContext, SupplierQuotationActionCommand command, SupplierQuotationAuditEvidence evidence, CancellationToken cancellationToken = default) => Unavailable<SupplierQuotationRecord>();

    public Task<IReadOnlyList<SupplierQuotationHistoryRecord>> ReadHistoryAsync(TenantContext tenantContext, Guid quotationId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<SupplierQuotationHistoryRecord>>([]);

    public Task<IReadOnlyList<SupplierQuotationAuditRecord>> ReadAuditAsync(TenantContext tenantContext, Guid quotationId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<SupplierQuotationAuditRecord>>([]);

    public Task<SupplierSourceDecisionRecord?> FindSourceDecisionAsync(TenantContext tenantContext, Guid purchaseRequestId, CancellationToken cancellationToken = default) =>
        Task.FromResult<SupplierSourceDecisionRecord?>(null);

    public Task<IReadOnlyList<SupplierSourceDecisionHistoryRecord>> ReadSourceDecisionHistoryAsync(TenantContext tenantContext, Guid purchaseRequestId, CancellationToken cancellationToken = default) =>
        Task.FromResult<IReadOnlyList<SupplierSourceDecisionHistoryRecord>>([]);

    public Task<SupplierQuotationPersistenceResult<SupplierSourceDecisionRecord>> RecordSourceDecisionAsync(TenantContext tenantContext, SupplierSourceDecisionCommand command, SupplierQuotationAuditEvidence evidence, CancellationToken cancellationToken = default) => Unavailable<SupplierSourceDecisionRecord>();
}

public static class SupplierQuotationValuePolicy
{
    public static bool TryNormalize(
        SupplierQuotationWriteRequest request,
        out string? quotationReference,
        out string? deliveryTerms,
        out string? offeredDeliveryLeadTime,
        out string? notes,
        out IReadOnlyList<SupplierQuotationLineWriteRequest> lines,
        out IReadOnlyList<SupplierQuotationEvidenceReferenceWriteRequest> evidence)
    {
        quotationReference = null;
        deliveryTerms = null;
        offeredDeliveryLeadTime = null;
        notes = null;
        lines = [];
        evidence = [];

        if (request.SupplierId == Guid.Empty
            || request.CurrencyId == Guid.Empty
            || request.OfferDate == DateOnly.MinValue
            || request.Lines is null
            || request.Lines.Count == 0
            || request.Lines.Count > 500
            || request.Evidence is { Count: > 50 })
        {
            return false;
        }

        if (!PurchaseRequestValuePolicy.TryText(request.SupplierQuotationReference, 256, allowEmpty: false, out quotationReference)
            || !PurchaseRequestValuePolicy.TryText(request.DeliveryTerms, 2048, allowEmpty: true, out deliveryTerms)
            || !PurchaseRequestValuePolicy.TryText(request.OfferedDeliveryLeadTime, 512, allowEmpty: true, out offeredDeliveryLeadTime)
            || !PurchaseRequestValuePolicy.TryText(request.Notes, 4096, allowEmpty: true, out notes)
            || request.ValidUntil is { } validUntil && validUntil < request.OfferDate
            || request.OfferedDeliveryDate is { } offeredDate && offeredDate < request.OfferDate)
        {
            return false;
        }

        var normalizedLines = new List<SupplierQuotationLineWriteRequest>(request.Lines.Count);
        var lineIds = new HashSet<Guid>();
        foreach (var line in request.Lines)
        {
            if (line.PurchaseRequestLineId == Guid.Empty
                || !lineIds.Add(line.PurchaseRequestLineId)
                || line.QuotedQuantity <= 0
                || line.UnitPrice < 0
                || line.DiscountAmount is < 0
                || line.DiscountPercentage is < 0 or > 100
                || line.DiscountAmount is not null && line.DiscountPercentage is not null
                || line.TaxRatePercentage is < 0 or > 100
                || line.TaxAmount is < 0
                || line.OfferedDeliveryDate is { } lineDeliveryDate && lineDeliveryDate < request.OfferDate
                || !PurchaseRequestValuePolicy.TryText(line.TaxReference, 256, allowEmpty: true, out var taxReference)
                || !PurchaseRequestValuePolicy.TryText(line.OfferedDeliveryLeadTime, 512, allowEmpty: true, out var lineLeadTime)
                || !PurchaseRequestValuePolicy.TryText(line.Notes, 2048, allowEmpty: true, out var lineNotes))
            {
                return false;
            }

            normalizedLines.Add(line with
            {
                TaxReference = taxReference,
                OfferedDeliveryLeadTime = lineLeadTime,
                Notes = lineNotes
            });
        }

        var normalizedEvidence = new List<SupplierQuotationEvidenceReferenceWriteRequest>();
        foreach (var item in request.Evidence ?? [])
        {
            if (!PurchaseRequestValuePolicy.TryText(item.ReferenceId, 256, allowEmpty: true, out var referenceId)
                || !PurchaseRequestValuePolicy.TryText(item.FileName, 255, allowEmpty: true, out var fileName)
                || !PurchaseRequestValuePolicy.TryText(item.ContentType, 128, allowEmpty: true, out var contentType)
                || !PurchaseRequestValuePolicy.TryText(item.Description, 1024, allowEmpty: true, out var description)
                || !PurchaseRequestValuePolicy.TryText(item.Source, 256, allowEmpty: true, out var source)
                || !PurchaseRequestValuePolicy.TryText(item.ExternalReference, 1024, allowEmpty: true, out var externalReference))
            {
                return false;
            }

            referenceId ??= externalReference;
            if (referenceId is null)
            {
                return false;
            }

            normalizedEvidence.Add(item with
            {
                ReferenceId = referenceId,
                FileName = fileName,
                ContentType = contentType,
                Description = description,
                Source = source ?? "buyer-recorded",
                ExternalReference = externalReference
            });
        }

        lines = normalizedLines;
        evidence = normalizedEvidence;
        return true;
    }

    public static bool TryRationale(string? value, out string rationale)
    {
        if (PurchaseRequestValuePolicy.TryText(value, 4096, allowEmpty: false, out var normalized)
            && normalized is not null)
        {
            rationale = normalized;
            return true;
        }

        rationale = string.Empty;
        return false;
    }

    public static decimal CommercialTotal(SupplierQuotationRecord record) =>
        record.Lines.Sum(CommercialTotal);

    public static decimal CommercialTotal(SupplierQuotationLineSnapshot line)
    {
        var gross = line.QuotedQuantity * line.UnitPrice;
        var discount = line.DiscountAmount
            ?? (line.DiscountPercentage is { } percentage ? gross * percentage / 100m : 0m);
        var net = gross - discount;
        return net + (line.TaxAmount ?? 0m);
    }

    public static string Amount(decimal value) =>
        value.ToString("0.########", CultureInfo.InvariantCulture);
}

public sealed record SupplierQuotationComparisonModel(
    Guid PurchaseRequestId,
    bool HasMixedCurrencies,
    bool DirectCurrencyComparisonAvailable,
    string ComparisonBasis,
    IReadOnlyList<SupplierQuotationCurrencyComparisonGroupModel> CurrencyGroups,
    IReadOnlyList<SupplierQuotationComparisonItemModel> Quotations,
    SupplierSourceDecisionRecord? CurrentSourceDecision);

public sealed record SupplierQuotationCurrencyComparisonGroupModel(
    Guid CurrencyId,
    string CurrencyCode,
    IReadOnlyList<Guid> SupplierQuotationIds,
    bool DirectlyComparableWithinGroup);

public sealed record SupplierQuotationComparisonItemModel(
    Guid SupplierQuotationId,
    SupplierQuotationSupplierSnapshot Supplier,
    SupplierQuotationStatus Status,
    string SupplierQuotationReference,
    DateOnly OfferDate,
    DateOnly? ValidUntil,
    SupplierQuotationCurrencySnapshot Currency,
    decimal CommercialTotal,
    int CoveredLineCount,
    int RequestedLineCount,
    bool HasEvidence,
    bool IsDirectlyComparableToAll,
    string? PaymentTermCode,
    string? DeliveryTerms,
    DateOnly? OfferedDeliveryDate,
    string? OfferedDeliveryLeadTime,
    IReadOnlyList<SupplierQuotationComparisonLineModel> Lines,
    IReadOnlyList<string> QualificationIssues);

public sealed record SupplierQuotationComparisonLineModel(
    Guid PurchaseRequestLineId,
    string ProductSku,
    string ProductName,
    decimal RequestedQuantity,
    decimal? QuotedQuantity,
    decimal? UnitPrice,
    decimal? DiscountAmount,
    decimal? DiscountPercentage,
    decimal? TaxRatePercentage,
    decimal? TaxAmount,
    DateOnly RequestedNeedByDate,
    DateOnly? OfferedDeliveryDate,
    bool IsCovered,
    string? QualificationIssue);

#pragma warning restore CS1591
