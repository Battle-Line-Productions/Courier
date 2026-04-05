# Admin Page & Navigation Redesign Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Reorganize the frontend navigation by consolidating user management, auth providers, and security settings under a tabbed Admin page, creating a personal Account page, and cleaning up dead routes.

**Architecture:** Move existing page content into tab components within a new `/admin` route. Create a read-only `/account` page with SSO-aware change password. Extend the backend `UserProfileDto` with SSO fields so the Account page can detect whether to show the password form. Update sidebar and topbar navigation, then remove old `/settings` routes.

**Tech Stack:** Next.js 15 (App Router), React 19, TypeScript, Tailwind, shadcn/ui Tabs, TanStack Query, ASP.NET Core (backend DTO change), EF Core

**Spec:** `docs/superpowers/specs/2026-04-04-admin-page-navigation-redesign.md`

---

## File Map

### New Files
| File | Purpose |
|---|---|
| `src/Courier.Frontend/src/app/(app)/admin/page.tsx` | Admin page shell with permission-gated tab bar |
| `src/Courier.Frontend/src/app/(app)/admin/users/new/page.tsx` | Create user page (moved from settings) |
| `src/Courier.Frontend/src/app/(app)/admin/users/[id]/page.tsx` | Edit user page (moved from settings) |
| `src/Courier.Frontend/src/app/(app)/admin/auth-providers/new/page.tsx` | Create auth provider page (moved from settings) |
| `src/Courier.Frontend/src/app/(app)/admin/auth-providers/[id]/page.tsx` | Edit auth provider page (moved from settings) |
| `src/Courier.Frontend/src/app/(app)/account/page.tsx` | Personal account page with profile + change password |

### Modified Files
| File | Change |
|---|---|
| `src/Courier.Features/Auth/AuthDtos.cs` | Add SSO fields to `UserProfileDto` |
| `src/Courier.Features/Auth/AuthService.cs` | Include SsoProvider in query, update `MapToProfile` |
| `src/Courier.Frontend/src/lib/types.ts` | Add SSO fields to frontend `UserProfileDto` |
| `src/Courier.Frontend/src/components/layout/sidebar.tsx` | Replace Settings + Auth Providers links with Admin |
| `src/Courier.Frontend/src/components/layout/topbar.tsx` | Update avatar menu + breadcrumb mappings |
| `src/Courier.Frontend/e2e/users.spec.ts` | Update `/settings/users` → `/admin/users` routes |
| `src/Courier.Frontend/e2e/settings.spec.ts` | Update `/settings` → `/admin` routes, restructure for tabs |
| `src/Courier.Frontend/e2e/navigation.spec.ts` | Update navigation test assertions |

### Deleted Files
| File | Reason |
|---|---|
| `src/Courier.Frontend/src/app/(app)/settings/page.tsx` | Content moved to Admin Security tab + Account page |
| `src/Courier.Frontend/src/app/(app)/settings/users/page.tsx` | Content moved to Admin Users tab |
| `src/Courier.Frontend/src/app/(app)/settings/users/new/page.tsx` | Moved to `/admin/users/new` |
| `src/Courier.Frontend/src/app/(app)/settings/users/[id]/page.tsx` | Moved to `/admin/users/[id]` |
| `src/Courier.Frontend/src/app/(app)/settings/auth-providers/page.tsx` | Content moved to Admin Auth Providers tab |
| `src/Courier.Frontend/src/app/(app)/settings/auth-providers/new/page.tsx` | Moved to `/admin/auth-providers/new` |
| `src/Courier.Frontend/src/app/(app)/settings/auth-providers/[id]/page.tsx` | Moved to `/admin/auth-providers/[id]` |

---

## Task 1: Backend — Extend UserProfileDto with SSO Fields

The Account page needs to know whether the user is SSO-managed and whether local password change is allowed. The current `UserProfileDto` only has `Id`, `Username`, `Email`, `DisplayName`, `Role`.

**Files:**
- Modify: `src/Courier.Features/Auth/AuthDtos.cs:22-29`
- Modify: `src/Courier.Features/Auth/AuthService.cs:287-300` (GetCurrentUserAsync)
- Modify: `src/Courier.Features/Auth/AuthService.cs:368-375` (MapToProfile)
- Test: `tests/Courier.Tests.Unit/Features/Auth/AuthServiceTests.cs`

- [ ] **Step 1: Add SSO fields to UserProfileDto**

In `src/Courier.Features/Auth/AuthDtos.cs`, add the new fields to the record:

```csharp
public record UserProfileDto
{
    public Guid Id { get; init; }
    public required string Username { get; init; }
    public string? Email { get; init; }
    public required string DisplayName { get; init; }
    public required string Role { get; init; }
    public bool IsSsoUser { get; init; }
    public string? SsoProviderName { get; init; }
    public bool AllowLocalPassword { get; init; } = true;
    public DateTimeOffset? LastLoginAt { get; init; }
}
```

- [ ] **Step 2: Update GetCurrentUserAsync to include SsoProvider**

In `src/Courier.Features/Auth/AuthService.cs`, update the `GetCurrentUserAsync` method to eager-load the SsoProvider:

```csharp
public async Task<ApiResponse<UserProfileDto>> GetCurrentUserAsync(Guid userId, CancellationToken ct = default)
{
    var user = await _db.Users
        .Include(u => u.SsoProvider)
        .FirstOrDefaultAsync(u => u.Id == userId, ct);

    if (user is null)
    {
        return new ApiResponse<UserProfileDto>
        {
            Error = ErrorMessages.Create(ErrorCodes.UserNotFound, "User not found.")
        };
    }

    return new ApiResponse<UserProfileDto> { Data = MapToProfile(user) };
}
```

- [ ] **Step 3: Update MapToProfile to map SSO fields**

In `src/Courier.Features/Auth/AuthService.cs`, update the `MapToProfile` method:

```csharp
private static UserProfileDto MapToProfile(User user) => new()
{
    Id = user.Id,
    Username = user.Username,
    Email = user.Email,
    DisplayName = user.DisplayName,
    Role = user.Role,
    IsSsoUser = user.IsSsoUser,
    SsoProviderName = user.SsoProvider?.Name,
    AllowLocalPassword = !user.IsSsoUser || (user.SsoProvider?.AllowLocalPassword ?? true),
    LastLoginAt = user.LastLoginAt,
};
```

- [ ] **Step 4: Build and verify**

Run: `dotnet build src/Courier.Features/Courier.Features.csproj`
Expected: Build succeeds with no errors.

- [ ] **Step 5: Add unit test for SSO profile fields**

Find the existing `AuthServiceTests.cs` test file. Add a test that verifies SSO fields are mapped correctly when the user is an SSO user. The test should:
- Create a User with `IsSsoUser = true`, `SsoProviderId` set, and a related `AuthProvider` entity with `Name` and `AllowLocalPassword`
- Call `GetCurrentUserAsync`
- Assert `IsSsoUser`, `SsoProviderName`, `AllowLocalPassword`, and `LastLoginAt` are correct

