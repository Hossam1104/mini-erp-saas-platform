using System.Data;
using System.Data.Common;
using System.Reflection;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Migrations;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.App.Modules.Finance;
using MiniErp.App.BuildingBlocks.Rest;
using MiniErp.App.Modules.Inventory;
using MiniErp.App.Modules.MasterData;
using MiniErp.Contracts.Modules.Finance;
using MiniErp.Contracts.Modules.Foundation;
using MiniErp.Contracts.Modules.Inventory;
using MiniErp.Contracts.Modules.MasterData;
using MiniErp.Infrastructure.Persistence;
using MiniErp.Infrastructure.Persistence.Migrations.Inventory;
using MiniErp.Infrastructure.Persistence.Modules.BusinessParties;
using MiniErp.Infrastructure.Persistence.Modules.Inventory;
using MiniErp.Infrastructure.Persistence.Modules.Finance;
using MiniErp.Infrastructure.Persistence.Modules.MasterData;
using MiniErp.Infrastructure.Persistence.Modules.Procurement;
using Xunit;

namespace MiniErp.ArchitectureTests;

[CollectionDefinition(Name, DisableParallelization = true)]
public sealed class SqlServerSafetyCollection : ICollectionFixture<SqlServerSafetyFixture>
{
    public const string Name = "SQL Server safety harness";
}

/// <summary>
/// The exact configuration-safety check the SQL Server harness relies on to
/// refuse a missing, non-LocalDB or non-disposable connection string. Extracted
/// so a test can exercise the real rejection path directly instead of
/// re-asserting an already-accepted connection string against constants.
/// </summary>
internal static class SqlServerSafetyConfigurationValidator
{
    internal const string RequiredDataSource = @"(localdb)\MSSQLLocalDB";
    internal const string DisposableDatabasePattern = "^MiniErpFoundation_[A-Za-z0-9_]+$";

    public static SqlConnectionStringBuilder ValidateSafeConnectionString(string? rawConnectionString)
    {
        if (string.IsNullOrWhiteSpace(rawConnectionString))
        {
            throw new InvalidOperationException(
                "MESP_SQLSERVER_SAFETY_CONNECTION_STRING is required for the disposable SQL Server safety harness. "
                + "Set it to a LocalDB integrated-security connection targeting a MiniErpFoundation_* database. "
                + "Do not point it at the persistent MESP runtime database.");
        }

        var builder = new SqlConnectionStringBuilder(rawConnectionString);
        if (!string.Equals(builder.DataSource, RequiredDataSource, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "The disposable SQL Server safety harness accepts only the machine-supported LocalDB instance "
                + $"'(localdb)\\MSSQLLocalDB'. The supplied MESP_SQLSERVER_SAFETY_CONNECTION_STRING targets a "
                + "different server and is rejected to protect any persistent database.");
        }

        if (!System.Text.RegularExpressions.Regex.IsMatch(
                builder.InitialCatalog,
                DisposableDatabasePattern,
                System.Text.RegularExpressions.RegexOptions.CultureInvariant))
        {
            throw new InvalidOperationException(
                "The SQL Server safety harness requires a disposable database whose name matches "
                + "the MiniErpFoundation_<AlphaNumeric> pattern. The supplied MESP_SQLSERVER_SAFETY_CONNECTION_STRING "
                + "targets a database outside that pattern and is rejected to protect any persistent database.");
        }

        return builder;
    }
}

/// <summary>
/// Disposable SQL Server LocalDB fixture. The safety checks intentionally fail
/// when an explicit, safe LocalDB connection is not supplied.
/// </summary>
public sealed class SqlServerSafetyFixture : IAsyncLifetime
{
    private string _connectionString = string.Empty;
    private string _inventoryConnectionString = string.Empty;
    private DbContextOptions _options = null!;

    public TenantContext TenantA { get; private set; } = null!;

    public TenantContext TenantB { get; private set; } = null!;

    public TenantPersistenceSessionFactory Factory { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        // Reads only the dedicated safety variable. The persistent runtime variable
        // MESP_SQLSERVER_CONNECTION_STRING must never be used here: the harness
        // creates and drops its database, which would be destructive against MESP.
        var rawConnectionString = Environment.GetEnvironmentVariable(
            "MESP_SQLSERVER_SAFETY_CONNECTION_STRING");

        var builder = SqlServerSafetyConfigurationValidator.ValidateSafeConnectionString(rawConnectionString);

        builder.IntegratedSecurity = true;
        builder.TrustServerCertificate = true;
        builder.ConnectTimeout = 15;
        _connectionString = builder.ConnectionString;

        await EnsureDatabaseExistsAsync(builder);

        TenantA = TenantContext.ForOrdinaryMembership(
            NewTenantId(),
            new MembershipReference(Guid.NewGuid()),
            correlationId: new CorrelationId("sql-a"));
        TenantB = TenantContext.ForSupportGrant(
            NewTenantId(),
            new SupportGrantReference(Guid.NewGuid(), Guid.NewGuid()),
            correlationId: new CorrelationId("sql-b"));

        _inventoryConnectionString = _connectionString;
        _options = SqlServerMigrationConfiguration.Configure(
            _connectionString,
            SqlServerMigrationConfiguration.TenancyHistoryTable);

        // This is intentionally the same disposable catalog for every module.
        // It proves the committed Development migration order and shared-table
        // ownership instead of validating isolated module databases.
        await using (var tenancy = new TenantPersistenceDbContext(_options, TenantA))
        {
            await tenancy.Database.MigrateAsync();
        }

        await using (var masterData = new MasterDataDbContext(
                         SqlServerMigrationConfiguration.Configure(_connectionString, SqlServerMigrationConfiguration.MasterDataHistoryTable),
                         TenantA))
        {
            await masterData.Database.MigrateAsync();
        }

        await using (var businessParties = new BusinessPartiesDbContext(
                         SqlServerMigrationConfiguration.Configure(_connectionString, SqlServerMigrationConfiguration.BusinessPartiesHistoryTable),
                         TenantA))
        {
            await businessParties.Database.MigrateAsync();
        }

        await using (var procurement = new ProcurementDbContext(
                         SqlServerMigrationConfiguration.Configure(_connectionString, SqlServerMigrationConfiguration.ProcurementHistoryTable),
                         TenantA))
        {
            await procurement.Database.MigrateAsync();
        }

        await using (var inventory = new InventoryDbContext(
                         SqlServerMigrationConfiguration.Configure(_connectionString, SqlServerMigrationConfiguration.InventoryHistoryTable),
                         TenantA))
        {
            await inventory.Database.MigrateAsync();
        }

        await using (var finance = new FinanceDbContext(
                         SqlServerMigrationConfiguration.Configure(_connectionString, SqlServerMigrationConfiguration.FinanceHistoryTable),
                         TenantA))
        {
            await finance.Database.MigrateAsync();
        }

        await CreateProbeTablesAsync();
        Factory = new TenantPersistenceSessionFactory(_options);
    }

    public async Task DisposeAsync()
    {
        if (string.IsNullOrWhiteSpace(_connectionString))
        {
            return;
        }

        await DropDatabaseAsync(_connectionString);
        _inventoryConnectionString = string.Empty;
    }

    internal TenantPersistenceDbContext CreateDbContext(TenantContext tenantContext)
    {
        return new TenantPersistenceDbContext(_options, tenantContext);
    }

    public async Task<SqlConnection> OpenConnectionAsync()
    {
        var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();
        return connection;
    }

    public async Task<SqlConnection> OpenInventoryConnectionAsync()
    {
        var connection = new SqlConnection(_inventoryConnectionString);
        await connection.OpenAsync();
        return connection;
    }

    public async Task AddAsync(TenantContext tenantContext, TenantOwnedRecord record)
    {
        await using var session = Factory.Create(tenantContext);
        session.Records.Add(record);
        await session.SaveChangesAsync();
    }

    private static TenantId NewTenantId() => new(Guid.NewGuid());

