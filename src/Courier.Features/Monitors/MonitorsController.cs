using Courier.Domain.Common;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Courier.Features.Monitors;

[ApiController]
[Route("api/v1/monitors")]
[Authorize]
public class MonitorsController : ControllerBase
{
    private readonly MonitorService _monitorService;
    private readonly IValidator<CreateMonitorRequest> _createValidator;

    public MonitorsController(
        MonitorService monitorService,
        IValidator<CreateMonitorRequest> createValidator)
    {
        _monitorService = monitorService;
        _createValidator = createValidator;
    }

    [HttpPost]
    [Authorize(Roles = "admin,operator")]
    public async Task<ActionResult<ApiResponse<MonitorDto>>> Create(
        [FromBody] CreateMonitorRequest request,
        CancellationToken ct)
    {
        var validation = await _createValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            var details = validation.Errors
                .Select(e => new FieldError(e.PropertyName, e.ErrorMessage))
                .ToList();

            return BadRequest(new ApiResponse<MonitorDto>
            {
                Error = ErrorMessages.Create(ErrorCodes.ValidationFailed, "Validation failed.", details)
            });
        }

        var result = await _monitorService.CreateAsync(request, ct);

        if (!result.Success)
        {
            return result.Error!.Code switch
            {
                ErrorCodes.ResourceNotFound => NotFound(result),
                _ => StatusCode(500, result)
            };
        }

        return Created($"/api/v1/monitors/{result.Data!.Id}", result);
    }

    [HttpGet]
    public async Task<ActionResult<PagedApiResponse<MonitorDto>>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? search = null,
        [FromQuery] string? state = null,
        [FromQuery] string? tag = null,
        CancellationToken ct = default)
    {
        var result = await _monitorService.ListAsync(page, pageSize, search, state, tag, ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<MonitorDto>>> GetById(Guid id, CancellationToken ct)
    {
        var result = await _monitorService.GetByIdAsync(id, ct);

        if (!result.Success)
            return NotFound(result);

        return Ok(result);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "admin,operator")]
    public async Task<ActionResult<ApiResponse<MonitorDto>>> Update(
        Guid id,
        [FromBody] UpdateMonitorRequest request,
        [FromServices] IValidator<UpdateMonitorRequest> validator,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            var details = validation.Errors
                .Select(e => new FieldError(e.PropertyName, e.ErrorMessage))
                .ToList();

            return BadRequest(new ApiResponse<MonitorDto>
            {
                Error = ErrorMessages.Create(ErrorCodes.ValidationFailed, "Validation failed.", details)
            });
        }

        var result = await _monitorService.UpdateAsync(id, request, ct);

        if (!result.Success)
        {
            return result.Error!.Code switch
            {
                ErrorCodes.ResourceNotFound => NotFound(result),
                _ => StatusCode(500, result)
            };
        }

        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "admin,operator")]
    public async Task<ActionResult<ApiResponse>> Delete(Guid id, CancellationToken ct)
    {
        var result = await _monitorService.DeleteAsync(id, ct);

        if (!result.Success)
        {
            return result.Error!.Code switch
            {
                ErrorCodes.ResourceNotFound => NotFound(result),
                _ => StatusCode(500, result)
            };
        }

        return Ok(result);
    }

    [HttpPost("{id:guid}/activate")]
    [Authorize(Roles = "admin,operator")]
    public async Task<ActionResult<ApiResponse<MonitorDto>>> Activate(Guid id, CancellationToken ct)
    {
        var result = await _monitorService.ActivateAsync(id, ct);

        if (!result.Success)
        {
            return result.Error!.Code switch
            {
                ErrorCodes.ResourceNotFound => NotFound(result),
                ErrorCodes.MonitorAlreadyActive or ErrorCodes.StateConflict => Conflict(result),
                _ => StatusCode(500, result)
            };
        }

        return Ok(result);
    }

    [HttpPost("{id:guid}/pause")]
    [Authorize(Roles = "admin,operator")]
    public async Task<ActionResult<ApiResponse<MonitorDto>>> Pause(Guid id, CancellationToken ct)
    {
        var result = await _monitorService.PauseAsync(id, ct);

        if (!result.Success)
        {
            return result.Error!.Code switch
            {
                ErrorCodes.ResourceNotFound => NotFound(result),
                ErrorCodes.StateConflict => Conflict(result),
                _ => StatusCode(500, result)
            };
        }

        return Ok(result);
    }

    [HttpPost("{id:guid}/disable")]
    [Authorize(Roles = "admin,operator")]
    public async Task<ActionResult<ApiResponse<MonitorDto>>> Disable(Guid id, CancellationToken ct)
    {
        var result = await _monitorService.DisableAsync(id, ct);

        if (!result.Success)
        {
            return result.Error!.Code switch
            {
                ErrorCodes.ResourceNotFound => NotFound(result),
                ErrorCodes.StateConflict => Conflict(result),
                _ => StatusCode(500, result)
            };
        }

        return Ok(result);
    }

    [HttpPost("{id:guid}/acknowledge-error")]
    [Authorize(Roles = "admin,operator")]
    public async Task<ActionResult<ApiResponse<MonitorDto>>> AcknowledgeError(Guid id, CancellationToken ct)
    {
        var result = await _monitorService.AcknowledgeErrorAsync(id, ct);

        if (!result.Success)
        {
            return result.Error!.Code switch
            {
                ErrorCodes.ResourceNotFound => NotFound(result),
                ErrorCodes.MonitorNotInError => Conflict(result),
                _ => StatusCode(500, result)
            };
        }

        return Ok(result);
    }

    [HttpGet("{id:guid}/file-log")]
    public async Task<ActionResult<PagedApiResponse<MonitorFileLogDto>>> ListFileLog(
        Guid id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken ct = default)
    {
        var result = await _monitorService.ListFileLogAsync(id, page, pageSize, ct);

        if (!result.Success)
            return NotFound(result);

        return Ok(result);
    }
}
