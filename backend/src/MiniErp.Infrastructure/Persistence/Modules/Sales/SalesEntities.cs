#pragma warning disable CS1591

using System.Text.Json;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.Modules.Sales;
using MiniErp.Contracts.Modules.Sales;

namespace MiniErp.Infrastructure.Persistence.Modules.Sales;

internal sealed class SalesQuotationEntity : ITenantOwned
{
    private SalesQuotationEntity() { }

    internal SalesQuotationEntity(TenantId tenantId, SalesQuotationWriteModel model, string number, string linesJson, string policyJson, DateTimeOffset now)
    {
        Id = model.Id; TenantId = tenantId; Number = number; CompanyId = model.CompanyId; BranchId = model.BranchId; CustomerId = model.CustomerId;
        CustomerCode = model.CustomerCode; CustomerName = model.CustomerName; QuotationDate = model.QuotationDate; ValidUntil = model.ValidUntil;
        CurrencyId = model.CurrencyId; CurrencyCode = model.CurrencyCode; CustomerContactId = model.CustomerContactId; Notes = model.Notes; CustomerReference = model.CustomerReference;
        Subtotal = model.Subtotal; DiscountAmount = model.DiscountAmount; TaxAmount = model.TaxAmount; Total = model.Total; LinesJson = linesJson;
        ExchangeRateJson = JsonSerializer.Serialize(model.ExchangeRateEvidence); PaymentTermJson = JsonSerializer.Serialize(model.PaymentTerm); Status = SalesQuotationStatus.Draft; RevisionNumber = 1; CreatedByActorId = Guid.Empty; CreatedAt = now; UpdatedAt = now; ApprovalPolicyJson = policyJson; Version = NewVersion();
    }

    internal Guid Id { get; private set; }
    public TenantId TenantId { get; private set; }
    internal string Number { get; private set; } = string.Empty;
    internal Guid CompanyId { get; private set; }
    internal Guid? BranchId { get; private set; }
    internal Guid CustomerId { get; private set; }
    internal string CustomerCode { get; private set; } = string.Empty;
    internal string CustomerName { get; private set; } = string.Empty;
    internal DateOnly QuotationDate { get; private set; }
    internal DateOnly ValidUntil { get; private set; }
    internal Guid CurrencyId { get; private set; }
    internal string CurrencyCode { get; private set; } = string.Empty;
    internal string? CustomerContactId { get; private set; }
    internal string? Notes { get; private set; }
    internal string? CustomerReference { get; private set; }
    internal decimal Subtotal { get; private set; }
    internal decimal DiscountAmount { get; private set; }
    internal decimal TaxAmount { get; private set; }
    internal decimal Total { get; private set; }
    internal SalesQuotationStatus Status { get; private set; }
    internal int RevisionNumber { get; private set; }
    internal Guid CreatedByActorId { get; private set; }
    internal DateTimeOffset CreatedAt { get; private set; }
    internal DateTimeOffset UpdatedAt { get; private set; }
    internal string LinesJson { get; private set; } = "[]";
    internal string? ExchangeRateJson { get; private set; }
    internal string? PaymentTermJson { get; private set; }
    internal string ApprovalPolicyJson { get; private set; } = "{}";
    internal string CurrentApprovalsJson { get; private set; } = "[]";
    internal byte[] Version { get; private set; } = [];

    internal void SetCreator(Guid actorId) => CreatedByActorId = actorId;
    internal void Edit(SalesQuotationWriteModel model, string linesJson, DateTimeOffset now, string policyJson)
    {
        if (CompanyId != model.CompanyId || BranchId != model.BranchId) throw new InvalidOperationException("quotation_scope_immutable");
        ValidUntil = model.ValidUntil; CurrencyId = model.CurrencyId; CurrencyCode = model.CurrencyCode;
        CustomerContactId = model.CustomerContactId; Notes = model.Notes; CustomerReference = model.CustomerReference; Subtotal = model.Subtotal; DiscountAmount = model.DiscountAmount; TaxAmount = model.TaxAmount; Total = model.Total; LinesJson = linesJson; ExchangeRateJson = JsonSerializer.Serialize(model.ExchangeRateEvidence); PaymentTermJson = JsonSerializer.Serialize(model.PaymentTerm); RevisionNumber++; Status = SalesQuotationStatus.Draft; ApprovalPolicyJson = policyJson; UpdatedAt = now; CurrentApprovalsJson = "[]"; Version = NewVersion();
    }
    internal void Transition(SalesQuotationStatus status, DateTimeOffset now, string policyJson, string? approvalsJson = null) { Status = status; UpdatedAt = now; ApprovalPolicyJson = policyJson; if (approvalsJson is not null) CurrentApprovalsJson = approvalsJson; Version = NewVersion(); }
    private static byte[] NewVersion() => Guid.NewGuid().ToByteArray();
}

