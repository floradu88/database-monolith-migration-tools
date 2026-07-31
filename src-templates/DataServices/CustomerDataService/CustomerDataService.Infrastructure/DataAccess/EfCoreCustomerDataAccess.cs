using Microsoft.EntityFrameworkCore;

namespace CustomerDataService.Infrastructure.DataAccess;

public sealed class EfCoreCustomerDataAccess : IDataAccessStrategy
{
    private readonly CustomerDbContext _db;

    public EfCoreCustomerDataAccess(CustomerDbContext db) => _db = db;

    public Task<CustomerSummary?> GetCustomerSummaryAsync(
        Guid customerId,
        CancellationToken cancellationToken)
        => _db.Customers
            .AsNoTracking()
            .Where(x => x.Id == customerId)
            .Select(x => new CustomerSummary(x.Id, x.Name, x.Status))
            .SingleOrDefaultAsync(cancellationToken);

    public Task UpdateCustomerAsync(
        CustomerUpdate update,
        CancellationToken cancellationToken)
        => throw new NotImplementedException();
}
