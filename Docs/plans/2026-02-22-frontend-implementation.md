# Job Management UI — Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Build the first functional Courier frontend with job CRUD, inline step builder, manual execution trigger, and live execution timeline — plus 3 new backend endpoints to support edit/delete.

**Architecture:** Hybrid sidebar+topbar shell layout. 4 pages (jobs list, create, detail, edit). TanStack Query for all API state. React Hook Form + Zod for forms. shadcn/ui component library on Tailwind CSS.

**Tech Stack:** Next.js 15, React 19, TypeScript, Tailwind CSS, shadcn/ui, TanStack Query, React Hook Form, Zod, Lucide React

**Design Doc:** `docs/plans/2026-02-22-frontend-design.md`

---

## Task 1: Backend — Add Update Job Endpoint

**Files:**
- Modify: `src/Courier.Features/Jobs/JobDto.cs`
- Modify: `src/Courier.Features/Jobs/JobService.cs`
- Modify: `src/Courier.Features/Jobs/JobsController.cs`
- Modify: `src/Courier.Features/Jobs/CreateJobValidator.cs`
- Test: `tests/Courier.Tests.Unit/Jobs/JobServiceTests.cs`
- Test: `tests/Courier.Tests.Integration/Jobs/JobsApiTests.cs`

**Step 1: Add UpdateJobRequest record to JobDto.cs**

```csharp
// Add after CreateJobRequest in JobDto.cs
public record UpdateJobRequest
{
    public required string Name { get; init; }
    public string? Description { get; init; }
}
```

**Step 2: Add UpdateJobValidator to CreateJobValidator.cs**

```csharp
// Add after CreateJobValidator class
public class UpdateJobValidator : AbstractValidator<UpdateJobRequest>
{
    public UpdateJobValidator()
    {
        RuleFor(x => x.Name).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Description).MaximumLength(2000);
    }
}
```

**Step 3: Write failing unit test for UpdateAsync**

Add to `tests/Courier.Tests.Unit/Jobs/JobServiceTests.cs`:

```csharp
[Fact]
public async Task UpdateAsync_ExistingJob_ReturnsUpdatedDto()
{
    // Arrange
    var job = new Job { Id = Guid.NewGuid(), Name = "Old Name", Description = "Old Desc" };
    _context.Jobs.Add(job);
    await _context.SaveChangesAsync();

    // Act
    var result = await _service.UpdateAsync(job.Id, "New Name", "New Desc");

    // Assert
    result.Data.ShouldNotBeNull();
    result.Data.Name.ShouldBe("New Name");
    result.Data.Description.ShouldBe("New Desc");
    result.Data.CurrentVersion.ShouldBe(2);
}

[Fact]
public async Task UpdateAsync_NonExistentJob_ReturnsNotFoundError()
{
    var result = await _service.UpdateAsync(Guid.NewGuid(), "Name", null);

    result.Success.ShouldBeFalse();
    result.Error!.Code.ShouldBe(ErrorCodes.ResourceNotFound);
}
```

**Step 4: Run tests to verify they fail**

Run: `dotnet test tests/Courier.Tests.Unit --filter "UpdateAsync" -v n`
Expected: FAIL — method `UpdateAsync` does not exist

**Step 5: Implement UpdateAsync in JobService.cs**

```csharp
public async Task<ApiResponse<JobDto>> UpdateAsync(Guid id, string name, string? description)
{
    var job = await _context.Jobs.FindAsync(id);

    if (job is null)
        return new ApiResponse<JobDto>
        {
            Error = ErrorMessages.Create(ErrorCodes.ResourceNotFound, $"Job {id} not found")
        };

    job.Name = name;
    job.Description = description;
    job.CurrentVersion++;
    job.UpdatedAt = DateTime.UtcNow;

    await _context.SaveChangesAsync();

    return new ApiResponse<JobDto> { Data = MapToDto(job) };
}
```

**Step 6: Run tests to verify they pass**

Run: `dotnet test tests/Courier.Tests.Unit --filter "UpdateAsync" -v n`
Expected: PASS

**Step 7: Add PUT endpoint to JobsController.cs**

```csharp
[HttpPut("{id:guid}")]
public async Task<ActionResult<ApiResponse<JobDto>>> Update(
    Guid id,
    [FromBody] UpdateJobRequest request,
    [FromServices] IValidator<UpdateJobRequest> validator)
{
    var validation = await validator.ValidateAsync(request);
    if (!validation.IsValid)
    {
        return BadRequest(new ApiResponse<JobDto>
        {
            Error = ErrorMessages.Create(
                ErrorCodes.ValidationFailed,
                "Validation failed",
                validation.Errors
                    .Select(e => new FieldError(e.PropertyName, e.ErrorMessage))
                    .ToList())
        });
    }

    var result = await _jobService.UpdateAsync(id, request.Name, request.Description);

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
```

**Step 8: Write integration test**

Add to `tests/Courier.Tests.Integration/Jobs/JobsApiTests.cs`:

```csharp
[Fact]
public async Task UpdateJob_ValidRequest_ReturnsUpdated()
{
    // Create a job first
    var createResponse = await _client.PostAsJsonAsync("/api/v1/jobs",
        new { name = "Original", description = "Original desc" });
    var created = await createResponse.Content.ReadFromJsonAsync<ApiResponse<JobDto>>();

    // Update it
    var updateResponse = await _client.PutAsJsonAsync($"/api/v1/jobs/{created!.Data!.Id}",
        new { name = "Updated", description = "Updated desc" });

    updateResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
    var updated = await updateResponse.Content.ReadFromJsonAsync<ApiResponse<JobDto>>();
    updated!.Data!.Name.ShouldBe("Updated");
    updated.Data.CurrentVersion.ShouldBe(2);
}
```

**Step 9: Run all tests**

Run: `dotnet test -v n`
Expected: All pass

**Step 10: Commit**

```bash
git add src/Courier.Features/Jobs/ tests/
git commit -m "feat: add PUT /api/v1/jobs/{id} update endpoint"
```

---

## Task 2: Backend — Add Delete Job Endpoint

**Files:**
- Modify: `src/Courier.Features/Jobs/JobService.cs`
- Modify: `src/Courier.Features/Jobs/JobsController.cs`
- Test: `tests/Courier.Tests.Unit/Jobs/JobServiceTests.cs`
- Test: `tests/Courier.Tests.Integration/Jobs/JobsApiTests.cs`

**Step 1: Write failing unit test for DeleteAsync**

```csharp
[Fact]
public async Task DeleteAsync_ExistingJob_SoftDeletes()
{
    var job = new Job { Id = Guid.NewGuid(), Name = "To Delete" };
    _context.Jobs.Add(job);
    await _context.SaveChangesAsync();

    var result = await _service.DeleteAsync(job.Id);

    result.Success.ShouldBeTrue();

    // Verify soft delete — bypass query filter
    var deleted = await _context.Jobs.IgnoreQueryFilters()
        .FirstOrDefaultAsync(j => j.Id == job.Id);
    deleted.ShouldNotBeNull();
    deleted.IsDeleted.ShouldBeTrue();
    deleted.DeletedAt.ShouldNotBeNull();
}

[Fact]
public async Task DeleteAsync_NonExistentJob_ReturnsNotFound()
{
    var result = await _service.DeleteAsync(Guid.NewGuid());

    result.Success.ShouldBeFalse();
    result.Error!.Code.ShouldBe(ErrorCodes.ResourceNotFound);
}
```

**Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Courier.Tests.Unit --filter "DeleteAsync" -v n`
Expected: FAIL

**Step 3: Implement DeleteAsync in JobService.cs**

```csharp
public async Task<ApiResponse> DeleteAsync(Guid id)
{
    var job = await _context.Jobs.FindAsync(id);

    if (job is null)
        return new ApiResponse
        {
            Error = ErrorMessages.Create(ErrorCodes.ResourceNotFound, $"Job {id} not found")
        };

    job.IsDeleted = true;
    job.DeletedAt = DateTime.UtcNow;

    await _context.SaveChangesAsync();

    return new ApiResponse();
}
```

**Step 4: Run tests to verify they pass**

Run: `dotnet test tests/Courier.Tests.Unit --filter "DeleteAsync" -v n`
Expected: PASS

**Step 5: Add DELETE endpoint to JobsController.cs**

```csharp
[HttpDelete("{id:guid}")]
public async Task<ActionResult<ApiResponse>> Delete(Guid id)
{
    var result = await _jobService.DeleteAsync(id);

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
```

**Step 6: Write integration test**

```csharp
[Fact]
public async Task DeleteJob_ExistingJob_SoftDeletes()
{
    var createResponse = await _client.PostAsJsonAsync("/api/v1/jobs",
        new { name = "To Delete" });
    var created = await createResponse.Content.ReadFromJsonAsync<ApiResponse<JobDto>>();

    var deleteResponse = await _client.DeleteAsync($"/api/v1/jobs/{created!.Data!.Id}");

    deleteResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

    // Verify it no longer shows in list
    var listResponse = await _client.GetAsync("/api/v1/jobs");
    var list = await listResponse.Content.ReadFromJsonAsync<PagedApiResponse<JobDto>>();
    list!.Data.ShouldNotContain(j => j.Id == created.Data.Id);
}
```

**Step 7: Run all tests**

Run: `dotnet test -v n`
Expected: All pass

**Step 8: Commit**

```bash
git add src/Courier.Features/Jobs/ tests/
git commit -m "feat: add DELETE /api/v1/jobs/{id} soft delete endpoint"
```

---

## Task 3: Backend — Add Replace Steps Endpoint

**Files:**
- Modify: `src/Courier.Features/Jobs/JobStepDto.cs`
- Modify: `src/Courier.Features/Jobs/JobStepService.cs`
- Modify: `src/Courier.Features/Jobs/JobsController.cs`
- Test: `tests/Courier.Tests.Unit/Jobs/JobStepServiceTests.cs` (create if needed)
- Test: `tests/Courier.Tests.Integration/Jobs/JobsApiTests.cs`

**Step 1: Add ReplaceJobStepsRequest to JobStepDto.cs**

```csharp
public record ReplaceJobStepsRequest
{
    public required List<StepInput> Steps { get; init; }
}

public record StepInput
{
    public required string Name { get; init; }
    public required string TypeKey { get; init; }
    public int StepOrder { get; init; }
    public string Configuration { get; init; } = "{}";
    public int TimeoutSeconds { get; init; } = 300;
}
```

**Step 2: Write failing test for ReplaceStepsAsync**

In unit test file:

```csharp
[Fact]
public async Task ReplaceStepsAsync_ReplacesAllSteps()
{
    var job = new Job { Id = Guid.NewGuid(), Name = "Test Job" };
    var oldStep = new JobStep
    {
        Id = Guid.NewGuid(), JobId = job.Id, Name = "Old Step",
        TypeKey = "file.copy", StepOrder = 1
    };
    _context.Jobs.Add(job);
    _context.JobSteps.Add(oldStep);
    await _context.SaveChangesAsync();

    var newSteps = new List<StepInput>
    {
        new() { Name = "New Step 1", TypeKey = "file.copy", StepOrder = 1 },
        new() { Name = "New Step 2", TypeKey = "file.move", StepOrder = 2 }
    };

    var result = await _service.ReplaceStepsAsync(job.Id, newSteps);

    result.Data.ShouldNotBeNull();
    result.Data.Count.ShouldBe(2);
    result.Data[0].Name.ShouldBe("New Step 1");
    result.Data[1].Name.ShouldBe("New Step 2");

    // Old step should be gone
    var allSteps = await _context.JobSteps.Where(s => s.JobId == job.Id).ToListAsync();
    allSteps.Count.ShouldBe(2);
}
```

**Step 3: Run tests to verify they fail**

Run: `dotnet test tests/Courier.Tests.Unit --filter "ReplaceStepsAsync" -v n`
Expected: FAIL

**Step 4: Implement ReplaceStepsAsync in JobStepService.cs**

```csharp
public async Task<ApiResponse<List<JobStepDto>>> ReplaceStepsAsync(Guid jobId, List<StepInput> steps)
{
    var job = await _context.Jobs.FindAsync(jobId);

    if (job is null)
        return new ApiResponse<List<JobStepDto>>
        {
            Error = ErrorMessages.Create(ErrorCodes.ResourceNotFound, $"Job {jobId} not found")
        };

    // Remove existing steps
    var existingSteps = await _context.JobSteps
        .Where(s => s.JobId == jobId)
        .ToListAsync();
    _context.JobSteps.RemoveRange(existingSteps);

    // Add new steps
    var newSteps = steps.Select(s => new JobStep
    {
        Id = Guid.NewGuid(),
        JobId = jobId,
        Name = s.Name,
        TypeKey = s.TypeKey,
        StepOrder = s.StepOrder,
        Configuration = s.Configuration,
        TimeoutSeconds = s.TimeoutSeconds
    }).ToList();

    _context.JobSteps.AddRange(newSteps);
    await _context.SaveChangesAsync();

    return new ApiResponse<List<JobStepDto>>
    {
        Data = newSteps.OrderBy(s => s.StepOrder).Select(MapToDto).ToList()
    };
}
```

**Step 5: Run tests to verify they pass**

Run: `dotnet test tests/Courier.Tests.Unit --filter "ReplaceStepsAsync" -v n`
Expected: PASS

**Step 6: Add PUT steps endpoint to JobsController.cs**

```csharp
[HttpPut("{jobId:guid}/steps")]
public async Task<ActionResult<ApiResponse<List<JobStepDto>>>> ReplaceSteps(
    Guid jobId,
    [FromBody] ReplaceJobStepsRequest request)
{
    var result = await _stepService.ReplaceStepsAsync(jobId, request.Steps);

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
```

**Step 7: Write integration test**

```csharp
[Fact]
public async Task ReplaceSteps_ValidRequest_ReplacesAllSteps()
{
    // Create job with a step
    var createResponse = await _client.PostAsJsonAsync("/api/v1/jobs",
        new { name = "Step Test Job" });
    var job = (await createResponse.Content.ReadFromJsonAsync<ApiResponse<JobDto>>())!.Data!;

    await _client.PostAsJsonAsync($"/api/v1/jobs/{job.Id}/steps",
        new { name = "Old Step", typeKey = "file.copy", stepOrder = 1 });

    // Replace with new steps
    var replaceResponse = await _client.PutAsJsonAsync($"/api/v1/jobs/{job.Id}/steps",
        new { steps = new[]
        {
            new { name = "New Step 1", typeKey = "file.copy", stepOrder = 1,
                  configuration = "{\"sourcePath\":\"/in\",\"destinationPath\":\"/out\"}" },
            new { name = "New Step 2", typeKey = "file.move", stepOrder = 2,
                  configuration = "{\"sourcePath\":\"/in\",\"destinationPath\":\"/archive\"}" }
        }});

    replaceResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
    var result = await replaceResponse.Content.ReadFromJsonAsync<ApiResponse<List<JobStepDto>>>();
    result!.Data!.Count.ShouldBe(2);
    result.Data[0].Name.ShouldBe("New Step 1");
}
```

**Step 8: Run all tests**

Run: `dotnet test -v n`
Expected: All pass

**Step 9: Commit**

```bash
git add src/Courier.Features/Jobs/ tests/
git commit -m "feat: add PUT /api/v1/jobs/{id}/steps atomic step replacement"
```

---

## Task 4: Frontend — Install Dependencies & Configure Tailwind + shadcn/ui

**Files:**
- Modify: `src/Courier.Frontend/package.json`
- Modify: `src/Courier.Frontend/next.config.ts`
- Create: `src/Courier.Frontend/postcss.config.mjs`
- Create: `src/Courier.Frontend/components.json`
- Modify: `src/Courier.Frontend/src/app/globals.css`
- Modify: `src/Courier.Frontend/tsconfig.json`

**Step 1: Install Tailwind CSS v4 + dependencies**

```bash
cd src/Courier.Frontend
npm install tailwindcss @tailwindcss/postcss postcss
```

**Step 2: Create postcss.config.mjs**

```javascript
/** @type {import('postcss-load-config').Config} */
const config = {
  plugins: {
    "@tailwindcss/postcss": {},
  },
};

export default config;
```

**Step 3: Create globals.css with Tailwind + shadcn theme**

Create `src/Courier.Frontend/src/app/globals.css`:

```css
@import "tailwindcss";

@custom-variant dark (&:is(.dark *));

:root {
  --background: oklch(1 0 0);
  --foreground: oklch(0.145 0 0);
  --card: oklch(1 0 0);
  --card-foreground: oklch(0.145 0 0);
  --popover: oklch(1 0 0);
  --popover-foreground: oklch(0.145 0 0);
  --primary: oklch(0.205 0 0);
  --primary-foreground: oklch(0.985 0 0);
  --secondary: oklch(0.97 0 0);
  --secondary-foreground: oklch(0.205 0 0);
  --muted: oklch(0.97 0 0);
  --muted-foreground: oklch(0.556 0 0);
  --accent: oklch(0.97 0 0);
  --accent-foreground: oklch(0.205 0 0);
  --destructive: oklch(0.577 0.245 27.325);
  --destructive-foreground: oklch(0.577 0.245 27.325);
  --border: oklch(0.922 0 0);
  --input: oklch(0.922 0 0);
  --ring: oklch(0.708 0 0);
  --radius: 0.625rem;
  --sidebar-background: oklch(0.985 0 0);
  --sidebar-foreground: oklch(0.145 0 0);
  --sidebar-primary: oklch(0.205 0 0);
  --sidebar-primary-foreground: oklch(0.985 0 0);
  --sidebar-accent: oklch(0.97 0 0);
  --sidebar-accent-foreground: oklch(0.205 0 0);
  --sidebar-border: oklch(0.922 0 0);
  --sidebar-ring: oklch(0.708 0 0);
}

@theme inline {
  --color-background: var(--background);
  --color-foreground: var(--foreground);
  --color-card: var(--card);
  --color-card-foreground: var(--card-foreground);
  --color-popover: var(--popover);
  --color-popover-foreground: var(--popover-foreground);
  --color-primary: var(--primary);
  --color-primary-foreground: var(--primary-foreground);
  --color-secondary: var(--secondary);
  --color-secondary-foreground: var(--secondary-foreground);
  --color-muted: var(--muted);
  --color-muted-foreground: var(--muted-foreground);
  --color-accent: var(--accent);
  --color-accent-foreground: var(--accent-foreground);
  --color-destructive: var(--destructive);
  --color-destructive-foreground: var(--destructive-foreground);
  --color-border: var(--border);
  --color-input: var(--input);
  --color-ring: var(--ring);
  --color-sidebar-background: var(--sidebar-background);
  --color-sidebar-foreground: var(--sidebar-foreground);
  --color-sidebar-primary: var(--sidebar-primary);
  --color-sidebar-primary-foreground: var(--sidebar-primary-foreground);
  --color-sidebar-accent: var(--sidebar-accent);
  --color-sidebar-accent-foreground: var(--sidebar-accent-foreground);
  --color-sidebar-border: var(--sidebar-border);
  --color-sidebar-ring: var(--sidebar-ring);
  --radius-sm: calc(var(--radius) - 4px);
  --radius-md: calc(var(--radius) - 2px);
  --radius-lg: var(--radius);
  --radius-xl: calc(var(--radius) + 4px);
}

@layer base {
  * {
    @apply border-border;
  }
  body {
    @apply bg-background text-foreground;
  }
}
```

**Step 4: Install shadcn/ui CLI and initialize**

```bash
cd src/Courier.Frontend
npx shadcn@latest init -d
```

This creates `components.json` and `lib/utils.ts`. Verify the output. If it asks questions, choose: New York style, neutral color, CSS variables.

**Step 5: Install required shadcn components**

```bash
cd src/Courier.Frontend
npx shadcn@latest add button input label table dialog badge dropdown-menu skeleton toast sonner accordion card separator
```

**Step 6: Install remaining dependencies**

```bash
cd src/Courier.Frontend
npm install @tanstack/react-query react-hook-form @hookform/resolvers zod lucide-react
```

**Step 7: Verify build compiles**

```bash
cd src/Courier.Frontend
npm run build
```

Expected: Build succeeds (may have warnings, no errors)

**Step 8: Commit**

```bash
git add src/Courier.Frontend/
git commit -m "chore: install Tailwind, shadcn/ui, TanStack Query, React Hook Form, Zod, Lucide"
```

---

## Task 5: Frontend — API Client, Types & Query Setup

**Files:**
- Create: `src/Courier.Frontend/src/lib/api.ts`
- Create: `src/Courier.Frontend/src/lib/types.ts`
- Create: `src/Courier.Frontend/src/lib/query-client.ts`

**Step 1: Create types.ts**

```typescript
// API envelope types — mirror backend ApiResponse<T>
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

export interface ApiResponse<T> {
  data?: T;
  error?: ApiError;
  timestamp: string;
  success: boolean;
}

export interface PaginationMeta {
  page: number;
  pageSize: number;
  totalCount: number;
  totalPages: number;
}

export interface PagedApiResponse<T> {
  data: T[];
  pagination: PaginationMeta;
  error?: ApiError;
  timestamp: string;
  success: boolean;
}

// Domain DTOs
export interface JobDto {
  id: string;
  name: string;
  description?: string;
  currentVersion: number;
  isEnabled: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface JobStepDto {
  id: string;
  jobId: string;
  stepOrder: number;
  name: string;
  typeKey: string;
  configuration: string;
  timeoutSeconds: number;
}

export interface JobExecutionDto {
  id: string;
  jobId: string;
  state: string;
  triggeredBy: string;
  queuedAt?: string;
  startedAt?: string;
  completedAt?: string;
  createdAt: string;
}

export interface StepExecutionDto {
  id: string;
  jobExecutionId: string;
  jobStepId: string;
  stepOrder: number;
  state: string;
  startedAt?: string;
  completedAt?: string;
  durationMs?: number;
  bytesProcessed?: number;
  outputData?: string;
  errorMessage?: string;
  retryAttempt: number;
  createdAt: string;
}

// Request types
export interface CreateJobRequest {
  name: string;
  description?: string;
}

export interface UpdateJobRequest {
  name: string;
  description?: string;
}

export interface StepInput {
  name: string;
  typeKey: string;
  stepOrder: number;
  configuration: string;
  timeoutSeconds: number;
}

export interface ReplaceJobStepsRequest {
  steps: StepInput[];
}

export interface TriggerJobRequest {
  triggeredBy: string;
}
```

**Step 2: Create api.ts**

```typescript
import type {
  ApiResponse,
  PagedApiResponse,
  JobDto,
  JobStepDto,
  JobExecutionDto,
  CreateJobRequest,
  UpdateJobRequest,
  ReplaceJobStepsRequest,
  TriggerJobRequest,
} from "./types";

const API_URL = process.env.NEXT_PUBLIC_API_URL || "http://localhost:5000";

class ApiClient {
  private baseUrl: string;

  constructor(baseUrl: string) {
    this.baseUrl = baseUrl;
  }

  private async request<T>(path: string, options?: RequestInit): Promise<T> {
    const response = await fetch(`${this.baseUrl}${path}`, {
      headers: { "Content-Type": "application/json", ...options?.headers },
      ...options,
    });

    const body = await response.json();

    if (!response.ok && !body.error) {
      throw new Error(`HTTP ${response.status}: ${response.statusText}`);
    }

    if (body.error) {
      throw new ApiError(body.error);
    }

    return body;
  }

  // Jobs
  async listJobs(page = 1, pageSize = 10): Promise<PagedApiResponse<JobDto>> {
    return this.request(`/api/v1/jobs?page=${page}&pageSize=${pageSize}`);
  }

  async getJob(id: string): Promise<ApiResponse<JobDto>> {
    return this.request(`/api/v1/jobs/${id}`);
  }

  async createJob(data: CreateJobRequest): Promise<ApiResponse<JobDto>> {
    return this.request("/api/v1/jobs", {
      method: "POST",
      body: JSON.stringify(data),
    });
  }

  async updateJob(id: string, data: UpdateJobRequest): Promise<ApiResponse<JobDto>> {
    return this.request(`/api/v1/jobs/${id}`, {
      method: "PUT",
      body: JSON.stringify(data),
    });
  }

  async deleteJob(id: string): Promise<ApiResponse<void>> {
    return this.request(`/api/v1/jobs/${id}`, { method: "DELETE" });
  }

  // Steps
  async listSteps(jobId: string): Promise<ApiResponse<JobStepDto[]>> {
    return this.request(`/api/v1/jobs/${jobId}/steps`);
  }

  async replaceSteps(jobId: string, data: ReplaceJobStepsRequest): Promise<ApiResponse<JobStepDto[]>> {
    return this.request(`/api/v1/jobs/${jobId}/steps`, {
      method: "PUT",
      body: JSON.stringify(data),
    });
  }

  // Executions
  async listExecutions(jobId: string, page = 1, pageSize = 10): Promise<PagedApiResponse<JobExecutionDto>> {
    return this.request(`/api/v1/jobs/${jobId}/executions?page=${page}&pageSize=${pageSize}`);
  }

  async getExecution(executionId: string): Promise<ApiResponse<JobExecutionDto>> {
    return this.request(`/api/v1/jobs/executions/${executionId}`);
  }

  async triggerJob(jobId: string, data: TriggerJobRequest = { triggeredBy: "ui" }): Promise<ApiResponse<JobExecutionDto>> {
    return this.request(`/api/v1/jobs/${jobId}/trigger`, {
      method: "POST",
      body: JSON.stringify(data),
    });
  }
}

export class ApiError extends Error {
  code: number;
  systemMessage: string;
  details?: { field: string; message: string }[];

  constructor(error: { code: number; systemMessage: string; message: string; details?: { field: string; message: string }[] }) {
    super(error.message);
    this.name = "ApiError";
    this.code = error.code;
    this.systemMessage = error.systemMessage;
    this.details = error.details;
  }
}

export const api = new ApiClient(API_URL);
```

**Step 3: Create query-client.ts**

```typescript
import { QueryClient } from "@tanstack/react-query";

export function makeQueryClient() {
  return new QueryClient({
    defaultOptions: {
      queries: {
        staleTime: 30 * 1000, // 30 seconds
        retry: 1,
      },
    },
  });
}

let browserQueryClient: QueryClient | undefined;

export function getQueryClient() {
  if (typeof window === "undefined") {
    return makeQueryClient();
  }
  if (!browserQueryClient) {
    browserQueryClient = makeQueryClient();
  }
  return browserQueryClient;
}
```

**Step 4: Commit**

```bash
git add src/Courier.Frontend/src/lib/
git commit -m "feat: add API client, TypeScript types, and QueryClient setup"
```

---

## Task 6: Frontend — TanStack Query Hooks

**Files:**
- Create: `src/Courier.Frontend/src/lib/hooks/use-jobs.ts`
- Create: `src/Courier.Frontend/src/lib/hooks/use-job-steps.ts`
- Create: `src/Courier.Frontend/src/lib/hooks/use-job-executions.ts`
- Create: `src/Courier.Frontend/src/lib/hooks/use-job-mutations.ts`

**Step 1: Create use-jobs.ts**

```typescript
import { useQuery } from "@tanstack/react-query";
import { api } from "../api";

export function useJobs(page: number, pageSize = 10) {
  return useQuery({
    queryKey: ["jobs", page, pageSize],
    queryFn: () => api.listJobs(page, pageSize),
  });
}

export function useJob(id: string) {
  return useQuery({
    queryKey: ["jobs", id],
    queryFn: () => api.getJob(id),
    enabled: !!id,
  });
}
```

**Step 2: Create use-job-steps.ts**

```typescript
import { useQuery } from "@tanstack/react-query";
import { api } from "../api";

export function useJobSteps(jobId: string) {
  return useQuery({
    queryKey: ["jobs", jobId, "steps"],
    queryFn: () => api.listSteps(jobId),
    enabled: !!jobId,
  });
}
```

**Step 3: Create use-job-executions.ts**

```typescript
import { useQuery } from "@tanstack/react-query";
import { api } from "../api";

export function useJobExecutions(jobId: string, page = 1, pageSize = 10) {
  return useQuery({
    queryKey: ["jobs", jobId, "executions", page, pageSize],
    queryFn: () => api.listExecutions(jobId, page, pageSize),
    enabled: !!jobId,
  });
}

export function useExecution(executionId: string, enabled = true) {
  return useQuery({
    queryKey: ["executions", executionId],
    queryFn: () => api.getExecution(executionId),
    enabled: !!executionId && enabled,
    refetchInterval: (query) => {
      const state = query.state.data?.data?.state;
      if (state === "queued" || state === "running") {
        return 2000;
      }
      return false;
    },
  });
}
```

**Step 4: Create use-job-mutations.ts**

```typescript
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { api } from "../api";
import type { CreateJobRequest, UpdateJobRequest, ReplaceJobStepsRequest } from "../types";

export function useCreateJob() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: CreateJobRequest) => api.createJob(data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["jobs"] });
    },
  });
}

