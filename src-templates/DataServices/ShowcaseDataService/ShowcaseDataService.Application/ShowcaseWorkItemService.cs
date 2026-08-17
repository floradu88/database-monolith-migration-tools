using System.Diagnostics;
using BuildingBlocks.DataAccess.Abstractions;
using BuildingBlocks.Migration;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ShowcaseDataService.Contracts;

namespace ShowcaseDataService.Application;

public sealed class ShowcaseWorkItemService : IShowcaseWorkItemService
{
    public const string PairName = "showcase-workitem";

    private readonly IShowcaseWorkItemAccess _access;
    private readonly IParallelWriteExecutor _executor;
    private readonly IParallelWriteStore _store;
    private readonly MigrationRoutingOptions _routing;
    private readonly ShowcaseWorkItemProcedureNames _names;
    private readonly IHttpContextAccessor _http;
    private readonly ILogger<ShowcaseWorkItemService> _logger;

    public ShowcaseWorkItemService(
        IShowcaseWorkItemAccess access,
        IParallelWriteExecutor executor,
        IParallelWriteStore store,
        IOptions<MigrationRoutingOptions> routing,
        IOptions<ShowcaseWorkItemProcedureNames> names,
        IHttpContextAccessor http,
        ILogger<ShowcaseWorkItemService> logger)
    {
        _access = access;
        _executor = executor;
        _store = store;
        _routing = routing.Value;
        _names = names.Value;
        _http = http;
        _logger = logger;
    }

    public Task UpsertAsync(ShowcaseWorkItemRequest request, CancellationToken cancellationToken = default) =>
        WriteAsync(
            "WorkItemUpsert",
            request.ExternalId.ToString(),
            ct => _access.UpsertAsync(request, _names.DboUpsert, "Owned", ct),
            ct => _access.UpsertAsync(request, _names.CoreUpsert, "Owned", ct),
            cancellationToken);

    public Task DeleteAsync(Guid externalId, CancellationToken cancellationToken = default) =>
        WriteAsync(
            "WorkItemDelete",
            externalId.ToString(),
            ct => _access.DeleteAsync(externalId, _names.DboDelete, "Owned", ct),
            ct => _access.DeleteAsync(externalId, _names.CoreDelete, "Owned", ct),
            cancellationToken);

    public async Task<TableIntegrityDto> CheckIntegrityAsync(CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        TableIntegritySqlRow? row = null;
        try
        {
            row = await _access.CheckIntegrityAsync("Owned", cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Integrity check failed to execute against SQL; returning in-memory evidence only.");
        }

        sw.Stop();
        ParallelWriteInstrumentation.IntegrityChecks.Add(1);
        ParallelWriteInstrumentation.IntegrityDurationMs.Record(sw.Elapsed.TotalMilliseconds);

        var snapshot = new TableIntegritySnapshot
        {
            PairName = PairName,
            IsMatch = row?.IsMatch ?? true,
            DboDeltaCount = row?.DboDeltaCount ?? 0,
            CoreCount = row?.CoreCount ?? 0,
            MissingInCoreCount = row?.MissingInCoreCount ?? 0,
            MissingInDboCount = row?.MissingInDboCount ?? 0,
            DurationMs = row?.DurationMs ?? sw.ElapsedMilliseconds,
            SampleDiff = row?.SampleDiff,
            CheckedAt = row?.CheckedAtUtc ?? DateTimeOffset.UtcNow
        };
        if (!snapshot.IsMatch)
            ParallelWriteInstrumentation.IntegrityMismatches.Add(1);
        _store.AddIntegrity(snapshot);
        _logger.LogInformation(
            "Integrity {Pair} match={Match} dboDelta={Dbo} core={Core} missingCore={MissingCore} missingDbo={MissingDbo} ms={Ms}",
            PairName, snapshot.IsMatch, snapshot.DboDeltaCount, snapshot.CoreCount,
            snapshot.MissingInCoreCount, snapshot.MissingInDboCount, snapshot.DurationMs);

        return ToDto(snapshot);
    }

    private async Task WriteAsync(
        string operation,
        string businessKey,
        Func<CancellationToken, Task> dbo,
        Func<CancellationToken, Task> core,
        CancellationToken cancellationToken)
    {
        var route = ResolveRoute();
        if (route == DataAccessRoute.Owned)
        {
            await core(cancellationToken);
            return;
        }

        if (route != DataAccessRoute.ParallelWrite)
        {
            await dbo(cancellationToken);
            return;
        }

        var result = await _executor.ExecuteAsync(
            operation,
            businessKey,
            dbo,
            core,
            _routing.ParallelWriteCoreTimeoutMs,
            cancellationToken);

        try
        {
            await _access.LogCallAsync(result, "Owned", cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to persist dual-write call log for {Operation} {Key}", operation, businessKey);
        }
    }

    private DataAccessRoute ResolveRoute()
    {
        var header = _http.HttpContext?.Request.Headers[_routing.RouteHeaderName].FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(header) && Enum.TryParse<DataAccessRoute>(header, true, out var parsed))
            return parsed;
        return _routing.DefaultRoute;
    }

    public static TableIntegrityDto ToDto(TableIntegritySnapshot snapshot) =>
        new(
            snapshot.PairName,
            snapshot.IsMatch,
            snapshot.DboDeltaCount,
            snapshot.CoreCount,
            snapshot.MissingInCoreCount,
            snapshot.MissingInDboCount,
            snapshot.DurationMs,
            snapshot.SampleDiff,
            snapshot.CheckedAt);
}

public sealed class ShowcaseWorkItemProcedureNames
{
    public const string SectionName = "Database";
    public string LegacySchema { get; set; } = "dbo";
    public string CoreSchema { get; set; } = "core";
    public string WorkItemUpsertProcedure { get; set; } = "usp_ShowcaseWorkItem_Upsert";
    public string WorkItemDeleteProcedure { get; set; } = "usp_ShowcaseWorkItem_Delete";
    public string IntegrityProcedure { get; set; } = "usp_TableIntegrity_Check";
    public string LogCallProcedure { get; set; } = "usp_LogDualWriteCall";

    public string DboUpsert => $"{LegacySchema}.{WorkItemUpsertProcedure}";
    public string CoreUpsert => $"{CoreSchema}.{WorkItemUpsertProcedure}";
    public string DboDelete => $"{LegacySchema}.{WorkItemDeleteProcedure}";
    public string CoreDelete => $"{CoreSchema}.{WorkItemDeleteProcedure}";
    public string Integrity => $"{CoreSchema}.{IntegrityProcedure}";
    public string LogCall => $"{CoreSchema}.{LogCallProcedure}";
}
