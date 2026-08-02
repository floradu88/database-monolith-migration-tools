using BuildingBlocks.DataAccess.EfCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using ShowcaseDataService.Infrastructure;

namespace ShowcaseDataService.Migrations;

public sealed class ShowcaseDbContextFactory : IDesignTimeDbContextFactory<ShowcaseDbContext>
{
    public ShowcaseDbContext CreateDbContext(string[] args)
    {
        var cs = Environment.GetEnvironmentVariable("SHOWCASE_DB_CONNECTION")
                 ?? "Server=(localdb)\\mssqllocaldb;Database=ShowcaseOwned;Trusted_Connection=True;TrustServerCertificate=True";
        var options = new DbContextOptionsBuilder<ShowcaseDbContext>()
            .UseShowcaseSqlServer(cs)
            .Options;
        return new ShowcaseDbContext(options);
    }
}
