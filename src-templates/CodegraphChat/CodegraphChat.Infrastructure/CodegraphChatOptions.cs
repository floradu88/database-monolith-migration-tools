namespace CodegraphChat.Infrastructure;

public sealed class CodegraphChatOptions
{
    public const string SectionName = "CodegraphChat";

    public string CodegraphExecutable { get; set; } = "codegraph";
    public string? TargetRepositoryPath { get; set; }
    public int ProcessTimeoutSeconds { get; set; } = 120;
    public int DefaultQueryLimit { get; set; } = 15;
    public int DefaultImpactDepth { get; set; } = 2;
}
