using Mira.Domain.Weather;

namespace Mira.Domain.Configuration;

// Same shape as mirror-magic's config.json. Defaults apply per field when omitted.
public sealed record MirrorSettings
{
    public string Name { get; init; } = "";
    public string Locale { get; init; } = "";
    // IANA zone (e.g. "Europe/London") that defines "today" for the calendar. Empty means the server's own zone.
    public string TimeZone { get; init; } = "";
    public bool Hour24 { get; init; }
    public Units Units { get; init; } = Units.Metric;
    public Locations.Location? Location { get; init; }
    // With no location, estimate one from the server's IP. Sends that IP to a geolocation service.
    public bool AutoLocation { get; init; } = true;
    public ModuleToggles Modules { get; init; } = new();
    public MarketsSettings Markets { get; init; } = new();
    public NewsSettings News { get; init; } = new();
    // Fixed places to time a drive to, e.g. Work. Empty means none.
    // Mapbox public token for live-traffic commute times. Server-side only: GetSettingsHandler strips it.
    public string? MapboxToken { get; init; }
    public IReadOnlyList<Destination> Commute { get; init; } = [];
    // iCal (.ics) URLs, merged by /api/calendar. The URLs are secrets: GetSettingsHandler strips them.
    public IReadOnlyList<Feed> Calendars { get; init; } = [];
    public DisplaySettings Display { get; init; } = new();
}

public enum LayoutMode { Auto, Portrait, Landscape }

public sealed record DisplaySettings
{
    // Auto follows the screen's shape; Portrait/Landscape force that arrangement.
    public LayoutMode Layout { get; init; } = LayoutMode.Auto;
    // Brightness from sunset to sunrise, 0.2 to 1 (1 = no dimming). The UI clamps it.
    public double NightDim { get; init; } = 0.6;
}

public sealed record Destination(string Name, double Lat, double Lon);

public sealed record ModuleToggles
{
    public bool Greeting { get; init; } = true;
    public bool Clock { get; init; } = true;
    public bool Weather { get; init; } = true;
    public bool Forecast { get; init; } = true;
    public bool Markets { get; init; } = true;
    public bool News { get; init; } = true;
    public bool Quote { get; init; } = true;
    public bool OnThisDay { get; init; } = true;
    public bool Network { get; init; }
    public bool Calendar { get; init; } = true;
}

public sealed record CryptoAsset(string Id, string Label);
public sealed record FxPair(string From, string To, string? Label = null);
public sealed record StockIndex(string Symbol, string Label);
public sealed record Feed(string Url, string Name);

public sealed record MarketsSettings
{
    public IReadOnlyList<CryptoAsset> Crypto { get; init; } = [new("bitcoin", "BTC"), new("ethereum", "ETH")];
    public string CryptoCurrency { get; init; } = "usd";
    public IReadOnlyList<FxPair> Fx { get; init; } = [new("EUR", "USD")];
    public IReadOnlyList<StockIndex> Indices { get; init; } = [new("^GSPC", "S&P 500")];
}

public sealed record NewsSettings
{
    public IReadOnlyList<Feed> Feeds { get; init; } = [
        new("https://feeds.bbci.co.uk/news/world/rss.xml", "BBC"),
        new("https://www.theguardian.com/world/rss", "Guardian"),
        new("https://www.aljazeera.com/xml/rss/all.xml", "Al Jazeera"),
        new("https://feeds.npr.org/1001/rss.xml", "NPR"),
        new("https://rss.nytimes.com/services/xml/rss/nyt/World.xml", "NYT")];
    // A second, regional list. Empty hides it.
    public IReadOnlyList<Feed> Local { get; init; } = [];
}
