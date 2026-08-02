using BuildingBlocks.DataAccess.Abstractions;
using BuildingBlocks.DataAccess.Dapper;
using Xunit;

namespace ShowcaseDataService.Tests;

public class FluentDataAccessTests
{
    [Fact]
    public void ExecuteSp_ExecuteSP_And_ExecuteSql_Extensions_Exist()
    {
        IDbConnectionFactory factory = new SqlConnectionFactory(_ =>
            "Server=(localdb)\\mssqllocaldb;Database=ShowcaseOwned;Trusted_Connection=True;TrustServerCertificate=True");
        var timings = new InMemoryDataAccessTimingStore();
        var ctx = new DataAccessContext(factory, timings);

        Assert.NotNull(ctx.ExecuteSp<object>("showcase.GetShowcaseSummary"));
        Assert.NotNull(ctx.ExecuteSP<object>("showcase.GetShowcaseSummary"));
        Assert.NotNull(ctx.ExecuteSql<object>("SELECT 1 AS Id"));
        Assert.NotNull(factory.ExecuteSP<object>("showcase.GetShowcaseSummary", timings));
        Assert.NotNull(factory.ExecuteSql<object>("SELECT 1 AS Id", timings));
    }

    [Fact]
    public void Fluent_Supports_Map_Timeout_Named_Chain()
    {
        IDbConnectionFactory factory = new SqlConnectionFactory(_ =>
            "Server=(localdb)\\mssqllocaldb;Database=ShowcaseOwned;Trusted_Connection=True;TrustServerCertificate=True");
        var query = new DataAccessContext(factory)
            .ExecuteSql<int>("SELECT 1")
            .On("Owned")
            .WithParameters(new { })
            .Named("probe")
            .Timeout(5)
            .Map(x => x);

        Assert.NotNull(query);
    }

    [Fact]
    public void TimingStore_Summarize_OrdersByAvg()
    {
        var store = new InMemoryDataAccessTimingStore();
        store.Record(new DataAccessTimingSample
        {
            Operation = "op",
            Method = DataAccessMethod.PlainSql,
            ConnectionName = "Owned",
            ElapsedMs = 2,
            RowCount = 1
        });
        store.Record(new DataAccessTimingSample
        {
            Operation = "op",
            Method = DataAccessMethod.StoredProcedure,
            ConnectionName = "Owned",
            ElapsedMs = 8,
            RowCount = 1
        });

        var stats = store.Summarize("op");
        Assert.Equal(2, stats.Count);
        Assert.Equal(DataAccessMethod.PlainSql, stats[0].Method);
        Assert.True(stats[0].AvgMs < stats[1].AvgMs);
    }
}
