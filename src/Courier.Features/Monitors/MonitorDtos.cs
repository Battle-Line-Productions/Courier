namespace Courier.Features.Monitors;

public record MonitorDto
{
    public Guid Id { get; init; }
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required string WatchTarget { get; init; }
    public int TriggerEvents { get; init; }
    public string? FilePatterns { get; init; }
    public int PollingIntervalSec { get; init; }
    public int StabilityWindowSec { get; init; }
    public bool BatchMode { get; init; }
    public int MaxConsecutiveFailures { get; init; }
    public int ConsecutiveFailureCount { get; init; }
    public required string State { get; init; }
    public DateTime? LastPolledAt { get; init; }
    public DateTime CreatedAt { get; init; }
    public DateTime UpdatedAt { get; init; }
    public List<MonitorJobBindingDto> Bindings { get; init; } = [];
}

public record MonitorJobBindingDto
{
    public Guid Id { get; init; }
    public Guid JobId { get; init; }
    public string? JobName { get; init; }
}

public record MonitorFileLogDto
{
    public Guid Id { get; init; }
    public string FilePath { get; init; } = string.Empty;
    public long FileSize { get; init; }
    public string? FileHash { get; init; }
    public DateTime LastModified { get; init; }
    public DateTime TriggeredAt { get; init; }
    public Guid? ExecutionId { get; init; }
}

public record CreateMonitorRequest
{
    public required string Name { get; init; }
    public string? Description { get; init; }
    public required string WatchTarget { get; init; }
    public int TriggerEvents { get; init; }
    public string? FilePatterns { get; init; }
    public int PollingIntervalSec { get; init; } = 60;
    public int StabilityWindowSec { get; init; }
    public bool BatchMode { get; init; }
    public int MaxConsecutiveFailures { get; init; } = 5;
    public required List<Guid> JobIds { get; init; }
}

public record UpdateMonitorRequest
{
    public string? Name { get; init; }
    public string? Description { get; init; }
    public string? WatchTarget { get; init; }
    public int? TriggerEvents { get; init; }
    public string? FilePatterns { get; init; }
    public int? PollingIntervalSec { get; init; }
    public int? StabilityWindowSec { get; init; }
    public bool? BatchMode { get; init; }
    public int? MaxConsecutiveFailures { get; init; }
    public List<Guid>? JobIds { get; init; }
}
