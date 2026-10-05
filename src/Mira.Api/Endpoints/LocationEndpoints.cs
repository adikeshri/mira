using MediatR;
using Mira.Application.Locations;

namespace Mira.Api.Endpoints;

public static class LocationEndpoints
{
    public static void MapLocation(this IEndpointRouteBuilder api)
    {
        // Estimate from the server's IP. 404 when the config disables it, 502 when no provider answers.
        api.MapGet("/location", async (ISender mediator, CancellationToken ct) => await mediator.Send(new GetIpLocationQuery(), ct) switch
        {
            { Disabled: true } => Results.NotFound(new { error = "disabled" }),
            { Location: { } loc } => Results.Ok(loc),
            _ => Results.Json(new { error = "location unavailable" }, statusCode: StatusCodes.Status502BadGateway),
        });

        api.MapGet("/places/reverse", async (double lat, double lon, ISender mediator, CancellationToken ct) =>
            new { name = await mediator.Send(new GetPlaceNameQuery(lat, lon), ct) });
    }
}
