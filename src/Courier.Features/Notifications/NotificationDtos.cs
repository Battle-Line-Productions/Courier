namespace Courier.Features.Notifications;

public record NotificationRuleDto
{
    public Guid Id { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required string EntityType { get; init; }
    public Guid? EntityId { get; init; }
    public required List<string> EventTypes { get; init; }
    public required string Channel { get; init; }
    public required object ChannelConfig { get; init; }
    public bool IsEnabled { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}

public record CreateNotificationRuleRequest
{
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required string EntityType { get; init; }
    public Guid? EntityId { get; init; }
    public required List<string> EventTypes { get; init; }
    public required string Channel { get; init; }
    public required object ChannelConfig { get; init; }
    public bool IsEnabled { get; init; } = true;
}

public record UpdateNotificationRuleRequest
{
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required string EntityType { get; init; }
    public Guid? EntityId { get; init; }
    public required List<string> EventTypes { get; init; }
    public required string Channel { get; init; }
    public required object ChannelConfig { get; init; }
    public bool IsEnabled { get; init; }
}

public record NotificationLogDto
{
    public Guid Id { get; init; }
    public Guid NotificationRuleId { get; init; }
    public string? RuleName { get; init; }
    public required string EventType { get; init; }
    public required string EntityType { get; init; }
    public Guid EntityId { get; init; }
    public required string Channel { get; init; }
    public required string Recipient { get; init; }
    public required object Payload { get; init; }
    public bool Success { get; init; }
    public string? ErrorMessage { get; init; }
    public DateTime SentAt { get; init; }
}
