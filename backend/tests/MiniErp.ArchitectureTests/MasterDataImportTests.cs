using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MiniErp.App.BuildingBlocks.Rest;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.Modules.MasterData;
using MiniErp.Contracts.Modules.Foundation;
using MiniErp.Contracts.Modules.MasterData;
using MiniErp.Infrastructure.Persistence.Modules.MasterData;
using Xunit;

namespace MiniErp.ArchitectureTests;

public sealed class MasterDataImportTests
{
    private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid ActorId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid SessionId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    [Fact]
    public void Import_normalization_rejects_authority_fields_and_normalized_duplicates()
    {
        var authorityFields = MasterDataImportFields.Normalize(
            new Dictionary<string, string?> { ["TenantId"] = TenantId.ToString("D") },
            out var authorityFailure);

        Assert.Empty(authorityFields);
        Assert.Equal("import_untrusted_field", authorityFailure?.Code);

        var duplicateFields = MasterDataImportFields.Normalize(
            new Dictionary<string, string?>
            {
                ["Code"] = "USD",
                [" code "] = "USD"
            },
            out var duplicateFailure);

        Assert.Equal("USD", duplicateFields["code"]);
        Assert.Equal("import_field_duplicate", duplicateFailure?.Code);
    }

    [Fact]
    public void Import_reconciliation_counts_current_rows_and_preserves_the_contract_formula()
    {
        var rows = new[]
        {
            Row(1, MasterDataImportRowOutcome.Accepted, MasterDataImportMutationDisposition.Committed),
            Row(2, MasterDataImportRowOutcome.Accepted, MasterDataImportMutationDisposition.SkippedExisting),
            Row(3, MasterDataImportRowOutcome.Rejected, MasterDataImportMutationDisposition.NotAttempted),
            Row(4, MasterDataImportRowOutcome.Quarantined, MasterDataImportMutationDisposition.Failed),
            Row(4, MasterDataImportRowOutcome.Quarantined, MasterDataImportMutationDisposition.NotAttempted, isCurrent: false)
        };

        var reconciliation = MasterDataImportReconciliation.FromRows(rows);

        Assert.Equal(4, reconciliation.TotalRows);
        Assert.Equal(2, reconciliation.Accepted);
        Assert.Equal(1, reconciliation.Rejected);
        Assert.Equal(1, reconciliation.Quarantined);
        Assert.Equal(1, reconciliation.Committed);
        Assert.Equal(1, reconciliation.Skipped);
        Assert.Equal(1, reconciliation.Failed);
        Assert.True(reconciliation.IsConsistent);
        Assert.Contains("TotalRows = Accepted + Rejected + Quarantined", MasterDataImportReconciliation.Formula, StringComparison.Ordinal);
    }

    [Fact]
    public void Import_registry_has_one_owner_per_resource_kind()
    {
        var kinds = new[]
        {
            MasterDataResourceKind.ProductCategory,
            MasterDataResourceKind.UnitOfMeasure,
            MasterDataResourceKind.Product,
            MasterDataResourceKind.Supplier,
            MasterDataResourceKind.BusinessCustomer,
            MasterDataResourceKind.Currency,
            MasterDataResourceKind.PaymentTerm,
            MasterDataResourceKind.Tax,
            MasterDataResourceKind.ExchangeRate,
            MasterDataResourceKind.PriceList
        };
        var registry = new MasterDataImportProcessorRegistry(kinds.Select(kind => new RecordingProcessor(kind)));

        Assert.Equal(kinds.OrderBy(kind => kind), registry.ResourceKinds.OrderBy(kind => kind));
        Assert.Throws<ArgumentException>(() => new MasterDataImportProcessorRegistry(
            [new RecordingProcessor(MasterDataResourceKind.Currency), new RecordingProcessor(MasterDataResourceKind.Currency)]));
    }

