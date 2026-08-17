using System.Text;
using System.Text.RegularExpressions;
using FindingsMigration.Contracts;

namespace FindingsMigration.Core;

public sealed class SpGenerationOptions
{
    public bool ParallelDboCore { get; init; }
    public string SourceSchema { get; init; } = "dbo";
    public string OwnedSchema { get; init; } = "core";
}

/// <summary>
/// Emits dbo→core table clones, core SP stubs, cutover register scripts, and ParallelWrite C# wrappers.
/// </summary>
public static class DualWriteArtifactGenerator
{
    public static void Emit(
        StoredProcedureMapDocument spMap,
        string serviceRoot,
        string serviceName,
        SpGenerationOptions options,
        List<string> written)
    {
        if (!options.ParallelDboCore) return;

        var sqlDir = Path.Combine(serviceRoot, $"{serviceName}.Database", "Programmability", "Generated");
        var tableDir = Path.Combine(serviceRoot, $"{serviceName}.Database", "Tables", "Generated");
        var cutoverDir = Path.Combine(serviceRoot, $"{serviceName}.Database", "Cutover");
        var csDir = Path.Combine(serviceRoot, $"{serviceName}.Infrastructure", "StoredProcedures", "Generated");
        Directory.CreateDirectory(sqlDir);
        Directory.CreateDirectory(tableDir);
        Directory.CreateDirectory(cutoverDir);
        Directory.CreateDirectory(csDir);

        var tables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var writeProcs = new List<StoredProcedureEntry>();
        foreach (var proc in spMap.Procedures)
        {
            if (proc.Writes.Count == 0) continue;
            writeProcs.Add(proc);
            foreach (var t in proc.Writes)
                tables.Add(t);
        }

        foreach (var table in tables.OrderBy(t => t, StringComparer.OrdinalIgnoreCase))
        {
            var leaf = TableLeaf(table);
            var path = Path.Combine(tableDir, $"{options.OwnedSchema}.{leaf}.sql");
            File.WriteAllText(path, BuildTableClone(options, leaf, table), Encoding.UTF8);
            written.Add(path);
        }

        foreach (var proc in writeProcs)
        {
            var leaf = ProcedureLeaf(proc);
            var sqlPath = Path.Combine(sqlDir, $"{options.OwnedSchema}.{leaf}.sql");
            File.WriteAllText(sqlPath, BuildCoreSpStub(options, proc, leaf), Encoding.UTF8);
            written.Add(sqlPath);
        }

        var cutoverUp = Path.Combine(cutoverDir, "003_register_dbo_core_pairs.up.sql");
        var cutoverDown = Path.Combine(cutoverDir, "003_register_dbo_core_pairs.down.sql");
        File.WriteAllText(cutoverUp, BuildCutoverUp(options, writeProcs, tables), Encoding.UTF8);
        File.WriteAllText(cutoverDown, BuildCutoverDown(), Encoding.UTF8);
        written.Add(cutoverUp);
        written.Add(cutoverDown);

        var csPath = Path.Combine(csDir, "ParallelDboCoreWriter.cs");
        File.WriteAllText(csPath, BuildParallelWriter(serviceName, options, writeProcs), Encoding.UTF8);
        written.Add(csPath);

        var playbook = Path.Combine(serviceRoot, "DBO-CORE-PARALLEL-WRITE.md");
        File.WriteAllText(playbook, BuildPlaybook(serviceName, options, writeProcs, tables), Encoding.UTF8);
        written.Add(playbook);
    }

    private static string TableLeaf(string qualified)
    {
        var leaf = qualified.Contains('.') ? qualified.Split('.').Last() : qualified;
        return Sanitize(leaf);
    }

    private static string ProcedureLeaf(StoredProcedureEntry proc)
    {
        var name = string.IsNullOrWhiteSpace(proc.NameTemplate) ? proc.Name : proc.NameTemplate.Replace("{", "").Replace("}", "");
        var leaf = name.Contains('.') ? name.Split('.').Last() : name;
        return Sanitize(leaf);
    }

