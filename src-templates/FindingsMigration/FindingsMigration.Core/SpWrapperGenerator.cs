using System.Text;
using System.Text.RegularExpressions;
using BuildingBlocks.DataAccess.Abstractions;
using FindingsMigration.Contracts;

namespace FindingsMigration.Core;

/// <summary>
/// Emits SQL stubs and Dapper SP wrappers from stored-procedure-map.json into a scaffolded DataService.
/// Supports templated names (<c>usp_{Area}_{Action}</c>) expanded via enum/constant token maps.
/// </summary>
public sealed class SpWrapperGenerator
{
    public SpGenerationResult Generate(
        StoredProcedureMapDocument spMap,
        string serviceRoot,
        string domainName,
        string targetSchema,
        string serviceName)
    {
        var schema = string.IsNullOrWhiteSpace(targetSchema)
            ? domainName.ToLowerInvariant()
            : targetSchema;

        var sqlDir = Path.Combine(serviceRoot, $"{serviceName}.Database", "Programmability", "Generated");
        var csDir = Path.Combine(serviceRoot, $"{serviceName}.Infrastructure", "StoredProcedures", "Generated");
        var manifestDir = Path.Combine(serviceRoot, "manifests", "objects");
        Directory.CreateDirectory(sqlDir);
        Directory.CreateDirectory(csDir);
        Directory.CreateDirectory(manifestDir);

        var written = new List<string>();
        foreach (var proc in spMap.Procedures)
        {
            if (string.IsNullOrWhiteSpace(proc.Name) && string.IsNullOrWhiteSpace(proc.NameTemplate))
                continue;

            var template = proc.NameTemplate;
            if (string.IsNullOrWhiteSpace(template) && proc.Name.Contains('{', StringComparison.Ordinal))
                template = proc.Name;

            var resolved = ResolveConcreteNames(proc, template);
            if (resolved.Count == 0)
                continue;

            foreach (var concrete in resolved)
            {
                var leaf = concrete.Contains('.') ? concrete.Split('.').Last() : concrete;
                var safeName = SanitizeIdentifier(leaf);
                var sqlPath = Path.Combine(sqlDir, $"{safeName}.sql");
                File.WriteAllText(sqlPath, BuildSqlStub(schema, proc, leaf, template), Encoding.UTF8);
                written.Add(sqlPath);
                EnsureSqlProjBuildInclude(serviceRoot, serviceName, $"Programmability\\Generated\\{safeName}.sql");

                var snippetPath = Path.Combine(manifestDir, $"{safeName}.migration-manifest.snippet.yml");
                File.WriteAllText(
                    snippetPath,
                    BuildMigrationManifestSnippet(domainName, serviceName, schema, proc, leaf),
                    Encoding.UTF8);
                written.Add(snippetPath);
            }

            var wrapperBase = SanitizeIdentifier(template ?? proc.Name);
            var className = $"Sp_{wrapperBase}";
            var interfaceName = $"I{className}";
            var csPath = Path.Combine(csDir, $"{className}.cs");
            File.WriteAllText(
                csPath,
                BuildWrapper(serviceName, schema, proc, className, interfaceName, template, resolved),
                Encoding.UTF8);
            written.Add(csPath);
        }

        var scaffoldNote = Path.Combine(serviceRoot, "SP-GENERATED.md");
        File.WriteAllText(scaffoldNote, BuildNote(domainName, serviceName, spMap, written), Encoding.UTF8);
        written.Add(scaffoldNote);

        return new SpGenerationResult
        {
            ProcedureCount = spMap.Procedures.Count(p =>
                !string.IsNullOrWhiteSpace(p.Name) || !string.IsNullOrWhiteSpace(p.NameTemplate)),
            WrittenFiles = written
        };
    }

