using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Mira.Application.History;
using Mira.Domain.History;

namespace Mira.Infrastructure.History;

public sealed class WikipediaOnThisDayProvider(HttpClient http, IMemoryCache cache) : IOnThisDayProvider
{
    public Task<IReadOnlyList<HistoricalEvent>> GetAsync(int month, int day, CancellationToken ct) =>
        cache.Cached($"onthisday:{month}:{day}", TimeSpan.FromHours(24), async () =>
        {
            var j = await http.GetFromJsonAsync<JsonElement>(
                $"https://en.wikipedia.org/api/rest_v1/feed/onthisday/events/{month:00}/{day:00}", ct);
            return Map(j);
        });

    public static IReadOnlyList<HistoricalEvent> Map(JsonElement j) =>
        !j.TryGetProperty("events", out var events) ? []
        : events.EnumerateArray()
            .Select(e => (Year: e.TryGetProperty("year", out var y) && y.TryGetInt32(out var yi) ? (int?)yi : null,
                          Text: e.TryGetProperty("text", out var t) ? t.GetString()?.Trim() : null))
            .Where(e => e.Year is not null && !string.IsNullOrEmpty(e.Text))
            .Select(e => new HistoricalEvent(e.Year!.Value, e.Text!))
            .ToList();
}
