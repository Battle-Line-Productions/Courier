using Courier.Domain.Enums;

namespace Courier.Domain.Engine;

public static class MonitorStateMachine
{
    private static readonly Dictionary<MonitorState, HashSet<MonitorState>> ValidTransitions = new()
    {
        [MonitorState.Active]   = [MonitorState.Paused, MonitorState.Disabled, MonitorState.Error],
        [MonitorState.Paused]   = [MonitorState.Active, MonitorState.Disabled],
        [MonitorState.Disabled] = [MonitorState.Active],
        [MonitorState.Error]    = [MonitorState.Active],
    };

    public static bool CanTransition(MonitorState from, MonitorState to)
        => ValidTransitions.TryGetValue(from, out var targets) && targets.Contains(to);

    public static MonitorState Transition(MonitorState from, MonitorState to)
        => CanTransition(from, to)
            ? to
            : throw new InvalidOperationException($"Invalid monitor state transition: {from} -> {to}");
}
