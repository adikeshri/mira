# Mira

.NET 9 backend-for-frontend for [mirror-magic](../mirror-magic). Serves the data the browser can't fetch itself (CORS) and
pre-assembles what the UI would otherwise stitch together from many third-party APIs.

## Layout (Clean Architecture, dependencies point inward)

| Project | Holds |
|---|---|
| `Mira.Domain` | Records and rules, no dependencies: `Quote`, `WeatherReport`, `NewsItem`, `MirrorSettings`, ... |
| `Mira.Application` | CQRS with MediatR 12 (last Apache-licensed line): one query + handler per use case (`GetWeatherQuery`, `GetMarketsQuery`, ...), the ports they depend on (`IWeatherProvider`, `IFeedReader`, ...), and a `ValidationBehavior` pipeline step that rejects malformed queries (`IValidated`) with a 400 |
| `Mira.Infrastructure` | Adapters: Open-Meteo, Nominatim, CoinGecko, Frankfurter, Yahoo, Wikipedia, RSS, `config.json`; caching |
| `Mira.Api` | Minimal-API endpoints, one file per domain; they only `Send` queries through `ISender` |

Each layer is organised by domain: Weather, Locations, Markets, News, History, Configuration.

## Endpoints

| Route | Replaces |
|---|---|
| `GET /api/config` | `/api/config` |
| `GET /api/location` | `/api/location` (IP estimate; 404 if `autoLocation` is off) |
| `GET /api/places/reverse?lat&lon` | browser call to Nominatim |
| `GET /api/weather?lat&lon&units=metric\|imperial` | browser calls to Open-Meteo forecast + air quality |
| `GET /api/markets` | `/api/indices` + browser calls to CoinGecko and Frankfurter; returns ready-made rows |
| `GET /api/news` | `/api/feed/{n}` + client-side RSS parsing; returns `{ world, local }` |
| `GET /api/on-this-day?month&day` | browser call to Wikipedia |
| `GET /health` | |

Swagger UI: `/swagger` (spec at `/swagger/v1/swagger.json`).

The internet-speed test stays in the browser, since it measures the client's connection.

## Run

```bash
cp config.example.json config.json   # optional; defaults apply without it
dotnet run --project src/Mira.Api    # http://127.0.0.1:5080
dotnet test
```

Config path: `Mira:ConfigPath` (env `Mira__ConfigPath`). `config.json` is re-read on every request.
Only feeds listed in `config.json` are ever fetched; clients never supply a URL.
