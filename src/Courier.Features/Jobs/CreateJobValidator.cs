using FluentValidation;

namespace Courier.Features.Jobs;

public class CreateJobValidator : AbstractValidator<CreateJobRequest>
{
    public CreateJobValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Job name is required.")
            .MaximumLength(200).WithMessage("Job name must not exceed 200 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(2000).WithMessage("Description must not exceed 2000 characters.");
    }
}
