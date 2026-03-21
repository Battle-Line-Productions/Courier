# RBAC Policy-Based Authorization Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace string-based `[Authorize(Roles = "...")]` with compile-safe `[RequirePermission(Permission.Xyz)]` policy-based authorization, add frontend role-gating, and build a comprehensive RBAC integration test suite.

**Architecture:** Permission enum + static role→permission mapping in Domain. Dynamic `IAuthorizationPolicyProvider` resolves `Permission` enum names to policies. `PermissionHandler` checks JWT role claims against the mapping. Custom 403 handler returns `ApiResponse` envelope. Integration tests use a modified `TestAuthHandler` that reads role from `X-Test-Role` header.

**Tech Stack:** ASP.NET Core Authorization, `FrozenSet<T>`, xUnit + Shouldly + Testcontainers, React hooks + TypeScript

**Spec:** `docs/superpowers/specs/2026-03-21-rbac-policy-authorization-design.md`

---

## File Structure

### New Files (Domain)
| File | Responsibility |
|------|---------------|
| `src/Courier.Domain/Enums/Permission.cs` | Permission enum (~50 values) |
| `src/Courier.Domain/Authorization/RolePermissions.cs` | Static role→permission FrozenSet mapping |

### New Files (Features)
| File | Responsibility |
|------|---------------|
| `src/Courier.Features/Security/PermissionRequirement.cs` | `IAuthorizationRequirement` wrapping a `Permission` |
| `src/Courier.Features/Security/PermissionHandler.cs` | `AuthorizationHandler` checking role claims |
| `src/Courier.Features/Security/PermissionPolicyProvider.cs` | Dynamic `IAuthorizationPolicyProvider` with cache |
| `src/Courier.Features/Security/RequirePermissionAttribute.cs` | `[RequirePermission(Permission.X)]` convenience attribute |
| `src/Courier.Features/Security/ApiResponseAuthorizationHandler.cs` | Custom 403 → `ApiResponse` envelope |

### New Files (Frontend)
| File | Responsibility |
|------|---------------|
| `src/Courier.Frontend/src/lib/permissions.ts` | Permission type + role→permission map (module-scope) |
| `src/Courier.Frontend/src/lib/hooks/use-permissions.ts` | `usePermissions()` hook |

### New Files (Tests)
| File | Responsibility |
|------|---------------|
| `tests/Courier.Tests.Unit/Security/RolePermissionsTests.cs` | Verify role→permission mapping |
| `tests/Courier.Tests.Unit/Security/PermissionHandlerTests.cs` | Verify handler succeed/fail logic |
| `tests/Courier.Tests.Integration/Rbac/RoleHeaderHandler.cs` | DelegatingHandler adding X-Test-Role header |
| `tests/Courier.Tests.Integration/Rbac/RbacFixture.cs` | Shared fixture: 3 role clients + seeded data |
| `tests/Courier.Tests.Integration/Rbac/RbacTestBase.cs` | Base class with `ClientForRole()` |
| `tests/Courier.Tests.Integration/Rbac/JobsRbacTests.cs` | Jobs endpoint RBAC tests |
| `tests/Courier.Tests.Integration/Rbac/ChainsRbacTests.cs` | Chains endpoint RBAC tests |
| `tests/Courier.Tests.Integration/Rbac/ConnectionsRbacTests.cs` | Connections endpoint RBAC tests |
| `tests/Courier.Tests.Integration/Rbac/KnownHostsRbacTests.cs` | Known hosts endpoint RBAC tests |
| `tests/Courier.Tests.Integration/Rbac/PgpKeysRbacTests.cs` | PGP keys endpoint RBAC tests |
| `tests/Courier.Tests.Integration/Rbac/SshKeysRbacTests.cs` | SSH keys endpoint RBAC tests |
| `tests/Courier.Tests.Integration/Rbac/MonitorsRbacTests.cs` | Monitors endpoint RBAC tests |
| `tests/Courier.Tests.Integration/Rbac/TagsRbacTests.cs` | Tags endpoint RBAC tests |
| `tests/Courier.Tests.Integration/Rbac/NotificationRulesRbacTests.cs` | Notification rules RBAC tests |
| `tests/Courier.Tests.Integration/Rbac/SettingsRbacTests.cs` | Settings endpoint RBAC tests |
| `tests/Courier.Tests.Integration/Rbac/UsersRbacTests.cs` | Users endpoint RBAC tests |
| `tests/Courier.Tests.Integration/Rbac/AuthRbacTests.cs` | Auth endpoint RBAC tests (anonymous + authenticated) |
| `tests/Courier.Tests.Integration/Rbac/ReadOnlyRbacTests.cs` | Dashboard, audit, filesystem, notifications RBAC tests |

### Modified Files
| File | Change |
|------|--------|
| `src/Courier.Api/Program.cs` | Register policy provider, handler, 403 handler |
| `tests/Courier.Tests.Integration/CourierApiFactory.cs` | Parameterize TestAuthHandler role via header |
| 16 controller files in `src/Courier.Features/` | Replace `[Authorize(Roles)]` → `[RequirePermission]` |
| ~10 frontend page files | Add `usePermissions()` conditional rendering |

---

## Chunk 1: Domain Layer — Permission Model

### Task 1: Permission Enum

**Files:**
- Create: `src/Courier.Domain/Enums/Permission.cs`

- [ ] **Step 1: Create the Permission enum**

```csharp
namespace Courier.Domain.Enums;

/// <summary>
/// Defines every authorized action in the system. Each value maps to a specific
/// resource + action from the RBAC permission matrix (Design Doc Section 12.2).
/// Used by RequirePermissionAttribute on controllers and by the frontend usePermissions hook.
/// </summary>
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

- [ ] **Step 2: Verify it builds**

Run: `dotnet build src/Courier.Domain/Courier.Domain.csproj`
Expected: Build succeeded.

---

### Task 2: RolePermissions Static Mapping

**Files:**
- Create: `src/Courier.Domain/Authorization/RolePermissions.cs`

- [ ] **Step 1: Create the Authorization directory and RolePermissions class**

```csharp
using System.Collections.Frozen;
using Courier.Domain.Enums;

namespace Courier.Domain.Authorization;

/// <summary>
/// Single source of truth for which roles have which permissions.
/// Maps directly to the permission matrix in Design Doc Section 12.2.
/// This class is intentionally in the Domain layer (BCL-only, no external deps).
/// </summary>
public static class RolePermissions
{
    private static readonly FrozenSet<Permission> AdminPermissions =
        Enum.GetValues<Permission>().ToFrozenSet();

    private static readonly FrozenSet<Permission> OperatorPermissions = new HashSet<Permission>
    {
        // Jobs: full operational access
        Permission.JobsView,
        Permission.JobsCreate,
        Permission.JobsEdit,
        Permission.JobsDelete,
        Permission.JobsExecute,
        Permission.JobsManageSchedules,
        Permission.JobsManageDependencies,

        // Chains: full operational access
        Permission.ChainsView,
        Permission.ChainsCreate,
        Permission.ChainsEdit,
        Permission.ChainsDelete,
        Permission.ChainsExecute,
        Permission.ChainsManageSchedules,

        // Connections: view + test only (no create/edit/delete)
        Permission.ConnectionsView,
        Permission.ConnectionsTest,

        // Keys: view + export public only (no manage/sharing)
        Permission.PgpKeysView,
        Permission.PgpKeysExportPublic,
        Permission.SshKeysView,
        Permission.SshKeysExportPublic,

        // Monitors: full operational access
        Permission.MonitorsView,
        Permission.MonitorsCreate,
        Permission.MonitorsEdit,
        Permission.MonitorsDelete,
        Permission.MonitorsChangeState,

        // Tags: full access
        Permission.TagsView,
        Permission.TagsManage,

        // Notifications: full access
        Permission.NotificationRulesView,
        Permission.NotificationRulesManage,
        Permission.NotificationLogsView,

        // Read-only shared resources
        Permission.AuditLogView,
        Permission.DashboardView,
        Permission.SettingsView,
        Permission.FilesystemBrowse,
        Permission.KnownHostsView,
    }.ToFrozenSet();

