using System.Data;
using System.Globalization;
using System.Security.Cryptography;
using Microsoft.Data.SqlClient;
using Microsoft.Data.Sqlite;

namespace MiniErp.DevelopmentDataCutover;

internal static class DevelopmentCutover
{
    private const string SqlConnectionEnvironmentVariable = "MESP_SQLSERVER_CONNECTION_STRING";
    private const string DefaultSourceDirectoryName = "MiniErp\\Development";
    private const string SharedTableName = "TenantOwnedRecords";
    private const string SharedSchemaName = "tenancy";

    public static async Task<int> RunAsync(string[] args)
    {
        try
        {
            var options = CutoverOptions.Parse(args);
            if (options.ShowHelp)
            {
                PrintUsage();
                return 0;
            }

            var connectionString = RequireSqlConnectionString();
            var sources = ResolveSources(options.SourceDirectory);

            if (options.Apply)
            {
                await ApplyAsync(connectionString, sources, options.BackupDirectory);
            }
            else if (options.Verify)
            {
                await VerifyAsync(connectionString, sources);
            }
            else
            {
                await InventoryAsync(connectionString, sources);
            }

            return 0;
        }
        catch (Exception exception)
        {
            Console.Error.WriteLine(
                $"Development data cutover failed: {exception.GetType().Name}: {SanitizeMessage(exception.Message)}");
            return 1;
        }
    }

    private static async Task InventoryAsync(
        string connectionString,
        IReadOnlyList<SourceDatabase> sources)
    {
        Console.WriteLine("Mode: inventory (no source or target writes)");
        PrintSourcePathsAndHashes(sources);

        foreach (var source in sources)
        {
            using var connection = OpenSource(source.Path);
            var tables = ReadSourceTables(connection);
            Console.WriteLine($"SQLite {source.Module}: {source.Path}");
            foreach (var table in tables.OrderBy(item => item, StringComparer.OrdinalIgnoreCase))
            {
                Console.WriteLine($"  {table}: {ReadSourceCount(connection, table)}");
            }
        }

        await using var target = await OpenTargetAsync(connectionString);
        var targetTables = await ReadTargetTablesAsync(target);
        Console.WriteLine($"SQL Server target: {target.Database} (provider: {target.GetType().Name})");
        foreach (var table in targetTables.OrderBy(item => item.FullName, StringComparer.OrdinalIgnoreCase))
        {
            Console.WriteLine($"  {table.FullName}: {table.RowCount}");
        }
    }

    private static async Task VerifyAsync(
        string connectionString,
        IReadOnlyList<SourceDatabase> sources)
    {
        EnsureSourceFilesExist(sources);
        var beforeHashes = sources.ToDictionary(
            source => source.Module,
            source => ComputeSha256(source.Path),
            StringComparer.OrdinalIgnoreCase);

        Console.WriteLine("Mode: verify (no source or target writes)");
        PrintSourcePathsAndHashes(sources, beforeHashes);

        await using var target = await OpenTargetAsync(connectionString);
        if (!string.Equals(target.Database, "MESP", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "The SQL Server cutover verifier refuses to inspect a database other than MESP.");
        }

        var targetTables = await ReadTargetTablesAsync(target);
        if (targetTables.Count == 0)
        {
            throw new InvalidOperationException("The MESP database has no migrated application tables.");
        }

        var targetByFullName = targetTables.ToDictionary(
            table => table.FullName,
            StringComparer.OrdinalIgnoreCase);
        foreach (var table in targetTables)
        {
            table.Columns = await ReadTargetColumnsAsync(target, table);
            table.PrimaryKeyColumns = await ReadPrimaryKeyColumnsAsync(target, table);
            if (table.PrimaryKeyColumns.Count == 0)
            {
                throw new InvalidOperationException($"Target table {table.FullName} has no primary key.");
            }
        }

        var mappings = DiscoverMappings(sources, targetByFullName);
        VerifyKeyParity(sources, mappings, targetTables);
        VerifyTenantParity(sources, mappings, targetTables);
        await VerifyForeignKeyConstraintsAsync(target, transaction: null);

        foreach (var source in sources)
        {
            var afterHash = ComputeSha256(source.Path);
            if (!string.Equals(beforeHashes[source.Module], afterHash, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"SQLite source changed during verification: {source.Path}.");
            }

            Console.WriteLine($"SQLite unchanged: {source.Module} (SHA-256 {afterHash})");
        }

        Console.WriteLine("Data parity: PASS");
        Console.WriteLine("IDs preserved: PASS");
        Console.WriteLine("Tenant IDs preserved: PASS");
        Console.WriteLine("Foreign-key lineage: PASS");
        Console.WriteLine("SQLite originals deleted: NO");
    }

