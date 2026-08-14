using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using MiniErp.App.BuildingBlocks.Rest;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.Modules.BusinessParties;
using MiniErp.App.Modules.MasterData;
using MiniErp.Contracts.Modules.MasterData;
using MiniErp.Infrastructure.Persistence.Modules.MasterData;
using Xunit;

namespace MiniErp.ArchitectureTests;

public sealed class MasterDataImportReplayLifecycleTests
{
    private static readonly Guid TenantId = Guid.Parse("aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa");
    private static readonly Guid ActorId = Guid.Parse("33333333-3333-3333-3333-333333333333");
    private static readonly Guid SessionId = Guid.Parse("44444444-4444-4444-4444-444444444444");

    [Fact]
    public async Task Pre_execution_replay_keeps_commit_batch_validated_until_execute_commits_all_current_eligible_rows()
    {
        await using var fixture = await ImportFixture.CreateAsync();
        var simulated = await fixture.CreateAndSimulateAsync(MasterDataImportMode.Commit);
        var quarantine = Assert.Single(
            await fixture.ImportPersistence.ListRowsAsync(fixture.TenantContext, simulated.Id),
            row => row.IsCurrent && row.Outcome == MasterDataImportRowOutcome.Quarantined);
        var validatedAt = simulated.CompletedAt;

        Assert.Equal(MasterDataImportStatus.Validated, simulated.Status);
        Assert.Equal(1, simulated.AcceptedCount);
        Assert.Equal(1, simulated.QuarantinedCount);
        Assert.Equal(0, simulated.CommittedCount);
        Assert.Empty(await fixture.CatalogPersistence.ListCategoriesAsync(fixture.TenantContext));

        var replay = await fixture.Service.ReplayQuarantinedRowAsync(
            fixture.Context,
            simulated.Id,
            quarantine.Id,
            new MasterDataImportReplayRequest(
                new Dictionary<string, string?>
                {
                    ["code"] = "CAT-P1-REPLAYED",
                    ["englishName"] = "Replayed category"
                }),
            "p1-1-pre-execution-replay",
            simulated.Version);

        Assert.True(replay.Succeeded, replay.Code);
        var replayedBatch = replay.Value!;
        Assert.Equal(MasterDataImportStatus.Validated, replayedBatch.Status);
        Assert.Equal(2, replayedBatch.AcceptedCount);
        Assert.Equal(0, replayedBatch.QuarantinedCount);
        Assert.Equal(0, replayedBatch.CommittedCount);
        Assert.Equal(validatedAt, replayedBatch.CompletedAt);

        var replayedRows = await fixture.ImportPersistence.ListRowsAsync(fixture.TenantContext, simulated.Id);
        var original = Assert.Single(replayedRows, row => row.Id == quarantine.Id);
        var replayedRow = Assert.Single(replayedRows, row => row.ReplayOfRowId == quarantine.Id);
        Assert.False(original.IsCurrent);
        Assert.True(replayedRow.IsCurrent);
        Assert.Equal(MasterDataImportRowOutcome.Accepted, replayedRow.Outcome);
        Assert.Equal(MasterDataImportMutationDisposition.Eligible, replayedRow.MutationDisposition);
        Assert.Equal(quarantine.OriginalRowId ?? quarantine.Id, replayedRow.OriginalRowId);
        Assert.Equal(quarantine.OriginalRowNumber, replayedRow.OriginalRowNumber);
        Assert.Equal(1, replayedRow.ReplaySequence);
        Assert.Equal("p1-1-pre-execution-replay", replayedRow.ReplayIdempotencyKey);

        var preExecutionCategories = await fixture.CatalogPersistence.ListCategoriesAsync(fixture.TenantContext);
        Assert.Empty(preExecutionCategories);
        var preExecutionAudit = await fixture.ImportPersistence.ListAuditAsync(fixture.TenantContext, simulated.Id);
        Assert.Contains(preExecutionAudit, item => item.OperationId == "master-data.import.row.replayed");
        Assert.DoesNotContain(preExecutionAudit, item => item.OperationId == "master-data.import.batch.executed");
        Assert.DoesNotContain(
            preExecutionAudit,
            item => item.OperationId == "master-data.import.row.mutated" && item.RowId == replayedRow.Id);

        var executed = await fixture.Service.ExecuteAsync(
            fixture.Context,
            simulated.Id,
            replayedBatch.Version);

        Assert.True(executed.Succeeded, executed.Code);
        Assert.Equal(MasterDataImportStatus.Completed, executed.Value!.Status);
        Assert.Equal(2, executed.Value.CommittedCount);
        Assert.Equal(0, executed.Value.QuarantinedCount);

        var categories = await fixture.CatalogPersistence.ListCategoriesAsync(fixture.TenantContext);
        Assert.Equal(2, categories.Count);
        Assert.All(
            categories.GroupBy(item => item.Code, StringComparer.Ordinal),
            group => Assert.Single(group));

        var executedAudit = await fixture.ImportPersistence.ListAuditAsync(fixture.TenantContext, simulated.Id);
        Assert.Contains(executedAudit, item => item.OperationId == "master-data.import.batch.executed");
        Assert.Equal(
            2,
            executedAudit.Count(item => item.OperationId == "master-data.import.row.mutated"));
    }