export function useUpdateJob(id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: UpdateJobRequest) => api.updateJob(id, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["jobs"] });
    },
  });
}

export function useDeleteJob() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => api.deleteJob(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["jobs"] });
    },
  });
}

export function useReplaceSteps(jobId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (data: ReplaceJobStepsRequest) => api.replaceSteps(jobId, data),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["jobs", jobId, "steps"] });
    },
  });
}

export function useTriggerJob(jobId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: () => api.triggerJob(jobId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ["jobs", jobId, "executions"] });
    },
  });
}
```

**Step 5: Commit**

```bash
git add src/Courier.Frontend/src/lib/hooks/
git commit -m "feat: add TanStack Query hooks for jobs, steps, and executions"
```

---

## Task 7: Frontend — Shell Layout (Sidebar + Topbar)

**Files:**
- Create: `src/Courier.Frontend/src/components/layout/sidebar.tsx`
- Create: `src/Courier.Frontend/src/components/layout/topbar.tsx`
- Create: `src/Courier.Frontend/src/components/layout/shell.tsx`
- Create: `src/Courier.Frontend/src/components/providers.tsx`
- Modify: `src/Courier.Frontend/src/app/layout.tsx`
- Modify: `src/Courier.Frontend/src/app/page.tsx` (redirect to /jobs)

**Step 1: Create providers.tsx** (QueryClientProvider wrapper)

```typescript
"use client";

