using System.Collections.Concurrent;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Mira.Application.Markets;
using Mira.Domain.Markets;

namespace Mira.Infrastructure.Markets;

// Index quotes from Yahoo Finance's unofficial chart endpoint.
//
// Yahoo is strict: requests without a browser User-Agent get 429, and so does a request
// that follows another within ~2s. So calls are serialised with a gap, cached by market
// hours, and the last good value is served when Yahoo refuses. Must be a singleton.
// ponytail: cache is in memory, a restart blanks it until the next fetch.
public sealed class YahooIndexQuotes(IHttpClientFactory factory, TimeProvider clock, ILogger<YahooIndexQuotes> log) : IIndexQuotes
{
    public const string ClientName = "yahoo";
    private static readonly TimeSpan OpenTtl = TimeSpan.FromMinutes(5), ClosedTtl = TimeSpan.FromHours(1),
        CloseGrace = TimeSpan.FromMinutes(15), FailureBackoff = TimeSpan.FromMinutes(2);

    public record Period(DateTimeOffset Start, DateTimeOffset End);
    private sealed record Entry(Quote Quote, DateTimeOffset ExpiresAt);

    public TimeSpan Gap { get; init; } = TimeSpan.FromSeconds(4);
    private readonly ConcurrentDictionary<string, Entry> _cache = new();
    private readonly ConcurrentDictionary<string, DateTimeOffset> _failedUntil = new();
    private readonly SemaphoreSlim _gate = new(1, 1);

    // When should a quote fetched at `now` be refetched?
    //   before the session opens -> hold until the open
    //   during the session       -> every 5 minutes
    //   after the close          -> hourly, until Yahoo rolls over to the next session
    public static DateTimeOffset ExpiryFor(DateTimeOffset now, Period? period)
    {
        if (period is null) return now + OpenTtl;
        if (now < period.Start) return period.Start;
        return now < period.End + CloseGrace ? now + OpenTtl : now + ClosedTtl;
    }

    public async Task<Quote?> GetAsync(string symbol, CancellationToken ct)
    {
        if (Fresh(symbol, out var quote)) return quote;
        _cache.TryGetValue(symbol, out var stale);

        await _gate.WaitAsync(ct);
        // Space the next call out without delaying this one's response.
        try
        {
            if (Fresh(symbol, out quote)) return quote; // another caller fetched it while we queued
            var (q, period) = await Fetch(symbol, ct);
            _cache[symbol] = new Entry(q, ExpiryFor(clock.GetUtcNow(), period));
            return q;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException or InvalidDataException)
        {
            log.LogWarning("yahoo {Symbol}: {Message}; serving last known value", symbol, e.Message);
            _failedUntil[symbol] = clock.GetUtcNow() + FailureBackoff;
            return stale?.Quote;
        }
        finally
        {
            _ = Task.Delay(Gap).ContinueWith(_ => _gate.Release());
        }
    }

    private bool Fresh(string symbol, out Quote? quote)
    {
        var now = clock.GetUtcNow();
        _cache.TryGetValue(symbol, out var hit);
        quote = hit?.Quote;
        return hit is not null && hit.ExpiresAt > now || _failedUntil.TryGetValue(symbol, out var until) && until > now;
    }

    private async Task<(Quote, Period?)> Fetch(string symbol, CancellationToken ct)
    {
        // Daily closes for the last week; the last bar is today's (live) one.
        var j = await factory.CreateClient(ClientName).GetFromJsonAsync<JsonElement>(
            $"https://query1.finance.yahoo.com/v8/finance/chart/{Uri.EscapeDataString(symbol)}?interval=1d&range=7d", ct);
        var result = j.GetProperty("chart").GetProperty("result")[0];
        var meta = result.GetProperty("meta");
        if (!meta.TryGetProperty("regularMarketPrice", out var p) || p.ValueKind != JsonValueKind.Number)
            throw new InvalidDataException($"{symbol}: no price");
        var value = p.GetDouble();
        var closes = result.GetProperty("indicators").GetProperty("quote")[0].GetProperty("close").EnumerateArray()
            .Where(v => v.ValueKind == JsonValueKind.Number).Select(v => v.GetDouble()).ToList();
        double? prev = closes.Count > 1 ? closes[^2] : Num(meta, "chartPreviousClose") ?? Num(meta, "previousClose");

        Period? period = null;
        if (meta.TryGetProperty("currentTradingPeriod", out var tp) && tp.TryGetProperty("regular", out var r)
            && Num(r, "start") is { } s && Num(r, "end") is { } e)
            period = new Period(DateTimeOffset.FromUnixTimeSeconds((long)s), DateTimeOffset.FromUnixTimeSeconds((long)e));
        return (Quote.Of(value, prev, closes), period);
    }

    private static double? Num(JsonElement e, string key) =>
        e.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.Number ? v.GetDouble() : null;
}
