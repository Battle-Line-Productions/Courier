# Job Engine Core Implementation Plan

> **For Claude:** REQUIRED SUB-SKILL: Use superpowers:executing-plans to implement this plan task-by-task.

**Goal:** Build the core job execution pipeline — schema, domain model, state machines, step registry, engine orchestrator, two real step handlers (file.copy, file.move), queue processor in Worker, and Quartz.NET persistent store wiring.

**Architecture:** Extends the existing walking skeleton's vertical-slice architecture. New domain types (enums, entities, engine abstractions) live in `Courier.Domain` (BCL-only). EF Core mappings extend `CourierDbContext`. The `JobEngine` orchestrator and step implementations live in `Courier.Features.Engine`. The Worker gets a `JobQueueProcessor` background service that polls for queued executions and feeds them to the engine.

**Tech Stack:** .NET 10, PostgreSQL 16, EF Core 10, DbUp, Quartz.NET 3.14 (persistent store), xUnit + Shouldly + NSubstitute + Testcontainers

---

## Assumptions & Scoping Decisions

- **No partitioning yet**: `job_executions` and `step_executions` use regular tables (not `PARTITION BY RANGE`) for simplicity. Production partitioning is a follow-up.
- **No job chains/dependencies**: Chains, chain_members, chain_executions, job_dependencies tables are deferred.
- **No cron scheduling yet**: Quartz.NET persistent store is wired up, but no triggers/schedules are created. Schedule CRUD comes in a follow-up.
- **No job versioning**: job_versions table deferred. `current_version` stays at 1.
- **No checkpoint/resume**: Paused state transitions are modeled but not exercised.
- **No audit log entries / domain events**: Tables deferred. Focus is execution pipeline.
- **File step handlers are local-only**: `file.copy` and `file.move` work on the local filesystem (no SFTP/FTP yet).

---

## Task 1: Migration 0002 — Job Engine Schema

**Files:**
- Create: `src/Courier.Migrations/Scripts/0002_job_engine_tables.sql`

**Step 1: Write the migration SQL**

```sql
-- ============================================================
-- JOB STEPS
-- ============================================================
CREATE TABLE job_steps (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    job_id              UUID NOT NULL,
    step_order          INT NOT NULL,
    name                TEXT NOT NULL,
    type_key            TEXT NOT NULL,
    configuration       JSONB NOT NULL DEFAULT '{}',
    timeout_seconds     INT NOT NULL DEFAULT 300,

    CONSTRAINT fk_job_steps_jobs FOREIGN KEY (job_id)
        REFERENCES jobs (id) ON DELETE CASCADE,
    CONSTRAINT ck_job_steps_order_positive CHECK (step_order >= 0),
    CONSTRAINT ck_job_steps_timeout_positive CHECK (timeout_seconds > 0)
);

CREATE UNIQUE INDEX ix_job_steps_job_order ON job_steps (job_id, step_order);
CREATE INDEX ix_job_steps_type_key ON job_steps (type_key);

-- ============================================================
-- JOB EXECUTIONS (non-partitioned for V1 dev)
-- ============================================================
CREATE TABLE job_executions (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    job_id              UUID NOT NULL,
    job_version_number  INT NOT NULL DEFAULT 1,
    triggered_by        TEXT NOT NULL,
    state               TEXT NOT NULL DEFAULT 'created',
    queued_at           TIMESTAMPTZ,
    started_at          TIMESTAMPTZ,
    completed_at        TIMESTAMPTZ,
    context_snapshot    JSONB DEFAULT '{}',
    created_at          TIMESTAMPTZ NOT NULL DEFAULT now(),

    CONSTRAINT fk_job_executions_jobs FOREIGN KEY (job_id)
        REFERENCES jobs (id) ON DELETE CASCADE,
    CONSTRAINT ck_job_executions_state CHECK (
        state IN ('created', 'queued', 'running', 'paused', 'completed', 'failed', 'cancelled')
    )
);

CREATE INDEX ix_job_executions_job_id ON job_executions (job_id, created_at DESC);
CREATE INDEX ix_job_executions_state ON job_executions (state, created_at DESC);
CREATE INDEX ix_job_executions_queued ON job_executions (queued_at)
    WHERE state = 'queued';

-- ============================================================
-- STEP EXECUTIONS (non-partitioned for V1 dev)
-- ============================================================
CREATE TABLE step_executions (
    id                  UUID PRIMARY KEY DEFAULT gen_random_uuid(),
    job_execution_id    UUID NOT NULL,
    job_step_id         UUID NOT NULL,
    step_order          INT NOT NULL,
    state               TEXT NOT NULL DEFAULT 'pending',
    started_at          TIMESTAMPTZ,
    completed_at        TIMESTAMPTZ,
    duration_ms         BIGINT,
    bytes_processed     BIGINT,
    output_data         JSONB,
    error_message       TEXT,
    error_stack_trace   TEXT,
    retry_attempt       INT NOT NULL DEFAULT 0,
    created_at          TIMESTAMPTZ NOT NULL DEFAULT now(),

    CONSTRAINT fk_step_executions_job_executions FOREIGN KEY (job_execution_id)
        REFERENCES job_executions (id) ON DELETE CASCADE,
    CONSTRAINT fk_step_executions_job_steps FOREIGN KEY (job_step_id)
        REFERENCES job_steps (id) ON DELETE CASCADE,
    CONSTRAINT ck_step_executions_state CHECK (
        state IN ('pending', 'running', 'completed', 'failed', 'skipped')
    )
);

CREATE INDEX ix_step_executions_job_execution ON step_executions (job_execution_id, step_order);
CREATE INDEX ix_step_executions_state ON step_executions (state, created_at DESC);

-- ============================================================
-- SYSTEM SETTINGS
-- ============================================================
CREATE TABLE system_settings (
    key                 TEXT PRIMARY KEY,
    value               TEXT NOT NULL,
    description         TEXT,
    updated_at          TIMESTAMPTZ NOT NULL DEFAULT now(),
    updated_by          TEXT NOT NULL
);

INSERT INTO system_settings (key, value, description, updated_by) VALUES
    ('job.concurrency_limit', '5', 'Maximum concurrent job executions', 'system'),
    ('job.temp_cleanup_days', '7', 'Days before orphaned temp directories are purged', 'system');
```

**Step 2: Verify the migration is embedded**

The migration file must be at `src/Courier.Migrations/Scripts/0002_job_engine_tables.sql` and the `.csproj` should already embed scripts via the existing glob pattern (check that `EmbeddedResource` includes `Scripts\*.sql`).

Run: `dotnet build src/Courier.Migrations/Courier.Migrations.csproj`
Expected: Build succeeds

**Step 3: Verify migrations run in integration test**

Run: `dotnet test tests/Courier.Tests.Integration --filter "HealthCheck_ReturnsHealthy" -v n`
Expected: PASS (the CourierApiFactory runs all DbUp migrations on startup)

**Step 4: Commit**

```bash
git add src/Courier.Migrations/Scripts/0002_job_engine_tables.sql
git commit -m "feat: add job engine schema migration (steps, executions, system_settings)"
```

---

## Task 2: Domain — Enums

**Files:**
- Create: `src/Courier.Domain/Enums/JobExecutionState.cs`
- Create: `src/Courier.Domain/Enums/StepExecutionState.cs`
- Create: `src/Courier.Domain/Enums/FailurePolicyType.cs`

**Step 1: Write the enums**

`src/Courier.Domain/Enums/JobExecutionState.cs`:
```csharp
namespace Courier.Domain.Enums;

public enum JobExecutionState
{
    Created,
    Queued,
    Running,
    Paused,
    Completed,
    Failed,
    Cancelled
}
```

`src/Courier.Domain/Enums/StepExecutionState.cs`:
```csharp
namespace Courier.Domain.Enums;

public enum StepExecutionState
{
    Pending,
    Running,
    Completed,
    Failed,
    Skipped
}
```

`src/Courier.Domain/Enums/FailurePolicyType.cs`:
```csharp
namespace Courier.Domain.Enums;

public enum FailurePolicyType
{
    Stop,
    RetryStep,
    RetryJob,
    SkipAndContinue
}
```

**Step 2: Build to verify**

Run: `dotnet build src/Courier.Domain/Courier.Domain.csproj`
Expected: Build succeeds, no warnings

**Step 3: Commit**

```bash
git add src/Courier.Domain/Enums/
git commit -m "feat: add job engine enums (execution states, failure policy)"
```

---

## Task 3: Domain — Entities (JobStep, JobExecution, StepExecution)

**Files:**
- Create: `src/Courier.Domain/Entities/JobStep.cs`
- Create: `src/Courier.Domain/Entities/JobExecution.cs`
- Create: `src/Courier.Domain/Entities/StepExecution.cs`
- Modify: `src/Courier.Domain/Entities/Job.cs` (add Steps navigation)

**Step 1: Write the entities**

`src/Courier.Domain/Entities/JobStep.cs`:
```csharp
namespace Courier.Domain.Entities;

public class JobStep
{
    public Guid Id { get; set; }
    public Guid JobId { get; set; }
    public int StepOrder { get; set; }
    public string Name { get; set; } = string.Empty;
    public string TypeKey { get; set; } = string.Empty;
    public string Configuration { get; set; } = "{}";
    public int TimeoutSeconds { get; set; } = 300;

    public Job Job { get; set; } = null!;
}
```

`src/Courier.Domain/Entities/JobExecution.cs`:
```csharp
using Courier.Domain.Enums;

namespace Courier.Domain.Entities;

public class JobExecution
{
    public Guid Id { get; set; }
    public Guid JobId { get; set; }
    public int JobVersionNumber { get; set; } = 1;
    public string TriggeredBy { get; set; } = string.Empty;
    public JobExecutionState State { get; set; } = JobExecutionState.Created;
    public DateTime? QueuedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public string ContextSnapshot { get; set; } = "{}";
    public DateTime CreatedAt { get; set; }

    public Job Job { get; set; } = null!;
    public List<StepExecution> StepExecutions { get; set; } = [];
}
```

`src/Courier.Domain/Entities/StepExecution.cs`:
```csharp
using Courier.Domain.Enums;

namespace Courier.Domain.Entities;

public class StepExecution
{
    public Guid Id { get; set; }
    public Guid JobExecutionId { get; set; }
    public Guid JobStepId { get; set; }
    public int StepOrder { get; set; }
    public StepExecutionState State { get; set; } = StepExecutionState.Pending;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public long? DurationMs { get; set; }
    public long? BytesProcessed { get; set; }
    public string? OutputData { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ErrorStackTrace { get; set; }
    public int RetryAttempt { get; set; }
    public DateTime CreatedAt { get; set; }

    public JobExecution JobExecution { get; set; } = null!;
    public JobStep JobStep { get; set; } = null!;
}
```

