# RBAC Policy-Based Authorization — Design Spec

**Date**: 2026-03-21
**Status**: Reviewed (spec review pass 1 — all critical/major issues resolved)
**Scope**: Replace role-string `[Authorize(Roles = "...")]` with policy-based `[RequirePermission(...)]` across all controllers, add frontend role-gating, and build a comprehensive RBAC integration test suite.

---

## 1. Problem

Courier's V1 RBAC is implemented via `[Authorize(Roles = "admin,operator")]` string attributes on 67 endpoints across 21 controllers. This works but has issues:

- **No compile-time safety** — role strings are typo-prone and not refactorable
- **No single source of truth** — the role→permission mapping is spread across controller attributes
- **No integration tests** — zero tests prove the permission matrix works
- **Frontend doesn't hide unauthorized actions** — Viewers see buttons the API will reject
- **Not extensible** — adding a new role or permission requires touching every controller

## 2. Design

### 2.1 Permission Enum (Domain Layer)

A `Permission` enum in `Courier.Domain.Enums` defines every authorized action. Each value maps to a specific resource + action from the design doc's permission matrix (Section 12.2).

```csharp
namespace Courier.Domain.Enums;

public enum Permission
{
    // Jobs
    JobsView,
    JobsCreate,
    JobsEdit,
    JobsDelete,
    JobsExecute,
    JobsManageSchedules,
    JobsManageDependencies,

    // Chains
    ChainsView,
    ChainsCreate,
    ChainsEdit,
    ChainsDelete,
    ChainsExecute,
    ChainsManageSchedules,

    // Connections
    ConnectionsView,
    ConnectionsCreate,
    ConnectionsEdit,
    ConnectionsDelete,
    ConnectionsTest,

    // PGP Keys
    PgpKeysView,
    PgpKeysManage,
    PgpKeysExportPublic,
    PgpKeysManageSharing,

    // SSH Keys
    SshKeysView,
    SshKeysManage,
    SshKeysExportPublic,
    SshKeysManageSharing,

    // File Monitors
    MonitorsView,
    MonitorsCreate,
    MonitorsEdit,
    MonitorsDelete,
    MonitorsChangeState,

    // Tags
    TagsView,
    TagsManage,

    // Notifications
    NotificationRulesView,
    NotificationRulesManage,
    NotificationLogsView,

    // Audit
    AuditLogView,

    // Users
    UsersView,
    UsersManage,

    // Settings
    SettingsView,
    SettingsManage,

    // Dashboard
    DashboardView,

    // Filesystem
    FilesystemBrowse,

    // Known Hosts
    KnownHostsView,
    KnownHostsManage,
}
```

### 2.2 Role → Permission Mapping (Domain Layer)

A static `RolePermissions` class is the single source of truth for which roles have which permissions. Lives in `Courier.Domain.Authorization`.

```csharp
namespace Courier.Domain.Authorization;

public static class RolePermissions
{
    private static readonly FrozenSet<Permission> AdminPermissions = new HashSet<Permission>
    {
        // Admin gets ALL permissions
        // (every Permission enum value)
    }.ToFrozenSet();

    private static readonly FrozenSet<Permission> OperatorPermissions = new HashSet<Permission>
    {
        // Jobs: full operational access
        Permission.JobsView, Permission.JobsCreate, Permission.JobsEdit,
        Permission.JobsDelete, Permission.JobsExecute,
        Permission.JobsManageSchedules, Permission.JobsManageDependencies,

        // Chains: full operational access
        Permission.ChainsView, Permission.ChainsCreate, Permission.ChainsEdit,
        Permission.ChainsDelete, Permission.ChainsExecute,
        Permission.ChainsManageSchedules,

        // Connections: view + test only
        Permission.ConnectionsView, Permission.ConnectionsTest,

        // Keys: view + export public only
        Permission.PgpKeysView, Permission.PgpKeysExportPublic,
        Permission.SshKeysView, Permission.SshKeysExportPublic,

        // Monitors: full operational access
        Permission.MonitorsView, Permission.MonitorsCreate, Permission.MonitorsEdit,
        Permission.MonitorsDelete, Permission.MonitorsChangeState,

        // Tags: full access
        Permission.TagsView, Permission.TagsManage,

        // Notifications: full access
        Permission.NotificationRulesView, Permission.NotificationRulesManage,
        Permission.NotificationLogsView,

        // Read-only shared
        Permission.AuditLogView, Permission.DashboardView,
        Permission.SettingsView, Permission.FilesystemBrowse,
        Permission.KnownHostsView,
    }.ToFrozenSet();

    private static readonly FrozenSet<Permission> ViewerPermissions = new HashSet<Permission>
    {
        // View-only across all resources
        Permission.JobsView,
        Permission.ChainsView,
        Permission.ConnectionsView,
        Permission.PgpKeysView, Permission.PgpKeysExportPublic,
        Permission.SshKeysView, Permission.SshKeysExportPublic,
        Permission.MonitorsView,
        Permission.TagsView,
        Permission.NotificationRulesView, Permission.NotificationLogsView,
        Permission.AuditLogView,
        Permission.DashboardView,
        Permission.SettingsView,
        Permission.FilesystemBrowse,
        Permission.KnownHostsView,
    }.ToFrozenSet();

    public static IReadOnlySet<Permission> GetPermissions(string role) => role.ToLowerInvariant() switch
    {
        "admin"    => AdminPermissions,
        "operator" => OperatorPermissions,
        "viewer"   => ViewerPermissions,
        _          => FrozenSet<Permission>.Empty,
    };

    public static bool HasPermission(string role, Permission permission)
        => GetPermissions(role).Contains(permission);
}
```

