using MediatR;
using Mira.Application.Markets;

namespace Mira.Api.Endpoints;

public static class MarketsEndpoints
{
    // Crypto, fx and index rows assembled from the configured lists, in config order.
    public static void MapMarkets(this IEndpointRouteBuilder api) =>
        api.MapGet("/markets", (ISender mediator, CancellationToken ct) => mediator.Send(new GetMarketsQuery(), ct));
}
