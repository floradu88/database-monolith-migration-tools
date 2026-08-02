using System.Data;
using BuildingBlocks.DataAccess.Abstractions;
using Dapper;
using Microsoft.Data.SqlClient;

namespace BuildingBlocks.DataAccess.Dapper;

public sealed class SqlConnectionFactory : IDbConnectionFactory
{
    private readonly Func<string, string> _resolver;

    public SqlConnectionFactory(Func<string, string> connectionStringResolver)
    {
        _resolver = connectionStringResolver;
    }

    public IDbConnection CreateConnection(string name = "Owned")
    {
        var cs = _resolver(name);
        if (string.IsNullOrWhiteSpace(cs))
            throw new InvalidOperationException($"Connection string '{name}' is not configured.");
        return new SqlConnection(cs);
    }
}

public abstract class StoredProcedureExecutorBase
{
    private readonly IDbConnectionFactory _factory;

    protected StoredProcedureExecutorBase(IDbConnectionFactory factory) => _factory = factory;

    protected async Task<T?> QuerySingleSpAsync<T>(
        string connectionName,
        string procedureName,
        object? parameters,
        CancellationToken cancellationToken = default)
    {
        using var conn = _factory.CreateConnection(connectionName);
        if (conn is SqlConnection sql) await sql.OpenAsync(cancellationToken);
        else conn.Open();
        return await conn.QuerySingleOrDefaultAsync<T>(
            new CommandDefinition(procedureName, parameters, commandType: CommandType.StoredProcedure, cancellationToken: cancellationToken));
    }

    protected async Task<IReadOnlyList<T>> QuerySpAsync<T>(
        string connectionName,
        string procedureName,
        object? parameters,
        CancellationToken cancellationToken = default)
    {
        using var conn = _factory.CreateConnection(connectionName);
        if (conn is SqlConnection sql) await sql.OpenAsync(cancellationToken);
        else conn.Open();
        var rows = await conn.QueryAsync<T>(
            new CommandDefinition(procedureName, parameters, commandType: CommandType.StoredProcedure, cancellationToken: cancellationToken));
        return rows.ToList();
    }

    protected async Task<int> ExecuteSpAsync(
        string connectionName,
        string procedureName,
        object? parameters,
        CancellationToken cancellationToken = default)
    {
        using var conn = _factory.CreateConnection(connectionName);
        if (conn is SqlConnection sql) await sql.OpenAsync(cancellationToken);
        else conn.Open();
        return await conn.ExecuteAsync(
            new CommandDefinition(procedureName, parameters, commandType: CommandType.StoredProcedure, cancellationToken: cancellationToken));
    }
}