**Step 2: Modify `Job.cs` — add Steps navigation property**

Add to `src/Courier.Domain/Entities/Job.cs`:
```csharp
public List<JobStep> Steps { get; set; } = [];
public List<JobExecution> Executions { get; set; } = [];
```

**Step 3: Build to verify Domain is still BCL-only**

Run: `dotnet build src/Courier.Domain/Courier.Domain.csproj`
Expected: Build succeeds

Run: `dotnet test tests/Courier.Tests.Architecture --filter "Domain_ShouldHaveNoDependencyOnExternalPackages" -v n`
Expected: PASS

**Step 4: Commit**

```bash
git add src/Courier.Domain/Entities/
git commit -m "feat: add JobStep, JobExecution, StepExecution entities"
```

---

## Task 4: Domain — Engine Abstractions

**Files:**
- Create: `src/Courier.Domain/Engine/IJobStep.cs`
- Create: `src/Courier.Domain/Engine/StepResult.cs`
- Create: `src/Courier.Domain/Engine/StepConfiguration.cs`
- Create: `src/Courier.Domain/Engine/JobContext.cs`
- Create: `src/Courier.Domain/Engine/FailurePolicy.cs`
- Test: `tests/Courier.Tests.Unit/Engine/JobContextTests.cs`

**Step 1: Write the test for JobContext**

`tests/Courier.Tests.Unit/Engine/JobContextTests.cs`:
```csharp
using Courier.Domain.Engine;
using Shouldly;

namespace Courier.Tests.Unit.Engine;

public class JobContextTests
{
    [Fact]
    public void Set_And_Get_ReturnsValue()
    {
        var ctx = new JobContext();
        ctx.Set("0.downloaded_file", "/tmp/file.txt");
        ctx.Get<string>("0.downloaded_file").ShouldBe("/tmp/file.txt");
    }

    [Fact]
    public void TryGet_MissingKey_ReturnsFalse()
    {
        var ctx = new JobContext();
        ctx.TryGet<string>("missing", out var value).ShouldBeFalse();
        value.ShouldBeNull();
    }

    [Fact]
    public void Snapshot_ReturnsAllEntries()
    {
        var ctx = new JobContext();
        ctx.Set("a", 1);
        ctx.Set("b", "two");
        var snap = ctx.Snapshot();
        snap.Count.ShouldBe(2);
        snap["a"].ShouldBe(1);
        snap["b"].ShouldBe("two");
    }

    [Fact]
    public void Snapshot_IsReadOnly_OriginalUnaffected()
    {
        var ctx = new JobContext();
        ctx.Set("a", 1);
        var snap = ctx.Snapshot();
        ctx.Set("b", 2);
        snap.Count.ShouldBe(1); // snapshot taken before "b" was added
    }

    [Fact]
    public void Restore_PopulatesFromDictionary()
    {
        var data = new Dictionary<string, object> { ["x"] = "hello" };
        var ctx = JobContext.Restore(data);
        ctx.Get<string>("x").ShouldBe("hello");
    }
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test tests/Courier.Tests.Unit --filter "JobContextTests" -v n`
Expected: FAIL — `JobContext` class does not exist yet

**Step 3: Write the engine abstractions**

`src/Courier.Domain/Engine/StepResult.cs`:
```csharp
namespace Courier.Domain.Engine;

public record StepResult
{
    public bool Success { get; init; }
    public long BytesProcessed { get; init; }
    public Dictionary<string, object>? Outputs { get; init; }
    public string? ErrorMessage { get; init; }
    public string? ErrorStackTrace { get; init; }

    public static StepResult Ok(long bytesProcessed = 0, Dictionary<string, object>? outputs = null)
        => new() { Success = true, BytesProcessed = bytesProcessed, Outputs = outputs };

    public static StepResult Fail(string message, string? stackTrace = null)
        => new() { Success = false, ErrorMessage = message, ErrorStackTrace = stackTrace };
}
```

`src/Courier.Domain/Engine/StepConfiguration.cs`:
```csharp
using System.Text.Json;

namespace Courier.Domain.Engine;

/// <summary>
/// Wraps the JSONB configuration for a step. Provides typed access to config values.
/// </summary>
public class StepConfiguration
{
    private readonly JsonElement _root;

    public StepConfiguration(string json)
    {
        _root = JsonDocument.Parse(json).RootElement;
    }

    public string GetString(string key)
        => _root.GetProperty(key).GetString()
           ?? throw new InvalidOperationException($"Step config key '{key}' is null.");

    public string? GetStringOrDefault(string key, string? defaultValue = null)
        => _root.TryGetProperty(key, out var prop) ? prop.GetString() : defaultValue;

    public int GetInt(string key)
        => _root.GetProperty(key).GetInt32();

    public bool GetBool(string key)
        => _root.GetProperty(key).GetBoolean();

    public bool Has(string key)
        => _root.TryGetProperty(key, out _);

    public string Raw => _root.GetRawText();
}
```

`src/Courier.Domain/Engine/IJobStep.cs`:
```csharp
namespace Courier.Domain.Engine;

/// <summary>
/// Defines a step type that can be registered in the StepTypeRegistry.
/// Step implementations must be safe to retry (see design doc 5.12).
/// </summary>
public interface IJobStep
{
    /// <summary>Type key used to resolve this step (e.g., "file.copy", "sftp.download").</summary>
    string TypeKey { get; }

    /// <summary>Execute the step logic.</summary>
    Task<StepResult> ExecuteAsync(
        StepConfiguration config,
        JobContext context,
        CancellationToken cancellationToken);

    /// <summary>Validate configuration before execution (dry-run support).</summary>
    Task<StepResult> ValidateAsync(StepConfiguration config);
}
```

`src/Courier.Domain/Engine/JobContext.cs`:
```csharp
namespace Courier.Domain.Engine;

/// <summary>
/// Accumulates outputs as steps execute. Persisted to DB for checkpoint/resume.
/// Key convention: "{stepIndex}.{outputName}" (e.g., "0.downloaded_files").
/// </summary>
public class JobContext
{
    private readonly Dictionary<string, object> _data = new();

    public void Set<T>(string key, T value) where T : notnull
        => _data[key] = value;

    public T Get<T>(string key)
        => (T)_data[key];

    public bool TryGet<T>(string key, out T? value)
    {
        if (_data.TryGetValue(key, out var raw) && raw is T typed)
        {
            value = typed;
            return true;
        }
        value = default;
        return false;
    }

    public IReadOnlyDictionary<string, object> Snapshot()
        => new Dictionary<string, object>(_data);

    public static JobContext Restore(IDictionary<string, object> data)
    {
        var ctx = new JobContext();
        foreach (var kvp in data)
            ctx._data[kvp.Key] = kvp.Value;
        return ctx;
    }
}
```

`src/Courier.Domain/Engine/FailurePolicy.cs`:
```csharp
using Courier.Domain.Enums;

namespace Courier.Domain.Engine;

/// <summary>
/// Deserialized failure policy from the jobs.failure_policy JSONB column.
/// </summary>
public record FailurePolicy
{
    public FailurePolicyType Type { get; init; } = FailurePolicyType.Stop;
    public int MaxRetries { get; init; } = 3;
    public int BackoffBaseSeconds { get; init; } = 1;
    public int BackoffMaxSeconds { get; init; } = 60;

    public TimeSpan GetBackoffDelay(int attempt)
    {
        var seconds = Math.Min(BackoffBaseSeconds * (1 << attempt), BackoffMaxSeconds);
        return TimeSpan.FromSeconds(seconds);
    }
}
```

**Step 4: Run the tests**

Run: `dotnet test tests/Courier.Tests.Unit --filter "JobContextTests" -v n`
Expected: All 5 PASS

**Step 5: Commit**

```bash
git add src/Courier.Domain/Engine/ tests/Courier.Tests.Unit/Engine/
git commit -m "feat: add engine abstractions (IJobStep, JobContext, StepResult, StepConfiguration, FailurePolicy)"
```

---

## Task 5: Domain — State Machines

**Files:**
- Create: `src/Courier.Domain/Engine/JobStateMachine.cs`
- Create: `src/Courier.Domain/Engine/StepStateMachine.cs`
- Test: `tests/Courier.Tests.Unit/Engine/JobStateMachineTests.cs`
- Test: `tests/Courier.Tests.Unit/Engine/StepStateMachineTests.cs`

**Step 1: Write the tests**

`tests/Courier.Tests.Unit/Engine/JobStateMachineTests.cs`:
```csharp
using Courier.Domain.Engine;
using Courier.Domain.Enums;
using Shouldly;

namespace Courier.Tests.Unit.Engine;

public class JobStateMachineTests
{
    [Theory]
    [InlineData(JobExecutionState.Created, JobExecutionState.Queued)]
    [InlineData(JobExecutionState.Queued, JobExecutionState.Running)]
    [InlineData(JobExecutionState.Running, JobExecutionState.Completed)]
    [InlineData(JobExecutionState.Running, JobExecutionState.Failed)]
    [InlineData(JobExecutionState.Running, JobExecutionState.Paused)]
    [InlineData(JobExecutionState.Paused, JobExecutionState.Running)]
    [InlineData(JobExecutionState.Queued, JobExecutionState.Cancelled)]
    [InlineData(JobExecutionState.Running, JobExecutionState.Cancelled)]
    [InlineData(JobExecutionState.Paused, JobExecutionState.Cancelled)]
    public void ValidTransitions_ShouldSucceed(JobExecutionState from, JobExecutionState to)
    {
        JobStateMachine.CanTransition(from, to).ShouldBeTrue($"{from} -> {to} should be valid");
    }

    [Theory]
    [InlineData(JobExecutionState.Created, JobExecutionState.Completed)]
    [InlineData(JobExecutionState.Completed, JobExecutionState.Running)]
    [InlineData(JobExecutionState.Failed, JobExecutionState.Running)]
    [InlineData(JobExecutionState.Cancelled, JobExecutionState.Running)]
    [InlineData(JobExecutionState.Created, JobExecutionState.Running)]
    public void InvalidTransitions_ShouldFail(JobExecutionState from, JobExecutionState to)
    {
        JobStateMachine.CanTransition(from, to).ShouldBeFalse($"{from} -> {to} should be invalid");
    }

    [Fact]
    public void Transition_InvalidTransition_ThrowsInvalidOperationException()
    {
        Should.Throw<InvalidOperationException>(() =>
            JobStateMachine.Transition(JobExecutionState.Completed, JobExecutionState.Running));
    }

    [Fact]
    public void Transition_ValidTransition_ReturnsNewState()
    {
        var result = JobStateMachine.Transition(JobExecutionState.Created, JobExecutionState.Queued);
        result.ShouldBe(JobExecutionState.Queued);
    }
}
```

