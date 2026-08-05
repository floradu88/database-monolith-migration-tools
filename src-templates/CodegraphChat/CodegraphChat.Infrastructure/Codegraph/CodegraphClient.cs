using Microsoft.Extensions.Options;

namespace CodegraphChat.Infrastructure.Codegraph;

public interface ICodegraphClient
{
    Task<(bool Available, string Detail)> CheckAsync(CancellationToken cancellationToken = default);
    Task<CliResult> StatusAsync(string repositoryPath, CancellationToken cancellationToken = default);
    Task<CliResult> EnsureIndexAsync(string repositoryPath, CancellationToken cancellationToken = default);
    Task<CliResult> QueryAsync(string repositoryPath, string search, int limit, CancellationToken cancellationToken = default);
    Task<CliResult> CallersAsync(string repositoryPath, string symbol, int limit, CancellationToken cancellationToken = default);
    Task<CliResult> CalleesAsync(string repositoryPath, string symbol, int limit, CancellationToken cancellationToken = default);
    Task<CliResult> ImpactAsync(string repositoryPath, string symbol, int depth, CancellationToken cancellationToken = default);
    Task<CliResult> FilesAsync(string repositoryPath, CancellationToken cancellationToken = default);
}

public sealed class CodegraphClient : ICodegraphClient
{
    private readonly CliProcessRunner _runner;
    private readonly CodegraphChatOptions _options;

    public CodegraphClient(CliProcessRunner runner, IOptions<CodegraphChatOptions> options)
    {
        _runner = runner;
        _options = options.Value;
    }

    public async Task<(bool Available, string Detail)> CheckAsync(CancellationToken cancellationToken = default)
    {
        var result = await _runner.RunAsync(
            _options.CodegraphExecutable,
            ["-V"],
            timeoutSeconds: 30,
            cancellationToken: cancellationToken);
        var text = $"{result.StandardOutput}\n{result.StandardError}".Trim();
        var available = result.Succeeded || System.Text.RegularExpressions.Regex.IsMatch(text, @"\d+\.\d+");
        return (available, string.IsNullOrWhiteSpace(text) ? (available ? "ok" : "not found") : text);
    }

    public Task<CliResult> StatusAsync(string repositoryPath, CancellationToken cancellationToken = default) =>
        _runner.RunAsync(
            _options.CodegraphExecutable,
            ["status", repositoryPath, "--json"],
            workingDirectory: repositoryPath,
            timeoutSeconds: _options.ProcessTimeoutSeconds,
            cancellationToken: cancellationToken);

    public async Task<CliResult> EnsureIndexAsync(string repositoryPath, CancellationToken cancellationToken = default)
    {
        var status = await StatusAsync(repositoryPath, cancellationToken);
        var hasIndex = Directory.Exists(Path.Combine(repositoryPath, ".codegraph")) ||
                       (status.Succeeded &&
                        (status.StandardOutput.Contains(".codegraph", StringComparison.OrdinalIgnoreCase)
                         || status.StandardOutput.Contains("indexed", StringComparison.OrdinalIgnoreCase)
                         || LooksLikeJson(status.StandardOutput)));

        if (hasIndex)
        {
            return await _runner.RunAsync(
                _options.CodegraphExecutable,
                ["sync", repositoryPath],
                workingDirectory: repositoryPath,
                timeoutSeconds: Math.Max(_options.ProcessTimeoutSeconds, 300),
                cancellationToken: cancellationToken);
        }

        return await _runner.RunAsync(
            _options.CodegraphExecutable,
            ["init", repositoryPath],
            workingDirectory: repositoryPath,
            timeoutSeconds: Math.Max(_options.ProcessTimeoutSeconds, 600),
            cancellationToken: cancellationToken);
    }

    private static bool LooksLikeJson(string text)
    {
        var trimmed = text.TrimStart();
        return trimmed.StartsWith('{') || trimmed.StartsWith('[');
    }

    public Task<CliResult> QueryAsync(string repositoryPath, string search, int limit, CancellationToken cancellationToken = default) =>
        _runner.RunAsync(
            _options.CodegraphExecutable,
            ["query", search, "--path", repositoryPath, "--limit", limit.ToString(), "--json"],
            workingDirectory: repositoryPath,
            timeoutSeconds: _options.ProcessTimeoutSeconds,
            cancellationToken: cancellationToken);

    public Task<CliResult> CallersAsync(string repositoryPath, string symbol, int limit, CancellationToken cancellationToken = default) =>
        _runner.RunAsync(
            _options.CodegraphExecutable,
            ["callers", symbol, "--path", repositoryPath, "--limit", limit.ToString(), "--json"],
            workingDirectory: repositoryPath,
            timeoutSeconds: _options.ProcessTimeoutSeconds,
            cancellationToken: cancellationToken);

    public Task<CliResult> CalleesAsync(string repositoryPath, string symbol, int limit, CancellationToken cancellationToken = default) =>
        _runner.RunAsync(
            _options.CodegraphExecutable,
            ["callees", symbol, "--path", repositoryPath, "--limit", limit.ToString(), "--json"],
            workingDirectory: repositoryPath,
            timeoutSeconds: _options.ProcessTimeoutSeconds,
            cancellationToken: cancellationToken);

    public Task<CliResult> ImpactAsync(string repositoryPath, string symbol, int depth, CancellationToken cancellationToken = default) =>
        _runner.RunAsync(
            _options.CodegraphExecutable,
            ["impact", symbol, "--path", repositoryPath, "--depth", depth.ToString(), "--json"],
            workingDirectory: repositoryPath,
            timeoutSeconds: _options.ProcessTimeoutSeconds,
            cancellationToken: cancellationToken);

    public Task<CliResult> FilesAsync(string repositoryPath, CancellationToken cancellationToken = default) =>
        _runner.RunAsync(
            _options.CodegraphExecutable,
            ["files", "--path", repositoryPath],
            workingDirectory: repositoryPath,
            timeoutSeconds: _options.ProcessTimeoutSeconds,
            cancellationToken: cancellationToken);
}
