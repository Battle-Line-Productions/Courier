using System.Security.Claims;
using Courier.Domain.Common;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Courier.Features.Users;

[ApiController]
[Route("api/v1/users")]
[Authorize(Roles = "admin")]
public class UsersController : ControllerBase
{
    private readonly UserService _userService;
    private readonly IValidator<CreateUserRequest> _createValidator;

    public UsersController(UserService userService, IValidator<CreateUserRequest> createValidator)
    {
        _userService = userService;
        _createValidator = createValidator;
    }

    [HttpGet]
    public async Task<ActionResult<PagedApiResponse<UserDto>>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? search = null,
        CancellationToken ct = default)
    {
        var result = await _userService.ListAsync(page, pageSize, search, ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<UserDto>>> GetById(Guid id, CancellationToken ct)
    {
        var result = await _userService.GetByIdAsync(id, ct);

        if (!result.Success)
            return NotFound(result);

        return Ok(result);
    }

    [HttpPost]
    public async Task<ActionResult<ApiResponse<UserDto>>> Create(
        [FromBody] CreateUserRequest request,
        CancellationToken ct)
    {
        var validation = await _createValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            var details = validation.Errors
                .Select(e => new FieldError(e.PropertyName, e.ErrorMessage))
                .ToList();

            return BadRequest(new ApiResponse<UserDto>
            {
                Error = ErrorMessages.Create(ErrorCodes.ValidationFailed, "Validation failed.", details)
            });
        }

        var performedBy = User.FindFirst("name")?.Value ?? "system";
        var result = await _userService.CreateAsync(request, performedBy, ct);

        if (!result.Success)
        {
            return result.Error!.Code switch
            {
                ErrorCodes.DuplicateUsername => Conflict(result),
                ErrorCodes.WeakPassword => BadRequest(result),
                _ => StatusCode(500, result)
            };
        }

        return Created($"/api/v1/users/{result.Data!.Id}", result);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponse<UserDto>>> Update(
        Guid id,
        [FromBody] UpdateUserRequest request,
        [FromServices] IValidator<UpdateUserRequest> validator,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            var details = validation.Errors
                .Select(e => new FieldError(e.PropertyName, e.ErrorMessage))
                .ToList();

            return BadRequest(new ApiResponse<UserDto>
            {
                Error = ErrorMessages.Create(ErrorCodes.ValidationFailed, "Validation failed.", details)
            });
        }

        var performedById = GetCurrentUserId();
        if (performedById is null)
            return Unauthorized();

        var result = await _userService.UpdateAsync(id, request, performedById.Value, ct);

        if (!result.Success)
        {
            return result.Error!.Code switch
            {
                ErrorCodes.UserNotFound => NotFound(result),
                ErrorCodes.CannotDemoteLastAdmin => Conflict(result),
                _ => StatusCode(500, result)
            };
        }

        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<ApiResponse>> Delete(Guid id, CancellationToken ct)
    {
        var performedById = GetCurrentUserId();
        if (performedById is null)
            return Unauthorized();

        var result = await _userService.DeleteAsync(id, performedById.Value, ct);

        if (!result.Success)
        {
            return result.Error!.Code switch
            {
                ErrorCodes.UserNotFound => NotFound(result),
                ErrorCodes.CannotDeleteSelf => Conflict(result),
                ErrorCodes.CannotDemoteLastAdmin => Conflict(result),
                _ => StatusCode(500, result)
            };
        }

        return Ok(result);
    }

    [HttpPost("{id:guid}/reset-password")]
    public async Task<ActionResult<ApiResponse>> ResetPassword(
        Guid id,
        [FromBody] NewPasswordRequest request,
        CancellationToken ct)
    {
        var performedBy = User.FindFirst("name")?.Value ?? "system";
        var result = await _userService.ResetPasswordAsync(id, request, performedBy, ct);

        if (!result.Success)
        {
            return result.Error!.Code switch
            {
                ErrorCodes.UserNotFound => NotFound(result),
                ErrorCodes.WeakPassword => BadRequest(result),
                _ => StatusCode(500, result)
            };
        }

        return Ok(result);
    }

    private Guid? GetCurrentUserId()
    {
        var claim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                    ?? User.FindFirst("sub")?.Value;
        return claim is not null && Guid.TryParse(claim, out var id) ? id : null;
    }
}
