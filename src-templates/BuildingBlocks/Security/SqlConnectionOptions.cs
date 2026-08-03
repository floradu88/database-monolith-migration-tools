namespace BuildingBlocks.Security;

public sealed class SqlConnectionOptions
{
    public const string SectionName = "SqlConnections";
    public string OwnedConnectionString { get; set; } = string.Empty;
    public string SourceFacadeConnectionString { get; set; } = string.Empty;
    public bool AllowDbOwner { get; set; } = false;
}

public static class SqlConnectionGuard
{
    public static void EnsureLeastPrivilege(SqlConnectionOptions options)
    {
        if (options.AllowDbOwner) return;
        foreach (var cs in new[] { options.OwnedConnectionString, options.SourceFacadeConnectionString })
        {
            if (string.IsNullOrWhiteSpace(cs)) continue;
            if (cs.Contains("db_owner", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("Runtime connection strings must not request db_owner. Use least-privilege identities.");
        }
    }

    /// <summary>
    /// Fail fast when connection string shape conflicts with the declared host provider
    /// (OnPrem / Azure / Aws). Helps catch mis-pointed cutovers during migration.
    /// </summary>
    public static void EnsureProviderCompatible(SqlHostProvider provider, string connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            return;

        var inferred = SqlConnectionStringComposer.InferProvider(connectionString);
        if (provider == SqlHostProvider.OnPrem && inferred is SqlHostProvider.Azure or SqlHostProvider.Aws)
        {
            throw new InvalidOperationException(
                $"Provider=OnPrem but connection looks like {inferred}. Set Database:Provider (or endpoint Provider) to {inferred}.");
        }

        if (provider == SqlHostProvider.Azure && inferred == SqlHostProvider.Aws)
        {
            throw new InvalidOperationException("Provider=Azure but connection looks like AWS RDS. Set Provider=Aws.");
        }

        if (provider == SqlHostProvider.Aws && inferred == SqlHostProvider.Azure)
        {
            throw new InvalidOperationException("Provider=Aws but connection looks like Azure SQL. Set Provider=Azure.");
        }

        switch (provider)
        {
            case SqlHostProvider.Azure:
                if (connectionString.Contains("Trusted_Connection=True", StringComparison.OrdinalIgnoreCase) ||
                    connectionString.Contains("Integrated Security=True", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        "Azure SQL targets must not use Trusted_Connection/Integrated Security. Use Azure AD / Managed Identity or SQL auth via secrets.");
                }

                if (connectionString.Contains("Encrypt=False", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("Azure SQL requires Encrypt=True (or omit Encrypt to use driver defaults that enforce encryption).");
                }
                break;

            case SqlHostProvider.Aws:
                if (connectionString.Contains("(localdb)", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException("Provider=Aws cannot use LocalDB. Point Server at the RDS/EC2 endpoint.");
                }
                break;
        }
    }
}
