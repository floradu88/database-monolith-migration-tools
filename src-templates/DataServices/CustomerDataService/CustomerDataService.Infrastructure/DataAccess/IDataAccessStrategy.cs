namespace CustomerDataService.Infrastructure.DataAccess;

public interface IDataAccessStrategy
{
    Task<CustomerSummary?> GetCustomerSummaryAsync(
        Guid customerId,
        CancellationToken cancellationToken);

    Task UpdateCustomerAsync(
        CustomerUpdate update,
        CancellationToken cancellationToken);
}
