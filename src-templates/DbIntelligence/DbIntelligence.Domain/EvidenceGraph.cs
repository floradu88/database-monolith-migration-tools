namespace DbIntelligence.Domain;

public enum NodeKind
{
    File,
    Type,
    Method,
    Application,
    Database,
    Schema,
    Table,
    View,
    StoredProcedure,
    Function,
    Trigger,
    Job,
    Concept
}

public enum EdgeRelation
{
    Calls,
    Imports,
    Uses,
    Reads,
    Writes,
    Executes,
    DependsOn,
    Owns,
    MigratesTo
}

public enum Confidence
{
    Extracted,
    Inferred,
    Ambiguous
}

public sealed class EvidenceGraph
{
    public List<GraphNode> Nodes { get; } = [];
    public List<GraphEdge> Edges { get; } = [];
    public GraphMeta Meta { get; set; } = new();

    public GraphNode? FindNode(string id) =>
        Nodes.FirstOrDefault(n => string.Equals(n.Id, id, StringComparison.OrdinalIgnoreCase));

    public void UpsertNode(GraphNode node)
    {
        var existing = FindNode(node.Id);
        if (existing is null)
        {
            Nodes.Add(node);
            return;
        }

        existing.Label = string.IsNullOrWhiteSpace(node.Label) ? existing.Label : node.Label;
        existing.Kind = node.Kind;
        existing.SourceFile ??= node.SourceFile;
        existing.SourceLocation ??= node.SourceLocation;
        existing.Community ??= node.Community;
        existing.Schema ??= node.Schema;
        existing.Database ??= node.Database;
        foreach (var kv in node.Properties)
            existing.Properties[kv.Key] = kv.Value;
    }

    public void UpsertEdge(GraphEdge edge)
    {
        var existing = Edges.FirstOrDefault(e =>
            string.Equals(e.Source, edge.Source, StringComparison.OrdinalIgnoreCase) &&
            string.Equals(e.Target, edge.Target, StringComparison.OrdinalIgnoreCase) &&
            e.Relation == edge.Relation);

        if (existing is null)
        {
            if (edge.Evidence is not null && edge.Locations.Count == 0)
                edge.Locations.Add(CloneEvidence(edge.Evidence));
            Edges.Add(edge);
            return;
        }

        // Prefer stronger confidence (Extracted < Inferred < Ambiguous).
        if (edge.Confidence < existing.Confidence)
            existing.Confidence = edge.Confidence;

        existing.Evidence ??= edge.Evidence;
        foreach (var loc in EnumerateIncomingLocations(edge))
            AddLocationIfNew(existing, loc);
    }

    private static IEnumerable<EdgeEvidence> EnumerateIncomingLocations(GraphEdge edge)
    {
        foreach (var loc in edge.Locations)
            yield return loc;
        if (edge.Evidence is not null)
            yield return edge.Evidence;
    }

    private static void AddLocationIfNew(GraphEdge edge, EdgeEvidence candidate)
    {
        if (string.IsNullOrWhiteSpace(candidate.File) && candidate.Line is null)
            return;

        var exists = edge.Locations.Any(l =>
            string.Equals(l.File, candidate.File, StringComparison.OrdinalIgnoreCase) &&
            l.Line == candidate.Line);
        if (exists)
            return;

        edge.Locations.Add(CloneEvidence(candidate));
        edge.Evidence ??= CloneEvidence(candidate);
    }

    private static EdgeEvidence CloneEvidence(EdgeEvidence e) => new()
    {
        File = e.File,
        Line = e.Line,
        Pattern = e.Pattern,
        RawReference = e.RawReference
    };
}

public sealed class GraphNode
{
    public required string Id { get; set; }
    public required string Label { get; set; }
    public NodeKind Kind { get; set; }
    public string? SourceFile { get; set; }
    public string? SourceLocation { get; set; }
    public string? Community { get; set; }
    public string? Schema { get; set; }
    public string? Database { get; set; }
    public Dictionary<string, string> Properties { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

public sealed class GraphEdge
{
    public required string Source { get; set; }
    public required string Target { get; set; }
    public EdgeRelation Relation { get; set; }
    public Confidence Confidence { get; set; } = Confidence.Extracted;
    /// <summary>Primary / first evidence location (backward compatible).</summary>
    public EdgeEvidence? Evidence { get; set; }
    /// <summary>All file:line locations for this edge (duplicates collapsed by full path + line).</summary>
    public List<EdgeEvidence> Locations { get; set; } = [];
}

public sealed class EdgeEvidence
{
    public string? File { get; set; }
    public int? Line { get; set; }
    public string? Pattern { get; set; }
    public string? RawReference { get; set; }
}

public sealed class GraphMeta
{
    public DateTimeOffset GeneratedAt { get; set; } = DateTimeOffset.UtcNow;
    public List<string> Sources { get; set; } = [];
    public string? TargetRepositoryPath { get; set; }
}

public sealed class CodeReferenceFinding
{
    public required string RepositoryPath { get; set; }
    public required string FilePath { get; set; }
    public string? TypeName { get; set; }
    public string? MemberName { get; set; }
    public int Line { get; set; }
    public required string RawReference { get; set; }
    public required string NormalizedObjectName { get; set; }
    public EdgeRelation AccessType { get; set; }
    public bool IsDynamic { get; set; }
    public Confidence Confidence { get; set; }
    public string Pattern { get; set; } = string.Empty;
}

public static class GraphIds
{
    public static string CodeMethod(string typeName, string memberName) =>
        $"code:{Sanitize(typeName)}.{Sanitize(memberName)}";

    public static string CodeType(string typeName) =>
        $"code:{Sanitize(typeName)}";

    public static string DbObject(string? database, string? schema, string name, NodeKind kind)
    {
        var db = string.IsNullOrWhiteSpace(database) ? "default" : Sanitize(database);
        var sch = string.IsNullOrWhiteSpace(schema) ? "dbo" : Sanitize(schema);
        return $"db:{db}.{sch}.{Sanitize(name)}:{kind}";
    }

    public static string Concept(string label) =>
        $"concept:{Sanitize(label)}";

    private static string Sanitize(string value) =>
        value.Trim().Replace('\\', '/').Replace(' ', '_');
}
