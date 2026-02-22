using Courier.Domain.Enums;

namespace Courier.Domain.Engine;

public record FailurePolicy
{
    public FailurePolicyType Type { get; init; } = FailurePolicyType.Stop;
    public int MaxRetries { get; init; } = 3;
    public int BackoffBaseSeconds { get; init; } = 1;
    public int BackoffMaxSeconds { get; init; } = 60;

    public TimeSpan GetBackoffDelay(int attempt)
    {
        var seconds = Math.Min(BackoffBaseSeconds * (1 << attempt), BackoffMaxSeconds);
        return TimeSpan.FromSeconds(seconds);
    }
}
