using System.Text.Json;
using Courier.Features.Events;
using Courier.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Shouldly;

namespace Courier.Tests.Unit.Events;

public class DomainEventServiceTests
{
    private static CourierDbContext CreateInMemoryContext()
    {
        var options = new DbContextOptionsBuilder<CourierDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        return new CourierDbContext(options);
    }

    // --- RecordAsync ---

    [Fact]
    public async Task RecordAsync_PersistsEventToDatabase()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var service = new DomainEventService(db);
        var entityId = Guid.NewGuid();

        // Act
        await service.RecordAsync("JobStarted", "job_execution", entityId);

        // Assert
        var evt = await db.DomainEvents.FirstOrDefaultAsync();
        evt.ShouldNotBeNull();
        evt.EventType.ShouldBe("JobStarted");
        evt.EntityType.ShouldBe("job_execution");
        evt.EntityId.ShouldBe(entityId);
        evt.Id.ShouldNotBe(Guid.Empty);
    }

    [Fact]
    public async Task RecordAsync_SetsOccurredAtToUtcNow()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var service = new DomainEventService(db);
        var before = DateTime.UtcNow.AddSeconds(-1);

        // Act
        await service.RecordAsync("JobCompleted", "job_execution", Guid.NewGuid());

        // Assert
        var evt = await db.DomainEvents.FirstOrDefaultAsync();
        evt.ShouldNotBeNull();
        evt.OccurredAt.ShouldBeGreaterThan(before);
        evt.OccurredAt.ShouldBeLessThanOrEqualTo(DateTime.UtcNow.AddSeconds(1));
    }

    [Fact]
    public async Task RecordAsync_WithPayload_SerializesAsJson()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var service = new DomainEventService(db);
        var payload = new { jobId = Guid.NewGuid(), state = "completed" };

        // Act
        await service.RecordAsync("JobCompleted", "job_execution", Guid.NewGuid(), payload);

        // Assert
        var evt = await db.DomainEvents.FirstOrDefaultAsync();
        evt.ShouldNotBeNull();
        evt.Payload.ShouldNotBeNull();
        evt.Payload.ShouldContain("jobId");
        evt.Payload.ShouldContain("completed");

        // Verify it's valid JSON
        var doc = JsonDocument.Parse(evt.Payload);
        doc.RootElement.GetProperty("state").GetString().ShouldBe("completed");
    }

    [Fact]
    public async Task RecordAsync_NullPayload_StoresNull()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var service = new DomainEventService(db);

        // Act
        await service.RecordAsync("JobFailed", "job_execution", Guid.NewGuid());

        // Assert
        var evt = await db.DomainEvents.FirstOrDefaultAsync();
        evt.ShouldNotBeNull();
        evt.Payload.ShouldBeNull();
    }

    [Fact]
    public async Task RecordAsync_ProcessedAtIsNullOnCreation()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var service = new DomainEventService(db);

        // Act
        await service.RecordAsync("StepCompleted", "step_execution", Guid.NewGuid());

        // Assert
        var evt = await db.DomainEvents.FirstOrDefaultAsync();
        evt.ShouldNotBeNull();
        evt.ProcessedAt.ShouldBeNull();
        evt.ProcessedBy.ShouldBeNull();
    }

    [Fact]
    public async Task RecordAsync_MultipleEventsForSameEntity_RecordedIndependently()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var service = new DomainEventService(db);
        var entityId = Guid.NewGuid();

        // Act
        await service.RecordAsync("JobStarted", "job_execution", entityId);
        await service.RecordAsync("StepCompleted", "job_execution", entityId);
        await service.RecordAsync("JobCompleted", "job_execution", entityId);

        // Assert
        var events = await db.DomainEvents.Where(e => e.EntityId == entityId).ToListAsync();
        events.Count.ShouldBe(3);
        events.Select(e => e.EventType).ShouldBe(
            new[] { "JobStarted", "StepCompleted", "JobCompleted" },
            ignoreOrder: true);
    }

    [Fact]
    public async Task RecordAsync_StoresEntityTypeAndEntityId()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var service = new DomainEventService(db);
        var entityId = Guid.NewGuid();

        // Act
        await service.RecordAsync("StepFailed", "step_execution", entityId);

        // Assert
        var evt = await db.DomainEvents.FirstOrDefaultAsync();
        evt.ShouldNotBeNull();
        evt.EntityType.ShouldBe("step_execution");
        evt.EntityId.ShouldBe(entityId);
    }

    // --- Record (synchronous) ---

    [Fact]
    public async Task Record_AddsToContextButDoesNotSave()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var service = new DomainEventService(db);
        var entityId = Guid.NewGuid();

        // Act
        service.Record("JobPaused", "job_execution", entityId, new { reason = "user_requested" });

        // Assert — not yet persisted
        var countBeforeSave = await db.DomainEvents.CountAsync();
        countBeforeSave.ShouldBe(0);

        // Save manually
        await db.SaveChangesAsync();

        var countAfterSave = await db.DomainEvents.CountAsync();
        countAfterSave.ShouldBe(1);

        var evt = await db.DomainEvents.FirstOrDefaultAsync();
        evt.ShouldNotBeNull();
        evt.EventType.ShouldBe("JobPaused");
        evt.EntityId.ShouldBe(entityId);
        evt.Payload.ShouldNotBeNull();
        evt.Payload.ShouldContain("user_requested");
    }

    [Fact]
    public async Task Record_NullPayload_StoresNull()
    {
        // Arrange
        using var db = CreateInMemoryContext();
        var service = new DomainEventService(db);

        // Act
        service.Record("JobCancelled", "job_execution", Guid.NewGuid());
        await db.SaveChangesAsync();

        // Assert
        var evt = await db.DomainEvents.FirstOrDefaultAsync();
        evt.ShouldNotBeNull();
        evt.Payload.ShouldBeNull();
    }
}
