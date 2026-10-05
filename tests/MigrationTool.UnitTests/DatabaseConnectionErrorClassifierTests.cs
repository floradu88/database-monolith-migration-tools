using MigrationTool.Domain;

namespace MigrationTool.UnitTests;

public sealed class DatabaseConnectionErrorClassifierTests
{
    [Theory]
    [InlineData(18456, DatabaseConnectionErrorKind.LoginFailed)]
    [InlineData(4060, DatabaseConnectionErrorKind.DatabaseNotFound)]
    [InlineData(10060, DatabaseConnectionErrorKind.Timeout)]
    [InlineData(10061, DatabaseConnectionErrorKind.ConnectionRefused)]
    [InlineData(53, DatabaseConnectionErrorKind.NetworkUnreachable)]
    public void Maps_sql_server_numbers(int number, DatabaseConnectionErrorKind expected)
    {
        var error = DatabaseConnectionErrorClassifier.FromSqlServer(number, "provider text");
        Assert.Equal(expected, error.Kind);
        Assert.False(string.IsNullOrWhiteSpace(error.Guidance));
    }

    [Fact]
    public void Maps_tls_from_message()
    {
        var error = DatabaseConnectionErrorClassifier.FromSqlServer(
            null,
            "A connection was successfully established, but then an error occurred during the login process. certificate chain");
        Assert.Equal(DatabaseConnectionErrorKind.TlsOrEncrypt, error.Kind);
    }
}
