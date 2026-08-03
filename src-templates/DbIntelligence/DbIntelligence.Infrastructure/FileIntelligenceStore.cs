using System.Collections.Concurrent;
using System.Net;
using System.Text;
using System.Text.Json;
using DbIntelligence.Contracts;
using DbIntelligence.Domain;
using Microsoft.Extensions.Options;

namespace DbIntelligence.Infrastructure;

/// <summary>
/// Process-local store: the live evidence graph and index jobs live <b>in memory</b>.
/// Persistence is optional file export only (JSON / markdown / HTML under <c>.db-index</c>) —
/// there is no database-backed catalog yet.
/// </summary>
public interface IIntelligenceStore
{
    EvidenceGraph? CurrentGraph { get; }
    void SetGraph(EvidenceGraph graph);
    IReadOnlyList<SearchResultDto> Search(string query, int limit = 50);
    IndexJobStatusDto CreateJob(IndexJobRequest request);
    IndexJobStatusDto? GetJob(string id);
    void UpdateJob(IndexJobStatusDto job);
    BatchIndexJobStatusDto CreateBatchJob(BatchIndexRequest request);
    BatchIndexJobStatusDto? GetBatchJob(string id);
    void UpdateBatchJob(BatchIndexJobStatusDto job);
    Task ExportAsync(EvidenceGraph graph, string outputDirectory, CancellationToken cancellationToken = default);
}

