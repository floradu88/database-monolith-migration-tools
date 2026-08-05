using CodegraphChat.Contracts;
using CodegraphChat.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.Configure<CodegraphChatOptions>(
    builder.Configuration.GetSection(CodegraphChatOptions.SectionName));
builder.Services.AddCodegraphChat();
builder.Services.AddCors(options =>
{
    options.AddPolicy("angular", policy =>
        policy.WithOrigins("http://localhost:4201")
              .AllowAnyHeader()
              .AllowAnyMethod());
});

var app = builder.Build();
app.UseCors("angular");
app.UseDefaultFiles();
app.UseStaticFiles();

var api = app.MapGroup("/api");

api.MapGet("/health", async (ITopicChatService chat) =>
{
    var health = await chat.GetHealthAsync();
    return health.Healthy
        ? Results.Ok(health)
        : Results.Json(health, statusCode: StatusCodes.Status503ServiceUnavailable);
});

api.MapGet("/session", async (ITopicChatService chat) =>
    Results.Ok(await chat.GetSessionAsync()));

api.MapPost("/session", async (SessionConfigRequest request, ITopicChatService chat) =>
{
    try
    {
        return Results.Ok(await chat.SetSessionAsync(request));
    }
    catch (Exception ex) when (ex is ArgumentException or DirectoryNotFoundException)
    {
        return Results.BadRequest(new { message = ex.Message });
    }
});

api.MapPost("/session/ensure-index", async (ITopicChatService chat) =>
{
    try
    {
        return Results.Ok(await chat.EnsureIndexAsync());
    }
    catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or DirectoryNotFoundException)
    {
        return Results.BadRequest(new { message = ex.Message });
    }
});

api.MapPost("/chat", async (ChatRequest request, ITopicChatService chat) =>
{
    try
    {
        return Results.Ok(await chat.AskAsync(request));
    }
    catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or DirectoryNotFoundException)
    {
        return Results.BadRequest(new { message = ex.Message });
    }
});

app.MapFallbackToFile("index.html");
app.Run();

public partial class Program;
