using DbIntelligence.Infrastructure;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.Configure<DbIntelligenceOptions>(
    builder.Configuration.GetSection(DbIntelligenceOptions.SectionName));
builder.Services.AddDbIntelligence();
builder.Services.AddHostedService<IndexingWorker>();

var host = builder.Build();
await host.RunAsync();
