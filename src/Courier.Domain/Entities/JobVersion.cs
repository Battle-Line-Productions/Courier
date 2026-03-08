namespace Courier.Domain.Entities;

public class JobVersion
{
    public Guid Id { get; set; }
    public Guid JobId { get; set; }
    public int VersionNumber { get; set; }
    public string ConfigSnapshot { get; set; } = "{}";
    public string? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; }

    public Job Job { get; set; } = null!;
}
