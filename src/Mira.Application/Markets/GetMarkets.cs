using MediatR;
using Mira.Application.Configuration;
using Mira.Domain.Markets;

namespace Mira.Application.Markets;

// Each source returns quotes keyed by the id the config uses; a missing key means "no quote".
public interface ICryptoQuotes
{
    Task<IReadOnlyDictionary<string, Quote>> GetAsync(IEnumerable<string> ids, string currency, CancellationToken ct);
}

public interface IFxQuotes
{
    Task<Quote?> GetAsync(string from, string to, CancellationToken ct);
}

public interface IIndexQuotes
{
    Task<Quote?> GetAsync(string symbol, CancellationToken ct);
}

public sealed record GetMarketsQuery : IRequest<IReadOnlyList<MarketRow>>;

public sealed class GetMarketsHandler(ISettingsStore settings, ICryptoQuotes crypto, IFxQuotes fx, IIndexQuotes indices)
    : IRequestHandler<GetMarketsQuery, IReadOnlyList<MarketRow>>
{
    public async Task<IReadOnlyList<MarketRow>> Handle(GetMarketsQuery q, CancellationToken ct)
    {
        var cfg = (await settings.GetAsync(ct)).Markets;

        // One source being down must not blank the others.
        var cryptoTask = cfg.Crypto.Count == 0
            ? Task.FromResult<IReadOnlyDictionary<string, Quote>>(new Dictionary<string, Quote>())
            : Safe(() => crypto.GetAsync(cfg.Crypto.Select(c => c.Id), cfg.CryptoCurrency, ct), new Dictionary<string, Quote>());
        var fxTasks = cfg.Fx.Select(p => Safe(() => fx.GetAsync(p.From, p.To, ct), null)).ToList();
        var indexTasks = cfg.Indices.Select(i => Safe(() => indices.GetAsync(i.Symbol, ct), null)).ToList();
        await Task.WhenAll([cryptoTask, .. fxTasks, .. indexTasks]);

        var cryptoQuotes = cryptoTask.Result;
        return
        [
            .. cfg.Crypto.Select(c => new MarketRow($"crypto:{c.Id}", c.Label, cfg.CryptoCurrency.ToUpperInvariant(), false,
                cryptoQuotes.GetValueOrDefault(c.Id))),
            .. cfg.Fx.Select((p, i) => new MarketRow($"fx:{p.From}{p.To}", p.Label ?? $"{p.From}/{p.To}", p.To, true, fxTasks[i].Result)),
            .. cfg.Indices.Select((x, i) => new MarketRow($"index:{x.Symbol}", x.Label, null, false, indexTasks[i].Result)),
        ];

        static async Task<T> Safe<T>(Func<Task<T>> source, T fallback)
        {
            try { return await source(); } catch (Exception e) when (e is HttpRequestException or TimeoutException or TaskCanceledException or System.Text.Json.JsonException) { return fallback; }
        }
    }
}
