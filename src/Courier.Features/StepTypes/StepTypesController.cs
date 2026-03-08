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
            .Select(MapToDto)
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

        return Ok(new ApiResponse<StepTypeMetadataDto> { Data = MapToDto(metadata) });
    }

    private static StepTypeMetadataDto MapToDto(StepTypeMetadata m) => new(
        m.TypeKey, m.DisplayName, m.Category, m.Description,
        Outputs: m.Outputs?.Select(o => new StepOutputMetaDto(o.Key, o.Description, o.ValueType, o.Conditional)).ToList(),
        Inputs: m.Inputs?.Select(i => new StepInputMetaDto(i.Key, i.Description, i.Required, i.SupportsContextRef)).ToList());
}

public record StepOutputMetaDto(string Key, string Description, string ValueType, bool Conditional);
public record StepInputMetaDto(string Key, string Description, bool Required, bool SupportsContextRef);
public record StepTypeMetadataDto(
    string TypeKey,
    string DisplayName,
    string Category,
    string Description,
    List<StepOutputMetaDto>? Outputs = null,
    List<StepInputMetaDto>? Inputs = null);
