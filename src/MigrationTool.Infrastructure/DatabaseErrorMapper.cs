using Microsoft.Data.SqlClient;
using MigrationTool.Domain;
using Npgsql;

namespace MigrationTool.Infrastructure;

internal static class DatabaseErrorMapper
{
    public static ScriptRunResult From(Exception exception)
    {
        int? number = null;
        string? state = null;
        foreach (var current in Chain(exception))
        {
            if (current is SqlException sql)
            {
                number ??= sql.Number;
            }

            if (current is PostgresException postgres)
            {
                state ??= postgres.SqlState;
            }
        }

        return ScriptRunResult.Fail(exception.ToString(), number, state);
    }

    private static IEnumerable<Exception> Chain(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            yield return current;
        }
    }
}
