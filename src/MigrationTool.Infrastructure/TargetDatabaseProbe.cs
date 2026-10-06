using System.Data;
using Microsoft.Data.SqlClient;
using MigrationTool.Domain;
using Npgsql;

namespace MigrationTool.Infrastructure;

internal static class TargetDatabaseProbe
{
    internal const string SqlServerCurrentDatabaseSql = "SELECT DB_NAME();";
    internal const string PostgreSqlCurrentDatabaseSql = "SELECT current_database();";

    public static string JournalLookupSql(DatabaseProviderKind provider, string? schema, string table)
    {
        var tableRef = string.IsNullOrWhiteSpace(schema)
            ? SqlIdentifier.Quote(provider, table)
            : SqlIdentifier.Quote(provider, schema.Trim()) + "." + SqlIdentifier.Quote(provider, table);
        var column = provider == DatabaseProviderKind.PostgreSql ? "scriptname" : "ScriptName";
        var quotedColumn = SqlIdentifier.Quote(provider, column);
        return $"SELECT CASE WHEN EXISTS (SELECT 1 FROM {tableRef} WHERE {quotedColumn} = @scriptName) THEN 1 ELSE 0 END;";
    }

    public static string ReadCurrentDatabase(
        DatabaseProviderKind provider,
        string connectionString,
        int commandTimeoutSeconds)
    {
        var timeout = commandTimeoutSeconds < 0 ? 0 : commandTimeoutSeconds;
        if (provider == DatabaseProviderKind.PostgreSql)
        {
            using var connection = new NpgsqlConnection(connectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandTimeout = timeout;
            command.CommandText = PostgreSqlCurrentDatabaseSql;
            return ReadName(command.ExecuteScalar());
        }

        using var sqlConnection = new SqlConnection(connectionString);
        sqlConnection.Open();
        using var sqlCommand = sqlConnection.CreateCommand();
        sqlCommand.CommandTimeout = timeout;
        sqlCommand.CommandText = SqlServerCurrentDatabaseSql;
        return ReadName(sqlCommand.ExecuteScalar());
    }

    public static bool JournalContains(
        DatabaseProviderKind provider,
        string connectionString,
        string? schema,
        string table,
        string scriptName,
        int commandTimeoutSeconds)
    {
        var timeout = commandTimeoutSeconds < 0 ? 0 : commandTimeoutSeconds;
        var sql = JournalLookupSql(provider, schema, table);
        if (provider == DatabaseProviderKind.PostgreSql)
        {
            using var connection = new NpgsqlConnection(connectionString);
            connection.Open();
            using var command = connection.CreateCommand();
            command.CommandTimeout = timeout;
            command.CommandText = sql;
            command.Parameters.Add(new NpgsqlParameter("scriptName", scriptName));
            return Convert.ToInt32(command.ExecuteScalar()) == 1;
        }

        using var sqlConnection = new SqlConnection(connectionString);
        sqlConnection.Open();
        using var sqlCommand = sqlConnection.CreateCommand();
        sqlCommand.CommandTimeout = timeout;
        sqlCommand.CommandText = sql;
        sqlCommand.Parameters.Add(new SqlParameter("@scriptName", SqlDbType.NVarChar, 255) { Value = scriptName });
        return Convert.ToInt32(sqlCommand.ExecuteScalar()) == 1;
    }

    private static string ReadName(object? value)
    {
        if (value is null or DBNull)
        {
            throw new InvalidOperationException("The server did not return the current database name.");
        }

        var name = Convert.ToString(value);
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new InvalidOperationException("The server did not return the current database name.");
        }

        return name;
    }
}
