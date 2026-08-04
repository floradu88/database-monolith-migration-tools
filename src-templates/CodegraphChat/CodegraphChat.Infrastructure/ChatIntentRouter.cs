using System.Text.RegularExpressions;

namespace CodegraphChat.Infrastructure;

public enum ChatIntent
{
    Query,
    Callers,
    Callees,
    Impact,
    Status,
    Files
}

public sealed record IntentResult(ChatIntent Intent, string? Symbol, string Topic);

public static class ChatIntentRouter
{
    private static readonly Regex Quoted = new(@"[""']([^""']+)[""']", RegexOptions.Compiled);
    private static readonly Regex SymbolLike = new(@"\b([A-Z][A-Za-z0-9_]*(?:\.[A-Z][A-Za-z0-9_]*)?)\b", RegexOptions.Compiled);

    public static IntentResult Route(string message, string? modeOverride = null)
    {
        var text = (message ?? string.Empty).Trim();
        if (!string.IsNullOrWhiteSpace(modeOverride) &&
            Enum.TryParse<ChatIntent>(modeOverride.Trim(), ignoreCase: true, out var forced))
        {
            return new IntentResult(forced, ExtractSymbol(text), text);
        }

        var lower = text.ToLowerInvariant();

        if (ContainsAny(lower, "index status", "codegraph status", "is the index", "index ready", "how big is the index"))
            return new IntentResult(ChatIntent.Status, null, text);

        if (ContainsAny(lower, "file structure", "project files", "list files", "show files", "directory tree"))
            return new IntentResult(ChatIntent.Files, null, text);

        if (ContainsAny(lower, "who calls", "callers of", "what calls", "called by", "who uses"))
            return new IntentResult(ChatIntent.Callers, ExtractSymbol(text), text);

        if (ContainsAny(lower, "callees of", "calls what", "what does it call", "dependencies of")
            || System.Text.RegularExpressions.Regex.IsMatch(lower, @"what does\s+\S+\s+call"))
            return new IntentResult(ChatIntent.Callees, ExtractSymbol(text), text);

        if (ContainsAny(lower, "impact of", "blast radius", "affected by", "what breaks", "change impact"))
            return new IntentResult(ChatIntent.Impact, ExtractSymbol(text), text);

        return new IntentResult(ChatIntent.Query, ExtractSymbol(text) ?? ExtractTopicToken(text), text);
    }

    public static string? ExtractSymbol(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return null;

        var quoted = Quoted.Match(text);
        if (quoted.Success)
            return quoted.Groups[1].Value.Trim();

        // Prefer PascalCase / dotted type names over stopwords.
        foreach (Match m in SymbolLike.Matches(text))
        {
            var candidate = m.Groups[1].Value;
            if (IsStopword(candidate))
                continue;
            return candidate;
        }

        return ExtractTopicToken(text);
    }

    private static string? ExtractTopicToken(string text)
    {
        var parts = text.Split([' ', '\t', '\r', '\n', ',', '?', '!', '.', ';', ':'], StringSplitOptions.RemoveEmptyEntries);
        foreach (var part in parts.Reverse())
        {
            var cleaned = part.Trim('"', '\'', '`');
            if (cleaned.Length < 2 || IsStopword(cleaned))
                continue;
            if (cleaned.All(c => char.IsLetterOrDigit(c) || c is '_' or '.' or '-'))
                return cleaned;
        }

        return null;
    }

    private static bool ContainsAny(string haystack, params string[] needles) =>
        needles.Any(n => haystack.Contains(n, StringComparison.Ordinal));

    private static bool IsStopword(string value)
    {
        return value.Equals("How", StringComparison.OrdinalIgnoreCase)
            || value.Equals("What", StringComparison.OrdinalIgnoreCase)
            || value.Equals("Who", StringComparison.OrdinalIgnoreCase)
            || value.Equals("Where", StringComparison.OrdinalIgnoreCase)
            || value.Equals("Show", StringComparison.OrdinalIgnoreCase)
            || value.Equals("Find", StringComparison.OrdinalIgnoreCase)
            || value.Equals("Tell", StringComparison.OrdinalIgnoreCase)
            || value.Equals("About", StringComparison.OrdinalIgnoreCase)
            || value.Equals("The", StringComparison.OrdinalIgnoreCase)
            || value.Equals("Does", StringComparison.OrdinalIgnoreCase)
            || value.Equals("Call", StringComparison.OrdinalIgnoreCase)
            || value.Equals("Calls", StringComparison.OrdinalIgnoreCase)
            || value.Equals("Caller", StringComparison.OrdinalIgnoreCase)
            || value.Equals("Callers", StringComparison.OrdinalIgnoreCase)
            || value.Equals("Callee", StringComparison.OrdinalIgnoreCase)
            || value.Equals("Callees", StringComparison.OrdinalIgnoreCase)
            || value.Equals("Impact", StringComparison.OrdinalIgnoreCase)
            || value.Equals("Status", StringComparison.OrdinalIgnoreCase)
            || value.Equals("Files", StringComparison.OrdinalIgnoreCase)
            || value.Equals("Codegraph", StringComparison.OrdinalIgnoreCase)
            || value.Equals("Index", StringComparison.OrdinalIgnoreCase)
            || value.Equals("Project", StringComparison.OrdinalIgnoreCase)
            || value.Equals("Topic", StringComparison.OrdinalIgnoreCase);
    }
}
