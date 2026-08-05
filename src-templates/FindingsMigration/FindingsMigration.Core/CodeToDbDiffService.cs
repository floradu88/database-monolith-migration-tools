using System.Text.Json;
using FindingsMigration.Contracts;

namespace FindingsMigration.Core;

/// <summary>
/// Diffs two <c>code-to-db-map.json</c> exports and returns only NEW EXTRACTED edges
/// present in the current map but not the previous (incremental re-index packaging).
/// </summary>
public sealed class CodeToDbDiffService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public CodeToDbDiffResult DiffFiles(string previousMapPath, string currentMapPath)
    {
        if (!File.Exists(previousMapPath))
            throw new FileNotFoundException("Previous code-to-db map not found", previousMapPath);
        if (!File.Exists(currentMapPath))
            throw new FileNotFoundException("Current code-to-db map not found", currentMapPath);

        var previous = JsonSerializer.Deserialize<CodeToDbMapDocument>(
            File.ReadAllText(previousMapPath), JsonOptions) ?? new();
        var current = JsonSerializer.Deserialize<CodeToDbMapDocument>(
            File.ReadAllText(currentMapPath), JsonOptions) ?? new();

        return Diff(previous, current);
    }

    public CodeToDbDiffResult Diff(CodeToDbMapDocument previous, CodeToDbMapDocument current)
    {
        var previousKeys = new HashSet<string>(
            (previous.Entries ?? [])
                .Where(IsExtracted)
                .Select(EdgeKey),
            StringComparer.OrdinalIgnoreCase);

        var newExtracted = (current.Entries ?? [])
            .Where(IsExtracted)
            .Where(e => !previousKeys.Contains(EdgeKey(e)))
            .ToList();

        return new CodeToDbDiffResult
        {
            PreviousExtractedCount = (previous.Entries ?? []).Count(IsExtracted),
            CurrentExtractedCount = (current.Entries ?? []).Count(IsExtracted),
            NewExtractedCount = newExtracted.Count,
            NewExtractedEntries = newExtracted
        };
    }

    public void WriteDiffDocument(CodeToDbDiffResult result, string outputPath)
    {
        var dir = Path.GetDirectoryName(outputPath);
        if (!string.IsNullOrWhiteSpace(dir))
            Directory.CreateDirectory(dir);

        var doc = new CodeToDbMapDocument
        {
            GeneratedAt = DateTimeOffset.UtcNow,
            Entries = result.NewExtractedEntries
        };
        File.WriteAllText(outputPath, JsonSerializer.Serialize(doc, JsonOptions));
    }

    private static bool IsExtracted(CodeToDbEntry e) =>
        string.Equals(e.Confidence, "EXTRACTED", StringComparison.OrdinalIgnoreCase);

    /// <summary>Stable identity for an edge across map exports.</summary>
    internal static string EdgeKey(CodeToDbEntry e)
    {
        var code = !string.IsNullOrWhiteSpace(e.CodeNodeId) ? e.CodeNodeId : e.CodeLabel;
        var db = !string.IsNullOrWhiteSpace(e.DbNodeId) ? e.DbNodeId : e.DbObject;
        return $"{code}|{db}|{e.Relation}|{e.Pattern ?? ""}";
    }
}

public sealed class CodeToDbDiffResult
{
    public int PreviousExtractedCount { get; init; }
    public int CurrentExtractedCount { get; init; }
    public int NewExtractedCount { get; init; }
    public List<CodeToDbEntry> NewExtractedEntries { get; init; } = [];
}
