using System.Text;
using System.Text.Json;
using FindingsMigration.Contracts;

namespace FindingsMigration.Core;

public sealed class DomainPackageBuilder
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        WriteIndented = true
    };

    public DomainPackageResult Build(
        string codeToDbMapPath,
        string? storedProcedureMapPath,
        string outputDirectory,
        DomainPackageOptions options)
    {
        if (!File.Exists(codeToDbMapPath))
            throw new FileNotFoundException("code-to-db map not found", codeToDbMapPath);

        var map = JsonSerializer.Deserialize<CodeToDbMapDocument>(
            File.ReadAllText(codeToDbMapPath), JsonOptions)
            ?? new CodeToDbMapDocument();

        StoredProcedureMapDocument spMap = new();
        if (!string.IsNullOrWhiteSpace(storedProcedureMapPath) && File.Exists(storedProcedureMapPath))
        {
            spMap = JsonSerializer.Deserialize<StoredProcedureMapDocument>(
                File.ReadAllText(storedProcedureMapPath), JsonOptions) ?? new();
        }

        var domain = SanitizeName(options.DomainName);
        var service = string.IsNullOrWhiteSpace(options.TargetService)
            ? $"{domain}DataService"
            : options.TargetService;
        var schema = string.IsNullOrWhiteSpace(options.TargetSchema)
            ? domain.ToLowerInvariant()
            : options.TargetSchema;
        var targetDb = string.IsNullOrWhiteSpace(options.TargetDatabase)
            ? $"{domain}Db"
            : options.TargetDatabase;

        Directory.CreateDirectory(outputDirectory);
        var domainsDir = Path.Combine(outputDirectory, "manifests", "domains");
        var wavesDir = Path.Combine(outputDirectory, "manifests", "migration-waves");
        var objectsDir = Path.Combine(outputDirectory, "manifests", "objects");
        Directory.CreateDirectory(domainsDir);
        Directory.CreateDirectory(wavesDir);
        Directory.CreateDirectory(objectsDir);

        var extracted = map.Entries
            .Where(e => string.Equals(e.Confidence, "EXTRACTED", StringComparison.OrdinalIgnoreCase))
            .ToList();
        var ambiguous = map.Entries
            .Where(e => string.Equals(e.Confidence, "AMBIGUOUS", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var packageEntries = options.IncludeAmbiguous
            ? extracted.Concat(ambiguous).ToList()
            : extracted;

        var written = new List<string>();

        var domainYml = Path.Combine(domainsDir, $"{domain.ToLowerInvariant()}.from-findings.yml");
        File.WriteAllText(domainYml, BuildDomainYaml(domain, service, schema, targetDb, options, packageEntries, spMap), Encoding.UTF8);
        written.Add(domainYml);

        var waveYml = Path.Combine(wavesDir, $"{domain.ToLowerInvariant()}-wave-001.from-findings.yml");
        File.WriteAllText(waveYml, BuildWaveYaml(domain, service, packageEntries, spMap), Encoding.UTF8);
        written.Add(waveYml);

        var objectIndex = 0;
        foreach (var group in packageEntries.GroupBy(e => e.DbObject, StringComparer.OrdinalIgnoreCase))
        {
            objectIndex++;
            var safe = SanitizeFileToken(group.Key);
            var path = Path.Combine(objectsDir, $"{domain.ToLowerInvariant()}-{objectIndex:000}-{safe}.from-findings.yml");
            File.WriteAllText(path, BuildObjectYaml(domain, service, schema, group.Key, group.ToList()), Encoding.UTF8);
            written.Add(path);
        }

        foreach (var proc in spMap.Procedures)
        {
            objectIndex++;
            var name = string.IsNullOrWhiteSpace(proc.Schema) ? proc.Name : $"{proc.Schema}.{proc.Name}";
            var safe = SanitizeFileToken(name);
            var path = Path.Combine(objectsDir, $"{domain.ToLowerInvariant()}-sp-{objectIndex:000}-{safe}.from-findings.yml");
            File.WriteAllText(path, BuildProcedureYaml(domain, service, schema, proc), Encoding.UTF8);
            written.Add(path);
        }

        var reviewPath = Path.Combine(outputDirectory, "FINDINGS-REVIEW.md");
        File.WriteAllText(reviewPath, BuildReviewMarkdown(domain, service, extracted, ambiguous, options.IncludeAmbiguous, spMap), Encoding.UTF8);
        written.Add(reviewPath);

        var apiStubsDir = Path.Combine(outputDirectory, "api-stubs");
        Directory.CreateDirectory(apiStubsDir);
        var apiIndex = Path.Combine(apiStubsDir, "API-STUBS.md");
        File.WriteAllText(apiIndex, BuildApiStubsMarkdown(domain, service, packageEntries), Encoding.UTF8);
        written.Add(apiIndex);
        foreach (var group in packageEntries.GroupBy(e => e.CodeLabel, StringComparer.OrdinalIgnoreCase))
        {
            var safe = SanitizeFileToken(group.Key);
            var stubPath = Path.Combine(apiStubsDir, $"{safe}.md");
            File.WriteAllText(stubPath, BuildApiOperationStub(service, group.Key, group.ToList()), Encoding.UTF8);
            written.Add(stubPath);
        }

        if (options.EmitReconciliationTests)
        {
            var stubRoot = string.IsNullOrWhiteSpace(options.ServiceRoot)
                ? outputDirectory
                : options.ServiceRoot;
            var recon = new ReconciliationTestStubGenerator().Write(stubRoot, domain, service);
            written.AddRange(recon.WrittenFiles);
        }

        var packageJson = Path.Combine(outputDirectory, "domain-package.json");
        var summary = new
        {
            generatedAt = DateTimeOffset.UtcNow,
            domain,
            targetService = service,
            targetSchema = schema,
            targetDatabase = targetDb,
            sourceMaps = new
            {
                codeToDb = Path.GetFullPath(codeToDbMapPath),
                storedProcedures = storedProcedureMapPath is null ? null : Path.GetFullPath(storedProcedureMapPath)
            },
            counts = new
            {
                extracted = extracted.Count,
                ambiguous = ambiguous.Count,
                packaged = packageEntries.Count,
                procedures = spMap.Procedures.Count,
                skippedAmbiguous = options.IncludeAmbiguous ? 0 : ambiguous.Count
            },
            entries = packageEntries,
            procedures = spMap.Procedures
        };
        File.WriteAllText(packageJson, JsonSerializer.Serialize(summary, JsonOptions), Encoding.UTF8);
        written.Add(packageJson);

        var scaffoldHint = Path.Combine(outputDirectory, "SCAFFOLD.md");
        File.WriteAllText(scaffoldHint, BuildScaffoldHint(domain, service, outputDirectory), Encoding.UTF8);
        written.Add(scaffoldHint);

        return new DomainPackageResult
        {
            DomainName = domain,
            TargetService = service,
            ExtractedCount = extracted.Count,
            AmbiguousCount = ambiguous.Count,
            SkippedAmbiguousCount = options.IncludeAmbiguous ? 0 : ambiguous.Count,
            ProcedureCount = spMap.Procedures.Count,
            WrittenFiles = written
        };
    }

    /// <summary>
    /// Writes a reconciliation xUnit stub into <paramref name="serviceRootOrOut"/> (or package out/).
    /// </summary>
    public ReconciliationTestStubResult WriteReconciliationTestStub(
        string serviceRootOrOut,
        string domainName,
        string serviceName) =>
        new ReconciliationTestStubGenerator().Write(serviceRootOrOut, domainName, serviceName);

    private static string BuildDomainYaml(
        string domain,
        string service,
        string schema,
        string targetDb,
        DomainPackageOptions options,
        List<CodeToDbEntry> entries,
        StoredProcedureMapDocument spMap)
    {
        var objects = entries.Select(e => e.DbObject).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList();
        var sb = new StringBuilder();
        sb.AppendLine($"# Generated by FindingsMigration — review before committing ownership.");
        sb.AppendLine($"domain: {domain.ToLowerInvariant()}");
        sb.AppendLine($"owner_team: {YamlEscape(options.OwnerTeam)}");
        sb.AppendLine($"source_database: {YamlEscape(options.SourceDatabase)}");
        sb.AppendLine("source_projects:");
        sb.AppendLine($"  - Monolith.Database.{domain}");
        sb.AppendLine($"target_service: {service}");
        sb.AppendLine($"target_database: {targetDb}");
        sb.AppendLine($"target_schema: {schema}");
        sb.AppendLine("database_change_model: hybrid");
        sb.AppendLine("sql_project_owns:");
        sb.AppendLine("  - schemas");
        sb.AppendLine("  - stored_procedures");
        sb.AppendLine("  - functions");
        sb.AppendLine("  - views");
        sb.AppendLine("  - security");
        sb.AppendLine("ef_migrations_own: []  # fill after human review — do not overlap SQL project");
        sb.AppendLine($"runtime_identity: {service}.Runtime");
        sb.AppendLine($"migration_identity: {service}.Migration");
        sb.AppendLine("candidate_objects:");
        foreach (var o in objects)
            sb.AppendLine($"  - {YamlEscape(o)}");
        if (spMap.Procedures.Count > 0)
        {
            sb.AppendLine("candidate_procedures:");
            foreach (var p in spMap.Procedures)
            {
                var n = string.IsNullOrWhiteSpace(p.Schema) ? p.Name : $"{p.Schema}.{p.Name}";
                sb.AppendLine($"  - {YamlEscape(n)}");
            }
        }
        sb.AppendLine("read_scaling:");
        sb.AppendLine("  replica_safe_operations: []");
        sb.AppendLine("sharding:");
        sb.AppendLine("  enabled: false");
        sb.AppendLine("  candidate_key: TenantId");
        sb.AppendLine("status: draft-from-findings");
        sb.AppendLine("requires_human_ownership_approval: true");
        return sb.ToString();
    }

    private static string BuildWaveYaml(
        string domain,
        string service,
        List<CodeToDbEntry> entries,
        StoredProcedureMapDocument spMap)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Generated wave stub for {domain} — not executable cutover.");
        sb.AppendLine($"wave: {domain.ToLowerInvariant()}-001");
        sb.AppendLine("status: planned");
        sb.AppendLine($"target_service: {service}");
        sb.AppendLine("items:");
        foreach (var o in entries.Select(e => e.DbObject).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x))
        {
            sb.AppendLine($"  - object: {YamlEscape(o)}");
            sb.AppendLine("    strategy: FacadeThenMove");
            sb.AppendLine("    validation: []");
        }
        foreach (var p in spMap.Procedures)
        {
            var n = string.IsNullOrWhiteSpace(p.Schema) ? p.Name : $"{p.Schema}.{p.Name}";
            sb.AppendLine($"  - object: {YamlEscape(n)}");
            sb.AppendLine("    type: StoredProcedure");
            var hasWrites = p.Writes.Count > 0;
            sb.AppendLine(hasWrites
                ? "    strategy: ParallelDboCore"
                : "    strategy: FacadeThenMove");
            if (hasWrites)
            {
                sb.AppendLine("    writes: parallel_dbo_core");
                sb.AppendLine("    tables: delta_only");
                sb.AppendLine("    integrity: evidence");
            }
        }
        return sb.ToString();
    }

    private static string BuildObjectYaml(
        string domain,
        string service,
        string schema,
        string dbObject,
        List<CodeToDbEntry> callers)
    {
        var sb = new StringBuilder();
        sb.AppendLine("source:");
        sb.AppendLine("  server: source-sql");
        sb.AppendLine("  database: MonolithDb");
        sb.AppendLine($"  object: {YamlEscape(dbObject)}");
        sb.AppendLine($"  type: {YamlEscape(callers[0].DbKind)}");
        sb.AppendLine();
        sb.AppendLine("usage:");
        sb.AppendLine("  applications:");
        foreach (var c in callers.Select(x => x.CodeLabel).Distinct(StringComparer.OrdinalIgnoreCase))
            sb.AppendLine($"    - {YamlEscape(c)}");
        sb.AppendLine();
        sb.AppendLine("ownership:");
        sb.AppendLine($"  targetService: {service}");
        sb.AppendLine($"  targetSchema: {schema}");
        sb.AppendLine($"  confidence: {(callers.All(c => c.Confidence.Equals("EXTRACTED", StringComparison.OrdinalIgnoreCase)) ? "0.80" : "0.40")}");
        sb.AppendLine("  approvedBy: null  # required before cutover");
        sb.AppendLine();
        sb.AppendLine("migration:");
        sb.AppendLine("  strategy: FacadeThenMove");
        sb.AppendLine($"  targetObject: {schema}.{dbObject.Split('.').Last()}");
        sb.AppendLine("  synchronization: TBD");
        sb.AppendLine("  rollback: SwitchFacadeToLegacy");
        sb.AppendLine();
        sb.AppendLine("evidence:");
        foreach (var c in callers)
        {
            sb.AppendLine($"  - code: {YamlEscape(c.CodeLabel)}");
            sb.AppendLine($"    file: {YamlEscape(c.SourceFile ?? "")}");
            sb.AppendLine($"    line: {c.Line?.ToString() ?? "null"}");
            sb.AppendLine($"    relation: {c.Relation}");
            sb.AppendLine($"    confidence: {c.Confidence}");
            sb.AppendLine($"    pattern: {YamlEscape(c.Pattern ?? "")}");
            sb.AppendLine($"    data_access_hint: {YamlEscape(DataAccessRecommendation.Recommend(c))}");
        }
        sb.AppendLine();
        sb.AppendLine($"domain: {domain.ToLowerInvariant()}");
        sb.AppendLine("status: draft-from-findings");
        sb.AppendLine($"data_access_hint: {YamlEscape(DataAccessRecommendation.Recommend(callers[0]))}");
        return sb.ToString();
    }

    private static string BuildProcedureYaml(
        string domain,
        string service,
        string schema,
        StoredProcedureEntry proc)
    {
        var name = string.IsNullOrWhiteSpace(proc.Schema) ? proc.Name : $"{proc.Schema}.{proc.Name}";
        var sb = new StringBuilder();
        sb.AppendLine("source:");
        sb.AppendLine("  server: source-sql");
        sb.AppendLine($"  database: {YamlEscape(proc.Database ?? "MonolithDb")}");
        sb.AppendLine($"  object: {YamlEscape(name)}");
        sb.AppendLine("  type: StoredProcedure");
        sb.AppendLine();
        sb.AppendLine("usage:");
        sb.AppendLine("  applications:");
        foreach (var c in proc.Callers.Distinct(StringComparer.OrdinalIgnoreCase))
            sb.AppendLine($"    - {YamlEscape(c)}");
        sb.AppendLine();
        sb.AppendLine("ownership:");
        sb.AppendLine($"  targetService: {service}");
        sb.AppendLine($"  targetSchema: {schema}");
        sb.AppendLine("  confidence: 0.75");
        sb.AppendLine("  approvedBy: null");
        sb.AppendLine();
        sb.AppendLine("migration:");
        sb.AppendLine("  strategy: FacadeThenMove");
        sb.AppendLine($"  targetObject: {schema}.{proc.Name}");
        sb.AppendLine();
        sb.AppendLine("tables_read:");
        foreach (var t in proc.Reads)
            sb.AppendLine($"  - {YamlEscape(t)}");
        sb.AppendLine("tables_written:");
        foreach (var t in proc.Writes)
            sb.AppendLine($"  - {YamlEscape(t)}");
        if (proc.Writes.Count > 0)
        {
            sb.AppendLine("dual_write:");
            sb.AppendLine("  topology: same_database");
            sb.AppendLine("  source_schema: dbo");
            sb.AppendLine("  owned_schema: core");
            sb.AppendLine("  invocation: parallel_independent_sps");
            sb.AppendLine("  history: delta_only");
            sb.AppendLine("  coverage: stored_procedure_writes_only");
            sb.AppendLine("  dbo_extras: expected");
            sb.AppendLine("  mismatch: evidence_only");
            sb.AppendLine("  integrity_rule: core_subset_of_dbo");
            sb.AppendLine("  integrity_proc: core.usp_TableIntegrity_Check");
        }
        sb.AppendLine($"domain: {domain.ToLowerInvariant()}");
        sb.AppendLine("status: draft-from-findings");
        return sb.ToString();
    }

    private static string BuildReviewMarkdown(
        string domain,
        string service,
        List<CodeToDbEntry> extracted,
        List<CodeToDbEntry> ambiguous,
        bool includedAmbiguous,
        StoredProcedureMapDocument spMap)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Findings review — {domain} → {service}");
        sb.AppendLine();
        sb.AppendLine($"Generated: {DateTimeOffset.UtcNow:O}");
        sb.AppendLine();
        sb.AppendLine("| Metric | Count |");
        sb.AppendLine("|--------|------:|");
        sb.AppendLine($"| EXTRACTED packaged | {extracted.Count} |");
        sb.AppendLine($"| AMBIGUOUS {(includedAmbiguous ? "included" : "held for review")} | {ambiguous.Count} |");
        sb.AppendLine($"| Stored procedures | {spMap.Procedures.Count} |");
        sb.AppendLine();
        sb.AppendLine("## Required human approvals");
        sb.AppendLine();
        sb.AppendLine("- [ ] Domain owner confirms `candidate_objects`");
        sb.AppendLine("- [ ] DBA reviews SQL project vs EF ownership split");
        sb.AppendLine("- [ ] Security reviews runtime/migration identities");
        sb.AppendLine("- [ ] AMBIGUOUS edges triaged (accept, reject, or rewrite evidence)");
        sb.AppendLine();
        sb.AppendLine("## Data access hints (docs/07-data-access-strategy.md)");
        sb.AppendLine();
        if (extracted.Count == 0)
            sb.AppendLine("_No EXTRACTED operations._");
        else
        {
            foreach (var group in extracted.GroupBy(e => e.CodeLabel, StringComparer.OrdinalIgnoreCase).OrderBy(g => g.Key))
            {
                var sample = group.First();
                sb.AppendLine($"- `{group.Key}` → `{sample.DbObject}`: {DataAccessRecommendation.Recommend(sample)}");
            }
        }
        sb.AppendLine();
        sb.AppendLine("## AMBIGUOUS queue");
        sb.AppendLine();
        if (ambiguous.Count == 0)
            sb.AppendLine("_None._");
        else
        {
            foreach (var e in ambiguous)
            {
                sb.AppendLine($"- `{e.CodeLabel}` —{e.Relation}→ `{e.DbObject}` ({e.Pattern}) @ `{e.SourceFile}:{e.Line}` — {DataAccessRecommendation.Recommend(e)}");
            }
        }
        sb.AppendLine();
        sb.AppendLine("## Next step");
        sb.AppendLine();
        sb.AppendLine("Run `scripts/New-DomainFromFindings.ps1` to scaffold from the **ShowcaseDataService** golden template, then copy reviewed manifests into kit `manifests/`.");
        return sb.ToString();
    }

    private static string BuildApiStubsMarkdown(
        string domain,
        string service,
        List<CodeToDbEntry> entries)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# API operation stubs — {domain} → {service}");
        sb.AppendLine();
        sb.AppendLine("Advisory DAL hints from `docs/07-data-access-strategy.md`. Not runtime routing.");
        sb.AppendLine();
        foreach (var group in entries.GroupBy(e => e.CodeLabel, StringComparer.OrdinalIgnoreCase).OrderBy(g => g.Key))
        {
            var sample = group.First();
            sb.AppendLine($"## `{group.Key}`");
            sb.AppendLine();
            sb.AppendLine($"- DB: `{sample.DbObject}` ({sample.DbKind})");
            sb.AppendLine($"- Relation: {sample.Relation}");
            sb.AppendLine($"- Pattern: {sample.Pattern}");
            sb.AppendLine($"- {DataAccessRecommendation.Recommend(sample)}");
            sb.AppendLine();
        }
        return sb.ToString();
    }

    private static string BuildApiOperationStub(string service, string codeLabel, List<CodeToDbEntry> callers)
    {
        var sample = callers[0];
        var sb = new StringBuilder();
        sb.AppendLine($"# API stub: {codeLabel}");
        sb.AppendLine();
        sb.AppendLine($"Target service: {service}");
        sb.AppendLine($"DB object: {sample.DbObject}");
        sb.AppendLine($"Kind: {sample.DbKind}");
        sb.AppendLine($"Relation: {sample.Relation}");
        sb.AppendLine($"Pattern: {sample.Pattern}");
        sb.AppendLine();
        sb.AppendLine(DataAccessRecommendation.Recommend(sample));
        sb.AppendLine();
        sb.AppendLine("Call sites:");
        foreach (var c in callers)
            sb.AppendLine($"- `{c.SourceFile}:{c.Line}` confidence={c.Confidence}");
        return sb.ToString();
    }

    private static string BuildScaffoldHint(string domain, string service, string outputDirectory) =>
        $"""
        # Scaffold next step

        Package output: `{outputDirectory}`

        From kit root (PowerShell):

        ```powershell
        cd src-templates\FindingsMigration
        .\scripts\New-DomainFromFindings.ps1 `
          -DomainName "{domain}" `
          -PackageDirectory "{outputDirectory}" `
          -CopyManifestsToKit
        ```

        This copies `DataServices/ShowcaseDataService` (golden) → `DataServices/{service}` with name replacements.
        Pass `-StoredProcedureMap` (or place `stored-procedure-map.json` in the package folder) to emit SQL stubs + Dapper `Sp_*` wrappers.
        Pass `-ParallelDboCore` to emit dbo→core table clones, core SP stubs, and ParallelWrite wrappers.
        It does **not** register ownership or run SQL.

        Agent playbook: given `code-to-db-map.json` / SP map → package → scaffold Showcase → generate-sp `--parallel-dbo-core` → wire façade (SourceFacade/Owned/Shadow/ParallelWrite) → open blue+green → run shadow + table integrity → present `/` dashboard.
        """;

    private static string SanitizeName(string name)
    {
        var chars = name.Where(ch => char.IsLetterOrDigit(ch)).ToArray();
        if (chars.Length == 0) return "Domain";
        var s = new string(chars);
        return char.ToUpperInvariant(s[0]) + s[1..];
    }

    private static string SanitizeFileToken(string value)
    {
        var sb = new StringBuilder();
        foreach (var ch in value)
            sb.Append(char.IsLetterOrDigit(ch) ? ch : '-');
        var s = sb.ToString().Trim('-');
        return string.IsNullOrEmpty(s) ? "object" : s.ToLowerInvariant();
    }

    private static string YamlEscape(string value) =>
        value.Contains(':') || value.Contains('#') || value.Contains('"') || value.Contains('\'')
            ? $"\"{value.Replace("\"", "\\\"")}\""
            : value;
}
