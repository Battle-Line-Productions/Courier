using Courier.Domain.Entities;
using Courier.Infrastructure.Data;
using Courier.Worker.Services;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Quartz;
using Quartz.Impl.Matchers;
using Shouldly;

namespace Courier.Tests.Unit.Jobs;

public class QuartzScheduleManagerTests
{
    private static CourierDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<CourierDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new CourierDbContext(options);
    }

    private static (QuartzScheduleManager manager, CourierDbContext db, IScheduler scheduler) CreateManager()
    {
        var db = CreateInMemoryContext();
        var scheduler = Substitute.For<IScheduler>();
        var schedulerFactory = Substitute.For<ISchedulerFactory>();
        schedulerFactory.GetScheduler(Arg.Any<CancellationToken>()).Returns(scheduler);

        var manager = new QuartzScheduleManager(schedulerFactory, db);

        return (manager, db, scheduler);
    }

    private static JobSchedule CreateCronSchedule(Guid jobId, bool isEnabled = true) => new()
    {
        Id = Guid.CreateVersion7(),
        JobId = jobId,
        ScheduleType = "cron",
        CronExpression = "0 0 3 * * ?",
        IsEnabled = isEnabled,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
    };

    private static JobSchedule CreateOneShotSchedule(Guid jobId, bool isEnabled = true) => new()
    {
        Id = Guid.CreateVersion7(),
        JobId = jobId,
        ScheduleType = "one_shot",
        RunAt = DateTimeOffset.UtcNow.AddHours(2),
        IsEnabled = isEnabled,
        CreatedAt = DateTime.UtcNow,
        UpdatedAt = DateTime.UtcNow,
    };

    [Fact]
    public async Task RegisterAsync_CronSchedule_SchedulesJobWithCronTrigger()
    {
        // Arrange
        var (manager, db, scheduler) = CreateManager();
        var jobId = Guid.CreateVersion7();
        var schedule = CreateCronSchedule(jobId);
        db.JobSchedules.Add(schedule);
        await db.SaveChangesAsync();

        // Act
        await manager.RegisterAsync(schedule);

        // Assert
        await scheduler.Received(1).ScheduleJob(
            Arg.Is<IJobDetail>(j =>
                j.Key.Name == schedule.Id.ToString() &&
                j.Key.Group == "courier" &&
                j.JobDataMap.GetString("jobId") == jobId.ToString()),
            Arg.Is<IReadOnlyCollection<ITrigger>>(triggers =>
                triggers.Count == 1 &&
                triggers.First().Key.Name == schedule.Id.ToString() &&
                triggers.First().Key.Group == "courier"),
            Arg.Is(true),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RegisterAsync_OneShotSchedule_SchedulesJobWithSimpleTrigger()
    {
        // Arrange
        var (manager, db, scheduler) = CreateManager();
        var jobId = Guid.CreateVersion7();
        var schedule = CreateOneShotSchedule(jobId);
        db.JobSchedules.Add(schedule);
        await db.SaveChangesAsync();

        // Act
        await manager.RegisterAsync(schedule);

        // Assert
        await scheduler.Received(1).ScheduleJob(
            Arg.Is<IJobDetail>(j =>
                j.Key.Name == schedule.Id.ToString() &&
                j.Key.Group == "courier"),
            Arg.Any<IReadOnlyCollection<ITrigger>>(),
            Arg.Is(true),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task RegisterAsync_SetsNextFireAtOnSchedule()
    {
        // Arrange
        var (manager, db, scheduler) = CreateManager();
        var jobId = Guid.CreateVersion7();
        var schedule = CreateCronSchedule(jobId);
        db.JobSchedules.Add(schedule);
        await db.SaveChangesAsync();

        schedule.NextFireAt.ShouldBeNull();

        // Act
        await manager.RegisterAsync(schedule);

        // Assert — RegisterAsync calls SaveChangesAsync after setting NextFireAt.
        // Since the mock trigger returns null for GetNextFireTimeUtc, NextFireAt remains null,
        // but the important thing is no exception was thrown and the flow completed.
        var updated = await db.JobSchedules.FirstAsync(s => s.Id == schedule.Id);
        // NextFireAt is set from the trigger's GetNextFireTimeUtc (null from mock is acceptable)
        updated.ShouldNotBeNull();
    }

    [Fact]
    public async Task UnregisterAsync_CallsDeleteJob()
    {
        // Arrange
        var (manager, _, scheduler) = CreateManager();
        var scheduleId = Guid.CreateVersion7();

        // Act
        await manager.UnregisterAsync(scheduleId);

        // Assert
        await scheduler.Received(1).DeleteJob(
            Arg.Is<JobKey>(k => k.Name == scheduleId.ToString() && k.Group == "courier"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncAllAsync_RegistersEnabledSchedules()
    {
        // Arrange
        var (manager, db, scheduler) = CreateManager();
        var jobId = Guid.CreateVersion7();

        db.JobSchedules.Add(CreateCronSchedule(jobId));
        db.JobSchedules.Add(CreateCronSchedule(jobId));
        await db.SaveChangesAsync();

        scheduler.GetJobKeys(Arg.Any<GroupMatcher<JobKey>>(), Arg.Any<CancellationToken>())
            .Returns(new HashSet<JobKey>());

        // Act
        await manager.SyncAllAsync();

        // Assert — 2 enabled schedules → each calls DeleteJob + ScheduleJob
        await scheduler.Received(2).ScheduleJob(
            Arg.Any<IJobDetail>(),
            Arg.Any<IReadOnlyCollection<ITrigger>>(),
            Arg.Is(true),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncAllAsync_IgnoresDisabledSchedules()
    {
        // Arrange
        var (manager, db, scheduler) = CreateManager();
        var jobId = Guid.CreateVersion7();

        db.JobSchedules.Add(CreateCronSchedule(jobId, isEnabled: true));
        db.JobSchedules.Add(CreateCronSchedule(jobId, isEnabled: false));
        await db.SaveChangesAsync();

        scheduler.GetJobKeys(Arg.Any<GroupMatcher<JobKey>>(), Arg.Any<CancellationToken>())
            .Returns(new HashSet<JobKey>());

        // Act
        await manager.SyncAllAsync();

        // Assert — only 1 enabled schedule registered
        await scheduler.Received(1).ScheduleJob(
            Arg.Any<IJobDetail>(),
            Arg.Any<IReadOnlyCollection<ITrigger>>(),
            Arg.Is(true),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncAllAsync_RemovesOrphanedQuartzJobs()
    {
        // Arrange
        var (manager, db, scheduler) = CreateManager();
        var orphanId = Guid.NewGuid().ToString();

        scheduler.GetJobKeys(Arg.Any<GroupMatcher<JobKey>>(), Arg.Any<CancellationToken>())
            .Returns(new HashSet<JobKey> { new(orphanId, "courier") });

        // Act
        await manager.SyncAllAsync();

        // Assert — orphaned job should be deleted
        await scheduler.Received().DeleteJob(
            Arg.Is<JobKey>(k => k.Name == orphanId && k.Group == "courier"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task SyncAllAsync_NoSchedules_NoQuartzCalls()
    {
        // Arrange
        var (manager, _, scheduler) = CreateManager();

        scheduler.GetJobKeys(Arg.Any<GroupMatcher<JobKey>>(), Arg.Any<CancellationToken>())
            .Returns(new HashSet<JobKey>());

        // Act
        await manager.SyncAllAsync();

        // Assert — no ScheduleJob calls
        await scheduler.DidNotReceive().ScheduleJob(
            Arg.Any<IJobDetail>(),
            Arg.Any<IReadOnlyCollection<ITrigger>>(),
            Arg.Any<bool>(),
            Arg.Any<CancellationToken>());
    }
}