import { QueryClientProvider } from "@tanstack/react-query";
import { getQueryClient } from "@/lib/query-client";

export function Providers({ children }: { children: React.ReactNode }) {
  const queryClient = getQueryClient();
  return (
    <QueryClientProvider client={queryClient}>
      {children}
    </QueryClientProvider>
  );
}
```

**Step 2: Create sidebar.tsx**

```typescript
"use client";

import Link from "next/link";
import { usePathname } from "next/navigation";
import {
  Briefcase,
  Cable,
  KeyRound,
  Eye,
  FileText,
  Settings,
  ChevronLeft,
  ChevronRight,
  Package,
} from "lucide-react";
import { cn } from "@/lib/utils";
import { useState } from "react";
import { Button } from "@/components/ui/button";

const navItems = [
  { label: "Jobs", href: "/jobs", icon: Briefcase, active: true },
  { label: "Connections", href: "#", icon: Cable, active: false },
  { label: "Keys", href: "#", icon: KeyRound, active: false },
  { label: "Monitors", href: "#", icon: Eye, active: false },
  { label: "Audit", href: "#", icon: FileText, active: false },
];

const bottomItems = [
  { label: "Settings", href: "#", icon: Settings, active: false },
];

export function Sidebar() {
  const pathname = usePathname();
  const [collapsed, setCollapsed] = useState(false);

  return (
    <aside
      className={cn(
        "flex flex-col border-r border-sidebar-border bg-sidebar-background transition-all duration-200",
        collapsed ? "w-16" : "w-56"
      )}
    >
      <div className="flex h-14 items-center border-b border-sidebar-border px-4">
        {!collapsed && (
          <div className="flex items-center gap-2">
            <Package className="h-5 w-5 text-sidebar-primary" />
            <span className="font-semibold text-sidebar-foreground">Courier</span>
          </div>
        )}
        {collapsed && <Package className="mx-auto h-5 w-5 text-sidebar-primary" />}
      </div>

      <nav className="flex-1 space-y-1 p-2">
        {navItems.map((item) => {
          const isActive = item.active && pathname.startsWith(item.href);
          return (
            <Link
              key={item.label}
              href={item.active ? item.href : "#"}
              className={cn(
                "flex items-center gap-3 rounded-md px-3 py-2 text-sm font-medium transition-colors",
                isActive
                  ? "bg-sidebar-accent text-sidebar-accent-foreground"
                  : item.active
                    ? "text-sidebar-foreground hover:bg-sidebar-accent hover:text-sidebar-accent-foreground"
                    : "cursor-not-allowed text-muted-foreground opacity-50"
              )}
              title={!item.active ? "Coming Soon" : undefined}
              onClick={!item.active ? (e) => e.preventDefault() : undefined}
            >
              <item.icon className="h-4 w-4 shrink-0" />
              {!collapsed && <span>{item.label}</span>}
              {!collapsed && !item.active && (
                <span className="ml-auto text-xs text-muted-foreground">Soon</span>
              )}
            </Link>
          );
        })}
      </nav>

      <div className="border-t border-sidebar-border p-2">
        {bottomItems.map((item) => (
          <Link
            key={item.label}
            href={item.active ? item.href : "#"}
            className="flex items-center gap-3 rounded-md px-3 py-2 text-sm font-medium text-muted-foreground opacity-50 cursor-not-allowed"
            title="Coming Soon"
            onClick={(e) => e.preventDefault()}
          >
            <item.icon className="h-4 w-4 shrink-0" />
            {!collapsed && <span>{item.label}</span>}
          </Link>
        ))}

        <Button
          variant="ghost"
          size="sm"
          className="mt-2 w-full"
          onClick={() => setCollapsed(!collapsed)}
        >
          {collapsed ? <ChevronRight className="h-4 w-4" /> : <ChevronLeft className="h-4 w-4" />}
          {!collapsed && <span className="ml-2">Collapse</span>}
        </Button>
      </div>
    </aside>
  );
}
```

**Step 3: Create topbar.tsx**

```typescript
"use client";

import { usePathname } from "next/navigation";
import Link from "next/link";
import { ChevronRight } from "lucide-react";

function useBreadcrumbs() {
  const pathname = usePathname();
  const segments = pathname.split("/").filter(Boolean);

  const crumbs: { label: string; href: string }[] = [];

  for (let i = 0; i < segments.length; i++) {
    const href = "/" + segments.slice(0, i + 1).join("/");
    let label = segments[i];

    // Prettify known segments
    if (label === "jobs") label = "Jobs";
    else if (label === "new") label = "Create";
    else if (label === "edit") label = "Edit";
    else if (label.match(/^[0-9a-f-]{36}$/)) label = "Detail";

    crumbs.push({ label, href });
  }

  return crumbs;
}

export function Topbar() {
  const crumbs = useBreadcrumbs();

  return (
    <header className="flex h-14 items-center border-b bg-background px-6">
      <nav className="flex items-center gap-1 text-sm text-muted-foreground">
        {crumbs.map((crumb, i) => (
          <span key={crumb.href} className="flex items-center gap-1">
            {i > 0 && <ChevronRight className="h-3 w-3" />}
            {i < crumbs.length - 1 ? (
              <Link href={crumb.href} className="hover:text-foreground transition-colors">
                {crumb.label}
              </Link>
            ) : (
              <span className="text-foreground font-medium">{crumb.label}</span>
            )}
          </span>
        ))}
      </nav>
    </header>
  );
}
```

**Step 4: Create shell.tsx**

```typescript
import { Sidebar } from "./sidebar";
import { Topbar } from "./topbar";

export function Shell({ children }: { children: React.ReactNode }) {
  return (
    <div className="flex h-screen overflow-hidden">
      <Sidebar />
      <div className="flex flex-1 flex-col overflow-hidden">
        <Topbar />
        <main className="flex-1 overflow-y-auto p-6">
          <div className="mx-auto max-w-5xl">{children}</div>
        </main>
      </div>
    </div>
  );
}
```

**Step 5: Update layout.tsx**

Replace `src/Courier.Frontend/src/app/layout.tsx`:

```typescript
import type { Metadata } from "next";
import { Providers } from "@/components/providers";
import { Shell } from "@/components/layout/shell";
import { Toaster } from "@/components/ui/sonner";
import "./globals.css";

export const metadata: Metadata = {
  title: "Courier",
  description: "Enterprise file transfer & job management",
};

export default function RootLayout({
  children,
}: Readonly<{
  children: React.ReactNode;
}>) {
  return (
    <html lang="en">
      <body className="antialiased">
        <Providers>
          <Shell>{children}</Shell>
          <Toaster />
        </Providers>
      </body>
    </html>
  );
}
```

**Step 6: Update page.tsx to redirect to /jobs**

Replace `src/Courier.Frontend/src/app/page.tsx`:

```typescript
import { redirect } from "next/navigation";

export default function Home() {
  redirect("/jobs");
}
```

**Step 7: Verify build**

```bash
cd src/Courier.Frontend && npm run build
```

**Step 8: Commit**

```bash
git add src/Courier.Frontend/
git commit -m "feat: add shell layout with sidebar, topbar, breadcrumbs, and providers"
```

---

## Task 8: Frontend — Shared Components

**Files:**
- Create: `src/Courier.Frontend/src/components/shared/status-badge.tsx`
- Create: `src/Courier.Frontend/src/components/shared/confirm-dialog.tsx`
- Create: `src/Courier.Frontend/src/components/shared/empty-state.tsx`

**Step 1: Create status-badge.tsx**

```typescript
import { Badge } from "@/components/ui/badge";
import { cn } from "@/lib/utils";

const stateStyles: Record<string, string> = {
  created: "bg-gray-100 text-gray-700",
  queued: "bg-gray-100 text-gray-700",
  running: "bg-blue-100 text-blue-700",
  completed: "bg-green-100 text-green-700",
  failed: "bg-red-100 text-red-700",
  cancelled: "bg-gray-100 text-gray-500",
  pending: "bg-gray-100 text-gray-700",
  skipped: "bg-gray-100 text-gray-500",
};

const stateIcons: Record<string, string> = {
  created: "\u25cb",
  queued: "\u25cb",
  running: "\u25cf",
  completed: "\u2713",
  failed: "\u2717",
  cancelled: "\u2298",
  pending: "\u25cb",
  skipped: "\u2014",
};

interface StatusBadgeProps {
  state: string;
  className?: string;
  pulse?: boolean;
}

export function StatusBadge({ state, className, pulse }: StatusBadgeProps) {
  const normalized = state.toLowerCase();
  return (
    <Badge
      variant="outline"
      className={cn(
        "gap-1 font-medium border-0",
        stateStyles[normalized] || "bg-gray-100 text-gray-700",
        pulse && normalized === "running" && "animate-pulse",
        className
      )}
    >
      <span>{stateIcons[normalized] || "\u25cf"}</span>
      <span className="capitalize">{normalized}</span>
    </Badge>
  );
}
```

**Step 2: Create confirm-dialog.tsx**

```typescript
"use client";

