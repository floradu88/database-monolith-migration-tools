using System.Collections.Concurrent;
using System.Text.Json;
using DbIntelligence.Contracts;
using DbIntelligence.Domain;
using Microsoft.Extensions.Options;

namespace DbIntelligence.Infrastructure;

/// <summary>
/// Process-local store: the live evidence graph and index jobs live <b>in memory</b>.
/// Persistence is optional file export only (JSON / markdown under an artifacts folder) —
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

        await WriteJsonAsync(Path.Combine(outputDirectory, "graph.json"), graphDto, cancellationToken);
        await WriteJsonAsync(Path.Combine(outputDirectory, "code-to-db-map.json"), codeMap, cancellationToken);
        await WriteJsonAsync(Path.Combine(outputDirectory, "stored-procedure-map.json"), spMap, cancellationToken);
        await File.WriteAllTextAsync(
            Path.Combine(outputDirectory, "GRAPH_REPORT.md"),
            BuildReport(graph, codeMap, spMap),
            cancellationToken);
    }

    private static async Task WriteJsonAsync<T>(string path, T value, CancellationToken cancellationToken)
    {
        await using var stream = File.Create(path);
        await JsonSerializer.SerializeAsync(stream, value, JsonOptions, cancellationToken);
    }

    private static string BuildReport(EvidenceGraph graph, CodeToDbMapDto codeMap, StoredProcedureMapDto spMap)
    {
        var topDegree = graph.Nodes
            .Select(n => (Node: n, Degree: graph.Edges.Count(e => e.Source == n.Id || e.Target == n.Id)))
            .OrderByDescending(x => x.Degree)
            .Take(10)
            .ToList();

        var lines = new List<string>
        {
            "# DbIntelligence GRAPH_REPORT",
            "",
            $"Generated: {DateTimeOffset.UtcNow:O}",
            $"Nodes: {graph.Nodes.Count}",
            $"Edges: {graph.Edges.Count}",
            $"Sources: {string.Join(", ", graph.Meta.Sources)}",
            "",
            "## God nodes",
            ""
        };

        foreach (var item in topDegree)
            lines.Add($"- {item.Node.Label} (`{item.Node.Kind}`) degree={item.Degree}");

        lines.Add("");
        lines.Add("## Code to DB map summary");
        lines.Add("");
        lines.Add($"Entries: {codeMap.Entries.Count}");
        lines.Add($"Stored procedures mapped: {spMap.Procedures.Count}");
        lines.Add("");
        lines.Add("## Review queue (AMBIGUOUS)");
        lines.Add("");
        foreach (var edge in graph.Edges.Where(e => e.Confidence == Confidence.Ambiguous).Take(25))
            lines.Add($"- {edge.Source} -[{edge.Relation}/{edge.Confidence}]-> {edge.Target}");

        return string.Join(Environment.NewLine, lines);
    }
}
