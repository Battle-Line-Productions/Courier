using Courier.Domain.Entities;
using Courier.Features.Chains;
using Courier.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Quartz;

namespace Courier.Worker.Services;

public class ChainScheduleManager
{
    private const string QuartzGroup = "courier-chains";

    private readonly ISchedulerFactory _schedulerFactory;
    private readonly CourierDbContext _db;

    public ChainScheduleManager(ISchedulerFactory schedulerFactory, CourierDbContext db)
    {
        _schedulerFactory = schedulerFactory;
        _db = db;
    }

    public async Task RegisterAsync(ChainSchedule schedule)
    {
        var scheduler = await _schedulerFactory.GetScheduler();
        var jobKey = new JobKey(schedule.Id.ToString(), QuartzGroup);
        var triggerKey = new TriggerKey(schedule.Id.ToString(), QuartzGroup);

        var jobDetail = JobBuilder.Create<QuartzChainAdapter>()
            .WithIdentity(jobKey)
            .UsingJobData("chainId", schedule.ChainId.ToString())
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

    public async Task UnregisterAsync(Guid scheduleId)
    {
        var scheduler = await _schedulerFactory.GetScheduler();
        var jobKey = new JobKey(scheduleId.ToString(), QuartzGroup);
        await scheduler.DeleteJob(jobKey);
    }

    public async Task SyncAllAsync(CancellationToken ct = default)
    {
        var enabledSchedules = await _db.ChainSchedules
            .Where(s => s.IsEnabled)
            .ToListAsync(ct);

        var quartzScheduleIds = new HashSet<string>(enabledSchedules.Select(s => s.Id.ToString()));

        // Register enabled schedules
        foreach (var schedule in enabledSchedules)
        {
            await UnregisterAsync(schedule.Id);
            await RegisterAsync(schedule);
        }

        // Unregister Quartz jobs for disabled/deleted schedules
        var scheduler = await _schedulerFactory.GetScheduler(ct);
        var quartzJobKeys = await scheduler.GetJobKeys(Quartz.Impl.Matchers.GroupMatcher<JobKey>.GroupEquals(QuartzGroup), ct);
        foreach (var key in quartzJobKeys)
        {
            if (!quartzScheduleIds.Contains(key.Name))
                await scheduler.DeleteJob(key, ct);
        }
    }
}
