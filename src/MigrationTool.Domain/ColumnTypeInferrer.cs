using System.Text;
using System.Text.RegularExpressions;

namespace MigrationTool.Domain;

public static partial class ColumnTypeInferrer
{
    [GeneratedRegex(
        """
        \bCAST\s*\(\s*(?:\[(?<c>[^\]]+)\]|"(?<c>[^"]+)"|(?<c>[A-Za-z_][\w$]*))\s+AS\s+(?<t>[A-Za-z][A-Za-z0-9_]*(?:\s*\(\s*\d+\s*(?:,\s*\d+\s*)?\))?)\s*\)
        """,
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex CastPattern();

    [GeneratedRegex(
        """
        \bCONVERT\s*\(\s*(?<t>[A-Za-z][A-Za-z0-9_]*(?:\s*\(\s*\d+\s*(?:,\s*\d+\s*)?\))?)\s*,\s*(?:\[(?<c>[^\]]+)\]|"(?<c>[^"]+)"|(?<c>[A-Za-z_][\w$]*))
        """,
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex ConvertPattern();

    [GeneratedRegex(
        """
        \bSET\s+(?:\[(?<c>[^\]]+)\]|"(?<c>[^"]+)"|(?<c>[A-Za-z_][\w$]*))\s*=\s*(?<lit>N?'(?:''|[^'])*'|-?\d+(?:\.\d+)?|(?i:true|false))\b
        """,
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex SetPattern();

    [GeneratedRegex(
        @"\bINSERT\s+INTO\s+[^(]+\((?<cols>[^)]+)\)\s*VALUES\s*\((?<vals>[^)]+)\)",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex InsertPattern();

    public static InferredColumnType Infer(DatabaseProviderKind provider, string sql, string column)
    {
        var cast = FindTyped(CastPattern().Matches(sql), column) ?? FindTyped(ConvertPattern().Matches(sql), column);
        if (cast is not null)
        {
            return new InferredColumnType(Normalize(provider, cast), "inferred from CAST or CONVERT in the failing script");
        }

        var literal = FindLiteral(sql, column);
        if (literal is not null)
        {
            var mapped = MapLiteral(provider, literal);
            if (mapped is not null)
            {
                return new InferredColumnType(mapped, "inferred from a literal in the failing script");
            }
        }

        var fallback = provider == DatabaseProviderKind.PostgreSql ? "text" : "nvarchar(max)";
        return new InferredColumnType(fallback, $"inferred as {fallback} because the script had no CAST or literal");
    }

    private static string? FindTyped(MatchCollection matches, string column)
    {
        foreach (Match match in matches)
        {
            if (NameEquals(match.Groups["c"].Value, column))
            {
                return Regex.Replace(match.Groups["t"].Value, @"\s+", "");
            }
        }

        return null;
    }

    private static string? FindLiteral(string sql, string column)
    {
        foreach (Match match in SetPattern().Matches(sql))
        {
            if (NameEquals(match.Groups["c"].Value, column))
            {
                return match.Groups["lit"].Value;
            }
        }

        foreach (Match match in InsertPattern().Matches(sql))
        {
            var columns = SplitArgs(match.Groups["cols"].Value);
            var values = SplitArgs(match.Groups["vals"].Value);
            for (var i = 0; i < columns.Count && i < values.Count; i++)
            {
                if (NameEquals(Unquote(columns[i]), column))
                {
                    return values[i];
                }
            }
        }

        return null;
    }

    private static string? MapLiteral(DatabaseProviderKind provider, string literal)
    {
        var value = literal.Trim();
        if (value.Equals("true", StringComparison.OrdinalIgnoreCase) || value.Equals("false", StringComparison.OrdinalIgnoreCase))
        {
            return provider == DatabaseProviderKind.PostgreSql ? "boolean" : "bit";
        }

        if (value.StartsWith("N'", StringComparison.OrdinalIgnoreCase) || value.StartsWith('\''))
        {
            return provider == DatabaseProviderKind.PostgreSql ? "text" : "nvarchar(max)";
        }

        if (Regex.IsMatch(value, @"^-?\d+$"))
        {
            return provider == DatabaseProviderKind.PostgreSql ? "integer" : "int";
        }

        if (Regex.IsMatch(value, @"^-?\d+\.\d+$"))
        {
            return provider == DatabaseProviderKind.PostgreSql ? "numeric(18,4)" : "decimal(18,4)";
        }

        return null;
    }

    private static string Normalize(DatabaseProviderKind provider, string sqlType)
    {
        if (provider == DatabaseProviderKind.SqlServer)
        {
            return sqlType;
        }

        return sqlType.ToLowerInvariant() switch
        {
            "nvarchar(max)" or "nvarchar" or "varchar" or "varchar(max)" or "ntext" => "text",
            "bit" => "boolean",
            "datetime" or "datetime2" or "datetimeoffset" => "timestamp",
            "int" => "integer",
            "decimal(18,4)" => "numeric(18,4)",
            _ => sqlType
        };
    }

    private static bool NameEquals(string candidate, string column) =>
        Unquote(candidate).Equals(column, StringComparison.OrdinalIgnoreCase);

    private static string Unquote(string value)
    {
        value = value.Trim();
        if (value.Length >= 2 && ((value[0] == '[' && value[^1] == ']') || (value[0] == '"' && value[^1] == '"')))
        {
            return value[1..^1];
        }

        return value;
    }

    private static List<string> SplitArgs(string text)
    {
        var parts = new List<string>();
        var current = new StringBuilder();
        var quote = '\0';
        foreach (var ch in text)
        {
            if (quote == '\0' && ch is '\'' or '"')
            {
                quote = ch;
                current.Append(ch);
                continue;
            }

            if (quote != '\0')
            {
                current.Append(ch);
                if (ch == quote)
                {
                    quote = '\0';
                }

                continue;
            }

            if (ch == ',')
            {
                parts.Add(current.ToString().Trim());
                current.Clear();
                continue;
            }

            current.Append(ch);
        }

        if (current.Length > 0)
        {
            parts.Add(current.ToString().Trim());
        }

        return parts;
    }
}