Also add a test for a non-SSO user verifying `IsSsoUser = false`, `SsoProviderName = null`, `AllowLocalPassword = true`.

- [ ] **Step 6: Run unit tests**

Run: `dotnet test tests/Courier.Tests.Unit --filter "FullyQualifiedName~AuthServiceTests"`
Expected: All tests pass.

- [ ] **Step 7: Commit**

```bash
git add src/Courier.Features/Auth/AuthDtos.cs src/Courier.Features/Auth/AuthService.cs tests/Courier.Tests.Unit/Features/Auth/AuthServiceTests.cs
git commit -m "feat: extend UserProfileDto with SSO and last login fields"
```

---

## Task 2: Frontend Types — Add SSO Fields to UserProfileDto

**Files:**
- Modify: `src/Courier.Frontend/src/lib/types.ts`

- [ ] **Step 1: Update the frontend UserProfileDto type**

In `src/Courier.Frontend/src/lib/types.ts`, find the `UserProfileDto` interface (around line 724) and add the new fields:

```typescript
export interface UserProfileDto {
  id: string;
  username: string;
  displayName: string;
  email?: string;
  role: string;
  isSsoUser: boolean;
  ssoProviderName?: string;
  allowLocalPassword: boolean;
  lastLoginAt?: string;
}
```

- [ ] **Step 2: Verify TypeScript compiles**

