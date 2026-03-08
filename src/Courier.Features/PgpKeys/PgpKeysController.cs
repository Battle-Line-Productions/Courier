using Courier.Domain.Common;
using Courier.Features.Keys;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Courier.Features.PgpKeys;

[ApiController]
[Route("api/v1/pgp-keys")]
[Authorize]
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
        [FromQuery] string? tag = null,
        CancellationToken ct = default)
    {
        var result = await _pgpKeyService.ListAsync(page, pageSize, search, status, keyType, algorithm, tag, ct);
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

    [HttpPost("{id:guid}/set-successor")]
    public async Task<ActionResult<ApiResponse>> SetSuccessor(Guid id, [FromBody] SetSuccessorRequest request, CancellationToken ct)
    {
        var result = await _pgpKeyService.SetSuccessorAsync(id, request.SuccessorKeyId, ct);

        if (!result.Success)
        {
            return result.Error!.Code switch
            {
                ErrorCodes.KeyNotFound => NotFound(result),
                ErrorCodes.KeySuccessorSelfReference or
                ErrorCodes.KeySuccessorCircularChain or
                ErrorCodes.KeySuccessorInvalidStatus => Conflict(result),
                _ => StatusCode(500, result)
            };
        }

        return Ok(result);
    }

    // --- Share Link Endpoints ---

    [HttpPost("{id:guid}/share")]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult<ApiResponse<ShareLinkResponse>>> CreateShareLink(
        Guid id,
        [FromBody] CreateShareLinkRequest request,
        [FromServices] KeyShareService keyShareService,
        CancellationToken ct)
    {
        var createdBy = User.FindFirst("name")?.Value ?? "system";
        var result = await keyShareService.CreateShareLinkAsync(id, "pgp", createdBy, request.ExpiryDays, ct);

        if (!result.Success)
        {
            return result.Error!.Code switch
            {
                ErrorCodes.ShareLinksDisabled => StatusCode(403, result),
                ErrorCodes.KeyNotFound => NotFound(result),
                _ => StatusCode(500, result)
            };
        }

        return Created($"/api/v1/pgp-keys/{id}/share/{result.Data!.LinkId}", result);
    }

    [HttpGet("{id:guid}/share")]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult<ApiResponse<List<ShareLinkListItem>>>> ListShareLinks(
        Guid id,
        [FromServices] KeyShareService keyShareService,
        CancellationToken ct)
    {
        var result = await keyShareService.ListShareLinksAsync(id, "pgp", ct);
        return Ok(result);
    }

    [HttpDelete("{id:guid}/share/{linkId:guid}")]
    [Authorize(Roles = "admin")]
    public async Task<ActionResult<ApiResponse>> RevokeShareLink(
        Guid id,
        Guid linkId,
        [FromServices] KeyShareService keyShareService,
        CancellationToken ct)
    {
        var result = await keyShareService.RevokeShareLinkAsync(linkId, ct);

        if (!result.Success)
        {
            return result.Error!.Code switch
            {
                ErrorCodes.ShareLinkNotFound => NotFound(result),
                ErrorCodes.ShareLinkRevoked => Conflict(result),
                _ => StatusCode(500, result)
            };
        }

        return Ok(result);
    }

    [HttpGet("shared/{token}")]
    [AllowAnonymous]
    public async Task<ActionResult<ApiResponse<SharedKeyResponse>>> GetSharedKey(
        string token,
        [FromServices] KeyShareService keyShareService,
        CancellationToken ct)
    {
        var result = await keyShareService.GetSharedKeyAsync(token, "pgp", ct);

        if (!result.Success)
        {
            return result.Error!.Code switch
            {
                ErrorCodes.ShareLinksDisabled => StatusCode(403, result),
                ErrorCodes.ShareLinkInvalidToken => NotFound(result),
                ErrorCodes.KeyNotFound => NotFound(result),
                _ => StatusCode(500, result)
            };
        }

        return Ok(result);
    }
}
