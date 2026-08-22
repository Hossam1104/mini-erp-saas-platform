#pragma warning disable CS1591

using System.Security.Cryptography;
using System.Text;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.Contracts.Modules.Procurement;

namespace MiniErp.App.Modules.Procurement;

/// <summary>
/// Records the physical receipt of goods against an already Confirmed/PartiallyConfirmed Purchase Order.
/// This is Inventory-owned physical acceptance evidence: it never creates or updates any stock
/// ledger (none exists yet), never creates AP/supplier liability/GL/tax posting. Warehouse options
/// are server-authoritatively validated against the caller's Tenant and Company/Branch scope via
/// <see cref="IProcurementWarehouseProvider"/>, ensuring client-supplied warehouse identifiers never
/// self-authorize or cross organizational boundaries.
/// </summary>
public sealed class GoodsReceiptService
{
    private const string CreateOperationId = "procurement.goods-receipt.create";
    private const string CancelOperationId = "procurement.goods-receipt.cancel";
    private readonly PurchaseRequestAuthorizationService authorization;
    private readonly IGoodsReceiptPersistence persistence;
    private readonly IProcurementWarehouseProvider warehouseProvider;
    private readonly IGoodsReceiptInventoryEffectReader? inventoryEffectReader;

    public GoodsReceiptService(
        PurchaseRequestAuthorizationService authorization,
        IGoodsReceiptPersistence persistence,
        IProcurementWarehouseProvider warehouseProvider,
        IGoodsReceiptInventoryEffectReader? inventoryEffectReader = null)
    {
        this.authorization = authorization ?? throw new ArgumentNullException(nameof(authorization));
        this.persistence = persistence ?? throw new ArgumentNullException(nameof(persistence));
        this.warehouseProvider = warehouseProvider ?? throw new ArgumentNullException(nameof(warehouseProvider));
        this.inventoryEffectReader = inventoryEffectReader;
    }

    public async Task<GoodsReceiptOperationResult<IReadOnlyList<ProcurementWarehouseOption>>> ListWarehousesAsync(
        ProcurementRequestContext context,
        Guid? companyId = null,
        Guid? branchId = null,
        CancellationToken cancellationToken = default)
    {
        var authorized = authorization.Authorize(context, "procurement.warehouse.list");
        if (!authorized.Allowed)
        {
            return GoodsReceiptOperationResult<IReadOnlyList<ProcurementWarehouseOption>>.Failure(authorized.Code);
        }

        try
        {
            var options = await warehouseProvider.ListAsync(context, companyId, branchId, cancellationToken);
            return GoodsReceiptOperationResult<IReadOnlyList<ProcurementWarehouseOption>>.Success(options);
        }
        catch
        {
            return GoodsReceiptOperationResult<IReadOnlyList<ProcurementWarehouseOption>>.Failure("persistence_unavailable");
        }
    }

    public async Task<GoodsReceiptOperationResult<IReadOnlyList<GoodsReceiptListRecord>>> ListAsync(
        ProcurementRequestContext context,
        GoodsReceiptStatus? status,
        Guid? purchaseOrderId,
        CancellationToken cancellationToken = default)
    {
        var authorized = authorization.Authorize(context, "procurement.goods-receipt.list");
        if (!authorized.Allowed)
        {
            return GoodsReceiptOperationResult<IReadOnlyList<GoodsReceiptListRecord>>.Failure(authorized.Code);
        }

        try
        {
            return GoodsReceiptOperationResult<IReadOnlyList<GoodsReceiptListRecord>>.Success(
                await persistence.ListAsync(context.TenantContext, status, purchaseOrderId, cancellationToken));
        }
        catch
        {
            return GoodsReceiptOperationResult<IReadOnlyList<GoodsReceiptListRecord>>.Failure("persistence_unavailable");
        }
    }

