using Courier.Domain.Engine;
using Courier.Domain.Enums;
using Shouldly;

namespace Courier.Tests.Unit.Engine;

public class StepStateMachineTests
{
    [Theory]
    [InlineData(StepExecutionState.Pending, StepExecutionState.Running)]
    [InlineData(StepExecutionState.Pending, StepExecutionState.Skipped)]
    [InlineData(StepExecutionState.Running, StepExecutionState.Completed)]
    [InlineData(StepExecutionState.Running, StepExecutionState.Failed)]
    public void ValidTransitions_ShouldSucceed(StepExecutionState from, StepExecutionState to)
    {
        StepStateMachine.CanTransition(from, to).ShouldBeTrue($"{from} -> {to} should be valid");
    }

    [Theory]
    [InlineData(StepExecutionState.Completed, StepExecutionState.Running)]
    [InlineData(StepExecutionState.Failed, StepExecutionState.Running)]
    [InlineData(StepExecutionState.Skipped, StepExecutionState.Running)]
    [InlineData(StepExecutionState.Pending, StepExecutionState.Completed)]
    public void InvalidTransitions_ShouldFail(StepExecutionState from, StepExecutionState to)
    {
        StepStateMachine.CanTransition(from, to).ShouldBeFalse($"{from} -> {to} should be invalid");
    }
}
