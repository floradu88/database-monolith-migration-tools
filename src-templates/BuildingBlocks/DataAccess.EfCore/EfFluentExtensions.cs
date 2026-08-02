using System.Diagnostics;
using BuildingBlocks.DataAccess.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace BuildingBlocks.DataAccess.EfCore;

/// <summary>
/// Fluent EF Core helpers that record the same timing store as ExecuteSp / ExecuteSql.
/// Example: <c>await db.ExecuteEf&lt;Dto&gt;(q =&gt; q.Items.AsNoTracking()...).Named("op").ToListAsync()</c>
/// </summary>
public static class EfFluentExtensions
{
    public static IFluentEfQuery<T> ExecuteEf<T>(
        this DbContext db,
        Func<DbContext, IQueryable<T>> query,
        IDataAccessTimingStore? timings = null,
        string connectionName = "Owned") =>
        new FluentEfQuery<T>(db, query, timings, connectionName);

    public static IFluentEfQuery<T> ExecuteQuery<T>(
        this DbContext db,
        Func<DbContext, IQueryable<T>> query,
        IDataAccessTimingStore? timings = null,
        string connectionName = "Owned") =>
        db.ExecuteEf(query, timings, connectionName);
}

public interface IFluentEfQuery<T>
{
    IFluentEfQuery<T> Named(string operationName);
    IFluentEfQuery<T> On(string connectionName);
    IFluentEfQuery<T> Map(Func<T, T> mapper);
    IFluentEfQuery<TResult> Map<TResult>(Func<T, TResult> mapper);
    List<T> ToList();
    Task<List<T>> ToListAsync(CancellationToken cancellationToken = default);
    T? FirstOrDefault();
    Task<T?> FirstOrDefaultAsync(CancellationToken cancellationToken = default);
}

internal sealed class FluentEfQuery<T> : IFluentEfQuery<T>
{
    private readonly DbContext _db;
    private readonly Func<DbContext, IQueryable<T>> _query;
    private readonly IDataAccessTimingStore? _timings;
    private string _connectionName;
    private string? _operationName;
    private Func<T, T>? _mapper;

    public FluentEfQuery(
        DbContext db,
        Func<DbContext, IQueryable<T>> query,
        IDataAccessTimingStore? timings,
        string connectionName)
    {
        _db = db;
        _query = query;
        _timings = timings;
        _connectionName = connectionName;
    }

    public IFluentEfQuery<T> Named(string operationName)
    {
        _operationName = operationName;
        return this;
    }

    public IFluentEfQuery<T> On(string connectionName)
    {
        _connectionName = connectionName;
        return this;
    }

    public IFluentEfQuery<T> Map(Func<T, T> mapper)
    {
        _mapper = mapper;
        return this;
    }

    public IFluentEfQuery<TResult> Map<TResult>(Func<T, TResult> mapper) =>
        new MappedFluentEfQuery<T, TResult>(this, mapper);

    public List<T> ToList()
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var rows = _query(_db).ToList();
            if (_mapper is not null) rows = rows.Select(_mapper).ToList();
            sw.Stop();
            Record(sw.ElapsedMilliseconds, rows.Count, true, null);
            return rows;
        }
        catch (Exception ex)
        {
            sw.Stop();
            Record(sw.ElapsedMilliseconds, 0, false, ex.Message);
            throw;
        }
    }

    public async Task<List<T>> ToListAsync(CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var rows = await _query(_db).ToListAsync(cancellationToken);
            if (_mapper is not null) rows = rows.Select(_mapper).ToList();
            sw.Stop();
            Record(sw.ElapsedMilliseconds, rows.Count, true, null);
            return rows;
        }
        catch (Exception ex)
        {
            sw.Stop();
            Record(sw.ElapsedMilliseconds, 0, false, ex.Message);
            throw;
        }
    }

    public T? FirstOrDefault()
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var row = _query(_db).FirstOrDefault();
            if (row is not null && _mapper is not null) row = _mapper(row);
            sw.Stop();
            Record(sw.ElapsedMilliseconds, row is null ? 0 : 1, true, null);
            return row;
        }
        catch (Exception ex)
        {
            sw.Stop();
            Record(sw.ElapsedMilliseconds, 0, false, ex.Message);
            throw;
        }
    }

    public async Task<T?> FirstOrDefaultAsync(CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var row = await _query(_db).FirstOrDefaultAsync(cancellationToken);
            if (row is not null && _mapper is not null) row = _mapper(row);
            sw.Stop();
            Record(sw.ElapsedMilliseconds, row is null ? 0 : 1, true, null);
            return row;
        }
        catch (Exception ex)
        {
            sw.Stop();
            Record(sw.ElapsedMilliseconds, 0, false, ex.Message);
            throw;
        }
    }

    private void Record(long ms, int rows, bool ok, string? error)
    {
        _timings?.Record(new DataAccessTimingSample
        {
            Operation = _operationName ?? "EfQuery",
            Method = DataAccessMethod.EfCore,
            ConnectionName = _connectionName,
            ElapsedMs = ms,
            RowCount = rows,
            Succeeded = ok,
            Error = error
        });
    }
}

internal sealed class MappedFluentEfQuery<TSource, TResult> : IFluentEfQuery<TResult>
{
    private readonly IFluentEfQuery<TSource> _inner;
    private readonly Func<TSource, TResult> _map;
    private Func<TResult, TResult>? _post;

    public MappedFluentEfQuery(IFluentEfQuery<TSource> inner, Func<TSource, TResult> map)
    {
        _inner = inner;
        _map = map;
    }

    public IFluentEfQuery<TResult> Named(string operationName)
    {
        _inner.Named(operationName);
        return this;
    }

    public IFluentEfQuery<TResult> On(string connectionName)
    {
        _inner.On(connectionName);
        return this;
    }

    public IFluentEfQuery<TResult> Map(Func<TResult, TResult> mapper)
    {
        _post = mapper;
        return this;
    }

    public IFluentEfQuery<TNext> Map<TNext>(Func<TResult, TNext> mapper) =>
        new MappedFluentEfQuery<TResult, TNext>(this, mapper);

    private TResult Project(TSource source)
    {
        var result = _map(source);
        return _post is null ? result : _post(result);
    }

    public List<TResult> ToList() => _inner.ToList().Select(Project).ToList();
    public async Task<List<TResult>> ToListAsync(CancellationToken cancellationToken = default) =>
        (await _inner.ToListAsync(cancellationToken)).Select(Project).ToList();

    public TResult? FirstOrDefault()
    {
        var row = _inner.FirstOrDefault();
        return row is null ? default : Project(row);
    }

    public async Task<TResult?> FirstOrDefaultAsync(CancellationToken cancellationToken = default)
    {
        var row = await _inner.FirstOrDefaultAsync(cancellationToken);
        return row is null ? default : Project(row);
    }
}
