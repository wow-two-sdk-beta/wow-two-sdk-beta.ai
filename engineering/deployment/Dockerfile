# syntax=docker/dockerfile:1

# One image serving the Ai service: the .NET host with the Form 1 module, and the hosted pages and console. The
# build context MUST be engineering/codebase/, which holds both ai.backend-services and ai.frontend-services.
#
#   docker build --build-arg APP_VERSION=0.1.99 --build-arg VCS_REF=$(git rev-parse HEAD) \
#     -f engineering/deployment/Dockerfile -t ai:local engineering/codebase
#
# The image holds no environment: the PostgreSQL connection string arrives at run time, through the environment's
# settings file (/app/appsettings.Target.json) or the DB_CONNECTION variable. `dotnet Ai.Api.dll migrate` is the
# deploy's migrate step, run from this same image before the new release serves.

# ---- Stage 1: build the frontend workspace (the fe-ai package first, then apps/web) ----
FROM node:24.11.0-alpine AS spa
RUN npm install --global pnpm@10.33.2
WORKDIR /src/ai.frontend-services
COPY ai.frontend-services/package.json ai.frontend-services/pnpm-lock.yaml ai.frontend-services/pnpm-workspace.yaml ./
COPY ai.frontend-services/apps/web/package.json ./apps/web/package.json
COPY ai.frontend-services/packages/fe-ai/package.json ./packages/fe-ai/package.json
RUN pnpm install --frozen-lockfile
COPY ai.frontend-services/ ./
# The build's version from the shared publish workflow (X.Y.Z or a pre-release); unset in a local build. Declared
# after the dependency layers, since a changed build argument misses the cache of every RUN after it.
ARG APP_VERSION
RUN pnpm build

# ---- Stage 2: publish the .NET service with its packages (the pages arrive from stage 1) ----
# Stays in global.json's 10.0.3xx band; the floating 10.0 tag moved to 10.0.4xx, which the pin refuses.
FROM mcr.microsoft.com/dotnet/sdk:10.0.302 AS build
ARG TARGETARCH
WORKDIR /src
# packages/ and service/ build together: the service takes the domain's packages by ProjectReference.
COPY ai.backend-services/ ./
COPY --from=spa /src/ai.frontend-services/apps/web/dist ./service/Ai.Api/wwwroot
# MSBuild reads APP_VERSION from the environment; Directory.Build.props turns it into the version. VCS_REF is the full
# commit SHA the shared publish workflow passes; the build appends it to the informational version.
ARG APP_VERSION
ARG VCS_REF
RUN case "$TARGETARCH" in amd64) rid=linux-x64 ;; arm64) rid=linux-arm64 ;; *) exit 1 ;; esac \
    && dotnet publish service/Ai.Api/Ai.Api.csproj -c Release -o /app -r "$rid" --self-contained false \
       -p:BuildSpa=false -p:SourceRevisionId="$VCS_REF"

# ---- Stage 3: runtime ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
RUN apt-get update && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*
WORKDIR /app
COPY --from=build /app ./

ENV ASPNETCORE_HTTP_PORTS=8080
USER app
EXPOSE 8080
HEALTHCHECK --interval=15s --timeout=5s --start-period=60s --retries=4 \
    CMD curl --fail --silent http://localhost:8080/health || exit 1
ENTRYPOINT ["dotnet", "Ai.Api.dll"]
