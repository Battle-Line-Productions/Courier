# Courier - Task Completion Checklist

When a coding task is completed, perform these verification steps:

## 1. Build
```bash
dotnet build Courier.slnx
```
Must compile with zero warnings (TreatWarningsAsErrors is enabled).

## 2. Unit Tests
```bash
dotnet test tests/Courier.Tests.Unit
```
All existing tests must pass. New code should have corresponding tests.

## 3. Architecture Tests
```bash
dotnet test tests/Courier.Tests.Architecture
```
Enforces dependency rules. Domain must have zero external NuGet deps. Infrastructure must not reference Features.

## 4. Integration Tests (if relevant)
```bash
dotnet test tests/Courier.Tests.Integration
```
Requires Docker. Run if changes affect API endpoints or database interactions.

## 5. Code Review Checks
- Services return `ApiResponse<T>`, never throw for business errors
- New entities have soft delete fields (`IsDeleted`, `DeletedAt`)
- New services registered in `FeaturesServiceExtensions.cs`
- New step handlers have `TypeKey` registered in `StepTypeRegistry`
- Database schema changes use DbUp migration scripts (not EF migrations)
- Error codes follow the numeric range convention
