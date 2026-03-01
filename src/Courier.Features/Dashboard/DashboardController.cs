using Courier.Domain.Common;
using Courier.Features.Monitors;
using Microsoft.AspNetCore.Mvc;

namespace Courier.Features.Dashboard;

[ApiController]
[Route("api/v1/dashboard")]
public class DashboardController : ControllerBase
{
    private readonly DashboardService _dashboardService;

    public DashboardController(DashboardService dashboardService)
    {
        _dashboardService = dashboardService;
    }

    [HttpGet("summary")]
    public async Task<ActionResult<ApiResponse<DashboardSummaryDto>>> GetSummary(CancellationToken ct)
    {
        var result = await _dashboardService.GetSummaryAsync(ct);
        return Ok(result);
    }

    [HttpGet("recent-executions")]
    public async Task<ActionResult<ApiResponse<List<RecentExecutionDto>>>> GetRecentExecutions(
        [FromQuery] int count = 10,
        CancellationToken ct = default)
    {
        var result = await _dashboardService.GetRecentExecutionsAsync(count, ct);
        return Ok(result);
    }

    [HttpGet("active-monitors")]
    public async Task<ActionResult<ApiResponse<List<MonitorDto>>>> GetActiveMonitors(CancellationToken ct)
    {
        var result = await _dashboardService.GetActiveMonitorsAsync(ct);
        return Ok(result);
    }

    [HttpGet("key-expiry")]
    public async Task<ActionResult<ApiResponse<List<ExpiringKeyDto>>>> GetExpiringKeys(
        [FromQuery] int daysAhead = 30,
        CancellationToken ct = default)
    {
        var result = await _dashboardService.GetExpiringKeysAsync(daysAhead, ct);
        return Ok(result);
    }
}
