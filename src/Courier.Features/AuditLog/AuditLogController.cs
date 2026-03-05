using Courier.Domain.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Courier.Features.AuditLog;

[ApiController]
[Route("api/v1/audit-log")]
[Authorize]
public class AuditLogController : ControllerBase
{
    private readonly AuditService _auditService;

    public AuditLogController(AuditService auditService)
    {
        _auditService = auditService;
    }

    [HttpGet]
    public async Task<ActionResult<PagedApiResponse<AuditLogEntryDto>>> List(
        [FromQuery] string? entityType = null,
        [FromQuery] Guid? entityId = null,
        [FromQuery] string? operation = null,
        [FromQuery] string? performedBy = null,
        [FromQuery] DateTime? from = null,
        [FromQuery] DateTime? to = null,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken ct = default)
    {
        var filter = new AuditLogFilter
        {
            EntityType = entityType,
            EntityId = entityId,
            Operation = operation,
            PerformedBy = performedBy,
            From = from,
            To = to,
        };

        var result = await _auditService.ListAsync(filter, page, pageSize, ct);
        return Ok(result);
    }

    [HttpGet("entity/{entityType}/{entityId:guid}")]
    public async Task<ActionResult<PagedApiResponse<AuditLogEntryDto>>> ListByEntity(
        string entityType,
        Guid entityId,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        CancellationToken ct = default)
    {
        var result = await _auditService.ListByEntityAsync(entityType, entityId, page, pageSize, ct);
        return Ok(result);
    }
}