internal sealed class SalesQuotationRevisionEntity : ITenantOwned
{
    private SalesQuotationRevisionEntity() { }
    internal SalesQuotationRevisionEntity(TenantId tenantId, Guid quotationId, int revisionNumber, SalesQuotationStatus status, string snapshotJson, string snapshotHash, Guid actorId, string? reason, DateTimeOffset now)
    { Id = Guid.NewGuid(); TenantId = tenantId; QuotationId = quotationId; RevisionNumber = revisionNumber; Status = status; SnapshotJson = snapshotJson; SnapshotHash = snapshotHash; ActorId = actorId; Reason = reason; OccurredAt = now; }
    internal Guid Id { get; private set; }
    public TenantId TenantId { get; private set; }
    internal Guid QuotationId { get; private set; }
    internal int RevisionNumber { get; private set; }
    internal SalesQuotationStatus Status { get; private set; }
    internal string SnapshotJson { get; private set; } = string.Empty;
    internal string SnapshotHash { get; private set; } = string.Empty;
    internal Guid ActorId { get; private set; }
    internal DateTimeOffset OccurredAt { get; private set; }
    internal string? Reason { get; private set; }
    internal byte[] Version { get; private set; } = [];
}

internal sealed class SalesOrderEntity : ITenantOwned
{
    private SalesOrderEntity() { }
    internal SalesOrderEntity(TenantId tenantId, SalesQuotationEntity quote, Guid actorId, string number, string linesJson, string policyJson, DateTimeOffset now)
    {
        Id = Guid.NewGuid(); TenantId = tenantId; Number = number; CompanyId = quote.CompanyId; BranchId = quote.BranchId; CustomerId = quote.CustomerId; CustomerCode = quote.CustomerCode; CustomerName = quote.CustomerName;
        SourceQuotationId = quote.Id; SourceQuotationNumber = quote.Number; SourceQuotationRevision = quote.RevisionNumber; CurrencyId = quote.CurrencyId; CurrencyCode = quote.CurrencyCode;
        Subtotal = quote.Subtotal; DiscountAmount = quote.DiscountAmount; TaxAmount = quote.TaxAmount; Total = quote.Total; LinesJson = linesJson; ExchangeRateJson = quote.ExchangeRateJson; PaymentTermJson = quote.PaymentTermJson; Status = SalesOrderStatus.Draft; CreditOutcome = SalesCreditOutcome.Unknown; ApprovalPolicyJson = policyJson;
        CreatedByActorId = actorId; CreatedAt = now; UpdatedAt = now; RevisionNumber = 1; Version = Guid.NewGuid().ToByteArray();
    }
    internal Guid Id { get; private set; }
    public TenantId TenantId { get; private set; }
    internal string Number { get; private set; } = string.Empty;
    internal Guid CompanyId { get; private set; }
    internal Guid? BranchId { get; private set; }
    internal Guid CustomerId { get; private set; }
    internal string CustomerCode { get; private set; } = string.Empty;
    internal string CustomerName { get; private set; } = string.Empty;
    internal Guid SourceQuotationId { get; private set; }
    internal string SourceQuotationNumber { get; private set; } = string.Empty;
    internal int SourceQuotationRevision { get; private set; }
    internal Guid CurrencyId { get; private set; }
    internal string CurrencyCode { get; private set; } = string.Empty;
    internal decimal Subtotal { get; private set; }
    internal decimal DiscountAmount { get; private set; }
    internal decimal TaxAmount { get; private set; }
    internal decimal Total { get; private set; }
    internal SalesOrderStatus Status { get; private set; }
    internal SalesCreditOutcome CreditOutcome { get; private set; }
    internal string? CreditReason { get; private set; }
    internal DateTimeOffset? CreditEvaluatedAt { get; private set; }
    internal DateTimeOffset? CreditOverrideExpiresAt { get; private set; }
    internal string LinesJson { get; private set; } = "[]";
    internal string? ExchangeRateJson { get; private set; }
    internal string? PaymentTermJson { get; private set; }
    internal string ApprovalPolicyJson { get; private set; } = "{}";
    internal string CurrentApprovalsJson { get; private set; } = "[]";
    internal Guid CreatedByActorId { get; private set; }
    internal DateTimeOffset CreatedAt { get; private set; }
    internal DateTimeOffset UpdatedAt { get; private set; }
    internal int RevisionNumber { get; private set; }
    internal byte[] Version { get; private set; } = [];
    internal void Edit(SalesQuotationWriteModel model, string linesJson, DateTimeOffset now)
    {
        if (CompanyId != model.CompanyId || BranchId != model.BranchId || CustomerId != model.CustomerId) throw new InvalidOperationException("order_scope_immutable");
        CurrencyId = model.CurrencyId; CurrencyCode = model.CurrencyCode; Subtotal = model.Subtotal; DiscountAmount = model.DiscountAmount; TaxAmount = model.TaxAmount;
        Total = model.Total; LinesJson = linesJson; ExchangeRateJson = JsonSerializer.Serialize(model.ExchangeRateEvidence); PaymentTermJson = JsonSerializer.Serialize(model.PaymentTerm); RevisionNumber++; Status = SalesOrderStatus.Draft;
        CreditOutcome = SalesCreditOutcome.Unknown; CreditReason = null; CreditEvaluatedAt = null; CreditOverrideExpiresAt = null; CurrentApprovalsJson = "[]"; UpdatedAt = now; Version = Guid.NewGuid().ToByteArray();
    }
    internal void Transition(SalesOrderStatus status, string? reason, DateTimeOffset now, SalesCreditEvaluation? credit, string policyJson, string? approvalsJson = null)
    { Status = status; CreditReason = reason ?? credit?.Reason; CreditOutcome = credit?.Outcome ?? CreditOutcome; CreditEvaluatedAt = credit is null ? CreditEvaluatedAt : credit.EvaluatedAt; CreditOverrideExpiresAt = credit?.OverrideExpiresAt ?? CreditOverrideExpiresAt; UpdatedAt = now; ApprovalPolicyJson = policyJson; if (approvalsJson is not null) CurrentApprovalsJson = approvalsJson; Version = Guid.NewGuid().ToByteArray(); }
    internal void OverrideCredit(SalesCreditEvaluation credit, DateTimeOffset now) { CreditOutcome = SalesCreditOutcome.Overridden; CreditReason = credit.Reason; CreditEvaluatedAt = credit.EvaluatedAt; CreditOverrideExpiresAt = credit.OverrideExpiresAt; Status = SalesOrderStatus.Approved; UpdatedAt = now; Version = Guid.NewGuid().ToByteArray(); }
}

