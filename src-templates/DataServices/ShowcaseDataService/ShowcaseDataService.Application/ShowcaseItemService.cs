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
    private readonly IShowcaseSloCounter _slo;
    private readonly IParallelWriteStore _parallelStore;
    private readonly MigrationRoutingOptions _options;
    private readonly ShowcaseSloOptions _sloOptions;
    private readonly IHttpContextAccessor _httpContextAccessor;

    public ShowcaseItemService(
        IShowcaseDataAccess dataAccess,
        IShadowCompareStore shadowStore,
        IDataAccessTimingStore timingStore,
        IShowcaseSloCounter slo,
        IParallelWriteStore parallelStore,
        IOptions<MigrationRoutingOptions> options,
        IOptions<ShowcaseSloOptions> sloOptions,
        IHttpContextAccessor httpContextAccessor)
    {
        _dataAccess = dataAccess;
        _shadowStore = shadowStore;
        _timingStore = timingStore;
        _slo = slo;
        _parallelStore = parallelStore;
        _options = options.Value;
        _sloOptions = sloOptions.Value;
        _httpContextAccessor = httpContextAccessor;
    }

    public async Task<ShowcaseSummaryDto?> GetSummaryAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var route = ResolveRoute();
            var method = ResolveMethod();
            ShowcaseSummaryDto? result = route switch
            {
                DataAccessRoute.Owned => await _dataAccess.GetSummaryAsync(id, "Owned", method, cancellationToken),
                DataAccessRoute.Shadow => await GetWithShadowAsync(id, cancellationToken),
                _ => await _dataAccess.GetSummaryAsync(id, "Source", DataAccessMethod.StoredProcedure, cancellationToken)
            };
            sw.Stop();
            _slo.RecordSuccess(sw.ElapsedMilliseconds);
            return result;
        }
        catch
        {
            _slo.RecordError();
            throw;
        }
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
        var slo = _slo.Snapshot(_sloOptions);
        var parallel = _parallelStore.Snapshot();
        var parallelDto = new ParallelWriteDashboardDto(
            parallel.Calls,
            parallel.DboFailures,
            parallel.CoreFailures,
            parallel.CoreTimeouts,
            parallel.IntegrityChecks,
            parallel.IntegrityMismatches,
            parallel.DboP95Ms,
            parallel.CoreP95Ms,
            parallel.LastIntegrityMatch,
            _parallelStore.RecentCalls(20).Select(c => new ParallelWriteCallDto(
                c.Operation, c.BusinessKey, c.DboSucceeded, c.CoreSucceeded, c.CoreTimedOut,
                c.DboDurationMs, c.CoreDurationMs, c.CoreError, c.CalledAt)).ToList(),
            _parallelStore.RecentIntegrity(10).Select(ShowcaseWorkItemService.ToDto).ToList());
        return new ShowcaseDashboardDto(
            ResolveSlot().ToString(),
            ResolveRoute().ToString(),
            ResolveMethod().ToString(),
            recent.Count,
            recent.Count(r => r.PayloadsMatch),
            recent.Count(r => !r.PayloadsMatch),
            recent.Select(r => new ShadowDiffDto(
                r.Operation, r.RouteA, r.RouteB, r.ElapsedMsA, r.ElapsedMsB, r.PayloadsMatch, r.DiffSummary, r.ComparedAt)).ToList(),
            stats,
            fastest is null ? null : $"{fastest} has lowest avg ms for GetShowcaseSummary",
            new SloCountersDto(
                slo.Requests,
                slo.Errors,
                slo.ErrorRatePercent,
                slo.P95Ms,
                _sloOptions.ReadLatencyP95MsBudget,
                slo.WithinLatencyBudget,
                slo.WithinErrorBudget),
            parallelDto);
    }

    public async Task<AccessBenchmarkDto> BenchmarkAccessAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var compare = await _dataAccess.CompareAccessMethodsAsync(id, cancellationToken);
        return new AccessBenchmarkDto(
            id,
            compare.EfResult,
            compare.SpResult,
            compare.SqlResult,
            compare.EfMs,
            compare.SpMs,
            compare.SqlMs,
            compare.Fastest.ToString(),
            compare.PayloadsMatch,
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
        var source = await _dataAccess.GetSummaryAsync(id, "Source", DataAccessMethod.StoredProcedure, cancellationToken);
        swA.Stop();

        var swB = Stopwatch.StartNew();
        var owned = await _dataAccess.GetSummaryAsync(id, "Owned", ResolveMethod(), cancellationToken);
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

    private DataAccessMethod ResolveMethod()
    {
        var header = _httpContextAccessor.HttpContext?.Request.Headers[_options.MethodHeaderName].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(header) && Enum.TryParse<DataAccessMethod>(header, true, out var parsed))
            return parsed;
        return _options.AuthoritativeMethod;
    }
}