/// <summary>
/// In-memory graph + job dictionary. Named "File" because <see cref="ExportAsync"/>
/// writes maps to disk; the API serves the in-process graph until restart.
/// </summary>
public sealed class FileIntelligenceStore : IIntelligenceStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    private readonly ConcurrentDictionary<string, IndexJobStatusDto> _jobs = new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, BatchIndexJobStatusDto> _batchJobs = new(StringComparer.OrdinalIgnoreCase);
    private readonly DbIntelligenceOptions _options;
    private readonly EvidenceGraphMerger _merger;
    private readonly object _graphLock = new();
    /// <summary>Live unified graph for this API process only (cleared on restart).</summary>
    private EvidenceGraph? _graph;

    public FileIntelligenceStore(IOptions<DbIntelligenceOptions> options, EvidenceGraphMerger merger)
    {
        _options = options.Value;
        _merger = merger;
    }

    public EvidenceGraph? CurrentGraph
    {
        get
        {
            lock (_graphLock)
                return _graph;
        }
    }

    public void SetGraph(EvidenceGraph graph)
    {
        lock (_graphLock)
            _graph = graph;
    }

    public IReadOnlyList<SearchResultDto> Search(string query, int limit = 50)
    {
        var graph = CurrentGraph;
        if (graph is null || string.IsNullOrWhiteSpace(query))
            return [];

        return graph.Nodes
            .Where(n => n.Id.Contains(query, StringComparison.OrdinalIgnoreCase)
                     || n.Label.Contains(query, StringComparison.OrdinalIgnoreCase)
                     || (n.SourceFile?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false))
            .Take(limit)
            .Select(n => new SearchResultDto
            {
                Id = n.Id,
                Label = n.Label,
                Kind = n.Kind.ToString(),
                SourceFile = n.SourceFile,
                Community = n.Community
            })
            .ToList();
    }

    public IndexJobStatusDto CreateJob(IndexJobRequest request)
    {
        var job = new IndexJobStatusDto
        {
            Id = Guid.NewGuid().ToString("N"),
            Status = "Pending",
            CreatedAt = DateTimeOffset.UtcNow,
            Message = request.TargetRepositoryPath
        };
        _jobs[job.Id] = job;
        return job;
    }

    public IndexJobStatusDto? GetJob(string id) =>
        _jobs.TryGetValue(id, out var job) ? job : null;

    public void UpdateJob(IndexJobStatusDto job) => _jobs[job.Id] = job;

    public BatchIndexJobStatusDto CreateBatchJob(BatchIndexRequest request)
    {
        var job = new BatchIndexJobStatusDto
        {
            Id = Guid.NewGuid().ToString("N"),
            Status = "Pending",
            CreatedAt = DateTimeOffset.UtcNow,
            ParentFolderPath = request.ParentFolderPath,
            Message = request.ParentFolderPath
        };
        _batchJobs[job.Id] = job;
        return job;
    }

    public BatchIndexJobStatusDto? GetBatchJob(string id) =>
        _batchJobs.TryGetValue(id, out var job) ? job : null;

    public void UpdateBatchJob(BatchIndexJobStatusDto job) => _batchJobs[job.Id] = job;

    public async Task ExportAsync(EvidenceGraph graph, string outputDirectory, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(outputDirectory);
        var graphDto = _merger.ToGraphifyDto(graph);
        var codeMap = _merger.ToCodeToDbMap(graph);
        var spMap = _merger.ToStoredProcedureMap(graph);
        var refs = _merger.ToCodeReferenceLocations(graph);
        var reportModel = BuildReportModel(graph, codeMap, spMap, refs);

        await WriteJsonAsync(Path.Combine(outputDirectory, "graph.json"), graphDto, cancellationToken);
        await WriteJsonAsync(Path.Combine(outputDirectory, "code-to-db-map.json"), codeMap, cancellationToken);
        await WriteJsonAsync(Path.Combine(outputDirectory, "stored-procedure-map.json"), spMap, cancellationToken);
        await WriteJsonAsync(Path.Combine(outputDirectory, "code-reference-locations.json"), refs, cancellationToken);
        await File.WriteAllTextAsync(
            Path.Combine(outputDirectory, "GRAPH_REPORT.md"),
            BuildMarkdownReport(reportModel),
            cancellationToken);
        await File.WriteAllTextAsync(
            Path.Combine(outputDirectory, "findings.html"),
            BuildHtmlReport(reportModel),
            cancellationToken);
    }

    private static async Task WriteJsonAsync<T>(string path, T value, CancellationToken cancellationToken)
    {
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, value, JsonOptions, cancellationToken);
    }

    private sealed record ReportModel(
        DateTimeOffset GeneratedAt,
        string Repository,
        int NodeCount,
        int EdgeCount,
        string Sources,
        IReadOnlyList<(string Label, string Kind, int Degree)> GodNodes,
        int CodeMapEntries,
        int ReferenceLocations,
        int StoredProcedures,
        IReadOnlyList<(string Location, string CodeLabel, string Relation, string DbObject)> References,
        IReadOnlyList<(string Source, string Relation, string Confidence, string Target)> Ambiguous,
        string MermaidFlow);

    private static ReportModel BuildReportModel(
        EvidenceGraph graph,
        CodeToDbMapDto codeMap,
        StoredProcedureMapDto spMap,
        CodeReferenceLocationsDocument refs)
    {
        var topDegree = graph.Nodes
            .Select(n => (Node: n, Degree: graph.Edges.Count(e => e.Source == n.Id || e.Target == n.Id)))
            .OrderByDescending(x => x.Degree)
            .Take(10)
            .Select(x => (x.Node.Label, x.Node.Kind.ToString(), x.Degree))
            .ToList();

        var references = refs.References
            .Take(200)
            .Select(r => (r.Location ?? "", r.CodeLabel ?? "", r.Relation ?? "", r.DbObject ?? ""))
            .ToList();

        var ambiguous = graph.Edges
            .Where(e => e.Confidence == Confidence.Ambiguous)
            .Take(25)
            .Select(e => (e.Source, e.Relation.ToString(), e.Confidence.ToString(), e.Target))
            .ToList();

        return new ReportModel(
            DateTimeOffset.UtcNow,
            graph.Meta.TargetRepositoryPath ?? "(unknown)",
            graph.Nodes.Count,
            graph.Edges.Count,
            string.Join(", ", graph.Meta.Sources),
            topDegree,
            codeMap.Entries.Count,
            refs.Count,
            spMap.Procedures.Count,
            references,
            ambiguous,
            BuildMermaid(graph, topDegree));
    }

    private static string MermaidSafe(string value)
    {
        return value
            .Replace("\"", "'", StringComparison.Ordinal)
            .Replace("\r", " ", StringComparison.Ordinal)
            .Replace("\n", " ", StringComparison.Ordinal)
            .Replace("[", "(", StringComparison.Ordinal)
            .Replace("]", ")", StringComparison.Ordinal)
            .Replace("{", "(", StringComparison.Ordinal)
            .Replace("}", ")", StringComparison.Ordinal)
            .Replace("|", "/", StringComparison.Ordinal);
    }

    private static string BuildMermaid(
        EvidenceGraph graph,
        IReadOnlyList<(string Label, string Kind, int Degree)> gods)
    {
        var sb = new StringBuilder();
        sb.AppendLine("flowchart LR");
        sb.AppendLine($"  repo[\"{MermaidSafe(graph.Meta.TargetRepositoryPath ?? "repository")}\"]");
        sb.AppendLine($"  stats[\"{graph.Nodes.Count} nodes / {graph.Edges.Count} edges\"]");
        sb.AppendLine("  repo --> stats");

        for (var i = 0; i < Math.Min(gods.Count, 8); i++)
        {
            var id = $"g{i}";
            sb.AppendLine($"  {id}[\"{MermaidSafe(gods[i].Label)} ({gods[i].Degree})\"]");
            sb.AppendLine($"  stats --> {id}");
        }

        // Sample high-signal code→db edges for the diagram (cap for readability).
        var sample = graph.Edges
            .Where(e => e.Relation is EdgeRelation.Reads or EdgeRelation.Writes
                or EdgeRelation.Executes or EdgeRelation.Calls or EdgeRelation.Uses)
            .Take(12)
            .ToList();

        if (sample.Count == 0)
            sample = graph.Edges.Take(12).ToList();

        var nodeLabels = graph.Nodes.ToDictionary(n => n.Id, n => n.Label, StringComparer.OrdinalIgnoreCase);
        var edgeNodeIds = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        var next = 0;
        string EnsureNode(string nodeId)
        {
            if (edgeNodeIds.TryGetValue(nodeId, out var existing))
                return existing;
            var shortId = $"e{next++}";
            edgeNodeIds[nodeId] = shortId;
            var label = nodeLabels.TryGetValue(nodeId, out var l) ? l : nodeId;
            sb.AppendLine($"  {shortId}[\"{MermaidSafe(label)}\"]");
            return shortId;
        }

        foreach (var edge in sample)
        {
            var s = EnsureNode(edge.Source);
            var t = EnsureNode(edge.Target);
            sb.AppendLine($"  {s} -->|{MermaidSafe(edge.Relation.ToString())}| {t}");
        }

        return sb.ToString().TrimEnd();
    }

    private static string BuildMarkdownReport(ReportModel m)
    {
        var lines = new List<string>
        {
            "# DbIntelligence GRAPH_REPORT",
            "",
            $"Generated: {m.GeneratedAt:O}",
            $"Repository: {m.Repository}",
            $"Nodes: {m.NodeCount}",
            $"Edges: {m.EdgeCount}",
            $"Sources: {m.Sources}",
            "",
            "## Overview (Mermaid)",
            "",
            "```mermaid",
            m.MermaidFlow,
            "```",
            "",
            "## God nodes",
            ""
        };

        foreach (var item in m.GodNodes)
            lines.Add($"- {item.Label} (`{item.Kind}`) degree={item.Degree}");

        lines.Add("");
        lines.Add("## Code to DB map summary");
        lines.Add("");
        lines.Add($"Entries: {m.CodeMapEntries}");
        lines.Add($"Reference locations (full path + line): {m.ReferenceLocations}");
        lines.Add($"Stored procedures mapped: {m.StoredProcedures}");
        lines.Add("");
        lines.Add("## Reference locations (full path:line)");
        lines.Add("");
        foreach (var r in m.References)
            lines.Add($"- `{r.Location}` — {r.CodeLabel} -[{r.Relation}]-> {r.DbObject}");
        if (m.ReferenceLocations > m.References.Count)
            lines.Add($"- … and {m.ReferenceLocations - m.References.Count} more (see code-reference-locations.json)");

        lines.Add("");
        lines.Add("## Review queue (AMBIGUOUS)");
        lines.Add("");
        if (m.Ambiguous.Count == 0)
            lines.Add("- (none)");
        else
        {
            foreach (var edge in m.Ambiguous)
                lines.Add($"- {edge.Source} -[{edge.Relation}/{edge.Confidence}]-> {edge.Target}");
        }

        lines.Add("");
        lines.Add("## Companion files");
        lines.Add("");
        lines.Add("- `findings.html` — standalone HTML report");
        lines.Add("- `graph.json`, `code-to-db-map.json`, `stored-procedure-map.json`, `code-reference-locations.json`");

        return string.Join(Environment.NewLine, lines);
    }

    private static string BuildHtmlReport(ReportModel m)
    {
        static string H(string? s) => WebUtility.HtmlEncode(s ?? "");

        var gods = string.Join(Environment.NewLine, m.GodNodes.Select(g =>
            $"<tr><td>{H(g.Label)}</td><td><code>{H(g.Kind)}</code></td><td>{g.Degree}</td></tr>"));

        var refs = string.Join(Environment.NewLine, m.References.Select(r =>
            $"<tr><td><code>{H(r.Location)}</code></td><td>{H(r.CodeLabel)}</td><td>{H(r.Relation)}</td><td>{H(r.DbObject)}</td></tr>"));

        var ambiguous = m.Ambiguous.Count == 0
            ? "<tr><td colspan=\"4\">(none)</td></tr>"
            : string.Join(Environment.NewLine, m.Ambiguous.Select(e =>
                $"<tr><td><code>{H(e.Source)}</code></td><td>{H(e.Relation)}</td><td>{H(e.Confidence)}</td><td><code>{H(e.Target)}</code></td></tr>"));

        var mermaid = H(m.MermaidFlow);

        return $$"""
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="utf-8" />
  <meta name="viewport" content="width=device-width, initial-scale=1" />
  <title>DbIntelligence findings — {{H(m.Repository)}}</title>
  <style>
    :root { color-scheme: light; --bg:#f4f6f8; --card:#fff; --text:#1b1f24; --muted:#5b6578; --line:#d8dee9; --accent:#0b6e4f; }
    body { margin:0; font-family:"Segoe UI",system-ui,sans-serif; background:var(--bg); color:var(--text); line-height:1.45; }
    header { background:#0f172a; color:#f8fafc; padding:1.4rem 1.6rem; }
    header h1 { margin:0 0 .35rem; font-size:1.35rem; }
    header p { margin:0; opacity:.85; }
    main { max-width:1100px; margin:0 auto; padding:1.25rem; display:grid; gap:1rem; }
    section { background:var(--card); border:1px solid var(--line); border-radius:8px; padding:1rem 1.1rem; }
    h2 { margin:0 0 .75rem; font-size:1.05rem; color:var(--accent); }
    .stats { display:grid; grid-template-columns:repeat(auto-fit,minmax(120px,1fr)); gap:.6rem; }
    .stat { background:#e8f5f0; border-radius:8px; padding:.7rem .8rem; }
    .stat .n { font-size:1.35rem; font-weight:700; color:var(--accent); }
    .stat .l { font-size:.75rem; color:var(--muted); text-transform:uppercase; letter-spacing:.04em; }
    table { width:100%; border-collapse:collapse; font-size:.92rem; }
    th,td { text-align:left; padding:.4rem .5rem; border-bottom:1px solid var(--line); vertical-align:top; }
    th { color:var(--muted); font-size:.75rem; text-transform:uppercase; }
    code { font-family:Consolas,"Cascadia Mono",monospace; font-size:.88em; }
    .mermaid { background:#fafbfc; border:1px dashed var(--line); border-radius:8px; padding:.75rem; overflow-x:auto; }
    footer { text-align:center; color:var(--muted); font-size:.85rem; padding:0 1rem 1.5rem; }
  </style>
</head>
<body>
  <header>
    <h1>DbIntelligence findings</h1>
    <p>{{H(m.Repository)}} · generated {{H(m.GeneratedAt.ToString("O"))}} · sources: {{H(m.Sources)}}</p>
  </header>
  <main>
    <section>
      <h2>Summary</h2>
      <div class="stats">
        <div class="stat"><div class="n">{{m.NodeCount}}</div><div class="l">Nodes</div></div>
        <div class="stat"><div class="n">{{m.EdgeCount}}</div><div class="l">Edges</div></div>
        <div class="stat"><div class="n">{{m.CodeMapEntries}}</div><div class="l">Code→DB</div></div>
        <div class="stat"><div class="n">{{m.StoredProcedures}}</div><div class="l">SPs</div></div>
        <div class="stat"><div class="n">{{m.ReferenceLocations}}</div><div class="l">Locations</div></div>
      </div>
    </section>
    <section>
      <h2>Overview</h2>
      <pre class="mermaid">{{mermaid}}</pre>
    </section>
    <section>
      <h2>God nodes</h2>
      <table>
        <thead><tr><th>Label</th><th>Kind</th><th>Degree</th></tr></thead>
        <tbody>
{{gods}}
        </tbody>
      </table>
    </section>
    <section>
      <h2>Reference locations</h2>
      <table>
        <thead><tr><th>Location</th><th>Code</th><th>Relation</th><th>DB object</th></tr></thead>
        <tbody>
{{refs}}
        </tbody>
      </table>
    </section>
    <section>
      <h2>Review queue (AMBIGUOUS)</h2>
      <table>
        <thead><tr><th>Source</th><th>Relation</th><th>Confidence</th><th>Target</th></tr></thead>
        <tbody>
{{ambiguous}}
        </tbody>
      </table>
    </section>
  </main>
  <footer>Written by DbIntelligence export into <code>.db-index</code> · also see <code>GRAPH_REPORT.md</code> and JSON maps</footer>
  <script type="module">
    import mermaid from "https://cdn.jsdelivr.net/npm/mermaid@11/dist/mermaid.esm.min.mjs";
    mermaid.initialize({ startOnLoad: true, theme: "neutral", securityLevel: "loose" });
  </script>
</body>
</html>
""";
    }
}