    public SpGenerationResult GenerateFromMapFile(
        string storedProcedureMapPath,
        string serviceRoot,
        string domainName,
        string targetSchema,
        string serviceName)
    {
        if (!File.Exists(storedProcedureMapPath))
            throw new FileNotFoundException("stored-procedure map not found", storedProcedureMapPath);

        var spMap = System.Text.Json.JsonSerializer.Deserialize<StoredProcedureMapDocument>(
            File.ReadAllText(storedProcedureMapPath),
            new System.Text.Json.JsonSerializerOptions { PropertyNameCaseInsensitive = true })
            ?? new StoredProcedureMapDocument();

        return Generate(spMap, serviceRoot, domainName, targetSchema, serviceName);
    }

    private static List<string> ResolveConcreteNames(StoredProcedureEntry proc, string? template)
    {
        if (proc.ResolvedNames is { Count: > 0 })
            return proc.ResolvedNames
                .Where(n => !string.IsNullOrWhiteSpace(n) && !n.Contains('{'))
                .Select(StoredProcedureName.NormalizeProcedureName)
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();

        if (!string.IsNullOrWhiteSpace(template) && template.Contains('{'))
        {
            var tokenMap = (proc.Tokens ?? new Dictionary<string, List<string>>())
                .ToDictionary(
                    kv => kv.Key,
                    kv => (IReadOnlyList<string>)kv.Value,
                    StringComparer.OrdinalIgnoreCase);
            return StoredProcedureName.Expand(template, tokenMap)
                .Where(n => !n.Contains('{'))
                .ToList();
        }

        if (!string.IsNullOrWhiteSpace(proc.Name) && !proc.Name.Contains('{'))
            return [StoredProcedureName.NormalizeProcedureName(proc.Name)];

        return [];
    }

    private static string BuildMigrationManifestSnippet(
        string domainName,
        string serviceName,
        string schema,
        StoredProcedureEntry proc,
        string procedureLeaf)
    {
        var sourceObject = string.IsNullOrWhiteSpace(proc.Schema)
            ? procedureLeaf
            : $"{proc.Schema}.{procedureLeaf}";
        var callers = proc.Callers.Count == 0
            ? "    - TBD"
            : string.Join(Environment.NewLine, proc.Callers.Select(c => $"    - {c}"));

        return $"""
            # Snippet aligned with src-templates/migration-manifest.example.yml — review before commit.
            source:
              server: source-sql
              database: {proc.Database ?? "MonolithDb"}
              schema: {proc.Schema ?? "dbo"}
              object: {sourceObject}
              type: StoredProcedure

            usage:
              applications:
            {callers}
              unknownCallers: false

            ownership:
              targetService: {serviceName}
              targetSchema: {schema}
              confidence: 0.75
              approvedBy: null  # required before cutover

            migration:
              strategy: FacadeThenMove
              targetObject: {schema}.{procedureLeaf}
              synchronization: TBD
              rollback: SwitchFacadeToLegacy
              wave: {domainName.ToLowerInvariant()}-001  # placeholder — assign in migration-waves

            validation:
              - ResultSetComparison
              - NullSemanticsComparison

            domain: {domainName.ToLowerInvariant()}
            status: draft-from-findings
            """;
    }

    private static string BuildSqlStub(string schema, StoredProcedureEntry proc, string procedureLeaf, string? template)
    {
        var reads = proc.Reads.Count == 0 ? "TBD" : string.Join(", ", proc.Reads);
        var writes = proc.Writes.Count == 0 ? "none" : string.Join(", ", proc.Writes);
        var callers = proc.Callers.Count == 0 ? "unknown" : string.Join(", ", proc.Callers);
        var templateLine = string.IsNullOrWhiteSpace(template) ? "" : $"\n            -- Name template: {template}";
        return $"""
            -- Ownership: SqlProject (desired-state Build under Programmability/Generated)
            -- Generated stub from FindingsMigration (FacadeThenMove). DBA must review before deploy.
            -- Source callers: {callers}
            -- Tables read: {reads}
            -- Tables written: {writes}{templateLine}
            -- Cutover up/down waves belong in Cutover/ (not SSDT Build).
            CREATE OR ALTER PROCEDURE [{schema}].[{procedureLeaf}]
            AS
            BEGIN
                SET NOCOUNT ON;
                -- TODO: migrate body from monolith SP, or keep façade on source until Owned cutover.
                RAISERROR('Stub only — not for production.', 16, 1);
            END
            GO
            """;
    }

