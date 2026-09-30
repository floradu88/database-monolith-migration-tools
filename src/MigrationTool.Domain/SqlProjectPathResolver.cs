namespace MigrationTool.Domain;

public static class SqlProjectPathResolver
{
    public static string Resolve(string pathOrFolder)
    {
        if (string.IsNullOrWhiteSpace(pathOrFolder))
        {
            throw new ArgumentException("SQL project path is required.", nameof(pathOrFolder));
        }

        var fullPath = Path.GetFullPath(pathOrFolder);
        if (!Path.Exists(fullPath))
        {
            throw new FileNotFoundException($"Path not found: {fullPath}", fullPath);
        }

        if (File.Exists(fullPath))
        {
            if (!fullPath.EndsWith(".sqlproj", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException($"Expected a .sqlproj file: {fullPath}", nameof(pathOrFolder));
            }

            return fullPath;
        }

        var projects = Directory.GetFiles(fullPath, "*.sqlproj", SearchOption.TopDirectoryOnly);
        if (projects.Length == 0)
        {
            throw new FileNotFoundException($"No .sqlproj under folder: {fullPath}", fullPath);
        }

        if (projects.Length > 1)
        {
            throw new InvalidOperationException(
                $"Multiple .sqlproj files under {fullPath}. Pass one file path.");
        }

        return projects[0];
    }
}
