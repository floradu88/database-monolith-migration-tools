namespace DbIntelligence.Infrastructure;

public sealed class DbIntelligenceOptions
{
    public const string SectionName = "DbIntelligence";

    public string TargetRepositoryPath { get; set; } = string.Empty;
    public string ArtifactsDirectory { get; set; } = "artifacts/db-intelligence";

    /// <summary>
    /// Command name or full path for Codegraph. Default assumes <c>codegraph</c> is on PATH.
    /// </summary>
    public string CodegraphExecutable { get; set; } = "codegraph";

    /// <summary>
    /// Command name or full path for Graphify. Default assumes <c>graphify</c> is on PATH.
    /// </summary>
    public string GraphifyExecutable { get; set; } = "graphify";

    public string? SqlConnectionString { get; set; }
    public int ProcessTimeoutSeconds { get; set; } = 1800;
}
