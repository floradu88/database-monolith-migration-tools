using Hangfire;
using Microsoft.Extensions.Logging;
using MigrationTool.Application;

namespace MigrationTool.Host;

public sealed class ScriptPollJob
{
    private readonly IScriptPoller _poller;
    private readonly ILogger<ScriptPollJob> _logger;

    public ScriptPollJob(IScriptPoller poller, ILogger<ScriptPollJob> logger)
    {
        _poller = poller;
        _logger = logger;
    }

    [DisableConcurrentExecution(60 * 60)]
    [AutomaticRetry(Attempts = 0)]
    public async Task RunAsync()
    {
        _logger.LogInformation("Script poll started");
        try
        {
            var summary = await _poller.PollAsync(CancellationToken.None);
            _logger.LogInformation(
                "Script poll finished. Processed {Processed}, succeeded {Succeeded}, failed {Failed}, repaired {Repaired}, connectionSkipped {ConnectionSkipped}",
                summary.Processed,
                summary.Succeeded,
                summary.Failed,
                summary.Repaired,
                summary.ConnectionSkipped);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Script poll failed");
            throw;
        }
    }
}