Run: `cd src/Courier.Frontend && npx tsc --noEmit`
Expected: No type errors (the new fields are optional enough that existing code won't break).

- [ ] **Step 3: Commit**

```bash
git add src/Courier.Frontend/src/lib/types.ts
git commit -m "feat: add SSO fields to frontend UserProfileDto type"
```

---

## Task 3: Create Admin Page Shell with Permission-Gated Tabs

**Files:**
- Create: `src/Courier.Frontend/src/app/(app)/admin/page.tsx`

- [ ] **Step 1: Create the admin directory**

```bash
mkdir -p src/Courier.Frontend/src/app/\(app\)/admin
```

- [ ] **Step 2: Create the Admin page with tab bar**

Create `src/Courier.Frontend/src/app/(app)/admin/page.tsx`:

```tsx
"use client";

import { useState } from "react";
import { usePermissions } from "@/lib/hooks/use-permissions";
import type { Permission } from "@/lib/permissions";
import { Tabs, TabsContent, TabsList, TabsTrigger } from "@/components/ui/tabs";
import { Users, ShieldCheck, Lock } from "lucide-react";
import { UsersTab } from "./users-tab";
import { AuthProvidersTab } from "./auth-providers-tab";
import { SecurityTab } from "./security-tab";

interface AdminTab {
  key: string;
  label: string;
  permission: Permission;
  icon: React.ComponentType<{ className?: string }>;
  component: React.ComponentType;
}

const adminTabs: AdminTab[] = [
  { key: "users", label: "Users", permission: "UsersView", icon: Users, component: UsersTab },
  { key: "auth-providers", label: "Auth Providers", permission: "AuthProvidersView", icon: ShieldCheck, component: AuthProvidersTab },
  { key: "security", label: "Security", permission: "SettingsView", icon: Lock, component: SecurityTab },
];

export default function AdminPage() {
  const { can, canAny } = usePermissions();

  const visibleTabs = adminTabs.filter((tab) => can(tab.permission));

  if (visibleTabs.length === 0) {
    return (
      <div className="text-center text-muted-foreground py-12">
        You do not have permission to view this page.
      </div>
    );
  }

  const defaultTab = visibleTabs[0].key;

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">Administration</h1>
        <p className="text-sm text-muted-foreground">Manage users, authentication, and security settings.</p>
      </div>

      <Tabs defaultValue={defaultTab}>
        <TabsList>
          {visibleTabs.map((tab) => (
            <TabsTrigger key={tab.key} value={tab.key}>
              <tab.icon className="mr-1.5 h-3.5 w-3.5" />
              {tab.label}
            </TabsTrigger>
          ))}
        </TabsList>
        {visibleTabs.map((tab) => (
          <TabsContent key={tab.key} value={tab.key} className="mt-6">
            <tab.component />
          </TabsContent>
        ))}
      </Tabs>
    </div>
  );
}
```

- [ ] **Step 3: Create placeholder tab components**

Create three stub files so the admin page compiles. These will be replaced with real content in subsequent tasks.

Create `src/Courier.Frontend/src/app/(app)/admin/users-tab.tsx`:

```tsx
"use client";

export function UsersTab() {
  return <div className="text-sm text-muted-foreground">Users tab — coming soon.</div>;
}
```

Create `src/Courier.Frontend/src/app/(app)/admin/auth-providers-tab.tsx`:

```tsx
"use client";

export function AuthProvidersTab() {
  return <div className="text-sm text-muted-foreground">Auth Providers tab — coming soon.</div>;
}
```

Create `src/Courier.Frontend/src/app/(app)/admin/security-tab.tsx`:

```tsx
"use client";

export function SecurityTab() {
  return <div className="text-sm text-muted-foreground">Security tab — coming soon.</div>;
}
```

- [ ] **Step 4: Verify the page renders**

Run: `cd src/Courier.Frontend && npx tsc --noEmit`
Expected: No type errors.

- [ ] **Step 5: Commit**

```bash
git add src/Courier.Frontend/src/app/\(app\)/admin/
git commit -m "feat: create admin page shell with permission-gated tab bar"
```

---

## Task 4: Implement Users Tab Component

Extract the users list from `src/app/(app)/settings/users/page.tsx` into the admin Users tab. Remove the page heading (the Admin page provides context). Update internal links from `/settings/users/` to `/admin/users/`.

**Files:**
- Modify: `src/Courier.Frontend/src/app/(app)/admin/users-tab.tsx`
- Reference: `src/Courier.Frontend/src/app/(app)/settings/users/page.tsx` (read-only, will be deleted later)

- [ ] **Step 1: Implement the Users tab**

Replace `src/Courier.Frontend/src/app/(app)/admin/users-tab.tsx` with the content from the existing users page, adapted for tab context:

```tsx
"use client";

import { useState } from "react";
import Link from "next/link";
import { useUsers, useDeleteUser } from "@/lib/hooks/use-users";
import { useAuth } from "@/lib/auth";
import { usePermissions } from "@/lib/hooks/use-permissions";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Search, Trash2, UserPlus } from "lucide-react";
import { toast } from "sonner";
import { ApiClientError } from "@/lib/api";
import { cn } from "@/lib/utils";

const roleBadgeColors: Record<string, string> = {
  admin: "bg-red-100 text-red-800 dark:bg-red-900/30 dark:text-red-400",
  operator: "bg-blue-100 text-blue-800 dark:bg-blue-900/30 dark:text-blue-400",
  viewer: "bg-gray-100 text-gray-800 dark:bg-gray-900/30 dark:text-gray-400",
};

export function UsersTab() {
  const { user: currentUser } = useAuth();
  const { can } = usePermissions();
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState("");
  const { data, isLoading } = useUsers(page, 10, search || undefined);
  const deleteUser = useDeleteUser();

  const users = data?.data ?? [];
  const pagination = data?.pagination;

  async function handleDelete(userId: string, username: string) {
    if (!confirm(`Are you sure you want to delete user "${username}"?`)) return;
    try {
      await deleteUser.mutateAsync(userId);
      toast.success(`User "${username}" deleted.`);
    } catch (err) {
      if (err instanceof ApiClientError) toast.error(err.message);
      else toast.error("Failed to delete user.");
    }
  }

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <div className="relative max-w-sm">
          <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
          <Input
            placeholder="Search users..."
            className="pl-9"
            value={search}
            onChange={(e) => { setSearch(e.target.value); setPage(1); }}
          />
        </div>
        {can("UsersManage") && (
          <Button asChild>
            <Link href="/admin/users/new">
              <UserPlus className="mr-2 h-4 w-4" />
              Add User
            </Link>
          </Button>
        )}
      </div>

      {isLoading ? (
        <div className="text-sm text-muted-foreground">Loading users...</div>
      ) : users.length === 0 ? (
        <div className="text-center text-muted-foreground py-12">No users found.</div>
      ) : (
        <>
          <div className="rounded-md border">
            <table className="w-full text-sm">
              <thead>
                <tr className="border-b bg-muted/50">
                  <th className="px-4 py-2 text-left font-medium">Username</th>
                  <th className="px-4 py-2 text-left font-medium">Display Name</th>
                  <th className="px-4 py-2 text-left font-medium">Role</th>
                  <th className="px-4 py-2 text-left font-medium">Status</th>
                  <th className="px-4 py-2 text-left font-medium">Last Login</th>
                  <th className="px-4 py-2 text-right font-medium">Actions</th>
                </tr>
              </thead>
              <tbody>
                {users.map((u) => (
                  <tr key={u.id} className="border-b last:border-0">
                    <td className="px-4 py-2">
                      <Link href={`/admin/users/${u.id}`} className="font-medium text-primary hover:underline">
                        {u.username}
                      </Link>
                    </td>
                    <td className="px-4 py-2 text-muted-foreground">{u.displayName}</td>
                    <td className="px-4 py-2">
                      <span className={cn("inline-block rounded-full px-2 py-0.5 text-xs font-medium capitalize", roleBadgeColors[u.role] ?? "bg-gray-100")}>
                        {u.role}
                      </span>
                    </td>
                    <td className="px-4 py-2">
                      {u.isActive ? (
                        <span className="text-green-600 dark:text-green-400">Active</span>
                      ) : (
                        <span className="text-red-600 dark:text-red-400">Disabled</span>
                      )}
                    </td>
                    <td className="px-4 py-2 text-muted-foreground text-xs">
                      {u.lastLoginAt ? new Date(u.lastLoginAt).toLocaleString() : "Never"}
                    </td>
                    <td className="px-4 py-2 text-right">
                      {u.id !== currentUser?.id && can("UsersManage") && (
                        <Button variant="ghost" size="icon" className="h-8 w-8 text-destructive hover:text-destructive" onClick={() => handleDelete(u.id, u.username)}>
                          <Trash2 className="h-4 w-4" />
                        </Button>
                      )}
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>

          {pagination && pagination.totalPages > 1 && (
            <div className="flex items-center justify-between">
              <span className="text-sm text-muted-foreground">
                Page {pagination.page} of {pagination.totalPages} ({pagination.totalCount} total)
              </span>
              <div className="flex gap-2">
                <Button variant="outline" size="sm" disabled={pagination.page <= 1} onClick={() => setPage(page - 1)}>Previous</Button>
                <Button variant="outline" size="sm" disabled={pagination.page >= pagination.totalPages} onClick={() => setPage(page + 1)}>Next</Button>
              </div>
            </div>
          )}
        </>
      )}
    </div>
  );
}
```

Key changes from original:
- Removed the page heading (`<h1>Users</h1>`) — the tab label provides context
- Changed permission check from `UsersManage` to `UsersView` (tab is visible with view permission; write actions still gated by `UsersManage`)
- Updated links: `/settings/users/new` → `/admin/users/new`, `/settings/users/${u.id}` → `/admin/users/${u.id}`
- Moved search bar and Add User button into same row

- [ ] **Step 2: Verify TypeScript compiles**

Run: `cd src/Courier.Frontend && npx tsc --noEmit`
Expected: No type errors.

- [ ] **Step 3: Commit**

```bash
git add src/Courier.Frontend/src/app/\(app\)/admin/users-tab.tsx
git commit -m "feat: implement Users tab for admin page"
```

---

## Task 5: Implement Auth Providers Tab Component

Extract the auth providers list from `src/app/(app)/settings/auth-providers/page.tsx` into the admin Auth Providers tab. Read the existing page first to understand its full structure, then adapt it.

**Files:**
- Modify: `src/Courier.Frontend/src/app/(app)/admin/auth-providers-tab.tsx`
- Reference: `src/Courier.Frontend/src/app/(app)/settings/auth-providers/page.tsx` (read-only)

- [ ] **Step 1: Read the existing auth providers page**

Read `src/Courier.Frontend/src/app/(app)/settings/auth-providers/page.tsx` to understand the full component structure, imports, and layout.

- [ ] **Step 2: Implement the Auth Providers tab**

Replace `src/Courier.Frontend/src/app/(app)/admin/auth-providers-tab.tsx` with the content from the existing auth providers page, adapted for tab context:

- Remove the page heading (`<h1>Auth Providers</h1>`)
- Update all links from `/settings/auth-providers/...` to `/admin/auth-providers/...`
- Keep all existing functionality (provider list, add button, row actions, status badges)

The exact code depends on what the existing page contains. The implementing agent should read the existing page and replicate its body, making only these changes:
1. Export as `AuthProvidersTab` function (not default export)
2. Remove the outer heading section
3. Replace every `/settings/auth-providers` with `/admin/auth-providers`

- [ ] **Step 3: Verify TypeScript compiles**

Run: `cd src/Courier.Frontend && npx tsc --noEmit`
Expected: No type errors.

- [ ] **Step 4: Commit**

```bash
git add src/Courier.Frontend/src/app/\(app\)/admin/auth-providers-tab.tsx
git commit -m "feat: implement Auth Providers tab for admin page"
```

---

## Task 6: Implement Security Tab Component

Extract the auth settings form from `src/app/(app)/settings/page.tsx`'s `AuthSettingsTab` component.

**Files:**
- Modify: `src/Courier.Frontend/src/app/(app)/admin/security-tab.tsx`

- [ ] **Step 1: Implement the Security tab**

Replace `src/Courier.Frontend/src/app/(app)/admin/security-tab.tsx` with the `AuthSettingsTab` content from the settings page:

```tsx
"use client";

import { useState } from "react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { useAuthSettings, useUpdateAuthSettings } from "@/lib/hooks/use-settings";
import { usePermissions } from "@/lib/hooks/use-permissions";
import { toast } from "sonner";
import { ApiClientError } from "@/lib/api";

export function SecurityTab() {
  const { data, isLoading } = useAuthSettings();
  const updateSettings = useUpdateAuthSettings();
  const { can } = usePermissions();
  const settings = data?.data;
  const canEdit = can("SettingsManage");

  const [form, setForm] = useState<{
    sessionTimeoutMinutes: number;
    refreshTokenDays: number;
    passwordMinLength: number;
    maxLoginAttempts: number;
    lockoutDurationMinutes: number;
  } | null>(null);

  const currentForm = form ?? (settings ? {
    sessionTimeoutMinutes: settings.sessionTimeoutMinutes,
    refreshTokenDays: settings.refreshTokenDays,
    passwordMinLength: settings.passwordMinLength,
    maxLoginAttempts: settings.maxLoginAttempts,
    lockoutDurationMinutes: settings.lockoutDurationMinutes,
  } : null);

  if (isLoading || !currentForm) {
    return <div className="text-sm text-muted-foreground">Loading settings...</div>;
  }

  async function handleSave() {
    if (!currentForm) return;
    try {
      await updateSettings.mutateAsync(currentForm);
      toast.success("Security settings updated.");
    } catch (err) {
      if (err instanceof ApiClientError) {
        toast.error(err.message);
      } else {
        toast.error("Failed to update settings.");
      }
    }
  }

  return (
    <div className="space-y-6">
      <div>
        <h3 className="text-sm font-medium">Session</h3>
        <p className="text-xs text-muted-foreground mb-3">Configure token lifetimes for user sessions.</p>
        <div className="grid grid-cols-2 gap-4">
          <div className="space-y-2">
            <Label htmlFor="sessionTimeout">Access Token Lifetime (minutes)</Label>
            <Input
              id="sessionTimeout"
              type="number"
              min={1}
              value={currentForm.sessionTimeoutMinutes}
              onChange={(e) => setForm({ ...currentForm, sessionTimeoutMinutes: parseInt(e.target.value) || 1 })}
              disabled={!canEdit}
            />
          </div>
          <div className="space-y-2">
            <Label htmlFor="refreshDays">Refresh Token Lifetime (days)</Label>
            <Input
              id="refreshDays"
              type="number"
              min={1}
              value={currentForm.refreshTokenDays}
              onChange={(e) => setForm({ ...currentForm, refreshTokenDays: parseInt(e.target.value) || 1 })}
              disabled={!canEdit}
            />
          </div>
        </div>
      </div>

      <div>
        <h3 className="text-sm font-medium">Password Policy</h3>
        <p className="text-xs text-muted-foreground mb-3">Set minimum password requirements.</p>
        <div className="grid grid-cols-2 gap-4">
          <div className="space-y-2">
            <Label htmlFor="minLength">Minimum Length</Label>
            <Input
              id="minLength"
              type="number"
              min={4}
              value={currentForm.passwordMinLength}
              onChange={(e) => setForm({ ...currentForm, passwordMinLength: parseInt(e.target.value) || 4 })}
              disabled={!canEdit}
            />
          </div>
        </div>
      </div>

      <div>
        <h3 className="text-sm font-medium">Account Lockout</h3>
        <p className="text-xs text-muted-foreground mb-3">Protect against brute-force attacks.</p>
        <div className="grid grid-cols-2 gap-4">
          <div className="space-y-2">
            <Label htmlFor="maxAttempts">Max Failed Attempts</Label>
            <Input
              id="maxAttempts"
              type="number"
              min={1}
              value={currentForm.maxLoginAttempts}
              onChange={(e) => setForm({ ...currentForm, maxLoginAttempts: parseInt(e.target.value) || 1 })}
              disabled={!canEdit}
            />
          </div>
          <div className="space-y-2">
            <Label htmlFor="lockoutDuration">Lockout Duration (minutes)</Label>
            <Input
              id="lockoutDuration"
              type="number"
              min={1}
              value={currentForm.lockoutDurationMinutes}
              onChange={(e) => setForm({ ...currentForm, lockoutDurationMinutes: parseInt(e.target.value) || 1 })}
              disabled={!canEdit}
            />
          </div>
        </div>
      </div>

      {canEdit && (
        <Button onClick={handleSave} disabled={updateSettings.isPending}>
          {updateSettings.isPending ? "Saving..." : "Save Changes"}
        </Button>
      )}
    </div>
  );
}
```

Key changes from original `AuthSettingsTab`:
- Exported as named `SecurityTab` function
- Added `SettingsManage` permission check to disable inputs for view-only users
- Toast message says "Security settings" instead of "Authentication settings"

- [ ] **Step 2: Verify TypeScript compiles**

Run: `cd src/Courier.Frontend && npx tsc --noEmit`
Expected: No type errors.

- [ ] **Step 3: Commit**

```bash
git add src/Courier.Frontend/src/app/\(app\)/admin/security-tab.tsx
git commit -m "feat: implement Security tab for admin page"
```

---

## Task 7: Move User CRUD Sub-Pages to /admin/users/*

Move the create and edit user pages from `/settings/users/` to `/admin/users/`, updating internal route references.

**Files:**
- Create: `src/Courier.Frontend/src/app/(app)/admin/users/new/page.tsx`
- Create: `src/Courier.Frontend/src/app/(app)/admin/users/[id]/page.tsx`

- [ ] **Step 1: Create directories**

```bash
mkdir -p src/Courier.Frontend/src/app/\(app\)/admin/users/new
mkdir -p src/Courier.Frontend/src/app/\(app\)/admin/users/\[id\]
```

- [ ] **Step 2: Create the new user page**

Create `src/Courier.Frontend/src/app/(app)/admin/users/new/page.tsx` with the same content as the existing `settings/users/new/page.tsx`, but with all `/settings/users` references changed to `/admin/users`:

```tsx
"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { useCreateUser } from "@/lib/hooks/use-users";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { toast } from "sonner";
import { ApiClientError } from "@/lib/api";

export default function NewUserPage() {
  const router = useRouter();
  const createUser = useCreateUser();
  const [username, setUsername] = useState("");
  const [displayName, setDisplayName] = useState("");
  const [email, setEmail] = useState("");
  const [password, setPassword] = useState("");
  const [confirmPassword, setConfirmPassword] = useState("");
  const [role, setRole] = useState("viewer");

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (password !== confirmPassword) {
      toast.error("Passwords do not match.");
      return;
    }
    try {
      await createUser.mutateAsync({
        username,
        displayName,
        email: email || undefined,
        password,
        confirmPassword,
        role,
      });
      toast.success("User created successfully.");
      router.push("/admin/users");
    } catch (err) {
      if (err instanceof ApiClientError) toast.error(err.message);
      else toast.error("Failed to create user.");
    }
  }

  return (
    <div className="space-y-6">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">Create User</h1>
        <p className="text-sm text-muted-foreground">Add a new user account.</p>
      </div>

      <form onSubmit={handleSubmit} className="max-w-lg space-y-4">
        <div className="space-y-2">
          <Label htmlFor="username">Username</Label>
          <Input id="username" value={username} onChange={(e) => setUsername(e.target.value)} required />
        </div>

        <div className="space-y-2">
          <Label htmlFor="displayName">Display Name</Label>
          <Input id="displayName" value={displayName} onChange={(e) => setDisplayName(e.target.value)} required />
        </div>

        <div className="space-y-2">
          <Label htmlFor="email">Email <span className="text-muted-foreground">(optional)</span></Label>
          <Input id="email" type="email" value={email} onChange={(e) => setEmail(e.target.value)} />
        </div>

        <div className="space-y-2">
          <Label htmlFor="role">Role</Label>
          <select
            id="role"
            className="flex h-9 w-full rounded-md border border-input bg-transparent px-3 py-1 text-sm shadow-sm transition-colors focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring"
            value={role}
            onChange={(e) => setRole(e.target.value)}
          >
            <option value="admin">Admin</option>
            <option value="operator">Operator</option>
            <option value="viewer">Viewer</option>
          </select>
        </div>

        <div className="space-y-2">
          <Label htmlFor="password">Password</Label>
          <Input id="password" type="password" value={password} onChange={(e) => setPassword(e.target.value)} required />
        </div>

        <div className="space-y-2">
          <Label htmlFor="confirmPassword">Confirm Password</Label>
          <Input id="confirmPassword" type="password" value={confirmPassword} onChange={(e) => setConfirmPassword(e.target.value)} required />
        </div>

        <div className="flex gap-3">
          <Button type="submit" disabled={createUser.isPending}>
            {createUser.isPending ? "Creating..." : "Create User"}
          </Button>
          <Button type="button" variant="outline" onClick={() => router.push("/admin/users")}>
            Cancel
          </Button>
        </div>
      </form>
    </div>
  );
}
```

Note: The "Cancel" button and success redirect both go to `/admin/users` — since this is a standalone page (not within the tab), it navigates back to the admin page which will show the Users tab.

- [ ] **Step 3: Create the edit user page**

Create `src/Courier.Frontend/src/app/(app)/admin/users/[id]/page.tsx` with the same content as the existing `settings/users/[id]/page.tsx`, but with `/settings/users` changed to `/admin/users`:

```tsx
"use client";

import { useState, useEffect } from "react";
import { useParams, useRouter } from "next/navigation";
import { useUser, useUpdateUser, useResetUserPassword } from "@/lib/hooks/use-users";
import { useAuth } from "@/lib/auth";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { toast } from "sonner";
import { ApiClientError } from "@/lib/api";

export default function UserDetailPage() {
  const params = useParams();
  const router = useRouter();
  const id = params.id as string;
  const { data, isLoading } = useUser(id);
  const updateUser = useUpdateUser(id);
  const resetPassword = useResetUserPassword(id);
  const { user: currentUser } = useAuth();

  const userData = data?.data;

  const [displayName, setDisplayName] = useState("");
  const [email, setEmail] = useState("");
  const [role, setRole] = useState("viewer");
  const [isActive, setIsActive] = useState(true);
  const [newPassword, setNewPassword] = useState("");
  const [confirmNewPassword, setConfirmNewPassword] = useState("");
  const [initialized, setInitialized] = useState(false);

  useEffect(() => {
    if (userData && !initialized) {
      setDisplayName(userData.displayName);
      setEmail(userData.email ?? "");
      setRole(userData.role);
      setIsActive(userData.isActive);
      setInitialized(true);
    }
  }, [userData, initialized]);

  if (isLoading) return <div className="text-sm text-muted-foreground">Loading user...</div>;
  if (!userData) return <div className="text-sm text-muted-foreground">User not found.</div>;

  async function handleUpdate(e: React.FormEvent) {
    e.preventDefault();
    try {
      await updateUser.mutateAsync({
        displayName,
        email: email || undefined,
        role,
        isActive,
      });
      toast.success("User updated successfully.");
    } catch (err) {
      if (err instanceof ApiClientError) toast.error(err.message);
      else toast.error("Failed to update user.");
    }
  }

  async function handleResetPassword(e: React.FormEvent) {
    e.preventDefault();
    if (newPassword !== confirmNewPassword) {
      toast.error("Passwords do not match.");
      return;
    }
    try {
      await resetPassword.mutateAsync({ password: newPassword, confirmPassword: confirmNewPassword });
      toast.success("Password reset successfully.");
      setNewPassword("");
      setConfirmNewPassword("");
    } catch (err) {
      if (err instanceof ApiClientError) toast.error(err.message);
      else toast.error("Failed to reset password.");
    }
  }

  const isSelf = currentUser?.id === id;

  return (
    <div className="space-y-8">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">{userData.username}</h1>
        <p className="text-sm text-muted-foreground">
          Created {new Date(userData.createdAt).toLocaleDateString()}
          {userData.lastLoginAt && ` · Last login ${new Date(userData.lastLoginAt).toLocaleString()}`}
        </p>
      </div>

      <form onSubmit={handleUpdate} className="max-w-lg space-y-4">
        <h2 className="text-lg font-medium">Account Details</h2>

        <div className="space-y-2">
          <Label htmlFor="displayName">Display Name</Label>
          <Input id="displayName" value={displayName} onChange={(e) => setDisplayName(e.target.value)} required />
        </div>

        <div className="space-y-2">
          <Label htmlFor="email">Email</Label>
          <Input id="email" type="email" value={email} onChange={(e) => setEmail(e.target.value)} />
        </div>

        <div className="space-y-2">
          <Label htmlFor="role">Role</Label>
          <select
            id="role"
            className="flex h-9 w-full rounded-md border border-input bg-transparent px-3 py-1 text-sm shadow-sm transition-colors focus-visible:outline-none focus-visible:ring-1 focus-visible:ring-ring"
            value={role}
            onChange={(e) => setRole(e.target.value)}
            disabled={isSelf}
          >
            <option value="admin">Admin</option>
            <option value="operator">Operator</option>
            <option value="viewer">Viewer</option>
          </select>
          {isSelf && <p className="text-xs text-muted-foreground">You cannot change your own role.</p>}
        </div>

        <div className="flex items-center gap-2">
          <input
            type="checkbox"
            id="isActive"
            checked={isActive}
            onChange={(e) => setIsActive(e.target.checked)}
            disabled={isSelf}
            className="h-4 w-4 rounded border-input"
          />
          <Label htmlFor="isActive">Account Active</Label>
        </div>

        <div className="flex gap-3">
          <Button type="submit" disabled={updateUser.isPending}>
            {updateUser.isPending ? "Saving..." : "Save Changes"}
          </Button>
          <Button type="button" variant="outline" onClick={() => router.push("/admin")}>
            Back
          </Button>
        </div>
      </form>

      {!isSelf && (
        <form onSubmit={handleResetPassword} className="max-w-lg space-y-4 border-t pt-6">
          <h2 className="text-lg font-medium">Reset Password</h2>
          <p className="text-sm text-muted-foreground">Set a new password for this user. All their active sessions will be revoked.</p>

          <div className="space-y-2">
            <Label htmlFor="newPassword">New Password</Label>
            <Input id="newPassword" type="password" value={newPassword} onChange={(e) => setNewPassword(e.target.value)} required />
          </div>

          <div className="space-y-2">
            <Label htmlFor="confirmNewPassword">Confirm New Password</Label>
            <Input id="confirmNewPassword" type="password" value={confirmNewPassword} onChange={(e) => setConfirmNewPassword(e.target.value)} required />
          </div>

          <Button type="submit" variant="destructive" disabled={resetPassword.isPending}>
            {resetPassword.isPending ? "Resetting..." : "Reset Password"}
          </Button>
        </form>
      )}
    </div>
  );
}
```

Key change: "Back" button navigates to `/admin` (not `/admin/users`, since `/admin` shows the Users tab by default).

- [ ] **Step 4: Verify TypeScript compiles**

Run: `cd src/Courier.Frontend && npx tsc --noEmit`
Expected: No type errors.

- [ ] **Step 5: Commit**

```bash
git add src/Courier.Frontend/src/app/\(app\)/admin/users/
git commit -m "feat: add user CRUD sub-pages under /admin/users"
```

---

## Task 8: Move Auth Provider CRUD Sub-Pages to /admin/auth-providers/*

Move the create and edit auth provider pages from `/settings/auth-providers/` to `/admin/auth-providers/`, updating internal route references.

**Files:**
- Create: `src/Courier.Frontend/src/app/(app)/admin/auth-providers/new/page.tsx`
- Create: `src/Courier.Frontend/src/app/(app)/admin/auth-providers/[id]/page.tsx`

- [ ] **Step 1: Create directories**

```bash
mkdir -p src/Courier.Frontend/src/app/\(app\)/admin/auth-providers/new
mkdir -p src/Courier.Frontend/src/app/\(app\)/admin/auth-providers/\[id\]
```

- [ ] **Step 2: Read the existing auth provider new page**

Read `src/Courier.Frontend/src/app/(app)/settings/auth-providers/new/page.tsx` to understand the full component.

- [ ] **Step 3: Create the new auth provider page**

Create `src/Courier.Frontend/src/app/(app)/admin/auth-providers/new/page.tsx` with the same content as the existing page, but replace every `/settings/auth-providers` with `/admin/auth-providers`. The two references to update are:
- Line ~109: `router.push("/settings/auth-providers")` → `router.push("/admin/auth-providers")`
- Line ~440: `onClick={() => router.push("/settings/auth-providers")}` → `onClick={() => router.push("/admin")}`

Since this is a large file, copy it in full and do a find-replace of `/settings/auth-providers` → `/admin/auth-providers`. For the "Back"/"Cancel" button navigation, use `/admin` (which defaults to the first visible tab — for admins that's Users, but this is acceptable since they'd click the Auth Providers tab).

**Correction:** The "Back" button should navigate to `/admin` — the admin page will show the last-active tab or default. Since we can't deep-link to a specific tab via URL in this design, `/admin` is the correct target. The user will see whatever tab was last active.

- [ ] **Step 4: Read the existing auth provider edit page**

Read `src/Courier.Frontend/src/app/(app)/settings/auth-providers/[id]/page.tsx` to understand the full component.

- [ ] **Step 5: Create the edit auth provider page**

Create `src/Courier.Frontend/src/app/(app)/admin/auth-providers/[id]/page.tsx` with the same content as the existing page, replacing `/settings/auth-providers` with `/admin` for back navigation.

The one reference to update (from the Grep results):
- Line ~536: `onClick={() => router.push("/settings/auth-providers")}` → `onClick={() => router.push("/admin")}`

- [ ] **Step 6: Verify TypeScript compiles**

Run: `cd src/Courier.Frontend && npx tsc --noEmit`
Expected: No type errors.

- [ ] **Step 7: Commit**

```bash
git add src/Courier.Frontend/src/app/\(app\)/admin/auth-providers/
git commit -m "feat: add auth provider CRUD sub-pages under /admin/auth-providers"
```

---

## Task 9: Create Account Page

New page at `/account` showing the read-only user profile and conditional change password form.

**Files:**
- Create: `src/Courier.Frontend/src/app/(app)/account/page.tsx`

- [ ] **Step 1: Create the account directory**

```bash
mkdir -p src/Courier.Frontend/src/app/\(app\)/account
```

- [ ] **Step 2: Create the Account page**

Create `src/Courier.Frontend/src/app/(app)/account/page.tsx`:

```tsx
"use client";

import { useState } from "react";
import { useAuth } from "@/lib/auth";
import { useChangePassword } from "@/lib/hooks/use-auth-actions";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { toast } from "sonner";
import { ApiClientError } from "@/lib/api";
import { cn } from "@/lib/utils";
import { Info } from "lucide-react";

const roleBadgeColors: Record<string, string> = {
  admin: "bg-red-100 text-red-800 dark:bg-red-900/30 dark:text-red-400",
  operator: "bg-blue-100 text-blue-800 dark:bg-blue-900/30 dark:text-blue-400",
  viewer: "bg-gray-100 text-gray-800 dark:bg-gray-900/30 dark:text-gray-400",
};

function ChangePasswordForm() {
  const changePassword = useChangePassword();
  const [currentPassword, setCurrentPassword] = useState("");
  const [newPassword, setNewPassword] = useState("");
  const [confirmNewPassword, setConfirmNewPassword] = useState("");

  async function handleSubmit(e: React.FormEvent) {
    e.preventDefault();
    if (newPassword !== confirmNewPassword) {
      toast.error("Passwords do not match.");
      return;
    }
    try {
      await changePassword.mutateAsync({ currentPassword, newPassword, confirmNewPassword });
      toast.success("Password changed successfully.");
      setCurrentPassword("");
      setNewPassword("");
      setConfirmNewPassword("");
    } catch (err) {
      if (err instanceof ApiClientError) {
        toast.error(err.message);
      } else {
        toast.error("Failed to change password.");
      }
    }
  }

  return (
    <form onSubmit={handleSubmit} className="max-w-md space-y-4">
      <div className="space-y-2">
        <Label htmlFor="currentPassword">Current Password</Label>
        <Input
          id="currentPassword"
          type="password"
          value={currentPassword}
          onChange={(e) => setCurrentPassword(e.target.value)}
          required
        />
      </div>
      <div className="space-y-2">
        <Label htmlFor="newPassword">New Password</Label>
        <Input
          id="newPassword"
          type="password"
          value={newPassword}
          onChange={(e) => setNewPassword(e.target.value)}
          required
        />
      </div>
      <div className="space-y-2">
        <Label htmlFor="confirmNewPassword">Confirm New Password</Label>
        <Input
          id="confirmNewPassword"
          type="password"
          value={confirmNewPassword}
          onChange={(e) => setConfirmNewPassword(e.target.value)}
          required
        />
      </div>
      <Button type="submit" disabled={changePassword.isPending}>
        {changePassword.isPending ? "Changing..." : "Change Password"}
      </Button>
    </form>
  );
}

export default function AccountPage() {
  const { user } = useAuth();

  if (!user) return null;

  const showChangePassword = !user.isSsoUser || user.allowLocalPassword;

  return (
    <div className="space-y-8">
      <div>
        <h1 className="text-2xl font-semibold tracking-tight">My Account</h1>
        <p className="text-sm text-muted-foreground">View your account details.</p>
      </div>

      <div className="max-w-lg space-y-4">
        <h2 className="text-lg font-medium">Profile</h2>

        <div className="rounded-md border divide-y">
          <div className="flex justify-between px-4 py-3">
            <span className="text-sm text-muted-foreground">Display Name</span>
            <span className="text-sm font-medium">{user.displayName}</span>
          </div>
          <div className="flex justify-between px-4 py-3">
            <span className="text-sm text-muted-foreground">Username</span>
            <span className="text-sm font-medium">{user.username}</span>
          </div>
          <div className="flex justify-between px-4 py-3">
            <span className="text-sm text-muted-foreground">Email</span>
            <span className="text-sm font-medium">{user.email ?? "—"}</span>
          </div>
          <div className="flex justify-between px-4 py-3">
            <span className="text-sm text-muted-foreground">Role</span>
            <span className={cn("inline-block rounded-full px-2 py-0.5 text-xs font-medium capitalize", roleBadgeColors[user.role] ?? "bg-gray-100")}>
              {user.role}
            </span>
          </div>
          {user.isSsoUser && user.ssoProviderName && (
            <div className="flex justify-between px-4 py-3">
              <span className="text-sm text-muted-foreground">SSO Provider</span>
              <span className="text-sm font-medium">{user.ssoProviderName}</span>
            </div>
          )}
          {user.lastLoginAt && (
            <div className="flex justify-between px-4 py-3">
              <span className="text-sm text-muted-foreground">Last Login</span>
              <span className="text-sm font-medium">{new Date(user.lastLoginAt).toLocaleString()}</span>
            </div>
          )}
        </div>
      </div>

      <div className="max-w-lg space-y-4 border-t pt-6">
        <h2 className="text-lg font-medium">Change Password</h2>

        {showChangePassword ? (
          <ChangePasswordForm />
        ) : (
          <div className="flex items-start gap-3 rounded-md border border-blue-200 bg-blue-50 p-4 dark:border-blue-800 dark:bg-blue-950">
            <Info className="h-5 w-5 text-blue-600 dark:text-blue-400 mt-0.5 shrink-0" />
            <p className="text-sm text-blue-800 dark:text-blue-200">
              Your password is managed by <strong>{user.ssoProviderName}</strong>. Contact your identity provider administrator to change your password.
            </p>
          </div>
        )}
      </div>
    </div>
  );
}
```

- [ ] **Step 3: Verify TypeScript compiles**

Run: `cd src/Courier.Frontend && npx tsc --noEmit`
Expected: No type errors. Note: If `user.isSsoUser` or `user.allowLocalPassword` aren't recognized yet (because the auth context still uses the old type), this will fail. Ensure Task 2 (types update) is complete first.

- [ ] **Step 4: Commit**

```bash
git add src/Courier.Frontend/src/app/\(app\)/account/
git commit -m "feat: create account page with SSO-aware change password"
```

---

## Task 10: Update Sidebar and Topbar Navigation

Update the sidebar to replace Settings + Auth Providers with a single Admin link. Update the topbar avatar menu to show "My Account" instead of "Profile" and "Change Password". Update breadcrumb mappings.

**Files:**
- Modify: `src/Courier.Frontend/src/components/layout/sidebar.tsx`
- Modify: `src/Courier.Frontend/src/components/layout/topbar.tsx`

- [ ] **Step 1: Update the sidebar**

In `src/Courier.Frontend/src/components/layout/sidebar.tsx`:

1. Replace the `Settings` import from lucide-react with `ShieldCheck` (already imported, so remove `Settings` from the import).

2. Replace the `bottomItems` array:

Old (line 40-42):
```tsx
const bottomItems = [
  { label: "Settings", href: "/settings", icon: Settings, active: true },
];
```

New:
```tsx
const bottomItems: { label: string; href: string; icon: React.ComponentType<{ className?: string }>; active: boolean }[] = [];
```

3. Replace the bottom section that renders `bottomItems` and the hardcoded Auth Providers link (lines 98-135) with a single Admin link gated by `canAny`:

Replace the entire `<div className="border-t border-sidebar-border p-2">` block with:

```tsx
<div className="border-t border-sidebar-border p-2">
  {canAny("UsersView", "AuthProvidersView", "SettingsView") && (
    <Link
      href="/admin"
      className={cn(
        "flex items-center gap-3 rounded-md px-3 py-2 text-sm font-medium transition-colors",
        pathname.startsWith("/admin")
          ? "bg-sidebar-accent text-sidebar-primary"
          : "text-sidebar-foreground hover:bg-sidebar-accent/60 hover:text-sidebar-accent-foreground"
      )}
    >
      <ShieldCheck className="h-4 w-4 shrink-0" />
      {!collapsed && <span>Admin</span>}
    </Link>
  )}

  <Button
    variant="ghost"
    size="sm"
    className="mt-2 w-full text-sidebar-foreground/50 hover:text-sidebar-foreground hover:bg-sidebar-accent/60"
    onClick={() => setCollapsed(!collapsed)}
  >
    {collapsed ? <ChevronRight className="h-4 w-4" /> : <ChevronLeft className="h-4 w-4" />}
    {!collapsed && <span className="ml-2">Collapse</span>}
  </Button>
</div>
```

4. Remove `Settings` from the lucide-react import (line 8). Keep `ShieldCheck`.

5. Remove the now-empty `bottomItems` array entirely if nothing uses it.

- [ ] **Step 2: Update the topbar avatar menu**

In `src/Courier.Frontend/src/components/layout/topbar.tsx`:

1. Replace the two dropdown menu items (Profile + Change Password) with a single "My Account" item. Remove the `KeyRound` import since it's no longer needed.

Old (lines 96-103):
```tsx
<DropdownMenuItem onSelect={() => setTimeout(() => router.push("/settings/users"))}>
  <User className="mr-2 h-4 w-4" />
  Profile
</DropdownMenuItem>
<DropdownMenuItem onSelect={() => setTimeout(() => router.push("/settings"))}>
  <KeyRound className="mr-2 h-4 w-4" />
  Change Password
</DropdownMenuItem>
```

New:
```tsx
<DropdownMenuItem onSelect={() => setTimeout(() => router.push("/account"))}>
  <User className="mr-2 h-4 w-4" />
  My Account
</DropdownMenuItem>
```

2. Remove `KeyRound` from the lucide-react import (line 5).

- [ ] **Step 3: Update breadcrumb mappings in topbar**

In the `useBreadcrumbs` function in `topbar.tsx`, add mappings for the new routes. In the `if/else if` chain (lines 26-43), add:

```tsx
else if (label === "admin") label = "Admin";
else if (label === "account") label = "My Account";
else if (label === "auth-providers") label = "Auth Providers";
else if (label === "security") label = "Security";
```

The `auth-providers` mapping is needed because the old route was under `/settings/auth-providers` (so it showed as "Settings > auth-providers"), but now it's `/admin/auth-providers`.

- [ ] **Step 4: Verify TypeScript compiles**

Run: `cd src/Courier.Frontend && npx tsc --noEmit`
Expected: No type errors.

- [ ] **Step 5: Commit**

```bash
git add src/Courier.Frontend/src/components/layout/sidebar.tsx src/Courier.Frontend/src/components/layout/topbar.tsx
git commit -m "feat: update navigation — Admin link in sidebar, My Account in avatar menu"
```

---

## Task 11: Remove Old /settings Routes

Delete the old settings directory and all its contents. The admin page and account page now serve all the same functionality.

**Files:**
- Delete: `src/Courier.Frontend/src/app/(app)/settings/` (entire directory)

- [ ] **Step 1: Verify new routes are in place**

Before deleting, confirm the new files exist:

```bash
ls src/Courier.Frontend/src/app/\(app\)/admin/page.tsx
ls src/Courier.Frontend/src/app/\(app\)/admin/users-tab.tsx
ls src/Courier.Frontend/src/app/\(app\)/admin/auth-providers-tab.tsx
ls src/Courier.Frontend/src/app/\(app\)/admin/security-tab.tsx
ls src/Courier.Frontend/src/app/\(app\)/admin/users/new/page.tsx
ls src/Courier.Frontend/src/app/\(app\)/admin/users/\[id\]/page.tsx
ls src/Courier.Frontend/src/app/\(app\)/admin/auth-providers/new/page.tsx
ls src/Courier.Frontend/src/app/\(app\)/admin/auth-providers/\[id\]/page.tsx
ls src/Courier.Frontend/src/app/\(app\)/account/page.tsx
```

Expected: All files exist.

- [ ] **Step 2: Delete the old settings directory**

```bash
rm -rf src/Courier.Frontend/src/app/\(app\)/settings
```

- [ ] **Step 3: Verify no broken imports**

Run: `cd src/Courier.Frontend && npx tsc --noEmit`
Expected: No type errors. The settings pages had no shared exports used elsewhere.

- [ ] **Step 4: Commit**

```bash
git add -A src/Courier.Frontend/src/app/\(app\)/settings/
git commit -m "chore: remove old /settings routes — replaced by /admin and /account"
```

---

## Task 12: Update E2E Tests for New Routes

Update all E2E test files that reference old `/settings` routes. The test logic stays the same — only route strings change.

**Files:**
- Modify: `src/Courier.Frontend/e2e/users.spec.ts`
- Modify: `src/Courier.Frontend/e2e/settings.spec.ts`
- Modify: `src/Courier.Frontend/e2e/navigation.spec.ts`

- [ ] **Step 1: Update users.spec.ts**

In `src/Courier.Frontend/e2e/users.spec.ts`, do a global find-replace:
- `/settings/users` → `/admin/users`

This covers all ~20 references: `goto("/settings/users")`, `goto("/settings/users/new")`, `goto(\`/settings/users/${user.id}\`)`, `toHaveURL("/settings/users"...)`, etc.

- [ ] **Step 2: Update settings.spec.ts**

In `src/Courier.Frontend/e2e/settings.spec.ts`, the tests cover:
- Auth settings (session timeout, password policy, lockout) → now at `/admin` Security tab
- Change password → now at `/account`

This file needs more than a simple find-replace. Read the full file first, then:

1. Tests that navigate to `/settings` and interact with the "Authentication" tab should instead navigate to `/admin` and click the "Security" tab trigger.
2. Tests that interact with the "Change Password" tab should navigate to `/account` instead.
3. Update assertions that check for `/settings` URL to check for `/admin` or `/account` as appropriate.

The exact changes depend on the test structure — read the file and adapt each test case.

- [ ] **Step 3: Update navigation.spec.ts**

In `src/Courier.Frontend/e2e/navigation.spec.ts`:
- Line ~119-120: Update the "Profile" navigation test — it should now click "My Account" in the avatar menu and verify navigation to `/account` (not `/settings/users`).
- Line ~139-140: Update the "Settings" navigation test — the sidebar now shows "Admin" instead of "Settings", and navigates to `/admin` (not `/settings`).

Read the full file to understand all navigation assertions, then update accordingly.

- [ ] **Step 4: Run E2E tests to verify (requires running Aspire stack)**

If the Aspire stack is running:

```bash
cd src/Courier.Frontend && npx playwright test e2e/users.spec.ts e2e/settings.spec.ts e2e/navigation.spec.ts --reporter=list 2>&1 | tail -20
```

If the Aspire stack is not running, at minimum verify TypeScript compilation:

```bash
cd src/Courier.Frontend && npx tsc --noEmit
```

- [ ] **Step 5: Commit**

```bash
git add src/Courier.Frontend/e2e/users.spec.ts src/Courier.Frontend/e2e/settings.spec.ts src/Courier.Frontend/e2e/navigation.spec.ts
git commit -m "test: update E2E tests for /admin and /account route migration"
```

---

## Verification Checklist

After all tasks are complete:

- [ ] `dotnet build Courier.slnx` — builds successfully
- [ ] `dotnet test tests/Courier.Tests.Unit` — all unit tests pass
- [ ] `cd src/Courier.Frontend && npx tsc --noEmit` — no TypeScript errors
- [ ] No references to `/settings/users`, `/settings/auth-providers`, or standalone `/settings` remain in `src/` (except `api.ts` which hits `/api/v1/settings/auth` — that's a backend API path, not a frontend route)
- [ ] The old `src/app/(app)/settings/` directory is deleted
- [ ] Manual smoke test (if Aspire stack is running):
  - Admin sidebar link visible for admin users
  - Admin page shows Users, Auth Providers, Security tabs
  - Operator sees only Auth Providers tab on Admin page
  - Viewer does not see Admin link in sidebar
  - Avatar menu shows "My Account" → navigates to `/account`
  - Account page shows profile info (read-only) and change password form
  - All CRUD operations work from new routes (`/admin/users/new`, `/admin/users/[id]`, etc.)
