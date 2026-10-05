using MediatR;
using Mira.Application.Weather;
using Mira.Domain.Weather;

namespace Mira.Api.Endpoints;

public static class WeatherEndpoints
{
    // units: "metric" (default) or "imperial".
    public static void MapWeather(this IEndpointRouteBuilder api) =>
        api.MapGet("/weather", async (double lat, double lon, string? units, ISender mediator, CancellationToken ct) =>
        {
            var parsed = Units.Metric;
            return units is null || Enum.TryParse(units, ignoreCase: true, out parsed)
                ? Results.Ok(await mediator.Send(new GetWeatherQuery(lat, lon, parsed), ct))
                : Results.BadRequest(new { error = "invalid units" });
        });
}
