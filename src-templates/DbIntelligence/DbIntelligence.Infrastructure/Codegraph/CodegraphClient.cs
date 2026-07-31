using System.Text.Json;
using DbIntelligence.Domain;
using Microsoft.Extensions.Options;

namespace DbIntelligence.Infrastructure.Codegraph;

public interface ICodegraphClient
{
    Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default);
    Task<CliResult> EnsureIndexAsync(string repositoryPath, CancellationToken cancellationToken = default);
    Task<CliResult> QueryAsync(string repositoryPath, string search, CancellationToken cancellationToken = default);
    Task<CliResult> ExploreAsync(string repositoryPath, string query, CancellationToken cancellationToken = default);
    Task<EvidenceGraph> ImportStatusAsGraphAsync(string repositoryPath, CancellationToken cancellationToken = default);
}

public sealed class CodegraphClient : ICodegraphClient
{
    private readonly CliProcessRunner _runner;
    private readonly DbIntelligenceOptions _options;

    public CodegraphClient(CliProcessRunner runner, IOptions<DbIntelligenceOptions> options)
    {
        _runner = runner;
        _options = options.Value;
    }

    public async Task<bool> IsAvailableAsync(CancellationToken cancellationToken = default)
    {
        var result = await _runner.RunAsync(_options.CodegraphExecutable, ["-V"], timeoutSeconds: 30, cancellationToken: cancellationToken);
        var text = $"{result.StandardOutput}\n{result.StandardError}";
        return result.Succeeded || System.Text.RegularExpressions.Regex.IsMatch(text, @"\d+\.\d+");
    }

    public async Task<CliResult> EnsureIndexAsync(string repositoryPath, CancellationToken cancellationToken = default)
    {
        var status = await _runner.RunAsync(
            _options.CodegraphExecutable,
            ["status", repositoryPath],
            workingDirectory: repositoryPath,
            timeoutSeconds: _options.ProcessTimeoutSeconds,
            cancellationToken: cancellationToken);

        if (status.Succeeded && status.StandardOutput.Contains(".codegraph", StringComparison.OrdinalIgnoreCase))
        {
            return await _runner.RunAsync(
                _options.CodegraphExecutable,
                ["sync", repositoryPath],
                workingDirectory: repositoryPath,
                timeoutSeconds: _options.ProcessTimeoutSeconds,
                cancellationToken: cancellationToken);
        }

        return await _runner.RunAsync(
            _options.CodegraphExecutable,
            ["init", repositoryPath],
            workingDirectory: repositoryPath,
            timeoutSeconds: _options.ProcessTimeoutSeconds,
            cancellationToken: cancellationToken);
    }

    public Task<CliResult> QueryAsync(string repositoryPath, string search, CancellationToken cancellationToken = default) =>
        _runner.RunAsync(
            _options.CodegraphExecutable,
            ["query", search, "--json"],
            workingDirectory: repositoryPath,
            timeoutSeconds: _options.ProcessTimeoutSeconds,
            cancellationToken: cancellationToken);

    public Task<CliResult> ExploreAsync(string repositoryPath, string query, CancellationToken cancellationToken = default) =>
        // Older Codegraph builds expose `query` rather than `explore`.
        _runner.RunAsync(
            _options.CodegraphExecutable,
            ["query", query, "--json"],
            workingDirectory: repositoryPath,
            timeoutSeconds: _options.ProcessTimeoutSeconds,
            cancellationToken: cancellationToken);

    public async Task<EvidenceGraph> ImportStatusAsGraphAsync(string repositoryPath, CancellationToken cancellationToken = default)
    {
        var graph = new EvidenceGraph();
        graph.Meta.Sources.Add("codegraph");
        graph.Meta.TargetRepositoryPath = repositoryPath;

        var status = await _runner.RunAsync(
            _options.CodegraphExecutable,
            ["status", repositoryPath],
            workingDirectory: repositoryPath,
            timeoutSeconds: 60,
            cancellationToken: cancellationToken);

        graph.UpsertNode(new GraphNode
        {
            Id = GraphIds.Concept($"codegraph:{Path.GetFileName(repositoryPath)}"),
            Label = $"Codegraph:{Path.GetFileName(repositoryPath)}",
            Kind = NodeKind.Application,
            SourceFile = repositoryPath,
            Community = "codegraph",
            Properties =
            {
                ["status"] = status.Succeeded ? "ready" : "unavailable",
                ["detail"] = Truncate(status.StandardOutput + status.StandardError, 2000)
            }
        });

        // Best-effort: if query --json returns an array of symbols, import them.
        var query = await QueryAsync(repositoryPath, "*", cancellationToken);
        if (query.Succeeded && LooksLikeJson(query.StandardOutput))
        {
            try
            {
                using var doc = JsonDocument.Parse(query.StandardOutput);
                if (doc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (var el in doc.RootElement.EnumerateArray())
                    {
                        var name = el.TryGetProperty("name", out var n) ? n.GetString()
                            : el.TryGetProperty("label", out var l) ? l.GetString()
                            : el.TryGetProperty("id", out var i) ? i.GetString()
                            : null;
                        if (string.IsNullOrWhiteSpace(name))
                            continue;

                        var file = el.TryGetProperty("file", out var f) ? f.GetString()
                            : el.TryGetProperty("path", out var p) ? p.GetString()
                            : null;

                        graph.UpsertNode(new GraphNode
                        {
                            Id = GraphIds.CodeType(name),
                            Label = name,
                            Kind = NodeKind.Type,
                            SourceFile = file,
                            Community = "codegraph"
                        });
                    }
                }
            }
            catch (JsonException)
            {
                // Schema varies by Codegraph version; status node remains.
            }
        }

        return graph;
    }

    private static bool LooksLikeJson(string text)
    {
        var trimmed = text.TrimStart();
        return trimmed.StartsWith('{') || trimmed.StartsWith('[');
    }

    private static string Truncate(string value, int max) =>
        value.Length <= max ? value : value[..max];
}
