using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using CodegraphChat.Contracts;
using CodegraphChat.Infrastructure.Codegraph;
using Microsoft.Extensions.Options;

namespace CodegraphChat.Infrastructure;

public interface IChatSessionStore
{
    string? RepositoryPath { get; set; }
}

public sealed class ChatSessionStore : IChatSessionStore
{
    public string? RepositoryPath { get; set; }
}

public interface ITopicChatService
{
    Task<HealthDto> GetHealthAsync(CancellationToken cancellationToken = default);
    Task<SessionConfigDto> GetSessionAsync(CancellationToken cancellationToken = default);
    Task<SessionConfigDto> SetSessionAsync(SessionConfigRequest request, CancellationToken cancellationToken = default);
    Task<SessionConfigDto> EnsureIndexAsync(CancellationToken cancellationToken = default);
    Task<ChatResponse> AskAsync(ChatRequest request, CancellationToken cancellationToken = default);
}

public sealed class TopicChatService : ITopicChatService
{
    private readonly ICodegraphClient _codegraph;
    private readonly CodegraphChatOptions _options;
    private readonly IChatSessionStore _session;
    private readonly ConcurrentDictionary<string, byte> _conversations = new(StringComparer.Ordinal);

    public TopicChatService(
        ICodegraphClient codegraph,
        IOptions<CodegraphChatOptions> options,
        IChatSessionStore session)
    {
        _codegraph = codegraph;
        _options = options.Value;
        _session = session;
        if (!string.IsNullOrWhiteSpace(_options.TargetRepositoryPath))
            _session.RepositoryPath = Path.GetFullPath(_options.TargetRepositoryPath);
    }

    public async Task<HealthDto> GetHealthAsync(CancellationToken cancellationToken = default)
    {
        var (available, detail) = await _codegraph.CheckAsync(cancellationToken);
        var missing = new List<string>();
        if (!available)
            missing.Add("codegraph");

        return new HealthDto
        {
            Status = available ? "healthy" : "unhealthy",
            Healthy = available,
            Message = available
                ? "Codegraph is available on PATH."
                : "Codegraph is missing. Use DbIntelligence Node/fnm scripts to install.",
            Missing = missing,
            InstallHint =
                ".\\..\\DbIntelligence\\scripts\\Initialize-DbIntelligenceNode.ps1 -InstallCodegraph -Yes",
            Codegraph = new ToolCheckDto
            {
                Available = available,
                VersionOrDetail = detail
            }
        };
    }

    public async Task<SessionConfigDto> GetSessionAsync(CancellationToken cancellationToken = default)
    {
        var (available, version) = await _codegraph.CheckAsync(cancellationToken);
        var repo = _session.RepositoryPath;
        var indexReady = false;
        string? detail = null;

        if (!string.IsNullOrWhiteSpace(repo) && Directory.Exists(repo))
        {
            var codegraphDir = Path.Combine(repo, ".codegraph");
            var folderReady = Directory.Exists(codegraphDir);
            var status = await _codegraph.StatusAsync(repo, cancellationToken);
            var statusReady = status.Succeeded &&
                              (status.StandardOutput.Contains(".codegraph", StringComparison.OrdinalIgnoreCase)
                               || status.StandardOutput.Contains("indexed", StringComparison.OrdinalIgnoreCase)
                               || LooksLikeJson(status.StandardOutput));
            // Folder presence wins when CLI status JSON shape drifts across Codegraph versions.
            indexReady = folderReady || statusReady;
            detail = Truncate(
                folderReady
                    ? $".codegraph present. {status.StandardOutput}{status.StandardError}"
                    : status.StandardOutput + status.StandardError,
                1500);
        }

        return new SessionConfigDto
        {
            RepositoryPath = repo,
            IndexReady = indexReady,
            IndexDetail = detail,
            CodegraphAvailable = available,
            CodegraphVersion = version
        };
    }

