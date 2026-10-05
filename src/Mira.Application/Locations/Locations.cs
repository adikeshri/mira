using MediatR;
using Mira.Application.Configuration;
using Mira.Domain.Locations;

namespace Mira.Application.Locations;

public interface IIpLocator
{
    Task<Location?> LocateAsync(CancellationToken ct);
}

public interface IPlaceNamer
{
    Task<string?> NameAsync(Location at, CancellationToken ct);
}

// Approximate location of this server. Disabled when the config forbids sending its IP out.
public sealed record IpLocation(Location? Location, bool Disabled);

public sealed record GetIpLocationQuery : IRequest<IpLocation>;

public sealed class GetIpLocationHandler(ISettingsStore settings, IIpLocator locator) : IRequestHandler<GetIpLocationQuery, IpLocation>
{
    public async Task<IpLocation> Handle(GetIpLocationQuery q, CancellationToken ct) =>
        (await settings.GetAsync(ct)).AutoLocation
            ? new IpLocation(await locator.LocateAsync(ct), false)
            : new IpLocation(null, true);
}

public sealed record GetPlaceNameQuery(double Lat, double Lon) : IRequest<string?>, IValidated
{
    public string? Validate() => new Location(Lat, Lon).IsValid ? null : "invalid coordinates";
}

public sealed class GetPlaceNameHandler(IPlaceNamer places) : IRequestHandler<GetPlaceNameQuery, string?>
{
    public Task<string?> Handle(GetPlaceNameQuery q, CancellationToken ct) => places.NameAsync(new Location(q.Lat, q.Lon), ct);
}
