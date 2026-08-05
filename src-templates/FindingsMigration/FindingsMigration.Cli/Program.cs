using FindingsMigration.Contracts;
using FindingsMigration.Core;

static int Usage()
{
    Console.WriteLine("""
        FindingsMigration.Cli — package DbIntelligence JSON maps into domain manifests + SP wrappers.

        Usage:
          findings-migrate --code-to-db <path> --domain <Name> [--out <dir>] [options]
          findings-migrate generate-sp --sp-map <path> --service-root <dir> --domain <Name> --service <Name> [--schema <name>]
          findings-migrate suggest-domains --graph <graph.json> [--min-nodes <n>] [--out <file>]
          findings-migrate confidence-gate --code-to-db <path> --manifests <dir> [--owned-schema <name>] [--ambiguous-baseline <file>] [--review-ack <file>]
          findings-migrate diff-maps --previous <path> --current <path> [--out <file>]
          findings-migrate slice-sql --objects <comma-list> --out <dir> --schema <name> --service <name> [--owner <team>]

        Package options:
          --code-to-db <path>     Required. code-to-db-map.json from DbIntelligence
          --sp-map <path>         Optional. stored-procedure-map.json
          --domain <Name>         Required. Domain name (e.g. Billing, Customer)
          --service <Name>        Optional. Default: {Domain}DataService
          --schema <name>         Optional. Default: lowercased domain
          --source-db <name>      Optional. Default: MonolithDb
          --target-db <name>      Optional. Default: {Domain}Db
          --owner <team>          Optional. Default: TBD
          --out <dir>             Optional. Default: ./out/{domain}
          --include-ambiguous     Include AMBIGUOUS edges in packaged manifests (default: review-only)
          --emit-reconciliation-tests  Write xUnit shadow/reconciliation stub under Tests/ or --service-root
          --service-root <dir>    Optional. Scaffolded DataService root for reconciliation stubs

        generate-sp options:
          --sp-map <path>         Required. stored-procedure-map.json
          --service-root <dir>    Required. Scaffolded DataService root
          --domain <Name>         Required.
          --service <Name>        Required. e.g. InsightDataService
          --schema <name>         Optional. Default: lowercased domain

        suggest-domains options:
          --graph <path>          Required. graph.json (Graphify/DbIntelligence)
          --min-nodes <n>         Optional. Default: 3
          --out <file>            Optional. Write JSON suggestions to file

        confidence-gate options:
          --code-to-db <path>     Required.
          --manifests <dir>       Required. manifests/domains directory
          --owned-schema <name>   Optional. Only gate edges for this schema
          --ambiguous-baseline <file>  Optional. Text file with previous AMBIGUOUS count
          --review-ack <file>     Optional. File containing AMBIGUOUS-ACK when count rises

        diff-maps options:
          --previous <path>       Required. Prior code-to-db-map.json
          --current <path>        Required. Newer code-to-db-map.json
          --out <file>            Optional. Write NEW EXTRACTED edges as a map JSON

        slice-sql options:
          --objects <list>        Required. Comma-separated DB objects (e.g. dbo.Customer,dbo.Order)
          --out <dir>             Required. Output folder for stub .sql + ownership.yml
          --schema <name>         Required. Target schema
          --service <name>        Required. Owning DataService name
          --owner <team>          Optional. Default: TBD
        """);
    return 2;
}

var argsList = args.Select(a => a.Trim()).Where(a => a.Length > 0).ToList();
if (argsList.Count == 0 || argsList.Contains("-h") || argsList.Contains("--help"))
    return Usage();

string? GetOpt(string name)
{
    var i = argsList.FindIndex(a => string.Equals(a, name, StringComparison.OrdinalIgnoreCase));
    if (i < 0 || i + 1 >= argsList.Count) return null;
    return argsList[i + 1];
}

bool HasFlag(string name) =>
    argsList.Any(a => string.Equals(a, name, StringComparison.OrdinalIgnoreCase));

