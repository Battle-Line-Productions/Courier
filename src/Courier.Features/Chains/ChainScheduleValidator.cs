using FluentValidation;
using Quartz;

namespace Courier.Features.Chains;

public class CreateChainScheduleValidator : AbstractValidator<CreateChainScheduleRequest>
{
    public CreateChainScheduleValidator()
    {
        RuleFor(x => x.ScheduleType)
            .NotEmpty().WithMessage("Schedule type is required.")
            .Must(t => t is "cron" or "one_shot").WithMessage("Schedule type must be 'cron' or 'one_shot'.");

        RuleFor(x => x.CronExpression)
            .NotEmpty().When(x => x.ScheduleType == "cron").WithMessage("Cron expression is required for cron schedules.")
            .Must(expr => CronExpression.IsValidExpression(expr!))
            .When(x => x.ScheduleType == "cron" && !string.IsNullOrEmpty(x.CronExpression))
            .WithMessage("Invalid cron expression.");

        RuleFor(x => x.RunAt)
            .NotNull().When(x => x.ScheduleType == "one_shot").WithMessage("RunAt is required for one-shot schedules.")
            .Must(r => r > DateTimeOffset.UtcNow)
            .When(x => x.ScheduleType == "one_shot" && x.RunAt.HasValue)
            .WithMessage("RunAt must be in the future.");
    }
}

public class UpdateChainScheduleValidator : AbstractValidator<UpdateChainScheduleRequest>
{
    public UpdateChainScheduleValidator()
    {
        RuleFor(x => x.CronExpression)
            .Must(expr => CronExpression.IsValidExpression(expr!))
            .When(x => !string.IsNullOrEmpty(x.CronExpression))
            .WithMessage("Invalid cron expression.");

        RuleFor(x => x.RunAt)
            .Must(r => r > DateTimeOffset.UtcNow)
            .When(x => x.RunAt.HasValue)
            .WithMessage("RunAt must be in the future.");
    }
}
