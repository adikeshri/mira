using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Extensions.FileProviders;
using Mira.Api.Endpoints;
using Mira.Application;
using Mira.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.ConfigureHttpJsonOptions(o =>
{
    o.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    o.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.CamelCase));
});
// The browser may call Mira directly from these origins (e.g. the Vite dev server). GET only.
builder.Services.AddCors(o => o.AddDefaultPolicy(p => p
    .WithOrigins(builder.Configuration.GetSection("Mira:AllowedOrigins").Get<string[]>() ?? [])
    .WithMethods("GET")));
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

app.UseCors();

// Optionally serve the built mirror UI (mirror-magic's dist/), so one process is the whole backend and frontend host.
if (builder.Configuration["Mira:UiPath"] is { Length: > 0 } uiPath)
{
    var root = Path.GetFullPath(uiPath);
    if (!Directory.Exists(root)) app.Logger.LogWarning("Mira:UiPath {Root} does not exist; not serving a UI", root);
    else
    {
        var files = new PhysicalFileProvider(root);
        app.Logger.LogInformation("serving UI from {Root}", root);
        app.Use((ctx, next) =>
        {
            if (!ctx.Request.Path.StartsWithSegments("/swagger")) // Swagger UI needs inline scripts
            {
                var h = ctx.Response.Headers;
                h.ContentSecurityPolicy = "default-src 'self'; script-src 'self'; style-src 'self'; font-src 'self' data:; img-src 'self' data:; " +
                                          "connect-src 'self'; object-src 'none'; base-uri 'none'; form-action 'none'; frame-ancestors 'none'";
                h.XContentTypeOptions = "nosniff";
                h["Referrer-Policy"] = "strict-origin-when-cross-origin";
                h["Permissions-Policy"] = "geolocation=(self), camera=(), microphone=()";
            }
            return next();
        });
        app.UseDefaultFiles(new DefaultFilesOptions { FileProvider = files });
        app.UseStaticFiles(new StaticFileOptions { FileProvider = files });
        app.MapFallbackToFile("index.html", new StaticFileOptions { FileProvider = files }); // single-page app
    }
}

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
api.MapGroup("").WithTags("Calendar").MapCalendar();
api.MapGroup("").WithTags("History").MapHistory();
api.MapGroup("").WithTags("Network").MapNetwork();
api.MapFallback(() => Results.NotFound(new { error = "not found" })); // unknown /api paths must not fall through to the UI

app.Run();

public partial class Program;
