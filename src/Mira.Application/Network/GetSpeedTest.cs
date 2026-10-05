using MediatR;

namespace Mira.Application.Network;

public interface ISpeedTestSource
{
    // A fixed-size payload the client can time. The caller disposes the stream.
    Task<Stream> OpenAsync(CancellationToken ct);
}

public sealed record GetSpeedTestQuery : IRequest<Stream>;

public sealed class GetSpeedTestHandler(ISpeedTestSource source) : IRequestHandler<GetSpeedTestQuery, Stream>
{
    public Task<Stream> Handle(GetSpeedTestQuery q, CancellationToken ct) => source.OpenAsync(ct);
}
