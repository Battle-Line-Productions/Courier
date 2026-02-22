# Frontend Design: Job Management UI

**Date**: 2026-02-22
**Status**: Approved

---

## Overview

Build the first functional frontend for Courier — a job management UI that allows users to create, view, edit, and run jobs with file.copy and file.move steps, and watch execution progress in real time.

## Scope

**In scope:**
- Hybrid shell layout (sidebar + topbar + breadcrumbs)
- Jobs list page with data table, search, pagination, row actions
- Job create/edit page with inline step builder
- Job detail page with overview, steps, execution timeline (auto-refresh)
- 3 new backend endpoints (update job, delete job, replace steps)

**Out of scope:**
- Authentication (MSAL)
- Dark mode
- Connections, Keys, Monitors, Audit, Settings pages
- Drag-and-drop step reorder

---

## Tech Stack

| Library | Purpose |
|---------|---------|
| Tailwind CSS | Utility-first styling |
| shadcn/ui | Component primitives (Button, Input, Table, Dialog, Badge, DropdownMenu, Accordion, Skeleton, Toast) |
| TanStack Query | Server state management, cache invalidation |
| React Hook Form | Form state management |
| Zod | Schema validation |
| Lucide React | Icons |

---

## Project Structure

```
src/Courier.Frontend/src/
├── app/
│   ├── layout.tsx              ← Root layout (sidebar + topbar + providers)
│   ├── page.tsx                ← Redirect to /jobs
│   ├── jobs/
│   │   ├── page.tsx            ← Jobs list (data table)
│   │   ├── new/page.tsx        ← Create job form
│   │   └── [id]/
│   │       ├── page.tsx        ← Job detail (overview + executions)
│   │       └── edit/page.tsx   ← Edit job form
│   └── not-found.tsx
├── components/
│   ├── ui/                     ← shadcn/ui primitives
│   ├── layout/
│   │   ├── sidebar.tsx         ← Left nav
│   │   ├── topbar.tsx          ← App branding + breadcrumbs
│   │   └── shell.tsx           ← Combines sidebar + topbar + content
│   ├── jobs/
│   │   ├── job-table.tsx       ← Data table for jobs list
│   │   ├── job-form.tsx        ← Create/edit form with step builder
│   │   ├── step-builder.tsx    ← Add/remove/reorder steps inline
│   │   ├── step-config-form.tsx ← Config fields per step type
│   │   ├── execution-timeline.tsx ← Step-by-step progress view
│   │   └── run-button.tsx      ← Trigger job with confirmation
│   └── shared/
│       ├── status-badge.tsx    ← Colored badge for job/step states
│       ├── confirm-dialog.tsx  ← Reusable confirmation modal
│       └── empty-state.tsx     ← "No jobs yet" placeholder
├── lib/
│   ├── api.ts                  ← Typed fetch wrapper, envelope unwrapping
│   ├── types.ts                ← API response types
│   ├── utils.ts                ← Date formatting helpers
│   ├── query-client.ts         ← QueryClient configuration
│   └── hooks/
│       ├── use-jobs.ts
│       ├── use-job-steps.ts
│       ├── use-job-executions.ts
│       └── use-job-mutations.ts
└── styles/
    └── globals.css             ← Tailwind base + shadcn theme tokens
```

---

## Shell Layout

Hybrid sidebar + topbar:

```
┌──────────────────────────────────────┐
│  Courier    [Search]  [User] [?]     │
├────────┬─────────────────────────────┤
│        │  Breadcrumbs                │
│  Jobs  ├─────────────────────────────┤
│  Conns │                             │
│  Keys  │   Main Content Area         │
│  Mon.  │                             │
│  Audit │                             │
│        │                             │
│ ────── │                             │
│  Sett. │                             │
└────────┴─────────────────────────────┘
```

- Sidebar: collapsible to icon-only. Jobs active, all others show "Coming Soon" tooltip.
- Topbar: "Courier" branding, breadcrumbs (e.g., Jobs / Invoice Processor / Edit)
- Content area: scrollable, max-width constrained

---

## Page 1: Jobs List (`/jobs`)

- Header: "Jobs" title + `[+ Create Job]` button
- Search input: client-side name filtering
- Data table columns: Name (link), Description (truncated), Version (badge), Enabled (badge), Created (relative time), Actions (⋮ dropdown)
- Row actions: Edit → `/jobs/{id}/edit`, Run → confirm dialog → trigger, Delete → confirm dialog → soft delete
- Pagination: server-side via `page` + `pageSize`
- Empty state: "No jobs yet. Create your first job to get started." with CTA button
- Data: `useJobs(page, pageSize)` hook

---

## Page 2: Job Create/Edit (`/jobs/new`, `/jobs/[id]/edit`)

Same `job-form.tsx` component. Edit mode pre-populates from `useJob(id)` + `useJobSteps(id)`.

**Job details:**
- Name: required, 1-100 chars (Zod validated)
- Description: optional, max 500 chars

**Step builder:**
- `[+ Add Step]` adds inline card at bottom
- Step type dropdown: `file.copy`, `file.move`
- Config fields per type: Source Path, Destination Path, Overwrite (checkbox)
- Each step: summary view by default, `[✎]` to expand, `[✕]` to remove
- Step order: auto-renumbered on add/remove

**Save:**
- Create: `POST /jobs` then `POST /steps` for each step
- Edit: `PUT /jobs/{id}` then `PUT /jobs/{id}/steps` (atomic replace)
- On success: navigate to `/jobs/{id}`

---

## Page 3: Job Detail (`/jobs/[id]`)

**Header:** Job name, description, metadata (version, enabled, created), `[Edit]` + `[▶ Run]` buttons

**Steps section:** Read-only list with order, name, type badge, source→destination summary

**Executions section:**
- Accordion list from `useJobExecutions(jobId)`
- Each row: status icon, execution number, state badge, relative time
- Expanded: step timeline with status, duration, error message
- Latest running execution: expanded by default, auto-refresh via `refetchInterval: 2000` while Running/Queued
- Paginated

**Status indicators:**

| State | Icon | Color |
|-------|------|-------|
| Queued | ○ | grey |
| Running | ● | blue (pulsing) |
| Completed | ✓ | green |
| Failed | ✗ | red |
| Cancelled | ⊘ | grey |
| Pending (step) | ○ | grey |
| Skipped (step) | — | grey |

---

## Backend Additions

3 new endpoints in existing `JobsController`:

**1. `PUT /api/v1/jobs/{id}`**
- Request: `UpdateJobRequest { Name, Description }`
- Increments `currentVersion`, updates `updatedAt`
- Returns `ApiResponse<JobDto>`

**2. `DELETE /api/v1/jobs/{id}`**
- Soft delete: `isDeleted = true`, `deletedAt = now`
- Returns `ApiResponse<object>`

**3. `PUT /api/v1/jobs/{id}/steps`**
- Request: `ReplaceJobStepsRequest { Steps: List<StepInput> }`
- Atomic: delete all existing, insert new, single transaction
- Returns `ApiResponse<List<JobStepDto>>`

---

## Data Flow

```
User Action → React Hook Form → Mutation Hook → API Client → Backend
                                      ↓
                              Cache Invalidation
                                      ↓
                              Query Hook Re-fetches
                                      ↓
                              Component Re-renders
```

For execution polling:
```
Trigger Job → Navigate to Detail → useExecution(id, { refetchInterval: 2000 })
                                        ↓
                                  Auto-refresh while Running
                                        ↓
                                  Stop polling on Complete/Failed
                                        ↓
                                  Invalidate executions list
```
