namespace Courier.Features.Jobs;

public record CreateJobScheduleRequest
{
    public required string ScheduleType { get; init; }
    public string? CronExpression { get; init; }
    public DateTimeOffset? RunAt { get; init; }
    public bool IsEnabled { get; init; } = true;
}

public record UpdateJobScheduleRequest
{
    public string? CronExpression { get; init; }
    public DateTimeOffset? RunAt { get; init; }
    public bool? IsEnabled { get; init; }
}

public record JobScheduleDto
{
    public Guid Id { get; init; }
    public Guid JobId { get; init; }
    public required string ScheduleType { get; init; }
    public string? CronExpression { get; init; }
    public DateTimeOffset? RunAt { get; init; }
    public bool IsEnabled { get; init; }
    public DateTimeOffset? LastFiredAt { get; init; }
    public DateTimeOffset? NextFireAt { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
}
