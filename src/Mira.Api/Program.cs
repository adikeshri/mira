using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Diagnostics;
using Mira.Api.Endpoints;
using Mira.Application;
using Mira.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
});
builder.Services
    .AddApplication()
    .AddInfrastructure(builder.Configuration["Mira:ConfigPath"] ?? "config.json");

builder.Services.AddEndpointsApiExplorer().AddSwaggerGen();

var app = builder.Build();

app.UseSwagger().UseSwaggerUI(o => o.RoutePrefix = "swagger"); // UI at /swagger

// Invalid input is a 400 and upstream trouble a 502; anything else is our bug.
app.UseExceptionHandler(h => h.Run(async ctx =>
{
    var e = ctx.Features.Get<IExceptionHandlerFeature>()?.Error;
    var (status, error) = e switch
    {
        ValidationException v => (StatusCodes.Status400BadRequest, v.Message),
        HttpRequestException or TaskCanceledException or JsonException => (StatusCodes.Status502BadGateway, "upstream unavailable"),
        _ => (StatusCodes.Status500InternalServerError, "internal error"),
    };
    ctx.Response.StatusCode = status;
    await ctx.Response.WriteAsJsonAsync(new { error });
}));

app.MapGet("/health", () => Results.Ok()).WithTags("Health");

var api = app.MapGroup("/api").AddEndpointFilter(async (ctx, next) =>
{
    ctx.HttpContext.Response.Headers.CacheControl = "no-store";
    return await next(ctx);
});
api.MapGroup("").WithTags("Configuration").MapConfig();
api.MapGroup("").WithTags("Locations").MapLocation();
api.MapGroup("").WithTags("Weather").MapWeather();
api.MapGroup("").WithTags("Markets").MapMarkets();
api.MapGroup("").WithTags("News").MapNews();
api.MapGroup("").WithTags("History").MapHistory();
api.MapGroup("").WithTags("Network").MapNetwork();

app.Run();

public partial class Program;
