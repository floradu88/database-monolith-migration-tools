namespace MigrationTool.Domain;

public sealed class MigrationOptions
{
    public const string SectionName = "Migration";

    public DatabaseProviderKind Provider { get; set; } = DatabaseProviderKind.SqlServer;

    public string ConnectionString { get; set; } = "";

    public string InboxPath { get; set; } = "scripts/inbox";

    public string SuccessPath { get; set; } = "scripts/success";

    public string FailedPath { get; set; } = "scripts/failed";

    public int MaxRetries { get; set; } = 4;

    public string PollCron { get; set; } = "*/1 * * * *";

    public string JournalSchema { get; set; } = "migration";

    public string JournalTable { get; set; } = "schema_versions";

    public int CommandTimeoutSeconds { get; set; } = 600;

    public bool RepairMissingColumns { get; set; } = true;
}