    private static async Task ApplyAsync(
        string connectionString,
        IReadOnlyList<SourceDatabase> sources,
        string? requestedBackupDirectory)
    {
        EnsureSourceFilesExist(sources);
        var beforeHashes = sources.ToDictionary(
            source => source.Module,
            source => ComputeSha256(source.Path),
            StringComparer.OrdinalIgnoreCase);

        var backupDirectory = CreateBackups(sources, requestedBackupDirectory);
        Console.WriteLine("Mode: apply (SQLite sources opened read-only; one SQL transaction)");
        Console.WriteLine($"Backup directory: {backupDirectory}");
        PrintSourcePathsAndHashes(sources, beforeHashes);

        await using var target = await OpenTargetAsync(connectionString);
        if (!string.Equals(target.Database, "MESP", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                "The SQL Server cutover refuses to write to a database other than MESP.");
        }

        var targetTables = await ReadTargetTablesAsync(target);
        if (targetTables.Count == 0)
        {
            throw new InvalidOperationException(
                "The MESP database has no migrated application tables. Apply the committed EF migrations first.");
        }

        var targetByFullName = targetTables.ToDictionary(
            table => table.FullName,
            StringComparer.OrdinalIgnoreCase);
        foreach (var table in targetTables)
        {
            if (table.RowCount != 0)
            {
                throw new InvalidOperationException(
                    $"The intended MESP database is not empty ({table.FullName} contains {table.RowCount} row(s)); refusing to overwrite existing data.");
            }

            table.Columns = await ReadTargetColumnsAsync(target, table);
            table.PrimaryKeyColumns = await ReadPrimaryKeyColumnsAsync(target, table);
            if (table.PrimaryKeyColumns.Count == 0)
            {
                throw new InvalidOperationException($"Target table {table.FullName} has no primary key.");
            }
        }

        var mappings = DiscoverMappings(sources, targetByFullName);
        var dependencies = await ReadDependenciesAsync(target, targetByFullName);
        var insertionOrder = TopologicallyOrder(targetTables, dependencies);
        var expectedCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        await using var transaction = await target.BeginTransactionAsync(IsolationLevel.Serializable);
        foreach (var targetTable in insertionOrder)
        {
            var tableMappings = mappings
                .Where(mapping => string.Equals(mapping.Target.FullName, targetTable.FullName, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (tableMappings.Length == 0)
            {
                expectedCounts[targetTable.FullName] = 0;
                Console.WriteLine(
                    $"Retained empty target-only table {targetTable.FullName}: no SQLite source slice.");
                continue;
            }

            var deduplicatedSharedRows = string.Equals(
                targetTable.FullName,
                $"[{SharedSchemaName}].[{SharedTableName}]",
                StringComparison.OrdinalIgnoreCase)
                ? new Dictionary<string, string>(StringComparer.Ordinal)
                : null;
            var inserted = 0;

            foreach (var mapping in tableMappings)
            {
                inserted += ImportMapping(
                    mapping,
                    targetTable,
                    target,
                    (SqlTransaction)transaction,
                    deduplicatedSharedRows);
            }

            expectedCounts[targetTable.FullName] = inserted;
            Console.WriteLine($"Imported {targetTable.FullName}: {inserted} row(s)");
        }

        await VerifyForeignKeyConstraintsAsync(target, (SqlTransaction)transaction);
        await transaction.CommitAsync();

        var targetAfter = await ReadTargetTablesAsync(target);
        VerifyCounts(expectedCounts, targetAfter);
        VerifyKeyParity(sources, mappings, targetAfter);
        VerifyTenantParity(sources, mappings, targetAfter);

        foreach (var source in sources)
        {
            var afterHash = ComputeSha256(source.Path);
            if (!string.Equals(beforeHashes[source.Module], afterHash, StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"SQLite source changed during import: {source.Path}.");
            }

            Console.WriteLine($"SQLite unchanged: {source.Module} (SHA-256 {afterHash})");
        }

        Console.WriteLine("Data parity: PASS");
        Console.WriteLine("IDs preserved: PASS");
        Console.WriteLine("Tenant IDs preserved: PASS");
        Console.WriteLine("Foreign-key lineage: PASS");
        Console.WriteLine("SQLite originals deleted: NO");
    }

    private static int ImportMapping(
        TableMapping mapping,
        TargetTable targetTable,
        SqlConnection target,
        SqlTransaction transaction,
        Dictionary<string, string>? deduplicatedSharedRows)
    {
        using var source = OpenSource(mapping.Source.Path);
        var sourceColumns = ReadSourceColumns(source, mapping.SourceTableName);
        var sourceColumnNames = sourceColumns.ToDictionary(item => item, StringComparer.OrdinalIgnoreCase);
        var insertColumns = targetTable.Columns.Where(column => !column.IsGenerated).ToArray();

        foreach (var column in insertColumns)
        {
            if (!sourceColumnNames.ContainsKey(column.Name))
            {
                throw new InvalidOperationException(
                    $"SQLite table {mapping.Source.Module}.{mapping.SourceTableName} is missing target column {column.Name}.");
            }
        }

        foreach (var sourceColumn in sourceColumns)
        {
            if (!targetTable.Columns.Any(column => string.Equals(column.Name, sourceColumn, StringComparison.OrdinalIgnoreCase))
                && !string.Equals(sourceColumn, "Version", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"SQLite table {mapping.Source.Module}.{mapping.SourceTableName} contains unexpected column {sourceColumn}.");
            }
        }

        var selectList = string.Join(", ", insertColumns.Select(column => QuoteSqliteIdentifier(column.Name)));
        using var sourceCommand = source.CreateCommand();
        sourceCommand.CommandText = $"SELECT {selectList} FROM {QuoteSqliteIdentifier(mapping.SourceTableName)};";
        using var reader = sourceCommand.ExecuteReader();
        using var insertCommand = target.CreateCommand();
        insertCommand.Transaction = transaction;
        var targetColumnList = string.Join(", ", insertColumns.Select(column => QuoteSqlServerIdentifier(column.Name)));
        var parameterList = string.Join(", ", insertColumns.Select((_, index) => $"@p{index}"));
        insertCommand.CommandText =
            $"INSERT INTO {targetTable.FullName} ({targetColumnList}) VALUES ({parameterList});";

        var parameters = insertColumns
            .Select((column, index) => CreateParameter(insertCommand, column, index))
            .ToArray();
        var ordinals = insertColumns
            .Select(column => reader.GetOrdinal(column.Name))
            .ToArray();
        var keyColumns = targetTable.PrimaryKeyColumns
            .Select(key => Array.FindIndex(
                insertColumns,
                column => string.Equals(column.Name, key, StringComparison.OrdinalIgnoreCase)))
            .ToArray();

        var inserted = 0;
        while (reader.Read())
        {
            var values = new object?[insertColumns.Length];
            for (var index = 0; index < insertColumns.Length; index++)
            {
                values[index] = ConvertValue(reader.GetValue(ordinals[index]), insertColumns[index]);
                parameters[index].Value = values[index] ?? DBNull.Value;
            }

            if (deduplicatedSharedRows is not null)
            {
                var key = Fingerprint(keyColumns.Select(index => values[index]));
                var row = Fingerprint(values);
                if (deduplicatedSharedRows.TryGetValue(key, out var priorRow))
                {
                    if (!string.Equals(priorRow, row, StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException(
                            "Conflicting SQLite rows share a TenantOwnedRecords primary key; refusing to choose a winner.");
                    }

                    continue;
                }

                deduplicatedSharedRows.Add(key, row);
            }

            insertCommand.ExecuteNonQuery();
            inserted++;
        }

        return inserted;
    }

    private static async Task VerifyForeignKeyConstraintsAsync(
        SqlConnection target,
        SqlTransaction? transaction)
    {
        await using var command = target.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = "DBCC CHECKCONSTRAINTS WITH ALL_CONSTRAINTS;";
        await using var reader = await command.ExecuteReaderAsync();
        if (await reader.ReadAsync())
        {
            throw new InvalidOperationException(
                "SQL Server reported a foreign-key or check-constraint violation after import.");
        }
    }

    private static void VerifyCounts(
        IReadOnlyDictionary<string, int> expectedCounts,
        IReadOnlyList<TargetTable> actualTables)
    {
        foreach (var table in actualTables)
        {
            if (!expectedCounts.TryGetValue(table.FullName, out var expected)
                || table.RowCount != expected)
            {
                throw new InvalidOperationException(
                    $"Target row-count parity failed for {table.FullName}: expected {expected}, actual {table.RowCount}.");
            }
        }
    }

    private static void VerifyKeyParity(
        IReadOnlyList<SourceDatabase> sources,
        IReadOnlyList<TableMapping> mappings,
        IReadOnlyList<TargetTable> targetTables)
    {
        foreach (var target in targetTables)
        {
            var sourceKeys = new HashSet<string>(StringComparer.Ordinal);
            foreach (var mapping in mappings.Where(item => string.Equals(item.Target.FullName, target.FullName, StringComparison.OrdinalIgnoreCase)))
            {
                try
                {
                    using var source = OpenSource(mapping.Source.Path);
                    using var command = source.CreateCommand();
                    var keyList = string.Join(", ", target.PrimaryKeyColumns.Select(QuoteSqliteIdentifier));
                    command.CommandText =
                        $"SELECT {keyList} FROM {QuoteSqliteIdentifier(mapping.SourceTableName)};";
                    using var reader = command.ExecuteReader();
                    while (reader.Read())
                    {
                        sourceKeys.Add(Fingerprint(
                            target.PrimaryKeyColumns.Select((column, index) =>
                                NormalizeForComparison(reader.GetValue(index), target.FindColumn(column)))));
                    }
                }
                catch (SqliteException exception)
                {
                    throw new InvalidOperationException(
                        $"SQLite primary-key parity query failed for {mapping.Source.Module}.{mapping.SourceTableName} -> {target.FullName} (target key columns: {string.Join(", ", target.PrimaryKeyColumns)}).",
                        exception);
                }
            }

            using var targetConnection = OpenTargetForVerification(targetTables);
            using var targetCommand = targetConnection.CreateCommand();
            var targetKeyList = string.Join(", ", target.PrimaryKeyColumns.Select(QuoteSqlServerIdentifier));
            targetCommand.CommandText = $"SELECT {targetKeyList} FROM {target.FullName};";
            using var targetReader = targetCommand.ExecuteReader();
            var targetKeys = new HashSet<string>(StringComparer.Ordinal);
            while (targetReader.Read())
            {
                targetKeys.Add(Fingerprint(
                    target.PrimaryKeyColumns.Select((column, index) =>
                        NormalizeForComparison(targetReader.GetValue(index), target.FindColumn(column)))));
            }

            if (!sourceKeys.SetEquals(targetKeys))
            {
                throw new InvalidOperationException(
                    $"Primary-key parity failed for {target.FullName}.");
            }
        }
    }

    private static void VerifyTenantParity(
        IReadOnlyList<SourceDatabase> sources,
        IReadOnlyList<TableMapping> mappings,
        IReadOnlyList<TargetTable> targetTables)
    {
        foreach (var target in targetTables.Where(table => table.HasColumn("TenantId")))
        {
            var sourceTenants = new HashSet<string>(StringComparer.Ordinal);
            foreach (var mapping in mappings.Where(item => string.Equals(item.Target.FullName, target.FullName, StringComparison.OrdinalIgnoreCase)))
            {
                using var source = OpenSource(mapping.Source.Path);
                using var command = source.CreateCommand();
                command.CommandText =
                    $"SELECT {QuoteSqliteIdentifier("TenantId")} FROM {QuoteSqliteIdentifier(mapping.SourceTableName)};";
                using var reader = command.ExecuteReader();
                while (reader.Read())
                {
                    sourceTenants.Add(Fingerprint([
                        NormalizeForComparison(reader.GetValue(0), target.FindColumn("TenantId"))]));
                }
            }

            using var targetConnection = OpenTargetForVerification(targetTables);
            using var targetCommand = targetConnection.CreateCommand();
            targetCommand.CommandText =
                $"SELECT {QuoteSqlServerIdentifier("TenantId")} FROM {target.FullName};";
            using var targetReader = targetCommand.ExecuteReader();
            var targetTenants = new HashSet<string>(StringComparer.Ordinal);
            while (targetReader.Read())
            {
                targetTenants.Add(Fingerprint([
                    NormalizeForComparison(targetReader.GetValue(0), target.FindColumn("TenantId"))]));
            }

            if (!sourceTenants.SetEquals(targetTenants))
            {
                throw new InvalidOperationException(
                    $"TenantId parity failed for {target.FullName}.");
            }
        }
    }

    private static SqlConnection OpenTargetForVerification(IReadOnlyList<TargetTable> targetTables)
    {
        var connectionString = Environment.GetEnvironmentVariable(SqlConnectionEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"{SqlConnectionEnvironmentVariable} is required for verification.");
        }

        var connection = new SqlConnection(connectionString);
        connection.Open();
        if (!string.Equals(connection.Database, "MESP", StringComparison.OrdinalIgnoreCase)
            || targetTables.Count == 0)
        {
            connection.Dispose();
            throw new InvalidOperationException("Verification target is not the intended MESP database.");
        }

        return connection;
    }

    private static IReadOnlyList<TableMapping> DiscoverMappings(
        IReadOnlyList<SourceDatabase> sources,
        IReadOnlyDictionary<string, TargetTable> targetByFullName)
    {
        var mappings = new List<TableMapping>();
        var mappedTargets = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var source in sources)
        {
            using var connection = OpenSource(source.Path);
            foreach (var sourceTable in ReadSourceTables(connection))
            {
                if (string.Equals(sourceTable, SharedTableName, StringComparison.OrdinalIgnoreCase))
                {
                    var sharedKey = $"[{SharedSchemaName}].[{SharedTableName}]";
                    if (!targetByFullName.TryGetValue(sharedKey, out var sharedTarget))
                    {
                        throw new InvalidOperationException("The tenancy target table is missing from MESP.");
                    }

                    mappings.Add(new TableMapping(source, sourceTable, sharedTarget));
                    mappedTargets.Add(sharedKey);
                    continue;
                }

                if (sourceTable.StartsWith("__EFMigrationsHistory", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                var targetKey = $"[{source.SchemaName}].[{sourceTable}]";
                if (!targetByFullName.TryGetValue(targetKey, out var target))
                {
                    throw new InvalidOperationException(
                        $"SQLite table {source.Module}.{sourceTable} has no owning SQL Server target table.");
                }

                mappings.Add(new TableMapping(source, sourceTable, target));
                mappedTargets.Add(targetKey);
            }
        }

        foreach (var target in targetByFullName.Values)
        {
            if (!mappedTargets.Contains(target.FullName))
            {
                if (target.RowCount != 0)
                {
                    throw new InvalidOperationException(
                        $"SQL Server target table {target.FullName} has no SQLite source mapping and contains {target.RowCount} row(s).");
                }

                Console.WriteLine(
                    $"Unmapped empty SQL Server table retained: {target.FullName} (no SQLite source slice).");
            }
        }

        return mappings;
    }

    private static IReadOnlyList<TargetTable> TopologicallyOrder(
        IReadOnlyList<TargetTable> tables,
        IReadOnlyDictionary<string, IReadOnlySet<string>> dependencies)
    {
        var remaining = new HashSet<string>(
            tables.Select(table => table.FullName),
            StringComparer.OrdinalIgnoreCase);
        var ordered = new List<TargetTable>(tables.Count);

        while (remaining.Count > 0)
        {
            var next = tables
                .Where(table => remaining.Contains(table.FullName))
                .Where(table => !dependencies.TryGetValue(table.FullName, out var parents)
                    || parents.All(parent => !remaining.Contains(parent)))
                .OrderBy(table => table.FullName, StringComparer.OrdinalIgnoreCase)
                .FirstOrDefault();
            if (next is null)
            {
                throw new InvalidOperationException(
                    "The SQL Server migration model contains a foreign-key cycle that the cutover importer cannot order safely.");
            }

            ordered.Add(next);
            remaining.Remove(next.FullName);
        }

        return ordered;
    }

    private static async Task<Dictionary<string, IReadOnlySet<string>>> ReadDependenciesAsync(
        SqlConnection connection,
        IReadOnlyDictionary<string, TargetTable> targetByFullName)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT parent_schema.name, parent_table.name, referenced_schema.name, referenced_table.name
            FROM sys.foreign_keys AS foreign_key
            INNER JOIN sys.tables AS parent_table ON parent_table.object_id = foreign_key.parent_object_id
            INNER JOIN sys.schemas AS parent_schema ON parent_schema.schema_id = parent_table.schema_id
            INNER JOIN sys.tables AS referenced_table ON referenced_table.object_id = foreign_key.referenced_object_id
            INNER JOIN sys.schemas AS referenced_schema ON referenced_schema.schema_id = referenced_table.schema_id
            WHERE parent_table.is_ms_shipped = 0 AND referenced_table.is_ms_shipped = 0;
            """;
        await using var reader = await command.ExecuteReaderAsync();
        var dependencies = new Dictionary<string, HashSet<string>>(StringComparer.OrdinalIgnoreCase);
        while (await reader.ReadAsync())
        {
            var child = $"[{reader.GetString(0)}].[{reader.GetString(1)}]";
            var parent = $"[{reader.GetString(2)}].[{reader.GetString(3)}]";
            if (string.Equals(child, parent, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!targetByFullName.ContainsKey(child) || !targetByFullName.ContainsKey(parent))
            {
                continue;
            }

            if (!dependencies.TryGetValue(child, out var set))
            {
                set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                dependencies.Add(child, set);
            }

            set.Add(parent);
        }

        return dependencies.ToDictionary(
            pair => pair.Key,
            pair => (IReadOnlySet<string>)pair.Value,
            StringComparer.OrdinalIgnoreCase);
    }

    private static async Task<List<TargetTable>> ReadTargetTablesAsync(SqlConnection connection)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT schema_name.name, table_name.name, SUM(partition_info.rows)
            FROM sys.tables AS table_name
            INNER JOIN sys.schemas AS schema_name ON schema_name.schema_id = table_name.schema_id
            INNER JOIN sys.partitions AS partition_info ON partition_info.object_id = table_name.object_id
                AND partition_info.index_id IN (0, 1)
            WHERE table_name.is_ms_shipped = 0
                AND table_name.name NOT LIKE '%EFMigrationsHistory%'
            GROUP BY schema_name.name, table_name.name;
            """;
        await using var reader = await command.ExecuteReaderAsync();
        var tables = new List<TargetTable>();
        while (await reader.ReadAsync())
        {
            tables.Add(new TargetTable(
                reader.GetString(0),
                reader.GetString(1),
                Convert.ToInt64(reader.GetValue(2), CultureInfo.InvariantCulture)));
        }

        return tables;
    }

    private static async Task<List<TargetColumn>> ReadTargetColumnsAsync(
        SqlConnection connection,
        TargetTable table)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT column_info.name,
                   type_info.name,
                   column_info.max_length,
                   column_info.precision,
                   column_info.scale,
                   column_info.is_nullable,
                   column_info.is_identity,
                   column_info.is_computed,
                   column_info.system_type_id
            FROM sys.columns AS column_info
            INNER JOIN sys.types AS type_info ON type_info.user_type_id = column_info.user_type_id
            WHERE column_info.object_id = OBJECT_ID(@tableName)
            ORDER BY column_info.column_id;
            """;
        command.Parameters.Add(new SqlParameter("@tableName", SqlDbType.NVarChar, 517)
        {
            Value = $"{table.SchemaName}.{table.TableName}"
        });
        await using var reader = await command.ExecuteReaderAsync();
        var columns = new List<TargetColumn>();
        while (await reader.ReadAsync())
        {
            var typeName = reader.GetString(1);
            var systemTypeId = Convert.ToInt32(reader.GetValue(8), CultureInfo.InvariantCulture);
            columns.Add(new TargetColumn(
                reader.GetString(0),
                typeName,
                Convert.ToInt16(reader.GetValue(2), CultureInfo.InvariantCulture),
                Convert.ToByte(reader.GetValue(3), CultureInfo.InvariantCulture),
                Convert.ToByte(reader.GetValue(4), CultureInfo.InvariantCulture),
                reader.GetBoolean(5),
                reader.GetBoolean(6),
                reader.GetBoolean(7),
                systemTypeId == 189 || string.Equals(typeName, "timestamp", StringComparison.OrdinalIgnoreCase)));
        }

        return columns;
    }

    private static async Task<List<string>> ReadPrimaryKeyColumnsAsync(
        SqlConnection connection,
        TargetTable table)
    {
        await using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT column_info.name
            FROM sys.indexes AS index_info
            INNER JOIN sys.index_columns AS index_column
                ON index_column.object_id = index_info.object_id
                AND index_column.index_id = index_info.index_id
            INNER JOIN sys.columns AS column_info
                ON column_info.object_id = index_column.object_id
                AND column_info.column_id = index_column.column_id
            WHERE index_info.object_id = OBJECT_ID(@tableName)
                AND index_info.is_primary_key = 1
            ORDER BY index_column.key_ordinal;
            """;
        command.Parameters.Add(new SqlParameter("@tableName", SqlDbType.NVarChar, 517)
        {
            Value = $"{table.SchemaName}.{table.TableName}"
        });
        await using var reader = await command.ExecuteReaderAsync();
        var columns = new List<string>();
        while (await reader.ReadAsync())
        {
            columns.Add(reader.GetString(0));
        }

        return columns;
    }

    private static List<string> ReadSourceTables(SqliteConnection connection)
    {
        using var command = connection.CreateCommand();
        command.CommandText = "SELECT name FROM sqlite_master WHERE type = 'table' AND name NOT LIKE 'sqlite_%' ORDER BY name;";
        using var reader = command.ExecuteReader();
        var tables = new List<string>();
        while (reader.Read())
        {
            tables.Add(reader.GetString(0));
        }

        return tables;
    }

    private static List<string> ReadSourceColumns(SqliteConnection connection, string tableName)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"PRAGMA table_info({QuoteSqliteIdentifier(tableName)});";
        using var reader = command.ExecuteReader();
        var columns = new List<string>();
        while (reader.Read())
        {
            columns.Add(reader.GetString(1));
        }

        return columns;
    }

