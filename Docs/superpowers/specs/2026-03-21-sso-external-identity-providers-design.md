# SSO & External Identity Provider Support — Design Specification

## Goal

Enable Courier to authenticate users via external identity providers (Entra ID, Auth0, Okta, Google, Keycloak, enterprise SAML) in addition to local username/password authentication. Admins can configure unlimited OIDC and SAML providers, with automatic user provisioning, optional role mapping from IdP claims, and per-provider control over whether SSO users can also have local passwords.

## Architecture

Server-side authentication flow. The backend handles all IdP communication — redirects, callbacks, token validation, SAML assertion processing. The frontend's only role is to render SSO login buttons and handle a one-time code exchange after the server completes IdP validation. This keeps the existing JWT token model completely intact and provides a unified flow for both OIDC and SAML.

**Libraries:**
- **OIDC:** No external library — standard HTTP calls for discovery, token exchange, and JWKS validation. `System.IdentityModel.Tokens.Jwt` (already in the project via ASP.NET Core) handles JWT validation.
- **SAML:** `ITfoxtec.Identity.Saml2` — mature library for SAML AuthnRequest generation, Response/Assertion parsing, and XML signature validation.

**Configuration:**
- `Sso:FrontendCallbackUrl` — The externally-reachable frontend URL for SSO callback redirects (e.g., `https://courier.example.com`). Required when any SSO provider is enabled. Configured in `appsettings.json` under the `Sso` section. If not set, SSO initiation endpoints return error `SsoNotConfigured` (10020).
- `Sso:ApiBaseUrl` — The externally-reachable API base URL used as `redirect_uri` (OIDC) and `AssertionConsumerServiceURL` (SAML). Derived from `Sso:ApiBaseUrl` config, NOT from the incoming request's `Host` header (prevents host header injection). Falls back to the request URL if not configured (acceptable for single-instance deployments).

**Multi-instance deployment note:** The one-time exchange codes (§1.3) are stored in `IMemoryCache`. In multi-instance deployments behind a load balancer, the exchange request must hit the same instance that created the code. V1 assumes single-instance API deployment. For multi-instance, a distributed cache (Redis or PostgreSQL) would replace `IMemoryCache` — this is noted as a future enhancement.

---

## 1. Authentication Flows

### 1.1 OIDC Flow

```
User clicks "Sign in with [Provider]" on login page
  → Browser navigates to GET /api/v1/auth/sso/{providerId}/login
  → Server loads provider config from DB
  → Server generates state (32 random bytes) + PKCE code_verifier/code_challenge
  → Server stores { state, code_verifier, providerId } in encrypted HTTP-only cookie (SameSite=Lax)
  → Server fetches provider's .well-known/openid-configuration (cached 24h; JWKS cached 1h)
  → Server returns 302 redirect to IdP authorize endpoint with:
      - response_type=code
      - client_id
      - redirect_uri={api_base}/api/v1/auth/sso/callback
      - scope=openid profile email (+ configured scopes)
      - state
      - code_challenge + code_challenge_method=S256
  → User authenticates at IdP
  → IdP redirects to GET /api/v1/auth/sso/callback?code={authCode}&state={state}
  → Server reads state cookie, validates state matches
  → Server exchanges auth code for tokens at IdP token endpoint (with code_verifier)
  → Server validates ID token (signature via JWKS, issuer, audience, expiry)
  → Server extracts claims → SsoClaimsPrincipal { SubjectId, Email, DisplayName, Groups[] }
  → Server calls ProvisionOrLinkUser (see §3)
  → Server applies role mapping (see §4)
  → Server creates one-time exchange code (see §5)
  → Server deletes state cookie
  → Server returns 302 redirect to {frontend_url}/auth/callback?code={exchangeCode}
  → Frontend reads code from URL
  → Frontend calls POST /api/v1/auth/sso/exchange { code }
  → Server validates + consumes exchange code
  → Server issues JWT access token + refresh token (same as local login)
  → Server returns standard LoginResponse { accessToken, refreshToken, expiresIn, user }
  → Frontend stores tokens using existing auth flow
```

### 1.2 SAML Flow

