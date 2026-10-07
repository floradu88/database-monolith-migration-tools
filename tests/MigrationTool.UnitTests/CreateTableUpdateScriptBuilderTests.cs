using MigrationTool.Domain;

namespace MigrationTool.UnitTests;

public class CreateTableUpdateScriptBuilderTests
{
    [Fact]
    public void Build_CreatesAdditiveUpdateScriptFromMissingColumns()
    {
        var script = CreateTableUpdateScriptBuilder.Build(
            DatabaseProviderKind.SqlServer,
            "dbo",
            "City",
            [new CreateTableColumn("Code", "nvarchar(10) NULL")],
            ["Id", "Name", "Code"],
            ["Id", "Name"]);

        Assert.Contains("-- Update script for [dbo].[City]", script, StringComparison.Ordinal);
        Assert.Contains("Source columns: Id, Name, Code", script, StringComparison.Ordinal);
        Assert.Contains("Live columns: Id, Name", script, StringComparison.Ordinal);
        Assert.Contains("Columns to add: Code", script, StringComparison.Ordinal);
        Assert.Contains("ALTER TABLE [dbo].[City] ADD [Code] nvarchar(10) NULL;", script, StringComparison.Ordinal);
        Assert.DoesNotContain("DROP COLUMN", script, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("DROP TABLE", script, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("update_dbo_City.sql", CreateTableUpdateScriptBuilder.FileName("dbo", "City"));
    }

    [Fact]
    public void Build_WhenNothingMissing_WritesNoAlterStatements()
    {
        var script = CreateTableUpdateScriptBuilder.Build(
            DatabaseProviderKind.SqlServer,
            "dbo",
            "City",
            [],
            ["Id"],
            ["Id"]);

        Assert.Contains("No ALTER statements required", script, StringComparison.Ordinal);
        Assert.DoesNotContain("ALTER TABLE", script, StringComparison.OrdinalIgnoreCase);
    }
}