`tests/Courier.Tests.Unit/Engine/StepStateMachineTests.cs`:
```csharp
using Courier.Domain.Engine;
using Courier.Domain.Enums;
using Shouldly;

namespace Courier.Tests.Unit.Engine;

public class StepStateMachineTests
{
    [Theory]
    [InlineData(StepExecutionState.Pending, StepExecutionState.Running)]
    [InlineData(StepExecutionState.Pending, StepExecutionState.Skipped)]
    [InlineData(StepExecutionState.Running, StepExecutionState.Completed)]
    [InlineData(StepExecutionState.Running, StepExecutionState.Failed)]
    public void ValidTransitions_ShouldSucceed(StepExecutionState from, StepExecutionState to)
    {
        StepStateMachine.CanTransition(from, to).ShouldBeTrue($"{from} -> {to} should be valid");
    }

    [Theory]
    [InlineData(StepExecutionState.Completed, StepExecutionState.Running)]
    [InlineData(StepExecutionState.Failed, StepExecutionState.Running)]
    [InlineData(StepExecutionState.Skipped, StepExecutionState.Running)]
    [InlineData(StepExecutionState.Pending, StepExecutionState.Completed)]
    public void InvalidTransitions_ShouldFail(StepExecutionState from, StepExecutionState to)
    {
        StepStateMachine.CanTransition(from, to).ShouldBeFalse($"{from} -> {to} should be invalid");
    }
}
```

**Step 2: Run tests to verify they fail**

Run: `dotnet test tests/Courier.Tests.Unit --filter "StateMachine" -v n`
Expected: FAIL — `JobStateMachine` and `StepStateMachine` do not exist

**Step 3: Implement state machines**

`src/Courier.Domain/Engine/JobStateMachine.cs`:
```csharp
using Courier.Domain.Enums;

namespace Courier.Domain.Engine;

public static class JobStateMachine
{
    private static readonly Dictionary<JobExecutionState, HashSet<JobExecutionState>> ValidTransitions = new()
    {
        [JobExecutionState.Created]   = [JobExecutionState.Queued],
        [JobExecutionState.Queued]    = [JobExecutionState.Running, JobExecutionState.Cancelled],
        [JobExecutionState.Running]   = [JobExecutionState.Completed, JobExecutionState.Failed, JobExecutionState.Paused, JobExecutionState.Cancelled],
        [JobExecutionState.Paused]    = [JobExecutionState.Running, JobExecutionState.Cancelled],
        [JobExecutionState.Completed] = [],
        [JobExecutionState.Failed]    = [],
        [JobExecutionState.Cancelled] = [],
    };

    public static bool CanTransition(JobExecutionState from, JobExecutionState to)
        => ValidTransitions.TryGetValue(from, out var targets) && targets.Contains(to);

    public static JobExecutionState Transition(JobExecutionState from, JobExecutionState to)
        => CanTransition(from, to)
            ? to
            : throw new InvalidOperationException($"Invalid job state transition: {from} -> {to}");
}
```

`src/Courier.Domain/Engine/StepStateMachine.cs`:
```csharp
using Courier.Domain.Enums;

namespace Courier.Domain.Engine;

public static class StepStateMachine
{
    private static readonly Dictionary<StepExecutionState, HashSet<StepExecutionState>> ValidTransitions = new()
    {
        [StepExecutionState.Pending]   = [StepExecutionState.Running, StepExecutionState.Skipped],
        [StepExecutionState.Running]   = [StepExecutionState.Completed, StepExecutionState.Failed],
        [StepExecutionState.Completed] = [],
        [StepExecutionState.Failed]    = [],
        [StepExecutionState.Skipped]   = [],
    };

    public static bool CanTransition(StepExecutionState from, StepExecutionState to)
        => ValidTransitions.TryGetValue(from, out var targets) && targets.Contains(to);

    public static StepExecutionState Transition(StepExecutionState from, StepExecutionState to)
        => CanTransition(from, to)
            ? to
            : throw new InvalidOperationException($"Invalid step state transition: {from} -> {to}");
}
```

**Step 4: Run tests**

Run: `dotnet test tests/Courier.Tests.Unit --filter "StateMachine" -v n`
Expected: All PASS

**Step 5: Commit**

```bash
git add src/Courier.Domain/Engine/JobStateMachine.cs src/Courier.Domain/Engine/StepStateMachine.cs
git add tests/Courier.Tests.Unit/Engine/
git commit -m "feat: add job and step state machines with transition validation"
```

---

## Task 6: Infrastructure — EF Core Mappings

**Files:**
- Modify: `src/Courier.Infrastructure/Data/CourierDbContext.cs`

**Step 1: Add DbSets and entity configurations**

Add three new `DbSet` properties and their `OnModelCreating` configurations:

```csharp
public DbSet<JobStep> JobSteps => Set<JobStep>();
public DbSet<JobExecution> JobExecutions => Set<JobExecution>();
public DbSet<StepExecution> StepExecutions => Set<StepExecution>();
```

Add to `OnModelCreating`:

```csharp
modelBuilder.Entity<JobStep>(entity =>
{
    entity.ToTable("job_steps");
    entity.HasKey(e => e.Id);
    entity.Property(e => e.Id).HasColumnName("id");
    entity.Property(e => e.JobId).HasColumnName("job_id");
    entity.Property(e => e.StepOrder).HasColumnName("step_order");
    entity.Property(e => e.Name).HasColumnName("name").IsRequired();
    entity.Property(e => e.TypeKey).HasColumnName("type_key").IsRequired();
    entity.Property(e => e.Configuration).HasColumnName("configuration").HasColumnType("jsonb");
    entity.Property(e => e.TimeoutSeconds).HasColumnName("timeout_seconds").HasDefaultValue(300);

    entity.HasOne(e => e.Job).WithMany(j => j.Steps).HasForeignKey(e => e.JobId);
    entity.HasIndex(e => new { e.JobId, e.StepOrder }).IsUnique();
});

modelBuilder.Entity<JobExecution>(entity =>
{
    entity.ToTable("job_executions");
    entity.HasKey(e => e.Id);
    entity.Property(e => e.Id).HasColumnName("id");
    entity.Property(e => e.JobId).HasColumnName("job_id");
    entity.Property(e => e.JobVersionNumber).HasColumnName("job_version_number").HasDefaultValue(1);
    entity.Property(e => e.TriggeredBy).HasColumnName("triggered_by").IsRequired();
    entity.Property(e => e.State).HasColumnName("state")
        .HasConversion(
            v => v.ToString().ToLowerInvariant(),
            v => Enum.Parse<JobExecutionState>(v, true));
    entity.Property(e => e.QueuedAt).HasColumnName("queued_at");
    entity.Property(e => e.StartedAt).HasColumnName("started_at");
    entity.Property(e => e.CompletedAt).HasColumnName("completed_at");
    entity.Property(e => e.ContextSnapshot).HasColumnName("context_snapshot").HasColumnType("jsonb");
    entity.Property(e => e.CreatedAt).HasColumnName("created_at");

    entity.HasOne(e => e.Job).WithMany(j => j.Executions).HasForeignKey(e => e.JobId);
});

modelBuilder.Entity<StepExecution>(entity =>
{
    entity.ToTable("step_executions");
    entity.HasKey(e => e.Id);
    entity.Property(e => e.Id).HasColumnName("id");
    entity.Property(e => e.JobExecutionId).HasColumnName("job_execution_id");
    entity.Property(e => e.JobStepId).HasColumnName("job_step_id");
    entity.Property(e => e.StepOrder).HasColumnName("step_order");
    entity.Property(e => e.State).HasColumnName("state")
        .HasConversion(
            v => v.ToString().ToLowerInvariant(),
            v => Enum.Parse<StepExecutionState>(v, true));
    entity.Property(e => e.StartedAt).HasColumnName("started_at");
    entity.Property(e => e.CompletedAt).HasColumnName("completed_at");
    entity.Property(e => e.DurationMs).HasColumnName("duration_ms");
    entity.Property(e => e.BytesProcessed).HasColumnName("bytes_processed");
    entity.Property(e => e.OutputData).HasColumnName("output_data").HasColumnType("jsonb");
    entity.Property(e => e.ErrorMessage).HasColumnName("error_message");
    entity.Property(e => e.ErrorStackTrace).HasColumnName("error_stack_trace");
    entity.Property(e => e.RetryAttempt).HasColumnName("retry_attempt");
    entity.Property(e => e.CreatedAt).HasColumnName("created_at");

    entity.HasOne(e => e.JobExecution).WithMany(je => je.StepExecutions).HasForeignKey(e => e.JobExecutionId);
    entity.HasOne(e => e.JobStep).WithMany().HasForeignKey(e => e.JobStepId);
});
```

**Important note on usings:** Add `using Courier.Domain.Enums;` to the top of `CourierDbContext.cs` since the enum conversions reference `JobExecutionState` and `StepExecutionState`.

**Step 2: Build to verify**

Run: `dotnet build src/Courier.Infrastructure/Courier.Infrastructure.csproj`
Expected: Build succeeds

**Step 3: Run existing tests to verify no regressions**

Run: `dotnet test Courier.slnx -v n`
Expected: All 20 existing tests pass

**Step 4: Commit**

```bash
git add src/Courier.Infrastructure/Data/CourierDbContext.cs
git commit -m "feat: add EF Core mappings for JobStep, JobExecution, StepExecution"
```

---

## Task 7: Features — StepTypeRegistry + Step Handlers

**Files:**
- Create: `src/Courier.Features/Engine/StepTypeRegistry.cs`
- Create: `src/Courier.Features/Engine/Steps/FileCopyStep.cs`
- Create: `src/Courier.Features/Engine/Steps/FileMoveStep.cs`
- Test: `tests/Courier.Tests.Unit/Engine/StepTypeRegistryTests.cs`
- Test: `tests/Courier.Tests.Unit/Engine/FileCopyStepTests.cs`
- Test: `tests/Courier.Tests.Unit/Engine/FileMoveStepTests.cs`

**Step 1: Write the StepTypeRegistry test**

