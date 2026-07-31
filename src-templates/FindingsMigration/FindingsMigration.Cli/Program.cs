using FindingsMigration.Contracts;
using FindingsMigration.Core;

static int Usage()
{
    Console.WriteLine("""
        FindingsMigration.Cli — package DbIntelligence JSON maps into domain manifests.

        Usage:
          findings-migrate --code-to-db <path> --domain <Name> [--out <dir>] [options]

        Options:
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
            IncludeAmbiguous = includeAmbiguous
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
