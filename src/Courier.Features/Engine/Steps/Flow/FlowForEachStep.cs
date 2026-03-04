using Courier.Domain.Engine;

namespace Courier.Features.Engine.Steps.Flow;

public class FlowForEachStep : IJobStep
{
    public string TypeKey => "flow.foreach";

    public Task<StepResult> ExecuteAsync(
        StepConfiguration config,
        JobContext context,
        CancellationToken cancellationToken)
    {
        // Iteration is handled by the engine's ExecuteForEachAsync.
        // This step only validates and returns Ok.
        return Task.FromResult(StepResult.Ok());
    }

    public Task<StepResult> ValidateAsync(StepConfiguration config)
    {
        if (!config.Has("source"))
            return Task.FromResult(StepResult.Fail("Missing required config: source"));
        return Task.FromResult(StepResult.Ok());
    }
}
