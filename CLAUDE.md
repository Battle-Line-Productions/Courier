# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Build & Run Commands

```bash
# Build
dotnet build Courier.slnx

# Run everything (starts Postgres, Seq, API, Worker, Frontend via Aspire)
cd src/Courier.AppHost && dotnet run

# Frontend first-time setup
cd src/Courier.Frontend && npm install

# Run all tests
dotnet test Courier.slnx

# Run specific test suites
dotnet test tests/Courier.Tests.Unit              # Fast, no Docker
dotnet test tests/Courier.Tests.Architecture      # Dependency rule enforcement
dotnet test tests/Courier.Tests.Integration       # Requires Docker (Testcontainers)

# Run a single test by name
dotnet test tests/Courier.Tests.Unit --filter "FullyQualifiedName~JobServiceTests.CreateAsync"

# Run tests in a specific class
dotnet test tests/Courier.Tests.Unit --filter "FullyQualifiedName~JobServiceTests"
```

Prerequisites: .NET 10 SDK, Docker Desktop, Node.js 20+.

## Architecture

**Vertical slice architecture** with strict dependency layers:

```
Api / Worker  →  Features  →  Infrastructure  →  Domain (BCL-only, zero NuGet deps)
                                                  Migrations (independent, DbUp)
```

- **Domain** (`Courier.Domain`): Entities, enums, value objects, interfaces. No external dependencies — enforced by architecture tests.
- **Infrastructure** (`Courier.Infrastructure`): EF Core DbContext (queries only), AES-GCM credential encryption. Does not reference Features.
- **Features** (`Courier.Features`): Vertical slices — each feature folder owns controller, service, DTOs, validators. Contains the job engine and step handlers.
- **Api** (`Courier.Api`): ASP.NET Core host. Controllers live in Features, not here — `AddApplicationPart(typeof(FeaturesServiceExtensions).Assembly)` pulls them in.
- **Worker** (`Courier.Worker`): .NET Worker Service. Polls `job_executions` table for queued jobs (DB-as-queue, 5s interval). Runs Quartz.NET scheduler.
- **Migrations** (`Courier.Migrations`): DbUp embedded SQL scripts. Runs on API startup only; Worker validates schema version.
- **AppHost** (`Courier.AppHost`): Aspire orchestrator for local dev.

## Key Conventions

### API Pattern

- All endpoints return `ApiResponse<T>` or `PagedApiResponse<T>` envelope (defined in Domain)
- Error handling: services return `ApiResponse` with `Error` field; controllers switch on `Error.Code` for HTTP status
- Numeric error codes in ranges: 1000 general, 2000 jobs, 3000 connections, 4000 keys, 5000 transfer, 6000 crypto, 8000 filesystem
- Routes: `api/v1/{resource}` — controllers use `[ApiController]` + `[Route("api/v1/...")]`
- Validation: FluentValidation, validated inline in controller actions (not filters)

### Entity Pattern

- Guid PKs, set in service layer with `Guid.CreateVersion7()`
- Soft delete: `IsDeleted` + `DeletedAt`, global EF query filter, bypassed with `IgnoreQueryFilters()`
- `CreatedAt`/`UpdatedAt` set manually in service code (no interceptors)
- Status fields stored as lowercase strings in DB, not enums on the entity

### Service Pattern

- Concrete classes (no interfaces), Scoped lifetime, inject `CourierDbContext` directly
- Always return `ApiResponse<T>` — never throw for business errors
- `MapToDto` is a private static method on each service
- Single `AddCourierFeatures(IServiceCollection, IConfiguration)` extension registers everything for both Api and Worker

### Database

- PostgreSQL 16+ with snake_case naming (tables, columns)
- EF Core for queries only; DbUp for all schema changes
- Migration scripts: `src/Courier.Migrations/Scripts/NNNN_description.sql`
- SQL conventions: `UUID PK DEFAULT gen_random_uuid()`, `TIMESTAMPTZ`, `BOOLEAN` soft delete, `BYTEA` for encrypted fields, `JSONB` for structured data
- `pg_advisory_lock(12345)` prevents concurrent migrations

### Serialization

- System.Text.Json everywhere (ASP.NET default camelCase output)
- Enums serialized as lowercase strings via explicit `MapToDto` logic (not JsonConverter)
- Newtonsoft used only for Quartz.NET persistent store serialization

### Job Engine

- `IJobStep` interface: `TypeKey` property + `ExecuteAsync(StepConfiguration, JobContext, CancellationToken)`
- Step type keys: `"file.copy"`, `"sftp.upload"`, `"pgp.encrypt"`, etc.
- `StepConfiguration` wraps `JsonElement` with typed accessors (`GetString`, `GetBool`, `GetStringArray`)
- `JobContext` is a key-value bag passed through all steps; outputs keyed as `"{stepOrder}.{outputKey}"`
- `context:` prefix in config values references prior step outputs (e.g., `"context:1.uploaded_file"`)
- `JobConnectionRegistry` pools transfer client connections within a single job execution

### Encryption

- AES-256-GCM envelope encryption: fresh DEK per encrypt, wrapped by KEK
- Blob format: `[1B version][12B nonce][16B tag][32B wrapped-dek][12B nonce][16B tag][N ciphertext]`
- KEK from config section `"Encryption"` — base64-encoded 32-byte key

### Testing

- **Unit tests**: InMemory EF database (unique per test), NSubstitute for mocking, Shouldly assertions
- **Integration tests**: `WebApplicationFactory<Courier.Api.Program>` + Testcontainers PostgreSQL. Factory removes all `IHostedService` registrations and replaces EF connection.
- **Architecture tests**: NetArchTest enforces dependency rules (Domain has zero external deps, etc.)

### Frontend

- Next.js 15 + React 19 + TypeScript + Tailwind + shadcn/ui
- TanStack Query hooks in `src/lib/hooks/` per domain (e.g., `use-jobs.ts`, `use-connections.ts`)
- API client class in `src/lib/api.ts` — throws `ApiClientError` with structured error info
- TypeScript types in `src/lib/types.ts` mirror backend DTOs exactly
