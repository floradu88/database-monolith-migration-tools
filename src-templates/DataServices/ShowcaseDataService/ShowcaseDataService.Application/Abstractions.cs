using BuildingBlocks.DataAccess.Abstractions;
using BuildingBlocks.Migration;
using ShowcaseDataService.Contracts;

namespace ShowcaseDataService.Application;

public interface IShowcaseItemService
{
    Task<ShowcaseSummaryDto?> GetSummaryAsync(Guid id, CancellationToken cancellationToken = default);
    Task UpdateAsync(ShowcaseUpdateRequest request, CancellationToken cancellationToken = default);
    ShowcaseDashboardDto GetDashboard();
    Task<AccessBenchmarkDto> BenchmarkAccessAsync(Guid id, CancellationToken cancellationToken = default);
}

public interface IShowcaseDataAccess
{
    Task<ShowcaseSummaryDto?> GetSummaryAsync(Guid id, string connectionName, DataAccessMethod method, CancellationToken cancellationToken = default);
    Task<ShowcaseSummaryDto?> GetSummaryViaEfAsync(Guid id, CancellationToken cancellationToken = default);
    Task<ShowcaseSummaryDto?> GetSummaryViaSpAsync(Guid id, string connectionName, CancellationToken cancellationToken = default);
    Task<ShowcaseSummaryDto?> GetSummaryViaSqlAsync(Guid id, string connectionName, CancellationToken cancellationToken = default);
    Task<DataAccessCompareResult<ShowcaseSummaryDto>> CompareAccessMethodsAsync(Guid id, CancellationToken cancellationToken = default);
    Task UpdateAsync(ShowcaseUpdateRequest request, string connectionName, CancellationToken cancellationToken = default);
}
