using BuildingBlocks.DataAccess.Abstractions;
using BuildingBlocks.DataAccess.Dapper;
using BuildingBlocks.Migration;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using ShowcaseDataService.Application;
using ShowcaseDataService.Contracts;
using Xunit;

namespace ShowcaseDataService.Tests;

public class ShowcaseItemServiceTests
{
    [Fact]
    public async Task SourceFacade_UsesSourceConnection()
    {
        var access = new FakeAccess();
        var svc = CreateService(access, DataAccessRoute.SourceFacade, BlueGreenSlot.Blue);
        await svc.GetSummaryAsync(Guid.NewGuid());
        Assert.Equal("Source", access.LastConnection);
        Assert.Equal(DataAccessMethod.StoredProcedure, access.LastMethod);
    }

    [Fact]
    public async Task Owned_UsesConfiguredMethod()
    {
        var access = new FakeAccess();
        var svc = CreateService(access, DataAccessRoute.Owned, BlueGreenSlot.Green, method: DataAccessMethod.PlainSql);
        await svc.GetSummaryAsync(Guid.NewGuid());
        Assert.Equal("Owned", access.LastConnection);
        Assert.Equal(DataAccessMethod.PlainSql, access.LastMethod);
    }

    [Fact]
    public async Task MethodHeader_OverridesAuthoritativeMethod()
    {
        var access = new FakeAccess();
        var http = new DefaultHttpContext();
        http.Request.Headers["X-Data-Access-Method"] = "StoredProcedure";
        var svc = CreateService(access, DataAccessRoute.Owned, BlueGreenSlot.Green, method: DataAccessMethod.EfCore, http: http);
        await svc.GetSummaryAsync(Guid.NewGuid());
        Assert.Equal(DataAccessMethod.StoredProcedure, access.LastMethod);
    }

    [Fact]
    public async Task Shadow_ComparesBoth_AndStoresResult()
    {
        var access = new FakeAccess();
        var store = new InMemoryShadowCompareStore();
        var svc = CreateService(access, DataAccessRoute.Shadow, BlueGreenSlot.Blue, store);
        await svc.GetSummaryAsync(Guid.Parse("11111111-1111-1111-1111-111111111111"));
        Assert.Contains("Source", access.Connections);
        Assert.Contains("Owned", access.Connections);
        Assert.Single(store.Recent());
    }

    [Fact]
    public async Task Benchmark_ReportsFastestAmongEfSpSql()
    {
        var access = new FakeAccess();
        var timings = new InMemoryDataAccessTimingStore();
        timings.Record(new DataAccessTimingSample
        {
            Operation = "GetShowcaseSummary",
            Method = DataAccessMethod.PlainSql,
            ConnectionName = "Owned",
            ElapsedMs = 1,
            RowCount = 1
        });
        timings.Record(new DataAccessTimingSample
        {
            Operation = "GetShowcaseSummary",
            Method = DataAccessMethod.StoredProcedure,
            ConnectionName = "Owned",
            ElapsedMs = 5,
            RowCount = 1
        });
        timings.Record(new DataAccessTimingSample
        {
            Operation = "GetShowcaseSummary",
            Method = DataAccessMethod.EfCore,
            ConnectionName = "Owned",
            ElapsedMs = 10,
            RowCount = 1
        });

        var svc = CreateService(access, DataAccessRoute.Owned, BlueGreenSlot.Green, timings: timings);
        var result = await svc.BenchmarkAccessAsync(Guid.NewGuid());
        Assert.NotNull(result.FastestMethod);
        Assert.Contains(result.CumulativeStats, s => s.IsFastest && s.Method == nameof(DataAccessMethod.PlainSql));
        Assert.True(result.PayloadsMatch);
    }

    [Fact]
    public async Task Dashboard_IncludesSloCounters()
    {
        var access = new FakeAccess();
        var svc = CreateService(access, DataAccessRoute.Owned, BlueGreenSlot.Green);
        await svc.GetSummaryAsync(Guid.NewGuid());
        var dash = svc.GetDashboard();
        Assert.True(dash.Slo.Requests >= 1);
        Assert.Equal(nameof(DataAccessMethod.EfCore), dash.AuthoritativeMethod);
    }

    private static ShowcaseItemService CreateService(
        FakeAccess access,
        DataAccessRoute route,
        BlueGreenSlot slot,
        IShadowCompareStore? store = null,
        IDataAccessTimingStore? timings = null,
        DataAccessMethod method = DataAccessMethod.EfCore,
        DefaultHttpContext? http = null)
    {
        http ??= new DefaultHttpContext();
        var accessor = new HttpContextAccessor { HttpContext = http };
        return new ShowcaseItemService(
            access,
            store ?? new InMemoryShadowCompareStore(),
            timings ?? new InMemoryDataAccessTimingStore(),
            new InMemoryShowcaseSloCounter(),
            new InMemoryParallelWriteStore(),
            Options.Create(new MigrationRoutingOptions
            {
                DefaultRoute = route,
                Slot = slot,
                AuthoritativeMethod = method
            }),
            Options.Create(new ShowcaseSloOptions()),
            accessor);
    }

    private sealed class FakeAccess : IShowcaseDataAccess
    {
        public string? LastConnection { get; private set; }
        public DataAccessMethod? LastMethod { get; private set; }
        public List<string> Connections { get; } = [];

        public Task<ShowcaseSummaryDto?> GetSummaryAsync(Guid id, string connectionName, DataAccessMethod method, CancellationToken cancellationToken = default)
        {
            LastConnection = connectionName;
            LastMethod = method;
            Connections.Add(connectionName);
            return Task.FromResult<ShowcaseSummaryDto?>(new ShowcaseSummaryDto(id, "n", "Active", connectionName));
        }

        public Task<ShowcaseSummaryDto?> GetSummaryViaEfAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult<ShowcaseSummaryDto?>(new ShowcaseSummaryDto(id, "n", "Active", "Owned-EF"));

        public Task<ShowcaseSummaryDto?> GetSummaryViaSpAsync(Guid id, string connectionName, CancellationToken cancellationToken = default) =>
            Task.FromResult<ShowcaseSummaryDto?>(new ShowcaseSummaryDto(id, "n", "Active", $"{connectionName}-SP"));

        public Task<ShowcaseSummaryDto?> GetSummaryViaSqlAsync(Guid id, string connectionName, CancellationToken cancellationToken = default) =>
            Task.FromResult<ShowcaseSummaryDto?>(new ShowcaseSummaryDto(id, "n", "Active", $"{connectionName}-SQL"));

        public Task<DataAccessCompareResult<ShowcaseSummaryDto>> CompareAccessMethodsAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(new DataAccessCompareResult<ShowcaseSummaryDto>
            {
                Operation = "GetShowcaseSummary",
                EfResult = new ShowcaseSummaryDto(id, "n", "Active", "Owned-EF"),
                SpResult = new ShowcaseSummaryDto(id, "n", "Active", "Owned-SP"),
                SqlResult = new ShowcaseSummaryDto(id, "n", "Active", "Owned-SQL"),
                EfMs = 10,
                SpMs = 5,
                SqlMs = 1,
                Fastest = DataAccessMethod.PlainSql,
                PayloadsMatch = true
            });

        public Task UpdateAsync(ShowcaseUpdateRequest request, string connectionName, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
