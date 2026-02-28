using Courier.Domain.Common;
using FluentValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Courier.Features.PgpKeys;

[ApiController]
[Route("api/v1/pgp-keys")]
public class PgpKeysController : ControllerBase
{
    private readonly PgpKeyService _pgpKeyService;
    private readonly IValidator<GeneratePgpKeyRequest> _generateValidator;

    public PgpKeysController(
        PgpKeyService pgpKeyService,
        IValidator<GeneratePgpKeyRequest> generateValidator)
    {
        _pgpKeyService = pgpKeyService;
        _generateValidator = generateValidator;
    }

    [HttpGet]
    public async Task<ActionResult<PagedApiResponse<PgpKeyDto>>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? search = null,
        [FromQuery] string? status = null,
        [FromQuery] string? keyType = null,
        [FromQuery] string? algorithm = null,
        CancellationToken ct = default)
    {
        var result = await _pgpKeyService.ListAsync(page, pageSize, search, status, keyType, algorithm, ct);
        return Ok(result);
    }

    [HttpPost("generate")]
    public async Task<ActionResult<ApiResponse<PgpKeyDto>>> Generate(
        [FromBody] GeneratePgpKeyRequest request,
        CancellationToken ct)
    {
        var validation = await _generateValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            var details = validation.Errors
                .Select(e => new FieldError(e.PropertyName, e.ErrorMessage))
                .ToList();

            return BadRequest(new ApiResponse<PgpKeyDto>
            {
                Error = ErrorMessages.Create(ErrorCodes.ValidationFailed, "Validation failed.", details)
            });
        }

        var result = await _pgpKeyService.GenerateAsync(request, ct);

        if (!result.Success)
        {
            return StatusCode(500, result);
        }

        return Created($"/api/v1/pgp-keys/{result.Data!.Id}", result);
    }

    [HttpPost("import")]
    public async Task<ActionResult<ApiResponse<PgpKeyDto>>> Import(
        [FromForm] ImportPgpKeyRequest request,
        IFormFile keyFile,
        [FromServices] IValidator<ImportPgpKeyRequest> validator,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            var details = validation.Errors
                .Select(e => new FieldError(e.PropertyName, e.ErrorMessage))
                .ToList();

            return BadRequest(new ApiResponse<PgpKeyDto>
            {
                Error = ErrorMessages.Create(ErrorCodes.ValidationFailed, "Validation failed.", details)
            });
        }

        if (keyFile is null || keyFile.Length == 0)
        {
            return BadRequest(new ApiResponse<PgpKeyDto>
            {
                Error = ErrorMessages.Create(ErrorCodes.KeyImportInvalidFormat, "A key file is required.")
            });
        }

        using var stream = keyFile.OpenReadStream();
        var result = await _pgpKeyService.ImportAsync(request, stream, ct);

        if (!result.Success)
        {
            return result.Error!.Code switch
            {
                ErrorCodes.KeyImportInvalidFormat => BadRequest(result),
                _ => StatusCode(500, result)
            };
        }

        return Created($"/api/v1/pgp-keys/{result.Data!.Id}", result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<PgpKeyDto>>> GetById(Guid id, CancellationToken ct)
    {
        var result = await _pgpKeyService.GetByIdAsync(id, ct);

        if (!result.Success)
            return NotFound(result);

        return Ok(result);
    }

    [HttpPut("{id:guid}")]
    public async Task<ActionResult<ApiResponse<PgpKeyDto>>> Update(
        Guid id,
        [FromBody] UpdatePgpKeyRequest request,
        [FromServices] IValidator<UpdatePgpKeyRequest> validator,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            var details = validation.Errors
                .Select(e => new FieldError(e.PropertyName, e.ErrorMessage))
                .ToList();

            return BadRequest(new ApiResponse<PgpKeyDto>
            {
                Error = ErrorMessages.Create(ErrorCodes.ValidationFailed, "Validation failed.", details)
            });
        }

        var result = await _pgpKeyService.UpdateAsync(id, request, ct);

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
        var result = await _pgpKeyService.DeleteAsync(id, ct);

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
        var result = await _pgpKeyService.ExportPublicKeyAsync(id, ct);

        if (!result.Success)
            return NotFound(result);

        var bytes = System.Text.Encoding.UTF8.GetBytes(result.Data!);
        return File(bytes, "application/pgp-keys", "public.asc");
    }

    [HttpPost("{id:guid}/retire")]
    public async Task<ActionResult<ApiResponse<PgpKeyDto>>> Retire(Guid id, CancellationToken ct)
    {
        var result = await _pgpKeyService.RetireAsync(id, ct);

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

    [HttpPost("{id:guid}/revoke")]
    public async Task<ActionResult<ApiResponse<PgpKeyDto>>> Revoke(Guid id, CancellationToken ct)
    {
        var result = await _pgpKeyService.RevokeAsync(id, ct);

        if (!result.Success)
        {
            return result.Error!.Code switch
            {
                ErrorCodes.KeyNotFound => NotFound(result),
                ErrorCodes.KeyAlreadyRevoked or ErrorCodes.InvalidKeyTransition => Conflict(result),
                _ => StatusCode(500, result)
            };
        }

        return Ok(result);
    }

    [HttpPost("{id:guid}/activate")]
    public async Task<ActionResult<ApiResponse<PgpKeyDto>>> Activate(Guid id, CancellationToken ct)
    {
        var result = await _pgpKeyService.ActivateAsync(id, ct);

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
