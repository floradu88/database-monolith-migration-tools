using MigrationTool.Domain;

namespace MigrationTool.Application;

public interface IDacpacBuilder
{
    DacpacPrerequisitesReport GetPrerequisites();

    Task<DacpacOperationResult> BuildFromSqlProjectAsync(DacpacBuildRequest request, CancellationToken cancellationToken);

    Task<DacpacOperationResult> ExtractFromDatabaseAsync(DacpacExtractRequest request, CancellationToken cancellationToken);
}
