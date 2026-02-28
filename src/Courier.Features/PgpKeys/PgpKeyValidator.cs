using FluentValidation;

namespace Courier.Features.PgpKeys;

public class GeneratePgpKeyValidator : AbstractValidator<GeneratePgpKeyRequest>
{
    private static readonly string[] ValidAlgorithms =
        ["rsa_2048", "rsa_3072", "rsa_4096", "ecc_curve25519", "ecc_p256", "ecc_p384"];

    public GeneratePgpKeyValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Key name is required.")
            .MaximumLength(200).WithMessage("Key name must not exceed 200 characters.");

        RuleFor(x => x.Algorithm)
            .NotEmpty().WithMessage("Algorithm is required.")
            .Must(v => ValidAlgorithms.Contains(v))
            .WithMessage("Algorithm must be one of: rsa_2048, rsa_3072, rsa_4096, ecc_curve25519, ecc_p256, ecc_p384.");

        RuleFor(x => x.ExpiresInDays)
            .GreaterThan(0).WithMessage("Expiration must be greater than 0 days.")
            .When(x => x.ExpiresInDays.HasValue);

        RuleFor(x => x.Email)
            .EmailAddress().WithMessage("Email must be a valid email address.")
            .When(x => !string.IsNullOrWhiteSpace(x.Email));
    }
}

public class ImportPgpKeyValidator : AbstractValidator<ImportPgpKeyRequest>
{
    public ImportPgpKeyValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Key name is required.")
            .MaximumLength(200).WithMessage("Key name must not exceed 200 characters.");
    }
}

public class UpdatePgpKeyValidator : AbstractValidator<UpdatePgpKeyRequest>
{
    public UpdatePgpKeyValidator()
    {
        RuleFor(x => x.Name)
            .MaximumLength(200).WithMessage("Key name must not exceed 200 characters.")
            .When(x => x.Name is not null);
    }
}
