namespace BuildingBlocks.DataAccess.Abstractions;

/// <summary>How a read/write was executed — used for A/B latency monitoring.</summary>
public enum DataAccessMethod
{
    EfCore = 0,
    StoredProcedure = 1,
    PlainSql = 2
}

public sealed class DataAccessTimingSample
{
    public required string Operation { get; init; }
    public required DataAccessMethod Method { get; init; }
    public required string ConnectionName { get; init; }
    public long ElapsedMs { get; init; }
    public int RowCount { get; init; }
    public bool Succeeded { get; init; } = true;
    public string? Error { get; init; }
    public DateTimeOffset RecordedAt { get; init; } = DateTimeOffset.UtcNow;
}

public interface IDataAccessTimingStore
{
    void Record(DataAccessTimingSample sample);
    IReadOnlyList<DataAccessTimingSample> Recent(int take = 100);
    IReadOnlyList<DataAccessMethodStats> Summarize(string? operation = null);
}

public sealed class DataAccessMethodStats
{
    public required string Operation { get; init; }
    public required DataAccessMethod Method { get; init; }
    public int Samples { get; init; }
    public double AvgMs { get; init; }
    public long MinMs { get; init; }
    public long MaxMs { get; init; }
    public long P95Ms { get; init; }
}

/// <summary>
/// Fluent query after ExecuteSp / ExecuteSql / ExecuteEf — map + materialize.
/// Example: <c>context.ExecuteSp&lt;Dto&gt;("dbo.Proc").WithParameters(...).Map(...).ToList()</c>
/// </summary>
public interface IFluentQuery<T>
{
    IFluentQuery<T> On(string connectionName);
    IFluentQuery<T> WithParameters(object? parameters);
    IFluentQuery<T> Named(string operationName);
    IFluentQuery<T> Timeout(int commandTimeoutSeconds);
    IFluentQuery<T> Map(Func<T, T> mapper);
    IFluentQuery<TResult> Map<TResult>(Func<T, TResult> mapper);

    List<T> ToList();
    Task<List<T>> ToListAsync(CancellationToken cancellationToken = default);
    T? FirstOrDefault();
    Task<T?> FirstOrDefaultAsync(CancellationToken cancellationToken = default);
    int Execute();
    Task<int> ExecuteAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Fluent data-access entry for SP + plain SQL (+ timing for EF via RecordTiming / Compare).
/// </summary>
public interface IDataAccessContext
{
    IFluentQuery<T> ExecuteSp<T>(string procedureName);
    /// <summary>Alias matching common call-site casing: ExecuteSP&lt;T&gt;().</summary>
    IFluentQuery<T> ExecuteSP<T>(string procedureName) => ExecuteSp<T>(procedureName);
    IFluentQuery<T> ExecuteSql<T>(string sql);

    void RecordTiming(
        string operation,
        DataAccessMethod method,
        string connectionName,
        long elapsedMs,
        int rowCount,
        bool succeeded = true,
        string? error = null);

    /// <summary>
    /// Run the same logical operation via SP, SQL, and an EF callback; record timings; return the fastest method.
    /// </summary>
    Task<DataAccessCompareResult<T>> CompareAsync<T>(
        string operationName,
        string connectionName,
        Func<CancellationToken, Task<T?>> efRead,
        Func<IDataAccessContext, CancellationToken, Task<T?>> spRead,
        Func<IDataAccessContext, CancellationToken, Task<T?>> sqlRead,
        CancellationToken cancellationToken = default);
}

public sealed class DataAccessCompareResult<T>
{
    public required string Operation { get; init; }
    public T? EfResult { get; init; }
    public T? SpResult { get; init; }
    public T? SqlResult { get; init; }
    public long EfMs { get; init; }
    public long SpMs { get; init; }
    public long SqlMs { get; init; }
    public required DataAccessMethod Fastest { get; init; }
    public bool PayloadsMatch { get; init; }
}
