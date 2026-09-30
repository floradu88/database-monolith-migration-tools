using MigrationTool.Domain;

namespace MigrationTool.UnitTests;

public class ScriptKindClassifierTests
{
    [Theory]
    [InlineData("ddl/001.sql", "SELECT 1;", ScriptKind.Ddl)]
    [InlineData("dml/001.sql", "SELECT 1;", ScriptKind.Dml)]
    [InlineData("data/001.sql", "SELECT 1;", ScriptKind.Data)]
    [InlineData("query/001.sql", "INSERT INTO t (id) VALUES (1);", ScriptKind.Query)]
    public void Classify_UsesFolderName(string path, string sql, ScriptKind expected)
    {
        Assert.Equal(expected, ScriptKindClassifier.Classify(path, sql));
    }

    [Fact]
    public void Classify_UsesKindCommentBeforeStatement()
    {
        var sql = """
            -- kind: data
            INSERT INTO city (name) VALUES ('Timisoara');
            """;

        Assert.Equal(ScriptKind.Data, ScriptKindClassifier.Classify("city.sql", sql));
    }

    [Theory]
    [InlineData("CREATE TABLE city (id int);", ScriptKind.Ddl)]
    [InlineData("UPDATE city SET name = 'A';", ScriptKind.Dml)]
    [InlineData("SELECT id FROM city;", ScriptKind.Query)]
    public void Classify_UsesFirstStatementWhenNoHint(string sql, ScriptKind expected)
    {
        Assert.Equal(expected, ScriptKindClassifier.Classify("city.sql", sql));
    }
}