    private static async Task EnsureDatabaseExistsAsync(SqlConnectionStringBuilder databaseBuilder)
    {
        var databaseName = databaseBuilder.InitialCatalog;
        var masterBuilder = new SqlConnectionStringBuilder(databaseBuilder.ConnectionString)
        {
            InitialCatalog = "master"
        };

        await using var connection = new SqlConnection(masterBuilder.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"IF DB_ID(N'{EscapeLiteral(databaseName)}') IS NULL "
            + $"CREATE DATABASE [{EscapeIdentifier(databaseName)}];";
        await command.ExecuteNonQueryAsync();
    }

    private static async Task DropDatabaseAsync(string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return;
        }

        var builder = new SqlConnectionStringBuilder(connectionString);
        var databaseName = builder.InitialCatalog;
        builder.InitialCatalog = "master";
        await using var connection = new SqlConnection(builder.ConnectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = $"IF DB_ID(N'{EscapeLiteral(databaseName)}') IS NOT NULL "
            + $"BEGIN ALTER DATABASE [{EscapeIdentifier(databaseName)}] SET SINGLE_USER WITH ROLLBACK IMMEDIATE; "
            + $"DROP DATABASE [{EscapeIdentifier(databaseName)}]; END;";
        await command.ExecuteNonQueryAsync();
    }

    private async Task CreateProbeTablesAsync()
    {
        await using var connection = await OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            IF SCHEMA_ID(N'test') IS NULL EXEC(N'CREATE SCHEMA [test]');
            IF OBJECT_ID(N'[test].[DurableWorkProbe]', N'U') IS NULL
            BEGIN
                CREATE TABLE [test].[DurableWorkProbe]
                (
                    [WorkId] uniqueidentifier NOT NULL CONSTRAINT [PK_DurableWorkProbe] PRIMARY KEY,
                    [TenantId] uniqueidentifier NOT NULL,
                    [EventId] uniqueidentifier NOT NULL,
                    [LeaseOwner] nvarchar(100) NULL,
                    [LeaseVersion] int NOT NULL CONSTRAINT [DF_DurableWorkProbe_LeaseVersion] DEFAULT (0),
                    [State] nvarchar(30) NOT NULL,
                    [Kind] nvarchar(30) NOT NULL,
                    [Payload] nvarchar(200) NOT NULL
                );
                CREATE UNIQUE INDEX [UX_DurableWorkProbe_Tenant_Event]
                    ON [test].[DurableWorkProbe] ([TenantId], [EventId]);
            END;
            """;
        await command.ExecuteNonQueryAsync();
    }

    private static string EscapeLiteral(string value) => value.Replace("'", "''", StringComparison.Ordinal);

    private static string EscapeIdentifier(string value) => value.Replace("]", "]]", StringComparison.Ordinal);
}

[Collection(SqlServerSafetyCollection.Name)]
public sealed class SqlServerSafetyTests
{
    private readonly SqlServerSafetyFixture _fixture;

    public SqlServerSafetyTests(SqlServerSafetyFixture fixture)
    {
        _fixture = fixture;
    }

    [Fact]
    public async Task All_module_sql_server_migrations_apply_in_development_order_to_one_disposable_database()
    {
        var connectionString = await GetConnectionStringAsync();
        var tenancyOptions = SqlServerMigrationConfiguration.Configure(connectionString, SqlServerMigrationConfiguration.TenancyHistoryTable);
        var masterDataOptions = SqlServerMigrationConfiguration.Configure(connectionString, SqlServerMigrationConfiguration.MasterDataHistoryTable);
        var businessPartiesOptions = SqlServerMigrationConfiguration.Configure(connectionString, SqlServerMigrationConfiguration.BusinessPartiesHistoryTable);
        var procurementOptions = SqlServerMigrationConfiguration.Configure(connectionString, SqlServerMigrationConfiguration.ProcurementHistoryTable);
        var inventoryOptions = SqlServerMigrationConfiguration.Configure(connectionString, SqlServerMigrationConfiguration.InventoryHistoryTable);
        var financeOptions = SqlServerMigrationConfiguration.Configure(connectionString, SqlServerMigrationConfiguration.FinanceHistoryTable);

        await using (var tenancy = new TenantPersistenceDbContext(tenancyOptions, _fixture.TenantA))
        {
            Assert.Equal(["20260815225855_InitialTenancy"], (await tenancy.Database.GetAppliedMigrationsAsync()).ToArray());
            Assert.Empty(await tenancy.Database.GetPendingMigrationsAsync());
        }

        await using (var masterData = new MasterDataDbContext(masterDataOptions, _fixture.TenantA))
        {
            Assert.Equal(["20260815225908_InitialMasterData", "20260816073832_SharedTenantRuntimeModel"], (await masterData.Database.GetAppliedMigrationsAsync()).ToArray());
            Assert.Empty(await masterData.Database.GetPendingMigrationsAsync());
        }

        await using (var businessParties = new BusinessPartiesDbContext(businessPartiesOptions, _fixture.TenantA))
        {
            Assert.Equal(["20260815225921_InitialBusinessParties", "20260816073853_SharedTenantRuntimeModel"], (await businessParties.Database.GetAppliedMigrationsAsync()).ToArray());
            Assert.Empty(await businessParties.Database.GetPendingMigrationsAsync());
        }

        await using (var procurement = new ProcurementDbContext(procurementOptions, _fixture.TenantA))
        {
            Assert.Equal(
                [
                    "20260815225933_InitialProcurement",
                    "20260816073856_SharedTenantRuntimeModel",
                    "20260817143432_PurchaseOrderAndSupplierConfirmation",
                    "20260817211222_AddPurchaseOrderAuditRequestFingerprint",
                    "20260818103736_PurchaseOrderCommercialIntegrityAndDurableReplay",
                    "20260819102200_GoodsReceiptAndPurchaseInvoiceHandoff",
                    "20260820094805_ThreeWayMatchingAndDeclaredInvoiceEvidence",
                    "20260820102459_MESP126ResolutionPolicyEvidence",
                    "20260821031935_MESP127SupplierReturnEvidence"
                ],
                (await procurement.Database.GetAppliedMigrationsAsync()).ToArray());
            Assert.Empty(await procurement.Database.GetPendingMigrationsAsync());
        }

        await using (var inventory = new InventoryDbContext(inventoryOptions, _fixture.TenantA))
        {
            Assert.Equal(
                [
                    "20260821113311_MESP128InventoryLedgerFoundation",
                    "20260821132738_MESP128StockIntegrityRemediation",
                    "20260821213832_MESP128OpusStockIntegrityRemediation",
                    "20260822092802_MESP129PhysicalStockMovements",
                    "20260822194250_MESP130StockControlAndCorrections",
                    "20260822220126_MESP130SolAcceptanceRemediation",
                    "20260822220521_MESP130SolAcceptanceCountApproval",
                    "20260823104702_MESP130InventoryCountLedgerFence",
                    "20260823124304_MESP131MovingWeightedAverageValuation",
                    "20260823180537_MESP131SolFinancialIntegrityRemediation",
                    "20260823225921_MESP131SolFinalValuationIntegrity"
                ],
                (await inventory.Database.GetAppliedMigrationsAsync()).ToArray());
            Assert.Empty(await inventory.Database.GetPendingMigrationsAsync());
        }

        await using (var finance = new FinanceDbContext(financeOptions, _fixture.TenantA))
        {
            Assert.Equal(
                [
                    "20260824125115_MESP132FinanceFoundation",
                    "20260824152331_MESP132SolFinanceCorrectnessRemediation"
                ],
                (await finance.Database.GetAppliedMigrationsAsync()).ToArray());
            Assert.Empty(await finance.Database.GetPendingMigrationsAsync());
        }

        await using var connection = await _fixture.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*)
            FROM INFORMATION_SCHEMA.TABLES
            WHERE TABLE_SCHEMA = N'inventory'
              AND TABLE_NAME IN (N'OpeningBalances', N'OpeningBalanceRows', N'OpeningBalanceHistory', N'StockLedgerMovements', N'Transfers', N'TransferLines', N'TransferEvents', N'Reservations', N'ReservationHistory', N'AuditEvents', N'IdempotencyEntries', N'ConcurrencyAnchors', N'ReasonCodes', N'Adjustments', N'AdjustmentLines', N'Counts', N'CountSnapshots', N'CountLines', N'StockIssues', N'StockIssueLines', N'ControlHistory', N'CompanyLedgerSequenceAnchors', N'ValuationPolicies', N'ValuationScopeAnchors', N'ValuationStates', N'MovementValuationEvents', N'ValuationRuns', N'FinanceValuationHandoffs');
            SELECT COUNT(*)
            FROM INFORMATION_SCHEMA.TABLES
            WHERE TABLE_SCHEMA = N'tenancy'
              AND TABLE_NAME = N'TenantOwnedRecords';
            SELECT COUNT(*)
            FROM sys.indexes AS indexes
            INNER JOIN sys.tables AS tables ON tables.object_id = indexes.object_id
            INNER JOIN sys.schemas AS schemas ON schemas.schema_id = tables.schema_id
            WHERE schemas.name = N'tenancy'
              AND tables.name = N'TenantOwnedRecords'
              AND indexes.name = N'IX_TenantOwnedRecords_TenantId_BusinessKey';
            """;
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal(28, reader.GetInt32(0));
        Assert.True(await reader.NextResultAsync());
        Assert.True(await reader.ReadAsync());
        Assert.Equal(1, reader.GetInt32(0));
        Assert.True(await reader.NextResultAsync());
        Assert.True(await reader.ReadAsync());
        Assert.Equal(1, reader.GetInt32(0));
    }

    [Fact]
    public async Task MESP132_sql_server_creates_only_module_owned_tenant_finance_tables()
    {
        await using var connection = await _fixture.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*)
            FROM INFORMATION_SCHEMA.TABLES
            WHERE TABLE_SCHEMA = N'finance'
              AND TABLE_NAME IN (N'Accounts', N'FiscalCalendars', N'FiscalYears', N'FiscalPeriods', N'CostCenters', N'PostingRules', N'Journals', N'JournalLines', N'AuditEvents', N'IdempotencyEntries', N'SourceEffects');
            SELECT COUNT(*)
            FROM sys.indexes AS indexes
            INNER JOIN sys.tables AS tables ON tables.object_id = indexes.object_id
            INNER JOIN sys.schemas AS schemas ON schemas.schema_id = tables.schema_id
            WHERE schemas.name = N'finance'
              AND tables.name = N'SourceEffects'
              AND indexes.name = N'IX_SourceEffects_TenantId_CompanyId_SourceContract_SourceEvidenceId_SourceEvidenceVersion';
            """;
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal(11, reader.GetInt32(0));
        Assert.True(await reader.NextResultAsync());
        Assert.True(await reader.ReadAsync());
        Assert.Equal(1, reader.GetInt32(0));
    }

    [Fact]
    public async Task MESP132_sql_server_period_close_and_post_have_one_consistent_committed_order()
    {
        var scenario = await CreateApprovedFinanceJournalAsync();
        var post = SafeFinanceOperationAsync(() => scenario.FirstPersistence.PostJournalAsync(
            scenario.FirstContext,
            new FinanceJournalActionCommand(scenario.ApprovedJournal.Id, scenario.ApprovedJournal.Version, "sql-period-post", "sql-period-post", "sql-period-post")));
        var close = SafeFinanceOperationAsync(() => scenario.SecondPersistence.SetPeriodStateAsync(
            scenario.SecondContext,
            new FinancePeriodStateCommand(scenario.Period.Id, FinanceFiscalPeriodState.Closed, "SQL race close", scenario.Period.Version, "sql-period-close", "sql-period-close")));

        await Task.WhenAll(post, close);
        var posted = await post;
        var closed = await close;

        Assert.True(closed.Succeeded, closed.Code);
        Assert.True(posted.Succeeded || posted.Code is "period_closed" or "concurrency_conflict", posted.Code);
        await using var db = new FinanceDbContext(scenario.Options, scenario.FirstContext.TenantContext);
        var persistedPeriod = await db.FiscalPeriods.SingleAsync(item => item.Id == scenario.Period.Id);
        var persistedJournal = await db.Journals.SingleAsync(item => item.Id == scenario.ApprovedJournal.Id);
        Assert.Equal(FinanceFiscalPeriodState.Closed, persistedPeriod.State);
        if (posted.Succeeded)
        {
            Assert.Equal(FinanceJournalStatus.Posted, persistedJournal.Status);
            Assert.Equal(scenario.Period.Id, persistedJournal.FiscalPeriodId);
        }
        else
        {
            Assert.NotEqual(FinanceJournalStatus.Posted, persistedJournal.Status);
        }
    }

    [Fact]
    public async Task MESP132_sql_server_account_restriction_and_post_have_one_consistent_committed_order()
    {
        var scenario = await CreateApprovedFinanceJournalAsync(foreignCurrency: true);
        var restrictiveChange = SafeFinanceOperationAsync(() => scenario.SecondPersistence.EditAccountAsync(
            scenario.SecondContext,
            new FinanceAccountCommand(
                scenario.CompanyId,
                scenario.DebitAccount.Code,
                scenario.DebitAccount.EnglishName,
                scenario.DebitAccount.ArabicName,
                scenario.DebitAccount.ParentAccountId,
                scenario.DebitAccount.AccountType,
                scenario.DebitAccount.IsPostingAccount,
                FinanceCurrencyBehavior.FunctionalOnly,
                scenario.DebitAccount.EffectiveFrom,
                scenario.DebitAccount.EffectiveTo,
                scenario.DebitAccount.Id,
                scenario.DebitAccount.Version,
                "sql-account-restrict",
                "sql-account-restrict")));
        var post = SafeFinanceOperationAsync(() => scenario.FirstPersistence.PostJournalAsync(
            scenario.FirstContext,
            new FinanceJournalActionCommand(scenario.ApprovedJournal.Id, scenario.ApprovedJournal.Version, "sql-account-post", "sql-account-post", "sql-account-post")));

        await Task.WhenAll(post, restrictiveChange);
        var posted = await post;
        var changed = await restrictiveChange;

        Assert.True(changed.Succeeded, changed.Code);
        Assert.True(posted.Succeeded || posted.Code is "account_currency_behavior_invalid" or "concurrency_conflict", posted.Code);
        await using var db = new FinanceDbContext(scenario.Options, scenario.FirstContext.TenantContext);
        var persistedAccount = await db.Accounts.SingleAsync(item => item.Id == scenario.DebitAccount.Id);
        var persistedJournal = await db.Journals.SingleAsync(item => item.Id == scenario.ApprovedJournal.Id);
        Assert.Equal(FinanceCurrencyBehavior.FunctionalOnly, persistedAccount.CurrencyBehavior);
        if (posted.Succeeded)
        {
            Assert.Equal(FinanceJournalStatus.Posted, persistedJournal.Status);
            var postedDebitLine = await db.JournalLines.Where(item => item.JournalId == persistedJournal.Id).SingleAsync(item => item.Debit > 0m);
            Assert.Equal(37.50m, postedDebitLine.FunctionalDebit);
        }
        else
        {
            Assert.NotEqual(FinanceJournalStatus.Posted, persistedJournal.Status);
        }
    }

    [Fact]
    public async Task MESP132_sql_server_same_approved_journal_concurrent_post_has_one_authoritative_effect()
    {
        var scenario = await CreateApprovedFinanceJournalAsync();
        var first = SafeFinanceOperationAsync(() => scenario.FirstPersistence.PostJournalAsync(
            scenario.FirstContext,
            new FinanceJournalActionCommand(scenario.ApprovedJournal.Id, scenario.ApprovedJournal.Version, "sql-same-journal-a", "sql-same-journal-a", "sql-same-journal-a")));
        var second = SafeFinanceOperationAsync(() => scenario.SecondPersistence.PostJournalAsync(
            scenario.SecondContext,
            new FinanceJournalActionCommand(scenario.ApprovedJournal.Id, scenario.ApprovedJournal.Version, "sql-same-journal-b", "sql-same-journal-b", "sql-same-journal-b")));

        var results = await Task.WhenAll(first, second);
        Assert.Equal(1, results.Count(item => item.Succeeded));
        Assert.Single(results, item => !item.Succeeded && item.Code is "concurrency_conflict" or "source_effect_exists");

        await using var db = new FinanceDbContext(scenario.Options, scenario.FirstContext.TenantContext);
        Assert.Equal(FinanceJournalStatus.Posted, (await db.Journals.SingleAsync(item => item.Id == scenario.ApprovedJournal.Id)).Status);
        Assert.Equal(2, await db.JournalLines.CountAsync(item => item.JournalId == scenario.ApprovedJournal.Id));
        Assert.Equal(1, await db.AuditEvents.CountAsync(item => item.ResourceId == scenario.ApprovedJournal.Id && item.OperationId == "finance.journal.post" && item.Result == "Succeeded"));
    }

    [Fact]
    public async Task MESP132_sql_server_same_inventory_handoff_concurrent_processing_has_one_source_effect()
    {
        var connectionString = await GetConnectionStringAsync();
        var financeOptions = SqlServerMigrationConfiguration.Configure(connectionString, SqlServerMigrationConfiguration.FinanceHistoryTable);
        var inventoryOptions = SqlServerMigrationConfiguration.Configure(connectionString, SqlServerMigrationConfiguration.InventoryHistoryTable);
        var companyId = Guid.NewGuid();
        var inventoryContext = InventoryContext(_fixture.TenantA, "tenant.inventory.valuation.process");
        var tenantId = _fixture.TenantA.TenantId;
        var warehouseId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var unitId = Guid.NewGuid();
        await using (var inventoryDb = new InventoryDbContext(inventoryOptions, _fixture.TenantA))
        {
            inventoryDb.StockMovements.Add(SqlMovement(tenantId, companyId, warehouseId, productId, unitId, InventoryMovementDirection.Inbound, 1m, 25m, "SAR", "sql-finance-handoff-race"));
            await inventoryDb.SaveChangesAsync();
        }

        var inventory = new InventoryValuationPersistence(inventoryOptions, null, null, null);
        var policy = await inventory.CreatePolicyAsync(inventoryContext, new InventoryValuationPolicyCommand(Guid.NewGuid(), SqlPolicy(companyId, new DateOnly(2026, 1, 1)), inventoryContext.ActorId, DateTimeOffset.UtcNow, "sql-finance-handoff-policy", "sql-finance-handoff-policy", "sql-finance-handoff-policy"));
        Assert.True(policy.Succeeded, policy.Code);
        var processed = await inventory.ProcessAsync(inventoryContext, new InventoryValuationProcessCommand(companyId, null, warehouseId, productId, unitId, inventoryContext.ActorId, DateTimeOffset.UtcNow, "sql-finance-handoff-process", "sql-finance-handoff-process", "sql-finance-handoff-process"));
        Assert.True(processed.Succeeded, processed.Code);
        var handoff = Assert.Single(await inventory.ListFinanceHandoffsAsync(inventoryContext, new InventoryValuationQuery(companyId)));
        Assert.Equal(InventoryFinanceValuationHandoffStatus.ReadyForFinance, handoff.Status);

        var provider = new ConfiguredFinanceCompanyProvider([new FinanceCompanyOption(tenantId.Value, companyId, "SQL Handoff Company", "SAR")]);
        var approval = new SqlFinanceSourceApprovalPolicy(FinanceApprovalRequirement.NotRequired);
        var first = new FinancePersistence(financeOptions, provider, inventory, new UnavailableMasterDataExchangeRatePersistence(), approval);
        var second = new FinancePersistence(financeOptions, provider, inventory, new UnavailableMasterDataExchangeRatePersistence(), approval);
        var firstContext = FinanceContext("tenant.finance.handoff.process");
        var secondContext = FinanceContext("tenant.finance.handoff.process");
        var accounts = await CreateFinanceAccountsAsync(first, firstContext, companyId, "sql-handoff");
        var calendar = await first.CreateCalendarAsync(firstContext, new FinanceFiscalCalendarCommand(companyId, "SQL Handoff FY", Guid.NewGuid(), "sql-handoff-calendar", "sql-handoff-calendar"));
        Assert.True(calendar.Succeeded, calendar.Code);
        var year = await first.CreateYearAsync(firstContext, new FinanceFiscalYearCommand(calendar.Value!.Id, 2026, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), Guid.NewGuid(), "sql-handoff-year", "sql-handoff-year"));
        Assert.True(year.Succeeded, year.Code);
        var period = await first.CreatePeriodAsync(firstContext, new FinanceFiscalPeriodCommand(year.Value!.Id, 1, "2026", "2026", null, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), Guid.NewGuid(), "sql-handoff-period", "sql-handoff-period"));
        Assert.True(period.Succeeded, period.Code);
        var opened = await first.SetPeriodStateAsync(firstContext, new FinancePeriodStateCommand(period.Value!.Id, FinanceFiscalPeriodState.Open, null, period.Value.Version, "sql-handoff-open", "sql-handoff-open"));
        Assert.True(opened.Succeeded, opened.Code);
        var rule = await first.CreatePostingRuleAsync(firstContext, new FinancePostingRuleCommand(companyId, "inventory-valuation-finance.v1", "OpeningBalance:Inbound", accounts.Debit.Id, accounts.Credit.Id, false, new DateOnly(2026, 1, 1), null, Guid.NewGuid(), "sql-handoff-rule", "sql-handoff-rule"));
        Assert.True(rule.Succeeded, rule.Code);

        var firstProcess = SafeFinanceOperationAsync(() => first.ProcessHandoffAsync(firstContext, new FinanceHandoffProcessCommand(handoff.Id, "sql-handoff-process-a", "sql-handoff-process-a")));
        var secondProcess = SafeFinanceOperationAsync(() => second.ProcessHandoffAsync(secondContext, new FinanceHandoffProcessCommand(handoff.Id, "sql-handoff-process-b", "sql-handoff-process-b")));
        var results = await Task.WhenAll(firstProcess, secondProcess);

        Assert.Contains(results, item => item.Succeeded);
        Assert.All(results, item => Assert.True(item.Succeeded || item.Code is "concurrency_conflict" or "source_effect_exists" or "finance_conflict", item.Code));
        await using var financeDb = new FinanceDbContext(financeOptions, _fixture.TenantA);
        Assert.Equal(1, await financeDb.Journals.CountAsync(item => item.CompanyId == companyId && item.SourceContract == "inventory-valuation-finance.v1"));
        Assert.Equal(1, await financeDb.SourceEffects.CountAsync(item => item.CompanyId == companyId && item.SourceContract == "inventory-valuation-finance.v1"));
        var journalId = await financeDb.Journals.Where(journal => journal.CompanyId == companyId && journal.SourceContract == "inventory-valuation-finance.v1").Select(journal => journal.Id).SingleAsync();
        Assert.Equal(2, await financeDb.JournalLines.CountAsync(item => item.JournalId == journalId));
        Assert.Equal(1, await financeDb.AuditEvents.CountAsync(item => item.OperationId == "finance.journal.post" && item.Result == "Succeeded" && item.ResourceId == journalId));
    }

    [Fact]
    public async Task MESP132_sql_server_first_company_journal_sequence_concurrency_is_unique_or_safe_conflict()
    {
        var connectionString = await GetConnectionStringAsync();
        var options = SqlServerMigrationConfiguration.Configure(connectionString, SqlServerMigrationConfiguration.FinanceHistoryTable);
        var companyId = Guid.NewGuid();
        var provider = new ConfiguredFinanceCompanyProvider([new FinanceCompanyOption(_fixture.TenantA.TenantId.Value, companyId, "SQL Sequence Company", "SAR")]);
        var first = new FinancePersistence(options, provider, new UnavailableInventoryValuationPersistence(), new UnavailableMasterDataExchangeRatePersistence());
        var second = new FinancePersistence(options, provider, new UnavailableInventoryValuationPersistence(), new UnavailableMasterDataExchangeRatePersistence());
        var contextA = FinanceContext("tenant.finance.journal.create");
        var contextB = FinanceContext("tenant.finance.journal.create");
        var accounts = await CreateFinanceAccountsAsync(first, contextA, companyId, "sql-sequence");
        var calendar = await first.CreateCalendarAsync(contextA, new FinanceFiscalCalendarCommand(companyId, "SQL Sequence FY", Guid.NewGuid(), "sql-sequence-calendar", "sql-sequence-calendar"));
        var year = await first.CreateYearAsync(contextA, new FinanceFiscalYearCommand(calendar.Value!.Id, 2026, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), Guid.NewGuid(), "sql-sequence-year", "sql-sequence-year"));
        var period = await first.CreatePeriodAsync(contextA, new FinanceFiscalPeriodCommand(year.Value!.Id, 1, "2026-01", "January", null, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), Guid.NewGuid(), "sql-sequence-period", "sql-sequence-period"));
        Assert.True((await first.SetPeriodStateAsync(contextA, new FinancePeriodStateCommand(period.Value!.Id, FinanceFiscalPeriodState.Open, null, period.Value.Version, "sql-sequence-open", "sql-sequence-open"))).Succeeded);
        var commandA = SqlManualJournal(companyId, accounts, "sql-sequence-a");
        var commandB = SqlManualJournal(companyId, accounts, "sql-sequence-b");

        var results = await Task.WhenAll(
            SafeFinanceOperationAsync(() => first.CreateJournalAsync(contextA, commandA)),
            SafeFinanceOperationAsync(() => second.CreateJournalAsync(contextB, commandB)));

        Assert.All(results, item => Assert.True(item.Succeeded || item.Code is "concurrency_conflict" or "idempotency_conflict"));
        var sequences = results.Where(item => item.Succeeded).Select(item => item.Value!.JournalSequence).ToArray();
        Assert.Equal(sequences.Length, sequences.Distinct().Count());
        await using var db = new FinanceDbContext(options, _fixture.TenantA);
        var persistedSequences = await db.Journals.Where(item => item.CompanyId == companyId).Select(item => item.JournalSequence).ToArrayAsync();
        Assert.Equal(persistedSequences.Length, persistedSequences.Distinct().Count());
    }

    [Fact]
    public async Task MESP131_sql_server_uses_durable_company_ledger_sequence_for_legacy_and_new_movement_shape()
    {
        var options = new DbContextOptionsBuilder().UseSqlServer(await GetConnectionStringAsync()).Options;
        var tenantId = _fixture.TenantA.TenantId;
        var companyId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var unitId = Guid.NewGuid();
        var firstId = Guid.NewGuid();
        var secondId = Guid.NewGuid();
        var actorId = Guid.NewGuid();

        await using (var db = new InventoryDbContext(options, _fixture.TenantA))
        {
            db.StockMovements.AddRange(
                new InventoryStockMovementEntity(
                    tenantId, firstId, companyId, null, warehouseId, "WH-MWA", "MWA warehouse", productId,
                    "SKU-MWA", "MWA product", unitId, "EA", InventoryMovementDirection.Inbound, 10m, 10m, "SAR",
                    InventoryValuationStatus.Known, null, InventoryMovementSourceType.OpeningBalance, Guid.NewGuid(), Guid.NewGuid(), null,
                    DateOnly.FromDateTime(DateTime.UtcNow), actorId, "sql-mesp131-first", DateTimeOffset.UtcNow),
                new InventoryStockMovementEntity(
                    tenantId, secondId, companyId, null, warehouseId, "WH-MWA", "MWA warehouse", productId,
                    "SKU-MWA", "MWA product", unitId, "EA", InventoryMovementDirection.Inbound, 5m, 20m, "SAR",
                    InventoryValuationStatus.Known, null, InventoryMovementSourceType.StockAdjustment, Guid.NewGuid(), Guid.NewGuid(), null,
                    DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-10)), actorId, "sql-mesp131-second", DateTimeOffset.UtcNow.AddDays(-10)));
            await db.SaveChangesAsync();
        }

        await using var connection = new SqlConnection(await GetConnectionStringAsync());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT [Id], [LedgerSequence]
            FROM [inventory].[StockLedgerMovements]
            WHERE [TenantId] = @tenantId AND [CompanyId] = @companyId
            ORDER BY [LedgerSequence];
            SELECT [NextSequence]
            FROM [inventory].[CompanyLedgerSequenceAnchors]
            WHERE [TenantId] = @tenantId AND [CompanyId] = @companyId;
            """;
        command.Parameters.AddWithValue("@tenantId", tenantId.Value);
        command.Parameters.AddWithValue("@companyId", companyId);
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal(firstId, reader.GetGuid(0));
        Assert.Equal(1L, reader.GetInt64(1));
        Assert.True(await reader.ReadAsync());
        Assert.Equal(secondId, reader.GetGuid(0));
        Assert.Equal(2L, reader.GetInt64(1));
        Assert.True(await reader.NextResultAsync());
        Assert.True(await reader.ReadAsync());
        Assert.Equal(3L, reader.GetInt64(0));
    }

