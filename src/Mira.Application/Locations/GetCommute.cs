using MediatR;
using Mira.Application.Configuration;
using Mira.Domain.Locations;

namespace Mira.Application.Locations;

public interface IRouter
{
    // Driving time and distance, or null when no route is found. A Mapbox token enables live traffic.
    Task<Commute?> DriveAsync(string name, Location from, Location to, string? mapboxToken, CancellationToken ct);
}

public sealed record GetCommuteQuery(double Lat, double Lon) : IRequest<IReadOnlyList<Commute?>>, IValidated
{
    public string? Validate() => new Location(Lat, Lon).IsValid ? null : "invalid coordinates";
}

// One entry per configured destination, in order; null where routing failed.
public sealed class GetCommuteHandler(ISettingsStore settings, IRouter router) : IRequestHandler<GetCommuteQuery, IReadOnlyList<Commute?>>
{
    public async Task<IReadOnlyList<Commute?>> Handle(GetCommuteQuery q, CancellationToken ct)
    {
        var from = new Location(q.Lat, q.Lon);
        var cfg = await settings.GetAsync(ct);
        var routes = cfg.Commute.Select(async d =>
        {
            try { return await router.DriveAsync(d.Name, from, new Location(d.Lat, d.Lon), cfg.MapboxToken, ct); }
            catch (Exception e) when (e is HttpRequestException or TaskCanceledException or System.Text.Json.JsonException) { return null; }
        });
        return await Task.WhenAll(routes);
    }
}
