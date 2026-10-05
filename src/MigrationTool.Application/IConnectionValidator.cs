using MigrationTool.Domain;

namespace MigrationTool.Application;

public sealed record ConnectionValidationResult(
    bool Succeeded,
    ConnectionStringShapeResult Shape,
    string? ServerVersion,
    string? Error,
    DatabaseConnectionError? ConnectionError = null,
    NetworkReachabilityReport? Network = null);

public interface INetworkProbe
{
    Task<NetworkReachabilityReport> ProbeAsync(
        DatabaseEndpoint endpoint,
        int timeoutSeconds,
        CancellationToken cancellationToken);
}

public interface IConnectionValidator
{
    Task<ConnectionValidationResult> ValidateAsync(
        string? connectionString,
        DatabaseProviderKind provider,
        int commandTimeoutSeconds,
        CancellationToken cancellationToken,
        bool openDatabase = true);

    Task<ConnectionValidationResult> CheckNetworkAsync(
        string? connectionString,
        DatabaseProviderKind provider,
        int timeoutSeconds,
        CancellationToken cancellationToken);
}
