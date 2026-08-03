using BuildingBlocks.DataAccess.Abstractions;
using Microsoft.Extensions.Options;
using ShowcaseDataService.Contracts;

namespace ShowcaseDataService.Infrastructure.StoredProcedures;

/// <summary>
/// Sample SP wrapper: <c>context.ExecuteSP&lt;T&gt;().WithParameters(...).ToListAsync()</c>.
/// Procedure schema comes from <see cref="ShowcaseDatabaseOptions"/> (single config place).
/// </summary>
public interface ISpGetShowcaseSummary
{
    Task<ShowcaseSummaryDto?> ExecuteAsync(Guid id, string connectionName, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ShowcaseSummaryDto>> ExecuteListAsync(string connectionName, CancellationToken cancellationToken = default);
}

public sealed class SpGetShowcaseSummary : ISpGetShowcaseSummary
{
    private readonly IDataAccessContext _access;
    private readonly ShowcaseDatabaseOptions _database;

    public SpGetShowcaseSummary(IDataAccessContext access, IOptions<ShowcaseDatabaseOptions> database)
    {
        _access = access;
        _database = database.Value;
    }

    public string ProcedureName => _database.GetShowcaseSummaryProcedure;

    public Task<ShowcaseSummaryDto?> ExecuteAsync(Guid id, string connectionName, CancellationToken cancellationToken = default) =>
        _access.ExecuteSP<ShowcaseSummaryDto>(ProcedureName)
            .On(connectionName)
            .WithParameters(new { Id = id })
            .Named("GetShowcaseSummary")
            .FirstOrDefaultAsync(cancellationToken);

    public async Task<IReadOnlyList<ShowcaseSummaryDto>> ExecuteListAsync(string connectionName, CancellationToken cancellationToken = default) =>
        await _access.ExecuteSP<ShowcaseSummaryDto>(ProcedureName)
            .On(connectionName)
            .Named("GetShowcaseSummary")
            .ToListAsync(cancellationToken);
}
