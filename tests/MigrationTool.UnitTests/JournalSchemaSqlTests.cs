using MigrationTool.Infrastructure;

namespace MigrationTool.UnitTests;

public class JournalSchemaSqlTests
{
    [Fact]
    public void SqlServerEnsureSql_CreatesSchemaInItsOwnBatch()
    {
        Assert.Contains("CREATE SCHEMA", JournalSchemaEnsurer.SqlServerEnsureSql, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("QUOTENAME(@schema)", JournalSchemaEnsurer.SqlServerEnsureSql, StringComparison.Ordinal);
        Assert.Contains("EXEC(N'CREATE SCHEMA '", JournalSchemaEnsurer.SqlServerEnsureSql, StringComparison.Ordinal);
        Assert.DoesNotContain("CREATE TABLE", JournalSchemaEnsurer.SqlServerEnsureSql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PostgreSqlEnsureSql_QuotesTheSchemaName()
    {
        Assert.Contains("information_schema.schemata", JournalSchemaEnsurer.PostgreSqlExistsSql, StringComparison.Ordinal);
        Assert.Contains("CREATE SCHEMA IF NOT EXISTS %I", JournalSchemaEnsurer.PostgreSqlCreateSql, StringComparison.Ordinal);
        Assert.DoesNotContain("CREATE TABLE", JournalSchemaEnsurer.PostgreSqlCreateSql, StringComparison.OrdinalIgnoreCase);
    }
}