    private static readonly FrozenSet<Permission> ViewerPermissions = new HashSet<Permission>
    {
        // View-only across all resources
        Permission.JobsView,
        Permission.ChainsView,
        Permission.ConnectionsView,
        Permission.PgpKeysView,
        Permission.PgpKeysExportPublic,
        Permission.SshKeysView,
        Permission.SshKeysExportPublic,
        Permission.MonitorsView,
        Permission.TagsView,
        Permission.NotificationRulesView,
        Permission.NotificationLogsView,
        Permission.AuditLogView,
        Permission.DashboardView,
        Permission.SettingsView,
        Permission.FilesystemBrowse,
        Permission.KnownHostsView,
    }.ToFrozenSet();

    /// <summary>
    /// Returns the set of permissions for a given role name.
    /// Returns empty set for unknown roles (fail-closed).
    /// </summary>
    public static IReadOnlySet<Permission> GetPermissions(string role) => role.ToLowerInvariant() switch
    {
        "admin" => AdminPermissions,
        "operator" => OperatorPermissions,
        "viewer" => ViewerPermissions,
        _ => FrozenSet<Permission>.Empty,
    };

    /// <summary>
    /// Checks if a role has a specific permission.
    /// </summary>
    public static bool HasPermission(string role, Permission permission)
        => GetPermissions(role).Contains(permission);
}
```

- [ ] **Step 2: Verify it builds**

Run: `dotnet build src/Courier.Domain/Courier.Domain.csproj`
Expected: Build succeeded.

---

### Task 3: RolePermissions Unit Tests

**Files:**
- Create: `tests/Courier.Tests.Unit/Security/RolePermissionsTests.cs`

- [ ] **Step 1: Write the unit tests**

```csharp
using Courier.Domain.Authorization;
using Courier.Domain.Enums;
using Shouldly;

namespace Courier.Tests.Unit.Security;

public class RolePermissionsTests
{
    [Fact]
    public void Admin_HasEveryPermission()
    {
        var allPermissions = Enum.GetValues<Permission>().ToHashSet();
        var adminPermissions = RolePermissions.GetPermissions("admin");

        adminPermissions.Count.ShouldBe(allPermissions.Count,
            "Admin should have every permission defined in the enum");

        foreach (var permission in allPermissions)
        {
            adminPermissions.ShouldContain(permission,
                $"Admin is missing permission: {permission}");
        }
    }

    [Fact]
    public void Operator_CanManageJobs()
    {
        var op = RolePermissions.GetPermissions("operator");
        op.ShouldContain(Permission.JobsView);
        op.ShouldContain(Permission.JobsCreate);
        op.ShouldContain(Permission.JobsEdit);
        op.ShouldContain(Permission.JobsDelete);
        op.ShouldContain(Permission.JobsExecute);
        op.ShouldContain(Permission.JobsManageSchedules);
        op.ShouldContain(Permission.JobsManageDependencies);
    }

    [Fact]
    public void Operator_CannotManageConnections()
    {
        var op = RolePermissions.GetPermissions("operator");
        op.ShouldContain(Permission.ConnectionsView);
        op.ShouldContain(Permission.ConnectionsTest);
        op.ShouldNotContain(Permission.ConnectionsCreate);
        op.ShouldNotContain(Permission.ConnectionsEdit);
        op.ShouldNotContain(Permission.ConnectionsDelete);
    }

    [Fact]
    public void Operator_CannotManageKeys()
    {
        var op = RolePermissions.GetPermissions("operator");
        op.ShouldContain(Permission.PgpKeysView);
        op.ShouldContain(Permission.PgpKeysExportPublic);
        op.ShouldNotContain(Permission.PgpKeysManage);
        op.ShouldNotContain(Permission.PgpKeysManageSharing);
        op.ShouldContain(Permission.SshKeysView);
        op.ShouldContain(Permission.SshKeysExportPublic);
        op.ShouldNotContain(Permission.SshKeysManage);
        op.ShouldNotContain(Permission.SshKeysManageSharing);
    }

    [Fact]
    public void Operator_CannotManageUsers()
    {
        var op = RolePermissions.GetPermissions("operator");
        op.ShouldNotContain(Permission.UsersView);
        op.ShouldNotContain(Permission.UsersManage);
    }

    [Fact]
    public void Operator_CannotManageSettings()
    {
        var op = RolePermissions.GetPermissions("operator");
        op.ShouldContain(Permission.SettingsView);
        op.ShouldNotContain(Permission.SettingsManage);
    }

    [Fact]
    public void Viewer_HasOnlyViewPermissions()
    {
        var viewer = RolePermissions.GetPermissions("viewer");

        // Viewer should have no mutating permissions
        viewer.ShouldNotContain(Permission.JobsCreate);
        viewer.ShouldNotContain(Permission.JobsEdit);
        viewer.ShouldNotContain(Permission.JobsDelete);
        viewer.ShouldNotContain(Permission.JobsExecute);
        viewer.ShouldNotContain(Permission.ChainsCreate);
        viewer.ShouldNotContain(Permission.ConnectionsCreate);
        viewer.ShouldNotContain(Permission.PgpKeysManage);
        viewer.ShouldNotContain(Permission.SshKeysManage);
        viewer.ShouldNotContain(Permission.MonitorsCreate);
        viewer.ShouldNotContain(Permission.TagsManage);
        viewer.ShouldNotContain(Permission.NotificationRulesManage);
        viewer.ShouldNotContain(Permission.UsersManage);
        viewer.ShouldNotContain(Permission.SettingsManage);
        viewer.ShouldNotContain(Permission.KnownHostsManage);

        // Viewer should have view + export permissions
        viewer.ShouldContain(Permission.JobsView);
        viewer.ShouldContain(Permission.ChainsView);
        viewer.ShouldContain(Permission.ConnectionsView);
        viewer.ShouldContain(Permission.PgpKeysView);
        viewer.ShouldContain(Permission.PgpKeysExportPublic);
        viewer.ShouldContain(Permission.SshKeysView);
        viewer.ShouldContain(Permission.SshKeysExportPublic);
        viewer.ShouldContain(Permission.MonitorsView);
        viewer.ShouldContain(Permission.TagsView);
        viewer.ShouldContain(Permission.AuditLogView);
        viewer.ShouldContain(Permission.DashboardView);
        viewer.ShouldContain(Permission.SettingsView);
        viewer.ShouldContain(Permission.FilesystemBrowse);
        viewer.ShouldContain(Permission.KnownHostsView);
    }

    [Fact]
    public void UnknownRole_HasNoPermissions()
    {
        RolePermissions.GetPermissions("hacker").ShouldBeEmpty();
        RolePermissions.GetPermissions("").ShouldBeEmpty();
        RolePermissions.GetPermissions("superadmin").ShouldBeEmpty();
    }

