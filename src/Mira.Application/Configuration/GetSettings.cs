using MediatR;
using Mira.Domain.Configuration;

namespace Mira.Application.Configuration;

public interface ISettingsStore
{
    Task<MirrorSettings> GetAsync(CancellationToken ct);
}

public sealed record GetSettingsQuery : IRequest<MirrorSettings>;

public sealed class GetSettingsHandler(ISettingsStore store) : IRequestHandler<GetSettingsQuery, MirrorSettings>
{
    public Task<MirrorSettings> Handle(GetSettingsQuery q, CancellationToken ct) => store.GetAsync(ct);
}
