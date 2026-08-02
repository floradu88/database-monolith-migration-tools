using System.Data;
using System.Diagnostics;
using System.Text.Json;
using BuildingBlocks.DataAccess.Abstractions;
using Dapper;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.DependencyInjection;

namespace BuildingBlocks.DataAccess.Dapper;

public sealed class DataAccessContext : IDataAccessContext
{
    private readonly IDbConnectionFactory _factory;
    private readonly IDataAccessTimingStore? _timings;
    private readonly string _defaultConnection;

    public DataAccessContext(
        IDbConnectionFactory factory,
        IDataAccessTimingStore? timings = null,
        string defaultConnection = "Owned")
    {
        _factory = factory;
        _timings = timings;
        _defaultConnection = defaultConnection;
    }

    public IFluentQuery<T> ExecuteSp<T>(string procedureName) =>
        new FluentQuery<T>(_factory, _timings, _defaultConnection, procedureName, CommandType.StoredProcedure, DataAccessMethod.StoredProcedure);

    public IFluentQuery<T> ExecuteSP<T>(string procedureName) => ExecuteSp<T>(procedureName);

    public IFluentQuery<T> ExecuteSql<T>(string sql) =>
        new FluentQuery<T>(_factory, _timings, _defaultConnection, sql, CommandType.Text, DataAccessMethod.PlainSql);

    public void RecordTiming(
        string operation,
        DataAccessMethod method,
        string connectionName,
        long elapsedMs,
        int rowCount,
        bool succeeded = true,
        string? error = null)
    {
        _timings?.Record(new DataAccessTimingSample
        {
            Operation = operation,
            Method = method,
            ConnectionName = connectionName,
            ElapsedMs = elapsedMs,
            RowCount = rowCount,
            Succeeded = succeeded,
            Error = error
        });
    }

    public async Task<DataAccessCompareResult<T>> CompareAsync<T>(
        string operationName,
        string connectionName,
        Func<CancellationToken, Task<T?>> efRead,
        Func<IDataAccessContext, CancellationToken, Task<T?>> spRead,
        Func<IDataAccessContext, CancellationToken, Task<T?>> sqlRead,
        CancellationToken cancellationToken = default)
    {
        // Inner fluent/EF helpers already record timings; this method only aggregates wall-clock for the compare payload.
        var sw = Stopwatch.StartNew();
        var ef = await efRead(cancellationToken);
        var efMs = sw.ElapsedMilliseconds;

        sw.Restart();
        var sp = await spRead(this, cancellationToken);
        var spMs = sw.ElapsedMilliseconds;

        sw.Restart();
        var sql = await sqlRead(this, cancellationToken);
        var sqlMs = sw.ElapsedMilliseconds;

        var fastest = new[]
        {
            (DataAccessMethod.EfCore, efMs),
            (DataAccessMethod.StoredProcedure, spMs),
            (DataAccessMethod.PlainSql, sqlMs)
        }.OrderBy(x => x.Item2).First().Item1;

        var opts = new JsonSerializerOptions { WriteIndented = false };
        var match = string.Equals(JsonSerializer.Serialize(ef, opts), JsonSerializer.Serialize(sp, opts), StringComparison.Ordinal)
                    && string.Equals(JsonSerializer.Serialize(sp, opts), JsonSerializer.Serialize(sql, opts), StringComparison.Ordinal);

        return new DataAccessCompareResult<T>
        {
            Operation = operationName,
            EfResult = ef,
            SpResult = sp,
            SqlResult = sql,
            EfMs = efMs,
            SpMs = spMs,
            SqlMs = sqlMs,
            Fastest = fastest,
            PayloadsMatch = match
        };
    }
}

public static class DataAccessContextExtensions
{
    public static IFluentQuery<T> ExecuteSp<T>(this IDbConnectionFactory factory, string procedureName, IDataAccessTimingStore? timings = null) =>
        new DataAccessContext(factory, timings).ExecuteSp<T>(procedureName);

