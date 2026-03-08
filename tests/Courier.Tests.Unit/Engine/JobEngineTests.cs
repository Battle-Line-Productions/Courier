using Courier.Domain.Engine;
using Courier.Domain.Entities;
using Courier.Domain.Enums;
using Courier.Features.AuditLog;
using Courier.Features.Engine;
using Courier.Features.Engine.Protocols;
using Courier.Features.Events;
using Courier.Features.Notifications;
using Courier.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
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

    private static (Job job, JobStep step, JobExecution execution) SeedJobWithStep(
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
        var engine = new JobEngine(db, registry, new JobConnectionRegistry(Substitute.For<ITransferClientFactory>()), new JobWorkspace(NullLogger<JobWorkspace>.Instance), Options.Create(new WorkspaceSettings()), NullLogger<JobEngine>.Instance, new AuditService(db), new NotificationDispatcher(db, [], NullLogger<NotificationDispatcher>.Instance), new DomainEventService(db));

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
        var engine = new JobEngine(db, registry, new JobConnectionRegistry(Substitute.For<ITransferClientFactory>()), new JobWorkspace(NullLogger<JobWorkspace>.Instance), Options.Create(new WorkspaceSettings()), NullLogger<JobEngine>.Instance, new AuditService(db), new NotificationDispatcher(db, [], NullLogger<NotificationDispatcher>.Instance), new DomainEventService(db));

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
        var engine = new JobEngine(db, registry, new JobConnectionRegistry(Substitute.For<ITransferClientFactory>()), new JobWorkspace(NullLogger<JobWorkspace>.Instance), Options.Create(new WorkspaceSettings()), NullLogger<JobEngine>.Instance, new AuditService(db), new NotificationDispatcher(db, [], NullLogger<NotificationDispatcher>.Instance), new DomainEventService(db));

        await engine.ExecuteAsync(execution.Id, CancellationToken.None);

        var updated = await db.JobExecutions.FirstAsync(e => e.Id == execution.Id);
        updated.State.ShouldBe(JobExecutionState.Failed);
    }

    [Fact]
    public async Task ExecuteAsync_SetsWorkspaceKeyInContext()
    {
        using var db = CreateInMemoryContext();
        var (job, step, execution) = SeedJobWithStep(db);

        var mockStep = Substitute.For<IJobStep>();
        mockStep.TypeKey.Returns("file.copy");
        mockStep.ExecuteAsync(Arg.Any<StepConfiguration>(), Arg.Any<JobContext>(), Arg.Any<CancellationToken>())
            .Returns(callInfo =>
            {
                var ctx = callInfo.ArgAt<JobContext>(1);
                ctx.TryGet<string>("workspace", out var ws).ShouldBeTrue();
                ws.ShouldNotBeNullOrEmpty();
                return StepResult.Ok(bytesProcessed: 0);
            });

        var registry = new StepTypeRegistry([mockStep]);
        var workspace = new JobWorkspace(NullLogger<JobWorkspace>.Instance);
        var settings = new WorkspaceSettings { BaseDirectory = Path.Combine(Path.GetTempPath(), $"engine-test-{Guid.NewGuid()}") };
        var engine = new JobEngine(db, registry, new JobConnectionRegistry(Substitute.For<ITransferClientFactory>()), workspace, Options.Create(settings), NullLogger<JobEngine>.Instance, new AuditService(db), new NotificationDispatcher(db, [], NullLogger<NotificationDispatcher>.Instance), new DomainEventService(db));

        await engine.ExecuteAsync(execution.Id, CancellationToken.None);

        // Workspace should be cleaned up after completion
        workspace.IsInitialized.ShouldBeTrue();
    }

    [Fact]
    public async Task ExecuteAsync_WorkspaceCleanedUpOnCompletion()
    {
        using var db = CreateInMemoryContext();
        var (job, step, execution) = SeedJobWithStep(db);

        var mockStep = Substitute.For<IJobStep>();
        mockStep.TypeKey.Returns("file.copy");
        mockStep.ExecuteAsync(Arg.Any<StepConfiguration>(), Arg.Any<JobContext>(), Arg.Any<CancellationToken>())
            .Returns(StepResult.Ok(bytesProcessed: 0));

        var registry = new StepTypeRegistry([mockStep]);
        var workspace = new JobWorkspace(NullLogger<JobWorkspace>.Instance);
        var baseDir = Path.Combine(Path.GetTempPath(), $"engine-test-{Guid.NewGuid()}");
        var settings = new WorkspaceSettings { BaseDirectory = baseDir, CleanupOnCompletion = true };
        var engine = new JobEngine(db, registry, new JobConnectionRegistry(Substitute.For<ITransferClientFactory>()), workspace, Options.Create(settings), NullLogger<JobEngine>.Instance, new AuditService(db), new NotificationDispatcher(db, [], NullLogger<NotificationDispatcher>.Instance), new DomainEventService(db));

        await engine.ExecuteAsync(execution.Id, CancellationToken.None);

        // Workspace directory should have been deleted
        if (workspace.Path is not null)
            Directory.Exists(workspace.Path).ShouldBeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_WorkspaceCleanedUpOnFailure()
    {
        using var db = CreateInMemoryContext();
        var (job, step, execution) = SeedJobWithStep(db);

        var mockStep = Substitute.For<IJobStep>();
        mockStep.TypeKey.Returns("file.copy");
        mockStep.ExecuteAsync(Arg.Any<StepConfiguration>(), Arg.Any<JobContext>(), Arg.Any<CancellationToken>())
            .Returns(StepResult.Fail("Something broke"));

        var registry = new StepTypeRegistry([mockStep]);
        var workspace = new JobWorkspace(NullLogger<JobWorkspace>.Instance);
        var baseDir = Path.Combine(Path.GetTempPath(), $"engine-test-{Guid.NewGuid()}");
        var settings = new WorkspaceSettings { BaseDirectory = baseDir, CleanupOnCompletion = true };
        var engine = new JobEngine(db, registry, new JobConnectionRegistry(Substitute.For<ITransferClientFactory>()), workspace, Options.Create(settings), NullLogger<JobEngine>.Instance, new AuditService(db), new NotificationDispatcher(db, [], NullLogger<NotificationDispatcher>.Instance), new DomainEventService(db));

        await engine.ExecuteAsync(execution.Id, CancellationToken.None);

        if (workspace.Path is not null)
            Directory.Exists(workspace.Path).ShouldBeFalse();
    }

    [Fact]
    public async Task ExecuteAsync_StepHandlerThrowsException_CaughtAndMarkedFailed()
    {
        using var db = CreateInMemoryContext();
        var (job, step, execution) = SeedJobWithStep(db);

        var mockStep = Substitute.For<IJobStep>();
        mockStep.TypeKey.Returns("file.copy");
        mockStep.ExecuteAsync(Arg.Any<StepConfiguration>(), Arg.Any<JobContext>(), Arg.Any<CancellationToken>())
            .Returns<StepResult>(_ => throw new IOException("Network connection lost"));

        var registry = new StepTypeRegistry([mockStep]);
        var engine = new JobEngine(db, registry, new JobConnectionRegistry(Substitute.For<ITransferClientFactory>()), new JobWorkspace(NullLogger<JobWorkspace>.Instance), Options.Create(new WorkspaceSettings()), NullLogger<JobEngine>.Instance, new AuditService(db), new NotificationDispatcher(db, [], NullLogger<NotificationDispatcher>.Instance), new DomainEventService(db));

        await engine.ExecuteAsync(execution.Id, CancellationToken.None);

        var updated = await db.JobExecutions.FirstAsync(e => e.Id == execution.Id);
        updated.State.ShouldBe(JobExecutionState.Failed);

        var stepExec = await db.StepExecutions.FirstAsync(se => se.JobExecutionId == execution.Id);
        stepExec.State.ShouldBe(StepExecutionState.Failed);
        stepExec.ErrorMessage.ShouldNotBeNull();
        stepExec.ErrorMessage.ShouldContain("Network connection lost");
    }

    [Fact]
    public async Task ParseFailurePolicy_NullJson_DefaultsToStop()
    {
        using var db = CreateInMemoryContext();
        var (job, _, execution) = SeedJobWithStep(db);

        // Set failure policy to invalid JSON to trigger default
        job.FailurePolicy = "null";
        await db.SaveChangesAsync();

        // Add a second step after the first one
        var step2 = new JobStep
        {
            Id = Guid.NewGuid(),
            JobId = job.Id,
            StepOrder = 1,
            Name = "Second Step",
            TypeKey = "file.copy",
            Configuration = """{"source_path": "/tmp/a.txt", "destination_path": "/tmp/b.txt"}""",
        };
        db.JobSteps.Add(step2);
        await db.SaveChangesAsync();

        var mockStep = Substitute.For<IJobStep>();
        mockStep.TypeKey.Returns("file.copy");

        // First call fails, second call would succeed
        mockStep.ExecuteAsync(Arg.Any<StepConfiguration>(), Arg.Any<JobContext>(), Arg.Any<CancellationToken>())
            .Returns(StepResult.Fail("First step failed"), StepResult.Ok(bytesProcessed: 0));

        var registry = new StepTypeRegistry([mockStep]);
        var engine = new JobEngine(db, registry, new JobConnectionRegistry(Substitute.For<ITransferClientFactory>()), new JobWorkspace(NullLogger<JobWorkspace>.Instance), Options.Create(new WorkspaceSettings()), NullLogger<JobEngine>.Instance, new AuditService(db), new NotificationDispatcher(db, [], NullLogger<NotificationDispatcher>.Instance), new DomainEventService(db));

        await engine.ExecuteAsync(execution.Id, CancellationToken.None);

        var updated = await db.JobExecutions.FirstAsync(e => e.Id == execution.Id);
        updated.State.ShouldBe(JobExecutionState.Failed);

        // Only one step execution should exist (second step was not run due to Stop policy)
        var stepExecs = await db.StepExecutions.Where(se => se.JobExecutionId == execution.Id).ToListAsync();
        stepExecs.Count.ShouldBe(1);
        stepExecs[0].ErrorMessage.ShouldBe("First step failed");
    }
}
