namespace Courier.Domain.Enums;

public enum FailurePolicyType
{
    Stop,
    RetryStep,
    RetryJob,
    SkipAndContinue
}
