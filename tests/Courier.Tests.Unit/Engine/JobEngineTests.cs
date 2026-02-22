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
