# Azure Function Callback Redesign Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the App Insights polling mechanism for Azure Function completion detection with a direct HTTP callback pattern, simplify the connection model, and publish a minimal SDK.

**Architecture:** Courier triggers Azure Functions via their HTTP endpoint with a function key. For long-running functions, Courier creates a callback record in its own DB and passes the callback URL + one-time key in the request. The function uses a minimal SDK to POST results back. The step handler polls the local DB for completion. A fire-and-forget mode skips callbacks entirely.

**Tech Stack:** ASP.NET Core 10, EF Core (InMemory for tests), PostgreSQL 16+, .NET SDK (netstandard2.0), Next.js 15, React 19, shadcn/ui

**Spec:** `docs/superpowers/specs/2026-04-04-azure-function-callback-redesign.md`

---

## File Structure

### New Files
| File | Responsibility |
|---|---|
| `src/Courier.Migrations/Scripts/0036_step_callbacks.sql` | Migration: step_callbacks table + function_key auth method |
| `src/Courier.Domain/Entities/StepCallback.cs` | Domain entity for callback records |
| `src/Courier.Features/Callbacks/CallbackDtos.cs` | Request/response DTOs for callback endpoint |
| `src/Courier.Features/Callbacks/StepCallbackService.cs` | CRUD operations on step_callbacks |
| `src/Courier.Features/Callbacks/CallbacksController.cs` | Public callback endpoint (no JWT auth) |
| `src/Courier.Functions.Sdk/Courier.Functions.Sdk.csproj` | SDK project file |
| `src/Courier.Functions.Sdk/CourierCallback.cs` | Main SDK class |
| `src/Courier.Functions.Sdk/CourierCallbackPayload.cs` | Internal request body model |
| `tests/Courier.Tests.Unit/Callbacks/StepCallbackServiceTests.cs` | Service unit tests |
| `tests/Courier.Tests.Unit/Callbacks/CallbacksControllerTests.cs` | Controller unit tests |
| `tests/Courier.Tests.Unit/Engine/Steps/Azure/AzureFunctionExecuteStepTests.cs` | Step handler unit tests |
| `tests/Courier.Tests.Unit/Sdk/CourierCallbackTests.cs` | SDK unit tests |

### Modified Files
| File | Change |
|---|---|
| `src/Courier.Infrastructure/Data/CourierDbContext.cs` | Add StepCallback DbSet + OnModelCreating config |
| `src/Courier.Features/Engine/StepTypeRegistry.cs` | Fix metadata key `azure.function` to `azure_function.execute`, update inputs/outputs |
| `src/Courier.Features/Connections/ConnectionValidator.cs` | Add `function_key` to valid auth methods, update azure_function rules |
| `src/Courier.Features/Engine/Steps/Azure/AzureFunctionExecuteStep.cs` | Complete rewrite: callback + fire-and-forget modes |
| `src/Courier.Features/FeaturesServiceExtensions.cs` | Remove old DI registrations, add new ones |
| `src/Courier.Features/Courier.Features.csproj` | Remove `Azure.Identity` package |
| `src/Courier.Api/appsettings.json` | Add `Courier:BaseUrl` |
| `src/Courier.Frontend/src/components/connections/connection-form.tsx` | Simplify Azure Function card |
| `src/Courier.Frontend/src/components/jobs/azure-function-step-config.tsx` | Add wait_for_callback, remove initial_delay_sec |
| `src/Courier.Frontend/src/components/jobs/step-config-form.tsx` | Update azure_function.execute section |
| `src/Courier.Frontend/src/components/jobs/step-constants.ts` | Update outputs |
| `src/Courier.Frontend/src/components/jobs/execution-timeline.tsx` | Remove trace viewer |
| `src/Courier.Frontend/src/lib/api.ts` | Remove getAzureFunctionTraces |
| `src/Courier.Frontend/src/lib/types.ts` | Remove AzureFunctionTraceDto |
| `Directory.Packages.props` | Add System.Text.Json if missing |
| `Courier.slnx` | Add SDK project |
| `tests/Courier.Tests.Unit/Courier.Tests.Unit.csproj` | Add SDK project reference |
| `CLAUDE.md` | Add function_key auth, Courier:BaseUrl, callback endpoint docs |

### Deleted Files
| File | Reason |
|---|---|
| `src/Courier.Features/AzureFunctions/AppInsightsQueryService.cs` | Replaced by local DB polling |
| `src/Courier.Features/AzureFunctions/AzureFunctionClient.cs` | Replaced by direct HTTP in step handler |
| `src/Courier.Features/AzureFunctions/AzureFunctionDtos.cs` | Replaced by callback DTOs |
| `src/Courier.Features/AzureFunctions/AzureFunctionsController.cs` | Traces endpoint removed |
| `src/Courier.Frontend/src/components/azure-function-trace-viewer.tsx` | No longer needed |
| `src/Courier.Frontend/src/lib/hooks/use-azure-function-traces.ts` | No longer needed |

---

### Task 1: Database Migration

**Files:**
- Create: `src/Courier.Migrations/Scripts/0036_step_callbacks.sql`

- [ ] **Step 1: Create migration file**

```sql
-- 0036: Step callbacks for Azure Function completion detection
-- Adds step_callbacks table and function_key auth method.

CREATE TABLE step_callbacks (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    callback_key        TEXT NOT NULL,
    status              TEXT NOT NULL DEFAULT 'pending',
    result_payload      JSONB,
    error_message       TEXT,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT now(),
    completed_at        TIMESTAMPTZ,
    expires_at          TIMESTAMPTZ NOT NULL
);

CREATE UNIQUE INDEX ix_step_callbacks_key ON step_callbacks(callback_key);

ALTER TABLE step_callbacks ADD CONSTRAINT ck_step_callbacks_status
    CHECK (status IN ('pending', 'completed', 'failed'));

-- Add function_key to auth method constraint.
-- Handle both possible constraint names (0007 bug: name may be ck_connections_auth or ck_connections_auth_method).
DO $$
BEGIN
    IF EXISTS (
        SELECT 1 FROM information_schema.check_constraints
        WHERE constraint_name = 'ck_connections_auth_method'
    ) THEN
        ALTER TABLE connections DROP CONSTRAINT ck_connections_auth_method;
    END IF;

    IF EXISTS (
        SELECT 1 FROM information_schema.check_constraints
        WHERE constraint_name = 'ck_connections_auth'
    ) THEN
        ALTER TABLE connections DROP CONSTRAINT ck_connections_auth;
    END IF;
END $$;

ALTER TABLE connections ADD CONSTRAINT ck_connections_auth_method
    CHECK (auth_method IN ('password', 'ssh_key', 'password_and_ssh_key', 'service_principal', 'function_key'));
```

- [ ] **Step 2: Verify migration file exists**

Run: `ls src/Courier.Migrations/Scripts/0036_step_callbacks.sql`
Expected: File listed

---

### Task 2: StepCallback Entity + DbContext

**Files:**
- Create: `src/Courier.Domain/Entities/StepCallback.cs`
- Modify: `src/Courier.Infrastructure/Data/CourierDbContext.cs`

- [ ] **Step 1: Create StepCallback entity**

```csharp
namespace Courier.Domain.Entities;

public class StepCallback
{
    public Guid Id { get; set; }
    public string CallbackKey { get; set; } = string.Empty;
    public string Status { get; set; } = "pending";
    public string? ResultPayload { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
}
```

- [ ] **Step 2: Add DbSet to CourierDbContext**

In `src/Courier.Infrastructure/Data/CourierDbContext.cs`, add with the other DbSet declarations (around line 13-42):

```csharp
public DbSet<StepCallback> StepCallbacks => Set<StepCallback>();
```

- [ ] **Step 3: Add OnModelCreating configuration**

In `src/Courier.Infrastructure/Data/CourierDbContext.cs`, inside `OnModelCreating` (after the last entity configuration block), add:

