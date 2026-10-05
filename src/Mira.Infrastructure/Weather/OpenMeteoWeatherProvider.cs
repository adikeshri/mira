using System.Globalization;
using System.Text.Json;
using Microsoft.Extensions.Caching.Memory;
using Mira.Application.Weather;
using Mira.Domain.Locations;
using Mira.Domain.Weather;

namespace Mira.Infrastructure.Weather;

public sealed class OpenMeteoWeatherProvider(HttpClient http, IMemoryCache cache) : IWeatherProvider
{
    private static readonly TimeSpan Ttl = TimeSpan.FromMinutes(10);
    private const string Current =
        "temperature_2m,apparent_temperature,relative_humidity_2m,wind_speed_10m,uv_index,precipitation_probability,weather_code,is_day";
    private const string Daily = "weather_code,temperature_2m_max,temperature_2m_min,sunrise,sunset";

    public Task<WeatherReport> GetAsync(Location at, Units units, CancellationToken ct)
    {
        // ~1 km precision is plenty for weather and keeps the cache key stable.
        var lat = Math.Round(at.Lat, 2).ToString(CultureInfo.InvariantCulture);
        var lon = Math.Round(at.Lon, 2).ToString(CultureInfo.InvariantCulture);
        return cache.Cached($"weather:{lat}:{lon}:{units}", Ttl, () => Load(lat, lon, units, ct));
    }

    private async Task<WeatherReport> Load(string lat, string lon, Units units, CancellationToken ct)
    {
        var imperial = units == Units.Imperial;
        var forecastUrl = $"https://api.open-meteo.com/v1/forecast?latitude={lat}&longitude={lon}&current={Current}&daily={Daily}" +
                          $"&temperature_unit={(imperial ? "fahrenheit" : "celsius")}&wind_speed_unit={(imperial ? "mph" : "kmh")}&timezone=auto&forecast_days=6";
        var aqiUrl = $"https://air-quality-api.open-meteo.com/v1/air-quality?latitude={lat}&longitude={lon}&current=us_aqi";

        var forecast = http.GetFromJsonAsync<JsonElement>(forecastUrl, ct);
        // AQI is a nice-to-have; never let it sink the forecast.
        var aqi = http.GetFromJsonAsync<JsonElement>(aqiUrl, ct).ContinueWith(t => t.IsCompletedSuccessfully ? Num(t.Result, "current", "us_aqi") : null, CancellationToken.None);
        return Map(await forecast, await aqi);
    }

    public static WeatherReport Map(JsonElement w, double? aqi)
    {
        var c = w.GetProperty("current");
        var d = w.GetProperty("daily");
        int[] Ints(string name) => d.GetProperty(name).EnumerateArray().Select(v => (int)Math.Round(v.GetDouble())).ToArray();
        string[] Strs(string name) => d.GetProperty(name).EnumerateArray().Select(v => v.GetString()!).ToArray();
        var (time, code, high, low, rise, set) = (Strs("time"), Ints("weather_code"), Ints("temperature_2m_max"), Ints("temperature_2m_min"), Strs("sunrise"), Strs("sunset"));

        int? Round(string name) => Num(c, name) is { } v ? (int)Math.Round(v) : null;
        return new WeatherReport(
            new CurrentConditions(
                Round("temperature_2m") ?? 0, Round("apparent_temperature"), Num(c, "relative_humidity_2m"), Num(c, "wind_speed_10m"),
                Num(c, "uv_index"), Num(c, "precipitation_probability"), (int)(Num(c, "weather_code") ?? 0), Num(c, "is_day") is not (null or 0)),
            new Today(high[0], low[0], rise[0], set[0]),
            time.Skip(1).Select((date, i) => new DailyForecast(date, high[i + 1], low[i + 1], code[i + 1])).ToList(),
            aqi is { } a ? (int)Math.Round(a) : null);
    }

    private static double? Num(JsonElement e, params string[] path)
    {
        foreach (var p in path) if (!e.TryGetProperty(p, out e)) return null;
        return e.ValueKind == JsonValueKind.Number ? e.GetDouble() : null;
    }
}
