using Hangfire;
using MigrationTool.Application;
using MigrationTool.Domain;
using MigrationTool.Host;
using MigrationTool.Infrastructure;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

builder.Host.UseSerilog((context, configuration) =>
    configuration.ReadFrom.Configuration(context.Configuration).WriteTo.Console());

builder.WebHost.UseUrls(builder.Configuration["Urls"] ?? "http://127.0.0.1:5088");

builder.Services.AddMigrationTool(builder.Configuration);
builder.Services.AddHangfire(configuration => configuration.UseInMemoryStorage());
builder.Services.AddHangfireServer();
builder.Services.AddSingleton<ScriptPollJob>();

var app = builder.Build();

app.UseHangfireDashboard("/hangfire", new DashboardOptions
{
    Authorization = [new LocalhostDashboardAuthorizationFilter()]
});

app.MapGet("/", () => Results.Text("Migration tool is running. Hangfire dashboard: /hangfire"));

if (args.Contains("--validate-connection"))
{
    var options = app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<MigrationOptions>>().Value;
    var connection = GetFlagValue(args, "--connection")
        ?? Environment.GetEnvironmentVariable("MIGRATION_CONNECTION_STRING")
        ?? options.ConnectionString;
    var result = await app.Services.GetRequiredService<IConnectionValidator>()
        .ValidateAsync(connection, options.Provider, options.CommandTimeoutSeconds, CancellationToken.None);
    if (result.Succeeded)
    {
        Log.Information(
            "Connection validation succeeded. Server={Server}, Database={Database}",
            result.Shape.Server,
            result.Shape.Database);
    }
    else
    {
        Log.Error("Connection validation failed: {Error}", result.Error);
        Environment.ExitCode = 1;
    }

    return;
}

if (args.Contains("--dacpac-prereqs"))
{
    var report = app.Services.GetRequiredService<IDacpacBuilder>().GetPrerequisites();
    Console.WriteLine(report.Format());
    Environment.ExitCode = report.CanBuildSdkStyle ? 0 : 1;
    return;
}

if (TryGetFlagValue(args, "--build-dacpac", out var sqlProjectPath))
{
    var configuration = GetFlagValue(args, "--configuration") ?? "Debug";
    var output = GetFlagValue(args, "--output");
    var intermediate = GetFlagValue(args, "--intermediate");
    using var scope = app.Services.CreateScope();
    var result = await scope.ServiceProvider.GetRequiredService<IDacpacBuilder>()
        .BuildFromSqlProjectAsync(
            new DacpacBuildRequest(sqlProjectPath!, configuration, output, intermediate),
            CancellationToken.None);
    LogDacpacResult(result);
    Environment.ExitCode = result.Succeeded ? 0 : 1;
    return;
}

if (TryGetFlagValue(args, "--extract-dacpac", out var extractOutput))
{
    var options = app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<MigrationOptions>>().Value;
    var connection = GetFlagValue(args, "--connection")
        ?? Environment.GetEnvironmentVariable("MIGRATION_CONNECTION_STRING")
        ?? options.ConnectionString;
    using var scope = app.Services.CreateScope();
    var result = await scope.ServiceProvider.GetRequiredService<IDacpacBuilder>()
        .ExtractFromDatabaseAsync(
            new DacpacExtractRequest(connection, extractOutput!),
            CancellationToken.None);
    LogDacpacResult(result);
    Environment.ExitCode = result.Succeeded ? 0 : 1;
    return;
}

if (TryGetFlagValue(args, "--publish-dacpac", out var publishDacpacPath))
{
    var options = app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<MigrationOptions>>().Value;
    var connection = GetFlagValue(args, "--connection")
        ?? Environment.GetEnvironmentVariable("MIGRATION_CONNECTION_STRING")
        ?? options.ConnectionString;
    var scriptOnly = args.Contains("--script-only", StringComparer.OrdinalIgnoreCase);
    var allowDataLoss = args.Contains("--allow-data-loss", StringComparer.OrdinalIgnoreCase);
    var scriptPath = GetFlagValue(args, "--script-output");
    using var scope = app.Services.CreateScope();
    var result = await scope.ServiceProvider.GetRequiredService<IDacpacBuilder>()
        .PublishAsync(
            new DacpacPublishRequest(
                publishDacpacPath!,
                connection,
                BlockOnPossibleDataLoss: !allowDataLoss,
                ScriptOnly: scriptOnly,
                DeployScriptPath: scriptPath),
            CancellationToken.None);
    LogDacpacResult(result);
    Environment.ExitCode = result.Succeeded ? 0 : 1;
    return;
}

if (args.Contains("--poll-once"))
{
    using var scope = app.Services.CreateScope();
    var summary = await scope.ServiceProvider
        .GetRequiredService<IScriptPoller>()
        .PollAsync(CancellationToken.None);
    Log.Information(
        "Poll once finished. Processed {Processed}, succeeded {Succeeded}, failed {Failed}, repaired {Repaired}, connectionSkipped {ConnectionSkipped}",
        summary.Processed,
        summary.Succeeded,
        summary.Failed,
        summary.Repaired,
        summary.ConnectionSkipped);
    return;
}

var pollOptions = app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<MigrationOptions>>().Value;
app.Services.GetRequiredService<IRecurringJobManager>().AddOrUpdate<ScriptPollJob>(
    "migration-script-poll",
    job => job.RunAsync(),
    pollOptions.PollCron);

app.Run();

static void LogDacpacResult(DacpacOperationResult result)
{
    if (result.Succeeded)
    {
        Log.Information(
            "DACPAC ready at {Path} (kind {Kind}, tool {Tool})",
            result.DacpacPath,
            result.ProjectKind,
            result.ToolUsed);
    }
    else
    {
        Log.Error("DACPAC operation failed: {Error}", result.Error);
        if (!string.IsNullOrWhiteSpace(result.Log))
        {
            Log.Error("{Log}", result.Log);
        }
    }
}

static bool TryGetFlagValue(string[] args, string flag, out string? value)
{
    value = GetFlagValue(args, flag);
    return value is not null;
}

static string? GetFlagValue(string[] args, string flag)
{
    for (var i = 0; i < args.Length; i++)
    {
        if (!string.Equals(args[i], flag, StringComparison.OrdinalIgnoreCase))
        {
            continue;
        }

        if (i + 1 >= args.Length)
        {
            throw new InvalidOperationException($"Missing value after {flag}.");
        }

        return args[i + 1];
    }

    return null;
}
