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
        var sqlType = type.SqlType.Contains("NULL", StringComparison.OrdinalIgnoreCase)
            ? type.SqlType
            : type.SqlType + " NULL";
        return Build(provider, schema, table, column, sqlType, type.Reason);
    }

    public static string Build(
        DatabaseProviderKind provider,
        string schema,
        string table,
        CreateTableColumn column,
        string reason = "taken from CREATE TABLE source")
    {
        var sqlType = column.SqlType.Contains("NULL", StringComparison.OrdinalIgnoreCase)
            ? column.SqlType
            : column.SqlType + " NULL";
        return Build(provider, schema, table, column.Name, sqlType, reason);
    }

    public static string Build(
        DatabaseProviderKind provider,
        string schema,
        string table,
        string column,
        string sqlType,
        string reason)
    {
        var qualified = $"{SqlIdentifier.Quote(provider, schema)}.{SqlIdentifier.Quote(provider, table)}";
        var quotedColumn = SqlIdentifier.Quote(provider, column);
        var add = provider == DatabaseProviderKind.PostgreSql ? "ADD COLUMN" : "ADD";
        return $"""
            -- Column type {reason}.
            ALTER TABLE {qualified} {add} {quotedColumn} {sqlType};
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
