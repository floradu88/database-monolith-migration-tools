using System.Text.Json;
using DbIntelligence.Contracts;
using DbIntelligence.Domain;
using DbIntelligence.Infrastructure.Codegraph;
using DbIntelligence.Infrastructure.Graphify;
using DbIntelligence.RepositoryScanner;
using DbIntelligence.SqlScanner;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DbIntelligence.Infrastructure;

public interface IIndexingService
{
    Task<IndexJobStatusDto> StartAsync(IndexJobRequest request, CancellationToken cancellationToken = default);
    Task RunJobAsync(string jobId, IndexJobRequest request, CancellationToken cancellationToken = default);
    Task<BatchIndexJobStatusDto> StartBatchAsync(BatchIndexRequest request, CancellationToken cancellationToken = default);
    Task RunBatchAsync(string batchJobId, BatchIndexRequest request, CancellationToken cancellationToken = default);
    DiscoveredProjectsDto DiscoverProjects(string parentFolderPath, bool requireProjectMarkers = false);
    Task<ToolAvailabilityDto> GetToolAvailabilityAsync(CancellationToken cancellationToken = default);
}

public sealed class IndexingService : IIndexingService
{
    private static readonly JsonSerializerOptions SummaryJson = new() { WriteIndented = true };

    private readonly IIntelligenceStore _store;
    private readonly ICodegraphClient _codegraph;
    private readonly IGraphifyClient _graphify;
    private readonly IPrerequisiteHealthService _prerequisites;
    private readonly RepositoryScannerService _repositoryScanner;
    private readonly SqlScannerService _sqlScanner;
    private readonly EvidenceGraphMerger _merger;
    private readonly ICombinedGraphService _combinedGraphs;
    private readonly DbIntelligenceOptions _options;
    private readonly ILogger<IndexingService> _logger;

    public IndexingService(
        IIntelligenceStore store,
        ICodegraphClient codegraph,
        IGraphifyClient graphify,
        IPrerequisiteHealthService prerequisites,
        RepositoryScannerService repositoryScanner,
        SqlScannerService sqlScanner,
        EvidenceGraphMerger merger,
        ICombinedGraphService combinedGraphs,
        IOptions<DbIntelligenceOptions> options,
        ILogger<IndexingService> logger)
    {
        _store = store;
        _codegraph = codegraph;
        _graphify = graphify;
        _prerequisites = prerequisites;
        _repositoryScanner = repositoryScanner;
        _sqlScanner = sqlScanner;
        _merger = merger;
        _combinedGraphs = combinedGraphs;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<ToolAvailabilityDto> GetToolAvailabilityAsync(CancellationToken cancellationToken = default)
    {
        var prereqs = await _prerequisites.CheckAsync(cancellationToken);
        return new ToolAvailabilityDto
        {
            CodegraphAvailable = prereqs.Codegraph.Available,
            GraphifyAvailable = prereqs.Graphify.Available,
            PythonAvailable = prereqs.Python.Available,
            PipAvailable = prereqs.Pip.Available,
            Healthy = prereqs.Healthy,
            CodegraphExecutable = _options.CodegraphExecutable,
            GraphifyExecutable = _options.GraphifyExecutable,
            Message = prereqs.Message,
            Prerequisites = prereqs
        };
    }

    public DiscoveredProjectsDto DiscoverProjects(string parentFolderPath, bool requireProjectMarkers = false)
    {
        var parent = Path.GetFullPath(parentFolderPath);
        var found = ProjectFolderDiscovery.Discover(parent, requireProjectMarkers);
        return new DiscoveredProjectsDto
        {
            ParentFolderPath = parent,
            Projects = found.Select(f => new DiscoveredProjectDto
            {
                Name = f.Name,
                Path = f.Path,
                HasProjectMarker = f.HasMarker
            }).ToList()
        };
    }

    public Task<IndexJobStatusDto> StartAsync(IndexJobRequest request, CancellationToken cancellationToken = default)
    {
        ResolveRepositoryPath(request);
        var job = _store.CreateJob(request);
        _ = Task.Run(() => RunJobAsync(job.Id, request, CancellationToken.None), CancellationToken.None);
        return Task.FromResult(job);
    }

    public Task<BatchIndexJobStatusDto> StartBatchAsync(BatchIndexRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.ParentFolderPath))
            throw new InvalidOperationException("ParentFolderPath is required.");

