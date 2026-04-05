# Azure Function Callback Redesign

> **Supersedes** the App Insights polling approach in the original Azure Function support implementation (migration 0007, `AppInsightsQueryService`, `AzureFunctionClient`).

## Goal

Replace the fragile App Insights/Log Analytics polling mechanism for Azure Function completion detection with a direct callback pattern. Simplify the connection model from 5 credentials to 2. Publish a minimal NuGet SDK (`Courier.Functions.Sdk`) so function authors can report completion with two lines of code.

## Architecture

Courier triggers Azure Functions via their standard HTTP endpoint using a function key (not the Admin API master key). For long-running functions, Courier creates a one-time callback record in its own database and passes the callback URL + key in the request payload. The function uses the Courier SDK to POST its result back when done. The step handler polls the local `step_callbacks` table for completion. A fire-and-forget mode skips the callback entirely for functions where the HTTP response is sufficient.

## Tech Stack

- **Backend**: ASP.NET Core 10 (callback endpoint), EF Core (callback table), existing encryption service
- **SDK**: .NET class library targeting `netstandard2.0` (broadest Azure Functions compatibility). Dependencies: `System.Text.Json` only
- **Frontend**: React 19 + shadcn/ui (connection form simplification, step config updates)
- **Database**: PostgreSQL 16+ (new `step_callbacks` table, auth method constraint update)

---

## 1. Database: `step_callbacks` Table

New table to track pending and completed callbacks.

```sql
CREATE TABLE step_callbacks (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    step_execution_id   UUID NOT NULL REFERENCES step_executions(id),
    job_execution_id    UUID NOT NULL REFERENCES job_executions(id),
    callback_key        TEXT NOT NULL,
    status              TEXT NOT NULL DEFAULT 'pending',
    result_payload      JSONB,
    error_message       TEXT,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT now(),
    completed_at        TIMESTAMPTZ,
    expires_at          TIMESTAMPTZ NOT NULL
);

CREATE INDEX ix_step_callbacks_step_execution ON step_callbacks(step_execution_id);
CREATE UNIQUE INDEX ix_step_callbacks_key ON step_callbacks(callback_key);
```

Column details:
- `callback_key`: Random UUID string, one-time-use API key. Unique indexed for fast lookup during callback validation.
- `status`: `pending` | `completed` | `failed`. Set to `completed`/`failed` by the callback endpoint.
- `result_payload`: Arbitrary JSONB from the Azure Function's response. Made available to downstream steps via context references.
- `expires_at`: Set to `now() + max_wait_sec` at creation time. The callback endpoint rejects callbacks past this time. The step handler also checks this during polling and fails the step on timeout.

Check constraint on status:
```sql
ALTER TABLE step_callbacks ADD CONSTRAINT ck_step_callbacks_status
    CHECK (status IN ('pending', 'completed', 'failed'));
```

## 2. Connection Model: Simplified

### Current (being replaced)

| Credential | Column | Purpose |
|---|---|---|
| Master Key | `password_encrypted` | Admin API trigger |
| Client Secret | `client_secret_encrypted` | Entra ID for Log Analytics |
| Tenant ID | `properties.tenant_id` | Entra token endpoint |
| Client ID | `properties.client_id` | Entra token request |
| Workspace ID | `properties.workspace_id` | Log Analytics API |

### New

| Credential | Column | Purpose |
|---|---|---|
| Host | `host` | Function App domain (e.g., `myapp.azurewebsites.net`) |
| Function Key | `password_encrypted` | Appended as `?code=` query param on HTTP trigger URL |

### Auth Method

New auth method value: `function_key`. This replaces `service_principal` for Azure Function connections.

Migration updates the check constraint:
```sql
ALTER TABLE connections DROP CONSTRAINT ck_connections_auth_method;
ALTER TABLE connections ADD CONSTRAINT ck_connections_auth_method
    CHECK (auth_method IN ('password', 'ssh_key', 'password_and_ssh_key', 'service_principal', 'function_key'));
```

### Validator Changes

For `azure_function` protocol:
- `auth_method` must be `function_key`
- `password` (the function key) is required
- `host` is required
- `properties` is NOT required (no longer needed)
- `client_secret` is NOT required (no longer needed)

