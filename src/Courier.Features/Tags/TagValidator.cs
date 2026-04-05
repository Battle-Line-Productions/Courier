using FluentValidation;

namespace Courier.Features.Tags;

public class CreateTagValidator : AbstractValidator<CreateTagRequest>
{
    public CreateTagValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Tag name is required.")
            .MaximumLength(100).WithMessage("Tag name must not exceed 100 characters.");

        RuleFor(x => x.Color)
            .Matches(@"^#[0-9A-Fa-f]{6}$").WithMessage("Color must be a valid hex color (e.g., #FF5733).")
            .When(x => x.Color is not null);

        RuleFor(x => x.Category)
            .MaximumLength(100).WithMessage("Category must not exceed 100 characters.")
            .When(x => x.Category is not null);

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("Description must not exceed 500 characters.")
            .When(x => x.Description is not null);
    }
}

public class UpdateTagValidator : AbstractValidator<UpdateTagRequest>
{
    public UpdateTagValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Tag name is required.")
            .MaximumLength(100).WithMessage("Tag name must not exceed 100 characters.");

        RuleFor(x => x.Color)
            .Matches(@"^#[0-9A-Fa-f]{6}$").WithMessage("Color must be a valid hex color (e.g., #FF5733).")
            .When(x => x.Color is not null);

        RuleFor(x => x.Category)
            .MaximumLength(100).WithMessage("Category must not exceed 100 characters.")
            .When(x => x.Category is not null);

        RuleFor(x => x.Description)
            .MaximumLength(500).WithMessage("Description must not exceed 500 characters.")
            .When(x => x.Description is not null);
    }
}

public class BulkTagAssignmentValidator : AbstractValidator<BulkTagAssignmentRequest>
{
    private static readonly string[] ValidEntityTypes = ["job", "connection", "pgp_key", "ssh_key", "file_monitor", "job_chain"];

    public BulkTagAssignmentValidator()
    {
        RuleFor(x => x.Assignments)
            .NotEmpty().WithMessage("At least one assignment is required.");

        RuleForEach(x => x.Assignments).ChildRules(a =>
        {
            a.RuleFor(x => x.TagId)
                .NotEmpty().WithMessage("Tag ID is required.");

            a.RuleFor(x => x.EntityType)
                .NotEmpty().WithMessage("Entity type is required.")
                .Must(v => ValidEntityTypes.Contains(v))
                .WithMessage("Entity type must be one of: job, connection, pgp_key, ssh_key, file_monitor, job_chain.");

            a.RuleFor(x => x.EntityId)
                .NotEmpty().WithMessage("Entity ID is required.");
        });
    }
}
