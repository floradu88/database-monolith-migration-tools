using BuildingBlocks.DataAccess.Abstractions;

namespace BuildingBlocks.Migration;

public enum DataAccessRoute
{
    SourceFacade = 0,
    Owned = 1,
    /// <summary>Read-only compare of source vs owned. Never dual-write.</summary>
    Shadow = 2,
    /// <summary>
    /// Independent dbo + core stored-procedure writes in parallel.
    /// dbo is the caller result; core failures are evidence only.
    /// </summary>
    ParallelWrite = 3
}

public enum BlueGreenSlot
{
    Blue = 0,
    Green = 1
}

public sealed class MigrationRoutingOptions
{
    public const string SectionName = "MigrationRouting";
    public DataAccessRoute DefaultRoute { get; set; } = DataAccessRoute.SourceFacade;
    public BlueGreenSlot Slot { get; set; } = BlueGreenSlot.Blue;
    /// <summary>Default owned-side DAL when route is Owned (EfCore | StoredProcedure | PlainSql).</summary>
    public DataAccessMethod AuthoritativeMethod { get; set; } = DataAccessMethod.EfCore;
    public string RouteHeaderName { get; set; } = "X-Data-Access-Route";
    public string SlotHeaderName { get; set; } = "X-Blue-Green-Slot";
    public string MethodHeaderName { get; set; } = "X-Data-Access-Method";
    /// <summary>Cancel the core SP independently so dbo latency stays the SLO.</summary>
    public int ParallelWriteCoreTimeoutMs { get; set; } = 2000;
    public string SourceSchema { get; set; } = "dbo";
    public string OwnedSchema { get; set; } = "core";
}

public sealed class ShadowCompareResult
{
    public required string Operation { get; init; }
    public required string RouteA { get; init; }
    public required string RouteB { get; init; }
    public long ElapsedMsA { get; init; }
    public long ElapsedMsB { get; init; }
    public bool PayloadsMatch { get; init; }
    public string? DiffSummary { get; init; }
    public DateTimeOffset ComparedAt { get; init; } = DateTimeOffset.UtcNow;
}

public interface IShadowCompareStore
{
    void Add(ShadowCompareResult result);
    IReadOnlyList<ShadowCompareResult> Recent(int take = 50);
}

public sealed class InMemoryShadowCompareStore : IShadowCompareStore
{
    private readonly Queue<ShadowCompareResult> _items = new();
    private readonly object _gate = new();
    private const int Max = 200;

    public void Add(ShadowCompareResult result)
    {
        lock (_gate)
        {
            _items.Enqueue(result);
            while (_items.Count > Max) _items.Dequeue();
        }
    }

    public IReadOnlyList<ShadowCompareResult> Recent(int take = 50)
    {
        lock (_gate)
            return _items.Reverse().Take(take).ToList();
    }
}
