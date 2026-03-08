using Courier.Domain.Entities;
using Courier.Features.AuditLog;
using Courier.Features.Monitors;
using Courier.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Courier.Tests.Unit.Monitors;

public class MonitorHealthTests
{
    private static CourierDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<CourierDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new CourierDbContext(options);
    }

    private static FileMonitor CreateMonitorEntity(
        string name = "Test Monitor",
        DateTime? lastPolledAt = null,
        long? lastPollDurationMs = null,
        int? lastPollFileCount = null,
        int consecutiveFailureCount = 0,
        DateTime? lastOverflowAt = null,
        int overflowCount24h = 0)
    {
        return new FileMonitor
        {
            Id = Guid.NewGuid(),
            Name = name,
            WatchTarget = "/data/incoming",
            TriggerEvents = 1,
            PollingIntervalSec = 60,
            State = "active",
            LastPolledAt = lastPolledAt,
            LastPollDurationMs = lastPollDurationMs,
            LastPollFileCount = lastPollFileCount,
            ConsecutiveFailureCount = consecutiveFailureCount,
            LastOverflowAt = lastOverflowAt,
            OverflowCount24h = overflowCount24h,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };
    }

    [Fact]
    public async Task GetByIdAsync_IncludesHealthMetricsInDto()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var service = new MonitorService(db, new AuditService(db));
        var pollTime = DateTime.UtcNow.AddMinutes(-5);

        var monitor = CreateMonitorEntity(
            lastPolledAt: pollTime,
            lastPollDurationMs: 250,
            lastPollFileCount: 3,
            consecutiveFailureCount: 0);
        db.FileMonitors.Add(monitor);
        await db.SaveChangesAsync();

        // Act
        var result = await service.GetByIdAsync(monitor.Id);

        // Assert
        result.Success.ShouldBeTrue();
        result.Data!.LastPolledAt.ShouldBe(pollTime);
        result.Data.LastPollDurationMs.ShouldBe(250);
        result.Data.LastPollFileCount.ShouldBe(3);
        result.Data.ConsecutiveFailureCount.ShouldBe(0);
    }

    [Fact]
    public async Task GetByIdAsync_LastPollTimestampReflectsEntityValue()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var service = new MonitorService(db, new AuditService(db));
        var pollTime = new DateTime(2026, 3, 1, 12, 0, 0, DateTimeKind.Utc);

        var monitor = CreateMonitorEntity(lastPolledAt: pollTime);
        db.FileMonitors.Add(monitor);
        await db.SaveChangesAsync();

        // Act
        var result = await service.GetByIdAsync(monitor.Id);

        // Assert
        result.Data!.LastPolledAt.ShouldBe(pollTime);
    }

    [Fact]
    public async Task GetByIdAsync_FileCountTrackedPerPoll()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var service = new MonitorService(db, new AuditService(db));

        var monitor = CreateMonitorEntity(lastPollFileCount: 42);
        db.FileMonitors.Add(monitor);
        await db.SaveChangesAsync();

        // Act
        var result = await service.GetByIdAsync(monitor.Id);

        // Assert
        result.Data!.LastPollFileCount.ShouldBe(42);
    }

    [Fact]
    public async Task GetByIdAsync_ConsecutiveFailureCountTracked()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var service = new MonitorService(db, new AuditService(db));

        var monitor = CreateMonitorEntity(consecutiveFailureCount: 3);
        db.FileMonitors.Add(monitor);
        await db.SaveChangesAsync();

        // Act
        var result = await service.GetByIdAsync(monitor.Id);

        // Assert
        result.Data!.ConsecutiveFailureCount.ShouldBe(3);
    }

    [Fact]
    public async Task GetByIdAsync_OverflowMetricsIncluded()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var service = new MonitorService(db, new AuditService(db));
        var overflowTime = DateTime.UtcNow.AddMinutes(-10);

        var monitor = CreateMonitorEntity(lastOverflowAt: overflowTime, overflowCount24h: 5);
        db.FileMonitors.Add(monitor);
        await db.SaveChangesAsync();

        // Act
        var result = await service.GetByIdAsync(monitor.Id);

        // Assert
        result.Data!.LastOverflowAt.ShouldBe(overflowTime);
        result.Data.OverflowCount24h.ShouldBe(5);
    }

    [Fact]
    public async Task GetByIdAsync_NullHealthMetrics_ReturnsNulls()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var service = new MonitorService(db, new AuditService(db));

        var monitor = CreateMonitorEntity(); // no health metrics set
        db.FileMonitors.Add(monitor);
        await db.SaveChangesAsync();

        // Act
        var result = await service.GetByIdAsync(monitor.Id);

        // Assert
        result.Data!.LastPolledAt.ShouldBeNull();
        result.Data.LastPollDurationMs.ShouldBeNull();
        result.Data.LastPollFileCount.ShouldBeNull();
        result.Data.LastOverflowAt.ShouldBeNull();
        result.Data.OverflowCount24h.ShouldBe(0);
    }

    [Fact]
    public async Task GetByIdAsync_PollDurationMsMappedCorrectly()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var service = new MonitorService(db, new AuditService(db));

        var monitor = CreateMonitorEntity(lastPollDurationMs: 1500);
        db.FileMonitors.Add(monitor);
        await db.SaveChangesAsync();

        // Act
        var result = await service.GetByIdAsync(monitor.Id);

        // Assert
        result.Data!.LastPollDurationMs.ShouldBe(1500);
    }

    [Fact]
    public async Task GetByIdAsync_MaxConsecutiveFailuresMappedCorrectly()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var service = new MonitorService(db, new AuditService(db));

        var monitor = CreateMonitorEntity();
        monitor.MaxConsecutiveFailures = 10;
        db.FileMonitors.Add(monitor);
        await db.SaveChangesAsync();

        // Act
        var result = await service.GetByIdAsync(monitor.Id);

        // Assert
        result.Data!.MaxConsecutiveFailures.ShouldBe(10);
    }
}