### Frontend Connection Form

When protocol is `azure_function`:
- Auto-set `authMethod` to `function_key`, `username` to `function_key`
- Show only two Azure-specific fields:
  - **Host**: placeholder "e.g., myapp.azurewebsites.net"
  - **Function Key**: password input with help text "Found in Azure Portal: Function App > App Keys > Host Keys, or scoped to a single function under Function Keys"
- Hide: Tenant ID, Client ID, Workspace ID, Client Secret fields
- Hide: Auth method selector, username field, SSH key selector, timeout/keepalive fields

## 3. Callback API Endpoint

### Endpoint

```
POST /api/v1/callbacks/{callbackId:guid}
Authorization: Bearer {callbackKey}
Content-Type: application/json
```

### Request Body

```json
{
    "success": true,
    "output": { "processedRecords": 1500 },
    "errorMessage": null
}
```

All fields optional. If `success` is omitted, defaults to `true`. `output` is arbitrary JSON stored as-is in `result_payload`. `errorMessage` is stored when `success` is `false`.

### Behavior

1. Look up `step_callbacks` by `id` = `callbackId`
2. If not found → 404
3. Extract key from `Authorization: Bearer {key}` header
4. If key does not match stored `callback_key` → 401
5. If `status` is not `pending` → 409 (already completed, replay protection)
6. If `now() > expires_at` → 410 (expired)
7. Update row: set `status` = `completed` or `failed` (based on `success`), `result_payload` = `output`, `error_message` = `errorMessage`, `completed_at` = now
8. Return 200: `{ "acknowledged": true }`

### Authentication

This endpoint is outside Courier's normal JWT auth middleware. The Azure Function has no Courier user session. The one-time callback key + expiry provides sufficient security:
- Key is a random UUID (128 bits of entropy)
- One-time use (rejected after first completion)
- Time-bounded (rejected after `expires_at`)
- Callback ID itself is a separate UUID (attacker must guess both)

### Controller Location

New file: `src/Courier.Features/Callbacks/CallbacksController.cs`
Route: `api/v1/callbacks`

### DTOs

```csharp
public record CallbackRequest
{
    public bool Success { get; init; } = true;
    public JsonElement? Output { get; init; }
    public string? ErrorMessage { get; init; }
}
```

## 4. Step Handler: `AzureFunctionExecuteStep`

### TypeKey

`azure_function.execute` — aligned across step handler, `StepTypeRegistry` metadata, and frontend constants.

### Step Configuration Inputs

| Key | Type | Required | Default | Description |
|---|---|---|---|---|
| `connection_id` | GUID | yes | — | Azure Function connection |
| `function_name` | string | yes | — | Function name in the Function App |
| `input_payload` | string | no | — | JSON payload for the function. Supports `context:` references. |
| `wait_for_callback` | bool | no | `true` | If false, step succeeds immediately on HTTP 2xx |
| `max_wait_sec` | int | no | `3600` | Maximum seconds to wait for callback |
| `poll_interval_sec` | int | no | `5` | Seconds between DB polls for callback completion |

### Fire-and-Forget Mode (`wait_for_callback: false`)

1. Resolve connection, decrypt function key
2. `POST https://{host}/api/{functionName}?code={functionKey}` with `input_payload` as body
3. HTTP 2xx → `StepResult.Ok()` with outputs: `{ "http_status": 200 }`
4. HTTP error → `StepResult.Fail("HTTP {status}: {body}")`
5. No `step_callbacks` row created

### Callback Mode (`wait_for_callback: true`)

1. Resolve connection, decrypt function key
2. Generate callback ID (`Guid.CreateVersion7()`) and callback key (random UUID)
3. Insert `step_callbacks` row: status `pending`, `expires_at` = `now + max_wait_sec`
4. Build request body:
   ```json
   {
       "payload": { ... input_payload ... },
       "callback": {
           "url": "https://{courierBaseUrl}/api/v1/callbacks/{callbackId}",
           "key": "{callbackKey}"
       }
   }
   ```