```
User clicks "Sign in with [SAML Provider]" on login page
  → Browser navigates to GET /api/v1/auth/sso/{providerId}/login
  → Server loads provider config from DB
  → Server generates RelayState (32 random bytes)
  → Server stores { relayState, providerId } in encrypted HTTP-only cookie (SameSite=Lax)
  → Server builds SAML AuthnRequest XML:
      - Issuer = configured entityId
      - AssertionConsumerServiceURL = {api_base}/api/v1/auth/sso/callback
      - NameIDPolicy = configured format
      - Signed with server key if signAuthnRequests=true
  → Server returns 302 redirect to IdP SSO URL with SAMLRequest (deflate + base64 + URL-encode)
  → User authenticates at IdP
  → IdP POSTs SAML Response to POST /api/v1/auth/sso/callback
      - SAMLResponse (base64-encoded XML) in form body
      - RelayState in form body
  → Server reads RelayState cookie, validates RelayState matches
  → Server decodes and parses SAML Response XML
  → Server validates:
      - Response signature (against provider's X.509 certificate)
      - Assertion signature (if separately signed)
      - Audience restriction matches our entityId
      - NotBefore / NotOnOrAfter conditions (30s clock skew tolerance)
      - Assertion ID not replayed (in-memory cache, 5-minute window)
  → Server extracts attributes → SsoClaimsPrincipal { SubjectId, Email, DisplayName, Groups[] }
  → Same provisioning, role mapping, and exchange code flow as OIDC
```

### 1.3 One-Time Exchange Code

After successful IdP validation, the server does NOT put tokens in the redirect URL. Instead:

1. Server generates 32 random bytes, base64url-encodes as the exchange code
2. Server stores in `IMemoryCache`: key=code, value={ UserId, ProviderId, CreatedAt }, TTL=60 seconds
3. Server redirects to `{frontend_url}/auth/callback?code={exchangeCode}`
4. Frontend calls `POST /api/v1/auth/sso/exchange` with the code
5. Server looks up code in cache — if found, removes it (single-use), issues JWT + refresh token
6. If code not found or expired → 401

This prevents tokens from appearing in browser history, server logs, or referrer headers.

---

## 2. Data Model

### 2.1 Auth Providers Table (enhance existing)

The existing `auth_providers` table is enhanced with additional columns:

```sql
ALTER TABLE auth_providers
  ADD COLUMN slug TEXT,
  ADD COLUMN allow_local_password BOOLEAN DEFAULT FALSE,
  ADD COLUMN role_mapping JSONB DEFAULT '{}',
  ADD COLUMN display_order INT DEFAULT 0,
  ADD COLUMN icon_url TEXT;

-- Backfill slug from name for any existing rows
-- Add unique constraint on slug
ALTER TABLE auth_providers ADD CONSTRAINT uq_auth_providers_slug UNIQUE (slug);
```

**Full schema after migration:**

| Column | Type | Description |
|--------|------|-------------|
| `id` | UUID PK | Provider ID |
| `type` | TEXT | `'oidc'` or `'saml'` |
| `name` | TEXT UNIQUE | Display name (e.g., "Corporate Entra ID") |
| `slug` | TEXT UNIQUE | URL-safe identifier (e.g., "corporate-entra-id") |
| `is_enabled` | BOOLEAN | Whether provider accepts logins |
| `configuration` | JSONB | Provider-specific config (see §2.2, §2.3) |
| `auto_provision` | BOOLEAN | Auto-create user on first SSO login |
| `default_role` | TEXT | Role for auto-provisioned users (default: `'viewer'`) |
| `allow_local_password` | BOOLEAN | Whether SSO users from this provider can also set a local password |
| `role_mapping` | JSONB | Optional claim/group → role mapping rules (see §4) |
| `display_order` | INT | Ordering of SSO buttons on login page |
| `icon_url` | TEXT | Optional provider logo URL |
| `created_at` | TIMESTAMPTZ | |
| `updated_at` | TIMESTAMPTZ | |
| `is_deleted` | BOOLEAN | Soft delete flag (default: false) |
| `deleted_at` | TIMESTAMPTZ | When soft-deleted |

### 2.2 OIDC Configuration JSONB

```json
{
  "authority": "https://login.microsoftonline.com/{tenant}/v2.0",
  "clientId": "xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx",
  "clientSecret": "<AES-256-GCM encrypted>",
  "scopes": ["openid", "profile", "email"],
  "claimMappings": {
    "email": "email",
    "name": "name",
    "groups": "groups"
  }
}
```

