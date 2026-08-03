using BuildingBlocks.DataAccess.Abstractions;
using BuildingBlocks.DataAccess.Dapper;
using BuildingBlocks.DataAccess.EfCore;
using BuildingBlocks.Migration;
using BuildingBlocks.Security;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
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
    private readonly ShowcaseDatabaseOptions _database;
    private readonly string _summarySql;

    public ShowcaseDataAccess(
        ShowcaseDbContext db,
        IDataAccessContext access,
        IDataAccessTimingStore timings,
        IOptions<ShowcaseDatabaseOptions> database)
    {
        _db = db;
        _access = access;
        _timings = timings;
        _database = database.Value;
        _summarySql = $"""
            SELECT [Id], [Name], [Status], CAST('Owned-SQL' AS NVARCHAR(50)) AS [Source]
            FROM {_database.ItemsTable}
            WHERE [Id] = @Id
            """;
    }

    public Task<ShowcaseSummaryDto?> GetSummaryAsync(
        Guid id,
        string connectionName,
        DataAccessMethod method,
        CancellationToken cancellationToken = default)
    {
        if (!string.Equals(connectionName, "Owned", StringComparison.OrdinalIgnoreCase))
            return GetSummaryViaSpAsync(id, connectionName, cancellationToken);

        return method switch
        {
            DataAccessMethod.StoredProcedure => GetSummaryViaSpAsync(id, connectionName, cancellationToken),
            DataAccessMethod.PlainSql => GetSummaryViaSqlAsync(id, connectionName, cancellationToken),
            _ => GetSummaryViaEfAsync(id, cancellationToken)
        };
    }

    public Task<ShowcaseSummaryDto?> GetSummaryViaEfAsync(Guid id, CancellationToken cancellationToken = default) =>
        _db.ExecuteEf(db => db.Set<ShowcaseItem>().AsNoTracking().Where(x => x.Id == id), _timings, "Owned")
            .Named("GetShowcaseSummary")
            .Map(item => new ShowcaseSummaryDto(item.Id, item.Name, item.Status, "Owned-EF"))
            .FirstOrDefaultAsync(cancellationToken);

    public Task<ShowcaseSummaryDto?> GetSummaryViaSpAsync(Guid id, string connectionName, CancellationToken cancellationToken = default) =>
        _access.ExecuteSP<ShowcaseSummaryDto>(_database.GetShowcaseSummaryProcedure)
            .On(connectionName)
            .WithParameters(new { Id = id })
            .Named("GetShowcaseSummary")
            .Timeout(30)
            .Map(row => row with { Source = $"{connectionName}-SP" })
            .FirstOrDefaultAsync(cancellationToken);

    public Task<ShowcaseSummaryDto?> GetSummaryViaSqlAsync(Guid id, string connectionName, CancellationToken cancellationToken = default) =>
        _access.ExecuteSql<ShowcaseSummaryDto>(_summarySql)
            .On(connectionName)
            .WithParameters(new { Id = id })
            .Named("GetShowcaseSummary")
            .Timeout(30)
            .Map(row => row with { Source = $"{connectionName}-SQL" })
            .FirstOrDefaultAsync(cancellationToken);

    public Task<DataAccessCompareResult<ShowcaseSummaryDto>> CompareAccessMethodsAsync(Guid id, CancellationToken cancellationToken = default) =>
        _access.CompareAsync(
            "GetShowcaseSummary",
            "Owned",
            ct => GetSummaryViaEfAsync(id, ct),
            (ctx, ct) => GetSummaryViaSpAsync(id, "Owned", ct),
            (ctx, ct) => GetSummaryViaSqlAsync(id, "Owned", ct),
            cancellationToken);

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
        var database = ShowcaseDatabaseOptions.FromConfiguration(configuration);
        services.AddSingleton(Options.Create(database));

        services.Configure<MigrationRoutingOptions>(configuration.GetSection(MigrationRoutingOptions.SectionName));
        services.Configure<ShowcaseAuthOptions>(configuration.GetSection(ShowcaseAuthOptions.SectionName));
        services.Configure<ShowcaseSloOptions>(configuration.GetSection(ShowcaseSloOptions.SectionName));

        var ownedCs = database.ResolveOwnedConnectionString();
        var sourceCs = database.ResolveSourceFacadeConnectionString();

        // Bridge for any BuildingBlocks callers still bound to SqlConnections.
        services.Configure<SqlConnectionOptions>(opts =>
        {
            opts.OwnedConnectionString = ownedCs;
            opts.SourceFacadeConnectionString = sourceCs;
            opts.AllowDbOwner = database.AllowDbOwner;
        });

        SqlConnectionGuard.EnsureLeastPrivilege(new SqlConnectionOptions
        {
            OwnedConnectionString = ownedCs,
            SourceFacadeConnectionString = sourceCs,
            AllowDbOwner = database.AllowDbOwner
        });

        services.AddSingleton<IShadowCompareStore, InMemoryShadowCompareStore>();
        services.AddSingleton<IDataAccessTimingStore, InMemoryDataAccessTimingStore>();
        services.AddSingleton<IShowcaseSloCounter, InMemoryShowcaseSloCounter>();
        services.AddSingleton<IDbConnectionFactory>(_ => new SqlConnectionFactory(name =>
            name.Equals("Source", StringComparison.OrdinalIgnoreCase)
                ? sourceCs
                : ownedCs));
        services.AddScoped<IDataAccessContext>(sp =>
            new DataAccessContext(
                sp.GetRequiredService<IDbConnectionFactory>(),
                sp.GetRequiredService<IDataAccessTimingStore>()));

        if (string.IsNullOrWhiteSpace(ownedCs))
        {
            ownedCs =
                "Server=(localdb)\\mssqllocaldb;Database=ShowcaseOwned;Trusted_Connection=True;TrustServerCertificate=True";
        }

        services.AddDbContext<ShowcaseDbContext>((sp, options) =>
            options.UseShowcaseSqlServer(ownedCs));

        services.AddScoped<ISpGetShowcaseSummary, SpGetShowcaseSummary>();
        services.AddScoped<ISpGetShowcaseReport, SpGetShowcaseReport>();
        services.AddScoped<IShowcaseDataAccess, ShowcaseDataAccess>();
        services.AddScoped<IShowcaseItemService, ShowcaseItemService>();
        return services;
    }
}

public sealed class ShowcaseAuthOptions
{
    public const string SectionName = "Auth";
    /// <summary>When false (default for local demo), JWT is not required.</summary>
    public bool RequireJwt { get; set; }
    public string Authority { get; set; } = "";
    public string Audience { get; set; } = "showcase-dataservice";
    /// <summary>Placeholder for Managed Identity / Azure AD app id — do not invent production values.</summary>
    public string ManagedIdentityClientId { get; set; } = "";
}
