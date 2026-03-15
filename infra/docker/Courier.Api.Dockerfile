# =============================================================================
# Courier API Host — Multi-stage Docker Build
# =============================================================================
# Build:  docker build -f infra/docker/Courier.Api.Dockerfile -t courier-api .
# FIPS:   docker build -f infra/docker/Courier.Api.Dockerfile --build-arg FIPS_ENABLED=true -t courier-api .
# Run:    docker run -p 8080:8080 -e ConnectionStrings__CourierDb="..." courier-api
# =============================================================================

# ---------------------------------------------------------------------------
# Stage 1: Restore + Publish
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy build infrastructure files first (maximizes layer cache hits)
COPY global.json Directory.Build.props Directory.Packages.props Courier.slnx ./

# Copy only .csproj files for the restore layer
COPY src/Courier.Api/Courier.Api.csproj src/Courier.Api/
COPY src/Courier.Domain/Courier.Domain.csproj src/Courier.Domain/
COPY src/Courier.Infrastructure/Courier.Infrastructure.csproj src/Courier.Infrastructure/
COPY src/Courier.Features/Courier.Features.csproj src/Courier.Features/
COPY src/Courier.Migrations/Courier.Migrations.csproj src/Courier.Migrations/
COPY src/Courier.ServiceDefaults/Courier.ServiceDefaults.csproj src/Courier.ServiceDefaults/

RUN dotnet restore src/Courier.Api/Courier.Api.csproj

# Copy all source code and publish
COPY src/ src/
RUN dotnet publish src/Courier.Api/Courier.Api.csproj -c Release -o /app --no-restore

# ---------------------------------------------------------------------------
# Stage 2: Runtime
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Install curl for health check probes
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/*

# FIPS 140-2 support: install OpenSSL FIPS config when enabled
ARG FIPS_ENABLED=false
COPY infra/docker/openssl-fips.cnf /tmp/openssl-fips.cnf
RUN if [ "$FIPS_ENABLED" = "true" ]; then \
        cp /tmp/openssl-fips.cnf /etc/ssl/openssl.cnf; \
    fi && rm /tmp/openssl-fips.cnf

COPY --from=build /app .

ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080

# Run as non-root (app user provided by base image)
USER app

HEALTHCHECK --interval=30s --timeout=5s --start-period=15s --retries=3 \
    CMD curl -f http://localhost:8080/health || exit 1

ENTRYPOINT ["dotnet", "Courier.Api.dll"]