    [Fact]
    public async Task Dry_run_simulation_validates_rows_without_invoking_a_commit_adapter()
    {
        var processor = new RecordingProcessor(MasterDataResourceKind.Currency);
        var batch = Batch(MasterDataImportMode.DryRun, MasterDataImportStatus.Draft, 1);
        var persistence = new RecordingImportPersistence(batch, [Row(1, MasterDataImportRowOutcome.NotProcessed, MasterDataImportMutationDisposition.NotAttempted)]);
        var service = new MasterDataImportService(
            new MasterDataImportAuthorizationComposition(new GrantingCapabilityResolver()),
            persistence,
            new MasterDataImportProcessorRegistry([processor]));

        var result = await service.SimulateAsync(Context(), batch.Id);

        Assert.True(result.Succeeded, result.Code);
        Assert.Equal(MasterDataImportStatus.Validated, result.Value!.Status);
        Assert.Equal(1, processor.ValidateCalls);
        Assert.Equal(0, processor.CommitCalls);
        Assert.Equal(MasterDataImportRowOutcome.Accepted, Assert.Single(persistence.Rows).Outcome);
        Assert.Contains(persistence.Audit, item => item.OperationId == "master-data.import.batch.simulated");
    }

    [Fact]
    public async Task Durable_import_tracking_is_idempotent_and_tenant_filtered()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();
        var options = new DbContextOptionsBuilder().UseSqlite(connection).Options;
        var tenantA = TenantContext.ForOrdinaryMembership(
            new TenantId(TenantId),
            new MembershipReference(Guid.NewGuid()),
            new ScopeReference($"Company:{TenantId:D}"),
            new CorrelationId("corr-import-a"),
            ActorId);
        var tenantB = TenantContext.ForOrdinaryMembership(
            new TenantId(Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb")),
            new MembershipReference(Guid.NewGuid()),
            new ScopeReference("Company:bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb"),
            new CorrelationId("corr-import-b"),
            ActorId);
        await using (var db = new MasterDataDbContext(options, tenantA))
        {
            await db.Database.EnsureCreatedAsync();
        }

        var batchId = Guid.NewGuid();
        var source = new MasterDataImportSource("fixture", "fixture.json", "fixture-1");
        var row = Row(1, MasterDataImportRowOutcome.NotProcessed, MasterDataImportMutationDisposition.NotAttempted, batchId: batchId);
        var command = new CreateMasterDataImportBatchCommand(
            batchId,
            MasterDataResourceKind.Currency,
            source,
            MasterDataImportDuplicatePolicy.Reject,
            MasterDataImportMode.DryRun,
            ActorId,
            DateTimeOffset.UtcNow,
            "corr-import-a",
            "durable-key",
            "durable-fingerprint",
            [row]);
        var persistence = new MasterDataImportPersistence(options);

        var created = await persistence.CreateBatchAsync(tenantA, command);
        var replay = await persistence.CreateBatchAsync(tenantA, command);
        var foreignRead = await persistence.FindBatchAsync(tenantB, batchId);

        Assert.True(created.Succeeded, created.Code);
        Assert.False(created.IsReplay);
        Assert.True(replay.Succeeded, replay.Code);
        Assert.True(replay.IsReplay);
        Assert.Null(foreignRead);

        var audit = new MasterDataImportAuditRecord(
            Guid.NewGuid(),
            DateTimeOffset.UtcNow,
            "master-data.import.test",
            "corr-import-a",
            new TenantId(TenantId),
            ActorId,
            batchId,
            row.Id,
            row.OriginalRowNumber,
            MasterDataResourceKind.Currency,
            "created",
            "fixture.json",
            "test evidence");
        var appended = await persistence.AppendAuditAsync(tenantA, audit);
        var tenantAudit = await persistence.ListAuditAsync(tenantA, batchId);
        var foreignAudit = await persistence.ListAuditAsync(tenantB, batchId);

        Assert.True(appended.Succeeded, appended.Code);
        Assert.Single(tenantAudit);
        Assert.Empty(foreignAudit);
    }

