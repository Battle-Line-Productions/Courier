using Courier.Domain.Common;
using Courier.Domain.Entities;
using Courier.Features.AuditLog;
using Courier.Features.Jobs;
using Courier.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
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
        var service = new JobScheduleService(db, new AuditService(db));

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
        var service = new JobScheduleService(db, new AuditService(db));
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
    public async Task CreateAsync_CronSchedule_ReturnsSuccess()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var service = new JobScheduleService(db, new AuditService(db));
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
        result.Data.Id.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public async Task CreateAsync_OneShotSchedule_ReturnsSuccess()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var service = new JobScheduleService(db, new AuditService(db));
        var job = await SeedJob(db);

        var runAt = DateTimeOffset.UtcNow.AddHours(1);
        var request = new CreateJobScheduleRequest
        {
            ScheduleType = "one_shot",
            RunAt = runAt,
            IsEnabled = true,
        };

        // Act
        var result = await service.CreateAsync(job.Id, request);

        // Assert
        result.Success.ShouldBeTrue();
        result.Data!.ScheduleType.ShouldBe("one_shot");
        result.Data.RunAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task CreateAsync_DisabledSchedule_PersistsDisabledState()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var service = new JobScheduleService(db, new AuditService(db));
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
    }

    [Fact]
    public async Task CreateAsync_JobNotFound_ReturnsError()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var service = new JobScheduleService(db, new AuditService(db));

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
    public async Task UpdateAsync_UpdatesCronExpression()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var service = new JobScheduleService(db, new AuditService(db));
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
        var result = await service.UpdateAsync(job.Id, schedule.Id, new UpdateJobScheduleRequest { CronExpression = "0 0 6 * * ?" });

        // Assert
        result.Success.ShouldBeTrue();
        result.Data!.CronExpression.ShouldBe("0 0 6 * * ?");
    }

    [Fact]
    public async Task UpdateAsync_TogglesEnabled()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var service = new JobScheduleService(db, new AuditService(db));
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
    }

    [Fact]
    public async Task UpdateAsync_NotFound_ReturnsError()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var service = new JobScheduleService(db, new AuditService(db));

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
        var service = new JobScheduleService(db, new AuditService(db));
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
    public async Task DeleteAsync_RemovesSchedule()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var service = new JobScheduleService(db, new AuditService(db));
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
    }

    [Fact]
    public async Task DeleteAsync_NotFound_ReturnsError()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var service = new JobScheduleService(db, new AuditService(db));

        // Act
        var result = await service.DeleteAsync(Guid.NewGuid(), Guid.NewGuid());

        // Assert
        result.Success.ShouldBeFalse();
        result.Error!.Code.ShouldBe(ErrorCodes.ScheduleNotFound);
    }
}
