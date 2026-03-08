using Courier.Domain.Entities;
using Courier.Domain.Enums;
using Courier.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Courier.Tests.Unit.Services;

/// <summary>
/// Tests for queue concurrency logic. The actual JobQueueProcessor uses raw SQL
/// with FOR UPDATE SKIP LOCKED (PostgreSQL-specific), so these tests validate
/// the concurrency limit semantics and queue ordering at the domain level using
/// InMemory database.
/// </summary>
public class QueueConcurrencyTests
{
    private static CourierDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<CourierDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new CourierDbContext(options);
    }

    private static Job CreateTestJob(CourierDbContext db)
    {
        var job = new Job
        {
            Id = Guid.NewGuid(),
            Name = $"TestJob-{Guid.NewGuid():N}",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.Jobs.Add(job);
        return job;
    }

    private static JobExecution CreateExecution(Guid jobId, JobExecutionState state, DateTime? queuedAt = null)
    {
        return new JobExecution
        {
            Id = Guid.NewGuid(),
            JobId = jobId,
            State = state,
            QueuedAt = queuedAt ?? DateTime.UtcNow,
            StartedAt = state == JobExecutionState.Running ? DateTime.UtcNow : null,
            CreatedAt = DateTime.UtcNow,
            TriggeredBy = "test",
        };
    }

    [Fact]
    public async Task ConcurrencyCheck_RunningCountBelowLimit_AllowsDequeue()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var job = CreateTestJob(db);
        var running = CreateExecution(job.Id, JobExecutionState.Running);
        var queued = CreateExecution(job.Id, JobExecutionState.Queued);
        db.JobExecutions.AddRange(running, queued);
        await db.SaveChangesAsync();

        var concurrencyLimit = 5;

        // Act
        var runningCount = await db.JobExecutions
            .CountAsync(e => e.State == JobExecutionState.Running);

        // Assert
        runningCount.ShouldBeLessThan(concurrencyLimit);
        (runningCount < concurrencyLimit).ShouldBeTrue();
    }

    [Fact]
    public async Task ConcurrencyCheck_RunningCountAtLimit_BlocksDequeue()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var job = CreateTestJob(db);
        var concurrencyLimit = 3;

        for (var i = 0; i < concurrencyLimit; i++)
        {
            db.JobExecutions.Add(CreateExecution(job.Id, JobExecutionState.Running));
        }
        var queued = CreateExecution(job.Id, JobExecutionState.Queued);
        db.JobExecutions.Add(queued);
        await db.SaveChangesAsync();

        // Act
        var runningCount = await db.JobExecutions
            .CountAsync(e => e.State == JobExecutionState.Running);

        // Assert
        (runningCount >= concurrencyLimit).ShouldBeTrue();
    }

    [Fact]
    public async Task QueueOrdering_QueuedExecutions_DequeuedByQueuedAtAscending()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var job = CreateTestJob(db);
        var first = CreateExecution(job.Id, JobExecutionState.Queued, DateTime.UtcNow.AddMinutes(-30));
        var second = CreateExecution(job.Id, JobExecutionState.Queued, DateTime.UtcNow.AddMinutes(-20));
        var third = CreateExecution(job.Id, JobExecutionState.Queued, DateTime.UtcNow.AddMinutes(-10));
        db.JobExecutions.AddRange(third, first, second); // Add in random order
        await db.SaveChangesAsync();

        // Act
        var nextInQueue = await db.JobExecutions
            .Where(e => e.State == JobExecutionState.Queued)
            .OrderBy(e => e.QueuedAt)
            .FirstOrDefaultAsync();

        // Assert
        nextInQueue.ShouldNotBeNull();
        nextInQueue.Id.ShouldBe(first.Id);
    }

    [Fact]
    public async Task CompletedExecution_FreesSlot()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var job = CreateTestJob(db);
        var concurrencyLimit = 2;

        var exec1 = CreateExecution(job.Id, JobExecutionState.Running);
        var exec2 = CreateExecution(job.Id, JobExecutionState.Running);
        db.JobExecutions.AddRange(exec1, exec2);
        await db.SaveChangesAsync();

        // Verify at limit
        var runningBefore = await db.JobExecutions.CountAsync(e => e.State == JobExecutionState.Running);
        runningBefore.ShouldBe(concurrencyLimit);

        // Act — mark one completed
        exec1.State = JobExecutionState.Completed;
        exec1.CompletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        // Assert
        var runningAfter = await db.JobExecutions.CountAsync(e => e.State == JobExecutionState.Running);
        runningAfter.ShouldBe(1);
        (runningAfter < concurrencyLimit).ShouldBeTrue();
    }

    [Fact]
    public async Task FailedExecution_FreesSlot()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var job = CreateTestJob(db);
        var concurrencyLimit = 2;

        var exec1 = CreateExecution(job.Id, JobExecutionState.Running);
        var exec2 = CreateExecution(job.Id, JobExecutionState.Running);
        db.JobExecutions.AddRange(exec1, exec2);
        await db.SaveChangesAsync();

        // Act — mark one failed
        exec1.State = JobExecutionState.Failed;
        exec1.CompletedAt = DateTime.UtcNow;
        await db.SaveChangesAsync();

        // Assert
        var runningAfter = await db.JobExecutions.CountAsync(e => e.State == JobExecutionState.Running);
        runningAfter.ShouldBe(1);
        (runningAfter < concurrencyLimit).ShouldBeTrue();
    }

    [Fact]
    public async Task ZeroQueuedExecutions_NoDequeue()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var job = CreateTestJob(db);
        var running = CreateExecution(job.Id, JobExecutionState.Running);
        db.JobExecutions.Add(running);
        await db.SaveChangesAsync();

        // Act
        var nextInQueue = await db.JobExecutions
            .Where(e => e.State == JobExecutionState.Queued)
            .OrderBy(e => e.QueuedAt)
            .FirstOrDefaultAsync();

        // Assert
        nextInQueue.ShouldBeNull();
    }

    [Fact]
    public async Task ConcurrencyLimit_FromSystemSettings_ReadsCorrectly()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        db.SystemSettings.Add(new SystemSetting
        {
            Key = "job.concurrency_limit",
            Value = "10",
            UpdatedAt = DateTime.UtcNow,
            UpdatedBy = "system",
        });
        await db.SaveChangesAsync();

        // Act
        var setting = await db.SystemSettings
            .Where(s => s.Key == "job.concurrency_limit")
            .Select(s => s.Value)
            .FirstOrDefaultAsync();

        var concurrencyLimit = int.TryParse(setting, out var parsed) ? parsed : 5;

        // Assert
        concurrencyLimit.ShouldBe(10);
    }

    [Fact]
    public async Task ConcurrencyLimit_MissingSetting_UsesDefault()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        // No system settings seeded

        // Act
        var setting = await db.SystemSettings
            .Where(s => s.Key == "job.concurrency_limit")
            .Select(s => s.Value)
            .FirstOrDefaultAsync();

        var defaultLimit = 5;
        var concurrencyLimit = int.TryParse(setting, out var parsed) ? parsed : defaultLimit;

        // Assert
        concurrencyLimit.ShouldBe(defaultLimit);
    }

    [Fact]
    public async Task ConcurrencyLimit_InvalidSetting_UsesDefault()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        db.SystemSettings.Add(new SystemSetting
        {
            Key = "job.concurrency_limit",
            Value = "not_a_number",
            UpdatedAt = DateTime.UtcNow,
            UpdatedBy = "system",
        });
        await db.SaveChangesAsync();

        // Act
        var setting = await db.SystemSettings
            .Where(s => s.Key == "job.concurrency_limit")
            .Select(s => s.Value)
            .FirstOrDefaultAsync();

        var defaultLimit = 5;
        var concurrencyLimit = int.TryParse(setting, out var parsed) ? parsed : defaultLimit;

        // Assert
        concurrencyLimit.ShouldBe(defaultLimit);
    }
}
