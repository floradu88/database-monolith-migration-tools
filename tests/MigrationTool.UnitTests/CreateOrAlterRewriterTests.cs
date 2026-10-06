using MigrationTool.Domain;

namespace MigrationTool.UnitTests;

public class CreateOrAlterRewriterTests
{
    [Theory]
    [InlineData("PROCEDURE")]
    [InlineData("PROC")]
    [InlineData("FUNCTION")]
    [InlineData("VIEW")]
    [InlineData("TRIGGER")]
    public void SqlServer_RewritesCreateModuleToCreateOrAlter(string kind)
    {
        var sql = $"CREATE {kind} dbo.example AS BEGIN SELECT 1; END";

        var result = CreateOrAlterRewriter.Rewrite(sql, DatabaseProviderKind.SqlServer);

        Assert.True(result.Changed);
        Assert.True(result.IsModule);
        Assert.StartsWith($"CREATE OR ALTER {kind}", result.Sql, StringComparison.Ordinal);
    }

    [Fact]
    public void SqlServer_LeavesCreateOrAlterAndCreateTable()
    {
        const string proc = "CREATE OR ALTER PROCEDURE dbo.example AS BEGIN SELECT 1; END";
        var existing = CreateOrAlterRewriter.Rewrite(proc, DatabaseProviderKind.SqlServer);
        Assert.False(existing.Changed);
        Assert.True(existing.IsModule);
        Assert.Equal(proc, existing.Sql);

        const string table = "CREATE TABLE dbo.City (Id int);";
        var created = CreateOrAlterRewriter.Rewrite(table, DatabaseProviderKind.SqlServer);
        Assert.False(created.Changed);
        Assert.False(created.IsModule);
        Assert.Equal(table, created.Sql);
    }

    [Fact]
    public void DoesNotRewriteCreateInsideStringsOrComments()
    {
        const string sql = """
            -- CREATE PROCEDURE skipped
            /* CREATE FUNCTION skipped */
            CREATE PROCEDURE dbo.example
            AS
            BEGIN
                SELECT 'CREATE PROCEDURE not real';
            END
            """;

        var result = CreateOrAlterRewriter.Rewrite(sql, DatabaseProviderKind.SqlServer);

        Assert.Equal(1, Count(result.Sql, "CREATE OR ALTER PROCEDURE"));
        Assert.Contains("SELECT 'CREATE PROCEDURE not real'", result.Sql, StringComparison.Ordinal);
        Assert.Contains("-- CREATE PROCEDURE skipped", result.Sql, StringComparison.Ordinal);
    }

    [Fact]
    public void PostgreSql_UsesCreateOrReplace()
    {
        var result = CreateOrAlterRewriter.Rewrite(
            "CREATE FUNCTION app.example() RETURNS int LANGUAGE sql AS $$ SELECT 1 $$;",
            DatabaseProviderKind.PostgreSql);

        Assert.StartsWith("CREATE OR REPLACE FUNCTION", result.Sql, StringComparison.Ordinal);
        Assert.True(result.IsModule);
    }

    [Fact]
    public void MixedFile_RewritesTheProcedureAndIsNotRepeatable()
    {
        var rewrite = CreateOrAlterRewriter.RewriteBatches(
            ["CREATE TABLE dbo.City (Id int);", "CREATE PROCEDURE dbo.p AS BEGIN SELECT 1; END"],
            DatabaseProviderKind.SqlServer);

        Assert.False(rewrite.Repeatable);
        Assert.True(rewrite.Changed);
        Assert.Equal("CREATE TABLE dbo.City (Id int);", rewrite.Batches[0]);
        Assert.StartsWith("CREATE OR ALTER PROCEDURE", rewrite.Batches[1], StringComparison.Ordinal);
    }

    private static int Count(string text, string value)
    {
        var count = 0;
        var index = 0;
        while ((index = text.IndexOf(value, index, StringComparison.Ordinal)) >= 0)
        {
            count++;
            index += value.Length;
        }

        return count;
    }
}