- `clientSecret` encrypted with the existing AES-256-GCM envelope encryption (same as connection credentials). The JSONB `configuration` field is stored as a C# `string`. The encrypt-on-write / decrypt-on-read pattern:
  - **On write:** Service parses the incoming JSON, extracts `clientSecret`, encrypts it with `CredentialEncryptor`, replaces the plaintext value in the JSON with the encrypted blob (base64-encoded), then stores the full JSON string.
  - **On read (internal):** Service parses the stored JSON, extracts the encrypted `clientSecret`, decrypts it with `CredentialEncryptor` for use in IdP token exchange calls.
  - **On read (API response):** Service replaces `clientSecret` with `"••••••••"` before returning the DTO. The encrypted value is never exposed.
  - **On update:** If the incoming `clientSecret` is null/empty/`"••••••••"`, the existing encrypted value is preserved from the DB.
- `authority` is the OpenID discovery base URL — server appends `/.well-known/openid-configuration`
- `claimMappings` allows customizing which claims map to the normalized fields

### 2.3 SAML Configuration JSONB

```json
{
  "entityId": "https://courier.example.com",
  "ssoUrl": "https://idp.example.com/saml/sso",
  "certificate": "<base64 X.509 cert>",
  "signAuthnRequests": true,
  "nameIdFormat": "urn:oasis:names:tc:SAML:1.1:nameid-format:emailAddress",
  "attributeMappings": {
    "email": "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/emailaddress",
    "name": "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name",
    "groups": "http://schemas.microsoft.com/ws/2008/06/identity/claims/groups"
  }
}
```

### 2.4 SSO User Links Table (new)

```sql
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
```

- `ON DELETE CASCADE` on `user_id`: deleting a user removes their SSO links
- `ON DELETE RESTRICT` on `provider_id`: providers cannot be hard-deleted while links exist (use soft delete instead)

This replaces the single `sso_provider_id`/`sso_subject_id` on the user entity for new SSO flows. The legacy fields remain for backward compatibility but new code uses `sso_user_links`. The existing `AuthProvider.Users` navigation property is deprecated in favor of `SsoUserLinks` collection.

### 2.5 Role Mapping JSONB

```json
{
  "enabled": true,
  "rules": [
    { "claim": "groups", "value": "courier-admins", "role": "admin" },
    { "claim": "groups", "value": "courier-operators", "role": "operator" }
  ]
}
```

Rules evaluated in order — first match wins. No match → provider's `default_role`.

---

## 3. User Provisioning (JIT)

When an SSO user authenticates successfully:

1. **Look up existing link:** Query `sso_user_links` by `(provider_id, subject_id)`
2. **If linked user found:**
   - **Check account status first:**
     - If `is_active = false` → reject with `AccountDisabled` (10002)
     - If `locked_until > now` → reject with `AccountLocked` (10001)
   - Update `last_login_at` on the link
   - Update `last_login_at` on the user
   - Update user's `display_name` and `email` from IdP claims (keeps profile in sync)
   - Proceed to role mapping and token issuance
3. **If no link found:**
   - Check `auto_provision` on the provider
   - If false → reject login with error: "Account not found. Contact your administrator."
   - If true → check for email collision:
     - If a local user with the same email exists → **reject login** with error: "An account with this email already exists. Contact your administrator to link your account." (No automatic merging — too risky.)
     - If no collision → create new user:
       - `username` = email prefix or IdP subject_id (ensure uniqueness by appending suffix if needed)
       - `display_name` = from IdP claims
       - `email` = from IdP claims
       - `role` = provider's `default_role` (or mapped role, see §4)
       - `is_sso_user` = true
       - `password_hash` = null (no local password unless `allow_local_password` is true)
       - `is_active` = true
     - Create `sso_user_links` record
     - Audit: `sso_user_provisioned`

### 3.1 Password Change Interaction

If an SSO user attempts to change or set their password via `POST /api/v1/auth/change-password`:
- Look up the user's primary SSO link → get the provider
- If provider's `allow_local_password` is false → reject with `SsoLocalPasswordNotAllowed` (10025)
- If `allow_local_password` is true → allow the password change (existing flow)
- If user has no SSO links (local-only user) → allow as normal

### 3.2 Manual Account Linking

Admins can link an existing local user to an SSO provider via the Users management page. This creates a `sso_user_links` record without requiring the user to log in via SSO first. Use case: migrating existing local users to SSO.

---

