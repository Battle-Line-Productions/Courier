using Courier.Domain.Engine;

namespace Courier.Features.Engine.Steps.Flow;

public class FlowIfStep : IJobStep
{
    public string TypeKey => "flow.if";

    public Task<StepResult> ExecuteAsync(
        StepConfiguration config,
        JobContext context,
        CancellationToken cancellationToken)
    {
        // Branching is handled by the engine's ExecuteIfElseAsync.
        // This step only validates and returns Ok.
        return Task.FromResult(StepResult.Ok());
    }

    public Task<StepResult> ValidateAsync(StepConfiguration config)
    {
        if (!config.Has("left"))
            return Task.FromResult(StepResult.Fail("Missing required config: left"));
        if (!config.Has("operator"))
            return Task.FromResult(StepResult.Fail("Missing required config: operator"));

        var op = config.GetString("operator").ToLowerInvariant();
        if (op != "exists" && !config.Has("right"))
            return Task.FromResult(StepResult.Fail("Missing required config: right (required for non-exists operators)"));

        return Task.FromResult(StepResult.Ok());
    }
}
