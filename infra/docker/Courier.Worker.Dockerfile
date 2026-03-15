# =============================================================================
# Courier Worker Host — Multi-stage Docker Build
# =============================================================================
# Build:  docker build -f infra/docker/Courier.Worker.Dockerfile -t courier-worker .
# FIPS:   docker build -f infra/docker/Courier.Worker.Dockerfile --build-arg FIPS_ENABLED=true -t courier-worker .
# Run:    docker run -e ConnectionStrings__CourierDb="..." courier-worker
# =============================================================================

# ---------------------------------------------------------------------------
# Stage 1: Restore + Publish
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy build infrastructure files first (maximizes layer cache hits)
COPY global.json Directory.Build.props Directory.Packages.props Courier.slnx ./

# Copy only .csproj files for the restore layer
COPY src/Courier.Worker/Courier.Worker.csproj src/Courier.Worker/
COPY src/Courier.Domain/Courier.Domain.csproj src/Courier.Domain/
COPY src/Courier.Infrastructure/Courier.Infrastructure.csproj src/Courier.Infrastructure/
COPY src/Courier.Features/Courier.Features.csproj src/Courier.Features/
COPY src/Courier.Migrations/Courier.Migrations.csproj src/Courier.Migrations/
COPY src/Courier.ServiceDefaults/Courier.ServiceDefaults.csproj src/Courier.ServiceDefaults/

RUN dotnet restore src/Courier.Worker/Courier.Worker.csproj

# Copy all source code and publish
COPY src/ src/
RUN dotnet publish src/Courier.Worker/Courier.Worker.csproj -c Release -o /app --no-restore

# ---------------------------------------------------------------------------
# Stage 2: Runtime
# ---------------------------------------------------------------------------
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# Install p7zip for archive operations and curl for health checks
RUN apt-get update \
    && apt-get install -y --no-install-recommends p7zip-full curl \
    && rm -rf /var/lib/apt/lists/*

# FIPS 140-2 support: install OpenSSL FIPS config when enabled
ARG FIPS_ENABLED=false
COPY infra/docker/openssl-fips.cnf /tmp/openssl-fips.cnf
RUN if [ "$FIPS_ENABLED" = "true" ]; then \
        cp /tmp/openssl-fips.cnf /etc/ssl/openssl.cnf; \
    fi && rm /tmp/openssl-fips.cnf

# Create temp workspace directory for job execution
RUN mkdir -p /data/courier/temp && chown -R app:app /data/courier
VOLUME ["/data/courier/temp"]

COPY --from=build /app .

ENV DOTNET_ENVIRONMENT=Production
ENV ASPNETCORE_URLS=http://+:8081
ENV Workspace__BaseDirectory=/data/courier/temp
EXPOSE 8081

# Run as non-root (app user provided by base image)
USER app

HEALTHCHECK --interval=30s --timeout=5s --start-period=15s --retries=3 \
    CMD curl -f http://localhost:8081/health || exit 1

ENTRYPOINT ["dotnet", "Courier.Worker.dll"]