    [Fact]
    public void HasPermission_IsCaseInsensitive()
    {
        RolePermissions.HasPermission("Admin", Permission.JobsView).ShouldBeTrue();
        RolePermissions.HasPermission("ADMIN", Permission.JobsView).ShouldBeTrue();
        RolePermissions.HasPermission("admin", Permission.JobsView).ShouldBeTrue();
    }

    [Fact]
    public void HasPermission_ReturnsFalseForDenied()
    {
        RolePermissions.HasPermission("viewer", Permission.JobsCreate).ShouldBeFalse();
        RolePermissions.HasPermission("operator", Permission.ConnectionsCreate).ShouldBeFalse();
    }
}
```

- [ ] **Step 2: Run the tests**

Run: `dotnet test tests/Courier.Tests.Unit --filter "FullyQualifiedName~RolePermissionsTests" -v minimal`
Expected: All tests pass.

- [ ] **Step 3: Commit**

```
feat: Add Permission enum and RolePermissions mapping

Co-Authored-By: Claude Opus 4.6 (1M context) <noreply@anthropic.com>
```

---

## Chunk 2: Authorization Infrastructure

### Task 4: PermissionRequirement and PermissionHandler

**Files:**
- Create: `src/Courier.Features/Security/PermissionRequirement.cs`
- Create: `src/Courier.Features/Security/PermissionHandler.cs`

- [ ] **Step 1: Create PermissionRequirement**

```csharp
using Courier.Domain.Enums;
using Microsoft.AspNetCore.Authorization;

namespace Courier.Features.Security;

/// <summary>
/// Authorization requirement that wraps a Permission enum value.
/// Resolved by PermissionHandler.
/// </summary>
public class PermissionRequirement(Permission permission) : IAuthorizationRequirement
{
    public Permission Permission { get; } = permission;
}
```

- [ ] **Step 2: Create PermissionHandler**

```csharp
using System.Security.Claims;
using Courier.Domain.Authorization;
using Microsoft.AspNetCore.Authorization;

namespace Courier.Features.Security;

/// <summary>
/// Evaluates PermissionRequirement by checking if any of the user's role claims
/// have the required permission. Stateless — registered as Singleton.
/// If dynamic permission lookup is ever needed (e.g., DB-backed),
/// change registration to Scoped to avoid captive dependency bugs.
/// </summary>
public class PermissionHandler : AuthorizationHandler<PermissionRequirement>
{
    protected override Task HandleRequirementAsync(
        AuthorizationHandlerContext context, PermissionRequirement requirement)
    {
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

- [ ] **Step 3: Verify it builds**

Run: `dotnet build src/Courier.Features/Courier.Features.csproj`
Expected: Build succeeded.

---

### Task 5: PermissionPolicyProvider and RequirePermissionAttribute

**Files:**
- Create: `src/Courier.Features/Security/PermissionPolicyProvider.cs`
- Create: `src/Courier.Features/Security/RequirePermissionAttribute.cs`

- [ ] **Step 1: Create PermissionPolicyProvider**

```csharp
using System.Collections.Concurrent;
using Courier.Domain.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;

namespace Courier.Features.Security;

/// <summary>
/// Dynamically creates authorization policies for Permission enum values.
/// Policy names match Permission enum names (e.g., "JobsCreate").
/// Policies are cached in a ConcurrentDictionary (built once per enum value).
/// Falls back to DefaultAuthorizationPolicyProvider for [Authorize] and [AllowAnonymous].
/// </summary>
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

- [ ] **Step 2: Create RequirePermissionAttribute**

```csharp
using Courier.Domain.Enums;
using Microsoft.AspNetCore.Authorization;

namespace Courier.Features.Security;

/// <summary>
/// Convenience attribute for permission-based authorization.
/// Usage: [RequirePermission(Permission.JobsCreate)]
/// Resolves to a named policy matching the Permission enum name.
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method, AllowMultiple = true)]
public class RequirePermissionAttribute : AuthorizeAttribute
{
    public RequirePermissionAttribute(Permission permission)
        : base(permission.ToString()) { }
}
```

- [ ] **Step 3: Verify it builds**

Run: `dotnet build src/Courier.Features/Courier.Features.csproj`
Expected: Build succeeded.

---

### Task 6: ApiResponseAuthorizationHandler (Custom 403)

**Files:**
- Create: `src/Courier.Features/Security/ApiResponseAuthorizationHandler.cs`

- [ ] **Step 1: Create the 403 response handler**

Note: The existing `ErrorCodes` class at `src/Courier.Domain/Common/ErrorCodes.cs` already defines `Forbidden = 10008`. Use that.

```csharp
using Courier.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using Microsoft.AspNetCore.Http;

namespace Courier.Features.Security;

/// <summary>
/// Returns 403 responses in the standard ApiResponse envelope format,
/// consistent with all other API error responses. Uses existing ErrorCodes.Forbidden (10008).
/// </summary>
public class ApiResponseAuthorizationHandler : IAuthorizationMiddlewareResultHandler
{
    private readonly AuthorizationMiddlewareResultHandler _default = new();

