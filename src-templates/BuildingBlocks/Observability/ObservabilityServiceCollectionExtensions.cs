using BuildingBlocks.Migration;
using Microsoft.Extensions.DependencyInjection;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace BuildingBlocks.Observability;

public static class ObservabilityServiceCollectionExtensions
{
    public static IServiceCollection AddShowcaseObservability(this IServiceCollection services, string serviceName)
    {
        services.AddOpenTelemetry()
            .ConfigureResource(r => r.AddService(serviceName))
            .WithTracing(t => t
                .AddSource(ParallelWriteInstrumentation.ActivitySourceName)
                .AddAspNetCoreInstrumentation()
                .AddHttpClientInstrumentation()
                .AddSqlClientInstrumentation()
                .AddConsoleExporter())
            .WithMetrics(m => m
                .AddMeter(ParallelWriteInstrumentation.MeterName)
                .AddAspNetCoreInstrumentation()
                .AddConsoleExporter());
        return services;
    }
}
