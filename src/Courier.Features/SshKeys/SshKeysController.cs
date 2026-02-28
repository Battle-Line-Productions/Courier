using Courier.Domain.Common;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Courier.Features.SshKeys;

[ApiController]
[Route("api/v1/ssh-keys")]
public class SshKeysController : ControllerBase
{
    private readonly SshKeyService _sshKeyService;
    private readonly IValidator<GenerateSshKeyRequest> _generateValidator;

    public SshKeysController(
        SshKeyService sshKeyService,
        IValidator<GenerateSshKeyRequest> generateValidator)
    {
        _sshKeyService = sshKeyService;
        _generateValidator = generateValidator;
    }

    [HttpGet]
    public async Task<ActionResult<PagedApiResponse<SshKeyDto>>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? search = null,
        [FromQuery] string? status = null,
        [FromQuery] string? keyType = null,
        CancellationToken ct = default)
    {
        var result = await _sshKeyService.ListAsync(page, pageSize, search, status, keyType, ct);
        return Ok(result);
    }

    [HttpPost("generate")]
    public async Task<ActionResult<ApiResponse<SshKeyDto>>> Generate(
        [FromBody] GenerateSshKeyRequest request,
        CancellationToken ct)
    {
        var validation = await _generateValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            var details = validation.Errors
                .Select(e => new FieldError(e.PropertyName, e.ErrorMessage))
                .ToList();

            return BadRequest(new ApiResponse<SshKeyDto>
            {
                Error = ErrorMessages.Create(ErrorCodes.ValidationFailed, "Validation failed.", details)
            });
        }

        var result = await _sshKeyService.GenerateAsync(request, ct);

        if (!result.Success)
        {
            return StatusCode(500, result);
        }

        return Created($"/api/v1/ssh-keys/{result.Data!.Id}", result);
    }

    [HttpPost("import")]
    public async Task<ActionResult<ApiResponse<SshKeyDto>>> Import(
        [FromForm] ImportSshKeyRequest request,
        IFormFile keyFile,
        [FromServices] IValidator<ImportSshKeyRequest> validator,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            var details = validation.Errors
                .Select(e => new FieldError(e.PropertyName, e.ErrorMessage))
                .ToList();

            return BadRequest(new ApiResponse<SshKeyDto>
            {
                Error = ErrorMessages.Create(ErrorCodes.ValidationFailed, "Validation failed.", details)
            });
        }

        if (keyFile is null || keyFile.Length == 0)
        {
            return BadRequest(new ApiResponse<SshKeyDto>
            {
                Error = ErrorMessages.Create(ErrorCodes.KeyImportInvalidFormat, "A key file is required.")
            });
        }

        using var stream = keyFile.OpenReadStream();
        var result = await _sshKeyService.ImportAsync(request, stream, ct);

        if (!result.Success)
        {
            return result.Error!.Code switch
            {
                ErrorCodes.KeyImportInvalidFormat => BadRequest(result),
                _ => StatusCode(500, result)
            };
        }

        return Created($"/api/v1/ssh-keys/{result.Data!.Id}", result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<SshKeyDto>>> GetById(Guid id, CancellationToken ct)
    {
        var result = await _sshKeyService.GetByIdAsync(id, ct);

        if (!result.Success)
            return NotFound(result);

        return Ok(result);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponse<SshKeyDto>>> Update(
        Guid id,
        [FromBody] UpdateSshKeyRequest request,
        [FromServices] IValidator<UpdateSshKeyRequest> validator,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            var details = validation.Errors
                .Select(e => new FieldError(e.PropertyName, e.ErrorMessage))
                .ToList();

            return BadRequest(new ApiResponse<SshKeyDto>
            {
                Error = ErrorMessages.Create(ErrorCodes.ValidationFailed, "Validation failed.", details)
            });
        }

        var result = await _sshKeyService.UpdateAsync(id, request, ct);

        if (!result.Success)
        {
            return result.Error!.Code switch
            {
                ErrorCodes.KeyNotFound => NotFound(result),
                _ => StatusCode(500, result)
            };
        }

        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    public async Task<ActionResult<ApiResponse>> Delete(Guid id, CancellationToken ct)
    {
        var result = await _sshKeyService.DeleteAsync(id, ct);

        if (!result.Success)
        {
            return result.Error!.Code switch
            {
                ErrorCodes.KeyNotFound => NotFound(result),
                _ => StatusCode(500, result)
            };
        }

        return Ok(result);
    }

    [HttpGet("{id:guid}/export/public")]
    public async Task<ActionResult> ExportPublicKey(Guid id, CancellationToken ct)
    {
        var result = await _sshKeyService.ExportPublicKeyAsync(id, ct);

        if (!result.Success)
            return NotFound(result);

        var bytes = System.Text.Encoding.UTF8.GetBytes(result.Data!);
        return File(bytes, "text/plain", "key.pub");
    }

    [HttpPost("{id:guid}/retire")]
    public async Task<ActionResult<ApiResponse<SshKeyDto>>> Retire(Guid id, CancellationToken ct)
    {
        var result = await _sshKeyService.RetireAsync(id, ct);

        if (!result.Success)
        {
            return result.Error!.Code switch
            {
                ErrorCodes.KeyNotFound => NotFound(result),
                ErrorCodes.KeyAlreadyRetired or ErrorCodes.InvalidKeyTransition => Conflict(result),
                _ => StatusCode(500, result)
            };
        }

        return Ok(result);
    }

    [HttpPost("{id:guid}/activate")]
    public async Task<ActionResult<ApiResponse<SshKeyDto>>> Activate(Guid id, CancellationToken ct)
    {
        var result = await _sshKeyService.ActivateAsync(id, ct);

        if (!result.Success)
        {
            return result.Error!.Code switch
            {
                ErrorCodes.KeyNotFound => NotFound(result),
                ErrorCodes.KeyAlreadyActive or ErrorCodes.InvalidKeyTransition => Conflict(result),
                _ => StatusCode(500, result)
            };
        }

        return Ok(result);
    }
}
