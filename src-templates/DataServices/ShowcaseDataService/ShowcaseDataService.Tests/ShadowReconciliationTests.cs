using BuildingBlocks.DataAccess.Abstractions;
using BuildingBlocks.Migration;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using ShowcaseDataService.Application;
using ShowcaseDataService.Contracts;
using Xunit;

namespace ShowcaseDataService.Tests;

public class ShadowReconciliationTests
{
    [Fact]
    public async Task Shadow_Mismatch_IsVisibleOnDashboard()
    {
        var access = new MismatchAccess();
        var store = new InMemoryShadowCompareStore();
        var http = new DefaultHttpContext();
        var svc = new ShowcaseItemService(
            access,
            store,
            new InMemoryDataAccessTimingStore(),
            new InMemoryShowcaseSloCounter(),
            Options.Create(new MigrationRoutingOptions
            {
                DefaultRoute = DataAccessRoute.Shadow,
                Slot = BlueGreenSlot.Blue
            }),
            Options.Create(new ShowcaseSloOptions()),
            new HttpContextAccessor { HttpContext = http });

        await svc.GetSummaryAsync(Guid.NewGuid());
        var dash = svc.GetDashboard();
        Assert.Equal(1, dash.ShadowComparisons);
        Assert.Equal(0, dash.MatchingShadows);
        Assert.Equal(1, dash.MismatchingShadows);
        Assert.False(dash.RecentDiffs[0].PayloadsMatch);
    }

    private sealed class MismatchAccess : IShowcaseDataAccess
    {
        public Task<ShowcaseSummaryDto?> GetSummaryAsync(Guid id, string connectionName, DataAccessMethod method, CancellationToken cancellationToken = default)
        {
            var name = connectionName == "Source" ? "legacy" : "owned";
            return Task.FromResult<ShowcaseSummaryDto?>(new ShowcaseSummaryDto(id, name, "Active", connectionName));
        }

        public Task<ShowcaseSummaryDto?> GetSummaryViaEfAsync(Guid id, CancellationToken cancellationToken = default) =>
            GetSummaryAsync(id, "Owned", DataAccessMethod.EfCore, cancellationToken);

        public Task<ShowcaseSummaryDto?> GetSummaryViaSpAsync(Guid id, string connectionName, CancellationToken cancellationToken = default) =>
            GetSummaryAsync(id, connectionName, DataAccessMethod.StoredProcedure, cancellationToken);

        public Task<ShowcaseSummaryDto?> GetSummaryViaSqlAsync(Guid id, string connectionName, CancellationToken cancellationToken = default) =>
            GetSummaryAsync(id, connectionName, DataAccessMethod.PlainSql, cancellationToken);

        public Task<DataAccessCompareResult<ShowcaseSummaryDto>> CompareAccessMethodsAsync(Guid id, CancellationToken cancellationToken = default) =>
            Task.FromResult(new DataAccessCompareResult<ShowcaseSummaryDto>
            {
                Operation = "GetShowcaseSummary",
                Fastest = DataAccessMethod.PlainSql,
                PayloadsMatch = false
            });

        public Task UpdateAsync(ShowcaseUpdateRequest request, string connectionName, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
