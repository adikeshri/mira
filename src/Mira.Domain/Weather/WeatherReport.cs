namespace Mira.Domain.Weather;

public enum Units { Metric, Imperial }

public sealed record CurrentConditions(
    int Temp, int? Apparent, double? Humidity, double? WindSpeed, double? Uv, double? PrecipProb, int Code, bool IsDay);

public sealed record Today(int High, int Low, string Sunrise, string Sunset);

public sealed record DailyForecast(string Date, int High, int Low, int Code);

public sealed record WeatherReport(CurrentConditions Current, Today Today, IReadOnlyList<DailyForecast> Daily, int? Aqi);
