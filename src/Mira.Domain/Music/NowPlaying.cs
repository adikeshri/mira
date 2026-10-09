namespace Mira.Domain.Music;

// PositionMs is where the track is right now (already advanced while playing); DurationMs is null until the player says.
public sealed record NowPlaying(string State, string? Title, string? Artist, string? Album, string? CoverUrl, long? PositionMs = null, long? DurationMs = null)
{
    public static readonly NowPlaying Stopped = new("stopped", null, null, null, null);
}