    private static string BuildWrapper(
        string serviceName,
        string schema,
        StoredProcedureEntry proc,
        string className,
        string interfaceName,
        string? template,
        IReadOnlyList<string> resolvedNames)
    {
        if (!string.IsNullOrWhiteSpace(template) && template.Contains('{'))
            return BuildTemplatedWrapper(serviceName, schema, className, interfaceName, template, resolvedNames);

        var leaf = resolvedNames[0].Contains('.') ? resolvedNames[0].Split('.').Last() : resolvedNames[0];
        var sqlName = $"{schema}.{leaf}";
        return $$"""
            // <auto-generated />
            // FindingsMigration SP wrapper — fluent ExecuteSp<T>().ToListAsync() / ExecuteAsync.
            using BuildingBlocks.DataAccess.Abstractions;

            namespace {{serviceName}}.Infrastructure.StoredProcedures.Generated;

            public interface {{interfaceName}}
            {
                Task<int> ExecuteAsync(string connectionName, CancellationToken cancellationToken = default);
            }

            public sealed class {{className}} : {{interfaceName}}
            {
                public const string ProcedureName = "{{sqlName}}";
                private readonly IDataAccessContext _access;

                public {{className}}(IDataAccessContext access) => _access = access;

                public Task<int> ExecuteAsync(string connectionName, CancellationToken cancellationToken = default) =>
                    _access.ExecuteSp<object>(ProcedureName)
                        .On(connectionName)
                        .Named(ProcedureName)
                        .ExecuteAsync(cancellationToken);
            }
            """;
    }

    private static string BuildTemplatedWrapper(
        string serviceName,
        string schema,
        string className,
        string interfaceName,
        string template,
        IReadOnlyList<string> resolvedNames)
    {
        var known = string.Join(", ", resolvedNames.Select(n => $"\"{schema}.{n.Split('.').Last()}\""));
        return $$"""
            // <auto-generated />
            // Templated SP wrapper — resolve {Token} holes via enums/constants (StoredProcedureName.Format).
            using BuildingBlocks.DataAccess.Abstractions;

            namespace {{serviceName}}.Infrastructure.StoredProcedures.Generated;

            public interface {{interfaceName}}
            {
                string Resolve(params object[] tokenSegments);
                Task<int> ExecuteAsync(string connectionName, CancellationToken cancellationToken = default, params object[] tokenSegments);
            }

            public sealed class {{className}} : {{interfaceName}}
            {
                public const string NameTemplate = "{{template}}";
                public static readonly string[] KnownProcedures = [{{known}}];
                private readonly IDataAccessContext _access;

                public {{className}}(IDataAccessContext access) => _access = access;

                public string Resolve(params object[] tokenSegments)
                {
                    var leaf = StoredProcedureName.Format(NameTemplate, tokenSegments);
                    return "{{schema}}." + leaf.Split('.').Last();
                }

                public Task<int> ExecuteAsync(string connectionName, CancellationToken cancellationToken = default, params object[] tokenSegments)
                {
                    var procedureName = Resolve(tokenSegments);
                    return _access.ExecuteSp<object>(procedureName)
                        .On(connectionName)
                        .Named(procedureName)
                        .ExecuteAsync(cancellationToken);
                }
            }
            """;
    }

