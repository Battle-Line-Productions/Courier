using Courier.Domain.Common;
using Courier.Domain.Entities;
using Courier.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Quartz;

namespace Courier.Features.Jobs;

public class JobScheduleService
{
    private readonly CourierDbContext _db;
    private readonly ISchedulerFactory _schedulerFactory;

    public JobScheduleService(CourierDbContext db, ISchedulerFactory schedulerFactory)
    {
        _db = db;
        _schedulerFactory = schedulerFactory;
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

        if (schedule.IsEnabled)
            await RegisterWithQuartzAsync(schedule);

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

        if (schedule.IsEnabled)
            await RegisterWithQuartzAsync(schedule);
        else
            await UnregisterFromQuartzAsync(schedule.Id);

        return new ApiResponse<JobScheduleDto> { Data = MapToDto(schedule) };
    }

    public async Task<ApiResponse> DeleteAsync(Guid jobId, Guid scheduleId, CancellationToken ct = default)
    {
        var schedule = await _db.JobSchedules.FirstOrDefaultAsync(s => s.Id == scheduleId, ct);
        if (schedule is null)
            return new ApiResponse { Error = ErrorMessages.Create(ErrorCodes.ScheduleNotFound, $"Schedule '{scheduleId}' not found.") };

        if (schedule.JobId != jobId)
            return new ApiResponse { Error = ErrorMessages.Create(ErrorCodes.ScheduleJobMismatch, $"Schedule '{scheduleId}' does not belong to job '{jobId}'.") };

        await UnregisterFromQuartzAsync(schedule.Id);
        _db.JobSchedules.Remove(schedule);
        await _db.SaveChangesAsync(ct);

        return new ApiResponse();
    }

    public async Task RegisterWithQuartzAsync(JobSchedule schedule)
    {
        var scheduler = await _schedulerFactory.GetScheduler();
        var jobKey = new JobKey(schedule.Id.ToString(), "courier");
        var triggerKey = new TriggerKey(schedule.Id.ToString(), "courier");

        var jobDetail = JobBuilder.Create<QuartzJobAdapter>()
            .WithIdentity(jobKey)
            .UsingJobData("jobId", schedule.JobId.ToString())
            .StoreDurably()
            .Build();

        TriggerBuilder triggerBuilder = TriggerBuilder.Create()
            .WithIdentity(triggerKey)
            .ForJob(jobKey);

        if (schedule.ScheduleType == "cron")
        {
            triggerBuilder.WithCronSchedule(schedule.CronExpression!);
        }
        else
        {
            triggerBuilder.StartAt(schedule.RunAt!.Value)
                .WithSimpleSchedule(x => x.WithRepeatCount(0));
        }

        var trigger = triggerBuilder.Build();

        await scheduler.ScheduleJob(jobDetail, [trigger], replace: true);

        schedule.NextFireAt = trigger.GetNextFireTimeUtc();
        await _db.SaveChangesAsync();
    }

    public async Task UnregisterFromQuartzAsync(Guid scheduleId)
    {
        var scheduler = await _schedulerFactory.GetScheduler();
        var jobKey = new JobKey(scheduleId.ToString(), "courier");
        await scheduler.DeleteJob(jobKey);
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
