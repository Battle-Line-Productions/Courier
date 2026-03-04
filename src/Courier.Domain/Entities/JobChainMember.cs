namespace Courier.Domain.Entities;

public class JobChainMember
{
    public Guid Id { get; set; }
    public Guid ChainId { get; set; }
    public Guid JobId { get; set; }
    public int ExecutionOrder { get; set; }
    public Guid? DependsOnMemberId { get; set; }
    public bool RunOnUpstreamFailure { get; set; }

    public JobChain Chain { get; set; } = null!;
    public Job Job { get; set; } = null!;
    public JobChainMember? DependsOnMember { get; set; }
}
