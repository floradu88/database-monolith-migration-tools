using BuildingBlocks.Migration;
using ShowcaseDataService.Contracts;

namespace ShowcaseDataService.Application;

public interface IShowcaseItemService
{
    Task<ShowcaseSummaryDto?> GetSummaryAsync(Guid id, CancellationToken cancellationToken = default);
    Task UpdateAsync(ShowcaseUpdateRequest request, CancellationToken cancellationToken = default);
    ShowcaseDashboardDto GetDashboard();
    /// <summary>Run EF vs ExecuteSp vs ExecuteSql for the same id and record timings.</summary>
    Task<AccessBenchmarkDto> BenchmarkAccessAsync(Guid id, CancellationToken cancellationToken = default);
}

public interface IShowcaseDataAccess
{
    Task<ShowcaseSummaryDto?> GetSummaryAsync(Guid id, string connectionName, CancellationToken cancellationToken = default);
    Task<ShowcaseSummaryDto?> GetSummaryViaEfAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ShowcaseSummaryDto?> GetSummaryViaSpAsync(Guid id, string connectionName, CancellationToken cancellationToken = default);
    Task<ShowcaseSummaryDto?> GetSummaryViaSqlAsync(Guid id, string connectionName, CancellationToken cancellationToken = default);
    Task UpdateAsync(ShowcaseUpdateRequest request, string connectionName, CancellationToken cancellationToken = default);
}
