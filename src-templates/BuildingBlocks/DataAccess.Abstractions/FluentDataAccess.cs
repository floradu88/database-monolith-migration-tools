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

/// <summary>Fluent query after ExecuteSp / ExecuteSql — map + materialize.</summary>
public interface IFluentQuery<T>
{
    IFluentQuery<T> On(string connectionName);
    IFluentQuery<T> WithParameters(object? parameters);
    IFluentQuery<T> Named(string operationName);
    IFluentQuery<T> Map(Func<T, T> mapper);
    Task<List<T>> ToListAsync(CancellationToken cancellationToken = default);
    Task<T?> FirstOrDefaultAsync(CancellationToken cancellationToken = default);
    Task<int> ExecuteAsync(CancellationToken cancellationToken = default);
}

/// <summary>
/// Fluent data-access entry: <c>context.ExecuteSp&lt;T&gt;("dbo.Proc").WithParameters(...).ToListAsync()</c>
/// and <c>context.ExecuteSql&lt;T&gt;("SELECT ...").ToListAsync()</c>.
/// </summary>
public interface IDataAccessContext
{
    IFluentQuery<T> ExecuteSp<T>(string procedureName);
    IFluentQuery<T> ExecuteSql<T>(string sql);
    /// <summary>Record an EF (or other) timing sample from outside the fluent pipeline.</summary>
    void RecordTiming(string operation, DataAccessMethod method, string connectionName, long elapsedMs, int rowCount, bool succeeded = true, string? error = null);
}
