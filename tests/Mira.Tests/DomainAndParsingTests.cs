using System.Text.Json;
using Mira.Domain.Markets;
using Mira.Infrastructure.Locations;
using Mira.Infrastructure.Markets;
using Mira.Infrastructure.News;

namespace Mira.Tests;

public class DomainAndParsingTests
{
    [Fact]
    public void Quote_change_is_relative_to_previous()
    {
        Assert.Equal(10, Quote.Of(110, 100, []).ChangePct!.Value, 6);
        Assert.Null(Quote.Of(110, null, []).ChangePct);
        Assert.Null(Quote.Of(110, 0, []).ChangePct);
    }

    [Fact]
    public void Rss_and_atom_parse_and_unsafe_links_are_dropped()
    {
        const string rss = """
            <rss><channel>
              <item><title> One </title><link>https://a.test/1</link><pubDate>Mon, 05 Oct 2026 10:00:00 GMT</pubDate></item>
              <item><title>Two</title><link>javascript:alert(1)</link></item>
              <item><link>https://a.test/3</link></item>
            </channel></rss>
            """;
        const string atom = """<feed xmlns="http://www.w3.org/2005/Atom"><entry><title>A</title><link href="https://b.test/a"/><updated>2026-10-05T10:00:00Z</updated></entry></feed>""";

        var items = RssFeedReader.Parse(rss, "X");
        Assert.Equal(["One", "Two"], items.Select(i => i.Title));
        Assert.Equal("https://a.test/1", items[0].Url);
        Assert.NotNull(items[0].PublishedAt);
        Assert.Null(items[1].Url);

        var entry = Assert.Single(RssFeedReader.Parse(atom, "Y"));
        Assert.Equal("https://b.test/a", entry.Url);
        Assert.Empty(RssFeedReader.Parse("not xml", "Z"));
        Assert.Empty(RssFeedReader.Parse("""<!DOCTYPE x [<!ENTITY e SYSTEM "file:///etc/passwd">]><rss/>""", "Z")); // DTDs refused
    }

    [Fact]
    public void Yahoo_expiry_follows_market_hours()
    {
        var open = new DateTimeOffset(2026, 10, 5, 13, 30, 0, TimeSpan.Zero);
        var period = new YahooIndexQuotes.Period(open, open.AddHours(6.5));
        Assert.Equal(open, YahooIndexQuotes.ExpiryFor(open.AddHours(-3), period));                       // before open: hold until open
        Assert.Equal(open.AddHours(1) + TimeSpan.FromMinutes(5), YahooIndexQuotes.ExpiryFor(open.AddHours(1), period)); // in session
        Assert.Equal(period.End.AddHours(2) + TimeSpan.FromHours(1), YahooIndexQuotes.ExpiryFor(period.End.AddHours(2), period)); // after close
    }

    [Fact]
    public void Ip_providers_parse_numbers_or_numeric_strings_and_reject_bad_ones()
    {
        var geojs = JsonDocument.Parse("""{"latitude":"51.5","longitude":"-0.12","city":"London","region":""}""").RootElement;
        Assert.Equal("London", IpLocator.ToLocation(geojs)!.Value.Name);
        Assert.Null(IpLocator.ToLocation(JsonDocument.Parse("""{"success":false}""").RootElement));
        Assert.Null(IpLocator.ToLocation(JsonDocument.Parse("""{"latitude":123,"longitude":0}""").RootElement));
    }
}