    public async Task<GoodsReceiptOperationResult<IReadOnlyList<GoodsReceiptEligibleSourceRecord>>> ListEligibleSourcesAsync(
        ProcurementRequestContext context,
        CancellationToken cancellationToken = default)
    {
        const string operationId = "procurement.goods-receipt.eligible-source.list";
        var authorized = authorization.Authorize(context, operationId);
        if (!authorized.Allowed)
        {
            return GoodsReceiptOperationResult<IReadOnlyList<GoodsReceiptEligibleSourceRecord>>.Failure(authorized.Code);
        }

        try
        {
            var sources = await persistence.ListEligibleSourcesAsync(context.TenantContext, cancellationToken);
            var permitted = sources
                .Where(item => authorization.Authorize(context, operationId, item.Scope).Allowed && item.Lines.Any(line => line.RemainingReceivableQuantity > 0))
                .OrderBy(item => item.SupplierName, StringComparer.Ordinal)
                .ToArray();
            return GoodsReceiptOperationResult<IReadOnlyList<GoodsReceiptEligibleSourceRecord>>.Success(permitted);
        }
        catch
        {
            return GoodsReceiptOperationResult<IReadOnlyList<GoodsReceiptEligibleSourceRecord>>.Failure("persistence_unavailable");
        }
    }

    public async Task<GoodsReceiptOperationResult<GoodsReceiptRecord>> GetAsync(
        ProcurementRequestContext context,
        Guid goodsReceiptId,
        CancellationToken cancellationToken = default) =>
        await GetAuthorizedAsync(context, goodsReceiptId, "procurement.goods-receipt.read", cancellationToken);

    public async Task<GoodsReceiptOperationResult<GoodsReceiptRecord>> CreateAsync(
        ProcurementRequestContext context,
        GoodsReceiptCreateRequest request,
        string? idempotencyKey,
        string? requestFingerprint,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (!GoodsReceiptValuePolicy.TryNormalizeCreate(request, out var receivedDate, out var referenceNote, out var notes, out var lines))
        {
            return GoodsReceiptOperationResult<GoodsReceiptRecord>.Failure("validation_failed");
        }

        // Creation has no pre-existing target to authorize against, so the durable replay is authorized
        // against the scope of the Goods Receipt the original request actually created, mirroring the
        // established Purchase Order create-replay pattern.
        var replayProbe = await ProbeReplayAsync(context, CreateOperationId, null, idempotencyKey, requestFingerprint, cancellationToken);
        if (replayProbe.Outcome == GoodsReceiptReplayOutcome.Conflict)
        {
            return GoodsReceiptOperationResult<GoodsReceiptRecord>.Failure("idempotency_conflict");
        }

        if (replayProbe is { Outcome: GoodsReceiptReplayOutcome.Replay, Record: { } replayed })
        {
            var replayAuthorized = authorization.Authorize(context, CreateOperationId, replayed.Scope);
            return replayAuthorized.Allowed
                ? GoodsReceiptOperationResult<GoodsReceiptRecord>.Success(replayed)
                : GoodsReceiptOperationResult<GoodsReceiptRecord>.Failure(replayAuthorized.Code);
        }

        GoodsReceiptEligibleSourceRecord? source;
        try
        {
            source = await persistence.FindEligibleSourceAsync(context.TenantContext, request.PurchaseOrderId, cancellationToken);
        }
        catch
        {
            return GoodsReceiptOperationResult<GoodsReceiptRecord>.Failure("persistence_unavailable");
        }

        if (source is null || source.Status is not (PurchaseOrderStatus.Confirmed or PurchaseOrderStatus.PartiallyConfirmed))
        {
            return GoodsReceiptOperationResult<GoodsReceiptRecord>.Failure("goods_receipt_source_not_eligible");
        }

        var authorized = authorization.Authorize(context, CreateOperationId, source.Scope);
        if (!authorized.Allowed)
        {
            return GoodsReceiptOperationResult<GoodsReceiptRecord>.Failure(authorized.Code);
        }

        var warehouseOption = await warehouseProvider.FindAsync(context, request.WarehouseId, cancellationToken);
        if (warehouseOption is null)
        {
            return GoodsReceiptOperationResult<GoodsReceiptRecord>.Failure("warehouse_not_authorized");
        }

        if (!warehouseOption.IsActive)
        {
            return GoodsReceiptOperationResult<GoodsReceiptRecord>.Failure("warehouse_inactive");
        }

        if (warehouseOption.CompanyId != source.Scope.CompanyId || (source.Scope.BranchId.HasValue && warehouseOption.BranchId.HasValue && warehouseOption.BranchId != source.Scope.BranchId))
        {
            return GoodsReceiptOperationResult<GoodsReceiptRecord>.Failure("warehouse_scope_denied");
        }

        var eligibleLines = source.Lines.ToDictionary(item => item.PurchaseOrderLineId);
        var commandLines = new List<GoodsReceiptLineCreateCommand>(lines.Count);
        foreach (var line in lines)
        {
            if (!eligibleLines.TryGetValue(line.PurchaseOrderLineId, out var eligibleLine))
            {
                return GoodsReceiptOperationResult<GoodsReceiptRecord>.Failure("goods_receipt_line_not_eligible");
            }

            if (line.AcceptedQuantity > eligibleLine.RemainingReceivableQuantity)
            {
                return GoodsReceiptOperationResult<GoodsReceiptRecord>.Failure("over_receipt_not_allowed");
            }

            commandLines.Add(new GoodsReceiptLineCreateCommand(
                Guid.NewGuid(),
                line.PurchaseOrderLineId,
                line.ReceivedQuantity,
                line.AcceptedQuantity,
                line.RejectedQuantity,
                line.DamagedQuantity,
                line.DamageNotes,
                line.Notes));
        }

        var id = Guid.NewGuid();
        var occurredAt = DateTimeOffset.UtcNow;
        var command = new GoodsReceiptCreateCommand(
            id,
            source.Scope,
            request.PurchaseOrderId,
            context.ActorId,
            request.WarehouseId,
            receivedDate,
            referenceNote,
            notes,
            commandLines,
            occurredAt,
            idempotencyKey);
        var evidence = CreateEvidence(context, id, source.Scope, CreateOperationId, null, GoodsReceiptStatus.Recorded, null, idempotencyKey, requestFingerprint, null, "Recorded");
        return ToOperationResult(await persistence.CreateAsync(context.TenantContext, command, evidence, cancellationToken));
    }

