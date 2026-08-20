#pragma warning disable CS1591

using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.Contracts.Modules.Procurement;

namespace MiniErp.App.Modules.Procurement;

public sealed record PurchaseInvoiceHandoffOperationResult<T>(bool Succeeded, string Code, T? Value)
{
    public static PurchaseInvoiceHandoffOperationResult<T> Success(T value) => new(true, "succeeded", value);

    public static PurchaseInvoiceHandoffOperationResult<T> Failure(string code) => new(false, code, default);
}

public sealed record PurchaseInvoiceHandoffEligibleLineRecord(
    Guid GoodsReceiptId,
    Guid GoodsReceiptLineId,
    Guid PurchaseOrderLineId,
    Guid ProductId,
    string ProductSku,
    string ProductName,
    Guid UnitOfMeasureId,
    string UnitOfMeasureCode,
    DateOnly ReceivedDate,
    decimal AcceptedQuantity,
    decimal AlreadyHandedOffQuantity,
    decimal RemainingHandoffQuantity,
    decimal UnitPrice,
    decimal? TaxRatePercentage,
    decimal? TaxAmount);

public sealed record PurchaseInvoiceHandoffEligibleSourceRecord(
    Guid PurchaseOrderId,
    PurchaseRequestScope Scope,
    Guid SupplierId,
    string SupplierCode,
    string SupplierName,
    Guid CurrencyId,
    string CurrencyCode,
    string CurrencyName,
    IReadOnlyList<PurchaseInvoiceHandoffEligibleLineRecord> Lines);

public sealed record PurchaseInvoiceHandoffSourceRecord(
    Guid Id,
    Guid GoodsReceiptId,
    Guid GoodsReceiptLineId,
    Guid PurchaseOrderLineId,
    decimal Quantity);

public sealed record PurchaseInvoiceDeclaredEvidenceAllocationRecord(
    Guid GoodsReceiptId,
    Guid GoodsReceiptLineId,
    decimal Quantity);

public sealed record PurchaseInvoiceDeclaredEvidenceLineRecord(
    Guid Id,
    Guid PurchaseOrderLineId,
    decimal Quantity,
    decimal UnitPrice,
    decimal? DiscountAmount,
    decimal? TaxRatePercentage,
    string? TaxCode,
    decimal? TaxAmount,
    decimal? NetAmount,
    decimal? GrossAmount,
    string? Description,
    IReadOnlyList<PurchaseInvoiceDeclaredEvidenceAllocationRecord> Allocations);

public sealed record PurchaseInvoiceDeclaredEvidenceRecord(
    Guid Id,
    int VersionNumber,
    string? SupplierInvoiceReference,
    DateOnly? SupplierInvoiceDate,
    string CurrencyCode,
    decimal? SubtotalAmount,
    decimal? DiscountAmount,
    decimal? TaxAmount,
    decimal? GrossAmount,
    DateTimeOffset RecordedAt,
    Guid RecordedByActorId,
    IReadOnlyList<PurchaseInvoiceDeclaredEvidenceLineRecord> Lines);

public sealed record PurchaseInvoiceHandoffLineRecord(
    Guid Id,
    Guid PurchaseOrderLineId,
    Guid ProductId,
    string ProductSku,
    string ProductName,
    string UnitOfMeasureCode,
    decimal HandoffQuantity,
    decimal UnitPrice,
    decimal? TaxRatePercentage,
    decimal? TaxAmount,
    decimal LineAmount);

public sealed record PurchaseInvoiceHandoffRecord(
    Guid Id,
    Guid TenantId,
    PurchaseRequestScope Scope,
    Guid PurchaseOrderId,
    Guid CreatedByActorId,
    PurchaseInvoiceHandoffStatus Status,
    Guid SupplierId,
    string SupplierCode,
    string SupplierName,
    string CurrencyCode,
    string? SupplierInvoiceReference,
    DateOnly? SupplierInvoiceDate,
    string? Notes,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? CancelledAt,
    string? CancellationReason,
    IReadOnlyList<PurchaseInvoiceHandoffLineRecord> Lines,
    IReadOnlyList<PurchaseInvoiceHandoffSourceRecord> Sources,
    byte[] Version,
    PurchaseInvoiceDeclaredEvidenceRecord? DeclaredEvidence = null);

