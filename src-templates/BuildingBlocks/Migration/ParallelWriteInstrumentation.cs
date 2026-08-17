using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace BuildingBlocks.Migration;

/// <summary>
/// OpenTelemetry meter + activity source for dbo/core parallel writes.
/// Register <see cref="MeterName"/> and <see cref="ActivitySourceName"/> in the host.
/// Do not put parameter values on tags.
/// </summary>
public static class ParallelWriteInstrumentation
{
    public const string MeterName = "BuildingBlocks.Migration.ParallelWrite";
    public const string ActivitySourceName = "BuildingBlocks.Migration.ParallelWrite";

    public static readonly Meter Meter = new(MeterName, "1.0.0");
    public static readonly ActivitySource ActivitySource = new(ActivitySourceName);

    public static readonly Counter<long> Calls = Meter.CreateCounter<long>(
        "migration.parallel_write.calls",
        unit: "{call}",
        description: "Parallel dbo+core write attempts");

    public static readonly Counter<long> DboFailures = Meter.CreateCounter<long>(
        "migration.parallel_write.dbo_failures",
        unit: "{failure}",
        description: "dbo stored-procedure failures (caller-visible)");

    public static readonly Counter<long> CoreFailures = Meter.CreateCounter<long>(
        "migration.parallel_write.core_failures",
        unit: "{failure}",
        description: "core stored-procedure failures (evidence only)");

    public static readonly Counter<long> CoreTimeouts = Meter.CreateCounter<long>(
        "migration.parallel_write.core_timeouts",
        unit: "{timeout}",
        description: "core stored-procedure timeouts");

    public static readonly Counter<long> IntegrityChecks = Meter.CreateCounter<long>(
        "migration.parallel_write.integrity_checks",
        unit: "{check}",
        description: "Table integrity checks");

    public static readonly Counter<long> IntegrityMismatches = Meter.CreateCounter<long>(
        "migration.parallel_write.integrity_mismatches",
        unit: "{mismatch}",
        description: "Table integrity mismatches (evidence, not caller errors)");

    public static readonly Histogram<double> DboDurationMs = Meter.CreateHistogram<double>(
        "migration.parallel_write.dbo_duration",
        unit: "ms",
        description: "dbo stored-procedure duration");

    public static readonly Histogram<double> CoreDurationMs = Meter.CreateHistogram<double>(
        "migration.parallel_write.core_duration",
        unit: "ms",
        description: "core stored-procedure duration");

    public static readonly Histogram<double> IntegrityDurationMs = Meter.CreateHistogram<double>(
        "migration.parallel_write.integrity_duration",
        unit: "ms",
        description: "Table integrity check duration");
}
