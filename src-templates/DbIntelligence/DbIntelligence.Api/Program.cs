using DbIntelligence.Contracts;
using DbIntelligence.Domain;
using DbIntelligence.Infrastructure;
using DbIntelligence.Infrastructure.Codegraph;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<DbIntelligenceOptions>(
    builder.Configuration.GetSection(DbIntelligenceOptions.SectionName));
builder.Services.AddDbIntelligence();
builder.Services.AddHostedService<IndexingWorker>();
builder.Services.AddCors(options =>
{
    options.AddPolicy("angular", policy =>
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod());
});

var app = builder.Build();
app.UseCors("angular");
app.UseDefaultFiles();
app.UseStaticFiles();

var api = app.MapGroup("/api");

api.MapPost("/index/jobs", async (IndexJobRequest request, IIndexingService indexing) =>
{
    try
    {
        var job = await indexing.StartAsync(request);
        return Results.Accepted($"/api/index/jobs/{job.Id}", job);
    }
    catch (Exception ex) when (ex is InvalidOperationException or DirectoryNotFoundException)
    {
        return Results.BadRequest(new { message = ex.Message });
    }
});

api.MapPost("/index/batch", async (BatchIndexRequest request, IIndexingService indexing) =>
{
    try
    {
        var job = await indexing.StartBatchAsync(request);
        return Results.Accepted($"/api/index/batch/{job.Id}", job);
    }
    catch (Exception ex) when (ex is InvalidOperationException or DirectoryNotFoundException)
    {
        return Results.BadRequest(new { message = ex.Message });
    }
});

api.MapGet("/index/discover", (string parentFolderPath, bool? requireProjectMarkers, IIndexingService indexing) =>
{
    try
    {
        return Results.Ok(indexing.DiscoverProjects(parentFolderPath, requireProjectMarkers == true));
    }
    catch (Exception ex) when (ex is InvalidOperationException or DirectoryNotFoundException or ArgumentException)
    {
        return Results.BadRequest(new { message = ex.Message });
    }
});

api.MapGet("/index/batch/{id}", (string id, IIntelligenceStore store) =>
{
    var job = store.GetBatchJob(id);
    return job is null ? Results.NotFound() : Results.Ok(job);
});

api.MapGet("/tools", async (IIndexingService indexing) =>
    Results.Ok(await indexing.GetToolAvailabilityAsync()));

api.MapGet("/health", async (IPrerequisiteHealthService health) =>
{
    var prereqs = await health.CheckAsync();
    var payload = new
    {
        status = prereqs.Status,
        healthy = prereqs.Healthy,
        message = prereqs.Message,
        missing = prereqs.Missing,
        installHint = prereqs.InstallHint,
        prerequisites = prereqs
    };

    return prereqs.Healthy
        ? Results.Ok(payload)
        : Results.Json(payload, statusCode: StatusCodes.Status503ServiceUnavailable);
});

api.MapGet("/index/jobs/{id}", (string id, IIntelligenceStore store) =>
{
    var job = store.GetJob(id);
    return job is null ? Results.NotFound() : Results.Ok(job);
});

api.MapGet("/search", (string? q, IIntelligenceStore store) =>
{
    if (string.IsNullOrWhiteSpace(q))
        return Results.Ok(Array.Empty<SearchResultDto>());
    return Results.Ok(store.Search(q));
});

api.MapGet("/explore", (string? q, int? depth, IIntelligenceStore store, EvidenceGraphMerger merger) =>
{
    var graph = store.CurrentGraph;
    if (graph is null)
        return Results.Ok(merger.ToGraphifyDto(new EvidenceGraph()));
    if (string.IsNullOrWhiteSpace(q))
        return Results.Ok(merger.ToGraphifyDto(graph));

    var neighborhood = merger.ExploreNeighborhood(graph, q, depth ?? 1);
    return Results.Ok(merger.ToGraphifyDto(neighborhood));
});

