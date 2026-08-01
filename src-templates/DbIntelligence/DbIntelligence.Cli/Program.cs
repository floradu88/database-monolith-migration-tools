using DbIntelligence.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

static void PrintHelp()
{
    Console.WriteLine("""
        DbIntelligence CLI

        Usage:
          dbintelligence --health
          dbintelligence --install-preqs [--yes]
          dbintelligence --help

        Commands:
          --health           Check python, pip, graphify, and codegraph availability.
          --install-preqs    Interactively install missing prerequisites (prompts for confirmation).
          --yes              With --install-preqs, answer yes to prompts (non-interactive).

        Graphify requires Python. If Python is missing, --health reports unhealthy and
        --install-preqs can install Python (winget on Windows), pip, graphifyy, and codegraph.
        Codegraph is installed with fnm when available: fnm exec --using=lts-latest -- npm i -g @colbymchenry/codegraph
        (otherwise plain npm i -g, then the official install script).
        """);
}

var argsList = args.Select(a => a.Trim()).Where(a => a.Length > 0).ToList();
var wantsHelp = argsList.Exists(a => a is "-h" or "--help" or "help");
if (wantsHelp)
{
    PrintHelp();
    return 0;
}

if (argsList.Count == 0)
    argsList.Add("--health");

var config = new ConfigurationBuilder()
    .SetBasePath(AppContext.BaseDirectory)
    .AddJsonFile("appsettings.json", optional: true)
    .AddEnvironmentVariables()
    .Build();

var services = new ServiceCollection();
services.AddSingleton<IConfiguration>(config);
services.Configure<DbIntelligenceOptions>(config.GetSection(DbIntelligenceOptions.SectionName));
services.AddLogging(builder => builder.AddSimpleConsole(o =>
{
    o.SingleLine = true;
    o.TimestampFormat = "HH:mm:ss ";
}));
services.AddDbIntelligence();
await using var provider = services.BuildServiceProvider();

var install = argsList.Exists(a => a is "--install-preqs" or "--install-prereqs" or "install-preqs");
var assumeYes = argsList.Exists(a => a is "--yes" or "-y");

if (install)
{
    var installer = provider.GetRequiredService<IPrerequisiteInstaller>();
    return await installer.InstallAsync(assumeYes, Console.In, Console.Out);
}

var health = provider.GetRequiredService<IPrerequisiteHealthService>();
var report = await health.CheckAsync();
Console.WriteLine($"Status: {report.Status}");
Console.WriteLine($"  python:    {(report.Python.Available ? "OK" : "MISSING")} {report.Python.VersionOrDetail ?? report.Python.Remediation}");
Console.WriteLine($"  pip:       {(report.Pip.Available ? "OK" : "MISSING")} {report.Pip.VersionOrDetail ?? report.Pip.Remediation}");
Console.WriteLine($"  graphify:  {(report.Graphify.Available ? "OK" : "MISSING")} {report.Graphify.VersionOrDetail ?? report.Graphify.Remediation}");
Console.WriteLine($"  codegraph: {(report.Codegraph.Available ? "OK" : "MISSING")} {report.Codegraph.VersionOrDetail ?? report.Codegraph.Remediation}");
if (!report.Healthy)
{
    Console.WriteLine();
    Console.WriteLine(report.Message);
    Console.WriteLine(report.InstallHint);
}

return report.Healthy ? 0 : 1;
