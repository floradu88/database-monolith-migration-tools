namespace DbIntelligence.Infrastructure;

public sealed class DbIntelligenceOptions
{
    public const string SectionName = "DbIntelligence";

    /// <summary>Default relative export folder under each indexed project.</summary>
    public const string DefaultArtifactsDirectory = ".db-index";

    /// <summary>Default combined-export folder name under a batch parent.</summary>
    public const string DefaultCombinedDirectoryName = ".db-index-combined";

    public string TargetRepositoryPath { get; set; } = string.Empty;
    public string ArtifactsDirectory { get; set; } = DefaultArtifactsDirectory;

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
