namespace MigrationTool.Domain;

public sealed record DatabaseEndpoint(string Host, int Port, string RawServer);

public static class DatabaseEndpointParser
{
    public static DatabaseEndpoint? TryParse(
        string? serverOrHost,
        DatabaseProviderKind provider,
        int? explicitPort = null)
    {
        if (string.IsNullOrWhiteSpace(serverOrHost))
        {
            return null;
        }

        var raw = serverOrHost.Trim();
        if (raw.StartsWith("tcp:", StringComparison.OrdinalIgnoreCase))
        {
            raw = raw[4..];
        }

        // Named instance "host\instance" — TCP port unknown without SQL Browser; use default.
        var withoutInstance = raw.Split('\\', 2)[0].Trim();
        string host;
        int? embeddedPort = null;

        if (withoutInstance.Contains(',', StringComparison.Ordinal))
        {
            var parts = withoutInstance.Split(',', 2, StringSplitOptions.TrimEntries);
            host = parts[0];
            if (parts.Length > 1 && int.TryParse(parts[1], out var commaPort))
            {
                embeddedPort = commaPort;
            }
        }
        else if (LooksLikeHostColonPort(withoutInstance))
        {
            var idx = withoutInstance.LastIndexOf(':');
            host = withoutInstance[..idx];
            if (int.TryParse(withoutInstance[(idx + 1)..], out var colonPort))
            {
                embeddedPort = colonPort;
            }
        }
        else
        {
            host = withoutInstance;
        }

        if (string.IsNullOrWhiteSpace(host))
        {
            return null;
        }

        var port = explicitPort
                   ?? embeddedPort
                   ?? (provider == DatabaseProviderKind.PostgreSql ? 5432 : 1433);

        return new DatabaseEndpoint(host, port, serverOrHost.Trim());
    }

    private static bool LooksLikeHostColonPort(string value)
    {
        // Avoid treating IPv6 as host:port; require a simple hostname/IPv4 with trailing :port.
        if (value.StartsWith('[') || value.Count(c => c == ':') != 1)
        {
            return false;
        }

        var idx = value.LastIndexOf(':');
        return idx > 0 && idx < value.Length - 1 && int.TryParse(value[(idx + 1)..], out _);
    }
}