import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from "@/components/ui/dialog";
import { Button } from "@/components/ui/button";

interface ConfirmDialogProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  title: string;
  description: string;
  confirmLabel?: string;
  variant?: "default" | "destructive";
  loading?: boolean;
  onConfirm: () => void;
}

export function ConfirmDialog({
  open,
  onOpenChange,
  title,
  description,
  confirmLabel = "Confirm",
  variant = "default",
  loading = false,
  onConfirm,
}: ConfirmDialogProps) {
  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{title}</DialogTitle>
          <DialogDescription>{description}</DialogDescription>
        </DialogHeader>
        <DialogFooter>
          <Button variant="outline" onClick={() => onOpenChange(false)} disabled={loading}>
            Cancel
          </Button>
          <Button variant={variant} onClick={onConfirm} disabled={loading}>
            {loading ? "..." : confirmLabel}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
```

**Step 3: Create empty-state.tsx**

```typescript
import { Button } from "@/components/ui/button";
import Link from "next/link";

interface EmptyStateProps {
  title: string;
  description: string;
  actionLabel?: string;
  actionHref?: string;
}

export function EmptyState({ title, description, actionLabel, actionHref }: EmptyStateProps) {
  return (
    <div className="flex flex-col items-center justify-center rounded-lg border border-dashed p-12 text-center">
      <h3 className="text-lg font-medium">{title}</h3>
      <p className="mt-1 text-sm text-muted-foreground">{description}</p>
      {actionLabel && actionHref && (
        <Button asChild className="mt-4">
          <Link href={actionHref}>{actionLabel}</Link>
        </Button>
      )}
    </div>
  );
}
```

**Step 4: Commit**

```bash
git add src/Courier.Frontend/src/components/shared/
git commit -m "feat: add shared components — StatusBadge, ConfirmDialog, EmptyState"
```

---

## Task 9: Frontend — Jobs List Page (`/jobs`)

**Files:**
- Create: `src/Courier.Frontend/src/app/jobs/page.tsx`
- Create: `src/Courier.Frontend/src/components/jobs/job-table.tsx`

**Step 1: Create job-table.tsx**

```typescript
"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from "@/components/ui/table";
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuTrigger,
} from "@/components/ui/dropdown-menu";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { MoreHorizontal, Pencil, Play, Trash2 } from "lucide-react";
import { ConfirmDialog } from "@/components/shared/confirm-dialog";
import { useDeleteJob, useTriggerJob } from "@/lib/hooks/use-job-mutations";
import { toast } from "sonner";
import type { JobDto } from "@/lib/types";
import Link from "next/link";

function timeAgo(dateStr: string): string {
  const seconds = Math.floor((Date.now() - new Date(dateStr).getTime()) / 1000);
  if (seconds < 60) return "just now";
  const minutes = Math.floor(seconds / 60);
  if (minutes < 60) return `${minutes}m ago`;
  const hours = Math.floor(minutes / 60);
  if (hours < 24) return `${hours}h ago`;
  const days = Math.floor(hours / 24);
  return `${days}d ago`;
}

interface JobTableProps {
  jobs: JobDto[];
}

export function JobTable({ jobs }: JobTableProps) {
  const router = useRouter();
  const deleteJob = useDeleteJob();
  const [deleteTarget, setDeleteTarget] = useState<JobDto | null>(null);
  const [runTarget, setRunTarget] = useState<JobDto | null>(null);

  function handleDelete() {
    if (!deleteTarget) return;
    deleteJob.mutate(deleteTarget.id, {
      onSuccess: () => {
        toast.success("Job deleted");
        setDeleteTarget(null);
      },
      onError: (error) => {
        toast.error(error.message);
      },
    });
  }

  return (
    <>
      <Table>
        <TableHeader>
          <TableRow>
            <TableHead>Name</TableHead>
            <TableHead>Description</TableHead>
            <TableHead>Version</TableHead>
            <TableHead>Enabled</TableHead>
            <TableHead>Created</TableHead>
            <TableHead className="w-10" />
          </TableRow>
        </TableHeader>
        <TableBody>
          {jobs.map((job) => (
            <TableRow key={job.id}>
              <TableCell>
                <Link
                  href={`/jobs/${job.id}`}
                  className="font-medium text-primary hover:underline"
                >
                  {job.name}
                </Link>
              </TableCell>
              <TableCell className="text-muted-foreground max-w-[200px] truncate">
                {job.description || "\u2014"}
              </TableCell>
              <TableCell>
                <Badge variant="secondary">v{job.currentVersion}</Badge>
              </TableCell>
              <TableCell>
                <Badge variant={job.isEnabled ? "default" : "secondary"}>
                  {job.isEnabled ? "Yes" : "No"}
                </Badge>
              </TableCell>
              <TableCell className="text-muted-foreground">
                {timeAgo(job.createdAt)}
              </TableCell>
              <TableCell>
                <DropdownMenu>
                  <DropdownMenuTrigger asChild>
                    <Button variant="ghost" size="icon" className="h-8 w-8">
                      <MoreHorizontal className="h-4 w-4" />
                    </Button>
                  </DropdownMenuTrigger>
                  <DropdownMenuContent align="end">
                    <DropdownMenuItem onClick={() => router.push(`/jobs/${job.id}/edit`)}>
                      <Pencil className="mr-2 h-4 w-4" />
                      Edit
                    </DropdownMenuItem>
                    <DropdownMenuItem onClick={() => setRunTarget(job)}>
                      <Play className="mr-2 h-4 w-4" />
                      Run
                    </DropdownMenuItem>
                    <DropdownMenuItem
                      onClick={() => setDeleteTarget(job)}
                      className="text-destructive"
                    >
                      <Trash2 className="mr-2 h-4 w-4" />
                      Delete
                    </DropdownMenuItem>
                  </DropdownMenuContent>
                </DropdownMenu>
              </TableCell>
            </TableRow>
          ))}
        </TableBody>
      </Table>

      <ConfirmDialog
        open={!!deleteTarget}
        onOpenChange={(open) => !open && setDeleteTarget(null)}
        title="Delete Job"
        description={`Are you sure you want to delete "${deleteTarget?.name}"? This action cannot be undone.`}
        confirmLabel="Delete"
        variant="destructive"
        loading={deleteJob.isPending}
        onConfirm={handleDelete}
      />

      {runTarget && (
        <RunDialog job={runTarget} onClose={() => setRunTarget(null)} />
      )}
    </>
  );
}

function RunDialog({ job, onClose }: { job: JobDto; onClose: () => void }) {
  const trigger = useTriggerJob(job.id);
  const router = useRouter();

  function handleRun() {
    trigger.mutate(undefined, {
      onSuccess: (data) => {
        toast.success("Job queued");
        onClose();
        router.push(`/jobs/${job.id}`);
      },
      onError: (error) => {
        toast.error(error.message);
      },
    });
  }

  return (
    <ConfirmDialog
      open
      onOpenChange={(open) => !open && onClose()}
      title="Run Job"
      description={`Run "${job.name}" now?`}
      confirmLabel="Run"
      loading={trigger.isPending}
      onConfirm={handleRun}
    />
  );
}
```

**Step 2: Create jobs list page**

Create `src/Courier.Frontend/src/app/jobs/page.tsx`:

```typescript
"use client";

import { useState } from "react";
import Link from "next/link";
import { Plus } from "lucide-react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Skeleton } from "@/components/ui/skeleton";
import { JobTable } from "@/components/jobs/job-table";
import { EmptyState } from "@/components/shared/empty-state";
import { useJobs } from "@/lib/hooks/use-jobs";

export default function JobsPage() {
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState("");
  const pageSize = 10;
  const { data, isLoading } = useJobs(page, pageSize);

  const jobs = data?.data ?? [];
  const pagination = data?.pagination;
  const filteredJobs = search
    ? jobs.filter((j) => j.name.toLowerCase().includes(search.toLowerCase()))
    : jobs;

  return (
    <div className="space-y-6">
      <div className="flex items-center justify-between">
        <h1 className="text-2xl font-bold">Jobs</h1>
        <Button asChild>
          <Link href="/jobs/new">
            <Plus className="mr-2 h-4 w-4" />
            Create Job
          </Link>
        </Button>
      </div>

      {isLoading ? (
        <div className="space-y-3">
          <Skeleton className="h-10 w-full" />
          <Skeleton className="h-64 w-full" />
        </div>
      ) : jobs.length === 0 ? (
        <EmptyState
          title="No jobs yet"
          description="Create your first job to get started."
          actionLabel="Create Job"
          actionHref="/jobs/new"
        />
      ) : (
        <>
          <Input
            placeholder="Search jobs..."
            value={search}
            onChange={(e) => setSearch(e.target.value)}
            className="max-w-sm"
          />

          <JobTable jobs={filteredJobs} />

          {pagination && pagination.totalPages > 1 && (
            <div className="flex items-center justify-center gap-2">
              <Button
                variant="outline"
                size="sm"
                disabled={page <= 1}
                onClick={() => setPage((p) => p - 1)}
              >
                Previous
              </Button>
              <span className="text-sm text-muted-foreground">
                Page {pagination.page} of {pagination.totalPages}
              </span>
              <Button
                variant="outline"
                size="sm"
                disabled={page >= pagination.totalPages}
                onClick={() => setPage((p) => p + 1)}
              >
                Next
              </Button>
            </div>
          )}
        </>
      )}
    </div>
  );
}
```

**Step 3: Verify build**

```bash
cd src/Courier.Frontend && npm run build
```

**Step 4: Commit**

```bash
git add src/Courier.Frontend/src/
git commit -m "feat: add jobs list page with data table, search, pagination, and row actions"
```

---

## Task 10: Frontend — Job Create/Edit Form

**Files:**
- Create: `src/Courier.Frontend/src/components/jobs/step-config-form.tsx`
- Create: `src/Courier.Frontend/src/components/jobs/step-builder.tsx`
- Create: `src/Courier.Frontend/src/components/jobs/job-form.tsx`
- Create: `src/Courier.Frontend/src/app/jobs/new/page.tsx`
- Create: `src/Courier.Frontend/src/app/jobs/[id]/edit/page.tsx`

**Step 1: Create step-config-form.tsx**

```typescript
"use client";

