namespace MigrationTool.Domain;

public static class TableColumnInventory
{
    public static string ExistsSql(DatabaseProviderKind provider, string schema, string table)
    {
        var schemaLiteral = SqlIdentifier.Literal(provider, schema);
        var tableLiteral = SqlIdentifier.Literal(provider, table);
        if (provider == DatabaseProviderKind.PostgreSql)
        {
            return $"""
                SELECT CASE
                    WHEN EXISTS (
                        SELECT 1
                        FROM information_schema.tables
                        WHERE table_schema = {schemaLiteral}
                          AND table_name = {tableLiteral}
                          AND table_type = 'BASE TABLE'
                    ) THEN 1 ELSE 0 END AS table_exists;
                """;
        }

        return $"""
            SELECT CASE
                WHEN OBJECT_ID({SqlIdentifier.Literal(provider, schema + "." + table)}, N'U') IS NOT NULL THEN 1
                ELSE 0 END AS table_exists;
            """;
    }

    public static string ColumnsSql(DatabaseProviderKind provider, string schema, string table)
    {
        var schemaLiteral = SqlIdentifier.Literal(provider, schema);
        var tableLiteral = SqlIdentifier.Literal(provider, table);
        if (provider == DatabaseProviderKind.PostgreSql)
        {
            return $"""
                SELECT column_name
                FROM information_schema.columns
                WHERE table_schema = {schemaLiteral}
                  AND table_name = {tableLiteral}
                ORDER BY ordinal_position;
                """;
        }

        return $"""
            SELECT COLUMN_NAME
            FROM INFORMATION_SCHEMA.COLUMNS
            WHERE TABLE_SCHEMA = {schemaLiteral}
              AND TABLE_NAME = {tableLiteral}
            ORDER BY ORDINAL_POSITION;
            """;
    }

    public static bool ParseExists(string? resultText)
    {
        foreach (var value in QueryResultParser.ReadFirstColumnValues(resultText))
        {
            if (value is "1" or "true" or "True" or "TRUE")
            {
                return true;
            }
        }

        return false;
    }

    public static HashSet<string> ParseColumnNames(string? resultText) =>
        new(QueryResultParser.ReadFirstColumnValues(resultText), StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<CreateTableColumn> MissingColumns(
        CreateTableDefinition definition,
        IReadOnlyCollection<string> liveColumns)
    {
        var live = new HashSet<string>(liveColumns, StringComparer.OrdinalIgnoreCase);
        return definition.Columns
            .Where(column => !live.Contains(column.Name))
            .ToList();
    }
}

public static class QueryResultParser
{
    public static IReadOnlyList<string> ReadFirstColumnValues(string? resultText)
    {
        var values = new List<string>();
        if (string.IsNullOrWhiteSpace(resultText))
        {
            return values;
        }

        var lines = resultText.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n');
        var sawHeader = false;
        foreach (var raw in lines)
        {
            var line = raw.Trim();
            if (line.Length == 0)
            {
                continue;
            }

            if (line.StartsWith('(') && line.EndsWith(')'))
            {
                continue;
            }

            var first = line.Split('|', 2)[0].Trim();
            if (!sawHeader)
            {
                sawHeader = true;
                continue;
            }

            if (first.Length > 0 && !first.Equals("NULL", StringComparison.OrdinalIgnoreCase))
            {
                values.Add(first);
            }
        }

        return values;
    }
}

public static class TableAlreadyExistsParser
{
    public static bool Matches(string? errorText, int? errorNumber, string? sqlState)
    {
        if (errorNumber is 2714 or 1801)
        {
            return true;
        }

        if (string.Equals(sqlState, "42P07", StringComparison.Ordinal))
        {
            return true;
        }

        var text = errorText ?? "";
        return text.Contains("already an object named", StringComparison.OrdinalIgnoreCase)
               || text.Contains("already exists", StringComparison.OrdinalIgnoreCase)
               || text.Contains("There is already an object", StringComparison.OrdinalIgnoreCase);
    }
}
