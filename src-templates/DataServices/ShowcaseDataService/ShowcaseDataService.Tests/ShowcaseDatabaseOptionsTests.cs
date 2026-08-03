using BuildingBlocks.Security;
using Microsoft.Extensions.Configuration;
using ShowcaseDataService.Infrastructure;
using Xunit;

namespace ShowcaseDataService.Tests;

public class ShowcaseDatabaseOptionsTests
{
    [Fact]
    public void FromConfiguration_Prefers_Database_Section()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:Schema"] = "owned_schema",
                ["Database:Owned:ConnectionString"] = "Server=a;Database=OwnedDb;Trusted_Connection=True;TrustServerCertificate=True",
                ["Database:SourceFacade:ConnectionString"] = "Server=a;Database=Monolith;Trusted_Connection=True;TrustServerCertificate=True",
                ["Database:Owned:Provider"] = "OnPrem",
                ["SqlConnections:OwnedConnectionString"] = "Server=legacy;Database=IgnoreMe;Trusted_Connection=True;TrustServerCertificate=True"
            })
            .Build();

        var options = ShowcaseDatabaseOptions.FromConfiguration(config);
        Assert.Equal("owned_schema", options.NormalizedSchema);
        Assert.Contains("OwnedDb", options.ResolveOwnedConnectionString());
        Assert.Contains("Monolith", options.ResolveSourceFacadeConnectionString());
        Assert.Equal(SqlHostProvider.OnPrem, options.Owned.ParsedProvider);
    }

    [Fact]
    public void FromConfiguration_Falls_Back_To_Legacy_SqlConnections()
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["SqlConnections:OwnedConnectionString"] = "Server=legacy;Database=OwnedFromLegacy;Trusted_Connection=True;TrustServerCertificate=True",
                ["SqlConnections:SourceFacadeConnectionString"] = "Server=legacy;Database=SourceFromLegacy;Trusted_Connection=True;TrustServerCertificate=True"
            })
            .Build();

        var options = ShowcaseDatabaseOptions.FromConfiguration(config);
        Assert.Equal(ShowcaseDatabaseOptions.DefaultSchema, options.NormalizedSchema);
        Assert.Contains("OwnedFromLegacy", options.ResolveOwnedConnectionString());
        Assert.Contains("SourceFromLegacy", options.ResolveSourceFacadeConnectionString());
    }
}

public class SqlHostProviderTests
{
    [Fact]
    public void Compose_Azure_ManagedIdentity()
    {
        var cs = SqlConnectionStringComposer.Resolve(new SqlEndpointOptions
        {
            Provider = nameof(SqlHostProvider.Azure),
            AuthMode = nameof(SqlAuthMode.AzureManagedIdentity),
            Server = "showcase.database.windows.net",
            DatabaseName = "ShowcaseOwned",
            ManagedIdentityClientId = "00000000-0000-0000-0000-000000000001",
            ApplicationName = "ShowcaseDataService.Owned"
        });

        Assert.Contains("database.windows.net", cs, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Authentication=Active Directory Managed Identity", cs, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Encrypt=True", cs, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("Trusted_Connection", cs, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Compose_Aws_SqlPassword()
    {
        var cs = SqlConnectionStringComposer.Resolve(new SqlEndpointOptions
        {
            Provider = nameof(SqlHostProvider.Aws),
            AuthMode = nameof(SqlAuthMode.SqlPassword),
            Server = "showcase.xxxxx.us-east-1.rds.amazonaws.com",
            Port = 1433,
            DatabaseName = "ShowcaseOwned",
            UserId = "app_rw",
            Password = "from-secret",
            ApplicationName = "ShowcaseDataService.Owned"
        });

        Assert.Contains("rds.amazonaws.com,1433", cs, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("User Id=app_rw", cs, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Encrypt=True", cs, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Guard_Rejects_OnPrem_Provider_With_Azure_Host()
    {
        Assert.Throws<InvalidOperationException>(() =>
            SqlConnectionGuard.EnsureProviderCompatible(
                SqlHostProvider.OnPrem,
                "Server=x.database.windows.net;Database=y;Encrypt=True"));
    }

    [Fact]
    public void Guard_Rejects_Azure_Trusted_Connection()
    {
        Assert.Throws<InvalidOperationException>(() =>
            SqlConnectionGuard.EnsureProviderCompatible(
                SqlHostProvider.Azure,
                "Server=x.database.windows.net;Database=y;Trusted_Connection=True;Encrypt=True"));
    }

    [Fact]
    public void InferProvider_Detects_Aws_And_Azure()
    {
        Assert.Equal(SqlHostProvider.Azure, SqlConnectionStringComposer.InferProvider("Server=a.database.windows.net;Database=d"));
        Assert.Equal(SqlHostProvider.Aws, SqlConnectionStringComposer.InferProvider("Server=a.rds.amazonaws.com;Database=d"));
        Assert.Equal(SqlHostProvider.OnPrem, SqlConnectionStringComposer.InferProvider("Server=sql1;Database=d"));
    }
}
