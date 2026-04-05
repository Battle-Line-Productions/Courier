# Courier

[![License](https://img.shields.io/badge/License-Apache%202.0-blue.svg)](LICENSE)
[![CI](https://github.com/Battle-Line-Productions/Courier/actions/workflows/ci.yml/badge.svg)](https://github.com/Battle-Line-Productions/Courier/actions/workflows/ci.yml)

Open-source enterprise Managed File Transfer (MFT) platform. Replaces manual SFTP, PGP, cron, and ad-hoc scripts with a unified, auditable, secure platform.

### Features

- **Multi-protocol transfers** — SFTP, FTP, FTPS with connection pooling and host key management
- **Job engine** — 30+ step types: file transfer, PGP encrypt/decrypt/sign/verify, compression, Azure Functions, flow control (if/else/foreach)
- **Job chaining** — Orchestrate multi-job sequences with dependency management
- **Scheduling** — Cron and one-time schedules via Quartz.NET
- **File monitoring** — Poll local or remote paths, trigger jobs on file events
- **PGP & SSH key management** — Generate, import, export, rotate, and share keys with full lifecycle tracking
- **Notifications** — Email and webhook alerts on job/chain/monitor events
- **Security** — RBAC (Admin/Operator/Viewer), OIDC & SAML SSO, AES-256-GCM envelope encryption, full audit trail
- **Tagging** — Organize all resources with colored, categorized tags
- **Dashboard** — Real-time metrics, recent executions, active monitors, key expiry alerts

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

## Documentation

- [Design Document](Docs/CourierDesignDoc.md) — Full system specification
- [Architecture Notes](ARCHITECTURE_NOTES.md) — Key architectural decisions
- [Contributing Guide](CONTRIBUTING.md) — How to contribute
- [Changelog](https://github.com/Battle-Line-Productions/Courier/releases) — Release history

## Contributing

We welcome contributions! See [CONTRIBUTING.md](CONTRIBUTING.md) for the full guide. Here's the quick version:

1. **Report bugs** — [Open a bug report](https://github.com/Battle-Line-Productions/Courier/issues/new?template=bug_report.yml)
2. **Request features** — [Open a feature request](https://github.com/Battle-Line-Productions/Courier/issues/new?template=feature_request.yml)
3. **Submit code** — Fork, branch from `main`, make your changes, open a PR
4. **Ask questions** — [Start a discussion](https://github.com/Battle-Line-Productions/Courier/discussions)

Check the [project roadmap](https://github.com/orgs/Battle-Line-Productions/projects/5) to see what's planned and where help is needed.

## Roadmap

See the full [Enterprise Feature Roadmap](https://github.com/orgs/Battle-Line-Productions/projects/5) for planned features, organized by priority:

| Tier | Focus Areas |
|------|-------------|
| **Tier 1** | Cloud storage connectors (S3, Azure Blob, GCS, SMB), HTTP transfers, Slack/Teams notifications, transfer integrity, reporting |
| **Tier 2** | Config export/import, API keys, bandwidth controls, secure file exchange, high availability |
| **Tier 3** | AS2/AS4 protocols, visual workflow designer, data transformation, DLP, analytics, multi-tenancy |

## License

Courier is licensed under the [Apache License 2.0](LICENSE).
