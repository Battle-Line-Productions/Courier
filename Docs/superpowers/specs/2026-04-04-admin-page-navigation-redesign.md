# Admin Page & Navigation Redesign

**Date:** 2026-04-04
**Status:** Approved
**Scope:** Frontend-only — reorganize navigation, create Admin page with tabbed layout, create Account page

---

## Problem

- User management pages exist at `/settings/users` but no sidebar link exposes them — they're unreachable without typing the URL
- Auth Provider management is a top-level sidebar item instead of being grouped with other admin functions
- The Settings page mixes admin config (session timeouts) with personal actions (change password)
- The topbar "Profile" link incorrectly navigates to the user list page
- No SSO-aware password change detection

## Design Decisions

1. **Single Admin page with tabbed layout** — consolidates all admin functions under one route
2. **Permission-per-tab** — the Admin sidebar link shows if you have access to any tab; individual tabs are gated by their own permissions
3. **Personal account page** — replaces Settings for non-admin personal actions
4. **Admin-managed user profiles** — users cannot edit their own display name or email
5. **SSO password detection** — change password hidden for SSO-only users, shown when local password is allowed

---

## 1. Navigation Changes

### Sidebar

**Current bottom section:**
```
Settings (/settings)
Auth Providers (/settings/auth-providers)  ← admin/operator only
```

**New bottom section:**
```
Admin (/admin)  ← visible if user has ANY admin-tab permission
```

One item replaces two. Settings is removed from the sidebar.

### Topbar Avatar Menu

**Current:**
```
Profile → /settings/users (broken — goes to user list)
Change Password → /settings
Sign Out
```

**New:**
```
My Account → /account
Sign Out
```

### Route Map

| Old Route | New Route | Notes |
|---|---|---|
| `/settings` | Removed | Auth config → Admin > Security |
| `/settings/users` | `/admin/users` | Tab content within Admin page |
| `/settings/users/new` | `/admin/users/new` | Standalone create page |
| `/settings/users/[id]` | `/admin/users/[id]` | Standalone edit page |
| `/settings/auth-providers` | `/admin/auth-providers` | Tab content within Admin page |
| `/settings/auth-providers/new` | `/admin/auth-providers/new` | Standalone create page |
| `/settings/auth-providers/[id]` | `/admin/auth-providers/[id]` | Standalone edit page |
| — | `/admin/security` | New (relocated auth settings) |
| — | `/account` | New personal profile page |

---

## 2. Admin Page (`/admin`)

### Layout

Single page with a horizontal tab bar. Tab content renders inline. CRUD sub-pages (`/admin/users/new`, `/admin/users/[id]`, `/admin/auth-providers/new`, `/admin/auth-providers/[id]`) remain as standalone routed pages.

### Tab Visibility

| Tab | Required Permission | Visible To |
|---|---|---|
| Users | `UsersView` | Admin |
| Auth Providers | `AuthProvidersView` | Admin, Operator |
| Security | `SettingsView` | Admin |

Default selected tab: first tab the user has permission to see. Admin lands on Users. Operator lands on Auth Providers.

### Access Control

The `/admin` route is accessible if the user has permission to see **at least one** tab. If a user navigates to `/admin` with no tab permissions, show "You do not have permission to view this page."

### Extensibility

Tabs are defined in a configuration array:

```typescript
const adminTabs = [
  { key: "users", label: "Users", permission: "UsersView", component: UsersTab },
  { key: "auth-providers", label: "Auth Providers", permission: "AuthProvidersView", component: AuthProvidersTab },
  { key: "security", label: "Security", permission: "SettingsView", component: SecurityTab },
];
```

Adding a new tab requires adding one entry to this array.

### Users Tab

Relocated from `/settings/users`. No functional changes.

- Searchable, paginated user list
- Columns: display name, email, role, active status, SSO badge, last login
- "Add User" button (requires `UsersManage`)
- Row actions: edit, reset password, delete (requires `UsersManage`)
- Edit/create navigate to `/admin/users/[id]` and `/admin/users/new`

### Auth Providers Tab

Relocated from `/settings/auth-providers`. No functional changes.

- Provider list with name, type (OIDC/SAML), enabled status, linked user count
- "Add Provider" button (requires `AuthProvidersCreate`)
- Row actions: edit, test connection, delete (requires respective permissions)
- Edit/create navigate to `/admin/auth-providers/[id]` and `/admin/auth-providers/new`

### Security Tab

Relocated from `/settings` page "Authentication" tab. No functional changes.

- Session timeout (minutes)
- Password minimum length
- Max login attempts before lockout
- Lockout duration (minutes)
- Save button (requires `SettingsManage`)

---

## 3. Account Page (`/account`)

### Access

All authenticated users. No special permissions required.

### Layout

Simple card-based page, no tabs.

### Profile Section (read-only)

| Field | Source |
|---|---|
| Display name | `user.displayName` |
| Username | `user.username` |
| Email | `user.email` |
| Role | `user.role` — rendered as badge |
| SSO Provider | `user.ssoProvider.name` + type badge (if `isSsoUser`) |
| Last login | `user.lastLoginAt` — formatted timestamp |

No edit buttons. Admin manages user details from Admin > Users.

### Change Password Section (conditional)

| Condition | Behavior |
|---|---|
| `isSsoUser = false` | Show change password form |
| `isSsoUser = true` AND provider `allowLocalPassword = true` | Show change password form |
| `isSsoUser = true` AND provider `allowLocalPassword = false` | Show info: "Your password is managed by {providerName}" |

Change password form fields:
- Current password
- New password
- Confirm new password

Calls the user's own password change endpoint (not the admin reset-password endpoint).

---

## 4. What Changes

### Moved (existing code, new location)
- Users list/CRUD pages: `/settings/users/*` → `/admin/users/*`
- Auth Providers list/CRUD pages: `/settings/auth-providers/*` → `/admin/auth-providers/*`
- Auth settings form: `/settings` Authentication tab → `/admin` Security tab

### New
- `/admin` page shell with permission-gated tab bar
- `/account` page with read-only profile + conditional change password
- Sidebar "Admin" link with permission check
- Avatar menu "My Account" link

### Deleted
- `/settings` page and all sub-routes
- "Auth Providers" sidebar link
- "Profile" and "Change Password" avatar menu links

### Backend
No backend changes. All existing API endpoints remain the same.

---

## 5. API Endpoints Used

All existing — no new endpoints needed.

| Endpoint | Used By |
|---|---|
| `GET /api/v1/users` | Admin > Users tab |
| `GET /api/v1/users/{id}` | Admin > Users edit page |
| `POST /api/v1/users` | Admin > Users create page |
| `PUT /api/v1/users/{id}` | Admin > Users edit page |
| `DELETE /api/v1/users/{id}` | Admin > Users tab (delete action) |
| `POST /api/v1/users/{id}/reset-password` | Admin > Users edit page |
| `GET /api/v1/auth-providers` | Admin > Auth Providers tab |
| `GET /api/v1/auth-providers/{id}` | Admin > Auth Providers edit page |
| `POST /api/v1/auth-providers` | Admin > Auth Providers create page |
| `PUT /api/v1/auth-providers/{id}` | Admin > Auth Providers edit page |
| `DELETE /api/v1/auth-providers/{id}` | Admin > Auth Providers tab |
| `POST /api/v1/auth-providers/{id}/test` | Admin > Auth Providers edit page |
| `GET /api/v1/settings` | Admin > Security tab, Account page |
| `PUT /api/v1/settings` | Admin > Security tab |
| `POST /api/v1/auth/change-password` | Account page |
| `GET /api/v1/auth/me` | Account page (current user info) |