```csharp
modelBuilder.Entity<StepCallback>(entity =>
{
    entity.ToTable("step_callbacks");
    entity.HasKey(e => e.Id);
    entity.Property(e => e.Id).HasColumnName("id");
    entity.Property(e => e.CallbackKey).HasColumnName("callback_key").IsRequired();
    entity.Property(e => e.Status).HasColumnName("status").IsRequired().HasDefaultValue("pending");
    entity.Property(e => e.ResultPayload).HasColumnName("result_payload").HasColumnType("jsonb");
    entity.Property(e => e.ErrorMessage).HasColumnName("error_message");
    entity.Property(e => e.CreatedAt).HasColumnName("created_at");
    entity.Property(e => e.CompletedAt).HasColumnName("completed_at");
    entity.Property(e => e.ExpiresAt).HasColumnName("expires_at");
    entity.HasIndex(e => e.CallbackKey).IsUnique();
});
```

- [ ] **Step 4: Verify build**

Run: `dotnet build Courier.slnx`
Expected: Build succeeds with 0 errors

---

### Task 3: Callback Service + Controller

**Files:**
- Create: `src/Courier.Features/Callbacks/CallbackDtos.cs`
- Create: `src/Courier.Features/Callbacks/StepCallbackService.cs`
- Create: `src/Courier.Features/Callbacks/CallbacksController.cs`

- [ ] **Step 1: Create callback DTOs**

```csharp
using System.Text.Json;

namespace Courier.Features.Callbacks;

public record CallbackRequest
{
    public bool Success { get; init; } = true;
    public JsonElement? Output { get; init; }
    public string? ErrorMessage { get; init; }
}
```

- [ ] **Step 2: Create StepCallbackService**

```csharp
using System.Text.Json;
using Courier.Domain.Entities;
using Courier.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Courier.Features.Callbacks;

public class StepCallbackService
{
    private readonly CourierDbContext _db;

    public StepCallbackService(CourierDbContext db)
    {
        _db = db;
    }

    public async Task<(Guid CallbackId, string CallbackKey)> CreateAsync(
        int maxWaitSec, CancellationToken ct = default)
    {
        var callback = new StepCallback
        {
            Id = Guid.CreateVersion7(),
            CallbackKey = Guid.NewGuid().ToString("N"),
            Status = "pending",
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddSeconds(maxWaitSec),
        };

        _db.StepCallbacks.Add(callback);
        await _db.SaveChangesAsync(ct);

        return (callback.Id, callback.CallbackKey);
    }

    public async Task<StepCallback?> GetByIdAsync(Guid callbackId, CancellationToken ct = default)
    {
        return await _db.StepCallbacks.FirstOrDefaultAsync(c => c.Id == callbackId, ct);
    }

    public async Task<(bool Success, string? Error)> ProcessCallbackAsync(
        Guid callbackId, string key, CallbackRequest request, CancellationToken ct = default)
    {
        var callback = await _db.StepCallbacks.FirstOrDefaultAsync(c => c.Id == callbackId, ct);

        if (callback is null)
            return (false, "not_found");

        if (callback.CallbackKey != key)
            return (false, "unauthorized");

        if (callback.Status != "pending")
            return (false, "already_completed");

        if (DateTime.UtcNow > callback.ExpiresAt)
            return (false, "expired");

        callback.Status = request.Success ? "completed" : "failed";
        callback.ResultPayload = request.Output?.GetRawText();
        callback.ErrorMessage = request.ErrorMessage;
        callback.CompletedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return (true, null);
    }

    public async Task DeleteAsync(Guid callbackId, CancellationToken ct = default)
    {
        var callback = await _db.StepCallbacks.FirstOrDefaultAsync(c => c.Id == callbackId, ct);
        if (callback is not null)
        {
            _db.StepCallbacks.Remove(callback);
            await _db.SaveChangesAsync(ct);
        }
    }

    public async Task MarkExpiredAsync(Guid callbackId, CancellationToken ct = default)
    {
        var callback = await _db.StepCallbacks.FirstOrDefaultAsync(c => c.Id == callbackId, ct);
        if (callback is not null && callback.Status == "pending")
        {
            callback.Status = "failed";
            callback.ErrorMessage = "Callback expired — function did not respond in time.";
            callback.CompletedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }
    }
}
```

- [ ] **Step 3: Create CallbacksController**

```csharp
using Courier.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Courier.Features.Callbacks;

[ApiController]
[Route("api/v1/callbacks")]
[AllowAnonymous]
public class CallbacksController : ControllerBase
{
    private readonly StepCallbackService _callbackService;

    public CallbacksController(StepCallbackService callbackService)
    {
        _callbackService = callbackService;
    }

    [HttpPost("{callbackId:guid}")]
    public async Task<IActionResult> ReceiveCallback(
        Guid callbackId,
        [FromBody] CallbackRequest request,
        CancellationToken ct)
    {
        var authHeader = Request.Headers.Authorization.FirstOrDefault();
        if (string.IsNullOrEmpty(authHeader) || !authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            return Unauthorized(new { error = "Missing or invalid Authorization header." });

        var key = authHeader["Bearer ".Length..].Trim();

        var (success, error) = await _callbackService.ProcessCallbackAsync(callbackId, key, request, ct);

        return error switch
        {
            null => Ok(new { acknowledged = true }),
            "not_found" => NotFound(new { error = "Callback not found." }),
            "unauthorized" => Unauthorized(new { error = "Invalid callback key." }),
            "already_completed" => Conflict(new { error = "Callback already completed." }),
            "expired" => StatusCode(410, new { error = "Callback has expired." }),
            _ => StatusCode(500, new { error = "Unexpected error." }),
        };
    }
}
```

- [ ] **Step 4: Verify build**

Run: `dotnet build Courier.slnx`
Expected: Build succeeds with 0 errors

---

### Task 4: Callback Unit Tests

**Files:**
- Create: `tests/Courier.Tests.Unit/Callbacks/StepCallbackServiceTests.cs`
- Create: `tests/Courier.Tests.Unit/Callbacks/CallbacksControllerTests.cs`

- [ ] **Step 1: Create StepCallbackService tests**

```csharp
using System.Text.Json;
using Courier.Features.Callbacks;
using Courier.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Courier.Tests.Unit.Callbacks;

public class StepCallbackServiceTests
{
    private static CourierDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<CourierDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new CourierDbContext(options);
    }

    [Fact]
    public async Task Create_ReturnsIdAndKey()
    {
        using var db = CreateDb();
        var service = new StepCallbackService(db);

        var (id, key) = await service.CreateAsync(3600);

        id.ShouldNotBe(Guid.Empty);
        key.ShouldNotBeNullOrEmpty();

        var callback = await db.StepCallbacks.FindAsync(id);
        callback.ShouldNotBeNull();
        callback!.Status.ShouldBe("pending");
        callback.ExpiresAt.ShouldBeGreaterThan(DateTime.UtcNow.AddSeconds(3500));
    }

    [Fact]
    public async Task ProcessCallback_ValidKey_SetsCompleted()
    {
        using var db = CreateDb();
        var service = new StepCallbackService(db);
        var (id, key) = await service.CreateAsync(3600);

        var output = JsonDocument.Parse("""{"count": 42}""").RootElement;
        var request = new CallbackRequest { Success = true, Output = output };

        var (success, error) = await service.ProcessCallbackAsync(id, key, request);

        success.ShouldBeTrue();
        error.ShouldBeNull();

        var callback = await db.StepCallbacks.FindAsync(id);
        callback!.Status.ShouldBe("completed");
        callback.ResultPayload.ShouldBe("""{"count": 42}""");
        callback.CompletedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task ProcessCallback_WrongKey_ReturnsUnauthorized()
    {
        using var db = CreateDb();
        var service = new StepCallbackService(db);
        var (id, _) = await service.CreateAsync(3600);

        var (success, error) = await service.ProcessCallbackAsync(id, "wrong-key", new CallbackRequest());

        success.ShouldBeFalse();
        error.ShouldBe("unauthorized");
    }

    [Fact]
    public async Task ProcessCallback_AlreadyCompleted_ReturnsAlreadyCompleted()
    {
        using var db = CreateDb();
        var service = new StepCallbackService(db);
        var (id, key) = await service.CreateAsync(3600);

        await service.ProcessCallbackAsync(id, key, new CallbackRequest());
        var (success, error) = await service.ProcessCallbackAsync(id, key, new CallbackRequest());

        success.ShouldBeFalse();
        error.ShouldBe("already_completed");
    }

    [Fact]
    public async Task ProcessCallback_Expired_ReturnsExpired()
    {
        using var db = CreateDb();
        var service = new StepCallbackService(db);
        var (id, key) = await service.CreateAsync(0); // expires immediately

        await Task.Delay(50); // ensure expiry

        var (success, error) = await service.ProcessCallbackAsync(id, key, new CallbackRequest());

        success.ShouldBeFalse();
        error.ShouldBe("expired");
    }

    [Fact]
    public async Task ProcessCallback_NotFound_ReturnsNotFound()
    {
        using var db = CreateDb();
        var service = new StepCallbackService(db);

        var (success, error) = await service.ProcessCallbackAsync(Guid.NewGuid(), "key", new CallbackRequest());

        success.ShouldBeFalse();
        error.ShouldBe("not_found");
    }

    [Fact]
    public async Task ProcessCallback_Failed_SetsFailedStatus()
    {
        using var db = CreateDb();
        var service = new StepCallbackService(db);
        var (id, key) = await service.CreateAsync(3600);

        var request = new CallbackRequest { Success = false, ErrorMessage = "boom" };
        var (success, _) = await service.ProcessCallbackAsync(id, key, request);

        success.ShouldBeTrue();
        var callback = await db.StepCallbacks.FindAsync(id);
        callback!.Status.ShouldBe("failed");
        callback.ErrorMessage.ShouldBe("boom");
    }

    [Fact]
    public async Task Delete_RemovesCallback()
    {
        using var db = CreateDb();
        var service = new StepCallbackService(db);
        var (id, _) = await service.CreateAsync(3600);

        await service.DeleteAsync(id);

        var callback = await db.StepCallbacks.FindAsync(id);
        callback.ShouldBeNull();
    }

    [Fact]
    public async Task MarkExpired_SetsFailed()
    {
        using var db = CreateDb();
        var service = new StepCallbackService(db);
        var (id, _) = await service.CreateAsync(3600);

        await service.MarkExpiredAsync(id);

        var callback = await db.StepCallbacks.FindAsync(id);
        callback!.Status.ShouldBe("failed");
        callback.ErrorMessage.ShouldContain("expired");
    }
}
```

