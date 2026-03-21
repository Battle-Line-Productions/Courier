# SSO & External Identity Provider Support — Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Enable Courier to authenticate users via external OIDC and SAML identity providers, with admin-managed provider configuration, automatic user provisioning, and optional role mapping.

**Architecture:** Server-side SSO flow — backend handles all IdP communication (redirects, callbacks, token/assertion validation). Frontend renders SSO login buttons and exchanges a one-time code for JWT tokens. Existing JWT token model is preserved unchanged.

**Tech Stack:** ASP.NET Core 10, ITfoxtec.Identity.Saml2, System.IdentityModel.Tokens.Jwt, IMemoryCache, Next.js 15, TanStack Query, shadcn/ui

**Spec:** `docs/superpowers/specs/2026-03-21-sso-external-identity-providers-design.md`

---

## File Structure

### New Files

**Domain Layer:**
- `src/Courier.Domain/Entities/SsoUserLink.cs` — SSO link entity (user ↔ provider mapping)

**Features Layer — Auth Providers CRUD:**
- `src/Courier.Features/AuthProviders/AuthProvidersController.cs` — CRUD + test connection + login-options
- `src/Courier.Features/AuthProviders/AuthProvidersService.cs` — Business logic, encryption, slugs
- `src/Courier.Features/AuthProviders/AuthProvidersDtos.cs` — Request/response DTOs
- `src/Courier.Features/AuthProviders/AuthProvidersValidator.cs` — FluentValidation

**Features Layer — SSO Flow:**
- `src/Courier.Features/Auth/Sso/SsoController.cs` — SSO endpoints (login, callbacks, exchange)
- `src/Courier.Features/Auth/Sso/SsoService.cs` — Orchestrates SSO flow, provisioning, role mapping
- `src/Courier.Features/Auth/Sso/OidcHandler.cs` — OIDC protocol (authorize URL, token exchange, ID token validation)
- `src/Courier.Features/Auth/Sso/SamlHandler.cs` — SAML protocol (AuthnRequest, assertion validation)
- `src/Courier.Features/Auth/Sso/SsoClaimsPrincipal.cs` — Normalized claims model
- `src/Courier.Features/Auth/Sso/SsoDtos.cs` — SSO endpoint DTOs
- `src/Courier.Features/Auth/Sso/SsoSettings.cs` — Configuration POCO for `Sso:` section

**Migration:**
- `src/Courier.Migrations/Scripts/0035_sso_external_providers.sql`

**Unit Tests:**
- `tests/Courier.Tests.Unit/AuthProviders/AuthProvidersServiceTests.cs`
- `tests/Courier.Tests.Unit/Sso/OidcHandlerTests.cs`
- `tests/Courier.Tests.Unit/Sso/SamlHandlerTests.cs`
- `tests/Courier.Tests.Unit/Sso/SsoServiceTests.cs`

**Integration Tests:**
- `tests/Courier.Tests.Integration/Rbac/AuthProvidersRbacTests.cs`

**Frontend:**
- `src/Courier.Frontend/src/lib/hooks/use-auth-providers.ts` — TanStack Query hooks
- `src/Courier.Frontend/src/app/(auth)/auth/callback/page.tsx` — SSO callback page
- `src/Courier.Frontend/src/app/(app)/settings/auth-providers/page.tsx` — Provider list
- `src/Courier.Frontend/src/app/(app)/settings/auth-providers/new/page.tsx` — Create provider
- `src/Courier.Frontend/src/app/(app)/settings/auth-providers/[id]/page.tsx` — Edit provider

### Modified Files

- `src/Courier.Domain/Entities/AuthProvider.cs` — Add new properties + soft delete
- `src/Courier.Domain/Enums/Permission.cs` — Add AuthProviders permissions
- `src/Courier.Domain/Authorization/RolePermissions.cs` — Map new permissions to roles
- `src/Courier.Domain/Common/ErrorCodes.cs` — Add SSO error codes (10020-10032)
- `src/Courier.Domain/Enums/AuditableEntityType.cs` — Add `AuthProvider`
- `src/Courier.Infrastructure/Data/CourierDbContext.cs` — Add DbSet + mappings + query filter
- `src/Courier.Features/FeaturesServiceExtensions.cs` — Register new services
- `src/Courier.Features/Auth/AuthService.cs` — Add `LoginViaSsoAsync`, update `ChangePasswordAsync`
- `src/Courier.Features/AuditLog/AuditService.cs` — Add `AuthProvider` to EntityTypeMap
- `src/Courier.Api/Program.cs` — Add `Sso` config section
- `src/Courier.Api/appsettings.json` — Add `Sso` placeholder
- `src/Courier.Frontend/src/lib/permissions.ts` — Add AuthProviders permissions
- `src/Courier.Frontend/src/lib/hooks/use-permissions.ts` — Update role maps
- `src/Courier.Frontend/src/lib/types.ts` — Add AuthProvider types
- `src/Courier.Frontend/src/lib/api.ts` — Add auth provider API methods
- `src/Courier.Frontend/src/app/(auth)/login/page.tsx` — Add SSO buttons
- Sidebar navigation component — Add Auth Providers link

---

## Chunk 1: Domain Foundation

### Task 1: Update AuthProvider Entity

**Files:**
- Modify: `src/Courier.Domain/Entities/AuthProvider.cs`

- [ ] **Step 1: Add new properties to AuthProvider**

The existing entity has: Id, Type, Name, IsEnabled, Configuration, AutoProvision, DefaultRole, CreatedAt, UpdatedAt, Users.

Add the new properties from the spec:

```csharp
public class AuthProvider
{
    public Guid Id { get; set; }
    public string Type { get; set; } = string.Empty; // "oidc" or "saml"
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public bool IsEnabled { get; set; }
    public string Configuration { get; set; } = "{}"; // JSONB - encrypted clientSecret inside
    public bool AutoProvision { get; set; } = true;
    public string DefaultRole { get; set; } = "viewer";
    public bool AllowLocalPassword { get; set; }
    public string RoleMapping { get; set; } = "{}"; // JSONB
    public int DisplayOrder { get; set; }
    public string? IconUrl { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }

    // Navigation
    [Obsolete("Use SsoUserLinks instead")]
    public List<User> Users { get; set; } = [];
    public List<SsoUserLink> SsoUserLinks { get; set; } = [];
}
```

- [ ] **Step 2: Add `SsoUserLinks` navigation to User entity**

In `src/Courier.Domain/Entities/User.cs`, add a navigation collection:

```csharp
    public List<SsoUserLink> SsoUserLinks { get; set; } = [];
```

- [ ] **Step 3: Verify build**

Run: `dotnet build src/Courier.Domain`
Expected: SUCCESS (no consumers of new properties yet)

### Task 2: Create SsoUserLink Entity

**Files:**
- Create: `src/Courier.Domain/Entities/SsoUserLink.cs`

- [ ] **Step 1: Create the entity**

```csharp
namespace Courier.Domain.Entities;

public class SsoUserLink
{
    public Guid Id { get; set; }
    public Guid UserId { get; set; }
    public Guid ProviderId { get; set; }
    public string SubjectId { get; set; } = string.Empty;
    public string? Email { get; set; }
    public DateTime LinkedAt { get; set; }
    public DateTime? LastLoginAt { get; set; }

    // Navigation
    public User User { get; set; } = null!;
    public AuthProvider Provider { get; set; } = null!;
}
```

- [ ] **Step 2: Verify build**

Run: `dotnet build src/Courier.Domain`
Expected: SUCCESS

### Task 3: Add SSO Error Codes

**Files:**
- Modify: `src/Courier.Domain/Common/ErrorCodes.cs`

- [ ] **Step 1: Add SSO error codes after the existing auth codes (after line with `UserNotFound = 10014`)**

```csharp
    // SSO (10020-10099)
    public const int SsoNotConfigured = 10020;
    public const int SsoProviderNotFound = 10021;
    public const int SsoProviderDisabled = 10022;
    public const int SsoStateMismatch = 10023;
    public const int SsoExchangeCodeInvalid = 10024;
    public const int SsoLocalPasswordNotAllowed = 10025;
    public const int SsoEmailCollision = 10026;
    public const int SsoAutoProvisionDisabled = 10027;
    public const int SsoIdTokenValidationFailed = 10028;
    public const int SsoSamlValidationFailed = 10029;
    public const int SsoSamlReplayDetected = 10030;
    public const int SsoTestConnectionFailed = 10031;
    public const int SsoClaimMappingFailed = 10032;
```

