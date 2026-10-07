using MigrationTool.Domain;

namespace MigrationTool.UnitTests;

public class CreateTableDefinitionParserTests
{
    [Fact]
    public void ParseAll_ReadsSchemaTableAndColumnsWithTypes()
    {
        const string sql = """
            CREATE TABLE [dbo].[City] (
                [Id] int NOT NULL,
                [Name] nvarchar(100) NULL,
                [Population] decimal(18, 2) NOT NULL,
                CONSTRAINT [PK_City] PRIMARY KEY ([Id])
            );
            """;

        var definition = Assert.Single(CreateTableDefinitionParser.ParseAll(sql));

        Assert.Equal("dbo", definition.Schema);
        Assert.Equal("City", definition.Table);
        Assert.Equal(3, definition.Columns.Count);
        Assert.Equal("Id", definition.Columns[0].Name);
        Assert.Contains("int", definition.Columns[0].SqlType, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("NOT NULL", definition.Columns[0].SqlType, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Name", definition.Columns[1].Name);
        Assert.Contains("nvarchar(100)", definition.Columns[1].SqlType, StringComparison.OrdinalIgnoreCase);
        Assert.Equal("Population", definition.Columns[2].Name);
        Assert.Contains("decimal(18, 2)", definition.Columns[2].SqlType, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void MissingColumns_ComparesSourceAgainstLiveNames()
    {
        var definition = Assert.Single(CreateTableDefinitionParser.ParseAll(
            "CREATE TABLE dbo.City (Id int NOT NULL, Name nvarchar(50) NULL, Code char(3) NULL);"));

        var missing = TableColumnInventory.MissingColumns(definition, ["Id", "Name"]);

        var column = Assert.Single(missing);
        Assert.Equal("Code", column.Name);
        Assert.Contains("char(3)", column.SqlType, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Alter_UsesCreateTableSourceType()
    {
        var column = new CreateTableColumn("Code", "nvarchar(10) NOT NULL");
        var sql = AlterScriptBuilder.Build(DatabaseProviderKind.SqlServer, "dbo", "City", column);

        Assert.Contains("ADD [Code] nvarchar(10) NOT NULL", sql, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE source", sql, StringComparison.Ordinal);
        Assert.DoesNotContain("DROP", sql, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void QueryResultParser_ReadsLiveColumnNames()
    {
        const string result = """
            COLUMN_NAME
            Id
            Name
            (2 rows)
            """;

        var names = TableColumnInventory.ParseColumnNames(result);

        Assert.Contains("Id", names);
        Assert.Contains("Name", names);
        Assert.Equal(2, names.Count);
    }

    [Fact]
    public void TableAlreadyExistsParser_DetectsSqlServerAndPostgres()
    {
        Assert.True(TableAlreadyExistsParser.Matches("There is already an object named 'City'", 2714, null));
        Assert.True(TableAlreadyExistsParser.Matches("relation \"city\" already exists", null, "42P07"));
        Assert.False(TableAlreadyExistsParser.Matches("Invalid column name 'Title'", 207, null));
    }

    [Fact]
    public void ExistsAndColumnsSql_TargetTheNamedTable()
    {
        var exists = TableColumnInventory.ExistsSql(DatabaseProviderKind.SqlServer, "dbo", "City");
        var columns = TableColumnInventory.ColumnsSql(DatabaseProviderKind.SqlServer, "dbo", "City");

        Assert.Contains("OBJECT_ID", exists, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("N'dbo.City'", exists, StringComparison.Ordinal);
        Assert.Contains("INFORMATION_SCHEMA.COLUMNS", columns, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("N'dbo'", columns, StringComparison.Ordinal);
        Assert.Contains("N'City'", columns, StringComparison.Ordinal);
    }
}