    private static long ReadSourceCount(SqliteConnection connection, string tableName)
    {
        using var command = connection.CreateCommand();
        command.CommandText = $"SELECT COUNT(*) FROM {QuoteSqliteIdentifier(tableName)};";
        return Convert.ToInt64(command.ExecuteScalar(), CultureInfo.InvariantCulture);
    }

    private static SqlParameter CreateParameter(
        SqlCommand command,
        TargetColumn column,
        int index)
    {
        var parameter = command.Parameters.Add($"@p{index}", ToSqlDbType(column.TypeName));
        if (column.TypeName is "nvarchar" or "varchar" or "nchar" or "char" or "varbinary" or "binary")
        {
            parameter.Size = column.MaxLength < 0
                ? -1
                : column.MaxLength / (column.TypeName.StartsWith("n", StringComparison.OrdinalIgnoreCase) ? 2 : 1);
        }

        if (column.TypeName is "decimal" or "numeric" or "money" or "smallmoney")
        {
            parameter.Precision = column.Precision;
            parameter.Scale = column.Scale;
        }

        return parameter;
    }

    private static SqlDbType ToSqlDbType(string typeName) => typeName.ToLowerInvariant() switch
    {
        "bigint" => SqlDbType.BigInt,
        "binary" => SqlDbType.Binary,
        "bit" => SqlDbType.Bit,
        "char" => SqlDbType.Char,
        "date" => SqlDbType.Date,
        "datetime" => SqlDbType.DateTime,
        "datetime2" => SqlDbType.DateTime2,
        "datetimeoffset" => SqlDbType.DateTimeOffset,
        "decimal" => SqlDbType.Decimal,
        "float" => SqlDbType.Float,
        "image" => SqlDbType.Image,
        "int" => SqlDbType.Int,
        "money" => SqlDbType.Money,
        "nchar" => SqlDbType.NChar,
        "ntext" => SqlDbType.NText,
        "nvarchar" => SqlDbType.NVarChar,
        "real" => SqlDbType.Real,
        "smallint" => SqlDbType.SmallInt,
        "smallmoney" => SqlDbType.SmallMoney,
        "text" => SqlDbType.Text,
        "time" => SqlDbType.Time,
        "tinyint" => SqlDbType.TinyInt,
        "uniqueidentifier" => SqlDbType.UniqueIdentifier,
        "varbinary" => SqlDbType.VarBinary,
        "varchar" => SqlDbType.VarChar,
        "xml" => SqlDbType.Xml,
        _ => throw new InvalidOperationException($"Unsupported SQL Server target type {typeName}.")
    };

