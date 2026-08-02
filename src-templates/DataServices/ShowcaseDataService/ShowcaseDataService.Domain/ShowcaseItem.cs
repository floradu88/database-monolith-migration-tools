namespace ShowcaseDataService.Domain;

/// <summary>Marks EF-owned vs SQL-project-owned objects to keep ownership non-overlapping.</summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Property, AllowMultiple = false, Inherited = true)]
public sealed class OwnershipAttribute : Attribute
{
    public OwnershipAttribute(string owner, string schema, string objectName)
    {
        Owner = owner;
        Schema = schema;
        ObjectName = objectName;
    }

    public string Owner { get; }
    public string Schema { get; }
    public string ObjectName { get; }
    /// <summary>EF | SqlProject | SharedReadOnly</summary>
    public string Kind { get; init; } = "EF";
}

/// <summary>EF-owned aggregate sample for the Showcase golden template.</summary>
[Ownership("ShowcaseDataService", "showcase", "Items", Kind = "EF")]
public sealed class ShowcaseItem
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = "Active";
    public DateTimeOffset UpdatedAt { get; set; } = DateTimeOffset.UtcNow;
}
