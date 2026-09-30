using System.Text.RegularExpressions;

namespace MigrationTool.Domain;

public static class SqlProjectClassifier
{
    private static readonly Regex SdkStylePattern = new(
        @"Sdk\s*=\s*[""']Microsoft\.Build\.Sql(?:/[^""']+)?[""']",
        RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Compiled);

    public static SqlProjectKind Classify(string sqlprojContents)
    {
        if (string.IsNullOrWhiteSpace(sqlprojContents))
        {
            return SqlProjectKind.Unknown;
        }

        if (SdkStylePattern.IsMatch(sqlprojContents))
        {
            return SqlProjectKind.SdkStyle;
        }

        if (sqlprojContents.Contains("Microsoft.Data.Tools.Schema.SqlTasks.targets", StringComparison.OrdinalIgnoreCase)
            || sqlprojContents.Contains("OutputType>Database", StringComparison.OrdinalIgnoreCase)
            || sqlprojContents.Contains("<DSP>", StringComparison.OrdinalIgnoreCase))
        {
            return SqlProjectKind.ClassicSsdt;
        }

        return SqlProjectKind.Unknown;
    }

    public static SqlProjectKind ClassifyFile(string sqlprojPath)
    {
        if (!File.Exists(sqlprojPath))
        {
            return SqlProjectKind.Unknown;
        }

        return Classify(File.ReadAllText(sqlprojPath));
    }
}