### 2.3 Authorization Infrastructure (Features/Security)

Three classes in `Courier.Features.Security`:

**PermissionRequirement.cs**:
```csharp
public class PermissionRequirement : IAuthorizationRequirement
{
    public Permission Permission { get; }
    public PermissionRequirement(Permission permission) => Permission = permission;
}
```

**PermissionHandler.cs**:
```csharp
// Registered as Singleton because it is stateless — reads only from the static
// RolePermissions class. If dynamic permission lookup is ever needed (e.g., DB-backed
// permissions), change registration to Scoped to avoid captive dependency bugs.
public class PermissionHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
        // Check ALL role claims, not just the first — supports future multi-role assignments.
        // Currently each user has exactly one role, but using .Any() prevents a silent
        // security regression if multi-role is added later.
        var roles = context.User.Claims
            .Where(c => c.Type == ClaimTypes.Role)
            .Select(c => c.Value);

        if (roles.Any(role => RolePermissions.HasPermission(role, requirement.Permission)))
        {
            context.Succeed(requirement);
        }
        return Task.CompletedTask;
    }
}
```

**PermissionPolicyProvider.cs**:
```csharp
public class PermissionPolicyProvider : IAuthorizationPolicyProvider
{
    private readonly DefaultAuthorizationPolicyProvider _fallback;
    private readonly ConcurrentDictionary<string, AuthorizationPolicy> _cache = new();

    public PermissionPolicyProvider(IOptions<AuthorizationOptions> options)
        => _fallback = new DefaultAuthorizationPolicyProvider(options);

    public Task<AuthorizationPolicy?> GetPolicyAsync(string policyName)
    {
        if (Enum.TryParse<Permission>(policyName, out var permission))
        {
            var policy = _cache.GetOrAdd(policyName, _ =>
                new AuthorizationPolicyBuilder()
                    .RequireAuthenticatedUser()
                    .AddRequirements(new PermissionRequirement(permission))
                    .Build());
            return Task.FromResult<AuthorizationPolicy?>(policy);
        }
        return _fallback.GetPolicyAsync(policyName);
    }

    public Task<AuthorizationPolicy> GetDefaultPolicyAsync()
        => _fallback.GetDefaultPolicyAsync();
    public Task<AuthorizationPolicy?> GetFallbackPolicyAsync()
        => _fallback.GetFallbackPolicyAsync();
}
```

**RequirePermissionAttribute.cs**:
```csharp
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public class RequirePermissionAttribute : AuthorizeAttribute
{
    public RequirePermissionAttribute(Permission permission)
        : base(permission.ToString()) { }
}
```

### 2.4 Registration (Program.cs)

In `Courier.Api/Program.cs`, replace the current `builder.Services.AddAuthorization()` with:

```csharp
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddSingleton<IAuthorizationHandler, PermissionHandler>();
builder.Services.AddAuthorization();
```

The Worker (`Courier.Worker/Program.cs`) does NOT need these registrations — it has no HTTP pipeline or authorization middleware. Only register in the API host.

### 2.4.1 Custom 403 Response Handler

To return 403 responses in the standard `ApiResponse<T>` envelope format (consistent with all other error responses), add a custom `IAuthorizationMiddlewareResultHandler`:

```csharp
// Features/Security/ApiResponseAuthorizationHandler.cs
public class ApiResponseAuthorizationHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly IAuthorizationMiddlewareResultHandler _default = new AuthorizationMiddlewareResultHandler();

    public async Task HandleAsync(RequestDelegate next, HttpContext context,
        AuthorizationPolicy policy, PolicyAuthorizationResult authorizeResult)
    {
        if (authorizeResult.Forbidden)
        {
            context.Response.StatusCode = StatusCodes.Status403Forbidden;
            context.Response.ContentType = "application/json";
            var response = new ApiResponse
            {
                Error = new ApiError(
                    ErrorCodes.Forbidden,
                    "Forbidden",
                    "You do not have permission to perform this action."),
            };
            await context.Response.WriteAsJsonAsync(response);
            return;
        }
        await _default.HandleAsync(next, context, policy, authorizeResult);
    }
}
```

