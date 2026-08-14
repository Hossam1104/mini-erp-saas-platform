using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.Modules.MasterData;
using MiniErp.Contracts.Modules.MasterData;

namespace MiniErp.Infrastructure.Persistence.Modules.MasterData;

/// <summary>
/// Module-owned durable tracking for the structured Master Data import seam.
/// These entities deliberately do not duplicate any business master entity.
/// </summary>
internal sealed class MasterDataImportBatchEntity : ITenantOwned
{
    private MasterDataImportBatchEntity()
    {
        SourceSystemCategory = string.Empty;
        CorrelationId = string.Empty;
        Fingerprint = string.Empty;
    }

    internal MasterDataImportBatchEntity(
        Guid id,
        TenantId tenantId,
        MasterDataResourceKind resourceKind,
        string sourceSystemCategory,
        string? sourceFileReference,
        string? batchReference,
        MasterDataImportDuplicatePolicy duplicatePolicy,
        MasterDataImportMode mode,
        Guid submittedActorId,
        DateTimeOffset createdAt,
        string correlationId,
        string? idempotencyKey,
        string fingerprint,
        int totalRows)
    {
        Id = id;
        TenantId = tenantId;
        ResourceKind = resourceKind;
        SourceSystemCategory = sourceSystemCategory;
        SourceFileReference = sourceFileReference;
        BatchReference = batchReference;
        DuplicatePolicy = duplicatePolicy;
        Mode = mode;
        Status = MasterDataImportStatus.Draft;
        SubmittedActorId = submittedActorId;
        CreatedAt = createdAt;
        CorrelationId = correlationId;
        IdempotencyKey = idempotencyKey;
        Fingerprint = fingerprint;
        TotalRows = totalRows;
    }

    internal Guid Id { get; private set; }

    public TenantId TenantId { get; private set; }

    internal MasterDataResourceKind ResourceKind { get; private set; }

    internal string SourceSystemCategory { get; private set; }

    internal string? SourceFileReference { get; private set; }

    internal string? BatchReference { get; private set; }

    internal MasterDataImportDuplicatePolicy DuplicatePolicy { get; private set; }

    internal MasterDataImportMode Mode { get; private set; }

    internal MasterDataImportStatus Status { get; private set; }

    internal Guid SubmittedActorId { get; private set; }

    internal DateTimeOffset CreatedAt { get; private set; }

    internal DateTimeOffset? StartedAt { get; private set; }

    internal DateTimeOffset? CompletedAt { get; private set; }

    internal string CorrelationId { get; private set; }

    internal int TotalRows { get; private set; }

    internal int AcceptedCount { get; private set; }

    internal int RejectedCount { get; private set; }

    internal int QuarantinedCount { get; private set; }

    internal int CommittedCount { get; private set; }

    internal int SkippedCount { get; private set; }

    internal int FailedCount { get; private set; }

    internal string? IdempotencyKey { get; private set; }

    internal string Fingerprint { get; private set; }

    internal byte[] Version { get; private set; } = Guid.NewGuid().ToByteArray();

    internal void ApplyResult(
        MasterDataImportStatus status,
        DateTimeOffset? startedAt,
        DateTimeOffset? completedAt,
        MasterDataImportReconciliation reconciliation,
        string correlationId,
        string fingerprint)
    {
        Status = status;
        StartedAt = startedAt;
        CompletedAt = completedAt;
        TotalRows = reconciliation.TotalRows;
        AcceptedCount = reconciliation.Accepted;
        RejectedCount = reconciliation.Rejected;
        QuarantinedCount = reconciliation.Quarantined;
        CommittedCount = reconciliation.Committed;
        SkippedCount = reconciliation.Skipped;
        FailedCount = reconciliation.Failed;
        CorrelationId = correlationId;
        Fingerprint = fingerprint;
        Version = Guid.NewGuid().ToByteArray();
    }
}

internal sealed class MasterDataImportRowEntity : ITenantOwned
{
    private MasterDataImportRowEntity()
    {
        SourceFieldsJson = "{}";
        NormalizedFieldsJson = "{}";
        DiagnosticsJson = "[]";
    }

    internal MasterDataImportRowEntity(
        Guid id,
        Guid batchId,
        TenantId tenantId,
        int originalRowNumber,
        int replaySequence,
        bool isCurrent,
        MasterDataResourceKind resourceKind,
        string sourceFieldsJson,
        string normalizedFieldsJson,
        MasterDataImportRowOutcome outcome,
        string diagnosticsJson,
        MasterDataImportDiagnosticSeverity highestSeverity,
        MasterDataImportMutationDisposition mutationDisposition,
        Guid? resultingResourceId,
        string? resultingResourceCode,
        byte[]? expectedResourceVersion,
        Guid? replayOfRowId,
        Guid? originalRowId,
        string? replayIdempotencyKey,
        DateTimeOffset processedAt)
    {
        Id = id;
        BatchId = batchId;
        TenantId = tenantId;
        OriginalRowNumber = originalRowNumber;
        ReplaySequence = replaySequence;
        IsCurrent = isCurrent;
        ResourceKind = resourceKind;
        SourceFieldsJson = sourceFieldsJson;
        NormalizedFieldsJson = normalizedFieldsJson;
        Outcome = outcome;
        DiagnosticsJson = diagnosticsJson;
        HighestSeverity = highestSeverity;
        MutationDisposition = mutationDisposition;
        ResultingResourceId = resultingResourceId;
        ResultingResourceCode = resultingResourceCode;
        ExpectedResourceVersion = expectedResourceVersion;
        ReplayOfRowId = replayOfRowId;
        OriginalRowId = originalRowId;
        ReplayIdempotencyKey = replayIdempotencyKey;
        ProcessedAt = processedAt;
    }

