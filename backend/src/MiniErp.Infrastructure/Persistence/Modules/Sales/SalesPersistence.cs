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

public sealed class SalesPersistence(DbContextOptions options) : ISalesPersistence
{
    private readonly DbContextOptions options = options;
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    private SalesDbContext Create(ProcurementRequestContext context) => new(options, context.TenantContext);

    public async Task<IReadOnlyList<SalesQuotationSummaryResponse>> ListQuotationsAsync(ProcurementRequestContext context, Guid? companyId, SalesQuotationStatus? status, CancellationToken cancellationToken = default)
    {
        await using var db = Create(context);
        var query = ApplyTrustedScope(db.Quotations.AsNoTracking(), context.TenantContext.Scope);
        if (companyId is { } company) query = query.Where(item => item.CompanyId == company);
        if (status is { } state) query = query.Where(item => item.Status == state);
        return (await query.ToListAsync(cancellationToken)).OrderByDescending(item => item.UpdatedAt).Take(500).Select(ToSummary).ToArray();
    }

    public async Task<SalesQuotationResponse?> GetQuotationAsync(ProcurementRequestContext context, Guid id, CancellationToken cancellationToken = default)
    {
        await using var db = Create(context);
        var entity = await ApplyTrustedScope(db.Quotations.AsNoTracking(), context.TenantContext.Scope).SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        return entity is null ? null : ToResponse(entity);
    }

