namespace Courier.Domain.Entities;

public class MonitorJobBinding
{
    public Guid Id { get; set; }
    public Guid MonitorId { get; set; }
    public Guid JobId { get; set; }
    public FileMonitor Monitor { get; set; } = null!;
    public Job Job { get; set; } = null!;
}
