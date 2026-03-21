using FluentValidation;

namespace Courier.Features.AuthProviders;

public class CreateAuthProviderValidator : AbstractValidator<CreateAuthProviderRequest>
{
    private static readonly string[] ValidTypes = ["oidc", "saml"];
    private static readonly string[] ValidRoles = ["admin", "operator", "viewer"];

    public CreateAuthProviderValidator()
    {
        RuleFor(x => x.Type)
            .NotEmpty().WithMessage("Provider type is required.")
            .Must(v => ValidTypes.Contains(v))
            .WithMessage("Type must be one of: oidc, saml.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Provider name is required.")
            .MaximumLength(200).WithMessage("Provider name must not exceed 200 characters.");

        RuleFor(x => x.DefaultRole)
            .NotEmpty().WithMessage("Default role is required.")
            .Must(v => ValidRoles.Contains(v))
            .WithMessage("Default role must be one of: admin, operator, viewer.");

        RuleFor(x => x.Configuration)
            .Must(v => v.ValueKind != System.Text.Json.JsonValueKind.Undefined
                    && v.ValueKind != System.Text.Json.JsonValueKind.Null)
            .WithMessage("Configuration is required.");
    }
}

public class UpdateAuthProviderValidator : AbstractValidator<UpdateAuthProviderRequest>
{
    private static readonly string[] ValidTypes = ["oidc", "saml"];
    private static readonly string[] ValidRoles = ["admin", "operator", "viewer"];

    public UpdateAuthProviderValidator()
    {
        RuleFor(x => x.Name)
            .MaximumLength(200).WithMessage("Provider name must not exceed 200 characters.")
            .When(x => x.Name is not null);

        RuleFor(x => x.Type)
            .Must(v => ValidTypes.Contains(v!))
            .WithMessage("Type must be one of: oidc, saml.")
            .When(x => x.Type is not null);

        RuleFor(x => x.DefaultRole)
            .Must(v => ValidRoles.Contains(v!))
            .WithMessage("Default role must be one of: admin, operator, viewer.")
            .When(x => x.DefaultRole is not null);
    }
}
