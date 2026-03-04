using Courier.Domain.Engine;

namespace Courier.Features.Engine.Steps.Flow;

public class FlowElseStep : IJobStep
{
    public string TypeKey => "flow.else";

    public Task<StepResult> ExecuteAsync(
        StepConfiguration config,
        JobContext context,
        CancellationToken cancellationToken)
    {
        // Marker only — branching handled by the engine.
        return Task.FromResult(StepResult.Ok());
    }

    public Task<StepResult> ValidateAsync(StepConfiguration config)
        => Task.FromResult(StepResult.Ok());
}