    internal Guid Id { get; private set; }

    public TenantId TenantId { get; private set; }

    internal Guid BatchId { get; private set; }

    internal int OriginalRowNumber { get; private set; }

    internal int ReplaySequence { get; private set; }

    internal bool IsCurrent { get; private set; }

    internal MasterDataResourceKind ResourceKind { get; private set; }

    internal string SourceFieldsJson { get; private set; }

    internal string NormalizedFieldsJson { get; private set; }

    internal MasterDataImportRowOutcome Outcome { get; private set; }

    internal string DiagnosticsJson { get; private set; }

    internal MasterDataImportDiagnosticSeverity HighestSeverity { get; private set; }

    internal MasterDataImportMutationDisposition MutationDisposition { get; private set; }

    internal Guid? ResultingResourceId { get; private set; }

    internal string? ResultingResourceCode { get; private set; }

    internal byte[]? ExpectedResourceVersion { get; private set; }

    internal Guid? ReplayOfRowId { get; private set; }

    internal Guid? OriginalRowId { get; private set; }

    internal string? ReplayIdempotencyKey { get; private set; }

    internal DateTimeOffset ProcessedAt { get; private set; }

    internal byte[] Version { get; private set; } = Guid.NewGuid().ToByteArray();

    internal void Apply(
        MasterDataImportRowRecord row,
        string sourceFieldsJson,
        string normalizedFieldsJson,
        string diagnosticsJson)
    {
        BatchId = row.BatchId;
        TenantId = row.TenantId;
        OriginalRowNumber = row.OriginalRowNumber;
        ReplaySequence = row.ReplaySequence;
        IsCurrent = row.IsCurrent;
        ResourceKind = row.ResourceKind;
        SourceFieldsJson = sourceFieldsJson;
        NormalizedFieldsJson = normalizedFieldsJson;
        Outcome = row.Outcome;
        DiagnosticsJson = diagnosticsJson;
        HighestSeverity = row.HighestSeverity;
        MutationDisposition = row.MutationDisposition;
        ResultingResourceId = row.ResultingResourceId;
        ResultingResourceCode = row.ResultingResourceCode;
        ExpectedResourceVersion = row.ExpectedResourceVersion;
        ReplayOfRowId = row.ReplayOfRowId;
        OriginalRowId = row.OriginalRowId;
        ReplayIdempotencyKey = row.ReplayIdempotencyKey;
        ProcessedAt = row.ProcessedAt;
        Version = Guid.NewGuid().ToByteArray();
    }
}

internal sealed class MasterDataImportAuditEntity : ITenantOwned
{
    private MasterDataImportAuditEntity()
    {
        OperationId = string.Empty;
        CorrelationId = string.Empty;
        Outcome = string.Empty;
        SourceReference = string.Empty;
    }

    internal MasterDataImportAuditEntity(MasterDataImportAuditRecord audit)
    {
        EvidenceId = audit.EvidenceId;
        TenantId = audit.TenantId;
        BatchId = audit.BatchId;
        RowId = audit.RowId;
        OriginalRowNumber = audit.OriginalRowNumber;
        OccurredAt = audit.OccurredAt;
        OperationId = audit.OperationId;
        CorrelationId = audit.CorrelationId;
        ActorId = audit.ActorId;
        ResourceKind = audit.ResourceKind;
        Outcome = audit.Outcome;
        SourceReference = audit.SourceReference;
        Detail = audit.Detail;
    }

    internal Guid EvidenceId { get; private set; }

    internal Guid Id => EvidenceId;

    public TenantId TenantId { get; private set; }

    internal Guid BatchId { get; private set; }

    internal Guid? RowId { get; private set; }

    internal int? OriginalRowNumber { get; private set; }

    internal DateTimeOffset OccurredAt { get; private set; }

    internal string OperationId { get; private set; }

    internal string CorrelationId { get; private set; }

    internal Guid ActorId { get; private set; }

    internal MasterDataResourceKind ResourceKind { get; private set; }

    internal string Outcome { get; private set; }

    internal string SourceReference { get; private set; }

    internal string? Detail { get; private set; }

    internal byte[] Version { get; private set; } = Guid.NewGuid().ToByteArray();
}
