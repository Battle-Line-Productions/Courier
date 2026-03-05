namespace Courier.Domain.Entities;

public class ChainSchedule
{
    public Guid Id { get; set; }
    public Guid ChainId { get; set; }
    public string ScheduleType { get; set; } = string.Empty;
    public string? CronExpression { get; set; }
    public DateTimeOffset? RunAt { get; set; }
    public bool IsEnabled { get; set; } = true;
    public DateTimeOffset? LastFiredAt { get; set; }
    public DateTimeOffset? NextFireAt { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }

    public JobChain? Chain { get; set; }
}
