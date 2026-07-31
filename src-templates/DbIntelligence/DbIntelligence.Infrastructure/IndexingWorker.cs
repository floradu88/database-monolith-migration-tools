using DbIntelligence.Contracts;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace DbIntelligence.Infrastructure;

public sealed class IndexingWorker : BackgroundService
{
    private readonly IIndexingService _indexing;
    private readonly IIntelligenceStore _store;
    private readonly DbIntelligenceOptions _options;
    private readonly ILogger<IndexingWorker> _logger;

    public IndexingWorker(
        IIndexingService indexing,
        IIntelligenceStore store,
        IOptions<DbIntelligenceOptions> options,
        ILogger<IndexingWorker> logger)
    {
        _indexing = indexing;
        _store = store;
        _options = options.Value;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.IsNullOrWhiteSpace(_options.TargetRepositoryPath) ||
            !Directory.Exists(_options.TargetRepositoryPath))
        {
            _logger.LogInformation("Worker idle: TargetRepositoryPath is not configured or missing.");
            return;
        }

        if (_store.CurrentGraph is not null)
        {
            _logger.LogInformation("Graph already loaded; worker will not auto-index on startup.");
            return;
        }

        _logger.LogInformation("Starting startup index for {Path}", _options.TargetRepositoryPath);
        var request = new IndexJobRequest
        {
            TargetRepositoryPath = _options.TargetRepositoryPath,
            RunCodegraph = true,
            RunGraphify = true,
            RunRepositoryScan = true,
            RunSqlScan = !string.IsNullOrWhiteSpace(_options.SqlConnectionString),
            SqlConnectionString = _options.SqlConnectionString
        };

        var job = _store.CreateJob(request);
        await _indexing.RunJobAsync(job.Id, request, stoppingToken);
    }
}
