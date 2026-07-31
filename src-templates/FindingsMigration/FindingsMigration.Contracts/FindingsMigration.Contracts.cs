using System.Text.Json.Serialization;

namespace FindingsMigration.Contracts;

public sealed class CodeToDbMapDocument
{
    [JsonPropertyName("generatedAt")]
    public DateTimeOffset? GeneratedAt { get; set; }

    [JsonPropertyName("entries")]
    public List<CodeToDbEntry> Entries { get; set; } = [];
}

public sealed class CodeToDbEntry
{
    [JsonPropertyName("codeNodeId")]
    public string CodeNodeId { get; set; } = "";

    [JsonPropertyName("codeLabel")]
    public string CodeLabel { get; set; } = "";

    [JsonPropertyName("sourceFile")]
    public string? SourceFile { get; set; }

    [JsonPropertyName("line")]
    public int? Line { get; set; }

    [JsonPropertyName("dbNodeId")]
    public string DbNodeId { get; set; } = "";

    [JsonPropertyName("dbObject")]
    public string DbObject { get; set; } = "";

    [JsonPropertyName("dbKind")]
    public string DbKind { get; set; } = "";

    [JsonPropertyName("relation")]
    public string Relation { get; set; } = "";

    [JsonPropertyName("confidence")]
    public string Confidence { get; set; } = "";

    [JsonPropertyName("pattern")]
    public string? Pattern { get; set; }
}

public sealed class StoredProcedureMapDocument
{
    [JsonPropertyName("generatedAt")]
    public DateTimeOffset? GeneratedAt { get; set; }

    [JsonPropertyName("procedures")]
    public List<StoredProcedureEntry> Procedures { get; set; } = [];
}

public sealed class StoredProcedureEntry
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("schema")]
    public string? Schema { get; set; }

    [JsonPropertyName("database")]
    public string? Database { get; set; }

    [JsonPropertyName("callers")]
    public List<string> Callers { get; set; } = [];

    [JsonPropertyName("reads")]
    public List<string> Reads { get; set; } = [];

    [JsonPropertyName("writes")]
    public List<string> Writes { get; set; } = [];
}

public sealed class DomainPackageOptions
{
    public required string DomainName { get; init; }
    public required string TargetService { get; init; }
    public string SourceDatabase { get; init; } = "MonolithDb";
    public string TargetDatabase { get; init; } = "";
    public string TargetSchema { get; init; } = "";
    public string OwnerTeam { get; init; } = "TBD";
    public bool IncludeAmbiguous { get; init; }
}

public sealed class DomainPackageResult
{
    public string DomainName { get; init; } = "";
    public string TargetService { get; init; } = "";
    public int ExtractedCount { get; init; }
    public int AmbiguousCount { get; init; }
    public int SkippedAmbiguousCount { get; init; }
    public int ProcedureCount { get; init; }
    public List<string> WrittenFiles { get; init; } = [];
}