5. `POST https://{host}/api/{functionName}?code={functionKey}` with that body
6. If HTTP response is not 2xx → delete callback row, `StepResult.Fail()`
7. Poll `step_callbacks` row every `poll_interval_sec`:
   - `completed` → `StepResult.Ok()` with outputs from `result_payload`
   - `failed` → `StepResult.Fail(error_message)`
   - `pending` + past `expires_at` → mark row as `failed`, `StepResult.Fail("timed out after {max_wait_sec}s")`
   - `pending` + not expired → sleep, poll again

### Step Outputs (callback mode)

| Key | Type | Description |
|---|---|---|
| `function_success` | bool | Whether the function reported success |
| `callback_result` | object | The full `output` JSON from the function's callback |

### Courier Base URL

The step handler needs to know Courier's own public URL to construct the callback URL. This comes from configuration — a new config value `Courier:BaseUrl` (e.g., `https://courier.example.com`). Required when using callback mode.

## 5. SDK: `Courier.Functions.Sdk`

### Package

- **Name**: `Courier.Functions.Sdk`
- **Target**: `netstandard2.0` (works with .NET 6+ Azure Functions, .NET 8 isolated worker, etc.)
- **Dependencies**: None beyond BCL (`System.Text.Json`, `System.Net.Http`)
- **Project location**: `src/Courier.Functions.Sdk/Courier.Functions.Sdk.csproj`

### Public API

```csharp
namespace Courier.Functions.Sdk;

public class CourierCallback
{
    /// <summary>
    /// Extracts callback info from the HTTP request body.
    /// Returns a no-op instance if no callback info is present (fire-and-forget mode).
    /// </summary>
    public static CourierCallback FromBody(string requestBody);

    /// <summary>
    /// The user's payload from the Courier job step configuration.
    /// Null if no payload was provided.
    /// </summary>
    public JsonElement? Payload { get; }

    /// <summary>
    /// Whether this instance has callback info (true) or is a no-op (false).
    /// </summary>
    public bool HasCallback { get; }

    /// <summary>
    /// Reports successful completion to Courier.
    /// No-op if HasCallback is false.
    /// </summary>
    public Task SuccessAsync(object? output = null, CancellationToken ct = default);

    /// <summary>
    /// Reports failure to Courier.
    /// No-op if HasCallback is false.
    /// </summary>
    public Task FailAsync(string errorMessage, CancellationToken ct = default);
}
```

### Usage Example

```csharp
using Courier.Functions.Sdk;

public class MyFunction
{
    [Function("ProcessReport")]
    public async Task<IActionResult> Run(
        [HttpTrigger(AuthorizationLevel.Function, "post")] HttpRequest req)
    {
        var body = await new StreamReader(req.Body).ReadToEndAsync();
        var callback = CourierCallback.FromBody(body);

        try
        {
            // Access the payload Courier sent
            var inputData = callback.Payload;

            // Do long-running work...
            var result = await DoExpensiveWork();

            // Report success — no-op if fire-and-forget
            await callback.SuccessAsync(new { processedRecords = result.Count });
            return new OkResult();
        }
        catch (Exception ex)
        {
            await callback.FailAsync(ex.Message);
            return new StatusCodeResult(500);
        }
    }
}
```

### Internal Implementation

`CourierCallback` internally holds the callback URL and key. `SuccessAsync`/`FailAsync` create a new `HttpClient`, POST to the callback URL with `Authorization: Bearer {key}`, and send:
```json
{ "success": true/false, "output": { ... }, "errorMessage": "..." }
```

If `HasCallback` is false (fire-and-forget), both methods return `Task.CompletedTask`.

### No-Op Behavior

When Courier sends a fire-and-forget request, the body contains only the input payload (no `callback` property). `FromBody` detects this and returns an instance where `HasCallback = false`. This means function authors can always use the SDK pattern — it works for both modes without conditional logic.

## 6. Code Removed

### Deleted Files

