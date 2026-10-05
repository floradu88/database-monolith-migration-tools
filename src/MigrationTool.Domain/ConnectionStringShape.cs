using System.Data.Common;
using System.Text;

namespace MigrationTool.Domain;

public sealed record ConnectionStringShapeResult(
    bool IsValid,
    IReadOnlyList<string> Errors,
    IReadOnlyList<string> Warnings,
    string RedactedConnectionString,
    string? Server,
    string? Database,
    bool HasCredentials);

public static class ConnectionStringShape
{
    private static readonly HashSet<string> SecretKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "Password", "Pwd", "User Password"
    };

    public static ConnectionStringShapeResult Validate(string? connectionString, DatabaseProviderKind provider)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        if (string.IsNullOrWhiteSpace(connectionString))
        {
            errors.Add("Connection string is empty. Set MIGRATION_CONNECTION_STRING or Migration:ConnectionString.");
            return new ConnectionStringShapeResult(false, errors, warnings, "", null, null, false);
        }

        DbConnectionStringBuilder builder;
        try
        {
            builder = new DbConnectionStringBuilder { ConnectionString = connectionString };
        }
        catch (ArgumentException ex)
        {
            errors.Add($"Connection string could not be parsed: {ex.Message}");
            return new ConnectionStringShapeResult(false, errors, warnings, Redact(connectionString), null, null, false);
        }

        string? server = null;
        string? database = null;
        var hasUser = false;
        var hasPassword = false;
        var encryptSet = false;

        foreach (string key in builder.Keys)
        {
            var value = Convert.ToString(builder[key]) ?? "";
            if (IsAny(key, "Data Source", "Server", "Address", "Addr", "Network Address", "Host"))
            {
                server = value;
            }
            else if (IsAny(key, "Initial Catalog", "Database"))
            {
                database = value;
            }
            else if (IsAny(key, "User ID", "UID", "User", "Username", "User Name"))
            {
                hasUser = !string.IsNullOrWhiteSpace(value);
            }
            else if (IsAny(key, "Password", "Pwd"))
            {
                hasPassword = !string.IsNullOrWhiteSpace(value);
            }
            else if (IsAny(key, "Encrypt", "SSL Mode"))
            {
                encryptSet = true;
            }
        }

        if (provider == DatabaseProviderKind.SqlServer)
        {
            if (string.IsNullOrWhiteSpace(server))
            {
                errors.Add("SQL Server connection string must include Data Source (or Server), e.g. Data Source=host,1433;…");
            }
            else if (!LooksLikeLocal(server) && !server.Contains(',', StringComparison.Ordinal) && !server.Contains(':', StringComparison.Ordinal))
            {
                warnings.Add("No port found on Data Source/Server. For AWS RDS SQL Server use host,1433 (comma before port).");
            }

            if (string.IsNullOrWhiteSpace(database))
            {
                errors.Add("SQL Server connection string must include Initial Catalog (or Database).");
            }

            var integrated = GetBool(builder, "Integrated Security") || GetBool(builder, "Trusted_Connection");
            if (!integrated && !hasUser)
            {
                errors.Add("SQL Server connection string needs User ID (or Trusted_Connection/Integrated Security=True).");
            }

            if (!integrated && hasUser && !hasPassword)
            {
                warnings.Add("User ID is set but Password is empty.");
            }

            if (!LooksLikeLocal(server) && !encryptSet)
            {
                warnings.Add("Encrypt is not set. For AWS RDS / cloud SQL Server prefer Encrypt=True (and TrustServerCertificate only if required).");
            }
        }
        else
        {
            if (string.IsNullOrWhiteSpace(server) && string.IsNullOrWhiteSpace(Get(builder, "Host")))
            {
                errors.Add("PostgreSQL connection string must include Host (or Server).");
            }

            if (string.IsNullOrWhiteSpace(database))
            {
                errors.Add("PostgreSQL connection string must include Database.");
            }
        }

        return new ConnectionStringShapeResult(
            errors.Count == 0,
            errors,
            warnings,
            Redact(connectionString),
            server ?? Get(builder, "Host"),
            database,
            hasUser || hasPassword);
    }

    public static string Redact(string connectionString)
    {
        try
        {
            var builder = new DbConnectionStringBuilder { ConnectionString = connectionString };
            var sb = new StringBuilder();
            foreach (string key in builder.Keys)
            {
                if (sb.Length > 0)
                {
                    sb.Append(';');
                }

                var value = SecretKeys.Contains(key) ? "***" : Convert.ToString(builder[key]);
                sb.Append(key).Append('=').Append(value);
            }

            return sb.ToString();
        }
        catch
        {
            return "***";
        }
    }

    private static bool IsAny(string key, params string[] names) =>
        names.Any(n => string.Equals(key, n, StringComparison.OrdinalIgnoreCase));

    private static string? Get(DbConnectionStringBuilder builder, string key)
    {
        foreach (string existing in builder.Keys)
        {
            if (string.Equals(existing, key, StringComparison.OrdinalIgnoreCase))
            {
                return Convert.ToString(builder[existing]);
            }
        }

        return null;
    }

    private static bool GetBool(DbConnectionStringBuilder builder, string key)
    {
        var value = Get(builder, key);
        return bool.TryParse(value, out var b) && b
               || string.Equals(value, "yes", StringComparison.OrdinalIgnoreCase)
               || string.Equals(value, "sspi", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikeLocal(string? server)
    {
        if (string.IsNullOrWhiteSpace(server))
        {
            return false;
        }

        var host = server.Split(',')[0].Split(':')[0].Trim();
        return host is "." or "(local)" or "localhost" or "127.0.0.1" or "::1"
               || host.StartsWith(".\\", StringComparison.Ordinal)
               || host.StartsWith("(local)\\", StringComparison.OrdinalIgnoreCase);
    }
}