        var parent = Path.GetFullPath(request.ParentFolderPath);
        if (!Directory.Exists(parent))
            throw new DirectoryNotFoundException($"Parent folder not found: {parent}");

        request.ParentFolderPath = parent;
        var discovered = ProjectFolderDiscovery.Discover(
            parent,
            request.RequireProjectMarkers,
            request.ProjectFolderNames);

        if (discovered.Count == 0)
        {
            throw new InvalidOperationException(
                $"No project folders found under '{parent}'. " +
                "Each immediate child folder is treated as a project (hidden/system folders skipped).");
        }

        var job = _store.CreateBatchJob(request);
        job.TotalProjects = discovered.Count;
        job.Projects = discovered.Select(d => new BatchProjectResultDto
        {
            Name = d.Name,
            Path = d.Path,
            Status = "Pending"
        }).ToList();
        _store.UpdateBatchJob(job);

        _ = Task.Run(() => RunBatchAsync(job.Id, request, CancellationToken.None), CancellationToken.None);
        return Task.FromResult(job);
    }

    public async Task RunBatchAsync(string batchJobId, BatchIndexRequest request, CancellationToken cancellationToken = default)
    {
        var batch = _store.GetBatchJob(batchJobId)
            ?? new BatchIndexJobStatusDto { Id = batchJobId, CreatedAt = DateTimeOffset.UtcNow };
        batch.Status = "Running";
        batch.Phase = "batch";
        batch.Message = $"Indexing {batch.Projects.Count} projects under {request.ParentFolderPath}";
        BatchLog(batch, batch.Message);
        _store.UpdateBatchJob(batch);

        try
        {
            foreach (var project in batch.Projects)
            {
                cancellationToken.ThrowIfCancellationRequested();
                batch.CurrentProject = project.Name;
                batch.Phase = $"project:{project.Name}";
                batch.Message = $"Indexing {project.Name}...";
                project.Status = "Running";
                project.StartedAt = DateTimeOffset.UtcNow;
                project.Message = "Running";
                BatchLog(batch, $"--- Begin {project.Name} ({project.Path}) ---");
                _store.UpdateBatchJob(batch);

                try
                {
                    var childRequest = new IndexJobRequest
                    {
                        TargetRepositoryPath = project.Path,
                        RunCodegraph = request.RunCodegraph,
                        RunGraphify = request.RunGraphify,
                        RefreshGraphify = request.RefreshGraphify,
                        RunRepositoryScan = request.RunRepositoryScan,
                        RunSqlScan = request.RunSqlScan,
                        SqlConnectionString = request.SqlConnectionString,
                        ArtifactsRelativeDirectory = request.ArtifactsRelativeDirectory
                            ?? DbIntelligenceOptions.DefaultArtifactsDirectory
                    };

                    var childJob = _store.CreateJob(childRequest);
                    await RunJobAsync(childJob.Id, childRequest, cancellationToken);
                    var finished = _store.GetJob(childJob.Id);

                    if (finished?.Status == "Completed")
                    {
                        var artifactsDir = ProjectFolderDiscovery.ResolveArtifactsDirectory(
                            project.Path, childRequest.ArtifactsRelativeDirectory);
                        project.Status = "Completed";
                        project.Message = finished.Message;
                        project.ArtifactsDirectory = artifactsDir;
                        project.CompletedAt = DateTimeOffset.UtcNow;

                        // Prefer counts from live graph when this was the last SetGraph.
                        var graph = _store.CurrentGraph;
                        if (graph is not null &&
                            string.Equals(graph.Meta.TargetRepositoryPath, project.Path, StringComparison.OrdinalIgnoreCase))
                        {
                            project.NodeCount = graph.Nodes.Count;
                            project.EdgeCount = graph.Edges.Count;
                        }

                        batch.CompletedProjects++;
                        BatchLog(batch, $"Completed {project.Name}: {finished.Message}");
                    }
                    else
                    {
                        project.Status = "Failed";
                        project.Message = finished?.Message ?? "Unknown failure";
                        project.CompletedAt = DateTimeOffset.UtcNow;
                        batch.FailedProjects++;
                        BatchLog(batch, $"Failed {project.Name}: {project.Message}");
                        if (!request.ContinueOnError)
                            break;
                    }
                }
                catch (Exception ex)
                {
                    project.Status = "Failed";
                    project.Message = ex.Message;
                    project.CompletedAt = DateTimeOffset.UtcNow;
                    batch.FailedProjects++;
                    BatchLog(batch, $"Failed {project.Name}: {ex.Message}");
                    _logger.LogError(ex, "Batch project {Project} failed", project.Name);
                    if (!request.ContinueOnError)
                        break;
                }

                _store.UpdateBatchJob(batch);
            }

            await WriteBatchSummaryAsync(request.ParentFolderPath, batch, cancellationToken);

            if (batch.CompletedProjects > 0)
            {
                try
                {
                    batch.Phase = "combine";
                    batch.Message = "Combining per-project graph.json files into one live graph...";
                    _store.UpdateBatchJob(batch);
                    var combined = await _combinedGraphs.CombineFromParentAsync(
                        new CombineGraphsRequest
                        {
                            ParentFolderPath = request.ParentFolderPath,
                            ArtifactsRelativeDirectory = request.ArtifactsRelativeDirectory
                                ?? DbIntelligenceOptions.DefaultArtifactsDirectory,
                            RequireProjectMarkers = request.RequireProjectMarkers,
                            ProjectFolderNames = request.ProjectFolderNames,
                            ShareDatabaseNodes = true,
                            OnlyCompletedFromSummary = true,
                            ExportCombined = true
                        },
                        cancellationToken);
                    BatchLog(batch,
                        $"Combined {combined.ProjectsLoaded} project(s) → {combined.NodeCount} nodes / {combined.EdgeCount} edges" +
                        (combined.CombinedOutputDirectory is null
                            ? ""
                            : $" (exported to {combined.CombinedOutputDirectory})"));
                }
                catch (Exception ex)
                {
                    BatchLog(batch, $"Combine skipped: {ex.Message}");
                    _logger.LogWarning(ex, "Post-batch combine failed for {Parent}", request.ParentFolderPath);
                }
            }

            batch.Status = batch.FailedProjects > 0 && batch.CompletedProjects == 0 ? "Failed" : "Completed";
            batch.Phase = "done";
            batch.CurrentProject = null;
            batch.Message =
                $"Batch done under '{request.ParentFolderPath}': {batch.CompletedProjects} ok, {batch.FailedProjects} failed, {batch.TotalProjects} total.";
            batch.CompletedAt = DateTimeOffset.UtcNow;
            BatchLog(batch, batch.Message);
            _store.UpdateBatchJob(batch);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Batch job {JobId} failed", batchJobId);
            batch.Status = "Failed";
            batch.Message = ex.Message;
            batch.CompletedAt = DateTimeOffset.UtcNow;
            batch.Log.Add(ex.ToString());
            _store.UpdateBatchJob(batch);
        }
    }

    public async Task RunJobAsync(string jobId, IndexJobRequest request, CancellationToken cancellationToken = default)
    {
        var job = _store.GetJob(jobId) ?? new IndexJobStatusDto { Id = jobId, CreatedAt = DateTimeOffset.UtcNow };
        job.Status = "Running";
        _store.UpdateJob(job);

        try
        {
            var repo = ResolveRepositoryPath(request);
            Log(job, $"Target repository: {repo}");

            if (request.RunCodegraph || request.RunGraphify)
            {
                await SetPhase(job, "tools", "Verifying CLI prerequisites (python/graphify/codegraph)...");
                var tools = await GetToolAvailabilityAsync(cancellationToken);

                if (request.RunGraphify && !tools.PythonAvailable)
                {
                    throw new InvalidOperationException(
                        "Python is not installed or not on PATH (required by Graphify). " +
                        "Run: dotnet run --project src-templates/DbIntelligence/DbIntelligence.Cli -- --install-preqs");
                }

                if (request.RunCodegraph && !tools.CodegraphAvailable)
                {
                    throw new InvalidOperationException(
                        $"Required CLI '{_options.CodegraphExecutable}' was not found on PATH. " +
                        "Prefer: fnm exec --using=lts-latest -- npm i -g @colbymchenry/codegraph  " +
                        "(or scripts/Install-DbIntelligencePrereqs.ps1 -Yes / DbIntelligence.Cli --install-preqs).");
                }

                if (request.RunGraphify && !tools.GraphifyAvailable)
                {
                    throw new InvalidOperationException(
                        $"Required CLI '{_options.GraphifyExecutable}' was not found on PATH. " +
                        "Install Graphify after Python: python -m pip install graphifyy " +
                        "(or run DbIntelligence.Cli --install-preqs).");
                }

                Log(job, tools.Message ?? "CLI tools verified.");
            }

            var parts = new List<EvidenceGraph?>();

            if (request.RunCodegraph)
            {
                await SetPhase(job, "codegraph", $"Running codegraph against {repo}...");
                var ensure = await _codegraph.EnsureIndexAsync(repo, cancellationToken);
                if (!ensure.Succeeded)
                {
                    throw new InvalidOperationException(
                        $"codegraph failed for '{repo}' (exit {ensure.ExitCode}): {Trim(ensure.StandardError)}");
                }

                Log(job, "Codegraph index ready.");
                parts.Add(await _codegraph.ImportStatusAsGraphAsync(repo, cancellationToken));
            }

            if (request.RunGraphify)
            {
                var graphifyJson = Path.Combine(repo, "graphify-out", "graph.json");
                var canReuse = !request.RefreshGraphify && File.Exists(graphifyJson);
                if (canReuse)
                {
                    await SetPhase(job, "graphify", $"Reusing existing {graphifyJson}...");
                    Log(job, $"Skipped graphify extract (RefreshGraphify=false; file length {new FileInfo(graphifyJson).Length} bytes).");
                }
                else
                {
                    await SetPhase(job, "graphify", $"Running graphify against {repo}...");
                    var run = await _graphify.RunAsync(repo, updateOnly: false, cancellationToken);
                    if (!run.Succeeded)
                    {
                        throw new InvalidOperationException(
                            $"graphify failed for '{repo}' (exit {run.ExitCode}): {Trim(run.StandardError)}");
                    }

                    Log(job, "Graphify completed.");
                }

                var imported = await _graphify.ImportGraphJsonAsync(repo, cancellationToken)
                    ?? throw new InvalidOperationException(
                        $"graphify finished but '{graphifyJson}' was not found.");
                parts.Add(imported);
                Log(job, $"Imported Graphify graph ({imported.Nodes.Count} nodes / {imported.Edges.Count} edges).");
            }

            if (request.RunRepositoryScan)
            {
                await SetPhase(job, "repository-scan", $"Scanning {repo} for SQL access patterns...");
                var findings = await _repositoryScanner.ScanAsync(repo, cancellationToken);
                var repoGraph = _repositoryScanner.ToGraph(findings);
                parts.Add(repoGraph);
                Log(job, $"Repository scan found {findings.Count} references.");
            }

            var sqlConnection = request.SqlConnectionString ?? _options.SqlConnectionString;
            if (request.RunSqlScan)
            {
                await SetPhase(job, "sql-scan", "Scanning SQL Server inventory...");
                if (string.IsNullOrWhiteSpace(sqlConnection))
                {
                    throw new InvalidOperationException(
                        "SQL scan was requested but no SqlConnectionString was provided.");
                }

                var sqlGraph = await _sqlScanner.ScanAsync(sqlConnection, cancellationToken);
                parts.Add(sqlGraph);
                Log(job, $"SQL scan imported {sqlGraph.Nodes.Count} objects.");
            }

            await SetPhase(job, "merge", "Merging evidence graphs...");
            var merged = _merger.Merge(parts.ToArray());
            merged.Meta.TargetRepositoryPath = repo;
            _store.SetGraph(merged);

            var relativeArtifacts = request.ArtifactsRelativeDirectory;
            if (relativeArtifacts is null)
                relativeArtifacts = _options.ArtifactsDirectory;

            var output = ProjectFolderDiscovery.ResolveArtifactsDirectory(repo, relativeArtifacts);
            await SetPhase(job, "export", $"Exporting artifacts to {output}...");
            await _store.ExportAsync(merged, output, cancellationToken);

            job.Status = "Completed";
            job.Phase = "done";
            job.Message = $"Indexed '{repo}' — {merged.Nodes.Count} nodes / {merged.Edges.Count} edges. Artifacts: {output}";
            job.CompletedAt = DateTimeOffset.UtcNow;
            _store.UpdateJob(job);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Index job {JobId} failed", jobId);
            job.Status = "Failed";
            job.Message = ex.Message;
            job.CompletedAt = DateTimeOffset.UtcNow;
            job.Log.Add(ex.ToString());
            _store.UpdateJob(job);
        }
    }

    private async Task WriteBatchSummaryAsync(
        string parentFolder,
        BatchIndexJobStatusDto batch,
        CancellationToken cancellationToken)
    {
        var summaryPath = Path.Combine(parentFolder, "db-intelligence-batch-summary.json");
        var payload = new
        {
            generatedAt = DateTimeOffset.UtcNow,
            parentFolder,
            batch.Id,
            batch.Status,
            batch.TotalProjects,
            batch.CompletedProjects,
            batch.FailedProjects,
            projects = batch.Projects
        };

        await File.WriteAllTextAsync(
            summaryPath,
            JsonSerializer.Serialize(payload, SummaryJson),
            cancellationToken);
        BatchLog(batch, $"Wrote batch summary: {summaryPath}");
    }

    private string ResolveRepositoryPath(IndexJobRequest request)
    {
        var raw = string.IsNullOrWhiteSpace(request.TargetRepositoryPath)
            ? _options.TargetRepositoryPath
            : request.TargetRepositoryPath;

        if (string.IsNullOrWhiteSpace(raw))
        {
            throw new InvalidOperationException(
                "TargetRepositoryPath is required. Pass the repository folder path to analyze " +
                "(or set DbIntelligence:TargetRepositoryPath).");
        }

        var full = Path.GetFullPath(raw);
        if (!Directory.Exists(full))
            throw new DirectoryNotFoundException($"Target repository path not found: {full}");

        request.TargetRepositoryPath = full;
        return full;
    }

    private Task SetPhase(IndexJobStatusDto job, string phase, string message)
    {
        job.Phase = phase;
        job.Message = message;
        Log(job, message);
        _store.UpdateJob(job);
        return Task.CompletedTask;
    }

    private void Log(IndexJobStatusDto job, string message)
    {
        var line = $"{DateTimeOffset.UtcNow:O} {message}";
        job.Log.Add(line);
        _logger.LogInformation("{JobId}: {Message}", job.Id, message);
    }

    private void BatchLog(BatchIndexJobStatusDto job, string message)
    {
        var line = $"{DateTimeOffset.UtcNow:O} {message}";
        job.Log.Add(line);
        _logger.LogInformation("batch {JobId}: {Message}", job.Id, message);
    }

    private static string Trim(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "(no output)" :
        value.Length <= 2000 ? value.Trim() : value.Trim()[..2000];
}
