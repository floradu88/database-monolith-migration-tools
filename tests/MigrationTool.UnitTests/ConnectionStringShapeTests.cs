using MigrationTool.Domain;

namespace MigrationTool.UnitTests;

public sealed class ConnectionStringShapeTests
{
    [Fact]
    public void Accepts_aws_rds_data_source_with_port_1433()
    {
        const string cs =
            "Data Source=mydb.abc123.us-east-1.rds.amazonaws.com,1433;Initial Catalog=AppDb;User ID=admin;Password=secret;Encrypt=True;TrustServerCertificate=False;";

        var result = ConnectionStringShape.Validate(cs, DatabaseProviderKind.SqlServer);

        Assert.True(result.IsValid);
        Assert.Contains("mydb.abc123.us-east-1.rds.amazonaws.com,1433", result.Server);
        Assert.Equal("AppDb", result.Database);
        Assert.DoesNotContain("secret", result.RedactedConnectionString, StringComparison.Ordinal);
        Assert.Contains("***", result.RedactedConnectionString);
    }

    [Fact]
    public void Accepts_server_keyword_with_port_1433()
    {
        const string cs =
            "Server=mydb.abc123.eu-west-1.rds.amazonaws.com,1433;Database=AppDb;User Id=admin;Password=secret;Encrypt=True;";

        var result = ConnectionStringShape.Validate(cs, DatabaseProviderKind.SqlServer);

        Assert.True(result.IsValid);
        Assert.Equal("AppDb", result.Database);
    }

    [Fact]
    public void Rejects_missing_data_source()
    {
        const string cs = "Initial Catalog=AppDb;User ID=admin;Password=secret;";

        var result = ConnectionStringShape.Validate(cs, DatabaseProviderKind.SqlServer);

        Assert.False(result.IsValid);
        Assert.Contains(result.Errors, e => e.Contains("Data Source", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Warns_when_cloud_host_omits_port()
    {
        const string cs =
            "Data Source=mydb.abc123.us-east-1.rds.amazonaws.com;Initial Catalog=AppDb;User ID=admin;Password=secret;Encrypt=True;";

        var result = ConnectionStringShape.Validate(cs, DatabaseProviderKind.SqlServer);

        Assert.True(result.IsValid);
        Assert.Contains(result.Warnings, w => w.Contains("1433", StringComparison.Ordinal));
    }
}