    private static object? ConvertValue(object raw, TargetColumn column)
    {
        if (raw is DBNull)
        {
            return null;
        }

        var value = raw is string text ? text : raw;
        return column.TypeName.ToLowerInvariant() switch
        {
            "uniqueidentifier" => value is Guid guid
                ? guid
                : Guid.Parse(Convert.ToString(value, CultureInfo.InvariantCulture)!),
            "bit" => value is bool boolean
                ? boolean
                : Convert.ToInt64(value, CultureInfo.InvariantCulture) != 0,
            "tinyint" => Convert.ToByte(value, CultureInfo.InvariantCulture),
            "smallint" => Convert.ToInt16(value, CultureInfo.InvariantCulture),
            "int" => Convert.ToInt32(value, CultureInfo.InvariantCulture),
            "bigint" => Convert.ToInt64(value, CultureInfo.InvariantCulture),
            "decimal" or "numeric" or "money" or "smallmoney" =>
                Convert.ToDecimal(value, CultureInfo.InvariantCulture),
            "float" => Convert.ToDouble(value, CultureInfo.InvariantCulture),
            "real" => Convert.ToSingle(value, CultureInfo.InvariantCulture),
            "date" => ParseDate(value).Date,
            "datetime" or "datetime2" or "smalldatetime" => ParseDate(value),
            "datetimeoffset" => ParseDateOffset(value),
            "time" => value is TimeSpan time
                ? time
                : TimeSpan.Parse(Convert.ToString(value, CultureInfo.InvariantCulture)!, CultureInfo.InvariantCulture),
            "binary" or "varbinary" or "image" => value is byte[] bytes
                ? bytes
                : Convert.FromBase64String(Convert.ToString(value, CultureInfo.InvariantCulture)!),
            "nvarchar" or "varchar" or "nchar" or "char" or "ntext" or "text" or "xml" =>
                Convert.ToString(value, CultureInfo.InvariantCulture)!,
            _ => value
        };
    }

