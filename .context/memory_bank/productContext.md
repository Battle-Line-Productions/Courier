# Product Context

## Soul Purpose

Courier is an enterprise file transfer and job management platform that replaces manual SFTP operations, ad-hoc PGP scripts, and disconnected scheduling tools with a unified, auditable, secure platform.

## Core Philosophy

1. **Security first** — AES-256-GCM envelope encryption, Azure Key Vault, FIPS-capable, no secrets on disk
2. **Auditability** — Append-only audit log on every operation, full traceability of file movements
3. **Reliability** — Checkpoint/resume, configurable retry policies, idempotent step execution

## Key Goals

- Multi-step job pipelines: chain file transfers, PGP encryption, compression into repeatable workflows
- Centralized connection and key management with lifecycle tracking
- File monitors that detect new files and trigger jobs automatically
- Web UI for managing jobs, keys, connections, and viewing audit history
- Role-based access control via Azure Entra ID (Admin, Operator, Viewer)

## Product Positioning

- **External Metaphor**: "Managed file transfer platform — like GoAnywhere or Axway, but built in-house"
- **Internal Reality**: A .NET 10 + Next.js platform with vertical slice architecture, PostgreSQL backend, and Azure-native security
