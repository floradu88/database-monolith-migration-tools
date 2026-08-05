using FindingsMigration.Contracts;

namespace FindingsMigration.Core;

/// <summary>
/// Lightweight EF vs Dapper vs SP hints from <c>docs/07-data-access-strategy.md</c>.
/// Advisory only — does not select a DAL at runtime.
/// </summary>
public static class DataAccessRecommendation
{
    /// <summary>
    /// One-line recommendation for an API/FINDINGS stub.
    /// Heuristics: simple CRUD / EF patterns → EF Core; multi-result / set-based / SP / Dapper → SP or Dapper.
    /// </summary>
    public static string Recommend(CodeToDbEntry entry) =>
        Recommend(entry.DbKind, entry.Relation, entry.Pattern);

    public static string Recommend(string? dbKind, string? relation, string? pattern)
    {
        var kind = dbKind ?? "";
        var rel = relation ?? "";
        var pat = pattern ?? "";

        if (LooksLikeStoredProcedure(kind, pat, rel))
            return "Recommend: Stored procedure / Dapper (multi-result or set-based — docs/07-data-access-strategy.md).";

        if (LooksLikeTunedOrMultiResult(pat, rel))
            return "Recommend: Dapper or SP (tuned / multi-result query — docs/07-data-access-strategy.md).";

        if (LooksLikeSimpleCrud(kind, pat, rel))
            return "Recommend: EF Core (ordinary CRUD / aggregate persistence — docs/07-data-access-strategy.md).";

        return "Recommend: EF Core for ordinary CRUD; Dapper/SP for set-based or multi-result (docs/07-data-access-strategy.md).";
    }

    private static bool LooksLikeStoredProcedure(string kind, string pattern, string relation) =>
        kind.Contains("StoredProcedure", StringComparison.OrdinalIgnoreCase) ||
        kind.Equals("Procedure", StringComparison.OrdinalIgnoreCase) ||
        pattern.Contains("procedure", StringComparison.OrdinalIgnoreCase) ||
        pattern.Contains("dapper-procedure", StringComparison.OrdinalIgnoreCase) ||
        relation.Contains("EXECUTES", StringComparison.OrdinalIgnoreCase);

    private static bool LooksLikeTunedOrMultiResult(string pattern, string relation) =>
        pattern.Contains("dapper", StringComparison.OrdinalIgnoreCase) ||
        pattern.Contains("multi-result", StringComparison.OrdinalIgnoreCase) ||
        pattern.Contains("set-based", StringComparison.OrdinalIgnoreCase) ||
        pattern.Contains("sqlcommand", StringComparison.OrdinalIgnoreCase) ||
        relation.Contains("WRITES", StringComparison.OrdinalIgnoreCase) &&
        pattern.Contains("bulk", StringComparison.OrdinalIgnoreCase);

    private static bool LooksLikeSimpleCrud(string kind, string pattern, string relation) =>
        kind.Contains("Table", StringComparison.OrdinalIgnoreCase) ||
        pattern.Contains("ef-", StringComparison.OrdinalIgnoreCase) ||
        pattern.Contains("linq", StringComparison.OrdinalIgnoreCase) ||
        relation.Contains("READS", StringComparison.OrdinalIgnoreCase) ||
        relation.Contains("WRITES", StringComparison.OrdinalIgnoreCase);
}
