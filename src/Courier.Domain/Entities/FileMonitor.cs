namespace Courier.Domain.Entities;

public class FileMonitor
{
    public Guid Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string WatchTarget { get; set; } = string.Empty;
    public int TriggerEvents { get; set; }
    public string? FilePatterns { get; set; }
    public int PollingIntervalSec { get; set; } = 60;
    public int StabilityWindowSec { get; set; }
    public bool BatchMode { get; set; }
    public int MaxConsecutiveFailures { get; set; } = 5;
    public int ConsecutiveFailureCount { get; set; }
    public string State { get; set; } = "active";
    public DateTime? LastPolledAt { get; set; }
    public long? LastPollDurationMs { get; set; }
    public int? LastPollFileCount { get; set; }
    public DateTime? LastOverflowAt { get; set; }
    public int OverflowCount24h { get; set; }
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime? DeletedAt { get; set; }
    public List<MonitorJobBinding> Bindings { get; set; } = [];
}
