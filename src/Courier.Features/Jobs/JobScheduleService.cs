using Courier.Domain.Common;
using Courier.Domain.Entities;
using Courier.Domain.Enums;
using Courier.Features.AuditLog;
using Courier.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace Courier.Features.Jobs;

public class JobScheduleService
{
    private readonly CourierDbContext _db;
    private readonly AuditService _audit;

    public JobScheduleService(CourierDbContext db, AuditService audit)
    {
        _db = db;
        _audit = audit;
    }

    public async Task<ApiResponse<List<JobScheduleDto>>> ListAsync(Guid jobId, CancellationToken ct = default)
    {
        var jobExists = await _db.Jobs.AnyAsync(j => j.Id == jobId, ct);
        if (!jobExists)
            return new ApiResponse<List<JobScheduleDto>> { Error = ErrorMessages.Create(ErrorCodes.ResourceNotFound, $"Job '{jobId}' not found.") };

        var schedules = await _db.JobSchedules
            .Where(s => s.JobId == jobId)
            .OrderBy(s => s.CreatedAt)
            .ToListAsync(ct);

        return new ApiResponse<List<JobScheduleDto>> { Data = schedules.Select(MapToDto).ToList() };
    }

    public async Task<ApiResponse<JobScheduleDto>> CreateAsync(Guid jobId, CreateJobScheduleRequest request, CancellationToken ct = default)
    {
        var job = await _db.Jobs.FirstOrDefaultAsync(j => j.Id == jobId, ct);
        if (job is null)
            return new ApiResponse<JobScheduleDto> { Error = ErrorMessages.Create(ErrorCodes.ResourceNotFound, $"Job '{jobId}' not found.") };

        var schedule = new JobSchedule
        {
            Id = Guid.CreateVersion7(),
            JobId = jobId,
            ScheduleType = request.ScheduleType,
            CronExpression = request.CronExpression,
            RunAt = request.RunAt,
            IsEnabled = request.IsEnabled,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
        };

        _db.JobSchedules.Add(schedule);
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(AuditableEntityType.Job, jobId, "ScheduleCreated", details: new { scheduleId = schedule.Id, schedule.ScheduleType, schedule.CronExpression }, ct: ct);

        return new ApiResponse<JobScheduleDto> { Data = MapToDto(schedule) };
    }

    public async Task<ApiResponse<JobScheduleDto>> UpdateAsync(Guid jobId, Guid scheduleId, UpdateJobScheduleRequest request, CancellationToken ct = default)
    {
        var schedule = await _db.JobSchedules.FirstOrDefaultAsync(s => s.Id == scheduleId, ct);
        if (schedule is null)
            return new ApiResponse<JobScheduleDto> { Error = ErrorMessages.Create(ErrorCodes.ScheduleNotFound, $"Schedule '{scheduleId}' not found.") };

        if (schedule.JobId != jobId)
            return new ApiResponse<JobScheduleDto> { Error = ErrorMessages.Create(ErrorCodes.ScheduleJobMismatch, $"Schedule '{scheduleId}' does not belong to job '{jobId}'.") };

        if (request.CronExpression is not null)
            schedule.CronExpression = request.CronExpression;

        if (request.RunAt.HasValue)
            schedule.RunAt = request.RunAt;

        if (request.IsEnabled.HasValue)
            schedule.IsEnabled = request.IsEnabled.Value;

        schedule.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(AuditableEntityType.Job, jobId, "ScheduleUpdated", details: new { scheduleId }, ct: ct);

        return new ApiResponse<JobScheduleDto> { Data = MapToDto(schedule) };
    }

    public async Task<ApiResponse> DeleteAsync(Guid jobId, Guid scheduleId, CancellationToken ct = default)
    {
        var schedule = await _db.JobSchedules.FirstOrDefaultAsync(s => s.Id == scheduleId, ct);
        if (schedule is null)
            return new ApiResponse { Error = ErrorMessages.Create(ErrorCodes.ScheduleNotFound, $"Schedule '{scheduleId}' not found.") };

        if (schedule.JobId != jobId)
            return new ApiResponse { Error = ErrorMessages.Create(ErrorCodes.ScheduleJobMismatch, $"Schedule '{scheduleId}' does not belong to job '{jobId}'.") };

        _db.JobSchedules.Remove(schedule);
        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync(AuditableEntityType.Job, jobId, "ScheduleDeleted", details: new { scheduleId }, ct: ct);

        return new ApiResponse();
    }

    private static JobScheduleDto MapToDto(JobSchedule s) => new()
    {
        Id = s.Id,
        JobId = s.JobId,
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
