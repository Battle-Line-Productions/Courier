using FluentValidation;

namespace Courier.Features.Notifications;

public class CreateNotificationRuleValidator : AbstractValidator<CreateNotificationRuleRequest>
{
    private static readonly string[] ValidEntityTypes = ["job", "monitor", "chain"];
    private static readonly string[] ValidChannels = ["email", "webhook"];
    private static readonly string[] ValidEventTypes = ["job_completed", "job_failed", "job_cancelled", "job_timed_out", "step_failed"];

    public CreateNotificationRuleValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(200).WithMessage("Name must not exceed 200 characters.");

        RuleFor(x => x.EntityType)
            .NotEmpty().WithMessage("Entity type is required.")
            .Must(et => ValidEntityTypes.Contains(et))
            .WithMessage($"Entity type must be one of: {string.Join(", ", ValidEntityTypes)}.");

        RuleFor(x => x.Channel)
            .NotEmpty().WithMessage("Channel is required.")
            .Must(c => ValidChannels.Contains(c))
            .WithMessage($"Channel must be one of: {string.Join(", ", ValidChannels)}.");

        RuleFor(x => x.EventTypes)
            .NotEmpty().WithMessage("At least one event type is required.")
            .Must(et => et.All(e => ValidEventTypes.Contains(e)))
            .WithMessage($"Event types must be from: {string.Join(", ", ValidEventTypes)}.");

        RuleFor(x => x.ChannelConfig)
            .NotNull().WithMessage("Channel configuration is required.");
    }
}

public class UpdateNotificationRuleValidator : AbstractValidator<UpdateNotificationRuleRequest>
{
    private static readonly string[] ValidEntityTypes = ["job", "monitor", "chain"];
    private static readonly string[] ValidChannels = ["email", "webhook"];
    private static readonly string[] ValidEventTypes = ["job_completed", "job_failed", "job_cancelled", "job_timed_out", "step_failed"];

    public UpdateNotificationRuleValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Name is required.")
            .MaximumLength(200).WithMessage("Name must not exceed 200 characters.");

        RuleFor(x => x.EntityType)
            .NotEmpty().WithMessage("Entity type is required.")
            .Must(et => ValidEntityTypes.Contains(et))
            .WithMessage($"Entity type must be one of: {string.Join(", ", ValidEntityTypes)}.");

        RuleFor(x => x.Channel)
            .NotEmpty().WithMessage("Channel is required.")
            .Must(c => ValidChannels.Contains(c))
            .WithMessage($"Channel must be one of: {string.Join(", ", ValidChannels)}.");

        RuleFor(x => x.EventTypes)
            .NotEmpty().WithMessage("At least one event type is required.")
            .Must(et => et.All(e => ValidEventTypes.Contains(e)))
            .WithMessage($"Event types must be from: {string.Join(", ", ValidEventTypes)}.");

        RuleFor(x => x.ChannelConfig)
            .NotNull().WithMessage("Channel configuration is required.");
    }
}