- [ ] **Step 2: Create CallbacksController tests**

```csharp
using Courier.Features.Callbacks;
using Courier.Infrastructure.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Primitives;
using Shouldly;

namespace Courier.Tests.Unit.Callbacks;

public class CallbacksControllerTests
{
    private static CourierDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<CourierDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new CourierDbContext(options);
    }

    private static CallbacksController CreateController(StepCallbackService service, string? bearerToken = null)
    {
        var controller = new CallbacksController(service);
        var httpContext = new DefaultHttpContext();
        if (bearerToken is not null)
            httpContext.Request.Headers.Authorization = new StringValues($"Bearer {bearerToken}");
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        return controller;
    }

    [Fact]
    public async Task ReceiveCallback_ValidKey_Returns200()
    {
        using var db = CreateDb();
        var service = new StepCallbackService(db);
        var (id, key) = await service.CreateAsync(3600);
        var controller = CreateController(service, key);

        var result = await controller.ReceiveCallback(id, new CallbackRequest { Success = true }, CancellationToken.None);

        result.ShouldBeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task ReceiveCallback_MissingAuth_Returns401()
    {
        using var db = CreateDb();
        var service = new StepCallbackService(db);
        var (id, _) = await service.CreateAsync(3600);
        var controller = CreateController(service, bearerToken: null);

        var result = await controller.ReceiveCallback(id, new CallbackRequest(), CancellationToken.None);

        result.ShouldBeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task ReceiveCallback_WrongKey_Returns401()
    {
        using var db = CreateDb();
        var service = new StepCallbackService(db);
        var (id, _) = await service.CreateAsync(3600);
        var controller = CreateController(service, "wrong-key");

        var result = await controller.ReceiveCallback(id, new CallbackRequest(), CancellationToken.None);

        result.ShouldBeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task ReceiveCallback_NotFound_Returns404()
    {
        using var db = CreateDb();
        var service = new StepCallbackService(db);
        var controller = CreateController(service, "some-key");

        var result = await controller.ReceiveCallback(Guid.NewGuid(), new CallbackRequest(), CancellationToken.None);

        result.ShouldBeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task ReceiveCallback_AlreadyCompleted_Returns409()
    {
        using var db = CreateDb();
        var service = new StepCallbackService(db);
        var (id, key) = await service.CreateAsync(3600);
        var controller = CreateController(service, key);

        await controller.ReceiveCallback(id, new CallbackRequest(), CancellationToken.None);
        var result = await controller.ReceiveCallback(id, new CallbackRequest(), CancellationToken.None);

        var statusResult = result.ShouldBeOfType<ObjectResult>();
        statusResult.StatusCode.ShouldBe(409);
    }
}
```

- [ ] **Step 3: Run tests**

Run: `dotnet test tests/Courier.Tests.Unit --filter "FullyQualifiedName~Callbacks"`
Expected: All tests pass

---

### Task 5: Fix StepTypeRegistry + ConnectionValidator

**Files:**
- Modify: `src/Courier.Features/Engine/StepTypeRegistry.cs`
- Modify: `src/Courier.Features/Connections/ConnectionValidator.cs`

- [ ] **Step 1: Fix StepTypeRegistry metadata key**

In `src/Courier.Features/Engine/StepTypeRegistry.cs`, find the `azure.function` entry (around line 218) and replace it:

Old:
```csharp
["azure.function"] = new("azure.function", "Azure Function", "cloud", "Executes an Azure Function",
    Outputs: [
        new("invocation_id", "Azure Function invocation ID", "string"),
        new("function_success", "Whether the function succeeded", "boolean"),
        new("function_duration_ms", "Function execution duration in milliseconds", "number"),
    ],
    Inputs: [
        new("connectionId", "Azure Function connection to use", Required: true),
        new("functionName", "Azure Function name to invoke", Required: true),
        new("inputPayload", "JSON payload to send to the function", Required: false, SupportsContextRef: true),
    ]),
```

New:
```csharp
["azure_function.execute"] = new("azure_function.execute", "Azure Function", "cloud", "Executes an Azure Function via HTTP trigger",
    Outputs: [
        new("function_success", "Whether the function succeeded", "boolean"),
        new("callback_result", "Output payload from the Azure Function callback", "object"),
        new("http_status", "HTTP status code from the function trigger", "number"),
    ],
    Inputs: [
        new("connection_id", "Azure Function connection to use", Required: true),
        new("function_name", "Azure Function name to invoke", Required: true),
        new("input_payload", "JSON payload to send to the function", Required: false, SupportsContextRef: true),
        new("wait_for_callback", "Whether to wait for the function to call back (default: true)", Required: false),
        new("max_wait_sec", "Maximum seconds to wait for callback (default: 3600)", Required: false),
        new("poll_interval_sec", "Seconds between DB polls for callback (default: 5)", Required: false),
    ]),
```

- [ ] **Step 2: Update ConnectionValidator — CreateConnectionValidator**

In `src/Courier.Features/Connections/ConnectionValidator.cs`, update the `CreateConnectionValidator`:

Change `ValidAuthMethods` array:
```csharp
private static readonly string[] ValidAuthMethods = ["password", "ssh_key", "password_and_ssh_key", "service_principal", "function_key"];
```

Replace the Azure Function-specific validation block (around lines 47-57):

Old:
```csharp
// Azure Function-specific validation
RuleFor(x => x.AuthMethod)
    .Equal("service_principal").WithMessage("Azure Function connections require service_principal auth method.")
    .When(x => x.Protocol == "azure_function");

RuleFor(x => x.ClientSecret)
    .NotEmpty().WithMessage("Client secret is required for service_principal auth.")
    .When(x => x.AuthMethod == "service_principal");

RuleFor(x => x.Properties)
    .NotEmpty().WithMessage("Properties are required for azure_function connections.")
    .When(x => x.Protocol == "azure_function");
```

