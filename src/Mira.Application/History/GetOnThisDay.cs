using MediatR;
using Mira.Domain.History;

namespace Mira.Application.History;

public interface IOnThisDayProvider
{
    Task<IReadOnlyList<HistoricalEvent>> GetAsync(int month, int day, CancellationToken ct);
}

public sealed record GetOnThisDayQuery(int Month, int Day) : IRequest<IReadOnlyList<HistoricalEvent>>, IValidated
{
    public string? Validate() => Month is < 1 or > 12 || Day is < 1 or > 31 ? "invalid date" : null;
}

public sealed class GetOnThisDayHandler(IOnThisDayProvider history) : IRequestHandler<GetOnThisDayQuery, IReadOnlyList<HistoricalEvent>>
{
    public Task<IReadOnlyList<HistoricalEvent>> Handle(GetOnThisDayQuery q, CancellationToken ct) => history.GetAsync(q.Month, q.Day, ct);
}
