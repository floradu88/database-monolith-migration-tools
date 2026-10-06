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

    internal const string PostgreSqlExistsSql =
        "SELECT EXISTS (SELECT 1 FROM information_schema.schemata WHERE schema_name = @schema);";

    internal const string PostgreSqlCreateSql =
        "SELECT format('CREATE SCHEMA IF NOT EXISTS %I', @schema);";

    public static void Ensure(
        DatabaseProviderKind provider,
        string connectionString,
        string? schema,
        int commandTimeoutSeconds,
        ILogger logger)
    {
        if (string.IsNullOrWhiteSpace(schema))
        {
            return;
        }

        var name = schema.Trim();
        if (name.Contains('\0'))
        {
            throw new InvalidOperationException("JournalSchema contains a null character.");
        }

        if (name.Length > 128)
        {
            throw new InvalidOperationException(
                $"JournalSchema '{name}' is longer than 128 characters.");
        }

        var timeout = commandTimeoutSeconds < 0 ? 0 : commandTimeoutSeconds;
        var created = provider == DatabaseProviderKind.PostgreSql
            ? EnsurePostgreSql(connectionString, name, timeout)
            : EnsureSqlServer(connectionString, name, timeout);

        if (created)
        {
            logger.LogInformation("Created journal schema {Schema}", name);
        }
    }

    private static bool EnsureSqlServer(string connectionString, string schema, int timeout)
    {
        using var connection = new SqlConnection(connectionString);
        connection.Open();
        using var command = connection.CreateCommand();
        command.CommandTimeout = timeout;
        command.CommandText = SqlServerEnsureSql;
        command.Parameters.Add(new SqlParameter("@schema", SqlDbType.NVarChar, 128) { Value = schema });
        return Convert.ToInt32(command.ExecuteScalar()) == 1;
    }

    private static bool EnsurePostgreSql(string connectionString, string schema, int timeout)
    {
        using var connection = new NpgsqlConnection(connectionString);
        connection.Open();
        using var exists = connection.CreateCommand();
        exists.CommandTimeout = timeout;
        exists.CommandText = PostgreSqlExistsSql;
        exists.Parameters.Add(new NpgsqlParameter("schema", schema));
        if (Convert.ToBoolean(exists.ExecuteScalar()))
        {
            return false;
        }

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
        return true;
    }
}