    [Fact]
    public async Task Shared_catalog_has_one_tenancy_owner_and_no_competing_inventory_tenant_table()
    {
        await using var connection = await _fixture.OpenConnectionAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT DB_NAME();
            SELECT COUNT(*)
            FROM INFORMATION_SCHEMA.TABLES
            WHERE TABLE_SCHEMA = N'tenancy'
              AND TABLE_NAME = N'TenantOwnedRecords';
            SELECT COUNT(*)
            FROM INFORMATION_SCHEMA.TABLES
            WHERE TABLE_SCHEMA = N'inventory'
              AND TABLE_NAME = N'TenantOwnedRecords';
            SELECT COUNT(*)
            FROM INFORMATION_SCHEMA.TABLES
            WHERE TABLE_SCHEMA = N'inventory'
              AND TABLE_NAME = N'StockLedgerMovements';
            """;

        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Matches("^MiniErpFoundation_[A-Za-z0-9_]+$", reader.GetString(0));
        Assert.True(await reader.NextResultAsync());
        Assert.True(await reader.ReadAsync());
        Assert.Equal(1, reader.GetInt32(0));
        Assert.True(await reader.NextResultAsync());
        Assert.True(await reader.ReadAsync());
        Assert.Equal(0, reader.GetInt32(0));
        Assert.True(await reader.NextResultAsync());
        Assert.True(await reader.ReadAsync());
        Assert.Equal(1, reader.GetInt32(0));
    }

    [Fact]
    public async Task Query_filter_is_evaluated_for_each_sql_server_tenant_context()
    {
        var recordA = new TenantOwnedRecord(_fixture.TenantA.TenantId, $"sql-a-{Guid.NewGuid():N}");
        var recordB = new TenantOwnedRecord(_fixture.TenantB.TenantId, $"sql-b-{Guid.NewGuid():N}");
        await _fixture.AddAsync(_fixture.TenantA, recordA);
        await _fixture.AddAsync(_fixture.TenantB, recordB);

        var reads = await Task.WhenAll(Enumerable.Range(0, 6).Select(async _ =>
        {
            await using var session = _fixture.Factory.Create(_fixture.TenantB);
            var records = await session.Records.ListAsync();
            return (records, foreign: await session.Records.FindAsync(recordA.Id));
        }));

        Assert.All(reads, result =>
        {
            Assert.Single(result.records);
            Assert.Equal(recordB.Id, result.records[0].Id);
            Assert.Null(result.foreign);
        });
    }

    [Fact]
    public async Task Stored_owner_guard_denies_forged_sql_server_update_and_delete()
    {
        var foreign = new TenantOwnedRecord(_fixture.TenantB.TenantId, $"sql-forged-{Guid.NewGuid():N}");
        await _fixture.AddAsync(_fixture.TenantB, foreign);

        await using (var updateSession = _fixture.Factory.Create(_fixture.TenantA))
        {
            var typed = (TenantPersistenceSession)updateSession;
            var forged = AttachForgedRecord(typed, foreign.Id, _fixture.TenantA.TenantId);
            typed.DbContext.Entry(forged).State = EntityState.Modified;
            var exception = await Assert.ThrowsAsync<TenantPersistenceViolationException>(
                () => updateSession.SaveChangesAsync());
            Assert.Equal(TenantPersistenceViolationCode.StoredOwnerUnavailable, exception.Code);
            Assert.Equal(TenantPersistenceOperation.Modify, exception.Operation);
        }

        await using (var deleteSession = _fixture.Factory.Create(_fixture.TenantA))
        {
            var typed = (TenantPersistenceSession)deleteSession;
            var forged = AttachForgedRecord(typed, foreign.Id, _fixture.TenantA.TenantId);
            typed.DbContext.Entry(forged).State = EntityState.Deleted;
            var exception = await Assert.ThrowsAsync<TenantPersistenceViolationException>(
                () => deleteSession.SaveChangesAsync());
            Assert.Equal(TenantPersistenceViolationCode.StoredOwnerUnavailable, exception.Code);
            Assert.Equal(TenantPersistenceOperation.Delete, exception.Operation);
        }

        await using var tenantB = _fixture.Factory.Create(_fixture.TenantB);
        var persisted = await tenantB.Records.FindAsync(foreign.Id);
        Assert.NotNull(persisted);
        Assert.Equal(foreign.BusinessKey, persisted!.BusinessKey);
    }

    [Fact]
    public async Task Tenant_aware_unique_index_allows_cross_tenant_duplicate_and_rejects_same_tenant_duplicate()
    {
        var key = $"sql-unique-{Guid.NewGuid():N}";
        await _fixture.AddAsync(_fixture.TenantA, new TenantOwnedRecord(_fixture.TenantA.TenantId, key));
        await _fixture.AddAsync(_fixture.TenantB, new TenantOwnedRecord(_fixture.TenantB.TenantId, key));

        await using var duplicateSession = _fixture.Factory.Create(_fixture.TenantA);
        duplicateSession.Records.Add(new TenantOwnedRecord(_fixture.TenantA.TenantId, key));
        var exception = await Assert.ThrowsAsync<TenantPersistenceViolationException>(
            () => duplicateSession.SaveChangesAsync());
        Assert.Equal(TenantPersistenceViolationCode.PersistenceConflict, exception.Code);
        Assert.Null(exception.InnerException);
        Assert.DoesNotContain("UNIQUE", exception.ToString(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Sql_server_rowversion_rejects_stale_update_and_delete()
    {
        var record = new TenantOwnedRecord(_fixture.TenantA.TenantId, $"sql-version-{Guid.NewGuid():N}");
        await _fixture.AddAsync(_fixture.TenantA, record);

        await using var first = _fixture.Factory.Create(_fixture.TenantA);
        await using var second = _fixture.Factory.Create(_fixture.TenantA);
        var firstRecord = await first.Records.FindAsync(record.Id);
        var secondRecord = await second.Records.FindAsync(record.Id);
        Assert.NotNull(firstRecord);
        Assert.NotNull(secondRecord);
        Assert.NotEmpty(firstRecord!.Version);
        Assert.Equal(firstRecord.Version, secondRecord!.Version);

        var firstSession = (TenantPersistenceSession)first;
        firstSession.DbContext.Entry(firstRecord).Property(nameof(TenantOwnedRecord.BusinessKey)).CurrentValue =
            $"sql-version-updated-{Guid.NewGuid():N}";
        Assert.Equal(1, await first.SaveChangesAsync());

        var secondSession = (TenantPersistenceSession)second;
        secondSession.DbContext.Entry(secondRecord).Property(nameof(TenantOwnedRecord.BusinessKey)).CurrentValue =
            $"sql-version-stale-{Guid.NewGuid():N}";
        var staleUpdate = await Assert.ThrowsAsync<TenantPersistenceViolationException>(
            () => second.SaveChangesAsync());
        Assert.Equal(TenantPersistenceViolationCode.PersistenceConflict, staleUpdate.Code);

        await using var staleDelete = _fixture.Factory.Create(_fixture.TenantA);
        var staleDeleteRecord = await staleDelete.Records.FindAsync(record.Id);
        Assert.NotNull(staleDeleteRecord);
        await using var current = _fixture.Factory.Create(_fixture.TenantA);
        var currentRecord = await current.Records.FindAsync(record.Id);
        Assert.NotNull(currentRecord);
        ((TenantPersistenceSession)current).DbContext.Entry(currentRecord!).Property(nameof(TenantOwnedRecord.BusinessKey)).CurrentValue =
            $"sql-version-current-{Guid.NewGuid():N}";
        await current.SaveChangesAsync();
        ((TenantPersistenceSession)staleDelete).DbContext.Entry(staleDeleteRecord!).State = EntityState.Deleted;
        var staleDeleteException = await Assert.ThrowsAsync<TenantPersistenceViolationException>(
            () => staleDelete.SaveChangesAsync());
        Assert.Equal(TenantPersistenceViolationCode.PersistenceConflict, staleDeleteException.Code);
    }

    [Fact]
    public async Task Inventory_sql_server_existing_anchor_touch_persists_a_real_update()
    {
        var connectionString = await GetConnectionStringAsync();
        var options = new DbContextOptionsBuilder().UseSqlServer(connectionString).Options;
        var anchorId = Guid.NewGuid();
        await using (var createDb = new InventoryDbContext(options, _fixture.TenantA))
        {
            await createDb.Database.EnsureCreatedAsync();
            var anchor = CreateAnchor(_fixture.TenantA.TenantId, anchorId, Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
            anchor.Touch();
            createDb.ConcurrencyAnchors.Add(anchor);
            await createDb.SaveChangesAsync();
        }

        long beforeSequence;
        byte[] beforeVersion;
        await using (var touchDb = new InventoryDbContext(options, _fixture.TenantA))
        {
            var anchor = await touchDb.ConcurrencyAnchors.SingleAsync(item => item.Id == anchorId);
            beforeSequence = anchor.TouchSequence;
            beforeVersion = anchor.Version.ToArray();
            anchor.Touch();
            await touchDb.SaveChangesAsync();
        }

        await using var verifyDb = new InventoryDbContext(options, _fixture.TenantA);
        var persisted = await verifyDb.ConcurrencyAnchors.AsNoTracking().SingleAsync(item => item.Id == anchorId);
        Assert.Equal(beforeSequence + 1, persisted.TouchSequence);
        Assert.NotEqual(beforeVersion, persisted.Version);
    }

    [Fact]
    public async Task Inventory_sql_server_branch_null_anchor_identity_is_database_unique_and_unfiltered()
    {
        var connectionString = await GetConnectionStringAsync();
        var options = new DbContextOptionsBuilder().UseSqlServer(connectionString).Options;
        var companyId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var unitId = Guid.NewGuid();
        await using (var createDb = new InventoryDbContext(options, _fixture.TenantA))
        {
            await createDb.Database.EnsureCreatedAsync();
            createDb.ConcurrencyAnchors.Add(CreateAnchor(_fixture.TenantA.TenantId, Guid.NewGuid(), companyId, warehouseId, productId, unitId));
            await createDb.SaveChangesAsync();
        }

        await using (var duplicateDb = new InventoryDbContext(options, _fixture.TenantA))
        {
            duplicateDb.ConcurrencyAnchors.Add(CreateAnchor(_fixture.TenantA.TenantId, Guid.NewGuid(), companyId, warehouseId, productId, unitId));
            await Assert.ThrowsAsync<DbUpdateException>(() => duplicateDb.SaveChangesAsync());
        }

        await using var connection = await _fixture.OpenConnectionAsync();
        await using var indexCommand = connection.CreateCommand();
        indexCommand.CommandText = """
            SELECT filter_definition
            FROM sys.indexes
            WHERE object_id = OBJECT_ID(N'[inventory].[ConcurrencyAnchors]')
              AND name = N'IX_ConcurrencyAnchors_TenantId_CompanyId_BranchId_WarehouseId_ProductId_UnitOfMeasureId_TrackingKey';
            """;
        var filterDefinition = await indexCommand.ExecuteScalarAsync();
        Assert.True(
            filterDefinition is null
            || filterDefinition is DBNull
            || string.IsNullOrEmpty(filterDefinition.ToString()),
            $"The shared-catalog anchor uniqueness index must be unfiltered; actual filter: {filterDefinition ?? "<null>"}.");
    }

    [Fact]
    public async Task Inventory_sql_server_transfer_receipt_reference_case_variants_converge_without_second_movement()
    {
        var connectionString = await GetConnectionStringAsync();
        var options = new DbContextOptionsBuilder().UseSqlServer(connectionString, sql => sql.CommandTimeout(15)).Options;
        var tenantId = _fixture.TenantA.TenantId;
        var companyId = Guid.NewGuid();
        var sourceWarehouseId = Guid.NewGuid();
        var destinationWarehouseId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var unitId = Guid.NewGuid();
        var source = new InventoryWarehouseOption(tenantId.Value, companyId, null, sourceWarehouseId, "SQL-SOURCE", "SQL source");
        var destination = new InventoryWarehouseOption(tenantId.Value, companyId, null, destinationWarehouseId, "SQL-DEST", "SQL destination");
        var product = new InventoryProductReference(tenantId.Value, productId, "SQL-RECEIPT", "SQL receipt product", unitId, "EA", true, true, false);
        var context = InventoryContext(_fixture.TenantA, "tenant.inventory.transfer.receive");

        await using (var seedDb = new InventoryDbContext(options, _fixture.TenantA))
        {
            seedDb.StockMovements.Add(new InventoryStockMovementEntity(
                tenantId,
                Guid.NewGuid(),
                companyId,
                null,
                sourceWarehouseId,
                source.Code,
                source.Name,
                productId,
                product.Sku,
                product.Name,
                unitId,
                product.BaseUnitOfMeasureCode,
                InventoryMovementDirection.Inbound,
                10m,
                1m,
                "SAR",
                null,
                InventoryMovementSourceType.OpeningBalance,
                Guid.NewGuid(),
                Guid.NewGuid(),
                null,
                new DateOnly(2026, 8, 22),
                Guid.NewGuid(),
                "sql-transfer-receipt-seed",
                DateTimeOffset.UtcNow));
            await seedDb.SaveChangesAsync();
        }

        var persistence = new InventoryPersistence(options);
        var created = Assert.IsType<InventoryTransferRecord>(await persistence.CreateTransferAsync(
            context,
            new InventoryTransferCreateCommand(
                Guid.NewGuid(),
                new InventoryScope(tenantId.Value, companyId, null, sourceWarehouseId),
                source,
                destination,
                productId,
                unitId,
                product,
                10m,
                InventoryTransferMode.InTransit,
                null,
                "SQL case-variant receipt",
                context.ActorId,
                DateTimeOffset.UtcNow,
                "sql-transfer-create",
                "sql-transfer-create-key",
                "sql-transfer-create-fingerprint")));
        var shipped = Assert.IsType<InventoryTransferRecord>(await persistence.ShipTransferAsync(
            context,
            new InventoryTransferActionCommand(created.Id, created.Version, null, null, "ship", context.ActorId, DateTimeOffset.UtcNow, "sql-transfer-ship", "sql-transfer-ship-key", "sql-transfer-ship-fingerprint")));
        var firstReceipt = Assert.IsType<InventoryTransferRecord>(await persistence.ReceiveTransferAsync(
            context,
            new InventoryTransferActionCommand(shipped.Id, shipped.Version, 4m, "RECEIVE-001", "receive", context.ActorId, DateTimeOffset.UtcNow, "sql-transfer-receive-a", "sql-transfer-receive-a-key", "sql-transfer-receive-a-fingerprint")));
        var duplicateReceipt = Assert.IsType<InventoryTransferRecord>(await persistence.ReceiveTransferAsync(
            context,
            new InventoryTransferActionCommand(firstReceipt.Id, firstReceipt.Version, 4m, "receive-001", "duplicate receive", context.ActorId, DateTimeOffset.UtcNow, "sql-transfer-receive-b", "sql-transfer-receive-b-key", "sql-transfer-receive-b-fingerprint")));

        Assert.Equal(4m, duplicateReceipt.ReceivedQuantity);
        Assert.Equal(6m, duplicateReceipt.InTransitQuantity);
        var movements = await persistence.ListMovementsAsync(context, null);
        Assert.Single(movements, item => item.SourceType == InventoryMovementSourceType.WarehouseTransferReceipt);
        var history = await persistence.ReadTransferHistoryAsync(context, created.Id);
        Assert.Equal("RECEIVE-001", Assert.Single(history, item => item.EventType == InventoryTransferEventType.Received).Reference);
        var audit = await persistence.ReadAuditAsync(context, "transfer", created.Id);
        Assert.Contains(audit, item => item.Decision == "Duplicate" && item.AfterSummary == "duplicate-receipt-reference");
    }

    [Fact]
    public async Task Inventory_sql_server_contention_is_classified_as_conflict_without_reservation_effect()
    {
        var connectionString = await GetConnectionStringAsync();
        var seedOptions = new DbContextOptionsBuilder().UseSqlServer(connectionString).Options;
        var tenantId = _fixture.TenantA.TenantId;
        var warehouseId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var unitId = Guid.NewGuid();
        var anchorId = Guid.NewGuid();
        var warehouse = new InventoryWarehouseOption(tenantId.Value, Guid.NewGuid(), null, warehouseId, "WH-CONTENTION", "Contention warehouse");
        var product = new InventoryProductReference(tenantId.Value, productId, "SKU-CONTENTION", "Contention product", unitId, "EA", true, true, false);

        await using (var seedDb = new InventoryDbContext(seedOptions, _fixture.TenantA))
        {
            await seedDb.Database.EnsureCreatedAsync();
            var anchor = CreateAnchor(tenantId, anchorId, warehouse.CompanyId, warehouseId, productId, unitId);
            anchor.Touch();
            seedDb.ConcurrencyAnchors.Add(anchor);
            seedDb.StockMovements.Add(new InventoryStockMovementEntity(
                tenantId,
                Guid.NewGuid(),
                warehouse.CompanyId,
                warehouse.BranchId,
                warehouse.WarehouseId,
                warehouse.Code,
                warehouse.Name,
                product.ProductId,
                product.Sku,
                product.Name,
                product.BaseUnitOfMeasureId,
                product.BaseUnitOfMeasureCode,
                InventoryMovementDirection.Inbound,
                10m,
                1m,
                "SAR",
                null,
                InventoryMovementSourceType.OpeningBalance,
                Guid.NewGuid(),
                Guid.NewGuid(),
                null,
                new DateOnly(2026, 8, 22),
                Guid.NewGuid(),
                "sql-contention-seed",
                DateTimeOffset.UtcNow));
            await seedDb.SaveChangesAsync();
        }

        var gate = new FirstInventoryConnectionClosedGate();
        var operationOptions = new DbContextOptionsBuilder()
            .UseSqlServer(connectionString, sql => sql.CommandTimeout(5))
            .AddInterceptors(gate)
            .Options;
        var inventoryContext = InventoryContext(_fixture.TenantA, "tenant.inventory.reservation.create");
        var service = new InventoryService(
            new InventoryPersistence(operationOptions),
            new InventoryResourceAuthorizationService(),
            new ConfiguredInventoryWarehouseProvider([warehouse]),
            new StaticInventoryProductProvider(product));
        var request = new InventoryReservationCreateRequest(
            warehouse.CompanyId,
            warehouse.BranchId,
            warehouse.WarehouseId,
            product.ProductId,
            product.BaseUnitOfMeasureId,
            7m,
            "Demand",
            "SQL-CONTENTION-DEMAND",
            true,
            null);

        var operation = service.CreateReservationAsync(inventoryContext, request, "sql-contention-key");
        await gate.WaitAsync();

        await using var blocker = await _fixture.OpenInventoryConnectionAsync();
        await using var transaction = (SqlTransaction)await blocker.BeginTransactionAsync(IsolationLevel.Serializable);
        await using (var lockCommand = blocker.CreateCommand())
        {
            lockCommand.Transaction = transaction;
            lockCommand.CommandText = """
                SELECT [TouchSequence]
                FROM [inventory].[ConcurrencyAnchors] WITH (UPDLOCK, HOLDLOCK)
                WHERE [Id] = @anchorId;
                """;
            lockCommand.Parameters.AddWithValue("@anchorId", anchorId);
            Assert.NotNull(await lockCommand.ExecuteScalarAsync());
        }

        var result = await operation;
        Assert.False(result.Succeeded);
        Assert.Equal("conflict", result.Code);

        await transaction.RollbackAsync();
        var persistence = new InventoryPersistence(seedOptions);
        var reservations = await persistence.ListReservationsAsync(inventoryContext, new InventoryScope(tenantId.Value, warehouse.CompanyId, warehouse.BranchId, warehouse.WarehouseId));
        Assert.Empty(reservations);
    }

    [Fact]
    public async Task Inventory_sql_server_overlapping_reservations_cannot_over_allocate()
    {
        var connectionString = await GetConnectionStringAsync();
        var options = new DbContextOptionsBuilder().UseSqlServer(connectionString, sql => sql.CommandTimeout(15)).Options;
        var tenantId = _fixture.TenantA.TenantId;
        var companyId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var unitId = Guid.NewGuid();
        var warehouse = new InventoryWarehouseOption(tenantId.Value, companyId, null, warehouseId, "WH-RESERVATION", "Reservation warehouse");
        var product = new InventoryProductReference(tenantId.Value, productId, "SKU-RESERVATION", "Reservation product", unitId, "EA", true, true, false);
        await using (var seedDb = new InventoryDbContext(options, _fixture.TenantA))
        {
            await seedDb.Database.EnsureCreatedAsync();
            var anchor = CreateAnchor(tenantId, Guid.NewGuid(), companyId, warehouseId, productId, unitId);
            anchor.Touch();
            seedDb.ConcurrencyAnchors.Add(anchor);
            seedDb.StockMovements.Add(new InventoryStockMovementEntity(
                tenantId,
                Guid.NewGuid(),
                companyId,
                null,
                warehouseId,
                warehouse.Code,
                warehouse.Name,
                productId,
                product.Sku,
                product.Name,
                unitId,
                product.BaseUnitOfMeasureCode,
                InventoryMovementDirection.Inbound,
                10m,
                1m,
                "SAR",
                null,
                InventoryMovementSourceType.OpeningBalance,
                Guid.NewGuid(),
                Guid.NewGuid(),
                null,
                new DateOnly(2026, 8, 22),
                Guid.NewGuid(),
                "sql-reservation-seed",
                DateTimeOffset.UtcNow));
            await seedDb.SaveChangesAsync();
        }

        var context = InventoryContext(_fixture.TenantA);
        using var barrier = new Barrier(2);
        var firstPersistence = new InventoryPersistence(options);
        var secondPersistence = new InventoryPersistence(options);
        var first = Task.Run(async () =>
        {
            barrier.SignalAndWait();
            return await firstPersistence.CreateReservationAsync(context, ReservationCommand(tenantId.Value, companyId, warehouseId, productId, unitId, warehouse, product, "SQL-RESERVATION-1"), 10m);
        });
        var second = Task.Run(async () =>
        {
            barrier.SignalAndWait();
            return await secondPersistence.CreateReservationAsync(context, ReservationCommand(tenantId.Value, companyId, warehouseId, productId, unitId, warehouse, product, "SQL-RESERVATION-2"), 10m);
        });

        var results = await Task.WhenAll(first, second);
        var reservations = await firstPersistence.ListReservationsAsync(context, new InventoryScope(tenantId.Value, companyId, null, warehouseId));
        Assert.NotEmpty(reservations);
        Assert.True(reservations.Sum(item => item.ReservedQuantity) <= 10m);
        Assert.Contains(
            reservations.Sum(item => item.ReservedQuantity) + reservations.Sum(item => item.UnallocatedQuantity),
            new[] { 7m, 14m });
        Assert.Contains(results, item => item is not null);
    }

    [Fact]
    public async Task Same_tenant_relationship_is_allowed_and_cross_tenant_relationship_is_denied()
    {
        var allowed = new TenantOwnedRecord(
            _fixture.TenantA.TenantId,
            $"sql-related-{Guid.NewGuid():N}",
            TenantRelationshipKind.CompanyBranch,
            _fixture.TenantA.TenantId);
        await _fixture.AddAsync(_fixture.TenantA, allowed);

        await using var session = _fixture.Factory.Create(_fixture.TenantA);
        session.Records.Add(new TenantOwnedRecord(
            _fixture.TenantA.TenantId,
            $"sql-cross-related-{Guid.NewGuid():N}",
            TenantRelationshipKind.BranchWarehouse,
            _fixture.TenantB.TenantId));
        var exception = await Assert.ThrowsAsync<TenantPersistenceViolationException>(
            () => session.SaveChangesAsync());
        Assert.Equal(TenantPersistenceViolationCode.RelationshipMismatch, exception.Code);
    }

    [Theory]
    [InlineData(TenantRelationshipKind.CompanyBranch)]
    [InlineData(TenantRelationshipKind.BranchWarehouse)]
    [InlineData(TenantRelationshipKind.CompanyDepartment)]
    public async Task Same_tenant_composite_relationship_is_allowed_on_sql_server(TenantRelationshipKind relationshipKind)
    {
        var allowed = new TenantOwnedRecord(
            _fixture.TenantA.TenantId,
            $"sql-related-{relationshipKind}-{Guid.NewGuid():N}",
            relationshipKind,
            _fixture.TenantA.TenantId);
        await _fixture.AddAsync(_fixture.TenantA, allowed);

        await using var session = _fixture.Factory.Create(_fixture.TenantA);
        Assert.NotNull(await session.Records.FindAsync(allowed.Id));
    }

    [Fact]
    public async Task Sql_server_schema_has_tenant_business_unique_index_and_rowversion()
    {
        await using var connection = await _fixture.OpenConnectionAsync();
        await using var indexCommand = connection.CreateCommand();
        indexCommand.CommandText = """
            SELECT COUNT(*)
            FROM
            (
                SELECT i.index_id
                FROM sys.indexes AS i
                INNER JOIN sys.index_columns AS ic
                    ON ic.object_id = i.object_id AND ic.index_id = i.index_id
                INNER JOIN sys.columns AS c
                    ON c.object_id = ic.object_id AND c.column_id = ic.column_id
                WHERE i.object_id = OBJECT_ID(N'[tenancy].[TenantOwnedRecords]')
                  AND i.is_unique = 1
                  AND ic.is_included_column = 0
                GROUP BY i.index_id
                HAVING COUNT(*) = 2
                   AND SUM(CASE WHEN c.name = N'TenantId' THEN 1 ELSE 0 END) = 1
                   AND SUM(CASE WHEN c.name = N'BusinessKey' THEN 1 ELSE 0 END) = 1
            ) AS matching_indexes;
            """;
        Assert.Equal(1, Convert.ToInt32(await indexCommand.ExecuteScalarAsync()));

        await using var rowVersionCommand = connection.CreateCommand();
        rowVersionCommand.CommandText = """
            SELECT TYPE_NAME(c.user_type_id)
            FROM sys.columns AS c
            WHERE c.object_id = OBJECT_ID(N'[tenancy].[TenantOwnedRecords]')
              AND c.name = N'Version';
            """;
        Assert.Equal("timestamp", (string?)await rowVersionCommand.ExecuteScalarAsync());
    }

    [Fact]
    public async Task Sql_server_collation_is_recorded_and_unicode_identifier_round_trips()
    {
        await using var connection = await _fixture.OpenConnectionAsync();
        await using var collationCommand = connection.CreateCommand();
        collationCommand.CommandText = "SELECT CAST(DATABASEPROPERTYEX(DB_NAME(), 'Collation') AS nvarchar(128));";
        var collation = (string?)await collationCommand.ExecuteScalarAsync();
        Assert.False(string.IsNullOrWhiteSpace(collation));

        var arabicKey = $"شركة-{Guid.NewGuid():N}";
        await _fixture.AddAsync(_fixture.TenantA, new TenantOwnedRecord(_fixture.TenantA.TenantId, arabicKey));
        await using var session = _fixture.Factory.Create(_fixture.TenantA);
        var records = await session.Records.ListAsync();
        Assert.Contains(records, item => item.BusinessKey == arabicKey);
    }

    [Fact]
    public async Task Durable_work_probe_transaction_rolls_back_work_and_outbox_together()
    {
        var tenantId = _fixture.TenantA.TenantId.Value;
        var workId = Guid.NewGuid();
        var workEventId = Guid.NewGuid();
        var outboxId = Guid.NewGuid();

        await using var connection = await _fixture.OpenConnectionAsync();
        await using var transaction = (SqlTransaction)await connection.BeginTransactionAsync(IsolationLevel.Serializable);
        await ExecuteProbeInsertAsync(connection, transaction, workId, tenantId, workEventId, "work");
        await ExecuteProbeInsertAsync(connection, transaction, outboxId, tenantId, outboxId, "outbox");
        await transaction.RollbackAsync();

        await using var countCommand = connection.CreateCommand();
        countCommand.CommandText = "SELECT COUNT(*) FROM [test].[DurableWorkProbe] WHERE [WorkId] IN (@workId, @outboxId);";
        countCommand.Parameters.AddWithValue("@workId", workId);
        countCommand.Parameters.AddWithValue("@outboxId", outboxId);
        Assert.Equal(0, Convert.ToInt32(await countCommand.ExecuteScalarAsync()));
    }

    [Fact]
    public async Task Durable_work_probe_idempotency_is_tenant_scoped()
    {
        var eventId = Guid.NewGuid();
        var tenantA = _fixture.TenantA.TenantId.Value;
        var tenantB = _fixture.TenantB.TenantId.Value;
        await using var connection = await _fixture.OpenConnectionAsync();

        await ExecuteProbeInsertAsync(connection, transaction: null, Guid.NewGuid(), tenantA, eventId, "outbox");
        await Assert.ThrowsAsync<SqlException>(() =>
            ExecuteProbeInsertAsync(connection, transaction: null, Guid.NewGuid(), tenantA, eventId, "outbox"));
        await ExecuteProbeInsertAsync(connection, transaction: null, Guid.NewGuid(), tenantB, eventId, "outbox");
    }

    [Fact]
    public async Task Durable_work_probe_claim_is_single_owner_and_optimistic()
    {
        var tenantId = _fixture.TenantA.TenantId.Value;
        var workId = Guid.NewGuid();
        await using var connection = await _fixture.OpenConnectionAsync();
        await ExecuteProbeInsertAsync(connection, transaction: null, workId, tenantId, Guid.NewGuid(), "work");

        await using var first = connection.CreateCommand();
        first.CommandText = """
            UPDATE [test].[DurableWorkProbe]
            SET [LeaseOwner] = @owner, [LeaseVersion] = [LeaseVersion] + 1, [State] = N'Leased'
            WHERE [WorkId] = @workId AND [State] = N'Pending' AND [LeaseOwner] IS NULL;
            """;
        first.Parameters.AddWithValue("@owner", "worker-a");
        first.Parameters.AddWithValue("@workId", workId);
        Assert.Equal(1, await first.ExecuteNonQueryAsync());

        await using var second = connection.CreateCommand();
        second.CommandText = first.CommandText;
        second.Parameters.AddWithValue("@owner", "worker-b");
        second.Parameters.AddWithValue("@workId", workId);
        Assert.Equal(0, await second.ExecuteNonQueryAsync());
    }

    [Theory]
    [InlineData(null, "a null connection string")]
    [InlineData("", "an empty connection string")]
    [InlineData(
        @"Server=tcp:prod-sql.example.com,1433;Database=MiniErpFoundation_x;Integrated Security=True;",
        "a non-LocalDB Server/DataSource")]
    [InlineData(
        @"Server=(localdb)\MSSQLLocalDB;Database=ProductionCustomers;Integrated Security=True;",
        "an InitialCatalog outside the disposable MiniErpFoundation_ prefix")]
    [InlineData(
        @"Server=(localdb)\MSSQLLocalDB;Database=MiniErpFoundation;Integrated Security=True;",
        "an InitialCatalog equal to the prefix with no disposable suffix")]
    [InlineData(
        @"Server=(localdb)\MSSQLLocalDB;Database=MiniErpFoundation_evil.database;Integrated Security=True;",
        "an InitialCatalog containing characters outside the disposable pattern")]
    public void Unsafe_sql_server_configuration_is_rejected_by_the_real_validator(
        string? unsafeConnectionString,
        string reason)
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => SqlServerSafetyConfigurationValidator.ValidateSafeConnectionString(unsafeConnectionString));
        Assert.False(string.IsNullOrWhiteSpace(exception.Message), $"Expected a safe-closed rejection for {reason}.");
    }

    [Fact]
    public void Safe_sql_server_configuration_passes_the_real_validator()
    {
        var builder = SqlServerSafetyConfigurationValidator.ValidateSafeConnectionString(
            @"Server=(localdb)\MSSQLLocalDB;Database=MiniErpFoundation_selftest01;Integrated Security=True;");

        Assert.Equal(@"(localdb)\MSSQLLocalDB", builder.DataSource);
        Assert.StartsWith("MiniErpFoundation_", builder.InitialCatalog, StringComparison.Ordinal);
    }

    [Fact]
    public void Fixture_accepted_connection_string_is_the_one_the_real_validator_approved()
    {
        // Confirms the dedicated safety variable (not the runtime variable) is what the fixture uses,
        // and that the injected value actually passes the validator's LocalDB + MiniErpFoundation_* checks.
        var rawConnectionString = Environment.GetEnvironmentVariable("MESP_SQLSERVER_SAFETY_CONNECTION_STRING");
        var builder = SqlServerSafetyConfigurationValidator.ValidateSafeConnectionString(rawConnectionString);

        Assert.Equal(@"(localdb)\MSSQLLocalDB", builder.DataSource);
        Assert.StartsWith("MiniErpFoundation_", builder.InitialCatalog, StringComparison.Ordinal);
    }

    [Fact]
    public void Runtime_connection_string_is_not_accepted_as_safety_configuration()
    {
        // Proves that the persistent MESP runtime variable cannot authorize destructive
        // safety-test execution even if its value happens to be set in the test process.
        // The safety harness must never fall back to the runtime connection.
        //
        // This test verifies the architectural boundary: MESP_SQLSERVER_CONNECTION_STRING
        // is for the persistent MESP application runtime; MESP_SQLSERVER_SAFETY_CONNECTION_STRING
        // is exclusively for the disposable LocalDB safety harness.
        var runtimeConnectionString = Environment.GetEnvironmentVariable("MESP_SQLSERVER_CONNECTION_STRING");

        if (string.IsNullOrWhiteSpace(runtimeConnectionString))
        {
            // No runtime variable set — the validator would already reject null/empty;
            // this is already covered by the Unsafe_sql_server_configuration_is_rejected theory.
            return;
        }

        // If a runtime variable IS set, it must point at server '.' / database MESP.
        // That connection must always be rejected by the safety validator because:
        // (a) it does not target (localdb)\MSSQLLocalDB, or
        // (b) its database name does not start with MiniErpFoundation_.
        // Either condition alone is sufficient for rejection.
        Assert.Throws<InvalidOperationException>(
            () => SqlServerSafetyConfigurationValidator.ValidateSafeConnectionString(runtimeConnectionString));
    }

    private async Task<string> GetConnectionStringAsync()
    {
        await using var connection = await _fixture.OpenInventoryConnectionAsync();
        return connection.ConnectionString;
    }

    private static InventoryConcurrencyAnchorEntity CreateAnchor(
        TenantId tenantId,
        Guid id,
        Guid companyId = default,
        Guid warehouseId = default,
        Guid productId = default,
        Guid unitOfMeasureId = default)
    {
        return new InventoryConcurrencyAnchorEntity(
            tenantId,
            id,
            companyId == Guid.Empty ? Guid.Parse("11111111-1111-1111-1111-111111111111") : companyId,
            null,
            warehouseId == Guid.Empty ? Guid.Parse("cccccccc-1111-1111-1111-111111111111") : warehouseId,
            productId == Guid.Empty ? Guid.Parse("77777777-7777-7777-7777-777777777777") : productId,
            unitOfMeasureId == Guid.Empty ? Guid.Parse("88888888-8888-8888-8888-888888888888") : unitOfMeasureId,
            string.Empty);
    }

    private static InventoryRequestContext InventoryContext(
        TenantContext tenantContext,
        string permission = "tenant.inventory.ledger.view") =>
        new InventoryTenantContextResolver().Resolve(
            FoundationRequestContext.ForTenant(
                Guid.Parse("44444444-4444-4444-4444-444444444444"),
                Guid.Parse("55555555-5555-5555-5555-555555555555"),
                tenantContext,
                permission)).Context!;

    private static InventoryReservationCommand ReservationCommand(
        Guid tenantId,
        Guid companyId,
        Guid warehouseId,
        Guid productId,
        Guid unitId,
        InventoryWarehouseOption warehouse,
        InventoryProductReference product,
        string sourceReference) => new(
        Guid.NewGuid(),
        new InventoryScope(tenantId, companyId, warehouse.BranchId, warehouseId),
        productId,
        unitId,
        7m,
        "Demand",
        sourceReference,
        true,
        null,
        product,
        warehouse.Code,
        warehouse.Name,
        Guid.Parse("44444444-4444-4444-4444-444444444444"),
        DateTimeOffset.UtcNow,
        $"sql-reservation-{Guid.NewGuid():N}",
        $"sql-reservation-key-{Guid.NewGuid():N}",
        $"sql-reservation-fingerprint-{Guid.NewGuid():N}");

    private sealed class StaticInventoryProductProvider(InventoryProductReference product) : IInventoryProductProvider
    {
        public Task<InventoryProductReference?> FindAsync(InventoryRequestContext context, Guid productId, CancellationToken cancellationToken = default) =>
            Task.FromResult<InventoryProductReference?>(product.TenantId == context.TenantId.Value && product.ProductId == productId ? product : null);
    }

    private sealed class FirstInventoryConnectionClosedGate : DbConnectionInterceptor
    {
        private readonly TaskCompletionSource<bool> completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int signaled;

        internal Task WaitAsync() => completion.Task.WaitAsync(TimeSpan.FromSeconds(15));

        public override async Task ConnectionOpenedAsync(
            DbConnection connection,
            ConnectionEndEventData eventData,
            CancellationToken cancellationToken = default)
        {
            await using var command = connection.CreateCommand();
            command.CommandText = "SET LOCK_TIMEOUT 1000;";
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        public override Task ConnectionClosedAsync(DbConnection connection, ConnectionEndEventData eventData)
        {
            if (Interlocked.Exchange(ref signaled, 1) == 0)
            {
                completion.TrySetResult(true);
            }

            return Task.CompletedTask;
        }
    }

    private sealed class FullCountLedgerQueryGate : DbCommandInterceptor
    {
        private readonly TaskCompletionSource<bool> entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int signaled;

        internal Task WaitAsync() => entered.Task.WaitAsync(TimeSpan.FromSeconds(15));

        internal void Release() => release.TrySetResult(true);

        public override async ValueTask<DbDataReader> ReaderExecutedAsync(
            DbCommand command,
            CommandExecutedEventData eventData,
            DbDataReader result,
            CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("[StockLedgerMovements]", StringComparison.OrdinalIgnoreCase)
                && command.CommandText.Contains("[ProductSku]", StringComparison.OrdinalIgnoreCase))
            {
                if (Interlocked.Exchange(ref signaled, 1) == 0)
                {
                    entered.TrySetResult(true);
                    await release.Task.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
                }
            }

            return result;
        }
    }

    private sealed class SelectedIdentityLedgerCountGate : DbCommandInterceptor
    {
        private readonly TaskCompletionSource<bool> entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> release = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int signaled;

        internal Task WaitAsync() => entered.Task.WaitAsync(TimeSpan.FromSeconds(15));

        internal void Release() => release.TrySetResult(true);

        public override async ValueTask<DbDataReader> ReaderExecutedAsync(
            DbCommand command,
            CommandExecutedEventData eventData,
            DbDataReader result,
            CancellationToken cancellationToken = default)
        {
            if (command.CommandText.Contains("[StockLedgerMovements]", StringComparison.OrdinalIgnoreCase)
                && command.CommandText.Contains("COUNT_BIG", StringComparison.OrdinalIgnoreCase)
                && Interlocked.Exchange(ref signaled, 1) == 0)
            {
                entered.TrySetResult(true);
                await release.Task.WaitAsync(TimeSpan.FromSeconds(15), cancellationToken);
            }

            return result;
        }
    }

    private sealed class StockMovementInsertAttemptGate : DbCommandInterceptor
    {
        private readonly TaskCompletionSource<bool> entered = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int signaled;

        internal Task WaitAsync() => entered.Task.WaitAsync(TimeSpan.FromSeconds(15));

        public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<int> result,
            CancellationToken cancellationToken = default)
        {
            SignalIfMovementInsert(command);

            return ValueTask.FromResult(result);
        }

        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(
            DbCommand command,
            CommandEventData eventData,
            InterceptionResult<DbDataReader> result,
            CancellationToken cancellationToken = default)
        {
            SignalIfMovementInsert(command);
            return ValueTask.FromResult(result);
        }

        private void SignalIfMovementInsert(DbCommand command)
        {
            if (command.CommandText.Contains("INSERT INTO [inventory].[StockLedgerMovements]", StringComparison.OrdinalIgnoreCase)
                && Interlocked.Exchange(ref signaled, 1) == 0)
            {
                entered.TrySetResult(true);
            }
        }
    }

    [Fact]
    public async Task MESP130_sql_server_full_count_boundary_blocks_insert_after_authoritative_read_and_uses_ledger_fence()
    {
        var connectionString = await GetConnectionStringAsync();
        var seedOptions = new DbContextOptionsBuilder().UseSqlServer(connectionString).Options;
        var tenantId = _fixture.TenantA.TenantId;
        var companyId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var productAId = Guid.NewGuid();
        var unitAId = Guid.NewGuid();
        var productBId = Guid.NewGuid();
        var unitBId = Guid.NewGuid();
        var warehouse = new InventoryWarehouseOption(tenantId.Value, companyId, null, warehouseId, "WH-FULL-RACE", "Full Count race warehouse");
        var productA = new InventoryProductReference(tenantId.Value, productAId, "SKU-FULL-A", "Full Count product A", unitAId, "EA", true, true, false);
        var productB = new InventoryProductReference(tenantId.Value, productBId, "SKU-FULL-B", "Full Count product B", unitBId, "EA", true, true, false);
        var context = InventoryContext(_fixture.TenantA);
        var scope = new InventoryScope(tenantId.Value, companyId, null, warehouseId);

        await using (var seedDb = new InventoryDbContext(seedOptions, _fixture.TenantA))
        {
            seedDb.StockMovements.Add(new InventoryStockMovementEntity(
                tenantId, Guid.NewGuid(), companyId, null, warehouseId, warehouse.Code, warehouse.Name,
                productAId, productA.Sku, productA.Name, unitAId, unitAId == Guid.Empty ? "EA" : productA.BaseUnitOfMeasureCode,
                InventoryMovementDirection.Inbound, 5m, null, null, InventoryValuationStatus.Pending, null,
                InventoryMovementSourceType.OpeningBalance, Guid.NewGuid(), Guid.NewGuid(), null,
                DateOnly.FromDateTime(DateTime.UtcNow), context.ActorId, "sql-full-race-seed-a", DateTimeOffset.UtcNow));
            await seedDb.SaveChangesAsync();
        }

        var gate = new FullCountLedgerQueryGate();
        var operationOptions = new DbContextOptionsBuilder()
            .UseSqlServer(connectionString, sql => sql.CommandTimeout(15))
            .AddInterceptors(gate)
            .Options;
        var persistence = new InventoryPersistence(operationOptions);
        var createTask = persistence.CreateCountAsync(
            context,
            new InventoryCountCreateCommand(
                Guid.NewGuid(), scope, warehouse.Code, warehouse.Name, InventoryCountType.Full, context.ActorId, null,
                [new InventoryCountLineCommand(Guid.NewGuid(), null, 1, productAId, unitAId, "", 0m, productA)],
                DateTimeOffset.UtcNow, context.ActorId, DateTimeOffset.UtcNow, "sql-full-race-create", "sql-full-race-key", "sql-full-race-fingerprint"));
        await gate.WaitAsync();

        var movementGate = new StockMovementInsertAttemptGate();
        var movementOptions = new DbContextOptionsBuilder()
            .UseSqlServer(connectionString, sql => sql.CommandTimeout(15))
            .AddInterceptors(movementGate)
            .Options;
        var movementPostedAt = DateTimeOffset.UtcNow.AddMinutes(-5);
        var movementTask = Task.Run(async () =>
        {
            await using var movementDb = new InventoryDbContext(movementOptions, _fixture.TenantA);
            movementDb.StockMovements.Add(new InventoryStockMovementEntity(
                tenantId, Guid.NewGuid(), companyId, null, warehouseId, warehouse.Code, warehouse.Name,
                productBId, productB.Sku, productB.Name, unitBId, productB.BaseUnitOfMeasureCode,
                InventoryMovementDirection.Inbound, 3m, null, null, InventoryValuationStatus.Pending, null,
                InventoryMovementSourceType.StockAdjustment, Guid.NewGuid(), Guid.NewGuid(), null,
                DateOnly.FromDateTime(movementPostedAt.UtcDateTime), context.ActorId, "sql-full-race-boundary-b", movementPostedAt));
            await movementDb.SaveChangesAsync();
        });
        await movementGate.WaitAsync();
        Assert.False(movementTask.IsCompleted);
        gate.Release();
        var created = Assert.IsType<InventoryCountRecord>(await createTask.WaitAsync(TimeSpan.FromSeconds(30)));
        await movementTask.WaitAsync(TimeSpan.FromSeconds(30));

        Assert.DoesNotContain(created.Lines, item => item.ProductId == productBId);
        Assert.True(movementPostedAt < created.SnapshotCutoff);

        var persistenceAfterBoundary = new InventoryPersistence(seedOptions);
        var submitted = Assert.IsType<InventoryCountRecord>(await persistenceAfterBoundary.SubmitCountAsync(
            context,
            new InventoryCountSubmitCommand(
                created.Id, created.Version,
                created.Lines.Select(item => new InventoryCountObservationRequest(item.Id, item.ExpectedQuantity ?? 0m)).ToArray(),
                context.ActorId, "sql-full-race-submit", "sql-full-race-submit-fingerprint", "sql-full-race-submit-correlation", DateTimeOffset.UtcNow)));
        var post = Assert.IsType<InventoryCountRecord>(await persistenceAfterBoundary.PostCountAsync(
            context,
            new InventoryControlActionCommand(
                submitted.Id, submitted.Version, context.ActorId, "sql full race", null, "sql-full-race-post-correlation", "sql-full-race-post", "sql-full-race-post-fingerprint", DateTimeOffset.UtcNow)));
        Assert.Equal(InventoryControlDocumentStatus.ResnapshotRequired, post.Status);
    }

    [Fact]
    public async Task MESP130_sql_server_cycle_count_boundary_blocks_selected_insert_after_fence_read()
    {
        var connectionString = await GetConnectionStringAsync();
        var seedOptions = new DbContextOptionsBuilder().UseSqlServer(connectionString).Options;
        var tenantId = _fixture.TenantA.TenantId;
        var companyId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var unitId = Guid.NewGuid();
        var warehouse = new InventoryWarehouseOption(tenantId.Value, companyId, null, warehouseId, "WH-CYCLE-FENCE", "Cycle Count fence warehouse");
        var product = new InventoryProductReference(tenantId.Value, productId, "SKU-CYCLE-FENCE", "Cycle Count fence product", unitId, "EA", true, true, false);
        var context = InventoryContext(_fixture.TenantA);
        var scope = new InventoryScope(tenantId.Value, companyId, null, warehouseId);

        await using (var seedDb = new InventoryDbContext(seedOptions, _fixture.TenantA))
        {
            seedDb.StockMovements.Add(new InventoryStockMovementEntity(
                tenantId, Guid.NewGuid(), companyId, null, warehouseId, warehouse.Code, warehouse.Name,
                productId, product.Sku, product.Name, unitId, product.BaseUnitOfMeasureCode,
                InventoryMovementDirection.Inbound, 10m, null, null, InventoryValuationStatus.Pending, null,
                InventoryMovementSourceType.OpeningBalance, Guid.NewGuid(), Guid.NewGuid(), null,
                DateOnly.FromDateTime(DateTime.UtcNow), context.ActorId, "sql-cycle-fence-seed", DateTimeOffset.UtcNow));
            await seedDb.SaveChangesAsync();
        }

        var fenceGate = new SelectedIdentityLedgerCountGate();
        var operationOptions = new DbContextOptionsBuilder()
            .UseSqlServer(connectionString, sql => sql.CommandTimeout(15))
            .AddInterceptors(fenceGate)
            .Options;
        var persistence = new InventoryPersistence(operationOptions);
        var createTask = persistence.CreateCountAsync(
            context,
            new InventoryCountCreateCommand(
                Guid.NewGuid(), scope, warehouse.Code, warehouse.Name, InventoryCountType.Cycle, context.ActorId, null,
                [new InventoryCountLineCommand(Guid.NewGuid(), null, 1, productId, unitId, "", 0m, product)],
                DateTimeOffset.UtcNow, context.ActorId, DateTimeOffset.UtcNow, "sql-cycle-fence-create", "sql-cycle-fence-key", "sql-cycle-fence-fingerprint"));
        await fenceGate.WaitAsync();

        var movementGate = new StockMovementInsertAttemptGate();
        var movementOptions = new DbContextOptionsBuilder()
            .UseSqlServer(connectionString, sql => sql.CommandTimeout(15))
            .AddInterceptors(movementGate)
            .Options;
        var movementPostedAt = DateTimeOffset.UtcNow.AddMinutes(-5);
        var movementTask = Task.Run(async () =>
        {
            await using var movementDb = new InventoryDbContext(movementOptions, _fixture.TenantA);
            movementDb.StockMovements.Add(new InventoryStockMovementEntity(
                tenantId, Guid.NewGuid(), companyId, null, warehouseId, warehouse.Code, warehouse.Name,
                productId, product.Sku, product.Name, unitId, product.BaseUnitOfMeasureCode,
                InventoryMovementDirection.Inbound, 1m, null, null, InventoryValuationStatus.Pending, null,
                InventoryMovementSourceType.StockAdjustment, Guid.NewGuid(), Guid.NewGuid(), null,
                DateOnly.FromDateTime(movementPostedAt.UtcDateTime), context.ActorId, "sql-cycle-fence-movement", movementPostedAt));
            await movementDb.SaveChangesAsync();
        });
        await movementGate.WaitAsync();
        Assert.False(movementTask.IsCompleted);
        fenceGate.Release();
        var created = Assert.IsType<InventoryCountRecord>(await createTask.WaitAsync(TimeSpan.FromSeconds(30)));
        await movementTask.WaitAsync(TimeSpan.FromSeconds(30));
        Assert.True(movementPostedAt < created.SnapshotCutoff);

        var persistenceAfterBoundary = new InventoryPersistence(seedOptions);
        var line = Assert.Single(created.Lines);
        var submitted = Assert.IsType<InventoryCountRecord>(await persistenceAfterBoundary.SubmitCountAsync(
            context,
            new InventoryCountSubmitCommand(
                created.Id, created.Version,
                [new InventoryCountObservationRequest(line.Id, 10m)],
                context.ActorId, "sql-cycle-fence-submit", "sql-cycle-fence-submit-fingerprint", "sql-cycle-fence-submit-correlation", DateTimeOffset.UtcNow)));
        var post = Assert.IsType<InventoryCountRecord>(await persistenceAfterBoundary.PostCountAsync(
            context,
            new InventoryControlActionCommand(
                submitted.Id, submitted.Version, context.ActorId, "sql cycle fence", null, "sql-cycle-fence-post-correlation", "sql-cycle-fence-post", "sql-cycle-fence-post-fingerprint", DateTimeOffset.UtcNow)));
        Assert.Equal(InventoryControlDocumentStatus.ResnapshotRequired, post.Status);
    }

    [Fact]
    public async Task MESP130_sql_server_correction_index_allows_one_direct_correction_only()
    {
        var connectionString = await GetConnectionStringAsync();
        var options = new DbContextOptionsBuilder().UseSqlServer(connectionString).Options;
        var originalId = Guid.NewGuid();
        var tenantId = _fixture.TenantA.TenantId;
        var companyId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var unitId = Guid.NewGuid();
        var actorId = Guid.NewGuid();
        await using (var createDb = new InventoryDbContext(options, _fixture.TenantA))
        {
            createDb.StockMovements.Add(new InventoryStockMovementEntity(
                tenantId, originalId, companyId, null, warehouseId, "WH-SQL", "SQL warehouse", productId,
                "SKU-SQL", "SQL product", unitId, "EA", InventoryMovementDirection.Inbound, 1m, null, null,
                InventoryValuationStatus.Pending, null, InventoryMovementSourceType.StockAdjustment, Guid.NewGuid(), Guid.NewGuid(), null,
                DateOnly.FromDateTime(DateTime.UtcNow), actorId, "sql-mesp130-original", DateTimeOffset.UtcNow));
            await createDb.SaveChangesAsync();
        }

        var correction = new InventoryStockMovementEntity(
            tenantId, Guid.NewGuid(), companyId, null, warehouseId, "WH-SQL", "SQL warehouse", productId,
            "SKU-SQL", "SQL product", unitId, "EA", InventoryMovementDirection.Outbound, 1m, null, null,
            InventoryValuationStatus.Pending, null, InventoryMovementSourceType.Correction, Guid.NewGuid(), Guid.NewGuid(), originalId,
            DateOnly.FromDateTime(DateTime.UtcNow), actorId, "sql-mesp130-correction-a", DateTimeOffset.UtcNow);
        await using (var firstCorrectionDb = new InventoryDbContext(options, _fixture.TenantA))
        {
            firstCorrectionDb.StockMovements.Add(correction);
            await firstCorrectionDb.SaveChangesAsync();
        }

        await using (var duplicateCorrectionDb = new InventoryDbContext(options, _fixture.TenantA))
        {
            duplicateCorrectionDb.StockMovements.Add(new InventoryStockMovementEntity(
                tenantId, Guid.NewGuid(), companyId, null, warehouseId, "WH-SQL", "SQL warehouse", productId,
                "SKU-SQL", "SQL product", unitId, "EA", InventoryMovementDirection.Outbound, 1m, null, null,
                InventoryValuationStatus.Pending, null, InventoryMovementSourceType.Correction, Guid.NewGuid(), Guid.NewGuid(), originalId,
                DateOnly.FromDateTime(DateTime.UtcNow), actorId, "sql-mesp130-correction-b", DateTimeOffset.UtcNow));
            await Assert.ThrowsAsync<DbUpdateException>(() => duplicateCorrectionDb.SaveChangesAsync());
        }

        await using var verifyDb = new InventoryDbContext(options, _fixture.TenantA);
        Assert.Equal(1, await verifyDb.StockMovements.CountAsync(item => item.CorrectionOfMovementId == originalId));
        await using var indexConnection = new SqlConnection(connectionString);
        await indexConnection.OpenAsync();
        await using var indexCommand = indexConnection.CreateCommand();
        indexCommand.CommandText = """
            SELECT COUNT(*)
            FROM sys.indexes AS indexes
            INNER JOIN sys.tables AS tables ON tables.object_id = indexes.object_id
            INNER JOIN sys.schemas AS schemas ON schemas.schema_id = tables.schema_id
            WHERE schemas.name = N'inventory'
              AND tables.name = N'StockLedgerMovements'
              AND indexes.name = N'IX_StockLedgerMovements_TenantId_CorrectionOfMovementId'
              AND indexes.is_unique = 1
              AND indexes.has_filter = 1
              AND indexes.filter_definition LIKE N'%CorrectionOfMovementId%IS NOT NULL%';
            """;
        Assert.Equal(1, Convert.ToInt32(await indexCommand.ExecuteScalarAsync()));
    }

    [Fact]
    public async Task MESP131_sql_server_process_fingerprint_is_bounded_and_durable_replay_conflict_is_explicit()
    {
        var connectionString = await GetConnectionStringAsync();
        var options = new DbContextOptionsBuilder().UseSqlServer(connectionString).Options;
        var tenantId = _fixture.TenantA.TenantId;
        var companyId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var unitId = Guid.NewGuid();
        var context = InventoryContext(_fixture.TenantA, "tenant.inventory.valuation.process");
        var movement = SqlMovement(tenantId, companyId, warehouseId, productId, unitId, InventoryMovementDirection.Inbound, 2m, 10m, "SAR", "sql-mesp131-fingerprint-movement");
        await using (var db = new InventoryDbContext(options, _fixture.TenantA))
        {
            db.StockMovements.Add(movement);
            await db.SaveChangesAsync();
        }

        var persistence = new InventoryValuationPersistence(options, null, null, null);
        var policyRequest = SqlPolicy(companyId, new DateOnly(2026, 1, 1));
        var policy = await persistence.CreatePolicyAsync(
            context,
            new InventoryValuationPolicyCommand(Guid.NewGuid(), policyRequest, context.ActorId, DateTimeOffset.UtcNow, "sql-mesp131-fingerprint-policy", "sql-mesp131-fingerprint-policy-key", "sql-mesp131-fingerprint-policy-fingerprint"));
        Assert.True(policy.Succeeded, policy.Code);

        var restRequest = new InventoryValuationProcessRequest(companyId, null, warehouseId, productId, unitId);
        var fingerprint = InventoryFingerprints.Create(restRequest);
        Assert.InRange(fingerprint.Length, 1, 128);
        var command = new InventoryValuationProcessCommand(companyId, null, warehouseId, productId, unitId, context.ActorId, DateTimeOffset.UtcNow, "sql-mesp131-fingerprint-process", "sql-mesp131-fingerprint-key", fingerprint);
        var result = await persistence.ProcessAsync(context, command);
        Assert.True(result.Succeeded, result.Code);

        await using (var db = new InventoryDbContext(options, _fixture.TenantA))
        {
            var run = await db.ValuationRuns.AsNoTracking().SingleAsync(item => item.IdempotencyKey == command.IdempotencyKey);
            Assert.Equal(fingerprint, run.RequestFingerprint);
            Assert.InRange(run.RequestFingerprint.Length, 1, 128);
        }

        var conflict = await persistence.ProcessAsync(context, command with { RequestFingerprint = fingerprint + "-different" });
        Assert.False(conflict.Succeeded);
        Assert.Equal("idempotency_conflict", conflict.Code);
    }

    [Fact]
    public async Task MESP131_sql_server_first_scope_concurrency_cannot_fork_state_anchor_or_event()
    {
        var connectionString = await GetConnectionStringAsync();
        var options = new DbContextOptionsBuilder().UseSqlServer(connectionString, sql => sql.CommandTimeout(30)).Options;
        var tenantId = _fixture.TenantA.TenantId;
        var companyId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var unitId = Guid.NewGuid();
        var context = InventoryContext(_fixture.TenantA, "tenant.inventory.valuation.process");
        await using (var db = new InventoryDbContext(options, _fixture.TenantA))
        {
            db.StockMovements.Add(SqlMovement(tenantId, companyId, warehouseId, productId, unitId, InventoryMovementDirection.Inbound, 1m, 10m, "SAR", "sql-mesp131-concurrency-movement"));
            await db.SaveChangesAsync();
        }

        var firstPersistence = new InventoryValuationPersistence(options, null, null, null);
        var secondPersistence = new InventoryValuationPersistence(options, null, null, null);
        var policy = await firstPersistence.CreatePolicyAsync(context, new InventoryValuationPolicyCommand(Guid.NewGuid(), SqlPolicy(companyId, new DateOnly(2026, 1, 1)), context.ActorId, DateTimeOffset.UtcNow, "sql-mesp131-concurrency-policy", "sql-mesp131-concurrency-policy-key", "sql-mesp131-concurrency-policy-fingerprint"));
        Assert.True(policy.Succeeded, policy.Code);
        var firstCommand = new InventoryValuationProcessCommand(companyId, null, warehouseId, productId, unitId, context.ActorId, DateTimeOffset.UtcNow, "sql-mesp131-concurrency-a", "sql-mesp131-concurrency-a-key", "sql-mesp131-concurrency-a-fingerprint");
        var secondCommand = firstCommand with { CorrelationId = "sql-mesp131-concurrency-b", IdempotencyKey = "sql-mesp131-concurrency-b-key", RequestFingerprint = "sql-mesp131-concurrency-b-fingerprint" };
        var results = await Task.WhenAll(
            Task.Run(() => firstPersistence.ProcessAsync(context, firstCommand)),
            Task.Run(() => secondPersistence.ProcessAsync(context, secondCommand)));

        Assert.All(results, result => Assert.True(result.Succeeded || result.Code == "valuation_concurrency_conflict", result.Code));
        await using var verifyDb = new InventoryDbContext(options, _fixture.TenantA);
        Assert.Equal(1, await verifyDb.ValuationStates.CountAsync(item => item.CompanyId == companyId && item.WarehouseId == warehouseId && item.ProductId == productId && item.UnitOfMeasureId == unitId));
        Assert.Equal(1, await verifyDb.ValuationScopeAnchors.CountAsync(item => item.CompanyId == companyId && item.WarehouseId == warehouseId && item.ProductId == productId && item.UnitOfMeasureId == unitId));
        Assert.Equal(1, await verifyDb.MovementValuationEvents.CountAsync(item => item.CompanyId == companyId && item.Status == InventoryValuationEventStatus.Applied));
    }

    [Fact]
    public async Task MESP131_sql_server_compatible_policy_transition_persists_current_state_policy_version()
    {
        var connectionString = await GetConnectionStringAsync();
        var options = new DbContextOptionsBuilder().UseSqlServer(connectionString).Options;
        var tenantId = _fixture.TenantA.TenantId;
        var companyId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var unitId = Guid.NewGuid();
        var context = InventoryContext(_fixture.TenantA, "tenant.inventory.valuation.process");
        await using (var db = new InventoryDbContext(options, _fixture.TenantA))
        {
            db.StockMovements.AddRange(
                SqlMovement(tenantId, companyId, warehouseId, productId, unitId, InventoryMovementDirection.Inbound, 2m, 10m, "SAR", "sql-mesp131-transition-first", new DateOnly(2026, 8, 2)),
                SqlMovement(tenantId, companyId, warehouseId, productId, unitId, InventoryMovementDirection.Inbound, 2m, 20m, "SAR", "sql-mesp131-transition-second", new DateOnly(2026, 9, 2)));
            await db.SaveChangesAsync();
        }

        var persistence = new InventoryValuationPersistence(options, null, null, null);
        var first = await persistence.CreatePolicyAsync(context, new InventoryValuationPolicyCommand(Guid.NewGuid(), SqlPolicy(companyId, new DateOnly(2026, 8, 1), new DateOnly(2026, 8, 31)), context.ActorId, DateTimeOffset.UtcNow, "sql-mesp131-transition-policy-one", "sql-mesp131-transition-policy-one-key", "sql-mesp131-transition-policy-one-fingerprint"));
        var second = await persistence.CreatePolicyAsync(context, new InventoryValuationPolicyCommand(Guid.NewGuid(), SqlPolicy(companyId, new DateOnly(2026, 9, 1)), context.ActorId, DateTimeOffset.UtcNow, "sql-mesp131-transition-policy-two", "sql-mesp131-transition-policy-two-key", "sql-mesp131-transition-policy-two-fingerprint"));
        Assert.True(first.Succeeded, first.Code);
        Assert.True(second.Succeeded, second.Code);
        var processed = await persistence.ProcessAsync(context, new InventoryValuationProcessCommand(companyId, null, warehouseId, productId, unitId, context.ActorId, DateTimeOffset.UtcNow, "sql-mesp131-transition-process", "sql-mesp131-transition-process-key", "sql-mesp131-transition-process-fingerprint"));
        Assert.True(processed.Succeeded, processed.Code);

        await using var verifyDb = new InventoryDbContext(options, _fixture.TenantA);
        var state = await verifyDb.ValuationStates.AsNoTracking().SingleAsync(item => item.CompanyId == companyId && item.WarehouseId == warehouseId && item.ProductId == productId && item.UnitOfMeasureId == unitId);
        Assert.Equal(second.Value!.Id, state.CurrentPolicyId);
        Assert.Equal(2, state.CurrentPolicyVersionNumber);
        Assert.Equal(4m, state.Quantity);
        Assert.Equal(60m, state.Value);
    }

    [Fact]
    public async Task MESP131_sql_server_unique_pool_indexes_are_unique_and_policy_free()
    {
        await using var connection = new SqlConnection(await GetConnectionStringAsync());
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*)
            FROM sys.indexes AS indexes
            INNER JOIN sys.tables AS tables ON tables.object_id = indexes.object_id
            INNER JOIN sys.schemas AS schemas ON schemas.schema_id = tables.schema_id
            WHERE schemas.name = N'inventory'
              AND tables.name IN (N'ValuationStates', N'ValuationScopeAnchors')
              AND indexes.is_unique = 1
              AND indexes.name IN (N'IX_ValuationStates_TenantId_CompanyId_BranchId_WarehouseId_ProductId_UnitOfMeasureId_TrackingIdentity', N'IX_ValuationScopeAnchors_TenantId_CompanyId_BranchId_WarehouseId_ProductId_UnitOfMeasureId_TrackingIdentity');
            SELECT COUNT(*)
            FROM sys.indexes AS indexes
            INNER JOIN sys.index_columns AS indexColumns ON indexColumns.object_id = indexes.object_id AND indexColumns.index_id = indexes.index_id
            INNER JOIN sys.columns AS columns ON columns.object_id = indexColumns.object_id AND columns.column_id = indexColumns.column_id
            INNER JOIN sys.tables AS tables ON tables.object_id = indexes.object_id
            INNER JOIN sys.schemas AS schemas ON schemas.schema_id = tables.schema_id
            WHERE schemas.name = N'inventory'
              AND tables.name IN (N'ValuationStates', N'ValuationScopeAnchors')
              AND indexes.name IN (N'IX_ValuationStates_TenantId_CompanyId_BranchId_WarehouseId_ProductId_UnitOfMeasureId_TrackingIdentity', N'IX_ValuationScopeAnchors_TenantId_CompanyId_BranchId_WarehouseId_ProductId_UnitOfMeasureId_TrackingIdentity')
              AND columns.name = N'PolicyId';
            SELECT COUNT(*)
            FROM sys.indexes AS indexes
            INNER JOIN sys.tables AS tables ON tables.object_id = indexes.object_id
            INNER JOIN sys.schemas AS schemas ON schemas.schema_id = tables.schema_id
            WHERE schemas.name = N'inventory'
              AND tables.name = N'ValuationPolicies'
              AND indexes.name = N'IX_ValuationPolicies_TenantId_CompanyId_VersionNumber'
              AND indexes.is_unique = 1;
            """;
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal(2, reader.GetInt32(0));
        Assert.True(await reader.NextResultAsync());
        Assert.True(await reader.ReadAsync());
        Assert.Equal(0, reader.GetInt32(0));
        Assert.True(await reader.NextResultAsync());
        Assert.True(await reader.ReadAsync());
        Assert.Equal(1, reader.GetInt32(0));
    }

