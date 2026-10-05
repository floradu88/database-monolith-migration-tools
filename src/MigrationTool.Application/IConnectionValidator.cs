using MigrationTool.Domain;

namespace MigrationTool.Application;

public sealed record ConnectionValidationResult(
    bool Succeeded,
    ConnectionStringShapeResult Shape,
    string? ServerVersion,
    string? Error,
    DatabaseConnectionError? ConnectionError = null);

public interface IConnectionValidator
{
    Task<ConnectionValidationResult> ValidateAsync(
        string? connectionString,
        DatabaseProviderKind provider,
        int commandTimeoutSeconds,
        CancellationToken cancellationToken);
}
