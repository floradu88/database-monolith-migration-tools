using Microsoft.Data.SqlClient;
using MigrationTool.Domain;
using Npgsql;

namespace MigrationTool.Infrastructure;

internal static class DatabaseConnectionExceptionMapper
{
    public static DatabaseConnectionError Map(Exception exception, DatabaseProviderKind provider)
    {
        foreach (var current in Chain(exception))
        {
            if (current is SqlException sql)
            {
                return DatabaseConnectionErrorClassifier.FromSqlServer(sql.Number, sql.Message);
            }

            if (current is PostgresException postgres)
            {
                return DatabaseConnectionErrorClassifier.FromPostgreSql(postgres.SqlState, postgres.MessageText);
            }

            if (current is TimeoutException)
            {
                return provider == DatabaseProviderKind.SqlServer
                    ? DatabaseConnectionErrorClassifier.FromSqlServer(-2, current.Message)
                    : DatabaseConnectionErrorClassifier.FromPostgreSql("08006", current.Message);
            }
        }

        return provider == DatabaseProviderKind.SqlServer
            ? DatabaseConnectionErrorClassifier.FromSqlServer(null, exception.Message)
            : DatabaseConnectionErrorClassifier.FromPostgreSql(null, exception.Message);
    }

    private static IEnumerable<Exception> Chain(Exception exception)
    {
        for (Exception? current = exception; current is not null; current = current.InnerException)
        {
            yield return current;
        }
    }
}
