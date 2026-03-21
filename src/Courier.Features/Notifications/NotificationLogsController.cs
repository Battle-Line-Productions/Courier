using Courier.Domain.Common;
using Courier.Domain.Enums;
using Courier.Features.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Courier.Features.Notifications;

[ApiController]
[Route("api/v1/notification-logs")]
[Authorize]
public class NotificationLogsController : ControllerBase
{
    private readonly NotificationLogService _logService;

    public NotificationLogsController(NotificationLogService logService)
    {
        _logService = logService;
    }

    [HttpGet]
    [RequirePermission(Permission.NotificationLogsView)]
    public async Task<ActionResult<PagedApiResponse<NotificationLogDto>>> List(
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 25,
        [FromQuery] Guid? ruleId = null,
        [FromQuery] string? entityType = null,
        [FromQuery] Guid? entityId = null,
        [FromQuery] bool? success = null,
        CancellationToken ct = default)
    {
        var result = await _logService.ListAsync(page, pageSize, ruleId, entityType, entityId, success, ct);
        return Ok(result);
    }
}