    [Fact]
    public void Import_catalogue_exposes_the_phase_a_mutation_boundary()
    {
        var operations = FoundationOperationCatalog.PublicOperations
            .Where(item => item.OperationId.StartsWith("master-data.import.", StringComparison.Ordinal))
            .ToDictionary(item => item.OperationId, StringComparer.Ordinal);

        Assert.Equal(11, operations.Count);
        Assert.True(operations["master-data.import.create"].RequiresAntiforgery);
        Assert.True(operations["master-data.import.create"].RequiresMandatoryAudit);
        Assert.Equal(FoundationIdempotencyPolicy.Required, operations["master-data.import.create"].Idempotency);
        Assert.Equal(FoundationConcurrencyPolicy.IfMatch, operations["master-data.import.execute"].Concurrency);
        Assert.True(MasterDataOperationCatalog.TryGetRequiredCapability(MasterDataOperation.Import, out var capability));
        Assert.Equal(MasterDataCapability.ImportMigrate, capability);
    }

    private static MasterDataRequestContext Context()
    {
        var foundation = FoundationRequestContext.ForTenant(
            ActorId,
            SessionId,
            TenantContext.ForOrdinaryMembership(
                new TenantId(TenantId),
                new MembershipReference(Guid.NewGuid()),
                new ScopeReference($"Company:{TenantId:D}"),
                new CorrelationId("corr-import-test"),
                ActorId),
            "master-data.import");
        return Assert.IsType<MasterDataRequestContext>(new MasterDataTenantContextResolver().Resolve(foundation).Context);
    }

    private static MasterDataImportBatchRecord Batch(
        MasterDataImportMode mode,
        MasterDataImportStatus status,
        int totalRows) => new(
        Guid.NewGuid(),
        new TenantId(TenantId),
        MasterDataResourceKind.Currency,
        new MasterDataImportSource("test", "test-fixture", "batch-1"),
        MasterDataImportDuplicatePolicy.Reject,
        mode,
        status,
        ActorId,
        DateTimeOffset.UtcNow,
        null,
        null,
        "corr-import-test",
        totalRows,
        0,
        0,
        0,
        0,
        0,
        0,
        "import-test-key",
        "fingerprint",
        [1, 2, 3]);

    private static MasterDataImportRowRecord Row(
        int rowNumber,
        MasterDataImportRowOutcome outcome,
        MasterDataImportMutationDisposition mutationDisposition,
        bool isCurrent = true,
        Guid? batchId = null) => new(
        Guid.NewGuid(),
        batchId ?? Guid.Empty,
        new TenantId(TenantId),
        rowNumber,
        0,
        isCurrent,
        MasterDataResourceKind.Currency,
        new Dictionary<string, string?> { ["code"] = "USD" },
        new Dictionary<string, string?>(),
        outcome,
        [],
        MasterDataImportDiagnosticSeverity.Info,
        mutationDisposition,
        null,
        null,
        null,
        null,
        null,
        null,
        DateTimeOffset.UtcNow,
        [4, 5, 6]);

    private sealed class GrantingCapabilityResolver : IMasterDataCapabilityResolver
    {
        private static readonly IReadOnlySet<MasterDataCapability> Capabilities =
            Enum.GetValues<MasterDataCapability>().ToHashSet();

        public IReadOnlySet<MasterDataCapability> Resolve(MasterDataRequestContext context) => Capabilities;
    }

    private sealed class RecordingProcessor : IMasterDataImportProcessor
    {
        public RecordingProcessor(MasterDataResourceKind resourceKind) => ResourceKind = resourceKind;

        public MasterDataResourceKind ResourceKind { get; }

        public bool AllowsMutableUpdate => true;

        public int ValidateCalls { get; private set; }

        public int CommitCalls { get; private set; }

