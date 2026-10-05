using MediatR;
using Mira.Application.News;

namespace Mira.Api.Endpoints;

public static class NewsEndpoints
{
    // { world: [...], local: [...] }, parsed, merged and newest-first.
    public static void MapNews(this IEndpointRouteBuilder api) =>
        api.MapGet("/news", (ISender mediator, CancellationToken ct) => mediator.Send(new GetNewsQuery(), ct));
}
