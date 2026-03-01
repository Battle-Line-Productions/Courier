using Courier.Domain.Entities;
using Courier.Domain.Enums;
using Courier.Domain.Common;
using Courier.Features.Monitors;
using Courier.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Courier.Features.Dashboard;

public class DashboardService
{
    private readonly CourierDbContext _db;

    public DashboardService(CourierDbContext db)
    {
        _db = db;
    }

    public async Task<ApiResponse<DashboardSummaryDto>> GetSummaryAsync(CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var since24H = now.AddHours(-24);
        var since7D = now.AddDays(-7);

        var totalJobs = await _db.Jobs.CountAsync(ct);
        var totalConnections = await _db.Connections.CountAsync(ct);
        var totalMonitors = await _db.FileMonitors.CountAsync(ct);
        var totalPgpKeys = await _db.PgpKeys.CountAsync(ct);
        var totalSshKeys = await _db.SshKeys.CountAsync(ct);

        var executions24H = await _db.JobExecutions
            .Where(e => e.CreatedAt >= since24H)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Total = g.Count(),
                Succeeded = g.Count(e => e.State == JobExecutionState.Completed),
                Failed = g.Count(e => e.State == JobExecutionState.Failed),
            })
            .FirstOrDefaultAsync(ct);

        var executions7D = await _db.JobExecutions
            .Where(e => e.CreatedAt >= since7D)
            .GroupBy(_ => 1)
            .Select(g => new
            {
                Total = g.Count(),
                Succeeded = g.Count(e => e.State == JobExecutionState.Completed),
                Failed = g.Count(e => e.State == JobExecutionState.Failed),
            })
            .FirstOrDefaultAsync(ct);

        var dto = new DashboardSummaryDto
        {
            TotalJobs = totalJobs,
            TotalConnections = totalConnections,
            TotalMonitors = totalMonitors,
            TotalPgpKeys = totalPgpKeys,
            TotalSshKeys = totalSshKeys,
            Executions24H = executions24H?.Total ?? 0,
            ExecutionsSucceeded24H = executions24H?.Succeeded ?? 0,
            ExecutionsFailed24H = executions24H?.Failed ?? 0,
            Executions7D = executions7D?.Total ?? 0,
            ExecutionsSucceeded7D = executions7D?.Succeeded ?? 0,
            ExecutionsFailed7D = executions7D?.Failed ?? 0,
        };

        return new ApiResponse<DashboardSummaryDto> { Data = dto };
    }

    public async Task<ApiResponse<List<RecentExecutionDto>>> GetRecentExecutionsAsync(
        int count = 10,
        CancellationToken ct = default)
    {
        count = Math.Clamp(count, 1, 50);

        var executions = await _db.JobExecutions
            .Include(e => e.Job)
            .OrderByDescending(e => e.CreatedAt)
            .Take(count)
            .Select(e => new RecentExecutionDto
            {
                Id = e.Id,
                JobId = e.JobId,
                JobName = e.Job.Name,
                State = e.State.ToString().ToLower(),
                TriggeredBy = e.TriggeredBy,
                StartedAt = e.StartedAt,
                CompletedAt = e.CompletedAt,
                CreatedAt = e.CreatedAt,
            })
            .ToListAsync(ct);

        return new ApiResponse<List<RecentExecutionDto>> { Data = executions };
    }

    public async Task<ApiResponse<List<MonitorDto>>> GetActiveMonitorsAsync(CancellationToken ct = default)
    {
        var monitors = await _db.FileMonitors
            .Include(m => m.Bindings).ThenInclude(b => b.Job)
            .Where(m => m.State == "active" || m.State == "paused" || m.State == "error")
            .OrderByDescending(m => m.UpdatedAt)
            .ToListAsync(ct);

        var dtos = monitors.Select(MapMonitorToDto).ToList();

        return new ApiResponse<List<MonitorDto>> { Data = dtos };
    }

    public async Task<ApiResponse<List<ExpiringKeyDto>>> GetExpiringKeysAsync(
        int daysAhead = 30,
        CancellationToken ct = default)
    {
        daysAhead = Math.Clamp(daysAhead, 1, 365);
        var cutoff = DateTime.UtcNow.AddDays(daysAhead);

        var keys = await _db.PgpKeys
            .Where(k => k.ExpiresAt != null && k.ExpiresAt <= cutoff)
            .OrderBy(k => k.ExpiresAt)
            .Select(k => new ExpiringKeyDto
            {
                Id = k.Id,
                Name = k.Name,
                KeyType = k.KeyType,
                Fingerprint = k.Fingerprint,
                ExpiresAt = k.ExpiresAt!.Value,
                DaysUntilExpiry = (int)(k.ExpiresAt!.Value - DateTime.UtcNow).TotalDays,
            })
            .ToListAsync(ct);

        return new ApiResponse<List<ExpiringKeyDto>> { Data = keys };
    }

    private static MonitorDto MapMonitorToDto(FileMonitor m) => new()
    {
        Id = m.Id,
        Name = m.Name,
        Description = m.Description,
        WatchTarget = m.WatchTarget,
        TriggerEvents = m.TriggerEvents,
        FilePatterns = m.FilePatterns,
        PollingIntervalSec = m.PollingIntervalSec,
        StabilityWindowSec = m.StabilityWindowSec,
        BatchMode = m.BatchMode,
        MaxConsecutiveFailures = m.MaxConsecutiveFailures,
        ConsecutiveFailureCount = m.ConsecutiveFailureCount,
        State = m.State,
        LastPolledAt = m.LastPolledAt,
        CreatedAt = m.CreatedAt,
        UpdatedAt = m.UpdatedAt,
        Bindings = m.Bindings.Select(b => new MonitorJobBindingDto
        {
            Id = b.Id,
            JobId = b.JobId,
            JobName = b.Job?.Name,
        }).ToList(),
    };
}
