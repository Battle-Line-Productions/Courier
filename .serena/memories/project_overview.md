# Courier - Project Overview

## Purpose
Enterprise file transfer & job management platform. Replaces manual SFTP, PGP, cron, and ad-hoc scripts with a unified, auditable, secure system.

## Tech Stack
- **Runtime**: .NET 10 (SDK 10.0.100, `global.json` with `latestFeature` rollForward)
- **Frontend**: Next.js 15 + React 19 + TypeScript + Tailwind + shadcn/ui
- **Database**: PostgreSQL 16+ (snake_case naming)
- **ORM**: EF Core 10 (queries only); DbUp 6.1.5 (migrations)
- **Transfer Protocols**: SSH.NET (SFTP), FluentFTP (FTP/FTPS)
- **Crypto**: BouncyCastle 2.5.1 (PGP), AES-256-GCM envelope encryption
- **Scheduling**: Quartz.NET 3.14
- **Testing**: xUnit 2.9, Shouldly 4.3, NSubstitute 5.3, Testcontainers 4.4, NetArchTest 1.3
- **Observability**: Serilog + Seq, OpenTelemetry
- **Orchestration**: .NET Aspire 13.x (AppHost)

## Architecture
**Vertical slice architecture** with strict dependency layers:
```
Api / Worker  →  Features  →  Infrastructure  →  Domain (BCL-only, zero NuGet deps)
                                                  Migrations (independent, DbUp)
```

## Solution Structure (Courier.slnx)

### Source Projects (src/)
| Project | Purpose |
|---------|---------|
| `Courier.Domain` | Entities, enums, value objects, interfaces. Zero NuGet deps. |
| `Courier.Infrastructure` | EF Core DbContext (queries), AES-GCM encryption. |
| `Courier.Features` | Vertical slices: controllers, services, DTOs, validators, engine. |
| `Courier.Api` | ASP.NET Core host. Controllers pulled in via `AddApplicationPart`. |
| `Courier.Worker` | .NET Worker Service. Polls DB for queued jobs, runs Quartz.NET. |
| `Courier.Migrations` | DbUp embedded SQL scripts. Runs on API startup. |
| `Courier.AppHost` | Aspire orchestrator for local dev. |
| `Courier.ServiceDefaults` | Shared OpenTelemetry, health checks, resilience config. |

### Test Projects (tests/)
| Project | Purpose |
|---------|---------|
| `Courier.Tests.Unit` | Fast, no Docker. InMemory EF, NSubstitute, Shouldly. |
| `Courier.Tests.Integration` | WebApplicationFactory + Testcontainers PostgreSQL. |
| `Courier.Tests.Architecture` | NetArchTest dependency rule enforcement. |

## Feature Domains in Features Project
- `Jobs/` - Job CRUD, steps, execution tracking
- `Connections/` - Protocol connection management
- `PgpKeys/` - PGP key management
- `SshKeys/` - SSH key management
- `Filesystem/` - File browsing
- `Engine/` - Job execution engine
  - `Steps/` - Step handlers (file ops, transfer, crypto)
  - `Protocols/` - Transfer clients (SFTP, FTP, FTPS)
  - `Crypto/` - PGP encrypt/decrypt/sign/verify
