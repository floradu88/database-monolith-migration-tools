using System.Data;
using System.Diagnostics;
using BuildingBlocks.DataAccess.Abstractions;
using Dapper;
using Microsoft.Data.SqlClient;

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
}

public static class DataAccessContextExtensions
{
    /// <summary><c>factory.ExecuteSp&lt;T&gt;("dbo.Proc").WithParameters(...).ToListAsync()</c></summary>
    public static IFluentQuery<T> ExecuteSp<T>(this IDbConnectionFactory factory, string procedureName, IDataAccessTimingStore? timings = null) =>
        new DataAccessContext(factory, timings).ExecuteSp<T>(procedureName);

    /// <summary><c>factory.ExecuteSql&lt;T&gt;("SELECT ...").ToListAsync()</c></summary>
    public static IFluentQuery<T> ExecuteSql<T>(this IDbConnectionFactory factory, string sql, IDataAccessTimingStore? timings = null) =>
        new DataAccessContext(factory, timings).ExecuteSql<T>(sql);
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

    public IFluentQuery<T> Map(Func<T, T> mapper)
    {
        _mapper = mapper;
        return this;
    }

    private async Task<IDbConnection> OpenAsync(CancellationToken cancellationToken)
    {
        var conn = _factory.CreateConnection(_connectionName);
        if (conn is SqlConnection sql)
            await sql.OpenAsync(cancellationToken);
        else
            conn.Open();
        return conn;
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

    private async Task<T?> QueryFirstCoreAsync(CancellationToken cancellationToken)
    {
        var conn = await OpenAsync(cancellationToken);
        try
        {
            var row = await conn.QueryFirstOrDefaultAsync<T>(CreateCommand(cancellationToken));
            if (row is not null && _mapper is not null)
                row = _mapper(row);
            return row;
        }
        finally
        {
            conn.Dispose();
        }
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

    private CommandDefinition CreateCommand(CancellationToken cancellationToken) =>
        new(_commandText, _parameters, commandType: _commandType, cancellationToken: cancellationToken);

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
