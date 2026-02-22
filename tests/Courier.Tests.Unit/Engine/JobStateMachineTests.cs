using Courier.Domain.Engine;
using Courier.Domain.Enums;
using Shouldly;

namespace Courier.Tests.Unit.Engine;

public class JobStateMachineTests
{
    [Theory]
    [InlineData(JobExecutionState.Created, JobExecutionState.Queued)]
    [InlineData(JobExecutionState.Queued, JobExecutionState.Running)]
    [InlineData(JobExecutionState.Running, JobExecutionState.Completed)]
    [InlineData(JobExecutionState.Running, JobExecutionState.Failed)]
    [InlineData(JobExecutionState.Running, JobExecutionState.Paused)]
    [InlineData(JobExecutionState.Paused, JobExecutionState.Running)]
    [InlineData(JobExecutionState.Queued, JobExecutionState.Cancelled)]
    [InlineData(JobExecutionState.Running, JobExecutionState.Cancelled)]
    [InlineData(JobExecutionState.Paused, JobExecutionState.Cancelled)]
    public void ValidTransitions_ShouldSucceed(JobExecutionState from, JobExecutionState to)
    {
        JobStateMachine.CanTransition(from, to).ShouldBeTrue($"{from} -> {to} should be valid");
    }

    [Theory]
    [InlineData(JobExecutionState.Created, JobExecutionState.Completed)]
    [InlineData(JobExecutionState.Completed, JobExecutionState.Running)]
    [InlineData(JobExecutionState.Failed, JobExecutionState.Running)]
    [InlineData(JobExecutionState.Cancelled, JobExecutionState.Running)]
    [InlineData(JobExecutionState.Created, JobExecutionState.Running)]
    public void InvalidTransitions_ShouldFail(JobExecutionState from, JobExecutionState to)
    {
        JobStateMachine.CanTransition(from, to).ShouldBeFalse($"{from} -> {to} should be invalid");
    }

    [Fact]
    public void Transition_InvalidTransition_ThrowsInvalidOperationException()
    {
        Should.Throw<InvalidOperationException>(() =>
            JobStateMachine.Transition(JobExecutionState.Completed, JobExecutionState.Running));
    }

    [Fact]
    public void Transition_ValidTransition_ReturnsNewState()
    {
        var result = JobStateMachine.Transition(JobExecutionState.Created, JobExecutionState.Queued);
        result.ShouldBe(JobExecutionState.Queued);
    }
}
