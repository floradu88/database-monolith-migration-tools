namespace BuildingBlocks.Security;

/// <summary>
/// Where the SQL Server-compatible database is hosted for migration / runtime.
/// All three are first-class: on-premises SQL Server, Azure SQL family, AWS RDS/EC2 SQL Server.
/// </summary>
public enum SqlHostProvider
{
    /// <summary>On-premises SQL Server / local lab (including LocalDB).</summary>
    OnPrem = 0,

    /// <summary>Azure SQL Database, Managed Instance, or SQL Server on Azure VM.</summary>
    Azure = 1,

    /// <summary>Amazon RDS for SQL Server or SQL Server on EC2.</summary>
    Aws = 2
}

/// <summary>How the service authenticates to SQL (secrets stay outside source — Key Vault / Secrets Manager / MI).</summary>
public enum SqlAuthMode
{
    /// <summary>Full connection string supplied (still validated for the selected provider).</summary>
    ConnectionString = 0,

    /// <summary>Windows integrated / Trusted_Connection (typical on-prem).</summary>
    Integrated = 1,

    /// <summary>SQL login + password from secret store (common for AWS RDS and some on-prem).</summary>
    SqlPassword = 2,

    /// <summary>Azure AD default credential chain (VS, az login, workload identity).</summary>
    AzureActiveDirectoryDefault = 3,

    /// <summary>Azure Managed Identity (optionally with ManagedIdentityClientId).</summary>
    AzureManagedIdentity = 4
}

/// <summary>One SQL endpoint (owned or source façade) with optional composed connection string.</summary>
public sealed class SqlEndpointOptions
{
    /// <summary>OnPrem | Azure | Aws (case-insensitive). Default OnPrem.</summary>
    public string Provider { get; set; } = nameof(SqlHostProvider.OnPrem);

    /// <summary>ConnectionString | Integrated | SqlPassword | AzureActiveDirectoryDefault | AzureManagedIdentity.</summary>
    public string AuthMode { get; set; } = nameof(SqlAuthMode.ConnectionString);

    /// <summary>When set, used as-is (after provider validation). Otherwise composed from Server/DatabaseName/AuthMode.</summary>
    public string ConnectionString { get; set; } = string.Empty;

    public string? Server { get; set; }
    public int? Port { get; set; }
    public string? DatabaseName { get; set; }

    /// <summary>SQL user id when AuthMode=SqlPassword. Password must come from secrets/env, not committed files.</summary>
    public string? UserId { get; set; }

    /// <summary>Password placeholder resolution: set via env/user-secrets only (never commit).</summary>
    public string? Password { get; set; }

    public string? ManagedIdentityClientId { get; set; }
    public string? ApplicationName { get; set; }

    /// <summary>Null = provider default.</summary>
    public bool? Encrypt { get; set; }

    /// <summary>Null = provider default. Prefer false for Azure/AWS production.</summary>
    public bool? TrustServerCertificate { get; set; }

    public SqlHostProvider ParsedProvider =>
        Enum.TryParse<SqlHostProvider>(Provider, ignoreCase: true, out var p) ? p : SqlHostProvider.OnPrem;

    public SqlAuthMode ParsedAuthMode =>
        Enum.TryParse<SqlAuthMode>(AuthMode, ignoreCase: true, out var a) ? a : SqlAuthMode.ConnectionString;
}