- [ ] **Step 2: Verify build**

Run: `dotnet build src/Courier.Domain`

### Task 4: Add AuthProviders Permissions

**Files:**
- Modify: `src/Courier.Domain/Enums/Permission.cs`
- Modify: `src/Courier.Domain/Authorization/RolePermissions.cs`

- [ ] **Step 1: Add permissions to the enum**

Add after the `KnownHostsManage` entry (end of the enum, before the closing brace):

```csharp
    // Auth Providers
    AuthProvidersView,
    AuthProvidersCreate,
    AuthProvidersEdit,
    AuthProvidersDelete,
```

- [ ] **Step 2: Update RolePermissions**

In `RolePermissions.cs`, the Admin role uses `Enum.GetValues<Permission>().ToFrozenSet()` so it auto-includes new values. For Operator, add `AuthProvidersView` to the operator set. Viewer gets none of the AuthProviders permissions.

Find the Operator permission list and add `Permission.AuthProvidersView` to it.

- [ ] **Step 3: Run existing RBAC unit tests**

Run: `dotnet test tests/Courier.Tests.Unit --filter "FullyQualifiedName~RolePermissions"`
Expected: All existing tests pass. The `AdminHasAllPermissions` test should still pass since it uses `Enum.GetValues`.

- [ ] **Step 4: Commit**

```
feat(domain): add SSO foundation - AuthProvider entity, SsoUserLink, error codes, permissions
```

### Task 5: Add AuditableEntityType

**Files:**
- Modify: `src/Courier.Domain/Enums/AuditableEntityType.cs`

- [ ] **Step 1: Add `AuthProvider` to the enum**

Add after `KnownHost`:

```csharp
    AuthProvider
```

- [ ] **Step 2: Verify build**

Run: `dotnet build src/Courier.Domain`

### Task 6: Database Migration

**Files:**
- Create: `src/Courier.Migrations/Scripts/0035_sso_external_providers.sql`

- [ ] **Step 1: Write migration script**

```sql
-- SSO External Identity Providers
-- Enhances auth_providers table and creates sso_user_links

-- Add new columns to auth_providers
ALTER TABLE auth_providers
  ADD COLUMN IF NOT EXISTS slug TEXT,
  ADD COLUMN IF NOT EXISTS allow_local_password BOOLEAN DEFAULT FALSE,
  ADD COLUMN IF NOT EXISTS role_mapping JSONB DEFAULT '{}',
  ADD COLUMN IF NOT EXISTS display_order INT DEFAULT 0,
  ADD COLUMN IF NOT EXISTS icon_url TEXT,
  ADD COLUMN IF NOT EXISTS is_deleted BOOLEAN NOT NULL DEFAULT FALSE,
  ADD COLUMN IF NOT EXISTS deleted_at TIMESTAMPTZ;

-- Generate slugs for any existing providers
UPDATE auth_providers
SET slug = TRIM(BOTH '-' FROM REGEXP_REPLACE(
    LOWER(REGEXP_REPLACE(name, '[^a-zA-Z0-9]+', '-', 'g')),
    '-{2,}', '-', 'g'))
WHERE slug IS NULL;

-- Handle edge case: empty slug after sanitization
UPDATE auth_providers SET slug = 'provider-' || LEFT(id::text, 8) WHERE slug = '' OR slug IS NULL;

ALTER TABLE auth_providers ALTER COLUMN slug SET NOT NULL;
ALTER TABLE auth_providers ADD CONSTRAINT uq_auth_providers_slug UNIQUE (slug);

-- SSO user links table
CREATE TABLE sso_user_links (
  id              UUID PRIMARY KEY DEFAULT gen_random_uuid(),
  user_id         UUID NOT NULL REFERENCES users(id) ON DELETE CASCADE,
  provider_id     UUID NOT NULL REFERENCES auth_providers(id) ON DELETE RESTRICT,
  subject_id      TEXT NOT NULL,
  email           TEXT,
  linked_at       TIMESTAMPTZ NOT NULL DEFAULT now(),
  last_login_at   TIMESTAMPTZ,
  CONSTRAINT uq_sso_user_links_provider_subject UNIQUE (provider_id, subject_id)
);

CREATE INDEX ix_sso_user_links_user_id ON sso_user_links (user_id);
CREATE INDEX ix_sso_user_links_provider_id ON sso_user_links (provider_id);

-- Update audit log CHECK constraint to include auth_provider
ALTER TABLE audit_log_entries DROP CONSTRAINT IF EXISTS ck_audit_entity_type;
ALTER TABLE audit_log_entries ADD CONSTRAINT ck_audit_entity_type
  CHECK (entity_type IN (
    'job', 'job_execution', 'step_execution', 'connection',
    'pgp_key', 'ssh_key', 'file_monitor', 'tag', 'chain',
    'chain_execution', 'notification_rule', 'user', 'known_host',
    'auth_provider'
  ));
```

- [ ] **Step 2: Verify migration compiles**

Run: `dotnet build src/Courier.Migrations`

- [ ] **Step 3: Commit**

```
feat(db): add migration 0035 for SSO external providers
```

### Task 7: DbContext Changes

**Files:**
- Modify: `src/Courier.Infrastructure/Data/CourierDbContext.cs`

- [ ] **Step 1: Add DbSet for SsoUserLink**

Add near the other DbSet declarations:

```csharp
public DbSet<SsoUserLink> SsoUserLinks => Set<SsoUserLink>();
```

- [ ] **Step 2: Add entity mapping for SsoUserLink in OnModelCreating**

Add in the `OnModelCreating` method, following existing patterns:

```csharp
modelBuilder.Entity<SsoUserLink>(entity =>
{
    entity.ToTable("sso_user_links");
    entity.HasKey(e => e.Id);
    entity.Property(e => e.Id).HasColumnName("id");
    entity.Property(e => e.UserId).HasColumnName("user_id");
    entity.Property(e => e.ProviderId).HasColumnName("provider_id");
    entity.Property(e => e.SubjectId).HasColumnName("subject_id");
    entity.Property(e => e.Email).HasColumnName("email");
    entity.Property(e => e.LinkedAt).HasColumnName("linked_at");
    entity.Property(e => e.LastLoginAt).HasColumnName("last_login_at");

    entity.HasOne(e => e.User)
        .WithMany(u => u.SsoUserLinks)
        .HasForeignKey(e => e.UserId)
        .OnDelete(DeleteBehavior.Cascade);

    entity.HasOne(e => e.Provider)
        .WithMany(p => p.SsoUserLinks)
        .HasForeignKey(e => e.ProviderId)
        .OnDelete(DeleteBehavior.Restrict);
});
```

- [ ] **Step 3: Update AuthProvider mapping for new columns**

Find the existing `modelBuilder.Entity<AuthProvider>` block and add mappings for the new columns:

```csharp
entity.Property(e => e.Slug).HasColumnName("slug");
entity.Property(e => e.AllowLocalPassword).HasColumnName("allow_local_password");
entity.Property(e => e.RoleMapping).HasColumnName("role_mapping");
entity.Property(e => e.DisplayOrder).HasColumnName("display_order");
entity.Property(e => e.IconUrl).HasColumnName("icon_url");
entity.Property(e => e.IsDeleted).HasColumnName("is_deleted");
entity.Property(e => e.DeletedAt).HasColumnName("deleted_at");
```

- [ ] **Step 4: Add soft delete query filter for AuthProvider**

In the AuthProvider entity mapping, add:

```csharp
entity.HasQueryFilter(e => !e.IsDeleted);
```

- [ ] **Step 5: Update AuditService EntityTypeMap**

In `src/Courier.Features/AuditLog/AuditService.cs`, find the `EntityTypeMap` dictionary and add:

```csharp
{ AuditableEntityType.AuthProvider, "auth_provider" },
```

- [ ] **Step 6: Run full build**

Run: `dotnet build Courier.slnx`
Expected: SUCCESS

- [ ] **Step 7: Commit**

```
feat(infra): add DbContext mappings for SSO entities and audit support
```

---