## 4. Role Mapping

When `role_mapping.enabled` is true on a provider:

1. Extract the configured claim from IdP claims (e.g., `groups`)
2. For each rule in `role_mapping.rules` (ordered):
   - If the claim value matches the rule's `value` → use the rule's `role`
   - First match wins
3. If no rule matches → use `default_role`

When `role_mapping.enabled` is false (or role_mapping is empty):
- Always use `default_role`

**Role update behavior:**
- On every SSO login, role mapping is re-evaluated
- If the mapped role differs from the user's current role, the user's role is updated
- This means IdP group changes propagate on next login
- Audit: `sso_role_updated` when role changes

**Guard rails:**
- Role mapping cannot create roles that don't exist (admin, operator, viewer only)
- If a mapping rule references an invalid role, it's skipped with a warning log

---

## 5. Security

### 5.1 CSRF Protection

- **OIDC:** `state` parameter = 32 random bytes. Stored in encrypted HTTP-only `SameSite=Lax` cookie. Validated on callback. Cookie deleted after use.
- **SAML:** `RelayState` parameter with same encrypted cookie approach.
- Cookie name: `.Courier.SsoState`
- Cookie encryption: ASP.NET Core Data Protection (same key ring as other cookies)

### 5.2 PKCE (OIDC only)

- `code_verifier` = 32 random bytes, base64url-encoded (43 characters)
- `code_challenge` = SHA-256 hash of verifier, base64url-encoded
- `code_challenge_method` = S256
- Verifier stored in the same state cookie, sent during token exchange

### 5.3 Client Secret Storage

- OIDC `clientSecret` encrypted with AES-256-GCM envelope encryption (existing system)
- Never returned in API responses — redacted to `"••••••••"`
- On update: if `clientSecret` is null/empty in the request, existing value preserved

### 5.4 SAML Signature Validation

- Response and Assertion signatures validated against provider's X.509 certificate
- Certificate stored as base64 PEM in JSONB config
- Audience restriction checked against configured `entityId`
- `NotBefore`/`NotOnOrAfter` conditions enforced with 30-second clock skew tolerance
- Replay protection: assertion IDs tracked in `IMemoryCache` with 5-minute TTL

### 5.5 User Provisioning Safety

- **Email collision:** SSO login fails if email matches an existing local user → admin must manually link
- **Disabled provider:** All SSO logins rejected with 403. Users with `allow_local_password=true` can still use password login.
- **Deleted provider (soft delete):** All SSO logins rejected. Linked users retain accounts but lose SSO access.

### 5.6 Rate Limiting

- `/api/v1/auth/sso/{id}/login` — same rate limit as regular login endpoints
- `/api/v1/auth/sso/exchange` — same rate limit as regular login endpoints
- Provider CRUD — standard API rate limit

### 5.7 Error Codes

SSO-specific error codes in the Authentication range (10000-10099), starting at 10020:

| Code | Name | Description |
|------|------|-------------|
| 10020 | `SsoNotConfigured` | `Sso:FrontendCallbackUrl` not configured |
| 10021 | `SsoProviderNotFound` | Provider ID does not exist |
| 10022 | `SsoProviderDisabled` | Provider exists but `is_enabled = false` |
| 10023 | `SsoStateMismatch` | State/RelayState cookie doesn't match callback parameter (CSRF) |
| 10024 | `SsoExchangeCodeInvalid` | Exchange code not found, expired, or already consumed |
| 10025 | `SsoLocalPasswordNotAllowed` | SSO user tried to set password but provider disallows it |
| 10026 | `SsoEmailCollision` | IdP email matches an existing local user — admin must manually link |
| 10027 | `SsoAutoProvisionDisabled` | No existing link and provider's `auto_provision = false` |
| 10028 | `SsoIdTokenValidationFailed` | OIDC ID token signature, issuer, audience, or expiry invalid |
| 10029 | `SsoSamlValidationFailed` | SAML assertion signature, audience, or conditions invalid |
| 10030 | `SsoSamlReplayDetected` | SAML assertion ID was already processed |
| 10031 | `SsoTestConnectionFailed` | Test connection to IdP failed (discovery/cert validation) |
| 10032 | `SsoClaimMappingFailed` | Required claim (e.g., email) not present in IdP response |

Existing auth error codes also apply in SSO flows:
- `10001` `AccountLocked` — linked user is locked
- `10002` `AccountDisabled` — linked user is deactivated

