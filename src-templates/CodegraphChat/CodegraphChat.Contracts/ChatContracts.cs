namespace CodegraphChat.Contracts;

public sealed class ChatRequest
{
    public string Message { get; set; } = string.Empty;
    public string? RepositoryPath { get; set; }
    public string? ConversationId { get; set; }
    /// <summary>Optional override: query | callers | callees | impact | status | files.</summary>
    public string? Mode { get; set; }
}

public sealed class ChatResponse
{
    public string ConversationId { get; set; } = string.Empty;
    public ChatMessageDto Reply { get; set; } = new();
    public IReadOnlyList<ChatEvidenceDto> Evidence { get; set; } = Array.Empty<ChatEvidenceDto>();
    public string DetectedMode { get; set; } = "query";
    public string? DetectedSymbol { get; set; }
}

public sealed class ChatMessageDto
{
    public string Role { get; set; } = "assistant";
    public string Content { get; set; } = string.Empty;
    public DateTimeOffset At { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class ChatEvidenceDto
{
    public string Command { get; set; } = string.Empty;
    public string? Symbol { get; set; }
    public bool Succeeded { get; set; }
    public int ExitCode { get; set; }
    public string Output { get; set; } = string.Empty;
    public string Error { get; set; } = string.Empty;
}

public sealed class SessionConfigRequest
{
    public string RepositoryPath { get; set; } = string.Empty;
}

public sealed class SessionConfigDto
{
    public string? RepositoryPath { get; set; }
    public bool IndexReady { get; set; }
    public string? IndexDetail { get; set; }
    public bool CodegraphAvailable { get; set; }
    public string? CodegraphVersion { get; set; }
    public bool? EnsureSucceeded { get; set; }
    public string? EnsureDetail { get; set; }
}

public sealed class HealthDto
{
    public string Status { get; set; } = "unhealthy";
    public bool Healthy { get; set; }
    public string Message { get; set; } = string.Empty;
    public IReadOnlyList<string> Missing { get; set; } = Array.Empty<string>();
    public string InstallHint { get; set; } = string.Empty;
    public ToolCheckDto Codegraph { get; set; } = new();
}

public sealed class ToolCheckDto
{
    public bool Available { get; set; }
    public string? VersionOrDetail { get; set; }
    public string Remediation { get; set; } =
        "Install with: ..\\DbIntelligence\\scripts\\Initialize-DbIntelligenceNode.ps1 -InstallCodegraph -Yes";
}
