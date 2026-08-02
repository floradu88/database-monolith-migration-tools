using BuildingBlocks.DataAccess.Abstractions;

namespace BuildingBlocks.DataAccess.Abstractions;

public sealed class InMemoryDataAccessTimingStore : IDataAccessTimingStore
{
    private readonly Queue<DataAccessTimingSample> _items = new();
    private readonly object _gate = new();
    private const int Max = 500;

    public void Record(DataAccessTimingSample sample)
    {
        lock (_gate)
        {
            _items.Enqueue(sample);
            while (_items.Count > Max) _items.Dequeue();
        }
    }

    public IReadOnlyList<DataAccessTimingSample> Recent(int take = 100)
    {
        lock (_gate)
            return _items.Reverse().Take(take).ToList();
    }

    public IReadOnlyList<DataAccessMethodStats> Summarize(string? operation = null)
    {
        List<DataAccessTimingSample> snapshot;
        lock (_gate)
            snapshot = _items.ToList();

        var query = snapshot.Where(s => s.Succeeded);
        if (!string.IsNullOrWhiteSpace(operation))
            query = query.Where(s => string.Equals(s.Operation, operation, StringComparison.OrdinalIgnoreCase));

        return query
            .GroupBy(s => new { s.Operation, s.Method })
            .Select(g =>
            {
                var ordered = g.Select(x => x.ElapsedMs).OrderBy(x => x).ToList();
                var p95Index = Math.Max(0, (int)Math.Ceiling(ordered.Count * 0.95) - 1);
                return new DataAccessMethodStats
                {
                    Operation = g.Key.Operation,
                    Method = g.Key.Method,
                    Samples = ordered.Count,
                    AvgMs = ordered.Average(),
                    MinMs = ordered.Min(),
                    MaxMs = ordered.Max(),
                    P95Ms = ordered[p95Index]
                };
            })
            .OrderBy(s => s.Operation)
            .ThenBy(s => s.AvgMs)
            .ToList();
    }
}