New:
```csharp
// Azure Function-specific validation
RuleFor(x => x.AuthMethod)
    .Equal("function_key").WithMessage("Azure Function connections require function_key auth method.")
    .When(x => x.Protocol == "azure_function");

RuleFor(x => x.Password)
    .NotEmpty().WithMessage("Function key is required for Azure Function connections.")
    .When(x => x.Protocol == "azure_function");
```

- [ ] **Step 3: Update ConnectionValidator — UpdateConnectionValidator**

In the same file, update `UpdateConnectionValidator`:

Change `ValidAuthMethods` array:
```csharp
private static readonly string[] ValidAuthMethods = ["password", "ssh_key", "password_and_ssh_key", "service_principal", "function_key"];
```

Replace the Azure Function-specific validation block (around lines 129-140):

Old:
```csharp
// Azure Function-specific validation
RuleFor(x => x.AuthMethod)
    .Equal("service_principal").WithMessage("Azure Function connections require service_principal auth method.")
    .When(x => x.Protocol == "azure_function");

RuleFor(x => x.ClientSecret)
    .NotEmpty().WithMessage("Client secret is required for service_principal auth.")
    .When(x => x.AuthMethod == "service_principal" && x.ClientSecret is not null);

RuleFor(x => x.Properties)
    .NotEmpty().WithMessage("Properties are required for azure_function connections.")
    .When(x => x.Protocol == "azure_function");
```

New:
```csharp
// Azure Function-specific validation
RuleFor(x => x.AuthMethod)
    .Equal("function_key").WithMessage("Azure Function connections require function_key auth method.")
    .When(x => x.Protocol == "azure_function");
```

- [ ] **Step 4: Verify build**

Run: `dotnet build Courier.slnx`
Expected: Build succeeds with 0 errors

---

### Task 6: Rewrite AzureFunctionExecuteStep

**Files:**
- Modify: `src/Courier.Features/Engine/Steps/Azure/AzureFunctionExecuteStep.cs`

- [ ] **Step 1: Rewrite step handler**

Replace the entire content of `src/Courier.Features/Engine/Steps/Azure/AzureFunctionExecuteStep.cs`:

```csharp
using System.Net.Http.Json;
using System.Text.Json;
using Courier.Domain.Engine;
using Courier.Domain.Encryption;
using Courier.Features.Callbacks;
using Courier.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Courier.Features.Engine.Steps.Azure;

public class AzureFunctionExecuteStep : IJobStep
{
    private readonly CourierDbContext _db;
    private readonly ICredentialEncryptor _encryptor;
    private readonly StepCallbackService _callbackService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AzureFunctionExecuteStep> _logger;

    public string TypeKey => "azure_function.execute";

    public AzureFunctionExecuteStep(
        CourierDbContext db,
        ICredentialEncryptor encryptor,
        StepCallbackService callbackService,
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<AzureFunctionExecuteStep> logger)
    {
        _db = db;
        _encryptor = encryptor;
        _callbackService = callbackService;
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
    }

    public async Task<StepResult> ExecuteAsync(
        StepConfiguration config,
        JobContext context,
        CancellationToken cancellationToken)
    {
        // 1. Resolve connection
        var connectionIdStr = config.GetString("connection_id");
        if (!Guid.TryParse(connectionIdStr, out var connectionId))
            return StepResult.Fail($"Invalid connection_id: {connectionIdStr}");

        var connection = await _db.Connections.FirstOrDefaultAsync(
            c => c.Id == connectionId, cancellationToken);
        if (connection is null)
            return StepResult.Fail($"Connection '{connectionId}' not found.");
        if (connection.Protocol != "azure_function")
            return StepResult.Fail($"Connection '{connection.Name}' uses protocol '{connection.Protocol}', expected 'azure_function'.");

        // 2. Decrypt function key
        if (connection.PasswordEncrypted is null)
            return StepResult.Fail("Connection is missing the function key (stored as password).");
        var functionKey = _encryptor.Decrypt(connection.PasswordEncrypted);

        // 3. Read step config
        var functionName = config.GetString("function_name");
        var inputPayload = config.GetStringOrDefault("input_payload");
        var waitForCallback = config.GetBoolOrDefault("wait_for_callback", true);
        var maxWaitSec = config.GetIntOrDefault("max_wait_sec", 3600);
        var pollIntervalSec = config.GetIntOrDefault("poll_interval_sec", 5);

        // 4. Build request
        var url = $"https://{connection.Host}/api/{functionName}?code={functionKey}";
        var client = _httpClientFactory.CreateClient("AzureFunctions");

        if (!waitForCallback)
            return await ExecuteFireAndForgetAsync(client, url, inputPayload, functionName, cancellationToken);

        return await ExecuteWithCallbackAsync(
            client, url, inputPayload, functionName,
            maxWaitSec, pollIntervalSec, cancellationToken);
    }

    private async Task<StepResult> ExecuteFireAndForgetAsync(
        HttpClient client, string url, string? inputPayload,
        string functionName, CancellationToken ct)
    {
        _logger.LogInformation("Triggering Azure Function '{FunctionName}' (fire-and-forget)", functionName);

        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Content = inputPayload is not null
                ? new StringContent(inputPayload, System.Text.Encoding.UTF8, "application/json")
                : JsonContent.Create(new { });

            using var response = await client.SendAsync(request, ct);

            if (!response.IsSuccessStatusCode)
            {
                var body = await response.Content.ReadAsStringAsync(ct);
                return StepResult.Fail($"Azure Function returned HTTP {(int)response.StatusCode}: {body}");
            }

            _logger.LogInformation("Azure Function '{FunctionName}' triggered successfully", functionName);
            return StepResult.Ok(outputs: new Dictionary<string, object>
            {
                ["function_success"] = true,
                ["http_status"] = (int)response.StatusCode,
            });
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            return StepResult.Fail($"Failed to trigger Azure Function: {ex.Message}");
        }
    }

    private async Task<StepResult> ExecuteWithCallbackAsync(
        HttpClient client, string url, string? inputPayload,
        string functionName, int maxWaitSec, int pollIntervalSec,
        CancellationToken ct)
    {
        var baseUrl = _configuration["Courier:BaseUrl"];
        if (string.IsNullOrEmpty(baseUrl))
            return StepResult.Fail("Courier:BaseUrl configuration is required for Azure Function callback mode.");

        // Create callback record
        var (callbackId, callbackKey) = await _callbackService.CreateAsync(maxWaitSec, ct);

        _logger.LogInformation(
            "Triggering Azure Function '{FunctionName}' with callback {CallbackId}",
            functionName, callbackId);

        // Build request body with callback info
        var callbackUrl = $"{baseUrl.TrimEnd('/')}/api/v1/callbacks/{callbackId}";
        object body;
        if (inputPayload is not null)
        {
            try
            {
                var payloadElement = JsonDocument.Parse(inputPayload).RootElement;
                body = new { payload = payloadElement, callback = new { url = callbackUrl, key = callbackKey } };
            }
            catch (JsonException)
            {
                body = new { payload = inputPayload, callback = new { url = callbackUrl, key = callbackKey } };
            }
        }
        else
        {
            body = new { callback = new { url = callbackUrl, key = callbackKey } };
        }

        // Trigger function
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, url);
            request.Content = JsonContent.Create(body);

            using var response = await client.SendAsync(request, ct);

            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync(ct);
                await _callbackService.DeleteAsync(callbackId, ct);
                return StepResult.Fail($"Azure Function returned HTTP {(int)response.StatusCode}: {responseBody}");
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            await _callbackService.DeleteAsync(callbackId, ct);
            return StepResult.Fail($"Failed to trigger Azure Function: {ex.Message}");
        }

        // Poll for callback completion
        _logger.LogInformation("Waiting for callback from '{FunctionName}' (max {MaxWait}s)", functionName, maxWaitSec);

        var deadline = DateTime.UtcNow.AddSeconds(maxWaitSec);

        while (DateTime.UtcNow < deadline)
        {
            ct.ThrowIfCancellationRequested();

            var callback = await _callbackService.GetByIdAsync(callbackId, ct);

            if (callback is null)
                return StepResult.Fail("Callback record was unexpectedly deleted.");

            if (callback.Status == "completed")
            {
                _logger.LogInformation("Azure Function '{FunctionName}' completed successfully via callback", functionName);

                var outputs = new Dictionary<string, object>
                {
                    ["function_success"] = true,
                };

                if (callback.ResultPayload is not null)
                {
                    try
                    {
                        var resultElement = JsonDocument.Parse(callback.ResultPayload).RootElement;
                        outputs["callback_result"] = resultElement;
                    }
                    catch (JsonException)
                    {
                        outputs["callback_result"] = callback.ResultPayload;
                    }
                }

                return StepResult.Ok(outputs: outputs);
            }

            if (callback.Status == "failed")
            {
                return StepResult.Fail(
                    $"Azure Function '{functionName}' reported failure: {callback.ErrorMessage ?? "no error message"}");
            }

            await Task.Delay(TimeSpan.FromSeconds(pollIntervalSec), ct);
        }

        // Timeout
        await _callbackService.MarkExpiredAsync(callbackId, ct);
        return StepResult.Fail($"Azure Function '{functionName}' did not call back within {maxWaitSec}s.");
    }

    public Task<StepResult> ValidateAsync(StepConfiguration config)
    {
        if (!config.Has("connection_id"))
            return Task.FromResult(StepResult.Fail("Missing required config: connection_id"));
        if (!config.Has("function_name"))
            return Task.FromResult(StepResult.Fail("Missing required config: function_name"));
        return Task.FromResult(StepResult.Ok());
    }
}
```

