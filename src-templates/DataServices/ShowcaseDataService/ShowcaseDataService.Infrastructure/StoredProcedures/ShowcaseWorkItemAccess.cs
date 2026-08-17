using BuildingBlocks.DataAccess.Abstractions;
using BuildingBlocks.Migration;
using Microsoft.Extensions.Options;
using ShowcaseDataService.Application;
using ShowcaseDataService.Contracts;

namespace ShowcaseDataService.Infrastructure.StoredProcedures;

public sealed class ShowcaseWorkItemAccess : IShowcaseWorkItemAccess
{
    private readonly IDataAccessContext _access;
    private readonly ShowcaseWorkItemProcedureNames _names;

    public ShowcaseWorkItemAccess(IDataAccessContext access, IOptions<ShowcaseWorkItemProcedureNames> names)
    {
        _access = access;
        _names = names.Value;
    }

    public Task UpsertAsync(
        ShowcaseWorkItemRequest request,
        string procedureName,
        string connectionName,
        CancellationToken cancellationToken = default) =>
        _access.ExecuteSp<object>(procedureName)
            .On(connectionName)
            .WithParameters(new { request.ExternalId, request.Name, request.Status })
            .Named("WorkItemUpsert")
            .Timeout(30)
            .ExecuteAsync(cancellationToken);

    public Task DeleteAsync(
        Guid externalId,
        string procedureName,
        string connectionName,
        CancellationToken cancellationToken = default) =>
        _access.ExecuteSp<object>(procedureName)
            .On(connectionName)
            .WithParameters(new { ExternalId = externalId })
            .Named("WorkItemDelete")
            .Timeout(30)
            .ExecuteAsync(cancellationToken);

    public Task<TableIntegritySqlRow?> CheckIntegrityAsync(string connectionName, CancellationToken cancellationToken = default) =>
        _access.ExecuteSp<TableIntegritySqlRow>(_names.Integrity)
            .On(connectionName)
            .WithParameters(new { PairName = ShowcaseWorkItemService.PairName })
            .Named("WorkItemIntegrity")
            .Timeout(60)
            .FirstOrDefaultAsync(cancellationToken);

    public Task LogCallAsync(ParallelWriteCallResult call, string connectionName, CancellationToken cancellationToken = default) =>
        _access.ExecuteSp<object>(_names.LogCall)
            .On(connectionName)
            .WithParameters(new
            {
                PairId = (int?)null,
                call.Operation,
                call.BusinessKey,
                call.CorrelationId,
                call.DboSucceeded,
                call.CoreSucceeded,
                call.CoreTimedOut,
                DboDurationMs = (int)call.DboDurationMs,
                CoreDurationMs = (int)call.CoreDurationMs,
                call.CoreError
            })
            .Named("WorkItemCallLog")
            .Timeout(15)
            .ExecuteAsync(cancellationToken);
}
