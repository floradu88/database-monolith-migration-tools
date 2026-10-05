namespace MigrationTool.Domain;

public sealed record NetworkCheckStepResult(
    string Name,
    bool Succeeded,
    string Detail,
    bool Optional = false);

public sealed record NetworkReachabilityReport(
    DatabaseEndpoint Endpoint,
    IReadOnlyList<string> ResolvedAddresses,
    IReadOnlyList<NetworkCheckStepResult> Steps)
{
    public bool DnsSucceeded => ResolvedAddresses.Count > 0;

    public bool TcpSucceeded => Steps.Any(s =>
        string.Equals(s.Name, "tcp", StringComparison.OrdinalIgnoreCase) && s.Succeeded);

    public bool PingSucceeded => Steps.Any(s =>
        string.Equals(s.Name, "ping", StringComparison.OrdinalIgnoreCase) && s.Succeeded);

    public bool IsReachableEnough => DnsSucceeded && TcpSucceeded;

    public string Format()
    {
        var lines = new List<string>
        {
            $"Network check: {Endpoint.Host}:{Endpoint.Port}",
            $"  DNS: {(DnsSucceeded ? string.Join(", ", ResolvedAddresses) : "FAILED")}"
        };

        foreach (var step in Steps)
        {
            var status = step.Succeeded ? "OK" : (step.Optional ? "SKIP/FAIL" : "FAILED");
            lines.Add($"  {step.Name}: {status} — {step.Detail}");
        }

        return string.Join(Environment.NewLine, lines);
    }
}
