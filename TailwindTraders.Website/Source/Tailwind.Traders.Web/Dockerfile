# Phase 1: .NET 10 + Node 22 (hardening — chiseled/non-root tuning, scanning — comes in Phase 2)
ARG sdkTag=10.0
ARG runtimeTag=10.0
ARG image=mcr.microsoft.com/dotnet/aspnet
ARG sdkImage=mcr.microsoft.com/dotnet/sdk

FROM node:22-alpine AS build-node
WORKDIR /ClientApp
COPY ClientApp/package.json ClientApp/package-lock.json ./
RUN npm ci --legacy-peer-deps --no-audit --no-fund
COPY ClientApp/ .
RUN npm run build

FROM ${sdkImage}:${sdkTag} AS build-net
ENV BuildingDocker=true
WORKDIR /app
COPY *.csproj .
RUN dotnet restore
COPY . .
RUN dotnet publish -c Release -o /ttweb --no-restore

FROM ${image}:${runtimeTag} AS runtime
WORKDIR /web
COPY --from=build-net /ttweb .
COPY --from=build-node /ClientApp/build ./ClientApp/build
# .NET 8+ images listen on 8080 and provide a non-root 'app' user
USER $APP_UID
EXPOSE 8080
ENTRYPOINT ["dotnet", "Tailwind.Traders.Web.dll"]
