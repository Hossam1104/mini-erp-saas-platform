using System.Data;
using System.Reflection;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using MiniErp.App.BuildingBlocks.Tenancy;
using MiniErp.Infrastructure.Persistence;
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
                "MESP_SQLSERVER_CONNECTION_STRING is required for the SQL Server safety harness.");
        }

        var builder = new SqlConnectionStringBuilder(rawConnectionString);
        if (!string.Equals(builder.DataSource, RequiredDataSource, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "The safety harness accepts only the machine-supported SQL Server LocalDB instance.");
        }

        if (!System.Text.RegularExpressions.Regex.IsMatch(
                builder.InitialCatalog,
                DisposableDatabasePattern,
                System.Text.RegularExpressions.RegexOptions.CultureInvariant))
        {
            throw new InvalidOperationException(
                "The SQL Server safety database must use the disposable MiniErpFoundation_ prefix.");
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
    private DbContextOptions _options = null!;

    public TenantContext TenantA { get; private set; } = null!;

    public TenantContext TenantB { get; private set; } = null!;

    public TenantPersistenceSessionFactory Factory { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        var rawConnectionString = Environment.GetEnvironmentVariable(
            "MESP_SQLSERVER_CONNECTION_STRING");

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

        _options = new DbContextOptionsBuilder()
            .UseSqlServer(_connectionString, sql => sql.EnableRetryOnFailure(3))
            .Options;

        await using (var context = CreateDbContext(TenantA))
        {
            await context.Database.EnsureCreatedAsync();
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

        var builder = new SqlConnectionStringBuilder(_connectionString);
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
        var rawConnectionString = Environment.GetEnvironmentVariable("MESP_SQLSERVER_CONNECTION_STRING");
        var builder = SqlServerSafetyConfigurationValidator.ValidateSafeConnectionString(rawConnectionString);

        Assert.Equal(@"(localdb)\MSSQLLocalDB", builder.DataSource);
        Assert.StartsWith("MiniErpFoundation_", builder.InitialCatalog, StringComparison.Ordinal);
    }

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
