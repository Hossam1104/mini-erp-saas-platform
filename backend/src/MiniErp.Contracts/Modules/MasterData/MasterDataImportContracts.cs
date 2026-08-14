#pragma warning disable CS1591

using System.Text.Json.Serialization;

namespace MiniErp.Contracts.Modules.MasterData;

/// <summary>Execution mode requested for a structured import batch.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MasterDataImportMode
{
    DryRun = 1,
    Commit = 2
}

/// <summary>Durable lifecycle of an import batch.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MasterDataImportStatus
{
    Draft = 1,
    Simulating = 2,
    Validated = 3,
    Completed = 4,
    CompletedWithErrors = 5,
    Failed = 6
}

/// <summary>Requested handling for an existing business identity.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MasterDataImportDuplicatePolicy
{
    Reject = 1,
    SkipExisting = 2,
    UpdateMutableFields = 3
}

/// <summary>Current outcome of one original source row or corrected replay.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MasterDataImportRowOutcome
{
    NotProcessed = 1,
    Accepted = 2,
    Rejected = 3,
    Quarantined = 4
}

/// <summary>Safe severity for a row diagnostic.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MasterDataImportDiagnosticSeverity
{
    Info = 1,
    Warning = 2,
    Error = 3
}

/// <summary>What the execution engine did with an accepted row.</summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MasterDataImportMutationDisposition
{
    NotAttempted = 1,
    Eligible = 2,
    SkippedExisting = 3,
    Committed = 4,
    Updated = 5,
    Failed = 6
}

/// <summary>
/// Generic source context. It deliberately contains no credentials, provider
/// selection, retention promise, or customer-specific legacy identifier.
/// </summary>
public sealed record MasterDataImportSourceRequest(
    string? SourceSystemCategory,
    string? SourceFileReference,
    string? BatchReference);

/// <summary>
/// A structured, already-normalized-at-the-file-boundary row. File parsing is
/// intentionally outside this Phase-A contract.
/// </summary>
public sealed record MasterDataImportRowInput(
    int RowNumber,
    IReadOnlyDictionary<string, string?>? Fields);

/// <summary>Structured batch creation contract for the backend import seam.</summary>
public sealed record MasterDataImportBatchRequest(
    MasterDataResourceKind ResourceKind,
    MasterDataImportSourceRequest? Source,
    string? SourceFileReference,
    MasterDataImportDuplicatePolicy DuplicatePolicy,
    MasterDataImportMode Mode,
    IReadOnlyList<MasterDataImportRowInput>? Rows);

/// <summary>Corrected values for one quarantined row replay.</summary>
public sealed record MasterDataImportReplayRequest(
    IReadOnlyDictionary<string, string?>? CorrectedFields);

public sealed record MasterDataImportDiagnosticResponse(
    string Code,
    string Message,
    string? Field,
    MasterDataImportDiagnosticSeverity Severity);

public sealed record MasterDataImportBatchResponse(
    Guid Id,
    Guid TenantId,
    MasterDataResourceKind ResourceKind,
    MasterDataImportSourceRequest Source,
    MasterDataImportDuplicatePolicy DuplicatePolicy,
    MasterDataImportMode Mode,
    MasterDataImportStatus Status,
    Guid SubmittedActorId,
    DateTimeOffset CreatedAt,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    string CorrelationId,
    int TotalRows,
    int AcceptedCount,
    int RejectedCount,
    int QuarantinedCount,
    int CommittedCount,
    int SkippedCount,
    int FailedCount,
    string? IdempotencyKey,
    byte[] Version,
    MasterDataImportReconciliationResponse Reconciliation);

public sealed record MasterDataImportRowResponse(
    Guid Id,
    Guid BatchId,
    int OriginalRowNumber,
    int ReplaySequence,
    bool IsCurrent,
    MasterDataResourceKind ResourceKind,
    IReadOnlyDictionary<string, string?> SourceFields,
    IReadOnlyDictionary<string, string?> NormalizedFields,
    MasterDataImportRowOutcome Outcome,
    IReadOnlyList<MasterDataImportDiagnosticResponse> Diagnostics,
    MasterDataImportDiagnosticSeverity HighestSeverity,
    MasterDataImportMutationDisposition MutationDisposition,
    Guid? ResultingResourceId,
    string? ResultingResourceCode,
    Guid? ReplayOfRowId,
    Guid? OriginalRowId,
    DateTimeOffset ProcessedAt,
    byte[] Version);

public sealed record MasterDataImportReconciliationResponse(
    int TotalRows,
    int Accepted,
    int Rejected,
    int Quarantined,
    int Committed,
    int Skipped,
    int Failed,
    bool IsConsistent,
    string Formula);

public sealed record MasterDataImportAuditResponse(
    Guid EvidenceId,
    DateTimeOffset OccurredAt,
    string OperationId,
    string CorrelationId,
    Guid TenantId,
    Guid ActorId,
    Guid BatchId,
    Guid? RowId,
    int? OriginalRowNumber,
    MasterDataResourceKind ResourceKind,
    string Outcome,
    string SourceReference,
    string? Detail);

#pragma warning restore CS1591
