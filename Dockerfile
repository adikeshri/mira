# Builds Mira and, via the named build context "ui", the mirror-magic UI, then serves both from one container.
#   docker build --build-context ui=../mirror-magic -t mira .
# (docker compose does this for you, see docker-compose.yml.)
#
# Multi-arch: the build stages run on the build machine and produce architecture-independent output, so the
# same Dockerfile builds natively on a Raspberry Pi (arm64 or armv7) or cross-builds from a laptop.

FROM --platform=$BUILDPLATFORM node:22-alpine AS ui-build
WORKDIR /ui
COPY --from=ui package.json package-lock.json ./
RUN npm ci
COPY --from=ui . .
# No VITE_MIRA_URL: the UI calls Mira on its own origin.
RUN npm run build

FROM --platform=$BUILDPLATFORM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src
COPY Mira.sln ./
COPY src/Mira.Domain/*.csproj src/Mira.Domain/
COPY src/Mira.Application/*.csproj src/Mira.Application/
COPY src/Mira.Infrastructure/*.csproj src/Mira.Infrastructure/
COPY src/Mira.Api/*.csproj src/Mira.Api/
COPY tests/Mira.Tests/*.csproj tests/Mira.Tests/
RUN dotnet restore
COPY . .
# Framework-dependent publish: no runtime identifier needed, so it runs on any CPU the runtime image supports.
RUN dotnet publish src/Mira.Api -c Release -o /out --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:9.0-alpine
WORKDIR /app
COPY --from=build /out ./
COPY --from=ui-build /ui/dist ./ui

ENV Urls=http://+:5080 \
    Mira__UiPath=/app/ui \
    Mira__ConfigPath=/config/config.json \
    DOTNET_SYSTEM_GLOBALIZATION_INVARIANT=true
# Unprivileged user that ships with the image.
USER $APP_UID
EXPOSE 5080

HEALTHCHECK --interval=30s --timeout=5s --start-period=15s --retries=3 \
  CMD wget -qO- http://127.0.0.1:5080/health >/dev/null || exit 1

ENTRYPOINT ["dotnet", "Mira.Api.dll"]
