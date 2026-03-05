namespace Courier.Features.Chains;

public record CreateChainScheduleRequest
{
    public required string ScheduleType { get; init; }
    public string? CronExpression { get; init; }
    public DateTimeOffset? RunAt { get; init; }
    public bool IsEnabled { get; init; } = true;
}

public record UpdateChainScheduleRequest
{
    public string? CronExpression { get; init; }
    public DateTimeOffset? RunAt { get; init; }
    public bool? IsEnabled { get; init; }
}

public record ChainScheduleDto
{
    public Guid Id { get; init; }
    public Guid ChainId { get; init; }
    public required string ScheduleType { get; init; }
    public string? CronExpression { get; init; }
    public DateTimeOffset? RunAt { get; init; }
    public bool IsEnabled { get; init; }
    public DateTimeOffset? LastFiredAt { get; init; }
    public DateTimeOffset? NextFireAt { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}
