using Courier.Domain.Common;
using Courier.Domain.Enums;
using Courier.Features.Security;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Courier.Features.Connections;

[ApiController]
[Authorize]
public class KnownHostsController : ControllerBase
{
    private readonly KnownHostService _knownHostService;
    private readonly IValidator<CreateKnownHostRequest> _createValidator;

    public KnownHostsController(
        KnownHostService knownHostService,
        IValidator<CreateKnownHostRequest> createValidator)
    {
        _knownHostService = knownHostService;
        _createValidator = createValidator;
    }

    [HttpGet("api/v1/connections/{connectionId:guid}/known-hosts")]
    [RequirePermission(Permission.KnownHostsView)]
    public async Task<ActionResult<ApiResponse<List<KnownHostDto>>>> ListByConnection(
        Guid connectionId,
        CancellationToken ct)
    {
        var result = await _knownHostService.GetByConnectionIdAsync(connectionId, ct);

        if (!result.Success)
            return NotFound(result);

        return Ok(result);
    }

    [HttpGet("api/v1/known-hosts/{id:guid}")]
    [RequirePermission(Permission.KnownHostsView)]
    public async Task<ActionResult<ApiResponse<KnownHostDto>>> GetById(Guid id, CancellationToken ct)
    {
        var result = await _knownHostService.GetByIdAsync(id, ct);

        if (!result.Success)
            return NotFound(result);

        return Ok(result);
    }

    [HttpPost("api/v1/connections/{connectionId:guid}/known-hosts")]
    [RequirePermission(Permission.KnownHostsManage)]
    public async Task<ActionResult<ApiResponse<KnownHostDto>>> Create(
        Guid connectionId,
        [FromBody] CreateKnownHostRequest request,
        CancellationToken ct)
    {
        var validation = await _createValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            var details = validation.Errors
                .Select(e => new FieldError(e.PropertyName, e.ErrorMessage))
                .ToList();

            return BadRequest(new ApiResponse<KnownHostDto>
            {
                Error = ErrorMessages.Create(ErrorCodes.ValidationFailed, "Validation failed.", details)
            });
        }

        var result = await _knownHostService.CreateAsync(connectionId, request, ct);

        if (!result.Success)
        {
            return result.Error!.Code switch
            {
                ErrorCodes.ResourceNotFound => NotFound(result),
                ErrorCodes.DuplicateKnownHostFingerprint => Conflict(result),
                _ => StatusCode(500, result)
            };
        }

        return Created($"/api/v1/known-hosts/{result.Data!.Id}", result);
    }

    [HttpDelete("api/v1/known-hosts/{id:guid}")]
    [RequirePermission(Permission.KnownHostsManage)]
    public async Task<ActionResult<ApiResponse>> Delete(Guid id, CancellationToken ct)
    {
        var result = await _knownHostService.DeleteAsync(id, ct);

        if (!result.Success)
        {
            return result.Error!.Code switch
            {
                ErrorCodes.KnownHostNotFound => NotFound(result),
                _ => StatusCode(500, result)
            };
        }

        return Ok(result);
    }

    [HttpPost("api/v1/known-hosts/{id:guid}/approve")]
    [RequirePermission(Permission.KnownHostsManage)]
    public async Task<ActionResult<ApiResponse<KnownHostDto>>> Approve(Guid id, CancellationToken ct)
    {
        var approvedBy = User.FindFirst("name")?.Value ?? "system";
        var result = await _knownHostService.ApproveAsync(id, approvedBy, ct);

        if (!result.Success)
        {
            return result.Error!.Code switch
            {
                ErrorCodes.KnownHostNotFound => NotFound(result),
                ErrorCodes.KnownHostAlreadyApproved => Conflict(result),
                _ => StatusCode(500, result)
            };
        }

        return Ok(result);
    }
}
