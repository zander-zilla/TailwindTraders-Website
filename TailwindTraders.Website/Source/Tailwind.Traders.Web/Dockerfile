# syntax=docker/dockerfile:1
# Phase 2: hardened multi-stage image (see docs/modernization/adr/0011-container-hardening.md)
#  - build stages: Node 22 (SPA) + .NET 10 SDK
#  - runtime: .NET 10 Ubuntu "chiseled" (distroless: no shell, no package manager, non-root by default)
#    "-extra" variant includes ICU + tzdata (needed by Microsoft.Data.SqlClient culture handling).
ARG DOTNET_VERSION=10.0
ARG NODE_VERSION=22

# ---------- 1. SPA build ----------
FROM node:${NODE_VERSION}-alpine AS build-node
WORKDIR /src/ClientApp
COPY ClientApp/package.json ClientApp/package-lock.json ./
RUN npm ci --legacy-peer-deps --no-audit --no-fund
COPY ClientApp/ ./
RUN npm run build && rm -rf node_modules

# ---------- 2. .NET publish ----------
FROM mcr.microsoft.com/dotnet/sdk:${DOTNET_VERSION} AS build-net
ENV BuildingDocker=true DOTNET_CLI_TELEMETRY_OPTOUT=1 DOTNET_NOLOGO=1
WORKDIR /src
COPY Tailwind.Traders.Web.csproj ./
RUN dotnet restore
COPY . .
RUN dotnet publish -c Release -o /app --no-restore /p:UseAppHost=false

# ---------- 3. Runtime (chiseled, non-root) ----------
FROM mcr.microsoft.com/dotnet/aspnet:${DOTNET_VERSION}-noble-chiseled-extra AS runtime
WORKDIR /app
COPY --from=build-net --chown=$APP_UID:$APP_UID /app ./
COPY --from=build-node --chown=$APP_UID:$APP_UID /src/ClientApp/build ./ClientApp/build
ENV ASPNETCORE_HTTP_PORTS=8080 \
    ASPNETCORE_ENVIRONMENT=Production \
    DOTNET_RUNNING_IN_CONTAINER=true
USER $APP_UID
EXPOSE 8080
# No HEALTHCHECK instruction: chiseled images have no shell/curl. Liveness is GET /healthz,
# probed by Kubernetes (Phase 4) or docker compose (see docker-compose.standalone.yml notes).
ENTRYPOINT ["dotnet", "Tailwind.Traders.Web.dll"]
