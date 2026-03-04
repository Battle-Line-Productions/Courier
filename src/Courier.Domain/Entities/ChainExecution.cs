using Courier.Domain.Enums;

namespace Courier.Domain.Entities;

public class ChainExecution
{
    public Guid Id { get; set; }
    public Guid ChainId { get; set; }
    public string TriggeredBy { get; set; } = string.Empty;
    public ChainExecutionState State { get; set; } = ChainExecutionState.Pending;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime CreatedAt { get; set; }

    public JobChain Chain { get; set; } = null!;
    public List<JobExecution> JobExecutions { get; set; } = [];
}
