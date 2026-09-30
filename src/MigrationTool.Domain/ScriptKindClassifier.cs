using System.Text;

namespace MigrationTool.Domain;

public static class ScriptKindClassifier
{
    public static ScriptKind Classify(string relativePath, string contents)
    {
        foreach (var segment in relativePath.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries))
        {
            if (TryParse(segment, out var fromFolder))
            {
                return fromFolder;
            }
        }

        foreach (var raw in contents.Split('\n'))
        {
            var line = raw.Trim().TrimStart('\uFEFF');
            if (line.Length == 0)
            {
                continue;
            }

            const string prefix = "-- kind:";
            if (line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                if (TryParse(line[prefix.Length..].Trim(), out var fromComment))
                {
                    return fromComment;
                }
            }

            break;
        }

        var token = FirstWord(StripComments(contents));
        return token.ToUpperInvariant() switch
        {
            "CREATE" or "ALTER" or "DROP" or "TRUNCATE" => ScriptKind.Ddl,
            "INSERT" or "UPDATE" or "DELETE" or "MERGE" => ScriptKind.Dml,
            "SELECT" or "WITH" => ScriptKind.Query,
            _ => ScriptKind.Dml
        };
    }

    public static bool TryParse(string? value, out ScriptKind kind)
    {
        kind = value?.Trim().ToLowerInvariant() switch
        {
            "ddl" => ScriptKind.Ddl,
            "dml" => ScriptKind.Dml,
            "data" => ScriptKind.Data,
            "query" => ScriptKind.Query,
            _ => default
        };

        return value is not null && value.Trim().ToLowerInvariant() is "ddl" or "dml" or "data" or "query";
    }

    private static string StripComments(string sql)
    {
        var builder = new StringBuilder(sql.Length);
        var inLine = false;
        var inBlock = false;
        for (var i = 0; i < sql.Length; i++)
        {
            var current = sql[i];
            var next = i + 1 < sql.Length ? sql[i + 1] : '\0';
            if (inLine)
            {
                if (current == '\n')
                {
                    inLine = false;
                    builder.Append(current);
                }

                continue;
            }

            if (inBlock)
            {
                if (current == '*' && next == '/')
                {
                    inBlock = false;
                    i++;
                }

                continue;
            }

            if (current == '-' && next == '-')
            {
                inLine = true;
                i++;
                continue;
            }

            if (current == '/' && next == '*')
            {
                inBlock = true;
                i++;
                continue;
            }

            builder.Append(current);
        }

        return builder.ToString();
    }

    private static string FirstWord(string sql)
    {
        var span = sql.AsSpan().Trim();
        var length = 0;
        while (length < span.Length && char.IsLetter(span[length]))
        {
            length++;
        }

        return length == 0 ? "" : span[..length].ToString();
    }
}
