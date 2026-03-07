using System.Text.Json;
using Courier.Domain.Engine;

namespace Courier.Tests.JobEngine.Helpers;

public class OutputTestStep : IJobStep
{
    public string TypeKey => "test.output";

    public Task<StepResult> ExecuteAsync(StepConfiguration config, JobContext context, CancellationToken cancellationToken)
    {
        if (!config.Has("outputs"))
            return Task.FromResult(StepResult.Ok());

        var outputsJson = config.GetString("outputs");
        var outputs = JsonSerializer.Deserialize<Dictionary<string, object>>(outputsJson)
            ?? new Dictionary<string, object>();

        return Task.FromResult(StepResult.Ok(outputs: outputs));
    }

    public Task<StepResult> ValidateAsync(StepConfiguration config) => Task.FromResult(StepResult.Ok());
}
