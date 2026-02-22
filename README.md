# Courier

Enterprise file transfer & job management platform. Replaces manual SFTP, PGP, cron, and ad-hoc scripts with a unified, auditable, secure platform.

## Quick Start (Aspire)

Prerequisites: [.NET 10 SDK](https://dotnet.microsoft.com/download), [Docker Desktop](https://www.docker.com/products/docker-desktop/), [Node.js 20+](https://nodejs.org/).

```bash
# Install frontend dependencies (first time only)
cd src/Courier.Frontend
npm install
cd ../..

# Start everything with Aspire
cd src/Courier.AppHost
dotnet run
```

This single command starts:

| Service | URL | Description |
|---------|-----|-------------|
| Aspire Dashboard | https://localhost:15888 | Service orchestration dashboard |
| API | http://localhost:5000 (assigned by Aspire) | REST API (Swagger at /swagger) |
| Worker | — | Background job processor |
| Frontend | http://localhost:3000 | Next.js UI |
| Seq | http://localhost:5341 | Structured log viewer |
| PostgreSQL | localhost:5432 | Database (managed by Aspire) |

> Actual ports may differ — check the Aspire dashboard for the assigned endpoints.

## Build & Test

```bash
# Build everything
dotnet build Courier.slnx

# Run all tests
dotnet test Courier.slnx

# Run specific test suites
dotnet test tests/Courier.Tests.Unit            # Fast, no Docker needed
dotnet test tests/Courier.Tests.Architecture    # Dependency rule enforcement
dotnet test tests/Courier.Tests.Integration     # Requires Docker (Testcontainers)
```

## Example API Usage

```bash
# Create a job
curl -X POST http://localhost:5000/api/v1/jobs \
  -H "Content-Type: application/json" \
  -d '{"name": "Daily SFTP Upload", "description": "Transfers reports to partner"}'

# List jobs
curl http://localhost:5000/api/v1/jobs

# Get a specific job
curl http://localhost:5000/api/v1/jobs/{id}

# Health checks
curl http://localhost:5000/health        # Liveness
curl http://localhost:5000/health/ready  # Readiness (includes PostgreSQL)
```

All endpoints return an `ApiResponse<T>` envelope:

```json
{
  "data": { "id": "...", "name": "Daily SFTP Upload", ... },
  "success": true,
  "timestamp": "2026-02-21T12:00:00Z"
}
```

## Project Structure

```
src/
  Courier.AppHost/          Aspire orchestrator (start here)
  Courier.Api/              ASP.NET Core Web API host
  Courier.Worker/           .NET Worker Service host
  Courier.Features/         Vertical slices (Jobs, etc.)
  Courier.Domain/           Entities, value objects, envelopes (BCL-only)
  Courier.Infrastructure/   EF Core DbContext, data access
  Courier.Migrations/       DbUp SQL scripts + MigrationRunner
  Courier.ServiceDefaults/  Shared Aspire defaults (OTel, health, resilience)
  Courier.Frontend/         Next.js TypeScript UI

tests/
  Courier.Tests.Unit/           xUnit + Shouldly unit tests
  Courier.Tests.Integration/    Testcontainers + WebApplicationFactory
  Courier.Tests.Architecture/   NetArchTest dependency enforcement
```

## Architecture

- **Vertical slice**: Each feature (Jobs, Connections, Keys, Monitors) owns its full stack
- **Dependency direction**: Domain <- Infrastructure <- Features <- Api/Worker
- **Database**: PostgreSQL 16+ with DbUp migrations (raw SQL, not EF migrations)
- **ORM**: EF Core for queries only; DbUp for schema changes
- **Migrations**: API host only (Worker validates schema version)
- **Orchestration**: .NET Aspire manages all services locally

## Design Doc

Full specification: [Docs/CourierDesignDoc.md](Docs/CourierDesignDoc.md)
Architecture notes: [ARCHITECTURE_NOTES.md](ARCHITECTURE_NOTES.md)