api.MapGet("/nodes/{id}", (string id, IIntelligenceStore store) =>
{
    var graph = store.CurrentGraph;
    var node = graph?.FindNode(id);
    if (node is null)
        return Results.NotFound();

    var callers = graph!.Edges
        .Where(e => string.Equals(e.Target, id, StringComparison.OrdinalIgnoreCase))
        .Select(e => new { e.Source, Relation = e.Relation.ToString(), Confidence = e.Confidence.ToString() })
        .ToList();
    var callees = graph.Edges
        .Where(e => string.Equals(e.Source, id, StringComparison.OrdinalIgnoreCase))
        .Select(e => new { e.Target, Relation = e.Relation.ToString(), Confidence = e.Confidence.ToString() })
        .ToList();

    return Results.Ok(new { node, callers, callees });
});

api.MapGet("/nodes/{id}/callers", (string id, IIntelligenceStore store) =>
{
    var graph = store.CurrentGraph;
    if (graph is null)
        return Results.Ok(Array.Empty<object>());
    var result = graph.Edges
        .Where(e => string.Equals(e.Target, id, StringComparison.OrdinalIgnoreCase))
        .Select(e => graph.FindNode(e.Source))
        .Where(n => n is not null)
        .ToList();
    return Results.Ok(result);
});

api.MapGet("/nodes/{id}/callees", (string id, IIntelligenceStore store) =>
{
    var graph = store.CurrentGraph;
    if (graph is null)
        return Results.Ok(Array.Empty<object>());
    var result = graph.Edges
        .Where(e => string.Equals(e.Source, id, StringComparison.OrdinalIgnoreCase))
        .Select(e => graph.FindNode(e.Target))
        .Where(n => n is not null)
        .ToList();
    return Results.Ok(result);
});

api.MapGet("/nodes/{id}/impact", (string id, int? depth, IIntelligenceStore store, EvidenceGraphMerger merger) =>
{
    var graph = store.CurrentGraph;
    if (graph is null)
        return Results.Ok(merger.ToGraphifyDto(new EvidenceGraph()));
    if (graph.FindNode(id) is null)
        return Results.NotFound();
    var neighborhood = merger.ExploreNeighborhood(graph, id, depth ?? 2);
    return Results.Ok(merger.ToGraphifyDto(neighborhood));
});

api.MapGet("/graphs/unified", (string? kind, string? confidence, bool? codeToDbOnly, IIntelligenceStore store, EvidenceGraphMerger merger) =>
{
    var graph = store.CurrentGraph;
    if (graph is null)
        return Results.Ok(merger.ToGraphifyDto(new EvidenceGraph()));

    IEnumerable<GraphEdge> edges = graph.Edges;
    if (codeToDbOnly == true)
    {
        edges = edges.Where(e =>
            ProjectGraphIds.IsCodeNodeId(e.Source) &&
            ProjectGraphIds.IsDbNodeId(e.Target));
    }

    if (!string.IsNullOrWhiteSpace(confidence) &&
        Enum.TryParse<Confidence>(confidence, true, out var conf))
    {
        edges = edges.Where(e => e.Confidence == conf);
    }

    var edgeList = edges.ToList();
    var filtering = codeToDbOnly == true || !string.IsNullOrWhiteSpace(confidence);
    var nodeIds = edgeList.SelectMany(e => new[] { e.Source, e.Target }).ToHashSet(StringComparer.OrdinalIgnoreCase);

    IEnumerable<GraphNode> nodes = graph.Nodes;
    if (filtering && nodeIds.Count > 0)
        nodes = nodes.Where(n => nodeIds.Contains(n.Id));
    if (!string.IsNullOrWhiteSpace(kind) && Enum.TryParse<NodeKind>(kind, true, out var nk))
        nodes = nodes.Where(n => n.Kind == nk);

    var filtered = new EvidenceGraph { Meta = graph.Meta };
    foreach (var n in nodes)
        filtered.UpsertNode(n);

    foreach (var e in edgeList.Where(e => filtered.FindNode(e.Source) is not null && filtered.FindNode(e.Target) is not null))
        filtered.UpsertEdge(e);

    // When not filtering edges, include all edges among selected nodes.
    if (!filtering)
    {
        foreach (var e in graph.Edges.Where(e => filtered.FindNode(e.Source) is not null && filtered.FindNode(e.Target) is not null))
            filtered.UpsertEdge(e);
    }

    return Results.Ok(merger.ToGraphifyDto(filtered));
});

