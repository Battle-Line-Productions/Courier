using FluentValidation;

namespace Courier.Features.SshKeys;

public class GenerateSshKeyValidator : AbstractValidator<GenerateSshKeyRequest>
{
    private static readonly string[] ValidKeyTypes = ["rsa_2048", "rsa_4096", "ed25519", "ecdsa_256"];

    public GenerateSshKeyValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Key name is required.")
            .MaximumLength(200).WithMessage("Key name must not exceed 200 characters.");

        RuleFor(x => x.KeyType)
            .NotEmpty().WithMessage("Key type is required.")
            .Must(v => ValidKeyTypes.Contains(v))
            .WithMessage("Key type must be one of: rsa_2048, rsa_4096, ed25519, ecdsa_256.");
    }
}

public class ImportSshKeyValidator : AbstractValidator<ImportSshKeyRequest>
{
    public ImportSshKeyValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Key name is required.")
            .MaximumLength(200).WithMessage("Key name must not exceed 200 characters.");
    }
}

public class UpdateSshKeyValidator : AbstractValidator<UpdateSshKeyRequest>
{
    public UpdateSshKeyValidator()
    {
        RuleFor(x => x.Name)
            .MaximumLength(200).WithMessage("Key name must not exceed 200 characters.")
            .When(x => x.Name is not null);
    }
}
