using System.Text.Json;
using DbIntelligence.Contracts;
using DbIntelligence.Domain;
using Microsoft.Extensions.Options;

namespace DbIntelligence.Infrastructure.Graphify;

public interface IGraphifyClient
{
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);
    Task<CliResult> RunAsync(string repositoryPath, bool updateOnly = false, CancellationToken cancellationToken = default);
    Task<EvidenceGraph?> ImportGraphJsonAsync(string repositoryPath, CancellationToken cancellationToken = default);
}

public sealed class GraphifyClient : IGraphifyClient
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly CliProcessRunner _runner;
    private readonly DbIntelligenceOptions _options;

    public GraphifyClient(CliProcessRunner runner, IOptions<DbIntelligenceOptions> options)
    {
        _runner = runner;
        _options = options.Value;
    }

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        var result = await _runner.RunAsync(_options.GraphifyExecutable, ["--help"], timeoutSeconds: 30, cancellationToken: cancellationToken);
        var text = $"{result.StandardOutput}\n{result.StandardError}";
        // Accept Graphify-Labs CLI (extract/update/query) — reject unrelated tools that only expose install.
        return text.Contains("extract", StringComparison.OrdinalIgnoreCase)
            || text.Contains("update <path>", StringComparison.OrdinalIgnoreCase)
            || text.Contains("graphify-out", StringComparison.OrdinalIgnoreCase);
    }

    public Task<CliResult> RunAsync(string repositoryPath, bool updateOnly = false, CancellationToken cancellationToken = default)
    {
        // Headless code extraction (AST, no LLM). Prefer update when refreshing an existing graph.
        var args = updateOnly
            ? new List<string> { "update", repositoryPath, "--force" }
            : new List<string> { "extract", repositoryPath, "--code-only", "--out", repositoryPath };

        return _runner.RunAsync(
            _options.GraphifyExecutable,
            args,
            workingDirectory: repositoryPath,
            timeoutSeconds: _options.ProcessTimeoutSeconds,
            cancellationToken: cancellationToken);
    }

    public async Task<EvidenceGraph?> ImportGraphJsonAsync(string repositoryPath, CancellationToken cancellationToken = default)
    {
        var path = Path.Combine(repositoryPath, "graphify-out", "graph.json");
        if (!File.Exists(path))
            return null;

        await using var stream = File.OpenRead(path);
        var dto = await JsonSerializer.DeserializeAsync<GraphifyGraphDto>(stream, JsonOptions, cancellationToken);
        if (dto is null)
            return null;

        var graph = new EvidenceGraph();
        graph.Meta.Sources.Add("graphify");
        graph.Meta.TargetRepositoryPath = repositoryPath;

        foreach (var node in dto.Nodes)
        {
            graph.UpsertNode(new GraphNode
            {
                Id = string.IsNullOrWhiteSpace(node.Id) ? GraphIds.Concept(node.Label) : node.Id,
                Label = string.IsNullOrWhiteSpace(node.Label) ? node.Id : node.Label,
                Kind = MapKind(node.Kind ?? node.Type ?? node.FileType),
                SourceFile = node.SourceFile,
                SourceLocation = node.SourceLocation,
                Community = node.Community,
                Schema = node.Schema,
                Database = node.Database
            });
        }

        foreach (var edge in dto.AllEdges)
        {
            var from = edge.FromId;
            var to = edge.ToId;
            if (string.IsNullOrWhiteSpace(from) || string.IsNullOrWhiteSpace(to))
                continue;

            graph.UpsertEdge(new GraphEdge
            {
                Source = from,
                Target = to,
                Relation = MapRelation(edge.RelationOrType),
                Confidence = MapConfidence(edge.Confidence),
                Evidence = BuildEvidence(edge)
            });
        }

        return graph;
    }

    private static EdgeEvidence? BuildEvidence(GraphifyEdgeDto edge)
    {
        if (edge.Evidence is not null)
        {
            return new EdgeEvidence
            {
                File = edge.Evidence.File,
                Line = edge.Evidence.Line,
                Pattern = edge.Evidence.Pattern,
                RawReference = edge.Evidence.RawReference
            };
        }

        if (string.IsNullOrWhiteSpace(edge.SourceFile) && string.IsNullOrWhiteSpace(edge.SourceLocation))
            return null;

        int? line = null;
        if (!string.IsNullOrWhiteSpace(edge.SourceLocation)
            && edge.SourceLocation.StartsWith("L", StringComparison.OrdinalIgnoreCase)
            && int.TryParse(edge.SourceLocation[1..], out var parsed))
            line = parsed;

        return new EdgeEvidence
        {
            File = edge.SourceFile,
            Line = line,
            Pattern = edge.RelationOrType
        };
    }

    private static NodeKind MapKind(string? kind) =>
        kind?.ToLowerInvariant() switch
        {
            "file" or "code" => NodeKind.File,
            "type" or "class" or "namespace" => NodeKind.Type,
            "method" or "function" => NodeKind.Method,
            "table" => NodeKind.Table,
            "view" => NodeKind.View,
            "storedprocedure" or "stored_procedure" or "procedure" => NodeKind.StoredProcedure,
            "database" => NodeKind.Database,
            "schema" => NodeKind.Schema,
            "trigger" => NodeKind.Trigger,
            "job" => NodeKind.Job,
            "application" => NodeKind.Application,
            _ => NodeKind.Concept
        };

    private static EdgeRelation MapRelation(string relation) =>
        relation.ToLowerInvariant() switch
        {
            "calls" or "call" => EdgeRelation.Calls,
            "imports" or "import" => EdgeRelation.Imports,
            "uses" or "use" or "contains" => EdgeRelation.Uses,
            "reads" or "read" => EdgeRelation.Reads,
            "writes" or "write" => EdgeRelation.Writes,
            "executes" or "execute" => EdgeRelation.Executes,
            "depends_on" or "dependson" or "depends" => EdgeRelation.DependsOn,
            "owns" => EdgeRelation.Owns,
            "migrates_to" or "migratesto" => EdgeRelation.MigratesTo,
            _ => EdgeRelation.Uses
        };

    private static Confidence MapConfidence(string? confidence) =>
        confidence?.ToUpperInvariant() switch
        {
            "INFERRED" => Confidence.Inferred,
            "AMBIGUOUS" => Confidence.Ambiguous,
            _ => Confidence.Extracted
        };
}
