using Courier.Domain.Engine;

namespace Courier.Tests.JobEngine.Helpers;

public class SlowTestStep : IJobStep
{
    public string TypeKey => "test.slow";

    public async Task<StepResult> ExecuteAsync(StepConfiguration config, JobContext context, CancellationToken cancellationToken)
    {
        var delayMs = config.GetInt("delay_ms");
        await Task.Delay(delayMs, cancellationToken);
        return StepResult.Ok();
    }

    public Task<StepResult> ValidateAsync(StepConfiguration config) => Task.FromResult(StepResult.Ok());
}
