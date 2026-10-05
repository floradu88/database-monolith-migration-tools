namespace MigrationTool.Domain;

public enum DatabaseConnectionErrorKind
{
    Unknown = 0,
    EmptyConnectionString,
    InvalidConnectionString,
    LoginFailed,
    DatabaseNotFound,
    NetworkUnreachable,
    Timeout,
    FirewallOrSecurity,
    TlsOrEncrypt,
    HostNotFound,
    ConnectionRefused
}

public sealed record DatabaseConnectionError(
    DatabaseConnectionErrorKind Kind,
    string Summary,
    string Guidance,
    int? SqlErrorNumber = null,
    string? SqlState = null,
    string? ProviderMessage = null)
{
    public string Format()
    {
        var parts = new List<string> { $"[{Kind}] {Summary}" };
        if (SqlErrorNumber is not null)
        {
            parts.Add($"SQL error {SqlErrorNumber}");
        }

        if (!string.IsNullOrWhiteSpace(SqlState))
        {
            parts.Add($"SQLSTATE {SqlState}");
        }

        if (!string.IsNullOrWhiteSpace(ProviderMessage))
        {
            parts.Add(ProviderMessage!);
        }

        parts.Add(Guidance);
        return string.Join(" — ", parts);
    }
}

public static class DatabaseConnectionErrorClassifier
{
    public static DatabaseConnectionError FromSqlServer(int? number, string? message)
    {
        var text = message ?? "";
        return number switch
        {
            18456 => Error(
                DatabaseConnectionErrorKind.LoginFailed,
                number,
                "Login failed for the supplied SQL Server credentials.",
                "Check User ID/Password (or Trusted_Connection). For AWS RDS confirm the master user and that the account is not locked."),
            4060 => Error(
                DatabaseConnectionErrorKind.DatabaseNotFound,
                number,
                "Cannot open the requested database (Initial Catalog / Database).",
                "Confirm the database name exists on that instance and the login has access."),
            53 or 11001 or 10053 or 10054 => Error(
                DatabaseConnectionErrorKind.NetworkUnreachable,
                number,
                "Network path to SQL Server failed.",
                "Check host name, VPN/security group, and that the instance is publicly reachable or reachable from this network."),
            -2 or 258 or -1 => Error(
                DatabaseConnectionErrorKind.Timeout,
                number,
                "Timed out connecting to SQL Server or waiting for a response.",
                "Verify network latency/firewall, increase CommandTimeoutSeconds if needed, and confirm the host accepts TCP 1433."),
            10060 => Error(
                DatabaseConnectionErrorKind.Timeout,
                number,
                "Connection timed out reaching SQL Server.",
                "For AWS RDS: security group inbound 1433 from your IP, public accessibility if required, and Data Source=host,1433."),
            10061 => Error(
                DatabaseConnectionErrorKind.ConnectionRefused,
                number,
                "Connection refused by the SQL Server host.",
                "Confirm SQL Server is listening on 1433 and nothing is blocking the port."),
            40615 or 49918 or 49919 or 49920 => Error(
                DatabaseConnectionErrorKind.FirewallOrSecurity,
                number,
                "Cloud firewall or capacity rule blocked the connection.",
                "Allow your client IP in the RDS security group / Azure firewall, then retry."),
            _ when ContainsAny(text, "certificate", "ssl", "tls", "encrypt") => Error(
                DatabaseConnectionErrorKind.TlsOrEncrypt,
                number,
                "TLS/encryption handshake with SQL Server failed.",
                "For AWS RDS prefer Encrypt=True. Use TrustServerCertificate=True only if you intentionally trust the server cert."),
            _ when ContainsAny(text, "could not open a connection", "network-related", "server was not found", "was not accessible") => Error(
                DatabaseConnectionErrorKind.NetworkUnreachable,
                number,
                "Could not reach SQL Server.",
                "Verify Data Source/Server (include ,1433 for AWS RDS), DNS, and security groups."),
            _ when ContainsAny(text, "login failed") => Error(
                DatabaseConnectionErrorKind.LoginFailed,
                number,
                "Login failed for the supplied SQL Server credentials.",
                "Check User ID/Password and that the login can access the database."),
            _ => Error(
                DatabaseConnectionErrorKind.Unknown,
                number,
                "SQL Server connection failed.",
                "Inspect the provider message, confirm host/port/database/credentials, then run --validate-connection.",
                text)
        };
    }

    public static DatabaseConnectionError FromPostgreSql(string? sqlState, string? message)
    {
        var text = message ?? "";
        return sqlState switch
        {
            "28P01" => new DatabaseConnectionError(
                DatabaseConnectionErrorKind.LoginFailed,
                "PostgreSQL authentication failed.",
                "Check Username/Password and auth method allowed by the server.",
                SqlState: sqlState,
                ProviderMessage: NullIfEmpty(text)),
            "3D000" => new DatabaseConnectionError(
                DatabaseConnectionErrorKind.DatabaseNotFound,
                "PostgreSQL database does not exist.",
                "Confirm the Database name in the connection string.",
                SqlState: sqlState,
                ProviderMessage: NullIfEmpty(text)),
            "08001" or "08006" => new DatabaseConnectionError(
                DatabaseConnectionErrorKind.NetworkUnreachable,
                "Could not establish a PostgreSQL connection.",
                "Check Host/Port, SSL Mode, and network/security group rules.",
                SqlState: sqlState,
                ProviderMessage: NullIfEmpty(text)),
            _ when ContainsAny(text, "ssl", "certificate", "tls") => new DatabaseConnectionError(
                DatabaseConnectionErrorKind.TlsOrEncrypt,
                "PostgreSQL TLS/SSL handshake failed.",
                "Set SSL Mode=Require (or VerifyFull) as appropriate for your host.",
                SqlState: sqlState,
                ProviderMessage: NullIfEmpty(text)),
            _ => new DatabaseConnectionError(
                DatabaseConnectionErrorKind.Unknown,
                "PostgreSQL connection failed.",
                "Inspect the provider message, then run --validate-connection.",
                SqlState: sqlState,
                ProviderMessage: NullIfEmpty(text))
        };
    }

    public static DatabaseConnectionError Empty() =>
        new(
            DatabaseConnectionErrorKind.EmptyConnectionString,
            "Connection string is empty.",
            "Set MIGRATION_CONNECTION_STRING or Migration:ConnectionString, then run --validate-connection.");

    public static DatabaseConnectionError InvalidShape(string details) =>
        new(
            DatabaseConnectionErrorKind.InvalidConnectionString,
            "Connection string shape is invalid.",
            details);

    private static DatabaseConnectionError Error(
        DatabaseConnectionErrorKind kind,
        int? number,
        string summary,
        string guidance,
        string? providerMessage = null) =>
        new(kind, summary, guidance, number, ProviderMessage: NullIfEmpty(providerMessage));

    private static bool ContainsAny(string text, params string[] fragments) =>
        fragments.Any(fragment => text.Contains(fragment, StringComparison.OrdinalIgnoreCase));

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value;
}
