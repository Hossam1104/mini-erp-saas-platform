#pragma warning disable CS1591

using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.Contracts.Modules.Audit;
using MiniErp.Contracts.Modules.Foundation;
using MiniErp.Contracts.Modules.MasterData;

namespace MiniErp.App.Modules.MasterData;

public sealed record MasterDataImportSource(
    string SourceSystemCategory,
    string? SourceFileReference,
    string? BatchReference);

public sealed record MasterDataImportDiagnostic(
    string Code,
    string Message,
    string? Field,
    MasterDataImportDiagnosticSeverity Severity);

public sealed record MasterDataImportBatchRecord(
    Guid Id,
    TenantId TenantId,
    MasterDataResourceKind ResourceKind,
    MasterDataImportSource Source,
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
    string Fingerprint,
    byte[] Version)
{
    public MasterDataImportReconciliation Reconciliation =>
        MasterDataImportReconciliation.FromBatch(this);
}

public sealed record MasterDataImportRowRecord(
    Guid Id,
    Guid BatchId,
    TenantId TenantId,
    int OriginalRowNumber,
    int ReplaySequence,
    bool IsCurrent,
    MasterDataResourceKind ResourceKind,
    IReadOnlyDictionary<string, string?> SourceFields,
    IReadOnlyDictionary<string, string?> NormalizedFields,
    MasterDataImportRowOutcome Outcome,
    IReadOnlyList<MasterDataImportDiagnostic> Diagnostics,
    MasterDataImportDiagnosticSeverity HighestSeverity,
    MasterDataImportMutationDisposition MutationDisposition,
    Guid? ResultingResourceId,
    string? ResultingResourceCode,
    byte[]? ExpectedResourceVersion,
    Guid? ReplayOfRowId,
    Guid? OriginalRowId,
    string? ReplayIdempotencyKey,
    DateTimeOffset ProcessedAt,
    byte[] Version)
{
    public string IdentityKey => NormalizedFields.TryGetValue("_identityKey", out var value)
        ? value ?? string.Empty
        : string.Empty;

    public MasterDataImportRowRecord WithValidation(
        MasterDataImportRowValidationResult result,
        DateTimeOffset processedAt) => this with
        {
            NormalizedFields = result.NormalizedFields,
            Outcome = result.Outcome,
            Diagnostics = result.Diagnostics,
            HighestSeverity = result.Diagnostics.Count == 0
                ? MasterDataImportDiagnosticSeverity.Info
                : result.Diagnostics.Max(item => item.Severity),
            MutationDisposition = result.MutationDisposition,
            ResultingResourceId = result.ResultingResourceId,
            ResultingResourceCode = result.ResultingResourceCode,
            ExpectedResourceVersion = result.ExpectedResourceVersion,
            ProcessedAt = processedAt
        };

    public MasterDataImportRowRecord WithMutation(
        MasterDataImportMutationResult result,
        DateTimeOffset processedAt) => this with
        {
            Outcome = result.Outcome,
            Diagnostics = result.Diagnostics,
            HighestSeverity = result.Diagnostics.Count == 0
                ? MasterDataImportDiagnosticSeverity.Info
                : result.Diagnostics.Max(item => item.Severity),
            MutationDisposition = result.MutationDisposition,
            ResultingResourceId = result.ResultingResourceId ?? ResultingResourceId,
            ResultingResourceCode = result.ResultingResourceCode ?? ResultingResourceCode,
            ProcessedAt = processedAt
        };
}