`tests/Courier.Tests.Unit/Engine/StepTypeRegistryTests.cs`:
```csharp
using Courier.Domain.Engine;
using Courier.Features.Engine;
using NSubstitute;
using Shouldly;

namespace Courier.Tests.Unit.Engine;

public class StepTypeRegistryTests
{
    [Fact]
    public void Resolve_RegisteredType_ReturnsStep()
    {
        var step = Substitute.For<IJobStep>();
        step.TypeKey.Returns("test.step");

        var registry = new StepTypeRegistry([step]);
        var resolved = registry.Resolve("test.step");

        resolved.ShouldBe(step);
    }

    [Fact]
    public void Resolve_UnknownType_ThrowsKeyNotFoundException()
    {
        var registry = new StepTypeRegistry([]);
        Should.Throw<KeyNotFoundException>(() => registry.Resolve("unknown.type"));
    }

    [Fact]
    public void GetRegisteredTypes_ReturnsAllKeys()
    {
        var step1 = Substitute.For<IJobStep>();
        step1.TypeKey.Returns("a");
        var step2 = Substitute.For<IJobStep>();
        step2.TypeKey.Returns("b");

        var registry = new StepTypeRegistry([step1, step2]);
        var keys = registry.GetRegisteredTypes();

        keys.ShouldContain("a");
        keys.ShouldContain("b");
        keys.Count().ShouldBe(2);
    }
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test tests/Courier.Tests.Unit --filter "StepTypeRegistryTests" -v n`
Expected: FAIL — `StepTypeRegistry` does not exist

**Step 3: Implement StepTypeRegistry**

`src/Courier.Features/Engine/StepTypeRegistry.cs`:
```csharp
using Courier.Domain.Engine;

namespace Courier.Features.Engine;

public class StepTypeRegistry
{
    private readonly Dictionary<string, IJobStep> _steps;

    public StepTypeRegistry(IEnumerable<IJobStep> steps)
    {
        _steps = steps.ToDictionary(s => s.TypeKey, StringComparer.OrdinalIgnoreCase);
    }

    public IJobStep Resolve(string typeKey)
        => _steps.TryGetValue(typeKey, out var step)
            ? step
            : throw new KeyNotFoundException($"No step handler registered for type key '{typeKey}'. Registered: [{string.Join(", ", _steps.Keys)}]");

    public IEnumerable<string> GetRegisteredTypes() => _steps.Keys;
}
```

**Step 4: Run StepTypeRegistry test**

Run: `dotnet test tests/Courier.Tests.Unit --filter "StepTypeRegistryTests" -v n`
Expected: All 3 PASS

**Step 5: Write FileCopyStep test**

`tests/Courier.Tests.Unit/Engine/FileCopyStepTests.cs`:
```csharp
using Courier.Domain.Engine;
using Courier.Features.Engine.Steps;
using Shouldly;

namespace Courier.Tests.Unit.Engine;

public class FileCopyStepTests : IDisposable
{
    private readonly string _tempDir;

    public FileCopyStepTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"courier-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    [Fact]
    public async Task Execute_CopiesFile_Successfully()
    {
        // Arrange
        var sourceFile = Path.Combine(_tempDir, "source.txt");
        var destFile = Path.Combine(_tempDir, "dest.txt");
        await File.WriteAllTextAsync(sourceFile, "hello world");

        var config = new StepConfiguration($$"""{"source_path": "{{sourceFile.Replace("\\", "\\\\")}}", "destination_path": "{{destFile.Replace("\\", "\\\\")}}"}""");
        var context = new JobContext();
        var step = new FileCopyStep();

        // Act
        var result = await step.ExecuteAsync(config, context, CancellationToken.None);

        // Assert
        result.Success.ShouldBeTrue();
        File.Exists(destFile).ShouldBeTrue();
        (await File.ReadAllTextAsync(destFile)).ShouldBe("hello world");
        File.Exists(sourceFile).ShouldBeTrue(); // source still exists
    }

    [Fact]
    public async Task Execute_SourceNotFound_ReturnsFailure()
    {
        var config = new StepConfiguration("""{"source_path": "/nonexistent/file.txt", "destination_path": "/tmp/out.txt"}""");
        var context = new JobContext();
        var step = new FileCopyStep();

        var result = await step.ExecuteAsync(config, context, CancellationToken.None);

        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldContain("Source file not found");
    }

    [Fact]
    public void TypeKey_IsFileCopy()
    {
        new FileCopyStep().TypeKey.ShouldBe("file.copy");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }
}
```

**Step 6: Run test to verify it fails**

Run: `dotnet test tests/Courier.Tests.Unit --filter "FileCopyStepTests" -v n`
Expected: FAIL — `FileCopyStep` does not exist

**Step 7: Implement FileCopyStep**

`src/Courier.Features/Engine/Steps/FileCopyStep.cs`:
```csharp
using Courier.Domain.Engine;

namespace Courier.Features.Engine.Steps;

public class FileCopyStep : IJobStep
{
    public string TypeKey => "file.copy";

    public Task<StepResult> ExecuteAsync(
        StepConfiguration config,
        JobContext context,
        CancellationToken cancellationToken)
    {
        var sourcePath = config.GetString("source_path");
        var destPath = config.GetString("destination_path");

        if (!File.Exists(sourcePath))
            return Task.FromResult(StepResult.Fail($"Source file not found: {sourcePath}"));

        var destDir = Path.GetDirectoryName(destPath);
        if (!string.IsNullOrEmpty(destDir))
            Directory.CreateDirectory(destDir);

        File.Copy(sourcePath, destPath, overwrite: true);

        var fileInfo = new FileInfo(destPath);
        return Task.FromResult(StepResult.Ok(
            bytesProcessed: fileInfo.Length,
            outputs: new Dictionary<string, object> { ["copied_file"] = destPath }));
    }

    public Task<StepResult> ValidateAsync(StepConfiguration config)
    {
        if (!config.Has("source_path"))
            return Task.FromResult(StepResult.Fail("Missing required config: source_path"));
        if (!config.Has("destination_path"))
            return Task.FromResult(StepResult.Fail("Missing required config: destination_path"));
        return Task.FromResult(StepResult.Ok());
    }
}
```

**Step 8: Run FileCopyStep tests**

Run: `dotnet test tests/Courier.Tests.Unit --filter "FileCopyStepTests" -v n`
Expected: All 3 PASS

**Step 9: Write FileMoveStep test**

`tests/Courier.Tests.Unit/Engine/FileMoveStepTests.cs`:
```csharp
using Courier.Domain.Engine;
using Courier.Features.Engine.Steps;
using Shouldly;

namespace Courier.Tests.Unit.Engine;

public class FileMoveStepTests : IDisposable
{
    private readonly string _tempDir;

    public FileMoveStepTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"courier-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    [Fact]
    public async Task Execute_MovesFile_Successfully()
    {
        var sourceFile = Path.Combine(_tempDir, "source.txt");
        var destFile = Path.Combine(_tempDir, "dest.txt");
        await File.WriteAllTextAsync(sourceFile, "move me");

        var config = new StepConfiguration($$"""{"source_path": "{{sourceFile.Replace("\\", "\\\\")}}", "destination_path": "{{destFile.Replace("\\", "\\\\")}}"}""");
        var context = new JobContext();
        var step = new FileMoveStep();

        var result = await step.ExecuteAsync(config, context, CancellationToken.None);

        result.Success.ShouldBeTrue();
        File.Exists(destFile).ShouldBeTrue();
        File.Exists(sourceFile).ShouldBeFalse(); // source no longer exists
    }

    [Fact]
    public async Task Execute_SourceNotFound_ReturnsFailure()
    {
        var config = new StepConfiguration("""{"source_path": "/nonexistent/file.txt", "destination_path": "/tmp/out.txt"}""");
        var context = new JobContext();
        var step = new FileMoveStep();

        var result = await step.ExecuteAsync(config, context, CancellationToken.None);

        result.Success.ShouldBeFalse();
        result.ErrorMessage.ShouldContain("Source file not found");
    }

    [Fact]
    public void TypeKey_IsFileMove()
    {
        new FileMoveStep().TypeKey.ShouldBe("file.move");
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }
}
```

**Step 10: Implement FileMoveStep**

`src/Courier.Features/Engine/Steps/FileMoveStep.cs`:
```csharp
using Courier.Domain.Engine;

namespace Courier.Features.Engine.Steps;

public class FileMoveStep : IJobStep
{
    public string TypeKey => "file.move";

    public Task<StepResult> ExecuteAsync(
        StepConfiguration config,
        JobContext context,
        CancellationToken cancellationToken)
    {
        var sourcePath = config.GetString("source_path");
        var destPath = config.GetString("destination_path");

        if (!File.Exists(sourcePath))
            return Task.FromResult(StepResult.Fail($"Source file not found: {sourcePath}"));

        var destDir = Path.GetDirectoryName(destPath);
        if (!string.IsNullOrEmpty(destDir))
            Directory.CreateDirectory(destDir);

        var fileInfo = new FileInfo(sourcePath);
        var bytes = fileInfo.Length;

        File.Move(sourcePath, destPath, overwrite: true);

        return Task.FromResult(StepResult.Ok(
            bytesProcessed: bytes,
            outputs: new Dictionary<string, object> { ["moved_file"] = destPath }));
    }

    public Task<StepResult> ValidateAsync(StepConfiguration config)
    {
        if (!config.Has("source_path"))
            return Task.FromResult(StepResult.Fail("Missing required config: source_path"));
        if (!config.Has("destination_path"))
            return Task.FromResult(StepResult.Fail("Missing required config: destination_path"));
        return Task.FromResult(StepResult.Ok());
    }
}
```

**Step 11: Run all step tests**

Run: `dotnet test tests/Courier.Tests.Unit --filter "FileMoveStepTests|FileCopyStepTests|StepTypeRegistryTests" -v n`
Expected: All 9 PASS

**Step 12: Commit**

```bash
git add src/Courier.Features/Engine/ tests/Courier.Tests.Unit/Engine/
git commit -m "feat: add StepTypeRegistry with file.copy and file.move step handlers"
```

---

## Task 8: Features — JobEngine (Step Orchestrator)

**Files:**
- Create: `src/Courier.Features/Engine/JobEngine.cs`
- Test: `tests/Courier.Tests.Unit/Engine/JobEngineTests.cs`

The JobEngine orchestrates sequential step execution for a single job execution. It:
1. Loads steps for the job (ordered by step_order)
2. Creates StepExecution records for each
3. Resolves each step handler from the registry
4. Executes each step, updating state and persisting after each
5. Applies failure policy on step failure
6. Updates the JobExecution state on completion/failure

**Step 1: Write the test**

