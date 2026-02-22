using Courier.Domain.Enums;

namespace Courier.Domain.Engine;

public static class StepStateMachine
{
    private static readonly Dictionary<StepExecutionState, HashSet<StepExecutionState>> ValidTransitions = new()
    {
        [StepExecutionState.Pending]   = [StepExecutionState.Running, StepExecutionState.Skipped],
        [StepExecutionState.Running]   = [StepExecutionState.Completed, StepExecutionState.Failed],
        [StepExecutionState.Completed] = [],
        [StepExecutionState.Failed]    = [],
        [StepExecutionState.Skipped]   = [],
    };

    public static bool CanTransition(StepExecutionState from, StepExecutionState to)
        => ValidTransitions.TryGetValue(from, out var targets) && targets.Contains(to);

    public static StepExecutionState Transition(StepExecutionState from, StepExecutionState to)
        => CanTransition(from, to)
            ? to
            : throw new InvalidOperationException($"Invalid step state transition: {from} -> {to}");
}
