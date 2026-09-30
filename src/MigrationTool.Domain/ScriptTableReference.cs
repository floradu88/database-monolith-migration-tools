using System.Text.RegularExpressions;

namespace MigrationTool.Domain;

public static partial class ScriptTableReference
{
    [GeneratedRegex(
        """
        \b(?:INSERT\s+INTO|UPDATE|DELETE\s+FROM|FROM|JOIN)\s+(?<name>(?:\[[^\]]+\]|"[^"]+"|[A-Za-z_][\w$]*)(?:\s*\.\s*(?:\[[^\]]+\]|"[^"]+"|[A-Za-z_][\w$]*))?)
        """,
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex TableRef();

    public static (string? Schema, string? Table) Find(string sql, string? column)
    {
        var matches = TableRef().Matches(sql);
        if (matches.Count == 0)
        {
            return (null, null);
        }

        if (!string.IsNullOrWhiteSpace(column))
        {
            foreach (Match match in matches)
            {
                var windowStart = Math.Max(0, match.Index - 40);
                var windowLength = Math.Min(sql.Length - windowStart, match.Length + 500);
                var window = sql.Substring(windowStart, windowLength);
                if (window.Contains(column, StringComparison.OrdinalIgnoreCase))
                {
                    return Split(match.Groups["name"].Value);
                }
            }
        }

        return Split(matches[0].Groups["name"].Value);
    }

    private static (string? Schema, string? Table) Split(string name)
    {
        var parts = Regex.Split(name.Trim(), @"\s*\.\s*");
        if (parts.Length >= 2)
        {
            return (Unquote(parts[0]), Unquote(parts[1]));
        }

        return (null, Unquote(parts[0]));
    }

    private static string Unquote(string value)
    {
        value = value.Trim();
        if (value.Length >= 2 && value[0] == '[' && value[^1] == ']')
        {
            return value[1..^1].Replace("]]", "]", StringComparison.Ordinal);
        }

        if (value.Length >= 2 && value[0] == '"' && value[^1] == '"')
        {
            return value[1..^1].Replace("\"\"", "\"", StringComparison.Ordinal);
        }

        return value;
    }
}
