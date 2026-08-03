using System.Text;

namespace BuildingBlocks.Security;

/// <summary>
/// Composes and validates SQL Server connection strings for OnPrem, Azure, and AWS targets.
/// Does not invent credentials — password / MI must be supplied by the operator via secrets.
/// </summary>
public static class SqlConnectionStringComposer
{
    public static string Resolve(SqlEndpointOptions endpoint, string? passwordOverride = null)
    {
        ArgumentNullException.ThrowIfNull(endpoint);

        if (!string.IsNullOrWhiteSpace(endpoint.ConnectionString))
        {
            var cs = EnsureApplicationName(endpoint.ConnectionString, endpoint.ApplicationName);
            SqlConnectionGuard.EnsureProviderCompatible(endpoint.ParsedProvider, cs);
            return cs;
        }

        if (string.IsNullOrWhiteSpace(endpoint.Server) || string.IsNullOrWhiteSpace(endpoint.DatabaseName))
            return string.Empty;

        var built = BuildFromParts(endpoint, passwordOverride ?? endpoint.Password);
        SqlConnectionGuard.EnsureProviderCompatible(endpoint.ParsedProvider, built);
        return built;
    }

    public static string BuildFromParts(SqlEndpointOptions endpoint, string? password)
    {
        var provider = endpoint.ParsedProvider;
        var auth = endpoint.ParsedAuthMode;
        var server = FormatServer(endpoint.Server!, endpoint.Port);
        var encrypt = endpoint.Encrypt ?? DefaultEncrypt(provider);
        var trust = endpoint.TrustServerCertificate ?? DefaultTrustServerCertificate(provider);

        var sb = new StringBuilder();
        Append(sb, "Server", server);
        Append(sb, "Database", endpoint.DatabaseName!);
        Append(sb, "Encrypt", encrypt ? "True" : "False");
        Append(sb, "TrustServerCertificate", trust ? "True" : "False");

        if (!string.IsNullOrWhiteSpace(endpoint.ApplicationName))
            Append(sb, "Application Name", endpoint.ApplicationName);

        switch (auth)
        {
            case SqlAuthMode.Integrated:
                if (provider is SqlHostProvider.Azure or SqlHostProvider.Aws)
                    throw new InvalidOperationException(
                        $"{auth} is not supported for {provider}. Use SqlPassword, Azure AD, or a full ConnectionString.");
                Append(sb, "Trusted_Connection", "True");
                break;

            case SqlAuthMode.SqlPassword:
                if (string.IsNullOrWhiteSpace(endpoint.UserId))
                    throw new InvalidOperationException("AuthMode=SqlPassword requires UserId; set Password via secrets/env.");
                Append(sb, "User Id", endpoint.UserId);
                if (!string.IsNullOrWhiteSpace(password))
                    Append(sb, "Password", password);
                break;

            case SqlAuthMode.AzureActiveDirectoryDefault:
                EnsureAzureFamily(provider, auth);
                Append(sb, "Authentication", "Active Directory Default");
                break;

            case SqlAuthMode.AzureManagedIdentity:
                EnsureAzureFamily(provider, auth);
                Append(sb, "Authentication", "Active Directory Managed Identity");
                if (!string.IsNullOrWhiteSpace(endpoint.ManagedIdentityClientId))
                    Append(sb, "User Id", endpoint.ManagedIdentityClientId);
                break;

            case SqlAuthMode.ConnectionString:
                throw new InvalidOperationException(
                    "AuthMode=ConnectionString requires Endpoint.ConnectionString, or switch AuthMode to Integrated/SqlPassword/Azure*.");

            default:
                throw new InvalidOperationException($"Unsupported AuthMode '{endpoint.AuthMode}'.");
        }

        return sb.ToString();
    }

    public static bool LooksLikeAzureHost(string serverOrCs) =>
        serverOrCs.Contains("database.windows.net", StringComparison.OrdinalIgnoreCase) ||
        serverOrCs.Contains("database.secure.windows.net", StringComparison.OrdinalIgnoreCase) ||
        serverOrCs.Contains(".database.azure.com", StringComparison.OrdinalIgnoreCase);

    public static bool LooksLikeAwsRdsHost(string serverOrCs) =>
        serverOrCs.Contains(".rds.amazonaws.com", StringComparison.OrdinalIgnoreCase) ||
        serverOrCs.Contains(".rds.", StringComparison.OrdinalIgnoreCase);

    public static SqlHostProvider InferProvider(string serverOrCs)
    {
        if (LooksLikeAzureHost(serverOrCs)) return SqlHostProvider.Azure;
        if (LooksLikeAwsRdsHost(serverOrCs)) return SqlHostProvider.Aws;
        return SqlHostProvider.OnPrem;
    }

    private static void EnsureAzureFamily(SqlHostProvider provider, SqlAuthMode auth)
    {
        if (provider != SqlHostProvider.Azure)
            throw new InvalidOperationException($"{auth} requires Provider=Azure (current: {provider}).");
    }

    private static bool DefaultEncrypt(SqlHostProvider provider) =>
        provider is SqlHostProvider.Azure or SqlHostProvider.Aws;

    private static bool DefaultTrustServerCertificate(SqlHostProvider provider) =>
        provider == SqlHostProvider.OnPrem;

    private static string FormatServer(string server, int? port)
    {
        if (port is null or <= 0) return server.Trim();
        if (server.Contains(',', StringComparison.Ordinal) || server.Contains(':', StringComparison.Ordinal))
            return server.Trim();
        return $"{server.Trim()},{port.Value}";
    }

    private static void Append(StringBuilder sb, string key, string value)
    {
        if (sb.Length > 0) sb.Append(';');
        sb.Append(key).Append('=').Append(value);
    }

    private static string EnsureApplicationName(string connectionString, string? applicationName)
    {
        if (string.IsNullOrWhiteSpace(applicationName))
            return connectionString;
        if (connectionString.Contains("Application Name", StringComparison.OrdinalIgnoreCase) ||
            connectionString.Contains("App=", StringComparison.OrdinalIgnoreCase))
            return connectionString;
        return connectionString.TrimEnd(';') + $";Application Name={applicationName}";
    }
}