    public async Task<GoodsReceiptOperationResult<GoodsReceiptRecord>> CancelAsync(
        ProcurementRequestContext context,
        Guid goodsReceiptId,
        byte[] expectedVersion,
        string? reason,
        string? idempotencyKey,
        string? requestFingerprint,
        CancellationToken cancellationToken = default)
    {
        var current = await GetAuthorizedAsync(context, goodsReceiptId, CancelOperationId, cancellationToken);
        if (!current.Succeeded || current.Value is null)
        {
            return current;
        }

        if (expectedVersion is null || expectedVersion.Length == 0)
        {
            return GoodsReceiptOperationResult<GoodsReceiptRecord>.Failure("validation_failed");
        }

        if (!GoodsReceiptValuePolicy.TryReason(reason, out var normalizedReason))
        {
            return GoodsReceiptOperationResult<GoodsReceiptRecord>.Failure("reason_required");
        }

        var replay = await TryDurableReplayAsync(context, CancelOperationId, goodsReceiptId, idempotencyKey, requestFingerprint, cancellationToken);
        if (replay is not null)
        {
            return replay;
        }

        if (current.Value.Status != GoodsReceiptStatus.Recorded)
        {
            return GoodsReceiptOperationResult<GoodsReceiptRecord>.Failure("cancel_not_allowed");
        }

        if (inventoryEffectReader is not null && await inventoryEffectReader.HasActiveEffectAsync(context.TenantContext, goodsReceiptId, cancellationToken))
        {
            return GoodsReceiptOperationResult<GoodsReceiptRecord>.Failure("inventory_effect_exists");
        }

        var command = new GoodsReceiptActionCommand(goodsReceiptId, expectedVersion, context.ActorId, normalizedReason, DateTimeOffset.UtcNow, idempotencyKey);
        var evidence = CreateEvidence(context, goodsReceiptId, current.Value.Scope, CancelOperationId, current.Value.Status, GoodsReceiptStatus.Cancelled, normalizedReason, idempotencyKey, requestFingerprint, current.Value.Status.ToString(), "Cancelled");
        return ToOperationResult(await persistence.CancelAsync(context.TenantContext, command, evidence, cancellationToken));
    }

