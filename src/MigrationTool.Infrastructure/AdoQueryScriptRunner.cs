using System.Data.Common;
using System.Globalization;
using System.Text;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Options;
using MigrationTool.Application;
using MigrationTool.Domain;
using Npgsql;

namespace MigrationTool.Infrastructure;

public sealed class AdoQueryScriptRunner : IQueryScriptRunner
{
    private readonly MigrationOptions _options;

    public AdoQueryScriptRunner(IOptions<MigrationOptions> options)
    {
        _options = options.Value;
    }

    public async Task<ScriptRunResult> ExecuteAsync(QueryScriptRequest script, CancellationToken cancellationToken)
    {
        try
        {
            await using var connection = OpenConnection();
            await connection.OpenAsync(cancellationToken);
            await using var command = connection.CreateCommand();
            command.CommandText = script.Sql;
            command.CommandTimeout = Math.Max(script.CommandTimeoutSeconds, 0);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            var text = await ReadGridsAsync(reader, cancellationToken);
            return ScriptRunResult.Ok(text);
        }
        catch (Exception ex)
        {
            return DatabaseErrorMapper.From(ex);
        }
    }

    private DbConnection OpenConnection() =>
        _options.Provider == DatabaseProviderKind.PostgreSql
            ? new NpgsqlConnection(_options.ConnectionString)
            : new SqlConnection(_options.ConnectionString);

    private static async Task<string> ReadGridsAsync(DbDataReader reader, CancellationToken cancellationToken)
    {
        var builder = new StringBuilder();
        var grid = 0;
        do
        {
            if (grid > 0)
            {
                builder.AppendLine();
            }

            grid++;
            if (reader.FieldCount == 0)
            {
                builder.AppendLine("(0 rows)");
                continue;
            }

            var headers = Enumerable.Range(0, reader.FieldCount).Select(reader.GetName);
            builder.AppendLine(string.Join(" | ", headers));
            var rows = 0;
            while (await reader.ReadAsync(cancellationToken))
            {
                rows++;
                var cells = new string[reader.FieldCount];
                for (var i = 0; i < reader.FieldCount; i++)
                {
                    cells[i] = reader.IsDBNull(i)
                        ? "NULL"
                        : Convert.ToString(reader.GetValue(i), CultureInfo.InvariantCulture) ?? "";
                }

                builder.AppendLine(string.Join(" | ", cells));
            }

            builder.AppendLine(rows == 1 ? "(1 row)" : $"({rows} rows)");
        }
        while (await reader.NextResultAsync(cancellationToken));

        return builder.ToString();
    }
}
