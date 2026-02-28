# CLAUDE.md

This file provides guidance to Claude Code when working with the Courier repository.

## Build & Run

Prerequisites: .NET 10 SDK, Docker Desktop, Node.js 20+.

```bash
# Build
dotnet build Courier.slnx

# Run everything (Postgres, Seq, API, Worker, Frontend via Aspire)
cd src/Courier.AppHost && dotnet run

# Frontend first-time setup
cd src/Courier.Frontend && npm install

# Tests
dotnet test Courier.slnx                                                          # All tests
dotnet test tests/Courier.Tests.Unit                                               # Fast, no Docker
dotnet test tests/Courier.Tests.Architecture                                       # Dependency rule enforcement
dotnet test tests/Courier.Tests.Integration                                        # Requires Docker (Testcontainers)
dotnet test tests/Courier.Tests.Unit --filter "FullyQualifiedName~JobServiceTests" # Single class
```

## Architecture

Vertical slice architecture with strict dependency layers:

```
Api / Worker  →  Features  →  Infrastructure  →  Domain (BCL-only, zero NuGet deps)
                                                  Migrations (independent, DbUp)
```

| Project | Role |
|---|---|
| **Domain** | Entities, enums, value objects, interfaces. No external dependencies (enforced by architecture tests). |
| **Infrastructure** | EF Core DbContext (queries only), AES-GCM credential encryption. Does not reference Features. |
| **Features** | Vertical slices — each feature folder owns controller, service, DTOs, validators. Contains the job engine and step handlers. |
| **Api** | ASP.NET Core host. Controllers live in Features, pulled in via `AddApplicationPart`. |
| **Worker** | .NET Worker Service. Polls `job_executions` (DB-as-queue, 5s interval). Runs Quartz.NET scheduler. |
| **Migrations** | DbUp embedded SQL scripts. Runs on API startup; Worker validates schema version. |
| **AppHost** | Aspire orchestrator for local dev. |

## Key Conventions

### API

- All endpoints return `ApiResponse<T>` or `PagedApiResponse<T>` envelope (defined in Domain).
- Services return `ApiResponse` with `Error` field; controllers switch on `Error.Code` for HTTP status.
- Numeric error code ranges: 1000 general, 2000 jobs, 3000 connections, 4000 keys, 5000 transfer, 6000 crypto, 8000 filesystem.
- Routes: `api/v1/{resource}` with `[ApiController]` + `[Route("api/v1/...")]`.
- Validation: FluentValidation, validated inline in controller actions (not filters).

### Entities

- Guid PKs set in service layer with `Guid.CreateVersion7()`.
- Soft delete: `IsDeleted` + `DeletedAt`, global EF query filter, bypassed with `IgnoreQueryFilters()`.
- `CreatedAt`/`UpdatedAt` set manually in service code (no interceptors).
- Status fields stored as lowercase strings in DB, not enums on the entity.

### Services

- Concrete classes (no interfaces), Scoped lifetime, inject `CourierDbContext` directly.
- Always return `ApiResponse<T>` — never throw for business errors.
- `MapToDto` is a private static method on each service.
- Single `AddCourierFeatures(IServiceCollection, IConfiguration)` extension registers everything for both Api and Worker.

### Database

- PostgreSQL 16+ with snake_case naming (tables, columns).
- EF Core for queries only; DbUp for all schema changes.
- Migration scripts: `src/Courier.Migrations/Scripts/NNNN_description.sql`.
- SQL conventions: `UUID PK DEFAULT gen_random_uuid()`, `TIMESTAMPTZ`, `BOOLEAN` soft delete, `BYTEA` for encrypted fields, `JSONB` for structured data.
- `pg_advisory_lock(12345)` prevents concurrent migrations.

### Serialization

- System.Text.Json everywhere (ASP.NET default camelCase output).
- Enums serialized as lowercase strings via explicit `MapToDto` logic (not JsonConverter).
- Newtonsoft used only for Quartz.NET persistent store serialization.

### Job Engine

