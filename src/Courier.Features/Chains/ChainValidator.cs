using FluentValidation;

namespace Courier.Features.Chains;

public class CreateChainValidator : AbstractValidator<CreateChainRequest>
{
    public CreateChainValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Chain name is required.")
            .MaximumLength(200).WithMessage("Chain name must not exceed 200 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(2000).WithMessage("Description must not exceed 2000 characters.");
    }
}

public class UpdateChainValidator : AbstractValidator<UpdateChainRequest>
{
    public UpdateChainValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Chain name is required.")
            .MaximumLength(200).WithMessage("Chain name must not exceed 200 characters.");

        RuleFor(x => x.Description)
            .MaximumLength(2000).WithMessage("Description must not exceed 2000 characters.");
    }
}
