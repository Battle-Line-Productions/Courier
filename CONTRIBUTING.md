# Contributing to Courier

Thank you for your interest in contributing to Courier! This guide covers everything you need to get started.

## Code of Conduct

By participating in this project, you agree to treat everyone with respect. Be constructive, be patient, and assume good intentions.

## How to Contribute

### Reporting Bugs

1. **Search existing issues** first to avoid duplicates.
2. **Use the Bug Report template** when [creating a new issue](https://github.com/Battle-Line-Productions/Courier/issues/new?template=bug_report.yml).
3. Include steps to reproduce, expected vs actual behavior, and environment details.
4. Attach logs or screenshots if they help illustrate the problem.

### Suggesting Features

1. Check the [project roadmap](https://github.com/orgs/Battle-Line-Productions/projects/5) for planned features.
2. **Use the Feature Request template** when [opening an issue](https://github.com/Battle-Line-Productions/Courier/issues/new?template=feature_request.yml).
3. Describe the use case and problem you're trying to solve, not just the solution.

### Submitting Code

1. **Fork the repository** and create a branch from `main`.
2. **Pick an issue** — look for issues labeled [`good first issue`](https://github.com/Battle-Line-Productions/Courier/labels/good%20first%20issue) if you're new.
3. Follow the development setup and conventions below.
4. **Open a pull request** targeting `main`. The PR template will guide you.
5. Ensure CI checks pass before requesting review.

## Development Setup

### Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Docker Desktop](https://www.docker.com/products/docker-desktop/)
- [Node.js 20+](https://nodejs.org/)

### Getting Started

```bash
# Clone your fork
git clone https://github.com/<your-username>/Courier.git
cd Courier

# Install frontend dependencies
cd src/Courier.Frontend
npm install
cd ../..

# Start everything with Aspire (Postgres, Seq, API, Worker, Frontend)
cd src/Courier.AppHost
dotnet run
```

### Running Tests

```bash
# Full test suite
dotnet test Courier.slnx

# Individual suites
dotnet test tests/Courier.Tests.Unit            # Fast, no Docker
dotnet test tests/Courier.Tests.Architecture    # Dependency rules
dotnet test tests/Courier.Tests.Integration     # Requires Docker (Testcontainers)
dotnet test tests/Courier.Tests.JobEngine       # Engine pipeline tests
```

### E2E Tests (Playwright)

Requires the Aspire stack to be running:

```bash
cd src/Courier.Frontend

# First time: install browsers
npx playwright install chromium

# Run tests (auto-discovers Aspire ports)
e2e-run.bat               # All tests
e2e-headed.bat            # Visible browser
e2e-ui.bat                # Playwright UI mode
```

## Project Conventions

### Architecture

Courier uses vertical slice architecture with strict dependency layers:

```
Api / Worker  →  Features  →  Infrastructure  →  Domain (BCL-only)
```

- **Domain** has zero external NuGet dependencies (enforced by architecture tests).
- **Features** are organized by domain area (Jobs, Connections, Keys, etc.), each owning its controller, service, DTOs, and validators.
- **Infrastructure** contains EF Core DbContext and credential encryption. It does NOT reference Features.

### Backend

- **API responses**: Always return `ApiResponse<T>` or `PagedApiResponse<T>` — never throw for business errors.
- **Services**: Concrete classes (no interfaces), scoped lifetime, inject `CourierDbContext` directly.
- **Entity IDs**: `Guid.CreateVersion7()`, set in the service layer.
- **Soft delete**: `IsDeleted` + `DeletedAt` with EF global query filters.
- **Timestamps**: `CreatedAt`/`UpdatedAt` set manually in service code.
- **Database changes**: Always use DbUp SQL migrations in `src/Courier.Migrations/Scripts/`. Never use EF migrations.
- **SQL conventions**: snake_case tables and columns, `UUID` PKs, `TIMESTAMPTZ`, `BOOLEAN` soft delete.
- **Serialization**: System.Text.Json everywhere (Newtonsoft only for Quartz.NET store).
- **Validation**: FluentValidation, validated inline in controller actions.
- **Audit logging**: All entity CRUD operations should be audit-logged via `AuditService`.

### Frontend

- **Stack**: Next.js 15, React 19, TypeScript, Tailwind CSS, shadcn/ui.
- **API hooks**: TanStack Query hooks in `src/lib/hooks/` per domain.
- **Types**: `src/lib/types.ts` mirrors backend DTOs exactly.
- **API client**: `src/lib/api.ts` — throws `ApiClientError` with structured error info.

### Testing

- **Unit tests**: InMemory EF database (unique per test), Shouldly assertions.
- **Integration tests**: `WebApplicationFactory` + Testcontainers PostgreSQL.
- **Architecture tests**: NetArchTest enforces dependency rules.
- **E2E tests**: Playwright with 4 max parallel workers.

### Commit Messages

Write clear, concise commit messages:
- Use imperative mood: "Add S3 connector" not "Added S3 connector"
- Reference issues when applicable: "Fix connection timeout handling (#42)"
- Keep the subject line under 72 characters

### Branch Naming

```
feature/short-description    # New features
fix/short-description        # Bug fixes
docs/short-description       # Documentation
refactor/short-description   # Refactoring
```

## Pull Request Process

1. **One concern per PR** — don't mix unrelated changes.
2. **Update tests** for any behavior changes. New features should include unit tests at minimum.
3. **Update types** — if you change a backend DTO, update `src/lib/types.ts` to match.
4. **Add migrations** — if you change the database schema, add a DbUp script in `src/Courier.Migrations/Scripts/` following the `NNNN_description.sql` naming pattern.
5. **CI must pass** — the `pr-check` workflow runs build, tests, and lint.
6. **Review feedback** — address all review comments or explain why you disagree.

### What We Look For in Review

- Does it follow existing patterns in the codebase?
- Are there tests?
- Is the migration safe (no data loss, backward compatible)?
- Are credentials encrypted at rest if stored?
- Is audit logging in place for new entity operations?
- Does the frontend match backend DTO shapes?

## Database Migrations

All schema changes go through DbUp. Never modify the EF model to drive schema changes.

```bash
# Add a new migration
# Create: src/Courier.Migrations/Scripts/NNNN_description.sql
# Convention: sequential number, lowercase_snake_case description
```

SQL conventions:
- `UUID` primary keys with `DEFAULT gen_random_uuid()`
- `TIMESTAMPTZ` for all timestamps
- `BOOLEAN` for soft delete (`is_deleted`)
- `BYTEA` for encrypted fields
- `JSONB` for structured data
- `snake_case` for all table and column names

## Getting Help

- **Questions**: Open a [Discussion](https://github.com/Battle-Line-Productions/Courier/discussions)
- **Bugs**: [File an issue](https://github.com/Battle-Line-Productions/Courier/issues/new?template=bug_report.yml)
- **Features**: [Request a feature](https://github.com/Battle-Line-Productions/Courier/issues/new?template=feature_request.yml)

## License

By contributing to Courier, you agree that your contributions will be licensed under the [Apache License 2.0](LICENSE).