## Chunk 2: Auth Providers CRUD

### Task 8: Auth Providers DTOs

**Files:**
- Create: `src/Courier.Features/AuthProviders/AuthProvidersDtos.cs`

- [ ] **Step 1: Create DTOs**

```csharp
using System.Text.Json;

namespace Courier.Features.AuthProviders;

// --- Response DTOs ---

public record AuthProviderDto(
    Guid Id,
    string Type,
    string Name,
    string Slug,
    bool IsEnabled,
    JsonElement Configuration, // clientSecret redacted
    bool AutoProvision,
    string DefaultRole,
    bool AllowLocalPassword,
    JsonElement? RoleMapping,
    int DisplayOrder,
    string? IconUrl,
    int LinkedUserCount,
    DateTime CreatedAt,
    DateTime UpdatedAt);

/// <summary>
/// Minimal DTO for the public login-options endpoint (no secrets, no admin details).
/// </summary>
public record LoginOptionDto(
    Guid Id,
    string Type,
    string Name,
    string Slug,
    string? IconUrl);

public record TestConnectionResultDto(
    bool Success,
    string Message,
    JsonElement? Details);

// --- Request DTOs ---

public record CreateAuthProviderRequest(
    string Type,
    string Name,
    bool IsEnabled,
    JsonElement Configuration,
    bool AutoProvision,
    string DefaultRole,
    bool AllowLocalPassword,
    JsonElement? RoleMapping,
    int DisplayOrder,
    string? IconUrl);

public record UpdateAuthProviderRequest(
    string? Name,
    bool? IsEnabled,
    JsonElement? Configuration,
    bool? AutoProvision,
    string? DefaultRole,
    bool? AllowLocalPassword,
    JsonElement? RoleMapping,
    int? DisplayOrder,
    string? IconUrl);
```

- [ ] **Step 2: Verify build**

Run: `dotnet build src/Courier.Features`

### Task 9: Auth Providers Validator

**Files:**
- Create: `src/Courier.Features/AuthProviders/AuthProvidersValidator.cs`

- [ ] **Step 1: Create validators**

```csharp
using FluentValidation;

namespace Courier.Features.AuthProviders;

public class CreateAuthProviderValidator : AbstractValidator<CreateAuthProviderRequest>
{
    public CreateAuthProviderValidator()
    {
        RuleFor(x => x.Type)
            .NotEmpty()
            .Must(t => t is "oidc" or "saml")
            .WithMessage("Type must be 'oidc' or 'saml'.");

        RuleFor(x => x.Name)
            .NotEmpty()
            .MaximumLength(200);

        RuleFor(x => x.DefaultRole)
            .Must(r => r is "admin" or "operator" or "viewer")
            .WithMessage("DefaultRole must be 'admin', 'operator', or 'viewer'.");

        RuleFor(x => x.Configuration).NotEmpty();
    }
}

public class UpdateAuthProviderValidator : AbstractValidator<UpdateAuthProviderRequest>
{
    public UpdateAuthProviderValidator()
    {
        RuleFor(x => x.Name)
            .MaximumLength(200)
            .When(x => x.Name is not null);

        RuleFor(x => x.DefaultRole)
            .Must(r => r is "admin" or "operator" or "viewer")
            .WithMessage("DefaultRole must be 'admin', 'operator', or 'viewer'.")
            .When(x => x.DefaultRole is not null);
    }
}
```

- [ ] **Step 2: Verify build**

Run: `dotnet build src/Courier.Features`

### Task 10: Auth Providers Service

**Files:**
- Create: `src/Courier.Features/AuthProviders/AuthProvidersService.cs`

- [ ] **Step 1: Create the service**

Follow the existing service pattern (concrete class, Scoped, injects CourierDbContext, returns ApiResponse<T>). Key responsibilities:
- CRUD operations with slug generation
- Encrypt `clientSecret` on write using `ICredentialEncryptor`
- Redact `clientSecret` on read (replace with `"••••••••"`)
- Preserve existing `clientSecret` when update doesn't provide new one
- Soft delete
- Test connection (OIDC discovery fetch / SAML cert validation)

The service should:
- Constructor inject: `CourierDbContext db`, `ICredentialEncryptor encryptor`, `AuditService audit`, `IHttpClientFactory httpClientFactory`
- Have methods: `ListAsync`, `GetByIdAsync`, `CreateAsync`, `UpdateAsync`, `DeleteAsync`, `TestConnectionAsync`, `GetLoginOptionsAsync`
- Use private `GenerateSlug(string name)` method: lowercase → replace non-alphanumeric with hyphens → collapse multiples → trim → handle collisions
- Use private `RedactConfiguration(string configJson)` to replace clientSecret with placeholder
- Use private `EncryptConfiguration(string configJson)` and `DecryptConfiguration(string configJson)` for secret handling
- Use private `MapToDto(AuthProvider entity, int linkedUserCount)` static method

**Audit logging:** Call `_audit.LogAsync(AuditableEntityType.AuthProvider, entity.Id, ...)` in:
- `CreateAsync` → event `auth_provider_created`
- `UpdateAsync` → event `auth_provider_updated`
- `DeleteAsync` → event `auth_provider_deleted`

The configuration encryption pattern:
1. Parse JSON string with `JsonDocument`
2. Find `clientSecret` property
3. Encrypt its value with `_encryptor.Encrypt(secret)` → base64-encode the bytes
4. Rebuild JSON with encrypted value
5. On read: reverse (decrypt base64 bytes with `_encryptor.Decrypt`)
6. On API response: replace with `"••••••••"`

For TestConnection:
- OIDC: `HttpClient.GetAsync("{authority}/.well-known/openid-configuration")` — parse JSON, extract `jwks_uri`, fetch that too
- SAML: Parse base64 X.509 cert, check expiry, optionally HEAD the `ssoUrl`

- [ ] **Step 2: Write unit tests**

Create: `tests/Courier.Tests.Unit/AuthProviders/AuthProvidersServiceTests.cs`

Test cases (follow InMemory EF pattern from existing tests):
- Create provider → slug generated correctly
- Create provider → clientSecret encrypted in DB
- Get provider → clientSecret redacted in response
- Update provider with null clientSecret → preserves existing encrypted value
- Update provider with new clientSecret → re-encrypts
- Delete provider → soft delete (IsDeleted=true, DeletedAt set)
- Slug collision → appends `-2`, `-3`
- Slug from special-chars-only name → falls back to `"provider-{guid}"`
- List returns only non-deleted providers
- Login options returns only enabled, non-deleted providers (id, name, slug, type, iconUrl)

- [ ] **Step 3: Run tests**

Run: `dotnet test tests/Courier.Tests.Unit --filter "FullyQualifiedName~AuthProvidersService"`
Expected: All pass

- [ ] **Step 4: Commit**

```
feat(auth-providers): add CRUD service with encryption and slug generation
```

### Task 11: Auth Providers Controller

**Files:**
- Create: `src/Courier.Features/AuthProviders/AuthProvidersController.cs`

- [ ] **Step 1: Create the controller**

Follow the `ConnectionsController` pattern exactly:

