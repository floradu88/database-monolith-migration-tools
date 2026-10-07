namespace MigrationTool.Domain;

public sealed record CreateTableColumn(string Name, string SqlType);

public sealed record CreateTableDefinition(
    string? Schema,
    string Table,
    IReadOnlyList<CreateTableColumn> Columns,
    int SourceStart,
    int SourceLength);

public static class CreateTableDefinitionParser
{
    private static readonly HashSet<string> TableConstraintStarts = new(StringComparer.OrdinalIgnoreCase)
    {
        "CONSTRAINT", "PRIMARY", "FOREIGN", "UNIQUE", "CHECK", "INDEX", "KEY"
    };

    private static readonly HashSet<string> TypeTerminators = new(StringComparer.OrdinalIgnoreCase)
    {
        "NULL", "NOT", "CONSTRAINT", "PRIMARY", "IDENTITY", "DEFAULT", "REFERENCES",
        "UNIQUE", "CHECK", "COLLATE", "CLUSTERED", "NONCLUSTERED", "ROWGUIDCOL",
        "SPARSE", "PERSISTED", "GENERATED", "ON"
    };

    public static IReadOnlyList<CreateTableDefinition> ParseAll(string sql)
    {
        var text = sql ?? "";
        var definitions = new List<CreateTableDefinition>();
        var index = 0;
        while (index < text.Length)
        {
            if (!TryFindCreateTable(text, index, out var createStart, out var nameStart, out var openParen))
            {
                break;
            }

            var closeParen = FindMatchingParen(text, openParen);
            if (closeParen < 0)
            {
                break;
            }

            var nameSpan = text[nameStart..openParen].Trim();
            var (schema, table) = SplitName(nameSpan);
            if (string.IsNullOrWhiteSpace(table))
            {
                index = closeParen + 1;
                continue;
            }

            var body = text[(openParen + 1)..closeParen];
            var columns = ParseColumns(body);
            if (columns.Count > 0)
            {
                definitions.Add(new CreateTableDefinition(schema, table, columns, createStart, closeParen - createStart + 1));
            }

            index = closeParen + 1;
        }

        return definitions;
    }

    public static bool ContainsCreateTable(string sql) =>
        ParseAll(sql).Count > 0;

    public static bool BatchIsCreateTableFor(string batch, CreateTableDefinition definition)
    {
        var parsed = ParseAll(batch);
        return parsed.Any(item =>
            string.Equals(item.Table, definition.Table, StringComparison.OrdinalIgnoreCase)
            && string.Equals(item.Schema ?? "", definition.Schema ?? "", StringComparison.OrdinalIgnoreCase));
    }