public sealed record MasterDataImportReconciliation(
    int TotalRows,
    int Accepted,
    int Rejected,
    int Quarantined,
    int Committed,
    int Skipped,
    int Failed,
    bool IsConsistent)
{
    public const string Formula = "TotalRows = Accepted + Rejected + Quarantined; execution dispositions do not change row outcome totals.";

    public static MasterDataImportReconciliation FromBatch(MasterDataImportBatchRecord batch) => new(
        batch.TotalRows,
        batch.AcceptedCount,
        batch.RejectedCount,
        batch.QuarantinedCount,
        batch.CommittedCount,
        batch.SkippedCount,
        batch.FailedCount,
        batch.TotalRows == batch.AcceptedCount + batch.RejectedCount + batch.QuarantinedCount);

    public static MasterDataImportReconciliation FromRows(
        IReadOnlyCollection<MasterDataImportRowRecord> rows) =>
        FromRows(rows, includeNonCurrent: false);

    public static MasterDataImportReconciliation FromRows(
        IReadOnlyCollection<MasterDataImportRowRecord> rows,
        bool includeNonCurrent)
    {
        ArgumentNullException.ThrowIfNull(rows);
        var currentRows = includeNonCurrent
            ? rows
            : rows.Where(item => item.IsCurrent).ToArray();
        var accepted = currentRows.Count(item => item.Outcome == MasterDataImportRowOutcome.Accepted);
        var rejected = currentRows.Count(item => item.Outcome == MasterDataImportRowOutcome.Rejected);
        var quarantined = currentRows.Count(item => item.Outcome == MasterDataImportRowOutcome.Quarantined);
        var committed = currentRows.Count(item => item.MutationDisposition is
            MasterDataImportMutationDisposition.Committed
            or MasterDataImportMutationDisposition.Updated);
        var skipped = currentRows.Count(item => item.MutationDisposition == MasterDataImportMutationDisposition.SkippedExisting);
        var failed = currentRows.Count(item => item.MutationDisposition == MasterDataImportMutationDisposition.Failed);
        return new(
            currentRows.Count,
            accepted,
            rejected,
            quarantined,
            committed,
            skipped,
            failed,
            currentRows.Count == accepted + rejected + quarantined);
    }
}

public sealed record MasterDataImportAuditRecord(
    Guid EvidenceId,
    DateTimeOffset OccurredAt,
    string OperationId,
    string CorrelationId,
    TenantId TenantId,
    Guid ActorId,
    Guid BatchId,
    Guid? RowId,
    int? OriginalRowNumber,
    MasterDataResourceKind ResourceKind,
    string Outcome,
    string SourceReference,
    string? Detail);

public sealed record CreateMasterDataImportBatchCommand(
    Guid BatchId,
    MasterDataResourceKind ResourceKind,
    MasterDataImportSource Source,
    MasterDataImportDuplicatePolicy DuplicatePolicy,
    MasterDataImportMode Mode,
    Guid SubmittedActorId,
    DateTimeOffset CreatedAt,
    string CorrelationId,
    string? IdempotencyKey,
    string Fingerprint,
    IReadOnlyList<MasterDataImportRowRecord> Rows);

public sealed record SaveMasterDataImportResultCommand(
    Guid BatchId,
    MasterDataImportStatus Status,
    DateTimeOffset? StartedAt,
    DateTimeOffset? CompletedAt,
    string CorrelationId,
    string Fingerprint,
    byte[] ExpectedBatchVersion,
    IReadOnlyList<MasterDataImportRowRecord> Rows,
    MasterDataImportReconciliation Reconciliation);

public enum MasterDataImportPersistenceOutcome
{
    Succeeded = 1,
    NotFound = 2,
    Duplicate = 3,
    Conflict = 4,
    InvalidReference = 5,
    Failure = 6,
    IdempotentReplay = 7
}

public sealed record MasterDataImportPersistenceResult<T>(
    MasterDataImportPersistenceOutcome Outcome,
    string Code,
    T? Value)
{
    public bool Succeeded => Outcome is MasterDataImportPersistenceOutcome.Succeeded
        or MasterDataImportPersistenceOutcome.IdempotentReplay;

    public bool IsReplay => Outcome == MasterDataImportPersistenceOutcome.IdempotentReplay;

    public static MasterDataImportPersistenceResult<T> Success(T value) =>
        new(MasterDataImportPersistenceOutcome.Succeeded, "persisted", value);

    public static MasterDataImportPersistenceResult<T> Replay(T value) =>
        new(MasterDataImportPersistenceOutcome.IdempotentReplay, "idempotent_replay", value);

    public static MasterDataImportPersistenceResult<T> Denied(
        MasterDataImportPersistenceOutcome outcome,
        string code) => new(outcome, code, default);
}

public interface IMasterDataImportPersistence
{
    Task<MasterDataImportPersistenceResult<MasterDataImportBatchRecord>> CreateBatchAsync(
        TenantContext tenantContext,
        CreateMasterDataImportBatchCommand command,
        CancellationToken cancellationToken = default);

