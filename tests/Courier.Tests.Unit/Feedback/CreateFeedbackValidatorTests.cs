using Courier.Features.Feedback;
using FluentValidation.TestHelper;

namespace Courier.Tests.Unit.Feedback;

public class CreateFeedbackValidatorTests
{
    private readonly CreateFeedbackValidator _validator = new();

    [Fact]
    public void Validate_ValidBugReport_Passes()
    {
        var request = new CreateFeedbackRequest { Title = "Bug title", Description = "Bug description", Type = "bug" };
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_ValidFeatureRequest_Passes()
    {
        var request = new CreateFeedbackRequest { Title = "Feature title", Description = "Feature description", Type = "feature" };
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_EmptyTitle_Fails()
    {
        var request = new CreateFeedbackRequest { Title = "", Description = "desc", Type = "bug" };
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Title);
    }

    [Fact]
    public void Validate_TitleTooLong_Fails()
    {
        var request = new CreateFeedbackRequest { Title = new string('a', 257), Description = "desc", Type = "bug" };
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Title);
    }

    [Fact]
    public void Validate_TitleAtMaxLength_Passes()
    {
        var request = new CreateFeedbackRequest { Title = new string('a', 256), Description = "desc", Type = "bug" };
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.Title);
    }

    [Fact]
    public void Validate_EmptyDescription_Fails()
    {
        var request = new CreateFeedbackRequest { Title = "title", Description = "", Type = "bug" };
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Description);
    }

    [Fact]
    public void Validate_DescriptionTooLong_Fails()
    {
        var request = new CreateFeedbackRequest { Title = "title", Description = new string('a', 65537), Type = "bug" };
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Description);
    }

    [Fact]
    public void Validate_DescriptionAtMaxLength_Passes()
    {
        var request = new CreateFeedbackRequest { Title = "title", Description = new string('a', 65536), Type = "bug" };
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.Description);
    }

    [Fact]
    public void Validate_EmptyType_Fails()
    {
        var request = new CreateFeedbackRequest { Title = "title", Description = "desc", Type = "" };
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Type);
    }

    [Fact]
    public void Validate_InvalidType_Fails()
    {
        var request = new CreateFeedbackRequest { Title = "title", Description = "desc", Type = "suggestion" };
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Type);
    }

    [Theory]
    [InlineData("bug")]
    [InlineData("feature")]
    public void Validate_ValidTypes_Pass(string type)
    {
        var request = new CreateFeedbackRequest { Title = "title", Description = "desc", Type = type };
        var result = _validator.TestValidate(request);
        result.ShouldNotHaveValidationErrorFor(x => x.Type);
    }

    [Theory]
    [InlineData("Bug")]
    [InlineData("FEATURE")]
    [InlineData("Feature")]
    public void Validate_TypeIsCaseSensitive_Fails(string type)
    {
        var request = new CreateFeedbackRequest { Title = "title", Description = "desc", Type = type };
        var result = _validator.TestValidate(request);
        result.ShouldHaveValidationErrorFor(x => x.Type);
    }
}
