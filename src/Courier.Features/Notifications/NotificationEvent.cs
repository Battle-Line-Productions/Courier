namespace Courier.Features.Notifications;

public record NotificationEvent
{
    public required string EventType { get; init; }
    public required string EntityType { get; init; }
    public Guid EntityId { get; init; }
    public string? EntityName { get; init; }
    public Dictionary<string, object> Context { get; init; } = [];
}
