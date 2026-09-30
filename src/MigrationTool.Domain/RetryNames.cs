using System.Globalization;

namespace MigrationTool.Domain;

public static class RetryNames
{
    public static string FailureScriptName(int attempt, string fileName) => $"{attempt:000}_{fileName}";

    public static string FailureErrorName(int attempt, string fileName)
    {
        var stem = Path.GetFileNameWithoutExtension(fileName);
        return $"{attempt:000}_{stem}.error.txt";
    }

    public static string FailureDiagnosticsName(int attempt, string fileName)
    {
        var stem = Path.GetFileNameWithoutExtension(fileName);
        return $"{attempt:000}_{stem}.diagnostics.txt";
    }

    public static int NextAttempt(IEnumerable<string> failedSqlFileNames, string fileName)
    {
        var max = 0;
        foreach (var name in failedSqlFileNames)
        {
            if (TryReadAttempt(name, fileName, out var attempt))
            {
                max = Math.Max(max, attempt);
            }
        }

        return max + 1;
    }

    public static bool TryReadAttempt(string failedFileName, string originalFileName, out int attempt)
    {
        attempt = 0;
        if (failedFileName.Length < originalFileName.Length + 4 || failedFileName[3] != '_')
        {
            return false;
        }

        if (!int.TryParse(failedFileName[..3], NumberStyles.None, CultureInfo.InvariantCulture, out attempt))
        {
            return false;
        }

        return failedFileName[4..] == originalFileName;
    }
}