- [ ] **Step 2: Verify build**

Run: `dotnet build Courier.slnx`
Expected: Build succeeds (old AzureFunctions services still exist but are now unreferenced)

---

### Task 7: Step Handler Unit Tests

**Files:**
- Create: `tests/Courier.Tests.Unit/Engine/Steps/Azure/AzureFunctionExecuteStepTests.cs`

- [ ] **Step 1: Create step handler tests**

```csharp
using System.Net;
using System.Text;
using System.Text.Json;
using Courier.Domain.Encryption;
using Courier.Domain.Engine;
using Courier.Features.Callbacks;
using Courier.Features.Engine.Steps.Azure;
using Courier.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Shouldly;

namespace Courier.Tests.Unit.Engine.Steps.Azure;

public class AzureFunctionExecuteStepTests
{
    private static CourierDbContext CreateDb()
    {
        var options = new DbContextOptionsBuilder<CourierDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;
        return new CourierDbContext(options);
    }

    private static ICredentialEncryptor CreateMockEncryptor()
    {
        var mock = Substitute.For<ICredentialEncryptor>();
        mock.Encrypt(Arg.Any<string>()).Returns(ci => Encoding.UTF8.GetBytes(ci.Arg<string>()));
        mock.Decrypt(Arg.Any<byte[]>()).Returns(ci => Encoding.UTF8.GetString(ci.Arg<byte[]>()));
        return mock;
    }

    private static IHttpClientFactory CreateMockHttpFactory(HttpStatusCode statusCode, string responseBody = "")
    {
        var handler = new FakeHttpHandler(statusCode, responseBody);
        var client = new HttpClient(handler);
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient("AzureFunctions").Returns(client);
        return factory;
    }

    private static IConfiguration CreateConfig(string? baseUrl = "https://courier.test")
    {
        var dict = new Dictionary<string, string?>();
        if (baseUrl is not null)
            dict["Courier:BaseUrl"] = baseUrl;
        return new ConfigurationBuilder().AddInMemoryCollection(dict).Build();
    }

    private static async Task<Guid> SeedConnection(CourierDbContext db)
    {
        var conn = new Domain.Entities.Connection
        {
            Id = Guid.CreateVersion7(),
            Name = "Test Azure Func",
            Protocol = "azure_function",
            Host = "myapp.azurewebsites.net",
            AuthMethod = "function_key",
            Username = "function_key",
            PasswordEncrypted = Encoding.UTF8.GetBytes("test-function-key"),
            Port = 443,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.Connections.Add(conn);
        await db.SaveChangesAsync();
        return conn.Id;
    }

    [Fact]
    public async Task FireAndForget_Success_ReturnsOk()
    {
        using var db = CreateDb();
        var connId = await SeedConnection(db);
        var step = new AzureFunctionExecuteStep(
            db, CreateMockEncryptor(), new StepCallbackService(db),
            CreateMockHttpFactory(HttpStatusCode.OK), CreateConfig(),
            Substitute.For<ILogger<AzureFunctionExecuteStep>>());

        var config = new StepConfiguration(JsonSerializer.Serialize(new
        {
            connection_id = connId.ToString(),
            function_name = "MyFunc",
            wait_for_callback = false,
        }));

        var result = await step.ExecuteAsync(config, new JobContext(), CancellationToken.None);

        result.Success.ShouldBeTrue();
        result.Outputs!["function_success"].ShouldBe(true);
        result.Outputs["http_status"].ShouldBe(200);
    }

    [Fact]
    public async Task FireAndForget_HttpError_ReturnsFail()
    {
        using var db = CreateDb();
        var connId = await SeedConnection(db);
        var step = new AzureFunctionExecuteStep(
            db, CreateMockEncryptor(), new StepCallbackService(db),
            CreateMockHttpFactory(HttpStatusCode.InternalServerError, "boom"), CreateConfig(),
            Substitute.For<ILogger<AzureFunctionExecuteStep>>());

        var config = new StepConfiguration(JsonSerializer.Serialize(new
        {
            connection_id = connId.ToString(),
            function_name = "MyFunc",
            wait_for_callback = false,
        }));

        var result = await step.ExecuteAsync(config, new JobContext(), CancellationToken.None);

        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldContain("500");
    }

    [Fact]
    public async Task Callback_MissingBaseUrl_ReturnsFail()
    {
        using var db = CreateDb();
        var connId = await SeedConnection(db);
        var step = new AzureFunctionExecuteStep(
            db, CreateMockEncryptor(), new StepCallbackService(db),
            CreateMockHttpFactory(HttpStatusCode.OK), CreateConfig(baseUrl: null),
            Substitute.For<ILogger<AzureFunctionExecuteStep>>());

        var config = new StepConfiguration(JsonSerializer.Serialize(new
        {
            connection_id = connId.ToString(),
            function_name = "MyFunc",
            wait_for_callback = true,
        }));

        var result = await step.ExecuteAsync(config, new JobContext(), CancellationToken.None);

        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldContain("Courier:BaseUrl");
    }

    [Fact]
    public async Task Callback_HttpTriggerFails_DeletesCallbackAndFails()
    {
        using var db = CreateDb();
        var connId = await SeedConnection(db);
        var callbackService = new StepCallbackService(db);
        var step = new AzureFunctionExecuteStep(
            db, CreateMockEncryptor(), callbackService,
            CreateMockHttpFactory(HttpStatusCode.InternalServerError), CreateConfig(),
            Substitute.For<ILogger<AzureFunctionExecuteStep>>());

        var config = new StepConfiguration(JsonSerializer.Serialize(new
        {
            connection_id = connId.ToString(),
            function_name = "MyFunc",
        }));

        var result = await step.ExecuteAsync(config, new JobContext(), CancellationToken.None);

        result.Success.ShouldBeFalse();
        // Callback should have been cleaned up
        var callbacks = await db.StepCallbacks.ToListAsync();
        callbacks.ShouldBeEmpty();
    }

    [Fact]
    public async Task Validate_MissingConnectionId_Fails()
    {
        using var db = CreateDb();
        var step = new AzureFunctionExecuteStep(
            db, CreateMockEncryptor(), new StepCallbackService(db),
            CreateMockHttpFactory(HttpStatusCode.OK), CreateConfig(),
            Substitute.For<ILogger<AzureFunctionExecuteStep>>());

        var config = new StepConfiguration(JsonSerializer.Serialize(new { function_name = "MyFunc" }));
        var result = await step.ValidateAsync(config);

        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldContain("connection_id");
    }

    [Fact]
    public async Task Validate_MissingFunctionName_Fails()
    {
        using var db = CreateDb();
        var step = new AzureFunctionExecuteStep(
            db, CreateMockEncryptor(), new StepCallbackService(db),
            CreateMockHttpFactory(HttpStatusCode.OK), CreateConfig(),
            Substitute.For<ILogger<AzureFunctionExecuteStep>>());

        var config = new StepConfiguration(JsonSerializer.Serialize(new { connection_id = Guid.NewGuid().ToString() }));
        var result = await step.ValidateAsync(config);

        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldContain("function_name");
    }

    private class FakeHttpHandler : HttpMessageHandler
    {
        private readonly HttpStatusCode _statusCode;
        private readonly string _body;

        public FakeHttpHandler(HttpStatusCode statusCode, string body = "")
        {
            _statusCode = statusCode;
            _body = body;
        }

        protected override Task<HttpResponseMessage> SendAsync(
            HttpRequestMessage request, CancellationToken cancellationToken)
        {
            return Task.FromResult(new HttpResponseMessage(_statusCode)
            {
                Content = new StringContent(_body),
            });
        }
    }
}
```

