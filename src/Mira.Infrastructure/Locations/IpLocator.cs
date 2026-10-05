using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Mira.Application.Locations;
using Mira.Domain.Locations;

namespace Mira.Infrastructure.Locations;

// City-level location from this server's public IP, via keyless services (they see the server's IP).
public sealed class IpLocator(HttpClient http, IMemoryCache cache, ILogger<IpLocator> log) : IIpLocator
{
    private static readonly TimeSpan Found = TimeSpan.FromHours(24), Retry = TimeSpan.FromMinutes(5);
    private static readonly string[] Providers =
    [
        "https://ipwho.is/?fields=success,latitude,longitude,city,region",
        "https://get.geojs.io/v1/ip/geo.json",
    ];

    public async Task<Location?> LocateAsync(CancellationToken ct)
    {
        if (cache.TryGetValue("iploc", out Location? hit)) return hit;
        var loc = await Lookup(ct);
        // A failure is remembered briefly so a dead service isn't hammered.
        cache.Set("iploc", loc, loc is null ? Retry : Found);
        return loc;
    }

    private async Task<Location?> Lookup(CancellationToken ct)
    {
        foreach (var url in Providers)
        {
            try
            {
                if (ToLocation(await http.GetFromJsonAsync<JsonElement>(url, ct)) is { } loc) return loc;
            }
            catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException)
            {
                log.LogWarning("ip location via {Host} failed: {Message}", new Uri(url).Host, e.Message);
            }
        }
        return null;
    }

    public static Location? ToLocation(JsonElement j)
    {
        if (j.TryGetProperty("success", out var ok) && ok.ValueKind == JsonValueKind.False) return null;
        if (Num(j, "latitude") is not { } lat || Num(j, "longitude") is not { } lon) return null;
        var name = string.Join(", ", new[] { Str(j, "city"), Str(j, "region") }.Where(s => s.Length > 0));
        var loc = new Location(lat, lon, name);
        return loc.IsValid ? loc : null;
    }

    // geojs sends numbers as strings.
    private static double? Num(JsonElement j, string key) =>
        !j.TryGetProperty(key, out var v) ? null
        : v.ValueKind == JsonValueKind.Number ? v.GetDouble()
        : v.ValueKind == JsonValueKind.String && double.TryParse(v.GetString(), CultureInfo.InvariantCulture, out var d) ? d
        : null;

    private static string Str(JsonElement j, string key) =>
        j.TryGetProperty(key, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString()!.Trim() : "";
}
