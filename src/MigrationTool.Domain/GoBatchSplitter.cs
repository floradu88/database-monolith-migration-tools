using System.Text.RegularExpressions;

namespace MigrationTool.Domain;

public sealed record GoSplitResult(IReadOnlyList<string> Batches, string NormalizedText, bool RemovedGo);

public static partial class GoBatchSplitter
{
    public const string BatchMarker = "-- migration-tool:batch";

    [GeneratedRegex(@"^\s*GO(?:\s+(?<count>\d+))?\s*(?:--.*)?\s*$", RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)]
    private static partial Regex GoLine();

    public static GoSplitResult Split(string sql)
    {
        var text = NormalizeNewlines(sql);
        var lines = text.Split('\n');
        var batches = new List<string>();
        var current = new List<string>();
        var removedGo = false;

        void Flush(int count)
        {
            var batch = string.Join('\n', current).Trim();
            current.Clear();
            if (batch.Length == 0)
            {
                return;
            }

            var times = Math.Max(count, 1);
            for (var i = 0; i < times; i++)
            {
                batches.Add(batch);
            }
        }

        foreach (var line in lines)
        {
            var match = GoLine().Match(line);
            if (match.Success)
            {
                removedGo = true;
                var count = match.Groups["count"].Success
                    ? int.Parse(match.Groups["count"].Value, System.Globalization.CultureInfo.InvariantCulture)
                    : 1;
                Flush(count);
                continue;
            }

            if (line.Trim().Equals(BatchMarker, StringComparison.Ordinal))
            {
                Flush(1);
                continue;
            }

            current.Add(line);
        }

        Flush(1);
        if (batches.Count == 0)
        {
            batches.Add(string.Empty);
        }

        var normalized = string.Join($"\n\n{BatchMarker}\n\n", batches).Trim();
        return new GoSplitResult(batches, normalized, removedGo);
    }

    private static string NormalizeNewlines(string sql) =>
        sql.Replace("\r\n", "\n", StringComparison.Ordinal).Replace('\r', '\n');
}
