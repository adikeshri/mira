using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Mira.Application.Markets;
using Mira.Application.News;
using Mira.Domain.Configuration;
using Mira.Domain.Markets;
using Mira.Domain.News;

namespace Mira.Tests;

public class ApiTests : IClassFixture<WebApplicationFactory<Program>>
{
    private sealed class FakeFeeds : IFeedReader
    {
        public Task<IReadOnlyList<NewsItem>> ReadAsync(Feed feed, CancellationToken ct) =>
            Task.FromResult<IReadOnlyList<NewsItem>>(feed.Name == "Down" ? []
                : Enumerable.Range(0, 15).Select(i => new NewsItem($"{feed.Name} {i}", feed.Name, null, DateTimeOffset.UnixEpoch.AddDays(i))).ToList());
    }

    private sealed class Quotes : ICryptoQuotes, IFxQuotes, IIndexQuotes
    {
        public Task<IReadOnlyDictionary<string, Quote>> GetAsync(IEnumerable<string> ids, string currency, CancellationToken ct) =>
            throw new HttpRequestException("coingecko down");
        public Task<Quote?> GetAsync(string from, string to, CancellationToken ct) => Task.FromResult<Quote?>(Quote.Of(1.1, 1.0, []));
        public Task<Quote?> GetAsync(string symbol, CancellationToken ct) => Task.FromResult<Quote?>(null);
    }

    private sealed class FixedSettings(MirrorSettings s) : Mira.Application.Configuration.ISettingsStore
    {
        public Task<MirrorSettings> GetAsync(CancellationToken ct) => Task.FromResult(s);
    }

    private readonly HttpClient _http;

    public ApiTests(WebApplicationFactory<Program> factory) =>
        _http = factory.WithWebHostBuilder(b => b.UseSetting("Mira:ConfigPath", "/nonexistent/config.json").ConfigureServices(s =>
        {
            s.AddSingleton<IFeedReader, FakeFeeds>();
            s.AddSingleton<Quotes>().AddSingleton<ICryptoQuotes>(p => p.GetRequiredService<Quotes>())
                .AddSingleton<IFxQuotes>(p => p.GetRequiredService<Quotes>()).AddSingleton<IIndexQuotes>(p => p.GetRequiredService<Quotes>());
        })).CreateClient();

    [Fact]
    public void Osrm_route_maps_to_minutes_and_km()
    {
        var j = JsonDocument.Parse("""{"routes":[{"duration":1510,"distance":12345}]}""").RootElement;
        Assert.Equal(new Domain.Locations.Commute("Work", 25, 12.3), Mira.Infrastructure.Locations.OsrmRouter.Map("Work", j));
        var traffic = JsonDocument.Parse("""{"routes":[{"duration":1510,"duration_typical":1200,"distance":12345}]}""").RootElement;
        Assert.Equal(20, Mira.Infrastructure.Locations.OsrmRouter.Map("Work", traffic)!.TypicalMinutes);
        Assert.Null(Mira.Infrastructure.Locations.OsrmRouter.Map("Work", JsonDocument.Parse("""{"routes":[]}""").RootElement));
    }

    [Fact]
    public async Task Commute_validates_coordinates_and_is_empty_without_destinations()
    {
        var store = new FixedSettings(new MirrorSettings { MapboxToken = "secret" });
        Assert.Null((await new Mira.Application.Configuration.GetSettingsHandler(store).Handle(new(), default)).MapboxToken);
        Assert.Equal(HttpStatusCode.BadRequest, (await _http.GetAsync("/api/commute?lat=91&lon=0")).StatusCode);
        Assert.Empty((await _http.GetFromJsonAsync<JsonElement>("/api/commute?lat=1&lon=1")).EnumerateArray());
    }

    [Fact]
    public async Task Health_and_config_defaults()
    {
        Assert.Equal(HttpStatusCode.OK, (await _http.GetAsync("/health")).StatusCode);
        var cfg = await _http.GetFromJsonAsync<JsonElement>("/api/config");
        Assert.Equal("metric", cfg.GetProperty("units").GetString());
        Assert.True(cfg.GetProperty("modules").GetProperty("clock").GetBoolean());
    }

