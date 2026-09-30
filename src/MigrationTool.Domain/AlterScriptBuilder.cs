namespace MigrationTool.Domain;

public static class AlterScriptBuilder
{
    public static string Build(
        DatabaseProviderKind provider,
        string schema,
        string table,
        string column,
        InferredColumnType type)
    {
        var qualified = $"{SqlIdentifier.Quote(provider, schema)}.{SqlIdentifier.Quote(provider, table)}";
        var quotedColumn = SqlIdentifier.Quote(provider, column);
        var add = provider == DatabaseProviderKind.PostgreSql ? "ADD COLUMN" : "ADD";
        return $"""
            -- Column type {type.Reason}.
            ALTER TABLE {qualified} {add} {quotedColumn} {type.SqlType} NULL;
            """;
    }

    public static string FileName(string schema, string table, string column) =>
        $"pre_add_{Safe(schema)}_{Safe(table)}_{Safe(column)}.sql";

    private static string Safe(string value)
    {
        var chars = value.Select(ch => char.IsLetterOrDigit(ch) ? ch : '_').ToArray();
        var safe = new string(chars).Trim('_');
        return safe.Length == 0 ? "object" : safe;
    }
}