internal sealed class SalesHistoryEntity : ITenantOwned
{
    private SalesHistoryEntity() { }
    internal SalesHistoryEntity(TenantId tenantId, string type, Guid documentId, SalesHistoryAction action, string? from, string? to, Guid actorId, string? reason, string? policyId, int? policyVersion, string? credit, string? hash, DateTimeOffset now, string? snapshotJson = null)
    { Id = Guid.NewGuid(); TenantId = tenantId; DocumentType = type; DocumentId = documentId; Action = action; FromStatus = from; ToStatus = to; ActorId = actorId; Reason = reason; PolicyId = policyId; PolicyVersion = policyVersion; CreditOutcome = credit; SnapshotHash = hash; SnapshotJson = snapshotJson; OccurredAt = now; }
    internal Guid Id { get; private set; }
    public TenantId TenantId { get; private set; }
    internal string DocumentType { get; private set; } = string.Empty;
    internal Guid DocumentId { get; private set; }
    internal SalesHistoryAction Action { get; private set; }
    internal string? FromStatus { get; private set; }
    internal string? ToStatus { get; private set; }
    internal Guid ActorId { get; private set; }
    internal DateTimeOffset OccurredAt { get; private set; }
    internal string? Reason { get; private set; }
    internal string? PolicyId { get; private set; }
    internal int? PolicyVersion { get; private set; }
    internal string? CreditOutcome { get; private set; }
    internal string? SnapshotHash { get; private set; }
    internal string? SnapshotJson { get; private set; }
    internal byte[] Version { get; private set; } = [];
}

