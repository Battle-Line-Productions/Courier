# Active Context

## Current Focus

**Primary**: Project initialization — design document has been read and understood. No code written yet.

## Session Mandates

- Follow the design doc at `Docs/CourierDesignDoc.md` as the single source of truth
- Vertical slice architecture with feature folders
- EF Core for queries only, DbUp for migrations
- All API responses use `ApiResponse<T>` envelope with numeric error codes

## Active Tasks

- [x] Read and understand the Courier design document
- [ ] Set up solution structure per Section 2.3
- [ ] Create domain entities per Section 4
- [ ] Create database schema per Section 13

## System Status

- **Health**: OK
- **Architecture**: .NET 10 API + Worker, Next.js frontend, PostgreSQL 16+, Azure Key Vault

## Recent Context

- **Session 2026-02-21**: First session. Read full design doc (~462KB). Captured architecture, domain model, tech stack, API design, security model. No code yet.

## Next Steps

1. Decide on implementation order (which vertical slice first)
2. Set up .NET solution structure with project references
3. Begin domain model implementation