    private static string BuildNote(string domain, string service, StoredProcedureMapDocument spMap, List<string> written)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# Generated SP wrappers — {domain}");
        sb.AppendLine();
        sb.AppendLine("Runtime modes (Showcase golden template):");
        sb.AppendLine("- `SourceFacade` / Blue — call source monolith connection");
        sb.AppendLine("- `Owned` / Green — call target owned DB");
        sb.AppendLine("- `Shadow` — compare both on reads; never dual-write");
        sb.AppendLine();
        sb.AppendLine("## Procedures");
        sb.AppendLine();
        foreach (var p in spMap.Procedures)
        {
            var template = p.NameTemplate ?? (p.Name.Contains('{') ? p.Name : null);
            if (!string.IsNullOrWhiteSpace(template))
                sb.AppendLine($"- template `{template}` tokens: {FormatTokens(p)} resolved: {string.Join(", ", p.ResolvedNames ?? [])}");
            else
                sb.AppendLine($"- `{p.Schema}.{p.Name}` callers: {string.Join(", ", p.Callers)}");
        }
        sb.AppendLine();
        sb.AppendLine("## Files");
        sb.AppendLine();
        foreach (var f in written)
            sb.AppendLine($"- `{f}`");
        sb.AppendLine();
        sb.AppendLine("Register generated wrappers in Infrastructure DI.");
        sb.AppendLine("SQL stubs are SqlProject-owned desired state under Programmability/Generated (added to .sqlproj Build when present).");
        sb.AppendLine("Keep Cutover/*.up.sql|*.down.sql as None — do not dual-own with EF migrations.");
        sb.AppendLine();
        sb.AppendLine("Runtime schema/connection: configure the service `Database` section (`Schema`, `OwnedConnectionString`) in one place; regenerate stubs with `--schema` when renaming off dbo.");
        sb.AppendLine("Templated names: map holes to enums/constants (`tokens` in stored-procedure-map.json) so `$\"{ValueA}_{ValueB}\"` call sites expand to concrete SPs.");
        return sb.ToString();
    }

    private static string FormatTokens(StoredProcedureEntry proc)
    {
        if (proc.Tokens is null || proc.Tokens.Count == 0) return "(none — add enum/const token map)";
        return string.Join("; ", proc.Tokens.Select(kv => $"{kv.Key}=[{string.Join(",", kv.Value)}]"));
    }

    private static void EnsureSqlProjBuildInclude(string serviceRoot, string serviceName, string relativeBuildPath)
    {
        var sqlproj = Path.Combine(serviceRoot, $"{serviceName}.Database", $"{serviceName}.Database.sqlproj");
        if (!File.Exists(sqlproj)) return;

        var marker = $@"Build Include=""{relativeBuildPath}""";
        var text = File.ReadAllText(sqlproj);
        if (text.Contains(marker, StringComparison.OrdinalIgnoreCase)) return;

        var insert = $"    <Build Include=\"{relativeBuildPath}\" />{Environment.NewLine}";
        const string anchor = "</ItemGroup>";
        var buildInclude = text.IndexOf("Build Include=", StringComparison.OrdinalIgnoreCase);
        if (buildInclude < 0) return;
        var endOfThatGroup = text.IndexOf(anchor, buildInclude, StringComparison.Ordinal);
        if (endOfThatGroup < 0) return;

        text = text.Insert(endOfThatGroup, insert);
        File.WriteAllText(sqlproj, text, Encoding.UTF8);
    }

    private static string SanitizeIdentifier(string name)
    {
        var cleaned = Regex.Replace(name, @"[^A-Za-z0-9_]", "_");
        cleaned = Regex.Replace(cleaned, "_+", "_").Trim('_');
        if (cleaned.Length == 0) return "Proc";
        if (char.IsDigit(cleaned[0])) cleaned = "_" + cleaned;
        return cleaned;
    }
}

public sealed class SpGenerationResult
{
    public int ProcedureCount { get; init; }
    public List<string> WrittenFiles { get; init; } = [];
}