internal sealed class SalesAuditEntity : ITenantOwned
{
    private SalesAuditEntity() { }
    internal SalesAuditEntity(TenantId tenantId, string operationId, string type, Guid documentId, Guid actorId, DateTimeOffset now, string decision, string? reason, string? before, string? after, string? key, string correlation)
    { Id = Guid.NewGuid(); TenantId = tenantId; OperationId = operationId; DocumentType = type; DocumentId = documentId; ActorId = actorId; OccurredAt = now; Decision = decision; Reason = reason; BeforeSummary = before; AfterSummary = after; IdempotencyKey = key; CorrelationId = correlation; }
    internal Guid Id { get; private set; }
    public TenantId TenantId { get; private set; }
    internal string OperationId { get; private set; } = string.Empty;
    internal string DocumentType { get; private set; } = string.Empty;
    internal Guid DocumentId { get; private set; }
    internal Guid ActorId { get; private set; }
    internal DateTimeOffset OccurredAt { get; private set; }
    internal string Decision { get; private set; } = string.Empty;
    internal string? Reason { get; private set; }
    internal string? BeforeSummary { get; private set; }
    internal string? AfterSummary { get; private set; }
    internal string? IdempotencyKey { get; private set; }
    internal string CorrelationId { get; private set; } = string.Empty;
    internal byte[] Version { get; private set; } = [];
}

internal sealed class SalesIdempotencyEntity : ITenantOwned
{
    private SalesIdempotencyEntity() { }
    internal SalesIdempotencyEntity(TenantId tenantId, string operationId, string key, string fingerprint, string type, Guid documentId, string responseJson, DateTimeOffset now)
    { Id = Guid.NewGuid(); TenantId = tenantId; OperationId = operationId; Key = key; Fingerprint = fingerprint; DocumentType = type; DocumentId = documentId; ResponseJson = responseJson; CreatedAt = now; }
    internal Guid Id { get; private set; }
    public TenantId TenantId { get; private set; }
    internal string OperationId { get; private set; } = string.Empty;
    internal string Key { get; private set; } = string.Empty;
    internal string Fingerprint { get; private set; } = string.Empty;
    internal string DocumentType { get; private set; } = string.Empty;
    internal Guid DocumentId { get; private set; }
    internal string ResponseJson { get; private set; } = string.Empty;
    internal DateTimeOffset CreatedAt { get; private set; }
    internal byte[] Version { get; private set; } = [];
}

internal sealed class SalesCreditEntity : ITenantOwned
{
    private SalesCreditEntity() { }
    internal SalesCreditEntity(TenantId tenantId, SalesCreditResponse response)
    { Id = Guid.NewGuid(); TenantId = tenantId; DocumentId = response.DocumentId; CustomerId = response.CustomerId; CompanyId = response.CompanyId; CurrencyCode = response.CurrencyCode; TransactionCurrencyCode = response.TransactionCurrencyCode; TransactionAmount = response.TransactionAmount; ConvertedOrderCommitment = response.ConvertedOrderCommitment; ExchangeRateJson = JsonSerializer.Serialize(response.ExchangeRateEvidence); OrderRevisionNumber = response.OrderRevisionNumber; OpenReceivables = response.OpenReceivables; OverdueReceivables = response.OverdueReceivables; NetReceivableExposure = response.NetReceivableExposure; ProposedExposure = response.ProposedExposure; CreditLimit = response.CreditLimit; Outcome = response.Outcome; Reason = response.Reason; AsOfDate = response.AsOfDate; EvaluatedAt = response.EvaluatedAt; OverrideExpiresAt = response.OverrideExpiresAt; }
    internal Guid Id { get; private set; }
    public TenantId TenantId { get; private set; }
    internal Guid DocumentId { get; private set; }
    internal Guid CustomerId { get; private set; }
    internal Guid CompanyId { get; private set; }
    internal string? CurrencyCode { get; private set; }
    internal string? TransactionCurrencyCode { get; private set; }
    internal decimal? TransactionAmount { get; private set; }
    internal decimal? ConvertedOrderCommitment { get; private set; }
    internal string? ExchangeRateJson { get; private set; }
    internal int? OrderRevisionNumber { get; private set; }
    internal decimal? OpenReceivables { get; private set; }
    internal decimal? OverdueReceivables { get; private set; }
    internal decimal? NetReceivableExposure { get; private set; }
    internal decimal? ProposedExposure { get; private set; }
    internal decimal? CreditLimit { get; private set; }
    internal SalesCreditOutcome Outcome { get; private set; }
    internal string? Reason { get; private set; }
    internal DateOnly AsOfDate { get; private set; }
    internal DateTimeOffset EvaluatedAt { get; private set; }
    internal DateTimeOffset? OverrideExpiresAt { get; private set; }
    internal byte[] Version { get; private set; } = [];
}