        public Task<MasterDataImportRowValidationResult> ValidateAsync(
            MasterDataImportProcessingContext context,
            MasterDataImportRowRecord row,
            CancellationToken cancellationToken = default)
        {
            ValidateCalls++;
            var normalized = MasterDataImportFields.WithIdentity(row.SourceFields, "currency:USD");
            return Task.FromResult(MasterDataImportRowValidationResult.Accepted(normalized, "currency:USD"));
        }

        public Task<MasterDataImportMutationResult> CommitAsync(
            MasterDataImportProcessingContext context,
            MasterDataImportRowRecord row,
            CancellationToken cancellationToken = default)
        {
            CommitCalls++;
            return Task.FromResult(MasterDataImportMutationResult.Committed(Guid.NewGuid(), "USD", false));
        }
    }

    private sealed class RecordingImportPersistence : IMasterDataImportPersistence
    {
        private MasterDataImportBatchRecord batch;

        internal RecordingImportPersistence(MasterDataImportBatchRecord batch, IReadOnlyList<MasterDataImportRowRecord> rows)
        {
            this.batch = batch;
            Rows = rows;
        }

        internal IReadOnlyList<MasterDataImportRowRecord> Rows { get; private set; }

        internal List<MasterDataImportAuditRecord> Audit { get; } = [];

        public Task<MasterDataImportPersistenceResult<MasterDataImportBatchRecord>> CreateBatchAsync(
            TenantContext tenantContext,
            CreateMasterDataImportBatchCommand command,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(MasterDataImportPersistenceResult<MasterDataImportBatchRecord>.Success(batch));

        public Task<MasterDataImportBatchRecord?> FindBatchAsync(
            TenantContext tenantContext,
            Guid batchId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<MasterDataImportBatchRecord?>(batch.Id == batchId ? batch : null);

        public Task<IReadOnlyList<MasterDataImportBatchRecord>> ListBatchesAsync(
            TenantContext tenantContext,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<MasterDataImportBatchRecord>>([batch]);

        public Task<IReadOnlyList<MasterDataImportRowRecord>> ListRowsAsync(
            TenantContext tenantContext,
            Guid batchId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Rows);

        public Task<MasterDataImportRowRecord?> FindRowAsync(
            TenantContext tenantContext,
            Guid batchId,
            Guid rowId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(Rows.SingleOrDefault(row => row.Id == rowId));

        public Task<MasterDataImportPersistenceResult<MasterDataImportBatchRecord>> SaveResultAsync(
            TenantContext tenantContext,
            SaveMasterDataImportResultCommand command,
            CancellationToken cancellationToken = default)
        {
            Rows = command.Rows;
            var reconciliation = command.Reconciliation;
            batch = batch with
            {
                Status = command.Status,
                StartedAt = command.StartedAt,
                CompletedAt = command.CompletedAt,
                TotalRows = reconciliation.TotalRows,
                AcceptedCount = reconciliation.Accepted,
                RejectedCount = reconciliation.Rejected,
                QuarantinedCount = reconciliation.Quarantined,
                CommittedCount = reconciliation.Committed,
                SkippedCount = reconciliation.Skipped,
                FailedCount = reconciliation.Failed,
                Version = [7, 8, 9]
            };
            return Task.FromResult(MasterDataImportPersistenceResult<MasterDataImportBatchRecord>.Success(batch));
        }

        public Task<MasterDataImportPersistenceResult<MasterDataImportAuditRecord>> AppendAuditAsync(
            TenantContext tenantContext,
            MasterDataImportAuditRecord audit,
            CancellationToken cancellationToken = default)
        {
            Audit.Add(audit);
            return Task.FromResult(MasterDataImportPersistenceResult<MasterDataImportAuditRecord>.Success(audit));
        }

        public Task<IReadOnlyList<MasterDataImportAuditRecord>> ListAuditAsync(
            TenantContext tenantContext,
            Guid batchId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult<IReadOnlyList<MasterDataImportAuditRecord>>(Audit);
    }
}