- [ ] **Step 2: Run tests**

Run: `dotnet test tests/Courier.Tests.Unit --filter "FullyQualifiedName~AzureFunctionExecuteStepTests"`
Expected: All tests pass

---

### Task 8: DI + Configuration

**Files:**
- Modify: `src/Courier.Features/FeaturesServiceExtensions.cs`
- Modify: `src/Courier.Api/appsettings.json`

- [ ] **Step 1: Update FeaturesServiceExtensions**

In `src/Courier.Features/FeaturesServiceExtensions.cs`, find the Azure Function registrations (around lines 109-114):

Old:
```csharp
// Azure Function step handler
services.AddHttpClient("AzureFunctions");
services.AddHttpClient("LogAnalytics");
services.AddScoped<AzureFunctionClient>();
services.AddScoped<AppInsightsQueryService>();
services.AddScoped<IJobStep, AzureFunctionExecuteStep>();
```

New:
```csharp
// Azure Function step handler + callback service
services.AddHttpClient("AzureFunctions");
services.AddScoped<StepCallbackService>();
services.AddScoped<IJobStep, AzureFunctionExecuteStep>();
```

Add the required using statements at the top of the file:
```csharp
using Courier.Features.Callbacks;
```

Remove the now-unused using statements:
```csharp
// Remove these:
using Courier.Features.AzureFunctions;
```

- [ ] **Step 2: Add Courier:BaseUrl to appsettings.json**

In `src/Courier.Api/appsettings.json`, add the `Courier` section:

```json
"Courier": {
    "BaseUrl": "http://localhost:5000"
}
```

Also add to `src/Courier.Worker/appsettings.json` if it exists (the Worker runs the job engine too):

```json
"Courier": {
    "BaseUrl": "http://localhost:5000"
}
```

- [ ] **Step 3: Verify build**

Run: `dotnet build Courier.slnx`
Expected: Build succeeds with 0 errors

---

### Task 9: Remove Old Azure Function Code

**Files:**
- Delete: `src/Courier.Features/AzureFunctions/AppInsightsQueryService.cs`
- Delete: `src/Courier.Features/AzureFunctions/AzureFunctionClient.cs`
- Delete: `src/Courier.Features/AzureFunctions/AzureFunctionDtos.cs`
- Delete: `src/Courier.Features/AzureFunctions/AzureFunctionsController.cs`
- Modify: `src/Courier.Features/Courier.Features.csproj`

- [ ] **Step 1: Delete old backend files**

```bash
rm src/Courier.Features/AzureFunctions/AppInsightsQueryService.cs
rm src/Courier.Features/AzureFunctions/AzureFunctionClient.cs
rm src/Courier.Features/AzureFunctions/AzureFunctionDtos.cs
rm src/Courier.Features/AzureFunctions/AzureFunctionsController.cs
```

If the `AzureFunctions` directory is now empty, remove it:
```bash
rmdir src/Courier.Features/AzureFunctions
```

- [ ] **Step 2: Remove Azure.Identity from Courier.Features.csproj**

In `src/Courier.Features/Courier.Features.csproj`, remove this line:
```xml
<PackageReference Include="Azure.Identity" />
```

- [ ] **Step 3: Verify build**

Run: `dotnet build Courier.slnx`
Expected: Build succeeds with 0 errors. No references to deleted types.

- [ ] **Step 4: Run all unit tests**

Run: `dotnet test tests/Courier.Tests.Unit`
Expected: All tests pass. If any tests reference the deleted types, they need to be updated or removed.

---

### Task 10: Courier.Functions.Sdk

**Files:**
- Create: `src/Courier.Functions.Sdk/Courier.Functions.Sdk.csproj`
- Create: `src/Courier.Functions.Sdk/CourierCallbackPayload.cs`
- Create: `src/Courier.Functions.Sdk/CourierCallback.cs`
- Create: `tests/Courier.Tests.Unit/Sdk/CourierCallbackTests.cs`
- Modify: `Courier.slnx`
- Modify: `Directory.Packages.props`
- Modify: `tests/Courier.Tests.Unit/Courier.Tests.Unit.csproj`

- [ ] **Step 1: Check if System.Text.Json is in Directory.Packages.props**

Read `Directory.Packages.props` and check for a `System.Text.Json` entry. If missing, add it in the appropriate section:

```xml
<PackageReference Include="System.Text.Json" Version="9.0.4" />
```

Use the latest 9.x version since the SDK targets netstandard2.0 (9.x supports netstandard2.0, 10.x may not).

- [ ] **Step 2: Create SDK project file**

```xml
<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <TargetFramework>netstandard2.0</TargetFramework>
    <LangVersion>latest</LangVersion>
    <Nullable>enable</Nullable>
    <PackageId>Courier.Functions.Sdk</PackageId>
    <Description>Minimal SDK for Azure Functions to report completion back to Courier.</Description>
  </PropertyGroup>

  <ItemGroup>
    <PackageReference Include="System.Text.Json" />
  </ItemGroup>

</Project>
```

- [ ] **Step 3: Create CourierCallbackPayload**

```csharp
using System.Text.Json.Serialization;

namespace Courier.Functions.Sdk;

internal class CourierCallbackPayload
{
    [JsonPropertyName("payload")]
    public System.Text.Json.JsonElement? Payload { get; set; }

    [JsonPropertyName("callback")]
    public CallbackInfo? Callback { get; set; }
}

internal class CallbackInfo
{
    [JsonPropertyName("url")]
    public string Url { get; set; } = string.Empty;

    [JsonPropertyName("key")]
    public string Key { get; set; } = string.Empty;
}
```

- [ ] **Step 4: Create CourierCallback**

```csharp
using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Courier.Functions.Sdk;

/// <summary>
/// Extracts callback information from a Courier-triggered HTTP request
/// and provides methods to report function completion.
/// </summary>
public class CourierCallback
{
    private readonly string? _callbackUrl;
    private readonly string? _callbackKey;

    private CourierCallback(string? callbackUrl, string? callbackKey, JsonElement? payload)
    {
        _callbackUrl = callbackUrl;
        _callbackKey = callbackKey;
        Payload = payload;
    }

    /// <summary>
    /// The user's payload from the Courier job step configuration.
    /// Null if no payload was provided.
    /// </summary>
    public JsonElement? Payload { get; }

    /// <summary>
    /// Whether this instance has callback info (true) or is a no-op (false).
    /// When false, SuccessAsync/FailAsync are silent no-ops.
    /// </summary>
    public bool HasCallback => _callbackUrl != null;

    /// <summary>
    /// Extracts callback info from the HTTP request body.
    /// Returns a no-op instance if no callback info is present (fire-and-forget mode).
    /// </summary>
    public static CourierCallback FromBody(string requestBody)
    {
        if (string.IsNullOrWhiteSpace(requestBody))
            return new CourierCallback(null, null, null);

        try
        {
            var doc = JsonDocument.Parse(requestBody);
            var root = doc.RootElement;

            JsonElement? payload = null;
            if (root.TryGetProperty("payload", out var p))
                payload = p.Clone();

            if (root.TryGetProperty("callback", out var cb)
                && cb.TryGetProperty("url", out var urlProp)
                && cb.TryGetProperty("key", out var keyProp))
            {
                var url = urlProp.GetString();
                var key = keyProp.GetString();

                if (!string.IsNullOrEmpty(url) && !string.IsNullOrEmpty(key))
                    return new CourierCallback(url, key, payload);
            }

            // No callback info — might be fire-and-forget or raw payload
            // Try to treat the whole body as the payload
            return new CourierCallback(null, null, root.Clone());
        }
        catch (JsonException)
        {
            return new CourierCallback(null, null, null);
        }
    }

    /// <summary>
    /// Reports successful completion to Courier.
    /// No-op if HasCallback is false.
    /// </summary>
    public async Task SuccessAsync(object? output = null, CancellationToken ct = default)
    {
        if (!HasCallback) return;
        await SendAsync(true, output, null, ct);
    }

    /// <summary>
    /// Reports failure to Courier.
    /// No-op if HasCallback is false.
    /// </summary>
    public async Task FailAsync(string errorMessage, CancellationToken ct = default)
    {
        if (!HasCallback) return;
        await SendAsync(false, null, errorMessage, ct);
    }

    private async Task SendAsync(bool success, object? output, string? errorMessage, CancellationToken ct)
    {
        using var client = new HttpClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, _callbackUrl);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _callbackKey);

        var body = new { success, output, errorMessage };
        var json = JsonSerializer.Serialize(body);
        request.Content = new StringContent(json, Encoding.UTF8, "application/json");

        await client.SendAsync(request, ct);
    }
}
```

