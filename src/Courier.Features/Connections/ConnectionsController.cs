using Courier.Domain.Common;
using Courier.Domain.Enums;
using Courier.Features.Security;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Courier.Features.Connections;

[ApiController]
[Route("api/v1/connections")]
[Authorize]
public class ConnectionsController : ControllerBase
{
    private readonly ConnectionService _connectionService;
    private readonly IValidator<CreateConnectionRequest> _createValidator;

    public ConnectionsController(
        ConnectionService connectionService,
        IValidator<CreateConnectionRequest> createValidator)
    {
        _connectionService = connectionService;
        _createValidator = createValidator;
    }

    [HttpPost]
    [RequirePermission(Permission.ConnectionsCreate)]
    public async Task<ActionResult<ApiResponse<ConnectionDto>>> Create(
        [FromBody] CreateConnectionRequest request,
        CancellationToken ct)
    {
        var validation = await _createValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            var details = validation.Errors
                .Select(e => new FieldError(e.PropertyName, e.ErrorMessage))
                .ToList();

            return BadRequest(new ApiResponse<ConnectionDto>
            {
                Error = ErrorMessages.Create(ErrorCodes.ValidationFailed, "Validation failed.", details)
            });
        }

        var result = await _connectionService.CreateAsync(request, ct);
        return Created($"/api/v1/connections/{result.Data!.Id}", result);
    }

    [HttpGet]
    [RequirePermission(Permission.ConnectionsView)]
    public async Task<ActionResult<PagedApiResponse<ConnectionDto>>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? search = null,
        [FromQuery] string? protocol = null,
        [FromQuery] string? group = null,
        [FromQuery] string? status = null,
        [FromQuery] string? tag = null,
        CancellationToken ct = default)
    {
        var result = await _connectionService.ListAsync(page, pageSize, search, protocol, group, status, tag, ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [RequirePermission(Permission.ConnectionsView)]
    public async Task<ActionResult<ApiResponse<ConnectionDto>>> GetById(Guid id, CancellationToken ct)
    {
        var result = await _connectionService.GetByIdAsync(id, ct);

        if (!result.Success)
            return NotFound(result);

        return Ok(result);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(Permission.ConnectionsEdit)]
    public async Task<ActionResult<ApiResponse<ConnectionDto>>> Update(
        Guid id,
        [FromBody] UpdateConnectionRequest request,
        [FromServices] IValidator<UpdateConnectionRequest> validator,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            var details = validation.Errors
                .Select(e => new FieldError(e.PropertyName, e.ErrorMessage))
                .ToList();

            return BadRequest(new ApiResponse<ConnectionDto>
            {
                Error = ErrorMessages.Create(ErrorCodes.ValidationFailed, "Validation failed.", details)
            });
        }

        var result = await _connectionService.UpdateAsync(id, request, ct);

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
    [RequirePermission(Permission.ConnectionsDelete)]
    public async Task<ActionResult<ApiResponse>> Delete(Guid id, CancellationToken ct)
    {
        var result = await _connectionService.DeleteAsync(id, ct);

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

    [HttpPost("{id:guid}/test")]
    [RequirePermission(Permission.ConnectionsTest)]
    public async Task<ActionResult<ApiResponse<ConnectionTestDto>>> TestConnection(Guid id, CancellationToken ct)
    {
        var result = await _connectionService.TestConnectionAsync(id, ct);

        if (!result.Success)
        {
            return result.Error!.Code switch
            {
                ErrorCodes.ResourceNotFound => NotFound(result),
                ErrorCodes.InvalidProtocolConfig => BadRequest(result),
                _ => StatusCode(500, result)
            };
        }

        return Ok(result);
    }
}
