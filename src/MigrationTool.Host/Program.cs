using Hangfire;
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

if (args.Contains("--poll-once"))
{
    using var scope = app.Services.CreateScope();
    var summary = await scope.ServiceProvider
        .GetRequiredService<MigrationTool.Application.IScriptPoller>()
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

var options = app.Services.GetRequiredService<Microsoft.Extensions.Options.IOptions<MigrationOptions>>().Value;
app.Services.GetRequiredService<IRecurringJobManager>().AddOrUpdate<ScriptPollJob>(
    "migration-script-poll",
    job => job.RunAsync(),
    options.PollCron);

app.Run();
