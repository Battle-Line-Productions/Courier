using Courier.Domain.Common;
using Courier.Domain.Entities;
using Courier.Domain.Enums;
using Courier.Features.AuditLog;
using Courier.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Courier.Features.Chains;

public class ChainScheduleService
{
    private readonly CourierDbContext _db;
    private readonly AuditService _audit;

    public ChainScheduleService(CourierDbContext db, AuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<ApiResponse<List<ChainScheduleDto>>> ListAsync(Guid chainId, CancellationToken ct = default)
    {
        var chainExists = await _db.JobChains.AnyAsync(c => c.Id == chainId, ct);
        if (!chainExists)
            return new ApiResponse<List<ChainScheduleDto>> { Error = ErrorMessages.Create(ErrorCodes.ChainNotFound, $"Chain '{chainId}' not found.") };

        var schedules = await _db.ChainSchedules
            .Where(s => s.ChainId == chainId)
            .OrderBy(s => s.CreatedAt)
            .ToListAsync(ct);

        return new ApiResponse<List<ChainScheduleDto>> { Data = schedules.Select(MapToDto).ToList() };
    }

    public async Task<ApiResponse<ChainScheduleDto>> CreateAsync(Guid chainId, CreateChainScheduleRequest request, CancellationToken ct = default)
    {
        var chain = await _db.JobChains.FirstOrDefaultAsync(c => c.Id == chainId, ct);
        if (chain is null)
            return new ApiResponse<ChainScheduleDto> { Error = ErrorMessages.Create(ErrorCodes.ChainNotFound, $"Chain '{chainId}' not found.") };

        var schedule = new ChainSchedule
        {
            Id = Guid.CreateVersion7(),
            ChainId = chainId,
            ScheduleType = request.ScheduleType,
            CronExpression = request.CronExpression,
            RunAt = request.RunAt,
            IsEnabled = request.IsEnabled,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        _db.ChainSchedules.Add(schedule);
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(AuditableEntityType.Chain, chainId, "ScheduleCreated", details: new { scheduleId = schedule.Id, schedule.ScheduleType, schedule.CronExpression }, ct: ct);

        return new ApiResponse<ChainScheduleDto> { Data = MapToDto(schedule) };
    }

    public async Task<ApiResponse<ChainScheduleDto>> UpdateAsync(Guid chainId, Guid scheduleId, UpdateChainScheduleRequest request, CancellationToken ct = default)
    {
        var schedule = await _db.ChainSchedules.FirstOrDefaultAsync(s => s.Id == scheduleId, ct);
        if (schedule is null)
            return new ApiResponse<ChainScheduleDto> { Error = ErrorMessages.Create(ErrorCodes.ChainScheduleNotFound, $"Schedule '{scheduleId}' not found.") };

        if (schedule.ChainId != chainId)
            return new ApiResponse<ChainScheduleDto> { Error = ErrorMessages.Create(ErrorCodes.ChainScheduleMismatch, $"Schedule '{scheduleId}' does not belong to chain '{chainId}'.") };

        if (request.CronExpression is not null)
            schedule.CronExpression = request.CronExpression;

        if (request.RunAt.HasValue)
            schedule.RunAt = request.RunAt;

        if (request.IsEnabled.HasValue)
            schedule.IsEnabled = request.IsEnabled.Value;

        schedule.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(AuditableEntityType.Chain, chainId, "ScheduleUpdated", details: new { scheduleId }, ct: ct);

        return new ApiResponse<ChainScheduleDto> { Data = MapToDto(schedule) };
    }

    public async Task<ApiResponse> DeleteAsync(Guid chainId, Guid scheduleId, CancellationToken ct = default)
    {
        var schedule = await _db.ChainSchedules.FirstOrDefaultAsync(s => s.Id == scheduleId, ct);
        if (schedule is null)
            return new ApiResponse { Error = ErrorMessages.Create(ErrorCodes.ChainScheduleNotFound, $"Schedule '{scheduleId}' not found.") };

        if (schedule.ChainId != chainId)
            return new ApiResponse { Error = ErrorMessages.Create(ErrorCodes.ChainScheduleMismatch, $"Schedule '{scheduleId}' does not belong to chain '{chainId}'.") };

        _db.ChainSchedules.Remove(schedule);
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(AuditableEntityType.Chain, chainId, "ScheduleDeleted", details: new { scheduleId }, ct: ct);

        return new ApiResponse();
    }

    private static ChainScheduleDto MapToDto(ChainSchedule s) => new()
    {
        Id = s.Id,
        ChainId = s.ChainId,
        ScheduleType = s.ScheduleType,
        CronExpression = s.CronExpression,
        RunAt = s.RunAt,
        IsEnabled = s.IsEnabled,
        LastFiredAt = s.LastFiredAt,
        NextFireAt = s.NextFireAt,
        CreatedAt = s.CreatedAt,
        UpdatedAt = s.UpdatedAt,
    };
}
