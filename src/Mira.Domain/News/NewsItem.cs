namespace Mira.Domain.News;

public sealed record NewsItem(string Title, string Source, string? Url, DateTimeOffset? PublishedAt);

public sealed record Headlines(IReadOnlyList<NewsItem> World, IReadOnlyList<NewsItem> Local);
