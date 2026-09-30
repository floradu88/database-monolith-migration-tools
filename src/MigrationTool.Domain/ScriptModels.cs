namespace MigrationTool.Domain;

public sealed record InboxScript(string FullPath, string RelativePath, string FileName, string Contents);

public sealed record ChangeScript(string JournalName, IReadOnlyList<string> Batches, int CommandTimeoutSeconds);

public sealed record QueryScriptRequest(string Name, string Sql, int CommandTimeoutSeconds);

public sealed record ScriptRunResult(
    bool Success,
    bool AlreadyApplied,
    string? ErrorText,
    int? ErrorNumber,
    string? SqlState,
    string? ResultText)
{
    public static ScriptRunResult Ok(string? resultText = null) =>
        new(true, false, null, null, null, resultText);

    public static ScriptRunResult Applied() =>
        new(true, true, null, null, null, null);

    public static ScriptRunResult Fail(string error, int? number = null, string? sqlState = null) =>
        new(false, false, error, number, sqlState, null);
}

public sealed record MissingColumnFault(string Column, string? Table, string? Schema);

public sealed record InferredColumnType(string SqlType, string Reason);