    [Fact]
    public async Task Post_execution_replay_commits_only_the_corrected_row_and_repeated_key_is_idempotent()
    {
        await using var fixture = await ImportFixture.CreateAsync();
        var simulated = await fixture.CreateAndSimulateAsync(MasterDataImportMode.Commit);
        var executed = await fixture.Service.ExecuteAsync(fixture.Context, simulated.Id, simulated.Version);

        Assert.True(executed.Succeeded, executed.Code);
        Assert.Equal(MasterDataImportStatus.CompletedWithErrors, executed.Value!.Status);
        Assert.Equal(1, executed.Value.CommittedCount);
        Assert.Single(await fixture.CatalogPersistence.ListCategoriesAsync(fixture.TenantContext));

        var quarantine = Assert.Single(
            await fixture.ImportPersistence.ListRowsAsync(fixture.TenantContext, simulated.Id),
            row => row.IsCurrent && row.Outcome == MasterDataImportRowOutcome.Quarantined);
        var replayKey = "p1-1-post-execution-replay";
        var replay = await fixture.Service.ReplayQuarantinedRowAsync(
            fixture.Context,
            simulated.Id,
            quarantine.Id,
            new MasterDataImportReplayRequest(
                new Dictionary<string, string?>
                {
                    ["code"] = "CAT-P1-REPLAYED",
                    ["englishName"] = "Replayed category"
                }),
            replayKey,
            executed.Value.Version);

        Assert.True(replay.Succeeded, replay.Code);
        Assert.Equal(MasterDataImportStatus.Completed, replay.Value!.Status);
        Assert.Equal(2, replay.Value.CommittedCount);

        var rowsAfterReplay = await fixture.ImportPersistence.ListRowsAsync(fixture.TenantContext, simulated.Id);
        var replayedRow = Assert.Single(rowsAfterReplay, row => row.ReplayOfRowId == quarantine.Id);
        Assert.Equal(MasterDataImportRowOutcome.Accepted, replayedRow.Outcome);
        Assert.Equal(MasterDataImportMutationDisposition.Committed, replayedRow.MutationDisposition);

        var auditAfterReplay = await fixture.ImportPersistence.ListAuditAsync(fixture.TenantContext, simulated.Id);
        var mutationAuditCount = auditAfterReplay.Count(item => item.OperationId == "master-data.import.row.mutated");
        var categoriesAfterReplay = await fixture.CatalogPersistence.ListCategoriesAsync(fixture.TenantContext);
        Assert.Equal(2, categoriesAfterReplay.Count);
        Assert.All(
            categoriesAfterReplay.GroupBy(item => item.Code, StringComparer.Ordinal),
            group => Assert.Single(group));

        var repeated = await fixture.Service.ReplayQuarantinedRowAsync(
            fixture.Context,
            simulated.Id,
            quarantine.Id,
            new MasterDataImportReplayRequest(
                new Dictionary<string, string?>
                {
                    ["code"] = "CAT-P1-REPLAYED",
                    ["englishName"] = "Replayed category"
                }),
            replayKey,
            replay.Value.Version);

        Assert.True(repeated.Succeeded, repeated.Code);
        Assert.Equal("idempotent_replay", repeated.Code);
        Assert.Equal(rowsAfterReplay.Count, (await fixture.ImportPersistence.ListRowsAsync(fixture.TenantContext, simulated.Id)).Count);
        Assert.Equal(mutationAuditCount, (await fixture.ImportPersistence.ListAuditAsync(fixture.TenantContext, simulated.Id))
            .Count(item => item.OperationId == "master-data.import.row.mutated"));
        Assert.Equal(2, (await fixture.CatalogPersistence.ListCategoriesAsync(fixture.TenantContext)).Count);
    }

    [Fact]
    public async Task Dry_run_replay_remains_validated_and_does_not_mutate_master_data()
    {
        await using var fixture = await ImportFixture.CreateAsync();
        var simulated = await fixture.CreateAndSimulateAsync(MasterDataImportMode.DryRun);
        var quarantine = Assert.Single(
            await fixture.ImportPersistence.ListRowsAsync(fixture.TenantContext, simulated.Id),
            row => row.IsCurrent && row.Outcome == MasterDataImportRowOutcome.Quarantined);

        var replay = await fixture.Service.ReplayQuarantinedRowAsync(
            fixture.Context,
            simulated.Id,
            quarantine.Id,
            new MasterDataImportReplayRequest(
                new Dictionary<string, string?>
                {
                    ["code"] = "CAT-P1-REPLAYED",
                    ["englishName"] = "Replayed category"
                }),
            "p1-1-dry-run-replay",
            simulated.Version);

        Assert.True(replay.Succeeded, replay.Code);
        Assert.Equal(MasterDataImportStatus.Validated, replay.Value!.Status);
        Assert.Equal(0, replay.Value.CommittedCount);
        Assert.Empty(await fixture.CatalogPersistence.ListCategoriesAsync(fixture.TenantContext));

        var rows = await fixture.ImportPersistence.ListRowsAsync(fixture.TenantContext, simulated.Id);
        var replayedRow = Assert.Single(rows, row => row.ReplayOfRowId == quarantine.Id);
        Assert.Equal(MasterDataImportRowOutcome.Accepted, replayedRow.Outcome);
        Assert.Equal(MasterDataImportMutationDisposition.Eligible, replayedRow.MutationDisposition);
        Assert.DoesNotContain(
            await fixture.ImportPersistence.ListAuditAsync(fixture.TenantContext, simulated.Id),
            item => item.OperationId == "master-data.import.batch.executed");
    }

