using Courier.Domain.Engine;
using Courier.Domain.Enums;
using Shouldly;

namespace Courier.Tests.Unit.Engine;

public class FailurePolicyUnitTests
{
    [Fact]
    public void GetBackoffDelay_Attempt0_ReturnsBaseSeconds()
    {
        var policy = new FailurePolicy { BackoffBaseSeconds = 2, BackoffMaxSeconds = 60 };
        policy.GetBackoffDelay(0).ShouldBe(TimeSpan.FromSeconds(2));
    }

    [Fact]
    public void GetBackoffDelay_Attempt1_ReturnsDoubleBase()
    {
        var policy = new FailurePolicy { BackoffBaseSeconds = 2, BackoffMaxSeconds = 60 };
        policy.GetBackoffDelay(1).ShouldBe(TimeSpan.FromSeconds(4));
    }

    [Fact]
    public void GetBackoffDelay_Attempt3_Returns8xBase()
    {
        var policy = new FailurePolicy { BackoffBaseSeconds = 2, BackoffMaxSeconds = 60 };
        // 2 * (1 << 3) = 2 * 8 = 16
        policy.GetBackoffDelay(3).ShouldBe(TimeSpan.FromSeconds(16));
    }

    [Fact]
    public void GetBackoffDelay_LargeAttempt_CappedAtMaxSeconds()
    {
        var policy = new FailurePolicy { BackoffBaseSeconds = 2, BackoffMaxSeconds = 60 };
        // 2 * (1 << 10) = 2048, capped at 60
        policy.GetBackoffDelay(10).ShouldBe(TimeSpan.FromSeconds(60));
    }

    [Fact]
    public void DefaultPolicy_TypeIsStop_MaxRetries3()
    {
        var policy = new FailurePolicy();
        policy.Type.ShouldBe(FailurePolicyType.Stop);
        policy.MaxRetries.ShouldBe(3);
    }

    [Fact]
    public void DefaultPolicy_BackoffBase1_BackoffMax60()
    {
        var policy = new FailurePolicy();
        policy.BackoffBaseSeconds.ShouldBe(1);
        policy.BackoffMaxSeconds.ShouldBe(60);
    }
}
