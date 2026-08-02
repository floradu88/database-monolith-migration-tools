using BuildingBlocks.Security;
using Xunit;

namespace ShowcaseDataService.Tests;

public class SqlConnectionGuardTests
{
    [Fact]
    public void Rejects_db_owner_in_connection_string()
    {
        var options = new SqlConnectionOptions
        {
            OwnedConnectionString = "Server=x;Database=y;User Id=a;Password=b;ApplicationIntent=ReadWrite;db_owner=true",
            AllowDbOwner = false
        };
        Assert.Throws<InvalidOperationException>(() => SqlConnectionGuard.EnsureLeastPrivilege(options));
    }
}
