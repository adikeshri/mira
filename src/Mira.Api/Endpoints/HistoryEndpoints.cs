using MediatR;
using Mira.Application.History;

namespace Mira.Api.Endpoints;

public static class HistoryEndpoints
{
    public static void MapHistory(this IEndpointRouteBuilder api) =>
        api.MapGet("/on-this-day", (int month, int day, ISender mediator, CancellationToken ct) =>
            mediator.Send(new GetOnThisDayQuery(month, day), ct));
}
