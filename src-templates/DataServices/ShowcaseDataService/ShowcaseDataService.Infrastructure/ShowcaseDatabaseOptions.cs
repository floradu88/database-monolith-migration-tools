using BuildingBlocks.Security;
using Microsoft.Extensions.Configuration;

namespace ShowcaseDataService.Infrastructure;

/// <summary>
/// Single place to configure owned-database connection, schema, and host provider
/// (OnPrem | Azure | Aws). Change <see cref="Schema"/> and owned endpoint here when
/// moving off monolith <c>dbo</c> / shared DB onto the service's own database.
/// </summary>
public sealed class ShowcaseDatabaseOptions
{
    public const string SectionName = "Database";
    public const string DefaultSchema = "showcase";
    public const string DefaultDeploymentSchema = "deployment";
    public const string ItemsTableName = "Items";
    public const string GetShowcaseSummaryProcedureName = "GetShowcaseSummary";

    /// <summary>SQL schema / namespace for owned objects (e.g. showcase, or dbo during early façade).</summary>
    public string Schema { get; set; } = DefaultSchema;

    /// <summary>Legacy schema for parallel-write quality window (same database).</summary>
    public string LegacySchema { get; set; } = "dbo";

    /// <summary>Owned candidate schema for parallel-write quality window (same database).</summary>
    public string CoreSchema { get; set; } = "core";

    /// <summary>Schema for contract / deployment metadata tables.</summary>
    public string DeploymentSchema { get; set; } = DefaultDeploymentSchema;

    /// <summary>Owned (target) database endpoint — OnPrem, Azure, or Aws.</summary>
    public SqlEndpointOptions Owned { get; set; } = new()
    {
        ApplicationName = "ShowcaseDataService.Owned"
    };

    /// <summary>Source monolith / façade endpoint during FacadeThenMove.</summary>
    public SqlEndpointOptions SourceFacade { get; set; } = new()
    {
        ApplicationName = "ShowcaseDataService.SourceFacade"
    };

    /// <summary>Legacy flat owned connection string (maps into <see cref="Owned"/>).</summary>
    public string OwnedConnectionString
    {
        get => Owned.ConnectionString;
        set => Owned.ConnectionString = value ?? string.Empty;
    }

    /// <summary>Legacy flat source connection string (maps into <see cref="SourceFacade"/>).</summary>
    public string SourceFacadeConnectionString
    {
        get => SourceFacade.ConnectionString;
        set => SourceFacade.ConnectionString = value ?? string.Empty;
    }

    /// <summary>Convenience: owned provider (OnPrem | Azure | Aws).</summary>
    public string Provider
    {
        get => Owned.Provider;
        set => Owned.Provider = value;
    }

    public bool AllowDbOwner { get; set; }

    public string NormalizedSchema =>
        string.IsNullOrWhiteSpace(Schema) ? DefaultSchema : Schema.Trim();

    public string NormalizedDeploymentSchema =>
        string.IsNullOrWhiteSpace(DeploymentSchema) ? DefaultDeploymentSchema : DeploymentSchema.Trim();

    public string Qualify(string objectName) =>
        $"[{NormalizedSchema}].[{objectName}]";

    public string Procedure(string procedureName) =>
        $"{NormalizedSchema}.{procedureName}";

    public string BracketedProcedure(string procedureName) =>
        Qualify(procedureName);

    public string ItemsTable => Qualify(ItemsTableName);

    public string GetShowcaseSummaryProcedure => Procedure(GetShowcaseSummaryProcedureName);

    public string ResolveOwnedConnectionString() =>
        SqlConnectionStringComposer.Resolve(Owned);

    public string ResolveSourceFacadeConnectionString() =>
        SqlConnectionStringComposer.Resolve(SourceFacade);

    public SqlConnectionOptions ToSqlConnectionOptions() => new()
    {
        OwnedConnectionString = ResolveOwnedConnectionString(),
        SourceFacadeConnectionString = ResolveSourceFacadeConnectionString(),
        AllowDbOwner = AllowDbOwner
    };

    /// <summary>
    /// Prefer <c>Database</c> section; fall back to legacy <c>SqlConnections</c> so existing env vars keep working.
    /// </summary>
    public static ShowcaseDatabaseOptions FromConfiguration(IConfiguration configuration)
    {
        var options = configuration.GetSection(SectionName).Get<ShowcaseDatabaseOptions>()
                      ?? new ShowcaseDatabaseOptions();

        // Flat env bindings: Database__OwnedConnectionString, Database__Provider, etc.
        var flatOwned = configuration[$"{SectionName}:OwnedConnectionString"];
        if (!string.IsNullOrWhiteSpace(flatOwned) && string.IsNullOrWhiteSpace(options.Owned.ConnectionString))
            options.Owned.ConnectionString = flatOwned;

        var flatSource = configuration[$"{SectionName}:SourceFacadeConnectionString"];
        if (!string.IsNullOrWhiteSpace(flatSource) && string.IsNullOrWhiteSpace(options.SourceFacade.ConnectionString))
            options.SourceFacade.ConnectionString = flatSource;

        var flatProvider = configuration[$"{SectionName}:Provider"];
        if (!string.IsNullOrWhiteSpace(flatProvider))
            options.Owned.Provider = flatProvider;

        var flatSourceProvider = configuration[$"{SectionName}:SourceProvider"];
        if (!string.IsNullOrWhiteSpace(flatSourceProvider))
            options.SourceFacade.Provider = flatSourceProvider;

        var legacy = configuration.GetSection(SqlConnectionOptions.SectionName).Get<SqlConnectionOptions>();
        if (legacy is not null)
        {
            if (string.IsNullOrWhiteSpace(options.Owned.ConnectionString))
                options.Owned.ConnectionString = legacy.OwnedConnectionString;
            if (string.IsNullOrWhiteSpace(options.SourceFacade.ConnectionString))
                options.SourceFacade.ConnectionString = legacy.SourceFacadeConnectionString;
            if (!configuration.GetSection(SectionName).GetSection(nameof(AllowDbOwner)).Exists())
                options.AllowDbOwner = legacy.AllowDbOwner;
        }

        if (string.IsNullOrWhiteSpace(options.Owned.ApplicationName))
            options.Owned.ApplicationName = "ShowcaseDataService.Owned";
        if (string.IsNullOrWhiteSpace(options.SourceFacade.ApplicationName))
            options.SourceFacade.ApplicationName = "ShowcaseDataService.SourceFacade";

        return options;
    }
}
