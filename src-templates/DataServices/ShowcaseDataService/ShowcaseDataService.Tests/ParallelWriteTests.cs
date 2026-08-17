using BuildingBlocks.DataAccess.Abstractions;
using BuildingBlocks.Migration;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using ShowcaseDataService.Application;
using ShowcaseDataService.Contracts;
using Xunit;

namespace ShowcaseDataService.Tests;

public class ParallelWriteTests
{
    [Fact]
    public async Task ParallelWrite_InvokesDboAndCore_AndDoesNotThrowWhenCoreFails()
    {
        var access = new FakeWorkItems { FailCore = true };
        var store = new InMemoryParallelWriteStore();
        var svc = Create(access, store, DataAccessRoute.ParallelWrite);

        await svc.UpsertAsync(new ShowcaseWorkItemRequest(Guid.NewGuid(), "n", "Active"));

        Assert.Contains(access.Calls, c => c.StartsWith("dbo.", StringComparison.OrdinalIgnoreCase) || c.Contains(".usp_ShowcaseWorkItem_Upsert", StringComparison.Ordinal));
        Assert.True(access.DboCalls >= 1);
        Assert.True(access.CoreCalls >= 1);
        var recent = store.RecentCalls();
        Assert.Single(recent);
        Assert.True(recent[0].DboSucceeded);
        Assert.False(recent[0].CoreSucceeded);
    }

    [Fact]
    public async Task SourceFacade_WritesDboOnly()
    {
        var access = new FakeWorkItems();
        var svc = Create(access, new InMemoryParallelWriteStore(), DataAccessRoute.SourceFacade);
        await svc.UpsertAsync(new ShowcaseWorkItemRequest(Guid.NewGuid(), "n", "Active"));
        Assert.Equal(1, access.DboCalls);
        Assert.Equal(0, access.CoreCalls);
    }

    [Fact]
    public async Task Owned_WritesCoreOnly()
    {
        var access = new FakeWorkItems();
        var svc = Create(access, new InMemoryParallelWriteStore(), DataAccessRoute.Owned);
        await svc.DeleteAsync(Guid.NewGuid());
        Assert.Equal(0, access.DboCalls);
        Assert.Equal(1, access.CoreCalls);
    }

    [Fact]
    public async Task ParallelWrite_PropagatesDboFailure()
    {
        var access = new FakeWorkItems { FailDbo = true };
        var svc = Create(access, new InMemoryParallelWriteStore(), DataAccessRoute.ParallelWrite);
        await Assert.ThrowsAsync<InvalidOperationException>(() =>
            svc.UpsertAsync(new ShowcaseWorkItemRequest(Guid.NewGuid(), "n", "Active")));
    }

    [Fact]
    public async Task Integrity_Mismatch_IsRecordedAsEvidence()
    {
        var access = new FakeWorkItems
        {
            Integrity = new TableIntegritySqlRow(1, false, 2, 1, 1, 0, 5, "missingInCore=1", DateTimeOffset.UtcNow)
        };
        var store = new InMemoryParallelWriteStore();
        var svc = Create(access, store, DataAccessRoute.ParallelWrite);
        var result = await svc.CheckIntegrityAsync();
        Assert.False(result.IsMatch);
        Assert.Equal(1, store.Snapshot().IntegrityMismatches);
        Assert.False(store.Snapshot().LastIntegrityMatch);
    }

    [Fact]
    public void Dashboard_IncludesParallelWriteMetrics()
    {
        var store = new InMemoryParallelWriteStore();
        store.AddCall(new ParallelWriteCallResult
        {
            Operation = "WorkItemUpsert",
            BusinessKey = Guid.NewGuid().ToString(),
            DboSucceeded = true,
            CoreSucceeded = false,
            CoreTimedOut = true,
            DboDurationMs = 4,
            CoreDurationMs = 2000,
            CoreError = "core timeout after 2000ms"
        });
        var http = new Microsoft.AspNetCore.Http.DefaultHttpContext();
        var itemSvc = new ShowcaseItemService(
            new ShowcaseItemServiceTestsAccess(),
            new InMemoryShadowCompareStore(),
            new InMemoryDataAccessTimingStore(),
            new InMemoryShowcaseSloCounter(),
            store,
            Options.Create(new MigrationRoutingOptions { DefaultRoute = DataAccessRoute.ParallelWrite }),
            Options.Create(new ShowcaseSloOptions()),
            new Microsoft.AspNetCore.Http.HttpContextAccessor { HttpContext = http });
        var dash = itemSvc.GetDashboard();
        Assert.Equal(1, dash.ParallelWrite.Calls);
        Assert.Equal(1, dash.ParallelWrite.CoreTimeouts);
        Assert.False(dash.ParallelWrite.RecentCalls[0].CoreSucceeded);
    }

    private static ShowcaseWorkItemService Create(
        FakeWorkItems access,
        IParallelWriteStore store,
        DataAccessRoute route)
    {
        var http = new Microsoft.AspNetCore.Http.DefaultHttpContext();
        return new ShowcaseWorkItemService(
            access,
            new ParallelWriteExecutor(store, NullLogger<ParallelWriteExecutor>.Instance),
            store,
            Options.Create(new MigrationRoutingOptions
            {
                DefaultRoute = route,
                ParallelWriteCoreTimeoutMs = 2000
            }),
            Options.Create(new ShowcaseWorkItemProcedureNames()),
            new Microsoft.AspNetCore.Http.HttpContextAccessor { HttpContext = http },
            NullLogger<ShowcaseWorkItemService>.Instance);
    }

    private sealed class FakeWorkItems : IShowcaseWorkItemAccess
    {
        public int DboCalls { get; private set; }
        public int CoreCalls { get; private set; }
        public List<string> Calls { get; } = [];
        public bool FailCore { get; init; }
        public bool FailDbo { get; init; }
        public TableIntegritySqlRow? Integrity { get; init; }

        public Task UpsertAsync(ShowcaseWorkItemRequest request, string procedureName, string connectionName, CancellationToken cancellationToken = default) =>
            Hit(procedureName);

        public Task DeleteAsync(Guid externalId, string procedureName, string connectionName, CancellationToken cancellationToken = default) =>
            Hit(procedureName);

        public Task<TableIntegritySqlRow?> CheckIntegrityAsync(string connectionName, CancellationToken cancellationToken = default) =>
            Task.FromResult(Integrity);

        public Task LogCallAsync(ParallelWriteCallResult call, string connectionName, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;

        private Task Hit(string procedureName)
        {
            Calls.Add(procedureName);
            var isCore = procedureName.StartsWith("core.", StringComparison.OrdinalIgnoreCase);
            if (isCore) CoreCalls++;
            else DboCalls++;
            if (isCore && FailCore) throw new InvalidOperationException("core failed");
            if (!isCore && FailDbo) throw new InvalidOperationException("dbo failed");
            return Task.CompletedTask;
        }
    }

    private sealed class ShowcaseItemServiceTestsAccess : IShowcaseDataAccess
    {
        public Task<ShowcaseSummaryDto?> GetSummaryAsync(Guid id, string connectionName, DataAccessMethod method, CancellationToken cancellationToken = default) =>
            Task.FromResult<ShowcaseSummaryDto?>(new ShowcaseSummaryDto(id, "n", "Active", connectionName));
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
                PayloadsMatch = true
            });
        public Task UpdateAsync(ShowcaseUpdateRequest request, string connectionName, CancellationToken cancellationToken = default) =>
            Task.CompletedTask;
    }
}
