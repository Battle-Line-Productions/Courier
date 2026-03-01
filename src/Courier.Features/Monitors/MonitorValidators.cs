using FluentValidation;

namespace Courier.Features.Monitors;

public class CreateMonitorValidator : AbstractValidator<CreateMonitorRequest>
{
    public CreateMonitorValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Monitor name is required.")
            .MaximumLength(200).WithMessage("Monitor name must not exceed 200 characters.");

        RuleFor(x => x.WatchTarget)
            .NotEmpty().WithMessage("Watch target is required.");

        RuleFor(x => x.TriggerEvents)
            .GreaterThan(0).WithMessage("At least one trigger event must be specified.");

        RuleFor(x => x.PollingIntervalSec)
            .GreaterThanOrEqualTo(30).WithMessage("Polling interval must be at least 30 seconds.");

        RuleFor(x => x.StabilityWindowSec)
            .GreaterThanOrEqualTo(0).WithMessage("Stability window must be non-negative.");

        RuleFor(x => x.MaxConsecutiveFailures)
            .GreaterThan(0).WithMessage("Max consecutive failures must be greater than 0.");

        RuleFor(x => x.JobIds)
            .NotEmpty().WithMessage("At least one job binding is required.");
    }
}

public class UpdateMonitorValidator : AbstractValidator<UpdateMonitorRequest>
{
    public UpdateMonitorValidator()
    {
        RuleFor(x => x.Name)
            .MaximumLength(200).WithMessage("Monitor name must not exceed 200 characters.")
            .When(x => x.Name is not null);

        RuleFor(x => x.TriggerEvents)
            .GreaterThan(0).WithMessage("At least one trigger event must be specified.")
            .When(x => x.TriggerEvents.HasValue);

        RuleFor(x => x.PollingIntervalSec)
            .GreaterThanOrEqualTo(30).WithMessage("Polling interval must be at least 30 seconds.")
            .When(x => x.PollingIntervalSec.HasValue);

        RuleFor(x => x.StabilityWindowSec)
            .GreaterThanOrEqualTo(0).WithMessage("Stability window must be non-negative.")
            .When(x => x.StabilityWindowSec.HasValue);

        RuleFor(x => x.MaxConsecutiveFailures)
            .GreaterThan(0).WithMessage("Max consecutive failures must be greater than 0.")
            .When(x => x.MaxConsecutiveFailures.HasValue);

        RuleFor(x => x.JobIds)
            .NotEmpty().WithMessage("At least one job binding is required.")
            .When(x => x.JobIds is not null);
    }
}