Register in Program.cs:
```csharp
builder.Services.AddSingleton<IAuthorizationMiddlewareResultHandler, ApiResponseAuthorizationHandler>();
```

Uses existing `ErrorCodes.Forbidden = 10008` from the auth/authz error code range.

### 2.5 Controller Migration

Every `[Authorize(Roles = "...")]` attribute is replaced with `[RequirePermission(Permission.Xyz)]`. Class-level `[Authorize]` is retained for authentication gating.

**Complete endpoint → permission mapping:**

#### SetupController
No changes. Stays `[AllowAnonymous]`.

#### AuthController
No changes. Note: AuthController has NO class-level `[Authorize]` — it uses `[EnableRateLimiting("auth")]` at class level with `[AllowAnonymous]` on Login/Refresh and method-level `[Authorize]` on Logout/Me/ChangePassword. This stays as-is.

#### UsersController
```
Class-level: [Authorize]
List                → [RequirePermission(Permission.UsersView)]
GetById             → [RequirePermission(Permission.UsersView)]
Create              → [RequirePermission(Permission.UsersManage)]
Update              → [RequirePermission(Permission.UsersManage)]
Delete              → [RequirePermission(Permission.UsersManage)]
ResetPassword       → [RequirePermission(Permission.UsersManage)]
```
Note: Currently `UsersView` is admin-only (only admin has it). Splitting View from Manage
allows future flexibility if Operators should see a user list without managing them.

#### JobsController
```
Class-level: [Authorize]
List                → [RequirePermission(Permission.JobsView)]
GetById             → [RequirePermission(Permission.JobsView)]
ListSteps           → [RequirePermission(Permission.JobsView)]
ListExecutions      → [RequirePermission(Permission.JobsView)]
GetExecution        → [RequirePermission(Permission.JobsView)]
ListSchedules       → [RequirePermission(Permission.JobsView)]
GetVersions         → [RequirePermission(Permission.JobsView)]
GetVersion          → [RequirePermission(Permission.JobsView)]
ListDependencies    → [RequirePermission(Permission.JobsView)]
Create              → [RequirePermission(Permission.JobsCreate)]
Update              → [RequirePermission(Permission.JobsEdit)]
Delete              → [RequirePermission(Permission.JobsDelete)]
ReplaceSteps        → [RequirePermission(Permission.JobsEdit)]
AddStep             → [RequirePermission(Permission.JobsEdit)]
Trigger             → [RequirePermission(Permission.JobsExecute)]
PauseExecution      → [RequirePermission(Permission.JobsExecute)]
ResumeExecution     → [RequirePermission(Permission.JobsExecute)]
CancelExecution     → [RequirePermission(Permission.JobsExecute)]
CreateSchedule      → [RequirePermission(Permission.JobsManageSchedules)]
UpdateSchedule      → [RequirePermission(Permission.JobsManageSchedules)]
DeleteSchedule      → [RequirePermission(Permission.JobsManageSchedules)]
AddDependency       → [RequirePermission(Permission.JobsManageDependencies)]
RemoveDependency    → [RequirePermission(Permission.JobsManageDependencies)]
```

#### ChainsController
```
Class-level: [Authorize]
List                → [RequirePermission(Permission.ChainsView)]
GetById             → [RequirePermission(Permission.ChainsView)]
ListExecutions      → [RequirePermission(Permission.ChainsView)]
GetExecution        → [RequirePermission(Permission.ChainsView)]
ListSchedules       → [RequirePermission(Permission.ChainsView)]
Create              → [RequirePermission(Permission.ChainsCreate)]
Update              → [RequirePermission(Permission.ChainsEdit)]
Delete              → [RequirePermission(Permission.ChainsDelete)]
ReplaceMembers      → [RequirePermission(Permission.ChainsEdit)]
Execute             → [RequirePermission(Permission.ChainsExecute)]
CreateSchedule      → [RequirePermission(Permission.ChainsManageSchedules)]
UpdateSchedule      → [RequirePermission(Permission.ChainsManageSchedules)]
DeleteSchedule      → [RequirePermission(Permission.ChainsManageSchedules)]
```

#### ConnectionsController
```
Class-level: [Authorize]
List                → [RequirePermission(Permission.ConnectionsView)]
GetById             → [RequirePermission(Permission.ConnectionsView)]
Create              → [RequirePermission(Permission.ConnectionsCreate)]
Update              → [RequirePermission(Permission.ConnectionsEdit)]
Delete              → [RequirePermission(Permission.ConnectionsDelete)]
TestConnection      → [RequirePermission(Permission.ConnectionsTest)]
```

#### KnownHostsController
```
Class-level: [Authorize]
ListByConnection    → [RequirePermission(Permission.KnownHostsView)]
GetById             → [RequirePermission(Permission.KnownHostsView)]
Create              → [RequirePermission(Permission.KnownHostsManage)]
Delete              → [RequirePermission(Permission.KnownHostsManage)]
Approve             → [RequirePermission(Permission.KnownHostsManage)]
```

