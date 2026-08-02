using DbIntelligence.Domain;

namespace DbIntelligence.Infrastructure;

/// <summary>
/// Helpers for project-prefixed node IDs used when combining multiple project graphs.
/// Format: <c>p:{projectSlug}/{originalId}</c>. DB nodes may stay unprefixed to share across projects.
/// </summary>
public static class ProjectGraphIds
{
    public const string ProjectPropertyKey = "project";

    public static string Prefix(string projectName, string originalId)
    {
        var slug = SanitizeProject(projectName);
        return $"p:{slug}/{originalId}";
    }

    public static string SanitizeProject(string projectName)
    {
        if (string.IsNullOrWhiteSpace(projectName))
            return "project";

        var chars = projectName.Trim().Select(c =>
            char.IsLetterOrDigit(c) || c is '.' or '_' or '-' ? c : '_').ToArray();
        var slug = new string(chars);
        return string.IsNullOrWhiteSpace(slug) ? "project" : slug;
    }

    public static string CoreId(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return id;

        if (!id.StartsWith("p:", StringComparison.OrdinalIgnoreCase))
            return id;

        var slash = id.IndexOf('/');
        return slash > 0 && slash < id.Length - 1 ? id[(slash + 1)..] : id;
    }

    public static string? TryGetProject(string id)
    {
        if (!id.StartsWith("p:", StringComparison.OrdinalIgnoreCase))
            return null;

        var slash = id.IndexOf('/');
        return slash > 2 ? id[2..slash] : null;
    }

    public static bool IsCodeNodeId(string id) =>
        CoreId(id).StartsWith("code:", StringComparison.OrdinalIgnoreCase);

    public static bool IsDbNodeId(string id) =>
        CoreId(id).StartsWith("db:", StringComparison.OrdinalIgnoreCase);

    public static bool ShouldShareAcrossProjects(GraphNode node) =>
        IsDbNodeId(node.Id) ||
        node.Kind is NodeKind.Database or NodeKind.Schema or NodeKind.Table
            or NodeKind.View or NodeKind.StoredProcedure or NodeKind.Function
            or NodeKind.Trigger;
}
