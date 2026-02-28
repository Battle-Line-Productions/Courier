# Courier - Code Style & Conventions

## General
- `.editorconfig` enforced: 4-space indent for C#/SQL, 2-space for JSON/YAML/JS/TS
- Line endings: LF
- Charset: UTF-8
- `TreatWarningsAsErrors`: true
- `Nullable`: enable
- `ImplicitUsings`: enable
- Central Package Management (CPM): versions in `Directory.Packages.props`

## C# Code Style

### Entities (Domain)
- Plain classes with auto-properties
- Guid PKs, soft delete (`IsDeleted` + `DeletedAt`)
- `CreatedAt`/`UpdatedAt` set manually (no interceptors)
- Navigation properties initialized with `= []`
- No constructors required; default values set inline
- File-scoped namespaces

### Services (Features)
- Concrete classes, no interfaces (except engine abstractions)
- Scoped lifetime, injected via DI
- Inject `CourierDbContext` directly
- Always return `ApiResponse<T>` — never throw for business errors
- `MapToDto` is a `private static` method on each service
- Constructor injection with `readonly` backing field

### API Pattern
- All endpoints return `ApiResponse<T>` or `PagedApiResponse<T>` envelope
- Routes: `api/v1/{resource}`
- Controllers use `[ApiController]` + `[Route("api/v1/...")]`
- Validation: FluentValidation, validated inline in controller actions
- Error codes: numeric, organized in ranges (1000 general, 2000 jobs, etc.)

### Engine Pattern
- `IJobStep` interface with `TypeKey` + `ExecuteAsync(StepConfiguration, JobContext, CancellationToken)`
- Step type keys: `"file.copy"`, `"sftp.upload"`, `"pgp.encrypt"`, etc.
- `StepConfiguration` wraps `JsonElement` with typed accessors
- `JobContext` is a key-value bag; outputs keyed as `"{stepOrder}.{outputKey}"`
- `context:` prefix references prior step outputs

### Serialization
- System.Text.Json everywhere (ASP.NET default camelCase output)
- Enums serialized as lowercase strings via explicit `MapToDto` logic
- Newtonsoft only for Quartz.NET persistent store

### Database
- PostgreSQL 16+ with snake_case naming (tables, columns)
- EF Core for queries only; DbUp for all schema changes
- Migration scripts: `src/Courier.Migrations/Scripts/NNNN_description.sql`
- SQL: `UUID PK DEFAULT gen_random_uuid()`, `TIMESTAMPTZ`, `BOOLEAN` soft delete, `BYTEA`, `JSONB`

### Encryption
- AES-256-GCM envelope encryption
- Blob format: `[1B version][12B nonce][16B tag][32B wrapped-dek][12B nonce][16B tag][N ciphertext]`

## Test Style

### Unit Tests
- InMemory EF database (unique per test via `Guid.NewGuid()`)
- NSubstitute for mocking external dependencies
- Shouldly assertions (`result.ShouldBe(...)`, `result.ShouldNotBeNull()`)
- Global using: `global using Xunit;` in `GlobalUsings.cs`
- Test naming: `MethodName_Scenario_ExpectedBehavior` (e.g., `Create_ValidRequest_ReturnsSuccessWithJob`)
- AAA pattern with `// Arrange`, `// Act`, `// Assert` comments
- Static helper `CreateInMemoryContext()` per test class
- `[Fact]` for single cases, `[Theory]` for parameterized
- No test base classes; each test class is self-contained

### Integration Tests
- `WebApplicationFactory<Courier.Api.Program>` + Testcontainers PostgreSQL
- Factory removes all `IHostedService` registrations and replaces EF connection

### Architecture Tests
- NetArchTest enforces dependency rules (Domain has zero external deps, etc.)

## DI Registration
- Single `AddCourierFeatures(IServiceCollection, IConfiguration)` extension in `FeaturesServiceExtensions.cs`
- Registers all services, engine components, validators for both Api and Worker