- [ ] **Step 5: Add SDK to solution**

In `Courier.slnx`, add inside the `/src/` folder:

```xml
<Project Path="src/Courier.Functions.Sdk/Courier.Functions.Sdk.csproj" />
```

- [ ] **Step 6: Add SDK reference to unit test project**

In `tests/Courier.Tests.Unit/Courier.Tests.Unit.csproj`, add to the ProjectReference ItemGroup:

```xml
<ProjectReference Include="..\..\src\Courier.Functions.Sdk\Courier.Functions.Sdk.csproj" />
```

- [ ] **Step 7: Create SDK tests**

```csharp
using System.Text.Json;
using Courier.Functions.Sdk;
using Shouldly;

namespace Courier.Tests.Unit.Sdk;

public class CourierCallbackTests
{
    [Fact]
    public void FromBody_WithCallbackInfo_HasCallbackIsTrue()
    {
        var body = """
        {
            "payload": {"key": "value"},
            "callback": {
                "url": "https://courier.test/api/v1/callbacks/abc123",
                "key": "secret-key"
            }
        }
        """;

        var cb = CourierCallback.FromBody(body);

        cb.HasCallback.ShouldBeTrue();
        cb.Payload.ShouldNotBeNull();
        cb.Payload!.Value.GetProperty("key").GetString().ShouldBe("value");
    }

    [Fact]
    public void FromBody_WithoutCallbackInfo_HasCallbackIsFalse()
    {
        var body = """{"key": "value"}""";

        var cb = CourierCallback.FromBody(body);

        cb.HasCallback.ShouldBeFalse();
        cb.Payload.ShouldNotBeNull();
    }

    [Fact]
    public void FromBody_EmptyBody_HasCallbackIsFalse()
    {
        var cb = CourierCallback.FromBody("");

        cb.HasCallback.ShouldBeFalse();
        cb.Payload.ShouldBeNull();
    }

    [Fact]
    public void FromBody_MalformedJson_HasCallbackIsFalse()
    {
        var cb = CourierCallback.FromBody("not json");

        cb.HasCallback.ShouldBeFalse();
        cb.Payload.ShouldBeNull();
    }

    [Fact]
    public void FromBody_NullPayload_PayloadIsNull()
    {
        var body = """
        {
            "callback": {
                "url": "https://courier.test/api/v1/callbacks/abc123",
                "key": "secret-key"
            }
        }
        """;

        var cb = CourierCallback.FromBody(body);

        cb.HasCallback.ShouldBeTrue();
        cb.Payload.ShouldBeNull();
    }

    [Fact]
    public async Task SuccessAsync_NoCallback_IsNoOp()
    {
        var cb = CourierCallback.FromBody("{}");

        // Should not throw
        await cb.SuccessAsync(new { count = 42 });
    }

    [Fact]
    public async Task FailAsync_NoCallback_IsNoOp()
    {
        var cb = CourierCallback.FromBody("{}");

        // Should not throw
        await cb.FailAsync("some error");
    }
}
```

- [ ] **Step 8: Verify build and tests**

Run: `dotnet build Courier.slnx && dotnet test tests/Courier.Tests.Unit --filter "FullyQualifiedName~CourierCallbackTests"`
Expected: Build succeeds, all SDK tests pass

---

### Task 11: Frontend — Connection Form

**Files:**
- Modify: `src/Courier.Frontend/src/components/connections/connection-form.tsx`

- [ ] **Step 1: Remove Azure-specific fields from zod schema**

In `connection-form.tsx`, find the schema fields (around lines 63-66):

Remove these lines:
```typescript
  // Azure Function specific (stored in properties JSON)
  tenantId: z.string().max(100).optional(),
  clientId: z.string().max(100).optional(),
  workspaceId: z.string().max(100).optional(),
```

- [ ] **Step 2: Remove parseAzureProperties helper**

Find and remove the `parseAzureProperties` function (around lines 88-95):

```typescript
function parseAzureProperties(props?: string): { tenantId?: string; clientId?: string; workspaceId?: string } {
  if (!props) return {};
  try {
    return JSON.parse(props);
  } catch {
    return {};
  }
}
```

And remove its usage where `azureProps` is set from `connection?.properties`.

- [ ] **Step 3: Update auto-set logic for azure_function**

Find the useEffect that auto-sets auth method for azure_function (around lines 161-167):

Old:
```typescript
useEffect(() => {
    if (isAzureFunction) {
      setValue("authMethod", "service_principal");
      setValue("username", "service_principal");
    }
  }, [isAzureFunction, setValue]);
```

New:
```typescript
useEffect(() => {
    if (isAzureFunction) {
      setValue("authMethod", "function_key");
      setValue("username", "function_key");
    }
  }, [isAzureFunction, setValue]);
```

- [ ] **Step 4: Update submit handler — remove properties/clientSecret for Azure**

Find the submit handler `onSubmit` (around line 173-180). Replace the Azure properties mapping:

Old:
```typescript
const isAzure = values.protocol === "azure_function";
const properties = isAzure
  ? JSON.stringify({
      tenant_id: values.tenantId,
      client_id: values.clientId,
      workspace_id: values.workspaceId,
    })
  : undefined;
```

New:
```typescript
const isAzure = values.protocol === "azure_function";
```

And where `properties` and `clientSecret` are included in the request body, ensure they are excluded for Azure Function connections (set to `undefined`).

- [ ] **Step 5: Replace Azure Function Settings card**

Find the Azure Function Settings card section (around lines 316-399) and replace the entire card:

Old: Card with Master Key, Client Secret, Tenant ID, Client ID, Workspace ID fields.

New:
```tsx
{/* Azure Function Settings */}
{isAzureFunction && (
  <Card>
    <CardHeader>
      <CardTitle>Azure Function Settings</CardTitle>
    </CardHeader>
    <CardContent className="space-y-4">
      <div className="grid gap-1.5">
        <div className="flex items-center gap-1.5">
          <Label htmlFor="password">Function Key</Label>
          <FieldTooltip text="Found in Azure Portal: Function App > App Keys > Host Keys for app-wide access, or under a specific function's Function Keys for scoped access." />
        </div>
        <Input
          id="password"
          type="password"
          placeholder={isEdit ? "Leave blank to keep current key" : "Enter function key"}
          {...register("password")}
        />
        {errors.password && (
          <p className="text-sm text-destructive">{errors.password.message}</p>
        )}
      </div>
    </CardContent>
  </Card>
)}
```

- [ ] **Step 6: Update form defaultValues**

Find where `defaultValues` are set (around line 100-130). Remove references to `tenantId`, `clientId`, `workspaceId`, and `azureProps`:

Remove:
```typescript
tenantId: azureProps.tenantId ?? "",
clientId: azureProps.clientId ?? "",
workspaceId: azureProps.workspaceId ?? "",
```

- [ ] **Step 7: Verify TypeScript compiles**

Run: `cd src/Courier.Frontend && npx tsc --noEmit`
Expected: 0 errors

---

