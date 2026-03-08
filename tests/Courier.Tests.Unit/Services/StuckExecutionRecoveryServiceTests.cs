using Courier.Domain.Entities;
using Courier.Domain.Enums;
using Courier.Features.AuditLog;
using Courier.Infrastructure.Data;
using Courier.Worker.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Shouldly;

namespace Courier.Tests.Unit.Services;

public class StuckExecutionRecoveryServiceTests
{
    private static CourierDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<CourierDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;
        return new CourierDbContext(options);
    }

    private static (IServiceScopeFactory scopeFactory, CourierDbContext db) CreateScopeFactory()
    {
        var db = CreateInMemoryContext();
        var audit = new AuditService(db);

        var serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(CourierDbContext)).Returns(db);
        serviceProvider.GetService(typeof(AuditService)).Returns(audit);

        var scope = Substitute.For<IServiceScope>();
        scope.ServiceProvider.Returns(serviceProvider);

        var scopeFactory = Substitute.For<IServiceScopeFactory>();
        scopeFactory.CreateScope().Returns(scope);

        return (scopeFactory, db);
    }

    private static StuckExecutionRecoveryService CreateService(IServiceScopeFactory scopeFactory)
    {
        return new StuckExecutionRecoveryService(scopeFactory, NullLogger<StuckExecutionRecoveryService>.Instance);
    }

    private static Job CreateTestJob()
    {
        return new Job
        {
            Id = Guid.NewGuid(),
            Name = $"TestJob-{Guid.NewGuid():N}",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
    }

    private static JobExecution CreateExecution(Guid jobId, JobExecutionState state, DateTime? startedAt = null)
    {
        return new JobExecution
        {
            Id = Guid.NewGuid(),
            JobId = jobId,
            State = state,
            StartedAt = startedAt,
            QueuedAt = DateTime.UtcNow.AddHours(-5),
            CreatedAt = DateTime.UtcNow.AddHours(-5),
            TriggeredBy = "test",
        };
    }

    [Fact]
    public async Task RecoverStuckExecutions_RunningOlderThanThreshold_MarkedFailed()
    {
        // Arrange
        var (scopeFactory, db) = CreateScopeFactory();
        var job = CreateTestJob();
        db.Jobs.Add(job);
        var execution = CreateExecution(job.Id, JobExecutionState.Running, DateTime.UtcNow.AddHours(-3));
        db.JobExecutions.Add(execution);
        await db.SaveChangesAsync();

        var service = CreateService(scopeFactory);
        using var cts = new CancellationTokenSource();

        // Act — service runs immediately on startup
        cts.CancelAfter(TimeSpan.FromSeconds(5));
        try { await service.StartAsync(cts.Token); await Task.Delay(500, cts.Token); }
        catch (OperationCanceledException) { }
        finally { await service.StopAsync(CancellationToken.None); }

        // Assert
        var updated = await db.JobExecutions.FirstAsync(e => e.Id == execution.Id);
        updated.State.ShouldBe(JobExecutionState.Failed);
    }

    [Fact]
    public async Task RecoverStuckExecutions_RunningWithinThreshold_NotTouched()
    {
        // Arrange
        var (scopeFactory, db) = CreateScopeFactory();
        var job = CreateTestJob();
        db.Jobs.Add(job);
        var execution = CreateExecution(job.Id, JobExecutionState.Running, DateTime.UtcNow.AddMinutes(-30));
        db.JobExecutions.Add(execution);
        await db.SaveChangesAsync();

        var service = CreateService(scopeFactory);
        using var cts = new CancellationTokenSource();

        // Act
        cts.CancelAfter(TimeSpan.FromSeconds(5));
        try { await service.StartAsync(cts.Token); await Task.Delay(500, cts.Token); }
        catch (OperationCanceledException) { }
        finally { await service.StopAsync(CancellationToken.None); }

        // Assert
        var updated = await db.JobExecutions.FirstAsync(e => e.Id == execution.Id);
        updated.State.ShouldBe(JobExecutionState.Running);
    }

    [Fact]
    public async Task RecoverStuckExecutions_FailedExecution_NotReprocessed()
    {
        // Arrange
        var (scopeFactory, db) = CreateScopeFactory();
        var job = CreateTestJob();
        db.Jobs.Add(job);
        var execution = CreateExecution(job.Id, JobExecutionState.Failed, DateTime.UtcNow.AddHours(-5));
        execution.CompletedAt = DateTime.UtcNow.AddHours(-4);
        db.JobExecutions.Add(execution);
        await db.SaveChangesAsync();

        var service = CreateService(scopeFactory);
        using var cts = new CancellationTokenSource();

        // Act
        cts.CancelAfter(TimeSpan.FromSeconds(5));
        try { await service.StartAsync(cts.Token); await Task.Delay(500, cts.Token); }
        catch (OperationCanceledException) { }
        finally { await service.StopAsync(CancellationToken.None); }

        // Assert
        var updated = await db.JobExecutions.FirstAsync(e => e.Id == execution.Id);
        updated.State.ShouldBe(JobExecutionState.Failed);
    }

    [Fact]
    public async Task RecoverStuckExecutions_CompletedExecution_NotAffected()
    {
        // Arrange
        var (scopeFactory, db) = CreateScopeFactory();
        var job = CreateTestJob();
        db.Jobs.Add(job);
        var execution = CreateExecution(job.Id, JobExecutionState.Completed, DateTime.UtcNow.AddHours(-5));
        execution.CompletedAt = DateTime.UtcNow.AddHours(-4);
        db.JobExecutions.Add(execution);
        await db.SaveChangesAsync();

        var service = CreateService(scopeFactory);
        using var cts = new CancellationTokenSource();

        // Act
        cts.CancelAfter(TimeSpan.FromSeconds(5));
        try { await service.StartAsync(cts.Token); await Task.Delay(500, cts.Token); }
        catch (OperationCanceledException) { }
        finally { await service.StopAsync(CancellationToken.None); }

        // Assert
        var updated = await db.JobExecutions.FirstAsync(e => e.Id == execution.Id);
        updated.State.ShouldBe(JobExecutionState.Completed);
    }

    [Fact]
    public async Task RecoverStuckExecutions_MultipleStuckExecutions_AllRecovered()
    {
        // Arrange
        var (scopeFactory, db) = CreateScopeFactory();
        var job = CreateTestJob();
        db.Jobs.Add(job);
        var exec1 = CreateExecution(job.Id, JobExecutionState.Running, DateTime.UtcNow.AddHours(-3));
        var exec2 = CreateExecution(job.Id, JobExecutionState.Running, DateTime.UtcNow.AddHours(-4));
        var exec3 = CreateExecution(job.Id, JobExecutionState.Running, DateTime.UtcNow.AddHours(-10));
        db.JobExecutions.AddRange(exec1, exec2, exec3);
        await db.SaveChangesAsync();

        var service = CreateService(scopeFactory);
        using var cts = new CancellationTokenSource();

        // Act
        cts.CancelAfter(TimeSpan.FromSeconds(5));
        try { await service.StartAsync(cts.Token); await Task.Delay(500, cts.Token); }
        catch (OperationCanceledException) { }
        finally { await service.StopAsync(CancellationToken.None); }

        // Assert
        var executions = await db.JobExecutions.ToListAsync();
        executions.ShouldAllBe(e => e.State == JobExecutionState.Failed);
    }

    [Fact]
    public async Task RecoverStuckExecutions_QueuedExecution_NotTreatedAsStuck()
    {
        // Arrange
        var (scopeFactory, db) = CreateScopeFactory();
        var job = CreateTestJob();
        db.Jobs.Add(job);
        var execution = CreateExecution(job.Id, JobExecutionState.Queued, null);
        db.JobExecutions.Add(execution);
        await db.SaveChangesAsync();

        var service = CreateService(scopeFactory);
        using var cts = new CancellationTokenSource();

        // Act
        cts.CancelAfter(TimeSpan.FromSeconds(5));
        try { await service.StartAsync(cts.Token); await Task.Delay(500, cts.Token); }
        catch (OperationCanceledException) { }
        finally { await service.StopAsync(CancellationToken.None); }

        // Assert
        var updated = await db.JobExecutions.FirstAsync(e => e.Id == execution.Id);
        updated.State.ShouldBe(JobExecutionState.Queued);
    }

    [Fact]
    public async Task RecoverStuckExecutions_RunningStepExecutions_AlsoFailed()
    {
        // Arrange
        var (scopeFactory, db) = CreateScopeFactory();
        var job = CreateTestJob();
        db.Jobs.Add(job);

        var jobStep = new JobStep
        {
            Id = Guid.NewGuid(),
            JobId = job.Id,
            TypeKey = "file.copy",
            Configuration = "{}",
            StepOrder = 1,
        };
        db.JobSteps.Add(jobStep);

        var execution = CreateExecution(job.Id, JobExecutionState.Running, DateTime.UtcNow.AddHours(-3));
        db.JobExecutions.Add(execution);

        var stepExec = new StepExecution
        {
            Id = Guid.NewGuid(),
            JobExecutionId = execution.Id,
            JobStepId = jobStep.Id,
            StepOrder = 1,
            State = StepExecutionState.Running,
            StartedAt = DateTime.UtcNow.AddHours(-3),
            CreatedAt = DateTime.UtcNow.AddHours(-3),
        };
        db.StepExecutions.Add(stepExec);
        await db.SaveChangesAsync();

        var service = CreateService(scopeFactory);
        using var cts = new CancellationTokenSource();

        // Act
        cts.CancelAfter(TimeSpan.FromSeconds(5));
        try { await service.StartAsync(cts.Token); await Task.Delay(500, cts.Token); }
        catch (OperationCanceledException) { }
        finally { await service.StopAsync(CancellationToken.None); }

        // Assert
        var updatedStep = await db.StepExecutions.FirstAsync(se => se.Id == stepExec.Id);
        updatedStep.State.ShouldBe(StepExecutionState.Failed);
        updatedStep.ErrorMessage.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task RecoverStuckExecutions_CompletedAtSetOnRecovery()
    {
        // Arrange
        var (scopeFactory, db) = CreateScopeFactory();
        var job = CreateTestJob();
        db.Jobs.Add(job);
        var execution = CreateExecution(job.Id, JobExecutionState.Running, DateTime.UtcNow.AddHours(-3));
        execution.CompletedAt = null;
        db.JobExecutions.Add(execution);
        await db.SaveChangesAsync();

        var service = CreateService(scopeFactory);
        using var cts = new CancellationTokenSource();

        // Act
        cts.CancelAfter(TimeSpan.FromSeconds(5));
        try { await service.StartAsync(cts.Token); await Task.Delay(500, cts.Token); }
        catch (OperationCanceledException) { }
        finally { await service.StopAsync(CancellationToken.None); }

        // Assert
        var updated = await db.JobExecutions.FirstAsync(e => e.Id == execution.Id);
        updated.CompletedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task RecoverStuckExecutions_StepErrorMessageSetOnRecovery()
    {
        // Arrange
        var (scopeFactory, db) = CreateScopeFactory();
        var job = CreateTestJob();
        db.Jobs.Add(job);

        var jobStep = new JobStep
        {
            Id = Guid.NewGuid(),
            JobId = job.Id,
            TypeKey = "sftp.upload",
            Configuration = "{}",
            StepOrder = 1,
        };
        db.JobSteps.Add(jobStep);

        var execution = CreateExecution(job.Id, JobExecutionState.Running, DateTime.UtcNow.AddHours(-5));
        db.JobExecutions.Add(execution);

        var stepExec = new StepExecution
        {
            Id = Guid.NewGuid(),
            JobExecutionId = execution.Id,
            JobStepId = jobStep.Id,
            StepOrder = 1,
            State = StepExecutionState.Running,
            StartedAt = DateTime.UtcNow.AddHours(-5),
            CreatedAt = DateTime.UtcNow.AddHours(-5),
        };
        db.StepExecutions.Add(stepExec);
        await db.SaveChangesAsync();

        var service = CreateService(scopeFactory);
        using var cts = new CancellationTokenSource();

        // Act
        cts.CancelAfter(TimeSpan.FromSeconds(5));
        try { await service.StartAsync(cts.Token); await Task.Delay(500, cts.Token); }
        catch (OperationCanceledException) { }
        finally { await service.StopAsync(CancellationToken.None); }

        // Assert
        var updatedStep = await db.StepExecutions.FirstAsync(se => se.Id == stepExec.Id);
        updatedStep.State.ShouldBe(StepExecutionState.Failed);
        updatedStep.CompletedAt.ShouldNotBeNull();
        updatedStep.ErrorMessage.ShouldBe("Worker crashed during execution");
    }
}
