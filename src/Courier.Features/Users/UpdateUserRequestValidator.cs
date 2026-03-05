using FluentValidation;

namespace Courier.Features.Users;

public class UpdateUserRequestValidator : AbstractValidator<UpdateUserRequest>
{
    private static readonly string[] ValidRoles = ["admin", "operator", "viewer"];

    public UpdateUserRequestValidator()
    {
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(256);
        RuleFor(x => x.Email).MaximumLength(256).EmailAddress().When(x => !string.IsNullOrEmpty(x.Email));
        RuleFor(x => x.Role).NotEmpty().Must(r => ValidRoles.Contains(r)).WithMessage("Role must be admin, operator, or viewer.");
    }
}
