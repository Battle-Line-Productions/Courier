using System.Security.Claims;
using Courier.Domain.Common;
using Courier.Domain.Enums;
using Courier.Features.Security;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Courier.Features.Settings;

[ApiController]
[Route("api/v1/settings")]
[Authorize]
public class SettingsController : ControllerBase
{
    private readonly SettingsService _settingsService;

    public SettingsController(SettingsService settingsService)
    {
        _settingsService = settingsService;
    }

    [HttpGet("auth")]
    public async Task<ActionResult<ApiResponse<AuthSettingsDto>>> GetAuthSettings(CancellationToken ct)
    {
        var result = await _settingsService.GetAuthSettingsAsync(ct);
        return Ok(result);
    }

    [HttpPut("auth")]
    [RequirePermission(Permission.SettingsManage)]
    public async Task<ActionResult<ApiResponse<AuthSettingsDto>>> UpdateAuthSettings(
        [FromBody] UpdateAuthSettingsRequest request,
        CancellationToken ct)
    {
        var performedBy = User.FindFirst("name")?.Value ?? "system";
        var result = await _settingsService.UpdateAuthSettingsAsync(request, performedBy, ct);
        return Ok(result);
    }
}