internal sealed class SalesDeliveryEntity : ITenantOwned
{
    private SalesDeliveryEntity() { }
    internal SalesDeliveryEntity(TenantId tenantId, Guid id, Guid orderId, int orderRevision, Guid companyId, Guid? branchId, Guid customerId, Guid warehouseId, string linesJson, string snapshotJson, Guid actorId, string? idempotencyKey, DateTimeOffset at)
    { Id = id; TenantId = tenantId; OrderId = orderId; OrderRevisionNumber = orderRevision; CompanyId = companyId; BranchId = branchId; CustomerId = customerId; WarehouseId = warehouseId; LinesJson = linesJson; SourceSnapshotJson = snapshotJson; HandoffJson = JsonSerializer.Serialize(new SalesHandoffEvidence("inventory.sales-delivery.post", [], "NotCommitted", "NotAcknowledged", "Pending", null, 0, null, idempotencyKey ?? string.Empty)); ActorId = actorId; IdempotencyKey = idempotencyKey; Status = SalesDeliveryStatus.Draft; CreatedAt = at; Version = NewVersion(); }
    internal Guid Id { get; private set; }
    public TenantId TenantId { get; private set; }
    internal Guid OrderId { get; private set; }
    internal int OrderRevisionNumber { get; private set; }
    internal Guid CompanyId { get; private set; }
    internal Guid? BranchId { get; private set; }
    internal Guid CustomerId { get; private set; }
    internal Guid WarehouseId { get; private set; }
    internal SalesDeliveryStatus Status { get; private set; }
    internal string? ErrorCode { get; private set; }
    internal string LinesJson { get; private set; } = "[]";
    internal string SourceSnapshotJson { get; private set; } = "{}";
    internal string HandoffJson { get; private set; } = "{}";
    internal string MovementIdsJson { get; private set; } = "[]";
    internal Guid ActorId { get; private set; }
    internal string? IdempotencyKey { get; private set; }
    internal DateTimeOffset CreatedAt { get; private set; }
    internal DateTimeOffset? PostedAt { get; private set; }
    internal byte[] Version { get; private set; } = [];
    internal void Posted(IEnumerable<Guid> movements, DateTimeOffset at) { var ids = movements.Distinct().ToArray(); MovementIdsJson = JsonSerializer.Serialize(ids); Status = SalesDeliveryStatus.Posted; PostedAt = at; ErrorCode = null; HandoffJson = JsonSerializer.Serialize(Handoff() with { DownstreamEffectIds = ids, DownstreamCommitState = "Committed", SalesAcknowledgementState = "Acknowledged", ReconciliationStatus = "Reconciled", LastError = null, AttemptCount = Handoff().AttemptCount + 1, LastAttemptAt = at }); Version = NewVersion(); }
    internal void Fail(string code, bool unknown) { Status = unknown ? SalesDeliveryStatus.Unknown : SalesDeliveryStatus.Failed; ErrorCode = code; var handoff = Handoff(); HandoffJson = JsonSerializer.Serialize(handoff with { DownstreamCommitState = unknown ? "Unknown" : handoff.DownstreamCommitState, ReconciliationStatus = unknown ? "Required" : "NotRequired", LastError = code, AttemptCount = handoff.AttemptCount + 1, LastAttemptAt = DateTimeOffset.UtcNow }); Version = NewVersion(); }
    private SalesHandoffEvidence Handoff() => JsonSerializer.Deserialize<SalesHandoffEvidence>(HandoffJson) ?? new("inventory.sales-delivery.post", [], "Unknown", "NotAcknowledged", "Required", null, 0, null, IdempotencyKey ?? string.Empty);
    private static byte[] NewVersion() => Guid.NewGuid().ToByteArray();
}

