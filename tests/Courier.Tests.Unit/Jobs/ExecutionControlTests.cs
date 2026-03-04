using Courier.Domain.Common;
using Courier.Domain.Entities;
using Courier.Domain.Enums;
using Courier.Features.AuditLog;
using Courier.Features.Jobs;
using Courier.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Courier.Tests.Unit.Jobs;

public class ExecutionControlTests
{
    private static CourierDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<CourierDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new CourierDbContext(options);
    }

    private static async Task<(CourierDbContext db, ExecutionService service, Guid executionId)> SetupExecutionAsync(JobExecutionState state)
    {
        var db = CreateInMemoryContext();
        var service = new ExecutionService(db, new AuditService(db));

        var job = new Job
        {
            Id = Guid.NewGuid(),
            Name = "Test Job",
            CreatedAt = DateTime.UtcNow,
        };
        db.Jobs.Add(job);

        var execution = new JobExecution
        {
            Id = Guid.NewGuid(),
            JobId = job.Id,
            TriggeredBy = "test",
            State = state,
            QueuedAt = DateTime.UtcNow,
            StartedAt = state == JobExecutionState.Running ? DateTime.UtcNow : null,
            CreatedAt = DateTime.UtcNow,
        };
        db.JobExecutions.Add(execution);
        await db.SaveChangesAsync();

        return (db, service, execution.Id);
    }

    [Fact]
    public async Task PauseExecution_RunningExecution_SetsRequestedState()
    {
        // Arrange
        var (db, service, executionId) = await SetupExecutionAsync(JobExecutionState.Running);

        // Act
        var result = await service.PauseExecutionAsync(executionId, "admin", CancellationToken.None);

        // Assert
        result.Success.ShouldBeTrue();
        result.Data!.State.ShouldBe("running"); // Still running — engine will acknowledge

        var execution = await db.JobExecutions.FindAsync(executionId);
        execution!.RequestedState.ShouldBe("paused");
        execution.PausedBy.ShouldBe("admin");
    }

    [Fact]
    public async Task PauseExecution_NotRunning_ReturnsError()
    {
        // Arrange
        var (_, service, executionId) = await SetupExecutionAsync(JobExecutionState.Queued);

        // Act
        var result = await service.PauseExecutionAsync(executionId, "admin", CancellationToken.None);

        // Assert
        result.Success.ShouldBeFalse();
        result.Error!.Code.ShouldBe(ErrorCodes.ExecutionCannotBePaused);
    }

    [Fact]
    public async Task ResumeExecution_PausedExecution_TransitionsToQueued()
    {
        // Arrange
        var (db, service, executionId) = await SetupExecutionAsync(JobExecutionState.Paused);

        // Act
        var result = await service.ResumeExecutionAsync(executionId, "admin", CancellationToken.None);

        // Assert
        result.Success.ShouldBeTrue();
        result.Data!.State.ShouldBe("queued");

        var execution = await db.JobExecutions.FindAsync(executionId);
        execution!.State.ShouldBe(JobExecutionState.Queued);
        execution.RequestedState.ShouldBeNull();
        execution.PausedAt.ShouldBeNull();
        execution.PausedBy.ShouldBeNull();
    }

    [Fact]
    public async Task ResumeExecution_NotPaused_ReturnsError()
    {
        // Arrange
        var (_, service, executionId) = await SetupExecutionAsync(JobExecutionState.Running);

        // Act
        var result = await service.ResumeExecutionAsync(executionId, "admin", CancellationToken.None);

        // Assert
        result.Success.ShouldBeFalse();
        result.Error!.Code.ShouldBe(ErrorCodes.ExecutionCannotBeResumed);
    }

    [Fact]
    public async Task CancelExecution_RunningExecution_SetsRequestedState()
    {
        // Arrange
        var (db, service, executionId) = await SetupExecutionAsync(JobExecutionState.Running);

        // Act
        var result = await service.CancelExecutionAsync(executionId, "admin", "No longer needed", CancellationToken.None);

        // Assert
        result.Success.ShouldBeTrue();
        result.Data!.State.ShouldBe("running"); // Still running — engine will acknowledge

        var execution = await db.JobExecutions.FindAsync(executionId);
        execution!.RequestedState.ShouldBe("cancelled");
        execution.CancelledBy.ShouldBe("admin");
        execution.CancelReason.ShouldBe("No longer needed");
    }

    [Fact]
    public async Task CancelExecution_QueuedExecution_TransitionsDirectlyToCancelled()
    {
        // Arrange
        var (db, service, executionId) = await SetupExecutionAsync(JobExecutionState.Queued);

        // Act
        var result = await service.CancelExecutionAsync(executionId, "admin", "Wrong job", CancellationToken.None);

        // Assert
        result.Success.ShouldBeTrue();
        result.Data!.State.ShouldBe("cancelled");

        var execution = await db.JobExecutions.FindAsync(executionId);
        execution!.State.ShouldBe(JobExecutionState.Cancelled);
        execution.CancelledAt.ShouldNotBeNull();
        execution.CancelledBy.ShouldBe("admin");
        execution.CancelReason.ShouldBe("Wrong job");
    }

    [Fact]
    public async Task CancelExecution_PausedExecution_TransitionsDirectlyToCancelled()
    {
        // Arrange
        var (db, service, executionId) = await SetupExecutionAsync(JobExecutionState.Paused);

        // Act
        var result = await service.CancelExecutionAsync(executionId, "admin", null, CancellationToken.None);

        // Assert
        result.Success.ShouldBeTrue();
        result.Data!.State.ShouldBe("cancelled");

        var execution = await db.JobExecutions.FindAsync(executionId);
        execution!.State.ShouldBe(JobExecutionState.Cancelled);
        execution.CancelledAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task CancelExecution_CompletedExecution_ReturnsError()
    {
        // Arrange
        var (_, service, executionId) = await SetupExecutionAsync(JobExecutionState.Completed);

        // Act
        var result = await service.CancelExecutionAsync(executionId, "admin", null, CancellationToken.None);

        // Assert
        result.Success.ShouldBeFalse();
        result.Error!.Code.ShouldBe(ErrorCodes.ExecutionCannotBeCancelled);
    }
}
