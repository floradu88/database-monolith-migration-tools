using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using MigrationTool.Application;
using MigrationTool.Domain;
using Npgsql;

namespace MigrationTool.Infrastructure;

public sealed class ConnectionValidator : IConnectionValidator
{
    private readonly ILogger<ConnectionValidator> _logger;

    public ConnectionValidator(ILogger<ConnectionValidator> logger)
    {
        _logger = logger;
    }

    public async Task<ConnectionValidationResult> ValidateAsync(
        string? connectionString,
        DatabaseProviderKind provider,
        int commandTimeoutSeconds,
        CancellationToken cancellationToken)
    {
        var shape = ConnectionStringShape.Validate(connectionString, provider);
        if (!shape.IsValid)
        {
            foreach (var error in shape.Errors)
            {
                _logger.LogError("Connection string validation: {Error}", error);
            }

            return new ConnectionValidationResult(false, shape, null, string.Join("; ", shape.Errors));
        }

        foreach (var warning in shape.Warnings)
        {
            _logger.LogWarning("Connection string validation: {Warning}", warning);
        }

        _logger.LogInformation(
            "Validating {Provider} connection to Server={Server}, Database={Database}. Redacted: {Redacted}",
            provider,
            shape.Server,
            shape.Database,
            shape.RedactedConnectionString);

        try
        {
            var timeout = Math.Clamp(commandTimeoutSeconds, 5, 120);
            if (provider == DatabaseProviderKind.SqlServer)
            {
                await using var connection = new SqlConnection(connectionString);
                await connection.OpenAsync(cancellationToken);
                await using var command = connection.CreateCommand();
                command.CommandText = "SELECT @@VERSION;";
                command.CommandTimeout = timeout;
                var version = Convert.ToString(await command.ExecuteScalarAsync(cancellationToken));
                _logger.LogInformation("SQL Server connection OK. Version: {Version}", version);
                return new ConnectionValidationResult(true, shape, version, null);
            }

            await using var npgsql = new NpgsqlConnection(connectionString);
            await npgsql.OpenAsync(cancellationToken);
            await using var pgCommand = npgsql.CreateCommand();
            pgCommand.CommandText = "SELECT version();";
            pgCommand.CommandTimeout = timeout;
            var pgVersion = Convert.ToString(await pgCommand.ExecuteScalarAsync(cancellationToken));
            _logger.LogInformation("PostgreSQL connection OK. Version: {Version}", pgVersion);
            return new ConnectionValidationResult(true, shape, pgVersion, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database connection failed for redacted string {Redacted}", shape.RedactedConnectionString);
            return new ConnectionValidationResult(false, shape, null, ex.Message);
        }
    }
}