    [Fact]
    public void MESP131_final_migration_has_populated_target_model_metadata()
    {
        const string migrationId = "20260823225921_MESP131SolFinalValuationIntegrity";
        var migrationType = typeof(MESP131SolFinalValuationIntegrity);
        var migrationAttribute = Assert.Single(migrationType.GetCustomAttributes<MigrationAttribute>());
        Assert.Equal(migrationId, migrationAttribute.Id);

        var targetModel = new MESP131SolFinalValuationIntegrity().TargetModel;
        Assert.NotEmpty(targetModel.GetEntityTypes());

        var valuationEvents = targetModel.FindEntityType(typeof(InventoryMovementValuationEventEntity));
        Assert.NotNull(valuationEvents);
        Assert.NotNull(valuationEvents.FindProperty(nameof(InventoryMovementValuationEventEntity.FormulaMovementValue)));
        Assert.NotNull(valuationEvents.FindProperty(nameof(InventoryMovementValuationEventEntity.RoundingAdjustmentAmount)));

        var financeHandoffs = targetModel.FindEntityType(typeof(InventoryFinanceValuationHandoffEntity));
        Assert.NotNull(financeHandoffs);
        Assert.NotNull(financeHandoffs.FindProperty(nameof(InventoryFinanceValuationHandoffEntity.RoundingAdjustmentAmount)));

        var snapshotModel = new InventoryDbContextModelSnapshot().Model;
        Assert.NotEmpty(snapshotModel.GetEntityTypes());
        Assert.NotNull(snapshotModel.FindEntityType(typeof(InventoryMovementValuationEventEntity))?
            .FindProperty(nameof(InventoryMovementValuationEventEntity.FormulaMovementValue)));
        Assert.NotNull(snapshotModel.FindEntityType(typeof(InventoryMovementValuationEventEntity))?
            .FindProperty(nameof(InventoryMovementValuationEventEntity.RoundingAdjustmentAmount)));
        Assert.NotNull(snapshotModel.FindEntityType(typeof(InventoryFinanceValuationHandoffEntity))?
            .FindProperty(nameof(InventoryFinanceValuationHandoffEntity.RoundingAdjustmentAmount)));
    }

