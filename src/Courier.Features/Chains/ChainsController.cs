using Courier.Domain.Common;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Courier.Features.Chains;

[ApiController]
[Route("api/v1/chains")]
[Authorize]
public class ChainsController : ControllerBase
{
    private readonly ChainService _chainService;
    private readonly ChainExecutionService _executionService;
    private readonly ChainScheduleService _scheduleService;
    private readonly IValidator<CreateChainRequest> _createValidator;

    public ChainsController(
        ChainService chainService,
        ChainExecutionService executionService,
        ChainScheduleService scheduleService,
        IValidator<CreateChainRequest> createValidator)
    {
        _chainService = chainService;
        _executionService = executionService;
        _scheduleService = scheduleService;
        _createValidator = createValidator;
    }

    [HttpPost]
    [Authorize(Roles = "admin,operator")]
    public async Task<ActionResult<ApiResponse<JobChainDto>>> Create(
        [FromBody] CreateChainRequest request,
        CancellationToken ct)
    {
        var validation = await _createValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            var details = validation.Errors
                .Select(e => new FieldError(e.PropertyName, e.ErrorMessage))
                .ToList();

            return BadRequest(new ApiResponse<JobChainDto>
            {
                Error = ErrorMessages.Create(ErrorCodes.ValidationFailed, "Validation failed.", details)
            });
        }

