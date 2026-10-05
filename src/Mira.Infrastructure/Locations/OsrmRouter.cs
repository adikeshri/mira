using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Mira.Application.Locations;
using Mira.Domain.Locations;

namespace Mira.Infrastructure.Locations;

// With a Mapbox token: live-traffic drive times. Without: keyless OSRM demo server, free-flow only.
// Both answer routes[0].duration (s) and distance (m); Mapbox adds duration_typical (s). The token travels in the URL; HttpClient logging stays at Warning.
public sealed class OsrmRouter(HttpClient http, IMemoryCache cache) : IRouter
{

    public Task<Commute?> DriveAsync(string name, Location from, Location to, string? mapboxToken, CancellationToken ct)
    {
        string F(double d) => d.ToString("0.####", CultureInfo.InvariantCulture);
        var path = $"{F(from.Lon)},{F(from.Lat)};{F(to.Lon)},{F(to.Lat)}";
        var url = string.IsNullOrEmpty(mapboxToken)
            ? $"https://router.project-osrm.org/route/v1/driving/{path}?overview=false"
            : $"https://api.mapbox.com/directions/v5/mapbox/driving-traffic/{path}?overview=false&access_token={Uri.EscapeDataString(mapboxToken)}";
        return cache.Cached($"route:{path}", string.IsNullOrEmpty(mapboxToken) ? TimeSpan.FromMinutes(30) : TimeSpan.FromMinutes(5), async () => Map(name, await http.GetFromJsonAsync<JsonElement>(url, ct)));
    }

    public static Commute? Map(string name, JsonElement j) =>
        j.TryGetProperty("routes", out var r) && r.GetArrayLength() > 0
            ? new Commute(name, (int)Math.Round(r[0].GetProperty("duration").GetDouble() / 60), Math.Round(r[0].GetProperty("distance").GetDouble() / 1000, 1),
                r[0].TryGetProperty("duration_typical", out var t) && t.ValueKind == JsonValueKind.Number ? (int)Math.Round(t.GetDouble() / 60) : null)
            : null;
}