api.MapGet("/maps/code-to-db", (IIntelligenceStore store, EvidenceGraphMerger merger) =>
{
    var graph = store.CurrentGraph ?? new EvidenceGraph();
    return Results.Ok(merger.ToCodeToDbMap(graph));
});

api.MapGet("/maps/code-references", (IIntelligenceStore store, EvidenceGraphMerger merger) =>
{
    var graph = store.CurrentGraph ?? new EvidenceGraph();
    return Results.Ok(merger.ToCodeReferenceLocations(graph));
});

api.MapGet("/maps/stored-procedures", (IIntelligenceStore store, EvidenceGraphMerger merger) =>
{
    var graph = store.CurrentGraph ?? new EvidenceGraph();
    return Results.Ok(merger.ToStoredProcedureMap(graph));
});

api.MapPost("/export", async (ExportRequest request, IIntelligenceStore store, IOptions<DbIntelligenceOptions> options) =>
{
    var graph = store.CurrentGraph;
    if (graph is null)
        return Results.BadRequest(new { message = "No graph loaded. Run an index job first." });

    string output;
    if (!string.IsNullOrWhiteSpace(request.OutputDirectory))
    {
        output = Path.GetFullPath(request.OutputDirectory!);
    }
    else if (!string.IsNullOrWhiteSpace(graph.Meta.TargetRepositoryPath))
    {
        output = ProjectFolderDiscovery.ResolveArtifactsDirectory(
            graph.Meta.TargetRepositoryPath,
            options.Value.ArtifactsDirectory);
    }
    else
    {
        output = Path.GetFullPath(options.Value.ArtifactsDirectory);
    }

    await store.ExportAsync(graph, output);
    return Results.Ok(new { outputDirectory = output });
});

