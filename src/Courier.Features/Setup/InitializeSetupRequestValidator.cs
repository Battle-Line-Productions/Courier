using FluentValidation;

namespace Courier.Features.Setup;

public class InitializeSetupRequestValidator : AbstractValidator<InitializeSetupRequest>
{
    public InitializeSetupRequestValidator()
    {
        RuleFor(x => x.Username).NotEmpty().MaximumLength(256);
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Email).MaximumLength(256).EmailAddress().When(x => !string.IsNullOrEmpty(x.Email));
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8);
        RuleFor(x => x.ConfirmPassword).Equal(x => x.Password).WithMessage("Passwords do not match.");
    }
}
