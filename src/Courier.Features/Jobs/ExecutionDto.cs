namespace Courier.Features.Jobs;

public record TriggerJobRequest
{
    public string TriggeredBy { get; init; } = "api";
}

public record JobExecutionDto
{
    public Guid Id { get; init; }
    public Guid JobId { get; init; }
    public required string State { get; init; }
    public required string TriggeredBy { get; init; }
    public DateTime? QueuedAt { get; init; }
    public DateTime? StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public DateTime CreatedAt { get; init; }
}
