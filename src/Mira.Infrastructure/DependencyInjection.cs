using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Mira.Application.Configuration;
using Mira.Application.History;
using Mira.Application.Locations;
using Mira.Application.Markets;
using Mira.Application.News;
using Mira.Application.Weather;
using Mira.Infrastructure.Configuration;
using Mira.Infrastructure.History;
using Mira.Infrastructure.Locations;
using Mira.Infrastructure.Markets;
using Mira.Infrastructure.News;
using Mira.Infrastructure.Weather;

namespace Mira.Infrastructure;

public static class DependencyInjection
{
    private const string UserAgent = "mira (magic-mirror BFF)";
    // Yahoo answers 429 to anything that doesn't look like a browser.
    private const string BrowserUserAgent = "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/124 Safari/537.36";

    public static IServiceCollection AddInfrastructure(this IServiceCollection s, string configPath)
    {
        s.AddMemoryCache();
        s.AddSingleton(TimeProvider.System);
        s.AddSingleton<ISettingsStore>(p => new FileSettingsStore(configPath, p.GetRequiredService<ILogger<FileSettingsStore>>()));

        s.AddHttpClient<IWeatherProvider, OpenMeteoWeatherProvider>(Client(10));
        s.AddHttpClient<IIpLocator, IpLocator>(Client(8));
        s.AddHttpClient<IPlaceNamer, NominatimPlaceNamer>(Client(10));
        s.AddHttpClient<IOnThisDayProvider, WikipediaOnThisDayProvider>(Client(10));
        s.AddHttpClient<ICryptoQuotes, CoinGeckoQuotes>(Client(10));
        s.AddHttpClient<IFxQuotes, FrankfurterFxQuotes>(Client(10));
        s.AddHttpClient<IFeedReader, RssFeedReader>(Client(15));

        s.AddHttpClient(YahooIndexQuotes.ClientName, c =>
        {
            c.Timeout = TimeSpan.FromSeconds(10);
            c.DefaultRequestHeaders.UserAgent.ParseAdd(BrowserUserAgent);
        });
        s.AddSingleton<IIndexQuotes, YahooIndexQuotes>();
        return s;

        static Action<HttpClient> Client(int timeoutSeconds) => c =>
        {
            c.Timeout = TimeSpan.FromSeconds(timeoutSeconds);
            c.DefaultRequestHeaders.UserAgent.ParseAdd(UserAgent);
        };
    }
}