```csharp
using Courier.Domain.Authorization;
using Courier.Domain.Common;
using Courier.Domain.Enums;
using Courier.Features.Security;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Courier.Features.AuthProviders;

[ApiController]
[Route("api/v1/auth-providers")]
[Authorize]
public class AuthProvidersController : ControllerBase
{
    private readonly AuthProvidersService _service;
    private readonly IValidator<CreateAuthProviderRequest> _createValidator;

    public AuthProvidersController(
        AuthProvidersService service,
        IValidator<CreateAuthProviderRequest> createValidator)
    {
        _service = service;
        _createValidator = createValidator;
    }

    [HttpGet]
    [RequirePermission(Permission.AuthProvidersView)]
    public async Task<ActionResult<PagedApiResponse<AuthProviderDto>>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken ct = default)
    {
        var result = await _service.ListAsync(page, pageSize, ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [RequirePermission(Permission.AuthProvidersView)]
    public async Task<ActionResult<ApiResponse<AuthProviderDto>>> GetById(
        Guid id, CancellationToken ct)
    {
        var result = await _service.GetByIdAsync(id, ct);
        if (!result.Success) return NotFound(result);
        return Ok(result);
    }

    [HttpPost]
    [RequirePermission(Permission.AuthProvidersCreate)]
    public async Task<ActionResult<ApiResponse<AuthProviderDto>>> Create(
        [FromBody] CreateAuthProviderRequest request, CancellationToken ct)
    {
        var validation = await _createValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            var details = validation.Errors
                .Select(e => new FieldError(e.PropertyName, e.ErrorMessage))
                .ToList();
            return BadRequest(new ApiResponse<AuthProviderDto>
            {
                Error = ErrorMessages.Create(ErrorCodes.ValidationFailed, "Validation failed.", details)
            });
        }

        var result = await _service.CreateAsync(request, ct);
        return Created($"/api/v1/auth-providers/{result.Data!.Id}", result);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(Permission.AuthProvidersEdit)]
    public async Task<ActionResult<ApiResponse<AuthProviderDto>>> Update(
        Guid id,
        [FromBody] UpdateAuthProviderRequest request,
        [FromServices] IValidator<UpdateAuthProviderRequest> validator,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            var details = validation.Errors
                .Select(e => new FieldError(e.PropertyName, e.ErrorMessage))
                .ToList();
            return BadRequest(new ApiResponse<AuthProviderDto>
            {
                Error = ErrorMessages.Create(ErrorCodes.ValidationFailed, "Validation failed.", details)
            });
        }

        var result = await _service.UpdateAsync(id, request, ct);
        if (!result.Success)
        {
            return result.Error!.Code switch
            {
                ErrorCodes.ResourceNotFound => NotFound(result),
                _ => StatusCode(500, result)
            };
        }
        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    [RequirePermission(Permission.AuthProvidersDelete)]
    public async Task<ActionResult<ApiResponse>> Delete(Guid id, CancellationToken ct)
    {
        var result = await _service.DeleteAsync(id, ct);
        if (!result.Success)
        {
            return result.Error!.Code switch
            {
                ErrorCodes.ResourceNotFound => NotFound(result),
                _ => StatusCode(500, result)
            };
        }
        return Ok(result);
    }

    [HttpPost("{id:guid}/test")]
    [RequirePermission(Permission.AuthProvidersEdit)]
    public async Task<ActionResult<ApiResponse<TestConnectionResultDto>>> TestConnection(
        Guid id, CancellationToken ct)
    {
        var result = await _service.TestConnectionAsync(id, ct);
        if (!result.Success)
        {
            return result.Error!.Code switch
            {
                ErrorCodes.ResourceNotFound => NotFound(result),
                _ => BadRequest(result)
            };
        }
        return Ok(result);
    }

    /// <summary>
    /// Public endpoint — returns enabled providers for the login page.
    /// </summary>
    [HttpGet("login-options")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<List<LoginOptionDto>>>> GetLoginOptions(
        CancellationToken ct)
    {
        var result = await _service.GetLoginOptionsAsync(ct);
        return Ok(result);
    }
}
```

- [ ] **Step 2: Register services in FeaturesServiceExtensions**

In `src/Courier.Features/FeaturesServiceExtensions.cs`, in the `AddCourierFeatures` method, add after the existing Auth section:

```csharp
        // Auth Providers
        services.AddScoped<AuthProvidersService>();
        services.AddHttpClient("SsoDiscovery");
```

- [ ] **Step 3: Verify build**

Run: `dotnet build Courier.slnx`

- [ ] **Step 4: Commit**

```
feat(auth-providers): add controller with CRUD, test connection, and login-options endpoints
```

### Task 12: RBAC Integration Tests for Auth Providers

**Files:**
- Create: `tests/Courier.Tests.Integration/Rbac/AuthProvidersRbacTests.cs`

- [ ] **Step 1: Create RBAC tests**

Follow the `ConnectionsRbacTests` pattern exactly:

```csharp
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Shouldly;

namespace Courier.Tests.Integration.Rbac;

public class AuthProvidersRbacTests(RbacFixture fixture) : RbacTestBase(fixture)
{
    private static void AssertAuthorized(HttpResponseMessage response)
    {
        response.StatusCode.ShouldNotBe(HttpStatusCode.Forbidden,
            "Expected authorized access but got Forbidden");
    }

    // --- Login Options (anonymous allowed) ---

    [Theory]
    [InlineData("admin")]
    [InlineData("operator")]
    [InlineData("viewer")]
    [InlineData("anonymous")]
    public async Task GetLoginOptions_AllRolesAndAnonymousAllowed(string role)
    {
        var response = await ClientForRole(role).GetAsync("api/v1/auth-providers/login-options");
        AssertAuthorized(response);
    }

    // --- View (admin + operator) ---

    [Theory]
    [InlineData("admin")]
    [InlineData("operator")]
    public async Task ListAuthProviders_AdminAndOperator_Allowed(string role)
    {
        var response = await ClientForRole(role).GetAsync("api/v1/auth-providers");
        AssertAuthorized(response);
    }

    [Fact]
    public async Task ListAuthProviders_Viewer_Forbidden()
    {
        var response = await Fixture.ViewerClient.GetAsync("api/v1/auth-providers");
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task ListAuthProviders_Anonymous_Unauthorized()
    {
        var response = await Fixture.AnonymousClient.GetAsync("api/v1/auth-providers");
        response.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    // --- Create (admin only) ---

    [Fact]
    public async Task CreateAuthProvider_Admin_NotForbidden()
    {
        var suffix = Guid.NewGuid().ToString("N")[..8];
        var response = await Fixture.AdminClient.PostAsJsonAsync("api/v1/auth-providers", new
        {
            type = "oidc",
            name = $"rbac-provider-{suffix}",
            isEnabled = false,
            configuration = new { authority = "https://test.example.com", clientId = "test", clientSecret = "test" },
            autoProvision = true,
            defaultRole = "viewer",
            allowLocalPassword = false,
            displayOrder = 0,
        });
        AssertAuthorized(response);

        // Clean up
        if (response.IsSuccessStatusCode)
        {
            var result = await response.Content.ReadFromJsonAsync<JsonElement>();
            if (result.TryGetProperty("data", out var data) && data.TryGetProperty("id", out var id))
            {
                await Fixture.AdminClient.DeleteAsync($"api/v1/auth-providers/{id.GetString()}");
            }
        }
    }

    [Theory]
    [InlineData("operator")]
    [InlineData("viewer")]
    public async Task CreateAuthProvider_NonAdmin_Forbidden(string role)
    {
        var response = await ClientForRole(role).PostAsJsonAsync("api/v1/auth-providers", new
        {
            type = "oidc",
            name = "rbac-should-fail",
            isEnabled = false,
            configuration = new { authority = "https://test.example.com", clientId = "test" },
            autoProvision = true,
            defaultRole = "viewer",
            allowLocalPassword = false,
            displayOrder = 0,
        });
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // --- Update (admin only) ---

    [Theory]
    [InlineData("operator")]
    [InlineData("viewer")]
    public async Task UpdateAuthProvider_NonAdmin_Forbidden(string role)
    {
        var fakeId = Guid.NewGuid();
        var response = await ClientForRole(role).PutAsJsonAsync($"api/v1/auth-providers/{fakeId}", new
        {
            name = "updated-name",
        });
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // --- Delete (admin only) ---

    [Theory]
    [InlineData("operator")]
    [InlineData("viewer")]
    public async Task DeleteAuthProvider_NonAdmin_Forbidden(string role)
    {
        var fakeId = Guid.NewGuid();
        var response = await ClientForRole(role).DeleteAsync($"api/v1/auth-providers/{fakeId}");
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // --- Test Connection (admin only, uses AuthProvidersEdit) ---

    [Theory]
    [InlineData("operator")]
    [InlineData("viewer")]
    public async Task TestConnection_NonAdmin_Forbidden(string role)
    {
        var fakeId = Guid.NewGuid();
        var response = await ClientForRole(role).PostAsync($"api/v1/auth-providers/{fakeId}/test", null);
        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }
}
```

- [ ] **Step 2: Run RBAC tests**

Run: `dotnet test tests/Courier.Tests.Integration --filter "FullyQualifiedName~AuthProvidersRbac"`
Expected: All pass (requires Docker for Testcontainers)

- [ ] **Step 3: Commit**

```
test(rbac): add auth providers RBAC integration tests
```

---

