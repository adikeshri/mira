using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Mira.Application.Locations;
using Mira.Domain.Locations;

namespace Mira.Infrastructure.Locations;

// Nominatim's policy asks for light use: one lookup per ~1 km cell, cached for a day.
public sealed class NominatimPlaceNamer(HttpClient http, IMemoryCache cache) : IPlaceNamer
{
    public Task<string?> NameAsync(Location at, CancellationToken ct)
    {
        var lat = Math.Round(at.Lat, 2).ToString(CultureInfo.InvariantCulture);
        var lon = Math.Round(at.Lon, 2).ToString(CultureInfo.InvariantCulture);
        return cache.Cached($"place:{lat}:{lon}", TimeSpan.FromHours(24), async () =>
        {
            var j = await http.GetFromJsonAsync<JsonElement>(
                $"https://nominatim.openstreetmap.org/reverse?lat={lat}&lon={lon}&format=json&zoom=10&accept-language=en", ct);
            return Name(j);
        });
    }

    public static string? Name(JsonElement j)
    {
        if (!j.TryGetProperty("address", out var a)) return null;
        string? Get(string k) => a.TryGetProperty(k, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
        var city = Get("city") ?? Get("town") ?? Get("village") ?? Get("hamlet") ?? Get("county");
        var name = string.Join(", ", new[] { city, Get("state") }.Where(s => !string.IsNullOrEmpty(s)));
        return name.Length > 0 ? name : null;
    }
}
