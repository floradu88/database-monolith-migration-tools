namespace MigrationTool.Domain;

public static class FailureReport
{
    public static string Format(
        string scriptName,
        int attempt,
        DatabaseProviderKind provider,
        DateTimeOffset utc,
        string errorText)
    {
        return $"""
            script: {scriptName}
            attempt: {attempt}
            provider: {provider}
            utc: {utc:O}
            ---
            {errorText}
            """;
    }
}
