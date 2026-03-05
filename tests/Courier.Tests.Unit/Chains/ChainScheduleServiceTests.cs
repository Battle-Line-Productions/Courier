using Courier.Domain.Common;
using Courier.Domain.Entities;
using Courier.Features.AuditLog;
using Courier.Features.Chains;
using Courier.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Courier.Tests.Unit.Chains;

public class ChainScheduleServiceTests
{
    private static CourierDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<CourierDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new CourierDbContext(options);
    }

    private static async Task<JobChain> SeedChain(CourierDbContext db)
    {
        var chain = new JobChain
        {
            Id = Guid.CreateVersion7(),
            Name = "Test Chain",
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.JobChains.Add(chain);
        await db.SaveChangesAsync();
        return chain;
    }

    [Fact]
    public async Task ListAsync_ChainNotFound_ReturnsError()
    {
        using var db = CreateInMemoryContext();
        var service = new ChainScheduleService(db, new AuditService(db));

        var result = await service.ListAsync(Guid.NewGuid());

        result.Success.ShouldBeFalse();
        result.Error!.Code.ShouldBe(ErrorCodes.ChainNotFound);
    }

    [Fact]
    public async Task ListAsync_ReturnsSchedules()
    {
        using var db = CreateInMemoryContext();
        var service = new ChainScheduleService(db, new AuditService(db));
        var chain = await SeedChain(db);

        db.ChainSchedules.Add(new ChainSchedule
        {
            Id = Guid.CreateVersion7(),
            ChainId = chain.Id,
            ScheduleType = "cron",
            CronExpression = "0 0 3 * * ?",
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        });
        await db.SaveChangesAsync();

        var result = await service.ListAsync(chain.Id);

        result.Success.ShouldBeTrue();
        result.Data!.Count.ShouldBe(1);
        result.Data[0].ScheduleType.ShouldBe("cron");
    }

    [Fact]
    public async Task CreateAsync_CronSchedule_ReturnsSuccess()
    {
        using var db = CreateInMemoryContext();
        var service = new ChainScheduleService(db, new AuditService(db));
        var chain = await SeedChain(db);

        var request = new CreateChainScheduleRequest
        {
            ScheduleType = "cron",
            CronExpression = "0 0 3 * * ?",
            IsEnabled = true,
        };

        var result = await service.CreateAsync(chain.Id, request);

        result.Success.ShouldBeTrue();
        result.Data!.ScheduleType.ShouldBe("cron");
        result.Data.CronExpression.ShouldBe("0 0 3 * * ?");
        result.Data.IsEnabled.ShouldBeTrue();
        result.Data.ChainId.ShouldBe(chain.Id);
        result.Data.Id.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public async Task CreateAsync_OneShotSchedule_ReturnsSuccess()
    {
        using var db = CreateInMemoryContext();
        var service = new ChainScheduleService(db, new AuditService(db));
        var chain = await SeedChain(db);

        var runAt = DateTimeOffset.UtcNow.AddHours(1);
        var request = new CreateChainScheduleRequest
        {
            ScheduleType = "one_shot",
            RunAt = runAt,
            IsEnabled = true,
        };

        var result = await service.CreateAsync(chain.Id, request);

        result.Success.ShouldBeTrue();
        result.Data!.ScheduleType.ShouldBe("one_shot");
        result.Data.RunAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task CreateAsync_DisabledSchedule_PersistsDisabledState()
    {
        using var db = CreateInMemoryContext();
        var service = new ChainScheduleService(db, new AuditService(db));
        var chain = await SeedChain(db);

        var request = new CreateChainScheduleRequest
        {
            ScheduleType = "cron",
            CronExpression = "0 0 3 * * ?",
            IsEnabled = false,
        };

        var result = await service.CreateAsync(chain.Id, request);

        result.Success.ShouldBeTrue();
        result.Data!.IsEnabled.ShouldBeFalse();
    }

    [Fact]
    public async Task CreateAsync_ChainNotFound_ReturnsError()
    {
        using var db = CreateInMemoryContext();
        var service = new ChainScheduleService(db, new AuditService(db));

        var request = new CreateChainScheduleRequest
        {
            ScheduleType = "cron",
            CronExpression = "0 0 3 * * ?",
        };

        var result = await service.CreateAsync(Guid.NewGuid(), request);

        result.Success.ShouldBeFalse();
        result.Error!.Code.ShouldBe(ErrorCodes.ChainNotFound);
    }

    [Fact]
    public async Task UpdateAsync_UpdatesCronExpression()
    {
        using var db = CreateInMemoryContext();
        var service = new ChainScheduleService(db, new AuditService(db));
        var chain = await SeedChain(db);

        var schedule = new ChainSchedule
        {
            Id = Guid.CreateVersion7(),
            ChainId = chain.Id,
            ScheduleType = "cron",
            CronExpression = "0 0 3 * * ?",
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.ChainSchedules.Add(schedule);
        await db.SaveChangesAsync();

        var result = await service.UpdateAsync(chain.Id, schedule.Id, new UpdateChainScheduleRequest { CronExpression = "0 0 6 * * ?" });

        result.Success.ShouldBeTrue();
        result.Data!.CronExpression.ShouldBe("0 0 6 * * ?");
    }

    [Fact]
    public async Task UpdateAsync_TogglesEnabled()
    {
        using var db = CreateInMemoryContext();
        var service = new ChainScheduleService(db, new AuditService(db));
        var chain = await SeedChain(db);

        var schedule = new ChainSchedule
        {
            Id = Guid.CreateVersion7(),
            ChainId = chain.Id,
            ScheduleType = "cron",
            CronExpression = "0 0 3 * * ?",
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.ChainSchedules.Add(schedule);
        await db.SaveChangesAsync();

        var result = await service.UpdateAsync(chain.Id, schedule.Id, new UpdateChainScheduleRequest { IsEnabled = false });

        result.Success.ShouldBeTrue();
        result.Data!.IsEnabled.ShouldBeFalse();
    }

    [Fact]
    public async Task UpdateAsync_NotFound_ReturnsError()
    {
        using var db = CreateInMemoryContext();
        var service = new ChainScheduleService(db, new AuditService(db));

        var result = await service.UpdateAsync(Guid.NewGuid(), Guid.NewGuid(), new UpdateChainScheduleRequest { IsEnabled = true });

        result.Success.ShouldBeFalse();
        result.Error!.Code.ShouldBe(ErrorCodes.ChainScheduleNotFound);
    }

    [Fact]
    public async Task UpdateAsync_WrongChain_ReturnsError()
    {
        using var db = CreateInMemoryContext();
        var service = new ChainScheduleService(db, new AuditService(db));
        var chain = await SeedChain(db);

        var schedule = new ChainSchedule
        {
            Id = Guid.CreateVersion7(),
            ChainId = chain.Id,
            ScheduleType = "cron",
            CronExpression = "0 0 3 * * ?",
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.ChainSchedules.Add(schedule);
        await db.SaveChangesAsync();

        var result = await service.UpdateAsync(Guid.NewGuid(), schedule.Id, new UpdateChainScheduleRequest { IsEnabled = false });

        result.Success.ShouldBeFalse();
        result.Error!.Code.ShouldBe(ErrorCodes.ChainScheduleMismatch);
    }

    [Fact]
    public async Task DeleteAsync_RemovesSchedule()
    {
        using var db = CreateInMemoryContext();
        var service = new ChainScheduleService(db, new AuditService(db));
        var chain = await SeedChain(db);

        var schedule = new ChainSchedule
        {
            Id = Guid.CreateVersion7(),
            ChainId = chain.Id,
            ScheduleType = "cron",
            CronExpression = "0 0 3 * * ?",
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.ChainSchedules.Add(schedule);
        await db.SaveChangesAsync();

        var result = await service.DeleteAsync(chain.Id, schedule.Id);

        result.Success.ShouldBeTrue();
        (await db.ChainSchedules.AnyAsync(s => s.Id == schedule.Id)).ShouldBeFalse();
    }

    [Fact]
    public async Task DeleteAsync_NotFound_ReturnsError()
    {
        using var db = CreateInMemoryContext();
        var service = new ChainScheduleService(db, new AuditService(db));

        var result = await service.DeleteAsync(Guid.NewGuid(), Guid.NewGuid());

        result.Success.ShouldBeFalse();
        result.Error!.Code.ShouldBe(ErrorCodes.ChainScheduleNotFound);
    }

    [Fact]
    public async Task DeleteAsync_WrongChain_ReturnsError()
    {
        using var db = CreateInMemoryContext();
        var service = new ChainScheduleService(db, new AuditService(db));
        var chain = await SeedChain(db);

        var schedule = new ChainSchedule
        {
            Id = Guid.CreateVersion7(),
            ChainId = chain.Id,
            ScheduleType = "cron",
            CronExpression = "0 0 3 * * ?",
            IsEnabled = true,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
        db.ChainSchedules.Add(schedule);
        await db.SaveChangesAsync();

        var result = await service.DeleteAsync(Guid.NewGuid(), schedule.Id);

        result.Success.ShouldBeFalse();
        result.Error!.Code.ShouldBe(ErrorCodes.ChainScheduleMismatch);
    }
}