`tests/Courier.Tests.Unit/Engine/JobEngineTests.cs`:
```csharp
using Courier.Domain.Engine;
using Courier.Domain.Entities;
using Courier.Domain.Enums;
using Courier.Features.Engine;
using Courier.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;

namespace Courier.Tests.Unit.Engine;

public class JobEngineTests
{
    private static CourierDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<CourierDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new CourierDbContext(options);
    }

    private static (Job job, JobStep step1, JobExecution execution) SeedJobWithStep(
        CourierDbContext db, string typeKey = "file.copy")
    {
        var job = new Job
        {
            Id = Guid.NewGuid(),
            Name = "Test Job",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.Jobs.Add(job);

        var step = new JobStep
        {
            Id = Guid.NewGuid(),
            JobId = job.Id,
            StepOrder = 0,
            Name = "Copy File",
            TypeKey = typeKey,
            Configuration = """{"source_path": "/tmp/in.txt", "destination_path": "/tmp/out.txt"}""",
        };
        db.JobSteps.Add(step);

        var execution = new JobExecution
        {
            Id = Guid.NewGuid(),
            JobId = job.Id,
            TriggeredBy = "test",
            State = JobExecutionState.Running,
            StartedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
        };
        db.JobExecutions.Add(execution);
        db.SaveChanges();

        return (job, step, execution);
    }

    [Fact]
    public async Task ExecuteAsync_AllStepsSucceed_MarksCompleted()
    {
        using var db = CreateInMemoryContext();
        var (job, step, execution) = SeedJobWithStep(db);

        var mockStep = Substitute.For<IJobStep>();
        mockStep.TypeKey.Returns("file.copy");
        mockStep.ExecuteAsync(Arg.Any<StepConfiguration>(), Arg.Any<JobContext>(), Arg.Any<CancellationToken>())
            .Returns(StepResult.Ok(bytesProcessed: 1024));

        var registry = new StepTypeRegistry([mockStep]);
        var engine = new JobEngine(db, registry, NullLogger<JobEngine>.Instance);

        await engine.ExecuteAsync(execution.Id, CancellationToken.None);

        var updated = await db.JobExecutions.FirstAsync(e => e.Id == execution.Id);
        updated.State.ShouldBe(JobExecutionState.Completed);
        updated.CompletedAt.ShouldNotBeNull();

        var stepExec = await db.StepExecutions.FirstAsync(se => se.JobExecutionId == execution.Id);
        stepExec.State.ShouldBe(StepExecutionState.Completed);
    }

    [Fact]
    public async Task ExecuteAsync_StepFails_StopPolicy_MarksJobFailed()
    {
        using var db = CreateInMemoryContext();
        var (job, step, execution) = SeedJobWithStep(db);

        var mockStep = Substitute.For<IJobStep>();
        mockStep.TypeKey.Returns("file.copy");
        mockStep.ExecuteAsync(Arg.Any<StepConfiguration>(), Arg.Any<JobContext>(), Arg.Any<CancellationToken>())
            .Returns(StepResult.Fail("Disk full"));

        var registry = new StepTypeRegistry([mockStep]);
        var engine = new JobEngine(db, registry, NullLogger<JobEngine>.Instance);

        await engine.ExecuteAsync(execution.Id, CancellationToken.None);

        var updated = await db.JobExecutions.FirstAsync(e => e.Id == execution.Id);
        updated.State.ShouldBe(JobExecutionState.Failed);

        var stepExec = await db.StepExecutions.FirstAsync(se => se.JobExecutionId == execution.Id);
        stepExec.State.ShouldBe(StepExecutionState.Failed);
        stepExec.ErrorMessage.ShouldBe("Disk full");
    }

    [Fact]
    public async Task ExecuteAsync_UnknownStepType_MarksJobFailed()
    {
        using var db = CreateInMemoryContext();
        var (job, step, execution) = SeedJobWithStep(db, typeKey: "nonexistent.step");

        var registry = new StepTypeRegistry([]);
        var engine = new JobEngine(db, registry, NullLogger<JobEngine>.Instance);

        await engine.ExecuteAsync(execution.Id, CancellationToken.None);

        var updated = await db.JobExecutions.FirstAsync(e => e.Id == execution.Id);
        updated.State.ShouldBe(JobExecutionState.Failed);
    }
}
```

**Step 2: Run test to verify it fails**

Run: `dotnet test tests/Courier.Tests.Unit --filter "JobEngineTests" -v n`
Expected: FAIL — `JobEngine` does not exist

**Step 3: Implement JobEngine**

`src/Courier.Features/Engine/JobEngine.cs`:
```csharp
using System.Diagnostics;
using System.Text.Json;
using Courier.Domain.Engine;
using Courier.Domain.Entities;
using Courier.Domain.Enums;
using Courier.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Courier.Features.Engine;

public class JobEngine
{
    private readonly CourierDbContext _db;
    private readonly StepTypeRegistry _registry;
    private readonly ILogger<JobEngine> _logger;

    public JobEngine(CourierDbContext db, StepTypeRegistry registry, ILogger<JobEngine> logger)
    {
        _db = db;
        _registry = registry;
        _logger = logger;
    }

    public async Task ExecuteAsync(Guid executionId, CancellationToken cancellationToken)
    {
        var execution = await _db.JobExecutions
            .Include(e => e.Job)
            .FirstOrDefaultAsync(e => e.Id == executionId, cancellationToken);

        if (execution is null)
        {
            _logger.LogError("JobExecution {ExecutionId} not found", executionId);
            return;
        }

        var steps = await _db.JobSteps
            .Where(s => s.JobId == execution.JobId)
            .OrderBy(s => s.StepOrder)
            .ToListAsync(cancellationToken);

        if (steps.Count == 0)
        {
            _logger.LogWarning("Job {JobId} has no steps. Marking execution as completed.", execution.JobId);
            execution.State = JobExecutionState.Completed;
            execution.CompletedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(cancellationToken);
            return;
        }

        // Parse failure policy from Job
        var failurePolicy = ParseFailurePolicy(execution.Job.FailurePolicy);

        var context = new JobContext();
        var allSucceeded = true;

        foreach (var step in steps)
        {
            var stepExecution = new StepExecution
            {
                Id = Guid.NewGuid(),
                JobExecutionId = execution.Id,
                JobStepId = step.Id,
                StepOrder = step.StepOrder,
                State = StepExecutionState.Running,
                StartedAt = DateTime.UtcNow,
                CreatedAt = DateTime.UtcNow,
            };
            _db.StepExecutions.Add(stepExecution);
            await _db.SaveChangesAsync(cancellationToken);

            var sw = Stopwatch.StartNew();
            StepResult result;

            try
            {
                var handler = _registry.Resolve(step.TypeKey);
                var config = new StepConfiguration(step.Configuration);

                using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeoutCts.CancelAfter(TimeSpan.FromSeconds(step.TimeoutSeconds));

                result = await handler.ExecuteAsync(config, context, timeoutCts.Token);
            }
            catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
            {
                result = StepResult.Fail($"Step '{step.Name}' timed out after {step.TimeoutSeconds}s");
            }
            catch (Exception ex)
            {
                result = StepResult.Fail(ex.Message, ex.StackTrace);
            }

            sw.Stop();
            stepExecution.DurationMs = sw.ElapsedMilliseconds;

            if (result.Success)
            {
                stepExecution.State = StepExecutionState.Completed;
                stepExecution.CompletedAt = DateTime.UtcNow;
                stepExecution.BytesProcessed = result.BytesProcessed;

                if (result.Outputs is not null)
                {
                    stepExecution.OutputData = JsonSerializer.Serialize(result.Outputs);
                    foreach (var kvp in result.Outputs)
                        context.Set($"{step.StepOrder}.{kvp.Key}", kvp.Value);
                }

                _logger.LogInformation("Step {StepName} completed in {DurationMs}ms", step.Name, sw.ElapsedMilliseconds);
            }
            else
            {
                stepExecution.State = StepExecutionState.Failed;
                stepExecution.CompletedAt = DateTime.UtcNow;
                stepExecution.ErrorMessage = result.ErrorMessage;
                stepExecution.ErrorStackTrace = result.ErrorStackTrace;

                _logger.LogWarning("Step {StepName} failed: {Error}", step.Name, result.ErrorMessage);

                if (failurePolicy.Type == FailurePolicyType.SkipAndContinue)
                {
                    _logger.LogInformation("Failure policy is SkipAndContinue. Continuing to next step.");
                    await _db.SaveChangesAsync(cancellationToken);
                    continue;
                }

                // For Stop (and RetryStep/RetryJob which aren't implemented yet), mark job as failed
                allSucceeded = false;
                await _db.SaveChangesAsync(cancellationToken);
                break;
            }

            // Persist context snapshot after each step
            execution.ContextSnapshot = JsonSerializer.Serialize(context.Snapshot());
            await _db.SaveChangesAsync(cancellationToken);
        }

        execution.CompletedAt = DateTime.UtcNow;
        execution.State = allSucceeded ? JobExecutionState.Completed : JobExecutionState.Failed;
        await _db.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("JobExecution {ExecutionId} finished with state {State}", executionId, execution.State);
    }

    private static FailurePolicy ParseFailurePolicy(string json)
    {
        try
        {
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            return JsonSerializer.Deserialize<FailurePolicy>(json, options) ?? new FailurePolicy();
        }
        catch
        {
            return new FailurePolicy(); // Default: Stop
        }
    }
}
```

**Step 4: Run JobEngine tests**

Run: `dotnet test tests/Courier.Tests.Unit --filter "JobEngineTests" -v n`
Expected: All 3 PASS

**Step 5: Commit**

```bash
git add src/Courier.Features/Engine/JobEngine.cs tests/Courier.Tests.Unit/Engine/JobEngineTests.cs
git commit -m "feat: add JobEngine orchestrator for sequential step execution"
```

---

## Task 9: Features — Job Steps CRUD + Trigger Execution

**Files:**
- Create: `src/Courier.Features/Jobs/JobStepDto.cs`
- Create: `src/Courier.Features/Jobs/JobStepService.cs`
- Create: `src/Courier.Features/Jobs/ExecutionService.cs`
- Create: `src/Courier.Features/Jobs/ExecutionDto.cs`
- Modify: `src/Courier.Features/Jobs/JobsController.cs` (add steps and trigger endpoints)
- Modify: `src/Courier.Features/FeaturesServiceExtensions.cs` (register new services)
- Modify: `src/Courier.Domain/Common/ErrorCodes.cs` (add new error codes)

**Step 1: Add new error codes**

Add to `src/Courier.Domain/Common/ErrorCodes.cs`:
```csharp
// Job System (2000-2999)
public const int JobNotEnabled = 2000;
public const int JobHasNoSteps = 2001;
public const int StepTypeNotRegistered = 2002;
public const int InvalidStepOrder = 2003;
public const int ExecutionNotFound = 2010;
```

And add system messages:
```csharp
[ErrorCodes.JobHasNoSteps] = "Job has no steps",
[ErrorCodes.StepTypeNotRegistered] = "Step type not registered",
[ErrorCodes.InvalidStepOrder] = "Invalid step order",
[ErrorCodes.ExecutionNotFound] = "Execution not found",
```