// Builds a downloadable promote-request JSON only — never shells out to FindingsMigration.
api.MapPost("/findings/promote", (PromoteFindingsRequest request, IIntelligenceStore store) =>
{
    if (string.IsNullOrWhiteSpace(request.DomainName))
        return Results.BadRequest(new { message = "DomainName is required." });
    if (request.SelectedRows is null || request.SelectedRows.Count == 0)
        return Results.BadRequest(new { message = "Select at least one Code→DB or References row." });

    var domain = request.DomainName.Trim();
    var owner = string.IsNullOrWhiteSpace(request.OwnerTeam) ? "TBD" : request.OwnerTeam.Trim();
    var outHint = string.IsNullOrWhiteSpace(request.SuggestedOutputPath)
        ? Path.Combine("src-templates", "FindingsMigration", "out", domain)
        : request.SuggestedOutputPath.Trim();

    var skippedAmbiguous = 0;
    var entries = new List<CodeToDbEntryDto>();
    foreach (var row in request.SelectedRows)
    {
        var confidence = string.IsNullOrWhiteSpace(row.Confidence) ? "EXTRACTED" : row.Confidence.Trim();
        if (!request.IncludeAmbiguous &&
            confidence.Equals("AMBIGUOUS", StringComparison.OrdinalIgnoreCase))
        {
            skippedAmbiguous++;
            continue;
        }

        var dbObject = row.DbObject?.Trim() ?? "";
        if (string.IsNullOrWhiteSpace(dbObject))
            continue;

        entries.Add(new CodeToDbEntryDto
        {
            CodeNodeId = row.CodeNodeId ?? "",
            CodeLabel = row.CodeLabel ?? row.CodeNodeId ?? "",
            SourceFile = row.SourceFile,
            SourceFileFullPath = row.SourceFileFullPath,
            Line = row.Line,
            Location = row.Location,
            DbNodeId = row.DbNodeId ?? "",
            DbObject = dbObject,
            DbKind = row.DbKind ?? "",
            Relation = string.IsNullOrWhiteSpace(row.Relation) ? "references" : row.Relation,
            Confidence = confidence,
            Pattern = row.Pattern,
            Project = row.Project
        });
    }

    if (entries.Count == 0)
        return Results.BadRequest(new
        {
            message = skippedAmbiguous > 0
                ? "No rows packaged. AMBIGUOUS selections were skipped; enable IncludeAmbiguous or pick EXTRACTED/INFERRED rows."
                : "No rows packaged. Selected rows need a DB object."
        });

    var repoPath = store.CurrentGraph?.Meta.TargetRepositoryPath;
    var mapFileName = $"promote-{domain}-code-to-db-map.json";
    var ps = $"""
        # 1) Save the promote response body (or its codeToDbMap) as {mapFileName}
        # 2) From the kit root, package locally — API does not run this:
        cd src-templates\FindingsMigration
        dotnet run --project FindingsMigration.Cli -- `
          --code-to-db ".\{mapFileName}" `
          --domain "{domain}" `
          --owner "{owner}" `
          --out "{outHint}"
        """;

    var response = new PromoteFindingsResponse
    {
        SchemaVersion = "1.0",
        DomainName = domain,
        SuggestedOutputPath = outHint,
        OwnerTeam = owner,
        GeneratedAt = DateTimeOffset.UtcNow,
        RepositoryPath = repoPath,
        SelectedCount = request.SelectedRows.Count,
        PackagedCount = entries.Count,
        SkippedAmbiguousCount = skippedAmbiguous,
        CodeToDbMap = new CodeToDbMapDto
        {
            GeneratedAt = DateTimeOffset.UtcNow,
            RepositoryPath = repoPath,
            Entries = entries
        },
        PowerShellCommand = ps.Trim(),
        Instructions =
            "Download this JSON, extract codeToDbMap to a file, then run FindingsMigration.Cli locally. " +
            "DbIntelligence.Api never shells out. AMBIGUOUS findings stay review-only unless IncludeAmbiguous was set. " +
            "Scaffold from ShowcaseDataService — not CustomerDataService."
    };

    return Results.Ok(response);
});

api.MapPost("/graphs/combine", async (CombineGraphsRequest request, ICombinedGraphService combined) =>
{
    try
    {
        var result = await combined.CombineFromParentAsync(request);
        return Results.Ok(result);
    }
    catch (Exception ex) when (ex is InvalidOperationException or DirectoryNotFoundException or ArgumentException)
    {
        return Results.BadRequest(new { message = ex.Message });
    }
});

api.MapGet("/codegraph/query", async (string? q, string? repositoryPath, ICodegraphClient codegraph, IOptions<DbIntelligenceOptions> options) =>
{
    if (string.IsNullOrWhiteSpace(q))
        return Results.BadRequest(new { message = "q is required" });

    var repo = string.IsNullOrWhiteSpace(repositoryPath)
        ? options.Value.TargetRepositoryPath
        : repositoryPath;
    if (string.IsNullOrWhiteSpace(repo))
        return Results.BadRequest(new { message = "repositoryPath (or DbIntelligence:TargetRepositoryPath) is required." });
    if (!Directory.Exists(repo))
        return Results.BadRequest(new { message = $"Repository path not found: {repo}" });

    var result = await codegraph.QueryAsync(Path.GetFullPath(repo), q);
    return Results.Ok(new { result.Succeeded, result.ExitCode, output = result.StandardOutput, error = result.StandardError });
});

app.MapFallbackToFile("index.html");
app.Run();

public partial class Program;
