using Courier.Domain.Common;
using Courier.Features.Engine;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Courier.Features.StepTypes;

[ApiController]
[Route("api/v1/step-types")]
[Authorize]
public class StepTypesController : ControllerBase
{
    private readonly StepTypeRegistry _registry;

    public StepTypesController(StepTypeRegistry registry)
    {
        _registry = registry;
    }

    [HttpGet]
    public ActionResult<ApiResponse<List<StepTypeMetadataDto>>> GetAll()
    {
        var metadata = _registry.GetAllMetadata()
            .Select(m => new StepTypeMetadataDto(m.TypeKey, m.DisplayName, m.Category, m.Description))
            .OrderBy(m => m.Category)
            .ThenBy(m => m.TypeKey)
            .ToList();

        return Ok(new ApiResponse<List<StepTypeMetadataDto>> { Data = metadata });
    }

    [HttpGet("{typeKey}")]
    public ActionResult<ApiResponse<StepTypeMetadataDto>> GetByTypeKey(string typeKey)
    {
        var metadata = _registry.GetMetadata(typeKey);
        if (metadata is null)
        {
            return NotFound(new ApiResponse<StepTypeMetadataDto>
            {
                Error = ErrorMessages.Create(ErrorCodes.ResourceNotFound, $"Step type '{typeKey}' not found.")
            });
        }

        var dto = new StepTypeMetadataDto(metadata.TypeKey, metadata.DisplayName, metadata.Category, metadata.Description);
        return Ok(new ApiResponse<StepTypeMetadataDto> { Data = dto });
    }
}

public record StepTypeMetadataDto(string TypeKey, string DisplayName, string Category, string Description);
