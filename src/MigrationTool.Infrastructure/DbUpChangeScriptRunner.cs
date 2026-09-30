using DbUp;
using DbUp.Engine;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MigrationTool.Application;
using MigrationTool.Domain;

namespace MigrationTool.Infrastructure;

public sealed class DbUpChangeScriptRunner : IChangeScriptRunner
{
    private readonly MigrationOptions _options;
    private readonly ILogger<DbUpChangeScriptRunner> _logger;

    public DbUpChangeScriptRunner(IOptions<MigrationOptions> options, ILogger<DbUpChangeScriptRunner> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public Task<ScriptRunResult> ExecuteAsync(ChangeScript script, CancellationToken cancellationToken)
    {
        var batches = script.Batches.Where(batch => !string.IsNullOrWhiteSpace(batch)).ToList();
        if (batches.Count == 0)
        {
            return Task.FromResult(ScriptRunResult.Ok());
        }

        return Task.Run(() => Upgrade(script.JournalName, batches, script.CommandTimeoutSeconds), cancellationToken);
    }

    private ScriptRunResult Upgrade(string journalName, IReadOnlyList<string> batches, int commandTimeoutSeconds)
    {
        var scripts = batches
            .Select((sql, index) => new SqlScript(BatchName(journalName, index, batches.Count), sql))
            .ToArray();
        var timeout = commandTimeoutSeconds < 0 ? (TimeSpan?)null : TimeSpan.FromSeconds(commandTimeoutSeconds);

        try
        {
            var builder = _options.Provider == DatabaseProviderKind.PostgreSql
                ? DeployChanges.To
                    .PostgresqlDatabase(_options.ConnectionString)
                    .WithScripts(scripts)
                    .WithTransaction()
                    .JournalToPostgresqlTable(_options.JournalSchema, _options.JournalTable)
                    .WithExecutionTimeout(timeout)
                    .LogTo(_logger)
                : DeployChanges.To
                    .SqlDatabase(_options.ConnectionString)
                    .WithScripts(scripts)
                    .WithTransaction()
                    .JournalToSqlTable(_options.JournalSchema, _options.JournalTable)
                    .WithExecutionTimeout(timeout)
                    .LogTo(_logger);

            var result = builder.Build().PerformUpgrade();
            if (!result.Successful)
            {
                var error = result.Error ?? new InvalidOperationException("DbUp upgrade failed.");
                var mapped = DatabaseErrorMapper.From(error);
                if (result.ErrorScript is not null)
                {
                    mapped = mapped with
                    {
                        ErrorText = $"script: {result.ErrorScript.Name}{Environment.NewLine}{mapped.ErrorText}"
                    };
                }

                return mapped;
            }

            return result.Scripts.Any() ? ScriptRunResult.Ok() : ScriptRunResult.Applied();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DbUp failed for {Script}", journalName);
            return DatabaseErrorMapper.From(ex);
        }
    }

    private static string BatchName(string journalName, int index, int count) =>
        count == 1 ? journalName : $"{journalName}#batch{index + 1}";
}