    private static string BuildTableClone(SpGenerationOptions options, string leaf, string source)
        => $"""
            -- Ownership: SqlProject. Generated clone of {source} → [{options.OwnedSchema}].[{leaf}]
            -- DBA: replace this stub with output of core.usp_EmitCloneTableDdl (sql/common/41). No data copy (delta-only).
            -- CREATE TABLE [{options.OwnedSchema}].[{leaf}] ( /* same columns as {source}; business key required */ );
            SELECT N'Replace this stub with reviewed clone DDL for {source}.' AS Instruction;
            GO
            """;

    private static string BuildCoreSpStub(SpGenerationOptions options, StoredProcedureEntry proc, string leaf)
    {
        var writes = string.Join(", ", proc.Writes);
        return $"""
            -- Ownership: SqlProject. core clone of {proc.Schema ?? options.SourceSchema}.{proc.Name}
            -- Same parameters as dbo. Writes: {writes}
            -- Point every table at [{options.OwnedSchema}].* — do not write dbo from this procedure.
            CREATE OR ALTER PROCEDURE [{options.OwnedSchema}].[{leaf}]
            AS
            BEGIN
                SET NOCOUNT ON;
                RAISERROR('Stub only — copy reviewed dbo body and qualify tables as {options.OwnedSchema}. Not for production.', 16, 1);
            END
            GO
            """;
    }

    private static string BuildCutoverUp(
        SpGenerationOptions options,
        List<StoredProcedureEntry> procs,
        HashSet<string> tables)
    {
        var sb = new StringBuilder();
        sb.AppendLine("-- Generated. DBA review. Delta-only register (no backfill).");
        sb.AppendLine("PRINT 'Register dbo→core DualWritePair rows';");
        sb.AppendLine("GO");
        var i = 0;
        foreach (var table in tables.OrderBy(t => t, StringComparer.OrdinalIgnoreCase))
        {
            i++;
            var leaf = TableLeaf(table);
            var proc = procs.FirstOrDefault(p => p.Writes.Any(w => w.Equals(table, StringComparison.OrdinalIgnoreCase)));
            var spLeaf = proc is null ? leaf : ProcedureLeaf(proc);
            sb.AppendLine($"""
                IF OBJECT_ID(N'[{options.OwnedSchema}].[usp_RegisterDualWritePair]', N'P') IS NOT NULL
                EXEC [{options.OwnedSchema}].[usp_RegisterDualWritePair]
                    @PairName = N'{leaf}',
                    @SourceSchema = N'{options.SourceSchema}',
                    @SourceTable = N'{leaf}',
                    @TargetSchema = N'{options.OwnedSchema}',
                    @TargetTable = N'{leaf}',
                    @SourceProcedure = N'{spLeaf}',
                    @TargetProcedure = N'{spLeaf}',
                    @BusinessKeyColumns = N'TBD_BUSINESS_KEY',
                    @CompareColumns = N'TBD_COMPARE_COLUMNS',
                    @WatermarkColumn = N'UpdatedAt';
                GO
                """);
        }

        return sb.ToString();
    }

    private static string BuildCutoverDown() =>
        """
        PRINT 'Disable generated DualWritePair rows (retain core for investigation)';
        GO
        IF OBJECT_ID(N'[core].[DualWritePair]', N'U') IS NOT NULL
            UPDATE [core].[DualWritePair] SET [Enabled] = 0 WHERE [Enabled] = 1;
        GO
        """;

