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

        var result = await ExecuteAsync(kind, journalName, rewritten.Batches, rewritten.Repeatable, cancellationToken);
        if (result.AlreadyApplied && !rewritten.Repeatable)
        {
            _store.MoveToSuccess(script);
            return ScriptOutcome.Done(repaired: false);
        }

        string? diagnostics = null;
        var repaired = false;
        if (!result.Success && _options.RepairMissingColumns)
        {
            var fault = MissingColumnParser.Parse(result.ErrorText, result.ErrorNumber, result.SqlState, sql);
            if (fault is { Table: not null })
            {
                var repair = await RepairAsync(fault, sql, cancellationToken);
                diagnostics = repair.Diagnostics;
                repaired = repair.AlterApplied;
                if (repair.AlterApplied)
                {
                    _logger.LogInformation("Applied column repair for {Script} and retrying", script.RelativePath);
                    result = await ExecuteAsync(kind, journalName, rewritten.Batches, rewritten.Repeatable, cancellationToken);
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
}
