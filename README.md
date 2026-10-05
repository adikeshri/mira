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
| `GET /api/network/speed-test` | browser download from Cloudflare (2.5 MB, relayed) |
| `GET /health` | |

Swagger UI: `/swagger` (spec at `/swagger/v1/swagger.json`).

The speed test is relayed, so it measures the slower of Mira's internet link and the mirror-to-Mira link; accurate when both are on one LAN.

## Run

```bash
cp config.example.json config.json   # optional; defaults apply without it
dotnet run --project src/Mira.Api    # http://127.0.0.1:5080
dotnet test
```

### Hosting the UI

Mira can serve mirror-magic's built UI itself, so nothing else needs to run:

```bash
(cd ../mirror-magic && npm ci && npm run build)
Mira__UiPath=$PWD/../mirror-magic/dist dotnet run --project src/Mira.Api   # UI and API on :5080
```

For UI development, run `npm run dev` in mirror-magic (port 8080) with `VITE_MIRA_URL=http://127.0.0.1:5080`;
Mira allows that origin via CORS (`Mira:AllowedOrigins`, GET only). When serving the UI, responses carry a strict CSP.

Config path: `Mira:ConfigPath` (env `Mira__ConfigPath`). `config.json` is re-read on every request.
Only feeds listed in `config.json` are ever fetched; clients never supply a URL.