## Chunk 3: SSO Protocol Handlers

### Task 13: SSO Settings and Claims Model

**Files:**
- Create: `src/Courier.Features/Auth/Sso/SsoSettings.cs`
- Create: `src/Courier.Features/Auth/Sso/SsoClaimsPrincipal.cs`

- [ ] **Step 1: Create SsoSettings**

```csharp
namespace Courier.Features.Auth.Sso;

public class SsoSettings
{
    public string? FrontendCallbackUrl { get; set; }
    public string? ApiBaseUrl { get; set; }
}
```

- [ ] **Step 2: Create SsoClaimsPrincipal**

```csharp
namespace Courier.Features.Auth.Sso;

/// <summary>
/// Normalized claims extracted from either OIDC or SAML identity providers.
/// </summary>
public record SsoClaimsPrincipal(
    string SubjectId,
    string? Email,
    string? DisplayName,
    string[] Groups);
```

- [ ] **Step 3: Add config section to appsettings.json and Program.cs**

In `src/Courier.Api/appsettings.json`, add:
```json
"Sso": {
    "FrontendCallbackUrl": "",
    "ApiBaseUrl": ""
}
```

In `src/Courier.Api/Program.cs`, add with other `Configure<>` calls:
```csharp
builder.Services.Configure<SsoSettings>(builder.Configuration.GetSection("Sso"));
```

- [ ] **Step 4: Verify build**

Run: `dotnet build Courier.slnx`

### Task 14: OIDC Handler

**Files:**
- Create: `src/Courier.Features/Auth/Sso/OidcHandler.cs`
- Create: `tests/Courier.Tests.Unit/Sso/OidcHandlerTests.cs`

- [ ] **Step 1: Create OidcHandler**

Responsibilities:
- `BuildAuthorizationUrl(providerConfig, redirectUri, state, codeChallenge)` → returns URL string
- `ExchangeCodeAsync(providerConfig, code, redirectUri, codeVerifier)` → returns ID token claims
- `ValidateIdTokenAsync(idToken, providerConfig)` → validates signature via JWKS, returns claims

Uses `IHttpClientFactory` for HTTP calls. Uses `IMemoryCache` for OIDC discovery document caching (24h) and JWKS caching (1h).

Key implementation details:
- Discovery doc URL: `{authority}/.well-known/openid-configuration`
- Token exchange: POST to `token_endpoint` with `grant_type=authorization_code`, `code`, `redirect_uri`, `client_id`, `client_secret`, `code_verifier`
- ID token validation: fetch JWKS from `jwks_uri`, validate with `TokenValidationParameters` (issuer, audience=clientId, signing keys from JWKS)
- Extract claims → `SsoClaimsPrincipal`

- [ ] **Step 2: Write unit tests**

Test cases:
- `BuildAuthorizationUrl_IncludesAllRequiredParameters` — verify URL has response_type, client_id, redirect_uri, scope, state, code_challenge, code_challenge_method
- `BuildAuthorizationUrl_AppendsScopesFromConfig`
- `ExchangeCodeAsync_ValidResponse_ExtractsClaims` (mock HttpClient)
- `ExchangeCodeAsync_ErrorResponse_ThrowsWithMessage`
- `ValidateIdTokenAsync_ExpiredToken_Throws`
- `ValidateIdTokenAsync_WrongAudience_Throws`

- [ ] **Step 3: Run tests**

Run: `dotnet test tests/Courier.Tests.Unit --filter "FullyQualifiedName~OidcHandler"`

- [ ] **Step 4: Commit**

```
feat(sso): add OIDC protocol handler with discovery caching and token validation
```

### Task 15: SAML Handler

**Files:**
- Create: `src/Courier.Features/Auth/Sso/SamlHandler.cs`
- Create: `tests/Courier.Tests.Unit/Sso/SamlHandlerTests.cs`

- [ ] **Step 1: Add ITfoxtec.Identity.Saml2 NuGet package**

Run: `dotnet add src/Courier.Features/Courier.Features.csproj package ITfoxtec.Identity.Saml2`

- [ ] **Step 2: Create SamlHandler**

Responsibilities:
- `BuildAuthnRequestUrl(providerConfig, relayState)` → returns redirect URL with SAMLRequest
- `ValidateResponseAsync(samlResponseBase64, providerConfig)` → validates signature + conditions, returns `SsoClaimsPrincipal`

Uses `ITfoxtec.Identity.Saml2` for XML signing/validation. Uses `IMemoryCache` for SAML assertion ID replay detection (5-minute window).

Key implementation details:
- AuthnRequest: set Issuer=entityId, Destination=ssoUrl, AssertionConsumerServiceURL=callback URL
- Response validation: parse XML, verify signature against X.509 cert from config, check audience, check NotBefore/NotOnOrAfter with 30s clock skew, check replay via assertion ID
- Extract attributes using configured `attributeMappings` → `SsoClaimsPrincipal`

- [ ] **Step 3: Write unit tests**

Test cases:
- `BuildAuthnRequestUrl_ContainsIssuerAndDestination`
- `ValidateResponseAsync_ValidSignedAssertion_ExtractsClaims` (create test assertion with test cert)
- `ValidateResponseAsync_InvalidSignature_Throws`
- `ValidateResponseAsync_ExpiredAssertion_Throws`
- `ValidateResponseAsync_WrongAudience_Throws`
- `ValidateResponseAsync_ReplayedAssertionId_Throws`

- [ ] **Step 4: Run tests**

Run: `dotnet test tests/Courier.Tests.Unit --filter "FullyQualifiedName~SamlHandler"`

- [ ] **Step 5: Commit**

```
feat(sso): add SAML protocol handler with signature validation and replay detection
```

---

## Chunk 4: SSO Flow Orchestration

### Task 16: SSO DTOs

**Files:**
- Create: `src/Courier.Features/Auth/Sso/SsoDtos.cs`

- [ ] **Step 1: Create DTOs**

```csharp
namespace Courier.Features.Auth.Sso;

public record SsoExchangeRequest(string Code);

public record SsoExchangeCodeData(Guid UserId, Guid ProviderId, DateTime CreatedAt);
```

### Task 17: SSO Service

**Files:**
- Create: `src/Courier.Features/Auth/Sso/SsoService.cs`
- Create: `tests/Courier.Tests.Unit/Sso/SsoServiceTests.cs`

- [ ] **Step 1: Create SsoService**

Constructor injects: `CourierDbContext`, `OidcHandler`, `SamlHandler`, `AuthService`, `AuditService`, `IMemoryCache`, `IOptions<SsoSettings>`, `ICredentialEncryptor`

Methods:
- `InitiateLoginAsync(Guid providerId)` → loads provider, validates enabled, builds redirect URL (delegates to OidcHandler or SamlHandler based on Type), returns `{ RedirectUrl, State, ProviderId }`
- `HandleOidcCallbackAsync(string code, string state, string codeVerifier, Guid providerId)` → decrypts provider config, delegates to OidcHandler, then calls `ProvisionOrLinkUserAsync`
- `HandleSamlCallbackAsync(string samlResponse, Guid providerId)` → decrypts provider config, delegates to SamlHandler, then calls `ProvisionOrLinkUserAsync`
- `ProvisionOrLinkUserAsync(SsoClaimsPrincipal claims, AuthProvider provider)` → implements §3 of spec (lookup link → check account status → provision new user or update existing)
- `ApplyRoleMappingAsync(SsoClaimsPrincipal claims, AuthProvider provider, User user)` → implements §4 of spec (evaluate rules, update role if changed)
- `CreateExchangeCodeAsync(Guid userId, Guid providerId)` → generates 32 random bytes, stores in IMemoryCache with 60s TTL, returns code string
- `ExchangeCodeAsync(string code)` → looks up and removes from cache, delegates to `AuthService.LoginViaSsoAsync(userId)`

**Audit logging (required by spec §5.8):** Call `_audit.LogAsync(...)` in:
- `HandleOidcCallbackAsync` / `HandleSamlCallbackAsync`: log `sso_login_success` (entityType=User) on success, `sso_login_failed` (entityType=AuthProvider) on failure
- `ProvisionOrLinkUserAsync`: log `sso_user_provisioned` (new user) or `sso_user_linked` (existing user linked)
- `ApplyRoleMappingAsync`: log `sso_role_updated` when role actually changes (include oldRole and newRole in details)