import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";

interface StepConfig {
  sourcePath: string;
  destinationPath: string;
  overwrite: boolean;
}

interface StepConfigFormProps {
  typeKey: string;
  config: StepConfig;
  onChange: (config: StepConfig) => void;
}

export function StepConfigForm({ typeKey, config, onChange }: StepConfigFormProps) {
  return (
    <div className="grid gap-3 pt-2">
      <div className="grid gap-1.5">
        <Label className="text-xs">Source Path</Label>
        <Input
          placeholder="/data/incoming/"
          value={config.sourcePath}
          onChange={(e) => onChange({ ...config, sourcePath: e.target.value })}
        />
      </div>
      <div className="grid gap-1.5">
        <Label className="text-xs">Destination Path</Label>
        <Input
          placeholder="/data/processed/"
          value={config.destinationPath}
          onChange={(e) => onChange({ ...config, destinationPath: e.target.value })}
        />
      </div>
      <label className="flex items-center gap-2 text-sm">
        <input
          type="checkbox"
          checked={config.overwrite}
          onChange={(e) => onChange({ ...config, overwrite: e.target.checked })}
          className="rounded border"
        />
        Overwrite existing files
      </label>
    </div>
  );
}

export function parseStepConfig(configJson: string): StepConfig {
  try {
    const parsed = JSON.parse(configJson);
    return {
      sourcePath: parsed.sourcePath || "",
      destinationPath: parsed.destinationPath || "",
      overwrite: parsed.overwrite || false,
    };
  } catch {
    return { sourcePath: "", destinationPath: "", overwrite: false };
  }
}

export function serializeStepConfig(config: StepConfig): string {
  return JSON.stringify(config);
}
```

**Step 2: Create step-builder.tsx**

```typescript
"use client";

import { useState } from "react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Card, CardContent } from "@/components/ui/card";
import { Badge } from "@/components/ui/badge";
import { Plus, Pencil, X, GripVertical } from "lucide-react";
import { StepConfigForm, parseStepConfig, serializeStepConfig } from "./step-config-form";

export interface StepFormData {
  name: string;
  typeKey: string;
  configuration: string;
  timeoutSeconds: number;
}

interface StepBuilderProps {
  steps: StepFormData[];
  onChange: (steps: StepFormData[]) => void;
}

const STEP_TYPES = [
  { value: "file.copy", label: "File Copy" },
  { value: "file.move", label: "File Move" },
];

export function StepBuilder({ steps, onChange }: StepBuilderProps) {
  const [editingIndex, setEditingIndex] = useState<number | null>(null);
  const [adding, setAdding] = useState(false);
  const [draft, setDraft] = useState<StepFormData>({
    name: "",
    typeKey: "file.copy",
    configuration: "{}",
    timeoutSeconds: 300,
  });

  function addStep() {
    onChange([...steps, { ...draft }]);
    setDraft({ name: "", typeKey: "file.copy", configuration: "{}", timeoutSeconds: 300 });
    setAdding(false);
  }

  function updateStep(index: number, updated: StepFormData) {
    const newSteps = [...steps];
    newSteps[index] = updated;
    onChange(newSteps);
  }

  function removeStep(index: number) {
    onChange(steps.filter((_, i) => i !== index));
    if (editingIndex === index) setEditingIndex(null);
  }

  function moveStep(from: number, to: number) {
    if (to < 0 || to >= steps.length) return;
    const newSteps = [...steps];
    const [moved] = newSteps.splice(from, 1);
    newSteps.splice(to, 0, moved);
    onChange(newSteps);
    if (editingIndex === from) setEditingIndex(to);
  }

  return (
    <div className="space-y-3">
      <div className="flex items-center justify-between">
        <Label className="text-base font-medium">Steps</Label>
        <Button
          type="button"
          variant="outline"
          size="sm"
          onClick={() => setAdding(true)}
          disabled={adding}
        >
          <Plus className="mr-1 h-3 w-3" />
          Add Step
        </Button>
      </div>

      {steps.length === 0 && !adding && (
        <p className="text-sm text-muted-foreground py-4 text-center">
          No steps yet. Add a step to define what this job does.
        </p>
      )}

      {steps.map((step, index) => {
        const config = parseStepConfig(step.configuration);
        const isEditing = editingIndex === index;

        return (
          <Card key={index}>
            <CardContent className="p-4">
              <div className="flex items-start gap-3">
                <div className="flex flex-col gap-1 pt-1">
                  <button
                    type="button"
                    onClick={() => moveStep(index, index - 1)}
                    disabled={index === 0}
                    className="text-muted-foreground hover:text-foreground disabled:opacity-30 text-xs"
                  >
                    ▲
                  </button>
                  <GripVertical className="h-4 w-4 text-muted-foreground" />
                  <button
                    type="button"
                    onClick={() => moveStep(index, index + 1)}
                    disabled={index === steps.length - 1}
                    className="text-muted-foreground hover:text-foreground disabled:opacity-30 text-xs"
                  >
                    ▼
                  </button>
                </div>

                <div className="flex-1 min-w-0">
                  <div className="flex items-center gap-2">
                    <span className="text-sm font-medium text-muted-foreground">
                      {index + 1}.
                    </span>
                    {isEditing ? (
                      <Input
                        value={step.name}
                        onChange={(e) =>
                          updateStep(index, { ...step, name: e.target.value })
                        }
                        className="h-7 text-sm"
                      />
                    ) : (
                      <span className="font-medium">{step.name}</span>
                    )}
                    <Badge variant="secondary" className="text-xs">
                      {step.typeKey}
                    </Badge>
                  </div>

                  {isEditing ? (
                    <>
                      <div className="mt-2 grid gap-1.5">
                        <Label className="text-xs">Step Type</Label>
                        <select
                          value={step.typeKey}
                          onChange={(e) =>
                            updateStep(index, { ...step, typeKey: e.target.value })
                          }
                          className="rounded-md border bg-background px-3 py-1.5 text-sm"
                        >
                          {STEP_TYPES.map((t) => (
                            <option key={t.value} value={t.value}>
                              {t.label}
                            </option>
                          ))}
                        </select>
                      </div>
                      <StepConfigForm
                        typeKey={step.typeKey}
                        config={config}
                        onChange={(c) =>
                          updateStep(index, {
                            ...step,
                            configuration: serializeStepConfig(c),
                          })
                        }
                      />
                      <Button
                        type="button"
                        variant="outline"
                        size="sm"
                        className="mt-2"
                        onClick={() => setEditingIndex(null)}
                      >
                        Done
                      </Button>
                    </>
                  ) : (
                    config.sourcePath && (
                      <p className="mt-1 text-sm text-muted-foreground">
                        {config.sourcePath} → {config.destinationPath}
                      </p>
                    )
                  )}
                </div>

                <div className="flex gap-1">
                  <Button
                    type="button"
                    variant="ghost"
                    size="icon"
                    className="h-7 w-7"
                    onClick={() =>
                      setEditingIndex(isEditing ? null : index)
                    }
                  >
                    <Pencil className="h-3 w-3" />
                  </Button>
                  <Button
                    type="button"
                    variant="ghost"
                    size="icon"
                    className="h-7 w-7 text-destructive"
                    onClick={() => removeStep(index)}
                  >
                    <X className="h-3 w-3" />
                  </Button>
                </div>
              </div>
            </CardContent>
          </Card>
        );
      })}

      {adding && (
        <Card className="border-dashed">
          <CardContent className="p-4 space-y-3">
            <div className="grid gap-1.5">
              <Label className="text-xs">Step Name</Label>
              <Input
                placeholder="e.g., Copy invoice files"
                value={draft.name}
                onChange={(e) => setDraft({ ...draft, name: e.target.value })}
              />
            </div>
            <div className="grid gap-1.5">
              <Label className="text-xs">Step Type</Label>
              <select
                value={draft.typeKey}
                onChange={(e) => setDraft({ ...draft, typeKey: e.target.value })}
                className="rounded-md border bg-background px-3 py-1.5 text-sm"
              >
                {STEP_TYPES.map((t) => (
                  <option key={t.value} value={t.value}>
                    {t.label}
                  </option>
                ))}
              </select>
            </div>
            <StepConfigForm
              typeKey={draft.typeKey}
              config={parseStepConfig(draft.configuration)}
              onChange={(c) =>
                setDraft({ ...draft, configuration: serializeStepConfig(c) })
              }
            />
            <div className="flex gap-2">
              <Button
                type="button"
                size="sm"
                onClick={addStep}
                disabled={!draft.name.trim()}
              >
                Add
              </Button>
              <Button
                type="button"
                variant="outline"
                size="sm"
                onClick={() => setAdding(false)}
              >
                Cancel
              </Button>
            </div>
          </CardContent>
        </Card>
      )}
    </div>
  );
}
```

**Step 3: Create job-form.tsx**

```typescript
"use client";

import { useState } from "react";
import { useRouter } from "next/navigation";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { Label } from "@/components/ui/label";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Separator } from "@/components/ui/separator";
import { StepBuilder, type StepFormData } from "./step-builder";
import { useCreateJob, useUpdateJob, useReplaceSteps } from "@/lib/hooks/use-job-mutations";
import { toast } from "sonner";
import type { JobDto, JobStepDto } from "@/lib/types";
import { parseStepConfig } from "./step-config-form";
import Link from "next/link";
import { ArrowLeft } from "lucide-react";