internal sealed class SalesInvoiceRequestEntity : ITenantOwned
{
    private SalesInvoiceRequestEntity() { }
    internal SalesInvoiceRequestEntity(TenantId tenantId, Guid id, Guid orderId, int orderRevision, Guid? deliveryId, Guid companyId, Guid? branchId, Guid customerId, DateOnly invoiceDate, string linesJson, decimal amount, string currencyCode, string snapshotJson, Guid actorId, string? idempotencyKey, DateTimeOffset at, string? paymentTermJson = null)
    { Id = id; TenantId = tenantId; OrderId = orderId; OrderRevisionNumber = orderRevision; DeliveryId = deliveryId; CompanyId = companyId; BranchId = branchId; CustomerId = customerId; InvoiceDate = invoiceDate; LinesJson = linesJson; Amount = amount; CurrencyCode = currencyCode; SourceSnapshotJson = snapshotJson; PaymentTermJson = paymentTermJson; HandoffJson = JsonSerializer.Serialize(new SalesHandoffEvidence("finance.sales-invoice.create", [], "NotCommitted", "NotAcknowledged", "Pending", null, 0, null, idempotencyKey ?? string.Empty)); ActorId = actorId; IdempotencyKey = idempotencyKey; Status = SalesInvoiceRequestStatus.Pending; CreatedAt = at; Version = NewVersion(); }
    internal Guid Id { get; private set; }
    public TenantId TenantId { get; private set; }
    internal Guid OrderId { get; private set; }
    internal int OrderRevisionNumber { get; private set; }
    internal Guid? DeliveryId { get; private set; }
    internal Guid CompanyId { get; private set; }
    internal Guid? BranchId { get; private set; }
    internal Guid CustomerId { get; private set; }
    internal DateOnly InvoiceDate { get; private set; }
    internal string LinesJson { get; private set; } = "[]";
    internal decimal Amount { get; private set; }
    internal string CurrencyCode { get; private set; } = string.Empty;
    internal string SourceSnapshotJson { get; private set; } = "{}";
    internal string? PaymentTermJson { get; private set; }
    internal string HandoffJson { get; private set; } = "{}";
    internal SalesInvoiceRequestStatus Status { get; private set; }
    internal string? ErrorCode { get; private set; }
    internal Guid? FinanceOpenItemId { get; private set; }
    internal Guid ActorId { get; private set; }
    internal string? IdempotencyKey { get; private set; }
    internal DateTimeOffset CreatedAt { get; private set; }
    internal DateTimeOffset? PostedAt { get; private set; }
    internal byte[] Version { get; private set; } = [];
    internal void Posted(Guid financeOpenItemId, DateTimeOffset at) { FinanceOpenItemId = financeOpenItemId; Status = SalesInvoiceRequestStatus.Posted; PostedAt = at; ErrorCode = null; HandoffJson = JsonSerializer.Serialize(Handoff() with { DownstreamEffectIds = [financeOpenItemId], DownstreamCommitState = "Committed", SalesAcknowledgementState = "Acknowledged", ReconciliationStatus = "Reconciled", LastError = null, AttemptCount = Handoff().AttemptCount + 1, LastAttemptAt = at }); Version = NewVersion(); }
    internal void Fail(string code, bool unknown) { Status = unknown ? SalesInvoiceRequestStatus.Unknown : SalesInvoiceRequestStatus.Failed; ErrorCode = code; var handoff = Handoff(); HandoffJson = JsonSerializer.Serialize(handoff with { DownstreamCommitState = unknown ? "Unknown" : handoff.DownstreamCommitState, ReconciliationStatus = unknown ? "Required" : "NotRequired", LastError = code, AttemptCount = handoff.AttemptCount + 1, LastAttemptAt = DateTimeOffset.UtcNow }); Version = NewVersion(); }
    private SalesHandoffEvidence Handoff() => JsonSerializer.Deserialize<SalesHandoffEvidence>(HandoffJson) ?? new("finance.sales-invoice.create", [], "Unknown", "NotAcknowledged", "Required", null, 0, null, IdempotencyKey ?? string.Empty);
    private static byte[] NewVersion() => Guid.NewGuid().ToByteArray();
}

#pragma warning restore CS1591