    public static IFluentQuery<T> ExecuteSP<T>(this IDbConnectionFactory factory, string procedureName, IDataAccessTimingStore? timings = null) =>
        factory.ExecuteSp<T>(procedureName, timings);

    public static IFluentQuery<T> ExecuteSql<T>(this IDbConnectionFactory factory, string sql, IDataAccessTimingStore? timings = null) =>
        new DataAccessContext(factory, timings).ExecuteSql<T>(sql);

    public static IServiceCollection AddFluentDataAccess(
        this IServiceCollection services,
        Func<IServiceProvider, Func<string, string>> connectionStringResolver,
        string defaultConnection = "Owned")
    {
        services.AddSingleton<IDataAccessTimingStore, InMemoryDataAccessTimingStore>();
        services.AddSingleton<IDbConnectionFactory>(sp =>
            new SqlConnectionFactory(connectionStringResolver(sp)));
        services.AddScoped<IDataAccessContext>(sp =>
            new DataAccessContext(
                sp.GetRequiredService<IDbConnectionFactory>(),
                sp.GetRequiredService<IDataAccessTimingStore>(),
                defaultConnection));
        return services;
    }
}

internal sealed class FluentQuery<T> : IFluentQuery<T>
{
    private readonly IDbConnectionFactory _factory;
    private readonly IDataAccessTimingStore? _timings;
    private readonly string _commandText;
    private readonly CommandType _commandType;
    private readonly DataAccessMethod _method;
    private string _connectionName;
    private object? _parameters;
    private string? _operationName;
    private Func<T, T>? _mapper;
    private int? _timeoutSeconds;

    public FluentQuery(
        IDbConnectionFactory factory,
        IDataAccessTimingStore? timings,
        string connectionName,
        string commandText,
        CommandType commandType,
        DataAccessMethod method)
    {
        _factory = factory;
        _timings = timings;
        _connectionName = connectionName;
        _commandText = commandText;
        _commandType = commandType;
        _method = method;
    }

    public IFluentQuery<T> On(string connectionName)
    {
        _connectionName = connectionName;
        return this;
    }

    public IFluentQuery<T> WithParameters(object? parameters)
    {
        _parameters = parameters;
        return this;
    }

    public IFluentQuery<T> Named(string operationName)
    {
        _operationName = operationName;
        return this;
    }

    public IFluentQuery<T> Timeout(int commandTimeoutSeconds)
    {
        _timeoutSeconds = commandTimeoutSeconds;
        return this;
    }

    public IFluentQuery<T> Map(Func<T, T> mapper)
    {
        _mapper = mapper;
        return this;
    }

    public IFluentQuery<TResult> Map<TResult>(Func<T, TResult> mapper) =>
        new MappedFluentQuery<T, TResult>(this, mapper);

    public List<T> ToList()
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var rows = QueryListCore();
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
            var rows = await QueryListCoreAsync(cancellationToken);
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
            var row = QueryFirstCore();
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
            var row = await QueryFirstCoreAsync(cancellationToken);
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

