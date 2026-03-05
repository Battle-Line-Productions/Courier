using System.Security.Claims;
using Courier.Domain.Common;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Courier.Features.Auth;

[ApiController]
[Route("api/v1/auth")]
public class AuthController : ControllerBase
{
    private readonly AuthService _authService;
    private readonly IValidator<LoginRequest> _loginValidator;

    public AuthController(AuthService authService, IValidator<LoginRequest> loginValidator)
    {
        _authService = authService;
        _loginValidator = loginValidator;
    }

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<LoginResponse>>> Login(
        [FromBody] LoginRequest request,
        CancellationToken ct)
    {
        var validation = await _loginValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            var details = validation.Errors
                .Select(e => new FieldError(e.PropertyName, e.ErrorMessage))
                .ToList();

            return BadRequest(new ApiResponse<LoginResponse>
            {
                Error = ErrorMessages.Create(ErrorCodes.ValidationFailed, "Validation failed.", details)
            });
        }

        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await _authService.LoginAsync(request, ipAddress, ct);

        if (!result.Success)
        {
            return result.Error!.Code switch
            {
                ErrorCodes.InvalidCredentials => Unauthorized(result),
                ErrorCodes.AccountLocked => StatusCode(423, result),
                ErrorCodes.AccountDisabled => StatusCode(403, result),
                _ => StatusCode(500, result)
            };
        }

        return Ok(result);
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<LoginResponse>>> Refresh(
        [FromBody] RefreshRequest request,
        CancellationToken ct)
    {
        var ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString();
        var result = await _authService.RefreshAsync(request.RefreshToken, ipAddress, ct);

        if (!result.Success)
        {
            return result.Error!.Code switch
            {
                ErrorCodes.InvalidRefreshToken => Unauthorized(result),
                ErrorCodes.RefreshTokenExpired => Unauthorized(result),
                ErrorCodes.AccountDisabled => StatusCode(403, result),
                _ => StatusCode(500, result)
            };
        }

        return Ok(result);
    }

    [HttpPost("logout")]
    [Authorize]
    public async Task<ActionResult<ApiResponse>> Logout(
        [FromBody] RefreshRequest request,
        CancellationToken ct)
    {
        var result = await _authService.LogoutAsync(request.RefreshToken, ct);
        return Ok(result);
    }

    [HttpGet("me")]
    [Authorize]
    public async Task<ActionResult<ApiResponse<UserProfileDto>>> Me(CancellationToken ct)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                          ?? User.FindFirst("sub")?.Value;

        if (userIdClaim is null || !Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new ApiResponse<UserProfileDto>
            {
                Error = ErrorMessages.Create(ErrorCodes.Unauthorized, "Invalid token.")
            });
        }

        var result = await _authService.GetCurrentUserAsync(userId, ct);

        if (!result.Success)
            return NotFound(result);

        return Ok(result);
    }

    [HttpPost("change-password")]
    [Authorize]
    public async Task<ActionResult<ApiResponse>> ChangePassword(
        [FromBody] ChangePasswordRequest request,
        CancellationToken ct)
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier)?.Value
                          ?? User.FindFirst("sub")?.Value;

        if (userIdClaim is null || !Guid.TryParse(userIdClaim, out var userId))
        {
            return Unauthorized(new ApiResponse
            {
                Error = ErrorMessages.Create(ErrorCodes.Unauthorized, "Invalid token.")
            });
        }

        var result = await _authService.ChangePasswordAsync(userId, request, ct);

        if (!result.Success)
        {
            return result.Error!.Code switch
            {
                ErrorCodes.UserNotFound => NotFound(result),
                ErrorCodes.InvalidCurrentPassword => BadRequest(result),
                ErrorCodes.WeakPassword => BadRequest(result),
                _ => StatusCode(500, result)
            };
        }

        return Ok(result);
    }
}
