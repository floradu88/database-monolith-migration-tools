using System.Diagnostics;
using System.Text.Json;
using BuildingBlocks.DataAccess.Abstractions;
using BuildingBlocks.Migration;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Options;
using ShowcaseDataService.Contracts;

namespace ShowcaseDataService.Application;

public sealed class ShowcaseItemService : IShowcaseItemService
{
    private readonly IShowcaseDataAccess _dataAccess;
    private readonly IShadowCompareStore _shadowStore;
    private readonly IDataAccessTimingStore _timingStore;
    private readonly MigrationRoutingOptions _options;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ShowcaseItemService(
        IShowcaseDataAccess dataAccess,
        IShadowCompareStore shadowStore,
        IDataAccessTimingStore timingStore,
        IOptions<MigrationRoutingOptions> options,
        IHttpContextAccessor httpContextAccessor)
    {
        _dataAccess = dataAccess;
        _shadowStore = shadowStore;
        _timingStore = timingStore;
        _options = options.Value;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<ShowcaseSummaryDto?> GetSummaryAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var route = ResolveRoute();
        return route switch
        {
            DataAccessRoute.Owned => await _dataAccess.GetSummaryAsync(id, "Owned", cancellationToken),
            DataAccessRoute.Shadow => await GetWithShadowAsync(id, cancellationToken),
            _ => await _dataAccess.GetSummaryAsync(id, "Source", cancellationToken)
        };
    }

    public async Task UpdateAsync(ShowcaseUpdateRequest request, CancellationToken cancellationToken = default)
    {
        var route = ResolveRoute();
        var connection = route == DataAccessRoute.Owned ? "Owned" : "Source";
        if (route == DataAccessRoute.Shadow)
            connection = ResolveSlot() == BlueGreenSlot.Green ? "Owned" : "Source";
        await _dataAccess.UpdateAsync(request, connection, cancellationToken);
    }

    public ShowcaseDashboardDto GetDashboard()
    {
        var recent = _shadowStore.Recent(20);
        var stats = BuildStats("GetShowcaseSummary");
        var fastest = stats.Where(s => s.IsFastest).Select(s => s.Method).FirstOrDefault();
        return new ShowcaseDashboardDto(
            ResolveSlot().ToString(),
            ResolveRoute().ToString(),
            recent.Count,
            recent.Count(r => r.PayloadsMatch),
            recent.Count(r => !r.PayloadsMatch),
            recent.Select(r => new ShadowDiffDto(
                r.Operation, r.RouteA, r.RouteB, r.ElapsedMsA, r.ElapsedMsB, r.PayloadsMatch, r.DiffSummary, r.ComparedAt)).ToList(),
            stats,
            fastest is null ? null : $"{fastest} has lowest avg ms for GetShowcaseSummary");
    }

    public async Task<AccessBenchmarkDto> BenchmarkAccessAsync(Guid id, CancellationToken cancellationToken = default)
    {
        const string connection = "Owned";

        var swEf = Stopwatch.StartNew();
        var ef = await _dataAccess.GetSummaryViaEfAsync(id, cancellationToken);
        swEf.Stop();

        var swSp = Stopwatch.StartNew();
        var sp = await _dataAccess.GetSummaryViaSpAsync(id, connection, cancellationToken);
        swSp.Stop();

        var swSql = Stopwatch.StartNew();
        var sql = await _dataAccess.GetSummaryViaSqlAsync(id, connection, cancellationToken);
        swSql.Stop();

        var ranked = new[]
        {
            (Method: nameof(DataAccessMethod.EfCore), Ms: swEf.ElapsedMilliseconds),
            (Method: nameof(DataAccessMethod.StoredProcedure), Ms: swSp.ElapsedMilliseconds),
            (Method: nameof(DataAccessMethod.PlainSql), Ms: swSql.ElapsedMilliseconds)
        };
        var fastest = ranked.OrderBy(x => x.Ms).First().Method;

        return new AccessBenchmarkDto(
            id,
            ef,
            sp,
            sql,
            swEf.ElapsedMilliseconds,
            swSp.ElapsedMilliseconds,
            swSql.ElapsedMilliseconds,
            fastest,
            BuildStats("GetShowcaseSummary"));
    }

    private List<AccessMethodStatDto> BuildStats(string operation)
    {
        var summary = _timingStore.Summarize(operation);
        if (summary.Count == 0)
            summary = _timingStore.Summarize();

        var byOp = summary.GroupBy(s => s.Operation, StringComparer.OrdinalIgnoreCase);
        var result = new List<AccessMethodStatDto>();
        foreach (var group in byOp)
        {
            var minAvg = group.Min(g => g.AvgMs);
            foreach (var s in group.OrderBy(g => g.AvgMs))
            {
                result.Add(new AccessMethodStatDto(
                    s.Operation,
                    s.Method.ToString(),
                    s.Samples,
                    Math.Round(s.AvgMs, 2),
                    s.MinMs,
                    s.MaxMs,
                    s.P95Ms,
                    Math.Abs(s.AvgMs - minAvg) < 0.0001));
            }
        }

        return result;
    }

    private async Task<ShowcaseSummaryDto?> GetWithShadowAsync(Guid id, CancellationToken cancellationToken)
    {
        var swA = Stopwatch.StartNew();
        var source = await _dataAccess.GetSummaryAsync(id, "Source", cancellationToken);
        swA.Stop();

        var swB = Stopwatch.StartNew();
        var owned = await _dataAccess.GetSummaryAsync(id, "Owned", cancellationToken);
        swB.Stop();

        var jsonA = JsonSerializer.Serialize(source);
        var jsonB = JsonSerializer.Serialize(owned);
        var match = string.Equals(jsonA, jsonB, StringComparison.Ordinal);
        _shadowStore.Add(new ShadowCompareResult
        {
            Operation = $"GetSummary:{id}",
            RouteA = "SourceFacade",
            RouteB = "Owned",
            ElapsedMsA = swA.ElapsedMilliseconds,
            ElapsedMsB = swB.ElapsedMilliseconds,
            PayloadsMatch = match,
            DiffSummary = match ? null : $"source={jsonA}; owned={jsonB}"
        });

        return ResolveSlot() == BlueGreenSlot.Green ? owned : source;
    }

    private DataAccessRoute ResolveRoute()
    {
        var header = _httpContextAccessor.HttpContext?.Request.Headers[_options.RouteHeaderName].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(header) && Enum.TryParse<DataAccessRoute>(header, true, out var parsed))
            return parsed;
        return _options.DefaultRoute;
    }

    private BlueGreenSlot ResolveSlot()
    {
        var header = _httpContextAccessor.HttpContext?.Request.Headers[_options.SlotHeaderName].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(header) && Enum.TryParse<BlueGreenSlot>(header, true, out var parsed))
            return parsed;
        return _options.Slot;
    }
}
