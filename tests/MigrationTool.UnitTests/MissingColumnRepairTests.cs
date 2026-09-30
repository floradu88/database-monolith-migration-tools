using MigrationTool.Domain;

namespace MigrationTool.UnitTests;

public class MissingColumnRepairTests
{
    [Fact]
    public void Parse_ReadsSqlServerInvalidColumnAndTableFromScript()
    {
        var fault = MissingColumnParser.Parse(
            "Invalid column name 'Title'.",
            errorNumber: 207,
            sqlState: null,
            "INSERT INTO dbo.Books (Title) VALUES ('Dune');");

        Assert.NotNull(fault);
        Assert.Equal("Title", fault.Column);
        Assert.Equal("Books", fault.Table);
        Assert.Equal("dbo", fault.Schema);
    }

    [Fact]
    public void Parse_ReadsPostgresColumnAndRelation()
    {
        var fault = MissingColumnParser.Parse(
            "42703: column \"title\" of relation \"books\" does not exist",
            errorNumber: null,
            sqlState: "42703",
            "SELECT title FROM books;");

        Assert.NotNull(fault);
        Assert.Equal("title", fault.Column);
        Assert.Equal("books", fault.Table);
    }

    [Fact]
    public void Parse_IgnoresUnrelatedErrors()
    {
        var fault = MissingColumnParser.Parse("divide by zero", 8134, null, "SELECT 1/0;");

        Assert.Null(fault);
    }

    [Fact]
    public void Alter_UsesLiteralTypeForEachProvider()
    {
        const string sql = "INSERT INTO dbo.Books (Title, Pages) VALUES ('Dune', 400);";

        var sqlServer = AlterScriptBuilder.Build(
            DatabaseProviderKind.SqlServer,
            "dbo",
            "Books",
            "Pages",
            ColumnTypeInferrer.Infer(DatabaseProviderKind.SqlServer, sql, "Pages"));
        var postgres = AlterScriptBuilder.Build(
            DatabaseProviderKind.PostgreSql,
            "public",
            "books",
            "title",
            ColumnTypeInferrer.Infer(DatabaseProviderKind.PostgreSql, sql, "Title"));

        Assert.Contains("ADD [Pages] int NULL", sqlServer, StringComparison.Ordinal);
        Assert.Contains("inferred from a literal", sqlServer, StringComparison.Ordinal);
        Assert.Contains("ADD COLUMN \"title\" text NULL", postgres, StringComparison.Ordinal);
    }

    [Fact]
    public void Alter_FallsBackWhenNoLiteralExists()
    {
        var inferred = ColumnTypeInferrer.Infer(
            DatabaseProviderKind.SqlServer,
            "SELECT Title FROM dbo.Books;",
            "Title");

        Assert.Equal("nvarchar(max)", inferred.SqlType);
        Assert.Contains("no CAST or literal", inferred.Reason, StringComparison.Ordinal);
    }

    [Fact]
    public void Diagnostic_IncludesColumnAndTableStateForBothProviders()
    {
        var sqlServer = DiagnosticScriptBuilder.Build(DatabaseProviderKind.SqlServer, "dbo", "Books");
        var postgres = DiagnosticScriptBuilder.Build(DatabaseProviderKind.PostgreSql, "public", "books");

        Assert.Contains("INFORMATION_SCHEMA.COLUMNS", sqlServer, StringComparison.Ordinal);
        Assert.Contains("sys.tables", sqlServer, StringComparison.Ordinal);
        Assert.Contains("N'dbo'", sqlServer, StringComparison.Ordinal);
        Assert.Contains("information_schema.columns", postgres, StringComparison.Ordinal);
        Assert.Contains("pg_class", postgres, StringComparison.Ordinal);
        Assert.Contains("'books'", postgres, StringComparison.Ordinal);
        Assert.DoesNotContain("N'", postgres, StringComparison.Ordinal);
    }
}
