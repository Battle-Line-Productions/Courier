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
    public DateTime? PausedAt { get; init; }
    public string? PausedBy { get; init; }
    public DateTime? CancelledAt { get; init; }
    public string? CancelledBy { get; init; }
    public string? CancelReason { get; init; }
    public DateTime CreatedAt { get; init; }
    public List<StepExecutionDto>? StepExecutions { get; init; }
}

public record CancelExecutionRequest
{
    public string? Reason { get; init; }
}

public record StepExecutionDto
{
    public Guid Id { get; init; }
    public int StepOrder { get; init; }
    public required string StepName { get; init; }
    public required string StepTypeKey { get; init; }
    public required string State { get; init; }
    public DateTime? StartedAt { get; init; }
    public DateTime? CompletedAt { get; init; }
    public long? DurationMs { get; init; }
    public long? BytesProcessed { get; init; }
    public string? OutputData { get; init; }
    public string? ErrorMessage { get; init; }
    public int RetryAttempt { get; init; }
}
