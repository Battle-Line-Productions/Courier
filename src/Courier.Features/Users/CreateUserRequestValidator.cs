using FluentValidation;

namespace Courier.Features.Users;

public class CreateUserRequestValidator : AbstractValidator<CreateUserRequest>
{
    private static readonly string[] ValidRoles = ["admin", "operator", "viewer"];

    public CreateUserRequestValidator()
    {
        RuleFor(x => x.Username).NotEmpty().MaximumLength(256);
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Email).MaximumLength(256).EmailAddress().When(x => !string.IsNullOrEmpty(x.Email));
        RuleFor(x => x.Password).NotEmpty().MinimumLength(8);
        RuleFor(x => x.Role).NotEmpty().Must(r => ValidRoles.Contains(r)).WithMessage("Role must be admin, operator, or viewer.");
    }
}
