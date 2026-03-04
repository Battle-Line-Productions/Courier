using Courier.Domain.Enums;

namespace Courier.Domain.Engine;

public static class JobStateMachine
{
    private static readonly Dictionary<JobExecutionState, HashSet<JobExecutionState>> ValidTransitions = new()
    {
        [JobExecutionState.Created]   = [JobExecutionState.Queued],
        [JobExecutionState.Queued]    = [JobExecutionState.Running, JobExecutionState.Cancelled],
        [JobExecutionState.Running]   = [JobExecutionState.Completed, JobExecutionState.Failed, JobExecutionState.Paused, JobExecutionState.Cancelled],
        [JobExecutionState.Paused]    = [JobExecutionState.Running, JobExecutionState.Cancelled, JobExecutionState.Queued],
        [JobExecutionState.Completed] = [],
        [JobExecutionState.Failed]    = [],
        [JobExecutionState.Cancelled] = [],
    };

    public static bool CanTransition(JobExecutionState from, JobExecutionState to)
        => ValidTransitions.TryGetValue(from, out var targets) && targets.Contains(to);

    public static JobExecutionState Transition(JobExecutionState from, JobExecutionState to)
        => CanTransition(from, to)
            ? to
            : throw new InvalidOperationException($"Invalid job state transition: {from} -> {to}");
}
