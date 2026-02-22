using Courier.Domain.Enums;

namespace Courier.Domain.Entities;

public class StepExecution
{
    public Guid Id { get; set; }
    public Guid JobExecutionId { get; set; }
    public Guid JobStepId { get; set; }
    public int StepOrder { get; set; }
    public StepExecutionState State { get; set; } = StepExecutionState.Pending;
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
    public long? DurationMs { get; set; }
    public long? BytesProcessed { get; set; }
    public string? OutputData { get; set; }
    public string? ErrorMessage { get; set; }
    public string? ErrorStackTrace { get; set; }
    public int RetryAttempt { get; set; }
    public DateTime CreatedAt { get; set; }

    public JobExecution JobExecution { get; set; } = null!;
    public JobStep JobStep { get; set; } = null!;
}
