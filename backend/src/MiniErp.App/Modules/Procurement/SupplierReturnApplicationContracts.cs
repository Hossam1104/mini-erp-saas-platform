#pragma warning disable CS1591

using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.Contracts.Modules.Procurement;

namespace MiniErp.App.Modules.Procurement;

public sealed record SupplierReturnOperationResult<T>(bool Succeeded, string Code, T? Value)
{
    public static SupplierReturnOperationResult<T> Success(T value) => new(true, "succeeded", value);
    public static SupplierReturnOperationResult<T> Failure(string code) => new(false, code, default);
}

public sealed record SupplierReturnEligibleLineRecord(
    Guid GoodsReceiptId,
    Guid GoodsReceiptLineId,
    Guid PurchaseOrderId,
    Guid PurchaseOrderLineId,
    Guid WarehouseId,
    Guid ProductId,
    string ProductSku,
    string ProductName,
    string UnitOfMeasureCode,
    decimal AcceptedQuantity,
    decimal AlreadyReturnedQuantity,
    decimal EligibleReturnQuantity,
    DateOnly ReceivedDate);

public sealed record SupplierReturnEligibleSourceRecord(
    Guid GoodsReceiptId,
    Guid PurchaseOrderId,
    Guid? SupplierConfirmationId,
    PurchaseRequestScope Scope,
    Guid WarehouseId,
    Guid SupplierId,
    string SupplierCode,
    string SupplierName,
    string CurrencyCode,
    IReadOnlyList<SupplierReturnEligibleLineRecord> Lines);

public sealed record SupplierReturnEvidenceRecord(
    Guid Id,
    string ReferenceId,
    string? FileName,
    string? ContentType,
    string? Description,
    string Source,
    DateTimeOffset RecordedAt);

public sealed record SupplierReturnLineRecord(
    Guid Id,
    Guid GoodsReceiptLineId,
    Guid PurchaseOrderLineId,
    Guid ProductId,
    string ProductSku,
    string ProductName,
    string UnitOfMeasureCode,
    decimal AcceptedQuantityAtReturn,
    decimal ReturnQuantity,
    decimal? EligibleQuantityAfter,
    string? Notes);

public sealed record SupplierReturnRecord(
    Guid Id,
    Guid TenantId,
    PurchaseRequestScope Scope,
    Guid GoodsReceiptId,
    Guid PurchaseOrderId,
    Guid? SupplierConfirmationId,
    Guid WarehouseId,
    Guid SupplierId,
    string SupplierCode,
    string SupplierName,
    string CurrencyCode,
    SupplierReturnStatus Status,
    SupplierReturnReasonCode ReasonCode,
    SupplierReturnCondition Condition,
    SupplierReturnCommercialOutcome CommercialOutcome,
    string? ReasonDetail,
    string? Notes,
    DateOnly ReturnDate,
    DateTimeOffset CreatedAt,
    DateTimeOffset UpdatedAt,
    DateTimeOffset? CancelledAt,
    DateTimeOffset? ReversedAt,
    Guid? CorrectionOfId,
    Guid? InventoryHandoffId,
    string? InventoryHandoffReference,
    string? FinanceReference,
    string? FinanceCurrencyCode,
    decimal? FinanceAmount,
    IReadOnlyList<SupplierReturnLineRecord> Lines,
    IReadOnlyList<SupplierReturnEvidenceRecord> Evidence,
    byte[] Version);

public sealed record SupplierReturnListRecord(
    Guid Id,
    Guid TenantId,
    PurchaseRequestScope Scope,
    Guid GoodsReceiptId,
    Guid PurchaseOrderId,
    Guid WarehouseId,
    string SupplierCode,
    string SupplierName,
    SupplierReturnStatus Status,
    SupplierReturnReasonCode ReasonCode,
    SupplierReturnCommercialOutcome CommercialOutcome,
    decimal TotalReturnQuantity,
    DateOnly ReturnDate,
    DateTimeOffset CreatedAt,
    byte[] Version);

public sealed record SupplierReturnHistoryRecord(
    Guid EvidenceId,
    Guid SupplierReturnId,
    DateTimeOffset OccurredAt,
    SupplierReturnStatus FromStatus,
    SupplierReturnStatus ToStatus,
    SupplierReturnHistoryAction Action,
    Guid ActorId,
    string? Reason,
    string CorrelationId);

public sealed record SupplierReturnAuditEvidence(
    Guid EvidenceId,
    Guid SupplierReturnId,
    DateTimeOffset OccurredAt,
    string OperationId,
    string CorrelationId,
    Guid TenantId,
    Guid ActorId,
    Guid SessionId,
    string AuthorizationPath,
    string Decision,
    string? Reason,
    SupplierReturnStatus? BeforeStatus,
    SupplierReturnStatus? AfterStatus,
    Guid CompanyId,
    Guid? BranchId,
    string? BeforeSummary,
    string? AfterSummary,
    string? IdempotencyKey,
    string? RequestFingerprint);

