namespace DbIntelligence.Contracts;

public sealed class PrerequisiteHealthDto
{
    public bool Healthy { get; set; }
    public string Status { get; set; } = "unhealthy";
    public string? Message { get; set; }
    public PrerequisiteCheckDto Python { get; set; } = new() { Name = "python" };
    public PrerequisiteCheckDto Pip { get; set; } = new() { Name = "pip" };
    public PrerequisiteCheckDto Graphify { get; set; } = new() { Name = "graphify" };
    public PrerequisiteCheckDto Codegraph { get; set; } = new() { Name = "codegraph" };
    public List<string> Missing { get; set; } = [];
    public string InstallHint { get; set; } =
        "Run: dotnet run --project src-templates/DbIntelligence/DbIntelligence.Cli -- --install-preqs";
}

public sealed class PrerequisiteCheckDto
{
    public string Name { get; set; } = string.Empty;
    public bool Available { get; set; }
    public string? VersionOrDetail { get; set; }
    public string? Executable { get; set; }
    public string? Remediation { get; set; }
}

public sealed class ToolAvailabilityDto
{
    public bool CodegraphAvailable { get; set; }
    public bool GraphifyAvailable { get; set; }
    public bool PythonAvailable { get; set; }
    public bool PipAvailable { get; set; }
    public bool Healthy { get; set; }
    public string CodegraphExecutable { get; set; } = "codegraph";
    public string GraphifyExecutable { get; set; } = "graphify";
    public string? Message { get; set; }
    public PrerequisiteHealthDto? Prerequisites { get; set; }
}
