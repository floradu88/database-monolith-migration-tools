using System.Text.Json.Serialization;

namespace DbIntelligence.Contracts;

public sealed class GraphifyGraphDto
{
    [JsonPropertyName("nodes")]
    public List<GraphifyNodeDto> Nodes { get; set; } = [];

    /// <summary>NetworkX / Graphify CLI export uses "links".</summary>
    [JsonPropertyName("links")]
    public List<GraphifyEdgeDto> Links { get; set; } = [];

    /// <summary>Our unified export and some tools use "edges".</summary>
    [JsonPropertyName("edges")]
    public List<GraphifyEdgeDto> Edges { get; set; } = [];

    [JsonPropertyName("meta")]
    public GraphifyMetaDto Meta { get; set; } = new();

    public IReadOnlyList<GraphifyEdgeDto> AllEdges =>
        Links.Count > 0 ? Links : Edges;
}

public sealed class GraphifyNodeDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("label")]
    public string Label { get; set; } = string.Empty;

    [JsonPropertyName("kind")]
    public string? Kind { get; set; }

    [JsonPropertyName("source_file")]
    public string? SourceFile { get; set; }

    [JsonPropertyName("source_location")]
    public string? SourceLocation { get; set; }

    [JsonPropertyName("community")]
    [JsonConverter(typeof(FlexibleStringConverter))]
    public string? Community { get; set; }

    [JsonPropertyName("file_type")]
    public string? FileType { get; set; }

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("schema")]
    public string? Schema { get; set; }

    [JsonPropertyName("database")]
    public string? Database { get; set; }
}

public sealed class GraphifyEdgeDto
{
    [JsonPropertyName("source")]
    public string Source { get; set; } = string.Empty;

    [JsonPropertyName("target")]
    public string Target { get; set; } = string.Empty;

    [JsonPropertyName("from")]
    public string? From { get; set; }

    [JsonPropertyName("to")]
    public string? To { get; set; }

    [JsonPropertyName("relation")]
    public string Relation { get; set; } = string.Empty;

    [JsonPropertyName("type")]
    public string? Type { get; set; }

    [JsonPropertyName("confidence")]
    [JsonConverter(typeof(FlexibleStringConverter))]
    public string? Confidence { get; set; } = "EXTRACTED";

    [JsonPropertyName("source_file")]
    public string? SourceFile { get; set; }

    [JsonPropertyName("source_location")]
    public string? SourceLocation { get; set; }

    [JsonPropertyName("evidence")]
    public GraphifyEvidenceDto? Evidence { get; set; }

    public string FromId => !string.IsNullOrWhiteSpace(Source) ? Source : From ?? string.Empty;
    public string ToId => !string.IsNullOrWhiteSpace(Target) ? Target : To ?? string.Empty;
    public string RelationOrType => !string.IsNullOrWhiteSpace(Relation) ? Relation : Type ?? "related";
}

public sealed class GraphifyEvidenceDto
{
    [JsonPropertyName("file")]
    public string? File { get; set; }

    [JsonPropertyName("line")]
    public int? Line { get; set; }

    [JsonPropertyName("pattern")]
    public string? Pattern { get; set; }

    [JsonPropertyName("raw_reference")]
    public string? RawReference { get; set; }
}

public sealed class GraphifyMetaDto
{
    [JsonPropertyName("generatedAt")]
    public DateTimeOffset GeneratedAt { get; set; } = DateTimeOffset.UtcNow;

    [JsonPropertyName("sources")]
    public List<string> Sources { get; set; } = [];

    [JsonPropertyName("targetRepositoryPath")]
    public string? TargetRepositoryPath { get; set; }
}

public sealed class CodeToDbMapDto
{
    [JsonPropertyName("generatedAt")]
    public DateTimeOffset GeneratedAt { get; set; } = DateTimeOffset.UtcNow;

    [JsonPropertyName("entries")]
    public List<CodeToDbEntryDto> Entries { get; set; } = [];
}

public sealed class CodeToDbEntryDto
{
    [JsonPropertyName("codeNodeId")]
    public string CodeNodeId { get; set; } = string.Empty;