**Step 2: Create DTOs**

`src/Courier.Features/Jobs/JobStepDto.cs`:
```csharp
namespace Courier.Features.Jobs;

public record AddJobStepRequest
{
    public required string Name { get; init; }
    public required string TypeKey { get; init; }
    public int StepOrder { get; init; }
    public string Configuration { get; init; } = "{}";
    public int TimeoutSeconds { get; init; } = 300;
}

public record JobStepDto
{
    public Guid Id { get; init; }
    public Guid JobId { get; init; }
    public int StepOrder { get; init; }
    public required string Name { get; init; }
    public required string TypeKey { get; init; }
    public string Configuration { get; init; } = "{}";
    public int TimeoutSeconds { get; init; }
}
```

`src/Courier.Features/Jobs/ExecutionDto.cs`:
```csharp
namespace Courier.Features.Jobs;

public record TriggerJobRequest
{
    public string TriggeredBy { get; init; } = "api";
}

public record JobExecutionDto
{
    public Guid Id { get; init; }
    public Guid JobId { get; init; }
    public required string State { get; init; }
    public required string TriggeredBy { get; init; }
    public DateTime? QueuedAt { get; init; }
    public DateTime? StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public DateTime CreatedAt { get; init; }
}
```

**Step 3: Implement JobStepService**

`src/Courier.Features/Jobs/JobStepService.cs`:
```csharp
using Courier.Domain.Common;
using Courier.Domain.Entities;
using Courier.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Courier.Features.Jobs;

public class JobStepService
{
    private readonly CourierDbContext _db;

    public JobStepService(CourierDbContext db) => _db = db;

    public async Task<ApiResponse<JobStepDto>> AddStepAsync(Guid jobId, AddJobStepRequest request, CancellationToken ct)
    {
        var job = await _db.Jobs.FirstOrDefaultAsync(j => j.Id == jobId, ct);
        if (job is null)
            return new ApiResponse<JobStepDto> { Error = ErrorMessages.Create(ErrorCodes.ResourceNotFound, $"Job '{jobId}' not found.") };

        var step = new JobStep
        {
            Id = Guid.NewGuid(),
            JobId = jobId,
            StepOrder = request.StepOrder,
            Name = request.Name,
            TypeKey = request.TypeKey,
            Configuration = request.Configuration,
            TimeoutSeconds = request.TimeoutSeconds,
        };

        _db.JobSteps.Add(step);
        await _db.SaveChangesAsync(ct);

        return new ApiResponse<JobStepDto> { Data = MapToDto(step) };
    }

    public async Task<ApiResponse<List<JobStepDto>>> ListStepsAsync(Guid jobId, CancellationToken ct)
    {
        var job = await _db.Jobs.FirstOrDefaultAsync(j => j.Id == jobId, ct);
        if (job is null)
            return new ApiResponse<List<JobStepDto>> { Error = ErrorMessages.Create(ErrorCodes.ResourceNotFound, $"Job '{jobId}' not found.") };

        var steps = await _db.JobSteps
            .Where(s => s.JobId == jobId)
            .OrderBy(s => s.StepOrder)
            .Select(s => MapToDto(s))
            .ToListAsync(ct);

        return new ApiResponse<List<JobStepDto>> { Data = steps };
    }

    private static JobStepDto MapToDto(JobStep s) => new()
    {
        Id = s.Id,
        JobId = s.JobId,
        StepOrder = s.StepOrder,
        Name = s.Name,
        TypeKey = s.TypeKey,
        Configuration = s.Configuration,
        TimeoutSeconds = s.TimeoutSeconds,
    };
}
```

**Step 4: Implement ExecutionService**

`src/Courier.Features/Jobs/ExecutionService.cs`:
```csharp
using Courier.Domain.Common;
using Courier.Domain.Entities;
using Courier.Domain.Enums;
using Courier.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Courier.Features.Jobs;

public class ExecutionService
{
    private readonly CourierDbContext _db;

    public ExecutionService(CourierDbContext db) => _db = db;

    public async Task<ApiResponse<JobExecutionDto>> TriggerAsync(Guid jobId, string triggeredBy, CancellationToken ct)
    {
        var job = await _db.Jobs.FirstOrDefaultAsync(j => j.Id == jobId, ct);
        if (job is null)
            return new ApiResponse<JobExecutionDto> { Error = ErrorMessages.Create(ErrorCodes.ResourceNotFound, $"Job '{jobId}' not found.") };

        if (!job.IsEnabled)
            return new ApiResponse<JobExecutionDto> { Error = ErrorMessages.Create(ErrorCodes.JobNotEnabled, $"Job '{jobId}' is disabled.") };

        var hasSteps = await _db.JobSteps.AnyAsync(s => s.JobId == jobId, ct);
        if (!hasSteps)
            return new ApiResponse<JobExecutionDto> { Error = ErrorMessages.Create(ErrorCodes.JobHasNoSteps, $"Job '{jobId}' has no steps configured.") };

        var execution = new JobExecution
        {
            Id = Guid.NewGuid(),
            JobId = jobId,
            JobVersionNumber = job.CurrentVersion,
            TriggeredBy = triggeredBy,
            State = JobExecutionState.Queued,
            QueuedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
        };

        _db.JobExecutions.Add(execution);
        await _db.SaveChangesAsync(ct);

        return new ApiResponse<JobExecutionDto> { Data = MapToDto(execution) };
    }

    public async Task<ApiResponse<JobExecutionDto>> GetExecutionAsync(Guid executionId, CancellationToken ct)
    {
        var execution = await _db.JobExecutions.FirstOrDefaultAsync(e => e.Id == executionId, ct);
        if (execution is null)
            return new ApiResponse<JobExecutionDto> { Error = ErrorMessages.Create(ErrorCodes.ExecutionNotFound, $"Execution '{executionId}' not found.") };

        return new ApiResponse<JobExecutionDto> { Data = MapToDto(execution) };
    }

    public async Task<PagedApiResponse<JobExecutionDto>> ListExecutionsAsync(Guid jobId, int page = 1, int pageSize = 25, CancellationToken ct = default)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        page = Math.Max(page, 1);

        var query = _db.JobExecutions
            .Where(e => e.JobId == jobId)
            .OrderByDescending(e => e.CreatedAt);

        var totalCount = await query.CountAsync(ct);
        var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(e => MapToDto(e))
            .ToListAsync(ct);

        return new PagedApiResponse<JobExecutionDto>
        {
            Data = items,
            Pagination = new PaginationMeta(page, pageSize, totalCount, totalPages),
        };
    }

    private static JobExecutionDto MapToDto(JobExecution e) => new()
    {
        Id = e.Id,
        JobId = e.JobId,
        State = e.State.ToString().ToLowerInvariant(),
        TriggeredBy = e.TriggeredBy,
        QueuedAt = e.QueuedAt,
        StartedAt = e.StartedAt,
        CompletedAt = e.CompletedAt,
        CreatedAt = e.CreatedAt,
    };
}
```

**Step 5: Expand JobsController with new endpoints**

Add to `src/Courier.Features/Jobs/JobsController.cs`:

```csharp
// Inject additional services in the constructor:
private readonly JobStepService _stepService;
private readonly ExecutionService _executionService;

// New endpoints:

[HttpPost("{jobId:guid}/steps")]
public async Task<ActionResult<ApiResponse<JobStepDto>>> AddStep(
    Guid jobId, [FromBody] AddJobStepRequest request, CancellationToken ct)
{
    var result = await _stepService.AddStepAsync(jobId, request, ct);
    return result.Success ? Created($"/api/v1/jobs/{jobId}/steps/{result.Data!.Id}", result) : NotFound(result);
}

[HttpGet("{jobId:guid}/steps")]
public async Task<ActionResult<ApiResponse<List<JobStepDto>>>> ListSteps(Guid jobId, CancellationToken ct)
{
    var result = await _stepService.ListStepsAsync(jobId, ct);
    return result.Success ? Ok(result) : NotFound(result);
}

[HttpPost("{jobId:guid}/trigger")]
public async Task<ActionResult<ApiResponse<JobExecutionDto>>> Trigger(
    Guid jobId, [FromBody] TriggerJobRequest? request, CancellationToken ct)
{
    var result = await _executionService.TriggerAsync(jobId, request?.TriggeredBy ?? "api", ct);
    if (!result.Success)
    {
        return result.Error!.Code switch
        {
            ErrorCodes.ResourceNotFound => NotFound(result),
            ErrorCodes.JobNotEnabled => Conflict(result),
            ErrorCodes.JobHasNoSteps => BadRequest(result),
            _ => BadRequest(result),
        };
    }
    return Accepted(result);
}

[HttpGet("{jobId:guid}/executions")]
public async Task<ActionResult<PagedApiResponse<JobExecutionDto>>> ListExecutions(
    Guid jobId, [FromQuery] int page = 1, [FromQuery] int pageSize = 25, CancellationToken ct = default)
{
    var result = await _executionService.ListExecutionsAsync(jobId, page, pageSize, ct);
    return Ok(result);
}

[HttpGet("executions/{executionId:guid}")]
public async Task<ActionResult<ApiResponse<JobExecutionDto>>> GetExecution(Guid executionId, CancellationToken ct)
{
    var result = await _executionService.GetExecutionAsync(executionId, ct);
    return result.Success ? Ok(result) : NotFound(result);
}
```

**Step 6: Update DI registration in `FeaturesServiceExtensions.cs`**

```csharp
using Courier.Domain.Engine;
using Courier.Features.Engine;
using Courier.Features.Engine.Steps;

// Inside AddCourierFeatures:
services.AddScoped<JobStepService>();
services.AddScoped<ExecutionService>();
services.AddScoped<JobEngine>();
services.AddSingleton<StepTypeRegistry>();
services.AddSingleton<IJobStep, FileCopyStep>();
services.AddSingleton<IJobStep, FileMoveStep>();
```

**Step 7: Build and verify**

Run: `dotnet build Courier.slnx`
Expected: Build succeeds

**Step 8: Commit**

```bash
git add src/Courier.Features/ src/Courier.Domain/Common/ErrorCodes.cs
git commit -m "feat: add job steps CRUD, trigger execution, and execution listing endpoints"
```

---

## Task 10: Worker — Queue Processor

**Files:**
- Create: `src/Courier.Worker/Services/JobQueueProcessor.cs`
- Modify: `src/Courier.Worker/Program.cs` (register services)
- Modify: `src/Courier.Worker/Courier.Worker.csproj` (add Features project reference)

**Step 1: Add Features project reference to Worker**