public sealed record SupplierReturnLineCreateCommand(
    Guid Id,
    Guid GoodsReceiptLineId,
    decimal ReturnQuantity,
    string? Notes);

public sealed record SupplierReturnEvidenceCreateCommand(
    Guid Id,
    string ReferenceId,
    string? FileName,
    string? ContentType,
    string? Description,
    string Source);

public sealed record SupplierReturnCreateCommand(
    Guid Id,
    PurchaseRequestScope Scope,
    Guid GoodsReceiptId,
    DateOnly ReturnDate,
    SupplierReturnReasonCode ReasonCode,
    SupplierReturnCondition Condition,
    SupplierReturnCommercialOutcome CommercialOutcome,
    string? ReasonDetail,
    string? Notes,
    IReadOnlyList<SupplierReturnLineCreateCommand> Lines,
    IReadOnlyList<SupplierReturnEvidenceCreateCommand> Evidence,
    Guid ActorId,
    DateTimeOffset OccurredAt,
    string? IdempotencyKey,
    Guid? CorrectionOfId = null);

public enum SupplierReturnMutationAction
{
    Submit = 1,
    Approve = 2,
    Reject = 3,
    Cancel = 4,
    RecordInventoryHandoff = 5,
    RecordFinanceReference = 6,
    Reverse = 7
}

public sealed record SupplierReturnActionCommand(
    Guid Id,
    byte[] ExpectedVersion,
    SupplierReturnMutationAction Action,
    Guid ActorId,
    string? Reason,
    DateTimeOffset OccurredAt,
    string? IdempotencyKey,
    string? HandoffReference = null,
    string? FinanceReference = null,
    string? FinanceCurrencyCode = null,
    decimal? FinanceAmount = null,
    string? Notes = null);

public sealed record SupplierReturnReplayQuery(
    Guid ActorId,
    string OperationId,
    string? IdempotencyKey,
    Guid? SupplierReturnId,
    string? RequestFingerprint);

public enum SupplierReturnReplayOutcome
{
    NotFound = 1,
    Replay = 2,
    Conflict = 3
}

public sealed record SupplierReturnReplayProbe(SupplierReturnReplayOutcome Outcome, SupplierReturnRecord? Record)
{
    public static readonly SupplierReturnReplayProbe NotFound = new(SupplierReturnReplayOutcome.NotFound, null);
    public static readonly SupplierReturnReplayProbe Conflict = new(SupplierReturnReplayOutcome.Conflict, null);
    public static SupplierReturnReplayProbe ForReplay(SupplierReturnRecord record) => new(SupplierReturnReplayOutcome.Replay, record);
}

public sealed record SupplierReturnPersistenceResult<T>(SupplierReturnPersistenceOutcome Outcome, string Code, T? Value)
{
    public bool Succeeded => Outcome == SupplierReturnPersistenceOutcome.Succeeded;
    public static SupplierReturnPersistenceResult<T> Success(T value) => new(SupplierReturnPersistenceOutcome.Succeeded, "persisted", value);
    public static SupplierReturnPersistenceResult<T> Denied(SupplierReturnPersistenceOutcome outcome, string code) => new(outcome, code, default);
}

public enum SupplierReturnPersistenceOutcome
{
    Succeeded = 1,
    NotFound = 2,
    Conflict = 3,
    InvalidState = 4,
    Duplicate = 5,
    Failure = 6
}

