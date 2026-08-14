using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.Modules.MasterData;
using MiniErp.Contracts.Modules.MasterData;

namespace MiniErp.Infrastructure.Persistence.Modules.MasterData;

/// <summary>
/// Durable adapter for import tracking. It is intentionally separate from
/// the ten business master aggregates and applies the trusted Tenant filter on
/// every read and write session.
/// </summary>
internal sealed class MasterDataImportPersistence : IMasterDataImportPersistence
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly DbContextOptions options;

    internal MasterDataImportPersistence(DbContextOptions options)
    {
        this.options = options ?? throw new ArgumentNullException(nameof(options));
    }

    public async Task<MasterDataImportPersistenceResult<MasterDataImportBatchRecord>> CreateBatchAsync(
        TenantContext tenantContext,
        CreateMasterDataImportBatchCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(command);

        await using var db = CreateContext(tenantContext);
        if (!string.IsNullOrWhiteSpace(command.IdempotencyKey))
        {
            var existing = await db.ImportBatches
                .SingleOrDefaultAsync(item => item.SubmittedActorId == command.SubmittedActorId
                    && item.ResourceKind == command.ResourceKind
                    && item.IdempotencyKey == command.IdempotencyKey,
                    cancellationToken);
            if (existing is not null)
            {
                return string.Equals(existing.Fingerprint, command.Fingerprint, StringComparison.Ordinal)
                    ? MasterDataImportPersistenceResult<MasterDataImportBatchRecord>.Replay(ToBatchRecord(existing))
                    : MasterDataImportPersistenceResult<MasterDataImportBatchRecord>.Denied(
                        MasterDataImportPersistenceOutcome.Duplicate,
                        "import_idempotency_conflict");
            }
        }

        var batchId = command.BatchId == Guid.Empty ? Guid.NewGuid() : command.BatchId;
        if (command.Rows.Any(row => row.Id == Guid.Empty
                || row.BatchId != batchId
                || row.TenantId != tenantContext.TenantId
                || row.ResourceKind != command.ResourceKind))
        {
            return MasterDataImportPersistenceResult<MasterDataImportBatchRecord>.Denied(
                MasterDataImportPersistenceOutcome.InvalidReference,
                "import_row_ownership_invalid");
        }

        var batch = new MasterDataImportBatchEntity(
            batchId,
            tenantContext.TenantId,
            command.ResourceKind,
            command.Source.SourceSystemCategory,
            command.Source.SourceFileReference,
            command.Source.BatchReference,
            command.DuplicatePolicy,
            command.Mode,
            command.SubmittedActorId,
            command.CreatedAt,
            command.CorrelationId,
            command.IdempotencyKey,
            command.Fingerprint,
            command.Rows.Count);

        db.ImportBatches.Add(batch);
        foreach (var row in command.Rows)
        {
            db.ImportRows.Add(ToEntity(row, tenantContext.TenantId));
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return MasterDataImportPersistenceResult<MasterDataImportBatchRecord>.Success(ToBatchRecord(batch));
        }
        catch (DbUpdateException)
        {
            return MasterDataImportPersistenceResult<MasterDataImportBatchRecord>.Denied(
                MasterDataImportPersistenceOutcome.Duplicate,
                "import_duplicate");
        }
    }

    public async Task<MasterDataImportBatchRecord?> FindBatchAsync(
        TenantContext tenantContext,
        Guid batchId,
        CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(tenantContext);
        var entity = await db.ImportBatches
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.Id == batchId, cancellationToken);
        return entity is null ? null : ToBatchRecord(entity);
    }

    public async Task<IReadOnlyList<MasterDataImportBatchRecord>> ListBatchesAsync(
        TenantContext tenantContext,
        CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(tenantContext);
        var entities = await db.ImportBatches
            .AsNoTracking()
            .ToListAsync(cancellationToken);
        return entities
            .OrderByDescending(item => item.CreatedAt)
            .ThenBy(item => item.Id)
            .Select(ToBatchRecord)
            .ToArray();
    }

    public async Task<IReadOnlyList<MasterDataImportRowRecord>> ListRowsAsync(
        TenantContext tenantContext,
        Guid batchId,
        CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(tenantContext);
        var entities = await db.ImportRows
            .AsNoTracking()
            .Where(item => item.BatchId == batchId)
            .OrderBy(item => item.OriginalRowNumber)
            .ThenBy(item => item.ReplaySequence)
            .ToListAsync(cancellationToken);
        return entities.Select(ToRowRecord).ToArray();
    }

    public async Task<MasterDataImportRowRecord?> FindRowAsync(
        TenantContext tenantContext,
        Guid batchId,
        Guid rowId,
        CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(tenantContext);
        var entity = await db.ImportRows
            .AsNoTracking()
            .SingleOrDefaultAsync(item => item.BatchId == batchId && item.Id == rowId, cancellationToken);
        return entity is null ? null : ToRowRecord(entity);
    }

    public async Task<MasterDataImportPersistenceResult<MasterDataImportBatchRecord>> SaveResultAsync(
        TenantContext tenantContext,
        SaveMasterDataImportResultCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(command);

        await using var db = CreateContext(tenantContext);
        var batch = await db.ImportBatches
            .SingleOrDefaultAsync(item => item.Id == command.BatchId, cancellationToken);
        if (batch is null)
        {
            return MasterDataImportPersistenceResult<MasterDataImportBatchRecord>.Denied(
                MasterDataImportPersistenceOutcome.NotFound,
                "import_batch_not_found");
        }

        if (!VersionMatches(batch.Version, command.ExpectedBatchVersion))
        {
            return MasterDataImportPersistenceResult<MasterDataImportBatchRecord>.Denied(
                MasterDataImportPersistenceOutcome.Conflict,
                "import_concurrency_conflict");
        }

        var rows = await db.ImportRows
            .Where(item => item.BatchId == command.BatchId)
            .ToDictionaryAsync(item => item.Id, cancellationToken);
        if (command.Rows.Any(row => row.Id == Guid.Empty
                || row.BatchId != command.BatchId
                || row.TenantId != tenantContext.TenantId
                || row.ResourceKind != batch.ResourceKind))
        {
            return MasterDataImportPersistenceResult<MasterDataImportBatchRecord>.Denied(
                MasterDataImportPersistenceOutcome.InvalidReference,
                "import_row_ownership_invalid");
        }

        foreach (var row in command.Rows)
        {
            if (rows.TryGetValue(row.Id, out var existing))
            {
                existing.Apply(
                    row,
                    Serialize(row.SourceFields),
                    Serialize(row.NormalizedFields),
                    Serialize(row.Diagnostics));
            }
            else
            {
                db.ImportRows.Add(ToEntity(row, tenantContext.TenantId));
            }
        }

        batch.ApplyResult(
            command.Status,
            command.StartedAt,
            command.CompletedAt,
            command.Reconciliation,
            command.CorrelationId,
            command.Fingerprint);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return MasterDataImportPersistenceResult<MasterDataImportBatchRecord>.Success(ToBatchRecord(batch));
        }
        catch (DbUpdateConcurrencyException)
        {
            return MasterDataImportPersistenceResult<MasterDataImportBatchRecord>.Denied(
                MasterDataImportPersistenceOutcome.Conflict,
                "import_concurrency_conflict");
        }
        catch (DbUpdateException)
        {
            return MasterDataImportPersistenceResult<MasterDataImportBatchRecord>.Denied(
                MasterDataImportPersistenceOutcome.Failure,
                "import_persistence_failed");
        }
    }

    public async Task<MasterDataImportPersistenceResult<MasterDataImportAuditRecord>> AppendAuditAsync(
        TenantContext tenantContext,
        MasterDataImportAuditRecord audit,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tenantContext);
        ArgumentNullException.ThrowIfNull(audit);
        if (audit.TenantId != tenantContext.TenantId)
        {
            return MasterDataImportPersistenceResult<MasterDataImportAuditRecord>.Denied(
                MasterDataImportPersistenceOutcome.InvalidReference,
                "cross_tenant_target_denied");
        }

        await using var db = CreateContext(tenantContext);
        if (!await db.ImportBatches.AnyAsync(item => item.Id == audit.BatchId, cancellationToken)
            || audit.RowId is { } rowId
                && !await db.ImportRows.AnyAsync(item => item.Id == rowId && item.BatchId == audit.BatchId, cancellationToken))
        {
            return MasterDataImportPersistenceResult<MasterDataImportAuditRecord>.Denied(
                MasterDataImportPersistenceOutcome.InvalidReference,
                "import_audit_target_not_found");
        }

        if (await db.ImportAuditEvents.AnyAsync(item => item.EvidenceId == audit.EvidenceId, cancellationToken))
        {
            return MasterDataImportPersistenceResult<MasterDataImportAuditRecord>.Replay(audit);
        }

        db.ImportAuditEvents.Add(new MasterDataImportAuditEntity(audit));
        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return MasterDataImportPersistenceResult<MasterDataImportAuditRecord>.Success(audit);
        }
        catch (DbUpdateException)
        {
            return MasterDataImportPersistenceResult<MasterDataImportAuditRecord>.Denied(
                MasterDataImportPersistenceOutcome.Failure,
                "import_audit_persistence_failed");
        }
    }

    public async Task<IReadOnlyList<MasterDataImportAuditRecord>> ListAuditAsync(
        TenantContext tenantContext,
        Guid batchId,
        CancellationToken cancellationToken = default)
    {
        await using var db = CreateContext(tenantContext);
        var entities = await db.ImportAuditEvents
            .AsNoTracking()
            .Where(item => item.BatchId == batchId)
            .ToListAsync(cancellationToken);
        return entities
            .OrderBy(item => item.OccurredAt)
            .ThenBy(item => item.EvidenceId)
            .Select(ToAuditRecord)
            .ToArray();
    }

    private MasterDataDbContext CreateContext(TenantContext tenantContext) =>
        new(options, tenantContext);

    private static MasterDataImportRowEntity ToEntity(
        MasterDataImportRowRecord row,
        TenantId tenantId)
    {
        if (row.TenantId != tenantId)
        {
            throw new InvalidOperationException("Import row Tenant ownership does not match the persistence session.");
        }

        return new MasterDataImportRowEntity(
            row.Id,
            row.BatchId,
            tenantId,
            row.OriginalRowNumber,
            row.ReplaySequence,
            row.IsCurrent,
            row.ResourceKind,
            Serialize(row.SourceFields),
            Serialize(row.NormalizedFields),
            row.Outcome,
            Serialize(row.Diagnostics),
            row.HighestSeverity,
            row.MutationDisposition,
            row.ResultingResourceId,
            row.ResultingResourceCode,
            row.ExpectedResourceVersion,
            row.ReplayOfRowId,
            row.OriginalRowId,
            row.ReplayIdempotencyKey,
            row.ProcessedAt);
    }

    private static MasterDataImportBatchRecord ToBatchRecord(MasterDataImportBatchEntity entity) => new(
        entity.Id,
        entity.TenantId,
        entity.ResourceKind,
        new MasterDataImportSource(
            entity.SourceSystemCategory,
            entity.SourceFileReference,
            entity.BatchReference),
        entity.DuplicatePolicy,
        entity.Mode,
        entity.Status,
        entity.SubmittedActorId,
        entity.CreatedAt,
        entity.StartedAt,
        entity.CompletedAt,
        entity.CorrelationId,
        entity.TotalRows,
        entity.AcceptedCount,
        entity.RejectedCount,
        entity.QuarantinedCount,
        entity.CommittedCount,
        entity.SkippedCount,
        entity.FailedCount,
        entity.IdempotencyKey,
        entity.Fingerprint,
        entity.Version.ToArray());

    private static MasterDataImportRowRecord ToRowRecord(MasterDataImportRowEntity entity)
    {
        var sourceFields = Deserialize<Dictionary<string, string?>>(entity.SourceFieldsJson)
            ?? new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var normalizedFields = Deserialize<Dictionary<string, string?>>(entity.NormalizedFieldsJson)
            ?? new Dictionary<string, string?>(StringComparer.OrdinalIgnoreCase);
        var diagnostics = Deserialize<List<MasterDataImportDiagnostic>>(entity.DiagnosticsJson)
            ?? [];
        return new MasterDataImportRowRecord(
            entity.Id,
            entity.BatchId,
            entity.TenantId,
            entity.OriginalRowNumber,
            entity.ReplaySequence,
            entity.IsCurrent,
            entity.ResourceKind,
            sourceFields,
            normalizedFields,
            entity.Outcome,
            diagnostics,
            entity.HighestSeverity,
            entity.MutationDisposition,
            entity.ResultingResourceId,
            entity.ResultingResourceCode,
            entity.ExpectedResourceVersion?.ToArray(),
            entity.ReplayOfRowId,
            entity.OriginalRowId,
            entity.ReplayIdempotencyKey,
            entity.ProcessedAt,
            entity.Version.ToArray());
    }

    private static MasterDataImportAuditRecord ToAuditRecord(MasterDataImportAuditEntity entity) => new(
        entity.EvidenceId,
        entity.OccurredAt,
        entity.OperationId,
        entity.CorrelationId,
        entity.TenantId,
        entity.ActorId,
        entity.BatchId,
        entity.RowId,
        entity.OriginalRowNumber,
        entity.ResourceKind,
        entity.Outcome,
        entity.SourceReference,
        entity.Detail);

    private static string Serialize<T>(T value) => JsonSerializer.Serialize(value, JsonOptions);

    private static T? Deserialize<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, JsonOptions);

    private static bool VersionMatches(byte[] current, byte[] expected) =>
        current.Length != 0 && expected.Length != 0 && current.SequenceEqual(expected);
}
