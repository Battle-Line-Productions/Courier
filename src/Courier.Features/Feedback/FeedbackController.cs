using System.Security.Claims;
using Courier.Domain.Common;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Courier.Features.Feedback;

[ApiController]
[Route("api/v1/feedback")]
[Authorize]
public class FeedbackController : ControllerBase
{
    private readonly FeedbackService _feedbackService;
    private readonly IValidator<CreateFeedbackRequest> _createValidator;

    public FeedbackController(
        FeedbackService feedbackService,
        IValidator<CreateFeedbackRequest> createValidator)
    {
        _feedbackService = feedbackService;
        _createValidator = createValidator;
    }

    private Guid GetUserId() => Guid.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);

    [HttpGet]
    public async Task<ActionResult<ApiResponse<List<FeedbackItemDto>>>> List(
        [FromQuery] string type = "feature",
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 20,
        [FromQuery] string state = "open",
        CancellationToken ct = default)
    {
        var result = await _feedbackService.ListAsync(type, page, pageSize, state, GetUserId(), ct);

        if (!result.Success)
        {
            return result.Error!.Code switch
            {
                ErrorCodes.GitHubNotConfigured => StatusCode(503, result),
                ErrorCodes.GitHubRateLimited => StatusCode(429, result),
                _ => StatusCode(500, result)
            };
        }

        return Ok(result);
    }

    [HttpGet("{number:int}")]
    public async Task<ActionResult<ApiResponse<FeedbackItemDto>>> GetByNumber(
        int number,
        CancellationToken ct)
    {
        var result = await _feedbackService.GetByNumberAsync(number, GetUserId(), ct);

        if (!result.Success)
        {
            return result.Error!.Code switch
            {
                ErrorCodes.GitHubIssueNotFound => NotFound(result),
                ErrorCodes.GitHubNotConfigured => StatusCode(503, result),
                _ => StatusCode(500, result)
            };
        }

        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<FeedbackItemDto>>> Create(
        [FromBody] CreateFeedbackRequest request,
        CancellationToken ct)
    {
        var validation = await _createValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            var details = validation.Errors
                .Select(e => new FieldError(e.PropertyName, e.ErrorMessage))
                .ToList();

            return BadRequest(new ApiResponse<FeedbackItemDto>
            {
                Error = ErrorMessages.Create(ErrorCodes.ValidationFailed, "Validation failed.", details)
            });
        }

        var result = await _feedbackService.CreateAsync(request, GetUserId(), ct);

        if (!result.Success)
        {
            return result.Error!.Code switch
            {
                ErrorCodes.GitHubAccountNotLinked => BadRequest(result),
                ErrorCodes.GitHubRateLimited => StatusCode(429, result),
                _ => StatusCode(500, result)
            };
        }

        return Created($"/api/v1/feedback/{result.Data!.Number}", result);
    }

    [HttpPost("{number:int}/vote")]
    public async Task<ActionResult<ApiResponse<FeedbackVoteResponse>>> Vote(
        int number,
        CancellationToken ct)
    {
        var result = await _feedbackService.VoteAsync(number, GetUserId(), ct);

        if (!result.Success)
        {
            return result.Error!.Code switch
            {
                ErrorCodes.GitHubAccountNotLinked => BadRequest(result),
                ErrorCodes.GitHubIssueNotFound => NotFound(result),
                _ => StatusCode(500, result)
            };
        }

        return Ok(result);
    }

    [HttpDelete("{number:int}/vote")]
    public async Task<ActionResult<ApiResponse<FeedbackVoteResponse>>> Unvote(
        int number,
        CancellationToken ct)
    {
        var result = await _feedbackService.UnvoteAsync(number, GetUserId(), ct);

        if (!result.Success)
        {
            return result.Error!.Code switch
            {
                ErrorCodes.GitHubAccountNotLinked => BadRequest(result),
                ErrorCodes.GitHubIssueNotFound => NotFound(result),
                _ => StatusCode(500, result)
            };
        }

        return Ok(result);
    }
}