    private static object NormalizeForComparison(object raw, TargetColumn column)
    {
        var converted = ConvertValue(raw, column);
        if (converted is null)
        {
            return "<null>";
        }

        return column.TypeName.ToLowerInvariant() switch
        {
            "uniqueidentifier" => ((Guid)converted).ToString("D"),
            "date" => ((DateTime)converted).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            "datetime" or "datetime2" or "smalldatetime" =>
                ((DateTime)converted).ToString("O", CultureInfo.InvariantCulture),
            "datetimeoffset" => ((DateTimeOffset)converted).ToString("O", CultureInfo.InvariantCulture),
            "binary" or "varbinary" or "image" => Convert.ToBase64String((byte[])converted),
            _ => converted
        };
    }

    private static DateTime ParseDate(object value) => value switch
    {
        DateTime dateTime => dateTime,
        DateTimeOffset dateTimeOffset => dateTimeOffset.DateTime,
        _ => DateTime.Parse(
            Convert.ToString(value, CultureInfo.InvariantCulture)!,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind)
    };

    private static DateTimeOffset ParseDateOffset(object value) => value switch
    {
        DateTimeOffset dateTimeOffset => dateTimeOffset,
        DateTime dateTime => new DateTimeOffset(dateTime),
        _ => DateTimeOffset.Parse(
            Convert.ToString(value, CultureInfo.InvariantCulture)!,
            CultureInfo.InvariantCulture,
            DateTimeStyles.RoundtripKind)
    };