#### PgpKeysController
```
Class-level: [Authorize]
List                → [RequirePermission(Permission.PgpKeysView)]
GetById             → [RequirePermission(Permission.PgpKeysView)]
ExportPublicKey     → [RequirePermission(Permission.PgpKeysExportPublic)]
Generate            → [RequirePermission(Permission.PgpKeysManage)]
Import              → [RequirePermission(Permission.PgpKeysManage)]
Update              → [RequirePermission(Permission.PgpKeysManage)]
Delete              → [RequirePermission(Permission.PgpKeysManage)]
Retire              → [RequirePermission(Permission.PgpKeysManage)]
Revoke              → [RequirePermission(Permission.PgpKeysManage)]
Activate            → [RequirePermission(Permission.PgpKeysManage)]
SetSuccessor        → [RequirePermission(Permission.PgpKeysManage)]
CreateShareLink     → [RequirePermission(Permission.PgpKeysManageSharing)]
ListShareLinks      → [RequirePermission(Permission.PgpKeysManageSharing)]
RevokeShareLink     → [RequirePermission(Permission.PgpKeysManageSharing)]
GetSharedKey        → [AllowAnonymous] (no change)
```

#### SshKeysController
```
Class-level: [Authorize]
List                → [RequirePermission(Permission.SshKeysView)]
GetById             → [RequirePermission(Permission.SshKeysView)]
ExportPublicKey     → [RequirePermission(Permission.SshKeysExportPublic)]
Generate            → [RequirePermission(Permission.SshKeysManage)]
Import              → [RequirePermission(Permission.SshKeysManage)]
Update              → [RequirePermission(Permission.SshKeysManage)]
Delete              → [RequirePermission(Permission.SshKeysManage)]
Retire              → [RequirePermission(Permission.SshKeysManage)]
Activate            → [RequirePermission(Permission.SshKeysManage)]
CreateShareLink     → [RequirePermission(Permission.SshKeysManageSharing)]
ListShareLinks      → [RequirePermission(Permission.SshKeysManageSharing)]
RevokeShareLink     → [RequirePermission(Permission.SshKeysManageSharing)]
GetSharedKey        → [AllowAnonymous] (no change)
```
Note: SshKeysController does NOT have a Revoke endpoint (only Retire and Activate).

#### MonitorsController
```
Class-level: [Authorize]
List                → [RequirePermission(Permission.MonitorsView)]
GetById             → [RequirePermission(Permission.MonitorsView)]
ListFileLog         → [RequirePermission(Permission.MonitorsView)]
Create              → [RequirePermission(Permission.MonitorsCreate)]
Update              → [RequirePermission(Permission.MonitorsEdit)]
Delete              → [RequirePermission(Permission.MonitorsDelete)]
Activate            → [RequirePermission(Permission.MonitorsChangeState)]
Pause               → [RequirePermission(Permission.MonitorsChangeState)]
Disable             → [RequirePermission(Permission.MonitorsChangeState)]
AcknowledgeError    → [RequirePermission(Permission.MonitorsChangeState)]
```

#### TagsController
```
Class-level: [Authorize]
List                → [RequirePermission(Permission.TagsView)]
GetById             → [RequirePermission(Permission.TagsView)]
ListEntities        → [RequirePermission(Permission.TagsView)]
Create              → [RequirePermission(Permission.TagsManage)]
Update              → [RequirePermission(Permission.TagsManage)]
Delete              → [RequirePermission(Permission.TagsManage)]
Assign              → [RequirePermission(Permission.TagsManage)]
Unassign            → [RequirePermission(Permission.TagsManage)]
```

#### NotificationRulesController
```
Class-level: [Authorize]
List                → [RequirePermission(Permission.NotificationRulesView)]
GetById             → [RequirePermission(Permission.NotificationRulesView)]
Create              → [RequirePermission(Permission.NotificationRulesManage)]
Update              → [RequirePermission(Permission.NotificationRulesManage)]
Delete              → [RequirePermission(Permission.NotificationRulesManage)]
Test                → [RequirePermission(Permission.NotificationRulesManage)]
```

#### SettingsController
```
Class-level: [Authorize]
GetAuthSettings     → no method-level attribute (all authenticated users can read)
UpdateAuthSettings  → [RequirePermission(Permission.SettingsManage)]
```

#### Read-Only Controllers (all authenticated users, with View permissions)

These controllers get `[RequirePermission]` on their endpoints for consistency and future-proofing.
Since all three roles have these View permissions, the effective behavior is unchanged.

- **DashboardController** — all endpoints get `[RequirePermission(Permission.DashboardView)]`
- **AuditLogController** — all endpoints get `[RequirePermission(Permission.AuditLogView)]`
- **NotificationLogsController** — all endpoints get `[RequirePermission(Permission.NotificationLogsView)]`
- **FilesystemController** — Browse gets `[RequirePermission(Permission.FilesystemBrowse)]`

