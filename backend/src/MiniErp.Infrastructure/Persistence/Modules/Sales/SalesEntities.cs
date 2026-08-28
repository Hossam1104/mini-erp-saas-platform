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
        ExchangeRateJson = JsonSerializer.Serialize(model.ExchangeRateEvidence); Status = SalesQuotationStatus.Draft; RevisionNumber = 1; CreatedByActorId = Guid.Empty; CreatedAt = now; UpdatedAt = now; ApprovalPolicyJson = policyJson; Version = NewVersion();
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
    internal string ApprovalPolicyJson { get; private set; } = "{}";
    internal string CurrentApprovalsJson { get; private set; } = "[]";
    internal byte[] Version { get; private set; } = [];

    internal void SetCreator(Guid actorId) => CreatedByActorId = actorId;
    internal void Edit(SalesQuotationWriteModel model, string linesJson, DateTimeOffset now)
    {
        CompanyId = model.CompanyId; BranchId = model.BranchId; ValidUntil = model.ValidUntil; CurrencyId = model.CurrencyId; CurrencyCode = model.CurrencyCode;
        CustomerContactId = model.CustomerContactId; Notes = model.Notes; CustomerReference = model.CustomerReference; Subtotal = model.Subtotal; DiscountAmount = model.DiscountAmount; TaxAmount = model.TaxAmount; Total = model.Total; LinesJson = linesJson; ExchangeRateJson = JsonSerializer.Serialize(model.ExchangeRateEvidence); RevisionNumber++; Status = SalesQuotationStatus.Draft; UpdatedAt = now; CurrentApprovalsJson = "[]"; Version = NewVersion();
    }
    internal void Transition(SalesQuotationStatus status, DateTimeOffset now, string policyJson) { Status = status; UpdatedAt = now; ApprovalPolicyJson = policyJson; Version = NewVersion(); }
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
        Subtotal = quote.Subtotal; DiscountAmount = quote.DiscountAmount; TaxAmount = quote.TaxAmount; Total = quote.Total; LinesJson = linesJson; ExchangeRateJson = quote.ExchangeRateJson; Status = SalesOrderStatus.Draft; CreditOutcome = SalesCreditOutcome.Unknown; ApprovalPolicyJson = policyJson;
        CreatedByActorId = actorId; CreatedAt = now; UpdatedAt = now; Version = Guid.NewGuid().ToByteArray();
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
    internal string ApprovalPolicyJson { get; private set; } = "{}";
    internal Guid CreatedByActorId { get; private set; }
    internal DateTimeOffset CreatedAt { get; private set; }
    internal DateTimeOffset UpdatedAt { get; private set; }
    internal byte[] Version { get; private set; } = [];
    internal void Transition(SalesOrderStatus status, string? reason, DateTimeOffset now, SalesCreditEvaluation? credit, string policyJson)
    { Status = status; CreditReason = reason ?? credit?.Reason; CreditOutcome = credit?.Outcome ?? CreditOutcome; CreditEvaluatedAt = credit is null ? CreditEvaluatedAt : credit.EvaluatedAt; CreditOverrideExpiresAt = credit?.OverrideExpiresAt ?? CreditOverrideExpiresAt; UpdatedAt = now; ApprovalPolicyJson = policyJson; Version = Guid.NewGuid().ToByteArray(); }
    internal void OverrideCredit(SalesCreditEvaluation credit, DateTimeOffset now) { CreditOutcome = SalesCreditOutcome.Overridden; CreditReason = credit.Reason; CreditEvaluatedAt = credit.EvaluatedAt; CreditOverrideExpiresAt = credit.OverrideExpiresAt; Status = SalesOrderStatus.Approved; UpdatedAt = now; Version = Guid.NewGuid().ToByteArray(); }
}

internal sealed class SalesHistoryEntity : ITenantOwned
{
    private SalesHistoryEntity() { }
    internal SalesHistoryEntity(TenantId tenantId, string type, Guid documentId, SalesHistoryAction action, string? from, string? to, Guid actorId, string? reason, string? policyId, int? policyVersion, string? credit, string? hash, DateTimeOffset now)
    { Id = Guid.NewGuid(); TenantId = tenantId; DocumentType = type; DocumentId = documentId; Action = action; FromStatus = from; ToStatus = to; ActorId = actorId; Reason = reason; PolicyId = policyId; PolicyVersion = policyVersion; CreditOutcome = credit; SnapshotHash = hash; OccurredAt = now; }
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
    { Id = Guid.NewGuid(); TenantId = tenantId; DocumentId = response.DocumentId; CustomerId = response.CustomerId; CompanyId = response.CompanyId; CurrencyCode = response.CurrencyCode; OpenReceivables = response.OpenReceivables; OverdueReceivables = response.OverdueReceivables; NetReceivableExposure = response.NetReceivableExposure; ProposedExposure = response.ProposedExposure; CreditLimit = response.CreditLimit; Outcome = response.Outcome; Reason = response.Reason; AsOfDate = response.AsOfDate; EvaluatedAt = response.EvaluatedAt; OverrideExpiresAt = response.OverrideExpiresAt; }
    internal Guid Id { get; private set; }
    public TenantId TenantId { get; private set; }
    internal Guid DocumentId { get; private set; }
    internal Guid CustomerId { get; private set; }
    internal Guid CompanyId { get; private set; }
    internal string CurrencyCode { get; private set; } = string.Empty;
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

#pragma warning restore CS1591
