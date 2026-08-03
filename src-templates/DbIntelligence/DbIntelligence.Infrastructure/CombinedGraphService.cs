using System.Text.Json;
using DbIntelligence.Contracts;
using DbIntelligence.Domain;
using Microsoft.Extensions.Logging;

namespace DbIntelligence.Infrastructure;

public interface ICombinedGraphService
{
    Task<CombineGraphsResultDto> CombineFromParentAsync(
        CombineGraphsRequest request,
        CancellationToken cancellationToken = default);
}

public sealed class CombinedGraphService : ICombinedGraphService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly IIntelligenceStore _store;
    private readonly EvidenceGraphMerger _merger;
    private readonly ILogger<CombinedGraphService> _logger;

    public CombinedGraphService(
        IIntelligenceStore store,
        EvidenceGraphMerger merger,
        ILogger<CombinedGraphService> logger)
    {
        _store = store;
        _merger = merger;
        _logger = logger;
    }

    public async Task<CombineGraphsResultDto> CombineFromParentAsync(
        CombineGraphsRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.ParentFolderPath))
            throw new InvalidOperationException("ParentFolderPath is required.");

        var parent = Path.GetFullPath(request.ParentFolderPath);
        if (!Directory.Exists(parent))
            throw new DirectoryNotFoundException($"Parent folder not found: {parent}");

        var projects = ResolveProjects(parent, request);
        if (projects.Count == 0)
            throw new InvalidOperationException($"No projects with graph.json found under {parent}.");

        var pieces = new List<EvidenceGraph>();
        var loaded = new List<CombinedProjectLoadDto>();
        var skipped = new List<CombinedProjectLoadDto>();

        foreach (var (name, path, artifactsDir) in projects)
        {
            var graphPath = Path.Combine(artifactsDir, "graph.json");
            if (!File.Exists(graphPath))
            {
                skipped.Add(new CombinedProjectLoadDto
                {
                    Name = name,
                    Path = path,
                    ArtifactsDirectory = artifactsDir,
                    Status = "Skipped",
                    Message = "graph.json not found"
                });
                continue;
            }

            var graph = await LoadExportedGraphAsync(graphPath, path, cancellationToken);
            if (graph is null || graph.Nodes.Count == 0)
            {
                skipped.Add(new CombinedProjectLoadDto
                {
                    Name = name,
                    Path = path,
                    ArtifactsDirectory = artifactsDir,
                    Status = "Skipped",
                    Message = "graph.json empty or unreadable"
                });
                continue;
            }

            var namespaced = NamespaceForProject(graph, name, request.ShareDatabaseNodes);
            pieces.Add(namespaced);
            loaded.Add(new CombinedProjectLoadDto
            {
                Name = name,
                Path = path,
                ArtifactsDirectory = artifactsDir,
                Status = "Loaded",
                NodeCount = namespaced.Nodes.Count,
                EdgeCount = namespaced.Edges.Count,
                GraphJsonPath = graphPath
            });
        }

        if (pieces.Count == 0)
            throw new InvalidOperationException($"No readable graph.json files under {parent}.");

        var combined = _merger.Merge(pieces.ToArray());
        combined.Meta.TargetRepositoryPath = parent;
        if (!combined.Meta.Sources.Contains("combined-parent", StringComparer.OrdinalIgnoreCase))
            combined.Meta.Sources.Add("combined-parent");

        _store.SetGraph(combined);

        string? exportDir = null;
        if (request.ExportCombined)
        {
            exportDir = string.IsNullOrWhiteSpace(request.CombinedOutputDirectory)
                ? Path.Combine(parent, DbIntelligenceOptions.DefaultCombinedDirectoryName)
                : Path.GetFullPath(request.CombinedOutputDirectory);
            await _store.ExportAsync(combined, exportDir, cancellationToken);
            _logger.LogInformation("Exported combined graph to {ExportDir}", exportDir);
        }

        return new CombineGraphsResultDto
        {
            ParentFolderPath = parent,
            ProjectsLoaded = loaded.Count,
            ProjectsSkipped = skipped.Count,
            NodeCount = combined.Nodes.Count,
            EdgeCount = combined.Edges.Count,
            CombinedOutputDirectory = exportDir,
            Loaded = loaded,
            Skipped = skipped
        };
    }

    private static List<(string Name, string Path, string ArtifactsDir)> ResolveProjects(
        string parent,
        CombineGraphsRequest request)
    {
        var relative = request.ArtifactsRelativeDirectory ?? DbIntelligenceOptions.DefaultArtifactsDirectory;
        var summaryPath = Path.Combine(parent, "db-intelligence-batch-summary.json");
        var results = new List<(string Name, string Path, string ArtifactsDir)>();

        if (File.Exists(summaryPath))
        {
            try
            {
                using var stream = File.OpenRead(summaryPath);
                using var doc = JsonDocument.Parse(stream);
                if (doc.RootElement.TryGetProperty("projects", out var projectsEl) &&
                    projectsEl.ValueKind == JsonValueKind.Array)
                {
                    foreach (var p in projectsEl.EnumerateArray())
                    {
                        var name = p.TryGetProperty("name", out var n) ? n.GetString() : null;
                        var path = p.TryGetProperty("path", out var pathEl) ? pathEl.GetString() : null;
                        var status = p.TryGetProperty("status", out var st) ? st.GetString() : null;
                        var artifacts = p.TryGetProperty("artifactsDirectory", out var ad)
                            ? ad.GetString()
                            : null;

                        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(path))
                            continue;
                        if (!string.Equals(status, "Completed", StringComparison.OrdinalIgnoreCase) &&
                            request.OnlyCompletedFromSummary)
                            continue;
                        if (request.ProjectFolderNames is { Count: > 0 } &&
                            !request.ProjectFolderNames.Contains(name, StringComparer.OrdinalIgnoreCase))
                            continue;

                        var artifactsDir = !string.IsNullOrWhiteSpace(artifacts)
                            ? Path.GetFullPath(artifacts)
                            : ProjectFolderDiscovery.ResolveArtifactsDirectory(path, relative);
                        results.Add((name, Path.GetFullPath(path), artifactsDir));
                    }
                }
            }
            catch (JsonException)
            {
                // Fall through to discovery.
            }
        }

        if (results.Count > 0)
            return results;

        var discovered = ProjectFolderDiscovery.Discover(
            parent,
            request.RequireProjectMarkers,
            request.ProjectFolderNames);

        foreach (var (name, path, _) in discovered)
        {
            var artifactsDir = ProjectFolderDiscovery.ResolveArtifactsDirectory(path, relative);
            results.Add((name, path, artifactsDir));
        }

        return results;
    }

    private static async Task<EvidenceGraph?> LoadExportedGraphAsync(
        string graphJsonPath,
        string projectPath,
        CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(graphJsonPath);
        var dto = await JsonSerializer.DeserializeAsync<GraphifyGraphDto>(stream, JsonOptions, cancellationToken);
        if (dto is null)
            return null;

        var graph = new EvidenceGraph();
        graph.Meta.Sources.Add("exported-graph");
        graph.Meta.TargetRepositoryPath = projectPath;
        if (dto.Meta?.Sources is { Count: > 0 })
        {
            foreach (var source in dto.Meta.Sources)
            {
                if (!graph.Meta.Sources.Contains(source, StringComparer.OrdinalIgnoreCase))
                    graph.Meta.Sources.Add(source);
            }
        }

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
                Evidence = edge.Evidence is null
                    ? null
                    : new EdgeEvidence
                    {
                        File = edge.Evidence.File,
                        Line = edge.Evidence.Line,
                        Pattern = edge.Evidence.Pattern,
                        RawReference = edge.Evidence.RawReference
                    }
            });
        }

        return graph;
    }

    private static EvidenceGraph NamespaceForProject(EvidenceGraph source, string projectName, bool shareDatabaseNodes)
    {
        var result = new EvidenceGraph { Meta = source.Meta };
        result.Meta.TargetRepositoryPath = source.Meta.TargetRepositoryPath;
        var idMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var node in source.Nodes)
        {
            var share = shareDatabaseNodes && ProjectGraphIds.ShouldShareAcrossProjects(node);
            var newId = share ? ProjectGraphIds.CoreId(node.Id) : ProjectGraphIds.Prefix(projectName, node.Id);
            idMap[node.Id] = newId;

            var copy = new GraphNode
            {
                Id = newId,
                Label = share ? node.Label : $"[{projectName}] {node.Label}",
                Kind = node.Kind,
                SourceFile = node.SourceFile,
                SourceLocation = node.SourceLocation,
                Community = node.Community ?? projectName,
                Schema = node.Schema,
                Database = node.Database,
                Properties = new Dictionary<string, string>(node.Properties, StringComparer.OrdinalIgnoreCase)
                {
                    [ProjectGraphIds.ProjectPropertyKey] = projectName
                }
            };
            result.UpsertNode(copy);
        }

        foreach (var edge in source.Edges)
        {
            if (!idMap.TryGetValue(edge.Source, out var from) || !idMap.TryGetValue(edge.Target, out var to))
                continue;

            result.UpsertEdge(new GraphEdge
            {
                Source = from,
                Target = to,
                Relation = edge.Relation,
                Confidence = edge.Confidence,
                Evidence = edge.Evidence is null
                    ? null
                    : new EdgeEvidence
                    {
                        File = edge.Evidence.File,
                        Line = edge.Evidence.Line,
                        Pattern = edge.Evidence.Pattern,
                        RawReference = edge.Evidence.RawReference
                    },
                Locations = edge.Locations.Select(l => new EdgeEvidence
                {
                    File = l.File,
                    Line = l.Line,
                    Pattern = l.Pattern,
                    RawReference = l.RawReference
                }).ToList()
            });
        }

        return result;
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
