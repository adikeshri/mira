using MediatR;
using Mira.Domain.Music;

namespace Mira.Application.Music;

// The player (librespot's --onevent hook) pushes events; the mirror polls the latest state. Held in memory: a restart means "stopped".
public sealed class NowPlayingState
{
    private readonly object _gate = new();
    private NowPlaying _current = NowPlaying.Stopped;

    public NowPlaying Get() { lock (_gate) return _current; }
    public void Update(Func<NowPlaying, NowPlaying> change) { lock (_gate) _current = change(_current); }
}

public sealed record SetNowPlayingCommand(string Event, string? Title, string? Artist, string? Album, string? Cover) : IRequest;

public sealed record GetNowPlayingQuery : IRequest<NowPlaying>;

public sealed class NowPlayingHandler(NowPlayingState state) :
    IRequestHandler<SetNowPlayingCommand>, IRequestHandler<GetNowPlayingQuery, NowPlaying>
{
    public Task Handle(SetNowPlayingCommand c, CancellationToken ct)
    {
        state.Update(n => c.Event switch
        {
            "track_changed" => new NowPlaying(n.State, Cap(c.Title), Cap(c.Artist), Cap(c.Album), HttpsOnly(c.Cover)),
            "playing" or "paused" => n with { State = c.Event },
            "stopped" or "session_disconnected" or "unavailable" => NowPlaying.Stopped,
            _ => n,
        });
        return Task.CompletedTask;
    }

    public Task<NowPlaying> Handle(GetNowPlayingQuery q, CancellationToken ct) => Task.FromResult(state.Get());

    private static string? Cap(string? s) => s is { Length: > 200 } ? s[..200] : s;
    private static string? HttpsOnly(string? url) => url is { Length: <= 500 } && url.StartsWith("https://", StringComparison.Ordinal) ? url : null;
}