public sealed record PurchaseInvoiceHandoffListRecord(
    Guid Id,
    Guid TenantId,
    PurchaseRequestScope Scope,
    Guid PurchaseOrderId,
    PurchaseInvoiceHandoffStatus Status,
    string SupplierCode,
    string SupplierName,
    string CurrencyCode,
    decimal Total,
    int LineCount,
    DateTimeOffset CreatedAt,
    byte[] Version);

public sealed record PurchaseInvoiceHandoffHistoryRecord(
    Guid EvidenceId,
    Guid PurchaseInvoiceHandoffId,
    DateTimeOffset OccurredAt,
    PurchaseInvoiceHandoffStatus FromStatus,
    PurchaseInvoiceHandoffStatus ToStatus,
    PurchaseInvoiceHandoffHistoryAction Action,
    Guid ActorId,
    string? Reason,
    string CorrelationId);

public sealed record PurchaseInvoiceHandoffAuditEvidence(
    Guid EvidenceId,
    Guid PurchaseInvoiceHandoffId,
    DateTimeOffset OccurredAt,
    string OperationId,
    string CorrelationId,
    Guid TenantId,
    Guid ActorId,
    Guid SessionId,
    string AuthorizationPath,
    string Decision,
    string? Reason,
    PurchaseInvoiceHandoffStatus? BeforeStatus,
    PurchaseInvoiceHandoffStatus? AfterStatus,
    Guid CompanyId,
    Guid? BranchId,
    string? BeforeSummary,
    string? AfterSummary,
    string? IdempotencyKey,
    string? RequestFingerprint);

public sealed record PurchaseInvoiceHandoffAuditRecord(
    Guid EvidenceId,
    Guid PurchaseInvoiceHandoffId,
    DateTimeOffset OccurredAt,
    string OperationId,
    string CorrelationId,
    Guid TenantId,
    Guid ActorId,
    Guid SessionId,
    string AuthorizationPath,
    string Decision,
    string? Reason,
    PurchaseInvoiceHandoffStatus? BeforeStatus,
    PurchaseInvoiceHandoffStatus? AfterStatus,
    Guid CompanyId,
    Guid? BranchId,
    string? BeforeSummary,
    string? AfterSummary,
    string? IdempotencyKey);

public sealed record PurchaseInvoiceHandoffSourceCommand(
    Guid GoodsReceiptId,
    Guid GoodsReceiptLineId,
    decimal Quantity);

public sealed record PurchaseInvoiceHandoffCreateCommand(
    Guid Id,
    PurchaseRequestScope Scope,
    Guid PurchaseOrderId,
    Guid CreatedByActorId,
    string? SupplierInvoiceReference,
    DateOnly? SupplierInvoiceDate,
    string? Notes,
    IReadOnlyList<PurchaseInvoiceHandoffSourceCommand> Sources,
    DateTimeOffset OccurredAt,
    string? IdempotencyKey,
    PurchaseInvoiceDeclaredEvidenceRequest? DeclaredEvidence = null);

public sealed record PurchaseInvoiceDeclaredEvidenceCaptureCommand(
    Guid PurchaseInvoiceHandoffId,
    byte[] ExpectedHandoffVersion,
    Guid ActorId,
    PurchaseInvoiceDeclaredEvidenceRequest Evidence,
    string? Reason,
    DateTimeOffset OccurredAt,
    string? IdempotencyKey);

public sealed record PurchaseInvoiceHandoffActionCommand(
    Guid Id,
    byte[] ExpectedVersion,
    Guid ActorId,
    string? Reason,
    DateTimeOffset OccurredAt,
    string? IdempotencyKey);

public sealed record PurchaseInvoiceHandoffReplayQuery(
    Guid ActorId,
    string OperationId,
    string? IdempotencyKey,
    Guid? PurchaseInvoiceHandoffId,
    string? RequestFingerprint);

