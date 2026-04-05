namespace Courier.Domain.Entities;

public class StepCallback
{
    public Guid Id { get; set; }
    public string CallbackKey { get; set; } = string.Empty;
    public string Status { get; set; } = "pending";
    public string? ResultPayload { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime ExpiresAt { get; set; }
}
