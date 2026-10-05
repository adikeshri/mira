using MediatR;
using Mira.Application.Network;

namespace Mira.Api.Endpoints;

public static class NetworkEndpoints
{
    // Raw bytes for the browser to time; the size is fixed server-side, never client-chosen.
    public static void MapNetwork(this IEndpointRouteBuilder api) =>
        api.MapGet("/network/speed-test", async (ISender mediator, CancellationToken ct) =>
            Results.Stream(await mediator.Send(new GetSpeedTestQuery(), ct), "application/octet-stream"));
}