public enum PurchaseInvoiceHandoffReplayOutcome
{
    NotFound = 1,
    Replay = 2,
    Conflict = 3
}

public sealed record PurchaseInvoiceHandoffReplayProbe(PurchaseInvoiceHandoffReplayOutcome Outcome, PurchaseInvoiceHandoffRecord? Record)
{
    public static readonly PurchaseInvoiceHandoffReplayProbe NotFound = new(PurchaseInvoiceHandoffReplayOutcome.NotFound, null);
    public static readonly PurchaseInvoiceHandoffReplayProbe Conflict = new(PurchaseInvoiceHandoffReplayOutcome.Conflict, null);
    public static PurchaseInvoiceHandoffReplayProbe ForReplay(PurchaseInvoiceHandoffRecord record) => new(PurchaseInvoiceHandoffReplayOutcome.Replay, record);
}

public sealed record PurchaseInvoiceHandoffPersistenceResult<T>(PurchaseInvoiceHandoffPersistenceOutcome Outcome, string Code, T? Value)
{
    public bool Succeeded => Outcome == PurchaseInvoiceHandoffPersistenceOutcome.Succeeded;
    public static PurchaseInvoiceHandoffPersistenceResult<T> Success(T value) => new(PurchaseInvoiceHandoffPersistenceOutcome.Succeeded, "persisted", value);
    public static PurchaseInvoiceHandoffPersistenceResult<T> Denied(PurchaseInvoiceHandoffPersistenceOutcome outcome, string code) => new(outcome, code, default);
}

public enum PurchaseInvoiceHandoffPersistenceOutcome
{
    Succeeded = 1,
    NotFound = 2,
    Conflict = 3,
    InvalidState = 4,
    Duplicate = 5,
    Failure = 6
}

