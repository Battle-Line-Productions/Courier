namespace Courier.Domain.Engine;

public interface IJobStep
{
    string TypeKey { get; }

    Task<StepResult> ExecuteAsync(
        StepConfiguration config,
        JobContext context,
        CancellationToken cancellationToken);

    Task<StepResult> ValidateAsync(StepConfiguration config);
}
