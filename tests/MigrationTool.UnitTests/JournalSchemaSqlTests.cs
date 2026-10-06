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
    public void SqlServerEnsureTableSql_CreatesTheJournalTableInDynamicSql()
    {
        Assert.Contains("EXEC(@sql)", JournalSchemaEnsurer.SqlServerEnsureTableSql, StringComparison.Ordinal);
        Assert.Contains("[ScriptName] nvarchar(255) NOT NULL", JournalSchemaEnsurer.SqlServerEnsureTableSql, StringComparison.Ordinal);
        Assert.Contains("[Applied] datetime NOT NULL", JournalSchemaEnsurer.SqlServerEnsureTableSql, StringComparison.Ordinal);
        Assert.DoesNotContain("[migration]", JournalSchemaEnsurer.SqlServerEnsureTableSql, StringComparison.Ordinal);
    }

    [Fact]
    public void SqlServerSchemaExistsSql_OnlyChecksForTheSchema()
    {
        Assert.Contains("sys.schemas", JournalSchemaEnsurer.SqlServerSchemaExistsSql, StringComparison.Ordinal);
        Assert.DoesNotContain("CREATE", JournalSchemaEnsurer.SqlServerSchemaExistsSql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PostgreSqlEnsureSql_QuotesTheSchemaName()
    {
        Assert.Contains("information_schema.schemata", JournalSchemaEnsurer.PostgreSqlExistsSql, StringComparison.Ordinal);
        Assert.Contains("CREATE SCHEMA IF NOT EXISTS %I", JournalSchemaEnsurer.PostgreSqlCreateSql, StringComparison.Ordinal);
        Assert.DoesNotContain("CREATE TABLE", JournalSchemaEnsurer.PostgreSqlCreateSql, StringComparison.OrdinalIgnoreCase);
    }
}
