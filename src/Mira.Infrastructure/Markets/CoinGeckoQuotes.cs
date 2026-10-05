using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Mira.Application.Markets;
using Mira.Domain.Markets;

namespace Mira.Infrastructure.Markets;

public sealed class CoinGeckoQuotes(HttpClient http, IMemoryCache cache) : ICryptoQuotes
{
    public Task<IReadOnlyDictionary<string, Quote>> GetAsync(IEnumerable<string> ids, string currency, CancellationToken ct)
    {
        var list = string.Join(",", ids.Select(Uri.EscapeDataString));
        return cache.Cached($"crypto:{currency}:{list}", TimeSpan.FromMinutes(5), async () =>
        {
            var coins = await http.GetFromJsonAsync<JsonElement>(
                $"https://api.coingecko.com/api/v3/coins/markets?vs_currency={Uri.EscapeDataString(currency)}&ids={list}&sparkline=true", ct);
            return Map(coins);
        });
    }

    public static IReadOnlyDictionary<string, Quote> Map(JsonElement coins)
    {
        var result = new Dictionary<string, Quote>();
        foreach (var c in coins.EnumerateArray())
        {
            if (!c.TryGetProperty("current_price", out var price) || price.ValueKind != JsonValueKind.Number) continue;
            double? change = c.TryGetProperty("price_change_percentage_24h", out var ch) && ch.ValueKind == JsonValueKind.Number ? ch.GetDouble() : null;
            var trend = c.TryGetProperty("sparkline_in_7d", out var s) && s.TryGetProperty("price", out var p) && p.ValueKind == JsonValueKind.Array
                ? p.EnumerateArray().Where(v => v.ValueKind == JsonValueKind.Number).Select(v => v.GetDouble()).ToList()
                : [];
            result[c.GetProperty("id").GetString()!] = new Quote(price.GetDouble(), change, trend);
        }
        return result;
    }
}