**Login options ordering:** In `GetLoginOptionsAsync`, query must include `.OrderBy(p => p.DisplayOrder)` to match spec requirement.

- [ ] **Step 2: Update AuthService with SSO login support**

In `src/Courier.Features/Auth/AuthService.cs`, add a `LoginViaSsoAsync` method that mirrors the existing `LoginAsync` pattern exactly (use `_jwt` field name, `GenerateAccessTokenAsync`, `JwtTokenService.GenerateRefreshToken()` static method, manually create `RefreshToken` entity, fetch timeout settings from `_settings`):

```csharp
public async Task<ApiResponse<LoginResponse>> LoginViaSsoAsync(Guid userId, string ipAddress, CancellationToken ct)
{
    var user = await _db.Users.FindAsync([userId], ct);
    if (user is null)
        return new ApiResponse<LoginResponse> { Error = ErrorMessages.Create(ErrorCodes.UserNotFound, "User not found.") };

    var accessToken = await _jwt.GenerateAccessTokenAsync(user, ct);
    var refreshTokenString = JwtTokenService.GenerateRefreshToken();

    // Fetch settings the same way LoginAsync does
    var refreshDays = int.Parse(await _settings.GetValueAsync("auth.refresh_token_days", ct) ?? "7");
    var sessionMinutes = int.Parse(await _settings.GetValueAsync("auth.session_timeout_minutes", ct) ?? "15");

    var refreshToken = new RefreshToken
    {
        Id = Guid.CreateVersion7(),
        UserId = user.Id,
        TokenHash = JwtTokenService.HashToken(refreshTokenString),
        ExpiresAt = DateTime.UtcNow.AddDays(refreshDays),
        CreatedByIp = ipAddress,
    };
    _db.RefreshTokens.Add(refreshToken);

    user.LastLoginAt = DateTime.UtcNow;
    await _db.SaveChangesAsync(ct);

    return new ApiResponse<LoginResponse>
    {
        Data = new LoginResponse
        {
            AccessToken = accessToken,
            RefreshToken = refreshTokenString,
            ExpiresIn = sessionMinutes * 60,
            User = new UserProfileDto
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email,
                DisplayName = user.DisplayName,
                Role = user.Role,
            },
        }
    };
}
```

Also update `ChangePasswordAsync` to check `allow_local_password`. **Important:** This check must go BEFORE the password verification block — an SSO user with no password hash would otherwise get `InvalidCurrentPassword` instead of the SSO-specific error:

```csharp
// Insert BEFORE the existing password hash verification, after the user null check:
if (user.IsSsoUser)
{
    var ssoLink = await _db.SsoUserLinks
        .Include(l => l.Provider)
        .FirstOrDefaultAsync(l => l.UserId == user.Id, ct);

    if (ssoLink?.Provider is { AllowLocalPassword: false })
    {
        return new ApiResponse { Error = ErrorMessages.Create(ErrorCodes.SsoLocalPasswordNotAllowed,
            "Your SSO provider does not allow local passwords.") };
    }
    // If AllowLocalPassword is true and PasswordHash is null, this is a first-time
    // password set — skip the "verify current password" check for this case.
}
```

- [ ] **Step 3: Write SsoService unit tests**

Test cases:
- `InitiateLogin_DisabledProvider_ReturnsSsoProviderDisabled`
- `InitiateLogin_NonExistentProvider_ReturnsSsoProviderNotFound`
- `HandleOidcCallback_NewUser_AutoProvisions`
- `HandleOidcCallback_ExistingLink_UpdatesLastLoginAt`
- `HandleOidcCallback_DeactivatedUser_ReturnsAccountDisabled`
- `HandleOidcCallback_LockedUser_ReturnsAccountLocked`
- `HandleOidcCallback_EmailCollision_ReturnsSsoEmailCollision`
- `HandleOidcCallback_AutoProvisionDisabled_ReturnsSsoAutoProvisionDisabled`
- `ApplyRoleMapping_MatchingGroup_UpdatesRole`
- `ApplyRoleMapping_NoMatch_UsesDefaultRole`
- `ApplyRoleMapping_Disabled_UsesDefaultRole`
- `ExchangeCode_ValidCode_ReturnsLoginResponse`
- `ExchangeCode_ExpiredCode_ReturnsSsoExchangeCodeInvalid`
- `ExchangeCode_SecondUse_ReturnsSsoExchangeCodeInvalid` (single-use)

- [ ] **Step 4: Run tests**

Run: `dotnet test tests/Courier.Tests.Unit --filter "FullyQualifiedName~SsoService"`

- [ ] **Step 5: Commit**

```
feat(sso): add SSO service with user provisioning, role mapping, and exchange codes
```

### Task 18: SSO Controller

**Files:**
- Create: `src/Courier.Features/Auth/Sso/SsoController.cs`

- [ ] **Step 1: Create the controller**

```csharp
using Courier.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Mvc;
using System.Security.Cryptography;
using System.Text.Json;

namespace Courier.Features.Auth.Sso;

[ApiController]
[Route("api/v1/auth/sso")]
[AllowAnonymous]
public class SsoController : ControllerBase
{
    private readonly SsoService _ssoService;
    private readonly IDataProtector _protector;

    public SsoController(SsoService ssoService, IDataProtectionProvider dataProtection)
    {
        _ssoService = ssoService;
        _protector = dataProtection.CreateProtector("Courier.Sso.State");
    }

    /// <summary>
    /// Initiates SSO login — redirects to the identity provider.
    /// </summary>
    [HttpGet("{providerId:guid}/login")]
    public async Task<IActionResult> InitiateLogin(Guid providerId, CancellationToken ct)
    {
        // Generate PKCE + state
        var stateBytes = RandomNumberGenerator.GetBytes(32);
        var state = Convert.ToBase64String(stateBytes);
        var codeVerifier = Base64UrlEncode(RandomNumberGenerator.GetBytes(32));
        var codeChallenge = Base64UrlEncode(
            SHA256.HashData(System.Text.Encoding.ASCII.GetBytes(codeVerifier)));

        var result = await _ssoService.InitiateLoginAsync(
            providerId, state, codeChallenge, ct);

        if (!result.Success)
        {
            return result.Error!.Code switch
            {
                ErrorCodes.SsoProviderNotFound => NotFound(result),
                ErrorCodes.SsoProviderDisabled => StatusCode(403, result),
                ErrorCodes.SsoNotConfigured => StatusCode(503, result),
                _ => StatusCode(500, result)
            };
        }

        // Store state in encrypted cookie — use the record type (not anonymous) for round-trip serialization
        var cookieData = JsonSerializer.Serialize(new SsoStateCookie(state, codeVerifier, providerId));
        var encryptedCookie = _protector.Protect(cookieData);

        Response.Cookies.Append(".Courier.SsoState", encryptedCookie, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Lax,
            MaxAge = TimeSpan.FromMinutes(10),
            Path = "/api/v1/auth/sso",
        });

        return Redirect(result.Data!);
    }

    /// <summary>
    /// OIDC callback — handles authorization code response.
    /// </summary>
    [HttpGet("callback")]
    public async Task<IActionResult> OidcCallback(
        [FromQuery] string code, [FromQuery] string state, CancellationToken ct)
    {
        var cookieState = ReadAndDeleteStateCookie();
        if (cookieState is null || cookieState.State != state)
        {
            return BadRequest(new ApiResponse
            {
                Error = ErrorMessages.Create(ErrorCodes.SsoStateMismatch, "Invalid SSO state. Please try again.")
            });
        }

        var result = await _ssoService.HandleOidcCallbackAsync(
            code, state, cookieState.CodeVerifier, cookieState.ProviderId, HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown", ct);

        if (!result.Success)
            return RedirectToFrontendWithError(result.Error!.Message);

        return RedirectToFrontendWithCode(result.Data!);
    }

    /// <summary>
    /// SAML callback — handles assertion response via HTTP POST binding.
    /// IgnoreAntiforgeryToken is required because the IdP sends a POST without CSRF tokens.
    /// </summary>
    [HttpPost("callback")]
    [IgnoreAntiforgeryToken]
    public async Task<IActionResult> SamlCallback(CancellationToken ct)
    {
        var samlResponse = Request.Form["SAMLResponse"].ToString();
        var relayState = Request.Form["RelayState"].ToString();

        var cookieState = ReadAndDeleteStateCookie();
        if (cookieState is null || cookieState.State != relayState)
        {
            return BadRequest(new ApiResponse
            {
                Error = ErrorMessages.Create(ErrorCodes.SsoStateMismatch, "Invalid SSO state. Please try again.")
            });
        }

        var result = await _ssoService.HandleSamlCallbackAsync(
            samlResponse, cookieState.ProviderId, HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown", ct);

        if (!result.Success)
            return RedirectToFrontendWithError(result.Error!.Message);

        return RedirectToFrontendWithCode(result.Data!);
    }

    /// <summary>
    /// Exchange one-time code for JWT tokens.
    /// </summary>
    [HttpPost("exchange")]
    public async Task<ActionResult<ApiResponse<Auth.AuthDtos.LoginResponse>>> Exchange(
        [FromBody] SsoExchangeRequest request, CancellationToken ct)
    {
        var result = await _ssoService.ExchangeCodeAsync(
            request.Code, HttpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown", ct);

        if (!result.Success)
        {
            return result.Error!.Code switch
            {
                ErrorCodes.SsoExchangeCodeInvalid => Unauthorized(result),
                _ => StatusCode(500, result)
            };
        }

        return Ok(result);
    }

    // --- Helpers ---

    private SsoStateCookie? ReadAndDeleteStateCookie()
    {
        if (!Request.Cookies.TryGetValue(".Courier.SsoState", out var encrypted))
            return null;

        Response.Cookies.Delete(".Courier.SsoState", new CookieOptions
        {
            Path = "/api/v1/auth/sso",
        });

        try
        {
            var json = _protector.Unprotect(encrypted);
            return JsonSerializer.Deserialize<SsoStateCookie>(json);
        }
        catch
        {
            return null;
        }
    }

    private IActionResult RedirectToFrontendWithCode(string exchangeCode)
    {
        var frontendUrl = _ssoService.GetFrontendCallbackUrl();
        return Redirect($"{frontendUrl}/auth/callback?code={Uri.EscapeDataString(exchangeCode)}");
    }

    private IActionResult RedirectToFrontendWithError(string error)
    {
        var frontendUrl = _ssoService.GetFrontendCallbackUrl();
        return Redirect($"{frontendUrl}/auth/callback?error={Uri.EscapeDataString(error)}");
    }

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');

    private record SsoStateCookie(string State, string CodeVerifier, Guid ProviderId);
}
```

