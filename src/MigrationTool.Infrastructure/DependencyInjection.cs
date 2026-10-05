using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using MigrationTool.Application;
using MigrationTool.Domain;

namespace MigrationTool.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddMigrationTool(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddOptions<MigrationOptions>()
            .Bind(configuration.GetSection(MigrationOptions.SectionName))
            .PostConfigure(ApplyConnectionStringOverride);

        services.AddSingleton<IScriptStore, FileScriptStore>();
        services.AddSingleton<IChangeScriptRunner, DbUpChangeScriptRunner>();
        services.AddSingleton<IQueryScriptRunner, AdoQueryScriptRunner>();
        services.AddSingleton<IScriptPoller, ScriptPoller>();
        services.AddSingleton<IDacpacBuilder, DacpacBuilder>();
        services.AddSingleton<INetworkProbe, NetworkProbe>();
        services.AddSingleton<IConnectionValidator, ConnectionValidator>();
        return services;
    }

    private static void ApplyConnectionStringOverride(MigrationOptions options)
    {
        var fromEnvironment = Environment.GetEnvironmentVariable("MIGRATION_CONNECTION_STRING");
        if (!string.IsNullOrWhiteSpace(fromEnvironment))
        {
            options.ConnectionString = fromEnvironment;
        }
    }
}