#### Unchanged Controllers (no permission attributes added)
These controllers stay with just `[Authorize]` because they don't map to a specific resource in the permission matrix:
- StepTypesController — metadata endpoint, all authenticated users
- FeedbackController — community feature, all authenticated users
- GitHubAuthController — OAuth flow, all authenticated users
- AzureFunctionsController — traces, all authenticated users

### 2.6 Frontend Role-Gating

#### Permission Types and Map

Types and role map live in a separate `permissions.ts` file at **module scope** (not inside a
component or hook) so the Sets are created once at import time, not on every render.

```typescript
// src/lib/permissions.ts — module-scope, imported by the hook

export type Permission =
  | "JobsView" | "JobsCreate" | "JobsEdit" | "JobsDelete"
  | "JobsExecute" | "JobsManageSchedules" | "JobsManageDependencies"
  | "ChainsView" | "ChainsCreate" | "ChainsEdit" | "ChainsDelete"
  | "ChainsExecute" | "ChainsManageSchedules"
  | "ConnectionsView" | "ConnectionsCreate" | "ConnectionsEdit"
  | "ConnectionsDelete" | "ConnectionsTest"
  | "PgpKeysView" | "PgpKeysManage" | "PgpKeysExportPublic" | "PgpKeysManageSharing"
  | "SshKeysView" | "SshKeysManage" | "SshKeysExportPublic" | "SshKeysManageSharing"
  | "MonitorsView" | "MonitorsCreate" | "MonitorsEdit"
  | "MonitorsDelete" | "MonitorsChangeState"
  | "TagsView" | "TagsManage"
  | "NotificationRulesView" | "NotificationRulesManage" | "NotificationLogsView"
  | "AuditLogView"
  | "UsersView" | "UsersManage"
  | "SettingsView" | "SettingsManage"
  | "DashboardView"
  | "FilesystemBrowse"
  | "KnownHostsView" | "KnownHostsManage";

// Module-scope: Sets created once at import, not per render
export const rolePermissions: Record<string, ReadonlySet<Permission>> = {
  admin: new Set([/* all Permission values */]),
  operator: new Set([/* mirrors OperatorPermissions from RolePermissions.cs */]),
  viewer: new Set([/* mirrors ViewerPermissions from RolePermissions.cs */]),
};
```

#### usePermissions Hook

```typescript
// src/lib/hooks/use-permissions.ts
import { useAuth } from "@/lib/hooks/use-auth";
import { type Permission, rolePermissions } from "@/lib/permissions";

export function usePermissions() {
  const { user } = useAuth();
  const permissions = rolePermissions[user?.role ?? ""] ?? new Set<Permission>();

  return {
    can: (permission: Permission): boolean => permissions.has(permission),
    canAny: (...perms: Permission[]): boolean => perms.some(p => permissions.has(p)),
    role: user?.role ?? null,
  };
}
```

#### Pages Requiring Changes

| Page | Elements to conditionally render |
|------|----------------------------------|
| `/jobs` (list) | Create button |
| `/jobs/[id]` (detail) | Edit, Delete, Trigger buttons |
| `/jobs/[id]/edit` | Redirect if no `JobsEdit` permission |
| `/chains` (list) | Create button |
| `/chains/[id]` (detail) | Edit, Delete, Execute buttons |
| `/connections` (list) | Create button |
| `/connections/[id]` (detail) | Edit, Delete, Test buttons |
| `/keys/pgp` (list) | Generate, Import buttons |
| `/keys/pgp/[id]` (detail) | Edit, Delete, lifecycle, share buttons |
| `/keys/ssh` (list) | Generate, Import buttons |
| `/keys/ssh/[id]` (detail) | Edit, Delete, lifecycle, share buttons |
| `/monitors` (list) | Create button |
| `/monitors/[id]` (detail) | Edit, Delete, state change buttons |
| `/tags` | Create, Edit, Delete, Assign buttons |
| `/notification-rules` (list) | Create button |
| `/notification-rules/[id]` (detail) | Edit, Delete, Test buttons |
| `/settings` | Auth settings update controls |
| `/settings/users` | Already gated (no change) |

#### Guarded Route Pattern

For edit/create pages that shouldn't be accessible at all:

```tsx
// src/app/(app)/jobs/new/page.tsx
const { can } = usePermissions();
if (!can("JobsCreate")) {
  redirect("/jobs");
}
```

### 2.7 Integration Test Suite

#### Test Infrastructure

**Location**: `tests/Courier.Tests.Integration/Rbac/`

**RbacFixture.cs** — shared fixture for all RBAC tests:

The existing `CourierApiFactory` uses a `TestAuthHandler` that always authenticates as admin.
For RBAC tests, we need role-differentiated clients. The approach:

1. **Extend `CourierApiFactory`** to accept a role claim via a custom request header
2. **Modify `TestAuthHandler`** to read the role from a `X-Test-Role` header (defaulting to "admin" for backward compatibility with existing tests)
3. **Create per-role `HttpClient` instances** that set the header via a `DelegatingHandler`

