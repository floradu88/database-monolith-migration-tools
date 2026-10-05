using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using Microsoft.Extensions.Logging;
using MigrationTool.Application;
using MigrationTool.Domain;

namespace MigrationTool.Infrastructure;

public sealed class NetworkProbe : INetworkProbe
{
    private readonly ILogger<NetworkProbe> _logger;

    public NetworkProbe(ILogger<NetworkProbe> logger)
    {
        _logger = logger;
    }

    public async Task<NetworkReachabilityReport> ProbeAsync(
        DatabaseEndpoint endpoint,
        int timeoutSeconds,
        CancellationToken cancellationToken)
    {
        var timeoutMs = Math.Clamp(timeoutSeconds, 2, 60) * 1000;
        var addresses = new List<string>();
        var steps = new List<NetworkCheckStepResult>();

        try
        {
            var entries = await Dns.GetHostAddressesAsync(endpoint.Host, cancellationToken);
            addresses.AddRange(entries.Select(a => a.ToString()));
            steps.Add(new NetworkCheckStepResult(
                "dns",
                addresses.Count > 0,
                addresses.Count > 0
                    ? $"Resolved {endpoint.Host} → {string.Join(", ", addresses)}"
                    : $"No addresses for {endpoint.Host}"));
            _logger.LogInformation("DNS {Host} → {Addresses}", endpoint.Host, string.Join(", ", addresses));
        }
        catch (Exception ex)
        {
            steps.Add(new NetworkCheckStepResult("dns", false, $"DNS lookup failed for {endpoint.Host}: {ex.Message}"));
            _logger.LogError(ex, "DNS lookup failed for {Host}", endpoint.Host);
            return new NetworkReachabilityReport(endpoint, addresses, steps);
        }

        var ping = await TryPingAsync(endpoint.Host, timeoutMs, cancellationToken);
        steps.Add(ping);
        if (ping.Succeeded)
        {
            _logger.LogInformation("ICMP ping OK for {Host}: {Detail}", endpoint.Host, ping.Detail);
        }
        else
        {
            _logger.LogWarning(
                "ICMP ping did not succeed for {Host} ({Detail}). TCP check still runs; many cloud hosts block ICMP.",
                endpoint.Host,
                ping.Detail);
        }

        var tcp = await TryTcpAsync(endpoint.Host, endpoint.Port, timeoutMs, cancellationToken);
        steps.Add(tcp);
        if (tcp.Succeeded)
        {
            _logger.LogInformation("TCP {Host}:{Port} OK: {Detail}", endpoint.Host, endpoint.Port, tcp.Detail);
        }
        else
        {
            _logger.LogError("TCP {Host}:{Port} failed: {Detail}", endpoint.Host, endpoint.Port, tcp.Detail);
        }

        return new NetworkReachabilityReport(endpoint, addresses, steps);
    }

    private static async Task<NetworkCheckStepResult> TryPingAsync(
        string host,
        int timeoutMs,
        CancellationToken cancellationToken)
    {
        try
        {
            using var ping = new Ping();
            // ICMP is best-effort; cancellation is honored via timeout only (Ping API varies by TFM).
            _ = cancellationToken;
            var reply = await ping.SendPingAsync(host, timeoutMs);
            if (reply.Status == IPStatus.Success)
            {
                return new NetworkCheckStepResult(
                    "ping",
                    true,
                    $"ICMP reply in {reply.RoundtripTime} ms",
                    Optional: true);
            }

            return new NetworkCheckStepResult(
                "ping",
                false,
                $"ICMP status {reply.Status} (often blocked by AWS/security groups; TCP matters more)",
                Optional: true);
        }
        catch (Exception ex)
        {
            return new NetworkCheckStepResult(
                "ping",
                false,
                $"ICMP unavailable: {ex.Message} (optional; TCP is the authoritative DB port check)",
                Optional: true);
        }
    }

    private static async Task<NetworkCheckStepResult> TryTcpAsync(
        string host,
        int port,
        int timeoutMs,
        CancellationToken cancellationToken)
    {
        try
        {
            using var client = new TcpClient();
            using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            linked.CancelAfter(timeoutMs);
            await client.ConnectAsync(host, port, linked.Token);
            return new NetworkCheckStepResult(
                "tcp",
                true,
                $"Connected to {host}:{port}");
        }
        catch (OperationCanceledException)
        {
            return new NetworkCheckStepResult(
                "tcp",
                false,
                $"Timed out connecting to {host}:{port}. Check security group / firewall for TCP {port}.");
        }
        catch (Exception ex)
        {
            return new NetworkCheckStepResult(
                "tcp",
                false,
                $"Cannot connect to {host}:{port}: {ex.Message}. For AWS RDS allow inbound TCP {port} from this client.");
        }
    }
}