public interface IPurchaseInvoiceHandoffPersistence
{
    Task<IReadOnlyList<PurchaseInvoiceHandoffListRecord>> ListAsync(TenantContext tenantContext, PurchaseInvoiceHandoffStatus? status, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PurchaseInvoiceHandoffEligibleSourceRecord>> ListEligibleSourcesAsync(TenantContext tenantContext, CancellationToken cancellationToken = default);
    Task<PurchaseInvoiceHandoffEligibleSourceRecord?> FindEligibleSourceAsync(TenantContext tenantContext, Guid purchaseOrderId, CancellationToken cancellationToken = default);
    Task<PurchaseInvoiceHandoffReplayProbe> ProbeReplayAsync(TenantContext tenantContext, PurchaseInvoiceHandoffReplayQuery query, CancellationToken cancellationToken = default);
    Task<PurchaseInvoiceHandoffRecord?> FindAsync(TenantContext tenantContext, Guid purchaseInvoiceHandoffId, CancellationToken cancellationToken = default);
    Task<PurchaseInvoiceHandoffPersistenceResult<PurchaseInvoiceHandoffRecord>> CreateAsync(TenantContext tenantContext, PurchaseInvoiceHandoffCreateCommand command, PurchaseInvoiceHandoffAuditEvidence evidence, CancellationToken cancellationToken = default);
    Task<PurchaseInvoiceHandoffPersistenceResult<PurchaseInvoiceHandoffRecord>> CaptureDeclaredEvidenceAsync(TenantContext tenantContext, PurchaseInvoiceDeclaredEvidenceCaptureCommand command, PurchaseInvoiceHandoffAuditEvidence evidence, CancellationToken cancellationToken = default);
    Task<PurchaseInvoiceHandoffPersistenceResult<PurchaseInvoiceHandoffRecord>> CancelAsync(TenantContext tenantContext, PurchaseInvoiceHandoffActionCommand command, PurchaseInvoiceHandoffAuditEvidence evidence, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PurchaseInvoiceHandoffHistoryRecord>> ReadHistoryAsync(TenantContext tenantContext, Guid purchaseInvoiceHandoffId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<PurchaseInvoiceHandoffAuditRecord>> ReadAuditAsync(TenantContext tenantContext, Guid purchaseInvoiceHandoffId, CancellationToken cancellationToken = default);
}

public sealed class UnavailablePurchaseInvoiceHandoffPersistence : IPurchaseInvoiceHandoffPersistence
{
    private static Task<PurchaseInvoiceHandoffPersistenceResult<T>> Unavailable<T>() =>
        Task.FromResult(PurchaseInvoiceHandoffPersistenceResult<T>.Denied(PurchaseInvoiceHandoffPersistenceOutcome.Failure, "persistence_unavailable"));

    public Task<IReadOnlyList<PurchaseInvoiceHandoffListRecord>> ListAsync(TenantContext tenantContext, PurchaseInvoiceHandoffStatus? status, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<PurchaseInvoiceHandoffListRecord>>([]);
    public Task<IReadOnlyList<PurchaseInvoiceHandoffEligibleSourceRecord>> ListEligibleSourcesAsync(TenantContext tenantContext, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<PurchaseInvoiceHandoffEligibleSourceRecord>>([]);
    public Task<PurchaseInvoiceHandoffEligibleSourceRecord?> FindEligibleSourceAsync(TenantContext tenantContext, Guid purchaseOrderId, CancellationToken cancellationToken = default) => Task.FromResult<PurchaseInvoiceHandoffEligibleSourceRecord?>(null);
    public Task<PurchaseInvoiceHandoffReplayProbe> ProbeReplayAsync(TenantContext tenantContext, PurchaseInvoiceHandoffReplayQuery query, CancellationToken cancellationToken = default) => Task.FromResult(PurchaseInvoiceHandoffReplayProbe.NotFound);
    public Task<PurchaseInvoiceHandoffRecord?> FindAsync(TenantContext tenantContext, Guid purchaseInvoiceHandoffId, CancellationToken cancellationToken = default) => Task.FromResult<PurchaseInvoiceHandoffRecord?>(null);
    public Task<PurchaseInvoiceHandoffPersistenceResult<PurchaseInvoiceHandoffRecord>> CreateAsync(TenantContext tenantContext, PurchaseInvoiceHandoffCreateCommand command, PurchaseInvoiceHandoffAuditEvidence evidence, CancellationToken cancellationToken = default) => Unavailable<PurchaseInvoiceHandoffRecord>();
    public Task<PurchaseInvoiceHandoffPersistenceResult<PurchaseInvoiceHandoffRecord>> CaptureDeclaredEvidenceAsync(TenantContext tenantContext, PurchaseInvoiceDeclaredEvidenceCaptureCommand command, PurchaseInvoiceHandoffAuditEvidence evidence, CancellationToken cancellationToken = default) => Unavailable<PurchaseInvoiceHandoffRecord>();
    public Task<PurchaseInvoiceHandoffPersistenceResult<PurchaseInvoiceHandoffRecord>> CancelAsync(TenantContext tenantContext, PurchaseInvoiceHandoffActionCommand command, PurchaseInvoiceHandoffAuditEvidence evidence, CancellationToken cancellationToken = default) => Unavailable<PurchaseInvoiceHandoffRecord>();
    public Task<IReadOnlyList<PurchaseInvoiceHandoffHistoryRecord>> ReadHistoryAsync(TenantContext tenantContext, Guid purchaseInvoiceHandoffId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<PurchaseInvoiceHandoffHistoryRecord>>([]);
    public Task<IReadOnlyList<PurchaseInvoiceHandoffAuditRecord>> ReadAuditAsync(TenantContext tenantContext, Guid purchaseInvoiceHandoffId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<PurchaseInvoiceHandoffAuditRecord>>([]);
}

public static class PurchaseInvoiceHandoffValuePolicy
{
    public static bool TryText(string? value, int maxLength, bool allowEmpty, out string? normalized)
    {
        normalized = string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        return (allowEmpty || !string.IsNullOrWhiteSpace(normalized)) && (normalized?.Length ?? 0) <= maxLength;
    }

    public static bool TryReason(string? reason, out string normalized)
    {
        normalized = reason?.Trim() ?? string.Empty;
        return normalized.Length is > 0 and <= 4096;
    }

    public static bool TryNormalizeCreate(
        PurchaseInvoiceHandoffCreateRequest request,
        out string? supplierInvoiceReference,
        out string? notes,
        out IReadOnlyList<PurchaseInvoiceHandoffSourceRequest> sources)
    {
        supplierInvoiceReference = null;
        notes = null;
        sources = [];
        if (request is null
            || request.PurchaseOrderId == Guid.Empty
            || request.Sources is null
            || request.Sources.Count == 0
            || request.Sources.Count > 1000
            || !TryText(request.SupplierInvoiceReference, 256, true, out supplierInvoiceReference)
            || !TryText(request.Notes, 4096, true, out notes))
        {
            return false;
        }

        var seen = new HashSet<(Guid, Guid)>();
        var normalized = new List<PurchaseInvoiceHandoffSourceRequest>(request.Sources.Count);
        foreach (var source in request.Sources)
        {
            if (source.GoodsReceiptId == Guid.Empty
                || source.GoodsReceiptLineId == Guid.Empty
                || source.Quantity <= 0
                || !seen.Add((source.GoodsReceiptId, source.GoodsReceiptLineId)))
            {
                return false;
            }

            normalized.Add(source);
        }

        sources = normalized;
        return true;
    }

    public static bool TryNormalizeDeclaredEvidence(
        PurchaseInvoiceDeclaredEvidenceRequest? request,
        out PurchaseInvoiceDeclaredEvidenceRequest? normalized)
    {
        normalized = null;
        if (request is null)
        {
            return true;
        }

        if (!TryText(request.SupplierInvoiceReference, 256, true, out var reference)
            || !TryText(request.CurrencyCode, 16, false, out var currency)
            || request.Lines is null
            || request.Lines.Count is < 1 or > 1000
            || !AmountsAreValid(request.SubtotalAmount, request.DiscountAmount, request.TaxAmount, request.GrossAmount))
        {
            return false;
        }

        var lines = new List<PurchaseInvoiceDeclaredEvidenceLineRequest>(request.Lines.Count);
        foreach (var line in request.Lines)
        {
            if (line.PurchaseOrderLineId == Guid.Empty
                || line.Quantity <= 0
                || line.UnitPrice < 0
                || !AmountsAreValid(line.DiscountAmount, line.TaxAmount, line.NetAmount, line.GrossAmount)
                || line.TaxRatePercentage is < 0 or > 100
                || !TryText(line.TaxCode, 128, true, out var taxCode)
                || !TryText(line.Description, 512, true, out var description)
                || line.Allocations is null
                || line.Allocations.Count == 0
                || line.Allocations.Count > 1000)
            {
                return false;
            }

            var allocations = new List<PurchaseInvoiceDeclaredEvidenceAllocationRequest>(line.Allocations.Count);
            var allocationTotal = 0m;
            var seen = new HashSet<(Guid ReceiptId, Guid LineId)>();
            foreach (var allocation in line.Allocations)
            {
                if (allocation.GoodsReceiptId == Guid.Empty
                    || allocation.GoodsReceiptLineId == Guid.Empty
                    || allocation.Quantity <= 0
                    || !seen.Add((allocation.GoodsReceiptId, allocation.GoodsReceiptLineId)))
                {
                    return false;
                }

                allocationTotal += allocation.Quantity;
                allocations.Add(allocation);
            }

            // Supplier-declared quantity is independent invoice evidence. The
            // allocations identify the physically supported portion and may
            // be smaller than the supplier declaration; the matcher records
            // the difference as a quantity variance. Allocating more than the
            // declared quantity would be structurally impossible evidence.
            if (allocationTotal > line.Quantity)
            {
                return false;
            }

            lines.Add(line with
            {
                TaxCode = taxCode,
                Description = description,
                Allocations = allocations
            });
        }

        normalized = request with
        {
            SupplierInvoiceReference = reference,
            CurrencyCode = currency,
            Lines = lines
        };
        return true;
    }

    private static bool AmountsAreValid(params decimal?[] amounts) => amounts.All(item => item is null or >= 0);

    public static decimal Total(PurchaseInvoiceHandoffRecord handoff) => handoff.Lines.Sum(line => line.LineAmount);
}

#pragma warning restore CS1591
