namespace Courier.Domain.Entities;

public class NotificationLog
{
    public Guid Id { get; set; }
    public Guid NotificationRuleId { get; set; }
    public string EventType { get; set; } = string.Empty;
    public string EntityType { get; set; } = string.Empty;
    public Guid EntityId { get; set; }
    public string Channel { get; set; } = string.Empty;
    public string Recipient { get; set; } = string.Empty;
    public string Payload { get; set; } = "{}";
    public bool Success { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime SentAt { get; set; }

    public NotificationRule NotificationRule { get; set; } = null!;
}
