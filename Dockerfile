# Build context is the repository root (not src/RaqmiSystem.Api) because the API project
# references Domain/Application/Infrastructure via relative ProjectReference paths and relies
# on the root-level Directory.Build.props / Directory.Packages.props for shared build settings
# and central package management.

# ---- Build stage ----
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution-level files first so `dotnet restore` is cached unless dependencies change.
COPY global.json ./
COPY Directory.Build.props Directory.Packages.props ./
COPY RaqmiSystem.sln ./
COPY src/RaqmiSystem.Domain/RaqmiSystem.Domain.csproj src/RaqmiSystem.Domain/
COPY src/RaqmiSystem.Application/RaqmiSystem.Application.csproj src/RaqmiSystem.Application/
COPY src/RaqmiSystem.Infrastructure/RaqmiSystem.Infrastructure.csproj src/RaqmiSystem.Infrastructure/
COPY src/RaqmiSystem.Api/RaqmiSystem.Api.csproj src/RaqmiSystem.Api/

RUN dotnet restore src/RaqmiSystem.Api/RaqmiSystem.Api.csproj

# Copy the remaining source and publish.
COPY src/RaqmiSystem.Domain/ src/RaqmiSystem.Domain/
COPY src/RaqmiSystem.Application/ src/RaqmiSystem.Application/
COPY src/RaqmiSystem.Infrastructure/ src/RaqmiSystem.Infrastructure/
COPY src/RaqmiSystem.Api/ src/RaqmiSystem.Api/

RUN dotnet publish src/RaqmiSystem.Api/RaqmiSystem.Api.csproj -c Release -o /app/publish --no-restore

# ---- Runtime stage ----
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# curl is used by the HEALTHCHECK below to probe the API's own /health endpoint.
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

# Run as a non-root user.
RUN groupadd --system raqmi \
    && useradd --system --gid raqmi --home-dir /app --shell /usr/sbin/nologin raqmi

COPY --from=build /app/publish .
RUN chown -R raqmi:raqmi /app

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

HEALTHCHECK --interval=30s --timeout=5s --start-period=20s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1

USER raqmi

ENTRYPOINT ["dotnet", "RaqmiSystem.Api.dll"]
