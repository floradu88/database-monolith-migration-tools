using System.Text.Json;
using System.Text.RegularExpressions;

namespace FindingsMigration.Core;

/// <summary>
/// CI confidence gate: fail when owned-schema EXTRACTED edges are missing from domain manifests,
/// or when AMBIGUOUS count rises without a review acknowledgement file.
/// Does not auto-approve ownership.
/// </summary>
public sealed class ConfidenceGateService
{
    private static readonly JsonSerializerOptions JsonOptions = new() { PropertyNameCaseInsensitive = true };

    public ConfidenceGateResult Evaluate(
        string codeToDbMapPath,
        string manifestsDomainsDirectory,
        string? ownedSchema = null,
        string? previousAmbiguousBaselinePath = null,
        string? reviewAckPath = null)
    {
        if (!File.Exists(codeToDbMapPath))
            throw new FileNotFoundException("code-to-db-map.json not found", codeToDbMapPath);

        var map = JsonSerializer.Deserialize<CodeToDbGateDocument>(File.ReadAllText(codeToDbMapPath), JsonOptions)
                  ?? new CodeToDbGateDocument();

        var manifestObjects = LoadManifestObjects(manifestsDomainsDirectory);
        var failures = new List<string>();
        var warnings = new List<string>();

        var extracted = (map.Entries ?? [])
            .Where(e => string.Equals(e.Confidence, "EXTRACTED", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(e.Confidence, "Extracted", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var ambiguous = (map.Entries ?? [])
            .Where(e => string.Equals(e.Confidence, "AMBIGUOUS", StringComparison.OrdinalIgnoreCase) ||
                        string.Equals(e.Confidence, "Ambiguous", StringComparison.OrdinalIgnoreCase))
            .ToList();

        foreach (var entry in extracted)
        {
            var db = entry.DbObject ?? "";
            if (!string.IsNullOrWhiteSpace(ownedSchema) &&
                !db.StartsWith(ownedSchema + ".", StringComparison.OrdinalIgnoreCase) &&
                !db.StartsWith("[" + ownedSchema + "]", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            if (!manifestObjects.Any(m => DbObjectMatches(m, db)))
            {
                failures.Add($"EXTRACTED edge missing from domain manifests: {entry.CodeLabel} -> {db}");
            }
        }

        var currentAmbiguous = ambiguous.Count;
        var baseline = 0;
        if (!string.IsNullOrWhiteSpace(previousAmbiguousBaselinePath) && File.Exists(previousAmbiguousBaselinePath))
        {
            var text = File.ReadAllText(previousAmbiguousBaselinePath).Trim();
            _ = int.TryParse(text, out baseline);
        }

        if (currentAmbiguous > baseline)
        {
            var ackOk = !string.IsNullOrWhiteSpace(reviewAckPath) &&
                        File.Exists(reviewAckPath) &&
                        File.ReadAllText(reviewAckPath).Contains("AMBIGUOUS-ACK", StringComparison.OrdinalIgnoreCase);
            if (!ackOk)
            {
                failures.Add(
                    $"AMBIGUOUS count rose from {baseline} to {currentAmbiguous} without review ack " +
                    $"(create a file containing AMBIGUOUS-ACK or update baseline).");
            }
            else
            {
                warnings.Add($"AMBIGUOUS rose {baseline} -> {currentAmbiguous} but review ack present.");
            }
        }

        return new ConfidenceGateResult
        {
            Passed = failures.Count == 0,
            ExtractedCount = extracted.Count,
            AmbiguousCount = currentAmbiguous,
            ManifestObjectCount = manifestObjects.Count,
            Failures = failures,
            Warnings = warnings
        };
    }

    private static HashSet<string> LoadManifestObjects(string? directory)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(directory) || !Directory.Exists(directory))
            return set;

        foreach (var file in Directory.EnumerateFiles(directory, "*.yml", SearchOption.AllDirectories)
                     .Concat(Directory.EnumerateFiles(directory, "*.yaml", SearchOption.AllDirectories)))
        {
            var text = File.ReadAllText(file);
            foreach (Match m in Regex.Matches(text, @"(?m)^\s*-\s*(?<obj>[\w\.\[\]]+)\s*$"))
                set.Add(m.Groups["obj"].Value.Trim());
            foreach (Match m in Regex.Matches(text, @"object:\s*(?<obj>[\w\.\[\]]+)"))
                set.Add(m.Groups["obj"].Value.Trim());
            foreach (Match m in Regex.Matches(text, @"targetObject:\s*(?<obj>[\w\.\[\]]+)"))
                set.Add(m.Groups["obj"].Value.Trim());
        }

        return set;
    }

    private static bool DbObjectMatches(string manifestObject, string dbObject)
    {
        var a = Normalize(manifestObject);
        var b = Normalize(dbObject);
        return a.Equals(b, StringComparison.OrdinalIgnoreCase) ||
               b.EndsWith("." + a, StringComparison.OrdinalIgnoreCase) ||
               a.EndsWith("." + b, StringComparison.OrdinalIgnoreCase);
    }

    private static string Normalize(string value) =>
        value.Replace("[", "", StringComparison.Ordinal).Replace("]", "", StringComparison.Ordinal).Trim();

    private sealed class CodeToDbGateDocument
    {
        public List<CodeToDbGateEntry>? Entries { get; set; }
    }

    private sealed class CodeToDbGateEntry
    {
        public string? CodeLabel { get; set; }
        public string? DbObject { get; set; }
        public string? Confidence { get; set; }
    }
}

public sealed class ConfidenceGateResult
{
    public bool Passed { get; set; }
    public int ExtractedCount { get; set; }
    public int AmbiguousCount { get; set; }
    public int ManifestObjectCount { get; set; }
    public List<string> Failures { get; set; } = [];
    public List<string> Warnings { get; set; } = [];
}
