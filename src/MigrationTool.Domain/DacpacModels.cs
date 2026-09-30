namespace MigrationTool.Domain;

public sealed record DacpacBuildRequest(
    string SqlProjectPath,
    string Configuration = "Debug",
    string? OutputDirectory = null,
    string? IntermediateDirectory = null);

public sealed record DacpacExtractRequest(
    string ConnectionString,
    string OutputDacpacPath,
    string? DatabaseName = null);

public sealed record DacpacOperationResult(
    bool Succeeded,
    string? DacpacPath,
    string ToolUsed,
    SqlProjectKind ProjectKind,
    string Log,
    string? Error);
