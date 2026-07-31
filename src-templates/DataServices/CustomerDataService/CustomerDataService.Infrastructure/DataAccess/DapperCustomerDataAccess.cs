using System.Data;
using Dapper;

namespace CustomerDataService.Infrastructure.DataAccess;

public sealed class DapperCustomerDataAccess : IDataAccessStrategy
{
    private readonly IDbConnectionFactory _connections;

    public DapperCustomerDataAccess(IDbConnectionFactory connections)
        => _connections = connections;

    public async Task<CustomerSummary?> GetCustomerSummaryAsync(
        Guid customerId,
        CancellationToken cancellationToken)
    {
        await using var connection =
            await _connections.OpenAsync(cancellationToken);

        var command = new CommandDefinition(
            "customer.GetCustomerSummary",
            new { CustomerId = customerId },
            commandType: CommandType.StoredProcedure,
            cancellationToken: cancellationToken);

        return await connection.QuerySingleOrDefaultAsync<CustomerSummary>(
            command);
    }

    public Task UpdateCustomerAsync(
        CustomerUpdate update,
        CancellationToken cancellationToken)
        => throw new NotImplementedException();
}