```csharp
// Modified TestAuthHandler (in CourierApiFactory.cs)
protected override Task<AuthenticateResult> HandleAuthenticateAsync()
{
    // Read role from custom header, default to "admin" for backward compat
    var role = Context.Request.Headers["X-Test-Role"].FirstOrDefault() ?? "admin";
    var userId = role switch
    {
        "admin"    => Fixture.AdminUserId,
        "operator" => Fixture.OperatorUserId,
        "viewer"   => Fixture.ViewerUserId,
        _          => Fixture.AdminUserId,
    };

    var claims = new[]
    {
        new Claim(ClaimTypes.NameIdentifier, userId.ToString()),
        new Claim(ClaimTypes.Role, role),
        new Claim("name", $"Test {role}"),
    };
    var identity = new ClaimsIdentity(claims, "Test");
    var principal = new ClaimsPrincipal(identity);
    var ticket = new AuthenticationTicket(principal, "Test");
    return Task.FromResult(AuthenticateResult.Success(ticket));
}

// RoleHeaderHandler — DelegatingHandler that adds the role header
public class RoleHeaderHandler(string role) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken ct)
    {
        request.Headers.Add("X-Test-Role", role);
        return base.SendAsync(request, ct);
    }
}

// RbacFixture
public class RbacFixture : IAsyncLifetime
{
    private CourierApiFactory _factory = null!;

    public HttpClient AdminClient { get; private set; } = null!;
    public HttpClient OperatorClient { get; private set; } = null!;
    public HttpClient ViewerClient { get; private set; } = null!;
    public HttpClient AnonymousClient { get; private set; } = null!;

    // Seeded test data IDs
    public Guid TestJobId { get; private set; }
    public Guid TestConnectionId { get; private set; }
    public Guid TestPgpKeyId { get; private set; }
    public Guid TestSshKeyId { get; private set; }
    public Guid TestMonitorId { get; private set; }
    public Guid TestTagId { get; private set; }
    public Guid TestChainId { get; private set; }
    public Guid TestNotificationRuleId { get; private set; }

    public async Task InitializeAsync()
    {
        _factory = new CourierApiFactory();
        await _factory.InitializeAsync();

        // Create role-differentiated clients
        AdminClient = _factory.CreateDefaultClient(new RoleHeaderHandler("admin"));
        OperatorClient = _factory.CreateDefaultClient(new RoleHeaderHandler("operator"));
        ViewerClient = _factory.CreateDefaultClient(new RoleHeaderHandler("viewer"));
        AnonymousClient = _factory.CreateClient(); // No auth header at all

        // Seed test data using admin client
        // (create job, connection, key, monitor, tag, chain, notification rule)
    }

    public async Task DisposeAsync()
    {
        // Cleanup seeded test data via admin client
        _factory?.Dispose();
    }
}

[CollectionDefinition("Rbac")]
public class RbacCollection : ICollectionFixture<RbacFixture> { }
```

The `AnonymousClient` is created without the `TestAuthHandler` to verify that unauthenticated
requests return 401. This requires a `CreateClient()` override that skips auth setup.

**RbacTestBase.cs** — base class for RBAC test classes:
```csharp
[Collection("Rbac")]
public abstract class RbacTestBase
{
    protected readonly RbacFixture Fixture;

    protected RbacTestBase(RbacFixture fixture) => Fixture = fixture;

    protected HttpClient ClientForRole(string role) => role switch
    {
        "admin"     => Fixture.AdminClient,
        "operator"  => Fixture.OperatorClient,
        "viewer"    => Fixture.ViewerClient,
        "anonymous" => Fixture.AnonymousClient,
        _ => throw new ArgumentException($"Unknown role: {role}"),
    };
}
```

#### Test Pattern

Each controller gets a test class with parameterized tests:

```csharp
public class JobsRbacTests : RbacTestBase
{
    public JobsRbacTests(RbacFixture fixture) : base(fixture) { }

    [Theory]
    [InlineData("admin",    HttpStatusCode.OK)]
    [InlineData("operator", HttpStatusCode.OK)]
    [InlineData("viewer",   HttpStatusCode.OK)]
    public async Task ListJobs_RespectsPermissions(string role, HttpStatusCode expected)
    {
        var client = ClientForRole(role);
        var response = await client.GetAsync("api/v1/jobs");
        response.StatusCode.ShouldBe(expected);
    }

    [Theory]
    [InlineData("admin",    HttpStatusCode.Created)]
    [InlineData("operator", HttpStatusCode.Created)]
    [InlineData("viewer",   HttpStatusCode.Forbidden)]
    public async Task CreateJob_RespectsPermissions(string role, HttpStatusCode expected)
    {
        var client = ClientForRole(role);
        var response = await client.PostAsJsonAsync("api/v1/jobs", new { name = $"rbac-test-{role}", ... });
        response.StatusCode.ShouldBe(expected);
    }

    // ... similar for Update, Delete, Trigger, Pause, Resume, Cancel,
    //     schedules, dependencies, steps
}
```

