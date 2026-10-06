using DbUp;
using DbUp.Engine;
using DbUp.Helpers;
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

        return Task.Run(
            () => Upgrade(script.JournalName, batches, script.CommandTimeoutSeconds, script.UseJournal),
            cancellationToken);
    }

    private ScriptRunResult Upgrade(
        string journalName,
        IReadOnlyList<string> batches,
        int commandTimeoutSeconds,
        bool useJournal)
    {
        var scripts = batches
            .Select((sql, index) => new SqlScript(BatchName(journalName, index, batches.Count), sql))
            .ToArray();
        var timeout = commandTimeoutSeconds < 0 ? (TimeSpan?)null : TimeSpan.FromSeconds(commandTimeoutSeconds);
        var shape = ConnectionStringShape.Validate(_options.ConnectionString, _options.Provider);
        if (!shape.IsValid || string.IsNullOrWhiteSpace(shape.Database))
        {
            var detail = shape.Errors.Count == 0
                ? "Connection string has no target database."
                : string.Join(" ", shape.Errors);
            return ScriptRunResult.Fail(
                "DbUp was not executed. " + detail);
        }

        string currentDatabase;
        try
        {
            currentDatabase = TargetDatabaseProbe.ReadCurrentDatabase(
                _options.Provider,
                _options.ConnectionString,
                commandTimeoutSeconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not read the current database before DbUp");
            var mapped = DatabaseErrorMapper.From(ex);
            return mapped with
            {
                ErrorText = "DbUp was not executed. The current database could not be read."
                    + Environment.NewLine
                    + mapped.ErrorText
            };
        }

        if (!TargetDatabase.Matches(shape.Database, currentDatabase))
        {
            var message =
                $"DbUp was not executed. Connected database is '{currentDatabase}', target database is '{shape.Database}'.";
            _logger.LogError("{Message}", message);
            return ScriptRunResult.Fail(message);
        }

        _logger.LogInformation(
            "DbUp executing on target database {Database} (journal {Journal})",
            currentDatabase,
            useJournal);

        if (useJournal)
        {
            try
            {
                JournalSchemaEnsurer.Ensure(
                    _options.Provider,
                    _options.ConnectionString,
                    _options.JournalSchema,
                    commandTimeoutSeconds,
                    _logger);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not ensure journal schema {Schema}", _options.JournalSchema);
                var mapped = DatabaseErrorMapper.From(ex);
                var schema = string.IsNullOrWhiteSpace(_options.JournalSchema) ? "dbo" : _options.JournalSchema.Trim();
                var table = string.IsNullOrWhiteSpace(_options.JournalTable) ? "schema_versions" : _options.JournalTable.Trim();
                return mapped with
                {
                    ErrorText =
                        $"Could not create journal schema '{schema}'. " +
                        $"Applied scripts are recorded in [{schema}].[{table}]. " +
                        "The login needs CREATE SCHEMA on this database, or a DBA must create the schema. " +
                        "Set Migration:JournalSchema to dbo when the login cannot create schemas."
                        + Environment.NewLine
                        + mapped.ErrorText
                };
            }
        }

        try
        {
            UpgradeEngine engine;
            if (_options.Provider == DatabaseProviderKind.PostgreSql)
            {
                var builder = DeployChanges.To
                    .PostgresqlDatabase(_options.ConnectionString)
                    .WithScripts(scripts)
                    .WithTransaction();
                engine = (useJournal
                        ? builder.JournalToPostgresqlTable(_options.JournalSchema, _options.JournalTable)
                        : builder.JournalTo(new NullJournal()))
                    .WithExecutionTimeout(timeout)
                    .LogTo(_logger)
                    .Build();
            }
            else
            {
                var builder = DeployChanges.To
                    .SqlDatabase(_options.ConnectionString)
                    .WithScripts(scripts)
                    .WithTransaction();
                engine = (useJournal
                        ? builder.JournalToSqlTable(_options.JournalSchema, _options.JournalTable)
                        : builder.JournalTo(new NullJournal()))
                    .WithExecutionTimeout(timeout)
                    .LogTo(_logger)
                    .Build();
            }

            var result = engine.PerformUpgrade();
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

            if (!useJournal)
            {
                _logger.LogInformation(
                    "DbUp executed {Count} repeatable script(s) on target database {Database}",
                    result.Scripts.Count(),
                    shape.Database);
                return ScriptRunResult.Ok();
            }

            var confirmed = ConfirmJournalOnTarget(scripts, commandTimeoutSeconds, shape.Database);
            if (confirmed is not null)
            {
                return confirmed;
            }

            if (result.Scripts.Any())
            {
                _logger.LogInformation(
                    "DbUp executed {Count} script(s) on target database {Database}",
                    result.Scripts.Count(),
                    shape.Database);
                return ScriptRunResult.Ok();
            }

            _logger.LogInformation(
                "DbUp confirmed {Count} script(s) already recorded on target database {Database}",
                scripts.Length,
                shape.Database);
            return ScriptRunResult.Applied();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "DbUp failed for {Script}", journalName);
            return DatabaseErrorMapper.From(ex);
        }
    }

    private ScriptRunResult? ConfirmJournalOnTarget(
        IReadOnlyList<SqlScript> scripts,
        int commandTimeoutSeconds,
        string expectedDatabase)
    {
        string actual;
        try
        {
            actual = TargetDatabaseProbe.ReadCurrentDatabase(
                _options.Provider,
                _options.ConnectionString,
                commandTimeoutSeconds);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not read the current database after DbUp");
            var mapped = DatabaseErrorMapper.From(ex);
            return mapped with
            {
                ErrorText =
                    $"DbUp finished, but the session database could not be confirmed as '{expectedDatabase}'."
                    + Environment.NewLine
                    + mapped.ErrorText
            };
        }

        if (!TargetDatabase.Matches(expectedDatabase, actual))
        {
            var message =
                $"DbUp finished on '{actual}' instead of target database '{expectedDatabase}'.";
            _logger.LogError("{Message}", message);
            return ScriptRunResult.Fail(message);
        }

        var schema = _options.JournalSchema;
        var table = string.IsNullOrWhiteSpace(_options.JournalTable) ? "schema_versions" : _options.JournalTable.Trim();
        foreach (var script in scripts)
        {
            bool recorded;
            try
            {
                recorded = TargetDatabaseProbe.JournalContains(
                    _options.Provider,
                    _options.ConnectionString,
                    schema,
                    table,
                    script.Name,
                    commandTimeoutSeconds);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Could not read the DbUp journal on {Database}", actual);
                var mapped = DatabaseErrorMapper.From(ex);
                return mapped with
                {
                    ErrorText =
                        $"DbUp finished on target database '{actual}', but [{schema}].[{table}] could not be read."
                        + Environment.NewLine
                        + mapped.ErrorText
                };
            }

            if (!recorded)
            {
                var message =
                    $"DbUp finished on target database '{actual}', but [{schema}].[{table}] has no row for '{script.Name}'.";
                _logger.LogError("{Message}", message);
                return ScriptRunResult.Fail(message);
            }
        }

        return null;
    }

    private static string BatchName(string journalName, int index, int count) =>
        count == 1 ? journalName : $"{journalName}#batch{index + 1}";
}
