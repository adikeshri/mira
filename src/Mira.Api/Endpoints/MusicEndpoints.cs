using System.Text.Json;
using MediatR;
using Microsoft.AspNetCore.Http.Json;
using Microsoft.Extensions.Options;
using Mira.Application.Music;

namespace Mira.Api.Endpoints;

public static class MusicEndpoints
{
    public static void MapMusic(this IEndpointRouteBuilder api)
    {
        api.MapGet("/now-playing", async (ISender mediator, CancellationToken ct) => await mediator.Send(new GetNowPlayingQuery(), ct));

        // Server-sent events: the current state on connect, then one message per change. The browser's EventSource reconnects by itself.
        api.MapGet("/now-playing/stream", async (HttpContext http, ISender mediator, IOptions<JsonOptions> json, CancellationToken ct) =>
        {
            http.Response.ContentType = "text/event-stream";
            try
            {
                await foreach (var n in mediator.CreateStream(new StreamNowPlayingQuery(), ct))
                {
                    await http.Response.WriteAsync($"data: {JsonSerializer.Serialize(n, json.Value.SerializerOptions)}\n\n", ct);
                    await http.Response.Body.FlushAsync(ct);
                }
            }
            catch (OperationCanceledException) { } // the browser went away
        });

        // Called by the player's event hook on the same machine (see README); the port is loopback-only by default.
        api.MapPost("/now-playing", async (string @event, string? title, string? artist, string? album, string? cover, string? position, string? duration, ISender mediator, CancellationToken ct) =>
        {
            await mediator.Send(new SetNowPlayingCommand(@event, title, artist, album, cover, Ms(position), Ms(duration)), ct);
            return Results.NoContent();
        });
    }

    // The hook sends an empty string for anything librespot did not set.
    private static long? Ms(string? s) => long.TryParse(s, out var v) && v >= 0 ? v : null;
}
