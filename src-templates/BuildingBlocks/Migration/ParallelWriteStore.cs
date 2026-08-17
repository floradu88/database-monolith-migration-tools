namespace BuildingBlocks.Migration;

public sealed class ParallelWriteCallResult
{
    public required string Operation { get; init; }
    public required string BusinessKey { get; init; }
    public Guid CorrelationId { get; init; } = Guid.NewGuid();
    public bool DboSucceeded { get; init; }
    public bool CoreSucceeded { get; init; }
    public bool CoreTimedOut { get; init; }
    public long DboDurationMs { get; init; }
    public long CoreDurationMs { get; init; }
    public string? CoreError { get; init; }
    public DateTimeOffset CalledAt { get; init; } = DateTimeOffset.UtcNow;
}

public sealed class TableIntegritySnapshot
{
    public required string PairName { get; init; }
    public bool IsMatch { get; init; }
    public int DboDeltaCount { get; init; }
    public int CoreCount { get; init; }
    public int MissingInCoreCount { get; init; }
    public int MissingInDboCount { get; init; }
    public long DurationMs { get; init; }
    public string? SampleDiff { get; init; }
    public DateTimeOffset CheckedAt { get; init; } = DateTimeOffset.UtcNow;
}

public interface IParallelWriteStore
{
    void AddCall(ParallelWriteCallResult result);
    void AddIntegrity(TableIntegritySnapshot snapshot);
    IReadOnlyList<ParallelWriteCallResult> RecentCalls(int take = 50);
    IReadOnlyList<TableIntegritySnapshot> RecentIntegrity(int take = 20);
    ParallelWriteDashboardSnapshot Snapshot();
}

public sealed class ParallelWriteDashboardSnapshot
{
    public int Calls { get; init; }
    public int DboFailures { get; init; }
    public int CoreFailures { get; init; }
    public int CoreTimeouts { get; init; }
    public int IntegrityChecks { get; init; }
    public int IntegrityMismatches { get; init; }
    public long DboP95Ms { get; init; }
    public long CoreP95Ms { get; init; }
    public bool LastIntegrityMatch { get; init; } = true;
}

public sealed class InMemoryParallelWriteStore : IParallelWriteStore
{
    private readonly Queue<ParallelWriteCallResult> _calls = new();
    private readonly Queue<TableIntegritySnapshot> _integrity = new();
    private readonly object _gate = new();
    private const int Max = 200;

    public void AddCall(ParallelWriteCallResult result)
    {
        lock (_gate)
        {
            _calls.Enqueue(result);
            while (_calls.Count > Max) _calls.Dequeue();
        }
    }

    public void AddIntegrity(TableIntegritySnapshot snapshot)
    {
        lock (_gate)
        {
            _integrity.Enqueue(snapshot);
            while (_integrity.Count > Max) _integrity.Dequeue();
        }
    }

    public IReadOnlyList<ParallelWriteCallResult> RecentCalls(int take = 50)
    {
        lock (_gate)
            return _calls.Reverse().Take(take).ToList();
    }

    public IReadOnlyList<TableIntegritySnapshot> RecentIntegrity(int take = 20)
    {
        lock (_gate)
            return _integrity.Reverse().Take(take).ToList();
    }

    public ParallelWriteDashboardSnapshot Snapshot()
    {
        lock (_gate)
        {
            var dbo = Percentile(_calls.Select(c => c.DboDurationMs).ToList());
            var core = Percentile(_calls.Select(c => c.CoreDurationMs).ToList());
            var last = _integrity.LastOrDefault();
            return new ParallelWriteDashboardSnapshot
            {
                Calls = _calls.Count,
                DboFailures = _calls.Count(c => !c.DboSucceeded),
                CoreFailures = _calls.Count(c => !c.CoreSucceeded),
                CoreTimeouts = _calls.Count(c => c.CoreTimedOut),
                IntegrityChecks = _integrity.Count,
                IntegrityMismatches = _integrity.Count(i => !i.IsMatch),
                DboP95Ms = dbo,
                CoreP95Ms = core,
                LastIntegrityMatch = last?.IsMatch ?? true
            };
        }
    }

    private static long Percentile(List<long> values)
    {
        if (values.Count == 0) return 0;
        values.Sort();
        return values[Math.Max(0, (int)Math.Ceiling(values.Count * 0.95) - 1)];
    }
}