    public int Execute()
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var affected = ExecuteCore();
            sw.Stop();
            Record(sw.ElapsedMilliseconds, affected, true, null);
            return affected;
        }
        catch (Exception ex)
        {
            sw.Stop();
            Record(sw.ElapsedMilliseconds, 0, false, ex.Message);
            throw;
        }
    }

    public async Task<int> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        var sw = Stopwatch.StartNew();
        try
        {
            var affected = await ExecuteCoreAsync(cancellationToken);
            sw.Stop();
            Record(sw.ElapsedMilliseconds, affected, true, null);
            return affected;
        }
        catch (Exception ex)
        {
            sw.Stop();
            Record(sw.ElapsedMilliseconds, 0, false, ex.Message);
            throw;
        }
    }

    private CommandDefinition CreateCommand(CancellationToken cancellationToken = default) =>
        new(_commandText, _parameters, commandType: _commandType, commandTimeout: _timeoutSeconds, cancellationToken: cancellationToken);

    private IDbConnection Open()
    {
        var conn = _factory.CreateConnection(_connectionName);
        if (conn.State != ConnectionState.Open) conn.Open();
        return conn;
    }

    private async Task<IDbConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var conn = _factory.CreateConnection(_connectionName);
        if (conn is SqlConnection sql) await sql.OpenAsync(cancellationToken);
        else conn.Open();
        return conn;
    }

    private List<T> QueryListCore()
    {
        using var conn = Open();
        var rows = conn.Query<T>(CreateCommand()).ToList();
        return _mapper is null ? rows : rows.Select(_mapper).ToList();
    }

    private async Task<List<T>> QueryListCoreAsync(CancellationToken cancellationToken)
    {
        var conn = await OpenAsync(cancellationToken);
        try
        {
            var rows = (await conn.QueryAsync<T>(CreateCommand(cancellationToken))).ToList();
            return _mapper is null ? rows : rows.Select(_mapper).ToList();
        }
        finally
        {
            conn.Dispose();
        }
    }

    private T? QueryFirstCore()
    {
        using var conn = Open();
        var row = conn.QueryFirstOrDefault<T>(CreateCommand());
        if (row is not null && _mapper is not null) row = _mapper(row);
        return row;
    }

    private async Task<T?> QueryFirstCoreAsync(CancellationToken cancellationToken)
    {
        var conn = await OpenAsync(cancellationToken);
        try
        {
            var row = await conn.QueryFirstOrDefaultAsync<T>(CreateCommand(cancellationToken));
            if (row is not null && _mapper is not null) row = _mapper(row);
            return row;
        }
        finally
        {
            conn.Dispose();
        }
    }

    private int ExecuteCore()
    {
        using var conn = Open();
        return conn.Execute(CreateCommand());
    }

    private async Task<int> ExecuteCoreAsync(CancellationToken cancellationToken)
    {
        var conn = await OpenAsync(cancellationToken);
        try
        {
            return await conn.ExecuteAsync(CreateCommand(cancellationToken));
        }
        finally
        {
            conn.Dispose();
        }
    }

    private void Record(long ms, int rows, bool ok, string? error)
    {
        _timings?.Record(new DataAccessTimingSample
        {
            Operation = _operationName ?? _commandText,
            Method = _method,
            ConnectionName = _connectionName,
            ElapsedMs = ms,
            RowCount = rows,
            Succeeded = ok,
            Error = error
        });
    }
}

/// <summary>Projects fluent rows through a mapper while preserving ToList / FirstOrDefault.</summary>
internal sealed class MappedFluentQuery<TSource, TResult> : IFluentQuery<TResult>
{
    private readonly IFluentQuery<TSource> _inner;
    private readonly Func<TSource, TResult> _map;
    private Func<TResult, TResult>? _post;

    public MappedFluentQuery(IFluentQuery<TSource> inner, Func<TSource, TResult> map)
    {
        _inner = inner;
        _map = map;
    }

    public IFluentQuery<TResult> On(string connectionName)
    {
        _inner.On(connectionName);
        return this;
    }

    public IFluentQuery<TResult> WithParameters(object? parameters)
    {
        _inner.WithParameters(parameters);
        return this;
    }

    public IFluentQuery<TResult> Named(string operationName)
    {
        _inner.Named(operationName);
        return this;
    }

    public IFluentQuery<TResult> Timeout(int commandTimeoutSeconds)
    {
        _inner.Timeout(commandTimeoutSeconds);
        return this;
    }

    public IFluentQuery<TResult> Map(Func<TResult, TResult> mapper)
    {
        _post = mapper;
        return this;
    }

    public IFluentQuery<TNext> Map<TNext>(Func<TResult, TNext> mapper) =>
        new MappedFluentQuery<TResult, TNext>(this, mapper);

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

    public int Execute() => _inner.Execute();
    public Task<int> ExecuteAsync(CancellationToken cancellationToken = default) => _inner.ExecuteAsync(cancellationToken);
}
