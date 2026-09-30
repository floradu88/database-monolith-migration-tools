using MigrationTool.Domain;

namespace MigrationTool.Application;

public interface IScriptStore
{
    IReadOnlyList<InboxScript> ListInbox();

    void SaveContents(string fullPath, string contents);

    void MoveToSuccess(InboxScript script);

    IReadOnlyList<string> ListFailedSqlFileNames();

    void WriteFailure(string fileName, string contents, int attempt, string errorText, string? diagnostics);

    void RemoveFromInbox(InboxScript script);

    void WriteQueryResult(string fileName, string resultText, DateTimeOffset utc);

    void WritePreStep(string fileName, string sql, string? diagnostics);
}

public interface IChangeScriptRunner
{
    Task<ScriptRunResult> ExecuteAsync(ChangeScript script, CancellationToken cancellationToken);
}

public interface IQueryScriptRunner
{
    Task<ScriptRunResult> ExecuteAsync(QueryScriptRequest script, CancellationToken cancellationToken);
}

public sealed record PollSummary(int Processed, int Succeeded, int Failed, int Repaired, bool ConnectionSkipped);

public interface IScriptPoller
{
    Task<PollSummary> PollAsync(CancellationToken cancellationToken);
}
