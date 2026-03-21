using Courier.Domain.Common;
using Courier.Domain.Enums;
using Courier.Features.Security;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Courier.Features.AuthProviders;

[ApiController]
[Route("api/v1/auth-providers")]
[Authorize]
public class AuthProvidersController : ControllerBase
{
    private readonly AuthProvidersService _authProvidersService;
    private readonly IValidator<CreateAuthProviderRequest> _createValidator;

    public AuthProvidersController(
        AuthProvidersService authProvidersService,
        IValidator<CreateAuthProviderRequest> createValidator)
    {
        _authProvidersService = authProvidersService;
        _createValidator = createValidator;
    }

    [HttpGet]
    [RequirePermission(Permission.AuthProvidersView)]
    public async Task<ActionResult<PagedApiResponse<AuthProviderDto>>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken ct = default)
    {
        var result = await _authProvidersService.ListAsync(page, pageSize, ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    [RequirePermission(Permission.AuthProvidersView)]
    public async Task<ActionResult<ApiResponse<AuthProviderDto>>> GetById(Guid id, CancellationToken ct)
    {
        var result = await _authProvidersService.GetByIdAsync(id, ct);

        if (!result.Success)
            return NotFound(result);

        return Ok(result);
    }

    [HttpPost]
    [RequirePermission(Permission.AuthProvidersCreate)]
    public async Task<ActionResult<ApiResponse<AuthProviderDto>>> Create(
        [FromBody] CreateAuthProviderRequest request,
        CancellationToken ct)
    {
        var validation = await _createValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            var details = validation.Errors
                .Select(e => new FieldError(e.PropertyName, e.ErrorMessage))
                .ToList();

            return BadRequest(new ApiResponse<AuthProviderDto>
            {
                Error = ErrorMessages.Create(ErrorCodes.ValidationFailed, "Validation failed.", details)
            });
        }

        var result = await _authProvidersService.CreateAsync(request, ct);
        return Created($"/api/v1/auth-providers/{result.Data!.Id}", result);
    }

    [HttpPut("{id:guid}")]
    [RequirePermission(Permission.AuthProvidersEdit)]
    public async Task<ActionResult<ApiResponse<AuthProviderDto>>> Update(
        Guid id,
        [FromBody] UpdateAuthProviderRequest request,
        [FromServices] IValidator<UpdateAuthProviderRequest> validator,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            var details = validation.Errors
                .Select(e => new FieldError(e.PropertyName, e.ErrorMessage))
                .ToList();

            return BadRequest(new ApiResponse<AuthProviderDto>
            {
                Error = ErrorMessages.Create(ErrorCodes.ValidationFailed, "Validation failed.", details)
            });
        }

        var result = await _authProvidersService.UpdateAsync(id, request, ct);

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
    [RequirePermission(Permission.AuthProvidersDelete)]
    public async Task<ActionResult<ApiResponse>> Delete(Guid id, CancellationToken ct)
    {
        var result = await _authProvidersService.DeleteAsync(id, ct);

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
    [RequirePermission(Permission.AuthProvidersEdit)]
    public async Task<ActionResult<ApiResponse<TestConnectionResultDto>>> TestConnection(Guid id, CancellationToken ct)
    {
        var result = await _authProvidersService.TestConnectionAsync(id, ct);

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

    [HttpGet("login-options")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<List<LoginOptionDto>>>> GetLoginOptions(CancellationToken ct)
    {
        var result = await _authProvidersService.GetLoginOptionsAsync(ct);
        return Ok(result);
    }
}
