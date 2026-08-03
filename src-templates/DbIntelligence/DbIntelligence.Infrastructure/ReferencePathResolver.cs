using DbIntelligence.Contracts;
using DbIntelligence.Domain;

namespace DbIntelligence.Infrastructure;

public static class ReferencePathResolver
{
    public static string? ResolveFullPath(string? path, string? repositoryRoot)
    {
        if (string.IsNullOrWhiteSpace(path))
            return null;

        try
        {
            if (Path.IsPathRooted(path))
                return Path.GetFullPath(path);

            if (!string.IsNullOrWhiteSpace(repositoryRoot))
                return Path.GetFullPath(Path.Combine(repositoryRoot, path));

            return Path.GetFullPath(path);
        }
        catch
        {
            return path;
        }
    }

    public static string? ToRelativePath(string? fullPath, string? repositoryRoot)
    {
        if (string.IsNullOrWhiteSpace(fullPath) || string.IsNullOrWhiteSpace(repositoryRoot))
            return null;

        try
        {
            var full = Path.GetFullPath(fullPath);
            var root = Path.GetFullPath(repositoryRoot);
            if (!full.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                return null;

            var rel = full[root.Length..].TrimStart(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            return string.IsNullOrWhiteSpace(rel) ? null : rel;
        }
        catch
        {
            return null;
        }
    }

    public static string FormatLocation(string? fullPath, int? line)
    {
        if (string.IsNullOrWhiteSpace(fullPath))
            return line is null ? "" : $":{line}";
        return line is null ? fullPath : $"{fullPath}:{line}";
    }

    public static CodeReferenceLocationDto ToLocationDto(
        string? file,
        int? line,
        string? repositoryRoot,
        string? codeNodeId = null,
        string? codeLabel = null,
        string? dbObject = null,
        string? dbKind = null,
        string? relation = null,
        string? confidence = null,
        string? pattern = null,
        string? rawReference = null,
        string? project = null)
    {
        var full = ResolveFullPath(file, repositoryRoot) ?? file ?? "";
        return new CodeReferenceLocationDto
        {
            FullPath = full,
            RelativePath = ToRelativePath(full, repositoryRoot) ?? (Path.IsPathRooted(file ?? "") ? null : file),
            Line = line,
            Location = FormatLocation(full, line),
            CodeNodeId = codeNodeId,
            CodeLabel = codeLabel,
            DbObject = dbObject,
            DbKind = dbKind,
            Relation = relation,
            Confidence = confidence,
            Pattern = pattern,
            RawReference = rawReference,
            Project = project
        };
    }
}
