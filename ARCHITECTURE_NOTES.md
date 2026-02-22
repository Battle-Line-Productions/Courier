# Architecture Notes & Assumptions

This document tracks assumptions made during the walking skeleton implementation and their rationale. All decisions are anchored to the [Design Doc](Docs/CourierDesignDoc.md).

## Anchored Design Doc References

| Topic | Doc Section | Lines (approx) |
|-------|-------------|-----------------|
| Aspire AppHost | 14.7 | 7754–7804 |
| DbUp Migration Strategy | 13.1 | 6166–6215 |
| MigrationRunner impl | 14.6 | 7679–7752 |
| Health Checks | 14.8 | 7806–7843 |
| Jobs table DDL | 13.3.1 | 6236–6250 |
| ApiResponse envelope | 10.1 | 3583–3618 |
| ApiExceptionMiddleware | 10.1 | 3938–3963 |
| Error Code Catalog | 10.1 | 3623–3663 |

## Assumptions

### A1: .NET 10 SDK availability
The design doc specifies .NET 10. We pin to `10.0.0` in `global.json`. The SDK must be installed locally.

### A2: MigrationRunner lives in Courier.Migrations, not Infrastructure
The design doc (14.6) shows the path as `Courier.Infrastructure/Migrations/MigrationRunner.cs`. However, the user's instructions say DbUp scripts are embedded in `Courier.Migrations` and the MigrationRunner should use `typeof(MigrationMarker).Assembly`. We place MigrationRunner in `Courier.Migrations` alongside the SQL scripts, with a `MigrationMarker` class for assembly scanning. This keeps all migration concerns in one project. The API registers it as a hosted service.

### A3: Seq container port mapping
The design doc uses port 5341 for the Seq UI. Seq's HTTP ingestion port is 5341, and the UI runs on port 80 inside the container. We map host 5341 → container 80, matching the doc's `WithEndpoint(port: 5341, targetPort: 80)`.

### A4: Health checks — V1 simplification
The design doc includes Azure Key Vault and Quartz health checks. For V1 walking skeleton, we include only PostgreSQL and self checks. Key Vault and Quartz checks are not wired yet (no Key Vault or Quartz in the skeleton).

### A5: Domain is BCL-only
The Domain project has zero NuGet dependencies. All domain types (ApiResponse, entities, value objects) use only `System.*` namespaces.

### A6: EF Core snake_case via Npgsql conventions
We use `NpgsqlSnakeCaseNamingConvention` to map PascalCase C# entities to snake_case PostgreSQL columns, matching the DDL in Section 13.3.

### A7: Frontend API base URL
Aspire passes the API URL to the frontend via `services__courier-api__https__0` or similar. We use `NEXT_PUBLIC_API_URL` env var in the Next.js app, set by Aspire's `WithEnvironment`.

### A8: No authentication in V1 skeleton
The design doc specifies Entra ID auth. The walking skeleton skips auth to prove the vertical slice pattern. Auth will be added in a subsequent phase.

### A9: DbUp schema_versions table
DbUp automatically creates and manages the `schemaversions` table (its default name). We do not create a separate `schema_versions` table — DbUp handles this internally.

### A10: Worker SchemaVersionValidator expected version
The Worker compiles in an expected minimum migration name. For V1, this is the `0001_initial_schema.sql` script name. The validator reads DbUp's `schemaversions` table and checks if this script has been applied.

### A11: Aspire Seq integration
Using `Aspire.Hosting.Seq` package's `AddSeq()` instead of raw `AddContainer()` if available, otherwise falling back to the doc's `AddContainer` approach.
