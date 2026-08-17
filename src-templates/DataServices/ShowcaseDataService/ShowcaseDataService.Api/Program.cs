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
// Lab default: Auth:RequireJwt = false (see AUTH.md). Do not invent IdP secrets here.
// When enabling JWT, set Authority/Audience from a real IdP; SQL MI / connection auth is separate (DATABASE-HOSTING.md).
if (auth.RequireJwt)
{
    // JWT / MI-ready placeholder — set Auth:Authority / Audience / ManagedIdentityClientId from real env values
    // (Key Vault / user-secrets). Never commit production credentials.
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

items.MapPost("/work-items", async (ShowcaseWorkItemRequest body, IShowcaseWorkItemService service, CancellationToken ct) =>
{
    await service.UpsertAsync(body, ct);
    return Results.Accepted();
});

items.MapDelete("/work-items/{externalId:guid}", async (Guid externalId, IShowcaseWorkItemService service, CancellationToken ct) =>
{
    await service.DeleteAsync(externalId, ct);
    return Results.NoContent();
});

items.MapGet("/work-items/integrity", async (IShowcaseWorkItemService service, CancellationToken ct) =>
    Results.Ok(await service.CheckIntegrityAsync(ct)));

items.MapGet("/items/{id:guid}/benchmark", async (Guid id, IShowcaseItemService service, CancellationToken ct) =>
    Results.Ok(await service.BenchmarkAccessAsync(id, ct)));

app.Run();

public partial class Program;