    private sealed class GrantingCapabilityResolver : IMasterDataCapabilityResolver
    {
        private static readonly IReadOnlySet<MasterDataCapability> Capabilities =
            Enum.GetValues<MasterDataCapability>().ToHashSet();

        public IReadOnlySet<MasterDataCapability> Resolve(MasterDataRequestContext context) => Capabilities;
    }

    private sealed class ImportFixture : IAsyncDisposable
    {
        private readonly SqliteConnection connection;

        private ImportFixture(
            SqliteConnection connection,
            TenantContext tenantContext,
            MasterDataRequestContext context,
            MasterDataImportService service,
            MasterDataImportPersistence importPersistence,
            MasterDataCatalogPersistence catalogPersistence)
        {
            this.connection = connection;
            TenantContext = tenantContext;
            Context = context;
            Service = service;
            ImportPersistence = importPersistence;
            CatalogPersistence = catalogPersistence;
        }

        public TenantContext TenantContext { get; }

        public MasterDataRequestContext Context { get; }

        public MasterDataImportService Service { get; }

        public MasterDataImportPersistence ImportPersistence { get; }

        public MasterDataCatalogPersistence CatalogPersistence { get; }

        public static async Task<ImportFixture> CreateAsync()
        {
            var connection = new SqliteConnection("Data Source=:memory:");
            await connection.OpenAsync();
            var options = new DbContextOptionsBuilder()
                .UseSqlite(connection)
                .Options;
            var tenantContext = TenantContext.ForOrdinaryMembership(
                new TenantId(TenantId),
                new MembershipReference(Guid.NewGuid()),
                new ScopeReference($"Company:{TenantId:D}"),
                new CorrelationId("corr-import-replay-p1-1"),
                ActorId);
            await using (var db = new MasterDataDbContext(options, tenantContext))
            {
                await db.Database.EnsureCreatedAsync();
            }

            var catalogPersistence = new MasterDataCatalogPersistence(options);
            var importPersistence = new MasterDataImportPersistence(options);
            var processors = new MasterDataImportProcessorRegistry(
                MasterDataImportProcessorFactory.Create(
                    catalogPersistence,
                    catalogPersistence,
                    new UnavailableMasterDataCurrencyPaymentTermPersistence(),
                    new UnavailableMasterDataTaxPersistence(),
                    new UnavailableMasterDataExchangeRatePersistence(),
                    new UnavailableMasterDataPriceListPersistence(),
                    new UnavailableSupplierPersistence(),
                    new UnavailableCustomerPersistence()));
            var service = new MasterDataImportService(
                new MasterDataImportAuthorizationComposition(new GrantingCapabilityResolver()),
                importPersistence,
                processors);
            var foundation = FoundationRequestContext.ForTenant(
                ActorId,
                SessionId,
                tenantContext,
                "master-data.import");
            var context = Assert.IsType<MasterDataRequestContext>(new MasterDataTenantContextResolver().Resolve(foundation).Context);

            return new ImportFixture(
                connection,
                tenantContext,
                context,
                service,
                importPersistence,
                catalogPersistence);
        }

        public async Task<MasterDataImportBatchRecord> CreateAndSimulateAsync(MasterDataImportMode mode)
        {
            var created = await Service.CreateBatchAsync(
                Context,
                new MasterDataImportBatchRequest(
                    MasterDataResourceKind.ProductCategory,
                    new MasterDataImportSourceRequest("p1-1-test", "p1-1.json", Guid.NewGuid().ToString("N")),
                    null,
                    MasterDataImportDuplicatePolicy.Reject,
                    mode,
                    [
                        new MasterDataImportRowInput(
                            1,
                            new Dictionary<string, string?>
                            {
                                ["code"] = "CAT-P1-VALID",
                                ["englishName"] = "Valid category"
                            }),
                        new MasterDataImportRowInput(
                            2,
                            new Dictionary<string, string?>
                            {
                                ["code"] = "CAT-P1-QUARANTINED",
                                ["englishName"] = "Quarantined category",
                                ["parentCategoryId"] = Guid.NewGuid().ToString("D")
                            })
                    ]),
                $"p1-1-create-{Guid.NewGuid():N}");
            Assert.True(created.Succeeded, created.Code);

            var simulated = await Service.SimulateAsync(Context, created.Value!.Id);
            Assert.True(simulated.Succeeded, simulated.Code);
            return simulated.Value!;
        }

        public async ValueTask DisposeAsync() => await connection.DisposeAsync();
    }
}
