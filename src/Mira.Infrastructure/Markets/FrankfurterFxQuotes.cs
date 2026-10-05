using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Mira.Application.Markets;
using Mira.Domain.Markets;

namespace Mira.Infrastructure.Markets;

// ECB rates change once a day, so an hour of caching loses nothing.
public sealed class FrankfurterFxQuotes(HttpClient http, IMemoryCache cache, TimeProvider clock) : IFxQuotes
{
    public Task<Quote?> GetAsync(string from, string to, CancellationToken ct) =>
        cache.Cached($"fx:{from}{to}", TimeSpan.FromHours(1), async () =>
        {
            var start = clock.GetUtcNow().AddDays(-10).ToString("yyyy-MM-dd");
            var j = await http.GetFromJsonAsync<JsonElement>(
                $"https://api.frankfurter.dev/v1/{start}..?base={Uri.EscapeDataString(from)}&symbols={Uri.EscapeDataString(to)}", ct);
            return Map(j, to);
        });

    public static Quote? Map(JsonElement j, string to)
    {
        if (!j.TryGetProperty("rates", out var rates)) return null;
        var values = rates.EnumerateObject()
            .OrderBy(d => d.Name, StringComparer.Ordinal)
            .Select(d => d.Value.TryGetProperty(to, out var v) && v.ValueKind == JsonValueKind.Number ? (double?)v.GetDouble() : null)
            .OfType<double>()
            .ToList();
        if (values.Count == 0) return null;
        return Quote.Of(values[^1], values.Count > 1 ? values[^2] : null, values.TakeLast(7).ToList());
    }
}
