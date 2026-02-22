namespace Courier.Domain.Enums;

public enum JobExecutionState
{
    Created,
    Queued,
    Running,
    Paused,
    Completed,
    Failed,
    Cancelled
}
