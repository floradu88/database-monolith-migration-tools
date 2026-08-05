using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace FindingsMigration.Core;

/// <summary>
/// Advisory domain suggestions from Graphify communities + source path prefixes.
/// Does not package or approve ownership — operator still passes --domain explicitly.
/// </summary>
public sealed class DomainSuggestionService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public DomainSuggestionResult SuggestFromGraphFile(string graphJsonPath, int minNodesPerDomain = 3)
    {
        if (!File.Exists(graphJsonPath))
            throw new FileNotFoundException("graph.json not found", graphJsonPath);

        var doc = JsonSerializer.Deserialize<GraphDocument>(File.ReadAllText(graphJsonPath), JsonOptions)
                  ?? new GraphDocument();
        return Suggest(doc, minNodesPerDomain);
    }

    public DomainSuggestionResult Suggest(GraphDocument graph, int minNodesPerDomain = 3)
    {
        var buckets = new Dictionary<string, DomainBucket>(StringComparer.OrdinalIgnoreCase);

        foreach (var node in graph.Nodes ?? [])
        {
            var community = NormalizeCommunity(node.Community);
            var pathHint = PathPrefixHint(node.SourceFile ?? node.Source_File);
            var key = !string.IsNullOrWhiteSpace(pathHint)
                ? pathHint!
                : (!string.IsNullOrWhiteSpace(community) ? $"Community_{community}" : "Unassigned");

            if (!buckets.TryGetValue(key, out var bucket))
            {
                bucket = new DomainBucket(key);
                buckets[key] = bucket;
            }

            bucket.NodeCount++;
            if (!string.IsNullOrWhiteSpace(community))
                bucket.Communities.Add(community);
            if (!string.IsNullOrWhiteSpace(node.Kind))
                bucket.Kinds.Add(node.Kind!);
            if (!string.IsNullOrWhiteSpace(pathHint))
                bucket.PathPrefixes.Add(pathHint!);
        }

        var suggestions = buckets.Values
            .Where(b => b.NodeCount >= minNodesPerDomain)
            .OrderByDescending(b => b.NodeCount)
            .Select(b => new DomainSuggestion
            {
                ProposedDomain = ToDomainName(b.Key),
                NodeCount = b.NodeCount,
                Communities = b.Communities.OrderBy(x => x).ToList(),
                PathPrefixes = b.PathPrefixes.OrderBy(x => x).ToList(),
                SampleKinds = b.Kinds.OrderBy(x => x).Take(8).ToList(),
                Rationale = BuildRationale(b)
            })
            .ToList();

        return new DomainSuggestionResult
        {
            GeneratedAt = DateTimeOffset.UtcNow,
            SuggestionCount = suggestions.Count,
            Suggestions = suggestions,
            Note = "Advisory only. Confirm ownership, then run findings-migrate --domain <Name> --code-to-db ..."
        };
    }

    private static string BuildRationale(DomainBucket b)
    {
        var parts = new List<string> { $"{b.NodeCount} nodes" };
        if (b.Communities.Count > 0)
            parts.Add($"communities: {string.Join(", ", b.Communities.OrderBy(x => x).Take(5))}");
        if (b.PathPrefixes.Count > 0)
            parts.Add($"paths: {string.Join(", ", b.PathPrefixes.OrderBy(x => x).Take(5))}");
        return string.Join("; ", parts);
    }

    private static string ToDomainName(string key)
    {
        var cleaned = Regex.Replace(key, @"[^A-Za-z0-9_]+", "_").Trim('_');
        if (cleaned.StartsWith("Community_", StringComparison.OrdinalIgnoreCase))
            cleaned = "Domain_" + cleaned["Community_".Length..];
        if (string.IsNullOrWhiteSpace(cleaned))
            return "Unassigned";
        // Pascal-ish: Billing, Onboarding
        return string.Concat(cleaned.Split('_', StringSplitOptions.RemoveEmptyEntries)
            .Select(s => char.ToUpperInvariant(s[0]) + s[1..].ToLowerInvariant()));
    }

    private static string? NormalizeCommunity(object? community)
    {
        if (community is null) return null;
        if (community is JsonElement el)
        {
            return el.ValueKind switch
            {
                JsonValueKind.String => el.GetString(),
                JsonValueKind.Number => el.ToString(),
                _ => el.ToString()
            };
        }

        return Convert.ToString(community);
    }

    private static string? PathPrefixHint(string? sourceFile)
    {
        if (string.IsNullOrWhiteSpace(sourceFile)) return null;
        var normalized = sourceFile.Replace('\\', '/');
        var parts = normalized.Split('/', StringSplitOptions.RemoveEmptyEntries);
        // Prefer Features/{Name}, Domains/{Name}, Services/{Name}
        for (var i = 0; i < parts.Length - 1; i++)
        {
            if (parts[i] is "Features" or "Domain" or "Domains" or "Services" or "Modules")
                return parts[i + 1];
        }

        // Fallback: first non-generic folder
        foreach (var p in parts)
        {
            if (p is "src" or "Src" or "app" or "App" or "bin" or "obj") continue;
            if (p.EndsWith(".cs", StringComparison.OrdinalIgnoreCase)) continue;
            return p;
        }

        return null;
    }

    private sealed class DomainBucket(string key)
    {
        public string Key { get; } = key;
        public int NodeCount { get; set; }
        public HashSet<string> Communities { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> PathPrefixes { get; } = new(StringComparer.OrdinalIgnoreCase);
        public HashSet<string> Kinds { get; } = new(StringComparer.OrdinalIgnoreCase);
    }
}

public sealed class GraphDocument
{
    [JsonPropertyName("nodes")]
    public List<GraphNodeLite>? Nodes { get; set; }
}

public sealed class GraphNodeLite
{
    public string? Id { get; set; }
    public string? Label { get; set; }
    public string? Kind { get; set; }
    public object? Community { get; set; }
    public string? SourceFile { get; set; }
    [JsonPropertyName("source_file")]
    public string? Source_File { get; set; }
}

public sealed class DomainSuggestionResult
{
    public DateTimeOffset GeneratedAt { get; set; }
    public int SuggestionCount { get; set; }
    public List<DomainSuggestion> Suggestions { get; set; } = [];
    public string Note { get; set; } = "";
}

public sealed class DomainSuggestion
{
    public string ProposedDomain { get; set; } = "";
    public int NodeCount { get; set; }
    public List<string> Communities { get; set; } = [];
    public List<string> PathPrefixes { get; set; } = [];
    public List<string> SampleKinds { get; set; } = [];
    public string Rationale { get; set; } = "";
}
