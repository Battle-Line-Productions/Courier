using Courier.Domain.Common;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Courier.Features.Notifications;

[ApiController]
[Route("api/v1/notification-rules")]
[Authorize]
public class NotificationRulesController : ControllerBase
{
    private readonly NotificationRuleService _ruleService;
    private readonly IValidator<CreateNotificationRuleRequest> _createValidator;

    public NotificationRulesController(
        NotificationRuleService ruleService,
        IValidator<CreateNotificationRuleRequest> createValidator)
    {
        _ruleService = ruleService;
        _createValidator = createValidator;
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<NotificationRuleDto>>> Create(
        [FromBody] CreateNotificationRuleRequest request,
        CancellationToken ct)
    {
        var validation = await _createValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            var details = validation.Errors
                .Select(e => new FieldError(e.PropertyName, e.ErrorMessage))
                .ToList();

            return BadRequest(new ApiResponse<NotificationRuleDto>
            {
                Error = ErrorMessages.Create(ErrorCodes.ValidationFailed, "Validation failed.", details)
            });
        }

        var result = await _ruleService.CreateAsync(request, ct);

        if (!result.Success)
        {
            return result.Error!.Code switch
            {
                ErrorCodes.DuplicateNotificationRuleName => Conflict(result),
                _ => StatusCode(500, result)
            };
        }

        return Created($"/api/v1/notification-rules/{result.Data!.Id}", result);
    }

    [HttpGet]
    public async Task<ActionResult<PagedApiResponse<NotificationRuleDto>>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? search = null,
        [FromQuery] string? entityType = null,
        [FromQuery] string? channel = null,
        [FromQuery] bool? isEnabled = null,
        CancellationToken ct = default)
    {
        var result = await _ruleService.ListAsync(page, pageSize, search, entityType, channel, isEnabled, ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<NotificationRuleDto>>> GetById(Guid id, CancellationToken ct)
    {
        var result = await _ruleService.GetByIdAsync(id, ct);

        if (!result.Success)
            return NotFound(result);

        return Ok(result);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponse<NotificationRuleDto>>> Update(
        Guid id,
        [FromBody] UpdateNotificationRuleRequest request,
        [FromServices] IValidator<UpdateNotificationRuleRequest> validator,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            var details = validation.Errors
                .Select(e => new FieldError(e.PropertyName, e.ErrorMessage))
                .ToList();

            return BadRequest(new ApiResponse<NotificationRuleDto>
            {
                Error = ErrorMessages.Create(ErrorCodes.ValidationFailed, "Validation failed.", details)
            });
        }

        var result = await _ruleService.UpdateAsync(id, request, ct);

        if (!result.Success)
        {
            return result.Error!.Code switch
            {
                ErrorCodes.NotificationRuleNotFound => NotFound(result),
                ErrorCodes.DuplicateNotificationRuleName => Conflict(result),
                _ => StatusCode(500, result)
            };
        }

        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<ApiResponse<object>>> Delete(Guid id, CancellationToken ct)
    {
        var result = await _ruleService.DeleteAsync(id, ct);

        if (!result.Success)
        {
            return result.Error!.Code switch
            {
                ErrorCodes.NotificationRuleNotFound => NotFound(result),
                _ => StatusCode(500, result)
            };
        }

        return Ok(result);
    }

    [HttpPost("{id:guid}/test")]
    public async Task<ActionResult<ApiResponse<object>>> Test(Guid id, CancellationToken ct)
    {
        var result = await _ruleService.TestAsync(id, ct);

        if (!result.Success)
        {
            return result.Error!.Code switch
            {
                ErrorCodes.NotificationRuleNotFound => NotFound(result),
                ErrorCodes.NotificationTestFailed => UnprocessableEntity(result),
                _ => StatusCode(500, result)
            };
        }

        return Ok(result);
    }
}
