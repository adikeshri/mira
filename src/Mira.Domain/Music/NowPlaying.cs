namespace Mira.Domain.Music;

public sealed record NowPlaying(string State, string? Title, string? Artist, string? Album, string? CoverUrl)
{
    public static readonly NowPlaying Stopped = new("stopped", null, null, null, null);
}