#### Test Coverage

| Test Class | Controller | Approximate Test Count |
|-----------|------------|----------------------|
| JobsRbacTests | JobsController | ~72 (24 endpoints x 3 roles) |
| ChainsRbacTests | ChainsController | ~39 |
| ConnectionsRbacTests | ConnectionsController | ~18 |
| KnownHostsRbacTests | KnownHostsController | ~15 |
| PgpKeysRbacTests | PgpKeysController | ~45 |
| SshKeysRbacTests | SshKeysController | ~39 |
| MonitorsRbacTests | MonitorsController | ~30 |
| TagsRbacTests | TagsController | ~24 |
| NotificationRulesRbacTests | NotificationRulesController | ~18 |
| SettingsRbacTests | SettingsController | ~6 |
| UsersRbacTests | UsersController | ~18 |
| DashboardRbacTests | DashboardController | ~12 |
| AuditLogRbacTests | AuditLogController | ~6 |
| AuthRbacTests | AuthController | ~10 |
| **Total** | | **~356** |

#### Performance

- All RBAC tests share a single `RbacFixture` (one Testcontainers Postgres instance)
- Tests only assert HTTP status codes — no deep response body validation
- Estimated runtime: 60-90 seconds for all ~356 tests
- Tests seed data once in `InitializeAsync`, reuse across all test classes

### 2.8 Unit Tests

In addition to integration tests, add unit tests for the authorization infrastructure:

**PermissionHandlerTests.cs** — verify the handler succeeds/fails correctly:
```csharp
[Fact]
public async Task AdminHasAllPermissions()
{
    foreach (var permission in Enum.GetValues<Permission>())
    {
        var result = await EvaluateAsync("admin", permission);
        result.Succeeded.ShouldBeTrue($"Admin should have {permission}");
    }
}

[Theory]
[InlineData("viewer", Permission.JobsCreate, false)]
[InlineData("viewer", Permission.JobsView, true)]
[InlineData("operator", Permission.ConnectionsCreate, false)]
[InlineData("operator", Permission.JobsCreate, true)]
public async Task RolePermissionCheck(string role, Permission perm, bool expected)
{
    var result = await EvaluateAsync(role, perm);
    result.Succeeded.ShouldBe(expected);
}
```

**RolePermissionsTests.cs** — verify the mapping is correct:
```csharp
[Fact]
public void AdminHasEveryPermission()
{
    var all = Enum.GetValues<Permission>();
    var admin = RolePermissions.GetPermissions("admin");
    admin.ShouldBe(all.ToHashSet());
}

[Fact]
public void ViewerHasNoMutatingPermissions()
{
    var viewer = RolePermissions.GetPermissions("viewer");
    viewer.ShouldNotContain(Permission.JobsCreate);
    viewer.ShouldNotContain(Permission.ConnectionsCreate);
    // ... etc
}

[Fact]
public void UnknownRoleHasNoPermissions()
{
    RolePermissions.GetPermissions("hacker").ShouldBeEmpty();
}
```

## 3. Files Changed

### New Files
| File | Layer | Purpose |
|------|-------|---------|
| `src/Courier.Domain/Enums/Permission.cs` | Domain | Permission enum |
| `src/Courier.Domain/Authorization/RolePermissions.cs` | Domain | Role → permission mapping |
| `src/Courier.Features/Security/PermissionRequirement.cs` | Features | Authorization requirement |
| `src/Courier.Features/Security/PermissionHandler.cs` | Features | Authorization handler |
| `src/Courier.Features/Security/PermissionPolicyProvider.cs` | Features | Dynamic policy provider |
| `src/Courier.Features/Security/RequirePermissionAttribute.cs` | Features | Convenience attribute |
| `src/Courier.Features/Security/ApiResponseAuthorizationHandler.cs` | Features | Custom 403 → ApiResponse envelope |
| `src/Courier.Frontend/src/lib/permissions.ts` | Frontend | Permission types and role→permission map (module-scope) |
| `src/Courier.Frontend/src/lib/hooks/use-permissions.ts` | Frontend | Hook that imports from permissions.ts |
| `tests/Courier.Tests.Unit/Security/PermissionHandlerTests.cs` | Tests | Handler unit tests |
| `tests/Courier.Tests.Unit/Security/RolePermissionsTests.cs` | Tests | Mapping unit tests |
| `tests/Courier.Tests.Integration/Rbac/RbacFixture.cs` | Tests | Shared test fixture |
| `tests/Courier.Tests.Integration/Rbac/RbacTestBase.cs` | Tests | Base class for RBAC tests |
| `tests/Courier.Tests.Integration/Rbac/JobsRbacTests.cs` | Tests | Jobs RBAC tests |
| `tests/Courier.Tests.Integration/Rbac/ChainsRbacTests.cs` | Tests | Chains RBAC tests |
| `tests/Courier.Tests.Integration/Rbac/ConnectionsRbacTests.cs` | Tests | Connections RBAC tests |
| `tests/Courier.Tests.Integration/Rbac/KnownHostsRbacTests.cs` | Tests | Known hosts RBAC tests |
| `tests/Courier.Tests.Integration/Rbac/PgpKeysRbacTests.cs` | Tests | PGP keys RBAC tests |
| `tests/Courier.Tests.Integration/Rbac/SshKeysRbacTests.cs` | Tests | SSH keys RBAC tests |
| `tests/Courier.Tests.Integration/Rbac/MonitorsRbacTests.cs` | Tests | Monitors RBAC tests |
| `tests/Courier.Tests.Integration/Rbac/TagsRbacTests.cs` | Tests | Tags RBAC tests |
| `tests/Courier.Tests.Integration/Rbac/NotificationRulesRbacTests.cs` | Tests | Notification rules RBAC tests |
| `tests/Courier.Tests.Integration/Rbac/SettingsRbacTests.cs` | Tests | Settings RBAC tests |
| `tests/Courier.Tests.Integration/Rbac/UsersRbacTests.cs` | Tests | Users RBAC tests |
| `tests/Courier.Tests.Integration/Rbac/DashboardRbacTests.cs` | Tests | Dashboard RBAC tests |
| `tests/Courier.Tests.Integration/Rbac/AuditLogRbacTests.cs` | Tests | Audit log RBAC tests |
| `tests/Courier.Tests.Integration/Rbac/AuthRbacTests.cs` | Tests | Auth RBAC tests |