    public async Task<SessionConfigDto> SetSessionAsync(SessionConfigRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.RepositoryPath))
            throw new ArgumentException("RepositoryPath is required.");

        var full = Path.GetFullPath(request.RepositoryPath.Trim());
        if (!Directory.Exists(full))
            throw new DirectoryNotFoundException($"Repository path not found: {full}");

        _session.RepositoryPath = full;
        return await GetSessionAsync(cancellationToken);
    }

    public async Task<SessionConfigDto> EnsureIndexAsync(CancellationToken cancellationToken = default)
    {
        var repo = _session.RepositoryPath ?? _options.TargetRepositoryPath;
        if (string.IsNullOrWhiteSpace(repo))
            throw new InvalidOperationException("Bind a repository path before ensuring the Codegraph index.");

        var full = Path.GetFullPath(repo);
        if (!Directory.Exists(full))
            throw new DirectoryNotFoundException($"Repository path not found: {full}");

        _session.RepositoryPath = full;
        var result = await _codegraph.EnsureIndexAsync(full, cancellationToken);
        var session = await GetSessionAsync(cancellationToken);
        session.EnsureSucceeded = result.Succeeded;
        session.EnsureDetail = Truncate(
            string.IsNullOrWhiteSpace(result.StandardOutput) ? result.StandardError : result.StandardOutput,
            2000);
        return session;
    }

    public async Task<ChatResponse> AskAsync(ChatRequest request, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request.Message))
            throw new ArgumentException("Message is required.");

        var conversationId = string.IsNullOrWhiteSpace(request.ConversationId)
            ? Guid.NewGuid().ToString("n")
            : request.ConversationId!.Trim();
        _conversations.TryAdd(conversationId, 0);

        var repo = ResolveRepository(request.RepositoryPath);
        var intent = ChatIntentRouter.Route(request.Message, request.Mode);
        var evidence = new List<ChatEvidenceDto>();
        var body = new StringBuilder();

        body.AppendLine($"**Topic:** {intent.Topic}");
        body.AppendLine($"**Mode:** `{intent.Intent}`");
        if (!string.IsNullOrWhiteSpace(intent.Symbol))
            body.AppendLine($"**Focus symbol:** `{intent.Symbol}`");
        body.AppendLine($"**Repository:** `{repo}`");
        body.AppendLine();

        switch (intent.Intent)
        {
            case ChatIntent.Status:
            {
                var result = await _codegraph.StatusAsync(repo, cancellationToken);
                evidence.Add(ToEvidence("status", null, result));
                body.AppendLine("### Index status");
                body.AppendLine(FormatCliOutput(result));
                break;
            }
            case ChatIntent.Files:
            {
                var result = await _codegraph.FilesAsync(repo, cancellationToken);
                evidence.Add(ToEvidence("files", null, result));
                body.AppendLine("### Indexed file structure");
                body.AppendLine(FormatCliOutput(result));
                break;
            }
            case ChatIntent.Callers:
            {
                var symbol = RequireSymbol(intent, "callers");
                var result = await _codegraph.CallersAsync(repo, symbol, _options.DefaultQueryLimit, cancellationToken);
                evidence.Add(ToEvidence("callers", symbol, result));
                body.AppendLine($"### Callers of `{symbol}`");
                body.AppendLine(FormatSymbolList(result, "No callers found."));
                break;
            }
            case ChatIntent.Callees:
            {
                var symbol = RequireSymbol(intent, "callees");
                var result = await _codegraph.CalleesAsync(repo, symbol, _options.DefaultQueryLimit, cancellationToken);
                evidence.Add(ToEvidence("callees", symbol, result));
                body.AppendLine($"### Callees of `{symbol}`");
                body.AppendLine(FormatSymbolList(result, "No callees found."));
                break;
            }
            case ChatIntent.Impact:
            {
                var symbol = RequireSymbol(intent, "impact");
                var result = await _codegraph.ImpactAsync(repo, symbol, _options.DefaultImpactDepth, cancellationToken);
                evidence.Add(ToEvidence("impact", symbol, result));
                body.AppendLine($"### Impact of changing `{symbol}`");
                body.AppendLine(FormatCliOutput(result));
                break;
            }
            default:
            {
                var search = intent.Symbol ?? intent.Topic;
                var query = await _codegraph.QueryAsync(repo, search, _options.DefaultQueryLimit, cancellationToken);
                evidence.Add(ToEvidence("query", search, query));
                body.AppendLine($"### Symbols matching `{search}`");
                body.AppendLine(FormatSymbolList(query, "No symbols matched that topic."));

                var top = TryFirstSymbolName(query.StandardOutput);
                if (!string.IsNullOrWhiteSpace(top) &&
                    !string.Equals(top, search, StringComparison.OrdinalIgnoreCase))
                {
                    var callers = await _codegraph.CallersAsync(repo, top, 8, cancellationToken);
                    evidence.Add(ToEvidence("callers", top, callers));
                    body.AppendLine();
                    body.AppendLine($"### Related callers of top hit `{top}`");
                    body.AppendLine(FormatSymbolList(callers, "No callers found."));
                }

                break;
            }
        }

        body.AppendLine();
        body.AppendLine("---");
        body.AppendLine("Ask follow-ups such as: `who calls X`, `impact of X`, `callees of X`, or `index status`.");

        return new ChatResponse
        {
            ConversationId = conversationId,
            DetectedMode = intent.Intent.ToString().ToLowerInvariant(),
            DetectedSymbol = intent.Symbol,
            Evidence = evidence,
            Reply = new ChatMessageDto
            {
                Role = "assistant",
                Content = body.ToString().Trim(),
                At = DateTimeOffset.UtcNow
            }
        };
    }

    private string ResolveRepository(string? overridePath)
    {
        var repo = !string.IsNullOrWhiteSpace(overridePath)
            ? overridePath
            : _session.RepositoryPath ?? _options.TargetRepositoryPath;

        if (string.IsNullOrWhiteSpace(repo))
            throw new InvalidOperationException(
                "Set a repository path first (POST /api/session or CodegraphChat:TargetRepositoryPath).");

        var full = Path.GetFullPath(repo);
        if (!Directory.Exists(full))
            throw new DirectoryNotFoundException($"Repository path not found: {full}");

        return full;
    }

    private static string RequireSymbol(IntentResult intent, string mode)
    {
        if (string.IsNullOrWhiteSpace(intent.Symbol))
            throw new ArgumentException($"Could not detect a symbol for {mode}. Quote it, e.g. who calls \"IndexingService\".");
        return intent.Symbol;
    }

    private static ChatEvidenceDto ToEvidence(string command, string? symbol, CliResult result) =>
        new()
        {
            Command = command,
            Symbol = symbol,
            Succeeded = result.Succeeded,
            ExitCode = result.ExitCode,
            Output = Truncate(result.StandardOutput, 8000),
            Error = Truncate(result.StandardError, 2000)
        };

    private static string FormatCliOutput(CliResult result)
    {
        if (!result.Succeeded && string.IsNullOrWhiteSpace(result.StandardOutput))
            return $"_Command failed (exit {result.ExitCode})._  \n```\n{Truncate(result.StandardError, 2000)}\n```";

        var text = result.StandardOutput.Trim();
        if (LooksLikeJson(text))
            return "```json\n" + PrettyJson(text) + "\n```";

        return "```\n" + Truncate(text, 6000) + "\n```";
    }

    private static string FormatSymbolList(CliResult result, string emptyMessage)
    {
        if (!result.Succeeded && string.IsNullOrWhiteSpace(result.StandardOutput))
            return FormatCliOutput(result);

        var text = result.StandardOutput.Trim();
        if (!LooksLikeJson(text))
            return FormatCliOutput(result);

        try
        {
            using var doc = JsonDocument.Parse(text);
            var lines = new List<string>();
            EnumerateSymbolLines(doc.RootElement, lines, max: 40);
            if (lines.Count == 0)
                return $"_{emptyMessage}_";

            var sb = new StringBuilder();
            foreach (var line in lines)
                sb.AppendLine("- " + line);
            return sb.ToString().TrimEnd();
        }
        catch (JsonException)
        {
            return FormatCliOutput(result);
        }
    }

    private static void EnumerateSymbolLines(JsonElement el, List<string> lines, int max)
    {
        if (lines.Count >= max)
            return;

        switch (el.ValueKind)
        {
            case JsonValueKind.Array:
                foreach (var item in el.EnumerateArray())
                    EnumerateSymbolLines(item, lines, max);
                break;
            case JsonValueKind.Object:
                if (TryFormatSymbolObject(el, out var line))
                {
                    lines.Add(line);
                    return;
                }

                foreach (var prop in el.EnumerateObject())
                {
                    if (prop.Value.ValueKind is JsonValueKind.Array or JsonValueKind.Object)
                        EnumerateSymbolLines(prop.Value, lines, max);
                }

                break;
        }
    }

    private static bool TryFormatSymbolObject(JsonElement el, out string line)
    {
        line = string.Empty;
        var name = GetString(el, "name") ?? GetString(el, "label") ?? GetString(el, "id") ?? GetString(el, "symbol");
        if (string.IsNullOrWhiteSpace(name))
            return false;

        var kind = GetString(el, "kind") ?? GetString(el, "type");
        var file = GetString(el, "file") ?? GetString(el, "path") ?? GetString(el, "source_file");
        var sb = new StringBuilder("`" + name + "`");
        if (!string.IsNullOrWhiteSpace(kind))
            sb.Append(" (").Append(kind).Append(')');
        if (!string.IsNullOrWhiteSpace(file))
            sb.Append(" — ").Append(file);
        line = sb.ToString();
        return true;
    }

    private static string? TryFirstSymbolName(string json)
    {
        if (!LooksLikeJson(json))
            return null;
        try
        {
            using var doc = JsonDocument.Parse(json);
            var lines = new List<string>();
            EnumerateSymbolLines(doc.RootElement, lines, max: 1);
            if (lines.Count == 0)
                return null;
            // lines look like `Name` (kind) — file; extract between backticks
            var m = System.Text.RegularExpressions.Regex.Match(lines[0], "`([^`]+)`");
            return m.Success ? m.Groups[1].Value : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? GetString(JsonElement el, string name) =>
        el.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String ? p.GetString() : null;

    private static bool LooksLikeJson(string text)
    {
        var trimmed = text.TrimStart();
        return trimmed.StartsWith('{') || trimmed.StartsWith('[');
    }

    private static string PrettyJson(string text)
    {
        try
        {
            using var doc = JsonDocument.Parse(text);
            return Truncate(JsonSerializer.Serialize(doc.RootElement, new JsonSerializerOptions { WriteIndented = true }), 6000);
        }
        catch (JsonException)
        {
            return Truncate(text, 6000);
        }
    }

    private static string Truncate(string value, int max) =>
        string.IsNullOrEmpty(value) ? value : value.Length <= max ? value : value[..max] + "\n... (truncated)";
}
