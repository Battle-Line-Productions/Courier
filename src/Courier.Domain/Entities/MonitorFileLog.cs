namespace Courier.Domain.Entities;

public class MonitorFileLog
{
    public Guid Id { get; set; }
    public Guid MonitorId { get; set; }
    public string FilePath { get; set; } = string.Empty;
    public long FileSize { get; set; }
    public string? FileHash { get; set; }
    public DateTime LastModified { get; set; }
    public DateTime TriggeredAt { get; set; }
    public Guid? ExecutionId { get; set; }
    public FileMonitor Monitor { get; set; } = null!;
}
