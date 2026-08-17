using System.Diagnostics;
using System.Diagnostics.Metrics;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace BuildingBlocks.Migration;

public interface IParallelWriteExecutor
{
    Task<ParallelWriteCallResult> ExecuteAsync(
        string operation,
        string businessKey,
        Func<CancellationToken, Task> dboWrite,
        Func<CancellationToken, Task> coreWrite,
        int coreTimeoutMs,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Fan-out dbo + core stored procedures. dbo outcome is returned to the caller (exceptions propagate).
/// core timeout/failure is recorded as evidence and never thrown.
/// </summary>
public sealed class ParallelWriteExecutor : IParallelWriteExecutor
{
    private readonly IParallelWriteStore _store;
    private readonly ILogger _logger;

    public ParallelWriteExecutor(IParallelWriteStore store, ILogger<ParallelWriteExecutor>? logger = null)
    {
        _store = store;
        _logger = logger ?? (ILogger)NullLogger.Instance;
    }

    public async Task<ParallelWriteCallResult> ExecuteAsync(
        string operation,
        string businessKey,
        Func<CancellationToken, Task> dboWrite,
        Func<CancellationToken, Task> coreWrite,
        int coreTimeoutMs,
        CancellationToken cancellationToken = default)
    {
        var correlationId = Guid.NewGuid();
        var tags = new TagList
        {
            { "migration.operation", operation },
            { "db.system", "mssql" }
        };

        using var activity = ParallelWriteInstrumentation.ActivitySource.StartActivity(
            "migration.parallel_write",
            ActivityKind.Internal);
        activity?.SetTag("migration.operation", operation);
        activity?.SetTag("migration.wave", "dbo-core");
        activity?.SetTag("legacy.or.target", "parallel");
        activity?.SetTag("correlation.id", correlationId.ToString());

        ParallelWriteInstrumentation.Calls.Add(1, tags);

        using var coreCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        if (coreTimeoutMs > 0)
            coreCts.CancelAfter(coreTimeoutMs);

        var dboSw = Stopwatch.StartNew();
        var coreSw = Stopwatch.StartNew();
        var dboTask = Invoke(dboWrite, cancellationToken);
        var coreTask = Invoke(coreWrite, coreCts.Token);

        await Task.WhenAll(Observe(dboTask), Observe(coreTask)).ConfigureAwait(false);
        dboSw.Stop();
        coreSw.Stop();

        var dboFault = dboTask.IsFaulted ? dboTask.Exception?.GetBaseException() : null;
        var coreFault = coreTask.IsFaulted ? coreTask.Exception?.GetBaseException() : null;
        var coreCanceled = coreTask.IsCanceled || coreCts.IsCancellationRequested && !coreTask.IsCompletedSuccessfully;

        ParallelWriteInstrumentation.DboDurationMs.Record(dboSw.Elapsed.TotalMilliseconds, tags);
        ParallelWriteInstrumentation.CoreDurationMs.Record(coreSw.Elapsed.TotalMilliseconds, tags);

        if (dboFault is not null)
            ParallelWriteInstrumentation.DboFailures.Add(1, tags);
        if (coreFault is not null || coreCanceled)
            ParallelWriteInstrumentation.CoreFailures.Add(1, tags);
        if (coreCanceled && coreFault is null)
            ParallelWriteInstrumentation.CoreTimeouts.Add(1, tags);

        var result = new ParallelWriteCallResult
        {
            Operation = operation,
            BusinessKey = businessKey,
            CorrelationId = correlationId,
            DboSucceeded = dboFault is null && !dboTask.IsCanceled,
            CoreSucceeded = coreFault is null && !coreCanceled && coreTask.IsCompletedSuccessfully,
            CoreTimedOut = coreCanceled && coreFault is null,
            DboDurationMs = dboSw.ElapsedMilliseconds,
            CoreDurationMs = coreSw.ElapsedMilliseconds,
            CoreError = coreCanceled && coreFault is null
                ? $"core timeout after {coreTimeoutMs}ms"
                : coreFault?.Message
        };
        _store.AddCall(result);

        _logger.LogInformation(
            "Parallel write {Operation} key={BusinessKey} corr={CorrelationId} dboOk={DboOk} dboMs={DboMs} coreOk={CoreOk} coreMs={CoreMs} coreTimeout={CoreTimeout}",
            operation, businessKey, correlationId, result.DboSucceeded, result.DboDurationMs,
            result.CoreSucceeded, result.CoreDurationMs, result.CoreTimedOut);

        if (dboFault is not null)
        {
            activity?.SetStatus(ActivityStatusCode.Error, dboFault.Message);
            throw dboFault;
        }

        if (!result.CoreSucceeded)
        {
            _logger.LogWarning(
                "Core write evidence {Operation} key={BusinessKey} corr={CorrelationId} error={Error}",
                operation, businessKey, correlationId, result.CoreError);
            activity?.SetTag("migration.core_mismatch", true);
        }

        return result;
    }

    private static Task Invoke(Func<CancellationToken, Task> write, CancellationToken cancellationToken)
    {
        try
        {
            return write(cancellationToken);
        }
        catch (Exception ex)
        {
            return Task.FromException(ex);
        }
    }

    private static async Task Observe(Task task)
    {
        try
        {
            await task.ConfigureAwait(false);
        }
        catch
        {
            // Recorded by the caller; dbo exceptions rethrown after both sides settle.
        }
    }
}
