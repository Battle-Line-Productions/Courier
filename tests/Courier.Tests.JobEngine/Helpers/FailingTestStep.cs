using Courier.Domain.Engine;

namespace Courier.Tests.JobEngine.Helpers;

public class FailingTestStep : IJobStep
{
    public string TypeKey => "test.fail";

    public Task<StepResult> ExecuteAsync(StepConfiguration config, JobContext context, CancellationToken cancellationToken)
    {
        var message = config.GetStringOrDefault("message", "intentional failure");
        return Task.FromResult(StepResult.Fail(message ?? "intentional failure"));
    }

    public Task<StepResult> ValidateAsync(StepConfiguration config) => Task.FromResult(StepResult.Ok());
}