### Task 12: Frontend — Step Config + Trace Cleanup

**Files:**
- Modify: `src/Courier.Frontend/src/components/jobs/azure-function-step-config.tsx`
- Modify: `src/Courier.Frontend/src/components/jobs/step-constants.ts`
- Modify: `src/Courier.Frontend/src/components/jobs/execution-timeline.tsx`
- Modify: `src/Courier.Frontend/src/lib/api.ts`
- Modify: `src/Courier.Frontend/src/lib/types.ts`
- Delete: `src/Courier.Frontend/src/components/azure-function-trace-viewer.tsx`
- Delete: `src/Courier.Frontend/src/lib/hooks/use-azure-function-traces.ts`

- [ ] **Step 1: Update azure-function-step-config.tsx**

Replace the `AzureFunctionStepConfig` interface. Remove `initialDelaySec`, add `waitForCallback`:

```typescript
export interface AzureFunctionStepConfig {
  connectionId: string;
  functionName: string;
  inputPayload: string;
  waitForCallback: boolean;
  pollIntervalSec: number;
  maxWaitSec: number;
}
```

Update `parseAzureFunctionConfig`:
```typescript
export function parseAzureFunctionConfig(configJson: string): AzureFunctionStepConfig {
  try {
    const parsed = JSON.parse(configJson);
    return {
      connectionId: parsed.connection_id ?? "",
      functionName: parsed.function_name ?? "",
      inputPayload: parsed.input_payload ?? "",
      waitForCallback: parsed.wait_for_callback ?? true,
      pollIntervalSec: parsed.poll_interval_sec ?? 5,
      maxWaitSec: parsed.max_wait_sec ?? 3600,
    };
  } catch {
    return { connectionId: "", functionName: "", inputPayload: "", waitForCallback: true, pollIntervalSec: 5, maxWaitSec: 3600 };
  }
}
```

Update `serializeAzureFunctionConfig`:
```typescript
export function serializeAzureFunctionConfig(config: AzureFunctionStepConfig): string {
  return JSON.stringify({
    connection_id: config.connectionId,
    function_name: config.functionName,
    input_payload: config.inputPayload || undefined,
    wait_for_callback: config.waitForCallback,
    poll_interval_sec: config.pollIntervalSec,
    max_wait_sec: config.maxWaitSec,
  });
}
```

Update `AzureFunctionStepConfigForm` component — replace `initialDelaySec` field with `waitForCallback` toggle:

Add a Switch (or Checkbox) for "Wait for Callback" above the polling fields. When `waitForCallback` is false, hide `pollIntervalSec` and `maxWaitSec`:

```tsx
<div className="flex items-center justify-between">
  <div className="space-y-0.5">
    <Label htmlFor="waitForCallback">Wait for Callback</Label>
    <p className="text-xs text-muted-foreground">
      When enabled, the step waits for the function to report completion via callback. When disabled, the step succeeds immediately after triggering.
    </p>
  </div>
  <Switch
    id="waitForCallback"
    checked={config.waitForCallback}
    onCheckedChange={(checked) => onChange({ ...config, waitForCallback: checked })}
  />
</div>
```

Only show `pollIntervalSec` and `maxWaitSec` fields when `config.waitForCallback` is true.

Remove the `initialDelaySec` field entirely.

Add the Switch import at the top:
```typescript
import { Switch } from "@/components/ui/switch";
```

- [ ] **Step 2: Update step-constants.ts outputs**

Find the `azure_function.execute` outputs (around line 132):

Old:
```typescript
"azure_function.execute": [
    { key: "invocation_id", description: "Azure Function invocation ID", valueType: "string" },
    { key: "function_success", description: "Whether the function succeeded", valueType: "boolean" },
    { key: "function_duration_ms", description: "Function execution duration in milliseconds", valueType: "number" },
],
```

New:
```typescript
"azure_function.execute": [
    { key: "function_success", description: "Whether the function succeeded", valueType: "boolean" },
    { key: "callback_result", description: "Output payload from the Azure Function callback", valueType: "object" },
    { key: "http_status", description: "HTTP status code from the function trigger", valueType: "number" },
],
```

- [ ] **Step 3: Remove trace viewer from execution-timeline.tsx**

In `src/Courier.Frontend/src/components/jobs/execution-timeline.tsx`:

Remove the import (line 10):
```typescript
import { AzureFunctionTraceViewer } from "@/components/azure-function-trace-viewer";
```

In `StepExecutionRow` (around lines 280-318), remove the azure function trace logic. Remove:
```typescript
const invocationId = outputData?.invocation_id;
const connectionId = outputData?.connection_id;
const isAzureFunction = step.stepTypeKey === "azure_function.execute";
const hasTraceData = isAzureFunction && invocationId && connectionId;
```

Remove the `showTraces` state variable if it was only used for trace viewing.

Remove the "View Logs" button and the `AzureFunctionTraceViewer` rendering block.

- [ ] **Step 4: Delete trace viewer files**

```bash
rm src/Courier.Frontend/src/components/azure-function-trace-viewer.tsx
rm src/Courier.Frontend/src/lib/hooks/use-azure-function-traces.ts
```

- [ ] **Step 5: Remove getAzureFunctionTraces from api.ts**

In `src/Courier.Frontend/src/lib/api.ts`, find and remove:
```typescript
async getAzureFunctionTraces(connectionId: string, invocationId: string): Promise<ApiResponse<AzureFunctionTraceDto[]>> {
    return this.request(`/api/v1/azure-functions/${connectionId}/traces/${encodeURIComponent(invocationId)}`);
}
```

- [ ] **Step 6: Remove AzureFunctionTraceDto from types.ts**

In `src/Courier.Frontend/src/lib/types.ts`, find and remove:
```typescript
export interface AzureFunctionTraceDto {
  timestamp: string;
  message: string;
  severityLevel: number;
}
```

- [ ] **Step 7: Verify TypeScript compiles**

Run: `cd src/Courier.Frontend && npx tsc --noEmit`
Expected: 0 errors

Clear `.next` cache if stale references cause errors:
```bash
rm -rf src/Courier.Frontend/.next && cd src/Courier.Frontend && npx tsc --noEmit
```

---

### Task 13: Documentation

**Files:**
- Modify: `CLAUDE.md`

- [ ] **Step 1: Update CLAUDE.md**

Add `function_key` to the auth method reference. In the **Entities** or **Key Conventions** section, where auth methods are mentioned, add `function_key` to the list.

In the **API** section or a new **Configuration** subsection, add:
```markdown
### Configuration

- `Courier:BaseUrl` — Public base URL of the Courier API (e.g., `https://courier.example.com`). Required for Azure Function callback mode.
```

Update the **Job Engine** section step type keys list to include:
```markdown
- Step type keys: `"file.copy"`, `"sftp.upload"`, `"pgp.encrypt"`, `"azure_function.execute"`, etc.
```

Add a note about the callback endpoint:
```markdown
### Azure Function Callbacks

- `POST /api/v1/callbacks/{callbackId}` — Public endpoint (no JWT auth) for Azure Functions to report completion.
- Auth: One-time `Authorization: Bearer {callbackKey}` header.
- Used by `Courier.Functions.Sdk` NuGet package.
```

- [ ] **Step 2: Verify all builds pass**

Run: `dotnet build Courier.slnx && dotnet test tests/Courier.Tests.Unit && cd src/Courier.Frontend && npx tsc --noEmit`
Expected: Everything passes

---

## Verification Checklist

After all tasks are complete, verify:

1. `dotnet build Courier.slnx` — 0 errors
2. `dotnet test tests/Courier.Tests.Unit` — all pass
3. `dotnet test tests/Courier.Tests.Architecture` — all pass (dependency rules still hold)
4. `cd src/Courier.Frontend && npx tsc --noEmit` — 0 errors
5. No references to `AppInsightsQueryService`, `AzureFunctionClient`, `AzureFunctionDtos`, `AzureFunctionsController`, or `Azure.Identity` remain in the codebase
6. `grep -r "azure\.function" src/Courier.Features/Engine/StepTypeRegistry.cs` returns nothing (old key removed)
7. `grep -r "azure_function.execute" src/` returns matches in StepTypeRegistry, step handler, and frontend
