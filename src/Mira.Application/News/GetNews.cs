using MediatR;
using Mira.Application.Configuration;
using Mira.Domain.Configuration;
using Mira.Domain.News;

namespace Mira.Application.News;

public interface IFeedReader
{
    // Items of one configured feed, or empty when it is unavailable.
    Task<IReadOnlyList<NewsItem>> ReadAsync(Feed feed, CancellationToken ct);
}

public sealed record GetNewsQuery : IRequest<Headlines>;

public sealed class GetNewsHandler(ISettingsStore settings, IFeedReader reader) : IRequestHandler<GetNewsQuery, Headlines>
{
    public const int MaxItems = 20;

    public async Task<Headlines> Handle(GetNewsQuery q, CancellationToken ct)
    {
        var cfg = (await settings.GetAsync(ct)).News;
        var (world, local) = (Merge(cfg.Feeds, ct), Merge(cfg.Local, ct));
        return new Headlines(await world, await local);
    }

    private async Task<IReadOnlyList<NewsItem>> Merge(IReadOnlyList<Feed> feeds, CancellationToken ct) =>
        (await Task.WhenAll(feeds.Select(f => reader.ReadAsync(f, ct))))
            .SelectMany(items => items)
            .OrderByDescending(i => i.PublishedAt ?? DateTimeOffset.MinValue)
            .Take(MaxItems)
            .ToList();
}
