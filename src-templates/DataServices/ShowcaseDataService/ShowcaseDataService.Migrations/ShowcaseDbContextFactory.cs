using BuildingBlocks.DataAccess.EfCore;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using ShowcaseDataService.Infrastructure;

namespace ShowcaseDataService.Migrations;

/// <summary>
/// Design-time factory. Prefer <c>Database</c> from appsettings / env:
/// <c>Database__Schema</c>, <c>Database__OwnedConnectionString</c>, or legacy <c>SHOWCASE_DB_*</c>.
/// </summary>
public sealed class ShowcaseDbContextFactory : IDesignTimeDbContextFactory<ShowcaseDbContext>
{
    public ShowcaseDbContext CreateDbContext(string[] args)
    {
        var configuration = BuildConfiguration();
        var database = ShowcaseDatabaseOptions.FromConfiguration(configuration);

        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SHOWCASE_DB_CONNECTION")))
            database.Owned.ConnectionString = Environment.GetEnvironmentVariable("SHOWCASE_DB_CONNECTION")!;
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SHOWCASE_DB_SCHEMA")))
            database.Schema = Environment.GetEnvironmentVariable("SHOWCASE_DB_SCHEMA")!;
        if (!string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable("SHOWCASE_DB_PROVIDER")))
            database.Owned.Provider = Environment.GetEnvironmentVariable("SHOWCASE_DB_PROVIDER")!;

        var ownedCs = database.ResolveOwnedConnectionString();
        if (string.IsNullOrWhiteSpace(ownedCs))
        {
            ownedCs =
                "Server=(localdb)\\mssqllocaldb;Database=ShowcaseOwned;Trusted_Connection=True;TrustServerCertificate=True";
            database.Owned.ConnectionString = ownedCs;
            database.Owned.Provider = nameof(BuildingBlocks.Security.SqlHostProvider.OnPrem);
        }

        var options = new DbContextOptionsBuilder<ShowcaseDbContext>()
            .UseShowcaseSqlServer(ownedCs)
            .Options;

        return new ShowcaseDbContext(options, Options.Create(database));
    }

    private static IConfiguration BuildConfiguration()
    {
        var basePath = FindApiContentRoot() ?? Directory.GetCurrentDirectory();
        return new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile("appsettings.Development.json", optional: true)
            .AddEnvironmentVariables()
            .Build();
    }

    private static string? FindApiContentRoot()
    {
        var dir = new DirectoryInfo(Directory.GetCurrentDirectory());
        while (dir is not null)
        {
            var api = Path.Combine(dir.FullName, "ShowcaseDataService.Api");
            if (Directory.Exists(api) && File.Exists(Path.Combine(api, "appsettings.json")))
                return api;

            var sibling = Path.Combine(dir.FullName, "..", "ShowcaseDataService.Api");
            var full = Path.GetFullPath(sibling);
            if (Directory.Exists(full) && File.Exists(Path.Combine(full, "appsettings.json")))
                return full;

            dir = dir.Parent;
        }

        return null;
    }
}
