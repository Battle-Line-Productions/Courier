using Courier.Domain.Engine;
using Courier.Domain.Enums;
using Shouldly;

namespace Courier.Tests.Unit.Engine;

public class FailurePolicyTests
{
    // --- GetBackoffDelay ---

    [Fact]
    public void GetBackoffDelay_Attempt0_ReturnsBaseDelay()
    {
        // Arrange
        var policy = new FailurePolicy { BackoffBaseSeconds = 5, BackoffMaxSeconds = 120 };

        // Act
        var delay = policy.GetBackoffDelay(0);

        // Assert
        delay.ShouldBe(TimeSpan.FromSeconds(5));
    }

    [Theory]
    [InlineData(0, 2)] // 2 * (1 << 0) = 2
    [InlineData(1, 4)] // 2 * (1 << 1) = 4
    [InlineData(2, 8)] // 2 * (1 << 2) = 8
    [InlineData(3, 16)] // 2 * (1 << 3) = 16
    [InlineData(4, 32)] // 2 * (1 << 4) = 32
    public void GetBackoffDelay_ExponentialGrowth_CorrectValues(int attempt, int expectedSeconds)
    {
        // Arrange
        var policy = new FailurePolicy { BackoffBaseSeconds = 2, BackoffMaxSeconds = 600 };

        // Act
        var delay = policy.GetBackoffDelay(attempt);

        // Assert
        delay.ShouldBe(TimeSpan.FromSeconds(expectedSeconds));
    }

    [Fact]
    public void GetBackoffDelay_ExceedsMax_CappedAtMaxSeconds()
    {
        // Arrange
        var policy = new FailurePolicy { BackoffBaseSeconds = 10, BackoffMaxSeconds = 60 };

        // Act — 10 * (1 << 5) = 320, should be capped at 60
        var delay = policy.GetBackoffDelay(5);

        // Assert
        delay.ShouldBe(TimeSpan.FromSeconds(60));
    }

    [Fact]
    public void GetBackoffDelay_MaxSecondsSameAsBase_AlwaysReturnsMax()
    {
        // Arrange
        var policy = new FailurePolicy { BackoffBaseSeconds = 30, BackoffMaxSeconds = 30 };

        // Act
        var delay0 = policy.GetBackoffDelay(0);
        var delay5 = policy.GetBackoffDelay(5);

        // Assert
        delay0.ShouldBe(TimeSpan.FromSeconds(30));
        delay5.ShouldBe(TimeSpan.FromSeconds(30));
    }

    [Fact]
    public void GetBackoffDelay_VeryLargeAttempt_DoesNotOverflow()
    {
        // Arrange
        var policy = new FailurePolicy { BackoffBaseSeconds = 1, BackoffMaxSeconds = 60 };

        // Act — attempt 20: 1 * (1 << 20) = 1048576, capped at 60
        var delay = policy.GetBackoffDelay(20);

        // Assert
        delay.ShouldBe(TimeSpan.FromSeconds(60));
    }

    // --- Default values ---

    [Fact]
    public void DefaultPolicy_HasExpectedDefaults()
    {
        // Act
        var policy = new FailurePolicy();

        // Assert
        policy.Type.ShouldBe(FailurePolicyType.Stop);
        policy.MaxRetries.ShouldBe(3);
        policy.BackoffBaseSeconds.ShouldBe(1);
        policy.BackoffMaxSeconds.ShouldBe(60);
    }

    [Fact]
    public void DefaultPolicy_GetBackoffDelay_Attempt0_Returns1Second()
    {
        // Arrange
        var policy = new FailurePolicy();

        // Act
        var delay = policy.GetBackoffDelay(0);

        // Assert
        delay.ShouldBe(TimeSpan.FromSeconds(1));
    }

    // --- Zero MaxRetries ---

    [Fact]
    public void Policy_ZeroMaxRetries_IsValid()
    {
        // Arrange
        var policy = new FailurePolicy { MaxRetries = 0, Type = FailurePolicyType.RetryStep };

        // Assert
        policy.MaxRetries.ShouldBe(0);
        policy.Type.ShouldBe(FailurePolicyType.RetryStep);
    }

    // --- FailurePolicyType variants ---

    [Theory]
    [InlineData(FailurePolicyType.Stop)]
    [InlineData(FailurePolicyType.RetryStep)]
    [InlineData(FailurePolicyType.RetryJob)]
    [InlineData(FailurePolicyType.SkipAndContinue)]
    public void Policy_CanBeCreatedWithAllTypes(FailurePolicyType type)
    {
        // Act
        var policy = new FailurePolicy { Type = type };

        // Assert
        policy.Type.ShouldBe(type);
    }
}
