using Courier.Domain.Common;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Courier.Features.Jobs;

[ApiController]
[Route("api/v1/jobs")]
[Authorize]
public class JobsController : ControllerBase
{
    private readonly JobService _jobService;
    private readonly JobStepService _stepService;
    private readonly ExecutionService _executionService;
    private readonly JobScheduleService _scheduleService;
    private readonly JobDependencyService _dependencyService;
    private readonly IValidator<CreateJobRequest> _validator;

    public JobsController(
        JobService jobService,
        JobStepService stepService,
        ExecutionService executionService,
        JobScheduleService scheduleService,
        JobDependencyService dependencyService,
        IValidator<CreateJobRequest> validator)
    {
        _jobService = jobService;
        _stepService = stepService;
        _executionService = executionService;
        _scheduleService = scheduleService;
        _dependencyService = dependencyService;
        _validator = validator;
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<JobDto>>> Create(
        [FromBody] CreateJobRequest request,
        CancellationToken ct)
    {
        var validation = await _validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            var details = validation.Errors
                .Select(e => new FieldError(e.PropertyName, e.ErrorMessage))
                .ToList();

            return BadRequest(new ApiResponse<JobDto>
            {
                Error = ErrorMessages.Create(ErrorCodes.ValidationFailed, "Validation failed.", details)
            });
        }

        var result = await _jobService.CreateAsync(request, ct);
        return Created($"/api/v1/jobs/{result.Data!.Id}", result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<JobDto>>> GetById(Guid id, CancellationToken ct)
    {
        var result = await _jobService.GetByIdAsync(id, ct);

        if (!result.Success)
            return NotFound(result);

        return Ok(result);
    }

    [HttpGet]
    public async Task<ActionResult<PagedApiResponse<JobDto>>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? tag = null,
        CancellationToken ct = default)
    {
        var result = await _jobService.ListAsync(page, pageSize, tag, ct);
        return Ok(result);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponse<JobDto>>> Update(
        Guid id,
        [FromBody] UpdateJobRequest request,
        [FromServices] IValidator<UpdateJobRequest> validator,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            var details = validation.Errors
                .Select(e => new FieldError(e.PropertyName, e.ErrorMessage))
                .ToList();

            return BadRequest(new ApiResponse<JobDto>
            {
                Error = ErrorMessages.Create(ErrorCodes.ValidationFailed, "Validation failed.", details)
            });
        }

        var result = await _jobService.UpdateAsync(id, request.Name, request.Description, ct);

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
    public async Task<ActionResult<ApiResponse>> Delete(Guid id, CancellationToken ct)
    {
        var result = await _jobService.DeleteAsync(id, ct);

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

    [HttpPut("{jobId:guid}/steps")]
    public async Task<ActionResult<ApiResponse<List<JobStepDto>>>> ReplaceSteps(
        Guid jobId,
        [FromBody] ReplaceJobStepsRequest request,
        CancellationToken ct)
    {
        var result = await _stepService.ReplaceStepsAsync(jobId, request.Steps, ct);

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

    [HttpPost("{jobId:guid}/steps")]
    public async Task<ActionResult<ApiResponse<JobStepDto>>> AddStep(
        Guid jobId,
        [FromBody] AddJobStepRequest request,
        CancellationToken ct)
    {
        var result = await _stepService.AddStepAsync(jobId, request, ct);

        if (!result.Success)
            return NotFound(result);

        return Created($"/api/v1/jobs/{jobId}/steps", result);
    }

    [HttpGet("{jobId:guid}/steps")]
    public async Task<ActionResult<ApiResponse<List<JobStepDto>>>> ListSteps(
        Guid jobId,
        CancellationToken ct)
    {
        var result = await _stepService.ListStepsAsync(jobId, ct);

        if (!result.Success)
            return NotFound(result);

        return Ok(result);
    }

    [HttpPost("{jobId:guid}/trigger")]
    public async Task<ActionResult<ApiResponse<JobExecutionDto>>> Trigger(
        Guid jobId,
        [FromBody] TriggerJobRequest request,
        CancellationToken ct)
    {
        var result = await _executionService.TriggerAsync(jobId, request.TriggeredBy, ct);

        if (!result.Success)
        {
            return result.Error!.Code switch
            {
                ErrorCodes.ResourceNotFound => NotFound(result),
                ErrorCodes.JobNotEnabled => Conflict(result),
                ErrorCodes.JobHasNoSteps => BadRequest(result),
                _ => StatusCode(500, result),
            };
        }

        return Accepted(result);
    }

    [HttpGet("{jobId:guid}/executions")]
    public async Task<ActionResult<PagedApiResponse<JobExecutionDto>>> ListExecutions(
        Guid jobId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken ct = default)
    {
        var result = await _executionService.ListExecutionsAsync(jobId, page, pageSize, ct);
        return Ok(result);
    }

    [HttpGet("executions/{executionId:guid}")]
    public async Task<ActionResult<ApiResponse<JobExecutionDto>>> GetExecution(
        Guid executionId,
        CancellationToken ct)
    {
        var result = await _executionService.GetExecutionAsync(executionId, ct);

        if (!result.Success)
            return NotFound(result);

        return Ok(result);
    }

    [HttpPost("executions/{executionId:guid}/pause")]
    public async Task<ActionResult<ApiResponse<JobExecutionDto>>> PauseExecution(
        Guid executionId,
        CancellationToken ct)
    {
        var result = await _executionService.PauseExecutionAsync(executionId, "system", ct);

        if (!result.Success)
        {
            return result.Error!.Code switch
            {
                ErrorCodes.ExecutionNotFound => NotFound(result),
                ErrorCodes.ExecutionCannotBePaused => Conflict(result),
                _ => StatusCode(500, result)
            };
        }

        return Ok(result);
    }

    [HttpPost("executions/{executionId:guid}/resume")]
    public async Task<ActionResult<ApiResponse<JobExecutionDto>>> ResumeExecution(
        Guid executionId,
        CancellationToken ct)
    {
        var result = await _executionService.ResumeExecutionAsync(executionId, "system", ct);

        if (!result.Success)
        {
            return result.Error!.Code switch
            {
                ErrorCodes.ExecutionNotFound => NotFound(result),
                ErrorCodes.ExecutionCannotBeResumed => Conflict(result),
                _ => StatusCode(500, result)
            };
        }

        return Ok(result);
    }

    [HttpPost("executions/{executionId:guid}/cancel")]
    public async Task<ActionResult<ApiResponse<JobExecutionDto>>> CancelExecution(
        Guid executionId,
        [FromBody(EmptyBodyBehavior = Microsoft.AspNetCore.Mvc.ModelBinding.EmptyBodyBehavior.Allow)] CancelExecutionRequest? request,
        CancellationToken ct)
    {
        var result = await _executionService.CancelExecutionAsync(executionId, "system", request?.Reason, ct);

        if (!result.Success)
        {
            return result.Error!.Code switch
            {
                ErrorCodes.ExecutionNotFound => NotFound(result),
                ErrorCodes.ExecutionCannotBeCancelled => Conflict(result),
                _ => StatusCode(500, result)
            };
        }

        return Ok(result);
    }

    [HttpGet("{jobId:guid}/schedules")]
    public async Task<ActionResult<ApiResponse<List<JobScheduleDto>>>> ListSchedules(
        Guid jobId,
        CancellationToken ct)
    {
        var result = await _scheduleService.ListAsync(jobId, ct);

        if (!result.Success)
            return NotFound(result);

        return Ok(result);
    }

    [HttpPost("{jobId:guid}/schedules")]
    public async Task<ActionResult<ApiResponse<JobScheduleDto>>> CreateSchedule(
        Guid jobId,
        [FromBody] CreateJobScheduleRequest request,
        [FromServices] IValidator<CreateJobScheduleRequest> validator,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            var details = validation.Errors
                .Select(e => new FieldError(e.PropertyName, e.ErrorMessage))
                .ToList();

            return BadRequest(new ApiResponse<JobScheduleDto>
            {
                Error = ErrorMessages.Create(ErrorCodes.ValidationFailed, "Validation failed.", details)
            });
        }

        var result = await _scheduleService.CreateAsync(jobId, request, ct);

        if (!result.Success)
        {
            return result.Error!.Code switch
            {
                ErrorCodes.ResourceNotFound => NotFound(result),
                _ => StatusCode(500, result)
            };
        }

        return Created($"/api/v1/jobs/{jobId}/schedules/{result.Data!.Id}", result);
    }

    [HttpPut("{jobId:guid}/schedules/{scheduleId:guid}")]
    public async Task<ActionResult<ApiResponse<JobScheduleDto>>> UpdateSchedule(
        Guid jobId,
        Guid scheduleId,
        [FromBody] UpdateJobScheduleRequest request,
        [FromServices] IValidator<UpdateJobScheduleRequest> validator,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            var details = validation.Errors
                .Select(e => new FieldError(e.PropertyName, e.ErrorMessage))
                .ToList();

            return BadRequest(new ApiResponse<JobScheduleDto>
            {
                Error = ErrorMessages.Create(ErrorCodes.ValidationFailed, "Validation failed.", details)
            });
        }

        var result = await _scheduleService.UpdateAsync(jobId, scheduleId, request, ct);

        if (!result.Success)
        {
            return result.Error!.Code switch
            {
                ErrorCodes.ScheduleNotFound => NotFound(result),
                ErrorCodes.ScheduleJobMismatch => BadRequest(result),
                _ => StatusCode(500, result)
            };
        }

        return Ok(result);
    }

    [HttpDelete("{jobId:guid}/schedules/{scheduleId:guid}")]
    public async Task<ActionResult<ApiResponse>> DeleteSchedule(
        Guid jobId,
        Guid scheduleId,
        CancellationToken ct)
    {
        var result = await _scheduleService.DeleteAsync(jobId, scheduleId, ct);

        if (!result.Success)
        {
            return result.Error!.Code switch
            {
                ErrorCodes.ScheduleNotFound => NotFound(result),
                ErrorCodes.ScheduleJobMismatch => BadRequest(result),
                _ => StatusCode(500, result)
            };
        }

        return Ok(result);
    }

    [HttpGet("{id:guid}/versions")]
    public async Task<ActionResult<ApiResponse<List<JobVersionDto>>>> GetVersions(Guid id, CancellationToken ct)
    {
        var result = await _jobService.GetVersionsAsync(id, ct);

        if (!result.Success)
            return NotFound(result);

        return Ok(result);
    }

    [HttpGet("{id:guid}/versions/{versionNumber:int}")]
    public async Task<ActionResult<ApiResponse<JobVersionDto>>> GetVersion(Guid id, int versionNumber, CancellationToken ct)
    {
        var result = await _jobService.GetVersionAsync(id, versionNumber, ct);

        if (!result.Success)
        {
            return result.Error!.Code switch
            {
                ErrorCodes.ResourceNotFound => NotFound(result),
                ErrorCodes.JobVersionNotFound => NotFound(result),
                _ => StatusCode(500, result)
            };
        }

        return Ok(result);
    }

    [HttpGet("{jobId:guid}/dependencies")]
    public async Task<ActionResult<ApiResponse<List<JobDependencyDto>>>> ListDependencies(
        Guid jobId,
        CancellationToken ct)
    {
        var result = await _dependencyService.ListDependenciesAsync(jobId, ct);
        return Ok(result);
    }

    [HttpPost("{jobId:guid}/dependencies")]
    public async Task<ActionResult<ApiResponse<JobDependencyDto>>> AddDependency(
        Guid jobId,
        [FromBody] AddJobDependencyRequest request,
        CancellationToken ct)
    {
        var result = await _dependencyService.AddDependencyAsync(jobId, request.UpstreamJobId, request.RunOnFailure, ct);

        if (!result.Success)
        {
            return result.Error!.Code switch
            {
                ErrorCodes.ResourceNotFound => NotFound(result),
                ErrorCodes.SelfDependency => BadRequest(result),
                ErrorCodes.DuplicateDependency => Conflict(result),
                ErrorCodes.CircularDependency => BadRequest(result),
                _ => StatusCode(500, result)
            };
        }

        return Created($"/api/v1/jobs/{jobId}/dependencies/{result.Data!.Id}", result);
    }

    [HttpDelete("{jobId:guid}/dependencies/{depId:guid}")]
    public async Task<ActionResult<ApiResponse>> RemoveDependency(
        Guid jobId,
        Guid depId,
        CancellationToken ct)
    {
        var result = await _dependencyService.RemoveDependencyAsync(jobId, depId, ct);

        if (!result.Success)
        {
            return result.Error!.Code switch
            {
                ErrorCodes.DependencyNotFound => NotFound(result),
                _ => StatusCode(500, result)
            };
        }

        return Ok(result);
    }
}
