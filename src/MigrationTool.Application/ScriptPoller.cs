using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MigrationTool.Domain;

namespace MigrationTool.Application;

public sealed class ScriptPoller : IScriptPoller
{
    private readonly MigrationOptions _options;
    private readonly IScriptStore _store;
    private readonly IChangeScriptRunner _changes;
    private readonly IQueryScriptRunner _queries;
    private readonly IConnectionValidator _connections;
    private readonly ILogger<ScriptPoller> _logger;

    public ScriptPoller(
        IOptions<MigrationOptions> options,
        IScriptStore store,
        IChangeScriptRunner changes,
        IQueryScriptRunner queries,
        IConnectionValidator connections,
        ILogger<ScriptPoller> logger)
    {
        _options = options.Value;
        _store = store;
        _changes = changes;
        _queries = queries;
        _connections = connections;
        _logger = logger;
    }

    public async Task<PollSummary> PollAsync(CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.ConnectionString))
        {
            var empty = DatabaseConnectionErrorClassifier.Empty();
            _logger.LogWarning("{Message} {Guidance}", empty.Summary, empty.Guidance);
            return new PollSummary(0, 0, 0, 0, true, empty.Format());
        }

        if (_options.MaxRetries < 1)
        {
            throw new InvalidOperationException("Migration:MaxRetries must be at least 1.");
        }

        var connectionCheck = await _connections.ValidateAsync(
            _options.ConnectionString,
            _options.Provider,
            _options.CommandTimeoutSeconds,
            cancellationToken);
        if (!connectionCheck.Succeeded)
        {
            var detail = connectionCheck.ConnectionError?.Format() ?? connectionCheck.Error ?? "Connection failed.";
            _logger.LogError("Poll skipped because the database is unreachable. {Detail}", detail);
            return new PollSummary(0, 0, 0, 0, true, detail);
        }

        var scripts = _store.ListInbox()
            .Select(script => (Script: script, Kind: ScriptKindClassifier.Classify(script.RelativePath, script.Contents)))
            .OrderBy(item => item.Kind)
            .ThenBy(item => item.Script.RelativePath, StringComparer.OrdinalIgnoreCase)
            .ToList();

        var processed = 0;
        var succeeded = 0;
        var failed = 0;
        var repaired = 0;

        foreach (var item in scripts)
        {
            cancellationToken.ThrowIfCancellationRequested();
            processed++;
            _logger.LogInformation("Running {Kind} script {Script}", item.Kind, item.Script.RelativePath);
            var outcome = await ProcessAsync(item.Script, item.Kind, cancellationToken);
            if (outcome.Repaired)
            {
                repaired++;
            }

            if (outcome.Success)
            {
                succeeded++;
                if (outcome.Repaired)
                {
                    _logger.LogWarning(
                        "Script {Script} succeeded after column repair ({Kind})",
                        item.Script.RelativePath,
                        item.Kind);
                }
                else
                {
                    _logger.LogInformation(
                        "Script {Script} succeeded ({Kind})",
                        item.Script.RelativePath,
                        item.Kind);
                }
            }
            else
            {
                failed++;
            }
        }

        if (failed > 0)
        {
            _logger.LogError(
                "Poll finished. Processed {Processed}, succeeded {Succeeded}, failed {Failed}, repaired {Repaired}",
                processed,
                succeeded,
                failed,
                repaired);
        }
        else if (repaired > 0)
        {
            _logger.LogWarning(
                "Poll finished. Processed {Processed}, succeeded {Succeeded}, failed {Failed}, repaired {Repaired}",
                processed,
                succeeded,
                failed,
                repaired);
        }
        else
        {
            _logger.LogInformation(
                "Poll finished. Processed {Processed}, succeeded {Succeeded}, failed {Failed}, repaired {Repaired}",
                processed,
                succeeded,
                failed,
                repaired);
        }

        return new PollSummary(processed, succeeded, failed, repaired, false);
    }

    private async Task<ScriptOutcome> ProcessAsync(InboxScript script, ScriptKind kind, CancellationToken cancellationToken)
    {
        var nextAttempt = RetryNames.NextAttempt(_store.ListFailedSqlFileNames(), script.FileName);
        if (nextAttempt > _options.MaxRetries)
        {
            _logger.LogWarning("Retries exhausted for {Script}. Removing it from the inbox.", script.RelativePath);
            _store.RemoveFromInbox(script);
            return new ScriptOutcome(false, false);
        }

        var split = GoBatchSplitter.Split(script.Contents);
        var rewritten = CreateOrAlterRewriter.RewriteBatches(split.Batches, _options.Provider);
        var normalized = string.Join($"\n\n{GoBatchSplitter.BatchMarker}\n\n", rewritten.Batches).Trim();
        if (split.RemovedGo || rewritten.Changed)
        {
            _store.SaveContents(script.FullPath, normalized);
        }

        var sql = normalized;
        var journalName = script.RelativePath.Replace('\\', '/');
        if (string.IsNullOrWhiteSpace(sql))
        {
            if (kind == ScriptKind.Query)
            {
                _store.WriteQueryResult(script.FileName, "", DateTimeOffset.UtcNow);
            }
            else
            {
                _store.MoveToSuccess(script);
            }

            return ScriptOutcome.Done(repaired: false);
        }

        string? diagnostics = null;
        var repaired = false;
        var batchesToRun = rewritten.Batches;
        if (kind != ScriptKind.Query && _options.SyncCreateTableColumns)
        {
            var createTables = CreateTableDefinitionParser.ParseAll(sql);
            if (createTables.Count > 0)
            {
                var sync = await SyncCreateTableColumnsAsync(createTables, batchesToRun, cancellationToken);
                diagnostics = sync.Diagnostics;
                repaired = sync.ColumnsAdded > 0;
                batchesToRun = sync.BatchesToRun;
                if (!string.IsNullOrWhiteSpace(sync.Error))
                {
                    RecordFailure(
                        script,
                        sql,
                        ScriptRunResult.Fail(sync.Error),
                        diagnostics);
                    return new ScriptOutcome(false, repaired);
                }

                _logger.LogInformation(
                    "CREATE TABLE sync for {Script}: source columns compared; added {Added}; skipped existing tables {Skipped}",
                    script.RelativePath,
                    sync.ColumnsAdded,
                    sync.SkippedCreateCount);
            }
        }

        ScriptRunResult result;
        if (batchesToRun.All(string.IsNullOrWhiteSpace))
        {
            result = ScriptRunResult.Ok();
        }
        else
        {
            result = await ExecuteAsync(kind, journalName, batchesToRun, rewritten.Repeatable, cancellationToken);
        }

        if (result.AlreadyApplied && !rewritten.Repeatable)
        {
            _store.MoveToSuccess(script);
            return ScriptOutcome.Done(repaired: repaired);
        }

        if (!result.Success
            && kind != ScriptKind.Query
            && _options.SyncCreateTableColumns
            && TableAlreadyExistsParser.Matches(result.ErrorText, result.ErrorNumber, result.SqlState))
        {
            var createTables = CreateTableDefinitionParser.ParseAll(sql);
            if (createTables.Count > 0)
            {
                var sync = await SyncCreateTableColumnsAsync(createTables, Array.Empty<string>(), cancellationToken);
                diagnostics = ConcatDiagnostics(diagnostics, sync.Diagnostics);
                repaired |= sync.ColumnsAdded > 0;
                if (string.IsNullOrWhiteSpace(sync.Error))
                {
                    _logger.LogInformation(
                        "CREATE TABLE already existed for {Script}; compared columns and added {Added}",
                        script.RelativePath,
                        sync.ColumnsAdded);
                    result = ScriptRunResult.Ok();
                }
                else
                {
                    result = result with
                    {
                        ErrorText = (result.ErrorText ?? "") + Environment.NewLine + sync.Error
                    };
                }
            }
        }

        if (!result.Success && _options.RepairMissingColumns)
        {
            var fault = MissingColumnParser.Parse(result.ErrorText, result.ErrorNumber, result.SqlState, sql);
            if (fault is { Table: not null })
            {
                var repair = await RepairAsync(fault, sql, cancellationToken);
                diagnostics = ConcatDiagnostics(diagnostics, repair.Diagnostics);
                repaired |= repair.AlterApplied;
                if (repair.AlterApplied)
                {
                    _logger.LogInformation("Applied column repair for {Script} and retrying", script.RelativePath);
                    result = await ExecuteAsync(kind, journalName, batchesToRun, rewritten.Repeatable, cancellationToken);
                }
                else if (!string.IsNullOrWhiteSpace(repair.AlterError))
                {
                    result = result with { ErrorText = (result.ErrorText ?? "") + Environment.NewLine + repair.AlterError };
                }
            }
            else if (fault is not null)
            {
                result = result with
                {
                    ErrorText = (result.ErrorText ?? "")
                        + Environment.NewLine
                        + $"Missing column '{fault.Column}' was detected, and the table name could not be determined, so no ALTER was applied."
                };
            }
        }

        if (result.Success)
        {
            if (kind == ScriptKind.Query)
            {
                _store.WriteQueryResult(script.FileName, result.ResultText ?? "", DateTimeOffset.UtcNow);
            }
            else if (rewritten.Repeatable)
            {
                _logger.LogInformation(
                    "Reapplied {Script}; leaving it in the inbox so the next poll runs it again",
                    script.RelativePath);
            }
            else
            {
                _store.MoveToSuccess(script);
            }

            return new ScriptOutcome(true, repaired);
        }

        RecordFailure(script, sql, result, diagnostics);
        return new ScriptOutcome(false, repaired);
    }

    private async Task<ScriptRunResult> ExecuteAsync(
        ScriptKind kind,
        string journalName,
        IReadOnlyList<string> batches,
        bool repeatable,
        CancellationToken cancellationToken)
    {
        if (kind == ScriptKind.Query)
        {
            var builder = new StringBuilder();
            foreach (var batch in batches.Where(batch => !string.IsNullOrWhiteSpace(batch)))
            {
                var part = await _queries.ExecuteAsync(
                    new QueryScriptRequest(journalName, batch, _options.CommandTimeoutSeconds),
                    cancellationToken);
                if (!part.Success)
                {
                    return part;
                }

                if (builder.Length > 0)
                {
                    builder.AppendLine();
                }

                builder.Append(part.ResultText);
            }

            return ScriptRunResult.Ok(builder.ToString());
        }

        return await _changes.ExecuteAsync(
            new ChangeScript(journalName, batches, _options.CommandTimeoutSeconds, UseJournal: !repeatable),
            cancellationToken);
    }

    private async Task<CreateTableSyncOutcome> SyncCreateTableColumnsAsync(
        IReadOnlyList<CreateTableDefinition> definitions,
        IReadOnlyList<string> batches,
        CancellationToken cancellationToken)
    {
        var diagnostics = new StringBuilder();
        var remaining = batches.ToList();
        var columnsAdded = 0;
        var skippedCreate = 0;

        foreach (var definition in definitions)
        {
            var schema = string.IsNullOrWhiteSpace(definition.Schema)
                ? SqlIdentifier.DefaultSchema(_options.Provider)
                : definition.Schema;
            var sourceNames = string.Join(", ", definition.Columns.Select(column => column.Name));
            diagnostics.AppendLine($"-- source {schema}.{definition.Table} columns: {sourceNames}");

            var existsSql = TableColumnInventory.ExistsSql(_options.Provider, schema, definition.Table);
            var existsResult = await _queries.ExecuteAsync(
                new QueryScriptRequest($"table-exists:{schema}.{definition.Table}", existsSql, _options.CommandTimeoutSeconds),
                cancellationToken);
            diagnostics.AppendLine(existsSql);
            diagnostics.AppendLine(existsResult.ResultText ?? existsResult.ErrorText ?? "");
            if (!existsResult.Success)
            {
                return new CreateTableSyncOutcome(
                    remaining,
                    columnsAdded,
                    skippedCreate,
                    diagnostics.ToString(),
                    existsResult.ErrorText ?? "Could not check whether the CREATE TABLE target exists.");
            }

            if (!TableColumnInventory.ParseExists(existsResult.ResultText))
            {
                diagnostics.AppendLine($"-- live table {schema}.{definition.Table} is missing; CREATE TABLE will run.");
                continue;
            }

            skippedCreate++;
            var columnsSql = TableColumnInventory.ColumnsSql(_options.Provider, schema, definition.Table);
            var columnsResult = await _queries.ExecuteAsync(
                new QueryScriptRequest($"table-columns:{schema}.{definition.Table}", columnsSql, _options.CommandTimeoutSeconds),
                cancellationToken);
            diagnostics.AppendLine(columnsSql);
            diagnostics.AppendLine(columnsResult.ResultText ?? columnsResult.ErrorText ?? "");
            if (!columnsResult.Success)
            {
                return new CreateTableSyncOutcome(
                    remaining,
                    columnsAdded,
                    skippedCreate,
                    diagnostics.ToString(),
                    columnsResult.ErrorText ?? "Could not list live columns for CREATE TABLE sync.");
            }

            var liveColumns = TableColumnInventory.ParseColumnNames(columnsResult.ResultText);
            diagnostics.AppendLine(
                $"-- live {schema}.{definition.Table} columns: {(liveColumns.Count == 0 ? "(none)" : string.Join(", ", liveColumns))}");

            var missing = TableColumnInventory.MissingColumns(definition, liveColumns);
            foreach (var column in missing)
            {
                var alterSql = AlterScriptBuilder.Build(
                    _options.Provider,
                    schema,
                    definition.Table,
                    column,
                    "taken from CREATE TABLE source");
                var preStepName = AlterScriptBuilder.FileName(schema, definition.Table, column.Name);
                var alter = await _changes.ExecuteAsync(
                    new ChangeScript(preStepName, [alterSql], _options.CommandTimeoutSeconds),
                    cancellationToken);
                if (!alter.Success)
                {
                    return new CreateTableSyncOutcome(
                        remaining,
                        columnsAdded,
                        skippedCreate,
                        diagnostics.ToString(),
                        alter.ErrorText ?? $"ALTER ADD for {column.Name} failed.");
                }

                _store.WritePreStep(preStepName, alterSql, diagnostics.ToString());
                columnsAdded++;
                _logger.LogInformation(
                    "Added missing column {Schema}.{Table}.{Column} from CREATE TABLE source",
                    schema,
                    definition.Table,
                    column.Name);
            }

            remaining = remaining
                .Where(batch => !CreateTableDefinitionParser.BatchIsCreateTableFor(batch, definition))
                .ToList();
        }

        return new CreateTableSyncOutcome(remaining, columnsAdded, skippedCreate, diagnostics.ToString(), null);
    }

    private static string? ConcatDiagnostics(string? left, string? right)
    {
        if (string.IsNullOrWhiteSpace(left))
        {
            return right;
        }

        if (string.IsNullOrWhiteSpace(right))
        {
            return left;
        }

        return left + Environment.NewLine + right;
    }

    private async Task<RepairOutcome> RepairAsync(MissingColumnFault fault, string sql, CancellationToken cancellationToken)
    {
        var schema = string.IsNullOrWhiteSpace(fault.Schema)
            ? SqlIdentifier.DefaultSchema(_options.Provider)
            : fault.Schema;
        var table = fault.Table ?? "";
        var diagnosticSql = DiagnosticScriptBuilder.Build(_options.Provider, schema, table);
        var diagnostic = await _queries.ExecuteAsync(
            new QueryScriptRequest($"diagnostic:{schema}.{table}", diagnosticSql, _options.CommandTimeoutSeconds),
            cancellationToken);

        var diagnostics = diagnostic.Success
            ? diagnosticSql + Environment.NewLine + Environment.NewLine + (diagnostic.ResultText ?? "")
            : diagnosticSql + Environment.NewLine + Environment.NewLine + "-- diagnostic failed" + Environment.NewLine + diagnostic.ErrorText;

        var inferred = ColumnTypeInferrer.Infer(_options.Provider, sql, fault.Column);
        var alterSql = AlterScriptBuilder.Build(_options.Provider, schema, table, fault.Column, inferred);
        var preStepName = AlterScriptBuilder.FileName(schema, table, fault.Column);
        var alter = await _changes.ExecuteAsync(
            new ChangeScript(preStepName, [alterSql], _options.CommandTimeoutSeconds),
            cancellationToken);

        if (!alter.Success)
        {
            return new RepairOutcome(false, diagnostics, alter.ErrorText ?? "ALTER pre-step failed.");
        }

        _store.WritePreStep(preStepName, alterSql, diagnostics);
        return new RepairOutcome(true, diagnostics, null);
    }

    private void RecordFailure(InboxScript script, string contents, ScriptRunResult result, string? diagnostics)
    {
        var attempt = RetryNames.NextAttempt(_store.ListFailedSqlFileNames(), script.FileName);
        if (attempt > _options.MaxRetries)
        {
            _logger.LogWarning("Retries exhausted for {Script}. Removing it from the inbox.", script.RelativePath);
            _store.RemoveFromInbox(script);
            return;
        }

        var error = FailureReport.Format(
            script.RelativePath,
            attempt,
            _options.Provider,
            DateTimeOffset.UtcNow,
            result.ErrorText ?? "Script failed.");
        _store.WriteFailure(script.FileName, contents, attempt, error, diagnostics);
        _logger.LogError("Script {Script} failed on attempt {Attempt}", script.RelativePath, attempt);

        if (attempt >= _options.MaxRetries)
        {
            _store.RemoveFromInbox(script);
        }
    }

    private sealed record ScriptOutcome(bool Success, bool Repaired)
    {
        public static ScriptOutcome Done(bool repaired) => new(true, repaired);
    }

    private sealed record RepairOutcome(bool AlterApplied, string Diagnostics, string? AlterError);

    private sealed record CreateTableSyncOutcome(
        IReadOnlyList<string> BatchesToRun,
        int ColumnsAdded,
        int SkippedCreateCount,
        string Diagnostics,
        string? Error);
}
