using Courier.Domain.Common;
using FluentValidation;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Courier.Features.Tags;

[ApiController]
[Route("api/v1/tags")]
[Authorize]
public class TagsController : ControllerBase
{
    private readonly TagService _tagService;
    private readonly IValidator<CreateTagRequest> _createValidator;

    public TagsController(
        TagService tagService,
        IValidator<CreateTagRequest> createValidator)
    {
        _tagService = tagService;
        _createValidator = createValidator;
    }

    [HttpPost]
    [Authorize(Roles = "admin,operator")]
    public async Task<ActionResult<ApiResponse<TagDto>>> Create(
        [FromBody] CreateTagRequest request,
        CancellationToken ct)
    {
        var validation = await _createValidator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            var details = validation.Errors
                .Select(e => new FieldError(e.PropertyName, e.ErrorMessage))
                .ToList();

            return BadRequest(new ApiResponse<TagDto>
            {
                Error = ErrorMessages.Create(ErrorCodes.ValidationFailed, "Validation failed.", details)
            });
        }

        var result = await _tagService.CreateAsync(request, ct);

        if (!result.Success)
        {
            return result.Error!.Code switch
            {
                ErrorCodes.DuplicateTagName => Conflict(result),
                _ => StatusCode(500, result)
            };
        }

        return Created($"/api/v1/tags/{result.Data!.Id}", result);
    }

    [HttpGet]
    public async Task<ActionResult<PagedApiResponse<TagDto>>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] string? search = null,
        [FromQuery] string? category = null,
        CancellationToken ct = default)
    {
        var result = await _tagService.ListAsync(page, pageSize, search, category, ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}")]
    public async Task<ActionResult<ApiResponse<TagDto>>> GetById(Guid id, CancellationToken ct)
    {
        var result = await _tagService.GetByIdAsync(id, ct);

        if (!result.Success)
            return NotFound(result);

        return Ok(result);
    }

    [HttpPut("{id:guid}")]
    [Authorize(Roles = "admin,operator")]
    public async Task<ActionResult<ApiResponse<TagDto>>> Update(
        Guid id,
        [FromBody] UpdateTagRequest request,
        [FromServices] IValidator<UpdateTagRequest> validator,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            var details = validation.Errors
                .Select(e => new FieldError(e.PropertyName, e.ErrorMessage))
                .ToList();

            return BadRequest(new ApiResponse<TagDto>
            {
                Error = ErrorMessages.Create(ErrorCodes.ValidationFailed, "Validation failed.", details)
            });
        }

        var result = await _tagService.UpdateAsync(id, request, ct);

        if (!result.Success)
        {
            return result.Error!.Code switch
            {
                ErrorCodes.ResourceNotFound => NotFound(result),
                ErrorCodes.DuplicateTagName => Conflict(result),
                _ => StatusCode(500, result)
            };
        }

        return Ok(result);
    }

    [HttpDelete("{id:guid}")]
    [Authorize(Roles = "admin,operator")]
    public async Task<ActionResult<ApiResponse>> Delete(Guid id, CancellationToken ct)
    {
        var result = await _tagService.DeleteAsync(id, ct);

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

    [HttpPost("assign")]
    [Authorize(Roles = "admin,operator")]
    public async Task<ActionResult<ApiResponse>> Assign(
        [FromBody] BulkTagAssignmentRequest request,
        [FromServices] IValidator<BulkTagAssignmentRequest> validator,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            var details = validation.Errors
                .Select(e => new FieldError(e.PropertyName, e.ErrorMessage))
                .ToList();

            return BadRequest(new ApiResponse
            {
                Error = ErrorMessages.Create(ErrorCodes.ValidationFailed, "Validation failed.", details)
            });
        }

        var result = await _tagService.AssignTagsAsync(request, ct);

        if (!result.Success)
        {
            return result.Error!.Code switch
            {
                ErrorCodes.ResourceNotFound => NotFound(result),
                ErrorCodes.InvalidTagEntityType => BadRequest(result),
                ErrorCodes.TagEntityNotFound => NotFound(result),
                _ => StatusCode(500, result)
            };
        }

        return Ok(result);
    }

    [HttpPost("unassign")]
    [Authorize(Roles = "admin,operator")]
    public async Task<ActionResult<ApiResponse>> Unassign(
        [FromBody] BulkTagAssignmentRequest request,
        [FromServices] IValidator<BulkTagAssignmentRequest> validator,
        CancellationToken ct)
    {
        var validation = await validator.ValidateAsync(request, ct);
        if (!validation.IsValid)
        {
            var details = validation.Errors
                .Select(e => new FieldError(e.PropertyName, e.ErrorMessage))
                .ToList();

            return BadRequest(new ApiResponse
            {
                Error = ErrorMessages.Create(ErrorCodes.ValidationFailed, "Validation failed.", details)
            });
        }

        var result = await _tagService.UnassignTagsAsync(request, ct);
        return Ok(result);
    }

    [HttpGet("{id:guid}/entities")]
    public async Task<ActionResult<PagedApiResponse<TagEntityDto>>> ListEntities(
        Guid id,
        [FromQuery] string? entityType = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken ct = default)
    {
        var result = await _tagService.ListEntitiesByTagAsync(id, entityType, page, pageSize, ct);

        if (!result.Success)
            return NotFound(result);

        return Ok(result);
    }
}