    [JsonPropertyName("codeLabel")]
    public string CodeLabel { get; set; } = string.Empty;

    [JsonPropertyName("sourceFile")]
    public string? SourceFile { get; set; }

    [JsonPropertyName("line")]
    public int? Line { get; set; }

    [JsonPropertyName("dbNodeId")]
    public string DbNodeId { get; set; } = string.Empty;

    [JsonPropertyName("dbObject")]
    public string DbObject { get; set; } = string.Empty;

    [JsonPropertyName("dbKind")]
    public string DbKind { get; set; } = string.Empty;

    [JsonPropertyName("relation")]
    public string Relation { get; set; } = string.Empty;

    [JsonPropertyName("confidence")]
    public string Confidence { get; set; } = string.Empty;

    [JsonPropertyName("pattern")]
    public string? Pattern { get; set; }

    [JsonPropertyName("project")]
    public string? Project { get; set; }
}

public sealed class StoredProcedureMapDto
{
    [JsonPropertyName("generatedAt")]
    public DateTimeOffset GeneratedAt { get; set; } = DateTimeOffset.UtcNow;

    [JsonPropertyName("procedures")]
    public List<StoredProcedureMapEntryDto> Procedures { get; set; } = [];
}

public sealed class StoredProcedureMapEntryDto
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    [JsonPropertyName("schema")]
    public string? Schema { get; set; }

    [JsonPropertyName("database")]
    public string? Database { get; set; }

    [JsonPropertyName("codeCallers")]
    public List<string> CodeCallers { get; set; } = [];

    [JsonPropertyName("sqlCallers")]
    public List<string> SqlCallers { get; set; } = [];

    [JsonPropertyName("reads")]
    public List<string> Reads { get; set; } = [];

    [JsonPropertyName("writes")]
    public List<string> Writes { get; set; } = [];
}

public sealed class IndexJobRequest
{
    /// <summary>
    /// Absolute or relative path to the repository to analyze.
    /// Required for index jobs unless DbIntelligence:TargetRepositoryPath is configured.
    /// </summary>
    public string? TargetRepositoryPath { get; set; }

    /// <summary>
    /// Invoke the <c>codegraph</c> CLI against <see cref="TargetRepositoryPath"/>.
    /// Assumes Codegraph is installed and on PATH.
    /// </summary>
    public bool RunCodegraph { get; set; } = true;

    /// <summary>
    /// Invoke the <c>graphify</c> CLI against <see cref="TargetRepositoryPath"/>.
    /// Assumes Graphify is installed and on PATH (requires Python).
    /// </summary>
    public bool RunGraphify { get; set; } = true;

    /// <summary>
    /// When true, always re-run <c>graphify extract</c>.
    /// When false (default), reuse <c>graphify-out/graph.json</c> if it already exists
    /// (important for large repos where extract can take many minutes).
    /// </summary>
    public bool RefreshGraphify { get; set; } = false;

    public bool RunRepositoryScan { get; set; } = true;
    public bool RunSqlScan { get; set; } = false;
    public string? SqlConnectionString { get; set; }

    /// <summary>
    /// Relative folder under the project root where JSON/MD artifacts are written.
    /// Empty or "." writes into the project root. Default: artifacts/db-intelligence.
    /// </summary>
    public string? ArtifactsRelativeDirectory { get; set; }
}

/// <summary>
/// Index every immediate child folder under a parent path (each child = one project).
/// Results are written under each project's root (see <see cref="ArtifactsRelativeDirectory"/>).
/// </summary>
public sealed class BatchIndexRequest
{
    /// <summary>Parent folder whose subfolders are individual projects.</summary>
    public string ParentFolderPath { get; set; } = string.Empty;

    public bool RunCodegraph { get; set; } = true;
    public bool RunGraphify { get; set; } = true;
    public bool RefreshGraphify { get; set; } = false;
    public bool RunRepositoryScan { get; set; } = true;
    public bool RunSqlScan { get; set; } = false;
    public string? SqlConnectionString { get; set; }

    /// <summary>
    /// Relative output under each project. Default empty = project root
    /// (graph.json, code-to-db-map.json, stored-procedure-map.json, GRAPH_REPORT.md).
    /// </summary>
    public string? ArtifactsRelativeDirectory { get; set; } = "";