    public async Task<GoodsReceiptOperationResult<IReadOnlyList<GoodsReceiptHistoryRecord>>> ReadHistoryAsync(ProcurementRequestContext context, Guid goodsReceiptId, CancellationToken cancellationToken = default)
    {
        var current = await GetAuthorizedAsync(context, goodsReceiptId, "procurement.goods-receipt.history.read", cancellationToken);
        if (!current.Succeeded || current.Value is null)
        {
            return GoodsReceiptOperationResult<IReadOnlyList<GoodsReceiptHistoryRecord>>.Failure(current.Code);
        }

        try
        {
            return GoodsReceiptOperationResult<IReadOnlyList<GoodsReceiptHistoryRecord>>.Success(await persistence.ReadHistoryAsync(context.TenantContext, goodsReceiptId, cancellationToken));
        }
        catch
        {
            return GoodsReceiptOperationResult<IReadOnlyList<GoodsReceiptHistoryRecord>>.Failure("persistence_unavailable");
        }
    }

    public async Task<GoodsReceiptOperationResult<IReadOnlyList<GoodsReceiptAuditRecord>>> ReadAuditAsync(ProcurementRequestContext context, Guid goodsReceiptId, CancellationToken cancellationToken = default)
    {
        var current = await GetAuthorizedAsync(context, goodsReceiptId, "procurement.goods-receipt.audit.read", cancellationToken);
        if (!current.Succeeded || current.Value is null)
        {
            return GoodsReceiptOperationResult<IReadOnlyList<GoodsReceiptAuditRecord>>.Failure(current.Code);
        }

        try
        {
            return GoodsReceiptOperationResult<IReadOnlyList<GoodsReceiptAuditRecord>>.Success(await persistence.ReadAuditAsync(context.TenantContext, goodsReceiptId, cancellationToken));
        }
        catch
        {
            return GoodsReceiptOperationResult<IReadOnlyList<GoodsReceiptAuditRecord>>.Failure("persistence_unavailable");
        }
    }

    private async Task<GoodsReceiptOperationResult<GoodsReceiptRecord>> GetAuthorizedAsync(ProcurementRequestContext context, Guid goodsReceiptId, string operationId, CancellationToken cancellationToken)
    {
        var current = await FindAsync(context, goodsReceiptId, cancellationToken);
        if (!current.Succeeded || current.Value is null)
        {
            return current;
        }

        var authorized = authorization.Authorize(context, operationId, current.Value.Scope);
        return authorized.Allowed ? current : GoodsReceiptOperationResult<GoodsReceiptRecord>.Failure(authorized.Code);
    }

    private async Task<GoodsReceiptOperationResult<GoodsReceiptRecord>> FindAsync(ProcurementRequestContext context, Guid goodsReceiptId, CancellationToken cancellationToken)
    {
        if (goodsReceiptId == Guid.Empty)
        {
            return GoodsReceiptOperationResult<GoodsReceiptRecord>.Failure("validation_failed");
        }

        try
        {
            var record = await persistence.FindAsync(context.TenantContext, goodsReceiptId, cancellationToken);
            return record is null
                ? GoodsReceiptOperationResult<GoodsReceiptRecord>.Failure("goods_receipt_not_found")
                : GoodsReceiptOperationResult<GoodsReceiptRecord>.Success(record);
        }
        catch
        {
            return GoodsReceiptOperationResult<GoodsReceiptRecord>.Failure("persistence_unavailable");
        }
    }