### 5.8 Audit Trail

All SSO events logged via existing `AuditService`. A new `AuditableEntityType.AuthProvider` value must be added to the enum and the `ck_audit_entity_type` CHECK constraint updated in the migration.

| Event | Entity Type | Entity ID | Data |
|-------|------------|-----------|------|
| `sso_login_success` | `User` | userId | providerId, IP |
| `sso_login_failed` | `AuthProvider` | providerId | reason, IP |
| `sso_user_provisioned` | `User` | userId | providerId |
| `sso_user_linked` | `User` | userId | providerId |
| `sso_role_updated` | `User` | userId | oldRole, newRole, providerId |
| `auth_provider_created` | `AuthProvider` | providerId | adminUserId |
| `auth_provider_updated` | `AuthProvider` | providerId | adminUserId |
| `auth_provider_deleted` | `AuthProvider` | providerId | adminUserId |

---

## 6. Backend Components

### 6.1 New Feature: `src/Courier.Features/Auth/Sso/`

| File | Responsibility |
|------|---------------|
| `SsoController.cs` | 4 endpoints: initiate login, OIDC callback (GET), SAML callback (POST), exchange code. The callback endpoints share the same path `/api/v1/auth/sso/callback` differentiated by HTTP method. The `providerId` is retrieved from the encrypted state cookie (not the URL) so the server knows which handler (OIDC vs SAML) to invoke. |
| `SsoService.cs` | Orchestrates SSO flow: initiate, handle callbacks, provision users, create exchange codes |
| `OidcHandler.cs` | Pure OIDC protocol: build authorize URL, exchange code, validate ID token via JWKS |
| `SamlHandler.cs` | Pure SAML protocol: build AuthnRequest, validate Response/Assertion, extract attributes |
| `SsoClaimsPrincipal.cs` | Normalized claim model: `{ SubjectId, Email, DisplayName, Groups[] }` |
| `SsoDtos.cs` | Request/response DTOs for SSO endpoints |

### 6.2 New Feature: `src/Courier.Features/AuthProviders/`

| File | Responsibility |
|------|---------------|
| `AuthProvidersController.cs` | CRUD + test connection + public login-options endpoint |
| `AuthProvidersService.cs` | Business logic: encrypt secrets, validate configs, generate slugs |
| `AuthProvidersDtos.cs` | Request/response DTOs |
| `AuthProvidersValidator.cs` | FluentValidation rules for create/update |

**Test Connection endpoint** (`POST /api/v1/auth-providers/{id}/test`):
- **OIDC:** Fetches `{authority}/.well-known/openid-configuration`. If successful, also fetches the JWKS URI from the discovery document. Returns success with IdP issuer name and supported scopes.
- **SAML:** Parses the configured X.509 certificate and validates it is not expired. Optionally performs an HTTP HEAD request to the `ssoUrl` to check reachability. Returns success with certificate subject and expiry date.
- **Response:** `ApiResponse<TestConnectionResult>` with `{ success: bool, message: string, details: { issuer?, certExpiry?, supportedScopes? } }`
- **Error:** Returns `SsoTestConnectionFailed` (10031) with the underlying error message.
- **Note:** The provider must be saved first (requires an `id`). Test-before-create is not supported in V1.

### 6.3 Domain Layer Changes

| File | Change |
|------|--------|
| `src/Courier.Domain/Entities/SsoUserLink.cs` | New entity |
| `src/Courier.Domain/Entities/AuthProvider.cs` | Add new properties (slug, allow_local_password, role_mapping, display_order, icon_url, IsDeleted, DeletedAt). Add `SsoUserLinks` navigation. Deprecate `Users` nav property. |
| `src/Courier.Domain/Enums/Permission.cs` | Add: `AuthProvidersView`, `AuthProvidersCreate`, `AuthProvidersEdit`, `AuthProvidersDelete` |
| `src/Courier.Domain/Authorization/RolePermissions.cs` | Add new permissions to role sets |
| `src/Courier.Domain/Common/ErrorCodes.cs` | Add SSO error codes 10020-10032 (see §5.7) |
| `src/Courier.Domain/Enums/AuditableEntityType.cs` | Add `AuthProvider` value |

### 6.4 Infrastructure Changes

