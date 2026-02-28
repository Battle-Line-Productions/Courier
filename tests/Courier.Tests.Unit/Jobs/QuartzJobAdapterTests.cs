using Courier.Domain.Entities;
using Courier.Domain.Enums;
using Courier.Features.Jobs;
using Courier.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NSubstitute;
using Quartz;
using Shouldly;

namespace Courier.Tests.Unit.Jobs;

public class QuartzJobAdapterTests
{
    private static CourierDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<CourierDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new CourierDbContext(options);
    }

    private static (QuartzJobAdapter adapter, CourierDbContext db) CreateAdapter()
    {
        var db = CreateInMemoryContext();
        var serviceProvider = Substitute.For<IServiceProvider>();
        serviceProvider.GetService(typeof(CourierDbContext)).Returns(db);

        var scope = Substitute.For<IServiceScope>();
        scope.ServiceProvider.Returns(serviceProvider);

        var scopeFactory = Substitute.For<IServiceScopeFactory>();
        scopeFactory.CreateScope().Returns(scope);

        var logger = Substitute.For<ILogger<QuartzJobAdapter>>();
        var adapter = new QuartzJobAdapter(scopeFactory, logger);

        return (adapter, db);
    }

    private static IJobExecutionContext CreateMockContext(Guid scheduleId, Guid jobId)
    {
        var context = Substitute.For<IJobExecutionContext>();
        var dataMap = new JobDataMap { { "jobId", jobId.ToString() } };
        context.MergedJobDataMap.Returns(dataMap);

        var jobKey = new JobKey(scheduleId.ToString(), "courier");
        var jobDetail = Substitute.For<IJobDetail>();
        jobDetail.Key.Returns(jobKey);
        context.JobDetail.Returns(jobDetail);
        context.NextFireTimeUtc.Returns(DateTimeOffset.UtcNow.AddHours(24));

        return context;
    }

    [Fact]
    public async Task Execute_ValidJob_CreatesQueuedExecution()
    {
        // Arrange
        var (adapter, db) = CreateAdapter();
        var job = new Job
        {
            Id = Guid.CreateVersion7(),
            Name = "Scheduled Job",
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.Jobs.Add(job);

        var step = new JobStep
        {
            Id = Guid.CreateVersion7(),
            JobId = job.Id,
            StepOrder = 1,
            Name = "Copy files",
            TypeKey = "file.copy",
            Configuration = "{}",
        };
        db.JobSteps.Add(step);

        var scheduleId = Guid.CreateVersion7();
        var schedule = new JobSchedule
        {
            Id = scheduleId,
            JobId = job.Id,
            ScheduleType = "cron",
            CronExpression = "0 0 3 * * ?",
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.JobSchedules.Add(schedule);
        await db.SaveChangesAsync();

        var context = CreateMockContext(scheduleId, job.Id);

        // Act
        await adapter.Execute(context);

        // Assert
        var execution = await db.JobExecutions.FirstOrDefaultAsync();
        execution.ShouldNotBeNull();
        execution.JobId.ShouldBe(job.Id);
        execution.State.ShouldBe(JobExecutionState.Queued);
        execution.TriggeredBy.ShouldBe("schedule");
    }

    [Fact]
    public async Task Execute_InvalidJobId_Skips()
    {
        // Arrange
        var (adapter, db) = CreateAdapter();

        var context = Substitute.For<IJobExecutionContext>();
        var dataMap = new JobDataMap { { "jobId", "not-a-guid" } };
        context.MergedJobDataMap.Returns(dataMap);

        // Act
        await adapter.Execute(context);

        // Assert
        (await db.JobExecutions.AnyAsync()).ShouldBeFalse();
    }

    [Fact]
    public async Task Execute_MissingJob_Skips()
    {
        // Arrange
        var (adapter, db) = CreateAdapter();
        var scheduleId = Guid.CreateVersion7();
        var context = CreateMockContext(scheduleId, Guid.NewGuid());

        // Act
        await adapter.Execute(context);

        // Assert
        (await db.JobExecutions.AnyAsync()).ShouldBeFalse();
    }

    [Fact]
    public async Task Execute_DisabledJob_Skips()
    {
        // Arrange
        var (adapter, db) = CreateAdapter();
        var job = new Job
        {
            Id = Guid.CreateVersion7(),
            Name = "Disabled Job",
            IsEnabled = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.Jobs.Add(job);
        await db.SaveChangesAsync();

        var scheduleId = Guid.CreateVersion7();
        var context = CreateMockContext(scheduleId, job.Id);

        // Act
        await adapter.Execute(context);

        // Assert
        (await db.JobExecutions.AnyAsync()).ShouldBeFalse();
    }

    [Fact]
    public async Task Execute_JobWithNoSteps_Skips()
    {
        // Arrange
        var (adapter, db) = CreateAdapter();
        var job = new Job
        {
            Id = Guid.CreateVersion7(),
            Name = "Empty Job",
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.Jobs.Add(job);
        await db.SaveChangesAsync();

        var scheduleId = Guid.CreateVersion7();
        var context = CreateMockContext(scheduleId, job.Id);

        // Act
        await adapter.Execute(context);

        // Assert
        (await db.JobExecutions.AnyAsync()).ShouldBeFalse();
    }

    [Fact]
    public async Task Execute_UpdatesScheduleLastFiredAt()
    {
        // Arrange
        var (adapter, db) = CreateAdapter();
        var job = new Job
        {
            Id = Guid.CreateVersion7(),
            Name = "Scheduled Job",
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.Jobs.Add(job);

        var step = new JobStep
        {
            Id = Guid.CreateVersion7(),
            JobId = job.Id,
            StepOrder = 1,
            Name = "Copy files",
            TypeKey = "file.copy",
            Configuration = "{}",
        };
        db.JobSteps.Add(step);

        var scheduleId = Guid.CreateVersion7();
        var schedule = new JobSchedule
        {
            Id = scheduleId,
            JobId = job.Id,
            ScheduleType = "cron",
            CronExpression = "0 0 3 * * ?",
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.JobSchedules.Add(schedule);
        await db.SaveChangesAsync();

        var context = CreateMockContext(scheduleId, job.Id);

        // Act
        await adapter.Execute(context);

        // Assert
        var updated = await db.JobSchedules.FirstAsync(s => s.Id == scheduleId);
        updated.LastFiredAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task Execute_OneShotSchedule_DisablesAfterFiring()
    {
        // Arrange
        var (adapter, db) = CreateAdapter();
        var job = new Job
        {
            Id = Guid.CreateVersion7(),
            Name = "One-Shot Job",
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.Jobs.Add(job);

        var step = new JobStep
        {
            Id = Guid.CreateVersion7(),
            JobId = job.Id,
            StepOrder = 1,
            Name = "Copy files",
            TypeKey = "file.copy",
            Configuration = "{}",
        };
        db.JobSteps.Add(step);

        var scheduleId = Guid.CreateVersion7();
        var schedule = new JobSchedule
        {
            Id = scheduleId,
            JobId = job.Id,
            ScheduleType = "one_shot",
            RunAt = DateTimeOffset.UtcNow.AddMinutes(5),
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.JobSchedules.Add(schedule);
        await db.SaveChangesAsync();

        var context = CreateMockContext(scheduleId, job.Id);

        // Act
        await adapter.Execute(context);

        // Assert
        var updated = await db.JobSchedules.FirstAsync(s => s.Id == scheduleId);
        updated.IsEnabled.ShouldBeFalse();
    }
}
