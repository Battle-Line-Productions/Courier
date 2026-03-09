# CLAUDE.md

This file provides guidance to Claude Code when working with the Courier repository.

## IMPOORTANT

- DO NOT USE GIT WORKTREES
- DO NOT COMMIT CODE, THIS IS MY JOB!
- ALWAYS USE AVAILABLE TOOLS

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
dotnet test tests/Courier.Tests.JobEngine                                          # Engine pipeline tests, no Docker
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
- `StepConfiguration` wraps `JsonElement` with typed accessors (`GetString`, `GetBool`, `GetStringArray`). Supports snake_case/camelCase fallback — `GetString("source_path")` finds `sourcePath` if exact key missing.
- `JobContext` is a key-value bag passed through all steps; outputs keyed as `"{alias}.{outputKey}"` where alias is a slugified step name (e.g., `download-report.downloaded_file`).
- `context:` prefix in config values references prior step outputs (e.g., `"context:download-report.downloaded_file"`). Steps can have custom aliases set via the UI.
- `JobConnectionRegistry` pools transfer client connections within a single job execution.

### Encryption

- AES-256-GCM envelope encryption: fresh DEK per encrypt, wrapped by KEK.
- Blob format: `[1B version][12B nonce][16B tag][32B wrapped-dek][12B nonce][16B tag][N ciphertext]`.
- KEK from config section `"Encryption"` — base64-encoded 32-byte key.

### Testing

