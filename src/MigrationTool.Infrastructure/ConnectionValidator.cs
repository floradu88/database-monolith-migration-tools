using System.Data.Common;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Logging;
using MigrationTool.Application;
using MigrationTool.Domain;
using Npgsql;

namespace MigrationTool.Infrastructure;

public sealed class ConnectionValidator : IConnectionValidator
{
    private readonly INetworkProbe _network;
    private readonly ILogger<ConnectionValidator> _logger;

    public ConnectionValidator(INetworkProbe network, ILogger<ConnectionValidator> logger)
    {
        _network = network;
        _logger = logger;
    }

    public Task<ConnectionValidationResult> CheckNetworkAsync(
        string? connectionString,
        DatabaseProviderKind provider,
        int timeoutSeconds,
        CancellationToken cancellationToken) =>
        ValidateAsync(connectionString, provider, timeoutSeconds, cancellationToken, openDatabase: false);

    public async Task<ConnectionValidationResult> ValidateAsync(
        string? connectionString,
        DatabaseProviderKind provider,
        int commandTimeoutSeconds,
        CancellationToken cancellationToken,
        bool openDatabase = true)
    {
        var shape = ConnectionStringShape.Validate(connectionString, provider);
        if (!shape.IsValid)
        {
            var shapeError = DatabaseConnectionErrorClassifier.InvalidShape(string.Join(" ", shape.Errors));
            foreach (var error in shape.Errors)
            {
                _logger.LogError("Connection string validation: {Error}", error);
            }

            return new ConnectionValidationResult(false, shape, null, shapeError.Format(), shapeError);
        }

        foreach (var warning in shape.Warnings)
        {
            _logger.LogWarning("Connection string validation: {Warning}", warning);
        }

        var endpoint = ResolveEndpoint(connectionString, provider, shape.Server);
        NetworkReachabilityReport? network = null;
        if (endpoint is null)
        {
            _logger.LogWarning("Could not parse host/port from connection string for network checks.");
        }
        else
        {
            network = await _network.ProbeAsync(endpoint, commandTimeoutSeconds, cancellationToken);
            if (!network.IsReachableEnough)
            {
                DatabaseConnectionError netError;
                if (!network.DnsSucceeded)
                {
                    netError = new DatabaseConnectionError(
                        DatabaseConnectionErrorKind.HostNotFound,
                        $"DNS lookup failed for {endpoint.Host}.",
                        "Check the hostname spelling and that public DNS can resolve the AWS RDS endpoint.",
                        ProviderMessage: network.Format());
                }
                else
                {
                    netError = new DatabaseConnectionError(
                        DatabaseConnectionErrorKind.NetworkUnreachable,
                        $"TCP {endpoint.Host}:{endpoint.Port} is not reachable.",
                        $"Allow inbound TCP {endpoint.Port} from this client (AWS security group / NACL / VPN). ICMP ping is optional and often blocked.",
                        ProviderMessage: network.Format());
                }

                _logger.LogError("{Detail}", netError.Format());
                return new ConnectionValidationResult(false, shape, null, netError.Format(), netError, network);
            }
        }

        if (!openDatabase)
        {
            _logger.LogInformation(
                "Network check succeeded for {Host}:{Port}. Skipping database login (openDatabase=false).",
                endpoint?.Host,
                endpoint?.Port);
            return new ConnectionValidationResult(true, shape, null, null, null, network);
        }

        _logger.LogInformation(
            "Validating {Provider} login to Server={Server}, Database={Database}. Redacted: {Redacted}",
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
                command.CommandText = TargetDatabaseProbe.SqlServerCurrentDatabaseSql;
                var actualDatabase = Convert.ToString(await command.ExecuteScalarAsync(cancellationToken));
                var mismatch = WrongDatabaseResult(shape, actualDatabase, version, network);
                if (mismatch is not null)
                {
                    return mismatch;
                }

                _logger.LogInformation(
                    "SQL Server connection OK. Database={Database} Version={Version}",
                    actualDatabase,
                    version);
                return new ConnectionValidationResult(true, shape, version, null, null, network);
            }

            await using var npgsql = new NpgsqlConnection(connectionString);
            await npgsql.OpenAsync(cancellationToken);
            await using var pgCommand = npgsql.CreateCommand();
            pgCommand.CommandText = "SELECT version();";
            pgCommand.CommandTimeout = timeout;
            var pgVersion = Convert.ToString(await pgCommand.ExecuteScalarAsync(cancellationToken));
            pgCommand.CommandText = TargetDatabaseProbe.PostgreSqlCurrentDatabaseSql;
            var pgDatabase = Convert.ToString(await pgCommand.ExecuteScalarAsync(cancellationToken));
            var pgMismatch = WrongDatabaseResult(shape, pgDatabase, pgVersion, network);
            if (pgMismatch is not null)
            {
                return pgMismatch;
            }

            _logger.LogInformation(
                "PostgreSQL connection OK. Database={Database} Version={Version}",
                pgDatabase,
                pgVersion);
            return new ConnectionValidationResult(true, shape, pgVersion, null, null, network);
        }
        catch (Exception ex)
        {
            var mapped = DatabaseConnectionExceptionMapper.Map(ex, provider);
            _logger.LogError(
                ex,
                "Database connection failed ({Kind}) for redacted string {Redacted}. {Guidance}",
                mapped.Kind,
                shape.RedactedConnectionString,
                mapped.Guidance);
            return new ConnectionValidationResult(false, shape, null, mapped.Format(), mapped, network);
        }
    }

    private ConnectionValidationResult? WrongDatabaseResult(
        ConnectionStringShapeResult shape,
        string? actualDatabase,
        string? version,
        NetworkReachabilityReport? network)
    {
        if (TargetDatabase.Matches(shape.Database, actualDatabase))
        {
            return null;
        }

        var error = DatabaseConnectionErrorClassifier.WrongDatabase(
            shape.Database ?? "(missing)",
            string.IsNullOrWhiteSpace(actualDatabase) ? "(none)" : actualDatabase);
        _logger.LogError("{Detail}", error.Format());
        return new ConnectionValidationResult(false, shape, version, error.Format(), error, network);
    }

    private static DatabaseEndpoint? ResolveEndpoint(
        string? connectionString,
        DatabaseProviderKind provider,
        string? server)
    {
        int? explicitPort = null;
        try
        {
            if (!string.IsNullOrWhiteSpace(connectionString))
            {
                var builder = new DbConnectionStringBuilder { ConnectionString = connectionString };
                foreach (string key in builder.Keys)
                {
                    if (string.Equals(key, "Port", StringComparison.OrdinalIgnoreCase)
                        && int.TryParse(Convert.ToString(builder[key]), out var port))
                    {
                        explicitPort = port;
                        break;
                    }
                }
            }
        }
        catch
        {
            // Fall back to server parsing only.
        }

        return DatabaseEndpointParser.TryParse(server, provider, explicitPort);
    }
}
