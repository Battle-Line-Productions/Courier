namespace Courier.Domain.Entities;

public class JobDependency
{
    public Guid Id { get; set; }
    public Guid UpstreamJobId { get; set; }
    public Guid DownstreamJobId { get; set; }
    public bool RunOnFailure { get; set; }

    public Job UpstreamJob { get; set; } = null!;
    public Job DownstreamJob { get; set; } = null!;
}