    private static GoodsReceiptAuditEvidence CreateEvidence(ProcurementRequestContext context, Guid goodsReceiptId, PurchaseRequestScope scope, string operationId, GoodsReceiptStatus? beforeStatus, GoodsReceiptStatus? afterStatus, string? reason, string? idempotencyKey, string? requestFingerprint, string? beforeSummary, string? afterSummary) => new(
        Guid.NewGuid(),
        goodsReceiptId,
        DateTimeOffset.UtcNow,
        operationId,
        context.CorrelationId?.Value ?? Guid.NewGuid().ToString("N"),
        context.TenantId.Value,
        context.ActorId,
        context.SessionId,
        context.AuthorizationPath.ToString(),
        "Allowed",
        reason,
        beforeStatus,
        afterStatus,
        scope.CompanyId,
        scope.BranchId,
        beforeSummary,
        afterSummary,
        idempotencyKey,
        ComputeRequestFingerprint(operationId, goodsReceiptId, requestFingerprint));

    private static string? ComputeRequestFingerprint(string operationId, Guid goodsReceiptId, string? rawFingerprint)
    {
        if (string.IsNullOrWhiteSpace(rawFingerprint))
        {
            return null;
        }

        var targetDiscriminator = operationId == CreateOperationId ? "new" : goodsReceiptId.ToString("D");
        var canonical = $"{operationId}|{targetDiscriminator}|{rawFingerprint}";
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(canonical)));
    }

    /// <summary>
    /// Consults durable persisted idempotency evidence after the trusted Tenant context, the current
    /// target, and the caller's authority over that target have already been established, but before any
    /// lifecycle/state check that the original successful request itself would have changed.
    /// </summary>
    private async Task<GoodsReceiptOperationResult<GoodsReceiptRecord>?> TryDurableReplayAsync(
        ProcurementRequestContext context,
        string operationId,
        Guid goodsReceiptId,
        string? idempotencyKey,
        string? requestFingerprint,
        CancellationToken cancellationToken)
    {
        var probe = await ProbeReplayAsync(context, operationId, goodsReceiptId, idempotencyKey, requestFingerprint, cancellationToken);
        return probe.Outcome switch
        {
            GoodsReceiptReplayOutcome.Replay when probe.Record is not null => GoodsReceiptOperationResult<GoodsReceiptRecord>.Success(probe.Record),
            GoodsReceiptReplayOutcome.Conflict => GoodsReceiptOperationResult<GoodsReceiptRecord>.Failure("idempotency_conflict"),
            _ => null
        };
    }

    private async Task<GoodsReceiptReplayProbe> ProbeReplayAsync(
        ProcurementRequestContext context,
        string operationId,
        Guid? goodsReceiptId,
        string? idempotencyKey,
        string? requestFingerprint,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(idempotencyKey))
        {
            return GoodsReceiptReplayProbe.NotFound;
        }

        var query = new GoodsReceiptReplayQuery(
            context.ActorId,
            operationId,
            idempotencyKey,
            goodsReceiptId,
            ComputeRequestFingerprint(operationId, goodsReceiptId ?? Guid.Empty, requestFingerprint));
        try
        {
            return await persistence.ProbeReplayAsync(context.TenantContext, query, cancellationToken);
        }
        catch
        {
            // A probe failure must never approve a mutation on its own; fall through to the normal path,
            // where the persistence-side replay check still runs inside the mutation transaction.
            return GoodsReceiptReplayProbe.NotFound;
        }
    }

    private static GoodsReceiptOperationResult<T> ToOperationResult<T>(GoodsReceiptPersistenceResult<T> result) =>
        result.Succeeded && result.Value is not null
            ? GoodsReceiptOperationResult<T>.Success(result.Value)
            : GoodsReceiptOperationResult<T>.Failure(result.Code);
}

#pragma warning restore CS1591