- [ ] **Step 2: Register SSO services**

In `FeaturesServiceExtensions.cs`, add:

```csharp
        // SSO
        services.AddScoped<SsoService>();
        services.AddScoped<OidcHandler>();
        services.AddScoped<SamlHandler>();
```

- [ ] **Step 3: Verify build**

Run: `dotnet build Courier.slnx`

- [ ] **Step 4: Commit**

```
feat(sso): add SSO controller with OIDC/SAML callbacks and one-time code exchange
```

---

## Chunk 5: Frontend — Types, API, Hooks, and Permissions

### Task 19: TypeScript Types and API Client

**Files:**
- Modify: `src/Courier.Frontend/src/lib/types.ts`
- Modify: `src/Courier.Frontend/src/lib/api.ts`

- [ ] **Step 1: Add types**

In `types.ts`, add:

```typescript
// Auth Providers
export interface AuthProviderDto {
  id: string;
  type: "oidc" | "saml";
  name: string;
  slug: string;
  isEnabled: boolean;
  configuration: Record<string, unknown>;
  autoProvision: boolean;
  defaultRole: string;
  allowLocalPassword: boolean;
  roleMapping: RoleMappingConfig | null;
  displayOrder: number;
  iconUrl: string | null;
  linkedUserCount: number;
  createdAt: string;
  updatedAt: string;
}

export interface RoleMappingConfig {
  enabled: boolean;
  rules: RoleMappingRule[];
}

export interface RoleMappingRule {
  claim: string;
  value: string;
  role: string;
}

export interface LoginOptionDto {
  id: string;
  type: "oidc" | "saml";
  name: string;
  slug: string;
  iconUrl: string | null;
}

export interface CreateAuthProviderRequest {
  type: "oidc" | "saml";
  name: string;
  isEnabled: boolean;
  configuration: Record<string, unknown>;
  autoProvision: boolean;
  defaultRole: string;
  allowLocalPassword: boolean;
  roleMapping?: RoleMappingConfig;
  displayOrder: number;
  iconUrl?: string;
}

export interface UpdateAuthProviderRequest {
  name?: string;
  isEnabled?: boolean;
  configuration?: Record<string, unknown>;
  autoProvision?: boolean;
  defaultRole?: string;
  allowLocalPassword?: boolean;
  roleMapping?: RoleMappingConfig;
  displayOrder?: number;
  iconUrl?: string;
}

export interface TestConnectionResult {
  success: boolean;
  message: string;
  details?: Record<string, unknown>;
}
```

- [ ] **Step 2: Add API client methods**

In `api.ts`, add methods to the API class:

```typescript
  // Auth Providers
  async listAuthProviders(page = 1, pageSize = 25) {
    return this.get<PagedApiResponse<AuthProviderDto>>(`/api/v1/auth-providers?page=${page}&pageSize=${pageSize}`);
  }

  async getAuthProvider(id: string) {
    return this.get<ApiResponse<AuthProviderDto>>(`/api/v1/auth-providers/${id}`);
  }

  async createAuthProvider(data: CreateAuthProviderRequest) {
    return this.post<ApiResponse<AuthProviderDto>>("/api/v1/auth-providers", data);
  }

  async updateAuthProvider(id: string, data: UpdateAuthProviderRequest) {
    return this.put<ApiResponse<AuthProviderDto>>(`/api/v1/auth-providers/${id}`, data);
  }

  async deleteAuthProvider(id: string) {
    return this.delete<ApiResponse<void>>(`/api/v1/auth-providers/${id}`);
  }

  async testAuthProvider(id: string) {
    return this.post<ApiResponse<TestConnectionResult>>(`/api/v1/auth-providers/${id}/test`);
  }

  async getLoginOptions() {
    return this.get<ApiResponse<LoginOptionDto[]>>("/api/v1/auth-providers/login-options");
  }

  async exchangeSsoCode(code: string) {
    return this.post<ApiResponse<LoginResponse>>("/api/v1/auth/sso/exchange", { code });
  }
```

- [ ] **Step 3: Verify build**

Run: `cd src/Courier.Frontend && npx tsc --noEmit`

### Task 20: TanStack Query Hooks

**Files:**
- Create: `src/Courier.Frontend/src/lib/hooks/use-auth-providers.ts`

- [ ] **Step 1: Create hooks**

```typescript
import { useQuery, useMutation, useQueryClient } from "@tanstack/react-query";
import { api } from "../api";
import { CreateAuthProviderRequest, UpdateAuthProviderRequest } from "../types";
import { toast } from "sonner";

export function useAuthProviders(page = 1, pageSize = 25) {
  return useQuery({
    queryKey: ["auth-providers", page, pageSize],
    queryFn: () => api.listAuthProviders(page, pageSize),
  });
}

export function useAuthProvider(id: string) {
  return useQuery({
    queryKey: ["auth-providers", id],
    queryFn: () => api.getAuthProvider(id),
    enabled: !!id,
  });
}

export function useLoginOptions() {
  return useQuery({
    queryKey: ["login-options"],
    queryFn: () => api.getLoginOptions(),
    staleTime: 60_000,
  });
}

export function useCreateAuthProvider() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: CreateAuthProviderRequest) => api.createAuthProvider(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["auth-providers"] });
      toast.success("Auth provider created successfully");
    },
    onError: () => {
      toast.error("Failed to create auth provider");
    },
  });
}

export function useUpdateAuthProvider() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: ({ id, data }: { id: string; data: UpdateAuthProviderRequest }) =>
      api.updateAuthProvider(id, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["auth-providers"] });
      toast.success("Auth provider updated successfully");
    },
    onError: () => {
      toast.error("Failed to update auth provider");
    },
  });
}

export function useDeleteAuthProvider() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => api.deleteAuthProvider(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["auth-providers"] });
      toast.success("Auth provider deleted successfully");
    },
    onError: () => {
      toast.error("Failed to delete auth provider");
    },
  });
}

export function useTestAuthProvider() {
  return useMutation({
    mutationFn: (id: string) => api.testAuthProvider(id),
    onSuccess: (data) => {
      if (data.data?.success) {
        toast.success("Connection test successful");
      } else {
        toast.error(data.data?.message || "Connection test failed");
      }
    },
    onError: () => {
      toast.error("Connection test failed");
    },
  });
}
```

