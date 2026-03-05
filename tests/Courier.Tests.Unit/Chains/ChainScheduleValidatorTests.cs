using Courier.Features.Chains;
using Shouldly;

namespace Courier.Tests.Unit.Chains;

public class ChainScheduleValidatorTests
{
    // ── CreateChainScheduleValidator ──────────────────────────────────────

    [Fact]
    public async Task CreateValidator_ValidCronRequest_Passes()
    {
        var validator = new CreateChainScheduleValidator();
        var request = new CreateChainScheduleRequest
        {
            ScheduleType = "cron",
            CronExpression = "0 0 3 * * ?",
        };

        var result = await validator.ValidateAsync(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task CreateValidator_ValidOneShotRequest_Passes()
    {
        var validator = new CreateChainScheduleValidator();
        var request = new CreateChainScheduleRequest
        {
            ScheduleType = "one_shot",
            RunAt = DateTimeOffset.UtcNow.AddHours(2),
        };

        var result = await validator.ValidateAsync(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task CreateValidator_EmptyScheduleType_Fails()
    {
        var validator = new CreateChainScheduleValidator();
        var request = new CreateChainScheduleRequest
        {
            ScheduleType = "",
        };

        var result = await validator.ValidateAsync(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "ScheduleType");
    }

    [Fact]
    public async Task CreateValidator_InvalidScheduleType_Fails()
    {
        var validator = new CreateChainScheduleValidator();
        var request = new CreateChainScheduleRequest
        {
            ScheduleType = "weekly",
        };

        var result = await validator.ValidateAsync(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "ScheduleType");
    }

    [Fact]
    public async Task CreateValidator_CronType_InvalidCronExpression_Fails()
    {
        var validator = new CreateChainScheduleValidator();
        var request = new CreateChainScheduleRequest
        {
            ScheduleType = "cron",
            CronExpression = "not-a-cron",
        };

        var result = await validator.ValidateAsync(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "CronExpression");
    }

    [Fact]
    public async Task CreateValidator_OneShotType_PastRunAt_Fails()
    {
        var validator = new CreateChainScheduleValidator();
        var request = new CreateChainScheduleRequest
        {
            ScheduleType = "one_shot",
            RunAt = DateTimeOffset.UtcNow.AddHours(-1),
        };

        var result = await validator.ValidateAsync(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "RunAt");
    }

    [Fact]
    public async Task CreateValidator_OneShotType_NoCronExpression_Passes()
    {
        var validator = new CreateChainScheduleValidator();
        var request = new CreateChainScheduleRequest
        {
            ScheduleType = "one_shot",
            RunAt = DateTimeOffset.UtcNow.AddHours(2),
            CronExpression = null,
        };

        var result = await validator.ValidateAsync(request);

        result.IsValid.ShouldBeTrue();
    }

    // ── UpdateChainScheduleValidator ──────────────────────────────────────

    [Fact]
    public async Task UpdateValidator_ValidCronExpression_Passes()
    {
        var validator = new UpdateChainScheduleValidator();
        var request = new UpdateChainScheduleRequest
        {
            CronExpression = "0 0 6 * * ?",
        };

        var result = await validator.ValidateAsync(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task UpdateValidator_ValidRunAt_Passes()
    {
        var validator = new UpdateChainScheduleValidator();
        var request = new UpdateChainScheduleRequest
        {
            RunAt = DateTimeOffset.UtcNow.AddHours(2),
        };

        var result = await validator.ValidateAsync(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task UpdateValidator_InvalidCronExpression_Fails()
    {
        var validator = new UpdateChainScheduleValidator();
        var request = new UpdateChainScheduleRequest
        {
            CronExpression = "not-valid-cron",
        };

        var result = await validator.ValidateAsync(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "CronExpression");
    }

    [Fact]
    public async Task UpdateValidator_PastRunAt_Fails()
    {
        var validator = new UpdateChainScheduleValidator();
        var request = new UpdateChainScheduleRequest
        {
            RunAt = DateTimeOffset.UtcNow.AddHours(-1),
        };

        var result = await validator.ValidateAsync(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "RunAt");
    }

    [Fact]
    public async Task UpdateValidator_EmptyRequest_Passes()
    {
        var validator = new UpdateChainScheduleValidator();
        var request = new UpdateChainScheduleRequest();

        var result = await validator.ValidateAsync(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task UpdateValidator_IsEnabledOnly_Passes()
    {
        var validator = new UpdateChainScheduleValidator();
        var request = new UpdateChainScheduleRequest
        {
            IsEnabled = false,
        };

        var result = await validator.ValidateAsync(request);

        result.IsValid.ShouldBeTrue();
    }
}
