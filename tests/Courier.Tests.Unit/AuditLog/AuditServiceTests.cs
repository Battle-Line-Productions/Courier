using Courier.Domain.Enums;
using Courier.Features.AuditLog;
using Courier.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Courier.Tests.Unit.AuditLog;

public class AuditServiceTests
{
    private static CourierDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<CourierDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new CourierDbContext(options);
    }

    [Fact]
    public async Task LogAsync_PersistsEntry()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var service = new AuditService(db);
        var entityId = Guid.NewGuid();

        // Act
        await service.LogAsync(AuditableEntityType.Job, entityId, "Created");

        // Assert
        var entry = await db.AuditLogEntries.FirstOrDefaultAsync();
        entry.ShouldNotBeNull();
        entry.EntityType.ShouldBe("job");
        entry.EntityId.ShouldBe(entityId);
        entry.Operation.ShouldBe("Created");
        entry.PerformedBy.ShouldBe("system");
        entry.PerformedAt.ShouldBeGreaterThan(DateTime.MinValue);
        entry.Id.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public async Task LogAsync_SerializesDetails()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var service = new AuditService(db);

        // Act
        await service.LogAsync(AuditableEntityType.Connection, Guid.NewGuid(), "Created",
            details: new { name = "Test", host = "sftp.example.com" });

        // Assert
        var entry = await db.AuditLogEntries.FirstOrDefaultAsync();
        entry.ShouldNotBeNull();
        entry.Details.ShouldContain("Test");
        entry.Details.ShouldContain("sftp.example.com");
    }

    [Fact]
    public async Task LogAsync_NullDetails_DefaultsToEmptyJson()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var service = new AuditService(db);

        // Act
        await service.LogAsync(AuditableEntityType.Job, Guid.NewGuid(), "Deleted");

        // Assert
        var entry = await db.AuditLogEntries.FirstOrDefaultAsync();
        entry.ShouldNotBeNull();
        entry.Details.ShouldBe("{}");
    }

    [Fact]
    public async Task ListAsync_ReturnsDescendingByDate()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var service = new AuditService(db);

        await service.LogAsync(AuditableEntityType.Job, Guid.NewGuid(), "First");
        await Task.Delay(10); // ensure different timestamps
        await service.LogAsync(AuditableEntityType.Job, Guid.NewGuid(), "Second");

        // Act
        var result = await service.ListAsync();

        // Assert
        result.Data.Count.ShouldBe(2);
        result.Data[0].Operation.ShouldBe("Second");
        result.Data[1].Operation.ShouldBe("First");
    }

    [Fact]
    public async Task ListAsync_FilterByEntityType()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var service = new AuditService(db);

        await service.LogAsync(AuditableEntityType.Job, Guid.NewGuid(), "Created");
        await service.LogAsync(AuditableEntityType.Connection, Guid.NewGuid(), "Created");

        // Act
        var result = await service.ListAsync(new AuditLogFilter { EntityType = "job" });

        // Assert
        result.Data.Count.ShouldBe(1);
        result.Data[0].EntityType.ShouldBe("job");
    }

    [Fact]
    public async Task ListAsync_FilterByOperation()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var service = new AuditService(db);

        await service.LogAsync(AuditableEntityType.Job, Guid.NewGuid(), "Created");
        await service.LogAsync(AuditableEntityType.Job, Guid.NewGuid(), "Deleted");

        // Act
        var result = await service.ListAsync(new AuditLogFilter { Operation = "deleted" });

        // Assert
        result.Data.Count.ShouldBe(1);
        result.Data[0].Operation.ShouldBe("Deleted");
    }

    [Fact]
    public async Task ListAsync_FilterByPerformedBy()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var service = new AuditService(db);

        await service.LogAsync(AuditableEntityType.Job, Guid.NewGuid(), "Created", performedBy: "system");
        await service.LogAsync(AuditableEntityType.Job, Guid.NewGuid(), "Created", performedBy: "admin@example.com");

        // Act
        var result = await service.ListAsync(new AuditLogFilter { PerformedBy = "admin" });

        // Assert
        result.Data.Count.ShouldBe(1);
        result.Data[0].PerformedBy.ShouldBe("admin@example.com");
    }

    [Fact]
    public async Task ListAsync_FilterByDateRange()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var service = new AuditService(db);

        await service.LogAsync(AuditableEntityType.Job, Guid.NewGuid(), "Old");
        await service.LogAsync(AuditableEntityType.Job, Guid.NewGuid(), "Recent");

        var from = DateTime.UtcNow.AddSeconds(-1);
        var to = DateTime.UtcNow.AddSeconds(1);

        // Act
        var result = await service.ListAsync(new AuditLogFilter { From = from, To = to });

        // Assert
        result.Data.Count.ShouldBe(2);
    }

    [Fact]
    public async Task ListAsync_Pagination()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var service = new AuditService(db);

        for (int i = 0; i < 5; i++)
            await service.LogAsync(AuditableEntityType.Job, Guid.NewGuid(), $"Op{i}");

        // Act
        var page1 = await service.ListAsync(page: 1, pageSize: 2);
        var page2 = await service.ListAsync(page: 2, pageSize: 2);

        // Assert
        page1.Data.Count.ShouldBe(2);
        page1.Pagination.TotalCount.ShouldBe(5);
        page1.Pagination.TotalPages.ShouldBe(3);
        page2.Data.Count.ShouldBe(2);
    }

    [Fact]
    public async Task ListByEntityAsync_ReturnsOnlyForEntity()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var service = new AuditService(db);
        var targetId = Guid.NewGuid();

        await service.LogAsync(AuditableEntityType.Job, targetId, "Created");
        await service.LogAsync(AuditableEntityType.Job, targetId, "Updated");
        await service.LogAsync(AuditableEntityType.Job, Guid.NewGuid(), "Created");

        // Act
        var result = await service.ListByEntityAsync("job", targetId);

        // Assert
        result.Data.Count.ShouldBe(2);
        result.Data.ShouldAllBe(e => e.EntityId == targetId);
    }

    [Fact]
    public async Task ListAsync_CombinedFilters()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var service = new AuditService(db);

        await service.LogAsync(AuditableEntityType.Job, Guid.NewGuid(), "Created");
        await service.LogAsync(AuditableEntityType.Connection, Guid.NewGuid(), "Created");
        await service.LogAsync(AuditableEntityType.Job, Guid.NewGuid(), "Deleted");

        // Act — filter to only job + Created
        var result = await service.ListAsync(new AuditLogFilter { EntityType = "job", Operation = "created" });

        // Assert
        result.Data.Count.ShouldBe(1);
        result.Data[0].EntityType.ShouldBe("job");
        result.Data[0].Operation.ShouldBe("Created");
    }

    [Fact]
    public async Task LogAsync_MapsEntityTypesToSnakeCase()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var service = new AuditService(db);

        // Act
        await service.LogAsync(AuditableEntityType.JobExecution, Guid.NewGuid(), "Started");
        await service.LogAsync(AuditableEntityType.StepExecution, Guid.NewGuid(), "Completed");
        await service.LogAsync(AuditableEntityType.PgpKey, Guid.NewGuid(), "Created");
        await service.LogAsync(AuditableEntityType.SshKey, Guid.NewGuid(), "Created");
        await service.LogAsync(AuditableEntityType.FileMonitor, Guid.NewGuid(), "Created");

        // Assert
        var entries = await db.AuditLogEntries.ToListAsync();
        entries.Select(e => e.EntityType).ShouldBe(
            ["job_execution", "step_execution", "pgp_key", "ssh_key", "file_monitor"],
            ignoreOrder: true);
    }
}
