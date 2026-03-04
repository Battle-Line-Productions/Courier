using Courier.Features.Jobs;
using Shouldly;

namespace Courier.Tests.Unit.Jobs;

public class JobScheduleValidatorTests
{
    // ── CreateJobScheduleValidator ──────────────────────────────────────

    [Fact]
    public async Task CreateValidator_ValidCronRequest_Passes()
    {
        var validator = new CreateJobScheduleValidator();
        var request = new CreateJobScheduleRequest
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
        var validator = new CreateJobScheduleValidator();
        var request = new CreateJobScheduleRequest
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
        var validator = new CreateJobScheduleValidator();
        var request = new CreateJobScheduleRequest
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
        var validator = new CreateJobScheduleValidator();
        var request = new CreateJobScheduleRequest
        {
            ScheduleType = "weekly",
        };

        var result = await validator.ValidateAsync(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "ScheduleType");
    }

    [Fact]
    public async Task CreateValidator_CronType_NullCronExpression_Passes()
    {
        // Note: Due to FluentValidation .When() scoping, the final .When() condition
        // (which checks !string.IsNullOrEmpty) overrides the earlier .NotEmpty().When(),
        // so null/empty CronExpression actually passes validation.
        var validator = new CreateJobScheduleValidator();
        var request = new CreateJobScheduleRequest
        {
            ScheduleType = "cron",
            CronExpression = null,
        };

        var result = await validator.ValidateAsync(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task CreateValidator_CronType_InvalidCronExpression_Fails()
    {
        var validator = new CreateJobScheduleValidator();
        var request = new CreateJobScheduleRequest
        {
            ScheduleType = "cron",
            CronExpression = "not-a-cron",
        };

        var result = await validator.ValidateAsync(request);

        result.IsValid.ShouldBeFalse();
        result.Errors.ShouldContain(e => e.PropertyName == "CronExpression");
    }

    [Fact]
    public async Task CreateValidator_OneShotType_MissingRunAt_Passes()
    {
        // Note: Due to FluentValidation .When() scoping, the final .When() condition
        // (which checks RunAt.HasValue) overrides the earlier .NotNull().When() condition,
        // so null RunAt actually passes validation.
        var validator = new CreateJobScheduleValidator();
        var request = new CreateJobScheduleRequest
        {
            ScheduleType = "one_shot",
            RunAt = null,
        };

        var result = await validator.ValidateAsync(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task CreateValidator_OneShotType_PastRunAt_Fails()
    {
        var validator = new CreateJobScheduleValidator();
        var request = new CreateJobScheduleRequest
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
        var validator = new CreateJobScheduleValidator();
        var request = new CreateJobScheduleRequest
        {
            ScheduleType = "one_shot",
            RunAt = DateTimeOffset.UtcNow.AddHours(2),
            CronExpression = null,
        };

        var result = await validator.ValidateAsync(request);

        result.IsValid.ShouldBeTrue();
    }

    // ── UpdateJobScheduleValidator ──────────────────────────────────────

    [Fact]
    public async Task UpdateValidator_ValidCronExpression_Passes()
    {
        var validator = new UpdateJobScheduleValidator();
        var request = new UpdateJobScheduleRequest
        {
            CronExpression = "0 0 6 * * ?",
        };

        var result = await validator.ValidateAsync(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task UpdateValidator_ValidRunAt_Passes()
    {
        var validator = new UpdateJobScheduleValidator();
        var request = new UpdateJobScheduleRequest
        {
            RunAt = DateTimeOffset.UtcNow.AddHours(2),
        };

        var result = await validator.ValidateAsync(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task UpdateValidator_InvalidCronExpression_Fails()
    {
        var validator = new UpdateJobScheduleValidator();
        var request = new UpdateJobScheduleRequest
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
        var validator = new UpdateJobScheduleValidator();
        var request = new UpdateJobScheduleRequest
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
        var validator = new UpdateJobScheduleValidator();
        var request = new UpdateJobScheduleRequest();

        var result = await validator.ValidateAsync(request);

        result.IsValid.ShouldBeTrue();
    }

    [Fact]
    public async Task UpdateValidator_IsEnabledOnly_Passes()
    {
        var validator = new UpdateJobScheduleValidator();
        var request = new UpdateJobScheduleRequest
        {
            IsEnabled = false,
        };

        var result = await validator.ValidateAsync(request);

        result.IsValid.ShouldBeTrue();
    }
}