### Modified Files
| File | Change |
|------|--------|
| `src/Courier.Api/Program.cs` | Register policy provider, handler, 403 result handler |
| `tests/Courier.Tests.Integration/CourierApiFactory.cs` | Extend TestAuthHandler to accept role via header |
| `src/Courier.Features/Jobs/JobsController.cs` | Replace `[Authorize(Roles)]` with `[RequirePermission]` |
| `src/Courier.Features/Chains/ChainsController.cs` | Same |
| `src/Courier.Features/Connections/ConnectionsController.cs` | Same |
| `src/Courier.Features/Connections/KnownHostsController.cs` | Same |
| `src/Courier.Features/PgpKeys/PgpKeysController.cs` | Same |
| `src/Courier.Features/SshKeys/SshKeysController.cs` | Same |
| `src/Courier.Features/Monitors/MonitorsController.cs` | Same |
| `src/Courier.Features/Tags/TagsController.cs` | Same |
| `src/Courier.Features/Notifications/NotificationRulesController.cs` | Same |
| `src/Courier.Features/Settings/SettingsController.cs` | Same |
| `src/Courier.Features/Users/UsersController.cs` | Same |
| `src/Courier.Features/Dashboard/DashboardController.cs` | Add `[RequirePermission(Permission.DashboardView)]` |
| `src/Courier.Features/AuditLog/AuditLogController.cs` | Add `[RequirePermission(Permission.AuditLogView)]` |
| `src/Courier.Features/Notifications/NotificationLogsController.cs` | Add `[RequirePermission(Permission.NotificationLogsView)]` |
| `src/Courier.Features/Filesystem/FilesystemController.cs` | Add `[RequirePermission(Permission.FilesystemBrowse)]` |
| ~10 frontend pages | Add `usePermissions()` conditional rendering |

## 4. What Doesn't Change

- JWT token structure (role claim already embedded correctly)
- Auth endpoints (login/refresh stay `[AllowAnonymous]`)
- Setup endpoints (stay `[AllowAnonymous]`)
- Service layer (no authorization checks — enforcement stays at controller level)
- Database schema (no migration needed)
- User entity (role field unchanged)
- Existing unit tests (controller tests may need minor attribute updates in mocks)

## 5. Risks and Mitigations

| Risk | Mitigation |
|------|-----------|
| Attribute migration introduces a typo/gap | Integration test suite catches every endpoint |
| Policy provider introduces performance overhead | `ConcurrentDictionary` cache ensures policies are built once |
| Frontend permission map drifts from backend | Unit test validates both define the same permission set (optional) |
| Existing tests break due to attribute changes | Existing tests use admin tokens; `[RequirePermission]` still authorizes admin |
| `DefaultAuthorizationPolicyProvider` fallback breaks | Preserved in `PermissionPolicyProvider` for `[Authorize]` and `[AllowAnonymous]` |

## 6. Success Criteria

1. All existing unit tests pass (no regressions)
2. All existing integration tests pass
3. All existing E2E tests pass
4. ~356 new RBAC integration tests pass
5. New unit tests for `PermissionHandler` and `RolePermissions` pass
6. Frontend hides unauthorized actions for Viewer and Operator roles
7. No `[Authorize(Roles = "...")]` remains in the codebase (fully migrated)
