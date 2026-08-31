#pragma warning disable CS1591

using System.Data;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.Modules.Procurement;
using MiniErp.App.Modules.Sales;
using MiniErp.Contracts.Modules.Sales;

namespace MiniErp.Infrastructure.Persistence.Modules.Sales;

public sealed class CustomerReturnPersistence(DbContextOptions options) : ISalesCustomerReturnPersistence, ISalesCustomerReturnSourceProvider
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private SalesDbContext Create(ProcurementRequestContext context) => new(options, context.TenantContext);
    private SalesDbContext Create(TenantContext context) => new(options, context);

    public async Task<IReadOnlyList<SalesCustomerReturnSourceRecord>> ListEligibleSourcesAsync(ProcurementRequestContext context, CancellationToken cancellationToken = default)
    {
        await using var db = Create(context);
        var deliveries = await db.Deliveries.AsNoTracking().Where(item => item.Status == SalesDeliveryStatus.Posted).OrderByDescending(item => item.PostedAt).Take(500).ToListAsync(cancellationToken);
        var result = new List<SalesCustomerReturnSourceRecord>(deliveries.Count);
        foreach (var delivery in deliveries)
        {
            var source = await BuildSourceAsync(db, delivery, null, cancellationToken);
            if (source is not null && source.Lines.Any(item => item.EligibleQuantity > 0m)) result.Add(source);
        }
        return result;
    }

    public async Task<SalesCustomerReturnSourceRecord?> GetEligibleSourceAsync(ProcurementRequestContext context, Guid deliveryId, CancellationToken cancellationToken = default)
    {
        await using var db = Create(context);
        var delivery = await db.Deliveries.AsNoTracking().SingleOrDefaultAsync(item => item.Id == deliveryId && item.Status == SalesDeliveryStatus.Posted, cancellationToken);
        return delivery is null ? null : await BuildSourceAsync(db, delivery, null, cancellationToken);
    }

    public async Task<SalesCustomerReturnResponse?> GetAsync(ProcurementRequestContext context, Guid id, CancellationToken cancellationToken = default)
    {
        await using var db = Create(context);
        var item = await db.CustomerReturns.AsNoTracking().Include(value => value.Lines).SingleOrDefaultAsync(value => value.Id == id, cancellationToken);
        return item is null || !InScope(context, item.CompanyId, item.BranchId) ? null : ToResponse(item);
    }

    public async Task<IReadOnlyList<SalesHistoryResponse>> ListHistoryAsync(ProcurementRequestContext context, Guid id, CancellationToken cancellationToken = default)
    {
        await using var db = Create(context);
        return (await db.History.AsNoTracking().Where(item => item.DocumentType == "customer-return" && item.DocumentId == id).OrderByDescending(item => item.OccurredAt).ToListAsync(cancellationToken)).Select(item => new SalesHistoryResponse(item.Id, item.DocumentType, item.DocumentId, item.Action.ToString(), item.FromStatus, item.ToStatus, item.ActorId, item.OccurredAt, item.Reason, item.PolicyId, item.PolicyVersion, item.CreditOutcome, item.SnapshotHash, item.SnapshotJson)).ToArray();
    }

    public async Task<IReadOnlyList<SalesAuditResponse>> ListAuditAsync(ProcurementRequestContext context, Guid id, CancellationToken cancellationToken = default)
    {
        await using var db = Create(context);
        return (await db.Audit.AsNoTracking().Where(item => item.DocumentType == "customer-return" && item.DocumentId == id).OrderByDescending(item => item.OccurredAt).ToListAsync(cancellationToken)).Select(item => new SalesAuditResponse(item.Id, item.OperationId, item.DocumentType, item.DocumentId, item.ActorId, item.OccurredAt, item.Decision, item.Reason, item.BeforeSummary, item.AfterSummary, item.IdempotencyKey, item.CorrelationId)).ToArray();
    }

    public async Task<SalesCustomerReturnSourceRecord?> GetCustomerReturnSourceAsync(TenantContext context, Guid returnId, CancellationToken cancellationToken = default)
    {
        await using var db = Create(context);
        var item = await db.CustomerReturns.AsNoTracking().Include(value => value.Lines).SingleOrDefaultAsync(value => value.Id == returnId, cancellationToken);
        if (item is null) return null;
        var delivery = await db.Deliveries.AsNoTracking().SingleOrDefaultAsync(value => value.Id == item.DeliveryId && value.Status == SalesDeliveryStatus.Posted, cancellationToken);
        return delivery is null ? null : await BuildSourceAsync(db, delivery, item, cancellationToken);
    }

    public async Task<SalesCustomerReturnOperationResult<SalesCustomerReturnResponse>> CreateAsync(ProcurementRequestContext context, SalesCustomerReturnCreateCommand command, CancellationToken cancellationToken = default)
    {
        await using var db = Create(context);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var operation = "sales.customer-return.create";
        var replay = await ReadReplayAsync(db, context, operation, command.IdempotencyKey, command.RequestFingerprint, cancellationToken);
        if (replay is not null) return replay;
        var delivery = await db.Deliveries.AsNoTracking().SingleOrDefaultAsync(item => item.Id == command.Request.DeliveryId && item.Status == SalesDeliveryStatus.Posted, cancellationToken);
        var source = delivery is null ? null : await BuildSourceAsync(db, delivery, null, cancellationToken);
        if (source is null) return Failure("return_source_not_found");
        if (command.Request.InvoiceId is { } invoiceId && invoiceId != source.RecognizedInvoiceId) return Failure("invoice_source_mismatch");
        if (command.Request.Consequence == SalesCustomerReturnConsequence.CreditNote && source.FinanceOpenItemId is null) return Failure("recognized_invoice_required");
        var lines = command.Request.Lines.ToDictionary(item => item.OrderLineId);
        foreach (var requestLine in command.Request.Lines)
        {
            var sourceLine = source.Lines.SingleOrDefault(item => item.OrderLineId == requestLine.OrderLineId);
            if (sourceLine is null || requestLine.Quantity > sourceLine.EligibleQuantity) return Failure("return_quantity_conflict");
        }
        var entity = new SalesCustomerReturnEntity(context.TenantId, command.Id, command.Request, source, command.ActorId, command.OccurredAt);
        foreach (var requestLine in command.Request.Lines)
        {
            var sourceLine = source.Lines.Single(item => item.OrderLineId == requestLine.OrderLineId);
            entity.Lines.Add(new SalesCustomerReturnLineEntity(context.TenantId, Guid.NewGuid(), entity.Id, source.DeliveryId, requestLine, sourceLine));
        }
        db.CustomerReturns.Add(entity);
        AddHistory(db, context, entity.Id, SalesHistoryAction.Created, null, entity.Status, command.Request.Reason, command.RequestFingerprint);
        AddAudit(db, context, operation, entity.Id, "Allowed", command.Request.Reason, null, $"status={entity.Status};delivery={entity.DeliveryId}", command.IdempotencyKey, command.RequestFingerprint, command.OccurredAt);
        await db.SaveChangesAsync(cancellationToken);
        var response = ToResponse(entity);
        await SaveReplayAsync(db, context, operation, command, response, "customer-return", entity.Id, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return SalesCustomerReturnOperationResult<SalesCustomerReturnResponse>.Success(response);
    }

    public async Task<SalesCustomerReturnOperationResult<SalesCustomerReturnResponse>> MutateAsync(ProcurementRequestContext context, SalesCustomerReturnActionCommand command, CancellationToken cancellationToken = default)
    {
        await using var db = Create(context);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var operation = $"sales.customer-return.{command.Action.ToString().ToLowerInvariant()}";
        var replay = await ReadReplayAsync(db, context, operation, command.IdempotencyKey, command.RequestFingerprint, cancellationToken);
        if (replay is not null) return replay;
        var entity = await db.CustomerReturns.Include(item => item.Lines).SingleOrDefaultAsync(item => item.Id == command.Id, cancellationToken);
        if (entity is null || !InScope(context, entity.CompanyId, entity.BranchId)) return Failure("customer_return_not_found");
        if (!entity.Version.SequenceEqual(command.ExpectedVersion)) return Failure("concurrency_conflict");
        var target = command.Action switch
        {
            SalesCustomerReturnMutation.Submit when entity.Status == SalesCustomerReturnStatus.Draft => SalesCustomerReturnStatus.Submitted,
            SalesCustomerReturnMutation.Approve when entity.Status == SalesCustomerReturnStatus.Submitted => SalesCustomerReturnStatus.AwaitingReceipt,
            SalesCustomerReturnMutation.Reject when entity.Status is SalesCustomerReturnStatus.Draft or SalesCustomerReturnStatus.Submitted => SalesCustomerReturnStatus.Rejected,
            SalesCustomerReturnMutation.Cancel when entity.Status is SalesCustomerReturnStatus.Draft or SalesCustomerReturnStatus.Submitted => SalesCustomerReturnStatus.Cancelled,
            SalesCustomerReturnMutation.Reverse when entity.Status is SalesCustomerReturnStatus.Approved or SalesCustomerReturnStatus.AwaitingReceipt => SalesCustomerReturnStatus.Reversed,
            _ => (SalesCustomerReturnStatus?)null
        };
        if (target is null) return Failure("customer_return_transition_invalid");
        var before = entity.Status;
        entity.SetStatus(target.Value, command.OccurredAt);
        AddHistory(db, context, entity.Id, command.Action == SalesCustomerReturnMutation.Reverse ? SalesHistoryAction.Reversed : ActionToHistory(command.Action), before, target.Value, command.Reason, command.RequestFingerprint);
        AddAudit(db, context, operation, entity.Id, "Allowed", command.Reason, $"status={before}", $"status={target}", command.IdempotencyKey, command.RequestFingerprint, command.OccurredAt);
        await db.SaveChangesAsync(cancellationToken);
        var response = ToResponse(entity);
        await SaveReplayAsync(db, context, operation, command, response, "customer-return", entity.Id, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return SalesCustomerReturnOperationResult<SalesCustomerReturnResponse>.Success(response);
    }

    private async Task<SalesCustomerReturnSourceRecord?> BuildSourceAsync(SalesDbContext db, SalesDeliveryEntity delivery, SalesCustomerReturnEntity? current, CancellationToken cancellationToken)
    {
        var order = await db.Orders.AsNoTracking().SingleOrDefaultAsync(item => item.Id == delivery.OrderId && item.RevisionNumber == delivery.OrderRevisionNumber, cancellationToken);
        if (order is null) return null;
        var orderLines = Lines(order.LinesJson).ToDictionary(item => item.Id);
        var deliveryLines = JsonSerializer.Deserialize<IReadOnlyList<SalesDeliveryRequestLine>>(delivery.LinesJson, Json) ?? [];
        var returns = await db.CustomerReturnLines.AsNoTracking().Where(item => item.DeliveryId == delivery.Id).ToListAsync(cancellationToken);
        var activeReturnIds = await db.CustomerReturns.AsNoTracking().Where(item => item.DeliveryId == delivery.Id && item.Status != SalesCustomerReturnStatus.Rejected && item.Status != SalesCustomerReturnStatus.Cancelled && item.Status != SalesCustomerReturnStatus.Reversed).Select(item => item.Id).ToListAsync(cancellationToken);
        var consumed = returns.Where(item => activeReturnIds.Contains(item.CustomerReturnId) && current?.Id != item.CustomerReturnId).GroupBy(item => item.OrderLineId).ToDictionary(group => group.Key, group => group.Sum(item => item.ReturnQuantity));
        var currentLines = current?.Lines.ToDictionary(item => item.OrderLineId) ?? [];
        var invoice = await db.InvoiceRequests.AsNoTracking().Where(item => item.DeliveryId == delivery.Id && item.Status == SalesInvoiceRequestStatus.Posted).OrderByDescending(item => item.PostedAt).FirstOrDefaultAsync(cancellationToken);
        var sourceLines = new List<SalesCustomerReturnSourceLineRecord>();
        foreach (var deliveryLine in deliveryLines)
        {
            if (!orderLines.TryGetValue(deliveryLine.OrderLineId, out var orderLine) || deliveryLine.Quantity <= 0m) return null;
            var prior = consumed.GetValueOrDefault(deliveryLine.OrderLineId);
            var currentQuantity = currentLines.GetValueOrDefault(deliveryLine.OrderLineId)?.ReturnQuantity ?? 0m;
            var unitGross = decimal.Round(orderLine.LineTotal / orderLine.Quantity, 8, MidpointRounding.ToEven);
            var unitTax = decimal.Round(orderLine.TaxAmount / orderLine.Quantity, 8, MidpointRounding.ToEven);
            sourceLines.Add(new(deliveryLine.OrderLineId, orderLine.ProductId, orderLine.ProductSku, orderLine.ProductName, orderLine.UnitOfMeasureId, orderLine.UnitOfMeasureCode, deliveryLine.Quantity, prior, Math.Max(0m, deliveryLine.Quantity - prior), decimal.Round(unitGross - unitTax, 8, MidpointRounding.ToEven), unitTax, unitGross, null, currentQuantity, currentLines.GetValueOrDefault(deliveryLine.OrderLineId)?.Id, orderLine.TaxId, orderLine.TaxRateVersionId));
        }
        return new(current?.Id ?? Guid.Empty, delivery.Id, delivery.OrderId, delivery.OrderRevisionNumber, delivery.TenantId.Value, delivery.CompanyId, delivery.BranchId, delivery.CustomerId, delivery.WarehouseId, delivery.PostedAt, invoice?.Id, invoice?.FinanceOpenItemId, order.CurrencyCode, sourceLines, current?.Status ?? SalesCustomerReturnStatus.Approved, current?.Consequence ?? SalesCustomerReturnConsequence.None, current?.Version ?? delivery.Version);
    }

    private static IReadOnlyList<SalesQuotationLineResponse> Lines(string json) => JsonSerializer.Deserialize<IReadOnlyList<SalesQuotationLineResponse>>(json, Json) ?? [];
    private static SalesCustomerReturnResponse ToResponse(SalesCustomerReturnEntity item) => new(item.Id, item.TenantId.Value, item.DeliveryId, item.OrderId, item.OrderRevisionNumber, item.CompanyId, item.BranchId, item.CustomerId, item.WarehouseId, item.InvoiceId, item.FinanceOpenItemId, item.Status, item.Consequence, item.ReturnDate, item.Reason, item.HandoffJson, item.CreatedAt, item.UpdatedAt, item.Lines.Select(line => new SalesCustomerReturnLineResponse(line.Id, line.OrderLineId, line.DeliveredQuantity, line.PreviouslyReturnedQuantity, line.ReturnQuantity, line.Reason)).ToArray(), JsonSerializer.Deserialize<IReadOnlyList<SalesCustomerReturnEvidenceReference>>(item.EvidenceJson, Json) ?? [], item.Version);
    private static bool InScope(ProcurementRequestContext context, Guid companyId, Guid? branchId) => context.TenantContext.Scope is not { } scope || ScopeMatches(scope.Value, companyId, branchId);
    private static bool ScopeMatches(string value, Guid companyId, Guid? branchId) { var parts = value.Split(':', 2); return parts.Length == 2 && Guid.TryParse(parts[1], out var id) && (parts[0] switch { "Tenant" => true, "Company" => companyId == id, "Branch" => branchId == id, _ => false }); }
    private static SalesHistoryAction ActionToHistory(SalesCustomerReturnMutation action) => action switch { SalesCustomerReturnMutation.Submit => SalesHistoryAction.Submitted, SalesCustomerReturnMutation.Approve => SalesHistoryAction.Approved, SalesCustomerReturnMutation.Reject => SalesHistoryAction.Rejected, _ => SalesHistoryAction.Cancelled };
    private static SalesCustomerReturnOperationResult<SalesCustomerReturnResponse> Failure(string code) => SalesCustomerReturnOperationResult<SalesCustomerReturnResponse>.Failure(code);
    private static void AddHistory(SalesDbContext db, ProcurementRequestContext context, Guid id, SalesHistoryAction action, Enum? from, Enum? to, string? reason, string fingerprint) => db.History.Add(new SalesHistoryEntity(context.TenantId, "customer-return", id, action, from?.ToString(), to?.ToString(), context.ActorId, reason, null, null, null, fingerprint, DateTimeOffset.UtcNow));
    private static void AddAudit(SalesDbContext db, ProcurementRequestContext context, string operation, Guid id, string decision, string? reason, string? before, string? after, string? key, string fingerprint, DateTimeOffset at) => db.Audit.Add(new SalesAuditEntity(context.TenantId, operation, "customer-return", id, context.ActorId, at, decision, reason, before, after, key, context.CorrelationId?.Value ?? "sales"));
    private static async Task<SalesCustomerReturnOperationResult<SalesCustomerReturnResponse>?> ReadReplayAsync(SalesDbContext db, ProcurementRequestContext context, string operation, string? key, string fingerprint, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(key)) return null;
        var row = await db.Idempotency.AsNoTracking().SingleOrDefaultAsync(item => item.OperationId == operation && item.Key == key, cancellationToken);
        if (row is null) return null;
        return row.Fingerprint == fingerprint ? SalesCustomerReturnOperationResult<SalesCustomerReturnResponse>.Success(JsonSerializer.Deserialize<SalesCustomerReturnResponse>(row.ResponseJson, Json)!) : SalesCustomerReturnOperationResult<SalesCustomerReturnResponse>.Failure("idempotency_conflict");
    }
    private static Task SaveReplayAsync<TCommand>(SalesDbContext db, ProcurementRequestContext context, string operation, TCommand command, SalesCustomerReturnResponse response, string type, Guid id, CancellationToken cancellationToken) where TCommand : notnull
    {
        var key = command switch { SalesCustomerReturnCreateCommand create => create.IdempotencyKey, SalesCustomerReturnActionCommand action => action.IdempotencyKey, _ => null };
        var fingerprint = command switch { SalesCustomerReturnCreateCommand create => create.RequestFingerprint, SalesCustomerReturnActionCommand action => action.RequestFingerprint, _ => string.Empty };
        if (!string.IsNullOrWhiteSpace(key)) db.Idempotency.Add(new SalesIdempotencyEntity(context.TenantId, operation, key!, fingerprint, type, id, JsonSerializer.Serialize(response, Json), DateTimeOffset.UtcNow));
        return Task.CompletedTask;
    }
}

#pragma warning restore CS1591
