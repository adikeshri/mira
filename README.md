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
| `GET /api/commute?lat&lon` | driving time to each `commute` destination in `config.json` (live traffic with a Mapbox token, else free-flow via OSRM) |
| `GET /api/calendar` | today's events only, merged from the iCal URLs in `config.json` `calendars` (all-day first, then by start; "today" follows `timeZone`) |
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

### Docker (Raspberry Pi)

One image holds Mira and the built UI. Needs a 64-bit OS on a Pi 3/4/5 (32-bit also works with the armv7 runtime image),
Docker with Compose, and the `mirror-magic` repo checked out next to this one.

```bash
git clone https://github.com/adikeshri/mira.git && git clone https://github.com/adikeshri/mirror-magic.git
cd mira
cp config.example.json config.json   # then edit it
docker compose up -d --build         # http://127.0.0.1:5080
```

- The image builds on the device, so it always matches the CPU; building on a laptop for another architecture also works (the
  build output is CPU-independent).
- `config.json` is mounted read-only and re-read on every request: edit it and reload the page, no rebuild.
- Port 5080 is published on `127.0.0.1` only. For other devices on your network, change the mapping to `"5080:5080"`.
- Runs as an unprivileged user, read-only filesystem, all capabilities dropped; `restart: unless-stopped` and a health check.
- Update: the UI comes from the `mirror-magic` checkout next to this repo, so `git pull` in **both** repos (and make sure
  `mirror-magic` is on the branch you want), then `docker compose up -d --build`. Plain `docker compose up -d` reuses the
  old image. To always pick up the latest UI from GitHub without a second checkout, run with
  `UI_CONTEXT=https://github.com/adikeshri/mirror-magic.git#main docker compose up -d --build`.
- After an update the kiosk browser may need one reload (Ctrl+Shift+R) or a restart if it cached the old page before this
  was fixed; from then on the page is revalidated on every load.
- Kiosk: `chromium-browser --kiosk --noerrdialogs --disable-infobars http://127.0.0.1:5080`.
- Without Compose: `docker build --build-context ui=../mirror-magic -t mira .`

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

Commute traffic: put a free [Mapbox](https://account.mapbox.com) public token in `config.json` as `"mapboxToken"` (Directions API, results cached 5 min).
Without it, `/api/commute` falls back to free-flow estimates and omits `typicalMinutes` (the usual drive time the UI compares traffic against). The token is never returned by `/api/config`.

Calendars: list iCal (`.ics`) URLs in `config.json` as `"calendars": [{ "name", "url" }]`; they are merged by `/api/calendar`.
The URLs are secrets (anyone with the link can read the calendar): they stay server-side and are stripped from `/api/config`.
Google: Settings, your calendar, Integrate calendar, *Secret address in iCal format*. Outlook.com: Settings, Calendar, Shared calendars, Publish a calendar, ICS link.