| File | Change |
|------|--------|
| `CourierDbContext.cs` | Add `DbSet<SsoUserLink>`, update `AuthProvider` entity mapping (new columns + soft delete query filter), add `SsoUserLink` mapping |
| `Program.cs` | Register SSO services, add `ITfoxtec.Identity.Saml2` package |

### 6.5 Modified Files

| File | Change |
|------|--------|
| `AuthService.cs` | Add `LoginViaSsoAsync(userId)` — issues JWT + refresh token for authenticated SSO user. Update `ChangePasswordAsync` to check `allow_local_password` for SSO users. |
| `AuditService.cs` | Add `AuthProvider` to `EntityTypeMap` dictionary |
| `AuthController.cs` | No changes (SSO has its own controller) |

---

## 7. Frontend Components

### 7.1 Login Page Changes (`/login`)

- Fetch enabled providers from `GET /api/v1/auth-providers/login-options` on mount
- Render SSO buttons below the existing password form, separated by "or sign in with" divider
- Each button navigates to `/api/v1/auth/sso/{providerId}/login` (full page navigation)
- Hide SSO section when no providers are enabled
- Buttons ordered by `display_order`

### 7.2 New Page: Auth Callback (`/auth/callback`)

- Reads `code` query parameter from URL
- Calls `POST /api/v1/auth/sso/exchange` with the code
- On success: stores tokens (same as local login), redirects to `/`
- On error: displays error message with "Back to Login" link
- Shows loading spinner during exchange

### 7.3 New Page: Auth Providers Settings (`/settings/auth-providers`)

Admin-only page for managing SSO providers:

- **List view:** Table with columns: Name, Type (OIDC/SAML badge), Enabled (toggle), Linked Users count, Actions (Edit, Delete)
- **Create/Edit:** Form with sections:
  - General: Name, Type (select), Enabled, Display Order, Icon URL
  - Configuration: Dynamic fields based on type — OIDC shows authority/clientId/clientSecret/scopes; SAML shows entityId/ssoUrl/certificate/nameIdFormat
  - User Provisioning: Auto-provision toggle, Default Role select, Allow Local Password toggle
  - Role Mapping: Enable toggle + rules table (Claim, Value, Role columns, add/remove rows)
- **Test Connection:** Button on create/edit form that calls `/auth-providers/{id}/test`, shows inline success/error
- **Delete:** Confirmation dialog warning about linked users losing SSO access

### 7.4 Permission Updates

Add to `src/Courier.Frontend/src/lib/permissions.ts` and `use-permissions.ts`:
- `AuthProvidersView` — Admin, Operator
- `AuthProvidersCreate` — Admin
- `AuthProvidersEdit` — Admin
- `AuthProvidersDelete` — Admin

### 7.5 Navigation

Add "Auth Providers" link under Settings in the sidebar, visible to users with `AuthProvidersView` permission.

### 7.6 TanStack Query Hooks

New `src/Courier.Frontend/src/lib/hooks/use-auth-providers.ts`:
- `useAuthProviders()` — list all providers
- `useAuthProvider(id)` — get single provider
- `useCreateAuthProvider()` — create mutation
- `useUpdateAuthProvider()` — update mutation
- `useDeleteAuthProvider()` — delete mutation
- `useTestAuthProvider()` — test connection mutation
- `useLoginOptions()` — public endpoint for login page

---

## 8. Database Migration

New migration script: `src/Courier.Migrations/Scripts/NNNN_sso_external_providers.sql`

