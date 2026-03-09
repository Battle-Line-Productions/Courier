using Courier.Domain.Common;
using Courier.Domain.Entities;
using Courier.Domain.Enums;
using Courier.Features.AuditLog;
using Courier.Features.Chains;
using Courier.Features.Events;
using Courier.Features.Jobs;
using Courier.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace Courier.Tests.Unit.Chains;

public class ChainExecutionTests
{
    private static CourierDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<CourierDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new CourierDbContext(options);
    }

    private static async Task<(JobChainDto Chain, Guid Job1Id, Guid Job2Id, Guid Job3Id)> SetupChainWithThreeJobs(
        CourierDbContext db, AuditService audit)
    {
        var jobService = new JobService(db, audit);
        var chainService = new ChainService(db, audit);

        var job1 = await jobService.CreateAsync(new CreateJobRequest { Name = "Download" });
        var job2 = await jobService.CreateAsync(new CreateJobRequest { Name = "Process" });
        var job3 = await jobService.CreateAsync(new CreateJobRequest { Name = "Upload" });

        var chain = await chainService.CreateAsync(new CreateChainRequest { Name = "Pipeline" });

        var members = new List<ChainMemberInput>
        {
            new() { JobId = job1.Data!.Id, ExecutionOrder = 1 },
            new() { JobId = job2.Data!.Id, ExecutionOrder = 2, DependsOnMemberIndex = 0 },
            new() { JobId = job3.Data!.Id, ExecutionOrder = 3, DependsOnMemberIndex = 1 },
        };

        await chainService.ReplaceMembersAsync(chain.Data!.Id, members);

        return (chain.Data, job1.Data.Id, job2.Data.Id, job3.Data.Id);
    }

    [Fact]
    public async Task TriggerAsync_ValidChain_CreatesExecutionsForEntryPoints()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var audit = new AuditService(db);
        var (chain, job1Id, _, _) = await SetupChainWithThreeJobs(db, audit);
        var executionService = new ChainExecutionService(db, audit, new DomainEventService(db));

        // Act
        var result = await executionService.TriggerAsync(chain.Id, "test-user");

        // Assert
        result.Success.ShouldBeTrue();
        result.Data.ShouldNotBeNull();
        result.Data!.State.ShouldBe("running");
        result.Data.JobExecutions.Count.ShouldBe(1);
        result.Data.JobExecutions[0].JobId.ShouldBe(job1Id);
        result.Data.JobExecutions[0].State.ShouldBe("queued");
    }

    [Fact]
    public async Task TriggerAsync_DisabledChain_ReturnsError()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var audit = new AuditService(db);
        var chainService = new ChainService(db, audit);
        var jobService = new JobService(db, audit);

        var job = await jobService.CreateAsync(new CreateJobRequest { Name = "Job" });
        var chain = await chainService.CreateAsync(new CreateChainRequest { Name = "Disabled" });
        await chainService.ReplaceMembersAsync(chain.Data!.Id,
            [new() { JobId = job.Data!.Id, ExecutionOrder = 1 }]);
        await chainService.SetEnabledAsync(chain.Data.Id, false);

        var executionService = new ChainExecutionService(db, audit, new DomainEventService(db));

        // Act
        var result = await executionService.TriggerAsync(chain.Data.Id, "test-user");

        // Assert
        result.Success.ShouldBeFalse();
        result.Error!.Code.ShouldBe(ErrorCodes.ChainNotEnabled);
    }

    [Fact]
    public async Task EvaluateChainProgress_UpstreamSuccess_EnqueuesDownstream()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var audit = new AuditService(db);
        var (chain, job1Id, job2Id, _) = await SetupChainWithThreeJobs(db, audit);
        var executionService = new ChainExecutionService(db, audit, new DomainEventService(db));
        var orchestrator = new ChainOrchestrator(db, NullLogger<ChainOrchestrator>.Instance, new DomainEventService(db));

        var triggered = await executionService.TriggerAsync(chain.Id, "test");
        var firstJobExec = await db.JobExecutions
            .FirstAsync(e => e.ChainExecutionId == triggered.Data!.Id && e.JobId == job1Id);

        // Simulate job completion
        firstJobExec.State = JobExecutionState.Completed;
        firstJobExec.CompletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        // Act
        await orchestrator.EvaluateChainProgressAsync(firstJobExec.Id);

        // Assert
        var downstream = await db.JobExecutions
            .Where(e => e.ChainExecutionId == triggered.Data!.Id && e.JobId == job2Id)
            .FirstOrDefaultAsync();

        downstream.ShouldNotBeNull();
        downstream!.State.ShouldBe(JobExecutionState.Queued);
    }

    [Fact]
    public async Task EvaluateChainProgress_UpstreamFailure_SkipsDownstream()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var audit = new AuditService(db);
        var (chain, job1Id, job2Id, job3Id) = await SetupChainWithThreeJobs(db, audit);
        var executionService = new ChainExecutionService(db, audit, new DomainEventService(db));
        var orchestrator = new ChainOrchestrator(db, NullLogger<ChainOrchestrator>.Instance, new DomainEventService(db));

        var triggered = await executionService.TriggerAsync(chain.Id, "test");
        var firstJobExec = await db.JobExecutions
            .FirstAsync(e => e.ChainExecutionId == triggered.Data!.Id && e.JobId == job1Id);

        // Simulate job failure
        firstJobExec.State = JobExecutionState.Failed;
        firstJobExec.CompletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        // Act
        await orchestrator.EvaluateChainProgressAsync(firstJobExec.Id);

        // Assert — downstream should be cancelled (skipped)
        var downstream = await db.JobExecutions
            .Where(e => e.ChainExecutionId == triggered.Data!.Id && e.JobId == job2Id)
            .FirstOrDefaultAsync();

        downstream.ShouldNotBeNull();
        downstream!.State.ShouldBe(JobExecutionState.Cancelled);

        // Transitive downstream should also be cancelled
        var transitive = await db.JobExecutions
            .Where(e => e.ChainExecutionId == triggered.Data!.Id && e.JobId == job3Id)
            .FirstOrDefaultAsync();

        transitive.ShouldNotBeNull();
        transitive!.State.ShouldBe(JobExecutionState.Cancelled);
    }

    [Fact]
    public async Task EvaluateChainProgress_UpstreamFailure_RunOnUpstreamFailureTrue_ContinuesDownstream()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var audit = new AuditService(db);
        var jobService = new JobService(db, audit);
        var chainService = new ChainService(db, audit);

        var job1 = await jobService.CreateAsync(new CreateJobRequest { Name = "Job 1" });
        var job2 = await jobService.CreateAsync(new CreateJobRequest { Name = "Job 2" });

        var chain = await chainService.CreateAsync(new CreateChainRequest { Name = "Continue on Fail" });
        await chainService.ReplaceMembersAsync(chain.Data!.Id, new List<ChainMemberInput>
        {
            new() { JobId = job1.Data!.Id, ExecutionOrder = 1 },
            new() { JobId = job2.Data!.Id, ExecutionOrder = 2, DependsOnMemberIndex = 0, RunOnUpstreamFailure = true },
        });

        var executionService = new ChainExecutionService(db, audit, new DomainEventService(db));
        var orchestrator = new ChainOrchestrator(db, NullLogger<ChainOrchestrator>.Instance, new DomainEventService(db));

        var triggered = await executionService.TriggerAsync(chain.Data.Id, "test");
        var firstJobExec = await db.JobExecutions
            .FirstAsync(e => e.ChainExecutionId == triggered.Data!.Id && e.JobId == job1.Data.Id);

        // Simulate failure
        firstJobExec.State = JobExecutionState.Failed;
        firstJobExec.CompletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        // Act
        await orchestrator.EvaluateChainProgressAsync(firstJobExec.Id);

        // Assert — downstream should be queued because RunOnUpstreamFailure is true
        var downstream = await db.JobExecutions
            .Where(e => e.ChainExecutionId == triggered.Data!.Id && e.JobId == job2.Data.Id)
            .FirstOrDefaultAsync();

        downstream.ShouldNotBeNull();
        downstream!.State.ShouldBe(JobExecutionState.Queued);
    }

    [Fact]
    public async Task EvaluateChainProgress_AllComplete_MarksChainCompleted()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var audit = new AuditService(db);
        var jobService = new JobService(db, audit);
        var chainService = new ChainService(db, audit);

        var job1 = await jobService.CreateAsync(new CreateJobRequest { Name = "Solo Job" });
        var chain = await chainService.CreateAsync(new CreateChainRequest { Name = "Single" });
        await chainService.ReplaceMembersAsync(chain.Data!.Id,
            [new() { JobId = job1.Data!.Id, ExecutionOrder = 1 }]);

        var executionService = new ChainExecutionService(db, audit, new DomainEventService(db));
        var orchestrator = new ChainOrchestrator(db, NullLogger<ChainOrchestrator>.Instance, new DomainEventService(db));

        var triggered = await executionService.TriggerAsync(chain.Data.Id, "test");
        var jobExec = await db.JobExecutions
            .FirstAsync(e => e.ChainExecutionId == triggered.Data!.Id);

        // Simulate completion
        jobExec.State = JobExecutionState.Completed;
        jobExec.CompletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        // Act
        await orchestrator.EvaluateChainProgressAsync(jobExec.Id);

        // Assert
        var chainExec = await db.ChainExecutions.FindAsync(triggered.Data!.Id);
        chainExec.ShouldNotBeNull();
        chainExec!.State.ShouldBe(ChainExecutionState.Completed);
        chainExec.CompletedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task EvaluateChainProgress_AnyMemberFailed_MarksChainFailed()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var audit = new AuditService(db);
        var jobService = new JobService(db, audit);
        var chainService = new ChainService(db, audit);

        var job1 = await jobService.CreateAsync(new CreateJobRequest { Name = "Solo Fail" });
        var chain = await chainService.CreateAsync(new CreateChainRequest { Name = "FailChain" });
        await chainService.ReplaceMembersAsync(chain.Data!.Id,
            [new() { JobId = job1.Data!.Id, ExecutionOrder = 1 }]);

        var executionService = new ChainExecutionService(db, audit, new DomainEventService(db));
        var orchestrator = new ChainOrchestrator(db, NullLogger<ChainOrchestrator>.Instance, new DomainEventService(db));

        var triggered = await executionService.TriggerAsync(chain.Data.Id, "test");
        var jobExec = await db.JobExecutions
            .FirstAsync(e => e.ChainExecutionId == triggered.Data!.Id);

        // Simulate failure
        jobExec.State = JobExecutionState.Failed;
        jobExec.CompletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        // Act
        await orchestrator.EvaluateChainProgressAsync(jobExec.Id);

        // Assert
        var chainExec = await db.ChainExecutions.FindAsync(triggered.Data!.Id);
        chainExec.ShouldNotBeNull();
        chainExec!.State.ShouldBe(ChainExecutionState.Failed);
        chainExec.CompletedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task EvaluateChainProgress_AlreadyQueuedMember_SkipsDuplicate()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var audit = new AuditService(db);
        var (chain, job1Id, job2Id, _) = await SetupChainWithThreeJobs(db, audit);
        var executionService = new ChainExecutionService(db, audit, new DomainEventService(db));
        var orchestrator = new ChainOrchestrator(db, NullLogger<ChainOrchestrator>.Instance, new DomainEventService(db));

        var triggered = await executionService.TriggerAsync(chain.Id, "test");
        var chainExecutionId = triggered.Data!.Id;

        // Pre-create a Job2 execution (as if it was already queued)
        var existingJob2Exec = new JobExecution
        {
            Id = Guid.NewGuid(),
            JobId = job2Id,
            JobVersionNumber = 1,
            TriggeredBy = "pre-existing",
            State = JobExecutionState.Queued,
            QueuedAt = DateTime.UtcNow,
            ChainExecutionId = chainExecutionId,
            CreatedAt = DateTime.UtcNow,
        };
        db.JobExecutions.Add(existingJob2Exec);

        var firstJobExec = await db.JobExecutions
            .FirstAsync(e => e.ChainExecutionId == chainExecutionId && e.JobId == job1Id);
        firstJobExec.State = JobExecutionState.Completed;
        firstJobExec.CompletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        // Act
        await orchestrator.EvaluateChainProgressAsync(firstJobExec.Id);

        // Assert — should NOT have created a duplicate Job2 execution
        var job2Executions = await db.JobExecutions
            .Where(e => e.ChainExecutionId == chainExecutionId && e.JobId == job2Id)
            .ToListAsync();
        job2Executions.Count.ShouldBe(1);
        job2Executions[0].Id.ShouldBe(existingJob2Exec.Id);
    }
}
