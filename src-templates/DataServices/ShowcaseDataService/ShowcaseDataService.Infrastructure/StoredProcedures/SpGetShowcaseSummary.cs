using BuildingBlocks.DataAccess.Abstractions;
using BuildingBlocks.DataAccess.Dapper;
using ShowcaseDataService.Contracts;

namespace ShowcaseDataService.Infrastructure.StoredProcedures;

/// <summary>
/// Sample SP wrapper using fluent <c>ExecuteSp&lt;T&gt;().ToListAsync()</c> / FirstOrDefaultAsync.
/// FindingsMigration can generate additional Sp_* classes from stored-procedure-map.json.
/// </summary>
public interface ISpGetShowcaseSummary
{
    Task<ShowcaseSummaryDto?> ExecuteAsync(Guid id, string connectionName, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ShowcaseSummaryDto>> ExecuteListAsync(string connectionName, CancellationToken cancellationToken = default);
}

public sealed class SpGetShowcaseSummary : ISpGetShowcaseSummary
{
    public const string ProcedureName = "showcase.GetShowcaseSummary";
    private readonly IDataAccessContext _access;

    public SpGetShowcaseSummary(IDataAccessContext access) => _access = access;

    public Task<ShowcaseSummaryDto?> ExecuteAsync(Guid id, string connectionName, CancellationToken cancellationToken = default) =>
        _access.ExecuteSp<ShowcaseSummaryDto>(ProcedureName)
            .On(connectionName)
            .WithParameters(new { Id = id })
            .Named("GetShowcaseSummary")
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<ShowcaseSummaryDto>> ExecuteListAsync(string connectionName, CancellationToken cancellationToken = default) =>
        await _access.ExecuteSp<ShowcaseSummaryDto>(ProcedureName)
            .On(connectionName)
            .Named("GetShowcaseSummary")
            .ToListAsync(cancellationToken);
}