    public async Task HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
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

- [ ] **Step 2: Verify it builds**

Run: `dotnet build src/Courier.Features/Courier.Features.csproj`
Expected: Build succeeded.

---

### Task 7: PermissionHandler Unit Tests

**Files:**
- Create: `tests/Courier.Tests.Unit/Security/PermissionHandlerTests.cs`

- [ ] **Step 1: Write the handler unit tests**

```csharp
using System.Security.Claims;
using Courier.Domain.Enums;
using Courier.Features.Security;
using Microsoft.AspNetCore.Authorization;
using Shouldly;

namespace Courier.Tests.Unit.Security;

public class PermissionHandlerTests
{
    private readonly PermissionHandler _handler = new();

    private async Task<AuthorizationResult> EvaluateAsync(string role, Permission permission)
    {
        var claims = new[] { new Claim(ClaimTypes.Role, role) };
        var user = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
        var requirement = new PermissionRequirement(permission);
        var context = new AuthorizationHandlerContext([requirement], user, null);

        await _handler.HandleAsync(context);

        return context.HasSucceeded
            ? AuthorizationResult.Success()
            : AuthorizationResult.Failed();
    }

    [Fact]
    public async Task Admin_HasAllPermissions()
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
    [InlineData("operator", Permission.PgpKeysManage, false)]
    [InlineData("operator", Permission.MonitorsCreate, true)]
    [InlineData("viewer", Permission.UsersManage, false)]
    [InlineData("admin", Permission.UsersManage, true)]
    public async Task RolePermissionCheck(string role, Permission perm, bool expected)
    {
        var result = await EvaluateAsync(role, perm);
        result.Succeeded.ShouldBe(expected, $"{role} + {perm} should be {expected}");
    }

    [Fact]
    public async Task NoRoleClaim_Fails()
    {
        var user = new ClaimsPrincipal(new ClaimsIdentity([], "Test"));
        var requirement = new PermissionRequirement(Permission.JobsView);
        var context = new AuthorizationHandlerContext([requirement], user, null);

        await _handler.HandleAsync(context);

        context.HasSucceeded.ShouldBeFalse();
    }

    [Fact]
    public async Task MultipleRoleClaims_ChecksAll()
    {
        // User has both viewer and operator roles
        var claims = new[]
        {
            new Claim(ClaimTypes.Role, "viewer"),
            new Claim(ClaimTypes.Role, "operator"),
        };
        var user = new ClaimsPrincipal(new ClaimsIdentity(claims, "Test"));
        var requirement = new PermissionRequirement(Permission.JobsCreate);
        var context = new AuthorizationHandlerContext([requirement], user, null);

        await _handler.HandleAsync(context);

        // operator has JobsCreate, so should succeed even though viewer doesn't
        context.HasSucceeded.ShouldBeTrue();
    }
}
```

- [ ] **Step 2: Run the tests**

Run: `dotnet test tests/Courier.Tests.Unit --filter "FullyQualifiedName~PermissionHandlerTests" -v minimal`
Expected: All tests pass.

- [ ] **Step 3: Commit**

```
feat: Add authorization infrastructure (handler, provider, attribute, 403 handler)

Co-Authored-By: Claude Opus 4.6 (1M context) <noreply@anthropic.com>
```

---

## Chunk 3: API Registration + Controller Migration

### Task 8: Register Authorization Services in Program.cs

**Files:**
- Modify: `src/Courier.Api/Program.cs`

- [ ] **Step 1: Add authorization service registrations**

In `src/Courier.Api/Program.cs`, find the line:
```csharp
builder.Services.AddAuthorization();
```

Replace it with:
```csharp
// Permission-based authorization
builder.Services.AddSingleton<IAuthorizationPolicyProvider, PermissionPolicyProvider>();
builder.Services.AddSingleton<IAuthorizationHandler, PermissionHandler>();
builder.Services.AddSingleton<IAuthorizationMiddlewareResultHandler, ApiResponseAuthorizationHandler>();
builder.Services.AddAuthorization();
```

Add the using at the top:
```csharp
using Courier.Features.Security;
```

- [ ] **Step 2: Verify it builds and existing tests pass**

Run: `dotnet build src/Courier.Api/Courier.Api.csproj && dotnet test tests/Courier.Tests.Unit -v minimal --no-build`
Expected: Build succeeded. All existing tests pass (no regressions — existing tests use admin role which has all permissions).

---

### Task 9: Migrate JobsController

**Files:**
- Modify: `src/Courier.Features/Jobs/JobsController.cs`

- [ ] **Step 1: Replace all `[Authorize(Roles = "admin,operator")]` with specific `[RequirePermission]` attributes**

Add using at top of file:
```csharp
using Courier.Domain.Enums;
using Courier.Features.Security;
```

Replace each method-level `[Authorize(Roles = "admin,operator")]` with the correct permission per the spec:

| Method | Old | New |
|--------|-----|-----|
| List | (inherits class `[Authorize]`) | `[RequirePermission(Permission.JobsView)]` |
| GetById | (inherits) | `[RequirePermission(Permission.JobsView)]` |
| ListSteps | (inherits) | `[RequirePermission(Permission.JobsView)]` |
| ListExecutions | (inherits) | `[RequirePermission(Permission.JobsView)]` |
| GetExecution | (inherits) | `[RequirePermission(Permission.JobsView)]` |
| ListSchedules | (inherits) | `[RequirePermission(Permission.JobsView)]` |
| GetVersions | (inherits) | `[RequirePermission(Permission.JobsView)]` |
| GetVersion | (inherits) | `[RequirePermission(Permission.JobsView)]` |
| ListDependencies | (inherits) | `[RequirePermission(Permission.JobsView)]` |
| Create | `[Authorize(Roles = "admin,operator")]` | `[RequirePermission(Permission.JobsCreate)]` |
| Update | `[Authorize(Roles = "admin,operator")]` | `[RequirePermission(Permission.JobsEdit)]` |
| Delete | `[Authorize(Roles = "admin,operator")]` | `[RequirePermission(Permission.JobsDelete)]` |
| ReplaceSteps | `[Authorize(Roles = "admin,operator")]` | `[RequirePermission(Permission.JobsEdit)]` |
| AddStep | `[Authorize(Roles = "admin,operator")]` | `[RequirePermission(Permission.JobsEdit)]` |
| Trigger | `[Authorize(Roles = "admin,operator")]` | `[RequirePermission(Permission.JobsExecute)]` |
| PauseExecution | `[Authorize(Roles = "admin,operator")]` | `[RequirePermission(Permission.JobsExecute)]` |
| ResumeExecution | `[Authorize(Roles = "admin,operator")]` | `[RequirePermission(Permission.JobsExecute)]` |
| CancelExecution | `[Authorize(Roles = "admin,operator")]` | `[RequirePermission(Permission.JobsExecute)]` |
| CreateSchedule | `[Authorize(Roles = "admin,operator")]` | `[RequirePermission(Permission.JobsManageSchedules)]` |
| UpdateSchedule | `[Authorize(Roles = "admin,operator")]` | `[RequirePermission(Permission.JobsManageSchedules)]` |
| DeleteSchedule | `[Authorize(Roles = "admin,operator")]` | `[RequirePermission(Permission.JobsManageSchedules)]` |
| AddDependency | `[Authorize(Roles = "admin,operator")]` | `[RequirePermission(Permission.JobsManageDependencies)]` |
| RemoveDependency | `[Authorize(Roles = "admin,operator")]` | `[RequirePermission(Permission.JobsManageDependencies)]` |

Keep `[Authorize]` at class level for authentication. Remove ALL `[Authorize(Roles = "...")]` from methods.

- [ ] **Step 2: Verify build**

Run: `dotnet build src/Courier.Features/Courier.Features.csproj`
Expected: Build succeeded.

---

### Task 10: Migrate ChainsController

**Files:**
- Modify: `src/Courier.Features/Chains/ChainsController.cs`

- [ ] **Step 1: Replace attributes per spec**

Same pattern as Task 9. Add usings, then:

| Method | New |
|--------|-----|
| List, GetById, ListExecutions, GetExecution, ListSchedules | `[RequirePermission(Permission.ChainsView)]` |
| Create | `[RequirePermission(Permission.ChainsCreate)]` |
| Update, ReplaceMembers | `[RequirePermission(Permission.ChainsEdit)]` |
| Delete | `[RequirePermission(Permission.ChainsDelete)]` |
| Execute | `[RequirePermission(Permission.ChainsExecute)]` |
| CreateSchedule, UpdateSchedule, DeleteSchedule | `[RequirePermission(Permission.ChainsManageSchedules)]` |

- [ ] **Step 2: Verify build**

Run: `dotnet build src/Courier.Features/Courier.Features.csproj`

---

### Task 11: Migrate ConnectionsController + KnownHostsController

**Files:**
- Modify: `src/Courier.Features/Connections/ConnectionsController.cs`
- Modify: `src/Courier.Features/Connections/KnownHostsController.cs`

- [ ] **Step 1: ConnectionsController — replace attributes**

| Method | Old | New |
|--------|-----|-----|
| List, GetById | (inherits) | `[RequirePermission(Permission.ConnectionsView)]` |
| Create | `[Authorize(Roles = "admin")]` | `[RequirePermission(Permission.ConnectionsCreate)]` |
| Update | `[Authorize(Roles = "admin")]` | `[RequirePermission(Permission.ConnectionsEdit)]` |
| Delete | `[Authorize(Roles = "admin")]` | `[RequirePermission(Permission.ConnectionsDelete)]` |
| TestConnection | `[Authorize(Roles = "admin,operator")]` | `[RequirePermission(Permission.ConnectionsTest)]` |

- [ ] **Step 2: KnownHostsController — replace attributes**

| Method | Old | New |
|--------|-----|-----|
| ListByConnection, GetById | (inherits) | `[RequirePermission(Permission.KnownHostsView)]` |
| Create | `[Authorize(Roles = "admin")]` | `[RequirePermission(Permission.KnownHostsManage)]` |
| Delete | `[Authorize(Roles = "admin")]` | `[RequirePermission(Permission.KnownHostsManage)]` |
| Approve | `[Authorize(Roles = "admin")]` | `[RequirePermission(Permission.KnownHostsManage)]` |

- [ ] **Step 3: Verify build**

Run: `dotnet build src/Courier.Features/Courier.Features.csproj`

---

### Task 12: Migrate PgpKeysController + SshKeysController

**Files:**
- Modify: `src/Courier.Features/PgpKeys/PgpKeysController.cs`
- Modify: `src/Courier.Features/SshKeys/SshKeysController.cs`

- [ ] **Step 1: PgpKeysController — replace attributes**

| Method | New |
|--------|-----|
| List, GetById | `[RequirePermission(Permission.PgpKeysView)]` |
| ExportPublicKey | `[RequirePermission(Permission.PgpKeysExportPublic)]` |
| Generate, Import, Update, Delete, Retire, Revoke, Activate, SetSuccessor | `[RequirePermission(Permission.PgpKeysManage)]` |
| CreateShareLink, ListShareLinks, RevokeShareLink | `[RequirePermission(Permission.PgpKeysManageSharing)]` |
| GetSharedKey | `[AllowAnonymous]` (no change) |

- [ ] **Step 2: SshKeysController — replace attributes**

Same pattern but with `SshKeys*` permissions. Note: SSH has no Revoke endpoint (only Retire + Activate).

- [ ] **Step 3: Verify build**

Run: `dotnet build src/Courier.Features/Courier.Features.csproj`

---

### Task 13: Migrate MonitorsController, TagsController, NotificationRulesController

**Files:**
- Modify: `src/Courier.Features/Monitors/MonitorsController.cs`
- Modify: `src/Courier.Features/Tags/TagsController.cs`
- Modify: `src/Courier.Features/Notifications/NotificationRulesController.cs`

- [ ] **Step 1: MonitorsController**

| Method | New |
|--------|-----|
| List, GetById, ListFileLog | `[RequirePermission(Permission.MonitorsView)]` |
| Create | `[RequirePermission(Permission.MonitorsCreate)]` |
| Update | `[RequirePermission(Permission.MonitorsEdit)]` |
| Delete | `[RequirePermission(Permission.MonitorsDelete)]` |
| Activate, Pause, Disable, AcknowledgeError | `[RequirePermission(Permission.MonitorsChangeState)]` |

- [ ] **Step 2: TagsController**

| Method | New |
|--------|-----|
| List, GetById, ListEntities | `[RequirePermission(Permission.TagsView)]` |
| Create, Update, Delete, Assign, Unassign | `[RequirePermission(Permission.TagsManage)]` |

- [ ] **Step 3: NotificationRulesController**

| Method | New |
|--------|-----|
| List, GetById | `[RequirePermission(Permission.NotificationRulesView)]` |
| Create, Update, Delete, Test | `[RequirePermission(Permission.NotificationRulesManage)]` |

- [ ] **Step 4: Verify build**

Run: `dotnet build src/Courier.Features/Courier.Features.csproj`

---

### Task 14: Migrate SettingsController, UsersController, Read-Only Controllers

**Files:**
- Modify: `src/Courier.Features/Settings/SettingsController.cs`
- Modify: `src/Courier.Features/Users/UsersController.cs`
- Modify: `src/Courier.Features/Dashboard/DashboardController.cs`
- Modify: `src/Courier.Features/AuditLog/AuditLogController.cs`
- Modify: `src/Courier.Features/Notifications/NotificationLogsController.cs`
- Modify: `src/Courier.Features/Filesystem/FilesystemController.cs`

- [ ] **Step 1: SettingsController**

| Method | Old | New |
|--------|-----|-----|
| GetAuthSettings | (inherits `[Authorize]`) | No change (all users can read) |
| UpdateAuthSettings | `[Authorize(Roles = "admin")]` | `[RequirePermission(Permission.SettingsManage)]` |

- [ ] **Step 2: UsersController**

Remove class-level `[Authorize(Roles = "admin")]`. Add `[Authorize]` at class level instead, then:

| Method | New |
|--------|-----|
| List, GetById | `[RequirePermission(Permission.UsersView)]` |
| Create, Update, Delete, ResetPassword | `[RequirePermission(Permission.UsersManage)]` |

- [ ] **Step 3: Read-only controllers**

Add `[RequirePermission]` to each:
- DashboardController: all endpoints → `[RequirePermission(Permission.DashboardView)]`
- AuditLogController: all endpoints → `[RequirePermission(Permission.AuditLogView)]`
- NotificationLogsController: all endpoints → `[RequirePermission(Permission.NotificationLogsView)]`
- FilesystemController: Browse → `[RequirePermission(Permission.FilesystemBrowse)]`

- [ ] **Step 4: Verify build and run existing tests**

Run: `dotnet build Courier.slnx && dotnet test tests/Courier.Tests.Unit -v minimal`
Expected: Build succeeded. All existing tests pass.

- [ ] **Step 5: Verify no `[Authorize(Roles` remains in codebase**

Run: `grep -rn "Authorize(Roles" src/Courier.Features/ --include="*.cs"`
Expected: No output (all occurrences replaced).

- [ ] **Step 6: Commit**

```
feat: Migrate all controllers from [Authorize(Roles)] to [RequirePermission]

Co-Authored-By: Claude Opus 4.6 (1M context) <noreply@anthropic.com>
```

---

## Chunk 4: Integration Test Infrastructure

### Task 15: Modify TestAuthHandler for Role Parameterization

**Files:**
- Modify: `tests/Courier.Tests.Integration/CourierApiFactory.cs`

- [ ] **Step 1: Change explicit interface implementations to implicit (public)**

`CourierApiFactory` uses explicit interface implementations for `IAsyncLifetime.InitializeAsync()` and
`IAsyncDisposable.DisposeAsync()`, which means they can't be called on the concrete type. Change them
to public methods so `RbacFixture` (and any future test fixture) can call them directly:

```csharp
// Change from:
async ValueTask IAsyncLifetime.InitializeAsync() { ... }
async ValueTask IAsyncDisposable.DisposeAsync() { ... }

// To:
public async ValueTask InitializeAsync() { ... }
public async ValueTask DisposeAsync() { ... }
```

- [ ] **Step 2: Update TestAuthHandler to read role from X-Test-Role header**

In the `TestAuthHandler.HandleAuthenticateAsync` method, replace the hardcoded claims with:

```csharp
protected override Task<AuthenticateResult> HandleAuthenticateAsync()
{
    // Support anonymous testing: if X-Test-Anonymous header is present, return NoResult
    if (Context.Request.Headers.ContainsKey("X-Test-Anonymous"))
    {
        return Task.FromResult(AuthenticateResult.NoResult());
    }

    // Read role from custom header, default to "admin" for backward compatibility
    var role = Context.Request.Headers["X-Test-Role"].FirstOrDefault() ?? "admin";

    var userId = role switch
    {
        "operator" => "00000000-0000-0000-0000-000000000002",
        "viewer" => "00000000-0000-0000-0000-000000000003",
        _ => "00000000-0000-0000-0000-000000000001", // admin
    };

    var claims = new[]
    {
        new Claim(ClaimTypes.NameIdentifier, userId),
        new Claim(ClaimTypes.Name, $"test{role}"),
        new Claim(ClaimTypes.Role, role),
        new Claim("name", $"Test {char.ToUpper(role[0])}{role[1..]}"),
    };

    var identity = new ClaimsIdentity(claims, "Test");
    var principal = new ClaimsPrincipal(identity);
    var ticket = new AuthenticationTicket(principal, "Test");

    return Task.FromResult(AuthenticateResult.Success(ticket));
}
```

- [ ] **Step 2: Run existing integration tests to verify backward compatibility**

Run: `dotnet test tests/Courier.Tests.Integration -v minimal`
Expected: All existing integration tests still pass (they don't send X-Test-Role, so they default to "admin").

---

### Task 16: Create RBAC Test Infrastructure

**Files:**
- Create: `tests/Courier.Tests.Integration/Rbac/RoleHeaderHandler.cs`
- Create: `tests/Courier.Tests.Integration/Rbac/RbacFixture.cs`
- Create: `tests/Courier.Tests.Integration/Rbac/RbacTestBase.cs`

- [ ] **Step 1: Create RoleHeaderHandler**

```csharp
namespace Courier.Tests.Integration.Rbac;

/// <summary>
/// DelegatingHandler that adds X-Test-Role header to all requests.
/// Used to parameterize the test auth handler's role.
/// </summary>
public class RoleHeaderHandler(string role) : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Headers.Add("X-Test-Role", role);
        return base.SendAsync(request, cancellationToken);
    }
}

/// <summary>
/// DelegatingHandler that marks requests as anonymous.
/// TestAuthHandler returns NoResult when it sees this header, causing 401.
/// </summary>
public class AnonymousHeaderHandler : DelegatingHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request, CancellationToken cancellationToken)
    {
        request.Headers.Add("X-Test-Anonymous", "true");
        return base.SendAsync(request, cancellationToken);
    }
}
```

- [ ] **Step 2: Create RbacFixture**

```csharp
using System.Net.Http.Json;

namespace Courier.Tests.Integration.Rbac;

public class RbacFixture : IAsyncLifetime
{
    private CourierApiFactory _factory = null!;

    public HttpClient AdminClient { get; private set; } = null!;
    public HttpClient OperatorClient { get; private set; } = null!;
    public HttpClient ViewerClient { get; private set; } = null!;
    public HttpClient AnonymousClient { get; private set; } = null!;

    // Seeded test data IDs — populated during InitializeAsync.
    // Individual test classes may seed additional entity types as needed.
    public Guid TestJobId { get; private set; }
    public Guid TestConnectionId { get; private set; }
    public Guid TestTagId { get; private set; }

    public async Task InitializeAsync()
    {
        _factory = new CourierApiFactory();
        // CourierApiFactory.InitializeAsync is now a public method (changed in Task 15)
        await _factory.InitializeAsync();

        AdminClient = CreateClientWithHandler(new RoleHeaderHandler("admin"));
        OperatorClient = CreateClientWithHandler(new RoleHeaderHandler("operator"));
        ViewerClient = CreateClientWithHandler(new RoleHeaderHandler("viewer"));
        AnonymousClient = CreateClientWithHandler(new AnonymousHeaderHandler());

        // Seed minimal shared test data using admin client.
        // Test classes needing additional entities (connections, keys, monitors, etc.)
        // should seed their own in a class-level helper using Fixture.AdminClient.
        await SeedTestDataAsync();
    }

    private HttpClient CreateClientWithHandler(DelegatingHandler handler)
    {
        handler.InnerHandler = _factory.Server.CreateHandler();
        return new HttpClient(handler)
        {
            BaseAddress = _factory.Server.BaseAddress,
        };
    }

    private async Task SeedTestDataAsync()
    {
        // Create a test job
        var jobResponse = await AdminClient.PostAsJsonAsync("api/v1/jobs", new
        {
            name = "rbac-test-job",
            description = "Test job for RBAC tests",
        });
        if (jobResponse.IsSuccessStatusCode)
        {
            var jobResult = await jobResponse.Content.ReadFromJsonAsync<JsonElement>();
            if (jobResult.TryGetProperty("data", out var data) &&
                data.TryGetProperty("id", out var id))
            {
                TestJobId = Guid.Parse(id.GetString()!);
            }
        }

        // Create a test tag
        var tagResponse = await AdminClient.PostAsJsonAsync("api/v1/tags", new
        {
            name = "rbac-test-tag",
            color = "#FF0000",
        });
        if (tagResponse.IsSuccessStatusCode)
        {
            var tagResult = await tagResponse.Content.ReadFromJsonAsync<JsonElement>();
            if (tagResult.TryGetProperty("data", out var data) &&
                data.TryGetProperty("id", out var id))
            {
                TestTagId = Guid.Parse(id.GetString()!);
            }
        }
    }

    public async Task DisposeAsync()
    {
        // Cleanup seeded data
        if (TestJobId != Guid.Empty)
            await AdminClient.DeleteAsync($"api/v1/jobs/{TestJobId}");
        if (TestTagId != Guid.Empty)
            await AdminClient.DeleteAsync($"api/v1/tags/{TestTagId}");

        AdminClient?.Dispose();
        OperatorClient?.Dispose();
        ViewerClient?.Dispose();
        AnonymousClient?.Dispose();

        if (_factory is not null)
            await _factory.DisposeAsync();
    }
}

[CollectionDefinition("Rbac")]
public class RbacCollection : ICollectionFixture<RbacFixture> { }
```

Note: Add `using System.Text.Json;` for `JsonElement`. The fixture seeds minimal data. Individual test classes can seed additional data as needed.

- [ ] **Step 3: Create RbacTestBase**

```csharp
namespace Courier.Tests.Integration.Rbac;

[Collection("Rbac")]
public abstract class RbacTestBase(RbacFixture fixture)
{
    protected RbacFixture Fixture { get; } = fixture;

    protected HttpClient ClientForRole(string role) => role switch
    {
        "admin" => Fixture.AdminClient,
        "operator" => Fixture.OperatorClient,
        "viewer" => Fixture.ViewerClient,
        "anonymous" => Fixture.AnonymousClient,
        _ => throw new ArgumentException($"Unknown role: {role}"),
    };
}
```

- [ ] **Step 4: Verify it builds**

Run: `dotnet build tests/Courier.Tests.Integration/Courier.Tests.Integration.csproj`
Expected: Build succeeded.

- [ ] **Step 5: Commit**

```
feat: Add RBAC integration test infrastructure

Co-Authored-By: Claude Opus 4.6 (1M context) <noreply@anthropic.com>
```

---

## Chunk 5: RBAC Integration Tests (Part 1 — Core Resources)

### Task 17: JobsRbacTests

**Files:**
- Create: `tests/Courier.Tests.Integration/Rbac/JobsRbacTests.cs`

- [ ] **Step 1: Write parameterized RBAC tests for Jobs endpoints**

Test every endpoint with all 3 roles. Use `[Theory]` + `[InlineData]` with role string and expected `HttpStatusCode`. Pattern:

```csharp
using System.Net;
using System.Net.Http.Json;
using Shouldly;

namespace Courier.Tests.Integration.Rbac;

public class JobsRbacTests(RbacFixture fixture) : RbacTestBase(fixture)
{
    [Theory]
    [InlineData("admin", HttpStatusCode.OK)]
    [InlineData("operator", HttpStatusCode.OK)]
    [InlineData("viewer", HttpStatusCode.OK)]
    public async Task ListJobs(string role, HttpStatusCode expected)
    {
        var response = await ClientForRole(role).GetAsync("api/v1/jobs");
        response.StatusCode.ShouldBe(expected);
    }

    [Theory]
    [InlineData("admin", HttpStatusCode.OK)]
    [InlineData("operator", HttpStatusCode.OK)]
    [InlineData("viewer", HttpStatusCode.Forbidden)]
    public async Task CreateJob(string role, HttpStatusCode expected)
    {
        var response = await ClientForRole(role).PostAsJsonAsync("api/v1/jobs", new
        {
            name = $"rbac-create-{role}-{Guid.NewGuid():N}",
            description = "RBAC test",
        });
        // For successful creates, clean up
        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<JsonElement>();
            var id = result.GetProperty("data").GetProperty("id").GetString();
            await Fixture.AdminClient.DeleteAsync($"api/v1/jobs/{id}");
        }
        response.StatusCode.ShouldBe(expected);
    }

    // Continue pattern for: Update, Delete, Trigger, PauseExecution,
    // ResumeExecution, CancelExecution, ReplaceSteps, AddStep,
    // ListSteps, ListExecutions, GetExecution, ListSchedules,
    // CreateSchedule, UpdateSchedule, DeleteSchedule,
    // GetVersions, GetVersion, ListDependencies, AddDependency, RemoveDependency
}
```

Each mutating test that succeeds should clean up after itself.
Each test that expects `Forbidden` can use a simple assertion on `response.StatusCode`.

For endpoints needing an existing resource ID, use `Fixture.TestJobId`.

- [ ] **Step 2: Run the tests**

Run: `dotnet test tests/Courier.Tests.Integration --filter "FullyQualifiedName~JobsRbacTests" -v minimal`
Expected: All tests pass.

---

### Task 18: ChainsRbacTests

**Files:**
- Create: `tests/Courier.Tests.Integration/Rbac/ChainsRbacTests.cs`

- [ ] **Step 1: Write RBAC tests for Chains endpoints**

Same pattern as JobsRbacTests. Cover: List, GetById, Create, Update, Delete, ReplaceMembers, Execute, ListExecutions, GetExecution, ListSchedules, CreateSchedule, UpdateSchedule, DeleteSchedule.

- [ ] **Step 2: Run tests**

Run: `dotnet test tests/Courier.Tests.Integration --filter "FullyQualifiedName~ChainsRbacTests" -v minimal`

---

### Task 19: ConnectionsRbacTests + KnownHostsRbacTests

**Files:**
- Create: `tests/Courier.Tests.Integration/Rbac/ConnectionsRbacTests.cs`
- Create: `tests/Courier.Tests.Integration/Rbac/KnownHostsRbacTests.cs`

- [ ] **Step 1: Write Connections RBAC tests**

Key role differences: Create/Edit/Delete = admin-only. Test = admin+operator. View = all.

- [ ] **Step 2: Write KnownHosts RBAC tests**

Create/Delete/Approve = admin-only. View = all.

- [ ] **Step 3: Run tests**

Run: `dotnet test tests/Courier.Tests.Integration --filter "FullyQualifiedName~ConnectionsRbacTests|FullyQualifiedName~KnownHostsRbacTests" -v minimal`

- [ ] **Step 4: Commit**

```
feat: Add RBAC integration tests for Jobs, Chains, Connections

Co-Authored-By: Claude Opus 4.6 (1M context) <noreply@anthropic.com>
```

---

## Chunk 6: RBAC Integration Tests (Part 2 — Keys, Monitors, Remaining)

### Task 20: PgpKeysRbacTests + SshKeysRbacTests

**Files:**
- Create: `tests/Courier.Tests.Integration/Rbac/PgpKeysRbacTests.cs`
- Create: `tests/Courier.Tests.Integration/Rbac/SshKeysRbacTests.cs`

- [ ] **Step 1: PGP keys — test all endpoints across roles**

Key distinctions: Manage (generate/import/lifecycle) = admin-only. ExportPublic = all. Sharing = admin-only. View = all.

- [ ] **Step 2: SSH keys — test all endpoints across roles**

Same pattern as PGP. Note: no Revoke endpoint on SSH.

- [ ] **Step 3: Run tests**

Run: `dotnet test tests/Courier.Tests.Integration --filter "FullyQualifiedName~PgpKeysRbacTests|FullyQualifiedName~SshKeysRbacTests" -v minimal`

---

### Task 21: MonitorsRbacTests + TagsRbacTests + NotificationRulesRbacTests

**Files:**
- Create: `tests/Courier.Tests.Integration/Rbac/MonitorsRbacTests.cs`
- Create: `tests/Courier.Tests.Integration/Rbac/TagsRbacTests.cs`
- Create: `tests/Courier.Tests.Integration/Rbac/NotificationRulesRbacTests.cs`

- [ ] **Step 1: Monitors — CRUD + state changes**

Create/Edit/Delete/ChangeState = admin+operator. View = all.

- [ ] **Step 2: Tags — CRUD + assign**

Manage = admin+operator. View = all.

- [ ] **Step 3: NotificationRules — CRUD + test**

Manage = admin+operator. View = all.

- [ ] **Step 4: Run tests**

Run: `dotnet test tests/Courier.Tests.Integration --filter "FullyQualifiedName~MonitorsRbacTests|FullyQualifiedName~TagsRbacTests|FullyQualifiedName~NotificationRulesRbacTests" -v minimal`

---

### Task 22: UsersRbacTests + SettingsRbacTests + AuthRbacTests + ReadOnlyRbacTests

**Files:**
- Create: `tests/Courier.Tests.Integration/Rbac/UsersRbacTests.cs`
- Create: `tests/Courier.Tests.Integration/Rbac/SettingsRbacTests.cs`
- Create: `tests/Courier.Tests.Integration/Rbac/AuthRbacTests.cs`
- Create: `tests/Courier.Tests.Integration/Rbac/ReadOnlyRbacTests.cs`

- [ ] **Step 1: Users — admin-only for everything**

All CRUD + ResetPassword = admin-only. View = admin-only (UsersView only granted to admin).

- [ ] **Step 2: Settings**

GetAuthSettings = all (no permission gate). UpdateAuthSettings = admin-only.

- [ ] **Step 3: AuthRbacTests — verify auth endpoints under new infrastructure**

AuthController has no `[RequirePermission]` but still needs tests to verify behavior under the
new authorization system. Test:
- Login, Refresh → 200 for anonymous (they're `[AllowAnonymous]`)
- Logout, Me, ChangePassword → 200 for admin, operator, viewer (they're `[Authorize]`)
- Logout, Me, ChangePassword → 401 for anonymous

```csharp
public class AuthRbacTests(RbacFixture fixture) : RbacTestBase(fixture)
{
    [Theory]
    [InlineData("admin", HttpStatusCode.OK)]
    [InlineData("operator", HttpStatusCode.OK)]
    [InlineData("viewer", HttpStatusCode.OK)]
    [InlineData("anonymous", HttpStatusCode.Unauthorized)]
    public async Task GetMe(string role, HttpStatusCode expected)
    {
        var response = await ClientForRole(role).GetAsync("api/v1/auth/me");
        response.StatusCode.ShouldBe(expected);
    }

    // Login and Refresh are AllowAnonymous — test they work for anonymous
    [Fact]
    public async Task Login_AllowsAnonymous()
    {
        var response = await Fixture.AnonymousClient.PostAsJsonAsync("api/v1/auth/login", new
        {
            username = "nonexistent",
            password = "wrong",
        });
        // Should get 401 (invalid credentials) NOT 403 (forbidden) — proves auth is anonymous
        response.StatusCode.ShouldNotBe(HttpStatusCode.Forbidden);
    }
}
```

- [ ] **Step 4: Read-only controllers**

Dashboard (GetSummary, GetRecentExecutions, etc.), AuditLog (List, ListByEntity), NotificationLogs (List), Filesystem (Browse) — all accessible by all three roles.

- [ ] **Step 5: Run all RBAC tests**

Run: `dotnet test tests/Courier.Tests.Integration --filter "Namespace~Rbac" -v minimal`
Expected: All RBAC tests pass.

- [ ] **Step 5: Commit**

```
feat: Add RBAC integration tests for all controllers

Co-Authored-By: Claude Opus 4.6 (1M context) <noreply@anthropic.com>
```

---

## Chunk 7: Frontend Role-Gating

### Task 23: Create permissions.ts and usePermissions Hook

**Files:**
- Create: `src/Courier.Frontend/src/lib/permissions.ts`
- Create: `src/Courier.Frontend/src/lib/hooks/use-permissions.ts`

- [ ] **Step 1: Create permissions.ts**

```typescript
// Module-scope: Sets created once at import, not per render.
// This file mirrors the backend RolePermissions.cs — keep in sync.

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

const allPermissions: Permission[] = [
  "JobsView", "JobsCreate", "JobsEdit", "JobsDelete",
  "JobsExecute", "JobsManageSchedules", "JobsManageDependencies",
  "ChainsView", "ChainsCreate", "ChainsEdit", "ChainsDelete",
  "ChainsExecute", "ChainsManageSchedules",
  "ConnectionsView", "ConnectionsCreate", "ConnectionsEdit",
  "ConnectionsDelete", "ConnectionsTest",
  "PgpKeysView", "PgpKeysManage", "PgpKeysExportPublic", "PgpKeysManageSharing",
  "SshKeysView", "SshKeysManage", "SshKeysExportPublic", "SshKeysManageSharing",
  "MonitorsView", "MonitorsCreate", "MonitorsEdit",
  "MonitorsDelete", "MonitorsChangeState",
  "TagsView", "TagsManage",
  "NotificationRulesView", "NotificationRulesManage", "NotificationLogsView",
  "AuditLogView",
  "UsersView", "UsersManage",
  "SettingsView", "SettingsManage",
  "DashboardView",
  "FilesystemBrowse",
  "KnownHostsView", "KnownHostsManage",
];

export const rolePermissions: Record<string, ReadonlySet<Permission>> = {
  admin: new Set(allPermissions),

  operator: new Set<Permission>([
    "JobsView", "JobsCreate", "JobsEdit", "JobsDelete",
    "JobsExecute", "JobsManageSchedules", "JobsManageDependencies",
    "ChainsView", "ChainsCreate", "ChainsEdit", "ChainsDelete",
    "ChainsExecute", "ChainsManageSchedules",
    "ConnectionsView", "ConnectionsTest",
    "PgpKeysView", "PgpKeysExportPublic",
    "SshKeysView", "SshKeysExportPublic",
    "MonitorsView", "MonitorsCreate", "MonitorsEdit",
    "MonitorsDelete", "MonitorsChangeState",
    "TagsView", "TagsManage",
    "NotificationRulesView", "NotificationRulesManage", "NotificationLogsView",
    "AuditLogView", "DashboardView", "SettingsView",
    "FilesystemBrowse", "KnownHostsView",
  ]),

  viewer: new Set<Permission>([
    "JobsView", "ChainsView", "ConnectionsView",
    "PgpKeysView", "PgpKeysExportPublic",
    "SshKeysView", "SshKeysExportPublic",
    "MonitorsView", "TagsView",
    "NotificationRulesView", "NotificationLogsView",
    "AuditLogView", "DashboardView", "SettingsView",
    "FilesystemBrowse", "KnownHostsView",
  ]),
};
```

- [ ] **Step 2: Create use-permissions.ts**

```typescript
import { useAuth } from "@/lib/auth";
import { type Permission, rolePermissions } from "@/lib/permissions";

export { type Permission } from "@/lib/permissions";

export function usePermissions() {
  const { user } = useAuth();
  const permissions = rolePermissions[user?.role ?? ""] ?? new Set<Permission>();

  return {
    can: (permission: Permission): boolean => permissions.has(permission),
    canAny: (...perms: Permission[]): boolean => perms.some((p) => permissions.has(p)),
    role: user?.role ?? null,
  };
}
```

Note: `useAuth` is exported from `src/Courier.Frontend/src/lib/auth.tsx` (not from `use-auth-actions.ts`, which only exports `useChangePassword`).

- [ ] **Step 3: Verify frontend builds**

Run: `cd src/Courier.Frontend && npx tsc --noEmit`
Expected: No type errors.

---

### Task 24: Add Permission Gating to Frontend Pages

**Files:**
- Modify: ~10 page files in `src/Courier.Frontend/src/app/(app)/`

- [ ] **Step 1: Gate action buttons on Jobs pages**

In `/jobs/page.tsx`, wrap the Create button:
```tsx
const { can } = usePermissions();
// ...
{can("JobsCreate") && <Button>Create Job</Button>}
```

In `/jobs/[id]/page.tsx`, wrap Edit/Delete/Trigger buttons similarly.

- [ ] **Step 2: Gate Connections pages**

Create/Edit/Delete → `can("ConnectionsCreate")` etc. (admin-only)
Test → `can("ConnectionsTest")` (admin+operator)

- [ ] **Step 3: Gate Keys pages (PGP + SSH)**

Generate/Import → `can("PgpKeysManage")` / `can("SshKeysManage")`
Lifecycle buttons (Retire, Revoke, Activate) → same manage permission
Share buttons → `can("PgpKeysManageSharing")` / `can("SshKeysManageSharing")`

- [ ] **Step 4: Gate Chains, Monitors, Tags, NotificationRules pages**

Same pattern — wrap mutating buttons with the appropriate `can()` check.

- [ ] **Step 5: Gate Settings page**

Wrap auth settings update controls with `can("SettingsManage")`.

- [ ] **Step 6: Guard create/edit routes**

In `/jobs/new/page.tsx`, `/jobs/[id]/edit/page.tsx`, and similar pages, add early redirect:
```tsx
const { can } = usePermissions();
if (!can("JobsCreate")) { redirect("/jobs"); }
```

- [ ] **Step 7: Verify frontend builds**

Run: `cd src/Courier.Frontend && npx tsc --noEmit`
Expected: No type errors.

- [ ] **Step 8: Commit**

```
feat: Add frontend permission gating with usePermissions hook

Co-Authored-By: Claude Opus 4.6 (1M context) <noreply@anthropic.com>
```

---

## Chunk 8: Final Verification

### Task 25: Full Test Suite Verification

- [ ] **Step 1: Run all unit tests**

Run: `dotnet test tests/Courier.Tests.Unit -v minimal`
Expected: All tests pass (existing + new RolePermissions + PermissionHandler tests).

- [ ] **Step 2: Run all integration tests**

Run: `dotnet test tests/Courier.Tests.Integration -v minimal`
Expected: All tests pass (existing + new RBAC tests).

- [ ] **Step 3: Run architecture tests**

Run: `dotnet test tests/Courier.Tests.Architecture -v minimal`
Expected: All pass. Domain layer still has no external dependencies (Permission enum + RolePermissions use only BCL types).

- [ ] **Step 4: Verify no `[Authorize(Roles` remains**

Run: `grep -rn "Authorize(Roles" src/ --include="*.cs"`
Expected: No output.

- [ ] **Step 5: Verify frontend builds cleanly**

Run: `cd src/Courier.Frontend && npm run build`
Expected: Build succeeded.

- [ ] **Step 6: Final commit**

```
chore: Verify RBAC migration complete — all tests passing

Co-Authored-By: Claude Opus 4.6 (1M context) <noreply@anthropic.com>
```