    Task<MasterDataImportBatchRecord?> FindBatchAsync(
        TenantContext tenantContext,
        Guid batchId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MasterDataImportBatchRecord>> ListBatchesAsync(
        TenantContext tenantContext,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MasterDataImportRowRecord>> ListRowsAsync(
        TenantContext tenantContext,
        Guid batchId,
        CancellationToken cancellationToken = default);

    Task<MasterDataImportRowRecord?> FindRowAsync(
        TenantContext tenantContext,
        Guid batchId,
        Guid rowId,
        CancellationToken cancellationToken = default);

    Task<MasterDataImportPersistenceResult<MasterDataImportBatchRecord>> SaveResultAsync(
        TenantContext tenantContext,
        SaveMasterDataImportResultCommand command,
        CancellationToken cancellationToken = default);

    Task<MasterDataImportPersistenceResult<MasterDataImportAuditRecord>> AppendAuditAsync(
        TenantContext tenantContext,
        MasterDataImportAuditRecord audit,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MasterDataImportAuditRecord>> ListAuditAsync(
        TenantContext tenantContext,
        Guid batchId,
        CancellationToken cancellationToken = default);
}

public sealed class UnavailableMasterDataImportPersistence : IMasterDataImportPersistence
{
    private static Task<T> Unavailable<T>() =>
        Task.FromException<T>(new InvalidOperationException("Master Data import persistence is unavailable."));

    public Task<MasterDataImportPersistenceResult<MasterDataImportBatchRecord>> CreateBatchAsync(TenantContext tenantContext, CreateMasterDataImportBatchCommand command, CancellationToken cancellationToken = default) => Unavailable<MasterDataImportPersistenceResult<MasterDataImportBatchRecord>>();
    public Task<MasterDataImportBatchRecord?> FindBatchAsync(TenantContext tenantContext, Guid batchId, CancellationToken cancellationToken = default) => Unavailable<MasterDataImportBatchRecord?>();
    public Task<IReadOnlyList<MasterDataImportBatchRecord>> ListBatchesAsync(TenantContext tenantContext, CancellationToken cancellationToken = default) => Unavailable<IReadOnlyList<MasterDataImportBatchRecord>>();
    public Task<IReadOnlyList<MasterDataImportRowRecord>> ListRowsAsync(TenantContext tenantContext, Guid batchId, CancellationToken cancellationToken = default) => Unavailable<IReadOnlyList<MasterDataImportRowRecord>>();
    public Task<MasterDataImportRowRecord?> FindRowAsync(TenantContext tenantContext, Guid batchId, Guid rowId, CancellationToken cancellationToken = default) => Unavailable<MasterDataImportRowRecord?>();
    public Task<MasterDataImportPersistenceResult<MasterDataImportBatchRecord>> SaveResultAsync(TenantContext tenantContext, SaveMasterDataImportResultCommand command, CancellationToken cancellationToken = default) => Unavailable<MasterDataImportPersistenceResult<MasterDataImportBatchRecord>>();
    public Task<MasterDataImportPersistenceResult<MasterDataImportAuditRecord>> AppendAuditAsync(TenantContext tenantContext, MasterDataImportAuditRecord audit, CancellationToken cancellationToken = default) => Unavailable<MasterDataImportPersistenceResult<MasterDataImportAuditRecord>>();
    public Task<IReadOnlyList<MasterDataImportAuditRecord>> ListAuditAsync(TenantContext tenantContext, Guid batchId, CancellationToken cancellationToken = default) => Unavailable<IReadOnlyList<MasterDataImportAuditRecord>>();
}

public sealed class MasterDataImportProcessingContext
{
    public MasterDataImportProcessingContext(
        MasterDataRequestContext requestContext,
        MasterDataImportBatchRecord batch,
        ISet<string> priorIdentityKeys)
    {
        RequestContext = requestContext ?? throw new ArgumentNullException(nameof(requestContext));
        Batch = batch ?? throw new ArgumentNullException(nameof(batch));
        PriorIdentityKeys = priorIdentityKeys ?? throw new ArgumentNullException(nameof(priorIdentityKeys));
    }

    public MasterDataRequestContext RequestContext { get; }

    public TenantContext TenantContext => RequestContext.TenantContext;

    public MasterDataImportBatchRecord Batch { get; }

    public ISet<string> PriorIdentityKeys { get; }
}

public sealed record MasterDataImportRowValidationResult(
    MasterDataImportRowOutcome Outcome,
    IReadOnlyDictionary<string, string?> NormalizedFields,
    IReadOnlyList<MasterDataImportDiagnostic> Diagnostics,
    MasterDataImportMutationDisposition MutationDisposition,
    Guid? ResultingResourceId = null,
    string? ResultingResourceCode = null,
    byte[]? ExpectedResourceVersion = null,
    string? IdentityKey = null)
{
    public static MasterDataImportRowValidationResult Rejected(
        IReadOnlyDictionary<string, string?> normalizedFields,
        string code,
        string message,
        string? field = null) => new(
        MasterDataImportRowOutcome.Rejected,
        normalizedFields,
        [new MasterDataImportDiagnostic(code, message, field, MasterDataImportDiagnosticSeverity.Error)],
        MasterDataImportMutationDisposition.NotAttempted);

    public static MasterDataImportRowValidationResult Quarantined(
        IReadOnlyDictionary<string, string?> normalizedFields,
        string code,
        string message,
        string? field = null) => new(
        MasterDataImportRowOutcome.Quarantined,
        normalizedFields,
        [new MasterDataImportDiagnostic(code, message, field, MasterDataImportDiagnosticSeverity.Warning)],
        MasterDataImportMutationDisposition.NotAttempted);

    public static MasterDataImportRowValidationResult Accepted(
        IReadOnlyDictionary<string, string?> normalizedFields,
        string identityKey,
        MasterDataImportMutationDisposition disposition = MasterDataImportMutationDisposition.Eligible,
        Guid? existingId = null,
        string? existingCode = null,
        byte[]? expectedVersion = null,
        IReadOnlyList<MasterDataImportDiagnostic>? diagnostics = null) => new(
        MasterDataImportRowOutcome.Accepted,
        normalizedFields,
        diagnostics ?? [],
        disposition,
        existingId,
        existingCode,
        expectedVersion,
        identityKey);
}

public sealed record MasterDataImportMutationResult(
    MasterDataImportRowOutcome Outcome,
    MasterDataImportMutationDisposition MutationDisposition,
    string Code,
    IReadOnlyList<MasterDataImportDiagnostic> Diagnostics,
    Guid? ResultingResourceId = null,
    string? ResultingResourceCode = null)
{
    public static MasterDataImportMutationResult Committed(Guid? resourceId, string? resourceCode, bool updated) => new(
        MasterDataImportRowOutcome.Accepted,
        updated ? MasterDataImportMutationDisposition.Updated : MasterDataImportMutationDisposition.Committed,
        updated ? "updated" : "committed",
        [],
        resourceId,
        resourceCode);

    public static MasterDataImportMutationResult Skipped(Guid? resourceId, string? resourceCode) => new(
        MasterDataImportRowOutcome.Accepted,
        MasterDataImportMutationDisposition.SkippedExisting,
        "skipped_existing",
        [],
        resourceId,
        resourceCode);

    public static MasterDataImportMutationResult Failed(string code, string message, string? field = null) => new(
        MasterDataImportRowOutcome.Quarantined,
        MasterDataImportMutationDisposition.Failed,
        code,
        [new MasterDataImportDiagnostic(code, message, field, MasterDataImportDiagnosticSeverity.Warning)]);
}

public interface IMasterDataImportProcessor
{
    MasterDataResourceKind ResourceKind { get; }

    bool AllowsMutableUpdate { get; }

    Task<MasterDataImportRowValidationResult> ValidateAsync(
        MasterDataImportProcessingContext context,
        MasterDataImportRowRecord row,
        CancellationToken cancellationToken = default);

    Task<MasterDataImportMutationResult> CommitAsync(
        MasterDataImportProcessingContext context,
        MasterDataImportRowRecord row,
        CancellationToken cancellationToken = default);
}

public sealed class MasterDataImportProcessorRegistry
{
    private readonly IReadOnlyDictionary<MasterDataResourceKind, IMasterDataImportProcessor> processors;

    public MasterDataImportProcessorRegistry(IEnumerable<IMasterDataImportProcessor> processors)
    {
        ArgumentNullException.ThrowIfNull(processors);
        var values = processors.ToArray();
        if (values.Length == 0)
        {
            throw new ArgumentException("At least one import processor is required.", nameof(processors));
        }

        var duplicate = values.GroupBy(item => item.ResourceKind).FirstOrDefault(group => group.Count() > 1);
        if (duplicate is not null)
        {
            throw new ArgumentException("Only one import processor may own a resource kind.", nameof(processors));
        }

        this.processors = values.ToDictionary(item => item.ResourceKind);
    }

    public IReadOnlyCollection<MasterDataResourceKind> ResourceKinds => processors.Keys.ToArray();

    public IMasterDataImportProcessor GetRequired(MasterDataResourceKind resourceKind) =>
        processors.TryGetValue(resourceKind, out var processor)
            ? processor
            : throw new KeyNotFoundException($"No Master Data import processor exists for '{resourceKind}'.");

    public bool TryGet(MasterDataResourceKind resourceKind, out IMasterDataImportProcessor processor) =>
        processors.TryGetValue(resourceKind, out processor!);
}

public sealed record MasterDataImportOperationResult<T>(
    bool Succeeded,
    string Code,
    T? Value,
    int StatusCode = 200)
{
    public static MasterDataImportOperationResult<T> Success(T value, string code = "succeeded") =>
        new(true, code, value);

    public static MasterDataImportOperationResult<T> Failure(
        string code,
        int statusCode = 400) => new(false, code, default, statusCode);
}

public sealed class MasterDataImportAuthorizationComposition
{
    public MasterDataImportAuthorizationComposition(
        IMasterDataCapabilityResolver capabilityResolver)
    {
        ArgumentNullException.ThrowIfNull(capabilityResolver);
        Authorization = new MasterDataResourceAuthorizationService(
            capabilityResolver,
            new MasterDataImportResourcePolicy(),
            new MasterDataImportApprovalPolicy(),
            new MasterDataImportScopePolicy());
    }

    public MasterDataResourceAuthorizationService Authorization { get; }
}

public sealed class MasterDataImportScopePolicy : IMasterDataScopePolicy
{
    public const string PolicyId = "master-data.import.scope";
    public const int PolicyVersion = 1;

    public MasterDataScopeDecision Evaluate(
        MasterDataRequestContext context,
        MasterDataResourceReference resource)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(resource);
        if (resource.ResourceKind != MasterDataResourceKind.Import
            || resource.Tenant.TenantId != context.TenantId.Value
            || resource.Scope is not { } scope
            || scope.OrganizationAnchor is not null
            || !string.Equals(scope.Policy.PolicyId, PolicyId, StringComparison.Ordinal)
            || scope.Policy.Version != PolicyVersion)
        {
            return MasterDataScopeDecision.Denied("resource_scope_denied");
        }

        return MasterDataScopeDecision.Success("import_scope_allowed");
    }

    public static BusinessScope CreateScope(TenantId tenantId) => new(
        new TenantOwnership(tenantId.Value),
        organizationAnchor: null,
        new ScopePolicyReference(PolicyId, PolicyVersion));
}

public sealed class MasterDataImportResourcePolicy : IMasterDataResourcePolicy
{
    public MasterDataPolicyDecision Evaluate(MasterDataPolicyInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        return input.Resource.ResourceKind == MasterDataResourceKind.Import
            ? MasterDataPolicyDecision.Allowed("import_resource_allowed")
            : MasterDataPolicyDecision.Denied("resource_policy_not_configured");
    }
}

public sealed class MasterDataImportApprovalPolicy : IMasterDataApprovalPolicy
{
    public MasterDataApprovalPolicyResult Evaluate(
        MasterDataRequestContext context,
        MasterDataResourceReference resource,
        MasterDataOperation operation)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(resource);
        return resource.ResourceKind == MasterDataResourceKind.Import
            && (operation is MasterDataOperation.View
                or MasterDataOperation.Import
                or MasterDataOperation.ViewAuditHistory)
            ? new MasterDataApprovalPolicyResult(MasterDataApprovalPolicyStatus.NotApplicable)
            : new MasterDataApprovalPolicyResult(MasterDataApprovalPolicyStatus.NotConfigured);
    }
}

#pragma warning restore CS1591
