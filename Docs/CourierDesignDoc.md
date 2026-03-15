# Courier — Design Document

**Project:** Courier — Enterprise File Transfer & Job Management Platform
**Author:** Michael
**Date:** February 2026
**Status:** Draft — V1 Scope

---

## Table of Contents

- **[1. Executive Summary](#1-executive-summary)**
  - [1.1 Problem](#11-problem)
  - [1.2 Solution](#12-solution)
  - [1.3 Key Capabilities (V1)](#13-key-capabilities-v1)
  - [1.4 Architecture at a Glance](#14-architecture-at-a-glance)
  - [1.5 Tech Stack Summary](#15-tech-stack-summary)
  - [1.6 Security Posture](#16-security-posture)
  - [1.7 Scope & Constraints](#17-scope--constraints)
  - [1.8 Document Guide](#18-document-guide)
- **[2. Architecture Overview](#2-architecture-overview)**
  - [2.1 System Context](#21-system-context)
  - [2.2 Deployment Units](#22-deployment-units)
  - [2.3 Internal Architecture — Vertical Slices](#23-internal-architecture--vertical-slices)
  - [2.4 Dependency Rules](#24-dependency-rules)
  - [2.5 Request Flow (API)](#25-request-flow-api)
  - [2.6 Execution Flow (Worker)](#26-execution-flow-worker)
  - [2.7 Data Flow Between API and Worker](#27-data-flow-between-api-and-worker)
  - [2.8 External Integration Points](#28-external-integration-points)
  - [2.9 Cross-Cutting Concerns](#29-cross-cutting-concerns)
  - [2.10 Architecture Decision Records](#210-architecture-decision-records)
  - [2.11 Non-Functional Requirements & Design Targets](#211-non-functional-requirements--design-targets)
- **[3. Tech Stack & Key Libraries](#3-tech-stack--key-libraries)**
  - [3.1 Runtime & Frameworks](#31-runtime--frameworks)
  - [3.2 Job Scheduling & Workflow](#32-job-scheduling--workflow)
  - [3.3 File Transfer](#33-file-transfer)
  - [3.4 Cryptography & Key Management](#34-cryptography--key-management)
  - [3.5 Compression](#35-compression)
  - [3.6 Authentication & Authorization](#36-authentication--authorization)
  - [3.7 API & Validation](#37-api--validation)
  - [3.8 Database & Migrations](#38-database--migrations)
  - [3.9 JSON Serialization](#39-json-serialization)
  - [3.10 Logging & Observability](#310-logging--observability)
  - [3.11 Testing](#311-testing)
  - [3.12 Infrastructure & Deployment](#312-infrastructure--deployment)
  - [3.13 Package Version Policy](#313-package-version-policy)
  - [3.14 Library Decision Summary](#314-library-decision-summary)
- **[4. Domain Model](#4-domain-model)**
  - [4.1 Design Conventions](#41-design-conventions)
  - [4.2 Entity Catalog](#42-entity-catalog)
    - [4.2.1 Job System Entities](#421-job-system-entities)
    - [4.2.2 Connection Entities](#422-connection-entities)
    - [4.2.3 Key Store Entities](#423-key-store-entities)
    - [4.2.4 File Monitor Entities](#424-file-monitor-entities)
    - [4.2.5 Cross-Cutting Entities](#425-cross-cutting-entities)
  - [4.3 Value Objects](#43-value-objects)
  - [4.4 Enumerations](#44-enumerations)
  - [4.5 Entity Relationship Diagram](#45-entity-relationship-diagram)
- **[5. Job Engine Design](#5-job-engine-design)**
  - [5.1 Core Concepts](#51-core-concepts)
  - [5.2 Step Type Registry](#52-step-type-registry)
  - [5.3 Job State Machine](#53-job-state-machine)
  - [5.4 Step State Machine](#54-step-state-machine)
  - [5.5 Checkpoint & Resume](#55-checkpoint--resume)
  - [5.6 Step Context & Data Passing](#56-step-context--data-passing)
  - [5.7 Scheduling](#57-scheduling)
  - [5.8 Concurrency Management](#58-concurrency-management)
  - [5.9 Job Dependencies](#59-job-dependencies)
  - [5.10 Job Chains (Execution Groups)](#510-job-chains-execution-groups)
  - [5.11 Failure Handling](#511-failure-handling)
  - [5.12 Idempotency Rules by Step Type](#512-idempotency-rules-by-step-type)
  - [5.13 Step Timeouts](#513-step-timeouts)
  - [5.14 Cancellation Support](#514-cancellation-support)
  - [5.15 Temp File Management](#515-temp-file-management)
  - [5.16 Job Audit Trail](#516-job-audit-trail)
  - [5.17 Job Versioning](#517-job-versioning)
  - [5.18 Notification Hooks (V2 Preparation)](#518-notification-hooks-v2-preparation)
  - [5.19 Dry Run Mode (Future Consideration)](#519-dry-run-mode-future-consideration)
- **[6. Connection & Protocol Layer](#6-connection--protocol-layer)**
  - [6.1 Connection Entity](#61-connection-entity)
    - [6.1.1 Connection Groups](#611-connection-groups)
  - [6.2 Unified Transfer Interface](#62-unified-transfer-interface)
  - [6.3 Protocol Implementations](#63-protocol-implementations)
    - [6.3.1 SFTP — SSH.NET](#631-sftp--sshnet)
    - [6.3.2 FTP / FTPS — FluentFTP](#632-ftp--ftps--fluentftp)
    - [6.3.3 Protocol Support Matrix](#633-protocol-support-matrix)
  - [6.4 Connection Session Management](#64-connection-session-management)
    - [6.4.1 Job Connection Registry](#641-job-connection-registry)
    - [6.4.2 Session Health & Recovery](#642-session-health--recovery)
  - [6.5 Transfer Resume for Large Files](#65-transfer-resume-for-large-files)
    - [6.5.1 Upload Resume](#651-upload-resume)
    - [6.5.2 Download Resume](#652-download-resume)
    - [6.5.3 Resume Tracking in JobContext](#653-resume-tracking-in-jobcontext)
  - [6.6 Atomic Upload Pattern](#66-atomic-upload-pattern)
  - [6.7 Host Key Verification (SFTP)](#67-host-key-verification-sftp)
    - [6.7.1 Known Hosts Table](#671-known-hosts-table)
  - [6.8 SSH Key Store](#68-ssh-key-store)
    - [6.8.1 SSH Key Entity](#681-ssh-key-entity)
    - [6.8.2 Supported Key Formats](#682-supported-key-formats)
    - [6.8.3 Encryption at Rest](#683-encryption-at-rest)
  - [6.9 Credential Storage](#69-credential-storage)
  - [6.10 Directory Operations](#610-directory-operations)
  - [6.11 Test Connection Endpoint](#611-test-connection-endpoint)
  - [6.12 Transfer Progress Reporting](#612-transfer-progress-reporting)
  - [6.13 TLS Configuration (FTPS)](#613-tls-configuration-ftps)
  - [6.14 SSH Algorithm Configuration (SFTP)](#614-ssh-algorithm-configuration-sftp)
  - [6.15 Connection Audit Log](#615-connection-audit-log)
- **[7. Cryptography & Key Store](#7-cryptography--key-store)**
  - [7.1 Unified Crypto Interface](#71-unified-crypto-interface)
  - [7.2 Library Selection](#72-library-selection)
  - [7.3 Key Store](#73-key-store)
    - [7.3.1 Key Entity](#731-key-entity)
    - [7.3.2 Key Status Lifecycle](#732-key-status-lifecycle)
    - [7.3.3 Key Generation](#733-key-generation)
    - [7.3.4 Key Import](#734-key-import)
    - [7.3.5 Key Export](#735-key-export)
    - [7.3.6 Private Key Encryption at Rest](#736-private-key-encryption-at-rest)
    - [7.3.7 EnvelopeEncryptionService Implementation](#737-envelopeencryptionservice-implementation)
  - [7.4 Encryption Operations](#74-encryption-operations)
    - [7.4.1 Single-Recipient Encryption](#741-single-recipient-encryption)
    - [7.4.2 Multi-Recipient Encryption](#742-multi-recipient-encryption)
    - [7.4.3 Sign-Then-Encrypt](#743-sign-then-encrypt)
  - [7.5 Decryption Operations](#75-decryption-operations)
  - [7.6 Signing & Verification](#76-signing--verification)
    - [7.6.1 Signature Modes](#761-signature-modes)
    - [7.6.2 Verification](#762-verification)
  - [7.7 Streaming Crypto for Large Files](#77-streaming-crypto-for-large-files)
  - [7.8 Key Rotation Management](#78-key-rotation-management)
  - [7.9 Key Audit Log](#79-key-audit-log)
  - [7.10 ECC Future-Proofing](#710-ecc-future-proofing)
- **[8. File Operations](#8-file-operations)**
  - [8.1 Compression & Decompression](#81-compression--decompression)
    - [8.1.1 Unified Interface](#811-unified-interface)
    - [8.1.2 Supported Formats](#812-supported-formats)
    - [8.1.3 Streaming Architecture](#813-streaming-architecture)
    - [8.1.4 Progress Reporting](#814-progress-reporting)
    - [8.1.5 Multi-File Handling](#815-multi-file-handling)
    - [8.1.6 Split Archives](#816-split-archives)
    - [8.1.7 Temp File Strategy](#817-temp-file-strategy)
    - [8.1.8 Archive Extraction Safety](#818-archive-extraction-safety)
    - [8.1.9 Archive Integrity Verification](#819-archive-integrity-verification)
    - [8.1.10 Archive Inspection](#8110-archive-inspection)
- **[9. File Monitor System](#9-file-monitor-system)**
  - [9.1 Monitor Entity](#91-monitor-entity)
  - [9.2 Local Monitoring — FileSystemWatcher + Polling Fallback](#92-local-monitoring--filesystemwatcher--polling-fallback)
  - [9.3 Remote Monitoring — Scheduled Polling](#93-remote-monitoring--scheduled-polling)
  - [9.4 File Readiness Detection](#94-file-readiness-detection)
  - [9.5 File Pattern Filtering](#95-file-pattern-filtering)
  - [9.6 Monitor → Job Binding & Context Injection](#96-monitor--job-binding--context-injection)
  - [9.7 Deduplication](#97-deduplication)
  - [9.8 Monitor State Machine](#98-monitor-state-machine)
  - [9.9 Error Handling & Resilience](#99-error-handling--resilience)
- **[10. API Design](#10-api-design)**
  - [10.1 General Conventions](#101-general-conventions)
  - [10.2 Jobs API](#102-jobs-api)
  - [10.3 Job Chains API](#103-job-chains-api)
  - [10.4 Connections API](#104-connections-api)
  - [10.5 PGP Keys API](#105-pgp-keys-api)
  - [10.6 SSH Keys API](#106-ssh-keys-api)
  - [10.7 File Monitors API](#107-file-monitors-api)
  - [10.8 Tags API](#108-tags-api)
  - [10.9 Audit Log API](#109-audit-log-api)
  - [10.10 System Settings API](#1010-system-settings-api)
  - [10.11 Dashboard & Summary Endpoints](#1011-dashboard--summary-endpoints)
  - [10.12 Step Type Registry API](#1012-step-type-registry-api)
  - [10.13 OpenAPI / Swagger Configuration](#1013-openapi--swagger-configuration)
  - [10.14 Request Validation](#1014-request-validation)
- **[11. Frontend Architecture](#11-frontend-architecture)**
  - [11.1 Tech Stack](#111-tech-stack)
  - [11.2 Rendering Strategy](#112-rendering-strategy)
    - [11.2.1 Static Export Constraints](#1121-static-export-constraints)
    - [11.2.2 Authentication Security in a Static SPA](#1122-authentication-security-in-a-static-spa)
  - [11.3 Authentication Flow](#113-authentication-flow)
  - [11.4 Project Structure](#114-project-structure)
  - [11.5 API Client Layer](#115-api-client-layer)
  - [11.6 Server State Management (TanStack Query)](#116-server-state-management-tanstack-query)
  - [11.7 Error Handling](#117-error-handling)
  - [11.8 Role-Based UI](#118-role-based-ui)
  - [11.9 Key UI Components](#119-key-ui-components)
    - [11.9.1 DataTable](#1191-datatable)
    - [11.9.2 Job Builder](#1192-job-builder)
    - [11.9.3 Execution Timeline](#1193-execution-timeline)
  - [11.10 Theming](#1110-theming)
  - [11.11 Build & Deployment](#1111-build--deployment)
- **[12. Security](#12-security)**
  - [12.1 Authentication](#121-authentication)
  - [12.2 Authorization — Role-Based Access Control (RBAC)](#122-authorization--role-based-access-control-rbac)
  - [12.3 Data Protection at Rest](#123-data-protection-at-rest)
    - [12.3.1 Envelope Encryption (PGP & SSH Key Material)](#1231-envelope-encryption-pgp--ssh-key-material)
    - [12.3.2 Database-Level Encryption](#1232-database-level-encryption)
    - [12.3.3 What Is Encrypted](#1233-what-is-encrypted)
    - [12.3.4 What Is NOT Encrypted (and Why)](#1234-what-is-not-encrypted-and-why)
  - [12.4 Data Protection in Transit](#124-data-protection-in-transit)
    - [12.4.1 API Layer (Client ↔ Courier)](#1241-api-layer-client--courier)
    - [12.4.2 File Transfer Layer (Courier ↔ Remote Servers)](#1242-file-transfer-layer-courier--remote-servers)
    - [12.4.3 Database Connections](#1243-database-connections)
    - [12.4.4 Azure Key Vault](#1244-azure-key-vault)
  - [12.5 Secrets Management](#125-secrets-management)
    - [12.5.1 Application Secrets](#1251-application-secrets)
    - [12.5.2 User Secrets (Connection Credentials, Key Passphrases)](#1252-user-secrets-connection-credentials-key-passphrases)
  - [12.6 API Security Hardening](#126-api-security-hardening)
    - [12.6.1 CORS](#1261-cors)
    - [12.6.2 Security Headers](#1262-security-headers)
    - [12.6.3 Request Size Limits](#1263-request-size-limits)
    - [12.6.4 Input Validation & Injection Prevention](#1264-input-validation--injection-prevention)
    - [12.6.5 Anti-Forgery](#1265-anti-forgery)
  - [12.7 Audit & Accountability](#127-audit--accountability)
  - [12.8 Sensitive Data Handling Rules](#128-sensitive-data-handling-rules)
  - [12.9 Network Security](#129-network-security)
  - [12.10 FIPS 140-2 / 140-3 Compliance](#1210-fips-140-2--140-3-compliance)
    - [12.10.1 What "FIPS Mode" Means for Courier](#12101-what-fips-mode-means-for-courier)
    - [12.10.2 Cryptographic Module Strategy](#12102-cryptographic-module-strategy)
    - [12.10.3 FIPS-Approved Algorithms](#12103-fips-approved-algorithms)
    - [12.10.4 Ed25519 / Curve25519 Handling](#12104-ed25519--curve25519-handling)
    - [12.10.5 Connection-Level FIPS Configuration](#12105-connection-level-fips-configuration)
    - [12.10.6 FIPS Enforcement Architecture](#12106-fips-enforcement-architecture)
    - [12.10.7 Validation & Compliance Evidence](#12107-validation--compliance-evidence)
    - [12.10.8 System Settings Addition](#12108-system-settings-addition)
  - [12.11 Security Summary](#1211-security-summary)
  - [12.12 Threat Model & Trust Boundaries](#1212-threat-model--trust-boundaries)
    - [12.12.1 Trust Boundaries](#12121-trust-boundaries)
    - [12.12.2 Asset Inventory](#12122-asset-inventory)
    - [12.12.3 Threat Scenarios](#12123-threat-scenarios)
    - [12.12.4 Accepted Risks (V1)](#12124-accepted-risks-v1)
- **[13. Database Schema](#13-database-schema)**
  - [13.1 Migration Strategy](#131-migration-strategy)
    - [13.1.1 Migration Safety in Multi-Replica Deployments](#1311-migration-safety-in-multi-replica-deployments)
  - [13.2 Conventions](#132-conventions)
  - [13.3 Schema DDL](#133-schema-ddl)
    - [13.3.1 Job System Tables](#1331-job-system-tables)
    - [13.3.2 Connection Tables](#1332-connection-tables)
    - [13.3.3 Key Store Tables](#1333-key-store-tables)
    - [13.3.4 File Monitor Tables](#1334-file-monitor-tables)
    - [13.3.5 Cross-Cutting Tables](#1335-cross-cutting-tables)
    - [13.3.6 Quartz.NET Scheduler Tables](#1336-quartznet-scheduler-tables)
  - [13.4 EF Core Mapping Configuration](#134-ef-core-mapping-configuration)
  - [13.5 Partition Management](#135-partition-management)
  - [13.6 Data Retention & Archival](#136-data-retention--archival)
    - [13.6.1 Partition Maintenance Failure Modes](#1361-partition-maintenance-failure-modes)
  - [13.7 Performance Considerations](#137-performance-considerations)
    - [13.7.1 JSONB Column Index Strategy](#1371-jsonb-column-index-strategy)
    - [13.7.2 General Performance Notes](#1372-general-performance-notes)
- **[14. Deployment & Infrastructure](#14-deployment--infrastructure)**
  - [14.1 Environments](#141-environments)
  - [14.2 Docker Images](#142-docker-images)
    - [14.2.1 API Host](#1421-api-host)
    - [14.2.2 Worker Host](#1422-worker-host)
    - [14.2.3 Frontend](#1423-frontend)
  - [14.3 Azure Container Apps Configuration](#143-azure-container-apps-configuration)
    - [14.3.1 Container App Definitions](#1431-container-app-definitions)
    - [14.3.2 Networking](#1432-networking)
    - [14.3.3 Managed Identity](#1433-managed-identity)
  - [14.4 Scaling Strategy](#144-scaling-strategy)
  - [14.5 CI/CD Pipeline (GitHub Actions)](#145-cicd-pipeline-github-actions)
    - [14.5.1 Pipeline Overview](#1451-pipeline-overview)
    - [14.5.2 PR Check Workflow](#1452-pr-check-workflow)
    - [14.5.3 Build & Deploy Workflow](#1453-build--deploy-workflow)
  - [14.6 Database Migrations in CI/CD](#146-database-migrations-in-cicd)
  - [14.7 Local Development (.NET Aspire)](#147-local-development-net-aspire)
  - [14.8 Health Checks](#148-health-checks)
  - [14.9 Observability](#149-observability)
  - [14.10 Backup & Disaster Recovery](#1410-backup--disaster-recovery)
  - [14.11 Infrastructure Summary](#1411-infrastructure-summary)
- **[15. V2 Roadmap](#15-v2-roadmap)**
  - [15.1 Phase 1 — Event-Driven Architecture](#151-phase-1--event-driven-architecture)
    - [15.1.1 Outbox Relay Service](#1511-outbox-relay-service)
    - [15.1.2 Event-Driven Job Scheduling](#1512-event-driven-job-scheduling)
    - [15.1.3 Horizontal Worker Scaling](#1513-horizontal-worker-scaling)
  - [15.2 Phase 2 — Notifications & Alerting](#152-phase-2--notifications--alerting)
    - [15.2.1 Notification Channels](#1521-notification-channels)
    - [15.2.2 Notification Rules Engine](#1522-notification-rules-engine)
    - [15.2.3 Alerting for Operational Events](#1523-alerting-for-operational-events)
  - [15.3 Phase 3 — API Access & Security Hardening](#153-phase-3--api-access--security-hardening)
    - [15.3.1 Machine-to-Machine API Access](#1531-machine-to-machine-api-access)
    - [15.3.2 Security Role Enhancements](#1532-security-role-enhancements)
    - [15.3.3 External SIEM Integration](#1533-external-siem-integration)
    - [15.3.4 File Content Scanning](#1534-file-content-scanning)
  - [15.4 Phase 4 — Observability & Metrics](#154-phase-4--observability--metrics)
    - [15.4.1 Metrics Dashboard](#1541-metrics-dashboard)
    - [15.4.2 Real-Time Transfer Progress](#1542-real-time-transfer-progress)
  - [15.5 Phase 5 — Platform Expansion](#155-phase-5--platform-expansion)
    - [15.5.1 Cloud Storage Connectors](#1551-cloud-storage-connectors)
    - [15.5.2 File Content Transformation](#1552-file-content-transformation)
    - [15.5.3 Multi-Tenancy](#1553-multi-tenancy)
    - [15.5.4 Additional Enhancements](#1554-additional-enhancements)
  - [15.6 V1 → V2 Migration Path](#156-v1--v2-migration-path)
  - [15.7 Dependency Graph](#157-dependency-graph)
  - [15.8 What V1 Built Specifically for V2](#158-what-v1-built-specifically-for-v2)

---

## 1. Executive Summary

Courier is an enterprise file transfer and job management platform that automates the movement, encryption, compression, and scheduling of files between internal systems and external partner servers. It replaces manual SFTP operations, ad-hoc scripts, and disconnected scheduling tools with a unified, auditable, secure platform.

### 1.1 Problem

Organizations that exchange files with external partners — encrypted invoices, data feeds, compliance reports — rely on a patchwork of manual processes: logging into SFTP clients, running PGP commands from the terminal, compressing files by hand, and scheduling transfers via cron jobs or Windows Task Scheduler. These processes are fragile, unauditable, and dependent on tribal knowledge. When a transfer fails at 2 AM, there is no centralized log, no automatic retry, and no visibility into what happened.

### 1.2 Solution

Courier provides a web-based platform where users define **Jobs** — multi-step pipelines that chain together file transfers (SFTP, FTP, FTPS), PGP encryption/decryption, compression (ZIP, GZIP, TAR, 7z), and file operations into repeatable, schedulable workflows. Jobs can be triggered on a cron schedule, executed manually, or fired automatically by **File Monitors** that watch local or remote directories for new files.

All connection credentials and cryptographic keys are encrypted at rest using AES-256-GCM envelope encryption backed by Azure Key Vault. All operations are recorded in an append-only audit log. When FIPS mode is enabled, the system restricts to FIPS-approved algorithms and runs on validated cryptographic modules where the platform provides them (see Section 12.10).

### 1.3 Key Capabilities (V1)

- **Job Engine**: Multi-step pipelines with configurable failure policies (retry step, retry job, skip, abort), step-level timeouts, and execution state tracking (pause, resume, cancel)
- **Job Chains**: Ordered sequences of Jobs with dependency edges, allowing complex multi-job workflows
- **File Monitors**: Local filesystem watchers (via `FileSystemWatcher`) and remote directory pollers that detect new or modified files and trigger bound Jobs or Chains
- **Connection Management**: Centralized SFTP, FTP, and FTPS connection configuration with credential storage, host key verification, and connection testing
- **PGP & SSH Key Stores**: Generate, import, export, and manage PGP and SSH keys with full lifecycle tracking (active → expiring → retired → revoked), automated rotation detection, and key successor chaining
- **Scheduling**: Cron-based and one-shot scheduling via Quartz.NET with persistent job store
- **Tagging**: Flexible tagging system across all resource types for organization and filtering
- **Audit Log**: Append-only log of every operation, queryable by entity, user, operation type, and time range
- **Dashboard**: Real-time summary of system health, recent executions, active monitors, and key expiration warnings
- **RBAC**: Three-role model (Admin, Operator, Viewer) via Azure Entra ID App Roles

### 1.4 Architecture at a Glance

Courier is deployed as three independent processes:

- **API Host** (ASP.NET Core 10) — REST API with OpenAPI spec, Entra ID authentication, FluentValidation
- **Worker Host** (.NET 10 Worker Service) — Quartz.NET scheduler, Job Engine, File Monitor polling, background maintenance
- **Frontend** (Next.js) — Web UI for job management, monitoring, key management, and audit

All three share a single **PostgreSQL 16+** database. Cryptographic key material is protected by **Azure Key Vault** (FIPS 140-2 Level 2/3). The codebase follows a **vertical slice** architecture with feature folders (Jobs, Connections, Keys, Monitors, Tags, Audit) that each own their full stack from controller to database mapping.

### 1.5 Tech Stack Summary

| Layer | Technology |
|-------|-----------|
| Backend | .NET 10, ASP.NET Core, EF Core (query only) |
| Frontend | Next.js (React) |
| Database | PostgreSQL 16+ with range partitioning |
| Migrations | DbUp (raw SQL scripts) |
| Scheduling | Quartz.NET (AdoJobStore) |
| File Transfer | SSH.NET (SFTP), FluentFTP (FTP/FTPS) |
| Cryptography | BouncyCastle (PGP), System.Security.Cryptography (AES-256-GCM, RSA/ECDSA) |
| Key Management | Azure Key Vault |
| Authentication | Azure Entra ID (OAuth 2.0 + PKCE) |
| Logging | Serilog → Seq (dev) / Application Insights (prod) |
| Testing | xUnit, Shouldly, NSubstitute, Testcontainers |

### 1.6 Security Posture

- FIPS-approved algorithms enforced for internal operations; validated module usage depends on OS/container configuration (Section 12.10)
- AES-256-GCM envelope encryption for all key material and credentials at rest
- TLS 1.2+ enforced on all transport layers (API, database, Key Vault, FTPS)
- Single-tenant Entra ID JWT validation with role-based access control
- Append-only audit log with sensitive data redaction in all logging
- No secrets on disk in production (Key Vault + Managed Identity)

### 1.7 Scope & Constraints

**In scope (V1)**: Job creation and execution, SFTP/FTP/FTPS transfers, PGP and SSH key management, file compression, cron scheduling, file monitoring, tagging, audit logging, role-based access, FIPS algorithm enforcement.

**Out of scope (V1)**: Real-time notifications (webhooks, email alerts), machine-to-machine API access (client credentials), multi-tenancy, file content transformation (CSV parsing, XML mapping), cloud storage connectors (S3, Azure Blob as transfer targets), and horizontal Worker scaling. These are candidates for the V2 roadmap (Section 15).

### 1.8 Document Guide

| Section | Contents |
|---------|----------|
| 2. Architecture Overview | System context, deployment units, vertical slice structure, data flow |
| 3. Tech Stack & Key Libraries | Complete technology inventory with version constraints |
| 4. Domain Model | All 24 entities, value objects, enumerations, entity relationship diagram |
| 5. Job Engine | Pipeline execution, step handlers, failure policies, state machine |
| 6. Connection & Protocol | SFTP/FTP/FTPS abstraction, credential storage, host key verification |
| 7. Crypto & Key Store | PGP/SSH key lifecycle, envelope encryption, key rotation |
| 8. File Operations | Compression, decompression, archive splitting, 7z CLI wrapper |
| 9. File Monitor | Local/remote monitoring, pattern matching, deduplication, job binding |
| 10. API Design | REST endpoints, standard response model, error codes, OpenAPI |
| 11. Frontend Architecture | Next.js UI structure, component library, API client |
| 12. Security | Auth, RBAC, encryption at rest/transit, FIPS, secrets, hardening, audit |
| 13. Database Schema | Full DDL, partitioning, EF Core mapping, retention policy |
| 14. Deployment & Infrastructure | Docker, Kubernetes, CI/CD, environments |
| 15. V2 Roadmap | Event-driven architecture, notifications, M2M access, metrics, cloud connectors, multi-tenancy, dependency graph |

---

## 2. Architecture Overview

Courier is a three-tier application deployed as three independent processes: a REST API host, a background Worker host, and a Next.js frontend. All three share a single PostgreSQL database and communicate indirectly through the database and Azure Key Vault — there is no inter-process messaging bus in V1. This polling-based coordination has a documented throughput ceiling (~50–100 jobs/hour, 3–10s pickup latency) that is acceptable for V1's target workload; Section 15 describes the migration to event-driven scheduling.

### 2.1 System Context

```
┌─────────────────────────────────────────────────────────────────────────┐
│                            COURIER PLATFORM                             │
│                                                                         │
│  ┌──────────────┐     ┌──────────────────┐     ┌────────────────────┐  │
│  │   Frontend    │────►│     API Host     │     │   Worker Host      │  │
│  │   (Next.js)   │◄────│  (ASP.NET Core)  │     │  (.NET Worker)     │  │
│  │               │     │                  │     │                    │  │
│  │  • Dashboard  │HTTPS│  • REST API      │     │  • Job Engine      │  │
│  │  • Job Builder│     │  • Auth (Entra)  │     │  • Quartz Scheduler│  │
│  │  • Monitor UI │     │  • Validation    │     │  • File Monitors   │  │
│  │  • Key Mgmt   │     │  • OpenAPI/Swagger│    │  • Key Rotation    │  │
│  │  • Audit Log  │     │                  │     │  • Partition Maint. │  │
│  └──────────────┘     └────────┬─────────┘     └─────────┬──────────┘  │
│                                │                          │             │
│                        ┌───────▼──────────────────────────▼──────┐      │
│                        │            PostgreSQL 16+               │      │
│                        │                                         │      │
│                        │  Jobs, Connections, Keys, Monitors,     │      │
│                        │  Executions, Audit Log, Quartz Tables   │      │
│                        └─────────────────────────────────────────┘      │
│                                                                         │
└─────────────────────────────────────────────────────────────────────────┘
                │                    │                      │
                │                    │                      │
        ┌───────▼──────┐    ┌───────▼──────┐     ┌────────▼────────┐
        │  Azure Entra  │    │  Azure Key   │     │  Partner SFTP/  │
        │  ID (Auth)    │    │  Vault       │     │  FTP Servers    │
        └──────────────┘    └──────────────┘     └─────────────────┘
```

### 2.2 Deployment Units

| Unit | Technology | Responsibilities | Scaling |
|------|-----------|------------------|---------|
| **API Host** | ASP.NET Core 10 | REST API, authentication, request validation, CRUD operations, OpenAPI spec | Horizontal — stateless, any number of replicas behind a load balancer |
| **Worker Host** | .NET 10 Worker Service | Quartz.NET scheduler, Job Engine execution, File Monitor polling, key rotation checks, partition maintenance | Single instance (V1) — Quartz AdoJobStore handles clustered failover if scaled later |
| **Frontend** | Next.js (standalone) | User interface, OAuth 2.0 Authorization Code + PKCE flow, API consumption | Horizontal — self-contained Node.js server in container |

**Why separate API and Worker?** The API host is request-driven and benefits from horizontal scaling. The Worker host is long-running and CPU/IO-bound (file transfers, encryption, compression). Separating them allows independent scaling, independent deployment, and prevents a runaway job from starving API response times. Both processes share the same domain logic via shared class libraries.

### 2.3 Internal Architecture — Vertical Slices

Courier organizes code by **feature**, not by technical layer. Each feature folder contains everything needed for that domain: API controllers, request/response DTOs, validators, application services, domain entities, and infrastructure adapters.

**Solution structure**:

```
Courier.sln
│
├── src/
│   ├── Courier.Api/                        ← API Host (ASP.NET Core)
│   │   ├── Program.cs                      ← Startup, middleware, DI
│   │   ├── Middleware/                      ← Exception handler, auth, CORS
│   │   └── appsettings.json
│   │
│   ├── Courier.Worker/                     ← Worker Host (.NET Worker Service)
│   │   ├── Program.cs                      ← Startup, hosted services, DI
│   │   └── appsettings.json
│   │
│   ├── Courier.Features/                   ← Shared feature library (API + Worker)
│   │   ├── Jobs/
│   │   │   ├── Entities/                   ← Job, JobStep, JobVersion, JobExecution, StepExecution
│   │   │   ├── Dtos/                       ← JobDto, CreateJobRequest, JobFilter
│   │   │   ├── Validators/                 ← CreateJobValidator, UpdateJobValidator
│   │   │   ├── Services/                   ← JobService, JobExecutionService
│   │   │   ├── Controllers/                ← JobsController
│   │   │   ├── StepTypes/                  ← IStepHandler implementations
│   │   │   └── Mapping/                    ← EF Core entity configuration
│   │   │
│   │   ├── Chains/
│   │   │   ├── Entities/
│   │   │   ├── Dtos/
│   │   │   ├── Validators/
│   │   │   ├── Services/
│   │   │   ├── Controllers/
│   │   │   └── Mapping/
│   │   │
│   │   ├── Connections/
│   │   │   ├── Entities/                   ← Connection, KnownHost
│   │   │   ├── Dtos/
│   │   │   ├── Validators/
│   │   │   ├── Services/                   ← ConnectionService, ConnectionTester
│   │   │   ├── Controllers/
│   │   │   ├── Protocols/                  ← SftpClient, FtpClient, FtpsClient adapters
│   │   │   └── Mapping/
│   │   │
│   │   ├── Keys/
│   │   │   ├── Pgp/                        ← PgpKey entity, PGP services, controllers
│   │   │   ├── Ssh/                        ← SshKey entity, SSH services, controllers
│   │   │   └── Shared/                     ← ICryptoProvider, Key rotation service
│   │   │
│   │   ├── Monitors/
│   │   │   ├── Entities/                   ← FileMonitor, MonitorJobBinding, MonitorFileLog
│   │   │   ├── Dtos/
│   │   │   ├── Validators/
│   │   │   ├── Services/                   ← MonitorService, LocalWatcher, RemotePoller
│   │   │   ├── Controllers/
│   │   │   └── Mapping/
│   │   │
│   │   ├── Tags/
│   │   │   ├── Entities/                   ← Tag, EntityTag
│   │   │   ├── Dtos/
│   │   │   ├── Services/
│   │   │   ├── Controllers/
│   │   │   └── Mapping/
│   │   │
│   │   ├── Audit/
│   │   │   ├── Entities/                   ← AuditLogEntry, DomainEvent
│   │   │   ├── Dtos/
│   │   │   ├── Services/                   ← AuditService
│   │   │   ├── Controllers/
│   │   │   └── Mapping/
│   │   │
│   │   ├── Dashboard/
│   │   │   ├── Dtos/                       ← SummaryDto, RecentExecutionDto
│   │   │   ├── Services/                   ← DashboardService
│   │   │   └── Controllers/
│   │   │
│   │   └── Settings/
│   │       ├── Entities/                   ← SystemSetting
│   │       ├── Dtos/
│   │       ├── Services/
│   │       ├── Controllers/
│   │       └── Mapping/
│   │
│   ├── Courier.Domain/                     ← Shared domain primitives
│   │   ├── Common/                         ← ApiResponse<T>, ErrorCodes, enums
│   │   ├── ValueObjects/                   ← FailurePolicy, StepConfiguration, etc.
│   │   └── Interfaces/                     ← ITransferClient, ICryptoProvider, IStepHandler
│   │
│   ├── Courier.Infrastructure/             ← Cross-cutting infrastructure
│   │   ├── Persistence/                    ← CourierDbContext, global filters, interceptors
│   │   ├── Encryption/                     ← EnvelopeEncryptionService, KeyVaultClient
│   │   ├── Compression/                    ← ZIP, GZIP, TAR, 7z providers
│   │   └── Migrations/                     ← DbUp runner, embedded SQL scripts
│   │
│   └── Courier.Frontend/                   ← Next.js project (separate build pipeline)
│       ├── src/
│       │   ├── app/                        ← Next.js app router
│       │   ├── components/                 ← Shared UI components
│       │   └── lib/                        ← API client, auth, utilities
│       └── package.json
│
├── tests/
│   ├── Courier.Tests.Unit/
│   ├── Courier.Tests.Integration/
│   └── Courier.Tests.Architecture/
│
└── infra/                                  ← Deployment configs
    ├── docker/                             ← Dockerfiles for API, Worker
    ├── k8s/                                ← Kubernetes manifests
    └── scripts/                            ← CI/CD, seed data
```

**Key principle**: Feature folders own their entire vertical slice. If you need to understand how Jobs work, you open `Courier.Features/Jobs/` — the entities, DTOs, validators, services, controllers, and EF mappings are all there. Cross-cutting concerns (database context, encryption, compression) live in `Courier.Infrastructure` and are injected via DI.

### 2.4 Dependency Rules

```
┌───────────────────────────────────────────┐
│             Courier.Api                    │  ← Thin host: startup, middleware
│             Courier.Worker                 │  ← Thin host: startup, hosted services
├───────────────────────────────────────────┤
│         Courier.Features                   │  ← All feature slices
├───────────────────────────────────────────┤
│       Courier.Infrastructure               │  ← EF Core, encryption, compression
├───────────────────────────────────────────┤
│          Courier.Domain                    │  ← Entities, interfaces, value objects
└───────────────────────────────────────────┘

  References flow downward only. No project references upward.
```

- `Courier.Api` → references `Courier.Features`, `Courier.Infrastructure`, `Courier.Domain`
- `Courier.Worker` → references `Courier.Features`, `Courier.Infrastructure`, `Courier.Domain`
- `Courier.Features` → references `Courier.Infrastructure`, `Courier.Domain`
- `Courier.Infrastructure` → references `Courier.Domain`
- `Courier.Domain` → references nothing (no NuGet dependencies except value types)

These rules are enforced by architecture tests in `Courier.Tests.Architecture` using NetArchTest or ArchUnitNET.

### 2.5 Request Flow (API)

A typical API request flows through the following pipeline:

```
Client Request (HTTPS)
    │
    ▼
┌─────────────────────────┐
│  Kestrel / Reverse Proxy │
└────────────┬────────────┘
             ▼
┌─────────────────────────┐
│  ApiExceptionMiddleware  │  ← Catches unhandled exceptions → ApiResponse envelope
└────────────┬────────────┘
             ▼
┌─────────────────────────┐
│  CORS Middleware         │  ← Validates origin against Frontend:Origin
└────────────┬────────────┘
             ▼
┌─────────────────────────┐
│  Authentication          │  ← Validates Entra ID JWT bearer token
└────────────┬────────────┘
             ▼
┌─────────────────────────┐
│  Authorization           │  ← Checks [Authorize(Roles = "...")] attributes
└────────────┬────────────┘
             ▼
┌─────────────────────────┐
│  Security Headers        │  ← X-Content-Type-Options, CSP, HSTS, etc.
└────────────┬────────────┘
             ▼
┌─────────────────────────┐
│  Serilog Request Logging │  ← Structured log with method, path, status, duration
└────────────┬────────────┘
             ▼
┌─────────────────────────┐
│  Controller Action       │  ← Route matched, model binding
└────────────┬────────────┘
             ▼
┌─────────────────────────┐
│  FluentValidation Filter │  ← Validates request body → 400 if invalid
└────────────┬────────────┘
             ▼
┌─────────────────────────┐
│  Application Service     │  ← Business logic, domain operations
└────────────┬────────────┘
             ▼
┌─────────────────────────┐
│  EF Core / DbContext     │  ← Query or persist via PostgreSQL
└────────────┬────────────┘
             ▼
┌─────────────────────────┐
│  ApiResponse<T> Envelope │  ← Wrap result in standard response model
└────────────┬────────────┘
             ▼
        JSON Response
```

### 2.6 Execution Flow (Worker)

A scheduled job execution flows through the Worker host:

```
Quartz.NET Trigger Fires
    │
    ▼
┌─────────────────────────┐
│  QuartzJobAdapter        │  ← Quartz IJob → resolves Courier's JobExecutionService via DI
└────────────┬────────────┘
             ▼
┌─────────────────────────┐
│  JobExecutionService     │  ← Creates JobExecution record, loads Job + Steps + Version
└────────────┬────────────┘
             ▼
┌─────────────────────────┐
│  Step Loop               │  ← For each Step in order:
│  │                       │
│  │  ┌─────────────────┐ │
│  │  │ IStepHandler     │ │  ← Resolved by typeKey from DI container
│  │  │ .ExecuteAsync()  │ │
│  │  └────────┬────────┘ │
│  │           │           │
│  │  ┌────────▼────────┐ │
│  │  │ Transfer /       │ │  ← ITransferClient, ICryptoProvider, ICompressionProvider
│  │  │ Encrypt /        │ │
│  │  │ Compress         │ │
│  │  └────────┬────────┘ │
│  │           │           │
│  │  StepExecution saved  │  ← State, duration, bytes, output
│  │  JobContext updated   │  ← Output variables for next step
│  │                       │
└────────────┬────────────┘
             ▼
┌─────────────────────────┐
│  JobExecution completed  │  ← Final state: Completed / Failed
│  Temp directory cleaned  │  ← Immediate cleanup on completion
│  Audit event logged      │
│  Downstream triggers     │  ← Dependent jobs / chain next member
└─────────────────────────┘
```

### 2.7 Data Flow Between API and Worker

The API and Worker hosts do not communicate directly. They coordinate through the database:

```
┌──────────────┐                              ┌──────────────┐
│   API Host   │                              │ Worker Host  │
│              │                              │              │
│  POST /jobs/{id}/execute                    │              │
│       │                                     │              │
│       ▼                                     │              │
│  Insert job_executions                      │              │
│  row (state: 'queued')  ──────────────────► │  Quartz polls│
│                            PostgreSQL        │  AdoJobStore │
│  Insert job_schedules  ──────────────────►  │              │
│  row (cron expression)                      │  Quartz picks│
│                                             │  up trigger  │
│                                             │       │      │
│  GET /jobs/{id}/executions                  │       ▼      │
│       │                                     │  Execute job │
│       ▼                                     │  Update rows │
│  Read job_executions ◄──────────────────────│  (state,     │
│  rows (state, results)     PostgreSQL       │   results)   │
│                                             │              │
└──────────────┘                              └──────────────┘
```

For manual execution (`POST /api/v1/jobs/{id}/execute`), the API host creates a `job_executions` record with state `queued` and schedules an immediate Quartz trigger. The Worker's Quartz scheduler picks up the trigger via its AdoJobStore poll interval and begins execution. The API host reads execution status by querying the same `job_executions` table.

**Throughput ceiling and known limitations**:

Database-as-bus is a deliberate V1 tradeoff: zero additional infrastructure, simple debugging (query the tables), and transactional consistency. It has predictable limits that should inform capacity planning:

| Metric | V1 Design Point | Bottleneck |
|--------|-----------------|-----------|
| Job throughput | ~50–100 jobs/hour | Concurrency limit (default 5) × avg job duration. Most file transfer jobs run 10–120 seconds. |
| Queue poll latency | 3–10 seconds (p95) | Quartz AdoJobStore poll interval (default 5s) + queue dequeue poll (default 5s). Worst case on a fresh trigger is the sum of both intervals. |
| File Monitor throughput | ~200 files/minute (local), ~30 files/minute (remote per connection) | Local: FileSystemWatcher + stability window. Remote: poll interval × connection overhead. |
| Typical file size | 1 KB – 500 MB | Streaming architecture handles large files. Memory usage scales with step buffer size (default 8 KB), not file size. |
| Concurrent polling load | Quartz (1 query/5s) + queue dequeue (1 query/5s) + N monitors (1 query/interval each) | Single Worker instance: ~2–10 queries/second to PostgreSQL. Negligible for a dedicated database. |

**Known failure modes at scale**:

- **Poll jitter under load**: When the database is under heavy write load (e.g., bulk audit logging during many concurrent jobs), poll queries experience variable latency. This manifests as inconsistent job pickup times. Mitigation: Quartz and queue polls use dedicated read connections, not the same connection pool as writes.
- **Thundering herd on restart**: If the Worker restarts with a backlog of queued jobs, Quartz fires all pending triggers simultaneously, exceeding the concurrency limit. Mitigation: the concurrency semaphore (Section 5.8) gates actual execution — excess triggers enter `Queued` state and wait.
- **No backpressure from API to Worker**: The API can queue jobs faster than the Worker can execute them. There is no feedback mechanism to slow down callers. Mitigation: the API returns the queue position in the response, and the dashboard shows queue depth. Alert on queue depth > configurable threshold.
- **"Exactly once" is not guaranteed**: Database polling provides at-least-once pickup semantics. `FOR UPDATE SKIP LOCKED` (Section 5.8) prevents duplicate pickup in the steady state, but crash recovery could re-execute a job that was in progress. Mitigation: jobs are designed to be re-runnable (overwrite semantics on upload), and the `job_executions` table records the outcome of each attempt.

These limits are acceptable for V1's target workload (internal file transfer operations, not high-frequency event processing). Section 15 documents the V2 migration to event-driven scheduling that removes the polling bottleneck.

### 2.8 External Integration Points

| External System | Direction | Protocol | Purpose | Section |
|----------------|-----------|----------|---------|---------|
| Azure Entra ID | Inbound | OAuth 2.0 / OIDC | User authentication, role claims | 12.1 |
| Azure Key Vault | Outbound | HTTPS (REST) | Master key wrap/unwrap, application secrets | 7, 12 |
| Azure Blob Storage | Outbound | HTTPS (REST) | Archived partition data (cold storage) | 13.6 |
| Azure Application Insights | Outbound | HTTPS | Telemetry, tracing, alerting (prod) | 3.10 |
| Seq | Outbound | HTTP | Structured log search (dev only) | 3.10 |
| Azure Function Apps | Outbound | HTTPS (Admin API) | Trigger serverless functions as job steps; poll completion via App Insights | 5.2, 6.1 |
| Azure Log Analytics | Outbound | HTTPS (REST) | Query Application Insights for function execution status and traces | 5.2 |
| Partner SFTP servers | Outbound | SFTP (SSH) | File transfer — upload, download, directory listing | 6 |
| Partner FTP/FTPS servers | Outbound | FTP / FTPS | File transfer — upload, download, directory listing | 6 |
| PostgreSQL | Both | TCP (SSL) | Primary data store | 13 |

### 2.9 Cross-Cutting Concerns

| Concern | Implementation | Owner |
|---------|---------------|-------|
| **Authentication** | Entra ID JWT validation via Microsoft.Identity.Web | API Host middleware |
| **Authorization** | Role-based `[Authorize]` attributes (Admin, Operator, Viewer) | API Host controllers |
| **Logging** | Serilog → Seq (dev) / App Insights (prod) with sensitive data redaction | Both hosts |
| **Audit** | AuditService writes to `audit_log_entries` on every state change | Both hosts |
| **Encryption** | EnvelopeEncryptionService wrapping Key Vault + AES-256-GCM | Both hosts |
| **FIPS compliance** | Algorithm restrictions + validated module detection (Section 12.10) | Both hosts |
| **Error handling** | ApiExceptionMiddleware (API), try/catch in hosted services (Worker) | Per host |
| **Health checks** | .NET Aspire health endpoints (DB, Key Vault, Quartz, disk space) | Both hosts |
| **Configuration** | Key Vault (prod) + User Secrets (dev) via .NET Configuration | Both hosts |

### 2.10 Architecture Decision Records

Key architectural decisions and their rationale, for future reference:

| Decision | Chosen | Alternatives Considered | Rationale |
|----------|--------|------------------------|-----------|
| Three deployables | API + Worker + Frontend | Monolith, two deployables | Independent scaling; CPU-bound jobs don't starve API; independent deploy cycles |
| Vertical slices | Feature folders | Layered architecture | Cohesion by feature; easier to navigate; each slice owns its full stack |
| Database as coordination | PostgreSQL polling | RabbitMQ, Azure Service Bus | V1 simplicity; no additional infrastructure; Quartz already polls. Ceiling: ~50–100 jobs/hour, 3–10s pickup latency. See Section 2.7 for throughput limits. |
| EF Core (query only) | DbUp for migrations | EF Core migrations | Raw SQL gives full control over partitioning, triggers, indexes; DbUp is simpler for teams with strong SQL skills |
| Single PostgreSQL instance | One database, all tables | Separate databases per concern | Simpler operations; transactional consistency; partitioning handles scale |
| Quartz.NET for scheduling | AdoJobStore | Hangfire, custom timer | Mature, persistent, cron support, clustered failover, battle-tested |
| BouncyCastle for PGP | FIPS-approved algorithms only | GnuPG CLI, custom PGP | Only .NET library with full PGP format support; FIPS algorithms enforced in config |
| Azure Key Vault for KEK | Envelope encryption | Local key file, AWS KMS | Azure-native; FIPS 140-2 Level 2/3; hardware-backed; no key material on disk |
| System.Text.Json primary | Newtonsoft for Quartz only | Newtonsoft everywhere | Performance; .NET native; smaller dependency surface; Quartz requires Newtonsoft |
| No inter-process messaging (V1) | Database polling + `FOR UPDATE SKIP LOCKED` | RabbitMQ, gRPC, SignalR | V1 simplicity; poll jitter and DB load are acceptable at target throughput. V2 migrates to event-driven scheduling via outbox + message bus (Section 15). |

### 2.11 Non-Functional Requirements & Design Targets

Without explicit targets, claims like "polling is fine" and "partition monthly" cannot be evaluated. These are the design-point assumptions for V1. They are not SLAs — they are the workload profile the architecture was designed to support. Exceeding them requires the V2 changes documented in Section 15.

**Throughput & capacity**:

| Metric | V1 Design Target | Notes |
|--------|-----------------|-------|
| Max file size (per step) | 10 GB | Streaming architecture; memory bounded to ~2× buffer size (default 80KB). Tested to 10 GB; larger files should work but are not validated. |
| Concurrent job executions | 5 (configurable up to 20) | Global semaphore, not per-job. Bounded by Worker CPU/memory and IOPS. |
| Job throughput | ~50–100 jobs/hour | Depends on avg job duration. Bottleneck is concurrency limit × job runtime. |
| File Monitor throughput | ~200 files/min (local), ~30 files/min (remote) | Local: limited by FileSystemWatcher + stability window. Remote: limited by poll interval + connection overhead. |
| Concurrent active monitors | 50 | Beyond this, poll scheduling contention and database load from directory state become measurable. |
| Concurrent transfers per connection | 1 | SSH.NET and FluentFTP connections are not shared across jobs. Each job opens its own connection. |
| PGP/SSH keys stored | ~500 | No hard limit; performance degrades on key list queries if tags are heavily used and unindexed. |
| Audit log write rate | ~10–50 entries/second sustained | Partitioned by month; insert performance is stable. Querying across partitions degrades beyond 12 months of retained data. |

**Latency**:

| Metric | V1 Design Target | Notes |
|--------|-----------------|-------|
| Job pickup latency (queued → running) | 3–10 seconds (p95) | Sum of Quartz poll interval + queue dequeue poll. |
| API response time (p95) | < 200ms | For CRUD operations. Excludes connection test (network-bound) and key generation (CPU-bound). |
| File Monitor detection (local, new file) | < 10 seconds | Watcher provides ~instant detection; stability window adds 5s before trigger. |
| File Monitor detection (remote, new file) | 1–2× poll interval | Depends on configured interval (min 30s, recommended 60s+). |
| Key Vault wrap/unwrap latency | ~20ms per operation | Azure Key Vault REST call. Adds to every encrypt/decrypt operation. |

**Retention & storage**:

| Metric | V1 Design Target | Notes |
|--------|-----------------|-------|
| Audit log retention | 12 months online | Monthly partitions. Older partitions archived to Azure Blob (cold storage). |
| Job execution history | 12 months online | Same partitioning strategy as audit log. |
| Temp directory retention (orphaned) | 7 days | Background cleanup service purges. |
| Database size (1 year, typical) | ~10–50 GB | Depends heavily on audit log volume and JSONB column sizes. |

**Availability & recovery**:

| Metric | V1 Design Target | Notes |
|--------|-----------------|-------|
| RPO (Recovery Point Objective) | < 1 hour | Azure Database for PostgreSQL continuous backup with PITR. RPO depends on WAL archival frequency. |
| RTO (Recovery Time Objective) | < 30 minutes | Container restart + migration check + Quartz re-acquisition. Does not include database restore time if the DB itself is lost. |
| Planned downtime tolerance | < 5 minutes | Rolling deployment: API hosts can be cycled independently. Worker requires brief stop for Quartz trigger handoff. |
| Unplanned Worker crash | Jobs in `Running` state are marked `Failed` on next startup. Queued jobs are re-picked up automatically. | No automatic failover to a second Worker in V1. |

**What these targets do NOT cover** (V2):

- Multi-region or active-active deployment
- Sub-second job pickup latency (requires event-driven scheduling)
- Horizontal Worker scaling (requires Quartz cluster mode + event bus)
- Zero-downtime database migrations
- Formal SLA commitments with contractual penalties

---

## 3. Tech Stack & Key Libraries

This section catalogs every technology choice in Courier, organized by layer. Each entry includes the specific package, its version constraint, what it's used for, and which section(s) it supports.

### 3.1 Runtime & Frameworks

| Technology | Version | Purpose | Section |
|-----------|---------|---------|---------|
| **.NET** | 10 | Application runtime, API host, background services | All |
| **ASP.NET Core** | 10 | REST API framework, middleware pipeline, OpenAPI | 10, 12 |
| **Entity Framework Core** | 10 | ORM for PostgreSQL (query/change tracking only — not migrations) | 4, 13 |
| **Npgsql.EntityFrameworkCore.PostgreSQL** | Latest stable | EF Core PostgreSQL provider | 13 |
| **EFCore.NamingConventions** | Latest stable | Automatic snake_case mapping for EF Core | 13 |
| **.NET Aspire** | Latest stable | Service orchestration, health checks, telemetry | 14 |
| **Next.js** | Latest stable (React) | Frontend framework | 11 |
| **PostgreSQL** | 16+ | Primary database | 13 |

### 3.2 Job Scheduling & Workflow

| Package | Purpose | Section |
|---------|---------|---------|
| **Quartz.NET** | Cron scheduling, one-shot scheduling, persistent job store (AdoJobStore) | 5 |
| **Quartz.Serialization.SystemTextJson** | Quartz serialization using System.Text.Json (preferred over Newtonsoft where possible) | 5 |

Quartz.NET is used solely as the scheduling trigger layer. It fires at the scheduled time and delegates to Courier's Job Engine for actual execution. Quartz tables (`QRTZ_*`) are managed via DbUp (Section 13.3.6).

### 3.3 File Transfer

| Package | Purpose | Section |
|---------|---------|---------|
| **SSH.NET** | SFTP client — connections, uploads, downloads, directory operations, host key verification | 6 |
| **FluentFTP** | FTP/FTPS client — connections, uploads, downloads, TLS negotiation, passive mode | 6 |

Both libraries are wrapped behind the `ITransferClient` abstraction (Section 6.2). Application code never references SSH.NET or FluentFTP types directly outside the protocol adapter implementations.

### 3.4 Cryptography & Key Management

| Package | Purpose | Section |
|---------|---------|---------|
| **BouncyCastle (Portable.BouncyCastle)** | PGP operations — encrypt, decrypt, sign, verify, key generation, key import/export, armor encoding, keyring parsing | 7 |
| **System.Security.Cryptography (.NET)** | AES-256-GCM encryption at rest, RSA/ECDSA key generation, SHA-256/384/512 hashing, CSPRNG | 7, 12 |
| **Azure.Security.KeyVault.Keys** | Master key (KEK) management — wrap/unwrap DEKs | 7, 12 |
| **Azure.Security.KeyVault.Secrets** | Application secrets (connection strings, Entra client secret) | 12 |
| **Azure.Identity** | `DefaultAzureCredential` for Key Vault authentication (Managed Identity in prod, Azure CLI in dev) | 12 |

**FIPS note**: When FIPS mode is enabled, internal cryptographic operations use .NET's `System.Security.Cryptography` APIs (backed by CNG on Windows, OpenSSL on Linux) restricted to FIPS-approved algorithms. Whether the underlying module runs in its FIPS-validated mode depends on OS/container configuration (Section 12.10). BouncyCastle (standard edition) is restricted to approved algorithms but is not a FIPS-validated module; a migration path to `bcpg-fips-csharp` exists if validated PGP operations are required.

### 3.5 Compression

| Package | Purpose | Section |
|---------|---------|---------|
| **SharpZipLib** | ZIP (with AES-256 password support), GZIP, TAR, TAR.GZ — compress and decompress | 8 |
| **SharpCompress** | RAR decompression only (RAR creation is proprietary) | 8 |
| **7z CLI (system dependency)** | 7z format — compress and decompress via `Process` wrapper | 8 |

All compression operations use streaming APIs and never load full files into memory. The `ICompressionProvider` abstraction (Section 8.1) wraps each library.

### 3.6 Authentication & Authorization

| Package | Purpose | Section |
|---------|---------|---------|
| **Microsoft.Identity.Web** | Entra ID JWT bearer token validation, OIDC metadata, token claim extraction | 12 |
| **Microsoft.AspNetCore.Authentication.JwtBearer** | ASP.NET JWT authentication middleware | 12 |

Roles (`Admin`, `Operator`, `Viewer`) are defined as Entra ID App Roles and enforced via `[Authorize(Roles = "...")]` attributes (Section 12.2).

### 3.7 API & Validation

| Package | Purpose | Section |
|---------|---------|---------|
| **Swashbuckle.AspNetCore** | OpenAPI/Swagger spec generation, Swagger UI (dev/staging only) | 10 |
| **FluentValidation** | Request body validation with auto-discovery | 10 |
| **FluentValidation.AspNetCore** | ASP.NET Core integration (validation filter) | 10 |

### 3.8 Database & Migrations

| Package | Purpose | Section |
|---------|---------|---------|
| **DbUp** | Database migration runner — numbered SQL scripts, embedded resources, executed on API startup only (Section 13.1) | 13 |
| **DbUp.PostgreSQL** | PostgreSQL provider for DbUp | 13 |
| **Npgsql** | Low-level PostgreSQL driver (used by EF Core and DbUp) | 13 |

Migrations are raw SQL scripts managed via DbUp. EF Core is not used for migrations — it is used only for querying and change tracking against the schema that DbUp creates.

### 3.9 JSON Serialization

| Package | Purpose | Section |
|---------|---------|---------|
| **System.Text.Json** | Primary serializer — API requests/responses, JSONB columns, configuration | All |
| **Newtonsoft.Json** | Required by Quartz.NET AdoJobStore serialization; used nowhere else | 5 |

**Configuration**:

```csharp
builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.PropertyNamingPolicy = JsonNamingPolicy.CamelCase;
    options.SerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull;
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower));
});
```

Enums are serialized as snake_case strings (`"retry_step"`, `"sftp"`) rather than integers, for API readability. JSONB columns in PostgreSQL use the same serializer settings via EF Core's `HasConversion` or Npgsql's `ConfigureJsonOptions`.

### 3.10 Logging & Observability

| Package | Purpose | Section |
|---------|---------|---------|
| **Serilog** | Structured logging framework | 12 |
| **Serilog.Sinks.Console** | Console output (all environments) | — |
| **Serilog.Sinks.Seq** | Seq sink for development — rich local log search and dashboards | — |
| **Serilog.Sinks.ApplicationInsights** | Azure Application Insights sink for staging/production | — |
| **Serilog.Enrichers.Environment** | Machine name, process ID enrichment | — |
| **Serilog.Enrichers.Thread** | Thread ID enrichment | — |
| **Serilog.AspNetCore** | ASP.NET Core request logging integration | — |

**Configuration**:

```csharp
Log.Logger = new LoggerConfiguration()
    .MinimumLevel.Information()
    .MinimumLevel.Override("Microsoft", LogEventLevel.Warning)
    .MinimumLevel.Override("System", LogEventLevel.Warning)
    .MinimumLevel.Override("Quartz", LogEventLevel.Warning)
    .Enrich.FromLogContext()
    .Enrich.WithMachineName()
    .Enrich.WithThreadId()
    .WriteTo.Console(outputTemplate:
        "[{Timestamp:HH:mm:ss} {Level:u3}] {SourceContext} {Message:lj}{NewLine}{Exception}")
    .WriteTo.Conditional(
        _ => builder.Environment.IsDevelopment(),
        config => config.Seq("http://localhost:5341"))
    .WriteTo.Conditional(
        _ => !builder.Environment.IsDevelopment(),
        config => config.ApplicationInsights(
            builder.Configuration["ApplicationInsights:ConnectionString"],
            TelemetryConverter.Traces))
    .CreateLogger();
```

**Sensitive data redaction**: Serilog destructuring policies are configured to redact passwords, passphrases, private key material, and bearer tokens from all log output (Section 12.8).

### 3.11 Testing

| Package | Purpose |
|---------|---------|
| **xUnit** | Test framework — test discovery, execution, parallelization |
| **Shouldly** | Assertion library — fluent, readable assertions with rich failure messages |
| **NSubstitute** | Mocking library — substitute interfaces and verify interactions |
| **Microsoft.AspNetCore.Mvc.Testing** | Integration test host — in-memory API server for endpoint testing |
| **Testcontainers.PostgreSql** | Disposable PostgreSQL containers for database integration tests |
| **Bogus** | Fake data generation for test fixtures |

**Test project structure**:

```
Courier.Tests.Unit/          Unit tests — pure logic, all dependencies mocked
Courier.Tests.Integration/   Integration tests — real database (Testcontainers), real HTTP
Courier.Tests.Architecture/  Architecture tests — enforce dependency rules, naming conventions
```

### 3.12 Infrastructure & Deployment

| Technology | Purpose | Section |
|-----------|---------|---------|
| **Docker** | Containerization — multi-stage builds for API and background services | 14 |
| **Kubernetes** / **Azure Container Apps** | Container orchestration (deployment target) | 14 |
| **.NET Aspire** | Local development orchestration, service discovery, health checks | 14 |
| **Azure Database for PostgreSQL Flexible Server** | Managed PostgreSQL with TDE, automated backups | 13, 14 |
| **Azure Key Vault** | Master key storage (FIPS 140-2 Level 2/3), application secrets | 7, 12 |
| **Azure Application Insights** | Production telemetry, distributed tracing, alerting | — |
| **Azure Blob Storage** | Cold storage for archived partition data | 13 |

### 3.13 Package Version Policy

**Version constraints**:

- **.NET 10 and ASP.NET Core 10**: Pinned to the major version. Minor/patch updates applied via regular dependency updates.
- **EF Core**: Matches the .NET major version (EF Core 10 with .NET 10).
- **All NuGet packages**: Use the latest stable release at project initialization. Pinned to major version with `~>` range to allow patch updates. Breaking changes require an explicit upgrade decision.
- **PostgreSQL**: Minimum version 16 for native partitioning performance improvements and JSONB enhancements.

**Dependency audit**: `dotnet list package --vulnerable` is run as part of the CI pipeline. Packages with known CVEs are flagged as build warnings (high severity) or build failures (critical severity).

### 3.14 Library Decision Summary

```
┌──────────────────┬──────────────────────────────────────────────┐
│ Concern           │ Decision                                     │
├──────────────────┼──────────────────────────────────────────────┤
│ Runtime           │ .NET 10                                      │
│ Frontend          │ Next.js (React)                              │
│ Database          │ PostgreSQL 16+                               │
│ ORM               │ EF Core 10 (query only, not migrations)     │
│ Migrations        │ DbUp (raw SQL scripts)                      │
│ Scheduling        │ Quartz.NET (AdoJobStore)                    │
│ SFTP              │ SSH.NET                                      │
│ FTP/FTPS          │ FluentFTP                                    │
│ PGP               │ BouncyCastle (FIPS-approved algorithms only) │
│ Encryption        │ .NET System.Security.Cryptography (CNG/OSSL) │
│ Key management    │ Azure Key Vault                              │
│ Compression       │ SharpZipLib + SharpCompress + 7z CLI         │
│ Auth              │ Entra ID via Microsoft.Identity.Web          │
│ API docs          │ Swashbuckle (OpenAPI/Swagger)                │
│ Validation        │ FluentValidation                             │
│ JSON (primary)    │ System.Text.Json                             │
│ JSON (Quartz)     │ Newtonsoft.Json                              │
│ Logging           │ Serilog → Seq (dev) / App Insights (prod)   │
│ Testing           │ xUnit + Shouldly + NSubstitute               │
│ Orchestration     │ .NET Aspire (dev), Docker + K8s (prod)      │
└──────────────────┴──────────────────────────────────────────────┘
```

---

## 4. Domain Model

This section defines all entities in the Courier system, their relationships, and the conventions used across the codebase. The domain model serves as the single source of truth for the data structures that underpin every subsystem.

### 4.1 Design Conventions

**Aggregate roots** are implemented as C# `class` types. These are mutable entities tracked by EF Core's change tracker and represent the top-level objects that own their child entities. Examples: `Job`, `Connection`, `PgpKey`, `FileMonitor`.

**Value objects** are implemented as C# `record` types. These are immutable, compared by value, and typically serialized as JSON columns or embedded within aggregate roots. Examples: `StepConfiguration`, `FailurePolicy`, `TransferProgress`, `VerifyResult`.

**General conventions:**

- All entities use `Guid` primary keys, generated application-side via `Guid.NewGuid()`
- All entities include `CreatedAt` and `UpdatedAt` timestamps, set automatically via EF Core interceptors
- All major entities support soft delete via an `IsDeleted` flag and `DeletedAt` timestamp. Soft-deleted entities are excluded from normal queries via a global query filter
- Nullable reference types are enabled project-wide. Non-nullable properties are required; nullable properties are optional
- Navigation properties use `IReadOnlyList<T>` for collections to enforce modification through aggregate root methods
- JSON columns (PostgreSQL `JSONB`) are used for flexible, schema-light data like step configuration and audit details

### 4.2 Entity Catalog

#### 4.2.1 Job System Entities

**Job** (aggregate root — `class`)

The central entity representing a named, versioned pipeline of steps.

| Property            | Type                | Description                                           |
|---------------------|---------------------|-------------------------------------------------------|
| `Id`                | `Guid`              | Primary key                                           |
| `Name`              | `string`            | Human-readable job name                               |
| `Description`       | `string?`           | Optional description                                  |
| `CurrentVersion`    | `int`               | Latest version number                                 |
| `FailurePolicy`     | `FailurePolicy`     | Value object: policy, max retries, backoff config     |
| `IsEnabled`         | `bool`              | Whether the job can be scheduled/triggered            |
| `CreatedAt`         | `DateTimeOffset`    | Creation timestamp                                    |
| `UpdatedAt`         | `DateTimeOffset`    | Last modification timestamp                           |
| `IsDeleted`         | `bool`              | Soft delete flag                                      |
| `DeletedAt`         | `DateTimeOffset?`   | Soft delete timestamp                                 |
| `Steps`             | `IReadOnlyList<JobStep>` | Ordered list of step definitions                 |
| `Schedules`         | `IReadOnlyList<JobSchedule>` | Attached schedules                          |
| `Versions`          | `IReadOnlyList<JobVersion>` | Historical configuration snapshots            |
| `Executions`        | `IReadOnlyList<JobExecution>` | Execution history                           |
| `Dependencies`      | `IReadOnlyList<JobDependency>` | Upstream dependencies                      |
| `Tags`              | `IReadOnlyList<EntityTag>` | Associated tags                               |

**JobStep** (entity owned by Job)

| Property            | Type                | Description                                           |
|---------------------|---------------------|-------------------------------------------------------|
| `Id`                | `Guid`              | Primary key                                           |
| `JobId`             | `Guid`              | FK to parent Job                                      |
| `StepOrder`         | `int`               | Execution order (0-based)                             |
| `Name`              | `string`            | Human-readable step name                              |
| `TypeKey`           | `string`            | Step type identifier (e.g., `sftp.download`)          |
| `Configuration`     | `StepConfiguration` | Value object serialized as JSONB                      |
| `TimeoutSeconds`    | `int`               | Step timeout (default: 300)                           |

**JobVersion** (entity owned by Job)

| Property            | Type                | Description                                           |
|---------------------|---------------------|-------------------------------------------------------|
| `Id`                | `Guid`              | Primary key                                           |
| `JobId`             | `Guid`              | FK to parent Job                                      |
| `VersionNumber`     | `int`               | Incrementing version                                  |
| `ConfigSnapshot`    | `string`            | Full job configuration as JSON                        |
| `CreatedAt`         | `DateTimeOffset`    | When this version was created                         |
| `CreatedBy`         | `string`            | User who made the change                              |

**JobExecution** (aggregate root — `class`)

A single run of a job. Owns its step executions and context snapshot.

| Property            | Type                | Description                                           |
|---------------------|---------------------|-------------------------------------------------------|
| `Id`                | `Guid`              | Primary key                                           |
| `JobId`             | `Guid`              | FK to Job                                             |
| `JobVersionNumber`  | `int`               | Which version was executed                            |
| `ChainExecutionId`  | `Guid?`             | FK to ChainExecution if part of a chain               |
| `TriggeredBy`       | `string`            | "schedule", "manual:{userId}", "monitor:{monitorId}"  |
| `State`             | `JobExecutionState` | Enum: Created, Queued, Running, Paused, Completed, Failed, Cancelled |
| `QueuedAt`          | `DateTimeOffset?`   | When the execution entered the queue                  |
| `StartedAt`         | `DateTimeOffset?`   | When execution began                                  |
| `CompletedAt`       | `DateTimeOffset?`   | When execution finished                               |
| `ContextSnapshot`   | `string`            | Serialized JobContext as JSON for checkpoint/resume    |
| `StepExecutions`    | `IReadOnlyList<StepExecution>` | Per-step execution records                 |

**StepExecution** (entity owned by JobExecution)

| Property            | Type                | Description                                           |
|---------------------|---------------------|-------------------------------------------------------|
| `Id`                | `Guid`              | Primary key                                           |
| `JobExecutionId`    | `Guid`              | FK to parent JobExecution                             |
| `JobStepId`         | `Guid`              | FK to the JobStep definition                          |
| `StepOrder`         | `int`               | Execution order                                       |
| `State`             | `StepExecutionState`| Enum: Pending, Running, Completed, Failed, Skipped    |
| `StartedAt`         | `DateTimeOffset?`   | When step began                                       |
| `CompletedAt`       | `DateTimeOffset?`   | When step finished                                    |
| `DurationMs`        | `long?`             | Execution duration in milliseconds                    |
| `BytesProcessed`    | `long?`             | Total bytes transferred/processed                     |
| `OutputData`        | `string?`           | Step output written to JobContext, as JSON             |
| `ErrorMessage`      | `string?`           | Error message if failed                               |
| `ErrorStackTrace`   | `string?`           | Stack trace if failed                                 |
| `RetryAttempt`      | `int`               | Current retry attempt number (0 = first try)          |

**JobChain** (aggregate root — `class`)

| Property            | Type                | Description                                           |
|---------------------|---------------------|-------------------------------------------------------|
| `Id`                | `Guid`              | Primary key                                           |
| `Name`              | `string`            | Human-readable chain name                             |
| `Description`       | `string?`           | Optional description                                  |
| `IsEnabled`         | `bool`              | Whether the chain can be scheduled/triggered          |
| `CreatedAt`         | `DateTimeOffset`    | Creation timestamp                                    |
| `UpdatedAt`         | `DateTimeOffset`    | Last modification timestamp                           |
| `IsDeleted`         | `bool`              | Soft delete flag                                      |
| `DeletedAt`         | `DateTimeOffset?`   | Soft delete timestamp                                 |
| `Members`           | `IReadOnlyList<JobChainMember>` | Ordered job references with dependencies   |
| `Schedules`         | `IReadOnlyList<ChainSchedule>` | Attached schedules                        |
| `Tags`              | `IReadOnlyList<EntityTag>` | Associated tags                               |

**JobChainMember** (entity owned by JobChain)

| Property            | Type                | Description                                           |
|---------------------|---------------------|-------------------------------------------------------|
| `Id`                | `Guid`              | Primary key                                           |
| `ChainId`           | `Guid`              | FK to parent JobChain                                 |
| `JobId`             | `Guid`              | FK to the Job                                         |
| `ExecutionOrder`    | `int`               | Order within the chain                                |
| `DependsOnMemberId` | `Guid?`             | FK to upstream JobChainMember (null = chain entry point)|
| `RunOnUpstreamFailure` | `bool`           | Whether to run if upstream member fails (default: false)|

**ChainExecution** (aggregate root — `class`)

| Property            | Type                | Description                                           |
|---------------------|---------------------|-------------------------------------------------------|
| `Id`                | `Guid`              | Primary key                                           |
| `ChainId`           | `Guid`              | FK to JobChain                                        |
| `TriggeredBy`       | `string`            | "schedule", "manual:{userId}"                         |
| `State`             | `ChainExecutionState`| Enum: Pending, Running, Completed, Failed, Paused, Cancelled |
| `StartedAt`         | `DateTimeOffset?`   | When chain execution began                            |
| `CompletedAt`       | `DateTimeOffset?`   | When chain execution finished                         |
| `JobExecutions`     | `IReadOnlyList<JobExecution>` | All job executions within this chain run    |

**JobDependency** (entity)

Represents a dependency edge between two standalone jobs (outside of chains).

| Property            | Type                | Description                                           |
|---------------------|---------------------|-------------------------------------------------------|
| `Id`                | `Guid`              | Primary key                                           |
| `UpstreamJobId`     | `Guid`              | FK to the job that must complete first                |
| `DownstreamJobId`   | `Guid`              | FK to the job that depends on the upstream            |
| `RunOnFailure`      | `bool`              | Allow downstream to run even if upstream fails        |

**JobSchedule** (entity)

Attached to a Job via `job_schedules` table.

| Property            | Type                | Description                                           |
|---------------------|---------------------|-------------------------------------------------------|
| `Id`                | `Guid`              | Primary key                                           |
| `JobId`             | `Guid`              | FK to Job                                             |
| `ScheduleType`      | `string`            | `"cron"` or `"one_shot"`                              |
| `CronExpression`    | `string?`           | Quartz cron expression (nullable for one-shot)        |
| `RunAt`             | `DateTimeOffset?`   | One-shot execution time (nullable for cron)           |
| `IsEnabled`         | `bool`              | Whether the schedule is active                        |
| `LastFiredAt`       | `DateTimeOffset?`   | Last time this schedule triggered                     |
| `NextFireAt`        | `DateTimeOffset?`   | Next calculated fire time                             |

**ChainSchedule** (entity)

Attached to a JobChain via `chain_schedules` table. Mirrors `JobSchedule` exactly.

| Property            | Type                | Description                                           |
|---------------------|---------------------|-------------------------------------------------------|
| `Id`                | `Guid`              | Primary key                                           |
| `ChainId`           | `Guid`              | FK to JobChain                                        |
| `ScheduleType`      | `string`            | `"cron"` or `"one_shot"`                              |
| `CronExpression`    | `string?`           | Quartz cron expression (nullable for one-shot)        |
| `RunAt`             | `DateTimeOffset?`   | One-shot execution time (nullable for cron)           |
| `IsEnabled`         | `bool`              | Whether the schedule is active                        |
| `LastFiredAt`       | `DateTimeOffset?`   | Last time this schedule triggered                     |
| `NextFireAt`        | `DateTimeOffset?`   | Next calculated fire time                             |

#### 4.2.2 Connection Entities

**Connection** (aggregate root — `class`)

| Property            | Type                | Description                                           |
|---------------------|---------------------|-------------------------------------------------------|
| `Id`                | `Guid`              | Primary key                                           |
| `Name`              | `string`            | Human-readable name                                   |
| `Group`             | `string?`           | Organizational folder/group                           |
| `Protocol`          | `ConnectionProtocol`| Enum: `SFTP`, `FTP`, `FTPS`                          |
| `Host`              | `string`            | Hostname or IP                                        |
| `Port`              | `int`               | Port number                                           |
| `AuthMethod`        | `AuthMethod`        | Enum: `Password`, `SshKey`, `PasswordAndSshKey`       |
| `Username`          | `string`            | Login username                                        |
| `PasswordEncrypted` | `byte[]?`           | AES-256 encrypted password                            |
| `SshKeyId`          | `Guid?`             | FK to SshKey                                          |
| `HostKeyPolicy`     | `HostKeyPolicy`     | Enum: `TrustOnFirstUse`, `AlwaysTrust`, `Manual`      |
| `StoredHostFingerprint` | `string?`       | Known host key fingerprint                            |
| `PassiveMode`       | `bool`              | FTP/FTPS: passive mode (default: true)                |
| `TlsVersionFloor`   | `TlsVersion?`      | FTPS: minimum TLS version                             |
| `TlsCertPolicy`     | `TlsCertPolicy`     | FTPS: cert validation mode (`SystemTrust`, `PinnedThumbprint`, `Insecure`) |
| `TlsPinnedThumbprint` | `string?`         | FTPS: expected SHA-256 cert thumbprint (when policy is `PinnedThumbprint`) |
| `SshAlgorithms`     | `SshAlgorithmConfig?`| Value object serialized as JSONB                     |
| `ConnectTimeoutSec` | `int`               | Connection timeout (default: 30)                      |
| `OperationTimeoutSec`| `int`              | Per-operation timeout (default: 300)                  |
| `KeepaliveIntervalSec`| `int`             | Keepalive interval (default: 60)                      |
| `TransportRetries`  | `int`               | Auto-reconnect attempts (default: 2)                  |
| `Status`            | `ConnectionStatus`  | Enum: `Active`, `Disabled`                            |
| `FipsOverride`      | `bool`              | Allow non-FIPS algorithms for this connection         |
| `Notes`             | `string?`           | Free-text notes                                       |
| `CreatedAt`         | `DateTimeOffset`    |                                                       |
| `UpdatedAt`         | `DateTimeOffset`    |                                                       |
| `IsDeleted`         | `bool`              |                                                       |
| `DeletedAt`         | `DateTimeOffset?`   |                                                       |
| `Tags`              | `IReadOnlyList<EntityTag>` | Associated tags                               |

**KnownHost** (entity owned by Connection)

| Property            | Type                | Description                                           |
|---------------------|---------------------|-------------------------------------------------------|
| `Id`                | `Guid`              | Primary key                                           |
| `ConnectionId`      | `Guid`              | FK to Connection                                      |
| `Fingerprint`       | `string`            | SHA-256 fingerprint                                   |
| `KeyType`           | `string`            | Algorithm (e.g., `ssh-rsa`, `ssh-ed25519`)            |
| `FirstSeen`         | `DateTimeOffset`    | When first recorded                                   |
| `LastSeen`          | `DateTimeOffset`    | Last successful connection                            |
| `ApprovedBy`        | `string`            | User or "system" for TOFU                             |

#### 4.2.3 Key Store Entities

**PgpKey** (aggregate root — `class`)

| Property            | Type                | Description                                           |
|---------------------|---------------------|-------------------------------------------------------|
| `Id`                | `Guid`              | Primary key                                           |
| `Name`              | `string`            | Human-readable label                                  |
| `Fingerprint`       | `string`            | Full PGP fingerprint (40 hex chars)                   |
| `ShortKeyId`        | `string`            | Short key ID (16 hex chars)                           |
| `Algorithm`         | `PgpAlgorithm`      | Enum: `RSA_2048`, `RSA_3072`, `RSA_4096`, `ECC_CURVE25519`, etc. |
| `KeyType`           | `PgpKeyType`        | Enum: `PublicOnly`, `KeyPair`                         |
| `Purpose`           | `string?`           | Free-text notes                                       |
| `Status`            | `PgpKeyStatus`      | Enum: `Active`, `Expiring`, `Retired`, `Revoked`, `Deleted` |
| `PublicKeyData`     | `string`            | ASCII-armored public key                              |
| `PrivateKeyData`    | `byte[]?`           | AES-256 encrypted private key (null for public-only)  |
| `PassphraseHash`    | `string?`           | Encrypted passphrase                                  |
| `ExpiresAt`         | `DateTimeOffset?`   | Key expiration date                                   |
| `SuccessorKeyId`    | `Guid?`             | FK to replacement key (for rotation)                  |
| `CreatedBy`         | `string`            | User who generated/imported                           |
| `CreatedAt`         | `DateTimeOffset`    |                                                       |
| `UpdatedAt`         | `DateTimeOffset`    |                                                       |
| `IsDeleted`         | `bool`              |                                                       |
| `DeletedAt`         | `DateTimeOffset?`   |                                                       |
| `Tags`              | `IReadOnlyList<EntityTag>` | Associated tags                               |

**SshKey** (aggregate root — `class`)

| Property            | Type                | Description                                           |
|---------------------|---------------------|-------------------------------------------------------|
| `Id`                | `Guid`              | Primary key                                           |
| `Name`              | `string`            | Human-readable label                                  |
| `KeyType`           | `SshKeyType`        | Enum: `RSA_2048`, `RSA_4096`, `ED25519`, `ECDSA_256`  |
| `PublicKeyData`     | `string`            | OpenSSH-format public key                             |
| `PrivateKeyData`    | `byte[]`            | AES-256 encrypted private key                         |
| `PassphraseHash`    | `string?`           | Encrypted passphrase (nullable)                       |
| `Fingerprint`       | `string`            | SHA-256 fingerprint                                   |
| `Status`            | `SshKeyStatus`      | Enum: `Active`, `Retired`, `Deleted`                  |
| `Notes`             | `string?`           | Free-text notes                                       |
| `CreatedBy`         | `string`            | User who generated/imported                           |
| `CreatedAt`         | `DateTimeOffset`    |                                                       |
| `UpdatedAt`         | `DateTimeOffset`    |                                                       |
| `IsDeleted`         | `bool`              |                                                       |
| `DeletedAt`         | `DateTimeOffset?`   |                                                       |
| `Tags`              | `IReadOnlyList<EntityTag>` | Associated tags                               |

#### 4.2.4 File Monitor Entities

**FileMonitor** (aggregate root — `class`)

| Property            | Type                | Description                                           |
|---------------------|---------------------|-------------------------------------------------------|
| `Id`                | `Guid`              | Primary key                                           |
| `Name`              | `string`            | Human-readable name                                   |
| `Description`       | `string?`           | Optional description                                  |
| `WatchTarget`       | `WatchTarget`       | Value object: target type (Local/Remote), path, connection ID |
| `TriggerEvents`     | `TriggerEventFlags` | Flags enum: `FileCreated`, `FileModified`, `FileExists` |
| `FilePatterns`      | `List<string>`      | Glob patterns stored as JSONB array                   |
| `PollingIntervalSec`| `int`               | Polling interval (default: 60)                        |
| `StabilityWindowSec`| `int`               | File readiness window (default: 5)                    |
| `BatchMode`         | `bool`              | True = batch, false = individual (default: true)      |
| `MaxConsecutiveFailures` | `int`          | Error threshold (default: 5)                          |
| `ConsecutiveFailureCount`| `int`          | Current failure counter                               |
| `State`             | `MonitorState`      | Enum: `Active`, `Paused`, `Disabled`, `Error`         |
| `CreatedAt`         | `DateTimeOffset`    |                                                       |
| `UpdatedAt`         | `DateTimeOffset`    |                                                       |
| `IsDeleted`         | `bool`              |                                                       |
| `DeletedAt`         | `DateTimeOffset?`   |                                                       |
| `BoundJobs`         | `IReadOnlyList<MonitorJobBinding>` | Linked jobs/chains                  |
| `Tags`              | `IReadOnlyList<EntityTag>` | Associated tags                               |

**MonitorJobBinding** (entity owned by FileMonitor)

| Property            | Type                | Description                                           |
|---------------------|---------------------|-------------------------------------------------------|
| `Id`                | `Guid`              | Primary key                                           |
| `MonitorId`         | `Guid`              | FK to FileMonitor                                     |
| `JobId`             | `Guid?`             | FK to Job (nullable — set if bound to a job)          |
| `ChainId`           | `Guid?`             | FK to JobChain (nullable — set if bound to a chain)   |

**MonitorDirectoryState** (entity owned by FileMonitor)

| Property            | Type                | Description                                           |
|---------------------|---------------------|-------------------------------------------------------|
| `Id`                | `Guid`              | Primary key                                           |
| `MonitorId`         | `Guid`              | FK to FileMonitor                                     |
| `DirectoryListing`  | `string`            | JSON array of {path, size, lastModified}              |
| `CapturedAt`        | `DateTimeOffset`    | When this snapshot was taken                          |

**MonitorFileLog** (entity owned by FileMonitor)

| Property            | Type                | Description                                           |
|---------------------|---------------------|-------------------------------------------------------|
| `Id`                | `Guid`              | Primary key                                           |
| `MonitorId`         | `Guid`              | FK to FileMonitor                                     |
| `FilePath`          | `string`            | Full path of detected file                            |
| `FileSize`          | `long`              | Size in bytes at trigger time                         |
| `FileHash`          | `string?`           | Optional SHA-256 hash                                 |
| `LastModified`      | `DateTimeOffset`    | File's last modified timestamp                        |
| `TriggeredAt`       | `DateTimeOffset`    | When the trigger fired                                |
| `ExecutionId`       | `Guid`              | FK to the JobExecution created                        |

#### 4.2.5 Cross-Cutting Entities

**Tag** (aggregate root — `class`)

| Property            | Type                | Description                                           |
|---------------------|---------------------|-------------------------------------------------------|
| `Id`                | `Guid`              | Primary key                                           |
| `Name`              | `string`            | Tag label (unique, case-insensitive)                  |
| `Color`             | `string?`           | Hex color code for UI display (e.g., `#FF5733`)       |
| `Category`          | `string?`           | Grouping category (e.g., "Partner", "Environment")    |
| `Description`       | `string?`           | Optional description                                  |
| `CreatedAt`         | `DateTimeOffset`    |                                                       |
| `IsDeleted`         | `bool`              |                                                       |
| `DeletedAt`         | `DateTimeOffset?`   |                                                       |

**EntityTag** (join entity)

Polymorphic association linking any taggable entity to a tag.

| Property            | Type                | Description                                           |
|---------------------|---------------------|-------------------------------------------------------|
| `Id`                | `Guid`              | Primary key                                           |
| `TagId`             | `Guid`              | FK to Tag                                             |
| `EntityType`        | `TaggableEntityType`| Enum: `Job`, `JobChain`, `Connection`, `PgpKey`, `SshKey`, `FileMonitor` |
| `EntityId`          | `Guid`              | FK to the tagged entity (not a database FK — resolved in application) |

**AuditLogEntry** (entity — append-only)

Unified audit log with an entity type discriminator.

| Property            | Type                | Description                                           |
|---------------------|---------------------|-------------------------------------------------------|
| `Id`                | `Guid`              | Primary key                                           |
| `EntityType`        | `AuditableEntityType` | Enum: `Job`, `JobExecution`, `StepExecution`, `Chain`, `Connection`, `PgpKey`, `SshKey`, `FileMonitor` |
| `EntityId`          | `Guid`              | FK to the audited entity                              |
| `Operation`         | `string`            | Operation name (e.g., `StateChanged`, `UsedForEncrypt`, `Connected`) |
| `PerformedBy`       | `string`            | User ID or "system"                                   |
| `PerformedAt`       | `DateTimeOffset`    | Timestamp                                             |
| `Details`           | `string`            | JSONB: operation-specific context (old/new state, error details, bytes transferred, etc.) |

The audit log uses a single table with a JSONB `Details` column for flexibility. Subsystem-specific fields (bytes transferred, transfer rate, error stack traces) are stored in the Details JSON rather than as top-level columns. This avoids a wide, sparse table while keeping all audit data queryable via PostgreSQL's JSONB operators.

**Indexes**: `(EntityType, EntityId, PerformedAt)` for entity-specific history queries, `(PerformedAt)` for time-range queries, `(PerformedBy, PerformedAt)` for user activity queries.

**DomainEvent** (entity — append-only)

Persisted domain events for V2 notification subscribers. Separate from the audit log because events are actionable (consumed by handlers) while audit entries are historical records.

| Property            | Type                | Description                                           |
|---------------------|---------------------|-------------------------------------------------------|
| `Id`                | `Guid`              | Primary key                                           |
| `EventType`         | `string`            | Event name (e.g., `JobCompleted`, `KeyExpiringSoon`)  |
| `EntityType`        | `string`            | Source entity type                                    |
| `EntityId`          | `Guid`              | Source entity ID                                      |
| `Payload`           | `string`            | JSONB: event-specific data                            |
| `OccurredAt`        | `DateTimeOffset`    | When the event occurred                               |
| `ProcessedAt`       | `DateTimeOffset?`   | When a subscriber consumed the event (null = pending) |
| `ProcessedBy`       | `string?`           | Which subscriber processed it                         |

In V1, events are written but `ProcessedAt` remains null (no subscribers yet). This table is designed as a **transactional outbox**: events are written in the same database transaction as the state change that produced them, guaranteeing consistency. In V2, this outbox becomes the foundation for event-driven scheduling — a relay process reads unprocessed events (using `FOR UPDATE SKIP LOCKED` to prevent duplicate delivery), publishes them to a message bus (Azure Service Bus or RabbitMQ), and marks them processed. This replaces database polling for job coordination and enables at-least-once delivery, fan-out notifications, and horizontal Worker scaling (see Section 15).

**SystemSetting** (entity)

Key-value configuration for runtime-adjustable settings.

| Property            | Type                | Description                                           |
|---------------------|---------------------|-------------------------------------------------------|
| `Key`               | `string`            | Primary key (e.g., `job.concurrency_limit`)           |
| `Value`             | `string`            | Setting value (parsed by application)                 |
| `Description`       | `string?`           | Human-readable description                            |
| `UpdatedAt`         | `DateTimeOffset`    | Last modification timestamp                           |
| `UpdatedBy`         | `string`            | User who changed the setting                          |

### 4.3 Value Objects

Value objects are implemented as C# `record` types. They are immutable, compared by value, and stored either as JSONB columns or as embedded properties within their parent entity.

```csharp
public record FailurePolicy(
    FailurePolicyType Type,       // Stop, RetryStep, RetryJob, SkipAndContinue
    int MaxRetries = 3,
    int BackoffBaseSeconds = 1,
    int BackoffMaxSeconds = 60);

public record StepConfiguration(
    Dictionary<string, object> Parameters);  // Flexible key-value config per step type

public record WatchTarget(
    WatchTargetType Type,         // Local, Remote
    string Path,                  // Directory path
    Guid? ConnectionId);          // FK to Connection (null for local)

public record SshAlgorithmConfig(
    List<string>? KeyExchange,
    List<string>? Encryption,
    List<string>? Mac,
    List<string>? HostKey);

public record SplitArchiveConfig(
    bool Enabled,
    int MaxPartSizeMb = 500);
```

### 4.4 Enumerations

```csharp
// Job System
public enum JobExecutionState { Created, Queued, Running, Paused, Completed, Failed, Cancelled }
public enum StepExecutionState { Pending, Running, Completed, Failed, Skipped }
public enum ChainExecutionState { Pending, Running, Completed, Failed, Paused, Cancelled }
public enum FailurePolicyType { Stop, RetryStep, RetryJob, SkipAndContinue }
public enum ScheduleType { Cron, OneShot }

// Connections
public enum ConnectionProtocol { SFTP, FTP, FTPS }
public enum AuthMethod { Password, SshKey, PasswordAndSshKey }
public enum HostKeyPolicy { TrustOnFirstUse, AlwaysTrust, Manual }
public enum TlsCertPolicy { SystemTrust, PinnedThumbprint, Insecure }
public enum ConnectionStatus { Active, Disabled }
public enum TlsVersion { TLS_1_0, TLS_1_1, TLS_1_2, TLS_1_3 }

// Keys
public enum PgpAlgorithm { RSA_2048, RSA_3072, RSA_4096, ECC_CURVE25519, ECC_P256, ECC_P384 }
public enum PgpKeyType { PublicOnly, KeyPair }
public enum PgpKeyStatus { Active, Expiring, Retired, Revoked, Deleted }
public enum SshKeyType { RSA_2048, RSA_4096, ED25519, ECDSA_256 }
public enum SshKeyStatus { Active, Retired, Deleted }

// File Monitor
public enum MonitorState { Active, Paused, Disabled, Error }
public enum WatchTargetType { Local, Remote }

[Flags]
public enum TriggerEventFlags
{
    FileCreated = 1,
    FileModified = 2,
    FileExists = 4
}

// Cross-cutting
public enum TaggableEntityType { Job, JobChain, Connection, PgpKey, SshKey, FileMonitor }
public enum AuditableEntityType { Job, JobExecution, StepExecution, Chain, ChainExecution, Connection, PgpKey, SshKey, FileMonitor }
```

### 4.5 Entity Relationship Diagram

```
┌──────────────────────────────────────────────────────────────────────────────────┐
│                              JOB SYSTEM                                          │
│                                                                                  │
│  ┌─────────┐ 1    * ┌──────────┐         ┌──────────────┐                       │
│  │   Job   │───────│ JobStep  │         │ JobSchedule  │                       │
│  └────┬────┘        └──────────┘         └──────────────┘                       │
│       │ 1                                  1 *                                   │
│       ├──────────* ┌──────────────┐                                              │
│       │            │  JobVersion  │         ┌────────────┐  1  * ┌──────────────┐│
│       │            └──────────────┘         │ JobChain   │─────│ChainSchedule ││
│       │ 1                                    └────┬────┘       └──────────────┘│
│       ├──────────* ┌──────────────┐               │ 1                            │
│       │            │ JobExecution │◄──────────┐   ├──────* ┌────────────────┐    │
│       │            └──────┬───────┘           │   │        │ JobChainMember │    │
│       │                   │ 1                  │   │        └────────────────┘    │
│       │                   ├────* ┌─────────────┤   │ 1                            │
│       │                   │     │StepExecution ││   ├──────* ┌────────────────┐    │
│       │                   │     └──────────────┘│   │        │ChainExecution │    │
│       │                   │                      │   │        └────────────────┘    │
│       │ *                 └──────────────────────┘   │                              │
│       ├────────────── ┌────────────────┐              │                             │
│       │               │ JobDependency  │              │                             │
│       │               │ (upstream/     │              │                             │
│       │               │  downstream)   │              │                             │
│       │               └────────────────┘              │                             │
│       │                                               │                             │
│  ─ ─ ─│─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─│─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─  │
│       │              CONNECTIONS                      │                             │
│       │                                               │                             │
│       │         ┌────────────┐ 1    * ┌───────────┐  │                             │
│       │         │ Connection │───────│ KnownHost │  │                             │
│       │         └──────┬─────┘        └───────────┘  │                             │
│       │                │                              │                             │
│       │                │ *..1                          │                             │
│       │                ▼                               │                             │
│       │         ┌────────────┐                        │                             │
│       │         │   SshKey   │                        │                             │
│       │         └────────────┘                        │                             │
│       │                                               │                             │
│  ─ ─ ─│─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─│─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─  │
│       │              KEY STORE                        │                             │
│       │                                               │                             │
│       │         ┌────────────┐                        │                             │
│       │         │   PgpKey   │                        │                             │
│       │         │            │───── successor_key_id  │                             │
│       │         └────────────┘      (self-ref)        │                             │
│       │                                               │                             │
│  ─ ─ ─│─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─│─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─  │
│       │            FILE MONITORS                      │                             │
│       │                                               │                             │
│       │  ┌─────────────┐ 1  * ┌────────────────────┐ │                             │
│       ├─│ FileMonitor  │────│ MonitorJobBinding   │─┘                             │
│       │  └──────┬──────┘     └────────────────────┘                               │
│       │         │ 1                                                                │
│       │         ├──────* ┌───────────────────────┐                                │
│       │         │        │ MonitorDirectoryState  │                                │
│       │         │        └───────────────────────┘                                │
│       │         │ 1                                                                │
│       │         └──────* ┌──────────────────┐                                     │
│       │                  │  MonitorFileLog   │                                     │
│       │                  └──────────────────┘                                     │
│       │                                                                            │
│  ─ ─ ─│─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ ─ │
│       │              CROSS-CUTTING                                                 │
│       │                                                                            │
│       │  ┌─────────┐ *    * ┌───────────┐                                         │
│       └─│   Tag    │◄─────│ EntityTag  │──── polymorphic FK to any taggable entity│
│          └─────────┘        └───────────┘                                         │
│                                                                                    │
│          ┌────────────────┐     ┌──────────────┐     ┌───────────────┐            │
│          │ AuditLogEntry  │     │ DomainEvent  │     │ SystemSetting │            │
│          │ (unified,      │     │ (actionable, │     │ (key-value    │            │
│          │  append-only)  │     │  append-only) │     │  config)      │            │
│          └────────────────┘     └──────────────┘     └───────────────┘            │
│                                                                                    │
└──────────────────────────────────────────────────────────────────────────────────┘

KEY RELATIONSHIPS:
  Job ──1:*──► JobStep             Job owns its ordered steps
  Job ──1:*──► JobVersion          Job owns its version history
  Job ──1:*──► JobExecution        Job owns its execution history
  Job ──1:*──► JobSchedule         Job can have multiple schedules
  Job ──*:*──► Job                 Via JobDependency (upstream/downstream)
  JobExecution ──1:*──► StepExecution    Execution owns step executions
  JobChain ──1:*──► JobChainMember      Chain owns ordered member references
  JobChain ──1:*──► ChainExecution      Chain owns chain execution history
  JobChain ──1:*──► ChainSchedule       Chain can have multiple schedules
  ChainExecution ──1:*──► JobExecution   Chain execution owns job executions
  Connection ──1:*──► KnownHost         Connection owns host fingerprints
  Connection ──*:1──► SshKey            Connections reference SSH keys
  FileMonitor ──1:*──► MonitorJobBinding    Monitor binds to jobs/chains
  FileMonitor ──1:*──► MonitorDirectoryState Monitor owns directory snapshots
  FileMonitor ──1:*──► MonitorFileLog       Monitor owns dedup log
  MonitorJobBinding ──*:1──► Job/JobChain   Binding references a job or chain
  PgpKey ──0:1──► PgpKey            Successor key (self-referencing)
  Tag ──*:*──► (any taggable)       Via EntityTag polymorphic join
```

---

## 5. Job Engine Design

The Job Engine is the core runtime of Courier. It is responsible for scheduling, executing, pausing, resuming, and monitoring multi-step file transfer workflows. The engine supports both standalone jobs and ordered execution chains with configurable dependency behavior.

### 5.1 Core Concepts

**Job**: A named, versioned pipeline of one or more Steps that execute sequentially. A Job has configuration (connections, paths, encryption keys), a schedule, a failure policy, and an execution history. When a Job definition is edited, previous runs retain their original configuration — new runs use the updated definition.

**Step**: A single unit of work within a Job. Each Step has a type (e.g., `SftpDownload`, `PgpEncrypt`, `Zip`), its own configuration, a timeout, and independent state tracking. Steps pass data to downstream steps via a shared `JobContext`.

**Job Chain**: A named, ordered group of Jobs that must execute in sequence. Chains define execution order and dependency relationships. A Chain can be scheduled or triggered on demand, and it manages the lifecycle of all its member Jobs as a coordinated unit.

**Job Context**: A key-value dictionary that accumulates outputs as Steps execute. For example, an `SftpDownload` step writes `{ "downloaded_file": "/tmp/courier/abc123/invoice.pgp" }` and the subsequent `PgpDecrypt` step reads that path as its input. The context is persisted to the database alongside step state so that resumed jobs have access to all prior outputs.

### 5.2 Step Type Registry

Step types are implemented as classes that implement the `IJobStep` interface. This provides a plugin-style architecture so new step types can be added without modifying the engine core.

```csharp
public interface IJobStep
{
    string TypeKey { get; }                          // e.g., "sftp.download", "pgp.encrypt"
    Task<StepResult> ExecuteAsync(
        StepConfiguration config,
        JobContext context,
        CancellationToken cancellationToken);
    Task<ValidationResult> ValidateAsync(
        StepConfiguration config);
    Task RollbackAsync(
        StepConfiguration config,
        JobContext context);                          // Best-effort cleanup on failure
}
```

**V1 Step Types:**

| Step Type Key       | Description                                  |
|---------------------|----------------------------------------------|
| `sftp.upload`       | Upload file(s) to an SFTP server             |
| `sftp.download`     | Download file(s) from an SFTP server         |
| `ftp.upload`        | Upload file(s) to an FTP/FTPS server         |
| `ftp.download`      | Download file(s) from an FTP/FTPS server     |
| `pgp.encrypt`       | Encrypt file(s) using a PGP public key       |
| `pgp.decrypt`       | Decrypt file(s) using a PGP private key      |
| `file.zip`          | Compress file(s) into an archive             |
| `file.unzip`        | Extract file(s) from an archive              |
| `file.move`         | Move file(s) to a destination path           |
| `file.copy`         | Copy file(s) to a destination path           |
| `file.delete`       | Delete file(s) from a path                   |
| `azure_function.execute` | Trigger an Azure Function and poll for completion via Application Insights |

Each step type is registered in a `StepTypeRegistry` at startup via dependency injection. The registry resolves the correct `IJobStep` implementation by `TypeKey` at runtime.

### 5.3 Job State Machine

Jobs follow a strict state machine with defined valid transitions:

```
                  ┌──────────┐
                  │ Created  │
                  └────┬─────┘
                       │ (enqueue)
                  ┌────▼─────┐
            ┌─────│  Queued   │
            │     └────┬─────┘
            │          │ (slot available)
            │     ┌────▼─────┐
            │     │ Running  │◄──── (resume)
            │     └──┬──┬──┬─┘
            │        │  │  │
            │        │  │  └──────────┐
            │        │  │             │ (pause)
            │        │  │        ┌────▼─────┐
            │        │  │        │  Paused   │
            │        │  │        └───────────┘
            │        │  │
            │        │  └─────┐ (failure, policy = stop)
            │        │        │
   (cancel) │   ┌────▼────┐ ┌─▼───────┐
            └──►│Cancelled│ │ Failed  │
                └─────────┘ └─────────┘
                       │
                  ┌────▼─────┐
                  │Completed │
                  └──────────┘
```

Valid transitions are enforced in code. Any invalid transition throws an `InvalidJobStateTransitionException`.

### 5.4 Step State Machine

Each step within a job tracks its own state independently:

- **Pending** → Step has not yet started
- **Running** → Step is currently executing
- **Completed** → Step finished successfully (output written to JobContext)
- **Failed** → Step failed (error details persisted)
- **Skipped** → Step was skipped due to failure policy configuration

After each step completes (or fails), its state and any context outputs are persisted to the database in a single transaction. This is the foundation of the checkpoint/resume system.

### 5.5 Checkpoint & Resume

When a Job is paused or fails at Step N, the engine persists:

- The Job's current state (`Paused` or `Failed`)
- Each Step's state (`Completed`, `Failed`, or `Pending`)
- The full `JobContext` dictionary at the point of interruption
- Error details for the failed step (if applicable)

On resume, the engine:

1. Loads the Job and its step states from the database
2. Restores the `JobContext` from the persisted snapshot
3. Identifies the first step in `Pending` or `Failed` state
4. Begins execution from that step, skipping all `Completed` steps

This means a 10-step job that failed at step 7 will resume from step 7 with all outputs from steps 1–6 intact. File outputs from completed steps must still exist on disk — the engine validates this before resuming and fails fast if intermediate files are missing.

### 5.6 Step Context & Data Passing

Steps communicate via the `JobContext`, a typed dictionary scoped to a single job execution:

```csharp
public class JobContext
{
    private readonly Dictionary<string, object> _data = new();

    public void Set<T>(string key, T value);
    public T Get<T>(string key);
    public bool TryGet<T>(string key, out T value);
    public IReadOnlyDictionary<string, object> Snapshot();  // For persistence
}
```

**Convention for keys**: Steps write outputs using the pattern `{stepIndex}.{outputName}`, e.g., `"0.downloaded_files"`, `"1.decrypted_file"`. Step configuration references upstream outputs using the same keys, which the engine resolves at runtime before passing config to the step.

The entire context is serialized to JSON and stored in the `job_executions` table after each step, enabling checkpoint/resume.

### 5.7 Scheduling

Courier uses **Quartz.NET** as its scheduling backbone, with schedule definitions stored in PostgreSQL so they survive application restarts and container re-deployments.

**Cron scheduling**: Jobs and Chains can be assigned a cron expression (e.g., `0 0 3 ? * TUE` for every Tuesday at 3:00 AM). Quartz manages trigger firing and misfire handling.

**One-shot scheduling**: Jobs can be scheduled to run once at a specific future datetime. Implemented as a Quartz `SimpleTrigger` with a repeat count of zero.

**On-demand execution**: An API endpoint (`POST /api/jobs/{id}/trigger`) enqueues a job for immediate execution, bypassing the scheduler. This creates a new `JobExecution` record and places it in the queue.

All three mechanisms feed into the same execution queue, ensuring consistent concurrency management.

### 5.8 Concurrency Management

The engine enforces a configurable maximum of concurrent job executions (default: 5). This is implemented as a persistent semaphore backed by the database, not an in-memory `SemaphoreSlim`, so it works correctly across container restarts.

**Queue dequeue pattern** (runs on a 5-second poll in the Worker host):

```sql
-- Atomic dequeue: claim the next queued execution if a slot is available
WITH running AS (
    SELECT COUNT(*) AS cnt
    FROM job_executions
    WHERE state = 'running'
),
next_job AS (
    SELECT je.id
    FROM job_executions je, running r
    WHERE je.state = 'queued'
      AND r.cnt < (SELECT value::int FROM system_settings WHERE key = 'job.concurrency_limit')
    ORDER BY je.queued_at ASC
    LIMIT 1
    FOR UPDATE SKIP LOCKED   -- prevents duplicate pickup across concurrent polls
)
UPDATE job_executions
SET state = 'running', started_at = now()
FROM next_job
WHERE job_executions.id = next_job.id
RETURNING job_executions.id, job_executions.job_id;
```

`FOR UPDATE SKIP LOCKED` is critical: if two poll cycles overlap (e.g., Quartz fires a trigger while the queue poll is running), the second query skips the row already locked by the first, preventing double-execution. This pattern is used consistently wherever Courier claims "one worker picks it up" — queue dequeue, monitor event processing, and partition maintenance.

**Queue behavior**: When all slots are occupied, new job executions enter `Queued` state and are picked up in FIFO order as slots free up. The queue is polled on a short interval (default: 5 seconds) by a background service.

**Concurrency limit is global**, not per-job. A single job definition can have multiple concurrent executions if the schedule overlaps and slots are available. If this needs to be prevented, a "max instances" setting per job can be added (not in V1 scope, but the schema supports it).

### 5.9 Job Dependencies

Jobs can declare dependencies on other Jobs. A dependency means "do not start this Job until the specified upstream Job's most recent execution has completed."

**Dependency configuration per edge**:

- `required_status`: The upstream job must reach this status. Default: `Completed`. Can be set to `Any` to allow the downstream job to run even if the upstream failed.
- `run_on_failure`: Boolean, default `false`. If `true`, the downstream job starts even when the upstream job fails. This is configured per-dependency, giving fine-grained control.

**Validation**: At job creation/edit time, the system performs a topological sort to detect circular dependencies. If a cycle is detected, the save is rejected with a descriptive error.

### 5.10 Job Chains (Execution Groups)

A Job Chain is a first-class entity that represents an ordered sequence of Jobs:

```
Chain: "Daily Partner Invoice Processing"
  ├── Job 1: Download invoices from Partner SFTP
  ├── Job 2: Decrypt PGP files (depends on Job 1)
  ├── Job 3: Unzip archives (depends on Job 2)
  └── Job 4: Move to processing folder (depends on Job 3)
```

**Chain properties**:

- **Name & description**: Human-readable identification
- **Member jobs**: Ordered list of Job references with dependency edges
- **Schedule**: A Chain can have its own cron/one-shot schedules via the separate `chain_schedules` table. When triggered, it starts the first Job(s) with no upstream dependencies
- **Chain-level state**: `Pending → Running → Completed | Failed | Paused | Cancelled`
- **Propagation behavior**: As each Job completes, the Chain evaluates which downstream Jobs are unblocked and enqueues them

**Chain Scheduling**: Chains support the same scheduling types as Jobs — **cron** (recurring via Quartz cron expressions) and **one_shot** (single future execution that auto-disables after firing). Chain schedules are stored in a separate `chain_schedules` table (not the `job_schedules` table) to maintain clean separation. The Worker runs a `ChainScheduleManager` that registers chain schedules with Quartz.NET under the `"courier-chains"` group (separate from job schedules in the `"courier"` group). The `ScheduleStartupSync` background service syncs both job and chain schedules on a 30-second interval. When a chain schedule fires, `QuartzChainAdapter` calls `ChainExecutionService.TriggerAsync()` with `triggeredBy: "schedule"`.

**Chain vs. standalone dependencies**: Jobs can have dependencies both within a Chain and as standalone relationships. Chains are a convenience for defining and scheduling a related group. Standalone dependencies allow cross-chain relationships.

**Chain failure behavior**: When a Job within a Chain fails and the dependency is configured with `run_on_failure: false`, all downstream Jobs in the chain are marked `Skipped` and the Chain transitions to `Failed`. If `run_on_failure: true`, execution continues.

### 5.11 Failure Handling

Each Job has a configurable **failure policy** that determines what happens when a Step fails:

| Policy               | Behavior                                                    |
|----------------------|-------------------------------------------------------------|
| `stop`               | Mark the Job as `Failed`. No further steps execute.         |
| `retry_step`         | Retry the failed step up to N times with exponential backoff. If all retries fail, mark `Failed`. |
| `retry_job`          | Re-run the entire Job from step 1, up to N times. If all retries fail, mark `Failed`.            |
| `skip_and_continue`  | Mark the failed step as `Skipped`, continue to the next step. |

**Retry configuration**:

- `max_retries`: Maximum number of retry attempts (default: 3)
- `backoff_base_seconds`: Base delay for exponential backoff (default: 1)
- `backoff_max_seconds`: Ceiling for backoff delay (default: 60)
- Backoff formula: `min(backoff_base * 2^attempt, backoff_max)`

The failure policy is set at the Job level. A future enhancement could allow per-step failure policy overrides.

### 5.12 Idempotency Rules by Step Type

Retries without strict idempotency turn into duplicate uploads, partial overwrites, or "successful but wrong" downstream states. Each step type declares its idempotency strategy, detection mechanism, and rollback behavior:

| Step Type | Idempotency Strategy | Detection Key | Rollback on Retry | Notes |
|-----------|---------------------|---------------|-------------------|-------|
| `sftp.upload` | **Overwrite** — re-upload overwrites the remote file | Remote path (full destination path) | Delete partial remote file before re-upload | Uses atomic rename (upload to `.tmp`, rename on completion). If retry finds the `.tmp` file, it deletes and restarts. If it finds the final file from a previous attempt, it overwrites. |
| `sftp.download` | **Resume** — continue from last byte offset | Local file path + size | Delete partial local file and restart if resume fails | SSH.NET supports offset-based reads. If the local file exists with size < remote size, resume. If sizes match, skip (already complete). If local > remote, delete and restart. |
| `ftp.upload` | **Overwrite** — same as SFTP | Remote path | Delete and re-upload | Atomic rename via FTP `RNFR`/`RNTO`. Resume not reliable across FTP servers. |
| `ftp.download` | **Resume** — same as SFTP | Local file path + size | Delete and re-download | FluentFTP `FtpLocalExists.Resume` handles offset. |
| `pgp.encrypt` | **Overwrite** — re-encrypt to same output path | Output file path | Delete output file | PGP encryption is deterministic for the same input + key. Re-encryption produces functionally equivalent output (different session key, same plaintext). |
| `pgp.decrypt` | **Overwrite** — re-decrypt to same output path | Output file path | Delete output file | Deterministic given same input + key. |
| `file.zip` | **Overwrite** — recreate archive | Output archive path | Delete partial archive | Archive creation is atomic (write to `.tmp`, rename). |
| `file.unzip` | **Clean and re-extract** | Output directory | Delete entire output directory and re-extract | Cannot safely resume partial extraction. Directory is wiped and re-extracted from scratch. |
| `file.rename` | **Check-and-skip** | Destination path exists with expected size | No rollback needed | If destination already exists with correct size, skip. If source still exists, perform rename. If neither exists, fail. |
| `file.copy` | **Overwrite** | Destination path | Delete destination | Copy is always safe to repeat. |
| `file.delete` | **Check-and-skip** | File existence | No rollback | If file is already gone, succeed silently. |

**Retry safety contract**: Every step implementation must satisfy this contract:

```csharp
/// <summary>
/// Step implementations must be safe to retry. Specifically:
/// 1. If the step partially completed, retry must not produce duplicates
/// 2. If the step fully completed, retry must detect this and either
///    skip (no-op) or overwrite with an equivalent result
/// 3. The step must clean up its own partial outputs before retrying
/// </summary>
public interface IJobStep
{
    // Called before retry to clean up partial state from the failed attempt.
    // Default implementation: delete all files this step wrote to the temp directory.
    Task CleanupBeforeRetryAsync(StepContext context, CancellationToken ct);

    Task ExecuteAsync(StepContext context, CancellationToken ct);
}
```

**Detection on resume**: When a paused or failed job resumes from a checkpoint, the engine validates the state before continuing:

1. All output files from completed steps must still exist on disk (checked via path + size from the context snapshot)
2. The step being resumed has its `CleanupBeforeRetryAsync()` called to clear any partial state
3. The JobContext is restored from the database snapshot, so downstream steps see the same inputs as the original run

**Upload duplicate prevention**: For `sftp.upload` and `ftp.upload`, the atomic rename pattern is the primary defense. The file is uploaded as `{filename}.courier-tmp`, and only renamed to the final name on successful completion. If a retry finds `{filename}.courier-tmp`, it knows the previous attempt was incomplete and deletes it before restarting. If it finds `{filename}` (no `.courier-tmp`), the previous attempt succeeded and the step can skip or overwrite. Partner systems polling for files never see partial uploads because the `.courier-tmp` suffix doesn't match their expected patterns.

### 5.13 Step Timeouts

Each Step has a configurable `timeout_seconds` (default: 300 — 5 minutes). The timeout is enforced via a `CancellationTokenSource` linked to the step's execution. When the timeout fires:

1. The `CancellationToken` is cancelled
2. The step implementation is expected to observe the token and terminate gracefully
3. If the step does not terminate within a grace period (10 seconds), the engine forcibly marks it as `Failed` with a timeout error
4. The Job's failure policy then determines what happens next

### 5.14 Cancellation Support

A `CancellationToken` is threaded through the entire execution pipeline: Job → Step → underlying library calls. This allows:

- **User-initiated cancellation**: Via `POST /api/jobs/{executionId}/cancel`
- **Timeout-initiated cancellation**: Per-step timeout (see above)
- **Application shutdown**: Aspire host shutdown triggers graceful cancellation of all running jobs, which then persist their checkpoint state

All step implementations are required to observe the cancellation token at regular intervals (e.g., between file chunks during transfer) and throw `OperationCancelledException` when triggered.

### 5.15 Temp File Management

Each Job execution is assigned a unique working directory:

```
/data/courier/temp/{executionId}/
```

Steps write all intermediate files to this directory. The `JobContext` stores relative paths so they resolve correctly. On job completion (success or failure after all retries exhausted), the engine:

1. Moves final output files to their configured destinations
2. Deletes the temp directory
3. Logs total temp disk usage in the execution audit record

For paused or resumable-failed jobs, the temp directory is **retained** until the job is either resumed and completed, or manually cancelled/cleaned up. A background cleanup service purges orphaned temp directories older than a configurable threshold (default: 7 days).

### 5.16 Job Audit Trail

Every state transition at both the Job and Step level is recorded in the `job_audit_log` table:

- **Timestamp** of the transition
- **From state → To state**
- **Duration** of the previous state
- **Bytes transferred** (for file transfer steps)
- **Error details** (message, stack trace, retry attempt number)
- **User** who initiated the action (for manual triggers, cancellations, pauses)

This audit log is append-only and never modified. It serves as the foundation for the V2 metrics dashboard and SLA monitoring system.

### 5.17 Job Versioning

Job definitions are versioned using a simple incrementing version number. When a Job is edited:

1. The current definition is snapshotted as version N
2. The updated definition becomes version N+1
3. Any in-progress or paused executions continue using the version they were started with
4. New executions use the latest version

The `job_definitions` table stores the current version, and `job_definition_versions` stores the full configuration for each historical version as a JSON column. This ensures auditability and prevents mid-execution configuration changes.

### 5.18 Notification Hooks (V2 Preparation)

The engine emits domain events at key lifecycle points:

- `JobStarted`, `JobCompleted`, `JobFailed`, `JobPaused`, `JobResumed`, `JobCancelled`
- `StepStarted`, `StepCompleted`, `StepFailed`, `StepSkipped`
- `ChainStarted`, `ChainCompleted`, `ChainFailed`

In V1, these events are used only for the audit log. In V2, subscribers can be registered to send email notifications (via SMTP), call webhooks (via REST), or trigger other jobs. The event infrastructure is built in V1 so V2 doesn't require engine changes — only new subscribers.

### 5.19 Dry Run Mode (Future Consideration)

Not in V1 scope, but the `IJobStep` interface includes `ValidateAsync` specifically to support a future dry-run mode. A dry run would execute `ValidateAsync` on each step (testing connections, verifying paths, checking key availability) without performing actual transfers. This can be implemented in V2 without modifying the step interface.

### 5.20 Control Flow (If/Else + ForEach)

The job engine supports branching and iteration through four special step types that use the existing flat step model with explicit block markers.

#### 5.20.1 Control Flow Step Types

| Step Type | Purpose | Required Config |
|-----------|---------|-----------------|
| `flow.foreach` | Iterate over a collection | `source` — context reference or JSON array |
| `flow.if` | Conditional branch | `left`, `operator`, `right` (right optional for `exists`) |
| `flow.else` | Alternate branch (must follow `flow.if` body) | none |
| `flow.end` | Closes a `flow.foreach` or `flow.if` block | none |

#### 5.20.2 Block Structure

Control flow steps are regular entries in the flat step list. Blocks are delimited by `flow.foreach`/`flow.if` at the start and `flow.end` at the end. The engine parses the flat list into a tree before execution.

```
Step 0: sftp.list         { "connection_id": "...", "remote_path": "/incoming" }
Step 1: flow.foreach      { "source": "context:0.file_list" }
Step 2:   sftp.download   { "connection_id": "...", "remote_path": "context:loop.current_item.name" }
Step 3:   flow.if         { "left": "context:loop.current_item.size", "operator": "greater_than", "right": "1048576" }
Step 4:     pgp.encrypt   { "input_path": "context:2.downloaded_file", "recipient_key_ids": ["..."] }
Step 5:   flow.else
Step 6:     file.copy     { "source_path": "context:2.downloaded_file", "destination_path": "/archive/" }
Step 7:   flow.end        {}   ← closes flow.if
Step 8: flow.end          {}   ← closes flow.foreach
Step 9: file.delete       { "path": "/staging/*" }
```

The `ExecutionPlanParser` converts this flat list into a tree:

```
Root (Sequence)
  ├── StepNode(sftp.list)
  ├── ForEachNode(flow.foreach)
  │     └── Body:
  │           ├── StepNode(sftp.download)
  │           └── IfElseNode(flow.if)
  │                 ├── Then: StepNode(pgp.encrypt)
  │                 └── Else: StepNode(file.copy)
  └── StepNode(file.delete)
```

**Parsing rules:**
- Every `flow.foreach` and `flow.if` must have a matching `flow.end`
- `flow.else` can only appear inside a `flow.if` block (at most once)
- Blocks can be nested (foreach inside foreach, if inside foreach, etc.)
- Malformed block structure causes the parser to throw before execution begins

#### 5.20.3 Loop Context Variables

When iterating inside a `flow.foreach`, the engine injects magic context keys:

| Key | Value |
|-----|-------|
| `loop.current_item` | The current item from the collection (JsonElement) |
| `loop.current_item.{prop}` | Property access on the current item (for objects) |
| `loop.index` | Zero-based iteration index |

These are injected directly into the `JobContext` data dictionary, so existing `context:` resolution works unchanged. Step handlers don't need any modifications to work inside loops.

**Nested loops:** Inner loops shadow `loop.current_item` and `loop.index`. Outer loop values are preserved at `loop.{depth}.current_item` and `loop.{depth}.index` (zero-based depth), and restored when the inner loop exits.

#### 5.20.4 Condition Operators

The `flow.if` step evaluates `left {operator} right` using string comparison (case-insensitive) with numeric fallback for comparison operators.

| Operator | Behavior |
|----------|----------|
| `equals` | Case-insensitive string equality |
| `not_equals` | Negation of equals |
| `contains` | Left contains right (case-insensitive) |
| `greater_than` | Decimal comparison (string fallback if non-numeric) |
| `less_than` | Decimal comparison (string fallback if non-numeric) |
| `exists` | True if left is non-null and non-empty (right is ignored) |
| `regex` | Right is a regex pattern matched against left |

Both `left` and `right` support `context:` references, which are resolved before evaluation.

#### 5.20.5 Edge Cases

| Scenario | Behavior |
|----------|----------|
| Empty collection in foreach | Body skipped entirely. Execution continues with next step after `flow.end`. |
| Step outputs inside loops | Keyed as `"{stepOrder}.{key}"` — each iteration overwrites previous. Last iteration's values persist after loop. |
| Pause mid-loop | Pause checked before each step including inside loops. On resume, containing foreach restarts from iteration 0 with restored context. |
| Failure + Stop policy | Job fails immediately; abort signal propagates up through the loop. |
| Failure + SkipAndContinue | Failed step skipped; iteration continues with next step in body. |
| Malformed block structure | Parser throws descriptive error; job fails before execution begins. |

#### 5.20.6 Step Executions for Loop Bodies

Steps executed inside a `flow.foreach` record their `iteration_index` in the `step_executions` table. This allows tracking which iteration produced which output or error. Non-loop steps have `iteration_index = NULL`.

---

## 6. Connection & Protocol Layer

This section covers the abstraction layer for remote file transfer protocols (SFTP, FTP, FTPS), connection management, credential storage, and the session lifecycle that integrates with the Job Engine.

### 6.1 Connection Entity

A Connection is a first-class persisted entity representing a configured remote server. Connections are defined once and referenced by multiple jobs — changing a connection's configuration automatically affects all jobs that use it.

| Field                  | Type      | Description                                                    |
|------------------------|-----------|----------------------------------------------------------------|
| `id`                   | UUID      | Internal identifier referenced by job step configuration       |
| `name`                 | TEXT      | Human-readable label (e.g., "Partner X Production SFTP")       |
| `group`                | TEXT      | Organizational folder (e.g., "Partner X", "Legacy Systems")    |
| `protocol`             | ENUM      | `SFTP`, `FTP`, `FTPS`, `azure_function`                        |
| `host`                 | TEXT      | Hostname or IP address                                         |
| `port`                 | INT       | Port number (defaults: SFTP=22, FTP=21, FTPS=990)             |
| `auth_method`          | ENUM      | `Password`, `SshKey`, `PasswordAndSshKey`, `service_principal` |
| `username`             | TEXT      | Login username                                                 |
| `password_encrypted`   | BYTEA     | AES-256 encrypted password (nullable); master key for `azure_function` |
| `client_secret_encrypted` | BYTEA  | AES-256 encrypted Entra client secret (nullable; used by `azure_function` protocol) |
| `properties`           | JSONB     | Protocol-specific config (e.g., `workspace_id`, `tenant_id`, `client_id` for `azure_function`) |
| `ssh_key_id`           | UUID      | FK to the SSH Key Store (nullable)                             |
| `host_key_policy`      | ENUM      | `TrustOnFirstUse`, `AlwaysTrust`, `Manual`                     |
| `stored_host_fingerprint` | TEXT   | Known host fingerprint for TOFU/Manual policies                |
| `passive_mode`         | BOOL      | FTP/FTPS: use passive mode (default: true)                     |
| `tls_version_floor`    | ENUM      | FTPS: minimum TLS version (default: `TLS_1_2`)                |
| `tls_validate_cert`    | BOOL      | FTPS: validate server certificate (default: true)              |
| `ssh_algorithms`       | JSONB     | SFTP: preferred/restricted key exchange, cipher, MAC algorithms|
| `connect_timeout_sec`  | INT       | Connection timeout (default: 30)                               |
| `operation_timeout_sec`| INT       | Per-operation timeout (default: 300)                           |
| `keepalive_interval_sec`| INT      | Session keepalive ping interval (default: 60)                  |
| `transport_retries`    | INT       | Auto-reconnect attempts on connection drop (default: 2, max: 3)|
| `status`               | ENUM      | `Active`, `Disabled`                                           |
| `created_at`           | TIMESTAMP | Creation timestamp                                             |
| `updated_at`           | TIMESTAMP | Last modification timestamp                                    |
| `notes`                | TEXT      | Free-text notes (e.g., partner contact info, maintenance windows)|

#### 6.1.1 Connection Groups

Connections can be assigned to a group for organizational purposes. Groups are simple text labels — no separate entity or hierarchy. The frontend UI renders connections grouped by this field, with an "Ungrouped" section for connections without a group.

Common groupings: by partner name, by environment (Production / UAT / Dev), by department, or by data flow direction (inbound / outbound).

### 6.2 Unified Transfer Interface

All file transfer operations go through a common interface regardless of protocol. Protocol-specific implementations handle the underlying differences transparently.

```csharp
public interface ITransferClient : IAsyncDisposable
{
    string Protocol { get; }                              // "sftp", "ftp", "ftps"
    bool IsConnected { get; }

    // Connection lifecycle
    Task ConnectAsync(CancellationToken cancellationToken);
    Task DisconnectAsync();

    // File operations
    Task UploadAsync(
        UploadRequest request,
        IProgress<TransferProgress> progress,
        CancellationToken cancellationToken);
    Task DownloadAsync(
        DownloadRequest request,
        IProgress<TransferProgress> progress,
        CancellationToken cancellationToken);
    Task RenameAsync(string oldPath, string newPath,
        CancellationToken cancellationToken);
    Task DeleteFileAsync(string remotePath,
        CancellationToken cancellationToken);

    // Directory operations
    Task<IReadOnlyList<RemoteFileInfo>> ListDirectoryAsync(
        string remotePath,
        CancellationToken cancellationToken);
    Task CreateDirectoryAsync(string remotePath,
        CancellationToken cancellationToken);
    Task DeleteDirectoryAsync(string remotePath, bool recursive,
        CancellationToken cancellationToken);

    // Diagnostics
    Task<ConnectionTestResult> TestAsync(
        CancellationToken cancellationToken);
}

public record UploadRequest(
    string LocalPath,
    string RemotePath,
    bool AtomicUpload,                               // Upload as .tmp then rename
    string AtomicSuffix,                             // Default: ".tmp"
    bool ResumePartial);                             // Attempt resume if partial exists

public record DownloadRequest(
    string RemotePath,
    string LocalPath,
    bool ResumePartial);                             // Attempt resume if partial exists

public record TransferProgress(
    long BytesTransferred,
    long TotalBytes,
    string CurrentFile,
    double TransferRateBytesPerSec);

public record RemoteFileInfo(
    string Name,
    string FullPath,
    long Size,
    DateTime LastModified,
    bool IsDirectory);

public record ConnectionTestResult(
    bool Success,
    TimeSpan Latency,
    string? ServerBanner,
    string? ErrorMessage,
    IReadOnlyList<string>? SupportedAlgorithms);     // SFTP only
```

### 6.3 Protocol Implementations

#### 6.3.1 SFTP — SSH.NET

The SFTP implementation uses **SSH.NET** (`SSH.NET` NuGet package), a free, open-source, mature SSH library for .NET optimized for parallelism. It supports .NET 10 and provides both synchronous and asynchronous SFTP operations.

**Key integration points:**

- **Authentication**: `PasswordAuthenticationMethod`, `PrivateKeyAuthenticationMethod`, or both combined in a single `ConnectionInfo`. Private keys are loaded from the SSH Key Store at connection time.
- **Host key verification**: Handled via the `HostKeyReceived` event on `SftpClient`. The implementation checks the connection's `host_key_policy` and either accepts, compares against `stored_host_fingerprint`, or rejects.
- **Transfer resume**: SSH.NET supports offset-based operations. For upload resume, the client checks the remote file size and begins writing at that offset. For download resume, the client checks the local file size and requests data starting from that position.
- **Keepalive**: Configured via `ConnectionInfo.Timeout` and periodic `SendKeepAlive()` calls on a background timer.
- **Algorithm configuration**: SSH.NET allows specifying preferred key exchange, encryption, and MAC algorithms via `ConnectionInfo`. The connection entity's `ssh_algorithms` JSONB field maps directly to these settings.

#### 6.3.2 FTP / FTPS — FluentFTP

The FTP and FTPS implementations use **FluentFTP** (`FluentFTP` NuGet package), a widely-used, actively maintained FTP library that handles the many quirks and edge cases of the FTP specification across different server implementations.

**Key integration points:**

- **Plain FTP**: Standard unencrypted FTP. Used only for legacy systems where no secure alternative is available.
- **FTPS (Explicit)**: Connection starts as plain FTP, then upgrades to TLS via the `AUTH TLS` command. Configured via `FtpConfig.EncryptionMode = FtpEncryptionMode.Explicit`.
- **FTPS (Implicit)**: Connection is TLS from the start on a dedicated port (typically 990). Configured via `FtpConfig.EncryptionMode = FtpEncryptionMode.Implicit`.
- **Passive mode**: Default and recommended. Configured via `FtpConfig.DataConnectionType = FtpDataConnectionType.PASV`. Active mode available for environments that require it.
- **TLS configuration**: Minimum TLS version set via `FtpConfig.SslProtocols`. Certificate validation is controlled by `tls_cert_policy` (see below).
- **Transfer resume**: FluentFTP supports the FTP `REST` (restart) command. For uploads, `FtpRemoteExists.Resume` appends to existing partial files. For downloads, `FtpLocalExists.Resume` continues from the current local file size.

**FTPS certificate validation**:

FluentFTP delegates certificate validation to a `ValidateCertificate` event callback. If no handler is attached, FluentFTP **accepts all certificates by default** — including expired, self-signed, and hostname-mismatched certificates. Courier always attaches an explicit handler based on the connection's `tls_cert_policy`:

| Policy | Behavior | Who Can Set | FIPS Mode |
|--------|----------|-------------|-----------|
| **`SystemTrust`** (default) | Validate against the OS/container trust store. Rejects expired, self-signed, revoked, and hostname-mismatched certificates. | Any role | Allowed |
| **`PinnedThumbprint`** | Validate that the server certificate's SHA-256 thumbprint exactly matches `tls_pinned_thumbprint` on the connection. Ignores trust store (supports self-signed partner certs with a known thumbprint). Recommended for partners with self-signed or internal CA certs. | Any role | Allowed |
| **`Insecure`** | Accept any certificate without validation. **Disables TLS verification entirely.** Same restrictions as SSH `AlwaysTrust`: Admin-only, blocked in FIPS mode, blocked in production by default, audited on every use. | Admin only | Blocked |

**FluentFTP callback implementation**:

```csharp
private void ConfigureTlsValidation(FtpClient client, Connection connection)
{
    client.ValidateCertificate += (control, e) =>
    {
        switch (connection.TlsCertPolicy)
        {
            case TlsCertPolicy.SystemTrust:
                // Delegate to OS trust store — SslPolicyErrors == None means valid
                e.Accept = e.PolicyErrors == System.Net.Security.SslPolicyErrors.None;
                if (!e.Accept)
                    _logger.LogWarning("TLS cert rejected for {Host}: {Errors}",
                        connection.Host, e.PolicyErrors);
                break;

            case TlsCertPolicy.PinnedThumbprint:
                var thumbprint = e.Certificate.GetCertHashString(
                    System.Security.Cryptography.HashAlgorithmName.SHA256);
                e.Accept = string.Equals(
                    thumbprint,
                    connection.TlsPinnedThumbprint,
                    StringComparison.OrdinalIgnoreCase);
                if (!e.Accept)
                    _logger.LogWarning(
                        "TLS cert thumbprint mismatch for {Host}: " +
                        "expected {Expected}, got {Actual}",
                        connection.Host,
                        connection.TlsPinnedThumbprint,
                        thumbprint);
                break;

            case TlsCertPolicy.Insecure:
                // Accept anything, but log for audit
                _auditService.LogInsecureTlsCertAccepted(
                    connection.Id, connection.Host,
                    e.Certificate.Subject, e.PolicyErrors.ToString());
                e.Accept = true;
                break;
        }
    };
}
```

**Restrictions on `Insecure` cert policy** (identical to SSH `AlwaysTrust`):

- Admin-only to set (error `3006: Insecure TLS policy requires admin`)
- Blocked when FIPS mode enabled (error `3007: Insecure TLS policy not allowed in FIPS mode`)
- Blocked in production by default (same `security.insecure_trust_allow_production` setting)
- Audit event `InsecureTlsPolicyUsed` on every connection with certificate subject, issuer, and policy errors

#### 6.3.3 Protocol Support Matrix

| Capability              | SFTP          | FTP           | FTPS          |
|-------------------------|---------------|---------------|---------------|
| Encryption in transit   | Yes (SSH)     | No            | Yes (TLS)     |
| Password auth           | Yes           | Yes           | Yes           |
| SSH key auth            | Yes           | N/A           | N/A           |
| Combined auth           | Yes           | N/A           | N/A           |
| Transfer resume         | Yes           | Server-dependent | Server-dependent |
| Passive mode            | N/A           | Yes           | Yes           |
| Host key verification   | Yes           | N/A           | N/A           |
| Certificate validation  | N/A           | N/A           | Yes           |
| Directory listing       | Yes           | Yes           | Yes           |
| Atomic rename           | Yes           | Yes           | Yes           |
| Large file streaming    | Yes           | Yes           | Yes           |

#### 6.3.4 Azure Functions

Azure Function connections use the Admin API for fire-and-forget function invocation and Application Insights (Log Analytics) for polling completion and retrieving execution traces.

| Field | Purpose |
|-------|---------|
| `host` | Function App URL (e.g., `myapp.azurewebsites.net`) |
| `password_encrypted` | Master key for the Function App (encrypted) |
| `client_secret_encrypted` | Entra service principal client secret (encrypted) |
| `auth_method` | `service_principal` |
| `properties` (JSONB) | `{ "workspace_id": "...", "tenant_id": "...", "client_id": "..." }` |

**Trigger flow:** POST to `https://{host}/admin/functions/{functionName}` with `x-functions-key` header. Returns 202 immediately (fire-and-forget). No invocation ID is returned by Azure's Admin API.

**Completion detection:** Poll Application Insights via Log Analytics REST API using KQL: query `requests` table filtered by function name and trigger timestamp. Uses `Azure.Identity.ClientSecretCredential` for Entra token acquisition (handles automatic token refresh for multi-hour polls).

**Trace retrieval:** On-demand query of `traces` table filtered by `customDimensions.InvocationId`. Available after function completion via dedicated API endpoint.

### 6.4 Connection Session Management

Connections are scoped to the lifetime of a job execution. The first step in a job that requires a remote connection opens a session; subsequent steps in the same job reuse that session. When the job execution ends (success, failure, or cancellation), all sessions are closed.

#### 6.4.1 Job Connection Registry

An in-memory registry holds open sessions for the duration of each job execution:

```csharp
public class JobConnectionRegistry : IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, ITransferClient> _sessions = new();

    public async Task<ITransferClient> GetOrOpenAsync(
        Guid executionId,
        Guid connectionId,
        ConnectionEntity config,
        CancellationToken cancellationToken)
    {
        var key = $"{executionId}:{connectionId}";
        return _sessions.GetOrAdd(key, _ =>
        {
            var client = CreateClient(config);
            await client.ConnectAsync(cancellationToken);
            return client;
        });
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var session in _sessions.Values)
            await session.DisconnectAsync();
        _sessions.Clear();
    }
}
```

The registry is created per job execution and disposed when the execution completes. This ensures:
- **Session reuse**: A job with 5 SFTP steps hitting the same server uses one connection
- **Multi-server support**: A job that touches server A and server B holds two concurrent sessions
- **Clean teardown**: All sessions are guaranteed to close, even on failure or cancellation
- **No cross-job leakage**: Each job execution gets its own registry instance

#### 6.4.2 Session Health & Recovery

During a long-running job, a session may drop due to network issues or server-side timeouts. The transfer client handles this transparently:

1. Before each operation, check `IsConnected`
2. If disconnected, attempt to reconnect (up to `transport_retries` times)
3. If the operation was a transfer with `ResumePartial` enabled, resume from the last known offset
4. If reconnection fails after all retries, throw a `ConnectionLostException` which surfaces as a step failure
5. The job's failure policy then determines whether to retry the step, skip it, or fail the job

Keepalive pings run on a background timer (configurable interval, default: 60 seconds) for each active session to prevent server-side idle timeouts during long-running non-transfer steps (e.g., a PGP decryption step between two SFTP steps).

### 6.5 Transfer Resume for Large Files

Transfer resume is critical for Courier's 6–10 GB file workloads. Both upload and download resume are supported, with protocol-specific implementations.

#### 6.5.1 Upload Resume

1. Before uploading, check if a partial file exists at the remote destination
2. If it exists and `ResumePartial` is enabled, query the remote file size
3. Seek the local file stream to the remote file's size (the byte offset where the previous upload stopped)
4. Begin uploading from that offset, appending to the remote file
5. After completion, verify the remote file size matches the expected total

If the remote partial file is larger than the local file (indicating corruption), delete the remote file and restart from the beginning.

#### 6.5.2 Download Resume

1. Before downloading, check if a partial local file exists
2. If it exists and `ResumePartial` is enabled, query the local file size
3. Request data from the remote server starting at the local file's size offset
4. Append to the local file
5. After completion, verify the local file size matches the remote file's total size

#### 6.5.3 Resume Tracking in JobContext

When a transfer step completes (fully or partially), it writes resume metadata to the `JobContext`:

```json
{
    "2.transfer_state": {
        "remote_path": "/incoming/large_file.dat",
        "local_path": "/data/courier/temp/exec-123/large_file.dat",
        "bytes_transferred": 4294967296,
        "total_bytes": 6442450944,
        "completed": false
    }
}
```

On job resume (after pause or retriable failure), the step reads this metadata and resumes from `bytes_transferred` instead of restarting. This works in conjunction with the Job Engine's checkpoint system (Section 5.5).

### 6.6 Atomic Upload Pattern

To prevent downstream systems from reading partially uploaded files, Courier supports an atomic upload pattern configurable per step:

1. Upload the file as `{filename}{suffix}` (default suffix: `.tmp`)
2. On successful upload completion, rename to the final `{filename}`
3. If the upload fails, delete the partial `.tmp` file (best effort)

```json
{
    "step_type": "sftp.upload",
    "config": {
        "connection_id": "<uuid>",
        "local_path": "context:1.decrypted_file",
        "remote_path": "/outgoing/invoice.csv",
        "atomic_upload": true,
        "atomic_suffix": ".tmp"
    }
}
```

This is especially important in environments where partner systems poll directories for new files — without atomic upload, they may pick up a half-written file.

### 6.7 Host Key Verification (SFTP)

Each SFTP connection has a configurable host key verification policy:

**TrustOnFirstUse (TOFU)** — Default and recommended. On the first connection, the server's host key fingerprint is stored in the `known_hosts` table. On subsequent connections, the fingerprint is compared. If it changes, the connection is rejected with a `HostKeyMismatchException` and the connection transitions to a `RequiresAttention` state until an admin re-approves the new fingerprint.

**Manual** — The admin must provide the expected host key fingerprint before the first connection. The connection will not succeed until a matching fingerprint is configured. Most secure option for high-security environments.

**AlwaysTrust (Insecure)** — Accept any host key without verification. **This disables MITM protection** and should only be used for development, testing, or legacy environments where host keys change frequently and the network path is trusted.

**Restrictions on AlwaysTrust**:

- **Admin-only**: Setting `host_key_policy = 'always_trust'` requires the Admin role. Operators receive error `3004: Insecure host key policy requires admin`. Controlled by system setting `security.insecure_trust_require_admin` (default: `true`).
- **Blocked in FIPS mode**: When `security.fips_mode_enabled = true`, `AlwaysTrust` is rejected with error `3005: Insecure host key policy not allowed in FIPS mode`. FIPS compliance implies a secure operating environment where MITM protection must be enforced.
- **Audit on every use**: Every connection (not just configuration change) using `AlwaysTrust` generates an audit event `InsecureHostKeyPolicyUsed` with the connection ID, remote host, and the actual host key fingerprint that was accepted without verification.
- **UI warning**: The connection detail page displays a persistent red banner: "Host key verification disabled — this connection is vulnerable to man-in-the-middle attacks."
- **Blocked in production by default**: System setting `security.insecure_trust_allow_production` (default: `false`). When false, `AlwaysTrust` is only allowed if the environment is `Development` or `Staging`. Production deployments must use TOFU or Manual.

**SSH.NET callback implementation**:

Host key verification in SSH.NET is not automatic — it requires an explicit callback on the `HostKeyReceived` event. If no handler is attached, SSH.NET **accepts all host keys by default**, which is itself an insecure behavior. Courier always attaches a handler:

```csharp
private void ConfigureHostKeyVerification(SftpClient client, Connection connection)
{
    client.HostKeyReceived += (sender, e) =>
    {
        var fingerprint = $"SHA256:{Convert.ToBase64String(e.HostKeyHash)}";

        switch (connection.HostKeyPolicy)
        {
            case HostKeyPolicy.AlwaysTrust:
                // Accept, but log for audit trail
                _auditService.LogInsecureHostKeyAccepted(
                    connection.Id, connection.Host, fingerprint);
                e.CanTrust = true;
                break;

            case HostKeyPolicy.TrustOnFirstUse:
                var stored = _knownHostService.GetFingerprint(connection.Id);
                if (stored == null)
                {
                    // First connection — store and trust
                    _knownHostService.StoreFingerprint(
                        connection.Id, fingerprint, e.HostKeyName, "system");
                    e.CanTrust = true;
                }
                else if (stored == fingerprint)
                {
                    _knownHostService.UpdateLastSeen(connection.Id, fingerprint);
                    e.CanTrust = true;
                }
                else
                {
                    // Mismatch — reject and flag
                    e.CanTrust = false;
                    _connectionService.SetRequiresAttention(connection.Id,
                        $"Host key changed: expected {stored}, got {fingerprint}");
                }
                break;

            case HostKeyPolicy.Manual:
                e.CanTrust = connection.StoredHostFingerprint == fingerprint;
                break;
        }
    };
}

#### 6.7.1 Known Hosts Table

| Column          | Type      | Description                                           |
|-----------------|-----------|-------------------------------------------------------|
| `connection_id` | UUID      | FK to the connection                                  |
| `fingerprint`   | TEXT      | SHA-256 fingerprint of the server's host key          |
| `key_type`      | TEXT      | Algorithm (e.g., `ssh-rsa`, `ssh-ed25519`)            |
| `first_seen`    | TIMESTAMP | When the fingerprint was first recorded               |
| `last_seen`     | TIMESTAMP | Last successful connection with this fingerprint      |
| `approved_by`   | TEXT      | User who approved (for TOFU auto-approvals: "system") |

### 6.8 SSH Key Store

SSH keys used for SFTP authentication are stored in a dedicated key store, separate from the PGP Key Store (Section 7.3). While the security patterns are similar (encryption at rest, audit logging), the key formats, operations, and lifecycle are different enough to warrant separation.

#### 6.8.1 SSH Key Entity

| Field                | Type      | Description                                            |
|----------------------|-----------|--------------------------------------------------------|
| `id`                 | UUID      | Internal identifier referenced by connections          |
| `name`               | TEXT      | Human-readable label (e.g., "Partner X Auth Key")      |
| `key_type`           | ENUM      | `RSA_2048`, `RSA_4096`, `ED25519`, `ECDSA_256`, etc.  |
| `public_key_data`    | TEXT      | OpenSSH-format public key                              |
| `private_key_data`   | BYTEA     | AES-256 encrypted private key material                 |
| `passphrase_hash`    | TEXT      | Encrypted passphrase (nullable)                        |
| `fingerprint`        | TEXT      | SHA-256 fingerprint of the public key                  |
| `status`             | ENUM      | `Active`, `Retired`, `Deleted`                         |
| `created_at`         | TIMESTAMP | When the key was generated or imported                 |
| `created_by`         | TEXT      | User who created the key                               |
| `notes`              | TEXT      | Free-text (e.g., which servers accept this key)        |

#### 6.8.2 Supported Key Formats

**Import**: OpenSSH format, PEM (PKCS#1, PKCS#8), PuTTY PPK (v2 and v3). SSH.NET handles all of these natively via `PrivateKeyFile`.

**Export**: OpenSSH format (public key for adding to `authorized_keys` on remote servers).

**Generation**: Courier can generate SSH key pairs (RSA 2048/4096, Ed25519) using SSH.NET's key generation utilities. Generated keys are immediately encrypted and stored.

#### 6.8.3 Encryption at Rest

SSH private keys are encrypted using the same envelope encryption pattern as PGP keys (Section 7.3.6): a random AES-256 DEK per key, encrypted with AES-256-GCM, with the DEK wrapped by the Azure Key Vault KEK via wrap/unwrap operations. The KEK never leaves Key Vault. The stored blob includes the KEK version, wrapped DEK, IV, auth tag, and ciphertext.

### 6.9 Credential Storage

Connection passwords and SSH key passphrases are encrypted at rest using the same envelope encryption pattern used throughout Courier (Section 7.3.6):

1. A random 256-bit DEK is generated per credential
2. The credential is encrypted with AES-256-GCM using the DEK
3. The DEK is wrapped by the Azure Key Vault KEK via the `WrapKey` operation
4. The wrapped DEK, IV, auth tag, KEK version, and ciphertext are stored as BYTEA in the connection entity
5. Decryption happens in memory at connection time via Key Vault `UnwrapKey` — plaintext credentials are never written to disk or logs

Credential values are never returned in API responses. The API returns only a boolean `has_password` / `has_ssh_key` to indicate whether credentials are configured.

### 6.10 Directory Operations

Directory operations are available as both standalone step types and as utility methods on `ITransferClient`:

| Step Type Key        | Description                                           |
|----------------------|-------------------------------------------------------|
| `remote.mkdir`       | Create a directory on the remote server               |
| `remote.rmdir`       | Delete a directory on the remote server               |
| `remote.list`        | List directory contents (output to JobContext)         |

These step types are protocol-agnostic — they resolve the correct `ITransferClient` implementation based on the referenced connection's protocol. The step configuration includes the connection ID and the remote path:

```json
{
    "step_type": "remote.mkdir",
    "config": {
        "connection_id": "<uuid>",
        "remote_path": "/outgoing/2026/02/20",
        "recursive": true
    }
}
```

Recursive directory creation (`mkdir -p` equivalent) is supported for SFTP. For FTP/FTPS, recursive creation is emulated by creating each path segment sequentially.

### 6.11 Test Connection Endpoint

The API exposes a test endpoint for validating connection configuration without running a full job:

```
POST /api/connections/{id}/test
```

The test operation:

1. Opens a connection using the stored configuration and credentials
2. Authenticates with the configured method
3. Lists the root directory (or a configured base path) to verify access
4. For SFTP: records the server's host key fingerprint and supported algorithms. Host key verification runs through the `HostKeyReceived` callback per the connection's `host_key_policy` (Section 6.7).
5. For FTPS: validates the TLS handshake and server certificate per the connection's `tls_cert_policy` (Section 6.3.2). Returns the certificate subject, issuer, thumbprint, and expiration for display in the UI.
6. Measures round-trip latency
7. Disconnects

**Response**:

```json
{
    "success": true,
    "latency_ms": 142,
    "server_banner": "OpenSSH_9.6",
    "host_key_fingerprint": "SHA256:xxxxxxxxxxx",
    "supported_algorithms": ["aes256-gcm@openssh.com", "chacha20-poly1305@openssh.com"],
    "tls_certificate": {
        "subject": "CN=partner-sftp.example.com",
        "issuer": "CN=Let's Encrypt Authority X3",
        "thumbprint_sha256": "AB:CD:EF:...",
        "not_after": "2026-12-01T00:00:00Z",
        "policy_errors": "None"
    }
}
```

On failure, the response includes a diagnostic error message with actionable details (e.g., "Authentication failed: server rejected password", "Connection timed out after 30 seconds", "Host key mismatch: expected SHA256:xxx, got SHA256:yyy").

The frontend UI uses this endpoint to provide a "Test Connection" button on the connection configuration form.

### 6.12 Transfer Progress Reporting

All upload and download operations report progress via `IProgress<TransferProgress>` at regular intervals. The engine uses this data for:

- **Audit logging**: Total bytes transferred, average transfer rate, and duration are recorded in the step audit entry
- **Timeout detection**: If no progress is reported within the step's timeout window, the step is timed out (consistent with Section 5.12)
- **V2 UI progress**: Real-time transfer progress for the frontend dashboard

Progress is reported every 1MB transferred or every 5 seconds, whichever comes first. For small files (under 1MB), progress is reported once at completion.

### 6.13 TLS Configuration (FTPS)

FTPS connections support configurable TLS settings for compatibility with diverse server environments:

- **Minimum TLS version**: Default `TLS 1.2`. Can be lowered to `TLS 1.1` or `TLS 1.0` for legacy servers (flagged with a warning in the UI).
- **Certificate validation**: Enabled by default. Can be disabled for self-signed certificates. When disabled, a warning is displayed in the UI and logged on each connection.
- **Client certificate**: Optional client certificate for mutual TLS authentication. Certificate stored encrypted in the connection entity.

### 6.14 SSH Algorithm Configuration (SFTP)

For SFTP connections, administrators can restrict or prefer specific cryptographic algorithms to match partner server requirements or security policies:

```json
{
    "ssh_algorithms": {
        "key_exchange": ["ecdh-sha2-nistp256", "diffie-hellman-group14-sha256"],
        "encryption": ["aes256-gcm@openssh.com", "aes256-ctr"],
        "mac": ["hmac-sha2-256", "hmac-sha2-512"],
        "host_key": ["ssh-ed25519", "rsa-sha2-256"]
    }
}
```

If not configured, SSH.NET's defaults are used (which prioritize modern, secure algorithms). This setting is primarily needed when connecting to legacy servers that only support older algorithms — the UI flags any algorithm configuration that includes known-weak algorithms.

### 6.15 Connection Audit Log

All connection activity is recorded in the `connection_audit_log` table:

| Column            | Type      | Description                                               |
|-------------------|-----------|-----------------------------------------------------------|
| `id`              | UUID      | Audit record ID                                           |
| `connection_id`   | UUID      | FK to the connection                                      |
| `operation`       | ENUM      | `Connected`, `Disconnected`, `AuthSuccess`, `AuthFailed`, `Upload`, `Download`, `Rename`, `Delete`, `Mkdir`, `Rmdir`, `TestConnection`, `HostKeyApproved`, `HostKeyRejected` |
| `job_execution_id`| UUID      | FK to job execution (nullable — null for test connections) |
| `performed_by`    | TEXT      | User or system                                            |
| `performed_at`    | TIMESTAMP | When the operation occurred                               |
| `bytes_transferred`| BIGINT   | For upload/download operations                            |
| `duration_ms`     | INT       | Operation duration                                        |
| `details`         | JSONB     | Additional context (error messages, server responses, etc.)|

This log, combined with the Job audit trail (Section 5.15), provides complete traceability of all file movements through Courier.

---

## 7. Cryptography & Key Store

This section covers the PGP encryption/decryption engine used by the `pgp.encrypt` and `pgp.decrypt` job steps, the file signing and verification system, and the centralized Key Store that manages all cryptographic key material.

### 7.1 Unified Crypto Interface

All cryptographic operations go through a common provider interface, consistent with the plugin pattern used by the Job Step registry and Compression providers.

```csharp
public interface ICryptoProvider
{
    Task<CryptoResult> EncryptAsync(
        EncryptRequest request,
        IProgress<CryptoProgress> progress,
        CancellationToken cancellationToken);
    Task<CryptoResult> DecryptAsync(
        DecryptRequest request,
        IProgress<CryptoProgress> progress,
        CancellationToken cancellationToken);
    Task<CryptoResult> SignAsync(
        SignRequest request,
        IProgress<CryptoProgress> progress,
        CancellationToken cancellationToken);
    Task<VerifyResult> VerifyAsync(
        VerifyRequest request,
        IProgress<CryptoProgress> progress,
        CancellationToken cancellationToken);
}

public record EncryptRequest(
    string InputPath,
    string OutputPath,
    IReadOnlyList<Guid> RecipientKeyIds,           // One or more public keys
    Guid? SigningKeyId,                             // Optional: sign-then-encrypt
    OutputFormat Format);                           // Armored (.asc) or Binary (.pgp)

public record DecryptRequest(
    string InputPath,
    string OutputPath,
    Guid PrivateKeyId,
    bool VerifySignature);                         // Also verify if signed

public record SignRequest(
    string InputPath,
    string OutputPath,
    Guid SigningKeyId,
    SignatureMode Mode);                           // Detached, Inline, or Clearsign

public record VerifyRequest(
    string InputPath,
    string? DetachedSignaturePath,                 // null = inline/clearsigned
    Guid? ExpectedSignerKeyId);                    // null = accept any known key

public record VerifyResult(
    bool IsValid,
    VerifyStatus Status,                           // Valid, Invalid, UnknownSigner, ExpiredKey, RevokedKey
    string? SignerFingerprint,
    DateTime? SignatureTimestamp);

public enum VerifyStatus
{
    Valid,
    Invalid,
    UnknownSigner,
    ExpiredKey,
    RevokedKey
}
```

The `pgp.encrypt`, `pgp.decrypt`, `pgp.sign`, and `pgp.verify` job steps delegate entirely to this interface. Step configuration references keys by their Key Store ID, which the engine resolves at runtime.

### 7.2 Library Selection

Courier uses **BouncyCastle** (`Portable.BouncyCastle` NuGet package) as its PGP engine. BouncyCastle is the de facto .NET library for OpenPGP operations — it is mature, actively maintained, and widely deployed in enterprise environments.

Key capabilities used:
- RSA key pair generation (2048, 3072, 4096 bit)
- OpenPGP encryption and decryption with support for multiple recipients
- Detached, inline, and clearsigned signatures
- ASCII-armored and binary format output
- Passphrase-protected private key handling
- Streaming API for processing large files without full memory load

BouncyCastle also supports ECC algorithms (Curve25519, NIST P-256/P-384), which positions Courier for the planned ECC key support without changing libraries.

### 7.3 Key Store

The Key Store is a centralized, encrypted vault for all PGP key material. It is a first-class entity in Courier with its own management API and UI surface.

#### 7.3.1 Key Entity

Each key in the store has the following metadata:

| Field              | Type      | Description                                                  |
|--------------------|-----------|--------------------------------------------------------------|
| `id`               | UUID      | Internal identifier used in job step configuration           |
| `name`             | TEXT      | Human-readable label (e.g., "Partner X Production Key")      |
| `fingerprint`      | TEXT      | Full PGP fingerprint (40 hex chars)                          |
| `key_id`           | TEXT      | Short key ID (16 hex chars) for display                      |
| `algorithm`        | ENUM      | `RSA_2048`, `RSA_3072`, `RSA_4096`, `ECC_CURVE25519`, etc.  |
| `key_type`         | ENUM      | `PublicOnly`, `KeyPair` (public + private)                   |
| `purpose`          | TEXT      | Free-text notes (e.g., partner name, use case)               |
| `status`           | ENUM      | `Active`, `Expiring`, `Retired`, `Revoked`, `Deleted`        |
| `created_at`       | TIMESTAMP | When the key was generated or imported                       |
| `expires_at`       | TIMESTAMP | Key expiration date (nullable for non-expiring keys)         |
| `public_key_data`  | TEXT      | ASCII-armored public key                                     |
| `private_key_data` | BYTEA     | AES-256 encrypted private key material (null for public-only)|
| `passphrase_hash`  | TEXT      | Encrypted passphrase for passphrase-protected private keys   |
| `created_by`       | TEXT      | User who generated or imported the key                       |

The `algorithm` enum is designed with room for expansion. Adding ECC support in the future requires adding new enum values and implementing the BouncyCastle ECC key generation — no schema migration needed.

#### 7.3.2 Key Status Lifecycle

Keys follow a defined lifecycle with five states:

```
    ┌────────┐
    │ Active │
    └──┬─┬─┬─┘
       │ │ │
       │ │ └──────────────┐ (manual revoke)
       │ │                │
       │ │ (approaching   │
       │ │  expiration)   │
       │ │           ┌────▼────┐
       │ │           │ Revoked │  ── cannot be used by any job
       │ │           └─────────┘
       │ │
       │ ┌────▼─────┐
       │ │ Expiring │  ── still usable, emits warning events
       │ └──┬──┬────┘
       │    │  │
       │    │  └── (expires) ──► Retired
       │    │
       │    └── (manual revoke) ──► Revoked
       │
       │ (manual retire)
  ┌────▼────┐
  │ Retired │  ── cannot be used by new jobs, existing references warn
  └─────────┘
       │
       │ (manual delete)
  ┌────▼────┐
  │ Deleted │  ── soft delete, key material purged, metadata retained for audit
  └─────────┘
```

**Active**: Key is available for use by any job. Default state after generation or import.

**Expiring**: Key's expiration date is within the configured warning window (default: 30 days). The key is still fully functional but domain events are emitted (`KeyExpiringSoon`) for V2 alerting. A background service checks expiration dates daily and transitions keys automatically.

**Retired**: Key has been manually retired or has passed its expiration date. Jobs cannot use a retired key for new operations. If an existing job references a retired key, the step fails with a descriptive error indicating the key must be updated. Decryption and signature verification with retired keys remain possible to handle legacy files.

**Revoked**: Key has been explicitly revoked, indicating it should no longer be trusted under any circumstances. All operations (including decryption and verification) are blocked. Verification against a revoked key returns `VerifyStatus.RevokedKey`. This is a terminal state — a revoked key cannot be re-activated.

**Deleted**: Soft delete. Private key material is securely purged (overwritten with zeros before removal). Public key data and metadata are retained for audit trail purposes. The key no longer appears in normal queries but is visible in audit history.

#### 7.3.3 Key Generation

Courier can generate RSA key pairs internally:

- **Supported sizes**: 2048, 3072, 4096 bit (default: 4096)
- **Key format**: OpenPGP-compatible key pair with configurable user ID (name + email)
- **Expiration**: Optional expiration date set at generation time
- **Passphrase**: Optional passphrase protection on the private key

Generation uses BouncyCastle's `RsaKeyPairGenerator` with a secure random source. The key pair is immediately encrypted and stored in the Key Store. The public key is made available for export.

#### 7.3.4 Key Import

Keys can be imported from external sources in the following formats:

| Format           | Extension(s)       | Import Behavior                          |
|------------------|--------------------|------------------------------------------|
| ASCII Armored    | `.asc`, `.txt`     | Parsed directly by BouncyCastle          |
| Binary           | `.pgp`, `.gpg`     | Parsed directly by BouncyCastle          |
| Keyring          | `.kbx`, `.gpg`     | Extracted into individual keys on import |

On import, Courier:

1. Parses the key material and extracts metadata (fingerprint, algorithm, expiration, user IDs)
2. Validates key integrity (verifies self-signatures)
3. Checks for fingerprint collisions with existing keys in the store
4. If importing a private key, prompts for the passphrase and verifies it can unlock the key
5. Encrypts private key material via envelope encryption (random DEK + Key Vault wrap) before storage
6. Creates the Key Store record with status `Active`

If the imported key is already expired, it is imported with status `Retired` and a warning is returned.

#### 7.3.5 Key Export

**Public key export** (authenticated, all roles):

- **Formats**: ASCII-armored (.asc) or binary (.pgp)
- **Access**: Available via API (`GET /api/v1/pgp-keys/{id}/export/public?format=armored`) and the frontend UI
- **Authentication required** — all public key exports go through the standard Entra ID auth pipeline. Even public keys reveal identity, partner relationships, and operational metadata. All exports are logged as audit events.

**Shareable public key links** (optional, Admin-only):

For scenarios where a partner needs to download a public key without Entra ID credentials, Admins can generate a time-limited, single-purpose shareable link:

- **Endpoint**: `POST /api/v1/pgp-keys/{id}/share` → returns a URL with a cryptographic token
- **URL format**: `/api/v1/pgp-keys/shared/{token}` — no authentication required on this endpoint
- **Token**: 256-bit random, stored hashed (SHA-256) in the database alongside key ID, expiration, and creator
- **Default expiration**: 72 hours (configurable per link, maximum 30 days, set via `security.max_share_link_days` system setting)
- **Single key, read-only**: The token grants access to one specific public key export only, nothing else
- **Audit**: Link creation, every download via the link, and link expiration are all logged with the requesting IP address
- **Revocation**: Links can be revoked before expiration via `DELETE /api/v1/pgp-keys/{id}/share/{token}`
- **Disabled by default**: The `security.public_key_share_links_enabled` system setting defaults to `false`. Admins must explicitly enable the feature before any shareable links can be generated

**Private key export** is a sensitive operation:

- Requires explicit user confirmation
- Exported private key is optionally re-encrypted with a user-provided passphrase
- The export event is prominently logged in the key audit trail
- A future enhancement could require multi-user approval for private key export

#### 7.3.6 Private Key Encryption at Rest

> **V1 Implementation Note — Local KEK (No Azure Key Vault)**
>
> The design below describes the target architecture using Azure Key Vault for KEK management. In V1, we opted **not** to use Azure Key Vault. Instead, credential encryption uses a **local AES-256-GCM envelope encryption** scheme with a KEK sourced from an environment variable (`COURIER_ENCRYPTION_KEY`, base64-encoded 256-bit key) or `appsettings.json` configuration (`Encryption:KeyEncryptionKey`).
>
> **V1 implementation (`AesGcmCredentialEncryptor`)**:
> - KEK is loaded from configuration at startup (validated: must be exactly 32 bytes)
> - DEK wrapping uses AES-256-GCM (KEK wraps DEK locally) instead of Key Vault `WrapKey`/`UnwrapKey`
> - Blob format: `[1B version][12B DEK-wrap-nonce][16B DEK-wrap-tag][32B wrapped-DEK][12B data-nonce][16B data-tag][N bytes ciphertext]` (89 + N bytes)
> - DEK is zeroed via `CryptographicOperations.ZeroMemory()` in `finally` blocks
> - The KEK **does** exist in process memory (unlike Key Vault where it never leaves the HSM)
> - A pre-generated dev-only key is provided in `appsettings.Development.json`; production deployments must supply their own key
>
> **Migration path to Key Vault (V2)**: Replace `AesGcmCredentialEncryptor` with the `EnvelopeEncryptionService` described below. Since both schemes use AES-256-GCM for data encryption, migration requires only re-wrapping DEKs (unwrap with local KEK, re-wrap via Key Vault) — the ciphertext payload is unchanged. The `ICredentialEncryptor` interface abstracts this swap.
>
> **Security trade-off**: The local KEK approach is simpler to deploy (no Azure dependency) but the KEK resides in process memory. An attacker with a memory dump could extract it. Key Vault eliminates this risk. For V1's deployment model (single-tenant, controlled infrastructure), this trade-off is acceptable.

All private key material is encrypted before storage in PostgreSQL using **AES-256-GCM envelope encryption** with Azure Key Vault wrap/unwrap operations. The master key (KEK) never leaves Key Vault.

**Encryption flow** (on key generation or import):

1. Generate a random 256-bit **Data Encryption Key (DEK)** using `RandomNumberGenerator`
2. Generate a random 96-bit IV
3. Encrypt the private key material with the DEK using AES-256-GCM, producing ciphertext + authentication tag
4. Call **Azure Key Vault `WrapKey`** to wrap the DEK with the KEK (RSA-OAEP-256). Key Vault returns the wrapped DEK and the KEK version used
5. Store the following as a single binary blob in `private_key_data`:

```
┌────────────┬──────────────┬─────────┬──────────┬─────────────┬────────────┐
│ KEK Version│ Wrapped DEK  │ IV      │ Auth Tag │ Ciphertext  │ Algorithm  │
│ (36 bytes) │ (256 bytes)  │(12 bytes)│(16 bytes)│ (variable)  │ (2 bytes)  │
└────────────┴──────────────┴─────────┴──────────┴─────────────┴────────────┘
```

**Decryption flow** (on private key use):

1. Parse the stored blob to extract KEK version, wrapped DEK, IV, auth tag, and ciphertext
2. Call **Azure Key Vault `UnwrapKey`** with the wrapped DEK and KEK version. Key Vault returns the plaintext DEK
3. Decrypt the ciphertext with the DEK using AES-256-GCM, verifying the authentication tag
4. Use the plaintext private key for the requested operation
5. Zero the plaintext DEK and private key from memory when done (`CryptographicOperations.ZeroMemory`)

**Key properties**:

- Each entity gets its own random DEK — no DEK is ever reused across entities
- The KEK (RSA 2048-bit, Key Vault key, not a secret) is never exported or held in application memory
- Key Vault operations are remote HTTPS calls — latency is ~20ms per wrap/unwrap
- The wrapped DEK is opaque to the application — only Key Vault can unwrap it
- Storing the KEK version enables seamless rotation: new encryptions use the latest KEK version, old data references the version it was encrypted with

**Master key rotation**:

When the KEK is rotated in Key Vault (new version created), a background task iterates all encrypted entities and re-wraps their DEKs:

1. Unwrap the DEK using the old KEK version (stored in the blob)
2. Re-wrap the DEK using the new KEK version
3. Update the stored blob with the new wrapped DEK and new KEK version
4. The actual ciphertext is untouched — only the DEK wrapper changes

This means rotation does not require re-encrypting any private key material, only re-wrapping the DEKs.

**What works in V1 without any code changes**: When a new KEK version is created in Key Vault, all *new* encryptions automatically use the latest version (the `CryptographyClient` defaults to the latest version for `WrapKeyAsync`). All *existing* encrypted blobs continue to decrypt correctly because the blob stores the specific KEK version URI, and `UnwrapKeyAsync` uses that version. Key Vault retains all previous versions unless explicitly disabled. This means KEK rotation is immediately safe — the transition period where old and new versions coexist is handled natively by Key Vault's versioning.

**What's planned for V2**: An automated background task that iterates all encrypted entities and re-wraps their DEKs with the current KEK version. This is a key hygiene measure (ensures old KEK versions can eventually be disabled) but is not required for security — old versions remain usable until explicitly disabled in Key Vault.

If Azure Key Vault is unavailable at startup, Courier refuses to start rather than operating with unencrypted keys.

#### 7.3.7 EnvelopeEncryptionService Implementation

The `EnvelopeEncryptionService` is the single code path for all at-rest encryption in Courier. Its design enforces a critical invariant: **no key material — neither the KEK nor any unwrapped DEK — is ever cached in process memory beyond a single encrypt or decrypt operation**.

```csharp
// Courier.Infrastructure/Encryption/EnvelopeEncryptionService.cs
public sealed class EnvelopeEncryptionService : IEnvelopeEncryptionService
{
    private readonly CryptographyClient _cryptoClient;  // Azure SDK — calls Key Vault REST API
    private readonly string _kekKeyName;

    // NOTE: CryptographyClient does NOT download the key. It sends wrap/unwrap
    // requests to Key Vault over HTTPS. The KEK plaintext never exists in this process.
    public EnvelopeEncryptionService(CryptographyClient cryptoClient, string kekKeyName)
    {
        _cryptoClient = cryptoClient;
        _kekKeyName = kekKeyName;
    }

    public async Task<EncryptedBlob> EncryptAsync(
        ReadOnlyMemory<byte> plaintext, CancellationToken ct)
    {
        // 1. Generate a random DEK — unique per call, never reused
        var dek = new byte[32]; // AES-256
        RandomNumberGenerator.Fill(dek);

        // 2. Generate a random IV
        var iv = new byte[12]; // AES-GCM 96-bit
        RandomNumberGenerator.Fill(iv);

        // 3. Encrypt plaintext with the DEK
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[16]; // AES-GCM 128-bit auth tag
        using (var aes = new AesGcm(dek, tagSizeInBytes: 16))
        {
            aes.Encrypt(iv, plaintext.Span, ciphertext, tag);
        }

        // 4. Wrap the DEK via Key Vault (remote HTTPS call)
        //    The KEK never leaves Key Vault. We send the DEK *to* Key Vault,
        //    Key Vault encrypts it with the KEK, and returns the wrapped blob.
        var wrapResult = await _cryptoClient.WrapKeyAsync(
            KeyWrapAlgorithm.RsaOaep256, dek, ct);

        // 5. Zero the plaintext DEK *immediately* — it must not survive this method
        CryptographicOperations.ZeroMemory(dek);

        // 6. Return the composite blob (no secret material remains in memory)
        return new EncryptedBlob(
            KekVersion: wrapResult.KeyId,     // Key Vault key version URI
            WrappedDek: wrapResult.EncryptedKey,
            Iv: iv,
            AuthTag: tag,
            Ciphertext: ciphertext);
    }

    public async Task<byte[]> DecryptAsync(EncryptedBlob blob, CancellationToken ct)
    {
        // 1. Unwrap the DEK via Key Vault (remote HTTPS call)
        //    Key Vault decrypts the wrapped DEK using the KEK version recorded in the blob.
        var unwrapResult = await _cryptoClient.UnwrapKeyAsync(
            KeyWrapAlgorithm.RsaOaep256, blob.WrappedDek, ct);
        var dek = unwrapResult.Key;

        try
        {
            // 2. Decrypt the ciphertext with the unwrapped DEK
            var plaintext = new byte[blob.Ciphertext.Length];
            using (var aes = new AesGcm(dek, tagSizeInBytes: 16))
            {
                aes.Decrypt(blob.Iv, blob.Ciphertext, blob.AuthTag, plaintext);
            }
            return plaintext;
        }
        finally
        {
            // 3. Zero the plaintext DEK — even if decryption fails
            CryptographicOperations.ZeroMemory(dek);
        }
    }
}

public record EncryptedBlob(
    string KekVersion,          // Key Vault key version URI (e.g., "https://vault.azure.net/keys/courier-kek/abc123")
    byte[] WrappedDek,          // DEK encrypted by KEK (opaque — only Key Vault can unwrap)
    byte[] Iv,                  // AES-GCM initialization vector (unique per operation)
    byte[] AuthTag,             // AES-GCM authentication tag (integrity verification)
    byte[] Ciphertext);         // Encrypted payload
```

**What this code guarantees**:

- The `CryptographyClient` is an Azure SDK client that sends HTTPS requests to Key Vault. It never downloads the KEK. The `WrapKeyAsync` / `UnwrapKeyAsync` methods send the DEK to Key Vault for server-side encryption/decryption using the KEK that exists only inside Key Vault's HSM or software boundary.
- The plaintext DEK exists in process memory only for the duration of a single `EncryptAsync` or `DecryptAsync` call, and is zeroed via `CryptographicOperations.ZeroMemory` in a `finally` block before the method returns.
- No DEK is ever cached, pooled, or stored in a field. Every encrypt operation generates a fresh DEK. Every decrypt operation unwraps the DEK, uses it, and zeros it.
- The service is registered as a **singleton** (the `CryptographyClient` is thread-safe and connection-pooled by the Azure SDK), but it holds no mutable state — no keys, no caches, no session data.

**What this code explicitly does NOT do** (anti-patterns that would compromise the security model):

| Anti-Pattern | Why It's Dangerous | How Courier Avoids It |
|-------------|-------------------|----------------------|
| Cache the KEK in a `private byte[] _masterKey` field | Process memory dump exposes all encrypted data. Single point of compromise for the full process lifetime. | `CryptographyClient` never exposes the KEK. It's a remote API client, not a key holder. |
| Cache unwrapped DEKs in an `IMemoryCache` or `ConcurrentDictionary` | DEK cache turns a single-entity breach into a multi-entity breach. Extends DEK exposure window from milliseconds to process lifetime. | DEKs are zeroed in `finally` blocks. No caching layer exists between Key Vault and the decrypt operation. |
| Download the KEK at startup via `GetKeyAsync` and use it locally | Eliminates the Key Vault security boundary. Makes the application the HSM. | `CryptographyClient` is used (wraps the Key Vault Cryptography REST API), not `KeyClient` (which can download public key material but not private keys for HSM-backed keys). |
| Pre-decrypt all credentials at startup into an in-memory store | Creates a "golden store" in process memory — one dump exposes all credentials. | Credentials are decrypted on-demand at connection time and zeroed after use. |
| Use `AzureKeyVaultConfigurationProvider` for the KEK | The config provider loads *values* into the .NET `IConfiguration` system as strings. The KEK is a *key object*, not a secret string — it cannot be loaded this way, and attempting to would mean holding key material in configuration memory. | KEK is accessed only via `CryptographyClient.WrapKeyAsync` / `UnwrapKeyAsync`. Application secrets (DB connection string, etc.) use the config provider. These are different systems serving different purposes. |

**Latency trade-off**: Every encrypt or decrypt operation incurs a Key Vault round-trip (~20ms). For Courier's workload (decrypt a credential or private key at job execution time, not on every API request), this is acceptable. A job that decrypts 3 credentials and 1 PGP key adds ~80ms of Key Vault overhead to its startup — negligible against a multi-minute file transfer. The NFR target (Section 2.11) accounts for this overhead.

**DI registration**:

```csharp
// Courier.Infrastructure/DependencyInjection.cs
services.AddSingleton(sp =>
{
    var vaultUri = sp.GetRequiredService<IConfiguration>()["KeyVault:Uri"]!;
    var keyName = sp.GetRequiredService<IConfiguration>()["KeyVault:KekKeyName"]!;
    var credential = new DefaultAzureCredential();

    // CryptographyClient — performs remote wrap/unwrap. Does NOT download the key.
    var cryptoClient = new CryptographyClient(
        new Uri($"{vaultUri}/keys/{keyName}"), credential);

    return new EnvelopeEncryptionService(cryptoClient, keyName);
});
```

**Note on `CryptographyClient` vs `KeyClient`**: The Azure SDK has two clients for Key Vault keys. `KeyClient` manages key lifecycle (create, rotate, delete, get metadata). `CryptographyClient` performs cryptographic operations (wrap, unwrap, sign, verify) without ever exposing the private key material. Courier uses `CryptographyClient` for envelope encryption and `KeyClient` only for the KEK rotation background task (to create new key versions and read metadata). Neither client can export an HSM-backed private key.

### 7.4 Encryption Operations

#### 7.4.1 Single-Recipient Encryption

The standard flow for encrypting a file:

1. Resolve the recipient's public key from the Key Store by ID
2. Validate the key is in `Active` or `Expiring` status
3. Open a streaming pipeline: `FileStream (input) → PGP EncryptionStream → FileStream (output)`
4. Write output in the configured format (armored or binary)
5. Report progress via `IProgress<CryptoProgress>` at regular intervals

#### 7.4.2 Multi-Recipient Encryption

When multiple recipient key IDs are provided, the file is encrypted such that any recipient can decrypt it with their own private key. BouncyCastle handles this natively by adding multiple public key encrypted session keys to the PGP message.

All recipient keys must be `Active` or `Expiring`. If any key is `Retired`, `Revoked`, or `Deleted`, the operation fails with a descriptive error listing the invalid keys.

#### 7.4.3 Sign-Then-Encrypt

A common workflow is to sign a file with the sender's private key and then encrypt it for the recipient. This is supported as a single step configuration:

```json
{
    "step_type": "pgp.encrypt",
    "config": {
        "recipient_key_ids": ["<recipient-uuid>"],
        "signing_key_id": "<sender-private-key-uuid>",
        "output_format": "armored"
    }
}
```

The engine signs the data first (using the private key), then encrypts the signed payload. The recipient decrypts first, then verifies the signature — both operations are automatic when `verify_signature: true` is set on the decrypt step.

### 7.5 Decryption Operations

Decryption resolves the private key from the Key Store, handles passphrase unlocking transparently, and streams the decrypted output:

1. Load the encrypted file as a stream
2. Resolve the private key and decrypt it via envelope decryption (Key Vault unwraps the DEK, DEK decrypts the key material)
3. If the private key has a passphrase, unlock it
4. Stream decrypt: `FileStream (encrypted) → PGP DecryptionStream → FileStream (output)`
5. If `verify_signature` is enabled and the message was signed, verify the signature against known public keys in the store
6. Write the `VerifyResult` to `JobContext` for downstream decision-making

If decryption fails (wrong key, corrupted data), the step fails with a specific error type that the job's failure policy can act on.

### 7.6 Signing & Verification

#### 7.6.1 Signature Modes

| Mode         | Output                                          | Use Case                              |
|--------------|--------------------------------------------------|---------------------------------------|
| `Detached`   | Separate `.sig` file alongside the original      | Original file must remain unmodified  |
| `Inline`     | Signed data wrapping the original content        | Single file with embedded signature   |
| `Clearsign`  | Human-readable text with ASCII signature block   | Text files, emails                    |

The signature mode is configured per step. Detached is the default and most common for file transfer workflows.

#### 7.6.2 Verification

Signature verification can be performed as a standalone step (`pgp.verify`) or as part of decryption. The result is a typed `VerifyResult` written to the `JobContext`:

- **Valid**: Signature matches, signer key is `Active` or `Expiring`
- **Invalid**: Signature does not match the data (file may be tampered)
- **UnknownSigner**: Signature is cryptographically valid but the signer's public key is not in the Key Store
- **ExpiredKey**: Signer's key has expired (signature may still be trustworthy depending on policy)
- **RevokedKey**: Signer's key has been revoked (signature should not be trusted)

Downstream steps can reference the verification status in their configuration to branch behavior — for example, a `file.move` step could be configured to only execute if `verify_status == Valid`.

### 7.7 Streaming Crypto for Large Files

All cryptographic operations use BouncyCastle's streaming API. The pipeline for a large file encryption:

```
FileStream (source, 80KB buffer)
  → [Optional] SignatureGenerationStream
    → PgpEncryptedDataGenerator (streaming)
      → [Optional] ArmoredOutputStream
        → FileStream (output, 80KB buffer)
```

Memory usage is bounded to approximately 2× the buffer size regardless of file size. For 6–10 GB files, the bottleneck is disk I/O, not memory. Progress is reported every 10MB processed, consistent with the compression system.

### 7.8 Key Rotation Management

A background service (`KeyExpirationService`) runs daily and performs the following:

1. **Scan** all keys with `Active` status and an `expires_at` date
2. **Transition** keys within the warning window (default: 30 days) to `Expiring` status
3. **Emit** `KeyExpiringSoon` domain events for each newly transitioned key
4. **Transition** keys past their expiration date to `Retired` status
5. **Emit** `KeyExpired` domain events for each newly retired key

In V1, these domain events are recorded in the key audit log. In V2, event subscribers will deliver notifications via email, Slack, or UI alerts. The event infrastructure mirrors the Job Engine's notification hooks (Section 5.17), ensuring consistent patterns across the system.

**Optional auto-generation**: A future enhancement could automatically generate a replacement key pair when a key enters `Expiring` status. The new key would be created with the same parameters (algorithm, size, user ID) and linked to the expiring key as its successor. This is not in V1 scope but the schema accommodates it with a nullable `successor_key_id` column.

### 7.9 Key Audit Log

All key operations are recorded in the `key_audit_log` table:

| Column          | Type      | Description                                              |
|-----------------|-----------|----------------------------------------------------------|
| `id`            | UUID      | Audit record ID                                          |
| `key_id`        | UUID      | FK to the key                                            |
| `operation`     | ENUM      | `Generated`, `Imported`, `Exported`, `UsedForEncrypt`, `UsedForDecrypt`, `UsedForSign`, `UsedForVerify`, `StatusChanged`, `Deleted` |
| `performed_by`  | TEXT      | User or system service that performed the operation      |
| `performed_at`  | TIMESTAMP | When the operation occurred                              |
| `job_execution_id` | UUID   | FK to job execution if the operation was part of a job   |
| `details`       | JSONB     | Additional context (e.g., old status → new status, export format, error details) |

This log is append-only and never modified. It provides full traceability for compliance requirements — you can answer questions like "which jobs used this key in the last 90 days" or "who exported this private key and when."

### 7.10 ECC Future-Proofing

While V1 supports RSA only, the system is designed for ECC support without breaking changes:

- The `algorithm` enum on the key entity includes placeholder values for `ECC_CURVE25519`, `ECC_P256`, and `ECC_P384`
- The `ICryptoProvider` interface is algorithm-agnostic — it receives key IDs and resolves the correct BouncyCastle implementation at runtime
- BouncyCastle already supports ECC key generation and OpenPGP operations with elliptic curves
- Adding ECC requires: implementing ECC key generation, adding ECC-specific validation rules, and testing interoperability with common PGP clients (GPG, Kleopatra)

No database migration, interface changes, or job configuration changes will be needed.

---

## 8. File Operations

This section covers two subsystems: the compression/decompression engine used by `file.zip` and `file.unzip` job steps, and the File Monitor system that watches directories for changes and triggers job executions.

### 8.1 Compression & Decompression

#### 8.1.1 Unified Interface

All compression operations go through a common provider interface, resolved by format at runtime. This follows the same plugin pattern as the Job Step registry.

```csharp
public interface ICompressionProvider
{
    string FormatKey { get; }                          // e.g., "zip", "tar.gz", "7z"
    Task CompressAsync(
        CompressRequest request,
        IProgress<CompressionProgress> progress,
        CancellationToken cancellationToken);
    Task DecompressAsync(
        DecompressRequest request,
        IProgress<CompressionProgress> progress,
        CancellationToken cancellationToken);
    Task<ArchiveContents> InspectAsync(               // List contents without extracting
        string archivePath,
        CancellationToken cancellationToken);
}

public record CompressRequest(
    IReadOnlyList<string> SourcePaths,                // Files or glob patterns
    string OutputPath,                                 // Destination archive path
    string? Password,                                  // Optional AES-256 encryption
    SplitArchiveConfig? SplitConfig);                  // Optional split into chunks

public record DecompressRequest(
    string ArchivePath,                                // Source archive (or first split part)
    string OutputDirectory,                            // Destination for extracted files
    string? Password);                                 // Optional decryption password

public record CompressionProgress(
    long BytesProcessed,
    long TotalBytes,
    string CurrentFile);
```

A `CompressionProviderRegistry` resolves the correct provider by format key. The `file.zip` and `file.unzip` job steps delegate entirely to this registry, keeping step logic thin.

#### 8.1.2 Supported Formats

| Format   | Compress | Decompress | Password Support | Library                    |
|----------|----------|------------|------------------|----------------------------|
| ZIP      | Yes      | Yes        | AES-256          | SharpZipLib                |
| GZIP     | Yes      | Yes        | No               | SharpZipLib                |
| TAR      | Yes      | Yes        | No               | SharpZipLib                |
| TAR.GZ   | Yes      | Yes        | No               | SharpZipLib                |
| 7z       | Yes      | Yes        | Yes              | 7z CLI via Process wrapper |
| RAR      | No       | Yes        | Yes              | SharpCompress              |

RAR creation is not supported because the RAR format is proprietary and licensing prohibits creation in third-party software. Extraction is supported via SharpCompress which implements the decompression algorithm under license.

For 7z, the most reliable cross-platform approach is wrapping the `7z` CLI binary (bundled in the Docker image) via a managed `Process` call. Native .NET 7z libraries exist but have inconsistent support for advanced features like solid archives and multi-threaded compression. The CLI wrapper provides full feature parity and has been battle-tested.

**7z CLI security hardening**:

Invoking an external process from a server application is inherently risky. The following protections are mandatory:

**No shell invocation**: The wrapper uses `ProcessStartInfo` with `UseShellExecute = false` and passes arguments as individual elements, never through shell interpretation. This eliminates command injection via filenames or passwords.

```csharp
var psi = new ProcessStartInfo
{
    FileName = "/usr/bin/7z",          // Absolute path — no PATH lookup
    UseShellExecute = false,           // No shell — exec() directly
    CreateNoWindow = true,
    RedirectStandardOutput = true,
    RedirectStandardError = true,
    Environment = {                    // Minimal environment
        ["PATH"] = "/usr/bin",
        ["LANG"] = "C.UTF-8"
    }
};

// Arguments are added individually — never string-interpolated
psi.ArgumentList.Add("a");                              // command: add
psi.ArgumentList.Add("-t7z");                           // format
psi.ArgumentList.Add($"-p{password}");                  // password (if any)
psi.ArgumentList.Add("-mhe=on");                        // encrypt headers
psi.ArgumentList.Add(sanitizedOutputPath);              // output archive
psi.ArgumentList.Add(sanitizedInputPath);               // input file(s)
```

**Filename sanitization**: All filenames are validated before being passed to the CLI. The validation rejects: path traversal sequences (`..`, `./`), absolute paths outside the job's temp directory, null bytes, control characters, and shell metacharacters (`` ` ``, `$`, `|`, `;`, `&`, `>`, `<`). Filenames that fail validation cause the step to fail with error `7001: Unsafe filename in archive operation`.

**Temp directory isolation**: All 7z operations execute within the job's unique temp directory (`/data/courier/temp/{executionId}/`). The wrapper sets `ProcessStartInfo.WorkingDirectory` to this path and validates that all input and output paths resolve within it (after symlink resolution). This prevents a malicious archive from extracting files outside the sandbox.

```csharp
private string ValidatePathWithinSandbox(string path, string sandboxDir)
{
    var resolved = Path.GetFullPath(path);
    if (!resolved.StartsWith(sandboxDir, StringComparison.Ordinal))
        throw new SecurityException(
            $"Path escapes sandbox: {path} resolves to {resolved}");
    return resolved;
}
```

**Path traversal on extraction (Zip Slip)**: When extracting archives, the wrapper validates every entry path before writing. Archive entries with names containing `..` or absolute paths are rejected. This prevents the classic Zip Slip attack where a malicious archive extracts files to arbitrary locations.

```csharp
// After extraction, verify no file escaped the sandbox
var extractedFiles = Directory.EnumerateFiles(outputDir, "*", SearchOption.AllDirectories);
foreach (var file in extractedFiles)
{
    var resolved = Path.GetFullPath(file);
    if (!resolved.StartsWith(outputDir, StringComparison.Ordinal))
    {
        // This should never happen if 7z respects paths, but defense in depth
        File.Delete(resolved);
        throw new SecurityException($"Extracted file escaped sandbox: {file}");
    }
}
```

**Hard timeouts**: The `Process` is given a hard timeout matching the job step's `timeout_seconds` configuration (default: 300 seconds). If the process does not exit within the timeout, it is killed via `Process.Kill(entireProcessTree: true)`. The step transitions to `TimedOut` state.

**Resource limits**: The 7z process inherits the container's cgroup resource limits (CPU and memory), which are set in the Docker deployment (Section 14.2). No additional per-process limits are applied in V1 — the container limits are the boundary. If 7z exceeds the container's memory limit, the OOM killer terminates it, and the step fails with a descriptive error.

**Stdout/stderr capture and sanitization**: The wrapper captures stdout and stderr for progress tracking and error reporting. Before logging, the output is sanitized to remove any content that might contain sensitive data (passwords are passed via arguments, but some 7z errors echo command-line context). Stderr is truncated to 4KB before being stored in the step's audit record.

**Binary integrity**: The `7z` binary is installed via `apt-get install p7zip-full` in the Dockerfile from the distro's official package repository. The binary path is hardcoded to `/usr/bin/7z` — no PATH lookup, no user-configurable binary location.

#### 8.1.3 Streaming Architecture

All compression operations use `Stream`-based pipelines. Files are never loaded fully into memory. This is critical for the 6–10 GB files Courier must handle.

For compression, source files are read through a `FileStream` with a configurable buffer size (default: 81920 bytes / 80KB) and fed into the compression stream. For decompression, the archive stream is read and entries are extracted directly to `FileStream` output targets.

The pipeline for a typical compress operation:

```
FileStream (source) → CompressionStream (e.g., ZipOutputStream) → FileStream (output archive)
```

Memory usage stays bounded regardless of file size. The buffer size is configurable per step for tuning throughput vs. memory on constrained environments.

#### 8.1.4 Progress Reporting

Compression steps report progress back to the Job Engine via the `IProgress<CompressionProgress>` callback. The engine uses this to:

- Update the `job_audit_log` with bytes-processed metrics
- Provide real-time progress data for the eventual V2 UI progress bars
- Detect stalls — if no progress is reported within the step's timeout window, the step is timed out

Progress is reported at the file level (after each file is compressed/extracted) and at the byte level within large individual files (every 10MB processed).

#### 8.1.5 Multi-File Handling

Compression steps accept multiple input sources via:

- **Explicit file list**: An array of absolute paths in the step configuration
- **Glob patterns**: Patterns like `*.csv` or `invoice_2026*.pdf` resolved against a base directory
- **JobContext references**: Upstream step outputs (e.g., `"0.downloaded_files"`) that resolve to one or more file paths

All matched files are included in the output archive. If no files match, the step fails with a descriptive error rather than creating an empty archive.

Decompression extracts all entries by default. A future enhancement could support selective extraction via filename patterns.

#### 8.1.6 Split Archives

For ZIP archives, Courier supports splitting output into multiple parts when the total size would exceed a configurable threshold. This is opt-in per step configuration:

```json
{
    "split_config": {
        "enabled": true,
        "max_part_size_mb": 500
    }
}
```

When enabled, the output is written as `archive.zip`, `archive.z01`, `archive.z02`, etc. Decompression of split archives is handled transparently — the step configuration points to the first part and SharpZipLib reassembles automatically.

Split archives are most useful when downstream systems have file size limits (e.g., email attachments in V2, or partner SFTP servers with quota restrictions).

#### 8.1.7 Temp File Strategy

Compression operations write output to the job execution's temp directory first (`/data/courier/temp/{executionId}/`). Once the archive is fully written and validated, it is atomically moved (or copied, if crossing filesystem boundaries) to its configured destination path.

This prevents partial archives from appearing at the destination if the process is interrupted mid-compression. The temp directory lifecycle is managed by the Job Engine as described in Section 5.14.

#### 8.1.8 Archive Extraction Safety

Extracting untrusted archives is a well-documented attack surface. Courier treats every archive as potentially hostile and enforces the following protections during decompression:

**Zip Slip (path traversal)**: Archive entries with names containing `..`, absolute paths, or paths that resolve outside the extraction sandbox are rejected before writing. This is checked both pre-extraction (by inspecting the entry name) and post-extraction (by resolving the written file's actual path via `Path.GetFullPath()` and comparing against the sandbox). See Section 8.1.2 for the 7z CLI-specific implementation; SharpZipLib and SharpCompress extractions use the same `ValidatePathWithinSandbox()` check.

**Symlink and hardlink attacks**: Archive formats (TAR, 7z) can contain symlink entries that point outside the extraction directory. On extraction, if a symlink target resolves outside the sandbox, the entry is skipped and logged as a security warning. Hardlinks to files outside the sandbox are similarly rejected. After extraction, a sweep verifies no symlinks escaped.

```csharp
private void ValidateExtractedEntry(string entryPath, string sandboxDir)
{
    var resolved = Path.GetFullPath(entryPath);
    if (!resolved.StartsWith(sandboxDir, StringComparison.Ordinal))
        throw new SecurityException($"Archive entry escapes sandbox: {entryPath}");

    // Check for symlinks pointing outside sandbox
    var fileInfo = new FileInfo(resolved);
    if (fileInfo.LinkTarget is not null)
    {
        var linkTarget = Path.GetFullPath(
            Path.Combine(Path.GetDirectoryName(resolved)!, fileInfo.LinkTarget));
        if (!linkTarget.StartsWith(sandboxDir, StringComparison.Ordinal))
        {
            File.Delete(resolved);
            throw new SecurityException(
                $"Symlink escapes sandbox: {entryPath} → {fileInfo.LinkTarget}");
        }
    }
}
```

**Decompression bomb protection**: A malicious archive can contain a small compressed file that expands to terabytes (e.g., a "zip bomb"). Courier enforces limits during extraction:

| Limit | Default | System Setting | Purpose |
|-------|---------|---------------|---------|
| Max total uncompressed bytes | 20 GB | `archive.max_uncompressed_bytes` | Prevents disk exhaustion. Set slightly above the largest expected legitimate archive (2× the max file size target). |
| Max file count | 10,000 | `archive.max_file_count` | Prevents inode exhaustion and excessive processing time. |
| Max compression ratio | 200:1 | `archive.max_compression_ratio` | A 1 MB archive expanding to 200 MB is normal; expanding to 200 GB is a bomb. Ratio is checked per-entry and across the entire archive. |
| Max nesting depth | 0 (no nested extraction) | `archive.max_nesting_depth` | Nested archives (a .zip inside a .zip) are not extracted recursively in V1. The inner archive is treated as a regular file. |
| Max single entry size | 10 GB | `archive.max_entry_size` | Prevents a single entry from consuming all available disk. |

Limits are checked during extraction, not just after. The extraction stream tracks cumulative bytes written and entry count, aborting immediately when any limit is exceeded:

```csharp
private void CheckExtractionLimits(
    long bytesWrittenSoFar, int filesExtractedSoFar,
    long entryCompressedSize, long entryUncompressedSize)
{
    if (bytesWrittenSoFar > _maxUncompressedBytes)
        throw new ArchiveSafetyException(
            $"Total uncompressed size exceeds limit ({_maxUncompressedBytes} bytes)");

    if (filesExtractedSoFar > _maxFileCount)
        throw new ArchiveSafetyException(
            $"File count exceeds limit ({_maxFileCount})");

    if (entryCompressedSize > 0)
    {
        var ratio = (double)entryUncompressedSize / entryCompressedSize;
        if (ratio > _maxCompressionRatio)
            throw new ArchiveSafetyException(
                $"Compression ratio {ratio:F1}:1 exceeds limit ({_maxCompressionRatio}:1)");
    }
}
```

#### 8.1.9 Archive Integrity Verification

After creating an archive, the engine optionally runs a validation pass to confirm integrity. This is enabled by default and can be disabled per step:

- **ZIP/7z**: Open the archive and verify all entries can be read without errors. For password-protected archives, verify decryption succeeds.
- **TAR/TAR.GZ/GZIP**: Verify the archive can be fully read without stream corruption.
- **RAR**: Not applicable (extraction-only).

Validation adds overhead proportional to archive size but catches corruption from disk errors or interrupted writes before the file is sent downstream.

#### 8.1.10 Archive Inspection

Archives can be inspected without extraction using the `InspectAsync` method on any compression provider. This returns:

```csharp
public record ArchiveContents(
    string Format,
    long TotalSizeCompressed,
    long TotalSizeUncompressed,
    double CompressionRatio,
    bool IsPasswordProtected,
    IReadOnlyList<ArchiveEntry> Entries);

public record ArchiveEntry(
    string Path,                       // Relative path within archive
    long CompressedSize,
    long UncompressedSize,
    DateTime LastModified,
    bool IsDirectory);
```

This supports two use cases: validation steps within a job pipeline (e.g., confirm an expected file exists in the archive before proceeding), and the frontend UI where users can preview archive contents when configuring jobs.



## 9. File Monitor System

The File Monitor system watches local and remote directories for file activity and triggers job executions in response. Monitors are first-class entities, independent of jobs, with their own configuration, lifecycle, and audit history.

### 9.1 Monitor Entity

A File Monitor is persisted in the database with the following configuration:

- **Name & description**: Human-readable identification
- **Watch target**: Local directory path or remote connection reference (SFTP/FTP) + remote path
- **Trigger events**: One or more of `FileCreated`, `FileModified`, `FileExists`
- **File patterns**: One or more glob patterns for filename filtering (e.g., `*.pgp`, `invoice_*.csv`)
- **Polling interval**: For remote monitors and as local fallback (default: 60 seconds)
- **Stability window**: Time to wait for file size to stabilize before considering a file "ready" (default: 5 seconds local, 1 poll interval remote)
- **Bound jobs/chains**: One or more Job or Chain references to trigger on detection
- **State**: `Active`, `Paused`, `Disabled`, or `Error`
- **Max consecutive failures**: Threshold before transitioning to `Error` state (default: 5)

### 9.2 Local Monitoring — FileSystemWatcher + Polling Fallback

For local and mounted directories, Courier uses .NET's `FileSystemWatcher` for near-instant event detection, combined with a periodic full-directory poll as the **authoritative source of truth**. The core invariant is: **the watcher is an optimization for latency; the poller is the guarantee of correctness.** If the watcher drops events, the poller catches them. If the watcher fails entirely, the poller continues alone.

**Design principle**: No file event is ever delivered by the watcher alone. Every detected file must survive reconciliation against the poller's full-directory scan to be considered actionable. The watcher's role is to reduce detection latency between poll intervals, not to serve as the primary detection mechanism.

**Known FileSystemWatcher problems and mitigations:**

- **Buffer overflow / dropped events**: Microsoft documents that the internal buffer can overflow under high file volume, silently dropping events with no notification to the application. Mitigation: set `InternalBufferSize` to 64KB (up from 8KB default), and handle the `Error` event to detect buffer overflows. Even with 64KB, a sustained burst of ~4,000+ events between drain cycles will overflow the buffer. The periodic poller (default: every 5 minutes) detects any files missed by the watcher.
- **Duplicate events**: A single file write can fire multiple `Changed` events. Mitigation: debounce events per file using a short window (500ms). Only the last event in the debounce window triggers processing.
- **Premature events**: Events fire when a file is first opened for writing, not when writing completes. Mitigation: file readiness detection (see 9.4).

**Poller as authoritative source**:

On each poll cycle, the poller performs a full `Directory.EnumerateFiles()` scan and compares the result against `monitor_directory_state`. Any file present in the directory but not yet processed (or modified since last processing) is treated as a new event, regardless of whether the watcher saw it. This means:

- If the watcher fires and the poller confirms the file, processing proceeds (typical fast path)
- If the watcher misses an event (buffer overflow, race condition, OS bug), the poller catches it within one poll interval
- If the watcher fires but the file disappears before the poller confirms it, the event is discarded (file was transient)

**Watcher health monitoring**:

Each local monitor tracks watcher health metrics, exposed via the Monitor API and stored in the monitor's state:

| Metric | Description | Source |
|--------|-------------|--------|
| `watcher_state` | `Active`, `Degraded`, `Disabled`, `Failed` | Runtime state |
| `last_overflow_at` | Timestamp of last `Error` event from `FileSystemWatcher` | `Error` event handler |
| `overflow_count_24h` | Number of buffer overflows in the last 24 hours | Rolling counter |
| `last_poll_at` | Timestamp of last completed full-directory poll | Poll completion |
| `last_poll_duration_ms` | Duration of the last full-directory scan | Timer |
| `last_poll_file_count` | Number of files found in last poll | Poll result |
| `watcher_events_since_last_poll` | Events received between polls (for overflow detection) | Counter reset per poll |

**Automatic watcher disable**:

If the watcher becomes a liability (constant overflows, excessive event volume), it is automatically disabled and the monitor falls back to polling-only:

- **Overflow threshold**: If `overflow_count_24h` exceeds `monitor.watcher_overflow_threshold` (default: 10), the watcher is disabled for that monitor and the state transitions to `Degraded`. The poller continues at normal interval. An audit event `WatcherAutoDisabled` is emitted with the overflow count and monitor ID.
- **File count threshold**: If `last_poll_file_count` exceeds `monitor.watcher_max_file_count` (default: 10,000), the watcher is disabled preemptively. Directories with very high file counts generate more filesystem events than the watcher can reliably buffer. The monitor operates in poll-only mode.
- **Manual re-enable**: Admins can re-enable the watcher via `POST /api/v1/monitors/{id}/reset-watcher` after addressing the root cause (e.g., reducing file count, increasing poll frequency). The monitor transitions back to `Active`.
- **Poll interval adjustment**: When the watcher is disabled (state: `Degraded`), the poll interval is automatically halved (e.g., 5 min → 2.5 min) to compensate for the loss of real-time detection. The original interval is restored when the watcher is re-enabled.

The watcher and polling fallback run as a combined system. If the `FileSystemWatcher` encounters an error (e.g., watched directory becomes unavailable), the system falls back to polling-only until the watcher can be re-established.

### 9.3 Remote Monitoring — Scheduled Polling

Remote SFTP and FTP directories are monitored via scheduled polling to respect connection limits and avoid holding persistent sessions:

1. On each poll interval, the monitor opens a short-lived connection using the configured connection reference
2. Lists the directory contents (filename, size, last modified)
3. Compares against the last known state stored in the `monitor_directory_state` table
4. Identifies new, modified, or existing files based on the configured trigger events
5. Disconnects immediately after listing

**Connection management**: Remote polls use the same Connection & Protocol Layer (Section 6) for SFTP/FTP access. Connections are opened and closed per poll — no persistent sessions. The polling interval should be set conservatively (minimum 30 seconds, recommended 60+ seconds) to avoid hitting server-side rate limits or max-session restrictions.

**State tracking**: After each poll, the full directory listing (filenames, sizes, timestamps) is persisted so the next poll can compute a diff. This state is stored in `monitor_directory_state` keyed by monitor ID.

### 9.4 File Readiness Detection

To prevent triggering on partially-written files, the monitor applies a stability check before considering a file "ready":

**Local files:**
1. When a file event is detected (or found during poll), record the file size
2. Wait for the configured stability window (default: 5 seconds)
3. Check the file size again
4. If the size has not changed, the file is considered ready and the trigger fires
5. If the size changed, reset the stability window and check again

**Remote files:**
1. On poll N, a new file is detected with size S
2. On poll N+1 (one interval later), the file is checked again
3. If the size matches and last-modified timestamp is unchanged, the file is considered ready
4. If either changed, it remains in a "pending readiness" state until the next poll confirms stability

This approach handles the common case of large files being uploaded to a watched directory over SFTP. The stability window is configurable per monitor for environments with very slow uploads.

### 9.5 File Pattern Filtering

Each monitor can define one or more glob patterns that filenames must match to trigger an event. Patterns are evaluated against the filename only (not the full path).

- Patterns use standard glob syntax: `*` matches any characters, `?` matches a single character
- Multiple patterns are OR'd — a file matching any pattern triggers the event
- An empty pattern list means all files match
- Patterns are case-insensitive on Windows, case-sensitive on Linux (matching OS filesystem behavior)

Examples: `*.pgp`, `invoice_*.csv`, `PARTNER_??_*.dat`

### 9.6 Monitor → Job Binding & Context Injection

When a monitor triggers, it creates a new execution of each bound Job or Chain and injects the file information into the `JobContext`:

```json
{
    "monitor.id": "a1b2c3d4-...",
    "monitor.name": "Partner Invoice Watch",
    "monitor.trigger_event": "FileCreated",
    "monitor.triggered_files": [
        {
            "path": "/data/incoming/invoice_2026-02-20.pgp",
            "size": 1048576,
            "last_modified": "2026-02-20T03:15:22Z"
        }
    ],
    "monitor.triggered_at": "2026-02-20T03:15:30Z"
}
```

Job steps can reference these context values in their configuration. For example, a `pgp.decrypt` step could reference `monitor.triggered_files[0].path` as its input file. This binding is configured in the job step definition using the same `JobContext` reference syntax described in Section 5.6.

If multiple files match the pattern in a single detection cycle, the behavior depends on monitor configuration:

- **Batch mode** (default): All matched files are injected into a single job execution as a list. Steps that support multi-file input process them all.
- **Individual mode**: A separate job execution is created for each matched file. Useful when each file needs independent processing and audit tracking.

### 9.7 Deduplication

The monitor maintains a `monitor_file_log` table that tracks which files have already triggered events to prevent duplicate job executions:

| Column         | Type      | Description                                   |
|----------------|-----------|-----------------------------------------------|
| `monitor_id`   | UUID      | FK to the monitor                             |
| `file_path`    | TEXT      | Full path of the detected file                |
| `file_size`    | BIGINT    | Size in bytes at time of trigger              |
| `file_hash`    | TEXT      | Optional SHA-256 hash for content-based dedup |
| `last_modified` | TIMESTAMP | File's last modified timestamp                |
| `triggered_at` | TIMESTAMP | When the trigger fired                        |
| `execution_id` | UUID      | FK to the job execution that was created      |

On each detection cycle, the monitor checks this log before triggering:

- **FileCreated**: Triggers only if the file path has never been seen, or if the file was previously processed and has since been deleted and recreated (detected by absence in a previous poll followed by presence)
- **FileModified**: Triggers if the file's size or last-modified timestamp differs from the last recorded values
- **FileExists**: Triggers on every detection cycle where the file is present (no dedup — useful for presence-check workflows)

The file log is pruned periodically to remove entries older than a configurable retention period (default: 30 days).

### 9.8 Monitor State Machine

```
    ┌────────┐
    │ Active │◄──── (activate / resume)
    └──┬──┬──┘
       │  │
       │  └───────────┐ (N consecutive failures)
       │              │
       │ (pause)  ┌───▼───┐
       │          │ Error │── (acknowledge & resume) ──► Active
       │          └───────┘
  ┌────▼────┐
  │ Paused  │
  └────┬────┘
       │ (disable)
  ┌────▼─────┐
  │ Disabled │
  └──────────┘
```

- **Active**: Monitor is running. Watching for events (local) or polling (remote).
- **Degraded**: Monitor is running in poll-only mode. FileSystemWatcher has been auto-disabled due to excessive overflows or high file count (Section 9.2). Functionally equivalent to Active but with higher detection latency (poll interval halved to compensate).
- **Paused**: Monitor is temporarily stopped. Configuration is retained. Can be resumed without re-creation.
- **Disabled**: Monitor is permanently stopped. Must be explicitly re-activated. Used for monitors that are no longer needed but whose configuration and history should be preserved.
- **Error**: Monitor encountered repeated failures (e.g., watched directory deleted, SFTP server unreachable for N consecutive polls). Requires manual acknowledgment to resume. An error event is emitted for V2 alerting.

### 9.9 Error Handling & Resilience

**Local monitor errors:**
- Watched directory deleted or unmounted: `FileSystemWatcher` raises an `Error` event. The monitor logs the error, disables the watcher (state: `Failed`), and operates in poll-only mode. If the directory remains unavailable for N consecutive polls, the monitor transitions to `Error`.
- Buffer overflow: `FileSystemWatcher` raises an `Error` event with `InternalBufferOverflowException`. The overflow is counted toward `overflow_count_24h`. If the threshold is exceeded, the watcher auto-disables (Section 9.2). The current poll cycle catches any missed files.
- Permission denied: Logged and counted as a failure toward the consecutive failure threshold.

**Remote monitor errors:**
- Connection refused / timeout: The poll is logged as a failure. The next poll interval retries with a fresh connection.
- Authentication failure: Treated as a critical error — single occurrence transitions the monitor to `Error` since retrying won't help without credential changes.
- Directory not found: Logged as a failure and counted toward the threshold.

All errors are recorded in the monitor's audit log with timestamps, error details, and the current consecutive failure count.

---

## 10. API Design

Courier exposes a RESTful API with an OpenAPI/Swagger specification generated via Swashbuckle. All endpoints are versioned under `/api/v1/` and return JSON. The API is the sole interface between the Next.js frontend and the .NET backend — there are no server-rendered views or direct database access from the frontend.

### 10.1 General Conventions

**Base URL**: `/api/v1`

**Authentication**: All endpoints require a valid Azure AD/Entra ID bearer token in the `Authorization` header. See Section 12 (Security) for details.

**Content type**: `application/json` for all request and response bodies.

**Standard Response Model**: Every API response — without exception — is wrapped in a standard envelope. There are no raw JSON objects, bare arrays, or 204 No Content responses. Every endpoint returns a body that conforms to one of the generic response types below.

**C# response types**:

```csharp
// Base envelope — present on every response
public record ApiResponse
{
    public ApiError? Error { get; init; }
    public DateTimeOffset Timestamp { get; init; } = DateTimeOffset.UtcNow;
    public bool Success => Error is null;
}

// Single item response (GET by ID, POST create, PUT update, action endpoints)
public record ApiResponse<T> : ApiResponse
{
    public T? Data { get; init; }
}

// Paginated list response (GET list endpoints)
public record PagedApiResponse<T> : ApiResponse
{
    public IReadOnlyList<T> Data { get; init; } = [];
    public PaginationMeta Pagination { get; init; } = default!;
}

public record PaginationMeta(
    int Page,
    int PageSize,
    int TotalCount,
    int TotalPages);

// Error detail
public record ApiError(
    int Code,                                 // Numeric error code (see Error Code Catalog)
    string SystemMessage,                     // Standardized message for this code (always the same)
    string Message,                           // Human-readable, context-specific message
    IReadOnlyList<FieldError>? Details = null);

public record FieldError(
    string Field,
    string Message);
```

**Error code design**: The `code` is a numeric identifier. The `systemMessage` is the canonical, standardized description for that code — it never varies. The `message` is a human-readable explanation specific to the current occurrence. Frontend consumers can switch on `code` for programmatic handling and display `message` to users. Developers and logging systems use `systemMessage` for consistent categorization.

**Error Code Catalog**:

| Code | HTTP | System Message | Description / When Used |
|------|------|----------------|-------------------------|
| **1000–1999: General** | | | |
| 1000 | 400 | `Validation failed` | Request body or query parameter validation failed |
| 1001 | 400 | `Invalid request format` | Malformed JSON, missing Content-Type, etc. |
| 1002 | 400 | `Invalid query parameter` | Unrecognized or malformed query parameter |
| 1003 | 400 | `Invalid sort field` | Sort requested on non-sortable field |
| 1004 | 400 | `Page out of range` | Requested page exceeds total pages |
| 1010 | 401 | `Authentication required` | Missing or invalid bearer token |
| 1011 | 401 | `Token expired` | Bearer token has expired |
| 1020 | 403 | `Insufficient permissions` | User lacks required role/permission |
| 1030 | 404 | `Resource not found` | Entity does not exist or is soft-deleted |
| 1040 | 405 | `Method not allowed` | HTTP method not supported on endpoint |
| 1050 | 409 | `State conflict` | Operation invalid for current entity state |
| 1051 | 409 | `Dependency conflict` | Operation blocked by dependency constraint |
| 1052 | 409 | `Duplicate resource` | Resource with same unique key already exists |
| 1060 | 429 | `Rate limit exceeded` | Too many requests |
| 1070 | 422 | `Unprocessable entity` | Syntactically valid but semantically invalid |
| 1099 | 500 | `Internal server error` | Unexpected server error |
| **2000–2999: Job System** | | | |
| 2000 | 409 | `Job not enabled` | Cannot execute a disabled job |
| 2001 | 429 | `Concurrency limit reached` | Global job execution limit exceeded |
| 2002 | 409 | `Execution not cancellable` | Execution is not in a cancellable state |
| 2003 | 409 | `Execution not pausable` | Execution is not in a pausable state |
| 2004 | 409 | `Execution not resumable` | Execution is not in a resumable state |
| 2005 | 409 | `Circular dependency detected` | Job dependency would create a cycle |
| 2006 | 409 | `Self dependency` | Job cannot depend on itself |
| 2007 | 409 | `Duplicate dependency` | Dependency between these jobs already exists |
| 2010 | 400 | `Invalid step type` | Step type key is not registered |
| 2011 | 400 | `Invalid step configuration` | Step configuration doesn't match type schema |
| 2012 | 400 | `Invalid cron expression` | Cron expression cannot be parsed |
| 2020 | 409 | `Chain not enabled` | Cannot execute a disabled chain |
| 2021 | 409 | `Chain member conflict` | Job referenced by chain member not found or deleted |
| **3000–3999: Connections** | | | |
| 3000 | 422 | `Connection test failed` | Test connection could not connect |
| 3001 | 422 | `Authentication failed` | Connection test: credentials rejected |
| 3002 | 422 | `Host key mismatch` | SFTP host key doesn't match stored fingerprint |
| 3003 | 422 | `Host unreachable` | Connection test: host refused or timed out |
| 3004 | 403 | `Insecure host key policy requires admin` | Only Admin role can set host_key_policy to always_trust |
| 3005 | 403 | `Insecure host key policy not allowed in FIPS mode` | AlwaysTrust blocked when FIPS mode enabled |
| 3006 | 403 | `Insecure TLS policy requires admin` | Only Admin role can set tls_cert_policy to insecure |
| 3007 | 403 | `Insecure TLS policy not allowed in FIPS mode` | Insecure cert policy blocked when FIPS mode enabled |
| 3008 | 403 | `Insecure trust policy blocked in production` | AlwaysTrust or Insecure cert policy blocked by production setting |
| 3010 | 409 | `Connection in use` | Cannot delete connection referenced by active jobs/monitors |
| 3011 | 400 | `Invalid protocol configuration` | SSH-specific config on FTP connection or vice versa |
| 3012 | 403 | `FIPS override requires admin` | Only Admin role can enable fips_override on connections |
| **4000–4999: Key Store** | | | |
| 4000 | 400 | `Key import failed` | Key file could not be parsed or is corrupt |
| 4001 | 400 | `Invalid passphrase` | Passphrase does not unlock private key |
| 4002 | 409 | `Key fingerprint exists` | Key with this fingerprint already imported |
| 4003 | 409 | `Key not active` | Operation requires key in Active status |
| 4004 | 409 | `Key already revoked` | Cannot change status of a revoked key |
| 4005 | 409 | `Key in use` | Cannot delete key referenced by active jobs |
| 4010 | 403 | `Private key export denied` | Private key export requires explicit confirmation |
| 4011 | 400 | `Algorithm not available in FIPS mode` | Key generation requested with non-FIPS algorithm while FIPS enabled |
| 4012 | 403 | `Share links disabled` | Public key share links are not enabled in system settings |
| 4013 | 404 | `Share link expired or revoked` | The share link token is invalid, expired, or has been revoked |
| **5000–5999: File Monitors** | | | |
| 5000 | 409 | `Monitor not active` | Operation requires monitor in Active state |
| 5001 | 409 | `Monitor not in error` | Acknowledge requires monitor in Error state |
| 5002 | 409 | `Monitor already active` | Activate called on already-active monitor |
| 5003 | 400 | `Invalid watch target` | Remote watch target references invalid connection |
| 5004 | 400 | `Invalid polling interval` | Polling interval below minimum (30 seconds) |
| **6000–6999: Tags & Cross-cutting** | | | |
| 6000 | 409 | `Duplicate tag name` | Tag with this name already exists |
| 6001 | 400 | `Invalid entity type` | Entity type not in taggable entity list |
| 6002 | 404 | `Tag assignment not found` | Tag is not assigned to the specified entity |
| **7000–7999: File Operations** | | | |
| 7000 | 422 | `Compression failed` | 7z/ZIP operation failed — see error details |
| 7001 | 422 | `Unsafe filename in archive operation` | Filename contains path traversal, shell metacharacters, or control characters |
| 7002 | 422 | `Archive path escapes sandbox` | Extracted file would resolve outside the job's temp directory |
| 7003 | 408 | `Archive operation timed out` | 7z process exceeded step timeout and was killed |
| 7004 | 422 | `Decompression bomb detected` | Archive exceeds uncompressed size, file count, or compression ratio limits |
| 7005 | 422 | `Symlink escapes sandbox` | Archive contains a symlink pointing outside the extraction directory |

**C# error code constants**:

```csharp
public static class ErrorCodes
{
    // General
    public const int ValidationFailed = 1000;
    public const int InvalidRequestFormat = 1001;
    public const int InvalidQueryParameter = 1002;
    public const int InvalidSortField = 1003;
    public const int PageOutOfRange = 1004;
    public const int AuthenticationRequired = 1010;
    public const int TokenExpired = 1011;
    public const int InsufficientPermissions = 1020;
    public const int ResourceNotFound = 1030;
    public const int MethodNotAllowed = 1040;
    public const int StateConflict = 1050;
    public const int DependencyConflict = 1051;
    public const int DuplicateResource = 1052;
    public const int RateLimitExceeded = 1060;
    public const int UnprocessableEntity = 1070;
    public const int InternalServerError = 1099;

    // Job System
    public const int JobNotEnabled = 2000;
    public const int ConcurrencyLimitReached = 2001;
    public const int ExecutionNotCancellable = 2002;
    public const int ExecutionNotPausable = 2003;
    public const int ExecutionNotResumable = 2004;
    public const int CircularDependency = 2005;
    public const int SelfDependency = 2006;
    public const int DuplicateDependency = 2007;
    public const int InvalidStepType = 2010;
    public const int InvalidStepConfiguration = 2011;
    public const int InvalidCronExpression = 2012;
    public const int ChainNotEnabled = 2020;
    public const int ChainMemberConflict = 2021;

    // Connections
    public const int ConnectionTestFailed = 3000;
    public const int ConnectionAuthFailed = 3001;
    public const int HostKeyMismatch = 3002;
    public const int HostUnreachable = 3003;
    public const int ConnectionInUse = 3010;
    public const int InvalidProtocolConfig = 3011;
    public const int FipsOverrideRequiresAdmin = 3012;
    public const int InsecureHostKeyPolicyRequiresAdmin = 3004;
    public const int InsecureHostKeyPolicyBlockedByFips = 3005;
    public const int InsecureTlsPolicyRequiresAdmin = 3006;
    public const int InsecureTlsPolicyBlockedByFips = 3007;
    public const int InsecureTrustPolicyBlockedInProd = 3008;

    // Key Store
    public const int KeyImportFailed = 4000;
    public const int InvalidPassphrase = 4001;
    public const int KeyFingerprintExists = 4002;
    public const int KeyNotActive = 4003;
    public const int KeyAlreadyRevoked = 4004;
    public const int KeyInUse = 4005;
    public const int PrivateKeyExportDenied = 4010;
    public const int AlgorithmNotFipsApproved = 4011;
    public const int ShareLinksDisabled = 4012;
    public const int ShareLinkExpiredOrRevoked = 4013;

    // File Monitors
    public const int MonitorNotActive = 5000;
    public const int MonitorNotInError = 5001;
    public const int MonitorAlreadyActive = 5002;
    public const int InvalidWatchTarget = 5003;
    public const int InvalidPollingInterval = 5004;

    // Tags
    public const int DuplicateTagName = 6000;
    public const int InvalidEntityType = 6001;
    public const int TagAssignmentNotFound = 6002;

    // File Operations
    public const int CompressionFailed = 7000;
    public const int UnsafeFilename = 7001;
    public const int ArchivePathEscapesSandbox = 7002;
    public const int ArchiveOperationTimedOut = 7003;
    public const int DecompressionBombDetected = 7004;
    public const int SymlinkEscapesSandbox = 7005;
}

// Lookup: code → system message (always the same)
public static class ErrorMessages
{
    private static readonly Dictionary<int, string> SystemMessages = new()
    {
        [1000] = "Validation failed",
        [1001] = "Invalid request format",
        [1010] = "Authentication required",
        [1030] = "Resource not found",
        [1050] = "State conflict",
        [1051] = "Dependency conflict",
        [1099] = "Internal server error",
        [2001] = "Concurrency limit reached",
        [2005] = "Circular dependency detected",
        // ... all codes registered
    };

    public static string GetSystemMessage(int code)
        => SystemMessages.TryGetValue(code, out var msg) ? msg : "Unknown error";

    public static ApiError Create(int code, string message, IReadOnlyList<FieldError>? details = null)
        => new(code, GetSystemMessage(code), message, details);
}
```

**Every response pattern**:

```json
// 1. Single item (GET /api/v1/jobs/{id}, POST create, PUT update)
// HTTP 200 or 201
{
    "data": { "id": "a1b2c3d4-...", "name": "Daily Download", ... },
    "error": null,
    "success": true,
    "timestamp": "2026-02-21T12:00:00Z"
}

// 2. Paginated list (GET /api/v1/jobs)
// HTTP 200
{
    "data": [ { ... }, { ... } ],
    "pagination": {
        "page": 1,
        "pageSize": 25,
        "totalCount": 142,
        "totalPages": 6
    },
    "error": null,
    "success": true,
    "timestamp": "2026-02-21T12:00:00Z"
}

// 3. Action result (POST execute, POST cancel, POST pause, POST test, POST retire, etc.)
// HTTP 200
{
    "data": {
        "executionId": "f1e2d3c4-...",
        "state": "queued",
        "message": "Job execution queued successfully."
    },
    "error": null,
    "success": true,
    "timestamp": "2026-02-21T12:00:00Z"
}

// 4. Delete confirmation (DELETE /api/v1/jobs/{id})
// HTTP 200
{
    "data": {
        "id": "a1b2c3d4-...",
        "deleted": true,
        "deletedAt": "2026-02-21T12:00:00Z"
    },
    "error": null,
    "success": true,
    "timestamp": "2026-02-21T12:00:00Z"
}

// 5. Bulk operation result (POST /api/v1/tags/assign)
// HTTP 200
{
    "data": {
        "assignedCount": 3,
        "assignments": [
            { "tagId": "e5f6a7b8-...", "entityType": "job", "entityId": "a1b2c3d4-...", "assigned": true },
            { "tagId": "e5f6a7b8-...", "entityType": "connection", "entityId": "b2c3d4e5-...", "assigned": true },
            { "tagId": "f6a7b8c9-...", "entityType": "job", "entityId": "a1b2c3d4-...", "assigned": true }
        ]
    },
    "error": null,
    "success": true,
    "timestamp": "2026-02-21T12:00:00Z"
}

// 6. Validation error (any endpoint)
// HTTP 400
{
    "data": null,
    "error": {
        "code": 1000,
        "systemMessage": "Validation failed",
        "message": "One or more validation errors occurred.",
        "details": [
            { "field": "name", "message": "Name must not be empty." },
            { "field": "steps", "message": "A job must have at least one step." }
        ]
    },
    "success": false,
    "timestamp": "2026-02-21T12:00:00Z"
}

// 7. Not found (any endpoint)
// HTTP 404
{
    "data": null,
    "error": {
        "code": 1030,
        "systemMessage": "Resource not found",
        "message": "Job with ID 'a1b2c3d4-...' was not found."
    },
    "success": false,
    "timestamp": "2026-02-21T12:00:00Z"
}

// 8. State conflict (e.g., cancelling an already-completed job)
// HTTP 409
{
    "data": null,
    "error": {
        "code": 2002,
        "systemMessage": "Execution not cancellable",
        "message": "Cannot cancel execution 'f1e2d3c4-...': current state is 'completed'."
    },
    "success": false,
    "timestamp": "2026-02-21T12:00:00Z"
}

// 9. Internal error (unexpected failures)
// HTTP 500
{
    "data": null,
    "error": {
        "code": 1099,
        "systemMessage": "Internal server error",
        "message": "An unexpected error occurred. Reference: err_a1b2c3d4"
    },
    "success": false,
    "timestamp": "2026-02-21T12:00:00Z"
}
```

**Enforcement**: A global ASP.NET result filter wraps all controller return values in the appropriate `ApiResponse<T>` or `PagedApiResponse<T>` envelope. Unhandled exceptions are caught by a global exception handler middleware that returns an `ApiResponse` with the error populated. This ensures no endpoint can accidentally return a raw object.

```csharp
// Global exception handler — guarantees envelope even on unhandled errors
public class ApiExceptionMiddleware : IMiddleware
{
    public async Task InvokeAsync(HttpContext context, RequestDelegate next)
    {
        try
        {
            await next(context);
        }
        catch (Exception ex)
        {
            var reference = $"err_{Guid.NewGuid():N[..8]}";
            _logger.LogError(ex, "Unhandled exception. Reference: {Reference}", reference);

            context.Response.StatusCode = 500;
            await context.Response.WriteAsJsonAsync(new ApiResponse
            {
                Error = ErrorMessages.Create(
                    ErrorCodes.InternalServerError,
                    $"An unexpected error occurred. Reference: {reference}")
            });
        }
    }
}
```

**Pagination**: Offset-based on all list endpoints. Query parameters: `page` (default: 1), `pageSize` (default: 25, max: 100). Response includes `totalCount` and `totalPages`.

**Sorting**: Query parameter `sort` with format `field:asc` or `field:desc` (e.g., `sort=name:asc`). Default sort is `created_at:desc` on all list endpoints.

**Filtering**: Resource-specific query parameters documented per endpoint (e.g., `state=running`, `protocol=sftp`).

**Soft delete behavior**: Deleted resources are excluded from list responses. GET by ID returns 404 for soft-deleted resources. A `GET /api/v1/{resource}?includeDeleted=true` query parameter is available for admin recovery.

**Error codes**: All errors use numeric codes with standardized system messages. See the full Error Code Catalog in Section 10.1 above. Ranges: 1000–1999 (general), 2000–2999 (jobs), 3000–3999 (connections), 4000–4999 (keys), 5000–5999 (monitors), 6000–6999 (tags).

### 10.2 Jobs API

#### Endpoints

```
GET    /api/v1/jobs                          List jobs
POST   /api/v1/jobs                          Create a job
GET    /api/v1/jobs/{id}                     Get job details
PUT    /api/v1/jobs/{id}                     Update a job (creates new version)
DELETE /api/v1/jobs/{id}                     Soft-delete a job

GET    /api/v1/jobs/{id}/steps               List steps for a job
PUT    /api/v1/jobs/{id}/steps               Replace all steps (atomic)

GET    /api/v1/jobs/{id}/versions            List job versions
GET    /api/v1/jobs/{id}/versions/{version}  Get specific version snapshot

POST   /api/v1/jobs/{id}/execute             Trigger manual execution
GET    /api/v1/jobs/{id}/executions          List executions for a job
GET    /api/v1/jobs/{id}/executions/{execId} Get execution details with step results
POST   /api/v1/jobs/{id}/executions/{execId}/cancel   Cancel a running execution
POST   /api/v1/jobs/{id}/executions/{execId}/pause    Pause a running execution
POST   /api/v1/jobs/{id}/executions/{execId}/resume   Resume a paused execution

GET    /api/v1/jobs/{id}/schedules           List schedules for a job
POST   /api/v1/jobs/{id}/schedules           Add a schedule
PUT    /api/v1/jobs/{id}/schedules/{schedId} Update a schedule
DELETE /api/v1/jobs/{id}/schedules/{schedId} Delete a schedule

GET    /api/v1/jobs/{id}/dependencies        List upstream dependencies
POST   /api/v1/jobs/{id}/dependencies        Add a dependency
DELETE /api/v1/jobs/{id}/dependencies/{depId} Remove a dependency
```

#### Filters (GET /api/v1/jobs)

| Parameter    | Type   | Description |
|-------------|--------|-------------|
| `search`    | string | Name/description substring search |
| `isEnabled` | bool   | Filter by enabled state |
| `tag`       | string | Filter by tag name (repeatable for OR) |
| `stepType`  | string | Filter by step type key contained in job |

#### Create/Update Request Body

```json
{
    "name": "Daily Partner Invoice Download",
    "description": "Downloads encrypted invoices from Partner SFTP",
    "isEnabled": true,
    "failurePolicy": {
        "type": "retry_step",
        "maxRetries": 3,
        "backoffBaseSeconds": 2,
        "backoffMaxSeconds": 120
    },
    "steps": [
        {
            "name": "Download from Partner",
            "typeKey": "sftp.download",
            "configuration": {
                "connectionId": "a1b2c3d4-...",
                "remotePath": "/outbound/invoices/",
                "filePattern": "*.pgp",
                "localPath": "${job.temp_dir}"
            },
            "timeoutSeconds": 600
        },
        {
            "name": "Decrypt PGP files",
            "typeKey": "pgp.decrypt",
            "configuration": {
                "inputPath": "${steps[0].downloaded_files}",
                "keyId": "b2c3d4e5-...",
                "outputPath": "${job.temp_dir}/decrypted/"
            },
            "timeoutSeconds": 300
        }
    ]
}
```

#### Execute Response

```json
{
    "data": {
        "executionId": "f1e2d3c4-...",
        "jobId": "a1b2c3d4-...",
        "state": "queued",
        "triggeredBy": "manual:user@company.com",
        "queuedAt": "2026-02-21T12:00:00Z"
    },
    "error": null,
    "success": true,
    "timestamp": "2026-02-21T12:00:00Z"
}
```

### 10.3 Job Chains API

```
GET    /api/v1/chains                         List chains
POST   /api/v1/chains                         Create a chain
GET    /api/v1/chains/{id}                    Get chain details with members
PUT    /api/v1/chains/{id}                    Update a chain
DELETE /api/v1/chains/{id}                    Soft-delete a chain

PUT    /api/v1/chains/{id}/members            Replace all members (atomic)

POST   /api/v1/chains/{id}/execute            Trigger manual execution
GET    /api/v1/chains/{id}/executions         List chain executions
GET    /api/v1/chains/{id}/executions/{execId} Get chain execution with job results

GET    /api/v1/chains/{id}/schedules          List schedules
POST   /api/v1/chains/{id}/schedules          Add a schedule
PUT    /api/v1/chains/{id}/schedules/{schedId} Update a schedule
DELETE /api/v1/chains/{id}/schedules/{schedId} Delete a schedule
```

#### Create/Update Request Body

```json
{
    "name": "Daily Partner Invoice Processing",
    "description": "Full pipeline: download, decrypt, decompress, archive",
    "isEnabled": true,
    "members": [
        {
            "jobId": "a1b2c3d4-...",
            "executionOrder": 0,
            "dependsOnMemberIndex": null,
            "runOnUpstreamFailure": false
        },
        {
            "jobId": "b2c3d4e5-...",
            "executionOrder": 1,
            "dependsOnMemberIndex": 0,
            "runOnUpstreamFailure": false
        }
    ]
}
```

Note: `dependsOnMemberIndex` references the index within the `members` array (not a database ID). The server resolves this to `depends_on_member_id` after persisting the members.

### 10.4 Connections API

```
GET    /api/v1/connections                    List connections
POST   /api/v1/connections                    Create a connection
GET    /api/v1/connections/{id}               Get connection details
PUT    /api/v1/connections/{id}               Update a connection
DELETE /api/v1/connections/{id}               Soft-delete a connection
POST   /api/v1/connections/{id}/test          Test connection (connect, auth, list root)
```

#### Filters (GET /api/v1/connections)

| Parameter   | Type   | Description |
|------------|--------|-------------|
| `search`   | string | Name/host substring search |
| `protocol` | string | Filter by protocol (sftp, ftp, ftps) |
| `group`    | string | Filter by group name |
| `status`   | string | Filter by status (active, disabled) |
| `tag`      | string | Filter by tag name |

#### Create/Update Request Body

```json
{
    "name": "Partner SFTP - Production",
    "group": "Partner Integrations",
    "protocol": "sftp",
    "host": "sftp.partner.com",
    "port": 22,
    "authMethod": "password_and_ssh_key",
    "username": "courier_svc",
    "password": "s3cret",
    "sshKeyId": "c3d4e5f6-...",
    "hostKeyPolicy": "trust_on_first_use",
    "connectTimeoutSec": 30,
    "operationTimeoutSec": 300,
    "keepaliveIntervalSec": 60,
    "transportRetries": 2,
    "fipsOverride": false,
    "notes": "Contact: partner-support@partner.com"
}
```

**Security note**: The `password` field is accepted on create/update but never returned in GET responses. GET responses include `hasPassword: true/false` instead.

#### Test Connection Response

```json
{
    "data": {
        "connected": true,
        "latencyMs": 142,
        "serverBanner": "OpenSSH_8.9p1 Ubuntu-3ubuntu0.4",
        "supportedAlgorithms": {
            "keyExchange": ["curve25519-sha256", "ecdh-sha2-nistp256"],
            "encryption": ["aes256-gcm@openssh.com", "chacha20-poly1305@openssh.com"],
            "mac": ["hmac-sha2-256-etm@openssh.com"],
            "hostKey": ["ssh-ed25519", "rsa-sha2-512"]
        },
        "testedAt": "2026-02-21T12:00:00Z"
    },
    "error": null,
    "success": true,
    "timestamp": "2026-02-21T12:00:00Z"
}
```

### 10.5 PGP Keys API

```
GET    /api/v1/pgp-keys                       List PGP keys
POST   /api/v1/pgp-keys/generate              Generate a new key pair
POST   /api/v1/pgp-keys/import                Import a key (multipart/form-data)
GET    /api/v1/pgp-keys/{id}                  Get key metadata
PUT    /api/v1/pgp-keys/{id}                  Update key metadata (name, purpose)
DELETE /api/v1/pgp-keys/{id}                  Soft-delete (purges key material)

GET    /api/v1/pgp-keys/{id}/export/public    Export public key (armored or binary)
POST   /api/v1/pgp-keys/{id}/export/private   Export private key (requires confirmation)

POST   /api/v1/pgp-keys/{id}/share            Generate shareable public key link (Admin only)
DELETE /api/v1/pgp-keys/{id}/share/{token}     Revoke a shareable link (Admin only)
GET    /api/v1/pgp-keys/shared/{token}         Download public key via shareable link (no auth)

POST   /api/v1/pgp-keys/{id}/retire           Retire a key
POST   /api/v1/pgp-keys/{id}/revoke           Revoke a key (terminal)
POST   /api/v1/pgp-keys/{id}/activate         Re-activate a retired key
```

#### Filters (GET /api/v1/pgp-keys)

| Parameter   | Type   | Description |
|------------|--------|-------------|
| `search`   | string | Name/fingerprint substring search |
| `status`   | string | Filter by status (active, expiring, retired, revoked) |
| `keyType`  | string | Filter by type (public_only, key_pair) |
| `algorithm`| string | Filter by algorithm |
| `tag`      | string | Filter by tag name |

#### Generate Request Body

```json
{
    "name": "Partner A - Encryption Key 2026",
    "algorithm": "rsa_4096",
    "userId": "courier@company.com",
    "passphrase": "optional-passphrase",
    "expiresAt": "2027-02-21T00:00:00Z",
    "purpose": "Encrypting outbound files to Partner A"
}
```

#### Import Request

`POST /api/v1/pgp-keys/import` with `multipart/form-data`:

| Field | Type | Description |
|-------|------|-------------|
| `file` | file | .asc, .pgp, .gpg, or .kbx file |
| `name` | string | Human-readable label |
| `passphrase` | string | Passphrase for private key (if applicable) |
| `purpose` | string | Optional description |

#### Key Response (GET)

```json
{
    "data": {
        "id": "b2c3d4e5-...",
        "name": "Partner A - Encryption Key 2026",
        "fingerprint": "A1B2C3D4E5F6...",
        "shortKeyId": "E5F6A1B2C3D4E5F6",
        "algorithm": "rsa_4096",
        "keyType": "key_pair",
        "status": "active",
        "hasPrivateKey": true,
        "hasPassphrase": true,
        "expiresAt": "2027-02-21T00:00:00Z",
        "successorKeyId": null,
        "purpose": "Encrypting outbound files to Partner A",
        "createdBy": "user@company.com",
        "createdAt": "2026-02-21T12:00:00Z",
        "tags": [{"name": "partner-a", "color": "#FF5733"}]
    },
    "error": null,
    "success": true,
    "timestamp": "2026-02-21T12:00:00Z"
}
```

### 10.6 SSH Keys API

```
GET    /api/v1/ssh-keys                       List SSH keys
POST   /api/v1/ssh-keys/generate              Generate a new key pair
POST   /api/v1/ssh-keys/import                Import a key (multipart/form-data)
GET    /api/v1/ssh-keys/{id}                  Get key metadata
PUT    /api/v1/ssh-keys/{id}                  Update key metadata
DELETE /api/v1/ssh-keys/{id}                  Soft-delete

GET    /api/v1/ssh-keys/{id}/export/public    Export public key (OpenSSH format)

POST   /api/v1/ssh-keys/{id}/share            Generate shareable public key link (Admin only)
DELETE /api/v1/ssh-keys/{id}/share/{token}     Revoke a shareable link (Admin only)
GET    /api/v1/ssh-keys/shared/{token}         Download public key via shareable link (no auth)

POST   /api/v1/ssh-keys/{id}/retire           Retire a key
POST   /api/v1/ssh-keys/{id}/activate         Re-activate a retired key
```

The SSH Keys API mirrors the PGP Keys API in structure. Key differences: no private key export endpoint (SSH private keys are never exported from Courier), and the generate endpoint supports `rsa_2048`, `rsa_4096`, and `ed25519` key types.

### 10.7 File Monitors API

```
GET    /api/v1/monitors                       List monitors
POST   /api/v1/monitors                       Create a monitor
GET    /api/v1/monitors/{id}                  Get monitor details
PUT    /api/v1/monitors/{id}                  Update a monitor
DELETE /api/v1/monitors/{id}                  Soft-delete a monitor

POST   /api/v1/monitors/{id}/activate         Activate / resume
POST   /api/v1/monitors/{id}/pause            Pause
POST   /api/v1/monitors/{id}/disable          Disable
POST   /api/v1/monitors/{id}/acknowledge      Acknowledge error and resume
POST   /api/v1/monitors/{id}/reset-watcher     Re-enable FileSystemWatcher after auto-disable (Admin)

GET    /api/v1/monitors/{id}/file-log         List triggered files (paginated)
GET    /api/v1/monitors/{id}/executions       List job executions triggered by this monitor
```

#### Filters (GET /api/v1/monitors)

| Parameter   | Type   | Description |
|------------|--------|-------------|
| `search`   | string | Name/description substring search |
| `state`    | string | Filter by state (active, paused, disabled, error) |
| `targetType`| string | Filter by watch target type (local, remote) |
| `tag`      | string | Filter by tag name |

#### Create/Update Request Body

```json
{
    "name": "Partner Invoice Watch",
    "description": "Watches for new PGP files from Partner SFTP",
    "watchTarget": {
        "type": "remote",
        "path": "/outbound/invoices/",
        "connectionId": "a1b2c3d4-..."
    },
    "triggerEvents": ["FileCreated"],
    "filePatterns": ["*.pgp", "*.gpg"],
    "pollingIntervalSec": 60,
    "stabilityWindowSec": 10,
    "batchMode": false,
    "maxConsecutiveFailures": 5,
    "boundJobs": [
        { "jobId": "c3d4e5f6-..." }
    ],
    "boundChains": [
        { "chainId": "d4e5f6a7-..." }
    ]
}
```

### 10.8 Tags API

```
GET    /api/v1/tags                           List all tags
POST   /api/v1/tags                           Create a tag
GET    /api/v1/tags/{id}                      Get tag details
PUT    /api/v1/tags/{id}                      Update a tag
DELETE /api/v1/tags/{id}                      Soft-delete a tag

GET    /api/v1/tags/{id}/entities             List all entities with this tag
POST   /api/v1/tags/assign                    Assign tag(s) to entity/entities (bulk)
POST   /api/v1/tags/unassign                  Remove tag(s) from entity/entities (bulk)
```

#### Filters (GET /api/v1/tags)

| Parameter  | Type   | Description |
|-----------|--------|-------------|
| `search`  | string | Name substring search |
| `category`| string | Filter by category |

#### Create Request Body

```json
{
    "name": "partner-acme",
    "color": "#FF5733",
    "category": "Partner",
    "description": "Resources related to ACME Corp integration"
}
```

#### Bulk Assign Request Body

```json
{
    "assignments": [
        { "tagId": "e5f6a7b8-...", "entityType": "job", "entityId": "a1b2c3d4-..." },
        { "tagId": "e5f6a7b8-...", "entityType": "connection", "entityId": "b2c3d4e5-..." },
        { "tagId": "f6a7b8c9-...", "entityType": "job", "entityId": "a1b2c3d4-..." }
    ]
}
```

### 10.9 Audit Log API

```
GET    /api/v1/audit-log                      Query audit log entries
GET    /api/v1/audit-log/entity/{type}/{id}   Get audit history for a specific entity
```

The audit log is read-only — entries are created internally by the system and cannot be modified or deleted via the API.

#### Filters (GET /api/v1/audit-log)

| Parameter    | Type   | Description |
|-------------|--------|-------------|
| `entityType`| string | Filter by entity type |
| `entityId`  | string | Filter by specific entity ID |
| `operation` | string | Filter by operation name |
| `performedBy`| string | Filter by user |
| `from`      | datetime | Start of time range (inclusive) |
| `to`        | datetime | End of time range (exclusive) |

#### Audit Entry Response

```json
{
    "data": [
        {
            "id": "a7b8c9d0-...",
            "entityType": "connection",
            "entityId": "a1b2c3d4-...",
            "operation": "Connected",
            "performedBy": "system",
            "performedAt": "2026-02-21T12:00:00Z",
            "details": {
                "host": "sftp.partner.com",
                "latencyMs": 142,
                "protocol": "sftp"
            }
        }
    ],
    "pagination": { "page": 1, "pageSize": 25, "totalCount": 89, "totalPages": 4 },
    "error": null,
    "success": true,
    "timestamp": "2026-02-21T12:00:05Z"
}
```

### 10.10 Authentication API

All auth endpoints use `[AllowAnonymous]` (login, refresh) or `[Authorize]` (me, logout, change-password).

```
POST   /api/v1/auth/login                     Authenticate with username/password
POST   /api/v1/auth/refresh                   Exchange refresh token for new token pair
POST   /api/v1/auth/logout                    Revoke refresh token
GET    /api/v1/auth/me                         Get current user profile
POST   /api/v1/auth/change-password           Change own password (requires current password)
```

**Login response**:

```json
{
    "data": {
        "accessToken": "eyJhbGciOiJIUzI1NiIs...",
        "refreshToken": "base64-random-32-bytes",
        "expiresIn": 900,
        "user": {
            "id": "uuid",
            "username": "admin",
            "displayName": "Admin User",
            "email": "admin@example.com",
            "role": "admin"
        }
    }
}
```

**Error codes** (10000–10014):

| Code | Name | HTTP Status |
|------|------|-------------|
| 10000 | InvalidCredentials | 401 |
| 10001 | AccountLocked | 423 |
| 10002 | AccountDisabled | 403 |
| 10003 | InvalidRefreshToken | 401 |
| 10004 | RefreshTokenExpired | 401 |
| 10007 | Unauthorized | 401 |
| 10008 | Forbidden | 403 |
| 10012 | WeakPassword | 400 |
| 10013 | InvalidCurrentPassword | 400 |

### 10.11 Setup API

Both endpoints use `[AllowAnonymous]`.

```
GET    /api/v1/setup/status                   Check if initial setup is completed
POST   /api/v1/setup/initialize               Create initial admin account
```

**Error codes**:

| Code | Name | HTTP Status |
|------|------|-------------|
| 10005 | SetupNotCompleted | 503 |
| 10006 | SetupAlreadyCompleted | 409 |

### 10.12 Users API (Admin Only)

All endpoints require `[Authorize(Roles = "admin")]`.

```
GET    /api/v1/users                          List users (paginated, searchable)
GET    /api/v1/users/{id}                     Get user by ID
POST   /api/v1/users                          Create user
PUT    /api/v1/users/{id}                     Update user (role, display name, active status)
DELETE /api/v1/users/{id}                     Soft-delete user
POST   /api/v1/users/{id}/reset-password      Reset user password (revokes all sessions)
```

**Error codes**:

| Code | Name | HTTP Status |
|------|------|-------------|
| 10009 | DuplicateUsername | 409 |
| 10010 | CannotDeleteSelf | 400 |
| 10011 | CannotDemoteLastAdmin | 400 |
| 10014 | UserNotFound | 404 |

### 10.13 System Settings API

```
GET    /api/v1/settings/auth                  Get auth settings (admin only)
PUT    /api/v1/settings/auth                  Update auth settings (admin only)
```

Auth settings control session timeout, refresh token lifetime, password policy, and lockout configuration. Settings are seeded on first migration and cannot be created or deleted via the API.

### 10.14 Dashboard & Summary Endpoints

These endpoints support the frontend dashboard with aggregated data that would be expensive to assemble from individual resource endpoints.

```
GET    /api/v1/dashboard/summary              System-wide summary stats
GET    /api/v1/dashboard/recent-executions    Recent job executions across all jobs
GET    /api/v1/dashboard/active-monitors      Currently active monitors with status
GET    /api/v1/dashboard/key-expiry           Keys expiring within N days
```

#### Summary Response

```json
{
    "data": {
        "jobs": { "total": 42, "enabled": 38, "disabled": 4 },
        "chains": { "total": 8, "enabled": 7, "disabled": 1 },
        "connections": { "total": 15, "active": 14, "disabled": 1 },
        "monitors": { "active": 6, "degraded": 0, "paused": 1, "error": 0 },
        "pgpKeys": { "active": 12, "expiring": 2, "retired": 5 },
        "sshKeys": { "active": 8, "retired": 2 },
        "recentExecutions": {
            "last24h": { "completed": 187, "failed": 3, "cancelled": 0 },
            "last7d": { "completed": 1247, "failed": 18, "cancelled": 2 }
        }
    },
    "error": null,
    "success": true,
    "timestamp": "2026-02-21T12:00:00Z"
}
```

### 10.15 Azure Functions API

On-demand trace retrieval for Azure Function executions. Traces are fetched live from Application Insights — nothing is stored in the Courier database.

```
GET    /api/v1/azure-functions/{connectionId}/traces/{invocationId}   Get execution traces
```

#### Response

```json
{
    "data": [
        {
            "timestamp": "2026-02-28T15:30:01.123Z",
            "message": "Processing file abc-123...",
            "severityLevel": 1
        },
        {
            "timestamp": "2026-02-28T15:30:05.456Z",
            "message": "File processed successfully",
            "severityLevel": 1
        }
    ],
    "error": null,
    "success": true,
    "timestamp": "2026-02-28T15:31:00Z"
}
```

Severity levels follow Application Insights convention: 0=Verbose, 1=Information, 2=Warning, 3=Error, 4=Critical.

### 10.16 Step Type Registry API

A read-only endpoint that returns all available step types and their configuration schemas. Used by the frontend job builder to render step-specific configuration forms.

```
GET    /api/v1/step-types                     List all registered step types
GET    /api/v1/step-types/{typeKey}           Get configuration schema for a step type
```

#### Step Type Response

```json
{
    "data": [
        {
            "typeKey": "sftp.download",
            "displayName": "SFTP Download",
            "category": "Transfer",
            "description": "Download files from a remote SFTP server",
            "configurationSchema": {
                "type": "object",
                "required": ["connectionId", "remotePath"],
                "properties": {
                    "connectionId": {
                        "type": "string",
                        "format": "uuid",
                        "description": "Connection to use",
                        "uiHint": "connection-picker"
                    },
                    "remotePath": {
                        "type": "string",
                        "description": "Remote directory path"
                    },
                    "filePattern": {
                        "type": "string",
                        "description": "Glob pattern for file matching",
                        "default": "*"
                    },
                    "localPath": {
                        "type": "string",
                        "description": "Local destination path",
                        "default": "${job.temp_dir}"
                    },
                    "deleteAfterDownload": {
                        "type": "boolean",
                        "description": "Remove file from server after download",
                        "default": false
                    }
                }
            }
        }
    ],
    "error": null,
    "success": true,
    "timestamp": "2026-02-21T12:00:00Z"
}
```

The `configurationSchema` follows JSON Schema with custom `uiHint` extensions that the frontend uses to render appropriate input controls (e.g., `connection-picker` renders a connection dropdown, `key-picker` renders a PGP key selector).

### 10.17 OpenAPI / Swagger Configuration

The API specification is generated at build time via Swashbuckle and served at `/swagger` in development and staging environments. Production exposes the spec at `/api/v1/openapi.json` but disables the Swagger UI.

```csharp
builder.Services.AddSwaggerGen(options =>
{
    options.SwaggerDoc("v1", new OpenApiInfo
    {
        Title = "Courier API",
        Version = "v1",
        Description = "Enterprise File Transfer & Job Management Platform"
    });

    // Bearer token auth
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        Description = "Azure AD / Entra ID bearer token"
    });

    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });

    // Include XML docs for rich descriptions
    var xmlFile = $"{Assembly.GetExecutingAssembly().GetName().Name}.xml";
    options.IncludeXmlComments(Path.Combine(AppContext.BaseDirectory, xmlFile));
});
```

### 10.18 Request Validation

All request bodies are validated using **FluentValidation**. Validators are auto-discovered from the assembly and wired into the ASP.NET pipeline via a validation filter. Validation errors return an HTTP 400 with error code `1000` (`Validation failed`) and field-level details.

```csharp
public class CreateJobValidator : AbstractValidator<CreateJobRequest>
{
    public CreateJobValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Job name is required.")
            .MaximumLength(200).WithMessage("Job name must not exceed 200 characters.");

        RuleFor(x => x.Steps)
            .NotEmpty().WithMessage("A job must have at least one step.");

        RuleForEach(x => x.Steps).ChildRules(step =>
        {
            step.RuleFor(s => s.TypeKey)
                .NotEmpty().WithMessage("Step type is required.");
            step.RuleFor(s => s.TimeoutSeconds)
                .InclusiveBetween(1, 86400).WithMessage("Timeout must be between 1 second and 24 hours.");
        });

        RuleFor(x => x.FailurePolicy.MaxRetries)
            .InclusiveBetween(0, 10).WithMessage("Max retries must be between 0 and 10.");
    }
}
```

Deeper validations (e.g., checking that a referenced `connectionId` exists, or that a step type key is registered) are performed in the application service layer and return `400` or `404` as appropriate.

---

## 11. Frontend Architecture

Courier's frontend is a Next.js application using the App Router, statically exported and served from a CDN. It is an internal tool behind Entra ID authentication — there are no public-facing pages or SEO requirements. All data flows through the Courier REST API (Section 10).

### 11.1 Tech Stack

| Technology | Purpose |
|-----------|---------|
| **Next.js** (App Router) | Framework — routing, static export, layouts |
| **React 19+** | UI library |
| **TypeScript** | Type safety across all frontend code |
| **Tailwind CSS** | Utility-first styling |
| **shadcn/ui** | Component library (Radix UI primitives + Tailwind) |
| **TanStack Query (React Query)** | Server state management — caching, refetching, mutations |
| **MSAL.js (@azure/msal-browser)** | Entra ID authentication — OAuth 2.0 PKCE flow |
| **Lucide React** | Icon library (consistent with shadcn/ui) |
| **React Hook Form** | Form state management (job builder, connection editor, key import) |
| **Zod** | Schema validation (client-side, mirrors FluentValidation rules) |
| **date-fns** | Date formatting and manipulation |
| **next-themes** | Dark/light mode theming |

### 11.2 Rendering Strategy

**Standalone server** (`output: 'standalone'` in `next.config.ts`). Next.js compiles a self-contained Node.js server that handles routing, serves static assets, and supports all App Router features — packaged into a minimal Docker image without `node_modules`.

> **Design decision (2026-03-15)**: The original design specified `output: 'export'` (static HTML + nginx). This was changed to `output: 'standalone'` because the frontend uses ~17 dynamic `[id]` routes, all of which are `"use client"` components. Next.js `output: 'export'` requires `generateStaticParams` on every dynamic route, which would need stubs added to all detail pages. The standalone approach works immediately with the existing codebase, handles all client-side routing natively, and avoids the need for nginx SPA fallback configuration. The tradeoff is a slightly larger Docker image (~150MB vs ~25MB for nginx) — acceptable for an internal enterprise application.

**Client-side data fetching**: All API calls happen in the browser via TanStack Query after authentication. Pages render loading skeletons until data arrives. No server-side rendering or data fetching is used — the Node.js server only handles routing and static asset serving.

**Build-time configuration**: Environment-specific values (API base URL, Entra ID client ID, tenant ID) are injected at build time via `NEXT_PUBLIC_*` environment variables. Separate builds are produced for dev, staging, and production.

#### 11.2.1 Client-Side SPA Constraints

Although the frontend runs on a Node.js server, Courier deliberately avoids server-side features. The frontend is a pure client-side SPA that uses Next.js for routing, layout system, and build tooling. There is no server-side rendering, no server-side auth, and no backend-for-frontend (BFF) pattern. The browser is the only meaningful execution environment.

**Server-side features intentionally not used**:

| Feature | Status | Courier's Approach |
|---------|--------|-------------------|
| Server Components (async data fetching) | **Not used** — all components are `"use client"` | TanStack Query for all data fetching, with loading skeletons |
| Server Actions (`"use server"`) | **Not used** | All mutations go through the REST API via TanStack Query's `useMutation` |
| Route Handlers (`app/api/...`) | **Not used** | The .NET API is the only backend; the frontend never proxies or transforms requests |
| Middleware (`middleware.ts`) | **Not used** | Auth guard is a client-side `AuthProvider` wrapper that checks token state before rendering children (see 11.3). Route protection is enforced in the browser, with the API as the authoritative gate. |
| `next/image` optimization | **Not used** | Standard `<img>` tags. Courier's UI is data-heavy, not image-heavy — no optimization needed. |
| Incremental Static Regeneration (ISR) | **Not used** | All data is fetched client-side; pages don't need regeneration. |

#### 11.2.2 Authentication Security in a Static SPA

Since there is no server component, all authentication state lives in the browser. This has specific security implications that are addressed by the OAuth 2.0 Authorization Code + PKCE flow:

**Why Authorization Code + PKCE (not Implicit Flow)**:

The OAuth 2.0 Implicit Flow was historically used for SPAs but is now deprecated by OAuth 2.1 because it exposes access tokens in the URL fragment (susceptible to browser history leaks and referrer header exfiltration). Courier uses the Authorization Code flow with Proof Key for Code Exchange (PKCE), which is the current best practice for public clients:

1. The browser generates a random `code_verifier` (128-bit entropy) and derives a `code_challenge` (SHA-256 hash)
2. The authorization request sends the `code_challenge` to Entra ID
3. Entra ID returns an authorization code to the redirect URI
4. The browser exchanges the code + `code_verifier` for tokens — Entra ID verifies the challenge, preventing authorization code interception attacks
5. No client secret is used (the app registration is configured as a "public client" in Entra ID)

**Token storage**: MSAL.js stores tokens in `sessionStorage` (cleared on tab close), never `localStorage` (persists across sessions and is accessible to any script on the same origin). The access token lifetime is controlled by Entra ID (default: 1 hour). Refresh tokens are handled by MSAL.js via silent iframe-based renewal — the refresh token itself is never exposed to application JavaScript.

**What the SPA cannot enforce**: Client-side auth checks (hiding UI elements for unauthorized roles, redirecting unauthenticated users to login) are a UX convenience, not a security boundary. A determined user could modify client-side JavaScript to bypass any UI restriction. The API enforces all authorization server-side — every endpoint validates the bearer token and checks role claims (Section 12.2). If a user manipulates the UI to call an endpoint they shouldn't access, the API returns `403 Forbidden`.

**XSS as the primary threat**: In an SPA where tokens live in the browser, XSS is the most dangerous attack vector — injected script can read `sessionStorage` and exfiltrate tokens. Mitigations: strict Content-Security-Policy header (Section 12.6.2), no `dangerouslySetInnerHTML` without sanitization, React's default escaping of rendered values, dependency auditing via `npm audit` in CI.

### 11.3 Authentication Flow

```
User visits Courier
    │
    ▼
┌─────────────────────────┐
│  MSAL.js checks for     │
│  cached token            │
└────────────┬────────────┘
             │
        ┌────▼────┐
        │ Token?  │
        └────┬────┘
         No  │  Yes
    ┌────────▼──┐  │
    │ Redirect   │  │
    │ to Entra   │  │
    │ ID login   │  │
    └────────┬──┘  │
             │     │
    ┌────────▼──┐  │
    │ Auth code  │  │
    │ callback   │  │
    │ + PKCE     │  │
    └────────┬──┘  │
             │     │
    ┌────────▼─────▼──────┐
    │  Access token in     │
    │  memory (MSAL cache) │
    └────────────┬────────┘
                 │
    ┌────────────▼────────────┐
    │  API calls include      │
    │  Authorization: Bearer  │
    │  header via interceptor │
    └─────────────────────────┘
```

**MSAL.js configuration**:

```typescript
const msalConfig: Configuration = {
  auth: {
    clientId: process.env.NEXT_PUBLIC_ENTRA_CLIENT_ID!,
    authority: `https://login.microsoftonline.com/${process.env.NEXT_PUBLIC_ENTRA_TENANT_ID}`,
    redirectUri: process.env.NEXT_PUBLIC_REDIRECT_URI!,
  },
  cache: {
    cacheLocation: "sessionStorage",  // Not localStorage — cleared on tab close
    storeAuthStateInCookie: false,
  },
};

const loginRequest: PopupRequest = {
  scopes: [`api://${process.env.NEXT_PUBLIC_ENTRA_CLIENT_ID}/access_as_user`],
};
```

**Token lifecycle**: MSAL.js handles token refresh automatically via silent token acquisition. If the silent refresh fails (e.g., session expired), the user is redirected to the Entra ID login page. The access token is never stored in localStorage — only in sessionStorage or MSAL's in-memory cache.

**Role extraction**: The `roles` claim from the Entra ID token is decoded client-side to control UI visibility (e.g., hiding "Create Connection" for Viewer role). This is a UX convenience only — the API enforces authorization server-side regardless of what the frontend shows.

### 11.4 Project Structure

```
Courier.Frontend/
├── src/
│   ├── app/                              ← Next.js App Router
│   │   ├── layout.tsx                    ← Root layout (providers, sidebar, theme)
│   │   ├── page.tsx                      ← Dashboard (home page)
│   │   ├── jobs/
│   │   │   ├── page.tsx                  ← Job list
│   │   │   ├── new/page.tsx              ← Job builder (create)
│   │   │   ├── [id]/
│   │   │   │   ├── page.tsx              ← Job detail
│   │   │   │   ├── edit/page.tsx         ← Job builder (edit)
│   │   │   │   ├── executions/page.tsx   ← Execution history
│   │   │   │   └── executions/[execId]/page.tsx  ← Execution detail
│   │   │   └── loading.tsx               ← Skeleton loader
│   │   ├── chains/
│   │   │   ├── page.tsx                  ← Chain list
│   │   │   ├── new/page.tsx
│   │   │   └── [id]/page.tsx
│   │   ├── connections/
│   │   │   ├── page.tsx                  ← Connection list
│   │   │   ├── new/page.tsx
│   │   │   └── [id]/page.tsx
│   │   ├── keys/
│   │   │   ├── pgp/
│   │   │   │   ├── page.tsx              ← PGP key list
│   │   │   │   ├── generate/page.tsx
│   │   │   │   ├── import/page.tsx
│   │   │   │   └── [id]/page.tsx
│   │   │   └── ssh/
│   │   │       ├── page.tsx              ← SSH key list
│   │   │       ├── generate/page.tsx
│   │   │       ├── import/page.tsx
│   │   │       └── [id]/page.tsx
│   │   ├── monitors/
│   │   │   ├── page.tsx
│   │   │   ├── new/page.tsx
│   │   │   └── [id]/page.tsx
│   │   ├── audit/
│   │   │   └── page.tsx                  ← Audit log (filterable)
│   │   └── settings/
│   │       └── page.tsx                  ← System settings (Admin only)
│   │
│   ├── components/
│   │   ├── ui/                           ← shadcn/ui components (Button, Dialog, Table, etc.)
│   │   ├── layout/
│   │   │   ├── Sidebar.tsx               ← Navigation sidebar
│   │   │   ├── Header.tsx                ← Top bar (user, theme toggle, role badge)
│   │   │   └── Breadcrumbs.tsx
│   │   ├── shared/
│   │   │   ├── DataTable.tsx             ← Generic paginated table with sort/filter
│   │   │   ├── StatusBadge.tsx           ← Colored badge for entity states
│   │   │   ├── TagPicker.tsx             ← Tag selector/creator
│   │   │   ├── ConfirmDialog.tsx         ← Destructive action confirmation
│   │   │   ├── EmptyState.tsx            ← Zero-state illustrations
│   │   │   ├── ErrorDisplay.tsx          ← Maps ApiError to user-friendly message
│   │   │   ├── LoadingSkeleton.tsx        ← Shimmer placeholders
│   │   │   └── RoleGate.tsx              ← Conditionally renders children based on role
│   │   ├── jobs/
│   │   │   ├── JobBuilder.tsx            ← Multi-step form for job creation/editing
│   │   │   ├── StepConfigurator.tsx      ← Dynamic form per step type (from step-types API)
│   │   │   ├── JobExecutionTimeline.tsx  ← Visual timeline of step execution states
│   │   │   ├── FailurePolicyEditor.tsx
│   │   │   └── CronEditor.tsx            ← Human-friendly cron expression builder
│   │   ├── connections/
│   │   │   ├── ConnectionForm.tsx
│   │   │   ├── ConnectionTestResult.tsx  ← Displays test outcome with algorithm details
│   │   │   └── FipsOverrideBanner.tsx    ← Warning banner when FIPS override enabled
│   │   ├── keys/
│   │   │   ├── KeyGenerateForm.tsx
│   │   │   ├── KeyImportForm.tsx
│   │   │   ├── KeyLifecycleBadge.tsx     ← Active/Expiring/Retired/Revoked status
│   │   │   └── KeyExpiryWarning.tsx
│   │   ├── monitors/
│   │   │   ├── MonitorForm.tsx
│   │   │   ├── MonitorStateBadge.tsx
│   │   │   └── FileLogTable.tsx          ← Triggered file history
│   │   └── dashboard/
│   │       ├── SummaryCards.tsx           ← Top-level metric cards
│   │       ├── RecentExecutionsTable.tsx
│   │       ├── ActiveMonitorsList.tsx
│   │       └── KeyExpiryList.tsx
│   │
│   ├── lib/
│   │   ├── api/
│   │   │   ├── client.ts                 ← Fetch wrapper with auth header injection
│   │   │   ├── types.ts                  ← ApiResponse<T>, PagedApiResponse<T>, ApiError
│   │   │   ├── jobs.ts                   ← Job API functions (listJobs, createJob, etc.)
│   │   │   ├── chains.ts
│   │   │   ├── connections.ts
│   │   │   ├── pgp-keys.ts
│   │   │   ├── ssh-keys.ts
│   │   │   ├── monitors.ts
│   │   │   ├── tags.ts
│   │   │   ├── audit.ts
│   │   │   ├── settings.ts
│   │   │   ├── dashboard.ts
│   │   │   └── step-types.ts
│   │   ├── auth/
│   │   │   ├── msal.ts                   ← MSAL instance, config, login/logout
│   │   │   ├── AuthProvider.tsx           ← React context provider for auth state
│   │   │   └── useAuth.ts                ← Hook: user, roles, token, isAuthenticated
│   │   ├── hooks/
│   │   │   ├── useJobs.ts                ← TanStack Query hooks for jobs
│   │   │   ├── useConnections.ts
│   │   │   ├── useKeys.ts
│   │   │   ├── useMonitors.ts
│   │   │   ├── useTags.ts
│   │   │   ├── useAuditLog.ts
│   │   │   ├── useDashboard.ts
│   │   │   └── usePagination.ts          ← Shared pagination state hook
│   │   ├── utils/
│   │   │   ├── errors.ts                 ← Error code → user-friendly message mapping
│   │   │   ├── dates.ts                  ← date-fns formatters
│   │   │   ├── cron.ts                   ← Cron expression ↔ human description
│   │   │   └── constants.ts              ← API base URL, roles, pagination defaults
│   │   └── validations/
│   │       ├── job.schema.ts             ← Zod schemas for job forms
│   │       ├── connection.schema.ts
│   │       ├── key.schema.ts
│   │       └── monitor.schema.ts
│   │
│   └── styles/
│       └── globals.css                   ← Tailwind directives, shadcn/ui theme tokens
│
├── public/                               ← Static assets (favicon, logo)
├── next.config.ts                        ← output: 'standalone', env vars, image config
├── tailwind.config.ts                    ← Theme, custom colors, font
├── tsconfig.json
└── package.json
```

### 11.5 API Client Layer

All API communication is centralized in `lib/api/`. A typed fetch wrapper handles authentication, response envelope unwrapping, and error normalization.

**Fetch client**:

```typescript
import { msalInstance, loginRequest } from "@/lib/auth/msal";
import type { ApiResponse, PagedApiResponse, ApiError } from "./types";

const API_BASE = process.env.NEXT_PUBLIC_API_BASE_URL!;

async function getAuthHeaders(): Promise<HeadersInit> {
  const account = msalInstance.getActiveAccount();
  if (!account) throw new Error("No active account");

  const { accessToken } = await msalInstance.acquireTokenSilent({
    ...loginRequest,
    account,
  });

  return {
    Authorization: `Bearer ${accessToken}`,
    "Content-Type": "application/json",
  };
}

export async function apiGet<T>(path: string): Promise<ApiResponse<T>> {
  const res = await fetch(`${API_BASE}${path}`, {
    headers: await getAuthHeaders(),
  });
  const body: ApiResponse<T> = await res.json();
  if (!body.success) throw new ApiClientError(body.error!);
  return body;
}

export async function apiGetPaged<T>(
  path: string,
  params: Record<string, string>
): Promise<PagedApiResponse<T>> {
  const query = new URLSearchParams(params).toString();
  const res = await fetch(`${API_BASE}${path}?${query}`, {
    headers: await getAuthHeaders(),
  });
  const body: PagedApiResponse<T> = await res.json();
  if (!body.success) throw new ApiClientError(body.error!);
  return body;
}

export async function apiPost<T>(path: string, data?: unknown): Promise<ApiResponse<T>> {
  const res = await fetch(`${API_BASE}${path}`, {
    method: "POST",
    headers: await getAuthHeaders(),
    body: data ? JSON.stringify(data) : undefined,
  });
  const body: ApiResponse<T> = await res.json();
  if (!body.success) throw new ApiClientError(body.error!);
  return body;
}

// apiPut, apiDelete follow the same pattern

export class ApiClientError extends Error {
  constructor(public error: ApiError) {
    super(error.message);
    this.name = "ApiClientError";
  }
}
```

**TypeScript types** (mirroring the backend standard response model):

```typescript
export interface ApiResponse<T = unknown> {
  data: T | null;
  error: ApiError | null;
  success: boolean;
  timestamp: string;
}

export interface PagedApiResponse<T = unknown> {
  data: T[];
  pagination: PaginationMeta;
  error: ApiError | null;
  success: boolean;
  timestamp: string;
}

export interface PaginationMeta {
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface ApiError {
  code: number;
  systemMessage: string;
  message: string;
  details?: FieldError[];
}

export interface FieldError {
  field: string;
  message: string;
}
```

### 11.6 Server State Management (TanStack Query)

All server data is managed through TanStack Query. No global state store (Redux, Zustand) is used — TanStack Query handles caching, background refetching, optimistic updates, and cache invalidation.

**Query hook pattern** (example: Jobs):

```typescript
// lib/hooks/useJobs.ts
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import * as jobsApi from "@/lib/api/jobs";

export const jobKeys = {
  all: ["jobs"] as const,
  lists: () => [...jobKeys.all, "list"] as const,
  list: (filters: JobFilter) => [...jobKeys.lists(), filters] as const,
  details: () => [...jobKeys.all, "detail"] as const,
  detail: (id: string) => [...jobKeys.details(), id] as const,
  executions: (id: string) => [...jobKeys.detail(id), "executions"] as const,
};

export function useJobs(filters: JobFilter) {
  return useQuery({
    queryKey: jobKeys.list(filters),
    queryFn: () => jobsApi.listJobs(filters),
  });
}

export function useJob(id: string) {
  return useQuery({
    queryKey: jobKeys.detail(id),
    queryFn: () => jobsApi.getJob(id),
  });
}

export function useCreateJob() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: jobsApi.createJob,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: jobKeys.lists() });
    },
  });
}

export function useExecuteJob() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => jobsApi.executeJob(id),
    onSuccess: (_, id) => {
      queryClient.invalidateQueries({ queryKey: jobKeys.executions(id) });
    },
  });
}
```

**Cache configuration**:

```typescript
const queryClient = new QueryClient({
  defaultOptions: {
    queries: {
      staleTime: 30_000,        // Data considered fresh for 30 seconds
      gcTime: 5 * 60_000,       // Unused cache garbage collected after 5 minutes
      retry: 1,                  // One retry on network failure
      refetchOnWindowFocus: true, // Refetch when user returns to tab
    },
  },
});
```

**Polling for active executions**: Job and chain execution detail pages use `refetchInterval` to poll for state changes while an execution is in progress:

```typescript
export function useJobExecution(jobId: string, execId: string) {
  return useQuery({
    queryKey: [...jobKeys.detail(jobId), "execution", execId],
    queryFn: () => jobsApi.getExecution(jobId, execId),
    refetchInterval: (query) => {
      const state = query.state.data?.data?.state;
      return state === "running" || state === "queued" ? 3_000 : false;
    },
  });
}
```

### 11.7 Error Handling

API errors follow the standard response model (Section 10.1) with numeric error codes. The frontend maps these codes to user-friendly messages and appropriate UI treatments.

**Error display strategy**:

| Error Code Range | UI Treatment |
|-----------------|--------------|
| 1000 (validation) | Inline field-level errors on the form, highlighted with `details[].field` mapping |
| 1030 (not found) | Redirect to list page with toast notification |
| 1050–1052 (conflicts) | Toast notification with `message` text and suggested action |
| 1010–1020 (auth/permission) | Redirect to login (1010/1011) or show "insufficient permissions" banner (1020) |
| 2000–2007 (job state) | Toast notification with specific guidance |
| 3000–3003 (connection test) | Inline display in connection test result panel |
| 4000–4013 (key errors) | Inline form errors or toast depending on context |
| 7001–7003 (archive security) | Step failure detail in execution view |
| 5000–5004 (monitor errors) | Toast notification |
| 1099 (internal) | Generic error banner with correlation reference from `message` |

**Error utility**:

```typescript
// lib/utils/errors.ts
import { toast } from "sonner";   // shadcn/ui compatible toast
import type { ApiError } from "@/lib/api/types";

export function handleApiError(error: ApiError, router?: AppRouterInstance) {
  if (error.code === 1010 || error.code === 1011) {
    // Token expired or invalid — force re-auth
    msalInstance.loginRedirect(loginRequest);
    return;
  }

  if (error.code === 1030 && router) {
    toast.error(error.message);
    router.back();
    return;
  }

  if (error.code === 1000 && error.details) {
    // Validation — handled by form, not toast
    return;
  }

  // Default: show toast
  toast.error(error.message);
}
```

### 11.8 Role-Based UI

The frontend reads roles from the Entra ID token and conditionally renders UI elements. This is purely cosmetic — the API enforces authorization regardless.

**RoleGate component**:

```tsx
// components/shared/RoleGate.tsx
import { useAuth } from "@/lib/auth/useAuth";

type Role = "Admin" | "Operator" | "Viewer";

interface RoleGateProps {
  allowed: Role[];
  children: React.ReactNode;
  fallback?: React.ReactNode;  // Optional: show something else for insufficient role
}

export function RoleGate({ allowed, children, fallback = null }: RoleGateProps) {
  const { roles } = useAuth();
  const hasAccess = roles.some((role) => allowed.includes(role));
  return hasAccess ? <>{children}</> : <>{fallback}</>;
}
```

**Usage examples**:

```tsx
// Only Admin sees "Create Connection" button
<RoleGate allowed={["Admin"]}>
  <Button onClick={() => router.push("/connections/new")}>
    New Connection
  </Button>
</RoleGate>

// Admin and Operator see "Execute" button
<RoleGate allowed={["Admin", "Operator"]}>
  <Button onClick={() => executeJob.mutate(job.id)}>
    Execute Now
  </Button>
</RoleGate>

// FIPS override toggle — Admin only, with warning
<RoleGate allowed={["Admin"]}>
  <FipsOverrideBanner connectionId={connection.id} />
</RoleGate>
```

### 11.9 Key UI Components

#### 11.9.1 DataTable

A generic, reusable table component used across all list pages. Built on shadcn/ui's `Table` with integrated pagination, sorting, and filtering.

**Features**:

- Column definitions with sortable flag, custom cell renderers
- Controlled pagination synced with URL query parameters (`?page=2&pageSize=25`)
- Sort state synced with URL (`?sort=name:asc`)
- Filter controls rendered above the table per resource type
- Loading skeleton state while data is fetching
- Empty state with illustration and call-to-action
- Row actions dropdown (view, edit, delete, execute)

**Pagination synced with URL**:

```typescript
// lib/hooks/usePagination.ts
import { useSearchParams, useRouter } from "next/navigation";

export function usePagination(defaults = { page: 1, pageSize: 25 }) {
  const searchParams = useSearchParams();
  const router = useRouter();

  const page = Number(searchParams.get("page")) || defaults.page;
  const pageSize = Number(searchParams.get("pageSize")) || defaults.pageSize;
  const sort = searchParams.get("sort") || undefined;

  const setPage = (newPage: number) => {
    const params = new URLSearchParams(searchParams.toString());
    params.set("page", String(newPage));
    router.push(`?${params.toString()}`);
  };

  return { page, pageSize, sort, setPage };
}
```

#### 11.9.2 Job Builder

The most complex UI component. A multi-step wizard for creating and editing jobs.

**Sections**:

1. **Basics** — Name, description, enabled toggle, tags
2. **Steps** — Ordered list of steps. Each step has a type dropdown (populated from `/api/v1/step-types`). Selecting a type renders a dynamic form generated from the step type's `configurationSchema` (JSON Schema with `uiHint` extensions). Steps are reorderable via drag-and-drop.
3. **Failure Policy** — Policy type selector, retry count, backoff configuration
4. **Schedule** — Optional cron expression builder with human-readable preview, or one-shot datetime picker
5. **Review** — Summary of the complete job configuration before save

**Dynamic step configuration rendering**:

The step type registry API (Section 10.12) returns a JSON Schema for each step type with custom `uiHint` extensions. The `StepConfigurator` component interprets these hints to render appropriate inputs:

| `uiHint` | Rendered Control |
|----------|-----------------|
| `connection-picker` | Connection dropdown (filtered by protocol if schema specifies) |
| `key-picker` | PGP/SSH key dropdown (filtered by status: active only) |
| `file-pattern` | Text input with glob pattern preview |
| `path` | Text input with path autocomplete (if connection supports listing) |
| `password` | Password input with show/hide toggle |
| (none) | Inferred from JSON Schema `type`: string → text input, boolean → switch, number → number input, enum → select |

#### 11.9.3 Execution Timeline

A visual timeline component displayed on the job execution detail page. Shows each step as a horizontal bar with state coloring, duration, bytes processed, and error details if failed.

```
Step 1: Download from Partner     ████████████████░░░░░  12.4s  3.2 MB   ✓ Completed
Step 2: Decrypt PGP files         ██████████░░░░░░░░░░░   6.1s  3.1 MB   ✓ Completed
Step 3: Decompress ZIP            ████████████████████░  18.7s  15.8 MB  ✓ Completed
Step 4: Upload to Internal SFTP   █████░░░░░░░░░░░░░░░░   FAILED after 4.2s
                                  Error: Connection refused (host: internal-sftp.corp.com:22)
```

### 11.10 Theming

Courier uses the shadcn/ui theming system with CSS custom properties. Light and dark modes are supported via `next-themes`, with the user's preference persisted in `localStorage`.

**Design tokens** (defined in `globals.css`):

```css
@layer base {
  :root {
    --background: 0 0% 100%;
    --foreground: 222.2 84% 4.9%;
    --primary: 222.2 47.4% 11.2%;
    --primary-foreground: 210 40% 98%;
    --destructive: 0 84.2% 60.2%;
    --muted: 210 40% 96.1%;
    --accent: 210 40% 96.1%;
    --border: 214.3 31.8% 91.4%;
    --ring: 222.2 84% 4.9%;
    --radius: 0.5rem;
  }

  .dark {
    --background: 222.2 84% 4.9%;
    --foreground: 210 40% 98%;
    /* ... dark variants */
  }
}
```

**Status colors** (consistent across all entity state badges):

| State | Color | Usage |
|-------|-------|-------|
| Active / Completed / Enabled | Green | Jobs, connections, monitors, keys, executions |
| Running / Queued | Blue | Executions |
| Paused | Yellow | Executions, monitors |
| Disabled / Retired | Gray | Jobs, connections, monitors, keys |
| Failed / Error / Revoked | Red | Executions, monitors, keys |
| Expiring | Orange | Keys approaching expiration |

### 11.11 Build & Deployment

**Build**:

```bash
# Install dependencies
npm ci

# Build standalone server
npm run build    # next build → outputs to .next/standalone/

# Contents of .next/standalone/:
# ├── server.js              ← self-contained Node.js server
# ├── .next/static/...       ← JS/CSS bundles (copied separately in Docker)
# ├── public/...             ← static assets (copied separately in Docker)
# └── node_modules/...       ← minimal production dependencies
```

**Deployment**: The `.next/standalone/` directory is a self-contained Node.js server. Run with `node server.js` — no `npm start` or full `node_modules` required.

| Environment | Hosting | API URL |
|-------------|---------|---------|
| Development | `next dev` (local via Aspire) | `http://localhost:5000/api/v1` |
| Staging | Container App (Node.js standalone) | `https://courier-staging.corp.com/api/v1` |
| Production | Container App (Node.js standalone) | `https://courier.corp.com/api/v1` |

**Routing**: The standalone Node.js server handles all client-side routing natively — no nginx SPA fallback or `try_files` configuration needed.

---

## 12. Security

This section consolidates all security concerns across the Courier platform: authentication, authorization, data protection at rest and in transit, secrets management, and hardening. Many security decisions are described in detail in their respective subsystem sections — this section provides the unified view and fills gaps.

### 12.1 Authentication

Courier uses a **hybrid local-first + SSO-optional** authentication model. Local username/password authentication works out of the box on first deployment, with no external identity provider required. Administrators can optionally configure enterprise SSO providers (OIDC, SAML) through the settings UI.

#### 12.1.1 First-Run Setup

On first startup, a `SetupGuardMiddleware` blocks all API requests (except `/api/v1/setup/*`, `/api/v1/auth/*`, `/health`, `/swagger`) with `503 Service Unavailable` until initial setup is completed. The frontend redirects to `/setup`, where the administrator creates the first admin account.

**Setup flow**:

1. Frontend detects `auth.setup_completed = false` via `GET /api/v1/setup/status` and redirects to `/setup`
2. Administrator enters username, display name, optional email, and password
3. `POST /api/v1/setup/initialize` creates the admin user with Argon2id-hashed password and sets `auth.setup_completed = true`
4. The setup guard cache is invalidated and subsequent requests proceed normally

#### 12.1.2 Local Authentication

**Password hashing**: Argon2id with 64 MB memory, 4 iterations, 8-way parallelism, 16-byte random salt, 32-byte hash output. Stored as `$argon2id$<base64-salt>$<base64-hash>`. Verification uses `CryptographicOperations.FixedTimeEquals` to prevent timing attacks.

**Login flow**:

1. `POST /api/v1/auth/login` with username and password
2. Server validates credentials, checks account active status and lockout
3. On success: returns JWT access token + opaque refresh token + user profile
4. On failure: increments `failed_login_count`; locks account for configurable duration after threshold exceeded

**JWT access tokens**: HMAC-SHA256 signed, configurable lifetime (default 15 minutes). Claims: `sub` (user ID), `role`, `name`, `email`, `jti`.

```csharp
builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = "courier",
            ValidAudience = "courier-api",
            IssuerSigningKey = new SymmetricSecurityKey(keyBytes),
            ClockSkew = TimeSpan.FromSeconds(30),
        };
    });
```

**Refresh tokens**: 32 random bytes (base64-encoded), stored as SHA-256 hash in the `refresh_tokens` table. Configurable lifetime (default 7 days). Token rotation on every refresh — the old token is revoked and a new one issued. Revoked tokens cannot be reused.

**Account lockout**: After configurable failed attempts (default 5), the account is locked for a configurable duration (default 15 minutes). Successful login resets the failed counter and clears the lock.

#### 12.1.3 SSO Authentication (Phase 2/3 — Future)

The `auth_providers` table and SSO fields on the `users` table are in place but not yet active. Planned phases:

- **Phase 2 — OIDC**: Azure AD, Google, Okta via `Microsoft.AspNetCore.Authentication.OpenIdConnect`. After OIDC verification, the server issues the same JWT + refresh token pair as local auth. Users are auto-provisioned on first login with a configurable default role.
- **Phase 3 — SAML**: Enterprise SAML 2.0 support via `ITfoxtec.Identity.Saml2` or equivalent. SP metadata and assertion consumer endpoints.

SSO users have `is_sso_user = true`, `sso_provider_id`, and `sso_subject_id` set. They may have no `password_hash` (SSO-only) or may also have a local password (dual auth).

### 12.2 Authorization — Role-Based Access Control (RBAC)

Courier uses a simple three-role model. For local users, roles are assigned by administrators through the user management UI (`/settings/users`). For SSO users (Phase 2+), roles are either mapped from the identity provider's claims or assigned a configurable default role on auto-provisioning.

**Roles**:

| Role | Description |
|------|-------------|
| `Admin` | Full access to all resources and settings. Can manage connections, keys, system settings, and view audit logs. Intended for platform administrators. |
| `Operator` | Can create/edit/execute jobs, chains, monitors. Can view connections and keys but cannot create or modify them. Cannot change system settings. |
| `Viewer` | Read-only access to all resources. Can view job executions, audit logs, dashboards. Cannot create, modify, delete, or execute anything. |

**Permission matrix**:

| Resource | Action | Admin | Operator | Viewer |
|----------|--------|-------|----------|--------|
| Jobs | View | ✓ | ✓ | ✓ |
| Jobs | Create / Edit / Delete | ✓ | ✓ | |
| Jobs | Execute / Cancel / Pause / Resume | ✓ | ✓ | |
| Job Chains | View | ✓ | ✓ | ✓ |
| Job Chains | Create / Edit / Delete | ✓ | ✓ | |
| Job Chains | Execute | ✓ | ✓ | |
| Connections | View | ✓ | ✓ | ✓ |
| Connections | Create / Edit / Delete | ✓ | | |
| Connections | Test | ✓ | ✓ | |
| PGP Keys | View metadata | ✓ | ✓ | ✓ |
| PGP Keys | Generate / Import / Delete | ✓ | | |
| PGP Keys | Export public | ✓ | ✓ | ✓ |
| PGP Keys | Export private | ✓ | | |
| PGP Keys | Create / Revoke share link | ✓ | | |
| PGP Keys | Retire / Revoke / Activate | ✓ | | |
| SSH Keys | View metadata | ✓ | ✓ | ✓ |
| SSH Keys | Generate / Import / Delete | ✓ | | |
| SSH Keys | Export public | ✓ | ✓ | ✓ |
| SSH Keys | Create / Revoke share link | ✓ | | |
| File Monitors | View | ✓ | ✓ | ✓ |
| File Monitors | Create / Edit / Delete | ✓ | ✓ | |
| File Monitors | Activate / Pause / Disable / Acknowledge | ✓ | ✓ | |
| Tags | View | ✓ | ✓ | ✓ |
| Tags | Create / Edit / Delete / Assign | ✓ | ✓ | |
| Audit Log | View | ✓ | ✓ | ✓ |
| Users | View / Create / Edit / Delete | ✓ | | |
| Users | Reset Password | ✓ | | |
| Auth Settings | View / Update | ✓ | | |
| System Settings | View | ✓ | ✓ | ✓ |
| System Settings | Update | ✓ | | |
| Dashboard | View | ✓ | ✓ | ✓ |

**Enforcement**:

Roles are enforced at the controller level via ASP.NET's `[Authorize]` attribute with role requirements. The role claim is embedded in the JWT access token issued by the Courier auth service:

```csharp
// Admin-only endpoints
[Authorize(Roles = "admin")]
[HttpPost("connections")]
public async Task<ApiResponse<ConnectionDto>> CreateConnection(CreateConnectionRequest request)

// All authenticated users (any role)
[Authorize]
[HttpGet("jobs")]
public async Task<PagedApiResponse<JobDto>> ListJobs([FromQuery] JobFilter filter)
```

**User management endpoints** (`/api/v1/users`) are restricted to the `admin` role. **Settings endpoints** (`/api/v1/settings/auth`) are also admin-only. Auth and setup endpoints use `[AllowAnonymous]`.

**Guards**:

- `CannotDeleteSelf` — admins cannot delete their own account
- `CannotDemoteLastAdmin` — the last remaining admin cannot have their role changed
- `DuplicateUsername` — usernames must be unique

### 12.3 Data Protection at Rest

#### 12.3.1 Envelope Encryption (PGP & SSH Key Material)

All cryptographic key material (PGP private keys, SSH private keys) and connection credentials (passwords, passphrases) are encrypted at rest using **AES-256-GCM envelope encryption**. This is described in detail in Section 7.5 (Cryptography) and Section 6.8 (SSH Key Store).

**Architecture**:

```
┌─────────────────┐
│  Azure Key Vault │
│                  │
│  Master Key      │◄──── RSA 2048-bit, hardware-backed, never exported
│  (KEK)           │      Used only to wrap/unwrap DEKs via API calls
└────────┬─────────┘
         │ wrapKey / unwrapKey (RSA-OAEP-256)
    ┌────▼────────────┐
    │  Data Encryption │
    │  Key (DEK)       │◄──── Random AES-256 key, unique per entity
    │                  │      Stored wrapped (opaque blob) alongside ciphertext
    └────────┬────────┘
             │ AES-256-GCM encrypt/decrypt
    ┌────────▼────────────┐
    │  Encrypted Data      │
    │  (BYTEA in Postgres) │
    │  [kek_ver | wrapped_dek | iv | tag | ciphertext | algo]
    └──────────────────────┘
```

- **Master key (KEK)**: An RSA 2048-bit key object in Azure Key Vault (not a secret). Hardware-backed where available. Never exported — the application only calls `WrapKey` and `UnwrapKey` via the Key Vault REST API's `CryptographyClient`. The plaintext KEK never exists in application memory, at startup or any other time. The `EnvelopeEncryptionService` (Section 7.3.7) holds a `CryptographyClient` reference (a remote API client), not key material.
- **Data Encryption Keys (DEKs)**: Random AES-256 keys generated per entity via `RandomNumberGenerator`. After encrypting the data, the DEK is wrapped by Key Vault and stored alongside the ciphertext. At decryption time, the wrapped DEK is sent to Key Vault for unwrapping, used locally for one operation, then zeroed from memory via `CryptographicOperations.ZeroMemory`. DEKs are never cached, pooled, or stored in any field — they exist in memory only for the duration of a single encrypt/decrypt call.
- **Algorithm**: AES-256-GCM with a unique 96-bit IV per encryption operation. The IV and authentication tag are stored with the ciphertext.
- **KEK version**: Each encrypted blob records which KEK version was used to wrap its DEK. This enables seamless rotation — Key Vault can unwrap using any previous version.
- **No key material in process memory**: The `EnvelopeEncryptionService` is a singleton that holds no mutable state — no keys, no caches, no session data. See Section 7.3.7 for the full implementation and the explicit anti-patterns it avoids.

**Startup requirement**: Courier refuses to start if Azure Key Vault is unreachable. The startup health check calls `KeyClient.GetKeyAsync` to verify connectivity and KEK existence — this retrieves key *metadata* (name, version, algorithm), not the private key material. If this check fails, the application throws and enters a crash loop. This is a reachability check, not a key download.

#### 12.3.2 Database-Level Encryption

PostgreSQL is configured with **Transparent Data Encryption (TDE)** at the storage level via Azure Database for PostgreSQL Flexible Server's encryption-at-rest feature. This provides defense-in-depth: even if the envelope encryption layer has a bug, the raw database files on disk are encrypted.

#### 12.3.3 What Is Encrypted

| Data | Encryption Method | Location |
|------|-------------------|----------|
| PGP private key material | AES-256-GCM envelope encryption | `pgp_keys.private_key_data` |
| PGP key passphrases | AES-256-GCM envelope encryption | `pgp_keys.passphrase_hash` |
| SSH private key material | AES-256-GCM envelope encryption | `ssh_keys.private_key_data` |
| SSH key passphrases | AES-256-GCM envelope encryption | `ssh_keys.passphrase_hash` |
| Connection passwords | AES-256-GCM envelope encryption | `connections.password_encrypted` |
| Database files on disk | Azure TDE | Storage layer |
| Azure Key Vault keys | HSM-backed | Key Vault |

#### 12.3.4 What Is NOT Encrypted (and Why)

| Data | Reason |
|------|--------|
| PGP public keys | Public by design — shared with partners |
| SSH public keys | Public by design — placed in `authorized_keys` |
| Connection hostnames/ports | Non-sensitive configuration data |
| Job configurations | May reference entity IDs but contain no secrets directly |
| Audit log entries | Historical records — no secret material is logged |

### 12.4 Data Protection in Transit

#### 12.4.1 API Layer (Client ↔ Courier)

All API traffic is served over **HTTPS (TLS 1.2+)**. HTTP connections are rejected — not redirected — in production. The TLS certificate is managed by the deployment infrastructure (Azure App Service, Kubernetes Ingress, or reverse proxy).

**HSTS**: The `Strict-Transport-Security` header is set with a 1-year max-age and `includeSubDomains`:

```csharp
app.UseHsts();  // Strict-Transport-Security: max-age=31536000; includeSubDomains
```

#### 12.4.2 File Transfer Layer (Courier ↔ Remote Servers)

- **SFTP**: All traffic encrypted via SSH. Algorithm preferences are configurable per connection (Section 6.7).
- **FTPS Explicit**: AUTH TLS upgrade before credentials are sent. Minimum TLS version configurable (default: TLS 1.2).
- **FTPS Implicit**: TLS from the first byte. Same TLS version floor.
- **Plain FTP**: Supported for legacy compatibility only. The UI displays a prominent warning when creating a plain FTP connection. Plain FTP transmits credentials and data in cleartext.

#### 12.4.3 Database Connections

PostgreSQL connections use **SSL/TLS** with certificate validation. The connection string includes `SSL Mode=Require` (staging/production) or `SSL Mode=Prefer` (development).

#### 12.4.4 Azure Key Vault

All Key Vault operations use HTTPS. Authentication is via Managed Identity (production) or Azure CLI credentials (development).

### 12.5 Secrets Management

Courier manages two categories of secrets: application secrets (infrastructure credentials) and user secrets (connection passwords, key passphrases). They are handled differently.

#### 12.5.1 Application Secrets

| Secret | Production | Development |
|--------|------------|-------------|
| Database connection string | Azure Key Vault | Local secrets file (`secrets.json`) |
| JWT signing secret | Azure Key Vault | `appsettings.Development.json` |
| Encryption KEK | Azure Key Vault | `appsettings.Development.json` (base64) |
| Key Vault URI | Environment variable | Environment variable |
| Managed Identity client ID | Environment variable | N/A (uses Azure CLI) |

**Production**: Application secrets are stored in Azure Key Vault as **Secret** objects and loaded at startup via the `AzureKeyVaultConfigurationProvider`:

```csharp
builder.Configuration.AddAzureKeyVault(
    new Uri(builder.Configuration["KeyVault:Uri"]!),
    new DefaultAzureCredential());
```

This loads configuration *values* (strings like the database connection string) into the .NET `IConfiguration` system. This is completely separate from the envelope encryption KEK, which is a Key Vault **Key** object accessed only via `CryptographyClient.WrapKeyAsync` / `UnwrapKeyAsync` at operation time, never loaded into configuration or process memory (see Section 7.3.7).

`DefaultAzureCredential` resolves to Managed Identity in production (no secrets on disk) and Azure CLI or Visual Studio credentials in development.

**Development**: Secrets that are not in Key Vault are stored in the .NET User Secrets store (`secrets.json`), which is outside the project directory and never committed to source control:

```bash
dotnet user-secrets set "ConnectionStrings:CourierDb" "Host=localhost;..."
dotnet user-secrets set "AzureAd:ClientSecret" "dev-client-secret"
```

#### 12.5.2 User Secrets (Connection Credentials, Key Passphrases)

These are managed by the application itself and stored encrypted in PostgreSQL via the envelope encryption scheme described in Section 12.3.1. They are never written to configuration files, environment variables, or logs.

**API surface rules**:

- Passwords and passphrases are accepted in POST/PUT request bodies (over HTTPS)
- They are never returned in GET responses — only boolean indicators (e.g., `hasPassword: true`)
- Private key export (PGP only) is an explicit action endpoint with audit logging
- SSH private keys are never exportable via the API

### 12.6 API Security Hardening

#### 12.6.1 CORS

Courier's API serves a single frontend origin. CORS is configured to allow only that origin:

```csharp
builder.Services.AddCors(options =>
{
    options.AddPolicy("CourierFrontend", policy =>
    {
        policy.WithOrigins(builder.Configuration["Frontend:Origin"]!)
              .AllowAnyHeader()
              .AllowAnyMethod()
              .AllowCredentials();
    });
});
```

In development, the frontend origin is `http://localhost:3000`. In production, it is the deployed Courier frontend URL. No wildcard origins are permitted.

#### 12.6.2 Security Headers

Applied via middleware on all responses:

```csharp
app.Use(async (context, next) =>
{
    context.Response.Headers["X-Content-Type-Options"] = "nosniff";
    context.Response.Headers["X-Frame-Options"] = "DENY";
    context.Response.Headers["X-XSS-Protection"] = "0";  // Disabled per OWASP; CSP preferred
    context.Response.Headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
    context.Response.Headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=()";
    context.Response.Headers["Content-Security-Policy"] =
        "default-src 'self'; frame-ancestors 'none'; form-action 'self'";
    await next();
});
```

#### 12.6.3 Request Size Limits

Maximum request body size is **50 MB**, sufficient for PGP/SSH key imports and large job configurations, while preventing abuse:

```csharp
builder.WebHost.ConfigureKestrel(options =>
{
    options.Limits.MaxRequestBodySize = 50 * 1024 * 1024;  // 50 MB
});
```

The key import endpoint (`POST /api/v1/pgp-keys/import`) accepts `multipart/form-data` with a single file. The file size is validated server-side before processing.

#### 12.6.4 Input Validation & Injection Prevention

- **SQL injection**: Eliminated by EF Core's parameterized queries. No raw SQL is executed from user input.
- **JSONB injection**: Step configurations and watch targets are serialized/deserialized through strongly-typed C# objects, not raw string concatenation.
- **Path traversal**: File paths in job step configurations are validated against an allowlist of base directories. Paths containing `..`, absolute paths outside the configured data directory, and symbolic links are rejected.
- **Command injection**: The 7z CLI wrapper (Section 8.1.2) uses `ProcessStartInfo.ArgumentList` (individual arguments, no shell interpretation), an absolute binary path (`/usr/bin/7z`), and `UseShellExecute = false`. Input filenames are validated against a safe character set and rejected if they contain shell metacharacters, path traversal sequences, or control characters. All operations are sandboxed within the job's temp directory, and extracted file paths are verified post-extraction against Zip Slip attacks. See Section 8.1.2 for the full hardening specification.

#### 12.6.5 Anti-Forgery

Since the API is purely JSON over bearer token authentication (no cookies), CSRF attacks are not applicable. The `Authorization: Bearer` header cannot be automatically attached by a browser in a cross-origin request without CORS cooperation, which is locked to the single frontend origin.

**Token storage**: Access tokens are held in memory only (JavaScript variable). Refresh tokens are stored in `localStorage`. While `localStorage` is accessible to XSS, the refresh token alone cannot access resources — it can only be exchanged for a new access token via `POST /api/v1/auth/refresh`, and token rotation ensures each refresh token is single-use.

### 12.7 Audit & Accountability

All security-relevant operations are recorded in the unified audit log (Section 13.3.5). The audit log is append-only — entries cannot be modified or deleted via any API endpoint.

**Security-critical audit events**:

| Operation | Logged Details |
|-----------|---------------|
| `Login` | User ID, IP address |
| `PasswordChanged` | User ID |
| `UserCreated` | User ID, username, role |
| `UserUpdated` | User ID, changed fields |
| `UserDeleted` | User ID, username |
| `UserPasswordReset` | Admin user ID, target user ID |
| `SetupInitialized` | Admin user ID |
| `PermissionDenied` | User ID, attempted action, required role, endpoint |
| `PrivateKeyExported` | User ID, key fingerprint, export format |
| `KeyGenerated` | User ID, algorithm, key fingerprint |
| `KeyRevoked` | User ID, key fingerprint, reason |
| `ConnectionCreated` | User ID, connection name, host, protocol |
| `ConnectionCredentialsUpdated` | User ID, connection ID (never the credential itself) |
| `HostKeyApproved` | User ID, connection ID, fingerprint, policy |
| `HostKeyRejected` | User ID, connection ID, presented fingerprint, expected fingerprint |
| `SystemSettingChanged` | User ID, setting key, old value, new value |
| `FipsOverrideEnabled` | User ID, connection ID, connection host |
| `FipsOverrideUsed` | Connection ID, negotiated algorithms, protocol |
| `PublicKeyShareLinkCreated` | User ID, key type, key ID, expiration |
| `PublicKeyShareLinkUsed` | Key type, key ID, requesting IP address |
| `PublicKeyShareLinkRevoked` | User ID, key type, key ID |
| `InsecureHostKeyPolicyUsed` | Connection ID, remote host, accepted fingerprint |
| `InsecureTlsPolicyUsed` | Connection ID, remote host, cert subject, policy errors |
| `WatcherAutoDisabled` | Monitor ID, overflow count, file count, reason |

**Audit log protection**: The `audit_log_entries` table has no UPDATE or DELETE exposed via the application. The EF Core `DbContext` does not include a `Remove` or `Update` method for `AuditLogEntry`. Database-level protection can be added via a PostgreSQL trigger that rejects `UPDATE` and `DELETE` on the audit table.

### 12.8 Sensitive Data Handling Rules

**Logging**: Sensitive data is never written to application logs. Structured logging is configured with a deny-list of property names that are automatically redacted:

```csharp
Log.Logger = new LoggerConfiguration()
    .Destructure.ByTransforming<CreateConnectionRequest>(r =>
        new { r.Name, r.Host, r.Port, Password = "[REDACTED]" })
    .CreateLogger();
```

Specific rules:

- Connection passwords: Never logged, even at Debug level
- Key passphrases: Never logged
- Private key material: Never logged
- Bearer tokens: Never logged in full — only the last 8 characters for correlation
- Request/response bodies: Logged at Debug level for non-sensitive endpoints only. Key and connection endpoints exclude sensitive fields.

**Error messages**: Internal error details (stack traces, database errors) are never returned to the client. The API returns a generic message with a correlation reference (e.g., `err_a1b2c3d4`) that maps to the detailed error in server logs.

**Temp files**: Job execution temp directories (`/data/courier/temp/{executionId}/`) may contain decrypted or decompressed files. These directories are cleaned up immediately on job completion or failure (Section 5.8). Paused jobs retain their temp directory but it is only accessible to the Courier application process.

### 12.9 Network Security

**Production deployment** assumes the following network topology:

- Courier API and background services run in a private subnet (Azure VNet or Kubernetes cluster)
- The frontend is served from a CDN or static hosting with API calls routed through a reverse proxy / API gateway
- PostgreSQL is accessible only from the private subnet (no public endpoint)
- Azure Key Vault is accessed via private endpoint or service endpoint within the VNet
- Outbound SFTP/FTP connections to partner servers are allowed through network security groups with destination allowlists

**No inbound connections from the internet directly to the Courier API**. All external traffic flows through the reverse proxy / load balancer which terminates TLS and forwards to the backend.

### 12.10 FIPS 140-2 / 140-3 Compliance

When `security.fips_mode_enabled = true`, Courier restricts all internal cryptographic operations to FIPS-approved algorithms and attempts to run on FIPS 140-2/140-3 validated cryptographic modules where the platform provides them. This section documents what that means, what is guaranteed, and where gaps remain.

#### 12.10.1 What "FIPS Mode" Means for Courier

FIPS 140-2 and 140-3 compliance is a property of **cryptographic modules**, not applications. An application achieves compliance by using only FIPS-approved algorithms through FIPS-validated modules (listed on NIST's Cryptographic Module Validation Program, CMVP). Courier cannot itself be "FIPS-validated" — it relies on the validated status of the underlying platform modules.

**What Courier guarantees in FIPS mode**:

- Only FIPS-approved algorithms are used for internal operations (AES, RSA 2048+, SHA-256+, ECDSA P-curves). Non-approved algorithms (SHA-1, MD5, DES, Blowfish, CAST5) are rejected at the application level.
- Azure Key Vault wrap/unwrap operations use a FIPS 140-2 Level 2 (software keys) or Level 3 (HSM-backed keys) validated module for all KEK operations.
- Algorithm restrictions are enforced programmatically regardless of whether the underlying OS module is running in FIPS mode.

**What Courier does not guarantee**:

- That the .NET runtime's underlying cryptographic module (CNG on Windows, OpenSSL on Linux) is running in its FIPS-validated mode. This depends on OS/container configuration (see 12.10.2). Courier detects and logs the module state at startup, but correct FIPS module configuration is an **operational responsibility**, not an application-level guarantee.
- That BouncyCastle (used for PGP format operations) is a FIPS-validated module. It is not, even when restricted to approved algorithms (see 12.10.2).
- That outbound SFTP/FTPS connections use only FIPS-approved algorithms. Partner interoperability may require non-FIPS algorithms (see 12.10.5).

**Compliance boundary**:

```
┌──────────────────────────────────────────────────────────────────────┐
│                    COURIER FIPS BOUNDARY                              │
│                                                                      │
│  ┌─────────────────────────┐   Validated module?                     │
│  │  Azure Key Vault        │   YES — CMVP #4456 (or current)        │
│  │  (KEK wrap/unwrap)      │   Level 2 (software) / Level 3 (HSM)   │
│  └─────────────────────────┘                                         │
│                                                                      │
│  ┌─────────────────────────┐   Validated module?                     │
│  │  Windows CNG            │   YES — if OS FIPS policy enabled       │
│  │  (AES-GCM, RSA, SHA)   │   CMVP #4515 (or current per OS ver)   │
│  └─────────────────────────┘                                         │
│                                                                      │
│  ┌─────────────────────────┐   Validated module?                     │
│  │  OpenSSL 3.x FIPS       │   YES — if FIPS provider correctly     │
│  │  provider (Linux)       │   installed and activated               │
│  │  (AES-GCM, RSA, SHA)   │   CMVP #4282 (or current per version)  │
│  └─────────────────────────┘                                         │
│                                                                      │
│  ┌─────────────────────────┐   Validated module?                     │
│  │  BouncyCastle           │   NO — approved algorithms only,        │
│  │  (PGP format ops)       │   no CMVP certificate                   │
│  └─────────────────────────┘                                         │
│                                                                      │
│  ┌─────────────────────────┐   Validated module?                     │
│  │  SSH.NET / FluentFTP    │   NO — transport libraries, not         │
│  │  (outbound connections) │   cryptographic modules                  │
│  └─────────────────────────┘                                         │
│                                                                      │
└──────────────────────────────────────────────────────────────────────┘
```

#### 12.10.2 Cryptographic Module Strategy

**Azure Key Vault** (KEK operations): FIPS 140-2 validated. Software-protected keys use Level 2; HSM-backed keys use Level 3. This is the strongest link in Courier's FIPS chain — the KEK never leaves a validated boundary.

**Windows CNG** (.NET on Windows): CNG is FIPS 140-2 validated (certificate varies by Windows version). To run .NET crypto through the validated module boundary, the **Windows FIPS security policy must be enabled** at the OS level:

```
# Group Policy: Computer Configuration → Windows Settings → Security Settings →
#   Local Policies → Security Options →
#   "System cryptography: Use FIPS compliant algorithms"

# Or registry:
HKLM\SYSTEM\CurrentControlSet\Control\Lsp\FipsAlgorithmPolicy\Enabled = 1
```

When this policy is enabled, .NET's `System.Security.Cryptography` types route through CNG's validated boundary. When disabled, they still use CNG but not necessarily in its validated mode.

**OpenSSL 3.x FIPS provider** (.NET on Linux / containers): OpenSSL 3.x includes a FIPS provider module that is separately validated (CMVP #4282, version-dependent). Activating it requires more than copying an `openssl.cnf` — the FIPS provider must be:

1. **Present in the container image**: The base image must include a build of OpenSSL 3.x that ships the FIPS provider module (`fips.so`). Not all distro packages include it — some require installing `openssl-fips-provider` or building from source.
2. **Self-tested**: The FIPS module runs a self-test on first activation, producing a checksum file. This self-test must pass for the module to operate in validated mode.
3. **Configured as the default provider**: `openssl.cnf` must activate the `fips` provider and optionally the `base` provider (for non-crypto operations like encoding), and set `fips` as the default property query.

```ini
# /etc/ssl/openssl.cnf (simplified — actual config depends on distro)
[openssl_init]
providers = provider_sect
alg_section = algorithm_sect

[provider_sect]
fips = fips_sect
base = base_sect

[fips_sect]
activate = 1

[base_sect]
activate = 1

[algorithm_sect]
default_properties = fips=yes
```

**Operational requirement**: The Dockerfile must use a base image where the OpenSSL FIPS provider is properly installed and self-tested. This is validated at image build time (see 14.2) and at application startup (see startup checks below). If the FIPS provider is not active, Courier logs a warning but does not refuse to start — the application-level algorithm restrictions still apply, but operations are not running through a validated module boundary.

**Startup detection**:

```csharp
// Application startup — detect and log FIPS module state
// Note: .NET does not expose a simple "is FIPS module active" API.
// We probe by checking platform indicators.

if (OperatingSystem.IsWindows())
{
    // Windows: check registry for FIPS policy
    using var key = Registry.LocalMachine.OpenSubKey(
        @"SYSTEM\CurrentControlSet\Control\Lsp\FipsAlgorithmPolicy");
    var enabled = key?.GetValue("Enabled") as int? == 1;
    logger.LogInformation("Windows FIPS policy: {State}",
        enabled ? "Enabled (CNG validated mode)" : "Disabled");
    if (!enabled)
        logger.LogWarning("Windows FIPS policy is not enabled. " +
            "Crypto operations use CNG but not in validated mode. " +
            "Courier still restricts to approved algorithms.");
}
else if (OperatingSystem.IsLinux())
{
    // Linux: probe whether OpenSSL FIPS provider is active
    try
    {
        // Attempt an operation that requires FIPS — if the provider
        // is active with default_properties=fips=yes, this succeeds.
        // A more robust check: shell out to `openssl list -providers`
        // and verify "fips" appears with status "active".
        var result = Process.Start(new ProcessStartInfo
        {
            FileName = "openssl",
            Arguments = "list -providers",
            RedirectStandardOutput = true,
        })!;
        var output = await result.StandardOutput.ReadToEndAsync();
        var fipsActive = output.Contains("name: OpenSSL FIPS Provider")
                      && output.Contains("status: active");
        logger.LogInformation("OpenSSL FIPS provider: {State}",
            fipsActive ? "Active (validated mode)" : "Not active");
        if (!fipsActive)
            logger.LogWarning("OpenSSL FIPS provider is not active. " +
                "Crypto operations use OpenSSL but not in validated mode. " +
                "Courier still restricts to approved algorithms. " +
                "See Section 12.10.2 for container configuration requirements.");
    }
    catch (Exception ex)
    {
        logger.LogWarning(ex, "Could not detect OpenSSL FIPS provider status.");
    }
}
```

**Envelope encryption specifics**:

| Operation | Module | Validated? |
|-----------|--------|-----------|
| Key wrapping (KEK) | Azure Key Vault (RSA-OAEP-256) | Yes — FIPS 140-2 Level 2/3 |
| Data encryption (DEK) | `AesGcm` → CNG (Windows) or OpenSSL (Linux) | Yes — if OS/container FIPS mode enabled |
| DEK generation | `RandomNumberGenerator` → CNG/OpenSSL CSPRNG | Yes — if OS/container FIPS mode enabled |
| IV generation | `RandomNumberGenerator` → CNG/OpenSSL CSPRNG | Yes — if OS/container FIPS mode enabled |

**PGP operations — BouncyCastle**:

The standard BouncyCastle NuGet package (`Portable.BouncyCastle` / `BouncyCastle.Cryptography`) is not FIPS-validated. The Legion of the Bouncy Castle does publish a **FIPS-certified line** (`bc-fips` for core crypto, `bcpg-fips` for OpenPGP) that carries CMVP validation. However:

- `bc-fips` and `bcpg-fips` are Java libraries. The .NET equivalents (`bc-fips-csharp`) exist but with more limited availability and documentation.
- The FIPS-certified BouncyCastle line has a different API surface and licensing model (requires a support agreement).
- Migration from standard BouncyCastle to `bc-fips-csharp` + `bcpg-fips-csharp` is a material effort that should be evaluated based on the organization's compliance requirements.

**Courier's V1 approach**: Use standard BouncyCastle configured to use only FIPS-approved algorithms (AES-256, RSA 2048+, SHA-256+). This provides algorithm compliance but not module validation for PGP operations. The `ICryptoProvider` abstraction (Section 7) isolates BouncyCastle usage, enabling a future migration to `bcpg-fips-csharp` if the organization requires validated-module PGP operations.

| PGP Operation | Algorithm Restriction | BouncyCastle Config |
|---------------|----------------------|---------------------|
| Symmetric encryption | AES-128, AES-192, AES-256 only | `SymmetricKeyAlgorithmTag.Aes256` — explicitly set, reject others |
| Asymmetric encryption | RSA 2048+ only | `PublicKeyAlgorithmTag.RsaGeneral` — reject ElGamal, DSA |
| Hashing | SHA-256, SHA-384, SHA-512 only | `HashAlgorithmTag.Sha256` minimum — reject SHA-1, MD5 |
| Signing | RSA 2048+ with SHA-256+ only | Reject SHA-1 signatures |

**Documented limitation**: PGP operations use only FIPS-approved algorithms but do not run through a FIPS-validated cryptographic module in V1. This is an accepted risk. If validated PGP operations are required, the migration path is to `bcpg-fips-csharp` behind the existing `ICryptoProvider` abstraction.

#### 12.10.3 FIPS-Approved Algorithms

**Allowed in FIPS mode** (internal operations):

| Category | Approved Algorithms |
|----------|---------------------|
| Symmetric encryption | AES-128, AES-192, AES-256 (GCM or CBC mode) |
| Asymmetric encryption | RSA 2048, 3072, 4096 |
| Key exchange | ECDH P-256, P-384, P-521 |
| Digital signatures | RSA 2048+ with SHA-256+, ECDSA P-256/P-384/P-521 |
| Hashing | SHA-256, SHA-384, SHA-512, SHA-3 |
| Key wrapping | RSA-OAEP-256 (via Azure Key Vault) |
| Random generation | CNG/OpenSSL CSPRNG (DRBG) |

**Explicitly prohibited in FIPS mode** (internal operations):

| Algorithm | Reason |
|-----------|--------|
| SHA-1 | Deprecated — collision attacks demonstrated |
| MD5 | Broken — not approved since FIPS 180-4 |
| DES / 3DES | Deprecated — insufficient key length |
| RSA < 2048 | Insufficient key length per NIST SP 800-131A |
| Ed25519 / Curve25519 | Not yet in most FIPS-validated modules (see 12.10.4) |
| Blowfish / CAST5 / IDEA | Not NIST-approved |
| ElGamal | Not NIST-approved |

#### 12.10.4 Ed25519 / Curve25519 Handling

Ed25519 and Curve25519 are modern, high-performance algorithms approved in FIPS 186-5 but not yet included in most FIPS-validated cryptographic modules (CNG, OpenSSL FIPS provider).

**Courier's approach**:

- The algorithm enums (`SshKeyType`, `PgpAlgorithm`) retain Ed25519 and Curve25519 values
- A runtime FIPS mode flag (`system_settings.fips_mode_enabled`, default: `true`) gates their availability
- When FIPS mode is enabled: Ed25519/Curve25519 key generation is rejected with error code `4011: Algorithm not available in FIPS mode`. Import of existing Ed25519 keys is allowed (they may be needed for partner connections) but flagged with a warning in the UI and audit log
- When FIPS mode is disabled: All algorithms are available without restriction
- Per-connection FIPS override: Individual connections can set `fips_override = true` to allow non-FIPS algorithms for that connection only, regardless of the global FIPS mode. This is logged as a security event

**Future**: When FIPS-validated modules include Ed25519, the restriction can be lifted by updating the FIPS algorithm allowlist without code changes.

#### 12.10.5 Connection-Level FIPS Configuration

Outbound connections (SFTP, FTP, FTPS) may need to negotiate algorithms that are not FIPS-approved to interoperate with partner servers. This is handled via a per-connection `fips_override` flag.

**Default behavior (FIPS mode enabled, no override)**:

- SFTP: SSH.NET configured to prefer FIPS-approved key exchange (ECDH P-256/P-384, `diffie-hellman-group14-sha256`, `diffie-hellman-group16-sha512`), encryption (`aes256-ctr`, `aes128-ctr`, `aes256-gcm@openssh.com`), MAC (`hmac-sha2-256`, `hmac-sha2-512`), and host key (`rsa-sha2-256`, `rsa-sha2-512`, `ecdsa-sha2-nistp256`) algorithms
- FTPS: TLS 1.2+ with FIPS-approved cipher suites only (AES-based, no RC4, no 3DES)

**With FIPS override (`fips_override = true` on connection)**:

- All algorithms supported by SSH.NET / FluentFTP are available, including `chacha20-poly1305`, `curve25519-sha256`, Ed25519 host keys
- The connection is flagged in the UI with a "Non-FIPS" badge
- Every connection using the override generates an audit event: `FipsOverrideUsed` with connection ID, negotiated algorithms, and user who configured the override

**Connection entity change**: Add `fips_override` to the connection schema:

```sql
ALTER TABLE connections ADD COLUMN fips_override BOOLEAN NOT NULL DEFAULT FALSE;
```

#### 12.10.6 FIPS Enforcement Architecture

```
┌───────────────────────────────────────────────────────────────────┐
│                      FIPS ENFORCEMENT                             │
│                                                                   │
│  Global: system_settings.fips_mode_enabled = true (default)       │
│                                                                   │
│  ┌─────────────────────────────────────────────────────────────┐ │
│  │  INTERNAL OPERATIONS (approved algorithms; validated module  │ │
│  │  depends on OS/container FIPS configuration)                │ │
│  │                                                             │ │
│  │  Encryption at rest ──► .NET AesGcm → CNG/OpenSSL          │ │
│  │  DEK generation ──────► .NET RandomNumberGenerator → ditto  │ │
│  │  Key wrapping ────────► Azure Key Vault RSA-OAEP-256 ✓     │ │
│  │  Key generation ──────► .NET RSA/ECDsa → CNG/OpenSSL       │ │
│  │  Hashing ─────────────► .NET SHA256/384/512 → CNG/OpenSSL  │ │
│  │                                                             │ │
│  │  ✓ = always validated     (others) = validated if OS FIPS on│ │
│  └─────────────────────────────────────────────────────────────┘ │
│                                                                   │
│  ┌─────────────────────────────────────────────────────────────┐ │
│  │  PGP OPERATIONS (approved algorithms only; NOT a validated  │ │
│  │  module — accepted risk, path to bcpg-fips-csharp exists)   │ │
│  │                                                             │ │
│  │  BouncyCastle ──► AES-256 only, RSA 2048+, SHA-256+        │ │
│  │                   Reject: CAST5, IDEA, SHA-1, ElGamal      │ │
│  └─────────────────────────────────────────────────────────────┘ │
│                                                                   │
│  ┌─────────────────────────────────────────────────────────────┐ │
│  │  OUTBOUND CONNECTIONS (FIPS default, per-connection override)│ │
│  │                                                             │ │
│  │  fips_override = false ──► FIPS cipher suites only          │ │
│  │  fips_override = true  ──► All algorithms + audit event     │ │
│  └─────────────────────────────────────────────────────────────┘ │
│                                                                   │
└───────────────────────────────────────────────────────────────────┘
```

#### 12.10.7 Validation & Compliance Evidence

To support FIPS compliance assessments:

- **Algorithm inventory**: The `/api/v1/settings` endpoint exposes `fips_mode_enabled` status. A future admin endpoint can return the complete list of algorithms in use across all connections and keys.
- **Module state logging**: Application startup logs record whether the underlying cryptographic module (CNG or OpenSSL FIPS provider) is running in validated mode (see 12.10.2). These logs provide auditable evidence of module state per deployment.
- **Audit trail**: All cryptographic operations are logged with the algorithm used (`algorithm` field in audit details). Auditors can query the audit log to verify no prohibited algorithms were used for internal operations.
- **FIPS override tracking**: All connections with `fips_override = true` are queryable. Audit events record every use of the override with the negotiated algorithm suite.
- **Module documentation**: This section records which cryptographic modules are used, their CMVP validation status, accepted risks (BouncyCastle standard edition), and the migration path to validated PGP operations (`bcpg-fips-csharp`).
- **Container configuration**: Dockerfiles must use a base image with the OpenSSL FIPS provider correctly installed. Validation of provider status is automated at startup (see 12.10.2), not assumed from Dockerfile alone.

#### 12.10.8 System Settings Addition

```sql
INSERT INTO system_settings (key, value, description, updated_by) VALUES
    ('security.fips_mode_enabled', 'true', 'Enforce FIPS 140-2 approved algorithms for internal operations', 'system'),
    ('security.fips_override_require_admin', 'true', 'Require Admin role to set fips_override on connections', 'system'),
    ('security.public_key_share_links_enabled', 'false', 'Allow Admins to generate unauthenticated share links for public keys', 'system'),
    ('security.max_share_link_days', '30', 'Maximum expiration period in days for public key share links', 'system'),
    ('security.insecure_trust_require_admin', 'true', 'Require Admin role to set AlwaysTrust (SSH) or Insecure (TLS) on connections', 'system'),
    ('security.insecure_trust_allow_production', 'false', 'Allow insecure trust policies in production environments', 'system');
```

When `fips_mode_enabled` is `true`:
- Key generation restricts to FIPS-approved algorithms
- BouncyCastle PGP operations are restricted to approved algorithms
- Connections without `fips_override` negotiate FIPS-only cipher suites
- Algorithm enums filter UI dropdowns to show only approved options

When `fips_override_require_admin` is `true` (default), only Admin role users can toggle `fips_override` on a connection. This prevents Operators from accidentally weakening security posture.

### 12.11 Security Summary

```
┌──────────────────────────────────────────────────────────────────┐
│                    SECURITY LAYERS                               │
│                                                                  │
│  ┌─────────────────────────────────────────────────────────────┐│
│  │  FIPS: Approved algorithms enforced; validated modules where    ││
│  │  platform provides them; per-connection override for partners  ││
│  └─────────────────────────────────────────────────────────────┘│
│  ┌─────────────────────────────────────────────────────────────┐│
│  │  Network: Private subnet, NSG allowlists, no public API    ││
│  └─────────────────────────────────────────────────────────────┘│
│  ┌─────────────────────────────────────────────────────────────┐│
│  │  Transport: TLS 1.2+ everywhere (API, DB, Key Vault, FTPS) ││
│  └─────────────────────────────────────────────────────────────┘│
│  ┌─────────────────────────────────────────────────────────────┐│
│  │  Authentication: Entra ID JWT validation (single tenant)    ││
│  └─────────────────────────────────────────────────────────────┘│
│  ┌─────────────────────────────────────────────────────────────┐│
│  │  Authorization: 3-role RBAC (Admin, Operator, Viewer)       ││
│  └─────────────────────────────────────────────────────────────┘│
│  ┌─────────────────────────────────────────────────────────────┐│
│  │  API Hardening: CORS, security headers, input validation    ││
│  └─────────────────────────────────────────────────────────────┘│
│  ┌─────────────────────────────────────────────────────────────┐│
│  │  Data at Rest: AES-256-GCM envelope encryption + Azure TDE  ││
│  └─────────────────────────────────────────────────────────────┘│
│  ┌─────────────────────────────────────────────────────────────┐│
│  │  Secrets: Key Vault (prod), User Secrets (dev), no env vars ││
│  └─────────────────────────────────────────────────────────────┘│
│  ┌─────────────────────────────────────────────────────────────┐│
│  │  Audit: Append-only log, all security events, no redaction  ││
│  └─────────────────────────────────────────────────────────────┘│
│                                                                  │
└──────────────────────────────────────────────────────────────────┘
```

### 12.12 Threat Model & Trust Boundaries

This section identifies the critical assets, likely attackers, plausible attack paths, and the mitigations Courier applies. It is not a formal STRIDE analysis but provides the "what keeps us up at night" foundation for security review.

#### 12.12.1 Trust Boundaries

```
┌───────────────────────────────────────────────────────────────────┐
│  TRUST BOUNDARY 1: Organization Perimeter                         │
│                                                                   │
│  ┌─────────────────────────────────────────────────────────────┐  │
│  │  TRUST BOUNDARY 2: Courier Internal                         │  │
│  │                                                             │  │
│  │  ┌──────────┐   ┌──────────┐   ┌──────────────────────┐    │  │
│  │  │ API Host │   │ Worker   │   │ PostgreSQL           │    │  │
│  │  │          │   │          │   │ (encrypted at rest,  │    │  │
│  │  │          │   │          │   │  private subnet)     │    │  │
│  │  └────┬─────┘   └────┬─────┘   └──────────────────────┘    │  │
│  │       │              │                                      │  │
│  └───────┼──────────────┼──────────────────────────────────────┘  │
│          │              │                                         │
│  ┌───────▼──────────────▼──────────────────────────────────────┐  │
│  │  TRUST BOUNDARY 3: Azure Managed Services                   │  │
│  │  Azure Key Vault (FIPS validated) │ Azure Blob (archive)    │  │
│  └─────────────────────────────────────────────────────────────┘  │
│                                                                   │
└───────────────────────┬───────────────────────────────────────────┘
                        │
    ════════════════════╪═══════════════════════════════  (Internet)
                        │
┌───────────────────────▼───────────────────────────────────────────┐
│  UNTRUSTED: Partner SFTP/FTP Servers                              │
│  (any host key, any certificate, any file content)                │
└───────────────────────────────────────────────────────────────────┘
```

Data crossing each boundary is subject to specific controls: authentication at boundary 1 (Entra ID), encryption at boundary 2 (AES-256-GCM envelope encryption, TLS for all connections), managed-service IAM at boundary 3, and protocol-level verification at the internet boundary (host key verification, TLS cert validation).

#### 12.12.2 Asset Inventory

| Asset | Sensitivity | Location | Primary Protection |
|-------|------------|----------|-------------------|
| Connection credentials (passwords) | Critical | PostgreSQL, encrypted with AES-256-GCM + Key Vault KEK | Envelope encryption (Section 12.3), KEK rotation, Admin-only access |
| SSH private keys | Critical | PostgreSQL, encrypted same as credentials | Envelope encryption, private key export denied by default (Section 6.8) |
| PGP private keys (passphrases) | Critical | PostgreSQL, passphrase encrypted same as credentials | Envelope encryption, private key export denied by default (Section 7.3) |
| Azure Key Vault KEK | Critical | Azure Key Vault (HSM-backed in production) | Azure RBAC, private endpoint, FIPS 140-2 Level 2/3 |
| Files in transit | High | Job temp directory, partner servers | TLS/SSH encryption in transit, temp dir cleanup, per-execution sandbox |
| Files at rest (temp) | High | `/data/courier/temp/{executionId}/` | Per-execution isolation, 7-day orphan cleanup, container-local storage |
| Audit log | High (integrity) | PostgreSQL partitioned tables | Append-only (no update/delete API), tamper detection via monthly checksums |
| User identity tokens | High | In-memory (API), never persisted | Short-lived (Entra ID default), validated on every request |
| Database connection string | High | Key Vault (prod), User Secrets (dev) | Never in env vars, never logged |
| Partner server host keys | Medium | `known_hosts` table | TOFU or Manual verification, mismatch detection |
| Job definitions | Medium | PostgreSQL | RBAC (Operator+ to create/edit), versioned, audit logged |
| Public keys (PGP/SSH) | Low | PostgreSQL, exportable | Authenticated export by default, optional share links (Section 7.3.5) |

#### 12.12.3 Threat Scenarios

| # | Attacker | Target Asset | Attack Path | Impact | Mitigation | Section |
|---|----------|-------------|-------------|--------|------------|---------|
| T1 | **External: Network MITM** | Files in transit, credentials | Intercept SFTP/FTPS connection to partner server via DNS spoofing or ARP poisoning | Credential theft, file exfiltration, file injection | SSH host key verification (TOFU/Manual), TLS cert validation (SystemTrust/PinnedThumbprint). `AlwaysTrust`/`Insecure` policies are Admin-only, FIPS-blocked, production-blocked, and audited per use. | 6.7, 6.3.2 |
| T2 | **External: Malicious archive** | Worker filesystem, downstream systems | Upload a crafted archive (via partner SFTP or manual upload) containing Zip Slip paths, decompression bombs, or symlink exploits | Disk exhaustion, arbitrary file overwrite, sandbox escape | Path traversal validation, symlink rejection, decompression limits (20 GB / 10K files / 200:1 ratio), no recursive extraction, post-extraction sweep. | 8.1.8 |
| T3 | **Insider: Compromised Operator** | Connection credentials, private keys | Use legitimate Operator role access to export credentials or reconfigure jobs to exfiltrate files | Credential exfiltration, unauthorized data access | Operators cannot export private keys (Admin-only). Operators cannot set `AlwaysTrust` or `Insecure` trust policies (Admin-only). All credential access is audit-logged. Connection passwords are never returned in API responses. | 7.3.5, 12.2 |
| T4 | **Insider: Compromised Admin** | Anything | Admin has full system access. Could disable FIPS mode, enable `AlwaysTrust` everywhere, export keys, create insecure connections. | Full system compromise | Admin actions are the highest-audited events. FIPS override, insecure trust policies, key exports, and share link creation all generate audit events. Audit log is append-only with no delete API. Admin cannot suppress audit entries. External SIEM integration (V2) provides tamper-resistant audit storage. | 12.7 |
| T5 | **External: SQL injection** | Database (all data) | Inject SQL via API parameters | Full database read/write | EF Core parameterized queries exclusively. No raw SQL from user input. JSONB serialized through typed C# objects. | 12.6.4 |
| T6 | **External: Command injection via 7z** | Worker filesystem | Craft a filename containing shell metacharacters that gets passed to the 7z CLI | Arbitrary command execution | No shell invocation (`UseShellExecute = false`), `ArgumentList` (not string interpolation), filename sanitization, absolute binary path, sandbox isolation. | 8.1.2 |
| T7 | **External: Token theft** | User session, API access | Steal Entra ID access token via XSS, token leak in logs, or intercepted API call | Impersonation, unauthorized actions as victim | CORS locked to single origin, security headers (CSP, X-Frame-Options), tokens never logged (sensitive data redaction), short-lived tokens (1hr), sessionStorage only (not localStorage), TLS required. See 11.2.2 for SPA-specific auth security. | 11.2.2, 12.6.1, 12.6.2, 12.8 |
| T8 | **Operational: FIPS policy bypass** | Compliance posture | Admin enables `fips_override` on connections or disables `fips_mode_enabled` globally | Non-compliant algorithm negotiation in audited environment | Every FIPS override generates `FipsOverrideUsed` audit event with negotiated algorithms. Global FIPS toggle change is audit-logged. Non-FIPS connections display a badge in the UI. Compliance reporting query surfaces all overrides. | 12.10.5 |
| T9 | **Operational: Key exfiltration** | PGP/SSH private keys | Admin creates share link for public key (low risk) or exports private key (high risk) | Partner impersonation, decryption of intercepted files | Private key export denied by default. Share links are for public keys only, disabled by default, Admin-only, time-limited (max 30 days), revocable, and download-counted. All actions audit-logged. | 7.3.5 |
| T10 | **External: Denial of service** | API availability | Flood the API with requests or queue excessive jobs | API unresponsive, job queue backed up | Rate limiting (Section 12.6), concurrency semaphore (Section 5.8), queue depth monitoring, request size limits (10 MB body). | 12.6.3 |
| T11 | **External/Insider: Process memory dump** | DEKs, plaintext credentials during use | Attacker with container access dumps Worker process memory to extract in-flight secrets | Credential and key material exposure for any operations active at dump time | KEK never in process memory (only in Key Vault HSM boundary). DEKs exist only for the duration of a single encrypt/decrypt call and are zeroed via `CryptographicOperations.ZeroMemory`. No DEK cache, no credential cache, no pre-decrypted store. Blast radius limited to operations in-flight at the exact moment of the dump. See Section 7.3.7 anti-patterns. | 7.3.7 |

#### 12.12.4 Accepted Risks (V1)

These are known risks accepted in V1 with documented mitigations and V2 plans:

| Risk | Severity | Justification | V2 Mitigation |
|------|----------|---------------|---------------|
| BouncyCastle is not a FIPS-validated module | Medium | Algorithm compliance is enforced; only module validation is missing. Acceptable if the organization's compliance requirement is algorithm-level, not module-level. | Migrate to `bcpg-fips-csharp` behind `ICryptoProvider`. |
| Single Worker instance is a single point of failure | Medium | Crash recovery is automatic (restart + re-pick queued jobs). Running jobs are lost and must be re-triggered. Acceptable for V1 internal workload. | Quartz cluster mode + event-driven scheduling enables horizontal Worker scaling. |
| Audit log is stored in the same database as application data | Medium | A compromised database admin could theoretically delete audit entries. Append-only API prevents application-level tampering, but not DBA-level. | External SIEM integration (immutable log shipping). |
| Admin role has no separation of duties | Medium | A single Admin can both configure insecure settings and suppress the UI warnings (though not the audit entries). Acceptable for small teams. | Introduce "Security Admin" role distinct from "System Admin" with approval workflows for security-sensitive changes. |
| No intrusion detection on partner file content | Low | Files received from partners are transferred as opaque blobs. Courier does not scan for malware or data loss prevention. | Integration with Azure Defender or ClamAV for file content scanning. |

---

## 13. Database Schema

This section defines the complete PostgreSQL schema for Courier. All tables use `snake_case` naming. Migrations are managed via **DbUp** with raw SQL scripts, not EF Core migrations. EF Core is used only as an ORM for querying and change tracking, with entity mappings configured to match this schema.

### 13.1 Migration Strategy

**DbUp** runs numbered SQL scripts on **API host startup only** (the Worker validates schema version but does not execute migrations — see 13.1.1). Scripts are embedded resources in a dedicated `Courier.Migrations` project and executed in order.

**Naming convention**: `XXXX_description.sql` (e.g., `0001_initial_schema.sql`, `0002_add_tags.sql`)

**DbUp configuration**:

```csharp
var upgrader = DeployChanges.To
    .PostgresqlDatabase(connectionString)
    .WithScriptsEmbeddedInAssembly(typeof(MigrationMarker).Assembly)
    .WithTransactionPerScript()
    .LogToConsole()
    .Build();

var result = upgrader.PerformUpgrade();
```

DbUp tracks executed scripts in a `schema_versions` table. Scripts are idempotent where possible. Destructive changes (column removal, table drops) are never included in the same release as the code that stops using them — they follow a two-release deprecation cycle.

#### 13.1.1 Migration Safety in Multi-Replica Deployments

When running multiple API or Worker instances (even temporarily during a rolling deployment), database migrations must not run concurrently and must not leave the schema in an inconsistent state.

**Which host runs migrations**: Only the **API host** runs migrations. The Worker host does not include the `MigrationRunner` hosted service in its DI registration. This is a deliberate design decision: the Worker should be able to start (or restart) without blocking on schema changes, and migrations should be coupled to the deployment of the API which owns the schema contract.

```csharp
// Courier.Api/Program.cs — API only
builder.Services.AddHostedService<MigrationRunner>();

// Courier.Worker/Program.cs — Worker does NOT register MigrationRunner
// Worker validates schema version on startup instead:
builder.Services.AddHostedService<SchemaVersionValidator>();
```

**SchemaVersionValidator** (Worker only): On startup, the Worker reads the `schema_versions` table and compares the highest applied migration against its expected minimum version (compiled into the Worker binary). If the database schema is behind, the Worker logs a fatal error and refuses to start, with a message directing the operator to deploy the API first. This prevents the Worker from operating against an incompatible schema.

**Concurrent migration prevention**: The `MigrationRunner` acquires a PostgreSQL advisory lock (`pg_advisory_lock(12345)`) before executing any scripts. This is a session-level lock — only one connection can hold it at a time. If a second API instance starts while the first is still migrating, the second blocks on the advisory lock until the first completes, then discovers all scripts are already applied and proceeds to start normally. See Section 14.6 for the full implementation.

**What happens if a migration fails mid-deploy**:

1. DbUp uses `WithTransactionPerScript()`, so each individual script runs in its own transaction. A failed script rolls back its own changes — the database is left in the state after the last successful script.
2. The `MigrationRunner` throws an exception, which prevents the API host from starting. The container enters a crash loop (or health check failure), which is the correct behavior — a partially-migrated API should not serve traffic.
3. The `schema_versions` table records which scripts have been applied. On the next deployment attempt (with the failing script fixed), DbUp skips already-applied scripts and retries from the failed one.
4. The advisory lock is released in a `finally` block, so even a crash releases it (PostgreSQL also auto-releases advisory locks when the session disconnects).

**Failure recovery procedure**: If a migration fails, the operator should: (1) check the API container logs for the specific script and SQL error, (2) fix the migration script, (3) redeploy. There is no manual rollback mechanism — DbUp is forward-only. If a rollback is needed, it must be a new forward migration that reverses the changes.

**Deployment ordering**: In a rolling deployment, the correct order is: (1) deploy new API hosts (which run migrations), (2) deploy new Worker hosts (which validate schema version). If the Worker is deployed first, it will refuse to start if it expects a newer schema than what's currently applied. This is safe — it just means the Worker restarts once the API has migrated the schema.

### 13.2 Conventions

- **Primary keys**: `id UUID DEFAULT gen_random_uuid()` on all tables
- **Timestamps**: `TIMESTAMPTZ` for all date/time columns, stored as UTC
- **Soft delete**: `is_deleted BOOLEAN DEFAULT FALSE` and `deleted_at TIMESTAMPTZ` on all major entities. A partial index `WHERE NOT is_deleted` is applied to commonly queried tables
- **JSONB**: Used for flexible/nested data (step configuration, audit details, directory listings)
- **Foreign keys**: Named `fk_{table}_{referenced_table}` with explicit `ON DELETE` behavior
- **Indexes**: Named `ix_{table}_{columns}`
- **Constraints**: Named `ck_{table}_{description}`
- **Partitioning**: Range partitioning by month on high-volume tables (audit_log_entries, domain_events, job_executions, step_executions, monitor_file_log)

### 13.3 Schema DDL

#### 13.3.1 Job System Tables

```sql
-- ============================================================
-- JOBS
-- ============================================================
CREATE TABLE jobs (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name                TEXT NOT NULL,
    description         TEXT,
    current_version     INT NOT NULL DEFAULT 1,
    failure_policy      JSONB NOT NULL DEFAULT '{"type":"stop","max_retries":3,"backoff_base_seconds":1,"backoff_max_seconds":60}',
    is_enabled          BOOLEAN NOT NULL DEFAULT TRUE,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT now(),
    is_deleted          BOOLEAN NOT NULL DEFAULT FALSE,
    deleted_at          TIMESTAMPTZ
);

CREATE INDEX ix_jobs_name ON jobs (name) WHERE NOT is_deleted;
CREATE INDEX ix_jobs_is_enabled ON jobs (is_enabled) WHERE NOT is_deleted;

-- ============================================================
-- JOB STEPS
-- ============================================================
CREATE TABLE job_steps (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    job_id              UUID NOT NULL,
    step_order          INT NOT NULL,
    name                TEXT NOT NULL,
    type_key            TEXT NOT NULL,
    configuration       JSONB NOT NULL DEFAULT '{}',
    timeout_seconds     INT NOT NULL DEFAULT 300,

    CONSTRAINT fk_job_steps_jobs FOREIGN KEY (job_id)
        REFERENCES jobs (id) ON DELETE CASCADE,
    CONSTRAINT ck_job_steps_order_positive CHECK (step_order >= 0),
    CONSTRAINT ck_job_steps_timeout_positive CHECK (timeout_seconds > 0)
);

CREATE UNIQUE INDEX ix_job_steps_job_order ON job_steps (job_id, step_order);
CREATE INDEX ix_job_steps_type_key ON job_steps (type_key);

-- ============================================================
-- JOB VERSIONS
-- ============================================================
CREATE TABLE job_versions (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    job_id              UUID NOT NULL,
    version_number      INT NOT NULL,
    config_snapshot     JSONB NOT NULL,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT now(),
    created_by          TEXT NOT NULL,

    CONSTRAINT fk_job_versions_jobs FOREIGN KEY (job_id)
        REFERENCES jobs (id) ON DELETE CASCADE
);

CREATE UNIQUE INDEX ix_job_versions_job_version ON job_versions (job_id, version_number);

-- ============================================================
-- JOB EXECUTIONS (PARTITIONED)
-- ============================================================
CREATE TABLE job_executions (
    id                  UUID NOT NULL DEFAULT gen_random_uuid(),
    job_id              UUID NOT NULL,
    job_version_number  INT NOT NULL,
    chain_execution_id  UUID,
    triggered_by        TEXT NOT NULL,
    state               TEXT NOT NULL DEFAULT 'created',
    queued_at           TIMESTAMPTZ,
    started_at          TIMESTAMPTZ,
    completed_at        TIMESTAMPTZ,
    context_snapshot    JSONB DEFAULT '{}',
    created_at          TIMESTAMPTZ NOT NULL DEFAULT now(),

    CONSTRAINT pk_job_executions PRIMARY KEY (id, created_at),
    CONSTRAINT ck_job_executions_state CHECK (
        state IN ('created', 'queued', 'running', 'paused', 'completed', 'failed', 'cancelled')
    )
) PARTITION BY RANGE (created_at);

-- Partition creation is handled by a scheduled maintenance script (see 13.5)
-- Example: CREATE TABLE job_executions_2026_02 PARTITION OF job_executions
--          FOR VALUES FROM ('2026-02-01') TO ('2026-03-01');

CREATE INDEX ix_job_executions_job_id ON job_executions (job_id, created_at DESC);
CREATE INDEX ix_job_executions_state ON job_executions (state, created_at DESC);
CREATE INDEX ix_job_executions_chain ON job_executions (chain_execution_id, created_at DESC)
    WHERE chain_execution_id IS NOT NULL;
CREATE INDEX ix_job_executions_queued ON job_executions (queued_at)
    WHERE state = 'queued';

-- ============================================================
-- STEP EXECUTIONS (PARTITIONED)
-- ============================================================
CREATE TABLE step_executions (
    id                  UUID NOT NULL DEFAULT gen_random_uuid(),
    job_execution_id    UUID NOT NULL,
    job_step_id         UUID NOT NULL,
    step_order          INT NOT NULL,
    state               TEXT NOT NULL DEFAULT 'pending',
    started_at          TIMESTAMPTZ,
    completed_at        TIMESTAMPTZ,
    duration_ms         BIGINT,
    bytes_processed     BIGINT,
    output_data         JSONB,
    error_message       TEXT,
    error_stack_trace   TEXT,
    retry_attempt       INT NOT NULL DEFAULT 0,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT now(),

    CONSTRAINT pk_step_executions PRIMARY KEY (id, created_at),
    CONSTRAINT ck_step_executions_state CHECK (
        state IN ('pending', 'running', 'completed', 'failed', 'skipped')
    )
) PARTITION BY RANGE (created_at);

CREATE INDEX ix_step_executions_job_execution ON step_executions (job_execution_id, step_order);
CREATE INDEX ix_step_executions_state ON step_executions (state, created_at DESC);

-- ============================================================
-- JOB CHAINS
-- ============================================================
CREATE TABLE job_chains (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name                TEXT NOT NULL,
    description         TEXT,
    is_enabled          BOOLEAN NOT NULL DEFAULT TRUE,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT now(),
    is_deleted          BOOLEAN NOT NULL DEFAULT FALSE,
    deleted_at          TIMESTAMPTZ
);

CREATE INDEX ix_job_chains_name ON job_chains (name) WHERE NOT is_deleted;

-- ============================================================
-- JOB CHAIN MEMBERS
-- ============================================================
CREATE TABLE job_chain_members (
    id                      UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    chain_id                UUID NOT NULL,
    job_id                  UUID NOT NULL,
    execution_order         INT NOT NULL,
    depends_on_member_id    UUID,
    run_on_upstream_failure BOOLEAN NOT NULL DEFAULT FALSE,

    CONSTRAINT fk_chain_members_chains FOREIGN KEY (chain_id)
        REFERENCES job_chains (id) ON DELETE CASCADE,
    CONSTRAINT fk_chain_members_jobs FOREIGN KEY (job_id)
        REFERENCES jobs (id) ON DELETE RESTRICT,
    CONSTRAINT fk_chain_members_depends FOREIGN KEY (depends_on_member_id)
        REFERENCES job_chain_members (id) ON DELETE SET NULL
);

CREATE UNIQUE INDEX ix_chain_members_chain_order ON job_chain_members (chain_id, execution_order);
CREATE INDEX ix_chain_members_job ON job_chain_members (job_id);

-- ============================================================
-- CHAIN EXECUTIONS
-- ============================================================
CREATE TABLE chain_executions (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    chain_id            UUID NOT NULL,
    triggered_by        TEXT NOT NULL,
    state               TEXT NOT NULL DEFAULT 'pending',
    started_at          TIMESTAMPTZ,
    completed_at        TIMESTAMPTZ,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT now(),

    CONSTRAINT fk_chain_executions_chains FOREIGN KEY (chain_id)
        REFERENCES job_chains (id) ON DELETE CASCADE,
    CONSTRAINT ck_chain_executions_state CHECK (
        state IN ('pending', 'running', 'completed', 'failed', 'paused', 'cancelled')
    )
);

CREATE INDEX ix_chain_executions_chain ON chain_executions (chain_id, created_at DESC);
CREATE INDEX ix_chain_executions_state ON chain_executions (state);

-- ============================================================
-- JOB DEPENDENCIES
-- ============================================================
CREATE TABLE job_dependencies (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    upstream_job_id     UUID NOT NULL,
    downstream_job_id   UUID NOT NULL,
    run_on_failure      BOOLEAN NOT NULL DEFAULT FALSE,

    CONSTRAINT fk_job_deps_upstream FOREIGN KEY (upstream_job_id)
        REFERENCES jobs (id) ON DELETE CASCADE,
    CONSTRAINT fk_job_deps_downstream FOREIGN KEY (downstream_job_id)
        REFERENCES jobs (id) ON DELETE CASCADE,
    CONSTRAINT ck_job_deps_no_self_ref CHECK (upstream_job_id != downstream_job_id)
);

CREATE UNIQUE INDEX ix_job_deps_pair ON job_dependencies (upstream_job_id, downstream_job_id);
CREATE INDEX ix_job_deps_downstream ON job_dependencies (downstream_job_id);

-- ============================================================
-- JOB SCHEDULES
-- ============================================================
CREATE TABLE job_schedules (
    id               UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    job_id           UUID NOT NULL,
    schedule_type    TEXT NOT NULL,              -- 'cron' | 'one_shot'
    cron_expression  TEXT,
    run_at           TIMESTAMPTZ,
    is_enabled       BOOLEAN NOT NULL DEFAULT TRUE,
    last_fired_at    TIMESTAMPTZ,
    next_fire_at     TIMESTAMPTZ,
    created_at       TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at       TIMESTAMPTZ NOT NULL DEFAULT now(),

    CONSTRAINT ck_schedule_type CHECK (schedule_type IN ('cron', 'one_shot')),
    CONSTRAINT ck_schedule_cron CHECK (
        (schedule_type = 'cron' AND cron_expression IS NOT NULL) OR
        (schedule_type = 'one_shot' AND run_at IS NOT NULL)
    ),
    CONSTRAINT fk_schedules_jobs FOREIGN KEY (job_id)
        REFERENCES jobs (id) ON DELETE CASCADE
);

CREATE INDEX ix_job_schedules_job_id ON job_schedules (job_id);
CREATE INDEX ix_job_schedules_enabled ON job_schedules (is_enabled, schedule_type);

-- ============================================================
-- CHAIN SCHEDULES
-- ============================================================
CREATE TABLE chain_schedules (
    id               UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    chain_id         UUID NOT NULL,
    schedule_type    TEXT NOT NULL,              -- 'cron' | 'one_shot'
    cron_expression  TEXT,
    run_at           TIMESTAMPTZ,
    is_enabled       BOOLEAN NOT NULL DEFAULT TRUE,
    last_fired_at    TIMESTAMPTZ,
    next_fire_at     TIMESTAMPTZ,
    created_at       TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at       TIMESTAMPTZ NOT NULL DEFAULT now(),

    CONSTRAINT ck_chain_schedule_type CHECK (schedule_type IN ('cron', 'one_shot')),
    CONSTRAINT ck_chain_schedule_cron CHECK (
        (schedule_type = 'cron' AND cron_expression IS NOT NULL) OR
        (schedule_type = 'one_shot' AND run_at IS NOT NULL)
    ),
    CONSTRAINT fk_chain_schedules_chains FOREIGN KEY (chain_id)
        REFERENCES job_chains (id) ON DELETE CASCADE
);

CREATE INDEX ix_chain_schedules_chain_id ON chain_schedules (chain_id);
CREATE INDEX ix_chain_schedules_enabled ON chain_schedules (is_enabled, schedule_type);
```

#### 13.3.2 Connection Tables

```sql
-- ============================================================
-- CONNECTIONS
-- ============================================================
CREATE TABLE connections (
    id                      UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name                    TEXT NOT NULL,
    "group"                 TEXT,
    protocol                TEXT NOT NULL,
    host                    TEXT NOT NULL,
    port                    INT NOT NULL,
    auth_method             TEXT NOT NULL,
    username                TEXT NOT NULL,
    password_encrypted      BYTEA,
    ssh_key_id              UUID,
    host_key_policy         TEXT NOT NULL DEFAULT 'trust_on_first_use',
    stored_host_fingerprint TEXT,
    passive_mode            BOOLEAN NOT NULL DEFAULT TRUE,
    tls_version_floor       TEXT,
    tls_cert_policy         TEXT NOT NULL DEFAULT 'system_trust',
    tls_pinned_thumbprint   TEXT,
    ssh_algorithms          JSONB,
    connect_timeout_sec     INT NOT NULL DEFAULT 30,
    operation_timeout_sec   INT NOT NULL DEFAULT 300,
    keepalive_interval_sec  INT NOT NULL DEFAULT 60,
    transport_retries       INT NOT NULL DEFAULT 2,
    status                  TEXT NOT NULL DEFAULT 'active',
    fips_override           BOOLEAN NOT NULL DEFAULT FALSE,
    notes                   TEXT,
    created_at              TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at              TIMESTAMPTZ NOT NULL DEFAULT now(),
    is_deleted              BOOLEAN NOT NULL DEFAULT FALSE,
    deleted_at              TIMESTAMPTZ,

    CONSTRAINT ck_connections_protocol CHECK (protocol IN ('sftp', 'ftp', 'ftps')),
    CONSTRAINT ck_connections_auth CHECK (auth_method IN ('password', 'ssh_key', 'password_and_ssh_key')),
    CONSTRAINT ck_connections_host_key CHECK (host_key_policy IN ('trust_on_first_use', 'always_trust', 'manual')),
    CONSTRAINT ck_connections_tls_cert CHECK (tls_cert_policy IN ('system_trust', 'pinned_thumbprint', 'insecure')),
    CONSTRAINT ck_connections_status CHECK (status IN ('active', 'disabled')),
    CONSTRAINT ck_connections_retries CHECK (transport_retries BETWEEN 0 AND 3),
    CONSTRAINT fk_connections_ssh_keys FOREIGN KEY (ssh_key_id)
        REFERENCES ssh_keys (id) ON DELETE SET NULL
);

CREATE INDEX ix_connections_name ON connections (name) WHERE NOT is_deleted;
CREATE INDEX ix_connections_group ON connections ("group") WHERE NOT is_deleted AND "group" IS NOT NULL;
CREATE INDEX ix_connections_protocol ON connections (protocol) WHERE NOT is_deleted;

-- ============================================================
-- KNOWN HOSTS
-- ============================================================
CREATE TABLE known_hosts (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    connection_id       UUID NOT NULL,
    fingerprint         TEXT NOT NULL,
    key_type            TEXT NOT NULL,
    first_seen          TIMESTAMPTZ NOT NULL DEFAULT now(),
    last_seen           TIMESTAMPTZ NOT NULL DEFAULT now(),
    approved_by         TEXT NOT NULL,

    CONSTRAINT fk_known_hosts_connections FOREIGN KEY (connection_id)
        REFERENCES connections (id) ON DELETE CASCADE
);

CREATE UNIQUE INDEX ix_known_hosts_connection_fingerprint ON known_hosts (connection_id, fingerprint);
```

#### 13.3.3 Key Store Tables

```sql
-- ============================================================
-- PGP KEYS
-- ============================================================
CREATE TABLE pgp_keys (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name                TEXT NOT NULL,
    fingerprint         TEXT NOT NULL,
    short_key_id        TEXT NOT NULL,
    algorithm           TEXT NOT NULL,
    key_type            TEXT NOT NULL,
    purpose             TEXT,
    status              TEXT NOT NULL DEFAULT 'active',
    public_key_data     TEXT NOT NULL,
    private_key_data    BYTEA,
    passphrase_hash     TEXT,
    expires_at          TIMESTAMPTZ,
    successor_key_id    UUID,
    created_by          TEXT NOT NULL,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT now(),
    is_deleted          BOOLEAN NOT NULL DEFAULT FALSE,
    deleted_at          TIMESTAMPTZ,

    CONSTRAINT ck_pgp_keys_algorithm CHECK (
        algorithm IN ('rsa_2048', 'rsa_3072', 'rsa_4096', 'ecc_curve25519', 'ecc_p256', 'ecc_p384')
    ),
    CONSTRAINT ck_pgp_keys_type CHECK (key_type IN ('public_only', 'key_pair')),
    CONSTRAINT ck_pgp_keys_status CHECK (
        status IN ('active', 'expiring', 'retired', 'revoked', 'deleted')
    ),
    CONSTRAINT fk_pgp_keys_successor FOREIGN KEY (successor_key_id)
        REFERENCES pgp_keys (id) ON DELETE SET NULL
);

CREATE UNIQUE INDEX ix_pgp_keys_fingerprint ON pgp_keys (fingerprint) WHERE NOT is_deleted;
CREATE INDEX ix_pgp_keys_status ON pgp_keys (status) WHERE NOT is_deleted;
CREATE INDEX ix_pgp_keys_expires ON pgp_keys (expires_at)
    WHERE status IN ('active', 'expiring') AND expires_at IS NOT NULL;

-- ============================================================
-- SSH KEYS
-- ============================================================
CREATE TABLE ssh_keys (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name                TEXT NOT NULL,
    key_type            TEXT NOT NULL,
    public_key_data     TEXT NOT NULL,
    private_key_data    BYTEA NOT NULL,
    passphrase_hash     TEXT,
    fingerprint         TEXT NOT NULL,
    status              TEXT NOT NULL DEFAULT 'active',
    notes               TEXT,
    created_by          TEXT NOT NULL,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT now(),
    is_deleted          BOOLEAN NOT NULL DEFAULT FALSE,
    deleted_at          TIMESTAMPTZ,

    CONSTRAINT ck_ssh_keys_type CHECK (
        key_type IN ('rsa_2048', 'rsa_4096', 'ed25519', 'ecdsa_256')
    ),
    CONSTRAINT ck_ssh_keys_status CHECK (status IN ('active', 'retired', 'deleted'))
);

CREATE UNIQUE INDEX ix_ssh_keys_fingerprint ON ssh_keys (fingerprint) WHERE NOT is_deleted;
CREATE INDEX ix_ssh_keys_status ON ssh_keys (status) WHERE NOT is_deleted;

-- ============================================================
-- PUBLIC KEY SHARE TOKENS
-- ============================================================
CREATE TABLE public_key_share_tokens (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    token_hash          TEXT NOT NULL,           -- SHA-256 hash of the random token
    key_type            TEXT NOT NULL,            -- 'pgp' or 'ssh'
    key_id              UUID NOT NULL,
    expires_at          TIMESTAMPTZ NOT NULL,
    created_by          TEXT NOT NULL,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT now(),
    revoked_at          TIMESTAMPTZ,
    download_count      INT NOT NULL DEFAULT 0,

    CONSTRAINT ck_share_key_type CHECK (key_type IN ('pgp', 'ssh'))
);

CREATE UNIQUE INDEX ix_share_tokens_hash ON public_key_share_tokens (token_hash);
CREATE INDEX ix_share_tokens_key ON public_key_share_tokens (key_type, key_id);
```

#### 13.3.4 File Monitor Tables

```sql
-- ============================================================
-- FILE MONITORS
-- ============================================================
CREATE TABLE file_monitors (
    id                          UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name                        TEXT NOT NULL,
    description                 TEXT,
    watch_target                JSONB NOT NULL,
    trigger_events              INT NOT NULL,
    file_patterns               JSONB NOT NULL DEFAULT '[]',
    polling_interval_sec        INT NOT NULL DEFAULT 60,
    stability_window_sec        INT NOT NULL DEFAULT 5,
    batch_mode                  BOOLEAN NOT NULL DEFAULT TRUE,
    max_consecutive_failures    INT NOT NULL DEFAULT 5,
    consecutive_failure_count   INT NOT NULL DEFAULT 0,
    state                       TEXT NOT NULL DEFAULT 'active',
    created_at                  TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_at                  TIMESTAMPTZ NOT NULL DEFAULT now(),
    is_deleted                  BOOLEAN NOT NULL DEFAULT FALSE,
    deleted_at                  TIMESTAMPTZ,

    CONSTRAINT ck_monitors_state CHECK (state IN ('active', 'degraded', 'paused', 'disabled', 'error')),
    CONSTRAINT ck_monitors_polling CHECK (polling_interval_sec >= 30),
    CONSTRAINT ck_monitors_stability CHECK (stability_window_sec >= 1)
);

CREATE INDEX ix_monitors_state ON file_monitors (state) WHERE NOT is_deleted;

-- ============================================================
-- MONITOR JOB BINDINGS
-- ============================================================
CREATE TABLE monitor_job_bindings (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    monitor_id          UUID NOT NULL,
    job_id              UUID,
    chain_id            UUID,

    CONSTRAINT ck_binding_target CHECK (
        (job_id IS NOT NULL AND chain_id IS NULL) OR
        (job_id IS NULL AND chain_id IS NOT NULL)
    ),
    CONSTRAINT fk_bindings_monitors FOREIGN KEY (monitor_id)
        REFERENCES file_monitors (id) ON DELETE CASCADE,
    CONSTRAINT fk_bindings_jobs FOREIGN KEY (job_id)
        REFERENCES jobs (id) ON DELETE CASCADE,
    CONSTRAINT fk_bindings_chains FOREIGN KEY (chain_id)
        REFERENCES job_chains (id) ON DELETE CASCADE
);

CREATE INDEX ix_bindings_monitor ON monitor_job_bindings (monitor_id);

-- ============================================================
-- MONITOR DIRECTORY STATE
-- ============================================================
CREATE TABLE monitor_directory_state (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    monitor_id          UUID NOT NULL,
    directory_listing   JSONB NOT NULL,
    captured_at         TIMESTAMPTZ NOT NULL DEFAULT now(),

    CONSTRAINT fk_dir_state_monitors FOREIGN KEY (monitor_id)
        REFERENCES file_monitors (id) ON DELETE CASCADE
);

-- Only keep latest state per monitor; old snapshots are pruned
CREATE INDEX ix_dir_state_monitor ON monitor_directory_state (monitor_id, captured_at DESC);

-- ============================================================
-- MONITOR FILE LOG (PARTITIONED)
-- ============================================================
CREATE TABLE monitor_file_log (
    id                  UUID NOT NULL DEFAULT gen_random_uuid(),
    monitor_id          UUID NOT NULL,
    file_path           TEXT NOT NULL,
    file_size           BIGINT NOT NULL,
    file_hash           TEXT,
    last_modified       TIMESTAMPTZ NOT NULL,
    triggered_at        TIMESTAMPTZ NOT NULL DEFAULT now(),
    execution_id        UUID NOT NULL,

    CONSTRAINT pk_monitor_file_log PRIMARY KEY (id, triggered_at)
) PARTITION BY RANGE (triggered_at);

CREATE INDEX ix_monitor_file_log_monitor ON monitor_file_log (monitor_id, file_path, triggered_at DESC);
CREATE INDEX ix_monitor_file_log_execution ON monitor_file_log (execution_id);
```

#### 13.3.5 Cross-Cutting Tables

```sql
-- ============================================================
-- TAGS
-- ============================================================
CREATE TABLE tags (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    name                TEXT NOT NULL,
    color               TEXT,
    category            TEXT,
    description         TEXT,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT now(),
    is_deleted          BOOLEAN NOT NULL DEFAULT FALSE,
    deleted_at          TIMESTAMPTZ
);

CREATE UNIQUE INDEX ix_tags_name ON tags (LOWER(name)) WHERE NOT is_deleted;
CREATE INDEX ix_tags_category ON tags (category) WHERE NOT is_deleted AND category IS NOT NULL;

-- ============================================================
-- ENTITY TAGS (POLYMORPHIC JOIN)
-- ============================================================
CREATE TABLE entity_tags (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    tag_id              UUID NOT NULL,
    entity_type         TEXT NOT NULL,
    entity_id           UUID NOT NULL,

    CONSTRAINT fk_entity_tags_tags FOREIGN KEY (tag_id)
        REFERENCES tags (id) ON DELETE CASCADE,
    CONSTRAINT ck_entity_tags_type CHECK (
        entity_type IN ('job', 'job_chain', 'connection', 'pgp_key', 'ssh_key', 'file_monitor')
    )
);

CREATE UNIQUE INDEX ix_entity_tags_unique ON entity_tags (tag_id, entity_type, entity_id);
CREATE INDEX ix_entity_tags_entity ON entity_tags (entity_type, entity_id);

-- ============================================================
-- AUDIT LOG (PARTITIONED)
-- ============================================================
CREATE TABLE audit_log_entries (
    id                  UUID NOT NULL DEFAULT gen_random_uuid(),
    entity_type         TEXT NOT NULL,
    entity_id           UUID NOT NULL,
    operation           TEXT NOT NULL,
    performed_by        TEXT NOT NULL,
    performed_at        TIMESTAMPTZ NOT NULL DEFAULT now(),
    details             JSONB DEFAULT '{}',

    CONSTRAINT pk_audit_log PRIMARY KEY (id, performed_at),
    CONSTRAINT ck_audit_entity_type CHECK (
        entity_type IN ('job', 'job_execution', 'step_execution', 'chain',
                        'chain_execution', 'connection', 'pgp_key', 'ssh_key', 'file_monitor')
    )
) PARTITION BY RANGE (performed_at);

CREATE INDEX ix_audit_entity ON audit_log_entries (entity_type, entity_id, performed_at DESC);
CREATE INDEX ix_audit_performed_at ON audit_log_entries (performed_at DESC);
CREATE INDEX ix_audit_performed_by ON audit_log_entries (performed_by, performed_at DESC);

-- ============================================================
-- DOMAIN EVENTS (PARTITIONED)
-- ============================================================
CREATE TABLE domain_events (
    id                  UUID NOT NULL DEFAULT gen_random_uuid(),
    event_type          TEXT NOT NULL,
    entity_type         TEXT NOT NULL,
    entity_id           UUID NOT NULL,
    payload             JSONB NOT NULL,
    occurred_at         TIMESTAMPTZ NOT NULL DEFAULT now(),
    processed_at        TIMESTAMPTZ,
    processed_by        TEXT,

    CONSTRAINT pk_domain_events PRIMARY KEY (id, occurred_at)
) PARTITION BY RANGE (occurred_at);

CREATE INDEX ix_domain_events_unprocessed ON domain_events (occurred_at)
    WHERE processed_at IS NULL;
CREATE INDEX ix_domain_events_type ON domain_events (event_type, occurred_at DESC);
CREATE INDEX ix_domain_events_entity ON domain_events (entity_type, entity_id, occurred_at DESC);

-- ============================================================
-- SYSTEM SETTINGS
-- ============================================================
CREATE TABLE system_settings (
    key                 TEXT PRIMARY KEY,
    value               TEXT NOT NULL,
    description         TEXT,
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_by          TEXT NOT NULL
);

-- Seed default settings
INSERT INTO system_settings (key, value, description, updated_by) VALUES
    ('job.concurrency_limit', '5', 'Maximum concurrent job executions', 'system'),
    ('job.temp_cleanup_days', '7', 'Days before orphaned temp directories are purged', 'system'),
    ('monitor.file_log_retention_days', '30', 'Days before monitor file log entries are pruned', 'system'),
    ('key.expiration_warning_days', '30', 'Days before expiration to transition keys to Expiring', 'system'),
    ('audit.partition_retention_months', '12', 'Months of audit log partitions to retain', 'system'),
    ('security.fips_mode_enabled', 'true', 'Enforce FIPS 140-2 approved algorithms for internal operations', 'system'),
    ('security.fips_override_require_admin', 'true', 'Require Admin role to set fips_override on connections', 'system'),
    ('security.public_key_share_links_enabled', 'false', 'Allow Admins to generate unauthenticated share links for public keys', 'system'),
    ('security.max_share_link_days', '30', 'Maximum expiration period in days for public key share links', 'system'),
    ('security.insecure_trust_require_admin', 'true', 'Require Admin role to set AlwaysTrust (SSH) or Insecure (TLS) on connections', 'system'),
    ('security.insecure_trust_allow_production', 'false', 'Allow insecure trust policies in production environments', 'system');
```

#### 13.3.6 Quartz.NET Scheduler Tables

Quartz.NET uses the **AdoJobStore** for persistent scheduling, which requires its own set of tables in the database. These tables store trigger definitions, cron expressions, job details, and scheduler state so that schedules survive application restarts.

Quartz.NET maintains an official PostgreSQL DDL script that creates approximately 12 tables with the `QRTZ_` prefix:

`QRTZ_JOB_DETAILS`, `QRTZ_TRIGGERS`, `QRTZ_CRON_TRIGGERS`, `QRTZ_SIMPLE_TRIGGERS`, `QRTZ_SIMPROP_TRIGGERS`, `QRTZ_BLOB_TRIGGERS`, `QRTZ_CALENDARS`, `QRTZ_PAUSED_TRIGGER_GRPS`, `QRTZ_FIRED_TRIGGERS`, `QRTZ_SCHEDULER_STATE`, `QRTZ_LOCKS`

**Source**: The official script is available in the Quartz.NET repository at `database/tables/tables_postgres.sql` and should be referenced from the latest stable release matching the NuGet package version used in the project.

**DbUp integration**: The Quartz DDL is included as the second migration script, executed immediately after the Courier schema:

```
0001_initial_schema.sql          -- Courier tables (sections 13.3.1–13.3.5)
0002_quartz_scheduler.sql        -- Quartz.NET tables (copied from official source)
0003_seed_system_settings.sql    -- Default system settings
```

The `0002_quartz_scheduler.sql` script is a direct copy of the official Quartz.NET PostgreSQL DDL with one modification: a `QRTZ_` table prefix scoped to a scheduler instance name (default: `CourierScheduler`) to avoid collisions if the database is shared with other applications.

**Quartz.NET configuration**:

```csharp
services.AddQuartz(q =>
{
    q.SchedulerId = "CourierScheduler";
    q.UsePersistentStore(store =>
    {
        store.UsePostgres(connectionString);
        store.UseNewtonsoftJsonSerializer();
    });
});
```

**Important**: These tables are managed entirely by Quartz.NET. Courier code never reads from or writes to `QRTZ_*` tables directly — all interaction goes through the Quartz.NET `IScheduler` API. The `job_schedules` and `chain_schedules` tables in the Courier schema (Section 13.3.1) are the application-level representation of schedules; Quartz.NET tables are the runtime execution layer. Job schedules use the `"courier"` Quartz group, while chain schedules use the `"courier-chains"` group to avoid collision.

### 13.4 EF Core Mapping Configuration

Although migrations are managed via DbUp, EF Core is used as the ORM. Entity mappings must match the snake_case schema exactly. Use the `Npgsql.EntityFrameworkCore.PostgreSQL` provider with the snake_case naming convention:

```csharp
services.AddDbContext<CourierDbContext>(options =>
    options.UseNpgsql(connectionString, npgsql =>
    {
        npgsql.MigrationsHistoryTable("__ef_migrations_history");  // Not used, but configured
    })
    .UseSnakeCaseNamingConvention());  // via EFCore.NamingConventions package
```

**Key configuration points:**

```csharp
public class CourierDbContext : DbContext
{
    // Aggregate roots
    public DbSet<Job> Jobs => Set<Job>();
    public DbSet<JobExecution> JobExecutions => Set<JobExecution>();
    public DbSet<JobChain> JobChains => Set<JobChain>();
    public DbSet<ChainExecution> ChainExecutions => Set<ChainExecution>();
    public DbSet<Connection> Connections => Set<Connection>();
    public DbSet<PgpKey> PgpKeys => Set<PgpKey>();
    public DbSet<SshKey> SshKeys => Set<SshKey>();
    public DbSet<FileMonitor> FileMonitors => Set<FileMonitor>();
    public DbSet<Tag> Tags => Set<Tag>();
    public DbSet<AuditLogEntry> AuditLogEntries => Set<AuditLogEntry>();
    public DbSet<DomainEvent> DomainEvents => Set<DomainEvent>();
    public DbSet<SystemSetting> SystemSettings => Set<SystemSetting>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        // Global soft delete filter on all major entities
        modelBuilder.Entity<Job>().HasQueryFilter(j => !j.IsDeleted);
        modelBuilder.Entity<JobChain>().HasQueryFilter(c => !c.IsDeleted);
        modelBuilder.Entity<Connection>().HasQueryFilter(c => !c.IsDeleted);
        modelBuilder.Entity<PgpKey>().HasQueryFilter(k => !k.IsDeleted);
        modelBuilder.Entity<SshKey>().HasQueryFilter(k => !k.IsDeleted);
        modelBuilder.Entity<FileMonitor>().HasQueryFilter(m => !m.IsDeleted);
        modelBuilder.Entity<Tag>().HasQueryFilter(t => !t.IsDeleted);

        // JSONB column mappings
        modelBuilder.Entity<Job>()
            .Property(j => j.FailurePolicy)
            .HasColumnType("jsonb");

        modelBuilder.Entity<JobStep>()
            .Property(s => s.Configuration)
            .HasColumnType("jsonb");

        modelBuilder.Entity<FileMonitor>()
            .Property(m => m.WatchTarget)
            .HasColumnType("jsonb");

        modelBuilder.Entity<FileMonitor>()
            .Property(m => m.FilePatterns)
            .HasColumnType("jsonb");

        modelBuilder.Entity<Connection>()
            .Property(c => c.SshAlgorithms)
            .HasColumnType("jsonb");

        // "group" is a reserved word — explicit column mapping
        modelBuilder.Entity<Connection>()
            .Property(c => c.Group)
            .HasColumnName("group");

        // SystemSetting uses Key as PK, not Id
        modelBuilder.Entity<SystemSetting>()
            .HasKey(s => s.Key);
    }
}
```

### 13.5 Partition Management

Partitioned tables require monthly partitions to be created in advance. A scheduled DbUp-compatible maintenance script or background service handles this:

```sql
-- ============================================================
-- Partition creation function
-- Called monthly by a scheduled job or on application startup
-- ============================================================
CREATE OR REPLACE FUNCTION create_monthly_partitions(
    target_date DATE DEFAULT CURRENT_DATE + INTERVAL '1 month'
)
RETURNS void AS $$
DECLARE
    partition_start DATE := date_trunc('month', target_date);
    partition_end DATE := partition_start + INTERVAL '1 month';
    suffix TEXT := to_char(partition_start, 'YYYY_MM');
BEGIN
    -- Job executions
    EXECUTE format(
        'CREATE TABLE IF NOT EXISTS job_executions_%s PARTITION OF job_executions
         FOR VALUES FROM (%L) TO (%L)',
        suffix, partition_start, partition_end
    );

    -- Step executions
    EXECUTE format(
        'CREATE TABLE IF NOT EXISTS step_executions_%s PARTITION OF step_executions
         FOR VALUES FROM (%L) TO (%L)',
        suffix, partition_start, partition_end
    );

    -- Monitor file log
    EXECUTE format(
        'CREATE TABLE IF NOT EXISTS monitor_file_log_%s PARTITION OF monitor_file_log
         FOR VALUES FROM (%L) TO (%L)',
        suffix, partition_start, partition_end
    );

    -- Audit log
    EXECUTE format(
        'CREATE TABLE IF NOT EXISTS audit_log_entries_%s PARTITION OF audit_log_entries
         FOR VALUES FROM (%L) TO (%L)',
        suffix, partition_start, partition_end
    );

    -- Domain events
    EXECUTE format(
        'CREATE TABLE IF NOT EXISTS domain_events_%s PARTITION OF domain_events
         FOR VALUES FROM (%L) TO (%L)',
        suffix, partition_start, partition_end
    );
END;
$$ LANGUAGE plpgsql;

-- Create partitions for current month and next 2 months on first run
SELECT create_monthly_partitions(CURRENT_DATE);
SELECT create_monthly_partitions(CURRENT_DATE + INTERVAL '1 month');
SELECT create_monthly_partitions(CURRENT_DATE + INTERVAL '2 months');
```

A background service in Courier (`PartitionMaintenanceService`) runs weekly and calls `create_monthly_partitions` for the upcoming month to ensure partitions always exist ahead of time. If a partition is missing when data is inserted, PostgreSQL raises an error — the service creates partitions 2 months in advance to prevent this.

### 13.6 Data Retention & Archival

High-volume partitioned tables support retention policies configured via `system_settings`:

```sql
-- ============================================================
-- Partition drop function for data retention
-- Drops partitions older than the configured retention period
-- ============================================================
CREATE OR REPLACE FUNCTION drop_old_partitions(
    table_name TEXT,
    retention_months INT
)
RETURNS void AS $$
DECLARE
    cutoff_date DATE := date_trunc('month', CURRENT_DATE - (retention_months || ' months')::INTERVAL);
    suffix TEXT := to_char(cutoff_date - INTERVAL '1 month', 'YYYY_MM');
    partition_name TEXT := table_name || '_' || suffix;
BEGIN
    -- Only drop if the partition exists
    IF EXISTS (
        SELECT 1 FROM pg_tables
        WHERE tablename = partition_name
    ) THEN
        EXECUTE format('DROP TABLE %I', partition_name);
        RAISE NOTICE 'Dropped partition: %', partition_name;
    END IF;
END;
$$ LANGUAGE plpgsql;
```

Before dropping, the `PartitionMaintenanceService` optionally exports the partition data to cold storage (e.g., Azure Blob Storage as compressed CSV) for compliance. The export-then-drop flow is:

1. Export partition to compressed file: `COPY (SELECT * FROM audit_log_entries_2025_01) TO '/tmp/audit_2025_01.csv.gz' WITH CSV HEADER`
2. Upload to Azure Blob Storage
3. Drop the partition

This is configurable per table. Job execution and audit data typically have longer retention (12+ months) while monitor file logs can be shorter (3–6 months).

#### 13.6.1 Partition Maintenance Failure Modes

The `PartitionMaintenanceService` is a background job, and background jobs fail. If partition creation or archival stops working, the consequences are predictable and must be planned for.

**Failure: Partition creation stops (service down, migration broken, Worker crashed)**

If `create_monthly_partitions` fails to run, existing partitions continue to work until the last pre-created partition's date range is exhausted. Since partitions are created 2 months in advance, the system has a ~2-month grace period before inserts begin failing.

When the grace period expires, any `INSERT` into a partitioned table (audit log, job execution, step execution, domain event, monitor file log) raises a PostgreSQL error: `ERROR: no partition of relation "audit_log_entries" found for row`. This is a hard failure — the row is lost, and the calling code receives an exception. For audit log entries, this means audit data is silently dropped unless the caller retries. For job executions, the job fails to record its state transition.

**Mitigation**: The `PartitionMaintenanceService` runs weekly and creates partitions 2 months ahead, providing a large failure window. A health check endpoint (`/health/partitions`) verifies that the next 2 months of partitions exist and returns `Unhealthy` if any are missing. The alerting system (V2) should monitor this health check. As a belt-and-suspenders defense, the API startup also runs `create_monthly_partitions` for the current and next month as part of the migration runner, so a fresh deployment always ensures the immediate future is covered.

**Failure: Archival stops (Azure Blob unreachable, export query fails, disk full)**

If the export-then-drop flow fails, the `PartitionMaintenanceService` retains the partition — it never drops a partition that hasn't been successfully exported. The partition continues to exist in PostgreSQL, consuming disk and contributing to query planner overhead.

**Disk and bloat impact of deferred archival:**

| Duration | Estimated Additional Disk | Query Impact | Risk Level |
|----------|--------------------------|-------------|------------|
| 1 month overdue | ~1–4 GB per table (varies by audit volume) | Negligible — planner still prunes efficiently | Low |
| 3 months overdue | ~5–15 GB total across all partitioned tables | Measurable — cross-partition queries (e.g., audit search without date filter) scan more partitions. VACUUM on parent table takes longer. | Medium |
| 6+ months overdue | ~15–40 GB total; autovacuum may fall behind on oldest partitions | Significant — `pg_stat_user_tables.n_dead_tup` grows on stale partitions if they had updates before cutoff. Index bloat on partition-local indexes accumulates. Backup size increases. PITR restore time increases. | High |
| 12 months overdue | Doubles expected database size | Query planner considers all partitions for unfiltered queries. Backup/restore times may exceed RTO. Disk pressure on Azure Flexible Server triggers autoscale or alerts. | Critical |

**Autovacuum interaction**: Partitions that are no longer receiving writes (past months) settle into a steady state where autovacuum has little to do. But if those partitions had in-progress job executions when the month rolled over (state updates crossing the partition boundary), they accumulate dead tuples from those final updates. PostgreSQL's autovacuum eventually cleans these, but with many stale partitions, the cumulative autovacuum load can delay vacuum runs on actively-written partitions, causing live table bloat.

**Recovery procedure**: If archival is overdue, the operator should: (1) fix the underlying issue (Blob Storage connectivity, disk space), (2) manually trigger the archival for each overdue month via `POST /api/v1/admin/partitions/{table}/{month}/archive`, (3) verify the export in Blob Storage, (4) let the service drop the archived partitions on its next run. The `PartitionMaintenanceService` processes months in chronological order, so it will catch up month by month.

**Monitoring**: The dashboard exposes partition health: oldest retained partition date, partition count per table, estimated size per partition (via `pg_total_relation_size`), and export status (exported / pending / failed). A system setting `partition.archive_overdue_warning_months` (default: 2) triggers a warning in the health check when any table has partitions older than retention + warning threshold.

### 13.7 Performance Considerations

#### 13.7.1 JSONB Column Index Strategy

Courier uses JSONB columns across 10 tables. Most JSONB columns should **not** have GIN indexes by default — GIN indexes are expensive to maintain (every key/value in the JSON document is indexed) and only justified by specific, frequent query patterns. The principle is: start without GIN indexes, add them when query profiling shows a need, and prefer `jsonb_path_ops` (supports `@>` containment queries only) over the default `jsonb_ops` (supports `@>`, `?`, `?|`, `?&` but is ~3× larger).

**JSONB column inventory and index recommendations:**

| Table | Column | Typical Size | Queried? | Recommended Index | Rationale |
|-------|--------|-------------|----------|-------------------|-----------|
| `jobs` | `failure_policy` | ~100 bytes | Read on execution, never queried by content | **None** | Always read by `job_id`, never filtered by policy fields |
| `job_steps` | `configuration` | 200–2000 bytes | Read on execution, never queried by content | **None** | Step config is read whole when the step executes; searching "find all steps that upload to host X" is not a V1 use case |
| `job_executions` | `config_snapshot` | 500–5000 bytes | Read on audit review | **None** | Historical snapshot, only accessed by `execution_id` |
| `job_executions` | `context_snapshot` | 200–5000 bytes | Read on resume | **None** | Accessed by `execution_id` for checkpoint/resume, never filtered by content |
| `step_executions` | `details` | 100–2000 bytes | Queried for troubleshooting (error search) | **Deferred** | Useful for "find all steps that failed with error containing 'timeout'" — add if troubleshooting queries become frequent |
| `connections` | `ssh_algorithms` | 200–500 bytes | Read on connection, never filtered | **None** | Always read by `connection_id` |
| `monitors` | `watch_target` | 100–300 bytes | Read on monitor start | **None** | Always read by `monitor_id` |
| `monitors` | `file_patterns` | 50–200 bytes | Read on event match | **None** | Evaluated in application code, not SQL |
| `monitor_directory_state` | `directory_listing` | 1–100 KB | Read per poll, never queried by content | **None** | Full listing is always read by `monitor_id`; diffing happens in C# |
| `audit_log_entries` | `details` | 100–2000 bytes | **Yes — audit search** | **GIN with `jsonb_path_ops`** | Audit queries filter by nested fields: "all events for connection X", "all events with bytes_transferred > threshold" |
| `domain_events` | `payload` | 200–2000 bytes | V2 event replay, not V1 | **None (V1), GIN (V2)** | V2 outbox relay needs `@>` queries; V1 events are fire-and-forget |

**Recommended V1 indexes** (only audit_log_entries):

```sql
-- GIN index on audit details for subsystem-specific queries
-- jsonb_path_ops is smaller and faster than default ops, sufficient for @> queries
CREATE INDEX ix_audit_details_gin ON audit_log_entries
    USING GIN (details jsonb_path_ops);
```

This enables queries like:

```sql
-- Find all audit entries for a specific connection
SELECT * FROM audit_log_entries
WHERE details @> '{"connection_id": "a1b2c3d4-..."}'
  AND performed_at >= '2026-01-01'
  AND performed_at < '2026-02-01';

-- Find all file transfers exceeding 1 GB
SELECT * FROM audit_log_entries
WHERE details @> '{"operation": "file_transfer"}'
  AND (details->>'bytes_transferred')::bigint > 1073741824
  AND performed_at >= '2026-01-01';
```

Note: the second query uses a cast expression that cannot be optimized by GIN — only the `@>` containment check uses the index; the size filter is applied as a post-filter. For frequent size-based queries, a functional B-tree index on the extracted field would be more efficient:

```sql
-- Only add if "transfers larger than X" is a frequent audit query
CREATE INDEX ix_audit_bytes ON audit_log_entries (((details->>'bytes_transferred')::bigint))
    WHERE details ? 'bytes_transferred';
```

**Index size considerations**: A GIN `jsonb_path_ops` index on the audit log is roughly 20–40% the size of the underlying data, depending on JSON document complexity. For 12 months of audit data at ~10 entries/second, this is approximately 2–8 GB of index space. Partition-local indexes keep individual partition indexes smaller (each month's index is built independently), and dropping old partitions drops their indexes too.

#### 13.7.2 General Performance Notes

**Partial indexes**: Used extensively on `is_deleted` columns to keep index sizes small. The `WHERE NOT is_deleted` filter means deleted records don't bloat indexes used for normal operations.

**Connection pooling**: Aspire configures Npgsql connection pooling. Default pool size of 20 is sufficient for the expected load. The pool is shared across the Job Engine, File Monitor service, and API layer.

**Vacuum and analyze**: PostgreSQL's autovacuum handles most maintenance. For partitioned tables with high churn (step_executions, audit_log_entries), consider increasing `autovacuum_vacuum_scale_factor` on those tables to trigger more frequent cleanup. See Section 13.6.1 for the interaction between stale partitions and autovacuum.

---

## 14. Deployment & Infrastructure

Courier is deployed to **Azure Container Apps** across four environments. Docker images are built and pushed via **GitHub Actions**. Local development uses **.NET Aspire** for service orchestration.

### 14.1 Environments

| Environment | Purpose | Infrastructure | Database | Key Vault | Deployment Trigger |
|------------|---------|----------------|----------|-----------|-------------------|
| **Local** | Developer workstation | .NET Aspire + Docker Compose | PostgreSQL container (Testcontainers or Aspire) | Azure CLI credentials to shared dev Key Vault | Manual (`dotnet run` / `aspire run`) |
| **Dev** | Integration testing, feature branch validation | Azure Container Apps (single replica each) | Azure PG Flex (Basic tier, 1 vCore) | Shared dev Key Vault | Push to `main` or manually triggered |
| **Staging** | Pre-production validation, QA, performance testing | Azure Container Apps (mirrors prod replica count) | Azure PG Flex (General Purpose, mirrors prod tier) | Staging Key Vault (separate from dev/prod) | Promotion from Dev (manual approval gate) |
| **Production** | Live system | Azure Container Apps (scaled per 14.4) | Azure PG Flex (General Purpose, HA enabled) | Production Key Vault (FIPS 140-2 Level 2+) | Promotion from Staging (manual approval gate) |

**Environment isolation**: Each environment (Dev, Staging, Production) has its own Azure resource group, Container Apps Environment, PostgreSQL instance, and Key Vault. No shared infrastructure between Staging and Production.

### 14.2 Docker Images

Three Docker images are built from the repository:

#### 14.2.1 API Host

```dockerfile
# Courier.Api.Dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution and restore
COPY Courier.sln .
COPY src/Courier.Api/*.csproj src/Courier.Api/
COPY src/Courier.Features/*.csproj src/Courier.Features/
COPY src/Courier.Infrastructure/*.csproj src/Courier.Infrastructure/
COPY src/Courier.Domain/*.csproj src/Courier.Domain/
RUN dotnet restore src/Courier.Api/Courier.Api.csproj

# Copy source and publish
COPY src/ src/
RUN dotnet publish src/Courier.Api/Courier.Api.csproj \
    -c Release -o /app --no-restore

# Runtime image
FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# FIPS: Base image must include OpenSSL 3.x with FIPS provider (fips.so)
# installed and self-tested. See Section 12.10.2 for full requirements.
# This config file activates the provider — but the module must already exist.
COPY infra/docker/openssl-fips.cnf /etc/ssl/openssl.cnf
ENV OPENSSL_CONF=/etc/ssl/openssl.cnf
# Build-time validation (non-blocking — logged as warning if unavailable)
RUN openssl list -providers 2>/dev/null | grep -q "FIPS" \
    && echo "FIPS provider: available" \
    || echo "WARNING: FIPS provider not found in base image — see Section 12.10.2"

COPY --from=build /app .
EXPOSE 8080
HEALTHCHECK --interval=30s --timeout=5s --start-period=10s \
    CMD curl -f http://localhost:8080/health || exit 1
ENTRYPOINT ["dotnet", "Courier.Api.dll"]
```

#### 14.2.2 Worker Host

```dockerfile
# Courier.Worker.Dockerfile
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Courier.sln .
COPY src/Courier.Worker/*.csproj src/Courier.Worker/
COPY src/Courier.Features/*.csproj src/Courier.Features/
COPY src/Courier.Infrastructure/*.csproj src/Courier.Infrastructure/
COPY src/Courier.Domain/*.csproj src/Courier.Domain/
RUN dotnet restore src/Courier.Worker/Courier.Worker.csproj

COPY src/ src/
RUN dotnet publish src/Courier.Worker/Courier.Worker.csproj \
    -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS runtime
WORKDIR /app

# 7z CLI for archive operations
RUN apt-get update && apt-get install -y --no-install-recommends p7zip-full curl && \
    rm -rf /var/lib/apt/lists/*

# FIPS: Same requirements as API host — see Section 12.10.2
COPY infra/docker/openssl-fips.cnf /etc/ssl/openssl.cnf
ENV OPENSSL_CONF=/etc/ssl/openssl.cnf
RUN openssl list -providers 2>/dev/null | grep -q "FIPS" \
    && echo "FIPS provider: available" \
    || echo "WARNING: FIPS provider not found in base image — see Section 12.10.2"

# Temp directory for job execution (mount volume in production)
RUN mkdir -p /data/courier/temp && chown -R app:app /data/courier
VOLUME ["/data/courier/temp"]

COPY --from=build /app .
HEALTHCHECK --interval=30s --timeout=5s --start-period=10s \
    CMD curl -f http://localhost:8081/health || exit 1
ENTRYPOINT ["dotnet", "Courier.Worker.dll"]
```

#### 14.2.3 Frontend

> **Updated 2026-03-15**: Changed from static export + nginx to standalone Node.js server. See Section 11.2 for rationale.

```dockerfile
# Courier.Frontend.Dockerfile — Next.js Standalone
FROM node:22-alpine AS deps
WORKDIR /app
COPY src/Courier.Frontend/package.json src/Courier.Frontend/package-lock.json ./
RUN npm ci

FROM node:22-alpine AS build
WORKDIR /app
COPY --from=deps /app/node_modules ./node_modules
COPY src/Courier.Frontend/ .

ARG NEXT_PUBLIC_API_URL=http://localhost:5000
ENV NEXT_PUBLIC_API_URL=${NEXT_PUBLIC_API_URL}

RUN npm run build    # next build → standalone server in .next/standalone/

FROM node:22-alpine AS runtime
WORKDIR /app
ENV NODE_ENV=production PORT=3000 HOSTNAME=0.0.0.0

COPY --from=build /app/.next/standalone ./
COPY --from=build /app/.next/static ./.next/static
COPY --from=build /app/public ./public

EXPOSE 3000
HEALTHCHECK --interval=30s --timeout=5s --start-period=10s --retries=3 \
    CMD wget -q --spider http://localhost:3000 || exit 1
CMD ["node", "server.js"]
```

The standalone output produces a self-contained Node.js server (~150MB image) that handles all routing natively — no nginx or SPA fallback configuration required. `NEXT_PUBLIC_API_URL` is baked into the JS bundle at build time, so the frontend image must be rebuilt per environment.

### 14.3 Azure Container Apps Configuration

Each environment deploys three Container Apps within a shared **Container Apps Environment** connected to a VNet.

#### 14.3.1 Container App Definitions

```yaml
# infra/container-apps/api.yaml
name: courier-api
properties:
  configuration:
    activeRevisionsMode: Single
    ingress:
      external: false          # Internal only — fronted by API gateway / Front Door
      targetPort: 8080
      transport: http
    secrets:
      - name: db-connection-string
        keyVaultUrl: https://{vault}.vault.azure.net/secrets/db-connection-string
        identity: system
      - name: appinsights-connection-string
        keyVaultUrl: https://{vault}.vault.azure.net/secrets/appinsights-connection-string
        identity: system
  template:
    containers:
      - name: courier-api
        image: couriercr.azurecr.io/courier-api:{tag}
        resources:
          cpu: 1.0
          memory: 2Gi
        env:
          - name: ConnectionStrings__CourierDb
            secretRef: db-connection-string
          - name: KeyVault__Uri
            value: https://{vault}.vault.azure.net
          - name: ApplicationInsights__ConnectionString
            secretRef: appinsights-connection-string
          - name: ASPNETCORE_ENVIRONMENT
            value: Production
        probes:
          - type: Liveness
            httpGet:
              path: /health
              port: 8080
            initialDelaySeconds: 10
            periodSeconds: 30
          - type: Readiness
            httpGet:
              path: /health/ready
              port: 8080
            initialDelaySeconds: 5
            periodSeconds: 10
    scale:
      minReplicas: 2
      maxReplicas: 6
      rules:
        - name: http-scaling
          http:
            metadata:
              concurrentRequests: "50"
```

```yaml
# infra/container-apps/worker.yaml
name: courier-worker
properties:
  configuration:
    activeRevisionsMode: Single
    # No ingress — worker has no external HTTP traffic
    secrets:
      - name: db-connection-string
        keyVaultUrl: https://{vault}.vault.azure.net/secrets/db-connection-string
        identity: system
      - name: appinsights-connection-string
        keyVaultUrl: https://{vault}.vault.azure.net/secrets/appinsights-connection-string
        identity: system
  template:
    containers:
      - name: courier-worker
        image: couriercr.azurecr.io/courier-worker:{tag}
        resources:
          cpu: 2.0
          memory: 4Gi
        env:
          - name: ConnectionStrings__CourierDb
            secretRef: db-connection-string
          - name: KeyVault__Uri
            value: https://{vault}.vault.azure.net
          - name: ApplicationInsights__ConnectionString
            secretRef: appinsights-connection-string
          - name: DOTNET_ENVIRONMENT
            value: Production
        volumeMounts:
          - volumeName: temp-storage
            mountPath: /data/courier/temp
        probes:
          - type: Liveness
            httpGet:
              path: /health
              port: 8081
            initialDelaySeconds: 15
            periodSeconds: 30
    volumes:
      - name: temp-storage
        storageType: AzureFile
        storageName: courier-temp
    scale:
      minReplicas: 1
      maxReplicas: 1          # Single instance in V1 (see Section 15)
```

```yaml
# infra/container-apps/frontend.yaml
name: courier-frontend
properties:
  configuration:
    activeRevisionsMode: Single
    ingress:
      external: true          # User-facing
      targetPort: 80
      transport: http
  template:
    containers:
      - name: courier-frontend
        image: couriercr.azurecr.io/courier-frontend:{tag}
        resources:
          cpu: 0.25
          memory: 0.5Gi
    scale:
      minReplicas: 2
      maxReplicas: 4
      rules:
        - name: http-scaling
          http:
            metadata:
              concurrentRequests: "100"
```

#### 14.3.2 Networking

```
┌──────────────────────────────────────────────────────────────┐
│                    Azure Container Apps Environment           │
│                    (VNet integrated)                          │
│                                                              │
│  ┌─────────────────┐                                         │
│  │  courier-frontend│◄──── Azure Front Door / CDN (HTTPS)    │
│  │  (external)      │      TLS termination, WAF              │
│  └────────┬────────┘                                         │
│           │ internal                                         │
│  ┌────────▼────────┐                                         │
│  │  courier-api     │◄──── Frontend calls via internal FQDN  │
│  │  (internal)      │      http://courier-api.internal.{env}  │
│  └────────┬────────┘                                         │
│           │                                                  │
│  ┌────────┴────────┐                                         │
│  │  courier-worker  │      No ingress — outbound only        │
│  │  (no ingress)    │                                         │
│  └─────────────────┘                                         │
│                                                              │
└──────────┬───────────────────┬───────────────────┬──────────┘
           │                   │                   │
    ┌──────▼──────┐    ┌──────▼──────┐    ┌──────▼──────┐
    │  PostgreSQL  │    │  Key Vault   │    │  Partner    │
    │  Flex Server │    │  (private    │    │  SFTP/FTP   │
    │  (private    │    │   endpoint)  │    │  servers    │
    │   endpoint)  │    │              │    │  (outbound  │
    │              │    │              │    │   NSG rules) │
    └─────────────┘    └─────────────┘    └─────────────┘
```

- **PostgreSQL**: Accessible only via private endpoint within the VNet. No public access.
- **Key Vault**: Accessible via private endpoint or service endpoint within the VNet.
- **Partner servers**: Outbound connections allowed through NSG rules with destination IP allowlists per partner.
- **Container Apps internal**: API host is internal-only ingress. Frontend proxies to API via the Container Apps Environment internal DNS.
- **External access**: Only the frontend has external ingress, fronted by Azure Front Door for TLS termination, WAF, and DDoS protection.

#### 14.3.3 Managed Identity

All three Container Apps use **system-assigned managed identities** for Azure resource access:

| Resource | Access Method |
|----------|--------------|
| Azure Key Vault | Managed Identity with `Key Vault Secrets User` + `Key Vault Crypto User` roles |
| Azure Container Registry | Managed Identity with `AcrPull` role |
| Azure Blob Storage (archives) | Managed Identity with `Storage Blob Data Contributor` role |
| Application Insights | Connection string from Key Vault (no identity needed) |

No service principal passwords or connection strings on disk. `DefaultAzureCredential` in the .NET applications resolves to managed identity automatically in Container Apps.

### 14.4 Scaling Strategy

| Container App | Min Replicas | Max Replicas | Scaling Rule | Rationale |
|--------------|-------------|-------------|--------------|-----------|
| `courier-api` | 2 | 6 | 50 concurrent requests per replica | Stateless — scales horizontally. Two minimum for availability. |
| `courier-worker` | 1 | 1 | None (fixed) | V1 single-instance. Quartz AdoJobStore supports clustered mode for V2. |
| `courier-frontend` | 2 | 4 | 100 concurrent requests per replica | Static files — very lightweight. Two minimum for availability. |

**Worker single-instance constraint**: The Worker host runs Quartz.NET, file monitors, and maintenance jobs. In V1, a single instance simplifies work claiming — although `FOR UPDATE SKIP LOCKED` (Section 5.8) prevents duplicate pickup, single-instance avoids edge cases around monitor deduplication and partition maintenance concurrency. See Section 2.7 for the throughput ceiling this implies. Quartz's `AdoJobStore` clustered mode plus the V2 event-driven architecture (Section 15) enables horizontal Worker scaling.

### 14.5 CI/CD Pipeline (GitHub Actions)

#### 14.5.1 Pipeline Overview

```
┌─────────────┐     ┌─────────────┐     ┌─────────────┐     ┌─────────────┐
│   PR Check   │────►│  Build &    │────►│  Deploy to   │────►│  Deploy to   │
│              │     │  Push Images │     │  Staging     │     │  Production  │
│  • Build     │     │              │     │              │     │              │
│  • Unit tests│     │  Trigger:    │     │  Trigger:    │     │  Trigger:    │
│  • Lint      │     │  push to     │     │  manual      │     │  manual      │
│  • Arch tests│     │  main        │     │  approval    │     │  approval    │
│              │     │              │     │              │     │              │
└─────────────┘     └─────────────┘     └─────────────┘     └─────────────┘
```

#### 14.5.2 PR Check Workflow

Runs on every pull request targeting `main`:

```yaml
# .github/workflows/pr-check.yml
name: PR Check
on:
  pull_request:
    branches: [main]

jobs:
  build-and-test:
    runs-on: ubuntu-latest
    services:
      postgres:
        image: postgres:16
        env:
          POSTGRES_PASSWORD: test
          POSTGRES_DB: courier_test
        ports: ["5432:5432"]
        options: >-
          --health-cmd pg_isready
          --health-interval 10s
          --health-timeout 5s
          --health-retries 5

    steps:
      - uses: actions/checkout@v4

      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: "10.0.x"

      - name: Restore
        run: dotnet restore

      - name: Build
        run: dotnet build --no-restore -c Release

      - name: Unit Tests
        run: dotnet test tests/Courier.Tests.Unit --no-build -c Release

      - name: Architecture Tests
        run: dotnet test tests/Courier.Tests.Architecture --no-build -c Release

      - name: Integration Tests
        run: dotnet test tests/Courier.Tests.Integration --no-build -c Release
        env:
          ConnectionStrings__CourierDb: "Host=localhost;Database=courier_test;Username=postgres;Password=test"

      - name: Vulnerability Scan
        run: dotnet list package --vulnerable --include-transitive 2>&1 | tee vuln-report.txt
        continue-on-error: true

      - name: Frontend Lint & Type Check
        working-directory: src/Courier.Frontend
        run: |
          npm ci
          npm run lint
          npm run type-check

      - name: Frontend Build
        working-directory: src/Courier.Frontend
        run: npm run build
        env:
          NEXT_PUBLIC_API_BASE_URL: http://localhost:5000/api/v1
          NEXT_PUBLIC_ENTRA_CLIENT_ID: test-client-id
          NEXT_PUBLIC_ENTRA_TENANT_ID: test-tenant-id
          NEXT_PUBLIC_REDIRECT_URI: http://localhost:3000
```

#### 14.5.3 Build & Deploy Workflow

Runs on push to `main`. Builds Docker images, pushes to Azure Container Registry, deploys to Dev automatically, then promotes to Staging and Production with manual approval gates:

```yaml
# .github/workflows/deploy.yml
name: Build & Deploy
on:
  push:
    branches: [main]
  workflow_dispatch:          # Manual trigger

env:
  REGISTRY: couriercr.azurecr.io
  TAG: ${{ github.sha }}

jobs:
  build-images:
    runs-on: ubuntu-latest
    permissions:
      id-token: write         # OIDC for Azure login
      contents: read
    steps:
      - uses: actions/checkout@v4

      - uses: azure/login@v2
        with:
          client-id: ${{ secrets.AZURE_CLIENT_ID }}
          tenant-id: ${{ secrets.AZURE_TENANT_ID }}
          subscription-id: ${{ secrets.AZURE_SUBSCRIPTION_ID }}

      - uses: azure/docker-login@v2
        with:
          login-server: ${{ env.REGISTRY }}

      - name: Build & Push API
        run: |
          docker build -f infra/docker/Courier.Api.Dockerfile -t $REGISTRY/courier-api:$TAG .
          docker push $REGISTRY/courier-api:$TAG

      - name: Build & Push Worker
        run: |
          docker build -f infra/docker/Courier.Worker.Dockerfile -t $REGISTRY/courier-worker:$TAG .
          docker push $REGISTRY/courier-worker:$TAG

      - name: Build & Push Frontend
        run: |
          docker build -f infra/docker/Courier.Frontend.Dockerfile \
            --build-arg NEXT_PUBLIC_API_BASE_URL=${{ vars.DEV_API_URL }} \
            --build-arg NEXT_PUBLIC_ENTRA_CLIENT_ID=${{ vars.DEV_ENTRA_CLIENT_ID }} \
            --build-arg NEXT_PUBLIC_ENTRA_TENANT_ID=${{ vars.ENTRA_TENANT_ID }} \
            --build-arg NEXT_PUBLIC_REDIRECT_URI=${{ vars.DEV_REDIRECT_URI }} \
            -t $REGISTRY/courier-frontend:$TAG-dev .
          docker push $REGISTRY/courier-frontend:$TAG-dev

  deploy-dev:
    needs: build-images
    runs-on: ubuntu-latest
    environment: dev
    steps:
      - uses: azure/login@v2
        with:
          client-id: ${{ secrets.AZURE_CLIENT_ID }}
          tenant-id: ${{ secrets.AZURE_TENANT_ID }}
          subscription-id: ${{ secrets.AZURE_SUBSCRIPTION_ID }}

      - name: Deploy to Dev
        uses: azure/container-apps-deploy-action@v2
        with:
          containerAppName: courier-api
          resourceGroup: courier-dev-rg
          imageToDeploy: ${{ env.REGISTRY }}/courier-api:${{ env.TAG }}

      - name: Deploy Worker to Dev
        uses: azure/container-apps-deploy-action@v2
        with:
          containerAppName: courier-worker
          resourceGroup: courier-dev-rg
          imageToDeploy: ${{ env.REGISTRY }}/courier-worker:${{ env.TAG }}

      - name: Deploy Frontend to Dev
        uses: azure/container-apps-deploy-action@v2
        with:
          containerAppName: courier-frontend
          resourceGroup: courier-dev-rg
          imageToDeploy: ${{ env.REGISTRY }}/courier-frontend:${{ env.TAG }}-dev

  deploy-staging:
    needs: deploy-dev
    runs-on: ubuntu-latest
    environment: staging       # Requires manual approval in GitHub
    steps:
      # Rebuild frontend with staging env vars, deploy all three apps
      # Same pattern as deploy-dev with staging resource group and vars
      - run: echo "Deploy to staging — same pattern with staging config"

  deploy-production:
    needs: deploy-staging
    runs-on: ubuntu-latest
    environment: production    # Requires manual approval in GitHub
    steps:
      # Rebuild frontend with production env vars, deploy all three apps
      - run: echo "Deploy to production — same pattern with production config"
```

**Frontend rebuilds per environment**: Because `NEXT_PUBLIC_*` vars are baked in at build time, the frontend image is rebuilt for each environment with the correct API URL and Entra ID config. API and Worker images are identical across environments — only runtime env vars differ.

### 14.6 Database Migrations in CI/CD

DbUp migrations run automatically on **API host startup only**. The Worker does not run migrations — it validates the schema version on startup and refuses to start if the database is behind its expected version (see Section 13.1.1 for the full safety model).

The first API container to start acquires a PostgreSQL advisory lock, executes pending migrations, then releases the lock. If multiple API replicas start simultaneously (rolling deployment), the second replica blocks on the advisory lock until the first completes, then discovers all scripts are already applied and starts normally.

```csharp
// Courier.Infrastructure/Migrations/MigrationRunner.cs
public class MigrationRunner : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        await using var conn = new NpgsqlConnection(_connectionString);
        await conn.OpenAsync(cancellationToken);

        // Advisory lock prevents concurrent migration runs across replicas
        await using var cmd = new NpgsqlCommand(
            "SELECT pg_advisory_lock(12345)", conn);
        await cmd.ExecuteNonQueryAsync(cancellationToken);

        try
        {
            var upgrader = DeployChanges.To
                .PostgresqlDatabase(_connectionString)
                .WithScriptsEmbeddedInAssembly(typeof(MigrationRunner).Assembly)
                .WithTransactionPerScript()
                .LogToConsole()
                .Build();

            var result = upgrader.PerformUpgrade();
            if (!result.Successful)
                throw new Exception($"Migration failed: {result.Error}");
        }
        finally
        {
            await using var unlock = new NpgsqlCommand(
                "SELECT pg_advisory_unlock(12345)", conn);
            await unlock.ExecuteNonQueryAsync(cancellationToken);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
```

**Deployment order**: The CI/CD pipeline deploys API hosts first, then Worker hosts. This ensures the schema is migrated before the Worker validates it. If the order is reversed (Worker deployed before API), the Worker's `SchemaVersionValidator` detects the schema mismatch and enters a retry loop until the API migrates the database.

```yaml
# In the GitHub Actions deploy job:
steps:
  - name: Deploy API (runs migrations on startup)
    uses: azure/container-apps-deploy-action@v2
    with:
      containerAppName: courier-api
      # API starts → acquires advisory lock → runs migrations → releases lock

  - name: Wait for API health check
    run: |
      for i in {1..30}; do
        if curl -sf https://courier-api.dev/health; then exit 0; fi
        sleep 5
      done
      exit 1

  - name: Deploy Worker (validates schema version on startup)
    uses: azure/container-apps-deploy-action@v2
    with:
      containerAppName: courier-worker
      # Worker starts → checks schema_versions → starts if compatible
```

**Failure behavior**: If a migration script fails, `WithTransactionPerScript()` rolls back that individual script. The API host crashes (refuses to start), the health check fails, and the deployment is halted before the Worker is deployed. The advisory lock is released via `finally` block (and PostgreSQL auto-releases session locks on disconnect). See Section 13.1.1 for the full failure recovery procedure.

**Destructive migration safety**: Migrations that drop columns or tables follow a two-release deprecation cycle. Release N marks the column as unused (application code stops reading/writing). Release N+1 drops the column. This is enforced by code review — DbUp does not have a built-in guard.

### 14.7 Local Development (.NET Aspire)

Local development uses .NET Aspire to orchestrate all services with a single command:

```csharp
// Courier.AppHost/Program.cs (Aspire orchestrator)
var builder = DistributedApplication.CreateBuilder(args);

// PostgreSQL with persistent volume
var postgres = builder.AddPostgres("courier-db")
    .WithDataVolume("courier-pgdata")
    .AddDatabase("CourierDb");

// Seq for local structured logging
var seq = builder.AddContainer("seq", "datalust/seq")
    .WithEndpoint(port: 5341, targetPort: 80, name: "seq-ui")
    .WithEnvironment("ACCEPT_EULA", "Y");

// API Host
var api = builder.AddProject<Projects.Courier_Api>("courier-api")
    .WithReference(postgres)
    .WithEnvironment("KeyVault__Uri", "https://courier-dev.vault.azure.net")
    .WithEnvironment("Serilog__WriteTo__0__Args__serverUrl", "http://localhost:5341");

// Worker Host
var worker = builder.AddProject<Projects.Courier_Worker>("courier-worker")
    .WithReference(postgres)
    .WithEnvironment("KeyVault__Uri", "https://courier-dev.vault.azure.net")
    .WithEnvironment("Serilog__WriteTo__0__Args__serverUrl", "http://localhost:5341");

// Frontend (npm dev server)
builder.AddNpmApp("courier-frontend", "../Courier.Frontend", "dev")
    .WithReference(api)
    .WithEndpoint(port: 3000, scheme: "http");

builder.Build().Run();
```

**Local dev flow**:

```bash
# Start everything
cd src/Courier.AppHost
dotnet run

# Aspire dashboard at https://localhost:15888
# API at http://localhost:5000
# Frontend at http://localhost:3000
# Seq at http://localhost:5341
# PostgreSQL at localhost:5432
```

### 14.8 Health Checks

Both API and Worker hosts expose health check endpoints used by Container Apps liveness and readiness probes.

**API Host** (`/health` and `/health/ready`):

```csharp
builder.Services.AddHealthChecks()
    .AddNpgSql(connectionString, name: "postgresql",
        failureStatus: HealthStatus.Unhealthy)
    .AddAzureKeyVault(new Uri(builder.Configuration["KeyVault:Uri"]!),
        new DefaultAzureCredential(),
        options => { options.AddSecret("db-connection-string"); },
        name: "keyvault")
    .AddCheck("self", () => HealthCheckResult.Healthy());

app.MapHealthChecks("/health", new HealthCheckOptions
{
    Predicate = check => check.Name == "self"    // Liveness — am I running?
});
app.MapHealthChecks("/health/ready", new HealthCheckOptions
{
    Predicate = _ => true    // Readiness — can I serve requests?
});
```

**Worker Host** (`/health`):

Checks PostgreSQL, Key Vault, Quartz scheduler status, and disk space on the temp volume:

```csharp
builder.Services.AddHealthChecks()
    .AddNpgSql(connectionString, name: "postgresql")
    .AddAzureKeyVault(vaultUri, credential, options => { }, name: "keyvault")
    .AddCheck<QuartzHealthCheck>("quartz")
    .AddDiskStorageHealthCheck(options =>
        options.AddDrive("/data/courier/temp", 1024));  // Fail if < 1 GB free
```

### 14.9 Observability

| Signal | Tool | Coverage |
|--------|------|----------|
| **Structured logs** | Serilog → Seq (local) / Application Insights (deployed) | All services |
| **Distributed traces** | OpenTelemetry → Application Insights | API requests, DB queries, Key Vault calls, HTTP outbound |
| **Metrics** | Application Insights + Container Apps built-in metrics | CPU, memory, request rate, response time, error rate |
| **Dashboards** | Azure Portal + Application Insights workbooks | Execution success rate, latency percentiles, active monitors, key expiry |
| **Alerts** | Application Insights alert rules | Job failure rate > threshold, Worker unhealthy, database connection failures, key expiry within 30 days |

**OpenTelemetry configuration**:

```csharp
builder.Services.AddOpenTelemetry()
    .WithTracing(tracing =>
    {
        tracing.AddAspNetCoreInstrumentation()
               .AddHttpClientInstrumentation()
               .AddNpgsql()
               .AddSource("Courier.JobEngine")
               .AddSource("Courier.FileMonitor")
               .AddAzureMonitorTraceExporter(options =>
                   options.ConnectionString = appInsightsConnectionString);
    });
```

### 14.10 Backup & Disaster Recovery

| Component | Backup Strategy | RPO | RTO |
|-----------|----------------|-----|-----|
| **PostgreSQL** | Azure PG Flex automated backups (daily full + continuous WAL) | < 5 minutes (point-in-time restore) | < 1 hour |
| **Key Vault** | Azure-managed soft delete (90-day retention) + purge protection | 0 (Azure-managed replication) | < 15 minutes |
| **Container images** | Azure Container Registry with geo-replication | 0 (immutable tags) | < 5 minutes (redeploy) |
| **Archived partitions** | Azure Blob Storage with LRS (locally redundant) | 0 (written once, never modified) | N/A (cold storage) |
| **Application code** | GitHub repository | 0 (Git history) | < 30 minutes (rebuild + deploy) |

**Database disaster recovery**: Azure PG Flex supports point-in-time restore to any second within the backup retention window (default: 7 days, configurable to 35). For cross-region DR, a read replica in a secondary region can be promoted.

### 14.11 Infrastructure Summary

```
┌──────────────────────────────────────────────────────────────┐
│                    AZURE RESOURCE GROUP                       │
│                    (per environment)                          │
│                                                              │
│  Container Apps Environment (VNet)                           │
│  ├── courier-api         (2–6 replicas, internal ingress)    │
│  ├── courier-worker      (1 replica, no ingress)             │
│  └── courier-frontend    (2–4 replicas, external ingress)    │
│                                                              │
│  Azure Database for PostgreSQL Flexible Server               │
│  ├── courier database                                        │
│  ├── Private endpoint in VNet                                │
│  └── Automated backups (7-day retention)                     │
│                                                              │
│  Azure Key Vault                                             │
│  ├── Master encryption key (KEK)                             │
│  ├── Application secrets                                     │
│  └── Private endpoint in VNet                                │
│                                                              │
│  Azure Container Registry (shared across environments)       │
│  ├── courier-api:{sha}                                       │
│  ├── courier-worker:{sha}                                    │
│  └── courier-frontend:{sha}-{env}                            │
│                                                              │
│  Azure Blob Storage (archive)                                │
│  └── courier-archives container                              │
│                                                              │
│  Azure Front Door (production only)                          │
│  ├── TLS termination                                         │
│  ├── WAF rules                                               │
│  └── Routes to courier-frontend                              │
│                                                              │
│  Application Insights                                        │
│  └── Logs, traces, metrics, alerts                           │
│                                                              │
└──────────────────────────────────────────────────────────────┘
```

---

## 15. V2 Roadmap

This section catalogs every feature, enhancement, and architectural change deferred to V2, organized into phases with dependency chains. Each item references the V1 section where it was promised, the V1 infrastructure it builds on, and the specific changes required.

**Phasing principle**: Phase 1 (event-driven architecture) is the foundation that unlocks Phases 2–4. It should be delivered first. Phases 2–5 can be delivered in parallel once Phase 1 is stable. Individual items within a phase are independent unless noted.

### 15.1 Phase 1 — Event-Driven Architecture

This is the single highest-value change. It replaces database polling with event-driven coordination, removes the throughput ceiling (Section 2.7: ~50–100 jobs/hour), enables horizontal Worker scaling, and provides the foundation for notifications.

#### 15.1.1 Outbox Relay Service

**V1 hook**: The `domain_events` table (Section 4.2) is already a transactional outbox — events are written in the same database transaction as the state change. `ProcessedAt` is always null in V1 (no subscribers). The schema, event types, and write path are all production-ready.

**V2 changes**:

1. **New hosted service**: `OutboxRelayService` runs as a background service in the Worker host. It polls `domain_events` for unprocessed events using `FOR UPDATE SKIP LOCKED` (same pattern as job dequeue, Section 5.8), publishes them to the message bus, and marks them processed.
2. **Message bus**: Azure Service Bus (production) or RabbitMQ (development/self-hosted). The relay publishes to topic-based subscriptions so multiple consumers can fan out from a single event.
3. **Delivery guarantees**: At-least-once. The outbox write is transactional (same DB transaction as the business operation). The relay marks events processed only after the bus confirms acceptance. If the relay crashes, unprocessed events are picked up on restart. Consumers must be idempotent.
4. **GIN index activation**: Add the GIN `jsonb_path_ops` index on `domain_events.payload` for relay queries (Section 13.7.1 notes this as "None (V1), GIN (V2)").

**What does NOT change**: The event writing path in the Job Engine, Key Store, and Monitor subsystems. V1 code already writes events — V2 adds consumers.

| Dependency | Status |
|-----------|--------|
| `domain_events` table + JSONB payload | ✅ V1 complete |
| `FOR UPDATE SKIP LOCKED` pattern | ✅ V1 complete |
| Event types emitted by all subsystems | ✅ V1 complete |
| Azure Service Bus / RabbitMQ client library | New dependency |
| `OutboxRelayService` hosted service | New code |

#### 15.1.2 Event-Driven Job Scheduling

**V1 hook**: Quartz.NET polls the database for trigger firing times (Section 5.7). The queue dequeue poll scans `job_executions` for `state = 'queued'` rows (Section 5.8). Both add latency (3–10s p95) and database load.

**V2 changes**:

1. When a job is queued (schedule fires, manual trigger, or monitor event), the state change writes a `JobQueued` domain event to the outbox.
2. The outbox relay publishes `JobQueued` to the message bus.
3. Worker instances subscribe to the `JobQueued` topic and begin execution immediately — no polling delay.
4. Quartz remains for schedule management (cron expressions, next-fire-time calculation) but no longer drives execution directly. It writes `JobQueued` events instead of directly transitioning jobs to `Running`.
5. The database poll loop (`QueueDequeueService`) becomes a fallback safety net — runs every 60 seconds instead of every 5 seconds, catching any events the bus missed.

**Expected improvement**: Job pickup latency drops from 3–10s to <500ms. Database poll load drops ~90%.

#### 15.1.3 Horizontal Worker Scaling

**V1 hook**: Worker is single-instance (Section 14.4). Quartz uses `AdoJobStore` which supports clustered mode. `FOR UPDATE SKIP LOCKED` prevents duplicate job pickup. The idempotency rules (Section 5.12) ensure retries are safe.

**V2 changes**:

1. **Quartz cluster mode**: Enable `quartz.jobStore.isClustered = true`. Multiple Worker instances share the same trigger table. Quartz's built-in cluster load balancing distributes trigger firings.
2. **Concurrency semaphore**: Replace the in-process `SemaphoreSlim` (Section 5.8) with a database-backed distributed semaphore (row-level advisory locks) so the global concurrency limit is enforced across all Worker instances.
3. **Monitor affinity**: File monitors with local directory watching cannot run on multiple Workers simultaneously (FileSystemWatcher is per-host). Introduce monitor-to-Worker affinity via a claim mechanism — each monitor is claimed by one Worker instance, with heartbeat-based lease expiration for failover.
4. **Partition maintenance**: `PartitionMaintenanceService` must use `FOR UPDATE SKIP LOCKED` to prevent multiple Workers from running the same maintenance task. (V1 relies on single-instance to avoid this.)

**Scaling target**: 3–5 Worker instances, supporting ~200–500 jobs/hour with sub-second pickup latency.

### 15.2 Phase 2 — Notifications & Alerting

**V1 hook**: Domain events are emitted for all significant state changes (Section 5.18). The event types (`JobCompleted`, `JobFailed`, `KeyExpiringSoon`, `MonitorError`, etc.) are defined and written. The subscriber infrastructure is the only missing piece.

#### 15.2.1 Notification Channels

| Channel | Implementation | Configuration |
|---------|---------------|---------------|
| **Email (SMTP)** | SMTP client (MailKit). Templates stored as Razor views or Liquid templates. | Per-organization SMTP settings. Per-job notification rules (on success, on failure, on timeout). |
| **Webhook (REST)** | `HttpClient` POST to user-configured URL. Retry with exponential backoff (3 attempts). Signed with HMAC-SHA256 for verification. | Per-job or per-monitor webhook URL. Secret key for signature verification. |
| **Slack / Microsoft Teams** | Incoming webhook URL (Slack) or Power Automate connector (Teams). Formatted as channel-appropriate card/block layout. | Organization-level channel configuration. |
| **UI toast / notification center** | Server-Sent Events (SSE) from API to frontend. Notification bell with unread count. | Always on. User-level read/dismiss state. |

#### 15.2.2 Notification Rules Engine

Notifications are configured per entity with a rules model:

```
Job "Daily Invoice Upload"
  → On: JobFailed, JobTimedOut
  → Channels: email (ops-team@corp.com), slack (#courier-alerts)
  → Suppress: if same job failed within last 30 minutes (flood control)

Monitor "Partner Inbound"
  → On: MonitorError (3+ consecutive failures)
  → Channels: email (admin@corp.com), webhook (https://pagerduty.com/...)

Key "Partner-PGP-2025"
  → On: KeyExpiringSoon (30 days before expiry)
  → Channels: email (security@corp.com)
  → Repeat: weekly until key is rotated or retired
```

**Flood control**: Notifications for the same entity + event type are suppressed if an identical notification was sent within a configurable window (default: 30 minutes). This prevents alert storms from flapping jobs or monitors.

#### 15.2.3 Alerting for Operational Events

These V1 events currently write only to the audit log. V2 connects them to notification channels:

| Event | V1 Behavior | V2 Addition | Section |
|-------|------------|-------------|---------|
| `KeyExpiringSoon` | Audit log entry + domain event | Email + Slack to configured key contacts | 7.9 |
| `MonitorError` (repeated failures) | State → Error, audit log | PagerDuty/email to ops team | 9.2 |
| `WatcherAutoDisabled` | Audit log entry | Notification to admin — watcher degraded | 9.2 |
| `InsecureHostKeyPolicyUsed` | Audit log entry | Alert to security team (high-sensitivity event) | 6.7 |
| `InsecureTlsPolicyUsed` | Audit log entry | Alert to security team | 6.3.2 |
| `FipsOverrideUsed` | Audit log entry | Alert to compliance team | 12.10.5 |
| `PartitionMaintenanceOverdue` | Health check `Unhealthy` | Alert to DBA/ops team | 13.6.1 |

### 15.3 Phase 3 — API Access & Security Hardening

#### 15.3.1 Machine-to-Machine API Access

**V1 hook**: Section 12.1 notes that all API access requires interactive Entra ID login. No service accounts or API keys exist.

**V2 changes**:

1. **Entra ID client credential grant**: Register Courier as an API with app roles in Entra ID. Service principals authenticate via client credentials (client ID + certificate, not client secret) and receive a token with app-level roles.
2. **API key alternative**: For environments where Entra ID integration is impractical (CI/CD, partner systems), generate API keys tied to a service account entity. Keys are hashed (SHA-256) in the database, never stored in plaintext. Each key has a role, expiration, and audit trail.
3. **Use cases**: CI/CD pipeline triggers job execution (`POST /api/v1/jobs/{id}/trigger`), external monitoring polls health endpoints, partner systems query transfer status.

#### 15.3.2 Security Role Enhancements

**V1 hook**: Section 12.2 defines three roles (Admin, Operator, Viewer). The accepted risks table (Section 12.12.4) notes that Admin has no separation of duties.

**V2 changes**:

| New Role | Purpose | Key Permissions |
|----------|---------|----------------|
| **Security Admin** | Manages security-sensitive configuration. Distinct from system Admin. | Change `host_key_policy`, `tls_cert_policy`, FIPS toggles, key export settings. Cannot create/edit jobs or connections. |
| **Auditor** | Read-only access to audit logs and security reports. Cannot modify any configuration. | View audit log, view connection test results, view key metadata. Cannot view credentials or export keys. |

**Approval workflows**: Security-sensitive actions (private key export, disabling FIPS mode, enabling `AlwaysTrust` in production) require approval from a second Security Admin. Implemented as a pending-action queue with email notification to approvers.

#### 15.3.3 External SIEM Integration

**V1 hook**: Audit log is append-only in PostgreSQL (Section 12.7). Accepted risk: DBA-level access could theoretically delete entries (Section 12.12.4).

**V2 changes**: Ship audit log entries to an external SIEM (Splunk, Sentinel, Elastic) via one of:

- **Direct push**: `OutboxRelayService` (Phase 1) publishes audit events to a SIEM-specific topic on the message bus. A SIEM adapter consumes and forwards.
- **Log forwarder**: Serilog sink writes structured audit events to stdout, collected by a container-level log forwarder (Fluent Bit → SIEM).
- **Azure Diagnostic Settings**: If using Azure Monitor, route Application Insights custom events to Log Analytics Workspace or Event Hub for SIEM ingestion.

Once SIEM integration is active, the SIEM becomes the immutable audit record. Database-level tampering is detectable by comparing SIEM entries against the database.

#### 15.3.4 File Content Scanning

**V1 hook**: Accepted risk — files transferred via Courier are opaque blobs with no content inspection (Section 12.12.4).

**V2 changes**: Optional per-job step type `file.scan` that submits files to a scanning engine before proceeding:

- **Azure Defender for Storage**: If files pass through Azure Blob (staging area), Defender scans on upload.
- **ClamAV sidecar**: Container-level ClamAV instance accessible via REST API. Step sends file hash + path, ClamAV scans, step proceeds or fails based on result.
- **DLP scanning**: For regulated environments, integrate with Microsoft Purview or a DLP API to detect sensitive data (PII, PCI) in outbound files.

Scanning is opt-in per job. Default is no scanning (V1 behavior preserved).

### 15.4 Phase 4 — Observability & Metrics

#### 15.4.1 Metrics Dashboard

**V1 hook**: The `job_audit_log` (Section 5.16) records every state transition, duration, and byte count. The data exists — V1 just doesn't aggregate it.

**V2 changes**:

- **Transfer metrics**: Total bytes transferred, average transfer rate, success/failure rate by connection, by job, by time period.
- **SLA monitoring**: Per-job SLA targets (e.g., "Daily Invoice Upload must complete by 06:00 UTC"). SLA breach generates an alert (Phase 2 notifications). Dashboard shows SLA compliance percentage over time.
- **Job health**: Mean execution time, p95 execution time, failure rate trend, retry frequency. Anomaly detection (job taking 3× longer than historical average → warning).
- **Connection health**: Success rate per connection, average connection time, last successful transfer timestamp. Stale connection detection (no successful transfer in N days).
- **Implementation**: Materialized views or periodic aggregation queries that roll up raw audit data into hourly/daily summary tables. Frontend dashboard reads summary tables for fast rendering.

#### 15.4.2 Real-Time Transfer Progress

**V1 hook**: Step implementations already track `TransferProgress` (bytes transferred, total bytes, transfer rate — Section 6.4). The data is emitted but not surfaced to the frontend in real time.

**V2 changes**:

1. Worker publishes progress updates to the message bus (Phase 1) at configurable intervals (default: every 5 seconds or 5% progress, whichever is less frequent).
2. API exposes a Server-Sent Events (SSE) endpoint: `GET /api/v1/executions/{id}/progress`.
3. Frontend subscribes to the SSE endpoint and renders a progress bar with transfer rate, ETA, and bytes transferred/remaining.
4. For long-running jobs (multi-step pipelines), the UI shows per-step progress with an overall pipeline progress bar.

### 15.5 Phase 5 — Platform Expansion

These features extend Courier's capabilities to new use cases. Each is independent and can be delivered based on demand.

#### 15.5.1 Cloud Storage Connectors

**V1 hook**: The `ITransferClient` interface (Section 6.1) abstracts protocol-specific operations behind a common surface. Adding a new protocol means implementing `ITransferClient` and registering it in the step type registry.

| Connector | Library | Use Case |
|-----------|---------|----------|
| **Azure Blob Storage** | `Azure.Storage.Blobs` | Internal staging, archive storage, inter-department transfers |
| **AWS S3** | `AWSSDK.S3` | Partner transfers where partners use S3 |
| **SFTP → S3 bridge** | Combine existing SFTP client + S3 client | Download from partner SFTP, upload to internal S3 |

**Connection entity changes**: New `protocol` enum values (`azure_blob`, `aws_s3`). Protocol-specific configuration (storage account, container, access key or managed identity for Azure; bucket, region, IAM role for AWS) stored in the existing JSONB `configuration` column.

#### 15.5.2 File Content Transformation

**V1 hook**: Step type registry (Section 5.2) is extensible — new step types are registered via DI, no engine changes needed.

| Step Type | Description | Use Case |
|-----------|-------------|----------|
| `transform.csv_to_json` | Parse CSV, output JSON | Data feed normalization |
| `transform.xml_to_csv` | Parse XML (XPath selectors), output CSV | Legacy partner integration |
| `transform.json_map` | Apply JSONPath mapping template | API response restructuring |
| `transform.fixed_width` | Parse/generate fixed-width text files | Mainframe/banking integrations |

Transformation steps read from the `JobContext` (input file path from a previous step) and write to the temp directory (output file path added to context for the next step). Same streaming architecture as compression steps (Section 8.1) — no full-file-in-memory loads.

#### 15.5.3 Multi-Tenancy

**V1 hook**: The `organization_id` column exists on major entities (jobs, connections, keys, monitors) for future multi-tenant isolation, but V1 runs as a single-tenant deployment.

**V2 changes**:

1. **Row-level security**: PostgreSQL RLS policies filter all queries by `organization_id`. The API extracts the tenant ID from the Entra ID token's `tid` (tenant ID) claim or a custom claim.
2. **Connection isolation**: Each organization's connections, keys, and jobs are invisible to other organizations. No cross-tenant API access.
3. **Separate KEKs**: Each organization gets its own KEK in Key Vault (or a separate Key Vault). Envelope encryption is scoped to the organization's KEK.
4. **Shared infrastructure**: Database, API hosts, and Worker instances are shared (cost efficiency). Isolation is logical, not physical. For high-security tenants, dedicated Worker instances can be deployed.

#### 15.5.4 Additional Enhancements

These are smaller features that have been noted throughout the document. Each is independent and low-risk.

| Enhancement | Description | V1 Hook | Section |
|-------------|-------------|---------|---------|
| **Dry run mode** | Execute `ValidateAsync` on each step without performing actual transfers. Tests connections, verifies paths, checks key availability. | `IJobStep.ValidateAsync` already exists in the interface | 5.19 |
| **Per-step failure policies** | Override the job-level failure policy on individual steps (e.g., "retry this upload 5 times but skip this rename on failure"). | Schema supports it; engine reads job-level only. | 5.11 |
| **Per-job max instances** | Prevent overlapping executions of the same job definition (e.g., "only one instance of Daily Invoice Upload at a time"). | Concurrency semaphore exists but is global, not per-job. | 5.8 |
| **KEK re-wrap background task** | Iterate all encrypted entities and re-wrap DEKs with the current KEK version. Key hygiene — allows old KEK versions to be disabled. | Encryption blob stores KEK version. V1 rotation is safe without re-wrap. | 7.3.7 |
| **FIPS-validated PGP** | Migrate from BouncyCastle (standard) to `bcpg-fips-csharp` behind `ICryptoProvider`. Provides module-level FIPS validation for PGP operations. | `ICryptoProvider` abstraction is designed for this swap. | 12.10.2 |
| **ECC key support** | Add Curve25519 and NIST P-256/P-384 key generation for PGP and SSH. | BouncyCastle supports ECC. `algorithm` enum has room for expansion. Schema needs no migration. | 7.10 |
| **Auto key generation on expiry** | Automatically generate a replacement key pair when a key enters `Expiring` status, linked via `successor_key_id`. | Schema supports `successor_key_id` (nullable FK). | 7.9 |
| **Multi-user approval for key export** | Require a second Admin to approve private key export requests. Pending-action queue with expiration. | Export endpoint exists with audit logging. Approval is a new middleware layer. | 7.3.5 |
| **Selective archive extraction** | Extract only entries matching filename patterns (glob or regex) instead of the full archive. | `ArchiveInspection` API returns entry list. Extraction loop can filter. | 8.1.10 |
| **Zero-downtime database migrations** | Use an expand-contract migration pattern where both old and new code can run against the schema simultaneously during rollout. | Two-release deprecation cycle is already policy. Needs tooling enforcement. | 13.1.1 |

### 15.6 V1 → V2 Migration Path

V2 features are additive — none require breaking changes to V1 data or APIs.

**Database**: All V2 schema changes are additive columns, new tables, or new indexes. No existing columns are removed or renamed. The two-release deprecation cycle (Section 13.1) applies to any V2 changes that eventually deprecate V1 patterns.

**API**: V2 endpoints are added alongside V1 endpoints. No existing V1 endpoints are removed. If a V2 feature changes the behavior of an existing endpoint (e.g., adding notification rules to job creation), the change is backward-compatible (new optional fields with defaults that preserve V1 behavior).

**Configuration**: New system settings for V2 features use the existing `system_settings` table (Section 4.2). Defaults are chosen so that a V2 deployment with no configuration changes behaves identically to V1.

**Event bus**: The outbox relay (Phase 1) is the only infrastructure addition that requires a new external dependency (Azure Service Bus or RabbitMQ). All other V2 features build on existing infrastructure. The relay can be deployed alongside V1 code — it processes events that V1 already writes, and V1 continues to function if the relay is not yet deployed.

### 15.7 Dependency Graph

```
Phase 1: Event-Driven Architecture
  │
  ├── 15.1.1 Outbox Relay ◄── V1 domain_events table
  │     │
  │     ├── 15.1.2 Event-Driven Scheduling ◄── V1 Quartz triggers
  │     │     │
  │     │     └── 15.1.3 Horizontal Worker Scaling ◄── V1 FOR UPDATE SKIP LOCKED
  │     │
  │     ├── 15.2 Notifications ◄── V1 domain events (all types)
  │     │     │
  │     │     └── 15.2.3 Operational Alerting
  │     │
  │     ├── 15.3.3 SIEM Integration
  │     │
  │     └── 15.4.2 Real-Time Progress
  │
  ├── (Independent of Phase 1)
  │
  ├── 15.3.1 Machine-to-Machine Access ◄── V1 Entra ID + RBAC
  │
  ├── 15.3.2 Security Roles ◄── V1 3-role RBAC
  │
  ├── 15.3.4 File Content Scanning ◄── V1 step type registry
  │
  ├── 15.4.1 Metrics Dashboard ◄── V1 job_audit_log data
  │
  ├── 15.5.1 Cloud Connectors ◄── V1 ITransferClient interface
  │
  ├── 15.5.2 Transformations ◄── V1 step type registry
  │
  ├── 15.5.3 Multi-Tenancy ◄── V1 organization_id columns
  │
  └── 15.5.4 Enhancements ◄── Various V1 hooks (see table)
```

### 15.8 What V1 Built Specifically for V2

These V1 design decisions exist solely to make V2 possible without rearchitecting:

| V1 Decision | V2 Payoff | Section |
|-------------|-----------|---------|
| `domain_events` table with transactional outbox pattern | Outbox relay, notifications, event-driven scheduling | 4.2 |
| `ProcessedAt` / `ProcessedBy` columns on domain events | Relay tracks consumption state without schema changes | 4.2 |
| `ITransferClient` protocol abstraction | Cloud storage connectors plug in without engine changes | 6.1 |
| `ICryptoProvider` abstraction for PGP operations | FIPS-validated BouncyCastle swap without consumer changes | 7.5 |
| `IJobStep.ValidateAsync` method on all step implementations | Dry run mode without modifying the step interface | 5.19 |
| `organization_id` on major entities | Multi-tenancy via RLS without schema migration | 4.1 |
| `successor_key_id` nullable FK on key entities | Auto key rotation linking without schema migration | 7.3.1 |
| `ssh_algorithms` JSONB (flexible schema) | Algorithm expansion without column migrations | 6.5 |
| Step type registry via DI | New step types (cloud, transform, scan) are registered — no engine changes | 5.2 |
| `FOR UPDATE SKIP LOCKED` everywhere | Horizontal scaling works without changing work-claiming logic | 5.8 |
| Quartz `AdoJobStore` (not `RAMJobStore`) | Cluster mode is a configuration change, not a code change | 5.7 |
| KEK version stored in every encrypted blob | KEK rotation works today; re-wrap task is a convenience, not a requirement | 7.3.6 |