| File | Reason |
|---|---|
| `src/Courier.Features/AzureFunctions/AppInsightsQueryService.cs` | Log Analytics polling replaced by DB polling |
| `src/Courier.Features/AzureFunctions/AzureFunctionClient.cs` | Admin API trigger replaced by direct HTTP call |
| `src/Courier.Features/AzureFunctions/AzureFunctionDtos.cs` | DTOs replaced by callback models |
| `src/Courier.Features/AzureFunctions/AzureFunctionsController.cs` | Traces endpoint no longer exists |
| `src/Courier.Frontend/src/components/azure-function-trace-viewer.tsx` | App Insights trace UI removed |
| `src/Courier.Frontend/src/lib/hooks/use-azure-function-traces.ts` | TanStack Query hook for traces removed |

### Removed Dependencies

| Package | Reason |
|---|---|
| `Azure.Identity` | Was only used for `ClientSecretCredential` in `AppInsightsQueryService` |

### Modified Files

| File | Change |
|---|---|
| `AzureFunctionExecuteStep.cs` | Rewritten: direct HTTP + callback polling |
| `StepTypeRegistry.cs` | Metadata key fixed to `azure_function.execute` |
| `ConnectionValidator.cs` | Add `function_key` auth method; remove `properties`/`client_secret` requirement for `azure_function` |
| `ConnectionService.cs` | Stop requiring client_secret for azure_function |
| `FeaturesServiceExtensions.cs` | Remove DI for deleted services, remove `LogAnalytics` named HttpClient, add `CallbacksController` |
| Frontend connection form | Simplify Azure Function card to Host + Function Key |
| Frontend step config form | Remove `initial_delay_sec`, add `wait_for_callback` toggle |
| Frontend step constants | Update outputs, remove trace viewer integration |
| Frontend execution timeline | Remove trace viewer integration |
| Frontend API client (`api.ts`) | Remove `getAzureFunctionTraces` method |
| Frontend types (`types.ts`) | Remove `AzureFunctionTraceDto` if present |

## 7. Configuration

New required configuration value for callback mode:

```json
{
    "Courier": {
        "BaseUrl": "https://courier.example.com"
    }
}
```

This is used by the step handler to construct the callback URL. If not set and `wait_for_callback` is `true`, the step fails with a clear error message: "Courier:BaseUrl configuration is required for Azure Function callback mode."

## 8. Migration Strategy

A new migration script `0008_azure_function_callback.sql` (or next available number):

1. Create `step_callbacks` table with indexes and check constraint
2. Add `function_key` to `ck_connections_auth_method` check constraint
3. Does NOT drop `properties` or `client_secret_encrypted` columns (used by future connection types)
4. Does NOT modify existing `azure_function` connections — they continue to work, users just need to re-save with the simplified form

Note: Migration 0007's constraint name bug (`ck_connections_auth_method` vs `ck_connections_auth`) must be handled — the new migration should use `IF EXISTS` checks for both possible names before dropping and recreating.

## 9. Documentation Updates

### CLAUDE.md

- Add `function_key` to auth method references
- Update Azure Function connection description
- Add `Courier:BaseUrl` to configuration section
- Note the callback endpoint pattern

### Frontend Inline Help

- Azure Function connection form: help text explaining where to find function keys in Azure Portal
- Step config form: tooltip on `wait_for_callback` explaining the two modes
- Step config form: tooltip on `max_wait_sec` explaining timeout behavior

## 10. Testing Strategy

### Unit Tests

- `CallbacksController`: valid callback, invalid key (401), already completed (409), expired (410), missing callback (404)
- `AzureFunctionExecuteStep` (callback mode): successful callback, failed callback, timeout, HTTP trigger failure
- `AzureFunctionExecuteStep` (fire-and-forget): HTTP success, HTTP failure
- `ConnectionValidator`: `function_key` auth method validation rules

### Integration Tests

- Full callback flow: create step execution → trigger function (mocked HTTP) → POST callback → verify step completes
- Timeout flow: create callback, wait past expiry, verify step fails

### E2E Tests

- Create Azure Function connection with simplified form
- Verify connection detail page shows Host + Function Key fields
- Create job with `azure_function.execute` step, verify config form shows `wait_for_callback` toggle

### SDK Tests

- `FromBody` with callback info → `HasCallback = true`, correct URL/key
- `FromBody` without callback info → `HasCallback = false`, no-op methods
- `FromBody` with malformed JSON → graceful handling
- `SuccessAsync` → correct HTTP request format
- `FailAsync` → correct HTTP request format with error message
