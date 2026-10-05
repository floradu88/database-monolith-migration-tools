using MigrationTool.Domain;

namespace MigrationTool.UnitTests;

public sealed class DatabaseEndpointParserTests
{
    [Fact]
    public void Parses_aws_rds_data_source_with_comma_port()
    {
        var endpoint = DatabaseEndpointParser.TryParse(
            "mydb.abc123.us-east-1.rds.amazonaws.com,1433",
            DatabaseProviderKind.SqlServer);

        Assert.NotNull(endpoint);
        Assert.Equal("mydb.abc123.us-east-1.rds.amazonaws.com", endpoint!.Host);
        Assert.Equal(1433, endpoint.Port);
    }

    [Fact]
    public void Defaults_sql_server_port_to_1433()
    {
        var endpoint = DatabaseEndpointParser.TryParse(
            "mydb.abc123.us-east-1.rds.amazonaws.com",
            DatabaseProviderKind.SqlServer);

        Assert.NotNull(endpoint);
        Assert.Equal(1433, endpoint!.Port);
    }

    [Fact]
    public void Strips_tcp_prefix()
    {
        var endpoint = DatabaseEndpointParser.TryParse(
            "tcp:server.database.windows.net,1433",
            DatabaseProviderKind.SqlServer);

        Assert.NotNull(endpoint);
        Assert.Equal("server.database.windows.net", endpoint!.Host);
        Assert.Equal(1433, endpoint.Port);
    }

    [Fact]
    public void Uses_explicit_postgres_port()
    {
        var endpoint = DatabaseEndpointParser.TryParse(
            "db.example.com",
            DatabaseProviderKind.PostgreSql,
            explicitPort: 5432);

        Assert.NotNull(endpoint);
        Assert.Equal(5432, endpoint!.Port);
    }
}