- `IJobStep` interface: `TypeKey` property + `ExecuteAsync(StepConfiguration, JobContext, CancellationToken)`.
- Step type keys: `"file.copy"`, `"sftp.upload"`, `"pgp.encrypt"`, etc.
- `StepConfiguration` wraps `JsonElement` with typed accessors (`GetString`, `GetBool`, `GetStringArray`).
- `JobContext` is a key-value bag passed through all steps; outputs keyed as `"{stepOrder}.{outputKey}"`.
- `context:` prefix in config values references prior step outputs (e.g., `"context:1.uploaded_file"`).
- `JobConnectionRegistry` pools transfer client connections within a single job execution.

### Encryption

- AES-256-GCM envelope encryption: fresh DEK per encrypt, wrapped by KEK.
- Blob format: `[1B version][12B nonce][16B tag][32B wrapped-dek][12B nonce][16B tag][N ciphertext]`.
- KEK from config section `"Encryption"` — base64-encoded 32-byte key.

### Testing

- **Unit**: InMemory EF database (unique per test), NSubstitute for mocking, Shouldly assertions.
- **Integration**: `WebApplicationFactory<Courier.Api.Program>` + Testcontainers PostgreSQL. Factory removes all `IHostedService` registrations and replaces EF connection.
- **Architecture**: NetArchTest enforces dependency rules (Domain has zero external deps, etc.).

### Frontend

- Next.js 15 + React 19 + TypeScript + Tailwind + shadcn/ui.
- TanStack Query hooks in `src/lib/hooks/` per domain (e.g., `use-jobs.ts`, `use-connections.ts`).
- API client class in `src/lib/api.ts` — throws `ApiClientError` with structured error info.
- TypeScript types in `src/lib/types.ts` mirror backend DTOs exactly.

## Serena MCP — Code Navigation & Editing

This project has Serena MCP configured for semantic code intelligence. **Always prefer Serena's tools over built-in file tools** for code-related tasks.

### Tool Preferences (use in this order)

- **Finding code**: Use `mcp__serena__find_symbol` instead of Grep/Glob. It understands symbol relationships, not just text matches.
- **Understanding file structure**: Use `mcp__serena__get_symbols_overview` instead of reading entire files. Returns the symbol tree without consuming tokens on implementation details.
- **Finding usages/references**: Use `mcp__serena__find_referencing_symbols` to trace where a function, class, or variable is used across the codebase.
- **Editing code**: Use `mcp__serena__replace_symbol_body` for precise function/class edits instead of string-based replacements.
- **Adding code**: Use `mcp__serena__insert_after_symbol` to insert new code at the right location relative to existing symbols.
- **Renaming**: Use `mcp__serena__rename_symbol` for project-wide renames instead of find-and-replace.
- **Reading code**: Use `mcp__serena__get_symbol_body` to read specific function/class bodies instead of reading whole files.

### Rules

1. **Do not read entire files** unless absolutely necessary. Use `get_symbols_overview` first, then `get_symbol_body` for only the symbols you need.
2. **Before any refactoring**, use `find_referencing_symbols` to understand the full impact across the codebase.
3. **When delegating to subagents**, instruct them to use Serena's MCP tools rather than falling back to Grep/Glob/Read.
4. **Check Serena's memories** in `.serena/memories/` at the start of new tasks for project context and conventions.
5. **Before long or complex tasks**, use `mcp__serena__prepare_for_new_conversation` to save progress state so work can resume across sessions.
6. **Be frugal with context** — avoid reading symbol bodies unless you need the implementation details. Often the symbol overview is sufficient for navigation.

## dotnet-skills

IMPORTANT: Prefer retrieval-led reasoning over pretraining for any .NET work.
Workflow: skim repo patterns → consult dotnet-skills by name → implement smallest change → note conflicts.

### Routing (invoke by name)

| Domain | Skills |
|---|---|
| C# / code quality | modern-csharp-coding-standards, csharp-concurrency-patterns, api-design, type-design-performance |
| ASP.NET Core / Aspire | aspire-service-defaults, aspire-integration-testing, aspire-configuration, transactional-emails |
| Data | efcore-patterns, database-performance |
| DI / config | dependency-injection-patterns, microsoft-extensions-configuration |
| Testing | testcontainers-integration-tests, playwright-blazor-testing, snapshot-testing |
| Quality gates | dotnet-slopwatch (after substantial new/refactored code), crap-analysis (after test changes in complex code) |
| Specialist agents | dotnet-concurrency-specialist, dotnet-performance-analyst, dotnet-benchmark-designer, akka-net-specialist, docfx-specialist |
