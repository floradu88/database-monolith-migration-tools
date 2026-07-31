namespace DbIntelligence.Infrastructure;

public static class ProjectFolderDiscovery
{
    private static readonly HashSet<string> SkipNames = new(StringComparer.OrdinalIgnoreCase)
    {
        ".git", ".svn", ".hg", ".vs", ".idea", ".cursor", ".codegraph",
        "node_modules", "bin", "obj", "dist", "build", "out", "packages",
        "TestResults", "artifacts", "coverage", "__pycache__", ".tmp",
        "graphify-out", "canvases"
    };

    private static readonly string[] ProjectMarkers =
    [
        ".git",
        "*.sln",
        "*.csproj",
        "*.fsproj",
        "*.vbproj",
        "package.json",
        "pyproject.toml",
        "requirements.txt",
        "Cargo.toml",
        "go.mod",
        "pom.xml",
        "build.gradle",
        "build.gradle.kts"
    ];

    public static IReadOnlyList<(string Name, string Path, bool HasMarker)> Discover(
        string parentFolderPath,
        bool requireProjectMarkers,
        IEnumerable<string>? explicitNames = null)
    {
        var parent = Path.GetFullPath(parentFolderPath);
        if (!Directory.Exists(parent))
            throw new DirectoryNotFoundException($"Parent folder not found: {parent}");

        IEnumerable<DirectoryInfo> children = new DirectoryInfo(parent)
            .EnumerateDirectories()
            .Where(d => !SkipNames.Contains(d.Name) && !d.Name.StartsWith('.'));

        if (explicitNames is not null)
        {
            var wanted = new HashSet<string>(
                explicitNames.Where(n => !string.IsNullOrWhiteSpace(n)),
                StringComparer.OrdinalIgnoreCase);
            if (wanted.Count > 0)
                children = children.Where(d => wanted.Contains(d.Name));
        }

        var list = new List<(string Name, string Path, bool HasMarker)>();
        foreach (var dir in children.OrderBy(d => d.Name, StringComparer.OrdinalIgnoreCase))
        {
            var hasMarker = HasAnyProjectMarker(dir.FullName);
            if (requireProjectMarkers && !hasMarker)
                continue;
            list.Add((dir.Name, dir.FullName, hasMarker));
        }

        return list;
    }

    public static bool HasAnyProjectMarker(string projectPath)
    {
        if (Directory.Exists(Path.Combine(projectPath, ".git")))
            return true;

        foreach (var marker in ProjectMarkers)
        {
            if (marker.StartsWith('*'))
            {
                if (Directory.EnumerateFiles(projectPath, marker, SearchOption.TopDirectoryOnly).Any())
                    return true;
            }
            else if (!marker.StartsWith('.') && File.Exists(Path.Combine(projectPath, marker)))
            {
                return true;
            }
        }

        return false;
    }

    public static string ResolveArtifactsDirectory(string projectPath, string? relative)
    {
        if (string.IsNullOrWhiteSpace(relative) || relative.Trim() is "." or "./")
            return Path.GetFullPath(projectPath);

        return Path.GetFullPath(Path.IsPathRooted(relative)
            ? relative
            : Path.Combine(projectPath, relative));
    }
}