    private static string Fingerprint(IEnumerable<object?> values)
    {
        var payload = string.Join(((char)31).ToString(), values.Select(ValueFingerprint));
        return Convert.ToHexString(SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(payload)));
    }

    private static string ValueFingerprint(object? value) => value switch
    {
        null => "<null>",
        byte[] bytes => $"bytes:{Convert.ToBase64String(bytes)}",
        DateTime dateTime => $"datetime:{dateTime:O}",
        DateTimeOffset dateTimeOffset => $"datetimeoffset:{dateTimeOffset:O}",
        Guid guid => $"guid:{guid:D}",
        IFormattable formattable => formattable.ToString(null, CultureInfo.InvariantCulture) ?? string.Empty,
        _ => value.ToString() ?? string.Empty
    };

    private static SqliteConnection OpenSource(string path)
    {
        var builder = new SqliteConnectionStringBuilder
        {
            DataSource = path,
            Mode = SqliteOpenMode.ReadOnly,
            Cache = SqliteCacheMode.Private
        };
        var connection = new SqliteConnection(builder.ConnectionString);
        connection.Open();
        return connection;
    }

    private static async Task<SqlConnection> OpenTargetAsync(string connectionString)
    {
        var connection = new SqlConnection(connectionString);
        await connection.OpenAsync();
        return connection;
    }

    private static string RequireSqlConnectionString()
    {
        var connectionString = Environment.GetEnvironmentVariable(SqlConnectionEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"{SqlConnectionEnvironmentVariable} must be configured locally before SQL Server cutover.");
        }

        return connectionString;
    }

    private static IReadOnlyList<SourceDatabase> ResolveSources(string? requestedSourceDirectory)
    {
        var defaultDirectory = requestedSourceDirectory;
        if (string.IsNullOrWhiteSpace(defaultDirectory))
        {
            var configuredDirectory = GetEnvironmentValue("MESP_DEV_SQLITE_DIRECTORY");
            defaultDirectory = string.IsNullOrWhiteSpace(configuredDirectory)
                ? Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    DefaultSourceDirectoryName)
                : configuredDirectory;
        }

        return
        [
            new SourceDatabase(
                "Master Data",
                "masterdata",
                ResolveSourcePath(
                    "MESP_DEV_MASTERDATA_SQLITE_CONNECTION_STRING",
                    defaultDirectory,
                    "masterdata.db")),
            new SourceDatabase(
                "Business Parties",
                "businessparties",
                ResolveSourcePath(
                    "MESP_DEV_BUSINESS_PARTIES_SQLITE_CONNECTION_STRING",
                    defaultDirectory,
                    "business-parties.db")),
            new SourceDatabase(
                "Procurement",
                "procurement",
                ResolveSourcePath(
                    "MESP_DEV_PROCUREMENT_SQLITE_CONNECTION_STRING",
                    defaultDirectory,
                    "procurement.db"))
        ];
    }

    private static string ResolveSourcePath(
        string connectionEnvironmentVariable,
        string defaultDirectory,
        string defaultFileName)
    {
        var configured = GetEnvironmentValue(connectionEnvironmentVariable);
        if (string.IsNullOrWhiteSpace(configured))
        {
            return Path.GetFullPath(Path.Combine(defaultDirectory, defaultFileName));
        }

        var builder = new SqliteConnectionStringBuilder(configured);
        if (string.IsNullOrWhiteSpace(builder.DataSource)
            || string.Equals(builder.DataSource, ":memory:", StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException(
                $"{connectionEnvironmentVariable} must identify a file-backed SQLite database.");
        }

        return Path.GetFullPath(builder.DataSource);
    }

    private static string? GetEnvironmentValue(string key) =>
        Environment.GetEnvironmentVariable(key);

    private static void EnsureSourceFilesExist(IReadOnlyList<SourceDatabase> sources)
    {
        foreach (var source in sources)
        {
            if (!File.Exists(source.Path))
            {
                throw new FileNotFoundException(
                    $"SQLite source database was not found for {source.Module}: {source.Path}");
            }
        }
    }

    private static string CreateBackups(
        IReadOnlyList<SourceDatabase> sources,
        string? requestedBackupDirectory)
    {
        var backupDirectory = string.IsNullOrWhiteSpace(requestedBackupDirectory)
            ? Path.Combine(
                Path.GetDirectoryName(sources[0].Path)!,
                "backups",
                $"sqlserver-cutover-{DateTime.UtcNow:yyyyMMddTHHmmssZ}-{Guid.NewGuid():N}")
            : Path.GetFullPath(requestedBackupDirectory);
        Directory.CreateDirectory(backupDirectory);

        foreach (var source in sources)
        {
            File.Copy(source.Path, Path.Combine(backupDirectory, Path.GetFileName(source.Path)), overwrite: false);
        }

        return backupDirectory;
    }

    private static void PrintSourcePathsAndHashes(
        IReadOnlyList<SourceDatabase> sources,
        IReadOnlyDictionary<string, string>? hashes = null)
    {
        foreach (var source in sources)
        {
            var hash = hashes is null ? ComputeSha256(source.Path) : hashes[source.Module];
            Console.WriteLine($"SQLite source {source.Module}: {source.Path}");
            Console.WriteLine($"  SHA-256: {hash}");
        }
    }

    private static string ComputeSha256(string path)
    {
        using var stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static string QuoteSqliteIdentifier(string identifier) =>
        $"\"{identifier.Replace("\"", "\"\"", StringComparison.Ordinal)}\"";

    private static string QuoteSqlServerIdentifier(string identifier) =>
        $"[{identifier.Replace("]", "]]", StringComparison.Ordinal)}]";

    private static string SanitizeMessage(string message)
    {
        var result = message;
        var connectionString = Environment.GetEnvironmentVariable(SqlConnectionEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(connectionString))
        {
            result = result.Replace(connectionString, "[redacted]", StringComparison.Ordinal);
        }

        return result;
    }

    private static void PrintUsage()
    {
        Console.WriteLine("MiniErp.DevelopmentDataCutover");
        Console.WriteLine("  --inventory                         Read source/target counts only (default)");
        Console.WriteLine("  --verify                            Verify imported IDs, TenantIds, constraints, and source hashes");
        Console.WriteLine("  --apply                             Backup and migrate SQLite data into empty MESP");
        Console.WriteLine("  --source-directory <path>           Override the default Development SQLite directory");
        Console.WriteLine("  --backup-directory <path>           Use an explicit backup directory");
        Console.WriteLine("  --help                              Show this help");
        Console.WriteLine("Required environment: MESP_SQLSERVER_CONNECTION_STRING (value is never printed)");
    }

    private sealed class CutoverOptions
    {
        internal bool Apply { get; private init; }
        internal bool Verify { get; private init; }
        internal bool ShowHelp { get; private init; }
        internal string? SourceDirectory { get; private init; }
        internal string? BackupDirectory { get; private init; }

        internal static CutoverOptions Parse(IReadOnlyList<string> args)
        {
            var apply = false;
            var verify = false;
            var showHelp = false;
            string? sourceDirectory = null;
            string? backupDirectory = null;

            for (var index = 0; index < args.Count; index++)
            {
                switch (args[index])
                {
                    case "--apply":
                        apply = true;
                        break;
                    case "--inventory":
                        apply = false;
                        verify = false;
                        break;
                    case "--verify":
                        apply = false;
                        verify = true;
                        break;
                    case "--help":
                    case "-h":
                        showHelp = true;
                        break;
                    case "--source-directory":
                        sourceDirectory = ReadValue(args, ref index, "--source-directory");
                        break;
                    case "--backup-directory":
                        backupDirectory = ReadValue(args, ref index, "--backup-directory");
                        break;
                    default:
                        throw new ArgumentException($"Unknown argument '{args[index]}'. Use --help for usage.");
                }
            }

            return new CutoverOptions
            {
                Apply = apply,
                Verify = verify,
                ShowHelp = showHelp,
                SourceDirectory = sourceDirectory,
                BackupDirectory = backupDirectory
            };
        }

        private static string ReadValue(IReadOnlyList<string> args, ref int index, string option)
        {
            if (++index >= args.Count || string.IsNullOrWhiteSpace(args[index]))
            {
                throw new ArgumentException($"{option} requires a non-empty path.");
            }

            return args[index];
        }
    }

    private sealed record SourceDatabase(string Module, string SchemaName, string Path);

    private sealed record TableMapping(SourceDatabase Source, string SourceTableName, TargetTable Target);

    private sealed class TargetTable
    {
        internal TargetTable(string schemaName, string tableName, long rowCount)
        {
            SchemaName = schemaName;
            TableName = tableName;
            RowCount = checked((int)rowCount);
            Columns = [];
            PrimaryKeyColumns = [];
        }

        internal string SchemaName { get; }
        internal string TableName { get; }
        internal int RowCount { get; }
        internal List<TargetColumn> Columns { get; set; }
        internal List<string> PrimaryKeyColumns { get; set; }
        internal string FullName => $"[{SchemaName}].[{TableName}]";

        internal bool HasColumn(string name) =>
            Columns.Any(column => string.Equals(column.Name, name, StringComparison.OrdinalIgnoreCase));

        internal TargetColumn FindColumn(string name) =>
            Columns.Single(column => string.Equals(column.Name, name, StringComparison.OrdinalIgnoreCase));
    }

    private sealed record TargetColumn(
        string Name,
        string TypeName,
        short MaxLength,
        byte Precision,
        byte Scale,
        bool IsNullable,
        bool IsIdentity,
        bool IsComputed,
        bool IsRowVersion)
    {
        internal bool IsGenerated => IsIdentity || IsComputed || IsRowVersion;
    }
}