    [Fact]
    public async Task MESP131_sql_server_finance_handoff_persists_direction_signed_amount_and_unique_evidence()
    {
        var connectionString = await GetConnectionStringAsync();
        var options = new DbContextOptionsBuilder().UseSqlServer(connectionString).Options;
        var tenantId = _fixture.TenantA.TenantId;
        var companyId = Guid.NewGuid();
        var warehouseId = Guid.NewGuid();
        var productId = Guid.NewGuid();
        var unitId = Guid.NewGuid();
        var context = InventoryContext(_fixture.TenantA, "tenant.inventory.valuation.process");
        await using (var db = new InventoryDbContext(options, _fixture.TenantA))
        {
            db.StockMovements.AddRange(
                SqlMovement(tenantId, companyId, warehouseId, productId, unitId, InventoryMovementDirection.Inbound, 10m, 10m, "SAR", "sql-mesp131-handoff-in"),
                SqlMovement(tenantId, companyId, warehouseId, productId, unitId, InventoryMovementDirection.Outbound, 2m, null, null, "sql-mesp131-handoff-out", new DateOnly(2026, 1, 2), InventoryMovementSourceType.StockIssue));
            await db.SaveChangesAsync();
        }

        var persistence = new InventoryValuationPersistence(options, null, null, null);
        var policy = await persistence.CreatePolicyAsync(context, new InventoryValuationPolicyCommand(Guid.NewGuid(), SqlPolicy(companyId, new DateOnly(2026, 1, 1)), context.ActorId, DateTimeOffset.UtcNow, "sql-mesp131-handoff-policy", "sql-mesp131-handoff-policy-key", "sql-mesp131-handoff-policy-fingerprint"));
        Assert.True(policy.Succeeded, policy.Code);
        var processed = await persistence.ProcessAsync(context, new InventoryValuationProcessCommand(companyId, null, warehouseId, productId, unitId, context.ActorId, DateTimeOffset.UtcNow, "sql-mesp131-handoff-process", "sql-mesp131-handoff-process-key", "sql-mesp131-handoff-process-fingerprint"));
        Assert.True(processed.Succeeded, processed.Code);
        var handoffs = (await persistence.ListFinanceHandoffsAsync(context, new InventoryValuationQuery(companyId))).OrderBy(item => item.LedgerSequence).ToArray();
        Assert.Equal(2, handoffs.Length);
        Assert.Equal(100m, handoffs[0].BaseAmount);
        Assert.Equal(100m, handoffs[0].SignedBaseAmount);
        Assert.Equal(-20m, handoffs[1].SignedBaseAmount);
        Assert.All(handoffs, handoff => Assert.Equal("inventory-valuation-finance.v1", handoff.ContractVersion));

        await using var indexConnection = new SqlConnection(connectionString);
        await indexConnection.OpenAsync();
        await using var indexCommand = indexConnection.CreateCommand();
        indexCommand.CommandText = """
            SELECT COUNT(*)
            FROM sys.indexes AS indexes
            INNER JOIN sys.tables AS tables ON tables.object_id = indexes.object_id
            INNER JOIN sys.schemas AS schemas ON schemas.schema_id = tables.schema_id
            WHERE schemas.name = N'inventory'
              AND tables.name = N'FinanceValuationHandoffs'
              AND indexes.name = N'IX_FinanceValuationHandoffs_TenantId_MovementId'
              AND indexes.is_unique = 1;
            """;
        Assert.Equal(1, Convert.ToInt32(await indexCommand.ExecuteScalarAsync()));
    }