        var result = await _chainService.CreateAsync(request, ct);
        return Created($"/api/v1/chains/{result.Data!.Id}", result);
    }

    [HttpGet]
    public async Task<ActionResult<PagedApiResponse<JobChainDto>>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken ct = default)
    {
        var result = await _chainService.ListAsync(page, pageSize, ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<JobChainDto>>> GetById(Guid id, CancellationToken ct)
    {
        var result = await _chainService.GetByIdAsync(id, ct);

        if (!result.Success)
            return NotFound(result);

        return Ok(result);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "admin,operator")]
    public async Task<ActionResult<ApiResponse<JobChainDto>>> Update(
        Guid id,
        [FromBody] UpdateChainRequest request,
        [FromServices] IValidator<UpdateChainRequest> validator,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            var details = validation.Errors
                .Select(e => new FieldError(e.PropertyName, e.ErrorMessage))
                .ToList();

            return BadRequest(new ApiResponse<JobChainDto>
            {
                Error = ErrorMessages.Create(ErrorCodes.ValidationFailed, "Validation failed.", details)
            });
        }

        var result = await _chainService.UpdateAsync(id, request.Name, request.Description, ct);

        if (!result.Success)
        {
            return result.Error!.Code switch
            {
                ErrorCodes.ChainNotFound => NotFound(result),
                _ => StatusCode(500, result)
            };
        }

        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "admin,operator")]
    public async Task<ActionResult<ApiResponse>> Delete(Guid id, CancellationToken ct)
    {
        var result = await _chainService.DeleteAsync(id, ct);

        if (!result.Success)
        {
            return result.Error!.Code switch
            {
                ErrorCodes.ChainNotFound => NotFound(result),
                _ => StatusCode(500, result)
            };
        }

        return Ok(result);
    }

    [HttpPut("{id:guid}/members")]
    [Authorize(Roles = "admin,operator")]
    public async Task<ActionResult<ApiResponse<List<JobChainMemberDto>>>> ReplaceMembers(
        Guid id,
        [FromBody] ReplaceChainMembersRequest request,
        CancellationToken ct)
    {
        var result = await _chainService.ReplaceMembersAsync(id, request.Members, ct);

        if (!result.Success)
        {
            return result.Error!.Code switch
            {
                ErrorCodes.ChainNotFound => NotFound(result),
                ErrorCodes.ChainMemberJobNotFound => BadRequest(result),
                ErrorCodes.CircularDependency => BadRequest(result),
                _ => StatusCode(500, result)
            };
        }

        return Ok(result);
    }

    [HttpPost("{id:guid}/execute")]
    [Authorize(Roles = "admin,operator")]
    public async Task<ActionResult<ApiResponse<ChainExecutionDto>>> Execute(
        Guid id,
        [FromBody] TriggerChainRequest request,
        CancellationToken ct)
    {
        var result = await _executionService.TriggerAsync(id, request.TriggeredBy, ct);

        if (!result.Success)
        {
            return result.Error!.Code switch
            {
                ErrorCodes.ChainNotFound => NotFound(result),
                ErrorCodes.ChainNotEnabled => Conflict(result),
                ErrorCodes.ChainHasNoMembers => BadRequest(result),
                _ => StatusCode(500, result)
            };
        }

        return Accepted(result);
    }

    [HttpGet("{id:guid}/executions")]
    public async Task<ActionResult<PagedApiResponse<ChainExecutionDto>>> ListExecutions(
        Guid id,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken ct = default)
    {
        var result = await _executionService.ListExecutionsAsync(id, page, pageSize, ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}/executions/{execId:guid}")]
    public async Task<ActionResult<ApiResponse<ChainExecutionDto>>> GetExecution(
        Guid id,
        Guid execId,
        CancellationToken ct)
    {
        var result = await _executionService.GetExecutionAsync(execId, ct);

        if (!result.Success)
            return NotFound(result);

        return Ok(result);
    }

    // --- Schedules ---

    [HttpGet("{chainId:guid}/schedules")]
    public async Task<ActionResult<ApiResponse<List<ChainScheduleDto>>>> ListSchedules(
        Guid chainId,
        CancellationToken ct)
    {
        var result = await _scheduleService.ListAsync(chainId, ct);

        if (!result.Success)
            return NotFound(result);

        return Ok(result);
    }

    [HttpPost("{chainId:guid}/schedules")]
    [Authorize(Roles = "admin,operator")]
    public async Task<ActionResult<ApiResponse<ChainScheduleDto>>> CreateSchedule(
        Guid chainId,
        [FromBody] CreateChainScheduleRequest request,
        [FromServices] IValidator<CreateChainScheduleRequest> validator,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            var details = validation.Errors
                .Select(e => new FieldError(e.PropertyName, e.ErrorMessage))
                .ToList();

            return BadRequest(new ApiResponse<ChainScheduleDto>
            {
                Error = ErrorMessages.Create(ErrorCodes.ValidationFailed, "Validation failed.", details)
            });
        }

        var result = await _scheduleService.CreateAsync(chainId, request, ct);

        if (!result.Success)
        {
            return result.Error!.Code switch
            {
                ErrorCodes.ChainNotFound => NotFound(result),
                _ => StatusCode(500, result)
            };
        }

        return Created($"/api/v1/chains/{chainId}/schedules/{result.Data!.Id}", result);
    }

    [HttpPut("{chainId:guid}/schedules/{scheduleId:guid}")]
    [Authorize(Roles = "admin,operator")]
    public async Task<ActionResult<ApiResponse<ChainScheduleDto>>> UpdateSchedule(
        Guid chainId,
        Guid scheduleId,
        [FromBody] UpdateChainScheduleRequest request,
        [FromServices] IValidator<UpdateChainScheduleRequest> validator,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            var details = validation.Errors
                .Select(e => new FieldError(e.PropertyName, e.ErrorMessage))
                .ToList();

            return BadRequest(new ApiResponse<ChainScheduleDto>
            {
                Error = ErrorMessages.Create(ErrorCodes.ValidationFailed, "Validation failed.", details)
            });
        }

        var result = await _scheduleService.UpdateAsync(chainId, scheduleId, request, ct);

        if (!result.Success)
        {
            return result.Error!.Code switch
            {
                ErrorCodes.ChainScheduleNotFound => NotFound(result),
                ErrorCodes.ChainScheduleMismatch => BadRequest(result),
                _ => StatusCode(500, result)
            };
        }

        return Ok(result);
    }

    [HttpDelete("{chainId:guid}/schedules/{scheduleId:guid}")]
    [Authorize(Roles = "admin,operator")]
    public async Task<ActionResult<ApiResponse>> DeleteSchedule(
        Guid chainId,
        Guid scheduleId,
        CancellationToken ct)
    {
        var result = await _scheduleService.DeleteAsync(chainId, scheduleId, ct);

        if (!result.Success)
        {
            return result.Error!.Code switch
            {
                ErrorCodes.ChainScheduleNotFound => NotFound(result),
                ErrorCodes.ChainScheduleMismatch => BadRequest(result),
                _ => StatusCode(500, result)
            };
        }

        return Ok(result);
    }
}
