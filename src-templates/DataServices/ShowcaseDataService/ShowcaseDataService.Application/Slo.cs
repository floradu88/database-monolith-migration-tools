namespace ShowcaseDataService.Application;

public sealed class ShowcaseSloOptions
{
    public const string SectionName = "Slo";
    public double ReadLatencyP95MsBudget { get; set; } = 200;
    public double ErrorRateBudgetPercent { get; set; } = 1;
}

public interface IShowcaseSloCounter
{
    void RecordSuccess(long elapsedMs);
    void RecordError();
    ShowcaseSloSnapshot Snapshot(ShowcaseSloOptions options);
}

public sealed class ShowcaseSloSnapshot
{
    public long Requests { get; init; }
    public long Errors { get; init; }
    public double ErrorRatePercent { get; init; }
    public long P95Ms { get; init; }
    public bool WithinLatencyBudget { get; init; }
    public bool WithinErrorBudget { get; init; }
}

public sealed class InMemoryShowcaseSloCounter : IShowcaseSloCounter
{
    private readonly object _gate = new();
    private long _requests;
    private long _errors;
    private readonly Queue<long> _latencies = new();
    private const int Max = 500;

    public void RecordSuccess(long elapsedMs)
    {
        lock (_gate)
        {
            _requests++;
            _latencies.Enqueue(elapsedMs);
            while (_latencies.Count > Max) _latencies.Dequeue();
        }
    }

    public void RecordError()
    {
        lock (_gate)
        {
            _requests++;
            _errors++;
        }
    }

    public ShowcaseSloSnapshot Snapshot(ShowcaseSloOptions options)
    {
        lock (_gate)
        {
            var ordered = _latencies.OrderBy(x => x).ToList();
            var p95 = ordered.Count == 0 ? 0 : ordered[Math.Max(0, (int)Math.Ceiling(ordered.Count * 0.95) - 1)];
            var errPct = _requests == 0 ? 0 : 100.0 * _errors / _requests;
            return new ShowcaseSloSnapshot
            {
                Requests = _requests,
                Errors = _errors,
                ErrorRatePercent = Math.Round(errPct, 2),
                P95Ms = p95,
                WithinLatencyBudget = p95 <= options.ReadLatencyP95MsBudget,
                WithinErrorBudget = errPct <= options.ErrorRateBudgetPercent
            };
        }
    }
}
