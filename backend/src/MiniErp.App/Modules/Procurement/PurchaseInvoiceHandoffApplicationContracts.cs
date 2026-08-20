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
    byte[] Version);

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

    public static decimal Total(PurchaseInvoiceHandoffRecord handoff) => handoff.Lines.Sum(line => line.LineAmount);
}

#pragma warning restore CS1591
