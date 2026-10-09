using MediatR;
using Mira.Domain.Music;

namespace Mira.Application.Music;

// The player (librespot's --onevent hook) pushes events; the mirror polls the latest state. Held in memory: a restart means "stopped".
// The player reports a position only on events, so while playing it is extrapolated from when it was reported.
public sealed class NowPlayingState(TimeProvider clock)
{
    private readonly object _gate = new();
    private NowPlaying _current = NowPlaying.Stopped;
    private DateTimeOffset _at = clock.GetUtcNow();

    public NowPlaying Get() { lock (_gate) return Advanced(); }

    // `change` sees the current track with its position already advanced.
    public void Update(Func<NowPlaying, NowPlaying> change)
    {
        lock (_gate)
        {
            _current = change(Advanced());
            _at = clock.GetUtcNow();
        }
    }

    private NowPlaying Advanced()
    {
        if (_current is not { State: "playing", PositionMs: { } p }) return _current;
        var pos = p + (long)(clock.GetUtcNow() - _at).TotalMilliseconds;
        return _current with { PositionMs = _current.DurationMs is { } d ? Math.Min(pos, d) : pos };
    }
}

public sealed record SetNowPlayingCommand(string Event, string? Title, string? Artist, string? Album, string? Cover, long? PositionMs, long? DurationMs) : IRequest;

public sealed record GetNowPlayingQuery : IRequest<NowPlaying>;

public sealed class NowPlayingHandler(NowPlayingState state) :
    IRequestHandler<SetNowPlayingCommand>, IRequestHandler<GetNowPlayingQuery, NowPlaying>
{
    public Task Handle(SetNowPlayingCommand c, CancellationToken ct)
    {
        state.Update(n => c.Event switch
        {
            "track_changed" => new NowPlaying(n.State, Cap(c.Title), Cap(c.Artist), Cap(c.Album), HttpsOnly(c.Cover), 0, c.DurationMs),
            "playing" or "paused" => n with { State = c.Event, PositionMs = c.PositionMs ?? n.PositionMs },
            "seeked" or "position_correction" => n with { PositionMs = c.PositionMs ?? n.PositionMs },
            "stopped" or "session_disconnected" or "unavailable" => NowPlaying.Stopped,
            _ => n,
        });
        return Task.CompletedTask;
    }

    public Task<NowPlaying> Handle(GetNowPlayingQuery q, CancellationToken ct) => Task.FromResult(state.Get());

    private static string? Cap(string? s) => s is { Length: > 200 } ? s[..200] : s;
    private static string? HttpsOnly(string? url) => url is { Length: <= 500 } && url.StartsWith("https://", StringComparison.Ordinal) ? url : null;
}
