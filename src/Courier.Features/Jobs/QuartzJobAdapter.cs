using Courier.Domain.Entities;
using Courier.Domain.Enums;
using Courier.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Courier.Features.Jobs;

[DisallowConcurrentExecution]
public class QuartzJobAdapter : IJob
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<QuartzJobAdapter> _logger;

    public QuartzJobAdapter(IServiceScopeFactory scopeFactory, ILogger<QuartzJobAdapter> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public async Task Execute(IJobExecutionContext context)
    {
        var jobIdStr = context.MergedJobDataMap.GetString("jobId");
        if (!Guid.TryParse(jobIdStr, out var jobId))
        {
            _logger.LogWarning("QuartzJobAdapter: invalid or missing jobId in data map");
            return;
        }

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CourierDbContext>();

        var job = await db.Jobs
            .Include(j => j.Steps)
            .FirstOrDefaultAsync(j => j.Id == jobId);

        if (job is null)
        {
            _logger.LogWarning("QuartzJobAdapter: job {JobId} not found, skipping", jobId);
            return;
        }

        if (!job.IsEnabled)
        {
            _logger.LogInformation("QuartzJobAdapter: job {JobId} is disabled, skipping", jobId);
            return;
        }

        if (job.Steps.Count == 0)
        {
            _logger.LogWarning("QuartzJobAdapter: job {JobId} has no steps, skipping", jobId);
            return;
        }

        var execution = new JobExecution
        {
            Id = Guid.CreateVersion7(),
            JobId = jobId,
            JobVersionNumber = job.CurrentVersion,
            TriggeredBy = "schedule",
            State = JobExecutionState.Queued,
            QueuedAt = DateTime.UtcNow,
            CreatedAt = DateTime.UtcNow,
        };

        db.JobExecutions.Add(execution);

        // Update schedule metadata
        var scheduleId = Guid.Parse(context.JobDetail.Key.Name);
        var schedule = await db.JobSchedules.FirstOrDefaultAsync(s => s.Id == scheduleId);
        if (schedule is not null)
        {
            schedule.LastFiredAt = DateTimeOffset.UtcNow;
            schedule.NextFireAt = context.NextFireTimeUtc;

            if (schedule.ScheduleType == "one_shot")
                schedule.IsEnabled = false;
        }

        await db.SaveChangesAsync();

        _logger.LogInformation(
            "QuartzJobAdapter: created execution {ExecutionId} for job {JobId} (triggered by schedule {ScheduleId})",
            execution.Id, jobId, scheduleId);
    }
}
