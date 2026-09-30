namespace MigrationTool.Domain;

public static class SqlIdentifier
{
    public static string Quote(DatabaseProviderKind provider, string identifier)
    {
        if (provider == DatabaseProviderKind.PostgreSql)
        {
            return "\"" + identifier.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"";
        }

        return "[" + identifier.Replace("]", "]]", StringComparison.Ordinal) + "]";
    }

    public static string Literal(DatabaseProviderKind provider, string value)
    {
        var body = "'" + value.Replace("'", "''", StringComparison.Ordinal) + "'";
        return provider == DatabaseProviderKind.SqlServer ? "N" + body : body;
    }

    public static string DefaultSchema(DatabaseProviderKind provider) =>
        provider == DatabaseProviderKind.PostgreSql ? "public" : "dbo";
}
