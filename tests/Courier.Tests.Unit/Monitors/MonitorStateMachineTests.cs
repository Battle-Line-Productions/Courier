using Courier.Domain.Engine;
using Courier.Domain.Enums;
using Shouldly;

namespace Courier.Tests.Unit.Monitors;

public class MonitorStateMachineTests
{
    [Theory]
    [InlineData(MonitorState.Active, MonitorState.Paused)]
    [InlineData(MonitorState.Active, MonitorState.Disabled)]
    [InlineData(MonitorState.Active, MonitorState.Error)]
    [InlineData(MonitorState.Paused, MonitorState.Active)]
    [InlineData(MonitorState.Paused, MonitorState.Disabled)]
    [InlineData(MonitorState.Disabled, MonitorState.Active)]
    [InlineData(MonitorState.Error, MonitorState.Active)]
    public void ValidTransitions_ShouldSucceed(MonitorState from, MonitorState to)
    {
        MonitorStateMachine.CanTransition(from, to).ShouldBeTrue($"{from} -> {to} should be valid");
    }

    [Theory]
    [InlineData(MonitorState.Active, MonitorState.Active)]
    [InlineData(MonitorState.Paused, MonitorState.Paused)]
    [InlineData(MonitorState.Paused, MonitorState.Error)]
    [InlineData(MonitorState.Disabled, MonitorState.Paused)]
    [InlineData(MonitorState.Disabled, MonitorState.Disabled)]
    [InlineData(MonitorState.Disabled, MonitorState.Error)]
    [InlineData(MonitorState.Error, MonitorState.Paused)]
    [InlineData(MonitorState.Error, MonitorState.Disabled)]
    [InlineData(MonitorState.Error, MonitorState.Error)]
    public void InvalidTransitions_ShouldFail(MonitorState from, MonitorState to)
    {
        MonitorStateMachine.CanTransition(from, to).ShouldBeFalse($"{from} -> {to} should be invalid");
    }

    [Theory]
    [InlineData(MonitorState.Active, MonitorState.Paused)]
    [InlineData(MonitorState.Paused, MonitorState.Active)]
    [InlineData(MonitorState.Disabled, MonitorState.Active)]
    [InlineData(MonitorState.Error, MonitorState.Active)]
    public void Transition_ValidTransition_ReturnsTargetState(MonitorState from, MonitorState to)
    {
        var result = MonitorStateMachine.Transition(from, to);
        result.ShouldBe(to);
    }

    [Theory]
    [InlineData(MonitorState.Disabled, MonitorState.Error)]
    [InlineData(MonitorState.Error, MonitorState.Disabled)]
    [InlineData(MonitorState.Paused, MonitorState.Error)]
    public void Transition_InvalidTransition_ThrowsInvalidOperationException(MonitorState from, MonitorState to)
    {
        Should.Throw<InvalidOperationException>(() => MonitorStateMachine.Transition(from, to));
    }
}