### Task 21: Frontend Permissions Update

**Files:**
- Modify: `src/Courier.Frontend/src/lib/permissions.ts`
- Modify: `src/Courier.Frontend/src/lib/hooks/use-permissions.ts` (if separate from permissions.ts)

- [ ] **Step 1: Add AuthProvider permissions**

Add to the Permission type union:
```typescript
| "AuthProvidersView"
| "AuthProvidersCreate"
| "AuthProvidersEdit"
| "AuthProvidersDelete"
```

Add to role permission maps:
- Admin: all four
- Operator: `AuthProvidersView` only
- Viewer: none

- [ ] **Step 2: Verify frontend build**

Run: `cd src/Courier.Frontend && npx tsc --noEmit`

- [ ] **Step 3: Commit**

```
feat(frontend): add auth provider types, API client, hooks, and permissions
```

---

## Chunk 6: Frontend — Pages and Login

### Task 22: Auth Providers List Page

**Files:**
- Create: `src/Courier.Frontend/src/app/(app)/settings/auth-providers/page.tsx`

- [ ] **Step 1: Create the list page**

Build a page following existing settings page patterns:
- Table with columns: Name, Type (badge), Enabled (toggle), Linked Users, Actions
- "Add Provider" button guarded by `can("AuthProvidersCreate")`
- Edit/Delete actions in each row
- Delete with confirmation dialog
- Use `useAuthProviders()` hook for data
- Use `useDeleteAuthProvider()` mutation for delete

### Task 23: Create/Edit Auth Provider Pages

**Files:**
- Create: `src/Courier.Frontend/src/app/(app)/settings/auth-providers/new/page.tsx`
- Create: `src/Courier.Frontend/src/app/(app)/settings/auth-providers/[id]/page.tsx`

- [ ] **Step 1: Create the form component**

Build a form with sections:
- **General:** Name, Type (select: OIDC/SAML), Enabled toggle, Display Order, Icon URL
- **Configuration:** Dynamic fields based on type:
  - OIDC: Authority URL, Client ID, Client Secret, Scopes (comma-separated)
  - SAML: Entity ID, SSO URL, Certificate (textarea), Sign Requests toggle, NameID Format
- **User Provisioning:** Auto-provision toggle, Default Role (select), Allow Local Password toggle
- **Role Mapping:** Enable toggle, rules table (Claim, Value, Role) with add/remove

- [ ] **Step 2: Create new page** — uses `useCreateAuthProvider()`, redirects to list on success

- [ ] **Step 3: Create edit page** — uses `useAuthProvider(id)` + `useUpdateAuthProvider()`, includes Test Connection button

- [ ] **Step 4: Add sidebar navigation**

Find the sidebar/navigation component and add "Auth Providers" link under Settings, guarded by `can("AuthProvidersView")`.

- [ ] **Step 5: Commit**

```
feat(frontend): add auth providers management pages and sidebar navigation
```

### Task 24: Login Page SSO Buttons

**Files:**
- Modify: `src/Courier.Frontend/src/app/(auth)/login/page.tsx`

- [ ] **Step 1: Add SSO buttons to login page**

After the existing form, add:

```tsx
// After the </form> closing tag:
<SsoLoginButtons />
```

Create a `SsoLoginButtons` component (can be inline or a separate file):

```tsx
function SsoLoginButtons() {
  const { data } = useLoginOptions();
  const providers = data?.data ?? [];

  if (providers.length === 0) return null;

  return (
    <>
      <div className="relative my-6">
        <div className="absolute inset-0 flex items-center">
          <span className="w-full border-t" />
        </div>
        <div className="relative flex justify-center text-xs uppercase">
          <span className="bg-card px-2 text-muted-foreground">or sign in with</span>
        </div>
      </div>
      <div className="space-y-2">
        {providers.map((provider) => (
          <a
            key={provider.id}
            href={`${process.env.NEXT_PUBLIC_API_URL || ""}/api/v1/auth/sso/${provider.id}/login`}
            className="flex w-full items-center justify-center gap-2 rounded-md border bg-background px-4 py-2.5 text-sm font-medium hover:bg-accent transition-colors"
          >
            {provider.iconUrl && (
              <img src={provider.iconUrl} alt="" className="h-4 w-4" />
            )}
            Sign in with {provider.name}
          </a>
        ))}
      </div>
    </>
  );
}
```

Add the import for `useLoginOptions` at the top of the file.

- [ ] **Step 2: Verify build and visual appearance**

Run: `cd src/Courier.Frontend && npm run build`

- [ ] **Step 3: Commit**

```
feat(frontend): add SSO login buttons to login page
```

### Task 25: Auth Callback Page

**Files:**
- Create: `src/Courier.Frontend/src/app/(auth)/auth/callback/page.tsx`

- [ ] **Step 1: Create the callback page**

```tsx
"use client";

import { useEffect, useState } from "react";
import { useRouter, useSearchParams } from "next/navigation";
import { useAuth } from "@/lib/auth";
import { api } from "@/lib/api";
import { Loader2 } from "lucide-react";
import { Button } from "@/components/ui/button";

export default function AuthCallbackPage() {
  const router = useRouter();
  const searchParams = useSearchParams();
  const { setTokens } = useAuth();
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    const code = searchParams.get("code");
    const errorParam = searchParams.get("error");

    if (errorParam) {
      setError(errorParam);
      return;
    }

    if (!code) {
      setError("No authorization code received.");
      return;
    }

    (async () => {
      try {
        const result = await api.exchangeSsoCode(code);
        if (result.data) {
          setTokens(result.data.accessToken, result.data.refreshToken);
          router.push("/");
        } else {
          setError(result.error?.message || "Authentication failed.");
        }
      } catch {
        setError("An unexpected error occurred during authentication.");
      }
    })();
  }, [searchParams, router, setTokens]);

  if (error) {
    return (
      <div className="rounded-xl border bg-card p-8 shadow-sm text-center">
        <h2 className="text-lg font-semibold mb-2">Authentication Failed</h2>
        <p className="text-sm text-muted-foreground mb-4">{error}</p>
        <Button variant="outline" onClick={() => router.push("/login")}>
          Back to Login
        </Button>
      </div>
    );
  }

  return (
    <div className="flex flex-col items-center gap-4 p-8">
      <Loader2 className="h-8 w-8 animate-spin text-primary" />
      <p className="text-sm text-muted-foreground">Completing sign in...</p>
    </div>
  );
}
```

**Note:** The `setTokens` method may need to be added to the auth context if it doesn't exist. Check the existing `useAuth()` hook — it likely stores tokens after `login()` succeeds. The callback page needs to set tokens directly without going through the username/password login flow. If `setTokens` doesn't exist, add it to the auth context/provider.

- [ ] **Step 2: Verify build**

Run: `cd src/Courier.Frontend && npm run build`

- [ ] **Step 3: Commit**

```
feat(frontend): add SSO auth callback page
```

---

## Final Verification

### Task 26: Full Test Suite

- [ ] **Step 1: Run all backend tests**

Run: `dotnet test Courier.slnx`
Expected: All existing + new tests pass

- [ ] **Step 2: Run frontend build**

Run: `cd src/Courier.Frontend && npm run build`
Expected: Clean build

- [ ] **Step 3: Run architecture tests**

Run: `dotnet test tests/Courier.Tests.Architecture`
Expected: All pass (Domain still has zero external deps)

- [ ] **Step 4: Final commit**

```
feat(sso): complete SSO external identity provider support

Adds OIDC and SAML authentication support:
- Auth provider CRUD with admin management UI
- Server-side SSO flow with one-time exchange codes
- JIT user provisioning with optional role mapping
- RBAC integration tests for auth providers
- Frontend: SSO login buttons, callback page, provider settings
```
