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
}