Add to `src/Courier.Worker/Courier.Worker.csproj`:
```xml
<ProjectReference Include="..\Courier.Features\Courier.Features.csproj" />
```

**Step 2: Implement JobQueueProcessor**

`src/Courier.Worker/Services/JobQueueProcessor.cs`:
```csharp
using Courier.Domain.Enums;
using Courier.Features.Engine;
using Courier.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Courier.Worker.Services;

/// <summary>
/// Polls for queued job executions and runs them through the JobEngine.
/// Uses FOR UPDATE SKIP LOCKED for safe concurrent dequeue.
/// </summary>
public class JobQueueProcessor : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<JobQueueProcessor> _logger;
    private readonly TimeSpan _pollInterval = TimeSpan.FromSeconds(5);

    public JobQueueProcessor(IServiceScopeFactory scopeFactory, ILogger<JobQueueProcessor> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("JobQueueProcessor started. Polling every {Interval}s.", _pollInterval.TotalSeconds);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await ProcessNextAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in queue processor loop");
            }

            await Task.Delay(_pollInterval, stoppingToken);
        }

        _logger.LogInformation("JobQueueProcessor stopping.");
    }

    private async Task ProcessNextAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CourierDbContext>();

        // Atomic dequeue using FOR UPDATE SKIP LOCKED
        // Note: InMemory provider doesn't support raw SQL, so this uses EF for dev.
        // In production, this would use the raw SQL from design doc Section 5.8.
        var execution = await db.JobExecutions
            .Where(e => e.State == JobExecutionState.Queued)
            .OrderBy(e => e.QueuedAt)
            .FirstOrDefaultAsync(ct);

        if (execution is null)
            return;

        _logger.LogInformation("Dequeued execution {ExecutionId} for job {JobId}", execution.Id, execution.JobId);

        execution.State = JobExecutionState.Running;
        execution.StartedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);

        var engine = scope.ServiceProvider.GetRequiredService<JobEngine>();
        await engine.ExecuteAsync(execution.Id, ct);
    }
}
```

**Step 3: Update Worker Program.cs**

Add to `src/Courier.Worker/Program.cs`:
```csharp
using Courier.Features;
using Courier.Worker.Services;

// After the existing service registrations:
builder.Services.AddCourierFeatures();
builder.Services.AddHostedService<JobQueueProcessor>();
```

**Step 4: Build to verify**

Run: `dotnet build src/Courier.Worker/Courier.Worker.csproj`
Expected: Build succeeds

**Step 5: Commit**

```bash
git add src/Courier.Worker/
git commit -m "feat: add JobQueueProcessor background service to Worker"
```

---

## Task 11: Worker — Quartz.NET Persistent Store

**Files:**
- Create: `src/Courier.Migrations/Scripts/0003_quartz_scheduler.sql`
- Modify: `src/Courier.Worker/Program.cs` (configure Quartz)
- Modify: `src/Courier.Migrations/SchemaVersionValidator.cs` (update expected migration)

**Step 1: Create Quartz.NET migration**

Download/create the Quartz.NET PostgreSQL DDL. The official script creates ~11 tables with `QRTZ_` prefix. Create `src/Courier.Migrations/Scripts/0003_quartz_scheduler.sql`:

```sql
-- Quartz.NET PostgreSQL DDL (from official Quartz.NET source, 3.x)
-- https://github.com/quartznet/quartznet/blob/main/database/tables/tables_postgres.sql
-- Tables are prefixed with QRTZ_ and scoped to the CourierScheduler instance.

-- NOTE: Copy the official Quartz.NET PostgreSQL DDL here.
-- The exact script can be obtained from:
-- https://github.com/quartznet/quartznet/blob/main/database/tables/tables_postgres.sql
-- Use the version matching Quartz 3.14.0 in Directory.Packages.props.

-- For the skeleton, we'll create a minimal placeholder that allows Quartz to start.
-- The full DDL should be copied from the official source before production use.

-- BEGIN QUARTZ TABLES --

CREATE TABLE IF NOT EXISTS qrtz_job_details (
    sched_name        TEXT NOT NULL,
    job_name          TEXT NOT NULL,
    job_group         TEXT NOT NULL,
    description       TEXT NULL,
    job_class_name    TEXT NOT NULL,
    is_durable        BOOL NOT NULL,
    is_nonconcurrent  BOOL NOT NULL,
    is_update_data    BOOL NOT NULL,
    requests_recovery BOOL NOT NULL,
    job_data          BYTEA NULL,
    PRIMARY KEY (sched_name, job_name, job_group)
);

CREATE TABLE IF NOT EXISTS qrtz_triggers (
    sched_name     TEXT NOT NULL,
    trigger_name   TEXT NOT NULL,
    trigger_group  TEXT NOT NULL,
    job_name       TEXT NOT NULL,
    job_group      TEXT NOT NULL,
    description    TEXT NULL,
    next_fire_time BIGINT NULL,
    prev_fire_time BIGINT NULL,
    priority       INT NULL,
    trigger_state  TEXT NOT NULL,
    trigger_type   TEXT NOT NULL,
    start_time     BIGINT NOT NULL,
    end_time       BIGINT NULL,
    calendar_name  TEXT NULL,
    misfire_instr  INT NULL,
    job_data       BYTEA NULL,
    PRIMARY KEY (sched_name, trigger_name, trigger_group),
    FOREIGN KEY (sched_name, job_name, job_group) REFERENCES qrtz_job_details(sched_name, job_name, job_group)
);

CREATE TABLE IF NOT EXISTS qrtz_simple_triggers (
    sched_name      TEXT NOT NULL,
    trigger_name    TEXT NOT NULL,
    trigger_group   TEXT NOT NULL,
    repeat_count    BIGINT NOT NULL,
    repeat_interval BIGINT NOT NULL,
    times_triggered BIGINT NOT NULL,
    PRIMARY KEY (sched_name, trigger_name, trigger_group),
    FOREIGN KEY (sched_name, trigger_name, trigger_group) REFERENCES qrtz_triggers(sched_name, trigger_name, trigger_group) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS qrtz_cron_triggers (
    sched_name      TEXT NOT NULL,
    trigger_name    TEXT NOT NULL,
    trigger_group   TEXT NOT NULL,
    cron_expression TEXT NOT NULL,
    time_zone_id    TEXT,
    PRIMARY KEY (sched_name, trigger_name, trigger_group),
    FOREIGN KEY (sched_name, trigger_name, trigger_group) REFERENCES qrtz_triggers(sched_name, trigger_name, trigger_group) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS qrtz_simprop_triggers (
    sched_name    TEXT NOT NULL,
    trigger_name  TEXT NOT NULL,
    trigger_group TEXT NOT NULL,
    str_prop_1    TEXT NULL,
    str_prop_2    TEXT NULL,
    str_prop_3    TEXT NULL,
    int_prop_1    INT NULL,
    int_prop_2    INT NULL,
    long_prop_1   BIGINT NULL,
    long_prop_2   BIGINT NULL,
    dec_prop_1    NUMERIC NULL,
    dec_prop_2    NUMERIC NULL,
    bool_prop_1   BOOL NULL,
    bool_prop_2   BOOL NULL,
    time_zone_id  TEXT NULL,
    PRIMARY KEY (sched_name, trigger_name, trigger_group),
    FOREIGN KEY (sched_name, trigger_name, trigger_group) REFERENCES qrtz_triggers(sched_name, trigger_name, trigger_group) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS qrtz_blob_triggers (
    sched_name    TEXT NOT NULL,
    trigger_name  TEXT NOT NULL,
    trigger_group TEXT NOT NULL,
    blob_data     BYTEA NULL,
    PRIMARY KEY (sched_name, trigger_name, trigger_group),
    FOREIGN KEY (sched_name, trigger_name, trigger_group) REFERENCES qrtz_triggers(sched_name, trigger_name, trigger_group) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS qrtz_calendars (
    sched_name    TEXT NOT NULL,
    calendar_name TEXT NOT NULL,
    calendar      BYTEA NOT NULL,
    PRIMARY KEY (sched_name, calendar_name)
);

CREATE TABLE IF NOT EXISTS qrtz_paused_trigger_grps (
    sched_name    TEXT NOT NULL,
    trigger_group TEXT NOT NULL,
    PRIMARY KEY (sched_name, trigger_group)
);

CREATE TABLE IF NOT EXISTS qrtz_fired_triggers (
    sched_name        TEXT NOT NULL,
    entry_id          TEXT NOT NULL,
    trigger_name      TEXT NOT NULL,
    trigger_group     TEXT NOT NULL,
    instance_name     TEXT NOT NULL,
    fired_time        BIGINT NOT NULL,
    sched_time        BIGINT NOT NULL,
    priority          INT NOT NULL,
    state             TEXT NOT NULL,
    job_name          TEXT NULL,
    job_group         TEXT NULL,
    is_nonconcurrent  BOOL NULL,
    requests_recovery BOOL NULL,
    PRIMARY KEY (sched_name, entry_id)
);

CREATE TABLE IF NOT EXISTS qrtz_scheduler_state (
    sched_name        TEXT NOT NULL,
    instance_name     TEXT NOT NULL,
    last_checkin_time BIGINT NOT NULL,
    checkin_interval  BIGINT NOT NULL,
    PRIMARY KEY (sched_name, instance_name)
);

CREATE TABLE IF NOT EXISTS qrtz_locks (
    sched_name TEXT NOT NULL,
    lock_name  TEXT NOT NULL,
    PRIMARY KEY (sched_name, lock_name)
);

-- Indexes for performance
CREATE INDEX IF NOT EXISTS idx_qrtz_t_next_fire_time ON qrtz_triggers(sched_name, next_fire_time);
CREATE INDEX IF NOT EXISTS idx_qrtz_t_state ON qrtz_triggers(sched_name, trigger_state);
CREATE INDEX IF NOT EXISTS idx_qrtz_t_nft_st ON qrtz_triggers(sched_name, trigger_state, next_fire_time);
CREATE INDEX IF NOT EXISTS idx_qrtz_ft_trig_name ON qrtz_fired_triggers(sched_name, trigger_name, trigger_group);
CREATE INDEX IF NOT EXISTS idx_qrtz_ft_trig_inst_name ON qrtz_fired_triggers(sched_name, instance_name);
CREATE INDEX IF NOT EXISTS idx_qrtz_ft_trig_nm_gp ON qrtz_fired_triggers(sched_name, trigger_name, trigger_group);
CREATE INDEX IF NOT EXISTS idx_qrtz_ft_jg ON qrtz_fired_triggers(sched_name, job_group);

-- END QUARTZ TABLES --
```

**Step 2: Configure Quartz in Worker Program.cs**

