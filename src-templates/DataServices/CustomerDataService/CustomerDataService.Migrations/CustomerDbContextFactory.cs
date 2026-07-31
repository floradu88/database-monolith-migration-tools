using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace CustomerDataService.Migrations;

public sealed class CustomerDbContextFactory
    : IDesignTimeDbContextFactory<CustomerDbContext>
{
    public CustomerDbContext CreateDbContext(string[] args)
    {
        var connectionString =
            Environment.GetEnvironmentVariable("CUSTOMER_DB_CONNECTION")
            ?? throw new InvalidOperationException(
                "CUSTOMER_DB_CONNECTION is required.");

        var options = new DbContextOptionsBuilder<CustomerDbContext>()
            .UseSqlServer(
                connectionString,
                sql => sql.MigrationsHistoryTable(
                    "__EFMigrationsHistory",
                    "deployment"))
            .Options;

        return new CustomerDbContext(options);
    }
}