const jobSchema = z.object({
  name: z.string().min(1, "Name is required").max(100, "Name must be 100 characters or less"),
  description: z.string().max(500, "Description must be 500 characters or less").optional(),
});

type JobFormValues = z.infer<typeof jobSchema>;

interface JobFormProps {
  job?: JobDto;
  existingSteps?: JobStepDto[];
}

export function JobForm({ job, existingSteps }: JobFormProps) {
  const router = useRouter();
  const isEdit = !!job;

  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<JobFormValues>({
    resolver: zodResolver(jobSchema),
    defaultValues: {
      name: job?.name ?? "",
      description: job?.description ?? "",
    },
  });

  const [steps, setSteps] = useState<StepFormData[]>(
    existingSteps?.map((s) => ({
      name: s.name,
      typeKey: s.typeKey,
      configuration: s.configuration,
      timeoutSeconds: s.timeoutSeconds,
    })) ?? []
  );

  const createJob = useCreateJob();
  const updateJob = useUpdateJob(job?.id ?? "");
  const replaceSteps = useReplaceSteps(job?.id ?? "");
  const isSubmitting = createJob.isPending || updateJob.isPending || replaceSteps.isPending;

  async function onSubmit(values: JobFormValues) {
    try {
      if (isEdit) {
        await updateJob.mutateAsync(values);
        await replaceSteps.mutateAsync({
          steps: steps.map((s, i) => ({
            name: s.name,
            typeKey: s.typeKey,
            stepOrder: i + 1,
            configuration: s.configuration,
            timeoutSeconds: s.timeoutSeconds,
          })),
        });
        toast.success("Job updated");
        router.push(`/jobs/${job.id}`);
      } else {
        const result = await createJob.mutateAsync(values);
        const jobId = result.data!.id;

        // Add steps sequentially
        for (let i = 0; i < steps.length; i++) {
          const step = steps[i];
          await fetch(
            `${process.env.NEXT_PUBLIC_API_URL || "http://localhost:5000"}/api/v1/jobs/${jobId}/steps`,
            {
              method: "POST",
              headers: { "Content-Type": "application/json" },
              body: JSON.stringify({
                name: step.name,
                typeKey: step.typeKey,
                stepOrder: i + 1,
                configuration: step.configuration,
                timeoutSeconds: step.timeoutSeconds,
              }),
            }
          );
        }

        toast.success("Job created");
        router.push(`/jobs/${jobId}`);
      }
    } catch (error: any) {
      toast.error(error.message || "Something went wrong");
    }
  }

  return (
    <form onSubmit={handleSubmit(onSubmit)} className="space-y-6">
      <div className="flex items-center gap-3">
        <Button variant="ghost" size="icon" asChild>
          <Link href={isEdit ? `/jobs/${job.id}` : "/jobs"}>
            <ArrowLeft className="h-4 w-4" />
          </Link>
        </Button>
        <h1 className="text-2xl font-bold">{isEdit ? "Edit Job" : "Create Job"}</h1>
      </div>

      <Card>
        <CardHeader>
          <CardTitle>Job Details</CardTitle>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="grid gap-1.5">
            <Label htmlFor="name">Name</Label>
            <Input id="name" placeholder="e.g., Invoice Processor" {...register("name")} />
            {errors.name && (
              <p className="text-sm text-destructive">{errors.name.message}</p>
            )}
          </div>
          <div className="grid gap-1.5">
            <Label htmlFor="description">Description</Label>
            <Input
              id="description"
              placeholder="What does this job do?"
              {...register("description")}
            />
            {errors.description && (
              <p className="text-sm text-destructive">{errors.description.message}</p>
            )}
          </div>
        </CardContent>
      </Card>

      <Card>
        <CardContent className="pt-6">
          <StepBuilder steps={steps} onChange={setSteps} />
        </CardContent>
      </Card>

      <div className="flex justify-end gap-3">
        <Button variant="outline" type="button" asChild>
          <Link href={isEdit ? `/jobs/${job.id}` : "/jobs"}>Cancel</Link>
        </Button>
        <Button type="submit" disabled={isSubmitting}>
          {isSubmitting ? "Saving..." : isEdit ? "Save Changes" : "Create Job"}
        </Button>
      </div>
    </form>
  );
}
```

**Step 4: Create new job page**

Create `src/Courier.Frontend/src/app/jobs/new/page.tsx`:

```typescript
"use client";

import { JobForm } from "@/components/jobs/job-form";

export default function NewJobPage() {
  return <JobForm />;
}
```

**Step 5: Create edit job page**

Create `src/Courier.Frontend/src/app/jobs/[id]/edit/page.tsx`:

```typescript
"use client";

import { use } from "react";
import { useJob } from "@/lib/hooks/use-jobs";
import { useJobSteps } from "@/lib/hooks/use-job-steps";
import { JobForm } from "@/components/jobs/job-form";
import { Skeleton } from "@/components/ui/skeleton";

export default function EditJobPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);
  const { data: jobData, isLoading: jobLoading } = useJob(id);
  const { data: stepsData, isLoading: stepsLoading } = useJobSteps(id);

  if (jobLoading || stepsLoading) {
    return (
      <div className="space-y-4">
        <Skeleton className="h-10 w-48" />
        <Skeleton className="h-64 w-full" />
      </div>
    );
  }

  if (!jobData?.data) {
    return <p className="text-muted-foreground">Job not found.</p>;
  }

  return <JobForm job={jobData.data} existingSteps={stepsData?.data ?? []} />;
}
```

**Step 6: Verify build**

```bash
cd src/Courier.Frontend && npm run build
```

**Step 7: Commit**

```bash
git add src/Courier.Frontend/src/
git commit -m "feat: add job create and edit pages with inline step builder"
```

---

## Task 11: Frontend — Job Detail Page (`/jobs/[id]`)

**Files:**
- Create: `src/Courier.Frontend/src/components/jobs/execution-timeline.tsx`
- Create: `src/Courier.Frontend/src/components/jobs/run-button.tsx`
- Create: `src/Courier.Frontend/src/app/jobs/[id]/page.tsx`

**Step 1: Create run-button.tsx**

```typescript
"use client";

import { useState } from "react";
import { Button } from "@/components/ui/button";
import { Play } from "lucide-react";
import { ConfirmDialog } from "@/components/shared/confirm-dialog";
import { useTriggerJob } from "@/lib/hooks/use-job-mutations";
import { toast } from "sonner";

interface RunButtonProps {
  jobId: string;
  jobName: string;
  onTriggered?: (executionId: string) => void;
}

export function RunButton({ jobId, jobName, onTriggered }: RunButtonProps) {
  const [open, setOpen] = useState(false);
  const trigger = useTriggerJob(jobId);

  function handleRun() {
    trigger.mutate(undefined, {
      onSuccess: (data) => {
        toast.success("Job queued");
        setOpen(false);
        if (data.data?.id) {
          onTriggered?.(data.data.id);
        }
      },
      onError: (error) => {
        toast.error(error.message);
      },
    });
  }

  return (
    <>
      <Button onClick={() => setOpen(true)}>
        <Play className="mr-2 h-4 w-4" />
        Run Job
      </Button>
      <ConfirmDialog
        open={open}
        onOpenChange={setOpen}
        title="Run Job"
        description={`Run "${jobName}" now?`}
        confirmLabel="Run"
        loading={trigger.isPending}
        onConfirm={handleRun}
      />
    </>
  );
}
```

**Step 2: Create execution-timeline.tsx**

```typescript
"use client";

import { useState } from "react";
import { useJobExecutions, useExecution } from "@/lib/hooks/use-job-executions";
import { StatusBadge } from "@/components/shared/status-badge";
import { Card, CardContent } from "@/components/ui/card";
import { Button } from "@/components/ui/button";
import { ChevronDown, ChevronRight } from "lucide-react";
import type { JobExecutionDto } from "@/lib/types";

function timeAgo(dateStr: string): string {
  const seconds = Math.floor((Date.now() - new Date(dateStr).getTime()) / 1000);
  if (seconds < 60) return "just now";
  const minutes = Math.floor(seconds / 60);
  if (minutes < 60) return `${minutes}m ago`;
  const hours = Math.floor(minutes / 60);
  if (hours < 24) return `${hours}h ago`;
  const days = Math.floor(hours / 24);
  return `${days}d ago`;
}

function formatDuration(ms?: number): string {
  if (!ms) return "";
  if (ms < 1000) return `${ms}ms`;
  return `${(ms / 1000).toFixed(1)}s`;
}

interface ExecutionTimelineProps {
  jobId: string;
  latestExecutionId?: string;
}

export function ExecutionTimeline({ jobId, latestExecutionId }: ExecutionTimelineProps) {
  const [page, setPage] = useState(1);
  const { data, isLoading } = useJobExecutions(jobId, page, 10);
  const [expandedId, setExpandedId] = useState<string | null>(latestExecutionId ?? null);

  const executions = data?.data ?? [];
  const pagination = data?.pagination;

  if (isLoading) {
    return <p className="text-sm text-muted-foreground">Loading executions...</p>;
  }

  if (executions.length === 0) {
    return (
      <p className="text-sm text-muted-foreground py-4 text-center">
        No executions yet. Run the job to see results here.
      </p>
    );
  }

  return (
    <div className="space-y-2">
      {executions.map((exec, i) => (
        <ExecutionRow
          key={exec.id}
          execution={exec}
          index={executions.length - i}
          expanded={expandedId === exec.id}
          onToggle={() => setExpandedId(expandedId === exec.id ? null : exec.id)}
          isLatest={i === 0}
        />
      ))}

      {pagination && pagination.totalPages > 1 && (
        <div className="flex items-center justify-center gap-2 pt-2">
          <Button
            variant="outline"
            size="sm"
            disabled={page <= 1}
            onClick={() => setPage((p) => p - 1)}
          >
            Previous
          </Button>
          <span className="text-sm text-muted-foreground">
            Page {pagination.page} of {pagination.totalPages}
          </span>
          <Button
            variant="outline"
            size="sm"
            disabled={page >= pagination.totalPages}
            onClick={() => setPage((p) => p + 1)}
          >
            Next
          </Button>
        </div>
      )}
    </div>
  );
}

