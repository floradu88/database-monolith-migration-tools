namespace ShowcaseDataService.Domain;

/// <summary>EF-owned aggregate sample for the Showcase golden template.</summary>
public sealed class ShowcaseItem
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = "Active";
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