    public async Task<SalesOperationResult<SalesQuotationResponse>> CreateQuotationAsync(ProcurementRequestContext context, SalesQuotationWriteModel model, string idempotencyKey, string requestFingerprint, SalesApprovalPolicyDefinition? policy, CancellationToken cancellationToken = default)
    {
        await using var db = Create(context);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var replay = await ReplayAsync<SalesQuotationResponse>(db, context, "sales.quotation.create", idempotencyKey, requestFingerprint, cancellationToken);
        if (replay is not null) return replay;
        var now = DateTimeOffset.UtcNow;
        var number = $"SQ-{now:yyyy}-{model.Id:N}"[..17];
        var entity = new SalesQuotationEntity(context.TenantId, model, number, LinesJson(model.Lines), JsonSerializer.Serialize(policy, Json), now);
        entity.SetCreator(context.ActorId);
        db.Quotations.Add(entity);
        await db.SaveChangesAsync(cancellationToken);
        var response = ToResponse(entity);
        AddRevision(db, context, entity, response, null, now);
        AddHistory(db, context, "quotation", entity.Id, SalesHistoryAction.Created, null, entity.Status, null, policy, null, null, now);
        AddAudit(db, context, "sales.quotation.create", "quotation", entity.Id, "Allowed", null, null, Summary(response), idempotencyKey, now);
        await db.SaveChangesAsync(cancellationToken);
        await SaveIdempotencyAsync(db, context, "sales.quotation.create", idempotencyKey, requestFingerprint, "quotation", entity.Id, response, now, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return SalesOperationResult<SalesQuotationResponse>.Success(response);
    }

    public async Task<SalesOperationResult<SalesQuotationResponse>> EditQuotationAsync(ProcurementRequestContext context, Guid id, SalesQuotationWriteModel model, byte[] expectedVersion, string idempotencyKey, string requestFingerprint, CancellationToken cancellationToken = default)
    {
        await using var db = Create(context);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var replay = await ReplayAsync<SalesQuotationResponse>(db, context, "sales.quotation.edit", idempotencyKey, requestFingerprint, cancellationToken);
        if (replay is not null) return replay;
        var entity = await ApplyTrustedScope(db.Quotations, context.TenantContext.Scope).SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (entity is null) return Failure<SalesQuotationResponse>("quotation_not_found");
        if (!entity.Version.SequenceEqual(expectedVersion)) return Failure<SalesQuotationResponse>("concurrency_conflict");
        if (entity.Status is not (SalesQuotationStatus.Draft or SalesQuotationStatus.ReturnedForChange)) return Failure<SalesQuotationResponse>("quotation_edit_locked");
        var before = ToResponse(entity);
        var now = DateTimeOffset.UtcNow;
        entity.Edit(model, LinesJson(model.Lines), now);
        await db.SaveChangesAsync(cancellationToken);
        var response = ToResponse(entity);
        AddRevision(db, context, entity, response, "quotation-edited", now);
        AddHistory(db, context, "quotation", id, SalesHistoryAction.Edited, before.Status, entity.Status, "quotation-edited", null, null, Hash(response), now);
        AddAudit(db, context, "sales.quotation.edit", "quotation", id, "Allowed", null, Summary(before), Summary(response), idempotencyKey, now);
        await db.SaveChangesAsync(cancellationToken);
        await SaveIdempotencyAsync(db, context, "sales.quotation.edit", idempotencyKey, requestFingerprint, "quotation", id, response, now, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return SalesOperationResult<SalesQuotationResponse>.Success(response);
    }

    public async Task<SalesOperationResult<SalesQuotationResponse>> TransitionQuotationAsync(ProcurementRequestContext context, Guid id, SalesQuotationStatus target, string? reason, byte[] expectedVersion, string idempotencyKey, string requestFingerprint, SalesApprovalPolicyDefinition? policy, Guid? delegatedFromActorId = null, CancellationToken cancellationToken = default)
    {
        await using var db = Create(context);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var replay = await ReplayAsync<SalesQuotationResponse>(db, context, $"sales.quotation.{Action(target)}", idempotencyKey, requestFingerprint, cancellationToken);
        if (replay is not null) return replay;
        var entity = await ApplyTrustedScope(db.Quotations, context.TenantContext.Scope).SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (entity is null) return Failure<SalesQuotationResponse>("quotation_not_found");
        if (!entity.Version.SequenceEqual(expectedVersion)) return Failure<SalesQuotationResponse>("concurrency_conflict");
        if (!CanTransition(entity.Status, target)) return Failure<SalesQuotationResponse>("quotation_transition_invalid");
        var before = entity.Status;
        var now = DateTimeOffset.UtcNow;
        entity.Transition(target, now, JsonSerializer.Serialize(policy, Json));
        await db.SaveChangesAsync(cancellationToken);
        var response = ToResponse(entity);
        AddHistory(db, context, "quotation", id, ActionToHistory(target), before, target, reason, policy, null, Hash(response), now);
        AddAudit(db, context, $"sales.quotation.{Action(target)}", "quotation", id, "Allowed", reason, $"status={before}", $"status={target};revision={entity.RevisionNumber}", idempotencyKey, now);
        await db.SaveChangesAsync(cancellationToken);
        await SaveIdempotencyAsync(db, context, $"sales.quotation.{Action(target)}", idempotencyKey, requestFingerprint, "quotation", id, response, now, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return SalesOperationResult<SalesQuotationResponse>.Success(response);
    }

    public async Task<IReadOnlyList<SalesQuotationRevisionResponse>> ListQuotationRevisionsAsync(ProcurementRequestContext context, Guid id, CancellationToken cancellationToken = default)
    {
        await using var db = Create(context);
        var rows = await db.QuotationRevisions.AsNoTracking().Where(item => item.QuotationId == id).OrderByDescending(item => item.RevisionNumber).ToListAsync(cancellationToken);
        return rows.Select(item => new SalesQuotationRevisionResponse(item.Id, item.QuotationId, item.RevisionNumber, item.Status, item.SnapshotHash, item.ActorId, item.OccurredAt, item.Reason, JsonSerializer.Deserialize<SalesQuotationResponse>(item.SnapshotJson, Json)!)).ToArray();
    }

    public async Task<IReadOnlyList<SalesHistoryResponse>> ListHistoryAsync(ProcurementRequestContext context, string documentType, Guid id, CancellationToken cancellationToken = default)
    {
        await using var db = Create(context);
        return (await db.History.AsNoTracking().Where(item => item.DocumentType == documentType && item.DocumentId == id).ToListAsync(cancellationToken)).OrderByDescending(item => item.OccurredAt).Select(item => new SalesHistoryResponse(item.Id, item.DocumentType, item.DocumentId, item.Action.ToString(), item.FromStatus, item.ToStatus, item.ActorId, item.OccurredAt, item.Reason, item.PolicyId, item.PolicyVersion, item.CreditOutcome, item.SnapshotHash)).ToArray();
    }

    public async Task<IReadOnlyList<SalesAuditResponse>> ListAuditAsync(ProcurementRequestContext context, string documentType, Guid id, CancellationToken cancellationToken = default)
    {
        await using var db = Create(context);
        return (await db.Audit.AsNoTracking().Where(item => item.DocumentType == documentType && item.DocumentId == id).ToListAsync(cancellationToken)).OrderByDescending(item => item.OccurredAt).Select(item => new SalesAuditResponse(item.Id, item.OperationId, item.DocumentType, item.DocumentId, item.ActorId, item.OccurredAt, item.Decision, item.Reason, item.BeforeSummary, item.AfterSummary, item.IdempotencyKey, item.CorrelationId)).ToArray();
    }

    public async Task<IReadOnlyList<SalesOrderSummaryResponse>> ListOrdersAsync(ProcurementRequestContext context, Guid? companyId, SalesOrderStatus? status, CancellationToken cancellationToken = default)
    {
        await using var db = Create(context);
        var query = ApplyTrustedScope(db.Orders.AsNoTracking(), context.TenantContext.Scope);
        if (companyId is { } company) query = query.Where(item => item.CompanyId == company);
        if (status is { } state) query = query.Where(item => item.Status == state);
        return (await query.ToListAsync(cancellationToken)).OrderByDescending(item => item.UpdatedAt).Take(500).Select(ToSummary).ToArray();
    }

    public async Task<SalesOrderResponse?> GetOrderAsync(ProcurementRequestContext context, Guid id, CancellationToken cancellationToken = default)
    {
        await using var db = Create(context);
        var entity = await ApplyTrustedScope(db.Orders.AsNoTracking(), context.TenantContext.Scope).SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        return entity is null ? null : ToResponse(entity);
    }

    public async Task<SalesOperationResult<SalesOrderResponse>> ConvertQuotationAsync(ProcurementRequestContext context, Guid quotationId, byte[] expectedVersion, string idempotencyKey, string requestFingerprint, SalesApprovalPolicyDefinition? policy, CancellationToken cancellationToken = default)
    {
        await using var db = Create(context);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var replay = await ReplayAsync<SalesOrderResponse>(db, context, "sales.quotation.convert", idempotencyKey, requestFingerprint, cancellationToken);
        if (replay is not null) return replay;
        var quote = await ApplyTrustedScope(db.Quotations, context.TenantContext.Scope).SingleOrDefaultAsync(item => item.Id == quotationId, cancellationToken);
        if (quote is null) return Failure<SalesOrderResponse>("quotation_not_found");
        if (!quote.Version.SequenceEqual(expectedVersion)) return Failure<SalesOrderResponse>("concurrency_conflict");
        if (quote.Status is not (SalesQuotationStatus.Approved or SalesQuotationStatus.Sent)) return Failure<SalesOrderResponse>("quotation_not_convertible");
        if (DateOnly.FromDateTime(DateTime.UtcNow) > quote.ValidUntil) return Failure<SalesOrderResponse>("quotation_expired");
        if (await ApplyTrustedScope(db.Orders, context.TenantContext.Scope).AnyAsync(item => item.SourceQuotationId == quote.Id && item.SourceQuotationRevision == quote.RevisionNumber, cancellationToken)) return Failure<SalesOrderResponse>("quotation_revision_already_converted");
        var now = DateTimeOffset.UtcNow;
        var quoteBefore = quote.Status;
        var order = new SalesOrderEntity(context.TenantId, quote, context.ActorId, $"SO-{now:yyyy}-{Guid.NewGuid():N}"[..17], quote.LinesJson, JsonSerializer.Serialize(policy, Json), now);
        db.Orders.Add(order);
        quote.Transition(SalesQuotationStatus.Converted, now, quote.ApprovalPolicyJson);
        await db.SaveChangesAsync(cancellationToken);
        var response = ToResponse(order);
        AddHistory(db, context, "order", order.Id, SalesHistoryAction.Created, null, order.Status, "converted-from-quotation", policy, null, Hash(response), now);
        AddHistory(db, context, "quotation", quote.Id, SalesHistoryAction.Converted, quoteBefore, quote.Status, "converted-to-order", policy, null, Hash(response), now);
        AddAudit(db, context, "sales.quotation.convert", "order", order.Id, "Allowed", null, $"source-quotation={quote.Number};revision={quote.RevisionNumber}", Summary(response), idempotencyKey, now);
        await db.SaveChangesAsync(cancellationToken);
        await SaveIdempotencyAsync(db, context, "sales.quotation.convert", idempotencyKey, requestFingerprint, "order", order.Id, response, now, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return SalesOperationResult<SalesOrderResponse>.Success(response);
    }

    public async Task<SalesOperationResult<SalesOrderResponse>> TransitionOrderAsync(ProcurementRequestContext context, Guid id, SalesOrderStatus target, string? reason, byte[] expectedVersion, string idempotencyKey, string requestFingerprint, SalesCreditEvaluation? credit, SalesApprovalPolicyDefinition? policy, Guid? delegatedFromActorId = null, CancellationToken cancellationToken = default)
    {
        await using var db = Create(context);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var replay = await ReplayAsync<SalesOrderResponse>(db, context, $"sales.order.{Action(target)}", idempotencyKey, requestFingerprint, cancellationToken);
        if (replay is not null) return replay;
        var entity = await ApplyTrustedScope(db.Orders, context.TenantContext.Scope).SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (entity is null) return Failure<SalesOrderResponse>("order_not_found");
        if (!entity.Version.SequenceEqual(expectedVersion)) return Failure<SalesOrderResponse>("concurrency_conflict");
        if (!CanTransition(entity.Status, target)) return Failure<SalesOrderResponse>("order_transition_invalid");
        var before = entity.Status;
        var now = DateTimeOffset.UtcNow;
        entity.Transition(target, reason, now, credit, JsonSerializer.Serialize(policy, Json));
        if (credit is not null) db.Credit.Add(ToCredit(entity, credit));
        await db.SaveChangesAsync(cancellationToken);
        var response = ToResponse(entity);
        AddHistory(db, context, "order", id, credit is null ? ActionToHistory(target) : SalesHistoryAction.CreditEvaluated, before, target, reason, policy, credit?.Outcome.ToString(), Hash(response), now);
        AddAudit(db, context, $"sales.order.{Action(target)}", "order", id, "Allowed", reason, $"status={before}", $"status={target};credit={credit?.Outcome}", idempotencyKey, now);
        await db.SaveChangesAsync(cancellationToken);
        await SaveIdempotencyAsync(db, context, $"sales.order.{Action(target)}", idempotencyKey, requestFingerprint, "order", id, response, now, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return SalesOperationResult<SalesOrderResponse>.Success(response);
    }

    public async Task<SalesOperationResult<SalesOrderResponse>> OverrideOrderCreditAsync(ProcurementRequestContext context, Guid id, string reason, DateTimeOffset expiresAt, string? scope, string? sourceReference, byte[] expectedVersion, string idempotencyKey, string requestFingerprint, SalesCreditEvaluation credit, CancellationToken cancellationToken = default)
    {
        await using var db = Create(context);
        await using var transaction = await db.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);
        var replay = await ReplayAsync<SalesOrderResponse>(db, context, "sales.order.credit.override", idempotencyKey, requestFingerprint, cancellationToken);
        if (replay is not null) return replay;
        var entity = await ApplyTrustedScope(db.Orders, context.TenantContext.Scope).SingleOrDefaultAsync(item => item.Id == id, cancellationToken);
        if (entity is null) return Failure<SalesOrderResponse>("order_not_found");
        if (entity.Status != SalesOrderStatus.CreditHold) return Failure<SalesOrderResponse>("credit_override_not_allowed");
        if (!entity.Version.SequenceEqual(expectedVersion)) return Failure<SalesOrderResponse>("concurrency_conflict");
        if (expiresAt <= DateTimeOffset.UtcNow) return Failure<SalesOrderResponse>("credit_override_expired");
        var now = DateTimeOffset.UtcNow;
        entity.OverrideCredit(credit with { OverrideExpiresAt = expiresAt }, now);
        db.Credit.Add(ToCredit(entity, credit with { OverrideExpiresAt = expiresAt }));
        await db.SaveChangesAsync(cancellationToken);
        var response = ToResponse(entity);
        AddHistory(db, context, "order", id, SalesHistoryAction.CreditOverridden, SalesOrderStatus.CreditHold, entity.Status, reason, null, SalesCreditOutcome.Overridden.ToString(), Hash(response), now);
        AddAudit(db, context, "sales.order.credit.override", "order", id, "Allowed", reason, "credit=hold", $"credit=overridden;expires={expiresAt:O};scope={scope};source={sourceReference}", idempotencyKey, now);
        await db.SaveChangesAsync(cancellationToken);
        await SaveIdempotencyAsync(db, context, "sales.order.credit.override", idempotencyKey, requestFingerprint, "order", id, response, now, cancellationToken);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return SalesOperationResult<SalesOrderResponse>.Success(response);
    }

    public async Task<SalesCreditResponse?> GetOrderCreditAsync(ProcurementRequestContext context, Guid id, CancellationToken cancellationToken = default)
    {
        await using var db = Create(context);
        if (!await ApplyTrustedScope(db.Orders.AsNoTracking(), context.TenantContext.Scope).AnyAsync(item => item.Id == id, cancellationToken)) return null;
        var row = (await db.Credit.AsNoTracking().Where(item => item.DocumentId == id).ToListAsync(cancellationToken)).OrderByDescending(item => item.EvaluatedAt).FirstOrDefault();
        return row is null ? null : new SalesCreditResponse(row.DocumentId, row.CustomerId, row.CompanyId, row.CurrencyCode, row.OpenReceivables, row.OverdueReceivables, row.NetReceivableExposure, row.ProposedExposure, row.CreditLimit, row.Outcome, row.Reason, row.AsOfDate, row.EvaluatedAt, row.OverrideExpiresAt);
    }

    private static IQueryable<SalesQuotationEntity> ApplyTrustedScope(IQueryable<SalesQuotationEntity> query, ScopeReference? scope)
    {
        if (scope is not { } reference) return query;
        var parts = reference.Value.Split(':', 2, StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || !Guid.TryParse(parts[1], out var id)) return query.Where(_ => false);
        return parts[0] switch
        {
            "Company" => query.Where(item => item.CompanyId == id),
            "Branch" => query.Where(item => item.BranchId == id),
            "Tenant" => query,
            _ => query.Where(_ => false)
        };
    }

    private static IQueryable<SalesOrderEntity> ApplyTrustedScope(IQueryable<SalesOrderEntity> query, ScopeReference? scope)
    {
        if (scope is not { } reference) return query;
        var parts = reference.Value.Split(':', 2, StringSplitOptions.TrimEntries);
        if (parts.Length != 2 || !Guid.TryParse(parts[1], out var id)) return query.Where(_ => false);
        return parts[0] switch
        {
            "Company" => query.Where(item => item.CompanyId == id),
            "Branch" => query.Where(item => item.BranchId == id),
            "Tenant" => query,
            _ => query.Where(_ => false)
        };
    }

    private static async Task<SalesOperationResult<T>?> ReplayAsync<T>(SalesDbContext db, ProcurementRequestContext context, string operation, string key, string fingerprint, CancellationToken cancellationToken)
    {
        var row = await db.Idempotency.AsNoTracking().SingleOrDefaultAsync(item => item.OperationId == operation && item.Key == key, cancellationToken);
        if (row is null) return null;
        if (!string.Equals(row.Fingerprint, fingerprint, StringComparison.Ordinal)) return SalesOperationResult<T>.Failure("idempotency_conflict");
        var response = JsonSerializer.Deserialize<T>(row.ResponseJson, Json);
        return response is null ? SalesOperationResult<T>.Failure("idempotency_replay_unavailable") : SalesOperationResult<T>.Success(response);
    }

    private static async Task SaveIdempotencyAsync<T>(SalesDbContext db, ProcurementRequestContext context, string operation, string key, string fingerprint, string type, Guid documentId, T response, DateTimeOffset now, CancellationToken cancellationToken)
    {
        db.Idempotency.Add(new SalesIdempotencyEntity(context.TenantId, operation, key, fingerprint, type, documentId, JsonSerializer.Serialize(response, Json), now));
        await Task.CompletedTask;
    }

    private static void AddRevision(SalesDbContext db, ProcurementRequestContext context, SalesQuotationEntity entity, SalesQuotationResponse response, string? reason, DateTimeOffset now) => db.QuotationRevisions.Add(new SalesQuotationRevisionEntity(context.TenantId, entity.Id, entity.RevisionNumber, entity.Status, JsonSerializer.Serialize(response, Json), Hash(response), context.ActorId, reason, now));
    private static void AddHistory(SalesDbContext db, ProcurementRequestContext context, string type, Guid id, SalesHistoryAction action, Enum? from, Enum? to, string? reason, SalesApprovalPolicyDefinition? policy, string? credit, string? hash, DateTimeOffset now) => db.History.Add(new SalesHistoryEntity(context.TenantId, type, id, action, from?.ToString(), to?.ToString(), context.ActorId, reason, policy?.PolicyId, policy?.Version, credit, hash, now));
    private static void AddAudit(SalesDbContext db, ProcurementRequestContext context, string operation, string type, Guid id, string decision, string? reason, string? before, string? after, string? key, DateTimeOffset now) => db.Audit.Add(new SalesAuditEntity(context.TenantId, operation, type, id, context.ActorId, now, decision, reason, before, after, key, context.CorrelationId?.Value ?? "sales"));
    private static SalesCreditEntity ToCredit(SalesOrderEntity entity, SalesCreditEvaluation credit) => new(entity.TenantId, new SalesCreditResponse(entity.Id, entity.CustomerId, entity.CompanyId, entity.CurrencyCode, credit.OpenReceivables, credit.OverdueReceivables, credit.NetReceivableExposure, credit.ProposedExposure, credit.CreditLimit, credit.Outcome, credit.Reason, credit.AsOfDate, credit.EvaluatedAt, credit.OverrideExpiresAt));
    private static string LinesJson(IReadOnlyList<SalesLineWriteModel> lines) => JsonSerializer.Serialize(lines.Select(item => new SalesQuotationLineResponse(item.Id, item.ProductId, item.ProductSku, item.ProductName, item.UnitOfMeasureId, item.UnitOfMeasureCode, item.Quantity, item.UnitPrice, item.ResolvedUnitPrice, item.DiscountPercent, item.DiscountAmount, item.TaxAmount, item.LineTotal, item.PriceListId, item.PriceVersionNumber, item.PriceEffectiveFrom, item.PriceProvenance, item.PriceSourceReference, item.ManualPriceApplied, item.CommercialAuthorityPolicyId, item.CommercialAuthorityActorId, item.CommercialAuthorityEvidence, item.Notes, item.TaxEvidence?.TaxId, item.TaxEvidence?.TaxCode, item.TaxEvidence?.RateVersionId, item.TaxEvidence?.RateVersionNumber, item.TaxEvidence?.EffectiveFrom, item.TaxEvidence?.EffectiveTo, item.TaxEvidence?.RatePercentage, item.TaxEvidence?.TaxableBase, item.TaxEvidence?.ReferenceValue)).ToArray(), Json);
    private static IReadOnlyList<SalesQuotationLineResponse> Lines(string json) => JsonSerializer.Deserialize<IReadOnlyList<SalesQuotationLineResponse>>(json, Json) ?? [];
    private static SalesQuotationResponse ToResponse(SalesQuotationEntity item) => new(item.Id, item.Number, item.TenantId.Value, item.CompanyId, item.BranchId, item.CustomerId, item.CustomerCode, item.CustomerName, item.CreatedByActorId, item.QuotationDate, item.ValidUntil, item.CurrencyId, item.CurrencyCode, item.CustomerContactId, item.Notes, item.CustomerReference, item.Subtotal, item.DiscountAmount, item.TaxAmount, item.Total, item.Status, item.RevisionNumber, Lines(item.LinesJson), item.Version, item.CreatedAt, item.UpdatedAt, DeserializeExchangeRate(item.ExchangeRateJson));
    private static SalesQuotationSummaryResponse ToSummary(SalesQuotationEntity item) => new(item.Id, item.Number, item.CompanyId, item.BranchId, item.CustomerId, item.CustomerCode, item.CustomerName, item.CreatedByActorId, item.QuotationDate, item.ValidUntil, item.CurrencyId, item.CurrencyCode, item.Subtotal, item.DiscountAmount, item.TaxAmount, item.Total, item.Status, item.RevisionNumber, item.Version, item.UpdatedAt);
    private static SalesOrderResponse ToResponse(SalesOrderEntity item) => new(item.Id, item.Number, item.TenantId.Value, item.CompanyId, item.BranchId, item.CustomerId, item.CustomerCode, item.CustomerName, item.CreatedByActorId, item.SourceQuotationId, item.SourceQuotationNumber, item.SourceQuotationRevision, item.CurrencyId, item.CurrencyCode, item.Subtotal, item.DiscountAmount, item.TaxAmount, item.Total, item.Status, item.CreditOutcome, item.CreditReason, item.CreditEvaluatedAt, item.CreditOverrideExpiresAt, Lines(item.LinesJson), item.Version, item.CreatedAt, item.UpdatedAt, DeserializeExchangeRate(item.ExchangeRateJson));
    private static SalesExchangeRateEvidence? DeserializeExchangeRate(string? json) => string.IsNullOrWhiteSpace(json) ? null : JsonSerializer.Deserialize<SalesExchangeRateEvidence>(json, Json);
    private static SalesOrderSummaryResponse ToSummary(SalesOrderEntity item) => new(item.Id, item.Number, item.CompanyId, item.BranchId, item.CustomerId, item.CustomerCode, item.CustomerName, item.CreatedByActorId, item.SourceQuotationId, item.SourceQuotationNumber, item.SourceQuotationRevision, item.CurrencyId, item.CurrencyCode, item.Total, item.Status, item.CreditOutcome, item.Version, item.UpdatedAt);
    private static string Summary(SalesQuotationResponse item) => $"quotation={item.Number};revision={item.RevisionNumber};status={item.Status};total={item.Total.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
    private static string Summary(SalesOrderResponse item) => $"order={item.Number};source={item.SourceQuotationNumber};status={item.Status};credit={item.CreditOutcome};total={item.Total.ToString(System.Globalization.CultureInfo.InvariantCulture)}";
    private static string Hash<T>(T value) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(value, Json))));
    private static SalesOperationResult<T> Failure<T>(string code) => SalesOperationResult<T>.Failure(code);
    private static string Action(Enum target) => target is SalesQuotationStatus quote ? quote switch { SalesQuotationStatus.PendingApproval => "submit", SalesQuotationStatus.Approved => "approve", SalesQuotationStatus.Rejected => "reject", SalesQuotationStatus.ReturnedForChange => "return", SalesQuotationStatus.Sent => "send", SalesQuotationStatus.Withdrawn => "withdraw", SalesQuotationStatus.Cancelled => "cancel", _ => "transition" } : target is SalesOrderStatus order ? order switch { SalesOrderStatus.PendingApproval => "submit", SalesOrderStatus.Approved => "approve", SalesOrderStatus.Rejected => "reject", SalesOrderStatus.ReturnedForChange => "return", SalesOrderStatus.Confirmed or SalesOrderStatus.CreditHold => "confirm", SalesOrderStatus.Cancelled => "cancel", _ => "transition" } : "transition";
    private static SalesHistoryAction ActionToHistory(Enum target) => target is SalesQuotationStatus quote ? quote switch { SalesQuotationStatus.PendingApproval => SalesHistoryAction.Submitted, SalesQuotationStatus.Approved => SalesHistoryAction.Approved, SalesQuotationStatus.Rejected => SalesHistoryAction.Rejected, SalesQuotationStatus.ReturnedForChange => SalesHistoryAction.ReturnedForChange, SalesQuotationStatus.Sent => SalesHistoryAction.Sent, SalesQuotationStatus.Withdrawn => SalesHistoryAction.Withdrawn, SalesQuotationStatus.Expired => SalesHistoryAction.Expired, SalesQuotationStatus.Cancelled => SalesHistoryAction.Cancelled, _ => SalesHistoryAction.Edited } : target is SalesOrderStatus order ? order switch { SalesOrderStatus.PendingApproval => SalesHistoryAction.Submitted, SalesOrderStatus.Approved => SalesHistoryAction.Approved, SalesOrderStatus.Rejected => SalesHistoryAction.Rejected, SalesOrderStatus.ReturnedForChange => SalesHistoryAction.ReturnedForChange, SalesOrderStatus.Cancelled => SalesHistoryAction.Cancelled, SalesOrderStatus.Confirmed => SalesHistoryAction.Confirmed, _ => SalesHistoryAction.Edited } : SalesHistoryAction.Edited;
    private static bool CanTransition(SalesQuotationStatus from, SalesQuotationStatus to) => (from, to) switch { (SalesQuotationStatus.Draft, SalesQuotationStatus.PendingApproval) => true, (SalesQuotationStatus.ReturnedForChange, SalesQuotationStatus.PendingApproval) => true, (SalesQuotationStatus.PendingApproval, SalesQuotationStatus.Approved or SalesQuotationStatus.Rejected or SalesQuotationStatus.ReturnedForChange) => true, (SalesQuotationStatus.Approved, SalesQuotationStatus.Sent or SalesQuotationStatus.Withdrawn or SalesQuotationStatus.Cancelled) => true, (SalesQuotationStatus.Sent, SalesQuotationStatus.Withdrawn or SalesQuotationStatus.Expired) => true, _ => false };
    private static bool CanTransition(SalesOrderStatus from, SalesOrderStatus to) => (from, to) switch { (SalesOrderStatus.Draft, SalesOrderStatus.PendingApproval) => true, (SalesOrderStatus.PendingApproval, SalesOrderStatus.Approved or SalesOrderStatus.Rejected or SalesOrderStatus.ReturnedForChange) => true, (SalesOrderStatus.Approved, SalesOrderStatus.Confirmed or SalesOrderStatus.Cancelled or SalesOrderStatus.CreditHold) => true, (SalesOrderStatus.CreditHold, SalesOrderStatus.Confirmed or SalesOrderStatus.Cancelled) => true, _ => false };
}

#pragma warning restore CS1591
