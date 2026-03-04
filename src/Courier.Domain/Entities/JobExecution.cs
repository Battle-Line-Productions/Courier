using Courier.Domain.Enums;

namespace Courier.Domain.Entities;

public class JobExecution
{
    public Guid Id { get; set; }
    public Guid JobId { get; set; }
    public int JobVersionNumber { get; set; } = 1;
    public string TriggeredBy { get; set; } = string.Empty;
    public JobExecutionState State { get; set; } = JobExecutionState.Created;
    public DateTime? QueuedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public DateTime? PausedAt { get; set; }
    public string? PausedBy { get; set; }
    public DateTime? CancelledAt { get; set; }
    public string? CancelledBy { get; set; }
    public string? CancelReason { get; set; }
    public string? RequestedState { get; set; }
    public string ContextSnapshot { get; set; } = "{}";
    public DateTime CreatedAt { get; set; }

    public Guid? ChainExecutionId { get; set; }

    public Job Job { get; set; } = null!;
    public ChainExecution? ChainExecution { get; set; }
    public List<StepExecution> StepExecutions { get; set; } = [];
}
