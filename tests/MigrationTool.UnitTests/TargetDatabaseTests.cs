using MigrationTool.Domain;
using MigrationTool.Infrastructure;

namespace MigrationTool.UnitTests;

public class TargetDatabaseTests
{
    [Theory]
    [InlineData("AppDb", "AppDb", true)]
    [InlineData("AppDb", "appdb", true)]
    [InlineData(" AppDb ", "AppDb", true)]
    [InlineData("AppDb", "master", false)]
    [InlineData("AppDb", "", false)]
    [InlineData("", "AppDb", false)]
    [InlineData(null, "AppDb", false)]
    public void Matches_compares_the_session_database_to_the_target(string? expected, string? actual, bool matches)
    {
        Assert.Equal(matches, TargetDatabase.Matches(expected, actual));
    }

    [Fact]
    public void JournalLookupSql_reads_the_sql_server_journal_on_the_named_database_objects()
    {
        var sql = TargetDatabaseProbe.JournalLookupSql(
            DatabaseProviderKind.SqlServer,
            "migration",
            "schema_versions");

        Assert.Contains("[migration].[schema_versions]", sql, StringComparison.Ordinal);
        Assert.Contains("[ScriptName] = @scriptName", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("ProductionMigrationStatus", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void JournalLookupSql_reads_the_postgresql_scriptname_column()
    {
        var sql = TargetDatabaseProbe.JournalLookupSql(
            DatabaseProviderKind.PostgreSql,
            "migration",
            "schema_versions");

        Assert.Contains("\"migration\".\"schema_versions\"", sql, StringComparison.Ordinal);
        Assert.Contains("\"scriptname\" = @scriptName", sql, StringComparison.Ordinal);
    }

    [Fact]
    public void CurrentDatabaseSql_asks_the_server_for_the_session_database()
    {
        Assert.Equal("SELECT DB_NAME();", TargetDatabaseProbe.SqlServerCurrentDatabaseSql);
        Assert.Equal("SELECT current_database();", TargetDatabaseProbe.PostgreSqlCurrentDatabaseSql);
    }
}