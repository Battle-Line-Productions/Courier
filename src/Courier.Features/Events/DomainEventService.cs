using System.Text.Json;
using Courier.Domain.Entities;
using Courier.Infrastructure.Data;

namespace Courier.Features.Events;

public class DomainEventService
{
    private readonly CourierDbContext _db;

    public DomainEventService(CourierDbContext db)
    {
        _db = db;
    }

    public async Task RecordAsync(string eventType, string entityType, Guid entityId, object? payload = null, CancellationToken ct = default)
    {
        var domainEvent = new DomainEvent
        {
            Id = Guid.CreateVersion7(),
            EventType = eventType,
            EntityType = entityType,
            EntityId = entityId,
            Payload = payload is not null ? JsonSerializer.Serialize(payload) : null,
            OccurredAt = DateTime.UtcNow,
        };

        _db.DomainEvents.Add(domainEvent);
        await _db.SaveChangesAsync(ct);
    }

    public void Record(string eventType, string entityType, Guid entityId, object? payload = null)
    {
        var domainEvent = new DomainEvent
        {
            Id = Guid.CreateVersion7(),
            EventType = eventType,
            EntityType = entityType,
            EntityId = entityId,
            Payload = payload is not null ? JsonSerializer.Serialize(payload) : null,
            OccurredAt = DateTime.UtcNow,
        };

        _db.DomainEvents.Add(domainEvent);
        // SaveChanges will be called by the caller's transaction
    }
}
