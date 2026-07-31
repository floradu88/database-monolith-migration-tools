using DbIntelligence.Infrastructure.Codegraph;
using DbIntelligence.Infrastructure.Graphify;
using DbIntelligence.RepositoryScanner;
using DbIntelligence.SqlScanner;
using Microsoft.Extensions.DependencyInjection;

namespace DbIntelligence.Infrastructure;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddDbIntelligence(this IServiceCollection services)
    {
        services.AddSingleton<CliProcessRunner>();
        services.AddSingleton<EvidenceGraphMerger>();
        services.AddSingleton<IIntelligenceStore, FileIntelligenceStore>();
        services.AddSingleton<ICodegraphClient, CodegraphClient>();
        services.AddSingleton<IGraphifyClient, GraphifyClient>();
        services.AddSingleton<IPrerequisiteHealthService, PrerequisiteHealthService>();
        services.AddSingleton<IPrerequisiteInstaller, PrerequisiteInstaller>();
        services.AddSingleton<RepositoryScannerService>();
        services.AddSingleton<SqlScannerService>();
        services.AddSingleton<IIndexingService, IndexingService>();
        return services;
    }
}
