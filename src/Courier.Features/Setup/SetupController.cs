using Courier.Domain.Common;
using Courier.Features.Auth;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Courier.Features.Setup;

[ApiController]
[Route("api/v1/setup")]
public class SetupController : ControllerBase
{
    private readonly SetupService _setupService;
    private readonly IValidator<InitializeSetupRequest> _validator;

    public SetupController(SetupService setupService, IValidator<InitializeSetupRequest> validator)
    {
        _setupService = setupService;
        _validator = validator;
    }

    [HttpGet("status")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<SetupStatusDto>>> Status(CancellationToken ct)
    {
        var result = await _setupService.GetStatusAsync(ct);
        return Ok(result);
    }

    [HttpPost("initialize")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<UserProfileDto>>> Initialize(
        [FromBody] InitializeSetupRequest request,
        CancellationToken ct)
    {
        var validation = await _validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            var details = validation.Errors
                .Select(e => new FieldError(e.PropertyName, e.ErrorMessage))
                .ToList();

            return BadRequest(new ApiResponse<UserProfileDto>
            {
                Error = ErrorMessages.Create(ErrorCodes.ValidationFailed, "Validation failed.", details)
            });
        }

        var result = await _setupService.InitializeAsync(request, ct);

        if (!result.Success)
        {
            return result.Error!.Code switch
            {
                ErrorCodes.SetupAlreadyCompleted => Conflict(result),
                _ => StatusCode(500, result)
            };
        }

        return Created("/api/v1/auth/me", result);
    }
}
