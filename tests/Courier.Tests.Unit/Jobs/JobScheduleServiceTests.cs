using Courier.Domain.Common;
using Courier.Domain.Entities;
using Courier.Features.Jobs;
using Courier.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using NSubstitute;
using Quartz;
using Shouldly;

namespace Courier.Tests.Unit.Jobs;

public class JobScheduleServiceTests
{
    private static CourierDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<CourierDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new CourierDbContext(options);
    }

    private static (ISchedulerFactory factory, IScheduler scheduler) CreateMockScheduler()
    {
        var scheduler = Substitute.For<IScheduler>();
        var factory = Substitute.For<ISchedulerFactory>();
        factory.GetScheduler(Arg.Any<CancellationToken>()).Returns(scheduler);
        return (factory, scheduler);
    }

    private static async Task<Job> SeedJob(CourierDbContext db, bool isEnabled = true)
    {
        var job = new Job
        {
            Id = Guid.CreateVersion7(),
            Name = "Test Job",
            IsEnabled = isEnabled,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.Jobs.Add(job);
        await db.SaveChangesAsync();
        return job;
    }

    [Fact]
    public async Task ListAsync_JobNotFound_ReturnsError()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var (factory, _) = CreateMockScheduler();
        var service = new JobScheduleService(db, factory);

        // Act
        var result = await service.ListAsync(Guid.NewGuid());

        // Assert
        result.Success.ShouldBeFalse();
        result.Error!.Code.ShouldBe(ErrorCodes.ResourceNotFound);
    }

    [Fact]
    public async Task ListAsync_ReturnsSchedules()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var (factory, _) = CreateMockScheduler();
        var service = new JobScheduleService(db, factory);
        var job = await SeedJob(db);

        db.JobSchedules.Add(new JobSchedule
        {
            Id = Guid.CreateVersion7(),
            JobId = job.Id,
            ScheduleType = "cron",
            CronExpression = "0 0 3 * * ?",
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        // Act
        var result = await service.ListAsync(job.Id);

        // Assert
        result.Success.ShouldBeTrue();
        result.Data!.Count.ShouldBe(1);
        result.Data[0].ScheduleType.ShouldBe("cron");
    }

    [Fact]
    public async Task CreateAsync_CronSchedule_RegistersWithQuartz()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var (factory, scheduler) = CreateMockScheduler();
        var service = new JobScheduleService(db, factory);
        var job = await SeedJob(db);

        var request = new CreateJobScheduleRequest
        {
            ScheduleType = "cron",
            CronExpression = "0 0 3 * * ?",
            IsEnabled = true,
        };

        // Act
        var result = await service.CreateAsync(job.Id, request);

        // Assert
        result.Success.ShouldBeTrue();
        result.Data!.ScheduleType.ShouldBe("cron");
        result.Data.CronExpression.ShouldBe("0 0 3 * * ?");
        result.Data.IsEnabled.ShouldBeTrue();
        result.Data.JobId.ShouldBe(job.Id);

        await scheduler.Received(1).ScheduleJob(
            Arg.Any<IJobDetail>(),
            Arg.Any<IReadOnlyCollection<ITrigger>>(),
            Arg.Is(true),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_DisabledSchedule_SkipsQuartz()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var (factory, scheduler) = CreateMockScheduler();
        var service = new JobScheduleService(db, factory);
        var job = await SeedJob(db);

        var request = new CreateJobScheduleRequest
        {
            ScheduleType = "cron",
            CronExpression = "0 0 3 * * ?",
            IsEnabled = false,
        };

        // Act
        var result = await service.CreateAsync(job.Id, request);

        // Assert
        result.Success.ShouldBeTrue();
        result.Data!.IsEnabled.ShouldBeFalse();

        await scheduler.DidNotReceive().ScheduleJob(
            Arg.Any<IJobDetail>(),
            Arg.Any<IReadOnlyCollection<ITrigger>>(),
            Arg.Any<bool>(),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task CreateAsync_JobNotFound_ReturnsError()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var (factory, _) = CreateMockScheduler();
        var service = new JobScheduleService(db, factory);

        var request = new CreateJobScheduleRequest
        {
            ScheduleType = "cron",
            CronExpression = "0 0 3 * * ?",
        };

        // Act
        var result = await service.CreateAsync(Guid.NewGuid(), request);

        // Assert
        result.Success.ShouldBeFalse();
        result.Error!.Code.ShouldBe(ErrorCodes.ResourceNotFound);
    }

    [Fact]
    public async Task UpdateAsync_EnableSchedule_RegistersWithQuartz()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var (factory, scheduler) = CreateMockScheduler();
        var service = new JobScheduleService(db, factory);
        var job = await SeedJob(db);

        var schedule = new JobSchedule
        {
            Id = Guid.CreateVersion7(),
            JobId = job.Id,
            ScheduleType = "cron",
            CronExpression = "0 0 3 * * ?",
            IsEnabled = false,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.JobSchedules.Add(schedule);
        await db.SaveChangesAsync();

        // Act
        var result = await service.UpdateAsync(job.Id, schedule.Id, new UpdateJobScheduleRequest { IsEnabled = true });

        // Assert
        result.Success.ShouldBeTrue();
        result.Data!.IsEnabled.ShouldBeTrue();

        await scheduler.Received(1).ScheduleJob(
            Arg.Any<IJobDetail>(),
            Arg.Any<IReadOnlyCollection<ITrigger>>(),
            Arg.Is(true),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAsync_DisableSchedule_UnregistersFromQuartz()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var (factory, scheduler) = CreateMockScheduler();
        var service = new JobScheduleService(db, factory);
        var job = await SeedJob(db);

        var schedule = new JobSchedule
        {
            Id = Guid.CreateVersion7(),
            JobId = job.Id,
            ScheduleType = "cron",
            CronExpression = "0 0 3 * * ?",
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.JobSchedules.Add(schedule);
        await db.SaveChangesAsync();

        // Act
        var result = await service.UpdateAsync(job.Id, schedule.Id, new UpdateJobScheduleRequest { IsEnabled = false });

        // Assert
        result.Success.ShouldBeTrue();
        result.Data!.IsEnabled.ShouldBeFalse();

        await scheduler.Received(1).DeleteJob(
            Arg.Is<JobKey>(k => k.Name == schedule.Id.ToString() && k.Group == "courier"),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task UpdateAsync_NotFound_ReturnsError()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var (factory, _) = CreateMockScheduler();
        var service = new JobScheduleService(db, factory);

        // Act
        var result = await service.UpdateAsync(Guid.NewGuid(), Guid.NewGuid(), new UpdateJobScheduleRequest { IsEnabled = true });

        // Assert
        result.Success.ShouldBeFalse();
        result.Error!.Code.ShouldBe(ErrorCodes.ScheduleNotFound);
    }

    [Fact]
    public async Task UpdateAsync_WrongJob_ReturnsError()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var (factory, _) = CreateMockScheduler();
        var service = new JobScheduleService(db, factory);
        var job = await SeedJob(db);

        var schedule = new JobSchedule
        {
            Id = Guid.CreateVersion7(),
            JobId = job.Id,
            ScheduleType = "cron",
            CronExpression = "0 0 3 * * ?",
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.JobSchedules.Add(schedule);
        await db.SaveChangesAsync();

        // Act
        var result = await service.UpdateAsync(Guid.NewGuid(), schedule.Id, new UpdateJobScheduleRequest { IsEnabled = false });

        // Assert
        result.Success.ShouldBeFalse();
        result.Error!.Code.ShouldBe(ErrorCodes.ScheduleJobMismatch);
    }

    [Fact]
    public async Task DeleteAsync_RemovesAndUnregisters()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var (factory, scheduler) = CreateMockScheduler();
        var service = new JobScheduleService(db, factory);
        var job = await SeedJob(db);

        var schedule = new JobSchedule
        {
            Id = Guid.CreateVersion7(),
            JobId = job.Id,
            ScheduleType = "cron",
            CronExpression = "0 0 3 * * ?",
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.JobSchedules.Add(schedule);
        await db.SaveChangesAsync();

        // Act
        var result = await service.DeleteAsync(job.Id, schedule.Id);

        // Assert
        result.Success.ShouldBeTrue();
        (await db.JobSchedules.AnyAsync(s => s.Id == schedule.Id)).ShouldBeFalse();

        await scheduler.Received(1).DeleteJob(
            Arg.Is<JobKey>(k => k.Name == schedule.Id.ToString()),
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task DeleteAsync_NotFound_ReturnsError()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var (factory, _) = CreateMockScheduler();
        var service = new JobScheduleService(db, factory);

        // Act
        var result = await service.DeleteAsync(Guid.NewGuid(), Guid.NewGuid());

        // Assert
        result.Success.ShouldBeFalse();
        result.Error!.Code.ShouldBe(ErrorCodes.ScheduleNotFound);
    }
}