    /// <summary>
    /// When true, only include child folders that look like code projects
    /// (.git, *.sln, *.csproj, package.json, etc.).
    /// </summary>
    public bool RequireProjectMarkers { get; set; } = false;

    /// <summary>Optional explicit child folder names; when empty, auto-discover.</summary>
    public List<string>? ProjectFolderNames { get; set; }

    /// <summary>Continue remaining projects when one fails (default true).</summary>
    public bool ContinueOnError { get; set; } = true;
}

public sealed class BatchIndexJobStatusDto
{
    public string Id { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public string? Phase { get; set; }
    public string? Message { get; set; }
    public string ParentFolderPath { get; set; } = string.Empty;
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public int TotalProjects { get; set; }
    public int CompletedProjects { get; set; }
    public int FailedProjects { get; set; }
    public string? CurrentProject { get; set; }
    public List<BatchProjectResultDto> Projects { get; set; } = [];
    public List<string> Log { get; set; } = [];
}

public sealed class BatchProjectResultDto
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public string? Message { get; set; }
    public string? ArtifactsDirectory { get; set; }
    public int? NodeCount { get; set; }
    public int? EdgeCount { get; set; }
    public DateTimeOffset? StartedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
}

public sealed class DiscoveredProjectsDto
{
    public string ParentFolderPath { get; set; } = string.Empty;
    public List<DiscoveredProjectDto> Projects { get; set; } = [];
}

public sealed class DiscoveredProjectDto
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public bool HasProjectMarker { get; set; }
}

public sealed class IndexJobStatusDto
{
    public string Id { get; set; } = string.Empty;
    public string Status { get; set; } = "Pending";
    public string? Phase { get; set; }
    public string? Message { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset? CompletedAt { get; set; }
    public List<string> Log { get; set; } = [];
}

public sealed class SearchResultDto
{
    public string Id { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Kind { get; set; } = string.Empty;
    public string? SourceFile { get; set; }
    public string? Community { get; set; }
}

public sealed class ExportRequest
{
    public string? OutputDirectory { get; set; }
}

/// <summary>
/// Load exported per-project graph.json files under a parent folder and present them as one in-memory graph.
/// </summary>
public sealed class CombineGraphsRequest
{
    public string ParentFolderPath { get; set; } = string.Empty;

    /// <summary>Relative artifacts folder under each project (empty = project root).</summary>
    public string? ArtifactsRelativeDirectory { get; set; } = "";

    public bool RequireProjectMarkers { get; set; }

    public List<string>? ProjectFolderNames { get; set; }

    /// <summary>When true (default), share unprefixed DB nodes across projects.</summary>
    public bool ShareDatabaseNodes { get; set; } = true;

    /// <summary>When a batch summary exists, only load projects marked Completed (default true).</summary>
    public bool OnlyCompletedFromSummary { get; set; } = true;

    /// <summary>Also write combined graph.json / maps under the parent (default true).</summary>
    public bool ExportCombined { get; set; } = true;

    /// <summary>Override combined export directory (default: {parent}/db-intelligence-combined).</summary>
    public string? CombinedOutputDirectory { get; set; }
}

public sealed class CombineGraphsResultDto
{
    public string ParentFolderPath { get; set; } = string.Empty;
    public int ProjectsLoaded { get; set; }
    public int ProjectsSkipped { get; set; }
    public int NodeCount { get; set; }
    public int EdgeCount { get; set; }
    public string? CombinedOutputDirectory { get; set; }
    public List<CombinedProjectLoadDto> Loaded { get; set; } = [];
    public List<CombinedProjectLoadDto> Skipped { get; set; } = [];
}

public sealed class CombinedProjectLoadDto
{
    public string Name { get; set; } = string.Empty;
    public string Path { get; set; } = string.Empty;
    public string? ArtifactsDirectory { get; set; }
    public string? GraphJsonPath { get; set; }
    public string Status { get; set; } = string.Empty;
    public string? Message { get; set; }
    public int? NodeCount { get; set; }
    public int? EdgeCount { get; set; }
}
