using BuildingBlocks.Migration;
using BuildingBlocks.Observability;
using Microsoft.Extensions.Options;
using ShowcaseDataService.Application;
using ShowcaseDataService.Contracts;
using ShowcaseDataService.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpContextAccessor();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddShowcaseObservability("ShowcaseDataService");
builder.Services.AddShowcaseInfrastructure(builder.Configuration);

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();
app.UseDefaultFiles();
app.UseStaticFiles();

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));
app.MapGet("/ready", (IOptions<MigrationRoutingOptions> routing) =>
    Results.Ok(new
    {
        status = "ready",
        slot = routing.Value.Slot.ToString(),
        defaultRoute = routing.Value.DefaultRoute.ToString()
    }));

app.MapGet("/api/showcase/items/{id:guid}", async (Guid id, IShowcaseItemService service, CancellationToken ct) =>
{
    var item = await service.GetSummaryAsync(id, ct);
    return item is null ? Results.NotFound() : Results.Ok(item);
});

app.MapPut("/api/showcase/items/{id:guid}", async (Guid id, ShowcaseUpdateRequest body, IShowcaseItemService service, CancellationToken ct) =>
{
    if (body.Id != id) return Results.BadRequest(new { message = "Id mismatch." });
    await service.UpdateAsync(body, ct);
    return Results.NoContent();
});

app.MapGet("/api/showcase/dashboard", (IShowcaseItemService service) => Results.Ok(service.GetDashboard()));

app.MapGet("/api/showcase/items/{id:guid}/benchmark", async (Guid id, IShowcaseItemService service, CancellationToken ct) =>
    Results.Ok(await service.BenchmarkAccessAsync(id, ct)));

app.Run();

public partial class Program;
