using Courier.Domain.Engine;

namespace Courier.Features.Engine.Steps.Flow;

public class FlowEndStep : IJobStep
{
    public string TypeKey => "flow.end";

    public Task<StepResult> ExecuteAsync(
        StepConfiguration config,
        JobContext context,
        CancellationToken cancellationToken)
    {
        // Marker only — block termination handled by the parser.
        return Task.FromResult(StepResult.Ok());
    }

    public Task<StepResult> ValidateAsync(StepConfiguration config)
        => Task.FromResult(StepResult.Ok());
}
