using System.Text.RegularExpressions;

namespace BuildingBlocks.DataAccess.Abstractions;

/// <summary>
/// Resolves and expands stored-procedure names built from templates such as
/// <c>usp_{Area}_{Action}</c> or interpolated call sites <c>$"{area}_{action}"</c>.
/// Prefer enums / named constants for each hole so discovery and runtime stay aligned.
/// </summary>
public static class StoredProcedureName
{
    public const int MaxExpansionCount = 64;

    /// <summary>
    /// Format a template with enum / string / Formattable segments.
    /// Named holes use <c>{EnumTypeName}</c> or <c>{0}</c>-style indexes.
    /// Example: <c>Format("usp_Showcase_{0}_{1}", ShowcaseArea.Billing, ShowcaseAction.Get)</c>.
    /// </summary>
    public static string Format(string template, params object[] segments)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(template);
        if (segments is null || segments.Length == 0)
            return template.Trim();

        var named = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < segments.Length; i++)
        {
            var value = SegmentToToken(segments[i]);
            named[i.ToString()] = value;
            if (segments[i] is Enum e)
                named[e.GetType().Name] = value;
        }

        return Resolve(template, named);
    }

    /// <summary>Replace <c>{Token}</c> holes from a dictionary (case-insensitive keys).</summary>
    public static string Resolve(string template, IReadOnlyDictionary<string, string> tokens)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(template);
        ArgumentNullException.ThrowIfNull(tokens);

        var result = StripInterpolationDecorators(template);
        foreach (Match m in HoleRegex.Matches(result).Cast<Match>().Reverse())
        {
            var key = m.Groups["name"].Value;
            if (!tokens.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
                continue;
            result = result.Remove(m.Index, m.Length).Insert(m.Index, SanitizeToken(value));
        }

        return NormalizeProcedureName(result);
    }

    /// <summary>
    /// Cartesian expansion of a template for discovery / SQL stub generation.
    /// Caps at <see cref="MaxExpansionCount"/> combinations.
    /// </summary>
    public static IReadOnlyList<string> Expand(
        string template,
        IReadOnlyDictionary<string, IReadOnlyList<string>> tokenValues)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(template);
        ArgumentNullException.ThrowIfNull(tokenValues);

        var normalizedTemplate = StripInterpolationDecorators(template);
        var holes = HoleRegex.Matches(normalizedTemplate)
            .Select(m => m.Groups["name"].Value)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        if (holes.Count == 0)
            return [NormalizeProcedureName(normalizedTemplate)];

        IEnumerable<IReadOnlyDictionary<string, string>> combos = [new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)];
        foreach (var hole in holes)
        {
            if (!TryGetTokenValues(tokenValues, hole, out var values) || values.Count == 0)
                return [NormalizeProcedureName(normalizedTemplate)];

            combos = combos.SelectMany(prefix => values.Select(v =>
            {
                var next = new Dictionary<string, string>(prefix, StringComparer.OrdinalIgnoreCase)
                {
                    [hole] = SanitizeToken(v)
                };
                return (IReadOnlyDictionary<string, string>)next;
            }));
        }

        return combos
            .Take(MaxExpansionCount)
            .Select(t => Resolve(normalizedTemplate, t))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Convert a C# interpolated-string source text into a <c>{hole}</c> template when possible.
    /// Example: <c>$"usp_{area}_{action}"</c> → <c>usp_{area}_{action}</c>.
    /// </summary>
    public static string? TryParseInterpolatedTemplate(string interpolatedSource)
    {
        if (string.IsNullOrWhiteSpace(interpolatedSource))
            return null;

        var text = interpolatedSource.Trim();
        if (text.StartsWith('$'))
            text = text[1..].Trim();
        if (text.Length < 2 || text[0] is not ('"' or '@'))
            return null;

        // Cheap parse for $"..." / $@"..." with {expr} holes (no nested braces).
        var start = text.IndexOf('"');
        var end = text.LastIndexOf('"');
        if (start < 0 || end <= start)
            return null;

        var content = text[(start + 1)..end];
        var sb = new System.Text.StringBuilder(content.Length);
        for (var i = 0; i < content.Length; i++)
        {
            var ch = content[i];
            if (ch == '{' && i + 1 < content.Length && content[i + 1] == '{')
            {
                sb.Append('{');
                i++;
                continue;
            }

            if (ch == '}' && i + 1 < content.Length && content[i + 1] == '}')
            {
                sb.Append('}');
                i++;
                continue;
            }

            if (ch == '{')
            {
                var close = content.IndexOf('}', i + 1);
                if (close < 0)
                    return null;
                var expr = content[(i + 1)..close].Trim();
                var hole = SimplifyHoleExpression(expr);
                if (string.IsNullOrWhiteSpace(hole))
                    return null;
                sb.Append('{').Append(hole).Append('}');
                i = close;
                continue;
            }

            sb.Append(ch);
        }

        var template = sb.ToString();
        return LooksLikeProcedureTemplate(template) ? template : null;
    }

    public static bool LooksLikeProcedureTemplate(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;
        if (value.Contains(' ', StringComparison.Ordinal))
            return false;
        if (value.Contains("SELECT", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("INSERT", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("UPDATE", StringComparison.OrdinalIgnoreCase) ||
            value.Contains("DELETE", StringComparison.OrdinalIgnoreCase))
            return false;

        return value.Contains('{') ||
               value.Contains("usp_", StringComparison.OrdinalIgnoreCase) ||
               value.Contains("sp_", StringComparison.OrdinalIgnoreCase) ||
               value.Contains('_') ||
               System.Text.RegularExpressions.Regex.IsMatch(value, @"^[\w\.\[\]]+$");
    }

    public static string NormalizeProcedureName(string value) =>
        value.Replace("[", "", StringComparison.Ordinal)
             .Replace("]", "", StringComparison.Ordinal)
             .Replace("\"", "", StringComparison.Ordinal)
             .Trim();

    private static bool TryGetTokenValues(
        IReadOnlyDictionary<string, IReadOnlyList<string>> tokenValues,
        string hole,
        out IReadOnlyList<string> values)
    {
        if (tokenValues.TryGetValue(hole, out values!))
            return true;

        foreach (var pair in tokenValues)
        {
            if (pair.Key.Equals(hole, StringComparison.OrdinalIgnoreCase) ||
                pair.Key.EndsWith(hole, StringComparison.OrdinalIgnoreCase) ||
                hole.EndsWith(pair.Key, StringComparison.OrdinalIgnoreCase))
            {
                values = pair.Value;
                return true;
            }
        }

        values = Array.Empty<string>();
        return false;
    }

    private static string SegmentToToken(object segment) => segment switch
    {
        null => "",
        Enum e => e.ToString(),
        string s => SanitizeToken(s),
        _ => SanitizeToken(Convert.ToString(segment) ?? "")
    };

    private static string SanitizeToken(string value)
    {
        var trimmed = value.Trim().Trim('"', '\'');
        // Enum-like / identifier tokens only — reject SQL fragments.
        return System.Text.RegularExpressions.Regex.Replace(trimmed, @"[^\w]", "");
    }

    private static string StripInterpolationDecorators(string template)
    {
        var text = template.Trim();
        if (text.StartsWith('$'))
            text = text[1..].Trim();
        if ((text.StartsWith("\"") && text.EndsWith("\"")) || (text.StartsWith("@\"") && text.EndsWith("\"")))
            text = text.Trim('@').Trim('"');
        return text;
    }

    private static string SimplifyHoleExpression(string expr)
    {
        // area.ToString() / nameof(Area.Billing) / (ShowcaseArea)x → Area / ShowcaseArea
        expr = expr.Trim();
        var nameofMatch = System.Text.RegularExpressions.Regex.Match(expr, @"nameof\s*\(\s*(?<t>\w+)");
        if (nameofMatch.Success)
            return nameofMatch.Groups["t"].Value;

        var castMatch = System.Text.RegularExpressions.Regex.Match(expr, @"\(\s*(?<t>\w+)\s*\)");
        if (castMatch.Success)
            return castMatch.Groups["t"].Value;

        var member = expr.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).LastOrDefault() ?? expr;
        member = member.Replace("ToString()", "", StringComparison.OrdinalIgnoreCase).Trim().Trim('(', ')');
        return System.Text.RegularExpressions.Regex.Replace(member, @"[^\w]", "");
    }

    private static readonly System.Text.RegularExpressions.Regex HoleRegex = new(
        @"\{(?<name>[A-Za-z_][A-Za-z0-9_]*)\}",
        System.Text.RegularExpressions.RegexOptions.Compiled);
}
