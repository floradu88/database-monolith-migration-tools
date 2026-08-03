using BuildingBlocks.DataAccess.Abstractions;
using Microsoft.Extensions.Options;
using ShowcaseDataService.Contracts;
using ShowcaseDataService.Domain;
using ShowcaseDataService.Infrastructure;

namespace ShowcaseDataService.Infrastructure.StoredProcedures;

/// <summary>
/// Templated SP sample: resolves <c>usp_Showcase_{Area}_{Action}</c> via enums, then executes.
/// </summary>
public interface ISpGetShowcaseReport
{
    Task<ShowcaseSummaryDto?> ExecuteAsync(
        Guid id,
        ShowcaseReportArea area,
        ShowcaseReportAction action,
        string connectionName,
        CancellationToken cancellationToken = default);
}

public sealed class SpGetShowcaseReport : ISpGetShowcaseReport
{
    private readonly IDataAccessContext _access;
    private readonly ShowcaseDatabaseOptions _database;

    public SpGetShowcaseReport(IDataAccessContext access, IOptions<ShowcaseDatabaseOptions> database)
    {
        _access = access;
        _database = database.Value;
    }

    public string ResolveProcedureName(ShowcaseReportArea area, ShowcaseReportAction action) =>
        _database.Procedure(ShowcaseProcedureNames.Report(area, action));

    public Task<ShowcaseSummaryDto?> ExecuteAsync(
        Guid id,
        ShowcaseReportArea area,
        ShowcaseReportAction action,
        string connectionName,
        CancellationToken cancellationToken = default)
    {
        var procedure = ResolveProcedureName(area, action);
        return _access.ExecuteSP<ShowcaseSummaryDto>(procedure)
            .On(connectionName)
            .WithParameters(new { Id = id })
            .Named($"GetShowcaseReport:{area}:{action}")
            .FirstOrDefaultAsync(cancellationToken);
    }
}