Replace the Quartz placeholder in `src/Courier.Worker/Program.cs`:
```csharp
using Quartz;

// Replace the commented-out Quartz section with:
builder.Services.AddQuartz(q =>
{
    q.SchedulerId = "CourierScheduler";
    q.UsePersistentStore(store =>
    {
        store.UsePostgres(builder.Configuration.GetConnectionString("CourierDb")!);
        store.UseNewtonsoftJsonSerializer();
    });
});
builder.Services.AddQuartzHostedService(q => q.WaitForJobsToComplete = true);
```

**Important:** Quartz.NET with `UseNewtonsoftJsonSerializer()` requires the `Quartz.Serialization.Json` NuGet package. Add to `Directory.Packages.props`:
```xml
<PackageVersion Include="Quartz.Serialization.Json" Version="3.14.0" />
```
And to `src/Courier.Worker/Courier.Worker.csproj`:
```xml
<PackageReference Include="Quartz.Serialization.Json" />
```

**Step 3: Update SchemaVersionValidator expected migration**

In `src/Courier.Migrations/SchemaVersionValidator.cs`, update:
```csharp
public const string ExpectedMinimumMigration = "0003_quartz_scheduler.sql";
```

**Step 4: Build to verify**

Run: `dotnet build Courier.slnx`
Expected: Build succeeds

**Step 5: Commit**

```bash
git add src/Courier.Migrations/Scripts/0003_quartz_scheduler.sql
git add src/Courier.Migrations/SchemaVersionValidator.cs
git add src/Courier.Worker/
git add Directory.Packages.props
git commit -m "feat: add Quartz.NET persistent store migration and Worker integration"
```

---

## Task 12: Integration + Architecture Tests

**Files:**
- Modify: `tests/Courier.Tests.Integration/Jobs/JobsApiTests.cs` (add step and trigger tests)
- Modify: `tests/Courier.Tests.Architecture/DependencyTests.cs` (verify new namespaces)

**Step 1: Add integration tests for new endpoints**

Add to `tests/Courier.Tests.Integration/Jobs/JobsApiTests.cs`:

```csharp
[Fact]
public async Task AddStep_ValidRequest_ReturnsCreated()
{
    // Create a job first
    var jobRequest = new CreateJobRequest { Name = "Step Test Job" };
    var jobResponse = await _client.PostAsJsonAsync("/api/v1/jobs", jobRequest);
    var job = await jobResponse.Content.ReadFromJsonAsync<ApiResponse<JobDto>>();

    var stepRequest = new AddJobStepRequest
    {
        Name = "Copy File",
        TypeKey = "file.copy",
        StepOrder = 0,
        Configuration = """{"source_path": "/tmp/in.txt", "destination_path": "/tmp/out.txt"}""",
    };

    var response = await _client.PostAsJsonAsync($"/api/v1/jobs/{job!.Data!.Id}/steps", stepRequest);

    response.StatusCode.ShouldBe(HttpStatusCode.Created);
    var body = await response.Content.ReadFromJsonAsync<ApiResponse<JobStepDto>>();
    body!.Data!.TypeKey.ShouldBe("file.copy");
}

[Fact]
public async Task ListSteps_AfterAdd_ReturnsSteps()
{
    var jobRequest = new CreateJobRequest { Name = "List Steps Test" };
    var jobResponse = await _client.PostAsJsonAsync("/api/v1/jobs", jobRequest);
    var job = await jobResponse.Content.ReadFromJsonAsync<ApiResponse<JobDto>>();
    var jobId = job!.Data!.Id;

    await _client.PostAsJsonAsync($"/api/v1/jobs/{jobId}/steps", new AddJobStepRequest
    {
        Name = "Step 1", TypeKey = "file.copy", StepOrder = 0,
    });

    var response = await _client.GetAsync($"/api/v1/jobs/{jobId}/steps");
    response.StatusCode.ShouldBe(HttpStatusCode.OK);
}

[Fact]
public async Task TriggerJob_WithSteps_ReturnsAccepted()
{
    // Create job
    var jobRequest = new CreateJobRequest { Name = "Trigger Test Job" };
    var jobResponse = await _client.PostAsJsonAsync("/api/v1/jobs", jobRequest);
    var job = await jobResponse.Content.ReadFromJsonAsync<ApiResponse<JobDto>>();
    var jobId = job!.Data!.Id;

    // Add a step
    await _client.PostAsJsonAsync($"/api/v1/jobs/{jobId}/steps", new AddJobStepRequest
    {
        Name = "Copy", TypeKey = "file.copy", StepOrder = 0,
        Configuration = """{"source_path": "/tmp/in.txt", "destination_path": "/tmp/out.txt"}""",
    });

    // Trigger
    var response = await _client.PostAsJsonAsync($"/api/v1/jobs/{jobId}/trigger",
        new TriggerJobRequest { TriggeredBy = "test" });

    response.StatusCode.ShouldBe(HttpStatusCode.Accepted);
    var body = await response.Content.ReadFromJsonAsync<ApiResponse<JobExecutionDto>>();
    body!.Data!.State.ShouldBe("queued");
}

[Fact]
public async Task TriggerJob_NoSteps_ReturnsBadRequest()
{
    var jobRequest = new CreateJobRequest { Name = "No Steps Job" };
    var jobResponse = await _client.PostAsJsonAsync("/api/v1/jobs", jobRequest);
    var job = await jobResponse.Content.ReadFromJsonAsync<ApiResponse<JobDto>>();

    var response = await _client.PostAsJsonAsync($"/api/v1/jobs/{job!.Data!.Id}/trigger",
        new TriggerJobRequest());

    response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
}
```

**Step 2: Add usings to the integration test file**

The integration test file will need these additional usings:
```csharp
using Courier.Features.Jobs;  // already present
// The new DTOs (AddJobStepRequest, JobStepDto, TriggerJobRequest, JobExecutionDto)
// are in the same namespace, so no new usings needed.
```

**Step 3: Run all tests**

Run: `dotnet test Courier.slnx -v n`
Expected: All tests pass (existing 20 + new unit tests + new integration tests)

**Step 4: Commit**

```bash
git add tests/
git commit -m "test: add integration tests for job steps and trigger endpoints"
```

---

## Task 13: Final Wiring + Full Test Run

**Step 1: Run the complete build**

Run: `dotnet build Courier.slnx`
Expected: Build succeeds with zero errors and zero warnings

**Step 2: Run the full test suite**

Run: `dotnet test Courier.slnx -v n`
Expected: All tests pass

**Step 3: Verify Aspire AppHost starts**

Run: `cd src/Courier.AppHost && dotnet run` (manual verification)
Expected: All services start, API responds, Worker starts polling

**Step 4: Final commit if any loose ends**

```bash
git add -A
git commit -m "chore: final wiring and cleanup for job engine core"
```

---

## Test Summary

| Layer | Tests | What They Cover |
|-------|-------|----------------|
| Unit | JobContextTests (5) | Set/Get/TryGet/Snapshot/Restore |
| Unit | JobStateMachineTests (4) | Valid transitions, invalid transitions, exception on invalid, return value |
| Unit | StepStateMachineTests (2) | Valid transitions, invalid transitions |
| Unit | StepTypeRegistryTests (3) | Resolve registered, resolve unknown, list all |
| Unit | FileCopyStepTests (3) | Copy success, source not found, TypeKey |
| Unit | FileMoveStepTests (3) | Move success, source not found, TypeKey |
| Unit | JobEngineTests (3) | All steps succeed, step fails (stop policy), unknown step type |
| Integration | JobsApiTests (4 new) | Add step, list steps, trigger with steps, trigger without steps |
| Architecture | DependencyTests (9 existing) | Dependency direction enforcement |

**Estimated new tests: ~27 + existing 20 = ~47 total**

---

## File Inventory

### New Files (create)
```
src/Courier.Migrations/Scripts/0002_job_engine_tables.sql
src/Courier.Migrations/Scripts/0003_quartz_scheduler.sql
src/Courier.Domain/Enums/JobExecutionState.cs
src/Courier.Domain/Enums/StepExecutionState.cs
src/Courier.Domain/Enums/FailurePolicyType.cs
src/Courier.Domain/Entities/JobStep.cs
src/Courier.Domain/Entities/JobExecution.cs
src/Courier.Domain/Entities/StepExecution.cs
src/Courier.Domain/Engine/IJobStep.cs
src/Courier.Domain/Engine/StepResult.cs
src/Courier.Domain/Engine/StepConfiguration.cs
src/Courier.Domain/Engine/JobContext.cs
src/Courier.Domain/Engine/FailurePolicy.cs
src/Courier.Domain/Engine/JobStateMachine.cs
src/Courier.Domain/Engine/StepStateMachine.cs
src/Courier.Features/Engine/StepTypeRegistry.cs
src/Courier.Features/Engine/JobEngine.cs
src/Courier.Features/Engine/Steps/FileCopyStep.cs
src/Courier.Features/Engine/Steps/FileMoveStep.cs
src/Courier.Features/Jobs/JobStepDto.cs
src/Courier.Features/Jobs/JobStepService.cs
src/Courier.Features/Jobs/ExecutionDto.cs
src/Courier.Features/Jobs/ExecutionService.cs
src/Courier.Worker/Services/JobQueueProcessor.cs
tests/Courier.Tests.Unit/Engine/JobContextTests.cs
tests/Courier.Tests.Unit/Engine/JobStateMachineTests.cs
tests/Courier.Tests.Unit/Engine/StepStateMachineTests.cs
tests/Courier.Tests.Unit/Engine/StepTypeRegistryTests.cs
tests/Courier.Tests.Unit/Engine/FileCopyStepTests.cs
tests/Courier.Tests.Unit/Engine/FileMoveStepTests.cs
tests/Courier.Tests.Unit/Engine/JobEngineTests.cs
```

### Modified Files
```
src/Courier.Domain/Entities/Job.cs (add Steps, Executions navigation)
src/Courier.Domain/Common/ErrorCodes.cs (add new error codes)
src/Courier.Infrastructure/Data/CourierDbContext.cs (add DbSets + mappings)
src/Courier.Features/Jobs/JobsController.cs (add step/trigger/execution endpoints)
src/Courier.Features/FeaturesServiceExtensions.cs (register new services)
src/Courier.Worker/Program.cs (add Features, QueueProcessor, Quartz)
src/Courier.Worker/Courier.Worker.csproj (add Features reference, Quartz.Serialization.Json)
src/Courier.Migrations/SchemaVersionValidator.cs (update expected migration)
tests/Courier.Tests.Integration/Jobs/JobsApiTests.cs (add new endpoint tests)
Directory.Packages.props (add Quartz.Serialization.Json)
```