if (string.Equals(argsList[0], "generate-sp", StringComparison.OrdinalIgnoreCase))
{
    var spMapPath = GetOpt("--sp-map");
    var serviceRoot = GetOpt("--service-root");
    var domainSp = GetOpt("--domain");
    var serviceSp = GetOpt("--service");
    var schemaSp = GetOpt("--schema") ?? "";
    if (string.IsNullOrWhiteSpace(spMapPath) || string.IsNullOrWhiteSpace(serviceRoot) ||
        string.IsNullOrWhiteSpace(domainSp) || string.IsNullOrWhiteSpace(serviceSp))
        return Usage();

    try
    {
        var gen = new SpWrapperGenerator();
        var result = gen.GenerateFromMapFile(spMapPath, serviceRoot, domainSp, schemaSp, serviceSp);
        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(result, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        return 0;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine(ex.Message);
        return 1;
    }
}

if (string.Equals(argsList[0], "suggest-domains", StringComparison.OrdinalIgnoreCase))
{
    var graphPath = GetOpt("--graph");
    if (string.IsNullOrWhiteSpace(graphPath))
        return Usage();
    var minNodesText = GetOpt("--min-nodes");
    var minNodes = 3;
    if (!string.IsNullOrWhiteSpace(minNodesText))
        _ = int.TryParse(minNodesText, out minNodes);

    try
    {
        var svc = new DomainSuggestionService();
        var result = svc.SuggestFromGraphFile(graphPath, Math.Max(1, minNodes));
        var json = System.Text.Json.JsonSerializer.Serialize(result, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
        var outFile = GetOpt("--out");
        if (!string.IsNullOrWhiteSpace(outFile))
        {
            var dir = Path.GetDirectoryName(outFile);
            if (!string.IsNullOrWhiteSpace(dir))
                Directory.CreateDirectory(dir);
            File.WriteAllText(outFile, json);
            Console.WriteLine($"Wrote suggestions to {Path.GetFullPath(outFile)}");
        }
        Console.WriteLine(json);
        return 0;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine(ex.Message);
        return 1;
    }
}

if (string.Equals(argsList[0], "confidence-gate", StringComparison.OrdinalIgnoreCase))
{
    var codeToDbGate = GetOpt("--code-to-db");
    var manifests = GetOpt("--manifests");
    if (string.IsNullOrWhiteSpace(codeToDbGate) || string.IsNullOrWhiteSpace(manifests))
        return Usage();

    try
    {
        var gate = new ConfidenceGateService();
        var result = gate.Evaluate(
            codeToDbGate,
            manifests,
            GetOpt("--owned-schema"),
            GetOpt("--ambiguous-baseline"),
            GetOpt("--review-ack"));
        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(result, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        return result.Passed ? 0 : 1;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine(ex.Message);
        return 1;
    }
}

if (string.Equals(argsList[0], "diff-maps", StringComparison.OrdinalIgnoreCase))
{
    var previous = GetOpt("--previous");
    var current = GetOpt("--current");
    if (string.IsNullOrWhiteSpace(previous) || string.IsNullOrWhiteSpace(current))
        return Usage();

    try
    {
        var svc = new CodeToDbDiffService();
        var result = svc.DiffFiles(previous, current);
        var outFile = GetOpt("--out");
        if (!string.IsNullOrWhiteSpace(outFile))
        {
            svc.WriteDiffDocument(result, outFile);
            Console.WriteLine($"Wrote {result.NewExtractedCount} NEW EXTRACTED edge(s) to {Path.GetFullPath(outFile)}");
        }

        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new
        {
            result.PreviousExtractedCount,
            result.CurrentExtractedCount,
            result.NewExtractedCount,
            newExtracted = result.NewExtractedEntries
        }, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        return 0;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine(ex.Message);
        return 1;
    }
}

if (string.Equals(argsList[0], "slice-sql", StringComparison.OrdinalIgnoreCase))
{
    var objects = GetOpt("--objects");
    var outDirSlice = GetOpt("--out");
    var schemaSlice = GetOpt("--schema");
    var serviceSlice = GetOpt("--service");
    if (string.IsNullOrWhiteSpace(objects) || string.IsNullOrWhiteSpace(outDirSlice) ||
        string.IsNullOrWhiteSpace(schemaSlice) || string.IsNullOrWhiteSpace(serviceSlice))
        return Usage();

    try
    {
        var gen = new SqlProjectSliceGenerator();
        var result = gen.GenerateFromCommaList(
            objects,
            outDirSlice,
            schemaSlice,
            serviceSlice,
            GetOpt("--owner"));
        Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(result, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"SQL slice written to: {Path.GetFullPath(outDirSlice)}");
        return 0;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine(ex.Message);
        return 1;
    }
}

var codeToDb = GetOpt("--code-to-db");
var domain = GetOpt("--domain");
if (string.IsNullOrWhiteSpace(codeToDb) || string.IsNullOrWhiteSpace(domain))
    return Usage();

var spMap = GetOpt("--sp-map");
var service = GetOpt("--service");
var schema = GetOpt("--schema");
var sourceDb = GetOpt("--source-db") ?? "MonolithDb";
var targetDb = GetOpt("--target-db");
var owner = GetOpt("--owner") ?? "TBD";
var outDir = GetOpt("--out") ?? Path.Combine(Environment.CurrentDirectory, "out", domain);
var includeAmbiguous = HasFlag("--include-ambiguous");
var emitRecon = HasFlag("--emit-reconciliation-tests");
var serviceRootOpt = GetOpt("--service-root");

try
{
    var builder = new DomainPackageBuilder();
    var result = builder.Build(
        codeToDb,
        spMap,
        outDir,
        new DomainPackageOptions
        {
            DomainName = domain,
            TargetService = service ?? $"{domain}DataService",
            TargetSchema = schema ?? "",
            SourceDatabase = sourceDb,
            TargetDatabase = targetDb ?? "",
            OwnerTeam = owner,
            IncludeAmbiguous = includeAmbiguous,
            EmitReconciliationTests = emitRecon,
            ServiceRoot = serviceRootOpt
        });

    Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(result, new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
    Console.WriteLine($"Package written to: {Path.GetFullPath(outDir)}");
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine(ex.Message);
    return 1;
}