function ExecutionRow({
  execution,
  index,
  expanded,
  onToggle,
  isLatest,
}: {
  execution: JobExecutionDto;
  index: number;
  expanded: boolean;
  onToggle: () => void;
  isLatest: boolean;
}) {
  const isRunning =
    execution.state === "queued" || execution.state === "running";

  // Poll for updates if this execution is running
  const { data: liveData } = useExecution(execution.id, isRunning);
  const liveExecution = liveData?.data ?? execution;

  return (
    <Card>
      <CardContent className="p-0">
        <button
          type="button"
          onClick={onToggle}
          className="flex w-full items-center gap-3 px-4 py-3 text-left hover:bg-muted/50 transition-colors"
        >
          {expanded ? (
            <ChevronDown className="h-4 w-4 shrink-0 text-muted-foreground" />
          ) : (
            <ChevronRight className="h-4 w-4 shrink-0 text-muted-foreground" />
          )}
          <StatusBadge state={liveExecution.state} pulse={isRunning} />
          <span className="text-sm font-medium">
            {isLatest ? "Latest" : `#${index}`}
          </span>
          <span className="ml-auto text-sm text-muted-foreground">
            {timeAgo(execution.createdAt)}
          </span>
        </button>

        {expanded && (
          <div className="border-t px-4 py-3">
            <div className="space-y-1 text-sm">
              <div className="flex justify-between text-muted-foreground">
                <span>Triggered by: {liveExecution.triggeredBy}</span>
                {liveExecution.startedAt && liveExecution.completedAt && (
                  <span>
                    Duration:{" "}
                    {formatDuration(
                      new Date(liveExecution.completedAt).getTime() -
                        new Date(liveExecution.startedAt).getTime()
                    )}
                  </span>
                )}
              </div>
              {/* Step-level detail would go here once we have a step executions endpoint */}
              <p className="text-xs text-muted-foreground pt-2">
                State: {liveExecution.state}
                {liveExecution.queuedAt && ` · Queued: ${timeAgo(liveExecution.queuedAt)}`}
                {liveExecution.startedAt && ` · Started: ${timeAgo(liveExecution.startedAt)}`}
                {liveExecution.completedAt && ` · Completed: ${timeAgo(liveExecution.completedAt)}`}
              </p>
            </div>
          </div>
        )}
      </CardContent>
    </Card>
  );
}
```

**Step 3: Create job detail page**

Create `src/Courier.Frontend/src/app/jobs/[id]/page.tsx`:

```typescript
"use client";

import { use, useState } from "react";
import Link from "next/link";
import { useJob } from "@/lib/hooks/use-jobs";
import { useJobSteps } from "@/lib/hooks/use-job-steps";
import { RunButton } from "@/components/jobs/run-button";
import { ExecutionTimeline } from "@/components/jobs/execution-timeline";
import { StatusBadge } from "@/components/shared/status-badge";
import { Button } from "@/components/ui/button";
import { Badge } from "@/components/ui/badge";
import { Card, CardContent, CardHeader, CardTitle } from "@/components/ui/card";
import { Separator } from "@/components/ui/separator";
import { Skeleton } from "@/components/ui/skeleton";
import { Pencil } from "lucide-react";

function timeAgo(dateStr: string): string {
  const seconds = Math.floor((Date.now() - new Date(dateStr).getTime()) / 1000);
  if (seconds < 60) return "just now";
  const minutes = Math.floor(seconds / 60);
  if (minutes < 60) return `${minutes}m ago`;
  const hours = Math.floor(minutes / 60);
  if (hours < 24) return `${hours}h ago`;
  const days = Math.floor(hours / 24);
  return `${days}d ago`;
}

function parseConfig(json: string): { sourcePath?: string; destinationPath?: string } {
  try {
    return JSON.parse(json);
  } catch {
    return {};
  }
}

export default function JobDetailPage({ params }: { params: Promise<{ id: string }> }) {
  const { id } = use(params);
  const { data: jobData, isLoading: jobLoading } = useJob(id);
  const { data: stepsData, isLoading: stepsLoading } = useJobSteps(id);
  const [latestExecutionId, setLatestExecutionId] = useState<string | undefined>();

  if (jobLoading || stepsLoading) {
    return (
      <div className="space-y-4">
        <Skeleton className="h-10 w-64" />
        <Skeleton className="h-32 w-full" />
        <Skeleton className="h-48 w-full" />
      </div>
    );
  }

  const job = jobData?.data;
  if (!job) {
    return <p className="text-muted-foreground">Job not found.</p>;
  }

  const steps = stepsData?.data ?? [];

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-start justify-between">
        <div>
          <h1 className="text-2xl font-bold">{job.name}</h1>
          {job.description && (
            <p className="mt-1 text-muted-foreground">{job.description}</p>
          )}
          <div className="mt-2 flex items-center gap-2">
            <Badge variant="secondary">v{job.currentVersion}</Badge>
            <Badge variant={job.isEnabled ? "default" : "secondary"}>
              {job.isEnabled ? "Enabled" : "Disabled"}
            </Badge>
            <span className="text-sm text-muted-foreground">
              Created {timeAgo(job.createdAt)}
            </span>
          </div>
        </div>
        <div className="flex gap-2">
          <Button variant="outline" asChild>
            <Link href={`/jobs/${id}/edit`}>
              <Pencil className="mr-2 h-4 w-4" />
              Edit
            </Link>
          </Button>
          <RunButton
            jobId={id}
            jobName={job.name}
            onTriggered={(execId) => setLatestExecutionId(execId)}
          />
        </div>
      </div>

      <Separator />

      {/* Steps */}
      <Card>
        <CardHeader>
          <CardTitle>Steps ({steps.length})</CardTitle>
        </CardHeader>
        <CardContent>
          {steps.length === 0 ? (
            <p className="text-sm text-muted-foreground">No steps configured.</p>
          ) : (
            <div className="space-y-2">
              {steps.map((step) => {
                const config = parseConfig(step.configuration);
                return (
                  <div
                    key={step.id}
                    className="flex items-center gap-3 rounded-md border px-4 py-3"
                  >
                    <span className="text-sm font-medium text-muted-foreground">
                      {step.stepOrder}.
                    </span>
                    <span className="font-medium">{step.name}</span>
                    <Badge variant="secondary" className="text-xs">
                      {step.typeKey}
                    </Badge>
                    {config.sourcePath && (
                      <span className="ml-auto text-sm text-muted-foreground">
                        {config.sourcePath} → {config.destinationPath}
                      </span>
                    )}
                  </div>
                );
              })}
            </div>
          )}
        </CardContent>
      </Card>

      {/* Executions */}
      <Card>
        <CardHeader>
          <CardTitle>Executions</CardTitle>
        </CardHeader>
        <CardContent>
          <ExecutionTimeline jobId={id} latestExecutionId={latestExecutionId} />
        </CardContent>
      </Card>
    </div>
  );
}
```

**Step 4: Verify build**

```bash
cd src/Courier.Frontend && npm run build
```

**Step 5: Commit**

```bash
git add src/Courier.Frontend/src/
git commit -m "feat: add job detail page with execution timeline and run button"
```

---

## Task 12: Integration Test — End-to-End Smoke Test

**Purpose:** Start the full stack (Aspire AppHost) and manually verify the frontend works end-to-end.

**Step 1: Start the application**

```bash
cd src/Courier.AppHost && dotnet run
```

**Step 2: Manual verification checklist**

Open the Aspire dashboard and find the frontend URL. Then:

- [ ] `/jobs` — Shows empty state with "Create Job" button
- [ ] Click "Create Job" → navigate to `/jobs/new`
- [ ] Fill in Name: "Test File Copy", Description: "Copies files from A to B"
- [ ] Add Step: "Copy invoices", type: file.copy, source: `/tmp/source`, dest: `/tmp/dest`
- [ ] Add Step: "Archive originals", type: file.move, source: `/tmp/source`, dest: `/tmp/archive`
- [ ] Click "Create Job" → redirects to `/jobs/{id}` detail page
- [ ] Verify steps show in the Steps section
- [ ] Click "Edit" → navigate to edit page with pre-filled form
- [ ] Change name, save → verify changes persist
- [ ] Click "Run Job" → confirm dialog → job queued
- [ ] Execution timeline shows the run with auto-refresh
- [ ] Go back to `/jobs` → job appears in table
- [ ] Use ⋮ menu → Delete → confirm → job removed from list

**Step 3: Fix any issues found during smoke testing**

**Step 4: Final commit**

```bash
git add -A
git commit -m "fix: address issues found during smoke testing"
```

---

## Summary

| Task | Description | Type |
|------|-------------|------|
| 1 | Backend: Update Job endpoint | Backend + Tests |
| 2 | Backend: Delete Job endpoint | Backend + Tests |
| 3 | Backend: Replace Steps endpoint | Backend + Tests |
| 4 | Install deps & configure Tailwind + shadcn | Frontend infra |
| 5 | API client, types, QueryClient | Frontend infra |
| 6 | TanStack Query hooks | Frontend infra |
| 7 | Shell layout (sidebar + topbar) | Frontend UI |
| 8 | Shared components (StatusBadge, ConfirmDialog, EmptyState) | Frontend UI |
| 9 | Jobs list page | Frontend page |
| 10 | Job create/edit form with step builder | Frontend page |
| 11 | Job detail page with execution timeline | Frontend page |
| 12 | End-to-end smoke test | Verification |