    private static string BuildParallelWriter(string serviceName, SpGenerationOptions options, List<StoredProcedureEntry> procs)
    {
        var examples = string.Join(", ", procs.Select(p => ProcedureLeaf(p)).Take(5));
        return $$"""
            // <auto-generated />
            // Fan-out dbo + {{options.OwnedSchema}} stored procedures via ParallelWriteExecutor.
            // dbo is the caller result; {{options.OwnedSchema}} failures are evidence only.
            using BuildingBlocks.DataAccess.Abstractions;
            using BuildingBlocks.Migration;

            namespace {{serviceName}}.Infrastructure.StoredProcedures.Generated;

            public sealed class ParallelDboCoreWriter
            {
                public const string SourceSchema = "{{options.SourceSchema}}";
                public const string OwnedSchema = "{{options.OwnedSchema}}";
                // Example procedures: {{examples}}
                private readonly IDataAccessContext _access;
                private readonly IParallelWriteExecutor _executor;
                private readonly MigrationRoutingOptions _routing;

                public ParallelDboCoreWriter(
                    IDataAccessContext access,
                    IParallelWriteExecutor executor,
                    Microsoft.Extensions.Options.IOptions<MigrationRoutingOptions> routing)
                {
                    _access = access;
                    _executor = executor;
                    _routing = routing.Value;
                }

                public Task<ParallelWriteCallResult> ExecuteAsync(
                    string operation,
                    string businessKey,
                    string procedureLeaf,
                    object parameters,
                    CancellationToken cancellationToken = default)
                {
                    var dbo = SourceSchema + "." + procedureLeaf;
                    var core = OwnedSchema + "." + procedureLeaf;
                    return _executor.ExecuteAsync(
                        operation,
                        businessKey,
                        ct => _access.ExecuteSp<object>(dbo).On("Owned").WithParameters(parameters).Named(operation).ExecuteAsync(ct),
                        ct => _access.ExecuteSp<object>(core).On("Owned").WithParameters(parameters).Named(operation).ExecuteAsync(ct),
                        _routing.ParallelWriteCoreTimeoutMs,
                        cancellationToken);
                }
            }
            """;
    }

    private static string BuildPlaybook(
        string service,
        SpGenerationOptions options,
        List<StoredProcedureEntry> procs,
        HashSet<string> tables)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"# dbo → {options.OwnedSchema} parallel write — {service}");
        sb.AppendLine();
        sb.AppendLine("Same database. dbo SP writes dbo tables; core SP writes core tables; app calls both in parallel (`DataAccessRoute.ParallelWrite`).");
        sb.AppendLine("Coverage is **stored-procedure writes only**. dbo may have more rows (history, EF, jobs, ad-hoc SQL). core must only contain SP-written rows, and each of those must exist in dbo.");
        sb.AppendLine("Integrity: `core EXCEPT dbo` empty = match. Extra dbo rows are expected and are not a fail.");
        sb.AppendLine();
        sb.AppendLine("## Tables to clone (sql/common/41)");
        foreach (var t in tables.OrderBy(x => x))
            sb.AppendLine($"- `{t}` → `{options.OwnedSchema}.{TableLeaf(t)}`");
        sb.AppendLine();
        sb.AppendLine("## Procedures");
        foreach (var p in procs)
            sb.AppendLine($"- `{p.Schema ?? options.SourceSchema}.{p.Name}` → `{options.OwnedSchema}.{ProcedureLeaf(p)}` writes: {string.Join(", ", p.Writes)}");
        sb.AppendLine();
        sb.AppendLine("## Operator steps");
        sb.AppendLine("1. Deploy `sql/common/40`–`45` (DBA review).");
        sb.AppendLine("2. Replace table stubs with `core.usp_EmitCloneTableDdl` output.");
        sb.AppendLine("3. Copy reviewed dbo SP bodies into core stubs; qualify tables as core.");
        sb.AppendLine("4. Set business-key / compare columns in Cutover `003_register_dbo_core_pairs.up.sql`.");
        sb.AppendLine("5. Wire `X-Data-Access-Route: ParallelWrite`.");
        sb.AppendLine("6. Watch dashboard parallel-write + integrity panels; cutover gate is mismatch rate 0.");
        sb.AppendLine();
        sb.AppendLine("Register `ParallelDboCoreWriter` and `IParallelWriteExecutor` in Infrastructure DI (Showcase already does).");
        return sb.ToString();
    }

    private static string Sanitize(string name)
    {
        var cleaned = Regex.Replace(name, @"[^A-Za-z0-9_]", "_");
        cleaned = Regex.Replace(cleaned, "_+", "_").Trim('_');
        return cleaned.Length == 0 ? "Object" : cleaned;
    }
}
