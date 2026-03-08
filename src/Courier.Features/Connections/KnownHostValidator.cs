using FluentValidation;

namespace Courier.Features.Connections;

public class CreateKnownHostValidator : AbstractValidator<CreateKnownHostRequest>
{
    public CreateKnownHostValidator()
    {
        RuleFor(x => x.KeyType)
            .NotEmpty().WithMessage("Key type is required.")
            .MaximumLength(50).WithMessage("Key type must not exceed 50 characters.");

        RuleFor(x => x.Fingerprint)
            .NotEmpty().WithMessage("Fingerprint is required.")
            .MaximumLength(500).WithMessage("Fingerprint must not exceed 500 characters.");
    }
}
