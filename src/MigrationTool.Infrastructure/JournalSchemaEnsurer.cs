using System.Data;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using MigrationTool.Domain;
using Npgsql;

namespace MigrationTool.Infrastructure;

/// <summary>
/// Creates the DbUp journal schema before an upgrade.
/// SQL Server binds <c>[schema].[table]</c> when a batch is compiled, so DbUp's combined
/// <c>CREATE SCHEMA</c> + <c>CREATE TABLE</c> script fails with error 2760 when the schema
/// is missing. <c>CREATE SCHEMA</c> also cannot run inside the transaction DbUp opens.
/// </summary>
internal static class JournalSchemaEnsurer
{
    internal const string SqlServerEnsureSql = """
        DECLARE @quoted nvarchar(258) = QUOTENAME(@schema);
        IF @quoted IS NULL
        BEGIN
            THROW 50000, 'Journal schema name is not a valid SQL Server identifier.', 1;
        END
        IF NOT EXISTS (SELECT 1 FROM sys.schemas WHERE name = @schema)
        BEGIN
            EXEC(N'CREATE SCHEMA ' + @quoted);
            SELECT 1;
        END
        ELSE
            SELECT 0;
        """;

    internal const string SqlServerEnsureTableSql = """
        DECLARE @schemaQuoted nvarchar(258) = QUOTENAME(@schema);
        DECLARE @tableQuoted nvarchar(258) = QUOTENAME(@table);
        DECLARE @pk nvarchar(258) = QUOTENAME(N'PK_' + @table + N'_Id');
        IF @schemaQuoted IS NULL OR @tableQuoted IS NULL OR @pk IS NULL
        BEGIN
            THROW 50000, 'Journal schema or table name is not a valid SQL Server identifier.', 1;
        END
        IF OBJECT_ID(@schema + N'.' + @table, N'U') IS NULL
        BEGIN
            DECLARE @sql nvarchar(max) =
                N'CREATE TABLE ' + @schemaQuoted + N'.' + @tableQuoted + N' (
                    [Id] int IDENTITY(1,1) NOT NULL CONSTRAINT ' + @pk + N' PRIMARY KEY,
                    [ScriptName] nvarchar(255) NOT NULL,
                    [Applied] datetime NOT NULL
                )';
            EXEC(@sql);
            SELECT 1;
        END
        ELSE
            SELECT 0;
        """;

    internal const string SqlServerSchemaExistsSql =
        "SELECT CASE WHEN EXISTS (SELECT 1 FROM sys.schemas WHERE name = @schema) THEN 1 ELSE 0 END;";

    internal const string PostgreSqlExistsSql =
        "SELECT EXISTS (SELECT 1 FROM information_schema.schemata WHERE schema_name = @schema);";

    internal const string PostgreSqlCreateSql =
        "SELECT format('CREATE SCHEMA IF NOT EXISTS %I', @schema);";

