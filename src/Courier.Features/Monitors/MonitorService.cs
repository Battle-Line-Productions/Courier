using Courier.Domain.Common;
using Courier.Domain.Entities;
using Courier.Domain.Engine;
using Courier.Domain.Enums;
using Courier.Features.AuditLog;
using Courier.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Courier.Features.Monitors;

public class MonitorService
{
    private readonly CourierDbContext _db;
    private readonly AuditService _audit;

    public MonitorService(CourierDbContext db, AuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<ApiResponse<MonitorDto>> CreateAsync(CreateMonitorRequest request, CancellationToken ct = default)
    {
        var jobIds = request.JobIds.Distinct().ToList();
        var existingJobCount = await _db.Jobs.CountAsync(j => jobIds.Contains(j.Id), ct);
        if (existingJobCount != jobIds.Count)
        {
            return new ApiResponse<MonitorDto>
            {
                Error = ErrorMessages.Create(ErrorCodes.ResourceNotFound, "One or more specified jobs were not found.")
            };
        }

        var monitor = new FileMonitor
        {
            Id = Guid.CreateVersion7(),
            Name = request.Name,
            Description = request.Description,
            WatchTarget = request.WatchTarget,
            TriggerEvents = request.TriggerEvents,
            FilePatterns = request.FilePatterns,
            PollingIntervalSec = request.PollingIntervalSec,
            StabilityWindowSec = request.StabilityWindowSec,
            BatchMode = request.BatchMode,
            MaxConsecutiveFailures = request.MaxConsecutiveFailures,
            State = "active",
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        monitor.Bindings = jobIds.Select(jobId => new MonitorJobBinding
        {
            Id = Guid.CreateVersion7(),
            MonitorId = monitor.Id,
            JobId = jobId,
        }).ToList();

        _db.FileMonitors.Add(monitor);
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(AuditableEntityType.FileMonitor, monitor.Id, "Created", details: new { monitor.Name, monitor.WatchTarget }, ct: ct);

        var created = await _db.FileMonitors
            .Include(m => m.Bindings).ThenInclude(b => b.Job)
            .FirstAsync(m => m.Id == monitor.Id, ct);

        return new ApiResponse<MonitorDto> { Data = MapToDto(created) };
    }

    public async Task<ApiResponse<MonitorDto>> GetByIdAsync(Guid id, CancellationToken ct = default)
    {
        var monitor = await _db.FileMonitors
            .Include(m => m.Bindings).ThenInclude(b => b.Job)
            .FirstOrDefaultAsync(m => m.Id == id, ct);

        if (monitor is null)
        {
            return new ApiResponse<MonitorDto>
            {
                Error = ErrorMessages.Create(ErrorCodes.ResourceNotFound, $"Monitor with id '{id}' not found.")
            };
        }

        return new ApiResponse<MonitorDto> { Data = MapToDto(monitor) };
    }

    public async Task<PagedApiResponse<MonitorDto>> ListAsync(
        int page = 1,
        int pageSize = 25,
        string? search = null,
        string? state = null,
        CancellationToken ct = default)
    {
        pageSize = Math.Clamp(pageSize, 1, 100);
        page = Math.Max(page, 1);

        var query = _db.FileMonitors
            .Include(m => m.Bindings).ThenInclude(b => b.Job)
            .AsQueryable();

        if (!string.IsNullOrWhiteSpace(search))
        {
            var term = search.ToLower();
            query = query.Where(m => m.Name.ToLower().Contains(term));
        }

        if (!string.IsNullOrWhiteSpace(state))
            query = query.Where(m => m.State == state);

        query = query.OrderByDescending(m => m.CreatedAt);

        var totalCount = await query.CountAsync(ct);
        var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(ct);

        return new PagedApiResponse<MonitorDto>
        {
            Data = items.Select(MapToDto).ToList(),
            Pagination = new PaginationMeta(page, pageSize, totalCount, totalPages)
        };
    }

    public async Task<ApiResponse<MonitorDto>> UpdateAsync(Guid id, UpdateMonitorRequest request, CancellationToken ct = default)
    {
        var monitor = await _db.FileMonitors
            .FirstOrDefaultAsync(m => m.Id == id, ct);

        if (monitor is null)
        {
            return new ApiResponse<MonitorDto>
            {
                Error = ErrorMessages.Create(ErrorCodes.ResourceNotFound, $"Monitor with id '{id}' not found.")
            };
        }

        if (request.Name is not null) monitor.Name = request.Name;
        if (request.Description is not null) monitor.Description = request.Description;
        if (request.WatchTarget is not null) monitor.WatchTarget = request.WatchTarget;
        if (request.TriggerEvents.HasValue) monitor.TriggerEvents = request.TriggerEvents.Value;
        if (request.FilePatterns is not null) monitor.FilePatterns = request.FilePatterns;
        if (request.PollingIntervalSec.HasValue) monitor.PollingIntervalSec = request.PollingIntervalSec.Value;
        if (request.StabilityWindowSec.HasValue) monitor.StabilityWindowSec = request.StabilityWindowSec.Value;
        if (request.BatchMode.HasValue) monitor.BatchMode = request.BatchMode.Value;
        if (request.MaxConsecutiveFailures.HasValue) monitor.MaxConsecutiveFailures = request.MaxConsecutiveFailures.Value;

        if (request.JobIds is not null)
        {
            var jobIds = request.JobIds.Distinct().ToList();
            var existingJobCount = await _db.Jobs.CountAsync(j => jobIds.Contains(j.Id), ct);
            if (existingJobCount != jobIds.Count)
            {
                return new ApiResponse<MonitorDto>
                {
                    Error = ErrorMessages.Create(ErrorCodes.ResourceNotFound, "One or more specified jobs were not found.")
                };
            }

            var existingBindings = await _db.MonitorJobBindings
                .Where(b => b.MonitorId == monitor.Id)
                .ToListAsync(ct);
            _db.MonitorJobBindings.RemoveRange(existingBindings);

            foreach (var jobId in jobIds)
            {
                _db.MonitorJobBindings.Add(new MonitorJobBinding
                {
                    Id = Guid.CreateVersion7(),
                    MonitorId = monitor.Id,
                    JobId = jobId,
                });
            }
        }

        monitor.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(AuditableEntityType.FileMonitor, id, "Updated", ct: ct);

        var updated = await _db.FileMonitors
            .AsNoTracking()
            .Include(m => m.Bindings).ThenInclude(b => b.Job)
            .FirstAsync(m => m.Id == monitor.Id, ct);

        return new ApiResponse<MonitorDto> { Data = MapToDto(updated) };
    }

    public async Task<ApiResponse> DeleteAsync(Guid id, CancellationToken ct = default)
    {
        var monitor = await _db.FileMonitors.FindAsync([id], ct);

        if (monitor is null)
        {
            return new ApiResponse
            {
                Error = ErrorMessages.Create(ErrorCodes.ResourceNotFound, $"Monitor with id '{id}' not found.")
            };
        }

        monitor.IsDeleted = true;
        monitor.DeletedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(AuditableEntityType.FileMonitor, id, "Deleted", ct: ct);

        return new ApiResponse();
    }

    public async Task<ApiResponse<MonitorDto>> ActivateAsync(Guid id, CancellationToken ct = default)
    {
        var monitor = await _db.FileMonitors
            .Include(m => m.Bindings).ThenInclude(b => b.Job)
            .FirstOrDefaultAsync(m => m.Id == id, ct);

        if (monitor is null)
        {
            return new ApiResponse<MonitorDto>
            {
                Error = ErrorMessages.Create(ErrorCodes.ResourceNotFound, $"Monitor with id '{id}' not found.")
            };
        }

        var currentState = Enum.Parse<MonitorState>(monitor.State, true);
        if (currentState == MonitorState.Active)
        {
            return new ApiResponse<MonitorDto>
            {
                Error = ErrorMessages.Create(ErrorCodes.MonitorAlreadyActive, "Monitor is already active.")
            };
        }

        if (!MonitorStateMachine.CanTransition(currentState, MonitorState.Active))
        {
            return new ApiResponse<MonitorDto>
            {
                Error = ErrorMessages.Create(ErrorCodes.StateConflict, $"Cannot activate monitor from state '{monitor.State}'.")
            };
        }

        monitor.State = "active";
        if (currentState == MonitorState.Error)
            monitor.ConsecutiveFailureCount = 0;

        monitor.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(AuditableEntityType.FileMonitor, id, "Activated", ct: ct);

        return new ApiResponse<MonitorDto> { Data = MapToDto(monitor) };
    }

    public async Task<ApiResponse<MonitorDto>> PauseAsync(Guid id, CancellationToken ct = default)
    {
        var monitor = await _db.FileMonitors
            .Include(m => m.Bindings).ThenInclude(b => b.Job)
            .FirstOrDefaultAsync(m => m.Id == id, ct);

        if (monitor is null)
        {
            return new ApiResponse<MonitorDto>
            {
                Error = ErrorMessages.Create(ErrorCodes.ResourceNotFound, $"Monitor with id '{id}' not found.")
            };
        }

        var currentState = Enum.Parse<MonitorState>(monitor.State, true);
        if (!MonitorStateMachine.CanTransition(currentState, MonitorState.Paused))
        {
            return new ApiResponse<MonitorDto>
            {
                Error = ErrorMessages.Create(ErrorCodes.StateConflict, $"Cannot pause monitor from state '{monitor.State}'.")
            };
        }

        monitor.State = "paused";
        monitor.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(AuditableEntityType.FileMonitor, id, "Paused", ct: ct);

        return new ApiResponse<MonitorDto> { Data = MapToDto(monitor) };
    }

    public async Task<ApiResponse<MonitorDto>> DisableAsync(Guid id, CancellationToken ct = default)
    {
        var monitor = await _db.FileMonitors
            .Include(m => m.Bindings).ThenInclude(b => b.Job)
            .FirstOrDefaultAsync(m => m.Id == id, ct);

        if (monitor is null)
        {
            return new ApiResponse<MonitorDto>
            {
                Error = ErrorMessages.Create(ErrorCodes.ResourceNotFound, $"Monitor with id '{id}' not found.")
            };
        }

        var currentState = Enum.Parse<MonitorState>(monitor.State, true);
        if (!MonitorStateMachine.CanTransition(currentState, MonitorState.Disabled))
        {
            return new ApiResponse<MonitorDto>
            {
                Error = ErrorMessages.Create(ErrorCodes.StateConflict, $"Cannot disable monitor from state '{monitor.State}'.")
            };
        }

        monitor.State = "disabled";
        monitor.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(AuditableEntityType.FileMonitor, id, "Disabled", ct: ct);

        return new ApiResponse<MonitorDto> { Data = MapToDto(monitor) };
    }

    public async Task<ApiResponse<MonitorDto>> AcknowledgeErrorAsync(Guid id, CancellationToken ct = default)
    {
        var monitor = await _db.FileMonitors
            .Include(m => m.Bindings).ThenInclude(b => b.Job)
            .FirstOrDefaultAsync(m => m.Id == id, ct);

        if (monitor is null)
        {
            return new ApiResponse<MonitorDto>
            {
                Error = ErrorMessages.Create(ErrorCodes.ResourceNotFound, $"Monitor with id '{id}' not found.")
            };
        }

        var currentState = Enum.Parse<MonitorState>(monitor.State, true);
        if (currentState != MonitorState.Error)
        {
            return new ApiResponse<MonitorDto>
            {
                Error = ErrorMessages.Create(ErrorCodes.MonitorNotInError, "Monitor is not in error state.")
            };
        }

        monitor.State = "active";
        monitor.ConsecutiveFailureCount = 0;
        monitor.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        return new ApiResponse<MonitorDto> { Data = MapToDto(monitor) };
    }

    public async Task<PagedApiResponse<MonitorFileLogDto>> ListFileLogAsync(
        Guid monitorId,
        int page = 1,
        int pageSize = 25,
        CancellationToken ct = default)
    {
        var monitorExists = await _db.FileMonitors.AnyAsync(m => m.Id == monitorId, ct);
        if (!monitorExists)
        {
            return new PagedApiResponse<MonitorFileLogDto>
            {
                Error = ErrorMessages.Create(ErrorCodes.ResourceNotFound, $"Monitor with id '{monitorId}' not found.")
            };
        }

        pageSize = Math.Clamp(pageSize, 1, 100);
        page = Math.Max(page, 1);

        var query = _db.MonitorFileLogs
            .Where(l => l.MonitorId == monitorId)
            .OrderByDescending(l => l.TriggeredAt);

        var totalCount = await query.CountAsync(ct);
        var totalPages = (int)Math.Ceiling((double)totalCount / pageSize);

        var items = await query
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .Select(l => MapFileLogToDto(l))
            .ToListAsync(ct);

        return new PagedApiResponse<MonitorFileLogDto>
        {
            Data = items,
            Pagination = new PaginationMeta(page, pageSize, totalCount, totalPages)
        };
    }

    private static MonitorDto MapToDto(FileMonitor m) => new()
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

    private static MonitorFileLogDto MapFileLogToDto(MonitorFileLog l) => new()
    {
        Id = l.Id,
        FilePath = l.FilePath,
        FileSize = l.FileSize,
        FileHash = l.FileHash,
        LastModified = l.LastModified,
        TriggeredAt = l.TriggeredAt,
        ExecutionId = l.ExecutionId,
    };
}