```sql
-- Enhance auth_providers table with new columns
ALTER TABLE auth_providers
  ADD COLUMN IF NOT EXISTS slug TEXT,
  ADD COLUMN IF NOT EXISTS allow_local_password BOOLEAN DEFAULT FALSE,
  ADD COLUMN IF NOT EXISTS role_mapping JSONB DEFAULT '{}',
  ADD COLUMN IF NOT EXISTS display_order INT DEFAULT 0,
  ADD COLUMN IF NOT EXISTS icon_url TEXT,
  ADD COLUMN IF NOT EXISTS is_deleted BOOLEAN NOT NULL DEFAULT FALSE,
  ADD COLUMN IF NOT EXISTS deleted_at TIMESTAMPTZ;

-- Generate slugs for existing providers (if any)
-- Trim leading/trailing hyphens, collapse multiple hyphens, enforce min length
UPDATE auth_providers
SET slug = TRIM(BOTH '-' FROM REGEXP_REPLACE(LOWER(REGEXP_REPLACE(name, '[^a-zA-Z0-9]+', '-', 'g')), '-{2,}', '-', 'g'))
WHERE slug IS NULL;

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

---

## 9. Testing Strategy

### 9.1 Unit Tests

**SsoService:**
- OIDC callback valid → provisions user, returns exchange code
- SAML callback valid → provisions user, returns exchange code
- Exchange code valid → returns LoginResponse, code consumed (second use fails)
- Exchange code expired → 401
- New user: auto-provisioned with default role
- Existing link: updates last_login_at, no duplicate user
- Email collision with local user → error (no auto-merge)
- Role mapping: matching group → assigned role
- Role mapping: no match → default_role
- Role mapping disabled → always default_role
- Provider disabled → rejects login

**OidcHandler:**
- Builds authorization URL with PKCE + state + scopes
- Token exchange valid → extracts claims
- Token exchange error → throws
- ID token expired → rejects
- ID token wrong audience → rejects

**SamlHandler:**
- Builds AuthnRequest XML
- Valid signed assertion → extracts attributes
- Invalid signature → rejects
- Expired assertion → rejects
- Audience mismatch → rejects

**AuthProvidersService:**
- CRUD operations
- Client secret encrypted on save, redacted on read
- Slug generation from name
- OIDC validation: requires authority + clientId
- SAML validation: requires entityId + ssoUrl + certificate

### 9.2 Integration Tests (RBAC)

**AuthProvidersRbacTests:**
- Admin: full CRUD access
- Operator: view only
- Viewer: view only
- Anonymous: only `login-options` endpoint accessible

**SsoFlowTests (mock IdP):**
- Full OIDC flow: initiate → mock callback → exchange → valid JWT
- Full SAML flow: initiate → mock callback → exchange → valid JWT
- Auto-provision creates user on first login
- Second login uses existing link
- Disabled provider → 403
- Invalid state → 401

### 9.3 E2E Tests (Playwright)

- Admin creates OIDC provider via settings UI
- Admin creates SAML provider via settings UI
- Provider list shows correct status
- Test Connection shows feedback
- Login page renders SSO buttons for enabled providers
- Login page hides SSO section when no providers
- Delete provider with confirmation

(Actual SSO redirect flows tested in integration tests, not E2E — would require a running mock IdP.)

---

## 10. RBAC Permissions

| Permission | Admin | Operator | Viewer |
|-----------|-------|----------|--------|
| `AuthProvidersView` | ✓ | ✓ | ✗ |
| `AuthProvidersCreate` | ✓ | ✗ | ✗ |
| `AuthProvidersEdit` | ✓ | ✗ | ✗ |
| `AuthProvidersDelete` | ✓ | ✗ | ✗ |

---

## 11. Implementation Notes

### 11.1 Slug Generation

Slugs are generated in `AuthProvidersService`, not in SQL:
1. Lowercase the name
2. Replace non-alphanumeric characters with hyphens
3. Collapse multiple consecutive hyphens to one
4. Trim leading/trailing hyphens
5. If the result is empty (name was all special characters), use `"provider-{shortGuid}"`
6. If a collision exists, append `-2`, `-3`, etc.

### 11.2 OIDC Discovery Caching

- Discovery documents (`/.well-known/openid-configuration`): cached 24 hours
- JWKS keys: cached 1 hour
- Both evicted when "Test Connection" is invoked (forces fresh fetch)
- Cache uses `IMemoryCache` with sliding expiration

---

## 12. Out of Scope

- **Multi-factor authentication (MFA)** — separate feature
- **Password reset flow** — separate feature
- **API key authentication (M2M)** — V2 roadmap
- **SCIM user provisioning** — enterprise feature, out of V1 scope
- **IdP-initiated SAML login** — only SP-initiated flows in this iteration
- **OIDC back-channel logout** — front-channel (token expiry) is sufficient for V1
- **SAML SP metadata endpoint** — IdP admins manually configure ACS URL and entity ID for V1. A `GET /api/v1/auth/sso/{providerId}/metadata` endpoint returning SP metadata XML is a future enhancement.
- **SAML Single Logout (SLO)** — not implemented in V1; session expiry via JWT lifetime is sufficient
- **Distributed exchange code store** — V1 assumes single API instance; multi-instance deployments need Redis/PostgreSQL-backed store (see Architecture section note)