- **Unit**: InMemory EF database (unique per test), NSubstitute for mocking, Shouldly assertions.
- **JobEngine**: End-to-end engine pipeline tests (context resolution, crypto, file ops, flow control, transfer steps, failure policies). InMemory EF, no Docker.
- **Integration**: `WebApplicationFactory<Courier.Api.Program>` + Testcontainers PostgreSQL. Factory removes all `IHostedService` registrations and replaces EF connection.
- **Architecture**: NetArchTest enforces dependency rules (Domain has zero external deps, etc.).
- **E2E (Playwright)**: Browser-based tests against a running Aspire stack. See [E2E Tests](#e2e-tests-playwright) section below.

### Frontend

- Next.js 15 + React 19 + TypeScript + Tailwind + shadcn/ui.
- TanStack Query hooks in `src/lib/hooks/` per domain (e.g., `use-jobs.ts`, `use-connections.ts`).
- API client class in `src/lib/api.ts` — throws `ApiClientError` with structured error info.
- TypeScript types in `src/lib/types.ts` mirror backend DTOs exactly.

## E2E Tests (Playwright)

**Prerequisite:** The Aspire stack must be running (`cd src/Courier.AppHost && dotnet run`). Aspire assigns dynamic ports — the test scripts auto-discover them.

```bash
cd src/Courier.Frontend

# First-time setup: install Playwright browsers
npx playwright install chromium

# Run all E2E tests (auto-discovers Aspire ports)
e2e-run.bat

# Run in headed mode (visible browser)
e2e-headed.bat

# Run with Playwright UI mode (interactive)
e2e-ui.bat

# Run against a fresh database (setup wizard tests)
e2e-fresh-db.bat

# Run specific test file
npx playwright test e2e/jobs.spec.ts

# Run with manual port override (if auto-discovery fails)
set API_URL=http://localhost:60606
set FRONTEND_URL=http://localhost:51420
npx playwright test
```

**Port discovery:** Scripts use `e2e/discover-ports.ps1` to find the Aspire-assigned API and frontend ports automatically. If the Aspire stack is restarted, ports change and scripts re-discover them.

**Environment variables:**
- `API_URL` — Backend API base URL (default: `http://localhost:5000`)
- `FRONTEND_URL` — Frontend base URL (default: `http://localhost:3000`)

**Test structure:**
- `e2e/fixtures.ts` — Custom fixtures: `authenticatedPage` (logged-in page), `apiHelper` (API request context)
- `e2e/helpers/api-helpers.ts` — Programmatic test data setup/teardown via API
- `e2e/global-setup.ts` — Creates test admin user on first run
- `e2e/global-teardown.ts` — Cleans up orphaned `e2e-` prefixed entities after each run
- Spec files: `auth.spec.ts`, `dashboard.spec.ts`, `jobs.spec.ts`, `connections.spec.ts`, `keys.spec.ts`, `chains.spec.ts`, `monitors.spec.ts`, `notifications.spec.ts`, `tags.spec.ts`, `users.spec.ts`, `audit-log.spec.ts`, `settings.spec.ts`, `navigation.spec.ts`

**Conventions:**
- All test entities prefixed with `e2e-` + unique suffix for isolation
- API-first setup: create data via `apiHelper`, test only UI workflows
- Cleanup in `finally` blocks via API helpers
- Toast assertions: `page.locator('[data-sonner-toast]').filter({ hasText: "..." })`
- Confirm dialogs: `page.getByRole('dialog')` then click confirm button inside
- Step config keys in E2E tests must use **snake_case** (e.g., `source_path`, `destination_path`) — the backend expects snake_case, the frontend converts camelCase form state to snake_case before sending
- `apiHelper.addJobSteps()` supports an optional `alias` field per step for custom step aliases

**Reading test results correctly (IMPORTANT):**
Playwright's default `line` and `dot` reporters use ANSI escape codes that overwrite previous lines. When captured via `tail` or `tee`, the summary counts appear garbled or incomplete. To get accurate results:
```bash
# Option 1: Pipe through cat -v to neutralize ANSI escapes, then grep for summary
npx playwright test --workers=4 2>&1 | cat -v | grep -E "passed|failed|flaky"

# Option 2: Use JSON output file (most reliable for automation)
PLAYWRIGHT_JSON_OUTPUT_NAME=results.json npx playwright test --reporter=json
node -e "const r=require('./results.json'); console.log(r.stats)"

# Option 3: Run with --reporter=list for non-ANSI output
npx playwright test --reporter=list 2>&1 | tail -5
```

**Worker count:** The config limits to 4 parallel workers (`workers: 4`). More workers overwhelm the API with concurrent login requests, causing cascading auth failures. Do NOT increase without load-testing the API.

**Troubleshooting cascading auth failures:**
If most tests fail with `body.data is null` or `Cannot destructure 'accessToken'`:
1. The test admin account may be locked. Unlock via DB:
   ```bash
   docker exec -e PGPASSWORD='<pw>' <container> psql -U postgres -d CourierDb \
     -c "UPDATE users SET failed_login_count=0, locked_until=NULL WHERE username='testadmin';"
   ```
2. Orphaned test data may slow queries. The global teardown handles this, but for manual cleanup:
   ```bash
   docker exec -e PGPASSWORD='<pw>' <container> psql -U postgres -d CourierDb \
     -c "DELETE FROM jobs WHERE name LIKE 'e2e-%'; DELETE FROM tags WHERE name LIKE 'e2e-%';" # etc.
   ```
3. Never modify the `testadmin` password in tests. Use a temporary user instead (see `settings.spec.ts`).

### Playwright Best Practices (for writing and fixing E2E tests)

**Timeouts after navigation:**
- ALWAYS add `{ timeout: 10_000 }` to the first `toBeVisible()` or `toHaveURL()` assertion after `page.goto()`. The default 5s timeout is too short — React hydration + AuthProvider session restore (API call for token refresh) can take 6-8s under parallel worker load.
- Subsequent assertions on the same page can use the default timeout since the page is already loaded.

**Radix UI + Next.js Link in dropdowns:**
- NEVER use `DropdownMenuItem asChild` with a Next.js `<Link>`. Radix closes the dropdown (unmounting the Link) before client-side navigation fires. Use `onSelect={() => router.push("/path")}` instead.
- Same applies to `ContextMenuItem`, `MenubarItem`, etc.
- `SelectItem value=""` crashes Radix Select — use `value="__none__"` for empty-state placeholders.

**CSS transitions:**
- After triggering a CSS transition (e.g., sidebar collapse/expand), add `await page.waitForTimeout(500)` before measuring dimensions with `boundingBox()`. The React state update is synchronous, but the CSS `transition-all` animation takes time.

**Parallel worker safety:**
- Tests that create data must use unique names with `e2e-` prefix + timestamp/random suffix.
- Jobs must have at least one step before triggering — `triggerJob()` silently fails on stepless jobs.
- Setup wizard tests (`setup.spec.ts`) require a fresh DB. They auto-skip when the system is already initialized.
- Keep workers at 4 max. More overwhelms the API with concurrent auth requests.

**Waiting patterns:**
- After `goto()`: wait for a heading or key element with `{ timeout: 10_000 }` before interacting.
- After clicking navigation links in a loop: add `waitForLoadState("networkidle")` between iterations.
- Before clicking dropdown/menu items: verify the item `toBeVisible()` first.
- For toast assertions after mutations: use `{ timeout: 10_000 }` — API round-trip + toast animation takes time.

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