public interface ISupplierReturnPersistence
{
    Task<IReadOnlyList<SupplierReturnListRecord>> ListAsync(TenantContext tenantContext, SupplierReturnStatus? status, Guid? supplierId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SupplierReturnEligibleSourceRecord>> ListEligibleSourcesAsync(TenantContext tenantContext, CancellationToken cancellationToken = default);
    Task<SupplierReturnEligibleSourceRecord?> FindEligibleSourceAsync(TenantContext tenantContext, Guid goodsReceiptId, CancellationToken cancellationToken = default);
    Task<SupplierReturnReplayProbe> ProbeReplayAsync(TenantContext tenantContext, SupplierReturnReplayQuery query, CancellationToken cancellationToken = default);
    Task<SupplierReturnRecord?> FindAsync(TenantContext tenantContext, Guid supplierReturnId, CancellationToken cancellationToken = default);
    Task<SupplierReturnPersistenceResult<SupplierReturnRecord>> CreateAsync(TenantContext tenantContext, SupplierReturnCreateCommand command, SupplierReturnAuditEvidence evidence, CancellationToken cancellationToken = default);
    Task<SupplierReturnPersistenceResult<SupplierReturnRecord>> ActionAsync(TenantContext tenantContext, SupplierReturnActionCommand command, SupplierReturnAuditEvidence evidence, CancellationToken cancellationToken = default);
    Task<SupplierReturnPersistenceResult<SupplierReturnRecord>> CorrectAsync(TenantContext tenantContext, SupplierReturnCreateCommand command, byte[] expectedVersion, SupplierReturnAuditEvidence evidence, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SupplierReturnHistoryRecord>> ReadHistoryAsync(TenantContext tenantContext, Guid supplierReturnId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SupplierReturnAuditEvidence>> ReadAuditAsync(TenantContext tenantContext, Guid supplierReturnId, CancellationToken cancellationToken = default);
}

public enum SupplierReturnInventoryEffectVerification
{
    ActiveEffectExists = 1,
    NoActiveEffect = 2,
    Unavailable = 3
}

public interface ISupplierReturnInventoryEffectReader
{
    Task<SupplierReturnInventoryEffectVerification> VerifyAsync(TenantContext tenantContext, Guid supplierReturnId, CancellationToken cancellationToken = default);
}

public sealed class UnavailableSupplierReturnInventoryEffectReader : ISupplierReturnInventoryEffectReader
{
    public Task<SupplierReturnInventoryEffectVerification> VerifyAsync(TenantContext tenantContext, Guid supplierReturnId, CancellationToken cancellationToken = default) =>
        Task.FromResult(SupplierReturnInventoryEffectVerification.Unavailable);
}

public interface ISupplierReturnPhysicalEffectGate
{
    Task<IDisposable> AcquireAsync(Guid supplierReturnId, CancellationToken cancellationToken = default);
}

public sealed class SupplierReturnPhysicalEffectGate : ISupplierReturnPhysicalEffectGate
{
    private readonly System.Collections.Concurrent.ConcurrentDictionary<Guid, System.Threading.SemaphoreSlim> locks = new();

    public async Task<IDisposable> AcquireAsync(Guid supplierReturnId, CancellationToken cancellationToken = default)
    {
        if (supplierReturnId == Guid.Empty)
        {
            throw new ArgumentException("A Supplier Return identifier is required.", nameof(supplierReturnId));
        }

        var gate = locks.GetOrAdd(supplierReturnId, static _ => new System.Threading.SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        return new Lease(gate);
    }

    private sealed class Lease(System.Threading.SemaphoreSlim gate) : IDisposable
    {
        private System.Threading.SemaphoreSlim? heldGate = gate;

        public void Dispose() => Interlocked.Exchange(ref heldGate, null)?.Release();
    }
}

public sealed class UnavailableSupplierReturnPersistence : ISupplierReturnPersistence
{
    private static Task<SupplierReturnPersistenceResult<T>> Unavailable<T>() =>
        Task.FromResult(SupplierReturnPersistenceResult<T>.Denied(SupplierReturnPersistenceOutcome.Failure, "persistence_unavailable"));

    public Task<IReadOnlyList<SupplierReturnListRecord>> ListAsync(TenantContext tenantContext, SupplierReturnStatus? status, Guid? supplierId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<SupplierReturnListRecord>>([]);
    public Task<IReadOnlyList<SupplierReturnEligibleSourceRecord>> ListEligibleSourcesAsync(TenantContext tenantContext, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<SupplierReturnEligibleSourceRecord>>([]);
    public Task<SupplierReturnEligibleSourceRecord?> FindEligibleSourceAsync(TenantContext tenantContext, Guid goodsReceiptId, CancellationToken cancellationToken = default) => Task.FromResult<SupplierReturnEligibleSourceRecord?>(null);
    public Task<SupplierReturnReplayProbe> ProbeReplayAsync(TenantContext tenantContext, SupplierReturnReplayQuery query, CancellationToken cancellationToken = default) => Task.FromResult(SupplierReturnReplayProbe.NotFound);
    public Task<SupplierReturnRecord?> FindAsync(TenantContext tenantContext, Guid supplierReturnId, CancellationToken cancellationToken = default) => Task.FromResult<SupplierReturnRecord?>(null);
    public Task<SupplierReturnPersistenceResult<SupplierReturnRecord>> CreateAsync(TenantContext tenantContext, SupplierReturnCreateCommand command, SupplierReturnAuditEvidence evidence, CancellationToken cancellationToken = default) => Unavailable<SupplierReturnRecord>();
    public Task<SupplierReturnPersistenceResult<SupplierReturnRecord>> ActionAsync(TenantContext tenantContext, SupplierReturnActionCommand command, SupplierReturnAuditEvidence evidence, CancellationToken cancellationToken = default) => Unavailable<SupplierReturnRecord>();
    public Task<SupplierReturnPersistenceResult<SupplierReturnRecord>> CorrectAsync(TenantContext tenantContext, SupplierReturnCreateCommand command, byte[] expectedVersion, SupplierReturnAuditEvidence evidence, CancellationToken cancellationToken = default) => Unavailable<SupplierReturnRecord>();
    public Task<IReadOnlyList<SupplierReturnHistoryRecord>> ReadHistoryAsync(TenantContext tenantContext, Guid supplierReturnId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<SupplierReturnHistoryRecord>>([]);
    public Task<IReadOnlyList<SupplierReturnAuditEvidence>> ReadAuditAsync(TenantContext tenantContext, Guid supplierReturnId, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<SupplierReturnAuditEvidence>>([]);
}

public static class SupplierReturnValuePolicy
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
        SupplierReturnCreateRequest request,
        out DateOnly returnDate,
        out string? reasonDetail,
        out string? notes,
        out IReadOnlyList<SupplierReturnLineCreateRequest> lines,
        out IReadOnlyList<SupplierReturnEvidenceReferenceWriteRequest> evidence)
    {
        returnDate = default;
        reasonDetail = null;
        notes = null;
        lines = [];
        evidence = [];
        if (request is null
            || request.GoodsReceiptId == Guid.Empty
            || request.ReturnDate == default
            || request.Lines is null
            || request.Lines.Count == 0
            || request.Lines.Count > 500
            || !Enum.IsDefined(request.ReasonCode)
            || !Enum.IsDefined(request.Condition)
            || !Enum.IsDefined(request.CommercialOutcome)
            || !TryText(request.ReasonDetail, 4096, true, out reasonDetail)
            || !TryText(request.Notes, 4096, true, out notes))
        {
            return false;
        }

        var seenLines = new HashSet<Guid>();
        var normalizedLines = new List<SupplierReturnLineCreateRequest>(request.Lines.Count);
        foreach (var line in request.Lines)
        {
            if (line is null || line.GoodsReceiptLineId == Guid.Empty || line.ReturnQuantity <= 0m || line.ReturnQuantity > 999999999999m || !seenLines.Add(line.GoodsReceiptLineId) || !TryText(line.Notes, 2048, true, out var lineNotes))
            {
                return false;
            }

            normalizedLines.Add(line with { Notes = lineNotes });
        }

        var normalizedEvidence = new List<SupplierReturnEvidenceReferenceWriteRequest>();
        foreach (var item in request.Evidence ?? [])
        {
            if (item is null
                || !TryText(item.ReferenceId, 256, false, out var referenceId)
                || !TryText(item.FileName, 255, true, out var fileName)
                || !TryText(item.ContentType, 128, true, out var contentType)
                || !TryText(item.Description, 1024, true, out var description)
                || !TryText(item.Source, 256, true, out var source))
            {
                return false;
            }

            normalizedEvidence.Add(item with
            {
                ReferenceId = referenceId,
                FileName = fileName,
                ContentType = contentType,
                Description = description,
                Source = source ?? "private-file-reference"
            });
        }

        if (normalizedEvidence.Count > 50)
        {
            return false;
        }

        returnDate = request.ReturnDate;
        lines = normalizedLines;
        evidence = normalizedEvidence;
        return true;
    }

    public static bool TryNormalizeHandoff(SupplierReturnInventoryHandoffRequest request, out string reference, out string? notes)
    {
        reference = string.Empty;
        notes = null;
        if (request is null || !TryText(request.HandoffReference, 256, false, out var normalizedReference) || !TryText(request.Notes, 2048, true, out notes))
        {
            return false;
        }

        reference = normalizedReference!;
        return true;
    }

    public static bool TryNormalizeFinance(SupplierReturnFinanceReferenceRequest request, out string reference, out string? currencyCode, out decimal? amount, out string? notes)
    {
        reference = string.Empty;
        currencyCode = null;
        amount = null;
        notes = null;
        if (request is null || !TryText(request.FinanceReference, 256, false, out var normalizedReference) || !TryText(request.CurrencyCode, 16, true, out currencyCode) || !TryText(request.Notes, 2048, true, out notes))
        {
            return false;
        }

        reference = normalizedReference!;

        if (request.Amount is < 0m or > 999999999999m)
        {
            return false;
        }

        amount = request.Amount;
        currencyCode = currencyCode?.ToUpperInvariant();
        return true;
    }
}

#pragma warning restore CS1591
