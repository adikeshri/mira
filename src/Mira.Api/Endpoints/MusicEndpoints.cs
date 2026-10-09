using MediatR;
using Mira.Application.Music;

namespace Mira.Api.Endpoints;

public static class MusicEndpoints
{
    public static void MapMusic(this IEndpointRouteBuilder api)
    {
        api.MapGet("/now-playing", async (ISender mediator, CancellationToken ct) => await mediator.Send(new GetNowPlayingQuery(), ct));

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
