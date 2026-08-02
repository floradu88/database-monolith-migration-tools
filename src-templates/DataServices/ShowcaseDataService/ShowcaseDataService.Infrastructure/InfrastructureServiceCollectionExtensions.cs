using System.Diagnostics;
using BuildingBlocks.DataAccess.Abstractions;
using BuildingBlocks.DataAccess.Dapper;
using BuildingBlocks.DataAccess.EfCore;
using BuildingBlocks.Migration;
using BuildingBlocks.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using ShowcaseDataService.Application;
using ShowcaseDataService.Contracts;
using ShowcaseDataService.Domain;
using ShowcaseDataService.Infrastructure.StoredProcedures;

namespace ShowcaseDataService.Infrastructure;

public sealed class ShowcaseDataAccess : IShowcaseDataAccess
{
    private readonly ShowcaseDbContext _db;
    private readonly IDataAccessContext _access;
    private readonly IDataAccessTimingStore _timings;

    private const string SummarySql = """
        SELECT [Id], [Name], [Status], CAST('Owned-SQL' AS NVARCHAR(50)) AS [Source]
        FROM [showcase].[Items]
        WHERE [Id] = @Id
        """;

    public ShowcaseDataAccess(ShowcaseDbContext db, IDataAccessContext access, IDataAccessTimingStore timings)
    {
        _db = db;
        _access = access;
        _timings = timings;
    }

    public async Task<ShowcaseSummaryDto?> GetSummaryAsync(Guid id, string connectionName, CancellationToken cancellationToken = default)
    {
        if (string.Equals(connectionName, "Owned", StringComparison.OrdinalIgnoreCase))
            return await GetSummaryViaEfAsync(id, cancellationToken);

        return await GetSummaryViaSpAsync(id, connectionName, cancellationToken);
    }

    public async Task<ShowcaseSummaryDto?> GetSummaryViaEfAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var item = await _db.Items.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id, cancellationToken);
            sw.Stop();
            _access.RecordTiming("GetShowcaseSummary", DataAccessMethod.EfCore, "Owned", sw.ElapsedMilliseconds, item is null ? 0 : 1);
            return item is null
                ? null
                : new ShowcaseSummaryDto(item.Id, item.Name, item.Status, "Owned-EF");
        }
        catch (Exception ex)
        {
            sw.Stop();
            _access.RecordTiming("GetShowcaseSummary", DataAccessMethod.EfCore, "Owned", sw.ElapsedMilliseconds, 0, false, ex.Message);
            throw;
        }
    }

    public Task<ShowcaseSummaryDto?> GetSummaryViaSpAsync(Guid id, string connectionName, CancellationToken cancellationToken = default) =>
        _access.ExecuteSp<ShowcaseSummaryDto>(SpGetShowcaseSummary.ProcedureName)
            .On(connectionName)
            .WithParameters(new { Id = id })
            .Named("GetShowcaseSummary")
            .Map(row => row with { Source = $"{connectionName}-SP" })
            .FirstOrDefaultAsync(cancellationToken);

    public Task<ShowcaseSummaryDto?> GetSummaryViaSqlAsync(Guid id, string connectionName, CancellationToken cancellationToken = default) =>
        _access.ExecuteSql<ShowcaseSummaryDto>(SummarySql)
            .On(connectionName)
            .WithParameters(new { Id = id })
            .Named("GetShowcaseSummary")
            .Map(row => row with { Source = $"{connectionName}-SQL" })
            .FirstOrDefaultAsync(cancellationToken);

    public async Task UpdateAsync(ShowcaseUpdateRequest request, string connectionName, CancellationToken cancellationToken = default)
    {
        if (!string.Equals(connectionName, "Owned", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("Updates in SourceFacade mode must be performed on the monolith until cutover; demo template only persists Owned writes.");

        var item = await _db.Items.FirstOrDefaultAsync(x => x.Id == request.Id, cancellationToken);
        if (item is null)
        {
            _db.Items.Add(new ShowcaseItem
            {
                Id = request.Id,
                Name = request.Name,
                Status = request.Status,
                UpdatedAt = DateTimeOffset.UtcNow
            });
        }
        else
        {
            item.Name = request.Name;
            item.Status = request.Status;
            item.UpdatedAt = DateTimeOffset.UtcNow;
        }

        await _db.SaveChangesAsync(cancellationToken);
    }
}

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddShowcaseInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<SqlConnectionOptions>(configuration.GetSection(SqlConnectionOptions.SectionName));
        services.Configure<MigrationRoutingOptions>(configuration.GetSection(MigrationRoutingOptions.SectionName));

        var sql = configuration.GetSection(SqlConnectionOptions.SectionName).Get<SqlConnectionOptions>() ?? new SqlConnectionOptions();
        SqlConnectionGuard.EnsureLeastPrivilege(sql);

        services.AddSingleton<IShadowCompareStore, InMemoryShadowCompareStore>();
        services.AddSingleton<IDataAccessTimingStore, InMemoryDataAccessTimingStore>();
        services.AddSingleton<IDbConnectionFactory>(_ => new SqlConnectionFactory(name =>
            name.Equals("Source", StringComparison.OrdinalIgnoreCase)
                ? sql.SourceFacadeConnectionString
                : sql.OwnedConnectionString));
        services.AddScoped<IDataAccessContext>(sp =>
            new DataAccessContext(
                sp.GetRequiredService<IDbConnectionFactory>(),
                sp.GetRequiredService<IDataAccessTimingStore>()));

        services.AddDbContext<ShowcaseDbContext>(options =>
            options.UseShowcaseSqlServer(
                string.IsNullOrWhiteSpace(sql.OwnedConnectionString)
                    ? "Server=(localdb)\\mssqllocaldb;Database=ShowcaseOwned;Trusted_Connection=True;TrustServerCertificate=True"
                    : sql.OwnedConnectionString));

        services.AddScoped<ISpGetShowcaseSummary, SpGetShowcaseSummary>();
        services.AddScoped<IShowcaseDataAccess, ShowcaseDataAccess>();
        services.AddScoped<IShowcaseItemService, ShowcaseItemService>();
        return services;
    }
}
