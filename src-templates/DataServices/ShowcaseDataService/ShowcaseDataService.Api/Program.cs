using BuildingBlocks.Migration;
using BuildingBlocks.Observability;
using Microsoft.AspNetCore.Authentication.JwtBearer;
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

var auth = builder.Configuration.GetSection(ShowcaseAuthOptions.SectionName).Get<ShowcaseAuthOptions>() ?? new();
if (auth.RequireJwt)
{
    // JWT / MI-ready placeholder — set Auth:Authority / Audience / ManagedIdentityClientId from real env values.
    builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(options =>
        {
            options.Authority = auth.Authority;
            options.Audience = auth.Audience;
        });
    builder.Services.AddAuthorization();
}

var app = builder.Build();

app.UseSwagger();
app.UseSwaggerUI();
app.UseDefaultFiles();
app.UseStaticFiles();

if (auth.RequireJwt)
{
    app.UseAuthentication();
    app.UseAuthorization();
}

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }));
app.MapGet("/ready", (IOptions<MigrationRoutingOptions> routing) =>
    Results.Ok(new
    {
        status = "ready",
        slot = routing.Value.Slot.ToString(),
        defaultRoute = routing.Value.DefaultRoute.ToString(),
        authoritativeMethod = routing.Value.AuthoritativeMethod.ToString()
    }));

var items = app.MapGroup("/api/showcase");
if (auth.RequireJwt) items.RequireAuthorization();

items.MapGet("/items/{id:guid}", async (Guid id, IShowcaseItemService service, CancellationToken ct) =>
{
    var item = await service.GetSummaryAsync(id, ct);
    return item is null ? Results.NotFound() : Results.Ok(item);
});

items.MapPut("/items/{id:guid}", async (Guid id, ShowcaseUpdateRequest body, IShowcaseItemService service, CancellationToken ct) =>
{
    if (body.Id != id) return Results.BadRequest(new { message = "Id mismatch." });
    await service.UpdateAsync(body, ct);
    return Results.NoContent();
});

items.MapGet("/dashboard", (IShowcaseItemService service) => Results.Ok(service.GetDashboard()));

items.MapGet("/items/{id:guid}/benchmark", async (Guid id, IShowcaseItemService service, CancellationToken ct) =>
    Results.Ok(await service.BenchmarkAccessAsync(id, ct)));

app.Run();

public partial class Program;