    public static bool Exists(
        DatabaseProviderKind provider,
        string connectionString,
        string? schema,
        int commandTimeoutSeconds)
    {
        if (string.IsNullOrWhiteSpace(schema))
        {
            return true;
        }

        var schemaName = RequireIdentifier(schema, "JournalSchema");
        var timeout = commandTimeoutSeconds < 0 ? 0 : commandTimeoutSeconds;
        if (provider == DatabaseProviderKind.PostgreSql)
        {
            using var connection = new NpgsqlConnection(connectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandTimeout = timeout;
            command.CommandText = PostgreSqlExistsSql;
            command.Parameters.Add(new NpgsqlParameter("schema", schemaName));
            return Convert.ToBoolean(command.ExecuteScalar());
        }

        using var sqlConnection = new SqlConnection(connectionString);
        sqlConnection.Open();
        using var sqlCommand = sqlConnection.CreateCommand();
        sqlCommand.CommandTimeout = timeout;
        sqlCommand.CommandText = SqlServerSchemaExistsSql;
        sqlCommand.Parameters.Add(new SqlParameter("@schema", SqlDbType.NVarChar, 128) { Value = schemaName });
        return Convert.ToInt32(sqlCommand.ExecuteScalar()) == 1;
    }

    public static void Ensure(
        DatabaseProviderKind provider,
        string connectionString,
        string? schema,
        string? table,
        int commandTimeoutSeconds,
        ILogger logger)
    {
        if (string.IsNullOrWhiteSpace(schema))
        {
            return;
        }

        var schemaName = RequireIdentifier(schema, "JournalSchema");
        var tableName = RequireIdentifier(
            string.IsNullOrWhiteSpace(table) ? "schema_versions" : table,
            "JournalTable");

        var timeout = commandTimeoutSeconds < 0 ? 0 : commandTimeoutSeconds;
        var created = provider == DatabaseProviderKind.PostgreSql
            ? EnsurePostgreSql(connectionString, schemaName, tableName, timeout)
            : EnsureSqlServer(connectionString, schemaName, tableName, timeout);

        if (created)
        {
            logger.LogInformation("Created journal {Schema}.{Table}", schemaName, tableName);
        }
    }

    private static string RequireIdentifier(string value, string setting)
    {
        var name = value.Trim();
        if (name.Contains('\0'))
        {
            throw new InvalidOperationException($"{setting} contains a null character.");
        }

        if (name.Length > 128)
        {
            throw new InvalidOperationException($"{setting} '{name}' is longer than 128 characters.");
        }

        return name;
    }

    private static bool EnsureSqlServer(string connectionString, string schema, string table, int timeout)
    {
        using var connection = new SqlConnection(connectionString);
        connection.Open();
        var schemaCreated = ExecuteSqlServer(connection, SqlServerEnsureSql, schema, table, timeout) == 1;
        var tableCreated = ExecuteSqlServer(connection, SqlServerEnsureTableSql, schema, table, timeout) == 1;
        return schemaCreated || tableCreated;
    }

    private static int ExecuteSqlServer(SqlConnection connection, string sql, string schema, string table, int timeout)
    {
        using var command = connection.CreateCommand();
        command.CommandTimeout = timeout;
        command.CommandText = sql;
        command.Parameters.Add(new SqlParameter("@schema", SqlDbType.NVarChar, 128) { Value = schema });
        command.Parameters.Add(new SqlParameter("@table", SqlDbType.NVarChar, 128) { Value = table });
        return Convert.ToInt32(command.ExecuteScalar());
    }

    private static bool EnsurePostgreSql(string connectionString, string schema, string table, int timeout)
    {
        using var connection = new NpgsqlConnection(connectionString);
        connection.Open();
        var createdSchema = false;
        using (var exists = connection.CreateCommand())
        {
            exists.CommandTimeout = timeout;
            exists.CommandText = PostgreSqlExistsSql;
            exists.Parameters.Add(new NpgsqlParameter("schema", schema));
            createdSchema = !Convert.ToBoolean(exists.ExecuteScalar());
        }

        if (createdSchema)
        {
            using var quote = connection.CreateCommand();
            quote.CommandTimeout = timeout;
            quote.CommandText = PostgreSqlCreateSql;
            quote.Parameters.Add(new NpgsqlParameter("schema", schema));
            var createSql = Convert.ToString(quote.ExecuteScalar());
            if (string.IsNullOrWhiteSpace(createSql))
            {
                throw new InvalidOperationException("Could not build CREATE SCHEMA for the journal.");
            }

            using var create = connection.CreateCommand();
            create.CommandTimeout = timeout;
            create.CommandText = createSql;
            create.ExecuteNonQuery();
        }

        using var tableCommand = connection.CreateCommand();
        tableCommand.CommandTimeout = timeout;
        tableCommand.CommandText = """
            SELECT format(
                'CREATE TABLE IF NOT EXISTS %I.%I (
                    schemaversionsid serial NOT NULL PRIMARY KEY,
                    scriptname character varying(255) NOT NULL,
                    applied timestamp without time zone NOT NULL
                )',
                @schema,
                @table);
            """;
        tableCommand.Parameters.Add(new NpgsqlParameter("schema", schema));
        tableCommand.Parameters.Add(new NpgsqlParameter("table", table));
        var createTableSql = Convert.ToString(tableCommand.ExecuteScalar());
        if (string.IsNullOrWhiteSpace(createTableSql))
        {
            throw new InvalidOperationException("Could not build CREATE TABLE for the journal.");
        }

        using var createTable = connection.CreateCommand();
        createTable.CommandTimeout = timeout;
        createTable.CommandText = createTableSql;
        createTable.ExecuteNonQuery();
        return createdSchema;
    }
}
