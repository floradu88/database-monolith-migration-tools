namespace MigrationTool.Domain;

public sealed record DacpacToolPrerequisite(
    string Name,
    bool Available,
    string? Path,
    string Notes);

public sealed record DacpacPrerequisitesReport(
    IReadOnlyList<DacpacToolPrerequisite> Tools,
    bool CanBuildSdkStyle,
    bool CanBuildClassicSsdt,
    bool CanExtract)
{
    public string Format()
    {
        var lines = new List<string>
        {
            "DACPAC prerequisites:",
            $"  SDK-style build (Microsoft.Build.Sql): {(CanBuildSdkStyle ? "ready" : "missing")}",
            $"  Classic SSDT build: {(CanBuildClassicSsdt ? "ready" : "missing")}",
            $"  SqlPackage extract: {(CanExtract ? "ready" : "missing")}",
            ""
        };

        foreach (var tool in Tools)
        {
            var status = tool.Available ? "OK" : "MISSING";
            var path = string.IsNullOrWhiteSpace(tool.Path) ? "-" : tool.Path;
            lines.Add($"  [{status}] {tool.Name}: {path}");
            if (!string.IsNullOrWhiteSpace(tool.Notes))
            {
                lines.Add($"         {tool.Notes}");
            }
        }

        return string.Join(Environment.NewLine, lines);
    }
}
