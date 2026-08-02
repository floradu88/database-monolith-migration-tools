namespace BuildingBlocks.Migration;

public enum DataAccessRoute
{
    SourceFacade = 0,
    Owned = 1,
    Shadow = 2
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
    public string RouteHeaderName { get; set; } = "X-Data-Access-Route";
    public string SlotHeaderName { get; set; } = "X-Blue-Green-Slot";
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