    [Fact]
    public async Task MESP131_sql_server_final_integrity_persists_closeout_evidence_and_isolates_tracking_failure()
    {
        var connectionString = await GetConnectionStringAsync();
        var options = new DbContextOptionsBuilder().UseSqlServer(connectionString).Options;
        var tenantId = _fixture.TenantA.TenantId;
        var context = InventoryContext(_fixture.TenantA, "tenant.inventory.valuation.process");

        var closeoutCompanyId = Guid.NewGuid();
        var closeoutWarehouseId = Guid.NewGuid();
        var closeoutProductId = Guid.NewGuid();
        var closeoutUnitId = Guid.NewGuid();
        await using (var db = new InventoryDbContext(options, _fixture.TenantA))
        {
            db.StockMovements.AddRange(
                SqlMovement(tenantId, closeoutCompanyId, closeoutWarehouseId, closeoutProductId, closeoutUnitId, InventoryMovementDirection.Inbound, 2m, 33.33m, "SAR", "sql-mesp131-final-in-one"),
                SqlMovement(tenantId, closeoutCompanyId, closeoutWarehouseId, closeoutProductId, closeoutUnitId, InventoryMovementDirection.Inbound, 1m, 33.34m, "SAR", "sql-mesp131-final-in-two"),
                SqlMovement(tenantId, closeoutCompanyId, closeoutWarehouseId, closeoutProductId, closeoutUnitId, InventoryMovementDirection.Outbound, 3m, null, null, "sql-mesp131-final-out", new DateOnly(2026, 1, 3), InventoryMovementSourceType.StockIssue));
            await db.SaveChangesAsync();
        }

        var persistence = new InventoryValuationPersistence(options, null, null, null);
        var closeoutPolicy = await persistence.CreatePolicyAsync(context, new InventoryValuationPolicyCommand(Guid.NewGuid(), SqlPolicy(closeoutCompanyId, new DateOnly(2026, 1, 1), unitCostScale: 2, amountScale: 4), context.ActorId, DateTimeOffset.UtcNow, "sql-mesp131-final-closeout-policy", "sql-mesp131-final-closeout-policy-key", "sql-mesp131-final-closeout-policy-fingerprint"));
        Assert.True(closeoutPolicy.Succeeded, closeoutPolicy.Code);
        var closeoutProcess = await persistence.ProcessAsync(context, new InventoryValuationProcessCommand(closeoutCompanyId, null, closeoutWarehouseId, closeoutProductId, closeoutUnitId, context.ActorId, DateTimeOffset.UtcNow, "sql-mesp131-final-closeout-process", "sql-mesp131-final-closeout-process-key", "sql-mesp131-final-closeout-process-fingerprint"));
        Assert.True(closeoutProcess.Succeeded, closeoutProcess.Code);
        var closeoutEvent = Assert.Single(await persistence.ListEventsAsync(context, new InventoryValuationQuery(closeoutCompanyId)), item => item.Direction == InventoryMovementDirection.Outbound);
        Assert.Equal(99.99m, closeoutEvent.FormulaMovementValue);
        Assert.Equal(0.01m, closeoutEvent.RoundingAdjustmentAmount);
        Assert.Equal(100m, closeoutEvent.MovementValue);
        Assert.Equal(0m, closeoutEvent.NewQuantity);
        Assert.Equal(0m, closeoutEvent.NewValue);
        var closeoutHandoff = Assert.Single(
            await persistence.ListFinanceHandoffsAsync(context, new InventoryValuationQuery(closeoutCompanyId)),
            item => item.Direction == InventoryMovementDirection.Outbound);
        Assert.Equal(100m, closeoutHandoff.BaseAmount);
        Assert.Equal(-100m, closeoutHandoff.SignedBaseAmount);
        Assert.Equal(0.01m, closeoutHandoff.RoundingAdjustmentAmount);

        var trackingCompanyId = Guid.NewGuid();
        var trackingWarehouseId = Guid.NewGuid();
        var trackingProductId = Guid.NewGuid();
        var trackingUnitId = Guid.NewGuid();
        await using (var db = new InventoryDbContext(options, _fixture.TenantA))
        {
            db.StockMovements.AddRange(
                SqlMovement(tenantId, trackingCompanyId, trackingWarehouseId, trackingProductId, trackingUnitId, InventoryMovementDirection.Inbound, 1m, 10m, "USD", "sql-mesp131-final-lot-a-failure", trackingIdentity: "LOT-A"),
                SqlMovement(tenantId, trackingCompanyId, trackingWarehouseId, trackingProductId, trackingUnitId, InventoryMovementDirection.Inbound, 1m, 20m, "SAR", "sql-mesp131-final-lot-b", trackingIdentity: "LOT-B"),
                SqlMovement(tenantId, trackingCompanyId, trackingWarehouseId, trackingProductId, trackingUnitId, InventoryMovementDirection.Inbound, 1m, 30m, "SAR", "sql-mesp131-final-lot-a-successor", trackingIdentity: "LOT-A"),
                SqlMovement(tenantId, trackingCompanyId, trackingWarehouseId, trackingProductId, trackingUnitId, InventoryMovementDirection.Inbound, 1m, 40m, "SAR", "sql-mesp131-final-lot-b-successor", trackingIdentity: "LOT-B"));
            await db.SaveChangesAsync();
        }

        var trackingPolicy = await persistence.CreatePolicyAsync(context, new InventoryValuationPolicyCommand(Guid.NewGuid(), SqlPolicy(trackingCompanyId, new DateOnly(2026, 1, 1), scopeMode: InventoryValuationScopeMode.WarehouseProductUomTracking), context.ActorId, DateTimeOffset.UtcNow, "sql-mesp131-final-tracking-policy", "sql-mesp131-final-tracking-policy-key", "sql-mesp131-final-tracking-policy-fingerprint"));
        Assert.True(trackingPolicy.Succeeded, trackingPolicy.Code);
        var trackingProcess = await persistence.ProcessAsync(context, new InventoryValuationProcessCommand(trackingCompanyId, null, trackingWarehouseId, trackingProductId, trackingUnitId, context.ActorId, DateTimeOffset.UtcNow, "sql-mesp131-final-tracking-process", "sql-mesp131-final-tracking-process-key", "sql-mesp131-final-tracking-process-fingerprint"));
        Assert.True(trackingProcess.Succeeded, trackingProcess.Code);
        Assert.Equal(2, trackingProcess.Value!.AppliedCount);
        Assert.Equal(2, trackingProcess.Value.PendingCount);
        var trackingEvents = (await persistence.ListEventsAsync(context, new InventoryValuationQuery(trackingCompanyId))).OrderBy(item => item.LedgerSequence).ToArray();
        Assert.Equal(InventoryValuationEventStatus.Pending, trackingEvents[0].Status);
        Assert.Equal(InventoryValuationEventStatus.Applied, trackingEvents[1].Status);
        Assert.Equal("pending_predecessor", trackingEvents[2].StatusCode);
        Assert.Equal(InventoryValuationEventStatus.Applied, trackingEvents[3].Status);

        await using var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT COUNT(*)
            FROM sys.columns
            INNER JOIN sys.tables ON sys.tables.object_id = sys.columns.object_id
            INNER JOIN sys.schemas ON sys.schemas.schema_id = sys.tables.schema_id
            WHERE sys.schemas.name = N'inventory'
              AND sys.tables.name = N'MovementValuationEvents'
              AND sys.columns.name IN (N'FormulaMovementValue', N'RoundingAdjustmentAmount');
            SELECT COUNT(*)
            FROM sys.columns
            INNER JOIN sys.tables ON sys.tables.object_id = sys.columns.object_id
            INNER JOIN sys.schemas ON sys.schemas.schema_id = sys.tables.schema_id
            WHERE sys.schemas.name = N'inventory'
              AND sys.tables.name = N'FinanceValuationHandoffs'
              AND sys.columns.name = N'RoundingAdjustmentAmount';
            """;
        await using var reader = await command.ExecuteReaderAsync();
        Assert.True(await reader.ReadAsync());
        Assert.Equal(2, reader.GetInt32(0));
        Assert.True(await reader.NextResultAsync());
        Assert.True(await reader.ReadAsync());
        Assert.Equal(1, reader.GetInt32(0));
    }

    private sealed record SqlFinanceScenario(
        DbContextOptions Options,
        Guid CompanyId,
        FinancePersistence FirstPersistence,
        FinancePersistence SecondPersistence,
        FinanceRequestContext FirstContext,
        FinanceRequestContext SecondContext,
        FinanceAccountRecord DebitAccount,
        FinanceAccountRecord CreditAccount,
        FinanceFiscalPeriodRecord Period,
        FinanceJournalRecord ApprovedJournal);

    private async Task<SqlFinanceScenario> CreateApprovedFinanceJournalAsync(bool foreignCurrency = false)
    {
        var connectionString = await GetConnectionStringAsync();
        var options = SqlServerMigrationConfiguration.Configure(connectionString, SqlServerMigrationConfiguration.FinanceHistoryTable);
        var companyId = Guid.NewGuid();
        var provider = new ConfiguredFinanceCompanyProvider([new FinanceCompanyOption(_fixture.TenantA.TenantId.Value, companyId, "SQL Finance Company", "SAR")]);
        IMasterDataExchangeRatePersistence exchangeRates = foreignCurrency
            ? new SqlFinanceExchangeRatePersistence(_fixture.TenantA.TenantId, "USD", "SAR", 3.75m)
            : new UnavailableMasterDataExchangeRatePersistence();
        var first = new FinancePersistence(options, provider, new UnavailableInventoryValuationPersistence(), exchangeRates);
        var second = new FinancePersistence(options, provider, new UnavailableInventoryValuationPersistence(), exchangeRates);
        var firstContext = FinanceContext("tenant.finance.journal.create");
        var secondContext = FinanceContext("tenant.finance.journal.post");
        var accounts = await CreateFinanceAccountsAsync(first, firstContext, companyId, "sql-race");
        var calendar = await first.CreateCalendarAsync(firstContext, new FinanceFiscalCalendarCommand(companyId, "SQL Finance FY", Guid.NewGuid(), "sql-race-calendar", "sql-race-calendar"));
        Assert.True(calendar.Succeeded, calendar.Code);
        var year = await first.CreateYearAsync(firstContext, new FinanceFiscalYearCommand(calendar.Value!.Id, 2026, new DateOnly(2026, 1, 1), new DateOnly(2026, 12, 31), Guid.NewGuid(), "sql-race-year", "sql-race-year"));
        Assert.True(year.Succeeded, year.Code);
        var period = await first.CreatePeriodAsync(firstContext, new FinanceFiscalPeriodCommand(year.Value!.Id, 1, "2026-01", "January", null, new DateOnly(2026, 1, 1), new DateOnly(2026, 1, 31), Guid.NewGuid(), "sql-race-period", "sql-race-period"));
        Assert.True(period.Succeeded, period.Code);
        var opened = await first.SetPeriodStateAsync(firstContext, new FinancePeriodStateCommand(period.Value!.Id, FinanceFiscalPeriodState.Open, null, period.Value.Version, "sql-race-open", "sql-race-open"));
        Assert.True(opened.Succeeded, opened.Code);

        var rateId = foreignCurrency ? ((SqlFinanceExchangeRatePersistence)exchangeRates).RateId : (Guid?)null;
        var rateVersionId = foreignCurrency ? ((SqlFinanceExchangeRatePersistence)exchangeRates).RateVersionId : (Guid?)null;
        var currency = foreignCurrency ? "USD" : (string?)null;
        var rate = foreignCurrency ? 3.75m : 1m;
        var command = new FinanceJournalCommand(
            companyId,
            new DateOnly(2026, 1, 15),
            new DateOnly(2026, 1, 15),
            currency,
            rate,
            rateId,
            rateVersionId,
            foreignCurrency ? 1 : null,
            "manual-journal.v1",
            "manual",
            null,
            null,
            null,
            "SQL concurrency journal",
            [
                new FinanceJournalLineCommand(accounts.Debit.Id, 10m, 0m, foreignCurrency ? 10m : null, foreignCurrency ? "USD" : "SAR", null, null),
                new FinanceJournalLineCommand(accounts.Credit.Id, 0m, 10m, foreignCurrency ? 10m : null, foreignCurrency ? "USD" : "SAR", null, null)
            ],
            Guid.NewGuid(),
            "sql-race-create",
            "sql-race-create");
        var created = await first.CreateJournalAsync(firstContext, command);
        Assert.True(created.Succeeded, created.Code);
        var submitted = await first.TransitionJournalAsync(firstContext, new FinanceJournalActionCommand(created.Value!.Id, created.Value.Version, "SQL concurrency submit", "sql-race-submit", "sql-race-submit"), FinanceJournalStatus.Submitted);
        Assert.True(submitted.Succeeded, submitted.Code);
        var approverContext = FinanceContext("tenant.finance.journal.approve");
        var approved = await first.TransitionJournalAsync(approverContext, new FinanceJournalActionCommand(submitted.Value!.Id, submitted.Value.Version, "SQL concurrency approve", "sql-race-approve", "sql-race-approve"), FinanceJournalStatus.Approved);
        Assert.True(approved.Succeeded, approved.Code);

        return new SqlFinanceScenario(options, companyId, first, second, firstContext, secondContext, accounts.Debit, accounts.Credit, opened.Value!, approved.Value!);
    }

    private async Task<(FinanceAccountRecord Debit, FinanceAccountRecord Credit)> CreateFinanceAccountsAsync(IFinancePersistence persistence, FinanceRequestContext context, Guid companyId, string prefix)
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var debit = await persistence.CreateAccountAsync(context, new FinanceAccountCommand(companyId, $"{prefix}-D-{suffix}", "SQL debit", null, null, FinanceAccountType.Asset, true, FinanceCurrencyBehavior.TransactionCurrencyAllowed, new DateOnly(2026, 1, 1), null, Guid.NewGuid(), null, $"{prefix}-debit-{suffix}", $"{prefix}-debit-{suffix}"));
        var credit = await persistence.CreateAccountAsync(context, new FinanceAccountCommand(companyId, $"{prefix}-C-{suffix}", "SQL credit", null, null, FinanceAccountType.Revenue, true, FinanceCurrencyBehavior.TransactionCurrencyAllowed, new DateOnly(2026, 1, 1), null, Guid.NewGuid(), null, $"{prefix}-credit-{suffix}", $"{prefix}-credit-{suffix}"));
        Assert.True(debit.Succeeded, debit.Code);
        Assert.True(credit.Succeeded, credit.Code);
        return (debit.Value!, credit.Value!);
    }

    private static FinanceJournalCommand SqlManualJournal(Guid companyId, (FinanceAccountRecord Debit, FinanceAccountRecord Credit) accounts, string key) =>
        new(companyId, new DateOnly(2026, 1, 15), new DateOnly(2026, 1, 15), null, 1m, null, null, null, "manual-journal.v1", "manual", null, null, null, "SQL sequence journal", [new FinanceJournalLineCommand(accounts.Debit.Id, 10m, 0m, null, "SAR", null, null), new FinanceJournalLineCommand(accounts.Credit.Id, 0m, 10m, null, "SAR", null, null)], Guid.NewGuid(), key, key);

    private FinanceRequestContext FinanceContext(string permission)
    {
        var foundation = FoundationRequestContext.ForTenant(Guid.NewGuid(), Guid.NewGuid(), _fixture.TenantA, permission);
        Assert.True(FinanceRequestContext.TryCreate(foundation, out var context));
        return context!;
    }

    private static async Task<FinanceOperationResult<T>> SafeFinanceOperationAsync<T>(Func<Task<FinanceOperationResult<T>>> action)
    {
        try
        {
            return await action();
        }
        catch (DbUpdateConcurrencyException)
        {
            return FinanceOperationResult<T>.Failure("concurrency_conflict");
        }
        catch (Exception exception) when (IsExpectedFinanceContention(exception))
        {
            return FinanceOperationResult<T>.Failure("concurrency_conflict");
        }
    }

    private static bool IsExpectedFinanceContention(Exception exception)
    {
        if (exception is DbUpdateConcurrencyException) return true;
        if (exception is DbUpdateException dbUpdate
            && dbUpdate.InnerException is SqlException sql
            && sql.Errors.Cast<SqlError>().Any(error => error.Number is 1205 or 2601 or 2627 or 3960)) return true;
        return exception.InnerException is not null && IsExpectedFinanceContention(exception.InnerException);
    }

    private sealed class SqlFinanceSourceApprovalPolicy(FinanceApprovalRequirement requirement) : IFinanceSourceApprovalPolicy
    {
        public FinanceApprovalRequirement Resolve(string sourceContract, string sourceEvent) =>
            string.Equals(sourceContract, "inventory-valuation-finance.v1", StringComparison.OrdinalIgnoreCase)
                ? requirement
                : FinanceApprovalRequirement.NotConfigured;
    }

    private sealed class SqlFinanceExchangeRatePersistence : IMasterDataExchangeRatePersistence
    {
        private readonly UnavailableMasterDataExchangeRatePersistence fallback = new();
        private readonly MasterDataExchangeRateRecord record;

        public SqlFinanceExchangeRatePersistence(TenantId tenantId, string sourceCurrency, string targetCurrency, decimal rate)
        {
            RateId = Guid.NewGuid();
            RateVersionId = Guid.NewGuid();
            record = new MasterDataExchangeRateRecord(RateId, tenantId, Guid.NewGuid(), Guid.NewGuid(), sourceCurrency, targetCurrency, MasterDataLifecycleState.Active, 1, [new MasterDataExchangeRateVersionRecord(RateVersionId, 1, new DateOnly(2026, 1, 1), null, rate, 4, ExchangeRateProvenance.Configured, "SQL test", sourceCurrency, targetCurrency)], [1]);
        }

        public Guid RateId { get; }
        public Guid RateVersionId { get; }
        public Task<IReadOnlyList<MasterDataExchangeRateRecord>> ListExchangeRatesAsync(TenantContext tenantContext, CancellationToken cancellationToken = default) => fallback.ListExchangeRatesAsync(tenantContext, cancellationToken);
        public Task<MasterDataExchangeRateRecord?> FindExchangeRateAsync(TenantContext tenantContext, Guid exchangeRateId, CancellationToken cancellationToken = default) => Task.FromResult(record.Id == exchangeRateId ? record : null);
        public Task<MasterDataPersistenceResult<MasterDataExchangeRateRecord>> CreateExchangeRateAsync(TenantContext tenantContext, Guid exchangeRateId, CreateMasterDataExchangeRateCommand command, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => fallback.CreateExchangeRateAsync(tenantContext, exchangeRateId, command, evidence, cancellationToken);
        public Task<MasterDataPersistenceResult<MasterDataExchangeRateRecord>> EditExchangeRateAsync(TenantContext tenantContext, EditMasterDataExchangeRateCommand command, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => fallback.EditExchangeRateAsync(tenantContext, command, evidence, cancellationToken);
        public Task<MasterDataPersistenceResult<MasterDataExchangeRateRecord>> SetExchangeRateLifecycleAsync(TenantContext tenantContext, Guid exchangeRateId, MasterDataLifecycleState lifecycleState, byte[] expectedVersion, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => fallback.SetExchangeRateLifecycleAsync(tenantContext, exchangeRateId, lifecycleState, expectedVersion, evidence, cancellationToken);
        public Task<MasterDataPersistenceResult<MasterDataAuditRecord>> AppendAuditAsync(TenantContext tenantContext, MasterDataAuditEvidence evidence, CancellationToken cancellationToken = default) => fallback.AppendAuditAsync(tenantContext, evidence, cancellationToken);
        public Task<IReadOnlyList<MasterDataAuditRecord>> ReadAuditHistoryAsync(TenantContext tenantContext, Guid? exchangeRateId = null, CancellationToken cancellationToken = default) => fallback.ReadAuditHistoryAsync(tenantContext, exchangeRateId, cancellationToken);
    }

    private static InventoryValuationPolicyRequest SqlPolicy(
        Guid companyId,
        DateOnly effectiveFrom,
        DateOnly? effectiveTo = null,
        InventoryValuationScopeMode scopeMode = InventoryValuationScopeMode.WarehouseProductUom,
        int unitCostScale = 8,
        int amountScale = 8,
        string currency = "SAR") =>
        new(companyId, Guid.Parse("99999999-9999-9999-9999-999999999999"), currency, scopeMode, effectiveFrom, effectiveTo, unitCostScale, amountScale, InventoryValuationRoundingMode.ToEven, "PurchaseOrderUnitPrice", "CurrentMovingAverage", "CurrentMovingAverage");

    private static InventoryStockMovementEntity SqlMovement(
        TenantId tenantId,
        Guid companyId,
        Guid warehouseId,
        Guid productId,
        Guid unitId,
        InventoryMovementDirection direction,
        decimal quantity,
        decimal? unitCost,
        string? currency,
        string sourceReference,
        DateOnly? effectiveDate = null,
        InventoryMovementSourceType sourceType = InventoryMovementSourceType.OpeningBalance,
        string? trackingIdentity = null) =>
        new(tenantId, Guid.NewGuid(), companyId, null, warehouseId, "WH-SQL-MWA", "SQL MWA warehouse", productId, "SKU-SQL-MWA", "SQL MWA product", unitId, "EA", direction, quantity, unitCost, currency, InventoryValuationStatus.Pending, trackingIdentity, sourceType, Guid.NewGuid(), Guid.NewGuid(), null, effectiveDate ?? new DateOnly(2026, 1, 1), Guid.Parse("44444444-4444-4444-4444-444444444444"), sourceReference, DateTimeOffset.UtcNow);

    private static async Task ExecuteProbeInsertAsync(
        SqlConnection connection,
        SqlTransaction? transaction,
        Guid workId,
        Guid tenantId,
        Guid eventId,
        string kind)
    {
        await using var command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
            INSERT INTO [test].[DurableWorkProbe]
                ([WorkId], [TenantId], [EventId], [State], [Kind], [Payload])
            VALUES (@workId, @tenantId, @eventId, N'Pending', @kind, N'opaque-test-payload');
            """;
        command.Parameters.AddWithValue("@workId", workId);
        command.Parameters.AddWithValue("@tenantId", tenantId);
        command.Parameters.AddWithValue("@eventId", eventId);
        command.Parameters.AddWithValue("@kind", kind);
        await command.ExecuteNonQueryAsync();
    }

    private static TenantOwnedRecord AttachForgedRecord(
        TenantPersistenceSession session,
        Guid foreignRecordId,
        TenantId trustedTenantId)
    {
        var forged = (TenantOwnedRecord)Activator.CreateInstance(
            typeof(TenantOwnedRecord),
            BindingFlags.Instance | BindingFlags.NonPublic,
            binder: null,
            args: [],
            culture: null)!;
        var entry = session.DbContext.Entry(forged);
        entry.Property(nameof(TenantOwnedRecord.Id)).CurrentValue = foreignRecordId;
        entry.Property(nameof(TenantOwnedRecord.TenantId)).CurrentValue = trustedTenantId;
        entry.Property(nameof(TenantOwnedRecord.BusinessKey)).CurrentValue = "forged-payload";
        entry.Property(nameof(TenantOwnedRecord.RelationshipKind)).CurrentValue = TenantRelationshipKind.None;
        entry.Property(nameof(TenantOwnedRecord.RelatedTenantId)).CurrentValue = null;
        return forged;
    }
}
