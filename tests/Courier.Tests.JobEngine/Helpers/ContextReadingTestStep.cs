using Courier.Domain.Engine;

namespace Courier.Tests.JobEngine.Helpers;

public class ContextReadingTestStep : IJobStep
{
    public string TypeKey => "test.context_reader";

    public Task<StepResult> ExecuteAsync(StepConfiguration config, JobContext context, CancellationToken cancellationToken)
    {
        var keys = config.GetStringArray("keys");
        var outputs = new Dictionary<string, object>();

        foreach (var key in keys)
        {
            if (context.TryGet<object>(key, out var value) && value is not null)
                outputs[key] = value;
            else
                outputs[key] = "__missing__";
        }

        return Task.FromResult(StepResult.Ok(outputs: outputs));
    }

    public Task<StepResult> ValidateAsync(StepConfiguration config) => Task.FromResult(StepResult.Ok());
}
