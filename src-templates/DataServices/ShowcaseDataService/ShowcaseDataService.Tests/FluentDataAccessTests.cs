using BuildingBlocks.DataAccess.Abstractions;
using BuildingBlocks.DataAccess.Dapper;
using Xunit;

namespace ShowcaseDataService.Tests;

public class FluentDataAccessTests
{
    [Fact]
    public void ExecuteSp_And_ExecuteSql_AreAvailable_AsExtensions()
    {
        IDbConnectionFactory factory = new SqlConnectionFactory(_ =>
            "Server=(localdb)\\mssqllocaldb;Database=ShowcaseOwned;Trusted_Connection=True;TrustServerCertificate=True");
        var timings = new InMemoryDataAccessTimingStore();
        var ctx = new DataAccessContext(factory, timings);

        var sp = ctx.ExecuteSp<object>("showcase.GetShowcaseSummary");
        var sql = ctx.ExecuteSql<object>("SELECT 1 AS Id");
        var viaExt = factory.ExecuteSp<object>("showcase.GetShowcaseSummary", timings);

        Assert.NotNull(sp);
        Assert.NotNull(sql);
        Assert.NotNull(viaExt);
    }

    [Fact]
    public void TimingStore_Summarize_MarksLowestAvg()
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