    private static bool TryFindCreateTable(
        string sql,
        int start,
        out int createStart,
        out int nameStart,
        out int openParen)
    {
        createStart = -1;
        nameStart = -1;
        openParen = -1;
        var i = start;
        while (i < sql.Length)
        {
            SkipTrivia(sql, ref i);
            if (i >= sql.Length)
            {
                return false;
            }

            if (!IsIdentifierStart(sql[i]))
            {
                i++;
                continue;
            }

            var wordStart = i;
            while (i < sql.Length && IsIdentifierPart(sql[i]))
            {
                i++;
            }

            var word = sql[wordStart..i];
            if (!word.Equals("CREATE", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            SkipTrivia(sql, ref i);
            if (i >= sql.Length || !IsIdentifierStart(sql[i]))
            {
                continue;
            }

            var secondStart = i;
            while (i < sql.Length && IsIdentifierPart(sql[i]))
            {
                i++;
            }

            var second = sql[secondStart..i];
            if (!second.Equals("TABLE", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            SkipTrivia(sql, ref i);
            if (MatchKeyword(sql, ref i, "IF"))
            {
                SkipTrivia(sql, ref i);
                MatchKeyword(sql, ref i, "NOT");
                SkipTrivia(sql, ref i);
                MatchKeyword(sql, ref i, "EXISTS");
                SkipTrivia(sql, ref i);
            }

            createStart = wordStart;
            nameStart = i;
            while (i < sql.Length && sql[i] != '(')
            {
                if (sql[i] == '-' && i + 1 < sql.Length && sql[i + 1] == '-')
                {
                    break;
                }

                i++;
            }

            if (i >= sql.Length || sql[i] != '(')
            {
                continue;
            }

            openParen = i;
            return true;
        }

        return false;
    }

    private static List<CreateTableColumn> ParseColumns(string body)
    {
        var columns = new List<CreateTableColumn>();
        foreach (var segment in SplitTopLevel(body))
        {
            var trimmed = segment.Trim().TrimStart('\uFEFF');
            if (trimmed.Length == 0)
            {
                continue;
            }

            var first = ReadIdentifier(trimmed, 0, out var afterName);
            if (first is null || TableConstraintStarts.Contains(first))
            {
                continue;
            }

            var typePart = trimmed[afterName..].Trim();
            var sqlType = ExtractSqlType(typePart);
            if (string.IsNullOrWhiteSpace(sqlType))
            {
                continue;
            }

            columns.Add(new CreateTableColumn(first, sqlType));
        }

        return columns;
    }

    private static string ExtractSqlType(string typePart)
    {
        if (string.IsNullOrWhiteSpace(typePart))
        {
            return "";
        }

        var depth = 0;
        var i = 0;
        while (i < typePart.Length)
        {
            var current = typePart[i];
            if (current == '(')
            {
                depth++;
                i++;
                continue;
            }

            if (current == ')')
            {
                depth = Math.Max(0, depth - 1);
                i++;
                continue;
            }

            if (depth == 0 && IsIdentifierStart(current))
            {
                var start = i;
                while (i < typePart.Length && IsIdentifierPart(typePart[i]))
                {
                    i++;
                }

                var word = typePart[start..i];
                if (TypeTerminators.Contains(word))
                {
                    if (word.Equals("NOT", StringComparison.OrdinalIgnoreCase))
                    {
                        var probe = i;
                        SkipSpaces(typePart, ref probe);
                        if (MatchKeyword(typePart, ref probe, "NULL"))
                        {
                            return (typePart[..start] + "NOT NULL").Trim();
                        }
                    }

                    if (word.Equals("NULL", StringComparison.OrdinalIgnoreCase))
                    {
                        return (typePart[..start] + "NULL").Trim();
                    }

                    return typePart[..start].Trim();
                }

                continue;
            }

            i++;
        }

        return typePart.Trim().TrimEnd(',');
    }

    private static IEnumerable<string> SplitTopLevel(string body)
    {
        var parts = new List<string>();
        var depth = 0;
        var start = 0;
        for (var i = 0; i < body.Length; i++)
        {
            var current = body[i];
            if (current == '(')
            {
                depth++;
            }
            else if (current == ')')
            {
                depth = Math.Max(0, depth - 1);
            }
            else if (current == ',' && depth == 0)
            {
                parts.Add(body[start..i]);
                start = i + 1;
            }
            else if (current == '\'' )
            {
                i = SkipSqlString(body, i);
            }
            else if (current == '[')
            {
                while (i < body.Length)
                {
                    if (body[i] == ']' && i + 1 < body.Length && body[i + 1] == ']')
                    {
                        i += 2;
                        continue;
                    }

                    if (body[i] == ']')
                    {
                        break;
                    }

                    i++;
                }
            }
            else if (current == '"')
            {
                i++;
                while (i < body.Length)
                {
                    if (body[i] == '"' && i + 1 < body.Length && body[i + 1] == '"')
                    {
                        i += 2;
                        continue;
                    }

                    if (body[i] == '"')
                    {
                        break;
                    }

                    i++;
                }
            }
        }

        parts.Add(body[start..]);
        return parts;
    }

    private static int FindMatchingParen(string sql, int openParen)
    {
        var depth = 0;
        for (var i = openParen; i < sql.Length; i++)
        {
            var current = sql[i];
            if (current == '\'')
            {
                i = SkipSqlString(sql, i);
                continue;
            }

            if (current == '(')
            {
                depth++;
            }
            else if (current == ')')
            {
                depth--;
                if (depth == 0)
                {
                    return i;
                }
            }
        }

        return -1;
    }

    private static (string? Schema, string Table) SplitName(string name)
    {
        var cleaned = name.Trim();
        foreach (var marker in new[] { " AS ", "\nAS ", "\tAS " })
        {
            var asIndex = cleaned.IndexOf(marker, StringComparison.OrdinalIgnoreCase);
            if (asIndex > 0)
            {
                cleaned = cleaned[..asIndex].Trim();
            }
        }

        var parts = new List<string>();
        var i = 0;
        while (i < cleaned.Length)
        {
            SkipSpaces(cleaned, ref i);
            if (i >= cleaned.Length)
            {
                break;
            }

            if (cleaned[i] == '.')
            {
                i++;
                continue;
            }

            var identifier = ReadIdentifier(cleaned, i, out i);
            if (identifier is null)
            {
                break;
            }

            parts.Add(identifier);
        }

        return parts.Count >= 2
            ? (parts[^2], parts[^1])
            : (null, parts.Count == 0 ? "" : parts[0]);
    }

    private static string? ReadIdentifier(string text, int start, out int after)
    {
        after = start;
        SkipSpaces(text, ref after);
        if (after >= text.Length)
        {
            return null;
        }

        if (text[after] == '[')
        {
            after++;
            var begin = after;
            while (after < text.Length)
            {
                if (text[after] == ']' && after + 1 < text.Length && text[after + 1] == ']')
                {
                    after += 2;
                    continue;
                }

                if (text[after] == ']')
                {
                    var value = text[begin..after].Replace("]]", "]", StringComparison.Ordinal);
                    after++;
                    return value;
                }

                after++;
            }

            return null;
        }

        if (text[after] == '"')
        {
            after++;
            var begin = after;
            while (after < text.Length)
            {
                if (text[after] == '"' && after + 1 < text.Length && text[after + 1] == '"')
                {
                    after += 2;
                    continue;
                }

                if (text[after] == '"')
                {
                    var value = text[begin..after].Replace("\"\"", "\"", StringComparison.Ordinal);
                    after++;
                    return value;
                }

                after++;
            }

            return null;
        }

        if (!IsIdentifierStart(text[after]))
        {
            return null;
        }

        var wordStart = after;
        while (after < text.Length && IsIdentifierPart(text[after]))
        {
            after++;
        }

        return text[wordStart..after];
    }

    private static void SkipTrivia(string sql, ref int i)
    {
        while (i < sql.Length)
        {
            if (char.IsWhiteSpace(sql[i]))
            {
                i++;
                continue;
            }

            if (sql[i] == '-' && i + 1 < sql.Length && sql[i + 1] == '-')
            {
                i += 2;
                while (i < sql.Length && sql[i] != '\n')
                {
                    i++;
                }

                continue;
            }

            if (sql[i] == '/' && i + 1 < sql.Length && sql[i + 1] == '*')
            {
                i += 2;
                while (i + 1 < sql.Length && !(sql[i] == '*' && sql[i + 1] == '/'))
                {
                    i++;
                }

                i = Math.Min(sql.Length, i + 2);
                continue;
            }

            break;
        }
    }

    private static void SkipSpaces(string text, ref int i)
    {
        while (i < text.Length && char.IsWhiteSpace(text[i]))
        {
            i++;
        }
    }

    private static bool MatchKeyword(string text, ref int i, string keyword)
    {
        SkipSpaces(text, ref i);
        if (i + keyword.Length > text.Length)
        {
            return false;
        }

        if (!text.AsSpan(i, keyword.Length).Equals(keyword, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        var end = i + keyword.Length;
        if (end < text.Length && IsIdentifierPart(text[end]))
        {
            return false;
        }

        i = end;
        return true;
    }

    private static int SkipSqlString(string sql, int quoteIndex)
    {
        var i = quoteIndex + 1;
        while (i < sql.Length)
        {
            if (sql[i] == '\'' && i + 1 < sql.Length && sql[i + 1] == '\'')
            {
                i += 2;
                continue;
            }

            if (sql[i] == '\'')
            {
                return i;
            }

            i++;
        }

        return sql.Length - 1;
    }

    private static bool IsIdentifierStart(char value) =>
        char.IsLetter(value) || value is '_' or '@' or '#';

    private static bool IsIdentifierPart(char value) =>
        char.IsLetterOrDigit(value) || value is '_' or '@' or '#' or '$';
}
