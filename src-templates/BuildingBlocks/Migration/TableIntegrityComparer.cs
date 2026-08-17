namespace BuildingBlocks.Migration;

public sealed class TableIntegrityCompareResult
{
    public bool IsMatch { get; init; }
    public int DboDeltaCount { get; init; }
    public int CoreCount { get; init; }
    /// <summary>dbo rows not in core (other writers / history). Informational — does not fail the check.</summary>
    public int MissingInCoreCount { get; init; }
    /// <summary>core SP-written rows not found in dbo. This is the mismatch.</summary>
    public int MissingInDboCount { get; init; }
    public IReadOnlyList<string> MissingInCoreKeys { get; init; } = [];
    public IReadOnlyList<string> MissingInDboKeys { get; init; } = [];
}

/// <summary>
/// SP-write subset check: every core row must exist in dbo on declared columns.
/// Extra dbo rows (EF, ad-hoc SQL, jobs, history) are expected and are not a mismatch.
/// Algorithm for <c>core.usp_TableIntegrity_Check</c>.
/// </summary>
public static class TableIntegrityComparer
{
    public static TableIntegrityCompareResult Compare(
        IReadOnlyList<IReadOnlyDictionary<string, string?>> dboRows,
        IReadOnlyList<IReadOnlyDictionary<string, string?>> coreRows,
        IReadOnlyList<string> compareColumns)
    {
        var cols = compareColumns.Select(c => c.Trim()).Where(c => c.Length > 0).ToArray();
        var dboSet = dboRows.Select(r => Signature(r, cols)).ToHashSet(StringComparer.Ordinal);
        var coreSet = coreRows.Select(r => Signature(r, cols)).ToHashSet(StringComparer.Ordinal);

        var extraInDbo = dboSet.Except(coreSet, StringComparer.Ordinal).ToList();
        var coreNotInDbo = coreSet.Except(dboSet, StringComparer.Ordinal).ToList();

        return new TableIntegrityCompareResult
        {
            IsMatch = coreNotInDbo.Count == 0,
            DboDeltaCount = dboRows.Count,
            CoreCount = coreRows.Count,
            MissingInCoreCount = extraInDbo.Count,
            MissingInDboCount = coreNotInDbo.Count,
            MissingInCoreKeys = extraInDbo,
            MissingInDboKeys = coreNotInDbo
        };
    }

    private static string Signature(IReadOnlyDictionary<string, string?> row, IReadOnlyList<string> columns)
    {
        var lookup = row.ToDictionary(kv => kv.Key, kv => kv.Value, StringComparer.OrdinalIgnoreCase);
        return string.Join('\u001f', columns.Select(c => lookup.TryGetValue(c, out var v) ? v ?? "" : ""));
    }
}
