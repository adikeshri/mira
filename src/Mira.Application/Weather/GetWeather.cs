using MediatR;
using Mira.Domain.Locations;
using Mira.Domain.Weather;

namespace Mira.Application.Weather;

public interface IWeatherProvider
{
    Task<WeatherReport> GetAsync(Location at, Units units, CancellationToken ct);
}

public sealed record GetWeatherQuery(double Lat, double Lon, Units Units) : IRequest<WeatherReport>, IValidated
{
    public string? Validate() => new Location(Lat, Lon).IsValid ? null : "invalid coordinates";
}

public sealed class GetWeatherHandler(IWeatherProvider weather) : IRequestHandler<GetWeatherQuery, WeatherReport>
{
    public Task<WeatherReport> Handle(GetWeatherQuery q, CancellationToken ct) => weather.GetAsync(new Location(q.Lat, q.Lon), q.Units, ct);
}
