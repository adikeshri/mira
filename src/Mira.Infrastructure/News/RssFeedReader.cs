using System.Xml;
using System.Xml.Linq;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Mira.Application.News;
using Mira.Domain.Configuration;
using Mira.Domain.News;

namespace Mira.Infrastructure.News;

// Fetches only feeds listed in config.json, never a URL supplied by a client.
public sealed class RssFeedReader(HttpClient http, IMemoryCache cache, ILogger<RssFeedReader> log) : IFeedReader
{
    private const int MaxBytes = 2 * 1024 * 1024;
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(10), StaleFor = TimeSpan.FromDays(1);

    public async Task<IReadOnlyList<NewsItem>> ReadAsync(Feed feed, CancellationToken ct)
    {
        if (cache.TryGetValue($"feed:{feed.Url}", out IReadOnlyList<NewsItem>? hit) && hit is not null) return hit;
        try
        {
            using var res = await http.GetAsync(feed.Url, HttpCompletionOption.ResponseHeadersRead, ct);
            res.EnsureSuccessStatusCode();
            var items = Parse(await ReadCapped(res, ct), feed.Name);
            cache.Set($"feed:{feed.Url}", items, Ttl);
            cache.Set($"feed-stale:{feed.Url}", items, StaleFor);
            return items;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or InvalidDataException)
        {
            log.LogWarning("feed {Url} failed: {Message}", feed.Url, e.Message);
            // Stale beats nothing.
            return cache.TryGetValue($"feed-stale:{feed.Url}", out IReadOnlyList<NewsItem>? stale) && stale is not null ? stale : [];
        }
    }

    private static async Task<string> ReadCapped(HttpResponseMessage res, CancellationToken ct)
    {
        await using var body = await res.Content.ReadAsStreamAsync(ct);
        using var buffer = new MemoryStream();
        var chunk = new byte[16 * 1024];
        int n;
        while ((n = await body.ReadAsync(chunk, ct)) > 0)
        {
            if (buffer.Length + n > MaxBytes) throw new InvalidDataException($"feed larger than {MaxBytes} bytes");
            buffer.Write(chunk, 0, n);
        }
        return System.Text.Encoding.UTF8.GetString(buffer.GetBuffer(), 0, (int)buffer.Length);
    }

    // Handles RSS <item> and Atom <entry>. Only text content is read, never markup.
    public static IReadOnlyList<NewsItem> Parse(string xml, string source)
    {
        XDocument doc;
        try
        {
            using var reader = XmlReader.Create(new StringReader(xml), new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null });
            doc = XDocument.Load(reader);
        }
        catch (XmlException)
        {
            return [];
        }

        return doc.Descendants().Where(e => e.Name.LocalName is "item" or "entry").SelectMany(el =>
        {
            string? Child(string name) => el.Elements().FirstOrDefault(c => c.Name.LocalName == name)?.Value.Trim();
            var title = Child("title");
            if (string.IsNullOrEmpty(title)) return [];
            // RSS has <link>text</link>, Atom has <link href>. Only http(s) links are kept.
            var link = el.Elements().FirstOrDefault(c => c.Name.LocalName == "link");
            var href = (link?.Attribute("href")?.Value is { Length: > 0 } a ? a : link?.Value)?.Trim();
            var url = href is not null && Uri.TryCreate(href, UriKind.Absolute, out var u) && u.Scheme is "http" or "https" ? href : null;
            var when = Child("pubDate") ?? Child("published") ?? Child("updated");
            DateTimeOffset? date = when is not null && DateTimeOffset.TryParse(when, out var d) ? d : null;
            return new[] { new NewsItem(title, source, url, date) };
        }).ToList();
    }
}