    [Fact]
    public async Task News_is_merged_newest_first_and_capped_per_list()
    {
        var news = await _http.GetFromJsonAsync<JsonElement>("/api/news");
        var world = news.GetProperty("world").EnumerateArray().ToList();
        Assert.Equal(GetNewsHandler.MaxItems, world.Count);
        Assert.EndsWith(" 14", world[0].GetProperty("title").GetString()); // newest first
        Assert.Empty(news.GetProperty("local").EnumerateArray());
    }

    [Fact]
    public async Task Markets_survive_one_source_being_down()
    {
        var rows = await _http.GetFromJsonAsync<JsonElement>("/api/markets");
        var byKey = rows.EnumerateArray().ToDictionary(r => r.GetProperty("key").GetString()!);
        Assert.Equal(JsonValueKind.Null, byKey["crypto:bitcoin"].GetProperty("quote").ValueKind);   // crypto source threw
        Assert.Equal("EUR/USD", byKey["fx:EURUSD"].GetProperty("label").GetString());
        Assert.True(byKey["fx:EURUSD"].GetProperty("invertColor").GetBoolean());
        Assert.NotEqual(JsonValueKind.Null, byKey["fx:EURUSD"].GetProperty("quote").ValueKind);
    }

    [Fact]
    public async Task Now_playing_follows_player_events()
    {
        async Task<JsonElement> Now() => await _http.GetFromJsonAsync<JsonElement>("/api/now-playing");
        Assert.Equal("stopped", (await Now()).GetProperty("state").GetString());

        await _http.PostAsync("/api/now-playing?event=track_changed&title=Song&artist=A, B&album=Alb&cover=https://i.scdn.co/x&duration=200000", null);
        await _http.PostAsync("/api/now-playing?event=playing", null);
        var n = await Now();
        Assert.Equal(("playing", "Song", "A, B"), (n.GetProperty("state").GetString(), n.GetProperty("title").GetString(), n.GetProperty("artist").GetString()));

        Assert.Equal(200000, n.GetProperty("durationMs").GetInt64());
        Assert.True(n.GetProperty("positionMs").GetInt64() >= 0);

        // The hook sends empty values for variables librespot did not set.
        Assert.Equal(HttpStatusCode.NoContent, (await _http.PostAsync("/api/now-playing?event=playing&position=&duration=", null)).StatusCode);
        await _http.PostAsync("/api/now-playing?event=paused&position=5000", null);
        Assert.Equal(5000, (await Now()).GetProperty("positionMs").GetInt64());
        Assert.Equal("paused", (await Now()).GetProperty("state").GetString());

        await _http.PostAsync("/api/now-playing?event=stopped", null);
        Assert.Equal(JsonValueKind.Null, (await Now()).GetProperty("title").ValueKind);
    }

    [Fact]
    public async Task Now_playing_stream_pushes_changes()
    {
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(10));
        using var res = await _http.GetAsync("/api/now-playing/stream", HttpCompletionOption.ResponseHeadersRead, cts.Token);
        Assert.Equal("text/event-stream", res.Content.Headers.ContentType?.MediaType);
        using var reader = new StreamReader(await res.Content.ReadAsStreamAsync(cts.Token));

        async Task<JsonElement> Next()
        {
            while (await reader.ReadLineAsync(cts.Token) is { } line)
                if (line.StartsWith("data:")) return JsonDocument.Parse(line[5..]).RootElement;
            throw new EndOfStreamException();
        }

        Assert.Equal(JsonValueKind.Object, (await Next()).ValueKind); // current state on connect
        await _http.PostAsync("/api/now-playing?event=track_changed&title=Pushed&duration=1000", null);
        var n = await Next();
        Assert.Equal("Pushed", n.GetProperty("title").GetString());
        Assert.Equal(1000, n.GetProperty("durationMs").GetInt64());
    }

    [Theory]
    [InlineData("/api/weather?lat=91&lon=0")]
    [InlineData("/api/weather?lat=1&lon=0&units=kelvin")]
    [InlineData("/api/places/reverse?lat=0&lon=181")]
    [InlineData("/api/on-this-day?month=13&day=1")]
    public async Task Bad_input_is_400(string url) => Assert.Equal(HttpStatusCode.BadRequest, (await _http.GetAsync(url)).StatusCode);
}
