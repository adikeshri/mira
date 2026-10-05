using MediatR;
using Mira.Application.Configuration;

namespace Mira.Api.Endpoints;

public static class ConfigEndpoints
{
    public static void MapConfig(this IEndpointRouteBuilder api) =>
        api.MapGet("/config", (ISender mediator, CancellationToken ct) => mediator.Send(new GetSettingsQuery(), ct));
}
